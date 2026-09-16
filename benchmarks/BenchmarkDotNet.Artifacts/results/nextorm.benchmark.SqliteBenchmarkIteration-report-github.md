```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| Nextorm_PreparedManualSql_ToListAsync | 10.31 μs | 0.045 μs | 0.042 μs | 0.1068 |      - |     976 B |
| Nextorm_Prepared_AsyncStream          | 10.51 μs | 0.063 μs | 0.059 μs | 0.0916 |      - |     792 B |
| Nextorm_Prepared_ToListAsync          | 10.55 μs | 0.058 μs | 0.055 μs | 0.1068 |      - |     976 B |
| Nextorm_Cached_ToListAsync            | 10.74 μs | 0.052 μs | 0.046 μs | 0.1984 |      - |    1744 B |
| Nextorm_CachedManualSql_ToListAsync   | 10.81 μs | 0.030 μs | 0.028 μs | 0.2136 |      - |    1864 B |
| DapperAsync                           | 15.01 μs | 0.149 μs | 0.139 μs | 0.2136 |      - |    1904 B |
| Dapper_AsyncStream                    | 15.35 μs | 0.138 μs | 0.115 μs | 0.2136 |      - |    1856 B |
| Linq2Db_AsyncStream                   | 15.85 μs | 0.076 μs | 0.067 μs | 0.2747 |      - |    2472 B |
| Linq2Db_ToListAsync                   | 15.95 μs | 0.079 μs | 0.074 μs | 0.3052 |      - |    2792 B |
| EFCore_Compiled_ToListAsync           | 35.77 μs | 0.417 μs | 0.390 μs | 1.2207 | 0.6104 |   10224 B |
