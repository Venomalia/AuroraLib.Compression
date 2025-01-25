using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
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
    /// Nintendo LZ40 compression algorithm similar to <see cref="LZ11"/>, mainly used in DS games.
    /// </summary>
    public sealed class LZ40 : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {
        private const byte Identifier = 0x40;

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<LZ40>("Nintendo LZ40", new MediaType(MIMEType.Application, "x-nintendo-lz40"), ".lz");

        private static readonly LzProperties _lz = new LzProperties(0x1000, 0x4000, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            // Has no distinct header, recognition is inaccurate!
            => stream.Peek(s => s.Position + 0x8 < s.Length && s.ReadByte() == Identifier && (s.ReadUInt24() != 0 || s.ReadUInt32() != 0));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
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
        public void Decompress(Stream source, Stream destination)
        {
            uint uncompressedSize = InternalGetDecompressedSize(source);
            DecompressHeaderless(source, destination, uncompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            if (source.Length <= 0xFFFFFF)
            {
                destination.Write(Identifier | (source.Length << 8));
            }
            else
            {
                destination.Write(Identifier | 0);
                destination.Write(source.Length);
            }

            CompressHeaderless(source, destination, LookAhead, level);
        }

        public static void DecompressHeaderless(Stream source, Stream destination, uint decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {
                int flag = 0, flagbits = 0;

                while (destination.Position + buffer.Position < endPosition)
                {
                    if (flagbits == 0)
                    {
                        // The flag value must be reversed.
                        flag = (byte)-source.ReadByte();
                        flagbits = 8;
                    }
                    if ((flag & 0x80) != 0) // Compressed
                    {
                        //DDDDDDDD DDDDLLLL
                        int distance = source.ReadUInt16();
                        int length = distance & 0xF;
                        distance >>= 4;
                        if (length <= 1)
                        {
                            if (length == 0) // match.Length 16-271
                            {
                                //DDDDDDDD DDDD0000 LLLLLLLL
                                length = source.ReadUInt8() + 16;
                            }
                            else // match.Length 272-65808
                            {
                                //DDDDDDDD DDDD0001 LLLLLLLL LLLLLLLL
                                length = source.ReadUInt16() + 272;
                            }
                        }
                        buffer.BackCopy(distance, length);
                    }
                    else // Not compressed
                    {
                        buffer.WriteByte(source.ReadUInt8());
                    }
                    flag <<= 1;
                    flagbits--;
                }

                if (destination.Position + buffer.Position > endPosition)
                {
                    throw new DecompressedSizeException(decomLength, destination.Position + buffer.Position - (endPosition - decomLength));
                }
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0, matchPointer = 0x0;

            using (PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level))
            using (FlagWriter flag = new FlagWriter(destination, Endian.Big, i => destination.WriteByte((byte)-i)))
            {
                while (sourcePointer < source.Length)
                {
                    if (matchPointer < matches.Count && matches[matchPointer].Offset == sourcePointer)
                    {
                        LzMatch match = matches[matchPointer++];

                        if (match.Length < 16) // match.Length 3-15
                        {
                            flag.Buffer.Write((ushort)(match.Distance << 4 | match.Length));
                        }
                        else if (match.Length < 272) // match.Length 16-271
                        {
                            flag.Buffer.Write((ushort)(match.Distance << 4));
                            flag.Buffer.Write((byte)(match.Length - 16));
                        }
                        else // match.Length 272-65808
                        {
                            flag.Buffer.Write((ushort)(match.Distance << 4 | 1));
                            flag.Buffer.Write((ushort)(match.Length - 272));
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
        }
    }
}
