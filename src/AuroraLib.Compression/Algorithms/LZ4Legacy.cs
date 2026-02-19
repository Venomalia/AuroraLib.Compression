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
    /// <see cref="LZ4"/> initial versions of “LZ4Demo”, known as LZ4Legacy.
    /// </summary>
    public sealed class LZ4Legacy : ICompressionAlgorithm, ILzSettings
    {

        private static readonly Identifier32 _identifier = new Identifier32((uint)LZ4.FrameTypes.Legacy);

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<LZ4Legacy>("LZ4 Legacy Compression", new MediaType(MIMEType.Application, "x-lz4demo"), ".lz4", _identifier);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        private LZ4 algorithmlZ4 = new LZ4();

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.Match(_identifier));

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            algorithmlZ4.Decompress(source, destination);
        }

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            algorithmlZ4.FrameType = LZ4.FrameTypes.Legacy;
            algorithmlZ4.LookAhead = LookAhead;
            algorithmlZ4.Compress(source, destination, level);
        }
    }
}
