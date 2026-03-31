using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.Exceptions;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.IO;

namespace AuroraLib.Compression.Formats.Nintendo
{
    /// <summary>
    /// Nintendo LZ10 compression algorithm based on LZ77, mainly used in GBA, DS and WII games.
    /// </summary>
    public class LZ10 : ICompressionAlgorithm, IProvidesDecompressedSize, IGbaRamMode
    {
        private const byte Identifier = 0x10;

        /// <inheritdoc/>
        public virtual IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<LZ10>("Nintendo LZ10", new MediaType(MIMEType.Application, "x-nintendo-lz10"), ".lz");

        internal static readonly LzProperties _lz = new LzProperties(0x1000, 18, 3);

        /// <summary>
        /// Set minDistance to 2 so that is compatible with GBA VRAM.
        /// </summary>
        internal static readonly LzProperties _lzVram = new LzProperties(0x1000, 18, 3, 0, 2);

        /// <inheritdoc/>
        public bool GbaVramCompatibilityMode { get; set; } = true;

        /// <inheritdoc/>
        public virtual bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x8 < stream.Length && stream.Peek(s => Validate(s));

        /// <inheritdoc/>
        public virtual uint GetDecompressedSize(Stream source)
            => source.Peek(InternalGetDecompressedSize);

        protected static uint InternalGetDecompressedSize(Stream source)
        {
            byte identifier = source.ReadUInt8();
            if (identifier != Identifier)
                throw new InvalidIdentifierException(identifier.ToString("X"), Identifier.ToString("X"));
            uint decompressedSize = source.ReadUInt24();
            if (decompressedSize == 0)
                decompressedSize = source.ReadUInt32();

            return decompressedSize;
        }

        /// <inheritdoc/>
        public virtual void Decompress(Stream source, Stream destination)
        {
            uint decompressedSize = InternalGetDecompressedSize(source);
            DecompressHeaderless(source, destination, decompressedSize);
        }

        /// <inheritdoc/>
        public virtual void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionSettings settings = default)
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

            CompressHeaderless(source, destination, settings, GbaVramCompatibilityMode);
        }

        public static void DecompressHeaderless(Stream source, Stream destination, uint decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsBits))
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
            }

            if (destination.Position > endPosition)
            {
                throw new DecompressedSizeException(decomLength, destination.Position - (endPosition - decomLength));
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, CompressionSettings settings = default, bool gbaVramCompatibilityMode = true)
        {
            int sourcePointer = 0x0;
            using LzChainMatchFinder matchFinder = new LzChainMatchFinder(gbaVramCompatibilityMode ? _lzVram : _lz, settings);
            using FlagWriter flag = new FlagWriter(destination, Endian.Big);
            while (true)
            {
                LzMatch match = matchFinder.FindNextBestMatch(source);
                int plain = match.Offset - sourcePointer;
                while (plain != 0)
                {
                    plain--;
                    flag.Buffer.WriteByte(source[sourcePointer++]);
                    flag.WriteBit(false);
                }

                // Last match reached.
                if (match.Length == 0)
                    return;

                flag.Buffer.Write((ushort)((match.Length - 3) << 12 | ((match.Distance - 1) & 0xFFF)), Endian.Big);
                sourcePointer += match.Length;
                flag.WriteBit(true);
            }
        }

        internal static bool Validate(Stream source, uint decompressedSize = 0)
        {
            if (decompressedSize == 0)
            {
                if (source.ReadByte() != Identifier) return false;

                decompressedSize = source.ReadUInt24();
                if (decompressedSize == 0)
                    decompressedSize = source.ReadUInt32();
                if (decompressedSize == 0) return false;
            }

            int i = 3;
            int Buffer = 0;
            FlagReader flag = new FlagReader(source, Endian.Big);
            while (source.Position < source.Length)
            {
                if (flag.Readbit()) // Compressed
                {
                    byte b1 = source.ReadUInt8();
                    byte b2 = source.ReadUInt8();
                    int distance = ((b1 & 0xf) << 8 | b2) + 1;
                    int length = (b1 >> 4) + 3;

                    if (distance > Buffer) return false;
                    if (i == 0) return true;
                    i--;
                    Buffer += length;
                }
                else // Not compressed
                {
                    source.Position++;
                    Buffer++;
                }
            }
            return Buffer == decompressedSize;
        }
    }
}
