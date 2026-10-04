```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  

```
| Method                       | Mean     | Median    | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------------- |---------:|----------:|---------:|---------:|---------:|----------:|
| Nextorm_Prepared_ToListAsync | 11.58 ms | 11.491 ms | 265.6250 | 156.2500 |        - |   2.21 MB |
| Nextorm_Prepared_AsyncStream | 11.63 ms | 11.695 ms | 265.6250 |        - |        - |   2.14 MB |
| AdoTupleToList               | 14.09 ms |  9.573 ms | 281.2500 | 281.2500 | 281.2500 |   2.68 MB |
| Nextorm_Cached_AsyncStream   | 20.91 ms | 24.262 ms | 265.6250 |        - |        - |   2.14 MB |
| Nextorm_Cached_ToListAsync   | 22.30 ms | 22.256 ms | 250.0000 | 156.2500 |        - |   2.22 MB |
| Dapper_AsyncStream           | 26.07 ms | 26.047 ms | 312.5000 |        - |        - |   2.59 MB |
| EFCore_AsyncStream           | 28.17 ms | 27.801 ms | 531.2500 |        - |        - |   4.28 MB |
| AdoWithDelegate              | 28.19 ms | 30.062 ms | 312.5000 | 187.5000 |  62.5000 |   2.39 MB |
| EFCore_Compiled_AsyncStream  | 29.56 ms | 28.895 ms | 531.2500 |        - |        - |   4.28 MB |
| Linq2Db_AsyncStream          | 32.34 ms | 32.205 ms | 250.0000 |        - |        - |   2.14 MB |
| Linq2Db_Compiled_ToList      | 32.48 ms | 32.420 ms | 343.7500 | 250.0000 |  62.5000 |   2.39 MB |
| Dapper_Async                 | 35.91 ms | 36.475 ms | 272.7273 |  90.9091 |        - |   2.84 MB |
| Linq2Db_ToListAsync          | 37.63 ms | 37.329 ms | 312.5000 | 187.5000 |  62.5000 |   2.39 MB |
| EFCore_ToListAsync           | 38.18 ms | 37.650 ms | 615.3846 | 384.6154 |  76.9231 |   4.53 MB |
