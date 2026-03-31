using System;

namespace AuroraLib.Compression
{
    /// <summary>
    /// Defines compression modes that control encoder behavior and compatibility.
    /// </summary>
    [Flags]
    public enum CompresionStrategy : byte
    {
        /// <summary>
        /// Automatically selects optimal settings for best compression ratio and performance.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Enables compatibility features for older or simpler decoders.
        /// <para>
        /// Note: Enabling this mode can reduce compression ratio.
        /// </para>
        /// </summary>
        CompatibilityMode = 1 << 0,
    }
}
