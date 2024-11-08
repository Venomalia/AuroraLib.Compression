using AuroraLib.Compression.Huffman;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Core;
using AuroraLib.Core.Buffers;
using AuroraLib.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo variant of the Huffman compression algorithm, mainly used in GBA and DS games.
    /// </summary>
    public sealed class HUF20 : ICompressionAlgorithm
    {
        /// <summary>
        /// Specifies the type of Huffman compression used.
        /// </summary>
        public CompressionType Type = CompressionType.Huffman8bits;

        public enum CompressionType : byte
        {
            Huffman4bits = 0x24,
            Huffman8bits = 0x28,
        }

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && Enum.IsDefined(typeof(CompressionType), stream.Read<CompressionType>()) && (stream.ReadUInt24() != 0 || stream.ReadUInt32() != 0) && stream.ReadByte() != 0 && stream.ReadByte() != 0;

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            Type = (CompressionType)source.ReadByte();
            int uncompressedSize = source.ReadUInt24();
            if (uncompressedSize == 0) uncompressedSize = (int)source.ReadUInt32();
            DecompressHeaderless(source, destination, uncompressedSize, (int)Type - 0x20, Endian.Little);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            if (source.Length <= 0xFFFFFF)
            {
                destination.Write((byte)Type | (source.Length << 8));
            }
            else
            {
                destination.Write((byte)Type | 0);
                destination.Write(source.Length);
            }
            CompressHeaderless(source, destination, (int)Type - 0x20, Endian.Little);
        }

        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength, int bitDepth, Endian order = Endian.Little)
        {
            byte treeSize = source.ReadUInt8();
            byte treeRoot = source.ReadUInt8();
            Span<byte> tree = stackalloc byte[treeSize * 2];
            source.Read(tree);

            using (SpanBuffer<byte> bufferPool = new SpanBuffer<byte>(decomLength))
            {
                DecompressHeaderless(source, bufferPool, tree, treeRoot, bitDepth, order);
                destination.Write(bufferPool.GetBuffer(), 0, decomLength);
            }
        }

        public static void DecompressHeaderless(Stream source, Span<byte> destination, ReadOnlySpan<byte> tree, byte treeRoot, int bitDepth, Endian order = Endian.Little)
        {
            int flag = 0, next = 0, treePos = treeRoot, i = 0, bitsRemaining = 0, symbolsToDecompress = destination.Length * 8 / bitDepth;

            // The target buffer must be clean if we have to write nibble.
            if (bitDepth != 8)
                destination.Clear();

            while (i < symbolsToDecompress)
            {
                if (bitsRemaining == 0)
                {
                    flag = source.ReadInt32();
                    bitsRemaining = 32;
                }

                next += ((treePos & 0x3F) << 1) + 2;
                int direction = 2 - ((flag >> --bitsRemaining) & 1); // 1 or 2
                bool leaf = ((treePos >> (5 + direction)) & 1) != 0;

                treePos = tree[next - direction];
                if (leaf)
                {
                    if (bitDepth == 8)
                    {
                        destination[i++] = (byte)treePos;
                    }
                    else
                    {
                        bool shift = (i & 1) == 0 ^ (order == Endian.Little);
                        destination[i++ / 2] |= (byte)(treePos << (shift ? 4 : 0));
                    }
                    treePos = treeRoot;
                    next = 0;
                }
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, int bitDepth, Endian order = Endian.Little)
        {
            HuffmanNode<int> tree = HuffmanTree.Build(source, bitDepth);
            List<HuffmanNode<int>> labelList = BuildLabelTreeList(tree);

            destination.WriteByte((byte)labelList.Count); // Write Tree Size 
            destination.WriteByte((byte)labelList[0].Value); // Write Tree Root
            foreach (var node in labelList) // Write Tree
            {
                destination.WriteByte((byte)node.Children.Left!.Value);
                destination.WriteByte((byte)node.Children.Right!.Value);
            }

            CompressHeaderless(source, destination, tree, bitDepth, order);
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, HuffmanNode<int> tree, int bitDepth, Endian order = Endian.Little)
        {
            Dictionary<int, string> bitCodes = tree.GetCodeDictionary();
            using (FlagWriter Flag = new FlagWriter(destination, Endian.Big, 0, 4, Endian.Little))
            {
                if (bitDepth == 8)
                {
                    foreach (byte data in source)
                        foreach (var bit in bitCodes[data])
                            Flag.WriteBit(bit == '1');
                }
                else
                {
                    foreach (byte data in source)
                    {
                        foreach (var bit in bitCodes[order == Endian.Little ? data & 0xF : data >> 4])
                            Flag.WriteBit(bit == '1');
                        foreach (var bit in bitCodes[order == Endian.Little ? data >> 4 : data & 0xF])
                            Flag.WriteBit(bit == '1');
                    }
                }
            }
        }

        private static List<HuffmanNode<int>> BuildLabelTreeList(HuffmanNode<int> rootNode)
        {
            var labelList = new List<HuffmanNode<int>>();
            var frequencies = new List<HuffmanNode<int>> { rootNode };

            while (frequencies.Count > 0)
            {
                HuffmanNode<int> node = frequencies
                    .Select((freq, i) => new { Node = freq, Score = freq.Value - i })
                    .OrderBy(freq => freq.Score)
                    .First().Node;

                frequencies.Remove(node);

                node.Value = labelList.Count - node.Value;
                labelList.Add(node);

                // Loop through all children that aren't leaves
                if (node.Children.Left!.IsLeaf)
                {
                    node.Value |= 0x80;
                }
                else
                {
                    node.Children.Left.Value = labelList.Count;
                    frequencies.Add(node.Children.Left);
                }
                if (node.Children.Right!.IsLeaf)
                {
                    node.Value |= 0x40;
                }
                else
                {
                    node.Children.Right.Value = labelList.Count;
                    frequencies.Add(node.Children.Right);
                }
            }
            return labelList;
        }
    }
}
