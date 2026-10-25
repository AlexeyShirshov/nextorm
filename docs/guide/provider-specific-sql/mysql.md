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

## MariaDB

`nextorm.mariadb` derives `MariaDbDialect` from `MySqlDialect`, so it inherits the whole surface above.
The only exceptions are `uuid_to_bin`/`bin_to_uuid` and `json_storage_free`: MariaDB has no such
functions (`uuid_to_bin`/`bin_to_uuid` convert through `CAST(... AS BINARY(16))`/`CAST(... AS UUID)`,
and MariaDB does not implement MySQL's binary-JSON freed-space accounting), so
`MariaDbDialect.MySqlFunctions` reports them as unsupported and the query is rejected with
`NotSupportedException`.

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
| `months_between(a, b)` | `months_between(...)` | MariaDB 12.2+; fractional months |
| `to_char(value, format)` | `to_char(...)` | MariaDB 10.6+; Oracle-style format mask |
| `to_date(value, format)` | `to_date(...)` | MariaDB 12.3+; Oracle-style parse |
| `to_number(value, format)` | `to_number(...)` | MariaDB 12.2+; returns `DOUBLE` |
| `kdf(password, salt, info, kdfName)` | `kdf(...)` | MariaDB 11.3+; key derivation, returns binary |
| `xxh3(value)` / `xxh32(value)` | same | MariaDB 13.1+; fast 64/32-bit xxHash |
| `json_detailed(json)` / `json_compact(json)` | same | re-indent / strip whitespace |
| `json_equals(json1, json2)` | `json_equals(...)` | `1` when the JSON documents are equal (object key order does not matter), `0` when they differ, `null` for SQL NULL; MariaDB 11.4 baseline, MySQL rejects it |
| `json_normalize(json)` | `json_normalize(...)` | canonical JSON text (sorted object keys, no unnecessary whitespace, idempotent), `null` for SQL NULL; MariaDB 11.4 baseline, MySQL rejects it |

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
