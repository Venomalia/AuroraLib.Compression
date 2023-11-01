# AuroraLib.Compression

Supports a wide range of compression algorithms mainly used in video games.

[Nuget Package](https://www.nuget.org/packages/AuroraLib.Compression)

## Supported Algorithms

| Algorithm      | Description                                                                |
|----------------|----------------------------------------------------------------------------|
| LZ10           | Nintendo LZ10 compression algorithm used in various games.                 |
| LZ11           | Nintendo LZ11 compression algorithm used in various games.                 |
| LZ77           | Nintendo LZ77 based on LZ10 algorithm used in some games.                  |
| MIO0*          | Nintendo MIO0 compression algorithm used in some Nintendo games.           |
| Yaz0*          | Nintendo Yaz0 compression algorithm used in various games.                 |
| Yaz1*          | Identical to Yaz0 only with a different identifier.                        |
| Yay0*          | Nintendo YAY0 compression algorithm used in some Nintendo games.           |
| RLE30          | Nintendo RLE30 compression algorithm used in GameBoy games.                |
| PRS*           | Sega PRS compression algorithm used in various Sega games.                 |
| LZSega         | LZSega based on LZSS compression used in some GameCube Sega games.         |
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
| LZHudson       | LZHudson based on LZSS compression algorithm used in Mario Party 4.        |
| LZShrek        | LZShrek compression algorithm used in Shrek Super Slam.                    |
| LZSS           | LZSS compression algorithm used in many games.                             |
| RefPack        | RefPack compression algorithm used in EA games.                            |
| GZip           | GZip based on DEFLATE compression algorithm.                               |
| ZLib           | ZLib based on DEFLATE compression algorithm.                               |
| AsuraZlb       | AsuraZlb based on ZLib compression algorithm used in Simpsons The Game.    |
| ZLB            | ZLB based on ZLib compression algorithm used in Star Fox Adventures.       |

 `*` Big-endian and little-endian version are supported.
 
## How To Use

Decompress a file.
``` csharp
    using FileStream source = new("input.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
    using FileStream destination = new("output.dat", FileMode.Create, FileAccess.ReadWrite, FileShare.None);
    new LZSS().Decompress(source, destination);
```

Compress a file.
``` csharp
    using FileStream source = new("input.dat", FileMode.Open, FileAccess.Read, FileShare.Read);
    using FileStream destination = new("output.dat", FileMode.Create, FileAccess.ReadWrite, FileShare.None);
    new LZSS().Compress(source, destination);
```

# Credits

- [Nickworonekin](https://github.com/nickworonekin/puyotools) Puyo Tools inspired the LZ Decode and Encode code and reference for CNX2, LZ00, LZ01, LZ10, LZ11, PRS algorithms.
- [Haruhiko Okumura](https://oku.edu.mie-u.ac.jp/) reference his original C implementation of the LZSS algorithm.
- [Daniel-McCarthy](https://github.com/Daniel-McCarthy/Mr-Peeps-Compressor) reference for MIO0, YAZ0, YAY0 algorithm.
- [Niotso.wiki](http://wiki.niotso.org/RefPack) reference for RefPack algorithm.
- [Sukharah](https://github.com/sukharah/CLZ-Compression) reference for CLZ0 algorithm.
- [Gamemasterplc](https://github.com/gamemasterplc/mpbintools/blob/master/bindump.c#L240C6-L240C21) reference for LZHudson algorithm.
- [KirbyUK](https://github.com/ShrekBoards/shrek-superslam/blob/master/src/compression.rs#L66) reference for LZShrek algorithm.
