```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method           | Mean     | Error    | StdDev   | Gen0    | Gen1   | Allocated |
|----------------- |---------:|---------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared | 104.2 μs |  0.50 μs |  0.44 μs |  1.5869 |      - |  13.01 KB |
| Dapper           | 198.6 μs |  3.91 μs |  3.66 μs |  2.4414 |      - |  21.45 KB |
| Linq2Db_Compiled | 208.7 μs |  1.52 μs |  1.42 μs |  3.4180 |      - |  28.63 KB |
| Nextorm_Cached   | 291.5 μs |  4.49 μs |  4.20 μs | 11.7188 |      - |  108.2 KB |
| Linq2Db          | 525.4 μs |  1.26 μs |  1.05 μs | 11.7188 |      - | 105.35 KB |
| EFCore_Compiled  | 529.7 μs |  9.07 μs |  8.49 μs | 14.6484 | 4.8828 | 124.14 KB |
| EFCore           | 834.0 μs | 16.37 μs | 26.43 μs | 19.5313 | 3.9063 | 182.11 KB |
