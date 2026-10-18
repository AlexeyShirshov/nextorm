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
| Nextorm_SteadyWriter_3Col   | 1     |        224.5 ns |        224.5 ns |   0.01 |   0.0534 |        - |        - |     448 B |        0.10 |
| Nextorm_ToList_3Col         | 1     |     18,323.6 ns |     18,160.1 ns |   1.00 |   0.4883 |        - |        - |    4600 B |        1.00 |
| Nextorm_WriteJson_1Col      | 1     |    530,567.3 ns |    524,324.9 ns |  29.02 |   0.9766 |        - |        - |   12512 B |        2.72 |
| Nextorm_WriteJson_ObjectStr | 1     |  1,120,253.5 ns |  1,112,287.2 ns |  61.27 |        - |        - |        - |   14797 B |        3.22 |
| Nextorm_WriteJson_ObjectNum | 1     |  1,222,101.9 ns |  1,210,365.1 ns |  66.84 |   3.9063 |   1.9531 |        - |   40723 B |        8.85 |
| Nextorm_CompileOnly_3Col    | 1     |  1,746,852.6 ns |  1,751,151.3 ns |  95.54 |   3.9063 |        - |        - |   40351 B |        8.77 |
| Nextorm_WriteJson_3Col      | 1     |  1,907,558.5 ns |  1,887,755.8 ns | 104.33 |   3.9063 |        - |        - |   48799 B |       10.61 |
| Nextorm_WriteJson_8Col      | 1     |  3,129,564.8 ns |  3,135,640.0 ns | 171.16 |   7.8125 |   3.9063 |        - |   69073 B |       15.02 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 10    |      2,412.8 ns |      2,418.2 ns |   0.09 |   0.6027 |        - |        - |    5056 B |        0.76 |
| Nextorm_ToList_3Col         | 10    |     27,406.6 ns |     27,262.0 ns |   1.00 |   0.7324 |        - |        - |    6688 B |        1.00 |
| Nextorm_WriteJson_1Col      | 10    |    566,818.5 ns |    558,034.6 ns |  20.70 |   0.9766 |        - |        - |   13375 B |        2.00 |
| Nextorm_WriteJson_ObjectStr | 10    |  1,103,541.2 ns |  1,100,986.1 ns |  40.31 |        - |        - |        - |   15401 B |        2.30 |
| Nextorm_WriteJson_ObjectNum | 10    |  1,260,463.1 ns |  1,248,776.0 ns |  46.04 |   3.9063 |   1.9531 |        - |   40555 B |        6.06 |
| Nextorm_WriteJson_3Col      | 10    |  2,072,070.8 ns |  2,030,523.7 ns |  75.68 |   3.9063 |        - |        - |   51886 B |        7.76 |
| Nextorm_CompileOnly_3Col    | 10    |  2,369,001.0 ns |  2,385,511.3 ns |  86.52 |   3.9063 |        - |        - |   40423 B |        6.04 |
| Nextorm_WriteJson_8Col      | 10    |  3,467,915.7 ns |  3,410,448.1 ns | 126.66 |   7.8125 |        - |        - |   74322 B |       11.11 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 100   |     17,548.0 ns |     17,203.6 ns |   0.12 |   2.1667 |   0.0916 |        - |   18328 B |        0.67 |
| Nextorm_ToList_3Col         | 100   |    144,415.6 ns |    142,656.1 ns |   1.02 |   2.9297 |        - |        - |   27553 B |        1.00 |
| Nextorm_WriteJson_1Col      | 100   |    668,867.2 ns |    656,143.0 ns |   4.74 |   1.9531 |        - |        - |   22027 B |        0.80 |
| Nextorm_WriteJson_ObjectStr | 100   |  1,374,321.1 ns |  1,367,951.7 ns |   9.74 |   1.9531 |        - |        - |   24075 B |        0.87 |
| Nextorm_WriteJson_ObjectNum | 100   |  1,493,748.3 ns |  1,481,052.9 ns |  10.58 |   3.9063 |        - |        - |   40129 B |        1.46 |
| Nextorm_CompileOnly_3Col    | 100   |  2,029,298.2 ns |  2,014,930.9 ns |  14.38 |   3.9063 |        - |        - |   39696 B |        1.44 |
| Nextorm_WriteJson_3Col      | 100   |  2,690,606.2 ns |  2,673,989.5 ns |  19.06 |   7.8125 |   3.9063 |        - |   66889 B |        2.43 |
| Nextorm_WriteJson_8Col      | 100   |  3,842,322.6 ns |  3,808,180.0 ns |  27.23 |   7.8125 |        - |        - |  129442 B |        4.70 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 1000  |    143,099.3 ns |    139,784.1 ns |   0.14 |  13.4277 |   2.1973 |        - |  114176 B |        0.48 |
| Nextorm_WriteJson_1Col      | 1000  |    981,368.4 ns |    962,033.3 ns |   0.93 |  11.7188 |   3.9063 |        - |  108435 B |        0.46 |
| Nextorm_ToList_3Col         | 1000  |  1,067,211.6 ns |  1,050,078.4 ns |   1.01 |  27.3438 |   7.8125 |        - |  236317 B |        1.00 |
| Nextorm_WriteJson_ObjectNum | 1000  |  1,639,798.2 ns |  1,625,493.9 ns |   1.55 |   3.9063 |        - |        - |   40017 B |        0.17 |
| Nextorm_WriteJson_ObjectStr | 1000  |  1,653,876.0 ns |  1,651,656.6 ns |   1.56 |  11.7188 |   3.9063 |        - |  110368 B |        0.47 |
| Nextorm_CompileOnly_3Col    | 1000  |  1,688,834.1 ns |  1,690,380.9 ns |   1.59 |   3.9063 |   1.9531 |        - |   40066 B |        0.17 |
| Nextorm_WriteJson_3Col      | 1000  |  3,411,493.9 ns |  3,365,818.4 ns |   3.22 |  23.4375 |   7.8125 |        - |  225352 B |        0.95 |
| Nextorm_WriteJson_8Col      | 1000  |  6,462,191.2 ns |  6,396,664.3 ns |   6.10 |  62.5000 |        - |        - |  676522 B |        2.86 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 10000 |  1,790,046.5 ns |  1,710,440.8 ns |   0.18 | 250.0000 | 240.2344 | 240.2344 | 1072438 B |        0.46 |
| Nextorm_CompileOnly_3Col    | 10000 |  2,009,797.5 ns |  1,846,061.3 ns |   0.20 |   3.9063 |        - |        - |   39847 B |        0.02 |
| Nextorm_WriteJson_1Col      | 10000 |  4,568,599.2 ns |  4,557,534.1 ns |   0.46 | 109.3750 |        - |        - |  972478 B |        0.42 |
| Nextorm_WriteJson_ObjectNum | 10000 |  5,839,150.7 ns |  5,050,205.4 ns |   0.59 |        - |        - |        - |   40443 B |        0.02 |
| Nextorm_WriteJson_ObjectStr | 10000 |  5,975,127.7 ns |  5,910,656.2 ns |   0.60 | 109.3750 |        - |        - |  974670 B |        0.42 |
| Nextorm_ToList_3Col         | 10000 |  9,993,753.2 ns | 10,043,243.7 ns |   1.01 | 265.6250 | 250.0000 |        - | 2323647 B |        1.00 |
| Nextorm_WriteJson_3Col      | 10000 | 14,472,744.8 ns | 14,401,782.7 ns |   1.46 | 203.1250 |  15.6250 |        - | 1809058 B |        0.78 |
| Nextorm_WriteJson_8Col      | 10000 | 43,818,539.1 ns | 40,758,132.1 ns |   4.41 | 500.0000 |        - |        - | 6150334 B |        2.65 |
