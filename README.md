# AuroraLib.Compression

Provides support for a wide range of compression algorithms primarily used in video games, it offers fast decompression/compression and efficient memory usage.
It is written entirely in managed C# and does not rely on external C++ libraries.

[Nuget Package](https://www.nuget.org/packages/AuroraLib.Compression)

[Benchmarks](https://github.com/Venomalia/AuroraLib.Compression/blob/main/Benchmarks.md)

## Supported Algorithms

| Algorithm      | Description                                                                |
|----------------|----------------------------------------------------------------------------|
| LZ10           | Nintendo LZ10 compression used in various GBA, DS and WII games.           |
| LZ11           | Nintendo LZ11 compression used in various DS and WII games.                |
| LZ40           | Nintendo LZ40 compression mainly used in DS games.                         |
| LZ60           | Nintendo LZ60 corresponds to LZ40 algorithm.                               |
| LZ77           | Nintendo LZ77 based on LZ10 used in WII games and data.                    |
| MIO0*          | Nintendo MIO0 compression mainly used in early Nintendo 64 games.          |
| Yay0*          | Nintendo YAY0 compression used in some Nintendo 64 and GameCube games.     |
| Yaz0*          | Nintendo Yaz0 compression used in games from the N64 to Switch era.        |
| Yaz1*          | Identical to Yaz0 used for data on the N64DD.                              |
| HUF20          | Nintendo Huffman compression algorithm, mainly used in GBA and DS games.   |
| RLE30          | Nintendo RLE compression algorithm used in GBA games.                      |
| LZOn           | Nintendo LZOn compression algorithm mainly used in DS Download Games.      |
| HWGZ           | Hyrule Warriors GZ compression format based on ZLib.                       |
| PRS*           | Sega PRS compression algorithm used in various Sega games.                 |
| LZSega         | A LZSS based compression algorithm used in some Sega GameCube games.       |
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
| LZSS           | LZSS compression algorithm used in many games.                             |
| RefPack        | RefPack compression algorithm used in some EA games.                       |
| GZip           | GZip based on DEFLATE compression algorithm.                               |
| ZLib           | ZLib based on DEFLATE compression algorithm.                               |
| AsuraZlb       | AsuraZlb based on ZLib compression algorithm used in Simpsons The Game.    |
| ZLB            | ZLB based on ZLib compression algorithm used in Star Fox Adventures.       |
| ALLZ           | Aqualead LZ compression algorithm used by a handful of games.              |
| LZO            | Lempel–Ziv–Oberhumer algorithm, focused on decompression speed             |
| LZ4            | LZ4 is similar to LZO focused on decompression speed.                      |
| MDF0           | Konami MDF0 based on ZLib used in Castlevania: The Adventure ReBirth.      |
| Level5         | Level5 compression algorithm, mainly used in Level5 3ds games.             |
| SSZL           | Level5 SSZL algorithm base on LZSS, used in Inazuma Eleven 3.              |
| IECP           | IECP algorithm base on LZSS, used in Fate/Extra.                           |

 `*` Big-endian and little-endian version are supported.
 
## How To Use

Decompress a file with a specific algorithm.
``` csharp
    using FileStream source = new("input.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
    using FileStream destination = new("output.dat", FileMode.Create, FileAccess.ReadWrite, FileShare.None);
    new LZSS().Decompress(source, destination);
```

Compress a file with a specific algorithm.
``` csharp
    using FileStream source = new("input.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
    using FileStream destination = new("output.dat", FileMode.Create, FileAccess.ReadWrite, FileShare.None);
    new LZSS().Compress(source, destination);
```

Check if the file can be decompressed with a specific algorithm.
``` csharp
    using FileStream source = new("input.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
    bool canDecompressed = new LZSS().IsMatch(source);
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
