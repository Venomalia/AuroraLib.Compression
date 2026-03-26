using AuroraLib.Core.Exceptions;
using System;
using System.IO.Compression;

namespace AuroraLib.Compression
{
    /// <summary>
    /// Compression settings controlling encoder behavior.
    /// </summary>
    public readonly struct CompressionSettings
    {
        private readonly byte _quality, _maxWindowBits;

        /// <summary>
        /// Compression quality (0–15).
        /// Higher = slower but better compression.
        /// </summary>
        public readonly int Quality => IsUninitialized ? (int)Balanced : _quality - 1;
        private bool IsUninitialized => _quality == 0; // so that the default(CompressionSettings) is mapped to Balanced.

        /// <summary>
        /// Maximum window size as log2 (0 = auto). 
        /// This value sets the largest window the encoder is allowed to use.
        /// Bigger windows improve compression ratio but require more memory.
        /// </summary>
        public readonly int MaxWindowBits => _maxWindowBits; // 0 = auto

        /// <summary>
        /// Creates CompressionSettings.
        /// </summary>
        /// <param name="quality">0–15, higher = slower but better compression (default = 8)</param>
        /// <param name="maxWindowBits">0 or 7-28, Max sliding window (0 = auto)</param>
        private CompressionSettings(int quality = 8, int maxWindowBits = 0)
        {
            ThrowIf.Negative(quality);
            ThrowIf.GreaterThan(quality, 15);
            ThrowIf.Negative(maxWindowBits);
            ThrowIf.GreaterThan(maxWindowBits, 28);
            if (maxWindowBits != 0) ThrowIf.LessThan(maxWindowBits, 7);

            _quality = (byte)(quality + 1);
            _maxWindowBits = (byte)maxWindowBits;
        }

        public static implicit operator CompressionSettings(int value) => new CompressionSettings(value);
        public static explicit operator int(CompressionSettings s) => s.Quality;
        public static implicit operator CompressionSettings(CompressionLevel level) => level switch
        {
            CompressionLevel.NoCompression => Fastest,
            CompressionLevel.Fastest => Fast,
            CompressionLevel.Optimal => Balanced,
#if NET6_0_OR_GREATER
            CompressionLevel.SmallestSize => Maximum,
#endif
            _ => throw new NotImplementedException(),
        };

        public static implicit operator CompressionLevel(CompressionSettings settings)
        {
            if (settings.Quality <= 2) return CompressionLevel.NoCompression;
            if (settings.Quality <= 6) return CompressionLevel.Fastest;
#if NET6_0_OR_GREATER
            if (settings.Quality >= 10) return CompressionLevel.SmallestSize;
#endif
            return CompressionLevel.Optimal;
        }

        /// <summary>Fastest compression possible. Minimal CPU, lower compression ratio.</summary>
        public static readonly CompressionSettings Fastest = new CompressionSettings(0);
        /// <summary>Fast compression. Slightly better ratio than Fastest.</summary>
        public static readonly CompressionSettings Fast = new CompressionSettings(4);
        /// <summary>Balanced speed and compression ratio. Good general-purpose default.</summary>
        public static readonly CompressionSettings Balanced = new CompressionSettings(8);
        /// <summary>High compression ratio. Slower than Balanced.</summary>
        public static readonly CompressionSettings High = new CompressionSettings(12);
        /// <summary>Maximum compression possible. Slowest, but best ratio.</summary>
        public static readonly CompressionSettings Maximum = new CompressionSettings(15);
    }
}
