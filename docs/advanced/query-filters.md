# Global query filters

> A query filter is a predicate bound to an entity type in the mapping metadata; nextorm `and`s it into every query in which the entity participates — the primary `FROM`, join sources, subqueries and eagerly-loaded children — unless the query calls `IgnoreFilters()`.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Provider overview](../providers/overview.md) · [Filtering (`Where`)](../guide/01-filtering-where.md)

## Overview

Global query filters attach a recurring predicate — soft-delete and multi-tenancy are the canonical cases — to the entity type instead of repeating it in every `Where`. A filter can read the executing [`IDataContext`](xref:NextORM.Core.IDataContext), so a tenant identifier or a soft-delete flag is resolved per query while the generated plan stays cached and shared across contexts.

This is the nextorm equivalent of EF Core global query filters and linq2db query filters.

## Declaring a filter

### Fluent

Declare the filter where the entity's mapping is configured with `From<T>`. The declaration runs once, when the type's metadata is first built, so the condition is part of the mapping rather than of one query:

```csharp
ctx.From<Document>(m => m.HasQueryFilter(d => !d.IsDeleted));
```

The context-aware overload receives the executing [`IDataContext`](xref:NextORM.Core.IDataContext) as a second parameter, so the filter can read per-context state:

```csharp
ctx.From<Document>(m => m.HasQueryFilter((d, c) => d.TenantId == (int)c.Properties["tenant"]));
```

[`EntityMetadataBuilder<T>`](xref:NextORM.Core.EntityMetadataBuilder`1) exposes the anonymous overloads plus the keyed one:

```csharp
public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, bool>> filter);
public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, IDataContext, bool>> filter);
public EntityMetadataBuilder<T> HasQueryFilter(string filterKey, Expression<Func<T, IDataContext, bool>>? filter);
```

A repeated anonymous call adds another predicate; all anonymous filters declared for a type are combined with `and`.

### Named (keyed) filters

Give a filter a **key** so it can be targeted individually by `IgnoreFilters`:

```csharp
ctx.From<Document>(m => m
    .HasQueryFilter("soft-delete", (d, c) => !d.IsDeleted)
    .HasQueryFilter("tenant", (d, c) => d.TenantId == (int)c.Properties["tenant"]));
```

A keyed filter occupies a **slot**: a repeated call with the same key **replaces** the earlier filter, and passing a `null` `filter` **removes** the slot. Anonymous filters (declared without a key) are additive — several are combined with `and` — and are reported with the key [`QueryFilters.AnonymousKey`](xref:NextORM.Core.QueryFilters.AnonymousKey), the empty string `""`.

When the same key is declared more than once, the resolver is deterministic: fluent calls come first (in call order), then `[QueryFilter]` attributes from the base type down to the most derived one, so a derived attribute overrides a base one and an attribute overrides a same-key fluent filter. A repeated key **on the same type** is different: the runtime does not guarantee attribute declaration order, so two `[QueryFilter]` attributes that share a non-empty `FilterKey` on one type have no deterministic winner and are **rejected** when the metadata is built. Declare the key once, or override it on a derived type. Anonymous filters are additive and may repeat. Keys are compared ordinally (case-sensitive); a `null`, empty or whitespace key declares an anonymous filter instead of a slot.

### Attribute

The same filters can be declared declaratively with [`[QueryFilter]`](xref:NextORM.Core.QueryFilterAttribute) on a class or an interface. `FilterLambda` names a **static member** of the attributed type — a field, a property or a parameterless method — that returns the filter lambda:

```csharp
[QueryFilter(FilterLambda = nameof(Active))]
public sealed class Document
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }

    public static Expression<Func<Document, bool>> Active() => d => !d.IsDeleted;
}
```

The named member may also return a context-aware predicate:

```csharp
[QueryFilter(FilterLambda = nameof(ForTenant))]
public sealed class Document
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public static Expression<Func<Document, IDataContext, bool>> ForTenant()
        => (d, c) => d.TenantId == (int)c.Properties["tenant"];
}
```

Set `FilterKey` to name the attribute filter and make it targetable:

```csharp
[QueryFilter(FilterKey = "soft-delete", FilterLambda = nameof(Active))]
[QueryFilter(FilterKey = "tenant", FilterLambda = nameof(ForTenant))]
public sealed class Document { /* ... */ }
```

With a key, the same slot rules apply: repeating the key replaces the earlier filter, and `FilterLambda = null` removes a slot inherited from a base type. An attribute with neither `FilterKey` nor `FilterLambda` is invalid and throws when the metadata is built.

`AllowMultiple = true` means several `[QueryFilter]` attributes can be placed on one type; anonymous attributes are combined with `and` just like repeated fluent calls.

## Reading the executing context

A filter whose lambda takes `IDataContext` reads the value from the context at execution time:

```csharp
public static Expression<Func<Document, IDataContext, bool>> ForTenant()
    => (d, c) => d.TenantId == (int)c.Properties["tenant"];
```

The tenant value is not inlined into the SQL. It becomes a **runtime parameter**, and the filter's identity (not its current values) is part of the plan key, so:

- two contexts with different tenant values can share the same cached plan;
- each execution reads its **own** context value, even on a plan-cache hit.

The same predicate compiles and re-reads the context on every invocation in the in-memory provider.

## Disabling filters for one query

[`IgnoreFilters`](xref:NextORM.Core.EntityBuilder`1.IgnoreFilters) returns a copy of the builder with the chosen filters disabled for that query. There are four overloads:

```csharp
public EntityBuilder<TEntity> IgnoreFilters();
public EntityBuilder<TEntity> IgnoreFilters(params Type[] entityTypes);
public EntityBuilder<TEntity> IgnoreFilters(IEnumerable<string> filterKeys);
public EntityBuilder<TEntity> IgnoreFilters(IEnumerable<string> filterKeys, params Type[] entityTypes);
```

Their semantics are:

| Call | Disables |
|---|---|
| `IgnoreFilters()` | every filter, on every entity type |
| `IgnoreFilters(typeof(A), typeof(B))` | every filter (any key, including anonymous) declared for `A` and `B` |
| `IgnoreFilters(["soft-delete"])` | the filter keyed `soft-delete` on every entity type |
| `IgnoreFilters(["soft-delete"], typeof(A))` | the filter keyed `soft-delete` only on `A` (intersection of key and type) |

Rules that follow from the above:

- **Types only** disables **all filters** of those entity types (anonymous and keyed).
- **Keys only** disables **those keys across all entity types**.
- **Keys and types** disables the **intersection** — only the listed keys on the listed types.
- **Empty or `null` key or type list is a no-op**: nothing is disabled. The key list is the gate, so an empty key list disables nothing even when entity types are supplied (mirroring EF Core).
- [`QueryFilters.AnonymousKey`](xref:NextORM.Core.QueryFilters.AnonymousKey) (`""`) targets anonymous filters, e.g. `IgnoreFilters([QueryFilters.AnonymousKey])`.

For example, ignoring only the soft-delete filter while keeping tenant scoping:

```csharp
var deleted = ctx.From<Document>()
    .IgnoreFilters(["soft-delete"])
    .Where(d => d.IsDeleted)
    .Select(d => d.Id)
    .ToList();
```

The original builder is unchanged; only the returned copy ignores the filters. **Repeated calls accumulate (union), they do not replace**: `IgnoreFilters(["a"]).IgnoreFilters(["b"])` disables both `a` and `b`, and the order of calls and duplicate keys do not matter. Type-only and key-only selectors also accumulate independently, while a single `IgnoreFilters(keys, types)` call is an **intersection** — each of its keys applies only to the listed types. A combined call is never folded into another selector's union. A scope that is empty (an empty or `null` key/type list) disables nothing, and an all-or-nothing `IgnoreFilters()` dominates any selective scope combined with it.

Once the query command exists, [`QueryCommand.IgnoreFilters`](xref:NextORM.Core.QueryCommand.IgnoreFilters) reads back the result: it is `true` when **any** filter is disabled — whether by the all-or-nothing form or by a selective scope — and `false` when none is. Its setter is all-or-nothing (`true` disables every filter, `false` clears the scope) because a `bool` cannot express a selective disable; the selective scope itself is the authoritative state.

## Scope: primary source, joins and subqueries

A filter is attached to the entity type and is injected wherever that type appears in the query shape:

| Position | Injected |
|---|---|
| Primary `FROM` | into the statement's `WHERE`, combined with an explicit `Where` |
| Join source | into the join's `ON` condition for the joined entity |
| Subquery | into the subquery when it is prepared |
| Mutation target (`UPDATE` / `DELETE`) | into the statement's `WHERE`, combined with the predicate or the key equality |
| `MERGE` target (SQL Server, PostgreSQL) | into the `MERGE ... ON` condition and every `WHEN NOT MATCHED BY SOURCE` arm (see [Write-target isolation](#write-target-isolation)) |

The main source of a join is the first table, so a filter declared for `T1` applies to `Item1` exactly as in a plain query.

The disable scope is carried by the query that starts it, so a type listed in a selective `IgnoreFilters` is disabled wherever it appears in that query, and `IgnoreFilters()` disables every filter everywhere in the query — including eagerly-loaded children declared with [`LoadWith`](eager-loading.md). This holds identically in split and single-query (`AsSingleQuery`) mode: the child side's effective scope is the **union** of its own `IgnoreFilters` scope and the parent's, `IgnoreFilters()` (`All`) absorbs that union, and a child's selective scope disables only filters on that child. See [Eager loading](eager-loading.md#single-query-mode-assinglequery).

## UPDATE and DELETE (DML)

A filter declared for the target entity applies to a mutation of that entity exactly as to a read:

| Statement | Filter |
|---|---|
| `Update<T>().Where(...).Update()` | `and`-ed into the `WHERE` with the predicate |
| `Update(entity)` (key form) | `WHERE <pk> = @p and <filter>` — a row excluded by the filter is not updated |
| `DeleteFrom<T>().Where(...).Delete()` | `and`-ed into the `WHERE` with the predicate |
| `Delete(entity)` (key form) | `WHERE <pk> = @p and <filter>` — a row excluded by the filter is not deleted |
| `Update<T>().Set(...).Update()` with no `Where` | every **filtered** row is updated (the filter still applies) |
| `DeleteFrom<T>().All()` | explicit full-table delete: **no** filter is applied |
| `UpdateJoin(...)` / join `Delete()` | the target (first table) and every joined source are filtered |

For the key forms, the filter is `and`-ed to the key equality, so `ctx.Delete(entity)` returns `0` if the filter excludes that key. The filter is injected into the mutation's prepared condition, so the rendered SQL reflects the effective filter set. Unlike a read, a mutation is neither prepared nor plan-cached, so there is no plan key for the filter to take part in.

[`UpdateBuilder<T>`](xref:NextORM.Core.UpdateBuilder`1), [`DeleteBuilder<T>`](xref:NextORM.Core.DeleteBuilder`1) and [`UpdateJoinBuilder<TProjection>`](xref:NextORM.Core.UpdateJoinBuilder`1) expose the same four `IgnoreFilters` overloads as the read builder and with the same semantics (type-only, key-only, key-and-type intersection, empty is a no-op). Unlike the read builder, which returns a copy, a DML builder is stateful: the call applies to the statement being built and repeated calls accumulate:

```csharp
// Ignore only the soft-delete filter; the tenant filter still applies.
ctx.DeleteFrom<Document>()
    .IgnoreFilters(["soft-delete"])
    .Where(d => d.IsDeleted)
    .Delete();

// Ignore every target filter, then update only the named column.
ctx.Update<Document>()
    .IgnoreFilters()
    .Set(d => d.Archived, true)
    .Update();
```

`INSERT` / `MERGE` do not inject a target filter through a source `FROM` (the target has no `FROM`); under an active filter a full `MERGE` constrains the target inside the statement, and the key-upsert forms that cannot do so **refuse** (see [Write-target isolation](#write-target-isolation)). `INSERT … SELECT` filters its source as any read — and the written rows are validated instead (see [INSERT and MERGE (validation)](#insert-and-merge-validation)).

## INSERT and MERGE (validation)

Neither an `INSERT` nor a `MERGE` injects a target filter through a source `FROM` — the target has no `FROM` — so a write is never silently restricted. A **full `MERGE`** (and the SQL Server key upsert, which renders as the same form) instead constrains the target atomically inside the statement, while an `ON CONFLICT` / `ON DUPLICATE KEY` / in-memory key upsert — which cannot carry the predicate — **refuses** (see [Write-target isolation](#write-target-isolation)). Independently, the values about to be written are validated against the target entity's active filters (minus the `IgnoreFilters` scope) **before** the statement runs; a violation raises [`QueryFilterException`](xref:NextORM.Core.QueryFilterException) (a [`DataContextException`](xref:NextORM.Core.DataContextException)).

| Statement | What is validated |
|---|---|
| `InsertInto<T>().Value(...)` / `Values(entity)` / batch `Values(...)` | every written row against the target filters |
| `BulkInsertInto<T>()` | every row in the source |
| `MergeInto<T>().Using(...)` | the source rows of **every** `MERGE` branch — insert, update-only and delete-only |
| `InsertInto<T>().Values(source, mapping)` (`INSERT … SELECT`) | a server-side pre-check of the source rows |
| `MergeInto<T>().Using(query)` (query-sourced `MERGE`) | a server-side pre-check of the source rows |

On the supported full-`MERGE` form the source-value validation covers **every branch combination** — an update-only or delete-only `MERGE` validates its incoming source rows just like one that inserts, and a non-passing source row throws [`QueryFilterException`](xref:NextORM.Core.QueryFilterException) before any mutation, even when the statement would not insert a row.

For a materialised entity the filter is evaluated directly against it. For the column/value forms only the written columns carry a value, so validation is **fail-closed**: if an active filter reads a column the statement does **not** write, the write is **rejected** with a [`QueryFilterException`](xref:NextORM.Core.QueryFilterException) rather than let through — a database default could satisfy or violate the filter, and nextorm does not guess. Write the column explicitly, or disable the filter for the statement with `IgnoreFilters`.

`INSERT … SELECT` and a query-sourced `MERGE` are pre-checked with a separate existence query over the source (looking for a row the target filter rejects). This is a **TOCTOU** guard, not a transactional one: between the check and the write a concurrent writer can still change the rows, and validation does **not** make the write atomic. Put both in the same transaction when the race matters.

A multi-row or bulk write is not atomic either. A synchronous bulk source is validated in full before the first batch is sent; an **asynchronous** bulk source is validated row by row as it streams, so a later violation can leave earlier rows already written.

`IgnoreFilters` on the [`InsertBuilder<T>`](xref:NextORM.Core.InsertBuilder`1), [`BulkInsertBuilder<T>`](xref:NextORM.Core.BulkInsertBuilder`1) and [`MergeBuilder<T>`](xref:NextORM.Core.MergeBuilder`1) disables the corresponding filters from validation too, with the same four overloads and semantics as the read builder:

```csharp
ctx.InsertInto<Document>()
    .IgnoreFilters(["soft-delete"])
    .Values(document)
    .Insert();
```

A filter declared in the builder-function form (`FilterFunc`) cannot be validated against a written row — it is evaluated only while a query plan is built — so a write whose active **target** filter is declared as a function is rejected (fail-closed) unless the filter is disabled with `IgnoreFilters`; declare the filter as a predicate (`FilterLambda`) when the write must be validated. `INSERT … SELECT` still filters its **source** as a read, and `UPDATE` / `DELETE` are unaffected (see [Builder-function filters](#builder-function-filters-filterfunc)).

## Write-target isolation

A write is filtered on two sides, and they are independent:

- the **source** of an `INSERT … SELECT` or a query-sourced `MERGE` is an ordinary read, so a filter declared for the source entity is injected into the source query as usual;
- the **target** has no `FROM`, so its filter is not part of any source query. Under an active, non-ignored target filter the operation is constrained atomically in the statement where the dialect can express it; where it cannot, the command **refuses** with `NotSupportedException` before any mutation. A capability/form refusal happens before any read, but a query-sourced merge may first run its source pre-check read before a translation/render refusal. The predicate is never silently dropped: an active filter that cannot be translated to a target predicate is a fail-closed error, not a bypass.

For the full `MERGE` form (SQL Server, PostgreSQL) the target predicate is injected into the `MERGE ... ON` condition and — on SQL Server, which has the arm — appended to every `WHEN NOT MATCHED BY SOURCE` branch. A target row hidden by the filter is therefore never matched, never updated and never deleted by the merge, including its delete arm. The SQL Server key upsert renders as the same `MERGE` form and takes the same predicate, so it is filtered too.

**Form rule.** The atomic form is a real multi-branch `MERGE`, which requires actual `WhenMatched()`/`WhenNotMatched()` branches. Calling `.On(...)` does not turn the key-upsert shortcut (`OnKeys()` + `WhenMatchedUpdate()` + `WhenNotMatchedInsert()`) into a full `MERGE`; the two are distinct forms. On a provider that has no full-`MERGE` support (SQLite, MySQL, MariaDB, in-memory), a branchless `.On(...)` under an active filter therefore fails closed with `NotSupportedException` before any source read.

**PostgreSQL is a server prerequisite, not a client-side guard.** PostgreSQL 15+ is required for the server to execute the general `MERGE`; nextorm does not discover or configure the server version and performs **no version read**. Capability-based rendering is version-independent — the library emits the statement from the dialect's capability flags alone, and an older server rejects it server-side. There is no library-side "PostgreSQL < 15 refuses" path.

| Provider / form | Target filter under an active, non-ignored filter |
|---|---|
| SQL Server — full `MERGE` and key upsert (`MERGE`) | injected into `ON` and into every `WHEN NOT MATCHED BY SOURCE` arm |
| PostgreSQL (server 15+) — full `MERGE` | injected into `ON` (PostgreSQL has no `WHEN NOT MATCHED BY SOURCE`) |
| PostgreSQL, SQLite — key upsert (`ON CONFLICT`) | `NotSupportedException` — fail closed |
| MySQL, MariaDB — key upsert (`ON DUPLICATE KEY`) | `NotSupportedException` — fail closed |
| In-memory — key upsert | `NotSupportedException` — fail closed |
| Any provider — no active filter, or the filter is disabled | native behavior, no extra predicate |

The capability refusal is a **metadata decision, before any connection, command or read** (zero database round-trips); the exception message points to `IgnoreFilters()` or to a full-`MERGE`-capable provider/form. It is deliberate fail-closed behavior, not a bug.

`IgnoreFilters` bypasses the target filter for the statement, with the same four overloads as elsewhere (all-or-nothing, by entity type, by key, and the key/type intersection). A scope that disables the target entity's filter — or all filters — restores the provider's native upsert without the predicate; a selective scope that does not cover the target filter leaves it active, so an `ON CONFLICT` / `ON DUPLICATE KEY` upsert still refuses.

### Limits of target isolation

- **Existence oracle on a unique-index collision (insert arm only).** When a `MERGE`'s insert arm attempts a row whose key collides with a target row the filter hides, the provider reports its native unique-constraint error for whichever unique index it hits — not only the PK/merge key — revealing that a hidden target row exists; it cannot be removed without dropping the unique constraint. The composite-key mitigation (include the tenant/filter column) applies to the primary/merge key only, since it prevents a hidden row's key from colliding with a visible source key but does not cover other unique indexes. An update-only or delete-only `MERGE` never inserts, so a hidden row is a silent no-op (the filtered-out row is simply not matched) and exposes no oracle.
- **Source-value validation is a separate read.** For `INSERT … SELECT` and a query-sourced `MERGE`, the written source values are checked by a separate existence query (`QueryFilterValidator`) with a **TOCTOU** window. It validates source values, not target rows, so it does not reveal hidden target existence and it does not make the write atomic (see [INSERT and MERGE (validation)](#insert-and-merge-validation)).
- The refusal above is the intended safe outcome when the dialect cannot isolate the target atomically.

With **no filter** configured the write path is unchanged: no predicate is added and no operation refuses.

## Builder-function filters (`FilterFunc`)

A filter can also be declared as a **builder function** instead of a predicate: a `Func<EntityBuilder<T>, IDataContext, EntityBuilder<T>>`. nextorm invokes it **once, while the query plan is built**, passing the live [`IDataContext`](xref:NextORM.Core.IDataContext); the function calls `Where` on the fresh builder and nextorm merges **only** that predicate into the main source `WHERE` or the join `ON`, exactly like a predicate filter.

```csharp
ctx.From<Document>(m => m.HasQueryFilter(
    (b, c) => b.Where(d => d.TenantId == (int)c.Properties["tenant"])));

// Keyed form — the function occupies the same slot as a predicate filter.
ctx.From<Document>(m => m.HasQueryFilter(
    "tenant", (b, c) => b.Where(d => d.TenantId == (int)c.Properties["tenant"])));
```

Read the value through the `IDataContext` **inside** the predicate — or use [`SqlFunctions.Parameter<T>(idx)`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)) — so it stays a bound parameter and the generated SQL is shared across contexts, exactly as for a predicate filter. A chain of `Where` calls is allowed.

### Attribute

`QueryFilterAttribute.FilterFunc` names a **static member** of the attributed type that returns the function; a declaration sets either `FilterLambda` or `FilterFunc`, not both:

```csharp
[QueryFilter(FilterKey = "tenant", FilterFunc = nameof(TenantFilter))]
public sealed class Document
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    public static Func<EntityBuilder<Document>, IDataContext, EntityBuilder<Document>> TenantFilter()
        => (b, c) => b.Where(d => d.TenantId == (int)c.Properties["tenant"]);
}
```

### IgnoreFilters

The function form takes part in `IgnoreFilters` exactly like a predicate filter: it carries the same key (or [`QueryFilters.AnonymousKey`](xref:NextORM.Core.QueryFilters.AnonymousKey)), so keyed, type-only and intersection scopes disable it the same way — `IgnoreFilters(["tenant"])`, `IgnoreFilters(typeof(Document))` and an all-or-nothing `IgnoreFilters()` all apply.

### The function must not snapshot a runtime value

The function runs **only when the plan is built**, so a runtime value must be read through the `IDataContext` **inside** the predicate. Capturing it into a local before the `Where` call is rejected with `NotSupportedException`: the value would be baked into the cached plan (and into the in-memory evaluation) and reused by every later execution and every other context, so a plan-cache hit would silently return the wrong rows.

```csharp
// Rejected: `tenant` is snapshotted before the Where call, so it would be frozen into the plan.
ctx.From<Document>(m => m.HasQueryFilter((b, c) =>
{
    var tenant = (int)c.Properties["tenant"];
    return b.Where(d => d.TenantId == tenant);
}));
```

### Unsupported cases

- **Non-`Where` mutations.** Only the `Where` predicate is merged, so a function that changes anything else on the builder — source, join, select, group, ordering, paging, eager loading or filter scope — returns a different builder, or returns no `Where` / a `null` builder, is rejected with `NotSupportedException`. Express the filter with `Where` alone.
- **Captured collections.** A predicate that closes over a collection — for example `ids.Contains(e.Id)` — captures it as a runtime value and is rejected with `NotSupportedException`. The function runs only at plan build, so the collection cannot stay a bound `IN` list; declare the filter as a predicate (`FilterLambda`) or use a `SqlFunctions.Parameter<T>(idx)` placeholder instead.
- **Foreign captured context.** A predicate that closes over an `IDataContext` other than the one passed to the function is rejected with `NotSupportedException`: reading it on a shared plan would silently return the wrong context's values. Read the function's `IDataContext` parameter instead.
- **`INSERT` / `MERGE` target validation.** A function filter is evaluated only while a query plan is built, so it cannot be validated against a written row; a write whose active **target** filter is declared as a function is rejected (fail-closed) unless the filter is disabled with `IgnoreFilters`. Declare the filter as a predicate (`FilterLambda`) when the write must be validated.
- **Filter functions on unbound `FromSql` / raw sources** are not applied (the raw SQL is passed through as written; the same holds for predicate filters). Bind the source with [`BindEntity<TEntity>`](#filters-on-bound-raw-sources) to opt in.

## In-memory provider

The in-memory provider applies the same predicate to the registered sequence before projection and joins, so soft-delete and multi-tenancy queries behave identically to SQL providers. The `IgnoreFilters` overloads are honoured there too. For a write — the key-upsert merge is the only write it applies — an active, non-ignored filter makes the operation **refuse** with `NotSupportedException` (fail closed; see [Write-target isolation](#write-target-isolation)); `IgnoreFilters` restores the native behavior.

## Filters on bound raw sources

A [`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object))/`From(string)` source has no mapped entity type, so nextorm cannot know which columns a filter reads. Call [`BindEntity<TEntity>`](xref:NextORM.Core.EntityBuilderExtensions.BindEntity``1(NextORM.Core.EntityBuilder{NextORM.Core.TableAlias},System.Collections.Generic.IReadOnlyCollection{System.String})) as the first operation on the source to attach entity metadata and declare the columns the raw SQL exposes; nextorm then applies each active global filter to that source best-effort:

```csharp
var rows = dataContext
    .FromSql("select id, tenant_id from documents where archived = 0")
    .BindEntity<Document>(["id", "tenant_id"])
    .Where(t => t["id"].AsInt > 10)
    .ToList();
```

`availableColumns` are the **output/SQL names** of the raw select list — a configured column mapping wins, otherwise the name the projection expects — and they are compared case-insensitively. The list is a caller declaration, not a schema read: nextorm does not parse the SQL or query the schema, and binding neither adds nor renames output columns, so the columns you project remain your responsibility.

The exact signature is:

```csharp
public static EntityBuilder<TEntity> BindEntity<TEntity>(
    this EntityBuilder<TableAlias> source,
    IReadOnlyCollection<string> availableColumns)
```

For each active filter on `TEntity`:

- if every column the filter reads is declared, the filter is `and`-ed into the source;
- if a required column is missing, the filter is **skipped** (no exception) and a `RawSourceFilterSkipped` warning is logged on the `NextORM.QueryFilters` logger category with reason `MissingColumns` and the missing names — the **physical mapped column names** the filter reads, not the caller's declared output names;
- **with an empty declared-column list**, a filter with a proven zero-column dependency (a predicate that reads no mapped column, such as a constant) is applied; a column-dependent or undetermined filter is skipped with a warning (reason `UndeterminedColumns`).

Filters are evaluated independently — one skipped filter does not stop the others — and [`IgnoreFilters`](#disabling-filters-for-one-query) disables the selected filters before this analysis, so an ignored filter is never reported. The warning is emitted once per skipped filter while the query plan is prepared (a cache miss); a plan-cache hit does not re-emit it. The message carries only the entity type name, the source ordinal, the filter key, the reason and the missing column names (physical mapped column names only) — never SQL text, table names, parameter values or captured values. Wire an `ILoggerFactory` and enable the `NextORM.QueryFilters` category at `Warning` to see it (categories are topic-based, so filtering that category is enough):

```csharp
using var loggerFactory = LoggerFactory.Create(builder => builder
    .AddFilter("NextORM.QueryFilters", LogLevel.Warning)
    .AddConsole());

var ctx = new SqliteDataContext(builder => builder.UseLoggerFactory(loggerFactory));
```

`BindEntity` must be the first operation after the source is created; a later call (after `Where`/`Select`/`Join`/projection) throws `InvalidOperationException`. Another source shape, or binding to `TableAlias`, throws `NotSupportedException`; a `null` source or column collection throws `ArgumentNullException`, and a `null`/blank entry throws `ArgumentException` (an empty collection is allowed, and the receiver is not mutated — a new typed builder is returned). `WithSql`, `PrepareFromSql` and `ExecuteRaw` are unchanged and do not take part, and the in-memory provider still rejects a raw source.

> **Not a security guarantee.** nextorm trusts the column list you supply: a filter whose columns you forget to declare is silently skipped, and the raw source then returns rows the filter would have excluded. Treat `BindEntity` as a convenience for composing filters over a trusted raw source, not as an enforcement or isolation boundary; keep row-level authorization in the SQL itself when it must be enforced.

### Joined bound sources and the main source

Binding is **per source**, not per entity type: each joined raw source uses its own `BindEntity<TEntity>` binding and its own declared column list, and never borrows the main source's binding or another occurrence's. The same entity type joined twice with different declared columns keeps both bindings independent. A joined source's compatible filters are merged into **that join's** `ON` condition; a skipped filter on one occurrence warns separately and leaves the other occurrences and the other filters unaffected, and an outer join's predicates stay in `ON` and are never moved into `WHERE`.

The main source's filters are always evaluated against the **main source's own binding**, even when the command is a joined projection. In that case the retained predicates are re-rooted onto the projection's **main** alias, `Item1` (`Projection<T1, …>`), and placed in the `WHERE` clause; they are never resolved from a join-side binding or alias.

`SourceOrdinal` identifies where a skip happened: the main source is `0`, and join index `j` (zero-based) is `j + 1`, counting **all** joins — including unbound ones — so the ordinal is the join's position, not the number of bound sources.

A `CROSS`/`CROSS APPLY` join has no `ON` clause, so a bound raw source on such a join has no predicate to attach to: its compatible filters are placed in `WHERE` instead, and nextorm never fabricates an `ON` for a join that has none. `OUTER APPLY`/`PASTE` are left unchanged, because moving their predicates into `WHERE` would change the result.

## Metadata

A declared filter is exposed through [`IQueryFilterMetadata`](xref:NextORM.Core.IQueryFilterMetadata):

```csharp
public interface IQueryFilterMetadata
{
    string Key { get; }                    // QueryFilters.AnonymousKey ("") for an anonymous filter
    LambdaExpression? Lambda { get; }      // the predicate, or null for a builder-function filter
    Delegate? Func { get; }                // the builder-function declaration, or null for a predicate filter
}
```

`Key` is [`QueryFilters.AnonymousKey`](xref:NextORM.Core.QueryFilters.AnonymousKey) for an anonymous filter and the declared key for a named one. A predicate filter reports its predicate in `Lambda` and `null` in `Func`; a builder-function filter reports `null` in `Lambda` and the `Func<EntityBuilder<T>, IDataContext, EntityBuilder<T>>` declaration in `Func`. A filter that reports both non-null is rejected.

`Func` has a default interface implementation returning `null`, so an external implementation that does not declare `Func` keeps compiling. The member's return type changed from `LambdaExpression?` to `Delegate?` while it was unreleased, so an implementation that explicitly declared the earlier shape must update the return type; released consumers are unaffected because the member never shipped.

## Limitations

- **Process-global, first-registration-wins.** Filters are registered **process-globally** and the first registration for an entity type wins (consistent with nextorm metadata): a later `From<T>(cfg)` / `HasQueryFilter` for the same type is ignored — filters are not scoped to a `DataContext`.
- **Disable scope follows the entry builder.** The selective scope is carried by the builder that starts the query; calling `IgnoreFilters` on a builder that is then used as a join source is not propagated. Use one of the type/key overloads on the query's entry builder instead. Eagerly-loaded `LoadWith` children are the exception: they inherit the entry builder's scope by union (see [Eager loading](eager-loading.md)).
- **Key-form mutations apply filters but do not expose `IgnoreFilters`.** `Update(entity)` and `Delete(entity)` honour the target filter, but their immediate terminal has no fluent `IgnoreFilters`; use the predicate form (`Update<T>().Where(...)` / `DeleteFrom<T>().Where(...)`) when you need to disable a filter on a mutation.
- **Plan lifetime.** A context value read by a filter is captured as a runtime parameter (plan-cache safe). The prepared plan retains the first `IDataContext` instance for its lifetime (bounded, one per plan shape).

## Not yet

Write-target isolation **is** implemented for the full `MERGE` (SQL Server, PostgreSQL 15+) and the SQL Server key upsert, and the `ON CONFLICT` / `ON DUPLICATE KEY` / in-memory key upserts **refuse** under an active filter rather than bypass it (see [Write-target isolation](#write-target-isolation), [#123](https://github.com/AlexeyShirshov/nextorm/issues/123)).

The following are **not** available:

- the EF Core bridge that forwards EF Core 10 keyed filters ([#125](https://github.com/AlexeyShirshov/nextorm/issues/125)).

`UPDATE`, `DELETE` and the full-`MERGE` write target are covered (see [UPDATE and DELETE (DML)](#update-and-delete-dml) and [Write-target isolation](#write-target-isolation)).
