```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                              | Mean      | Ratio | Gen0       | Gen1     | Allocated | Alloc Ratio |
|------------------------------------ |----------:|------:|-----------:|---------:|----------:|------------:|
| Nextorm_Prepared_ToListAsync        |  55.46 ms |  1.00 |   700.0000 | 100.0000 |   6.22 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync |  56.87 ms |  1.03 |   700.0000 | 100.0000 |   6.23 MB |        1.00 |
| EFCore_Compiled_ToListAsync         | 113.84 ms |  2.05 |  1600.0000 | 200.0000 |  13.57 MB |        2.18 |
| Linq2Db_ToListAsync                 | 392.47 ms |  7.08 | 13000.0000 |        - | 107.98 MB |       17.37 |
