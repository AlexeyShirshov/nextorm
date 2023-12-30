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
| NextormLargeFirstCompiled | 352.0 μs |  6.27 μs |  5.86 μs |  3.4180 |      - |  17.05 KB |
| NextormFirstParam         | 491.7 μs |  9.75 μs | 13.01 μs |  9.7656 |      - |  49.32 KB |
| NextormFirst              | 492.0 μs |  6.09 μs |  5.08 μs | 12.6953 |      - |  58.88 KB |
| EFCoreLargeFirstCompiled  | 601.1 μs | 11.20 μs | 10.48 μs | 11.7188 | 3.9063 |  57.34 KB |
| DapperLargeFirst          | 439.6 μs |  7.96 μs |  7.44 μs |  3.9063 |      - |  19.63 KB |
