namespace AuroraLib.Compression.Interfaces
{
    /// <summary>
    /// Extends the <see cref="ICompressionEncoder"/> interface to provide functionality more settings for LZ-based compression algorithms, allowing for customization of compression behavior.
    /// </summary>
    public interface ILzSettings : ICompressionEncoder
    {
        /// <summary>
        /// Enables the algorithm to analyze repeating patterns more effectively, resulting in smaller compressed files.
        /// <para>Note: Activating this setting may cause issues if the specific implementation of a game does not support LookAhead optimization.</para>
        /// </summary>
        bool LookAhead { get; set; }
    }
}
