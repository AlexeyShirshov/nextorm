```

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22621.2861/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 8.0.100
  [Host]   : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2
  .NET 8.0 : .NET 8.0.0 (8.0.23.53103), X64 RyuJIT AVX2

Job=.NET 8.0  Runtime=.NET 8.0  

```
| Method        | Mean      | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|-------------- |----------:|---------:|---------:|-------:|-------:|----------:|
| Dapper        |  46.87 μs | 0.790 μs | 0.739 μs | 0.7324 |      - |   3.54 KB |
| NextormCached |  49.82 μs | 0.984 μs | 0.921 μs | 1.3428 |      - |   6.58 KB |
| EFcore        | 137.54 μs | 2.419 μs | 2.144 μs | 9.7656 | 0.4883 |   45.1 KB |
