```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Categories | Mean     | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|---------------------- |----------- |---------:|------:|---------:|--------:|-----------:|------------:|
| NextormCompiledToList | Buffered   | 3.940 ms |     ? |  46.8750 |       - |  228.57 KB |           ? |
| NextormCachedToList   | Buffered   | 4.027 ms |     ? |  46.8750 |       - |  233.86 KB |           ? |
| EFCore                | Buffered   | 8.743 ms |     ? | 218.7500 | 31.2500 | 1071.48 KB |           ? |
| Dapper                | Buffered   | 3.986 ms |     ? |  39.0625 |       - |  185.39 KB |           ? |
|                       |            |          |       |          |         |            |             |
| NextormCompiledAsync  | Stream     | 4.011 ms |  1.01 |  46.8750 |       - |  225.08 KB |        1.02 |
| NextormCompiled       | Stream     | 3.953 ms |  1.00 |  46.8750 |       - |  221.17 KB |        1.00 |
| NextormCachedParam    | Stream     | 4.069 ms |  1.03 |  46.8750 |       - |  226.46 KB |        1.02 |
| NextormCached         | Stream     | 5.260 ms |  1.33 | 156.2500 |       - |  725.89 KB |        3.28 |
| EFCoreStream          | Stream     | 8.574 ms |  2.16 | 218.7500 | 31.2500 | 1060.78 KB |        4.80 |
| EFCoreCompiled        | Stream     | 5.576 ms |  1.41 | 109.3750 | 31.2500 |  534.38 KB |        2.42 |
| DapperUnbuffered      | Stream     | 4.042 ms |  1.02 |  39.0625 |       - |  208.67 KB |        0.94 |
