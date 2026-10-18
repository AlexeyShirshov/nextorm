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
| Nextorm_SteadyWriter_3Col   | 1     |        202.6 ns |        200.3 ns |   0.01 |   0.0534 |        - |        - |     448 B |        0.10 |
| Nextorm_ToList_3Col         | 1     |     17,484.9 ns |     17,485.1 ns |   1.00 |   0.4883 |        - |        - |    4600 B |        1.00 |
| Nextorm_WriteJson_1Col      | 1     |    523,501.3 ns |    523,746.4 ns |  29.94 |   0.9766 |        - |        - |   12624 B |        2.74 |
| Nextorm_WriteJson_ObjectNum | 1     |  1,148,411.2 ns |  1,149,896.7 ns |  65.68 |   3.9063 |   1.9531 |        - |   40228 B |        8.75 |
| Nextorm_WriteJson_ObjectStr | 1     |  1,244,472.9 ns |  1,190,921.2 ns |  71.18 |        - |        - |        - |   14573 B |        3.17 |
| Nextorm_CompileOnly_3Col    | 1     |  1,717,152.7 ns |  1,718,927.8 ns |  98.21 |   3.9063 |        - |        - |   39983 B |        8.69 |
| Nextorm_WriteJson_3Col      | 1     |  1,807,653.3 ns |  1,809,692.8 ns | 103.39 |   3.9063 |        - |        - |   49231 B |       10.70 |
| Nextorm_WriteJson_8Col      | 1     |  2,994,447.6 ns |  2,962,782.0 ns | 171.26 |   7.8125 |   3.9063 |        - |   68969 B |       14.99 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 10    |      1,648.7 ns |      1,650.2 ns |   0.06 |   0.6027 |        - |        - |    5056 B |        0.76 |
| Nextorm_ToList_3Col         | 10    |     25,811.0 ns |     25,767.2 ns |   1.00 |   0.7324 |        - |        - |    6688 B |        1.00 |
| Nextorm_WriteJson_1Col      | 10    |    529,136.6 ns |    526,553.6 ns |  20.50 |   0.9766 |        - |        - |   13375 B |        2.00 |
| Nextorm_WriteJson_ObjectStr | 10    |  1,109,093.2 ns |  1,084,972.0 ns |  42.97 |        - |        - |        - |   15289 B |        2.29 |
| Nextorm_WriteJson_ObjectNum | 10    |  1,166,206.7 ns |  1,154,382.8 ns |  45.18 |   3.9063 |   1.9531 |        - |   40483 B |        6.05 |
| Nextorm_CompileOnly_3Col    | 10    |  1,632,412.2 ns |  1,625,645.3 ns |  63.25 |   3.9063 |   1.9531 |        - |   39691 B |        5.93 |
| Nextorm_WriteJson_3Col      | 10    |  1,900,951.9 ns |  1,873,238.1 ns |  73.65 |   3.9063 |        - |        - |   50966 B |        7.62 |
| Nextorm_WriteJson_8Col      | 10    |  2,992,524.7 ns |  2,931,169.2 ns | 115.94 |   7.8125 |   3.9063 |        - |   74695 B |       11.17 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 100   |     15,043.0 ns |     15,110.3 ns |   0.15 |   2.1820 |   0.0916 |        - |   18328 B |        0.67 |
| Nextorm_ToList_3Col         | 100   |    102,447.9 ns |    102,529.1 ns |   1.00 |   2.9297 |        - |        - |   27553 B |        1.00 |
| Nextorm_WriteJson_1Col      | 100   |    537,980.3 ns |    535,703.6 ns |   5.25 |   1.9531 |   0.9766 |        - |   22027 B |        0.80 |
| Nextorm_WriteJson_ObjectStr | 100   |  1,077,353.6 ns |  1,075,304.9 ns |  10.52 |   1.9531 |        - |        - |   23955 B |        0.87 |
| Nextorm_WriteJson_ObjectNum | 100   |  1,418,204.3 ns |  1,315,723.8 ns |  13.84 |   3.9063 |   1.9531 |        - |   40691 B |        1.48 |
| Nextorm_CompileOnly_3Col    | 100   |  1,627,447.0 ns |  1,634,530.3 ns |  15.89 |   3.9063 |        - |        - |   40695 B |        1.48 |
| Nextorm_WriteJson_3Col      | 100   |  1,858,525.9 ns |  1,862,058.0 ns |  18.14 |   7.8125 |   3.9063 |        - |   66642 B |        2.42 |
| Nextorm_WriteJson_8Col      | 100   |  3,219,833.1 ns |  3,223,495.0 ns |  31.43 |   7.8125 |        - |        - |  129098 B |        4.69 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 1000  |    137,560.3 ns |    136,821.2 ns |   0.15 |  13.4277 |   2.1973 |        - |  114176 B |        0.48 |
| Nextorm_WriteJson_1Col      | 1000  |    867,650.1 ns |    867,073.4 ns |   0.94 |  11.7188 |   3.9063 |        - |  108435 B |        0.46 |
| Nextorm_ToList_3Col         | 1000  |    926,983.7 ns |    931,143.7 ns |   1.00 |  27.3438 |   7.8125 |        - |  236317 B |        1.00 |
| Nextorm_WriteJson_ObjectStr | 1000  |  1,415,403.4 ns |  1,411,432.9 ns |   1.53 |  11.7188 |   3.9063 |        - |  110360 B |        0.47 |
| Nextorm_WriteJson_ObjectNum | 1000  |  1,442,250.4 ns |  1,445,602.2 ns |   1.56 |   3.9063 |        - |        - |   40553 B |        0.17 |
| Nextorm_CompileOnly_3Col    | 1000  |  1,685,558.4 ns |  1,662,040.7 ns |   1.82 |   3.9063 |   1.9531 |        - |   39978 B |        0.17 |
| Nextorm_WriteJson_3Col      | 1000  |  2,968,940.3 ns |  2,964,935.0 ns |   3.20 |  23.4375 |   7.8125 |        - |  224864 B |        0.95 |
| Nextorm_WriteJson_8Col      | 1000  |  6,080,970.7 ns |  6,068,184.7 ns |   6.56 |  78.1250 |  15.6250 |        - |  676816 B |        2.86 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 10000 |  1,435,510.6 ns |  1,435,857.9 ns |   0.16 | 283.2031 | 283.2031 | 283.2031 | 1072348 B |        0.46 |
| Nextorm_CompileOnly_3Col    | 10000 |  1,636,060.9 ns |  1,636,122.2 ns |   0.18 |   3.9063 |   1.9531 |        - |   39979 B |        0.02 |
| Nextorm_WriteJson_1Col      | 10000 |  4,172,249.4 ns |  4,190,548.3 ns |   0.47 | 109.3750 |        - |        - |  972470 B |        0.42 |
| Nextorm_WriteJson_ObjectNum | 10000 |  4,308,396.8 ns |  4,307,936.4 ns |   0.49 |        - |        - |        - |   40757 B |        0.02 |
| Nextorm_WriteJson_ObjectStr | 10000 |  4,956,021.5 ns |  4,930,217.9 ns |   0.56 | 109.3750 |        - |        - |  974590 B |        0.42 |
| Nextorm_ToList_3Col         | 10000 |  8,852,138.4 ns |  8,883,224.0 ns |   1.00 | 265.6250 | 250.0000 |        - | 2323648 B |        1.00 |
| Nextorm_WriteJson_3Col      | 10000 | 13,890,515.3 ns | 13,883,154.3 ns |   1.57 | 203.1250 |  15.6250 |        - | 1809322 B |        0.78 |
| Nextorm_WriteJson_8Col      | 10000 | 35,533,393.5 ns | 35,477,496.6 ns |   4.01 | 714.2857 |        - |        - | 6148095 B |        2.65 |
