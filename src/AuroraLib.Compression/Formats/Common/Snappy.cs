using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.IO;

namespace AuroraLib.Compression.Formats.Common
{
    /// <summary>
    /// Google's Snappy compression algorithm (previously known as Zippy), focused on decompression speed.
    /// </summary>
    public sealed class Snappy : ICompressionAlgorithm
    {
        private const int MaxChunkSize = 0x10000;
        private static readonly byte[] _streamIdentifierChunk = new byte[] { 0xff, 0x06, 0x00, 0x00, 0x73, 0x4e, 0x61, 0x50, 0x70, 0x59 }; // Chunk + sNaPpY
        private static readonly Identifier64 _identifier = new Identifier64(_streamIdentifierChunk.AsSpan(0, 8));

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<Snappy>("Snappy Frame", new MediaType(MIMEType.Application, "x-snappy-framed"), ".sz", _identifier);

        private static readonly LzProperties _lz = new LzProperties(0x8000, 63 + 1, 4);

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Peek(s => s.Position + 0x10 < s.Length && stream.Match(_streamIdentifierChunk));

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_streamIdentifierChunk);

            while (source.Position < source.Length)
            {
                int chunkType = source.ReadByte();
                int chunkLength = source.ReadUInt24LittleEndian();

                if (chunkType == 0) // Compressed
                {
                    _ = source.ReadUInt32LittleEndian();// Skip CRC
                    DecompressHeaderless(source, destination);
                }
                else if (chunkType == 1) // Uncompressed
                {
                    _ = source.ReadUInt32LittleEndian();// Skip CRC
                    new SubStream(source, chunkLength - 4).CopyTo(destination);
                }
                else
                {
                    if (chunkType >= 0x02 && chunkType <= 0x7F)
                        throw new InvalidDataException($"Encountered reserved unskippable chunk type 0x{chunkType:X2}");

                    source.Seek(chunkLength, SeekOrigin.Current);
                    continue;
                }
            }

        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionSettings settings = default)
        {
            // Stream identifier Chunk
            destination.Write(_streamIdentifierChunk);

            using LzChainMatchFinder matchFinder = new LzChainMatchFinder(_lz, settings);
            using MemoryPoolStream buffer = new MemoryPoolStream(0x1000);
            int pos = 0;
            while (pos < source.Length)
            {
                buffer.SetLength(0);
                int chunkSize = Math.Min(MaxChunkSize, source.Length - pos);
                ReadOnlySpan<byte> chunk = source.Slice(pos, chunkSize);

                CompressHeaderless(chunk, buffer, settings, matchFinder);
                matchFinder.Reset();
                uint crc = CRCMask(Crc32C.Compute(chunk));

                if (buffer.Length >= chunkSize)
                {
                    // Uncompressed
                    destination.WriteByte(01);
                    destination.Write((UInt24)(chunkSize + 4));
                    destination.Write(crc);
                    destination.Write(chunk);
                }
                else
                {
                    // Compressed
                    destination.WriteByte(00);
                    destination.Write((UInt24)(buffer.Length + 4));
                    destination.Write(crc);
                    buffer.WriteTo(destination);
                }
                pos += chunkSize; // next
            }
        }

        private static uint ReadDecompressedSize(Stream source)
        {
            uint result = 0;
            int shift = 0;
            int b = -1;

            while ((b & 0x80) != 0)
            {
                b = source.ReadUInt8();
                result |= (uint)(b & 0x7F) << shift;
                shift += 7;
            }
            return result;
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, CompressionSettings settings = default)
        {
            using LzChainMatchFinder matchFinder = new LzChainMatchFinder(_lz, settings);
            CompressHeaderless(source, destination, settings, matchFinder);
        }

        private static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, CompressionSettings settings, LzChainMatchFinder matchFinder)
        {
            // Write DecompressedSize
            int v = source.Length;
            while (v >= 0x80)
            {
                destination.WriteByte((byte)(v | 0x80));
                v >>= 7;
            }
            destination.WriteByte((byte)v);

            int sourcePointer = 0x0;
            while (true)
            {
                LzMatch match = matchFinder.FindNextBestMatch(source);
                int plain = match.Offset - sourcePointer;

                // ---- Literal ----
                if (plain > 0)
                {
                    if (plain <= 60)
                    {
                        destination.WriteByte((byte)((plain - 1) << 2));
                    }
                    else
                    {
                        int len = plain - 1;
                        if (len <= 0xFF)
                        {
                            destination.WriteByte((byte)(60 << 2));
                            destination.WriteByte((byte)len);
                        }
                        else if (len <= 0xFFFF)
                        {
                            destination.WriteByte((byte)(61 << 2));
                            destination.Write((ushort)len);
                        }
                        else if (len <= 0xFFFFFF)
                        {
                            destination.WriteByte((byte)(62 << 2));
                            destination.Write((UInt24)len);
                        }
                        else
                        {
                            destination.WriteByte((byte)(63 << 2));
                            destination.Write((uint)len);
                        }
                    }

                    destination.Write(source.Slice(sourcePointer, plain));
                    sourcePointer += plain;
                }

                // Last match reached.
                if (match.Length == 0)
                    return;

                sourcePointer += match.Length;
                if (match.Distance < 2048 && match.Length >= 4 && match.Length <= 11)
                {
                    byte tag = (byte)(1 | ((match.Length - 4) << 2) | ((match.Distance >> 8) << 5));

                    destination.WriteByte(tag);
                    destination.WriteByte((byte)match.Distance);
                }
                else
                {
                    byte tag = (byte)(2 | ((match.Length - 1) << 2));

                    destination.WriteByte(tag);
                    destination.Write((ushort)match.Distance);
                }
            }
        }

        public static void DecompressHeaderless(Stream source, Stream destination)
        {
            int distance, length;

            uint decompressedSize = ReadDecompressedSize(source);

            long endPosition = destination.Position + decompressedSize;
            destination.SetLength(endPosition);
            using LzWindows buffer = new LzWindows(destination, (byte)(_lz.WindowsBits + 1));

            while (destination.Position + buffer.Position < endPosition)
            {
                int tag = source.ReadByte();

                int type = tag & 0x3;
                length = tag >> 2;
                if (type == 0)
                {
                    // Literal
                    if (length >= 60)
                    {
                        int lenBytes = length - 59;
                        length = 0; // 1-4 byte

                        for (int i = 0; i < lenBytes; i++)
                            length |= source.ReadByte() << (8 * i);
                    }
                    buffer.CopyFrom(source, length + 1);
                    continue;
                }
                else if (type == 1) // Copy with 1-byte offset
                {
                    length = (length & 0x7) + 3;
                    distance = ((tag >> 5) << 8) | source.ReadByte();
                }
                else if (type == 2) // Copy with 2-byte offset
                {
                    distance = source.ReadUInt16LittleEndian();
                }
                else // Copy with 4-byte offset
                {
                    distance = source.ReadInt32LittleEndian();
                }
                buffer.BackCopy(distance, length + 1);
            }
        }

        public static uint CRCMask(uint crc) => ((crc >> 15) | (crc << 17)) + 0xa282ead8;
    }
}
