#if NET6_0_OR_GREATER
using AuroraLib.Compression;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Brotli developed by Jyrki Alakuijala and Zoltán Szabadka as successor to gzip and deflate.
    /// </summary>
    public sealed class Brotli : ICompressionAlgorithm
    {
        private static readonly string[] _extensions = new string[] { ".br", string.Empty };

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<Brotli>("Brotli", new MediaType(MIMEType.Application, "x-brotli"), _extensions);
        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
        {
            if (!fileNameAndExtension.IsEmpty && !PathX.GetExtension(fileNameAndExtension).Contains(_extensions[0].AsSpan(), StringComparison.InvariantCultureIgnoreCase))
                return false;

            var decoder = new BrotliDecoder();
            long startpos = stream.Position;
            int testLen = Math.Min((int)(stream.Length - startpos), 0x80);
            Span<byte> testData = stackalloc byte[testLen];
            Span<byte> destination = stackalloc byte[0x40];

            stream.ReadExactly(testData);
            var result = decoder.Decompress(testData, destination, out int _, out int _);
            stream.Seek(startpos, SeekOrigin.Begin);
            return result != OperationStatus.InvalidData;
        }

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            using BrotliStream br = new BrotliStream(source, CompressionMode.Decompress, true);
            br.CopyTo(destination);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionSettings settings = default)
        {
            int quality = (settings.Quality * 11) / 15;// Quality 0-11 (5)
            int window = settings.MaxWindowBits; // Window 10-24 (22);
            window = window == 0 ? 19 + (quality / 2) : window.Clamp(10, 24);

            using var encoder = new BrotliEncoder(quality, window);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(0x8000);
            try
            {
                OperationStatus status = OperationStatus.DestinationTooSmall;
                int bytesConsumed = 0, bytesWritten = 0;
                while (status == OperationStatus.DestinationTooSmall)
                {
                    status = encoder.Compress(source, buffer, out bytesConsumed, out bytesWritten, true);
                    destination.Write(buffer, 0, bytesWritten);
                    source = source[bytesConsumed..];
                }

                if (status == OperationStatus.Done)
                    return;
                else
                    throw new InvalidDataException("Brotli compression failed due to invalid data.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
#endif
