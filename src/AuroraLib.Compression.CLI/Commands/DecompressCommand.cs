using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using System;
using System.IO;
using static AuroraLib.Compression.CLI.ArgumentParser;

namespace AuroraLib.Compression.CLI.Commands
{
    internal static class DecompressCommand
    {
        public static bool Execute(string sourceFile, string destinationFile, IFormatInfo format)
        {
            HelpPrinter.PrintOperation(nameof(Modes.Decompress), sourceFile, destinationFile);
            using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);

            var decoder = format.CreateInstance();
            if (decoder is ICompressionDecoder compressionDecoder)
            {
                using FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None);

                DateTime startTime = DateTime.Now;
                HelpPrinter.PrintOperationStart(startTime);
                compressionDecoder.Decompress(source, destination);
                HelpPrinter.PrintOperationEnd(startTime);
                HelpPrinter.PrintRemainingDataNote(sourceFile, source);
                return true;
            }
            Console.Error.WriteLine($"{format.FullName} failed to unpack this file!");
            return false;
        }


        public static bool Execute(string sourceFile, string destinationFile)
        {
            HelpPrinter.PrintOperation(nameof(Modes.Decompress), sourceFile, destinationFile);
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

                    DateTime startTime = DateTime.Now;
                    HelpPrinter.PrintOperationStart(startTime);
                    compressionDecoder.Decompress(source, destination);
                    HelpPrinter.PrintOperationEnd(startTime);
                    HelpPrinter.PrintRemainingDataNote(sourceFile, source);
                    return true;
                }
            }
            Console.Error.WriteLine("No suitable decoder found for the file format!");
            return false;
        }
    }
}
