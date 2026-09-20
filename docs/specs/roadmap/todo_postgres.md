# TODO: недостающие функции PostgreSQL

> Незаблокированный остаток Phase 2 сведён в `todo_phase2.md`.

Черновик списка популярных функций/конструкций PostgreSQL, которых пока нет в CommonFunctions.
Скалярные функции обычно добавляются через `[SqlFunction]` или новый метод `SqlFunctions.Sql` + транслятор;
агрегаты, `FILTER`/`WITHIN GROUP` и табличные функции требуют поддержки в `SqlBuilder`/`CommonFunctions`.

## Уже реализовано (для контекста)

- Агрегаты: `count`, `count_big`, `count_distinct`, `min`, `max`, `avg`, `sum`, `stdev`, `stdevp`,
  `var`, `varp` (+ `distinct`).
- Строки: `upper`, `lower`, `trim`/`ltrim`/`rtrim`, `substring`, `replace`, `like`, `contains`,
  `startswith`, `endswith`, `length`, `string.IsNullOrEmpty`, конкатенация.
- Математика: `abs`, `ceiling`, `floor`, `round`, `sqrt`, `pow`, `exp`, `log` (1 арг.), `sin`, `cos`,
  `tan`, `sign`, `trunc`.
- Даты: `now`/`utcnow`, `year`/`month`/`day`/`hour`/`minute`/`second`.
- Окна: `row_number`, `rank`, `dense_rank`, `ntile`, `lag`, `lead`, `first_value`, `last_value`,
  `sum_over`, `avg_over`, `min_over`, `max_over`, `count_over`.
- Массивы: `cardinality`, `array_length`, `array_ndims`, `array_lower`, `array_upper`,
  `array_position`, `array_contains` (`@>`), `array_overlaps` (`&&`), `array_to_string`,
  `any`/`all` над массивом.
- JSON/JSONB: `json_agg`, `jsonb_agg`, `json_object_agg`, `jsonb_object_agg`, `json_build_object`,
  `jsonb_build_object`, `json_build_array`, `jsonb_build_array`, `to_json`, `to_jsonb`, `json_cast`,
  `json_get`/`json_get_text` (`->`/`->>`), `json_get_path`/`json_get_path_text` (`#>`/`#>>`),
  `json_contains` (`@>`), `json_exists` (`?`), `json_exists_any` (`?|`), `json_exists_all` (`?&`),
  `json_array_length`/`jsonb_array_length`, `json_typeof`/`jsonb_typeof`.
- Прочее: `coalesce` через `??`, `case`/`switch`, `union`/`intersect`/`except`, CTE (в т.ч.
  рекурсивные), `distinct`, соединения, `apply`/`lateral` (частично).

## Агрегаты и GROUP BY

- [x] `string_agg`
- [x] `array_agg`
- [x] `bool_and` / `bool_or` / `every`, `bit_and` / `bit_or` / `bit_xor` (флаги `SupportsBooleanAggregates` / `SupportsBitAggregates`)
- [x] Упорядоченные агрегаты (`WITHIN GROUP (ORDER BY ...)`): `percentile_cont`, `percentile_disc`, `mode` (`SupportsOrderedAggregates`). Кросс-провайдерный `percentile_cont`/`percentile_disc` для SQL Server/MariaDB (оконная форма) — см. `todo_mssql.md` → «Осталось» и `docs/providers/clickhouse.md`.
- [x] Статистика: `corr`, `covar_pop`/`covar_samp`, `regr_*` (`SupportsStatisticalAggregates`)
- [x] `FILTER (WHERE ...)`; `ORDER BY` внутри агрегата пока нет

## Даты и время

- [x] `date_trunc` (одна из самых востребованных)
- [x] `age`, `date_bin`, `make_date`, `make_interval`, `justify_days`/`justify_hours`
- [x] `to_char`, `to_date`, `to_number`, `to_timestamp`
- [x] Интервальная арифметика: `SqlFunctions.Sql.date_add`/`end_of_month` и `DateTime.Add*` (интервальная арифметика `+ interval` в PostgreSQL, `dateadd` в SQL Server, `addXxx` в ClickHouse); `AT TIME ZONE` / `timezone()` — функция `SqlFunctions.Sql.timezone`
- [x] Части даты через свойства `DateTime`: `Year`, `Month`, `Day`, `Hour`, `Minute`, `Second`, `DayOfYear` (`doy`) — `MemberTranslator` → `MakeDatePart`
- [ ] **Публичный `EXTRACT`/`date_part`** — `quarter`, `week`, `epoch`, `dow`, `isodow` и `DateTime.DayOfWeek` недоступны: `MakeDatePart` вызывается только для перечисленных выше свойств, отдельного `SqlFunctions.Sql.extract(part, value)` в API нет (в `todo_postgres.md` он ранее упоминался как готовый — это было неверно). В demo-запросе `business_occupancy_matrix.sql` `EXTRACT(ISODOW FROM scheduled_departure)` заменён на `date_diff('day', date_trunc('week', d), d) + 1`, а `EXTRACT(EPOCH FROM a - b)` в `aircraft_delay_chains.sql` — на `date_diff('seconds', a, b)`. Нужны метод `extract`/`date_part` (и/или свойства `DateTime.DayOfWeek`) с ветками диалектов; `MakeDatePart`/`MakeDateDiff` уже есть.
- [x] `current_date`, `current_time`, `localtime`, `localtimestamp`
- [x] `generate_series` (табличная функция, см. ниже)

## Строки и регулярные выражения

- [x] `regexp_replace`, `regexp_split_to_array`, `regexp_like`, `regexp_count`, `regexp_instr`
      (остались возвращающие набор `regexp_matches` и `regexp_split_to_table`)
- [x] `split_part`, `strpos`/`position`, `left`, `right`, `lpad`, `rpad`, `repeat`, `reverse`,
      `initcap`, `translate`, `overlay`
- [x] `concat_ws`, `format`, `startswith`
- [x] `md5` (остались `digest`/`sha256` из pgcrypto)

## Условные / generic

- [x] `greatest`, `least`, `nullif` (популярны и просты)
- [x] `num_nulls`, `num_nonnulls`

## Математика

- [x] `asin`/`acos`/`atan`/`atan2`, `cbrt`, гиперболические (`sinh`/`cosh`/`tanh`, `asinh`/`acosh`/`atanh`)
- [x] `degrees`, `radians`, `pi()`, `random()` (остался `setseed`)
- [x] `log(base, x)` (через `SqlFunctions.Sql.log`; 2-аргументный `Math.Log` по-прежнему намеренно не поддержан), `mod()`, `gcd`/`lcm`,
      `factorial`, `width_bucket`
- [ ] **`round(double precision, int)`** — `Math.Round(x, n)` транслируется в `round(x, n)` для всех
      диалектов (`MathFunctionTranslator`), но PostgreSQL определяет только `round(numeric, int)`;
      над `double precision` сервер бросает `function round(double precision, integer) does not exist`.
      Сейчас обходится приведением на стороне LINQ — `Math.Round((decimal)x, n)`. Нужен диалектный хук
      (PostgreSQL оборачивает первый аргумент в `(...)::numeric`, когда он не `numeric`), иначе
      ошибка всплывает только на исполнении. Уровень: простое (хук `MakeMathFunction`).

## Массивы (дополнить)

- [x] `array_append`, `array_prepend`, `array_cat`, `array_remove`, `array_replace`, `array_fill`
- [x] `array_dims`, `array_positions`, `array_reverse`, `array_sort` (остались `array_shuffle`/`array_sample`)
- [x] `string_to_array`; `array_agg` и `unnest` — готовы
- [x] Операторы `<@`, `||` (конкатенация массивов) — `array_contained_by`, `array_concat`;
      `=`/`<>` работают через обычное сравнение

## JSON/JSONB (дополнить)

- [x] JSONPath: `jsonb_path_query_array`/`jsonb_path_query_first`, `jsonb_path_exists`, `jsonb_path_match`
      (остался возвращающий набор `jsonb_path_query`)
- [x] Мутация/утилиты: `jsonb_set`, `jsonb_insert`, `jsonb_strip_nulls`, `jsonb_pretty`, `jsonb_delete`
- [ ] Разворачивание в строки: `jsonb_array_elements(_text)`, `jsonb_each(_text)`, `jsonb_object_keys`,
      `jsonb_to_record(set)`, `json_populate_record`
- [x] `row_to_json`, `array_to_json`, оператор `||`, оператор `-`

## Оконные функции и фреймы

- [x] `percent_rank`, `cume_dist` (общая оконная поверхность `SqlFunctions.Sql`; SQL-gen тесты во всех
      провайдерах, включая ClickHouse, + `CommonTestSuite.Window.PercentRankCumeDist_ShouldComputeOverOrder`).
- [x] `nth_value(property, n)` — `CommonFunctions.nth_value` + флаг `SupportsNthValue`; поддержан
      PostgreSQL, MySQL/MariaDB, SQLite и ClickHouse; SQL Server бросает `NotSupportedException`
      (в T-SQL `NTH_VALUE` нет). На поддерживающих провайдерах результат frame-зависим — полную рамку
      задаёт вызывающий.
- [ ] Именованные окна (`WINDOW w AS (...)`), режим фрейма `GROUPS`, исключения фрейма (`EXCLUDE`)

## Табличные функции / FROM

- [x] `generate_series`
- [x] `unnest`
- [ ] `jsonb_array_elements`
- [ ] `regexp_split_to_table`
- [ ] `ts_stat`

(есть механизм `[SqlTableFunction]` + `FromTableFunction`, но встроенных нет)

## PostgreSQL-специфичные конструкции

- [x] `SELECT DISTINCT ON (...)`, `LIMIT ... WITH TIES`, `TABLESAMPLE`, `FOR UPDATE`/`FOR SHARE` —
      `EntityBuilder.DistinctOn`/`WithTies`/`TableSample`/`ForUpdate`/`ForShare`; диалектные хуки
      `SupportsDistinctOn`/`MakeDistinctOn`, `SupportsTableSample`/`SupportsTableSampleMethod`/`MakeTableSample`,
      `SupportsWithTies` (+ `Paging.HasWithTies`), `SupportsLocking`/`MakeLock`. `WITH TIES`/`TABLESAMPLE`
      кросс-провайдерны (SQL Server: `TOP(n) WITH TIES`/`FETCH ... WITH TIES`, `TABLESAMPLE (n PERCENT)`;
      только `SYSTEM`); блокировка также у MySQL/MariaDB (`FOR UPDATE`/`LOCK IN SHARE MODE`). Тесты:
      `SqlGenerationTests.DistinctOn_*`/`TableSample_*`/`WithTies_*`/`ForUpdate_*`/`ForShare_*`,
      `PostgresDialectTests.QueryModifierHooks_ShouldUsePostgresForms`; `docs/guide/provider-specific/postgresql.md`.
- [x] Полнотекстовый поиск: `@@`, `to_tsvector`, `to_tsquery`, `ts_rank`, `ts_headline` —
      кросс-провайдерные boolean-предикаты `contains`/`freetext` уже были (`SupportsFullText`/
      `MakeFullText`); добавлена native-поверхность PostgreSQL в `PostgresFunctions`
      (`to_tsvector`/`to_tsquery`/`plainto_tsquery`/`phraseto_tsquery`/`websearch_to_tsquery`,
      `ts_match` → `@@`, `ts_rank`, `ts_headline`) под отдельным флагом
      `SupportsTextSearchFunctions` (base `false`, PostgreSQL `true`). `tsvector`/`tsquery`
      представлены `string` (Npgsql). Тесты: `SqlGenerationTests.TextSearchRank_*`/
      `TextSearchMatch_*`/`TextSearchQueryVariants_*`/`TextSearchHeadline_*`,
      `Sqlite/ClickHouse SQL-gen `TextSearch_ShouldThrow…`; `docs/guide/provider-specific/postgresql.md`.
- [x] `uuid`-генераторы (`gen_random_uuid`, `uuidv7`) и `pg_typeof` — `pg_typeof` (PG-only) в
      `ExtendedScalarFunctionTranslator` и оборачивается в `cast(... as text)`, т.к. `regtype` не
      читается драйвером. `gen_random_uuid`/`uuidv7` шагом 1 признаны кросс-провайдерными и
      промоутнуты в `CommonFunctions` (`SupportsUuidGenerators` + `SupportsUuidGenerator(name)` +
      `MakeUuidGenerator(name)`): SQL Server `newid()`, MariaDB `UUID_v4()`/`UUID_v7()`, ClickHouse
      `generateUUIDv4()`/`generateUUIDv7()`, MySQL/SQLite gated off. `uuidv7` — PG18+/MariaDB 11.7+
      (в контейнерах PG17/MySQL 8.4 только SQL-gen). `current_user`/`session_user`/`current_schema`/
      `current_database`/`version()` промоутнуты ранее — см. `docs/advanced/api-reference.md`.
      Тесты: `SqlGenerationTests.UuidGenerators_ShouldUsePostgresNames`,
      `PostgresSpecificTests.InfoFunctions_ShouldReturnServerValues` (реальный PostgreSQL).
**Вне области.** `RETURNING` и DML (`INSERT` / `UPDATE` / `DELETE` / `MERGE`) — за рамками проекта
на текущий момент: nextorm — read-only.

## Приоритетный шортлист

1. [x] `greatest` / `least` / `nullif`
2. [x] `string_agg` / `array_agg`
3. [x] `date_trunc`
4. [x] `FILTER (WHERE ...)` для агрегатов
5. [x] `unnest` / `generate_series`
