using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
using AuroraLib.Core.Interfaces;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Konami MDF0 based on ZLib compression algorithm used in Castlevania: The Adventure ReBirth.
    /// </summary>
    public sealed class MDF0 : ICompressionAlgorithm, IHasIdentifier
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32((byte)'m', (byte)'d', (byte)'f', 0x0);

        private static readonly ZLib zLib = new ZLib();

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x14 < stream.Length && stream.Match(_identifier) && stream.ReadUInt32() != 0 && ZLib.IsMatchStatic(stream, extension);

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            long start = destination.Position;
            source.MatchThrow(_identifier);
            uint decompressedSize = source.ReadUInt32();
            zLib.Decompress(source, destination);

            if (destination.Position - start != decompressedSize)
            {
                throw new DecompressedSizeException(decompressedSize, destination.Position - start);
            }
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier);
            destination.Write(source.Length); // decompressedSize
            zLib.Compress(source, destination, level);
        }
    }
}
