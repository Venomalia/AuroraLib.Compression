using AuroraLib.Core.Buffers;
using AuroraLib.Core.IO;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.IO
{
    /// <summary>
    /// Represents a circular window buffer used in LZ compression.
    /// </summary>
    public sealed class LzWindows : CircularBuffer
    {
        private readonly Stream destination;

        public LzWindows(Stream destination, int capacity) : base(capacity)
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
#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
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
#if !(NETSTANDARD || NET20_OR_GREATER)
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (Length > Position + buffer.Length)
            {
                // The entire buffer fits without wrapping around.
                buffer.CopyTo(_Buffer.AsSpan((int)Position));
                Position += buffer.Length;
            }
            else
            {
                // Partially write and wrap around.
                int left = (int)(Length - (Position));
                destination.Write(_Buffer, 0, (int)Position);
                if (left != 0)
                    destination.Write(buffer.Slice(0, left));
                base.Write(buffer);
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
        [DebuggerStepThrough]
        protected override void Dispose(bool disposing)
        {
            if (_Buffer.Length != 0 && Position != 0)
                FlushToDestination((int)Position);
            base.Dispose(disposing);
        }
    }
}
