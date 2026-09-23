# Examples verification report

> **Last verified: 2026-09-23** (examples switched to the published `nextorm` **1.0.4-alpha** NuGet
> packages; ClickHouse queries re-validated against `clickhouse/clickhouse-server:25.8-alpine`).
> Re-run the examples whenever the engine gains features and update the tables below. This file is the
> source of truth for "what works / what does not and why"; the topics are owned by the roadmap backlog
> under [`docs/specs/roadmap/`](../docs/specs/roadmap) and [`docs/specs/`](../docs/specs).
> Run mechanics live in [`.opencode/skills/running-examples/SKILL.md`](../.opencode/skills/running-examples/SKILL.md).

## Environment

Testcontainers + Podman (Windows Podman machine, reached from WSL through
`unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`). Datasets download once into
`~/.cache/nextorm/{aviasales,adventureworks,clickhouse}/` and are reused.

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project examples/<name> -c Debug
```

Each `Program` runs every query to completion: it prints `[ OK ]`/`[FAIL]` per query and an
`n/m succeeded` summary. Only documented gaps (`NotSupportedException`/`QueryPreparationException`)
are recorded as `FAIL`; any other exception aborts the run.

## Results

| Example | Result | Failing queries |
|---|---|---|
| `nextorm.examples.postgres.aviasales` | **11/11** | — |
| `nextorm.examples.mssql.adventureworks` | **11/11** | — |
| `nextorm.examples.clickhouse.analytics` | **10/11** | `Incremental` |

## Failures and their tracked cause

| Query | Why it fails | Tracked in |
|---|---|---|
| CH `Incremental` | `uniqMerge`, the `-Merge` combinator over an `AggregateFunction(uniq, …)` state column, has no LINQ surface in the released packages (the state cannot be projected as a scalar) | [`todo_clickhouse_aggregate_function_state.md`](../docs/specs/roadmap/todo_clickhouse_aggregate_function_state.md) |

MSSQL `QuarterlyPivot` is no longer a failure: the native `PIVOT` now accepts a derived query, so the
reference `WITH OrderMargins AS (<5-table join>) ... PIVOT (...)` is expressed directly.

## Actuality changes made on this verification

* **Examples moved to the published packages.** The three projects now reference the `nextorm.*` NuGet
  packages (`NextOrmVersion = 1.0.4-alpha` in [`Directory.Packages.props`](../Directory.Packages.props))
  instead of `src/`, so they build against what ships on nuget.org. Re-run against Testcontainers after
  the switch: postgres `11/11`, mssql `11/11`, clickhouse `10/11` (`Incremental` only).
* **Databases are cached in named Docker volumes (the examples are read-only).** Each `DemoDatabase`
  mounts a named volume at the provider's data directory and only copies the dataset + loads when a
  row-count probe finds no data, so warm runs skip the copy and the multi-minute load and merely start
  a container against the existing volume. Measured on Podman: mssql cold `31 s` → warm `12 s`,
  postgres cold `2:34` → warm `1:31` (query time dominates), clickhouse cold `2:54` → warm `28 s`.
  `NEXTORM_EXAMPLES_RELOAD=true` deletes the volume first for a cold rebuild. Volumes:
  `nextorm-examples-{aviasales,adventureworks,clickhouse}-data`.
* **CH `ArrayAnalytics` and `Retention` are now `WORKING` (ClickHouse 8/11 → 10/11).** The stale
  "higher-order lambdas / `groupArray` have no LINQ surface" notes were wrong for the 1.0.4 surface:
  `ArrayAnalytics` uses `split_by_char` → `array_map` → `array_filter` grouped by the resulting array, and
  `Retention` uses `ctx.With(...)` CTEs + `group_array(Tuple.Create(...))`. `length(x)` inside the
  higher-order lambda is written as `x.ToLower().Length` because member access on the lambda parameter
  (`x.Length`) is rejected by the translator. A full `hits_v1` run against the 1.0.4 packages completed
  with **10/11** — `[ OK ]` for `ArrayAnalytics` and `Retention` (plus all six course exercises), the only
  `FAIL` being `Incremental`.
* **CH `Incremental` remains the only `FAIL`**: `uniqMerge` (the `-Merge`/`-State` combinator) has no LINQ
  surface in the released packages.
* **CH `Sessions`** now uses the native `SqlFunctions.ClickHouse.lag_in_frame(...)` (was emulated with
  `Sql.lag`). `runningAccumulate(...)` is still emulated by a framed `sum_over(...)`.
* **CH `SessionDepth`** now uses `SqlFunctions.ClickHouse.multi_if(when(...), …, otherwise(...))` in
  `GROUP BY`/`ORDER BY`/`SELECT` (was a nested ternary).
* **MSSQL `QuarterlyPivot`** was the documented derived-source blocker; it now fits the native `Pivot`
  over a derived query (see the next bullet). Its message/comment no longer reports a gap.
* Docs synced: `README.md` and the per-provider READMEs.
* **Re-verified after correlated subqueries were completed** (depth ≥ 2, aggregate terminals, `HAVING`,
  function over an outer column) **and correlated `CROSS`/`OUTER APPLY`/`LATERAL` landed**: results are
  unchanged (11/11, 10/11, 8/11). No example query overlaps those features — the remaining gaps are
  ClickHouse arrays / `AggregateFunction` states / `groupArray` and MSSQL `PIVOT` over a derived source,
  none of which correlation or `APPLY` addresses. Query comments and failure messages stay accurate.
* **Re-verified after a derived query could become the primary `FROM` source of a join** (`ctx.From(derivedQuery).Join(...)`, `ResolveJoinBase`): results were unchanged (11/11, 10/11, 8/11). No example query uses derived-primary joins; the `QuarterlyPivot` blocker stayed open then and is now resolved (next bullet). The stale `todo_mssql_derived_from_join.md` link was repointed to `todo_mssql_pivot_derived.md` after it was retired.
* **Re-verified after `PIVOT`/`UNPIVOT` accepted a derived source** (`ResolvePivotInner`, `ctx.From(derivedQuery).Pivot(...)`): MSSQL moved **10/11 → 11/11** — `QuarterlyPivot` now pivots a 5-table join through a derived query (numeric `FOR QuarterNum IN (1,2,3,4)`; the string form needs an explicit T-SQL `CAST`). The other two examples are unchanged. The work plan `docs/specs/roadmap/todo_mssql_pivot_derived.md` was folded into the docs and retired.

## Known environment pitfalls (do not mistake for product failures)

* The Podman machine can stop after an OOM on the WSL host (`no such container`, connection refused).
  Start it again (`podman.exe machine start`) and prune leftovers.
* A WSL restart clears `/tmp` and drops the `/mnt/wsl/podman-sockets/...` mount, so the next example run
  fails with `DockerUnavailableException`. Recreate it with
  `"/mnt/c/Program Files/RedHat/Podman/podman.exe" machine start` (idempotent) and wait for
  `podman-user.sock` to reappear before retrying.
* The first (cold) `hits_v1` load copies the ~841 MB archive and builds the columnar parts; free memory
  first (`dotnet build-server shutdown`, `POST /containers/prune`) or the process is OOM-killed. Warm
  runs reuse the named volume and skip it.
* This repository is sometimes edited by another agent in parallel; re-check that the example
  `md5sum`s you measured still match before trusting a run, and re-run if the tree moved.
