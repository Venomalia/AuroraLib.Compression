using AuroraLib.Compression.Exceptions;
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
    /// Nintendo LZ10 compression algorithm based on LZ77, mainly used in GBA, DS and WII games.
    /// </summary>
    public class LZ10 : ICompressionAlgorithm, ILzSettings
    {
        private const byte Identifier = 0x10;

        internal static readonly LzProperties _lz = new LzProperties(0x1000, 18, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public virtual bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && stream.ReadByte() == Identifier && (stream.ReadUInt24() != 0 || stream.ReadUInt32() != 0) && (stream.ReadUInt8() & 0x80) == 0;

        /// <inheritdoc/>
        public virtual void Decompress(Stream source, Stream destination)
        {
            source.Position += 1;
            int uncompressedSize = source.ReadUInt24();
            if (uncompressedSize == 0) uncompressedSize = (int)source.ReadUInt32();
            DecompressHeaderless(source, destination, uncompressedSize);
        }

        /// <inheritdoc/>
        public virtual void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
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

#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {
                FlagReader flag = new FlagReader(source, Endian.Big);

                while (destination.Position + buffer.Position < endPosition)
                {
                    if (flag.Readbit()) // Compressed
                    {
                        byte b1 = source.ReadUInt8();
                        byte b2 = source.ReadUInt8();
                        int distance = ((b1 & 0xf) << 8 | b2) + 1;
                        int length = (b1 >> 4) + 3;
                        buffer.BackCopy(distance, length);
                    }
                    else // Not compressed
                    {
                        buffer.WriteByte(source.ReadUInt8());
                    }
                }

                if (destination.Position + buffer.Position > endPosition)
                {
                    throw new DecompressedSizeException(decomLength, destination.Position + buffer.Position - (endPosition - decomLength));
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
            using (FlagWriter flag = new FlagWriter(destination, Endian.Big))
            {
                while (sourcePointer < source.Length)
                {
                    // Search for a match
                    if (dictionary.TryToFindMatch(source, sourcePointer, out LzMatch match))
                    {
                        flag.Buffer.Write((ushort)((match.Length - 3) << 12 | ((match.Distance - 1) & 0xFFF)), Endian.Big);
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
