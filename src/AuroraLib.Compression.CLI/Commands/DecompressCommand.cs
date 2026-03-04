using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using System;
using System.IO;

namespace AuroraLib.Compression.CLI.Commands
{
    internal static class DecompressCommand
    {
        public static bool Execute(string sourceFile, string destinationFile, IFormatInfo format)
        {
            using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);

            var decoder = format.CreateInstance();
            if (decoder is ICompressionDecoder compressionDecoder)
            {
                using FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None);
                compressionDecoder.Decompress(source, destination);
                HelpPrinter.PrintRemainingDataNote(sourceFile, source);
                return true;
            }
            return false;
        }


        public static bool Execute(string sourceFile, string destinationFile)
        {
            using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            ReadOnlySpan<char> fileName = Path.GetFileName(sourceFile).AsSpan();

            if (FormatService.Formats.Identify(source, fileName, out IFormatInfo format) && format.Class != null)
            {
                HelpPrinter.PrintFormatInfo(format);
                var decoder = format.CreateInstance();
                if (decoder is ICompressionDecoder compressionDecoder)
                {
                    using FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None);
                    source.Seek(0L, SeekOrigin.Begin);
                    compressionDecoder.Decompress(source, destination);
                    HelpPrinter.PrintRemainingDataNote(sourceFile, source);
                    return true;
                }
            }
            return false;
        }
    }
}
