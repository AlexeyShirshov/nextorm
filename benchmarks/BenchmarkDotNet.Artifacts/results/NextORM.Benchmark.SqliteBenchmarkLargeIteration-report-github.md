```

BenchmarkDotNet v0.15.8, Linux Ubuntu 26.04.1 LTS (Resolute Raccoon)
INTEL XEON PLATINUM 8573C 3.46GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Job=ShortRun  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  

```
| Method                       | Mean      | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------------- |----------:|---------:|---------:|---------:|----------:|
| Nextorm_Prepared_ToListAsync |  8.590 ms |  15.6250 |        - |        - |   2.21 MB |
| Nextorm_Cached_ToListAsync   |  8.634 ms |  15.6250 |        - |        - |   2.21 MB |
| Nextorm_Prepared_AsyncStream |  8.778 ms |  15.6250 |        - |        - |   2.14 MB |
| Nextorm_Cached_AsyncStream   |  8.840 ms |  15.6250 |        - |        - |   2.14 MB |
| AdoWithDelegate              |  8.861 ms |  31.2500 |  31.2500 |  31.2500 |   2.39 MB |
| AdoTupleToList               |  8.963 ms | 281.2500 | 281.2500 | 281.2500 |   2.68 MB |
| Dapper_AsyncStream           |  9.364 ms |  31.2500 |        - |        - |   2.59 MB |
| Dapper_Async                 |  9.520 ms |  31.2500 |  31.2500 |  31.2500 |   2.84 MB |
| Linq2Db_AsyncStream          |  9.704 ms |  15.6250 |        - |        - |   2.14 MB |
| Linq2Db_Compiled_ToList      |  9.791 ms |  31.2500 |  31.2500 |  31.2500 |   2.39 MB |
| Linq2Db_ToListAsync          |  9.892 ms |  31.2500 |  31.2500 |  31.2500 |   2.39 MB |
| EFCore_AsyncStream           |  9.921 ms |  46.8750 |        - |        - |   4.28 MB |
| EFCore_Compiled_AsyncStream  |  9.973 ms |  46.8750 |        - |        - |   4.28 MB |
| EFCore_ToListAsync           | 11.564 ms |  62.5000 |  46.8750 |  31.2500 |   4.53 MB |
