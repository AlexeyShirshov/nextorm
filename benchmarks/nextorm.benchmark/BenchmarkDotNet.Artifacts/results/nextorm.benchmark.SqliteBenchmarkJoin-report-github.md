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
| Nextorm_Prepared |   348.4 μs |  6.84 μs |  7.60 μs |  3.4180 |      - |  17.77 KB |
| Dapper           |   517.4 μs |  7.64 μs |  7.14 μs |  3.9063 |      - |  21.92 KB |
| Nextorm_Cached   |   598.1 μs |  8.14 μs |  7.22 μs | 21.4844 |      - |    100 KB |
| EFCore_Compiled  |   726.9 μs | 11.24 μs | 10.51 μs | 20.5078 | 4.8828 |  95.24 KB |
| EFCore           | 1,066.4 μs | 20.44 μs | 22.72 μs | 31.2500 | 3.9063 | 150.87 KB |
