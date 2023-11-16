using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Run-Length Encoding compression algorithm was used in nintendo games.
    /// </summary>
    public sealed class RLE30 : ICompressionAlgorithm
    {
        private const byte Identifier = 0x30;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.ReadByte() == Identifier && (stream.ReadUInt24() != 0 || stream.ReadUInt32() != 0) && stream.ReadUInt8() != 0;

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.Position += 1;
            uint uncompressedSize = source.ReadUInt24();
            if (uncompressedSize == 0)
            {
                uncompressedSize = source.ReadUInt32();
            }
            DecompressHeaderless(source, destination, (int)uncompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(Identifier);
            if (source.Length <= 0xFFFFFF)
            {
                destination.Write((UInt24)source.Length);
            }
            else
            {
                destination.Write(new UInt24(0));
                destination.Write(source.Length);
            }

            CompressHeaderless(source, destination);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            Span<byte> bytes = stackalloc byte[0x7F];

            while (destination.Position < endPosition)
            {
                byte b1 = source.ReadUInt8();
                int length = b1 & 0x7F;
                Span<byte> section = bytes[..length];
                if (b1 >> 7 == 1)
                {
                    if (source.Read(section) != length)
                        throw new EndOfStreamException();
                }
                else
                {
                    section.Fill(source.ReadUInt8());
                }
                destination.Write(section);
            }

            if (destination.Position > endPosition)
            {
                throw new DecompressedSizeException(decomLength, destination.Position - (endPosition - decomLength));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination)
        {
            int sourcePointer = 0x0;
            RleMatchFinder matchFinder = new(3, 0x7F);

            while (sourcePointer < source.Length)
            {
                if (matchFinder.TryToFindMatch(source, sourcePointer, out int duration))
                {
                    destination.WriteByte((byte)duration);
                    destination.Write(source[sourcePointer]);
                }
                else
                {
                    destination.WriteByte((byte)(duration | 0x80));
                    destination.Write(source.Slice(sourcePointer, duration));
                }
                sourcePointer += duration;
            }
        }
    }
}
