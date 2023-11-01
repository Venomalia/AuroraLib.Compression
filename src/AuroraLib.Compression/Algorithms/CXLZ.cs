namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// CXLZ extension header based on LZ10 algorithm used in Puyo Puyo.
    /// </summary>
    public sealed class CXLZ : LZ10, IHasIdentifier
    {
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new("CXLZ");

        public override bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && stream.Match(_identifier) && base.IsMatch(stream, extension);

        public override void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier);
            base.Compress(source, destination, level);
        }

        public override void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            base.Decompress(source, destination);
        }

    }
}
