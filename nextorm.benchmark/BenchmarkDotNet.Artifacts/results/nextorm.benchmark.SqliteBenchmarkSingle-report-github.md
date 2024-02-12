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
| Nextorm_Prepared_SingleOrDefault | 329.1 μs |  6.24 μs |  5.83 μs |  1.00 |    0.00 |  2.4414 |      - |  12.81 KB |        1.00 |
| Nextorm_Cached_SingleOrDefault   | 445.5 μs |  8.80 μs | 12.33 μs |  1.36 |    0.04 |  9.7656 |      - |  49.02 KB |        3.83 |
| Dapper_SingleOrDefault           | 481.8 μs |  9.53 μs | 13.04 μs |  1.46 |    0.05 |  2.9297 |      - |  16.87 KB |        1.32 |
| EFCore_Compiled_SingleOrDefault  | 576.0 μs | 11.29 μs | 11.09 μs |  1.75 |    0.05 | 11.7188 | 3.9063 |  54.34 KB |        4.24 |
| EFCore_SingleOrDefault           | 942.5 μs | 18.45 μs | 21.25 μs |  2.86 |    0.07 | 23.4375 | 5.8594 | 108.12 KB |        8.44 |
