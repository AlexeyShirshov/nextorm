```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  Categories=csv-fixed  

```
| Method                | Rows  | Mean         | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|---------------------- |------ |-------------:|------:|---------:|---------:|-----------:|------------:|
| Nextorm_ToList_3Col   | 1     |     17.00 μs |  1.00 |   0.4883 |        - |    4.49 KB |        1.00 |
| Nextorm_WriteCsv_1Col | 1     |    197.95 μs | 11.67 |   1.2207 |   0.9766 |   11.67 KB |        2.60 |
| Nextorm_WriteCsv_3Col | 1     |    571.14 μs | 33.68 |   2.9297 |   1.9531 |   28.08 KB |        6.25 |
| Nextorm_WriteCsv_8Col | 1     |  1,620.96 μs | 95.60 |   7.8125 |   5.8594 |   73.42 KB |       16.34 |
|                       |       |              |       |          |          |            |             |
| Nextorm_ToList_3Col   | 10    |     24.09 μs |  1.00 |   0.7935 |        - |    6.53 KB |        1.00 |
| Nextorm_WriteCsv_1Col | 10    |    204.22 μs |  8.48 |   1.4648 |   1.2207 |   12.52 KB |        1.92 |
| Nextorm_WriteCsv_3Col | 10    |    587.99 μs | 24.41 |   2.9297 |   1.9531 |   29.65 KB |        4.54 |
| Nextorm_WriteCsv_8Col | 10    |  1,618.20 μs | 67.19 |   7.8125 |   5.8594 |   78.81 KB |       12.07 |
|                       |       |              |       |          |          |            |             |
| Nextorm_ToList_3Col   | 100   |     97.54 μs |  1.00 |   3.1738 |   0.1221 |   26.91 KB |        1.00 |
| Nextorm_WriteCsv_1Col | 100   |    235.42 μs |  2.41 |   2.4414 |   1.2207 |   20.97 KB |        0.78 |
| Nextorm_WriteCsv_3Col | 100   |    697.86 μs |  7.15 |   4.8828 |   3.9063 |   45.12 KB |        1.68 |
| Nextorm_WriteCsv_8Col | 100   |  1,924.30 μs | 19.73 |  15.6250 |  13.6719 |  132.21 KB |        4.91 |
|                       |       |              |       |          |          |            |             |
| Nextorm_WriteCsv_1Col | 1000  |    544.80 μs |  0.66 |  12.6953 |   2.9297 |  105.35 KB |        0.46 |
| Nextorm_ToList_3Col   | 1000  |    829.38 μs |  1.00 |  27.3438 |   6.8359 |  230.78 KB |        1.00 |
| Nextorm_WriteCsv_3Col | 1000  |  1,637.11 μs |  1.97 |  23.4375 |   7.8125 |  199.78 KB |        0.87 |
| Nextorm_WriteCsv_8Col | 1000  |  4,815.62 μs |  5.81 |  78.1250 |  23.4375 |   666.5 KB |        2.89 |
|                       |       |              |       |          |          |            |             |
| Nextorm_WriteCsv_1Col | 10000 |  3,678.40 μs |  0.43 | 113.2813 |   3.9063 |  949.16 KB |        0.42 |
| Nextorm_ToList_3Col   | 10000 |  8,526.10 μs |  1.00 | 265.6250 | 203.1250 | 2269.26 KB |        1.00 |
| Nextorm_WriteCsv_3Col | 10000 | 10,906.99 μs |  1.28 | 203.1250 |  15.6250 | 1746.12 KB |        0.77 |
| Nextorm_WriteCsv_8Col | 10000 | 31,962.75 μs |  3.75 | 687.5000 |        - | 6007.91 KB |        2.65 |
