```

BenchmarkDotNet v0.13.10, Windows 10 (10.0.19045.3636/22H2/2022Update)
Intel Core i7-4500U CPU 1.80GHz (Haswell), 1 CPU, 4 logical and 2 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Categories | Mean     | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|---------------------- |----------- |---------:|------:|---------:|---------:|-----------:|------------:|
| NextormCompiledToList | Buffered   | 13.21 ms |     ? | 125.0000 |  62.5000 |   271.1 KB |           ? |
| NextormCachedToList   | Buffered   | 13.55 ms |     ? | 125.0000 |  62.5000 |  276.38 KB |           ? |
| EFCore                | Buffered   | 20.86 ms |     ? | 500.0000 |        - | 1071.53 KB |           ? |
| Dapper                | Buffered   | 13.60 ms |     ? |  78.1250 |        - |  188.52 KB |           ? |
|                       |            |          |       |          |          |            |             |
| NextormCompiledAsync  | Stream     | 13.47 ms |  1.02 | 109.3750 |        - |  253.91 KB |        1.02 |
| NextormCompiled       | Stream     | 13.21 ms |  1.00 | 109.3750 |        - |  250.01 KB |        1.00 |
| NextormCachedParam    | Stream     | 13.55 ms |  1.03 | 125.0000 |        - |  255.16 KB |        1.02 |
| NextormCached         | Stream     | 17.86 ms |  1.35 | 468.7500 | 156.2500 | 1015.52 KB |        4.06 |
| EFCoreStream          | Stream     | 20.73 ms |  1.57 | 500.0000 |        - | 1060.82 KB |        4.24 |
| EFCoreCompiled        | Stream     | 15.56 ms |  1.18 | 250.0000 |        - |   534.4 KB |        2.14 |
| DapperUnbuffered      | Stream     | 13.55 ms |  1.03 |  93.7500 |        - |   211.8 KB |        0.85 |
