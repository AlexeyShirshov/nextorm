```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V74 3.69GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Categories | Mean       | Error       | StdDev   | Gen0    | Gen1    | Allocated  |
|----------------- |----------- |-----------:|------------:|---------:|--------:|--------:|-----------:|
| Nextorm_Prepared |            |   877.4 μs |    53.19 μs |  2.92 μs |  4.8828 |       - |   85.16 KB |
| Dapper           |            | 1,176.8 μs |    40.86 μs |  2.24 μs |  7.8125 |       - |  139.07 KB |
| Linq2Db_Compiled |            | 1,246.9 μs |    42.88 μs |  2.35 μs | 13.6719 |       - |   233.6 KB |
| Nextorm_Cached   | acceptance | 1,406.1 μs |   110.83 μs |  6.07 μs | 37.1094 |       - |  618.02 KB |
| Linq2Db          |            | 1,613.7 μs |    20.24 μs |  1.11 μs | 23.4375 |       - |  401.57 KB |
| EFCore_Compiled  |            | 2,457.1 μs |    90.40 μs |  4.95 μs | 46.8750 | 23.4375 |  807.05 KB |
| EFCore           |            | 3,489.2 μs | 1,042.07 μs | 57.12 μs | 62.5000 | 31.2500 | 1211.82 KB |
