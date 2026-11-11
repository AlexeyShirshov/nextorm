# TODO: ClickHouse `AggregateFunction(...)` state type (`-State`/`-Merge`, `runningAccumulate`)
> Tracking issue: [#62](https://github.com/AlexeyShirshov/nextorm/issues/62) — открыт,
> milestone не назначен (backlog).

> Рабочий план (design RFC). Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.6.
> **Статус: заблокировано драйвером `ClickHouse.Driver` 1.5.0** (перепроверено 2026-11-11;
> исходная проверка 22.09.2026 — на 1.4.0). Апгрейд 1.4.0 → 1.5.0 поведения не изменил:
> `AggregateFunctionException` и сообщение «Unable to directly query column with type
> AggregateFunction(…) … use <function>Merge()» присутствуют в обеих сборках, release notes
> 1.5.0 поддержки состояний не объявляют.

## Блокер (перепроверено 2026-11-11 на 1.5.0)

- **Драйвер не может читать/писать `AggregateFunction(...)`.** `ClickHouse.Driver`
  1.4.0 и 1.5.0 (`Types/AggregateFunctionType.cs`) бросают `AggregateFunctionException` из
  `FrameworkType`, `Read`, `Write` и `ToString`: «Unable to directly query column with type
  AggregateFunction(&lt;function&gt;). Use &lt;function&gt;Merge() function to query this value».
  Значит, материализация состояния в CLR (`byte[]` или отдельный тип) и передача состояния из CLR
  невозможны на этих версиях — а это и есть цель пункта («чтение/передача»).
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

## Текущее состояние (2026-11-11)

- `uniq`/`uniqExact`/`uniqCombined`/`uniqHLL12` и `count_distinct` есть; `uniqState`/`uniqMerge`
  не выражаются (`src/nextorm.core/Query/SqlFunctions/SqlFunctions.ClickHouse.cs:84-96`).
- Пример `examples/nextorm.examples.clickhouse.analytics`: запрос `Incremental` (состояние
  `uniqState` + `uniqMerge`) **удалён из примера 2026-11-01** вместе с чисткой whole-query
  `FromSql`; его отсутствие записано в `examples/VERIFICATION.md`. Остальные array/tuple-запросы
  примера (`ArrayAnalytics`, `Retention`) выражаются через `array_map`/`array_filter`/`group_array`.

## Дизайн (черновик)

- CLR-представление состояния: `byte[]` (opaque) либо отдельный `AggregateFunctionState`.
- Capability-объект (по образцу Фаз 1–3) с рендером `uniqState`/`uniqMerge`/`runningAccumulate`.
- Ридер: материализация `AggregateFunction(...)` как `byte[]`.

## Открытые вопросы

1. Моделировать как `byte[]` или как именованный тип?
2. Нужна ли типизация состояния в `[Column]`/проекции?
3. `-State` как проекция или только промежуточная CTE/таблица.

## Файлы к изменению

- `src/nextorm.clickhouse/*`,
  `src/nextorm.core/DataContext/Mapping/RowMapperFactory.cs`,
  `src/nextorm.core/Query/SqlFunctions/SqlFunctions.ClickHouse.cs`.
- Тесты: `tests/nextorm.clickhouse.tests`, `tests/nextorm.integration.tests`.
- Доки EN+RU, gap-analysis.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **план не готов к дизайн-ревью — 1 блокер-полнота**; статус «заблокировано драйвером» подтверждён.

- **[TYPE]/[KISS] 🔴** Публичный API не определён (`:45-49` — 3 bullet-черновика, ни одной сигнатуры): нет имён/сигнатур (`uniq_state`, `uniq_merge`, …) и поверхности; нет имени/формы capability-объекта + свойства `ISqlDialect`; нет типа материализации `AggregateFunction` и его маппинга в row reader; не решено, как колонка состояния типизируется в `[Column]`/проекции (Q §53-54); нет тест-плана. Fix: довести §«Дизайн» до уровня планов функций (см. [ClickHouse-specific SQL](../../guide/provider-specific-sql/clickhouse.md)).
- **[TYPE] 🟡** `byte[]` vs именованный `AggregateFunctionState` не решён (`:47,53`) — влияет на всю публичную поверхность. Deferred (driver-blocked); при именованном типе он должен быть `sealed`/`readonly`.
- **[LSP]/[TYPE] ℹ️** `SimpleAggregateFunction` частично работает, но `sumMerge(...)` по нему нет (`:20-22`). Fix: заложить явный guard/`Supports` до реализации.
- **[OCP] ℹ️** `runningAccumulate` устарел (CH 25.8, `:18-19`) — не заводить публичный член, оставить оконную форму (`sum_over`).
- **ℹ️** Блокер был актуален на момент ревью: `Directory.Packages.props:19` — `ClickHouse.Driver 1.4.0`
  (ровно версия из плана). На 2026-11-11 закреплён `1.5.0` (`Directory.Packages.props:9`), поведение
  то же — см. §«Блокер». Разблокировка — только новая версия драйвера.
