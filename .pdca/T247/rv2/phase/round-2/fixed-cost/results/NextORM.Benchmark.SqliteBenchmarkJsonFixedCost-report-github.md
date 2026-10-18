```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=DefaultJob  Categories=json-fixed  

```
| Method                      | Rows  | Mean            | Median          | Ratio  | Gen0     | Gen1     | Gen2     | Allocated | Alloc Ratio |
|---------------------------- |------ |----------------:|----------------:|-------:|---------:|---------:|---------:|----------:|------------:|
| Nextorm_SteadyWriter_3Col   | 1     |        235.4 ns |        233.4 ns |   0.01 |   0.0534 |        - |        - |     448 B |        0.10 |
| Nextorm_ToList_3Col         | 1     |     18,429.5 ns |     18,366.6 ns |   1.00 |   0.4883 |        - |        - |    4600 B |        1.00 |
| Nextorm_WriteJson_1Col      | 1     |    533,646.8 ns |    536,191.4 ns |  28.98 |   0.9766 |        - |        - |   12512 B |        2.72 |
| Nextorm_WriteJson_ObjectStr | 1     |  1,010,509.5 ns |  1,008,827.4 ns |  54.87 |        - |        - |        - |   14425 B |        3.14 |
| Nextorm_WriteJson_ObjectNum | 1     |  1,341,510.8 ns |  1,336,410.8 ns |  72.84 |   3.9063 |   1.9531 |        - |   40159 B |        8.73 |
| Nextorm_WriteJson_3Col      | 1     |  1,934,792.4 ns |  1,932,614.3 ns | 105.05 |   3.9063 |        - |        - |   49630 B |       10.79 |
| Nextorm_CompileOnly_3Col    | 1     |  2,034,228.6 ns |  2,007,286.4 ns | 110.45 |   3.9063 |        - |        - |   39855 B |        8.66 |
| Nextorm_WriteJson_8Col      | 1     |  3,410,092.8 ns |  3,405,302.5 ns | 185.16 |   7.8125 |   3.9063 |        - |   70232 B |       15.27 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 10    |      2,057.7 ns |      2,069.9 ns |   0.07 |   0.6027 |        - |        - |    5056 B |        0.76 |
| Nextorm_ToList_3Col         | 10    |     29,358.9 ns |     28,829.8 ns |   1.00 |   0.7324 |        - |        - |    6688 B |        1.00 |
| Nextorm_WriteJson_1Col      | 10    |    598,655.7 ns |    593,238.7 ns |  20.49 |   0.9766 |        - |        - |   13383 B |        2.00 |
| Nextorm_WriteJson_ObjectNum | 10    |  1,219,005.6 ns |  1,212,360.2 ns |  41.71 |   3.9063 |   1.9531 |        - |   40699 B |        6.09 |
| Nextorm_WriteJson_ObjectStr | 10    |  1,235,234.1 ns |  1,208,199.0 ns |  42.27 |        - |        - |        - |   15289 B |        2.29 |
| Nextorm_CompileOnly_3Col    | 10    |  1,934,269.1 ns |  1,922,414.6 ns |  66.19 |   3.9063 |        - |        - |   40207 B |        6.01 |
| Nextorm_WriteJson_3Col      | 10    |  2,060,713.2 ns |  2,032,911.3 ns |  70.52 |   3.9063 |        - |        - |   51326 B |        7.67 |
| Nextorm_WriteJson_8Col      | 10    |  3,475,412.4 ns |  3,404,305.8 ns | 118.93 |   7.8125 |        - |        - |   74948 B |       11.21 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 100   |     15,308.6 ns |     15,283.8 ns |   0.13 |   2.1667 |   0.0916 |        - |   18328 B |        0.67 |
| Nextorm_ToList_3Col         | 100   |    122,659.9 ns |    118,600.7 ns |   1.01 |   2.9297 |        - |        - |   27553 B |        1.00 |
| Nextorm_WriteJson_1Col      | 100   |    568,074.1 ns |    564,386.6 ns |   4.67 |   1.9531 |   0.9766 |        - |   22139 B |        0.80 |
| Nextorm_WriteJson_ObjectNum | 100   |  1,238,220.8 ns |  1,216,001.6 ns |  10.18 |   3.9063 |        - |        - |   40703 B |        1.48 |
| Nextorm_WriteJson_ObjectStr | 100   |  1,250,460.4 ns |  1,232,117.1 ns |  10.28 |   1.9531 |        - |        - |   23955 B |        0.87 |
| Nextorm_CompileOnly_3Col    | 100   |  1,760,643.8 ns |  1,732,127.9 ns |  14.48 |   3.9063 |        - |        - |   40615 B |        1.47 |
| Nextorm_WriteJson_3Col      | 100   |  2,246,443.1 ns |  2,240,980.1 ns |  18.47 |   7.8125 |   3.9063 |        - |   67257 B |        2.44 |
| Nextorm_WriteJson_8Col      | 100   |  3,478,547.6 ns |  3,471,751.1 ns |  28.60 |   7.8125 |        - |        - |  129931 B |        4.72 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 1000  |    148,781.6 ns |    147,225.1 ns |   0.16 |  13.4277 |   2.1973 |        - |  114176 B |        0.48 |
| Nextorm_ToList_3Col         | 1000  |    915,547.0 ns |    918,277.7 ns |   1.00 |  27.3438 |   7.8125 |        - |  236317 B |        1.00 |
| Nextorm_WriteJson_1Col      | 1000  |    921,159.8 ns |    913,717.3 ns |   1.01 |  11.7188 |   3.9063 |        - |  108435 B |        0.46 |
| Nextorm_WriteJson_ObjectNum | 1000  |  1,501,764.8 ns |  1,506,132.9 ns |   1.64 |   3.9063 |        - |        - |   40121 B |        0.17 |
| Nextorm_WriteJson_ObjectStr | 1000  |  1,522,990.5 ns |  1,523,232.0 ns |   1.66 |  11.7188 |   3.9063 |        - |  110360 B |        0.47 |
| Nextorm_CompileOnly_3Col    | 1000  |  1,979,962.4 ns |  1,947,774.3 ns |   2.16 |   3.9063 |        - |        - |   40359 B |        0.17 |
| Nextorm_WriteJson_3Col      | 1000  |  3,218,603.2 ns |  3,195,106.5 ns |   3.52 |  23.4375 |   7.8125 |        - |  225656 B |        0.95 |
| Nextorm_WriteJson_8Col      | 1000  |  6,611,506.7 ns |  6,567,411.7 ns |   7.22 |  78.1250 |  15.6250 |        - |  677048 B |        2.86 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 10000 |  1,575,930.4 ns |  1,563,815.3 ns |   0.15 | 277.3438 | 277.3438 | 277.3438 | 1072345 B |        0.46 |
| Nextorm_CompileOnly_3Col    | 10000 |  1,748,063.2 ns |  1,751,292.3 ns |   0.17 |   3.9063 |        - |        - |   40647 B |        0.02 |
| Nextorm_WriteJson_1Col      | 10000 |  4,301,983.1 ns |  4,248,728.1 ns |   0.41 | 109.3750 |        - |        - |  972473 B |        0.42 |
| Nextorm_WriteJson_ObjectNum | 10000 |  4,554,754.7 ns |  4,537,856.2 ns |   0.43 |        - |        - |        - |   40277 B |        0.02 |
| Nextorm_WriteJson_ObjectStr | 10000 |  5,317,577.0 ns |  5,310,447.8 ns |   0.51 | 109.3750 |        - |        - |  974438 B |        0.42 |
| Nextorm_ToList_3Col         | 10000 | 10,549,347.3 ns | 10,476,419.4 ns |   1.00 | 265.6250 | 250.0000 |        - | 2323647 B |        1.00 |
| Nextorm_WriteJson_3Col      | 10000 | 14,383,433.5 ns | 14,338,063.5 ns |   1.37 | 203.1250 |  15.6250 |        - | 1808914 B |        0.78 |
| Nextorm_WriteJson_8Col      | 10000 | 37,893,495.7 ns | 37,515,160.5 ns |   3.60 | 714.2857 |        - |        - | 6147951 B |        2.65 |
