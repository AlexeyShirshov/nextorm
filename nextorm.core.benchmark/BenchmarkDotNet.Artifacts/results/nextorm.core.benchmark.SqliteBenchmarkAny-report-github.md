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
| NextormCompiled | 40.75 μs | 0.680 μs | 0.602 μs | 0.4272 |      - |    2024 B |
| NextormCached   | 45.59 μs | 0.476 μs | 0.422 μs | 0.7935 |      - |    3849 B |
| EFCore          | 63.04 μs | 1.180 μs | 1.104 μs | 1.4648 | 0.4883 |    7040 B |
| EFCoreCompiled  | 53.03 μs | 0.885 μs | 0.828 μs | 1.0376 | 0.3052 |    5072 B |
| Dapper          | 41.22 μs | 0.823 μs | 1.126 μs | 0.1221 |      - |     848 B |
