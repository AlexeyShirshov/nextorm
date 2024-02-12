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
| Nextorm_Prepared_Scalar_FirstOrDefault        | 319.4 μs |  6.09 μs |  5.70 μs |  2.4414 |      - |  13.24 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 346.0 μs |  4.33 μs |  3.84 μs |  3.4180 |      - |  17.05 KB |
| Dapper_Scalar_FirstOrDefault                  | 415.7 μs |  7.95 μs |  7.44 μs |  3.4180 |      - |  16.95 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 429.5 μs |  8.51 μs | 10.13 μs | 10.7422 |      - |  49.44 KB |
| Dapper_Entity_FirstOrDefault                  | 443.1 μs |  7.24 μs |  6.77 μs |  3.9063 |      - |  19.71 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 447.2 μs |  8.89 μs | 11.24 μs |  8.7891 |      - |  43.25 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 458.3 μs |  9.04 μs |  9.67 μs | 10.7422 |      - |  51.61 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 555.8 μs |  8.11 μs |  7.58 μs | 11.7188 | 3.9063 |  54.34 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 598.4 μs | 11.96 μs | 11.75 μs | 11.7188 | 3.9063 |  57.34 KB |
