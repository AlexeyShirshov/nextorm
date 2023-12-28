```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Categories | Mean     | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|---------------------- |----------- |---------:|------:|---------:|---------:|-----------:|------------:|
| NextormCompiledToList | Buffered   | 3.111 ms |     ? |  27.3438 |        - |  140.29 KB |           ? |
| NextormCachedToList   | Buffered   | 3.292 ms |     ? |  31.2500 |        - |  147.75 KB |           ? |
| EFCore                | Buffered   | 8.917 ms |     ? | 218.7500 |  31.2500 | 1071.48 KB |           ? |
| Dapper                | Buffered   | 4.584 ms |     ? |  39.0625 |        - |  185.39 KB |           ? |
|                       |            |          |       |          |          |            |             |
| NextormCompiledAsync  | Stream     | 4.007 ms |  0.99 |  46.8750 |        - |  228.99 KB |        1.02 |
| NextormCompiled       | Stream     | 3.988 ms |  1.00 |  46.8750 |        - |  225.08 KB |        1.00 |
| NextormCachedParam    | Stream     | 4.442 ms |  1.10 |  46.8750 |        - |  231.83 KB |        1.03 |
| NextormCached         | Stream     | 6.922 ms |  1.72 | 210.9375 | 203.1250 |   979.6 KB |        4.35 |
| EFCoreStream          | Stream     | 8.878 ms |  2.22 | 218.7500 |  31.2500 | 1060.78 KB |        4.71 |
| EFCoreCompiled        | Stream     | 5.436 ms |  1.35 | 109.3750 |  31.2500 |  534.38 KB |        2.37 |
| DapperUnbuffered      | Stream     | 4.067 ms |  1.01 |  39.0625 |        - |  208.67 KB |        0.93 |
