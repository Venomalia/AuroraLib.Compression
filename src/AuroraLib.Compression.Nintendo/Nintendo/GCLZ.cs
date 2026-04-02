using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;

namespace AuroraLib.Compression.Formats.Nintendo
{
    /// <summary>
    /// GCLZ extension header based on LZ10 algorithm used in Pandora's Tower.
    /// </summary>
    public sealed class GCLZ : ICompressionAlgorithm
    {
        private readonly static LZ10 lz10 = new LZ10();

        private static readonly Identifier32 _identifier = new Identifier32("GCLZ".AsSpan());

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<GCLZ>("GCLZ", new MediaType(MIMEType.Application, "x-nintendo-lz10+gclz"), string.Empty, _identifier);
        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x8 < stream.Length && stream.Peek(s => s.Match(_identifier) && LZ10.IsMatchStatic(s));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                return lz10.GetDecompressedSize(s);
            });

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionSettings settings = default)
        {
            destination.Write(_identifier);
            lz10.Compress(source, destination, settings);
        }

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            lz10.Decompress(source, destination);
        }

    }
}
