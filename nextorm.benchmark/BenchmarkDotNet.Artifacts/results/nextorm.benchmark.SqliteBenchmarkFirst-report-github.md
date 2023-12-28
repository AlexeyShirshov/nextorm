```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                    | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| NextormLargeFirstCompiled | 34.62 μs | 0.680 μs | 0.954 μs | 0.3662 |      - |   1.76 KB |
| NextormFirstCached        | 87.91 μs | 1.411 μs | 1.320 μs | 2.6855 | 1.2207 |  13.32 KB |
| EFCoreLargeFirstCompiled  | 57.03 μs | 0.658 μs | 0.616 μs | 1.0986 | 0.3662 |   5.27 KB |
| DapperLargeFirst          | 49.93 μs | 0.984 μs | 1.443 μs | 0.4272 |      - |   2.02 KB |
