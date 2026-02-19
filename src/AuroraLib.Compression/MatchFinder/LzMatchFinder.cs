using AuroraLib.Core.Collections;
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
        private readonly int _minDistance;
        private readonly int _windowsSize;
        private readonly int _goodMatch;
        private readonly bool _lookAhead;

        private readonly LzProperties[] _lzProperties;

        private LZMatchFinder(LzProperties[] lzProperties, int maxWindowsSize, bool lookAhead = true)
        {
            _lzProperties = lzProperties;
            _lookAhead = lookAhead;
            _windowsSize = maxWindowsSize;
            _minMatchLength = int.MaxValue;
            _maxMatchLength = 0;
            _minDistance = int.MaxValue;
            foreach (var prop in _lzProperties)
            {
                if (_minMatchLength > prop.MinLength)
                    _minMatchLength = prop.MinLength;

                if (_maxMatchLength < prop.MaxLength)
                    _maxMatchLength = prop.MaxLength;

                if (_minDistance > prop.MinDistance)
                    _minDistance = prop.MinDistance;
            }
        }

        private LZMatchFinder(int windowsSize, int maxLength, int minLength = 3, bool lookAhead = true, int minDistance = 1)
        {
            _lookAhead = lookAhead;
            _minMatchLength = minLength;
            _maxMatchLength = maxLength;
            _windowsSize = windowsSize;
            _minDistance = minDistance;
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
        public static PoolList<LzMatch> FindMatchesParallel(ReadOnlySpan<byte> source, LzProperties lz, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal, int blockSize = 0x8000)
            => FindMatchesParallel(source, lz, lz.GetWindowsLevel(level), lookAhead, blockSize);

        public static PoolList<LzMatch> FindMatchesParallel(ReadOnlySpan<byte> source, LzProperties lz, int maxWindowsSize, bool lookAhead = true, int blockSize = 0x8000)
        {
            if (maxWindowsSize <= 0)
                return new PoolList<LzMatch>();

            LZMatchFinder finder = new LZMatchFinder(maxWindowsSize, lz.MaxLength, lz.MinLength, lookAhead, lz.MinDistance);

            fixed (byte* dataPtr = source)
                return finder.FindMatches(dataPtr, source.Length, blockSize);
        }

        public static PoolList<LzMatch> FindMatchesParallel(ReadOnlySpan<byte> source, LzProperties[] lzs, int maxWindowsSize, bool lookAhead = true, int blockSize = 0x8000)
        {
            if (maxWindowsSize <= 0)
                return new PoolList<LzMatch>();

            LZMatchFinder finder = new LZMatchFinder(lzs, maxWindowsSize, lookAhead);

            fixed (byte* dataPtr = source)
                return finder.FindMatches(dataPtr, source.Length, blockSize);
        }

#if DEBUG
        /// <inheritdoc cref="FindMatchesParallel(ReadOnlySpan{byte}, LzProperties, bool, CompressionLevel, int)"/>
        public static PoolList<LzMatch> FindMatches(ReadOnlySpan<byte> source, LzProperties lz, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            if (level == CompressionLevel.NoCompression)
                return new PoolList<LzMatch>();

            LZMatchFinder finder = new LZMatchFinder(lz.GetWindowsLevel(level), lz.MaxLength, lz.MinLength, lookAhead, lz.MinDistance);
            return finder.FindMatches(source);
        }

        internal PoolList<LzMatch> FindMatches(ReadOnlySpan<byte> data)
        {
            var matchResults = new PoolList<LzMatch>();
            fixed (byte* dataPtr = data)
                FindMatchesInBlock(dataPtr, data.Length, 0, data.Length, matchResults);
            return matchResults;
        }
#endif

        internal PoolList<LzMatch> FindMatches(byte* data, int length, int blockSize)
        {
            int numberOfBlocks = Math.Max(1, (length + blockSize - 1) / blockSize);
            PoolList<LzMatch>[] blockMatches = new PoolList<LzMatch>[numberOfBlocks];

            // Process each block in parallel
            ParallelLoopResult result = Parallel.For(0, numberOfBlocks, blockIndex =>
            {
                var lzMatches = new PoolList<LzMatch>();
                int start = blockIndex * blockSize;
                int end = Math.Min(start + blockSize, length);

                FindMatchesInBlock(data, length, start, end, lzMatches);
                blockMatches[blockIndex] = lzMatches;
            });

            PoolList<LzMatch> matchResults = blockMatches[0];
            matchResults.SetMinimumCapacity(blockMatches.Sum(list => list.Count));

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
                    blockMatches[i].Dispose();
                }
            }
            else
            {
                for (int i = 1; i < blockMatches.Length; i++)
                {
                    matchResults.AddRange(blockMatches[i]);
                    blockMatches[i].Dispose();
                }
            }

            return matchResults;
        }

        internal void FindMatchesInBlock(byte* dataPtr, int length, int start, int end, IList<LzMatch> lzMatches)
        {
            if (start == 0)
                start = _lookAhead ? 1 : _minMatchLength;

            for (int i = start; i < end - 3; i++)
            {
                int matchStart = Math.Max(0, i - (_windowsSize));
                int maxBestLength = _lookAhead
                    ? Math.Min(_maxMatchLength, length - i)
                    : Math.Min(Math.Min(_maxMatchLength, end - i), i);

                int bestLength = 0, bestDistance = 0;

                for (int j = i - _minDistance; j >= matchStart; j--)
                {
                    // Use ushort comparison for the first two bytes for faster matching
                    if (*(ushort*)(dataPtr + i) == *(ushort*)(dataPtr + j))
                    {
                        int currentDistance = i - j;
                        int currentLength = 2;
                        int currentBestLength = _lookAhead ? maxBestLength : Math.Min(maxBestLength, currentDistance);
                        // Check additional bytes for a match
                        while (currentLength < currentBestLength && dataPtr[i + currentLength] == dataPtr[j + currentLength])
                            currentLength++;

                        // Update if a better match is found
                        if (currentLength > bestLength && IsValidMatch(ref currentLength, currentDistance))
                        {
                            bestLength = currentLength;
                            bestDistance = currentDistance;

                            // Stop if the maximum match length is reached
                            if (bestLength >= maxBestLength)
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

        private bool IsValidMatch(ref int length, int distance)
        {
            if (_lzProperties == null)
                return true;

            foreach (var propertie in _lzProperties)
            {
                if (distance <= propertie.WindowsSize && length >= propertie.MinLength && distance >= propertie.MinDistance)
                {
                    if (length > propertie.MaxLength)
                        length = propertie.MaxLength;
                    return true;
                }
            }
            return false;
        }
    }
}
