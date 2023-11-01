using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// AsuraZlb based on ZLib compression algorithm used in The Simpsons Game.
    /// </summary>
    public sealed class AsuraZlb : ICompressionAlgorithm, IHasIdentifier
    {
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier64 _identifier = new("AsuraZlb");

        private static readonly ZLib zLib = new();

        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x14 < stream.Length && stream.Match(_identifier);

        public void Decompress(Stream source, Stream destination)
        {
            long start = destination.Position;
            source.MatchThrow(_identifier);
            _ = source.ReadUInt32();
            uint compressedSize = source.ReadUInt32(Endian.Big);
            uint decompressedSize = source.ReadUInt32(Endian.Big);
            zLib.Decompress(source, destination, (int)compressedSize);

            if (destination.Position - start != decompressedSize)
            {
                throw new DecompressedSizeException(decompressedSize, destination.Position - start);
            }
        }

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            long start = destination.Position;
            destination.Write(_identifier);
            destination.Write(1);
            destination.Write(0, Endian.Big); // Placeholder
            destination.Write(source.Length, Endian.Big);
            zLib.Compress(source, destination, level);
            destination.At(start + 12, s => s.Write((uint)(destination.Length - start - 0x14), Endian.Big));
        }
    }
}
