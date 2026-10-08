```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=ShortRun  Runtime=.NET 10.0  Toolchain=InProcessEmitToolchain  
IterationCount=3  LaunchCount=1  WarmupCount=3  
Categories=json-stream-phase2  

```
| Method                           | Mean     | Gen0   | Gen1   | Allocated |
|--------------------------------- |---------:|-------:|-------:|----------:|
| NativeArrayJagged_MaterializeStj | 1.407 μs | 0.0668 |      - |     560 B |
| NativeArrayJagged_Stream         | 1.575 μs | 0.1850 |      - |    1552 B |
| NativeArrayInt_MaterializeStj    | 2.059 μs | 0.1602 |      - |    1360 B |
| NativeArrayInt_Stream            | 6.680 μs | 1.3351 | 0.0229 |   11208 B |
