# WIP: `OPENJSON ... WITH` (типизированная схема)

> Рабочий план по скилу `implementing-todo-features`. Источник: `todo_mssql.md:108-112`
> (дубль в `todo_phase2.md:29`).

## Пункт и цель

- Пункт бэклога: встроенный `SqlFunctions.SqlServer.openjson(json)` отдаёт **схему по умолчанию**
  (`key`/`value`/`type`). Осталось: типизированная схема `WITH (col type '$.path', ...)`, дающая
  именованные колонки с типами.
- Бэклог сам отмечает: «пользовательский `[SqlTableFunction]`-враппер уже покрывает этот случай;
  встроенная поддержка — опционально».
- Цель: сделать так, чтобы враппер **и правда** покрывал `WITH`, то есть дать `[SqlTableFunction]`
  возможность дописать хвостовое `with (...)`.

## Матрица «провайдер × форма» (шаг 1)

Форма: «разобрать JSON в строки с именованными типизированными колонками (schema-on-read)».
Заполнена по документации СУБД.

| Провайдер | Нативная форма | Источник |
| --- | --- | --- |
| PostgreSQL | `json_to_recordset(json) AS x(col type, ...)` / `jsonb_to_recordset`; скаляр — `json_to_record` | https://www.postgresql.org/docs/current/functions-json.html |
| SQL Server | `OPENJSON(json) WITH (col type '$.path', ...)` (2016+); схема по умолчанию без `WITH` | MS Learn: `OPENJSON (Transact-SQL)` |
| MySQL | `JSON_TABLE(json, '$[*]' COLUMNS(col type PATH '$.x', ...))` (8.0+) | https://dev.mysql.com/doc/refman/8.4/en/json-table-functions.html |
| MariaDB | `JSON_TABLE(...)` (10.6+) | https://mariadb.com/kb/en/json_table/ |
| ClickHouse | типизированного «shred в строки» TVF нет; покомпонентно через `JSONExtract*`/`JSON_QUERY` | https://clickhouse.com/docs/en/sql-reference/functions/json-functions |
| SQLite | `json_each`/`json_tree` дают динамическую схему; типизация — `json_extract` по колонкам | https://www.sqlite.org/json1.html |
| InMemory | неприменимо: TVF — только SQL | — |

Вывод: «типизированный JSON-rowset» есть у PostgreSQL, SQL Server и MySQL/MariaDB, но с **совершенно
разным синтаксисом** и требует row-shape/типов, которых у nextorm нет для PG/MySQL (в
`sql-capabilities-gap-analysis.md` gap #4 `JSON_TABLE`, `jsonb_to_record(set)` уже отмечены как
несмэпленные). Поэтому единый кросс-провайдерный API не вводится; в рамках MSSQL-пункта расширяется
существующий provider-native механизм `[SqlTableFunction]` (пользовательские TVF не гейтятся и уже
эмитятся дословно — см. `SqlSourceRenderer.MakeTableFunction`).

## Решение и тир

**Тир (b/c-гибрид в рамках существующего механизма):** добавить в `SqlTableFunctionAttribute`
необязательное свойство `WithClause`, которое `SqlSourceRenderer` дописывает как ` with (<clause>)`
после вызова функции и перед псевдонимом. Тогда:

```csharp
public interface IOpenJsonTypedRow
{
    [Column("name")] string? Name { get; set; }
    [Column("age")] int Age { get; set; }
}

[SqlTableFunction("openjson", WithClause = "name nvarchar(50) '$.name', age int '$.age'")]
public static IQueryable<IOpenJsonRowTyped> OpenJsonTyped(string json) => throw new NotSupportedException();
```

рендерит `from openjson(@json) with (name nvarchar(50) '$.name', age int '$.age') as [t1]`, а row-shape
интерфейс даёт CLR-типы для проекции. Это:
- не вносит T-SQL в ядро (пользователь пишет provider-нативный suffix так же, как в `[SqlFunction]`);
- extend-only: новое необязательное свойство атрибута + перегрузка конструктора
  `TableFunctionExpression`;
- работает для любого TVF с хвостовым `with (...)`, не только `openjson`.

Встроенный generic-оверлоад `SqlFunctions.SqlServer.openjson<T>(json, withClause)` не добавляется:
row-shape уже задаётся враппером пользователя, а generic-возврат не поддерживается текущей моделью TVF.

## Диалектный план

- `Supports*`/`Make*` не добавляются: `WITH` — синтаксис самого пользовательского TVF, эмитится
  дословно. Встроенный `openjson` остаётся под `ISqlDialect.SupportsTableFunction("openjson")`.

## Публичный API

- `SqlTableFunctionAttribute.WithClause` (`string?`, get/set) + XML-doc.
- `TableFunctionExpression`: свойство `WithClause` (`string?`) + новая перегрузка конструктора
  (`name, schema, withClause, call`); старая сигнатура сохранена (extend-only).

## План тестов

- SQL-gen (SQL Server): `SqlGenerationTests.OpenJsonWith_ShouldAppendWithClause` — враппер с
  `WithClause` рендерит `... from openjson(@json) with (...) as [t1]`.
- Интеграция (SQL Server): `SqlServerSpecificTests.OpenJson_WithTypedSchema_ShouldReturnTypedColumns`
  — реальный `OPENJSON ... WITH` возвращает типизированные значения.
- Coverage: новые строки прод-кода — 1 ветка в `SqlSourceRenderer`; доля строк не падает.

## Файлы документации

- `docs/guide/13-table-valued-functions.md` (+RU) — как задать `WITH` через `WithClause`.
- `docs/providers/sqlserver.md` (+RU) — пример типизированного `OPENJSON`.
- `docs/specs/roadmap/todo_mssql.md`, `todo_phase2.md`, `sql-capabilities-gap-analysis.md`.

## Итог

- Реализовано свойство `SqlTableFunctionAttribute.WithClause` + перегрузка конструктора
  `TableFunctionExpression`; `SqlSourceRenderer.MakeTableFunction` дописывает ` with (...)`.
- Тесты: SQL-gen `SqlGenerationTests.OpenJsonWith_ShouldAppendWithClause` (194/194 по проекту) и
  интеграционный `SqlServerSpecificTests.OpenJson_WithTypedSchema_ShouldReturnTypedColumns`
  (реальный SQL Server через Testcontainers — зелёный); `dotnet build nextorm.sln -c Release` — 0/0.
- Гайд EN/RU, чекбокс `todo_mssql.md` `[x]`, строка `todo_phase2.md` «закрыто», gap-analysis
  обновлена. WIP закрыт.
