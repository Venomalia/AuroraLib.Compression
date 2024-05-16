using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// LZHudson is an LZ based compression algorithm used in Mario Party 4.
    /// </summary>
    public sealed class LZHudson : ICompressionAlgorithm, ILzSettings
    {
        private static readonly LzProperties _lz = new(0x1000, 0xFF + 18, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && extension.Contains(".LZHudson", StringComparison.InvariantCultureIgnoreCase) && stream.ReadUInt32(Endian.Big) != 0;

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            uint uncompressedSize = source.ReadUInt32(Endian.Big);
            DecompressHeaderless(source, destination, (int)uncompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(source.Length, Endian.Big);
            CompressHeaderless(source, destination, LookAhead, level);
        }

        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using LzWindows buffer = new(destination, _lz.WindowsSize);

            uint flag = 0, flagbits = 0;

            while (destination.Position + buffer.Position < endPosition)
            {
                if (flagbits == 0)
                {
                    flag = source.ReadUInt32(Endian.Big);
                    flagbits = 32;
                }

                if ((flag & 0x80000000) != 0)
                {
                    buffer.WriteByte(source.ReadUInt8());
                }
                else
                {
                    byte b1 = source.ReadUInt8(); // LLLLDDDD
                    byte b2 = source.ReadUInt8(); // DDDDDDDD
                    int distance = (((b1 & 0x0F) << 8) | b2) + 1;
                    int length = ((b1 & 0xF0) >> 4) + 2;
                    if (length == 2)
                    {
                        length = source.ReadUInt8() + 18;
                    }
                    buffer.BackCopy(distance, length);
                }
                flag <<= 1;
                flagbits--;
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0;
            LzMatchFinder dictionary = new(_lz, lookAhead, level);
            using MemoryPoolStream buffer = new(128);
            uint flag = 0, flagbits = 0;

            while (sourcePointer < source.Length)
            {
                if (dictionary.TryToFindMatch(source, sourcePointer, out LzMatch match))
                {
                    int length = match.Length > 17 ? 0 : match.Length - 2;

                    buffer.WriteByte((byte)((length << 4) | ((match.Distance - 1) >> 8))); // LLLLDDDD
                    buffer.WriteByte((byte)(match.Distance - 1)); // DDDDDDDD
                    if (length == 0)
                    {
                        buffer.WriteByte((byte)(match.Length - 18));
                    }
                    sourcePointer += match.Length;
                }
                else
                {
                    buffer.WriteByte(source[sourcePointer++]);
                    flag |= (0x80000000 >> (int)flagbits);
                }

                flagbits++;
                if (flagbits == 32)
                {
                    destination.Write(flag, Endian.Big);
                    buffer.WriteTo(destination);
                    buffer.SetLength(0);
                    flag = flagbits = 0;
                }
            }
            if (flagbits != 0)
            {
                destination.Write(flag, Endian.Big);
                buffer.WriteTo(destination);
            }
        }
    }
}
