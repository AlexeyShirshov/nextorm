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
| NextormCompiled | 40.01 μs | 0.667 μs | 0.624 μs | 0.3662 |      - |    1952 B |
| NextormCached   | 47.85 μs | 0.956 μs | 0.981 μs | 0.8545 |      - |    4153 B |
| EFCore          | 66.80 μs | 1.227 μs | 1.837 μs | 1.4648 | 0.4883 |    7040 B |
| EFCoreCompiled  | 54.35 μs | 0.773 μs | 0.723 μs | 1.0376 | 0.3052 |    5072 B |
| Dapper          | 41.79 μs | 0.669 μs | 0.657 μs | 0.1221 |      - |     848 B |
