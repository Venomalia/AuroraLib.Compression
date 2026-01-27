using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.Buffers;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// LZ4 algorithm, similar to LZO focused on decompression speed.
    /// </summary>
    // https://github.com/lz4/lz4/tree/dev/doc
    public sealed partial class LZ4 : ICompressionAlgorithm, ILzSettings, IHasIdentifier
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32((uint)FrameTypes.LZ4FrameHeader);

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<LZ4>("LZ4 Frame Compression", new MediaType(MIMEType.Application, "x-lz4"), ".lz4", _identifier);

        private static readonly LzProperties _lz = new LzProperties(0xFFFF, int.MaxValue, 4);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <summary>
        /// What type of frame should be written
        /// </summary>
        public FrameTypes FrameType = FrameTypes.LZ4FrameHeader;

        /// <summary>
        /// Specifies the maximum size of the data blocks to be written.
        /// </summary>
        public BlockMaxSizes BlockSize = BlockMaxSizes.Block4MB;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Peek(s => s.Position + 0x10 < s.Length && Enum.IsDefined(typeof(FrameTypes), s.ReadUInt32()));

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            uint blockSize;
            while (source.Position < source.Length)
            {
                FrameTypes magic = source.Read<FrameTypes>();
            SwitchStart:
                switch (magic)
                {
                    case FrameTypes.Legacy:
                        blockSize = source.ReadUInt32();
                        do
                        {
                            DecompressBlockHeaderless(source, destination, blockSize);

                            if ((sbyte)source.ReadByte() == -1) // EOF
                                return;
                            source.Position--;

                            blockSize = source.ReadUInt32();
                        } while (!Enum.IsDefined(typeof(FrameTypes), blockSize));
                        Trace.WriteLine("Mixed LZ4 Legacy and LZ4 Frame");
                        magic = (FrameTypes)blockSize;
                        goto SwitchStart;
                    case FrameTypes.LZ4FrameHeader:
                        DecompressLZ4FrameHeader(source, destination);
                        break;
                    case FrameTypes.Skippable0:
                    case FrameTypes.Skippable1:
                    case FrameTypes.Skippable2:
                    case FrameTypes.Skippable3:
                    case FrameTypes.Skippable4:
                    case FrameTypes.Skippable5:
                    case FrameTypes.Skippable6:
                    case FrameTypes.Skippable7:
                    case FrameTypes.Skippable8:
                    case FrameTypes.Skippable9:
                    case FrameTypes.SkippableA:
                    case FrameTypes.SkippableB:
                    case FrameTypes.SkippableC:
                    case FrameTypes.SkippableD:
                    case FrameTypes.SkippableE:
                    case FrameTypes.SkippableF:
                        blockSize = source.ReadUInt32();
                        source.Position += blockSize;
                        break;
                    default: //end
                        source.Position -= 4;
                        return;
                }
            }
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(FrameType);
            switch (FrameType)
            {
                case FrameTypes.Legacy:
                    int sourcePointer = 0x0;
                    while (sourcePointer != source.Length)
                    {
                        long blockStart = destination.Position;
                        destination.Write(0); // Placeholder

                        ReadOnlySpan<byte> blockSource = source.Slice(sourcePointer, Math.Min((int)BlockMaxSizes.Block4MB * 2, source.Length - sourcePointer));
                        CompressBlockHeaderless(blockSource, destination, LookAhead, level);
                        sourcePointer += blockSource.Length;

                        uint thisBlockSize = (uint)(destination.Position - blockStart - 4);
                        destination.At(blockStart, s => s.Write(thisBlockSize));
                    }
                    destination.WriteByte(0xFF); // EOF flag
                    break;
                case FrameTypes.LZ4FrameHeader:
                    CompressLZ4FrameHeader(source, destination, level);
                    break;
                case FrameTypes.Skippable0:
                case FrameTypes.Skippable1:
                case FrameTypes.Skippable2:
                case FrameTypes.Skippable3:
                case FrameTypes.Skippable4:
                case FrameTypes.Skippable5:
                case FrameTypes.Skippable6:
                case FrameTypes.Skippable7:
                case FrameTypes.Skippable8:
                case FrameTypes.Skippable9:
                case FrameTypes.SkippableA:
                case FrameTypes.SkippableB:
                case FrameTypes.SkippableC:
                case FrameTypes.SkippableD:
                case FrameTypes.SkippableE:
                case FrameTypes.SkippableF:
                    destination.Write(source.Length);
                    destination.Write(source);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        public static void DecompressBlockHeaderless(Stream source, Stream destination, uint compressedBlockSize)
        {
            using (LzWindows windows = new LzWindows(destination, _lz.WindowsSize))
            using (SpanBuffer<byte> sourceBlock = new SpanBuffer<byte>(compressedBlockSize))
            {
                source.Read(sourceBlock);
                DecompressBlockHeaderless(sourceBlock, windows);
            }
        }

        public static void DecompressBlockHeaderless(ReadOnlySpan<byte> source, LzWindows buffer)
        {
            int sourcePointer = 0;

            while (sourcePointer < source.Length)
            {
                int token = source[sourcePointer++];

                // Plain copy
                int plainLength = token >> 4;
                ReadExtension(source, ref plainLength, ref sourcePointer);
                buffer.Write(source.Slice(sourcePointer, plainLength));
                sourcePointer += plainLength;

                if (sourcePointer >= source.Length)
                    break;

                // Distance copy
                int matchLength = token & 0xF;
                int matchDistance = source[sourcePointer++] | source[sourcePointer++] << 8;

                ReadExtension(source, ref matchLength, ref sourcePointer);
                buffer.BackCopy(matchDistance, matchLength + 4);
            }
        }

        public static void CompressBlockHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0, plainLength, token;
            // The last sequence contains at last 5 bytes of literals.
            using PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source.Slice(0, source.Length - 5), _lz, lookAhead, level);

            matches.Add(new LzMatch(source.Length, 0, 0)); // Dummy-Match

            foreach (LzMatch match in matches)
            {
                plainLength = match.Offset - sourcePointer;

                // Write token
                token = (plainLength > 0xF ? 0xF : plainLength) << 4;
                if (match.Length != 0)
                    token |= (match.Length - 4 > 0xF ? 0xF : match.Length - 4);
                destination.WriteByte((byte)token);

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
        private static void ReadExtension(ReadOnlySpan<byte> source, ref int length, ref int sourcePointer)
        {
            if (length == 0xF)
            {
                int b;
                while ((b = source[sourcePointer++]) == byte.MaxValue)
                {
                    length += byte.MaxValue;
                }
                length += b;
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
