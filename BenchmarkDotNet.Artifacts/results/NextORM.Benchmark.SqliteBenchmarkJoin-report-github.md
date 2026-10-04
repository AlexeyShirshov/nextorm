```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method           | Mean       | Error    | StdDev   | Gen0    | Gen1   | Allocated |
|----------------- |-----------:|---------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared |   146.2 μs |  2.88 μs |  4.31 μs |  1.4648 |      - |  13.01 KB |
| Linq2Db_Compiled |   221.2 μs |  3.97 μs |  3.72 μs |  3.4180 |      - |  28.63 KB |
| Dapper           |   277.8 μs |  5.44 μs |  9.53 μs |  2.4414 |      - |  21.45 KB |
| Nextorm_Cached   |   516.5 μs | 10.14 μs | 20.49 μs | 15.6250 |      - | 134.84 KB |
| Linq2Db          |   575.3 μs | 11.45 μs | 30.57 μs | 11.7188 |      - | 106.92 KB |
| EFCore_Compiled  |   719.7 μs | 13.94 μs | 15.49 μs | 14.6484 | 4.8828 | 124.14 KB |
| EFCore           | 1,132.9 μs | 22.51 μs | 32.99 μs | 19.5313 | 3.9063 | 182.11 KB |
