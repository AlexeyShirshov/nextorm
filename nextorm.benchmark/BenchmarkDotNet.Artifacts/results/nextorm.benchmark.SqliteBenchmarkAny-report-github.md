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
| NextormCompiled | 31.85 μs | 0.493 μs | 0.461 μs | 0.2441 |      - |    1264 B |
| NextormCached   | 35.20 μs | 0.684 μs | 0.703 μs | 0.4272 |      - |    2240 B |
| EFCore          | 65.04 μs | 1.239 μs | 1.159 μs | 1.4648 | 0.4883 |    7040 B |
| EFCoreCompiled  | 52.90 μs | 1.004 μs | 1.157 μs | 1.0376 | 0.3052 |    5072 B |
| Dapper          | 38.41 μs | 0.761 μs | 1.138 μs | 0.1221 |      - |     816 B |
