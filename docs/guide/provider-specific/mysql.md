# MySQL and MariaDB-specific SQL

> `nextorm.mysql` (and `nextorm.mariadb`, which derives from it) expose the native MySQL/MariaDB
> string/conditional idioms, the `%`-templated date conversion and Unix-epoch functions, the
> hexadecimal hashes, the IPv4 conversion pair, the JSON mutation family and the binary UUID pair on
> [`SqlFunctions.MySql`](xref:NextORM.Core.SqlFunctions.MySql). The surface is gated per name by
> [`ISqlDialect.MySqlFunctions`](xref:NextORM.Core.ISqlDialect.MySqlFunctions); other providers reject a
> call with `NotSupportedException`.

**Prerequisites:** [Scalar functions](../11-scalar-functions.md) · [MySQL provider](../../providers/mysql.md) · [MariaDB provider](../../providers/mariadb.md)

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
| `uuid_to_bin(uuid)` / `bin_to_uuid(binary)` | same | **MySQL only** (see below) |

JSON mutation methods take a single `path`/`value` pair; MySQL accepts further pairs but the portable
surface keeps one pair per call, and the value is a string (MySQL treats a string argument as a JSON
string unless it is valid JSON).

```csharp
var updated = dataContext.From<IComplexEntity>()
    .Select(x => SqlFunctions.MySql.json_set(x.Payload, "$.active", "true"))
    .First();
```

## MariaDB

`nextorm.mariadb` derives `MariaDbDialect` from `MySqlDialect`, so it inherits the whole surface above.
The only exceptions are `uuid_to_bin`/`bin_to_uuid`: MariaDB has no such functions (it converts
through `CAST(... AS BINARY(16))`/`CAST(... AS UUID)`), so `MariaDbDialect.MySqlFunctions` reports them
as unsupported and the query is rejected with `NotSupportedException`.

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
| `next_value_for(sequence)` | `next value for sequence` | ANSI sequence syntax |
| `nextval(sequence)` | `nextval(sequence)` | PostgreSQL-style sequence access |
| `setval(sequence, value)` | `setval(sequence, value)` | sets the next sequence value |
| `lastval(sequence)` | `lastval(sequence)` | the connection's last sequence value |

The sequence members take the sequence name as a string and emit it as an identifier (not a quoted
literal), so `SqlFunctions.MySql.next_value_for("order_seq")` renders `next value for order_seq`.

```csharp
var rows = dataContext.From<IOrderEntity>()
    .Select(x => new
    {
        Id = SqlFunctions.MySql.nextval("order_seq"),
        Label = SqlFunctions.MySql.nvl(x.Label, "unnamed"),
        Month = SqlFunctions.MySql.add_months(x.CreatedAt, 1),
        Pretty = SqlFunctions.MySql.json_detailed(x.Payload)
    })
    .ToList();
```

## In-memory and other providers

The in-memory provider cannot evaluate these native functions and rejects them with
`NotSupportedException`; PostgreSQL, SQL Server, SQLite and ClickHouse reject the whole
`SqlFunctions.MySql` surface the same way. Cross-provider functions with matching semantics live on
[`SqlFunctions.Sql`](../11-scalar-functions.md); the provider-exclusive names stay here.
