using AuroraLib.Core;
using AuroraLib.Core.Interfaces;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo LZ77 extension Header from LZ10 algorithm
    /// </summary>
    public sealed class LZ77 : LZ10, IHasIdentifier
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32("LZ77".AsSpan());

        /// <inheritdoc/>
        public override bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && stream.Match(_identifier) && LZ10.IsMatchStatic(stream, extension);

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
