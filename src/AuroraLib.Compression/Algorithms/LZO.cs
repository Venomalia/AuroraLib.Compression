using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Lempel–Ziv–Oberhumer (LZO) algorithm, focused on decompression speed.
    /// </summary>
    public sealed class LZO : ICompressionAlgorithm, ILzSettings
    {
        private static readonly string[] _extensions = new string[] { ".lzo" , string.Empty };

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<LZO>("Lempel–Ziv–Oberhumer", new MediaType(MIMEType.Application, "x-lzo"), _extensions);

        private static readonly LzProperties _lz = new LzProperties(0xBFFF, int.MaxValue, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
        {
            if (!fileNameAndExtension.IsEmpty && !PathX.GetExtension(fileNameAndExtension).Contains(_extensions[0].AsSpan(), StringComparison.InvariantCultureIgnoreCase))
                return false;

            // Has no header, recognition is inaccurate!
            int flag = stream.PeekByte();
            return (flag > 11 && flag < 0x20) || (flag != -1 && flag < 0x10);
        }

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
            => DecompressHeaderless(source, destination);

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
            => CompressHeaderless(source, destination, LookAhead, level);

#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static void DecompressHeaderless(Stream source, Stream destination)
        {
            int flag, length, distance, plain = 0;

            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {

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
                        int flagcode = flag >> 4;
                        if (flagcode == 0)
                        {
                            // Plain copy or special case depending on the number of plain bytes last read.   
                            if (plain == 0) // 0000 LLLL | plain copy L = 4-18 or 18+
                            {
                                length = 3 + flag;
                                if (length == 3)
                                    length = 18 + ReadExtendedInt(source);

                                plain = 4;
                                buffer.CopyFrom(source, length);
                                // Continue & read a new flag.
                                continue;
                            }
                            else if (plain <= 3) // 0000 DDPP DDDD DDDD | P = 0-3 D = 1-1024 L = 2
                            {
                                distance = source.ReadByte();
                                distance = (distance << 2) + (flag >> 2) + 1;
                                length = 2;
                            }
                            else // 0000 DDPP DDDD DDDD | P = 0-3 D = 2049-3072 L = 3
                            {
                                distance = source.ReadByte();
                                distance = (distance << 2) + (flag >> 2) + (2048 + 1);
                                length = 3;
                            }
                        }
                        else if (flagcode == 1) // 0001 HLLL ... DDDD DDPP DDDD DDDD | P = 0-3 D = 16385-49151 L = 3-9 or 9+
                        {
                            length = 2 + (flag & 0x7);
                            if (length == 2)
                                length = 9 + ReadExtendedInt(source);

                            distance = 16384 + ((flag & 0x8) << 11);
                            flag = source.ReadByte();
                            distance |= (source.ReadByte() << 6 | flag >> 2);

                            // End flag
                            if (distance == 16384)
                                return;
                        }
                        else if (flagcode <= 3) // 001L LLLL ... DDDD DDPP DDDD DDDD | P = 0-3 D = 1-16384 L = 3-33 or 33+
                        {
                            length = 2 + (flag & 0x1f);
                            if (length == 2)
                                length = 33 + ReadExtendedInt(source);

                            flag = source.ReadByte();
                            distance = source.ReadByte();
                            distance = (distance << 6 | flag >> 2) + 1;
                        }
                        else if (flagcode <= 7) // 01LD DDPP DDDD DDDD | P = 0-3 D = 1-2048 L = 3-4
                        {
                            length = 3 + ((flag >> 5) & 0x1);
                            distance = source.ReadByte();
                            distance = (distance << 3) + ((flag >> 2) & 0x7) + 1;
                        }
                        else // 1LLD DDPP DDDD DDDD | P = 0-3 D = 1-2048 L = 5-8
                        {
                            length = 5 + ((flag >> 5) & 0x3);
                            distance = source.ReadByte();
                            distance = (distance << 3) + ((flag & 0x1c) >> 2) + 1;
                        }
                        plain = flag & 0x3;
                        buffer.BackCopy(distance, length);
                        buffer.CopyFrom(source, plain);

                    } while ((flag = source.ReadByte()) != -1);
                }
            }

            throw new EndOfStreamException();
        }


        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0;
            List<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level);
            matches.Add(new LzMatch(source.Length, 0, 0)); // Dummy-Match
            using (MemoryPoolStream buffer = new MemoryPoolStream())
            {
                for (int i = 0; i < matches.Count; i++)
                {
                    LzMatch match = matches[i];
                    int plain = match.Offset - sourcePointer;

                    // plain copy
                    if (plain != 0)
                    {
                        if (plain < 4)
                        {
                            int dif = 4 - plain;
                            match = new LzMatch(match.Offset + dif, match.Distance, match.Length - dif);
                            plain = 4;
                        }

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

                    // LZ copy + short plain copy
                    if (match.Length >= _lz.MinLength)
                    {
                        sourcePointer += match.Length;
                        plain = matches[i + 1].Offset - sourcePointer;
                        if (plain > 3)
                            plain = 0;

                        if (match.Length <= 8 && match.Distance <= 2048)
                        {
                            byte flag = (byte)(plain | (((match.Distance - 1) & 0x7) << 2));
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
                            destination.WriteByte((byte)(plain | (match.Distance - 1) << 2));
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
                            destination.WriteByte((byte)(plain | distance << 2));
                            destination.WriteByte((byte)(distance >> 6));
                        }
                        destination.Write(source.Slice(sourcePointer, plain));
                        sourcePointer += plain;
                    }
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
