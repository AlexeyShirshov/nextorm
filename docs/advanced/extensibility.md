# Extensibility: custom SQL dialects and renderers

> nextorm renders SQL through an [`ISqlDialect`](xref:NextORM.Core.ISqlDialect); the supported extension boundary lets an external assembly supply its own dialect and, for the native extreme-row strategy, its own renderer — without friend access.

**Prerequisites:** [Native extreme-row strategies (`SelectWhereMax` / `SelectWhereMin`)](select-where-extrema-native.md) · [Provider overview](../connecting-providers/overview.md) · [API quick reference](api-reference.md)

## Overview

The native extreme-row strategy is a public dialect capability, not a hard-coded provider branch:

- [`ISqlDialect.ExtremeRowRenderer`](xref:NextORM.Core.ISqlDialect.ExtremeRowRenderer) is the optional renderer, declared as a default interface member returning `null`; [`SqlDialectBase.ExtremeRowRenderer`](xref:NextORM.Core.SqlDialectBase.ExtremeRowRenderer) overrides it virtually with the same default. `null` means no native strategy.
- [`IExtremeRowRenderer`](xref:NextORM.Core.IExtremeRowRenderer) exposes `CanRender(`[`ExtremeRowDescription`](xref:NextORM.Core.ExtremeRowDescription)`)`, a side-effect-free decision taken before any SQL is assembled, and `Render(`[`ExtremeRowRenderRequest`](xref:NextORM.Core.ExtremeRowRenderRequest)`)`, which returns the winning-row source. Only a positive `CanRender` leads to `Render`, and `Render` has no late fallback to the portable path.
- [`ExtremeRowRenderColumn`](xref:NextORM.Core.ExtremeRowRenderColumn), [`ExtremeRowDescription`](xref:NextORM.Core.ExtremeRowDescription) and [`ExtremeRowRenderRequest`](xref:NextORM.Core.ExtremeRowRenderRequest) carry only shape facts (CLR type, nullability, direct-mapped/converter flags, and the prepared source/aliases), never the expression or build context. See the [API quick reference](api-reference.md).
- Each of the three DTOs also exposes a **public eager constructor**, so an assembly without friend access can build them and drive its own [`IExtremeRowRenderer`](xref:NextORM.Core.IExtremeRowRenderer). Construction validates references and collection elements immediately and structurally: a required reference that is `null` throws `ArgumentNullException`; a `null` element, an empty-string alias element or a `SourceSql` that is empty/whitespace-only throws `ArgumentException`; an undeclared `KeywordCase` value throws `ArgumentException`. It performs **no** SQL parsing or provider-type/security validation, adds no minimum-count, cross-list cardinality or alias-disjointness rule (empty shapes and overlapping aliases are allowed), never reads `ExtremeRowDescription.Payload`, and never calls `CanRender`/`Render`. The collections are **borrowed**, not copied and not made immutable: the same instance is observed through the property, and a caller must not mutate it while the DTO is in use, including from a concurrent caller. The core's internal lazy payload/candidate-preparation path (and the unvalidated trusted request construction used on the hot path) remains an implementation detail and is **not** public API.

`PostgresDialect` and `ClickHouseDialect` are deliberately **not `sealed`**: keeping them inheritable, together with their public constructors (`PostgresDialect()`, `PostgresDialect(Version?)`, `ClickHouseDialect()`), the shared `Instance` singleton and the virtual `ExtremeRowRenderer` hook, is the supported external extension boundary — a public compatibility commitment, not a test detail. A custom dialect can derive from either provider dialect, override the hook and install its own public [`IExtremeRowRenderer`](xref:NextORM.Core.IExtremeRowRenderer) implementation; the built-in renderers (`PostgresExtremeRowRenderer`, `ClickHouseExtremeRowRenderer`) are `internal` and are not part of that contract. Returning `null` from the override — or installing a renderer whose `CanRender` answers `false` — keeps the portable window-function lowering, and the provider defaults and constructors are unchanged.

## Creating a custom dialect

```csharp
// A non-friend assembly subclasses the provider dialect and installs a custom renderer.
public sealed class MyPostgresDialect : PostgresDialect
{
    public override IExtremeRowRenderer? ExtremeRowRenderer => MyRenderer.Instance;
}

// Only the global (ungrouped) single-key form is handled natively; every other shape
// falls back to the portable lowering because CanRender answers false.
public sealed class MyRenderer : IExtremeRowRenderer
{
    public static readonly MyRenderer Instance = new();

    public bool CanRender(ExtremeRowDescription description)
        => description.Groups.Count == 0 && description.Keys.Count == 1;

    public string Render(ExtremeRowRenderRequest request)
    {
        var direction = request.IsMax ? "desc" : "asc";
        return request.SourceSql
            + " order by \"" + request.KeyAliases[0] + "\" " + direction + " limit 1";
    }
}

// The second example: returning null from the override forces the portable window-function
// lowering for every request, without installing a renderer.
public sealed class PortablePostgresDialect : PostgresDialect
{
    public override IExtremeRowRenderer? ExtremeRowRenderer => null;
}
```

## Registering a custom dialect

Creating the dialect subclass is only half of it — the context has to be told to use it. [`DataContext.Dialect`](xref:NextORM.Core.DataContext.Dialect) is an **`abstract`** property (`src/nextorm.core/DataContext/DataContext.cs:153`), and every provider context overrides it to return its built-in dialect: `PostgresDataContext` and `MariaDbDataContext` return the instance they built at construction, while `SqliteDataContext`, `MySqlDataContext`, `SqlServerDataContext` and `ClickHouseDataContext` return a shared `Instance` singleton. There is no `DataContextBuilder`/`UseDialect` switch; the supported way to install a custom dialect is to subclass the **provider context** and override `Dialect` to return your dialect:

```csharp
public sealed class MyPostgresDataContext : PostgresDataContext
{
    private readonly ISqlDialect _dialect = new MyPostgresDialect();

    public MyPostgresDataContext(string connectionString, DataContextBuilder builder)
        : base(connectionString, builder)
    {
    }

    public override ISqlDialect Dialect => _dialect;
}

using var ctx = new MyPostgresDataContext(connectionString, new DataContextBuilder());

// Reaches the database through MyPostgresDialect → MyRenderer.
var top = ctx.From<Player>().SelectWhereMax(p => p.Score).ToList();
```

The same shape works for `MyClickHouseDataContext : ClickHouseDataContext` (`ClickHouseDialect` is likewise not `sealed`). The plan cache is keyed by the **context type**, so derive a distinct `DataContext` subclass per dialect — the same rule the PostgreSQL server-version guard already states. Installing a dialect that returns `null` from `ExtremeRowRenderer` (or a renderer whose `CanRender` answers `false`) keeps the portable window-function lowering.

## See also

- [Native extreme-row strategies (`SelectWhereMax` / `SelectWhereMin`)](select-where-extrema-native.md)
- [API quick reference](api-reference.md)
- [Provider overview](../connecting-providers/overview.md)

---

Source: `src/nextorm.core/DataContext/Dialect/**`, `src/nextorm.postgres/**`, `src/nextorm.clickhouse/**` (XML doc comments are the authoritative API documentation).
