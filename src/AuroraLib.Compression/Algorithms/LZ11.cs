using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Exceptions;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo LZ11 compression algorithm extension of the <see cref="LZ10"/> algorithm, mainly used in DS and WII games.
    /// </summary>
    public class LZ11 : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {
        private const byte Identifier = 0x11;

        /// <inheritdoc/>
        public virtual IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<LZ11>("Nintendo LZ11", new MediaType(MIMEType.Application, "x-nintendo-lz11"), ".lz");

        private static readonly LzProperties _lz = new LzProperties(0x1000, 0x4000, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public virtual bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x8 < stream.Length && stream.Peek(s => s.ReadByte() == Identifier && (s.ReadUInt24() != 0 || s.ReadUInt32() != 0) && Validate(s));

        /// <inheritdoc/>
        public virtual uint GetDecompressedSize(Stream source)
            => source.Peek(InternalGetDecompressedSize);

        protected static uint InternalGetDecompressedSize(Stream source)
        {
            byte identifier = source.ReadUInt8();
            if (identifier != Identifier)
                throw new InvalidIdentifierException(identifier.ToString("X"), Identifier.ToString("X"));
            uint decompressedSize = source.ReadUInt24();
            if (decompressedSize == 0)
                decompressedSize = source.ReadUInt32();

            return decompressedSize;
        }

        /// <inheritdoc/>
        public virtual void Decompress(Stream source, Stream destination)
        {
            // Read Header
            uint decompressedSize = InternalGetDecompressedSize(source);

            // Perform the decompression
            DecompressHeaderless(source, destination, decompressedSize);
        }

        /// <inheritdoc/>
        public virtual void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Write Header
            if (source.Length <= 0xFFFFFF)
            {
                destination.Write(Identifier | (source.Length << 8));
            }
            else
            {
                destination.Write(Identifier | 0);
                destination.Write(source.Length);
            }

            // Perform the compression
            CompressHeaderless(source, destination, LookAhead, level);
        }

        public static void DecompressHeaderless(Stream source, Stream destination, uint decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {
                FlagReader Flag = new FlagReader(source, Endian.Big);

                while (destination.Position + buffer.Position < endPosition)
                {
                    if (Flag.Readbit())
                    {
                        int distance, length;
                        byte b1 = source.ReadUInt8();
                        byte b2 = source.ReadUInt8();
                        if (b1 >> 4 == 0) // match.Length 17-272
                        {
                            //0000LLLL LLLLDDDD DDDDDDDD
                            byte b3 = source.ReadUInt8();
                            distance = ((b2 & 0xf) << 8 | b3) + 1;
                            length = ((b1 & 0xf) << 4 | b2 >> 4) + 17;
                        }
                        else if (b1 >> 4 == 1) // match.Length 273-65808
                        {
                            //0001LLLL LLLLLLLL LLLLDDDD DDDDDDDD
                            byte b3 = source.ReadUInt8();
                            byte b4 = source.ReadUInt8();
                            distance = ((b3 & 0xf) << 8 | b4) + 1;
                            length = ((b1 & 0xf) << 12 | b2 << 4 | b3 >> 4) + 273;
                        }
                        else // match.Length 3-16
                        {
                            //LLLLDDDD DDDDDDDD
                            distance = ((b1 & 0xf) << 8 | b2) + 1;
                            length = (b1 >> 4) + 1;
                        }

                        buffer.BackCopy(distance, length);
                    }
                    else // Not compressed
                    {
                        buffer.WriteByte(source.ReadUInt8());
                    }
                }
            }

            if (destination.Position > endPosition)
            {
                throw new DecompressedSizeException(decomLength, destination.Position - (endPosition - decomLength));
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0, matchPointer = 0x0;

            using PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level);
            using FlagWriter flag = new FlagWriter(destination, Endian.Big);
            while (sourcePointer < source.Length)
            {
                if (matchPointer < matches.Count && matches[matchPointer].Offset == sourcePointer)
                {
                    LzMatch match = matches[matchPointer++];

                    if (match.Length <= 16)  // match.Length 3-16
                    {
                        flag.Buffer.Write((ushort)((match.Length - 1) << 12 | ((match.Distance - 1) & 0xFFF)), Endian.Big);
                    }
                    else if (match.Length <= 272) // match.Length 17-272
                    {
                        flag.Buffer.WriteByte((byte)(((match.Length - 17) & 0xFF) >> 4));
                        flag.Buffer.Write((ushort)((match.Length - 17) << 12 | ((match.Distance - 1) & 0xFFF)), Endian.Big);
                    }
                    else // match.Length 273-65808
                    {
                        flag.Buffer.Write((uint)(0x10000000 | ((match.Length - 273) & 0xFFFF) << 12 | ((match.Distance - 1) & 0xFFF)), Endian.Big);
                    }
                    sourcePointer += match.Length;
                    flag.WriteBit(true);
                }
                else
                {
                    flag.Buffer.WriteByte(source[sourcePointer++]);
                    flag.WriteBit(false);
                }
            }
        }

        private static bool Validate(Stream source)
        {
            int i = 3;
            int Buffer = 0;
            FlagReader Flag = new FlagReader(source, Endian.Big);
            while (source.Position < source.Length)
            {
                if (Flag.Readbit())
                {
                    int distance, length;
                    byte b1 = source.ReadUInt8();
                    byte b2 = source.ReadUInt8();
                    if (b1 >> 4 == 0) // match.Length 17-272
                    {
                        byte b3 = source.ReadUInt8();
                        distance = ((b2 & 0xf) << 8 | b3) + 1;
                        length = ((b1 & 0xf) << 4 | b2 >> 4) + 17;
                    }
                    else if (b1 >> 4 == 1) // match.Length 273-65808
                    {
                        byte b3 = source.ReadUInt8();
                        byte b4 = source.ReadUInt8();
                        distance = ((b3 & 0xf) << 8 | b4) + 1;
                        length = ((b1 & 0xf) << 12 | b2 << 4 | b3 >> 4) + 273;
                    }
                    else // match.Length 3-16
                    {
                        distance = ((b1 & 0xf) << 8 | b2) + 1;
                        length = (b1 >> 4) + 1;
                    }

                    if (distance >= Buffer) return false;
                    if (i == 0) return true;
                    i--;
                    Buffer += length;
                }
                else // Not compressed
                {
                    source.Position++;
                    Buffer++;
                }
            }
            return true;
        }
    }
}
