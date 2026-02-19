using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// CNX2 compression algorithm
    /// </summary>
    public sealed class CNX2 : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {

        private static readonly Identifier32 _identifier = new Identifier32((byte)'C', (byte)'N', (byte)'X', 0x2);

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<CNX2>("CNX2", new MediaType(MIMEType.Application, "x-cnx2"), string.Empty, _identifier);

        private static readonly LzProperties _lz = new LzProperties(0x800, 0x1F + 4, 4);

        /// <summary>
        /// The extension string that is set when writing and reading.
        /// </summary>
        string Extension { get; set; } = "DEC";

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

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
                s.MatchThrow(_identifier);
                s.Position += 8;
                return s.ReadUInt32(Endian.Big);
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            // Read Header
            source.MatchThrow(_identifier);
            Extension = source.ReadString(4, 0x10);
            uint compressedSize = source.ReadUInt32(Endian.Big);
            uint decompressedSize = source.ReadUInt32(Endian.Big);

            // Mark the initial positions of the streams
            long compressedStartPosition = source.Position;

            // Perform the decompression
            DecompressHeaderless(source, destination, (int)decompressedSize);

            // Verify compressed size and handle mismatches
            Helper.TraceIfCompressedSizeMismatch(source.Position - compressedStartPosition, compressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Mark the initial positions of the destination
            long StartPosition = destination.Position;

            // Write Header
            destination.Write(_identifier);
            destination.WriteString(Extension.AsSpan(), 4, 0x10);
            destination.Write(0, Endian.Big); // Compressed length (will be filled in later)
            destination.Write(source.Length, Endian.Big);

            // Perform the compression
            CompressHeaderless(source, destination, LookAhead, level);

            // Go back to the beginning of the file and write out the compressed length
            uint destinationLength = (uint)(destination.Position - StartPosition);
            destination.At(StartPosition + 8, x => x.Write(destinationLength - 16, Endian.Big));
        }

        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            FlagReader flag = new FlagReader(source, Endian.Little);
            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {
                while (destination.Position + buffer.Position < endPosition)
                {
                    int distance, length;
                    switch (flag.ReadInt(2))
                    {
                        // Jump to the next 0x800 boundary
                        case 0:
                            length = source.ReadUInt8();
                            source.Position += length;
                            flag.Reset();
                            break;

                        // Not compressed, single byte
                        case 1:
                            buffer.WriteByte(source.ReadUInt8());
                            break;

                        // Compressed
                        case 2:
                            ushort matchPair = source.ReadUInt16(Endian.Big);
                            distance = (matchPair >> 5) + 1;
                            length = (matchPair & 0x1F) + 4;

                            buffer.BackCopy(distance, length);
                            break;

                        // Not compressed, multiple bytes
                        case 3:
                            length = source.ReadUInt8();
                            buffer.CopyFrom(source, length);
                            break;
                    }
                }
            }

            // Verify decompressed size
            if (destination.Position != endPosition)
            {
                throw new DecompressedSizeException(decomLength, destination.Position - (endPosition - decomLength));
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0, plainSize;

            using (PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level))
            using (FlagWriter flag = new FlagWriter(destination, Endian.Little))
            {
                matches.Add(new LzMatch(source.Length, 0, 0)); // Dummy-Match
                foreach (LzMatch match in matches)
                {
                    plainSize = match.Offset - sourcePointer;

                    while (plainSize != 0)
                    {
                        byte length = (byte)Math.Min(byte.MaxValue, plainSize);
                        if (length == 1)
                        {
                            flag.Buffer.WriteByte(source[sourcePointer]);
                            flag.WriteInt(1, 2);
                        }
                        else
                        {
                            flag.Buffer.WriteByte(length);
                            flag.Buffer.Write(source.Slice(sourcePointer, length));
                            flag.WriteInt(3, 2);
                        }

                        sourcePointer += length;
                        plainSize -= length;
                    }

                    // Match has data that still needs to be processed?
                    if (match.Length != 0)
                    {
                        flag.Buffer.Write((ushort)((((match.Distance - 1) & 0x7FF) << 5) | ((match.Length - 4) & 0x1F)), Endian.Big);
                        sourcePointer += match.Length;
                        flag.WriteInt(2, 2);
                    }
                }
            }
        }
    }
}
