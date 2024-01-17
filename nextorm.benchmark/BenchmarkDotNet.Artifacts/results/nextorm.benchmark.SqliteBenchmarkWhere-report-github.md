```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                     | Mean     | Ratio | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|--------------------------- |---------:|------:|---------:|--------:|-----------:|------------:|
| NextormCompiledStream      | 3.103 ms |  1.00 |  27.3438 |       - |  132.11 KB |        1.00 |
| NextormCompiledAsyncStream | 3.111 ms |  1.00 |  23.4375 |       - |  125.08 KB |        0.95 |
| NextormCompiledToList      | 3.144 ms |  1.01 |  27.3438 |       - |  133.26 KB |        1.01 |
| NextormCachedToList        | 3.456 ms |  1.11 |  31.2500 |  3.9063 |  144.96 KB |        1.10 |
| NextormCachedParamStream   | 3.985 ms |  1.28 |  31.2500 |       - |  143.89 KB |        1.09 |
| DapperUnbuffered           | 4.086 ms |  1.32 |  39.0625 |       - |  208.67 KB |        1.58 |
| Dapper                     | 4.223 ms |  1.34 |  39.0625 |       - |  185.39 KB |        1.40 |
| NextormCachedStream        | 4.409 ms |  1.42 | 125.0000 |       - |  574.33 KB |        4.35 |
| EFCoreCompiled             | 5.509 ms |  1.78 | 109.3750 | 31.2500 |  534.38 KB |        4.04 |
| EFCoreToList               | 8.935 ms |  2.88 | 218.7500 | 31.2500 | 1071.48 KB |        8.11 |
| EFCoreAsyncStream          | 8.975 ms |  2.91 | 218.7500 | 31.2500 | 1060.78 KB |        8.03 |
