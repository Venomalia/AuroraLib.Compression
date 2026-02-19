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
    /// Level5 SSZL algorithm base on LZSS, first used in Inazuma Eleven 3.
    /// </summary>
    public class Level5LZSS : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {

        private static readonly Identifier32 _identifier = new Identifier32("SSZL".AsSpan());

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<Level5LZSS>("Level5 lzss", new MediaType(MIMEType.Application, "x-lzss+level5"), string.Empty, _identifier);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.Match(_identifier) && s.ReadUInt32() == 0);

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                s.Position += 8;
                return s.ReadUInt32();
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            // Read Header
            source.MatchThrow(_identifier);
            _ = source.ReadUInt32();
            uint compressedSize = source.ReadUInt32();
            uint decompressedSize = source.ReadUInt32();

            // Mark the initial positions of the streams
            long compressedStartPosition = source.Position;

            // Perform the decompression
            LZSS.DecompressHeaderless(source, destination, decompressedSize, LZSS.Lzss0Properties);

            // Verify compressed size and handle mismatches
            Helper.TraceIfCompressedSizeMismatch(source.Position - compressedStartPosition, compressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            long startpos = destination.Length;

            destination.Write(_identifier);
            destination.Write(0);
            destination.Write(0); // Placeholder
            destination.Write(source.Length);

            LZSS.CompressHeaderless(source, destination, LZSS.Lzss0Properties, LookAhead, level);
            destination.At(startpos + 0x8, s => s.Write((uint)(destination.Length - startpos - 0x10)));
        }
    }
}
