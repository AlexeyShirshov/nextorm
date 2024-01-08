```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                    | Mean     | Error   | StdDev  | Gen0    | Gen1   | Allocated |
|-------------------------- |---------:|--------:|--------:|--------:|-------:|----------:|
| NextormLargeFirstCompiled | 337.4 μs | 5.48 μs | 5.12 μs |  3.4180 |      - |  17.05 KB |
| DapperLargeFirst          | 432.2 μs | 7.11 μs | 6.65 μs |  3.9063 |      - |  19.63 KB |
| NextormFirst              | 468.4 μs | 9.21 μs | 9.86 μs | 11.7188 |      - |  56.69 KB |
| NextormFirstParam         | 511.5 μs | 8.57 μs | 8.01 μs |  9.7656 |      - |  47.14 KB |
| EFCoreLargeFirstCompiled  | 586.9 μs | 7.96 μs | 7.06 μs | 11.7188 | 3.9063 |  57.34 KB |
