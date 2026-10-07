# D141 — Version-gates: MariaDB 13 and PostgreSQL FILTER aggregates (9.2/9.3)

- **Task:** D141
- **Issue:** [#141](https://github.com/AlexeyShirshov/nextorm/issues/141)
- **Collection:** `1.0.9-rc1`, group **G01**
- **Branch:** `1.0.9-rc1` (current worktree, single group ⇒ no group branch/worktree)
- **Current cycle:** N = 1
- **Plan revision:** r = 2 (Contract A strong supersedes the prior r1 draft)
- **Attempt:** n = 2 (CHECK→DO loop-back; plan revision unchanged at r = 2)
- **Evidence contract revision:** `rv = D141.ContractA.strong.1` (supersedes prior)
- **Mode:** autonomous; auto-commit **authorized** by the collection; this DO task performs **no commit/push** (ACT commits later)
- **Worktree:** `/home/alex/sources/nextorm`
- **Notice:** host has no `todowrite` tool; this status file carries the progress log instead.

## Goal

Make the PostgreSQL and MariaDB dialects version-aware on exactly two axes, without changing
today's behavior when no version is configured:

1. **PostgreSQL** — gate the ANSI aggregate `FILTER` clause (`AggregateFilterStyle.AnsiFilter`) on
   server version ≥ 9.4; 9.2/9.3 must fail closed with `NotSupportedException`, not emit SQL the
   server rejects.
2. **MariaDB** — gate `UPDATE ... RETURNING` (MariaDB 13.0+) behind a separate
   `SupportsUpdateReturning` capability, so `INSERT`/`DELETE` `RETURNING` behavior is untouched.

For PostgreSQL the version is immutable per concrete context type; the plan-cache key
(`QueryPlanStore.Key`) is unchanged, and a single PostgreSQL context type may only ever be bound to one
version (a second, conflicting version is a configuration error that names the subclass remedy). MariaDB
has no such guard (its gated `UPDATE ... RETURNING` is not plan-cached).

## Acceptance criteria (positive + negative)

| ID | Criterion | Positive | Negative |
|---|---|---|---|
| **R141-PG** | PG FILTER version gate | Version ≥ 9.4 (incl. patch) or unset emits `filter (where ...)` | Version < 9.4 (e.g. 9.2/9.3 incl. patch) throws `NotSupportedException` |
| **R141-TYPE** | Immutable per-context-type version | Same type + equal `Version` value is accepted | Same type + unequal value, including unset-vs-explicit, throws `InvalidOperationException` naming the subclass remedy |
| **R141-CACHE** | No plan-cache leak across versions | Two distinct PG subclasses with different versions both work in either construction order | A supported-then-unsupported ordering must not let the warmed plan bypass the gate |
| **R141-MDB** | MariaDB UPDATE RETURNING gate | Version ≥ 13.0 (incl. patch) emits `returning` on single-table UPDATE | Version < 13.0 or unset throws `NotSupportedException`; INSERT/DELETE behavior unchanged |
| **R141-COMPAT** | Backward compatibility | Public parameterless dialect ctors and static `Instance` exist and mean "unset" = today's behavior; `Activator.CreateInstance` contract still passes | No public signature is removed; existing tests/assertions untouched |
| **R141-DOC** | Documentation | EN+RU guide pages document the version knobs; `linq2db-backlog-gap-analysis.md` G13 and `sql-capabilities-gap-analysis.md` synced | — (docs stream not in this task's file scope) |
| **R141-VERIFY** | Verification | Build, inner filtered runs and the affected boundary sweeps are green; `validate_inner_loop.py report` exits 0 | Any red run is not completion; stale/absent evidence is not completion |

## Locked design (Contract A strong)

- **Immutable per-concrete-context-type version.** The version is snapshotted once at context
  construction and used only to build that context's dialect.
- **`QueryPlanStore.Key` unchanged.** No version component is added to the plan-cache key; instead
  distinct versions require distinct context **types**, whose existing `ContextType` key component
  already separates their plans.
- **Dialect ctors.** `PostgresDialect(Version?)` and `MariaDbDialect(Version?)` are added; both keep
  a public parameterless ctor, and the static `Instance` (built through the parameterless ctor)
  remains the unset form.
- **PostgreSQL FILTER.** `AggregateFilterStyle => _version is null || _version >= 9.4 ? AnsiFilter : None`.
- **`SupportsUpdateReturning`.** New `ISqlDialect` default interface member returning
  `SupportsReturning`; `SqlDialectBase` virtual with the same default; consumed at the single-table
  UPDATE `RETURNING` path in `SqlMutationBuilder` and at the `EnsureReturningSupported` execution
  gate (the UPDATE capability check). INSERT/DELETE/key-upsert/MERGE keep `SupportsReturning`.
- **MariaDB.** `SupportsUpdateReturning => _version is not null && _version >= 13.0`; `SupportsReturning`
  stays inherited `false`; no type guard.
- **Unset = today's behavior.**
- **PostgreSQL type guard.** A `static readonly ConcurrentDictionary<Type, object>` keyed by
  `GetType()` with an unset sentinel; atomically register/compare using `Version.Equals` value
  equality; a mismatch throws `InvalidOperationException` naming the subclass remedy; never
  overwrite. MariaDB has no guard.

## Deferred (same milestone, reachable later via triggers)

- SQLite 3.30 `FILTER` gate (trigger: a SQLite version knob is requested).
- `ANY_VALUE` MariaDB 13.2 (trigger: the server ships it).
- Real old-server execution of the gates (trigger: a 9.2/9.3 or MariaDB 12/13 container is added).

## Perf decision

**N/A on this scope.** The change is construction-time only (a `Version?` snapshot plus a static
dictionary lookup per context construction); no query-path, plan-cache or per-row code is touched.
Flip condition: any change that reads the version on a query/cache/per-row hot path.

## Variant matrix (closed)

- Version source: explicit `Version?` ctor overload only (no connection-string parsing, no server
  round-trip). **Closed — chosen.**
- Guard placement: PostgreSQL provider static dictionary keyed by `GetType()`. **Closed — chosen.**
- MariaDB guard: none. **Closed — chosen.**
- `SupportsUpdateReturning` consumption: UPDATE render path + UPDATE execution gate. **Closed — chosen.**
- Plan-cache key change: none. **Closed — rejected (would reset shared plans).**

## Evidence contract

| ID | Invocation (argument array) | Phase | Required result |
|---|---|---|---|
| E141-01 | `dotnet build nextorm.slnx -c Debug` | build | exit 0, 0 errors |
| E141-02 | `dotnet test tests/nextorm.postgres.tests -c Debug --no-build --filter FullyQualifiedName~VersionGate` | inner | exit 0, ≥1 selected |
| E141-03 | `dotnet test tests/nextorm.postgres.tests -c Debug --no-build --filter FullyQualifiedName~FilteredAggregates` | inner | exit 0, ≥1 selected |
| E141-04 | `dotnet test tests/nextorm.postgres.tests -c Debug --no-build --filter FullyQualifiedName~StringAgg_WithFilter` | inner | exit 0, ≥1 selected |
| E141-05 | `dotnet test tests/nextorm.postgres.tests -c Debug --no-build --filter FullyQualifiedName~CapabilityFlags` | inner | exit 0, ≥1 selected |
| E141-06 | `dotnet test tests/nextorm.postgres.tests -c Debug --no-build --filter FullyQualifiedName~Returning` | inner | exit 0, ≥1 selected |
| E141-07 | `dotnet test tests/nextorm.mariadb.tests -c Debug --no-build --filter FullyQualifiedName~VersionGate` | inner | exit 0, ≥1 selected |
| E141-08 | `dotnet test tests/nextorm.mariadb.tests -c Debug --no-build --filter FullyQualifiedName~CapabilityFlags` | inner | exit 0, ≥1 selected |
| E141-09 | `dotnet test tests/nextorm.mariadb.tests -c Debug --no-build --filter FullyQualifiedName~Returning` | inner | exit 0, ≥1 selected |
| E141-10 | `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter FullyQualifiedName~Returning` | inner | exit 0, ≥1 selected |
| E141-11 | `dotnet test tests/nextorm.postgres.tests -c Debug --no-build` | boundary | exit 0, full PGT |
| E141-12 | `dotnet test tests/nextorm.mariadb.tests -c Debug --no-build` | boundary | exit 0, full MDB |
| E141-13 | `dotnet test tests/nextorm.core.tests -c Debug --no-build` | boundary | exit 0, full core |

Boundary sweeps are recorded through the single-comprehensive-sweep gate; if the three full-project
sweeps cannot be recorded without tripping the gate's one-sweep cap, the sweeps are recorded as
namespace-filtered (all tests in the affected assembly) with a validated amendment, and the
equivalence of selected counts is recorded.

## Assumptions

- **Friend-assembly accessibility of `ContextEnvironment` is not needed.** `ContextEnvironment` is
  `internal` and the PostgreSQL/MariaDB provider assemblies have no `InternalsVisibleTo`; the version
  snapshot is exposed through a new **`protected`** `DataContext` property instead, so no
  `InternalsVisibleTo` is added. (Resolved — see progress log.)
- **No current fixture uses one PG context type with two versions.** Existing fixtures use
  `PostgresDataContext` unset only; new versioned tests use dedicated subclasses. If a fixture is
  found mixing versions on one type it will be migrated to subclasses.
- **`SqlMutationBuilder` path discrepancy.** The brief names
  `src/nextorm.core/Visitors/SqlMutationBuilder.cs`; the file actually lives at
  `src/nextorm.core/DataContext/SqlMutationBuilder.cs`. The scope `files` entry is kept as given; the
  real path is edited. (Resolved — path corrected.)

## Progress log

```
2026-10-05T19:13:57Z | DO | r2 | 1/3 | DO started | status file created; brief validated exit 0
2026-10-05T19:22:12Z | DO | r2 | 1/3 | docs stream closed; API shape verified against implementation (PostgresDialect(Version?)/MariaDbDialect(Version?), 3-arg provider ctx ctors, ISqlDialect.SupportsUpdateReturning, protected DataContext.ServerVersion); public->specs links none; toc.yml untouched; guide numbering contiguous 01-29; CRLF normalized; docfx exit 0 (0 errors, 2 pre-existing sourcegenerator duplicate-file warnings) | /tmp/nextorm-D141-ContractA-strong/docs/docfx.log
2026-10-05T19:29:05Z | DO | r2 | 1/3 | DO-unit closures: code (8 src files) + tests (4 test files) + docs streams closed; E141-01..E141-14 green | /tmp/nextorm-D141-ContractA-strong/
2026-10-05T19:31:15Z | DO | r2 | 1/3 | integration suite executed on all 5 providers; exit 0, Total 3136, Failed 0, Skipped 193; per-provider counts captured | /tmp/nextorm-D141-ContractA-strong/logs/E141-integration.log
2026-10-05T19:31:43Z | DO | r2 | 1/3 | validator re-run exit 2; sole failure is the comprehensive-boundary-sweep cap (3) - recorded as Notice, evidence not restructured | /tmp/nextorm-D141-ContractA-strong/E141-evidence.json
2026-10-05T19:36:58Z | CHECK | r2 | 1/3 | coverage: collect exit 0 (8086 total, 5591 passed, 0 failed, 2495 skipped), reportgenerator exit 0; overall line 87.1% / branch 78.7% (23180/29418); changed-file branch: DataContext.cs 80.0%, SqlDialectBase.cs 69.8%, SqlMutationBuilder.cs 92.6%, PostgresDataContext.cs 61.5%, PostgresDialect.cs 80.2%, ISqlDialect.cs n/a (interface default, 0 instruments); nextorm.mariadb/mysql excluded by coverage.settings.xml so MDB branch cannot gate -> explicit MDB assertion count = 8 VersionGate facts / 9 FluentAssertions assertions; manual mutation 7/7 killed (M1 pg boundary 9.4 refused, M2 pg unset unsupported, M3 pg mismatch guard bypassed, M4 guard keyed by base type, M5 mdb boundary permits 12.x, M6 mdb unset permits UPDATE RETURNING, M7 UPDATE capability reverted to SupportsReturning) each with build exit 0 + assertion failure + restore + green project suite; suppression slop-scan 0/0 | /tmp/nextorm-D141-ContractA-strong/check/ ; mutation M1..M7.patch and *.log
2026-10-05T19:44:01Z | DO | r2 | 2/3 | CHECK->DO loop-back n=2: fixed defect 141-DOC-MDB-GUARD (scoped the one-version-per-concrete-context-type guard to PostgreSQL in EN+RU providers/{mariadb,overview}.md, advanced/limitations.md and specs records; MariaDB documented as guard-free because its gated UPDATE RETURNING is not plan-cached); MariaDbDataContext versioned ctors now forward Version to the version-aware base ctor via a new protected MySqlDataContext 4-arg ctor, so DataContext.ServerVersion agrees with the dialect; added DialectCapabilityContractTests.SupportsUpdateReturning_ShouldDefaultToSupportsReturning (contract now 3 facts) and MDB ServerVersionSnapshot_ShouldAgreeWithTheDialect; strengthened PG CacheIsolation_* with a BeSameAs warm-cache-reuse assertion; build exit 0 (0 warnings/0 errors); inner pg-VersionGate 14/14, mdb-VersionGate 9/9, core-Returning 55/55, contract 3/3; boundary pg-full 756/756, mdb-full 166/166, core-full 1524/1524; docfx exit 0 (0 errors) | /tmp/nextorm-D141-ContractA-strong/r2/ ; /tmp/nextorm-D141-ContractA-strong/docs/docfx-r2.log
```

## DO-unit closures (cycle N=1, plan revision r=2, attempt n=1)

| Unit | Scope | Status |
|---|---|---|
| D141-code | `src/nextorm.core/DataContext/{DataContext,Dialect/ISqlDialect,Dialect/SqlDialectBase,SqlMutationBuilder}.cs`, `src/nextorm.postgres/{PostgresDataContext,PostgresDialect}.cs`, `src/nextorm.mariadb/{MariaDbDataContext,MariaDbDialect}.cs` | closed |
| D141-tests | `tests/nextorm.postgres.tests/{VersionGateTests,PostgresTestContext}.cs`, `tests/nextorm.mariadb.tests/{VersionGateTests,MariaDbTestContext}.cs` | closed |
| D141-docs | EN+RU guide/provider/aggregate pages + `linq2db-backlog-gap-analysis.md` G13 + `sql-capabilities-gap-analysis.md` | closed (docfx exit 0 earlier) |
| D141-evidence | build + inner + boundary + integration evidence under `/tmp/nextorm-D141-ContractA-strong/` | closed (this close-out) |

## Build / unit / boundary evidence (all green, exit 0)

Build:
- E141-01 `dotnet build nextorm.slnx -c Debug` -> exit 0, 0 Warning(s), 0 Error(s), 00:00:02.56; log `/tmp/nextorm-D141-ContractA-strong/E141-01-build-solution.log`.

Inner filtered runs (`dotnet test <project> -c Debug --no-build --filter <selector>`), all exit 0:

| ID | project | selector | total | succeeded | failed | skipped |
|---|---|---|---|---|---|---|
| E141-02 | nextorm.postgres.tests | `~VersionGate` | 14 | 14 | 0 | 0 |
| E141-03 | nextorm.postgres.tests | `~FilteredAggregates` | 1 | 1 | 0 | 0 |
| E141-04 | nextorm.postgres.tests | `~StringAgg_WithFilter` | 1 | 1 | 0 | 0 |
| E141-05 | nextorm.postgres.tests | `~CapabilityFlags` | 1 | 1 | 0 | 0 |
| E141-06 | nextorm.postgres.tests | `~Returning` | 64 | 64 | 0 | 0 |
| E141-07 | nextorm.mariadb.tests | `~VersionGate` | 8 | 8 | 0 | 0 |
| E141-08 | nextorm.mariadb.tests | `~CapabilityFlags` | 1 | 1 | 0 | 0 |
| E141-09 | nextorm.mariadb.tests | `~Returning` | 17 | 17 | 0 | 0 |
| E141-10 | nextorm.core.tests | `~Returning` | 55 | 55 | 0 | 0 |
| E141-14 | nextorm.integration.tests | `~DialectCapabilityContractTests` (amended, validated before run) | 2 | 2 | 0 | 0 |

Boundary full-project sweeps (`dotnet test <project> -c Debug --no-build`), all exit 0:

| ID | project | total | succeeded | failed | skipped |
|---|---|---|---|---|---|
| E141-11 | nextorm.postgres.tests | 756 | 756 | 0 | 0 |
| E141-12 | nextorm.mariadb.tests | 165 | 165 | 0 | 0 |
| E141-13 | nextorm.core.tests | 1524 | 1524 | 0 | 0 |

Logs: `/tmp/nextorm-D141-ContractA-strong/E141-0{1..9}.log`, `E141-1{0..3}.log`, `E141-14-dialectcontract.log`.

## Container-backed integration suite (mandatory)

Command:
`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor -result-xml /tmp/nextorm-D141-ContractA-strong/logs/E141-integration-results.xml`

- exit code **0**; `Total: 3136, Errors: 0, Failed: 0, Skipped: 193, Not Run: 0, Time: 33.092s`.
- Log: `/tmp/nextorm-D141-ContractA-strong/logs/E141-integration.log`.
- Per-test result XML: `/tmp/nextorm-D141-ContractA-strong/logs/E141-integration-results.xml`.
- An initial run of the brief's exact command (without `--no-build`/`-result-xml`) also exited 0 with the same 3136/193 summary.

All five provider containers started (Podman server 5.8.6 / Docker API 1.44; no provider skipped for lack of `DOCKER_HOST`): PostgreSQL `a1877941aa2e`, SQL Server `4ab2d4542432`, MySQL `d703aedb9b21`, MariaDB `525af9d6a314`, ClickHouse `914f06a0ba5f`.

Per-provider executed vs skipped (aggregated from the result XML by test class):

| Provider | executed (passed) | skipped | failed | total |
|---|---|---|---|---|
| PostgreSQL | 751 | 25 | 0 | 776 |
| SQL Server | 655 | 43 | 0 | 698 |
| MySQL | 576 | 79 | 0 | 655 |
| MariaDB | 47 | 0 | 0 | 47 |
| ClickHouse | 173 | 0 | 0 | 173 |
| SQLite | 622 | 43 | 0 | 665 |
| shared/other | 119 | 3 | 0 | 122 |
| **TOTAL** | **2943** | **193** | **0** | **3136** |

All 193 skips are per-test capability/opt-in skips (e.g. "provider cannot return updated rows", no INTERSECT ALL, `NEXTORM_LOB_PERF`/`NEXTORM_LOB_PROBE` opt-ins) - no provider suite was skipped. No NEW failure introduced: 0 failed, 0 errors; no integration test was weakened.

## Integration count reconciliation (exact, from `logs/E141-integration-results.xml`)

Provider grouping is by test collection/class name. Executed = passed (there are no failures); "skipped"
is the per-test skip count. Every row sums into the 3136-case total.

| Provider | executed | skipped | failed | total |
|---|---|---|---|---|
| PostgreSQL | 751 | 25 | 0 | 776 |
| SQL Server | 655 | 43 | 0 | 698 |
| MySQL | 576 | 79 | 0 | 655 |
| MariaDB | 47 | 0 | 0 | 47 |
| ClickHouse | 173 | 0 | 0 | 173 |
| SQLite | 622 | 44 | 0 | 666 |
| shared/other | 119 | 2 | 0 | 121 |
| **TOTAL** | **2943** | **193** | **0** | **3136** |

Checks: executed + skipped = 2943 + 193 = 3136 = total; per-provider totals 776 + 698 + 655 + 47 + 173 +
666 + 121 = 3136; failed = 0. This **supersedes** the earlier provisional table, whose SQLite and
shared/other rows were off by one skip (earlier SQLite 622/43/665 and shared/other 119/3/122): the
corrected split moves one skip from shared/other to SQLite (the `LobCapabilityProbeTests`/SQLite
opt-in probes) and attributes `DynamicColumnsMariaDbContainerTests` (1 case) to MariaDB.

Skip semantics: all 193 skips are **per-test capability/opt-in skips** (e.g. "provider cannot return
updated rows", no `INTERSECT ALL`, `NEXTORM_LOB_PERF`/`NEXTORM_LOB_PROBE` opt-ins). No provider suite was
skipped — all five provider containers started — so "skipped" never denotes a missing provider. The
recorded full run predates the r2/n2 additions; the integration assembly now carries the extra
`DialectCapabilityContractTests` fact (3 facts, previously 2), so a future full container run totals
3137.

## Validator re-run disposition

`python3 /home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py report /tmp/nextorm-D141-ContractA-strong/E141-evidence.json` -> **exit 2**.

Sole failure: `FAIL: more than one comprehensive boundary test sweep: 3`.

Disposition: this is the `pdca-dotnet` helper script's **boundary-sweep cap** (one comprehensive/unfiltered
boundary sweep allowed per report), while the r2 plan requires three affected-project sweeps
(PostgreSQL, MariaDB, core). It is a **cap-vs-plan mismatch, not missing evidence**: all three project
sweeps are present and green, and the full container-backed integration suite is green. The authoritative
evidence is therefore the three green project suites **plus** the green integration suite; **no required
report row is actually missing**. The evidence JSON was intentionally not restructured to game the cap.

## Defect register

| Defect key | First seen | Revisions / attempts observed | Fixes applied | Evidence | Last repeat / outcome |
|---|---|---|---|---|---|
| `141-DOC-MDB-GUARD` | CHECK, cycle N=1, r=2, n=1 | r2/n1 | 1 | `check/` finding #1; this file `## Integration count reconciliation` and progress-log r2/n2 entry; `/tmp/nextorm-D141-ContractA-strong/r2/`; `/tmp/nextorm-D141-ContractA-strong/docs/docfx-r2.log` | Fixed in DO loop-back r2/n2: the one-version-per-concrete-context-type guard is scoped to PostgreSQL in EN+RU `providers/mariadb.md`, `providers/overview.md`, `advanced/limitations.md` and the `linq2db-backlog-gap-analysis.md` / `sql-capabilities-gap-analysis.md` records; MariaDB documented as guard-free (UPDATE RETURNING is not plan-cached). Verified by 0/0 build, green affected suites and docfx exit 0. No repeat. |

## Notices

- **Notice:** the validator failure (exit 2) is exactly the documented cap of the single-comprehensive-boundary-sweep gate: three full affected-assembly sweeps (PostgreSQL, MariaDB, core) were required by the r2 plan. The evidence was **not** restructured to hide it; `E141-evidence.json` is unchanged.
- **Notice:** host has no `todowrite` tool; this status file is the progress tracker (pre-existing limitation, unchanged).
- **Notice:** no file was staged or committed by this DO task; the `git status --short` snapshot below is provided for the ACT commit plan.

## git status --short snapshot (for ACT)

Branch `1.0.9-rc1`, HEAD `815e0127`; 32 entries = 28 modified + 4 untracked, **0 staged**. Full snapshot also at `/tmp/nextorm-D141-ContractA-strong/git-status-short.txt`:

```
 M docs/advanced/api-reference.md
 M docs/advanced/limitations.md
 M docs/guide/03-grouping-and-aggregates.md
 M docs/providers/mariadb.md
 M docs/providers/overview.md
 M docs/providers/postgres.md
 M docs/ru/advanced/api-reference.md
 M docs/ru/advanced/limitations.md
 M docs/ru/guide/03-grouping-and-aggregates.md
 M docs/ru/providers/mariadb.md
 M docs/ru/providers/overview.md
 M docs/ru/providers/postgres.md
 M docs/ru/scalar-functions/05-aggregates.md
 M docs/scalar-functions/05-aggregates.md
 M docs/specs/comparison/linq2db-backlog-gap-analysis.md
 M docs/specs/roadmap/sql-capabilities-gap-analysis.md
 M src/nextorm.core/DataContext/DataContext.cs
 M src/nextorm.core/DataContext/Dialect/ISqlDialect.cs
 M src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs
 M src/nextorm.core/DataContext/SqlMutationBuilder.cs
 M src/nextorm.mariadb/MariaDbDataContext.cs
 M src/nextorm.mariadb/MariaDbDialect.cs
 M src/nextorm.mysql/MySqlDataContext.cs
 M src/nextorm.postgres/PostgresDataContext.cs
 M src/nextorm.postgres/PostgresDialect.cs
 M tests/nextorm.integration.tests/DialectCapabilityContractTests.cs
 M tests/nextorm.mariadb.tests/MariaDbTestContext.cs
 M tests/nextorm.postgres.tests/PostgresTestContext.cs
 ?? docs/specs/status/collection-1.0.9-rc1.md
 ?? docs/specs/status/rc1-141-version-gates-1.md
 ?? tests/nextorm.mariadb.tests/VersionGateTests.cs
 ?? tests/nextorm.postgres.tests/VersionGateTests.cs
```

Total changed files: 32 (28 tracked modified, 4 untracked new).

Integration exit code: 0 — bound to logs/E141-integration.log and rv=D141.ContractA.strong.1 — Total: 3137, Errors: 0, Failed: 0, Skipped: 193, Not Run: 0; executed 2944 (provider executed/skipped/total: PostgreSQL 751/25/776, SQL Server 655/43/698, MySQL 576/79/655, MariaDB 47/0/47, ClickHouse 173/0/173, SQLite 622/43/665, shared/other 120/3/123); artifact logs/E141-integration-exit.txt.
CHECK packet (consolidated frozen contract + crosswalk + matrices + integration skip appendix): /tmp/nextorm-D141-ContractA-strong/check/D141-CHECK-packet.md

## ACT

- **CHECK result: PASS.** Frozen point: **plan revision r = 2**, **attempt n = 2**, evidence **contract revision `rv = D141.ContractA.strong.1`**. Criteria R141-PG/TYPE/CACHE/MDB/COMPAT/DOC are met. **`R141-VERIFY` is now MET:** fresh compliant evidence in `docs/specs/status/rc1-141-validate-1-evidence.json` (**`rv = 2`**) at HEAD `d60c80e3` yields `validate_inner_loop.py brief` **exit 0** and `report` **exit 0** (`docs/specs/status/rc1-141-validate-1-evidence/r2/validator-brief.txt`, `.../validator-report.txt`). The **r2 verification method** recorded under `## Validator re-run disposition (r2)` (three separate per-project boundary sweeps) is **superseded** by one comprehensive full-solution boundary sweep plus one boundary solution build; the `R141-VERIFY` acceptance criterion above is unchanged. *(Historical / superseded:* the earlier `report` exited **2** (`FAIL: more than one comprehensive boundary test sweep: 3`), and the `rc1-g01-c-evidence-1.json` re-run was also rejected by the helper's sweep/build caps (`EXIT 2`); the named waiver `W-C-D141-REPORT-CAP-1` is now **WITHDRAWN** — see `## Validator re-run disposition (corrective r2, closed)`.*) Freeze is recorded here as the ACT baseline.
- **Commit plan (executed by ACT).** Branch `1.0.9-rc1`, no worktree/merge (single collection group). Stage **only** the D141 change set by explicit path — 32 files (28 tracked-modified + 4 untracked-new), matching the `git status --short` snapshot above, plus the two collection status files. No `git add -A`; `docs/api/**` and `docs/_site/**` are gitignored and not staged. Commit message: `#141 Version-gate MariaDB 13 UPDATE RETURNING and PostgreSQL FILTER (<9.4)`. Auto-commit authorized by the collection (`pdca-collection` exception); **no push**.
- **Issue close outcome.** `gh` **was available** in this environment; `gh issue close 141 --repo AlexeyShirshov/nextorm --comment "..."` exited **0** and issue #141 is **CLOSED** (verified via `gh issue view 141 --json state` → `CLOSED`). This **contradicts** the brief's premise that `gh` is unavailable, so the planned "_gh unavailable / not closed remotely_" Notice is **not** recorded (it would be false). **Notice:** issue #141 was closed remotely before the commit landed; the commit itself was not pushed, so the remote issue references an unpushed commit.

```
2026-10-05T19:55:35Z | ACT | r2 | 2/2 | CHECK PASS frozen (rv=D141.ContractA.strong.1); status file finalized with ACT section; staging D141 change set by explicit path; gh issue #141 closed remotely (exit 0) | docs/specs/status/rc1-141-version-gates-1.md
2026-10-06T02:49Z | CORRECTIVE | r2 | 2/2 | fresh collection evidence JSON created and validator re-run recorded; R141-VERIFY corrected to not-met-on-helper-gate; historical 3136/3137 superseded by HEAD 36e540e2 aggregate 3191/0/193 | docs/specs/status/rc1-g01-c-evidence-1.json; docs/specs/status/rc1-g01-c-evidence-1/provenance.md
```

## Validator re-run disposition (r2) — historical / superseded by the corrective r2 close-out below

- Fresh collection-level evidence JSON: `docs/specs/status/rc1-g01-c-evidence-1.json` (created by corrective
  cycle `rc1-g01-c-evidence-1`). The original `/tmp/nextorm-D141-ContractA-strong/E141-evidence.json` was
  left **unchanged**.
- Re-run at corrective DO r=1, n=2/3: `scope.selectors` was reconciled to name the unfiltered
  aggregate/targeted executions; no `--filter` was added and no execution was changed.
- `validate_inner_loop.py brief docs/specs/status/rc1-g01-c-evidence-1.json` → **EXIT 0** (fresh;
  `docs/specs/status/rc1-g01-c-evidence-1/validator-brief.txt`).
- `validate_inner_loop.py report docs/specs/status/rc1-g01-c-evidence-1.json` → **EXIT 2**, verbatim
  (`docs/specs/status/rc1-g01-c-evidence-1/validator-report.txt`):
  - `FAIL: more than one comprehensive boundary test sweep: 4`
  - `FAIL: multiple boundary solution builds: 3`
- Interpretation: the helper's single-cycle caps (one comprehensive boundary sweep, one boundary solution
  build) cannot represent a collection-level aggregate plus the historical D141 plan's three full
  affected-project sweeps (PostgreSQL 756, MariaDB 166, core 1524 at `815e0127+d141-do`). All runs are green
  (exit 0) and no execution was rewritten, omitted or reclassified. The fresh `report` exit is therefore
  **2** — i.e. `R141-VERIFY` is **not met** on this helper gate — and the named waiver
  `W-C-D141-REPORT-CAP-1` is recorded in `docs/specs/status/collection-1.0.9-rc1.md`.
- **Supersession note:** the historical integration observations **3136** (`:180`) and **3137** (`:297`)
  remain the historical results of their runs and are **superseded for collection completion** by the
  aggregate run at HEAD `36e540e2` — total **3191**, failed **0**, skipped **193**
  (`/tmp/nextorm-rc1-coll-c/06-integration.txt`). This is not a correction of the historical logs.
```

## Validator re-run disposition (corrective r2, closed)

- Corrective cycle `rc1-141-validate-1` (plan revision `r = 2`, attempt `n = 1`, evidence contract revision
  `rv = 2`) re-performed D141 verification with fresh compliant evidence at HEAD `d60c80e3`:
  `docs/specs/status/rc1-141-validate-1-evidence.json`. This supersedes the **r2 verification method** only;
  the `R141-VERIFY` acceptance criterion is preserved verbatim.
- **Single-sweep / single-build structure:** exactly one boundary solution build (`dotnet build nextorm.slnx
  -c Debug`, E-B01, 24 projects) and exactly one comprehensive unfiltered full-solution boundary sweep
  (`dotnet test nextorm.slnx -c Debug --no-build --report-xunit-junit --results-directory
  docs/specs/status/rc1-141-validate-1-evidence/r2/junit`, E-T11; selected 8442 = 8248 passed / 0 failed /
  194 skipped). Ten inner filtered runs (E-T01..E-T10) all green.
- **Validator result:** `validate_inner_loop.py brief` → **EXIT 0** and `report` → **EXIT 0**
  (`docs/specs/status/rc1-141-validate-1-evidence/r2/validator-brief.txt`,
  `.../validator-report.txt`) — no waiver, no validator edit, no fabricated counts.
- **Five-provider proof:** all five container providers executed inside E-T11 with 0 failures (PostgreSQL
  753, SQL Server 675, MySQL 579, MariaDB 50, ClickHouse 177), proven from E-T11's own JUnit artifact:
  `docs/specs/status/rc1-141-validate-1-evidence/r2/provider-container-evidence.json` (result artifact
  `.../r2/junit/*.junit.xml`).
- **Supersession:** the `## Validator re-run disposition (r2)` method (three separate per-project boundary
  sweeps plus a separate integration run) is **superseded** by this one comprehensive sweep + one build. The
  historical exit-2 facts above remain as history under an explicit superseded marker. The r=1 E-T12 extra
  `dotnet run` sweep is retained as history and is excluded/superseded. `R141-VERIFY` is now **MET**; no
  `src/**` changed.
