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
    /// AsuraZlb based on ZLib compression algorithm used in The Simpsons Game.
    /// </summary>
    public sealed class AsuraZlb : ICompressionAlgorithm, IHasIdentifier, IProvidesDecompressedSize
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier64 _identifier = new Identifier64("AsuraZlb".AsSpan());

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<AsuraZlb>("Asura zlip", new MediaType(MIMEType.Application, "zlip+asura"), string.Empty, _identifier);

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
                s.Position += 8;
                return s.ReadUInt32(Endian.Big);
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            long start = destination.Position;
            source.MatchThrow(_identifier);
            source.Skip(4);
            uint compressedSize = source.ReadUInt32(Endian.Big);
            uint decompressedSize = source.ReadUInt32(Endian.Big);

            // Mark the initial positions of the streams
            long compressedStartPosition = source.Position;
            long destinationStartPosition = destination.Position;

            // Perform the decompression
            zLib.Decompress(source, destination, (int)compressedSize);

            // Verify decompressed size
            DecompressedSizeException.ThrowIfMismatch(destination.Position - destinationStartPosition, decompressedSize);

            // Verify compressed size and handle mismatches
            Helper.TraceIfCompressedSizeMismatch(source.Position - compressedStartPosition, compressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            long start = destination.Position;
            destination.Write(_identifier);
            destination.Write(1);
            destination.Write(0, Endian.Big); // Placeholder
            destination.Write(source.Length, Endian.Big);
            zLib.Compress(source, destination, level);
            destination.At(start + 12, s => s.Write((uint)(destination.Length - start - 0x14), Endian.Big));
        }
    }
}
