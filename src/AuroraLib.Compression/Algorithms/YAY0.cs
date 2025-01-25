using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
using AuroraLib.Core.Buffers;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo Yay0 compression algorithm successor to the <see cref="MIO0"/> algorithm with increased match length, used in some Nintendo 64 and GameCube games.
    /// </summary>
    public sealed class Yay0 : ICompressionAlgorithm, ILzSettings, IHasIdentifier, IEndianDependentFormat, IProvidesDecompressedSize
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32("Yay0".AsSpan());

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<Yay0>("Nintendo Yay0", new MediaType(MIMEType.Application, "x-nintendo-yay0"), string.Empty, _identifier);

        internal static readonly LzProperties _lz = new LzProperties(0x1000, 0xff + 0x12, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public Endian FormatByteOrder { get; set; } = Endian.Big;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.Match(_identifier));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                Endian endian = s.DetectByteOrder<uint>(3);
                return s.ReadUInt32(Endian.Big);
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            const int flagDataStart = 0x10;
            uint startPosition = (uint)source.Position;
            source.MatchThrow(_identifier);
            Endian endian = source.DetectByteOrder<uint>(3);
            uint uncompressedSize = source.ReadUInt32(endian);
            uint compressedDataPointer = source.ReadUInt32(endian) + startPosition;
            uint uncompressedDataPointer = source.ReadUInt32(endian) + startPosition;
            DecompressHeaderless(source, destination, uncompressedSize, (int)compressedDataPointer - flagDataStart, (int)uncompressedDataPointer - flagDataStart);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            using (MemoryPoolStream compressedData = new MemoryPoolStream(1024),
                uncompressedData = new MemoryPoolStream(1024),
                flagData = new MemoryPoolStream(512))
            {
                CompressHeaderless(source, compressedData, uncompressedData, flagData, LookAhead, level);

                uint startPosition = (uint)destination.Position;
                destination.Write(_identifier);
                destination.Write(source.Length, FormatByteOrder);
                destination.Write((uint)(0x10 + flagData.Length - startPosition), FormatByteOrder);
                destination.Write((uint)(0x10 + flagData.Length + compressedData.Length - startPosition), FormatByteOrder);
                flagData.WriteTo(destination);
                compressedData.WriteTo(destination);
                uncompressedData.WriteTo(destination);
            }
        }

        public static void DecompressHeaderless(Stream source, Stream destination, uint decomLength, int compressedDataPointer, int uncompressedDataPointer)
        {

            using (SpanBuffer<byte> data = new SpanBuffer<byte>((int)(source.Length - source.Position)))
            {
#if NET20_OR_GREATER
                source.Read(data.GetBuffer(), 0, data.Length);
#else
                source.Read(data);
#endif
                int read = DecompressHeaderless(data, destination, decomLength, compressedDataPointer, uncompressedDataPointer);
                if (source.CanSeek)
                    source.Position -= data.Length - read;
            }
        }

        public static int DecompressHeaderless(ReadOnlySpan<byte> source, Stream destination, uint decomLength, int compressedDataPointer, int uncompressedDataPointer)
        {
            using (Stream flagSource = source.AsReadOnlyStream())
            using (Stream compressedSource = source.Slice(compressedDataPointer).AsReadOnlyStream())
            using (Stream uncompressedSource = source.Slice(uncompressedDataPointer).AsReadOnlyStream())
            {
                FlagReader flag = new FlagReader(flagSource, Endian.Big);
                DecompressHeaderless(flag, compressedSource, uncompressedSource, destination, decomLength);
                return Math.Max(compressedDataPointer + (int)compressedSource.Position, uncompressedDataPointer + (int)uncompressedSource.Position);
            }
        }

        public static void DecompressHeaderless(FlagReader flag, Stream compressedSource, Stream uncompressedSource, Stream destination, uint decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {
                while (destination.Position + buffer.Position < endPosition)
                {
                    if (flag.Readbit())
                    {
                        buffer.WriteByte(uncompressedSource.ReadUInt8());
                    }
                    else
                    {
                        byte b1 = compressedSource.ReadUInt8();
                        byte b2 = compressedSource.ReadUInt8();
                        // Calculate the match distance & length
                        int distance = (((byte)(b1 & 0x0F) << 8) | b2) + 0x1;
                        int length = b1 >> 4;

                        if (length == 0)
                            length = uncompressedSource.ReadByte() + 0x12;
                        else
                            length += 2;

                        buffer.BackCopy(distance, length);
                    }
                }

                if (destination.Position + buffer.Position > endPosition)
                {
                    throw new DecompressedSizeException(decomLength, destination.Position + buffer.Position - (endPosition - decomLength));
                }
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream compressedData, Stream uncompressedData, Stream flagData, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            using (FlagWriter flag = new FlagWriter(flagData, Endian.Big))
            {
                CompressHeaderless(source, compressedData, uncompressedData, flag, lookAhead, level);
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream compressedData, Stream uncompressedData, FlagWriter flag, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0, matchPointer = 0x0;

            using (PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level))
            {
                while (sourcePointer < source.Length)
                {
                    if (matchPointer < matches.Count && matches[matchPointer].Offset == sourcePointer)
                    {
                        LzMatch match = matches[matchPointer++];

                        // 2 byte match.Length 3-17
                        if (match.Length < 18)
                        {
                            compressedData.Write((ushort)((match.Distance - 0x1) | ((match.Length - 0x2) << 12)), Endian.Big);
                        }
                        else //3 byte match.Length 18-273
                        {
                            compressedData.Write((ushort)((match.Distance - 0x1) & 0xFFF), Endian.Big);
                            uncompressedData.Write((byte)(match.Length - 0x12));
                        }
                        sourcePointer += match.Length;
                        flag.WriteBit(false);

                    }
                    else
                    {
                        uncompressedData.Write(source[sourcePointer++]);
                        flag.WriteBit(true);
                    }
                }
            }
        }
    }
}
