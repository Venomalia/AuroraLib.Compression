using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.IO
{
    /// <summary>
    /// Reads individual bits from a stream and provides methods for interpreting the flag values.
    /// </summary>
    public class FlagReader
    {
        private byte CurrentFlag;
        public byte BitsLeft { get; private set; }
        public readonly Stream Base;
        public readonly Endian BitOrder;

        public FlagReader(Stream source, Endian bitOrder)
        {
            Base = source;
            BitOrder = bitOrder;
        }

        /// <summary>
        /// Reads a single bit from the stream.
        /// </summary>
        /// <returns>The value of the read bit.</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public bool Readbit()
        {
            if (BitsLeft == 0)
            {
                CurrentFlag = Base.ReadUInt8();
                BitsLeft = 8;
            }

            int bitindex = BitOrder == Endian.Little ? 8 - BitsLeft : BitsLeft - 1;
            bool flag = (CurrentFlag & (1 << bitindex)) != 0;

            BitsLeft--;
            return flag;
        }

        /// <summary>
        /// Reads an integer value with the specified number of bits from the stream.
        /// </summary>
        /// <param name="bits">The number of bits to read.</param>
        /// <returns>The integer value read from the stream.</returns>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public int ReadInt(int bits, bool reverseOrder = false)
        {
            int vaule = 0;
            if (!reverseOrder)
            {
                for (int i = 0; i < bits; i++)
                {
                    if (Readbit())
                    {
                        vaule |= 1 << i;
                    }
                }
            }
            else
            {
                for (int i = 0; i < bits; i++)
                {
                    vaule <<= 1;
                    if (Readbit())
                    {
                        vaule |= 1;
                    }
                }
            }
            return vaule;
        }

        public void Reset() => BitsLeft = 0;
    }
}
