using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;
using ZstdSharp;

namespace AuroraLib.Compression.CLI.Algorithms
{
    public class Zstd : ICompressionAlgorithm
    {
        public IFormatInfo Info => new FormatInfo<Zstd>("Zstandard", new MediaType(MIMEType.Application, "zstd"), ".zst");

        private static readonly Identifier32 _identifier = new(0xFD2FB528);

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Length > 0x10 && stream.Match(_identifier);

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            int zslevel = level switch
            {
                CompressionLevel.NoCompression => 0,
                CompressionLevel.Fastest => 5,
                CompressionLevel.Optimal => 10,
#if NET6_0_OR_GREATER
                CompressionLevel.SmallestSize => 20,
#endif
                _ => throw new NotImplementedException(),
            };
            Compress(source, destination, zslevel);
        }

        public static void Compress(ReadOnlySpan<byte> source, Stream destination, int level)
        {
            using var compressionStream = new CompressionStream(destination, level);
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
