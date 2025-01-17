using AuroraLib.Core.Format;
using System.IO;

namespace AuroraLib.Compression.Interfaces
{
    /// <summary>
    /// Defines an interface for file decompression.
    /// </summary>
#if NET8_0_OR_GREATER
    public interface ICompressionDecoder : IStaticFormatRecognition
#else
    public interface ICompressionDecoder : IFormatRecognition
#endif
    {
        /// <summary>
        /// Decompresses data from the source <see cref="Stream"/> and writes the decompressed data to the destination <see cref="Stream"/>.
        /// </summary>
        /// <param name="source">The <see cref="Stream"/> containing compressed data to be decompressed.</param>
        /// <param name="destination">The <see cref="Stream"/> to write the decompressed data to.</param>
        void Decompress(Stream source, Stream destination);
    }
}
