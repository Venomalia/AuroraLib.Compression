using AuroraLib.Compression.Formats.Nintendo;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;

namespace AuroraLib.Compression.Formats.Sega
{
    /// <summary>
    /// COMP extension header based on LZ11 algorithm used in Puyo Puyo Chronicle.
    /// </summary>
    public sealed class COMP : ICompressionAlgorithm
    {
        private readonly static LZ11 lz11 = new LZ11();
        private static readonly Identifier32 _identifier = new Identifier32("COMP".AsSpan());

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<COMP>("COMP", new MediaType(MIMEType.Application, "x-nintendo-lz11+comp"), string.Empty, _identifier);

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x8 < stream.Length && stream.Peek(s => s.Match(_identifier) && LZ11.IsMatchStatic(s));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                return lz11.GetDecompressedSize(s);
            });

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionSettings settings = default)
        {
            destination.Write(_identifier);
            lz11.Compress(source, destination, settings);
        }

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            lz11.Decompress(source, destination);
        }
    }
}
