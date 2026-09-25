# Built-in function coverage: per-provider gap analysis

This document lists, for every supported provider, the **built-in SQL functions that have no
implementation** in nextorm's query-building surface. It complements
[`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md) (which tracks query
*constructs*) and [`capability-matrix.md`](../comparison/capability-matrix.md) (which tracks the
provider-vs-provider matrix): here the unit is the individual function.

## Scope and method

* **Supported providers:** SQLite, SQL Server, PostgreSQL, MySQL, MariaDB (`nextorm.mariadb`, built on
  MySQL) and ClickHouse. The in-memory provider renders no SQL and is excluded.
* **Canonical function list:** taken from the vendor documentation (see [Sources](#sources)); the list
  per provider is the documented built-in set, grouped the way the vendor groups it.
* **Implemented surface:** `SqlFunctions.Sql` (`CommonFunctions`), `SqlFunctions.Postgres`,
  `SqlFunctions.SqlServer`, `SqlFunctions.ClickHouse`, the SQLite custom aggregates, plus the built-in
  CLR-member translation (`StringFunctionTranslator`, `MathFunctionTranslator`,
  `DateTimeFunctionTranslator`, `ScalarFunctionTranslator`, ...). See
  [Scalar functions](../../scalar-functions/index.md) for the documented surface.
* **Definition of a gap:** a function is a gap when **neither** a `SqlFunctions.*` member **nor** a
  built-in CLR-member translation emits it. A gap can still be reached with a `[SqlFunction]` UDF or a
  raw fragment; that escape hatch is deliberately **not** counted as coverage.
* nextorm is a query builder with a *curated* function catalog, not an exhaustive mirror of every
  vendor function. Niche families (administration, metadata, permissions, encryption, spatial,
  network, Lua/exec) are marked **out of scope** and are not proposed for implementation.

### How to read the lists

* Each provider section lists **missing** functions grouped by the vendor's own categories.
* `≈` marks a case where a portable CLR member or a `SqlFunctions.Sql` helper already covers the common
  use (for example `LEFT`/`RIGHT` ≈ `string.Substring`, `CHARINDEX`/`INSTR` ≈ `string.IndexOf`).
  These are listed for completeness but are low value.
* `out of scope` marks a family nextorm intentionally does not model.
* Nothing here is a commitment; the actionable subset is the [summary table](#summary-actionable-set-all-shipped).

### Audit status

The per-provider lists below are a **vendor-catalogue inventory**, not a line-by-line verification
against the code. On 2026-09-25 every *Missing* entry was checked against the implemented surfaces —
`SqlFunctions.Sql` (`CommonFunctions`), the per-provider `SqlFunctions.*`, and the built-in CLR
translations (`Math.*`, `string.*`, `DateTime.*`, `Regex.*`, plus the window and aggregate
translators) — and the entries reachable through them are marked `≈`. An entry **without** a marker is
not exposed by the query surface; it may still be reachable through a `[SqlFunction]` UDF or a raw
fragment, which is deliberately not counted as coverage. Re-verify against the vendor page before
acting on any single entry.

## Summary: actionable set (all shipped)

Every row below has shipped; the per-provider sections now list only the **still-missing remainder** of
each vendor catalogue (in scope) plus the out-of-scope families. The design-RFC work plans were folded
into the guides and deleted.

| Provider | Shipped surface | Docs |
|---|---|---|
| **Cross-provider** | `SqlFunctions.Sql` string library (`left`, `right`, `lpad`, `rpad`, `repeat`, `reverse`, `space`, `concat_ws`, `translate`, `ascii`, `char`), the length pair (`bit_length`, `octet_length`) and the angle functions (`cot`, `degrees`, `radians`, `pi`); `Math.Acos`/`Asin`/`Atan`/`Atan2`; remainder, base-10 logarithm, power and generic string formatting stay on the portable CLR methods (`%`, `Math.Log10`, `Math.Pow`, `string.Format`) | [Scalar functions](../../scalar-functions/01-string-functions.md#cross-provider-scalar-functions) (gap-analysis §4.36; regex translation shipped earlier, §5.35) |
| **PostgreSQL** | extended scalar + string library, `sha224`/`sha384`/`sha512`, the regexp family (`regexp_substr` included), `age`, `date_bin`, `make_time`/`make_timestamp` (`make_date` via `date_from_parts`), `current_setting`/`set_config`, the sequence functions (`nextval`/`setval`/`currval`/`lastval`), native JSON/JSONB (`json_array`, SQL/JSON `json_exists`/`json_query`/`json_value`), native arrays, `generate_series`/`unnest`, the statistical/boolean/bit aggregates, `num_nulls`/`num_nonnulls`, `mode`, `percentile_cont`/`disc`, `ts_*` full-text | [Scalar functions](../../scalar-functions/01-string-functions.md#string-and-regular-expression-extensions-postgresql) / [JSON and JSONB](../../guide/18-json.md) (gap-analysis §4.37) |
| **SQL Server** | `ISqlServerFunctions` + `SqlServerFunctions` T-SQL scalar library (`PATINDEX`, `QUOTENAME`, `SOUNDEX`, `DIFFERENCE`, `STRING_ESCAPE`, `FORMAT`, `NCHAR`, `UNICODE`, the `ACOS`/`ASIN`/`ATAN`/`ATN2`/`SQUARE` math set, `DATENAME`, `DATE_BUCKET`, `HASHBYTES`, `NEWSEQUENTIALID`, `JSON_ARRAY`/`JSON_OBJECT`/`JSON_ARRAYAGG`/`JSON_OBJECTAGG`/`JSON_CONTAINS`/`JSON_PATH_EXISTS`); `ASCII`/`CHAR`/`TRANSLATE`/`REVERSE`/`SPACE`/`CONCAT_WS` and `COT`/`DEGREES`/`RADIANS`/`PI` via the portable surface | [SQL Server-specific SQL](../../guide/provider-specific/sqlserver.md#t-sql-scalar-functions) (gap-analysis §4.38) |
| **MySQL** | `SqlFunctions.MySql` (`FIND_IN_SET`, `FIELD`, `ELT`, `SUBSTRING_INDEX`, `FORMAT`, `STR_TO_DATE`, `DATE_FORMAT`, `FROM_UNIXTIME`, `UNIX_TIMESTAMP`, `MD5`/`SHA1`/`SHA2`, `INET_ATON`/`INET_NTOA`, the `JSON_*` mutation family, `UUID_TO_BIN`/`BIN_TO_UUID`); `LAST_DAY` via `end_of_month` | [MySQL and MariaDB-specific SQL](../../guide/provider-specific/mysql.md) (gap-analysis §4.39, #80) |
| **MariaDB** | the MySQL surface plus `REGEXP_INSTR`/`REGEXP_REPLACE`/`REGEXP_SUBSTR`, `NVL`/`NVL2`, `ADD_MONTHS`, `MONTHS_BETWEEN`, `TO_CHAR`/`TO_DATE`/`TO_NUMBER`, `KDF`, `XXH3`/`XXH32`, `JSON_DETAILED`/`JSON_COMPACT`, the `NEXT VALUE FOR`/`NEXTVAL`/`SETVAL`/`LASTVAL` sequences | [MySQL and MariaDB-specific SQL](../../guide/provider-specific/mysql.md#mariadb) (gap-analysis §4.40, #79) |
| **SQLite** | `SqlFunctions.Sqlite`: core scalars (`printf`/`format`, `hex`/`unhex`, `random`/`randomblob`, `quote`, `typeof`, `glob`, `unicode`/`char`, `soundex`, `octet_length`, `if`/`ifnull`), the JSON1 family, the date helpers (`timediff`/`unixepoch`/`julianday`) and the math extension | [SQLite-specific SQL](../../guide/provider-specific/sqlite.md) (gap-analysis §4.41) |
| **ClickHouse** | `SqlFunctions.ClickHouse`: UTF-8 case/trim/regexp/search strings, date helpers (`formatDateTime`/`parseDateTime*`, `now`/`today`/`yesterday`), the array/map set operations, the bitmap aggregates (`groupBitmap*`/`sumMap*`), the hash family and `generateULID` | [ClickHouse-specific SQL](../../guide/provider-specific/clickhouse.md) (gap-analysis §4.42, #78) |

---

## Cross-provider (`SqlFunctions.Sql` + CLR-member translation)

**Covered today:** `like` (+escape), `@in`, `any`/`all`, `nullif`, `greatest`, `least`, `iif`,
`contains`, `freetext`, `exists`, `string_agg`, `count`/`count_big`/`count_distinct`/`count_big_distinct`,
`sum`/`avg`/`min`/`max` (+`distinct`, +`filter`), `stdev`/`stdevp`/`var`/`varp`, `corr`, `covar_pop`/`covar_samp`,
`any_agg`, window functions (`row_number`, `rank`, `dense_rank`, `ntile`, `lag`, `lead`, `first_value`,
`last_value`, `nth_value`, `percent_rank`, `cume_dist`, `percentile_cont`/`percentile_disc`, the `*_over`
aggregate forms), `date_trunc`, `date_add`, `date_diff`, `end_of_month`, `date_from_parts`, `extract`,
`date_part`, `current_user`/`session_user`/`current_schema`/`current_database`/`version`,
`gen_random_uuid`/`uuidv7`, `asc`/`desc`.

The portable **CLR translation** covers `string.ToUpper`/`ToLower`/`Trim`/`TrimStart`/`TrimEnd`/
`Substring`/`Length`/`Replace`/`Remove`/`Insert`/`IndexOf`/`LastIndexOf`/`PadLeft`/`PadRight`/
`new string(char,n)`/`Split`/`Join`/`Contains`/`StartsWith`/`EndsWith`/`IsNullOrEmpty`/`+`/`string.Concat`,
`Math.Abs`/`Ceiling`/`Floor`/`Round`/`Truncate`/`Sqrt`/`Pow`/`Exp`/`Log`(natural)/`Sin`/`Cos`/`Tan`/
`Acos`/`Asin`/`Atan`/`Atan2` (SQL Server maps `Atan2` to `atn2`)/`Sign`,
`DateTime.Now`/`UtcNow`/`Year`/`Month`/`Day`/`DayOfYear`/`Hour`/`Minute`/`Second` and `DateTime.AddDays`/
`AddMonths`/…, `??`, numeric casts.

On the providers with `SupportsRegex` (PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse and SQL Server
2025+) `Regex.IsMatch`/`Regex.Replace` with a compile-time-constant pattern translate to the native regex
form (SQL Server renders `REGEXP_LIKE`/`REGEXP_REPLACE`, the match needing a 170 compatibility level).
`string.Format`/`x.ToString(fmt)` translate through the provider's format functions where
`ISqlDialect.StringFormats` is present (all providers here).

**Shipped as portable wrappers** (`SqlFunctions.Sql`, rendered natively where the provider can express
them — see
[Scalar functions](../../scalar-functions/01-string-functions.md#cross-provider-scalar-functions)):
`left`, `right`, `lpad`, `rpad`, `repeat`, `reverse`, `space`, `concat_ws`, `translate`, `ascii`,
`char`, `bit_length`, `octet_length`, `cot`, `degrees`, `radians`, `pi`. `cot` is rejected on ClickHouse
and SQLite, which have no native `cot`. Remainder, base-10 logarithm, power and generic string formatting
are covered by the portable CLR methods (`%`, `Math.Log10`, `Math.Pow`, `string.Format`), which already
translate per provider, so there is no `SqlFunctions.Sql` spelling for them.

**Remaining provider-specific (not portable gaps):**

* **Date:** `date_bin`/`age` exist only on PostgreSQL, `timediff` only on SQLite.
* **Aggregate:** `bit_and`/`bit_or`/`bit_xor`, `bool_and`/`bool_or`/`every` and `mode` are implemented
  on the PostgreSQL provider surface (gated by `SupportsBitAggregates`/`SupportsBooleanAggregates`/
  `SupportsOrderedAggregates`); `any_value` is the portable `any_agg` (enabled on MySQL and ClickHouse;
  MariaDB gates it off and PostgreSQL/SQL Server do not opt in); `stddev`/`variance` are the existing
  `stdev`/`var` spellings.

> One `SqlFunctions.Sql` member lights up several providers; the string wrappers above already shipped
> (see the string/numeric wrappers item 36 in
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md)).

---

## PostgreSQL

**Covered today:** the portable set plus the extended scalar library (`asin`, `acos`, `atan`, `atan2`,
`cbrt`, `sinh`/`cosh`/`tanh`, `asinh`/`acosh`/`atanh`, `degrees`, `radians`, `pi`, `random`, `setseed`,
`log(base,x)`, `gcd`, `lcm`, `factorial`, `width_bucket`), the string library (`split_part`,
`strpos`, `left`/`right`, `lpad`/`rpad`, `repeat`, `reverse`, `initcap`, `translate`, `overlay`,
`concat_ws`, `format`), `md5`/`digest`/`sha256`/`sha224`/`sha384`/`sha512`, the regexp family
(`regexp_replace`, `regexp_like`, `regexp_split_to_array`, `regexp_count`, `regexp_instr`,
`regexp_substr`, `regexp_matches`, `regexp_split_to_table`),
native JSON/JSONB (≈60 functions), native arrays (≈28 functions), `generate_series`/`unnest`, the
statistical/boolean/bit aggregates, `num_nulls`/`num_nonnulls`, `mode`, `percentile_cont`/`disc`,
`regexp_*` and the `ts_*` full-text surface.

**Missing:**

* **String:** `char_length` (≈`Length`), `btrim`, two-argument
  `ltrim`/`rtrim`, `position` (≈`IndexOf`), `starts_with`, `parse_ident`, `quote_ident`,
  `quote_literal`, `quote_nullable`, `to_ascii`, `to_hex`/`to_oct`/`to_bin`, `convert_from`/`convert_to`,
  `encode`/`decode`, `normalize`, `casefold`, `unistr`, `string_to_table`, `regexp_match`.
* **Numeric:** `div`, `erf`, `erfc`, `gamma`/`tgamma`, `lgamma`, `scale`, `min_scale`,
  `trim_scale`, `random_normal`, and the degree variants `acosd`/`asind`/`atand`/`atan2d`/`cosd`/`cotd`/
  `sind`/`tand`.
* **Date/time:** `date_subtract`, `justify_interval`, `make_timestamptz`, `clock_timestamp`,
  `statement_timestamp`, `transaction_timestamp`, `timeofday`, `isfinite`, `pg_sleep`/`pg_sleep_for`/
  `pg_sleep_until`.
* **JSON/JSONB:** `json`/`json_scalar`/`json_serialize`, `json_array_elements(_text)` and
  `json_each(_text)` (non-`b` forms), `json_extract_path(_text)`/`jsonb_extract_path(_text)`,
  `json_populate_record(set)`, `jsonb_populate_record(set)`, `json_to_record(set)`
  (`jsonb_to_record`/`jsonb_to_recordset` are shipped), `jsonb_set_lax`, the `jsonb_path_*_tz` variants,
  the SQL/JSON table form `json_table`, `json_agg_strict`/`jsonb_agg_strict`, `json_object_agg_unique(+strict)`,
  `json_arrayagg`/`json_objectagg`, `jsonb_populate_record_valid`.
* **Array:** `trim_array`, `generate_subscripts`, and the multi-array/`ordinality` forms of `unnest`.
* **Aggregate:** `any_value`, `grouping`, `xmlagg`, `regr_sxx`/`regr_sxy`/`regr_syy`.
* **Full-text:** `array_to_tsvector`, `json_to_tsvector`/`jsonb_to_tsvector`, `get_current_ts_config`,
  `numnode`, `querytree`, `setweight`, `strip`, `ts_delete`, `ts_filter`, `ts_rewrite`, `tsquery_phrase`,
  `tsvector_to_array`, `ts_debug`, `ts_lexize`, `ts_parse`, `ts_token_type`.
* **UUID:** `uuidv4` (≈`gen_random_uuid`), `uuid_extract_timestamp`, `uuid_extract_version`.
* **System info:** `current_role`, `current_catalog`, `current_schemas`, `pg_backend_pid`,
  `pg_postmaster_start_time`, `pg_current_logfile`, and the `pg_*` introspection family.
* **Binary string:** `get_bit`/`set_bit`, `get_byte`/`set_byte`, `bit_count`, `crc32`/`crc32c`,
  `convert_from`/`convert_to`.
* **out of scope:** network (`host`, `masklen`, `broadcast`, `family`, `netmask`, `set_masklen`, `abbrev`,
  `inet_*`, `macaddr8_set7bit`), range/multirange, geometric, enum, `pg_*` metadata/permission helpers,
  XML (`xml*`, `xpath`, `xmltable`, ...), `ts_stat` is present but the debug/parse family is not.

---

## SQL Server

**Covered today:** the portable set plus `json_value`/`json_query`/`json_modify`/`isjson`, `choose`,
`string_split`, `openjson`/`openxml`, `contains`/`freetext` (with `containstable`/`freetexttable`), the
`xml` data-type methods (`xml_value`/`xml_query`/`xml_exist`/`xml_nodes`), and `iif`/`greatest`/`least`.

**Missing:**

* **String:** `STR`; the rest of the T-SQL string set is shipped (`ASCII`/`CHAR` via `SqlFunctions.Sql`,
  `NCHAR`/`UNICODE`/`DIFFERENCE`/`FORMAT`/`PATINDEX`/`QUOTENAME`/`SOUNDEX`/`STRING_ESCAPE` via
  `SqlServerFunctions`, `SPACE`/`TRANSLATE`/`REVERSE`/`CONCAT_WS` via the portable surface) or reachable
  through the CLR string members (`LEN`/`LEFT`/`RIGHT`/`REPLICATE`/`STUFF`/`REPLACE`/`SUBSTRING`/
  `CHARINDEX`/`LOWER`/`UPPER`/`TRIM`/`LTRIM`/`RTRIM`, `≈`).
* **Regular expressions (SQL Server 2025):** `REGEXP_LIKE` and `REGEXP_REPLACE` now back the portable
  `Regex.IsMatch`/`Regex.Replace` translation (SQL Server 2025+; the match needs database compatibility
  level 170), like the other providers. The extraction/tabular remainder — `REGEXP_SUBSTR`,
  `REGEXP_COUNT`, `REGEXP_INSTR`, `REGEXP_MATCHES`, `REGEXP_SPLIT_TO_TABLE` — stays a gap.
* **Fuzzy matching (SQL Server 2025):** `EDIT_DISTANCE`, `EDIT_DISTANCE_SIMILARITY`,
  `JARO_WINKLER_DISTANCE`, `JARO_WINKLER_SIMILARITY`.
* **Numeric:** `RAND`; `ABS`/`CEILING`/`FLOOR`/`ROUND`/`SQRT`/`POWER`/`EXP`/`LOG`/`LOG10`/`SIGN`/
  `SIN`/`COS`/`TAN`/`TRUNC` and `ACOS`/`ASIN`/`ATAN`/`ATAN2` are reachable through `Math.*`;
  `ACOS`/`ASIN`/`ATAN`/`ATN2`/`SQUARE` ship on `SqlServerFunctions`, and `COT`/`DEGREES`/`RADIANS`/`PI`
  come from the portable surface (`SqlFunctions.Sql`).
* **Bit manipulation:** `LEFT_SHIFT`, `RIGHT_SHIFT`, `BIT_COUNT`, `GET_BIT`, `SET_BIT`.
* **Date/time:** `DATETIMEFROMPARTS`, `DATETIME2FROMPARTS`, `DATETIMEOFFSETFROMPARTS`,
  `SMALLDATETIMEFROMPARTS`, `TIMEFROMPARTS`, `SWITCHOFFSET`, `TODATETIMEOFFSET`, `ISDATE`
  (`DATEADD`/`DATEDIFF`/`DATEDIFF_BIG`/`DATEFROMPARTS`/`EOMONTH`/`DATETRUNC`/`DATEPART`/`DATE_BUCKET`/
  `DATENAME`/`YEAR`/`MONTH`/`DAY` are covered).
* **Conversion:** `PARSE`, `TRY_CAST`, `TRY_CONVERT`, `TRY_PARSE`, `BASE64_ENCODE`/`BASE64_DECODE`.
* **Logical:** `ISNUMERIC`.
* **Aggregate:** `ANY_VALUE`, `APPROX_COUNT_DISTINCT`, `CHECKSUM_AGG`, `GROUPING_ID`, `PRODUCT`.
* **Analytic:** `APPROX_PERCENTILE_CONT`, `APPROX_PERCENTILE_DISC`.
* **Cryptographic:** `CRYPT_GEN_RANDOM`; `HASHBYTES` is shipped, and the `ENCRYPTBY*`/`DECRYPTBY*`/
  `SIGNBY*`/`VERIFYSIGNEDBY*`/`CERT*`/`KEY_*` family is **out of scope**.
* **System:** `COMPRESS`/`DECOMPRESS`, `SESSION_CONTEXT`, `SESSION_ID`, `CURRENT_TIMEZONE(_ID)`,
  `XACT_STATE`, `@@ROWCOUNT`/`@@ERROR`/`ERROR_*`, `HOST_NAME`/`HOST_ID` (`NEWSEQUENTIALID` is shipped).
* **out of scope:** security/identity (`SUSER_*`, `HAS_PERMS_BY_NAME`, `IS_MEMBER`, ...), metadata
  (`OBJECT_*`, `COLUMNPROPERTY`, `INDEXPROPERTY`, `sys.*`), system-statistical (`@@CPU_BUSY`, ...),
  collation, rowset (`OPENROWSET`/`OPENQUERY`), cursor, AI (`AI_*`), vector (`VECTOR_*`),
  text/image (`TEXTPTR`/`TEXTVALID`).

---

## MySQL

**Covered today:** the portable set; `string`/`Math`/`DateTime` CLR translation; `group_concat` rendered
through `string_agg`; `last_day` through `end_of_month`; `date_add`/`date_diff`; the text-JSON subset
(`json_value` via `json_unquote(json_extract(...))`, `json_query` via `json_extract`, `json_modify` via
`json_set`, `isjson` via `json_valid`); `current_user`/`session_user`/`schema`/`database`/`version`;
`iif`→`if`; `greatest`/`least`/`nullif`/`coalesce`.

**Native surface (shipped, [#80](https://github.com/AlexeyShirshov/nextorm/issues/80)):**
`SqlFunctions.MySql` (`MySqlFunctions`, gated per name by `ISqlDialect.MySqlFunctions`) renders
`FIND_IN_SET`, `FIELD`, `ELT`, `SUBSTRING_INDEX`, `FORMAT`, `STR_TO_DATE`, `DATE_FORMAT`,
`FROM_UNIXTIME`, `UNIX_TIMESTAMP`, `MD5`/`SHA1`/`SHA2`, `INET_ATON`/`INET_NTOA`, the JSON mutation family
(`JSON_SET`/`JSON_INSERT`/`JSON_REPLACE`/`JSON_REMOVE`/`JSON_MERGE_PATCH`/`JSON_MERGE_PRESERVE`/
`JSON_ARRAY_APPEND`/`JSON_ARRAY_INSERT`/`JSON_DEPTH`/`JSON_KEYS`/`JSON_LENGTH`/`JSON_TYPE`) and
`UUID_TO_BIN`/`BIN_TO_UUID`. MariaDB inherits the surface except `UUID_TO_BIN`/`BIN_TO_UUID` (not
implemented there). See [MySQL and MariaDB-specific SQL](../../guide/provider-specific/mysql.md). The
subsets below are the still-missing remainder of the full MySQL catalogue.

**Missing:**

* **String:** `BIN`, `EXPORT_SET`, `HEX`, `INSERT` (≈`string.Insert`), `LOCATE`/`POSITION` (≈`string.IndexOf`),
  `MAKE_SET`, `OCT`, `ORD`, `QUOTE`, `SOUNDEX`, `STRCMP`, `TO_BASE64`/`FROM_BASE64`, `UNHEX`,
  `WEIGHT_STRING`, `REGEXP`/`RLIKE` (≈`Regex.IsMatch`) (`ELT`, `FIELD`, `FIND_IN_SET`, `FORMAT`,
  `SUBSTRING_INDEX`, `REGEXP_INSTR`/`REGEXP_REPLACE`/`REGEXP_SUBSTR` are shipped on `SqlFunctions.MySql`;
  `CHAR`, `REPEAT`, `REVERSE`, `SPACE` via the portable surface).
* **Numeric:** `CONV`, `CRC32`, `LOG2`, `RAND`, `TRUNCATE` (`LOG10` and `MOD` are reachable through
  `Math.Log10`/`%`; `ABS`/`CEILING`/`FLOOR`/`ROUND`/`SQRT`/`POW`/`EXP`/`LOG`/`SIGN`/`SIN`/`COS`/`TAN` and
  `ACOS`/`ASIN`/`ATAN`/`ATAN2` through `Math.*`; `COT`/`DEGREES`/`RADIANS`/`PI` via the portable surface).
* **Date/time:** `ADDDATE`, `ADDTIME`, `CONVERT_TZ`, `DAYNAME`, `DAYOFMONTH`/`DAYOFWEEK`/`DAYOFYEAR`
  (≈`extract`), `FROM_DAYS`, `GET_FORMAT`, `MAKEDATE`, `MAKETIME`, `MICROSECOND`, `MONTHNAME`,
  `PERIOD_ADD`/`PERIOD_DIFF`, `SEC_TO_TIME`, `SUBDATE`, `SUBTIME`, `TIME`/`TIME_FORMAT`/`TIME_TO_SEC`/
  `TIMEDIFF`/`TIMESTAMP`, `TO_DAYS`/`TO_SECONDS`, `WEEK`/`WEEKDAY`/`WEEKOFYEAR` (≈`extract`), `YEARWEEK`,
  `CURDATE`/`CURTIME`/`SYSDATE`/`UTC_*` (≈`DateTime.Now`/`UtcNow`) (`DATE_FORMAT`, `FROM_UNIXTIME`,
  `STR_TO_DATE`, `UNIX_TIMESTAMP` are shipped; `UNIX_TIMESTAMP` ≈ `date_part("epoch", ...)`).
* **JSON:** `JSON_ARRAY`, `JSON_CONTAINS(_PATH)`, `JSON_EXTRACT` (≈`json_query`), `JSON_MERGE`, `JSON_OBJECT`,
  `JSON_OVERLAPS`, `JSON_PRETTY`, `JSON_QUOTE`, `JSON_SCHEMA_VALID(_REPORT)`, `JSON_SEARCH`,
  `JSON_STORAGE_FREE`/`SIZE`, `JSON_TABLE`, `JSON_UNQUOTE` (≈`json_value`), `JSON_VALID` (≈`isjson`),
  `JSON_ARRAYAGG`/`JSON_OBJECTAGG`, `->`/`->>`, `MEMBER OF` (the `JSON_*` mutation family,
  `JSON_DEPTH`/`KEYS`/`LENGTH`/`TYPE` are shipped).
* **Aggregate:** `BIT_AND`/`BIT_OR`/`BIT_XOR`, `JSON_ARRAYAGG`/`JSON_OBJECTAGG`.
* **Bit:** `BIT_COUNT`.
* **Encryption/compression/hash:** `AES_ENCRYPT`/`AES_DECRYPT`, `COMPRESS`/`UNCOMPRESS`/
  `UNCOMPRESSED_LENGTH`, `RANDOM_BYTES`, `VALIDATE_PASSWORD_STRENGTH`, `STATEMENT_DIGEST`/
  `STATEMENT_DIGEST_TEXT` (`MD5`, `SHA1`/`SHA`, `SHA2` are shipped).
* **Information:** `CONNECTION_ID`, `FOUND_ROWS`, `LAST_INSERT_ID`, `ROW_COUNT`, `USER`/`SYSTEM_USER`
  (≈`current_user`), `CURRENT_ROLE`, `CHARSET`/`COLLATION`/`COERCIBILITY`, `BENCHMARK`, `ICU_VERSION`.
* **Miscellaneous:** `UUID` (MySQL is v1-only, so `gen_random_uuid`/`uuidv7` are gated off),
  `UUID_SHORT`, `IS_UUID`, `SLEEP`, `ANY_VALUE` (≈`any_agg`), `GROUPING`, `VALUES`, `NAME_CONST`, `DEFAULT`
  (`BIN_TO_UUID`/`UUID_TO_BIN`, `INET_ATON`/`INET_NTOA` are shipped).
* **out of scope:** spatial (`ST_*`, `MBR*`, `Point`/`Polygon`/..., `ST_AsGeoJSON`), full-text is partially
  covered through `contains`/`freetext`.

---

## MariaDB

MariaDB is a superset of MySQL: **every MySQL gap above applies** — except `ANY_VALUE`, which MariaDB
gates off (`SupportsAnyValueAggregate` is `false`) — and the MySQL-compatible members
(`JSON_VALUE`, `REGEXP_*`, `group_concat`, ..., where present) behave the same. The MariaDB-specific
additions below are **shipped ([#79](https://github.com/AlexeyShirshov/nextorm/issues/79))** on the
shared `SqlFunctions.MySql` surface, gated per name so MySQL rejects them: `REGEXP_INSTR`/
`REGEXP_REPLACE`/`REGEXP_SUBSTR`, `NVL`/`NVL2`, `ADD_MONTHS`, `MONTHS_BETWEEN`,
`TO_CHAR`/`TO_DATE`/`TO_NUMBER`, `KDF`, `XXH3`/`XXH32`, `JSON_DETAILED`/`JSON_COMPACT` and the
sequence functions `NEXT VALUE FOR`/`NEXTVAL`/`SETVAL`/`LASTVAL`. The MariaDB-specific names that remain
missing:

* **Regexp / string:** `SFORMAT`, `TRIM_ORACLE`, `NATURAL_SORT_KEY`, `LENGTHB`, `CHR`.
* **Date/time / numeric:** `TRUNC`, `FORMAT_PICO_TIME`.
* **Conditional:** `DECODE_ORACLE`.
* **Information / hash:** `ROWNUM`, `DECODE_HISTOGRAM`, `CURRENT_ROLE`, `BINLOG_GTID_POS`,
  `ENCODE`/`DECODE`, `ENCRYPT`, `DES_ENCRYPT`/`DES_DECRYPT`, `OLD_PASSWORD`, `CRC32C`, `SYS_GUID`,
  `FORMAT_BYTES`.
* **JSON:** `JSON_LOOSE`, `JSON_NORMALIZE`, `JSON_EQUALS`, `JSON_OVERLAPS`, `JSON_ARRAY_INTERSECT`,
  `JSON_KEY_VALUE`, `JSON_OBJECT_FILTER_KEYS`, `JSON_OBJECT_TO_ARRAY`, `JSON_SCHEMA_VALID`.
* **Window / aggregate:** `MEDIAN`, `PERCENTILE_CONT`/`PERCENTILE_DISC` (MariaDB exposes them as window
  functions).
* **Sequence:** `PREVIOUS VALUE FOR`.
* **Miscellaneous:** `_rowid`, `COLUMN_*` dynamic-column functions, `WSREP_*` (Galera), `VEC_*` (vector),
  the `SYS.*` helper schema.
* **UUID:** `UUID_v4`/`UUID_v7` **are implemented** (`gen_random_uuid`/`uuidv7`).
* **out of scope:** geographic/spatial (same as MySQL).

---

## SQLite

**Covered today:** the portable set; `string`/`Math`/`DateTime` CLR translation; the `stdev`/`stdevp`/
`var`/`varp` custom aggregates (`SQLiteFunctions.Register`); `group_concat` through `string_agg`;
`iif`/`nullif`/`max`/`min` (for `greatest`/`least`); `version`→`sqlite_version()`; date arithmetic and
date-part extraction through `strftime`/`date`.

**Shipped:** the SQLite-only surface `SqlFunctions.Sqlite` (`SqliteFunctions`, gated by
`ISqlDialect.SqliteFunctions`; other providers reject) covers the core scalars
(`printf`/`format`, `hex`/`unhex`, `random`/`randomblob`, `quote`, `typeof`, `glob`, `unicode`/`char`,
`soundex`, `octet_length`, `if`/`ifnull`); the JSON1 family (`json`/`jsonb`, `json_extract` and the
`->`/`->>` operators, `json_array`/`json_array_insert`, `json_insert`/`json_replace`/`json_set`,
`json_object`/`json_patch`/`json_pretty`/`json_quote`/`json_remove`/`json_type`/`json_valid`, the
`json_group_array`/`json_group_object` aggregates and the `json_each`/`json_tree` table functions); the
date helpers `timediff`/`unixepoch`/`julianday`; and the math-extension functions (`acos`…`tanh`,
`log2`/`log10`, `mod`); `degrees`/`radians`/`pi` come from the portable surface. Docs: [SQLite-specific SQL](../../guide/provider-specific/sqlite.md) (EN+RU).

**Missing:**

* **Core scalar (out of scope):** `changes`, `last_insert_rowid`, `likelihood`/`likely`, `load_extension`,
  `sqlite_compileoption_get`/`sqlite_compileoption_used`, `sqlite_offset`, `sqlite_source_id`,
  `total_changes`, `unistr`/`unistr_quote`, `zeroblob`, `json_error_position`.
* **Aggregate:** `median`, `percentile`/`percentile_cont`/`percentile_disc`, `total`.

---

## ClickHouse

**Covered today:** the portable set plus the ClickHouse-specific surface: arrays
(`length`, `has`, `index_of`, `has_any`/`has_all`, `starts_with`/`ends_with`/`has_substr`,
`array_string_concat`, `split_by_char`, `array_sort`/`array_reverse`/`array_distinct`, `range`,
`array_enumerate`, `array_cum_sum`, `array_slice`, `array_push_back`, `array_join`, `group_array`/
`group_uniq_array`), higher-order (`array_map`/`filter`/`exists`/`all`/`count`/`first`/`last` (+index)
functions), the JSON family (`JSONExtract*`, `visitParamExtract*`, `JSONPath` scalars, `json_all_paths`,
`json_extract_keys*`), the `json_value`/`json_query`/`json_exists` SQL/JSON scalars, dictionaries
(`dict_get`, `dict_get_or_default`, `dict_has`, `dict_get_hierarchy`, `dict_get_children`, `dict_is_in`),
aggregates (`argMin`/`argMax`, `uniq*`, `quantile*`/`median`, `topK`/`topKWeighted`, `anyLast`,
`retention`, `windowFunnel`, `sequenceMatch`), the `-If` combinators, `global_in`, the `to*`
date-conversion/parts surface, the table functions `numbers`/`numbers_mt`/`zeros`/`zeros_mt`/
`generateRandom`/`generate_series`/`url`/`s3`/`file`/`remote`/`remoteSecure`/`cluster`/`clusterAllReplicas`,
  and `lagInFrame`/`leadInFrame`/`multi_if`.

**Native surface (shipped, [#78](https://github.com/AlexeyShirshov/nextorm/issues/78)):**
`SqlFunctions.ClickHouse` renders the UTF-8 case/trim/regexp/search strings (`lowerUTF8`/`upperUTF8`,
`trimLeft`/`trimRight`/`trimBoth`, `replaceRegexpOne`/`replaceRegexpAll`, `match`/`extract`/`extractAll`,
`splitByString`/`splitByRegexp`/`splitByWhitespace`), the date helpers (`formatDateTime`, `parseDateTime*`,
`now`/`today`/`yesterday`), the array set operations (`arrayConcat`/`arrayFlatten`/`arrayUniq`/
`arrayIntersect`/`arrayUnion`/`arrayExcept`/`arraySymmetricDifference`), the `Map` family (`map`/`map_keys`/
`map_values`/`map_contains_key`/`map_contains_value`/`map_add`/`map_concat`/`map_filter`/`map_apply`/
`map_all`/`map_exists`/`map_sort`), the bitmap aggregates (`groupBitmap`/`groupBitmapAnd`/`groupBitmapOr`/
`groupBitmapXor`, `sumMap`/`sumMapFiltered`), the hash family (`md5`/`sha1`/`sha256`/`sha512`, `xxHash32`/
`xxHash64`/`xxh3`, `cityHash64`, `sipHash64`/`sipHash128`, `murmurHash*`) and `generateULID`. See
[ClickHouse-specific SQL](../../guide/provider-specific/clickhouse.md). The lists below are the
still-missing remainder of the full ClickHouse catalogue.

**Missing:**

* **String:** `concat` (≈`string.Concat`), `substring`/`substringUTF8` (≈`string.Substring`),
  `reverse`/`reverseUTF8` (≈portable `reverse`), `format`/`printf` (≈`string.Format` for the supported
  numeric/date specifiers), `overlay`, `position`/`positionUTF8` (≈`string.IndexOf`), `initcap`,
  `soundex`, `base64Encode`/`base64Decode`, `hex`/`unhex`, `editDistance`, `jaroSimilarity`,
  `byteHammingDistance`, `normalizeUTF8*`, `substringIndex`, `countSubstrings`, `extractGroups`
  (`lowerUTF8`/`upperUTF8`, `trimLeft`/`trimRight`/`trimBoth`, `replaceRegexpOne`/`replaceRegexpAll`,
  `match`/`extract`/`extractAll`, `splitByString`/`splitByRegexp`/`splitByWhitespace`, `left`/`right`,
  `leftPad`/`rightPad`, `repeat`, `translate` are shipped).
* **Numeric / math:** `log2`, `cbrt`, `erf`, `erfc`, `lgamma`, `tgamma`, `gcd`, `lcm`, `e`,
  `isFinite`/`isInfinite`/`isNaN`, `exp10`/`exp2`, `hypot`, `intDiv`, `intExp2`/`intExp10`,
  `plus`/`minus`/`multiply`/`divide`/`negate` (operator forms), `sigmoid`, `sqr`.
  `abs`/`ceil`/`floor`/`round`/`trunc`/`sqrt`/`pow`/`exp`/`log`/`log10` and `sin`/`cos`/`tan`/`asin`/
  `acos`/`atan`/`atan2` are reachable through `Math.*`; `pi` from the portable surface.
* **Rounding:** `roundAge`, `roundBankers`, `roundDown`, `roundDuration`, `roundToExp2` (`round`/`ceil`/
  `floor`/`trunc` are reachable through `Math.*`).
* **Date/time:** `makeDate`/`makeDate32`/`makeDateTime`(+`64`), `now64`/`nowInBlock`,
  `timeSlot`/`timeSlots`, `toStartOfFifteenMinutes`/`toStartOfFiveMinutes`/`toStartOfTenMinutes`,
  `toStartOfInterval`, `toISOYear`/`toWeek`/`toYearWeek`/`toLastDayOfWeek`, `toDaysInMonth`,
  `fromUnixTimestamp*`, `fromUTCTimestamp`, `toModifiedJulianDay`, `age`, `dateName`, `serverTimezone`,
  `timezoneOf`/`timezoneOffset`, `toTime`/`toTime64`, `timestamp` (`formatDateTime`(+`InJodaSyntax`),
  `parseDateTime*`, `now`/`today`/`yesterday` are shipped).
* **Array:** `arrayElement`, `arrayEnumerateDense`(`Ranked`), `arrayZip`, `arrayDifference`,
  `arrayAvg`/`arraySum`/`arrayMin`/`arrayMax`/`arrayProduct`, `arrayTopK`/`arrayBottomK`,
  `arrayRandomSample`, `arrayCompact`, `arrayPopBack`/`arrayPopFront`/`arrayPushFront`, `arrayResize`,
  `arrayRemove`, `arrayRotateLeft`/`arrayRotateRight`/`arrayShiftLeft`/`arrayShiftRight`, `arrayReduce`,
  `arrayFold`, `arrayFill`/`arrayReverseFill`, `arraySplit`/`arrayReverseSplit`,
  `arrayReverseSort`/`arrayPartialSort`, `arrayShingles`, `arrayTranspose`, `countEqual`,
  `arrayWithConstant`, `arrayJaccardIndex`/`arraySimilarity` (`arrayConcat`, `arrayFlatten`, `arrayUniq`,
  `arrayIntersect`/`arrayUnion`/`arrayExcept`/`arraySymmetricDifference` are shipped).
* **Tuple:** `tupleConcat`, `tupleNames`, `untuple`, `flattenTuple`, `tupleToNameValuePairs`,
  `tuple+`/`tuple-`/`tuple*`/`tuple/` and the `tupleHammingDistance`/`dotProduct` helpers (the portable
  row-value surface covers `tuple(...)` and `tupleElement`).
* **Map:** `mapSubtract`, `mapUpdate`, `mapReverseSort`, `mapPartialSort`, `mapFromArrays`,
  `mapPopulateSeries`, `extractKeyValuePairs` (the rest of the family, `map`/`mapKeys`/`mapValues`/
  `mapContainsKey`/`mapContainsValue`/`mapAdd`/`mapConcat`/`mapFilter`/`mapApply`/`mapAll`/`mapExists`/
  `mapSort`, is shipped).
* **Higher-order:** `arrayFold`, `arrayCumSumNonNegative`, `arrayFill`/`arrayReverseFill`,
  `arraySort`/`arrayReverseSort`/`arrayPartialSort`, `arraySplit`/`arrayReverseSplit`,
  `arraySum`/`arrayMin`/`arrayMax`/`arrayAvg`/`arrayProduct`, `arrayReduce`/`arrayReduceInRanges`,
  `arrayTopK`/`arrayBottomK` (the `map*` higher-order forms are shipped).
* **JSON:** `JSONMergePatch`, `JSONArrayLength`, `JSONDynamicPaths`(`WithTypes`), `JSONSharedDataPaths`
  (`WithTypes`), `JSONKey`, `JSONExtractKeysAndValuesRaw`, `JSONExtractUInt`/`...OrDefault`,
  `simpleJSONExtract*`, `prettyPrintJSON`, `isValidJSON` (`toJSONString` is shipped).
* **Hash:** `SHA224`/`SHA384`/`SHA512_256`, `BLAKE3`, `MD4`, `RIPEMD160`, `keccak256`, `farmHash64`,
  `metroHash64`, `javaHash`, `hiveHash`, `intHash*`, `halfMD5`, `URLHash`, `wyHash64`, and the
  `ngramMinHash`/`ngramSimHash`/`wordShingle*` families (`MD5`, `SHA1`/`SHA256`/`SHA512`,
  `xxHash32`/`xxHash64`/`xxh3`, `sipHash64`/`sipHash128`, `cityHash64`, `murmurHash*` are shipped).
* **Encoding:** `bin`/`unbin`, `hex`/`unhex`, `base32*`/`base58*`/`base64*`(+`URL`), `bech32*`,
  `bitmaskToArray`/`bitmaskToList`/`bitPositionsToArray`, `sqidEncode`/`sqidDecode`,
  `hilbertEncode`/`hilbertDecode`, `mortonEncode`/`mortonDecode` (`char` ships via the portable surface).
* **UUID / ULID:** `UUIDNumToString`, `UUIDStringToNum`, `UUIDToNum`, `UUIDv7ToDateTime`, `toUUID`,
  `toUUIDOrDefault`/`toUUIDOrNull`, `ULIDStringToDateTime`, the snowflake family (`generateSnowflakeID`,
  `dateTimeToSnowflakeID`, `dateTime64ToSnowflakeID`, `snowflakeIDToDateTime*`), `dateTimeToUUIDv7`
  (`generateULID` is shipped).
* **URL / IP:** the entire family (`domain`, `protocol`, `path`, `queryString`, `fragment`, `port`,
  `extractURLParameter*`, `cutURLParameter`, `cutToFirstSignificantSubdomain*`, `topLevelDomain`,
  `encodeURLComponent`, `IPv4NumToString`, `IPv6NumToString`, `toIPv4`/`toIPv6`, `isIPv4String`/
  `isIPv6String`, `isIPAddressInRange`, `IPv4CIDRToRange`/`IPv6CIDRToRange`, ...).
* **Bit:** the entire family: `bitAnd`/`bitOr`/`bitXor`/`bitNot`, `bitShiftLeft`/`bitShiftRight`,
  `bitRotateLeft`/`bitRotateRight`, `bitCount`, `bitTest`/`bitTestAll`/`bitTestAny`,
  `bitHammingDistance`.
* **Type conversion:** `toInt*`/`toUInt*`/`toFloat*`/`toDecimal*`/`toString`/`toBool`/`toFixedString`/
  `toLowCardinality`/`toInterval*`/`accurateCast*`/`reinterpretAs*` (numeric/date casts
  are reachable through the CLR cast + `MakeTypeName`).
* **Aggregate:** `sum`/`avg`/`count`/`min`/`max`/`stddev`/`var`/`corr`/`covar` are portable;
  `groupArray`/`groupUniqArray`/`uniq*`/`quantile*`/`median`/`topK`/`argMin`/`argMax`/`anyLast` and the
  bitmap aggregates (`groupBitmap`(`And`/`Or`/`Xor`), `groupBitAnd`/`groupBitOr`/`groupBitXor`,
  `sumMap`/`sumMapFiltered`) are implemented. Missing: `entropy`, `first_value`/`last_value`
  (the window forms ship via `SqlFunctions.Sql`),
  `maxIntersections`(`Position`),
  `simpleLinearRegression`/`stochasticLinearRegression`/`stochasticLogisticRegression`,
  `deltaSum`/`deltaSumTimestamp`, `exponentialMovingAverage`/`exponentialTimeDecayed*`,
  `gini`, `skewPop`/`skewSamp`, `kurtPop`/`kurtSamp`, `rankCorr`, `contingency`/`cramersV`/`cramersVBiasCorrected`/
  `theilsU`, `studentTTest*`/`welchTTest`/`mannWhitneyUTest`/`kolmogorovSmirnovTest`/`meanZTest`/
  `analysisOfVariance`, `quantileTDigest`/`quantileGK`/`quantileDD`/`quantileBFloat16`/`quantilePrometheusHistogram`
  and the `quantiles*` plural family (only the plain `quantile`/`quantiles`/`quantileExact`/`quantileTiming`
  are wrapped), `approx_top_k`/`approx_top_sum`, `anyHeavy` (`any` ≈ `any_agg`), `maxMap`/`minMap`, `sumCount`,
  `sumKahan`, `groupArrayMovingAvg`/`groupArrayMovingSum`/`groupArraySample`/`groupArraySorted`/
  `groupArrayLast`/`groupArrayInsertAt`/`groupArrayIntersect`, `groupConcat` (renderable through
  `string_agg`), `groupFormat`, `histogram`, `sparkbar`, `uniqUpTo`, the time-series (`timeSeries*`) and
  `sequenceCount`/`sequenceMatchEvents`/`sequenceNextNode` family.
* **Window:** standard functions are implemented (`row_number`, `rank`, `dense_rank`, `percent_rank`,
  `cume_dist`, `ntile`, `lag`/`lead`, `first_value`/`last_value`/`nth_value`, `lagInFrame`/`leadInFrame`).
  Missing: `nonNegativeDerivative`.
* **Table functions:** `numbers`/`numbers_mt`/`zeros`/`zeros_mt`/`generateRandom`/`generate_series`/
  `url`/`s3`/`file`/`remote`/`remoteSecure`/`cluster`/`clusterAllReplicas` are implemented. Missing:
  `mysql`, `postgresql`, `sqlite`, `jdbc`, `odbc`, `mongodb`, `redis`, `hdfs`(`Cluster`), `s3Cluster`,
  `azureBlobStorage`(`Cluster`), `deltaLake`(`Cluster`), `iceberg`(`Cluster`), `hudi`(`Cluster`),
  `paimon`(`Cluster`), `bigquery`, `gcs`, `ytsaurus`, `arrowFlight`, `prometheusQuery`(`Range`),
  `input`, `view`, `format`, `null`, `executable`, `primes`, `merge`, `dictionary`, `eval`,
  `loop`, `fuzzJSON`, `fuzzQuery`, `timeSeries*` — **mostly out of scope** (external engines); `values`
  ships via `SqlFunctions.ClickHouse`.
* **Dictionary:** `dict_get`/`dict_get_or_default`/`dict_has`/`dict_get_hierarchy`/`dict_get_children`/
  `dict_is_in` are implemented. Missing: the typed `dictGet*` accessors, `dictGetKeys`, `dictGetRoot`,
  `dictGetAll`/`dictGetDescendants`, and the `regionTo*`/`regionIn`/`regionHierarchy` family.

---

## Sources

* PostgreSQL: `https://www.postgresql.org/docs/current/functions.html` and its subsection pages
  (`functions-string`, `functions-math`, `functions-datetime`, `functions-array`, `functions-json`,
  `functions-aggregate`, `functions-window`, `functions-textsearch`, `functions-uuid`,
  `functions-conditional`, `functions-net`, `functions-range`, `functions-sequence`,
  `functions-binarystring`, `functions-formatting`, `functions-xml`, `functions-enum`,
  `functions-geometry`, `functions-info`, `functions-matching`).
* SQL Server: `https://learn.microsoft.com/en-us/sql/t-sql/functions/functions?view=sql-server-ver17`
  and its category pages (string, mathematical, date-and-time, json, conversion, aggregate, ranking,
  analytic, security, cryptographic, metadata, system, system-statistical, bit-manipulation,
  regular-expressions, ai, vector, xml).
* MySQL: `https://dev.mysql.com/doc/refman/8.4/en/built-in-function-reference.html` and the section
  pages (string, mathematical, date-and-time, json, aggregate, window, information, encryption,
  locking, miscellaneous, bit, spatial, fulltext, cast, flow-control, comparison).
* MariaDB: `https://mariadb.com/kb/en/built-in-functions/` and its subpages (string, numeric,
  date-time, json, aggregate, window, control-flow, information, sequence, miscellaneous, geographic,
  encryption-hashing-compression, bit, regular-expressions).
* SQLite: `https://sqlite.org/lang_corefunc.html`, `lang_aggfunc.html`, `lang_mathfunc.html`,
  `lang_datefunc.html`, `json1.html`, `windowfunctions.html`.
* ClickHouse: `https://clickhouse.com/docs/en/sql-reference/functions` and the category pages
  (string-functions, string-replace-functions, string-search-functions, splitting-merging-functions,
  arithmetic-functions, math-functions, rounding-functions, comparison-functions, logical-functions,
  conditional-functions, array-functions, date-time-functions, time-window-functions,
  type-conversion-functions, json-functions, tuple-functions, tuple-map-functions, bit-functions,
  hash-functions, encoding-functions, uuid-functions, ulid-functions, url-functions,
  ip-address-functions, time-series-functions, ext-dict-functions, aggregate-functions,
  table-functions).
