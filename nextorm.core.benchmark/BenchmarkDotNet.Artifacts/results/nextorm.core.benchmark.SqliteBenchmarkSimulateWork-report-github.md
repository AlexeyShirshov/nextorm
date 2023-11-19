```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 7.0 : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


```
| Method                        | Mean        | Ratio | Gen0       | Gen1       | Allocated | Alloc Ratio |
|------------------------------ |------------:|------:|-----------:|-----------:|----------:|------------:|
| NextormCompiledToList         |   218.99 ms |  1.00 |  2333.3333 |          - |  11.65 MB |        1.00 |
| NextormCachedToList           |   340.60 ms |  1.58 |  7000.0000 |          - |  33.44 MB |        2.87 |
| NextormCachedWithParamsToList |   218.40 ms |  1.01 |  2333.3333 |          - |  11.67 MB |        1.00 |
| EFCore                        | 1,791.66 ms |  8.41 | 18000.0000 | 17000.0000 |  81.49 MB |        6.99 |
| EFCoreStream                  | 1,486.71 ms |  6.62 | 14000.0000 | 13000.0000 |  65.18 MB |        5.59 |
| EFCoreCompiled                |    64.87 ms |  0.30 |  2625.0000 |          - |  12.16 MB |        1.04 |
| Dapper                        |   240.20 ms |  1.11 |  2333.3333 |          - |  11.48 MB |        0.99 |
|                               |             |       |            |            |           |             |
| NextormCompiledToList         |   208.44 ms |     ? |  2500.0000 |          - |  11.65 MB |           ? |
| NextormCachedToList           |   318.99 ms |     ? |  7000.0000 |          - |  33.45 MB |           ? |
| NextormCachedWithParamsToList |   206.38 ms |     ? |  2500.0000 |          - |  11.67 MB |           ? |
| EFCore                        | 1,598.29 ms |     ? | 18000.0000 | 17000.0000 |  81.27 MB |           ? |
| EFCoreStream                  | 1,254.23 ms |     ? | 14000.0000 | 13000.0000 |  65.53 MB |           ? |
| EFCoreCompiled                |    45.80 ms |     ? |  2636.3636 |          - |  12.16 MB |           ? |
| Dapper                        |   222.78 ms |     ? |  2500.0000 |          - |  11.48 MB |           ? |
