using System.Diagnostics;

namespace AuroraLib.Compression
{
    internal static class Helper
    {
        internal static void TraceIfCompressedSizeMismatch(long actual, long expected)
        {
            if (actual != expected)
            {
                Trace.WriteLine($"Warning: Expected {expected} bytes of compressed data, but read {actual} bytes.");
            }
        }
    }
}
