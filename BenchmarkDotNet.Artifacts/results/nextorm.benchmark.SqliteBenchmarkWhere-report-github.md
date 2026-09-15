```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]    : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  

```
| Method                            | Mean      | Allocated  |
|---------------------------------- |----------:|-----------:|
| Nextorm_CachedForLoop_ToListAsync |  86.47 ms |   59.29 KB |
| Nextorm_Prepared_ToListAsync      |  86.71 ms |    48.1 KB |
| Nextorm_Prepared_AsyncStream      |  87.38 ms |   53.98 KB |
| Nextorm_Cached_ToListAsync        |  89.48 ms |  441.12 KB |
| Nextorm_Cached_AsyncStream        |  90.27 ms |     447 KB |
| Dapper_AsyncStream                |  91.85 ms |  203.98 KB |
| EFCore_Compiled_AsyncStream       |  93.73 ms |   785.7 KB |
| Dapper_Async                      |  95.39 ms |   180.7 KB |
| EFCore_ToListAsync                |  97.80 ms |    1434 KB |
| EFCore_AsyncStream                | 100.41 ms | 1420.98 KB |
