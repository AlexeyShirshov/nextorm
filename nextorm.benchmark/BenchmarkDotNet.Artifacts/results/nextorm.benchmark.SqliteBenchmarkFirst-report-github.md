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
| Nextorm_Prepared_Scalar_FirstOrDefault        | 319.7 μs |  6.02 μs |  5.63 μs |  2.4414 |      - |  13.24 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 335.9 μs |  6.08 μs |  5.69 μs |  3.4180 |      - |  17.05 KB |
| Dapper_Scalar_FirstOrDefault                  | 415.0 μs |  8.24 μs | 11.27 μs |  3.4180 |      - |  16.95 KB |
| Dapper_Entity_FirstOrDefault                  | 437.6 μs |  7.40 μs |  6.92 μs |  3.9063 |      - |  19.63 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 473.7 μs |  8.95 μs | 15.44 μs |  9.7656 |      - |  47.37 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 477.7 μs |  8.88 μs |  8.72 μs | 11.7188 |      - |  55.52 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 497.4 μs |  9.85 μs | 13.15 μs | 11.7188 |      - |  54.21 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 550.0 μs | 10.95 μs | 11.25 μs | 11.7188 | 3.9063 |  54.34 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 591.1 μs | 11.16 μs | 11.46 μs | 11.7188 | 3.9063 |  57.34 KB |
