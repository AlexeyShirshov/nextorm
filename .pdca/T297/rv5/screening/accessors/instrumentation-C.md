# P03c instrumentation — candidate C (flat COW root + B probes)

Candidate C is a throwaway screening tree, not a product change.

- `cp -a --reflink=auto /tmp/opencode/t297-p02/B /tmp/opencode/t297-p02/C` (A and B untouched).
- The three P03a files were replaced with the current product tree at HEAD `c4b34178`
  (working tree clean for these paths):
  `src/nextorm.core/Query/QueryCommand.Description.cs`,
  `src/nextorm.core/Query/QueryCommand.cs`,
  `src/nextorm.core/Query/QueryCommand.EagerLoading.cs`.
- The exact probe blocks B carries were re-applied to `QueryCommand.Description.cs` and
  `QueryCommand.cs`; `QueryCommand.EagerLoading.cs` carries no probe in B (verified), so it stays
  the HEAD file byte-for-byte.
- `Probe/T297PhaseProbe.cs` (incl. `CowDescription = 10`, `CowClone = 11`) and the remaining
  instrumented files were copied from B unchanged. `cmp` is clean for
  `DataContext/QueryPlanner.cs`, `DataContext/Cache/DbPreparedQueryCommand.cs`,
  `DataContext/QueryExecutor.cs`, `Query/QueryCommand.QueryPreparer.cs`,
  `Query/QueryCommand.TResult.cs`, `Probe/T297PhaseProbe.cs`.
- Probe host: `probe-b` copied to `probe-c`, `AssemblyName=t297-probe-c`, `ProjectReference`
  → `../C/benchmarks/nextorm.benchmark/nextorm.benchmark.csproj`.

P03a removed 378 lines and added 206 across the three files, so the COW boundary lines moved.
The semantic boundary points are unchanged.

## New line numbers in C (the only moved boundaries)

| phase | C path:line + enclosing method | B path:line (reference) |
|---|---|---|
| cow_description | `Query/QueryCommand.Description.cs:143-155` `QueryDescription.MarkShared` | `:260-274` |
| cow_description | `Query/QueryCommand.Description.cs:161-177` `QueryDescription.CloneShellForWrite` | `:281-296` |
| cow_description | `Query/QueryCommand.Description.cs:191-205` `QueryCommand.WriteDescription` (span `:195-200`, clone charged separately) | `:312-321` |
| cow_clone | `Query/QueryCommand.cs:193-210` live-clone shared-description branch of `QueryCommand(IDataContext?, QueryCommand, bool)` | `:195-208` |

`QueryCommand.EagerLoading.cs` has no probe in either B or C.

## Unchanged instrumented boundaries (same lines as B)

- `DataContext/QueryPlanner.cs`: `planner_gate:594-602`, `planner_recipe:614-662`,
  `prepare_pipeline:666-668`, `plan:707-720`, `bind:883-890`, `enum_create:900-907`.
- `Query/QueryCommand.QueryPreparer.cs`: `prepare_call:32-33`, `prep_preamble:33-88`,
  `nav_expand:93-99`, `get_from:101-103`, `prepare_from:104-107`, `prepare_join:108-110`,
  `prepare_columns:111-116`, `prepare_where:117-119`, `prepare_prewhere:120-122`,
  `prepare_arrayjoin:123-125`, `prepare_grouping:127-134`, `prepare_sorting:135-137`,
  `prepare_union:139-143`, `prepare_ctes:144-146`, `prepare_hints:147-149`,
  `unkeyed_scans:155-163`, `from_hash:182-184`.
- `DataContext/Cache/DbPreparedQueryCommand.cs`: `get_db_command:199-294`.
- `DataContext/QueryExecutor.cs`: `execute:181-189` and `:211-221`;
  `read_rows:757-765`, `:809-817`; `map_rows:771-779`, `:823-831`.
- `Query/QueryCommand.TResult.cs`: `construct`/`prepare`:303-313, `:332-342`.

## Build / smoke evidence

- `dotnet build C/benchmarks/nextorm.benchmark/nextorm.benchmark.csproj -c Release`:
  exit 0, 0 warnings, 0 errors (`/tmp/opencode/t297-p02/logs-build-C.log`).
- `dotnet build probe-c/probe-c.csproj -c Release`: exit 0, 0 warnings, 0 errors
  (`/tmp/opencode/t297-p02/logs-build-probe-c.log`).
- Smoke `--arm all --warmup 3 --iters 3` under `taskset -c 1`,
  `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`: exit 0 (`smoke-c.jsonl`, `smoke-c.err`).
  Verification passed; per-query calls `prepare/plan/get_db_command/execute = 1`,
  `bind = 1` for inlist and 0 elsewhere, `enum_create = 0` — identical to the B smoke.
  `cow_description` fires in all arms (calls/op 4, 8, 1, 2) and `cow_clone` in `recursive`
  (1/op), so the re-applied COW counters are live in C.
