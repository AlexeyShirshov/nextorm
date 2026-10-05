```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method           | Categories | Mean       | Error       | StdDev      | Median     | Gen0     | Gen1    | Allocated  |
|----------------- |----------- |-----------:|------------:|------------:|-----------:|---------:|--------:|-----------:|
| Nextorm_Prepared |            |   939.6 μs |     6.11 μs |     5.10 μs |   938.0 μs |   9.7656 |       - |   85.16 KB |
| Dapper           |            | 1,362.6 μs |    10.41 μs |     9.23 μs | 1,365.6 μs |  15.6250 |       - |  139.06 KB |
| Linq2Db_Compiled |            | 1,526.8 μs |    14.96 μs |    13.26 μs | 1,524.0 μs |  27.3438 |       - |  233.59 KB |
| Nextorm_Cached   | acceptance | 1,614.8 μs |    14.87 μs |    25.26 μs | 1,613.7 μs |  62.5000 |       - |  547.69 KB |
| Linq2Db          |            | 2,506.1 μs |    49.95 μs |    51.30 μs | 2,496.6 μs |  46.8750 |       - |   412.5 KB |
| EFCore_Compiled  |            | 3,700.8 μs |    73.53 μs |   143.42 μs | 3,634.5 μs |  93.7500 | 46.8750 |  807.03 KB |
| EFCore           |            | 9,759.0 μs | 1,510.45 μs | 4,453.60 μs | 7,044.2 μs | 125.0000 | 31.2500 | 1211.76 KB |
