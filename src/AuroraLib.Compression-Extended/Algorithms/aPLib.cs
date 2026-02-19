using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Exceptions;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// aPLib is one of the top pure LZ-based compression algorithm by Jørgen Ibsen.
    /// </summary>
    public sealed class aPLib : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {

        private static readonly Identifier32 _identifier = new Identifier32((byte)'A', (byte)'P', (byte)'3', (byte)'2');

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<aPLib>("Ibsen Software aPLib pack", new MediaType(MIMEType.Application, "x-aplib"), string.Empty);

        private static readonly LzProperties[] lzProperties = new LzProperties[]
        {
            new LzProperties(0x200000, int.MaxValue, 8, 0, 2),
            new LzProperties(0x500, int.MaxValue, 3, 0, 2),
            new LzProperties(0x80, 3, 2),
            new LzProperties(0x800, int.MaxValue, 4, 0, 2),
            new LzProperties(0x2000, int.MaxValue, 5, 0, 2),
            new LzProperties(0x7800, int.MaxValue, 7, 0, 2),
        };


        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.Match(_identifier) && s.ReadUInt32() == 24);

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                source.Position += 12;
                return s.ReadUInt32();
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {

            if (!source.Match(_identifier))
            {
                source.Position -= 4;
                DecompressHeaderless(source, destination);
                return;
            }
            uint headerSize = source.ReadUInt32();
            uint compressedSize = source.ReadUInt32();
            _ = source.ReadUInt32();
            uint decompressedSize = source.ReadUInt32();
            _ = source.ReadUInt32();
            source.Position += (headerSize - 24);

            // Decompress
            long compressedStart = source.Position;
            long decompressedStart = destination.Position;
            DecompressHeaderless(source, destination);

            // Verify compressed size
            Helper.TraceIfCompressedSizeMismatch(source.Position - compressedStart, compressedSize);

            // Verify decompressed size
            long actual = destination.Position - decompressedStart;
            if (actual != decompressedSize)
                throw new DecompressedSizeException(decompressedSize, actual);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Compress the data without a header
            using MemoryPoolStream buffer = new MemoryPoolStream();
            CompressHeaderless(source, buffer, LookAhead, lzProperties[0].GetWindowsLevel(level));
            Span<byte> compressedData = buffer.UnsafeAsSpan();
            // Header layout (24 bytes total): 107146
            destination.Write(_identifier);             // 'AP32' tag
            destination.Write(24);                      // Header size in bytes
            destination.Write(compressedData.Length);   // Compressed size
            destination.Write(0);           // CRC32 of compressed data
            destination.Write(source.Length);           // Original size
            destination.Write(0);             // CRC32 of original data

            //Write the actual compressed data
            destination.Write(compressedData);
        }

        public static void DecompressHeaderless(Stream source, Stream destination)
        {
            int lastOffset = 0, offset, length;
            bool lwm = false;

            FlagReader flag = new FlagReader(source, Endian.Big);
            using var buffer = new LzWindows(destination, 0x200000);

            buffer.WriteByte(source.ReadUInt8());
            while (true)
            {
                int prefix = 0;
                while (prefix < 3 && flag.Readbit())
                    prefix++;

                switch (prefix)
                {
                    case 0: // Literal
                        buffer.WriteByte(source.ReadUInt8());
                        lwm = false;
                        break;
                    case 1: // offset = 1-2097152 length = 2-2147483647
                        {
                            offset = ReadGamma(flag);

                            if (!lwm && offset == 2)
                            {
                                // repeat last offset
                                offset = lastOffset;
                                length = ReadGamma(flag);
                                buffer.BackCopy(offset, length);
                            }
                            else
                            {
                                offset -= lwm ? 2 : 3;
                                offset = (offset << 8) | source.ReadUInt8();
                                length = ReadGamma(flag);
                                length += LengthDelta(offset);

                                buffer.BackCopy(offset, length);
                                lastOffset = offset;
                            }
                            lwm = true;
                        }
                        break;

                    case 2: // offset = 1-127 length = 2-3
                        {
                            offset = source.ReadUInt8();
                            length = 2 + (offset & 0x1);
                            offset >>= 1;

                            if (offset == 0)
                                return; // end

                            buffer.BackCopy(offset, length);

                            lastOffset = offset;
                            lwm = true;
                        }
                        break;

                    case 3: // offset = 1-15 length = 1
                        {
                            offset = flag.ReadInt(4, true);

                            if (offset > 0)
                                buffer.BackCopy(offset, 1);
                            else
                                buffer.WriteByte(0);

                            lwm = false;
                        }
                        break;
                }
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, int maxWindowsSize = 0x200000)
        {
            int srcPtr = 0, matchPtr = 0, lastOffset = 0;
            bool pair = true;

            using FlagWriter flag = new FlagWriter(destination, Endian.Big);

            using PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, lzProperties, maxWindowsSize, lookAhead);
            // 1) First literal: written raw (no marker)
            destination.WriteByte(source[srcPtr++]);

            while (srcPtr < source.Length)
            {
                // Check for match starting at current position
                if (matchPtr < matches.Count && matches[matchPtr].Offset == srcPtr)
                {
                    LzMatch match = matches[matchPtr++];

                    if (pair && lastOffset == match.Distance && match.Length >= 2) // --- Repeat-last-offset case ---
                    {
                        flag.WriteBit(true);
                        flag.WriteBit(false);
                        WriteGamma(flag, 2);//Flag
                        WriteGamma(flag, match.Length);
                    }
                    else if (match.Length <= 3 && match.Distance <= 127) // --- SHORTBLOCK (length 2..3 && distance <= 127) ---
                    {
                        flag.WriteBit(true);
                        flag.WriteBit(true);
                        flag.Buffer.WriteByte((byte)((match.Distance << 1) | (match.Length - 2)));
                        flag.WriteBit(false);
                    }
                    else // --- NORMAL BLOCK (length >= 2, distance >= 2) ---
                    {
                        int lengthDelta = match.Length - LengthDelta(match.Distance);
                        if (lengthDelta < 2)
                        {
                            continue;
                        }
                        flag.WriteBit(true);
                        flag.WriteBit(false);

                        // compute high part for gamma
                        int high = (match.Distance >> 8) + 2;
                        if (pair)
                            high += 1; // pair bias

                        WriteGamma(flag, high);

                        // low byte
                        flag.Buffer.WriteByte((byte)(match.Distance));
                        flag.FlushIfNecessary();
                        // length adjusted
                        WriteGamma(flag, lengthDelta);
                    }

                    lastOffset = match.Distance;
                    pair = false;
                    srcPtr += match.Length;
                    continue;
                }
                byte by = source[srcPtr++];
                pair = true;

                // Check for SINGLE-BYTE matches
                int offset = -1;
                if (by == 0)
                    offset = 0;
                else
                {
                    int lookback = Math.Min(16, srcPtr);
                    for (int i = 1; i < lookback; i++)
                    {
                        if (source[srcPtr - 1 - i] == by)
                        {
                            offset = i;
                            break;
                        }
                    }
                }

                if (offset != -1)
                {
                    flag.WriteBit(true);
                    flag.WriteBit(true);
                    flag.WriteBit(true);
                    flag.WriteInt(offset, 4, true);
                    continue;
                }

                // literal: prefix 0 + byte
                flag.Buffer.WriteByte(by);
                flag.WriteBit(false);
            }
            // End marker: SHORTBLOCK with offset==0
            flag.WriteBit(true);
            flag.WriteBit(true);
            flag.Buffer.WriteByte(0);
            flag.WriteBit(false);
        }

        private static int LengthDelta(int distance)
        {
            if (distance < 0x80 || distance >= 0x7D00)
                return 2;
            if (distance >= 0x500)
                return 1;
            return 0;
        }

        private static int ReadGamma(FlagReader reader)
        {
            int value = 1;
            do
            {
                value <<= 1;
                value |= (reader.Readbit() ? 1 : 0);
            }
            while (reader.Readbit());
            return value;
        }

        private static void WriteGamma(FlagWriter writer, int value)
        {
            ThrowIf.LessThan(value, 1);
            int k = BitLength((uint)value);

            for (int i = k - 2; i >= 0; i--)
            {
                writer.WriteBit(((value >> i) & 1) != 0);
                writer.WriteBit(i > 0);
            }
        }

        private static int BitLength(uint value)
#if NET6_0_OR_GREATER
        => 32 - System.Numerics.BitOperations.LeadingZeroCount(value);
#else
        {
            if (value == 0) return 0;
            int k = 0;
            while (value > 0)
            {
                k++;
                value >>= 1;
            }
            return k;
        }
#endif
    }
}
