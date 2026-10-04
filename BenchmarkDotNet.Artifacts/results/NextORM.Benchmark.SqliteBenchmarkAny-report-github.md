```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method           | Categories | Mean     | Error     | StdDev    | Gen0     | Gen1    | Allocated  |
|----------------- |----------- |---------:|----------:|----------:|---------:|--------:|-----------:|
| Nextorm_Prepared |            | 1.289 ms | 0.0249 ms | 0.0657 ms |   9.7656 |       - |   85.16 KB |
| Dapper           |            | 2.018 ms | 0.0502 ms | 0.1479 ms |  15.6250 |       - |  139.06 KB |
| Linq2Db_Compiled |            | 2.264 ms | 0.0466 ms | 0.1366 ms |  27.3438 |       - |  233.59 KB |
| Nextorm_Cached   | acceptance | 2.492 ms | 0.0742 ms | 0.2163 ms |  66.4063 |       - |     568 KB |
| Linq2Db          |            | 3.782 ms | 0.0962 ms | 0.2807 ms |  46.8750 |       - |  401.56 KB |
| EFCore_Compiled  |            | 5.716 ms | 0.1343 ms | 0.3917 ms |  93.7500 | 46.8750 |  807.04 KB |
| EFCore           |            | 9.092 ms | 0.2886 ms | 0.8417 ms | 125.0000 | 31.2500 | 1211.76 KB |
