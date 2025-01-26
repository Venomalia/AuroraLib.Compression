using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
using AuroraLib.Core.Buffers;
using AuroraLib.Core.Exceptions;
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
        /// <summary>
        /// Reads the provided <paramref name="source"/> read only span to determine the size of the decompressed data.
        /// </summary>
        /// <param name="algorithm">The decompression algorithm.</param>
        /// <param name="source">The source span with compressed data.</param>
        /// <returns>The size of the decompressed data, in bytes.</returns>
        /// <exception cref="EndOfStreamException"></exception>
        /// <exception cref="InvalidIdentifierException">Thrown if the stream does not contain valid compressed data.</exception>
        public static uint GetDecompressedSize(this IProvidesDecompressedSize algorithm, ReadOnlySpan<byte> source)
            => algorithm.GetDecompressedSize(source.AsReadOnlyStream());

        #region Decompress

        /// <summary>
        /// Decompresses the data from a <paramref name="source"/> stream to a specified <paramref name="destination"/> span of <see cref="byte"/>.
        /// <para>Before calling, use <see cref="IProvidesDecompressedSize.GetDecompressedSize(Stream)"/> to determine the required size for the <paramref name="destination"/> span.</para> 
        /// </summary>
        /// <param name="algorithm">The decompression algorithm.</param>
        /// <param name="source">The source stream with compressed data.</param>
        /// <param name="destination">The destination span for the decompressed data.</param>
        /// <exception cref="IOException"></exception>
        /// <exception cref="EndOfStreamException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidIdentifierException"></exception>
        /// <exception cref="DecompressedSizeException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static unsafe void Decompress(this IProvidesDecompressedSize algorithm, Stream source, Span<byte> destination)
        {
            fixed (byte* ptr = destination)
            {
                Stream destinationStream = new UnmanagedMemoryStream(ptr, destination.Length, destination.Length, FileAccess.Write);
                algorithm.Decompress(source, destinationStream);
            }
        }

        /// <summary>
        /// Decompresses data from a <paramref name="source"/> read only span of <see cref="byte"/> to a specified <paramref name="destination"/> span of <see cref="byte"/>.
        /// <para>Before calling, use <see cref="IProvidesDecompressedSize.GetDecompressedSize(Stream)"/> to determine the required size for the <paramref name="destination"/> span.</para> 
        /// </summary>
        /// <param name="algorithm">The decompression algorithm.</param>
        /// <param name="source">The source span with compressed data.</param>
        /// <param name="destination">The destination span for the decompressed data.</param>
        /// <exception cref="IOException"></exception>
        /// <exception cref="EndOfStreamException"></exception>
        /// <exception cref="InvalidIdentifierException"></exception>
        /// <exception cref="DecompressedSizeException"></exception>
        public static unsafe void Decompress(this IProvidesDecompressedSize algorithm, ReadOnlySpan<byte> source, Span<byte> destination)
            => algorithm.Decompress(source.AsReadOnlyStream(), destination);

        /// <summary>
        /// Decompresses data from the <paramref name="source"/> ReadOnlySpan and writes the decompressed data to the <paramref name="destination"/> <see cref="Stream"/>.
        /// </summary>
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
            algorithm.Decompress(source.AsReadOnlyStream(), out byte[] destination);
            return destination;
        }

        /// <summary>
        /// Decompresses data from the specified <paramref name="source"/> <see cref="Stream"/> and outputs the decompressed data to a <paramref name="destination"/> <see cref="byte"/> array.
        /// </summary>
        /// <param name="algorithm">The decompression algorithm.</param>
        /// <param name="source">The <see cref="Stream"/> containing the compressed data.</param>
        /// <param name="destination">The output byte array that will contain the decompressed data.</param>
        /// <exception cref="EndOfStreamException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidIdentifierException"></exception>
        /// <exception cref="DecompressedSizeException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static void Decompress(this ICompressionDecoder algorithm, Stream source, out byte[] destination)
        {
            if (algorithm is IProvidesDecompressedSize providesDecompressed)
            {
                destination = new byte[providesDecompressed.GetDecompressedSize(source)];
                providesDecompressed.Decompress(source, destination);
            }
            else
            {
                using MemoryPoolStream buffer = new MemoryPoolStream();
                algorithm.Decompress(source, buffer);
                destination = buffer.ToArray();
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
            using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
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
        /// Compresses data from the specified <paramref name="source"/> read only span and outputs the compressed data to a <paramref name="destination"/> <see cref="byte"/> array.
        /// </summary>
        /// <param name="algorithm">The compression algorithm.</param>
        /// <param name="source">The <see cref="ReadOnlySpan{Byte}"/> containing the data to be compressed.</param>
        /// <param name="destination">The output byte array that will contain the compressed data.</param>
        /// <param name="level">The <see cref="CompressionLevel"/> to use for compression (default is <see cref="CompressionLevel.Optimal"/>).</param>
        public static void Compress(this ICompressionEncoder algorithm, ReadOnlySpan<byte> source, out byte[] destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            using MemoryPoolStream buffer = algorithm.Compress(source, level);
            destination = buffer.ToArray();
        }

        /// <summary>
        /// Compresses data from the specified <paramref name="source"/> <see cref="Stream"/> and outputs the compressed data to a <paramref name="destination"/> <see cref="byte"/> array.
        /// </summary>
        /// <param name="algorithm">The compression algorithm.</param>
        /// <param name="source">The <see cref="Stream"/> containing the data to be compressed.</param>
        /// <param name="destination">The output byte array that will contain the compressed data.</param>
        /// <param name="level">The <see cref="CompressionLevel"/> to use for compression (default is <see cref="CompressionLevel.Optimal"/>).</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is null.</exception>
        /// <exception cref="NotSupportedException">Thrown if the <paramref name="source"/> stream does not support reading.</exception>
        public static void Compress(this ICompressionEncoder algorithm, Stream source, out byte[] destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            using MemoryPoolStream buffer = algorithm.Compress(source, level);
            destination = buffer.ToArray();
        }

        /// <summary>
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
