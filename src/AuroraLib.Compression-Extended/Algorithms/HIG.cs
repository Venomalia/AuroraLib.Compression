using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Exceptions;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// High Impact Games WAD compression, similar to LZO.
    /// </summary>
    public sealed class HIG : ICompressionAlgorithm, IProvidesDecompressedSize, ILzSettings
    {
        private static readonly Identifier32 _identifier = new Identifier32("HIG!".AsSpan());

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<HIG>("High Impact Games WAD", new MediaType(MIMEType.Application, "x-hig-wad"), ".wad", _identifier);


        private static readonly LzProperties _lz = new LzProperties(0x7FFF, ushort.MaxValue, 4);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <summary>
        /// v5 == Ratchet and Clank: Size Matters (PS2)
        /// v6 == Ratchet and Clank: Size Matters (PSP)
        /// </summary>
        public int Version { get; set; } = 6;

        /// <summary>
        /// Full path to conf file stored for versions 5 and 6.
        /// </summary>
        public string FullPath { get; set; } = "C:\\HIG\\PROJECTS\\test.mb.wad.conf";

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x8 < stream.Length && stream.Peek(s => s.Match(_identifier));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                s.Skip(0x38);
                return s.ReadUInt32LittleEndian();
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            // Mark the initial positions of the streams
            long startPosition = source.Position;

            // Read Header
            Span<int> header = stackalloc int[16]; // Most of the data is still unknown!
            source.Read(header);

            if (header[0] != (int)_identifier)
                throw new InvalidIdentifierException(header[0].AsBytes(), _identifier.AsSpan());

            int compressedSize = 0;
            int compressedStart = header[1];
            Version = header[14];
            uint decompressedSize = (uint)header[15];

            // Read extension header 0x40 - 0x80
            if (header[14] == 5 || header[14] == 6)
            {
                compressedStart = 0xC0;
                compressedSize = source.ReadInt32LittleEndian();
                FullPath = source.ReadString(0x7C);
            }

            // Perform the decompression
            source.Seek(compressedStart, SeekOrigin.Begin);
            DecompressHeaderless(source, destination, decompressedSize);

            // Verify compressed size if present
            if (compressedSize != 0)
                Helper.TraceIfCompressedSizeMismatch(source.Position - compressedStart - startPosition, compressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Mark the initial positions of the destination
            long destinationStartPosition = destination.Position;

            // Write header 0x40
            Span<int> header = stackalloc int[16]; // Most of the data is still unknown! These files will probably not be accepted by a game!
            header[0] = (int)_identifier;
            header[1] = (Version == 5 | Version == 6) ? 0 : 0x40;
            header[13] = 0; // Hash?
            header[14] = Version;
            header[15] = source.Length;
            destination.Write<int>(header);

            // Write extension header 0x80
            if (header[14] == 5 || header[14] == 6)
            {
                destination.Write(0); // Compressed length (will be filled in later)
                destination.WriteString(FullPath.AsSpan(), 0x7C);
            }

            // Perform the compression
            CompressHeaderless(source, destination, LookAhead, level);

            // Go back to the beginning of the file and write out the compressed length
            if (header[14] == 5 || header[14] == 6)
            {
                int compressedSize = (int)(destination.Position - (destinationStartPosition + 0x40 + 0x80));
                destination.At(destinationStartPosition + 0x40, x => x.Write(compressedSize));
            }
        }

        public static void DecompressHeaderless(Stream source, Stream destination, uint decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);

            int b, length, distance, plain;

            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {
                // --- Initial RAW block ---
                plain = source.ReadByte() + 2;
                if (plain == 2)
                    plain = source.ReadUInt16LittleEndian();
                buffer.CopyFrom(source, plain);

                while (destination.Position + buffer.Position < endPosition)
                {
                    // --- Dictionary block --- 
                    b = source.ReadUInt8();
                    length = b >> 5;
                    if (length < 6)
                    {
                        // >LLLD DDPP> DDDD DDDD
                        length += 4;
                        distance = (b & 0b11100) << 6;
                        plain = b & 0x03;
                    }
                    else
                    {
                        if (length == 6)
                        {
                            // >110L LLLL> DDDD DDPP DDDD DDDD
                            length = (b & 0x1F) + 4;
                            distance = 0;
                        }
                        else // length == 7
                        {
                            // >111D LLLL> ... DDDD DDPP DDDD DDDD
                            length = (b & 0b1111) + 3;
                            distance = (b & 0b1_0000) << 10;
                            if (length == 3)
                            {
                                // 111D 0000 >LLLL LLLL> ... DDDD DDPP DDDD DDDD
                                length = source.ReadUInt8() + 18;
                                if (length == 18)
                                {
                                    // 111D 0000 0000 0000 >LLLL LLLL LLLL LLLL> DDDD DDPP DDDD DDDD
                                    length = source.ReadUInt16BigEndian();
                                }
                            }
                        }
                        // ... >DDDD DDPP> DDDD DDDD
                        b = source.ReadUInt8(); // second last byte
                        distance |= ((b & 0b11111100) << 6);
                        plain = b & 0x03;
                    }
                    distance |= source.ReadUInt8(); // last byte.
                    buffer.BackCopy(distance, length);

                    // --- RAW block --- 
                    switch (plain)
                    {
                        case 0:
                            plain = source.ReadByte() + 2;
                            if (plain == 2)
                                plain = source.ReadUInt16LittleEndian();
                            buffer.CopyFrom(source, plain);
                            break;
                        case 1:
                            buffer.WriteByte(source.ReadUInt8());
                            break;
                        case 2:
                            buffer.WriteByte(source.ReadUInt8());
                            buffer.WriteByte(source.ReadUInt8());
                            break;
                        default: // 3 == nothing to do for us
                            break;
                    }
                }
            }

            // Verify decompressed size
            if (destination.Position > endPosition)
            {
                throw new DecompressedSizeException(decomLength, destination.Position - (endPosition - decomLength));
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            using PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, false, level);
            matches.Add(new LzMatch(source.Length, 0, 0)); // Dummy-Match

            // --- Initial RAW block ---
            int plain = matches[0].Offset;

            // Must be at least 2 bytes
            if (plain < 2)
            {
                matches[0] = new LzMatch(2, matches[0].Distance, matches[0].Length - (plain+1));
                plain = 2;
                if (matches[0].Length < _lz.MinLength)
                {
                    matches.RemoveAt(0);
                    plain = matches[0].Offset;
                }
            }

            if (plain <= byte.MaxValue + 2)
            {
                destination.WriteByte((byte)(plain - 2));
            }
            else
            {
                destination.WriteByte(byte.MinValue);
                destination.Write((ushort)plain);
            }
            destination.Write(source.Slice(0, plain));

            int sourcePointer = plain;
            for (int i = 0; i < matches.Count - 1; i++)
            {
                // --- Dictionary block --- 
                LzMatch match = matches[i];
                plain = matches[i + 1].Offset - (match.Offset + match.Length);
                int b = plain switch
                {
                    0 => 3, // 0 byte
                    1 => 1, // 1 byte
                    2 => 2, // 2 bytes
                    _ => 0, // X bytes
                };

                if (match.Distance <= 0x7FF && match.Length <= 5 + 4)
                {
                    // >LLLD DDPP> DDDD DDDD
                    b |= (match.Length - 4) << 5 | (match.Distance >> 6 & 0b1_1100);
                    destination.WriteByte((byte)b);
                }
                else
                {
                    if (match.Distance <= 0x3FFF && match.Length <= 31 + 4)
                    {
                        // >110L LLLL> DDDD DDPP DDDD DDDD
                        destination.WriteByte((byte)(0b1100_0000 | (match.Length - 4)));
                    }
                    else
                    {
                        // >111D LLLL> ... DDDD DDPP DDDD DDDD
                        int length = match.Length <= 15 + 3 ? match.Length - 3 : 0;
                        destination.WriteByte((byte)(0b1110_0000 | (match.Distance >> 10 & 0b1_0000) | length));
                        if (length == 0)
                        {
                            if (match.Length <= byte.MaxValue + 18)
                            {
                                // 111D 0000 >LLLL LLLL> ... DDDD DDPP DDDD DDDD
                                destination.WriteByte((byte)(match.Length - 18));
                            }
                            else
                            {
                                // 111D 0000 0000 0000 >LLLL LLLL LLLL LLLL> DDDD DDPP DDDD DDDD
                                destination.WriteByte(0);
                                destination.Write((ushort)match.Length, Endian.Big);
                            }
                        }
                    }
                    // ... >DDDD DDPP> DDDD DDDD
                    b |= match.Distance >> 6 & 0b1111_1100;
                    destination.WriteByte((byte)b); // second last byte
                }
                destination.WriteByte((byte)match.Distance); // last byte.
                sourcePointer += match.Length;

                // --- RAW block --- 
                if (plain != 0)
                {
                    if (plain > 2)
                    {
                        if (plain <= byte.MaxValue + 2)
                        {
                            destination.WriteByte((byte)(plain - 2));
                        }
                        else
                        {
                            destination.WriteByte(byte.MinValue);
                            destination.Write((ushort)plain);
                        }
                    }

                    destination.Write(source.Slice(sourcePointer, plain));
                    sourcePointer += plain;
                }
            }
        }
    }
}
