using AuroraLib.Core.Exceptions;
using System;
using System.IO;

namespace AuroraLib.Compression.Interfaces
{
    /// <summary>
    /// Extends the <see cref="ICompressionDecoder"/> interface to provide functionality for determining the size of decompressed data.
    /// </summary>
    public interface IProvidesDecompressedSize : ICompressionDecoder
    {
        /// <summary>
        /// Reads the provided stream to determine the size of the decompressed data.
        /// </summary>
        /// <param name="source">The stream containing the compressed data.</param>
        /// <returns>The size of the decompressed data, in bytes.</returns>
        /// <exception cref="EndOfStreamException"></exception>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="source"/> is null.</exception>
        /// <exception cref="InvalidIdentifierException">Thrown if the stream does not contain valid compressed data.</exception>
        uint GetDecompressedSize(Stream source);
    }
}
