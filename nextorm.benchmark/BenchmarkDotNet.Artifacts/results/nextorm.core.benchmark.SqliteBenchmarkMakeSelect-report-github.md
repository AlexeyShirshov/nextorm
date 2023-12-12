```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 7.0 : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2


```
| Method     | Job      | Runtime  | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|----------- |--------- |--------- |---------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| MakeParams | .NET 7.0 | .NET 7.0 | 3.992 μs | 0.0465 μs | 0.0363 μs |  1.00 |    0.00 | 0.8392 |   3.86 KB |        1.00 |
| MakeSelect | .NET 7.0 | .NET 7.0 | 3.783 μs | 0.0740 μs | 0.1215 μs |  0.93 |    0.03 | 0.8583 |   3.95 KB |        1.02 |
| Lookup     | .NET 7.0 | .NET 7.0 | 3.204 μs | 0.0627 μs | 0.0722 μs |  0.81 |    0.02 | 0.6447 |   2.97 KB |        0.77 |
| MakeParams | .NET 8.0 | .NET 8.0 | 3.384 μs | 0.0670 μs | 0.1470 μs |  0.86 |    0.04 | 0.8240 |   3.86 KB |        1.00 |
| MakeSelect | .NET 8.0 | .NET 8.0 | 3.224 μs | 0.0642 μs | 0.1681 μs |  0.80 |    0.03 | 0.8545 |   3.95 KB |        1.02 |
| Lookup     | .NET 8.0 | .NET 8.0 | 2.633 μs | 0.0463 μs | 0.0433 μs |  0.66 |    0.01 | 0.6409 |   2.97 KB |        0.77 |
