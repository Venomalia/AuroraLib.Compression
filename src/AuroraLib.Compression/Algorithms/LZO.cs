using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Lempel–Ziv–Oberhumer (LZO) algorithm, focused on decompression speed.
    /// </summary>
    public sealed class LZO : ICompressionAlgorithm, ILzSettings
    {
        private static readonly LzProperties _lz = new(0xBFFF, int.MaxValue, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
        {
            // Has no header, recognition is inaccurate!
            int flag = stream.ReadByte();
            return (flag > 11 && flag < 0x20) || (flag != -1 && flag < 0x10);
        }

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
            => DecompressHeaderless(source, destination);

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
            => CompressHeaderless(source, destination, LookAhead, level);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void DecompressHeaderless(Stream source, Stream destination)
        {
            int flag, length, distance, plain = 0;

            using LzWindows buffer = new(destination, _lz.WindowsSize);

            // Read the first flage.
            if ((flag = source.ReadByte()) != -1)
            {
                // special case if the first flag is greater than 17
                if (flag > 17)
                {
                    length = flag - 17;
                    buffer.CopyFrom(source, length);
                    flag = source.ReadByte();
                }

                do // We read a new flag at the end.
                {
                    switch (flag >> 4)
                    {
                        case 0:
                            // Plain copy or special case depending on the number of plain bytes last read.   
                            switch (plain)
                            {
                                case 0: // 0000 LLLL | plain copy L = 4-18 or 18+
                                    length = 3 + flag;
                                    if (length == 3)
                                        length = 18 + ReadExtendedInt(source);

                                    plain = 4;
                                    buffer.CopyFrom(source, length);
                                    // Continue & read a new flag.
                                    continue;
                                case <= 3: // 0000 DDPP DDDD DDDD | P = 0-3 D = 1-1024 L = 2
                                    distance = source.ReadByte();
                                    distance = (distance << 2) + (flag >> 2) + 1;
                                    length = 2;
                                    break;
                                default:// 0000 DDPP DDDD DDDD | P = 0-3 D = 2049-3072 L = 3
                                    distance = source.ReadByte();
                                    distance = (distance << 2) + (flag >> 2) + (2048 + 1);
                                    length = 3;
                                    break;
                            }
                            break;
                        case 1: // 0001 HLLL ... DDDD DDPP DDDD DDDD | P = 0-3 D = 16385-49151 L = 3-9 or 9+
                            length = 2 + (flag & 0x7);
                            if (length == 2)
                                length = 9 + ReadExtendedInt(source);

                            distance = 16384 + ((flag & 0x8) << 11);
                            flag = source.ReadByte();
                            distance |= (source.ReadByte() << 6 | flag >> 2);

                            // End flag
                            if (distance == 16384)
                                return;

                            break;
                        case <= 3: // 001L LLLL ... DDDD DDPP DDDD DDDD | P = 0-3 D = 1-16384 L = 3-33 or 33+
                            length = 2 + (flag & 0x1f);
                            if (length == 2)
                                length = 33 + ReadExtendedInt(source);

                            flag = source.ReadByte();
                            distance = source.ReadByte();
                            distance = (distance << 6 | flag >> 2) + 1;
                            break;
                        case <= 7: // 01LD DDPP DDDD DDDD | P = 0-3 D = 1-2048 L = 3-4
                            length = 3 + ((flag >> 5) & 0x1);
                            distance = source.ReadByte();
                            distance = (distance << 3) + ((flag >> 2) & 0x7) + 1;
                            break;
                        default: // 1LLD DDPP DDDD DDDD | P = 0-3 D = 1-2048 L = 5-8
                            length = 5 + ((flag >> 5) & 0x3);
                            distance = source.ReadByte();
                            distance = (distance << 3) + ((flag & 0x1c) >> 2) + 1;
                            break;
                    }
                    plain = flag & 0x3;
                    buffer.BackCopy(distance, length);
                    buffer.CopyFrom(source, plain);

                } while ((flag = source.ReadByte()) != -1);
            }
            throw new EndOfStreamException();
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0;
            LzMatchFinder dictionary = new(_lz, lookAhead, level);

            LzMatch match, next = default;
            while (sourcePointer < source.Length)
            {
                int plain = 0;
                match = next;
                dictionary.AddEntryRange(source, sourcePointer, match.Length);
                sourcePointer += match.Length;
                // find plain and next match
                while (sourcePointer + plain < source.Length)
                {
                    dictionary.FindMatch(source, sourcePointer + plain, out next);
                    if (next.Length != 0)
                    {
                        // long plain bytes must be at least 4 bytes long.
                        if (match.Length == 0 && plain < 4)
                        {
                            while (plain < 4)
                            {
                                plain++;
                                dictionary.AddEntry(source, sourcePointer + plain);
                            }
                            continue;
                        }
                        break;
                    }

                    dictionary.AddEntry(source, sourcePointer + plain);
                    plain++;
                }

                // LZ copy + short plain copy
                if (match.Length != 0)
                {
                    int sPlain = 0;
                    if (plain < 4)
                    {
                        sPlain = plain;
                        plain = 0;
                    }

                    if (match.Length <= 8 && match.Distance <= 2048)
                    {
                        byte flag = (byte)(sPlain | (((match.Distance - 1) & 0x7) << 2));
                        if (match.Length <= 4) //P = 0-3 D = 1-2048 L = 3-4
                        {
                            destination.WriteByte((byte)(flag | 0x40 | ((match.Length - 3) << 5)));
                        }
                        else //P = 0-3 D = 1-2048 L = 5-8
                        {
                            destination.WriteByte((byte)(flag | 0x80 | ((match.Length - 5) << 5)));
                        }
                        destination.WriteByte((byte)((match.Distance - 1) >> 3));

                    }
                    else if (match.Distance <= 16384) //P = 0-3 D = 1-16384 L = 3-33 or 33+
                    {
                        if (match.Length > 33)
                        {
                            destination.WriteByte(0x20);
                            WriteExtendedInt(destination, match.Length - 33);
                        }
                        else
                        {
                            destination.WriteByte((byte)(0x20 | (match.Length - 2)));
                        }
                        destination.WriteByte((byte)(sPlain | (match.Distance - 1) << 2));
                        destination.WriteByte((byte)((match.Distance - 1) >> 6));
                    }
                    else //P = 0-3 D = 16385-49151 L = 3-9 or 9+
                    {
                        const int hFlag = 0x4000;
                        int distance = match.Distance - hFlag;
                        byte flag = (byte)(0x10 | ((distance & hFlag) >> 11));
                        if (match.Length > 9)
                        {
                            destination.WriteByte(flag);
                            WriteExtendedInt(destination, match.Length - 9);
                        }
                        else
                        {
                            destination.WriteByte((byte)(flag | (match.Length - 2)));
                        }
                        destination.WriteByte((byte)(sPlain | distance << 2));
                        destination.WriteByte((byte)(distance >> 6));
                    }
                    destination.Write(source.Slice(sourcePointer, sPlain));
                    sourcePointer += sPlain;
                }

                // plain copy
                if (plain != 0)
                {
                    if (plain > 18)
                    {
                        destination.WriteByte(0);
                        WriteExtendedInt(destination, plain - 18);
                    }
                    else
                    {
                        destination.WriteByte((byte)(plain - 3));
                    }
                    destination.Write(source.Slice(sourcePointer, plain));
                    sourcePointer += plain;
                }
            }
            // end flag
            destination.WriteByte(0x11);
            destination.WriteByte(0x0);
            destination.WriteByte(0x0);
        }

        private static int ReadExtendedInt(Stream source)
        {
            int b, length = 0;
            while ((b = source.ReadByte()) == 0)
                length += byte.MaxValue;

            if (b == -1)
                throw new EndOfStreamException();

            return length + b;
        }

        private static void WriteExtendedInt(Stream destination, int vaule)
        {
            while (vaule > byte.MaxValue)
            {
                destination.WriteByte(0);
                vaule -= byte.MaxValue;
            }
            destination.WriteByte((byte)vaule);
        }
    }
}
