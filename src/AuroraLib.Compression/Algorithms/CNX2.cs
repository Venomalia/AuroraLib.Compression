using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// CNX2 compression algorithm
    /// </summary>
    public sealed class CNX2 : ICompressionAlgorithm, ILzSettings, IHasIdentifier
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

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
        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            Extension = source.ReadString(4, 0x10);
            long endPosition = source.Position + source.ReadUInt32(Endian.Big) + 8;
            uint decompressedSize = source.ReadUInt32(Endian.Big);
            DecompressHeaderless(source, destination, (int)decompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            long StartPosition = destination.Position;
            destination.Write(_identifier);
            destination.WriteString(Extension.AsSpan(), 4, 0x10);
            destination.Write(0, Endian.Big); // Compressed length (will be filled in later)
            destination.Write(source.Length, Endian.Big);

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

                if (destination.Position + buffer.Position > endPosition)
                {
                    throw new DecompressedSizeException(decomLength, destination.Position + buffer.Position - (endPosition - decomLength));
                }
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0, plainSize;
            List<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level);
            matches.Add(new LzMatch(source.Length, 0, 0)); // Dummy-Match

            using (FlagWriter flag = new FlagWriter(destination, Endian.Little))
            {
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
