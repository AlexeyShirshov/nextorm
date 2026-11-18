```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                              | Mean       | Median     | Ratio | Gen0       | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|-----------:|------:|-----------:|---------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   8.425 ms |   8.395 ms |  0.15 |   765.6250 |        - |   6.21 MB |        1.00 |
| Dapper_AsyncStream                  |  24.088 ms |  24.064 ms |  0.43 |  1093.7500 |        - |   8.86 MB |        1.43 |
| Nextorm_PreparedForLoop_ToListAsync |  55.433 ms |  55.424 ms |  0.98 |   777.7778 | 111.1111 |   6.23 MB |        1.00 |
| Nextorm_Prepared_ToListAsync        |  56.366 ms |  56.256 ms |  1.00 |   777.7778 | 111.1111 |   6.22 MB |        1.00 |
| Nextorm_Cached_AsyncStream          |  59.271 ms |  58.372 ms |  1.05 |  3000.0000 |        - |  30.45 MB |        4.90 |
| Dapper_Async                        |  77.707 ms |  77.647 ms |  1.38 |  1000.0000 | 142.8571 |   8.87 MB |        1.43 |
| EFCore_Compiled_ToListAsync         | 113.061 ms | 113.033 ms |  2.01 |  1500.0000 |        - |  13.57 MB |        2.18 |
| Nextorm_Cached_ToList               | 118.760 ms | 118.601 ms |  2.11 |  3600.0000 | 200.0000 |  29.75 MB |        4.79 |
| Linq2Db_ToListAsync                 | 358.743 ms | 357.873 ms |  6.37 | 13000.0000 |        - | 107.98 MB |       17.37 |
