using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.Buffers;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// LZShrek compression algorithm used in Shrek Super Slam.
    /// </summary>
    public sealed class LZShrek : ICompressionAlgorithm, ILzSettings
    {
        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<LZShrek>("LZ Shrek", new MediaType(MIMEType.Application, "x-lz-shrek"), string.Empty);

        private static readonly LzProperties _lz = new LzProperties(0x1000, 262, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.Read<int>() == 0x10 && s.Read<int>() != 0 && s.Read<int>() == s.Length - 0x10 && s.Read<int>() == 0);

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            uint offset = source.ReadUInt32();
            uint decomLength = source.ReadUInt32();
            uint compLength = source.ReadUInt32();
            source.Seek(offset, SeekOrigin.Begin);

            using (SpanBuffer<byte> sourceBuffer = new SpanBuffer<byte>(compLength))
            {
#if NET20_OR_GREATER
                source.Read(sourceBuffer.GetBuffer(), 0, sourceBuffer.Length);
#else
                source.Read(sourceBuffer);
#endif
                DecompressHeaderless(sourceBuffer, destination, (int)decomLength);
            }
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(0x10);
            destination.Write(source.Length);
            destination.Write(0x0);
            destination.Write(0x0);

            long start = destination.Position;
            CompressHeaderless(source, destination, LookAhead, level);
            uint compLength = (uint)(destination.Position - start);
            destination.At(start - 8, s => s.Write(compLength));
        }

        public static void DecompressHeaderless(ReadOnlySpan<byte> source, Stream destination, int decomLength)
        {
            long endPosition = destination.Position + decomLength;
            destination.SetLength(endPosition);
            using (LzWindows buffer = new LzWindows(destination, _lz.WindowsSize))
            {
                int sourcePointer = 0;

                while (sourcePointer < source.Length)
                {
                    byte flag = source[sourcePointer++];
                    int compressed = (flag & 7) + 1; // 1-8
                    int uncompressed = ReadDistance(flag, source, ref sourcePointer);

                    if (uncompressed != 0)
                    {
                        buffer.Write(source.Slice(sourcePointer, uncompressed));
                        sourcePointer += uncompressed;
                    }

                    for (int i = 0; i < compressed; i++)
                    {
                        flag = source[sourcePointer++];
                        int length = flag & 7; // 1-7 | 0 flag

                        if (length == 0)
                        {
                            length = source[sourcePointer++];
                            if (length == 0)
                            {
                                if (destination.Position + buffer.Position > endPosition)
                                {
                                    throw new DecompressedSizeException(decomLength, destination.Position + buffer.Position - (endPosition - decomLength));
                                }
                                return; // end~
                            }
                            length += 7; // 7-262
                        }

                        int distance = ReadDistance(flag, source, ref sourcePointer) + 1;

                        buffer.BackCopy(distance, length);
                    }
                }
            }
            throw new EndOfStreamException();
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0;
            List<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level);
            matches.Add(new LzMatch(source.Length, 0, 0)); // Dummy-Match
            using (MemoryPoolStream buffer = new MemoryPoolStream())
            {
                for (int i = 0; i < matches.Count; i++)
                {
                    LzMatch match = matches[i];
                    ReadOnlySpan<byte> uncompressed = source.Slice(sourcePointer, match.Offset - sourcePointer);
                    sourcePointer += uncompressed.Length;
                    int compressedLength = 1;

                    while (match.Length != 0)
                    {
                        // max is DDDDDLLL (LLLLLLLL) ((DDDDDDDD) DDDDDDDD)
                        int lengthflag = match.Length > 7 ? 0 : match.Length;
                        int distanceflag = match.Distance > 30 ? (match.Distance > 286 ? 0x1F : 0x1E) : match.Distance - 1;

                        buffer.WriteByte((byte)(distanceflag << 3 | (lengthflag)));
                        if (lengthflag == 0) // match.Length 8-262
                            buffer.WriteByte((byte)(match.Length - 7));

                        if (distanceflag == 0x1E) // 31-286
                            buffer.WriteByte((byte)(match.Distance - 31));
                        else if (distanceflag == 0x1F) // 287-65822
                            buffer.Write((ushort)(match.Distance - 287));

                        sourcePointer += match.Length;

                        if (compressedLength < 8 && i < matches.Count && matches[i + 1].Offset == sourcePointer)
                        {
                            compressedLength++;
                            match = matches[++i];
                        }
                        else
                            break;
                    }
                    // Write flag and buffer to destination.
                    int uncompressedflag = uncompressed.Length > 29 ? (uncompressed.Length > 285 ? 0x1F : 0x1E) : uncompressed.Length;
                    destination.WriteByte((byte)(uncompressedflag << 3 | compressedLength - 1));

                    if (uncompressedflag == 0x1E) // 30-285
                        destination.WriteByte((byte)(uncompressed.Length - 30));
                    else if (uncompressedflag == 0x1F) // 286-65821
                        destination.Write((ushort)(uncompressed.Length - 286));

                    destination.Write(uncompressed);
                    buffer.WriteTo(destination);
                    buffer.SetLength(0);
                }
            }

            destination.Write(0);
        }

        private static int ReadDistance(byte flag, ReadOnlySpan<byte> source, ref int sourcePointer)
        {
            int distance = flag >> 3; // 0-29 | 30,31 flag
            if (distance == 0x1E) // 30-285
            {
                distance += source[sourcePointer++];
            }
            else if (distance == 0x1F) // 286-65821
            {
                distance = 286;
                distance += source[sourcePointer++];
                distance += (source[sourcePointer++] << 8);
            }
            return distance;
        }
    }
}
