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
        public readonly byte DistanceBits;

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
        public readonly int WindowsSize;

        /// <summary>
        /// Gets the window start position.
        /// </summary>
        public readonly int WindowsStart;

        public LzProperties(int windowsSize, int maxLength, byte minLength = 3, int windowsStart = 0)
        {
            DistanceBits = (byte)Math.Ceiling(Math.Log(windowsSize, 2));
            LengthBits = (byte)Math.Ceiling(Math.Log(maxLength - minLength, 2));
            MinLength = minLength;
            WindowsSize = windowsSize;
            MaxLength = maxLength;
            WindowsStart = windowsStart;
        }

        public LzProperties(byte distanceBits, byte lengthBits, byte threshold = 2)
        {
            DistanceBits = distanceBits;
            LengthBits = lengthBits;
            MinLength = (byte)(threshold + 1);
            WindowsSize = 1 << distanceBits;
            MaxLength = (1 << lengthBits) + threshold;
            WindowsStart = WindowsSize - (1 << lengthBits) - threshold;
        }

        public LzProperties SetLevel(CompressionLevel level = CompressionLevel.Optimal)
#if NET6_0_OR_GREATER
            => level switch
            {
                CompressionLevel.NoCompression => new LzProperties(0, 0, byte.MaxValue, WindowsStart),
                CompressionLevel.Optimal => WindowsSize > 0x10000 ? new LzProperties(0x10000, MaxLength, MinLength, WindowsStart) : this,
                CompressionLevel.Fastest => WindowsSize > 0x4000 ? new LzProperties(0x4000, MaxLength, MinLength, WindowsStart) : this,
                CompressionLevel.SmallestSize => this,
                _ => throw new NotImplementedException(),
            };
#else
        {
            switch (level)
            {
                case CompressionLevel.Optimal:
                    return WindowsSize > 0x20000 ? new LzProperties(0x20000, MaxLength, MinLength, WindowsStart) : this;
                case CompressionLevel.Fastest:
                    return WindowsSize > 0x4000 ? new LzProperties(0x4000, MaxLength, MinLength, WindowsStart) : this;
                case CompressionLevel.NoCompression:
                    return new LzProperties(0, 0, byte.MaxValue, WindowsStart);
                case (CompressionLevel)3:
                    return this;
                default:
                    throw new NotImplementedException();
            }
        }
#endif

        public int GetWindowsFlag()
            => WindowsSize - 1;

        public int GetLengthBitsFlag()
            => (1 << LengthBits) - 1;
    }
}
