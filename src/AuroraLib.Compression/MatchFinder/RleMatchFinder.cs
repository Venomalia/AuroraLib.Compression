using System;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression
{
    /// <summary>
    /// Provides functionality for finding matches in Run-Length compression algorithm.
    /// </summary>
    public class RleMatchFinder
    {
        private readonly int minMatch;
        private readonly int maxMatch;

        public RleMatchFinder(int minMatch = 3, int maxMatch = 127)
        {
            this.minMatch = minMatch;
            this.maxMatch = maxMatch;
        }

        /// <summary>
        /// Attempts to find the best match in the provided source data at the given offset and returns the match information.
        /// </summary>
        /// <param name="source">The source data to search for matches.</param>
        /// <param name="offset">The offset in the source data to start searching for a match.</param>
        /// <param name="match">The best match found at the specified offset.</param>
        /// <returns>True if a match is found; otherwise, false.</returns>
#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public bool TryToFindMatch(ReadOnlySpan<byte> source, int offset, out int duration)
        {
            ReadOnlySpan<byte> dataToMatch = source.Slice(offset, Math.Min(maxMatch, source.Length - offset));
            duration = GetRelMatchLength(dataToMatch);

            if (duration < minMatch)
            {
                duration = 0;
                do
                {
                    duration++;

                    if (source.Length - offset - duration < minMatch)
                    {
                        duration = source.Length - offset;
                        break;
                    }
                    dataToMatch = source.Slice(offset + duration, minMatch);
                } while (duration != maxMatch && minMatch != GetRelMatchLength(dataToMatch));
                return false;
            }
            return true;
        }

        private static int GetRelMatchLength(ReadOnlySpan<byte> data)
        {
            byte matchByte = data[0];
            for (int i = 1; i < data.Length; i++)
            {
                if (data[i] != matchByte)
                {
                    return i;
                }
            }
            return data.Length;
        }
    }
}
