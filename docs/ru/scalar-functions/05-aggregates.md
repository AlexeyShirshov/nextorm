# Агрегаты строк и массивов

`SqlFunctions.Sql.string_agg` доступен в PostgreSQL, SQL Server 2017+, ClickHouse, MySQL/MariaDB и SQLite
([`SupportsStringAgg`](xref:NextORM.Core.ISqlDialect.SupportsStringAgg), по умолчанию берёт значение зонтичного
[`SupportsStringArrayAggregates`](xref:NextORM.Core.ISqlDialect.SupportsStringArrayAggregates)); в ClickHouse он рендерится как
`arrayStringConcat(groupArray(x), delimiter)`, в MySQL/MariaDB — как
`group_concat(x separator delimiter)`, в SQLite — как `group_concat(x, delimiter)`.
`SqlFunctions.Postgres.array_agg` ([`SupportsArrayAgg`](xref:NextORM.Core.ISqlDialect.SupportsArrayAgg)) требует типа-массива, поэтому доступен только в
PostgreSQL. Результат `array_agg` — колонка-массив:

```csharp
var names = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Sql.string_agg(e.String, ","))
    .First();
```

```sql
-- PostgreSQL / SQL Server
select string_agg(somestring, ',')
 from complex_entity
-- ClickHouse
select arrayStringConcat(groupArray(somestring), ',')
 from complex_entity
-- MySQL/MariaDB
select group_concat(somestring separator ',')
 from complex_entity
-- SQLite
select group_concat(somestring, ',')
 from complex_entity
```

| C# | SQL | Провайдеры |
|---|---|---|
| `SqlFunctions.Sql.string_agg(x, delimiter)` | `string_agg(x, delimiter)` / `arrayStringConcat(groupArray(x), delimiter)` / `group_concat(x separator delimiter)` / `group_concat(x, delimiter)` | PostgreSQL, SQL Server, ClickHouse, MySQL/MariaDB, SQLite |
| `SqlFunctions.Postgres.array_agg(x)` | `array_agg(x)` | PostgreSQL |

## Фильтр агрегатов (FILTER)

`count`/`count_big`/`min`/`max`/`avg`/`sum` и строковые/массивные агрегаты принимают дополнительный
аргумент `Expression<Func<bool>>`, который фильтрует строки, видимые агрегату. Предикат фильтра —
обычный предикат запроса и может ссылаться на колонки и параметры. Способ записи выбирает
[`AggregateFilterStyle`](xref:NextORM.Core.ISqlDialect.AggregateFilterStyle): PostgreSQL и SQLite
генерируют ANSI-предложение `filter (where ...)`, ClickHouse — комбинатор `-If` (`countIf`/`sumIf`/...),
а MySQL/MariaDB и SQL Server отклоняют вызов. В PostgreSQL ANSI-предложение требует версии сервера 9.4
или новее; диалект, настроенный на более старую `Version`, отклоняет вызов через
`NotSupportedException`, а незаданная версия предполагает 9.4+ (см.
[провайдер PostgreSQL](../connecting-providers/postgres.md#версия-сервера)):

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new
    {
        e.Int,
        Big = SqlFunctions.Sql.count(() => e.Id > 10L),
        Total = SqlFunctions.Sql.sum(e.Id, () => e.Boolean == true)
    })
    .ToList();
```

```sql
select nullableint, count(*) filter (where (id > 10)) as "Big", sum(id) filter (where (b = true)) as "Total"
 from complex_entity group by nullableint
```

В ClickHouse тот же запрос генерирует `toInt32(countIf((id > 10)))` и `sumIf(id, (b = true))`.

## Медиана и квантили

Четыре имени непереносимы и намеренно не взаимозаменяемы: `percentile_cont` интерполирует,
`percentile_disc` возвращает входное значение, `median` — фиксированный случай `0.5`, а общий член
`percentile` отклоняется везде вместо того, чтобы стать псевдонимом непрерывной формы.

| C# | SQL | Провайдеры |
|---|---|---|
| `SqlFunctions.Postgres.percentile_cont(fraction, () => x)` | `percentile_cont(f) within group (order by x)` (`double precision`) | PostgreSQL: упорядоченный агрегат, одна строка на группу; только сортировка с плавающей точкой |
| `SqlFunctions.Postgres.percentile_disc(fraction, () => x)` | `percentile_disc(f) within group (order by x)` (сохраняет тип выражения сортировки) | PostgreSQL: упорядоченный агрегат |
| `SqlFunctions.Postgres.median(() => x)` | `percentile_cont(0.5) within group (order by x)` | PostgreSQL: агрегат, одна строка на группу |
| `SqlFunctions.Sql.percentile_cont(fraction, x).Over(...)`, `percentile_disc(fraction, x).Over(...)` | `percentile_cont(f) within group (order by x) over (...)` | SQL Server (`float(53)`), MariaDB (`double(18,10)`): аналитическая форма, одна строка на входную строку, окно только с `partition by` |
| `SqlFunctions.SqlServer.median(x).Over(...)`, `SqlFunctions.MySql.median(x).Over(...)` | `percentile_cont(0.5) within group (order by x) over (...)` | SQL Server, MariaDB: аналитическая форма, одна строка на входную строку, окно только с `partition by` |
| `SqlFunctions.ClickHouse.median(x)` | `toFloat64(median(x))` | ClickHouse: агрегат семейства `quantile` (приблизительный) |
| общий `percentile(fraction, value)` | `NotSupportedException` | все провайдеры |

Дробь — это `double` в `[0, 1]`, никогда не проценты 0–100; она должна быть конечной и константной
(литерал или захваченная переменная), а зависящая от строки дробь отклоняется при генерации SQL. SQLite
отклоняет `median` и любые квантили (в поставляемой сборке нет агрегата `median` и указан
`SQLITE_ENABLE_PERCENTILE=0`, а nextorm не эмулирует их), MySQL отклоняет все четыре (нет ни `MEDIAN(…)`,
ни `PERCENTILE_*`), а ClickHouse отклоняет имена квантилей: его `median` — нативный агрегат семейства
`quantile`, и для переносимых написаний не создаётся псевдонима `quantile*`. Аналитические формы
обязательно завершаются `Over(...)`, а оконный `ORDER BY` или рамка отклоняются.

## Функции, возвращающие наборы (PostgreSQL)

`SqlFunctions.Postgres.generate_series` и `SqlFunctions.Postgres.unnest` — это предобъявленные источники
[`[SqlTableFunction]`](../guide/13-table-valued-functions.md), поэтому отдельная обёртка не нужна:

```csharp
var numbers = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.generate_series(1L, 3L))
    .Select(r => r.Value)
    .ToList();

var elements = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

`generate_series` проецируется на [`Value`](xref:NextORM.Core.SqlFunctions.IGenerateSeriesRow.Value), а `unnest` — на
[`Value`](xref:NextORM.Core.SqlFunctions.IUnnestRow`1.Value); обе соответствуют единственной колонке, которую возвращает функция.

## Каталог агрегатов ClickHouse (#286, T286b)

Остаточный каталог агрегатов ClickHouse типизирован на `ClickHouseFunctions` и диспетчеризуется
`AdvancedAggregateTranslator` через возможность `IClickHouseAggregateRenderer`, существующую только для
ClickHouse (у остальных провайдеров она `null`, поэтому вызов падает до SQL). Параметрические агрегаты
используют грамматику вендора с двумя скобками — параметры accuracy/level/limit идут ведущими
CLR-аргументами, а аргументы-значения образуют вторую пару, — и результат нормализуется к
объявленной CLR-форме (`toInt64`, `toInt32`, `toFloat64` или составное приведение для `Array`/`Tuple`).

| Группа | Члены |
|---|---|
| Статистика | `entropy`, `simple_linear_regression`, `delta_sum`, `delta_sum_timestamp`, `gini`, `skew_pop`, `skew_samp`, `kurt_pop`, `kurt_samp`, `rank_corr`, `contingency`, `cramers_v`, `cramers_v_bias_corrected`, `theils_u`, `welch_t_test`, `mann_whitney_u_test`, `kolmogorov_smirnov_test`, `mean_z_test`, `analysis_of_variance`, `student_t_test`, `student_t_test_one_sample` |
| Квантили | `quantile_tdigest`, `quantile_gk`, `quantile_dd`, `quantile_bfloat16`, `quantile_prometheus_histogram`, `quantiles`, `quantiles_exact`, `quantiles_exact_low`, `quantiles_exact_high`, `quantiles_exact_weighted`, `quantiles_timing`, `quantiles_tdigest`, `quantiles_bfloat16`, `quantiles_dd`, `quantiles_gk`, `quantiles_prometheus_histogram`, `uniq_up_to` |
| Top-K и карты | `approx_top_k`, `approx_top_sum`, `any_heavy`, `max_map`, `min_map` |
| Семейство group-array | `group_array`, `group_array_moving_avg`, `group_array_moving_sum`, `group_array_sample`, `group_array_sorted`, `group_array_last`, `group_array_insert_at`, `group_array_intersect`, `group_concat` |
| Счётчики и суммы | `sum_count`, `sum_kahan`, `histogram`, `sparkbar`, `group_format`, `max_intersections` (+`max_intersections_position`) |
| Выбор строки | `first_value`, `last_value` (агрегатные формы; оконные формы остаются на `CommonFunctions`) |
| Последовательности | `sequence_count`, `sequence_match_events`, `sequence_next_node` |
| Time series | `time_series_group_sum`, `time_series_group_rate_sum`, `time_series_instant_rate_to_grid`, `time_series_rate_to_grid`, `time_series_resample_to_grid_with_staleness`; оконные `exponential_time_decayed_sum`/`_max`/`_count`/`_avg` (дополните `Over(...)`) |

Версионные факты закреплены, а не скрыты: `gini`, `student_t_test_one_sample`, `group_format` и
`quantile_prometheus_histogram` появились после 25.8, а `time_series_group_sum`/
`time_series_group_rate_sum` были удалены, поэтому их плечи рендерят имя вендора, а живой тест
фиксирует `UNKNOWN_FUNCTION`. `sequence_next_node` требует
`allow_experimental_funnel_functions`, а сетки time-series — `allow_experimental_time_series_aggregate_functions`.
Три члена остаются отклонёнными (`stochastic_linear_regression`, `stochastic_logistic_regression`,
`exponential_moving_average`: непрозрачные состояния `AggregateFunction`) — см.
[ClickHouse-specific SQL › Пока не поддерживается](../guide/provider-specific-sql/clickhouse.md#пока-не-поддерживается).
