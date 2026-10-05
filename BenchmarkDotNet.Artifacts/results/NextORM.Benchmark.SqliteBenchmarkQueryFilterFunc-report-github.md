```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Dry    : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  LaunchCount=1  Categories=query-filter  

```
| Method                     | Toolchain              | IterationCount | RunStrategy | UnrollFactor | WarmupCount | Mean     | Error     | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
|--------------------------- |----------------------- |--------------- |------------ |------------- |------------ |---------:|----------:|----------:|------:|-------:|----------:|------------:|
| FuncFilter_Prepared_ToList | Default                | 1              | ColdStart   | 1            | 1           | 2.427 ms |        NA | 0.0000 ms |  1.00 |      - |  79.69 KB |        1.00 |
|                            |                        |                |             |              |             |          |           |           |       |        |           |             |
| FuncFilter_Prepared_ToList | InProcessEmitToolchain | 3              | Default     | 16           | 3           | 1.061 ms | 0.0833 ms | 0.0046 ms |  1.00 | 7.8125 |  75.01 KB |        1.00 |
