using AuroraLib.Core.Extensions;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.MatchFinder
{
    /// <summary>
    /// Provides functionality for finding matches in LZ compression algorithms.
    /// </summary>
    public class LzMatchFinder
    {
        private readonly LzProperties lz;
        private readonly bool lookAhead;
        private readonly List<int>[] offsetLists;

        public LzMatchFinder(LzProperties lzProperties, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Build the offset list, so Lz compression will become significantly faster
            offsetLists = new List<int>[0x100];
            for (int i = 0; i < offsetLists.Length; i++)
                offsetLists[i] = new List<int>(0x10);
#if NET20_OR_GREATER
            switch (level)
            {
                case CompressionLevel.Optimal:
                    lz = lzProperties.WindowsSize > 0x10000 ? new LzProperties(0x10000, lzProperties.MaxLength, lzProperties.MinLength, lzProperties.WindowsStart) : lzProperties;
                    break;
                case CompressionLevel.Fastest:
                    lz = lzProperties.WindowsSize > 0x4000 ? new LzProperties(0x4000, lzProperties.MaxLength, lzProperties.MinLength, lzProperties.WindowsStart) : lzProperties;
                    break;
                case CompressionLevel.NoCompression:
                    lz = new LzProperties(0, 0, byte.MaxValue, lzProperties.WindowsStart);
                    break;
                default:
                    throw new NotImplementedException();
            }
#else
            lz = level switch
            {
                CompressionLevel.NoCompression => new LzProperties(0, 0, byte.MaxValue, lzProperties.WindowsStart),
                CompressionLevel.Optimal => lzProperties.WindowsSize > 0x10000 ? new LzProperties(0x10000, lzProperties.MaxLength, lzProperties.MinLength, lzProperties.WindowsStart) : lzProperties,
                CompressionLevel.Fastest => lzProperties.WindowsSize > 0x4000 ? new LzProperties(0x4000, lzProperties.MaxLength, lzProperties.MinLength, lzProperties.WindowsStart) : lzProperties,
#if NET6_0_OR_GREATER
                CompressionLevel.SmallestSize => lzProperties,
#endif
                _ => throw new NotImplementedException(),
            };
#endif
            this.lookAhead = lookAhead;
        }

        /// <summary>
        /// Attempts to find the best match in the provided source data at the given offset and returns the match information.
        /// </summary>
        /// <param name="source">The source data to search for matches.</param>
        /// <param name="offset">The offset in the source data to start searching for a match.</param>
        /// <param name="match">The best match found at the specified offset.</param>
        /// <returns>True if a match is found; otherwise, false.</returns>
        public bool TryToFindMatch(ReadOnlySpan<byte> source, int offset, out LzMatch match)
        {
            FindMatch(source, offset, out match);
            if (match.Length != 0)
            {
                AddEntryRange(source, offset, match.Length);
                return true;
            }
            else
            {
                AddEntry(source, offset);
                return false;
            }
        }


        /// <summary>
        /// Finds the best match in the provided source data at the given offset and returns the match information.
        /// </summary>
        /// <param name="source">The source data to search for matches.</param>
        /// <param name="offset">The offset in the source data to start searching for a match.</param>
        /// <param name="match">The best match found at the specified offset.</param>
#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void FindMatch(ReadOnlySpan<byte> source, int offset, out LzMatch match)
        {
            match = default;

            // Check if there is enough data to find matches
            if (offset == 0 || source.Length - offset < lz.MinLength)
            {
                return;
            }

            // Remove old entries for this index
            RemoveOldEntries(source[offset], offset);

            // Start finding matches
            ReadOnlySpan<byte> dataToMatch = source.Slice(offset, Math.Min(lz.MaxLength, source.Length - offset));
            List<int> offsetList = offsetLists[source[offset]];
            for (int i = offsetList.Count - 1; i >= 0; i--)
            {
                int possibleMatchOffset = offsetList[i];
                int possibleMatchLength = lookAhead ? dataToMatch.Length : Math.Min(dataToMatch.Length, offset - possibleMatchOffset);
                ReadOnlySpan<byte> possibleMatch = source.Slice(possibleMatchOffset, possibleMatchLength);

                // Checks the maximum length of the match.
                int matchLength = SpanEx.MaxMatch(dataToMatch, possibleMatch);

                // Is that match good and better than what we have?
                if (matchLength >= lz.MinLength && matchLength > match.Length)
                {
                    match = new LzMatch(offset - possibleMatchOffset, matchLength);
                    // Found the best possible match?
                    if (matchLength == dataToMatch.Length) break;
                }
            }
        }


        private void RemoveOldEntries(byte index, int offset)
        {
            int windowStart = offset - lz.WindowsSize;
            if (windowStart > 0)
            {
                List<int> offsetList = offsetLists[index];

                int i = 0;
                for (; i < offsetList.Count; i++)
                {
                    if (offsetList[i] >= windowStart)
                        break;
                }
                offsetList.RemoveRange(0, i);
            }
        }

        /// <summary>
        /// Adds a new entry to the offset list for a specific source data index.
        /// </summary>
        /// <param name="source">The source data containing the entry.</param>
        /// <param name="offset">The offset value to be added to the offset list.</param>
        public void AddEntry(ReadOnlySpan<byte> source, int offset)
            => offsetLists[source[offset]].Add(offset);

        /// <summary>
        /// Adds a range of entries to the offset list for a specific source data index.
        /// </summary>
        /// <param name="source">The source data containing the entries.</param>
        /// <param name="offset">The starting offset for the range of entries.</param>
        /// <param name="length">The number of entries to add to the offset list.</param>
        public void AddEntryRange(ReadOnlySpan<byte> source, int offset, int length)
        {
            for (int i = 0; i < length; i++)
                AddEntry(source, offset + i);
        }
    }
}
