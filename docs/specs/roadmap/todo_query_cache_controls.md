# TODO: управление кэшем планов/запросов (ClearCache, disable, sliding expiration)

> Tracking issue: [#97](https://github.com/AlexeyShirshov/nextorm/issues/97).

> Рабочий план (design RFC). Расширяет **G10** в
> [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md) (ограничение размера
> `DataContextCache`). Источник: инфраструктура linq2db — `Query<T>.ClearCache()`,
> `LinqOptions.DisableQueryCache` (`UseDisableQueryCache`), `LinqOptions.CacheSlidingExpiration`
> (`UseCacheSlidingExpiration`).

## 1. Пункт и цель

- **Фича:** first-class управление кэшами nextorm: явная очистка, глобальное отключение и (опционально)
  вытеснение по времени.
- **Критерий приёмки:**
  1. публичный способ очистить кэши (`DataContextCache.Clear()`), согласованный с
     `[ThreadStatic]`-план-кэшем;
  2. глобальное отключение кэша на уровне контекста/приложения без «залипания» через общий
     `QueryCommand` (см. предупреждение в `AGENTS.md` о `queryCommand.Cache = false` — sticky-флаг
     `_dontCache`);
  3. (опционально) sliding expiration для process-wide `DataContextCache`;
  4. без вызова — текущее поведение (zero-cost).
- **Что уже есть:** per-command `Cache = false` / `storeInCache: false`; `MapperCache` ограничен
  `MaxEntries = 4096`; ограничение размера `DataContextCache` — открытый хвост G10.

## 2. Провайдерная матрица

Управление кэшем — провайдерно-независимая инфраструктура (общая для SQL и in-memory). Отдельных
диалектных хуков не требуется; ни один провайдер не переопределяет поведение.

| Провайдер | Хранилище планов | Отношение к фиче |
|---|---|---|
| PostgreSQL | общий `DataContextCache` + `[ThreadStatic] QueryPlanStore` | `Clear()` / disable / TTL не отличаются |
| SQL Server | то же | то же |
| MySQL | то же | то же |
| MariaDB | то же | то же |
| SQLite | то же | то же |
| ClickHouse | то же | то же |
| InMemory | `Metadata`/`SelectListCache` — те же process-wide; `CommandIndex`/`ExpressionsCache` — per-instance | `DataContextCache.Clear()` чистит process-wide словари, но **не** per-instance `CommandIndex` (его чистит `PurgeQueryCache()`); disable — через `InMemoryDataContext.QueryCacheEnabled` |

Источники: `src/nextorm.core/DataContext/{DataContextCache,InMemoryDataContext}.cs`,
`src/nextorm.core/DataContext/Cache/QueryPlanStore.cs`, `src/nextorm.core/DataContext/QueryPlanner.cs`.
Внешняя документация провайдеров не требуется: фича не выражается в SQL ни на одном диалекте.

**Единообразие провайдеров.** Все SQL-провайдеры получают фичу бесплатно через общий
`DataContext`/`QueryPlanner`; InMemory — через собственный `QueryCacheEnabled` и уже существующий
`PurgeQueryCache()`. Гейтов `Supports*` нет: это не SQL-возможность, а политика кэша.

## 3. C#-аналог и tier

CLR-аналога нет; tier **b** (публичный API управления кэшем). linq2db-имена:
`ClearCache`, `UseDisableQueryCache`, `UseCacheSlidingExpiration`. nextorm-имена:
`DataContextCache.Clear()`, `UseQueryCache(bool)`, `UseCacheSlidingExpiration(TimeSpan)`.

## 4. Дизайн и публичный API (финальные сигнатуры)

```csharp
public static class DataContextCache
{
    // Очищает process-wide словари (Metadata/SelectListCache/ExpressionsCache/InValuesCache),
    // MapperCache, ProjectionAliasCache, FROM-кэш планировщика и план-кэш (QueryPlanStore).
    public static void Clear();

    // TTL process-wide кэшей; TimeSpan.Zero (по умолчанию) — вытеснение выключено (zero-cost).
    public static TimeSpan CacheSlidingExpiration { get; set; }
}

public class DataContextBuilder
{
    public bool QueryCacheEnabled { get; }
    public DataContextBuilder UseQueryCache(bool enabled = true);
    public DataContextBuilder UseCacheSlidingExpiration(TimeSpan ttl);
}

public abstract class DataContext
{
    public bool QueryCacheEnabled { get; set; }   // инициализируется из builder
}

public partial class InMemoryDataContext
{
    public bool QueryCacheEnabled { get; set; }   // инициализируется true
}
```

- **Очистка process-wide словарей** — `DataContextCache.Clear()` обнуляет четыре
  `ConcurrentDictionary`-кэша, `MapperCache`, `ProjectionAliasCache`, приватный `_fromCache`
  `QueryPlanner` и `QueryPlanStore`.
- **План-кэш `[ThreadStatic]`.** `QueryPlanStore.Clear()` реализован через глобальную generation
  (`Interlocked.Increment`) + `[ThreadStatic]` generation; поток, у которого generation устарел,
  лениво пересоздаёт свой словарь при следующем обращении. Это даёт `Clear()` глобальный эффект, а не
  «только текущий поток»; прямой обход чужих `[ThreadStatic]`-словарей невозможен по определению
  `[ThreadStatic]`, поэтому очистка ленивая (при следующем обращении потока). `PurgeQueryCache()`
  остаётся per-context именем того же действия.
- **Глобальное отключение** — флаг `QueryCacheEnabled` на конфигурации контекста
  (`DataContextBuilder.UseQueryCache(false)`, а также сеттер на самом контексте). `DataContext`
  пробрасывает его как локальный `storeInCache: false` в `_planner.GetPreparedQueryCommand(...)` и
  **никогда** не трогает sticky `QueryCommand.Cache` (иначе `_dontCache` протёк бы на общий
  `AnyCommand` и на весь контекст). InMemory-контекст аналогично гейтит
  `InMemoryQueryBuilder.GetPreparedQueryCommand`.
- **Sliding expiration** — поверх `ConcurrentDictionary`: `TimedDictionary<TKey,TValue>` хранит
  `(value, lastAccessTicks)` и на чтении (TryGetValue/индексатор/ContainsKey) лениво проверяет TTL и
  обновляет таймстемп; истёкшая запись удаляется через `TryRemove(KeyValuePair)` (не затирая более
  свежую). При `TimeSpan.Zero` ветка времени не исполняется. TTL — глобальный (кэши статические),
  поэтому задаётся на `DataContextCache`/builder'е, а не на отдельном контексте.

## 5. Влияние на бенчмарки

- Без вызова новых API путь не меняется по существу: `TimedDictionary` в режиме `TTL == Zero` только
  делегирует в внутренний `ConcurrentDictionary` (лишний branch, без обращения к времени).
- `QueryCacheEnabled == true` (по умолчанию) — вычисление `storeInCache && QueryCacheEnabled`
  в hot-path планирования: один branch на подготовку, не на исполнение.
- Prepared-путь (`Prepare()`/`storeInCache: false`) не затрагивается: он и раньше шёл мимо кэша.

## 6. План тестов

Core (`tests/nextorm.core.tests`), без контейнеров:

- `Clear()` возвращает process-wide словари к пустому состоянию (Metadata/SelectListCache).
- Отключение: `QueryCacheEnabled = false` — повторный `GetPreparedQueryCommand(..., storeInCache: true)`
  возвращает **разные** инстансы (план строится заново); с включённым кэшем — тот же инстанс.
- **Регресс sticky-флага:** `QueryCommand.Cache` остаётся `true` после вызова с `storeInCache: false`
  и после вызова на контексте с `QueryCacheEnabled = false`.
- Builder: `UseQueryCache(false)` → `QueryCacheEnabled == false`; `UseCacheSlidingExpiration(ttl)` →
  `DataContextCache.CacheSlidingExpiration == ttl`; отрицательный ttl → `ArgumentOutOfRangeException`.
- Sliding expiration: короткий TTL + `Thread.Sleep`; запись исчезает после простоя и остаётся при
  доступе в пределах TTL (refresh). Тесты — в коллекции с `DisableParallelization`, глобальный TTL
  сбрасывается в `finally`.

SQL plan-cache (`tests/nextorm.sqlite.tests`, file-db, без контейнеров):
- `DataContextCache.Clear()` инвалидирует `[ThreadStatic]` план-кэш: повторный prepared-вызов после
  `Clear()` даёт новый инстанс.

Baseline покрытия (core-only, `coverage.settings.xml`, до правок): line **39.4 %**
(12009/30473), branch **32.3 %** (5385/16635). После реализации: line **39.5 %**
(12086/30592), branch **32.3 %** (5398/16677). Порог `MIN_LINE_COVERAGE=75` относится к полному
набору core+sqlite+postgres+sqlserver.

## 7. Файлы к изменению

- `src/nextorm.core/DataContext/DataContextCache.cs` (`Clear`, TTL, `TimedDictionary`),
  `src/nextorm.core/TimedDictionary.cs` (новый),
  `src/nextorm.core/DataContext/Cache/QueryPlanStore.cs` (generation-clear),
  `src/nextorm.core/DataContext/QueryPlanner.cs` (`ClearFromCache`),
  `src/nextorm.core/DataContext/MapperCache.cs` и `ProjectionAliasCache.cs` (`Clear`),
  `src/nextorm.core/DI/DataContextBuilder.cs` (`UseQueryCache`, `UseCacheSlidingExpiration`),
  `src/nextorm.core/DataContext/DataContext.cs` (`QueryCacheEnabled`, гейт `storeInCache`),
  `src/nextorm.core/DataContext/InMemoryDataContext.cs` (гейт).
- Доки: `docs/guide/15-query-reuse.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`, gap-analysis §G10.

## 8. Открытые вопросы (решения)

1. `Clear()` чистит process-wide словари и план-кэш (через generation), per-instance `CommandIndex`
   InMemory — по-прежнему только `PurgeQueryCache()`.
2. Sliding expiration сделан отдельным TTL-механизмом (LRU/размер из G10 — по-прежнему хвост).
3. Имя — `UseQueryCache(bool)` (едино с `QueryCacheEnabled`); `UsePlanCache` отклонён как более узкое.
4. Глобальное отключение opt-in; бенчмарки prepared-пути не затронуты.

## Статус реализации

**Реализовано (26.09.2026, uncommitted worktree).**

- `DataContextCache.Clear()` — очищает `Metadata`/`SelectListCache`/`ExpressionsCache`/`InValuesCache`,
  `MapperCache`, `ProjectionAliasCache`, FROM-кэш `QueryPlanner` и `QueryPlanStore`.
- `QueryPlanStore.Clear()` — generation-clear (`Interlocked.Increment` + `[ThreadStatic]`-generation):
  очистка становится process-wide и лениво применяется к потокам при следующем обращении; снято
  ограничение «только текущий поток».
- `DataContextBuilder.UseQueryCache(bool)` + `QueryCacheEnabled` на `DataContext`/`InMemoryDataContext` —
  глобальное отключение через локальный `storeInCache: false`; sticky `QueryCommand.Cache` не мутируется
  (регресс-тесты `QueryCacheControlsTests`).
- `DataContextBuilder.UseCacheSlidingExpiration(TimeSpan)` + `DataContextCache.CacheSlidingExpiration` —
  ленивое скользящее вытеснение process-wide кэшей (новый internal `TimedDictionary<TKey,TValue>`,
  `TimeSpan.Zero` = выключено и без обращения к времени).
- Тесты: core `QueryCacheControlsTests` (**9**) — Clear, enable/disable, sticky-флаг, builder, TTL/refresh;
  sqlite `DataContextCacheClearTests` (**1**) — `Clear()` инвалидирует план-кэш. Прогон: core **410/410**,
  sqlite **515/515** (0 failed / 0 skipped).
- Покрытие core-only: до 39.4 % line / 32.3 % branch; после 39.5 % line / 32.3 % branch (рост +0.1 pp).
- Сборка `nextorm.slnx -c Release` — **0 warnings / 0 errors**.
- Аудит: subagent-инструмент `task` в сессии недоступен, поэтому выполнен self-review (SOLID/DRY,
  производительность, именование) и вручную добавлена запись в
  `docs/specs/design/API-NAMING-REVIEW.md` (QCC1–QCC4). `nextorm-code-auditor` /
  `nextorm-design-engineer` следует прогнать при ревью.
- Доки EN+RU: `docs/guide/15-query-reuse.md` (+RU) — раздел «Cache controls» / «Управление кэшем»;
  `docs/advanced/api-reference.md` (+RU) — строка `DataContextCache` и методы builder'а;
  `linq2db-backlog-gap-analysis.md` — G10/`#3009`/сравнительная таблица переведены в Done.

**Отложено:** жёсткий предел размера/LRU `DataContextCache` (хвост `#3009`) не добавлялся — вытеснение
теперь по времени (sliding expiration). Per-instance `InMemoryDataContext.CommandIndex` очищается только
`PurgeQueryCache()`, как и раньше; процессные кэши выражений, не входящие в кэш-инфраструктуру
(reflection-кэши), `Clear()` не трогает.
