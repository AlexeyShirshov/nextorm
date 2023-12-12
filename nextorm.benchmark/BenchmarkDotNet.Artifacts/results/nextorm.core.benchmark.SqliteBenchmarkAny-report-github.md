```

BenchmarkDotNet v0.13.10, Windows 10 (10.0.19045.3636/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method          | Mean     | Error   | StdDev  | Gen0   | Allocated |
|---------------- |---------:|--------:|--------:|-------:|----------:|
| NextormCompiled | 135.6 μs | 2.71 μs | 4.45 μs | 0.7324 |    2024 B |
| NextormCached   | 145.5 μs | 2.42 μs | 4.49 μs | 1.7090 |    3849 B |
| EFCore          | 170.1 μs | 3.31 μs | 3.95 μs | 2.9297 |    7040 B |
| EFCoreCompiled  | 150.7 μs | 2.16 μs | 3.48 μs | 2.1973 |    5072 B |
| Dapper          | 131.3 μs | 2.43 μs | 2.28 μs | 0.2441 |     848 B |
