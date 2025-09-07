using AuroraLib.Compression.Exceptions;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.Format.Identifier;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace AuroraLib.Compression.Algorithms
{
    /// <summary>
    /// SynSophiaZip algorithm Zlib/Gzip-based compression with Mersenne Twister XOR encryption.
    /// </summary>
    public sealed class SSZL : ICompressionAlgorithm, IHasIdentifier, IProvidesDecompressedSize
    {
        private const uint _buildVersion = 0x2010_11_09;
        private const uint _seed = 0x40c360f3;

        /// <inheritdoc/>
        public IIdentifier Identifier => _identifier;

        private static readonly Identifier32 _identifier = new Identifier32(0x4C5A5353); //SSLZ

        /// <inheritdoc/>
        public IFormatInfo Info => _info;

        private static readonly IFormatInfo _info = new FormatInfo<SSZL>("SynSophiaZip", new MediaType(MIMEType.Application, "zlib+synsophia"), string.Empty, _identifier);

        /// <summary>
        /// Specifies the encoding options for SSLZ files.
        /// This determines whether the encoder will compress and/or encrypt the output file.
        /// </summary>
        public Option Options = Option.Compressed | Option.Encrypted;

        /// <summary>
        /// Flags representing the state of an SSLZ file.
        /// </summary>
        [Flags]
        public enum Option : uint
        {
            /// <summary>
            /// No special options set.
            /// </summary>
            None = 0,
            /// <summary>
            /// Indicates the data is compressed.
            /// </summary>
            Compressed = 0x1,
            /// <summary>
            /// Use Gzib instead of Zlib compression
            /// </summary>
            UseGzip = Compressed | 0x2,
            /// <summary>
            /// Unknown or reserved flag (internal use).
            /// </summary>
            Unknown = 0x10000,
            /// <summary>
            /// Indicates the data is encrypted using MT_XOR algorithm.
            /// </summary>
            Encrypted = 0x80000000,
        }

        /// <inheritdoc/>
        public bool IsMatch(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => IsMatchStatic(stream, fileNameAndExtension);

        /// <inheritdoc cref="IsMatch(Stream, ReadOnlySpan{char})"/>
        public static bool IsMatchStatic(Stream stream, ReadOnlySpan<char> fileNameAndExtension = default)
            => stream.Position + 0x10 < stream.Length && stream.Peek(s => s.Match(_identifier) && s.ReadUInt32() == _buildVersion);

        /// <inheritdoc/>
        public uint GetDecompressedSize(Stream source)
            => source.Peek(s =>
            {
                s.MatchThrow(_identifier);
                s.Position += 4;
                return s.ReadUInt32();
            });

        /// <inheritdoc/>
        public void Decompress(Stream source, Stream destination)
        {
            long start = destination.Position;
            // read Header 
            source.MatchThrow(_identifier); // must be "SSLZ"
            uint version = source.ReadUInt32(); // always 0x2010_11_09
            uint decompressedSize = source.ReadUInt32();
            Options = (Option)source.ReadUInt32();
            _ = source.ReadUInt32(); // reserved (always 0)

            // Validate version
            if (version != _buildVersion)
                Trace.WriteLine($"Unexpected build version: 0x{version:X8}");

            bool isEncrypted = (Options & Option.Encrypted) != 0;
            bool isCompressed = (Options & Option.Compressed) != 0;
            using Stream decrypted = isEncrypted ? Decrypt(source) : new SubStream(source);
            decrypted.CopyTo(destination);
            decrypted.Position = 0;
            destination.Position = 0;
            if (isCompressed)
            {
                bool isGzip = decrypted.Peek<ushort>() == 0x8B1F;
                var decoder = isGzip ? (ICompressionDecoder)new GZip() : (ICompressionDecoder)new ZLib();
                decoder.Decompress(decrypted, destination);
            }
            else
            {
                decrypted.CopyTo(destination);
            }

            long actualSize = destination.Length - start;
            if (decompressedSize != actualSize)
            {
                throw new DecompressedSizeException(decompressedSize, actualSize);
            }

            static Stream Decrypt(Stream source)
            {
                MemoryPoolStream buffer = new MemoryPoolStream();
                source.CopyTo(buffer);
                buffer.Position = 0;
                MTXorTransform(buffer.UnsafeAsSpan(), _seed);
                return buffer;
            }
        }

        /// <inheritdoc/>
        public void Compress(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level = CompressionLevel.Optimal)
        {
            destination.Write(_identifier);
            destination.Write(_buildVersion);
            destination.Write(source.Length);
            destination.Write((uint)Options & 0xFFFFFFFD); // We don't need to save whether we use Gzib or Zlip.
            destination.Write(0); // reserved

            bool encrypt = (Options & Option.Encrypted) != 0;
            bool compress = (Options & Option.Compressed) != 0;
            bool useGzip = (Options & Option.UseGzip) != 0;

            if (encrypt)
            {
                using MemoryPoolStream buffer = new MemoryPoolStream();
                CompressOrCopy(source, buffer, level, compress, useGzip);
                MTXorTransform(buffer.UnsafeAsSpan(), _seed);
                destination.Write(buffer.UnsafeGetBuffer(), 0, (int)buffer.Length);
            }
            else
            {
                CompressOrCopy(source, destination, level, compress, useGzip);
            }

            static void CompressOrCopy(ReadOnlySpan<byte> source, Stream destination, CompressionLevel level, bool Compress, bool useGzip)
            {
                if (Compress)
                {
                    var encoder = useGzip ? (ICompressionEncoder)new GZip() : new ZLib();
                    encoder.Compress(source, destination);
                }
                else
                {
                    destination.Write(source);
                }
            }
        }

        private static void MTXorTransform(Span<byte> target, uint seed)
        {
            using MersenneTwister mt = new MersenneTwister(seed);
            int fullDWords = target.Length >> 2;
            int remainingBytes = target.Length & 3;

            // XOR full 4-byte words
            if (fullDWords > 0)
            {
                var dwordsSpan = MemoryMarshal.Cast<byte, uint>(target.Slice(0, fullDWords * 4));
                for (int i = 0; i < fullDWords; i++)
                {
                    dwordsSpan[i] ^= mt.Next();
                }
            }

            // remaining bytes
            if (remainingBytes > 0)
            {
                uint next = mt.Next();
                int baseIndex = fullDWords * 4;
                for (int i = 0; i < remainingBytes; i++)
                {
                    target[baseIndex + i] ^= (byte)((next >> (i * 8)) & 0xFF);
                }
            }
        }

        /// <summary>
        /// Custom Mersenne Twister variant used for transformation.
        /// </summary>
        private class MersenneTwister : IDisposable
        {
            private const int N = 624;              // Size of the state array
            private const int MASK = 0x7FFFFFFF;    // 31-bit mask (signed int behavior)
            private const uint MULT = 0x13F8769B;   // Multiplier used in initialization
            private const uint T = 0x1908B0DF;      // Twist constant
            private const uint B = 0xFF3A58AD;      // Tempering mask 1
            private const uint C = 0xFFFFDF8C;      // Tempering mask 2

            private int _index;                     // Current index into the buffer
            private readonly uint[] _buffer;        // Internal state array of length N

            public MersenneTwister(uint seed)
            {
                _buffer = ArrayPool<uint>.Shared.Rent(N);
                Init(seed);
            }

            /// <summary>
            /// Initialize state with given seed.
            /// </summary>
            private void Init(uint seed)
            {
                _buffer[0] = seed & MASK;
                _index = N;

                for (int i = 0; i < N - 1; i++)
                {
                    uint cur = _buffer[i];
                    uint tmp = cur ^ (cur >> 30);
                    uint mul = MULT * tmp;
                    uint val = ((uint)(i + 1) - mul) & MASK;
                    _buffer[i + 1] = val;
                }
            }

            /// <summary>
            /// Generate the next pseudorandom 32-bit value.
            /// </summary>
            public uint Next()
            {
                if (_index == N)
                    _index = 0;

                _buffer[_index] = Twist(_buffer[_index], _buffer[(_index + 1) % N], _buffer[(_index + 397) % N]);
                /*
                if (_index < 227)       // Twist for [0] ... [226].
                {
                    _buffer[_index] = Twist(_buffer[_index], _buffer[_index + 1], _buffer[_index + 397]);
                }
                else if (_index == 623) // Twist for last element.
                {
                    _buffer[_index] = Twist(_buffer[_index], _buffer[0], _buffer[396]);
                }
                else                    // Twist for [227] ... [622].
                {
                    _buffer[_index] = Twist(_buffer[_index], _buffer[_index + 1], _buffer[_index - 227]);
                }
                */

                // Apply tempering and return
                return Temper(_buffer[_index++]);
            }

            private static uint Twist(uint a, uint b, uint c)
            {
                uint part = (a ^ ((a ^ b) & MASK)) >> 1;
                return (part ^ c ^ (T * (b & 1))) & MASK;
            }

            private static uint Temper(uint next)
            {
                uint t1 = (next >> 11) ^ next;
                uint t2 = (t1 & B) << 7;
                uint t3 = t2 ^ ((next >> 11) ^ next);
                uint t4 = (t3 & C) << 15;
                uint t5 = t4 ^ t2 ^ (next >> 11) ^ next;

                // Cast to int to replicate C's arithmetic right shift on signed 32-bit
                // Note: Using ((t5 ^ (t5 >> 18)) & uintMask) directly would produce incorrect results!
                int v = (int)t5;
                return (uint)((v ^ (v >> 18)) & MASK);
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                if (_index != -1)
                {
                    ArrayPool<uint>.Shared.Return(_buffer);
                    _index = -1;
                }
            }
        }
    }
}
