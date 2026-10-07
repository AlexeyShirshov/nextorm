# Independent final CHECK verification for D193 (tuple `IN`/`Contains`) — issue #193 (task `rc1-tail-193-verify-1`)

- task: `rc1-tail-193-verify-1`
- collection: `1.0.9-rc1-tail` (single group G1)
- branch: `1.0.9-rc1`
- issue: #193 (https://github.com/AlexeyShirshov/nextorm/issues/193)
- cycle: N=1
- plan revision: r=1
- attempt: n=2/3
- contract: rv=1
- mode: autonomous
- base HEAD: `c36be770e30c90ae15bf8792c5fd93fa397365ca`

## Goal

Obtain an independent final CHECK PASS for D193 on the current tree, repair evidence/status rot, and return control to parent C.

## Acceptance criteria AC1–AC7

- **AC1** validator `brief` before edits and `report` before CHECK, both exit 0, evidence path + commands + actual selected counts recorded; inner = filtered affected subset; one comprehensive sweep at DO→CHECK boundary only.
- **AC2** independent `check` verdict PASS for the current tree, covering W1–W4; no escalate waiver.
- **AC3** D193 status file: correct the "no commit/push/merge" claims to the real commits `e19d751a10eee9407484c26f407327fd8de832a0` and bookkeeping `cd1a7c6c`; add `## CHECK r=2` with real verdict/evidence/exit codes/selected counts; keep historical r=1 FAIL.
- **AC4** Integration reconciliation: explain the 75-test gap (cross-provider/shared "other" bucket: IdentityReturning/EfCore/CoreApiContractTests/DialectCapabilityContractTests/LOB harness/QueryFilterInMemoryParity) and the 3 extra skips (env-gated LOB `LOB_PERF`/`LOB_PROBE`); record reconciled totals.
- **AC5** Collection status updated: corrective-task reference `rc1-tail-193-verify-1`; D193 `done` only with real CHECK PASS; verification state stays `unverified` pending re-run parent C.
- **AC6** New corrective status file created; original D193 status kept.
- **AC7** Commit only corrective files, message starts `#193`; never push.

## Plan (r=1)

- **P1** setup status/evidence + plan + history (this file, `/tmp/nextorm-rc1-tail-193-verify-1/`).
- **P2** validator `brief` + filtered inner tests (core/sqlite/sqlserver, `FullyQualifiedName~D193TupleIn`).
- **P3** repair D193 status + collection status + integration reconciliation.
- **P4** one solution build + one unfiltered integration sweep at the DO→CHECK boundary + validator `report`.
- **P5** independent `check` verdict covering W1–W4.
- **P6** selective commit of the 3 corrective `.md` files, message starts `#193`, no push.

## Test scope (scope.json)

```json
{
  "unit": "rc1-tail-193-verify-1",
  "scope": {
    "projects": ["tests/nextorm.core.tests", "tests/nextorm.sqlite.tests", "tests/nextorm.sqlserver.tests", "tests/nextorm.integration.tests"],
    "selectors": ["FullyQualifiedName~D193TupleIn"],
    "files": ["docs/specs/status/rc1-tail-193-tuple-in-1.md", "docs/specs/status/collection-1.0.9-rc1-tail.md", "docs/specs/status/rc1-tail-193-verify-1.md"],
    "rationale": "Docs/status-only correction; reverify existing D193 fixes with affected filtered tests and one comprehensive integration boundary sweep.",
    "rebuild": "none",
    "boundary": "Exactly one solution build and one unfiltered integration-project sweep at DO-to-CHECK, with container providers enabled."
  }
}
```

## Planned evidence executions (evidence.json, values filled after runs)

| ID | Phase | Command | Project / args | `source_revision` | `artifact_revision` | `exit_code` | `selected_count` |
| --- | --- | --- | --- | --- | --- | --- | --- |
| EX-01 | inner | `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~D193TupleIn` | core | `r1` | `r1` | <filled> | <filled> |
| EX-02 | inner | `dotnet test tests/nextorm.sqlite.tests -c Debug --filter FullyQualifiedName~D193TupleIn` | sqlite | `r1` | `r1` | <filled> | <filled> |
| EX-03 | inner | `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter FullyQualifiedName~D193TupleIn` | sqlserver | `r1` | `r1` | <filled> | <filled> |
| EX-04 | boundary | `dotnet build nextorm.slnx -c Debug` | solution | `r1` | `r1` | <filled> | n/a |
| EX-05 | boundary | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug` | integration | `r1` | `r1` | <filled> | <filled> |

## Evidence contract rv=1

| ID | AC | Owner | Evidence | Status |
| --- | --- | --- | --- | --- |
| EV-01 | AC1 | DO | `/tmp/nextorm-rc1-tail-193-verify-1/brief.log` (`brief` exit 0) | satisfied (exit 0) |
| EV-02 | AC1 | DO | `/tmp/nextorm-rc1-tail-193-verify-1/report.log` (`report` exit 0) + `evidence.json` | satisfied (exit 0) |
| EV-03 | AC2 | CHECK | independent `check` verdict PASS covering W1–W4 | satisfied (independent CHECK PASS) |
| EV-04 | AC3 | DOC | `docs/specs/status/rc1-tail-193-tuple-in-1.md` (`## CHECK r=2`, commits corrected, r=1 FAIL kept) | satisfied (final PASS verdict persisted) |
| EV-05 | AC4 | DOC | integration reconciliation (75-test gap + 3 env-gated skips) in D193 status | satisfied |
| EV-06 | AC5 | DOC | `docs/specs/status/collection-1.0.9-rc1-tail.md` updated | satisfied (D193 done on real PASS) |
| EV-07 | AC6 | DO | this file created + CRLF normalized; original D193 status retained | satisfied |
| EV-08 | AC7 | ACT | selective commit message starts `#193`; no push | satisfied after commit |

## Defect history (durable)

- **D193-W1** cache-shape safety (`QueryCommand` not mutated per call) — fixed in commit `e19d751a`.
- **D193-W2** `Nullable<ValueTuple>` routing hole — fixed in commit `e19d751a`.
- **D193-W3** throwaway spike removed — fixed in commit `e19d751a`.
- **D193-W4** SQL Server rejection ordering/message — fixed in commit `e19d751a`.
- Recurrence of any W1–W4 → **escalate BEFORE a second fix**.
- Plan revision r=1, attempt n=1/3.

## Progress log

| Time (UTC) | Phase | Rev | Attempt | Event | Evidence |
| --- | --- | --- | --- | --- | --- |
| 2026-10-06T21:09Z | PLAN | r=1 | n=1/3 | plan fixed; brief pending | docs/specs/status/rc1-tail-193-verify-1.md |
| 2026-10-06T21:09Z | DO started | r=1 | n=1/3 | scope.json written; validator brief exit 0 | /tmp/nextorm-rc1-tail-193-verify-1/scope.json, /tmp/nextorm-rc1-tail-193-verify-1/brief.log |
| 2026-10-07T02:09Z | DO | r=1 | n=1/3 | scope.json + brief exit 0 | /tmp/nextorm-rc1-tail-193-verify-1/scope.json, /tmp/nextorm-rc1-tail-193-verify-1/brief.log |
| 2026-10-07T02:11Z | DO | r=1 | n=1/3 | inner core 32/sqlite 36/sqlserver 10 exit 0; build 0/0; integration 3311/3114/0/197; report exit 0 | /tmp/nextorm-rc1-tail-193-verify-1/core.log, sqlite.log, sqlserver.log, build.log, integration.log, report.log |
| 2026-10-07T02:12Z | DO | r=1 | n=1/3 | status rot fixed; reconciliation recorded; collection updated | docs/specs/status/rc1-tail-193-tuple-in-1.md, docs/specs/status/collection-1.0.9-rc1-tail.md |
| 2026-10-07T02:26Z | CHECK | r=1 | n=1/3 | FAIL (evidence/status completeness; no product defect) → loop-back DO n=2/3 | /tmp/nextorm-rc1-tail-193-verify-1/check.md |
| 2026-10-07T02:28Z | CHECK | r=1 | n=2/3 | PASS (independent, no waiver) | /tmp/nextorm-rc1-tail-193-verify-1/check.md |
| 2026-10-07T02:29Z | ACT | r=1 | n=2/3 | finalized statuses; corrective commit | docs/specs/status/rc1-tail-193-tuple-in-1.md, docs/specs/status/collection-1.0.9-rc1-tail.md, docs/specs/status/rc1-tail-193-verify-1.md |

## DO evidence

Inner filtered (phase `inner`, `--filter FullyQualifiedName~D193TupleIn`):

| # | Command | Exit | Selected | Log |
|---|---|---|---|---|
| 1 | `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~D193TupleIn` | 0 | 32 | `/tmp/nextorm-rc1-tail-193-verify-1/core.log` |
| 2 | `dotnet test tests/nextorm.sqlite.tests -c Debug --filter FullyQualifiedName~D193TupleIn` | 0 | 36 | `/tmp/nextorm-rc1-tail-193-verify-1/sqlite.log` |
| 3 | `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter FullyQualifiedName~D193TupleIn` | 0 | 10 | `/tmp/nextorm-rc1-tail-193-verify-1/sqlserver.log` |

Boundary:

| # | Command | Exit | Result | Log |
|---|---|---|---|---|
| 4 | `dotnet build nextorm.slnx -c Debug` | 0 | 0 Warning(s) 0 Error(s) | `/tmp/nextorm-rc1-tail-193-verify-1/build.log` |
| 5 | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug` | 0 | Total 3311 / Passed 3114 / Failed 0 / Skipped 197 (all providers executed) | `/tmp/nextorm-rc1-tail-193-verify-1/integration.log` |

Validator: `brief` exit 0 (`/tmp/nextorm-rc1-tail-193-verify-1/brief.log`); `report` exit 0 (`/tmp/nextorm-rc1-tail-193-verify-1/report.log`); evidence `/tmp/nextorm-rc1-tail-193-verify-1/evidence.json`.

## Variant matrix closure (pinned D193 contract)

Disposition per pinned variant; evidence is `file:line` in the current tree.

| Variant | Disposition | Evidence |
|---|---|---|
| ValueTuple arities 1..7 semantics | test | `tests/nextorm.core.tests/D193TupleInTests.cs:42,53,75,86,119,133,147,161` |
| Reference `Tuple` arities 1..7 semantics | test | `D193TupleInTests.cs:64,104,175,189,207,225,243` |
| Render grammar arities 1..7 | test | `tests/nextorm.sqlite.tests/D193TupleInSqlGenerationTests.cs:170,185,202,217,232,247,262,277,292,307,322,337` |
| Captured/inline/enumerable RHS; `IN` vs `Contains` | test | core `:42,63`; sqlite `:22,37`; per-provider `:22/:23` + `:37/:38` |
| empty collection | test | core `:273`; sqlite `:52`; per-provider `:52/:53`; sqlserver `:40` |
| default row | test | core `:261`; sqlite `:352` |
| value/reference tuple elements | test | core `:42,63`; sqlite `:22,277` |
| nullable components | test | core `:284`; sqlite `:65,78`; per-provider `:65/:66` |
| null collection | test/guard | core `:345`; sqlite `:368`; sqlserver `:53` |
| null reference tuple entry | test | core `:450` area; sqlite `:158`; sqlserver `:66` |
| `Nullable<ValueTuple>` rejection | test | core `:424`; sqlite `:129` |
| malformed arity / nested | test | core `:384,393`; sqlserver `:79,95` |
| SQL Server rejection (all forms/clauses) | test + guard | sqlserver `:25,40,53,66,79,95,110,129,145,159`; render guard `src/nextorm.core/Visitors/InValuesTranslator.cs:135-136` (first statement of `TranslateTupleInValues`, fires for every clause, before empty-list fold); integration `tests/nextorm.integration.tests/CommonTestSuite.In.cs:214`; preflight `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:1877` (WHERE/PREWHERE) |
| SQL Server explicit `SqlFunctions.Sql.@in(Tuple…)` unkeyed clauses | guard (closed) | the render guard `InValuesTranslator.cs:135` is clause-agnostic and precedes SQL submission |
| non-SQLite arities >2 render | test (shared contract) + per-provider smoke | provider-agnostic RHS renderer `InValuesTranslator.cs:204` + default `RenderInValues` `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs:643`; SQLite arity sweep `:170..337`; per-provider arity-2 smoke `postgres/mysql/mariadb/clickhouse` `:22/:23`; MySQL/MariaDB share the dialect |
| PostgreSQL `ROW(...)` / ClickHouse `tuple(...)`/`global_in` | test | `tests/nextorm.postgres.tests/D193TupleInSqlGenerationTests.cs:22,37`; `tests/nextorm.clickhouse.tests/D193TupleInSqlGenerationTests.cs:23,38`; dialects `PostgresDialect.cs:658-664`, `ClickHouseDialect.cs:931-937` |
| converter-bound components / context reuse | test | sqlite `:112`; cache tests `D193TupleInPlanCacheTests.cs` |
| shared-command cache safety | test | `D193TupleInTests.cs:330`; `D193TupleInPlanCacheTests.cs:321,254,348,427,472`; guard `DataContext/QueryPlanner.cs:573-574` |
| `Rest`/nested arity≥8 | deferred + trigger (valid) | D193 status `#### E. Deferred` `:1300` — only if a user scenario requires the CLR `Rest`/nested shape; renderer scope frozen 1..7; fail-closed rejection tested core `:384,393`, sqlserver `:79,95` |
| alternate bulk binding | deferred + measured-limit trigger | D193 `## Variant matrix` `:89` |

## Priority matrix (nextorm class-priority)

- class **new SQL function/operator**: parity with SQL sibling (tuple forms); capability flag per dialect (`ISqlDialect.cs:254,261` `SupportsTupleFunctions`/`Tuple`; `SqlDialectBase.cs:70,73`); test per provider; nullable semantics → closed by the rows above.
- class **query-path/plan-cache change**: cached-vs-prepared ratio perf acceptance; must not mutate shared `QueryCommand`; temp-table/TVP branches → closed by `QueryPlanner.cs:573-574`, cache tests, and the original r=2 R7 perf evidence (`rc1-tail-193-tuple-in-1.md:388-450`).

## rv=1 evidence ledger (E193-00..06)

| Row | Binding | Evidence |
|---|---|---|
| E193-00 preflight/reconnaissance | `rc1-tail-193-tuple-in-1.md:1319` (`## E193-00 evidence`) | reconnaissance artifacts + facts recorded |
| E193-01 core/SQL tests exit 0 | fresh inner core 32 / sqlite 36 / sqlserver 10, exit 0 | `/tmp/nextorm-rc1-tail-193-verify-1/{core,sqlite,sqlserver}.log` |
| E193-02 per-provider `FULL(P)`/`SQL(P)` exit 0 | per-provider D193 render tests + boundary integration (all providers executed) | provider test logs + `integration.log` |
| E193-03 container integration executed, no wholesale skip | fresh boundary `integration.log` 3311/3114/0/197; providers confirmed | `/tmp/nextorm-rc1-tail-193-verify-1/integration.log` |
| E193-04 docs EN+RU | commit `e19d751a` docs files; original `## Docs` | `e19d751a`; `rc1-tail-193-tuple-in-1.md` `## Docs` |
| E193-05 regression/coverage(85/75)/perf | r=2 full-solution 87.3% line / 79.1% branch; R7 perf | `rc1-tail-193-tuple-in-1.md:532-533`; `:388-450` |
| E193-06 build 0W/0E, CRLF/scope, no commit | fresh boundary build 0/0; CRLF confirmed; the original "no commit" was a pre-commit snapshot, later committed `e19d751a`+`cd1a7c6c` | `/tmp/nextorm-rc1-tail-193-verify-1/build.log` |

## Corrective CHECK history

- Round 1 (2026-10-07) independent `check` returned **FAIL** — findings: AC2 unmet (open variant rows), AC3 unmet (PENDING verdict), AC4 unverified (reconciliation breakdown), AC5 unmet (premature done), AC7 unverified (commit not yet done); **no in-scope product defect**; loop-back DO `r=1 n=2/3`.
- DO fixes applied (loop-back):
  - D193 marked `in-progress`: collection task row + G1 group row changed, and D193 moved out of the collection `Done` list into an "In progress" bullet.
  - `## CHECK r=2` heading retitled "under corrective revalidation rc1-tail-193-verify-1"; the PENDING placeholder blockquote and bullet replaced with a truthful "not final" verdict; no PASS claimed.
  - Stale W4 call-site pointers corrected to the current tree (`QueryPreparer.cs:2126` `PrepareWhere`, `:2159` `PreparePreWhere`; `:175–176` `RefreshInValuesShape` unchanged) at the W4 audit row and the two R5 rows.
  - Integration reconciliation replaced with the parsed TRX breakdown (provider buckets, folded shared classes, other bucket, arithmetic 3236+75=3311 / 3042+72=3114 / 194+3=197, strict-prefix 3187/2993/194 + 124/121/3, the 3 env-gated skips, baseline TRX pointer).
  - Variant matrix closure, class-priority matrix and `rv=1` evidence ledger added to this file; EV-03..08 statuses updated.

## ACT

Finalized. Validator `report` gate exit 0 (`/tmp/nextorm-rc1-tail-193-verify-1/report.log`); independent
CHECK **PASS** (`/tmp/nextorm-rc1-tail-193-verify-1/check.md`, no escalate waiver, no in-scope product
defect); finalization committed in a single corrective commit `9858fbe3` (message
`#193 Verify tuple IN CHECK and repair evidence/status`); no push.

## Final CHECK verdict

**PASS** — independent `check`, no escalate waiver; report `/tmp/nextorm-rc1-tail-193-verify-1/check.md`.
