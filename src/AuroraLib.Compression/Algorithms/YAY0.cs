using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.Buffers;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo Yay0 compression algorithm successor to the <see cref="MIO0"/> algorithm with increased match length, used in some Nintendo 64 and GameCube games.
    /// </summary>
    public sealed class Yay0 : ICompressionAlgorithm, ILzSettings, IHasIdentifier
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new("Yay0");

        internal static readonly LzProperties _lz = new(0x1000, 0xff + 0x12, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        public Endian EndianOrder = Endian.Big;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.Match(_identifier);

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            uint startPosition = (uint)source.Position;
            source.MatchThrow(_identifier);
            Endian endian = source.DetectByteOrder<uint>(3);
            uint uncompressedSize = source.ReadUInt32(endian);
            uint compressedDataPointer = source.ReadUInt32(endian) + startPosition;
            uint uncompressedDataPointer = source.ReadUInt32(endian) + startPosition;
            DecompressHeaderless(source, destination, uncompressedSize, (int)compressedDataPointer, (int)uncompressedDataPointer);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            using MemoryPoolStream compressedData = new(1024), uncompressedData = new(1024), flagData = new(512);
            CompressHeaderless(source, compressedData, uncompressedData, flagData, LookAhead, level);

            uint startPosition = (uint)destination.Position;
            destination.Write(_identifier);
            destination.Write(source.Length, EndianOrder);
            destination.Write((uint)(0x10 + flagData.Length - startPosition), EndianOrder);
            destination.Write((uint)(0x10 + flagData.Length + compressedData.Length - startPosition), EndianOrder);
            flagData.WriteTo(destination);
            compressedData.WriteTo(destination);
            uncompressedData.WriteTo(destination);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void DecompressHeaderless(Stream source, Stream destination, uint decomLength, int compressedDataPointer, int uncompressedDataPointer)
        {
            const int flagDataStart = 0x10;
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using LzWindows buffer = new(destination, _lz.WindowsSize);

            using SpanBuffer<byte> data = new((int)(source.Length - source.Position));
            Span<byte> sData = data;
            source.Read(sData);
            int flagDataPointer = 0;
            compressedDataPointer -= flagDataStart;
            uncompressedDataPointer -= flagDataStart;
            nint maskBitCounter = 0, currentMask = 0;

            while (destination.Position + buffer.Position < endPosition)
            {
                // If we're out of bits, get the next mask.
                if (maskBitCounter == 0)
                {
                    currentMask = sData[flagDataPointer++];
                    maskBitCounter = 8;
                }

                if ((currentMask & 0x80) == 0x80)
                {
                    buffer.WriteByte(sData[uncompressedDataPointer++]);
                }
                else
                {
                    byte b1 = sData[compressedDataPointer++];
                    byte b2 = sData[compressedDataPointer++];
                    // Calculate the match distance & length
                    int distance = (((byte)(b1 & 0x0F) << 8) | b2) + 0x1;
                    int length = b1 >> 4;

                    if (length == 0)
                        length = sData[uncompressedDataPointer++] + 0x12;
                    else
                        length += 2;

                    buffer.BackCopy(distance, length);
                }

                // Get the next bit in the mask.
                currentMask <<= 1;
                maskBitCounter--;
            }

            source.Position -= (sData.Length - Math.Max(compressedDataPointer, uncompressedDataPointer));
            if (destination.Position + buffer.Position > endPosition)
            {
                throw new DecompressedSizeException(decomLength, destination.Position + buffer.Position - (endPosition - decomLength));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream compressedData, Stream uncompressedData, Stream flagData, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0;
            using FlagWriter flag = new(flagData, Endian.Big);
            LzMatchFinder dictionary = new(_lz, lookAhead, level);

            while (sourcePointer < source.Length)
            {
                // Search for a match
                if (dictionary.TryToFindMatch(source, sourcePointer, out LzMatch match))
                {

                    // 2 byte match.Length 3-17
                    if (match.Length < 18)
                    {
                        compressedData.Write((ushort)((match.Distance - 0x1) | ((match.Length - 0x2) << 12)), Endian.Big);
                    }
                    else //3 byte match.Length 18-273
                    {
                        compressedData.Write((ushort)((match.Distance - 0x1) & 0xFFF), Endian.Big);
                        uncompressedData.Write((byte)(match.Length - 0x12));
                    }
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
