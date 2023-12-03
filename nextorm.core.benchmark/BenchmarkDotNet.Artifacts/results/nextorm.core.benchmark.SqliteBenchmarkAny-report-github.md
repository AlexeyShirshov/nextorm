```

BenchmarkDotNet v0.13.10, Windows 10 (10.0.19045.3570/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method          | Mean     | Error   | StdDev  | Gen0   | Allocated |
|---------------- |---------:|--------:|--------:|-------:|----------:|
| NextormCompiled | 128.2 μs | 2.37 μs | 2.22 μs | 0.7324 |    1952 B |
| NextormCached   | 142.7 μs | 1.73 μs | 1.62 μs | 1.9531 |    4154 B |
| EFCore          | 169.4 μs | 3.27 μs | 3.06 μs | 2.9297 |    7040 B |
| EFCoreCompiled  | 149.1 μs | 2.78 μs | 2.46 μs | 2.1973 |    5072 B |
| Dapper          | 130.2 μs | 1.80 μs | 1.69 μs | 0.2441 |     848 B |
