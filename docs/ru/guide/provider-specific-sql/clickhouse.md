# Специфичный для ClickHouse SQL

> У ClickHouse самая крупная эксклюзивная поверхность в nextorm: нативные массивы и `ARRAY JOIN`,
> модификаторы `LIMIT n BY expr` и `GROUP BY ... WITH TOTALS`, модификаторы запроса
> `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS`, строгость соединений и `GLOBAL JOIN`, распределённый
> предикат `GLOBAL IN`, собственные агрегаты, функции массивов, JSON и словарей, а также табличные
> функции `numbers`/`zeros`.

**Что нужно знать:** [Запросы и проекции](../../engine/index.md) · [Провайдер ClickHouse](../../connecting-providers/clickhouse.md)

> **Методы-расширения.** Каждый эксклюзивный для ClickHouse член на этой странице (`ArrayJoin`/
> `LeftArrayJoin`/`ArrayJoinElement`/`LeftArrayJoinElement`, `LimitBy`, `WithTotals`, `Final`,
> `PreWhere`, `Settings`, `Sample`, `SemiJoin`/`AntiJoin`/`PasteJoin` и `j => j.Global()`) — это
> метод-расширение из пакета `nextorm.clickhouse`, а не часть общего API `nextorm`. Добавьте
> `using NextORM.ClickHouse;`, и примеры компилируются без изменений.

## Массивы и `ARRAY JOIN`

У ClickHouse есть нативный тип `Array(T)`. Функции массивов работают с колонками-массивами или
вложенными выражениями, а клауза array-join разворачивает по одной строке на элемент:

```csharp
var rows = dataContext.From<IArrayEntity>()
    .LeftArrayJoin(e => e.Tags)
    .Where(e => e.Id > 0)
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id
 from array_entity
 left array join tags where id > 0
```

Скалярный `array_join` (одна строка на элемент, проекция) и методы-клаузы
`ArrayJoin`/`LeftArrayJoin`/`ArrayJoinElement`/`LeftArrayJoinElement` описаны в разделе
[Массивы (ClickHouse)](../../scalar-functions/06-arrays.md#массивы-clickhouse); функции массивов гейтятся
`SupportsArrayFunctions`, клауза — `ArrayJoinClause`.

## `LIMIT n BY expr`

[`LimitBy`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.LimitBy``2(NextORM.Core.EntityBuilder{``0},System.Int32,System.Int32,System.Linq.Expressions.Expression{System.Func{``0,``1}})) возвращает первые `n` строк **на каждое значение
ключа**, клауза рендерится после `ORDER BY` и до финального `LIMIT`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .OrderBy(x => x.Id)
    .LimitBy(2, x => x.Int)
    .Select(x => new { x.Id, x.Int })
    .ToList();
```

```sql
select id, nullableint
 from complex_entity order by id limit 2 by nullableint
```

См. [Сортировка и постраничная выборка](../06-sorting-and-paging.md#limit-by-clickhouse)
([`LimitBy`](xref:NextORM.Core.ISqlDialect.LimitBy)).

## `GROUP BY ... WITH TOTALS`

[`WithTotals`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.WithTotals``1(NextORM.Core.EntityBuilder{``0})) добавляет к группировке модификатор ClickHouse
`with totals` — строку итогов по всему набору. Он ортогонален `ROLLUP`/`CUBE` и не сочетается с
`GROUPING SETS`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .GroupBy(x => x.Int)
    .Select(x => new { x.Int, Count = SqlFunctions.Sql.count() })
    .WithTotals()
    .ToList();
```

```sql
select nullableint, count(*)
 from complex_entity group by nullableint with totals
```

См. [Группировка и агрегаты](../05-grouping-and-aggregates.md#with-totals)
([`SupportsGroupByWithTotals`](xref:NextORM.Core.ISqlDialect.SupportsGroupByWithTotals)). Официальный
`ClickHouse.Driver` возвращает блок итогов отдельно и не отдаёт его через `IDataReader`, поэтому
материализуются только строки групп.

## Модификаторы запроса: `FINAL`, `SAMPLE`, `PREWHERE`, `SETTINGS`

Четыре метода рендерят модификаторы уровня запроса ClickHouse: `Final()`, `Sample(ratio[, offset])`,
`PreWhere(predicate)` и `Settings(("key", "value"), ...)`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Final()
    .PreWhere(c => c.Int > 0)
    .Settings(("max_threads", "2"))
    .Select(c => new { c.Id })
    .ToList();
```

```sql
select id
 from complex_entity final prewhere (nullableint > 0) settings max_threads = 2
```

См. [Хинты запросов](../15-query-hints.md#модификаторы-запроса-clickhouse)
([`SupportsFinal`](xref:NextORM.Core.ISqlDialect.SupportsFinal)/[`SupportsSample`](xref:NextORM.Core.ISqlDialect.SupportsSample)/[`SupportsPreWhere`](xref:NextORM.Core.ISqlDialect.SupportsPreWhere)/[`SupportsSettings`](xref:NextORM.Core.ISqlDialect.SupportsSettings)).
`FINAL`/`PREWHERE` требуют движок таблицы, который их поддерживает, — движок `Memory` отклоняет оба.

## Строгость соединения и `GLOBAL JOIN`

Модификаторы соединения ClickHouse передаются соединению через его завершающую лямбду:
`j => j.WithStrictness(JoinStrictness.Any | All | Asof)` и `j => j.Global()`:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .LeftJoin<IComplexEntity>((s, c) => s.Id == c.Id,
        j => j.Global().WithStrictness(JoinStrictness.Any))
    .Select(p => new { p.Item1.Id, p.Item2.String })
    .ToList();
```

```sql
select t1.id, t2.somestring
 from simple_entity as `t1`
 global left any join complex_entity as `t2` on cast(t1.id as bigint) = t2.id
```

См. [Соединения](../04-joins.md#специфичные-для-провайдера-модификаторы-соединения-clickhouse)
([`SupportsJoinStrictness`](xref:NextORM.Core.ISqlDialect.SupportsJoinStrictness)/[`SupportsGlobalJoin`](xref:NextORM.Core.ISqlDialect.SupportsGlobalJoin)).
`ANY` оставляет одну правую строку на каждую левую, `ALL` — все совпадения, `ASOF` требует одну
колонку равенства и завершающее неравенство; `GLOBAL` рассылает правую сторону в распределённых
запросах. Для `SEMI`/`ANTI`/`PASTE` есть отдельные построители: `SemiJoin`/`AntiJoin` отдают только
левые колонки для левых строк, у которых есть (соответственно нет) совпадение, а `PasteJoin`
сопоставляет два источника по позиции строки без `ON`
([`SupportsSemiAntiJoin`](xref:NextORM.Core.ISqlDialect.SupportsSemiAntiJoin)/`SupportsPasteJoin`).

## `GLOBAL IN`

[`SqlFunctions.ClickHouse.global_in`](xref:NextORM.Core.ClickHouseFunctions.global_in``1(``0,NextORM.Core.QueryCommand{``0})) рендерит распределённый
предикат по подзапросу или списку значений; отрицание через `!` даёт `GLOBAL NOT IN`:

```csharp
var values = new int[] { 1, 3 };
var ids = dataContext.From<ISimpleEntity>()
    .Where(x => SqlFunctions.ClickHouse.global_in(x.Id, values))
    .Select(x => new { x.Id })
    .ToList();
```

```sql
select id
 from simple_entity where global in (@p0, @p1)
```

См. [Фильтрация](../03-filtering-where.md#global-in-clickhouse)
([`SupportsGlobalPredicates`](xref:NextORM.Core.ISqlDialect.SupportsGlobalPredicates)).

## Агрегаты

Эксклюзивные агрегаты ClickHouse живут на [`ClickHouseFunctions`](xref:NextORM.Core.ClickHouseFunctions)
и отклоняются всеми остальными диалектами. Их нативные типы результата (`UInt64`, `Float32`, целые
типы комбинаторов `-If`) не всегда совпадают с объявленным CLR-типом, поэтому диалект оборачивает их в
`toInt64(...)`/`toInt32(...)`/`toFloat64(...)`, нормализуя значение к типу, который объявляет метод:

* агрегаты числа уникальных `uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12` (`toInt64`);
* параметрические `quantile(level)(value)`/`quantile_exact`/`quantile_timing` и `median` (`toFloat64`), многоуровневые `quantiles(level1, level2, ...)(value)` (возвращают `Array(Float64)`, материализуются как CLR `double[]`; уровни должны быть inline-массивом);
* `median` — нативный агрегат семейства `quantile` (приблизительный; одна строка на группу) и единственное имя квантилеподобной формы, которое рендерится: общий `SqlFunctions.ClickHouse.percentile` и оконные `percentile_cont`/`percentile_disc` бросают `NotSupportedException`, и для этих переносимых написаний не создаётся псевдонима `quantile*`;
* агрегаты наиболее частых значений `topK(k)(value)`/`topKWeighted(k)(value, weight)` (возвращают `Array(T)`, материализуются как CLR `T[]`);
* агрегат последнего произвольного значения `any_last` (`anyLast`);
* возвращающие массивы агрегаты `group_array`/`group_uniq_array` (`groupArray`/`groupUniqArray`, материализуются как CLR `T[]`; `groupArray` над array-колонкой даёт вложенный `T[][]`);
* агрегаты последовательностей/воронки `window_funnel`/`sequence_match`/`retention` (`windowFunnel`/`sequenceMatch` с `toInt32(...)`; `retention` нормализуется к объявленному `int[]`);
* общий API фильтрации агрегатов, рендерящий комбинаторы `-If`: `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf`;
* `arg_min`/`arg_max`;
* битмап-агрегаты `group_bitmap`/`group_bitmap_and`/`group_bitmap_or`/`group_bitmap_xor`
  (`groupBitmap`/`groupBitmapAnd`/`groupBitmapOr`/`groupBitmapXor`; непрозрачное состояние `UInt64`) и
  агрегаты по ключам `sum_map`/`sum_map_filtered` (`sumMap`/`sumMapFiltered(keys)(key, value)`,
  проецируется как `Map`).

Функции, возвращающие массив, объявляют закрытый CLR-тип массива, и диалект нормализует результат
сервера к нему ровно одним внешним `cast(... as Array(...))`: `top_k`, `top_k_weighted`, `group_array`,
`group_uniq_array` (nullable-вход объявляет nullable-тип элемента, `int?[]` →
`Array(Nullable(Int32))`), `retention` (`int[]` → `Array(Int32)`, никогда не `byte[]`), `array_map` и
все три перегрузки `range` (`long[]` → `Array(Int64)`). Это только коррекция типа отрендеренного
вызова: она не меняет ни трансляцию лямбды, ни рендеринг операндов, поэтому описанные ниже ограничения
higher-order лямбд продолжают действовать. Привязка симметрична для входа: **связанный** массив ранга 1,
чей объявленный тип элемента nullable, отправляется как `Array(Nullable(T))`, выведенный из объявленного
CLR-типа, поэтому `null`-элементы и их позиции сохраняются (см.
[Массивы (ClickHouse)](../../scalar-functions/06-arrays.md#массивы-clickhouse)).

Переносимые агрегаты — в том числе `count`, произвольное значение `any_agg` (рендерится `ANY_VALUE` в
MySQL и MariaDB) и `corr`/`covar*` — документированы на тематической странице. Обычная колонка `UInt64`
(или любая проекция `ulong`/`ulong?`) материализуется напрямую через аксессор построителя строк
`DbDataReader.GetFieldValue<ulong>`, поэтому нормализующее приведение нужно только результатам функций
выше.

См. [Группировка и агрегаты](../05-grouping-and-aggregates.md).

## Нативные скалярные функции, Map и хэши

Помимо кросс-провайдерной скалярной поверхности, `ClickHouseFunctions` предоставляет нативные функции
ClickHouse, у которых нет переносимого написания. Они рендерят точное camel-case-имя и отклоняются
всеми остальными диалектами:

* UTF-8-регистр, trim и regexp/поиск по строкам: `lower_utf8`/`upper_utf8` (`lowerUTF8`/`upperUTF8`),
  `trim_left`/`trim_right`/`trim_both` (`trimLeft`/`trimRight`/`trimBoth`, с необязательным набором
  символов), `replace_regexp_one`/`replace_regexp_all`, `match`, `extract`, `extract_all` (проецируется
  как `string[]`) и `split_by_string`/`split_by_regexp`/`split_by_whitespace`;
* дата и время: `format_date_time` (`formatDateTime`, необязательный часовой пояс),
  `parse_date_time`/`parse_date_time_best_effort` (`parseDateTime`/`parseDateTimeBestEffort`) и
  текущая дата `now`/`today`/`yesterday`;
* операции над множествами массивов: `array_concat`, `array_flatten`, `array_uniq` (`arrayUniq`, в
  обёртке `toInt64`), `array_intersect`, `array_union`, `array_symmetric_difference`;
* семейство `Map`: конструктор `map` (inline-пары `Tuple.Create`), `map_keys`/`map_values`,
  `map_contains_key`/`map_contains_value`, `map_add`/`map_concat`, а также публичные
  высокопорядковые помощники `map_filter`/`map_apply`/`map_sort` (результат — кортежи и массивы,
  понижаются до массивных форм) и `map_all`/`map_exists` (булевы результаты, нативные предикаты);
* хэши: шесть строковых хэшей `md5`/`sha1`/`sha256`/`sha512`/`sip_hash128`/`murmur_hash3_128`
  (`MD5`/`SHA1`/`SHA256`/`SHA512`/`sipHash128`/`murmurHash3_128`, нижний шестнадцатеричный регистр) и
  числовые `xx_hash32`/`xx_hash64`/`xxh3`, `city_hash64`, `sip_hash64` и
  `murmur_hash2_32`/`murmur_hash2_64`/`murmur_hash3_32`/`murmur_hash3_64`;
* математика: `log2`, `cbrt`, `exp2` и `exp10` (рендерятся `log2(x)`/`cbrt(x)`/`exp2(x)`/`exp10(x)`), у
  каждой перегрузки `double` и `double?`, результат `Float64`/`Nullable(Float64)` (аргумент `NULL`
  остаётся `NULL`); остальные провайдеры и in-memory провайдер их отклоняют;
* `generate_ulid` (`generateULID`).

### Поставленные семейства (#286, T286b)

Таблицы ниже дополняют нативную поверхность. Каждый член — типизированный метод
`ClickHouseFunctions`, который рендерит точное имя вендора, явно объявляет CLR-форму результата и
отклоняется с `NotSupportedException` всеми остальными диалектами (общий шлюз
`IScalarFunctions`/capabilities). Закреплённый сервер — ClickHouse **25.8.33.6**
(`clickhouse/clickhouse-server:25.8-alpine`); имена, появившиеся позже, и CLR-формы, которые нельзя
материализовать, перечислены в разделе [Пока не поддерживается](#пока-не-поддерживается).

| Семейство | Члены |
|---|---|
| Строки | `overlay`, `initcap`, `soundex`, `base64_encode`, `base64_decode`, `hex`, `unhex`, `edit_distance`, `jaro_similarity`, `byte_hamming_distance`, `substring_index`, `count_substrings`, `extract_groups`, `normalize_utf8_nfc`, `normalize_utf8_nfd`, `normalize_utf8_nfkc`, `normalize_utf8_nfkd` |
| Числа и округление | `erf`, `erfc`, `lgamma`, `tgamma`, `gcd`, `lcm`, `is_finite`, `is_infinite`, `is_nan`, `hypot`, `int_div`, `int_exp2`, `int_exp10`, `sigmoid`, `sqr`; округление `round_age`, `round_bankers`, `round_down`, `round_duration`, `round_to_exp2` |
| Дата и время | `make_date`, `make_date32`, `make_date_time`, `now64`, `now_in_block`, `time_slot`, `time_slots`, `to_start_of_fifteen_minutes`, `to_start_of_five_minutes`, `to_start_of_ten_minutes`, `to_start_of_interval`, `to_iso_year`, `to_week`, `to_year_week`, `to_last_day_of_week`, `to_days_in_month`, `to_modified_julian_day`, `age`, `date_name`, `server_timezone`, `timezone_of`, `timezone_offset`, `to_time`, `timestamp`, `from_unix_timestamp`, `from_unix_timestamp64_milli`, `from_unix_timestamp64_micro` |
| Массивы | `array_element`, `array_enumerate_dense`, `array_zip`, `array_difference`, `array_avg`, `array_sum`, `array_min`, `array_max`, `array_product`, `array_top_k`, `array_bottom_k`, `array_random_sample`, `array_compact`, `array_pop_back`, `array_pop_front`, `array_push_front`, `array_resize`, `array_remove`, `array_rotate_left`, `array_rotate_right`, `array_shift_left`, `array_shift_right`, `array_fold`, `array_fill`, `array_reverse_fill`, `array_split`, `array_reverse_split`, `array_reverse_sort`, `array_partial_sort`, `array_sort`, `array_shingles`, `array_transpose`, `array_cum_sum_non_negative`, `count_equal`, `array_with_constant`, `array_jaccard_index`, `array_similarity` |
| Кортежи | `tuple_concat`, `tuple_names`, `flatten_tuple`, `tuple_to_name_value_pairs`, `tuple_plus`, `tuple_minus`, `tuple_multiply`, `tuple_divide`, `tuple_hamming_distance`, `dot_product` |
| Map | `map_subtract`, `map_update`, `map_reverse_sort`, `map_partial_sort`, `map_from_arrays`, `map_populate_series`, `extract_key_value_pairs` |
| JSON | `json_merge_patch`, `json_array_length`, `json_dynamic_paths`, `json_shared_data_paths` (у каждого есть `_with_types`-вариант), `json_key`, `json_extract_keys_and_values_raw`, `json_extract_uint`, `is_valid_json`, `simple_json_extract_raw`, `simple_json_extract_int`, `simple_json_extract_uint`, `simple_json_extract_float`, `simple_json_extract_bool`, `simple_json_extract_string` |
| Хэши | `sha224`, `sha384`, `sha512_256`, `blake3`, `md4`, `ripemd160`, `keccak256`, `farm_hash64`, `metro_hash64`, `java_hash`, `hive_hash`, `half_md5`, `url_hash`, `wy_hash64`, `int_hash32`, `int_hash64`, `ngram_min_hash`, `ngram_sim_hash`, `ngram_min_hash_arg`, `word_shingle_min_hash`, `word_shingle_sim_hash`, `word_shingle_min_hash_arg` |
| Кодирование | `bin`, `unbin`, `bitmask_to_array`, `bitmask_to_list`, `bit_positions_to_array`, `sqid_encode`, `sqid_decode`, `hilbert_encode`, `hilbert_decode`, `morton_encode`, `morton_decode`, `base32_encode`, `base32_decode`, `base58_encode`, `base58_decode`, `base64_url_encode`, `base64_url_decode`, `bech32_encode`, `bech32_decode` |
| UUID/ULID | `uuid_num_to_string`, `uuid_string_to_num`, `uuid_to_num`, `uuid_v7_to_datetime`, `to_uuid`, `to_uuid_or_default`, `to_uuid_or_null`, `ulid_string_to_datetime`, `generate_snowflake_id`, `date_time_to_snowflake_id`, `date_time64_to_snowflake_id`, `date_time_to_uuid_v7`, `snowflake_id_to_date_time`, `snowflake_id_to_date_time64` |
| URL/IP | `domain`, `protocol`, `path`, `query_string`, `fragment`, `port`, `cut_url_parameter`, `top_level_domain`, `encode_url_component`, `ipv4_num_to_string`, `ipv6_num_to_string`, `to_ipv4`, `to_ipv6`, `is_ipv4_string`, `is_ipv6_string`, `is_ip_address_in_range`, `ipv4_cidr_to_range`, `ipv6_cidr_to_range`, `extract_url_parameter`, `extract_url_parameter_names`, `cut_to_first_significant_subdomain`, `cut_to_first_significant_subdomain_with_www`, `cut_to_first_significant_subdomain_custom`, `cut_to_first_significant_subdomain_custom_with_www`, `url_hierarchy`, `url_path_hierarchy`, `domain_without_www`, `first_significant_subdomain`, `first_significant_subdomain_custom`, `path_full`, `query_string_and_fragment`, `cut_www`, `cut_query_string`, `cut_fragment`, `cut_query_string_and_fragment`, `decode_url_component`, `encode_url_form_component`, `decode_url_form_component`, `ipv4_string_to_num`, `ipv4_string_to_num_or_default`, `ipv4_string_to_num_or_null`, `ipv4_num_to_string_class_c`, `ipv6_string_to_num`, `ipv6_string_to_num_or_default`, `ipv6_string_to_num_or_null`, `ipv4_to_ipv6`, `cut_ipv6` |
| Биты | `bitAnd`, `bitOr`, `bitXor`, `bitNot`, `bitShiftLeft`, `bitShiftRight`, `bitRotateLeft`, `bitRotateRight`, `bitCount`, `bitTest`, `bitTestAll`, `bitTestAny`, `bitHammingDistance` |
| Приведение типов | `to_string`, `to_bool`, `to_fixed_string`, `to_low_cardinality`, `to_int8`…`to_int64`, `to_uint8`…`to_uint64`, `to_float32`, `to_float64`, `to_decimal32`, `to_decimal64`, `accurate_cast_*` с вариантами `_or_null`/`_or_default`, `reinterpret_as_uint8`…`reinterpret_as_uuid` |
| Агрегаты | `entropy`, `first_value`, `last_value`, `max_intersections` (+`max_intersections_position`), `simple_linear_regression`, `delta_sum`, `delta_sum_timestamp`, `gini`, `skew_pop`, `skew_samp`, `kurt_pop`, `kurt_samp`, `rank_corr`, `contingency`, `cramers_v`, `cramers_v_bias_corrected`, `theils_u`, `welch_t_test`, `mann_whitney_u_test`, `kolmogorov_smirnov_test`, `mean_z_test`, `analysis_of_variance`, `quantile_tdigest`, `quantile_gk`, `quantile_dd`, `quantile_bfloat16`, `quantile_prometheus_histogram`, `approx_top_k`, `approx_top_sum`, `any_heavy`, `max_map`, `min_map`, `sum_count`, `sum_kahan`, `group_array_moving_avg`, `group_array_moving_sum`, `group_array_sample`, `group_array_sorted`, `group_array_last`, `group_array_insert_at`, `group_array_intersect`, `group_concat`, `group_format`, `histogram`, `sparkbar`, `uniq_up_to`, `sequence_count`, `sequence_match_events`, `sequence_next_node`, `exponential_time_decayed_sum`, `exponential_time_decayed_max`, `exponential_time_decayed_count`, `exponential_time_decayed_avg`, `student_t_test`, `student_t_test_one_sample`, `quantiles`, `quantiles_exact`, `quantiles_exact_low`, `quantiles_exact_high`, `quantiles_exact_weighted`, `quantiles_timing`, `quantiles_tdigest`, `quantiles_bfloat16`, `quantiles_dd`, `quantiles_gk`, `quantiles_prometheus_histogram`, `time_series_group_sum`, `time_series_group_rate_sum`, `time_series_instant_rate_to_grid`, `time_series_rate_to_grid`, `time_series_resample_to_grid_with_staleness`, `group_array` |
| Оконные | `non_negative_derivative` (возвращает `WindowFunction<double>`, дополните `Over(...)`) |
| Словари | `dict_get_or_null`, `dict_get_all`, `dict_get_descendants`, `region_to_name`, `region_to_city`, `region_to_area`, `region_to_district`, `region_to_country`, `region_to_continent`, `region_to_top_continent`, `region_to_population`, `region_hierarchy`, `region_in` (плюс уже поставленные `dict_get`, `dict_get_or_default`, `dict_get_hierarchy`, `dict_get_children`, `dict_has`, `dict_is_in`) |

Объявленные CLR-формы точные, а не выведенные: результаты `UInt64`/`UInt32`/`UInt16` проецируются как
`ulong`/`uint`/`int`, `UInt8` — как `byte` или `bool`, результаты `FixedString` — как hex-текст
(бинарные UUID, IPv6 и кодирование), результаты `Tuple`/`Array` — как `System.Tuple<...>`/`T[]` с
явно выписанными типами компонентов, а каждый nullable-аргумент вендора даёт `NULL`, а не значение по
умолчанию. Члены с версионными ограничениями (`array_top_k`, `array_bottom_k`, `array_remove`,
`array_transpose`, `gini`, `quantile_prometheus_histogram`, `group_format`, сетки `time_series_*`)
сохраняют нативное плечо и фиксируют пробел сервера живым негативом `UNKNOWN_FUNCTION`/`BAD_ARGUMENTS`;
`sequence_next_node` и сетки time-series требуют `allow_experimental_funnel_functions` /
`allow_experimental_time_series_aggregate_functions`.

Шесть строковых хэшей кодируются на сервере в нижний шестнадцатеричный регистр, поэтому проекция
`string?` получает текст хэша, а не сырые байты `FixedString` (которые драйвер декодировал бы как
UTF-8 с потерями):

| Функция | Рендеринг SQL | Символов |
|---|---|---:|
| `md5` | `lower(hex(MD5(value)))` | 32 |
| `sha1` | `lower(hex(SHA1(value)))` | 40 |
| `sha256` | `lower(hex(SHA256(value)))` | 64 |
| `sha512` | `lower(hex(SHA512(value)))` | 128 |
| `sip_hash128` | `lower(hex(sipHash128(value)))` | 32 |
| `murmur_hash3_128` | `lower(hex(murmurHash3_128(value)))` | 32 |

Все символы входят в `[0-9a-f]`. Аргумент `NULL` даёт `NULL`: он не заменяется пустой строкой или
хэшем по умолчанию. Значение без изменений проходит обычную материализацию строки — через анонимную
проекцию, типизированную проекцию и `Scalar<string>` (это `string`, а не массив байтов).

`array_except` остаётся в `SqlFunctions.ClickHouse` для совместимости API, но **не
поддерживается** на закреплённой базовой версии 25.8: нативная `arrayExcept` появилась в
ClickHouse 25.9, поэтому вызов отклоняется с `NotSupportedException` на этапе трансляции и не
доходит до сервера. Остальные операции над множествами массивов не затронуты.

Нативный результат `Map` (конструктор `map`, `map_add`, `map_concat`) представляется как
`IDictionary<TKey, TValue>`; при проецировании комбинируйте его с `map_keys`/`map_values` или одним
из предикатов `map_contains_*`, потому что построитель строк не материализует «голую» колонку
`IDictionary`.

```csharp
var rows = dataContext.From<Event>()
    .Select(e => new
    {
        HasMatch = SqlFunctions.ClickHouse.match(e.Name, "error"),
        Keys = SqlFunctions.ClickHouse.map_keys(SqlFunctions.ClickHouse.map(Tuple.Create("a", 1L))),
        NewId = SqlFunctions.ClickHouse.generate_ulid()
    })
    .ToList();
```

Пять высокопорядковых помощников — публичные LINQ-методы, и объявленный CLR-тип результата входит в
контракт; нативную серверную функцию сохраняют только `map_all` и `map_exists`:

* `map_filter` — записи, удовлетворяющие предикату, как `Tuple<TKey, TValue>[]`. Понижается до
  `arrayFilter(_t -> predicate, arrayZip(mapKeys(map), mapValues(map)))`, где ключ и значение лямбды
  перепривязаны к полям пары `_t.1`/`_t.2`;
* `map_apply` — по одному результату лямбды на запись, в порядке карты, как `TResult[]`. Понижается
  до `arrayMap((k, v) -> expression, mapKeys(map), mapValues(map))`;
* `map_sort` — записи, упорядоченные по ключу, как `Tuple<TKey, TValue>[]`. Понижается до
  `arraySort(_t -> _t.1, arrayZip(mapKeys(map), mapValues(map)))`;
* `map_all`/`map_exists` — `bool`. Остаются нативными и рендерятся как
  `mapAll((k, v) -> predicate, map)` / `mapExists((k, v) -> predicate, map)`, лямбда первой.

Пониженное выражение filter, apply и sort оборачивается приведением **объявленного** типа результата,
а не того типа, который сервер вывел бы из операнда: `Tuple<string, int>[]` даёт
`cast(... as Array(Tuple(String, Int32)))`, результат `long` в apply — `cast(... as Array(Int64))`, —
поэтому сервер сообщает ровно тот тип элемента, который обещает объявление, а не более узкий. Тип
элемента выбирают два правила:

* результат apply **значимого типа** остаётся «голым» — `int` → `Array(Int32)`;
* результат apply **ссылочного типа** записывается как `Nullable(...)` — `string` →
  `Array(Nullable(String))` — потому что объявленный элемент допускает `null` (ветка `default(string)`
  в проекции — это реальный `NULL`), а ClickHouse `String` его не допускает; материализуемый
  CLR-тип при этом остаётся `string[]`.

Компоненты пары ключ/значение у `map_filter`/`map_sort` остаются «голыми», если только сам
CLR-компонент не `Nullable<T>`: ключи и значения карты необнуляемы.

```sql
-- map_filter: arrayFilter по парам из arrayZip, затем приведение объявленного типа
cast(arrayFilter(_t -> (_t.2 > 0),
                arrayZip(mapKeys(map('a', 1, 'b', -1)), mapValues(map('a', 1, 'b', -1))))
    as Array(Tuple(String, Int64)))

-- map_sort: arraySort по ключу тех же пар, затем приведение объявленного типа
cast(arraySort(_t -> _t.1,
              arrayZip(mapKeys(map('b', 2, 'a', 1)), mapValues(map('b', 2, 'a', 1))))
    as Array(Tuple(String, Int64)))

-- map_apply: по одному результату на запись, приведение к объявленному TResult — «голое»
-- для значимого типа, Nullable(...) для ссылочного, допускающего null
cast(arrayMap((k, v) -> (v * 2),
              mapKeys(map('a', 1, 'b', -1)), mapValues(map('a', 1, 'b', -1)))
    as Array(Int64))

cast(arrayMap((k, v) -> k,
              mapKeys(map('a', 1, 'b', -1)), mapValues(map('a', 1, 'b', -1)))
    as Array(Nullable(String)))
```

**Ломающее изменение в 1.0.9-rc4.** Раньше `map_filter` объявлял `IDictionary<TKey, TValue>`, теперь
возвращает `Tuple<TKey, TValue>[]`, потому что карту нельзя материализовать из проецируемого
выражения. `map_sort` (уже массив кортежей) и `map_apply` (уже `TResult[]`) сохраняют свои формы.

Публичного пути, превращающего отфильтрованную карту обратно в серверную `Map`, нет: `mapFilter`
достижима только через понижение `map_*`, `map_keys`/`map_values` требуют `IDictionary` и потому
отклоняют массив кортежей на этапе компиляции, а лямбды высокопорядковых функций массивов запрещают
доступ к полям параметра, поэтому `arrayMap(t => t.Item2, ...)` выразить нельзя. Переходите на
чтение возвращённых кортежей — обычный LINQ по массиву после материализации:
`filtered.Select(t => t.Item2)` для значений или
`filtered.ToDictionary(t => t.Item1, t => t.Item2)` для словаря на клиенте.

Нативная поверхность `Map` — `map_keys`/`map_values`, `map_contains_key`/`map_contains_value`,
`map_add`/`map_concat` и конструктор `map` — по-прежнему строится на `IDictionary<TKey, TValue>` и
отделена от этих пяти помощников, которые принимают операнд-карту, а возвращают массивы или
булевы значения.

См. [Скалярные функции](../../scalar-functions/index.md).

## JSON, словари и функции массивов

* извлекатели строкового JSON `JSONExtractString`/`JSONExtractInt`/`JSONExtractFloat`/
  `JSONExtractBool`/`JSONExtractRaw`/`JSONHas`/`JSONType`, `json_length` и плоский
  `visitParamExtract*`;
* возвращающие массивы `JSONExtractKeys`/`JSONExtractArrayRaw` (`json_extract_keys`/
  `json_extract_array_raw`, проецируются как `string[]`) и `JSONExtractKeysAndValues`
  (`json_extract_keys_and_values<T>`, проецируется как `Tuple<string, T>[]`; `T` — non-nullable
  ClickHouse-тип);
* JSONPath-скаляры `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (по строковому JSON);
* функции нативного JSON `json_all_paths`/`json_all_paths_with_types` (`JSONAllPaths`/
  `JSONAllPathsWithTypes`; первая проецируется как `string[]`, нативный `Map(String, String)` второй —
  как `Dictionary<string, string>`; обе принимают нативное значение `JSON`) и `to_json_string`
  (`toJSONString`);
* словарные функции `dict_get`/`dict_get_or_default`/`dict_has` и иерархические
  `dict_get_hierarchy`/`dict_get_children`/`dict_is_in` (нужен настроенный
  `CREATE DICTIONARY`);
* функции массивов `length`/`has`/`index_of`/`has_any`/`has_all`/`starts_with`/`ends_with`/
  `has_substr`/`array_string_concat`/`split_by_char`/`array_sort`/`array_reverse`/`array_distinct`/
  `range`/`array_enumerate`/`array_cum_sum`/`array_slice`/`array_push_back`;
* конструктор кортежа и доступ к элементу: `System.Tuple.Create(a, b)` рендерится как `tuple(a, b)`,
  а `System.Tuple<...>.ItemN` — как `tupleElement(t, n)` (целый `Tuple(...)` проецируется как
  `System.Tuple<...>`); `untuple` не поддерживается.

См. [Скалярные функции](../../scalar-functions/index.md) и [Поддержка JSON](../16-json.md)
([`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract)/[`SupportsDictionaries`](xref:NextORM.Core.ISqlDialect.SupportsDictionaries)).

## Табличные функции

`numbers`/`numbers_mt`, генераторы строк `zeros`/`zeros_mt`, `generate_random` и `values` доступны через
[`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})),
как и серверные/кластерные источники `url`, `s3`, `file`, `remote`, `remote_secure`, `cluster` и
`cluster_all_replicas`. Именно эти тринадцать имён принимает `ISqlDialect.SupportsTableFunction`.
Колонка `number` типа `UInt64` приводится к `Int64` внутри оборачивающего подзапроса, чтобы
материализоваться в CLR `long`; `zeros` материализуется сразу в `byte`:

```csharp
var rows = dataContext.FromTableFunction(() => SqlFunctions.ClickHouse.zeros(3))
    .Select(r => new { r.Value })
    .ToList();
```

Табличные функции внешних движков (`mysql`, `postgresql`, `sqlite`, `jdbc`, `odbc`, `mongodb`,
`redis`, `hdfs`, `s3Cluster`, `azureBlobStorage`, `deltaLake`, `iceberg`, `hudi`, `paimon`, `bigquery`,
`gcs`, `ytsaurus`, `arrowFlight`, `prometheusQuery`, `input`, `view`, `format`, `null`, `executable`,
`primes`, `merge`, `dictionary`, `eval`, `loop`, `fuzzJSON`, `fuzzQuery`) **не** моделируются: они
относятся к границе внешнего I/O, описанной как §4 каталога покрытия, и доступны только через сырой
SQL или обёртку `[SqlFunction]`. Диалект отклоняет каждую из них, а живой boundary-тест перебирает все
функции из `system.table_functions` и доказывает, что принимаются только тринадцать имён выше.

См. [Табличные функции](../13-table-valued-functions.md).

## Нативные JSON-колонки

Нативная колонка `JSON` в ClickHouse имеет корень-**объект** (массивы и скалярные корни отклоняются).
Свойство `[JsonColumn]` (storage `Auto` или `Native`) отображает POCO с корнем-объектом, `JsonObject`,
`JsonDocument` или `JsonElement` на неё и на чтение, и на запись; `Text` сохраняет колонку `String`, а
модель, не сериализующаяся в объект, отклоняется с `NotSupportedException`. «Голые» `JsonObject`/
`JsonDocument`/`JsonElement` с корнем-объектом тоже отображаются при проекции и как параметры (драйвер
отдаёт `JSON` как `JsonObject`, а `JsonDocument`/`JsonElement` из него адаптируются). Проецируйте через
именованную форму (`Select(x => new { x.Doc })` или DTO): «голый» скаляр верхнего уровня
`Select(x => x.Doc)` не обрабатывается ядровым классификатором проекции. Пустой `{}` остаётся непустым
значением, а SQL `NULL` в колонке `Nullable(JSON)` остаётся отличным от него (`null`, либо
`default`/`Undefined` для value-типа `JsonElement`). Чтение «голой» `string` из колонки `JSON` и
легаси-алиас `Object('json')` не поддерживаются; алиас создаётся только с connection-level параметром
`allow_experimental_object_type`, который nextorm никогда не выставляет, и даже тогда чтение
материализуется как `Tuple<SByte,String>`, а не через нативный транспорт `JsonObject`. См.
[Поддержка JSON](../16-json.md#отобразить-clr-объект-на-нативную-json-колонку).

## Пока не поддерживается

Каталог закрывает остаточный набор §3 на закреплённом сервере **25.8.33.6**: перечисленные ниже члены
существуют как типизированные методы-отказы на `ClickHouseFunctions` и падают с точным
`NotSupportedException` до отправки SQL провайдеру — они никогда не переписываются молча, не
подменяются значением по умолчанию и не усекаются.

| Семейство | Отклонённый член | Проверенная причина |
|---|---|---|
| Дата/время | `from_utc_timestamp` | Результат вендора не несёт зону в своём типе, а его «настенное» время — это время целевой зоны, поэтому пара (настенное время, зона) непредставима в `System.DateTime`. |
| Дата/время | `to_time64` | `Time64(scale)` не имеет CLR-носителя: `TimeSpan` отображается на хранение длительности `Int64`, и драйвер не может материализовать значение. |
| Дата/время | `from_unix_timestamp64_nano` | `DateTime64(9)` превышает разрешение `DateTime` в 100 нс и усекалось бы молча. |
| Массивы | `array_reduce`, `array_reduce_in_ranges` | Агрегат задаётся строкой SQL во время выполнения, поэтому типизированный контракт агрегата невозможен, а строка попала бы на сервер как непрозрачный фрагмент. |
| Кортежи | `untuple` | Подставляет динамический набор колонок с реализационными именами в место вызова, тогда как nextorm отображает одно выражение проекции ровно в одну колонку результата. |
| JSON | `pretty_print_json` | Появился в ClickHouse 26.4; отсутствует на закреплённом 25.8.33.6 (`UNKNOWN_FUNCTION`), документированной эквивалентной композиции нет. |
| JSON | `json_extract_uint_or_default` | Такой функции не существует; `json_extract_uint` уже возвращает дефолт вендора `0` для отсутствующего пути/неверного типа/битого JSON и `NULL` для `NULL`-входа. |
| Хэши | `ngram_sim_hash_arg`, `word_shingle_sim_hash_arg` | Таких функций нет: аргументы возвращают только формы `MinHashArg`; имена отсутствуют в `system.functions` на 25.8.33.6 и в справочнике вендора. |
| Приведение типов | `to_int128`, `to_int256`, `to_uint128`, `to_uint256`, `reinterpret_as_int128`, `reinterpret_as_int256`, `reinterpret_as_uint128`, `reinterpret_as_uint256` | Драйвер отдаёт 128/256-битные типы как `System.Numerics.BigInteger`; в общем построителе строк нет аксессора `BigInteger`, поэтому значение или битовый шаблон нельзя материализовать в целое фиксированной ширины. |
| Приведение типов | `to_decimal128`, `to_decimal256` | 38/76 значащих цифр против 96-битной мантиссы `System.Decimal` (28–29 цифр): значение выше этого диапазона потеряло бы точность молча или дало `OverflowException`. |
| Приведение типов | одиннадцать единиц `to_interval_*` | У результатов `Interval*`, существующих только в ClickHouse, нет CLR-представления, и сам драйвер не может их материализовать (`Unknown type: Interval*` на 25.8.33.6). |
| Агрегаты | `stochastic_linear_regression`, `stochastic_logistic_regression`, `exponential_moving_average` | Агрегат возвращает непрозрачное бинарное состояние `AggregateFunction` без типизированного CLR-результата; значение даёт только комбинатор `-Merge`, которому нужна колонка типа `AggregateFunction`, недоступная провайдеру. |
| Словари | `dict_get_keys`, `dict_get_root` | Появились в 25.12 и 26.7 соответственно; отсутствуют на закреплённом сервере. Используйте `dict_get_hierarchy` (последний элемент — для корня). |
| Словари | `region_to_parent`, `region_to_parents` | Таких функций в 25.8.33.6 нет; документированная композиция — `region_hierarchy` без первого элемента. |

Сквозной прогон серверных/кластерных табличных функций (`url`, `s3`, `file`, `remote`, `remote_secure`,
`cluster`, `cluster_all_replicas`) на реальном кластере **не** покрыт и остаётся вне области охвата. См.
[Ограничения и возможности вне области охвата](../../advanced/limitations.md).

## См. также

* [Провайдер ClickHouse](../../connecting-providers/clickhouse.md)
* [Специфичный для провайдеров SQL](overview.md)

---

Source: `src/nextorm.clickhouse/ClickHouseDialect.cs`, `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`,
`tests/nextorm.clickhouse.tests/SqlGeneration/SqlGenerationTests.cs`.
