using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Buffers;

namespace AuroraLib.Compression
{
    public static class CompressionEX
    {
        #region Decompress

        /// <summary>
        /// Decompresses a byte array using the specified compression algorithm and returns the decompressed data as a new byte array.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for decompression.</param>
        /// <param name="source">The byte array containing compressed data to be decompressed.</param>
        /// <returns>A new byte array containing the decompressed data.</returns>
        public static byte[] Decompress(this ICompressionDecoder algorithm, byte[] source)
        {
            using MemoryPoolStream destination = new();
            using MemoryStream sourceStream = new(source);
            algorithm.Decompress(sourceStream, destination);
            return destination.ToArray();
        }

        /// <summary>
        /// Decompresses data from the input stream using the specified compression algorithm and returns the decompressed data as a MemoryPoolStream.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for decompression.</param>
        /// <param name="source">The input stream containing compressed data to be decompressed.</param>
        /// <returns>A MemoryPoolStream containing the decompressed data.</returns>
        public static MemoryPoolStream Decompress(this ICompressionDecoder algorithm, Stream source)
        {
            MemoryPoolStream destination = new();
            algorithm.Decompress(source, destination);
            destination.Position = 0;
            return destination;
        }

        /// <summary>
        /// Decompresses data from the source file using the specified compression algorithm and writes the decompressed data to the destination file.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for decompression.</param>
        /// <param name="sourceFile">The path to the source file containing compressed data to be decompressed.</param>
        /// <param name="destinationFile">The path to the destination file where the decompressed data will be written.</param>
        public static void Decompress(this ICompressionDecoder algorithm, string sourceFile, string destinationFile)
        {
            using FileStream source = new(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream destination = new(destinationFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            algorithm.Decompress(source, destination);
        }
        #endregion

        #region Compress

        /// <summary>
        /// Compresses data from the input stream using the specified compression algorithm and returns the compressed data as a MemoryPoolStream.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for compression.</param>
        /// <param name="source">The input stream containing data to be compressed.</param>
        /// <param name="level">The compression level to use (optional, default is CompressionLevel.Optimal).</param>
        /// <returns>A MemoryPoolStream containing the compressed data.</returns>
        public static MemoryPoolStream Compress(this ICompressionEncoder algorithm, Stream source, CompressionLevel level = CompressionLevel.Optimal)
        {
            MemoryPoolStream destination = new();
            algorithm.Compress(source, destination, level);
            destination.Position = 0;
            return destination;
        }

        /// <summary>
        /// Compresses data from the source stream using the specified compression algorithm and writes the compressed data to the destination stream.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for compression.</param>
        /// <param name="source">The input stream containing data to be compressed.</param>
        /// <param name="destination">The output stream where the compressed data will be written.</param>
        /// <param name="level">The compression level to use (optional, default is CompressionLevel.Optimal).</param>
        public static void Compress(this ICompressionEncoder algorithm, Stream source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            using SpanBuffer<byte> bytes = new((int)(source.Length - source.Position));
            source.Read(bytes);
            algorithm.Compress(bytes, destination, level);
        }

        /// <summary>
        /// Compresses data from the input ReadOnlySpan using the specified compression algorithm and returns the compressed data as a MemoryPoolStream.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for compression.</param>
        /// <param name="source">The input ReadOnlySpan containing data to be compressed.</param>
        /// <param name="level">The compression level to use (optional, default is CompressionLevel.Optimal).</param>
        /// <returns>A MemoryPoolStream containing the compressed data.</returns>
        public static MemoryPoolStream Compress(this ICompressionEncoder algorithm, ReadOnlySpan<byte> source, CompressionLevel level = CompressionLevel.Optimal)
        {
            MemoryPoolStream destination = new();
            algorithm.Compress(source, destination, level);
            destination.Position = 0;
            return destination;
        }

        /// <summary>
        /// Compresses data from the source file using the specified compression algorithm and writes the compressed data to the destination file.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for compression.</param>
        /// <param name="sourceFile">The path to the source file containing data to be compressed.</param>
        /// <param name="destinationFile">The path to the destination file where the compressed data will be written.</param>
        /// <param name="level">The compression level to use (optional, default is CompressionLevel.Optimal).</param>
        public static void Compress(this ICompressionEncoder algorithm, string sourceFile, string destinationFile, CompressionLevel level = CompressionLevel.Optimal)
        {
            using FileStream source = new(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream destination = new(destinationFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            algorithm.Compress(source, destination, level);
        }

        #endregion
    }
}