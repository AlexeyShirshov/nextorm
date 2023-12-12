```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2715/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]     : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2


```
| Method      | Mean      | Error     | StdDev    | Gen0   | Allocated |
|------------ |----------:|----------:|----------:|-------:|----------:|
| DirectCall  |  18.75 ns |  0.214 ns |  0.200 ns |      - |         - |
| DynamicCall | 898.94 ns | 13.964 ns | 13.061 ns | 0.1698 |     800 B |
