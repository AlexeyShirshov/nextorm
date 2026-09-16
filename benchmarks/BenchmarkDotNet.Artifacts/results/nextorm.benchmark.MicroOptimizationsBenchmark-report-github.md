```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                          | Mean        | Error      | StdDev    | Median      | Ratio    | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------- |------------:|-----------:|----------:|------------:|---------:|--------:|-------:|----------:|------------:|
| M7_StartsWith_Ordinal           |   0.0049 ns |  0.1560 ns | 0.0086 ns |   0.0000 ns |     0.02 |    0.04 |      - |         - |          NA |
| M7_EndsWith_Ordinal             |   0.0081 ns |  0.0814 ns | 0.0045 ns |   0.0099 ns |     0.04 |    0.02 |      - |         - |          NA |
| M5_FastPath_LongToBool          |   0.2032 ns |  0.7676 ns | 0.0421 ns |   0.2232 ns |     1.03 |    0.30 |      - |         - |          NA |
| M5_ConvertChangeType_LongToBool |   0.2075 ns |  1.0263 ns | 0.0563 ns |   0.1973 ns |     1.05 |    0.35 |      - |         - |          NA |
| M8_CachedLookup                 |   0.2571 ns |  1.5007 ns | 0.0823 ns |   0.2483 ns |     1.30 |    0.47 |      - |         - |          NA |
| M6_Bitwise                      |   1.6986 ns |  3.2366 ns | 0.1774 ns |   1.7778 ns |     8.59 |    2.10 |      - |         - |          NA |
| M11_Loop_Any                    |   2.0109 ns |  0.1890 ns | 0.0104 ns |   2.0070 ns |    10.17 |    2.30 |      - |         - |          NA |
| M6_HasFlag                      |   2.2327 ns |  3.8262 ns | 0.2097 ns |   2.2845 ns |    11.29 |    2.72 |      - |         - |          NA |
| M11_Linq_Any                    |   2.9139 ns |  2.7208 ns | 0.1491 ns |   2.8970 ns |    14.73 |    3.40 |      - |         - |          NA |
| M11_Loop_SelectToArray          |   9.1569 ns |  8.7742 ns | 0.4809 ns |   9.0763 ns |    46.30 |   10.69 | 0.0057 |      48 B |          NA |
| M7_StartsWith_Culture           |  14.5740 ns |  5.7115 ns | 0.3131 ns |  14.4055 ns |    73.69 |   16.73 |      - |         - |          NA |
| M7_EndsWith_Culture             |  19.3139 ns | 23.5603 ns | 1.2914 ns |  18.6645 ns |    97.66 |   22.84 |      - |         - |          NA |
| M11_Linq_SelectToArray          |  19.8878 ns | 16.7144 ns | 0.9162 ns |  20.2166 ns |   100.56 |   23.12 | 0.0143 |     120 B |          NA |
| M10_Sealed_InterfaceCall        |  41.9227 ns | 19.2238 ns | 1.0537 ns |  41.4885 ns |   211.99 |   48.19 |      - |         - |          NA |
| M10_Unsealed_InterfaceCall      |  42.4037 ns | 13.8548 ns | 0.7594 ns |  42.1520 ns |   214.42 |   48.63 |      - |         - |          NA |
| M8_StringFormat                 |  47.4752 ns | 24.0884 ns | 1.3204 ns |  47.4540 ns |   240.06 |   54.63 | 0.0076 |      64 B |          NA |
| M3_New_FastPathPerRow           |  82.6255 ns |  0.8998 ns | 0.0493 ns |  82.6505 ns |   417.80 |   94.52 |      - |         - |          NA |
| M3_Old_AwaitPerRow              | 479.9092 ns | 15.2389 ns | 0.8353 ns | 480.0360 ns | 2,426.70 |  549.00 |      - |         - |          NA |
