```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  Categories=json-fixed  

```
| Method                      | Rows  | Mean            | Median          | Ratio   | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|---------------------------- |------ |----------------:|----------------:|--------:|---------:|---------:|---------:|----------:|------------:|
| Nextorm_SteadyWriter_3Col   | 1     |        213.3 ns |        213.0 ns |   0.010 |   0.0534 |        - |        - |     448 B |        0.10 |
| Nextorm_ToList_3Col         | 1     |     22,314.9 ns |     22,301.3 ns |   1.023 |   0.4883 |        - |        - |    4600 B |        1.00 |
| Nextorm_WriteJson_1Col      | 1     |    611,261.1 ns |    605,559.7 ns |  28.021 |   0.9766 |        - |        - |   12520 B |        2.72 |
| Nextorm_WriteJson_ObjectNum | 1     |  1,116,651.8 ns |  1,113,831.1 ns |  51.189 |   3.9063 |   1.9531 |        - |   40487 B |        8.80 |
| Nextorm_WriteJson_ObjectStr | 1     |  1,264,991.7 ns |  1,253,465.0 ns |  57.989 |        - |        - |        - |   14625 B |        3.18 |
| Nextorm_CompileOnly_3Col    | 1     |  1,790,092.7 ns |  1,760,227.1 ns |  82.061 |   3.9063 |        - |        - |   40415 B |        8.79 |
| Nextorm_WriteJson_3Col      | 1     |  2,492,759.7 ns |  2,414,971.4 ns | 114.273 |   3.9063 |        - |        - |   49662 B |       10.80 |
| Nextorm_WriteJson_8Col      | 1     |  3,034,373.0 ns |  3,022,291.3 ns | 139.101 |   7.8125 |   3.9063 |        - |   69645 B |       15.14 |
|                             |       |                 |                 |         |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 10    |      1,672.0 ns |      1,660.4 ns |    0.06 |   0.6027 |        - |        - |    5056 B |        0.76 |
| Nextorm_ToList_3Col         | 10    |     25,899.5 ns |     25,842.9 ns |    1.00 |   0.7324 |        - |        - |    6688 B |        1.00 |
| Nextorm_WriteJson_1Col      | 10    |    537,802.4 ns |    535,310.2 ns |   20.77 |   0.9766 |        - |        - |   13375 B |        2.00 |
| Nextorm_WriteJson_ObjectStr | 10    |  1,039,144.9 ns |  1,019,315.6 ns |   40.13 |        - |        - |        - |   15549 B |        2.32 |
| Nextorm_WriteJson_ObjectNum | 10    |  1,104,068.9 ns |  1,099,712.9 ns |   42.64 |   3.9063 |   1.9531 |        - |   40483 B |        6.05 |
| Nextorm_CompileOnly_3Col    | 10    |  1,571,874.8 ns |  1,569,705.2 ns |   60.70 |   3.9063 |   1.9531 |        - |   40842 B |        6.11 |
| Nextorm_WriteJson_3Col      | 10    |  1,852,368.9 ns |  1,850,602.9 ns |   71.54 |   3.9063 |        - |        - |   51998 B |        7.77 |
| Nextorm_WriteJson_8Col      | 10    |  2,766,291.5 ns |  2,769,972.1 ns |  106.83 |   7.8125 |   3.9063 |        - |   74815 B |       11.19 |
|                             |       |                 |                 |         |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 100   |     15,896.2 ns |     15,483.2 ns |    0.16 |   2.1820 |   0.0916 |        - |   18328 B |        0.67 |
| Nextorm_ToList_3Col         | 100   |    102,367.8 ns |    102,070.1 ns |    1.00 |   2.9297 |        - |        - |   27553 B |        1.00 |
| Nextorm_WriteJson_1Col      | 100   |    592,392.4 ns |    575,100.9 ns |    5.79 |   1.9531 |   0.9766 |        - |   22323 B |        0.81 |
| Nextorm_WriteJson_ObjectStr | 100   |  1,110,453.5 ns |  1,096,693.8 ns |   10.85 |   1.9531 |        - |        - |   24115 B |        0.88 |
| Nextorm_WriteJson_ObjectNum | 100   |  1,113,509.4 ns |  1,109,836.1 ns |   10.88 |   3.9063 |   1.9531 |        - |   40116 B |        1.46 |
| Nextorm_CompileOnly_3Col    | 100   |  1,597,049.4 ns |  1,596,101.9 ns |   15.60 |   3.9063 |   1.9531 |        - |   39914 B |        1.45 |
| Nextorm_WriteJson_3Col      | 100   |  2,049,558.4 ns |  2,005,229.9 ns |   20.02 |   7.8125 |   3.9063 |        - |   66522 B |        2.41 |
| Nextorm_WriteJson_8Col      | 100   |  3,031,339.4 ns |  3,024,838.1 ns |   29.62 |   7.8125 |        - |        - |  129106 B |        4.69 |
|                             |       |                 |                 |         |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 1000  |    161,886.9 ns |    159,936.9 ns |    0.17 |  13.4277 |   2.1973 |        - |  114176 B |        0.48 |
| Nextorm_WriteJson_1Col      | 1000  |    902,521.9 ns |    891,240.6 ns |    0.97 |  11.7188 |   3.9063 |        - |  108587 B |        0.46 |
| Nextorm_ToList_3Col         | 1000  |    937,116.0 ns |    943,442.7 ns |    1.00 |  27.3438 |   9.7656 |        - |  236445 B |        1.00 |
| Nextorm_WriteJson_ObjectStr | 1000  |  1,787,760.2 ns |  1,735,571.6 ns |    1.91 |  11.7188 |   3.9063 |        - |  110368 B |        0.47 |
| Nextorm_WriteJson_ObjectNum | 1000  |  1,841,113.0 ns |  1,784,791.8 ns |    1.97 |   3.9063 |        - |        - |   40673 B |        0.17 |
| Nextorm_CompileOnly_3Col    | 1000  |  2,237,396.6 ns |  2,225,512.6 ns |    2.39 |   3.9063 |        - |        - |   39696 B |        0.17 |
| Nextorm_WriteJson_3Col      | 1000  |  3,175,130.8 ns |  3,129,218.1 ns |    3.40 |  23.4375 |   7.8125 |        - |  225640 B |        0.95 |
| Nextorm_WriteJson_8Col      | 1000  |  7,898,392.2 ns |  7,697,906.5 ns |    8.45 |  78.1250 |  15.6250 |        - |  676752 B |        2.86 |
|                             |       |                 |                 |         |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 10000 |  1,643,547.0 ns |  1,630,435.0 ns |    0.15 | 265.6250 | 257.8125 | 257.8125 | 1072349 B |        0.46 |
| Nextorm_CompileOnly_3Col    | 10000 |  2,348,884.1 ns |  2,352,417.3 ns |    0.21 |   3.9063 |        - |        - |   39839 B |        0.02 |
| Nextorm_WriteJson_1Col      | 10000 |  5,699,604.9 ns |  5,653,036.6 ns |    0.52 | 109.3750 |        - |        - |  972473 B |        0.42 |
| Nextorm_WriteJson_ObjectNum | 10000 |  6,183,166.1 ns |  6,088,974.5 ns |    0.56 |        - |        - |        - |   40437 B |        0.02 |
| Nextorm_WriteJson_ObjectStr | 10000 |  6,913,241.5 ns |  6,890,950.9 ns |    0.63 | 109.3750 |        - |        - |  974581 B |        0.42 |
| Nextorm_ToList_3Col         | 10000 | 11,079,648.2 ns | 11,004,314.8 ns |    1.01 | 265.6250 | 250.0000 |        - | 2323647 B |        1.00 |
| Nextorm_WriteJson_3Col      | 10000 | 19,318,113.0 ns | 18,989,082.0 ns |    1.75 | 187.5000 |        - |        - | 1809342 B |        0.78 |
| Nextorm_WriteJson_8Col      | 10000 | 55,243,985.2 ns | 56,601,708.2 ns |    5.01 | 666.6667 |        - |        - | 6147733 B |        2.65 |
