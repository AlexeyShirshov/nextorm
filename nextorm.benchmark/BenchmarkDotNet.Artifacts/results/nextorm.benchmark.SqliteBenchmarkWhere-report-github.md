```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                | Mean     | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|---------------------- |---------:|------:|---------:|--------:|-----------:|------------:|
| NextormCompiledToList | 3.056 ms |  0.99 |  27.3438 |       - |  133.26 KB |        1.01 |
| NextormCompiled       | 3.078 ms |  1.00 |  27.3438 |       - |  132.11 KB |        1.00 |
| NextormCompiledAsync  | 3.085 ms |  1.00 |  23.4375 |       - |  125.08 KB |        0.95 |
| NextormCachedParam    | 3.354 ms |  1.09 |  31.2500 |  3.9063 |     144 KB |        1.09 |
| NextormCachedToList   | 3.910 ms |  1.27 |  31.2500 |       - |  145.06 KB |        1.10 |
| DapperUnbuffered      | 3.993 ms |  1.30 |  39.0625 |       - |  208.67 KB |        1.58 |
| Dapper                | 3.997 ms |  1.30 |  39.0625 |       - |  185.39 KB |        1.40 |
| NextormCached         | 4.462 ms |  1.45 | 125.0000 |       - |  593.86 KB |        4.50 |
| EFCoreCompiled        | 5.399 ms |  1.75 | 109.3750 | 31.2500 |  534.38 KB |        4.04 |
| EFCoreStream          | 8.716 ms |  2.83 | 218.7500 | 31.2500 | 1060.78 KB |        8.03 |
| EFCore                | 8.830 ms |  2.87 | 218.7500 | 31.2500 | 1086.32 KB |        8.22 |
