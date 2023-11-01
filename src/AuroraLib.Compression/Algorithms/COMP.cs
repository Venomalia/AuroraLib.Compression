namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// COMP extension header based on LZ11 algorithm used in Puyo Puyo Chronicle.
    /// </summary>
    public sealed class COMP : LZ11, IHasIdentifier
    {
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new("COMP");

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
