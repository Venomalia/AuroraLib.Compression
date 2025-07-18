using AuroraLib.Compression.Algorithms;
using AuroraLib.Compression.CLI;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Format;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

class Program
{
    static readonly FormatDictionary formats;

    static readonly Dictionary<string, Flags> FlagMap;

    static readonly Assembly AuroraLib_Compression = typeof(Yay0).Assembly;

    static readonly Assembly AuroraLib_Compression_Extended = typeof(LZ4).Assembly;

    enum Flags
    {
        Help,
        Compress,
        Decompress,
        In,
        Out,
        Algo,
        Level,
        LookAhead,
        Endian,
    }

    static Program()
    {
        formats = new FormatDictionary(new Assembly[] { AuroraLib_Compression, AuroraLib_Compression_Extended });
        formats.Add(new SpezialFormatInfo<HUF20>("Nintendo HUF20 4bits", new MediaType(MIMEType.Application, "x-nintendo-huf20-4bits"), ".huf4", () => new HUF20() { Type = HUF20.CompressionType.Huffman4bits }));
        formats.Add(new SpezialFormatInfo<HUF20>("Nintendo HUF20 8bits", new MediaType(MIMEType.Application, "x-nintendo-huf20-8bits"), ".huf8", () => new HUF20() { Type = HUF20.CompressionType.Huffman8bits }));
        formats.Add(new SpezialFormatInfo<LZ77>("Nintendo LZ77 Chunk", new MediaType(MIMEType.Application, "x-nintendo-lz10+lz77Chunk"), string.Empty, () => new LZ77() { Type = LZ77.CompressionType.ChunkLZ10 }));
        formats.Add(new SpezialFormatInfo<Level5>("Level5 Huffman 4Bit", new MediaType(MIMEType.Application, "x-level5-Huffman4Bit"), ".l5huf4", () => new Level5() { Type = Level5.CompressionType.Huffman4Bit }));
        formats.Add(new SpezialFormatInfo<Level5>("Level5 Huffman 8Bit", new MediaType(MIMEType.Application, "x-level5-Huffman8Bit"), ".l5huf8", () => new Level5() { Type = Level5.CompressionType.Huffman8Bit }));
        formats.Add(new SpezialFormatInfo<Level5>("Level5 LZ10", new MediaType(MIMEType.Application, "x-level5-LZ10"), ".l5LZ10", () => new Level5() { Type = Level5.CompressionType.LZ10 }));
        formats.Add(new SpezialFormatInfo<Level5>("Level5 ZLib", new MediaType(MIMEType.Application, "x-level5-ZLib"), ".l5ZLib", () => new Level5() { Type = Level5.CompressionType.ZLib }));
        formats.Add(new SpezialFormatInfo<Level5>("Level5 RLE", new MediaType(MIMEType.Application, "x-level5-RLE"), ".l5RLE", () => new Level5() { Type = Level5.CompressionType.RLE }));

        FlagMap = new Dictionary<string, Flags>(StringComparer.OrdinalIgnoreCase);
#if NETFRAMEWORK

        foreach (var flagob in Enum.GetValues(typeof(Flags)))
        {
            Flags flag = (Flags)flagob;
#else
            foreach (var flag in Enum.GetValues<Flags>())
        {
#endif
            string name = flag.ToString();

            FlagMap.Add("-" + name.ToLower(), flag);
            string shortName = "-" + new string(name.Where(char.IsUpper).ToArray()).ToLower();
            FlagMap.TryAdd(shortName, flag);
        }
    }

    static void Main(string[] args)
    {
        // Display program header with version, OS, and runtime info
        var ALC = AuroraLib_Compression.GetName();
        Console.WriteLine(new string('-', 100));
        Console.WriteLine($"{ALC.Name} v{ALC.Version}      OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}      Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine(new string('-', 100));

        // Show help if no arguments or help flags are provided
        if (args.Length == 0 || args.Contains("-help") || args.Contains("-h"))
        {
            ShowHelp();
            Console.ReadKey();
            return;
        }

        // Quick mode: decompress if only a single file path is provided
        if (args.Length == 1)
        {
            string input = args[0];
            string output = input + "~output";

            Console.WriteLine($"Decompressing '{input}' to '{output}'.");
            Decompress(input, output);
            Console.WriteLine("Decompression completed successfully.");
            return;
        }

        // Parse arguments into a dictionary
        var argsDict = ParseArgs(args);
        try
        {
            // Validate input file
            string input = GetRequiredArg(argsDict, Flags.In);
            if (!File.Exists(input))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error: Input file not found: \"{input}\"");
                Console.ResetColor();
                return;
            }

            // Validate output file
            if (!argsDict.TryGetValue(Flags.Out, out var output))
                output = input + "~output";

            if (File.Exists(output))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Output file already exists: \"{output}\"");
                Console.ResetColor();
                return;
            }

            // Handle compression
            if (argsDict.ContainsKey(Flags.Compress))
            {
                string algo = GetRequiredArg(argsDict, Flags.Algo);
                var level = argsDict.TryGetValue(Flags.Level, out var lvlStr) && Enum.TryParse(lvlStr, out CompressionLevel lvl) ? lvl : CompressionLevel.Optimal;
                bool? lookAhead = argsDict.TryGetValue(Flags.LookAhead, out var laStr) ? bool.Parse(laStr) : null;
                Endian? order = argsDict.TryGetValue(Flags.Endian, out var ordStr) && Enum.TryParse(ordStr, true, out Endian ord) ? ord : null;

                Console.WriteLine($"Compressing '{input}' to '{output}' using algorithm '{algo}'.");
                Console.WriteLine($"  Compression Level: {level}{(lookAhead == null ? null : $" LookAhead: {lookAhead}")}{(order == null ? null : $" Byte order: {order}")}.");
                Compress(algo, input, output, level, lookAhead, order);
                Console.WriteLine("Compression completed successfully.");
            }

            // Handle decompression
            else if (argsDict.ContainsKey(Flags.Decompress)) // Decompress
            {
                if (argsDict.TryGetValue(Flags.Algo, out var algo))
                {
                    Console.WriteLine($"Decompressing '{input}' to '{output}' using algorithm '{algo}'.");
                    Decompress(algo, input, output);
                }
                else
                {
                    Console.WriteLine($"Decompressing '{input}' to '{output}'.");
                    Decompress(input, output);
                }
                Console.WriteLine("Decompression completed successfully.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine("Unknown command. Use -help for usage.");
                Console.ResetColor();
                return;
            }
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(e);
            Console.ResetColor();
            return;
        }
    }

    static string GetRequiredArg(Dictionary<Flags, string?> args, Flags key)
    {
        if (!args.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required argument: {key}");
        return value;
    }

    static Dictionary<Flags, string?> ParseArgs(string[] args)
    {
        var result = new Dictionary<Flags, string?>();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("-")) continue;

            if (!FlagMap.TryGetValue(arg.ToLower(), out var flag))
                throw new ArgumentException($"Unknown flag: {arg}");

            if (result.ContainsKey(flag))
                throw new ArgumentException($"Duplicate flag: {arg}");

            string? value = null;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
            {
                value = args[++i];
            }

            result[flag] = value;
        }

        return result;
    }

    string GetAllUppercaseLetters(Enum flag)
    {
        string name = flag.ToString();
        return new string(name.Where(char.IsUpper).ToArray());
    }

    static void ShowHelp()
    {
        Console.WriteLine("Usage:");

        ConsoleFlag(Flags.Decompress, null, "Decompress a file.");
        ConsoleFlag(Flags.In, "file", "Input file path.");
        ConsoleFlag(Flags.Out, "file", "Output file path. [optional]");
        ConsoleFlag(Flags.Algo, "name", "Algorithm/format name or MIME Type [optional]");

        ConsoleFlag(Flags.Compress, null, "Compress a file using the specified algorithm.");
        ConsoleFlag(Flags.In, "file", "Input file path.");
        ConsoleFlag(Flags.Out, "file", "Output file path. [optional]");
        ConsoleFlag(Flags.Algo, "name", "Algorithm/format name or MIME Type");
        ConsoleFlag(Flags.Level, "level", $"CompressionLevel <{string.Join(", ", Enum.GetNames(typeof(CompressionLevel)))}> [optional]");
        ConsoleFlag(Flags.LookAhead, "true|false", "Use LookAhead [optional, format-specific]");
        ConsoleFlag(Flags.Endian, $"{Endian.Little}|{Endian.Big}", "Byte order [optional, format-specific]");

        ConsoleFlag(Flags.Help, null, "Show this help.");

        void ConsoleFlag(Flags flag, string? arg, string description)
        {
            string longName = "-" + flag.ToString().ToLower();
            string shortName = "-" + new string(flag.ToString().Where(char.IsUpper).ToArray()).ToLower();

            if (arg == null)
            {
                Console.WriteLine();
                Console.WriteLine($" {longName,-14} {'(' + shortName + ')',-22} {description}");
            }
            else
            {
                Console.WriteLine($"   {longName,-12} {'(' + shortName + ')',-5} {'<' + arg + '>',-15}  {description}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("List of supported Algorithms/formats:");
        Console.WriteLine(new string('-', 100));
        Console.WriteLine("{0,-30} {1,-10} {2,-35} {3}", "Name", "Class", "MIME Type", "Options");
        Console.WriteLine(new string('-', 100));
        foreach (var formatInfo in formats.Values.OrderBy(f => f.FullName))
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
        Console.WriteLine(new string('-', 100));
    }


    static void Decompress(string sourceFile, string destinationFile)
    {
        using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        ReadOnlySpan<char> fileName = Path.GetFileName(sourceFile).AsSpan();

        if (formats.Identify(source, fileName, out IFormatInfo format) && format.Class != null)
        {
            var decoder = format.CreateInstance();
            if (decoder is ICompressionDecoder compressionDecoder)
            {
                using FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None);
                Console.WriteLine($"Detected format: {format.FullName}");
                source.Seek(0L, SeekOrigin.Begin);
                compressionDecoder.Decompress(source, destination);
                return;
            }
        }
        throw new InvalidOperationException("No suitable decoder found for the file format.");
    }

    static void Decompress(string algo, string sourceFile, string destinationFile)
    {
        var format = GetFormatInfo(algo);
        using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);

        var decoder = format.CreateInstance();
        if (decoder is ICompressionDecoder compressionDecoder)
        {
            using FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None);
            compressionDecoder.Decompress(source, destination);
            return;
        }
        throw new InvalidOperationException($"Format '{format?.FullName}' does not support decompression.");
    }

    static void Compress(string algo, string sourceFile, string destinationFile, CompressionLevel level = CompressionLevel.Optimal, bool? useLookAhead = null, Endian? order = null)
    {

        var format = GetFormatInfo(algo);
        using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);

        var encoder = format.CreateInstance();
        if (encoder is ICompressionEncoder compressionEncoder)
        {
            using FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None);

            if (useLookAhead != null && encoder is ILzSettings encoderLzSettings)
            {
                encoderLzSettings.LookAhead = useLookAhead.Value;
            }

            if (order != null && encoder is IEndianDependentFormat encoderEndianSettings)
            {
                encoderEndianSettings.FormatByteOrder = order.Value;
            }

            compressionEncoder.Compress(source, destination, level);
            return;
        }
        throw new InvalidOperationException($"Format '{format?.FullName}' does not support compression.");
    }

    static IFormatInfo GetFormatInfo(string algo)
    {
        foreach (var format in formats.Values)
        {
            if (format.FullName == algo || format.MIMEType.ToString() == algo || format.Class?.Name == algo)
            {
                return format;
            }
        }
        throw new ArgumentException($"Unknown format or algorithm: '{algo}'", nameof(algo));
    }

}
