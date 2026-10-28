# Collection-valued projection slots (`Select`)

> A projection may name a **declared** collection navigation as one of its members: the direct form
> `Select(p => new { p.Id, p.Comments })` and the adapter form
> `p.Comments.AsEntityBuilder<Comment>().Where(..).OrderBy(..).Limit(..).Select(..).ToList()` both project a
> **per-parent collection** into the result type you declare. The engine materializes the entity graph through the
> existing eager-loading route — split by default, one denormalized statement in single-query mode — and then
> applies the adapter pipeline and builds the projected shape **in memory**. The statement count is the eager
> loading's: it does not grow with the number of parents, and no correlation column is added to the SQL. This page
> documents the delivered v1 surface and its limits.

**Prerequisites:** [Relationships and single-query loading (`JoinInto`)](relationships.md) · [Eager loading child collections (`LoadWith`)](eager-loading.md) · [Projections](../guide/02-projections.md)

## Overview

A collection-valued slot is an ordinary projection member whose value is a whole collection of one navigation:

```csharp
var posts = ctx.From<Post>()
    .Where(p => p.Published)
    .OrderBy(p => p.Id)
    .Select(p => new { p.Id, p.Comments })
    .ToList();

foreach (var post in posts)
    foreach (var comment in post.Comments)
        Console.WriteLine($"{post.Id}: {comment.Id}");
```

Three things define the surface:

- **The relationship must be declared** — `HasMany`, `HasManyThrough` or `[Relationship]` on the collection side
  (see [Relationships](relationships.md)). A collection-typed member with no declared relationship is an ordinary
  projected value, not a slot; nothing is inferred from a convention or a foreign-key name.
- **Both collection cardinalities are projected** — one-to-many and many-to-many. A projected **reference**
  (many-to-one / one-to-one) is also honoured when the projection carries a collection slot as well, with its own
  cardinality rule ([below](#projected-references-and-cardinality)).
- **The projection is computed in memory, after materialization.** SQL selects the entity rows; the adapter
  operations and the destination shape are applied to the materialized collection. There is no SQL column pruning
  and no pushed-down DTO column in v1 — see [v1 limitations](#v1-limitations).

The public result type is exactly the type you declare: `Select` still returns
[`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1), and `TResult` carries the collection member as written.

## Declaring the entities

The examples on this page use one graph: `Post → Comment → Reaction`, `Post ↔ Tag` through `PostTag`, and a
`Comment.Author` reference.

```csharp
[SqlTable("post")]
public sealed class Post
{
    [Key, Column("id")] public int Id { get; set; }
    [Column("title")] public string? Title { get; set; }
    [Column("published")] public bool Published { get; set; }

    public List<Comment> Comments { get; } = [];
    public ICollection<Tag> Tags { get; } = new List<Tag>();
}

[SqlTable("comment")]
public sealed class Comment
{
    [Key, Column("id")] public int Id { get; set; }
    [Column("post_id")] public int PostId { get; set; }
    [Column("author_id")] public int AuthorId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; }
    [Column("active")] public bool Active { get; set; }

    public List<Reaction> Reactions { get; } = [];
    public Author? Author { get; set; }
}

[SqlTable("reaction")]
public sealed class Reaction
{
    [Key, Column("id")] public int Id { get; set; }
    [Column("comment_id")] public int CommentId { get; set; }
    [Column("kind")] public string? Kind { get; set; }
}

[SqlTable("tag")]
public sealed class Tag
{
    [Key, Column("id")] public int Id { get; set; }
    [Column("name")] public string? Name { get; set; }
}

[SqlTable("post_tag")]
public sealed class PostTag
{
    [Column("post_id")] public int PostId { get; set; }
    [Column("tag_id")] public int TagId { get; set; }
}

[SqlTable("author")]
public sealed class Author
{
    [Key, Column("id")] public int Id { get; set; }
    [Column("name")] public string? Name { get; set; }
}
```

The relationships are declared once, on the side that owns the collection:

```csharp
_ = ctx.From<Post>(cfg => cfg
    .HasMany(p => p.Comments, c => c.PostId)
    .HasManyThrough<Tag, PostTag, int, int>(
        p => p.Tags, p => p.Id, l => l.PostId, t => t.Id, l => l.TagId));

_ = ctx.From<Comment>(cfg => cfg
    .HasMany(c => c.Reactions, r => r.CommentId)
    .HasOne(c => c.Author, c => c.AuthorId));
```

## Direct slot

The direct form projects the navigation member itself:

```csharp
var posts = ctx.From<Post>()
    .Where(p => p.Published)
    .OrderBy(p => p.Id)
    .Select(p => new { p.Id, p.Comments })
    .ToList();

// select ... from post as t1 where t1.published            -- statement 1 (split)
// select ... from comment as t1 where t1.post_id in (@k0)  -- statement 2, one per key chunk
```

- **Empty children are an empty collection.** A post with no comments appears in the result with an empty
  `Comments` collection — never `null`.
- **Order and duplicates follow the child rows.** The direct form declares no ordering, so the order inside each
  collection is the child statement's order; add the adapter's `OrderBy` when it matters. Many-to-many slots keep
  one element per **distinct junction row** (a cartesian repeat of one junction row collapses; a literally
  duplicated link stays two elements).
- **Two statements for a one-to-many slot in split mode** — the parent statement and one batched child statement
  driven by the parent keys. The child statement is chunked, so it is one statement per 1000 parent keys, never one
  per parent.

## Adapter slot

The adapter form starts from the same navigation and applies a pipeline per parent before projecting:

```csharp
var pageSize = 2;
var authorId = 7;

var posts = ctx.From<Post>()
    .Where(p => p.Published)
    .OrderBy(p => p.Id)
    .Select(p => new
    {
        p.Id,
        Recent = p.Comments
            .AsEntityBuilder<Comment>()
            .Where(c => c.Active && c.AuthorId == authorId)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id, OrderDirection.Desc)
            .Limit(pageSize)
            .Select(c => new CommentRow { Id = c.Id, CreatedAt = c.CreatedAt })
            .ToList(),
        Tags = p.Tags.AsEntityBuilder<Tag>().OrderBy(t => t.Name).ToArray(),
    })
    .ToList();
```

`AsEntityBuilder<T>()` is the same navigation adapter that answers `Any`/`Count` (see
[Implicit navigation queries](implicit-navigation.md)); inside a projection it continues into a collection
pipeline:

| Operator | Meaning inside a slot |
|---|---|
| `Where(predicate)` | Filters this parent's elements before ordering and paging. The predicate may read the current parent (`i => i.PostId == p.Id`). |
| `OrderBy` / `OrderByDescending` | Primary ordering of this parent's elements, ascending or descending (`OrderDirection`). |
| `ThenBy` / `ThenByDescending` | Secondary ordering keys, applied in call order after the primary key. |
| `Limit(count)` | Keeps at most `count` elements **of this parent** after filtering and ordering. A captured count is read at execution. |
| `Select(projection)` | Projects each element into the slot's element type (DTO, record or anonymous shape). |
| `ToList()` / `ToArray()` | Terminates the slot and chooses the destination shape. |

- **Every operator is applied per parent, in memory.** The pipeline runs `filter → order → limit → select` on the
  materialized children of each parent, so a `Limit` is a per-parent page, never a global `LIMIT`/`TOP`/`FETCH`.
  Statement counts do not change: an adapter slot costs the same statements as the direct slot (2 split / 1 single
  for a depth-one one-to-many), whatever the page size.
- **The pipeline is declarative, not a LINQ-on-collections execution.** The operators are recorded from the
  expression tree and evaluated against the materialized collection; the lambdas keep their reference to the
  projection parameter, so a correlated predicate sees the current parent without recompiling.
- **A captured value rebinds on every execution.** One command built with `pageSize = 1` and executed again after
  changing it to `2` returns `[10]` and then `[10, 11]` — the value is read when the pipeline runs and is never
  baked into the plan key or the compiled delegate.
- **`Offset` is not part of the adapter.** The adapter accepts the seven operators above; `Limit` is the only
  paging operator, and `Offset`/`Skip` are refused by name like any other unsupported operator.

## Nested slots (depth 2 and deeper)

A slot nests through the adapter's `Select(...)`: the nested slot is declared on the child element, so the same
code path handles any depth.

```csharp
var posts = ctx.From<Post>()
    .Where(p => p.Id == 1)
    .Select(p => new
    {
        p.Id,
        Comments = p.Comments
            .AsEntityBuilder<Comment>()
            .Where(c => c.Active)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                Reactions = c.Reactions
                    .AsEntityBuilder<Reaction>()
                    .OrderBy(r => r.Kind)
                    .Limit(3)
                    .Select(r => r.Kind)
                    .ToList(),
            })
            .ToList(),
    })
    .ToList();
```

- **One statement per level.** In split mode the graph above costs three statements (parent, comments, reactions);
  a three-level chain costs four, and a many-to-many level adds its junction statement. Single-query mode flattens
  the whole graph into one statement. The counts are constant across 1, 3 and 30 roots.
- **Each level filters, orders and pages its own elements.** A `Limit` applies to the element of its **own**
  level: at level one it pages each post's comments, at level two it pages each comment's reactions. Level-one
  ordering is unaffected by a level-two order, and two consecutive levels can order independently.
- **A nested slot must sit inside the adapter's `Select(...)`.** An inline child shape in the direct form that
  itself carries a slot (`Select(p => new { p.Id, X = new { p.Comments } })`) is refused by name: the drawer has no
  way to address a shape built outside the adapter.
- **Many-to-many nests too.** A slot over `Comment → Tag` inside a depth-two graph is loaded with the level's own
  statement set and returns the correct rows, including in single-query mode.

## Split and single-query

Slots are materialized by the eager-loading route, so the **whole projected graph** runs in the mode that route
resolves — split by default, one denormalized statement when the context opts into single-query:

```csharp
var builder = new DataContextBuilder()
    .UseSqlite(connection)
    .UseEagerLoadMode(EagerLoadMode.SingleQuery);
using var ctx = builder.CreateDataContext();

var posts = ctx.From<Post>()
    .Select(p => new { p.Id, p.Comments })
    .ToList();   // one statement for the parent and its comments
```

| Graph | Split (default) | Single-query |
|---|---|---|
| depth 1, one-to-many | 2 | 1 |
| depth 1, many-to-many | 3 (parent + junction + tag) | 1 |
| depth 2 | 3 | 1 |
| depth 2, a many-to-many level | 4 | 1 |
| depth 3 | 4 | 1 |
| same graph, 1 / 3 / 30 roots | same count | same count |

One mode covers every level: a nested level cannot run in a different mode from its root, exactly as for
`LoadWith` graphs (see [Eager loading child collections](eager-loading.md#nested-graphs-level-2-and-deeper)).

## CLR shapes

A slot's **destination member type** must be one of seven shapes over the element type the slot produces:

| Shape | Direct slot | Adapter slot |
|---|---|---|
| `List<T>` | yes | yes (`ToList()`) |
| `T[]` | not expressible | yes (`ToArray()`) |
| `IEnumerable<T>` | yes | yes |
| `ICollection<T>` | yes | yes |
| `IList<T>` | yes | yes |
| `IReadOnlyCollection<T>` | yes | yes |
| `IReadOnlyList<T>` | yes | yes |

The direct form can only declare a destination that is **assignable from the navigation's declared CLR type**, so
a `List<T>`-typed navigation covers the six collection interfaces but never `T[]` — an array destination exists
only through the adapter's `ToArray()`, which materializes a real `T[]`.

**Any other shape fails closed, with no substitution.** A `HashSet<T>`, a custom collection type or an `object`
destination is refused before any SQL is generated:

```
The collection projection slot 'Post.Comments' is declared as 'System.Collections.Generic.HashSet`1[Comment]':
only List<T>, T[], IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T> are
supported, and no other shape is substituted.
```

The same rule applies to an expression that is neither a direct member nor an adapter chain — `.ToHashSet()` on a
declared collection navigation is refused with *"… is not a supported collection slot: declare it as a direct
navigation member or as a navigation adapter (nav.AsEntityBuilder<T>() ... .ToList()/.ToArray())."* A scalar
collection **column** (for example `byte[]`) is not a navigation and stays an ordinary projected value.

## Terminals and the parameterless overloads

A slot is carried by the projected command, so it is executed by the ordinary materializing terminals:

- `QueryCommand<TResult>.ToList()` / `.ToArray()` — the projected result;
- `EntityBuilder<TEntity>.ToList()` / `.ToArray()` — the entity form, when the projection is the whole entity.

Those four **parameterless** overloads are part of the delivered public surface:

```csharp
public static List<TEntity> ToList<TEntity>(this EntityBuilder<TEntity> builder);
public static TEntity[] ToArray<TEntity>(this EntityBuilder<TEntity> builder);
public List<TResult> ToList();
public TResult[] ToArray();
```

They exist because the adapter chain has to be **expressible in an expression tree**: the sibling
`params ReadOnlySpan<object?>` overloads are a `ref struct` with an expanded `params` collection, which an
expression tree cannot reference (`CS8640`/`CS9226`). Each parameterless overload forwards to its `ReadOnlySpan`
twin with an empty span and binds no parameters, so a zero-argument call executes exactly the same query and
existing call sites keep their meaning.

Any other terminal on a slot-bearing command is refused by name — the collection projection is computed after
materialization, so a terminal that would have to stream or aggregate it (for example `ToEnumerable()`) throws
`NotSupportedException` before any SQL runs.

## Projected references and cardinality

A projection that carries a collection slot may also project a **reference** navigation (`Comment.Author`) at any
level. That reference keeps the eager-loading reference contract, scoped to this projection:

- no match → `null`;
- one distinct identity → the materialized entity;
- **more than one distinct identity for one parent** → `InvalidOperationException` before any projected row is
  observable:

```
The reference navigation 'Comment.Author' matched more than one distinct 'Author' for a single parent;
a reference navigation requires at most one related row per parent.
```

Repeated physical rows that share one identity (the same author appearing twice in a cartesian product) are
tolerated and collapse to one instance. The throw happens during materialization — before the projector runs — so
neither a partially filled list nor a partially projected DTO can escape.

## Empty versus null

The two absences stay distinct at every level:

| Situation | Value |
|---|---|
| declared collection with no rows (filtered out, paged away, or none at all) | **empty collection** |
| projected reference with no matching row | **`null`** |
| present reference, present children | entities and collections |

A parent with no children is never dropped from the result: the collection is empty and the parent row stays.

## Refused forms before any SQL

Everything the v1 surface cannot express is rejected **while the projection is bound** — before any SQL is built,
before any command runs and before any plan entry is inserted. Every refusal below was observed with **0
statements** and is a `NotSupportedException`:

| Refused form | Example | Message names |
|---|---|---|
| `Limit` on a many-to-many slot | `p.Tags.AsEntityBuilder<Tag>().OrderBy(t => t.Id).Limit(2).ToList()` | `M:N + Limit` — a per-parent page across a junction has no parent partition in this release; remove the `Limit` or page the materialized collection yourself |
| `GroupBy` inside the adapter | `p.Comments.AsEntityBuilder<Comment>().GroupBy(c => c.AuthorId)…` | the adapter's operator whitelist |
| `Distinct`, `Union`, `Except`, `Intersect` or a join inside the adapter | `…AsEntityBuilder<Comment>().Distinct()…` | the adapter's operator whitelist |
| explicit grouping composed with the projection | `GroupBy` / `SelectGroup` plus a slot | *"cannot be combined with grouping"* |
| an outer terminal other than `ToList`/`ToArray` | `.ToEnumerable()` on the slot-bearing command | *"supports only ToList() and ToArray()"* |
| the in-memory provider | any slot over `InMemoryDataContext` | *"requires a SQL provider"* |
| an unsupported destination shape | `HashSet<T>`, `object`, a custom collection | the seven-shape list |
| a collection expression that is not a slot | `p.Comments.ToHashSet()`, LINQ composition on the bare navigation | *"not a supported collection slot"* |
| a nested slot inside an inline child shape | a child `new { … }` in the direct form that carries a slot | *"nested slot must be declared inside an adapter Select(...)"* |

A rejected projection leaves no trace: it produces no SQL and no cached plan, and the next valid projection on the
same context runs correctly.

## v1 limitations

1. **The projection is computed in memory over fully materialized entities.** SQL selects the entity rows (every
   mapped column of the parent and of each child level); the adapter pipeline and the destination shape are
   applied afterwards. There is **no SQL column pruning and no pushed-down DTO column** in this release — a
   `Select` inside the adapter narrows the projected CLR shape, not the SQL select list.
2. **`M:N + Limit` is refused.** A per-parent page across a junction has no parent partition in v1. An unpaged
   many-to-many slot is supported in both forms, at depth one and nested, with its own ordering; page the
   materialized collection yourself after loading it.
3. **The adapter operator set is closed:** `Where`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`,
   `Limit`, `Select`, terminated by `ToList()`/`ToArray()`. `GroupBy`, `Distinct`, `Union`/`Except`/`Intersect`,
   joins and `Offset` are refused by name before SQL, as are an explicit grouping composition, an unsupported outer
   terminal, an in-memory arm and a custom slot.
4. **A broken reference cardinality throws at materialization.** `InvalidOperationException`, naming the navigation,
   before any partial result; repeated physical rows of one identity collapse normally.
5. **Absence is empty versus `null`:** a missing reference projects `null`, absent children project an empty
   collection at every level.
6. **ClickHouse rejects duplicate select-list aliases.** Two projected sources whose property names are identical
   in one statement (for example a nested many-to-many graph whose projected reference exposes `ParentId` while
   another level exposes `ParentId` too) make the builder emit two aliases with one name, and ClickHouse fails with
   `Code: 179 … MULTIPLE_EXPRESSIONS_FOR_ALIAS`; the other five providers accept it. Give the projected properties
   distinct names (the physical column is unchanged).

## Providers

| Provider | Direct and adapter slots | Split and single-query |
|---|---|---|
| SQLite | yes | yes |
| PostgreSQL | yes | yes |
| SQL Server | yes | yes |
| MySQL | yes | yes |
| MariaDB | yes | yes |
| ClickHouse | yes (distinct projected property names — see the alias limitation) | yes |
| In-memory | rejected fail-closed (`NotSupportedException`) | — |

Provider differences are limited to the SQL the entity query renders; the projected values and statement counts
are the same on every SQL provider.

## See also

- [Eager loading child collections (`LoadWith`)](eager-loading.md) — the loader behind the materialization: modes, assignment rule and ordering guarantees.
- [Relationships and single-query loading (`JoinInto`)](relationships.md) — declaring the metadata a slot needs.
- [Implicit navigation queries](implicit-navigation.md) — the same `AsEntityBuilder<T>` adapter as a query construct (`Any`/`Count`/`LongCount`).
- [Per-parent paging in eager loading](eager-loading-per-parent-paging.md) — a child query's page, the loader-side counterpart of a slot's `Limit`.
- [Projections](../guide/02-projections.md) — the projection forms a slot can appear in.
- [Joins](../guide/04-joins.md) — the explicit join surface.
- [Limitations and out-of-scope features](../advanced/limitations.md).

---

Source: `tests/nextorm.core.tests/EagerLoading/CollectionProjectionSlotTests.cs`,
`tests/nextorm.core.tests/EagerLoading/CollectionProjectionDepthTests.cs`,
`tests/nextorm.core.tests/EagerLoading/CollectionProjectionCardinalityTests.cs`,
`tests/nextorm.<provider>.tests/EagerLoading/CollectionProjectionSqlGenerationTests.cs`,
`tests/nextorm.integration.tests/CommonTestSuite/CollectionProjection/CommonTestSuite.CollectionProjectionLive.cs`.
