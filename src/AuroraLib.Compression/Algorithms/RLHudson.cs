using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Run-Length Encoding algorithm used by Hudson Soft.
    /// </summary>
    public sealed class RLHudson : ICompressionAlgorithm
    {
        private const int Identifier = 0x5;
        private const int FlagMask = 0x80;
        private const int MaxLength = 0x7F;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.ReadInt32(Endian.Big) != 0 && stream.ReadInt32(Endian.Big) == Identifier;

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            uint uncompressedSize = source.ReadUInt32(Endian.Big);
            _ = source.ReadUInt32(Endian.Big);

            DecompressHeaderless(source, destination, (int)uncompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(source.Length, Endian.Big);
            destination.Write(Identifier, Endian.Big);

            CompressHeaderless(source, destination);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            Span<byte> bytes = stackalloc byte[MaxLength];

            while (destination.Position < endPosition)
            {
                int flag = source.ReadByte();
                int length = flag & MaxLength;
                Span<byte> section = bytes[..length];
                if (flag < FlagMask)
                {
                    section.Fill(source.ReadUInt8());
                }
                else
                {
                    if (source.Read(section) != length)
                        throw new EndOfStreamException();
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
            RleMatchFinder matchFinder = new(3, MaxLength);

            while (sourcePointer < source.Length)
            {
                if (matchFinder.TryToFindMatch(source, sourcePointer, out int duration))
                {
                    destination.WriteByte((byte)duration);
                    destination.Write(source[sourcePointer]);
                }
                else
                {
                    destination.WriteByte((byte)(duration | FlagMask));
                    destination.Write(source.Slice(sourcePointer, duration));
                }
                sourcePointer += duration;
            }
        }
    }
}
