# Query pipeline: fluent to SQL

> Follow a query from its fluent description to provider-specific SQL. The builder records query intent in a [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1); `QueryPreparer` turns it into prepared clauses and a `QueryPlan` key, then `SqlBuilder` renders it through the provider dialect. See the [engine overview](index.md) for the whole route, including execution and results.

## The built query

`ctx.From<TEntity>()` is one of the context's builder factories (see [builder entry points](index.md#builder-entry-points)). It returns an [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1). Each fluent operator (`Where`, `Join`, `Select`, `OrderBy`, `GroupBy`, `With`, hints, ...) records part of the query in a [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1). At this point, the command is a description of the query, not a live connection, and nothing has executed.

The other factories start the other statement shapes and return their own builders (`InsertBuilder<T>`, `UpdateBuilder<T>`, `DeleteBuilder<T>`, `MergeBuilder<T>`, `BatchBuilder`, ...). Those statements are rendered and run by the mutation and batch paths, not by the plan cache described here; this page follows the `SELECT` path.

Calling a terminal (`ToListAsync`, `FirstAsync`, `ToAsyncEnumerable`, ...) starts the rest of the pipeline. `Select` fixes `TResult` on the command, and its terminals return that projection shape.

## Preparation

Preparation turns the recorded query into clause fragments the later stages can use. `QueryPreparer` processes the source and joins, columns and projection, `WHERE` and pre-`WHERE`, grouping, sorting, CTEs, and hints; it also computes the hashes that form the plan key. Preparation happens before the cache lookup. Afterward, the command carries its prepared pieces and current plan hash.

To translate the recorded expressions, `QueryPreparer` uses expression visitors: passes that walk and transform parts of the expression structure. For example, `CorrelatedQueryExpressionVisitor` rewrites correlated subqueries, while the `WHERE` and base visitors translate predicates. Their output is an internal select description (`SelectExpression`) for the SQL builder to render.

## The plan key

A `QueryPlan` captures the *structural shape* of the query, not its runtime values. Two calls with different lambda instances but the same shape produce the same key, so the cache can reuse the plan while the runtime parameter values change independently. [`QueryPlanStore`](03-plan-cache-and-reuse.md) keeps plans in a per-thread cache keyed by `(context type, QueryPlan)`. To disable lookup for one command, use `QueryCommand<TResult>.Cache`; see [plan cache and reuse](03-plan-cache-and-reuse.md) for the rules.

## SQL generation

`SqlBuilder` renders the prepared command through the provider's `ISqlDialect`, producing SQL text and an ordered parameter list. The dialect maps the same query shape to SQLite, SQL Server, PostgreSQL, MySQL/MariaDB, or ClickHouse SQL; see [Provider-specific SQL](../guide/provider-specific-sql/overview.md). The next stage binds those parameters to a `DbCommand` (see [execution and rows](02-execution-and-rows.md)).

## In-memory

The [in-memory provider](../connecting-providers/in-memory.md) runs the same command and projection without SQL: there is no dialect and no `DbCommand`, so the prepared expression is compiled and evaluated over the in-memory data set instead.

## See also

* [Engine overview](index.md)
* [Execution and rows](02-execution-and-rows.md)
* [Plan cache and reuse](03-plan-cache-and-reuse.md)
* [Query hints](../guide/15-query-hints.md)
* [Provider-specific SQL](../guide/provider-specific-sql/overview.md)
