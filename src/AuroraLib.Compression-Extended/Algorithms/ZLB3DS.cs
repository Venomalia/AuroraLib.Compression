using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// ZLB 3ds based on ZLib compression algorithm.
    /// </summary>
    public sealed class ZLB3DS : ICompressionAlgorithm, IProvidesDecompressedSize
    {
        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<ZLB3DS>("ZLB (3ds)", new MediaType(MIMEType.Application, "zlib+zlb3ds"), ".zlb");

        private static readonly ZLib zLib = new ZLib();

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x6 < stream.Length && stream.Peek(s => s.ReadUInt32LittleEndian() != 0 && ZLib.IsMatchStatic(s));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source) => source.Peek(s => s.ReadUInt32LittleEndian());

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            // Read Header
            uint decompressedSize = source.ReadUInt32LittleEndian();
            long endPosition = destination.Position + decompressedSize;
            destination.SetLength(endPosition);
            // Perform the decompression
            zLib.Decompress(source, destination);

            // Verify decompressed size
            if (destination.Position > endPosition)
                throw new DecompressedSizeException(decompressedSize, destination.Position - (endPosition - decompressedSize));
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(source.Length);

            // Perform the compression
            zLib.Compress(source, destination, level);
        }
    }
}
