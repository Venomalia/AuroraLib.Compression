using AuroraLib.Compression.Interfaces;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// GZip open-source compression algorithm, which is based on the DEFLATE compression format.
    /// </summary>
    public sealed class GZip : ICompressionAlgorithm
    {
        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x8 < stream.Length && stream.ReadByte() == 31 && stream.ReadByte() == 139;

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            using GZipStream algo = new(source, CompressionMode.Decompress, true);
            algo.CopyTo(destination);
            source.Position = source.Length;
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            using GZipStream algo = new(destination, level, true);
            algo.Write(source);
        }
    }
}
