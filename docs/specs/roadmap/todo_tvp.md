# TODO: table-valued parameters (TVP)
> Tracking issue: [#73](https://github.com/AlexeyShirshov/nextorm/issues/73).

> Рабочий план (design RFC). Источник — **G8** из
> [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md):
> `linq2db#1645`. Тесно связано с [`todo_stored_procedures.md`](todo_stored_procedures.md) (TVP чаще
> всего передаётся в процедуру) и с `todo_dynamic_result_schema.md` (серверные `values()`).
> Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

## 1. Пункт и цель

- **Фича:** передать таблицу строк **параметром** (не источником `FROM`): SQL Server — нативный
  TVP (`SqlDbType.Structured` + `TypeName`), остальные — документированная эмуляция.
- **Критерий приёмки:** `ctx` умеет выполнить `INSERT ... SELECT ... FROM @tvp` / вызов процедуры с
  TVP-параметром на SQL Server; на PG — эквивалент через массив + `unnest`/`jsonb_to_recordset`;
  неподдержанная комбинация отклоняется понятным `NotSupportedException`; in-memory — по CLR-коллекции.
- **Не про TVF-источники:** `[SqlTableFunction]`/`FromTableFunction` уже умеют таблицу как **источник**
  запроса; здесь — параметр.

## 2. Почему это нужно

1. **Батч-операции без N round-trip:** передать 1000 строк одним параметром на SQL Server вместо
   1000 `INSERT`; linq2db закрывает это в `#1645`.
2. **Вызов хранимых процедур с табличным аргументом** — основной сценарий TVP; без SP-поверхности
   (см. `todo_stored_procedures.md`) ценность ограничена.
3. nextorm уже умеет TVF как источник и `IN`-по-набору скаляров, но не «таблица как параметр».

## 3. Текущее состояние (проверено по коду)

- Ни `SqlDbType.Structured`, ни `DataTable`/`SqlDataRecord`, ни `IEnumerable<T>`-параметров в `src/`
  нет; `CommandType` не выставляется (всё — текстовый SQL), см. `todo_stored_procedures.md`.
- TVF-источник есть: `[SqlTableFunction]` + `FromTableFunction` (`SqlFunctions.Postgres.cs:578-592`
  `regexp_matches`, `SqlFunctions.cs` row-интерфейсы).
- Скалярный набор для `IN`: `CommonFunctions.@in<T>(T, IEnumerable<T>/params T[])`
  (`Query/SqlFunctions.cs:452-458`).

## 4. Матрица провайдеров

Источники: MS Learn «Table-valued parameters» (`SqlDbType.Structured`, `TypeName`, `DataTable`/
`DbDataReader`/`IEnumerable<SqlDataRecord>`, input-only); PostgreSQL 18 §9.19/§8.15 (arrays,
`unnest`, `jsonb_to_recordset`); MySQL 8.0 JSON (`JSON_TABLE`); MariaDB `JSON_TABLE` (10.6+); SQLite
JSON1 (`json_each`); ClickHouse `values()`/`input()`.

| Провайдер | native TVP | Форма | Источник |
|---|---|---|---|
| SQL Server | **да** | `SqlParameter { SqlDbType = Structured, TypeName = "dbo.T" }`, значение `DataTable`/`DbDataReader`/`IEnumerable<SqlDataRecord>`; input-only, нужен user-defined table type | MS Learn |
| PostgreSQL | — | эмуляция: массив + `unnest(arr)` / `= ANY(arr)`, либо `jsonb_to_recordset($1)` | PG 18 |
| MySQL | — | эмуляция: `JSON_TABLE` (8.0) / derived table (`UNION ALL`) | MySQL ref |
| MariaDB | — | эмуляция: `JSON_TABLE` (10.6+) / derived table | MariaDB KB |
| ClickHouse | — | `values('name Type, …', …)` / `input()`; связано с `todo_dynamic_result_schema.md` | ClickHouse |
| SQLite | — | эмуляция: `json_each` (JSON1) / temp table | sqlite.org |
| InMemory | — | CLR-коллекция напрямую | — |

**Единообразие:** нативная поддержка — **только SQL Server** (provider-surface); PG/MySQL/MariaDB/
SQLite/ClickHouse получают эмуляцию, если/когда она нужна (фаза 3). Гейт `SupportsTableValuedParameters`
(default `false`).

## 5. Ближайший CLR-аналог и тир

- Аналог — `IEnumerable<T>`/`IList<T>` как параметр, сопоставленный пользовательскому табличному типу.
- Тир **(b)**: provider-surface на SQL Server (`SqlServerDataContext` + новый публичный тип для
  табличного параметра); `[SqlFunction]` не подходит (нужен `SqlDbType`/`TypeName`, а не name-swap).

## 6. Дизайн и публичный API

```csharp
// SQL Server provider
public sealed class TableValuedParameter<T>
{
    public TableValuedParameter(string sqlTypeName, IEnumerable<T> rows);
}
```

- Точки входа (SQL Server): перегрузка создания параметра/`ctx.ExecuteRawAsync` с
  `TableValuedParameter<T>` → `SqlParameter { SqlDbType = Structured, TypeName = ... }`; для потока —
  `IEnumerable<SqlDataRecord>`/`DbDataReader`.
- Эмуляция (PG): `ctx.Parameter<T>("jsonb", rows)` + `FromTableFunction`/`jsonb_to_recordset`; либо
  helper `Unnest(rows, columnDef)`.
- In-memory: коллекция напрямую.
- Пока нет SP-поверхности, единственный сценарий — `INSERT ... SELECT ... FROM @tvp` через сырой SQL
  (`WithSql`); поэтому §7 фаза 1 имеет смысл только вместе с `todo_stored_procedures.md`.

## 7. Этапы внедрения

1. **SQL Server TVP**: тип `TableValuedParameter<T>`, `Structured`-параметр, `TypeName`; SQL-gen +
   интеграция (container) на `INSERT ... SELECT FROM @tvp`.
2. **Связка со SP**: передача TVP в `ExecuteProcedure` (см. `todo_stored_procedures.md`).
3. **Эмуляции**: PG `unnest`/`jsonb_to_recordset`, MySQL/MariaDB `JSON_TABLE`, SQLite `json_each`,
   ClickHouse `values()` — каждая под своим gate и с `ShouldThrow` на остальных.

## 8. План тестов

- SQL-gen (`tests/nextorm.sqlserver.tests`): параметр `Structured` + `TypeName` в тексте команды.
- Интеграция (`tests/nextorm.integration.tests/SqlServerSpecificTests.cs`): создать user-defined
  table type, `INSERT ... SELECT FROM @tvp`, вызов процедуры с TVP; пустой набор; null-колонки.
- PG-эмуляция: `unnest`/`jsonb_to_recordset` round-trip (`PostgresSpecificTests.cs`).
- In-memory: коллекция-параметр.
- Покрытие: SQL Server входит в `coverage.settings.xml`; PG-эмуляция тоже.

## 9. Открытые вопросы

1. Делать ли TVP сразу или после `todo_stored_procedures.md` (основной сценарий — SP).
2. API: `TableValuedParameter<T>` (provider-specific) vs cross-provider `SetParameter<T>`.
3. SQL Server: `IEnumerable<SqlDataRecord>` (стриминг) vs `DataTable` (проще, но требует
   `Microsoft.Data.SqlClient`-типов) — не тянуть ли провайдерские типы в публичный API.
4. Нужны ли эмуляции на PG/MySQL/MariaDB, или ограничиться SQL Server + документировать.
5. Связь с `@in`-набором: не дублировать ли (TVP шире, но `@in` уже есть).

## 10. Файлы к изменению

- Новое: `src/nextorm.sqlserver/TableValuedParameter.cs` (или в core с provider-хуком).
- Правки: `src/nextorm.sqlserver/SqlServerDataContext.cs` (`CreateParam`/`Structured`),
  `DataContext/DataContext.cs` (создание команды/параметра), при эмуляциях — соответствующие диалекты
  и `SqlFunctions.*`.
- Доки: `docs/providers/sqlserver.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/specs/design/API-NAMING-REVIEW.md`.
