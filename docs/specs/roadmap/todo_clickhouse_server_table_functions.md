# TODO: ClickHouse серверные/кластерные табличные функции (`url`/`s3`/`remote`/…)

> Рабочий план (design RFC). Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.11.

## Пункт и цель

- Проблема: серверные TVF ClickHouse не задекларированы: `url`, `s3`, `remote`, `remoteSecure`,
  `file`, `format`, `merge`, `input`, `cluster`, `clusterAllReplicas` (конфигурация сервера/кластера).
- Цель: предобъявленные `SqlFunctions.ClickHouse.*`-обёртки с гейтом провайдера.
- Критерий приёмки: SQL-gen тесты на каждую функцию; чужие провайдеры — `NotSupportedException`.

## Текущее состояние

- Предобъявлены только `numbers`/`numbers_mt`/`zeros`/`zeros_mt`/`generateRandom`.
- `format`/`input` — табличные функции, принимающие структуру (взаимодействует с
  `todo_dynamic_result_schema.md`).

## Дизайн (черновик)

- `[SqlTableFunction]`-обёртки в `SqlFunctions.ClickHouse` + `SupportsTableFunction`-гейт.
- Секреты (URL/ключи S3) — обычные строковые аргументы; аутентификация — ответственность сервера.

## Открытые вопросы

1. Ограничиться списком из gap-analysis или добавить `s3Cluster`/`remote`-варианты.
2. Типизация `format`/`input` (структура) — см. dynamic-schema.
3. Рекомендации по безопасности (URL/S3 в логах/планах).

## Файлы к изменению

- `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`, `src/nextorm.clickhouse/*Dialect.cs`.
- Тесты: `tests/nextorm.clickhouse.tests`.
- Доки EN+RU, gap-analysis.
