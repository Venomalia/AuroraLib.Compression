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
        private const int BlockSize = 0x8000;

        // Properties related to the LZ compression parameters
        private readonly int _minMatchLength;
        private readonly int _maxMatchLength;
        private readonly int _minDistance;
        private readonly int _windowsSize;
        private readonly int _goodMatch;
        private readonly bool _lookAhead;
        private readonly bool _lazyMatch;

        private readonly LzProperties[] _lzProperties;

        private LZMatchFinder(LzProperties[] lzProperties, int maxWindowsSize, bool lookAhead = true, bool lazyMatch = true)
        {
            _lzProperties = lzProperties;
            _lookAhead = lookAhead;
            _lazyMatch = lazyMatch;
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

        private LZMatchFinder(int windowsSize, int maxLength, int minLength = 3, bool lookAhead = true, bool lazyMatch = true, int minDistance = 1)
        {
            _lookAhead = lookAhead;
            _lazyMatch = lazyMatch;
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
        public static PoolList<LzMatch> FindMatchesParallel(ReadOnlySpan<byte> source, LzProperties lz, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
            => FindMatchesParallel(source, lz, lz.GetWindowsLevel(level), lookAhead, level != CompressionLevel.Fastest);

        public static PoolList<LzMatch> FindMatchesParallel(ReadOnlySpan<byte> source, LzProperties lz, int maxWindowsSize, bool lookAhead = true, bool lazyMatch = true)
        {
            if (maxWindowsSize <= 0)
                return new PoolList<LzMatch>();

            LZMatchFinder finder = new LZMatchFinder(maxWindowsSize, lz.MaxLength, lz.MinLength, lookAhead, lazyMatch, lz.MinDistance);

            fixed (byte* dataPtr = source)
                return finder.FindMatches(dataPtr, source.Length, BlockSize);
        }

        public static PoolList<LzMatch> FindMatchesParallel(ReadOnlySpan<byte> source, LzProperties[] lzs, int maxWindowsSize, bool lookAhead = true, bool lazyMatch = true)
        {
            if (maxWindowsSize <= 0)
                return new PoolList<LzMatch>();

            LZMatchFinder finder = new LZMatchFinder(lzs, maxWindowsSize, lookAhead);

            fixed (byte* dataPtr = source)
                return finder.FindMatches(dataPtr, source.Length, BlockSize);
        }

#if DEBUG
        /// <inheritdoc cref="FindMatchesParallel(ReadOnlySpan{byte}, LzProperties, bool, CompressionLevel, int)"/>
        public static PoolList<LzMatch> FindMatches(ReadOnlySpan<byte> source, LzProperties lz, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            if (level == CompressionLevel.NoCompression)
                return new PoolList<LzMatch>();

            LZMatchFinder finder = new LZMatchFinder(lz.GetWindowsLevel(level), lz.MaxLength, lz.MinLength, lookAhead, level!= CompressionLevel.Fastest, lz.MinDistance);
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

        internal void FindMatchesInBlock(byte* dataPtr, int dataLength, int start, int end, IList<LzMatch> lzMatches)
        {
            if (start == 0)
                start = _lookAhead ? 1 : _minMatchLength;

            for (int i = start; i < end - 3; i++)
            {
                FindBestMatch(dataPtr, dataLength, end, i, out int bestDistance, out int bestLength);

                // continue if no match was found.
                if (bestLength < _minMatchLength)
                    continue;

                // Lazy match: prefer match at i+1 if it beats current match after losing one literal.
                if (_lazyMatch && bestLength <= _minMatchLength + 2 && i + 1 < end)
                {
                    FindBestMatch(dataPtr, dataLength, end, i + 1, out int nextDistance, out int nextLength);
                    if (nextLength - 1 > bestLength)
                    {
                        i++;
                        bestLength = nextLength;
                        bestDistance = nextDistance;
                    }
                }

                // Add the best match
                lzMatches.Add(new LzMatch(i, bestDistance, bestLength));
                i += bestLength - 1;
            }
        }

        private void FindBestMatch(byte* dataPtr, int dataLength, int end, int i, out int bestDistance, out int bestLength)
        {
            int matchStart = Math.Max(0, i - _windowsSize);

            int maxBestLength = _lookAhead
                ? Math.Min(_maxMatchLength, dataLength - i) // <-- | -->
                : Math.Min(_maxMatchLength, i); // <-- |

            bestLength = 0;
            bestDistance = 0;

            ushort a16 = *(ushort*)(dataPtr + i);
            for (int j = i - _minDistance; j >= matchStart; j--)
            {
                // Use ushort comparison for the first two bytes for faster matching
                if (a16 == *(ushort*)(dataPtr + j))
                {
                    int currentDistance = i - j;
                    int currentLength = 2;

                    int currentBestLength = _lookAhead
                        ? maxBestLength
                        : Math.Min(maxBestLength, currentDistance);

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
