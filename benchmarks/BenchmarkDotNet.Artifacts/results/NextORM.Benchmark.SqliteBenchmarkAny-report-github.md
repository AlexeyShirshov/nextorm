```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method           | Mean       | Error    | StdDev   | Gen0     | Gen1    | Allocated  |
|----------------- |-----------:|---------:|---------:|---------:|--------:|-----------:|
| Nextorm_Prepared |   922.9 μs | 12.20 μs | 10.19 μs |   9.7656 |       - |   85.16 KB |
| Dapper           | 1,371.1 μs | 11.58 μs |  9.67 μs |  15.6250 |       - |  139.06 KB |
| Linq2Db_Compiled | 1,492.6 μs | 26.83 μs | 38.47 μs |  27.3438 |       - |  233.59 KB |
| Nextorm_Cached   | 1,607.8 μs | 19.42 μs | 17.22 μs |  62.5000 |       - |  518.02 KB |
| Linq2Db          | 2,470.5 μs | 39.02 μs | 34.59 μs |  46.8750 |       - |  401.56 KB |
| EFCore_Compiled  | 3,544.8 μs | 55.72 μs | 52.12 μs |  97.6563 | 46.8750 |  807.03 KB |
| EFCore           | 6,124.0 μs | 25.31 μs | 21.13 μs | 125.0000 | 31.2500 | 1211.76 KB |
