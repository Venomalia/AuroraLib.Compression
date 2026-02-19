using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo MIO0 compression algorithm, mainly used in early Nintendo 64 games.
    /// </summary>
    public sealed class MIO0 : ICompressionAlgorithm, ILzSettings, IEndianDependentFormat, IProvidesDecompressedSize
    {

        private static readonly Identifier32 _identifier = new Identifier32("MIO0".AsSpan());

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<MIO0>("Nintendo MIO0", new MediaType(MIMEType.Application, "x-nintendo-mio0"), string.Empty, _identifier);

        internal static readonly LzProperties _lz = new LzProperties(0x1000, 18, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = false;

        /// <inheritdoc/>
        public Endian FormatByteOrder { get; set; } = Endian.Big;

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
                Endian endian = s.DetectByteOrder<uint>(3);
                return s.ReadUInt32(endian);
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            const int flagDataStart = 0x10;
            uint startPosition = (uint)source.Position;
            source.MatchThrow(_identifier);
            Endian endian = source.DetectByteOrder<uint>(3);
            uint uncompressedSize = source.ReadUInt32(endian);
            uint compressedDataPointer = source.ReadUInt32(endian) + startPosition;
            uint uncompressedDataPointer = source.ReadUInt32(endian) + startPosition;
            DecompressHeaderless(source, destination, uncompressedSize, (int)compressedDataPointer - flagDataStart, (int)uncompressedDataPointer - flagDataStart);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            using MemoryPoolStream compressedData = new MemoryPoolStream(1024);
            using MemoryPoolStream uncompressedData = new MemoryPoolStream(1024);
            using MemoryPoolStream flagData = new MemoryPoolStream(512);

            CompressHeaderless(source, compressedData, uncompressedData, flagData, LookAhead, level);

            uint startPosition = (uint)destination.Position;
            destination.Write(_identifier);
            destination.Write(source.Length, FormatByteOrder);
            destination.Write((uint)(0x10 + flagData.Length - startPosition), FormatByteOrder);
            destination.Write((uint)(0x10 + flagData.Length + compressedData.Length - startPosition), FormatByteOrder);
            flagData.WriteTo(destination);
            compressedData.WriteTo(destination);
            uncompressedData.WriteTo(destination);
        }

        public static void DecompressHeaderless(Stream source, Stream destination, uint decomLength, int compressedDataPointer, int uncompressedDataPointer)
        {

            int bufferLength = (int)(source.Length - source.Position);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferLength);
            try
            {
                source.ReadExactly(buffer, 0, bufferLength);

                int read = DecompressHeaderless(buffer.AsSpan(0, bufferLength), destination, decomLength, compressedDataPointer, uncompressedDataPointer);
                if (source.CanSeek)
                    source.Position -= bufferLength - read;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

        }

#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static int DecompressHeaderless(ReadOnlySpan<byte> source, Stream destination, uint decomLength, int compressedDataPointer, int uncompressedDataPointer)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using LzWindows buffer = new LzWindows(destination, _lz.WindowsSize);

            int flagDataPointer = 0;
            int maskBitCounter = 0, currentMask = 0;

            while (destination.Position + buffer.Position < endPosition)
            {
                // If we're out of bits, get the next mask.
                if (maskBitCounter == 0)
                {
                    currentMask = source[flagDataPointer++];
                    maskBitCounter = 8;
                }

                if ((currentMask & 0x80) == 0x80)
                {
                    buffer.WriteByte(source[uncompressedDataPointer++]);
                }
                else
                {
                    byte b1 = source[compressedDataPointer++];
                    byte b2 = source[compressedDataPointer++];

                    // Calculate the match distance & length
                    int distance = (((byte)(b1 & 0x0F) << 8) | b2) + 0x1;
                    int length = (b1 >> 4) + 3;

                    buffer.BackCopy(distance, length);
                }

                // Get the next bit in the mask.
                currentMask <<= 1;
                maskBitCounter--;
            }

            if (destination.Position + buffer.Position > endPosition)
            {
                throw new DecompressedSizeException(decomLength, destination.Position + buffer.Position - (endPosition - decomLength));
            }
            return Math.Max(compressedDataPointer, uncompressedDataPointer);
        }


        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream compressedData, Stream uncompressedData, Stream flagData, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            using PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level);
            using FlagWriter flag = new FlagWriter(flagData, Endian.Big);
            CompressHeaderless(source, compressedData, uncompressedData, flag, matches);
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream compressedData, Stream uncompressedData, FlagWriter flag, IReadOnlyList<LzMatch> matches)
        {
            int sourcePointer = 0x0, matchPointer = 0x0;
            while (sourcePointer < source.Length)
            {
                if (matchPointer < matches.Count && matches[matchPointer].Offset == sourcePointer)
                {
                    LzMatch match = matches[matchPointer++];

                    // 2 byte match.Length 3-18
                    compressedData.Write((ushort)((match.Distance - 0x1) | ((match.Length - 0x3) << 12)), Endian.Big);
                    sourcePointer += match.Length;
                    flag.WriteBit(false);

                }
                else
                {
                    uncompressedData.Write(source[sourcePointer++]);
                    flag.WriteBit(true);
                }
            }
        }
    }
}
