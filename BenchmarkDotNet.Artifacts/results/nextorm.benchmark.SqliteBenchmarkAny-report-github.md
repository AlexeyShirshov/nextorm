```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method           | Mean       | Error    | StdDev   | Gen0     | Gen1    | Allocated  |
|----------------- |-----------:|---------:|---------:|---------:|--------:|-----------:|
| Nextorm_Prepared |   917.6 μs |  8.03 μs |  7.51 μs |   9.7656 |       - |   85.16 KB |
| Nextorm_Cached   | 1,310.7 μs | 14.92 μs | 13.96 μs |  39.0625 |       - |  349.25 KB |
| Dapper           | 1,362.9 μs | 14.84 μs | 13.88 μs |  15.6250 |       - |  139.06 KB |
| Linq2Db          | 2,498.4 μs | 47.14 μs | 44.09 μs |  46.8750 |       - |  401.56 KB |
| EFCore_Compiled  | 3,553.3 μs | 36.89 μs | 34.50 μs |  97.6563 | 46.8750 |  807.03 KB |
| EFCore           | 5,969.7 μs | 58.28 μs | 48.67 μs | 125.0000 | 31.2500 | 1211.76 KB |
