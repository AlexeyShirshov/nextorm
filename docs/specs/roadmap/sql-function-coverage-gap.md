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
  [Scalar functions](../../guide/11-scalar-functions.md) for the documented surface.
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
* Nothing here is a commitment; the actionable subset is the [summary table](#summary-highest-value-gaps).

## Summary: highest-value gaps

| Provider | Most valuable missing functions (not reachable by a portable member today) |
|---|---|
| **Cross-provider** | `left`/`right`, `lpad`/`rpad`, `repeat`, `reverse`, `space`, `concat_ws`, `translate`, `ascii`/`char`, `mod`, `log10`, `power`, `format` (all exist on ≥3 providers, only PostgreSQL has wrappers) |
| **PostgreSQL** | `sha224`/`sha384`/`sha512`, `regexp_substr`, `make_date`/`make_time`/`make_timestamp`, `age`, `date_bin`, `current_setting`, `nextval`/`setval`, `num_nulls` is present but `json_array`/SQL-JSON `json_value` absent |
| **SQL Server** | `PATINDEX`, `QUOTENAME`, `SOUNDEX`, `DIFFERENCE`, `STRING_ESCAPE`, `TRANSLATE`, `FORMAT`, `ASCII`/`CHAR`/`UNICODE`, `ACOS`/`ASIN`/`ATAN`/`ATN2`/`COT`/`DEGREES`/`RADIANS`/`PI`/`LOG10`, `DATENAME`, `DATE_BUCKET`, `HASHBYTES`, `NEWSEQUENTIALID`, `JSON_ARRAY`/`JSON_OBJECT`/`JSON_ARRAYAGG`/`JSON_CONTAINS` |
| **MySQL** | `FIND_IN_SET`, `FIELD`, `ELT`, `SUBSTRING_INDEX`, `FORMAT`, `STR_TO_DATE`, `DATE_FORMAT`, `LAST_DAY` (via `end_of_month`), `FROM_UNIXTIME`, `UNIX_TIMESTAMP`, `MD5`/`SHA1`/`SHA2`, `INET_ATON`/`INET_NTOA`, the whole `JSON_*` mutation family, `UUID_TO_BIN`/`BIN_TO_UUID` |
| **MariaDB** | everything MySQL lacks plus `REGEXP_INSTR`/`REGEXP_REPLACE`/`REGEXP_SUBSTR`, `NVL`/`NVL2`, `ADD_MONTHS`, `MONTHS_BETWEEN`, `TO_CHAR`/`TO_DATE`/`TO_NUMBER`, `KDF`, `XXH3`, `JSON_DETAILED`/`JSON_COMPACT`, `NEXT VALUE FOR` sequences |
| **SQLite** | the whole JSON1 family (`json_extract`, `->`, `json_set`, `json_group_array`, `json_each`, `json_tree`), `printf`/`format`, `hex`/`unhex`, `random`/`randomblob`, `quote`, `typeof`, `glob`, `unicode`/`char`, `timediff`, most `Math.*` |
| **ClickHouse** | `lowerUTF8`/`upperUTF8`, `trim*`, `replaceRegexp*`, `match`/`extract`, `splitByString`, `formatDateTime`/`parseDateTime`, `now`/`today`/`yesterday`, the `map*` family, `arrayConcat`/`arrayFlatten`/`arrayUniq`/`arrayIntersect`, the `groupBitmap`/`groupBit*`/`sumMap` aggregates, hash functions, `generateULID` |

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
`Math.Abs`/`Ceiling`/`Floor`/`Round`/`Truncate`/`Sqrt`/`Pow`/`Exp`/`Log`(natural)/`Sin`/`Cos`/`Tan`/`Sign`,
`DateTime.Now`/`UtcNow`/`Year`/`Month`/`Day`/`DayOfYear`/`Hour`/`Minute`/`Second` and `DateTime.AddDays`/
`AddMonths`/…, `??`, numeric casts.

**Missing as *portable* wrappers** (functions that exist on several providers but have no `SqlFunctions.Sql`
member today; the PostgreSQL provider exposes some of them, see below):

* **String:** `left`, `right`, `lpad`, `rpad`, `repeat`, `reverse`, `space`, `replace` (only the CLR
  `string.Replace`), `concat_ws`, `translate`, `ascii`, `char`, `bit_length`, `octet_length`, `format`.
* **Numeric:** `mod`, `log10`, `power` (only CLR `Math.Pow`), `ln` (only CLR `Math.Log`), `cot`,
  `degrees`, `radians`, `pi`, `sign` (only CLR), `acos`/`asin`/`atan`/`atan2`.
* **Date:** `date_diff` exists but `date_bin`/`age`/`timediff` have no portable form; `now`/`current_timestamp`
  are only reachable through `DateTime.Now`.
* **Aggregate:** `bit_and`/`bit_or`/`bit_xor`, `bool_and`/`bool_or`/`every`, `any_value`, `mode`,
  `stddev`/`variance` family spellings beyond the curated set.

> Adding portable wrappers here is the single highest-leverage change: one member lights up several
> providers (see the string/regexp item 35 in [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md)).

---

## PostgreSQL

**Covered today:** the portable set plus the extended scalar library (`asin`, `acos`, `atan`, `atan2`,
`cbrt`, `sinh`/`cosh`/`tanh`, `asinh`/`acosh`/`atanh`, `degrees`, `radians`, `pi`, `random`, `setseed`,
`log(base,x)`, `mod`, `gcd`, `lcm`, `factorial`, `width_bucket`), the string library (`split_part`,
`strpos`, `left`/`right`, `lpad`/`rpad`, `repeat`, `reverse`, `initcap`, `translate`, `overlay`,
`concat_ws`, `format`), `md5`/`digest`/`sha256`, the regexp family (`regexp_replace`, `regexp_like`,
`regexp_split_to_array`, `regexp_count`, `regexp_instr`, `regexp_matches`, `regexp_split_to_table`),
native JSON/JSONB (≈60 functions), native arrays (≈28 functions), `generate_series`/`unnest`, the
statistical/boolean/bit aggregates, `num_nulls`/`num_nonnulls`, `mode`, `percentile_cont`/`disc`,
`regexp_*` and the `ts_*` full-text surface.

**Missing:**

* **String:** `ascii`, `chr`, `bit_length`, `octet_length`, `char_length` (≈`Length`), `btrim`,
  two-argument `ltrim`/`rtrim`, `position` (≈`IndexOf`), `starts_with`, `parse_ident`, `quote_ident`,
  `quote_literal`, `quote_nullable`, `to_ascii`, `to_hex`/`to_oct`/`to_bin`, `convert_from`/`convert_to`,
  `encode`/`decode`, `normalize`, `casefold`, `unistr`, `string_to_table`, `regexp_substr`, `regexp_match`.
* **Hash:** `sha224`, `sha384`, `sha512` (only `md5`, `sha256` and generic `digest` are wrapped).
* **Numeric:** `cot`, `div`, `erf`, `erfc`, `gamma`/`tgamma`, `lgamma`, `log10`, `scale`, `min_scale`,
  `trim_scale`, `random_normal`, and the degree variants `acosd`/`asind`/`atand`/`atan2d`/`cosd`/`cotd`/
  `sind`/`tand`.
* **Date/time:** `age`, `date_bin`, `date_subtract`, `justify_interval`, `make_date`, `make_time`,
  `make_timestamp`, `make_timestamptz`, `clock_timestamp`, `statement_timestamp`, `transaction_timestamp`,
  `timeofday`, `isfinite`, `pg_sleep`/`pg_sleep_for`/`pg_sleep_until`.
* **JSON/JSONB:** `json_array`/`jsonb_array`, `json_object`/`jsonb_object`, `json`/`json_scalar`/
  `json_serialize`, `json_array_elements(_text)` and `json_each(_text)` (non-`b` forms),
  `json_extract_path(_text)`/`jsonb_extract_path(_text)`, `json_populate_record(set)`,
  `jsonb_populate_record(set)`, `json_to_record(set)`/`jsonb_to_record(set)`, `jsonb_set_lax`,
  `json_strip_nulls`, the `jsonb_path_*_tz` variants, the SQL/JSON `json_exists`/`json_query`/
  `json_value`/`json_table`, `json_agg_strict`/`jsonb_agg_strict`, `json_object_agg_unique(+strict)`,
  `json_arrayagg`/`json_objectagg`, `jsonb_populate_record_valid`.
* **Array:** `trim_array`, `generate_subscripts`, and the multi-array/`ordinality` forms of `unnest`.
* **Aggregate:** `any_value`, `grouping`, `xmlagg`, `range_agg`, `range_intersect_agg`, `regr_sxx`/
  `regr_sxy`/`regr_syy`, `json_arrayagg`/`json_objectagg`.
* **Full-text:** `array_to_tsvector`, `json_to_tsvector`/`jsonb_to_tsvector`, `get_current_ts_config`,
  `numnode`, `querytree`, `setweight`, `strip`, `ts_delete`, `ts_filter`, `ts_rewrite`, `tsquery_phrase`,
  `tsvector_to_array`, `ts_debug`, `ts_lexize`, `ts_parse`, `ts_token_type`.
* **UUID:** `uuidv4` (≈`gen_random_uuid`), `uuid_extract_timestamp`, `uuid_extract_version`.
* **System info:** `current_role`, `current_catalog`, `current_schemas`, `current_setting`/`set_config`,
  `pg_backend_pid`, `pg_postmaster_start_time`, `pg_current_logfile`, and the `pg_*` introspection family.
* **Sequence:** `nextval`, `setval`, `currval`, `lastval`.
* **Binary string:** `encode`/`decode` (also under string), `get_bit`/`set_bit`, `get_byte`/`set_byte`,
  `bit_count`, `crc32`/`crc32c`, `sha224`/`sha384`/`sha512`, `convert_from`/`convert_to`.
* **out of scope:** network (`host`, `masklen`, `broadcast`, `family`, `netmask`, `set_masklen`, `abbrev`,
  `inet_*`, `macaddr8_set7bit`), range/multirange, geometric, enum, `pg_*` metadata/permission helpers,
  XML (`xml*`, `xpath`, `xmltable`, ...), `ts_stat` is present but the debug/parse family is not.

---

## SQL Server

**Covered today:** the portable set plus `json_value`/`json_query`/`json_modify`/`isjson`, `choose`,
`string_split`, `openjson`/`openxml`, `contains`/`freetext` (with `containstable`/`freetexttable`), the
`xml` data-type methods (`xml_value`/`xml_query`/`xml_exist`/`xml_nodes`), and `iif`/`greatest`/`least`.

**Missing:**

* **String:** `ASCII`, `CHAR`, `NCHAR`, `UNICODE`, `DIFFERENCE`, `FORMAT`, `PATINDEX`, `QUOTENAME`,
  `SOUNDEX`, `SPACE`, `STR`, `STRING_ESCAPE`, `TRANSLATE`, `REVERSE`, `CONCAT_WS` (only `string.Concat`
  and `+` work), `LEN`/`LEFT`/`RIGHT`/`REPLICATE`/`STUFF`/`REPLACE`/`SUBSTRING`/`CHARINDEX`/`LOWER`/`UPPER`/
  `TRIM`/`LTRIM`/`RTRIM` are reachable through the CLR string members (`≈`).
* **Regular expressions (SQL Server 2025):** `REGEXP_LIKE`, `REGEXP_REPLACE`, `REGEXP_SUBSTR`,
  `REGEXP_COUNT`, `REGEXP_INSTR`, `REGEXP_MATCHES`, `REGEXP_SPLIT_TO_TABLE`.
* **Fuzzy matching (SQL Server 2025):** `EDIT_DISTANCE`, `EDIT_DISTANCE_SIMILARITY`,
  `JARO_WINKLER_DISTANCE`, `JARO_WINKLER_SIMILARITY`.
* **Numeric:** `ACOS`, `ASIN`, `ATAN`, `ATN2`, `COT`, `DEGREES`, `LOG10`, `PI`, `RADIANS`, `RAND`,
  `SQUARE` (`ABS`/`CEILING`/`FLOOR`/`ROUND`/`SQRT`/`POWER`/`EXP`/`LOG`/`SIGN`/`SIN`/`COS`/`TAN`/`TRUNC`
  are reachable through `Math.*`).
* **Bit manipulation:** `LEFT_SHIFT`, `RIGHT_SHIFT`, `BIT_COUNT`, `GET_BIT`, `SET_BIT`.
* **Date/time:** `DATE_BUCKET`, `DATENAME`, `DATETIMEFROMPARTS`, `DATETIME2FROMPARTS`,
  `DATETIMEOFFSETFROMPARTS`, `SMALLDATETIMEFROMPARTS`, `TIMEFROMPARTS`, `SWITCHOFFSET`,
  `TODATETIMEOFFSET`, `ISDATE` (`DATEADD`/`DATEDIFF`/`DATEDIFF_BIG`/`DATEFROMPARTS`/`EOMONTH`/
  `DATETRUNC`/`DATEPART`/`YEAR`/`MONTH`/`DAY` are covered).
* **JSON:** `JSON_ARRAY`, `JSON_OBJECT`, `JSON_ARRAYAGG`, `JSON_OBJECTAGG`, `JSON_CONTAINS`,
  `JSON_PATH_EXISTS`.
* **Conversion:** `PARSE`, `TRY_CAST`, `TRY_CONVERT`, `TRY_PARSE`, `BASE64_ENCODE`/`BASE64_DECODE`.
* **Logical:** `ISNUMERIC`.
* **Aggregate:** `ANY_VALUE`, `APPROX_COUNT_DISTINCT`, `CHECKSUM_AGG`, `GROUPING_ID`, `PRODUCT`.
* **Analytic:** `APPROX_PERCENTILE_CONT`, `APPROX_PERCENTILE_DISC`.
* **Cryptographic:** `HASHBYTES`, `CRYPT_GEN_RANDOM`, and the `ENCRYPTBY*`/`DECRYPTBY*`/`SIGNBY*`/
  `VERIFYSIGNEDBY*`/`CERT*`/`KEY_*` family **out of scope**.
* **System:** `NEWSEQUENTIALID`, `COMPRESS`/`DECOMPRESS`, `SESSION_CONTEXT`, `SESSION_ID`,
  `CURRENT_TIMEZONE(_ID)`, `XACT_STATE`, `@@ROWCOUNT`/`@@ERROR`/`ERROR_*`, `HOST_NAME`/`HOST_ID`.
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

**Missing:**

* **String:** `BIN`, `BIT_LENGTH`, `CHAR`, `ELT`, `EXPORT_SET`, `FIELD`, `FIND_IN_SET`, `FORMAT`, `HEX`,
  `INSERT`, `LOCATE`/`POSITION` (≈`IndexOf`), `MAKE_SET`, `OCT`, `ORD`, `QUOTE`, `REPEAT`, `REVERSE`,
  `SOUNDEX`, `SPACE`, `STRCMP`, `SUBSTRING_INDEX`, `TO_BASE64`/`FROM_BASE64`, `UNHEX`, `WEIGHT_STRING`,
  `REGEXP`/`RLIKE` and `REGEXP_INSTR`/`REGEXP_LIKE`/`REGEXP_REPLACE`/`REGEXP_SUBSTR`.
* **Numeric:** `ACOS`, `ASIN`, `ATAN`, `ATAN2`, `CONV`, `COT`, `CRC32`, `DEGREES`, `LOG2`, `LOG10`, `MOD`,
  `PI`, `RADIANS`, `RAND`, `TRUNCATE` (`ABS`/`CEILING`/`FLOOR`/`ROUND`/`SQRT`/`POW`/`EXP`/`LOG`/`SIGN`/
  `SIN`/`COS`/`TAN` are reachable through `Math.*`).
* **Date/time:** `ADDDATE`, `ADDTIME`, `CONVERT_TZ`, `DATE_FORMAT`, `DAYNAME`, `DAYOFMONTH`/`DAYOFWEEK`/
  `DAYOFYEAR` (≈`extract`), `FROM_DAYS`, `FROM_UNIXTIME`, `GET_FORMAT`, `MAKEDATE`, `MAKETIME`,
  `MICROSECOND`, `MONTHNAME`, `PERIOD_ADD`/`PERIOD_DIFF`, `SEC_TO_TIME`, `STR_TO_DATE`, `SUBDATE`,
  `SUBTIME`, `TIME`/`TIME_FORMAT`/`TIME_TO_SEC`/`TIMEDIFF`/`TIMESTAMP`, `TO_DAYS`/`TO_SECONDS`,
  `UNIX_TIMESTAMP` (≈`date_part("epoch", ...)`), `WEEK`/`WEEKDAY`/`WEEKOFYEAR` (≈`extract`), `YEARWEEK`,
  `CURDATE`/`CURTIME`/`SYSDATE`/`UTC_*` (≈`DateTime.Now`/`UtcNow`).
* **JSON:** `JSON_ARRAY`, `JSON_ARRAY_APPEND`/`JSON_ARRAY_INSERT`, `JSON_CONTAINS(_PATH)`, `JSON_DEPTH`,
  `JSON_EXTRACT`, `JSON_INSERT`, `JSON_KEYS`, `JSON_LENGTH`, `JSON_MERGE`/`JSON_MERGE_PATCH`/
  `JSON_MERGE_PRESERVE`, `JSON_OBJECT`, `JSON_OVERLAPS`, `JSON_PRETTY`, `JSON_QUOTE`, `JSON_REMOVE`,
  `JSON_REPLACE`, `JSON_SCHEMA_VALID(_REPORT)`, `JSON_SEARCH`, `JSON_SET`, `JSON_STORAGE_FREE`/`SIZE`,
  `JSON_TABLE`, `JSON_TYPE`, `JSON_UNQUOTE`, `JSON_VALID`, `JSON_ARRAYAGG`/`JSON_OBJECTAGG`, `->`/`->>`,
  `MEMBER OF`.
* **Aggregate:** `BIT_AND`/`BIT_OR`/`BIT_XOR`, `JSON_ARRAYAGG`/`JSON_OBJECTAGG`.
* **Bit:** `BIT_COUNT`.
* **Encryption/compression/hash:** `MD5`, `SHA1`/`SHA`, `SHA2`, `AES_ENCRYPT`/`AES_DECRYPT`, `COMPRESS`/
  `UNCOMPRESS`/`UNCOMPRESSED_LENGTH`, `RANDOM_BYTES`, `VALIDATE_PASSWORD_STRENGTH`,
  `STATEMENT_DIGEST`/`STATEMENT_DIGEST_TEXT`.
* **Information:** `CONNECTION_ID`, `FOUND_ROWS`, `LAST_INSERT_ID`, `ROW_COUNT`, `USER`/`SYSTEM_USER`,
  `CURRENT_ROLE`, `CHARSET`/`COLLATION`/`COERCIBILITY`, `BENCHMARK`, `ICU_VERSION`.
* **Miscellaneous:** `UUID` (MySQL is v1-only, so `gen_random_uuid`/`uuidv7` are gated off),
  `UUID_SHORT`, `BIN_TO_UUID`/`UUID_TO_BIN`, `IS_UUID`, `INET_ATON`/`INET_NTOA`, `SLEEP`, `ANY_VALUE`,
  `GROUPING`, `VALUES`, `NAME_CONST`, `DEFAULT`.
* **out of scope:** spatial (`ST_*`, `MBR*`, `Point`/`Polygon`/..., `ST_AsGeoJSON`), full-text is partially
  covered through `contains`/`freetext`.

---

## MariaDB

MariaDB is a superset of MySQL: **every MySQL gap above applies**, and the MySQL-compatible members
(`JSON_VALUE`, `REGEXP_*`, `group_concat`, ..., where present) behave the same. The following
MariaDB-specific additions are also missing:

* **Regexp / string:** `REGEXP_INSTR`, `REGEXP_REPLACE`, `REGEXP_SUBSTR`, `SFORMAT`, `TRIM_ORACLE`,
  `NATURAL_SORT_KEY`, `LENGTHB`, `CHR`, `TO_CHAR`, `TO_DATE`, `TO_NUMBER`.
* **Date/time / numeric:** `ADD_MONTHS`, `MONTHS_BETWEEN`, `TRUNC`, `FORMAT_PICO_TIME`.
* **Conditional:** `DECODE_ORACLE`, `NVL`, `NVL2`.
* **Information / hash:** `ROWNUM`, `DECODE_HISTOGRAM`, `CURRENT_ROLE`, `BINLOG_GTID_POS`, `KDF`,
  `ENCODE`/`DECODE`, `ENCRYPT`, `DES_ENCRYPT`/`DES_DECRYPT`, `OLD_PASSWORD`, `CRC32C`, `XXH3`/`XXH32`,
  `SYS_GUID`, `FORMAT_BYTES`.
* **JSON:** `JSON_DETAILED`, `JSON_COMPACT`, `JSON_LOOSE`, `JSON_NORMALIZE`, `JSON_EQUALS`,
  `JSON_OVERLAPS`, `JSON_ARRAY_INTERSECT`, `JSON_KEY_VALUE`, `JSON_OBJECT_FILTER_KEYS`,
  `JSON_OBJECT_TO_ARRAY`, `JSON_SCHEMA_VALID`.
* **Window / aggregate:** `MEDIAN`, `PERCENTILE_CONT`/`PERCENTILE_DISC` (MariaDB exposes them as window
  functions).
* **Sequence:** `NEXT VALUE FOR`, `NEXTVAL`, `LASTVAL`, `PREVIOUS VALUE FOR`, `SETVAL`.
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

**Missing:**

* **Core scalar:** `changes`, `concat`/`concat_ws`, `format`, `glob`, `hex`/`unhex`, `if`/`ifnull`
  (`ifnull` ≈ `??`), `last_insert_rowid`, `likelihood`/`likely`, `load_extension`, `printf`, `quote`,
  `random`/`randomblob`, `sqlite_compileoption_get`/`sqlite_compileoption_used`, `sqlite_offset`,
  `sqlite_source_id`, `total_changes`, `typeof`, `unicode`, `unistr`/`unistr_quote`, `zeroblob`, `char`,
  `soundex`, `octet_length`, `sign` (≈`Math.Sign`).
* **String:** `substr`/`substring`/`trim`/`ltrim`/`rtrim`/`length`/`replace`/`upper`/`lower`/`instr` are
  reachable through the CLR members (`≈`).
* **Date/time:** `timediff`; `unixepoch` (≈`date_part("epoch", ...)`); `julianday`.
* **Math:** `acos`, `acosh`, `asin`, `asinh`, `atan`, `atan2`, `atanh`, `cosh`, `degrees`, `log10`,
  `log2`, `mod`, `pi`, `radians`, `sinh`, `tanh` (`abs`/`ceil`/`ceiling`/`floor`/`round`/`trunc`/`sqrt`/
  `pow`/`power`/`exp`/`ln`/`sin`/`cos`/`tan` are reachable through `Math.*`).
* **JSON (JSON1):** the entire family is missing: `json`/`jsonb`, `json_array`/`json_array_insert`,
  `json_extract` and the `->`/`->>` operators, `json_insert`/`json_replace`/`json_set`,
  `json_object`/`json_patch`/`json_pretty`/`json_quote`/`json_remove`/`json_type`/`json_valid`/
  `json_error_position`, the `jsonb_*` variants, and the `json_group_array`/`json_group_object`
  aggregates.
* **Aggregate:** `median`, `percentile`/`percentile_cont`/`percentile_disc`, `total`, and (from JSON1)
  `json_group_array`/`json_group_object`.
* **Table-valued:** `json_each`/`jsonb_each`, `json_tree`/`jsonb_tree`.

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

**Missing:**

* **String:** `lowerUTF8`/`upperUTF8`, `concat`/`concatWithSeparator`, `substring`/`substringUTF8`
  (≈`Substring`), `reverse`/`reverseUTF8`, `trimLeft`/`trimRight`/`trimBoth`, `left`/`right`,
  `leftPad`/`rightPad`, `repeat`, `format`/`printf`, `overlay`, `translate`, `replaceOne`/`replaceAll`,
  `replaceRegexpOne`/`replaceRegexpAll`, `match`/`matchCaseInsensitive`, `extract`/`extractAll`,
  `position`/`positionUTF8`, `splitByString`/`splitByRegexp`/`splitByWhitespace`, `initcap`, `soundex`,
  `base64Encode`/`base64Decode`, `hex`/`unhex`, `bit_length`, `editDistance`, `jaroSimilarity`,
  `byteHammingDistance`, `normalizeUTF8*`, `substringIndex`, `countSubstrings`, `extractGroups`.
* **Numeric / math:** `acos`, `asin`, `atan`, `atan2`, `cos`, `sin`, `tan`, `exp`, `log`, `log2`,
  `log10`, `sqrt`, `cbrt`, `pow` (several are reachable through `Math.*`), `erf`, `erfc`, `lgamma`,
  `tgamma`, `gcd`, `lcm`, `e`, `pi`, `isFinite`/`isInfinite`/`isNaN`, `exp10`/`exp2`, `hypot`, `intDiv`,
  `intExp2`/`intExp10`, `plus`/`minus`/`multiply`/`divide`/`negate` (operator forms), `sigmoid`, `sqr`.
* **Rounding:** `roundAge`, `roundBankers`, `roundDown`, `roundDuration`, `roundToExp2` (`round`/`ceil`/
  `floor`/`trunc` are reachable through `Math.*`).
* **Date/time:** `formatDateTime`(+`InJodaSyntax`), `parseDateTime*`, `makeDate`/`makeDate32`/
  `makeDateTime`(+`64`), `now`/`now64`/`nowInBlock`, `today`/`yesterday`, `timeSlot`/`timeSlots`,
  `toStartOfFifteenMinutes`/`toStartOfFiveMinutes`/`toStartOfTenMinutes`, `toStartOfInterval`,
  `toISOYear`/`toWeek`/`toYearWeek`/`toLastDayOfWeek`, `toDaysInMonth`, `fromUnixTimestamp*`,
  `fromUTCTimestamp`, `toModifiedJulianDay`, `age`, `dateName`, `serverTimezone`, `timezoneOf`/
  `timezoneOffset`, `toTime`/`toTime64`, `timestamp`.
* **Array:** `arrayConcat`, `arrayElement`, `arrayFlatten`, `arrayUniq`, `arrayEnumerateDense`
  (`Ranked`), `arrayZip`, `arrayIntersect`/`arrayUnion`/`arrayExcept`/`arraySymmetricDifference`,
  `arrayDifference`, `arrayAvg`/`arraySum`/`arrayMin`/`arrayMax`/`arrayProduct`,
  `arrayTopK`/`arrayBottomK`, `arrayRandomSample`, `arrayCompact`, `arrayPopBack`/`arrayPopFront`/
  `arrayPushFront`, `arrayResize`, `arrayRemove`, `arrayRotateLeft`/`arrayRotateRight`/`arrayShiftLeft`/
  `arrayShiftRight`, `arrayReduce`, `arrayFold`, `arrayFill`/`arrayReverseFill`, `arraySplit`/
  `arrayReverseSplit`, `arrayReverseSort`/`arrayPartialSort`, `arrayShingles`, `arrayTranspose`,
  `countEqual`, `arrayWithConstant`, `arrayJaccardIndex`/`arraySimilarity`.
* **Tuple:** `tupleConcat`, `tupleNames`, `untuple`, `flattenTuple`, `tupleToNameValuePairs`,
  `tuple+`/`tuple-`/`tuple*`/`tuple/` and the `tupleHammingDistance`/`dotProduct` helpers (the portable
  row-value surface covers `tuple(...)` and `tupleElement`).
* **Map:** the entire family: `map`, `mapKeys`/`mapValues`, `mapContainsKey`/`mapContainsValue`,
  `mapAdd`/`mapSubtract`/`mapUpdate`/`mapConcat`, `mapFilter`/`mapApply`/`mapAll`/`mapExists`,
  `mapSort`/`mapReverseSort`/`mapPartialSort`, `mapFromArrays`, `mapPopulateSeries`,
  `extractKeyValuePairs`.
* **Higher-order:** `arrayFold`, `arrayCumSumNonNegative`, `arrayFill`/`arrayReverseFill`,
  `arraySort`/`arrayReverseSort`/`arrayPartialSort`, `arraySplit`/`arrayReverseSplit`,
  `arraySum`/`arrayMin`/`arrayMax`/`arrayAvg`/`arrayProduct`, `arrayReduce`/`arrayReduceInRanges`,
  `arrayTopK`/`arrayBottomK`, `mapApply`/`mapFilter`/`mapAll`/`mapExists`/`mapSort`.
* **JSON:** `JSONMergePatch`, `JSONArrayLength`, `JSONDynamicPaths`(`WithTypes`), `JSONSharedDataPaths`
  (`WithTypes`), `JSONKey`, `JSONExtractKeysAndValuesRaw`, `JSONExtractUInt`/`...OrDefault`,
  `simpleJSONExtract*`, `prettyPrintJSON`, `isValidJSON`, `toJSONString`, `toJSONString`.
* **Hash:** the entire hash family: `MD5`, `SHA1`/`SHA224`/`SHA256`/`SHA384`/`SHA512`/`SHA512_256`,
  `BLAKE3`, `MD4`, `RIPEMD160`, `keccak256`, `xxHash32`/`xxHash64`/`xxh3`, `sipHash*`, `cityHash64`,
  `farmHash64`, `metroHash64`, `murmurHash*`, `javaHash`, `hiveHash`, `intHash*`, `halfMD5`,
  `URLHash`, `wyHash64`, and the `ngramMinHash`/`ngramSimHash`/`wordShingle*` families.
* **Encoding:** `bin`/`unbin`, `hex`/`unhex`, `base32*`/`base58*`/`base64*`(+`URL`), `bech32*`,
  `bitmaskToArray`/`bitmaskToList`/`bitPositionsToArray`, `char`, `sqidEncode`/`sqidDecode`,
  `hilbertEncode`/`hilbertDecode`, `mortonEncode`/`mortonDecode`.
* **UUID / ULID:** `UUIDNumToString`, `UUIDStringToNum`, `UUIDToNum`, `UUIDv7ToDateTime`, `toUUID`,
  `toUUIDOrDefault`/`toUUIDOrNull`, `generateULID`, `ULIDStringToDateTime`, the snowflake family
  (`generateSnowflakeID`, `dateTimeToSnowflakeID`, `dateTime64ToSnowflakeID`, `snowflakeIDToDateTime*`),
  `dateTimeToUUIDv7`.
* **URL / IP:** the entire family (`domain`, `protocol`, `path`, `queryString`, `fragment`, `port`,
  `extractURLParameter*`, `cutURLParameter`, `cutToFirstSignificantSubdomain*`, `topLevelDomain`,
  `encodeURLComponent`, `IPv4NumToString`, `IPv6NumToString`, `toIPv4`/`toIPv6`, `isIPv4String`/
  `isIPv6String`, `isIPAddressInRange`, `IPv4CIDRToRange`/`IPv6CIDRToRange`, ...).
* **Bit:** the entire family: `bitAnd`/`bitOr`/`bitXor`/`bitNot`, `bitShiftLeft`/`bitShiftRight`,
  `bitRotateLeft`/`bitRotateRight`, `bitCount`, `bitTest`/`bitTestAll`/`bitTestAny`,
  `bitHammingDistance`.
* **Type conversion:** `toInt*`/`toUInt*`/`toFloat*`/`toDecimal*`/`toString`/`toBool`/`toFixedString`/
  `toLowCardinality`/`toInterval*`/`accurateCast*`/`reinterpretAs*`/`parseDateTime*` (numeric/date casts
  are reachable through the CLR cast + `MakeTypeName`).
* **Aggregate:** `sum`/`avg`/`count`/`min`/`max`/`stddev`/`var`/`corr`/`covar` are portable;
  `groupArray`/`groupUniqArray`/`uniq*`/`quantile*`/`median`/`topK`/`argMin`/`argMax`/`anyLast` are
  implemented. Missing: `groupBitmap`(`And`/`Or`/`Xor`), `groupBitAnd`/`groupBitOr`/`groupBitXor`,
  `sumMap`/`sumMapFiltered`, `entropy`, `first_value`/`last_value`, `maxIntersections`(`Position`),
  `simpleLinearRegression`/`stochasticLinearRegression`/`stochasticLogisticRegression`,
  `deltaSum`/`deltaSumTimestamp`, `exponentialMovingAverage`/`exponentialTimeDecayed*`,
  `gini`, `skewPop`/`skewSamp`, `kurtPop`/`kurtSamp`, `rankCorr`, `contingency`/`cramersV`/`cramersVBiasCorrected`/
  `theilsU`, `studentTTest*`/`welchTTest`/`mannWhitneyUTest`/`kolmogorovSmirnovTest`/`meanZTest`/
  `analysisOfVariance`, `quantileTDigest`/`quantileGK`/`quantileDD`/`quantileBFloat16`/`quantilePrometheusHistogram`
  and the `quantiles*` plural family (only the plain `quantile`/`quantiles`/`quantileExact`/`quantileTiming`
  are wrapped), `approx_top_k`/`approx_top_sum`, `any`/`anyHeavy`, `maxMap`/`minMap`, `sumCount`,
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
  `input`, `values`, `view`, `format`, `null`, `executable`, `primes`, `merge`, `dictionary`, `eval`,
  `loop`, `fuzzJSON`, `fuzzQuery`, `timeSeries*` — **mostly out of scope** (external engines).
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
