```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 3.24GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method           | Categories | Mean       | Error        | StdDev      | Gen0    | Gen1    | Allocated  |
|----------------- |----------- |-----------:|-------------:|------------:|--------:|--------:|-----------:|
| Nextorm_Prepared |            |   940.9 μs |     25.19 μs |     1.38 μs |  4.8828 |       - |   84.38 KB |
| Nextorm_Cached   | acceptance | 1,310.3 μs |    144.40 μs |     7.91 μs | 11.7188 |  3.9063 |  194.56 KB |
| Dapper           |            | 1,453.0 μs |    117.03 μs |     6.41 μs |  7.8125 |       - |  138.29 KB |
| Linq2Db_Compiled |            | 1,598.0 μs |    149.28 μs |     8.18 μs | 13.6719 |       - |  232.82 KB |
| Linq2Db          |            | 2,616.9 μs |    218.60 μs |    11.98 μs | 23.4375 |       - |  400.02 KB |
| EFCore_Compiled  |            | 3,745.4 μs |    815.49 μs |    44.70 μs | 42.9688 | 19.5313 |   763.3 KB |
| EFCore           |            | 7,272.9 μs | 23,362.85 μs | 1,280.60 μs | 62.5000 | 31.2500 | 1163.44 KB |
