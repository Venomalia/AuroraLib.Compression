using AuroraLib.Core.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AuroraLib.Compression.IO
{
    /// <summary>
    /// Represents a circular window buffer used in LZ compression.
    /// </summary>
    public sealed class LzWindows : CircularBuffer
    {
        public readonly Stream destination;

        public LzWindows(Stream destination, int capacity) : base(capacity)
            => this.destination = destination;

        /// <summary>
        /// Copies data from a specific position in the circular buffer to the current position.
        /// </summary>
        /// <param name="distance">The distance from the current position to the source data.</param>
        /// <param name="length">The number of bytes to copy.</param>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BackCopy(int distance, int length)
        {
            distance = (int)(Length - distance);
            for (int i = 0; i < length; i++)
            {
                WriteByte(_Buffer[(distance + Position) % Length]);
            }
        }

        /// <summary>
        /// Copies data from an offset position within the circular buffer to the current position.
        /// </summary>
        /// <param name="Offset">The offset position from which data will be copied.</param>
        /// <param name="length">The number of bytes to copy.</param>
        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OffsetCopy(int Offset, int length)
        {
            for (int i = 0; i < length; i++)
            {
                WriteByte(_Buffer[(Offset + i) % Length]);
            }
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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
                destination.Write(_Buffer.AsSpan(0, (int)Position));
                destination.Write(buffer[..left]);
                base.Write(buffer);
            }
        }

        [DebuggerStepThrough]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void WriteByte(byte value)
        {
            _Buffer[Position++] = value;
            if (Position == 0)
            {
                destination.Write(_Buffer.AsSpan(0, (int)Length));
            }
        }

        [DebuggerStepThrough]
        protected override void Dispose(bool disposing)
        {
            if (_Buffer.Length != 0 && Position != 0)
            {
                destination.Write(_Buffer.AsSpan(0, (int)Position));
            }
            base.Dispose(disposing);
        }
    }
}
