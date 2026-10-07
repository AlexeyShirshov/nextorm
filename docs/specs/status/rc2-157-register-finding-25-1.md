# PDCA 02 — Reconcile register finding 25

- collection: `1.0.9-rc2`
- task: `D157` / issue `#157` (https://github.com/AlexeyShirshov/nextorm/issues/157)
- cycle: `N=1, revision r=1, attempt n=1`
- selected variant: `pdca-dotnet`
- mode: collection child (plan-only; DO executed later by a separate orchestrator)
- plan_state: ready

## Goal

Close finding 25's outdated claim that `WITH TIES` combined with `DISTINCT`/`DISTINCT ON` reaches the database unchecked. Prove the existing SQL-generation guard with regression tests, document its timing, and preserve the historical finding/provenance. No production executable change.

## Scope

**In scope**
- Regression SQL-generation tests for PostgreSQL and SQL Server.
- Documentation-only amendment to `WithTies()`'s XML `<summary>`.
- Timing clarification in the EN/RU sorting-and-paging guides.
- Finding 25 only in `docs/specs/design/code-smells-review.md`.
- The child-cycle status file (this file).

**Out of scope**
- Executable production changes, public-signature changes, an early fluent guard, SQL algorithms, provider extensions, unrelated limitations.
- `docs/specs/design/API-NAMING-REVIEW.md` (same-named finding 25 there is unrelated), the collection status, neighboring findings, the section summary table.
- Database-backed integration tests, commits, pushes, merges.

## Established facts (scout, file:line)

- Guard already exists: `src/nextorm.core/DataContext/SqlBuilder.cs:81-82` — `if (cmd.Paging.HasWithTies && (cmd.IsDistinct || cmd.DistinctOn is not null)) throw new BuildSqlCommandException("WITH TIES cannot be combined with DISTINCT or DISTINCT ON.");` Covers both DISTINCT and DISTINCT ON; trigger point is SQL generation, not the fluent call.
- Neighbors: `:75-76` dialect `NotSupportedException`; `:78-79` `BuildSqlCommandException("WITH TIES requires a positive page limit.")`.
- Exception type: `src/nextorm.core/BuildSqlCommandException.cs:12` (public : DataContextException).
- Public API: `EntityBuilder<TEntity>.WithTies()` `src/nextorm.core/Builders/EntityBuilder.cs:2167` (summary `:2162-2166`; does not mention DISTINCT); `Distinct()` `:1799`; `DistinctOn<TResult>(...)` `:1816`.
- Finding 25: `docs/specs/design/code-smells-review.md:1851-1863` (section head `:1790`; severity 🟡, status ОТКРЫТА; not in "не рекомендуется"/"Исключения"). Its cited ranges `EntityBuilder.cs:477-504`, `:570-576`, `SqlBuilder.cs:66-70` are STALE.
- Existing WITH TIES tests: PostgreSQL `tests/nextorm.postgres.tests/SqlGenerationTests.cs:3812,3822,3832` (ns `NextORM.Postgres.Tests`, `SqlOf` `:25`); SQL Server `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:2642,2652,2662` (ns `NextORM.SqlServer.Tests`, `SqlOf` `:23`). No test asserts DISTINCT+WITH TIES rejection; call-order coverage is absent.
- Docs EN/RU: `docs/guide/04-sorting-and-paging.md:246-247` and `docs/ru/guide/04-sorting-and-paging.md:246-247` already state combining with `DISTINCT`/`DISTINCT ON` is rejected; timing/exception not stated. No public doc links `docs/specs`.
- Provenance: `docs/specs/status/native-extreme-row-144-1.md:303` (debt 6; original issue cited `:302`); mapping `:314` → #157.

## Minimal solution

Tests and documentation around the EXISTING guard (no runtime change). Rejected alternatives: adding a guard to `WithTies()` (changes timing; explicitly prohibited); correcting the register without tests (no regression evidence).

## Acceptance criteria

Names below are PLANNED test symbols.

| ID | Observable acceptance, incl. negative case | Verification |
|---|---|---|
| AC1 | PostgreSQL rejects `DISTINCT` + `WITH TIES` in both modifier orders. Query construction succeeds; `SqlOf` throws `BuildSqlCommandException` with exactly `WITH TIES cannot be combined with DISTINCT or DISTINCT ON.` An early fluent exception or a different failure is NOT acceptable. | New PG `WithTies_WithDistinct_InBothCallOrders_ShouldRejectCombination` theory (2 cases) |
| AC2 | SQL Server satisfies the same requirement. | New SQL Server theory, same method name (2 cases) |
| AC3 | PostgreSQL rejects `DISTINCT ON` + `WITH TIES` in both modifier orders, same timing/type/message. | New PG `WithTies_WithDistinctOn_InBothCallOrders_ShouldRejectCombination` theory (2 cases) |
| AC4 | Valid, ordered, positive-limit `WITH TIES` without either modifier still generates the expected provider SQL. Blanket rejection fails acceptance. | Existing PG `WithTies_ShouldUseFetchFirstWithTies`; SQL Server `WithTies_ShouldUseTopWithTies`; offset cases |
| AC5 | Finding 25 explicitly closes the obsolete "no guard/server receives invalid SQL" claim using current source/test references + verification evidence; historical assertions kept as history. | Register diff + refreshed references |
| AC6 | XML and EN/RU guides state rejection occurs during SQL generation, not at the `WithTies()` fluent call. No public link to internal specs. | Docs review, build, public-page link check |
| AC7 | No executable production changes, signature changes, unrelated register changes, or collection-status changes. | Allowlist + diff review |
| AC8 | Debug build and both affected provider test projects pass; required tests are discovered and executed, not skipped. | Recorded commands, exit codes, logs |

Rejection fixtures: reuse established provider fixtures, positive limit (e.g. `2`), valid `ORDER BY` (for PG DISTINCT ON, an order compatible with the selected key). Do not invent entity members/fixture APIs.

## Variant matrix — execution-path coverage

| Modifier state | Call order | Dialect | Limit/order | Closure |
|---|---|---|---|---|
| `IsDistinct=true`, `DistinctOn=null`, WITH TIES | Distinct → WithTies | PostgreSQL | Positive / valid | New test; existing guard `SqlBuilder.cs:81-82` |
| Same | WithTies → Distinct | PostgreSQL | Positive / valid | New test; same guard |
| Same | Distinct → WithTies | SQL Server | Positive / valid | New test; same guard |
| Same | WithTies → Distinct | SQL Server | Positive / valid | New test; same guard |
| `IsDistinct=false`, `DistinctOn!=null`, WITH TIES | DistinctOn → WithTies | PostgreSQL | Positive / compatible order | New test; same guard |
| Same | WithTies → DistinctOn | PostgreSQL | Positive / compatible order | New test; same guard |
| Neither modifier, WITH TIES | Existing fluent sequence | PG / SQL Server | Positive / valid, incl. offset | Existing tests (AC4); non-rejection branch |
| WITH TIES, no positive limit | Existing sequence | PG / SQL Server | Missing/nonpositive | Existing guard `:78-79`; existing `WithTies_WithoutLimit_ShouldThrow` |
| WITH TIES, unsupported dialect | Either | SupportsWithTies=false | Otherwise valid | Existing guard `:75-76` |
| DISTINCT + DISTINCT ON | Either order | Existing fluent API | Independent of WITH TIES | Existing guards `EntityBuilder.cs:1801-1802,1820-1821`; outside this finding |
| `HasWithTies=false` | Either | Relevant dialects | Existing valid fixtures | Existing guard short-circuit `:81` |
| WITH TIES with missing/incompatible ORDER BY | Either | PG / SQL Server | Invalid ordering | Deferred — separate task requests ordering-validation semantics |
| DISTINCT ON + WITH TIES | Either | SQL Server / non-PG | Any | Deferred — provider support explicitly requested |
| Any combination | Either | In-memory | Any | Deferred — separate task defines in-memory WITH TIES semantics |
| Null selector / default entity values / value-vs-reference entity | Either | Any | Any | Deferred — guard operates on command flags, not row values |

## Ordered DO tasks

- **D1** Write this status file; record baseline commands/results before changing tests/docs.
- **D2** PostgreSQL regression tests: `tests/nextorm.postgres.tests/SqlGenerationTests.cs` (WITH TIES area ~`:3812-3832`). Add AC1/AC3 theories. Construct the query outside `Assert.Throws`; invoke only SQL generation inside it; assert exception type + exact message.
- **D3** SQL Server regression tests: `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs` (~`:2642-2662`). Add AC2 theory, same assertion discipline.
- **D4** Public documentation: XML `<summary>` only in `src/nextorm.core/EntityBuilder.cs` (`:2162-2166`); `docs/guide/04-sorting-and-paging.md:246-247`; `docs/ru/guide/04-sorting-and-paging.md:246-247`. Preserve existing limit/dialect statements; add the timing statement.
- **D5** Verify + obtain fresh references: run required commands; inspect discovery/results; obtain new test locations via Roslyn; confirm guard/signatures unchanged; preserve CRLF.
- **D6** After D5 passes: reconcile finding 25 in `docs/specs/design/code-smells-review.md:1851-1863`; update this status file with evidence + final references; rerun doc/scope checks.

No runtime implementation task is authorized.

## Footprint (content-write allowlist)

1. `docs/specs/status/rc2-157-register-finding-25-1.md`
2. `tests/nextorm.postgres.tests/SqlGenerationTests.cs`
3. `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`
4. `src/nextorm.core/EntityBuilder.cs` — XML documentation trivia only
5. `docs/guide/04-sorting-and-paging.md`
6. `docs/ru/guide/04-sorting-and-paging.md`
7. `docs/specs/design/code-smells-review.md` — finding 25 only

Evidence logs under `/tmp/nextorm-D157-r1/` (not source edits). Build outputs are generated artifacts.

**Uncertainty:** insertion locations and resulting test/doc line numbers; no uncertainty authorizes another source file.

**Unit execution mode:** one unit, sequential, one tree. No worktree. Tests, XML and register evidence share acceptance obligations.

## Docs and register wording

XML/EN claim: "WITH TIES cannot be combined with DISTINCT or DISTINCT ON. This combination is rejected during SQL generation with BuildSqlCommandException; validation is not performed by the WithTies fluent call itself."

RU mirror: "WITH TIES нельзя сочетать с DISTINCT или DISTINCT ON. Такое сочетание отклоняется при генерации SQL с BuildSqlCommandException; проверка не выполняется непосредственно при fluent-вызове WithTies."

Do not amend `Distinct()`/`DistinctOn()` summaries, API-reference pages, README, or other guides.

Finding 25 update:
- Mark the obsolete no-guard/server-arrival assertion CLOSED.
- Identify the old assertion and proposed fluent guard / `InvalidOperationException` expectation as historical.
- State the existing SQL-generation guard is accepted behavior; absence of an early fluent guard is NOT separate debt.
- Cite unchanged `SqlBuilder.cs:81-82`, exception declaration `BuildSqlCommandException.cs:12`, refreshed locations of the three new test methods.
- Cite executed evidence: command identifiers, log paths, six rejection cases + valid positive cases.
- Preserve provenance: debt 6 of #144, `native-extreme-row-144-1.md:303` (original issue cited `:302`), mapping `:314` → #157.
- Keep old source ranges only if clearly labeled historical.

## Test strategy

SQL-generation unit tests only; both projects need no database. No container run applicable.

Commands (argument arrays; capture stdout/stderr + actual exit codes):

| ID | Invocation |
|---|---|
| C1 | `["dotnet","build","-c","Debug"]` |
| C2 | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug","--filter","FullyQualifiedName~NextORM.Postgres.Tests.SqlGenerationTests.WithTies"]` |
| C3 | `["dotnet","test","tests/nextorm.sqlserver.tests","-c","Debug","--filter","FullyQualifiedName~NextORM.SqlServer.Tests.SqlGenerationTests.WithTies"]` |
| C4 | `["dotnet","test","tests/nextorm.postgres.tests","-c","Debug"]` |
| C5 | `["dotnet","test","tests/nextorm.sqlserver.tests","-c","Debug"]` |
| C6 | `["git","diff","--check"]` |
| C7 | `["git","diff","--name-only"]` |
| C8 | `["git","diff","--unified=5","--","src/nextorm.core/EntityBuilder.cs","tests/nextorm.postgres.tests/SqlGenerationTests.cs","tests/nextorm.sqlserver.tests/SqlGenerationTests.cs","docs/guide/04-sorting-and-paging.md","docs/ru/guide/04-sorting-and-paging.md","docs/specs/design/code-smells-review.md"]` |
| C9 | `["git","grep","-n","-F","specs/","--","docs/guide/04-sorting-and-paging.md","docs/ru/guide/04-sorting-and-paging.md"]` |
| C10 | `["git","status","--short","--untracked-files=all"]` |

Fresh test references: Roslyn `action=members`, symbols `NextORM.Postgres.Tests.SqlGenerationTests` and `NextORM.SqlServer.Tests.SqlGenerationTests`, after implementation.

Boundary sweep: C4/C5 run both complete affected projects, incl. existing positive/offset/missing-limit cases.

Branch/mutation note: no mutation run authorized. Expected sensitivity: deleting the combination guard, dropping a modifier arm, changing its exception/message, or introducing fluent-time rejection makes required cases fail.

Coverage: project thresholds 85% line / 75% branch; hard-fail on `main`, warning on this branch. No production executable lines change; no numeric coverage gate added. `scripts/validate_inner_loop.py` does NOT exist in this repo — do not invent it.

Priority: P1 by construction for AC1-AC8 + evidence rows; P2 baseline comparison. Do not borrow the unrelated API-naming finding's P2 label.

## Performance and reconnaissance

- Performance measurement: NOT needed. `SqlBuilder.cs:81-82` unchanged; `EntityBuilder.cs:2162-2166` documentation-only; otherwise tests/Markdown. No query-path, per-row or one-time executable work changes.
- Design spike: NOT needed. Agreed design + verified guard settle behavior.

## Predecessor-result requirements

No uncompleted implementation predecessor is required; the existing guard is established evidence. #144 supplies provenance only, not a dependency to reopen. Only D157 edits `code-smells-review.md`; tasks editing `API-NAMING-REVIEW.md` do not overlap. Serialize with any concurrent task touching the same provider test files, `EntityBuilder.cs`, or the two guide pages.

## Assumptions / prerequisites / blockers

- New fixture member names / final lines: reuse existing positive fixtures; collect Roslyn references during DO.
- Issue URL (#157) recorded; do not fabricate.
- Treat the XML amendment as documentation trivia only; executable text and signatures unchanged.
- Record baseline; do not absorb unrelated workspace changes or repair unrelated failures.
- No implementation blocker demonstrated.

## Evidence contract — rv=1

All execution evidence below is PLANNED. Stable row IDs.

| Row / req | Scenario and expected evidence | Invocation and required result | Artifacts | Owner | Applicability/priority |
|---|---|---|---|---|---|
| E01 / AC7 | Scope/signatures: only allowlisted changes; XML-only production edit; register restricted to finding 25 | C6 exit 0; C7/C8/C10 exit 0 + review PASS; separate preexisting changes | scope-check.log, changed-files.log, task.diff, workspace-status.log, scope-review record | coder; CHECK validates | Always / P1 |
| E02 / AC1 | PG DISTINCT: two cases discovered/executed; construction succeeds; exact exception at `SqlOf` | C2 exit 0; both cases identified, neither skipped; assertion review PASS | postgres-withties.log, case summary | coder; CHECK validates | Always / P1 |
| E03 / AC2 | SQL Server DISTINCT: same two-case obligations | C3 exit 0; both cases executed, not skipped | sqlserver-withties.log, case summary | coder; CHECK validates | Always / P1 |
| E04 / AC3 | PG DISTINCT ON: both modifier orders, valid fixture prerequisites, exact rejection | C2 exit 0; both DISTINCT ON cases executed; prerequisite/assertion review PASS | postgres-withties.log, fixture review | coder; CHECK validates | Always / P1 |
| E05 / AC4 | Existing valid plain/offset WITH TIES still succeed on both providers | C2/C3 exit 0; named existing positive methods executed, not skipped | both focused logs, positive-case summary | coder; CHECK validates | Always / P1 |
| E06 / AC5 | Register closure, provenance, real source/test locations + evidence refs | C8 exit 0; two Roslyn `members` calls return new methods/locations; register review PASS | task.diff, Roslyn captures, register-review record | coder; CHECK validates | Always / P1 |
| E07 / AC6 | XML + EN/RU timing statement; no internal-spec link | C8 review PASS; C9 exit 1 with no matches (exit 2+ = error); C1 build exit 0 | public-doc-link-check.log, docs-review record, build log | coder; CHECK validates | Always / P1 |
| E08 / AC8 | Build + affected-project boundary sweep | C1/C4/C5 exit 0; logs present; required tests execute without skips; no warning-as-error | build.log, postgres-project.log, sqlserver-project.log | coder; CHECK validates | Always / P1 |
| E09 / baseline | Pre-edit build + existing focused-suite behavior | Before D2-D4: C1/C2/C3 exit 0, or record observed failure for classification | baseline-*.log | coder | Once before implementation / P2 |

All artifacts under `/tmp/nextorm-D157-r1/`. The status file records exact paths, command arrays and observed exit codes. No nonexistent `validate_inner_loop.py` is used.

CHECK re-gather budget: one targeted batch per CHECK, owned by `check`, at most three deficient rows; may rerun specified commands but cannot alter scope/acceptance. Missing evidence alone does not revise the contract or reset attempts.

Revision rule: initial `rv=1`, plan `r=1`, attempt `n=1`; no supersession. A real plan change must explicitly supersede and preserve row/requirement IDs.

## DO evidence — r=1, n=1 (collected 2026-10-07)

Footprint correction applied: the XML `<summary>` lives in `src/nextorm.core/Builders/EntityBuilder.cs`
(`WithTies()` `:2167`, summary now `:2162-2171`); `src/nextorm.core/EntityBuilder.cs` does not exist.

Fresh references (Roslyn `action=members`, after implementation):
- `NextORM.Postgres.Tests.SqlGenerationTests.WithTies_WithDistinct_InBothCallOrders_ShouldRejectCombination(bool)` — `tests/nextorm.postgres.tests/SqlGenerationTests.cs:3845`
- `NextORM.Postgres.Tests.SqlGenerationTests.WithTies_WithDistinctOn_InBothCallOrders_ShouldRejectCombination(bool)` — `tests/nextorm.postgres.tests/SqlGenerationTests.cs:3863`
- `NextORM.SqlServer.Tests.SqlGenerationTests.WithTies_WithDistinct_InBothCallOrders_ShouldRejectCombination(bool)` — `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:2675`
- Guard unchanged `src/nextorm.core/DataContext/SqlBuilder.cs:81-82`; exception `src/nextorm.core/BuildSqlCommandException.cs:12`; `WithTies()` signature unchanged.

| Cmd | Argument array | Exit | Result |
|---|---|---|---|
| C1 | `dotnet build -c Debug` | 0 | 0 Warning(s) 0 Error(s) — `/tmp/nextorm-D157-r1/build.log` |
| C2 | `dotnet test tests/nextorm.postgres.tests -c Debug --filter FullyQualifiedName~NextORM.Postgres.Tests.SqlGenerationTests.WithTies` | 0 | total 7 / passed 7 / failed 0 / skipped 0 — `/tmp/nextorm-D157-r1/postgres-withties.log` |
| C3 | `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter FullyQualifiedName~NextORM.SqlServer.Tests.SqlGenerationTests.WithTies` | 0 | total 5 / passed 5 / failed 0 / skipped 0 — `/tmp/nextorm-D157-r1/sqlserver-withties.log` |
| C4 | `dotnet test tests/nextorm.postgres.tests -c Debug` | 0 | total 791 / passed 791 / failed 0 / skipped 0 — `/tmp/nextorm-D157-r1/postgres-project.log` |
| C5 | `dotnet test tests/nextorm.sqlserver.tests -c Debug` | 0 | total 712 / passed 712 / failed 0 / skipped 0 — `/tmp/nextorm-D157-r1/sqlserver-project.log` |
| C6 | `git diff --check` | 0 | clean |
| C7 | `git diff --name-only` | 0 | allowlist only |
| C9 | `git grep -n -F specs/ -- docs/guide/04-sorting-and-paging.md docs/ru/guide/04-sorting-and-paging.md` | 1 | no matches (expected) |
| C10 | `git status --short --untracked-files=all` | 0 | D157 footprint + preexisting untracked files |

Per-case evidence: PG DISTINCT 2/2, PG DISTINCT ON 2/2, SQL Server DISTINCT 2/2 executed (exit 0 each;
`pg-distinct2.log`, `pg-distincton2.log`, `ss-distinct2.log`); existing valid/offset/missing-limit
`WITH TIES` cases remain green (AC4). Baseline E09: C1/C2/C3 exit 0 before edits (`baseline-*.log`).

Journal:
- `2026-10-07T15:24Z | DO | r=1 | n=1/3 | D2-D4 implemented (tests, XML, EN/RU docs); C1 build 0/0 | /tmp/nextorm-D157-r1/build.log`
- `2026-10-07T15:24Z | DO | r=1 | n=1/3 | D5 verify: C2=7/0/0, C3=5/0/0, C4=791/0/0, C5=712/0/0 | /tmp/nextorm-D157-r1/postgres-withties.log`
- `2026-10-07T15:24Z | DO | r=1 | n=1/3 | D6 finding 25 reconciled CLOSED; Roslyn locations captured | docs/specs/design/code-smells-review.md`
