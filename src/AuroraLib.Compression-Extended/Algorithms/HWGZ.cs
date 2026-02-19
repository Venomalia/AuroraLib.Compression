using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Hyrule Warriors GZ compression format based on ZLib.
    /// </summary>
    public sealed class HWGZ : ICompressionAlgorithm, IEndianDependentFormat, IProvidesDecompressedSize
    {
        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<HWGZ>("Hyrule Warriors GZ", new MediaType(MIMEType.Application, "zlib+hwgz"), ".gz");

        /// <summary>
        /// Defines the size of the chunks.
        /// </summary>
        public int ChunkSize = 0x10000;

        /// <inheritdoc/>
        public Endian FormatByteOrder { get; set; } = Endian.Big;

        /// <inheritdoc/>
        public bool IsMatch(Stream source, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(source, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream source, ReadOnlySpan<char> fileNameAndExtension = default)
        {
            if (source.Length < 0x80)
                return false;

            long pos = source.Position;
            if (!Header.TryRead(source, out Header header, out Endian order))
                return false;

            source.Align(4 * header.ChunkCount, SeekOrigin.Current, 128);
            bool result = source.At(4, SeekOrigin.Current, s => ZLib.IsMatchStatic(s));
            source.Position = pos;
            return result;
        }

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                if (!Header.TryRead(s, out Header header, out Endian order))
                    throw new InvalidOperationException("Header a Invalidate");
                return header.DecompressedSize;
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            // Read Header
            if (!Header.TryRead(source, out Header header, out Endian order))
                throw new InvalidOperationException("Header a Invalidate");

            source.Align(4 * header.ChunkCount, SeekOrigin.Current, 128);

            // Mark the initial positions of the destination
            long destinationStartPosition = destination.Position;

            ZLib zLib = new ZLib();
            // Decompress each chunk
            for (int i = 0; i < header.ChunkCount; i++)
            {
                int chunkDataSize = source.ReadInt32(FormatByteOrder);
                zLib.Decompress(source, destination, chunkDataSize);
                source.Align(128);
            }

            // Verify decompressed size
            DecompressedSizeException.ThrowIfMismatch(destination.Position - destinationStartPosition, header.DecompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Mark the initial positions of the destination
            long destStart = destination.Position;

            int chunkCount = (source.Length + ChunkSize - 1) / ChunkSize;
            uint[] chunkSizes = new uint[chunkCount];

            // Write Header
            destination.Write(ChunkSize, FormatByteOrder);
            destination.Write(chunkCount, FormatByteOrder);
            destination.Write(source.Length, FormatByteOrder);
            destination.Write<uint>(chunkSizes); // Placeholder

            destination.WriteAlign(128);

            ZLib zLib = new ZLib();
            // Compress each chunk
            using (MemoryPoolStream buffer = new MemoryPoolStream(ChunkSize))
            {
                for (int i = 0; i < chunkCount; i++)
                {
                    int segmentStart = i * ChunkSize;
                    int segmentSize = Math.Min(ChunkSize, source.Length - segmentStart);

                    buffer.SetLength(0);
                    zLib.Compress(source.Slice(segmentStart, segmentSize), buffer, level);

                    ReadOnlySpan<byte> segmentData = buffer.UnsafeAsSpan();
                    chunkSizes[i] = (uint)(segmentData.Length + 4);
                    destination.Write(segmentData.Length, FormatByteOrder);
                    destination.Write(segmentData);
                    destination.WriteAlign(128);
                }
            }

            if (FormatByteOrder == Endian.Big)
            {
#if NET8_0
                BinaryPrimitives.ReverseEndianness(chunkSizes, chunkSizes);
#else
                for (int i = 0; i < chunkSizes.Length; i++)
                    chunkSizes[0] = BinaryPrimitives.ReverseEndianness(chunkSizes[0]);
#endif
            }
            destination.At(destStart + 12, s => s.Write<uint>(chunkSizes));
        }

        private readonly struct Header : IReversibleEndianness<Header>
        {
            public readonly uint ChunkSize;
            public readonly uint ChunkCount;
            public readonly uint DecompressedSize;

            public bool Validate() => ChunkCount != 0 && ChunkCount == (DecompressedSize + ChunkSize - 1) / ChunkSize;

            public Header(uint chunkSize, uint chunkCount, uint decompressedSize)
            {
                ChunkSize = chunkSize;
                ChunkCount = chunkCount;
                DecompressedSize = decompressedSize;
            }

            public static bool TryRead(Stream source, out Header header, out Endian endian)
            {
                endian = Endian.Little;
                header = source.Read<Header>();
                if (header.ChunkSize == 0 || header.ChunkCount == 0 || header.DecompressedSize == 0)
                    return false;

                if (!header.Validate())
                {
                    endian = Endian.Big;
                    header = header.ReverseEndianness();
                    if (!header.Validate())
                        return false;
                }
                return true;
            }

            public Header ReverseEndianness()
                => new Header(
                    BinaryPrimitives.ReverseEndianness(ChunkSize),
                    BinaryPrimitives.ReverseEndianness(ChunkCount),
                    BinaryPrimitives.ReverseEndianness(DecompressedSize));
        }
    }
}
