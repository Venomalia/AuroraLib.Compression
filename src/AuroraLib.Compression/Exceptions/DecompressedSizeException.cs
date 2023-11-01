namespace AuroraLib.Compression.Exceptions
{
    public class DecompressedSizeException : Exception
    {
        public DecompressedSizeException() : base() { }

        public DecompressedSizeException(long expected, long actual) : base(CreateMessage(expected, actual))
        { }

        public DecompressedSizeException(long expected, long actual, Exception? innerException) : base(CreateMessage(expected, actual), innerException)
        { }

        private static string CreateMessage(long expected, long actual)
            => $"{actual}b expected {expected}b.";
    }
}
