```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                       | Mean     | Error    | StdDev   | Gen0   | Allocated |
|----------------------------- |---------:|---------:|---------:|-------:|----------:|
| NextormCompiledToList        | 42.22 μs | 0.842 μs | 0.787 μs | 0.4883 |   2.47 KB |
| NextormCompiledManualToList  | 42.26 μs | 0.739 μs | 0.655 μs | 0.4883 |   2.47 KB |
| NextormCachedToList          | 48.64 μs | 0.796 μs | 0.665 μs | 0.9766 |   4.56 KB |
| NextormManualSQLCachedToList | 49.49 μs | 0.979 μs | 1.435 μs | 0.9766 |   4.77 KB |
| Dapper                       | 44.21 μs | 0.865 μs | 0.888 μs | 0.3662 |   1.91 KB |
