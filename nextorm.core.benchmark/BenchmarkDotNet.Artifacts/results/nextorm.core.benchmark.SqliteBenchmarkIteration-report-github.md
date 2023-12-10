```

BenchmarkDotNet v0.13.10, Windows 10 (10.0.19045.3636/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                       | Mean     | Error   | StdDev  | Gen0   | Allocated |
|----------------------------- |---------:|--------:|--------:|-------:|----------:|
| NextormCompiledToList        | 134.0 μs | 2.53 μs | 2.71 μs | 0.9766 |   2.47 KB |
| NextormCompiledManualToList  | 134.0 μs | 2.53 μs | 3.87 μs | 0.9766 |   2.47 KB |
| NextormCachedToList          | 146.4 μs | 1.89 μs | 1.68 μs | 2.1973 |   4.76 KB |
| NextormManualSQLCachedToList | 146.7 μs | 2.85 μs | 2.93 μs | 2.1973 |   4.97 KB |
| Dapper                       | 137.9 μs | 1.97 μs | 1.84 μs | 0.7324 |   1.91 KB |
