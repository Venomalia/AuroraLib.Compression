using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// CLZ0 compression algorithm, used in Games from Victor Interactive Software.
    /// </summary>
    public sealed class CLZ0 : ICompressionAlgorithm, ILzSettings, IHasIdentifier
    {
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new((byte)'C', (byte)'L', (byte)'Z', 0);

        internal static readonly LzProperties _lz = new(12, 4, 3);

        public bool LookAhead { get; set; } = true;

        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.Match(_identifier);

        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            _ = source.ReadUInt32(Endian.Big);
            _ = source.ReadUInt32(Endian.Big);
            uint decompressedSize = source.ReadUInt32(Endian.Big);
            DecompressHeaderless(source, destination, (int)decompressedSize);
        }

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier);
            destination.Write(source.Length, Endian.Big);
            destination.Write(0, Endian.Big);
            destination.Write(source.Length, Endian.Big);
            CompressHeaderless(source, destination, LookAhead, level);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            FlagReader flag = new(source, Endian.Little);
            using LzWindows buffer = new(destination, _lz.WindowsSize);

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

            if (destination.Position + buffer.Position > endPosition)
            {
                throw new DecompressedSizeException(decomLength, destination.Position + buffer.Position - (endPosition - decomLength));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0;
            LzMatchFinder dictionary = new(_lz, lookAhead, level);
            using FlagWriter flag = new(destination, Endian.Little);

            while (sourcePointer < source.Length)
            {
                // Search for a match
                if (dictionary.TryToFindMatch(source, sourcePointer, out LzMatch match))
                {
                    int delta = 0x1000 - match.Distance;
                    flag.Buffer.Write((byte)delta);
                    flag.Buffer.Write((byte)((match.Length - 3) | (delta >> 8 << 4)));
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
