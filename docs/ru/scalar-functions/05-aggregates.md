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

`SqlFunctions.Postgres.generate_series` и `SqlFunctions.Postgres.unnest` — это предобъявленные
runtime-фабрики [`TvfEntityBuilder<T>`](../guide/13-table-valued-functions.md), поэтому собственная фабрика не нужна:

```csharp
var numbers = dataContext
    .From(() => SqlFunctions.Postgres.generate_series(1L, 3L))
    .Select(r => r.Value)
    .ToList();

var elements = dataContext
    .From(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

`generate_series` проецируется на [`Value`](xref:NextORM.Core.SqlFunctions.IGenerateSeriesRow.Value), а `unnest` — на
[`Value`](xref:NextORM.Core.SqlFunctions.IUnnestRow`1.Value); обе соответствуют единственной колонке, которую возвращает функция.

## Дополнения агрегатов PostgreSQL (#281)

Члены ниже дополняют агрегатную поверхность PostgreSQL. Это обычные агрегаты `pg_proc` — или, для
агрегатов SQL/JSON и `grouping`, конструкции уровня парсера без строки в `pg_proc` — с проверкой по
каждому имени; любой другой провайдер, включая in-memory, при трансляции выбрасывает
`NotSupportedException`. `json_agg`, `jsonb_agg`, `json_object_agg` и `jsonb_object_agg` по-прежнему
документированы вместе с JSON-функциями в разделе [JSON и JSONB](07-json-and-xml.md).

### `any_value`

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.any_value(x)` | `any_value(x)` |

`any_value` возвращает произвольное значение группы (`anyelement → anyelement`, PostgreSQL 16+), причём
и аргумент, и результат сохраняют собственный тип аргумента (`integer` остаётся `integer`). Это агрегат
`pg_proc` PostgreSQL, а не переносимый `Sql.any_agg`. Поскольку результат полиморфен, nextorm требует
одноколоночную CLR-форму — примитив, строку, дату/время, decimal, `Guid` или массив байтов — и
отклоняет любой другой `T` до генерации SQL; при необходимости проецируйте значение с явным
преобразованием. Группа, все значения которой NULL, даёт NULL, а пустой вход по-прежнему даёт одну
строку с NULL.

### Регрессионные суммы (`regr_sxx`, `regr_sxy`, `regr_syy`)

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.regr_sxx(y, x)` | `regr_sxx(y, x)` |
| `SqlFunctions.Postgres.regr_sxy(y, x)` | `regr_sxy(y, x)` |
| `SqlFunctions.Postgres.regr_syy(y, x)` | `regr_syy(y, x)` |

Оба операнда — обобщённые (`TY?`/`TX?`); перегрузки агрегата в PostgreSQL принимают `double precision`,
и результат — `double precision` (`double?`). `regr_sxx` равен
`sum(X^2) - sum(X)^2/N`, `regr_syy` — то же по `Y`, а `regr_sxy` — `sum(X*Y) - sum(X)*sum(Y)/N`. Пара
с NULL-операндом пропускается; группа с единственной не-NULL парой даёт `0` (измерено), а группа из
одних NULL или пустой вход дают NULL — пустой вход по-прежнему даёт одну строку.

### `xmlagg`

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.xmlagg(x)` | `xmlagg(cast(x as xml))` |

В PostgreSQL есть единственный агрегат `xmlagg(xml) → xml`: `xmlagg(text)` и `xmlagg(integer)` дают
`42883` "function … does not exist" (измерено), поэтому операнд приводится к `xml` и обязан содержать
XML-разметку, которую принимает тип `xml`. Результат — конкатенация документов группы без разделителя,
и Npgsql материализует результат `xml` как `string` (измерено), что и объявляет член. NULL-операнды
пропускаются, группа, все операнды которой NULL, даёт NULL, а пустой вход по-прежнему даёт одну строку
с NULL.

### Строгие и уникальные JSON-агрегаты

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_agg_strict(x)` | `json_agg_strict(x)` |
| `SqlFunctions.Postgres.jsonb_agg_strict(x)` | `jsonb_agg_strict(x)` |
| `SqlFunctions.Postgres.json_object_agg_unique(k, v)` | `json_object_agg_unique(k, v)` |
| `SqlFunctions.Postgres.json_object_agg_unique_strict(k, v)` | `json_object_agg_unique_strict(k, v)` |

Агрегаты PostgreSQL 16. Две массивные формы игнорируют NULL-значения: группа, все значения которой
NULL, даёт `[]` (а не массив null, измерено), пустой вход по-прежнему даёт одну строку с NULL, а
результат `json`/`jsonb` читается как строка. `json_object_agg_unique` сохраняет NULL-значение как JSON
`null`, а `json_object_agg_unique_strict` отбрасывает пару с NULL-значением; оба поднимают `22030` при
дубликате ключа и `22004` при NULL-ключе (измерено на 17.11), а пустой вход даёт одну строку с NULL.

### Агрегаты SQL/JSON (`json_arrayagg`, `json_objectagg`)

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_arrayagg(x)` | `json_arrayagg(x)` |
| `SqlFunctions.Postgres.jsonb_arrayagg(x)` | `json_arrayagg(x returning jsonb)` |
| `SqlFunctions.Postgres.json_objectagg(k, v)` | `json_objectagg(k value v)` |
| `SqlFunctions.Postgres.json_objectagg_unique(k, v)` | `json_objectagg(k value v with unique keys)` |
| `SqlFunctions.Postgres.jsonb_objectagg(k, v)` | `json_objectagg(k value v returning jsonb)` |

Конструкции уровня парсера PostgreSQL 17 без строки в `pg_proc`. `json_arrayagg` по умолчанию работает
как `ABSENT ON NULL`: NULL-значения отбрасываются, группа из одних NULL даёт `[]`, а пустой вход даёт
NULL. `json_objectagg` принимает ровно одну пару на вызов (`key value value`; форма из двух пар через
запятую — это `42601`) и по умолчанию работает как `NULL ON NULL`, сохраняя дубликаты ключей, а
`with unique keys` поднимает `22030`; NULL-ключ поднимает `22004`, а пустой вход даёт NULL. Ключ не
приводится — сервер сам приводит его к тексту (измерено на целочисленном ключе). `jsonb_arrayagg` и
`jsonb_objectagg` — имена членов для пунктов `RETURNING jsonb`, поскольку функций с такими именами в
PostgreSQL нет. У построителя нет места для внутреннего списка аргументов агрегата, поэтому `ORDER BY …`,
`ABSENT|NULL ON NULL` и `DISTINCT` не экспонируются; написание `DISTINCT` на 17.11 — синтаксическая
ошибка (`42601`, измерено). Любой результат — это `json`/`jsonb`, читаемый как строка.

### `grouping`

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.grouping(column)` | `grouping(column)` |

Один бит идентичности супер-агрегата текущей строки: `0`, когда строка группирует колонку, и `1`, когда
строка — супер-агрегат, который её агрегировал. Аргумент обязан быть выражением собственного списка
группировки запроса — иначе `42803` — и функция полезна только внутри группировки
`GROUP BY ROLLUP`/`CUBE`/`GROUPING SETS`; в обычном `GROUP BY` она допустима и всегда равна `0`.
`GroupByRollup(...)` в nextorm порождает такую группировку (как и `GroupByCube`/`GroupByGroupingSets`),
поэтому `grouping` можно проецировать рядом с агрегатными колонками. Вызов без аргументов `grouping()` —
синтаксическая ошибка (`42601`), а результат — `integer` (`int?`). Измерено на 17.11. Для каждой строки
возвращается `0` или `1`; случая NULL или пустого входа у неё нет.

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
