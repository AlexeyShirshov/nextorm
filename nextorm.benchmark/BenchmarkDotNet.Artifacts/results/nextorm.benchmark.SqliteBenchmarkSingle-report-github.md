```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                           | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|--------------------------------- |---------:|---------:|---------:|------:|--------:|--------:|-------:|----------:|------------:|
| Nextorm_Prepared_SingleOrDefault | 305.5 μs |  5.85 μs |  5.47 μs |  1.00 |    0.00 |  2.4414 |      - |  12.81 KB |        1.00 |
| Dapper_SingleOrDefault           | 400.7 μs |  7.82 μs |  9.01 μs |  1.31 |    0.04 |  3.4180 |      - |  16.87 KB |        1.32 |
| Nextorm_Cached_SingleOrDefault   | 434.3 μs |  8.33 μs | 10.54 μs |  1.42 |    0.04 | 10.7422 |      - |  53.78 KB |        4.20 |
| EFCore_Compiled_SingleOrDefault  | 551.2 μs | 10.34 μs | 11.49 μs |  1.80 |    0.05 | 11.7188 | 3.9063 |  54.34 KB |        4.24 |
| EFCore_SingleOrDefault           | 878.0 μs | 17.36 μs | 19.30 μs |  2.87 |    0.10 | 23.4375 | 5.8594 | 108.12 KB |        8.44 |
