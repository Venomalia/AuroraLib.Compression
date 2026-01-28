using AuroraLib.Compression.Interfaces;
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
    /// Rocket Company Ltd ECD algorithm base on LZSS, used in Kanken Training 2.
    /// </summary>
    // https://github.com/FanTranslatorsInternational/Kuriimu2/blob/8cc3c310a597fdf78209d693c7333009d772c15f/src/Kompression/Implementations/Decoders/LzEcdDecoder.cs
    public sealed class ECD : ICompressionAlgorithm, ILzSettings, IHasIdentifier, IProvidesDecompressedSize
    {
        private static readonly LzProperties LZPropertie = new LzProperties(0x400, 0x42, 3, 0x3BE);

        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier _identifier = new Identifier("ECD");

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<ECD>("ECD lzss", new MediaType(MIMEType.Application, "x-lzss+ecd"), string.Empty, _identifier);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <summary>
        /// 
        /// </summary>
        public byte PlainSize { get; set; } = 4;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.Match(_identifier)) && GetDecompressedSizeStatic(stream) != 0;

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source) => GetDecompressedSizeStatic(source);

        private static uint GetDecompressedSizeStatic(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                s.Position += 5;
                if (s.ReadUInt32(Endian.Big) + 0x10 > source.Length)
                    return 0u;
                return s.ReadUInt32(Endian.Big);
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            bool isCompressed = source.ReadByte() == 1;
            uint plainSize = source.ReadUInt32(Endian.Big);
            uint compressedSize = source.ReadUInt32(Endian.Big);
            uint decompressedSize = source.ReadUInt32(Endian.Big);

            // Mark the initial positions of the streams
            long compressedStartPosition = source.Position;

            if (isCompressed)
            {
                // Copy plain bytes
                for (var i = 0; i < plainSize; i++)
                    destination.WriteByte((byte)source.ReadByte());

                // Perform the decompression
                LZSS.DecompressHeaderless(source, destination, decompressedSize - plainSize, LZPropertie);
            }
            else
            {
                source.CopyTo(destination);
            }

            // Verify compressed size and handle mismatches
            Helper.TraceIfCompressedSizeMismatch(source.Position - compressedStartPosition, compressedSize);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Determine whether compression should be applied
            bool isCompressed = level != CompressionLevel.NoCompression && source.Length > 0x10;
            int plainSize = isCompressed ? PlainSize : 0;

            // Store the current position in the destination stream (assumes the stream supports seeking)
            long startpos = destination.Length;
            destination.Write(_identifier.AsSpan());
            destination.WriteByte(isCompressed ? (byte)1 : (byte)0);
            destination.Write(plainSize, Endian.Big);
            destination.Write(isCompressed ? 0 : source.Length, Endian.Big); // Placeholder
            destination.Write(source.Length, Endian.Big);

            if (isCompressed)
            {
                // Copy plain bytes
                destination.Write(source.Slice(0, plainSize));

                LZSS.CompressHeaderless(source.Slice(plainSize), destination, LZPropertie, LookAhead, level);

                uint compressedSize = (uint)(destination.Length - startpos - 0x10);
                destination.At(startpos + 0x8, s => s.Write(compressedSize, Endian.Big));

                // If compression was ineffective (data grew in size), fall back to uncompressed storage
                if (compressedSize >= source.Length)
                {
                    destination.Position = startpos;
                    destination.SetLength(startpos);
                    Compress(source, destination, CompressionLevel.NoCompression);
                }
            }
            else
            {
                destination.Write(source);
            }
        }
    }
}
