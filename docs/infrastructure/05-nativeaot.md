# NativeAOT (partial support)

> Publish applications with `PublishAot=true` on .NET 11 for `linux-x64`.
> Support is **partial and experimental**: a measured subset of the engine runs natively, and the
> rest is tracked in [#370](https://github.com/AlexeyShirshov/nextorm/issues/370).

**Prerequisites:** [Connections](01-connections.md) · [Value converters and JSON columns](03-value-converters.md).

## What is verified

A published native binary runs against a real database and is compared with the JIT oracle
row by row. Two harnesses do this: the SQLite runner (`tests/nextorm.aot.runner`, driven by
`AotNativeRunTests`) and the provider probes under `examples/` plus the self-provisioning
`tests/nextorm.aot.sqlprobe` for MySQL and MariaDB. The harness is **fail-closed**: a native
scenario that fails, or whose row count/digest differs from its JIT control, fails the test.

| Area | Status |
|---|---|
| Target | `net11.0`, `linux-x64` |
| Providers | SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse (the measured surface differs per provider, see below) |
| Query surface | typed and anonymous projections, filters, ordering, paging, aggregates, `GROUP BY`/`HAVING`, joins, CTEs, window functions (where the provider supports them), SQLite JSON functions, ClickHouse arrays, `SelectGroup`/`SelectGroupHaving`, `SelectWhereMin`/`SelectWhereMax`, prepared-command reuse |
| Parity | every scenario is also executed under JIT; the harness fails on any status or value divergence |

Measured native results (JIT values are equal in every case):

| Provider | Instrument | Native result |
|---|---|---|
| SQLite | `tests/nextorm.aot.runner`, 18 scenarios + 4 component slices | **18/18** |
| PostgreSQL | `examples/nextorm.aotexamples.postgres.aviasales`, 10 queries | **10/10** |
| SQL Server | `examples/nextorm.aotexamples.mssql.adventureworks`, 11 queries | **11/11** |
| MySQL | `tests/nextorm.aot.sqlprobe --provider mysql`, 7 shapes | **7/7** |
| MariaDB | `tests/nextorm.aot.sqlprobe --provider mariadb`, 7 shapes | **7/7** |
| ClickHouse | `examples/nextorm.aotexamples.clickhouse.analytics`, 9 queries | **9/9** |

The output is a self-contained native executable. The packages do **not** carry an
`IsAotCompatible` marker yet, and this page is not a zero-configuration guarantee.

## What is not verified

- **Correlated subqueries are covered for six shapes.** `Any`, `Count`, `Sum` over a nullable
  selector, a projection terminal (`FirstOrDefault`), correlation depth 2 and `exists(...)` all run in
  the native runner and produce the JIT-equal values. A reference navigation used *inside* a
  correlated body and a correlated `APPLY` source are not part of the measured native surface yet —
  `APPLY` is not expressible on the SQLite instrument at all and is covered by the provider SQL
  generation suites instead.
- A **tuple-typed ClickHouse result column** whose element types fall outside the driver's fixed
  registration sample fails closed with a clear `NotSupportedException` at preparation; project the
  tuple elements as separate columns or read the tuple as text instead.
- EF Core bridge (`nextorm.entityframeworkcore`), `System.Text.Json` converters through a generated
  `JsonTypeInfo<T>` (serialization remains reflection-based, so a consumer still needs the usual
  `JsonSerializerContext` setup), bulk insert, raw SQL row mapping and DML builders are not part of
  the measured native surface.
- Trimming beyond the provider defaults.

### Provider probes

The probes under `examples/` run the nextorm arm of each comparison example without linq2db,
Testcontainers or BenchmarkDotNet, so the process can be published natively and compared with a JIT
run by row counts and row digests. The MySQL/MariaDB probe provisions its own tiny schema and seed,
so it needs no dataset. Measured 2026-11-13, `linux-x64`:

| Probe | Native binary | Result |
|---|---|---|
| `nextorm.aotexamples.postgres.aviasales` | 18 MB | **10/10** queries, digests equal to JIT |
| `nextorm.aotexamples.mssql.adventureworks` | 28 MB | **11/11** |
| `nextorm.aotexamples.clickhouse.analytics` | 16 MB | **9/9** |
| `tests/nextorm.aot.sqlprobe` (MySQL / MariaDB) | — | **7/7** each |

The three failure classes that the probes originally reported — `decimal` operators removed by
trimming, `System.Tuple` construction discovered through reflection, and runtime expression-tree
compilation — are fixed and are no longer observed in any probe.

## Residual diagnostics

The canonical publish of the native runner reports **202 unique AOT/trim diagnostics** (208 raw;
measured 2026-11-13, against the 18-scenario runner). They are neither suppressed nor rooted:

| Code | Count | Nature |
|---|---|---|
| `IL3050` | 110 | runtime code generation — `Expression.Lambda`, `Type.MakeGenericType`, `MethodInfo.MakeGenericMethod`, `Expression.NewArrayInit`, `System.Text.Json` |
| `IL2026` | 29 | `RequiresUnreferencedCode` APIs |
| `IL2070` | 27 | reflection over a runtime-typed `Type` |
| `IL2075` | 15 | member access on a runtime-typed value |
| `IL2060` | 15 | statically unanalyzable generic instantiation |
| `IL2090` | 5 | generic parameter with `DynamicallyAccessedMembers` requirements |
| `IL2072` | 3 | `DynamicallyAccessedMembers` requirement not satisfied |
| `IL2055` | 2 | `MakeGenericType` over a runtime type |
| `IL2111` | 1 | reflection over a type with annotations |
| `IL2067` | 1 | parameter annotated with `DynamicallyAccessedMembers` |

The remaining work is architectural: the query pipeline carries result and projection types as a
runtime `Type` (`QueryCommand.ResultType`, `ProjectionEntityItem.EntityType`) and resolves some
generic members from it. Annotations alone cannot remove these rows; the compile-time pipeline and
the per-provider native matrix are the scope of
[#370](https://github.com/AlexeyShirshov/nextorm/issues/370).

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
toolchain). Use one of the verified providers and stay inside the measured query surface above. A
query outside it may fail at runtime; that is expected at this stage.

## Reporting a failure

Open an issue with the smallest failing query, the target RID, the .NET SDK version and the publish
diagnostics. A native reproducer is worth more than a JIT one: a failure caused by trimming appears
only after publishing.
