using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Buffers;
using AuroraLib.Core.Extensions;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// A extension class that provides extension methods for compression and decompression.
    /// </summary>
    public static class CompressionExtension
    {
        #region Decompress

        /// <summary>
        /// Decompresses data using the specified compression algorithm and writes the result to a <see cref="Stream"/>.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for decompression.</param>
        /// <param name="source">The ReadOnlySpan containing compressed data to be decompressed.</param>
        /// <param name="destination">The <see cref="Stream"/> to write the decompressed data to.</param>
        public static void Decompress(this ICompressionDecoder algorithm, ReadOnlySpan<byte> source, Stream destination)
            => algorithm.Decompress(source.AsReadOnlyStream(), destination);

        /// <summary>
        /// Decompresses data using the specified compression algorithm and returns the decompressed data as a new <see cref="byte"/> array.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for decompression.</param>
        /// <param name="source">The ReadOnlySpan containing compressed data to be decompressed.</param>
        /// <returns>A new <see cref="byte"/> array containing the decompressed data.</returns>
        public static byte[] Decompress(this ICompressionDecoder algorithm, ReadOnlySpan<byte> source)
        {
            using (MemoryPoolStream destination = new MemoryPoolStream())
            {
                algorithm.Decompress(source, destination);
                return destination.ToArray();
            }
        }

        /// <summary>
        /// Decompresses data from the input <see cref="Stream"/> using the specified compression algorithm and returns the decompressed data as a <see cref="MemoryPoolStream"/>.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for decompression.</param>
        /// <param name="source">The input <see cref="Stream"/> containing compressed data to be decompressed.</param>
        /// <returns>A <see cref="MemoryPoolStream"/> containing the decompressed data.</returns>
        public static MemoryPoolStream Decompress(this ICompressionDecoder algorithm, Stream source)
        {
            MemoryPoolStream destination = new MemoryPoolStream();
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
            using (FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                algorithm.Decompress(source, destination);
        }
        #endregion

        #region Compress

        /// <summary>
        /// Compresses data from the input <see cref="Stream"/> using the specified compression algorithm and returns the compressed data as a <see cref="MemoryPoolStream"/>.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for compression.</param>
        /// <param name="source">The input <see cref="Stream"/> containing data to be compressed.</param>
        /// <param name="level">The compression level to use (optional, default is CompressionLevel.Optimal).</param>
        /// <returns>A <see cref="MemoryPoolStream"/> containing the compressed data.</returns>
        public static MemoryPoolStream Compress(this ICompressionEncoder algorithm, Stream source, CompressionLevel level = CompressionLevel.Optimal)
        {
            MemoryPoolStream destination = new MemoryPoolStream();
            algorithm.Compress(source, destination, level);
            destination.Position = 0;
            return destination;
        }

        /// <summary>
        /// Compresses data from the source <see cref="Stream"/> using the specified compression algorithm and writes the compressed data to the destination <see cref="Stream"/>.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for compression.</param>
        /// <param name="source">The input <see cref="Stream"/> containing data to be compressed.</param>
        /// <param name="destination">The output <see cref="Stream"/> where the compressed data will be written.</param>
        /// <param name="level">The compression level to use (optional, default is CompressionLevel.Optimal).</param>
        public static void Compress(this ICompressionEncoder algorithm, Stream source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            using (SpanBuffer<byte> bytes = new SpanBuffer<byte>((int)(source.Length - source.Position)))
            {
                source.Read(bytes);
                algorithm.Compress(bytes, destination, level);
            }
        }

        /// <summary>
        /// Compresses data from the input ReadOnlySpan using the specified compression algorithm and returns the compressed data as a <see cref="MemoryPoolStream"/>.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for compression.</param>
        /// <param name="source">The input ReadOnlySpan containing data to be compressed.</param>
        /// <param name="level">The compression level to use (optional, default is CompressionLevel.Optimal).</param>
        /// <returns>A <see cref="MemoryPoolStream"/> containing the compressed data.</returns>
        public static MemoryPoolStream Compress(this ICompressionEncoder algorithm, ReadOnlySpan<byte> source, CompressionLevel level = CompressionLevel.Optimal)
        {
            MemoryPoolStream destination = new MemoryPoolStream();
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
            using (FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                algorithm.Compress(source, destination, level);
        }

        #endregion
    }
}