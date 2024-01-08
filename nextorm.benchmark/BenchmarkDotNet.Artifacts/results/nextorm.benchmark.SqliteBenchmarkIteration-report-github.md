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
| NextormCompiledToList | 41.08 μs | 0.723 μs | 0.676 μs | 0.4883 |      - |   2.39 KB |
| Dapper                | 42.28 μs | 0.543 μs | 0.481 μs | 0.3662 |      - |   1.88 KB |
| NextormCachedToList   | 45.99 μs | 0.899 μs | 0.883 μs | 0.8545 |      - |   4.08 KB |
| EFCore                | 63.65 μs | 0.969 μs | 0.859 μs | 1.8311 | 0.3662 |   8.63 KB |
