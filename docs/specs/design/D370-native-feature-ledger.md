# D370 Stream B — NativeAOT feature ledger (fail-closed)

Status: S6 evidence, r4 (reconciled to the strict per-cell evidence rule). Scope: every provider that
ships a `nextorm.*` data context, with the NativeAOT status of each query/mapping capability.

## Classification rule (strict, per cell)

| Class | Required evidence for that exact provider and capability |
|---|---|
| `supported-proven` | A published NativeAOT binary of that provider's instrument executed **that shape** against a live database and produced the same `(row_count, digest)` as the JIT run of the same source. |
| `supported-by-design` | No native run of the shape, but a **capability-specific** structural argument is recorded here: the path contains no run-time generic instantiation, no reflection over a run-time type and no `Expression.Compile` of a run-time-closed delegate, and the mechanism is shared provider-independent code. A blanket argument is not acceptable. |
| `fail-closed` | Rejected before execution with a deterministic exception naming the limitation; no silent fallback. |
| `not-covered` | No native instrument exercises the shape for that provider. Explicitly unknown; never read as supported. |

Identity rules: `SelectGroup` does not prove `SelectGroupHaving`; a constant predicate does not prove
captured parameters; an aggregate over a decimal column does not prove decimal binary-operator rewriting;
paging through `Paging` is not implied by an aggregate query.

## Instruments (what was actually executed)

| Provider | Instrument | Native | JIT comparison |
|---|---|---|---|
| SQLite | `tests/nextorm.aot.runner`, 37 scenarios (21 harness + 16 shared-catalog shapes) | 37/37 | equal per scenario |
| PostgreSQL | `tests/nextorm.aot.sqlprobe --provider postgres`, 30 shapes; `examples/nextorm.aotexamples.postgres.aviasales`, 10 queries | 30/30 + 10/10 | identical |
| SQL Server | `tests/nextorm.aot.sqlprobe --provider sqlserver`, 30 shapes; `examples/nextorm.aotexamples.mssql.adventureworks`, 11 queries | 30/30 + 11/11 | identical |
| MySQL | `tests/nextorm.aot.sqlprobe --provider mysql`, 30 shapes | 30/30 | identical |
| MariaDB | `tests/nextorm.aot.sqlprobe --provider mariadb`, 30 shapes | 30/30 | identical |
| ClickHouse | `tests/nextorm.aot.sqlprobe --provider clickhouse`, 30 shapes; `examples/nextorm.aotexamples.clickhouse.analytics`, 9 queries | 30/30 + 9/9 | identical (the analytics example compares sorted row sets) |

The shared catalog (`tests/nextorm.aot.scenarios`) holds the shapes that every SQLite instrument and the
JIT control execute from one declaration, so a native arm and its control cannot drift. It currently
carries the sixteen SQLite shapes above.

`tests/nextorm.aot.sqlprobe` is the cross-provider instrument: it provisions its own tiny schema in the
provider's own dialect and seeds it with raw ADO, so no shape depends on a dataset image, and the same
thirty shapes run against all five server providers. Tabular shapes print a row count and digest of the
formatted rows; eager-loading shapes also print the loaded graph and successful SQL-command count. The
published native output is compared with a genuine JIT control built with `PublishAot=false`: the JIT
reports `dynamic-code-supported=True`, `dynamic-code-compiled=True`, while the native executable reports
both flags as `False`. All 30 shapes passed on each provider and the JIT/native outcomes matched. The
shapes are: `customer-filter` (typed scalar projection, constant predicate, `OrderBy`),
`orders-page` (`Paging`), `aggregate` (`GroupBy` + `count` + `sum(decimal)`), `selectgroup`,
`selectgroup-having`, `join`, `cte`, `prepared-reuse` (one command executed twice), `csv-streaming`,
`json-streaming`, `row-streaming` (`ToAsyncEnumerable`), `raw-sql-mapping` (`ExecuteRaw`, a scalar set and
a mapped entity), `dml-builders` (insert/update/delete), `bulk-insert`, `captured-parameter` (a captured
local compared against a decimal column), `aggregate-count-distinct`, `aggregate-avg`, `aggregate-min`,
`aggregate-max`, `having`, `window-function`, `json-column` (a `[JsonColumn]` write and read through the
generated converter), `relationship-load-with` (the split loader over an attribute-declared
`[Relationship]`), `value-converters` (a `[ValueConverter]` written and read back),
`eager-load-single-query`, `join-into-collection`, `eager-load-many-to-many`,
`join-into-many-to-many`, `eager-load-composite-many-to-many` and
`join-into-composite-many-to-many`.

The probe's write shapes use explicit primary keys instead of an identity column: ClickHouse has no
identity, and a fixed key keeps the native arm and its control from ever printing a generated value. The
ClickHouse schema is a `MergeTree` per table (`ORDER BY id`), and its delete/update shapes run through
synchronous `ALTER TABLE … DELETE/UPDATE` mutations. The SQL Server driver refuses to open a connection in
globalization-invariant mode, so the probe keeps globalization enabled and pins the default culture to the
invariant one, which preserves the digest determinism.

## Capability matrix

`P` = supported-proven, `D` = supported-by-design, `F` = fail-closed, `U` = not-covered.

| Capability | SQLite | PostgreSQL | SQL Server | MySQL | MariaDB | ClickHouse |
|---|---|---|---|---|---|---|
| Typed scalar projection | P | P | P | P | P | P |
| Anonymous / named shape projection | P | P | P | P | P | P |
| Filter with a captured parameter | P | P | P | P | P | P |
| Ordering | P | P | P | P | P | P |
| Paging (`Paging.Offset/Limit`, `.Limit`) | P | P | P | P | P | P |
| Aggregate `count` | P | P | P | P | P | P |
| Aggregate `count_distinct` | P | P | P | P | P | P |
| Aggregate `sum` | P | P | P | P | P | P |
| Aggregate `avg` | P | P | P | P | P | P |
| Aggregate `min` | P | P | P | P | P | P |
| Aggregate `max` | P | P | P | P | P | P |
| Explicit grouping (`GroupBy`) | P | P | P | P | P | P |
| `HAVING` | P | P | P | P | P | P |
| `SelectGroup` | P | P | P | P | P | P |
| `SelectGroupHaving` | P | P | P | P | P | P |
| Join | P | P | P | P | P | P |
| CTE (`AsCte`) | P | P | P | P | P | P |
| Window functions (`Over`, frames) | P | P | P | P | P | P |
| JSON columns | P | P | P | P | P | P |
| Array / nested types | U | U | U | U | U | P |
| Tuple-typed **result column** | U | U | U | U | U | **F** |
| Decimal binary/comparison operators in rewriting | P | P | P | P | P | P |
| Prepared command reuse / plan cache | P | P | P | P | P | P |
| Eager loading of a declared relationship (split loader) | P | P | P | P | P | P |
| Single-query eager loading (`JoinInto` row projection) | P | P | P | P | P | P |
| Many-to-many `JoinInto` (scalar and composite junction) | P | P | P | P | P | P |
| Value converters (`[JsonColumn]`, custom converter) | P | P | P | P | P | P |
| CSV streaming (`WriteCsv`) | P | P | P | P | P | P |
| JSON streaming (`WriteJson`) | P | P | P | P | P | P |
| Row streaming (`IAsyncEnumerable`) | P | P | P | P | P | P |
| Bulk insert (native and portable) | P | P | P | P | P | P |
| Raw SQL row mapping (`ExecuteRaw`) | P | P | P | P | P | P |
| DML builders (insert/update/delete) | P | P | P | P | P | P |

Cell totals: **187 `supported-proven`, 0 `supported-by-design`, 1 `fail-closed`, 10 `not-covered`**
(198 cells across 33 capabilities and six providers; counted mechanically across the matrix rows).

What each `P` rests on:

- SQLite: the thirty-seven-scenario native harness — typed/anonymous projection, captured parameter,
  ordering, `count`/`sum`, `GroupBy`+`HAVING`, `SelectGroup`, `SelectGroupHaving`, SQLite JSON functions,
  decimal operators, correlated subqueries, generated row materializers and the streaming writers, plus
  the sixteen shared-catalog shapes (`tests/nextorm.aot.scenarios`): paging, `count_distinct`, `avg`,
  `min`, `max`, join, CTE, two executions of one prepared command, a window aggregate, the
  `value-converters` shape, `relationship-load-with`, `row-streaming`, `raw-sql-mapping`, `dml-builders`,
  `bulk-insert` and `many-to-many-load-with`. The window cell rests on `OVER ()`; nextorm has no frame-specifier API (`ROWS
  BETWEEN`), so `OVER` is the whole surface the cell can name.
- SQLite, **CSV/JSON streaming**: the `streaming-writers` harness scenario writes a flat projection of
  `simple_entity` through `WriteCsv` and `WriteJson` and compares the full text (CRLF included) with the
  JIT control; the `CsvStreamOptions` and `JsonShapePlan` component slices assert the writers run in the
  published binary at all.
- SQLite, **row streaming, raw SQL, DML builders, bulk insert**: four catalog shapes, each idempotent
  (a fixed marker scoped by delete-before/delete-after, no generated key ever printed, so the JIT control
  and the native arm can share the database). `row-streaming` drains `ToAsyncEnumerable` one row at a
  time (`1,2,3,4,5,6,7,8,9,10`); `raw-sql-mapping` reads a scalar set and a mapped entity through
  `ExecuteRaw` (`scalars=10,20,30;mapped=10:Buyer;20:Approver;30:Unlinked`); `dml-builders` inserts with
  `ReturningIdentity`, updates, reads back and deletes (`cleaned=0;updated=1;ages=8;removed=1`);
  `bulk-insert` writes five rows in one command (`cleaned=0;written=5;ages=0,1,2,3,4;removed=5`). All
  four match the JIT control exactly.
- SQLite, **single-query eager loading and `JoinInto`**: the expanded native runner and its JIT
  control match for ordinary and nested `LoadWith(SingleQuery)` graphs, scalar and composite
  many-to-many graphs, mixed `int`/`long` keys, a converted key and nullable composite keys. The
  one-command graph cases pass. Full output and runner exit evidence:
  `/tmp/opencode/aot-joininto-20261116/native-evidence-focused-r1/native-run.log` and
  `/tmp/opencode/aot-joininto-20261116/acceptance-focused-handoff.md`.
- SQLite, **JSON columns** and **value converters**: the `value-converters` catalog shape writes a
  `[JsonColumn(Storage = Text)]` property and a `[ValueConverter]` property through `Update` and reads
  both back through the converter (`1:12.5:native/42;2:-3:null/null`). The mapping comes from the
  compile-time entity description, so the converter is constructed by generated code
  (`JsonColumnConverters.Create<TModel>` / `new TConverter()`) rather than by `MakeGenericType` +
  `Activator`; the JSON contract metadata comes from a source-generated `JsonSerializerContext` that the
  generator finds in the same assembly and passes to the converter, which is what a trimmed application
  needs.
- SQLite, **eager loading of a declared relationship**: the `relationship-load-with` catalog shape loads
  `[Relationship(ForeignKey = ...)] ICollection<Order>` off three customers with `LoadWith`, and the
  navigation, its keys and both mappings come from the compile-time entity description: the generator now
  reproduces the `[Relationship]` declaration (collection vs reference navigation, ordered foreign and
  principal components, the dependent/principal side) instead of leaving the type on the reflective path.
  Native output `10:Buyer[1];20:Approver[2];30:Unlinked[]` equals the JIT control, so the one-to-many
  shape, the empty-parent case and the split loader all run natively.
- SQLite, **eager loading of a many-to-many navigation**: the `many-to-many-load-with` catalog shape
  declares `HasManyThrough<Tag, Link, int, int>` and loads the tags with `LoadWith` (the default split
  loader), which reads the junction as an ordinary typed entity instead of closing a link row type at
  run time. Native output `1500000000:2:t1,t2` equals the JIT control, independently covering the
  split-loader path.
- Every provider, **decimal binary/comparison operators in rewriting**: the `captured-parameter` shape
  compares a decimal column against a captured decimal local, so preparation rebuilds that binary node
  with `Expression.MakeBinary` over operand types only known at run time — the seam the earlier
  structural argument covered. It runs on the published native binary of all six providers, so those
  cells are `P` rather than `D`.
- PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse: the thirty-shape probe, identical under JIT
  and natively on every provider. The seed is the same everywhere, so most digests are shared across the
  five providers (`customer-filter` 2/`9971efbc…`, `orders-page` 3/`71a626d4…`, `aggregate`
  3/`b78cc7c1…`, `selectgroup` 3/`8663d521…`, `selectgroup-having` 3/`1206c50c…`, `join`
  6/`702bc5e2…`, `cte` 3/`8bed2d87…`, `prepared-reuse` 24/`d5116e23…`, `csv-streaming`
  64/`40fc6302…`, `json-streaming` 144/`d0f917cb…`, `row-streaming` 11/`93c69ba5…`,
  `raw-sql-mapping` 85/`a3ddc5a1…`, `captured-parameter` 20/`53def4cb…`, `aggregate-count-distinct`
  1/`4e074085…`, `aggregate-min` 1/`6b86b273…`, `aggregate-max` 1/`e7f6c011…`, `having`
  3/`1206c50c…`, `window-function` 6/`e50dfff0…`, `json-column` 2/`84d47cfb…`,
  `relationship-load-with` 2/`a7365ecf…`, `value-converters` 3/`c77388f0…`). Where the engines differ the
  digests differ by design and are compared per provider: `aggregate-avg` is 19/`8df97053…` on
  PostgreSQL (numeric scale), 9/`d5621c7a…` on SQL Server, MySQL and MariaDB and 30/`83da4bf8…` on
  ClickHouse; the ClickHouse mutations report `dml-builders` 36/`3d817595…` and `bulk-insert`
  44/`b22040db…` where the other engines report `ca5752cc…` and `ad788c31…`.
- PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse, **JSON columns**: the `json-column` shape stores a
  model through `[JsonColumn(Storage = Text)]` and reads it back through the generated converter, with the
  contract metadata coming from a source-generated `JsonSerializerContext` the generator finds in the
  probe assembly.
- PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse, **single-query and many-to-many eager loading**:
  all six added graph shapes passed in both real JIT and native runs on every provider. The graph and
  command-count oracles were `1[10,11];2[];commands=1` (single-query eager load and `JoinInto`
  collection), `1[10,10,11];2[12];3[];commands=1` (scalar many-to-many), and
  `1[10,10,11];2[11];3[];commands=1` (composite many-to-many). `ProbeCommandCounter` verifies one
  successful command and zero command failures for each graph. The JIT arm reported dynamic-code
  support/compilation `True/True`; the native arm reported `False/False`. Per-provider logs and full
  command evidence are listed in `/tmp/opencode/aot-joininto-20261116/server-native-handoff.md`.
- The probe references the entity generator, because a mapped entity used only by `ExecuteRaw<T>` has no
  query of its own to keep its members: without the compile-time description the trimmed mapping came
  back empty and the raw read failed closed on the native build (found on MySQL, fixed for every
  provider).

## Remaining rejection evidence (native status distinguished)

| Provider | Shape | Rejection | Evidence |
|---|---|---|---|
| ClickHouse | `Tuple<...>` result column of element types other than the driver's registration sample | `NotSupportedException` at preparation naming `ClickHouse.Driver` `TupleType.Read`/`Activator.CreateInstance` and the remediation (`tupleElement(...)` columns or `toString(...)` + client parse) | native `Retention` run, `.pdca/370/rv1/attempts/s5-probe-clickhouse-retention-failclosed.log` |
| All providers | `TimeOnly` projection without a converter | `NotSupportedException` at preparation (provider-specific text) | JIT suite; deterministic preparation-time rejection (not native-proven, listed for completeness only) |
| PostgreSQL / SQL Server | `System.Tuple` with more than seven elements in a raw ROW | `NotSupportedException` | JIT suite (`RawMapperFactory` guard) |

The ClickHouse tuple-column row is the remaining native-proven fail-closed cell in the matrix. The
`TimeOnly` and raw `System.Tuple` rows are JIT-only and are not counted as native fail-closed evidence.

## What this ledger does not claim

- It does not claim whole-surface NativeAOT support. The canonical registry still lists 215 residual
  diagnostic rows (`.pdca/370/rv1/registry-s6.tsv`); each remains tracked by the campaign.
- `U` cells are unknowns of this ledger, not refusals and not support.
- The probe proves the thirty shapes it runs on the tiny seeded schema (three customers, six
  orders, one write table, one JSON table, one parent/child pair, one converter table), not the
  providers' whole surface.
- The ten `U` cells are: array/nested types (every provider except ClickHouse) and the tuple-typed result
  column (every provider except ClickHouse, where it is `F`). They stay unknown until a native instrument
  exercises them.
