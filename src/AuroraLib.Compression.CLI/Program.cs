using AuroraLib.Compression;
using AuroraLib.Compression.CLI;
using AuroraLib.Compression.CLI.Commands;
using AuroraLib.Compression.Formats.Common;
using AuroraLib.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using static AuroraLib.Compression.CLI.ArgumentParser;

class Program
{

    static readonly Assembly AuroraLib_Compression = typeof(LZO).Assembly;

    static void Main(string[] args)
    {

        // Display program header with version, OS, and runtime info
        var ALC = AuroraLib_Compression.GetName();
        Console.WriteLine(new string('-', 100));
        Console.WriteLine($"{ALC.Name} v{ALC.Version}      OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}      Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine(new string('-', 100));

#if !DEBUG
        try
#endif
        {
            // Parse arguments into a dictionary
            var mode = ArgumentParser.Parse(args, out var argsDict);

            if (mode == Modes.Help)
            {
                HelpPrinter.ShowHelp();
                Console.ReadKey();
                return;
            }

            // Validate input file
            string input = GetRequiredArg(argsDict, Flags.In);
            if (!File.Exists(input))
                throw new ArgumentException($"Error: Input file not found: \"{input}\"");

            // Validate output file
            if (!argsDict.TryGetValue(Flags.OUt, out string output))
                output = input + "~output";


            // Should the output be overwritten?
            if (File.Exists(output) && new FileInfo(output).Length > 0 && !argsDict.ContainsKey(Flags.Overwrite))
                throw new Exception($"Error: Output file already exists: \"{output}\" use -o to overwrite.");

            // Is quiet?
            bool isQuiet = argsDict.ContainsKey(Flags.Quiet);
            if (isQuiet)
                Console.SetOut(TextWriter.Null);

            string algo;
            switch (mode)
            {
                case Modes.Compress:
                    algo = GetRequiredArg(argsDict, Flags.Algo);
                    var encoder = FormatService.GetFormatInfo(algo) ?? throw new ArgumentException($"Unknown encoder: '{algo}'.");
                    var level = argsDict.TryGetValue(Flags.Level, out var lvlStr) && Enum.TryParse(lvlStr, out CompressionLevel lvl) ? lvl : CompressionLevel.Optimal;
                    bool? lookAhead = argsDict.TryGetValue(Flags.LookAhead, out var laStr) ? bool.Parse(laStr) : null;
                    Endian? order = argsDict.TryGetValue(Flags.Endian, out var ordStr) && Enum.TryParse(ordStr, true, out Endian ord) ? ord : null;

                    CompressionSettings settings = level;
                    settings = new CompressionSettings(settings.Quality, settings.MaxWindowBits, lookAhead == false ? CompresionStrategy.CompatibilityMode : CompresionStrategy.Default);
                    Console.WriteLine($"Compressing '{input}' to '{output}' using algorithm '{encoder.FullName}'.");
                    Console.WriteLine($"  Compression Level: {level}{(lookAhead == null ? null : $" LookAhead: {lookAhead}")}{(order == null ? null : $" Byte order: {order}")}.");
                    Console.WriteLine();
                    if (!CompressCommand.Execute(input, output, encoder, level, order))
                    {
                        throw new ArgumentException($"Unknown encoder: '{algo}'.");
                    }
                    HelpPrinter.PrintSaveFile(output);
                    break;
                case Modes.Decompress:
                    bool isFixAlgo = argsDict.TryGetValue(Flags.Algo, out algo);
                    if (argsDict.ContainsKey(Flags.SCan))
                    {
                        if (isFixAlgo)
                        {
                            var decoder = FormatService.GetFormatInfo(algo) ?? throw new ArgumentException($"Unknown decoder: '{algo}'.");
                            Console.WriteLine($"Scanning '{input}' for compressed streams using '{decoder.FullName}'...\n");
                            if (!ScanDecompressCommand.Execute(input, output, decoder))
                            {
                                Console.Error.WriteLine($"{decoder.FullName} is not a valid decoder!");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Scanning '{input}' for compressed streams...\n");
                            ScanDecompressCommand.Execute(input, output);
                        }
                    }
                    else // default
                    {
                        if (isFixAlgo)
                        {
                            var decoder = FormatService.GetFormatInfo(algo) ?? throw new ArgumentException($"Unknown decoder: '{algo}'.");
                            Console.WriteLine($"Decompressing '{input}' to '{output}' using '{decoder.FullName}'...\n");
                            if (!DecompressCommand.Execute(input, output, decoder))
                            {
                                Console.Error.WriteLine($"{decoder.FullName} failed to unpack this file!");
                                return;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Decompressing '{input}' to '{output}'...\n");
                            if (!DecompressCommand.Execute(input, output))
                            {
                                Console.Error.WriteLine("No suitable decoder found for the file format!");
                                return;
                            }
                        }
                        HelpPrinter.PrintSaveFile(output);
                    }
                    Console.WriteLine("Decompression completed successfully.");
                    break;
                case Modes.Mime:
                    Console.WriteLine($"Trying to recognize format of '{input}'.\n");
                    var format = DetectedMimeCommand.Execute(input);
                    if (format != null)
                    {
                        HelpPrinter.PrintFormatInfo(format);
                    }
                    else
                    {
                        Console.WriteLine("Unknown format");
                        Environment.Exit(-1);
                    }
                    break;
                case Modes.BruteForce:
                    if (!argsDict.TryGetValue(Flags.OFfset, out string offsetStr) || !long.TryParse(offsetStr, out long offset))
                        offset = 0;
                    if (offset < 0)
                        throw new ArgumentException("Invalid offset value, cannot be negative.");

                    if (!long.TryParse(GetRequiredArg(argsDict, Flags.Size), out long expectedSize))
                        throw new ArgumentException("Invalid decompressed size value.");
                    if (expectedSize <= 0)
                        throw new ArgumentException("Invalid decompressed size value, cannot be negative or 0.");

                    Console.WriteLine($"Brute-force compression algorithm for '{input}' at offset {offset}...\n");

                    BruteForceCommand.Execute(input, output, offset, expectedSize);
                    break;
                default:
                    throw new ArgumentException($"Unknown command. Use -help for usage.");
            }
            Console.WriteLine("Completed successfully.");
        }
#if !DEBUG
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(e);
            Console.ResetColor();
            Environment.Exit(1);
            return;
        }
#endif
    }

    static string GetRequiredArg(Dictionary<Flags, string?> args, Flags key)
    {
        if (!args.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing required argument: {key}");
        return value!;
    }
}
