# D229-D230 — D229-D230-join-alias, cycle N=1

## Identity and state

- task_id: D229-D230-join-alias; issues: #229, #230
- title: Join alias generator — #229 anonymous-chain-type fail-closed (NORMGEN009) + #230 syntactic root recovery for a consumed aliased chain
- selected_variant: pdca-dotnet
- base: branch `1.0.9` @ `62e59e53`; milestone `1.0.9`
- cycle: N=1
- plan_revision: r=1
- attempt: n=3/3 (CHECK n=1 FAIL → DO n=2; CHECK n=2 FAIL → escalate decision C, no replan → DO n=3; CHECK n=3 PASS)
- evidence_contract: rv=1
- plan_state: done — cycle closed
- mode: normal (user-gated)
- phase: **ACT complete — cycle closed** (CHECK n=3 PASS; attempt n=3/3)
- status_file: `docs/specs/status/D229-D230-join-alias-1.md`

## Task

Bookkeeping + boundary verification for the already-applied, frozen implementation of issues
**#229** and **#230** in `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs`: #229 makes an alias
over a chain whose projected type is anonymous fail closed with the new diagnostic `NORMGEN009`
(instead of emitting uncompilable generated source naming `global::<anonymous type: …>`); #230 adds a
syntactic query-root recovery path so an aliased inner chain consumed by a later query (`AsCte` →
`ctx.From(cte)` → aliased outer chain, whose builder type is a generated type absent on the input
pass) still emits the downstream aliases. Product code is **frozen** — DO performs no `src/**` or
`tests/**` edits.

## Cycle goal

The frozen generator change is recorded and its boundary is verified: the solution builds with
warnings-as-errors clean, the full alias suite (107 tests) and the per-provider `Alias` SQL-generation
subsets (sqlite/sqlserver/postgres/mysql/mariadb/clickhouse) are green with zero skipped, and the
acceptance matrix E01–E23 is carried in a schema-valid status file, scope and manifest for CHECK to
close.

## Frozen implementation (DO = as-is; no edits)

Uncommitted, frozen on branch `1.0.9` @ base `62e59e53`:

| Path | Change | Issue |
|---|---|---|
| `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` | +202/-1 — adds `NORMGEN009` (anonymous chain type → fail closed) and the #230 syntactic root recovery (`QueryRootOperators`/`ElementPreservingOperators`/`ElementWrappers`) | #229, #230 |
| `src/nextorm.core.sourcegenerator/AnalyzerReleases.Unshipped.md` | registers `NORMGEN009 | NextORM.JoinAlias | Error | Join alias over an anonymous projection type` | #229 |
| `tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs` | adds `Aliased_chain_consumed_by_a_later_query_keeps_the_downstream_aliases` (@527) and `Alias_over_an_anonymous_chain_type_reports_NORMGEN009` (@575) | #230, #229 |
| `docs/guide/04-joins.md`, `docs/ru/guide/04-joins.md` | generator reports `NORMGEN001`–`NORMGEN009`; alias over an anonymous chain type | #229 |
| `docs/advanced/limitations.md`, `docs/ru/advanced/limitations.md` | alias limitation row updated with the anonymous-projection rejection | #229 |

DO **does not edit any of these**. Identity: HEAD `62e59e53c1121a32c4e0854332513a65a41d6516`; current
generator file sha256 `c195b0f8728d8a4bebc0f51c676483676cbbaacbb8d9233924c808b5ac9748cd`; base generator
`62e59e53:src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` sha256
`cc098b69228d2417b10d94b586cc37c3a49a145d47098f4c0dea74787191cd06`.

## Acceptance matrix (required_rows E01–E23)

| ID | Criterion → proof | State |
|---|---|---|
| E01 | #229 root anonymous → fail closed: test `Alias_over_an_anonymous_chain_type_reports_NORMGEN009`; expect NORMGEN009, chain rejected, no `<anonymous type:` in generated output. | met (CHECK n=3 PASS) |
| E02 | #229 non-last (intermediate) step anonymous → same fail-closed; demonstrated case/guard. | met (CHECK n=3 PASS) |
| E03 | #229 anonymous only at last step → supported (rendered as TJoin), compiles, no NORMGEN009 solely for that tail. | met (CHECK n=3 PASS) |
| E04 | #230 aliased inner chain → AsCte → aliased outer chain with error-typed root: test `Aliased_chain_consumed_by_a_later_query_keeps_the_downstream_aliases`; all inner/downstream aliases emitted; generated compilation succeeds. | met (CHECK n=3 PASS) |
| E05 | #230 non-error semantic builder type: prior semantic resolution still selected; syntax fallback does not change valid chains. | met (CHECK n=3 PASS) |
| E06 | #230 error-branch supported roots preserve operators/wrappers: every actual member of QueryRootOperators/ElementPreservingOperators/ElementWrappers inventoried (see `## Operator sets (E06)`) and covered by test or explicit guard. | met (CHECK n=3 PASS; 30-member walk theory + set-equality guard) |
| E07 | #230 identifier/lambda/unwrap branches reachable; unsupported input must not fabricate a root or emit uncompilable output. | met (CHECK n=3 PASS; wrapper/parameter branches) |
| E08 | NORMGEN009 emitted with correct id, release entry, usable location; valid chains do not acquire it. | met (CHECK n=3 PASS) |
| E09 | frozen generated surface: `AliasGeneratedSurfaceTests` unchanged, no snapshot refresh. | met (CHECK n=3 PASS) |
| E10 | sqlite alias SQL-gen via `--filter FullyQualifiedName~Alias`, 0 skipped. | met (58/58, exit 0) |
| E11 | sqlserver alias SQL-gen via `--filter FullyQualifiedName~Alias`, 0 skipped. | met (49/49, exit 0) |
| E12 | postgres alias SQL-gen via `--filter FullyQualifiedName~Alias`, 0 skipped. | met (67/67, exit 0) |
| E13 | mysql alias SQL-gen via `--filter FullyQualifiedName~Alias`, 0 skipped. | met (27/27, exit 0) |
| E14 | mariadb alias SQL-gen via `--filter FullyQualifiedName~Alias`, 0 skipped. | met (22/22, exit 0) |
| E15 | clickhouse alias SQL-gen via `--filter FullyQualifiedName~Alias`, 0 skipped. | met (37/37, exit 0) |
| E16 | four EN+RU docs pages updated; do NOT attribute the unrelated parallel D217 DateTimeOffset row. | met (CHECK n=3 PASS) |
| E17 | red<->green of both new regression tests: each fails against base generator `62e59e53`, passes against current (CHECK to run in disposable trees). | met (CHECK n=3 PASS; base exit 2 / current exit 0) |
| E18 | build warnings-as-errors clean + alias suite fresh. | met (CHECK n=3: build 0W/0E exit 0; alias suite 147/147, 0 skipped, exit 0) |
| E19 | coverage config inspected; changed generator branches delta reported; note the generator is EXCLUDED from coverage.settings.xml. | met (CHECK n=3 PASS; lines 88.17% / branches 66.09%) |
| E20 | nextorm class-priority rows: all three classes (streaming terminal / new SQL function / query-path-plan-cache) are N/A with predicate (no such change). | na |
| E21 | parallel work in tree (D217, examples) fingerprinted and preserved; examples using published rc2 are not fix evidence. | met (CHECK n=3 PASS) |
| E22 | container integration if required by the change; skipped providers cannot be green. | na (generator-only; no container path required) |
| E23 | manifest/scope/status artifacts schema-valid; `validate_inner_loop.py manifest` exit 0. | met (CHECK n=3 PASS; manifest validator exit 0) |

## Operator sets (E06)

Actual string members of the three operator sets in
`src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` (verbatim):

- **`QueryRootOperators`** (`HashSet<string>`, `StringComparer.Ordinal`, :77-80):
  `From`, `CreateQueryBuilder`, `FromSql`, `FromTableFunction`.
- **`ElementPreservingOperators`** (:86-91):
  `Where`, `GroupBy`, `Having`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`,
  `Limit`, `Offset`, `Skip`, `Take`, `Distinct`, `DistinctOn`, `AsCte`, `AsRecursiveCte`,
  `ToCommand`, `Prepare`, `ForUpdate`, `ForShare`, `WithKeywordCase`, `As`.
- **`ElementWrappers`** (:94-97):
  `Cte`, `QueryCommand`, `EntityBuilder`, `TempTableSource`, `MutationCteQuery`.

Consumers: `ResolveRootFromSyntax` uses `QueryRootOperators` (:380); `ResolveQueryElementType` uses
`ElementPreservingOperators` (:419) and `QueryRootOperators` (:422); `UnwrapElement` uses
`ElementWrappers` (:468). E06 requires each member to be covered by a test or an explicit guard.

## Test strategy

Affected filtered subset of the frozen repository (DO inner loop) plus one DO→CHECK boundary sweep.
Scope brief: `artifacts/pdca/D229-D230-join-alias/rv1/scope.json`, validated with
`python3 scripts/validate_inner_loop.py brief ...` (exit 0, pre-run). The alias suite
(`tests/nextorm.alias.tests`) is the primary surface; per-provider `--filter FullyQualifiedName~Alias`
runs cover the generated alias SQL on all six SQL providers. Boundary = one full
`dotnet test tests/nextorm.alias.tests -c Debug` sweep after `dotnet build nextorm.slnx -c Debug`.
No `--no-build`; logs under `artifacts/pdca/D229-D230-join-alias/rv1/`.

## Docs plan

Docs are already updated and frozen as part of the implementation: `docs/guide/04-joins.md` and
`docs/ru/guide/04-joins.md` (generator range `NORMGEN001`–`NORMGEN009`, anonymous-chain rejection) and
`docs/advanced/limitations.md` / `docs/ru/advanced/limitations.md` (alias limitation row). DO makes no
further docs edits. **Attribution:** the `DateTimeOffset` limitation row that also appears in the
`limitations.md` diffs is parallel **D217** work — it is **not** part of #229/#230 and must not be
attributed to this task (E16).

## Perf measurement decision

Not applicable: generator-only change; no query-path/cache files changed; no per-row production work
and no SQL-shape change. No benchmark is run.

## Reconnaissance decision

None: no read-only prerequisite gather beyond reading the frozen files and the boundary toolchain.
The implementation is already applied and frozen; the diagnosis (issue #229/#230) is part of that
frozen change, not new exploration.

## Risks

- Uncommitted frozen implementation: identity must be pinned by HEAD SHA + file sha256; a stray edit
  invalidates the freeze. DO records the current and base generator hashes.
- Selector/count drift: per-provider counts must be reported exactly; skip is never green.
- Parallel work in the tree (D217, examples publishing `1.0.9-rc2`) must be preserved and never claimed
  as evidence for this change.
- `NORMGEN009` release-tracker: `AnalyzerReleases.Unshipped.md` entry is part of the frozen change; a
  missing entry would fail warnings-as-errors.
- E17 red↔green requires disposable trees at base `62e59e53`; it is CHECK's action, not DO's.

## Approval gate

User go obtained for the plan. Any **NEW** code change (any edit under `src/**` or `tests/**`, or a
change to the frozen `NORMGEN009`/root-recovery behavior) requires a separate explicit approval before
DO may proceed. This task is bookkeeping + boundary verification only.

## CHECK n=1 (r=1)

- verdict: **FAIL** → loop-back to DO. Plan revision unchanged (`plan_revision: r=1`, no replan);
  attempt `n` 1 → 2/3. DO adds **test coverage and justified guards only**; the frozen generator
  (`src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs`) is **not rewritten**.
- findings:
  - **E02** — no test or guard closed the intermediate (non-last) anonymous joined-step case; the
    frozen suite only exercised the single-step root case (E01). *Closed this loop-back by
    `Alias_over_an_intermediate_anonymous_join_step_reports_NORMGEN009`.*
  - **E06/E07** — the operator inventory was not matched member-by-member: the `ElementPreservingOperators`
    allowlist members `Where`/`OrderBy` (beyond `GroupBy`/`Select`/`AsCte`) had no proof, and the
    `ResolveIdentifierElementType` parameter branch (fail-closed `null`) was not independently covered.
    *Closed this loop-back by `Error_path_chain_with_Where_and_OrderBy_keeps_the_downstream_aliases`
    plus the `## Edge closures (E06/E07)` justified guards below.*
- scope of loop-back: `tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs`, this status file
  and `artifacts/pdca/D229-D230-join-alias/rv1/**` only. No `src/**` edit.

## DO n=3 (r=1) — recovery-walk coverage + evidence-only closures

Per the escalate decision (no replan; plan_revision still `r=1`). Frozen generator untouched
(`sha256 c195b0f8…`). Added to `tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs`:

- `Error_path_recovery_handles_every_walk_member` (`[Theory]` + `MemberData RecoveryWalkMembers`):
  drives **all 30** allowlist members in one parameterized theory — the 4 `QueryRootOperators` as
  walked root nodes, the 21 `ElementPreservingOperators` via the per-operator
  `PreservingOperatorSuffixes` template map, and the 5 `ElementWrappers` as wrapper-typed root
  arguments. Each case asserts no generator crash, no NORMGEN009 on the valid named chain, no
  `<anonymous type:` token, and, if a surface is emitted, the recovered NAMED element type
  `AliasJoin_A1_Root_A2_Seller<global::Harness.MidRow, TJoin>` (a wrong alias fails; fail-closed is
  the only other allowed outcome).
- `Recovery_allowlists_match_the_expected_members` (`[Fact]`): set-equality guard — reflects the
  generator's three private static sets and compares them to the expected member lists, so any
  add/rename/remove fails loudly and forces a template/reason (set-membership risk).
- `Error_path_recovery_unwraps_a_wrapper_typed_root_argument` (`[Theory]`, 5 wrappers): reaches the
  0%-covered `UnwrapElement` wrapper branch for `Cte`/`QueryCommand`/`EntityBuilder`/`TempTableSource`/
  `MutationCteQuery` (a parameter is enough; the source is compiled, not run).
- `Error_path_recovery_with_a_non_wrapper_parameter_fails_closed` (`[Fact]`): reaches the
  `ResolveIdentifierElementType` parameter branch with a non-wrapper declared type → `UnwrapElement`
  `null` → no alias surface (fail-closed).

Evidence (hardened run dirs, `rv1/do3b/` + `rv1/check3/`): `do3b/filter-recovery-theory.log` (30/30
walk cases, exit 0), `do3b/build-solution.log` (0W/0E exit 0), `do3b/alias-full.log` (total 147, failed
0, succeeded 147, skipped 0, exit 0); the wrapper theory and the non-wrapper fail-closed case are the
separate hardened logs `check3/filter-Error_path_recovery_unwraps_a_wrapper_typed_root_argument.log`
(5/5) and `check3/filter-Error_path_recovery_with_a_non_wrapper_parameter_fails_closed.log` (1/1).

## Edge closures (E06/E07)

Recovery-walk **termination and totality** (the #230 syntactic root recovery:
`ResolveQueryElementType` `:394-441`, allowlists `:77-97`, entry `ResolveRootFromSyntax` `:376-386`):

- **Termination (well-founded descent).** Every recursive step is either
  `ResolveQueryElementType(memberAccess.Expression, …)` (`:420`) or
  `ResolveQueryElementType(arguments[0].Expression, …)` (`:432`) — a **strictly nested** receiver or
  argument node of the same expression. The syntax tree is finite and each step descends one level
  (monotone, acyclic), so the walk terminates on every input; no memoisation is needed.
- **Totality (exhaustive per node).** Per node `ResolveQueryElementType` has exactly three
  mutually-exclusive outcomes and no other: (a) the name is a query-root operator [4] ⇒ return its
  element type (explicit type argument `:424-427`, else source argument `:430-432`, else `null`
  `:434`); (b) the name is an element-preserving operator [21] ⇒ recurse (`:419-420`); (c) any other
  shape ⇒ `UnwrapElement(...)`/`null` (`:416`, `:437`, `:440`). An unrecovered element type is
  `null` ⇒ `ResolveRootFromSyntax` `:382-383` returns `null` ⇒ no alias surface (fail-closed, no
  fabricated root, no uncompilable output).
- **ABSENT vs IN-SET-untested.** An operator **absent** from a set stops the walk at `:437`; for an
  error-typed receiver that yields `null` ⇒ fail-closed — absence is a *safe* state, not a crash.
  The **residual risk is set-membership semantics**: a member that belongs in a set but is absent
  (walk stops one operator early) or a Select-like operator wrongly labeled preserving (walk
  recurses past the operator that establishes a different element type). All 21 preserving members
  traverse **one shared code branch** (`:419-420`), so the theory proves the branch logic; the
  theory additionally drives each member's *name* through it, and the set-equality guard pins the
  membership — the risk is now exercised, not assumed.

| Member(s) | Where | Proof | State |
|---|---|---|---|
| `From`, `CreateQueryBuilder`, `FromSql`, `FromTableFunction` | `QueryRootOperators` (`:77-80`), consumed at `:422` | `[Theory] Error_path_recovery_handles_every_walk_member` (`root` cases) drives each name as a walked root node | covered |
| `Where`, `GroupBy`, `Having`, `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `Limit`, `Offset`, `Skip`, `Take`, `Distinct`, `DistinctOn`, `AsCte`, `AsRecursiveCte`, `ToCommand`, `Prepare`, `ForUpdate`, `ForShare`, `WithKeywordCase`, `As` | `ElementPreservingOperators` (`:86-91`), consumed at `:419-420` | all 21 share the ONE branch `:419-420`; the theory's `preserving` cases drive each member's own `PreservingOperatorSuffixes` template through it | covered |
| `Cte`, `QueryCommand`, `EntityBuilder`, `TempTableSource`, `MutationCteQuery` | `ElementWrappers` (`:94-97`), consumed by `UnwrapElement` (`:467-470`) | `[Theory] Error_path_recovery_unwraps_a_wrapper_typed_root_argument` drives each wrapper-typed argument (incl. a `MutationCteQuery<T>` parameter) to the unwrap | covered; `MutationCteQuery` closed here |
| `ResolveIdentifierElementType` parameter branch | method `:443-457`; parameter branch `:453-454` | `[Fact] Error_path_recovery_with_a_non_wrapper_parameter_fails_closed` reaches it with a non-wrapper parameter (`null` ⇒ fail-closed); the wrapper theory reaches it with a wrapper (element type) | covered |

- **Re-open trigger.** Any add/rename/remove of a name in the three allowlists, any change to the
  `:419-420` / `:422` structure, or a new operator that does not preserve the element type (a
  Select-like mislabel) must update `ExpectedQueryRootOperators` / `ExpectedElementPreservingOperators`
  / `ExpectedElementWrappers` and the per-operator templates and re-run the theory.
  `Recovery_allowlists_match_the_expected_members` fails on any add/rename/remove, and a member with
  no template fails the theory — nothing is silently skipped. The three sets are disjoint; no member
  remains unreachable-by-construction.

## CHECK n=3 (r=1) — PASS

- verdict: **PASS** — cycle closed. attempt `n=3/3`; the frozen generator is byte-identical (sha256
  `c195b0f8728d8a4bebc0f51c676483676cbbaacbb8d9233924c808b5ac9748cd`; base
  `62e59e53:src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` sha256
  `cc098b69228d2417b10d94b586cc37c3a49a145d47098f4c0dea74787191cd06`); no `src/**` edit in DO/ACT.
- evidence (hardened run dirs `rv1/do3b/` + `rv1/check3/`):
  - build — `check3/build-solution.log`: `dotnet build nextorm.slnx -c Debug` 0 Warning(s) / 0 Error(s), exit 0.
  - alias suite — `check3/alias-full.log`: `dotnet test tests/nextorm.alias.tests -c Debug` total 147, failed 0, succeeded 147, skipped 0, exit 0.
  - per-provider alias `--filter FullyQualifiedName~Alias`, all exit 0 / 0 skipped: sqlite 58 (`provider-sqlite.log`), sqlserver 49 (`provider-sqlserver.log`), postgres 67 (`provider-postgres.log`), mysql 27 (`provider-mysql.log`), mariadb 22 (`provider-mariadb.log`), clickhouse 37 (`provider-clickhouse.log`).
  - red↔green — `redgreen/base-focused.log` exit 2 (total 2, failed 2, passed 0) → `redgreen/current-focused-combined.log` exit 0 (total 2, failed 0, succeeded 2); `redgreen/identity-and-summary.txt` (base dll 64b01470… vs current 4f598527…).
  - recovery theory — `do3b/filter-recovery-theory.log` 30/30 exit 0; `check3/filter-Recovery_allowlists_match_the_expected_members.log` 1/1; `check3/filter-Error_path_recovery_unwraps_a_wrapper_typed_root_argument.log` 5/5; `check3/filter-Error_path_recovery_with_a_non_wrapper_parameter_fails_closed.log` 1/1.
  - generator coverage — `check2/e19-coverage.txt`: lines 1670/1894 = 88.17%, branches 846/1280 = 66.09% (generator EXCLUDED from coverage.settings.xml; a temp settings file targeted the dll).
  - suppression/slop — 0 new (0 `NoWarn` / `SuppressMessage` / `#pragma warning` added in the task diff; `slopwatch` is not in `.config/dotnet-tools.json`, so the scan was manual; `0/…`).
  - manifest — `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D229-D230-join-alias/rv1/manifest.json` exit 0 (`act/manifest-validator.log`).

## ACT

Cycle history (N=1, `r=1` throughout — no replan, the plan revision never changed):

- **PLAN (r1)** — bookkeeping + boundary verification for the already-frozen #229/#230 generator change; approval gate recorded.
- **DO (n=1)** — frozen impl / boundary green: solution build 0W/0E, alias suite 107/107, provider alias subsets all exit 0/0 skipped; acceptance matrix + manifest rv=1 written.
- **CHECK n=1 → FAIL** — E02 (no intermediate-anonymous coverage), E06/E07 (allowlist members `Where`/`OrderBy` and the `ResolveIdentifierElementType` parameter branch not independently covered); loop-back to DO, `r=1`.
- **DO (n=2)** — 3 tests + justified guards (no `src/**` edit): intermediate-anonymous case, `Where`/`OrderBy` error-path chain, valid-chain no-NORMGEN009.
- **CHECK n=2 → FAIL** — E06/E07 **recurring**: per-member allowlist coverage and the wrapper/parameter branches still not independently exercised.
- **ESCALATE → decision C** — no replan; keep the frozen generator, prove **termination/totality** of the #230 syntactic recovery walk and cover the allowlist member-by-member (theory over the allowlist + set-equality guard) instead of ad-hoc guards.
- **DO (n=3)** — 30-member recovery-walk theory (`Error_path_recovery_handles_every_walk_member` / `RecoveryWalkMembers`: 4 root + 21 preserving + 5 wrapper) + wrapper/param tests (`Error_path_recovery_unwraps_a_wrapper_typed_root_argument` 5; `Error_path_recovery_with_a_non_wrapper_parameter_fails_closed` 1) + set-equality guard (`Recovery_allowlists_match_the_expected_members`) + evidence links. Hardened run `rv1/do3b/`; check run `rv1/check3/`.
- **CHECK n=3 → PASS** — cycle closed (see `## CHECK n=3 (r=1) — PASS`).

Frozen uncommitted files (identity pinned by HEAD `62e59e53`): the product files listed under
`## Frozen implementation` — `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` (sha256
`c195b0f8728d8a4bebc0f51c676483676cbbaacbb8d9233924c808b5ac9748cd`; base
`62e59e53:src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` sha256
`cc098b69228d2417b10d94b586cc37c3a49a145d47098f4c0dea74787191cd06`),
`src/nextorm.core.sourcegenerator/AnalyzerReleases.Unshipped.md`, the four EN+RU docs pages
(`docs/guide/04-joins.md`, `docs/ru/guide/04-joins.md`, `docs/advanced/limitations.md`,
`docs/ru/advanced/limitations.md`), plus `tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs`
and this status file. All **uncommitted** — no commit/push/merge in this cycle.

**Registry hygiene.** `docs/specs/design/API-NAMING-REVIEW.md` has a `## Закрытие планов (26.09.2026)`
section, but its convention is to record the deletion of a fully shipped
`docs/specs/roadmap/todo_*.md` plan. This task has **no** roadmap `todo_*` plan (nothing to delete) and
**no public API rename** — it only adds the diagnostic id `NORMGEN009`, already registered in
`AnalyzerReleases.Unshipped.md` as part of the frozen change. **No registry change is required**;
nothing was deleted or renamed.

**Open items.** Issues #229 and #230 remain **open**; the implementation is **uncommitted** (the user
commits/pushes). No `src/**` product edit was made in DO/ACT.

## Progress log

2026-10-13T18:58:30Z | DO | r1 | n1/3 | scope brief artifacts/pdca/D229-D230-join-alias/rv1/scope.json validated (validate_inner_loop.py brief exit 0); frozen identity HEAD 62e59e53, generator sha256 c195b0f8…, base sha256 cc098b69…; DO started | artifacts/pdca/D229-D230-join-alias/rv1/scope.json

2026-10-13T19:00:00Z | DO | r1 | n1/3 | boundary solution build dotnet build nextorm.slnx -c Debug exit 0 (0 Warning(s), 0 Error(s)) | artifacts/pdca/D229-D230-join-alias/rv1/build-solution.log

2026-10-13T19:01:00Z | DO | r1 | n1/3 | full alias suite dotnet test tests/nextorm.alias.tests -c Debug: total 107, failed 0, succeeded 107, skipped 0, exit 0 | artifacts/pdca/D229-D230-join-alias/rv1/alias-full.log

2026-10-13T19:02:00Z | DO | r1 | n1/3 | per-provider alias filtered runs --filter FullyQualifiedName~Alias all exit 0, 0 skipped: sqlite 58, sqlserver 49, postgres 67, mysql 27, mariadb 22, clickhouse 37 | artifacts/pdca/D229-D230-join-alias/rv1/provider-*.log

2026-10-13T19:02:30Z | DO | r1 | n1/3 | E09 AliasGeneratedSurfaceTests unchanged (git diff --name-only empty); coverage.settings.xml includes only nextorm.{core,sqlite,postgres,sqlserver} — generator EXCLUDED; parallel D217/examples work fingerprinted | artifacts/pdca/D229-D230-join-alias/rv1/e09-surface-diff-nameonly.txt, artifacts/pdca/D229-D230-join-alias/rv1/parallel-work-fingerprint.txt
2026-10-13T19:01:10Z | DO | r1 | n1/3 | operator sets inventoried (QueryRootOperators 4; ElementPreservingOperators 21; ElementWrappers 5); scope brief validator exit 0; manifest rv=1 E01-E23 written (met E10-E15,E18; na E20,E22; rest open) manifest validator exit 0 | artifacts/pdca/D229-D230-join-alias/rv1/manifest.json, artifacts/pdca/D229-D230-join-alias/rv1/manifest-validator.log

2026-10-13T14:08:35Z | CHECK | r1 | n1/3 | CHECK FAIL: E02 (no intermediate-anonymous coverage), E06/E07 (allowlist members Where/OrderBy and the ResolveIdentifierElementType parameter branch not independently covered); loop-back to DO | docs/specs/status/D229-D230-join-alias-1.md

2026-10-13T14:09:00Z | DO | r1 | n2/3 | DO loop-back started after CHECK FAIL; plan revision unchanged r1, attempt n=2; scope tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs + status + artifacts only, no src/** edit | artifacts/pdca/D229-D230-join-alias/rv1/loopback/

2026-10-13T14:09:30Z | DO | r1 | n2/3 | loop-back build dotnet build nextorm.slnx -c Debug exit 0 (0 Warning(s), 0 Error(s)) | artifacts/pdca/D229-D230-join-alias/rv1/loopback/build-solution.log

2026-10-13T14:09:45Z | DO | r1 | n2/3 | loop-back full alias suite dotnet test tests/nextorm.alias.tests -c Debug: total 110, failed 0, succeeded 110, skipped 0, exit 0 (107 prior + 3 new) | artifacts/pdca/D229-D230-join-alias/rv1/loopback/alias-full.log

2026-10-13T14:09:55Z | DO | r1 | n2/3 | focused filters exit 0, 1/1/0 skipped each: Alias_over_an_intermediate_anonymous_join_step_reports_NORMGEN009, Error_path_chain_with_Where_and_OrderBy_keeps_the_downstream_aliases, Valid_named_aliased_chain_acquires_no_NORMGEN009 | artifacts/pdca/D229-D230-join-alias/rv1/loopback/filter-*.log

2026-10-13T14:10:05Z | DO | r1 | n2/3 | E02/E06/E07/E08 -> met in manifest (E18 loop-back evidence added); validate_inner_loop.py manifest exit 0; generator sha256 c195b0f8… unchanged (no src edit) | artifacts/pdca/D229-D230-join-alias/rv1/loopback/manifest-validator.log

2026-10-13T19:20:00Z | DO | r1 | n3/3 | DO n=3 started per escalate decision (no replan, r=1): recovery-walk coverage + evidence-only row closures; scope tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs + status + artifacts only, no src/** edit | artifacts/pdca/D229-D230-join-alias/rv1/do3/

2026-10-13T19:28:30Z | DO | r1 | n3/3 | new coverage green: Error_path_recovery_handles_every_walk_member (30 cases: 4 root + 21 preserving + 5 wrapper), Error_path_recovery_unwraps_a_wrapper_typed_root_argument (5), Recovery_allowlists_match_the_expected_members, Error_path_recovery_with_a_non_wrapper_parameter_fails_closed; focused filter total 36, failed 0, succeeded 36, skipped 0, exit 0 | artifacts/pdca/D229-D230-join-alias/rv1/do3/filter-recovery-theory.log

2026-10-13T19:30:00Z | DO | r1 | n3/3 | E01/E03/E04/E08/E16/E23 -> met in manifest by linking existing artifacts; E06/E07 evidence strengthened with the walk theory; validate_inner_loop.py manifest exit 0 | artifacts/pdca/D229-D230-join-alias/rv1/do3/manifest-validator.log

2026-10-13T19:48:00Z | DO | r1 | n3/3 | boundary solution build dotnet build nextorm.slnx -c Debug exit 0 (0 Warning(s), 0 Error(s)); full alias suite dotnet test tests/nextorm.alias.tests -c Debug: total 147, failed 0, succeeded 147, skipped 0, exit 0 (110 prior + 37 new) | artifacts/pdca/D229-D230-join-alias/rv1/do3/build-solution.log, artifacts/pdca/D229-D230-join-alias/rv1/do3/alias-full.log

2026-10-13T20:06:30Z | ACT | r1 | n3/3 | CHECK n=3 PASS; cycle closed. Status finalized (phase "ACT complete — cycle closed", plan_state done); every formal evidence pointer relinked from superseded rv1/do3/ to the hardened rv1/do3b/ + rv1/check3/ dirs; manifest relinked for E05/E06/E07/E18/E23; registry hygiene — no API-NAMING-REVIEW.md change required (no roadmap todo_* plan, no public API rename, NORMGEN009 already in AnalyzerReleases.Unshipped.md); final manifest validator exit 0 | docs/specs/status/D229-D230-join-alias-1.md, artifacts/pdca/D229-D230-join-alias/rv1/manifest.json, artifacts/pdca/D229-D230-join-alias/rv1/act/manifest-validator.log
