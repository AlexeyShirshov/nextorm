```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
AMD EPYC 9V74 3.68GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.403
  [Host] : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                       | Mean      | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------------- |----------:|---------:|---------:|---------:|----------:|
| Nextorm_Prepared_AsyncStream |  7.589 ms | 132.8125 |        - |        - |   2.14 MB |
| Nextorm_Prepared_ToListAsync |  7.624 ms | 132.8125 |  78.1250 |        - |   2.21 MB |
| Nextorm_Cached_ToListAsync   |  7.681 ms | 132.8125 |  78.1250 |        - |   2.22 MB |
| Nextorm_Cached_AsyncStream   |  7.724 ms | 125.0000 |        - |        - |   2.14 MB |
| AdoTupleToList               |  8.106 ms | 281.2500 | 281.2500 | 281.2500 |   2.68 MB |
| Dapper_AsyncStream           |  8.176 ms | 156.2500 |        - |        - |   2.59 MB |
| Linq2Db_AsyncStream          |  8.537 ms | 125.0000 |        - |        - |   2.14 MB |
| EFCore_Compiled_AsyncStream  |  8.950 ms | 265.6250 |        - |        - |   4.28 MB |
| EFCore_AsyncStream           |  8.959 ms | 265.6250 |        - |        - |   4.28 MB |
| AdoWithDelegate              |  9.416 ms | 187.5000 | 171.8750 |  46.8750 |   2.39 MB |
| Linq2Db_Compiled_ToList      |  9.999 ms | 187.5000 | 171.8750 |  46.8750 |   2.39 MB |
| Dapper_Async                 | 10.183 ms | 187.5000 | 171.8750 |  62.5000 |   2.85 MB |
| Linq2Db_ToListAsync          | 10.312 ms | 187.5000 | 171.8750 |  46.8750 |   2.39 MB |
| EFCore_ToListAsync           | 11.557 ms | 296.8750 | 203.1250 |  62.5000 |   4.53 MB |
