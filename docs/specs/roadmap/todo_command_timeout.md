# TODO: command timeout (per-context / per-query)

> Tracking issue: [#93](https://github.com/AlexeyShirshov/nextorm/issues/93).

> Рабочий план (design RFC). Источник: сравнение инфраструктуры linq2db (Data Connection System):
> `DataOptions.UseCommandTimeout()` / `DataContextOptions.WithCommandTimeout()`. В nextorm собственного
> таймаута для обычных запросов нет — только `BulkInsertOptions.TimeoutSeconds` (SQL Server native).

## 1. Пункт и цель

- **Фича:** задать `DbCommand.CommandTimeout` для обычных команд (`SELECT`/DML/`ExecuteScalar`) на уровне
  контекста и/или отдельного запроса.
- **Критерий приёмки:**
  1. `UseCommandTimeout(seconds)` на `DataContextBuilder` влияет на все команды контекста;
  2. per-query override (`WithCommandTimeout`) сильнее контекстного;
  3. значение не «протекает» через план-кэш на другие вызовы;
  4. `null`/0 — провайдерный дефолт (текущее поведение, zero-cost);
  5. in-memory — no-op; отсутствие поддержки на провайдере — понятное поведение.
- **Обходной путь сейчас:** `IQueryInterceptor.CommandInitialized` может выставить `command.CommandTimeout`
  ([гайд 27](../../guide/27-interceptors.md:18)), но это не first-class API.

## 2. Провайдерная матрица

`DbCommand.CommandTimeout` — общий ADO.NET-контракт; конкретный эффект — на драйвере. Значение
`> 0` (секунды) записывается в `DbCommand.CommandTimeout` (prepared SELECT/DML) и в `DbBatch.Timeout`
(батчи); `null`/`<= 0` — свойство не трогается (провайдерный дефолт, zero-cost). Источники: ADO.NET
`DbCommand.CommandTimeout` и документация драйверов.

| Провайдер | CommandTimeout | Примечание |
|---|---|---|
| SQL Server | да | `SqlCommand.CommandTimeout`, секунды; дефолт драйвера — 30. `0` = бесконечно, поэтому nextorm трактует `<= 0` как «не задавать» (провайдерный дефолт). |
| PostgreSQL | да | `NpgsqlCommand.CommandTimeout` (дефолт 30 c). |
| MySQL | да | `MySqlConnector.MySqlCommand.CommandTimeout`. |
| MariaDB | да | Наследует `MySqlConnector` (тот же `MySqlCommand`), как и весь MySQL-диалект. |
| SQLite | да | `Microsoft.Data.Sqlite.SqliteCommand.CommandTimeout` — прерывание по таймеру; дефолт драйвера 30 c. |
| ClickHouse | да (свойство) / фактически no-op | `ClickHouse.Driver` 1.4.0 экспонирует унаследованный `CommandTimeout`, но HTTP-запрос ограничивается `ClickHouseConnection.Timeout` (дефолт 2 мин), а не `CommandTimeout` команды; установка свойства не приводит к серверному/HTTP-таймауту. Оставлено как честный ADO-контракт, документировано. |
| InMemory | — | Команды (`DbCommand`) нет; `IContextEnvironment.CommandTimeout` возвращает `null` (default interface member), per-query значение хранится на команде, но нигде не применяется — no-op. |

**Единообразие провайдеров.** Таймаут — не SQL-конструкт и не рендерится в текст, поэтому нет
`Supports*`/`Make*`-гейта: применяется один и тот же ADO-путь (`DbCommand.CommandTimeout` /
`DbBatch.Timeout`) на всех SQL-провайдерах. Провайдерные отличия — только фактический эффект драйвера,
они перечислены выше. Базовая семантика едина: `null`/`<= 0` — не трогать свойство.

## 3. C#-аналог и tier

Аналог — `System.Data.Common.DbCommand.CommandTimeout` (tier b: новое свойство конфигурации).
linq2db-имена: `UseCommandTimeout` / `WithCommandTimeout`.

## 4. Дизайн и публичный API

```csharp
// per-context
public DataContextBuilder UseCommandTimeout(int seconds);
public int? DataContextBuilder.CommandTimeout { get; }
public int? DataContext.CommandTimeout { get; }          // через IContextEnvironment
int? IContextEnvironment.CommandTimeout { get; }          // DIM => null

// per-query
public EntityBuilder WithCommandTimeout(int seconds);
public EntityBuilder<TEntity> WithCommandTimeout(int seconds);
public QueryCommand<TResult> WithCommandTimeout(int seconds);
public int? QueryCommand.CommandTimeout { get; internal set; }
internal int? QueryCommand.ResolvedCommandTimeout;        // участие в плане-ключе
```

- **Хранение:** `int?` в конфигурации контекста (`DataContextBuilder` → `ContextEnvironment` →
  `IContextEnvironment.CommandTimeout`) и `int?` на `QueryCommand`.
- **Применение:** `QueryPlanner.GetPreparedQueryCommand` при создании `DbCommand` для кэшируемой
  подготовленной команды ставит `ResolvedCommandTimeout` (per-query ?? context). `QueryExecutor`
  ставит контекстный таймаут на одноразовую mutation-команду (`CreateMutationCommand`); `BatchRunner` —
  на `DbBatch.Timeout`/соединённую команду. **Общее состояние не мутируется**: setter применяется к
  только что созданному `DbCommand`/`DbBatch`, а не к разделяемой `DbPreparedQueryCommand`.
- **План-ключ:** `ResolvedCommandTimeout` входит в `QueryPlanEqualityComparer` (`Equals` +
  `GetHashCode`) и переносится `QueryCommand.CopyTo` (в т.ч. cache-клон), поэтому:
  - две команды с разным таймаутом (per-query или context) не делят кэшированный `DbCommand`;
  - контексты с разными `UseCommandTimeout` не делят план (кэш `[ThreadStatic]` общий по типу контекста);
  - повторный запрос с тем же таймаутом переиспользует план, значение не «залипает».
- **Семантика `null`/`0`:** `UseCommandTimeout(<= 0)` → `null` (не задавать); `WithCommandTimeout(<= 0)`
  → снять per-query override (наследовать контекстный дефолт). Сам таймаут не рендерится в SQL,
  поэтому «zero-cost» путь не меняется.
- **DML/`ExecuteScalar`:** контекстный таймаут применяется к одноразовым mutation-командам; per-query
  override доступен только на query-командах (`EntityBuilder`/`QueryCommand<T>`).
- **Батчи:** контекстный таймаут применяется в `BatchRunner` (per-query на batch не переносится —
  документировано).

## 5. План тестов

- SQLite (без контейнеров, `tests/nextorm.sqlite.tests/CommandTimeoutTests.cs`): подготовка без
  открытия соединения, поэтому `DbCommand.CommandTimeout` проверяется напрямую.
  - контекстный таймаут попадает на подготовленную команду;
  - не задан → провайдерный дефолт (`SqliteCommand.CommandTimeout`);
  - per-query `WithCommandTimeout` перекрывает контекст (generic-билдер, non-generic `From(table)`,
    `QueryCommand<T>`);
  - разные per-query таймауты не делят план, одинаковый — переиспользует (`ReferenceEquals`);
  - per-query не «протекает» на последующий обычный запрос;
  - два контекста одного типа с разными `UseCommandTimeout` не делят план;
  - `WithCommandTimeout(0)` → провайдерный дефолт;
  - контекстный таймаут попадает на mutation-команду (`Delete ... All()`, наблюдается интерсептором).
- SQL-gen не нужен (SQL не меняется). Интеграция (опционально): реальный таймаут на PostgreSQL/SQL Server.

## 6. Файлы к изменению

- `src/nextorm.core/DI/DataContextBuilder.cs`
- `src/nextorm.core/DataContext/Roles/IContextEnvironment.cs`, `DataContext/ContextEnvironment.cs`,
  `DataContext/DataContext.cs`
- `src/nextorm.core/DataContext/QueryPlanner.cs`, `DataContext/QueryExecutor.cs`, `DataContext/BatchRunner.cs`
- `src/nextorm.core/Builders/EntityBuilder.cs` (generic + non-generic)
- `src/nextorm.core/Query/QueryCommand.cs`, `QueryCommand.TResult.cs`, `QueryCommand.Clone.cs`,
  `QueryCommand.QueryPreparer.cs`, `QueryPlanEqualityComparer.cs`
- Тесты: `tests/nextorm.sqlite.tests/CommandTimeoutTests.cs`
- Доки: `docs/guide/16-connections-and-logging.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/specs/design/API-NAMING-REVIEW.md`,
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`; план-файл — с разделом статуса.

## 7. Открытые вопросы

1. Название: `UseCommandTimeout` vs `UseCommandTimeoutSeconds` — принят `UseCommandTimeout` (совпадает
   с `BulkInsertOptions.TimeoutSeconds` и linq2db; суффикс секунд несёт XML-doc/тип `int`).
2. Per-query override в MVP — **входит** (`WithCommandTimeout` на builder/`QueryCommand<T>`).
3. ClickHouse: маппинг на HTTP — у `ClickHouse.Driver` 1.4.0 `CommandTimeout` не влияет на HTTP
   (см. §2), поэтому фактический эффект — no-op; документировано.

## Статус реализации

**DONE.** Фича реализована и покрыта тестами без контейнеров.

- **Реализовано:** `DataContextBuilder.UseCommandTimeout(int)` + `CommandTimeout`; `DataContext.CommandTimeout`
  и DIM `IContextEnvironment.CommandTimeout`; `WithCommandTimeout(int)` на non-generic/generic
  `EntityBuilder` и `QueryCommand<TResult>`; `QueryCommand.CommandTimeout`/`ResolvedCommandTimeout`;
  применение в `QueryPlanner` (prepared SELECT), `QueryExecutor.CreateMutationCommand` (DML/scalar) и
  `BatchRunner` (`DbBatch.Timeout`/соединённая команда); `ResolvedCommandTimeout` в
  `QueryPlanEqualityComparer` и `QueryCommand.CopyTo`.
- **Тесты:** `tests/nextorm.sqlite.tests/CommandTimeoutTests.cs` — 10 кейсов, все зелёные. Полные
  прогоны без контейнеров: core 401/0, sqlite 524/0, postgres 521/0, sqlserver 388/0, mysql 168/0,
  mariadb 93/0, clickhouse 338/0 (0 failed).
- **Build:** `dotnet build nextorm.slnx -c Release -m:2` — 0 warnings / 0 errors.
- **Доки EN+RU:** `guide/16-connections-and-logging.md` §«Command timeout» (+RU),
  `advanced/api-reference.md` (+RU), `advanced/limitations.md` (+RU),
  `specs/design/API-NAMING-REVIEW.md` (запись об изменении), gap-analysis §5 (запись в ledger).
- **Аудит:** `task`-субагенты недоступны в этой сессии, выполнен подробный self-review (план-ключ и
  cache-клон проверены, `_dontCache` не трогается, общее состояние не мутируется, XML-doc у всех новых
  публичных членов, CRLF сохранён).
