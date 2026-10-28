# Provider mapping table

| Feature | SQLite | SQL Server | PostgreSQL |
|---|---|---|---|
| `ToUpper` / `ToLower` | `upper` / `lower` | `upper` / `lower` | `upper` / `lower` |
| `Length` | `length` | `len` | `length` |
| `Substring` | `substring`, 1-based | `substring`, 1-based | `substring`, 1-based |
| `Trim` / `TrimStart` / `TrimEnd` | `trim` / `ltrim` / `rtrim` | `trim` / `ltrim` / `rtrim` | `trim` / `ltrim` / `rtrim` |
| `Replace` | `replace` | `replace` | `replace` |
| `Contains` / `StartsWith` / `EndsWith` / `like` | `like` (escape `\`) | `like` (escape `\`) | `like` (escape `\`) |
| `string.IsNullOrEmpty` | `(x is null or x = '')` | `(x is null or x = '')` | `(x is null or x = '')` |
| `string.Format` / `ToString(format)` | `printf` / `strftime` | `format` | `to_char` |
| Ordinal `Equals` / `CompareOrdinal` | `collate binary` | `collate Latin1_General_100_BIN2` | `collate "C"` |
| `collate(s, name)` | `s collate name` | `s collate name` | `s collate "name"` |
| `[Collation]` column | `s collate name` | `s collate name` | `s collate "name"` |
| Regex (`IsMatch` / `Replace`) | `s regexp ...` / `regexp_replace(...)` | `regexp_like(...)` / `regexp_replace(...)` (2025+) | `s ~ ...` / `regexp_replace(...)` |
| `Abs` | `abs` | `abs` | `abs` |
| `Round` | `round(x)` | `round(x, 0)` | `round(x)` |
| `Truncate` | `trunc` | `round(x, 0, 1)` | `trunc` |
| `Log` (natural) | `ln` | `log` | `ln` |
| `Now` / `UtcNow` | `datetime('now')` / `datetime('now')` | `getdate()` / `getutcdate()` | `now()` / `now() at time zone 'utc'` |
| `Year` / `Month` / `Day` / `Hour` | `cast(strftime('%Y'...`/`'%m'`/`'%d'`/`'%H'` `as integer)` | `datepart(year, ...)` etc. | `extract(year from ...)` etc. |
| `??` coalesce | `ifnull(a, b)` | `isnull(a, b)` | `coalesce(a, b)` |
| String concatenation (`+`) | `\|\|` | `+` | `\|\|` |
| Boolean predicate as a value | unchanged | `cast(case when ... then 1 else 0 end as bit)` | unchanged |
| Arrays (`any`/`all`, array functions) | `NotSupportedException` | `NotSupportedException` | `any(@array)`, `cardinality(...)`, ... |
| JSON/JSONB (`json_agg`, `->`, ...) | `NotSupportedException` | `NotSupportedException` | supported |
| Text JSON (`json_value`, `json_query`, `json_modify`, `isjson`) | `NotSupportedException` | `json_value(...)`, ..., `isjson(...)` | `NotSupportedException` |
| `nullif` | supported | supported | supported |
| `greatest` / `least` | `max(...)` / `min(...)` (single argument -> `(...)`) | supported (2022+) | supported |
| `iif` | `iif(cond, a, b)` (3.32+) | `iif(cond, a, b)` | `case when cond then a else b end` |
| `multi_if` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` (ClickHouse-only; `multiIf`) |
| `date_trunc` | `NotSupportedException` | `datetrunc(...)` (2022+) | supported |
| `date_add` / `end_of_month` / `date_diff` / `date_from_parts` | `datetime(x, n \|\| ' days')` / `date(x, 'start of month', ...)` / `strftime` difference / `date(printf(...))` | `dateadd(...)` / `eomonth(...)` / `datediff(...)` / `datefromparts(...)` | interval arithmetic / `date_trunc` / date-part difference / `make_date` |
| `string_agg` / `array_agg` | `group_concat(x, delimiter)` (no `array_agg`) | `string_agg` (2017+); `array_agg` throws | supported |
| Aggregate `filter (where ...)` | `filter (where ...)` | `NotSupportedException` | `filter (where ...)` |
| Extended scalar library (`asin`, `split_part`, `regexp_*`, `to_char`, ...) | `NotSupportedException` | `NotSupportedException` | supported |
| Boolean/bitwise/statistical aggregates | `NotSupportedException` | `NotSupportedException` | supported |
| Ordered-set aggregates (`percentile_cont`, ...) | `NotSupportedException` | window `percentile_cont(f) within group (order by x) over (...)` | `within group (order by ...)` |
| Median | `NotSupportedException` | `percentile_cont(0.5) within group (order by x) over (...)` (analytic: one row per input row, partition-only `OVER`) | `percentile_cont(0.5) within group (order by x)` (aggregate: one row per group; floating-point sort only) |
| Generic `percentile(fraction, value)` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` (never an alias of the continuous form) |
| JSONPath (`jsonb_path_*`) | `NotSupportedException` | `NotSupportedException` | `cast(path as jsonpath)` |
| Built-in table functions | `NotSupportedException` | `string_split(...)`, `openjson(...)`, `regexp_split_to_table(...)` (2025+) | `generate_series(...)`, `unnest(...)` |

The in-memory provider does not render SQL: it compiles and evaluates the expression against in-memory
rows, so the .NET method itself runs. The SQL matrix above applies to the SQLite, SQL Server and
PostgreSQL providers.

ClickHouse renders `dateTrunc('part', x)`, `addDays`/`addMonths`/.../`addSeconds` (and a scaled
`addYears` over a `DateTime64(6)` operand for `year`/`decade`/`century`, whose SQL result type is
`DateTime64(6)` while the CLR API is unchanged; `millennium` throws), `toLastDayOfMonth(x)`,
`arrayStringConcat(groupArray(x), delimiter)`, `groupBitAnd`/`groupBitOr`/`groupBitXor`,
`covarPop`/`covarSamp`, `argMin`/`argMax`, the `-If` combinators and `multiIf`. It rejects the ANSI
`filter (where ...)` clause and the `regr_*`/boolean aggregates with `NotSupportedException`; see the
[ClickHouse provider](../connecting-providers/clickhouse.md).

## SQL Server version-gated functions (2022/2025)

These members live on [`SqlFunctions.SqlServer`](../guide/provider-specific-sql/sqlserver.md#version-gated-functions-2022-and-2025)
and are gated by the **declared major version** alone: an engine-edition declaration never substitutes for
a version, and an unversioned context refuses every one of them before SQL with the required and the
declared major version in the message.

| Member | SQL | Result | Requires |
|---|---|---|---|
| `regexp_count(input, pattern)` | `regexp_count(input, pattern)` | `int?` — `0` for no match, `NULL` for a NULL input | major version 17 |
| `regexp_substr(input, pattern[, start[, occurrence[, match_type]]])` | `regexp_substr(input, pattern[, …])` | `string?` — `NULL` for no match or a NULL operand; the match type renders as `cast(… as varchar(4000))`, because the native argument rejects `nvarchar` (Msg 8116) | major version 17; no compatibility-level gate |
| `regexp_instr(input, pattern[, start[, occurrence[, return_option[, match_type]]]])` | `regexp_instr(input, pattern[, …])` | `int?` — `0` for no match, `NULL` for a NULL operand; a supplied NULL return option behaves like `0` | major version 17; no compatibility-level gate |
| `regexp_matches(input, pattern[, match_type])` | a `FROM` source, e.g. `from regexp_matches(input, pattern)` | one row per non-overlapping match: `match_id` (`bigint`), `start_position`/`end_position` (`int`), `match_value`, `substring_matches` (the full JSON capture text); no match → zero rows | major version 17 **and** database compatibility level 170 |
| `edit_distance(input, pattern[, maximum_distance])` | `edit_distance(input, pattern[, maximum_distance])` | `int?`; the capped form returns `maximum_distance + 1` when the true distance exceeds it, a negative cap is uncapped | major 17 + compatibility 170 + `PREVIEW_FEATURES = ON` + Windows collation |
| `edit_distance_similarity(input, pattern)` | `edit_distance_similarity(input, pattern)` | `int?` in `0..100` | the same fuzzy prerequisites |
| `jaro_winkler_distance(input, pattern)` | `jaro_winkler_distance(input, pattern)` | `double?` (`float(53)`) | the same fuzzy prerequisites |
| `jaro_winkler_similarity(input, pattern)` | `jaro_winkler_similarity(input, pattern)` | `int?` in `0..100` | the same fuzzy prerequisites |
| `left_shift(value, shift)` / `right_shift(value, shift)` | `CASE WHEN value IS NULL OR shift IS NULL THEN CAST(NULL AS <operand type>) ELSE left_shift(cast(value as <operand type>), cast(shift as bigint)) END` | the operand's own type (`byte?`/`short?`/`int?`/`long?`); logical zero-fill shift; typed-NULL guard (a bare `left_shift(NULL, …)` is Msg 8116) | major version 16 |
| `bit_count(value)` | `CASE WHEN value IS NULL THEN CAST(NULL AS bigint) ELSE bit_count(cast(value as <operand type>)) END` | `long?` (`bigint`) | major version 16 |
| `get_bit(value, offset)` | `CASE WHEN value IS NULL OR offset IS NULL THEN CAST(NULL AS bit) ELSE get_bit(cast(value as <operand type>), cast(offset as int)) END` | `bool?` (`bit`) | major version 16 |
| `set_bit(value, offset[, bitValue])` | `… ELSE set_bit(cast(value as <operand type>), cast(offset as int)[, cast(bitValue as bit)]) END` | the operand's own type; an explicitly NULL `bitValue` yields `NULL`, an omitted one keeps the native default 1 | major version 16 |
| `product(value)` | `product(value)` (aggregate context; no `OVER`) | the input type; NULL rows are ignored, an all-NULL group is `NULL` | major version 17 |
| `regexp_split_to_table(input, pattern)` | a `FROM` source, e.g. `from regexp_split_to_table(input, pattern)` | one row per fragment: `value` plus the one-based `ordinal` (`bigint`) | major version 17 **and** database compatibility level 170 |

`regexp_split_to_table` returns
[`TvfEntityBuilder<SqlFunctions.ISqlServerRegexpSplitToTableRow>`](xref:NextORM.Core.SqlFunctions.ISqlServerRegexpSplitToTableRow)
and needs no renderer hook: the member, the row shape and the `SupportsTableFunction` registration are the
whole route. At **compatibility level 160** on a major-17 engine the function does not exist (Msg 208), so
the 170 prerequisite is a real gate that nextorm documents and asserts live rather than silently assuming
(the dialect cannot inspect the database compatibility level).

The regex scalars float above the compatibility level: they execute at 160 and at 170 on a major-17
engine. `regexp_matches` is the exception among them — at compatibility 160 the engine reports it missing
(Msg 208), so its 170 prerequisite is the same kind of documented, live-asserted runtime gate. The four
fuzzy names need **all** of compatibility 170, `PREVIEW_FEATURES = ON` and a Windows collation; on the
tested build `17.0.5005.3` a `SQL_*` collation is Msg 9847 and a missing preview flag is Msg 195. The
declared-major-17 flag promises the version gate only — those runtime prerequisites are environment
facts, asserted live, and no promise is made for future builds.

## SQL Server TRY_CAST and TRY_CONVERT

`SqlFunctions.SqlServer.try_cast<T>(value)` and `try_convert<T>(value[, style])` render the T-SQL
`TRY_CAST`/`TRY_CONVERT` through a **closed CLR→SQL destination map**; a malformed, overflowing or
out-of-range value is SQL NULL instead of an error. Unlike the names above, they are not version-gated:
every SQL Server declaration (15, 16, 17 and unversioned) renders them, and every other provider rejects
the member with `The TRY_CAST function is not supported by this provider.`

| Member | SQL | Result |
|---|---|---|
| `try_cast<T>(value)` | `try_cast(value as <type>)` | `T?` — NULL when the conversion is invalid |
| `try_convert<T>(value)` / `try_convert<T>(value, style)` | `try_convert(<type>, value[, style])` | `T?` — NULL when the conversion is invalid |
| `try_cast(value)` / `try_convert(value[, style])` | `try_cast(value as nvarchar(4000))` / `try_convert(nvarchar(4000), value[, style])` | `string?` — input longer than 4000 characters is truncated to 4000 |

| CLR target `T` | SQL Server type |
|---|---|
| `byte` / `short` / `int` / `long` | `tinyint` / `smallint` / `int` / `bigint` |
| `float` / `double` | `real` / `float(53)` |
| `decimal` | `decimal(38,10)` |
| `bool` | `bit` |
| `Guid` | `uniqueidentifier` |
| `DateTime` / `DateTimeOffset` | `datetime2(7)` / `datetimeoffset(7)` |
| `TimeSpan` | `time(7)` |

An unmapped target is refused **before** the command is prepared (`The TRY_CONVERT function cannot map CLR
target 'MyType' to a supported SQL Server conversion type.`). The style is the native
`datetime`/`float`/money style: omitting it and passing a NULL style are separate overloads, and the NULL
style makes the engine return NULL even for a convertible value. A conversion the engine itself prohibits is
not masked — `TRY_CAST(1 AS xml)` raises Msg 529, and `xml` is not a public target. See
[conditionals and conversion](04-conditionals-and-conversion.md#sql-server-try_cast-and-try_convert).

## MySQL-specific functions

The names on [`SqlFunctions.MySql`](../guide/provider-specific-sql/mysql.md) render only on the MySQL
provider; PostgreSQL, SQL Server, SQLite, ClickHouse and the in-memory provider reject them with
`NotSupportedException`. Every name below is **verified against MySQL 8.4 only**: MariaDB refuses the
names added by this slice until its own verification ([#284](https://github.com/AlexeyShirshov/nextorm/issues/284)),
while the MariaDB-only names keep their separate section below.

The catalogue holds 86 newly delivered members plus the two names that were already covered
(`json_storage_free`, delivered in an earlier slice, and the cross-provider `connection_id`); the three
refused names are listed in [MySQL refusals](#mysql-refusals).

| Family | Member | MySQL SQL | Notes |
|---|---|---|---|
| String | `bin(value)` | `BIN(value)` | binary form of an unsigned 64-bit value, without a `0b` prefix |
| String | `export_set(bits, on, off[, separator[, count]])` | `EXPORT_SET(...)` | bit string read from the least significant bit; a constant `count` outside `[0, 64]` is refused |
| String | `hex(value)` | `HEX(value)` | hexadecimal form of binary data, a string's bytes or a 64-bit integer |
| String | `make_set(bits, values...)` | `MAKE_SET(...)` | comma-separated values whose 1-based position matches a set bit |
| String | `oct(value)` | `OCT(value)` | octal form of an unsigned 64-bit value |
| String | `ord(value)` | `ORD(value)` | code of the leftmost character, `0` for an empty string |
| String | `quote(value)` | `QUOTE(value)` | single-quoted SQL literal with quotes and backslashes escaped; the bare word `NULL` for SQL NULL |
| String | `soundex(value)` | `SOUNDEX(value)` | four-character Soundex code, empty when the value has no letters |
| String | `strcmp(left, right)` | `STRCMP(...)` | `-1`/`0`/`1` by collation order, `null` when an argument is SQL NULL |
| String | `to_base64(value)` | `TO_BASE64(...)` | Base64 of binary data, wrapped at 76 characters per line |
| String | `from_base64(text)` | `FROM_BASE64(...)` | `null` when the text is not valid Base64 |
| String | `unhex(text)` | `UNHEX(...)` | `null` for an odd length or a non-hexadecimal character |
| String | `weight_string(value[, cast, length])` | `WEIGHT_STRING(...)` | only the `AS CHAR(N)`/`AS BINARY(N)` modifiers, both compile-time constants |
| Numeric | `conv(value, fromBase, toBase)` | `CONV(...)` | target bases `2`–`36`, `-1` for a signed interpretation |
| Numeric | `crc32(value)` | `CRC32(...)` | unsigned 32-bit CRC-32 checksum widened to `long` |
| Numeric | `log2(value)` | `LOG2(...)` | base-2 logarithm |
| Numeric | `rand([seed])` | `RAND([seed])` | `double` in `[0, 1)`; a seed repeats the sequence |
| Numeric | `truncate(value, places)` | `TRUNCATE(...)` | truncation toward zero; a negative count zeroes the integer part |
| Bit | `bit_count(value)` | `BIT_COUNT(...)` | set bits in the 64-bit representation of the value |
| Date/time | `adddate(date, days)` | `ADDDATE(...)` | numeric-days form only; the `INTERVAL` unit form is not modelled |
| Date/time | `addtime(datetime, time)` | `ADDTIME(...)` | adds a duration |
| Date/time | `convert_tz(datetime, from, to)` | `CONVERT_TZ(...)` | IANA zones; `null` when the server does not know a zone |
| Date/time | `dayname(date)` | `DAYNAME(...)` | weekday name, for example `Wednesday` |
| Date/time | `from_days(days)` | `FROM_DAYS(...)` | date of a day number counted from year 0 |
| Date/time | `get_format(kind, format)` | `GET_FORMAT(...)` | typed `MySqlFormatKind` plus a literal format name (`USA`, `JIS`, `ISO`, `EUR`, `INTERNAL`) |
| Date/time | `makedate(year, dayOfYear)` | `MAKEDATE(...)` | `null` when the day falls outside the year |
| Date/time | `maketime(hour, minute, second)` | `MAKETIME(...)` | `null` when a part is out of range |
| Date/time | `microsecond(datetime)` | `MICROSECOND(...)` | microseconds part, `0`–`999999` |
| Date/time | `monthname(date)` | `MONTHNAME(...)` | month name, for example `May` |
| Date/time | `period_add(period, months)` | `PERIOD_ADD(...)` | `YYMM`/`YYYYMM` period shifted by a month count |
| Date/time | `period_diff(period1, period2)` | `PERIOD_DIFF(...)` | months between two `YYMM`/`YYYYMM` periods |
| Date/time | `sec_to_time(seconds)` | `SEC_TO_TIME(...)` | duration; a fractional input keeps its fraction |
| Date/time | `subdate(date, days)` | `SUBDATE(...)` | numeric-days form only |
| Date/time | `subtime(datetime, time)` | `SUBTIME(...)` | subtracts a duration |
| Date/time | `time(datetime)` | `TIME(...)` | time-of-day part |
| Date/time | `time_format(time, format)` | `TIME_FORMAT(...)` | `%`-templated duration formatting |
| Date/time | `time_to_sec(time)` | `TIME_TO_SEC(...)` | whole seconds: MySQL 8.4 discards any sub-second component |
| Date/time | `timediff(datetime1, datetime2)` | `TIMEDIFF(...)` | duration between two values |
| Date/time | `timestamp(datetime)` | `TIMESTAMP(...)` | the value as `DATETIME` |
| Date/time | `to_days(date)` | `TO_DAYS(...)` | day number counted from year 0 |
| Date/time | `to_seconds(date)` | `TO_SECONDS(...)` | second number counted from year 0 |
| Date/time | `yearweek(date[, mode])` | `YEARWEEK(...)` | year and week under the default or an explicit week mode |
| JSON | `json_array(values...)` | `JSON_ARRAY(...)` | each argument is a SQL value, so strings become JSON strings |
| JSON | `json_arrayagg(value)` | `JSON_ARRAYAGG(...)` | aggregates the group's values into one JSON array |
| JSON | `json_arrow(column, path)` | `column -> 'path'` | requires a mapped JSON column and a compile-time path literal |
| JSON | `json_arrow_text(column, path)` | `column ->> 'path'` | unquoted form of `->`; same column and literal restriction |
| JSON | `json_contains(target, candidate[, path])` | `JSON_CONTAINS(...)` | `1` when the candidate is contained in the target |
| JSON | `json_contains_path(json, oneOrAll, paths...)` | `JSON_CONTAINS_PATH(...)` | the mode must be the literal `'one'` or `'all'` and every path a literal |
| JSON | `json_merge(first, second[, rest...])` | `JSON_MERGE(...)` | duplicate-preserving merge, deprecated by MySQL in favour of the patch/preserve pair |
| JSON | `json_object(keyValues...)` | `JSON_OBJECT(...)` | alternating key/value arguments; the count must be even and the keys strings |
| JSON | `json_objectagg(key, value)` | `JSON_OBJECTAGG(...)` | aggregates key/value pairs into one JSON object |
| JSON | `json_overlaps(left, right)` | `JSON_OVERLAPS(...)` | `1` when the documents share at least one value |
| JSON | `json_pretty(json)` | `JSON_PRETTY(...)` | re-indents the document for readability |
| JSON | `json_quote(value)` | `JSON_QUOTE(...)` | the value as a JSON string literal |
| JSON | `json_schema_valid(schema, document)` | `JSON_SCHEMA_VALID(...)` | `1` when the document validates against the schema |
| JSON | `json_schema_validation_report(schema, document)` | `JSON_SCHEMA_VALIDATION_REPORT(...)` | JSON report of the validation |
| JSON | `json_search(json, oneOrAll, search[, escape[, paths...]])` | `JSON_SEARCH(...)` | the mode must be `'one'` or `'all'`; the escape must be a single character |
| JSON | `json_storage_size(json)` | `JSON_STORAGE_SIZE(...)` | bytes used by the stored representation |
| JSON | `json_storage_free(json)` | `JSON_STORAGE_FREE(...)` | already delivered in an earlier slice: bytes freed in a binary `JSON` column value by a partial in-place update, `0` when nothing was freed, `null` for SQL NULL — see the [MySQL guide](../guide/provider-specific-sql/mysql.md#native-functions) |
| JSON | `member_of(value, jsonArray)` | `value MEMBER OF (jsonArray)` | the array argument must be a compile-time JSON array literal |
| Aggregate | `bit_and(value)` | `BIT_AND(value)` | bitwise AND of every non-null value in the group, `null` for an empty group |
| Aggregate | `bit_or(value)` | `BIT_OR(value)` | bitwise OR of every non-null value in the group |
| Aggregate | `bit_xor(value)` | `BIT_XOR(value)` | bitwise XOR of every non-null value in the group |
| Encryption/compression/hash | `aes_encrypt(data, key[, iv, kdf, salt, iterations])` | `AES_ENCRYPT(...)` | MySQL 8.4 defaults to `aes-128-ecb` |
| Encryption/compression/hash | `aes_decrypt(data, key[, iv, kdf, salt, iterations])` | `AES_DECRYPT(...)` | `null` for invalid input or padding |
| Encryption/compression/hash | `compress(value)` | `COMPRESS(...)` | zlib with an uncompressed-length prefix |
| Encryption/compression/hash | `uncompress(value)` | `UNCOMPRESS(...)` | `null` when the input is not a compressed value |
| Encryption/compression/hash | `uncompressed_length(value)` | `UNCOMPRESSED_LENGTH(...)` | length recorded in the `COMPRESS` header |
| Encryption/compression/hash | `random_bytes(length)` | `RANDOM_BYTES(...)` | cryptographically random bytes |
| Encryption/compression/hash | `validate_password_strength(password)` | `VALIDATE_PASSWORD_STRENGTH(...)` | grade depends on the server's password-validation configuration |
| Encryption/compression/hash | `statement_digest(statement)` | `STATEMENT_DIGEST(...)` | SHA-256 digest of the normalised statement text |
| Encryption/compression/hash | `statement_digest_text(statement)` | `STATEMENT_DIGEST_TEXT(...)` | normalised form of the statement text |
| Information | `connection_id()` | — | already covered by the cross-provider `CommonFunctions.connection_id()`, rendered as `CAST(CONNECTION_ID() AS UNSIGNED)`; see [Session and server information](09-system-functions.md#session-and-server-information-cross-provider) |
| Information | `found_rows()` | `FOUND_ROWS()` | row count the last `SELECT` would have returned without its `LIMIT` |
| Information | `last_insert_id([value])` | `LAST_INSERT_ID(...)` | first automatically generated value of the last insert on this connection |
| Information | `row_count()` | `ROW_COUNT()` | rows affected by the last statement on this connection |
| Information | `current_role()` | `CURRENT_ROLE()` | active roles, `NONE` when no role is active |
| Information | `charset(expression)` | `CHARSET(...)` | character set of the expression |
| Information | `collation(expression)` | `COLLATION(...)` | collation of the expression |
| Information | `coercibility(expression)` | `COERCIBILITY(...)` | coercibility value used to resolve collation conflicts |
| Information | `benchmark(count, expression)` | `BENCHMARK(...)` | evaluates the expression `count` times for its side effects and returns `0` |
| Information | `icu_version()` | `ICU_VERSION()` | ICU version the server was built with, empty when built without ICU |
| Miscellaneous | `uuid_short()` | `UUID_SHORT()` | 64-bit identifier unique to the server instance |
| Miscellaneous | `is_uuid(value)` | `IS_UUID(...)` | `1` for a syntactically valid string UUID, `null` for SQL NULL |
| Miscellaneous | `sleep(seconds)` | `SLEEP(...)` | pauses the statement, returns `0`, or `1` when interrupted |
| Miscellaneous | `grouping(expressions...)` | `GROUPING(...)` | rollup bitmap; every argument must match a `GROUP BY` expression, which is a caller obligation |
| Miscellaneous | `name_const(name, value)` | `NAME_CONST(...)` | returns the value under a column label given as a compile-time literal |
| Miscellaneous | `column_default(column)` | `DEFAULT(column)` | default value of a named column; the argument must be a column reference |

### MySQL behaviour limits

These shapes are validated before any SQL is emitted; a call that does not match is refused with
`NotSupportedException` rather than rendered with different semantics:

* `column_default` requires a mappable column reference — constants, parameters, casts and computed
  expressions are refused.
* `->` and `->>` require a JSON column on the left and a compile-time path literal on the right.
* `json_object` requires an even argument count with string keys.
* `json_contains_path` and `json_search` require the literal `'one'` or `'all'` and literal paths.
* `json_search` takes a single-character escape.
* `get_format` takes a typed kind (`MySqlFormatKind`) plus a literal format name.
* `weight_string` supports only the `AS CHAR(N)`/`AS BINARY(N)` modifiers, both compile-time constants.
* `time_to_sec` returns whole seconds: MySQL 8.4 discards the argument's sub-second precision.
* `name_const` requires a compile-time literal name; `member_of` requires a literal JSON array.

### MySQL refusals

Three vendor names are deliberately refused: the member exists only to carry the refusal, throws
`NotSupportedException` before any SQL is emitted, and is covered by a test with a positive sibling
control on the same provider. The exact messages are listed in the
[MySQL guide](../guide/provider-specific-sql/mysql.md#guards-and-refusals).

| Refused name | Reason | Reachable through |
|---|---|---|
| `uuid` | `UUID()` generates a version 1 value; this API's UUID contract is v4 (`gen_random_uuid`) or v7 (`uuidv7`), and a v1 value is never substituted | `uuidv7()`, or `UUID()` in raw SQL when v1 is genuinely intended |
| `values` | `VALUES(column)` has no scalar-expression contract — it names the new row only inside the upsert mutation builder — and is deprecated in MySQL 8.4 | the mutation builder's upsert API |
| `json_table` | a table-producing construct: needs a `FROM`/join position and a `COLUMNS` definition list, so it is not a scalar function | raw SQL, or a `[SqlFunction]` wrapper |

## MariaDB-only JSON functions

`json_equals` and `json_normalize` on
[`SqlFunctions.MySql`](../guide/provider-specific-sql/mysql.md#mariadb) render only on MariaDB
(verified on the 11.4 baseline); MySQL, PostgreSQL, SQL Server, SQLite, ClickHouse and the in-memory
provider reject both with `NotSupportedException`.

| Member | SQL | Result | Availability |
|---|---|---|---|
| `json_equals(json1, json2)` | `json_equals(...)` | `int?` — `1` for equal JSON documents (object key order does not matter), `0` when they differ, `null` for SQL NULL | [MariaDB only](../guide/provider-specific-sql/mysql.md#mariadb); MySQL and other providers reject |
| `json_normalize(json)` | `json_normalize(...)` | `string?` — canonical JSON text with sorted object keys and no unnecessary whitespace (idempotent), `null` for SQL NULL | [MariaDB only](../guide/provider-specific-sql/mysql.md#mariadb); MySQL and other providers reject |

## Explicitly unsupported

These throw `NotSupportedException` rather than emitting SQL with different semantics:

* `string.IsNullOrWhiteSpace(x)` - throws with a message mentioning `IsNullOrWhiteSpace`
  (`SqlGenerationTests.IsNullOrWhiteSpace_ShouldThrowClearException`,
  `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:974`).
* `Math.Log(value, base)` - the two-argument form has a provider-specific argument order, so it is left
  unsupported (`SqlGenerationTests.MathLogWithBase_ShouldThrowClearException`,
  `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:985`). The PostgreSQL two-argument form is available
  as `SqlFunctions.Postgres.log(base, x)` in the extended scalar library instead. PostgreSQL's only
  two-argument overload is `log(numeric, numeric)`, so it renders
  `log((base)::numeric, (x)::numeric)` with both operands cast; one-argument `Math.Log` keeps
  rendering `ln(...)`.
* `Math.Round` overloads that take a `MidpointRounding` (more than two arguments) - not portable.
* `string.Substring(Range)` - no SQL equivalent.
* `string.Trim(c)`/`TrimStart(c)`/`TrimEnd(c)` - SQL trims whitespace only, not an arbitrary character
  set.
* `string.ToUpper(CultureInfo)`/`ToLower(CultureInfo)` with a culture other than
  `CultureInfo.InvariantCulture` - only the invariant upper/lower has a portable form.
* `string.Compare(a, b)` and `string.Compare(a, b, bool)` without a `StringComparison` - culture-sensitive;
  use `string.Compare(a, b, StringComparison.Ordinal)` or `CompareOrdinal`.
* `StringComparison.InvariantCulture`/`CurrentCulture` (with or without `IgnoreCase`) - no portable form.
* `string.Format`/`ToString(format)` specifiers outside the documented subset in
  [Formatting dates and numbers to strings](03-date-and-time.md#formatting-dates-and-numbers-to-strings) - never dropped.
* `Regex` on SQL Server 2022 and earlier - the `REGEXP_*` functions are SQL Server 2025-only (`REGEXP_LIKE`
  additionally needs database compatibility level 170); on 2025+ the translation works, and
  `SqlFunctions.Sql.like` remains the fallback for simple patterns (see
  [Regular expressions](01-string-functions.md#regular-expressions)).
* A `Regex` pattern, replacement or `RegexOptions` that is not a compile-time constant, a `RegexOptions`
  other than `IgnoreCase` (with `Compiled`/`CultureInvariant` as no-ops), and the `Regex` members other
  than `IsMatch`/`Replace` (for example `Match`, `Split`, capture groups).
