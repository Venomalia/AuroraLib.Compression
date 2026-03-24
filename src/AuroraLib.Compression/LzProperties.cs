using System;
using System.IO.Compression;

namespace AuroraLib.Compression
{
    /// <summary>
    /// Represents properties and settings for LZ compression.
    /// </summary>
    public class LzProperties
    {
        /// <summary>
        /// Gets the number of bits used to represent the distance.
        /// </summary>
        public readonly byte WindowsBits;

        /// <summary>
        /// Gets the number of bits used to represent the length.
        /// </summary>
        public readonly byte LengthBits;

        /// <summary>
        /// Gets the minimum match length.
        /// </summary>
        public readonly byte MinLength;

        /// <summary>
        /// Gets the maximum match length.
        /// </summary>
        public readonly int MaxLength;

        /// <summary>
        /// Gets the window size.
        /// </summary>
        public readonly int MaxDistance;

        /// <summary>
        /// Gets the minimum distance
        /// </summary>
        public readonly int MinDistance;

        /// <summary>
        /// Gets the window start position.
        /// </summary>
        public readonly int WindowsStart;

        public LzProperties(int windowsSize, int maxLength, byte minLength = 3, int windowsStart = 0, int minDistance = 1)
        {
            WindowsBits = (byte)Math.Ceiling(Math.Log(windowsSize, 2));
            LengthBits = (byte)Math.Ceiling(Math.Log(maxLength - minLength, 2));
            MinLength = minLength;
            MaxDistance = windowsSize;
            MaxLength = maxLength;
            WindowsStart = windowsStart;
            MinDistance = minDistance;
        }

        public LzProperties(byte distanceBits, byte lengthBits, byte threshold = 2)
        {
            WindowsBits = distanceBits;
            LengthBits = lengthBits;
            MinLength = (byte)(threshold + 1);
            MaxDistance = 1 << distanceBits;
            MaxLength = (1 << lengthBits) + threshold;
            WindowsStart = MaxDistance - (1 << lengthBits) - threshold;
            MinDistance = 1;
        }

        public int GetWindowsLevel(CompressionLevel level)
#if NET6_0_OR_GREATER
            => level switch
            {
                CompressionLevel.Optimal => MaxDistance > 0x8000 ? 0x8000 : MaxDistance,
                CompressionLevel.Fastest => MaxDistance > 0x4000 ? 0x4000 : MaxDistance >> 1,
                CompressionLevel.SmallestSize => MaxDistance,
                CompressionLevel.NoCompression => 0,
                _ => throw new NotImplementedException(),
            };
#else
        {
            return level switch
            {
                CompressionLevel.Optimal => MaxDistance > 0x10000 ? 0x10000 : MaxDistance,
                CompressionLevel.Fastest => MaxDistance > 0x4000 ? 0x4000 : MaxDistance >> 1,
                (CompressionLevel)3 => MaxDistance,
                CompressionLevel.NoCompression => 0,
                _ => throw new NotImplementedException(),
            };
        }
#endif

        public int GetWindowsFlag()
            => MaxDistance - 1;

        public int GetLengthBitsFlag()
            => (1 << LengthBits) - 1;
    }
}
