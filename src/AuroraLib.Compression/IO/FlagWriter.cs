using AuroraLib.Core;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.IO
{
    /// <summary>
    /// Represents a flag writer used for compressing data. It provides methods to write individual bits.
    /// </summary>
    public sealed class FlagWriter : IDisposable
    {
        private int CurrentFlag;
        public int BitsLeft { get; private set; }
        private readonly int FlagSize;
        public readonly Stream Base;
        public readonly MemoryPoolStream Buffer;
        public readonly Endian BitOrder;
        private readonly Action<int> WriteFlag;


        public FlagWriter(Stream destination, Endian bitOrder, Action<int> writeFlag, int bufferCapacity = 0x100, byte flagSize = 1)
        {
            Base = destination;
            BitOrder = bitOrder;
            Buffer = new MemoryPoolStream(bufferCapacity);
            CurrentFlag = 0;
            FlagSize = BitsLeft = 8 * flagSize;
            WriteFlag = writeFlag;
        }

        public FlagWriter(Stream destination, Endian bitOrder, int bufferCapacity = 0x100, byte flagSize = 1, Endian byteOrder = Endian.Little) : this(destination, bitOrder, null!, bufferCapacity, flagSize)
        {
            switch (flagSize)
            {
                case 1:
                    WriteFlag = i => destination.WriteByte((byte)i);
                    break;
                case 2:
                    WriteFlag = i => destination.Write((ushort)i, byteOrder);
                    break;
                case 3:
                    WriteFlag = i => destination.Write((UInt24)i, byteOrder);
                    break;
                case 4:
                    WriteFlag = i => destination.Write(i, byteOrder);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Writes a single bit as a flag. The bits are accumulated in a byte and flushed to the destination stream when necessary.
        /// </summary>
        /// <param name="bit">The bit value to write (true for 1, false for 0).</param>
#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void WriteBit(bool bit)
        {
            if (bit)
            {
                int shiftAmount = BitOrder == Endian.Little ? FlagSize - BitsLeft : BitsLeft - 1;
                CurrentFlag |= (1 << shiftAmount);
            }

            if (--BitsLeft == 0)
                Flush();
        }

        /// <summary>
        /// Writes an integer value as a sequence of bits with the specified number of bits. The bits are written from the most significant bit to the least significant bit.
        /// </summary>
        /// <param name="value">The integer value to write.</param>
        /// <param name="bits">The number of bits to write (default is 1).</param>
#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public void WriteInt(int value, int bits, bool reverseOrder = false)
        {
            if (!reverseOrder)
            {
                for (int i = 0; i < bits; i++)
                {
                    WriteBit(((value >> i) & 0x1) == 1);
                }
            }
            else
            {
                for (int i = bits - 1; i >= 0; i--)
                {
                    WriteBit(((value >> i) & 0x1) == 1);
                }
            }
        }

        /// <summary>
        /// Flushes any remaining bits and the buffer to the underlying stream.
        /// </summary>
        public void Flush()
        {
            if (BitsLeft != FlagSize)
            {
                WriteFlag(CurrentFlag);
                BitsLeft = FlagSize;
                CurrentFlag = 0;
            }
            if (Buffer.Length != 0)
            {
                Buffer.WriteTo(Base);
                Buffer.SetLength(0);
            }
        }

        /// <summary>
        /// Flushes the buffer to the underlying stream if necessary.
        /// </summary>
        public void FlushIfNecessary()
        {
            if (BitsLeft == FlagSize && Buffer.Length != 0)
            {
                Buffer.WriteTo(Base);
                Buffer.SetLength(0);
            }
        }

        public void Dispose()
        {
            Flush();
            Buffer.Dispose();
        }
    }
}
