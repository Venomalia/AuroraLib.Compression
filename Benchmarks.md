BenchmarkDotNet v0.14.0, Windows 10, AMD Ryzen 7 3800X, 1 CPU, 16 logical and 8 physical cores. NET SDK 8.0.400

| Method     | Algorithm | MB | Mean         | Error       | StdDev      | Gen0    | Allocated |
|----------- |---------- |--- |-------------:|------------:|------------:|--------:|----------:|
| Compress   | ALLZ      | 1  |  72,736.5 us | 1,434.81 us | 2,147.56 us |       - |    7087 B |
| Decompress | ALLZ      | 1  |   3,455.3 us |    48.01 us |    42.56 us |       - |     141 B |
| Compress   | CLZ0      | 1  |  21,654.8 us |   424.41 us |   471.74 us |       - |    6856 B |
| Decompress | CLZ0      | 1  |   3,901.3 us |    46.34 us |    43.34 us |       - |     213 B |
| Compress   | CNS       | 1  |   5,601.4 us |    77.30 us |    68.52 us |       - |    6805 B |
| Decompress | CNS       | 1  |   3,598.0 us |    42.41 us |    39.67 us |       - |     197 B |
| Compress   | CNX2      | 1  |  19,876.6 us |   348.84 us |   309.24 us |       - |    6976 B |
| Decompress | CNX2      | 1  |   3,653.0 us |    43.65 us |    40.83 us |       - |     245 B |
| Compress   | HUF20     | 1  |  17,689.6 us |   201.15 us |   188.16 us | 31.2500 |  398903 B |
| Decompress | HUF20     | 1  |   9,574.7 us |   185.77 us |   241.56 us |       - |     106 B |
| Compress   | HWGZ      | 1  |   7,394.5 us |    92.70 us |    86.71 us |       - |    4182 B |
| Decompress | HWGZ      | 1  |   1,174.0 us |    14.27 us |    12.65 us |       - |    6105 B |
| Compress   | LZ00      | 1  |  23,128.5 us |   434.20 us |   445.90 us |       - |    6968 B |
| Decompress | LZ00      | 1  |   4,205.2 us |    50.96 us |    47.66 us |       - |     299 B |
| Compress   | LZ02      | 1  |  44,243.8 us |   881.28 us | 1,114.54 us |       - |    6889 B |
| Decompress | LZ02      | 1  |   2,986.9 us |    34.56 us |    32.33 us |       - |     215 B |
| Compress   | LZ10      | 1  |  37,801.3 us |   669.52 us |   771.02 us |       - |    6864 B |
| Decompress | LZ10      | 1  |   3,828.9 us |     6.12 us |     5.73 us |       - |     189 B |
| Compress   | LZ11      | 1  |  35,927.7 us |   697.26 us |   618.10 us |       - |    6891 B |
| Decompress | LZ11      | 1  |   3,500.1 us |     3.03 us |     2.68 us |       - |     189 B |
| Compress   | LZ40      | 1  |  20,452.9 us |   319.00 us |   298.39 us |       - |    6950 B |
| Decompress | LZ40      | 1  |   3,532.6 us |     6.55 us |     6.13 us |       - |     141 B |
| Compress   | LZ4Legacy | 1  | 131,799.8 us | 2,470.23 us | 4,578.74 us |       - |    7218 B |
| Decompress | LZ4Legacy | 1  |   2,649.3 us |    32.52 us |    30.41 us |       - |     141 B |
| Compress   | LZO       | 1  | 129,463.7 us | 2,415.20 us | 5,832.97 us |       - |    7156 B |
| Decompress | LZO       | 1  |   2,902.7 us |    39.59 us |    37.03 us |       - |     142 B |
| Compress   | LZSS      | 1  |  21,672.2 us |   391.68 us |   347.22 us |       - |    6922 B |
| Decompress | LZSS      | 1  |   3,741.9 us |    39.14 us |    36.61 us |       - |     213 B |
| Compress   | LZShrek   | 1  |  39,688.8 us |   792.59 us |   778.43 us |       - |    7010 B |
| Decompress | LZShrek   | 1  |   2,603.3 us |    29.79 us |    27.86 us |       - |     141 B |
| Compress   | MIO0      | 1  |  40,157.3 us |   779.19 us |   985.42 us |       - |    7103 B |
| Decompress | MIO0      | 1  |   2,284.3 us |    19.84 us |    18.56 us |       - |     165 B |
| Compress   | PRS       | 1  |  34,021.8 us |   580.83 us |   543.30 us |       - |    6879 B |
| Decompress | PRS       | 1  |   2,131.5 us |    28.23 us |    26.40 us |       - |     237 B |
| Compress   | RLE30     | 1  |   3,810.5 us |    38.89 us |    36.38 us |       - |      91 B |
| Decompress | RLE30     | 1  |     662.6 us |     6.05 us |     5.66 us |       - |      65 B |
| Compress   | RefPack   | 1  |  69,832.0 us | 1,364.97 us | 2,164.98 us |       - |    6967 B |
| Decompress | RefPack   | 1  |   1,254.7 us |    14.06 us |    13.15 us |       - |     139 B |
| Compress   | Yay0      | 1  |  23,130.2 us |   439.65 us |   431.79 us |       - |    7050 B |
| Decompress | Yay0      | 1  |   3,241.4 us |    31.60 us |    29.56 us |       - |     478 B |
| Compress   | Yaz0      | 1  |  23,597.2 us |   470.32 us |   461.92 us |       - |    6872 B |
| Decompress | Yaz0      | 1  |   2,653.0 us |    33.51 us |    31.34 us |       - |     213 B |