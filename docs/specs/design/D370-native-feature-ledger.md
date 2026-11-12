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
| SQLite | `tests/nextorm.aot.runner`, 9 scenarios | 9/9 | equal per scenario |
| PostgreSQL | `examples/nextorm.aotexamples.postgres.aviasales`, 10 queries | 10/10 | 10/10 identical |
| SQL Server | `examples/nextorm.aotexamples.mssql.adventureworks`, 11 queries | 11/11 | 11/11 identical |
| MySQL | `tests/nextorm.aot.sqlprobe --provider mysql`, 7 shapes | 7/7 | 7/7 identical |
| MariaDB | `tests/nextorm.aot.sqlprobe --provider mariadb`, 7 shapes | 7/7 | 7/7 identical |
| ClickHouse | `examples/nextorm.aotexamples.clickhouse.analytics`, 9 queries | 9/9 | 9/9 identical (sorted-row-set oracle) |

The MySQL/MariaDB probe's seven shapes are: `customer-filter` (typed scalar projection, constant
predicate, `OrderBy`), `orders-page` (`Paging`), `aggregate` (`GroupBy` + `count` + `sum(decimal)`),
`selectgroup` (plain `SelectGroup`), `join`, `cte`, `prepared-reuse` (one command executed twice).

## Capability matrix

`P` = supported-proven, `D` = supported-by-design, `F` = fail-closed, `U` = not-covered.

| Capability | SQLite | PostgreSQL | SQL Server | MySQL | MariaDB | ClickHouse |
|---|---|---|---|---|---|---|
| Typed scalar projection | P | P | P | P | P | P |
| Anonymous / named shape projection | P | P | P | P | P | P |
| Filter with a captured parameter | P | U | U | U | U | U |
| Ordering | P | P | P | P | P | P |
| Paging (`Paging.Offset/Limit`, `.Limit`) | U | P | P | P | P | P |
| Aggregate `count` | P | P | P | P | P | P |
| Aggregate `count_distinct` | U | P | U | U | U | P |
| Aggregate `sum` | P | P | P | P | P | U |
| Aggregate `avg` | U | P | P | U | U | U |
| Aggregate `min` | U | U | U | U | U | P |
| Aggregate `max` | U | U | P | U | U | P |
| Explicit grouping (`GroupBy`) | P | P | P | P | P | P |
| `HAVING` | P | U | U | U | U | U |
| `SelectGroup` | P | P | P | P | P | P |
| `SelectGroupHaving` | P | U | U | U | U | U |
| Join | U | P | P | P | P | P |
| CTE (`AsCte`) | U | P | P | P | P | P |
| Window functions (`Over`, frames) | U | P | P | U | U | P |
| JSON columns | P | U | U | U | U | U |
| Array / nested types | U | U | U | U | U | P |
| Tuple-typed **result column** | U | U | U | U | U | **F** |
| Decimal binary/comparison operators in rewriting | P | P | P | D | D | U |
| Prepared command reuse / plan cache | U | U | U | P | P | U |
| Eager loading (eager-load binding, per-parent paging) | U | U | U | U | U | U |
| Composite junction (many-to-many `JoinInto`) | U | U | U | U | U | U |
| Value converters (`[JsonColumn]`, custom converter) | U | U | U | U | U | U |
| CSV streaming (`WriteCsv`) | U | U | U | U | U | U |
| JSON streaming (`WriteJson`) | U | U | U | U | U | U |
| Row streaming (`IAsyncEnumerable`) | U | U | U | U | U | U |
| Bulk insert (native and portable) | U | U | U | U | U | U |
| Raw SQL row mapping (`ExecuteRaw`) | U | U | U | U | U | U |
| DML builders (insert/update/delete) | U | U | U | U | U | U |

Cell totals: **76 `supported-proven`, 2 `supported-by-design`, 1 `fail-closed`, 113 `not-covered`**
(192 cells across 32 capabilities and six providers; counted mechanically across the matrix rows).

What each `P` rests on:

- SQLite: the nine-scenario native harness — typed/anonymous projection, captured parameter, ordering,
  `count`/`sum`, `GroupBy`+`HAVING`, `SelectGroup`, `SelectGroupHaving`, JSON columns, decimal operators.
  The two component slices (`CsvStreamOptions`, `JsonShapePlan`) are native-only runs with no JIT
  comparison, so CSV/JSON streaming stay `not-covered` under this ledger's evidence rule.
- PostgreSQL: the ten-query probe — projection, ordering, paging, `count`/`count_distinct`/`sum`/`avg`,
  `GroupBy`, `SelectGroup`, join, CTE, windows, decimal operators.
- SQL Server: the eleven-query probe — projection, ordering, paging,
  `count`/`sum`/`avg`/`max`, `GroupBy`, `SelectGroup`, join, CTE, windows, decimal operators.
- MySQL / MariaDB: the seven-shape probe — projection, ordering, paging, `count`/`sum`, `GroupBy`,
  `SelectGroup`, join, CTE, prepared-command reuse.
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
| PostgreSQL / SQL Server | `System.Tuple` with more than seven elements in a raw ROW | `NotSupportedException` | JIT suite (`RawMapperFactory` guard) |

Only the ClickHouse row is native-proven; the other two are JIT-only and are not counted as native
fail-closed evidence.

## What this ledger does not claim

- It does not claim whole-surface NativeAOT support. The canonical registry still lists 215 residual
  diagnostic rows (`.pdca/370/rv1/registry-s6.tsv`); each remains tracked by the campaign.
- `U` cells are unknowns of this ledger, not refusals and not support.
- The MySQL/MariaDB instrument proves the seven shapes it runs (three customers, six orders), not the
  providers' whole surface.
