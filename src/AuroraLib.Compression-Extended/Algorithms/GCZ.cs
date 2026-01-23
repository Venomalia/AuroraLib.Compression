using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Konami GCZ based on LZSS, mainly used in Konami/Bemani music games.
    /// </summary>
    public sealed class GCZ : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {
        private const string _extension = ".gcz";
        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<GCZ>("Konami GCZ", new MediaType(MIMEType.Application, "x-lzss+gcz"), _extension);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        private static readonly LzProperties _lz = LZSS.Lzss0Properties;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x10 < stream.Length && PathX.GetExtension(fileNameAndExtension).Contains(_extension.AsSpan(), StringComparison.InvariantCultureIgnoreCase) && stream.Peek(s =>
            {
                uint x = s.ReadUInt32();
                return x != 0 && x != 0x4347; // 0 or GC
            });

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source) => source.Peek(s => s.ReadUInt32());

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            uint decompressedSize = source.ReadUInt32();
            LZSS.DecompressHeaderless(source, destination, decompressedSize, _lz);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(source.Length);
            LZSS.CompressHeaderless(source, destination, _lz, LookAhead, level);
        }
    }
}
