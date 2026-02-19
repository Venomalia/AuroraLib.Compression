using AuroraLib.Core;
using AuroraLib.Core.Exceptions;
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
        public int BitsLeft { get; private set; }

        private int CurrentFlag;
        private readonly int _FlagSize;
        private readonly bool _BitOrderIsBe;
        private readonly Endian _ByteOrder;

        private readonly Stream _Base;
        public readonly MemoryPoolStream Buffer;
        private readonly Action<int>? WriteFlagDelegate;

        public FlagWriter(Stream destination, Endian bitOrder, byte flagSize = 1, Endian byteOrder = Endian.Little, int bufferCapacity = 0x100)
        {
            ThrowIf.Null(destination, nameof(destination));
            ThrowIf.NegativeOrZero(flagSize, nameof(flagSize));

            _FlagSize = BitsLeft = 8 * flagSize;
            _BitOrderIsBe = bitOrder == Endian.Big;
            _ByteOrder = byteOrder;

            _Base = destination;
            Buffer = new MemoryPoolStream(bufferCapacity);
        }

        public FlagWriter(Stream destination, Endian bitOrder, Action<int> writeFlagDelegate, byte flagSize, int bufferCapacity = 0x100) : this(destination, bitOrder, flagSize, default, bufferCapacity)
            => WriteFlagDelegate = writeFlagDelegate;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteFlag(int value)
        {
            switch (_FlagSize)
            {
                case 8:
                    _Base.WriteByte((byte)value);
                    return;
                case 16:
                    _Base.Write((ushort)value, _ByteOrder);
                    return;
                case 24:
                    _Base.Write((UInt24)value, _ByteOrder);
                    return;
                case 32:
                    _Base.Write(value, _ByteOrder);
                    return;
                default:
                    throw new NotImplementedException($"Unsupported flag size: {_FlagSize}");
            }
            ;
        }

        /// <summary>
        /// Writes a single bit as a flag. The bits are accumulated in a byte and flushed to the destination stream when necessary.
        /// </summary>
        /// <param name="bit">The bit value to write (true for 1, false for 0).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBit(bool bit)
        {
            if (bit)
            {
                int shiftAmount = _BitOrderIsBe ? BitsLeft - 1 : _FlagSize - BitsLeft;
                CurrentFlag |= (1 << shiftAmount);
            }
            BitsLeft--;
            if (BitsLeft == 0)
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
            if (BitsLeft != _FlagSize)
            {
                if (WriteFlagDelegate is null)
                    WriteFlag(CurrentFlag);
                else
                    WriteFlagDelegate(CurrentFlag);
                BitsLeft = _FlagSize;
                CurrentFlag = 0;
            }
            if (Buffer.Length != 0)
            {
                Buffer.WriteTo(_Base);
                Buffer.SetLength(0);
            }
        }

        /// <summary>
        /// Flushes the buffer to the underlying stream if necessary.
        /// </summary>
        public void FlushIfNecessary()
        {
            if (BitsLeft == _FlagSize && Buffer.Length != 0)
            {
                Buffer.WriteTo(_Base);
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
