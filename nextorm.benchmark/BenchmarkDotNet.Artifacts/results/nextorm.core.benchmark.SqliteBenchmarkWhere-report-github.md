```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Categories | Mean     | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|---------------------- |----------- |---------:|------:|---------:|---------:|-----------:|------------:|
| NextormCompiledToList | Buffered   | 4.091 ms |     ? |  46.8750 |        - |  236.39 KB |           ? |
| NextormCachedToList   | Buffered   | 4.545 ms |     ? |  46.8750 |        - |  243.02 KB |           ? |
| EFCore                | Buffered   | 8.957 ms |     ? | 218.7500 |  31.2500 | 1101.17 KB |           ? |
| Dapper                | Buffered   | 4.312 ms |     ? |  39.0625 |        - |  188.52 KB |           ? |
|                       |            |          |       |          |          |            |             |
| NextormCompiledAsync  | Stream     | 4.043 ms |  0.98 |  46.8750 |        - |  228.99 KB |        1.02 |
| NextormCompiled       | Stream     | 4.108 ms |  1.00 |  46.8750 |        - |  225.08 KB |        1.00 |
| NextormCachedParam    | Stream     | 4.422 ms |  1.08 |  46.8750 |        - |  231.94 KB |        1.03 |
| NextormCached         | Stream     | 6.958 ms |  1.69 | 210.9375 | 203.1250 |   979.6 KB |        4.35 |
| EFCoreStream          | Stream     | 8.578 ms |  2.08 | 218.7500 |  31.2500 | 1060.78 KB |        4.71 |
| EFCoreCompiled        | Stream     | 5.409 ms |  1.32 | 109.3750 |  31.2500 |  534.38 KB |        2.37 |
| DapperUnbuffered      | Stream     | 4.252 ms |  1.03 |  39.0625 |        - |   211.8 KB |        0.94 |
