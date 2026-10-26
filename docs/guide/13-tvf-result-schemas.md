# Table-valued function result schemas

> Some table functions do not declare their output columns in the call: the caller supplies the schema.
> Declare it as the mapped row type of the runtime factory and choose where it is rendered.

**Prerequisites:** [Table-valued functions](13-table-valued-functions.md) · [Dynamic columns](29-dynamic-columns.md) · [Raw SQL](14-raw-sql.md#multiple-result-sets)

## Dynamic result schema

A table function whose result shape is supplied by the caller gets it from the factory's row type — the
`T` of `TvfEntityBuilder<T>`. nextorm renders the matching native column list from that type's `[Column]`
metadata and CLR property types, and every provider form is expressed by
[`TableFunctionSchema`](xref:NextORM.Core.TableFunctionSchema) on the descriptor:

```csharp
public TvfEntityBuilder(
    string name,
    string? schema,
    string? withClause,
    string? callClause,
    IReadOnlyList<int> verbatimArguments,
    TableFunctionSchema resultSchema);
```

The rows still materialise into the same `[Column]`-annotated DTO as any other table function — there is
no separate dynamic reader — and the declared schema is part of the query plan key, so two row shapes
never share a cached plan. `None` is the default and renders nothing: the function supplies its own
result shape.

## Where the schema is rendered

| `ResultSchema` | Rendered form | Provider |
|---|---|---|
| `None` | nothing — the function declares its own columns | every provider |
| `LeadingArgument` | a quoted structure string as the first call argument: `values('a UInt8, b String', (1,'x'))` | ClickHouse |
| `AliasColumnList` | an alias column-definition list: `jsonb_to_recordset(@json) as "t1"(a integer, b text)` | PostgreSQL |

ClickHouse `values` reads a literal table from a tuple list; the `tuples` argument is emitted verbatim, so
it is developer-authored SQL only — never build it from user input:

```csharp
public interface IPairRow
{
    [Column("a")]
    byte A { get; set; }
    [Column("b")]
    string? B { get; set; }
}

var rows = dataContext
    .From(() => SqlFunctions.ClickHouse.values<IPairRow>("(1, 'x'), (2, 'y')"))
    .Select(r => new { r.A, r.B })
    .ToList();
```

```sql
select a, b
 from values('a UInt8, b String', (1, 'x'), (2, 'y')) as `t1`
```

PostgreSQL's `jsonb_to_record`/`jsonb_to_recordset` expand a JSON object/array into a record whose columns
are declared by the row type:

```csharp
public interface IPairRow
{
    [Column("a")]
    int A { get; set; }
    [Column("b")]
    string? B { get; set; }
}

using var json = JsonDocument.Parse("""[{"a":1,"b":"x"},{"a":2,"b":"y"}]""");

var rows = dataContext
    .From(() => SqlFunctions.Postgres.jsonb_to_recordset<IPairRow>(json))
    .Select(r => new { r.A, r.B })
    .ToList();
```

```sql
select a, b
 from jsonb_to_recordset(@json) as "t1"(a integer, b text)
```

The column types come from the provider's CLR-to-SQL type mapping (`byte`→`UInt8`, `int`→`integer`,
`string`→`text`/`String`); a nullable value type becomes `Nullable(T)` on ClickHouse.

## Provider restrictions

A provider opts into the specific form it can express
([`SupportsResultSchema(TableFunctionSchema)`](xref:NextORM.Core.ISqlDialect.SupportsResultSchema(NextORM.Core.TableFunctionSchema)));
a declared form the provider does not implement is rejected with `NotSupportedException` — it is never
emitted as SQL without the column list.

| Provider | `None` | `LeadingArgument` | `AliasColumnList` |
|---|---|---|---|
| PostgreSQL | yes | rejected | yes |
| SQL Server | yes | rejected | rejected |
| MySQL | yes | rejected | rejected |
| MariaDB | yes | rejected | rejected |
| SQLite | yes | rejected | rejected |
| ClickHouse | yes | yes | rejected |
| In-memory | rejected (TVF sources are not supported at all) | rejected | rejected |

The refusal names the function and the form:
`The table function '<name>' declares a result schema in the 'LeadingArgument' form, which is not supported
by this provider.` A row type with no mapped properties fails with `BuildSqlCommandException` instead of
emitting an empty column list.

Because PostgreSQL requires the alias column definition list, `jsonb_to_record`/`jsonb_to_recordset`
always need a caller-declared row type; a free/partial column list without one is not supported. For a
schema-less result use a `[DynamicColumns]` dictionary store (see [Dynamic columns](29-dynamic-columns.md)),
the raw `ToDataReader`/`ResultSet` reader (see [Raw SQL](14-raw-sql.md#multiple-result-sets)) or the
key/value expansion functions `jsonb_each`/`jsonb_each_text`/`jsonb_object_keys` (see
[Dynamic record schema](provider-specific-sql/postgresql.md#dynamic-record-schema)).

## See also

* [Table-valued functions](13-table-valued-functions.md) - declaring a runtime factory, arguments, joins and APPLY.
* [ClickHouse](provider-specific-sql/clickhouse.md) - the `values` table function and its structure argument.
* [PostgreSQL](provider-specific-sql/postgresql.md#dynamic-record-schema) - record-producing functions and their column list.
* [Dynamic columns](29-dynamic-columns.md) - reading columns the row type does not declare.

---

Source: `src/nextorm.core/Expressions/TableFunctionSchema.cs`, `src/nextorm.core/DataContext/SqlSourceRenderer.cs` (`RenderResultSchema`), `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`, `src/nextorm.core/Query/SqlFunctions.Postgres.cs`;
provider dialects `src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.clickhouse/ClickHouseDialect.cs`;
tests: `tests/nextorm.<provider>.tests/T320/T320RuntimeTvfSqlTests.cs`, `tests/nextorm.integration.tests/T320/T320ClickHouseTvfTests.cs`.
