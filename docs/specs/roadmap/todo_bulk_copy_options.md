# TODO: паритет опций bulk copy / bulk insert

> Tracking issue: [#92](https://github.com/AlexeyShirshov/nextorm/issues/92).

> Рабочий план (design RFC). Источник: сравнение инфраструктуры linq2db — `BulkCopyOptions`
> (`CheckConstraints`, `TableLock`, `KeepNulls`, `FireTriggers`, `BulkCopyType`, `MaxDegreeOfParallelism`,
> `WithoutSession`, `ServerName`/`DatabaseName`). В nextorm есть `BulkInsertOptions`, но без этих флагов.

## 1. Пункт и цель

- **Фича:** довести поверхность `BulkInsertOptions`/`BulkInsertOptionsBuilder` до паритета с linq2db там,
  где это осмысленно для nextorm-модели.
- **Критерий приёмки:**
  1. SQL Server native-путь умеет `CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers`;
  2. ClickHouse умеет параллельную вставку/сессию, если драйвер это даёт;
  3. невыразимые на пути опции дают явный `NotSupportedException`, а не молча игнорируются;
  4. `null`/`false` — текущее поведение (zero-cost).
- **Что уже есть:** `MaxBatchSize`, `MaxParameters`, `MaxSqlLength`, `KeepIdentity`, `IgnoreDuplicates`,
  `TableName`, `TableSchema`, `TimeoutSeconds`, `Progress`/`NotifyEvery`.

## 2. Провайдерная матрица (что выразимо)

Источник: linq2db `BulkCopyOptions` (подтверждено по докам/коду linq2db); для SQL Server — `SqlBulkCopy`
(`SqlBulkCopyOptions` flags).

| Опция | SQL Server native | PostgreSQL `COPY` | portable `INSERT` | ClickHouse |
|---|---|---|---|---|
| `CheckConstraints` | да (`SqlBulkCopyOptions.CheckConstraints`) | — (ограничения проверяются всегда) | — | — |
| `TableLock` | да (`TableLock`) | — | — | — |
| `KeepNulls` | да (`KeepNulls`) | — | — | — |
| `FireTriggers` | да (`FireTriggers`) | — | — | — |
| `BulkCopyType` (native/rows) | nextorm выбирает сам | — | — | — |
| `MaxDegreeOfParallelism` | — | — | — | **проверить** (ClickHouse.Client) |
| `WithoutSession` | — | — | — | **проверить** |
| `ServerName`/`DatabaseName` | — | — | — | — (nextorm: `TableSchema`) |
| `UseInternalTransaction` | — | — | — | **By design**: nextorm не открывает неявную транзакцию |

**Единообразие:** флаги — per-provider; база — `null`/`false`, на неподдерживающем пути
`NotSupportedException`. `UseInternalTransaction` не переносим (расходится с моделью nextorm —
задокументировано).

## 3. C#-аналог и tier

Аналог — `Microsoft.Data.SqlClient.SqlBulkCopyOptions`; tier **b**: расширение record
`BulkInsertOptions` + `BulkInsertOptionsBuilder` + прокидывание в нативный хук
(`CreateBulkCopy` в `SqlServerDataContext`).

## 4. Дизайн и публичный API (предложение)

```csharp
public sealed record BulkInsertOptions
{
    public bool? CheckConstraints { get; init; }
    public bool? TableLock { get; init; }
    public bool? KeepNulls { get; init; }
    public bool? FireTriggers { get; init; }
    // ClickHouse (если драйвер поддерживает):
    public int? MaxDegreeOfParallelism { get; init; }
    public bool? WithoutSession { get; init; }
}
```

- SQL Server: сложить включённые флаги в `SqlBulkCopyOptions` (`CreateBulkCopy`).
- PostgreSQL/portable: при `true` — `NotSupportedException` с перечислением причины.
- План-ключ: опции bulk не в plan cache (мутации не кэшируются) — ключ не трогаем.

## 5. План тестов

- SQL Server: `CreateBulkCopy` выставляет нужные `SqlBulkCopyOptions` (юнит над контекстом / SQL-gen
  отсутствует — проверять через интеграцию с контейнером).
- PostgreSQL/portable: `ShouldThrow` на `CheckConstraints=true` и т. п.
- ClickHouse: `ShouldThrow`/поведение по результатам проверки драйвера.

## 6. Файлы к изменению

- `src/nextorm.core/Builders/BulkInsertOptions.cs`, `DataContext/DataContext.cs`
  (`BulkInsertRows*` сигнатуры), `nextorm.sqlserver/SqlServerDataContext.cs`
  (`CreateBulkCopy`/`SqlBulkCopyOptions`), `nextorm.postgres/PostgresDataContext.cs`,
  `DataContext/PortableBulkInsertExecutor.cs`, `nextorm.clickhouse/ClickHouseDataContext.cs`.
- Доки: `docs/guide/24-bulk-insert.md` (+RU), `docs/providers/overview.md` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/specs/design/API-NAMING-REVIEW.md`.

## 7. Открытые вопросы

1. Нужны ли `CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers` вообще (SQL Server-only)? Решить.
2. ClickHouse: поддержаны ли `MaxDegreeOfParallelism`/`WithoutSession` используемым драйвером.
3. Не дублирует ли `IgnoreDuplicates` linq2db-овский `KeepNulls` по смыслу (нет).
