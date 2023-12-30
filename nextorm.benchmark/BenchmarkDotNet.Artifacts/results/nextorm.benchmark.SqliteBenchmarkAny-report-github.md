```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method          | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|---------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| NextormCached   | 36.78 μs | 0.341 μs | 0.319 μs | 0.4272 |      - |    2272 B |
| NextormCompiled | 37.40 μs | 0.564 μs | 0.528 μs | 0.2441 |      - |    1264 B |
| Dapper          | 38.98 μs | 0.745 μs | 0.969 μs | 0.1221 |      - |     816 B |
| EFCoreCompiled  | 54.38 μs | 1.013 μs | 1.084 μs | 1.0376 | 0.3052 |    5072 B |
| EFCore          | 65.24 μs | 1.090 μs | 0.966 μs | 1.4648 | 0.4883 |    7040 B |
