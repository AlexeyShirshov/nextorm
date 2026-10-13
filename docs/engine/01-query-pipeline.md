# Query pipeline: fluent to SQL

> How a fluent query becomes SQL text and a plan key. A [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) is built by the fluent surface, prepared by `QueryPreparer` (through expression visitors), keyed by a `QueryPlan`, and rendered by `SqlBuilder` against the provider dialect. See the [engine overview](index.md) for the whole picture.

## The built query

`ctx.From<TEntity>()` returns an [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1); each fluent operator (`Where`, `Join`, `Select`, `OrderBy`, `GroupBy`, `With`, hints, ...) records a piece of intent on a [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1). Nothing is executed yet — a command is a description of a query, not a live connection.

The terminal you call (`ToListAsync`, `FirstAsync`, `ToAsyncEnumerable`, ...) is what drives the command through the rest of the pipeline. The same command type is used for the projection, so `Select` fixes `TResult` once and the terminals return that shape.

## Preparation

`QueryPreparer` walks the recorded pieces and turns them into prepared clause fragments plus the hashes that form the plan key: source and joins, columns/projection, `WHERE` / pre-`WHERE`, grouping, sorting, CTEs and hints. Preparation happens before the plan lookup, so a command carries prepared pieces and a current plan hash once it has been prepared once.

The expression tree is translated with expression visitors — for example `CorrelatedQueryExpressionVisitor` rewrites correlated subqueries, and the `WHERE`/base visitors drive predicate translation. The output is the internal select description (`SelectExpression`) that the SQL builder renders.

## The plan key

A `QueryPlan` captures the *structural shape* of the query, not its runtime values. Two calls with a different lambda instance but the same shape produce the same key and therefore a cache hit; the runtime parameter values are carried separately. [`QueryPlanStore`](03-plan-cache-and-reuse.md) keeps plans in a per-thread cache keyed by `(context type, QueryPlan)`. Disabling lookup for one command is done with `QueryCommand<TResult>.Cache`; the [plan cache and reuse](03-plan-cache-and-reuse.md) page covers the rules.

## SQL generation

`SqlBuilder` renders the prepared command through the provider's `ISqlDialect`, producing the SQL text and the ordered parameter list. The dialect is what makes the same shape compile to SQLite, SQL Server, PostgreSQL, MySQL/MariaDB or ClickHouse SQL — see [Provider-specific SQL](../guide/provider-specific-sql/overview.md). The parameters are bound to a real `DbCommand` in the next stage ([execution and rows](02-execution-and-rows.md)).

## In-memory

The [in-memory provider](../connecting-providers/in-memory.md) runs the same command and projection without SQL: there is no dialect and no `DbCommand`, so the prepared expression is compiled and evaluated over the in-memory data set instead.

## See also

* [Engine overview](index.md)
* [Execution and rows](02-execution-and-rows.md)
* [Plan cache and reuse](03-plan-cache-and-reuse.md)
* [Query hints](../guide/13-query-hints.md)
* [Provider-specific SQL](../guide/provider-specific-sql/overview.md)
