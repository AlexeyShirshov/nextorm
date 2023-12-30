```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|---------------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| NextormCompiledToList | 35.04 μs | 0.643 μs | 0.570 μs | 0.3662 |      - |   1.73 KB |
| NextormCachedToList   | 38.22 μs | 0.741 μs | 0.793 μs | 0.7324 |      - |   3.41 KB |
| EFCore                | 64.18 μs | 1.260 μs | 1.500 μs | 1.8311 | 0.3662 |   8.63 KB |
| Dapper                | 42.79 μs | 0.772 μs | 0.858 μs | 0.3662 |      - |   1.88 KB |
