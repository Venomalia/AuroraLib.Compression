using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.IO;

namespace AuroraLib.Compression.CLI.Commands
{
    internal static class ScanDecompressCommand
    {
        public static bool Execute(string sourceFile, string destinationFolder, IFormatInfo format)
        {
            int found = 0;
            using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using MemoryPoolStream destination = new MemoryPoolStream();

            var decoder = format.CreateInstance();
            if (decoder is ICompressionDecoder compressionDecoder)
            {
                for (long i = 0; i < source.Length;)
                {
                    source.Seek(i, SeekOrigin.Begin);

                    try
                    {
                        if (compressionDecoder.IsMatch(source, default))
                        {
                            source.Seek(i, SeekOrigin.Begin);
                            i = UnpackInternally(destinationFolder, format, source, destination, compressionDecoder, ref found);
                            continue;
                        }
                    }
                    catch (Exception)
                    { }
                    i++;
                }
                return true;
            }
            return false;
        }
        public static void Execute(string sourceFile, string destinationFolder)
        {
            int found = 0;
            using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            ReadOnlySpan<char> fileName = Path.GetFileName(sourceFile).AsSpan();

            using MemoryPoolStream destination = new MemoryPoolStream();

            for (long i = 0; i < source.Length;)
            {
                source.Seek(i, SeekOrigin.Begin);

                try
                {
                    if (FormatService.Formats.Identify(source, fileName, out IFormatInfo format) && format!.Class != null)
                    {
                        var decoder = format.CreateInstance();
                        if (decoder is ICompressionDecoder compressionDecoder)
                        {
                            source.Seek(i, SeekOrigin.Begin);
                            i = UnpackInternally(destinationFolder, format, source, destination, compressionDecoder, ref found);
                            continue;
                        }
                    }
                }
                catch (Exception)
                { }
                i++;
            }
        }

        private static long UnpackInternally(string destinationFolder, IFormatInfo format, FileStream source, MemoryPoolStream destination, ICompressionDecoder compressionDecoder, ref int i)
        {
            long start = source.Position;
            destination.SetLength(0);
            compressionDecoder.Decompress(source, destination);

            if (destination.Length > 0x10)
            {
                // Success!
                var outputFile = Path.Combine(destinationFolder, $"{i++}#{compressionDecoder.GetType().Name}_0x{start:X}-0x{source.Position:X}.bin");

                HelpPrinter.PrintFoundStream(start, source.Position);
                HelpPrinter.PrintFormatInfo(format);
                HelpPrinter.PrintSaveFile(outputFile);
                Console.WriteLine();

                Directory.CreateDirectory(destinationFolder);
                using FileStream file = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
                destination.WriteTo(file);

                return source.Position;
            }
            return start + 1;
        }
    }
}
