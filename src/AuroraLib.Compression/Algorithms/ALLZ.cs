using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.Buffers;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Aqualead LZ compression algorithm, used in games that utilize the Aqualead framework.
    /// </summary>
    public sealed class ALLZ : ICompressionAlgorithm, IHasIdentifier
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new("ALLZ");

        internal static readonly LzProperties _lz = new(0x20000, 0x40000, 3); //A larger window is possible but will take a lot longer.

        public byte LzCopyBits = 0;
        public byte LzDistanceBits = 14;
        public byte LzLengthBits = 1;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Length > 0x10 && stream.Match(_identifier);

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            Span<byte> flags = stackalloc byte[4];
            source.Read(flags);
            uint decomLength = source.ReadUInt32();

            using SpanBuffer<byte> buffer = new(decomLength);
            DecompressHeaderless(source, buffer, flags);
            destination.Write(buffer);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier);
            Span<byte> flags = stackalloc byte[4] { 0, LzCopyBits, LzDistanceBits, LzLengthBits };
            destination.Write(flags);
            destination.Write(source.Length);
            CompressHeaderless(source, destination, flags, true, level);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void DecompressHeaderless(Stream source, Span<byte> destination, ReadOnlySpan<byte> flags)
        {
            int length, distance, destinationPointer = 0;
            FlagReader flag = new(source, Endian.Little);

            while (destinationPointer < destination.Length)
            {
                if (!flag.Readbit())
                {
                    length = ReadALFlag(flags[3]) + 1;
                    source.Read(destination.Slice(destinationPointer, length));
                    destinationPointer += length;
                }

                if (destinationPointer < destination.Length)
                {
                    distance = ReadALFlag(flags[2]) + 1;
                    length = ReadALFlag(flags[1]) + 3;

                    while (length-- > 0)
                    {
                        destination[destinationPointer] = destination[destinationPointer - distance];
                        destinationPointer++;
                    }
                }
            }
            return;

            int ReadALFlag(int startBits)
            {
                int bits = startBits;
                while (flag.Readbit())
                    bits++;
                int result = flag.ReadInt(bits);
                result += ((1 << (bits - startBits)) - 1) << startBits;
                return result;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, ReadOnlySpan<byte> flags, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0;
            LzMatch match = default;

            LzMatchFinder dictionary = new(_lz, lookAhead, level);
            using FlagWriter flag = new(destination, Endian.Little);

            while (sourcePointer < source.Length)
            {
                int uncompressedLength = 0;
                while (sourcePointer + uncompressedLength < source.Length && !dictionary.TryToFindMatch(source, sourcePointer + uncompressedLength, out match))
                    uncompressedLength++;

                if (uncompressedLength > 0)
                {
                    flag.WriteBit(false);
                    WriteALFlag(flags[3], uncompressedLength - 1);
                    flag.Buffer.Write(source.Slice(sourcePointer, uncompressedLength));
                    sourcePointer += uncompressedLength;
                    flag.FlushIfNecessary();
                }
                else
                {
                    flag.WriteBit(true);
                }

                WriteALFlag(flags[2], match.Distance - 1);
                WriteALFlag(flags[1], match.Length - 3);
                sourcePointer += match.Length;
            }
            return;

            void WriteALFlag(int startBits, int value)
            {
                int bitMask = 0, bits = startBits;

                while (bitMask + ((1 << bits) - 1) < value)
                {
                    bits++;
                    bitMask = ((1 << (bits - startBits)) - 1) << startBits;
                    flag.WriteBit(true);
                }
                flag.WriteBit(false);
                value -= bitMask;
                flag.WriteInt(value, bits);
            }
        }
    }
}
