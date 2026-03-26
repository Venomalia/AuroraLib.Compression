using System;
using System.IO;

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
        /// <param name="settings">Compression settings controlling encoder behavior such as the Compression quality (default is <see cref="CompressionSettings.Balanced"/>).</param>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="source"/> or <paramref name="destination"/> is null.</exception>
        /// <exception cref="NotSupportedException">Thrown if the <paramref name="destination"/> stream does not support writing.</exception>
        void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionSettings settings = default);

    }
}
