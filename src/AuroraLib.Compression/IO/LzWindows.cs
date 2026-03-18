using AuroraLib.Core.Exceptions;
using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AuroraLib.Compression.IO
{
    /// <summary>
    /// Represents a circular window buffer used in LZ compression.
    /// </summary>
    public sealed class LzWindows : Stream
    {
        private readonly byte[] _Buffer;

        private readonly int _Length;

        private int _Position;

        private readonly Stream? _Destination;

        private bool _Disposed = false;

        /// <inheritdoc/>
        public override long Length => _Length;
        /// <inheritdoc/>
        public override bool CanRead => !_Disposed;
        /// <inheritdoc/>
        public override bool CanSeek => !_Disposed;
        /// <inheritdoc/>
        public override bool CanWrite => !_Disposed;

        /// <inheritdoc/>
        public override long Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [DebuggerStepThrough]
            get => _Position;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [DebuggerStepThrough]
            set => _Position = (int)((value + _Length) & (_Length - 1));
        }

        public LzWindows(Stream destination, byte windowsBits)
        {
            ThrowIf.Null(destination);

            _Destination = destination;
            _Length = 1 << windowsBits;
            _Buffer = ArrayPool<byte>.Shared.Rent(_Length);
            _Position = 0;
        }

        public LzWindows(byte[] buffer)
        {
            _Length = buffer.Length;
            _Buffer = buffer;
            _Position = 0;
        }

        /// <summary>
        /// Copies data from a specific position in the circular buffer to the current position.
        /// </summary>
        /// <param name="distance">The distance from the current position to the source data.</param>
        /// <param name="length">The number of bytes to copy.</param>
        [DebuggerStepThrough]
        public void BackCopy(int distance, int length)
        {
            int bufferLength = _Length;
            int mask = bufferLength - 1;

            while (length > 0)
            {
                int chunk = length;

                // Calculate source position (wraparound)
                int srcPos = (_Position - distance) & mask;

                // Small-distance Handling
                if (distance < length && distance != 0)
                    chunk = distance;

                // If the chunk would go past the end of the buffer, adjust it
                if (srcPos + chunk > bufferLength)
                    chunk = bufferLength - srcPos;

                // Write the chunk into the ring buffer, InternWrite handles wraparound & flush
                InternWrite(_Buffer.AsSpan(srcPos, chunk));

                length -= chunk;
            }
        }

        /// <summary>
        /// Copies data from an offset position within the circular buffer to the current position.
        /// </summary>
        /// <param name="Offset">The offset position from which data will be copied.</param>
        /// <param name="length">The number of bytes to copy.</param>
        [DebuggerStepThrough]
        public void OffsetCopy(int Offset, int length)
        {
            Offset &= _Length - 1;
            int distance = _Position >= Offset ? _Position - Offset : _Position - Offset + _Length;
            BackCopy(distance, length);
        }


        /// <summary>
        /// Copies data from a <paramref name="source"/> <see cref="Stream"/> to this <see cref="LzWindows"/>.
        /// </summary>
        /// <param name="source">The source stream containing data to copy.</param>
        /// <param name="length">The number of bytes to copy.</param>
        [DebuggerStepThrough]
        public void CopyFrom(Stream source, int length)
        {
            while (length != 0)
            {
                int l = Math.Min(length, _Length - _Position);
                source.ReadExactly(_Buffer, _Position, l);
                Position += l;
                length -= l;
                if (_Position == 0)
                    FlushToDestination(_Length);
            }
        }

        /// <inheritdoc/>
        [DebuggerStepThrough]
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        /// <inheritdoc/>
        [DebuggerStepThrough]
#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public override int Read(Span<byte> buffer)
#else
        public int Read(Span<byte> buffer)
#endif
        {
            int num;
            for (int i = 0; buffer.Length > i; i += num)
            {
                num = (int)Math.Min(_Length - _Position, buffer.Length);
                _Buffer.AsSpan((int)Position, num).CopyTo(buffer.Slice(i, num));
                Position += num;
            }

            return buffer.Length;
        }

        /// <inheritdoc/>
        [DebuggerStepThrough]
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        /// <inheritdoc/>
        [DebuggerStepThrough]

#if NET6_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        public override void Write(ReadOnlySpan<byte> buffer)
#else
        public void Write(ReadOnlySpan<byte> buffer)
#endif
        {
            int windows = _Length;
            if (buffer.Length < windows)
            {
                InternWrite(buffer);
                return;
            }

            int offset = 0;
            while (buffer.Length - offset >= windows)
            {
                InternWrite(buffer.Slice(offset, windows));
                offset += windows;
            }
            if (offset < buffer.Length)
            {
                InternWrite(buffer.Slice(offset));
            }
        }

        private void InternWrite(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty)
                return;

            ref byte dst = ref MemoryMarshal.GetReference(_Buffer.AsSpan());
            ref byte src = ref MemoryMarshal.GetReference(buffer);

            int len = buffer.Length;
            int pos = _Position;
            int windows = _Length;

            if (windows > pos + len)
            {
                // The entire buffer fits without wrapping around.
                Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dst, pos), ref src, (uint)len);
                _Position += len;
            }
            else // Partially write and wrap around.
            {
                int left = windows - pos;
                int remaining = len - left;

                // Fill the remaining window if necessary.
                if (left > 0) Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dst, pos), ref src, (uint)left);

                // Flush the entire window onto the underlying stream.
                _Destination!.Write(_Buffer, 0, (int)windows);

                // Fill the remaining window if necessary.
                if (remaining != 0) Unsafe.CopyBlockUnaligned(ref dst, ref Unsafe.Add(ref src, len - remaining), (uint)remaining);

                // Set position at the beginning of the window
                _Position = remaining;
            }
        }

        /// <inheritdoc/>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteByte(byte value)
        {
            _Buffer[Position++] = value;
            if (_Position == 0)
                FlushToDestination(_Length);
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            if (_Position != 0)
            {
                FlushToDestination(_Position);
                _Position = 0;
            }
        }

        private void FlushToDestination(int length) => _Destination?.Write(_Buffer, 0, length);

        /// <inheritdoc/>
        [DebuggerStepThrough]
        public override long Seek(long offset, SeekOrigin origin) => origin switch
        {
            SeekOrigin.Begin => Position = offset,
            SeekOrigin.Current => Position += offset,
            SeekOrigin.End => Position = _Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        /// <inheritdoc/>
        [DebuggerStepThrough]
        public override void SetLength(long value) => throw new NotSupportedException();

        public byte[] Getbuffer() => _Buffer;

        /// <inheritdoc/>
        [DebuggerStepThrough]
        protected override void Dispose(bool disposing)
        {
            if (_Buffer.Length != 0 && _Position != 0)
                FlushToDestination(_Position);

            if (!_Disposed && _Destination != null)
                ArrayPool<byte>.Shared.Return(_Buffer);

            _Disposed = true;
        }
    }
}
