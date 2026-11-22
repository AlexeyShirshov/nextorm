```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method                                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| Nextorm_Prepared_ToListAsync          | 11.64 μs | 0.094 μs | 0.079 μs | 0.1068 |      - |     968 B |
| Nextorm_PreparedManualSql_ToListAsync | 11.67 μs | 0.106 μs | 0.099 μs | 0.1068 |      - |     968 B |
| Nextorm_CachedManualSql_ToListAsync   | 13.33 μs | 0.146 μs | 0.143 μs | 0.2594 |      - |    2288 B |
| Nextorm_Cached_ToListAsync            | 13.46 μs | 0.174 μs | 0.145 μs | 0.2594 |      - |    2248 B |
| Linq2Db_ToListAsync                   | 16.93 μs | 0.303 μs | 0.284 μs | 0.3052 |      - |    2768 B |
| EFCore_Compiled_ToListAsync           | 37.25 μs | 0.701 μs | 1.092 μs | 1.1597 | 0.5493 |    9776 B |
