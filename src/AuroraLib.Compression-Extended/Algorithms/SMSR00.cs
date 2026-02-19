using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo SMSR00 compression algorithm, mainly used in Yoshi's Story for the Nintendo 64.
    /// </summary>
    public sealed class SMSR00 : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {
        const int headerSize = 0x10;

        private static readonly Identifier _identifier = new Identifier("SMSR00");

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<SMSR00>("Nintendo SMSR00", new MediaType(MIMEType.Application, "x-nintendo-smsr00"), string.Empty, _identifier);

        internal static readonly LzProperties _lz = new LzProperties(0x1000, 18, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = false;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + headerSize < stream.Length && stream.Peek(s => s.Match(_identifier));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                s.Position += 2;
                return s.ReadUInt32(Endian.Big);
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            source.Skip(2);
            uint uncompressedSize = source.ReadUInt32(Endian.Big);
            uint uncompressedDataPointer = source.ReadUInt32(Endian.Big);
            DecompressHeaderless(source, destination, uncompressedSize, (int)(uncompressedDataPointer - source.Position));
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            using MemoryPoolStream uncompressedData = new MemoryPoolStream(0x400);
            using MemoryPoolStream codeData = new MemoryPoolStream(0x1000);

            CompressHeaderless(source, uncompressedData, codeData, LookAhead, level);

            uint startPosition = (uint)destination.Position;
            destination.Write(_identifier.AsSpan());
            destination.Write((ushort)0);
            destination.Write(source.Length, Endian.Big);
            destination.Write((uint)(headerSize + codeData.Length - startPosition), Endian.Big);
            codeData.WriteTo(destination);
            uncompressedData.WriteTo(destination);
        }

        public static void DecompressHeaderless(Stream source, Stream destination, uint decomLength, int uncompressedDataPointer)
        {
            int codesLength = uncompressedDataPointer;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(codesLength);
            try
            {
                source.ReadExactly(buffer, 0, codesLength);
                DecompressHeaderless(source, MemoryMarshal.Cast<byte, ushort>(buffer.AsSpan(0, codesLength)), destination, decomLength);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static void DecompressHeaderless(Stream uncompressed, ReadOnlySpan<ushort> codes, Stream destination, uint decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using LzWindows buffer = new LzWindows(destination, _lz.WindowsSize);

            int codePointer = 0;

            ushort currentMask = 0;
            int maskBitCounter = 0;

            while (destination.Position + buffer.Position < endPosition)
            {
                // If we're out of bits, get the next mask.
                if (maskBitCounter == 0)
                {
                    currentMask = BinaryPrimitives.ReverseEndianness(codes[codePointer++]);
                    maskBitCounter = 16;
                }

                if ((currentMask & 0x8000) == 0x8000)
                {
                    buffer.WriteByte(uncompressed.ReadUInt8());
                }
                else
                {
                    ushort data = BinaryPrimitives.ReverseEndianness(codes[codePointer++]);
                    // Calculate the match distance & length
                    int distance = (data & 0x0FFF) + 1;
                    int length = (data >> 12) + 3;

                    buffer.BackCopy(distance, length);
                }

                // Get the next bit in the mask.
                currentMask <<= 1;
                maskBitCounter--;
            }

            if (destination.Position + buffer.Position > endPosition)
            {
                throw new DecompressedSizeException(decomLength, destination.Position + buffer.Position - (endPosition - decomLength));
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream uncompressedData, Stream codeData, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            using PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level);
            using FlagWriter flag = new FlagWriter(codeData, Endian.Big, 2, Endian.Big);
            MIO0.CompressHeaderless(source, flag.Buffer, uncompressedData, flag, matches);
        }
    }
}
