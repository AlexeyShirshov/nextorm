```

BenchmarkDotNet v0.13.9+228a464e8be6c580ad9408e98f18813f6407fb5a, Windows 11 (10.0.22621.2428/22H2/2022Update/SunValley2)
Intel Core i5-9600KF CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK 7.0.114
  [Host]     : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.14 (7.0.1423.51910), X64 RyuJIT AVX2


```
| Method                | Mean     | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated  | Alloc Ratio |
|---------------------- |---------:|----------:|----------:|------:|--------:|---------:|--------:|-----------:|------------:|
| NextormCompiledAsync  | 4.202 ms | 0.0693 ms | 0.0648 ms |  1.01 |    0.03 |  46.8750 |       - |  230.55 KB |        1.02 |
| NextormCompiled       | 4.157 ms | 0.0800 ms | 0.0749 ms |  1.00 |    0.00 |  46.8750 |       - |  225.86 KB |        1.00 |
| NextormCompiledToList | 4.034 ms | 0.0488 ms | 0.0433 ms |  0.97 |    0.02 |  46.8750 |       - |  233.26 KB |        1.03 |
| NextormCached         | 4.462 ms | 0.0585 ms | 0.0547 ms |  1.07 |    0.02 |  62.5000 |       - |   288.5 KB |        1.28 |
| NextormCachedToList   | 4.435 ms | 0.0518 ms | 0.0484 ms |  1.07 |    0.02 |  62.5000 |       - |   295.9 KB |        1.31 |
| EFCore                | 9.795 ms | 0.0970 ms | 0.0810 ms |  2.35 |    0.04 | 218.7500 | 46.8750 |  1061.3 KB |        4.70 |
| EFCoreStream          | 9.788 ms | 0.1039 ms | 0.0972 ms |  2.36 |    0.05 | 234.3750 | 46.8750 | 1080.29 KB |        4.78 |
| EFCoreCompiled        | 5.900 ms | 0.0484 ms | 0.0404 ms |  1.42 |    0.03 | 109.3750 | 31.2500 |  534.38 KB |        2.37 |
| Dapper                | 4.154 ms | 0.0439 ms | 0.0343 ms |  0.99 |    0.02 |  39.0625 |       - |  188.91 KB |        0.84 |
| DapperUnbuffered      | 4.188 ms | 0.0734 ms | 0.0686 ms |  1.01 |    0.02 |  39.0625 |       - |  209.46 KB |        0.93 |
