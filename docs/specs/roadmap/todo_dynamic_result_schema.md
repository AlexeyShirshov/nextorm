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
