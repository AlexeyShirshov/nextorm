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

### 2.1. Источники, по которым заполнена матрица

- **SQL Server** — Microsoft Learn, `SqlBulkCopyOptions` enum (`Microsoft.Data.SqlClient`): `Default = 0`,
  `CheckConstraints = 2`, `TableLock = 4`, `KeepNulls = 8`, `FireTriggers = 16`,
  `UseInternalTransaction = 32`. По умолчанию SQL Server **не** проверяет CHECK/FK-ограничения и **не**
  подменяет NULL на DEFAULT (для `INSERT`-пути поведение другое: ограничения проверяются всегда).
- **PostgreSQL** — официальная документация `COPY`/`INSERT`: `COPY ... FROM STDIN` — низкоуровневый
  протокол, у него нет флагов проверки ограничений/блокировки/триггеров; ограничения проверяются
  всегда, триггеры срабатывают всегда, `NULL` пишется как `NULL` (DEFAULT применяется только при
  отсутствии колонки). Таких опций в API `NpgsqlBinaryImporter` нет.
- **MySQL / MariaDB** — native bulk в nextorm не подключён; `LOAD DATA` — файловый, к потоку строк не
  применим; у `INSERT`/`INSERT IGNORE` флагов проверки ограничений/блокировки/триггеров нет.
- **SQLite** — native bulk нет; `INSERT OR IGNORE` и `PRAGMA foreign_keys`, отдельных bulk-флагов нет.
- **ClickHouse** — драйвер `ClickHouse.Driver` 1.4.0: `ClickHouse.Driver.Copy.ClickHouseBulkCopy`
  помечен `[Obsolete]` («функциональность перенесена в `ClickHouseClient`») и содержит
  `MaxDegreeOfParallelism`; не-`Obsolete` путь `ClickHouseClient.InsertBinaryAsync` требует собственного
  клиента, собранного из строки подключения (а не соединения контекста), и выставляет сессию через
  `UseSession`/`SessionId` клиента. Опции `WithoutSession` в драйвере **нет**; сессия — свойство
  connection string (`UseSession`), а не per-write опция.
- **In-memory** — провайдер только для чтения, bulk-терминал бросает `NotSupportedException`.

### 2.2. Существующие опции (без изменений)

| Опция | SQL Server native | PostgreSQL `COPY` | portable `INSERT` | ClickHouse |
|---|---|---|---|---|
| `MaxBatchSize` | `SqlBulkCopy.BatchSize` | — (игнор, поток) | чанк строк | чанк строк |
| `MaxParameters` / `MaxSqlLength` | — | — | чанк параметров/длины | чанк |
| `KeepIdentity` | портируемо (`SET IDENTITY_INSERT`) | `OVERRIDING SYSTEM VALUE` | явные значения | — |
| `IgnoreDuplicates` | отклоняется | `ON CONFLICT DO NOTHING` | `INSERT OR IGNORE`/`IGNORE` | no-op |
| `TableName` / `TableSchema` | да | да | да | да |
| `TimeoutSeconds` | `BulkCopyTimeout` | отклоняется | отклоняется | отклоняется |
| `Progress` / `NotifyEvery` | `NotifyAfter` | цикл записи | после чанка | после чанка |

### 2.3. Новые опции (этот пункт)

Единообразие: булевы флаги объявлены как `bool?`; `true` добавляет соответствующий
`SqlBulkCopyOptions`-флаг, `null`/`false` — поведение не меняется (zero-cost). Провайдер без
нативного пути, не умеющий флаг, бросает `NotSupportedException` при выполнении (не молчит).

| Опция | SQL Server native | PostgreSQL `COPY` | portable `INSERT` (MySQL/MariaDB/SQLite/ClickHouse/InMemory) | Как выразимо |
|---|---|---|---|---|
| `CheckConstraints` | да (`SqlBulkCopyOptions.CheckConstraints`) | — (CHECK/FK проверяются всегда, тумблера нет) | — (то же) | SQL Server-only |
| `TableLock` | да (`TableLock`) | — (нет bulk-тумблера блокировки) | — (обычный `INSERT` берёт построчные блокировки) | SQL Server-only |
| `KeepNulls` | да (`KeepNulls`) | — (в `COPY`/`INSERT` явный NULL и так пишется, DEFAULT — только при отсутствии колонки) | — (то же) | SQL Server-only |
| `FireTriggers` | да (`FireTriggers`) | — (INSERT-триггеры срабатывают всегда) | — (то же) | SQL Server-only |
| `MaxDegreeOfParallelism` | — | — | — | **отложено**: есть только у устаревшего `ClickHouseBulkCopy` |
| `WithoutSession` | — | — | — | **отложено**: в `ClickHouse.Driver` 1.4.0 такой опции нет; сессия — `UseSession` в строке подключения |
| `UseInternalTransaction` | — | — | — | **by design**: nextorm не открывает неявную транзакцию |
| `BulkCopyType` (native/rows) | nextorm выбирает сам по возможностям пути | — | — | не является опцией пользователя |

Для ClickHouse нативный bulk-путь в nextorm сознательно не подключён (см. `ClickHouseDialect`:
`ClickHouseBulkCopy` устарел, `InsertBinaryAsync` требует отдельного `ClickHouseClient`); поэтому
`MaxDegreeOfParallelism`/`WithoutSession` не добавлены в публичный API — иначе они бы всегда бросали.
Это зафиксировано в `docs/advanced/limitations.md` (+RU).

**Решение по единообразию:** реализуются четыре SQL Server-флага. Их можно выставить на любом
провайдере (API один), но на неподдерживающем пути запись падает с явным `NotSupportedException`:
- PostgreSQL native (`PostgresDataContext.BulkInsertRows*`) — отказ;
- портируемый путь (`PortableBulkInsertExecutor`, включая SQL Server `Returning*`) — отказ;
- InMemory как read-only падает раньше, общим отказом.

## 3. C#-аналог и tier

Аналог — `Microsoft.Data.SqlClient.SqlBulkCopyOptions`; tier **b**: расширение record
`BulkInsertOptions` + `BulkInsertOptionsBuilder` + внутренний `BulkCopyFlags` + прокидывание в нативный
хук (`BulkInsertRows*` → `CreateBulkCopy` в `SqlServerDataContext`).

## 4. Дизайн и публичный API

```csharp
public sealed record BulkInsertOptions
{
    public bool? CheckConstraints { get; init; }
    public bool? TableLock { get; init; }
    public bool? KeepNulls { get; init; }
    public bool? FireTriggers { get; init; }
}

public sealed class BulkInsertOptionsBuilder
{
    public BulkInsertOptionsBuilder CheckConstraints(bool value = true);
    public BulkInsertOptionsBuilder TableLock(bool value = true);
    public BulkInsertOptionsBuilder KeepNulls(bool value = true);
    public BulkInsertOptionsBuilder FireTriggers(bool value = true);
}
```

Внутренняя передача — `public readonly record struct BulkCopyFlags(bool CheckConstraints, bool TableLock,
bool KeepNulls, bool FireTriggers)` (`src/nextorm.core/Builders/BulkCopyFlags.cs`) со `static None`/`IsAny`/
`ThrowIfRequested(path)`; кладётся в `BulkInsertCommand.BulkCopy` и добавляется последним параметром в
`DataContext.BulkInsertRows`/`BulkInsertRowsAsync`. Тип **публичный вынужденно**: параметр
`protected`-хука не может быть менее доступным, чем сам хук (иначе CS0051), а хук задаёт точку
расширения провайдеров.

- SQL Server: включённые флаги OR-ятся в `SqlBulkCopyOptions` (`CreateBulkCopy`).
- PostgreSQL: `BulkCopyFlags.ThrowIfRequested("the PostgreSQL COPY path")`.
- Portable: `PortableBulkInsertExecutor.EnsurePortableOptionsSupported` (переименование
  `EnsureTimeoutSupported`) — отказ и для `TimeoutSeconds`, и для флагов; вызывается также из
  `BulkInsertReturningBuilder` (SQL Server `Returning*` идёт портируемым путём).
- `null`/`false` — `BulkCopyFlags.None`, ни одного нового выделения/ветки в горячем пути.
- План-ключ: опции bulk не в plan cache (мутации не кэшируются) — ключ не трогаем.

## 5. План тестов

- `tests/nextorm.core.tests/BulkInsertBuilderTests.cs`: билдер выставляет флаги (`Build()`), дефолты
  `null`; флаги на невыразимом пути дают `NotSupportedException`.
- `tests/nextorm.sqlserver.tests/BulkInsertSqlGenerationTests.cs`: `SqlServerDataContext.MapBulkCopyOptions`
  (`internal static`) собирает нужный `SqlBulkCopyOptions`; `ToSql()` с флагами не ломается.
- `tests/nextorm.postgres.tests/BulkInsertSqlGenerationTests.cs`: PostgreSQL-путь отклоняет флаг.

## 6. Файлы к изменению

- `src/nextorm.core/Builders/BulkInsertOptions.cs`, `Builders/BulkInsertBuilder.cs`,
  `Query/Mutations/BulkInsertCommand.cs`, `DataContext/DataContext.cs` (`BulkInsertRows*`),
  `DataContext/PortableBulkInsertExecutor.cs`, `Builders/BulkInsertReturningBuilder.cs`.
- `src/nextorm.sqlserver/SqlServerDataContext.cs` (`CreateBulkCopy`/`SqlBulkCopyOptions`),
  `src/nextorm.postgres/PostgresDataContext.cs`.
- Тесты: `tests/nextorm.core.tests`, `tests/nextorm.sqlserver.tests`, `tests/nextorm.postgres.tests`.
- Доки: `docs/guide/24-bulk-insert.md` (+RU), `docs/providers/sqlserver.md` (+RU),
  `docs/advanced/api-reference.md` (+RU), `docs/advanced/limitations.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`, `docs/specs/roadmap/sql-capabilities-gap-analysis.md`.

## 7. Открытые вопросы

1. ~~Нужны ли `CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers` вообще~~ — да, criterion 1.
2. ClickHouse: `MaxDegreeOfParallelism`/`WithoutSession` — **отложено** (устаревший/отсутствующий API
   драйвера, нативный путь не подключён).
3. `IgnoreDuplicates` не дублирует `KeepNulls` (нет: конфликты vs DEFAULT-подстановка).

## 8. Baseline покрытия

`coverage.settings.xml` включает только `nextorm.{core,sqlite,postgres,sqlserver}`. Правки —
аддитивные (флаги + отказы), удалённых строк нет; покрытие замеряется после правок по
`dotnet-coverage collect` → `reportgenerator`. ClickHouse/MySQL/MariaDB в порог не входят.

## Статус реализации

- [x] `BulkInsertOptions`/`BulkInsertOptionsBuilder`: `CheckConstraints`, `TableLock`, `KeepNulls`,
      `FireTriggers` (`bool?`, дефолт `null`).
- [x] Внутренний `BulkCopyFlags` + `BulkInsertCommand.BulkCopy` + новые параметры хуков.
- [x] SQL Server: OR-флагов в `SqlBulkCopyOptions` (`MapBulkCopyOptions`); PostgreSQL и portable — явный
      `NotSupportedException`.
- [x] ClickHouse `MaxDegreeOfParallelism`/`WithoutSession` — отложено (см. §2.3), зафиксировано в
      limitations.
- [x] Тесты (core/sqlserver/postgres, без контейнеров), build 0/0.
- [x] Доки EN+RU, api-reference, limitations, API-NAMING-REVIEW, gap-analysis.
