using AuroraLib.Compression.Interfaces;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// ZLib open-source compression algorithm, which is based on the DEFLATE compression format.
    /// </summary>
    public sealed class ZLib : ICompressionAlgorithm
    {
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && stream.Read<Header>().Validate();

        public void Decompress(Stream source, Stream destination)
        {
            using ZLibStream algo = new(source, CompressionMode.Decompress, true);
            algo.CopyTo(destination);
            source.Position = source.Length;
        }

        public void Decompress(Stream source, Stream destination, int sourceSize)
            => Decompress(new SubStream(source, sourceSize), destination);

        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            using ZLibStream algo = new(destination, level, true);
            algo.Write(source);
        }

        public readonly struct Header
        {
            private readonly byte cmf;
            private readonly byte flg;

            public Header(byte cmf, byte flg)
            {
                this.cmf = cmf;
                this.flg = flg;
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
