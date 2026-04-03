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
        LZ4.HashAlgorithm = K4os.Hash.xxHash.XXH32.DigestOf;

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

                    CompressionSettings settings = default;
                    if (argsDict.TryGetValue(Flags.Level, out var lvlStr))
                    {
                        if (int.TryParse(lvlStr, out int quality))
                        {
                            settings = quality;
                        }
                        else if (Enum.TryParse(lvlStr, out CompressionLevel lvl))
                        {
                            settings = lvl;
                        }
                    }
                    bool useLegacyMode = argsDict.ContainsKey(Flags.LegacyMode);
                    int MaxWindowBits = argsDict.TryGetValue(Flags.MaxWindow, out var winStr) ? int.Parse(winStr!) : 0;
                    Endian? order = argsDict.TryGetValue(Flags.Endian, out var ordStr) && Enum.TryParse(ordStr, true, out Endian ord) ? ord : null;
                    bool useWram = argsDict.ContainsKey(Flags.WRam);

                    settings = new CompressionSettings(settings.Quality, MaxWindowBits, useLegacyMode ? CompresionStrategy.CompatibilityMode : CompresionStrategy.Default);

                    if (!CompressCommand.Execute(input, output, encoder, settings, order, useWram))
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
                            if (!ScanDecompressCommand.Execute(input, output, decoder))
                                return;
                        }
                        else
                        {
                            ScanDecompressCommand.Execute(input, output);
                        }
                    }
                    else // default
                    {
                        if (isFixAlgo)
                        {
                            var decoder = FormatService.GetFormatInfo(algo) ?? throw new ArgumentException($"Unknown decoder: '{algo}'.");
                            if (!DecompressCommand.Execute(input, output, decoder))
                                return;
                        }
                        else
                        {
                            if (!DecompressCommand.Execute(input, output))
                                return;
                        }
                        HelpPrinter.PrintSaveFile(output);
                    }
                    break;
                case Modes.Mime:
                    HelpPrinter.PrintOperation(mode.ToString(), input, null);
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

                    BruteForceCommand.Execute(input, output, offset, expectedSize);
                    break;
                default:
                    throw new ArgumentException($"Unknown command. Use -help for usage.");
            }
            Console.WriteLine();
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
