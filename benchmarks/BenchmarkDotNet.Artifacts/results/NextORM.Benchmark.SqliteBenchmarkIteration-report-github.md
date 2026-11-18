```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method                                | Mean     | Error    | StdDev   | Median   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|---------:|---------:|---------:|-------:|-------:|----------:|
| Nextorm_PreparedManualSql_ToListAsync | 11.75 μs | 0.043 μs | 0.040 μs | 11.75 μs | 0.1068 |      - |     968 B |
| Nextorm_Prepared_AsyncStream          | 11.81 μs | 0.066 μs | 0.052 μs | 11.81 μs | 0.1221 |      - |    1048 B |
| Nextorm_Prepared_ToListAsync          | 11.92 μs | 0.163 μs | 0.136 μs | 11.93 μs | 0.1068 |      - |     968 B |
| Nextorm_CachedManualSql_ToListAsync   | 13.40 μs | 0.227 μs | 0.189 μs | 13.38 μs | 0.2594 |      - |    2288 B |
| Nextorm_Cached_ToListAsync            | 14.11 μs | 0.327 μs | 0.949 μs | 13.71 μs | 0.2594 |      - |    2248 B |
| Dapper_AsyncStream                    | 16.40 μs | 0.313 μs | 0.372 μs | 16.32 μs | 0.2136 |      - |    1848 B |
| DapperAsync                           | 16.43 μs | 0.306 μs | 0.271 μs | 16.34 μs | 0.2136 |      - |    1896 B |
| Linq2Db_AsyncStream                   | 16.84 μs | 0.107 μs | 0.095 μs | 16.84 μs | 0.2747 |      - |    2456 B |
| Linq2Db_Compiled_ToList               | 16.92 μs | 0.240 μs | 0.213 μs | 16.82 μs | 0.2441 |      - |    2080 B |
| Linq2Db_ToListAsync                   | 17.28 μs | 0.308 μs | 0.401 μs | 17.15 μs | 0.3052 |      - |    2768 B |
| EFCore_Compiled_ToListAsync           | 35.90 μs | 0.411 μs | 0.364 μs | 35.95 μs | 1.1597 | 0.5493 |    9776 B |
