# NativeAOT (partial support)

> Publish applications with `PublishAot=true` on .NET 11 for `linux-x64` and the SQLite provider.
> Support is **partial and experimental**: a measured subset of the engine runs natively, and the
> rest is tracked in [#370](https://github.com/AlexeyShirshov/nextorm/issues/370).

**Prerequisites:** [Connections](01-connections.md) · [Value converters and JSON columns](03-value-converters.md).

## What is verified

A published native binary runs against a real SQLite database and is compared with the JIT oracle
row by row (`tests/nextorm.aot.runner` driven by `AotNativeRunTests`). The CI job `aot` publishes
and executes it on every push, and the `publish` job requires that gate.

| Area | Status |
|---|---|
| Target | `net11.0`, `linux-x64` |
| Providers | SQLite |
| Query surface | interface entities, captured predicates, anonymous projections, nullable scalar materialization, SQLite JSON functions (`json_extract`/`json_get_text`), `GROUP BY` with aggregates and `HAVING` |
| `nextorm.extensions` | `SelectWhereMin`/`SelectWhereMax` and `SelectGroup`/`SelectGroupHaving` component slices |
| Parity | every scenario is also executed under JIT and the results are compared |

The output is a self-contained native executable. The packages do **not** carry an
`IsAotCompatible` marker yet, and this page is not a zero-configuration guarantee.

## What is not verified

- SQL Server, PostgreSQL, MySQL, MariaDB and ClickHouse: driver AOT support and native assets are
  unverified;
- the EF Core bridge (`nextorm.entityframeworkcore`);
- `System.Text.Json` converters through a generated `JsonTypeInfo<T>`: serialization remains
  reflection-based, so a consumer still needs the usual `JsonSerializerContext` setup;
- trimming beyond the provider defaults.

The canonical publish of the native runner currently reports **230 unique AOT/trim diagnostics**
(236 raw; measured 2026-11-07). They are neither suppressed nor rooted:

| Code | Count | Nature |
|---|---|---|
| `IL3050` | 124 | runtime code generation — `Expression.Lambda`, `Type.MakeGenericType`, `MethodInfo.MakeGenericMethod`, `Expression.NewArrayInit`, one `Reflection.Emit`, `System.Text.Json` |
| `IL2070` | 30 | reflection over a runtime-typed `Type` |
| `IL2026` | 26 | `RequiresUnreferencedCode` APIs |
| `IL2060` | 22 | statically unanalyzable generic instantiation |
| `IL2055`/`IL2067`/`IL2072`/`IL2075`/`IL2090`/`IL2111` | 28 | metadata that may be trimmed |

The remaining work is architectural: the query pipeline carries result and projection types as a
runtime `Type` (`QueryCommand.ResultType`, `ProjectionEntityItem.EntityType`), so annotations alone
cannot remove these rows. The compile-time pipeline and the per-provider native matrix are the scope
of [#370](https://github.com/AlexeyShirshov/nextorm/issues/370).

## Real-world check

`oc-report` — a reporting utility over a 13 GB SQLite database — runs natively with no source
changes beyond `PublishAot=true`. Publishing and the run happened on one machine (warm page cache,
median of three runs, 3-day window):

| Implementation | AOT publish | Native binary | Run | Peak RSS |
|---|---|---|---|---|
| nextorm, typed queries | only `PublishAot=true` | 8.2 MB | 0.12 s | 30 MB |

The reference app is
[`examples/nextorm.examples.oc-report`](https://github.com/AlexeyShirshov/nextorm/tree/main/examples/nextorm.examples.oc-report).

## Trying it

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
</PropertyGroup>
```

```bash
dotnet publish -c Release
```

The standard NativeAOT prerequisites apply (`clang`, `zlib1g-dev`, a RID-specific native
toolchain). Use the SQLite provider and stay inside the verified query surface above. A query
outside it may fail at runtime; that is expected at this stage.

## Reporting a failure

Open an issue with the smallest failing query, the target RID, the .NET SDK version and the publish
diagnostics. A native reproducer is worth more than a JIT one: a failure caused by trimming appears
only after publishing.
