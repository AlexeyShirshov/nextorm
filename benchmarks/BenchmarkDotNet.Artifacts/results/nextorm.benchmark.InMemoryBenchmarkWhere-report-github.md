```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                  | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|------------------------ |----------:|----------:|----------:|------:|--------:|----------:|----------:|------------:|------------:|
| NextormPreparedParam    |  2.514 ms | 0.0188 ms | 0.0157 ms |  1.00 |    0.01 |         - |         - |    22.66 KB |        1.00 |
| Linq                    |  2.835 ms | 0.0536 ms | 0.0501 ms |  1.13 |    0.02 |         - |         - |    17.28 KB |        0.76 |
| EFCoreInMemory_Compiled | 48.938 ms | 0.9504 ms | 1.0169 ms | 19.46 |    0.41 | 5727.2727 | 2272.7273 | 47121.09 KB |    2,079.83 |
