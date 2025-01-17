using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
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
            => stream.Position + 0x8 < stream.Length && stream.Peek<Header>().Validate();

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
#if NET6_0_OR_GREATER
            using ZLibStream algo = new(source, CompressionMode.Decompress, true);
            algo.CopyTo(destination);
#else
            Header header = source.Read<Header>();
            using (DeflateStream dflStream = new DeflateStream(source, CompressionMode.Decompress, true))
                dflStream.CopyTo(destination);
#endif
            source.Position = source.Length;
        }


        public void Decompress(Stream source, Stream destination, int sourceSize)
            => Decompress(new SubStream(source, sourceSize), destination);

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
#if NET6_0_OR_GREATER
            using ZLibStream algo = new(destination, level, true);
            algo.Write(source);
        }
#else
            destination.Write(new Header(level));
            using (DeflateStream dflStream = new DeflateStream(destination, level, true))
                dflStream.Write(source);

            destination.Write(ComputeAdler32(source), Endian.Big);
        }

        private static uint ComputeAdler32(ReadOnlySpan<byte> source)
        {
            const uint MOD_ADLER = 65521;
            uint a = 1, b = 0;

            foreach (byte byteValue in source)
            {
                a = (a + byteValue) % MOD_ADLER;
                b = (b + a) % MOD_ADLER;
            }

            return (b << 16) | a;
        }
#endif

        public readonly struct Header
        {
            private readonly byte cmf;
            private readonly byte flg;

            public Header(CompressionLevel level)
            {
                cmf = 0x78;
                switch (level)
                {
                    case System.IO.Compression.CompressionLevel.NoCompression:
                    case System.IO.Compression.CompressionLevel.Fastest:
                        flg = 0x01;
                        break;
                    case System.IO.Compression.CompressionLevel.Optimal:
                        flg = 0xDA;
                        break;
                    default:
                        flg = 0x9C;
                        break;
                }
            }

            public enum CompressionMethod : byte
            {
                Deflate = 8
            }

            public CompressionMethod Method => (CompressionMethod)(cmf & 0x0F);

            public byte CompressionInfo => (byte)((cmf >> 4) & 0x0F);

            public ushort FletcherChecksum => (ushort)(((flg & 0xFF) << 8) | cmf);

            public bool HasDictionary => ((flg >> 5) & 0x01) != 0;

            public byte CompressionLevel => (byte)((flg >> 6) & 0x03);

            public bool Validate()
            {
                ushort checksum = FletcherChecksum;

                if (Method != CompressionMethod.Deflate || CompressionInfo > 7)
                    return false;

                return checksum % 31 != 0 || checksum % 255 != 0;
            }
        }
    }
}
