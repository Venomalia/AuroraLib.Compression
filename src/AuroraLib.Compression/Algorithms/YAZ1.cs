namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Identical to <see cref="Yaz0"/> only with different identifier, in N64DD games the 0 was replaced by 1 if the files were on the disk instead of the cartridge.
    /// </summary>
    public sealed class Yaz1 : Yaz0
    {
        /// <inheritdoc/>
        public override IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new("Yaz1");
    }
}
