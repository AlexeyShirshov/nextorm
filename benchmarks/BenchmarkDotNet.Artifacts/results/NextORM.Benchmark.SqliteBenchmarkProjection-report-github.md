```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                         | Categories                | Mean     | Median   | Gen0     | Gen1     | Gen2    | Allocated |
|------------------------------- |-------------------------- |---------:|---------:|---------:|---------:|--------:|----------:|
| A_Nextorm_Prepared_ToListAsync | A_Projection              | 4.433 ms | 4.409 ms | 156.2500 | 109.3750 |       - |    1.3 MB |
| A_EFCore_Compiled_ToListAsync  | A_Projection              | 4.979 ms | 4.960 ms | 414.0625 |        - |       - |   3.36 MB |
| A_Linq2Db_Compiled_ToList      | A_Projection              | 6.528 ms | 6.451 ms | 218.7500 | 210.9375 | 54.6875 |   1.47 MB |
|                                |                           |          |          |          |          |         |           |
| A_Dapper_ToListAsync           | A_Projection,B_Projection | 7.363 ms | 7.159 ms | 250.0000 | 234.3750 | 62.5000 |    1.7 MB |
|                                |                           |          |          |          |          |         |           |
| B_Nextorm_Cached_ToListAsync   | B_Projection              | 4.401 ms | 4.302 ms | 156.2500 | 109.3750 |       - |    1.3 MB |
| B_Linq2Db_ToListAsync          | B_Projection              | 6.069 ms | 5.979 ms | 218.7500 | 210.9375 | 54.6875 |   1.48 MB |
| B_EFCore_ToListAsync           | B_Projection              | 9.262 ms | 9.230 ms | 515.6250 | 312.5000 | 78.1250 |   3.62 MB |
