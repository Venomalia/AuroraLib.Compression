using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// FastLZ is a fast and simple compression algorithm by Ariya Hidayat
    /// </summary>
    public sealed class FastLZ : ICompressionAlgorithm, ILzSettings
    {
        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<FastLZ>("FastLZ stream", new MediaType(MIMEType.Application, "x-fastlz"), string.Empty);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        private static readonly LzProperties _lz1 = new LzProperties(0x2000, byte.MaxValue + 3 + 6, 3);
        private static readonly LzProperties[] _lz2 = new LzProperties[]
        {
            new LzProperties(0x1FFF, int.MaxValue, 3), // Short Match:
            new LzProperties(0x11FFF, int.MaxValue, 5) // Long Match: min match length 3, 4 is possible but does not save space.
        };


        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x4 < stream.Length && stream.Peek(Validate);

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            int length = (int)(source.Length - source.Position);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                source.ReadExactly(buffer, 0, length);
                DecompressHeaderless(buffer.AsSpan(0, length), destination);
            }
            finally
            {
                ArrayPool<byte>.Shared?.Return(buffer);
            }
        }

        public static void DecompressHeaderless(ReadOnlySpan<byte> source, Stream destination)
        {
            // The level is stored in ctrl of the first block.
            int level = (source[0] >> 5) + 1;
            if (level == 1) DecompressHeaderless_Level1(source, destination);
            else if (level == 2) DecompressHeaderless_Level2(source, destination);
            else throw new InvalidDataException($"Unknown FastLZ level {level}.");
        }

        public static void DecompressHeaderless_Level1(ReadOnlySpan<byte> source, Stream destination)
        {
            using LzWindows buffer = new LzWindows(destination, _lz1.WindowsBits);
            int pointer = 0;
            int ctrl = source[pointer++] & 0b0001_1111;

            while (true)
            {
                if (ctrl >= 0b10_0000)
                {
                    // --- Match Block ---
                    // Short Match:  LLLD DDDD DDDD DDDD
                    // Long Match:   111D DDDD LLLL LLLL DDDD DDDD
                    int len = (ctrl >> 5) - 1;
                    int ofs = (ctrl & 31) << 8;

                    if (len == 7 - 1)
                        len += source[pointer++];

                    ofs |= source[pointer++];

                    buffer.BackCopy(ofs + 1, len + 3);
                }
                else
                {
                    // --- Literal Run ---
                    // 000L LLLL
                    ctrl++;
                    buffer.Write(source.Slice(pointer, ctrl));
                    pointer += ctrl;
                }

                // If source end
                if (pointer >= source.Length)
                    return;

                // --- Next Flag --- 
                ctrl = source[pointer++];
            }
        }

        public static void DecompressHeaderless_Level2(ReadOnlySpan<byte> source, Stream destination)
        {
            using LzWindows buffer = new LzWindows(destination, _lz2[1].WindowsBits);
            int pointer = 0;
            int ctrl = source[pointer++] & 0b0001_1111;

            while (true)
            {
                if (ctrl >= 0b10_0000)
                {
                    // --- Match Block ---
                    // Short Match:  LLLD DDDD DDDD DDDD
                    // Length Extension: 111D DDDD LLLL LLLL ... DDDD DDDD
                    // Long Match:   1111 1111 ... 1111 1111 DDDD DDDD DDDD DDDD

                    int len = (ctrl >> 5) - 1;
                    int ofs = (ctrl & 31) << 8;

                    // --- Length Extension ---
                    if (len == 7 - 1)
                    {
                        do
                        {
                            ctrl = source[pointer++];
                            len += ctrl;
                        }
                        while (ctrl == 255);
                    }

                    ctrl = source[pointer++];
                    ofs |= ctrl;

                    // --- Large Offset Extension ---
                    if (ofs == 0x1FFF)
                    {
                        ofs = source[pointer++] << 8;
                        ofs |= source[pointer++];
                        ofs += 0x1FFF;
                    }

                    buffer.BackCopy(ofs + 1, len + 3);
                }
                else
                {
                    // --- Literal Run ---
                    // 000L LLLL

                    ctrl++;
                    buffer.Write(source.Slice(pointer, ctrl));
                    pointer += ctrl;
                }

                // If source end
                if (pointer >= source.Length)
                    return;

                // --- Next Flag ---
                ctrl = source[pointer++];
            }
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionSettings settings = default)
            => CompressHeaderless(source, destination, LookAhead, settings);

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionSettings settings = default)
        {
            if (source.Length < 0x10000 || settings.Quality <= 4 || settings.MaxWindowBits <= 13)
                CompressHeaderless_Level1(source, destination, lookAhead, settings);
            else
                CompressHeaderless_Level2(source, destination, lookAhead, true, _lz2[1].GetWindowsLevel((CompressionLevel)settings));
        }

        public static void CompressHeaderless_Level1(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {

            using PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz1, lookAhead, level);
            matches.Add(new LzMatch(source.Length, 0, 0)); // Dummy-Match

            InternCompressHeaderless(source, destination, matches, false);
        }

        public static void CompressHeaderless_Level2(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, bool lazyMatch = true, int maxWindowsSize = 0x11FFF)
        {
            using PoolList<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz2, maxWindowsSize, lookAhead, lazyMatch);
            matches.Add(new LzMatch(source.Length, 0, 0)); // Dummy-Match

            InternCompressHeaderless(source, destination, matches, true);
        }

        private static void InternCompressHeaderless(ReadOnlySpan<byte> source, Stream destination, PoolList<LzMatch> matches, bool isLevel2)
        {
            bool level2First = isLevel2;
            int sourcePointer = 0x0;
            byte ctrl;
            for (int i = 0; i < matches.Count; i++)
            {
                LzMatch match = matches[i];
                int plain = match.Offset - sourcePointer;

                // ---- Literal Runs ----
                while (plain > 0)
                {
                    int chunk = Math.Min(plain, 32);
                    ctrl = (byte)(chunk - 1);
                    if (level2First) // of level 2
                    {
                        ctrl |= (1 << 5);
                        level2First = false;
                    }

                    destination.WriteByte(ctrl);
                    destination.Write(source.Slice(sourcePointer, chunk));

                    sourcePointer += chunk;
                    plain -= chunk;
                }

                // --- Match Block ---
                if (match.Length >= 3)
                {
                    // Short Match:  LLLD DDDD DDDD DDDD
                    int length = match.Length - 3;
                    int distance = match.Distance - 1;

                    int shortDistance = isLevel2 ? Math.Min(match.Distance - 1, 0x1FFF) : match.Distance - 1;

                    ctrl = (byte)((Math.Min(length, 6) + 1) << 5);
                    destination.WriteByte((byte)(ctrl | shortDistance >> 8));

                    // Length Extension: 111D DDDD LLLL LLLL ... DDDD DDDD ...
                    if (length >= 6)
                    {
                        length -= 6;

                        while (length >= 255) // level 2
                        {
                            destination.WriteByte(255);
                            length -= 255;
                        }

                        destination.WriteByte((byte)length);
                    }

                    destination.WriteByte((byte)shortDistance);

                    // Level 2 Long Match:   1111 1111 ... 1111 1111 DDDD DDDD DDDD DDDD
                    if (isLevel2 && distance >= 0x1FFF)
                    {
                        distance -= 0x1FFF;
                        destination.WriteByte((byte)(distance >> 8));
                        destination.WriteByte((byte)distance);
                    }
                    sourcePointer += match.Length;
                }
            }
        }

        private static bool Validate(Stream source)
        {
            int ctrl = source.ReadByte();
            int level = (ctrl >> 5) + 1;
            if (level != 0 && level != 1)
                return false;

            int i = 3;
            int buffer = 0;

            while (ctrl != -1)
            {
                if (ctrl >= 0b10_0000) // --- Match Block ---
                {
                    int length = (ctrl >> 5) - 1;
                    int distance = (ctrl & 31) << 8;
                    if (length == 7 - 1)
                        length += source.ReadByte();

                    ctrl = source.ReadByte();
                    distance |= ctrl;

                    if (ctrl == -1 || length < 0) return false; //eos
                    if (distance + 1 > buffer) return false; // eow
                    if (i-- == 0) return true; //ok
                    buffer += length + 3;
                }
                else // --- Literal Run ---
                {
                    ctrl++;
                    buffer += ctrl;
                    if (source.Position + ctrl > source.Length)
                        return false;
                    source.Position += ctrl;
                }

                // If source end
                if (source.Position >= source.Length)
                    return i < 3;

                // --- Next Flag ---
                ctrl = source.ReadByte();
            }
            return false;
        }
    }
}
