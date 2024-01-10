```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Mean     | Error    | StdDev   | Gen0   | Allocated |
|---------------------- |---------:|---------:|---------:|-------:|----------:|
| NextormCompiledToList | 34.27 μs | 0.358 μs | 0.317 μs | 0.3662 |   1.73 KB |
| NextormCompiledStream | 39.88 μs | 0.676 μs | 0.830 μs | 0.3052 |   1.46 KB |
| NextormCachedToList   | 42.04 μs | 0.413 μs | 0.345 μs | 0.6104 |   2.98 KB |
