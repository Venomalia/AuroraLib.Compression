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
    /// Sega MDB4 base on LZSS, used in Typing of the Dead for the PlayStation 2
    /// </summary>
    public class MDB4 : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {
        private static readonly Identifier32 _identifier = new Identifier32("MDB4".AsSpan());

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<MDB4>("Sega MDB4", new MediaType(MIMEType.Application, "x-lzss+MDB4"), ".MDB", _identifier);

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
                _ = s.ReadUInt32LittleEndian();
                return s.ReadUInt32LittleEndian();
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            // Read Header
            source.MatchThrow(_identifier);
            _ = source.ReadUInt32LittleEndian();
            uint decompressedSize = source.ReadUInt32LittleEndian();
            uint compressedSize = source.ReadUInt32LittleEndian();
            source.Skip(4 * 4);

            // Mark the initial positions of the streams
            long compressedStartPosition = source.Position;

            // Perform the decompression
            LZSS.DecompressHeaderless(source, destination, decompressedSize, LZSS.DefaultProperties); //Maybe Sega Saxman LZSS? should be the same as our LZSS.DefaultProperties.

            // Verify compressed size and handle mismatches
            Helper.TraceIfCompressedSizeMismatch(source.Position - compressedStartPosition, compressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Mark the initial positions of the destination
            long destinationStartPosition = destination.Position;

            // Write Header
            destination.Write(_identifier);
            destination.Write(source.Length + 1); // Perhaps a byte flag and source.Length as uint24?
            destination.Write(source.Length);
            destination.Write(0); // Compressed length (will be filled in later)

            destination.Write(0, 4); //4*4 byte

            // Perform the compression
            LZSS.CompressHeaderless(source, destination, LZSS.DefaultProperties, LookAhead, level);

            // Go back to the beginning of the file and write out the compressed length
            int destinationLength = (int)(destination.Position - destinationStartPosition - 0x10);
            destination.At(destinationStartPosition + 12, x => x.Write(destinationLength));
        }
    }
}
