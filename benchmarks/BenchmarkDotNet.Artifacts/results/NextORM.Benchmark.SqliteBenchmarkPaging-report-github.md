```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Job=Job-JWQWGO  Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                       | Categories        | Mean      | Median    | Gen0   | Gen1   | Allocated |
|----------------------------- |------------------ |----------:|----------:|-------:|-------:|----------:|
| A_Nextorm_Prepared_PageAsync | A_Paging          |  90.91 μs |  90.84 μs | 1.5869 |      - |  13.95 KB |
| A_Linq2Db_Compiled_Page      | A_Paging          | 124.58 μs | 121.79 μs | 2.0752 |      - |   17.6 KB |
| A_EFCore_Compiled_PageAsync  | A_Paging          | 131.03 μs | 130.72 μs | 5.3711 | 0.9766 |  44.37 KB |
|                              |                   |           |           |        |        |           |
| A_Dapper_PageAsync           | A_Paging,B_Paging | 112.87 μs | 108.63 μs | 2.1973 |      - |  18.14 KB |
|                              |                   |           |           |        |        |           |
| B_Nextorm_Cached_PageAsync   | B_Paging          | 118.32 μs | 114.62 μs | 2.1973 |      - |  19.43 KB |
| B_Linq2Db_PageAsync          | B_Paging          | 149.24 μs | 147.56 μs | 2.4414 |      - |   22.6 KB |
| B_EFCore_PageAsync           | B_Paging          | 215.39 μs | 213.59 μs | 6.8359 | 0.9766 |  56.39 KB |
