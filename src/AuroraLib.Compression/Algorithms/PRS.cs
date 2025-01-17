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

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// SEGA PRS is an LZ based compression algorithm.
    /// </summary>
    public sealed class PRS : ICompressionAlgorithm, ILzSettings, IEndianDependentFormat
    {
        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<PRS>("SEGA PRS", new MediaType(MIMEType.Application, "x-sega-prs"), ".prs");

        private static readonly LzProperties _lz = new LzProperties(0x1FFF, 0x100, 2);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public Endian FormatByteOrder { get; set; } = Endian.Big;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x8 < stream.Length && GetByteOrder(stream).HasValue;

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
            => DecompressHeaderless(source, destination);

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
            => CompressHeaderless(source, destination, FormatByteOrder, LookAhead, level);

        public static void DecompressHeaderless(Stream source, Stream destination)
        {
            Endian detected = GetByteOrder(source) == Endian.Big ? Endian.Big : Endian.Little;
            long sourcePos = source.Position;
            long destinationPos = destination.Position;
            try
            {
                DecompressHeaderless(source, destination, detected);
            }
            catch (Exception)
            {
                source.Seek(sourcePos, SeekOrigin.Begin);
                destination.Seek(destinationPos, SeekOrigin.Begin);
                DecompressHeaderless(source, destination, detected == Endian.Big ? Endian.Little : Endian.Big);
            }
        }

        public static void DecompressHeaderless(Stream source, Stream destination, Endian order)
        {
            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {
                FlagReader Flag = new FlagReader(source, order);

                while (source.Position < source.Length)
                {
                    if (Flag.Readbit())  // Uncompressed value
                    {
                        buffer.WriteByte(source.ReadUInt8());
                    }
                    else
                    {
                        int distance, length;
                        if (Flag.Readbit()) // Compressed value D 1-0x2000 L 3-0x100
                        {
                            distance = source.ReadUInt16(order);

                            if (distance == 0)
                            {
                                return;
                            }

                            length = distance & 7;
                            distance = 0x2000 - (distance >> 3);
                            if (length == 0) // L 1-0x100
                            {
                                length = source.ReadUInt8() + 1;
                            }
                            else // L 3-9
                            {
                                length += 2;
                            }
                        }
                        else // Compressed value D 1-0x100 L 2-5
                        {
                            length = Flag.ReadInt(2, true) + 2;
                            distance = 0x100 - source.ReadUInt8();
                        }
                        buffer.BackCopy(distance, length);
                    }
                }
            }
            throw new EndOfStreamException();
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, Endian order = Endian.Little, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0, matchPointer = 0;
            List<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level);

            using (FlagWriter flag = new FlagWriter(destination, order))
            {
                while (sourcePointer < source.Length)
                {
                    LzMatch match;
                    if (matchPointer < matches.Count && matches[matchPointer].Offset == sourcePointer)
                        match = matches[matchPointer++];
                    else
                        match = default;

                    if (match.Length == 0 || (match.Length == 2 && match.Distance > 0x100))
                    {
                        flag.Buffer.WriteByte(source[sourcePointer++]);
                        flag.WriteBit(true);
                    }
                    else
                    {
                        sourcePointer += match.Length;
                        int distance = match.Distance * -1;
                        int length = match.Length;

                        flag.WriteBit(false);
                        if ((distance >= -0x100) && (length <= 5)) // Compressed value D 1-0x100 L 2-5
                        {
                            flag.WriteBit(false);
                            flag.WriteInt(length - 2, 2, true);
                            flag.Buffer.WriteByte((byte)distance);
                            flag.FlushIfNecessary();
                        }
                        else
                        {
                            if (length > 9) // L 1-0x100
                            {
                                flag.Buffer.Write((ushort)(distance << 3), order);
                                flag.Buffer.WriteByte((byte)(length - 1));
                            }
                            else // L 3-9
                            {
                                flag.Buffer.Write((ushort)((distance << 3) | (length - 2)), order);
                            }
                            flag.WriteBit(true);
                        }
                    }
                }
                flag.WriteBit(false);
                flag.Buffer.WriteByte(0);
                flag.Buffer.WriteByte(0);
                flag.WriteBit(true);
            }
        }

        internal static Endian? GetByteOrder(Stream stream)
        {
            byte flag = stream.PeekByte();
            if (flag > 12 && (flag & 0x1) == 1 && ValidateByteOrder(stream, Endian.Little))
                return Endian.Little;

            if ((flag & 128) == 128 && ValidateByteOrder(stream, Endian.Big))
                return Endian.Big;
            return null;
        }

        internal static bool ValidateByteOrder(Stream stream, Endian order)
        {
            long startPos = stream.Position;
            FlagReader Flag = new FlagReader(stream, order);
            int Buffer = 0;
            while (stream.Position < stream.Length)
            {
                if (Flag.Readbit())  // Uncompressed value
                {
                    stream.Position++;
                    Buffer++;
                }
                else
                {
                    int distance;
                    if (Flag.Readbit()) // Compressed value D 1-0x2000 L 3-0x100
                    {
                        distance = stream.ReadUInt16(order);
                        distance = 0x2000 - (distance >> 3);
                    }
                    else // Compressed value D 1-0x100 L 2-5
                    {
                        _ = Flag.ReadInt(2, true) + 2;
                        distance = 0x100 - stream.ReadUInt8();
                    }
                    stream.Position = startPos;
                    return distance < Buffer;
                }
            }
            stream.Position = startPos;
            return false;
        }
    }
}
