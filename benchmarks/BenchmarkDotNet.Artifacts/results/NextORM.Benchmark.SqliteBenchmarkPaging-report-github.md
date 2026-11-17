```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                       | Categories        | Mean      | Gen0   | Gen1   | Allocated |
|----------------------------- |------------------ |----------:|-------:|-------:|----------:|
| A_Nextorm_Prepared_PageAsync | A_Paging          |  89.99 μs | 1.5869 |      - |  13.95 KB |
| A_Linq2Db_Compiled_Page      | A_Paging          | 107.29 μs | 2.0752 |      - |   17.6 KB |
| A_EFCore_Compiled_PageAsync  | A_Paging          | 128.99 μs | 5.3711 | 0.9766 |  44.37 KB |
|                              |                   |           |        |        |           |
| A_Dapper_PageAsync           | A_Paging,B_Paging | 101.65 μs | 2.1973 |      - |  18.14 KB |
|                              |                   |           |        |        |           |
| B_Nextorm_Cached_PageAsync   | B_Paging          | 100.22 μs | 2.3193 |      - |   19.4 KB |
| B_Linq2Db_PageAsync          | B_Paging          | 135.85 μs | 2.6855 |      - |  22.71 KB |
| B_EFCore_PageAsync           | B_Paging          | 183.08 μs | 6.8359 | 1.2207 |  56.28 KB |
