# TODO: `OUTPUT INTO`, несколько result-set'ов, upsert-with-output
> Tracking issue: [#15](https://github.com/AlexeyShirshov/nextorm/issues/15).

> Рабочий план (design RFC). Gap-анализ: **G4** из
> [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md); linq2db
> [#3832](https://github.com/linq2db/linq2db/issues/3832) (`...WithOutputIntoOutput`),
> [#2982](https://github.com/linq2db/linq2db/issues/2982) (multi-result-set),
> [#3124](https://github.com/linq2db/linq2db/issues/3124)/[#4824](https://github.com/linq2db/linq2db/issues/4824)
> (`InsertOrUpdate`/`InsertOrAction...WithOutput`). Пересекается с
> [`todo_stored_procedures.md`](todo_stored_procedures.md) (несколько наборов) и key-upsert/upsert
> ([guide 19](../../guide/23-merge-statement.md#upsert-key-merge)). Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

## Пункт и цель

Три связанных расширения DML-поверхности:

1. **`OUTPUT ... INTO <target>`** (SQL Server): записать изменённые строки в таблицу/табличную
   переменную, а не только отдать клиенту.
2. **Несколько result-set'ов**: одна команда (батч/процедура) отдаёт несколько наборов —
   материализовать их по порядку (`NextResult`-семантика).
3. **Upsert-with-output**: вернуть строки из *key-upsert* формы (`ON CONFLICT`/`ON DUPLICATE KEY`/
   `MERGE ... USING (VALUES ...)`), а не только из полного `MERGE`.

**Критерий приёмки:** новые формы отклоняются понятным `NotSupportedException` на провайдерах без
поддержки и покрыты SQL-gen + интеграцией; существующий `Returning()` не меняет поведение.

## Почему это нужно (мотивация)

1. **`OUTPUT INTO` — идиоматичный SQL Server** для «обнови и запиши в журнал/табличную переменную»;
   linq2db закрывает это `#3832`. nextorm умеет только `OUTPUT inserted.<col>` клиенту
   (`ISqlDialect.MakeOutput`, `ISqlDialect.cs:1080`).
2. **Multi-result** нужен для батчей и хранимых процедур (`#2982`); сегодня `rg NextResult` в core —
   пусто, reader читает ровно один набор.
3. **Upsert-with-output** — ключевой практический сценарий «вставь-или-обнови и верни строку»
   (`#3124`/`#4824`); в nextorm `Returning()` на `MergeBuilder` **требует full-`MERGE`-веток**, а
   key-upsert строк не возвращает (`MergeBuilder.cs:253-266`).

## Текущее состояние (проверено по коду)

- `SupportsReturning` (PostgreSQL, SQLite `RETURNING`) и `SupportsOutput` (SQL Server `OUTPUT`):
  `ISqlDialect.cs:1007-1016`; рендер `MakeReturning`/`MakeOutput` (`:1075`/`:1080`).
- Терминалы `Returning()` есть у `Insert`/`Update`/`Delete`/`Merge`; материализация —
  `IMutationExecutor.ExecuteReturning` (`DataContext.cs:319-335`), один набор.
- **`OUTPUT INTO` отсутствует**: нет `Into(...)`/`MakeOutputInto`, `MakeOutput` только
  `output inserted.<col>`.
- **Multi-result отсутствует**: `NextResult`/несколько наборов нигде не обрабатываются
  (`ResultSetEnumerator` читает один reader).
- **Key-upsert без output**: `MergeBuilder.Returning()` документирован «Requires the full-`MERGE`
  branch form; the key-upsert form does not return rows» (`MergeBuilder.cs:253-256`).

## Провайдерная матрица

Источники: MS Learn «OUTPUT Clause» (`OUTPUT ... INTO <target>`, `NextResult`); PostgreSQL 18
`RETURNING` (`WITH ... INSERT ... RETURNING`, `ON CONFLICT ... RETURNING`); SQLite `RETURNING` (3.35+);
MySQL 8.4 (`multiple result sets`, нет `RETURNING`); MariaDB KB (`RETURNING` в `INSERT`/`DELETE`);
ClickHouse.

| Провайдер | `OUTPUT ... INTO` | несколько result-set | upsert-with-output |
|---|---|---|---|
| SQL Server | да (`OUTPUT … INTO @tbl/target`) | да (`NextResult`) | `MERGE … OUTPUT` (full MERGE); key-upsert = `MERGE`, уточнить |
| PostgreSQL | нет (`RETURNING`; эквивалент — data-modifying CTE) | да (несколько инструкций/`refcursor`) | `ON CONFLICT … RETURNING` (9.5+); full `MERGE … RETURNING` (17+) |
| SQLite | нет | нет | `ON CONFLICT … RETURNING` (3.35+) |
| MySQL | нет | да | нет (нет `RETURNING`; только `LAST_INSERT_ID`) |
| MariaDB | нет | да | нет (`RETURNING` не для `ON DUPLICATE KEY`) — проверить |
| ClickHouse | нет | нет | нет |
| InMemory | нет | нет | нет (но вернуть строку из движкового upsert — можно) |

**Единообразие:** `OUTPUT INTO` — SQL-Server-only surface (`SupportsOutput` + новый `SupportsOutputInto`);
multi-result — по `SupportsMultipleResultSets` (SQL Server/PG/MySQL/MariaDB); upsert-with-output —
по `SupportsReturning`/`SupportsOutput` с учётом gate для full-MERGE.

## Дизайн и публичный API

### 1. `OUTPUT INTO`

```csharp
// на returning-билдерах Insert/Update/Delete
public InsertReturningBuilder<TEntity, TResult> Into<TTarget>(Expression<Func<TTarget, object>> columns, bool alsoReturnToClient = false);
```

- SQL Server рендерит `OUTPUT inserted.<col>, … INTO <target>(cols) [OUTPUT …]`. `<target>` —
  табличная переменная/таблица; существует два варианта: только `INTO` (клиенту ничего не идёт) и
  `INTO` + второй `OUTPUT` (linq2db `...WithOutputIntoOutput`) — уточнить синтаксис конкретного сервера
  (см. открытые вопросы).
- Провайдеры без `SupportsOutputInto` бросают `NotSupportedException`.
- Табличная переменная (`@t`) не может быть объявлена из nextorm-текста — либо принимать имя
  существующей таблицы, либо генерировать `DECLARE`-батч (фаза 2).

### 2. Несколько result-set'ов

```csharp
public IAsyncEnumerable<IReadOnlyList<T>> ReadAll<T>(...);   // или MultiResult
```

- `IQueryExecutor`/executor должен уметь `NextResult`; материализация каждого набора — существующим
  `RowMapperFactory.GetOrBuild<TResult>` (как `ExecuteReturning`).
- Основной потребитель — [`todo_stored_procedures.md`](todo_stored_procedures.md); для батчей
  nextorm-билдеров это менее нужно.

### 3. Upsert-with-output

- Снять ограничение «key-upsert не возвращает строк»: для `ON CONFLICT`-формы на PostgreSQL/SQLite
  рендерить `RETURNING`, для SQL Server `MERGE ... OUTPUT`.
- MySQL/MariaDB/ClickHouse — `NotSupportedException` (нет `RETURNING` для key-upsert).

## Этапы внедрения

1. `SupportsOutputInto` + `.Into(...)` на `Insert`/`Update`/`Delete` (SQL Server); SQL-gen + интеграция.
2. `ReadAll`/multi-result + `NextResult` в executor (SQL Server/PG/MySQL/MariaDB); связка со SP.
3. Key-upsert `Returning()` (PG/SQLite `ON CONFLICT ... RETURNING`, SQL Server `MERGE ... OUTPUT`).

## План тестов

- SQL-gen (`tests/nextorm.sqlserver.tests`): `OUTPUT … INTO`; `tests/nextorm.postgres.tests`/`sqlite`:
  `ON CONFLICT … RETURNING`; `tests/nextorm.mysql.tests`: gate `NotSupportedException`.
- Интеграция: SQL Server — `OUTPUT INTO` реальной таблицы и `NextResult` двух наборов; PostgreSQL —
  upsert `RETURNING`; SQLite — upsert `RETURNING`; MySQL — multi-result.
- Покрытие: SQL Server/PostgreSQL в `coverage.settings.xml`; MySQL вне — зафиксировать.

## Открытые вопросы

1. `Into` — на возвращающем билдере или отдельный `OutputInto(...)`? Нужен ли режим «и INTO, и клиенту»
   (второй `OUTPUT`), и как он рендерится на разных версиях SQL Server.
2. Табличная переменная (`DECLARE @t TABLE …`) — генерировать батч или принимать имя готовой таблицы?
3. Multi-result API: `IAsyncEnumerable<IReadOnlyList<T>>` vs `MultiResult` с типизированными наборами.
4. Нужен ли multi-result вне хранимых процедур (пересечение с `todo_stored_procedures.md`).
5. Как key-upsert `RETURNING` сочетается с `MergeBuilder` (единый билдер, [guide 19](../../guide/23-merge-statement.md#upsert-key-merge)).
6. MariaDB: реально ли `RETURNING` в key-upsert (по докам — нет) — gate vs документировать.

## Файлы к изменению

- Новое: `src/nextorm.core/Builders/OutputIntoBuilder.cs` (или расширение `*ReturningBuilder`),
  тесты SQL-gen/интеграции.
- Правки: `DataContext/Dialect/ISqlDialect.cs` (`SupportsOutputInto`, `MakeOutputInto`),
  `DataContext/SqlMutationBuilder.cs:715` (рендер output/returning), `DataContext/DataContext.cs:319-344`
  (материализация, multi-result), `DataContext/ResultSetEnumerator.cs` (`NextResult`),
  `Builders/MergeBuilder.cs:253-282` (key-upsert returning),
  `src/nextorm.sqlserver/SqlServerDialect.cs`, executor в `nextorm.sqlserver`/`nextorm.postgres`.
- Документация: `docs/advanced/limitations.md` (гигантский DML-абзац, `:15`; +RU),
  `docs/guide/19-insert-statement.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **нужен пересмотр — 2 блокера** (контракт `Into`, устаревший/дублирующий multi-result).

- **[LSP] 🔴** `Into()` возвращает `InsertReturningBuilder<TEntity, TResult>` (`:78`), но при `alsoReturnToClient=false` клиентского набора нет: `ToList()` пуст, `Single()` бросает (`Builders/InsertReturningBuilder.cs:22,67,102,185`), `TResult` нечем заполнить (Q1 `:122`). Fix: отдельный `OutputIntoBuilder` без row-терминалов либо non-returning билдер; `INTO`+второй `OUTPUT` — отдельным методом.
- **[DRY] 🔴** «multi-result отсутствует» (`:46-47`) устарело: навигация `NextResult` уже есть для батча (`DataContext/BatchRunner.cs:199-219`, `AdvanceToResultSet`, вызывается из `RunBatch*`); предлагаемые `ReadAll<T>`/`MultiResult` (`:89-98`) её дублируют. Fix: переиспользовать существующий обход; `ResultSetEnumerator.cs` — не место навигации (`:136`).
- **[DIP] 🟡** `SupportsOutputInto`/`SupportsMultipleResultSets` (`:68-70,134`) — делать DIM `=> false` (+ `SqlDialectBase virtual`), не abstract, иначе source-разрыв (инварианты 5/7).
- **[TYPE] 🟡** `Expression<Func<TTarget, object>>` (`:78-88`) боксит value-колонки на планировании и не даёт имя таблицы/`[SqlTable]` для `TTarget`. Fix: явное имя целевой таблицы либо резолв через `IEntityMetadata` (как `From<T>`).
- **[SRP] 🟡** upsert-with-output не сведён по провайдерам: `MergeBuilder.Returning()` документирует «key-upsert не возвращает строк» (`Builders/MergeBuilder.cs:253-259`), план оставляет MariaDB «проверить»/SQL Server «уточнить» (`:60,64,128`). Deferred: после фиксации матрицы.
- **[DRY] ℹ️** Устаревшие якоря: `ISqlDialect.cs:1007-1016/1075/1080` → `:1172/1179/1238/1243`; `DataContext.cs:319-335` → `:372-388`; `MergeBuilder.cs:253-266` → `:260-282`.
- **[DIP] ℹ️** `alsoReturnToClient` — boolean blindness; согласовать имя с API-NAMING (`Into` vs linq2db `...WithOutputIntoOutput`).
