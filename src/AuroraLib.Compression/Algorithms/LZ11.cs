using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo LZ11 compression algorithm
    /// </summary>
    public class LZ11 : ICompressionAlgorithm, ILzSettings
    {
        private static readonly LzProperties _lz = new(0x1000, 0x4000, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public virtual bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && stream.ReadByte() == 0x11 && (stream.ReadUInt24() != 0 || stream.ReadUInt32() != 0) && (stream.ReadUInt8() & 0x80) == 0;

        /// <inheritdoc/>
        public virtual void Decompress(Stream source, Stream destination)
        {
            source.Position += 1;
            int uncompressedSize = source.ReadUInt24();
            if (uncompressedSize == 0)
            {
                uncompressedSize = (int)source.ReadUInt32();
            }
            DecompressHeaderless(source, destination, uncompressedSize);
        }

        /// <inheritdoc/>
        public virtual void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            if (source.Length <= 0xFFFFFF)
            {
                destination.Write(0x11 | (source.Length << 8));
            }
            else
            {
                destination.Write(0x11);
                destination.Write(source.Length);
            }

            CompressHeaderless(source, destination, LookAhead, level);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using LzWindows buffer = new(destination, _lz.WindowsSize);
            FlagReader Flag = new(source, Endian.Big);

            while (destination.Position + buffer.Position < endPosition)
            {
                if (Flag.Readbit())
                {
                    int distance, length;
                    byte b1 = source.ReadUInt8();
                    byte b2 = source.ReadUInt8();
                    if (b1 >> 4 == 0) // match.Length 17-272
                    {
                        //0000LLLL LLLLDDDD DDDDDDDD
                        byte b3 = source.ReadUInt8();
                        distance = ((b2 & 0xf) << 8 | b3) + 1;
                        length = ((b1 & 0xf) << 4 | b2 >> 4) + 17;
                    }
                    else if (b1 >> 4 == 1) // match.Length 273-65808
                    {
                        //0001LLLL LLLLLLLL LLLLDDDD DDDDDDDD
                        byte b3 = source.ReadUInt8();
                        byte b4 = source.ReadUInt8();
                        distance = ((b3 & 0xf) << 8 | b4) + 1;
                        length = ((b1 & 0xf) << 12 | b2 << 4 | b3 >> 4) + 273;
                    }
                    else // match.Length 3-16
                    {
                        //LLLLDDDD DDDDDDDD
                        distance = ((b1 & 0xf) << 8 | b2) + 1;
                        length = (b1 >> 4) + 1;
                    }

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

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0;
            LzMatchFinder dictionary = new(_lz, lookAhead, level);
            using FlagWriter flag = new(destination, Endian.Big);

            while (sourcePointer < source.Length)
            {
                if (dictionary.TryToFindMatch(source, sourcePointer, out LzMatch match))
                {
                    if (match.Length <= 16)  // match.Length 3-16
                    {
                        flag.Buffer.Write((ushort)((match.Length - 1) << 12 | ((match.Distance - 1) & 0xFFF)), Endian.Big);
                    }
                    else if (match.Length <= 272) // match.Length 17-272
                    {
                        flag.Buffer.WriteByte((byte)(((match.Length - 17) & 0xFF) >> 4));
                        flag.Buffer.Write((ushort)((match.Length - 17) << 12 | ((match.Distance - 1) & 0xFFF)), Endian.Big);
                    }
                    else // match.Length 273-65808
                    {
                        flag.Buffer.Write((uint)(0x10000000 | ((match.Length - 273) & 0xFFFF) << 12 | ((match.Distance - 1) & 0xFFF)), Endian.Big);
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
