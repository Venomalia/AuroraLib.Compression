using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// 3DS-LZ is a header for LZ10 compressed files in some games on the 3DS.
    /// </summary>
    public sealed class LZ_3DS : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {
        private static readonly Identifier64 _identifier = new Identifier64("3DS-LZ\r\n".AsSpan());

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<LZ_3DS>("3DS-LZ", new MediaType(MIMEType.Application, "x-nintendo-lz10+3DS-LZ"), string.Empty, _identifier);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;
        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.Match(_identifier));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                return new LZ10().GetDecompressedSize(source);
            });

        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            new LZ10().Decompress(source, destination);
        }

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier);
            new LZ10() { LookAhead = LookAhead }.Compress(source, destination);
        }
    }
}
