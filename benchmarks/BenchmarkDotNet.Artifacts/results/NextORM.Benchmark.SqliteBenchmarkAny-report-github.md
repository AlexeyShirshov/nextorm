```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
INTEL XEON PLATINUM 8573C 3.59GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Categories | Mean       | Error     | StdDev   | Gen0   | Gen1   | Allocated  |
|----------------- |----------- |-----------:|----------:|---------:|-------:|-------:|-----------:|
| Nextorm_Prepared |            |   387.3 μs |  45.77 μs |  2.51 μs | 0.9766 |      - |   84.38 KB |
| Nextorm_Cached   | acceptance |   583.8 μs |  68.78 μs |  3.77 μs | 1.9531 | 0.9766 |  195.34 KB |
| Dapper           |            |   643.5 μs |  21.23 μs |  1.16 μs | 0.9766 |      - |  138.29 KB |
| Linq2Db_Compiled |            |   703.7 μs |  52.26 μs |  2.86 μs | 1.9531 |      - |  232.82 KB |
| Linq2Db          |            |   999.8 μs |  79.36 μs |  4.35 μs | 3.9063 |      - |  400.01 KB |
| EFCore_Compiled  |            | 2,086.0 μs | 289.77 μs | 15.88 μs | 7.8125 | 3.9063 |   763.3 KB |
| EFCore           |            | 2,617.5 μs | 416.83 μs | 22.85 μs | 7.8125 |      - | 1163.34 KB |
