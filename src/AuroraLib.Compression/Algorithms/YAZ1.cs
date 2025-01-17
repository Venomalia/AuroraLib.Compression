using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Identical to <see cref="Yaz0"/> only with different identifier, in N64DD games the 0 was replaced by 1 if the files were on the disk instead of the cartridge.
    /// </summary>
    public sealed class Yaz1 : Yaz0 , ICompressionAlgorithm
    {
        /// <inheritdoc/>
        public override IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32("Yaz1".AsSpan());

        /// <inheritdoc/>
        public override IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<Yaz1>("Nintendo Yaz0 N64DD", new MediaType(MIMEType.Application, "x-nintendo-yaz0+dd"), string.Empty, _identifier);

        /// <inheritdoc/>
        public override bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public new static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.Match(_identifier));
    }
}
