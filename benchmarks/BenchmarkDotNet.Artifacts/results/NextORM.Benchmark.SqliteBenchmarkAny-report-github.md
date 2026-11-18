```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V45 4.30GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Categories | Mean       | Error     | StdDev   | Gen0    | Gen1    | Allocated  |
|----------------- |----------- |-----------:|----------:|---------:|--------:|--------:|-----------:|
| Nextorm_Prepared |            |   728.6 μs |  69.05 μs |  3.78 μs |  4.8828 |       - |   84.38 KB |
| Nextorm_Cached   | acceptance |   860.0 μs |  93.71 μs |  5.14 μs | 11.7188 |  2.9297 |  194.56 KB |
| Dapper           |            |   921.6 μs |  45.93 μs |  2.52 μs |  7.8125 |       - |  138.29 KB |
| Linq2Db_Compiled |            |   968.5 μs | 463.25 μs | 25.39 μs | 13.6719 |       - |  232.81 KB |
| Linq2Db          |            | 1,159.9 μs |  72.29 μs |  3.96 μs | 23.4375 |       - |  400.01 KB |
| EFCore_Compiled  |            | 1,950.4 μs | 114.38 μs |  6.27 μs | 42.9688 | 19.5313 |  763.28 KB |
| EFCore           |            | 2,348.8 μs | 350.36 μs | 19.20 μs | 70.3125 | 31.2500 | 1163.31 KB |
