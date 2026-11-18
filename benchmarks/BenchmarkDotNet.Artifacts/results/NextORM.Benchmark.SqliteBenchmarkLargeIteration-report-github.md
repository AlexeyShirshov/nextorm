```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                       | Mean      | Median    | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------------- |----------:|----------:|---------:|---------:|---------:|----------:|
| Nextorm_Cached_ToListAsync   |  8.030 ms |  8.029 ms | 265.6250 | 156.2500 |        - |   2.21 MB |
| Nextorm_Prepared_AsyncStream |  8.044 ms |  8.049 ms | 265.6250 |        - |        - |   2.14 MB |
| Nextorm_Prepared_ToListAsync |  8.147 ms |  8.126 ms | 265.6250 | 156.2500 |        - |   2.21 MB |
| Nextorm_Cached_AsyncStream   |  8.369 ms |  8.299 ms | 265.6250 |        - |        - |   2.14 MB |
| Dapper_AsyncStream           |  8.799 ms |  8.774 ms | 312.5000 |        - |        - |   2.59 MB |
| AdoTupleToList               |  9.095 ms |  9.090 ms | 281.2500 | 281.2500 | 281.2500 |   2.68 MB |
| Linq2Db_AsyncStream          |  9.140 ms |  9.117 ms | 265.6250 |        - |        - |   2.14 MB |
| EFCore_AsyncStream           |  9.611 ms |  9.597 ms | 531.2500 |        - |        - |   4.28 MB |
| EFCore_Compiled_AsyncStream  |  9.991 ms |  9.892 ms | 531.2500 |        - |        - |   4.28 MB |
| Linq2Db_ToListAsync          | 11.864 ms | 11.755 ms | 343.7500 | 265.6250 |  62.5000 |   2.39 MB |
| EFCore_ToListAsync           | 12.949 ms | 12.945 ms | 625.0000 | 406.2500 |  93.7500 |   4.53 MB |
| Dapper_Async                 | 13.232 ms | 12.707 ms | 390.6250 | 265.6250 |  78.1250 |   2.85 MB |
| AdoWithDelegate              | 13.473 ms | 11.685 ms | 343.7500 | 250.0000 |  62.5000 |   2.39 MB |
| Linq2Db_Compiled_ToList      | 15.062 ms | 12.074 ms | 343.7500 | 250.0000 |  62.5000 |   2.39 MB |
