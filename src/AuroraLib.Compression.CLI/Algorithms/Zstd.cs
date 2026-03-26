using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;
using ZstdSharp;

namespace AuroraLib.Compression.CLI.Algorithms
{
    public class Zstd : ICompressionAlgorithm
    {
        public IFormatInfo Info => new FormatInfo<Zstd>("Zstandard", new MediaType(MIMEType.Application, "zstd"), ".zst", _identifier);

        private static readonly Identifier32 _identifier = new(0xFD2FB528);

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Length > 0x10 && stream.Match(_identifier);

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionSettings settings = default)
        {
            using var compressionStream = new CompressionStream(destination, settings.Quality);
            compressionStream.SetPledgedSrcSize((ulong)source.Length);
            compressionStream.Write(source);
        }

        public void Decompress(Stream source, Stream destination)
        {
            using var decompressionStream = new DecompressionStream(source);
            decompressionStream.CopyTo(destination);
        }
    }
}
