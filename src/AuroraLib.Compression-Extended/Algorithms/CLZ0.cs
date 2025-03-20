using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// CLZ0 compression algorithm, used in Games from Victor Interactive Software.
    /// </summary>
    public sealed class CLZ0 : ICompressionAlgorithm, ILzSettings, IHasIdentifier, IProvidesDecompressedSize
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32((byte)'C', (byte)'L', (byte)'Z', 0);

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<CLZ0>("Victor Interactive CLZ0", new MediaType(MIMEType.Application, "x-clz0"), string.Empty, _identifier);

        internal static readonly LzProperties _lz = new LzProperties(0x1000, 18, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.Match(_identifier));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                s.Position += 8;
                return s.ReadUInt32(Endian.Big);
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            // Read Header
            source.MatchThrow(_identifier);
            _ = source.ReadUInt32(Endian.Big);
            _ = source.ReadUInt32(Endian.Big);
            uint decompressedSize = source.ReadUInt32(Endian.Big);

            // Perform the decompression
            DecompressHeaderless(source, destination, (int)decompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Write Header
            destination.Write(_identifier);
            destination.Write(source.Length, Endian.Big);
            destination.Write(0, Endian.Big);
            destination.Write(source.Length, Endian.Big);

            // Perform the compression
            CompressHeaderless(source, destination, LookAhead, level);
        }

        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            FlagReader flag = new FlagReader(source, Endian.Little);
            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {
                while (destination.Position + buffer.Position < endPosition)
                {
                    if (flag.Readbit())
                    {
                        int distance = source.ReadByte();
                        int length = source.ReadByte();

                        distance |= length >> 4 << 8;
                        distance = 0x1000 - distance; // window delta to distance
                        length = (length & 0x0f) + 3;

                        buffer.BackCopy(distance, length);
                    }
                    else
                    {
                        buffer.WriteByte(source.ReadUInt8());
                    }
                }
            }

            // Verify decompressed size
            if (destination.Position != endPosition)
            {
                throw new DecompressedSizeException(decomLength, destination.Position - (endPosition - decomLength));
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0, matchPointer = 0x0;
            using PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level);
            using FlagWriter flag = new FlagWriter(destination, Endian.Little);
            while (sourcePointer < source.Length)
            {
                if (matchPointer < matches.Count && matches[matchPointer].Offset == sourcePointer)
                {
                    LzMatch match = matches[matchPointer++];

                    int delta = 0x1000 - match.Distance;
                    flag.Buffer.WriteByte((byte)delta);
                    flag.Buffer.WriteByte((byte)((match.Length - 3) | (delta >> 8 << 4)));
                    sourcePointer += match.Length;
                    flag.WriteBit(true);

                }
                else
                {
                    flag.Buffer.WriteByte(source[sourcePointer++]);
                    flag.WriteBit(false);
                }
            }
        }
    }
}
