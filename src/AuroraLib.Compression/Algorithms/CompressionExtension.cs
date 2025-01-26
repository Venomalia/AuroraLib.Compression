using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
using AuroraLib.Core.Buffers;
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
        /// </summary>
        /// Decompresses data from the <paramref name="source"/> ReadOnlySpan and writes the decompressed data to the <paramref name="destination"/> <see cref="Stream"/>.
        /// <param name="algorithm">The decompression algorithm.</param>
        /// <param name="source">The ReadOnlySpan containing the compressed data.</param>
        /// <param name="destination">The <see cref="Stream"/> to write the decompressed data to.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidIdentifierException"></exception>
        /// <exception cref="DecompressedSizeException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static void Decompress(this ICompressionDecoder algorithm, ReadOnlySpan<byte> source, Stream destination)
            => algorithm.Decompress(source.AsReadOnlyStream(), destination);

        /// <summary>
        /// Decompresses data from the <paramref name="source"/> ReadOnlySpan and returns the decompressed data as a <see cref="byte"/> array.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for decompression.</param>
        /// <param name="source">The ReadOnlySpan containing compressed data to be decompressed.</param>
        /// <returns>A new <see cref="byte"/> array containing the decompressed data.</returns>
        /// <exception cref="EndOfStreamException"></exception>
        /// <exception cref="InvalidIdentifierException"></exception>
        /// <exception cref="DecompressedSizeException"></exception>
        public static byte[] Decompress(this ICompressionDecoder algorithm, ReadOnlySpan<byte> source)
        {
            using (MemoryPoolStream destination = new MemoryPoolStream())
            {
                algorithm.Decompress(source, destination);
                return destination.ToArray();
            }
        }

        /// <summary>
        /// Decompresses data from the <paramref name="source"/> <see cref="Stream"/> and returns a <see cref="MemoryPoolStream"/> containing the decompressed data.
        /// <para>After use, the <see cref="MemoryPoolStream"/> needs to be <see cref="IDisposable.Dispose"/>.</para> 
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for decompression.</param>
        /// <param name="source">The input <see cref="Stream"/> containing compressed data to be decompressed.</param>
        /// <returns>A <see cref="MemoryPoolStream"/> containing the decompressed data.</returns>
        /// <exception cref="EndOfStreamException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidIdentifierException"></exception>
        /// <exception cref="DecompressedSizeException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static MemoryPoolStream Decompress(this ICompressionDecoder algorithm, Stream source)
        {
            MemoryPoolStream destination = new MemoryPoolStream();
            algorithm.Decompress(source, destination);
            destination.Position = 0;
            return destination;
        }

        /// <summary>
        /// Decompresses data from the specified source file and writes the decompressed data to the specified destination file.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for decompression.</param>
        /// <param name="sourceFile">The path to the source file containing compressed data to be decompressed.</param>
        /// <param name="destinationFile">The path to the destination file where the decompressed data will be written.</param>
        /// <exception cref="EndOfStreamException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidIdentifierException"></exception>
        /// <exception cref="DecompressedSizeException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="PathTooLongException"></exception>
        public static void Decompress(this ICompressionDecoder algorithm, string sourceFile, string destinationFile)
        {
            using (FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                algorithm.Decompress(source, destination);
        }
        #endregion

        #region Compress

        /// <summary>
        /// Compresses data from the specified <paramref name="source"/> <see cref="Stream"/> and returns a <see cref="MemoryPoolStream"/> containing the compressed data.
        /// <para>After use, the <see cref="MemoryPoolStream"/> needs to be <see cref="IDisposable.Dispose"/>.</para> 
        /// </summary>
        /// <param name="algorithm">The compression algorithm.</param>
        /// <param name="source">The <see cref="Stream"/> containing the data to be compressed.</param>
        /// <param name="level">The <see cref="CompressionLevel"/> to use for compression (default is <see cref="CompressionLevel.Optimal"/>).</param> 
        /// <returns>A <see cref="MemoryPoolStream"/> containing the compressed data.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static MemoryPoolStream Compress(this ICompressionEncoder algorithm, Stream source, CompressionLevel level = CompressionLevel.Optimal)
        {
            MemoryPoolStream destination = new MemoryPoolStream();
            algorithm.Compress(source, destination, level);
            destination.Position = 0;
            return destination;
        }

        /// <summary>
        /// Compresses data from the specified <paramref name="source"/> <see cref="Stream"/> and writes the compressed data to the specified <paramref name="destination"/> <see cref="Stream"/>.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for compression.</param>
        /// <param name="source">The input <see cref="Stream"/> containing data to be compressed.</param>
        /// <param name="destination">The output <see cref="Stream"/> where the compressed data will be written.</param>
        /// <param name="level">The <see cref="CompressionLevel"/> to use for compression (default is <see cref="CompressionLevel.Optimal"/>).</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static void Compress(this ICompressionEncoder algorithm, Stream source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            using SpanBuffer<byte> bytes = new SpanBuffer<byte>((int)(source.Length - source.Position));
            source.Read(bytes);
            algorithm.Compress(bytes, destination, level);
        }

        /// <summary>
        /// Compresses data from the specified <paramref name="source"/> ReadOnlySpan and returns a <see cref="MemoryPoolStream"/> containing the compressed data.
        /// <para>After use, the <see cref="MemoryPoolStream"/> needs to be <see cref="IDisposable.Dispose"/>.</para> 
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for compression.</param>
        /// <param name="source">The input ReadOnlySpan containing data to be compressed.</param>
        /// <param name="level">The <see cref="CompressionLevel"/> to use for compression (default is <see cref="CompressionLevel.Optimal"/>).</param>
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
        /// Compresses data from the specified <paramref name="sourceFile"/> and writes the compressed data to the specified <paramref name="destinationFile"/>.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use for compression.</param>
        /// <param name="sourceFile">The path to the source file containing data to be compressed.</param>
        /// <param name="destinationFile">The path to the destination file where the compressed data will be written.</param>
        /// <param name="level">The <see cref="CompressionLevel"/> to use for compression (default is <see cref="CompressionLevel.Optimal"/>).</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="UnauthorizedAccessException"></exception>
        /// <exception cref="PathTooLongException"></exception>
        public static void Compress(this ICompressionEncoder algorithm, string sourceFile, string destinationFile, CompressionLevel level = CompressionLevel.Optimal)
        {
            using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            algorithm.Compress(source, destination, level);
        }

        #endregion
    }
}
