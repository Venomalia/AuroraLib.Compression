using AuroraLib.Core;

namespace AuroraLib.Compression.Interfaces
{
    /// <summary>
    /// Represents a file format whose endianness (byte order) depends on the target architecture.
    /// </summary>
    public interface IEndianDependentFormat
    {
        /// <summary>
        /// Gets or sets the byte order (endianness) for this format, depending on the target architecture.
        /// </summary>
        Endian FormatByteOrder { get; set; }
    }
}
