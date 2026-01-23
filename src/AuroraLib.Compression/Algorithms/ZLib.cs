using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// ZLib open-source compression algorithm, which is based on the DEFLATE compression format.
    /// </summary>
    public sealed class ZLib : ICompressionAlgorithm
    {
        private static readonly string[] _extensions = new string[] { ".zz", ".zlib", string.Empty };
        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<ZLib>("zlib", new MediaType(MIMEType.Application, "zlib"), _extensions);
        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x4 < stream.Length && CheckZlibHeaderAndFirstBlock(stream.Peek<uint>());

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            using ZLibStream algo = new ZLibStream(source, CompressionMode.Decompress, true);
            algo.CopyTo(destination);
        }

        public void Decompress(Stream source, Stream destination, int sourceSize)
            => Decompress(new SubStream(source, sourceSize), destination);

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            using ZLibStream algo = new ZLibStream(destination, level, true);
            algo.Write(source);
        }

        internal static bool CheckZlibHeaderAndFirstBlock(uint header4Bytes)
        {
            byte cmf = (byte)((header4Bytes >> 0) & 0xFF);  // CMF
            byte flg = (byte)((header4Bytes >> 8) & 0xFF);  // FLG
            byte d1 = (byte)((header4Bytes >> 16) & 0xFF); // first byte Compressed Data
            byte d2 = (byte)((header4Bytes >> 24) & 0xFF); // second byte Compressed Data

            if ((cmf & 0x0F) != 8) return false; // CM != deflate
            if (((cmf >> 4) & 0x0F) > 7) return false; // CINFO > 7 (max window 32KB)
            if ((cmf * 256 + flg) % 31 != 0) return false; // checksum

            bool bfinal = (d1 & 1) != 0;
            int btype = (d1 >> 1) & 0x03;

            if (btype == 3 || bfinal) return false; // invalid or empty

            if (btype == 0)
            {
                ushort len = (ushort)(d1 | (d2 << 8));
                ushort nlen = (ushort)~len;
                if ((len ^ nlen) != 0xFFFF) return false;
                if (len == 0) return false;
            }
            return true;
        }
    }
}
