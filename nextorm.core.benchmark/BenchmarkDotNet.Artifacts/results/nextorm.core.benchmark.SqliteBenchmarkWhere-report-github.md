```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 7.0 : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


```
| Method                | Runtime  | Categories | Mean      | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|---------------------- |--------- |----------- |----------:|------:|---------:|--------:|-----------:|------------:|
| NextormCompiledToList | .NET 7.0 | Buffered   |  4.187 ms |     ? |  46.8750 |       - |  229.36 KB |           ? |
| NextormCachedToList   | .NET 7.0 | Buffered   |  4.531 ms |     ? |  46.8750 |       - |  235.67 KB |           ? |
| EFCore                | .NET 7.0 | Buffered   | 10.239 ms |     ? | 218.7500 | 46.8750 |  1061.3 KB |           ? |
| Dapper                | .NET 7.0 | Buffered   |  4.253 ms |     ? |  39.0625 |       - |  188.91 KB |           ? |
|                       |          |            |           |       |          |         |            |             |
| NextormCompiledAsync  | .NET 7.0 | Stream     |  4.218 ms |  1.02 |  46.8750 |       - |  226.64 KB |        1.02 |
| NextormCompiled       | .NET 7.0 | Stream     |  4.155 ms |  1.00 |  46.8750 |       - |  221.96 KB |        1.00 |
| NextormCached         | .NET 7.0 | Stream     |  4.388 ms |  1.05 |  46.8750 |       - |   228.5 KB |        1.03 |
| EFCoreStream          | .NET 7.0 | Stream     |  9.945 ms |  2.38 | 234.3750 | 46.8750 | 1080.29 KB |        4.87 |
| EFCoreCompiled        | .NET 7.0 | Stream     |  6.094 ms |  1.47 | 109.3750 | 31.2500 |  534.38 KB |        2.41 |
| DapperUnbuffered      | .NET 7.0 | Stream     |  4.221 ms |  1.01 |  39.0625 |       - |  209.46 KB |        0.94 |
|                       |          |            |           |       |          |         |            |             |
| NextormCompiledToList | .NET 8.0 | Buffered   |  4.049 ms |     ? |  46.8750 |       - |  229.35 KB |           ? |
| NextormCachedToList   | .NET 8.0 | Buffered   |  4.398 ms |     ? |  46.8750 |       - |  235.68 KB |           ? |
| EFCore                | .NET 8.0 | Buffered   |  8.627 ms |     ? | 218.7500 | 31.2500 | 1071.48 KB |           ? |
| Dapper                | .NET 8.0 | Buffered   |  3.949 ms |     ? |  39.0625 |       - |  185.39 KB |           ? |
|                       |          |            |           |       |          |         |            |             |
| NextormCompiledAsync  | .NET 8.0 | Stream     |  3.975 ms |     ? |  46.8750 |       - |  225.86 KB |           ? |
| NextormCompiled       | .NET 8.0 | Stream     |  3.991 ms |     ? |  46.8750 |       - |  221.96 KB |           ? |
| NextormCached         | .NET 8.0 | Stream     |  4.293 ms |     ? |  46.8750 |       - |  228.36 KB |           ? |
| EFCoreStream          | .NET 8.0 | Stream     |  8.948 ms |     ? | 218.7500 | 31.2500 | 1060.78 KB |           ? |
| EFCoreCompiled        | .NET 8.0 | Stream     |  5.119 ms |     ? | 109.3750 | 31.2500 |  534.38 KB |           ? |
| DapperUnbuffered      | .NET 8.0 | Stream     |  3.952 ms |     ? |  42.9688 |       - |  208.67 KB |           ? |
