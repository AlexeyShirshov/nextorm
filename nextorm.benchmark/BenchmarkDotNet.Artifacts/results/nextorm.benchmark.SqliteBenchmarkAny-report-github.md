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
| NextormCompiled | 31.74 μs | 0.342 μs | 0.320 μs | 0.2441 |      - |    1264 B |
| NextormCached   | 36.33 μs | 0.573 μs | 0.536 μs | 0.4272 |      - |    2288 B |
| Dapper          | 38.83 μs | 0.698 μs | 0.653 μs | 0.1221 |      - |     816 B |
| EFCoreCompiled  | 54.16 μs | 0.766 μs | 0.717 μs | 1.0376 | 0.3052 |    5072 B |
| EFCore          | 64.59 μs | 1.286 μs | 1.202 μs | 1.4648 | 0.4883 |    7040 B |
