using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.IO;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    public sealed partial class LZ4
    {
        private const uint UncompressedFlag = 0x80000000;

        /// <summary>
        /// Gets or sets the flags for the frame descriptor when writing a <see cref="FrameTypes.LZ4FrameHeader"/>.
        /// </summary>
        public FrameDescriptorFlags Flags = FrameDescriptorFlags.IsVersion1 | FrameDescriptorFlags.HasContentSize;

        /// <summary>
        /// The hash algorithm to be used for the <see cref="FrameTypes.LZ4FrameHeader"/>, normally xxHash-32.
        /// Is required to write and verify checksums in the <see cref="FrameTypes.LZ4FrameHeader"/> format.
        /// </summary>
        public static HashCalculator? HashAlgorithm;
        public delegate uint HashCalculator(ReadOnlySpan<byte> bytes);

        private static void CheckChecksum(Stream source, Span<byte> buffer)
        {
            uint Checksum = source.ReadUInt32();
            if (HashAlgorithm != null)
            {
                uint computedChecksum = HashAlgorithm.Invoke(buffer);
                if (Checksum != computedChecksum)
                    throw new InvalidDataException($"Checksum mismatch: expected {Checksum}, but calculated {computedChecksum}.");
            }
            else
            {
                Trace.WriteLine("Warning: Hash algorithm is not defined. Skipping LZ4 content checksum verification.");
            }
        }

        public enum BlockMaxSizes : int
        {
            Block64KB = 0x10000,
            Block256KB = 0x40000,
            Block1MB = 0x100000,
            Block4MB = 0x400000
        }

        public enum FrameTypes : uint
        {
            /// <summary>
            /// The Legacy frame format was defined into the initial versions of LZ4.
            /// </summary>
            Legacy = 0x184C2102,
            /// <summary>
            /// LZ4 Frame Header used since version 1.
            /// </summary>
            LZ4FrameHeader = 0x184D2204,
            /// <summary>
            /// Skippable frame allow the integration of user-defined data, these are skipped by the decoder. 
            /// </summary>
            Skippable0 = 0x184D2A50,
            /// <inheritdoc cref="Skippable0"/>
            Skippable1 = 0x184D2A51,
            /// <inheritdoc cref="Skippable0"/>
            Skippable2 = 0x184D2A52,
            /// <inheritdoc cref="Skippable0"/>
            Skippable3 = 0x184D2A53,
            /// <inheritdoc cref="Skippable0"/>
            Skippable4 = 0x184D2A54,
            /// <inheritdoc cref="Skippable0"/>
            Skippable5 = 0x184D2A55,
            /// <inheritdoc cref="Skippable0"/>
            Skippable6 = 0x184D2A56,
            /// <inheritdoc cref="Skippable0"/>
            Skippable7 = 0x184D2A57,
            /// <inheritdoc cref="Skippable0"/>
            Skippable8 = 0x184D2A58,
            /// <inheritdoc cref="Skippable0"/>
            Skippable9 = 0x184D2A59,
            /// <inheritdoc cref="Skippable0"/>
            SkippableA = 0x184D2A5A,
            /// <inheritdoc cref="Skippable0"/>
            SkippableB = 0x184D2A5B,
            /// <inheritdoc cref="Skippable0"/>
            SkippableC = 0x184D2A5C,
            /// <inheritdoc cref="Skippable0"/>
            SkippableD = 0x184D2A5D,
            /// <inheritdoc cref="Skippable0"/>
            SkippableE = 0x184D2A5E,
            /// <inheritdoc cref="Skippable0"/>
            SkippableF = 0x184D2A5F,
        }

        [Flags]
        public enum FrameDescriptorFlags : byte
        {
            HasDictID = 1,
            HasContentChecksum = 4,
            HasContentSize = 8,
            HasBlockChecksum = 16,
            IsBlockIndependence = 32,
            IsVersion1 = 64,
        }

        private void DecompressLZ4FrameHeader(Stream source, Stream destination)
        {
            long destStartPos = destination.Position;
            FrameDescriptor descriptor = new FrameDescriptor();
            descriptor.BinaryDeserialize(source);

            if (descriptor.Flags.HasFlag(FrameDescriptorFlags.HasDictID))
                throw new NotSupportedException("External dictionaries are currently not supported");


            byte[] buffer = ArrayPool<byte>.Shared.Rent((int)descriptor.BlockMaxSize);
            try
            {
                using LzWindows windows = new LzWindows(destination, _lz.WindowsSize);
                // Decode Blocks
                while (true)
                {
                    uint blockSize = source.ReadUInt32();
                    // Check for EndMark
                    if (blockSize == 0x0)
                        break;

                    // Check if the block is uncompressed (highest bit = 1)
                    bool isUncompressed = (blockSize & UncompressedFlag) != 0;
                    Span<byte> blockBuffer = buffer.AsSpan(0, (int)(blockSize & (UncompressedFlag - 1)));

                    // Read the block content into the buffer
                    source.Read(blockBuffer);

                    // Check the block checksum, if available.
                    if (descriptor.Flags.HasFlag(FrameDescriptorFlags.HasBlockChecksum))
                        CheckChecksum(source, blockBuffer);

                    // If the block is uncompressed, write it directly to the destination
                    if (isUncompressed)
                        windows.Write(blockBuffer);
                    else
                        DecompressBlockHeaderless(blockBuffer, windows);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            // Check the content size, if available.
            if (descriptor.Flags.HasFlag(FrameDescriptorFlags.HasContentSize) && destination.Position != destStartPos + descriptor.ContentSize)
            {
                throw new DecompressedSizeException(descriptor.ContentSize, destination.Position + destStartPos);
            }

            // Check the block checksum, if available.
            if (descriptor.Flags.HasFlag(FrameDescriptorFlags.HasContentChecksum))
            {
                Span<byte> contentBuffer;
                if (destination is MemoryPoolStream mps)
                    contentBuffer = mps.UnsafeAsSpan((int)destStartPos);
                else if (destination is MemoryStream ms)
                    contentBuffer = ms.GetBuffer().AsSpan((int)destStartPos);
                else
                {
                    contentBuffer = new byte[descriptor.ContentSize];
                    destination.Position = destStartPos;
                    destination.Read(contentBuffer);
                }
                CheckChecksum(source, contentBuffer);
            }
        }

        private void CompressLZ4FrameHeader(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            if (HashAlgorithm == null)
                throw new ArgumentException("Requires a hash function.", nameof(HashAlgorithm));

            if (Flags.HasFlag(FrameDescriptorFlags.HasDictID))
                throw new NotSupportedException("External dictionaries are currently not supported");

            Flags &= FrameDescriptorFlags.IsVersion1;

            FrameDescriptor descriptor = new FrameDescriptor() { Flags = Flags, BlockMaxSize = BlockSize, ContentSize = destination.Length };
            descriptor.BinarySerialize(destination);

            int sourcePointer = 0x0;
            using (MemoryPoolStream buffer = new MemoryPoolStream((int)BlockSize))
            {
                while (sourcePointer != source.Length)
                {
                    buffer.SetLength(0);

                    ReadOnlySpan<byte> blockData = source.Slice(sourcePointer, Math.Min((int)BlockSize, source.Length - sourcePointer));
                    CompressBlockHeaderless(blockData, buffer, LookAhead, level);
                    sourcePointer += blockData.Length;

                    if (buffer.Position >= (int)BlockSize)
                    {
                        // Write Block Size Uncompressed
                        destination.Write((uint)blockData.Length | UncompressedFlag);
                    }
                    else
                    {
                        // Write Block Size Compressed
                        destination.Write((uint)buffer.Position);
                        blockData = buffer.UnsafeAsSpan();
                    }

                    // Write Block Data
                    destination.Write(blockData);

                    // Write Block Content Checksum, if required.
                    if (descriptor.Flags.HasFlag(FrameDescriptorFlags.HasBlockChecksum))
                        destination.Write(HashAlgorithm.Invoke(blockData));
                }

                // Write EndMark
                destination.Write(0);

                // Write Content Checksum, if required.
                if (descriptor.Flags.HasFlag(FrameDescriptorFlags.HasContentChecksum))
                    destination.Write(HashAlgorithm.Invoke(source));
            }
        }
    }
}
