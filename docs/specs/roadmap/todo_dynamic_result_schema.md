# TODO: Динамическая схема результата (ClickHouse `values()`, PostgreSQL `jsonb_to_record(set)`)
> Tracking issue: [#63](https://github.com/AlexeyShirshov/nextorm/issues/63).

> Рабочий план (design RFC). Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.11.

## Пункт и цель

- Проблема: источники с динамической (задаваемой в рантайме) схемой не поддержаны — ClickHouse
  `values('a UInt8, b String', ...)`, PostgreSQL `jsonb_to_record(set)`/`json_populate_record(set)`.
  Схема известна только из аргумента, а не из `TEntity`.
- Цель: способ объявить схему источника явно (например, `TableAlias`/`DefineColumns`) и читать строки.
- Критерий приёмки: SQL-gen + интеграционный тест на `values(...)` (CH) и `jsonb_to_record(set)`
  (PG) с явной схемой; прочие провайдеры — `NotSupportedException`.

## Текущее состояние

- Обычные TVF гейтятся `SupportsTableFunction`; динамическая схема — вне модели. TVF-механизм теперь
  умеет `CallClause`/`VerbatimArguments` (см. `docs/guide/13-table-valued-functions.md`), но
  `jsonb_to_record(set)` по-прежнему требует явного описания схемы результата.
- См. также серверные TVF ClickHouse (уже реализованы с generic-схемой `TRow`):
  [Table-valued functions](../../guide/13-table-valued-functions.md#built-in-table-functions);
  `format`/`merge`/`input` остаются здесь.

## Дизайн (черновик)

- Явное описание колонок результата (имена + типы) на билдере источника.
- Ридер по объявленной схеме, а не по `TEntity`-маппингу.

## Открытые вопросы

1. API описания схемы: `TableAlias`-подобный builder vs `[Column]`-DTO для `TResult`.
2. Как сочетать с план-кэшем (схема — часть ключа плана).
3. Ограничиться ли `string`/примитивами в первой фазе.

## Файлы к изменению

- `src/nextorm.core/Builders/*`, `DataContext/RowMapper*`, `Query/SqlFunctions.*.cs`,
  провайдерные `*Dialect.cs`.
- Тесты: SQL-gen + integration.
- Доки EN+RU, gap-analysis.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only, ветка `1.0.6-alpha`, dirty worktree).
> `file:line` — по дереву на момент ревью, перепроверять перед реализацией. Правок в код не вносилось.
> Вердикт: **нужен пересмотр — 2 блокера**; черновик слишком ваг (нет API и контракта ридера).

- **[DRY]/[OCP] 🔴** Третий механизм декларации схемы (TableAlias-подобный builder / `DefineColumns`): `:24-27,31`. Уже есть `TRow`-generic TVF (`Query/SqlFunctions.ClickHouse.cs:392-420`) и `SqlTableFunctionAttribute` c `WithClause`/`CallClause` (`SqlTableFunctionAttribute.cs:54-65`). Fix: расширить атрибут рендером column-definition-list из метаданных `TRow`, а не плодить параллельный builder (инвариант 1).
- **[DIP] 🔴** Нет контракта ридера/`TResult`: `:27,31` не говорит, во что материализуются строки (`object[]`? `IDataRecord`? `[Column]`-DTO?) и как схема доходит до `RowMapperFactory.GetOrBuild` (`DataContext/RowMapperFactory.cs:87`). Fix: зафиксировать носитель схемы до реализации.
- **[PERF] 🟡** План-кэш не закрыт (Q2, `:32`): схема обязана входить в ключ `From`/колонок (`Query/QueryPlanEqualityComparer.cs:133-135`, `FromExpressionPlanEqualityComparer`). Fix: включить схему в хеш.
- **[TYPE] 🟡** Незапечатанная accessor-поверхность: `TableAlias`/`TableColumn` — `public class` (`Builders/TableAlias.cs:8,101`). Fix: переиспользовать `TableAlias`, запечатать при расширении.
- **[DRY] 🟡** Неверный указатель файлов `:37`: TVF-источник живёт в `SqlTableFunctionAttribute.cs` + `DataContextExtensions.FromTableFunction:37`, а не в `Query/SqlFunctions.*.cs`. Fix.
- **ℹ️** Не хватает: формы аргумента CH `values('a UInt8, b String', …)`, рендера PG `AS x(col type, …)` и провайдерной матрицы гейтов — без них план не рецензируем до реализации.

## Реализация (финальный дизайн, 2026-09-25) — SHIPPED (ClickHouse `values`, PostgreSQL `jsonb_to_record(set)`)

Все блокеры дизайн-ревью закрыты **без** третьего builder'а: механизм — расширение существующего
`[SqlTableFunction]`/`TRow`-пути.

- **Blocker [DRY]/[OCP] 🔴 — третий механизм декларации схемы.** Не заводится. Добавлен enum
  `TableFunctionSchema { None, LeadingArgument, AliasColumnList }` и свойство
  `SqlTableFunctionAttribute.ResultSchema`; `TableFunctionExpression` несёт `ResultType` (generic-аргумент
  `IQueryable<T>` placeholder-метода) и `ResultSchema`. Список колонок рендерится из `[Column]`-метаданных
  `TRow` (`DataContextCache.Metadata`) и `ISqlDialect.MakeResultColumnType` (CLR→SQL). `TableAlias`/
  `TableColumn` не расширялись, поэтому не запечатывались.
- **Blocker [DIP] 🔴 — контракт ридера/`TResult`.** Носитель схемы — тот же `[Column]`-DTO `TRow`, что и у
  любой TVF: строки материализуются существующим путём `From<T>()` → `EntityMetadata` → `SelectList` →
  `RowMapperFactory.GetOrBuild<TResult>` (нового ридера нет). Схема влияет только на текст SQL.
- **[PERF] 🟡 — план-кэш.** `FromExpressionPlanEqualityComparer` теперь сравнивает и хеширует
  `TableFunction.ResultType`/`ResultSchema` вместе с `Call`; две разные формы строк не делят план.
- **[TYPE] 🟡 — accessor-поверхность.** `TableAlias`/`TableColumn` не менялись (см. выше).
- **[DRY] 🟡 — указатели файлов.** Рендер — `SqlSourceRenderer.MakeTableFunction`/`RenderResultSchema`;
  объявление — `SqlTableFunctionAttribute.cs` + `DataContextExtensions.FromTableFunction`.
- **ℹ️ формы и гейты.** CH: `values('<структура из TRow>', tuples…)` (`LeadingArgument`, tuples —
  verbatim). PG: `jsonb_to_record(json) AS "t1"(a integer, b text)` (`AliasColumnList`, диалектный
  `MakeTableFunctionAlias`). Гейт `ISqlDialect.SupportsResultSchema(TableFunctionSchema)` (DIM `=> false`,
  `SqlDialectBase virtual false`, override по размещению у ClickHouse (`LeadingArgument`) и PostgreSQL
  (`AliasColumnList`)); прочие комбинации/провайдеры — `NotSupportedException`.

### Матрица провайдеров

| Провайдер | `values(...)` | `jsonb_to_record(set)` | Источник |
|---|---|---|---|
| ClickHouse | **да** (`values`) | — (нет jsonb) | ClickHouse docs, `values` |
| PostgreSQL | — | **да** (`jsonb_to_record`/`jsonb_to_recordset`) | PG docs, record SRF |
| SQL Server / MySQL / MariaDB / SQLite | — | — | нет `values`/record-SRF; гейт + `NotSupportedException` |
| InMemory | — | — | TVF как источник не поддержан |

### Тесты

SQL-gen: `tests/nextorm.clickhouse.tests` (`TableFunction_Values_*`), `tests/nextorm.postgres.tests`
(`TableFunction_JsonbToRecord*`), core (`DynamicResultSchemaTests`). Интеграция:
`ClickHouseIntegrationTests.Values_WithDeclaredSchema_ShouldReturnRows`,
`PostgresSpecificTests.JsonbToRecord(set)_WithDeclaredSchema_ShouldReturn*`.

### Осталось вне области

`format`/`merge`/`input` (CH) и `json_populate_record(set)` (PG — заполняет переданную базовую запись, а не
свободный список колонок). В `docs/advanced/limitations.md`.
