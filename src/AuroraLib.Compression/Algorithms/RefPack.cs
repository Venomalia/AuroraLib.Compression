﻿using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// RefPack is an LZ compression format made by Frank Barchard of EA Canada
    /// http://wiki.niotso.org/RefPack
    /// </summary>
    public sealed class RefPack : ICompressionAlgorithm, ILzSettings
    {
        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        private static readonly LzProperties _lz = new(0x20000, 1028, 3);

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && stream.Read<Header>().IsValid && stream.ReadInt24(Endian.Big) != 0;

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            Header header = source.Read<Header>();
            uint uncompressedSize = header.IsInt32 ? source.ReadUInt32(Endian.Big) : source.ReadUInt24(Endian.Big);
            uint compressedSize = 0;
            if (header.HasCompressedSize)
            {
                compressedSize = header.IsInt32 ? source.ReadUInt32(Endian.Big) : source.ReadUInt24(Endian.Big);
            }

            DecompressHeaderless(source, destination, (int)uncompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(new Header(true, false));
            destination.Write(source.Length, Endian.Big);
            CompressHeaderless(source, destination, LookAhead, level);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using LzWindows buffer = new(destination, _lz.WindowsSize);

            while (source.Position < source.Length)
            {
                int plainSize, length = 0, distance = 0;

                byte prefix = source.ReadUInt8();
                if ((prefix & 0x80) == 0) // P = 0-3 D = 1-1024 L = 3-10
                {
                    // 0DDLLLPP DDDDDDDD
                    byte data0 = source.ReadUInt8(); // DDDDDDDD
                    plainSize = prefix & 0x03;
                    length = ((prefix & 0x1C) >> 2) + 3;
                    distance = (((prefix & 0x60) << 3) | data0) + 1;
                }
                else if ((prefix & 0x40) == 0) //P = 0-3 D = 1-16384 L = 4-67
                {
                    //10LLLLLL PPDDDDDD DDDDDDDD
                    byte data0 = source.ReadUInt8(); // PPDDDDDD
                    byte data1 = source.ReadUInt8(); // DDDDDDDD

                    plainSize = data0 >> 6;
                    length = (prefix & 0x3F) + 4;
                    distance = (((data0 & 0x3F) << 8) | data1) + 1;
                }
                else if ((prefix & 0x20) == 0) //P = 0-3 D = 1-131072 L = 5-1028
                {
                    //110DLLPP DDDDDDDD DDDDDDDD LLLLLLLL
                    byte data0 = source.ReadUInt8(); // DDDDDDDD
                    byte data1 = source.ReadUInt8(); // DDDDDDDD
                    byte data2 = source.ReadUInt8(); // LLLLLLLL

                    plainSize = prefix & 3;
                    length = (((prefix & 0x0C) << 6) | data2) + 5;
                    distance = (((((prefix & 0x10) << 4) | data0) << 8) | data1) + 1;
                }
                else // P = 4-112
                {
                    // 111PPPPP
                    plainSize = (prefix & 0x1F) * 4 + 4;
                    if (plainSize > 0x70) // P = 0-3
                    {
                        // 111111PP
                        plainSize = prefix & 3;
                        buffer.CopyFrom(source, plainSize);

                        if (destination.Position + buffer.Position > endPosition)
                        {
                            throw new DecompressedSizeException(decomLength, destination.Position + buffer.Position - (endPosition - decomLength));
                        }
                        return; // end
                    }
                }

                buffer.CopyFrom(source, plainSize);
                buffer.BackCopy(distance, length);
            }
            throw new EndOfStreamException();
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0;
            LzMatchFinder dictionary = new(_lz, lookAhead, level);

            int plainSize = 0;
            while (sourcePointer < source.Length)
            {
                LzMatch match = default;
                while (sourcePointer + plainSize < source.Length)
                {
                    dictionary.FindMatch(source, sourcePointer + plainSize, out match);
                    if (match.Length != 0 && (match.Distance <= 0x400 || match.Length >= 5 || (match.Length >= 4 && match.Distance <= 0x4000))) break;
                    dictionary.AddEntry(source, sourcePointer + plainSize);
                    plainSize++;
                }

                while (plainSize > 3)
                {
                    int copyflag = (plainSize > 0x70 ? 0x70 : plainSize) / 4 - 1;
                    destination.Write((byte)(0xE0 | copyflag));
                    copyflag = copyflag * 4 + 4;
                    destination.Write(source.Slice(sourcePointer, copyflag));
                    sourcePointer += copyflag;
                    plainSize -= copyflag;
                }

                if (match.Length != 0)
                {
                    if (match.Length <= 10 && match.Distance <= 0x400) // P = 0-3 D = 1-1024 L = 3-10
                    {
                        destination.Write((byte)(plainSize | (((match.Distance - 1) & 0x300) >> 3) | ((match.Length - 3) << 2))); // 0DDLLLPP
                        destination.Write((byte)(match.Distance - 1)); // DDDDDDDD
                    }
                    else if (match.Length >= 4 && match.Length <= 67 && match.Distance <= 0x4000)
                    {
                        destination.Write((byte)(0x80 | match.Length - 4)); // 10LLLLLL
                        destination.Write((byte)(((match.Distance - 1) >> 8) | (plainSize << 6))); // PPDDDDDD
                        destination.Write((byte)(match.Distance - 1)); // DDDDDDDD
                    }
                    else
                    {
                        destination.Write((byte)(0xC0 | ((match.Distance - 1) >> 16 << 4) | ((match.Length - 5) >> 8 << 2) | plainSize)); // 110DLLPP
                        destination.Write((byte)((match.Distance - 1) >> 8)); // DDDDDDDD
                        destination.Write((byte)(match.Distance - 1)); // DDDDDDDD
                        destination.Write((byte)(match.Length - 5)); // LLLLLLLL
                    }
                    dictionary.AddEntryRange(source, sourcePointer + plainSize, match.Length);
                    destination.Write(source.Slice(sourcePointer, plainSize));
                    sourcePointer += plainSize + match.Length;
                    plainSize = 0;
                }
                else
                {
                    break;
                }
            }
            destination.Write((byte)(0xFC | plainSize)); // 111111PP
        }

        public readonly struct Header
        {
            internal readonly byte value1;
            internal readonly byte value2;

            public Header(bool isInt32 = true, bool hasCompressedSize = false, bool unkFlag = false)
            {
                value1 = 0x10;
                if (isInt32) value1 |= 0x80;
                if (hasCompressedSize) value1 |= 0x1;
                if (unkFlag) value1 |= 0x40;
                value2 = 0xFB;
            }

            public readonly bool HasCompressedSize => (value1 & 0x1) != 0;
            public readonly bool UnkFlag => (value1 & 0x40) != 0;
            public readonly bool IsInt32 => (value1 & 0x80) != 0;
            public readonly bool IsValid => (value1 & 0x3E) == 0x10 || (value2 == 0xFB);
        }
    }
}
