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
    public sealed class LzWindows : PoolStream
    {
        private readonly Stream destination;

        private long _Position;

        /// <inheritdoc/>
        public override long Position
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [DebuggerStepThrough]
            get
            {
                return _Position;
            }
            [DebuggerStepThrough]
            set
            {
                if (value < 0)
                {
                    _Position = Length + value;
                }
                else if (value >= Length)
                {
                    _Position = value & (Length - 1);
                }
                else
                {
                    _Position = value;
                }
            }
        }

        public LzWindows(Stream destination, byte windowsBits) : this(destination, ArrayPool<byte>.Shared, 1 << windowsBits)
        { }


        public LzWindows(Stream destination, ArrayPool<byte> aPool, int capacity) : base(aPool, aPool.Rent(capacity), capacity)
            => this.destination = destination;

        /// <summary>
        /// Copies data from a specific position in the circular buffer to the current position.
        /// </summary>
        /// <param name="distance">The distance from the current position to the source data.</param>
        /// <param name="length">The number of bytes to copy.</param>
        [DebuggerStepThrough]
        public void BackCopy(int distance, int length)
        {
            int bufferLength = (int)Length;
            int mask = bufferLength - 1;

            while (length > 0)
            {
                int chunk = length;

                // Calculate source position (wraparound)
                int srcPos = ((int)_Position - distance) & mask;

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
            Offset &= (int)Length - 1;
            int distance = (int)(_Position >= Offset ? _Position - Offset : _Position - Offset + Length);
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
                int l = Math.Min(length, (int)(Length - Position));
                source.Read(_Buffer, (int)Position, l);
                Position += l;
                length -= l;
                if (Position == 0)
                    FlushToDestination((int)Length);
            }
        }

        /// <inheritdoc/>
        [DebuggerStepThrough]
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        /// <inheritdoc/>
        [DebuggerStepThrough]
        public override int Read(Span<byte> buffer)
        {
            int num;
            for (int i = 0; buffer.Length > i; i += num)
            {
                num = (int)Math.Min(Length - Position, buffer.Length);
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
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            int windows = (int)Length;
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

            uint len = (uint)buffer.Length;
            uint pos = (uint)_Position;
            uint windows = (uint)Length;

            if (windows > pos + len)
            {
                // The entire buffer fits without wrapping around.
                Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dst, pos), ref src, len);
                _Position += len;
            }
            else // Partially write and wrap around.
            {
                uint left = windows - pos;
                uint remaining = len - left;

                // Fill the remaining window if necessary.
                if (left > 0) Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref dst, pos), ref src, left);

                // Flush the entire window onto the underlying stream.
                destination.Write(_Buffer, 0, (int)windows);

                // Fill the remaining window if necessary.
                if (remaining != 0) Unsafe.CopyBlockUnaligned(ref dst, ref Unsafe.Add(ref src, len - remaining), remaining);

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
            if (Position == 0)
                FlushToDestination((int)Length);
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            if (Position != 0)
            {
                FlushToDestination((int)Position);
                Position = 0;
            }
        }

        private void FlushToDestination(int length)
                => destination.Write(_Buffer, 0, length);

        /// <inheritdoc/>
        protected override Span<byte> InternalBufferAsSpan(int start, int length)
            => _Buffer.AsSpan(start, length);

        /// <inheritdoc/>
        protected override void ExpandBuffer(int minimumLength)
            => throw new NotSupportedException();

        /// <inheritdoc/>
        [DebuggerStepThrough]
        protected override void Dispose(bool disposing)
        {
            if (_Buffer.Length != 0 && Position != 0)
                FlushToDestination((int)Position);
            base.Dispose(disposing);
        }
    }
}
