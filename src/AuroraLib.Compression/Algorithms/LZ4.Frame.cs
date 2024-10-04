using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.IO;
using AuroraLib.Core.Buffers;
using AuroraLib.Core.Interfaces;
using AuroraLib.Core.IO;
using System;
using System.Buffers.Binary;
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
        public static HashCalculator HashAlgorithm;
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
                Console.Error.WriteLine("Warning: Hash algorithm is not defined. Skipping LZ4 content checksum verification.");
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
            /// Skippable frames allow the integration of user-defined data, these are skipped by the decoder. 
            /// </summary>
            Skippable0 = 0x184D2A50,
            Skippable1 = 0x184D2A51,
            Skippable2 = 0x184D2A52,
            Skippable3 = 0x184D2A53,
            Skippable4 = 0x184D2A54,
            Skippable5 = 0x184D2A55,
            Skippable6 = 0x184D2A56,
            Skippable7 = 0x184D2A57,
            Skippable8 = 0x184D2A58,
            Skippable9 = 0x184D2A59,
            SkippableA = 0x184D2A5A,
            SkippableB = 0x184D2A5B,
            SkippableC = 0x184D2A5C,
            SkippableD = 0x184D2A5D,
            SkippableE = 0x184D2A5E,
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
            FrameDescriptor descriptor = source.Read<FrameDescriptor>();

            if (descriptor.Flags.HasFlag(FrameDescriptorFlags.HasDictID))
                throw new NotSupportedException("External dictionaries are currently not supported");

            SpanBuffer<byte> buffer = new SpanBuffer<byte>((int)descriptor.BlockMaxSize);
            using (LzWindows windows = new LzWindows(destination, _lz.WindowsSize))
            {
                // Decode Blocks
                while (true)
                {
                    uint blockSize = source.ReadUInt32();
                    // Check for EndMark
                    if (blockSize == 0x0)
                        break;

                    // Check if the block is uncompressed (highest bit = 1)
                    bool isUncompressed = (blockSize & UncompressedFlag) != 0;
                    Span<byte> blockBuffer = buffer.Slice(0, (int)(blockSize & (UncompressedFlag - 1)));

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
                    contentBuffer = mps.UnsaveAsSpan((int)destStartPos);
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
                throw new AggregateException();

            if (Flags.HasFlag(FrameDescriptorFlags.HasDictID))
                throw new NotSupportedException("External dictionaries are currently not supported");

            Flags &= FrameDescriptorFlags.IsVersion1;

            FrameDescriptor descriptor = new FrameDescriptor() { Flags = Flags, BlockMaxSize = BlockSize, ContentSize = destination.Length };
            destination.Write(descriptor);


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
                        blockData = buffer.UnsaveAsSpan();
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

        private class FrameDescriptor : IBinaryObject
        {
            public FrameDescriptorFlags Flags;
            public BlockMaxSizes BlockMaxSize;
            public long ContentSize;
            public uint DictionaryID;
            public byte HeaderChecksum;

            public void BinaryDeserialize(Stream source)
            {
                Flags = source.Read<FrameDescriptorFlags>();
                byte BD = source.ReadUInt8();
                BlockMaxSize = GetBlockMaxSize(BD);
                ContentSize = Flags.HasFlag(FrameDescriptorFlags.HasContentSize) ? source.ReadInt64() : 0;
                DictionaryID = Flags.HasFlag(FrameDescriptorFlags.HasDictID) ? source.ReadUInt32() : 0;
                HeaderChecksum = source.ReadUInt8();
            }

            public void BinarySerialize(Stream dest)
            {
                Span<byte> bytes = stackalloc byte[GetDescriptorSize(Flags)];
                BinarySerialize(bytes);
                dest.Write(bytes);
            }

            private void BinarySerialize(Span<byte> bytes)
            {
                bytes[0] = (byte)(Flags | FrameDescriptorFlags.IsVersion1);
                bytes[1] = BuildBDByte();
                if (Flags.HasFlag(FrameDescriptorFlags.HasContentSize))
                    BinaryPrimitives.WriteInt64LittleEndian(bytes.Slice(2), ContentSize);
                if (Flags.HasFlag(FrameDescriptorFlags.HasDictID))
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.Slice(bytes.Length - 5), DictionaryID);

                bytes[bytes.Length - 1] = (byte)((HashAlgorithm.Invoke(bytes.Slice(0, bytes.Length - 1)) >> 8) & 0xFF);
            }

            private BlockMaxSizes GetBlockMaxSize(byte BD)
            {
                // Extract block size bits (bits 4-6 of BD byte)
                switch ((BD & 0x70) >> 4)
                {
                    case 4:
                        return BlockMaxSizes.Block64KB;
                    case 5:
                        return BlockMaxSizes.Block256KB;
                    case 6:
                        return BlockMaxSizes.Block1MB;
                    case 7:
                        return BlockMaxSizes.Block4MB;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(BD), "Invalid BlockMaxSize in BD byte");
                }
            }

            private byte BuildBDByte()
            {
                switch (BlockMaxSize)
                {
                    case BlockMaxSizes.Block64KB:
                        return 0x40;
                    case BlockMaxSizes.Block256KB:
                        return 0x50;
                    case BlockMaxSizes.Block1MB:
                        return 0x60;
                    case BlockMaxSizes.Block4MB:
                        return 0x70;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(BlockMaxSize), "Invalid BlockMaxSize value");
                }
            }

            private static int GetDescriptorSize(FrameDescriptorFlags flag)
            {
                int size = 3;
                if ((flag & FrameDescriptorFlags.HasContentSize) != 0)
                    size += 8;
                if ((flag & FrameDescriptorFlags.HasDictID) != 0)
                    size += 4;
                return size;
            }
        }
    }
}
