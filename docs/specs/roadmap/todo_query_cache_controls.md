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
  1. публичный способ очистить кэши (`ClearCache`/`Clear()`), согласованный с `[ThreadStatic]`-план-кэшем;
  2. глобальное отключение кэша на уровне контекста/приложения без «залипания» через общий
     `QueryCommand` (см. предупреждение в `AGENTS.md` о `queryCommand.Cache = false` — sticky-флаг
     `_dontCache`);
  3. (опционально) sliding expiration для process-wide `DataContextCache`;
  4. без вызова — текущее поведение (zero-cost).
- **Что уже есть:** per-command `Cache = false` / `storeInCache: false`; `MapperCache` ограничен
  `MaxEntries = 4096`; ограничение размера `DataContextCache` — открытый хвост G10.

## 2. Провайдерная матрица

Управление кэшем — провайдерно-независимая инфраструктура (общая для SQL и in-memory).

| Провайдер | Отношение |
|---|---|
| PostgreSQL / SQL Server / MySQL / MariaDB / SQLite / ClickHouse | общий `DataContextCache` + per-thread план-кэш |
| InMemory | `Metadata`/`SelectListCache` те же; `ExpressionsCache` — per-instance |

Отдельных диалектных хуков не требуется.

## 3. C#-аналог и tier

CLR-аналога нет; tier **b** (публичный API управления кэшем). linq2db-имена:
`ClearCache`, `UseDisableQueryCache`, `UseCacheSlidingExpiration`.

## 4. Дизайн и публичный API (предложение)

```csharp
public static class DataContextCache
{
    // Очистка всех process-wide словарей и (через per-context доступ) план-кэшей.
    public static void Clear();
}

public DataContextBuilder UseQueryCache(bool enabled = true);
public DataContextBuilder UseCacheSlidingExpiration(TimeSpan ttl);
```

- Очистка process-wide словарей — тривиальна; план-кэш `[ThreadStatic]` —
  очищать только текущий поток (или сделать план-кэш доступным для очистки per-thread), иначе публичный
  `Clear()` не даёт полной гарантии — **задокументировать**.
- Глобальное отключение — флаг на конфигурации контекста, НЕ через sticky `queryCommand.Cache`; путь
  подготовки — `_planner.GetPreparedQueryCommand(..., storeInCache: false)` (локальный, как в
  `DataContext.GetPreparedTemporaryTableCommand`).
- Sliding expiration — поверх `ConcurrentDictionary` (таймстемп + ленивое вытеснение) либо поверх
  будущего LRU из G10; при `0`/`null` — выключено.

## 5. План тестов

- Core: `Clear()` возвращает кэш к пустому состоянию (метаданные/select-list пересоздаются).
- Отключение: запрос не кладётся в кэш, повторный вызов строит план заново; `Cache`-флаг команды не
  мутируется (регресс-тест на sticky-флаг).
- Sliding expiration: запись исчезает по истечении ttl (используя тестовые часы/инъекцию времени).

## 6. Файлы к изменению

- `src/nextorm.core/DataContext/DataContextCache.cs`, `DataContext/Cache/QueryPlanStore.cs`,
  `DataContext/QueryPlanner.cs`, `DI/DataContextBuilder.cs`, `Query/QueryCommand.cs` (не мутировать sticky
  `Cache`), `DataContext/DataContext.cs` (`GetPreparedQueryCommand`-путь).
- Доки: `docs/guide/15-query-reuse.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`.

## 7. Открытые вопросы

1. `Clear()` должен чистить только process-wide словари или ещё и per-thread план-кэши (как)?
2. Делать ли sliding expiration отдельно или в рамках LRU из G10.
3. Имя: `UseQueryCache(bool)` vs `UsePlanCache`.
4. Не конфликтует ли глобальное отключение с бенчмарками prepared-пути (opt-in).
