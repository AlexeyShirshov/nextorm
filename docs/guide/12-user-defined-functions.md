# User-defined functions

> Call a database scalar function from a LINQ expression by mapping a placeholder CLR method with
> `[SqlFunction]`.

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Querying and projections](01-querying-and-projections.md) · [Scalar functions](11-scalar-functions.md)

## Overview

`SqlFunctionAttribute` maps a CLR method to a database scalar function. Apply it to a placeholder
method, or to the declaring type to map every method of that type by name:

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SqlFunctionAttribute : Attribute
{
    public SqlFunctionAttribute();
    public SqlFunctionAttribute(string name);
    public string? Name { get; set; }     // defaults to the CLR method name
    public string? Schema { get; set; }   // optional schema/owner prefix
}
```

The placeholder body is **never executed** - it only has to satisfy the compiler, so
`=> throw new NotSupportedException()` or `=> default!` is enough. The call is translated to
`[schema.]name(arg1, arg2, ...)`:

* `Name` overrides the emitted function name; without it the CLR method name is used (this is also the
  behaviour when the attribute is on the declaring type).
* `Schema` prefixes the name as `schema.name`.
* arguments are rendered through the regular expression visitor, so a **captured value becomes a
  parameter** exactly like anywhere else;
* for an **instance** method the target (`node.Object`) is emitted as the **first argument**, before the
  method arguments.

The built-in `string`/`Math`/`DateTime` translations run first, so `[SqlFunction]` cannot change their
behaviour. nextorm only emits the call; the function must already exist in the target database.

## Mapping a static method

```csharp
private static class Udf
{
    [SqlFunction("upper")]
    public static string ToUpper(string value) => throw new NotSupportedException();

    [SqlFunction("abs")]
    public static long Abs(long value) => throw new NotSupportedException();

    [SqlFunction("coalesce")]
    public static int? Coalesce(int? value, int? fallback) => throw new NotSupportedException();
}

var value = dataContext.From<IComplexEntity>()
    .Where(e => e.Id == 2)
    .Select(e => Udf.ToUpper(e.String!))
    .First();
// "XXX"
```

```sql
-- SQLite
select upper(somestring) as 'V' from complex_entity
-- SQL Server
select upper(somestring) as [V] from complex_entity
-- PostgreSQL
select upper(somestring) as "V" from complex_entity
```

## Name and schema

`Name` overrides the function name and `Schema` qualifies it:

```csharp
private static class Udf
{
    [SqlFunction("my_fn", Schema = "dbo")]
    public static long WithSchema(long value) => throw new NotSupportedException();
}

var value = dataContext.From<IComplexEntity>()
    .Select(e => new { V = Udf.WithSchema(e.Id) })
    .First();
```

```sql
select dbo.my_fn(id) as 'V' from complex_entity
```

## Attribute on the declaring type

When the attribute is placed on the type, every method is mapped by its CLR name (no per-method
attribute is needed):

```csharp
[SqlFunction]
private static class ImplicitUdf
{
    public static string Lower(string value) => throw new NotSupportedException();
}

var value = dataContext.From<IComplexEntity>()
    .Select(e => new { V = ImplicitUdf.Lower(e.String!) })
    .First();
```

```sql
select Lower(somestring) as 'V' from complex_entity
```

## Captured argument becomes a parameter

A captured local is parameterised, and the parameter-extraction pass carries it through:

```csharp
var start = 1;

var prepared = dataContext.From<IComplexEntity>()
    .Select(e => new { V = Udf.Slice(e.String!, start) })
    .Prepare();
```

```sql
-- SQLite placeholder; SQL Server/PostgreSQL use @start
select substr(somestring, $start) as 'V' from complex_entity
```

with the mapped method:

```csharp
[SqlFunction("substr")]
public static string Slice(string value, int start) => throw new NotSupportedException();
```

## Instance methods

For an instance method the receiver is the first SQL argument:

```csharp
public sealed class Formatter
{
    [SqlFunction("format")]
    public string Format(string value) => throw new NotSupportedException();
}
```

A query using `new Formatter().Format(e.String!)` is rendered as `format(<receiver>, somestring)`.
This is implemented in `BaseExpressionVisitor.TryTranslateSqlFunction` (`node.Object` is emitted before
the method arguments).

## Provider differences

The function name and schema are emitted **verbatim** by every provider - none of the built-in dialects
quotes or remaps them - so the same `[SqlFunction]` text must name an existing object in each target
database. Only the surrounding identifier (column alias) quoting differs:

| Provider | Behaviour |
|---|---|
| SQLite | `[schema.]name(args)` verbatim; aliases single-quoted (`as 'V'`). |
| SQL Server | `[schema.]name(args)` verbatim; aliases bracket-quoted (`as [V]`). |
| PostgreSQL | `[schema.]name(args)` verbatim; aliases double-quoted (`as "V"`). |
| In-memory | The attribute is a SQL translation; the in-memory provider evaluates the CLR method instead. |

## See also

* [Scalar functions](11-scalar-functions.md) - the built-in translations that take precedence.
* [Table-valued functions](13-table-valued-functions.md) - the `FROM`-source equivalent.
* [Provider overview](../providers/overview.md) - quoting and capability flags.

---

Source: `src/nextorm.core/SqlFunctionAttribute.cs:14`, `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:563`;
`test/nextorm.integration.tests/CommonTestSuite.Udf.cs:14`, `:24`, `:36`;
generated SQL: `test/nextorm.sqlite.tests/SqlGenerationTests.cs:713`, `:722`, `:737`, `:746`;
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:466`, `:476`, `:490`;
`test/nextorm.postgres.tests/SqlGenerationTests.cs:399`, `:409`, `:423`.
