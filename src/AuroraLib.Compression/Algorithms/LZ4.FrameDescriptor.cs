using AuroraLib.Core.IO;
using System;
using System.Buffers.Binary;
using System.IO;

namespace AuroraLib.Compression.Algorithms
{
    public sealed partial class LZ4
    {
        private sealed class FrameDescriptor : IBinaryObject
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

                bytes[bytes.Length - 1] = (byte)((HashAlgorithm!.Invoke(bytes.Slice(0, bytes.Length - 1)) >> 8) & 0xFF);
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
