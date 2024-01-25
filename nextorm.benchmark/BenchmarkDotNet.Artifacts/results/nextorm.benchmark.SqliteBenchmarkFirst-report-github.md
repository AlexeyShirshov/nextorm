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
| Nextorm_Prepared_Scalar_FirstOrDefault        | 316.1 μs | 4.53 μs | 4.24 μs |  2.4414 |      - |  13.24 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 345.2 μs | 4.95 μs | 4.63 μs |  3.4180 |      - |  17.05 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 425.3 μs | 8.37 μs | 8.96 μs |  9.7656 |      - |  46.39 KB |
| Dapper_Entity_FirstOrDefault                  | 434.7 μs | 6.81 μs | 5.69 μs |  3.9063 |      - |  19.71 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 440.6 μs | 7.82 μs | 8.36 μs |  8.7891 |      - |  40.82 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 454.2 μs | 7.50 μs | 7.02 μs |  9.7656 |      - |  49.11 KB |
| Dapper_Scalar_FirstOrDefault                  | 466.9 μs | 8.02 μs | 7.50 μs |  3.4180 |      - |  16.95 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 548.8 μs | 8.73 μs | 8.17 μs | 11.7188 | 3.9063 |  54.34 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 589.7 μs | 9.35 μs | 8.74 μs | 11.7188 | 3.9063 |  57.34 KB |
