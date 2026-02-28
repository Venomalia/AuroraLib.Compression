namespace AuroraLib.Compression.Interfaces
{
    /// <summary>
    /// Extends the <see cref="ICompressionEncoder"/> interface with additional settings for LZ-based compression algorithms, allowing customization of compression behavior.
    /// </summary>
    public interface ILzSettings : ICompressionEncoder
    {
        /// <summary>
        /// Enables look-ahead matching, allowing the encoder to find longer and more optimal repeating patterns.
        /// <para>
        /// If enabled, this will improve the compression ratio, but may reduce compatibility with decoders that do not support look-ahead optimization.
        /// </para>
        /// </summary>
        bool LookAhead { get; set; }
    }
}
