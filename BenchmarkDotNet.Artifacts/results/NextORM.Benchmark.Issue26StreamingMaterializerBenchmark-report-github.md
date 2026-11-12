```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9974)
AMD Ryzen 7 9800X3D 4.70GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 11.0.100
  [Host] : .NET 11.0.0 (11.0.0, 11.0.26.53018), X64 RyuJIT x86-64-v4

Toolchain=InProcessEmitToolchain  IterationCount=5  WarmupCount=3  

```
| Method         | Categories | Mean       | Error     | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------------- |----------- |-----------:|----------:|---------:|------:|--------:|-------:|----------:|------------:|
| Class128       | 128B       | 1,239.9 μs |  15.01 μs |  2.32 μs |  1.00 |    0.00 | 1.9531 | 142.39 KB |        1.00 |
| Value128       | 128B       | 1,227.9 μs |   3.17 μs |  0.82 μs |  0.99 |    0.00 |      - |   1.89 KB |        0.01 |
| RefReadonly128 | 128B       | 1,219.3 μs |  13.69 μs |  3.56 μs |  0.98 |    0.00 |      - |   1.89 KB |        0.01 |
|                |            |            |           |          |       |         |        |           |             |
| Class256       | 256B       | 2,406.8 μs | 308.16 μs | 47.69 μs |  1.00 |    0.02 | 3.9063 | 268.22 KB |        1.00 |
| Value256       | 256B       | 2,369.5 μs |  15.87 μs |  4.12 μs |  0.98 |    0.02 |      - |   2.84 KB |        0.01 |
| RefReadonly256 | 256B       | 2,364.8 μs |  10.93 μs |  1.69 μs |  0.98 |    0.02 |      - |   2.83 KB |        0.01 |
|                |            |            |           |          |       |         |        |           |             |
| Class32        | 32B        |   372.7 μs |   5.61 μs |  1.46 μs |  1.00 |    0.01 | 0.9766 |  48.06 KB |        1.00 |
| Value32        | 32B        |   366.8 μs |   2.75 μs |  0.71 μs |  0.98 |    0.00 |      - |   1.21 KB |        0.03 |
| RefReadonly32  | 32B        |   367.7 μs |  11.52 μs |  2.99 μs |  0.99 |    0.01 |      - |   1.21 KB |        0.03 |
