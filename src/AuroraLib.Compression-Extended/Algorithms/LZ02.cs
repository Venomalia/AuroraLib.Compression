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
    /// LZ02 compression algorithm used in Mario Golf: Toadstool Tour.
    /// </summary>
    public sealed class LZ02 : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {
        private static readonly string[] _extensions = new string[] { ".lz02", string.Empty };

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<LZ02>("LZ02", new MediaType(MIMEType.Application, "x-lz02"), _extensions);

        internal static readonly LzProperties _lz = new LzProperties(0xFFF, 272, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            // Has no distinct header, recognition is inaccurate!
            => (fileNameAndExtension.IsEmpty || PathX.GetExtension(fileNameAndExtension).Contains(_extensions[0].AsSpan(), StringComparison.InvariantCultureIgnoreCase))
#if NET5_0_OR_GREATER
                && stream.Position + 0x8 < stream.Length && stream.Peek(s => Enum.IsDefined(s.Read<DataType>()) && s.Read<UInt24>(Endian.Big) != 0 && (s.ReadByte() & 0xE0) == 0);
#else
                && stream.Position + 0x8 < stream.Length && stream.Peek(s => Enum.IsDefined(typeof(DataType), s.Read<DataType>()) && s.Read<UInt24>(Endian.Big) != 0 && (s.ReadByte() & 0xE0) == 0);
#endif

        private enum DataType : byte
        {
            Default = 0x01,
            Extended = 0x02,
        }

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s => InternalGetDecompressedSize(s, out _));

        private static uint InternalGetDecompressedSize(Stream source, out DataType type)
        {
            type = source!.Read<DataType>();
            if (!Enum.IsDefined(typeof(DataType), type))
                throw new InvalidIdentifierException(type.ToString("X"), "1 or 2");
            return source!.ReadUInt24(Endian.Big);
        }

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            uint uncompressedSize = InternalGetDecompressedSize(source, out DataType type);
            DecompressHeaderless(source, destination, uncompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
            => Compress(source, ReadOnlySpan<byte>.Empty, destination, level);

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, ReadOnlySpan<byte> extendData, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(extendData.IsEmpty ? DataType.Default : DataType.Extended);
            destination.Write((UInt24)source.Length, Endian.Big);
            CompressHeaderless(source, destination, LookAhead, level);
            destination.Write(extendData);
        }

        public static void DecompressHeaderless(Stream source, Stream destination, uint decomLength = 0)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            FlagReader flag = new FlagReader(source, Endian.Big);
            using LzWindows buffer = new LzWindows(destination, _lz.WindowsSize);
            while (source.Position < source.Length)
            {
                if (flag.Readbit()) // Compressed
                {
                    // DDDDLLLL DDDDDDDD
                    byte b1 = source.ReadUInt8();
                    byte b2 = source.ReadUInt8();
                    int distance = (b1 & 0xF0) << 4 | b2;
                    int length = (b1 & 0xF) + 1; // 1-16 length

                    if (length == 1)
                    {
                        if (distance == 0)
                        {
                            if (destination.Position + buffer.Position > endPosition)
                            {
                                throw new DecompressedSizeException(decomLength, destination.Position + buffer.Position - (endPosition - decomLength));
                            }
                            return;
                        }
                        length = source.ReadUInt8() + 17; // 17-272 length
                    }

                    buffer.BackCopy(distance, length);
                }
                else // Not compressed
                {
                    buffer.WriteByte(source.ReadUInt8());
                }
            }
            throw new EndOfStreamException();
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

                    int length = match.Length > 16 ? 0 : match.Length - 1;
                    flag.Buffer.WriteByte((byte)((match.Distance >> 8 << 4) | length));
                    flag.Buffer.WriteByte((byte)(match.Distance & 0xFF));
                    if (length == 0)
                        flag.Buffer.WriteByte((byte)(match.Length - 17));

                    sourcePointer += match.Length;
                    flag.WriteBit(true);

                }
                else
                {
                    flag.Buffer.WriteByte(source[sourcePointer++]);
                    flag.WriteBit(false);
                }
            }
            flag.Buffer.Write(0);
            flag.Buffer.Write(0);
            flag.WriteBit(true);
        }
    }
}
