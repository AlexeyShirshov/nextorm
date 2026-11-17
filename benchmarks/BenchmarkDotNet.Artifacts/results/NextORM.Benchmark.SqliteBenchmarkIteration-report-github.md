```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method                                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| Nextorm_Prepared_AsyncStream          | 12.26 μs | 0.145 μs | 0.136 μs | 0.1221 |      - |    1080 B |
| Nextorm_Prepared_ToListAsync          | 12.37 μs | 0.209 μs | 0.195 μs | 0.1068 |      - |    1000 B |
| Nextorm_PreparedManualSql_ToListAsync | 12.50 μs | 0.101 μs | 0.089 μs | 0.1068 |      - |    1000 B |
| Nextorm_CachedManualSql_ToListAsync   | 14.02 μs | 0.247 μs | 0.206 μs | 0.2747 |      - |    2320 B |
| Nextorm_Cached_ToListAsync            | 14.11 μs | 0.282 μs | 0.235 μs | 0.2594 |      - |    2280 B |
| Dapper_AsyncStream                    | 16.55 μs | 0.076 μs | 0.064 μs | 0.2136 |      - |    1848 B |
| DapperAsync                           | 16.73 μs | 0.238 μs | 0.244 μs | 0.2136 |      - |    1896 B |
| Linq2Db_Compiled_ToList               | 17.17 μs | 0.058 μs | 0.051 μs | 0.2441 |      - |    2080 B |
| Linq2Db_AsyncStream                   | 17.18 μs | 0.060 μs | 0.054 μs | 0.2747 |      - |    2456 B |
| Linq2Db_ToListAsync                   | 17.69 μs | 0.098 μs | 0.082 μs | 0.3052 |      - |    2768 B |
| EFCore_Compiled_ToListAsync           | 38.08 μs | 0.743 μs | 0.826 μs | 1.1597 | 0.5493 |    9776 B |
