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
        /// Compresses data from the <paramref name="source"/> span and writes the compressed data to the <paramref name="destination"/> <see cref="Stream"/>.
        /// </summary>
        /// <param name="source">The ReadOnlySpan containing the data to be compressed.</param>
        /// <param name="destination">The Stream to write the compressed data to.</param>
        /// <param name="level">The CompressionLevel to use for compression (default is <see cref="CompressionLevel.Optimal"/>).</param>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="source"/> or <paramref name="destination"/> is null.</exception>
        /// <exception cref="NotSupportedException">Thrown if the <paramref name="destination"/> stream does not support writing.</exception>
        void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal);

    }
}
