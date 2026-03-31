using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
using AuroraLib.Core.Exceptions;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;

namespace AuroraLib.Compression.Formats.WayForward
{
    /// <summary>
    /// WayForward's LZ algorithm, focused on decompression speed.
    /// </summary>
    public sealed class WFLZ : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize, IEndianDependentFormat
    {
        private static readonly LzProperties _lz = new LzProperties(ushort.MaxValue, byte.MaxValue, 4 + 1);

        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32("WFLZ".AsSpan());

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<WFLZ>("WayForward LZ", new MediaType(MIMEType.Application, "x-wflz"), string.Empty, _identifier);
        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public Endian FormatByteOrder { get; set; } = Endian.Little;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.Match(_identifier));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                var header = s.Read<Header>();
                if (header.Identifier != _identifier)
                    throw new InvalidIdentifierException(header.Identifier.AsSpan(), _identifier.AsSpan());
                return FormatByteOrder == Endian.Little ? header.DecompressedSize : BinaryPrimitives.ReverseEndianness(header.DecompressedSize);
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            // Read Header
            var header = source.Read<Header>();
            if (header.Identifier != _identifier)
                throw new InvalidIdentifierException(header.Identifier.AsSpan(), _identifier.AsSpan());
            int compressedSize = FormatByteOrder == Endian.Little ? (int)header.CompressedSize : BinaryPrimitives.ReverseEndianness((int)header.CompressedSize);
            int decompressedSize = FormatByteOrder == Endian.Little ? (int)header.DecompressedSize : BinaryPrimitives.ReverseEndianness((int)header.DecompressedSize);

            // set destination length
            long endPosition = destination.Position + decompressedSize;
            destination.SetLength(endPosition);

            // Decompress
            byte[] buffer = ArrayPool<byte>.Shared.Rent(compressedSize);
            try
            {
                source.ReadExactly(buffer, 0, compressedSize);
                DecompressHeaderless(buffer.AsSpan(0, compressedSize), destination, FormatByteOrder);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            // Verify decompressed size
            if (destination.Position != endPosition)
            {
                throw new DecompressedSizeException(decompressedSize, destination.Position - (endPosition - decompressedSize));
            }
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionSettings settings = default)
        {
            // Mark the initial positions of the destination
            long destinationStartPosition = destination.Position;

            // Write Header
            destination.Write(_identifier);
            destination.Write(0); // Compressed length (will be filled in later)
            destination.Write(source.Length, FormatByteOrder);

            // Perform the compression
            CompressHeaderless(source, destination, FormatByteOrder, LookAhead, settings);

            // Go back to the beginning of the file and write out the compressed length
            int destinationLength = (int)(destination.Position - destinationStartPosition - 12);
            destination.At(destinationStartPosition + 4, x => x.Write(destinationLength, FormatByteOrder));
        }

        internal readonly struct Header
        {
            public readonly Identifier32 Identifier;
            public readonly uint CompressedSize;
            public readonly uint DecompressedSize;
        }

        private readonly struct WFLZ_Block : IReversibleEndianness<WFLZ_Block>
        {
            public readonly ushort Dist;
            public readonly byte Length;
            public readonly byte Plain;

            public WFLZ_Block(ushort dist, byte length, byte numLiterals)
            {
                Dist = dist;
                Length = length;
                Plain = numLiterals;
            }

            public WFLZ_Block ReverseEndianness() => new WFLZ_Block(BinaryPrimitives.ReverseEndianness(Dist), Length, Plain);
        }

        public static void DecompressHeaderless(ReadOnlySpan<byte> source, Stream destination, Endian order = Endian.Little)
        {
            int sourcePointer = 0x0;

            using LzWindows buffer = new LzWindows(destination, _lz.WindowsBits);
            (ushort Dist, ushort Length, ushort Plain) block;
            while (true)
            {
                block.Dist = order == Endian.Little ? BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(sourcePointer)) : BinaryPrimitives.ReadUInt16BigEndian(source.Slice(sourcePointer));
                block.Length = source[sourcePointer + 2];
                block.Plain = source[sourcePointer + 3];
                sourcePointer += 4;

                if (block.Length != 0)
                {
                    block.Length += 4;
                    buffer.BackCopy(block.Dist, block.Length);
                }
                else if (block.Plain == 0)
                {
                    return;
                }

                if (block.Plain != 0)
                {
                    buffer.Write(source.Slice(sourcePointer, block.Plain));
                    sourcePointer += block.Plain;
                }
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, Endian order = Endian.Little, bool lookAhead = true, CompressionSettings settings = default)
        {

            int sourcePointer = 0x0;

            using LzChainMatchFinder matchFinder = new LzChainMatchFinder(_lz, settings, !lookAhead);

            LzMatch match = default, next = matchFinder.FindNextBestMatch(source);
            int plain = next.Offset;
            while (true)
            {
                var block = new WFLZ_Block((ushort)match.Distance, match.Length == 0 ? byte.MinValue : (byte)(match.Length - 4), (byte)Math.Min(plain, byte.MaxValue));
                destination.Write(order == Endian.Little ? block : block.ReverseEndianness());
                plain -= block.Plain;

                sourcePointer += match.Length;
                destination.Write(source.Slice(sourcePointer, block.Plain));
                sourcePointer += block.Plain;

                if (plain == 0)
                {
                    if (sourcePointer == source.Length)
                        break;

                    match = next;
                    next = matchFinder.FindNextBestMatch(source);
                    plain = next.Offset - (match.Offset + match.Length);
                }
                else
                {
                    match = default;
                }
            }
            //end block
            destination.Write(default(WFLZ_Block));
        }

    }
}
