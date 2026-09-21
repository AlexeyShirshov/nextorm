# TODO: ClickHouse `AggregateFunction(...)` state type (`-State`/`-Merge`, `runningAccumulate`)

> Рабочий план (design RFC). Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.6.

## Пункт и цель

- Проблема: комбинаторы `-State`/`-Merge` (`uniqState`/`uniqMerge`/`sumState`/`sumMerge`, …) и
  `runningAccumulate` требуют колонки типа `AggregateFunction(<agg>, <types>)`; такого типа в
  метаданных/ридере nextorm нет, выразить их нельзя даже через `[SqlFunction]`-UDF с типизированной
  проекцией.
- Цель: поддержать opaque-состояния (чтение/передача) и нативные `...Merge`/`runningAccumulate`.
- Критерий приёмки: SQL-gen + интеграционный round-trip `uniqState(col)` →
  таблица-состояние → `uniqMerge(state)`; прочие провайдеры — `NotSupportedException`.

## Текущее состояние

- `uniqExact`/`count_distinct` есть; `uniqState`/`uniqMerge` не выражаются.
- Пример `examples/nextorm.examples.clickhouse.analytics` (`Incremental`) обходит это сырым SQL.

## Дизайн (черновик)

- CLR-представление состояния: `byte[]` (opaque) либо отдельный `AggregateFunctionState`.
- Capability-объект (по образцу Фаз 1–3) с рендером `uniqState`/`uniqMerge`/`runningAccumulate`.
- Ридер: материализация `AggregateFunction(...)` как `byte[]`.

## Открытые вопросы

1. Моделировать как `byte[]` или как именованный тип?
2. Нужна ли типизация состояния в `[Column]`/проекции?
3. `-State` как проекция или только промежуточная CTE/таблица.

## Файлы к изменению

- `src/nextorm.clickhouse/*`, `src/nextorm.core/DataContext/{Dialect,RowMapper*}`,
  `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`.
- Тесты: `tests/nextorm.clickhouse.tests`, `tests/nextorm.integration.tests`.
- Доки EN+RU, gap-analysis.
