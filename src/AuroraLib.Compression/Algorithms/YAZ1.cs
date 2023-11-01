namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Identical to YAZ0 only with different identifier.
    /// </summary>
    public sealed class Yaz1 : Yaz0
    {
        public override IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new("Yaz1");
    }
}
