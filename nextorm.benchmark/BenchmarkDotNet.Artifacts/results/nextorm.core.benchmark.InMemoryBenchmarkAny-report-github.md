```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method              | Mean         | Error      | StdDev     | Gen0   | Allocated |
|-------------------- |-------------:|-----------:|-----------:|-------:|----------:|
| NextormCompiledSync |    42.686 ns |  0.8671 ns |  0.8111 ns | 0.0068 |      32 B |
| NextormCached       | 1,148.806 ns | 22.9928 ns | 40.2701 ns | 0.4597 |    2168 B |
| Linq                |     4.798 ns |  0.0987 ns |  0.0923 ns |      - |         - |
