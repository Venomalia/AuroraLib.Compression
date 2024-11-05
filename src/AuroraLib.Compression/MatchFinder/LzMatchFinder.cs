using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace AuroraLib.Compression.MatchFinder
{
    /// <summary>
    /// A class for finding LZ compression matches within a given sequence.
    /// </summary>
    public unsafe sealed class LZMatchFinder
    {
        // Properties related to the LZ compression parameters
        private readonly int _minMatchLength;
        private readonly int _maxMatchLength;
        private readonly int _windowsSize;
        private readonly bool _lookAhead;

        private LZMatchFinder(int windowsSize, int maxLength, int minLength = 3, bool lookAhead = true)
        {
            _lookAhead = lookAhead;
            _minMatchLength = minLength;
            _maxMatchLength = maxLength;
            _windowsSize = windowsSize;
        }

        /// <summary>
        /// Finds compression matches within the specified source data.
        /// </summary>
        /// <param name="source">The byte data in which to find matches.</param>
        /// <param name="lz">LZ compression properties, such as window size and match lengths.</param>
        /// <param name="lookAhead">Whether to use look-ahead optimization.</param>
        /// <param name="level">Compression level, affecting match sensitivity.</param>
        /// <param name="blockSize">Size of each block to process in parallel.</param>
        /// <returns>A list of matches found in the source data.</returns>
        public static List<LzMatch> FindMatchesParallel(ReadOnlySpan<byte> source, LzProperties lz, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal, int blockSize = 0x8000)
        {
            if (level == CompressionLevel.NoCompression)
                return new List<LzMatch>();

            lz = lz.SetLevel(level);
            LZMatchFinder finder = new LZMatchFinder(lz.WindowsSize, lz.MaxLength, lz.MinLength, lookAhead);

            fixed (byte* dataPtr = source)
                return finder.FindMatches(dataPtr, source.Length, blockSize);
        }

        /// <inheritdoc cref="FindMatchesParallel(ReadOnlySpan{byte}, LzProperties, bool, CompressionLevel, int)"/>
        public static List<LzMatch> FindMatches(ReadOnlySpan<byte> source, LzProperties lz, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            if (level == CompressionLevel.NoCompression)
                return new List<LzMatch>();

            lz = lz.SetLevel(level);
            LZMatchFinder finder = new LZMatchFinder(lz.WindowsSize, lz.MaxLength, lz.MinLength, lookAhead);
            return finder.FindMatches(source);
        }

        internal List<LzMatch> FindMatches(ReadOnlySpan<byte> data)
        {
            var matchResults = new List<LzMatch>();
            fixed (byte* dataPtr = data)
                FindMatchesInBlock(dataPtr, data.Length, 0, data.Length, matchResults);
            return matchResults;
        }

        internal List<LzMatch> FindMatches(byte* data, int length, int blockSize)
        {
            int numberOfBlocks = Math.Max(1, (length + blockSize - 1) / blockSize);
            List<LzMatch>[] blockMatches = new List<LzMatch>[numberOfBlocks];

            // Process each block in parallel
            ParallelLoopResult result = Parallel.For(0, numberOfBlocks, blockIndex =>
            {
                var lzMatches = new List<LzMatch>();
                int start = blockIndex * blockSize;
                int end = Math.Min(start + blockSize, length);
                FindMatchesInBlock(data, length, start, end, lzMatches);
                blockMatches[blockIndex] = lzMatches;
            });

            List<LzMatch> matchResults = blockMatches[0];
            matchResults.Capacity = blockMatches.Sum(list => list.Count);

            // Handle overlapping matches between blocks if necessary
            if (_lookAhead || blockSize < _maxMatchLength)
            {
                LzMatch last = matchResults.LastOrDefault();
                for (int i = 1; i < blockMatches.Length; i++)
                {
                    LzMatch first = blockMatches[i].FirstOrDefault();
                    if (last.Offset + last.Length > first.Offset)
                    {
                        int diff = last.Offset + last.Length - first.Offset;
                        if (last.Distance == first.Distance && last.Length + first.Length - diff < _maxMatchLength)
                        {
                            matchResults[matchResults.Count - 1] = new LzMatch(last.Offset, first.Distance, last.Length + first.Length - diff);
                            blockMatches[i].RemoveAt(0);
                        }
                        else if (first.Length - diff >= _minMatchLength)
                        {
                            first = blockMatches[i][0] = new LzMatch(first.Offset + diff, first.Distance, first.Length - diff);
                        }
                        else
                        {
                            blockMatches[i].RemoveAt(0);
                        }
                    }
                    else if (last.Distance == first.Distance && last.Length + first.Length < _maxMatchLength && last.Offset + last.Length == first.Offset)
                    {
                        matchResults[matchResults.Count - 1] = new LzMatch(last.Offset, first.Distance, last.Length + first.Length);
                        blockMatches[i].RemoveAt(0);
                    }
                    matchResults.AddRange(blockMatches[i]);
                    last = matchResults.Last();
                }
            }
            else
            {
                for (int i = 1; i < blockMatches.Length; i++)
                    matchResults.AddRange(blockMatches[i]);
            }

            return matchResults;
        }

        internal void FindMatchesInBlock(byte* dataPtr, int length, int start, int end, List<LzMatch> lzMatches)
        {
            if (start == 0)
                start = _lookAhead ? 1 : _minMatchLength;

            for (int i = start; i < end; i++)
            {
                int matchStart = Math.Max(0, i - _windowsSize);
                int maxBestLength = _lookAhead
                    ? Math.Min(_maxMatchLength, length - i)
                    : Math.Min(Math.Min(_maxMatchLength, end - i), i);

                int bestLength = 0, bestDistance = 0;

                for (int j = i - 1; j >= matchStart; j--)
                {
                    // Use ushort comparison for the first two bytes for faster matching
                    if (*(ushort*)(dataPtr + i) == *(ushort*)(dataPtr + j))
                    {
                        int currentLength = 2;
                        int currentBestLength = _lookAhead ? maxBestLength : Math.Min(maxBestLength, i - j);
                        // Check additional bytes for a match
                        while (currentLength < currentBestLength && dataPtr[i + currentLength] == dataPtr[j + currentLength])
                            currentLength++;

                        // Update if a better match is found
                        if (currentLength > bestLength)
                        {
                            bestLength = currentLength;
                            bestDistance = i - j;

                            // Stop if the maximum match length is reached
                            if (bestLength == maxBestLength)
                                break;
                        }
                    }
                }

                // If a match is found, add it to the list
                if (bestLength >= _minMatchLength)
                {
                    lzMatches.Add(new LzMatch(i, bestDistance, bestLength));
                    i += bestLength - 1;
                }
            }
        }
    }
}
