using AuroraLib.Compression.Interfaces;
using AuroraLib.Compression.IO;
using AuroraLib.Core;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Nintendo Yaz0 compression algorithm successor to the <see cref="Yay0"/> algorithm, used in numerous Nintendo titles from the N64 era to Switch.
    /// </summary>
    public class Yaz0 : ICompressionAlgorithm, ILzSettings, IHasIdentifier, IEndianDependentFormat, IProvidesDecompressedSize
    {

        /// <inheritdoc/>
        public virtual IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32("Yaz0".AsSpan());

        /// <inheritdoc/>
        public virtual IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<Yaz0>("Nintendo Yaz0", new MediaType(MIMEType.Application, "x-nintendo-yaz0"), string.Empty, _identifier);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <inheritdoc/>
        public Endian FormatByteOrder { get; set; } = Endian.Big;

        /// <summary>
        /// Gets or sets the memory alignment for the initialized buffer.<br/>
        /// Must be <c>0</c> or a power of <c>two</c>.
        /// <para/>
        /// This setting is only supported in select titles starting with the Wii U generation.
        /// It has no effect on compression.
        /// </summary>
        public uint MemoryAlignment { get; set; } = 0;

        /// <inheritdoc/>
        public virtual bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.Match(_identifier));

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(Identifier);
                return s.ReadUInt32(FormatByteOrder);
            });

        /// <inheritdoc/>
        public virtual void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(Identifier);
            uint decompressedSize = source.ReadUInt32(FormatByteOrder);
            MemoryAlignment = source.ReadUInt32(FormatByteOrder);
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
                decompressedSize = BitConverterX.ReverseEndianness(decompressedSize);
                MemoryAlignment = BitConverterX.ReverseEndianness(MemoryAlignment);
                DecompressHeaderless(source, destination, decompressedSize);
            }
        }

        /// <inheritdoc/>
        public virtual void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(Identifier.AsSpan());
            destination.Write(source.Length, FormatByteOrder);
            destination.Write(MemoryAlignment);
            destination.Write(0);
            CompressHeaderless(source, destination, LookAhead, level);
        }

        public static void DecompressHeaderless(Stream source, Stream destination, uint decomLength)
            => Yay0.DecompressHeaderless(new FlagReader(source, Endian.Big), source, source, destination, decomLength);

        public static void CompressHeaderless(ReadOnlySpan<byte> source, Stream destination, bool lookAhead = true, CompressionLevel level = CompressionLevel.Optimal)
        {
            using FlagWriter flag = new FlagWriter(destination, Endian.Big);
            Yay0.CompressHeaderless(source, flag.Buffer, flag.Buffer, flag, lookAhead, level);
        }
    }
}
