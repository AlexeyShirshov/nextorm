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

**Закрыто: пользы нет, оставляем `struct`.**

Исходная гипотеза (копируется по значению, кандидат на `sealed class` / `ref struct`) по коду не
подтвердилась:

- `SqlBuilder` не реализует интерфейс → методы не виртуальные, девиртуализации от `sealed` нечего устранять.
- Создаётся локально (`DbContext.cs:155`, `SqlBuilder.cs:137`, `BaseExpressionVisitor.cs:222/268/469/591/789`),
  методы вызываются прямо на локальной переменной; не передаётся по значению, не хранится в `readonly`-поле,
  не вызывается через интерфейс → defensive copies нет, JIT передаёт `this` по ссылке.
- `sealed class` добавил бы heap-аллокацию на каждое `new SqlBuilder(...)` (при генерации SQL, в т.ч. до 5
  мест на подзапросы в visitor'ах) — регрессия по аллокациям, а не выигрыш.
- `ref struct` по перфу ничего не меняет (используется только как синхронный локальный).

Реальная стоимость в hot-path `SqlBuilder` — интерфейсный диспетч на `_columnsProvider` / `_aliasProvider` /
`_paramProvider` и аллокации visitor'ов (`MakeWhere` / `MakeSort` / `MakeColumn`), а не структура. Замена
struct↔class их не трогает.

### 5. Стоимость построения запроса/кэша — замер потолка

Разложил per-execution оверхед кэшированного пути (`NORM.Param`, без БД) новыми arm'ами
`Construct_Only` и `RePrepare_PlanOnly_Param` (warmup 5, iteration 10, 100 итераций на операцию):

| Arm | Allocated/100 | На 1 выполнение |
|---|---:|---:|
| `Construct_Only` (`Entity.Clone` + `Where` + `Select`, вкл. построение expression tree) | 167.97 KB | ~1.68 KB |
| `RePrepare_PlanOnly_Param` (`PrepareCommand` + plan lookup при стабильной команде) | 39.84 KB | ~0.40 KB |
| `Cached_PlanOnly_NoParam` | 268.76 KB | ~2.69 KB |
| `Cached_PlanOnly_Param` | 303.13 KB | ~3.03 KB |
| `Cached_ToList` (с БД) | 336.94 KB | ~3.37 KB |
| `Prepared_ToList` (baseline) | 33.79 KB | ~0.34 KB |

Разложение кэшированного оверхеда (~3.03 KB/вып.):

- **Конструирование запроса ~1.68 KB (≈55%)** — `Entity.Clone` + `Where` + `Select`. Большая часть —
  построение expression tree: лямбды `it => ...` внутри цикла компилируются в `Expression` заново на
  каждом вызове. Это следствие паттерна использования API, а не внутренностей ORM.
- **`PrepareCommand` + plan lookup ~1.0 KB (≈33%)** — то, что мог бы убрать #5 (нижняя оценка из
  `RePrepare` — ~0.4 KB при стабильной команде; на свежей команде дороже из-за хэширования новых деревьев).
- **`ExtractParams` ~0.34 KB (≈11%)** — уже частично закрыто #1.

**Вывод: #5 закрываем.** Потолок выигрыша — максимум ~1 KB/вып. (~⅓ оверхеда), при этом доминирует
конструирование expression tree, которое #5 не трогает. При цене ~300-500 строк / 5-8 файлов и высоком
риске ROI плохой.

**Реальный рычаг** — не внутренний plan-кэш, а паттерн использования: переиспользовать `.Prepare()` /
`IPreparedQueryCommand` либо кэшировать `Entity`/`QueryCommand` вместо пересборки в цикле. Это видно и в
`SqliteBenchmarkSingle`: cached 40.58 KB против prepared 4.3 KB.

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

## Ускорение прогона бенчмарков (инфраструктура)

Полный прогон всех классов занимал часы из-за: дефолтного job (warmup 6 / 15 итераций, target 500 ms),
сборки generated-проекта и запуска процесса на каждый кейс (кейс = класс × метод × `[Params]`), тяжёлых
внутренних циклов и БД на `/mnt/c`.

Сделано:

- `nextorm.benchmark/NextormConfig.cs` — job вынесен централизованно: по умолчанию `Job.ShortRun`
  (warmup 3 / iterations 3) + `InProcessEmitToolchain` (без build/launch на кейс); `NEXTORM_BENCH_FULL=1`
  включает полный out-of-process `Job.Default`.
- `nextorm.benchmark/Program.cs` — включён `BenchmarkSwitcher...Run(args)` (фильтры и `--list` работают).
- Убраны `[SimpleJob]` из всех классов (иначе job'ы дублировались), `[Config(typeof(NextormConfig))]`
  добавлен где отсутствовал.
- Добавлены parameterless-конструкторы классам с `(bool withLogging = false)` — требование `InProcessEmit`.
- `nextorm.benchmark/BenchDb.cs` — путь к БД: `NEXTORM_BENCH_DB` → `/tmp/nextorm-bench/test.db` →
  fallback `data/test.db`. Все SQLite-классы переведены на него.
- БД скопирована в WSL: `/tmp/nextorm-bench/test.db`.

Результат: `SqliteBenchmarkSingle` (5 кейсов) ~39 с, `SqliteBenchmarkSimulateWork` (8) ~58 с, один метод
`SqliteBenchmarkWhere` ~9 с. Полный прогон (87 кейсов) — ориентировочно ~10-20 мин вместо часов.

Использование:

```bash
./nextorm.benchmark/bin/linux/Release/net10.0/nextorm.benchmark --list flat
./nextorm.benchmark/bin/linux/Release/net10.0/nextorm.benchmark --filter "*SqliteBenchmarkSingle*"
NEXTORM_BENCH_FULL=1 ./nextorm.benchmark/bin/linux/Release/net10.0/nextorm.benchmark --filter "*SqliteBenchmarkWhere*"
NEXTORM_BENCH_DB=/path/test.db ./nextorm.benchmark/bin/linux/Release/net10.0/nextorm.benchmark --filter ...
```

Побочный эффект: на нативном ФС картина совпала с коммитнутым отчётом —
`Nextorm_Prepared_SingleOrDefault` = 86.75 µs против Dapper 135.49 µs (Ratio 1.00 vs 1.56). Прежнее
«Dapper быстрее» было артефактом медленного `/mnt/c`.

## Компараторы выражений (expression plan/equality)

Разобрал `ExpressionPlanEqualityComparer` и обёртки (`Select/From/Join/Sorting/QueryPlan...`).

Что устроено хорошо:

- `GetHashCode` переиспользует один `Visitor` на `QueryCommand` (`QueryCommand.cs:741`) — без аллокаций в
  стабильном состоянии.
- Хэши секций (`WherePlanHash`, `ColumnsPlanHash`, ...) считаются один раз в `PrepareCommand` и
  переиспользуются в `QueryPlanEqualityComparer`.
- Структурный `Equals` вызывается только при коллизии бакетов в `Dictionary<QueryPlan,…>`.

Замер (после исправления бенчмарка — сравнитель вынесен из метода, как кэшируется в проде):

| Метод | Mean | Allocated |
|---|---:|---:|
| `ExpressionPlanEqualityComparer.GetHashCode` | 130.1 ns | 0 B |
| legacy `ExpressionPlanEqualityComparerDELETE` (старый замер, с созданием) | 310.1 ns | 32 B |

Вывод: обход дерева не аллоцирует; активный сравнитель ~2.2× быстрее legacy. Прежние «64 B» в отчёте были
аллокацией самого сравнителя + `Visitor`, а не `GetHashCode`.

Сделано:

- Legacy-сравнители перенесены из production-сборки в `nextorm.core.tests` (**2344 строки**):
  `ExpressionPlanEqualityComparerDELETE` (826), `PreciseExpressionEqualityComparerDELETE` (797),
  `ExpressionEqualityComparerDELETE` (721).
- `nextorm.core/Query/ExpressionPlanEqualityComparer.cs`: 1681 → 864 строк (только активный класс).
- Бенчмарк `BenchmarkQueryCommand` исправлен: сравнитель создаётся один раз, легаси-арм убран.

Проверки: core/benchmark/sqlite build — успешно; 35 core + 101 sqlite тестов — зелёные.

Остаточный потенциал (не делаем, ROI низкий): избыточное смешивание в `VisitBase` (дублирование `Type`),
`Dictionary` в `CompareLambda` для параметров. Это CPU-микрооптимизации на редком пути (`Equals` — только
на коллизиях), дают десятки ns.

## Сравнение с Dapper (свежие замеры, native FS)

In-process ShortRun, БД `/tmp/nextorm-bench/test.db`.

| Класс / arm | Nextorm | Dapper | Итог |
|---|---:|---:|---|
| Where Prepared_ToList | 863.6 µs / 48 KB | 1330.9–1407.1 µs / 181–204 KB | победа |
| Where Cached_ToList (rebuild) | 1417.0 µs / 441 KB | 1330.9 µs / 204 KB | поражение |
| Single Prepared | 86.75 µs / 4.3 KB | 135.49 µs / 16.4 KB | победа |
| Single Cached | 137.5 µs / 40.6 KB | 135.5 µs / 16.4 KB | вровень |
| First Prepared Scalar/Entity | 87.7 / 100.5 µs | 139.1 / 156.3 µs | победа |
| First Cached Scalar/Entity | 146.2 / 165.6 µs / 40–43 KB | 139.1 / 156.3 µs / 16–19 KB | поражение |
| Join Prepared | 105.5 µs / 9 KB | 193.0 µs / 21 KB | победа |
| Join Cached | 262.9 µs / 89 KB | 193.0 µs / 21 KB | поражение |
| Any Prepared | 866.1 µs / 41 KB | 1330.8 µs / 139 KB | победа |
| Any Cached | 1278.6 µs / 310 KB | 1330.8 µs | вровень |
| Iteration (все) | ~10.5–10.9 µs / 0.8–1.7 KB | ~15.1–15.3 µs / 1.9 KB | победа |
| Cache Iter=1 | 268.6 / 271.4 µs | 228.1 µs | поражение (cold) |
| Cache Iter≥3 | 293–539 µs | 707–1301 µs | победа |
| SimulateWork Prepared_AsyncStream | 5.4 ms | 27.8 ms | победа |
| SimulateWork Cached | 42.7–88.3 ms | 27.8–70.2 ms | поражение |

Причины:

1. **Cached-путь** — `ctx.Entity.Where(...).Select(...)` пересобирает `Entity`/`QueryCommand`, строит
   expression tree, гоняет `PrepareCommand` + plan lookup + `ExtractParams` (~3 KB, ~1–3 µs на вызов).
   Dapper берёт готовый SQL + POCO и переиспользует кэш SQL/маппера. Отсюда проигрыш по времени
   (Where/First/Single/SimulateWork) и по аллокациям (2–2.5×).
2. **Cold-start** (`Cache Iter=1`): первый вызов nextorm делает генерацию SQL, хэш плана, компиляцию
   map-делегата (`Expression.Compile`); Dapper — только парсинг SQL. На ≥3 итерациях nextorm впереди вдвое.
3. **Аллокации** даже при равном времени (Any: 310 KB vs 139 KB) — GC-давление на высоком QPS.
4. **Исправлен баг `Join` cached** (pre-existing, воспроизводился на `bc8b066`): на cache-hit `ExtractParams`
   (paramMode) не наполняет `_columnsProvider` — ветки `MakeFrom`/`MakeJoin` под `!_paramMode` — но `MakeWhere`
   всё равно резолвил алиас → `FindAlias` = `null` → `InvalidOperationException`
   (`BaseExpressionVisitor.cs:832`). Фикс: в `BaseExpressionVisitor.VisitMember` алиасы резолвятся только при
   `!_paramMode` (в paramMode текст SQL не строится). Добавлен регрессионный тест
   `JoinTests.SelectJoinWithCapturedParam_RepeatedExecution_ShouldReturnData` (падает без фикса).
   Проверки: 35 core + 102 sqlite тестов зелёные; `Join.Nextorm_Cached` теперь 262.9 µs / 89 KB против
   Dapper 193.0 µs / 21 KB.

Вывод: уступаем ровно там, где API пересобирает запрос (cached + cold-start); prepared-путь быстрее Dapper
в 1.5–2×. Это различие модели использования, а не дефект ядра.

## План

1. [x] Бенчмарк, изолирующий per-execution оверхед (с БД и без БД).
2. [x] Замерить стабильный baseline (больше итераций).
3. [x] Починить п.1 (кэш параметров) — изолированная, самая явная цель.
4. [x] Перезамерить, сравнить с baseline.
5. [x] П.2 (alias strings) — проверено, эффект в пределах шума, откачено.
6. [x] П.3 (try/finally для pooled StringBuilder) — сделано, тесты зелёные.
7. [x] П.4 (SqlBuilder как `sealed class`/`ref struct`) — пользы нет, оставляем `struct` (см. #4).
8. [x] П.5 (построение запроса + plan lookup) — замер потолка сделан, польза ≤1 KB/вып., закрыто
   (см. #5). Рычаг — переиспользование `.Prepare()`/`IPreparedQueryCommand`, а не внутренний plan-кэш.
