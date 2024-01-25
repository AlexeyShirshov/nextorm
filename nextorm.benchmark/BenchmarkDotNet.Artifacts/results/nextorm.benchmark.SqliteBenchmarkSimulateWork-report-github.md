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
| Nextorm_Prepared_AsyncStream        |   6.158 ms |  0.04 | 1382.8125 |   6.21 MB |        1.00 |
| Dapper_AsyncStream                  |  29.917 ms |  0.19 | 3125.0000 |  14.08 MB |        2.27 |
| Nextorm_Cached_AsyncStream          |  52.894 ms |  0.34 | 6500.0000 |  29.57 MB |        4.76 |
| Nextorm_PreparedForLoop_ToListAsync | 156.235 ms |  1.00 | 1250.0000 |   6.23 MB |        1.00 |
| Nextorm_Prepared_ToListAsync        | 156.316 ms |  1.00 | 1250.0000 |   6.21 MB |        1.00 |
| Dapper_Async                        | 209.758 ms |  1.34 | 3000.0000 |  14.09 MB |        2.27 |
| Nextorm_Cached_ToList               | 240.995 ms |  1.54 | 6000.0000 |  29.23 MB |        4.70 |
| EFCore_Compiled_ToListAsync         | 245.202 ms |  1.57 | 4000.0000 |  20.41 MB |        3.29 |
