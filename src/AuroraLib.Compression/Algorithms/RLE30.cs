using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendos Run-Length Encoding algorithm mainly used in GBA, DS games.
    /// </summary>
    public sealed class RLE30 : ICompressionAlgorithm
    {
        private const byte Identifier = 0x30;
        private const int FlagMask = 0x80;
        private const int MinLength = 1;
        private const int MaxLength = 0x7F;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.ReadByte() == Identifier && (s.ReadUInt24() != 0 || s.ReadUInt32() != 0) && s.ReadUInt8() != 0);

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.Position += 1;
            uint uncompressedSize = source.ReadUInt24();
            if (uncompressedSize == 0)
            {
                uncompressedSize = source.ReadUInt32();
            }
            DecompressHeaderless(source, destination, (int)uncompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(Identifier);
            if (source.Length <= 0xFFFFFF)
            {
                destination.Write((UInt24)source.Length);
            }
            else
            {
                destination.Write(new UInt24(0));
                destination.Write(source.Length);
            }

            CompressHeaderless(source, destination);
        }

#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            Span<byte> bytes = stackalloc byte[0xFF];

            while (destination.Position < endPosition)
            {
                int flag = source.ReadByte();
                int length = (flag & MaxLength) + MinLength;
                Span<byte> section = bytes.Slice(0, length);
                if (flag >= FlagMask)
                {
                    section = bytes.Slice(0, length + 2);
                    section.Fill(source.ReadUInt8());
                }
                else
                {
                    if (source.Read(section) != length)
                        throw new EndOfStreamException();
                }
                destination.Write(section);
            }

            if (destination.Position > endPosition)
            {
                throw new DecompressedSizeException(decomLength, destination.Position - (endPosition - decomLength));
            }
        }

#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination)
        {
            int sourcePointer = 0x0;
            RleMatchFinder matchFinder = new RleMatchFinder(3, MaxLength);

            while (sourcePointer < source.Length)
            {
                if (matchFinder.TryToFindMatch(source, sourcePointer, out int duration))
                {
                    destination.WriteByte((byte)(duration - 3 | FlagMask));
                    destination.Write(source[sourcePointer]);
                }
                else
                {
                    destination.WriteByte((byte)(duration - MinLength));
                    destination.Write(source.Slice(sourcePointer, duration));
                }
                sourcePointer += duration;
            }
        }
    }
}
