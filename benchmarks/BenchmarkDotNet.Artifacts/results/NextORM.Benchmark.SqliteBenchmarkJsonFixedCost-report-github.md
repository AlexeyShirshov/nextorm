```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  Categories=json-fixed  

```
| Method                      | Rows  | Mean         | Ratio  | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|---------------------------- |------ |-------------:|-------:|---------:|---------:|-----------:|------------:|
| Nextorm_ToList_3Col         | 1     |     19.35 μs |   1.03 |   0.4883 |        - |    4.49 KB |        1.00 |
| Nextorm_WriteJson_1Col      | 1     |    483.08 μs |  25.82 |   0.9766 |        - |   12.22 KB |        2.72 |
| Nextorm_WriteJson_ObjectStr | 1     |    966.99 μs |  51.69 |   0.9766 |        - |    14.1 KB |        3.14 |
| Nextorm_WriteJson_ObjectNum | 1     |  1,090.66 μs |  58.30 |   3.9063 |   1.9531 |   39.53 KB |        8.80 |
| Nextorm_WriteJson_3Col      | 1     |  1,716.87 μs |  91.78 |   3.9063 |        - |   48.48 KB |       10.79 |
| Nextorm_WriteJson_8Col      | 1     |  2,875.35 μs | 153.71 |   7.8125 |   3.9063 |    67.9 KB |       15.12 |
|                             |       |              |        |          |          |            |             |
| Nextorm_ToList_3Col         | 10    |     23.37 μs |   1.00 |   0.7935 |        - |    6.53 KB |        1.00 |
| Nextorm_WriteJson_1Col      | 10    |    495.09 μs |  21.18 |   1.4648 |   0.9766 |   13.07 KB |        2.00 |
| Nextorm_WriteJson_ObjectStr | 10    |    995.92 μs |  42.61 |        - |        - |   14.94 KB |        2.29 |
| Nextorm_WriteJson_ObjectNum | 10    |  1,039.72 μs |  44.49 |   3.9063 |   1.9531 |   39.53 KB |        6.05 |
| Nextorm_WriteJson_3Col      | 10    |  1,744.61 μs |  74.65 |   5.8594 |   3.9063 |   50.03 KB |        7.66 |
| Nextorm_WriteJson_8Col      | 10    |  2,878.13 μs | 123.14 |   7.8125 |   3.9063 |   73.25 KB |       11.22 |
|                             |       |              |        |          |          |            |             |
| Nextorm_ToList_3Col         | 100   |     97.30 μs |   1.00 |   3.1738 |   0.1221 |   26.91 KB |        1.00 |
| Nextorm_WriteJson_1Col      | 100   |    511.73 μs |   5.26 |   1.9531 |   0.9766 |   21.51 KB |        0.80 |
| Nextorm_WriteJson_ObjectStr | 100   |  1,018.95 μs |  10.47 |   1.9531 |        - |    23.4 KB |        0.87 |
| Nextorm_WriteJson_ObjectNum | 100   |  1,100.03 μs |  11.31 |   3.9063 |   1.9531 |   39.53 KB |        1.47 |
| Nextorm_WriteJson_3Col      | 100   |  1,806.90 μs |  18.57 |   7.8125 |   5.8594 |    65.5 KB |        2.43 |
| Nextorm_WriteJson_8Col      | 100   |  3,091.60 μs |  31.77 |  11.7188 |   7.8125 |  126.63 KB |        4.71 |
|                             |       |              |        |          |          |            |             |
| Nextorm_WriteJson_1Col      | 1000  |    804.44 μs |   0.97 |  12.6953 |   3.9063 |   105.9 KB |        0.46 |
| Nextorm_ToList_3Col         | 1000  |    830.79 μs |   1.00 |  27.3438 |   6.8359 |  230.78 KB |        1.00 |
| Nextorm_WriteJson_ObjectStr | 1000  |  1,308.68 μs |   1.58 |  11.7188 |   3.9063 |   107.8 KB |        0.47 |
| Nextorm_WriteJson_ObjectNum | 1000  |  1,330.79 μs |   1.60 |   3.9063 |   1.9531 |   39.54 KB |        0.17 |
| Nextorm_WriteJson_3Col      | 1000  |  2,754.57 μs |   3.32 |  23.4375 |   7.8125 |   220.2 KB |        0.95 |
| Nextorm_WriteJson_8Col      | 1000  |  6,047.45 μs |   7.28 |  78.1250 |  15.6250 |  661.09 KB |        2.86 |
|                             |       |              |        |          |          |            |             |
| Nextorm_WriteJson_1Col      | 10000 |  3,913.77 μs |   0.46 | 109.3750 |        - |  949.73 KB |        0.42 |
| Nextorm_WriteJson_ObjectNum | 10000 |  4,060.16 μs |   0.48 |        - |        - |   39.56 KB |        0.02 |
| Nextorm_WriteJson_ObjectStr | 10000 |  4,915.49 μs |   0.58 | 109.3750 |        - |  951.65 KB |        0.42 |
| Nextorm_ToList_3Col         | 10000 |  8,444.08 μs |   1.00 | 265.6250 | 203.1250 | 2269.25 KB |        1.00 |
| Nextorm_WriteJson_3Col      | 10000 | 12,959.94 μs |   1.53 | 203.1250 |  15.6250 |    1767 KB |        0.78 |
| Nextorm_WriteJson_8Col      | 10000 | 33,498.27 μs |   3.97 | 733.3333 |        - | 6004.15 KB |        2.65 |
