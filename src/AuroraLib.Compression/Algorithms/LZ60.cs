using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo LZ60 compression algorithm same as <see cref="LZ40"/>.
    /// </summary>
    public sealed class LZ60 : ICompressionAlgorithm, ILzSettings
    {
        private const byte Identifier = 0x60;

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && stream.ReadByte() == Identifier && (stream.ReadUInt24() != 0 || stream.ReadUInt32() != 0);

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.Position += 1;
            int uncompressedSize = source.ReadUInt24();
            if (uncompressedSize == 0) uncompressedSize = (int)source.ReadUInt32();
            LZ40.DecompressHeaderless(source, destination, uncompressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            if (source.Length <= 0xFFFFFF)
            {
                destination.Write(Identifier | (source.Length << 8));
            }
            else
            {
                destination.Write(Identifier | 0);
                destination.Write(source.Length);
            }

            LZ40.CompressHeaderless(source, destination, LookAhead, level);
        }
    }
}
