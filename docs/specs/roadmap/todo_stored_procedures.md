# TODO: хранимые процедуры и функции (вызов, output-параметры, несколько result-set)
> Tracking issue: [#70](https://github.com/AlexeyShirshov/nextorm/issues/70).

> Рабочий план (design RFC). Отдельный workstream (не нумерованный gap из linq2db-разбора, но
> связан с **G8**/TVP: [`todo_tvp.md`](todo_tvp.md)). Сейчас вызов процедур **явно вне scope** —
> `docs/advanced/limitations.md:42` («nextorm builds `SELECT` statements from LINQ; calling a stored
> procedure or assembling SQL at runtime is outside the builder surface»). Публичный API →
> `docs/specs/design/API-NAMING-REVIEW.md`.

## 1. Пункт и цель

- **Фича:** выполнить хранимую процедуру/функцию, получить её result-set'ы, `OUT`/`INOUT`-параметры и
  return value, а также выполнить произвольную параметризованную команду (не только `SELECT`).
- **Критерий приёмки:** `ctx` умеет вызвать процедуру и материализовать строки тем же путём, что и
  LINQ-запрос (кэш mapper'а, `IDataRecord`); SQL Server отдаёт `OUT`/return value и несколько наборов;
  PG/MySQL/MariaDB — вызов и (при наличии) `OUT`/несколько наборов; SQLite/ClickHouse — понятный
  `NotSupportedException`; ограничение в `limitations.md` обновлено.
- **Ограничение дизайна (AGENTS):** не расширять `IQueryExecutor` — сделать узкую роль
  (default-реализации интерфейса) рядом с существующими командами.

## 2. Почему это нужно

1. **Зрелые схемы живут в процедурах:** вход по `CALL`/`EXEC`, `OUT`-параметры, несколько наборов —
   востребованный сценарий, который nextorm сегодня отвергает by design.
2. **Нужно для TVP** (`todo_tvp.md`): табличный параметр почти всегда передаётся в процедуру.
3. **Сырой параметризованный SQL** (не `SELECT`) — родственный пробел: `WithSql` даёт сырой `SELECT`
   как источник, но не произвольную команду с параметрами/`Direction`.

## 3. Текущее состояние (проверено по коду)

- `CommandType` в `src/nextorm.core/` **не выставляется** (всё — текст): `DataContext.cs:184`
  `cmd.CommandText = sql`; исполнение — `ExecuteScalar`/`ExecuteNonQuery`/`ExecuteReader`
  (`DataContext/ResultSetEnumerator.cs:215,237`).
- Материализация строк — `RowMapperFactory`/`RowMaterializerBuilder` (пригодна для SP-result-set).
- `WithSql` — сырой `SELECT`-источник (см. `limitations.md:42`); произвольной команды нет.
- `docs/advanced/limitations.md:42` (EN+RU) прямо перечисляет SP и dynamic SQL как вне scope.

## 4. Матрица провайдеров

Источники: MS Learn (`SqlCommand.CommandType = StoredProcedure`, `ParameterDirection`, `NextResult`);
PostgreSQL 18 §9.19/`CALL` (PG11+) и `SELECT * FROM func()`; MySQL 8.0 stored routines (`CALL`,
`OUT`/`INOUT`); MariaDB `CALL`; SQLite (нет процедур); ClickHouse (нет процедур).

| Провайдер | Вызов | OUT/INOUT | Return value | Несколько result-set | Источник |
|---|---|---|---|---|---|
| SQL Server | `EXEC name @p…` (`CommandType.StoredProcedure`) | `ParameterDirection.Output`/`InputOutput` | `ReturnValue` | да (`NextResult`) | MS Learn |
| PostgreSQL | функция — `SELECT * FROM f(args)`; процедура (11+) — `CALL p(args)` | `OUT`/`INOUT` (функции); `refcursor` (процедуры) | функция возвращает value/row; процедура — нет | да (несколько инструкций/`refcursor`) | PG 18 |
| MySQL | `CALL p(args)` | `OUT`/`INOUT` (`MySqlParameter.Direction`) | — | да | MySQL ref |
| MariaDB | `CALL p(args)` | `OUT`/`INOUT` | — | да | MariaDB KB |
| ClickHouse | — (нет процедур; есть executable UDF) | — | — | — | ClickHouse |
| SQLite | — (нет процедур) | — | — | — | sqlite.org |
| InMemory | — | — | — | — | — |

**Единообразие:** реально поддержаны SQL Server / PostgreSQL / MySQL / MariaDB; ClickHouse/SQLite/
InMemory — gate-off (`SupportsStoredProcedures => false`). PostgreSQL-специфика (functions vs
`CALL`-procedures) — отдельный под-режим.

## 5. Ближайший CLR-аналог и тир

- Аналог — `System.Data.Common.DbCommand` с `CommandType.StoredProcedure` и
  `DbParameter.Direction`.
- Тир **(b)**: новый метод/роль на `IDataContext`/`DataContext` (`ExecuteProcedure`,
  `ExecuteProcedureAsync`) + результат-объект с наборами и выходными параметрами; `[SqlFunction]` не
  подходит (это не функция внутри LINQ-дерева).

## 6. Дизайн и публичный API

```csharp
public sealed class ProcedureResult
{
    public IReadOnlyList<DbParameter> OutputParameters { get; }
    public object? ReturnValue { get; }
    public IAsyncEnumerable<T> ReadAsync<T>(CancellationToken ct = default);
    public IReadOnlyList<T> Read<T>();
}

// IDataContext (узкая роль, default-реализация)
Task<ProcedureResult> ExecuteProcedureAsync(string name, object?[] parameters, CancellationToken ct = default);
```

- `CommandType = StoredProcedure`; параметры — с `Direction`; name — как есть (без параметризации
  имени).
- Материализация набора — через существующий `RowMapperFactory` (SQL + `selectList`), как у
  `Returning`/мутаций (`RowMapperFactory.GetOrBuild<TResult>(string sql, …, selectList, …)`).
- Несколько наборов — `ProcedureResult` с упорядоченным доступом (`Read<T>()` по индексу набора).
- PG: отдельный режим `Function` (`SELECT * FROM f(...)`) vs `Procedure` (`CALL`); `OUT`-параметры —
  через `SELECT`-проекцию функции.
- In-memory/ClickHouse/SQLite — `NotSupportedException` с внятным текстом.
- Сырая параметризованная команда — отдельный, меньший по объёму метод `ExecuteRaw`/`ExecuteRawAsync`
  (text + параметры + `Direction`), как фундамент под SP.

## 7. Этапы внедрения

1. `ExecuteRaw`/`ExecuteRawAsync` (текст + параметры, без `SELECT`-builder'а) — снимает самое узкое место.
2. `ExecuteProcedure` (SQL Server `CommandType.StoredProcedure`; PG/MySQL `CALL`) + материализация
   первого набора.
3. `OUT`/`INOUT`-параметры и return value (SQL Server; PG-функции).
4. Несколько result-set'ов.
5. Интеграция с TVP (`todo_tvp.md`).

## 8. План тестов

- SQL-gen/команда: проверка `CommandType`/направления параметров (без БД сложно — dialect/контекст).
- Интеграция (containers): `SqlServerSpecificTests.cs` — `OUT`/return/несколько наборов;
  `PostgresSpecificTests.cs` — функция `SELECT * FROM f()` и `CALL`-процедура; `MySqlSpecificTests.cs`
  — `CALL` + `OUT`; SQLite/ClickHouse — `ShouldThrow`.
- Core: `ExecuteRaw` (текст+параметры) на SQLite.
- Покрытие: SQL Server/PG в `coverage.settings.xml`; MySQL/ClickHouse — вне, зафиксировать.

## 9. Открытые вопросы

1. Названия: `ExecuteProcedure`/`ExecuteStoredProcedure`/`ExecuteRaw`.
2. `ProcedureResult` как отдельный тип vs `IAsyncEnumerable<T>` напрямую.
3. Как быть с несколькими result-set'ами в API (индекс vs последовательный `NextResult`).
4. PG: различать ли функцию и процедуру явным флагом или автоопределением.
5. Не нарушает ли SP-поверхность текущий принцип «всё через LINQ» — согласовать объём (только вызов,
   без маппинга параметров из выражений).
6. Обновление `limitations.md` (EN+RU): что именно остаётся вне scope после реализации.

## 10. Файлы к изменению

- Новое: `src/nextorm.core/DataContext/ProcedureResult.cs`, роль/методы в `DataContext` (или отдельный
  `IStoredProcedureExecutor` с default-реализацией), при необходимости `Visitors`-обвязка отсутствует.
- Правки: `src/nextorm.core/DataContext/DataContext.cs`, `IQueryExecutor`-соседние роли (не расширять
  сам executor), `DataContext/ResultSetEnumerator.cs` (материализация набора),
  provider-специфика — `nextorm.postgres/PostgresDataContext.cs`, `nextorm.mysql/*`,
  `nextorm.sqlserver/SqlServerDataContext.cs`.
- Доки: `docs/advanced/limitations.md` (+RU — убрать/сузить пункт), `docs/guide/` новая страница
  «Stored procedures» (+RU + `toc.yml`), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **нужен пересмотр — 1 блокер** (владение reader'ом/`IDisposable`).

- **[TYPE] 🔴** `ProcedureResult` (`:69-75`) отдаёт `IAsyncEnumerable<T>`/`IReadOnlyList<T>` над открытым reader'ом, но не `IDisposable`/`IAsyncDisposable` и не говорит, кто закрывает reader/команду; текущий reader освобождает `ResultSetEnumerator<TResult>` (`src/nextorm.core/DataContext/ResultSetEnumerator.cs:96-115`). Fix: `ProcedureResult : IAsyncDisposable` (или eager-материализация) + описать владение в XML-doc.
- **[DIP/ISP] 🟡** `OutputParameters : IReadOnlyList<DbParameter>` (`:71`) протаскивает ADO.NET-тип и mutable-элемент в публичную поверхность. Fix: immutable `readonly record struct ProcedureOutputParameter(string Name, object? Value, ParameterDirection Direction)`, `DbParameter` — internal.
- **[SRP/ISP] 🟡** Новый метод/роль на `IDataContext` (`:77-79,122-123`): после F2 `IDataContext` — пустой композит ролей (`src/nextorm.core/DataContext/IDataContext.cs:17-24`); SP-роль вернёт gate-off члены, от которых F2 избавлялся. Fix: extension над `IDataContext`, реализация на `DataContext`/`InMemoryQueryExecutor` с DIM; не расширять композит.
- **[TYPE] 🟡** `Read<T>()`/`ReadAsync<T>()` без индекса набора (`:73-74`) против «по индексу набора» (`:85`); повторный вызов не определён. Fix: `Read<T>(int resultSetIndex = 0)` + `ResultSetCount` либо forward-`NextResult`-итератор.
- **[DRY/TYPE] 🟡** Две несовместимые формы параметров: `ExecuteProcedure(string, object?[])` (`:78`) и `ExecuteRaw` «text + параметры + Direction» (`:89-90`). Fix: одна descriptor-форма, переиспользуемая обоими.
- **[OCP] 🟡** §4/§6 вводят гейт `SupportsStoredProcedures` и PG-режим Function/Procedure (`:54-56,86-88`), но не называют член диалекта; `ISqlDialect` уже 97 членов и не дробится (F12). Fix: DIM `SupportsStoredProcedures` + `MakeProcedureCall` по образцу `DialectCapabilities.cs`.
- **[PERF/SRP] 🟡** Путь материализации не определён: `RowMapperFactory.GetOrBuild<TResult>(sql, …, selectList, …)` (`:83-84`) требует `SelectExpression[]`, которого у произвольного `T` нет; наличный per-type источник — `DataContextCache.SelectListCache` (`src/nextorm.core/DataContext/DataContextCache.cs:23`), план его не называет. Fix: зафиксировать деривацию select-list.
- **[PERF] ℹ️** `RowMapperFactory` кэширует mapper по SQL-тексту (`:123`), а `ExecuteRaw` допускает произвольный SQL → miss и рост `MapperCache`. Deferred (метрика `MapperCache`).
- **[DRY] 🟡** Устаревшие §3-якоря: `DataContext.cs:184` → `src/nextorm.core/DataContext/DataContext.cs:235` и `QueryExecutor.cs:273`; `ResultSetEnumerator.cs:215,237` → `ExecuteReaderCore`/`ExecuteReaderAsync`, а `ExecuteScalar`/`ExecuteNonQuery` — `QueryExecutor.cs:292,315`; `limitations.md:42` → `docs/advanced/limitations.md:46`. Fix.
