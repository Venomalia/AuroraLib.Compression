using AuroraLib.Compression.Algorithms;
using AuroraLib.Compression.CLI.Algorithms;
using AuroraLib.Core.Format;
using System.Reflection;
using static AuroraLib.Compression.Algorithms.RefPack;

namespace AuroraLib.Compression.CLI
{
    internal static class FormatService
    {
        public static FormatDictionary Formats { get; }

        static FormatService()
        {

            Assembly AuroraLib_Compression = typeof(LZO).Assembly;
            Assembly AuroraLib_Compression_Extended = typeof(AKLZ).Assembly; // Assembly.LoadFrom(path); ? 
            Assembly Extern = typeof(Zstd).Assembly;

            Formats = new FormatDictionary(new Assembly[] { AuroraLib_Compression, AuroraLib_Compression_Extended, Extern })
            {
                new SpezialFormatInfo<HUF20>("Nintendo HUF20 4bits", new MediaType(MIMEType.Application, "x-nintendo-huf20-4bits"), ".huf4", () => new HUF20() { Type = HUF20.CompressionType.Huffman4bits }),
                new SpezialFormatInfo<HUF20>("Nintendo HUF20 8bits", new MediaType(MIMEType.Application, "x-nintendo-huf20-8bits"), ".huf8", () => new HUF20() { Type = HUF20.CompressionType.Huffman8bits }),
                new SpezialFormatInfo<LZ77>("Nintendo LZ77 Chunk", new MediaType(MIMEType.Application, "x-nintendo-lz10+lz77chunk"), string.Empty, () => new LZ77() { Type = LZ77.CompressionType.ChunkLZ10 }),
                new SpezialFormatInfo<LZ77>("Nintendo LZ77 LZ11", new MediaType(MIMEType.Application, "x-nintendo-lz11+lz77"), string.Empty, () => new LZ77() { Type = LZ77.CompressionType.LZ11 }),
                new SpezialFormatInfo<LZ77>("Nintendo LZ77 RLE30", new MediaType(MIMEType.Application, "x-nintendo-rle30+lz77"), string.Empty, () => new LZ77() { Type = LZ77.CompressionType.RLE30 }),
                new SpezialFormatInfo<LZ77>("Nintendo LZ77 HUF20 4bits", new MediaType(MIMEType.Application, "x-nintendo-huf20-4bits+lz77"), string.Empty, () => new LZ77() { Type = LZ77.CompressionType.HUF20_4bits }),
                new SpezialFormatInfo<LZ77>("Nintendo LZ77 HUF20 8bits", new MediaType(MIMEType.Application, "x-nintendo-huf20-8bits+lz77"), string.Empty, () => new LZ77() { Type = LZ77.CompressionType.HUF20_8bits }),
                new SpezialFormatInfo<Level5>("Level5 Huffman 4Bit", new MediaType(MIMEType.Application, "x-level5-huffman4bit"), ".l5huf4", () => new Level5() { Type = Level5.CompressionType.Huffman4Bit }),
                new SpezialFormatInfo<Level5>("Level5 Huffman 8Bit", new MediaType(MIMEType.Application, "x-level5-huffman8bit"), ".l5huf8", () => new Level5() { Type = Level5.CompressionType.Huffman8Bit }),
                new SpezialFormatInfo<Level5>("Level5 LZ10", new MediaType(MIMEType.Application, "x-level5-lz10"), ".l5LZ10", () => new Level5() { Type = Level5.CompressionType.LZ10 }),
                new SpezialFormatInfo<Level5>("Level5 ZLib", new MediaType(MIMEType.Application, "x-level5-zlib"), ".l5ZLib", () => new Level5() { Type = Level5.CompressionType.ZLib }),
                new SpezialFormatInfo<Level5>("Level5 RLE", new MediaType(MIMEType.Application, "x-level5-rle"), ".l5RLE", () => new Level5() { Type = Level5.CompressionType.RLE }),
                new SpezialFormatInfo<RefPack>("RefPack v1", new MediaType(MIMEType.Application, "x-ea-refpack+v1"), string.Empty, () => new RefPack() { Options = OptionFlags.Default }),
                new SpezialFormatInfo<RefPack>("RefPack Maxis", new MediaType(MIMEType.Application, "x-ea-refpack+maxis"), string.Empty, () => new RefPack() { Options = OptionFlags.Default | OptionFlags.UsePreHeader }),
                new SpezialFormatInfo<RefPack>("RefPack v3", new MediaType(MIMEType.Application, "x-ea-refpack+v3"), string.Empty, () => new RefPack() { Options = OptionFlags.Default | OptionFlags.UseInt32 })
            };
        }

        public static IFormatInfo? GetFormatInfo(string algo)
        {
            algo = algo.ToLower();
            foreach (var format in FormatService.Formats.Values)
            {
                if (format.FullName.ToLower() == algo || format.MIMEType.ToString() == algo || format.Class?.Name.ToLower() == algo)
                {
                    return format;
                }
            }
            return null;
        }
    }
}
