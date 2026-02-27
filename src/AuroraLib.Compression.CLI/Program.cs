using AuroraLib.Compression;
using AuroraLib.Compression.Algorithms;
using AuroraLib.Compression.CLI;
using AuroraLib.Compression.CLI.Algorithms;
using AuroraLib.Compression.Interfaces;
using AuroraLib.Core;
using AuroraLib.Core.Collections;
using AuroraLib.Core.Format;
using AuroraLib.Core.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using static AuroraLib.Compression.Algorithms.RefPack;

class Program
{
    static readonly FormatDictionary formats;

    static readonly Dictionary<string, Flags> FlagMap;

    static readonly Assembly AuroraLib_Compression = typeof(LZO).Assembly;

    static readonly Assembly AuroraLib_Compression_Extended = typeof(AKLZ).Assembly;

    static readonly Assembly Extern = typeof(Zstd).Assembly;

    enum Flags
    {
        Help,
        Compress,
        Decompress,
        Mime,
        In,
        OUt,
        Algo,
        Level,
        LookAhead,
        Endian,
        Overwrite,
        Quiet,
        BruteForce,
        Size,
        OFfset
    }

    static Program()
    {
        formats = new FormatDictionary(new Assembly[] { AuroraLib_Compression, AuroraLib_Compression_Extended, Extern });
        formats.Add(new SpezialFormatInfo<HUF20>("Nintendo HUF20 4bits", new MediaType(MIMEType.Application, "x-nintendo-huf20-4bits"), ".huf4", () => new HUF20() { Type = HUF20.CompressionType.Huffman4bits }));
        formats.Add(new SpezialFormatInfo<HUF20>("Nintendo HUF20 8bits", new MediaType(MIMEType.Application, "x-nintendo-huf20-8bits"), ".huf8", () => new HUF20() { Type = HUF20.CompressionType.Huffman8bits }));
        formats.Add(new SpezialFormatInfo<LZ77>("Nintendo LZ77 Chunk", new MediaType(MIMEType.Application, "x-nintendo-lz10+lz77chunk"), string.Empty, () => new LZ77() { Type = LZ77.CompressionType.ChunkLZ10 }));
        formats.Add(new SpezialFormatInfo<LZ77>("Nintendo LZ77 LZ11", new MediaType(MIMEType.Application, "x-nintendo-lz11+lz77"), string.Empty, () => new LZ77() { Type = LZ77.CompressionType.LZ11 }));
        formats.Add(new SpezialFormatInfo<LZ77>("Nintendo LZ77 RLE30", new MediaType(MIMEType.Application, "x-nintendo-rle30+lz77"), string.Empty, () => new LZ77() { Type = LZ77.CompressionType.RLE30 }));
        formats.Add(new SpezialFormatInfo<LZ77>("Nintendo LZ77 HUF20 4bits", new MediaType(MIMEType.Application, "x-nintendo-huf20-4bits+lz77"), string.Empty, () => new LZ77() { Type = LZ77.CompressionType.HUF20_4bits }));
        formats.Add(new SpezialFormatInfo<LZ77>("Nintendo LZ77 HUF20 8bits", new MediaType(MIMEType.Application, "x-nintendo-huf20-8bits+lz77"), string.Empty, () => new LZ77() { Type = LZ77.CompressionType.HUF20_8bits }));
        formats.Add(new SpezialFormatInfo<Level5>("Level5 Huffman 4Bit", new MediaType(MIMEType.Application, "x-level5-huffman4bit"), ".l5huf4", () => new Level5() { Type = Level5.CompressionType.Huffman4Bit }));
        formats.Add(new SpezialFormatInfo<Level5>("Level5 Huffman 8Bit", new MediaType(MIMEType.Application, "x-level5-huffman8bit"), ".l5huf8", () => new Level5() { Type = Level5.CompressionType.Huffman8Bit }));
        formats.Add(new SpezialFormatInfo<Level5>("Level5 LZ10", new MediaType(MIMEType.Application, "x-level5-lz10"), ".l5LZ10", () => new Level5() { Type = Level5.CompressionType.LZ10 }));
        formats.Add(new SpezialFormatInfo<Level5>("Level5 ZLib", new MediaType(MIMEType.Application, "x-level5-zlib"), ".l5ZLib", () => new Level5() { Type = Level5.CompressionType.ZLib }));
        formats.Add(new SpezialFormatInfo<Level5>("Level5 RLE", new MediaType(MIMEType.Application, "x-level5-rle"), ".l5RLE", () => new Level5() { Type = Level5.CompressionType.RLE }));
        formats.Add(new SpezialFormatInfo<RefPack>("RefPack v1", new MediaType(MIMEType.Application, "x-ea-refpack+v1"), string.Empty, () => new RefPack() { Options = OptionFlags.Default }));
        formats.Add(new SpezialFormatInfo<RefPack>("RefPack Maxis", new MediaType(MIMEType.Application, "x-ea-refpack+maxis"), string.Empty, () => new RefPack() { Options = OptionFlags.Default | OptionFlags.UsePreHeader }));
        formats.Add(new SpezialFormatInfo<RefPack>("RefPack v3", new MediaType(MIMEType.Application, "x-ea-refpack+v3"), string.Empty, () => new RefPack() { Options = OptionFlags.Default | OptionFlags.UseInt32 }));

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
            if (!FlagMap.TryAdd(shortName, flag))

                throw new Exception($"Key '{shortName}' is Already in use");
        }
    }

    static void Main(string[] args)
    {
        // Parse arguments into a dictionary
        var argsDict = ParseArgs(args);

        bool isQuiet = argsDict.ContainsKey(Flags.Quiet);
        if (isQuiet)
        {
            Console.SetOut(TextWriter.Null);
        }

        // Display program header with version, OS, and runtime info
        var ALC = AuroraLib_Compression.GetName();
        Console.WriteLine(new string('-', 100));
        Console.WriteLine($"{ALC.Name} v{ALC.Version}      OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}      Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        Console.WriteLine(new string('-', 100));

        // Show help if no arguments or help flags are provided
        if (args.Length == 0 || argsDict.ContainsKey(Flags.Help))
        {
            if (isQuiet) return;

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
            Console.ReadKey();
            return;
        }

#if !DEBUG
        try
#endif
        {
            // Validate input file
            string input = GetRequiredArg(argsDict, Flags.In);
            if (!File.Exists(input))
            {
                Error_WriteLineAndExit($"Error: Input file not found: \"{input}\"");
                return;
            }

            // Validate output file
            if (!argsDict.TryGetValue(Flags.OUt, out var output))
                output = input + "~output";

            if (File.Exists(output) && new FileInfo(output).Length > 0 && !argsDict.ContainsKey(Flags.Overwrite))
            {
                Error_WriteLineAndExit($"Error: Output file already exists: \"{output}\" use -o to overwrite.");
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
            else if (argsDict.ContainsKey(Flags.Mime))
            {
                Console.WriteLine($"Trying to recognize format of '{input}'.");
                DetectedMimeType(input);
                return;
            }
            else if (argsDict.ContainsKey(Flags.BruteForce))
            {
                if (!long.TryParse(GetRequiredArg(argsDict, Flags.OFfset), out long offset))
                    Error_WriteLineAndExit("Invalid offset value.");
                if (offset < 0)
                    Error_WriteLineAndExit("Invalid offset value, cannot be negative.");

                if (!long.TryParse(GetRequiredArg(argsDict, Flags.Size), out long expectedSize))
                    Error_WriteLineAndExit("Invalid decompressed size value.");
                if (expectedSize <= 0)
                    Error_WriteLineAndExit("Invalid decompressed size value, cannot be negative or 0.");

                Console.WriteLine($"Trying to brute-force compression algorithm for '{input}' at offset {offset}.");
                output = Path.Combine(Path.GetDirectoryName(input), "~output" + Path.GetFileNameWithoutExtension(input));
                BruteForce(input, output, offset, expectedSize);
                return;
            }
            else
            {
                Error_WriteLineAndExit("Unknown command. Use -help for usage.");
                return;
            }
        }
#if !DEBUG
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(e);
            Console.ResetColor();
            return;
        }
#endif
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

    static void ShowHelp()
    {
        Console.WriteLine("Usage:");

        ConsoleFlag(Flags.Decompress, null, "Decompress a file.");
        ConsoleFlag(Flags.In, "file path", "Input file path.");
        ConsoleFlag(Flags.OUt, "file path", "Output file path. [optional]");
        ConsoleFlag(Flags.Algo, "name", "Algorithm/format name or MIME Type. [optional]");
        ConsoleFlag(Flags.Overwrite, string.Empty, "Overwrite output file if already exists. [optional]");
        ConsoleFlag(Flags.Quiet, string.Empty, "Suppress all output except errors. [optional]");

        ConsoleFlag(Flags.Compress, null, "Compress a file using the specified algorithm.");
        ConsoleFlag(Flags.In, "file path", "Input file path.");
        ConsoleFlag(Flags.OUt, "file path", "Output file path. [optional]");
        ConsoleFlag(Flags.Algo, "name", "Algorithm/format name or MIME Type");
        ConsoleFlag(Flags.Level, "level", $"CompressionLevel <{string.Join(", ", Enum.GetNames(typeof(CompressionLevel)))}> [optional]");
        ConsoleFlag(Flags.LookAhead, "true|false", "Use LookAhead [optional, format-specific]");
        ConsoleFlag(Flags.Endian, $"{Endian.Little}|{Endian.Big}", "Byte order [optional, format-specific]");
        ConsoleFlag(Flags.Overwrite, string.Empty, "Overwrite output file if already exists. [optional]");
        ConsoleFlag(Flags.Quiet, string.Empty, "Suppress all output except errors. [optional]");

        ConsoleFlag(Flags.Mime, null, "Try to recognize the file format used.");
        ConsoleFlag(Flags.In, "file path", "Input file path.");

        ConsoleFlag(Flags.BruteForce, null, "Test all algorithms until one matches the expected decompressed size.");
        ConsoleFlag(Flags.In, "file path", "Input file path.");
        ConsoleFlag(Flags.Size, "number", "Expected decompressed size in bytes.");
        ConsoleFlag(Flags.OFfset, "number", "Start offset of compressed data in byte.");
        ConsoleFlag(Flags.Quiet, string.Empty, "Suppress all output except errors. [optional]");

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
            else if (arg == string.Empty)
            {
                Console.WriteLine($"   {longName,-12} {'(' + shortName + ')',-21}  {description}");
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
            PrintType(format);
            var decoder = format.CreateInstance();
            if (decoder is ICompressionDecoder compressionDecoder)
            {
                using FileStream destination = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None);
                source.Seek(0L, SeekOrigin.Begin);
                compressionDecoder.Decompress(source, destination);
                return;
            }
        }
        var error = "No suitable decoder found for the file format!";
        Console.Error.WriteLine(error);
        throw new InvalidOperationException(error);
    }

    static void DetectedMimeType(string sourceFile)
    {
        using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        ReadOnlySpan<char> fileName = Path.GetFileName(sourceFile).AsSpan();

        if (formats.Identify(source, fileName, out IFormatInfo format) && format.Class != null)
        {
            PrintType(format);
            return;
        }
        Console.WriteLine("Unknown format.");
    }

    static void BruteForce(string sourceFile, string destinationFolder, long offset, long expectedSize)
    {
        using FileStream source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        var RawData = new byte[source.Length - offset];
        var destination = new byte[expectedSize];
        source.Seek(offset, SeekOrigin.Begin);
        source.ReadExactly(RawData, 0, RawData.Length);

        foreach (var decoder in GetRawDecodersList())
        {
            Console.WriteLine();
            Console.WriteLine($"Testing {decoder.Name} decoder...");
            var ms = new MemoryStream(destination, 0, destination.Length, true, true);
            try
            {
                ms.Position = 0;
                decoder.Decompress(RawData, ms, (uint)expectedSize);
                if (ms.Position == expectedSize)
                {
                    Console.WriteLine($"{decoder.Name} decoder successfully unpacked the file.");
                    Directory.CreateDirectory(destinationFolder);
                    var outputFile = Path.Combine(destinationFolder, decoder.Name + ".bin");
                    File.WriteAllBytes(outputFile, destination);
                    Console.WriteLine($"Saved to '{outputFile}'.");
                    // continue in case of false positive.
                }
                else
                {
                    Console.WriteLine($"{decoder.Name} failed: Output is smaller than expected.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{decoder.Name} failed: {ExplainException(ex)}.");
            }
        }
        Console.WriteLine();
        Console.WriteLine("All tests completed.");

        static string ExplainException(Exception ex)
        {
            if (ex is EndOfStreamException) return "Compressed data ended unexpectedly";
            if (ex is InvalidDataException or ArgumentOutOfRangeException or IndexOutOfRangeException) return "Invalid compressed data";
            if (ex is NotSupportedException && ex.Message.Contains("not expandable")) return "Output exceeded expected size";

            return ex.Message;
        }
    }

    static List<(string Name, RawDecoder Decompress)> GetRawDecodersList() => new List<(string, RawDecoder)>
    {
        // Frequently used
        ("ZLib",(rawData, destination, expectedSize) => new ZLib().Decompress(new MemoryStream(rawData),destination)),
        ("GZip",(rawData, destination, expectedSize) => new GZip().Decompress(new MemoryStream(rawData),destination)),
        ("Zstandard",(rawData, destination, expectedSize) => new Zstd().Decompress(new MemoryStream(rawData),destination)),
        ("Deflate", Deflate),
        ("LZO",(rawData, destination, expectedSize) => LZO.DecompressHeaderless(new MemoryStream(rawData),destination)),
        ("LZ4",(rawData, destination, expectedSize) => LZ4.DecompressBlockHeaderless(new MemoryStream(rawData),destination, (uint)rawData.Length)),
        ("LZSS (12, 4, 2)",(rawData, destination, expectedSize) => LZSS.DecompressHeaderless(new MemoryStream(rawData),destination, expectedSize,LZSS.DefaultProperties)),
        ("LZSS (12, 4, 3)",(rawData, destination, expectedSize) => LZSS.DecompressHeaderless(new MemoryStream(rawData),destination, expectedSize,new LzProperties((byte)12, 4, 3))),
        ("LZSS (10, 6, 2)",(rawData, destination, expectedSize) => LZSS.DecompressHeaderless(new MemoryStream(rawData),destination, expectedSize,new LzProperties((byte)10, 6, 2))),
        ("LZSS (10, 6, 3)",(rawData, destination, expectedSize) => LZSS.DecompressHeaderless(new MemoryStream(rawData),destination, expectedSize,new LzProperties((byte)10, 6, 3))),
        ("LZSS0",(rawData, destination, expectedSize) => LZSS.DecompressHeaderless(new MemoryStream(rawData),destination, expectedSize,LZSS.Lzss0Properties)),
        ("PRS big",(rawData, destination, expectedSize) => PRS.DecompressHeaderless(new MemoryStream(rawData),destination, Endian.Big)),
        ("PRS Little",(rawData, destination, expectedSize) => PRS.DecompressHeaderless(new MemoryStream(rawData),destination, Endian.Little)),
        ("LZ10",(rawData, destination, expectedSize) => LZ10.DecompressHeaderless(new MemoryStream(rawData),destination, expectedSize)),
        ("LZ11",(rawData, destination, expectedSize) => LZ11.DecompressHeaderless(new MemoryStream(rawData),destination, expectedSize)),
        // sometimes used
        ("Yaz0",(rawData,destination, expectedSize) => Yaz0.DecompressHeaderless(new MemoryStream(rawData),destination, expectedSize)),
        ("RefPack",(rawData,destination, expectedSize) => RefPack.DecompressHeaderless(new MemoryStream(rawData),destination, (int)expectedSize)),
        ("LZ02",(rawData,destination, expectedSize) => LZ02.DecompressHeaderless(new MemoryStream(rawData),destination, expectedSize)),
        ("HUF20 8bit",(rawData,destination, expectedSize) => HUF20.DecompressHeaderless(new MemoryStream(rawData),destination, (int)expectedSize, 8)),
        ("HUF20 4bit Little",(rawData,destination, expectedSize) => HUF20.DecompressHeaderless(new MemoryStream(rawData),destination, (int)expectedSize, 4, Endian.Little)),
        ("HUF20 4bit Big",(rawData,destination, expectedSize) => HUF20.DecompressHeaderless(new MemoryStream(rawData),destination, (int)expectedSize, 4, Endian.Big)),
        ("aPLib",(rawData,destination, expectedSize) => aPLib.DecompressHeaderless(new MemoryStream(rawData),destination)),
        // rarely used
        ("RLE30",(rawData,destination, expectedSize) => RLE30.DecompressHeaderless(new MemoryStream(rawData),destination, expectedSize)),
        ("LZ40",(rawData,destination, expectedSize) => LZ40.DecompressHeaderless(new MemoryStream(rawData),destination, expectedSize)),
        ("BLZ",(rawData,destination, expectedSize) => BLZ.DecompressHeaderless(rawData,destination.GetBuffer())),
        ("CLZ0",(rawData,destination, expectedSize) => CLZ0.DecompressHeaderless(new MemoryStream(rawData),destination, (int)expectedSize)),
        ("CNS",(rawData,destination, expectedSize) => CNS.DecompressHeaderless(new MemoryStream(rawData),destination, (int)expectedSize)),
        ("LZHudson",(rawData,destination, expectedSize) => LZHudson.DecompressHeaderless(new MemoryStream(rawData),destination, expectedSize)),
        ("LZShrek",(rawData,destination, expectedSize) => LZShrek.DecompressHeaderless(rawData,destination, (int)expectedSize)),
        ("RLHudson",(rawData,destination, expectedSize) => RLHudson.DecompressHeaderless(new MemoryStream(rawData),destination, (int)expectedSize)),
        ("CRILAYLA",(rawData,destination, expectedSize) => CRILAYLA.DecompressHeaderless(rawData,destination.GetBuffer())),
        ("ALLZ",(rawData,destination, expectedSize) => ALLZ.DecompressHeaderless(((ReadOnlySpan<byte>)rawData.AsSpan(4)).AsReadOnlyStream(),destination.GetBuffer(), rawData.AsSpan(0,4))),
    };

    private static void Deflate(byte[] RawData, MemoryStream destination, uint expectedSize)
    {
        using DeflateStream dflStream = new DeflateStream(new MemoryStream(RawData), CompressionMode.Decompress, true);
        dflStream.CopyTo(destination);
    }

    private delegate void RawDecoder(byte[] RawData, MemoryStream destination, uint expectedSize);

    static void PrintType(IFormatInfo format)
    {
        Console.WriteLine($"Detected format: {format.FullName}");
        Console.WriteLine($"MIME: {format.MIMEType}");
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
        var error = $"Format '{format?.FullName}' is not compressed.";
        Error_WriteLineAndExit(error);
        throw new InvalidOperationException(error);
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
            Console.WriteLine($"{source.Length / (1024.0 * 1024):F2} MB input, {destination.Length / (1024.0 * 1024):F2} MB output, compression ratio: {(100.0 * destination.Length / source.Length):F2}%");

            return;
        }
        var error = $"Format '{format?.FullName}' is not compressed.";
        Error_WriteLineAndExit(error);
        throw new InvalidOperationException(error);
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

        var error = $"Unknown format or algorithm: '{algo}'.";
        Error_WriteLineAndExit(error);
        throw new ArgumentException(error, nameof(algo));
    }

#if NET6_0_OR_GREATER
    [DoesNotReturn]
#endif
    static void Error_WriteLineAndExit(string value)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(value);
        Console.ResetColor();
        Environment.Exit(1);
    }
}
