using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo BLZ compression algorithm similar to LZ77 but compresses data backwards, mainly used on the 3ds.
    /// </summary>
    public sealed class BLZ : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {
        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<BLZ>("Nintendo BLZ", new MediaType(MIMEType.Application, "x-blz"), string.Empty);

        private static readonly LzProperties _lz = new LzProperties(0x1000, 18, 3, 0, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => 0x10 < stream.Length && stream.At(stream.Length - 8, s => s.ReadUInt24() == stream.Length && s.ReadByte() >= 8 && s.ReadUInt32() != 0);

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.At(source.Length - 8, s =>
            {
                uint compressedSize = s.ReadUInt24();
                byte headerSize = s.ReadUInt8();
                if (headerSize < 8)
                    throw new Exception("Invalid BLZ header.");
                return s.ReadUInt32() + compressedSize;
            });

        public void Decompress(Stream source, Stream destination)
        {
            source.Position = source.Length - 8;
            UInt24 compressedSize = source.ReadUInt24();
            byte headerSize = source.ReadUInt8();
            int decompressedSize = (source.ReadInt32() + compressedSize);
            int codeSize = compressedSize - headerSize;
            if (headerSize < 8)
                throw new Exception("Invalid BLZ header.");

            source.Position = source.Length - compressedSize;
            byte[] inBuffer = ArrayPool<byte>.Shared.Rent(codeSize);
            byte[] outBuffer = ArrayPool<byte>.Shared.Rent(decompressedSize);
            try
            {
                source.Read(inBuffer, 0, codeSize);
                DecompressHeaderless(inBuffer.AsSpan(0, codeSize), outBuffer.AsSpan(0, decompressedSize));
                destination.Write(outBuffer, 0, decompressedSize);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(inBuffer);
                ArrayPool<byte>.Shared.Return(outBuffer);
            }
        }

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // That would make the output file larger than the buffer.
            if (level == CompressionLevel.NoCompression)
                level = CompressionLevel.Fastest;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(source.Length + (int)(source.Length * 0.1));
            try
            {
                int compressedSize = CompressHeaderless(source, buffer, LookAhead, level);
                destination.Write(buffer, buffer.Length - compressedSize, (int)compressedSize);

                // Write Header + optional padding
                byte headerSize = 8;
                int totalSize = ((int)compressedSize + headerSize);
                int padding = (16 - (totalSize % 16)) % 16;
                totalSize += padding;
                headerSize += (byte)padding;

                destination.Write((byte)0xff, padding);
                destination.Write((UInt24)totalSize);                    // compressed size (24-bit)
                destination.WriteByte(headerSize);                       // header size = 8
                destination.Write((uint)(source.Length - totalSize));  // decompressed size delta
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static void DecompressHeaderless(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            // Start from end (because data is reversed)
            int src = source.Length;
            int dst = destination.Length;
            int flags = 0, mask = 0;

            while (src > 0)
            {
                if ((mask >>= 1) == 0)
                {
                    flags = source[--src];
                    mask = 0x80;
                }

                if ((flags & mask) == 0)
                {
                    destination[--dst] = source[--src]; // literal
                }
                else
                {
                    int info = (source[--src] << 8) | source[--src];
                    int distance = (info & 0x0FFF) + 3;
                    int length = ((info >> 12) & 0xF) + 3;

                    for (int i = 0; i < length && dst > 0; i++)
                    {
                        destination[dst - 1] = destination[dst - 1 + distance];
                        dst--;
                    }
                }
            }

            // Verify decompressed size
            if (dst != 0)
            {
                throw new DecompressedSizeException(destination.Length, destination.Length - dst);
            }
        }

        public static int CompressHeaderless(ReadOnlySpan<byte> source, byte[] destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Destination start from end (because data is reversed)
            int src = 0x0, dst = destination.Length - 2, matchPointer = 0x0, flag = 1, flagPos = destination.Length - 1, lzCode;

            byte[] reverseSource = ArrayPool<byte>.Shared.Rent(source.Length);
            try
            {
                // Our LZMatcher only works in one direction.
                ReverseSpan(source, reverseSource);
                source = reverseSource.AsSpan(0, source.Length);
                using PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level);

                while (src < source.Length)
                {

                    flag <<= 1;
                    if (matchPointer < matches.Count && matches[matchPointer].Offset == src)
                    {
                        LzMatch match = matches[matchPointer++];
                        lzCode = (ushort)((match.Length - 3) << 12 | ((match.Distance - 3) & 0xFFF));
                        destination[dst--] = (byte)(lzCode >> 8);
                        destination[dst--] = (byte)lzCode;
                        src += match.Length;
                        flag |= 1;

                    }
                    else
                    {
                        destination[dst--] = source[src++]; // Literal
                    }

                    // Do we have to write the flag?
                    if ((flag & 0x100) != 0)
                    {
                        destination[flagPos] = (byte)flag;
                        flag = 1;
                        flagPos = dst--;
                    }
                }

                // Do we still have to write a flag?
                if (flag != 1)
                    destination[flagPos] = (byte)flag;
                else
                    dst++; // No flag written, one step back!
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(reverseSource);
            }
            return destination.Length - dst - 1;

            void ReverseSpan(ReadOnlySpan<byte> source, Span<byte> reversed)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    reversed[i] = source[source.Length - 1 - i];
                }
            }
        }
    }
}
