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
    /// LZ01 implementation based on LZSS algorithm used in Skies of Arcadia Legends.
    /// </summary>
    public sealed class LZ01 : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32("LZ01".AsSpan());

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<LZ01>("Sega LZ01", new MediaType(MIMEType.Application, "x-lzss+lz01"), string.Empty, _identifier);

        private static readonly LzProperties _lz = LZSS.Lzss0Properties;

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
                s.Position += 4;
                return s.ReadUInt32();
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            // Mark the initial positions of the streams
            long compressedStartPosition = source.Position;

            // Read Header
            source.MatchThrow(_identifier);
            uint sourceLength = source.ReadUInt32();
            uint decompressedSize = source.ReadUInt32();
            _ = source.ReadUInt32();

            // Perform the decompression
            LZSS.DecompressHeaderless(source, destination, decompressedSize, _lz);

            // Verify compressed size and handle mismatches
            Helper.TraceIfCompressedSizeMismatch(source.Position - compressedStartPosition, sourceLength);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Mark the initial positions of the destination
            long destinationStartPosition = destination.Position;

            // Write Header
            destination.Write(_identifier);
            destination.Write(0); // Compressed length (will be filled in later)
            destination.Write(source.Length);
            destination.Write(0);

            // Perform the compression
            LZSS.CompressHeaderless(source, destination, _lz, LookAhead, level);

            // Go back to the beginning of the file and write out the compressed length
            int destinationLength = (int)(destination.Position - destinationStartPosition);
            destination.At(destinationStartPosition + 4, x => x.Write(destinationLength));
        }
    }
}
