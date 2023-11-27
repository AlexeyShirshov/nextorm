```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Categories | Mean     | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|---------------------- |----------- |---------:|------:|---------:|--------:|-----------:|------------:|
| NextormCompiledToList | Buffered   | 3.933 ms |     ? |  46.8750 |       - |  228.57 KB |           ? |
| NextormCachedToList   | Buffered   | 4.242 ms |     ? |  46.8750 |       - |  235.53 KB |           ? |
| EFCore                | Buffered   | 8.707 ms |     ? | 218.7500 | 31.2500 | 1071.48 KB |           ? |
| Dapper                | Buffered   | 3.992 ms |     ? |  39.0625 |       - |  185.39 KB |           ? |
|                       |            |          |       |          |         |            |             |
| NextormCompiledAsync  | Stream     | 3.967 ms |  1.02 |  46.8750 |       - |  225.08 KB |        1.02 |
| NextormCompiled       | Stream     | 3.898 ms |  1.00 |  46.8750 |       - |  221.17 KB |        1.00 |
| NextormCachedParam    | Stream     | 4.306 ms |  1.10 |  46.8750 |       - |  228.21 KB |        1.03 |
| NextormCached         | Stream     | 5.010 ms |  1.28 | 140.6250 |       - |     668 KB |        3.02 |
| EFCoreStream          | Stream     | 8.721 ms |  2.24 | 218.7500 | 31.2500 | 1060.78 KB |        4.80 |
| EFCoreCompiled        | Stream     | 5.392 ms |  1.38 | 109.3750 | 31.2500 |  534.38 KB |        2.42 |
| DapperUnbuffered      | Stream     | 4.077 ms |  1.05 |  39.0625 |       - |  208.67 KB |        0.94 |
