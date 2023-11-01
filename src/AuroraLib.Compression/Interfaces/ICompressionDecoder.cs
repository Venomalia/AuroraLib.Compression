namespace AuroraLib.Compression.Interfaces
{
    /// <summary>
    /// Defines an interface for file decompression.
    /// </summary>
    public interface ICompressionDecoder : IFormatRecognition
    {
        /// <summary>
        /// Decompresses data from the source stream and writes the decompressed data to the destination stream.
        /// </summary>
        /// <param name="source">The source stream containing compressed data to be decompressed.</param>
        /// <param name="destination">The destination stream where the decompressed data will be written.</param>
        void Decompress(Stream source, Stream destination);
    }
}
