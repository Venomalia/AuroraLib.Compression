using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Interfaces
{
    /// <summary>
    /// Defines an interface for file compression.
    /// </summary>
    public interface ICompressionEncoder
    {
        /// <summary>
        /// Compresses the specified data with the specified compression level and writes the compressed result to a data stream.
        /// </summary>
        /// <param name="source">The data to be compressed as a read-only span of bytes.</param>
        /// <param name="destination">The data stream where the compressed data should be written.</param>
        /// <param name="level">The compression level to be applied (optional, defaults to CompressionLevel.Optimal).</param>
        void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal);
    }
}
