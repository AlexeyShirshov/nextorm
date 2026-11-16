# D370 Stream B — NativeAOT feature ledger (fail-closed)

Status: S6 evidence, r2 (reconciled to the strict per-cell evidence rule). Scope: every provider that
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
| PostgreSQL | `examples/nextorm.aotexamples.postgres.aviasales`, 10 queries | 10/10 | 10/10 identical |
| SQL Server | `examples/nextorm.aotexamples.mssql.adventureworks`, 11 queries | 11/11 | 11/11 identical |
| MySQL | `tests/nextorm.aot.sqlprobe --provider mysql`, 21 shapes | 21/21 | 21/21 identical |
| MariaDB | `tests/nextorm.aot.sqlprobe --provider mariadb`, 21 shapes | 21/21 | 21/21 identical |
| ClickHouse | `examples/nextorm.aotexamples.clickhouse.analytics`, 9 queries | 9/9 | 9/9 identical (sorted-row-set oracle) |

The shared catalog (`tests/nextorm.aot.scenarios`) holds the shapes that every instrument and the JIT
control execute from one declaration, so a native arm and its control cannot drift. It currently carries
the sixteen SQLite shapes above; the provider instruments keep their own dataset-specific queries because
their schemas differ.

The MySQL/MariaDB probe's twenty-one shapes are: `customer-filter` (typed scalar projection, constant
predicate, `OrderBy`), `orders-page` (`Paging`), `aggregate` (`GroupBy` + `count` + `sum(decimal)`),
`selectgroup` (plain `SelectGroup`), `join`, `cte`, `prepared-reuse` (one command executed twice),
`csv-streaming`, `json-streaming`, `row-streaming` (`ToAsyncEnumerable`), `raw-sql-mapping`
(`ExecuteRaw` with a scalar set and a mapped entity), `dml-builders` (insert/update/delete),
`bulk-insert`, `captured-parameter` (a captured local in the predicate), `aggregate-count-distinct`,
`aggregate-avg`, `aggregate-min`, `aggregate-max`, `having`, `window-function` and `json-column`
(a `[JsonColumn]` write and read through the generated converter).

## Capability matrix

`P` = supported-proven, `D` = supported-by-design, `F` = fail-closed, `U` = not-covered.

| Capability | SQLite | PostgreSQL | SQL Server | MySQL | MariaDB | ClickHouse |
|---|---|---|---|---|---|---|
| Typed scalar projection | P | P | P | P | P | P |
| Anonymous / named shape projection | P | P | P | P | P | P |
| Filter with a captured parameter | P | U | U | P | P | U |
| Ordering | P | P | P | P | P | P |
| Paging (`Paging.Offset/Limit`, `.Limit`) | P | P | P | P | P | P |
| Aggregate `count` | P | P | P | P | P | P |
| Aggregate `count_distinct` | P | P | U | P | P | P |
| Aggregate `sum` | P | P | P | P | P | U |
| Aggregate `avg` | P | P | P | P | P | U |
| Aggregate `min` | P | U | U | P | P | P |
| Aggregate `max` | P | U | P | P | P | P |
| Explicit grouping (`GroupBy`) | P | P | P | P | P | P |
| `HAVING` | P | U | U | P | P | U |
| `SelectGroup` | P | P | P | P | P | P |
| `SelectGroupHaving` | P | U | U | U | U | U |
| Join | P | P | P | P | P | P |
| CTE (`AsCte`) | P | P | P | P | P | P |
| Window functions (`Over`, frames) | P | P | P | P | P | P |
| JSON columns | P | U | U | P | P | U |
| Array / nested types | U | U | U | U | U | P |
| Tuple-typed **result column** | U | U | U | U | U | **F** |
| Decimal binary/comparison operators in rewriting | P | P | P | D | D | U |
| Prepared command reuse / plan cache | P | U | U | P | P | U |
| Eager loading of a declared relationship (split loader) | P | U | U | U | U | U |
| Single-query eager loading (`JoinInto` row projection) | F | U | U | U | U | U |
| Many-to-many `JoinInto` (scalar and composite junction) | F | U | U | U | U | U |
| Value converters (`[JsonColumn]`, custom converter) | P | U | U | U | U | U |
| CSV streaming (`WriteCsv`) | P | U | U | P | P | U |
| JSON streaming (`WriteJson`) | P | U | U | P | P | U |
| Row streaming (`IAsyncEnumerable`) | P | U | U | P | P | U |
| Bulk insert (native and portable) | P | U | U | P | P | U |
| Raw SQL row mapping (`ExecuteRaw`) | P | U | U | P | P | U |
| DML builders (insert/update/delete) | P | U | U | P | P | U |

Cell totals: **121 `supported-proven`, 2 `supported-by-design`, 3 `fail-closed`, 72 `not-covered`**
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
- SQLite, **many-to-many single-query loading**: rejected at declaration, before any SQL, by a
  deterministic `NotSupportedException` naming the run-time-closed link row type and the composite
  junction's `Reflection.Emit` carrier; the `join-into-many-to-many` component slice asserts the
  rejection in the published binary (a silent success would mean the mapper reached a row type the
  image does not carry).
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
  run time. Native output `1500000000:2:t1,t2` equals the JIT control, so the remediation the
  single-query rejection names is itself proven.
- PostgreSQL: the ten-query probe — projection, ordering, paging, `count`/`count_distinct`/`sum`/`avg`,
  `GroupBy`, `SelectGroup`, join, CTE, windows, decimal operators.
- SQL Server: the eleven-query probe — projection, ordering, paging,
  `count`/`sum`/`avg`/`max`, `GroupBy`, `SelectGroup`, join, CTE, windows, decimal operators.
- MySQL / MariaDB: the twenty-one-shape probe — projection, ordering, paging,
  `count`/`count_distinct`/`sum`/`avg`/`min`/`max`, `GroupBy`+`HAVING`, `SelectGroup`, join, CTE,
  windows, a captured predicate parameter, prepared-command reuse, CSV/JSON streaming, row streaming,
  raw-SQL row mapping (scalar and mapped entity), the insert/update/delete builders, bulk insert and a
  `[JsonColumn]` write/read. The JSON cell rests on the `json-column` shape, which stores a model
  through `[JsonColumn(Storage = Text)]` and reads it back through the generated converter, with the
  contract metadata coming from a source-generated `JsonSerializerContext` the generator finds in the
  probe assembly. Both providers
  print the same count and digest per shape under JIT and natively (`csv-streaming` 64/digest
  `40fc6302…`, `json-streaming` 144/`d0f917cb…`, `row-streaming` 11/`93c69ba5…`, `raw-sql-mapping`
  85/`a3ddc5a1…`, `dml-builders` 36/`ca5752cc…`, `bulk-insert` 44/`ad788c31…`). The probe references
  the entity generator, because a mapped entity used only by `ExecuteRaw<T>` has no query of its own to
  keep its members: without the generated description the trimmed mapping came back empty and the raw
  read failed closed.
- ClickHouse: the nine-query probe — projection, ordering, paging,
  `count`/`count_distinct`/`min`/`max`, `GroupBy`, `SelectGroup`, join, CTE, windows, arrays, and the
  fail-closed tuple column.

### The only `supported-by-design` cells

MySQL and MariaDB, **decimal binary/comparison operators in rewriting**: the decimal requirement is a
compile-time `[DynamicallyAccessedMembers]` contract declared in `src/nextorm.core/Visitors/NumericOperators.cs`
and reached from every query preparation, independent of the provider assembly; the provider adds no
operator resolution and no run-time type. The provider is therefore not expected to differ from the three
providers where the shape is `supported-proven`. The probe's `sum(decimal)` exercises decimal mapping and
the aggregate, not `Expression.MakeBinary`; the cell is deliberately not `P`.

## Fail-closed entries (native-proven)

| Provider | Shape | Rejection | Evidence |
|---|---|---|---|
| ClickHouse | `Tuple<...>` result column of element types other than the driver's registration sample | `NotSupportedException` at preparation naming `ClickHouse.Driver` `TupleType.Read`/`Activator.CreateInstance` and the remediation (`tupleElement(...)` columns or `toString(...)` + client parse) | native `Retention` run, `.pdca/370/rv1/attempts/s5-probe-clickhouse-retention-failclosed.log` |
| All providers | `TimeOnly` projection without a converter | `NotSupportedException` at preparation (provider-specific text) | JIT suite; deterministic preparation-time rejection (not native-proven, listed for completeness only) |
| All providers, native image | Single-query eager loading (`JoinInto`, or `LoadWith` with `EagerLoadMode.SingleQuery`) | `NotSupportedException` at declaration naming the run-time-closed `Projection<…>` row type and pointing at the split loader | native `join-into-single-query` component slice in `native-run.log` (the child reports the rejection, the harness asserts it) |
| All providers, native image | Many-to-many eager loading in one query (`JoinInto`, or `LoadWith` with `EagerLoadMode.SingleQuery`) | `NotSupportedException` at declaration naming the run-time-closed link row type (a composite junction would additionally need a carrier emitted with `Reflection.Emit`), pointing at the split loader | native `join-into-many-to-many` component slice in `native-run.log`; the split loader stays available for the same navigation |
| PostgreSQL / SQL Server | `System.Tuple` with more than seven elements in a raw ROW | `NotSupportedException` | JIT suite (`RawMapperFactory` guard) |

The ClickHouse row, the tuple-column row, the single-query eager-load row and the many-to-many
eager-load row are native-proven; the other two are JIT-only and are not counted as native fail-closed
evidence.

## What this ledger does not claim

- It does not claim whole-surface NativeAOT support. The canonical registry still lists 215 residual
  diagnostic rows (`.pdca/370/rv1/registry-s6.tsv`); each remains tracked by the campaign.
- `U` cells are unknowns of this ledger, not refusals and not support.
- The MySQL/MariaDB instrument proves the twenty-one shapes it runs (three customers, six orders, one
  write table, one JSON table), not the providers' whole surface.
- The streaming, raw-SQL, DML and bulk shapes run on SQLite, MySQL and MariaDB only. PostgreSQL,
  SQL Server and ClickHouse keep those cells `U` until their dataset-backed instruments run the shapes;
  the same holds for the captured-parameter, `count_distinct`/`avg`/`min`/`max`, `HAVING`, window and
  JSON cells that only the MySQL/MariaDB probe and the SQLite harness execute.
