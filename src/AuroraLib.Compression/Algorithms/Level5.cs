using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Buffers;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Level5 compression algorithm, mainly used in Level5 3ds games.
    /// </summary>
    public class Level5 : ICompressionAlgorithm, ILzSettings
    {
        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <summary>
        /// Specifies the type of compression used.
        /// </summary>
        public CompressionType Type = CompressionType.OnlySave;

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
        {
            uint typeAndSize = stream.ReadUInt32();
            int decompressedSize = (int)(typeAndSize >> 3);
            return (stream.ReadByte() == 0x78 & typeAndSize != 0) || (Enum.IsDefined(typeof(CompressionType), (CompressionType)(typeAndSize & 0x7)) && decompressedSize != 0 && decompressedSize <= 0x1FFFFF && (typeAndSize & 0x7) == 0 ? decompressedSize + 4 == stream.Length : true);
        }

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            uint typeAndSize = source.ReadUInt32();
            if (source.Peek<byte>() == 0x78) // Zlib Type
            {
                new ZLib().Decompress(source, destination);
                return;
            }
            CompressionType type = (CompressionType)(typeAndSize & 0x7);
            int decompressedSize = (int)(typeAndSize >> 3);

            switch (type)
            {
                case CompressionType.OnlySave:
                    using (SpanBuffer<byte> buffer = new SpanBuffer<byte>(decompressedSize))
                    {
                        source.Read(buffer);
                        destination.Write(buffer);
                    }
                    break;
                case CompressionType.LZ10:
                    LZ10.DecompressHeaderless(source, destination, decompressedSize);
                    break;
                case CompressionType.RLE:
                    RLE30.DecompressHeaderless(source, destination, decompressedSize);
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
                default:
                    throw new NotSupportedException();
            }
        }

        public enum CompressionType
        {
            OnlySave = 0,
            LZ10 = 1,
            //Huffman4Bit = 2,
            //Huffman8Bit = 3,
            RLE = 4,
            ZLib = 8
        }
    }
}
