```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 2.65GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Categories | Mean       | Error        | StdDev      | Gen0    | Gen1    | Allocated  |
|----------------- |----------- |-----------:|-------------:|------------:|--------:|--------:|-----------:|
| Nextorm_Prepared |            |   938.8 μs |     21.28 μs |     1.17 μs |  4.8828 |       - |   84.38 KB |
| Nextorm_Cached   | acceptance | 1,266.0 μs |     63.67 μs |     3.49 μs | 11.7188 |  3.9063 |  194.56 KB |
| Dapper           |            | 1,407.9 μs |     73.71 μs |     4.04 μs |  7.8125 |       - |  138.29 KB |
| Linq2Db_Compiled |            | 1,526.1 μs |     37.86 μs |     2.08 μs | 13.6719 |       - |  232.82 KB |
| Linq2Db          |            | 2,538.6 μs |    210.08 μs |    11.52 μs | 23.4375 |       - |  400.02 KB |
| EFCore_Compiled  |            | 3,662.6 μs |    496.93 μs |    27.24 μs | 42.9688 | 19.5313 |   763.3 KB |
| EFCore           |            | 7,260.3 μs | 30,284.32 μs | 1,659.99 μs | 62.5000 | 31.2500 | 1163.44 KB |
