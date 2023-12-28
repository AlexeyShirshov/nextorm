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
| NextormCompiledToList | 33.44 μs | 0.662 μs | 0.883 μs | 0.3662 |      - |   1.73 KB |
| NextormCachedToList   | 40.38 μs | 0.783 μs | 0.932 μs | 0.7324 |      - |   3.76 KB |
| EFCore                | 63.15 μs | 1.246 μs | 1.280 μs | 1.8311 | 0.3662 |   8.63 KB |
| Dapper                | 42.14 μs | 0.816 μs | 0.907 μs | 0.3662 |      - |   1.88 KB |
