using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// ZLB based on ZLib compression algorithm used in Star Fox Adventures.
    /// </summary>
    public sealed class ZLB : ICompressionAlgorithm, IHasIdentifier
    {
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new((byte)'Z', (byte)'L', (byte)'B', 0x0);

        private static readonly ZLib zLib = new();

        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x14 < stream.Length && stream.Match(_identifier);

        public void Decompress(Stream source, Stream destination)
        {
            long start = destination.Position;
            source.MatchThrow(_identifier);
            Header header = source.Read<Header>(Endian.Big);
            zLib.Decompress(source, destination, (int)header.CompressedSize);

            if (destination.Position - start != header.DecompressedSize)
            {
                throw new DecompressedSizeException(header.DecompressedSize, destination.Position - start);
            }
        }

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            long start = destination.Position;
            destination.Write(_identifier);
            destination.Write(1, Endian.Big);
            destination.Write(source.Length, Endian.Big);
            destination.Write(0, Endian.Big); // Placeholder
            zLib.Compress(source, destination, level);
            destination.At(start + 12, s => s.Write((uint)(destination.Length - start - 0x14), Endian.Big));
        }

        public readonly struct Header
        {
            public readonly uint Version; // 1
            public readonly uint DecompressedSize;
            public readonly uint CompressedSize;
        }
    }
}
