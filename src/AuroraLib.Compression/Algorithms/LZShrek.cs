﻿using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.Buffers;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// LZShrek compression algorithm used in Shrek Super Slam.
    /// </summary>
    public sealed class LZShrek : ICompressionAlgorithm, ILzSettings
    {
        private static readonly LzProperties _lz = new(0x1000, 262, 3);

        public bool LookAhead { get; set; } = true;

        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.Read<int>() == 0x10 && stream.Read<int>() != 0 && stream.Read<int>() == stream.Length - 0x10 && stream.Read<int>() == 0;

        public void Decompress(Stream source, Stream destination)
        {
            uint offset = source.ReadUInt32();
            uint decomLength = source.ReadUInt32();
            uint compLength = source.ReadUInt32();
            source.Seek(offset, SeekOrigin.Begin);

            using SpanBuffer<byte> sourceBuffer = new(compLength);
            source.Read(sourceBuffer);
            DecompressHeaderless(sourceBuffer, destination, (int)decomLength);
        }

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(0x10);
            destination.Write(source.Length);
            destination.Write(0x0);
            destination.Write(0x0);

            long start = destination.Position;
            CompressHeaderless(source, destination, LookAhead, level);
            uint compLength = (uint)(destination.Position - start);
            destination.At(start - 8, s => s.Write(compLength));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void DecompressHeaderless(ReadOnlySpan<byte> source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using LzWindows buffer = new(destination, _lz.WindowsSize);
            int sourcePointer = 0;

            while (sourcePointer < source.Length)
            {
                byte flag = source[sourcePointer++];
                int compressed = (flag & 7) + 1; // 1-8
                int uncompressed = ReadDistance(flag, source, ref sourcePointer);

                if (uncompressed != 0)
                {
                    buffer.Write(source.Slice(sourcePointer, uncompressed));
                    sourcePointer += uncompressed;
                }

                for (int i = 0; i < compressed; i++)
                {
                    flag = source[sourcePointer++];
                    int length = flag & 7; // 1-7 | 0 flag

                    if (length == 0)
                    {
                        length = source[sourcePointer++];
                        if (length == 0)
                        {
                            if (destination.Position + buffer.Position > endPosition)
                            {
                                throw new DecompressedSizeException(decomLength, destination.Position + buffer.Position - (endPosition - decomLength));
                            }
                            return; // end~
                        }
                        length += 7; // 7-262
                    }

                    int distance = ReadDistance(flag, source, ref sourcePointer) + 1;

                    buffer.BackCopy(distance, length);
                }
            }
            throw new EndOfStreamException();
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0;
            LzMatchFinder dictionary = new(_lz, lookAhead, level);
            using MemoryPoolStream buffer = new();

            while (sourcePointer < source.Length)
            {
                int uncompressedLength = 0, compressedLength = 0;

                if (!dictionary.TryToFindMatch(source, sourcePointer, out LzMatch match))
                {
                    uncompressedLength++;
                    // How long comes no new match?
                    while (sourcePointer + uncompressedLength < source.Length && !dictionary.TryToFindMatch(source, sourcePointer + uncompressedLength, out match))
                    {
                        uncompressedLength++;
                    }

                    buffer.Write(source.Slice(sourcePointer, uncompressedLength));
                    sourcePointer += uncompressedLength;
                }

                // Match has data that still needs to be processed?
                while (match.Length != 0)
                {
                    compressedLength++;

                    // max is DDDDDLLL (LLLLLLLL) ((DDDDDDDD) DDDDDDDD)
                    int lengthflag = match.Length > 7 ? 0 : match.Length;
                    int distanceflag = match.Distance > 30 ? (match.Distance > 286 ? 0x1F : 0x1E) : match.Distance - 1;
                    buffer.WriteByte((byte)(distanceflag << 3 | (lengthflag)));
                    if (lengthflag == 0) // match.Length 8-262
                    {
                        buffer.WriteByte((byte)(match.Length - 7));
                    }

                    if (distanceflag == 0x1E) // 31-286
                    {
                        buffer.WriteByte((byte)(match.Distance - 31));
                    }
                    else if (distanceflag == 0x1F) // 287-65822
                    {
                        buffer.Write((ushort)(match.Distance - 287));
                    }

                    sourcePointer += match.Length;
                    if (sourcePointer + compressedLength < source.Length && compressedLength < 8)
                    {
                        dictionary.FindMatch(source, sourcePointer, out match);
                        if (match.Length != 0)
                        {
                            dictionary.AddEntryRange(source, sourcePointer, match.Length);
                        }
                    }
                    else
                    {
                        match = default;
                    }
                }
                // Write flag and buffer to destination.
                int uncompressedflag = uncompressedLength > 29 ? (uncompressedLength > 285 ? 0x1F : 0x1E) : uncompressedLength;
                compressedLength = Math.Max(0, compressedLength - 1);
                destination.WriteByte((byte)(uncompressedflag << 3 | compressedLength));

                if (uncompressedflag == 0x1E) // 30-285
                {
                    destination.WriteByte((byte)(uncompressedLength - 30));
                }
                else if (uncompressedflag == 0x1F) // 286-65821
                {
                    destination.Write((ushort)(uncompressedLength - 286));
                }

                buffer.WriteTo(destination);
                buffer.SetLength(0);
            }
            destination.Write(0);
        }

        private static int ReadDistance(byte flag, ReadOnlySpan<byte> source, ref int sourcePointer)
        {
            int distance = flag >> 3; // 0-29 | 30,31 flag
            if (distance == 0x1E) // 30-285
            {
                distance += source[sourcePointer++];
            }
            else if (distance == 0x1F) // 286-65821
            {
                distance = 286;
                distance += source[sourcePointer++];
                distance += (source[sourcePointer++] << 8);
            }
            return distance;
        }
    }
}
