using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// ZLB based on ZLib compression algorithm used in Star Fox Adventures.
    /// </summary>
    public sealed class ZLB : ICompressionAlgorithm, IHasIdentifier, IProvidesDecompressedSize
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32((byte)'Z', (byte)'L', (byte)'B', 0x0);

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<ZLB>("ZLB", new MediaType(MIMEType.Application, "zlib+zlb"), ".zlb", _identifier);

        private static readonly ZLib zLib = new ZLib();

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x14 < stream.Length && stream.Peek(s => s.Match(_identifier));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                return s.Read<Header>(Endian.Big).DecompressedSize;
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            // Read Header
            source.MatchThrow(_identifier);
            Header header = source.Read<Header>(Endian.Big);

            // Validate header version
            if (header.Version != 1)
            {
                Trace.WriteLine($"Unexpected header version: {header.Version}");
            }

            // Mark the initial positions of the streams
            long compressedStartPosition = source.Position;
            long destinationStartPosition = destination.Position;

            // Perform the decompression
            zLib.Decompress(source, destination, (int)header.CompressedSize);

            // Verify decompressed size
            DecompressedSizeException.ThrowIfMismatch(destination.Position - destinationStartPosition, header.DecompressedSize);

            // Verify compressed size and handle mismatches
            Helper.TraceIfCompressedSizeMismatch(source.Position - compressedStartPosition, header.CompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Mark the initial positions of the destination
            long start = destination.Position;

            // Write Header
            destination.Write(_identifier);
            destination.Write(1, Endian.Big);
            destination.Write(source.Length, Endian.Big);
            destination.Write(0, Endian.Big); // Placeholder

            // Perform the compression
            zLib.Compress(source, destination, level);

            // Go back to the beginning of the file and write out the compressed length
            destination.At(start + 12, s => s.Write((uint)(destination.Length - start - 0x14), Endian.Big));
        }

        private readonly struct Header : IReversibleEndianness<Header>
        {
            public readonly uint Version; // 1
            public readonly uint DecompressedSize;
            public readonly uint CompressedSize;

            public Header(uint version, uint decompressedSize, uint compressedSize)
            {
                Version = version;
                DecompressedSize = decompressedSize;
                CompressedSize = compressedSize;
            }

            public Header ReverseEndianness()
                => new Header(
                    BinaryPrimitives.ReverseEndianness(Version),
                    BinaryPrimitives.ReverseEndianness(DecompressedSize),
                    BinaryPrimitives.ReverseEndianness(CompressedSize));
        }
    }
}
