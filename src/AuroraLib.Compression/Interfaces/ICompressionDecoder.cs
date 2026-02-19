using AuroraLib.Compression.Exceptions;
using AuroraLib.Core.Exceptions;
using AuroraLib.Core.Format;
using System;
using System.IO;

namespace AuroraLib.Compression.Interfaces
{
    /// <summary>
    /// Defines an interface for file decompression.
    /// </summary>
    public interface ICompressionDecoder : IFormatInfoProvider
    {
        /// <summary>
        /// Decompresses data from the <paramref name="source"/> <see cref="Stream"/> and writes the decompressed data to the <paramref name="destination"/> <see cref="Stream"/>.
        /// </summary>
        /// <param name="source">The <see cref="Stream"/> containing compressed data to be decompressed.</param>
        /// <param name="destination">The <see cref="Stream"/> to write the decompressed data to.</param>
        /// <exception cref="EndOfStreamException">Thrown if the end of the <paramref name="source"/> stream is reached unexpectedly.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the <paramref name="source"/> or <paramref name="destination"/> is null.</exception>
        /// <exception cref="InvalidIdentifierException">Thrown if the stream does not contain valid compressed data.</exception>
        /// <exception cref="DecompressedSizeException">Thrown if the stream does not contain valid compressed data.</exception>
        /// <exception cref="NotSupportedException">Thrown if the <paramref name="destination"/> stream does not support writing.</exception>
        void Decompress(Stream source, Stream destination);
    }
}
