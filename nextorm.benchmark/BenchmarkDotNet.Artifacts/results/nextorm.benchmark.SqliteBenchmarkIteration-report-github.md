```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Mean     | Error    | StdDev   | Gen0   | Allocated |
|---------------------- |---------:|---------:|---------:|-------:|----------:|
| NextormCompiledStream | 35.33 μs | 0.702 μs | 0.937 μs | 0.3052 |   1.46 KB |
| NextormCompiledToList | 41.57 μs | 0.580 μs | 0.514 μs | 0.3662 |   1.73 KB |
| NextormCachedToList   | 43.79 μs | 0.290 μs | 0.272 μs | 0.6104 |   2.98 KB |
