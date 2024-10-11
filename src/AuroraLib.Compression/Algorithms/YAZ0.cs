using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
using AuroraLib.Core.Interfaces;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo Yaz0 compression algorithm successor to the <see cref="Yay0"/> algorithm, used in numerous Nintendo titles from the N64 era to Switch.
    /// </summary>
    public class Yaz0 : ICompressionAlgorithm, ILzSettings, IHasIdentifier, IEndianDependentFormat
    {

        /// <inheritdoc/>
        public virtual IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32("Yaz0".AsSpan());

        internal static readonly LzProperties _lz = new LzProperties(0x1000, 0xff + 0x12, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public Endian FormatByteOrder { get; set; } = Endian.Big;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.Match(_identifier);

        /// <inheritdoc/>
        public virtual void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            uint decompressedSize = source.ReadUInt32(FormatByteOrder);
            _ = source.ReadUInt32(FormatByteOrder);
            _ = source.ReadUInt32(FormatByteOrder);

            long sourceDataStartPosition = source.Position;
            long destinationStartPosition = destination.Position;
            try
            {
                DecompressHeaderless(source, destination, (int)decompressedSize);
            }
            catch (Exception) // try other order
            {
                source.Seek(sourceDataStartPosition, SeekOrigin.Begin);
                destination.Seek(destinationStartPosition, SeekOrigin.Begin);
                decompressedSize = BitConverterX.Swap(decompressedSize);
                DecompressHeaderless(source, destination, (int)decompressedSize);
            }
        }

        /// <inheritdoc/>
        public virtual void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier);
            destination.Write(source.Length, FormatByteOrder);
            destination.Write(0);
            destination.Write(0);
            CompressHeaderless(source, destination, LookAhead, level);
        }

#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            FlagReader flag = new FlagReader(source, Endian.Big);
            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {
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
                        int length = b1 >> 4;

                        if (length == 0)
                            length = source.ReadByte() + 0x12;
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
                        // 2 byte match.Length 3-17
                        if (match.Length < 18)
                        {
                            flag.Buffer.Write((ushort)((match.Distance - 0x1) | ((match.Length - 0x2) << 12)), Endian.Big);
                        }
                        else //3 byte match.Length 18-273
                        {
                            flag.Buffer.Write((ushort)((match.Distance - 0x1) & 0xFFF), Endian.Big); ;
                            flag.Buffer.WriteByte((byte)(match.Length - 0x12));
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
