using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// LZHudson is an LZ based compression algorithm used in Mario Party 4.
    /// </summary>
    public sealed class LZHudson : ICompressionAlgorithm, ILzSettings
    {
        private static readonly LzProperties _lz = new LzProperties(0x1000, 0xFF + 18, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && extension.Contains(".LZHudson".AsSpan(), StringComparison.InvariantCultureIgnoreCase) && stream.ReadUInt32(Endian.Big) != 0;

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            uint uncompressedSize = source.ReadUInt32(Endian.Big);
            DecompressHeaderless(source, destination, (int)uncompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(source.Length, Endian.Big);
            CompressHeaderless(source, destination, LookAhead, level);
        }

#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {
                FlagReader flag = new FlagReader(source, Endian.Big, 4, Endian.Big);

                while (destination.Position + buffer.Position < endPosition)
                {
                    if (flag.Readbit()) // Compressed?
                    {
                        buffer.WriteByte(source.ReadUInt8());
                    }
                    else
                    {
                        byte b1 = source.ReadUInt8(); // LLLLDDDD
                        byte b2 = source.ReadUInt8(); // DDDDDDDD
                        int distance = (((b1 & 0x0F) << 8) | b2) + 1;
                        int length = ((b1 & 0xF0) >> 4) + 2;
                        if (length == 2)
                        {
                            length = source.ReadUInt8() + 18;
                        }
                        buffer.BackCopy(distance, length);
                    }
                }
            }
        }

#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0;
            LzMatchFinder dictionary = new LzMatchFinder(_lz, lookAhead, level);
            using (FlagWriter flag = new FlagWriter(destination, Endian.Big, 128, 4, Endian.Big))
            {
                while (sourcePointer < source.Length)
                {
                    if (dictionary.TryToFindMatch(source, sourcePointer, out LzMatch match))
                    {
                        int length = match.Length > 17 ? 0 : match.Length - 2;

                        flag.Buffer.WriteByte((byte)((length << 4) | ((match.Distance - 1) >> 8))); // LLLLDDDD
                        flag.Buffer.WriteByte((byte)(match.Distance - 1)); // DDDDDDDD
                        if (length == 0)
                        {
                            flag.Buffer.WriteByte((byte)(match.Length - 18));
                        }
                        sourcePointer += match.Length;
                        flag.WriteBit(false);
                    }
                    else
                    {
                        flag.Buffer.WriteByte(source[sourcePointer++]);
                        flag.WriteBit(true);
                    }
                }
            }
        }
    }
}
