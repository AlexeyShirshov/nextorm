```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                       | Mean     | Gen0     | Gen1     | Gen2     | Allocated |
|----------------------------- |---------:|---------:|---------:|---------:|----------:|
| Nextorm_Cached_ToListAsync   | 10.24 ms | 390.6250 | 296.8750 |        - |   2.22 MB |
| Nextorm_Prepared_ToListAsync | 10.33 ms | 390.6250 | 328.1250 |        - |   2.21 MB |
| AdoTupleToList               | 10.39 ms | 421.8750 | 281.2500 | 281.2500 |   2.68 MB |
| Nextorm_Cached_AsyncStream   | 10.47 ms | 468.7500 |        - |        - |   2.14 MB |
| Nextorm_Prepared_AsyncStream | 10.55 ms | 468.7500 |        - |        - |   2.14 MB |
| Dapper_AsyncStream           | 11.04 ms | 578.1250 |        - |        - |   2.59 MB |
| EFCore_Compiled_AsyncStream  | 11.65 ms | 875.0000 |        - |        - |   3.97 MB |
| EFCore_AsyncStream           | 11.70 ms | 875.0000 |        - |        - |   3.98 MB |
| AdoWithDelegate              | 12.44 ms | 375.0000 | 265.6250 | 125.0000 |   2.39 MB |
| Dapper_Async                 | 13.81 ms | 453.1250 | 265.6250 | 125.0000 |   2.84 MB |
| EFCore_ToListAsync           | 15.92 ms | 656.2500 | 312.5000 | 125.0000 |   4.23 MB |
