using System;

namespace AuroraLib.Compression.Exceptions
{
    /// <summary>
    /// Exception thrown when the actual decompressed size differs from the expected size.
    /// </summary>
    public class DecompressedSizeException : Exception
    {
        public DecompressedSizeException() : base() { }

        public DecompressedSizeException(long expected, long actual) : base(CreateMessage(expected, actual))
        { }

        private static string CreateMessage(long expected, long actual)
            => $"Expected {expected} bytes, but write {actual}bytes.";

        public static void ThrowIfMismatch(long actual, long expected)
        {
            if (actual != expected)
            {
                throw new DecompressedSizeException(expected, actual);
            }
        }
    }
}
