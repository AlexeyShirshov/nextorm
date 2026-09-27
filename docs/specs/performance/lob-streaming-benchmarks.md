# LOB-стриминг: замер производительности (цикл 1)

Внутренний перф-замер потока LOB-стриминга (`Stream`/`TextReader`) для PostgreSQL и SQL Server.
Относится к циклу 1 потока LOB-стриминга (#27; status-файл цикла удалён при закрытии, история —
`docs/specs/design/code-smells-review.md` §«Цикл #27…»): обязательная
приёмка cached path (часть A) и сравнение streaming vs buffered по аллокациям (часть B).

- **Дата:** 2026-09-27.
- **Хост:** `alex-asus`, AMD Ryzen 7 5800HS with Radeon Graphics, 1 CPU, 8 logical / 4 physical cores.
- **ОС:** Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish), WSL2.
- **SDK/runtime:** .NET SDK 10.0.401, runtime .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3.
- **BenchmarkDotNet:** 0.15.8 (часть A); часть B — без BDN, см. метод ниже.

## Часть A — 7 acceptance-кейсов cached path

Команда (та же, что в `docs/specs/performance/acceptance-benchmarks.md`):

```
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance
```

Прогон выполнен дважды (для оценки разброса). Оба раза: **7 выбранных кейсов, 0 падений**;
BDN `Global total time` 50.4 s и 47.55 s, внешний wall-clock 51 s — в пределах бюджета ≤4 мин.

### Mean / Error / StdDev

| Кейс | Baseline Mean | Run 1 Mean | Run 1 Error | Run 1 StdDev | Run 2 Mean | Run 2 Error | Run 2 StdDev |
|------|---------------|------------|-------------|--------------|------------|-------------|--------------|
| `Nextorm_Count` | 2.915 ms | 2.203 ms | 0.0611 ms | 0.0033 ms | 2.383 ms | 1.104 ms | 0.0605 ms |
| `Nextorm_GroupByCount` | 60.95 ms | 58.47 ms | 15.86 ms | 0.869 ms | 62.74 ms | 15.83 ms | 0.868 ms |
| `Nextorm_Cached` | 1.772 ms | 1.815 ms | 0.3005 ms | 0.0165 ms | 2.032 ms | 1.761 ms | 0.0965 ms |
| `Prepared_ToList` | 923.8 μs | 910.9 μs | 129.40 μs | 7.09 μs | 1.124 ms | 1.5835 ms | 0.0868 ms |
| `Cached_ToList` | 1,727.4 μs | 2,042.0 μs | 4,340.74 μs | 237.93 μs | 1.852 ms | 0.3197 ms | 0.0175 ms |
| `Cached_PlanOnly_Param` | 518.3 μs | 517.1 μs | 15.72 μs | 0.86 μs | 1.091 ms | 2.5253 ms | 0.1384 ms |
| `Nextorm_Cached_ToListAsync` | 2.137 ms | 2.072 ms | — | — | 2.390 ms | — | — |

### Allocated / Gen0

| Кейс | Baseline Allocated | Run 1 Allocated | Run 2 Allocated |
|------|-------------------|-----------------|-----------------|
| `Nextorm_Count` | 335.17 KB | 335.16 KB | 346.09 KB |
| `Nextorm_GroupByCount` | 50 MB | 50.03 MB | 50.03 MB |
| `Nextorm_Cached` | 534.42 KB | 534.42 KB | 534.42 KB |
| `Prepared_ToList` | 76.14 KB | 76.14 KB | 76.14 KB |
| `Cached_ToList` | 565.22 KB | 565.22 KB | 565.23 KB |
| `Cached_PlanOnly_Param` | 489.08 KB | 489.08 KB | 489.08 KB |
| `Nextorm_Cached_ToListAsync` | 692.07 KB | 692.07 KB | 692.07 KB |

### Cached vs prepared (одно и то же исполнение SQLite)

| Run | `Prepared_ToList` | `Cached_ToList` | Отношение времени | Отношение Allocated |
|-----|-------------------|-----------------|-------------------|---------------------|
| Baseline | 923.8 μs | 1,727.4 μs | **1.87** | **7.42** |
| Run 1 | 910.9 μs | 2,042.0 μs | **2.24** (+19.8 % к baseline) | 7.42 |
| Run 2 | 1.124 ms | 1.852 ms | **1.65** (−11.8 % к baseline) | 7.42 |

**Вывод части A.** Аллокационное отношение cached/prepared равно baseline (7.42) в обоих прогонах —
утечки памяти в план-кэш нет. Отношение времени колеблется 1.65–2.24 вокруг baseline 1.87; Run 1 даёт
+19.8 % (чуть ниже порога расследования 20 %), но его `Cached_ToList` имел `Error = 4,340.74 μs`
при `Mean = 2,042.0 μs` — прогон шумный. Повтор (Run 2) дал 1.65, то есть ниже baseline. Разброс
Run-to-run больше порога, подтверждённой регрессии нет; `Cached_PlanOnly_Param` в правило
cached/prepared не входит (другой workload, см. baseline-документ).

## Часть B — streaming vs buffered LOB: 1 МиБ и 8 МиБ

### Выбор метода и обоснование

Предпочтительный BDN-класс `[BenchmarkCategory("acceptance-lob")]` в `benchmarks/nextorm.benchmark`
не добавлен: проект ссылается только на `nextorm.core` + `nextorm.sqlite`
(`benchmarks/nextorm.benchmark/nextorm.benchmark.csproj`), а для PostgreSQL/SQL Server к нему
пришлось бы добавить `nextorm.postgres`/`nextorm.sqlserver`, драйверы Npgsql /
Microsoft.Data.SqlClient и Testcontainers — непропорционально для одного замера. Поэтому
использован согласованный fallback: детерминированный замер в интеграционном харнессе.

Харнесс — `tests/nextorm.integration.tests/LobPerfHarnessTests.cs`, включается только при
`NEXTORM_LOB_PERF=1` (иначе кейс `skipped`). Он:

1. для PostgreSQL и SQL Server вызывает `EnsureSeeded()` и вставляет в `lob_entity` строку id=2
   размером 1 МиБ (`delete` + параметризованный `insert` через `ExecuteRaw`); 8 МиБ — уже
   засеянная строка id=1;
2. для каждой комбинации провайдер × размер (1/8 МиБ) × BLOB/CLOB × streaming/buffered снимает
   `GC.GetAllocatedBytesForCurrentThread()` вокруг операции и минимальное время `Stopwatch`;
3. **streaming** читает в переиспользуемый буфер 64 КиБ (`byte[]` для BLOB, `char[]` для CLOB)
   циклом `Read`, без `CopyTo`/`ReadToEnd`; **buffered** — материализация `.First()`
   (полный `byte[]` / `string`);
4. перед измерением — 2 прогрева (JIT, статические инициализации, построение и кэширование
   плана), затем 3 сэмпла, берётся минимум (консервативная оценка аллокаций).

### Команда

```
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock NEXTORM_LOB_PERF=1 \
  dotnet run --project tests/nextorm.integration.tests -c Debug -- \
  -class nextorm.integration.tests.LobPerfHarnessTests -noColor
```

Контейнеры: переиспользуемые PostgreSQL 17 и SQL Server 2025 (Podman). Тест: 1 кейс, 0 падений,
wall 18.8 s. LOB-прогон выполнен **отдельно** от 7-кейсовой приёмки.

### Результаты (минимум из 3 сэмплов)

`Allocated` — точная дельта `GC.GetAllocatedBytesForCurrentThread()`, байты. `Time` — минимальное
время операции (`Stopwatch`), мс; это не BDN `Mean`, а консервативная оценка, приведена справочно
(зависит от контейнера/сети).

| Провайдер | Тип | Размер | Streaming Allocated | Streaming Time | Buffered Allocated | Buffered Time | Buffered / Streaming |
|-----------|-----|--------|---------------------|----------------|--------------------|---------------|----------------------|
| PostgreSQL | BLOB | 1 МиБ | 74,208 B (≈72.5 КиБ) | 5.7 ms | 1,056,856 B (≈1.01 МиБ) | 4.5 ms | 14.2× |
| PostgreSQL | BLOB | 8 МиБ | 77,704 B (≈75.9 КиБ) | 30.0 ms | 8,397,792 B (≈8.01 МиБ) | 26.2 ms | 108.1× |
| PostgreSQL | CLOB | 1 МиБ | 142,832 B (≈139.5 КиБ) | 7.2 ms | 2,105,800 B (≈2.01 МиБ) | 7.6 ms | 14.7× |
| PostgreSQL | CLOB | 8 МиБ | 142,832 B (≈139.5 КиБ) | 43.6 ms | 16,786,784 B (≈16.01 МиБ) | 48.5 ms | 117.5× |
| SQL Server | BLOB | 1 МиБ | 75,776 B (≈74.0 КиБ) | 6.5 ms | 2,105,776 B (≈2.01 МиБ) | 19.2 ms | 27.8× |
| SQL Server | BLOB | 8 МиБ | 80,576 B (≈78.7 КиБ) | 54.6 ms | 16,788,912 B (≈16.01 МиБ) | 54.2 ms | 208.4× |
| SQL Server | CLOB | 1 МиБ | 272,072 B (≈265.7 КиБ) | 13.0 ms | 2,105,920 B (≈2.01 МиБ) | 13.8 ms | 7.7× |
| SQL Server | CLOB | 8 МиБ | 293,960 B (≈287.1 КиБ) | 96.9 ms | 16,793,448 B (≈16.02 МиБ) | 92.9 ms | 57.1× |

### Масштабирование 1 МиБ → 8 МиБ (×8 данных)

| Провайдер/тип | Streaming: рост | Buffered: рост |
|---------------|-----------------|----------------|
| PostgreSQL BLOB | 74,208 → 77,704 B = **×1.05** | 1.01 → 8.01 МиБ = **×8.0** |
| PostgreSQL CLOB | 142,832 → 142,832 B = **×1.00** | 2.01 → 16.01 МиБ = **×8.0** |
| SQL Server BLOB | 75,776 → 80,576 B = **×1.06** | 2.01 → 16.01 МиБ = **×8.0** |
| SQL Server CLOB | 272,072 → 293,960 B = **×1.08** | 2.01 → 16.02 МиБ = **×8.0** |

**Вывод части B.** Streaming-аллокации при росте данных 1 → 8 МиБ практически не растут
(≤ +8 %), то есть **O(буфера)**: для BLOB это ~64 КиБ буфер + накладные (~72–80 КиБ), для CLOB —
64K-символьный буфер (128 КиБ) + накладные (~140 КиБ у PostgreSQL, ~0.27–0.29 МиБ у SQL Server).
Buffered-путь растёт ровно ×8 вместе с размером значения (**O(размера)**): PostgreSQL BLOB ≈ ×1
размера, PostgreSQL CLOB ≈ ×2 (UTF-16), SQL Server BLOB/CLOB ≈ ×2 (драйвер буферизует значение
полностью). Цель подтверждена — `ToStream`/`ToTextReader` дают O(буфера), буферизованное чтение —
O(размера).

## Non-determinism

- Аллокации измерены детерминированно (`GC.GetAllocatedBytesForCurrentThread`), числа
  воспроизведены в двух запусках; время операции шумное, зависит от контейнера/сети — используется
  как справочное, не для регрессионных утверждений.
- Замеры части B сняты на текущем хосте в одном процессе; сравнение с другими хостами/конфигами
  некорректно.
- Baseline части A (`docs/specs/performance/acceptance-benchmarks.md`) не изменялся.
