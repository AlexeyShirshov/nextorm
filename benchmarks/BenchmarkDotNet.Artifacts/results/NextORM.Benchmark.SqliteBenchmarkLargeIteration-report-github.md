```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 9V45 4.23GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                       | Mean      | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------------- |----------:|---------:|---------:|---------:|----------:|
| Nextorm_Prepared_AsyncStream |  6.632 ms | 132.8125 |        - |        - |   2.14 MB |
| Nextorm_Prepared_ToListAsync |  6.735 ms | 132.8125 |  78.1250 |        - |   2.21 MB |
| Nextorm_Cached_ToListAsync   |  6.994 ms | 132.8125 |  78.1250 |        - |   2.21 MB |
| Nextorm_Cached_AsyncStream   |  7.043 ms | 132.8125 |        - |        - |   2.14 MB |
| AdoTupleToList               |  7.457 ms | 281.2500 | 281.2500 | 281.2500 |   2.68 MB |
| Dapper_AsyncStream           |  7.467 ms | 156.2500 |        - |        - |   2.59 MB |
| Linq2Db_AsyncStream          |  7.585 ms | 132.8125 |        - |        - |   2.14 MB |
| EFCore_Compiled_AsyncStream  |  7.659 ms | 265.6250 |        - |        - |   4.28 MB |
| EFCore_AsyncStream           |  7.850 ms | 265.6250 |        - |        - |   4.28 MB |
| AdoWithDelegate              |  8.292 ms | 187.5000 | 171.8750 |  46.8750 |   2.39 MB |
| Linq2Db_Compiled_ToList      |  9.061 ms | 187.5000 | 171.8750 |  46.8750 |   2.39 MB |
| Dapper_Async                 |  9.095 ms | 187.5000 | 171.8750 |  62.5000 |   2.85 MB |
| Linq2Db_ToListAsync          |  9.441 ms | 187.5000 | 171.8750 |  46.8750 |   2.39 MB |
| EFCore_ToListAsync           | 10.620 ms | 296.8750 | 203.1250 |  62.5000 |   4.53 MB |
