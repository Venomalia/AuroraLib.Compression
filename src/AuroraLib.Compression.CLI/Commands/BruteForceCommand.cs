using AuroraLib.Compression.CLI.Algorithms;
using AuroraLib.Compression.Formats.Activision;
using AuroraLib.Compression.Formats.Camelot;
using AuroraLib.Compression.Formats.Common;
using AuroraLib.Compression.Formats.CRI;
using AuroraLib.Compression.Formats.EA;
using AuroraLib.Compression.Formats.HudsonSoft;
using AuroraLib.Compression.Formats.Marvelous;
using AuroraLib.Compression.Formats.Nintendo;
using AuroraLib.Compression.Formats.Sega;
using AuroraLib.Compression.Formats.Specialized;
using AuroraLib.Core;
using AuroraLib.Core.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace AuroraLib.Compression.CLI.Commands
{
    internal static class BruteForceCommand
    {
        public static void Execute(string sourceFile, string destinationFolder, long offset, long expectedSize)
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
                        SaveFile(destinationFolder, ms, "SUCCESS~" + decoder.Name);
                        // continue in case of false positive.
                        continue;
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

                if (ms.Position > 0x400)
                {
                    ms.SetLength(ms.Position);
                    SaveFile(destinationFolder, ms, "FAIL~" + decoder.Name);
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

            static void SaveFile(string destinationFolder, MemoryStream destination, string decoderName)
            {
                Directory.CreateDirectory(destinationFolder);
                var outputFile = Path.Combine(destinationFolder, decoderName + ".bin");

                using FileStream file = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
                destination.WriteTo(file);
                HelpPrinter.PrintSaveFile(outputFile);
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
    }
}
