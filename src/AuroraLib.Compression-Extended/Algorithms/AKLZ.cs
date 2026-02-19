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
    /// AKLZ implementation based on LZSS algorithm used in Skies of Arcadia Legends.
    /// </summary>
    public sealed class AKLZ : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {

        private static readonly Identifier _identifier = new Identifier("AKLZ~?Qd=ÌÌÍ");

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<AKLZ>("Arcadia LZ", new MediaType(MIMEType.Application, "x-lzss+arcadia"), string.Empty, _identifier);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        private static readonly LzProperties _lz = LZSS.DefaultProperties;

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
                return s.ReadUInt32(Endian.Big);
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            uint decompressedSize = source.ReadUInt32(Endian.Big);
            LZSS.DecompressHeaderless(source, destination, decompressedSize, _lz);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier.AsSpan());
            destination.Write(source.Length, Endian.Big);
            LZSS.CompressHeaderless(source, destination, _lz, LookAhead, level);
        }
    }
}
