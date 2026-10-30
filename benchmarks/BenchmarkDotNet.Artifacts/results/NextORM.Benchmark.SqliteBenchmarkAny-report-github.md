```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74 3.69GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Categories | Mean       | Error     | StdDev   | Gen0    | Gen1    | Allocated  |
|----------------- |----------- |-----------:|----------:|---------:|--------:|--------:|-----------:|
| Nextorm_Prepared |            |   869.7 μs |  43.19 μs |  2.37 μs |  4.8828 |       - |   85.16 KB |
| Dapper           |            | 1,180.8 μs |  54.93 μs |  3.01 μs |  7.8125 |       - |  139.07 KB |
| Linq2Db_Compiled |            | 1,248.4 μs |  78.07 μs |  4.28 μs | 13.6719 |       - |   233.6 KB |
| Nextorm_Cached   | acceptance | 1,382.0 μs |  94.48 μs |  5.18 μs | 37.1094 |       - |  611.76 KB |
| Linq2Db          |            | 1,627.5 μs | 295.74 μs | 16.21 μs | 23.4375 |       - |  401.57 KB |
| EFCore_Compiled  |            | 2,613.2 μs | 317.69 μs | 17.41 μs | 46.8750 | 23.4375 |  807.05 KB |
| EFCore           |            | 3,574.7 μs | 678.20 μs | 37.17 μs | 62.5000 | 31.2500 | 1211.81 KB |
