# Scalar functions

> Translate `string`, `Math` and `DateTime` members, `??` coalescing, boolean predicates and numeric
> conversions into provider-specific SQL.

**Prerequisites:** [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Querying and projections](../guide/01-querying-and-projections.md) · [Filtering (WHERE)](../guide/02-filtering-where.md)

## Overview

nextorm recognises a fixed set of CLR members and rewrites them to SQL inside any query expression.
The dispatch lives in [`BaseExpressionVisitor`](xref:NextORM.Core.BaseExpressionVisitor): `string` methods, `Math` methods, `DateTime` members,
`SqlFunctions.Sql.like`, the `??` operator and numeric conversions. Everything provider-specific is delegated to
[`ISqlDialect`](xref:NextORM.Core.ISqlDialect), so the same C# code renders the correct function on every provider.

Two rules apply throughout:

* a **captured** value (a local or parameter) becomes a query **parameter**, not a literal;
* a **constant** is inlined. For `Contains`/`StartsWith`/`EndsWith` that also means `%`, `_` and `\` in a
  constant are escaped and an `escape '\'` clause is emitted.

The built-in translations are attempted **before** any [`[SqlFunction]`](../guide/12-user-defined-functions.md)
mapping, so a user-defined attribute cannot change the behaviour of `string`/`Math`/`DateTime` members.

Cross-provider helpers live on [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql). Functions that only one provider supports are grouped
under a provider-specific surface: [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) (PostgreSQL: native arrays, native JSON, the extended
scalar library, the PostgreSQL-only aggregates and the `generate_series`/`unnest` table functions),
[`SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer) (SQL Server: the JSON-as-text functions and `string_split`/`openjson`) and
[`ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) (ClickHouse: `arg_min`/`arg_max`, the `-If` combinator, the string-JSON
`JSONExtract*` family, the flat-JSON `visitParamExtract*` fast path, the JSONPath scalars
`json_value`/`json_query`/`json_exists` and the dictionary functions
`dict_get`/`dict_get_or_default`/`dict_has`/`dict_get_hierarchy`/`dict_get_children`/`dict_is_in`). Calling one of them on a
provider that does not opt in throws `NotSupportedException`.

## See also

* [Filtering (WHERE)](../guide/02-filtering-where.md) - `Contains`/`in`, `??` and conditional expressions in predicates.
* [Grouping and aggregates](../guide/04-grouping-and-aggregates.md) - aggregate functions (`count`, `sum`, ...).
* [User-defined functions](../guide/12-user-defined-functions.md) - when a scalar function is not built in.
* [Provider overview](../providers/overview.md) - capability flags and quoting.

---

Source: `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:541`, `:1157`, `:1204`, `:1752`;
`src/nextorm.core/Visitors/BuiltinFunctionTranslator.cs`, `src/nextorm.core/Visitors/AggregateFilter.cs`;
`src/nextorm.core/Query/SqlFunctions.cs`;
`src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:57`;
`tests/nextorm.integration.tests/CommonTestSuite.Functions.cs:8`, `:19`, `:43`, `:54`, `:65`, `:76`, `:87`, `:98`, `:117`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:695`, `:775`, `:801`, `:811`, `:832`, `:852`, `:861`, `:872`, `:882`, `:892`, `:912`, `:921`, `:931`, `:940`, `:950`, `:960`, `:974`, `:985`;
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:457`, `:512`, `:522`, `:533`, `:543`, `:552`, `:563`, `:573`, `:583`, `:593`, `:602`, `:613`, `:624`, `:633`, `:643`;
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:390`, `:445`, `:465`, `:475`, `:484`, `:495`, `:505`, `:515`, `:525`, `:534`, `:544`, `:554`, `:563`, `:573`.
