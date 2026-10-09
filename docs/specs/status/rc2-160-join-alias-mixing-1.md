# Task T160 / #160 — Join aliases: free mixing positional/alias + root alias (.WithAlias)

- collection: `rc2-final`
- intent: own PLAN (COLLECTION TASK PLAN); phase P (no DO yet)
- selected_variant: `pdca-dotnet`
- cycle_id: N=1
- plan_revision: r=1
- baseline: `9a2a2871`; current-tree HEAD `6f01bc65`
- plan_state: ready
- status file: `docs/specs/status/rc2-160-join-alias-mixing-1.md`
- preserved prior work: branch `wip/d160-incomplete` (`214c1323` STOP doc, tip `40b1a159`); terminal r=4 STOP (R02 over-preservation) is historical input, not an accepted result.
- persisted: 2026-10-09

---

# T160 / #160 — gate-1 PLAN
- **State:** `plan_state=ready`; collection `rc2-final`; cycle `N=1`; initial sealed plan `r=1`, attempt `n=1`; contract `rv=1`.
- **P160.2:** reconcile the new-cycle contract with the eleven legacy acceptance obligations; replace legacy evidence-copying with fresh, source-identified evidence.
- This is not a reset of legacy CHECK attempts. Legacy implementation and evidence remain historical inputs, not accepted results.

## Goal and binding acceptance criteria
Support alias/positional JOIN composition across the complete root-source matrix without changing baseline positional behavior, leaking query state, or accepting unsupported execution.
**R160-01 through R160-11 are individually binding, P1, including each original negative case.** Their verbatim text is incorporated by immutable source reference `214c1323:docs/specs/status/rc2-160-join-alias-mixing-1.md:33-44`. Before DO, coder mechanically copies those eleven clauses into this status file and `artifacts/pdca/D160/rv1/acceptance.md`; no interpretation, renumbering, or replacement by the scenario summaries below. CHECK evaluates every clause separately.

| Acceptance ID | Required criterion and negative case | New evidence row |
|---|---|---|
| R160-01 | Verbatim R160-01, including its negative case | N160-01 |
| R160-02 | Verbatim R160-02; additionally prove baseline-green → historical-defect-red → revised-green | N160-02 |
| R160-03 | Verbatim R160-03, including its negative case | N160-03 |
| R160-04 | Verbatim R160-04, including its negative case | N160-04 |
| R160-05 | Verbatim R160-05, including its negative case | N160-05 |
| R160-06 | Verbatim R160-06, including its negative case | N160-06 |
| R160-07 | Verbatim R160-07, including its negative case | N160-07 |
| R160-08 | Verbatim R160-08, including its negative case | N160-08 |
| R160-09 | Verbatim R160-09, including its negative case | N160-09 |
| R160-10 | Verbatim R160-10, including its negative case | N160-10 |
| R160-11 | Verbatim R160-11, including its negative case | N160-11 |

## Minimal solution and alternatives
- Essential goal: preserve source/slot identity while composing aliases and positional joins.
- Non-negotiable boundary: retain baseline positional state transfer, including omission of `SourceEntityType` and `BindArrayJoinElement`; no shared-command mutation.
- Optimum: transfer only alias/slot metadata through a distinct explicit seam, with generator diagnostics and expression rewriting at existing boundaries.
| Approach | Benefit | Cost/risk | Decision |
|---|---|---|---|
| Apply the historical patch wholesale | Least editing | Known non-reproducible green claim; positional regression; patch alone does not reconstruct STOP tree | Reject |
| Shared broad state-copy helper | Superficial deduplication | Changes positional semantics through previously omitted fields | Reject |
| Selective semantic port plus explicit alias/slot seam | Preserves established boundary; auditable footprint | Additional tests and controlled reconstruction | Choose |

## Tasks and port decision
- **P160.2 — fix now:** freeze verbatim acceptance, author own rv1 rows, record legacy rv1→rv5 as archive only. Do not import E160-01..33/C-E01..04 as obligations.
- **D160.1 — fix now:** record commit/tree identities for `6f01bc65`, `9a2a2871`, `40b1a159`, `214c1323`; export clean source snapshots; capture generator inputs, SDK, package/config identities.
- **D160.2 — fix now:** establish the identical positional-only R02 probe in baseline, historical and revised snapshots before treating the port as valid.
- **D160.3 — fix now:** selectively port relevant production/test changes from the complete historical tree, not the standalone reconstruction patch; exclude STOP bookkeeping and incremental/generated outputs. Preserve `EntityBuilder.cs:2787-2827`; scrutinize `SourceEntityType:263/:42`, `BindArrayJoinElement:265/:43`, `CopyTo:1989/:1998-1999`. Introduce the separate alias/slot seam without widening positional copying. Review the six reported production files: `AnalyzerReleases.Unshipped.md`, `JoinAliasGenerator.cs`, `EntityBuilder.cs`, `Projection.cs`, `SqlBuilder.cs`, `SqlSourceRenderer.cs`; discover actual port locations with Roslyn.
- **D160.4 — fix now:** implement and close the complete variant matrix below, incl. misuse diagnostics, cache isolation, six-provider execution.
- **D160.5 — fix now:** update EN/RU docs; execute fresh-build, coverage and performance protocols; assemble evidence without declaring a CHECK verdict.
- **Deferred:** unrelated generator cleanup, broad state-transfer refactoring, unrelated provider gaps.
- Existing unfinished implementation remains active; neither analysis nor a legacy "completed" claim marks it done or superseded.

## Execution mode and footprint
Single lane, current worktree, branch `1.0.9-rc2`; no branch switching, new worktrees, commits, merges, or blind patch application. Build snapshots are disposable source exports, not development worktrees; all implementation changes remain uncommitted in the current tree.
Footprint: the reported 22-file legacy delta, six production files, relevant generator/core/integration tests, benchmark additions, EN/RU docs, status and evidence helpers. Overlapping builder/generator/renderer contracts require sequential integration; narrow the delta rather than copying all +3037 lines automatically.

## Test strategy and closed variant matrix
Unit/compiler tests for rewriting and guards; real integration for SQL/provider behavior. Broad selectors are intentional; no unverified future test symbols are asserted.
| Variant | Closure | Positive / negative observation | Selector |
|---|---|---|---|
| alias→positional, positional→alias, alternation | test | Correct binding / no alias loss, rebinding, or invalid SQL | U, S, I |
| `p.ItemK` ≡ slot-K; value/reference; null/default | test | Same selected source/value / no capture of another slot | U, S, I |
| Root alias: `From<T>`, named table/TableAlias, `FromSql` | test + guard | Slot 1 / repeated or invalid `.WithAlias` rejected | U, S, I |
| Root alias: CTE, CteReference, temporary table | test + guard | Slot 1 / unresolved or multiply named root rejected | U, S, I |
| Root alias: table function, QueryCommand, `CreateQueryBuilder*` incl. SQL, EntityBuilder | test + guard | Slot 1 / no source-category omission | U, S, I |
| Expression-only inputs | test + guard | Supported expressions rewrite / unsupported shapes fail explicitly | U, S |
| In-memory execution | guard + test | Unsupported alias path fails closed / never silently misbinds | U |
| t2/t3/t4 slot shifts, correlated APPLY | test | Correct outer/inner identities / no stale index or correlation | U, S, I |
| R02 `ArrayJoinElement(...).Join(pos)` | test | Baseline behavior retained / historical defect fails same assertion | H |
| Plan cache: repeated calls, mixed sequences, flags | test | Stable reuse and isolated invocation state / no sticky cache disabling | U, S, I |
| SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse | test | Required scenarios execute per provider / skips are not PASS | I |
No listed variant is deferred. Coverage: line **≥85%**, branch **≥75%** for configured included assemblies.

## Exact evidence invocations
All helpers are **planned artifacts**, authored and frozen before their first run; they log expanded commands, source hashes, results, exit codes.
- **U:** `dotnet test tests/nextorm.core.tests -c Debug`
- **S:** `dotnet test -c Debug` — provision provider environment first; record every project, test count, failure, skip.
- **I:** `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`
- **H:** `bash artifacts/pdca/D160/rv1/r02-chain.sh`; each state runs `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~Positional_join_control_pins_the_baseline_modifier_behavior`.
- **F:** `bash artifacts/pdca/D160/rv1/fresh-build.sh --states baseline,historical,revised`
- **V:** `bash artifacts/pdca/D160/rv1/coverage.sh` — repository CI dotnet-coverage→reportgenerator protocol.
- **M:** `bash artifacts/pdca/D160/rv1/performance.sh --states baseline,revised`
- **D:** `dotnet docfx docs/docfx.json`; **X:** `bash artifacts/pdca/D160/rv1/audit.sh`
Normal runs require exit 0, nonzero relevant test counts, zero failures, no required-provider skips. H requires baseline **1/1 pass exit 0**, historical **1/1 assertion failure exit 2**, revised **1/1 pass exit 0**; compilation/environment failure is not the required red. F reconstructs complete tracked inputs plus recorded candidate changes/new files, excludes bin/obj/generated outputs, builds fresh, preserves logs and generated-source inventories/hashes. Load the integration skill; start/recheck Podman if needed; configure MariaDB through its actual existing harness.

## Design checklist, docs, performance, reconnaissance
- Design: stable source identities; slot-1 naming across every root; explicit alias seam; baseline positional boundary; correlation scope; expression-only guards; in-memory fail-closed; compiler diagnostics; no sticky shared-command/cache mutation; nullable/analyzer-clean; CRLF preserved.
- Docs: update affected public API/examples/limitations in EN and RU together; curated references/links when applicable; no public links to internal specs; do not edit generated `docs/api`/`_site`.
- Performance measurement required: generator work is largely compile-time, but builder/renderer/cache seams affect execution; `SqlBuilder.cs:185` is part of the rendering boundary. Reproduce all seven acceptance cases, record baseline ratio **1.87**, investigate **>2.244**; additionally measure positional baseline/revised and revised mixed-alias paths with identical workloads. M uses BenchmarkDotNet, separates SQL construction from execution, captures allocations/environment/raw reports; discover actual benchmark identities.
- Bounded reconnaissance required: D160.2 is the controlled experiment proving the transfer boundary; observable criterion = H's exact green/red/green sequence from identified fresh sources. No further architecture spike or legacy-contract retrieval needed.

## Versioned evidence contract — rv1
Common slots apply to every row: owner=`CHECK` (coder supplies execution evidence); priority=P1; applicability=`true for this cycle`; `rv=1`. Sources are **planned**; artifacts under `artifacts/pdca/D160/rv1/<row-id>/`. Missing evidence means **open**, never PASS.
| Row ID / requirement | Required scenario and exact invocation | Expected evidence / artifacts |
|---|---|---|
| N160-01 / R160-01 | Verbatim clause + negative; U,S,I,X | Actual scenario crosswalk, tests/logs, source review |
| N160-02 / R160-02 | Verbatim clause + negative; H,U,F,X | Identical probe hash, three source identities, assertion logs, seam review |
| N160-03..N160-11 / R160-03..R160-11 | Verbatim clause + negative; U,S,I,X | Actual scenario crosswalk, tests/logs, source review |
| N160-12 / R160-01..11 | Reproducible fresh source/generator chain; F | Input manifests, expanded commands, build logs, generated hashes |
| N160-13 / R160-01..11 | Entire closed variant/provider matrix; U,S,I,X | Per-variant and six-provider execution/guard ledger |
| N160-14 / R160-01..11 | Coverage targets; V | Raw coverage, reports, numerical threshold checks |
| N160-15 / R160-01..11 | Seven acceptance and join-seam performance; M | Raw BDN reports, baseline/candidate comparison, outlier explanation |
| N160-16 / R160-01..11 | EN/RU consistency and design audit; D,X | DocFX log, locale/link audit, Roslyn review ledger |
Freeze manifest fields `{task,tree,contract_rv,status_file,required_rows[],rows[]{id,status,evidence[],reason,predicate}}`; all sixteen rows required; statuses only `met|open|na`. For always-applicable rows `na` is invalid. Legacy rv5 is archived, **not superseded by this cycle's rv1**. A later in-cycle revision explicitly supersedes its predecessor, preserves IDs/obligations, adds new-variant IDs. **CHECK re-gather budget:** two targeted retrieval rounds total, owned by CHECK and routed through the orchestrator.

## Assumptions, classification, risks and references
Missing exact new symbols/locations, generator inventories and MariaDB setup are discovery/execution outputs, not permission to invent evidence. Predecessors are the accepted current-tree contents at `6f01bc65`. Classification: known positional regression and reproducibility deficit → in-cycle prerequisites, not an external blocker. Persistent low confidence after targeted evidence gathering → orchestrator calls `escalate`, trigger 5.
Main risks: broad historical state copying, stale generated outputs, wrong slot/correlation rebinding, provider skips, cache leakage, performance cases missing the changed seam.
References: #160/T160; legacy status `214c1323:…:19,33-44,60-91,150`; implementation tip `40b1a159`; baseline renderer `SqlBuilder.cs:185`; `ArrayJoinProjection.cs:39`; global `pdca-dotnet` evidence contract `:905-945`.
**Handoff:** coder persists this complete plan, verbatim acceptance snapshot and rv1 contract before DO; port selectively, prove H, then close all sixteen rows; CHECK alone issues the verdict.
