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
        private byte CurrentFlag;
        public byte BitsLeft { get; private set; }
        public readonly Stream Base;
        public readonly MemoryPoolStream Buffer;
        public readonly Endian BitOrder;

        public FlagWriter(Stream destination, Endian bitOrder, int bufferCapacity = 0x100)
        {
            Base = destination;
            BitOrder = bitOrder;
            Buffer = new MemoryPoolStream(bufferCapacity);
            CurrentFlag = 0;
            BitsLeft = 8;
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
                if (BitOrder == Endian.Little)
                    CurrentFlag |= (byte)(1 << (8 - BitsLeft));
                else
                    CurrentFlag |= (byte)(1 << (BitsLeft - 1));
            }

            BitsLeft--;

            if (BitsLeft == 0)
            {
                Flush();
            }
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
            if (BitsLeft != 8)
            {
                Base.WriteByte(CurrentFlag);
                BitsLeft = 8;
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
            if (BitsLeft == 8 && Buffer.Length != 0)
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
