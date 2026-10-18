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
| Nextorm_SteadyWriter_3Col   | 1     |        479.0 ns |        514.8 ns |   0.02 |   0.0534 |        - |        - |     448 B |        0.10 |
| Nextorm_ToList_3Col         | 1     |     29,639.7 ns |     28,790.6 ns |   1.02 |   0.4883 |        - |        - |    4600 B |        1.00 |
| Nextorm_WriteJson_1Col      | 1     |    773,651.0 ns |    764,288.5 ns |  26.58 |        - |        - |        - |   12497 B |        2.72 |
| Nextorm_WriteJson_ObjectStr | 1     |  1,706,621.8 ns |  1,607,600.5 ns |  58.62 |        - |        - |        - |   14425 B |        3.14 |
| Nextorm_WriteJson_ObjectNum | 1     |  1,849,709.5 ns |  1,810,591.1 ns |  63.54 |   3.9063 |        - |        - |   40009 B |        8.70 |
| Nextorm_CompileOnly_3Col    | 1     |  2,368,274.4 ns |  2,366,839.5 ns |  81.35 |   3.9063 |        - |        - |   40207 B |        8.74 |
| Nextorm_WriteJson_8Col      | 1     |  4,165,433.8 ns |  4,196,772.7 ns | 143.09 |   7.8125 |        - |        - |   69180 B |       15.04 |
| Nextorm_WriteJson_3Col      | 1     |  4,866,985.1 ns |  4,695,621.5 ns | 167.19 |        - |        - |        - |   49215 B |       10.70 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 10    |      1,818.0 ns |      1,802.4 ns |   0.02 |   0.6027 |        - |        - |    5056 B |        0.76 |
| Nextorm_ToList_3Col         | 10    |     78,835.5 ns |     80,050.2 ns |   1.04 |   0.4883 |        - |        - |    6688 B |        1.00 |
| Nextorm_WriteJson_1Col      | 10    |    544,801.8 ns |    535,809.1 ns |   7.19 |        - |        - |        - |   13361 B |        2.00 |
| Nextorm_WriteJson_ObjectNum | 10    |  1,106,031.7 ns |  1,083,438.2 ns |  14.60 |   3.9063 |   1.9531 |        - |   40547 B |        6.06 |
| Nextorm_WriteJson_ObjectStr | 10    |  1,156,623.8 ns |  1,102,422.7 ns |  15.27 |        - |        - |        - |   15289 B |        2.29 |
| Nextorm_CompileOnly_3Col    | 10    |  1,689,296.6 ns |  1,667,211.3 ns |  22.31 |   3.9063 |   1.9531 |        - |   39835 B |        5.96 |
| Nextorm_WriteJson_8Col      | 10    |  2,924,478.9 ns |  2,869,483.3 ns |  38.62 |   7.8125 |   3.9063 |        - |   75391 B |       11.27 |
| Nextorm_WriteJson_3Col      | 10    |  4,919,337.7 ns |  5,260,489.0 ns |  64.96 |        - |        - |        - |   50983 B |        7.62 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 100   |     17,117.4 ns |     17,079.5 ns |   0.13 |   2.1667 |   0.0916 |        - |   18328 B |        0.67 |
| Nextorm_ToList_3Col         | 100   |    140,669.8 ns |    130,346.9 ns |   1.05 |   3.1738 |   0.1221 |        - |   27553 B |        1.00 |
| Nextorm_WriteJson_1Col      | 100   |    707,925.9 ns |    712,869.6 ns |   5.27 |   1.9531 |   0.9766 |        - |   22155 B |        0.80 |
| Nextorm_WriteJson_ObjectStr | 100   |  1,113,308.3 ns |  1,112,207.1 ns |   8.29 |   1.9531 |        - |        - |   23955 B |        0.87 |
| Nextorm_WriteJson_ObjectNum | 100   |  1,156,135.9 ns |  1,141,887.2 ns |   8.61 |   3.9063 |   1.9531 |        - |   40004 B |        1.45 |
| Nextorm_CompileOnly_3Col    | 100   |  1,992,248.4 ns |  1,971,428.4 ns |  14.83 |   3.9063 |        - |        - |   40071 B |        1.45 |
| Nextorm_WriteJson_3Col      | 100   |  2,452,946.8 ns |  2,292,452.4 ns |  18.27 |   7.8125 |        - |        - |   66613 B |        2.42 |
| Nextorm_WriteJson_8Col      | 100   |  3,283,554.8 ns |  3,224,026.3 ns |  24.45 |   7.8125 |        - |        - |  129218 B |        4.69 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 1000  |    141,952.6 ns |    139,357.1 ns |   0.13 |  13.4277 |   2.1973 |        - |  114176 B |        0.48 |
| Nextorm_ToList_3Col         | 1000  |  1,089,965.7 ns |  1,051,109.4 ns |   1.01 |  27.3438 |   7.8125 |        - |  236317 B |        1.00 |
| Nextorm_WriteJson_1Col      | 1000  |  1,167,847.1 ns |  1,150,671.3 ns |   1.09 |  11.7188 |   3.9063 |        - |  108435 B |        0.46 |
| Nextorm_WriteJson_ObjectNum | 1000  |  1,792,069.1 ns |  1,767,179.0 ns |   1.67 |   3.9063 |        - |        - |   40463 B |        0.17 |
| Nextorm_WriteJson_ObjectStr | 1000  |  1,819,840.5 ns |  1,799,769.1 ns |   1.69 |  11.7188 |   3.9063 |        - |  110368 B |        0.47 |
| Nextorm_CompileOnly_3Col    | 1000  |  1,873,469.2 ns |  1,818,301.1 ns |   1.74 |   3.9063 |        - |        - |   39983 B |        0.17 |
| Nextorm_WriteJson_3Col      | 1000  |  3,798,238.0 ns |  3,734,498.4 ns |   3.54 |  23.4375 |   7.8125 |        - |  225864 B |        0.96 |
| Nextorm_WriteJson_8Col      | 1000  |  7,931,651.9 ns |  7,779,248.6 ns |   7.38 |  62.5000 |        - |        - |  677546 B |        2.87 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 10000 |  1,802,921.0 ns |  1,854,718.9 ns |   0.19 | 265.6250 | 257.8125 | 257.8125 | 1072343 B |        0.46 |
| Nextorm_CompileOnly_3Col    | 10000 |  2,046,921.6 ns |  2,068,717.8 ns |   0.21 |   3.9063 |        - |        - |   39839 B |        0.02 |
| Nextorm_WriteJson_1Col      | 10000 |  4,349,100.3 ns |  4,282,756.5 ns |   0.45 | 109.3750 |        - |        - |  972473 B |        0.42 |
| Nextorm_WriteJson_ObjectNum | 10000 |  4,608,303.1 ns |  4,524,838.2 ns |   0.48 |        - |        - |        - |   40299 B |        0.02 |
| Nextorm_WriteJson_ObjectStr | 10000 |  5,189,104.1 ns |  5,143,833.2 ns |   0.54 | 109.3750 |        - |        - |  974523 B |        0.42 |
| Nextorm_ToList_3Col         | 10000 |  9,585,913.7 ns |  9,444,609.3 ns |   1.00 | 250.0000 | 218.7500 |        - | 2323647 B |        1.00 |
| Nextorm_WriteJson_3Col      | 10000 | 16,906,143.4 ns | 16,494,355.5 ns |   1.77 | 203.1250 |  15.6250 |        - | 1809202 B |        0.78 |
| Nextorm_WriteJson_8Col      | 10000 | 40,354,101.8 ns | 39,822,703.0 ns |   4.22 | 666.6667 |        - |        - | 6148118 B |        2.65 |
