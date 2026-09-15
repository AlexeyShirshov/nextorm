```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.3 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]    : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  IterationCount=20  
WarmupCount=5  

```
| Method                  | Mean         | Ratio | Gen0     | Gen1     | Allocated  | Alloc Ratio |
|------------------------ |-------------:|------:|---------:|---------:|-----------:|------------:|
| Cached_PlanOnly_NoParam |     281.0 μs | 0.001 |  31.2500 |        - |  268.76 KB |        7.95 |
| Cached_PlanOnly_Param   |     490.5 μs | 0.002 |  35.1563 |        - |  303.13 KB |        8.97 |
| Build_Sql               |   8,177.9 μs | 0.033 | 109.3750 |  93.7500 |  942.09 KB |       27.88 |
| Build_Sql_Join          |  30,109.7 μs | 0.122 | 230.7692 | 153.8462 | 1986.06 KB |       58.78 |
| Prepared_ToList         | 247,611.3 μs | 1.003 |        - |        - |   33.79 KB |        1.00 |
| Cached_ToList           | 259,551.7 μs | 1.052 |        - |        - |     337 KB |        9.97 |
