using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// GZip open-source compression algorithm, which is based on the DEFLATE compression format.
    /// </summary>
    public sealed class GZip : ICompressionAlgorithm
    {
        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<GZip>("gzip", new MediaType(MIMEType.Application, "gzip"), ".gz");

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x8 < stream.Length && (stream.Peek<uint>() & 0x00FFFFFF) == 0x088B1F;

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            using (GZipStream algo = new GZipStream(source, CompressionMode.Decompress, true))
                algo.CopyTo(destination);
            source.Position = source.Length;
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            using (GZipStream algo = new GZipStream(destination, level, true))
                algo.Write(source);
        }
    }
}
