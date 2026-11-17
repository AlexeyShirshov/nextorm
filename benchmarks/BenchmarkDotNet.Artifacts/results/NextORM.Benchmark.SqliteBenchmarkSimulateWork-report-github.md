```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                              | Mean       | Ratio | Gen0       | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|-----------:|---------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   7.893 ms |  0.15 |   765.6250 |        - |   6.21 MB |        1.00 |
| Dapper_AsyncStream                  |  24.616 ms |  0.45 |  1093.7500 |        - |   8.86 MB |        1.43 |
| Nextorm_Prepared_ToListAsync        |  54.250 ms |  1.00 |   700.0000 | 100.0000 |   6.22 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  54.394 ms |  1.00 |   700.0000 | 100.0000 |   6.23 MB |        1.00 |
| Nextorm_Cached_AsyncStream          |  57.458 ms |  1.06 |  3000.0000 |        - |  30.34 MB |        4.88 |
| Dapper_Async                        |  73.641 ms |  1.36 |  1000.0000 | 142.8571 |   8.87 MB |        1.43 |
| EFCore_Compiled_ToListAsync         | 108.639 ms |  2.00 |  1600.0000 | 200.0000 |  13.57 MB |        2.18 |
| Nextorm_Cached_ToList               | 115.117 ms |  2.12 |  3600.0000 | 200.0000 |  29.64 MB |        4.77 |
| Linq2Db_ToListAsync                 | 364.543 ms |  6.72 | 13000.0000 |        - | 108.02 MB |       17.37 |
