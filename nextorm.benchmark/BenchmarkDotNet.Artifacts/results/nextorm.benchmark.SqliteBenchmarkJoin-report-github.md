```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method           | Mean       | Error    | StdDev   | Gen0    | Gen1   | Allocated |
|----------------- |-----------:|---------:|---------:|--------:|-------:|----------:|
| Nextorm_Prepared |   395.7 μs |  6.61 μs |  6.18 μs |  3.4180 |      - |  17.77 KB |
| Dapper           |   472.9 μs |  9.23 μs |  9.48 μs |  4.3945 |      - |  21.92 KB |
| Nextorm_Cached   |   613.5 μs | 11.82 μs | 13.61 μs | 23.4375 |      - | 110.31 KB |
| EFCore_Compiled  |   717.0 μs | 13.93 μs | 17.61 μs | 20.5078 | 4.8828 |  95.23 KB |
| EFCore           | 1,030.8 μs | 18.27 μs | 15.25 μs | 31.2500 | 3.9063 | 150.87 KB |
