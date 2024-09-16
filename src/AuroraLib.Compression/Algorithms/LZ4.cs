using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// LZ4 algorithm, similar to LZO focused on decompression speed.
    /// </summary>
    public sealed class LZ4 : ICompressionAlgorithm, ILzSettings
    {
        private static readonly LzProperties _lz = new LzProperties(0xFFFF, int.MaxValue, 4);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && stream.ReadUInt32() != 0 && stream.ReadUInt32() - 8 != stream.Length;

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
            => DecompressHeaderless(source, destination);

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
            => CompressHeaderless(source, destination, LookAhead, level);

        public static void DecompressHeaderless(Stream source, Stream destination)
        {
            while (source.Position < source.Length)
            {
                uint decompressedBlocSize = source.ReadUInt32();
                uint compressedBlockSize = source.ReadUInt32();

                if (compressedBlockSize == 0) break;
                long endPosition = destination.Position + decompressedBlocSize;
                destination.SetLength(endPosition);
                DecompressBlockHeaderless(source, destination, compressedBlockSize);

                if (destination.Position > endPosition)
                {
                    throw new DecompressedSizeException(decompressedBlocSize, destination.Position - (endPosition - decompressedBlocSize));
                }
            }
        }

#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static void DecompressBlockHeaderless(Stream source, Stream destination, uint compressedBlockSize)
        {
            long blockEnd = source.Position + compressedBlockSize;
            if (blockEnd > source.Length)
            {
                throw new EndOfStreamException();
            }

            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {
                while (source.Position < blockEnd)
                {
                    int flag = source.ReadByte();

                    // Plain copy
                    int plainLength = flag >> 4;
                    ReadExtension(source, ref plainLength);
                    buffer.CopyFrom(source, plainLength);

                    if (source.Position >= blockEnd)
                        break;

                    // Distance copy
                    int matchLength = flag & 0xF;
                    int matchDistance = source.ReadUInt16();
                    ReadExtension(source, ref matchLength);
                    buffer.BackCopy(matchDistance, matchLength + 4);
                }
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            long startPos = destination.Position;
            destination.Write(source.Length);
            destination.Write(0);

            CompressBlockHeaderless(source, destination, lookAhead, level);

            uint compressedBlockSize = (uint)(destination.Position - (startPos + 8));
            destination.At(startPos + 4, s => s.Write(compressedBlockSize));
        }

#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static void CompressBlockHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0;
            LzMatchFinder dictionary = new LzMatchFinder(_lz, lookAhead, level);
            while (sourcePointer < source.Length)
            {
                int plainLength = 0;
                LzMatch match = default;
                while (sourcePointer + plainLength < source.Length)
                {
                    if (dictionary.TryToFindMatch(source, sourcePointer + plainLength, out match))
                        break;
                    plainLength++;
                }
                int flag = (match.Length - 4 > 0xF ? 0xF : match.Length - 4) | (plainLength > 0xF ? 0xF : plainLength) << 4;
                destination.WriteByte((byte)flag);

                // Plain copy
                WriteExtension(destination, plainLength);
                destination.Write(source.Slice(sourcePointer, plainLength));
                sourcePointer += plainLength;

                if (sourcePointer >= source.Length)
                    break;

                // Distance copy
                destination.Write((ushort)match.Distance);
                WriteExtension(destination, match.Length - 4);
                sourcePointer += match.Length;
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ReadExtension(Stream stream, ref int length)
        {
            if (length == 0xF)
            {
                int vaule;
                do
                {
                    vaule = stream.ReadByte();
                    length += vaule;
                } while (vaule == 0xFF);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteExtension(Stream stream, int length)
        {
            length -= 0xF;
            if (length >= 0)
            {
                int byteToWrite;
                do
                {
                    byteToWrite = Math.Min(length, 0xFF);
                    stream.WriteByte((byte)byteToWrite);
                    length -= byteToWrite;
                } while (byteToWrite == 0xFF);
            }
        }
    }
}
