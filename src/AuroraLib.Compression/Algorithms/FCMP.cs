using AuroraLib.Compression.Interfaces;

namespace AuroraLib.Compression.Algorithms
{

    /// <summary>
    /// FCMP extension header based on LZSS algorithm used in Muramasa The Demon Blade.
    /// </summary>
    public sealed class FCMP : ICompressionAlgorithm, ILzSettings, IHasIdentifier
    {
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new("FCMP");

        public bool LookAhead { get; set; } = true;

        private static readonly LzProperties _lz = new(0x1000, 0xF + 3, 3, 0xFEE);

        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.Match(_identifier);

        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            int destinationLength = source.ReadInt32();
            int unk = source.ReadInt32(); //always 305397760?
            LZSS.DecompressHeaderless(source, destination, (int)destinationLength, _lz);
        }

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Write out the header
            destination.Write(_identifier);
            destination.Write(source.Length); // Decompressed length
            destination.Write(305397760);
            LZSS.CompressHeaderless(source, destination, _lz, LookAhead, level);
        }
    }
}
