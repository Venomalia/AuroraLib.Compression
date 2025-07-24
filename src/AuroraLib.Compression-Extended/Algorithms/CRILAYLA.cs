using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// CRILAYLA compression algorithm by CRI Middleware, used in many games built with the CRIWARE toolset.
    /// </summary>
    public sealed class CRILAYLA : ICompressionAlgorithm, ILzSettings, IHasIdentifier, IProvidesDecompressedSize
    {
        const int HeaderSize = 0x100;

        private static readonly LzProperties _lz = new LzProperties(0x2000, ushort.MaxValue, 3, 0, 3);

        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier64 _identifier = new Identifier64(4705233847682945603ul); // CRILAYLA

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<CRILAYLA>("CRI Middleware Crilayla", new MediaType(MIMEType.Application, "x-crilayla"), string.Empty, _identifier);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Length > 0x10 && stream.Peek(s => s.Match(_identifier));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                return s.ReadUInt32() + 0x100;
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);

            uint decompressedSize = source.ReadUInt32();
            uint compressedSize = source.ReadUInt32();

            byte[] destinationBuffer = ArrayPool<byte>.Shared.Rent((int)decompressedSize + HeaderSize);
            byte[] sourceBuffer = ArrayPool<byte>.Shared.Rent((int)compressedSize);
            try
            {
                source.Read(sourceBuffer, 0, (int)compressedSize);
                source.Read(destinationBuffer, 0, HeaderSize); // Read Header

                Span<byte> destinationSpan = destinationBuffer.AsSpan(HeaderSize, (int)decompressedSize);
                Span<byte> sourceSpan = sourceBuffer.AsSpan(0, (int)compressedSize);
                DecompressHeaderless(sourceSpan, destinationSpan);

                destination.Write(destinationBuffer, 0, (int)decompressedSize + HeaderSize);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(destinationBuffer);
                ArrayPool<byte>.Shared.Return(sourceBuffer);
            }
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // That would make the output file larger than the buffer.
            if (level == CompressionLevel.NoCompression)
                level = CompressionLevel.Fastest;

            if (source.Length < HeaderSize)
                throw new ArgumentException($"Source must be at least {HeaderSize} bytes long.");

            byte[] buffer = ArrayPool<byte>.Shared.Rent(source.Length + (int)(source.Length * 0.1));
            try
            {
                int compressedSize = CompressHeaderless(source.Slice(HeaderSize), buffer, LookAhead, level);
                destination.Write(_identifier);
                destination.Write(source.Length - HeaderSize);
                destination.Write(compressedSize);
                destination.Write(buffer, buffer.Length - compressedSize, (int)compressedSize);
                destination.Write(source.Slice(0, HeaderSize));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static void DecompressHeaderless(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            int sourcePointer = source.Length - 1;
            int destinationPointer = destination.Length - 1;
            byte bitBuffer = 0;
            int bitsLeft = 0;
            ReadOnlySpan<int> vleLevels = stackalloc int[4] { 2, 3, 5, 8 };
            ReadOnlySpan<int> vleFlags = stackalloc int[4] { 0x3, 0x7, 0x1F, 0xFF };

            while (destinationPointer >= 0)
            {
                if (GetBits(source, ref sourcePointer, ref bitBuffer, ref bitsLeft, 1) == 1)
                {
                    int distance = GetBits(source, ref sourcePointer, ref bitBuffer, ref bitsLeft, 13) + 3;
                    int length = 3;

                    int vle = 0, value;
                    while (true)
                    {
                        length += value = GetBits(source, ref sourcePointer, ref bitBuffer, ref bitsLeft, vleLevels[vle]);
                        if (value != vleFlags[vle])
                            break;

                        if (vle != 3)
                            vle++;
                    }

                    for (int i = 0; i < length; i++)
                    {
                        destination[destinationPointer] = destination[destinationPointer + distance];
                        destinationPointer--;
                    }
                }
                else
                {
                    destination[destinationPointer--] = (byte)GetBits(source, ref sourcePointer, ref bitBuffer, ref bitsLeft, 8);
                }
            }
        }

        private static ushort GetBits(ReadOnlySpan<byte> input, ref int inputPointer, ref byte flag, ref int flagBitsLeft, int bitCount)
        {
            ushort value = 0;

            while (bitCount > 0)
            {
                if (flagBitsLeft == 0)
                {
                    flag = input[inputPointer--];
                    flagBitsLeft = 8;
                }

                int read = Math.Min(flagBitsLeft, bitCount);

                value <<= read;

                value |= (ushort)(flag >> (flagBitsLeft - read) & ((1 << read) - 1));

                flagBitsLeft -= read;
                bitCount -= read;
            }

            return value;
        }

        public static int CompressHeaderless(ReadOnlySpan<byte> source, byte[] destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int src = 0x0;
            int dst = destination.Length - 1;
            int matchPointer = 0x0;
            byte bitBuffer = 0;
            int bitsLeft = 8;
            ReadOnlySpan<int> vleLevels = stackalloc int[4] { 2, 3, 5, 8 };
            ReadOnlySpan<int> vleFlags = stackalloc int[4] { 0x3, 0x7, 0x1F, 0xFF };

            byte[] reverseSource = ArrayPool<byte>.Shared.Rent(source.Length);
            try
            {
                // Our LZMatcher only works in one direction.
                ReverseSpan(source, reverseSource);
                source = reverseSource.AsSpan(0, source.Length);
                using PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level);

                while (src < source.Length)
                {
                    if (matchPointer < matches.Count && matches[matchPointer].Offset == src)
                    {
                        var match = matches[matchPointer++];

                        SetBits(destination, ref dst, ref bitBuffer, ref bitsLeft, 1, 1); // is match
                        SetBits(destination, ref dst, ref bitBuffer, ref bitsLeft, (ushort)(match.Distance - 3), 13);

                        int vle = 0;
                        int length = match.Length - 3;
                        while (length >= vleFlags[vle])
                        {
                            SetBits(destination, ref dst, ref bitBuffer, ref bitsLeft, (ushort)vleFlags[vle], vleLevels[vle]);
                            length -= vleFlags[vle];

                            if (vle != 3)
                                vle++;
                        }
                        SetBits(destination, ref dst, ref bitBuffer, ref bitsLeft, (ushort)length, vleLevels[vle]);

                        src += match.Length;
                    }
                    else
                    {
                        SetBits(destination, ref dst, ref bitBuffer, ref bitsLeft, 0, 1); // is literal
                        SetBits(destination, ref dst, ref bitBuffer, ref bitsLeft, source[src++], 8);
                    }
                }

                if (bitsLeft != 8)
                    destination[dst--] = bitBuffer;

                return destination.Length - dst - 1;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(reverseSource);
            }

            void ReverseSpan(ReadOnlySpan<byte> source, Span<byte> reversed)
            {
                for (int i = 0; i < source.Length; i++)
                    reversed[i] = source[source.Length - 1 - i];
            }
        }

        private static void SetBits(Span<byte> output, ref int outputPointer, ref byte flag, ref int flagBitsLeft, ushort value, int bitCount)
        {
            while (bitCount > 0)
            {
                if (flagBitsLeft == 0)
                {
                    output[outputPointer--] = flag;
                    flag = 0;
                    flagBitsLeft = 8;
                }

                int write = Math.Min(flagBitsLeft, bitCount);
                int shift = bitCount - write;
                byte bits = (byte)((value >> shift) & ((1 << write) - 1));
                flag |= (byte)(bits << (flagBitsLeft - write));

                flagBitsLeft -= write;
                bitCount -= write;
            }
        }
    }
}
