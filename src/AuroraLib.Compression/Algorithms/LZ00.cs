using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
using AuroraLib.Core.Buffers;
using AuroraLib.Core.Interfaces;
using AuroraLib.Core.IO;
using System;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// Sega LZ00 based on LZSS algorithm with encryption
    /// </summary>
    public sealed class LZ00 : ICompressionAlgorithm, ILzSettings, IHasIdentifier, IObjectName
    {
        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32("LZ00".AsSpan());

        private static readonly LzProperties _lz = LZSS.Lzss0Properties;

        /// <inheritdoc/>
        public bool LookAhead { get; set; } = true;

        /// <summary>
        /// File name that was read during decoding and is written to the file header during encoding.
        /// </summary>
        public string Name { get; set; } = "Temp.dat";

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> extension = default)
            => IsMatchStatic(stream, extension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> extension = default)
            => stream.Position + 0x40 < stream.Length && stream.Match(_identifier);

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            source.MatchThrow(_identifier);
            uint sourceLength = source.ReadUInt32();
            source.Position += 8;

            Name = source.ReadString(32);

            uint decompressedSize = source.ReadUInt32();
            uint key = source.ReadUInt32();
            source.Position += 8;

            StreamTransformer transformSource = new StreamTransformer(source, key);
            LZSS.DecompressHeaderless(transformSource, destination, (int)decompressedSize, _lz);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Since the original files appear to use the time the file was compressed (as Unix time), we will do the same.
            uint key = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Compress(source, destination, key, level);
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, uint key, CompressionLevel level = CompressionLevel.Optimal)
        {
            long destinationStartPosition = destination.Position;
            destination.Write(_identifier);
            destination.Write(0); // Compressed length (will be filled in later)
            destination.Write(0);
            destination.Write(0);

            destination.WriteString(Name.AsSpan(), 32, 0);

            destination.Write(source.Length); // Decompressed length
            destination.Write(key); // Encryption key
            destination.Write(0);
            destination.Write(0);

            StreamTransformer transformDestination = new StreamTransformer(destination, key);
            LZSS.CompressHeaderless(source, transformDestination, _lz, LookAhead, level);

            // Go back to the beginning of the file and write out the compressed length
            int destinationLength = (int)(destination.Position - destinationStartPosition);
            destination.At(destinationStartPosition + 4, x => x.Write(destinationLength));
        }

        private sealed class StreamTransformer : Stream
        {
            public uint Key;
            public readonly Stream Base;

            public override bool CanRead => Base.CanRead;
            public override bool CanSeek => Base.CanSeek;
            public override bool CanWrite => Base.CanWrite;
            public override long Length => Base.Length;
            public override long Position { get => Base.Position; set => Base.Position = value; }
            public override void Flush() => Base.Flush();
            public override long Seek(long offset, SeekOrigin origin) => Base.Seek(offset, origin);
            public override void SetLength(long value) => Base.SetLength(value);

            public StreamTransformer(Stream baseStream, uint key = 0)
            {
                Base = baseStream;
                Key = key;
            }

            private void GenerateNextKey()
            {
                uint x = (((((((Key << 1) + Key) << 5) - Key) << 5) + Key) << 7) - Key;
                x = (x << 6) - x;
                x = (x << 4) - x;
                Key = (x << 2) - x + 12345;
            }

            private byte Transform(byte value)
            {
                GenerateNextKey();
                uint t = (Key >> 16) & 0x7FFF;
                return (byte)(value ^ (((t << 8) - t) >> 15));
            }

            private void Transform(Span<byte> buffer)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = Transform(buffer[i]);
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
#if !(NET20_OR_GREATER || NETSTANDARD2_0)
                => Read(buffer.AsSpan(offset, count));

            public override int Read(Span<byte> buffer)
            {
                int read = Base.Read(buffer);
                Transform(buffer);
                return read;
            }
#else
            {
                int read = Base.Read(buffer, offset, count);
                Transform(buffer.AsSpan(offset, count));
                return read;
            }
#endif
            public override void Write(byte[] buffer, int offset, int count)

#if !(NET20_OR_GREATER || NETSTANDARD2_0)
                => Write(buffer.AsSpan(offset, count));

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                using SpanBuffer<byte> bytes = new SpanBuffer<byte>(buffer);
                Transform(bytes);
                Base.Write(bytes);
            }
#else
            {
                using (SpanBuffer<byte> bytes = new SpanBuffer<byte>(buffer))
                {
                    Transform(bytes.Span.Slice(offset, count));
                    Base.Write(bytes.GetBuffer(), offset, count);
                }
            }
#endif
        }
    }
}
