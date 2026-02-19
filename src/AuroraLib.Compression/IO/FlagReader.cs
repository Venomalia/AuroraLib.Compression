using AuroraLib.Core.Exceptions;
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
        public int BitsLeft { get; private set; }

        private int CurrentFlag;
        private readonly int _FlagSize;
        private readonly bool _BitOrderIsBe;
        private readonly Endian _ByteOrder;
        private readonly Stream _Base;
        private readonly Func<int>? ReadFlagDelegate;

        public FlagReader(Stream source, Endian bitOrder, byte flagSize = 1, Endian byteOrder = Endian.Little)
        {
            ThrowIf.Null(source, nameof(source));
            ThrowIf.NegativeOrZero(flagSize, nameof(flagSize));

            _Base = source;
            _BitOrderIsBe = bitOrder == Endian.Big;

            _FlagSize = flagSize * 8;
            _ByteOrder = byteOrder;
        }

        public FlagReader(Stream source, Endian bitOrder, Func<int> readFlagDelegate, byte flagSize) : this(source, bitOrder, flagSize)
            => ReadFlagDelegate = readFlagDelegate;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadNextFlag() => _FlagSize switch
        {
            8 => _Base.ReadInt8(),
            16 => _Base.ReadUInt16(_ByteOrder),
            24 => _Base.ReadUInt24(_ByteOrder),
            32 => _Base.ReadInt32(_ByteOrder),
            _ => throw new NotImplementedException($"Unsupported flag size: {_FlagSize}")
        };

        /// <summary>
        /// Reads a single bit from the stream.
        /// </summary>
        /// <returns>The value of the read bit.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Readbit()
        {
            if (BitsLeft == 0)
            {
                CurrentFlag = ReadFlagDelegate is null ? ReadNextFlag() : ReadFlagDelegate();
                BitsLeft = _FlagSize;
            }

            int shiftAmount = _BitOrderIsBe ? BitsLeft - 1 : _FlagSize - BitsLeft;
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
