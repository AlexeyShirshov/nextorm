```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Categories | Mean     | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|---------------------- |----------- |---------:|------:|---------:|--------:|-----------:|------------:|
| NextormCompiledToList | Buffered   | 3.227 ms |     ? |  27.3438 |       - |  140.29 KB |           ? |
| NextormCachedToList   | Buffered   | 3.513 ms |     ? |  31.2500 |       - |  151.87 KB |           ? |
| Dapper                | Buffered   | 4.299 ms |     ? |  39.0625 |       - |  185.39 KB |           ? |
| EFCore                | Buffered   | 9.181 ms |     ? | 218.7500 | 31.2500 | 1071.47 KB |           ? |
|                       |            |          |       |          |         |            |             |
| DapperUnbuffered      | Stream     | 4.088 ms |  0.98 |  39.0625 |       - |  208.67 KB |        0.93 |
| NextormCompiledAsync  | Stream     | 4.097 ms |  0.98 |  46.8750 |       - |  228.99 KB |        1.02 |
| NextormCompiled       | Stream     | 4.165 ms |  1.00 |  46.8750 |       - |  225.08 KB |        1.00 |
| NextormCachedParam    | Stream     | 4.699 ms |  1.13 |  46.8750 |       - |  235.92 KB |        1.05 |
| EFCoreCompiled        | Stream     | 5.579 ms |  1.34 | 109.3750 | 31.2500 |  534.38 KB |        2.37 |
| NextormCached         | Stream     | 5.612 ms |  1.34 | 148.4375 |       - |  686.82 KB |        3.05 |
| EFCoreStream          | Stream     | 9.203 ms |  2.20 | 218.7500 | 31.2500 |  1072.5 KB |        4.76 |
