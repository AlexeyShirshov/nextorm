```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                                        | Mean     | Error   | StdDev  | Gen0    | Gen1   | Allocated |
|---------------------------------------------- |---------:|--------:|--------:|--------:|-------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        | 322.1 μs | 6.19 μs | 7.37 μs |  2.4414 |      - |  13.24 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 396.0 μs | 3.66 μs | 3.24 μs |  3.4180 |      - |  17.05 KB |
| Dapper_Scalar_FirstOrDefault                  | 412.2 μs | 6.99 μs | 6.20 μs |  3.4180 |      - |  16.95 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 455.5 μs | 7.37 μs | 6.53 μs |  9.7656 |      - |  47.37 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 466.0 μs | 8.33 μs | 7.38 μs | 11.7188 |      - |  55.52 KB |
| Dapper_Entity_FirstOrDefault                  | 496.2 μs | 4.66 μs | 4.13 μs |  3.9063 |      - |  19.63 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 501.9 μs | 9.14 μs | 8.55 μs | 11.7188 |      - |  54.21 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 547.4 μs | 4.71 μs | 4.17 μs | 11.7188 | 3.9063 |  54.34 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 593.0 μs | 5.69 μs | 5.04 μs | 11.7188 | 3.9063 |  57.34 KB |
