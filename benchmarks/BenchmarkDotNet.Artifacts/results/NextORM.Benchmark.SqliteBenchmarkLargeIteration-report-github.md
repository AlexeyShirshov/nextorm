```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                       | Mean      | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------------- |----------:|---------:|---------:|---------:|----------:|
| Nextorm_Prepared_AsyncStream |  7.921 ms | 265.6250 |        - |        - |   2.14 MB |
| Nextorm_Prepared_ToListAsync |  7.962 ms | 265.6250 | 156.2500 |        - |   2.21 MB |
| Nextorm_Cached_ToListAsync   |  7.992 ms | 265.6250 | 156.2500 |        - |   2.21 MB |
| Nextorm_Cached_AsyncStream   |  8.051 ms | 265.6250 |        - |        - |   2.14 MB |
| Dapper_AsyncStream           |  8.528 ms | 312.5000 |        - |        - |   2.59 MB |
| Linq2Db_AsyncStream          |  8.647 ms | 265.6250 |        - |        - |   2.14 MB |
| AdoTupleToList               |  8.894 ms | 281.2500 | 281.2500 | 281.2500 |   2.68 MB |
| EFCore_Compiled_AsyncStream  |  9.152 ms | 531.2500 |        - |        - |   4.28 MB |
| EFCore_AsyncStream           |  9.327 ms | 531.2500 |        - |        - |   4.28 MB |
| AdoWithDelegate              | 10.257 ms | 343.7500 | 265.6250 |  62.5000 |   2.39 MB |
| Linq2Db_Compiled_ToList      | 10.748 ms | 343.7500 | 265.6250 |  62.5000 |   2.39 MB |
| Dapper_Async                 | 10.988 ms | 375.0000 | 250.0000 |  78.1250 |   2.85 MB |
| Linq2Db_ToListAsync          | 11.012 ms | 343.7500 | 265.6250 |  62.5000 |   2.39 MB |
| EFCore_ToListAsync           | 12.998 ms | 625.0000 | 406.2500 |  93.7500 |   4.53 MB |
