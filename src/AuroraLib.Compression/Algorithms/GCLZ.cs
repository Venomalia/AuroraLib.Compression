using AuroraLib.Core;
using AuroraLib.Core.Interfaces;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// GCLZ extension header based on LZ10 algorithm used in Pandora's Tower.
    /// </summary>
    public sealed class GCLZ : LZ10, IHasIdentifier
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32("GCLZ".AsSpan());

        /// <inheritdoc/>
        public override bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public new static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && stream.Peek(s => s.Match(_identifier) && LZ10.IsMatchStatic(s));

        /// <inheritdoc/>
        public override void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier);
            base.Compress(source, destination, level);
        }

        /// <inheritdoc/>
        public override void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            base.Decompress(source, destination);
        }

    }
}
