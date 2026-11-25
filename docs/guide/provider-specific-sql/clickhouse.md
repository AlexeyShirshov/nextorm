# ClickHouse-specific SQL

> ClickHouse-specific queries use constructs beyond the shared `nextorm` API: native arrays and
> `ARRAY JOIN`, the `LIMIT n BY expr` and `GROUP BY ... WITH TOTALS` modifiers,
> `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS`, join strictness and `GLOBAL JOIN`, the distributed `GLOBAL IN`
> predicate, ClickHouse-specific aggregates and scalar functions for arrays, JSON and dictionaries,
> and the `numbers`/`zeros` table functions.

**Prerequisites:** [Querying and projections](../../engine/index.md) · [ClickHouse provider](../../connecting-providers/clickhouse.md)

> **Extension methods.** Every ClickHouse-only member on this page (`ArrayJoin`/`LeftArrayJoin`/
> `ArrayJoinElement`/`LeftArrayJoinElement`, `LimitBy`, `WithTotals`, `Final`, `PreWhere`, `Settings`,
> `Sample`, `SemiJoin`/`AntiJoin`/`PasteJoin` and `j => j.Global()`) is an extension method from the
> `nextorm.clickhouse` package, not part of the common `nextorm` API. Add `using NextORM.ClickHouse;`
> and the examples compile unchanged.

## Arrays and `ARRAY JOIN`

ClickHouse has a native `Array(T)` type. Array functions operate on array columns or nested array
expressions, and the array-join clause expands one row per element:

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

Use scalar `array_join` when you need its one-row-per-element result in a projection. Use
`ArrayJoin`/`LeftArrayJoin`/`ArrayJoinElement`/`LeftArrayJoinElement` to add the array-join clause to a
query. See [Arrays (ClickHouse)](../../scalar-functions/06-arrays.md#arrays-clickhouse) for both forms.
The dialect checks their support separately: `SupportsArrayFunctions` covers array functions, while
`ArrayJoinClause` covers the clause methods.

## `LIMIT n BY expr`

[`LimitBy`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.LimitBy``2(NextORM.Core.EntityBuilder{``0},System.Int32,System.Int32,System.Linq.Expressions.Expression{System.Func{``0,``1}})) returns the first `n` rows **per distinct key**, emitted
after `ORDER BY` and before the final `LIMIT`:

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

See [Sorting and paging](../06-sorting-and-paging.md#limit-by-clickhouse)
([`LimitBy`](xref:NextORM.Core.ISqlDialect.LimitBy)).

## `GROUP BY ... WITH TOTALS`

[`WithTotals`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.WithTotals``1(NextORM.Core.EntityBuilder{``0})) appends the ClickHouse `with totals` modifier to a
grouping, adding a totals row for the whole result set. It is orthogonal to `ROLLUP`/`CUBE` and cannot
be combined with `GROUPING SETS`:

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

See [Grouping and aggregates](../05-grouping-and-aggregates.md#with-totals)
([`SupportsGroupByWithTotals`](xref:NextORM.Core.ISqlDialect.SupportsGroupByWithTotals)). The totals
row is not included in the materialized result: the official `ClickHouse.Driver` returns its totals
block separately, outside `IDataReader`. The query above therefore materializes only the group rows.

## Query modifiers: `FINAL`, `SAMPLE`, `PREWHERE`, `SETTINGS`

Four builder methods render ClickHouse query-level modifiers: `Final()`, `Sample(ratio[, offset])`,
`PreWhere(predicate)` and `Settings(("key", "value"), ...)`:

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

The builder puts `SETTINGS` after the other clauses in the query tail. The order is `ORDER BY` →
`LIMIT ... BY` → `LIMIT`/`OFFSET` → `SETTINGS`. ClickHouse reports a syntax error (server Code 62)
if `LIMIT` follows `SETTINGS`.

This order also lets scalar terminal operators work on a query with settings. `First`,
`FirstOrDefault`, `Single` and `SingleOrDefault` add paging (`limit 1` or `limit 2`); the builder then
places `SETTINGS` after that paging clause.

```csharp
var count = dataContext.From<IComplexEntity>()
    .Settings(("max_threads", "2"))
    .Select(c => SqlFunctions.Sql.count())
    .First();
```

```sql
select toInt32(count(*))
 from complex_entity
limit 1
 settings max_threads = 2
```

See [Query hints](../15-query-hints.md#clickhouse-query-modifiers)
([`SupportsFinal`](xref:NextORM.Core.ISqlDialect.SupportsFinal)/[`SupportsSample`](xref:NextORM.Core.ISqlDialect.SupportsSample)/[`SupportsPreWhere`](xref:NextORM.Core.ISqlDialect.SupportsPreWhere)/[`SupportsSettings`](xref:NextORM.Core.ISqlDialect.SupportsSettings)).
`FINAL`/`PREWHERE` need a table engine that supports them — the `Memory` engine rejects both.

## Join strictness and `GLOBAL JOIN`

ClickHouse join modifiers are passed to the join through its trailing lambda:
`j => j.WithStrictness(JoinStrictness.Any | All | Asof)` and `j => j.Global()`:

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

See [Joins](../04-joins.md#provider-specific-join-modifiers-clickhouse)
([`SupportsJoinStrictness`](xref:NextORM.Core.ISqlDialect.SupportsJoinStrictness)/[`SupportsGlobalJoin`](xref:NextORM.Core.ISqlDialect.SupportsGlobalJoin)).
`ANY` keeps one right-hand row per left-hand row; `ALL` keeps every match. `ASOF` needs one equi-join
column followed by an inequality, and `GLOBAL` broadcasts the right side for distributed queries.

`SEMI`, `ANTI` and `PASTE` use dedicated builders. `SemiJoin` and `AntiJoin` return only the left-hand
columns, for left rows that do or do not have a match, respectively. `PasteJoin` pairs the two sources
by row position and has no `ON` clause
([`SupportsSemiAntiJoin`](xref:NextORM.Core.ISqlDialect.SupportsSemiAntiJoin)/`SupportsPasteJoin`).

## `GLOBAL IN`

[`SqlFunctions.ClickHouse.global_in`](xref:NextORM.Core.ClickHouseFunctions.global_in``1(``0,NextORM.Core.QueryCommand{``0})) renders the distributed
predicate over a subquery or a value list; negate with `!` for `GLOBAL NOT IN`:

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

See [Filtering](../03-filtering-where.md#global-in-clickhouse)
([`SupportsGlobalPredicates`](xref:NextORM.Core.ISqlDialect.SupportsGlobalPredicates)).

## Aggregates

ClickHouse-only aggregates are methods on
[`ClickHouseFunctions`](xref:NextORM.Core.ClickHouseFunctions); every other dialect rejects them.
ClickHouse's native result types (`UInt64`, `Float32` and the `-If` integer types) do not always match
the CLR return types declared by those methods. The dialect therefore casts results with
`toInt64(...)`, `toInt32(...)` or `toFloat64(...)` to match the declared type:

* distinct-count aggregates `uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12` (`toInt64`);
* parameterised `quantile(level)(value)`/`quantile_exact`/`quantile_timing` and `median` (`toFloat64`);
* multi-level `quantiles(level1, level2, ...)(value)` returns `Array(Float64)`, materialized as a
  CLR `double[]`; its levels must be an inline array;
* `median` is the native `quantile`-family aggregate. It is approximate and returns one row per group.
  It is also the only percentile-shaped name supported here: the generic
  `SqlFunctions.ClickHouse.percentile` and window `percentile_cont`/`percentile_disc` throw
  `NotSupportedException`. The portable spellings do not get `quantile*` aliases;
* the most-frequent `topK(k)(value)`/`topKWeighted(k)(value, weight)` aggregates (return `Array(T)`, materialised as a CLR `T[]`);
* the last-row arbitrary-value aggregate `any_last` (`anyLast`);
* the array-returning aggregates `group_array`/`group_uniq_array` (`groupArray`/`groupUniqArray`, materialised as a CLR `T[]`; `groupArray` of an array column yields a nested `T[][]`);
* the sequence/funnel aggregates `window_funnel`/`sequence_match`/`retention` (`windowFunnel`/`sequenceMatch` with `toInt32(...)`; `retention` is normalised to the declared `int[]`);
* the shared filtered-aggregate API rendered as the `-If` combinators `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf`;
* `arg_min`/`arg_max`;
* the bitmap aggregates `group_bitmap`/`group_bitmap_and`/`group_bitmap_or`/`group_bitmap_xor`
  (`groupBitmap`/`groupBitmapAnd`/`groupBitmapOr`/`groupBitmapXor`; the opaque `UInt64` state) and the
  per-key `sum_map`/`sum_map_filtered` (`sumMap`/`sumMapFiltered(keys)(key, value)`, surfaced as a
  `Map`).

The array-returning facades declare a specific CLR array type. The dialect uses one outer
`cast(... as Array(...))` to make the server result match that type. This applies to `top_k`,
`top_k_weighted`, `group_array`, `group_uniq_array`, `retention` (`int[]` → `Array(Int32)`, never
`byte[]`), `array_map` and all three `range` overloads (`long[]` → `Array(Int64)`).

The declared CLR element type determines the ClickHouse array element type:

* A **value-type** element keeps its declared nullability: `int` → `Array(Int32)`, `int?` →
  `Array(Nullable(Int32))`; `byte` stays `UInt8`.
* A **reference-type** element is always written as `Nullable(...)`. CLR nullable annotations on
  reference-type elements are erased at runtime, so `string[]` and `string?[]` both become
  `Array(Nullable(String))`. A jagged `string[][]` becomes `Array(Array(Nullable(String)))`: the
  array type is applied first, never `Nullable(Array(...))`.

For a `string[]` result, this changes only the SQL type declaration; the data, materialized CLR result
type (`string[]`) and public API stay the same. Value-type arrays (`int[]`, `int?[]`, `byte[]`) are
unaffected. This type correction applies to the rendered call only: it does not change lambda
translation or operand rendering, so the higher-order lambda limitations below still apply.

Input binding follows the same nullable-element rule. A **bound**, one-dimensional (rank-1) array
with a nullable declared element type is sent as `Array(Nullable(T))`, based on its declared CLR type. This preserves
both `null` elements and their positions; see
[Arrays (ClickHouse)](../../scalar-functions/06-arrays.md#arrays-clickhouse).

Portable aggregates — including `count`, the arbitrary-value `any_agg` (rendered as `ANY_VALUE` on MySQL
and MariaDB) and `corr`/`covar*` — are documented on the concept page, not here.

A plain `UInt64` column, or any `ulong`/`ulong?` projection, needs no normalizing cast. The row reader
reads it directly with `DbDataReader.GetFieldValue<ulong>`. Only the aggregate results above need the
casts described here.

See [Grouping and aggregates](../05-grouping-and-aggregates.md).

## Native scalar functions, maps and hashes

In addition to scalar functions shared across providers, `ClickHouseFunctions` exposes native
ClickHouse functions with no portable spelling. Each uses its native ClickHouse name in SQL, and every
other dialect rejects it:

* UTF-8 case, trim and regexp/search strings: `lower_utf8`/`upper_utf8` (`lowerUTF8`/`upperUTF8`),
  `trim_left`/`trim_right`/`trim_both` (`trimLeft`/`trimRight`/`trimBoth`, with an optional character
  set), `replace_regexp_one`/`replace_regexp_all`, `match`, `extract`, `extract_all` (projects as
  `string[]`) and `split_by_string`/`split_by_regexp`/`split_by_whitespace`;
* date and time: `format_date_time` (`formatDateTime`, optional time zone),
  `parse_date_time`/`parse_date_time_best_effort` (`parseDateTime`/`parseDateTimeBestEffort`) and the
  current-date helpers `now`/`today`/`yesterday`;
* array set operations: `array_concat`, `array_flatten`, `array_uniq` (`arrayUniq`, wrapped in
  `toInt64`), `array_intersect`, `array_union`, `array_symmetric_difference`, and `array_except`
  (`arrayExcept`, available only when the context declares ClickHouse 25.9 or later — see
  [Declaring the server version](../../connecting-providers/clickhouse.md#declaring-the-server-version));
* the `Map` family: the `map` constructor (inline `Tuple.Create` pairs), `map_keys`/`map_values`,
  `map_contains_key`/`map_contains_value`, `map_add`/`map_concat`, and the public higher-order
  helpers `map_filter`/`map_apply`/`map_sort` (tuple and array results, lowered to array forms) plus
  `map_all`/`map_exists` (booleans, native predicates);
* hashes: the six string-returning digests `md5`/`sha1`/`sha256`/`sha512`/`sip_hash128`/
  `murmur_hash3_128` (`MD5`/`SHA1`/`SHA256`/`SHA512`/`sipHash128`/`murmurHash3_128`, lowercase
  hexadecimal) plus the numeric `xx_hash32`/`xx_hash64`/`xxh3`, `city_hash64`, `sip_hash64` and
  `murmur_hash2_32`/`murmur_hash2_64`/`murmur_hash3_32`/`murmur_hash3_64`;
* math: `log2`, `cbrt`, `exp2` and `exp10` (rendered `log2(x)`/`cbrt(x)`/`exp2(x)`/`exp10(x)`), each
  with a `double` and a `double?` overload returning `Float64`/`Nullable(Float64)` (a `NULL` argument
  stays `NULL`); the other providers and the in-memory provider reject them;
* `generate_ulid` (`generateULID`).

### Function families

The table groups native functions by purpose. Each entry is a typed
`ClickHouseFunctions` method: it uses the exact ClickHouse name in SQL and declares its CLR result
shape. A shared `IScalarFunctions` capability check rejects the method with `NotSupportedException`
on other dialects. The baseline server is ClickHouse **25.8.33.6**
(`clickhouse/clickhouse-server:25.8-alpine`). Functions introduced later than that version, and result
shapes the provider cannot materialize, are listed under
[Unsupported functions](#unsupported-functions).

| Family | Members |
|---|---|
| String | `overlay`, `initcap`, `soundex`, `base64_encode`, `base64_decode`, `hex`, `unhex`, `edit_distance`, `jaro_similarity`, `byte_hamming_distance`, `substring_index`, `count_substrings`, `extract_groups`, `normalize_utf8_nfc`, `normalize_utf8_nfd`, `normalize_utf8_nfkc`, `normalize_utf8_nfkd` |
| Numeric and rounding | `erf`, `erfc`, `lgamma`, `tgamma`, `gcd`, `lcm`, `is_finite`, `is_infinite`, `is_nan`, `hypot`, `int_div`, `int_exp2`, `int_exp10`, `sigmoid`, `sqr`; rounding `round_age`, `round_bankers`, `round_down`, `round_duration`, `round_to_exp2` |
| Date and time | `make_date`, `make_date32`, `make_date_time`, `now64`, `now_in_block`, `time_slot`, `time_slots`, `to_start_of_fifteen_minutes`, `to_start_of_five_minutes`, `to_start_of_ten_minutes`, `to_start_of_interval`, `to_iso_year`, `to_week`, `to_year_week`, `to_last_day_of_week`, `to_days_in_month`, `to_modified_julian_day`, `age`, `date_name`, `server_timezone`, `timezone_of`, `timezone_offset`, `to_time`, `timestamp`, `from_unix_timestamp`, `from_unix_timestamp64_milli`, `from_unix_timestamp64_micro` |
| Array | `array_element`, `array_enumerate_dense`, `array_zip`, `array_difference`, `array_avg`, `array_sum`, `array_min`, `array_max`, `array_product`, `array_top_k`, `array_bottom_k`, `array_random_sample`, `array_compact`, `array_pop_back`, `array_pop_front`, `array_push_front`, `array_resize`, `array_remove`, `array_rotate_left`, `array_rotate_right`, `array_shift_left`, `array_shift_right`, `array_fold`, `array_fill`, `array_reverse_fill`, `array_split`, `array_reverse_split`, `array_reverse_sort`, `array_partial_sort`, `array_sort`, `array_shingles`, `array_transpose`, `array_cum_sum_non_negative`, `count_equal`, `array_with_constant`, `array_jaccard_index`, `array_similarity` |
| Tuple | `tuple_concat`, `tuple_names`, `flatten_tuple`, `tuple_to_name_value_pairs`, `tuple_plus`, `tuple_minus`, `tuple_multiply`, `tuple_divide`, `tuple_hamming_distance`, `dot_product` |
| Map | `map_subtract`, `map_update`, `map_reverse_sort`, `map_partial_sort`, `map_from_arrays`, `map_populate_series`, `extract_key_value_pairs` |
| JSON | `json_merge_patch`, `json_array_length`, `json_dynamic_paths`, `json_shared_data_paths` (each with its `_with_types` sibling), `json_key`, `json_extract_keys_and_values_raw`, `json_extract_uint`, `is_valid_json`, `simple_json_extract_raw`, `simple_json_extract_int`, `simple_json_extract_uint`, `simple_json_extract_float`, `simple_json_extract_bool`, `simple_json_extract_string` |
| Hash | `sha224`, `sha384`, `sha512_256`, `blake3`, `md4`, `ripemd160`, `keccak256`, `farm_hash64`, `metro_hash64`, `java_hash`, `hive_hash`, `half_md5`, `url_hash`, `wy_hash64`, `int_hash32`, `int_hash64`, `ngram_min_hash`, `ngram_sim_hash`, `ngram_min_hash_arg`, `word_shingle_min_hash`, `word_shingle_sim_hash`, `word_shingle_min_hash_arg` |
| Encoding | `bin`, `unbin`, `bitmask_to_array`, `bitmask_to_list`, `bit_positions_to_array`, `sqid_encode`, `sqid_decode`, `hilbert_encode`, `hilbert_decode`, `morton_encode`, `morton_decode`, `base32_encode`, `base32_decode`, `base58_encode`, `base58_decode`, `base64_url_encode`, `base64_url_decode`, `bech32_encode`, `bech32_decode` |
| UUID/ULID | `uuid_num_to_string`, `uuid_string_to_num`, `uuid_to_num`, `uuid_v7_to_datetime`, `to_uuid`, `to_uuid_or_default`, `to_uuid_or_null`, `ulid_string_to_datetime`, `generate_snowflake_id`, `date_time_to_snowflake_id`, `date_time64_to_snowflake_id`, `date_time_to_uuid_v7`, `snowflake_id_to_date_time`, `snowflake_id_to_date_time64` |
| URL/IP | `domain`, `protocol`, `path`, `query_string`, `fragment`, `port`, `cut_url_parameter`, `top_level_domain`, `encode_url_component`, `ipv4_num_to_string`, `ipv6_num_to_string`, `to_ipv4`, `to_ipv6`, `is_ipv4_string`, `is_ipv6_string`, `is_ip_address_in_range`, `ipv4_cidr_to_range`, `ipv6_cidr_to_range`, `extract_url_parameter`, `extract_url_parameter_names`, `cut_to_first_significant_subdomain`, `cut_to_first_significant_subdomain_with_www`, `cut_to_first_significant_subdomain_custom`, `cut_to_first_significant_subdomain_custom_with_www`, `url_hierarchy`, `url_path_hierarchy`, `domain_without_www`, `first_significant_subdomain`, `first_significant_subdomain_custom`, `path_full`, `query_string_and_fragment`, `cut_www`, `cut_query_string`, `cut_fragment`, `cut_query_string_and_fragment`, `decode_url_component`, `encode_url_form_component`, `decode_url_form_component`, `ipv4_string_to_num`, `ipv4_string_to_num_or_default`, `ipv4_string_to_num_or_null`, `ipv4_num_to_string_class_c`, `ipv6_string_to_num`, `ipv6_string_to_num_or_default`, `ipv6_string_to_num_or_null`, `ipv4_to_ipv6`, `cut_ipv6` |
| Bit | `bitAnd`, `bitOr`, `bitXor`, `bitNot`, `bitShiftLeft`, `bitShiftRight`, `bitRotateLeft`, `bitRotateRight`, `bitCount`, `bitTest`, `bitTestAll`, `bitTestAny`, `bitHammingDistance` |
| Type conversion | `to_string`, `to_bool`, `to_fixed_string`, `to_low_cardinality`, `to_int8`…`to_int64`, `to_uint8`…`to_uint64`, `to_float32`, `to_float64`, `to_decimal32`, `to_decimal64`, `accurate_cast_*` with the `_or_null`/`_or_default` variants, `reinterpret_as_uint8`…`reinterpret_as_uuid` |
| Aggregate | `entropy`, `first_value`, `last_value`, `max_intersections` (+`max_intersections_position`), `simple_linear_regression`, `delta_sum`, `delta_sum_timestamp`, `gini`, `skew_pop`, `skew_samp`, `kurt_pop`, `kurt_samp`, `rank_corr`, `contingency`, `cramers_v`, `cramers_v_bias_corrected`, `theils_u`, `welch_t_test`, `mann_whitney_u_test`, `kolmogorov_smirnov_test`, `mean_z_test`, `analysis_of_variance`, `quantile_tdigest`, `quantile_gk`, `quantile_dd`, `quantile_bfloat16`, `quantile_prometheus_histogram`, `approx_top_k`, `approx_top_sum`, `any_heavy`, `max_map`, `min_map`, `sum_count`, `sum_kahan`, `group_array_moving_avg`, `group_array_moving_sum`, `group_array_sample`, `group_array_sorted`, `group_array_last`, `group_array_insert_at`, `group_array_intersect`, `group_concat`, `group_format`, `histogram`, `sparkbar`, `uniq_up_to`, `sequence_count`, `sequence_match_events`, `sequence_next_node`, `exponential_time_decayed_sum`, `exponential_time_decayed_max`, `exponential_time_decayed_count`, `exponential_time_decayed_avg`, `student_t_test`, `student_t_test_one_sample`, `quantiles`, `quantiles_exact`, `quantiles_exact_low`, `quantiles_exact_high`, `quantiles_exact_weighted`, `quantiles_timing`, `quantiles_tdigest`, `quantiles_bfloat16`, `quantiles_dd`, `quantiles_gk`, `quantiles_prometheus_histogram`, `time_series_group_sum`, `time_series_group_rate_sum`, `time_series_instant_rate_to_grid`, `time_series_rate_to_grid`, `time_series_resample_to_grid_with_staleness`, `group_array` |
| Window | `non_negative_derivative` (returns `WindowFunction<double>`, complete it with `Over(...)`) |
| Dictionary | `dict_get_or_null`, `dict_get_all`, `dict_get_descendants`, `region_to_name`, `region_to_city`, `region_to_area`, `region_to_district`, `region_to_country`, `region_to_continent`, `region_to_top_continent`, `region_to_population`, `region_hierarchy`, `region_in` (plus the already shipped `dict_get`, `dict_get_or_default`, `dict_get_hierarchy`, `dict_get_children`, `dict_has`, `dict_is_in`) |

The CLR result types are declared by each method; they are not inferred from the server result.
`UInt64`/`UInt32`/`UInt16` results use `ulong`/`uint`/`int`, and `UInt8` uses `byte` or `bool`.
`FixedString` results for binary UUID, IPv6 and encoding values become hexadecimal text.
`Tuple`/`Array` results use `System.Tuple<...>`/`T[]` with their component types specified. A nullable
vendor argument propagates `NULL`; it does not substitute a default value.

Some functions depend on the server version: `array_top_k`, `array_bottom_k`, `array_remove`,
`array_transpose`, `gini`, `quantile_prometheus_histogram`, `group_format` and the `time_series_*`
grids. Live negative tests check the pinned server's `UNKNOWN_FUNCTION`/`BAD_ARGUMENTS` responses for
these functions. `sequence_next_node` requires `allow_experimental_funnel_functions`; the time-series
grids require `allow_experimental_time_series_aggregate_functions`.

The six string-returning digests are encoded on the server as lowercase hexadecimal. A `string?`
projection therefore receives digest text rather than raw `FixedString` bytes, which the driver would
decode as lossy UTF-8:

| Function | Rendered SQL | Characters |
|---|---|---:|
| `md5` | `lower(hex(MD5(value)))` | 32 |
| `sha1` | `lower(hex(SHA1(value)))` | 40 |
| `sha256` | `lower(hex(SHA256(value)))` | 64 |
| `sha512` | `lower(hex(SHA512(value)))` | 128 |
| `sip_hash128` | `lower(hex(sipHash128(value)))` | 32 |
| `murmur_hash3_128` | `lower(hex(murmurHash3_128(value)))` | 32 |

Every character is in `[0-9a-f]`. A `NULL` argument propagates as `NULL` — it is never replaced by an
empty string or a default digest — and the value survives ordinary string materialisation unchanged
through an anonymous projection, a typed projection and `Scalar<string>` (a plain `string`, not a byte
array).

`array_except` remains in `SqlFunctions.ClickHouse` for API compatibility, but its availability depends
on the ClickHouse version declared for the context. ClickHouse introduced the native `arrayExcept` in
version 25.9:

| Declaration | Behaviour |
|---|---|
| none (the pinned 25.8 baseline, the default) | `NotSupportedException` with the message `The array_except function is not supported by this provider.` — nothing is emitted |
| earlier than 25.9 (for example `25.8`) | `NotSupportedException` naming the required `25.9` and the declared version, thrown before any SQL |
| 25.9 or later | the native `arrayExcept(...)`, which takes exactly two arrays; any other arity is refused before SQL |

The declaration is per context, so two versions need distinct context subclasses; see
[Declaring the server version](../../connecting-providers/clickhouse.md#declaring-the-server-version).
The other array set operations are unaffected and never consult the version.

A native `Map` result from the `map` constructor, `map_add` or `map_concat` has the CLR type
`IDictionary<TKey, TValue>`. The row mapper cannot materialize a bare `IDictionary` column. When you
project a map, select `map_keys`/`map_values` or use a `map_contains_*` predicate instead.

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

Five public LINQ helpers accept lambdas for map operations; their declared CLR result types are part of
the contract. `map_filter`, `map_apply` and `map_sort` are translated into array operations. Only
`map_all` and `map_exists` keep native server functions:

* `map_filter` — the entries matching the predicate, as `Tuple<TKey, TValue>[]`. Lowered to
  `arrayFilter(_t -> predicate, arrayZip(mapKeys(map), mapValues(map)))`, with the lambda's key and
  value rebound to the pair fields `_t.1`/`_t.2`;
* `map_apply` — one lambda result per entry, in map order, as `TResult[]`. Lowered to
  `arrayMap((k, v) -> expression, mapKeys(map), mapValues(map))`;
* `map_sort` — the entries ordered by key, as `Tuple<TKey, TValue>[]`. Lowered to
  `arraySort(_t -> _t.1, arrayZip(mapKeys(map), mapValues(map)))`;
* `map_all`/`map_exists` — `bool`. Kept native and rendered as
  `mapAll((k, v) -> predicate, map)` / `mapExists((k, v) -> predicate, map)`, lambda first.

The translated `map_filter`, `map_apply` and `map_sort` expressions are wrapped in a SQL `CAST` to the
**declared** result type, not the type the server would infer from the input. For example,
`Tuple<string, int>[]` becomes `cast(... as Array(Tuple(String, Int32)))`, and a `long` result from
`map_apply` becomes `cast(... as Array(Int64))`. This makes the server return the element type promised
by the method declaration, even when its inferred type would be narrower. The element type follows two
rules:

* a **value-type** apply result stays bare — `int` → `Array(Int32)`;
* a **reference-type** apply result is spelled `Nullable(...)` — `string` → `Array(Nullable(String))` —
  because the declared element admits `null` (the `default(string)` branch of a projection is a real
  `NULL`) while ClickHouse `String` does not; the materialised CLR type stays `string[]`.

The key/value tuple components of `map_filter`/`map_sort` stay bare unless the CLR component itself is
`Nullable<T>`, because the map keys and values are non-nullable.

```sql
-- map_filter: arrayFilter over the zipped pairs, then the declared-type cast
cast(arrayFilter(_t -> (_t.2 > 0),
                arrayZip(mapKeys(map('a', 1, 'b', -1)), mapValues(map('a', 1, 'b', -1))))
    as Array(Tuple(String, Int64)))

-- map_sort: arraySort by key over the same pairs, then the declared-type cast
cast(arraySort(_t -> _t.1,
              arrayZip(mapKeys(map('b', 2, 'a', 1)), mapValues(map('b', 2, 'a', 1))))
    as Array(Tuple(String, Int64)))

-- map_apply: one result per entry, cast as the declared TResult — bare for a value type,
-- Nullable(...) for a reference type that admits null
cast(arrayMap((k, v) -> (v * 2),
              mapKeys(map('a', 1, 'b', -1)), mapValues(map('a', 1, 'b', -1)))
    as Array(Int64))

cast(arrayMap((k, v) -> k,
              mapKeys(map('a', 1, 'b', -1)), mapValues(map('a', 1, 'b', -1)))
    as Array(Nullable(String)))
```

`map_filter` returns `Tuple<TKey, TValue>[]`, because a map cannot be materialised from a projected
expression. `map_sort` returns a tuple array and `map_apply` returns `TResult[]`.

There is no public operation that converts a filtered map back into a server-side `Map`.
`mapFilter` is reached only through the `map_*` translation. `map_keys`/`map_values` require an
`IDictionary`, so they reject the returned tuple array at compile time. Higher-order array lambdas also
do not allow member access on their parameter, so you cannot express `arrayMap(t => t.Item2, ...)`.

After the array is materialized, use ordinary LINQ to read the returned tuples:
`filtered.Select(t => t.Item2)` gets the values, and
`filtered.ToDictionary(t => t.Item1, t => t.Item2)` creates a dictionary on the client.

The native `Map` surface — `map_keys`/`map_values`, `map_contains_key`/`map_contains_value`,
`map_add`/`map_concat` and the `map` constructor — stays `IDictionary<TKey, TValue>`-based and is
separate from these five helpers, which take a map operand and return arrays or booleans.

See [Scalar functions](../../scalar-functions/index.md).

## JSON, dictionaries and array functions

* For JSON stored as a string, use the extractors `JSONExtractString`/`JSONExtractInt`/`JSONExtractFloat`/`JSONExtractBool`/
  `JSONExtractRaw`/`JSONHas`/`JSONType`, `json_length`, and the flat-JSON `visitParamExtract*`;
* Array-returning JSON functions include `JSONExtractKeys`/`JSONExtractArrayRaw` (`json_extract_keys`/`json_extract_array_raw`,
  projecting as `string[]`) and `JSONExtractKeysAndValues` (`json_extract_keys_and_values<T>`, projecting
  as `Tuple<string, T>[]`; `T` must be a non-nullable ClickHouse type);
* JSONPath functions `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` operate on JSON stored as a string;
* For a native `JSON` value, use `json_all_paths`/`json_all_paths_with_types` (`JSONAllPaths`/
  `JSONAllPathsWithTypes`; the former projects as `string[]`, the latter's native `Map(String, String)`
  as `Dictionary<string, string>`; both take a native `JSON` value) and `to_json_string`
  (`toJSONString`);
* Dictionary lookups include `dict_get`/`dict_get_or_default`/`dict_has` and the hierarchy
  `dict_get_hierarchy`/`dict_get_children`/`dict_is_in` (need a configured `CREATE DICTIONARY`);
* Array functions include `length`/`has`/`index_of`/`has_any`/`has_all`/`starts_with`/`ends_with`/`has_substr`/
  `array_string_concat`/`split_by_char`/`array_sort`/`array_reverse`/`array_distinct`/`range`/
  `array_enumerate`/`array_cum_sum`/`array_slice`/`array_push_back`;
* Tuple construction and element access: `System.Tuple.Create(a, b)` becomes `tuple(a, b)`, and
  `System.Tuple<...>.ItemN` becomes `tupleElement(t, n)` (a whole `Tuple(...)` projects as
  `System.Tuple<...>`); `untuple` is not supported.

See [Scalar functions](../../scalar-functions/index.md) and [JSON support](../16-json.md)
([`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract)/[`SupportsDictionaries`](xref:NextORM.Core.ISqlDialect.SupportsDictionaries)).

## Table functions

To query these sources, pass a factory lambda to
[`From`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{NextORM.Core.EntityBuilder{``0}}})): `numbers`/`numbers_mt`,
the row-count generators `zeros`/`zeros_mt`, `generate_random`, `values`, and the server/cluster
sources `url`, `s3`, `file`, `remote`, `remote_secure`, `cluster` and `cluster_all_replicas`. The
dialect's `SupportsTableFunction` check accepts exactly these thirteen names. The `number` column is
`UInt64`; a wrapping subquery casts it to `Int64` so it materializes as a CLR `long`. The `zeros` result
materializes directly as `byte`:

```csharp
var rows = dataContext.From(() => SqlFunctions.ClickHouse.zeros(3))
    .Select(r => new { r.Value })
    .ToList();
```

Other table functions connect to external engines or I/O systems: `mysql`, `postgresql`, `sqlite`,
`jdbc`, `odbc`, `mongodb`,
`redis`, `hdfs`, `s3Cluster`, `azureBlobStorage`, `deltaLake`, `iceberg`, `hudi`, `paimon`, `bigquery`,
`gcs`, `ytsaurus`, `arrowFlight`, `prometheusQuery`, `input`, `view`, `format`, `null`, `executable`,
`primes`, `merge`, `dictionary`, `eval`, `loop`, `fuzzJSON` and `fuzzQuery` are not modeled by the
provider. They belong to the external-I/O boundary documented as §4 of the coverage catalog and are
reachable only through raw SQL or a `[SqlFunction]` wrapper. The dialect rejects all of them. A live
boundary test checks every function in the server's `system.table_functions` and verifies that only the
thirteen names above are accepted.

Integration tests verify that nextorm reads rows through `remote` using a loopback address, `cluster`
using a named single-shard cluster, `file` using a CSV in the server's `user_files_path`, and `s3`
using a CSV object in S3-compatible storage. These tests don't cover multi-node clusters or
end-to-end execution of `url`, `remote_secure`, or `cluster_all_replicas`.

See [Table-valued functions](../13-table-valued-functions.md).

## Native JSON columns

ClickHouse's native `JSON` column requires an **object root**; it rejects array and scalar roots.

For mapped properties, `[JsonColumn]` with `Auto` or `Native` storage maps an object-root POCO,
`JsonObject`, `JsonDocument` or `JsonElement` to the native column on both reads and writes. `Text`
storage keeps a `String` column. If a model does not serialize to an object, nextorm throws
`NotSupportedException`.

Bare object-root `JsonObject`, `JsonDocument` and `JsonElement` values also work in projections and as
parameters. The driver returns `JSON` as `JsonObject`; nextorm adapts that value to `JsonDocument` or
`JsonElement` when needed. Project through a named shape, such as `Select(x => new { x.Doc })` or a
DTO. The core projection classifier does not handle a bare top-level scalar projection
(`Select(x => x.Doc)`).

An empty `{}` remains a non-null value. SQL `NULL` in a `Nullable(JSON)` column remains distinct: it
reads as `null`, or as `default`/`Undefined` for the value-type `JsonElement`.

Reading a bare `string` from a `JSON` column is unsupported. The legacy `Object('json')` alias is also
unsupported: creating it requires the connection-level `allow_experimental_object_type` setting, which
nextorm never sets. Even with that setting, reads materialize as `Tuple<SByte,String>`, not through the
native `JsonObject` transport. See
[JSON support](../16-json.md#map-a-clr-object-to-a-native-json-column).

## Null-safe comparison

ClickHouse accepts `isNotDistinctFrom(...)` and the `<=>` operator only in a `JOIN ON` section. Outside
that section, both fail on version 25.8 with server error Code 48 `NOT_IMPLEMENTED`
("Function isNotDistinctFrom can be used only in the JOIN ON section").

The scalar helpers therefore use this predicate instead:
`((a is null and b is null) or (a is not null and b is not null and a = b))`.
`is_distinct_from` is its `not (...)` complement. This form does not replace values with a sentinel:
`NULL` never compares equal to a real value, including `0`. The scalar result already has type `UInt8`.
See
[Null-safe comparison](../../scalar-functions/04-conditionals-and-conversion.md#null-safe-comparison-is-not-distinct-from).

## Unsupported functions

The following `ClickHouseFunctions` methods throw `NotSupportedException`
before sending SQL to the provider. nextorm does not silently rewrite them, substitute defaults or
truncate their results.

| Family | Refused member | Tested reason |
|---|---|---|
| Date/time | `from_utc_timestamp` | The vendor result carries no zone in its type while its wall clock is the target-zone time, so the wall-clock/zone pair is not representable in `System.DateTime`. |
| Date/time | `to_time64` | `Time64(scale)` has no CLR carrier: `TimeSpan` maps to `Int64` duration storage and the driver cannot materialise it. |
| Date/time | `from_unix_timestamp64_nano` | `DateTime64(9)` exceeds `DateTime`'s 100 ns tick resolution and would truncate silently. |
| Array | `array_reduce`, `array_reduce_in_ranges` | The aggregate is named by a runtime SQL string, so no typed aggregate contract can close it and the string would reach the server as an opaque fragment. |
| Tuple | `untuple` | It substitutes a dynamic, implementation-named column set into the call location, while nextorm maps one projection expression to exactly one result column. |
| JSON | `pretty_print_json` | Introduced in ClickHouse 26.4; absent from the pinned 25.8.33.6 server (`UNKNOWN_FUNCTION`) and no documented equivalent composition exists. |
| JSON | `json_extract_uint_or_default` | No such function exists; `json_extract_uint` already returns the vendor default `0` for a missing path/wrong type/malformed JSON and `NULL` for a `NULL` input. |
| Hash | `ngram_sim_hash_arg`, `word_shingle_sim_hash_arg` | No such functions: only the `MinHashArg` forms return arguments; absent from `system.functions` on 25.8.33.6 and from the vendor reference. |
| Type conversion | `to_int128`, `to_int256`, `to_uint128`, `to_uint256`, `reinterpret_as_int128`, `reinterpret_as_int256`, `reinterpret_as_uint128`, `reinterpret_as_uint256` | The driver surfaces the 128/256-bit types as `System.Numerics.BigInteger`; the shared row materialiser has no `BigInteger` accessor, so the value or bit pattern cannot be materialised into a fixed-width CLR integer. |
| Type conversion | `to_decimal128`, `to_decimal256` | 38/76 significant digits versus `System.Decimal`'s 96-bit mantissa (28–29 digits): a value above that range would lose precision silently or throw `OverflowException`. |
| Type conversion | the eleven `to_interval_*` units | The ClickHouse-only `Interval*` result types have no CLR representation and the driver itself cannot materialise them (`Unknown type: Interval*` on 25.8.33.6). |
| Aggregate | `stochastic_linear_regression`, `stochastic_logistic_regression`, `exponential_moving_average` | The aggregate returns an opaque binary `AggregateFunction` state with no typed CLR result; only the `-Merge` combinator yields the value and it needs an `AggregateFunction`-typed column the provider cannot read. |
| Dictionary | `dict_get_keys`, `dict_get_root` | Introduced in 25.12 and 26.7 respectively; absent from the pinned server. Use `dict_get_hierarchy` (last element for the root). |
| Dictionary | `region_to_parent`, `region_to_parents` | No such functions in 25.8.33.6; the documented composition is `region_hierarchy` without the first element. |

## See also

* [ClickHouse provider](../../connecting-providers/clickhouse.md)
* [Provider-specific SQL](overview.md)
* [Limitations and out-of-scope features](../../advanced/limitations.md)

---

Source: `src/nextorm.clickhouse/ClickHouseDialect.cs`, `src/nextorm.core/Query/SqlFunctions/SqlFunctions.ClickHouse.cs`,
`tests/nextorm.clickhouse.tests/SqlGeneration/SqlGenerationTests.cs`.
