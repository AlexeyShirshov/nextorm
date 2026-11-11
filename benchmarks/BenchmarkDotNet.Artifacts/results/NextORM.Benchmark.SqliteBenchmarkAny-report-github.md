```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 3.13GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Categories | Mean       | Error        | StdDev      | Gen0    | Gen1    | Allocated  |
|----------------- |----------- |-----------:|-------------:|------------:|--------:|--------:|-----------:|
| Nextorm_Prepared |            |   923.5 μs |     32.33 μs |     1.77 μs |  4.8828 |       - |   84.38 KB |
| Nextorm_Cached   | acceptance | 1,262.2 μs |    118.68 μs |     6.51 μs | 11.7188 |  3.9063 |  193.78 KB |
| Dapper           |            | 1,421.9 μs |    164.90 μs |     9.04 μs |  7.8125 |       - |  138.29 KB |
| Linq2Db_Compiled |            | 1,523.8 μs |    117.87 μs |     6.46 μs | 13.6719 |       - |  232.82 KB |
| Linq2Db          |            | 2,561.0 μs |    450.57 μs |    24.70 μs | 23.4375 |       - |  400.02 KB |
| EFCore_Compiled  |            | 3,742.6 μs |    516.71 μs |    28.32 μs | 42.9688 | 19.5313 |   763.3 KB |
| EFCore           |            | 7,168.9 μs | 27,767.30 μs | 1,522.02 μs | 62.5000 | 31.2500 | 1163.44 KB |
