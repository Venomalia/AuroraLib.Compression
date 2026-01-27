# AuroraLib.Compression

AuroraLib.Compression is a high-performance, memory-efficient library for a broad range of compression algorithms primarily used in video games.
Designed for seamless use across multiple .NET versions. Written entirely in managed C#, it eliminates the need for external C++ libraries while offering fast decompression and compression.

## Supported Algorithms

### AuroraLib.Compression

[![NuGet Package](https://img.shields.io/nuget/v/AuroraLib.Compression.svg?style=flat-square&label=NuGet%20Package)](https://www.nuget.org/packages/AuroraLib.Compression)

| Algorithm      | Description                                                                |
|----------------|----------------------------------------------------------------------------|
| LZO            | Lempel–Ziv–Oberhumer algorithm, focused on decompression speed             |
| LZ4            | LZ4 is similar to LZO focused on decompression speed.                      |
| LZSS           | LZSS compression algorithm used in many games.                             |
| GZip           | GZip based on DEFLATE compression algorithm.                               |
| ZLib           | ZLib based on DEFLATE compression algorithm.                               |
| LZ10           | Nintendo LZ10 compression used in various GBA, DS and WII games.           |
| LZ11           | Nintendo LZ11 compression used in various DS and WII games.                |
| LZ77           | Nintendo LZ77 based on LZ10 used in WII games and data.                    |
| Yay0*          | Nintendo YAY0 compression used in some Nintendo 64 and GameCube games.     |
| Yaz0*          | Nintendo Yaz0 compression used in games from the N64 to Switch era.        |
| HUF20          | Nintendo Huffman compression algorithm, mainly used in GBA and DS games.   |
| PRS*           | Sega PRS compression algorithm used in various Sega games.                 |
| RefPack        | RefPack compression algorithm used in some EA games.                       |

 `*` Big-endian and little-endian version are supported.
 
### AuroraLib.Compression-Extended

[![NuGet Package](https://img.shields.io/nuget/v/AuroraLib.Compression-Extended.svg?style=flat-square&label=NuGet%20Package)](https://www.nuget.org/packages/AuroraLib.Compression-Extended)

| Algorithm      | Description                                                                |
|----------------|----------------------------------------------------------------------------|
| BLZ            | Nintendo BLZ compression, mainly used on the 3ds games.                    |
| LZ40           | Nintendo LZ40 compression, mainly used in DS games.                        |
| LZ60           | Nintendo LZ60 corresponds to LZ40 algorithm.                               |
| SMSR00         | Nintendo SMSR00 compression, mainly used in Yoshi's Story.                 |
| MIO0*          | Nintendo MIO0 compression mainly used in early Nintendo 64 games.          |
| Yaz1*          | Identical to Yaz0 used for data on the N64DD.                              |
| RLE30          | Nintendo RLE compression algorithm used in GBA games.                      |
| ALLZ           | Aqualead LZ compression algorithm used by a handful of games.              |
| LZOn           | Nintendo LZOn compression algorithm mainly used in DS Download Games.      |
| HWGZ*          | Hyrule Warriors GZ compression format based on ZLib.                       |
| LZSega         | A LZSS based compression algorithm used in some Sega GameCube games.       |

| CRILAYLA       | Algorithm by CRI Middleware, used in games built with the CRIWARE toolset. |
| CNX2           | Sega CNX2 algorithm, used in some Puyo Puyo.                               |
| COMP           | Sega COMP based on LZ11 algorithm, used in some Puyo Puyo.                 |
| CXLZ           | Sega CXLZ based on LZ10 algorithm, used in some Puyo Puyo.                 |
| LZ00           | Sega LZ00 is based on LZSS algorithm with encryption.                      |
| LZ01           | Sega LZ01 based on LZSS algorithm, used in some Puyo Puyo.                 |
| AKLZ           | A LZSS based algorithm used in Skies of Arcadia Legends.                   |
| CNS            | CNS compression algorithm, used in Games from Red Entertainment.           |
| CLZ0           | CLZ0 compression algorithm, used in Games from Victor Interactive Software.|
| FCMP           | FCMP based on LZSS algorithm, used in Muramasa The Demon Blade.            |
| GCLZ           | GCLZ based on LZ10 algorithm, used in Pandora's Tower.                     |
| LZ02           | LZ02 compression algorithm used in Mario Golf: Toadstool Tour.             |
| LZHudson       | A LZSS based compression algorithm used in Mario Party 4-7.                |
| RLHudson       | A RLE based compression algorithm used in Mario Party 4-7.                 |
| LZShrek        | LZShrek compression algorithm used in Shrek Super Slam.                    |
| AsuraZlb       | AsuraZlb based on ZLib compression algorithm used in Simpsons The Game.    |
| ZLB            | ZLB based on ZLib compression algorithm used in Star Fox Adventures.       |
| MDF0           | Konami MDF0 based on ZLib used in Castlevania: The Adventure ReBirth.      |
| GCZ            | Konami GCZ based on LZSS, mainly used in Konami/Bemani music games.        |
| Level5         | Level5 compression algorithm, mainly used in Level5 3ds games.             |
| Level5LZSS     | Level5 SSZL algorithm base on LZSS, used in Inazuma Eleven 3.              |
| SDPC           | TREVA Entertainment SDPC compression based on LZO.                         |
| SSZL           | SynSophiaZip based on Zlib with Mersenne Twister xor encryption.           |
| IECP           | IECP algorithm base on LZSS, used in Fate/Extra.                           |
| ECD            | Rocket Company ECD algorithm base on LZSS, used in Kanken Training 2.      |
| aPLib          | aPLib is a pure LZ-based compression algorithm by Jørgen Ibsen.            |

 `*` Big-endian and little-endian version are supported.
 
[Command line tool](https://github.com/Venomalia/AuroraLib.Compression/releases)

[Benchmarks](https://github.com/Venomalia/AuroraLib.Compression/blob/main/Benchmarks.md)

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
    new LZSS().Compress(source, destination);
```

### Check If a File Matches a Specific Compression Algorithm
``` csharp
    using FileStream source = new("input.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
    bool canDecompressed = new LZSS().IsMatch(source);
```

### Configure an Compression Encoder
``` csharp
    Yaz0 encoder = new Yaz0
    {
        FormatByteOrder = Endian.Big,  // Use big-endian byte order
        LookAhead = false              // Disable look-ahead optimization
    };
```

### Automatically Detect and Decompress Using a Recognized Algorithm
``` csharp
    FormatDictionary formats = new FormatDictionary(new Assembly[] { AuroraLib_Compression, AuroraLib_Compression_Extended });

    using FileStream source = new("input.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
    ReadOnlySpan<char> fileName = Path.GetFileName("input.dat");

    if (formats.Identify(source, fileName, out IFormatInfo format) && format.Class != null)
    {
        var decoder = format.CreateInstance();
        if (decoder is ICompressionDecoder compressionDecoder)
        {
            using Stream destination = compressionDecoder.Decompress(source);
            // Use the decompressed stream as needed
        }
    }
```

# Credits

- [Nickworonekin](https://github.com/nickworonekin/puyotools) Puyo Tools inspired the LZ Decode and Encode code and reference for CNX2, LZ00, LZ01, LZ10, LZ11, PRS algorithms.
- [Haruhiko Okumura](https://oku.edu.mie-u.ac.jp/) reference his original C implementation of the LZSS algorithm.
- [Daniel-McCarthy](https://github.com/Daniel-McCarthy/Mr-Peeps-Compressor) reference for MIO0, YAZ0, YAY0 algorithm.
- [Kuriimu](https://github.com/IcySon55/Kuriimu/blob/ebfbf8de50755cc32a7e1ea4aee394628d49d3d2/src/Kontract/Compression/Huffman.cs#L9) reference for HUF20 algorithm.
- [Niotso.wiki](http://wiki.niotso.org/RefPack) reference for RefPack algorithm.
- [Sukharah](https://github.com/sukharah/CLZ-Compression) reference for CLZ0 algorithm.
- [Gamemasterplc](https://github.com/gamemasterplc/mpbintools/blob/master/bindump.c#L240C6-L240C21) reference for LZHudson algorithm.
- [KirbyUK](https://github.com/ShrekBoards/shrek-superslam/blob/master/src/compression.rs#L66) reference for LZShrek algorithm.
- [Brolijah](https://github.com/Brolijah/Aqualead_LZSS) reference for ALLZ algorithm.thm.
- [CUE](https://www.romhacking.net/utilities/826/) reference for LZ40 algorithm.
