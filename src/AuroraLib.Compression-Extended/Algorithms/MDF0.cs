using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Konami MDF0 based on ZLib compression algorithm used in Castlevania: The Adventure ReBirth.
    /// </summary>
    public sealed class MDF0 : ICompressionAlgorithm, IHasIdentifier, IProvidesDecompressedSize
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32((byte)'m', (byte)'d', (byte)'f', 0x0);

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<MDF0>("Konami MDF0", new MediaType(MIMEType.Application, "zlib+mdf0"), string.Empty, _identifier);

        private static readonly ZLib zLib = new ZLib();

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x14 < stream.Length && stream.Peek(s => s.Match(_identifier) && s.ReadUInt32() != 0 && ZLib.IsMatchStatic(s));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                return s.ReadUInt32();
            });

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
