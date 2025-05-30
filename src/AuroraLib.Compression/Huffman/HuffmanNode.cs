using System;
using System.Collections.Generic;

namespace AuroraLib.Compression.Huffman
{
    /// <summary>
    /// Represents a node in a Huffman tree, used in the Huffman algorithm.
    /// </summary>
    /// <typeparam name="T">The type of the value contained in the Huffman node.</typeparam>
    public sealed class HuffmanNode<T> : IComparable<HuffmanNode<T>> where T : notnull
    {
        /// <summary>
        /// The value stored in the node.
        /// </summary>
        public T Value;

        /// <summary>
        /// The frequency of the value.
        /// </summary>
        public uint Frequency;

        /// <summary>
        /// The left and right children of the node.
        /// </summary>
        public (HuffmanNode<T>? Left, HuffmanNode<T>? Right) Children { get; set; }

        /// <summary>
        /// Gets a value indicating whether the node is a leaf node.
        /// </summary>
        public bool IsLeaf => Children.Left == null;

        public HuffmanNode(T vaule, uint frequency)
        {
            Value = vaule;
            Frequency = frequency;
        }

        public HuffmanNode(uint frequency, HuffmanNode<T> childrenLeft, HuffmanNode<T> childrenRight)
        {
            Frequency = frequency;
            Children = (childrenLeft, childrenRight);
        }

        public Dictionary<T, (int code, short len)> GetCodeDictionary()
        {
            var result = new Dictionary<T, (int, short)>();
            BuildCodeDictionary(0, 0, result);
            return result;
        }

        private void BuildCodeDictionary(int code, short len, Dictionary<T, (int code, short len)> result)
        {
            if (IsLeaf)
            {
                result.Add(Value, (code, len));
            }
            else
            {
                len++;
                Children.Left!.BuildCodeDictionary((code << 1), len, result);
                Children.Right!.BuildCodeDictionary((code << 1) | 1, len, result);
            }
        }

        /// <inheritdoc/>
        public int CompareTo(HuffmanNode<T>? other)
            => other == null ? 1 : Frequency.CompareTo(other.Frequency);
    }
}
