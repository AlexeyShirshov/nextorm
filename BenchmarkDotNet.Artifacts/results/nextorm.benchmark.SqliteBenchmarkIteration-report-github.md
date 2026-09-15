```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]    : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  

```
| Method                                | Mean     | Error    | StdDev   | Gen0   | Allocated |
|-------------------------------------- |---------:|---------:|---------:|-------:|----------:|
| Nextorm_Prepared_AsyncStream          | 857.6 μs | 16.86 μs | 27.22 μs |      - |     792 B |
| Nextorm_Cached_ToListAsync            | 861.0 μs | 17.01 μs | 38.40 μs |      - |    1600 B |
| Nextorm_PreparedManualSql_ToListAsync | 866.7 μs | 15.99 μs | 17.11 μs |      - |     832 B |
| Nextorm_Prepared_ToListAsync          | 880.4 μs | 16.31 μs | 15.26 μs |      - |     832 B |
| Nextorm_CachedManualSql_ToListAsync   | 896.4 μs | 17.52 μs | 19.48 μs |      - |    1720 B |
| Dapper_AsyncStream                    | 900.3 μs | 11.74 μs | 10.41 μs |      - |    1856 B |
| DapperAsync                           | 914.5 μs |  9.69 μs |  8.59 μs |      - |    1904 B |
| EFCore_Compiled_ToListAsync           | 928.3 μs | 17.80 μs | 18.28 μs | 0.9766 |    9872 B |
