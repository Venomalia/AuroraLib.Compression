#if NET6_0_OR_GREATER
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

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

        private static readonly IFormatInfo _info = new FormatInfo<Brotli>("Brotli", new MediaType(MIMEType.Application, "application/x-brotli"), _extensions);
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
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level)
        {
            int quality = level switch
            {
                CompressionLevel.Optimal => 4,
                CompressionLevel.Fastest => 1,
                CompressionLevel.NoCompression => 0,
                CompressionLevel.SmallestSize => 10,
                _ => (int)level,
            };

            using var encoder = new BrotliEncoder(quality, 22); // Quality 0-11 (4), Window 1-24 (22);
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
