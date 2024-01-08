```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                    | Mean     | Error    | StdDev   | Gen0    | Gen1   | Allocated |
|-------------------------- |---------:|---------:|---------:|--------:|-------:|----------:|
| DapperLargeFirst          | 456.8 μs |  8.97 μs | 11.66 μs |  3.9063 |      - |  19.63 KB |
| NextormLargeFirstCompiled | 499.2 μs |  7.57 μs |  7.08 μs |  4.8828 |      - |   25.1 KB |
| NextormFirst              | 582.6 μs | 10.55 μs |  8.81 μs | 13.6719 |      - |  64.58 KB |
| EFCoreLargeFirstCompiled  | 589.6 μs |  8.55 μs |  8.00 μs | 11.7188 | 3.9063 |  57.34 KB |
| NextormFirstParam         | 627.3 μs | 12.13 μs | 12.46 μs | 11.7188 |      - |  55.18 KB |
