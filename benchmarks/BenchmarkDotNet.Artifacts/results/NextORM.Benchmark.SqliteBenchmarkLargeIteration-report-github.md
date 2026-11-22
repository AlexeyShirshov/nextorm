```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v3

Runtime=.NET 11.0  Toolchain=InProcessEmitToolchain  

```
| Method                       | Mean      | Gen0     | Gen1     | Gen2    | Allocated |
|----------------------------- |----------:|---------:|---------:|--------:|----------:|
| Nextorm_Cached_ToListAsync   |  8.697 ms | 265.6250 | 156.2500 |       - |   2.21 MB |
| Nextorm_Prepared_ToListAsync |  8.823 ms | 265.6250 | 156.2500 |       - |   2.21 MB |
| Linq2Db_ToListAsync          | 12.142 ms | 343.7500 | 265.6250 | 62.5000 |   2.39 MB |
| EFCore_ToListAsync           | 14.558 ms | 625.0000 | 328.1250 | 78.1250 |   4.53 MB |
