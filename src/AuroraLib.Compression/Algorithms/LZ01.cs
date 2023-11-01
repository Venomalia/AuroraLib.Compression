﻿using AuroraLib.Compression.Interfaces;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// LZ01 implementation based on LZSS algorithm used in Skies of Arcadia Legends.
    /// </summary>
    public sealed class LZ01 : ICompressionAlgorithm, ILzSettings, IHasIdentifier
    {
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new("LZ01");

        private static readonly LzProperties _lz = new(0x1000, 0xF + 3, 3, 0xFEE);

        public bool LookAhead { get; set; } = true;

        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.Match(_identifier);

        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            uint sourceLength = source.ReadUInt32();
            uint decompressedSize = source.ReadUInt32();
            _ = source.ReadUInt32();
            LZSS.DecompressHeaderless(source, destination, (int)decompressedSize, _lz);
        }

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            long destinationStartPosition = destination.Position;
            destination.Write(_identifier);
            destination.Write(0); // Compressed length (will be filled in later)
            destination.Write(source.Length);
            destination.Write(0);

            LZSS.CompressHeaderless(source, destination, _lz, LookAhead, level);

            // Go back to the beginning of the file and write out the compressed length
            int destinationLength = (int)(destination.Position - destinationStartPosition);
            destination.At(destinationStartPosition + 4, x => x.Write(destinationLength));
        }
    }
}
