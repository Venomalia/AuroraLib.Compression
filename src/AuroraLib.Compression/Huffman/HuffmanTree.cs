using System;
using System.Collections.Generic;

namespace AuroraLib.Compression.Huffman
{
    public static class HuffmanTree
    {
        /// <summary>
        /// Builds a Huffman tree from the given input, where each symbol is represented by a specified number of bits (default 8 bits).
        /// </summary>
        /// <param name="input">A read-only span of bytes representing the data to be compressed using Huffman coding.</param>
        /// <param name="bitDepth">The number of bits per symbol (default is 8, which means each byte is a symbol).</param>
        /// <returns>The root node of the generated Huffman tree.</returns>
        public static HuffmanNode<int> Build(ReadOnlySpan<byte> input, int bitDepth = 8)
            => CreateTree(GetFrequencies(input, bitDepth));

        /// <summary>
        /// Builds a Huffman tree from the given input, where each element in the input span is treated as a symbol.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the input span, which will be treated as symbols in the Huffman tree.</typeparam>
        /// <param name="input">A read-only span of elements representing the data to be compressed using Huffman coding.</param>
        /// <returns>The root node of the generated Huffman tree.</returns>
        public static HuffmanNode<T> Build<T>(ReadOnlySpan<T> input)
            => CreateTree(GetFrequencies(input));

        private static List<HuffmanNode<int>> GetFrequencies(ReadOnlySpan<byte> input, int bitDepth)
        {
            if (bitDepth != 4 && bitDepth != 8)
                throw new ArgumentException($"bitDepth {bitDepth}");

            // Iterate through the input span, counting the frequency of each unique symbol.
            Span<uint> frequency = stackalloc uint[1 << bitDepth];
            if (bitDepth == 4)
            {
                foreach (byte item in input)
                {
                    frequency[item >> 4]++;
                    frequency[item & 0xF]++;
                }
            }
            else
            {
                foreach (byte item in input)
                    frequency[item]++;
            }

            // Convert each frequency into a Huffman node and add it to the list.
            List<HuffmanNode<int>> tree = new List<HuffmanNode<int>>(frequency.Length);
            for (int i = 0; i < frequency.Length; i++)
            {
                if (frequency[i] != 0)
                    tree.Add(new HuffmanNode<int>(i, frequency[i]));
            }
            return tree;
        }

        private static List<HuffmanNode<T>> GetFrequencies<T>(ReadOnlySpan<T> input)
        {
            // Iterate through the input span, counting the frequency of each unique symbol.
            Dictionary<T, uint> frequency = new Dictionary<T, uint>();
            foreach (T item in input)
            {
                if (frequency.TryGetValue(item, out uint value))
                    frequency[item] = ++value;
                else
                    frequency[item] = 1;
            }

            // Convert each dictionary entry (symbol and its frequency) into a Huffman node and add it to the list.
            List<HuffmanNode<T>> tree = new List<HuffmanNode<T>>();
            foreach (var entry in frequency)
            {
                tree.Add(new HuffmanNode<T>(entry.Key, entry.Value));
            }

            return tree;
        }

        /// <summary>
        /// Creates a Huffman tree from a collection of Huffman nodes.
        /// </summary>
        /// <typeparam name="T">The type of the value contained in the Huffman node.</typeparam>
        /// <param name="frequencies">A collection of Huffman nodes.</param>
        /// <returns>The root node of the resulting Huffman tree.</returns>
        public static HuffmanNode<T> CreateTree<T>(IEnumerable<HuffmanNode<T>> frequencies)
        {
            var frequenciesList = new List<HuffmanNode<T>>(frequencies);

            while (frequenciesList.Count > 1)
            {
                frequenciesList.Sort();

                var leastFrequencyNode1 = frequenciesList[0];
                frequenciesList.RemoveAt(0);

                var leastFrequencyNode2 = frequenciesList[0];
                frequenciesList.RemoveAt(0);

                var combinedNode = new HuffmanNode<T>(leastFrequencyNode1.Frequency + leastFrequencyNode2.Frequency, leastFrequencyNode1, leastFrequencyNode2);
                frequenciesList.Add(combinedNode);
            }
            return frequenciesList[0];
        }
    }
}
