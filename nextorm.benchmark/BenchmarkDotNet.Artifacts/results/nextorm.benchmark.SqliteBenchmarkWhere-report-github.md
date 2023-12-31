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
| NextormCachedToList   | Buffered   | 3.430 ms |     ? |  31.2500 |  3.9063 |  152.01 KB |           ? |
| NextormCompiledToList | Buffered   | 3.700 ms |     ? |  27.3438 |       - |  140.29 KB |           ? |
| Dapper                | Buffered   | 4.057 ms |     ? |  39.0625 |       - |  185.39 KB |           ? |
| EFCore                | Buffered   | 8.771 ms |     ? | 218.7500 | 31.2500 | 1071.48 KB |           ? |
|                       |            |          |       |          |         |            |             |
| NextormCompiledAsync  | Stream     | 4.024 ms |  0.87 |  46.8750 |       - |  228.99 KB |        1.02 |
| NextormCachedParam    | Stream     | 4.554 ms |  0.99 |  46.8750 |       - |  236.05 KB |        1.05 |
| DapperUnbuffered      | Stream     | 4.597 ms |  1.00 |  39.0625 |       - |  208.67 KB |        0.93 |
| NextormCompiled       | Stream     | 4.601 ms |  1.00 |  46.8750 |       - |  225.08 KB |        1.00 |
| EFCoreCompiled        | Stream     | 5.356 ms |  1.16 | 109.3750 | 31.2500 |  534.38 KB |        2.37 |
| NextormCached         | Stream     | 6.077 ms |  1.32 | 140.6250 |       - |  664.17 KB |        2.95 |
| EFCoreStream          | Stream     | 8.792 ms |  1.91 | 218.7500 | 31.2500 | 1060.78 KB |        4.71 |
