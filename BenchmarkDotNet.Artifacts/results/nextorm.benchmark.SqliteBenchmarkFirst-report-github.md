```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]    : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  

```
| Method                                        | Mean     | Error     | StdDev    | Allocated |
|---------------------------------------------- |---------:|----------:|----------:|----------:|
| Nextorm_Prepared_Scalar_FirstOrDefault        | 8.734 ms | 0.1619 ms | 0.1352 ms |   4.52 KB |
| Nextorm_Cached_Scalar_FirstOrDefault          | 8.887 ms | 0.1769 ms | 0.3367 ms |  40.79 KB |
| Nextorm_Prepared_Entity_FirstOrDefault        | 8.898 ms | 0.1081 ms | 0.1011 ms |   8.54 KB |
| Dapper_Entity_FirstOrDefault                  | 9.036 ms | 0.1004 ms | 0.0838 ms |  19.24 KB |
| Dapper_Scalar_FirstOrDefault                  | 9.170 ms | 0.1666 ms | 0.1558 ms |  16.48 KB |
| Nextorm_Cached_Entity_FirstOrDefault          | 9.260 ms | 0.1788 ms | 0.1913 ms |  43.18 KB |
| Nextorm_PreparedForLoop_Entity_FirstOrDefault | 9.288 ms | 0.1829 ms | 0.1879 ms |  34.74 KB |
| EFCore_Compiled_Scalar_FirstOrDefault         | 9.476 ms | 0.1283 ms | 0.1137 ms |   79.8 KB |
| EFCore_Compiled_Entity_FirstOrDefault         | 9.478 ms | 0.0696 ms | 0.0651 ms |   83.2 KB |
