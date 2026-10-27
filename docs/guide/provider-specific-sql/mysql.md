# MySQL and MariaDB-specific SQL

> `nextorm.mysql` (and `nextorm.mariadb`, which derives from it) expose the native MySQL/MariaDB
> string/conditional idioms, the `%`-templated date conversion and Unix-epoch functions, the
> hexadecimal hashes, the IPv4 conversion pair, the JSON mutation family and the binary UUID pair on
> [`SqlFunctions.MySql`](xref:NextORM.Core.SqlFunctions.MySql). The surface is gated per name by
> [`ISqlDialect.MySqlFunctions`](xref:NextORM.Core.ISqlDialect.MySqlFunctions); other providers reject a
> call with `NotSupportedException`.

**Prerequisites:** [Scalar functions](../../scalar-functions/index.md) · [MySQL provider](../../connecting-providers/mysql.md) · [MariaDB provider](../../connecting-providers/mariadb.md)

## Native functions

Every member of [`MySqlFunctions`](xref:NextORM.Core.MySqlFunctions) is called through
`SqlFunctions.MySql.*` inside a query expression, exactly like `SqlFunctions.Sql.*`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(x => SqlFunctions.MySql.find_in_set(x.Status, "new,open") > 0)
    .Select(x => new
    {
        Position = SqlFunctions.MySql.field(x.Status, "new", "open", "closed"),
        Part = SqlFunctions.MySql.substring_index(x.Path, "/", 2),
        Price = SqlFunctions.MySql.format(x.Price, 2),
        Hash = SqlFunctions.MySql.md5(x.Name),
        Updated = SqlFunctions.MySql.from_unixtime(x.UpdatedAtUnix)
    })
    .ToList();
```

```sql
select field(status, 'new', 'open', 'closed'), substring_index(path, '/', 2),
       format(price, 2), md5(name), from_unixtime(updated_at_unix)
 from complex_entity
where find_in_set(status, 'new,open') > 0
```

| Member | Renders | Notes |
|---|---|---|
| `find_in_set(value, set)` | `find_in_set(value, set)` | 1-based position in a comma-separated list, `0` when absent |
| `field(value, values...)` | `field(value, v1, ...)` | 1-based index of `value`, `0` when absent |
| `elt(index, values...)` | `elt(index, v1, ...)` | 1-based element access |
| `substring_index(value, delimiter, count)` | `substring_index(...)` | counts from the right when `count` is negative |
| `format(value, decimals)` | `format(value, decimals)` | numeric grouping + fixed decimals |
| `str_to_date(value, format)` | `str_to_date(...)` | `%`-templated parse |
| `date_format(value, format)` | `date_format(...)` | `%`-templated format |
| `from_unixtime(seconds)` | `from_unixtime(...)` | Unix epoch to date/time |
| `unix_timestamp(value)` | `unix_timestamp(...)` | date/time to Unix epoch seconds |
| `md5(value)` / `sha1(value)` / `sha2(value, bits)` | same | hexadecimal digests |
| `inet_aton(text)` / `inet_ntoa(number)` | same | dotted-quad IPv4 to/from its numeric form |
| `json_set` / `json_insert` / `json_replace` | same | path-based JSON mutation |
| `json_remove(json, path)` | `json_remove(...)` | deletes the value at the path |
| `json_merge_patch` / `json_merge_preserve` | same | RFC 7396 merge-patch / duplicate-preserving merge |
| `json_array_append` / `json_array_insert` | same | append to / insert into the array at the path |
| `json_depth` / `json_keys` / `json_length` / `json_type` | same | JSON introspection |
| `json_storage_free(json)` | `json_storage_free(...)` | **MySQL only** — bytes freed in the binary `JSON` column value by an in-place `JSON_SET`/`JSON_REPLACE`/`JSON_REMOVE`; `0` when nothing was freed, `null` for SQL NULL |
| `uuid_to_bin(uuid)` / `bin_to_uuid(binary)` | same | **MySQL only** (see below) |

JSON mutation methods take a single `path`/`value` pair; MySQL accepts further pairs but the portable
surface keeps one pair per call, and the value is a string (MySQL treats a string argument as a JSON
string unless it is valid JSON).

```csharp
var updated = dataContext.From<IComplexEntity>()
    .Select(x => SqlFunctions.MySql.json_set(x.Payload, "$.active", "true"))
    .First();
```

`json_storage_free` is a diagnostic for MySQL's binary JSON storage: after a partial update it reports
how many bytes the in-place change freed, so the number describes the stored representation, not the
length of the JSON text. It is `0` for literals, captured values and columns whose update freed nothing,
and `null` (never `0`) when the argument is SQL NULL. Only MySQL implements it.

### String

Thirteen string and binary members: `bin`, `oct`, `hex`, `make_set`, `export_set`, `ord`, `quote`,
`soundex`, `strcmp`, the Base64 pair `to_base64`/`from_base64`, `unhex` and `weight_string`. `hex`
accepts binary data, text and a 64-bit integer; `weight_string` supports only the `AS CHAR(N)` /
`AS BINARY(N)` modifiers and both must be compile-time constants; `export_set` refuses a constant
`count` outside `[0, 64]`.

### Numeric

Five numeric members: `conv` (bases 2–36, `-1` for a signed interpretation), `crc32` (widened to
`long`), `log2`, `rand` (a `double` in `[0, 1)`, a seed repeats the sequence) and `truncate`
(truncation toward zero, a negative count zeroes the integer part).

### Bit

One bit member: `bit_count(value)` counts the set bits in the 64-bit representation of a binary
value. The three bit aggregates live under [Aggregate](#aggregate).

### Date and time

Twenty-three temporal members: `adddate`/`subdate` (numeric-days form only), `addtime`/`subtime`,
`convert_tz` (IANA zones), `dayname`/`monthname`, `from_days`/`to_days`/`to_seconds`, `get_format`,
`makedate`/`maketime`, `microsecond`, `period_add`/`period_diff`, `sec_to_time`, `time`, `time_format`,
`time_to_sec`, `timediff`, `timestamp` and `yearweek`. `get_format` takes a typed `MySqlFormatKind`
plus a literal format name (`USA`, `JIS`, `ISO`, `EUR`, `INTERNAL`); `time_to_sec` returns whole
seconds because MySQL 8.4 discards the argument's sub-second precision.

### JSON

Seventeen JSON members plus the two path operators: `json_array`, `json_arrayagg`, `json_contains`,
`json_contains_path`, `json_merge`, `json_object`, `json_objectagg`, `json_overlaps`, `json_pretty`,
`json_quote`, `json_schema_valid`, `json_schema_validation_report`, `json_search`, `json_storage_size`,
`member_of`, `json_arrow` (`column -> 'path'`) and `json_arrow_text` (`column ->> 'path'`). The
operators need a mapped JSON column on the left and a compile-time path literal on the right;
`json_object` needs an even argument count with string keys; `json_contains_path` and `json_search`
need the literal `'one'` or `'all'` plus literal paths, and the `json_search` escape is a single
character; `member_of` needs a literal JSON array. `json_storage_free` is documented above and
[`JSON_TABLE`](#guards-and-refusals) is refused.

### Aggregate

Three aggregates over the group: `bit_and`, `bit_or` and `bit_xor`, each returning `null` for an empty
group and ignoring SQL NULL inputs.

### Encryption, compression and hashing

Nine members: `aes_encrypt`/`aes_decrypt` (MySQL 8.4 defaults to `aes-128-ecb`), the `compress` pair
`compress`/`uncompress` plus `uncompressed_length`, `random_bytes`, `validate_password_strength`,
`statement_digest` and `statement_digest_text`. `validate_password_strength` only grades meaningfully
when the server's `validate_password` component is configured.

### Information

Nine session-information members: `found_rows`, `last_insert_id`, `row_count`, `current_role`,
`charset`, `collation`, `coercibility`, `benchmark` and `icu_version`. The connection id is **not**
duplicated here: it stays the cross-provider `CommonFunctions.connection_id()`, rendered as
`CAST(CONNECTION_ID() AS UNSIGNED)` — see
[Session and server information](../../scalar-functions/09-system-functions.md#session-and-server-information-cross-provider).

### Miscellaneous

Six members: `uuid_short`, `is_uuid`, `sleep`, `grouping`, `name_const` and `column_default`.
`grouping` returns the rollup bitmap and every argument must match a `GROUP BY` expression — a caller
obligation the translator cannot check; `name_const` requires a compile-time literal label;
`column_default` requires a column reference, so constants, parameters, casts and computed expressions
are refused.

Every member, its SQL spelling and its result shape are listed in the
[provider mapping table](../../scalar-functions/08-reference.md#mysql-specific-functions).

## Guards and refusals

Every guard and every refusal is raised during preparation, **before any SQL is emitted**, and each is
covered by a test with a positive sibling control on the same provider (neighbouring names keep
rendering after the refusal). The guards are:

* `column_default` requires a column reference; constants, parameters, casts and computed expressions
  are refused.
* `->` and `->>` require a JSON column on the left and a compile-time path literal on the right.
* `json_object` requires an even argument count with string keys.
* `json_contains_path` and `json_search` require the literal `'one'` or `'all'` and literal paths.
* `json_search` takes a single-character escape.
* `get_format` takes a typed kind plus a literal format name; `weight_string` only the
  `AS CHAR(N)`/`AS BINARY(N)` modifiers; `name_const` a literal name; `member_of` a literal JSON
  array.
* A constant `export_set` count outside `[0, 64]` is refused.
* `time_to_sec` returns whole seconds — MySQL 8.4 discards the sub-second part.

Three vendor names are refused outright; each member exists only to carry the refusal and throws
`NotSupportedException` with exactly this text:

* `json_table()` — `MySQL JSON_TABLE is a table-producing construct that requires a FROM/join position and a COLUMNS definition list; it is not a scalar function and cannot be expressed on SqlFunctions.MySql.`
* `uuid()` — `uuid is refused: MySQL UUID() generates UUID version 1; this API's UUID contract is v4 (gen_random_uuid) or v7 (uuidv7), and a v1 value is never substituted for it. Use uuidv7() to generate an identifier, or UUID() through raw SQL when v1 is genuinely intended.`
* `values(column)` — `MySQL VALUES(column) cannot be translated as a scalar query function; use the mutation builder's upsert API.`

`JSON_TABLE` is a table-producing construct, not a scalar: it needs a `FROM`/join position and a
`COLUMNS` definition list, so it is reachable only through raw SQL or a `[SqlFunction]` wrapper. The
same holds for the MySQL vendor surface as a whole — the refusals are deterministic translator
outcomes, not gaps left for a later slice.

## MariaDB

`nextorm.mariadb` derives `MariaDbDialect` from `MySqlDialect`, so it inherits the whole surface above.
The three names it has never supported stay excluded: `uuid_to_bin`/`bin_to_uuid` and
`json_storage_free`. MariaDB has no such functions (`uuid_to_bin`/`bin_to_uuid` convert through
`CAST(... AS BINARY(16))`/`CAST(... AS UUID)`, and MariaDB does not implement MySQL's binary-JSON
freed-space accounting), so `MariaDbDialect.MySqlFunctions` reports them as unsupported and the query
is rejected with `NotSupportedException`.

The names added by this slice are delivered against **MySQL 8.4 only**. `MariaDbNativeFunctions` keeps
every one of them in its exclusion list, so MariaDB refuses each name explicitly instead of rendering
SQL whose behaviour this unit cannot claim. MariaDB verification is owned by
[#284](https://github.com/AlexeyShirshov/nextorm/issues/284), which will revisit the exclusions name by
name; the three exclusions above and all MariaDB-only/version-gated arms below are unchanged.

On top of the inherited surface, MariaDB adds its own names through the same `SqlFunctions.MySql`
entry point. MySQL's dialect does not report them, so a MySQL query using one of them is rejected with
`NotSupportedException`:

| Member | Renders | Notes |
|---|---|---|
| `regexp_instr(value, pattern)` | `regexp_instr(...)` | 1-based position of the first regex match, `0` when absent |
| `regexp_substr(value, pattern)` | `regexp_substr(...)` | the matching substring, empty when absent |
| `regexp_replace(value, pattern, replacement)` | `regexp_replace(...)` | replaces every regex match |
| `nvl(value, fallback)` | `nvl(...)` | `IFNULL` synonym |
| `nvl2(value, whenNotNull, whenNull)` | `nvl2(...)` | picks by the null-ness of `value` |
| `add_months(date, months)` | `add_months(...)` | MariaDB 10.6.1+; clamps to the month's last day |
| `months_between(a, b)` | `months_between(...)` | MariaDB 12.2.1+ (the enforced minimum; 12.2 and 12.2.0 refuse); fractional months |
| `to_char(value, format)` | `to_char(...)` | MariaDB 10.6+; Oracle-style format mask |
| `to_date(value, format)` | `to_date(...)` | MariaDB 12.3+; Oracle-style parse |
| `to_number(value, format)` | `to_number(...)` | MariaDB 12.2 family, enforced minimum 12.2.1; returns `DOUBLE` |
| `kdf(password, salt, info, kdfName)` | `kdf(...)` | MariaDB 11.3+; key derivation, returns binary |
| `xxh3(value)` / `xxh32(value)` | same | MariaDB 13.1+; fast 64/32-bit xxHash |
| `json_detailed(json)` / `json_compact(json)` | same | re-indent / strip whitespace |
| `json_equals(json1, json2)` | `json_equals(...)` | `1` when the JSON documents are equal (object key order does not matter), `0` when they differ, `null` for SQL NULL; MariaDB 11.4 baseline, MySQL rejects it |
| `json_normalize(json)` | `json_normalize(...)` | canonical JSON text (sorted object keys, no unnecessary whitespace, idempotent), `null` for SQL NULL; MariaDB 11.4 baseline, MySQL rejects it |

These three Oracle-compatible conversions (`months_between`, `to_date`, `to_number`) are **version-gated**:
MariaDB has none of them on 11.4, so `MariaDbDialect` emits each one only when the context was built with an
explicit server version at or above its minimum. An **unversioned** MariaDB context (the default two-argument
constructor) refuses the call with `NotSupportedException` during preparation instead of emitting SQL the
server rejects; the platform equivalents (`TIMESTAMPDIFF`, `STR_TO_DATE`, `CAST(... AS DECIMAL)`) are never
substituted. The UUID generators follow the same rule with a MariaDB 11.7 minimum (see
[Sequences and UUID generators](../../scalar-functions/10-sequences.md)).

Sequence access is provider-specific; see the common
[Sequences and UUID generators](../../scalar-functions/10-sequences.md) page.

```csharp
var rows = dataContext.From<IOrderEntity>()
    .Select(x => new
    {
        Label = SqlFunctions.MySql.nvl(x.Label, "unnamed"),
        Month = SqlFunctions.MySql.add_months(x.CreatedAt, 1),
        Pretty = SqlFunctions.MySql.json_detailed(x.Payload)
    })
    .ToList();
```

## Null-safe comparison (MySQL and MariaDB)

Both providers have the null-safe equality operator `<=>`: `SqlFunctions.Sql.is_not_distinct_from(a, b)`
renders `(a <=> b)` and `is_distinct_from(a, b)` renders its complement `(not (a <=> b))`. MariaDB
inherits the same rendering from `MySqlDialect`; both forms are two-valued (1/0). Ordinary `==`/`!=`
keep their SQL NULL semantics. See
[Null-safe comparison](../../scalar-functions/04-conditionals-and-conversion.md#null-safe-comparison-is-not-distinct-from).

## In-memory and other providers

The in-memory provider cannot evaluate these native functions and rejects them with
`NotSupportedException`; PostgreSQL, SQL Server, SQLite and ClickHouse reject the whole
`SqlFunctions.MySql` surface the same way. Cross-provider functions with matching semantics live on
[`SqlFunctions.Sql`](../../scalar-functions/index.md); the provider-exclusive names stay here.
