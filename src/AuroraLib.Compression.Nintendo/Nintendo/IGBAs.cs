using AuroraLib.Compression.Interfaces;

namespace AuroraLib.Compression.Formats.Nintendo
{
    /// <summary>
    /// Provides GBA VRAM compatibility mode for LZ compression.
    /// </summary>
    public interface IGbaRamMode : ICompressionAlgorithm
    {
        /// <summary>
        /// Enables GBA VRAM compatibility mode (minimum distance is set to 2).
        /// <para>
        /// If enabled, improves compatibility but may slightly reduce compression efficiency.
        /// </para>
        /// </summary>
        bool GbaVramCompatibilityMode { get; set; }
    }
}
