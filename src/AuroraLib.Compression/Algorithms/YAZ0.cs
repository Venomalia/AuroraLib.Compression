using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo Yaz0 compression algorithm
    /// </summary>
    public class Yaz0 : ICompressionAlgorithm, ILzSettings, IHasIdentifier
    {

        public virtual IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new("Yaz0");

        internal static readonly LzProperties _lz = new(0x1000, 0xff + 0x12, 3);

        public bool LookAhead { get; set; } = true;

        public Endian EndianOrder = Endian.Big;

        public virtual bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.Match(_identifier);

        public virtual void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            Endian endian = source.DetectByteOrder<uint>(3);
            uint decompressedSize = source.ReadUInt32(endian);
            uint compressedDataOffset = source.ReadUInt32(endian);
            uint uncompressedDataOffset = source.ReadUInt32(endian);

            DecompressHeaderless(source, destination, (int)decompressedSize);
        }

        public virtual void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier);
            destination.Write(source.Length, EndianOrder);
            destination.Write(0);
            destination.Write(0);
            CompressHeaderless(source, destination, LookAhead, level);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            FlagReader flag = new(source, Endian.Big);
            using LzWindows buffer = new(destination, _lz.WindowsSize);

            while (destination.Position + buffer.Position < endPosition)
            {
                if (flag.Readbit())
                {
                    buffer.WriteByte(source.ReadUInt8());
                }
                else
                {
                    byte b1 = source.ReadUInt8();
                    byte b2 = source.ReadUInt8();
                    // Calculate the match distance & length
                    int distance = (((byte)(b1 & 0x0F) << 8) | b2) + 0x1;
                    int length = (b1 & 0xF0) == 0 ? source.ReadByte() + 0x12 : (byte)((b1 & 0xF0) >> 4) + 0x2;
                    buffer.BackCopy(distance, length);
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
                // Search for a match
                if (dictionary.TryToFindMatch(source, sourcePointer, out LzMatch match))
                {
                    // 2 byte match.Length 3-17
                    if (match.Length < 18)
                    {
                        flag.Buffer.Write((ushort)((match.Distance - 0x1) | ((match.Length - 0x2) << 12)), Endian.Big);
                    }
                    else //3 byte match.Length 18-273
                    {
                        flag.Buffer.Write((ushort)((match.Distance - 0x1) & 0xFFF), Endian.Big); ;
                        flag.Buffer.Write((byte)(match.Length - 0x12));
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
