```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                         | Mean        | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------------- |------------:|----------:|----------:|------:|--------:|---------:|---------:|-----------:|------------:|
| Linq                           |    75.33 μs |  1.483 μs |  2.351 μs |  0.75 |    0.03 |  28.6865 |        - |  234.45 KB |        1.00 |
| LinqToList                     |    95.39 μs |  1.882 μs |  2.638 μs |  0.95 |    0.03 |  38.2080 |  25.3906 |  312.59 KB |        1.33 |
| NextormPreparedSync            |    99.91 μs |  1.557 μs |  1.457 μs |  1.00 |    0.02 |  28.6865 |        - |  234.38 KB |        1.00 |
| NextormPreparedSyncToList      |   137.49 μs |  1.715 μs |  1.520 μs |  1.38 |    0.02 |  38.0859 |  22.7051 |  312.55 KB |        1.33 |
| EFCoreInMemory_Compiled        | 1,467.34 μs | 27.461 μs | 50.901 μs | 14.69 |    0.55 | 419.9219 | 208.9844 | 3439.24 KB |       14.67 |
| EFCoreInMemory_Compiled_ToList | 1,575.88 μs | 29.371 μs | 26.037 μs | 15.78 |    0.34 | 435.5469 | 208.9844 | 3567.56 KB |       15.22 |
