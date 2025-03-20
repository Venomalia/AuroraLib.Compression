using AuroraLib.Core.IO;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

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
                    _Position = value % Length;
                }
                else
                {
                    _Position = value;
                }
            }
        }

        public LzWindows(Stream destination, int capacity) : this(destination, ArrayPool<byte>.Shared, capacity)
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
            // Optimization: Ensure distance and position are valid for the operation.
            if (distance >= length && distance <= Position)
                Write(_Buffer.AsSpan((int)(Position - distance), length));
            else
                OffsetCopy((int)(Length + Position - distance), length);
        }

        /// <summary>
        /// Copies data from an offset position within the circular buffer to the current position.
        /// </summary>
        /// <param name="Offset">The offset position from which data will be copied.</param>
        /// <param name="length">The number of bytes to copy.</param>
        [DebuggerStepThrough]
        public void OffsetCopy(int Offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                WriteByte(_Buffer[(Offset + i) % Length]);
            }
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
        public unsafe override void Write(ReadOnlySpan<byte> buffer)
        {
            int position = (int)_Position;
            if (Length > position + buffer.Length)
            {
                // The entire buffer fits without wrapping around.
                buffer.CopyTo(_Buffer.AsSpan(position));
                _Position += buffer.Length;
            }
            else
            {
                // Partially write and wrap around.
                int left = (int)(Length - position);
                int copy = (int)Math.Min(Length, buffer.Length);

                destination.Write(_Buffer, 0, position);
                if (left != 0)
                {
                    destination.Write(buffer.Slice(0, left));
                    buffer.Slice(buffer.Length - copy, left).CopyTo(_Buffer.AsSpan(position, left));
                }

                if (copy > left)
                {
                    int remaining = copy - left;
                    buffer.Slice(buffer.Length - remaining, remaining).CopyTo(_Buffer.AsSpan(0, remaining));
                }

                Position += copy;
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
