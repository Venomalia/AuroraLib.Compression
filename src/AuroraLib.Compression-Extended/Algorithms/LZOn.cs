using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// LZO Nintendo mainly used in DS Download Games.
    /// </summary>
    public sealed class LZOn : ICompressionAlgorithm, ILzSettings, IHasIdentifier, IProvidesDecompressedSize
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier64 _identifier = new Identifier64(new Identifier32("LZOn".AsSpan()), new Identifier32(0x00, 0x2F, 0xF1, 0x71));

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<LZOn>("LZO Nintendo", new MediaType(MIMEType.Application, "x-lzo+nintendo"), string.Empty, _identifier);

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
                return s.ReadUInt32(Endian.Big);
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            // Read Header
            source.MatchThrow(_identifier);
            uint decompressedSize = source.ReadUInt32(Endian.Big);
            uint compressedSize = source.ReadUInt32(Endian.Big);

            // Mark the initial positions of the streams
            long compressedStartPosition = source.Position;
            long destinationStartPosition = destination.Position;

            // Perform the decompression
            LZO.DecompressHeaderless(source, destination);

            // Verify decompressed size
            DecompressedSizeException.ThrowIfMismatch(destination.Position - destinationStartPosition, decompressedSize);

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
            destination.Write(source.Length, Endian.Big);
            destination.Write(0); // Compressed length (will be filled in later)

            // Perform the compression
            LZO.CompressHeaderless(source, destination, LookAhead, level);

            // Go back to the beginning of the file and write out the compressed length
            int destinationLength = (int)(destination.Position - destinationStartPosition - 0x10);
            destination.At(destinationStartPosition + 12, x => x.Write(destinationLength, Endian.Big));
        }
    }
}
