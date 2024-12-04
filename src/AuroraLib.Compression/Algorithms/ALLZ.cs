using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Compression.MatchFinder;
using AuroraLib.Core;
using AuroraLib.Core.Buffers;
using AuroraLib.Core.Interfaces;
using AuroraLib.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Aqualead LZ compression algorithm, used in games that utilize the Aqualead framework.
    /// </summary>
    public sealed class ALLZ : ICompressionAlgorithm, IHasIdentifier
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32("ALLZ".AsSpan());

        internal static readonly LzProperties _lz = new LzProperties(0x20000, 0x40000, 3); //A larger window is possible but will take a lot longer.

        public byte LzCopyBits = 0;
        public byte LzDistanceBits = 14;
        public byte LzLengthBits = 1;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.Match(_identifier));

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            Span<byte> flags = stackalloc byte[4];
            source.Read(flags);
            uint decomLength = source.ReadUInt32();

            using (SpanBuffer<byte> buffer = new SpanBuffer<byte>(decomLength))
            {
                DecompressHeaderless(source, buffer, flags);
                destination.Write(buffer);
            }
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier);
            Span<byte> flags = stackalloc byte[4] { 0, LzCopyBits, LzDistanceBits, LzLengthBits };
            destination.Write(flags);
            destination.Write(source.Length);
            CompressHeaderless(source, destination, flags, true, level);
        }

        public static void DecompressHeaderless(Stream source, Span<byte> destination, ReadOnlySpan<byte> flags)
        {
            int length, distance, destinationPointer = 0;
            FlagReader flag = new FlagReader(source, Endian.Little);

            while (destinationPointer < destination.Length)
            {
                if (!flag.Readbit())
                {
                    length = ReadALFlag(flags[3]) + 1;
                    source.Read(destination.Slice(destinationPointer, length));
                    destinationPointer += length;
                }

                if (destinationPointer < destination.Length)
                {
                    distance = ReadALFlag(flags[2]) + 1;
                    length = ReadALFlag(flags[1]) + 3;

                    while (length-- > 0)
                    {
                        destination[destinationPointer] = destination[destinationPointer - distance];
                        destinationPointer++;
                    }
                }
            }
            return;

            int ReadALFlag(int startBits)
            {
                int bits = startBits;
                while (flag.Readbit())
                    bits++;
                int result = flag.ReadInt(bits);
                result += ((1 << (bits - startBits)) - 1) << startBits;
                return result;
            }
        }

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, ReadOnlySpan<byte> flags, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            int sourcePointer = 0x0, plainSize = 0;
            List<LzMatch> matches = LZMatchFinder.FindMatchesParallel(source, _lz, lookAhead, level);
            matches.Add(new LzMatch(source.Length, 0, 0)); // Dummy-Match

            using (FlagWriter flag = new FlagWriter(destination, Endian.Little))
            {
                foreach (LzMatch match in matches)
                {
                    plainSize = match.Offset - sourcePointer;
                    if (plainSize > 0)
                    {
                        flag.WriteBit(false);
                        WriteALFlag(flags[3], plainSize - 1);
                        flag.Buffer.Write(source.Slice(sourcePointer, plainSize));
                        sourcePointer += plainSize;
                        flag.FlushIfNecessary();
                    }
                    else
                    {
                        flag.WriteBit(true);
                    }

                    WriteALFlag(flags[2], match.Distance - 1);
                    WriteALFlag(flags[1], match.Length - 3);
                    sourcePointer += match.Length;
                }

                return;

                void WriteALFlag(int startBits, int value)
                {
                    int bitMask = 0, bits = startBits;

                    while (bitMask + ((1 << bits) - 1) < value)
                    {
                        bits++;
                        bitMask = ((1 << (bits - startBits)) - 1) << startBits;
                        flag.WriteBit(true);
                    }
                    flag.WriteBit(false);
                    value -= bitMask;
                    flag.WriteInt(value, bits);
                }
            }
        }
    }
}
