using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
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
    public sealed class HWGZ : ICompressionAlgorithm
    {
        /// <summary>
        /// Defines the size of the chunks.
        /// </summary>
        public int ChunkSize = 0x10000;

        /// <inheritdoc/>
        public Endian ExplicitOrder { get; set; } = Endian.Big;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
        {
            if (stream == null || stream.Length <= 128)
                return false;

            uint chunkSize = stream.ReadUInt32();
            uint chunkCount = stream.ReadUInt32();
            uint decompressedSize = stream.ReadUInt32();

            if (chunkCount != (decompressedSize + chunkSize - 1) / chunkSize)
            {
                chunkSize = BinaryPrimitives.ReverseEndianness(chunkSize);
                chunkCount = BinaryPrimitives.ReverseEndianness(chunkCount);
                decompressedSize = BinaryPrimitives.ReverseEndianness(decompressedSize);
            }
            stream.Align(4 * chunkCount, SeekOrigin.Current, 128);
            return decompressedSize != 0 && chunkCount == (decompressedSize + chunkSize - 1) / chunkSize && stream.At(4, SeekOrigin.Current, s => s.Read<ZLib.Header>().Validate());
        }

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            long destStart = destination.Position;

            uint chunkSize = source.ReadUInt32(ExplicitOrder);
            uint chunkCount = source.ReadUInt32(ExplicitOrder);
            uint decompressedSize = source.ReadUInt32(ExplicitOrder);

            if (chunkCount != (decompressedSize + chunkSize - 1) / chunkSize)
            {
                chunkCount = BinaryPrimitives.ReverseEndianness(chunkCount);
                decompressedSize = BinaryPrimitives.ReverseEndianness(decompressedSize);
                ExplicitOrder = ExplicitOrder == Endian.Big ? Endian.Little : Endian.Big;
            }
            source.Align(4 * chunkCount, SeekOrigin.Current, 128);

            ZLib zLib = new ZLib();
            for (int i = 0; i < chunkCount; i++)
            {
                int chunkDataSize = source.ReadInt32(ExplicitOrder);
                zLib.Decompress(source, destination, chunkDataSize);
                source.Align(128);
            }

            if (destination.Position - destStart != decompressedSize)
            {
                throw new DecompressedSizeException(decompressedSize, destination.Position - destStart);
            }
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            long destStart = destination.Position;
            int chunkCount = (source.Length + ChunkSize - 1) / ChunkSize;
            uint[] chunkSizes = new uint[chunkCount];

            destination.Write(ChunkSize, ExplicitOrder);
            destination.Write(chunkCount, ExplicitOrder);
            destination.Write(source.Length, ExplicitOrder);
            destination.Write<uint>(chunkSizes, ExplicitOrder); // Placeholder

            destination.WriteAlign(128);

            ZLib zLib = new ZLib();
            using (MemoryPoolStream buffer = new MemoryPoolStream(ChunkSize))
            {
                for (int i = 0; i < chunkCount; i++)
                {
                    int segmentStart = i * ChunkSize;
                    int segmentSize = Math.Min(ChunkSize, source.Length - segmentStart);

                    buffer.SetLength(0);
                    zLib.Compress(source.Slice(segmentStart, segmentSize), buffer, level);

                    ReadOnlySpan<byte> segmentData = buffer.UnsaveAsSpan();
                    chunkSizes[i] = (uint)(segmentData.Length + 4);
                    destination.Write(segmentData.Length, ExplicitOrder);
                    destination.Write(segmentData);
                    destination.WriteAlign(128);
                }
            }
            destination.At(destStart + 12, s => s.Write<uint>(chunkSizes, ExplicitOrder));
        }
    }
}
