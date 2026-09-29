# Querying and projections

> Shape the result of a query with [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})): one column, an anonymous type, a DTO or record, a tuple, a member initialiser, a nested entity or a calculated column.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md)

## Overview

`dataContext.From<TEntity>()` returns an [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1). Every query starts by projecting that
entity with [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})):

```csharp
public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
```

The lambda is not executed - it is translated into the `SELECT` list of the generated statement.
[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) returns a [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1); the terminal ([`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])),
[`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`AnyAsync`](xref:NextORM.Core.EntityBuilderExtensions.AnyAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), ...) executes it. See [Sorting and paging](../guide/04-sorting-and-paging.md)
for the terminals and their async forms. When the projection is a single `byte[]`/`string` column it can instead be streamed with `ToStream`/`ToTextReader` — see [Streaming large objects](../guide/26-large-objects.md).

Rules that apply to every projection:

* The lambda's shape decides the select list. Columns that are not projected are not read.
* A projected member whose name matches the mapped column name (case-insensitive) is emitted without
  an alias. A renamed or calculated member is aliased: `as 'Calc'` on SQLite, `as [Calc]` on SQL
  Server, `as "Calc"` on PostgreSQL.
* Constructor arguments, tuple items and member-initialiser assignments all map to select-list
  entries; the runtime materialiser builds the object from the reader.
* On the in-memory provider no SQL exists; the same expression is compiled and run over the
  in-memory data set.

The `Output:` tables below show the rows returned by each example against the integration-test seed
data: `simple_entity` holds ids `1`-`10` and `complex_entity` three rows
(`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

## See also

* [Filtering (WHERE)](../guide/01-filtering-where.md)
* [Sorting and paging](../guide/04-sorting-and-paging.md)
* [Table-valued functions](../guide/11-table-valued-functions.md)
* [Entities and metadata](../getting-started/03-entities-and-metadata.md)
* [Provider overview](../providers/overview.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:20`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:26`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:45`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:75`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:94`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:103`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:112`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:122`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:352`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:367`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:382`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:727`;
`tests/nextorm.integration.tests/TestModels.cs:3`;
`tests/nextorm.integration.tests/TestModels.cs:12`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:111`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:217`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:242`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:255`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:268`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:293`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:223`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:248`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1117`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1132`.
