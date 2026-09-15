```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| Nextorm_PreparedManualSql_ToListAsync | 10.48 μs | 1.360 μs | 0.075 μs | 0.0916 |      - |     832 B |
| Nextorm_Prepared_ToListAsync          | 10.50 μs | 0.514 μs | 0.028 μs | 0.0916 |      - |     832 B |
| Nextorm_Prepared_AsyncStream          | 10.84 μs | 2.856 μs | 0.157 μs | 0.0916 |      - |     792 B |
| Nextorm_Cached_ToListAsync            | 10.92 μs | 4.192 μs | 0.230 μs | 0.1831 |      - |    1600 B |
| Nextorm_CachedManualSql_ToListAsync   | 10.92 μs | 0.768 μs | 0.042 μs | 0.1984 |      - |    1720 B |
| DapperAsync                           | 15.12 μs | 4.348 μs | 0.238 μs | 0.2136 |      - |    1904 B |
| Dapper_AsyncStream                    | 15.33 μs | 4.750 μs | 0.260 μs | 0.2136 |      - |    1856 B |
| EFCore_Compiled_ToListAsync           | 36.72 μs | 3.494 μs | 0.192 μs | 1.1597 | 0.5493 |    9872 B |
