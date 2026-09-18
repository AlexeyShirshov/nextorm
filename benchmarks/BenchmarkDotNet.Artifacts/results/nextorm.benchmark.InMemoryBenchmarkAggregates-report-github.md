```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  Categories=InMemoryNew  

```
| Method                | Mean            | Error            | StdDev        | Ratio  | RatioSD | Gen0       | Gen1      | Allocated  | Alloc Ratio |
|---------------------- |----------------:|-----------------:|--------------:|-------:|--------:|-----------:|----------:|-----------:|------------:|
| Linq_Count            |        187.8 ns |          8.51 ns |       0.47 ns |  0.000 |    0.00 |          - |         - |          - |        0.00 |
| Nextorm_Count         |  2,156,012.2 ns |    350,728.19 ns |  19,224.60 ns |  1.000 |    0.01 |    31.2500 |         - |   268000 B |        1.00 |
| Linq_Sum              |  2,405,036.3 ns |    201,538.53 ns |  11,047.01 ns |  1.116 |    0.01 |          - |         - |     4020 B |        0.01 |
| Linq_MinMax           |  4,714,360.0 ns |     74,123.80 ns |   4,062.98 ns |  2.187 |    0.02 |          - |         - |     8039 B |        0.03 |
| Nextorm_Sum           |  5,539,534.4 ns |    368,917.47 ns |  20,221.61 ns |  2.569 |    0.02 |    31.2500 |         - |   320839 B |        1.20 |
| Nextorm_MinMax        | 11,252,641.9 ns |  1,523,749.83 ns |  83,521.87 ns |  5.219 |    0.05 |    62.5000 |         - |   641678 B |        2.39 |
| EFCoreInMemory_Count  | 31,729,729.6 ns | 17,856,880.05 ns | 978,795.85 ns | 14.718 |    0.41 |  5750.0000 | 1875.0000 | 48362111 B |      180.46 |
| EFCoreInMemory_Sum    | 37,285,920.9 ns |  2,917,432.92 ns | 159,914.34 ns | 17.295 |    0.15 |  5714.2857 | 1928.5714 | 48457727 B |      180.81 |
| EFCoreInMemory_MinMax | 78,644,166.4 ns | 10,050,344.79 ns | 550,893.31 ns | 36.479 |    0.36 | 11428.5714 | 3857.1429 | 96918654 B |      361.64 |
