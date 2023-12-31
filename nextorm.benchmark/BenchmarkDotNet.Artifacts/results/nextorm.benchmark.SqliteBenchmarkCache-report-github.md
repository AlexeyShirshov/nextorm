```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method    | Mean     | Error   | StdDev  | Gen0   | Gen1   | Allocated |
|---------- |---------:|--------:|--------:|-------:|-------:|----------:|
| NonCached | 318.0 μs | 6.13 μs | 7.75 μs | 3.4180 | 2.9297 |   17.1 KB |
| Cached    | 323.3 μs | 6.28 μs | 8.38 μs | 3.4180 | 2.9297 |  17.84 KB |
