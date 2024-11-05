using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Core;
using AuroraLib.Core.Interfaces;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo Yaz0 compression algorithm successor to the <see cref="Yay0"/> algorithm, used in numerous Nintendo titles from the N64 era to Switch.
    /// </summary>
    public class Yaz0 : ICompressionAlgorithm, ILzSettings, IHasIdentifier, IEndianDependentFormat
    {

        /// <inheritdoc/>
        public virtual IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32("Yaz0".AsSpan());

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public Endian FormatByteOrder { get; set; } = Endian.Big;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x10 < stream.Length && stream.Match(_identifier);

        /// <inheritdoc/>
        public virtual void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            uint decompressedSize = source.ReadUInt32(FormatByteOrder);
            _ = source.ReadUInt32(FormatByteOrder);
            _ = source.ReadUInt32(FormatByteOrder);

            long sourceDataStartPosition = source.Position;
            long destinationStartPosition = destination.Position;
            try
            {
                DecompressHeaderless(source, destination, decompressedSize);
            }
            catch (Exception) // try other order
            {
                source.Seek(sourceDataStartPosition, SeekOrigin.Begin);
                destination.Seek(destinationStartPosition, SeekOrigin.Begin);
                decompressedSize = BitConverterX.Swap(decompressedSize);
                DecompressHeaderless(source, destination, decompressedSize);
            }
        }

        /// <inheritdoc/>
        public virtual void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier);
            destination.Write(source.Length, FormatByteOrder);
            destination.Write(0);
            destination.Write(0);
            CompressHeaderless(source, destination, LookAhead, level);
        }

        public static void DecompressHeaderless(Stream source, Stream destination, uint decomLength)
            => Yay0.DecompressHeaderless(new FlagReader(source, Endian.Big), source, source, destination, decomLength);

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            using (FlagWriter flag = new FlagWriter(destination, Endian.Big))
            {
                Yay0.CompressHeaderless(source, flag.Buffer, flag.Buffer, flag, lookAhead, level);
            }
        }
    }
}
