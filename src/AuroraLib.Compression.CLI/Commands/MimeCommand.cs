using AuroraLib.Core.Format;
using System;
using System.IO;

namespace AuroraLib.Compression.CLI.Commands
{
    internal static class DetectedMimeCommand
    {
        public static IFormatInfo? Execute(string sourceFile)
        {
            using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            ReadOnlySpan<char> fileName = Path.GetFileName(sourceFile).AsSpan();

            if (FormatService.Formats.Identify(source, fileName, out IFormatInfo format) && format.Class != null)
            {
                return format;
            }
            return null;
        }
    }
}
