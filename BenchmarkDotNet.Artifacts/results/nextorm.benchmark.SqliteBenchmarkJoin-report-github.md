```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method           | Mean     | Error   | StdDev  | Gen0    | Gen1   | Allocated |
|----------------- |---------:|--------:|--------:|--------:|-------:|----------:|
| Nextorm_Prepared | 104.6 μs | 1.91 μs | 1.79 μs |  1.4648 |      - |   12.3 KB |
| Dapper           | 199.0 μs | 3.94 μs | 3.69 μs |  2.4414 |      - |  21.45 KB |
| Nextorm_Cached   | 260.3 μs | 3.87 μs | 3.43 μs |  9.7656 |      - |  91.79 KB |
| EFCore_Compiled  | 510.3 μs | 9.02 μs | 8.43 μs | 14.6484 | 4.8828 | 124.14 KB |
| Linq2Db          | 534.1 μs | 2.40 μs | 2.00 μs | 11.7188 |      - | 107.31 KB |
| EFCore           | 814.8 μs | 7.04 μs | 6.59 μs | 19.5313 | 3.9063 | 182.11 KB |
