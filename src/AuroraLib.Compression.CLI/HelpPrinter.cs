using AuroraLib.Compression.Interfaces;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Linq;
using static AuroraLib.Compression.CLI.ArgumentParser;

namespace AuroraLib.Compression.CLI
{
    internal static class HelpPrinter
    {
        public static void PrintSaveFile(string outputFile)
            => Console.WriteLine($"[SAVED] {outputFile}");

        public static void PrintFormatInfo(IFormatInfo format)
        {
            Console.WriteLine($"Format: {format.FullName}");
            Console.WriteLine($"MIME: {format.MIMEType}");

            if (format.FileExtensions.Any() && !string.IsNullOrEmpty(format.FileExtensions.First()))
                Console.WriteLine($"Extensions: {string.Join(", ", format.FileExtensions)}");

            if (format.Identifier != null)
                Console.WriteLine($"Identifier: {format.Identifier}");
        }

        public static void PrintSupportedFormats()
        {
            Console.WriteLine("List of supported Algorithms/formats:");
            Console.WriteLine(new string('-', 100));
            Console.WriteLine("{0,-30} {1,-10} {2,-35} {3}", "Name", "Class", "MIME Type", "Options");
            Console.WriteLine(new string('-', 100));
            foreach (var formatInfo in FormatService.Formats.Values.OrderBy(f => f.FullName))
            {
                var instance = formatInfo.CreateInstance();

                if (instance is ICompressionEncoder)
                {
                    List<string> supported = new List<string>();

                    if (instance is ILzSettings)
                        supported.Add("LookAhead");

                    if (instance is IEndianDependentFormat)
                        supported.Add("ByteOrder");

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
            ConsoleFlag(Flags.Overwrite, string.Empty, "Overwrite output file if already exists. [optional]");
            ConsoleFlag(Flags.Quiet, string.Empty, "Suppress all output except errors. [optional]");

            ConsoleFlag(Modes.Compress, null, "Compress a file using the specified algorithm.");
            ConsoleFlag(Flags.In, "file path", "Input file path.");
            ConsoleFlag(Flags.OUt, "file path", "Output file path. [optional]");
            ConsoleFlag(Flags.Algo, "name", "Algorithm/format name or MIME Type");
            ConsoleFlag(Flags.Level, "level", $"CompressionLevel <{string.Join(", ", Enum.GetNames(typeof(CompressionLevel)))}> [optional]");
            ConsoleFlag(Flags.LookAhead, "true|false", "Use LookAhead [optional, format-specific]");
            ConsoleFlag(Flags.Endian, $"{Endian.Little}|{Endian.Big}", "Byte order [optional, format-specific]");
            ConsoleFlag(Flags.Overwrite, string.Empty, "Overwrite output file if already exists. [optional]");
            ConsoleFlag(Flags.Quiet, string.Empty, "Suppress all output except errors. [optional]");

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
