using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo MIO0 compression algorithm
    /// </summary>
    public sealed class MIO0 : ICompressionAlgorithm, ILzSettings, IHasIdentifier
    {
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new("MIO0");

        internal static readonly LzProperties _lz = new(0x1000, 18, 3);

        public bool LookAhead { get; set; } = true;

        public Endian EndianOrder = Endian.Big;

        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.Match(_identifier);

        public void Decompress(Stream source, Stream destination)
        {
            uint startPosition = (uint)source.Position;
            source.MatchThrow(_identifier);
            Endian endian = source.DetectByteOrder<uint>(3);
            uint uncompressedSize = source.ReadUInt32(endian);
            uint compressedDataPointer = source.ReadUInt32(endian) + startPosition;
            uint uncompressedDataPointer = source.ReadUInt32(endian) + startPosition;
            DecompressHeaderless(source, destination, uncompressedSize, compressedDataPointer, uncompressedDataPointer);
        }

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
        public static void DecompressHeaderless(Stream source, Stream destination, uint decomLength, uint compressedDataPointer, uint uncompressedDataPointer)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using LzWindows buffer = new(destination, _lz.WindowsSize);

            uint flagDataPointer = (uint)source.Position;
            uint maskBitCounter = 0, currentMask = 0;

            while (destination.Position + buffer.Position < endPosition)
            {
                // If we're out of bits, get the next mask.
                if (maskBitCounter == 0)
                {
                    source.Position = flagDataPointer;
                    currentMask = source.ReadUInt8();
                    maskBitCounter = 8;
                    flagDataPointer += 1;
                }

                if ((currentMask & 0x80) == 0x80)
                {
                    source.Position = uncompressedDataPointer++;
                    buffer.WriteByte(source.ReadUInt8());
                }
                else
                {
                    source.Position = compressedDataPointer;
                    ushort link = source.ReadUInt16(Endian.Big);
                    compressedDataPointer += 2;

                    // Calculate the match distance & length
                    int distance = (link & 0xfff) + 1;
                    int length = (link >> 12) + 3;

                    buffer.BackCopy(distance, length);
                }

                // Get the next bit in the mask.
                currentMask <<= 1;
                maskBitCounter--;
            }

            source.Position = uncompressedDataPointer;
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
