using AuroraLib.Compression.Interfaces;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// AKLZ implementation based on LZSS algorithm used in Skies of Arcadia Legends.
    /// </summary>
    public sealed class AKLZ : ICompressionAlgorithm, ILzSettings, IHasIdentifier
    {
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier _identifier = new("AKLZ~?Qd=ÌÌÍ");

        public bool LookAhead { get; set; } = true;

        private static readonly LzProperties _lz = new((byte)12, (byte)4, 2);

        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.Match(_identifier);

        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            uint decompressedSize = source.ReadUInt32(Endian.Big);
            LZSS.DecompressHeaderless(source, destination, (int)decompressedSize, _lz);
        }

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier.Bytes);
            destination.Write(source.Length, Endian.Big);
            LZSS.CompressHeaderless(source, destination, _lz, LookAhead, level);
        }
    }
}
