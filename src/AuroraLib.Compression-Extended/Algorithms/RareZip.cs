using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Rareware Zip based on DEFLATE, used in banjo-kazooie.
    /// </summary>
    public sealed class RareZip : ICompressionAlgorithm, IProvidesDecompressedSize
    {
        private static readonly Identifier _identifier = new Identifier(new byte[] { 0x11, 0x72 });

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<RareZip>("Rareware Zip", new MediaType(MIMEType.Application, "x-rarezip"), string.Empty);

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        public new static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x6 < stream.Length && stream.Peek(s => s.Match(_identifier) && s.ReadUInt32(Endian.Big) != 0);

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                return s.ReadUInt32(Endian.Big);
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            uint decomLength = source.ReadUInt32(Endian.Big);

            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using (DeflateStream deflate = new DeflateStream(source, CompressionMode.Decompress, true))
                deflate.CopyTo(destination);

            if (destination.Position > endPosition)
            {
                throw new DecompressedSizeException(decomLength, destination.Position - (endPosition - decomLength));
            }
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier.AsSpan());
            destination.Write(source.Length, Endian.Big);
            using DeflateStream deflate = new DeflateStream(destination, level, true);
            deflate.Write(source);
        }
    }
}
