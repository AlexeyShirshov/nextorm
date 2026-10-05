```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method           | Mean       | Error    | StdDev    | Median     | Gen0    | Gen1   | Allocated |
|----------------- |-----------:|---------:|----------:|-----------:|--------:|-------:|----------:|
| Nextorm_Prepared |   137.5 μs |  2.67 μs |   3.57 μs |   137.8 μs |  1.4648 |      - |  13.01 KB |
| Dapper           |   278.8 μs |  5.50 μs |  12.29 μs |   274.7 μs |  1.9531 |      - |  21.45 KB |
| Linq2Db_Compiled |   284.0 μs |  5.61 μs |  11.96 μs |   281.7 μs |  2.9297 |      - |  28.63 KB |
| Nextorm_Cached   |   496.0 μs | 13.23 μs |  33.68 μs |   487.0 μs | 15.6250 |      - | 129.53 KB |
| EFCore_Compiled  |   708.4 μs | 13.83 μs |  29.18 μs |   706.3 μs | 13.6719 | 3.9063 | 124.14 KB |
| Linq2Db          |   722.7 μs | 14.15 μs |  29.22 μs |   722.1 μs | 11.7188 |      - | 105.35 KB |
| EFCore           | 1,216.4 μs | 92.62 μs | 255.10 μs | 1,111.1 μs | 19.5313 | 3.9063 | 182.11 KB |
