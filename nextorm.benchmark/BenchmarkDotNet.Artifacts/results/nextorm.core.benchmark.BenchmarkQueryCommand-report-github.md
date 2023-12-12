```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method                   | Mean      | Error    | StdDev   | Median    | Gen0   | Gen1   | Allocated |
|------------------------- |----------:|---------:|---------:|----------:|-------:|-------:|----------:|
| StandardEqualityComparer | 137.82 ns | 2.789 ns | 4.583 ns | 137.13 ns | 0.1836 | 0.0002 |     864 B |
| PlanEqualityComparer     |  53.92 ns | 1.147 ns | 3.290 ns |  55.00 ns | 0.0697 |      - |     328 B |
