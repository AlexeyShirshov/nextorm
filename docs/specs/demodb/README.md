# Демонстрационные БД: примеры nextorm и сверка результатов

Набор реалистичных аналитических запросов к трём «модельным» базам лежит в соседних папках:

| Папка | СУБД | Модельная БД | Запросов |
|---|---|---|---|
| [`docs/specs/postgres-demodb/`](../postgres-demodb) | PostgreSQL | демо-база «Авиаперевозки» (схема `bookings`, он же *aviasales*), версия 2025-09-01 | 5 |
| [`docs/specs/mssql-demodb/`](../mssql-demodb) | SQL Server | AdventureWorks 2022 (`Sales`, `Production`, `Person`, `Purchasing`) | 5 |
| [`docs/specs/clickhouse-demodb/`](../clickhouse-demodb) | ClickHouse | `datasets.hits_v1` (аналитика визитов) | 5 |

Цель этого раздела:

1. показать, как каждый SQL-запрос выражается на nextorm (LINQ-модель, без «магических» сущностей);
2. зафиксировать, что выражается штатным API, что — только с эквивалентной заменой, а что пока
   требует сырого SQL;
3. описать проверяемый план сверки: *прямой SQL* против *запроса nextorm* на одних и тех же данных,
   с критериями совпадения результатов.

Runnable-версии примеров (консольные приложения с Testcontainers-подготовкой БД) лежат в
[`examples/`](../../../examples/README.md): `nextorm.examples.postgres.aviasales`,
`nextorm.examples.mssql.adventureworks`, `nextorm.examples.clickhouse.analytics`.

## Состав раздела

| Документ | Содержание |
|---|---|
| [postgres-aviasales.md](postgres-aviasales.md) | Сущности + 5 примеров nextorm по демо-БД PostgreSQL |
| [mssql-adventureworks.md](mssql-adventureworks.md) | Сущности + 5 примеров nextorm по AdventureWorks |
| [clickhouse-analytics.md](clickhouse-analytics.md) | Сущности + 5 примеров nextorm по `hits_v1` |
| [verification-plan.md](verification-plan.md) | План сверки «direct SQL ↔ nextorm» на Testcontainers |

## Сводная матрица покрытия

Обозначения: **OK** — выражается штатным LINQ-API; **≡** — выражается с эквивалентной заменой
(тот же результат, другой SQL); **raw** — штатного API нет, на время сверки используется
`WithSql(...)` с типизированной проекцией.

### PostgreSQL / aviasales

| Запрос | Покрытие | Замена / причина |
|---|---|---|
| `aircraft_delay_chains.sql` | **≡** | `EXTRACT(EPOCH …)/60` → `date_diff('milliseconds', …)/60000.0`; `model ->> 'ru'` → `json_get_text`; `LAG` → `lag(...).Over(...)` |
| `business_occupancy_matrix.sql` | **≡** | `EXTRACT(ISODOW …)` → `date_diff('day', date_trunc('week', …), …) + 1`; `AVG(CASE WHEN …)` → filtered `avg(x, () => …)`; `NULLIF` → `nullif` |
| `passenger_noshow_analysis.sql` | **≡** | `SUM(CASE WHEN bp.seat_no IS NULL …)` → `sum(x == null ? 1 : 0)`; фильтр по агрегату → `Having`/производная таблица |
| `rolling_revenue_metrics.sql` | **OK** | `date_trunc`, оконные фреймы, `Math.Round` |
| `route_network_abc_xyz.sql` | **≡** | `STDDEV` → `stdev`; оконные суммы и `CASE`; JSON-конкатенация `city ->> 'ru'` |

### SQL Server / AdventureWorks

| Запрос | Покрытие | Замена / причина |
|---|---|---|
| `mssql_vip_churn.sql` | **≡** | `MAX(OrderDate) OVER()` → `max_over(...).Over()`; `LAG` → `lag`; `DATEDIFF(day, …)` → `date_diff("day", …)`; `FORMAT(…, 'yyyy-MM-dd')` → проекция `DateTime` или UDF `[SqlFunction("format")]` |
| `mssql_rolling_kpi.sql` | **≡** | `DATEADD(month, DATEDIFF(month,0,…),0)` → `date_trunc("month", …)` (SQL Server 2022 `datetrunc`); `FORMAT` — как выше; фреймы окон |
| `mssql_supply_chain.sql` | **OK** | `date_diff`, JOIN |
| `mssql_product_abc_xyz.sql` | **≡** | `DATEADD(quarter, …)` → `date_trunc("quarter", …)`; `STDEV` → `stdev`; `AVG(CAST(qty AS FLOAT))` → `avg((double)qty)`; оконные доли |
| `mssql_quarterly_pivot.sql` | **≡** | нативный `PIVOT` не поддержан ([todo_mssql §Отложено](../roadmap/todo_mssql.md)); эквивалент — условный `SUM(CASE WHEN quarter = n THEN margin END)` |

### ClickHouse / hits_v1

| Запрос | Покрытие | Замена / причина |
|---|---|---|
| `clickhouse_array_analytics.sql` | **raw** | higher-order `arrayMap`/`arrayFilter` + группировка по массиву не поддержаны ([todo_clickhouse §Уровень 3](../roadmap/todo_clickhouse.md)) |
| `clickhouse_funnel.sql` | **raw** | `windowFunnel` — «продвинутый» агрегат, не реализован |
| `clickhouse_incremental.sql` | **raw** | комбинатор `-Merge` (`uniqMerge`) по `AggregatingMergeTree` не поддержан |
| `clickhouse_retention.sql` | **raw** | `groupArray((…))` возвращает массив/кортеж; row reader для массивов ещё нет |
| `clickhouse_sessions.sql` | **≡** | `lagInFrame` → `lag`; `runningAccumulate(if(…))` → кумулятивный `sum_over(case when …)`; `quantile(0.99)(…)` есть |

> Для **raw**-позиций пример показывает и типизированную проекцию результата, и вызов
> `WithSql`/`PrepareFromSql` — сам текст запроса остаётся из `*-demodb`. Так сверка «direct SQL ↔
> nextorm» возможна уже сейчас, а разрыв в API фиксируется как задача роадмапа.

## Связанные документы

* [`docs/advanced/limitations.md`](../../advanced/limitations.md) — что вне области по дизайну.
* [`docs/specs/roadmap/sql-capabilities-gap-analysis.md`](../roadmap/sql-capabilities-gap-analysis.md) — сводный анализ пробелов.
* [`docs/specs/roadmap/todo_postgres.md`](../roadmap/todo_postgres.md), [`todo_mssql.md`](../roadmap/todo_mssql.md), [`todo_clickhouse.md`](../roadmap/todo_clickhouse.md).
* [`docs/guide/10-window-functions.md`](../../guide/10-window-functions.md) — оконные функции.
* [`docs/guide/14-raw-sql.md`](../../guide/14-raw-sql.md) — `WithSql`/`PrepareFromSql`.

## Статус

Черновик (WIP). Примеры написаны по текущему API `1.0.3-alpha`; при изменении публичной
поверхности (переименовании `SqlFunctions`/`EntityBuilder`) документы обновляются в том же коммите
(AGENTS.md: «Renaming a public type or method requires updating both `docs/**` and `docs/ru/**`»).
