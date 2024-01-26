```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                                        | Mean     | Error    | StdDev   | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |---------:|---------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        | 311.8 μs |  3.44 μs |  3.22 μs |  2.4414 |      - |  13.24 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 341.9 μs |  4.01 μs |  3.75 μs |  3.4180 |      - |  17.05 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 422.6 μs |  6.81 μs |  6.37 μs |  9.7656 |      - |  46.86 KB |
| Dapper_Scalar_FirstOrDefault                  | 441.2 μs |  3.77 μs |  3.15 μs |  3.4180 |      - |  16.95 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 460.3 μs |  8.55 μs |  8.78 μs | 10.7422 |      - |  49.58 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 497.3 μs |  5.18 μs |  4.59 μs |  8.7891 |      - |  41.29 KB |
| Dapper_Entity_FirstOrDefault                  | 497.6 μs |  4.88 μs |  4.56 μs |  3.9063 |      - |  19.71 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 549.9 μs |  9.53 μs |  8.45 μs | 11.7188 | 3.9063 |  54.34 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 595.8 μs | 11.34 μs | 11.13 μs | 11.7188 | 3.9063 |  57.34 KB |
