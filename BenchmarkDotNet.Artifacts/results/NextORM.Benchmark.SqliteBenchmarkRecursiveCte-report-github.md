```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Dry    : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  LaunchCount=1  Categories=recursive-cte  

```
| Method                           | Toolchain              | IterationCount | RunStrategy | UnrollFactor | WarmupCount | Mean     | Error     | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------------------- |----------------------- |--------------- |------------ |------------- |------------ |---------:|----------:|----------:|------:|-------:|----------:|------------:|
| Recursive_Reused_Prepared_ToList | Default                | 1              | ColdStart   | 1            | 1           | 2.269 ms |        NA | 0.0000 ms |  1.00 |      - |     50 KB |        1.00 |
|                                  |                        |                |             |              |             |          |           |           |       |        |           |             |
| Recursive_Reused_Prepared_ToList | InProcessEmitToolchain | 3              | Default     | 16           | 3           | 1.237 ms | 0.2907 ms | 0.0159 ms |  1.00 | 3.9063 |  45.32 KB |        1.00 |
