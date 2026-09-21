# TODO: Расширение набора встроенных табличных функций (`CONTAINSTABLE`/`FREETEXTTABLE`, `JSON_TABLE`, `jsonb_to_record`)

> Рабочий план (design RFC). Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.13.

## Пункт и цель

- Проблема: предобъявленный набор TVF узок. Не замаплены `CONTAINSTABLE`/`FREETEXTTABLE` (с
  ranking), MySQL `JSON_TABLE`, PostgreSQL `jsonb_to_record`/`json_populate_record` (динамическая
  схема). EF Core и linq2db дают больше «из коробки».
- Цель: добавить обёртки `[SqlTableFunction]` в `SqlFunctions.*`.
- Критерий приёмки: SQL-gen тест на каждую функцию + гейт; чужие провайдеры — `NotSupportedException`.

## Текущее состояние

- Предобъявлены: `generate_series`, `unnest`, `regexp_matches`, `regexp_split_to_table`,
  `jsonb_array_elements(_text)`, `jsonb_each(_text)`, `jsonb_object_keys`, `jsonb_path_query`,
  `ts_stat` (PG); `string_split`, `openjson` (MSSQL); `numbers*`/`zeros*`/`generateRandom` (CH).
- SQL Server `OPENJSON ... WITH` уже выразим через `SqlTableFunctionAttribute.WithClause`.

## Дизайн (черновик)

- `CONTAINSTABLE(table, column, query [, top_n])` → TVF с колонками `KEY`/`RANK` (связка с
  `todo_fulltext_ranking.md`).
- MySQL `JSON_TABLE(doc, path COLUMNS(...))` — статическая схема колонок.
- PostgreSQL `jsonb_to_record(set)`/`json_populate_record` — динамическая схема, см.
  `todo_dynamic_result_schema.md`.

## Открытые вопросы

1. Разбить ли по отдельным todo (ranking vs JSON record).
2. Как выражать `COLUMNS(...)`/`WITH(...)`-схемы в атрибуте.
3. Приоритет провайдеров.

## Файлы к изменению

- `src/nextorm.core/Query/SqlFunctions.*.cs`, `DataContext/Dialect/*`, провайдерные `*Dialect.cs`.
- Тесты: SQL-gen.
- Доки EN+RU, gap-analysis.
