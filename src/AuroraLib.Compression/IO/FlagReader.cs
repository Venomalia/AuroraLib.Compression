using AuroraLib.Core;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.IO
{
    /// <summary>
    /// Reads individual bits from a stream and provides methods for interpreting the flag values.
    /// </summary>
    public sealed class FlagReader
    {
        private int CurrentFlag;
        public int BitsLeft { get; private set; }
        private readonly int FlagSize;
        public readonly Stream Base;
        public readonly Endian BitOrder;
        private readonly Func<int> ReadFlag;

        public FlagReader(Stream source, Endian bitOrder, byte flagSize = 1, Endian byteOrder = Endian.Little)
        {
            Base = source;
            BitOrder = bitOrder;

            FlagSize = flagSize * 8;
            switch (flagSize)
            {
                case 1:
                    ReadFlag = () => Base.ReadInt8();
                    break;
                case 2:
                    ReadFlag = () => Base.ReadUInt16(byteOrder);
                    break;
                case 3:
                    ReadFlag = () => Base.ReadUInt24(byteOrder);
                    break;
                case 4:
                    ReadFlag = () => Base.ReadInt32(byteOrder);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Reads a single bit from the stream.
        /// </summary>
        /// <returns>The value of the read bit.</returns>
        public bool Readbit()
        {
            if (BitsLeft == 0)
            {
                CurrentFlag = ReadFlag();
                BitsLeft = FlagSize;
            }

            int shiftAmount = BitOrder == Endian.Little ? FlagSize - BitsLeft : BitsLeft - 1;
            BitsLeft--;

            return (CurrentFlag & (1 << shiftAmount)) != 0;
        }

        /// <summary>
        /// Reads an integer value with the specified number of bits from the stream.
        /// </summary>
        /// <param name="bits">The number of bits to read.</param>
        /// <returns>The integer value read from the stream.</returns>
#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
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
