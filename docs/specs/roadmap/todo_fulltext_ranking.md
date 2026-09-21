# TODO: Full-text ranking/score (`ts_rank`, `CONTAINSTABLE`)

> Рабочий план (design RFC). Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.12.

## Пункт и цель

- Проблема: `SqlFunctions.Sql.contains`/`freetext` рендерят только булев предикат; проекции
  релевантности нет (PostgreSQL `ts_rank`/`ts_rank_cd`, SQL Server `CONTAINSTABLE`+`RANK`,
  MySQL `MATCH ... AGAINST` уже даёт предикат).
- Цель: surface для score/ranking.
- Критерий приёмки: SQL-gen тесты на `ts_rank`/`CONTAINSTABLE`; провайдеры без поддержки —
  `NotSupportedException`.

## Текущее состояние

- `contains`/`freetext` — булевы, гейт `SupportsFullText`.
- `CONTAINSTABLE` — это TVF (см. `todo_builtin_tvf_expansion.md`), score приходит колонкой `RANK`.

## Дизайн (черновик)

- PostgreSQL: `CommonFunctions.ts_rank(...)`/`ts_rank_cd`.
- SQL Server: `CONTAINSTABLE` как TVF + проекция `RANK` (зависит от TVF-expansion).
- MySQL/MariaDB: `MATCH(...) AGAINST(...)` уже есть; отдельного score SQL не даёт.

## Открытые вопросы

1. Портативная сигнатура score или провайдерные `SqlFunctions.Postgres/SqlServer`.
2. Зависимость от `todo_builtin_tvf_expansion.md` для SQL Server.

## Файлы к изменению

- `src/nextorm.core/Builders/BuiltinFunctionTranslator.cs`, `Query/SqlFunctions.*.cs`,
  `Dialect/ISqlDialect.cs`/`SqlDialectBase.cs`, провайдерные `*Dialect.cs`.
- Тесты: SQL-gen.
- Доки EN+RU, gap-analysis.
