using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Core;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// LZHudson is an LZ based compression algorithm used in Mario Party 4.
    /// </summary>
    public sealed class LZHudson : ICompressionAlgorithm, ILzSettings
    {
        private static readonly LzProperties _lz = new LzProperties(0x1000, 0xFF + 18, 3);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && extension.Contains(".LZHudson".AsSpan(), StringComparison.InvariantCultureIgnoreCase) && stream.Peek<uint>(Endian.Big) != 0;

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            uint uncompressedSize = source.ReadUInt32(Endian.Big);
            DecompressHeaderless(source, destination, uncompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(source.Length, Endian.Big);
            CompressHeaderless(source, destination, LookAhead, level);
        }

        public static void DecompressHeaderless(Stream source, Stream destination, uint decomLength)
           => Yay0.DecompressHeaderless(new FlagReader(source, Endian.Big, 4, Endian.Big), source, source, destination, decomLength);

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            using (FlagWriter flag = new FlagWriter(destination, Endian.Big, 128, 4, Endian.Big))
            {
                Yay0.CompressHeaderless(source, flag.Buffer, flag.Buffer, flag, lookAhead, level);
            }
        }
    }
}
