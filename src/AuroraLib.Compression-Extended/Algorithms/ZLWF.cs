using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
using AuroraLib.Core.Exceptions;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using static AuroraLib.Compression.Algorithms.WFLZ;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// WayForward's LZ chunk header.
    /// </summary>
    public sealed class ZLWF : ICompressionAlgorithm, ILzSettings, IHasIdentifier, IProvidesDecompressedSize, IEndianDependentFormat
    {/// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32("ZLWF".AsSpan());

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<ZLWF>("WayForward Chunk LZ", new MediaType(MIMEType.Application, "-wflz+chunk"), string.Empty, _identifier);
        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public Endian FormatByteOrder { get; set; } = Endian.Big;

        /// <summary>
        /// Specifies the maximum size of the data blocks to be written.
        /// </summary>
        public uint BlockSize = 0x400000;

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
                var header = s.Read<Header>();
                if (header.Identifier != _identifier)
                    throw new InvalidIdentifierException(header.Identifier.AsSpan(), _identifier.AsSpan());
                return FormatByteOrder == Endian.Little ? header.DecompressedSize : BinaryPrimitives.ReverseEndianness(header.DecompressedSize);
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            long startPos = source.Position;
            // Read Header
            var header = source.Read<Header>();
            if (header.Identifier != _identifier)
                throw new InvalidIdentifierException(header.Identifier.AsSpan(), _identifier.AsSpan());
            int compressedSize = FormatByteOrder == Endian.Little ? (int)header.CompressedSize : BinaryPrimitives.ReverseEndianness((int)header.CompressedSize);
            int decompressedSize = FormatByteOrder == Endian.Little ? (int)header.DecompressedSize : BinaryPrimitives.ReverseEndianness((int)header.DecompressedSize);
            int chunks = source.ReadInt32(FormatByteOrder);

            // Read offsets
            Span<int> offsets = stackalloc int[chunks];
            source.Read(offsets, FormatByteOrder);

            // set destination length
            long endPosition = destination.Position + decompressedSize;
            destination.SetLength(endPosition);

            // Decompress
            WFLZ decoder = new WFLZ() { FormatByteOrder = FormatByteOrder };
            for (int i = 0; i < chunks; i++)
            {
                source.Seek(startPos + offsets[i], SeekOrigin.Begin);
                decoder.Decompress(source, destination);
            }

            // Verify decompressed size
            if (destination.Position != endPosition)
            {
                throw new DecompressedSizeException(decompressedSize, destination.Position - (endPosition - decompressedSize));
            }
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            const int Align = 0x10;
            // Mark the initial positions of the destination
            long destinationStartPosition = destination.Position;
            int chunks = (int)((source.Length + BlockSize - 1) / BlockSize);
            Span<int> offsets = stackalloc int[chunks];

            // Write Header
            destination.Write(_identifier);
            destination.Write(0); // Compressed length (will be filled in later)
            destination.Write(source.Length, FormatByteOrder);
            destination.Write(chunks, FormatByteOrder);

            // Write offsets
            destination.Write<int>(offsets); // Offsets (will be filled in later)

            // Perform the compression
            WFLZ encoder = new WFLZ() { FormatByteOrder = FormatByteOrder, LookAhead = LookAhead };
            for (int i = 0; i < chunks; i++)
            {
                int blockStart = (int)BlockSize * i;
                int blockSize = Math.Min((int)BlockSize, source.Length - blockStart);
                destination.WriteAlign(Align);
                offsets[i] = (int)(destination.Position - destinationStartPosition);
                encoder.Compress(source.Slice(blockStart, blockSize), destination);
            }
            destination.WriteAlign(Align);

            // Go back to the beginning of the file and write out the compressed length
            int destinationLength = (int)(destination.Position - destinationStartPosition - 0x10);
            destination.Seek(destinationStartPosition + 4, SeekOrigin.Begin);
            destination.Write(destinationLength, FormatByteOrder);
            destination.Skip(8);
            destination.Write<int>(offsets, FormatByteOrder);
            destination.Seek(destination.Length,SeekOrigin.Begin);
        }
    }
}
