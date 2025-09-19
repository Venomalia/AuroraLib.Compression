using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Exceptions;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// RefPack is an LZ compression format made by Frank Barchard of EA Canada
    /// http://wiki.niotso.org/RefPack
    /// </summary>
    public sealed class RefPack : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {
        private const byte Identifier = 0xFB;

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<RefPack>("RefPack", new MediaType(MIMEType.Application, "x-ea-refpack"), string.Empty);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <summary>
        /// Set specific <see cref="RefPack"/> encode options.
        /// </summary>
        public OptionFlags Options = OptionFlags.Default | OptionFlags.UsePreHeader;

        private static readonly LzProperties[] lzProperties = new LzProperties[]
        {
            new LzProperties(0x20000, 1028, 5),
            new LzProperties(0x4000, 67, 4),
            new LzProperties(0x400, 10, 3),
        };

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x8 < stream.Length && (
            stream.Peek(s => ((OptionFlags)s.ReadByte()).HasFlag(OptionFlags.Default) && s.ReadByte() == Identifier && s.ReadInt24(Endian.Big) != 0) || // Version 1 & 3
            stream.Peek(s => s.ReadInt32() != 0 && s.ReadUInt16(Endian.Big) == 0x10FB && s.ReadInt24(Endian.Big) != 0)); // Version 2

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                _ = InternalReadHeader(source, out uint decompressedSize, out uint _);
                return decompressedSize;
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            // Read Header
            Options = InternalReadHeader(source, out uint decompressedSize, out uint compressedSize);

            // Mark the initial positions of the streams
            long compressedStartPosition = source.Position;

            // Perform the decompression
            DecompressHeaderless(source, destination, (int)decompressedSize);
        }

        private static OptionFlags InternalReadHeader(Stream source, out uint decompressedSize, out uint compressedSize)
        {
            OptionFlags flag = (OptionFlags)source.ReadByte();
            byte identifier = source.ReadUInt8();
            if (identifier != Identifier)
            {
                source.Position -= 0x2;
                compressedSize = source.ReadUInt32();
                // test Is version 2
                if (source.Peek<ushort>(Endian.Big) == 0x10FB)
                    return InternalReadHeader(source, out decompressedSize, out _);
                else
                    throw new InvalidIdentifierException(identifier.ToString("X"), Identifier.ToString("X"));
            }

            if (!flag.HasFlag(OptionFlags.Default))
                throw new NotSupportedException($"No supported Flag {flag}");

            bool IsInt32 = flag.HasFlag(OptionFlags.UseInt32);
            decompressedSize = IsInt32 ? source.ReadUInt32(Endian.Big) : source.ReadUInt24(Endian.Big);
            if (flag.HasFlag(OptionFlags.StoresCompressedSize))
                compressedSize = IsInt32 ? source.ReadUInt32(Endian.Big) : source.ReadUInt24(Endian.Big);
            else
                compressedSize = 0;
            return flag;
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            if (Options.HasFlag(OptionFlags.UsePreHeader))
            {
                // Write Header Version 2
                if (source.Length >= 0xFFFFFF)
                    throw new NotSupportedException("RefPack Version 2 does not support files over 16MB.");

                long compressedSizesOffset = destination.Position;

                destination.Write(0); //Write CompressedSize Placeholder
                destination.WriteByte(0x10);
                destination.WriteByte(Identifier);
                destination.Write((UInt24)source.Length, Endian.Big);

                // Perform the compression
                CompressHeaderless(source, destination, LookAhead, lzProperties[0].GetWindowsLevel(level));

                // 135280
                // Go back to the beginning of the file and write out the compressed length
                destination.At(compressedSizesOffset, s => s.Write((uint)(destination.Length - compressedSizesOffset - 4)));
            }
            else
            {
                // Write Header Version 1 or 3
                Options |= OptionFlags.Default;
                bool IsInt32 = Options.HasFlag(OptionFlags.UseInt32);
                bool StoresCompressedSize = Options.HasFlag(OptionFlags.StoresCompressedSize);
                if (!IsInt32 && source.Length >= 0xFFFFFF)
                {
                    Options |= OptionFlags.UseInt32;
                    IsInt32 = true;
                    Trace.WriteLine("File size exceeds 16MB. Switching to 32-bit Mode RefPack Version 3.");
                }

                destination.WriteByte((byte)Options);
                destination.WriteByte(Identifier);

                //Write DecompressedSize
                if (IsInt32)
                    destination.Write(source.Length, Endian.Big);
                else
                    destination.Write((UInt24)source.Length, Endian.Big);

                // Mark the CompressedSize positions
                long compressedSizesOffset = destination.Position;

                //Write CompressedSize Placeholder
                if (StoresCompressedSize)
                    if (IsInt32)
                        destination.Write(0);
                    else
                        destination.Write((UInt24)0);

                // Perform the compression
                CompressHeaderless(source, destination, LookAhead, lzProperties[0].GetWindowsLevel(level));

                // Go back to the beginning of the file and write out the compressed length
                if (StoresCompressedSize)
                {
                    uint compressedSizes = (uint)(destination.Length - compressedSizesOffset - (IsInt32 ? 4 : 3));
                    destination.At(compressedSizesOffset, s =>
                    {
                        if (IsInt32)
                            s.Write(compressedSizes, Endian.Big);
                        else
                            s.Write((UInt24)compressedSizes, Endian.Big);
                    });
                }

            }
        }

        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using LzWindows buffer = new LzWindows(destination, lzProperties[0].WindowsSize);

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

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, int maxWindowsSize = 0x200000)
        {
            int sourcePointer = 0x0, plainSize = 0;
            using PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, lzProperties, maxWindowsSize, lookAhead);

            matches.Add(new LzMatch(source.Length, 0, 0)); // Dummy-Match
            foreach (LzMatch match in matches)
            {
#if DEBUG
                //No longer needed
                if ((match.Distance > 0x4000 && match.Length < 5) || (match.Distance > 0x400 && match.Length < 4))
                    continue;
#endif
                plainSize = match.Offset - sourcePointer;

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
                    destination.Write(source.Slice(sourcePointer, plainSize));
                    sourcePointer += plainSize + match.Length;
                    plainSize = 0;
                }
            }
            destination.Write((byte)(0xFC | plainSize)); // 111111PP
        }

        /// <summary>
        /// All support <see cref="RefPack"/> options.
        /// </summary>
        [Flags]
        public enum OptionFlags : byte
        {
            /// <summary>
            /// Saves the compressed size of the file.
            /// </summary>
            StoresCompressedSize = 0x01,

            /// <summary>
            /// Use header type version 2.
            /// </summary>
            UsePreHeader = 0x02,

            /// <summary>
            /// Use RefPack Encoding (Always set)
            /// </summary>
            Default = 0x10,

            //Huffman = 0x20 | Default,

            //RunLength = 0x4A,

            /// <summary>
            /// Unknown, only for header version 3.
            /// </summary>
            Unknown = 0x40,

            /// <summary>
            /// Saves the uncompressed and compressed size as int32, only for header version 3.
            /// </summary>
            UseInt32 = 0x80,
        }
    }
}
