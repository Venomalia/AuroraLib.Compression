namespace AuroraLib.Compression.MatchFinder
{
    /// <summary>
    /// A match in LZ compression, storing information about the distance and length of the match.
    /// </summary>
    public readonly struct LzMatch
    {
        /// <summary>
        /// Gets the distance of the match, indicating the offset from the current position.
        /// </summary>
        public readonly int Distance;

        /// <summary>
        /// Gets the length of the match, indicating the number of bytes in the match.
        /// </summary>
        public readonly int Length;

        public LzMatch(int distance, int length)
        {
            Distance = distance;
            Length = length;
        }
    }
}
