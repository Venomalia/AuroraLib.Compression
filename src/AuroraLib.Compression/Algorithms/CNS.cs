using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
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
    /// CNS compression algorithm, used in Games from Red Entertainment.
    /// </summary>
    public sealed class CNS : ICompressionAlgorithm, ILzSettings, IHasIdentifier, IProvidesDecompressedSize
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32("@CNS".AsSpan());

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<CNS>("Red Entertainmen CNS", new MediaType(MIMEType.Application, "x-red-cns"), string.Empty, _identifier);

        internal static readonly LzProperties _lz = new LzProperties(0x100, 130, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <summary>
        /// The extension string that is set when writing and reading.
        /// </summary>
        public string Extension = "PAK";

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
                s.Position += 4;
                return s.ReadUInt32(Endian.Little);
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            Extension = source.ReadString(4);
            uint decompressedSize = source.ReadUInt32(Endian.Little);
            source.Position += 4; // 0
            DecompressHeaderless(source, destination, (int)decompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier);
            if (source[0] == 0x0 && source[1] == 0x20 && source[2] == 0xAF && source[3] == 0x30)
            {
                destination.WriteString("TPL".AsSpan(), 4);
            }
            else
            {
                destination.WriteString(Extension.AsSpan(), 4);
            }
            destination.Write(source.Length, Endian.Little);
            destination.Write(0);

            CompressHeaderless(source, destination, LookAhead, level);
        }

        public static void DecompressHeaderless(Stream source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {

                Span<byte> bytes = stackalloc byte[128];

                while (destination.Position + buffer.Position < endPosition)
                {
                    int length = source.ReadUInt8();

                    // The first bit is the flag.
                    if ((length & 0x80) == 0) // Uncompressed 1-127
                    {
                        if (source.Read(bytes.Slice(0, length)) != length)
                        {
                            throw new EndOfStreamException();
                        }
                        buffer.Write(bytes.Slice(0, length));
                    }
                    else // Compressed 3-130
                    {
                        int distance = source.ReadUInt8() + 1;
                        length = (length & 0x7F) + 3;

                        buffer.BackCopy(distance, length);
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
            using (PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level))
            {
                matches.Add(new LzMatch(source.Length, 0, 0)); // Dummy-Match

                foreach (LzMatch match in matches)
                {
                    plainSize = match.Offset - sourcePointer;

                    while (plainSize != 0)
                    {
                        byte length = (byte)Math.Min(127, plainSize);
                        destination.WriteByte(length);
                        destination.Write(source.Slice(sourcePointer, length));
                        sourcePointer += length;
                        plainSize -= length;
                    }

                    // Match has data that still needs to be processed?
                    if (match.Length != 0)
                    {
                        destination.WriteByte((byte)(0x80 | (match.Length - 3)));
                        destination.WriteByte((byte)(match.Distance - 1));
                        sourcePointer += match.Length;
                    }
                }
            }
        }
    }
}
