```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                     | Categories  | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|--------------------------- |------------ |---------:|---------:|---------:|-------:|-------:|----------:|
| NextormCompiled            |             | 40.86 μs | 0.784 μs | 0.734 μs | 0.3662 |      - |    2000 B |
| NextormCached              |             | 55.23 μs | 0.864 μs | 0.766 μs | 1.4648 |      - |    7105 B |
| EFCore                     |             | 63.88 μs | 1.147 μs | 1.072 μs | 1.4648 | 0.4883 |    7040 B |
| EFCoreCompiled             |             | 52.05 μs | 0.802 μs | 0.711 μs | 1.0376 | 0.3052 |    5072 B |
| Dapper                     |             | 37.66 μs | 0.493 μs | 0.437 μs | 0.1221 |      - |     816 B |
| NextormFilterCompiled      | Filter      | 41.48 μs | 0.294 μs | 0.230 μs | 0.4272 |      - |    2016 B |
| NextormFilterCached        | Filter      | 60.55 μs | 1.175 μs | 1.154 μs | 1.5869 |      - |    7810 B |
| EFCoreFilter               | Filter      | 78.82 μs | 0.889 μs | 0.694 μs | 1.9531 | 0.4883 |    9320 B |
| EFCoreFilterCompiled       | Filter      | 53.85 μs | 0.495 μs | 0.463 μs | 1.0376 | 0.3052 |    5096 B |
| DapperFilter               | Filter      | 39.34 μs | 0.706 μs | 0.661 μs | 0.1221 |      - |     832 B |
| NextormFilterParamCompiled | FilterParam | 43.29 μs | 0.817 μs | 0.874 μs | 0.4272 |      - |    2248 B |
| NextormFilterParamCached   | FilterParam | 68.31 μs | 1.356 μs | 1.665 μs | 1.9531 |      - |   10178 B |
| EFCoreFilterParam          | FilterParam | 84.45 μs | 0.674 μs | 0.630 μs | 1.9531 | 0.4883 |    9889 B |
| EFCoreFilterParamCompiled  | FilterParam | 55.48 μs | 0.759 μs | 0.634 μs | 1.1597 | 0.3662 |    5592 B |
| DapperFilterParam          | FilterParam | 42.08 μs | 0.588 μs | 0.521 μs | 0.3052 |      - |    1496 B |
