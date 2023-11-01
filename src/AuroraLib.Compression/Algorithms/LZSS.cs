using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Lempel–Ziv–Storer–Szymanski algorithm, a derivative of LZ77 from Haruhiko Okumura.
    /// </summary>
    public class LZSS : ICompressionAlgorithm, ILzSettings, IHasIdentifier
    {

        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new("LZSS");

        public readonly LzProperties LZ;

        public bool LookAhead { get; set; } = true;

        public LZSS() : this(new((byte)12, 4, 2))
        { }

        public LZSS(LzProperties lz)
            => LZ = lz;

        public virtual bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.Match(_identifier);

        public virtual void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            uint destinationSize = source.ReadUInt32(Endian.Big);
            uint compressedSize = source.ReadUInt32(Endian.Big);
            uint unk = source.ReadUInt32(Endian.Big);

            DecompressHeaderless(source, destination, (int)destinationSize, LZ);
        }

        public virtual void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            long destinationStartPosition = destination.Position;
            destination.Write(_identifier);
            destination.Write(source.Length, Endian.Big);
            destination.Write(0); // Compressed length (will be filled in later)
            destination.Write(0);

            CompressHeaderless(source, destination, LZ, LookAhead, level);

            // Go back to the beginning of the file and write out the compressed length
            int destinationLength = (int)(destination.Position - destinationStartPosition - 0x10);
            destination.At(destinationStartPosition + 8, x => x.Write(destinationLength, Endian.Big));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength, LzProperties lz)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            FlagReader flag = new(source, Endian.Little);
            using LzWindows buffer = new(destination, lz.WindowsSize);

            int f = lz.GetLengthBitsFlag();

            while (destination.Position + buffer.Position < endPosition)
            {
                if (flag.Readbit())
                {
                    buffer.WriteByte(source.ReadUInt8());
                }
                else
                {
                    byte b1 = source.ReadUInt8();
                    byte b2 = source.ReadUInt8();

                    int offset = (b2 >> lz.LengthBits << 8) | b1;
                    int length = (b2 & f) + lz.MinLength;
                    offset = lz.WindowsSize + offset - lz.WindowsStart;

                    buffer.OffsetCopy(offset, length);
                }
            }

            if (destination.Position + buffer.Position > endPosition)
            {
                throw new DecompressedSizeException(decomLength, destination.Position + buffer.Position - (endPosition - decomLength));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, LzProperties lz, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0;
            LzMatchFinder dictionary = new(lz, lookAhead, level);
            using FlagWriter flag = new(destination, Endian.Little);

            int n = lz.GetWindowsFlag();
            int f = lz.GetLengthBitsFlag();

            while (sourcePointer < source.Length)
            {
                if (dictionary.TryToFindMatch(source, sourcePointer, out LzMatch lzMatch))
                {
                    // Distance to offset
                    int offset = ((lz.WindowsStart + sourcePointer - lzMatch.Distance) & n);
                    flag.Buffer.Write((ushort)((offset & 0xFF) | (offset & 0xFF00) << lz.LengthBits | ((lzMatch.Length - lz.MinLength) & f) << 8));
                    flag.WriteBit(false);
                    sourcePointer += lzMatch.Length;
                }
                else
                {
                    flag.Buffer.WriteByte(source[sourcePointer++]);
                    flag.WriteBit(true);
                }
            }
        }
    }
}
