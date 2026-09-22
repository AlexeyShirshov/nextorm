# TODO: ClickHouse `AggregateFunction(...)` state type (`-State`/`-Merge`, `runningAccumulate`)

> Рабочий план (design RFC). Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.6.
> **Статус: заблокировано драйвером `ClickHouse.Driver` 1.4.0** (проверено 22.09.2026).

## Блокер (проверено 22.09.2026)

- **Драйвер не может читать/писать `AggregateFunction(...)`.** `ClickHouse.Driver`
  1.4.0 (`Types/AggregateFunctionType.cs`) бросает `AggregateFunctionException` из `FrameworkType`,
  `Read`, `Write` и `ToString`: «Unable to directly query column with type
  AggregateFunction(&lt;function&gt;). Use &lt;function&gt;Merge() function to query this value».
  Значит, материализация состояния в CLR (`byte[]` или отдельный тип) и передача состояния из CLR
  невозможны на этой версии драйвера — а это и есть цель пункта («чтение/передача»).
- **`uniqMerge(uniqState(x))` невыразим в одном запросе** — ClickHouse отклоняет вложенный агрегат
  (`ILLEGAL_AGGREGATION`). Состояние обязано пройти через серверный подзапрос/таблицу-состояние, т.е.
  пересечь CLR-границу, что упирается в первый блокер.
- **`runningAccumulate` объявлен устаревшим в ClickHouse 25.8** (`DEPRECATED_FUNCTION`; без
  `allow_deprecated_error_prone_window_functions=1` — ошибка). ClickHouse рекомендует оконные функции.
- `SimpleAggregateFunction(...)` драйвер читает/пишет как underlying-тип (`SimpleAggregateFunctionType`
  делегирует `UnderlyingType`), но `sumMerge(...)` по нему не работает
  (`It must be AggregateFunction(...)`), т.е. для `-Merge` он не подходит.

Вывод зафиксирован в `docs/advanced/limitations.md` (+RU). Разблокировка — новая версия драйвера,
умеющая читать `AggregateFunction` (тогда вернуться к дизайну ниже).

## Пункт и цель (исходно)

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
