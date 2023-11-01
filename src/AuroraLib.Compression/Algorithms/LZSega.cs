using AuroraLib.Compression.Interfaces;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// This LZSS header was used by Sega in early GameCube games like F-zero GX or Super Monkey Ball.
    /// </summary>
    public sealed class LZSega : ICompressionAlgorithm, ILzSettings
    {
        public bool LookAhead { get; set; } = true;

        private static readonly LzProperties _lz = new((byte)12, 4, 2);

        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
        {
            if (stream.Length < 0x12)
                return false;

            uint compressedSize = stream.ReadUInt32();
            uint decompressedSize = stream.ReadUInt32();
            return (compressedSize == stream.Length - 8 || compressedSize == stream.Length) && decompressedSize != compressedSize && decompressedSize >= 0x20;
        }

        public void Decompress(Stream source, Stream destination)
        {
            uint compressedSize = source.ReadUInt32();
            uint decompressedSize = source.ReadUInt32();
            LZSS.DecompressHeaderless(source, destination, (int)decompressedSize, _lz);
        }

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            long destinationStartPosition = destination.Position;
            destination.Write(0); // Compressed length (will be filled in later)
            destination.Write(source.Length);
            LZSS.CompressHeaderless(source, destination, _lz, LookAhead, level);

            // Go back to the beginning of the file and write out the compressed length
            int destinationLength = (int)(destination.Position - destinationStartPosition - 0x8);
            destination.At(destinationStartPosition, x => x.Write(destinationLength));
        }
    }
}
