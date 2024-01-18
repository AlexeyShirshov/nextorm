```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.3007/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.101
  [Host]   : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.1 (8.0.123.58001), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                              | Mean       | Median     | Ratio | Gen0      | Allocated | Alloc Ratio |
|------------------------------------ |-----------:|-----------:|------:|----------:|----------:|------------:|
| Nextorm_Prepared_StreamAsync        |   6.087 ms |   6.193 ms |  0.04 | 1382.8125 |   6.21 MB |        1.00 |
| Dapper_AsyncStream                  |  30.005 ms |  29.783 ms |  0.20 | 3125.0000 |  14.08 MB |        2.27 |
| Nextorm_Cached_AsyncStream          |  53.687 ms |  53.793 ms |  0.35 | 7000.0000 |  31.51 MB |        5.07 |
| Nextorm_Prepared_ToListAsync        | 152.192 ms | 151.314 ms |  1.00 | 1250.0000 |   6.21 MB |        1.00 |
| Nextorm_PreparedForLoop_ToListAsync | 153.249 ms | 151.664 ms |  1.01 | 1250.0000 |   6.23 MB |        1.00 |
| Dapper_Async                        | 206.064 ms | 205.767 ms |  1.36 | 3000.0000 |  14.09 MB |        2.27 |
| EFCore_Compiled_ToListAsync         | 246.402 ms | 244.861 ms |  1.63 | 4000.0000 |  20.41 MB |        3.29 |
| Nextorm_Cached_ToList               | 271.234 ms | 268.467 ms |  1.80 | 7000.0000 |  32.23 MB |        5.19 |
