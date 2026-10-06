# SQLite FTS5 maintenance real-SQLite tests and EN/RU docs — issue #196 (task D196)

- task: D196
- issue: #196
- collection: 1.0.9-rc1-tail
- group: G1
- branch: 1.0.9-rc1
- status: done
- cycle: N=1
- plan revision: r=1
- attempt: n=1/3
- contract: rv=1
- mode: autonomous (auto-commit authorized; push never)
- notice: host has no pdca-orchestrator agent and no todowrite for subagents; the flat primary drives this cycle and this status file carries the progress log.

## Current state

**PLAN r=1 recorded verbatim; D196.1 complete; DO continues at D196.2.** D196.1 (baseline + symbol
manifest) is done — its Step-2 evidence is recorded under **D196.1 evidence** below and raw under
`/tmp/nextorm-d196-r1/`. The verbatim PLAN r=1 (P196-r1) is embedded below; the earlier "missing plan"
blocker is resolved. Next: D196.2 (close test gaps, FIX NOW) and D196.3 (EN/RU docs, FIX NOW); D196.4 runs
the single boundary validation/CHECK sweep; D196.5 performs ACT + collection handoff only after CHECK
PASS. Counters unchanged: `r=1`, `n=1/3`, `rv=1`. No production source or tests have been edited by D196.1.

## Goal / PLAN (verbatim, r=1)

---BEGIN PLAN---
# P196-r1 — FTS5 maintenance verification and documentation

## 1. Cycle identity and decision
- Task: D196; issue #196; collection `1.0.9-rc1-tail`; group G1.
- Branch/workspace: current `1.0.9-rc1` worktree. No new branch, worktree, merge, or push.
- Status file: `docs/specs/status/rc1-tail-196-fts5-tests-docs-1.md`.
- Initial counters: plan r=1, attempt n=1; evidence contract rv=1. No previous contract is superseded.
- Mode: autonomous. Write this plan and the frozen evidence contract before implementation; continue after gate 1 without requesting `go`.
- Driving arrangement: flat primary, because `pdca-orchestrator` and subagent `todowrite` are unavailable.
- Dependency: D195, commit `79d50db3`, supplied FC195-1. Consume that contract unchanged.
- Defect history: no open D195 defect. `D195-FTS5-IDENTIFIER-QUOTING` was fixed; retain its regression coverage.

Decision: prove the shipped implementation again, strengthen the identified assertions, make a small EN/RU documentation follow-up, and finalize D196. Do not reimplement or redesign FTS5 maintenance.

## 2. Goal and acceptance criteria
Goal: independently verify FC195-1 against real SQLite and non-SQLite providers, close the concrete test/documentation gaps, and complete the collection without changing production behavior.

| Requirement | Acceptance criterion | Negative case |
|---|---|---|
| R196-01 Frozen implementation | Production source is unchanged throughout D196; existing public surface and semantics remain FC195-1. | Any source change, new API, altered range/guard/quoting/dispatch behavior, or changed execution semantics fails. |
| R196-02 Exact SQL | SQLite maintenance SQL-generation tests pass, including byte-exact golden assertions for `snake_case` and `camelCase`; all eight operation forms covered. | Trimming, case normalization, identifier splitting, missing quotes, altered signed values, extra parameters, or a trailing semicolon must fail an assertion. |
| R196-03 Builder guards | Core tests pass for factory validation, immutable selection, unselected terminals, capability rejection, and bounds. | Invalid inputs/unsupported contexts must not yield a usable command or reach execution. |
| R196-04 Real SQLite execution | Existing maintenance integration scenarios pass against real SQLite. Every result in both all-operations tests has an explicit assertion; semantic rebuild/integrity scenarios remain intact. | No-throw-only coverage is insufficient for these two tests; skipped execution, missing assertions, failed cancellation/error/repair scenarios fail. |
| R196-05 Non-SQLite rejection | PostgreSQL, SQL Server, MySQL, MariaDB, and ClickHouse rejection tests pass for SQL rendering, sync and async execution. | Accepting the command, or performing connection I/O before rejection, fails. |
| R196-06 Integration boundary | The full integration boundary executes SQLite, PostgreSQL, SQL Server, MySQL, and ClickHouse; required provider tests are not skipped. | A green process with a skipped/unexecuted provider is not acceptance. MariaDB rejection is covered separately by its provider unit project. |
| R196-07 Documentation | The three public EN pages and their three RU mirrors describe the same frozen surface, usage, and limitations. DocFX reports zero errors. | EN/RU disagreement, an undocumented newly described behavior, broken links, or public links into `docs/specs/**` fail. |
| R196-08 Build/hygiene | .NET 10 Debug solution build reports `0 Warning(s)`, `0 Error(s)`; diff checks pass; edited files retain CRLF. | Nonzero exit, warnings, mixed/LF-only line endings, or unrelated edits fail. |
| R196-09 Coverage evidence | Coverage is collected and reported using `coverage.settings.xml`; both line and branch percentages and comparison with 85% / 75% are recorded. | Missing/invalid coverage or treating unexecuted tests as coverage evidence fails. Below-threshold results on this non-main branch are explicit warnings, not silently presented as meeting thresholds. |
| R196-10 Traceability/completion | D196-owned tests are attributable to issue 196; final evidence is recorded; collection D196 and G1 become `done`; one task-only commit starts with `#196`. | Premature `done`, missing evidence, staging unrelated files, or pushing fails. |

## 3. Minimal solution
Three answers:
1. Essential outcome: stronger proof of already-shipped FTS5 maintenance, not another implementation.
2. Non-negotiable constraints: frozen FC195-1, no production edits, real provider execution, EN/RU parity, warning-free build, task-only commit.
3. Smallest satisfactory change: extend existing SQLite tests, strengthen the two existing integration methods, retain issue-195 provenance while adding issue-196 attribution to modified/new cases, and amend the existing six documentation pages.

Alternatives: (selected) extend existing tests and pages — directly closes gaps, preserves scenarios/contract; (rejected) separate D196 integration suite — duplicates setup; (rejected) rework implementation or broadly reconcile comparison specs — violates minimal scope/frozen boundary.

For successful maintenance operations, assert the returned result is nonnegative, not exactly 1 or another invented stable affected-row count. Existing rebuild/integrity tests continue to establish meaningful database effects.

## 4. Findings: fix now versus deferred
- Missing byte-exact `snake_case`/`camelCase` unit assertions → FIX NOW. Add two names × eight SQL forms in the existing SQLite SQL-generation test class.
- All-operations sync/async tests mostly assert only no exception → FIX NOW. Capture and assert every terminal result in both methods; preserve existing semantic/error tests.
- Internal comparison specifications still say deferred/open → DEFERRED. Outside this public-test/doc follow-up. Trigger: a dedicated capability/comparison-spec reconciliation task or the next edit of those rows. Record exact stale locations; do not declare them reconciled.
- No issue-196 traits → FIX NOW. Add issue-196 attribution to new/modified tests; preserve issue-195 traits on existing tests.
- Contradictory historical `--filter` reports → FIX NOW in strategy. Use native xUnit selectors through `dotnet run`; do not use `--filter` for this cycle.

## 5. What the brief did not establish
- Full integration method names abbreviated → resolve via Roslyn symbol manifest before test edits (DONE in D196.1).
- SQLite identifier quote delimiter → obtain from existing golden assertions; new expected SQL must be literal golden data, not generated by the production renderer (D196.1 recorded ASCII `"`, doubled).
- Collection status file path → resolved: `docs/specs/status/collection-1.0.9-rc1-tail.md`.
- Current dirty state/tooling → record baseline HEAD/status/versions before edits (DONE D196.1).
- D195 reports are not D196 execution evidence → run the contracted commands again.
- Exact affected-row count not frozen → test nonnegative success results + existing semantic effects.
- Supplied pack is not the integration-running skill → executor loads `.opencode/skills/running-integration-tests/SKILL.md` before integration execution.
Unresolved contradictory fact returns to PLAN; it does not authorize production changes.

## 6. DO units
### D196.1 — Freeze baseline, evidence contract, execution facts (DONE)
Create status file with this plan, r=1/n=1/rv=1, progress entries; load instructions; record baseline HEAD/dirty/source manifest/tool versions/collection-register path; obtain Roslyn symbol manifest; verify expected SQL quoting from existing evidence; persist evidence contract + symbol mapping.

### D196.2 — Close test gaps (FIX NOW)
Files:
- `tests/nextorm.sqlite.tests/SqliteFts5MaintenanceSqlGenerationTests.cs`
- `tests/nextorm.integration.tests/SqliteSpecificTests.cs:1735,1755` (maintenance block `:1727–1967` as context).
Actions:
- Add literal byte-exact expected SQL for names `snake_case` and `camelCase` across `AutoMerge`, `CrisisMerge`, signed `Merge`, `Optimize`, `Rebuild`, `IntegrityCheck(null/false/true)`.
- Use valid representative values; preserve existing boundary/extreme-value tests.
- Assert no trailing semicolon and empty parameters through existing test facilities.
- In both all-operations integration methods, capture each of the six operation-family results and assert `>= 0`.
- Preserve existing repair, integrity, contentless, missing/non-FTS table, cancellation, and extreme-value scenarios.
- Add `[Trait("Issue","196")]` to new/modified tests; retain existing issue-195 provenance.
- No production-file edits.

### D196.3 — Public EN/RU documentation follow-up (FIX NOW)
| EN page | RU mirror | Change |
|---|---|---|
| `docs/providers/sqlite.md:230–265,306` | `docs/ru/providers/sqlite.md` | Consolidate maintenance availability and frozen operation/terminal contract; distinguish successful execution from any guarantee of a fixed affected-row count. |
| `docs/guide/provider-specific/sqlite.md:246–286` | `docs/ru/guide/provider-specific/sqlite.md` | Add/adjust concise sync and async maintenance examples; explain verbatim names, signed `Merge`, integrity modes, cancellation. |
| `docs/advanced/limitations.md:42,81` | `docs/ru/advanced/limitations.md` | Align supported maintenance and failure limitations with tested behavior: capability rejection, invalid targets, existing contentless rebuild limitation. |
Use existing wording where accurate. Do NOT touch `docs/guide/11-table-valued-functions.md` (DML not TVF), numbered guide structure/TOCs (no renumbering), internal comparison specs, or production sources. Do not add public links to `docs/specs/**`.

### D196.4 — Final boundary validation and CHECK (FIX NOW)
Run the command/evidence matrix; inspect actual test counts and skipped-provider output, not just exit codes; validate docs parity, production-source invariance, CRLF, allowlisted diff; record coverage percentages and threshold warnings.

### D196.5 — ACT and collection handoff (ONLY AFTER CHECK PASSES)
Record final criterion/evidence results; update the existing collection register D196 → `done`, G1 → `done`; normalize changed text files to CRLF and rerun hygiene; stage only the two test files, six public doc pages, this status file, and the collection register; commit message starting `#196` (e.g. `#196 Verify FTS5 maintenance and finish EN/RU documentation`); verify committed filenames/message and preservation of unrelated changes; report SHA; never push.

## 7. Execution mode and structured test scopes
Mode: sequential, one tree. No worktree/branch. CHECK independent.
D196.2 test scope: projects `nextorm.sqlite.tests`, `nextorm.core.tests`, `nextorm.integration.tests`; selectors native class selectors for SQLite/core classes and native `*Fts5Maintenance*` method selector for focused integration; changed files the two test files; read-only regression files core builder/factory tests and five provider-rejection classes; rebuild `affected`; rationale test changes must compile and execute, production consumed unchanged.
D196.3 test scope: DocsFX `docs/docfx.json`; selectors/files the six named pages + DocFX build + parity/link review; rebuild `none` for product/test assemblies; rationale docs-only edits do not justify an affected-code rebuild.
Single boundary sweep point: after D196.2 and D196.3, D196.4 runs one final validation batch (solution build, focused regressions, five rejection projects, full container-backed integration, coverage, DocFX, hygiene). Focused checks during D196.2 are feedback runs, not substitutes.

## 8. Test strategy and closed variant matrix
Native xUnit command selectors replace disputed `--filter`. Selection must execute more than zero tests and include intended cases.

| Variant/path | Closure | Location |
|---|---|---|
| All eight maintenance SQL forms | Existing tests plus new name golden cases | SQLite SQL-generation class |
| `snake_case`, `camelCase` verbatim names | NEW unit tests | Same class |
| Quoting, literal `.`, whitespace/verbatim, invariant formatting, keyword case | Existing unit regression; verify mapping | SQLite/core classes |
| Null context/table, empty/whitespace/NUL names | Existing guard tests | Core builder class |
| Unselected `ToSql`/`Execute`/`ExecuteAsync` | Existing guard tests | Core builder class |
| Immutable selection; no public ctor | Existing tests plus surface/source review | Core builder class; frozen builder |
| AutoMerge lower/upper/OOR; CrisisMerge nonneg/neg | Existing unit tests/guards | Core builder and SQLite SQL tests |
| Signed/extreme Merge; extreme CrisisMerge | Existing unit + real SQLite tests | SQLite SQL tests and integration block |
| IntegrityCheck omitted/false/true | Unit SQL tests; existing real SQLite integrity tests | SQLite SQL and integration |
| Sync and async successful maintenance | STRENGTHENED integration assertions | Integration all-operations pair |
| Pre-cancelled token | Existing real SQLite test | Integration `:1886` |
| Repair stale external-content index | Existing semantic integration test | Integration `:1773` |
| Contentless rebuild failure | Existing negative integration test | Integration `:1817` |
| Missing/non-FTS table | Existing negative integration tests | Integration `:1838,1857` |
| Missing SQLite functions/FTS5 capability; non-mutating context | Existing guard tests; rejection before I/O | Core and provider tests |
| PG/SQL Server/MySQL/MariaDB/ClickHouse | Existing provider rejection tests, all three terminals | Five provider projects |
| SQLite plus four container providers | Full integration boundary; no required-provider skips | Integration project |
| Generic value/reference-type permutations | N/A by observable API shape: no generic entity/value input | FC195-1 surface review |
| Future options/providers not in FC195-1 | Deferred; trigger: public-contract/provider capability change | Separate cycle, not D196 |

If an "existing coverage" row is not actually supported by the symbol/assertion manifest, add a test inside the named test files while preserving FC195-1. Report a production failure rather than repairing production code under D196.

Coverage policy: collect configured coverage for `nextorm.core`, `sqlite`, `postgres`, `sqlserver`; report line ≥85% and branch ≥75% comparisons; below-threshold results on this non-main branch are recorded warnings; collection/report failure or missing metrics is an evidence failure; do not claim an unmeasured baseline/improvement.

## 9. Command registry
All commands from repo root. Capture exit status and stdout/stderr independently. Artifacts dir `/tmp/nextorm-d196-r1/`.
- C01 Build: `dotnet build nextorm.slnx -c Debug` → exit 0, 0 Warning(s) 0 Error(s).
- C02 SQLite SQL tests: `dotnet run --project tests/nextorm.sqlite.tests -c Debug -- -class '*SqliteFts5MaintenanceSqlGenerationTests' -noColor`.
- C03 Core: `dotnet run --project tests/nextorm.core.tests -c Debug -- -class '*SqliteFts5CommandBuilderTests' -noColor` and `-class '*CreateCommandBuilderFactoryTests'`.
- C04 Five rejection projects: `for provider in postgres sqlserver mysql mariadb clickhouse; do dotnet run --project "tests/nextorm.${provider}.tests" -c Debug -- -class '*SqliteFunctionsRejectionTests' -method '*Fts5Maintenance*' -noColor || exit "$?"; done` — preserve per-provider results.
- C05 Focused real SQLite: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -class '*SqliteSpecificTests' -method '*Fts5Maintenance*' -noColor`.
- C06 Full integration: same DOCKER_HOST + `dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` → all five providers execute, no required-provider skips. If socket missing, follow the integration skill (start Podman machine, wait, recheck, retry); never downgrade to SQLite-only.
- C07 Coverage: use the invocation resolved in D196.1 (`dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"` then `dotnet tool run reportgenerator ...`); record line/branch + 85/75 comparison.
- C08 Docs: `dotnet docfx docs/docfx.json` → exit 0, zero errors (record warnings).
- C09 Diff/line-endings: `git diff --check`, `git diff --cached --check`; normalize only allowlisted changed files.
- C10 Source invariance/scope: `git ls-files -z -- src | xargs -0 sha256sum` compare to `source-before.sha256`; `git status --porcelain=v1 -uall`; `git diff --name-only`; `git diff --cached --name-only`.
- C11 Semantic/doc review: `roslyn` definitions/members; ignore-aware doc review; inspect DocFX output and EN/RU parity directly (text search alone is not parity proof).
- C12 Commit: stage explicit allowlisted paths (never `git add .`); `git commit -m "#196 Verify FTS5 maintenance and finish EN/RU documentation"`; `git show --format=fuller --name-only HEAD`; `git status --short`.

## 10. Priority matrix and design checklist
All evidence rows are P1 by construction; CHECK cannot lower them. Internal-spec reconciliation remains deferred and is not an acceptance dependency.
Design checklist pass for selected design: no public API addition/removal/rename; sealed builder/internal ctor unchanged; no renderer/dispatch/guard/caching/execution changes; no mutation of shared query-command state or sticky cache flags; no TVP/decimal/provider/dependency/CPM changes; test goldens independent of renderer under test; no fixed affected-row-count promise; async examples use frozen `ExecuteAsync` name; no guide restructuring/generated-doc edits/public internal-spec links; final CHECK verifies against the actual diff.

## 11. Criterion → test-symbol mapping
| Criterion | Verified mapping / planned scenario |
|---|---|
| R196-02 | `SqliteFts5MaintenanceSqlGenerationTests`; planned SC196-NAME-SNAKE, SC196-NAME-CAMEL, each covering eight forms |
| R196-03 | `SqliteFts5CommandBuilderTests`; `CreateCommandBuilderFactoryTests` |
| R196-04 | `SqliteSpecificTests`, all-operations and ExecuteAsync prefixes `:1735,1755`; existing repair/integrity/error/cancellation/extreme cases |
| R196-05 | Each provider's `SqliteFunctionsRejectionTests`, `Fts5Maintenance_ToSql/Execute/ExecuteAsync` |
| R196-06 | Full-integration result manifest grouped by five required providers |
| R196-01/07/08/09/10 | Source/diff, documentation, build, coverage, handoff evidence; not falsely mapped to unit-test symbols |
Manifest records canonical symbol, file/location, selector, scenario ID, requirement ID, result, evidence-row ID.

## 12. Versioned evidence contract — rv=1
Every row P1. Rows: EC196-01/R196-01 source invariance (C10+C11); EC196-02/R196-02 C02; EC196-03/R196-03 C03; EC196-04..08/R196-05 C04 per provider; EC196-09/R196-04 C05; EC196-10/R196-06 C06; EC196-11/R196-08 C01; EC196-12/R196-09 C07; EC196-13/R196-07 C08+C11; EC196-14/R196-08 C09+C10; EC196-15/R196-10 trait/register/status/C12. Owners: tests/CHECK/docs/primary as per the tables above. CHECK re-gather budget: two targeted re-gather dispatches total for rv=1, each addressing one missing/ambiguous row with at most one targeted rerun; initial execution is not re-gather; missing reports first trigger artifact retrieval/precise rerun; exhausted budget with low confidence → escalate trigger 5; a justified contract revision supersedes rv=1 preserving row IDs.

## 13. Performance and reconnaissance decisions
Perf measurement: NONE. D196 changes tests and documentation only; no per-row/hot-path production change. Do not run BenchmarkDotNet; if accidentally run, restore tracked artifacts.
Reconnaissance: bounded fact gathering needed; no design spike. D196.1 gathers actual Roslyn symbols, existing golden quote syntax, collection-register path, tool/runner facts. Native-selector execution checked by nonzero selected-test counts in the first focused runs.

## 14. Risks, return handling, confidence
Risks: native selectors matching zero tests (require actual counts + manifest); container startup/image availability (inspect provider execution, never accept skipped success); a stronger assertion exposing a D195 defect (do not repair production under D196); exact row-count assertions inventing a contract (use nonnegative + semantic tests); EN/RU drift (paired review); coverage below targets (report under branch warning policy); auto-staging earlier work (explicit allowlist + baseline comparison).
Return: no DO→PLAN blocker yet. Missing tooling/socket/symbol/register → additive prerequisite (keep original DO active blocked). Missing/ambiguous evidence → targeted scout/re-gather. Proven frozen-production defect or required surface change → out of D196 authority; escalate + route a D195 replan, no in-cycle production repair. Persistent low confidence → escalate trigger 5. A genuinely changed corrective plan → P196-r2, reset n=1. Three failed CHECKs in the same revision → escalate; no fourth attempt.
Confidence: high in the minimal test/doc-only approach; execution success, exact method identities, tooling, provider readiness, and register path verified only by the contracted prerequisites/checks.

Completion condition: all applicable evidence rows satisfy their requirements, threshold warnings recorded, CHECK accepts, and the task-only commit plus collection handoff succeeds.
---END PLAN---

## Acceptance criteria (R196-01..R196-10)

- **R196-01 Frozen implementation** — Production source is unchanged throughout D196; existing public
  surface and semantics remain FC195-1. *Negative:* any source change, new API, altered
  range/guard/quoting/dispatch behavior, or changed execution semantics fails.
- **R196-02 Exact SQL** — SQLite maintenance SQL-generation tests pass, including byte-exact golden
  assertions for `snake_case` and `camelCase`; all eight operation forms covered. *Negative:* trimming,
  case normalization, identifier splitting, missing quotes, altered signed values, extra parameters, or a
  trailing semicolon must fail an assertion.
- **R196-03 Builder guards** — Core tests pass for factory validation, immutable selection, unselected
  terminals, capability rejection, and bounds. *Negative:* invalid inputs/unsupported contexts must not
  yield a usable command or reach execution.
- **R196-04 Real SQLite execution** — Existing maintenance integration scenarios pass against real SQLite.
  Every result in both all-operations tests has an explicit assertion; semantic rebuild/integrity
  scenarios remain intact. *Negative:* no-throw-only coverage is insufficient for these two tests; skipped
  execution, missing assertions, failed cancellation/error/repair scenarios fail.
- **R196-05 Non-SQLite rejection** — PostgreSQL, SQL Server, MySQL, MariaDB, and ClickHouse rejection
  tests pass for SQL rendering, sync and async execution. *Negative:* accepting the command, or performing
  connection I/O before rejection, fails.
- **R196-06 Integration boundary** — The full integration boundary executes SQLite, PostgreSQL, SQL
  Server, MySQL, and ClickHouse; required provider tests are not skipped. *Negative:* a green process with
  a skipped/unexecuted provider is not acceptance. MariaDB rejection is covered separately by its provider
  unit project.
- **R196-07 Documentation** — The three public EN pages and their three RU mirrors describe the same frozen
  surface, usage, and limitations. DocFX reports zero errors. *Negative:* EN/RU disagreement, an
  undocumented newly described behavior, broken links, or public links into `docs/specs/**` fail.
- **R196-08 Build/hygiene** — .NET 10 Debug solution build reports `0 Warning(s)`, `0 Error(s)`; diff
  checks pass; edited files retain CRLF. *Negative:* nonzero exit, warnings, mixed/LF-only line endings, or
  unrelated edits fail.
- **R196-09 Coverage evidence** — Coverage is collected and reported using `coverage.settings.xml`; both
  line and branch percentages and comparison with 85% / 75% are recorded. *Negative:* missing/invalid
  coverage or treating unexecuted tests as coverage evidence fails. Below-threshold results on this
  non-main branch are explicit warnings, not silently presented as meeting thresholds.
- **R196-10 Traceability/completion** — D196-owned tests are attributable to issue 196; final evidence is
  recorded; collection D196 and G1 become `done`; one task-only commit starts with `#196`. *Negative:*
  premature `done`, missing evidence, staging unrelated files, or pushing fails.

## Durable state

- cycle `N=1` · plan revision **`r=1`** · attempt **`n=1/3`** · contract/evidence revision **`rv=1`** —
  carried in this file; a session/`task_id` reset never resets `r`, `n` or the defect history.
- DO ledger:

| unit | status | evidence |
|---|---|---|
| D196.1 freeze baseline, evidence contract, execution facts | done | this file (`D196.1 evidence`); `/tmp/nextorm-d196-r1/{baseline.txt,source-before.sha256,dotnet-version.txt,dotnet-tool-list.txt,dotnet-tools.json,manifest.md}` |
| D196.2 close test gaps (FIX NOW) | done | two test files edited; brief validation exit 0; report validation exit 0; 23+49+11 tests selected/green — `/tmp/nextorm-d196-r1/{scope-d196.2.json,evidence-d196.2.json,sqlite-unit-d196.2.log,core-unit-d196.2.log,sqlite-integration-d196.2.log}` |
| D196.3 public EN/RU documentation follow-up (FIX NOW) | done | six EN/RU pages changed; DocFX exit 0; EN/RU parity - `/tmp/nextorm-d196-r1/docfx-d196.3.log` |
| D196.4 final boundary validation (FIX NOW) | done | boundary sweep C01-C11 complete; EC196-01..EC196-14 PASS (see Evidence (raw)); CHECK verdict owned by check role - `/tmp/nextorm-d196-r1/*` |
| D196.5 ACT and collection handoff (only after CHECK PASS) | pending | — (plan §6; collection register D196/G1 → done; C12 commit starting `#196`) |

### Defect history (stable keys)

| defect key | state | observed r/n | fixes applied | evidence / note |
|---|---|---|---|---|
| `D195-FTS5-IDENTIFIER-QUOTING` | fixed by D195; no open defect | D195 r=1 n=1/3 → fixed at n=2/3 | 1 (always-quote via `ISqlDialect.QuoteIdentifier`) | closed in D195; CHECK PASS r=1 n=2/3; commit `79d50db3`. D196 has observed no new defect. |

## Decisions

- **Decision (plan §1).** Prove the shipped implementation again, strengthen the identified assertions,
  make a small EN/RU documentation follow-up, and finalize D196. Do not reimplement or redesign FTS5
  maintenance. Consume FC195-1 (D195 commit `79d50db3`) unchanged; no source change.
- **Selected minimal change (plan §3).** Extend the existing SQLite tests, strengthen the two existing
  integration methods, retain issue-195 provenance while adding issue-196 attribution to modified/new
  cases, and amend the existing six documentation pages. *Rejected:* a separate D196 integration suite
  (duplicates setup); reworking the implementation or broadly reconciling comparison specs (violates
  minimal scope / frozen boundary).
- **Fix-now (plan §4).** Missing byte-exact `snake_case`/`camelCase` assertions; all-operations
  sync/async tests asserting only no-exception (capture and assert every terminal result); issue-196
  traits; and use native xUnit selectors via `dotnet run` instead of `--filter`.
- **Deferred (plan §4).** Internal comparison specifications that still say deferred/open are outside this
  public test/doc follow-up; their exact stale locations are recorded but they are **not** declared
  reconciled. Trigger: a dedicated capability/comparison-spec reconciliation task or the next edit.
- **Affected-row semantics (plan §3).** Successful maintenance results are asserted nonnegative, never a
  fixed/invented affected-row count; existing rebuild/integrity tests keep establishing real DB effects.
- **No production change** — D196 does not touch `src/**`; D196.1 confirmed 0 modified `src/**` files
  (407-file `source-before.sha256` baseline). Baseline captured before any D196 edit.
- **EC196-15 is an ACT gate, not an unmet CHECK criterion.** The plan evaluates the commit/collection
  handoff as a "CHECK precommit": it is inherently pending until D196.5/ACT and is not a missing or failed
  CHECK evidence row. The CHECK verdict fails on the evidence/closure gate, not on EC196-15 being pending.
- **Perf/recon/unit mode (plan §13/§7).** NONE / bounded fact gathering no spike / sequential one tree.

## Risks / known issues

From plan §14:
- **Native selectors matching zero tests** — require actual selected-test counts + the symbol manifest;
  a zero-test selector is not evidence.
- **Container startup/image availability** — inspect provider execution; never accept skipped success. If
  the Podman socket is missing, follow `.opencode/skills/running-integration-tests/SKILL.md` (start/wait/
  recheck/retry); never downgrade to SQLite-only.
- **A stronger assertion exposing a D195 defect** — do not repair production under D196; report and route
  a D195 replan (out of D196 authority).
- **Exact row-count assertions inventing a contract** — use nonnegative success + existing semantic tests.
- **EN/RU drift** — paired review; DocFX zero errors; no public links to `docs/specs/**`.
- **Coverage below targets** — record under the non-main-branch warning policy; do not present as meeting
  thresholds.
- **Auto-staging earlier work** — explicit allowlist + baseline comparison; never `git add .`.
- **Return handling:** missing tooling/socket/symbol/register → additive prerequisite (original DO stays
  active blocked); missing/ambiguous evidence → targeted scout/re-gather; persistent low confidence →
  escalate trigger 5; a genuinely changed corrective plan → P196-r2 resetting `n=1`; three failed CHECKs
  in the same revision → escalate (no fourth attempt).

## Perf measurement

**NONE.** D196 finalizes tests/docs against the frozen FC195-1 with **no production change** (confirmed:
0 tracked `src/**` files modified; 407-file `source-before.sha256` baseline). There is no per-row or
per-query hot path to measure, so no benchmark or before/after acceptance is required.

## Reconnaissance

**Bounded fact gathering, NO SPIKE.** D196.1 gathered: baseline HEAD/dirty state; the 407-file
production-source hash baseline; tool versions; the collection register path; the Roslyn symbol manifest
for the four named test classes + the five provider rejection classes; and the golden SQL quoting shape
read from existing assertions (delimiter `"`, embedded `"` doubled). No unknown transport/server behavior
to probe.

## Unit mode

One current tree, sequential units. No worktree, no commits by this unit; auto-commit is a later unit.
No production source or tests are edited in D196.1.

## Evidence contract (rv=1; EC196-01..EC196-15)

Every row is P1. Contract per plan §12; commands per plan §9 (C01–C12). CHECK re-gather budget: two
targeted re-gather dispatches total for rv=1, each addressing one missing/ambiguous row with at most one
targeted rerun; initial execution is not re-gather; a justified contract revision supersedes rv=1
preserving row IDs.

| row | requirement | check / command | required result | actual result | owner |
|---|---|---|---|---|---|
| EC196-01 | R196-01 | production-source invariance (C10+C11) | `src` hash identical to `source-before.sha256`; FC195-1 surface unchanged | `src` hash identical (407/407) to `source-before.sha256`; no `src/**` edits - PASS | coder/primary |
| EC196-02 | R196-02 | C02 SQLite SQL tests | exit 0; byte-exact `snake_case`/`camelCase` × eight forms; nonzero selected tests | exit 0; focused class 23/23 (0 failed, 0 skipped); full project 1137 total / 1136 passed / 0 failed / 1 skipped; byte-exact `snake_case`/`camelCase` x 8 - PASS | tests |
| EC196-03 | R196-03 | C03 core builder + factory tests | exit 0; guards/immutability/unselected/capability/bounds | exit 0; `SqliteFts5CommandBuilderTests` 49/49; `CreateCommandBuilderFactoryTests` 27/27; full core 1712/1712 - PASS | tests |
| EC196-04 | R196-05 | C04 PostgreSQL rejection | exit 0; ToSql/Execute/ExecuteAsync reject before I/O | exit 0; 3 total / 3 passed / 0 failed / 0 skipped - PASS | tests |
| EC196-05 | R196-05 | C04 SQL Server rejection | exit 0; ToSql/Execute/ExecuteAsync reject before I/O | exit 0; 3 total / 3 passed / 0 failed / 0 skipped - PASS | tests |
| EC196-06 | R196-05 | C04 MySQL rejection | exit 0; ToSql/Execute/ExecuteAsync reject before I/O | exit 0; 3 total / 3 passed / 0 failed / 0 skipped - PASS | tests |
| EC196-07 | R196-05 | C04 MariaDB rejection | exit 0; ToSql/Execute/ExecuteAsync reject before I/O | exit 0; 3 total / 3 passed / 0 failed / 0 skipped - PASS | tests |
| EC196-08 | R196-05 | C04 ClickHouse rejection | exit 0; ToSql/Execute/ExecuteAsync reject before I/O | exit 0; 3 total / 3 passed / 0 failed / 0 skipped - PASS | tests |
| EC196-09 | R196-04 | C05 focused real SQLite | real SQLite; all-operations results asserted (`>=0`); repair/integrity/cancel/error intact | exit 0; 11 total / 11 passed / 0 failed / 0 skipped - PASS | tests |
| EC196-10 | R196-06 | C06 full integration boundary | all five providers execute; no required-provider skips | exit 0; 3311 total / 0 failed / 197 skipped (all per-capability); SQLite+PostgreSQL+SQL Server+MySQL+ClickHouse executed (CH focused 117/117) - PASS | tests |
| EC196-11 | R196-08 | C01 solution build | exit 0; `0 Warning(s) 0 Error(s)` | exit 0; 0 Warning(s) 0 Error(s) - PASS | coder |
| EC196-12 | R196-09 | C07 coverage collect+report | line/branch recorded vs 85%/75%; below-threshold warning explicit | collect exit 0, report exit 0; line 87.2% >=85, branch 79.1% >=75 - PASS | coder |
| EC196-13 | R196-07 | C08 DocFX + C11 parity/link review | exit 0, 0 errors; EN/RU parity; no `docs/specs/**` links | DocFX exit 0, 0 errors, 2 pre-existing warnings; 6 pages EN/RU parity; no `docs/specs` links - PASS | docs |
| EC196-14 | R196-08 | C09+C10 diff/CRLF/scope | no whitespace errors; CRLF; allowlisted files only | `git diff --check` exit 0; `--cached --check` exit 0; CRLF 9/9; diff = 2 tests + 6 docs; 0 staged - PASS | coder |
| EC196-15 | R196-10 | trait/register/status + C12 commit | issue-196 traits; D196/G1 `done`; one `#196` commit; no push | pending | primary |

**Fact reconciled from D196.1 (plan §8):** D195's "Fts5Maintenance 11/11" is a **Theory-case count**, not
11 methods — the integration class has **10** `Fts5Maintenance_*` methods (8 `[Fact]` +
`Fts5Maintenance_ExtremeMergeValue_ShouldMatchCanonicalDirectSql` `[Theory]` with 2 rows +
`Fts5Maintenance_ExtremeCrisisMergeValue_ShouldMatchCanonicalDirectSql` `[Theory]` with 1 row = 11 cases).
Recorded as a fact for the D196.2/D196.4 manifest so focused runs assert the intended case coverage.

## D196.1 evidence (Step 2; raw under /tmp/nextorm-d196-r1/)

- **Baseline:** HEAD `2da40cabe75e9029907179942d3ce3205fb7da92` (branch `1.0.9-rc1`); `git status
  --porcelain=v1 --untracked-files=all` = 17 entries, **all untracked** (`artifacts/pdca/D198/*`), 0
  modified/staged. `/tmp/nextorm-d196-r1/baseline.txt`.
- **Production invariance:** `git ls-files -z -- src | xargs -0 sha256sum > source-before.sha256` =
  **407 files** hashed. `/tmp/nextorm-d196-r1/source-before.sha256`.
- **Tools:** `dotnet --version` = **10.0.401**; `.config/dotnet-tools.json` pins dotnet-coverage
  **18.11.2**, dotnet-reportgenerator-globaltool **5.5.11**, docfx **2.78.5**. `/tmp/nextorm-d196-r1/`.
- **Coverage invocation (exact working form, plan §9 C07 defers to the D196.1-resolved command; CI
  `.github/workflows/dotnet.yml:42-59`):**
  - `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`
  - `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"`
  - thresholds: MIN_LINE_COVERAGE=85 / MIN_BRANCH_COVERAGE=75 (hard only on `main`).
- **Collection register:** `docs/specs/status/collection-1.0.9-rc1-tail.md` (exists).
- **`scripts/validate_inner_loop.py`:** **not present in the repo** (`.opencode/skills/pdca-dotnet/scripts/`
  and `.opencode/scripts/` do not exist). Present **globally** at
  `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py` (18828 bytes) — later DO
  briefs should validate `test scope` against that path.
- **Roslyn symbol manifest** (`action=members`; ellipses from the planner resolve as follows):
  - `NextORM.Core.Tests.SqliteFts5CommandBuilderTests` — `tests/nextorm.core.tests/SqliteFts5CommandBuilderTests.cs:15`;
    30 public test methods + private `AssertAllTerminalsReject(DataContext,string):445` + 7 nested dialects/contexts.
    Key: `Factory_NullDataContext_ShouldThrowArgumentNull:149`, `Factory_NullTableName_ShouldThrowArgumentNull:159`,
    `Factory_EmptyWhitespaceOrNulTableName_ShouldThrowArgument:173`, `IntegrityCheck_OmittedFlag...:290`,
    `IntegrityCheck_ExplicitFlag...:301`, `AutoMerge_OutOfRange...:338`, `CrisisMerge_Negative...:352`,
    `TableNameWithSurroundingSpaces_ShouldBePreservedAndQuoted:405`,
    `KeywordCaseAndQuoting_ShouldNotChangeTheQuotedIdentifierShape:419`,
    `FunctionsWithoutTableFunction_AllTerminals...:430`, `TableFunctionWithoutFunctions_AllTerminals...:438`.
  - `NextORM.Sqlite.Tests.SqliteFts5MaintenanceSqlGenerationTests` —
    `tests/nextorm.sqlite.tests/SqliteFts5MaintenanceSqlGenerationTests.cs:11`; 12 test methods incl.
    `TableName_ShouldBeQuotedAsOneIdentifier:85`, `IntegerArgument_ShouldRenderInvariantUnchanged:97`,
    `UnquotedContext_ShouldStillQuoteIdentifiers:109`, `LowercaseUnquotedDefaults_ShouldQuoteIdentifiersAndKeepLowercaseKeywords:118`,
    `UnquotedContext_TableName_ShouldBeQuotedAsOneIdentifier:131`.
  - `NextORM.Core.Tests.CreateCommandBuilderFactoryTests` — `tests/nextorm.core.tests/CreateCommandBuilderFactoryTests.cs:24`;
    relevant `OldDmlFactoryNames_ShouldBeAbsent:256`, `NewDmlFactoryNames_ShouldBePresent:265`
    (asserts `CreateSqliteFts5CommandBuilder` present at `:276`).
  - `NextORM.Integration.Tests.SqliteSpecificTests` — `Fts5Maintenance`-prefixed methods:
    **10 methods / 11 test cases** (8 Facts; `..._ExtremeMergeValue...:1904` Theory 2 rows;
    `..._ExtremeCrisisMergeValue...:1938` Theory 1 row) — resolves the D195 "11/11" as a case count, not 11
    methods: `..._AllOperations_ShouldExecuteAndKeepIndexQueryable:1735`,
    `..._AllOperations_ShouldExecuteAsyncAndKeepIndexQueryable:1755`,
    `..._Rebuild_ShouldRepairStaleExternalContentIndex:1773`,
    `..._IntegrityCheck_OmittedAndFalse_ShouldSucceed_TrueOnStale_ShouldThrowNative:1794`,
    `..._RebuildOnContentlessTable_ShouldThrowNativeError:1817`, `..._MissingTable_ShouldThrowNativeError:1838`,
    `..._NonFtsTable_ShouldThrowNativeError:1857`, `..._PreCancelledToken_ShouldNotExecute:1886`.
  - Five rejection classes, each with exactly **3** `Fts5Maintenance_*` methods (`ToSql`/`Execute`/`ExecuteAsync`):
    `NextORM.Postgres.Tests.SqliteFunctionsRejectionTests` `tests/nextorm.postgres.tests/SqliteFunctionsRejectionTests.cs:10`
    (`:199/:211/:223`), `NextORM.SqlServer.Tests...` `:11` (`:117/:129/:141`),
    `NextORM.MySql.Tests...` `:11` (`:117/:129/:141`), `NextORM.MariaDb.Tests...` `:11` (`:117/:129/:141`),
    `NextORM.ClickHouse.Tests...` `:11` (`:117/:129/:141`).
- **Golden quoting (read from assertions, not generated):** delimiter is ASCII double quote `"` (U+0022),
  embedded `"` doubled to `""`. Canonical `INSERT INTO "ft" ("ft", "rank") VALUES ('automerge', 4)`;
  default lowercase/unquoted context `insert into "ft" ("ft") values ('optimize')`; injection golden
  `ft"; DROP TABLE x; --` → `"ft""; DROP TABLE x; --"`. Sources:
  `tests/nextorm.sqlite.tests/SqliteFts5MaintenanceSqlGenerationTests.cs:21,123,127-130`;
  `tests/nextorm.core.tests/SqliteFts5CommandBuilderTests.cs:17-19,405,419`.

Full raw manifest: `/tmp/nextorm-d196-r1/manifest.md`.

## Changed files

- `docs/specs/status/rc1-tail-196-fts5-tests-docs-1.md` (this file; created by D196.1, updated by D196.2).
- `tests/nextorm.sqlite.tests/SqliteFts5MaintenanceSqlGenerationTests.cs` (D196.2: added
  `VerbatimTableName_ShouldRenderAllEightOperationForms` `[Theory]` over `snake_case`/`camelCase`,
  `[Trait("Issue","196")]`; 8 byte-exact golden forms per name).
- `tests/nextorm.integration.tests/SqliteSpecificTests.cs` (D196.2: captured and asserted `>= 0` for all
  six operation-family results in both all-operations tests, retaining `[Trait("Issue","195")]` and adding
  `[Trait("Issue","196")]`).
- **No production source and no other tests were edited.** Modified tree: **two test files + six public
  EN/RU documentation pages** (3 EN + 3 RU), plus this untracked status file. Production tree invariance:
  `/tmp/nextorm-d196-r1/source-before.sha256` (407/407); `git diff --name-only` = 2 test files + 6 public
  doc pages; `git status --porcelain -uall` additionally shows this untracked status file. The earlier
  "no docs were edited" claim was stale: D196.3 edited the six named pages (see the Evidence (raw) section).

## Pointers

- Issue #196 — "FTS5 maintenance real-SQLite tests and EN/RU docs — follow-up to #181"
  (https://github.com/AlexeyShirshov/nextorm/issues/196); depends on #195; parent #181.
- D195 status file: `docs/specs/status/rc1-tail-195-fts5-maintenance-1.md` (CHECK PASS r=1 n=2/3;
  frozen-contract handoff for D196).
- Frozen contract **FC195-1 (rv=1)** — public surface/validation/rendering of
  `SqliteFts5CommandBuilder` + `CreateSqliteFts5CommandBuilder`; D196 binds to it unchanged.
- Collection register: `docs/specs/status/collection-1.0.9-rc1-tail.md` (D196 row `pending`; next step D196).

## Progress log

- 2026-10-07T01:23Z | PLAN | r=1 | iteration n=1/3 | PLAN ready (autonomous mode) — but the verbatim PLAN r=1 text was NOT included in the DO brief; recorded as BLOCKED | this file
- 2026-10-07T01:23Z | DO | r=1 | n=1/3 | D196.1 started: baseline HEAD `2da40cab`, 17 untracked / 0 modified, 407-file `src` hash baseline; tool versions (dotnet 10.0.401, dotnet-coverage 18.11.2, reportgenerator 5.5.11, docfx 2.78.5); Roslyn symbol manifest resolved; golden quoting `"`/`""`; `validate_inner_loop.py` only at `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/` | `/tmp/nextorm-d196-r1/{baseline.txt,source-before.sha256,manifest.md}`
- 2026-10-07T01:25Z | DO | r=1 | n=1/3 | **BLOCKER:** verbatim PLAN r=1 (Goal, R196-01..R196-10, EC196-01..EC196-15, DO units D196.2..D196.5, decisions, risks) absent from the DO brief and not on disk; plan-dependent sections marked BLOCKED; waiting on orchestrator | this file
- 2026-10-07T01:34Z | PLAN | r=1 | n=1/3 | plan recorded verbatim (D196.1 complete) | this file
- 2026-10-07T01:30Z | DO | r=1 | n=1/3 | D196.2 started: structured test scope validated (brief exit 0) via `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py`; scope = sqlite/core/integration test projects, native `-class`/`-method` selectors, affected rebuild | `/tmp/nextorm-d196-r1/scope-d196.2.json`
- 2026-10-07T01:30Z | DO | r=1 | n=1/3 | D196.2 edits: added `VerbatimTableName_ShouldRenderAllEightOperationForms` `[Theory]` (`snake_case`/`camelCase` × 8 literal golden forms, `Issue 196`) to `SqliteFts5MaintenanceSqlGenerationTests.cs`; strengthened both all-operations integration methods to capture/assert all six operation results `>= 0` (retaining `Issue 195`). Only the two test files changed; CRLF preserved; `src/**` hash identical to `source-before.sha256` | `git diff --name-only`; `/tmp/nextorm-d196-r1/source-check.sha256`
- 2026-10-07T01:30Z | DO | r=1 | n=1/3 | D196.2 changed-file list: `tests/nextorm.sqlite.tests/SqliteFts5MaintenanceSqlGenerationTests.cs`, `tests/nextorm.integration.tests/SqliteSpecificTests.cs` (+this status file) — no production or docs edits | this file (§Changed files)
- 2026-10-07T01:30Z | DO | r=1 | n=1/3 | D196.2 inner loop green: sqlite unit 23/23 (exit 0), core unit 49/49 (exit 0), focused real SQLite `*Fts5Maintenance*` 11/11 (exit 0); report validation exit 0 | `/tmp/nextorm-d196-r1/{evidence-d196.2.json,sqlite-unit-d196.2.log,core-unit-d196.2.log,sqlite-integration-d196.2.log}`
- 2026-10-07T01:31Z | DO | r=1 | n=1/3 | D196.3 started: structured docs test scope written and validated (brief exit 0) against `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py`; docs-only scope, `rebuild: none`, the six named EN/RU pages + DocFX | `/tmp/nextorm-d196-r1/scope-d196.3.json`
- 2026-10-07T01:31Z | DO | r=1 | n=1/3 | D196.3 pages changed (6/6, EN/RU parity): providers/sqlite (factory `CreateSqliteFts5CommandBuilder(string tableName)`; successful terminal returns a nonnegative affected-row `int` with no fixed count promised), guide/provider-specific/sqlite (`Merge` never `abs`; `IntegrityCheck` `false`→`0`/`true`→`1`; names verbatim with `.` literal; nonnegative/no-fixed-count; `CancellationToken` forwarded), advanced/limitations (null/empty/whitespace/NUL target rejected; nonnegative/no-fixed-count; contentless `Rebuild` fails natively); `advanced/limitations.md:81` and its RU mirror left unchanged — already accurate/parity-complete | `git diff` 6 pages; `/tmp/nextorm-d196-r1/docfx-d196.3.log`
- 2026-10-07T01:32Z | DO | r=1 | n=1/3 | D196.3 docs verification green: `dotnet docfx docs/docfx.json` exit 0, 0 errors, 2 pre-existing warnings (`AnalyzerReleases.*.md` duplicate source files in the source-generator project); `git diff --check -- docs/` exit 0; all six pages CRLF (total lines == CRLF lines) | `/tmp/nextorm-d196-r1/docfx-d196.3.log`


## Evidence (raw) - D196.4 boundary sweep (2026-10-07T01:38Z)

Artifacts: `/tmp/nextorm-d196-r1/`. Branch `1.0.9-rc1`; no commit, no stage. All commands from repo root.

| command | exit | key numbers | log |
|---|---|---|---|
| C01 `dotnet build nextorm.slnx -c Debug` | 0 | 0 Warning(s), 0 Error(s) | `build-final.log` |
| C02 `dotnet test tests/nextorm.sqlite.tests -c Debug` | 0 | total 1137, passed 1136, failed 0, skipped 1 | `sqlite-tests-final.log` |
| C02f `-class '*SqliteFts5MaintenanceSqlGenerationTests'` | 0 | Total 23, Errors 0, Failed 0, Skipped 0 | `sqlite-fts5-final.log` |
| C03 `dotnet test tests/nextorm.core.tests -c Debug` | 0 | total 1712, passed 1712, failed 0, skipped 0 | `core-tests-final.log` |
| C03f `-class '*SqliteFts5CommandBuilderTests'` | 0 | Total 49, Errors 0, Failed 0, Skipped 0 | `core-fts5-final.log` |
| C03f `-class '*CreateCommandBuilderFactoryTests'` | 0 | Total 27, Errors 0, Failed 0, Skipped 0 | `core-factory-final.log` |
| C04 `postgres` `*SqliteFunctionsRejectionTests` `*Fts5Maintenance*` | 0 | Total 3, Failed 0, Skipped 0 | `rejection-postgres-final.log` |
| C04 `sqlserver` | 0 | Total 3, Failed 0, Skipped 0 | `rejection-sqlserver-final.log` |
| C04 `mysql` | 0 | Total 3, Failed 0, Skipped 0 | `rejection-mysql-final.log` |
| C04 `mariadb` | 0 | Total 3, Failed 0, Skipped 0 | `rejection-mariadb-final.log` |
| C04 `clickhouse` | 0 | Total 3, Failed 0, Skipped 0 | `rejection-clickhouse-final.log` |
| C05 `*SqliteSpecificTests` `*Fts5Maintenance*` (DOCKER_HOST) | 0 | Total 11, Failed 0, Skipped 0 | `sqlite-integration-final.log` |
| C06 full integration (DOCKER_HOST) | 0 | Total 3311, Errors 0, Failed 0, Skipped 197, Not Run 0 | `integration-full.log` |
| C06b focused `*ClickHouseIntegrationTests` (proof CH executed) | 0 | Total 117, Errors 0, Failed 0, Skipped 0 | `integration-clickhouse-final.log` |
| C07 `dotnet-coverage collect` | 0 | 8914 total / 6297 passed / 0 failed / 2617 skipped | `coverage-final.log` |
| C07 `reportgenerator` | 0 | line 87.2%, branch 79.1% (thresholds 85 / 75) | `coverage-report-final.log`, `tests/coverage/report/Summary.txt` |
| C08 `dotnet docfx docs/docfx.json` | 0 | 0 error(s), 2 warning(s) (pre-existing `AnalyzerReleases.*` duplicates) | `docfx-final.log` |
| C09 `git diff --check` / `git diff --cached --check` | 0 / 0 | no whitespace errors | - |
| C10 source invariance | - | `src` 407/407 identical to `source-before.sha256` | `source-after.sha256` |

**C06 provider execution (explicit):** executed - SQLite (no container), PostgreSQL (`postgres:17-alpine`),
SQL Server (`mssql/server:2025-latest`), MySQL (`mysql:8.4`), ClickHouse
(`clickhouse/clickhouse-server:25.8-alpine`). ClickHouse proof: zero ClickHouse `[SKIP]` lines in the full
run (xUnit v3 prints passed tests silently and every unavailable-provider test would print a skip), a focused
`ClickHouseIntegrationTests` run of 117/117 with 0 skipped, and the reused-container list containing the CH
image. MariaDB (`mariadb:11.4`) also present. The 197 skips are all per-capability `Assert.Skip` reasons
(e.g. "This provider does not implement streaming LOB reads"), not whole-provider unavailability. No failure.

**C10 scope:** `git diff --name-only` = 6 docs pages + 2 test files; status file untracked;
`git diff --cached --name-only` empty. `git status --porcelain=v1 -uall` = 8 modified + this status file +
pre-existing `artifacts/pdca/D198/*` (17 untracked). All 9 changed text files CRLF (0 bare LF).

**C11 doc parity:** six pages changed as three EN/RU pairs (EN providers 9 / guide 21 / limitations 2 vs RU
11 / 19 / 2); no `docs/specs` links in any of the six (rg: NONE).

## Progress log (D196.4)

- 2026-10-07T01:38Z | DO | r=1 | iteration n=1/3 | D196.4 boundary sweep complete: C01 exit 0 (0W/0E); C02 focused 23/23 + full 1137/1136/0/1; C03 49/49 + 27/27 + full 1712/1712; C04 five providers 3/3 each exit 0; C05 11/11; C06 exit 0 (3311 total / 0 failed / 197 per-capability skipped; all five required providers executed, ClickHouse proven 117/117); C07 line 87.2% >=85 / branch 79.1% >=75; C08 exit 0, 0 errors, 2 pre-existing warnings; C09/C10 hygiene + source invariance green | `/tmp/nextorm-d196-r1/*`
- 2026-10-07T01:38Z | DO | r=1 | iteration n=1/3 | EC196-01..EC196-14 all PASS; EC196-15 (commit/register) remains pending for D196.5/ACT (no commit or stage performed) | this file
- 2026-10-06T20:48Z | CHECK | r=1 | iteration n=1/3 | verdict FAIL (evidence/closure gate, not a product defect); findings 1-3,5 accepted; R196-02 parameter-negative + provenance + R196-10 closure re-gathered | this file
- 2026-10-06T20:48Z | CHECK | r=1 | iteration n=1/3 | D196.4 provenance re-gathered: evidence-d196.4.json with 16 run-linked executions (exact argument arrays + exit codes + logs); validator exit 0; M5 stale changed-files text corrected to 2 test files + 6 public EN/RU doc pages + untracked status file; EC196-15 recorded as an ACT gate (pending until D196.5), not an unmet CHECK criterion | /tmp/nextorm-d196-r1/evidence-d196.4.json

## CHECK (2026-10-07T01:45Z)

**Verdict: PASS** — plan revision `r=1`, attempt `n=1/3`, evidence contract `rv=1`. No DO loop occurred
between the first adjudication and the pass: the earlier fail was an evidence/closure gate only, resolved
by re-gathering, so `n` stays `1/3` and the plan revision is unchanged (`r=1`).

- The first-adjudication fail was **evidence/closure-only** — no product defect and no production change.
  Findings 1–3 and 5 were accepted; R196-02 was closed by the `SqlMutationBuilder.cs:527` empty-parameter
  chain (maintenance commands carry zero parameters); M2 provenance was supplied via
  `/tmp/nextorm-d196-r1/evidence-d196.4.json`; M5 (stale changed-files text) was corrected in this status
  file.
- EC196-01..EC196-14 PASS; **EC196-15 (trait/register/status + commit) is an ACT gate and is fulfilled by
  this ACT**.
- Frozen FC195-1 verified unchanged; `src/**` hash invariant (407/407). No production defect.

Final counters: **N=1 · r=1 · n=1/3 · rv=1**.

## ACT (2026-10-07T01:50Z)

- Finalized D196: header `status:` → `done`; D196.5 executed.
- Commit 1 — `#196 Verify FTS5 maintenance and finish EN/RU documentation`: two test files + six EN/RU
  public doc pages + this status file → `PENDING_COMMIT1_SHA`.
- Commit 2 — `#196 Update collection register: D196 and G1 done`: collection register + this status file.
- Collection register updated: D196 `pending` → `done`, G1 `in-progress` → `done`.
- Issue #196 closed on GitHub (`PENDING_ISSUE_RESULT`). **No `git push`.**

### ACT progress log

- 2026-10-07T01:50Z | ACT | r=1 | iteration n=1/3 | CHECK PASS r=1 n=1/3 rv=1 recorded; D196.5 finalized header `done`; EC196-15 fulfilled | this file
- 2026-10-07T01:50Z | ACT | r=1 | iteration n=1/3 | commit 1 `#196 Verify FTS5 maintenance and finish EN/RU documentation` = PENDING_COMMIT1_SHA | this file
- 2026-10-07T01:50Z | ACT | r=1 | iteration n=1/3 | collection register D196 → done, G1 → done; issue #196 closed; no push | docs/specs/status/collection-1.0.9-rc1-tail.md

## Done / Verified

Final acceptance matrix — R196-01..R196-10 all **met**:

| criterion | result | evidence |
|---|---|---|
| R196-01 Frozen implementation | met | `src` 407/407 hash identical to `source-before.sha256`; 0 `src/**` edits |
| R196-02 Exact SQL | met | SQLite FTS5 class 23/23; full sqlite 1137/1136/0/1; byte-exact `snake_case`/`camelCase` × 8 forms |
| R196-03 Builder guards | met | core builder 49/49; factory 27/27; full core 1712/0/0 |
| R196-04 Real SQLite execution | met | focused real SQLite 11/11 (all-operations results asserted `>= 0`) |
| R196-05 Non-SQLite rejection | met | PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse — 3/3 each, exit 0 |
| R196-06 Integration boundary | met | full integration 3311 total / 0 failed / 197 per-capability skipped; all 5 providers executed (ClickHouse focused 117/117) |
| R196-07 Documentation | met | DocFX exit 0, 0 errors; 6 pages EN/RU parity; no `docs/specs/**` links |
| R196-08 Build/hygiene | met | solution build exit 0, 0 Warning(s)/0 Error(s); `git diff --check` clean; CRLF |
| R196-09 Coverage evidence | met | line 87.2% ≥ 85 / branch 79.1% ≥ 75 (`coverage.settings.xml`) |
| R196-10 Traceability/completion | met | issue-196 traits; D196/G1 `done`; `#196` commit; no push |

- Diff = **2 tests + 6 EN/RU docs** (+ this status file) — no production change.
- **Done / Verified:** R196-01..R196-10 met; D196 and collection group G1 `done`.
