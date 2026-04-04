using AuroraLib.Core.Exceptions;
using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.MatchFinder
{
    /// <summary>
    /// LZ77 match finder using hash + optional chain traversal.
    /// Designed for high-performance compression encoders.
    /// </summary>
    public unsafe sealed class LzChainMatchFinder : IDisposable
    {
        // ===== Format constraints =====
        private readonly int _minMatchLength;
        private readonly int _maxMatchLength;
        private readonly int _minDistance;
        private readonly int _maxDistance;
        private readonly LzProperties[]? _lzProperties;

        // ===== Hash / chain configuration =====
        private readonly int _chainMask;
        private readonly int _hashBits;
        private readonly int _hashMask;
        private readonly int _maxChain;
        private readonly uint _minMask;

        // ===== Options =====
        private readonly int _lazyThreshold;// Lazy matching: compares current position with next byte position
        private readonly bool _noSelfOverlap;// Prevents unsafe overlap copy matches (decoder safety mode)

        // ===== Lookup tables =====
        private readonly int[] _headTable; // Lookup table: maps hash -> most recent match position (addressable by hash)
        private readonly int[] _chainTable;  // Chain table: secondary collision resolution (linked list per hash bucket)
        private readonly int[]? _minTable; // Optional table for small matches (addressable by hash)

        // ===== States =====
        private int Position; // Position is internal cursor and is advanced automatically.

        private static readonly int[] NoChainTable = new int[] { -1 }; // Used when chain is disabled (avoid null checks in hot path)
        public LzChainMatchFinder(LzProperties[] properties, int maxChain = 16, int lazyThreshold = 5, int hashBits = 18, int maxChainSizeBits = 20, int maxWindowBits = 0, bool noSelfOverlap = false, bool UseMinTable = false)
        {
            ThrowIf.Null(properties);
            ThrowIf.LessThan(properties.Length, 1);
            ThrowIf.NegativeOrZero(maxChain);
            ThrowIf.Negative(lazyThreshold);
            ThrowIf.LessThan(hashBits, 14);
            ThrowIf.GreaterThan(hashBits, 24);
            ThrowIf.NegativeOrZero(maxChainSizeBits);

            if (properties.Length != 1)
                _lzProperties = properties;

            // ===== Derive global constraints from format =====
            _minMatchLength = int.MaxValue;
            _maxMatchLength = 0;
            _minDistance = int.MaxValue;
            _maxDistance = 0;
            int windowsBits = 1;
            foreach (var prop in properties)
            {
                if (_minMatchLength > prop.MinLength) _minMatchLength = prop.MinLength;
                if (_maxMatchLength < prop.MaxLength) _maxMatchLength = prop.MaxLength;
                if (_minDistance > prop.MinDistance) _minDistance = prop.MinDistance;
                if (_maxDistance < prop.MaxDistance) _maxDistance = prop.MaxDistance;
                if (windowsBits < prop.WindowsBits) windowsBits = prop.WindowsBits;
            }
            if (maxWindowBits != 0)
            {
                windowsBits = Math.Max(windowsBits, maxWindowBits);
                _maxDistance = Math.Max(_maxDistance, 1 << maxWindowBits);
            }

            // ===== Options =====
            _lazyThreshold = lazyThreshold;
            _noSelfOverlap = noSelfOverlap;

            // ===== init hash table =====
            _hashBits = hashBits;
            _hashMask = (1 << hashBits) - 1;
            _headTable = ArrayPool<int>.Shared.Rent(1 << hashBits);

            // ===== init chain table =====
            _maxChain = maxChain;
            if (_maxChain == 1)
            {
                // No-chain mode: disables traversal completely (fast path optimization)
                _chainTable = NoChainTable;
                _chainMask = 0;
            }
            else
            {
                maxChainSizeBits = Math.Min(maxChainSizeBits, windowsBits);
                _chainTable = ArrayPool<int>.Shared.Rent(1 << maxChainSizeBits);
                _chainMask = (1 << maxChainSizeBits) - 1;
            }

            // ===== init Small-match table =====
            if (UseMinTable && _minMatchLength < 4)
            {
                _minMask = 0xFFFFFFFFu >> ((4 - _minMatchLength) * 8);
                _minTable = ArrayPool<int>.Shared.Rent(ushort.MaxValue + 1);
            }
            Reset();
        }

        public LzChainMatchFinder(LzProperties[] properties, CompressionSettings settings) : this(properties, GetMaxChain(settings.Quality), 3 + (settings.Quality / 3), 15 + (int)Math.Sqrt(2 * settings.Quality), 17 + (int)Math.Sqrt(2 * settings.Quality), settings.MaxWindowBits, settings.Strategy == CompresionStrategy.CompatibilityMode, settings.Quality >= 10)
        { }

        static int GetMaxChain(int Quality)
        {
            if (Quality < 6)
                return Quality + 1;
            if (Quality >= 11)
                return 1 << (Quality - 5);
            int baseVal = 1 << (Quality >> 1);
            return baseVal | (baseVal >> (Quality & 1));
        }

        public LzChainMatchFinder(LzProperties propertie, CompressionSettings settings) : this(new LzProperties[] { propertie }, settings)
        {
        }

        public void Reset()
        {
            Position = 0;
            _headTable.AsSpan(0, _hashMask + 1).Fill(-1);
            if (_maxChain != 1)
                _chainTable.AsSpan(0, _chainMask + 1).Fill(-1);
            _minTable?.AsSpan(0, ushort.MaxValue + 1).Fill(-1);
        }

        private void Insert(int pos, int h4, int hm)
        {
            if (_chainMask != 0)
                _chainTable[pos & _chainMask] = _headTable[h4];

            _headTable[h4] = pos;
            if (_minTable != null)
            {
                _chainTable[pos & _chainMask] = _minTable[hm];
                _minTable[hm] = pos;
            }
        }

        /// <summary>
        /// Returns next best LZ match at current position.
        /// Advances internal cursor automatically.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public LzMatch FindNextBestMatch(ReadOnlySpan<byte> data)
        {
            fixed (byte* dataPtr = data)
                return FindNextBestMatch(dataPtr, data.Length);
        }

        public LzMatch FindNextBestMatch(byte* data, int length)
        {
            int limit = length - 4;

            int maxChain = _maxChain;
            while (Position <= limit)
            {
                MatchSearch(data, length, Position, maxChain, out int bestDistance, out int bestLength);

                // No valid match → advance by 1 byte
                if (bestLength < _minMatchLength)
                {
                    Position++;
                    continue;
                }

                // ===== Lazy matching (lookahead optimization) =====
                // Checks if shifting forward by 1 byte yields a better match
                int skip = 0;
                if (bestLength <= _lazyThreshold && Position + 1 <= limit)
                {
                    int nextPos = Position + 1;
                    MatchSearch(data, length, nextPos, maxChain, out int nextDistance, out int nextLength);
                    if (nextLength > bestLength)
                    {
                        bestLength = nextLength;
                        bestDistance = nextDistance;
                        Position = nextPos;
                    }
                    else
                    {
                        skip++;
                    }
                }

                var match = new LzMatch(Position, bestDistance, bestLength);

                // ===== Fill hash window =====
                // Ensures skipped region is indexed so future matches remain valid
                int end = Position + bestLength;
                Position++;
                Position += skip;
                while (Position < end && Position <= limit)
                {
                    ComputeHash(data + Position, out int h4, out int hm);
                    Insert(Position, h4, hm);
                    Position++;
                }

                return match;
            }

            // ===== END =====
            // Reached end of input buffer
            Position = length;
            return new LzMatch(length, 0, 0);
        }

        private void MatchSearch(byte* data, int dataLength, int pos, int attempts, out int bestDistance, out int bestLength)
        {
            byte* dataPos = data + pos;
            ComputeHash(dataPos, out int h4, out int hm);
            int cur = _headTable[h4];

            bestDistance = bestLength = 0;
            int bestScore = -1;

            int bestPossibleMatch = Math.Min(dataLength - pos, _maxMatchLength);

            // ===== Chain matches =====
            while (cur != -1 && attempts-- > 0)
            {
                int distance = pos - cur;
                if (distance > _maxDistance)
                    break;

                if (distance < _minDistance)
                {
                    cur = GetNext(cur);
                    continue;
                }

                int len = GetMatchLength(dataPos, data + cur, bestPossibleMatch);
                int score = ScoreMatch(ref len, distance);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestLength = len;
                    bestDistance = distance;
                    if (bestLength == bestPossibleMatch)
                        break;
                }

                cur = GetNext(cur);
            }

            // ===== fallback for small matches =====
            // Used when chain search fails 
            if (bestLength == 0 && _minTable != null)
            {
                cur = _minTable[hm];

                if (cur != -1)
                {
                    int distance = pos - cur;

                    if (distance >= _minDistance && distance <= _maxDistance)
                    {
                        bestLength = GetMatchLength(dataPos, data + cur, bestPossibleMatch);
                        _ = ScoreMatch(ref bestLength, distance);
                        bestDistance = distance;
                    }
                }
            }

            Insert(pos, h4, hm);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetNext(int pos) => _chainTable[pos & _chainMask];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ComputeHash(byte* data, out int h4, out int hm)
        {
            const uint prim = 2654435761u;
            uint v = *(uint*)data;
            uint min = v & _minMask;

            v *= prim;
            min *= prim;

            h4 = (int)(v >> (32 - _hashBits)) & _hashMask;
            hm = (int)((min >> 16) & ushort.MaxValue);
        }

        private int ScoreMatch(ref int length, int distance)
        {
            // Prevent unsafe overlap copies if required by decoder
            if (_noSelfOverlap && length > distance)
                length = distance;

            if (_lzProperties == null)
                return length - _minMatchLength;

            foreach (var propertie in _lzProperties)
            {
                if (distance <= propertie.MaxDistance && length >= propertie.MinLength && distance >= propertie.MinDistance)
                {
                    if (length > propertie.MaxLength)
                        length = propertie.MaxLength;
                    return length - propertie.MinLength;
                }
            }
            length = 0;
            return -1;
        }

        public void Dispose()
        {
            // Release head table back to pool.
            ArrayPool<int>.Shared.Return(_headTable);

            // Only return chain table if it was actually allocated.
            if (!ReferenceEquals(_chainTable, NoChainTable))
                ArrayPool<int>.Shared.Return(_chainTable);

            // Release min table back to pool.
            if (_minTable != null)
                ArrayPool<int>.Shared.Return(_minTable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetMatchLength(byte* dataA, byte* dataB, int max)
        {
            int len = 0;

            // 8-byte fast path
            while (len + 8 <= max)
            {
                ulong diff = *(ulong*)(dataA + len) ^ *(ulong*)(dataB + len);
                if (diff != 0)
                    return len + TrailingZeroCount(diff) / 8;

                len += 8;
            }

            // byte fallback
            while (len < max && dataA[len] == dataB[len])
                len++;

            return len;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int TrailingZeroCount(ulong value)
#if NET6_0_OR_GREATER
      => BitOperations.TrailingZeroCount(value);
#else
        {
            //if (value == 0) return 64; // We'll check that somewhere else!
            value &= (~value + 1);
            return DeBruijnIdx64[(value * 0x022FDD63CC95386DUL) >> 58];
        }
        static readonly int[] DeBruijnIdx64 = new int[64] { 0, 1, 2, 53, 3, 7, 54, 27, 4, 38, 41, 8, 34, 55, 48, 28, 62, 5, 39, 46, 44, 42, 22, 9, 24, 35, 59, 56, 49, 18, 29, 11, 63, 52, 6, 26, 37, 40, 33, 47, 61, 45, 43, 21, 23, 58, 17, 10, 51, 25, 36, 32, 60, 20, 57, 16, 50, 31, 19, 15, 30, 14, 13, 12 };
#endif
    }
}
