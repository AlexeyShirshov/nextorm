```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD Ryzen 7 5800HS with Radeon Graphics 3.19GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Method                  | Mean           | Error         | StdDev        | Gen0    | Gen1    | Allocated |
|------------------------ |---------------:|--------------:|--------------:|--------:|--------:|----------:|
| Linq                    |       2.518 ns |     0.0175 ns |     0.0164 ns |       - |       - |         - |
| NextormPrepared         |      22.046 ns |     0.4427 ns |     0.5270 ns |       - |       - |         - |
| EFCoreInMemory_Compiled | 283,555.448 ns | 4,173.5551 ns | 3,903.9460 ns | 57.1289 | 22.4609 |  481808 B |
