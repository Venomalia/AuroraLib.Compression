# AuroraLib.Compression

AuroraLib.Compression is a high-performance, memory-efficient compression library providing a wide range of algorithms commonly used in video games and game engines.
The library is written entirely in managed C# and supports multiple .NET versions without requiring external native dependencies.

The goal of AuroraLib.Compression is not to provide the fastest possible implementation of a single algorithm, but to offer a broad collection of compression formats while keeping the library lightweight, flexible, and easy to integrate.

## Command line tool
[Download Auracomp](https://github.com/Venomalia/AuroraLib.Compression/releases)

## Benchmarks
[View Benchmarks](https://github.com/Venomalia/AuroraLib.Compression/blob/main/Benchmarks.md)

## Supported Algorithms

### AuroraLib.Compression
Core compression algorithms and widely used general-purpose formats.

[![NuGet Package](https://img.shields.io/nuget/v/AuroraLib.Compression.svg?style=flat-square&label=NuGet%20Package)](https://www.nuget.org/packages/AuroraLib.Compression)

| Algorithm      | Description                                                                |
|----------------|----------------------------------------------------------------------------|
| LZO            | Lempel–Ziv–Oberhumer algorithm, focused on fast decompression speed        |
| LZ4            | Very fast LZ-based algorithm designed for high decompression speed.        |
| LZ4Legacy      | Legacy variant of the LZ4 compression format.                              |
| LZSS           | Classic LZSS compression algorithm widely used.                            |
| GZip           | GZip compression format based on the DEFLATE algorithm.                    |
| ZLib           | ZLib compression format using the DEFLATE algorithm.                       |
| Brotli         | Modern compression by Jyrki Alakuijala and Zoltán Szabadka. (.NET 6+)      |
| FastLZ         | Lightweight and fast compression algorithm by Ariya Hidayat.               |
| aPLib          | Compression algorithm designed for high compression ratios, by Jørgen Ibsen.|
| Snappy         | Google's Snappy previously known as Zippy, focused on decompression speed. |

### AuroraLib.Compression.Nintendo
Compression formats used across Nintendo platforms such as GBA, DS, Wii, and more.

[![NuGet Package](https://img.shields.io/nuget/v/AuroraLib.Compression.Nintendo.svg?style=flat-square&label=NuGet%20Package)](https://www.nuget.org/packages/AuroraLib.Compression.Nintendo)

| Algorithm      | Description                                                                |
|----------------|----------------------------------------------------------------------------|
| LZ10           | Nintendo LZ10 compression used in various GBA, DS and Wii games.           |
| LZ11           | Nintendo LZ11 compression used in various DS and Wii games.                |
| LZ40           | Nintendo LZ40 compression, mainly used in DS games.                        |
| LZ60           | Nintendo LZ60 corresponds to LZ40 algorithm.                               |
| LZ77           | Nintendo LZ77 based on LZ10 used in Wii games and data.                    |
| HUF20          | Nintendo Huffman compression algorithm, mainly used in GBA and DS games.   |
| RLE30          | Nintendo RLE compression algorithm used in GBA games.                      |
| BLZ            | Nintendo BLZ compression, mainly used on the 3ds games.                    |
| LZOn           | Nintendo LZOn compression algorithm mainly used in DS Download Games.      |
| Yay0*          | Nintendo YAY0 compression used in some Nintendo 64 and GameCube games.     |
| Yaz0*          | Nintendo Yaz0 compression used in games from the N64 to Switch era.        |
| Yaz1*          | Identical to Yaz0 used for data on the N64DD.                              |
| MIO0*          | Nintendo MIO0 compression mainly used in early Nintendo 64 games.          |
| SMSR00         | Nintendo SMSR00 compression, mainly used in Yoshi's Story.                 |
| Level5         | Level5 compression algorithm, mainly used in Level5 3ds games.             |
| Level5LZSS     | Level5 SSZL algorithm base on LZSS, used in Inazuma Eleven 3.              |
| LZHudson       | A LZSS based compression algorithm used in Mario Party 4-7.                |
| RLHudson       | A RLE based compression algorithm used in Mario Party 4-7.                 |
| COMP           | Based on LZ11 algorithm, used in some Puyo Puyo.                           |
| CXLZ           | Based on LZ10 algorithm, used in some Puyo Puyo.                           |
| GCLZ           | Based on LZ10 algorithm, used in Pandora's Tower.                          |
| LZ_3DS         | based on LZ10 algorithm, used in some 3DS games.                           |

 `*` Both big-endian and little-endian variants are supported.

### AuroraLib.Compression.Sega
Compression formats found in Sega games and platforms.

[![NuGet Package](https://img.shields.io/nuget/v/AuroraLib.Compression.Sega.svg?style=flat-square&label=NuGet%20Package)](https://www.nuget.org/packages/AuroraLib.Compression.Sega)

| Algorithm      | Description                                                                |
|----------------|----------------------------------------------------------------------------|
| PRS*           | Sega PRS compression algorithm used in various Sega games.                 |
| AKLZ           | A LZSS based algorithm used in Skies of Arcadia Legends.                   |
| CNX2           | Sega CNX2 algorithm, used in some Puyo Puyo.                               |
| LZ00           | Sega LZ00 is based on LZSS algorithm with encryption.                      |
| LZ01           | Sega LZ01 based on LZSS algorithm, used in some Puyo Puyo.                 |
| LZSega         | A LZSS based compression algorithm used in some Sega GameCube games.       |

 `*` Both big-endian and little-endian variants are supported.

### AuroraLib.Compression-Extended
Additional compression formats used by various game studios and engines.

[![NuGet Package](https://img.shields.io/nuget/v/AuroraLib.Compression-Extended.svg?style=flat-square&label=NuGet%20Package)](https://www.nuget.org/packages/AuroraLib.Compression-Extended)

| Algorithm      | Description                                                                |
|----------------|----------------------------------------------------------------------------|
| ALLZ           | Aqualead LZ compression algorithm used by a handful of games.              |
| HWGZ*          | Hyrule Warriors GZ compression format based on ZLib.                       |
| RareZip        | Rareware Zip based on DEFLATE, used in Banjo-Kazooie.                      |
| ZLB            | ZLB based on ZLib compression algorithm used in Star Fox Adventures.       |
| RefPack        | RefPack compression algorithm used in some EA games.                       |
| CRILAYLA       | Algorithm by CRI Middleware, used in games built with the CRIWARE toolset. |
| MDB4           | Sega MDB4 based on LZSS, used in Typing of the Dead for the PlayStation 2. |
| CNS            | CNS compression algorithm, used in Games from Red Entertainment.           |
| CLZ0           | CLZ0 compression algorithm, used in Games from Victor Interactive Software.|
| FCMP           | FCMP based on LZSS algorithm, used in Muramasa The Demon Blade.            |
| LZ02           | LZ02 compression algorithm used in Mario Golf: Toadstool Tour.             |
| LZShrek        | LZShrek compression algorithm used in Shrek Super Slam.                    |
| AsuraZlb       | AsuraZlb based on ZLib compression algorithm used in Simpsons The Game.    |
| HIG            | High Impact Games WAD compression, similar to LZO.			              |
| MDF0           | Konami MDF0 based on ZLib used in Castlevania: The Adventure ReBirth.      |
| GCZ            | Konami GCZ based on LZSS, mainly used in Konami/Bemani music games.        |
| SDPC           | TREVA Entertainment SDPC compression based on LZO.                         |
| SSZL           | SynSophiaZip based on Zlib with Mersenne Twister xor encryption.           |
| IECP           | IECP algorithm based on LZSS, used in Fate/Extra.                          |
| ECD            | Rocket Company ECD algorithm based on LZSS, used in Kanken Training 2.     |
| WFLZ           | WayForward's LZ algorithm, focused on decompression speed.                 |
| ZLWF           | WayForward's LZ chunk header.                                              |

 `*` Both big-endian and little-endian variants are supported.
 
## How To Use

### Decompress a File Using a Specific Algorithm
``` csharp
    using FileStream source = new("input.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
    using FileStream destination = new("output.dat", FileMode.Create, FileAccess.ReadWrite, FileShare.None);
    new LZSS().Decompress(source, destination);
```

### Compress a File Using a Specific Algorithm
``` csharp
    using FileStream source = new("input.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
    using FileStream destination = new("output.dat", FileMode.Create, FileAccess.ReadWrite, FileShare.None);
    new LZSS().Compress(source, destination, CompressionSettings.Balanced);
```

### Check If a File Matches a Specific Compression Algorithm
``` csharp
    using FileStream source = new("input.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
    bool canDecompressed = LZSS.IsMatchStatic(source);
```

### Configure an Compression Encoder
``` csharp
    Yaz0 encoder = new Yaz0
    {
        FormatByteOrder = Endian.Big,  // Use big-endian byte order
    };
	
    CompressionSettings settings = new CompressionSettings(
        quality: 10,  // 0 (fastest, lowest compression) to 15 (slowest, best compression)
        maxWindowBits: 12, // Maximum LZ window size (for LZ-based algos) 
        strategy: CompresionStrategy.CompatibilityMode // Enables compatibility features for older or simpler decoders.
    );
	
	encoder.Compress(source, destination, settings);
```

### Automatically Detect and Decompress Using a Recognized Algorithm
This approach is useful when you don't know in advance which compression algorithm was used on a file.
``` csharp
    FormatDictionary formats = new FormatDictionary(new Assembly[] 
    { 
        typeof(LZSS).Assembly,    // Base algorithms assembly
        typeof(Yaz0).Assembly     // Nintendo algorithms assembly
    });

    using FileStream source = new("input.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
    ReadOnlySpan<char> fileName = Path.GetFileName("input.dat");

    // Identify the compression format
    if (formats.Identify(source, fileName, out IFormatInfo format) && format.Class != null)
    {
     	// Create an instance of the detected format
        var decoder = format.CreateInstance();
		
        if (decoder is ICompressionDecoder compressionDecoder)
        {
            using Stream destination = compressionDecoder.Decompress(source);
            // Use the decompressed stream as needed
        }
    }
```

# Credits

- [LZ4](https://github.com/lz4/lz4/tree/dev/doc) reference for LZ4 algorithm.
- [Haruhiko Okumura](https://oku.edu.mie-u.ac.jp/) reference his original C implementation of the LZSS algorithm.
- [Oberhumer](https://www.oberhumer.com/opensource/lzo/) reference for the LZO algorithm.
- [Google](https://github.com/google/snappy) reference for the snappy algorithm.
- [Ariya](https://github.com/ariya/FastLZ) reference for the FastLZ algorithm.
- [ibsensoftware](https://ibsensoftware.com/products_aPLib.html) Creator of aPLib.
- [Daniel-McCarthy](https://github.com/Daniel-McCarthy/Mr-Peeps-Compressor) reference for MIO0, YAZ0, YAY0 algorithm.
- [Nickworonekin](https://github.com/nickworonekin/puyotools) Puyo Tools reference for CNX2, LZ00, LZ01, LZ10, LZ11, PRS algorithms.
- [Kuriimu](https://github.com/IcySon55/Kuriimu/blob/ebfbf8de50755cc32a7e1ea4aee394628d49d3d2/src/Kontract/Compression/Huffman.cs#L9) reference for HUF20 algorithm.
- [Niotso.wiki](http://wiki.niotso.org/RefPack) reference for RefPack algorithm.
- [Sukharah](https://github.com/sukharah/CLZ-Compression) reference for CLZ0 algorithm.
- [Gamemasterplc](https://github.com/gamemasterplc/mpbintools/blob/master/bindump.c#L240C6-L240C21) reference for LZHudson algorithm.
- [KirbyUK](https://github.com/ShrekBoards/shrek-superslam/blob/master/src/compression.rs#L66) reference for LZShrek algorithm.
- [Brolijah](https://github.com/Brolijah/Aqualead_LZSS) reference for ALLZ algorithm.thm.
- [CUE](https://www.romhacking.net/utilities/826/) reference for LZ40 algorithm.
- [hack64](https://hack64.net/wiki/doku.php?id=yoshis_story:smsr00_compression) reference for SMSR00 algorithm.
- [RareZip](https://github.com/MittenzHugg/rarezip/tree/2c4ba146c1b2fec851d3db8cf455c6af090bc544) reference for RareZip algorithm.
- [ShaneYCG](https://github.com/ShaneYCG/wflz/tree/master) reference for WFLZ algorithm.
- [UYA_pyTools](https://github.com/electrogecko/UYA_pyTools/blob/main/SM/tjzip_dump.py) reference for HIG algorithm.
