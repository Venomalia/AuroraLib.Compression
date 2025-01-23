BenchmarkDotNet v0.14.0, Windows 10, AMD Ryzen 7 3800X, 1 CPU, 16 logical and 8 physical cores. NET SDK 8.0.400

| Method     | Algorithm | MB | Mean         | Error       | StdDev      | Gen0     | Allocated |
|----------- |---------- |--- |-------------:|------------:|------------:|---------:|----------:|
| Compress   | ALLZ      | 1  |  76,131.8 us | 1,501.29 us | 1,541.72 us |       - |    7285 B |
| Decompress | ALLZ      | 1  |   5,137.3 us |    10.04 us |     8.38 us |       - |     243 B |
| Compress   | CLZ0      | 1  |  21,671.8 us |   364.12 us |   373.92 us |       - |    6890 B |
| Decompress | CLZ0      | 1  |   4,105.6 us |    55.51 us |    51.93 us |       - |     315 B |
| Compress   | CNS       | 1  |   5,519.9 us |    36.18 us |    32.07 us |       - |    6813 B |
| Decompress | CNS       | 1  |   3,591.4 us |    13.58 us |    10.60 us |       - |     197 B |
| Compress   | CNX2      | 1  |  35,482.6 us |   673.81 us |   748.94 us |       - |    7105 B |
| Decompress | CNX2      | 1  |   3,726.3 us |    29.49 us |    24.62 us |       - |     341 B |
| Compress   | HUF20     | 1  |  17,885.8 us |   252.34 us |   236.04 us | 31.2500 |  398999 B |
| Decompress | HUF20     | 1  |   9,842.2 us |   190.93 us |   169.26 us |       - |      76 B |
| Compress   | HWGZ      | 1  |   7,464.5 us |    80.62 us |    75.41 us |       - |    4182 B |
| Decompress | HWGZ      | 1  |   1,190.2 us |     7.34 us |     6.87 us |       - |    6105 B |
| Compress   | LZ00      | 1  |  23,302.9 us |   334.38 us |   312.78 us |       - |    7096 B |
| Decompress | LZ00      | 1  |   4,317.0 us |    45.56 us |    42.62 us |       - |     395 B |
| Compress   | LZ02      | 1  |  26,137.6 us |   514.95 us |   572.36 us |       - |    6916 B |
| Decompress | LZ02      | 1  |   2,727.5 us |     4.35 us |     3.86 us |       - |     284 B |
| Compress   | LZ10      | 1  |  39,257.7 us |   726.95 us |   679.99 us |       - |    7057 B |
| Decompress | LZ10      | 1  |   3,885.6 us |     3.97 us |     3.52 us |       - |     285 B |
| Compress   | LZ11      | 1  |  20,563.2 us |   343.27 us |   286.65 us |       - |    6904 B |
| Decompress | LZ11      | 1  |   3,651.7 us |     4.06 us |     3.80 us |       - |     284 B |
| Compress   | LZ40      | 1  |  21,386.8 us |   426.25 us |   507.42 us |       - |    6904 B |
| Decompress | LZ40      | 1  |   3,683.0 us |    30.55 us |    28.58 us |       - |     140 B |
| Compress   | LZ4Legacy | 1  |  77,821.4 us | 1,500.67 us | 1,897.87 us |       - |    7110 B |
| Decompress | LZ4Legacy | 1  |   2,680.5 us |     1.76 us |     1.47 us |       - |     141 B |
| Compress   | LZO       | 1  |  75,013.5 us | 1,479.93 us | 2,074.66 us |       - |    7058 B |
| Decompress | LZO       | 1  |   3,044.1 us |    37.75 us |    35.31 us |       - |     141 B |
| Compress   | LZSS      | 1  |  22,289.8 us |   429.59 us |   421.91 us |       - |    6962 B |
| Decompress | LZSS      | 1  |   3,917.3 us |    16.26 us |    14.41 us |       - |     309 B |
| Compress   | LZShrek   | 1  |  40,945.5 us |   811.16 us | 1,110.32 us |       - |   15618 B |
| Decompress | LZShrek   | 1  |   2,621.6 us |    19.85 us |    15.50 us |       - |     140 B |
| Compress   | MIO0      | 1  |  23,206.3 us |   441.93 us |   413.38 us |       - |    7132 B |
| Decompress | MIO0      | 1  |   2,394.4 us |     4.15 us |     3.88 us |       - |     165 B |
| Compress   | PRS       | 1  |  33,078.2 us |   642.58 us |   879.57 us |       - |    7055 B |
| Decompress | PRS       | 1  |   2,163.0 us |    23.41 us |    21.90 us |       - |     429 B |
| Compress   | RLE30     | 1  |   3,826.3 us |    66.02 us |    58.53 us |       - |      91 B |
| Decompress | RLE30     | 1  |     670.6 us |     9.38 us |     8.77 us |       - |      65 B |
| Compress   | RefPack   | 1  |  72,131.2 us | 1,433.70 us | 2,992.66 us |       - |    6967 B |
| Decompress | RefPack   | 1  |   1,270.0 us |     2.95 us |     2.46 us |       - |     139 B |
| Compress   | Yay0      | 1  |  23,458.7 us |   396.25 us |   370.66 us |       - |    7142 B |
| Decompress | Yay0      | 1  |   3,366.4 us |    30.52 us |    28.55 us |       - |     573 B |
| Compress   | Yaz0      | 1  |  23,337.6 us |   450.22 us |   442.18 us |       - |    6934 B |
| Decompress | Yaz0      | 1  |   2,734.8 us |    38.16 us |    35.70 us |       - |     309 B |