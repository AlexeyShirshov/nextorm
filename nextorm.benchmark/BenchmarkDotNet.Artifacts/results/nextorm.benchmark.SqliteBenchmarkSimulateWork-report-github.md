```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                              | Mean       | Ratio | Gen0      | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|------:|----------:|----------:|------------:|
| Nextorm_Prepared_AsyncStream        |   6.249 ms |  0.04 | 1382.8125 |   6.21 MB |        1.00 |
| Dapper_AsyncStream                  |  28.495 ms |  0.18 | 3125.0000 |  14.08 MB |        2.27 |
| Nextorm_Cached_AsyncStream          |  51.402 ms |  0.33 | 6500.0000 |  31.05 MB |        5.00 |
| Nextorm_Prepared_ToListAsync        | 156.767 ms |  1.00 | 1250.0000 |   6.21 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync | 158.188 ms |  1.01 | 1250.0000 |   6.23 MB |        1.00 |
| Dapper_Async                        | 215.332 ms |  1.36 | 3000.0000 |  14.09 MB |        2.27 |
| Nextorm_Cached_ToList               | 240.329 ms |  1.54 | 6000.0000 |  30.71 MB |        4.94 |
| EFCore_Compiled_ToListAsync         | 245.411 ms |  1.57 | 4000.0000 |  20.41 MB |        3.29 |
