# Examples verification report

> **Last verified: 2026-09-21** (working tree on top of commit `8670bba`).
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
| `nextorm.examples.clickhouse.analytics` | **8/11** | `ArrayAnalytics`, `Incremental`, `Retention` |

## Failures and their tracked cause

| Query | Why it fails | Tracked in |
|---|---|---|
| CH `ArrayAnalytics` | higher-order lambdas `arrayMap`/`arrayFilter` (and grouping by an array) are not translated | [`todo_clickhouse_arrays.md`](../docs/specs/roadmap/todo_clickhouse_arrays.md) (lambda/higher-order аргументы) |
| CH `Incremental` | no `AggregateFunction(...)` state type, so the `-Merge` combinator `uniqMerge` cannot be expressed | [`todo_clickhouse_aggregate_function_state.md`](../docs/specs/roadmap/todo_clickhouse_aggregate_function_state.md) |
| CH `Retention` | no row reader for `Array(T)`/`Tuple`, so `groupArray((...))` cannot be materialised (plus native `UInt64`) | [`todo_clickhouse_arrays.md`](../docs/specs/roadmap/todo_clickhouse_arrays.md), [`todo_clickhouse_uint64_row_reader.md`](../docs/specs/roadmap/todo_clickhouse_uint64_row_reader.md) |

MSSQL `QuarterlyPivot` is no longer a failure: the native `PIVOT` now accepts a derived query, so the
reference `WITH OrderMargins AS (<5-table join>) ... PIVOT (...)` is expressed directly.

## Actuality changes made on this verification

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
* Loading `hits_v1` copies the ~841 MB archive through memory; free memory first
  (`dotnet build-server shutdown`, `POST /containers/prune`) or the process is OOM-killed.
* This repository is sometimes edited by another agent in parallel; re-check that the example
  `md5sum`s you measured still match before trusting a run, and re-run if the tree moved.
