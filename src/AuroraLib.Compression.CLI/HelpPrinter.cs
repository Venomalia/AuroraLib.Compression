using AuroraLib.Compression.Formats.Nintendo;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using static AuroraLib.Compression.CLI.ArgumentParser;

namespace AuroraLib.Compression.CLI
{
    internal static class HelpPrinter
    {
        public static void PrintOperationStart(DateTime startTime)
        {
            Console.WriteLine($"[Operation Start] {startTime:yyyy-MM-dd HH:mm:ss}");
        }
        public static void PrintOperationEnd(DateTime startTime)
        {
            DateTime endTime = DateTime.Now;
            Console.WriteLine($"[Operation Complete] {endTime:yyyy-MM-dd HH:mm:ss} (Duration: {(endTime - startTime).TotalSeconds:F2}s)");
            Console.WriteLine();
        }

        public static void PrintStats(long sourceLength, long destinationLength)
        {
            double inputMB = sourceLength / (1024.0 * 1024);
            double outputMB = destinationLength / (1024.0 * 1024);
            double savedMB = inputMB - outputMB;
            double ratio = 100.0 * outputMB / inputMB;

            Console.WriteLine("[Stats]");
            Console.WriteLine($" Input:  {inputMB:F2} MB");
            Console.WriteLine($" Output: {outputMB:F2} MB");
            Console.WriteLine($" Saved:  {savedMB:F2} MB");
            Console.WriteLine($" Ratio:  {ratio:F2}%");
            Console.WriteLine();
        }

        public static void PrintOperation(string operation, string input, string? output)
        {
            Console.WriteLine($"[Operation] {operation}");
            Console.WriteLine($" Input: \"{input}\"");
            if (output != null)
                Console.WriteLine($" Output: \"{output}\"");
            Console.WriteLine();
        }
        public static void PrintCompressionSettings(CompressionSettings settings, ICompressionEncoder encoder)
        {
            Console.WriteLine($"[Compression Settings]");
            Console.WriteLine($" Quality: {settings.Quality}");
            Console.WriteLine($" Strategy: {settings.Strategy}");
            if (settings.MaxWindowBits != 0)
                Console.WriteLine($" MaxWindow: 0x{1 << settings.MaxWindowBits:x}");

            if (encoder is IEndianDependentFormat encoderEndianSettings)
                Console.WriteLine($" ByteOrder: {encoderEndianSettings.FormatByteOrder}");
            if (encoder is IGbaRamMode gbaRamMode)
                Console.WriteLine($" GbaRamMode: {(gbaRamMode.GbaVramCompatibilityMode ? "Vram" : "Wram")}");
            Console.WriteLine();
        }
        public static void PrintSaveFile(string outputFile)
            => Console.WriteLine($"[Saved] to \"{outputFile}\"");

        public static void PrintFoundStream(long start, long end)
            => Console.WriteLine($"[Found] Offset 0x{start:X} - 0x{end:X} ({end - start} bytes)");

        public static void PrintRemainingDataNote(string sourceFile, FileStream source)
        {
            if (source.Position + 0x20 < sourceFile.Length)
            {
                long remaining = source.Length - source.Position;
                Console.WriteLine($"Note: Ended at 0x{source.Position:X}, {remaining} bytes remaining. There may be more compressed streams in the file.\nTry decompress again with \"-scan\" flag.");
            }
        }

        public static void PrintFormatInfo(IFormatInfo format)
        {
            Console.WriteLine($"[Format] {format.FullName}");
            Console.WriteLine($" MIME: {format.MIMEType}");

            if (format.FileExtensions.Any() && !string.IsNullOrEmpty(format.FileExtensions.First()))
                Console.WriteLine($" Extensions: {string.Join(", ", format.FileExtensions)}");

            if (format.Identifier != null)
                Console.WriteLine($" Identifier: {format.Identifier}");
            Console.WriteLine();
        }

        public static void PrintSupportedFormats()
        {
            Console.WriteLine($"List of supported Algorithms/formats: [{FormatService.Formats.Count}]");
            Console.WriteLine(new string('-', 100));
            Console.WriteLine("{0,-30} {1,-10} {2,-35} {3}", "Name", "Class", "MIME Type", "Options");
            Console.WriteLine(new string('-', 100));
            foreach (var formatInfo in FormatService.Formats.Values.OrderBy(f => f.FullName))
            {
                var instance = formatInfo.CreateInstance();

                if (instance is ICompressionEncoder)
                {
                    List<string> supported = new List<string>();

                    if (instance is IEndianDependentFormat)
                        supported.Add(nameof(Flags.Endian));

                    if (instance is IGbaRamMode)
                        supported.Add(nameof(Flags.WRam));

                    Console.WriteLine("{0,-30} {1,-10} {2,-35} {3}", formatInfo.FullName, formatInfo.Class?.Name ?? "—", formatInfo.MIMEType, string.Join(", ", supported));
                }
            }
        }

        public static void ShowHelp()
        {
            Console.WriteLine("Usage:");

            ConsoleFlag(Modes.Decompress, null, "Decompress a file.");
            ConsoleFlag(Flags.In, "file path", "Input file path.");
            ConsoleFlag(Flags.OUt, "file path", "Output file path. [optional]");
            ConsoleFlag(Flags.Algo, "name", "Algorithm/format name or MIME Type. [optional]");
            ConsoleFlag(Flags.SCan, string.Empty, "Scan input file for all valid compressed streams and extract them. [optional]");
            ConsoleFlag(Flags.Overwrite, string.Empty, "Overwrite output file if already exists. [optional]");
            ConsoleFlag(Flags.Quiet, string.Empty, "Suppress all output except errors. [optional]");

            ConsoleFlag(Modes.Compress, null, "Compress a file using the specified algorithm.");
            ConsoleFlag(Flags.In, "file path", "Input file path.");
            ConsoleFlag(Flags.OUt, "file path", "Output file path. [optional]");
            ConsoleFlag(Flags.Algo, "name", "Algorithm/format name or MIME Type");
            ConsoleFlag(Flags.Level, "0-15 or level", $"CompressionLevel <{string.Join(", ", Enum.GetNames(typeof(CompressionLevel)))}> [optional]");
            ConsoleFlag(Flags.MaxWindow, "1-28", "sets the largest window the encoder is allowed to use. [optional]");
            ConsoleFlag(Flags.LegacyMode, string.Empty, "Use compatibility features for older or simpler decoders [optional]");
            ConsoleFlag(Flags.Overwrite, string.Empty, "Overwrite output file if already exists. [optional]");
            ConsoleFlag(Flags.Quiet, string.Empty, "Suppress all output except errors. [optional]");
            ConsoleFlag(Flags.Endian, $"{Endian.Little}|{Endian.Big}", "Byte order [optional, format-specific]");
            ConsoleFlag(Flags.WRam, string.Empty, "Use WRam mode [optional, format-specific]");

            ConsoleFlag(Modes.Mime, null, "Try to recognize the file format used.");
            ConsoleFlag(Flags.In, "file path", "Input file path.");

            ConsoleFlag(Modes.BruteForce, null, "Test all algorithms until one matches the expected decompressed size.");
            ConsoleFlag(Flags.In, "file path", "Input file path.");
            ConsoleFlag(Flags.Size, "number", "Expected decompressed size in bytes.");
            ConsoleFlag(Flags.OFfset, "number", "Start offset of compressed data in byte.");
            ConsoleFlag(Flags.Quiet, string.Empty, "Suppress all output except errors. [optional]");

            ConsoleFlag(Modes.Help, null, "Show this help.");

            Console.WriteLine();
            HelpPrinter.PrintSupportedFormats();
            Console.WriteLine(new string('-', 100));
        }
        static void ConsoleFlag(Modes mode, string? arg, string description) => ConsoleFlag(mode.ToString(), arg, description);
        static void ConsoleFlag(Flags flag, string? arg, string description) => ConsoleFlag(flag.ToString(), arg, description);
        static void ConsoleFlag(string flag, string? arg, string description)
        {
            string longName = "-" + flag.ToLower();
            string shortName = "-" + new string(flag.ToString().Where(char.IsUpper).ToArray()).ToLower();

            if (arg == null)
            {
                Console.WriteLine();
                Console.WriteLine($" {longName,-14} {'(' + shortName + ')',-22} {description}");
            }
            else if (arg == string.Empty)
            {
                Console.WriteLine($"   {longName,-12} {'(' + shortName + ')',-21}  {description}");
            }
            else
            {
                Console.WriteLine($"   {longName,-12} {'(' + shortName + ')',-5} {'<' + arg + '>',-15}  {description}");
            }
        }
    }
}
