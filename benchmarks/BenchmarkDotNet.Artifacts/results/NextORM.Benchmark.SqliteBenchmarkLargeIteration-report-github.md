```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
AMD EPYC 7763 3.24GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                       | Mean      | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------------- |----------:|---------:|---------:|---------:|----------:|
| Nextorm_Cached_ToListAsync   |  9.387 ms | 125.0000 |  62.5000 |        - |   2.21 MB |
| Nextorm_Prepared_AsyncStream |  9.539 ms | 125.0000 |        - |        - |   2.14 MB |
| Nextorm_Prepared_ToListAsync |  9.565 ms | 125.0000 |  62.5000 |        - |   2.21 MB |
| Nextorm_Cached_AsyncStream   |  9.800 ms | 125.0000 |        - |        - |   2.14 MB |
| Dapper_AsyncStream           | 10.058 ms | 156.2500 |        - |        - |   2.59 MB |
| AdoTupleToList               | 10.199 ms | 281.2500 | 281.2500 | 281.2500 |   2.68 MB |
| Linq2Db_AsyncStream          | 10.638 ms | 125.0000 |        - |        - |   2.14 MB |
| EFCore_Compiled_AsyncStream  | 11.256 ms | 265.6250 |        - |        - |   4.28 MB |
| EFCore_AsyncStream           | 11.773 ms | 265.6250 |        - |        - |   4.28 MB |
| AdoWithDelegate              | 12.138 ms | 187.5000 | 171.8750 |  46.8750 |   2.39 MB |
| Dapper_Async                 | 12.659 ms | 187.5000 | 171.8750 |  62.5000 |   2.85 MB |
| Linq2Db_Compiled_ToList      | 12.706 ms | 187.5000 | 171.8750 |  46.8750 |   2.39 MB |
| Linq2Db_ToListAsync          | 13.664 ms | 187.5000 | 171.8750 |  46.8750 |   2.39 MB |
| EFCore_ToListAsync           | 15.188 ms | 281.2500 | 187.5000 |  62.5000 |   4.53 MB |
