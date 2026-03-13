using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Level5 compression algorithm, mainly used in Level5 3ds games.
    /// </summary>
    public class Level5 : ICompressionAlgorithm, ILzSettings, IProvidesDecompressedSize
    {
        private static readonly string[] _extensions = new string[] { ".Level5", ".zlb" };

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<Level5>("Level5 compression", new MediaType(MIMEType.Application, "x-level5-compressed"), _extensions);

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <summary>
        /// Specifies the type of compression used.
        /// </summary>
        public CompressionType Type = CompressionType.LZ10;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
        {
            if (stream.Length < 0x10)
                return false;

            uint typeAndSize = stream.ReadUInt32();
            int decompressedSize = (int)(typeAndSize >> 3);
            bool isZLib = typeAndSize != 0 && stream.PeekByte() == 0x78 && ZLib.IsMatchStatic(stream);
            stream.Position -= 4;
            string extension = PathX.GetExtension(fileNameAndExtension).ToString();
            return isZLib || Enum.IsDefined(typeof(CompressionType), (CompressionType)(typeAndSize & 0x7)) && decompressedSize != 0 && (CompressionType)(typeAndSize & 0x7) switch
            {
                CompressionType.LZ10 => stream.At(4, s=> LZ10.Validate(s,(uint)decompressedSize)),
                _ => _extensions.Any(ext => extension.Equals(ext, StringComparison.InvariantCultureIgnoreCase)),
            };
        }

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                uint typeAndSize = s.ReadUInt32();
                return s.Peek<byte>() == 0x78 ? typeAndSize : typeAndSize >> 3;
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            uint typeAndSize = source.ReadUInt32();
            if (source.Peek<byte>() == 0x78) // Zlib Type
            {
                long endPosition = destination.Position + typeAndSize;
                destination.SetLength(endPosition);

                new ZLib().Decompress(source, destination);

                // Verify decompressed size
                if (destination.Position > endPosition)
                    throw new DecompressedSizeException(typeAndSize, destination.Position - (endPosition - typeAndSize));
                return;
            }
            CompressionType type = (CompressionType)(typeAndSize & 0x7);
            uint decompressedSize = typeAndSize >> 3;

            switch (type)
            {
                case CompressionType.OnlySave:
                    byte[] buffer = ArrayPool<byte>.Shared.Rent((int)decompressedSize);
                    try
                    {
                        source.ReadExactly(buffer, 0, (int)decompressedSize);
                        destination.Write(buffer, 0, (int)decompressedSize);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                    break;
                case CompressionType.LZ10:
                    LZ10.DecompressHeaderless(source, destination, decompressedSize);
                    break;
                case CompressionType.RLE:
                    RLE30.DecompressHeaderless(source, destination, decompressedSize);
                    break;
                case CompressionType.Huffman4Bit:
                    HUF20.DecompressHeaderless(source, destination, (int)decompressedSize, 4, Endian.Big);
                    break;
                case CompressionType.Huffman8Bit:
                    HUF20.DecompressHeaderless(source, destination, (int)decompressedSize, 8, Endian.Big);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            if (Type == CompressionType.ZLib) // Zlib Type
            {
                destination.Write(source.Length);
                new ZLib().Compress(source, destination, level);
            }

            if (level == CompressionLevel.NoCompression)
                Type = CompressionType.OnlySave;

            if (source.Length > 0x1fffffff)
            {
                new ArgumentOutOfRangeException($"{nameof(Level5)} does not support files larger than 0x1fffffff.");
            }
            destination.Write((int)Type | (source.Length << 3));

            switch (Type)
            {
                case CompressionType.OnlySave:
                    destination.Write(source);
                    break;
                case CompressionType.LZ10:
                    LZ10.CompressHeaderless(source, destination, LookAhead, level);
                    break;
                case CompressionType.RLE:
                    RLE30.CompressHeaderless(source, destination);
                    break;
                case CompressionType.Huffman4Bit:
                    HUF20.CompressHeaderless(source, destination, 4, Endian.Big);
                    break;
                case CompressionType.Huffman8Bit:
                    HUF20.CompressHeaderless(source, destination, 8, Endian.Big);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        public enum CompressionType
        {
            OnlySave = 0,
            LZ10 = 1,
            Huffman4Bit = 2,
            Huffman8Bit = 3,
            RLE = 4,
            ZLib = 8
        }
    }
}
