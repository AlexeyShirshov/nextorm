```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  IterationCount=3  
LaunchCount=1  WarmupCount=3  Categories=eager-perparent-paging  

```
| Method                      | Toolchain              | ParentCount | Mean       | Ratio | Gen0      | Gen1      | Allocated   | Alloc Ratio |
|---------------------------- |----------------------- |------------ |-----------:|------:|----------:|----------:|------------:|------------:|
| Split_Paged                 | Default                | 1           |         NA |     ? |        NA |        NA |          NA |           ? |
| SingleQuery_Paged           | Default                | 1           |         NA |     ? |        NA |        NA |          NA |           ? |
| Split_Unpaged_ReferencePage | Default                | 1           |         NA |     ? |        NA |        NA |          NA |           ? |
|                             |                        |             |            |       |           |           |             |             |
| SingleQuery_Paged           | InProcessEmitToolchain | 1           |   4.103 ms |  0.47 |   39.0625 |   31.2500 |   346.81 KB |        0.81 |
| Split_Unpaged_ReferencePage | InProcessEmitToolchain | 1           |   5.545 ms |  0.64 |   46.8750 |    7.8125 |   399.39 KB |        0.94 |
| Split_Paged                 | InProcessEmitToolchain | 1           |   8.786 ms |  1.01 |   46.8750 |         - |   426.46 KB |        1.00 |
|                             |                        |             |            |       |           |           |             |             |
| Split_Paged                 | Default                | 100         |         NA |     ? |        NA |        NA |          NA |           ? |
| SingleQuery_Paged           | Default                | 100         |         NA |     ? |        NA |        NA |          NA |           ? |
| Split_Unpaged_ReferencePage | Default                | 100         |         NA |     ? |        NA |        NA |          NA |           ? |
|                             |                        |             |            |       |           |           |             |             |
| Split_Unpaged_ReferencePage | InProcessEmitToolchain | 100         |  23.415 ms |  0.75 |  468.7500 |   31.2500 |  3960.63 KB |        2.69 |
| SingleQuery_Paged           | InProcessEmitToolchain | 100         |  31.210 ms |  0.99 |  187.5000 |   93.7500 |  1616.93 KB |        1.10 |
| Split_Paged                 | InProcessEmitToolchain | 100         |  31.645 ms |  1.01 |  125.0000 |         - |  1472.31 KB |        1.00 |
|                             |                        |             |            |       |           |           |             |             |
| Split_Paged                 | Default                | 1000        |         NA |     ? |        NA |        NA |          NA |           ? |
| SingleQuery_Paged           | Default                | 1000        |         NA |     ? |        NA |        NA |          NA |           ? |
| Split_Unpaged_ReferencePage | Default                | 1000        |         NA |     ? |        NA |        NA |          NA |           ? |
|                             |                        |             |            |       |           |           |             |             |
| SingleQuery_Paged           | InProcessEmitToolchain | 1000        | 275.948 ms |  0.72 | 1000.0000 |         - | 12903.04 KB |        1.11 |
| Split_Unpaged_ReferencePage | InProcessEmitToolchain | 1000        | 323.823 ms |  0.84 | 4000.0000 | 1000.0000 | 38566.18 KB |        3.31 |
| Split_Paged                 | InProcessEmitToolchain | 1000        | 387.315 ms |  1.00 | 1000.0000 |         - | 11656.19 KB |        1.00 |

Benchmarks with issues:
  EagerPerParentPagingBenchmark.Split_Paged: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3) [ParentCount=1]
  EagerPerParentPagingBenchmark.SingleQuery_Paged: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3) [ParentCount=1]
  EagerPerParentPagingBenchmark.Split_Unpaged_ReferencePage: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3) [ParentCount=1]
  EagerPerParentPagingBenchmark.Split_Paged: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3) [ParentCount=100]
  EagerPerParentPagingBenchmark.SingleQuery_Paged: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3) [ParentCount=100]
  EagerPerParentPagingBenchmark.Split_Unpaged_ReferencePage: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3) [ParentCount=100]
  EagerPerParentPagingBenchmark.Split_Paged: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3) [ParentCount=1000]
  EagerPerParentPagingBenchmark.SingleQuery_Paged: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3) [ParentCount=1000]
  EagerPerParentPagingBenchmark.Split_Unpaged_ReferencePage: ShortRun(IterationCount=3, LaunchCount=1, WarmupCount=3) [ParentCount=1000]
