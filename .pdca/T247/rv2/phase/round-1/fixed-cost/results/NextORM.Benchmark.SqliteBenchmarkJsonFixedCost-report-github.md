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
| Nextorm_SteadyWriter_3Col   | 1     |        258.5 ns |        253.0 ns |   0.01 |   0.0534 |        - |        - |     448 B |        0.10 |
| Nextorm_ToList_3Col         | 1     |     18,286.0 ns |     18,120.8 ns |   1.00 |   0.4883 |        - |        - |    4600 B |        1.00 |
| Nextorm_WriteJson_1Col      | 1     |    554,321.8 ns |    554,927.1 ns |  30.40 |   0.9766 |        - |        - |   12512 B |        2.72 |
| Nextorm_WriteJson_ObjectStr | 1     |  1,250,076.8 ns |  1,192,881.1 ns |  68.56 |        - |        - |        - |   14425 B |        3.14 |
| Nextorm_WriteJson_ObjectNum | 1     |  1,451,214.9 ns |  1,453,280.9 ns |  79.59 |   3.9063 |        - |        - |   40153 B |        8.73 |
| Nextorm_WriteJson_3Col      | 1     |  1,999,783.7 ns |  1,964,164.1 ns | 109.68 |   3.9063 |        - |        - |   49087 B |       10.67 |
| Nextorm_CompileOnly_3Col    | 1     |  2,116,888.5 ns |  2,079,891.3 ns | 116.10 |   3.9063 |        - |        - |   40415 B |        8.79 |
| Nextorm_WriteJson_8Col      | 1     |  3,223,648.4 ns |  3,231,378.1 ns | 176.81 |   7.8125 |   3.9063 |        - |   69545 B |       15.12 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 10    |      2,203.2 ns |      2,181.5 ns |   0.08 |   0.6027 |        - |        - |    5056 B |        0.75 |
| Nextorm_ToList_3Col         | 10    |     28,301.3 ns |     28,142.2 ns |   1.00 |   0.7324 |        - |        - |    6768 B |        1.00 |
| Nextorm_WriteJson_1Col      | 10    |    648,113.9 ns |    620,299.3 ns |  22.97 |   0.9766 |        - |        - |   13383 B |        1.98 |
| Nextorm_WriteJson_ObjectNum | 10    |  1,541,955.3 ns |  1,522,566.9 ns |  54.65 |   3.9063 |        - |        - |   40449 B |        5.98 |
| Nextorm_WriteJson_ObjectStr | 10    |  1,667,270.2 ns |  1,615,747.7 ns |  59.09 |        - |        - |        - |   15291 B |        2.26 |
| Nextorm_CompileOnly_3Col    | 10    |  2,002,929.4 ns |  1,975,217.4 ns |  70.99 |   3.9063 |        - |        - |   40007 B |        5.91 |
| Nextorm_WriteJson_3Col      | 10    |  2,053,635.9 ns |  2,048,553.7 ns |  72.78 |   3.9063 |        - |        - |   50638 B |        7.48 |
| Nextorm_WriteJson_8Col      | 10    |  4,200,160.5 ns |  4,103,236.5 ns | 148.86 |   7.8125 |        - |        - |   74826 B |       11.06 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 100   |     13,803.6 ns |     13,789.3 ns |   0.09 |   2.1820 |   0.0916 |        - |   18328 B |        0.67 |
| Nextorm_ToList_3Col         | 100   |    155,590.9 ns |    153,729.2 ns |   1.01 |   2.9297 |        - |        - |   27553 B |        1.00 |
| Nextorm_WriteJson_1Col      | 100   |    696,298.9 ns |    698,907.9 ns |   4.53 |   1.9531 |   0.9766 |        - |   22035 B |        0.80 |
| Nextorm_WriteJson_ObjectNum | 100   |  1,382,253.0 ns |  1,380,598.2 ns |   9.00 |   3.9063 |   1.9531 |        - |   40260 B |        1.46 |
| Nextorm_WriteJson_ObjectStr | 100   |  1,415,659.4 ns |  1,417,263.1 ns |   9.22 |   1.9531 |        - |        - |   23955 B |        0.87 |
| Nextorm_CompileOnly_3Col    | 100   |  1,902,843.9 ns |  1,866,511.8 ns |  12.39 |   3.9063 |        - |        - |   39999 B |        1.45 |
| Nextorm_WriteJson_3Col      | 100   |  2,679,559.8 ns |  2,653,672.5 ns |  17.44 |   7.8125 |        - |        - |   66805 B |        2.42 |
| Nextorm_WriteJson_8Col      | 100   |  3,737,697.2 ns |  3,699,447.2 ns |  24.33 |   7.8125 |        - |        - |  129650 B |        4.71 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_SteadyWriter_3Col   | 1000  |    138,613.5 ns |    137,648.5 ns |   0.16 |  13.4277 |   2.1973 |        - |  114176 B |        0.48 |
| Nextorm_WriteJson_1Col      | 1000  |    890,886.2 ns |    893,974.2 ns |   1.00 |  11.7188 |   3.9063 |        - |  108947 B |        0.46 |
| Nextorm_ToList_3Col         | 1000  |    891,582.2 ns |    890,953.5 ns |   1.00 |  27.3438 |   9.7656 |        - |  236317 B |        1.00 |
| Nextorm_WriteJson_ObjectStr | 1000  |  1,414,849.6 ns |  1,406,483.7 ns |   1.59 |  11.7188 |   3.9063 |        - |  110472 B |        0.47 |
| Nextorm_WriteJson_ObjectNum | 1000  |  1,507,960.1 ns |  1,506,137.7 ns |   1.69 |   3.9063 |        - |        - |   40161 B |        0.17 |
| Nextorm_CompileOnly_3Col    | 1000  |  1,687,582.7 ns |  1,682,582.1 ns |   1.89 |   3.9063 |        - |        - |   40271 B |        0.17 |
| Nextorm_WriteJson_3Col      | 1000  |  3,000,460.3 ns |  3,007,256.3 ns |   3.37 |  23.4375 |   7.8125 |        - |  224920 B |        0.95 |
| Nextorm_WriteJson_8Col      | 1000  |  6,561,450.3 ns |  6,512,420.3 ns |   7.36 |  78.1250 |  15.6250 |        - |  678219 B |        2.87 |
|                             |       |                 |                 |        |          |          |          |           |             |
| Nextorm_CompileOnly_3Col    | 10000 |  1,785,998.6 ns |  1,728,242.8 ns |   0.19 |   3.9063 |        - |        - |   40063 B |        0.02 |
| Nextorm_SteadyWriter_3Col   | 10000 |  1,920,125.3 ns |  1,910,495.5 ns |   0.20 | 277.3438 | 277.3438 | 277.3438 | 1072345 B |        0.46 |
| Nextorm_WriteJson_1Col      | 10000 |  4,180,087.3 ns |  4,191,911.5 ns |   0.44 | 109.3750 |        - |        - |  972473 B |        0.42 |
| Nextorm_WriteJson_ObjectStr | 10000 |  5,180,068.5 ns |  5,166,058.7 ns |   0.55 | 109.3750 |        - |        - |  974443 B |        0.42 |
| Nextorm_WriteJson_ObjectNum | 10000 |  5,833,655.5 ns |  5,063,832.5 ns |   0.62 |        - |        - |        - |   40747 B |        0.02 |
| Nextorm_ToList_3Col         | 10000 |  9,409,391.9 ns |  9,400,500.5 ns |   1.00 | 265.6250 | 250.0000 |        - | 2323647 B |        1.00 |
| Nextorm_WriteJson_3Col      | 10000 | 14,930,872.2 ns | 14,773,951.2 ns |   1.59 | 187.5000 |        - |        - | 1809486 B |        0.78 |
| Nextorm_WriteJson_8Col      | 10000 | 37,797,678.6 ns | 37,406,846.4 ns |   4.02 | 666.6667 |        - |        - | 6147942 B |        2.65 |
