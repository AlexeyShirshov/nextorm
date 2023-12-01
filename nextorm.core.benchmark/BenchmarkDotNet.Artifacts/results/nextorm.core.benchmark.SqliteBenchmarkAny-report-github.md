```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method          | Mean     | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|---------------- |---------:|---------:|---------:|-------:|-------:|----------:|
| NextormCompiled | 39.34 μs | 0.780 μs | 0.729 μs | 0.3662 |      - |    1952 B |
| NextormCached   | 47.53 μs | 0.785 μs | 0.735 μs | 0.8545 |      - |    4145 B |
| EFCore          | 63.03 μs | 0.772 μs | 0.685 μs | 1.4648 | 0.4883 |    7040 B |
| EFCoreCompiled  | 51.89 μs | 0.839 μs | 0.744 μs | 1.0376 | 0.3052 |    5072 B |
| Dapper          | 41.91 μs | 0.827 μs | 1.359 μs | 0.1221 |      - |     848 B |
