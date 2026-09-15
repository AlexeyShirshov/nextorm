# Performance Optimizations

Аудит hot-path `nextorm.core` по мотивам скилла `dotnet-performance-patterns`.
Базовое правило: сначала измеряем, потом оптимизируем (measure first).

## Бенчмарк

Файл: `nextorm.benchmark/SqliteBenchmarkCachedPlan.cs`
Отчёт: `BenchmarkDotNet.Artifacts/results/nextorm.benchmark.SqliteBenchmarkCachedPlan-report-github.md`

Запуск:

```
dotnet run -c Release --project nextorm.benchmark -- \
  --filter "*SqliteBenchmarkCachedPlan*" --warmupCount 3 --iterationCount 5
```

Дизайн эксперимента — две пары замеров, чтобы сократить вклад БД:

- `Prepared_ToList` (baseline) — подготовленная команда, без поиска в кэше планов и без повторного извлечения параметров.
- `Cached_ToList` — тот же SQL, но кэшированный запрос: платит plan lookup + `ExtractParams` на каждом прогоне. Дельта к baseline = чистый per-execution оверхед кэшированного пути.
- `Cached_PlanOnly_NoParam` — без БД (константа в условии → `NoParams`, `ExtractParams` пропускается): только построение запроса + plan lookup.
- `Cached_PlanOnly_Param` — без БД, с `NORM.Param`: построение запроса + plan lookup + `ExtractParams`. Дельта к NoParam изолирует `ExtractParams`.

## Результаты (100 выполнений на операцию)

| Метод | Время/оп | Allocated/оп | На 1 выполнение |
|---|---:|---:|---:|
| `Prepared_ToList` (baseline) | 96.78 ms | 33.8 KB | ~0.34 KB |
| `Cached_ToList` | 100.76 ms | 412.7 KB | ~4.13 KB |
| `Cached_PlanOnly_NoParam` | 259.5 µs | 268.8 KB | ~2.69 KB |
| `Cached_PlanOnly_Param` | 529.5 µs | 378.9 KB | ~3.79 KB |

Выводы:

- Кэшированный путь аллоцирует в ~12 раз больше подготовленного: +3.79 KB на выполнение.
- `Cached_ToList − Prepared_ToList` по памяти (~379 KB) ≈ `Cached_PlanOnly_Param` — значит БД-часть сокращается, оверхед чистый.
- `ExtractParams` ≈ 1.1 KB/вып. (дельта `Param − NoParam`: 378.9 − 268.8 KB).
- Остальные ~2.7 KB/вып. — построение `Entity`/`QueryCommand` + планирование/поиск в кэше (есть и в NoParam-варианте).
- По времени оверхед ~40 µs из ~1000 µs на запрос (≈4%), по GC — доминирует.

Примечание: замеры шумные (MinIterationTime); для точных цифр поднять `Iterations` и/или считать через `--invocationCount`.

## Что уже соответствует скиллу

- `_sbPool` через `ObjectPool<StringBuilder>` — `nextorm.core/DataContext/SqlBuilder.cs:12`, `nextorm.core/DataContext/DbContext.cs:20`.
- `ValueList<T>` / `ValueList3<T>` на inline-массивах — `nextorm.core/DataContext/ValueList.cs:11`, `nextorm.core/Query/DefaultColumnsProvider.cs:10`.
- `ExpressionKey` кэширует хэш в `_hash` — `nextorm.core/ExpressionCache.cs:14,17`.
- Бенчмарки уже с `[MemoryDiagnoser]` (напр. `nextorm.benchmark/SqliteBenchmarkWhere.cs:17`).

## Находки (по убыванию значимости)

### 1. Полная пересборка параметров на каждом выполнении кэшированного запроса

`nextorm.core/DataContext/DbContext.cs:385` вызывает `ExtractParams` → `MakeSelect(paramMode: true)`
(`DbContext.cs:141-152`) на каждом исполнении. Внутри заново аллоцируются `List<Param>`,
`DefaultColumnsProvider`, `DefaultAliasProvider`, `DefaultParamProvider` и прогоняются все visitor'ы —
только чтобы достать значения. Замерено: ≈1.1 KB/вып.

**Сделано.** Реальные значения `NORM.Param`-плейсхолдеров всё равно подставляет
`DbPreparedQueryCommand.GetDbCommand` из runtime-массива, поэтому повторная перестройка SQL нужна только
для *вычисляемых* параметров (захваченные переменные, замыкания). Добавлен флаг
`DbPreparedQueryCommand.NeedsParamRefresh`, который выставляется на cache-miss только если среди
параметров есть не-`NORM.Param` (проверка `DbContext.IsRuntimeParam`). На cache-hit `ExtractParams`
вызывается лишь при `NeedsParamRefresh == true`.

Изменённые файлы:

- `nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs` — поле `NeedsParamRefresh` + параметр ctor.
- `nextorm.core/DataContext/DbContext.cs` — вычисление флага, условие на cache-hit, `IsRuntimeParam`.

### 2. Аллокации строк в alias-провайдере

`nextorm.core/DataContext/DefaultAliasProvider.cs:19,25,33` — `"t" + idx`. `FindAlias` дергается на каждый
alias в `nextorm.core/Visitors/BaseExpressionVisitor.cs:596,795,828,834`. Набор ограничен (`t1..tN`) →
кэшировать в `string[]` / использовать `string.Create` (таблица string-building в скилле).

**Проверено, откачено.** Реализовал кэш алиасов и замерил через `Build_Sql` / `Build_Sql_Join`
(генерация SQL без кэша планов): 940.99 → 942.09 KB (простой) и 1.93 → 1.92 MB (join) — в пределах шума
(<1%). Стоимость генерации SQL доминируется visitor-машинерией, а не строками алиасов. Изменение откачено,
чтобы не усложнять код без измеримой пользы.

### 3. ObjectPool без try/finally

`nextorm.core/DataContext/SqlBuilder.cs` — `MakeJoin`, `MakeFrom`, `MakeSelect`:
`_sbPool.Get()` → `ToString()` → `Return` без `try/finally`. Если visitor/`Append` бросит — буфер не
вернётся, пул голодает (гоча #4 скилла).

**Сделано.** Все три метода обёрнуты в `try/finally`, пул возвращается на любом пути (включая
исключения при построении SQL). Аллокации на успешном пути не изменились, поведение то же: 101 sqlite +
35 core тестов зелёные.

### 4. `SqlBuilder` — изменяемый `struct` с 9 полями

`nextorm.core/DataContext/SqlBuilder.cs:10`. Копируется по значению при каждом вызове (напр. вложенный
`new SqlBuilder(...).MakeSelect` при union, `:134`). Кандидат на `sealed class` (девиртуализация/inlining)
или `ref struct` — только после замеров, это изменение дизайна.

### 5. Стоимость построения запроса/кэша (~2.7 KB/вып.)

`Entity.Clone()` + перестройка expression tree + `PrepareCommand` + `QueryPlan` на каждом кэшированном
исполнении. Меньший приоритет, чем п.1, но заметный. Требует отдельного бенчмарка/профиля.

## Стабильный baseline (warmup 5, iteration 20)

| Метод | Время/оп | Allocated/оп |
|---|---:|---:|
| `Cached_PlanOnly_NoParam` | 226.8 µs | 268.76 KB |
| `Cached_PlanOnly_Param` | 348.7 µs | 378.91 KB |
| `Prepared_ToList` | 83.18 ms | 33.79 KB |
| `Cached_ToList` | 88.31 ms | 412.72 KB |

## Результаты после оптимизации #1 (те же параметры замера)

| Метод | Allocated до | Allocated после | Δ |
|---|---:|---:|---:|
| `Cached_ToList` | 412.72 KB | 336.94 KB | −75.8 KB (−18%) |
| `Cached_PlanOnly_Param` | 378.91 KB | 303.13 KB | −75.8 KB |
| `Cached_PlanOnly_NoParam` | 268.76 KB | 268.76 KB | 0 |
| `Prepared_ToList` | 33.79 KB | 33.79 KB | 0 |

`ExtractParams`-часть: ~1.1 KB → ~0.34 KB на выполнение. `Alloc Ratio` кэша к prepared: 12.21 → 9.97.
Тесты: 101 sqlite + 35 core — зелёные.

## Проверка на `SqliteBenchmarkSingle` (HEAD vs наши правки)

Один и тот же прогон (`--warmupCount 5 --iterationCount 15`, 10 запросов на операцию), WSL/Linux/.NET 10.

HEAD (без правок):

| Метод | Mean | Allocated | Alloc Ratio |
|---|---:|---:|---:|
| `Dapper_SingleOrDefault` | 9.00 ms | 16.4 KB | 3.82 |
| `EFCore_SingleOrDefault` | 25.37 ms | 149.08 KB | 34.69 |
| `Nextorm_Prepared_SingleOrDefault` | 25.65 ms | 4.3 KB | 1.00 |
| `Nextorm_Cached_SingleOrDefault` | 26.12 ms | 40.58 KB | 9.44 |
| `EFCore_Compiled_SingleOrDefault` | 27.23 ms | 79.8 KB | 18.57 |

Наши правки:

| Метод | Mean | Allocated | Alloc Ratio |
|---|---:|---:|---:|
| `Nextorm_Prepared_SingleOrDefault` | 8.74 ms | 4.3 KB | 1.00 |
| `Dapper_SingleOrDefault` | 8.84 ms | 16.4 KB | 3.82 |
| `Nextorm_Cached_SingleOrDefault` | 9.05 ms | 40.58 KB | 9.44 |
| `EFCore_Compiled_SingleOrDefault` | 9.40 ms | 79.8 KB | 18.57 |
| `EFCore_SingleOrDefault` | 9.98 ms | 149.05 KB | 34.69 |

Выводы:

- Аллокации до/после идентичны: 4.3 / 40.58 / 16.4 / 79.8 / 149 KB. Наши изменения на этот бенчмарк
  не влияют: `Cached` использует захваченную переменную (`it.Id == i`) — это случай
  `NeedsParamRefresh == true`, где `ExtractParams` остаётся по замыслу #1.
- Время невоспроизводимо: один и тот же `Nextorm_Prepared` дал 25.65 ms в HEAD-прогоне и 8.74 ms в
  следующем (~3×). В HEAD-прогоне nextorm/EF ~25-27 ms, а Dapper 9 ms; в следующем — всё 8.7-10 ms.
  `RatioSD` до 0.18 при разбросе, перекрывающем разницу.
- «Nextorm быстрее Dapper» на master справедливо **по аллокациям** (4.3 KB vs 16.4 KB, ~3.8×), но
  **не по времени** на этой машине.

## Ограничения замеров (важно)

- Коммитнутый отчёт `SqliteBenchmarkSingle` (nextorm prepared 329 µs, Dapper 482 µs) снят на
  **Windows 11 + .NET 8** и нативном диске. Текущие замеры — **WSL + .NET 10**, а `data/test.db`
  лежит на `/mnt/c/...`; файловый I/O через WSL доминирует (0.9-2.5 мс на запрос против ~33 µs).
  Абсолютные времена между этими конфигурациями несравнимы.
- Для достоверных latency-замеров: запускать на нативном Linux/Windows диске (не `/mnt/c`), добавить
  `--invocationCount`, изолировать провайдеры (`[IterationSetup]`), фиксировать CPU/турбо. Аллокации
  (`MemoryDiagnoser`) стабильны и пригодны в текущем окружении.

## План

1. [x] Бенчмарк, изолирующий per-execution оверхед (с БД и без БД).
2. [x] Замерить стабильный baseline (больше итераций).
3. [x] Починить п.1 (кэш параметров) — изолированная, самая явная цель.
4. [x] Перезамерить, сравнить с baseline.
5. [x] П.2 (alias strings) — проверено, эффект в пределах шума, откачено.
6. [x] П.3 (try/finally для pooled StringBuilder) — сделано, тесты зелёные.
7. [ ] П.4 (SqlBuilder как `sealed class`/`ref struct`) — изменение дизайна, требует замеров.
8. [ ] П.5 (построение запроса + plan lookup, ~3 KB/вып.) — остаётся доминирующим; нужен отдельный
   профиль и, вероятно, кэш подготовленного плана, чтобы не готовить новый `QueryCommand` каждый раз.
