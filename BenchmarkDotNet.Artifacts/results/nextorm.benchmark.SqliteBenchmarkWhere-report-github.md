```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Toolchain=InProcessEmitToolchain  IterationCount=3  
LaunchCount=1  WarmupCount=3  

```
| Method                            | Mean       | Gen0     | Gen1    | Allocated  |
|---------------------------------- |-----------:|---------:|--------:|-----------:|
| Nextorm_Prepared_ToListAsync      |   863.6 μs |   5.8594 |       - |   48.11 KB |
| Nextorm_Prepared_AsyncStream      |   880.2 μs |   5.8594 |       - |   53.98 KB |
| Nextorm_CachedForLoop_ToListAsync | 1,078.6 μs |   5.8594 |  1.9531 |   59.36 KB |
| Dapper_AsyncStream                | 1,330.9 μs |  23.4375 |       - |  203.99 KB |
| Dapper_Async                      | 1,407.1 μs |  21.4844 |       - |  180.71 KB |
| Nextorm_Cached_ToListAsync        | 1,417.0 μs |  52.7344 |       - |  441.11 KB |
| Nextorm_Cached_AsyncStream        | 1,469.7 μs |  54.6875 |       - |     447 KB |
| EFCore_Compiled_AsyncStream       | 3,330.6 μs |  93.7500 | 46.8750 |  785.72 KB |
| EFCore_AsyncStream                | 6,866.1 μs | 171.8750 | 54.6875 | 1420.92 KB |
| EFCore_ToListAsync                | 7,171.7 μs | 171.8750 | 54.6875 | 1433.95 KB |
