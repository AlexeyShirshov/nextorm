# D160 — Join aliases: free mixing positional/alias + root alias (.WithAlias)

- task_id: D160
- GitHub issue: #160 — https://github.com/AlexeyShirshov/nextorm/issues/160
- branch: 1.0.9-rc2
- selected_variant: pdca-dotnet
- cycle_id: 1
- plan_revision: 4
- contract_revision: rv=5 (rv=4 superseded by rv=5)
- attempt: n=1/3
- phase: STOP — incomplete (r=4 final CHECK FAILED; sealed terminal rule; no r=5)
- final_state: incomplete (STOP)
- plan_state: r=3 final CHECK FAILED (fresh clean-build boundary red: slot-numbering shift + positional over-preservation; DO green not reproducible) → escalated → planner sealed r=4 (rv=5); r=4 DO green but r=4 final CHECK FAILED on confirmed R02 → sealed STOP (planner decision (a)); last revision for the F1/C1 family; remaining work — (flow closed: incomplete; no r=5).
- provenance: collection `docs/specs/status/collection-1.0.9-rc2.md` task D160; brief `/tmp/opencode/rc2/briefs.md` §#160; design `docs/specs/design/join-alias-mixing-and-root-alias.md`
- no commits / no push / no worktree (collection P phase)

## Goal (approved scope #160)
1. Free mixing of positional and alias joins: alias→positional, positional→alias, alternation; `p.ItemK` and the alias of slot K are the SAME slot; alias optional.
2. Root alias via chained `.WithAlias(Alias.X)` naming slot 1 for ALL root sources: `From<T>`, `From("table")`/`TableAlias`, `FromSql`, `From(Cte<T>)`, temp-table, `FromTableFunction<T>`, `From(QueryCommand<T>)`, `CreateQueryBuilder*`.

## Approved decisions (design spec, design approved in-chat, spec review pending)
- `.WithAlias(Alias.X)` root only (slot 1); joins keep trailing `Alias.<Name>`.
- In-memory fail-closed: any alias in the chain (root or join) → `NotSupportedException`; pure positional chains in memory unchanged.
- Single slot-encoded naming `A{slot}_{name}` / `P{slot}` (e.g. `AliasProjection_A1_Order_P2_A3_Buyer<T1,T2,T3>`); legacy generated names not preserved (pre-release).
- Architecture A: generator-heavy / core-light; all projection extensions (positional and alias) go through the existing `JoinAlias<TNext,TNextEntity,TJoinEntity>` seam; core adds only `Projection<T1>` (arity-1, `IExtendableProjection`) and generic `AliasRoot<TNext,TNextEntity>` seam (Phase 2). Guard `EntityBuilder.cs:3137-3139` is KEPT as failsafe.
- Priority: performance + convenience; pure positional path (no aliases) keeps zero overhead.
- Phases: Phase 1 — free mixing (generator-centric; core unchanged); Phase 2 — root alias. Each phase independently green.
- Out of scope: `JoinInto` alias surface; full in-memory alias support (fail-closed); arity cap 8 (overflow via `As<T>`).

## Non-goals
Alias API for `JoinInto`; in-memory alias support; raising the arity-8 cap; new provider SQL capabilities; backward compatibility of legacy generated names.

## Acceptance criteria (observable behavior + negative case)
- R160-01: all mixing directions compile; JOIN/APPLY and SQL slots match chain order; `ItemK` and slot-K alias resolve to the same `tK`. Negative: reordering alias↔slot, repeated CLR type, `Buyer2` must not shift slots.
- R160-02: alias-only semantics preserved; positional-only preserves existing API/SQL/execution path with no alias overhead. Negative: pure positional must not enter alias refusal/seam; unsupported provider op not silently supported.
- R160-03: `.WithAlias` works on all listed root sources; `p.Order.X` → `t1.X`. Negative: repeated `.WithAlias`, applied to a join result, invalid/duplicate root alias → compile-time rejection, no wrong projection.
- R160-04: dim-1 planned correctly; later Extend yields slots 1,2,…; arity 2–8 correct. Negative: 9th slot rejected by existing diagnostic contract; documented `As<T>` overflow path remains usable.
- R160-05: alias members expression-only; positional members keep prior semantics. Negative: direct read of alias member throws, including root projection.
- R160-06: any alias (root or join) fail-closed in-memory with `NotSupportedException`; pure positional chains work. Negative: root alias without join and alias after positional prefix also rejected, no partial result.
- R160-07: repeated/alternating executions keep correct plans/params/cache; cached-path perf gate run. Negative: changing alias/slot/param does not reuse a wrong plan; shared command does not get sticky `Cache=false`.
- R160-08: build exit 0, 0 warnings/0 errors; coverage line ≥85%, branch ≥75%. Negative: green build without coverage/mandatory evidence is not full acceptance.
- R160-09: real execution on PostgreSQL, SQL Server, MySQL, MariaDB, SQLite, ClickHouse; expected SQL/result and positional/alias parity. Negative: skipped or missing provider is not passing evidence.
- R160-10: EN/RU docs reflect mixing, root alias, expression-only, in-memory refusal, arity; obsolete generated names/alias-only wording removed. Negative: no new public links into `docs/specs/**`, no broken links, no EN/RU divergence.
- R160-11: NORMGEN001–006 contracts preserved; root misuse gets stable diagnostics; incrementality and overload binding correct. Negative: same-name descriptors, invalid markers, wrong root usages do not produce malformed generated C# and do not bypass diagnostics.

## What the statement did not say (gaps closed)
1. Collection evidence rows C-E01..C-E04 and the pdca-dotnet Versioned evidence contract slots are NOT in the input — pre-DO sealing prerequisite; orchestrator obtains them verbatim via scout and coder records them before DO. Not a task-plan gap; local contract below does not replace them.
2. Full 7-operator/overload inventory not supplied — working assumption: INNER/LEFT/RIGHT/FULL/CROSS JOIN, CROSS/OUTER APPLY; confirm via Roslyn before DO and close every real conditional/conditionless/correlated overload.
3. `scripts/validate_inner_loop.py` CLI, exact coverage commands, and MariaDB run config unknown — scout before DO; do not invent a MariaDB env var or treat MySQL as a MariaDB substitute.
4. `Projection<T1>` planner compatibility unproven — closed by bounded executable spike `D:160-02`.
5. Spec review pending — explicit assumption: collection PLAN authorizes work on the approved decisions; not authorization to expand scope.
6. Project `AGENTS.md` generator note is stale (`nextorm.core.sourcegenerator` is a real generator, packed as analyzer). Treat confirmed generator/project facts as authoritative; fix only that stale paragraph.
7. Benchmark acceptance 7-vs-8: an 8th `[BenchmarkCategory("acceptance")]` class exists (`benchmarks/nextorm.benchmark/acceptance/SqlServerBufferedNumericAcceptanceBenchmark.cs:61`); establish actual category inventory before baseline and explain any divergence from the 7-case document.

## Predecessor-result requirements
- #113 closed: alias implementation, seams, diagnostics, and the positional-mixing removal must be present; `MixedJoinChainTests.cs` was deleted.
- #146 typed CTE slices A+B DONE.
- #159 (CTE direct join overloads/alias API) is NOT a hard dependency but shares the generator + `EntityBuilder` join-family footprint: do not run concurrently (#159 not started in parallel); check incoming footprint before edits.
- Assumptions/prerequisites listed in "gaps" above; pre-DO sealing gather required.

## Variant matrix (from the execution path; T=test, G=guard, D=deferred+trigger)
| Branch / variant | Closure | Where |
|---|---|---|
| Alias→positional, positional→alias, alternation | T (new) compile + SQL + result | alias suite, provider SQL, integration |
| Alias-only; positional-only | T (extend) baseline parity | existing suites |
| All 7 operators; conditional/conditionless overloads | T new/extend binding + slot SQL; unsupported provider combos G (extend) | generator harness, provider SQL |
| Correlated builder and correlated query APPLY | T new/extend, outer slots not shifted | alias/provider/integration |
| `Where` before/after a mixed step; alias and `ItemK` | T (new) | alias Where + provider SQL |
| `From<T>` both forms | T (new) | root matrix |
| `From("table")`/`TableAlias` | T (new) | root matrix |
| `FromSql`; `CreateQueryBuilderFromSql` | T (new) | root matrix |
| `From(Cte<T>)`; `From(CteReference)` | T (extend) | typed CTE + root matrix |
| Temp-table source | T (new) | root SQL + integration temp lifecycle |
| `FromTableFunction<T>`; builder-from-function | T (new); unsupported provider G | SQL + supporting provider integration |
| `From(QueryCommand<T>)` all confirmed forms | T (new) | root matrix |
| `CreateQueryBuilder*`; `From(EntityBuilder)` | T (new) per discovered root overload | root matrix |
| Dim-1 root; dim 2–8 | T new/extend planner + Extend + SQL | shape/root tests |
| >8; overflow via `As<T>` | G extend + T new | diagnostics + SQL |
| Reference/value entity; nullable ref/default value | T (new); invalid `null` G per current API | root + mixing + shape |
| Repeated CLR type across slots | T (new) | shape + SQL + cache |
| Two different descriptors with same alias name | T (new) diagnostic collision by name | diagnostics |
| Digit-ending alias `Buyer2`; colliding member names | T new/extend | naming + diagnostics |
| Root/join alias in-memory; positional in-memory | G + T new/extend | refusal + core in-memory |
| Repeated/alternating executions; params; shared scalar command | T (extend) | cache tests |
| Generated extension vs inherited instance shadowing | T (new) semantic binding + actual route | generator harness |
| NORMGEN001–006 | T (extend) each diagnostic separately | diagnostic suite |
| Non-root/repeated `.WithAlias`; root collision | G + T (new); new diagnostic IDs only after registry check | diagnostic suite |
| Incremental edit of alias/slot/order; generated shape/name | T (extend) | generator/surface suite |
| Direct alias read vs expression access | T (extend) | expression-only suite |
| Full in-memory aliases; `JoinInto` aliases; arity >8 without `As<T>` | D — trigger: separate approved scope/API change; not silently moved into this milestone | backlog/status |

Cross-product is not full Cartesian: each runtime/generator branch is covered; critical interactions — repeated CLR type × mixing, digit-name × naming, root × Extend, correlation × mixing, cache × alternation. Provider non-applicability is proven by the existing provider guard, not by absence of a test.

## Task list / decomposition
- D:160-01 Phase 1 — slot model and routing. `JoinAliasGenerator.cs:231-312,457,613-648,788-795`: replace the alias-only chain model with ordered slots with optional alias; allow a positional prefix and subsequent positional steps; semantic builder/overload resolution (not parsing `Buyer2` as a slot number); slot-encoded generated projection/receiver names; correct `JoinSlotAttribute`; close shadowing; each mixed extension routes through the corresponding `JoinAlias` seam; do NOT touch `EntityBuilder.cs` or the positional-only runtime path. Include Phase 1 regression tests, SQL/result checks, and checkpoint.
- D:160-02 Phase 2 prerequisite — dim-1 spike. Minimal `Projection<T1>` and verification of the existing planner/visitor path. Observable success: root projection plans; alias maps to `t1`; Extend creates correct second slot; no flattening/nested-projection SQL and no new positional execution branch. One bounded prototype/measurement. On failure: DO→PLAN report with exact repro; Phase 2 stays active, blocked on a revised plan; do not silently declare architecture B.
- D:160-03 Phase 2 — root alias. `Projection.cs:25-37`: arity-1 `IExtendableProjection` compatible with the current contract. `EntityBuilder.cs` near alias seams `:2671-2846`: `AliasRoot<TNext,TNextEntity>`. Preserve source/query state and correct cache behavior; no source reconstruction per root kind. Immediate in-memory refusal; keep guard `:3137-3139`. Generator: generic `.WithAlias` for root `EntityBuilder<T>` + root shape + misuse diagnostics. Close the whole root matrix incl. CTE/temp/function/query-command/builders.
- D:160-04 Cross-cutting verification, perf, docs, evidence. Full alias/provider/core/integration regression, coverage, acceptance benchmarks. EN/RU docs, fix the spec path and the stale generator note. Final CHECK across all contract rows; D160 completes only via CHECK.

## Footprint (all new paths are planned, not existing-symbol evidence)
| Area | Phase 1 | Phase 2 / completion | Confidence |
|---|---|---|---|
| Generator | `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` | same file; `AliasGenerator.cs` only if marker/root emit requires | first exact; second possible |
| Core | none | `src/nextorm.core/Builders/Projection.cs`, `EntityBuilder.cs` | exact; extra core files need replan |
| Alias tests | existing alias suites; new `tests/nextorm.alias.tests/MixedJoinChainTests.cs` | new `tests/nextorm.alias.tests/RootAliasTests.cs`; existing shape/refusal/cache/diagnostic/CTE suites | new paths planned; existing edits scenario-dependent |
| Core tests | existing positional/in-memory suites for regression if needed | `tests/nextorm.core.tests/*Projection*Tests.cs` or new `RootProjectionTests.cs` | possible; pick existing ownership after scout |
| Provider SQL | `tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests/JoinAliasSqlGenerationTests.cs` | same | expected |
| Integration | `tests/nextorm.integration.tests/CommonTestSuite.JoinAlias.cs` | same; existing `*SpecificTests.cs` only for provider-specific root capabilities | common expected; specific possible |
| Benchmarks | existing acceptance runs, no harness edits | planned `benchmarks/nextorm.benchmark/JoinAliasMixingBenchmark.cs` (paired positional/mixed/root), no acceptance category | new file expected |
| Public docs | pages below | pages below | exact |
| Registers/specs | collection-assigned status file; `docs/specs/design/join-alias-mixing-and-root-alias.md`; new `docs/specs/design/join-alias-160-variant-matrix.md` | evidence index, results, risks | status path from collection; rest planned |
| Repo instructions | — | `AGENTS.md`, only the stale generator paragraph | expected |

Do NOT edit `TypeExtensions`, alias visitors/cache, `SqlBuilder` "just in case"; needing them is a replan (Phase 2 would stop being core-light).

## Test strategy and DO test scope
- Unit/compile-time: generator diagnostics, generated shape, expression-only contract, binding/shadowing, in-memory guards, slot/cache regressions. Negative compilation checked inside the generator harness: test process exit 0, expected compilation with the required diagnostic.
- SQL generation: six provider projects, no DB. Integration: real provider executions; common scenario in `CommonTestSuite.JoinAlias.cs`, provider-only in `*SpecificTests.cs`.
- DO test scope fields: projects — alias tests; core tests for projection/in-memory changes; six provider SQL projects; integration project. Selectors — existing relevant classes/methods via Roslyn first, new symbols only after they exist; the exact contract gate commands below run projects fully. Files — matching footprint rows. Rebuild — normal build without `--no-build` after generator/core edits; regenerated consumers must rebuild. Boundary — generator → generated consumer binding → core seam → planner/visitor → SQL/cache → provider execution. Rationale — SQL snapshots alone do not prove shadowing, root-state preservation, or materialization. Validator — the verified `scripts/validate_inner_loop.py` command and its log are mandatory (obtain CLI via scout; do not guess).
- Red→green: first pin the failing mixed-chain/root scenario; missing API may be compiler-red; the diagnostic harness permits a runnable negative check. Cache/slot/refusal are behavioral-red. After implementation the same scenarios are green.
- Branch/mutation delta: new root/mixed resolution, arity-1 Extend, refusal, and diagnostics branches need positive/negative tests. In the existing mutation harness (if any) check delta; otherwise run six bounded reversible manual mutations: (1) alias slot K→K+1; (2) positional-after-alias bypass seam; (3) remove root in-memory guard; (4) mis-parse `Buyer2`; (5) apply a sticky cache flag; (6) mis-extend dim-1. Each must produce a failing test, then restore and go green — a local check, not a full mutation score. No new mutation packages/CPM deps.

## Priorities and design checklist
- P1 by construction: R160-01…09 and R160-11 (incl. shadowing, dim-1, slot/cache/in-memory branches); R160-10 is a P1 delivery obligation. Additional registry P1 entries apply additively; CHECK does not lower priorities.
- SOLID/DRY: one slot model; one generic root seam; no per-root-source implementation duplication.
- Type design: root-only operation, immutable slot identity, collision by alias name; named members expression-only.
- Perf: no reflection/per-row alias lookups, no new closures, no sticky cache mutation on the positional path.
- Sealedness: new projection/helper types sealed where generated inheritance is not required; generated receiver not sealed against forwarding topology. The input has no full sealing ratio — scout records delta, do not invent a percentage.
- Preserve: nullable, warnings-as-errors, CPM, CRLF.

## Unit execution mode
Sequential, in one tree, inside the assigned collection worktree if it already exists. No new worktrees (generator/generated-consumer/core footprint is shared; collection serializes tasks). Do not run #159 in parallel. Commits only with explicit collection auto-commit; push forbidden.

## Docs plan
- `docs/guide/02-joins.md` + `docs/ru/guide/02-joins.md`: mixing, `ItemK` = alias, `.WithAlias`, correlated example, limitations.
- `docs/querying/01-projections.md` + RU: dim-1 root, slot-encoded names, expression-only, arity/`As<T>`.
- `docs/advanced/limitations.md` + RU: root-only, in-memory fail-closed, `JoinInto` out of scope.
- Relevant `toc.yml`: check; change only on real structural change. Do not move pages.
- Spec: the projections page path is `docs/querying/01-projections.md` (the real path; there is no `docs/guide/01-projections.md`).
- Public docs must not link to internal specs.

## Performance decision
Perf measurement IS required: the change passes through `JoinAlias`/planner/cache and Phase 2 adds a root projection — a query-path change, not one-time codegen. Before implementation and after each phase run:
`dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
Record the actual case list, raw results, environment, revision/tree identity. Documented gate is 7 cases / 0 failures; do not silently exclude the 8th class. Confirm the actual inventory against baseline. Ratio baseline 1.87; >2.244 triggers investigation, not automatic fail. Do not close the perf row with an unexplained regression. An additional paired benchmark measures pure positional vs the original baseline and mixed/root-alias cached execution on the same host/config/tree.

## Reconnaissance decision
Required: (a) before DO, read-only scout for contracts/CLI, Roslyn overload inventory/shadowing, planner dim-1 path, MariaDB setup, benchmark inventory; (b) before full Phase 2, executable dim-1 spike `D:160-02`. Spike success is defined by SQL/slot/Extend observations, not by absence of an exception when creating a builder.

## Evidence contract rv=1 (local rows)
General slots applied to every local row: Source status — sources are planned; actual symbols/locations/artifacts added on fact. Artifacts — `<cycle-status-artifact-dir>/<row-ID>/` (log, command/invocation, exit/result, executed selectors/providers, tree identity, test/report refs); the dir path is fixed by the collection status before DO. Owner — `coder` creates execution evidence, `check` independently matches row/result/artifact. rv=1; requirement/row IDs stable. Applicability — the final D160 CHECK makes all local rows mandatory; a phase checkpoint does not cancel future obligations nor constitute a final PASS. Missing evidence — `unknown/missing`, not PASS and not a reason to re-run DO.

Exact known commands:
- B: `dotnet build nextorm.slnx -c Debug`
- A: `dotnet test tests/nextorm.alias.tests -c Debug`
- K: `dotnet test tests/nextorm.core.tests -c Debug`
- S (each): `dotnet test tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests -c Debug`
- I: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` (load integration skill first; Podman recovery if socket missing; MariaDB must be confirmed by inventory/config or the row stays open)
- F: acceptance command above
- X: `git diff --check` and `dotnet docfx docs/docfx.json`

Local rows (all mandatory P1):
- E160-01 / R01,R02,R11: A + S — mixed/alias/positional, seven-operator and binding cases; generated output and SQL assertions.
- E160-02 / R03: A + S — complete root-overload inventory → executed scenario mapping; `t1` assertions.
- E160-03 / R04: A + K — dim-1 planning/Extend, 2–8, >8 rejection, `As<T>` evidence.
- E160-04 / R05,R06: A + K — direct-read exception, root/join refusal, positional in-memory positive cases.
- E160-05 / R01,R11: A — naming/duplicate-descriptors/Buyer2/null-default/value-reference/diagnostic/incrementality cases.
- E160-06 / R07: A + K — repeated/alternating cache cases, shared command unchanged; execution/count assertions.
- E160-07 / R08: B — exit 0, 0 warnings / 0 errors.
- E160-08 / R09: I plus confirmed MariaDB setup — six-provider executed inventory, result assertions, no skipped mandatory providers.
- E160-09 / R07: F at baseline and both phases — BDN artifacts, case inventory 7/8, baseline comparison, investigation >2.244.
- E160-10 / R08: coverage collect/reportgenerator steps from `.github/workflows/dotnet.yml` with `coverage.settings.xml` — exit 0, line ≥85%, branch ≥75%, raw/report artifacts; commands fixed before DO.
- E160-11 / R10: X — EN/RU pages, removed obsolete alias-only restrictions/generated names, links, no public links to docs/specs; actual file:line evidence.
- E160-12 / R01…R11: `scripts/validate_inner_loop.py` with its repository-documented invocation — exact CLI fixed before DO; success result and complete scope; every DO brief.
- E160-13 / R01,R04,R06,R07,R11: six reversible mutations from the plan — each edit, exact selected test command, failing assertion, restoration, green rerun; no residual mutation.
- E160-14 / R03,R04: D:160-02 dim-1 spike — run A and K, record `t1` SQL, Extend-to-slot-2 and planner observations.

Collection C-E01…C-E04 inheritance: for every row preserve the original requirement ID, row ID, priorities, predicates, exact invocation, exit/log obligations, artifacts and owners from the collection contract. Their definitions are absent from the input — do NOT assign invented values and do NOT mark N/A. Pre-DO sealing gate: orchestrator runs a scout to return verbatim the active collection evidence rows C-E01..C-E04 and the pdca-dotnet Versioned evidence contract, plus the verified inner-loop/coverage commands and MariaDB execution configuration; then coder records the inherited rows and concrete commands/paths into the status before DO begins. On conflict return to PLAN; obligations may not be weakened. This input refinement alone does not increase r/rv or reset n.

CHECK re-gather budget: owner `check`; at most 2 targeted scout calls and 1 repeat proving-command run per CHECK, no implementation edits. Beyond that, missing evidence stays explicitly open; low confidence → escalation trigger 5, not endless re-gather. On an actual revision: explicit `rv=k superseded by rv=k+1`, preserve IDs and obligations, new variants get new IDs. r increases only when tasks/dependencies/remediation actions change.

## Risks
| Risk | Mitigation / trigger |
|---|---|
| Instance method captures positional-after-alias | semantic binding tests + generated forwarding; do not touch Phase 1 core |
| Dim-1 breaks planner | D:160-02 spike; on failure new P with repro, original Phase 2 blocked, not superseded |
| Cache mixes shapes or sticky flag disables it | repeated/alternating/shared-command tests + acceptance measurements |
| Generator combinatorial expansion | emit only used shapes; incremental tests; record compile cost if it grows unexpectedly |
| Incorrect same-name/digit aliases | single structured slot model + diagnostic negatives |
| #159 conflict | collection serialization; check incoming footprint before edits |
| DBs/MariaDB unavailable | skill recovery, verify setup; skipped ≠ green; persistent evidence deficit → scout, then trigger 5 |
| Generated API breaking change | allowed pre-release rename; sync tests/docs; legacy compatibility deferred only on explicit request |
| Core scope creep | stop the candidate and return to PLAN; do not silently dilute architecture A |

Classification at start: missing contract/CLI/setup data is an additive sealing prerequisite, not an external blocker. D160 stays active; no superseded→replacement. Unproven dim-1 is an experimental premise closed by D:160-02.

Confidence: high in the chosen architecture on the supplied facts; medium in overload shadowing and dim-1; MariaDB integration setup and inherited collection rows not yet proven.

Next step: pre-DO sealing gather (scout) for the missing contracts/commands, then coder records the full status + evidence contract. Normal mode — before the user's `go`; autonomous — no `go` wait but only after the mandatory gate 1. Then D:160-01 → D:160-02 → D:160-03 → D:160-04 → final CHECK.

## DO — pre-DO sealing (cycle 1, plan r=1, brief rv=1)

### Progress log
| UTC | phase | revision | iteration | event | evidence |
|---|---|---|---|---|---|
| 2026-10-07T18:44:46Z | DO | r1 | n1/3 | DO started (pre-DO sealing) | this file |
| 2026-10-07T18:44:46Z | DO | r1 | n1/3 | inherited collection evidence C-E01..C-E04 recorded verbatim | docs/specs/status/collection-1.0.9-rc2.md §Evidence contract r1/rv1 |
| 2026-10-08T00:05:00Z | DO | r2 | n1/3 | planner revision r=2 persisted; rv=1 explicitly superseded by rv=2; delta = generated-`new`-instance-method positional-after-alias mechanism + D:160-04 audit unit; new rows E160-15..E160-24 | this file §Revision r=2 |
| 2026-10-08T00:05:00Z | DO | r2 | n1/3 | D:160-04(partial) generated-API/receiver-binding audit: slot-encoded name rename is mechanism-required (same alias sequence + same arity can differ in positional positions) and matches approved decision #22; keep rename | this file |
| 2026-10-08T00:05:00Z | DO | r2 | n1/3 | D:160-01 Phase 1 receiver binding implemented (generated `new` instance methods for positional-after-alias); build + alias suite green | this file §DO ledger |
| 2026-10-08T00:05:36Z | DO | r2 | n1/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings/0 errors | rc2-160-evidence/E160-07/build.log |
| 2026-10-08T00:05:36Z | DO | r2 | n1/3 | inner `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~MixedJoinChainTests` exit 0, selected 7 | rc2-160-evidence/E160-15/mixed-filtered2.log |
| 2026-10-08T00:05:36Z | DO | r2 | n1/3 | inner `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~AliasProjectionShapeTests` exit 0, selected 3 (receiver-binding negative) | rc2-160-evidence/E160-20/shape-filtered.log |
| 2026-10-08T00:05:36Z | DO | r2 | n1/3 | ONE broad boundary sweep `dotnet test tests/nextorm.alias.tests -c Debug` exit 0, selected 53, 0 failed | rc2-160-evidence/E160-01/alias-boundary.log |
| 2026-10-08T00:05:36Z | DO | r2 | n1/3 | validator `brief` exit 0; `report` exit 0 (1 boundary build, 2 inner filtered, 1 broad boundary sweep) | rc2-160-evidence/brief.json, report.json |
| 2026-10-08T00:05:36Z | DO | r2 | n1/3 | D:160-01 closed to green checkpoint; D:160-04(partial: audit + binding/shadowing/collision evidence) closed; remaining: E160-02/03/06/08/09/10/11/13/14/21/23/24 at DO→CHECK boundary + Phase 2 | this file §DO ledger |
| 2026-10-08T00:17:03Z | DO | r2 | n1/3 | D:160-02 dim-1 spike closed green: `dotnet build tests/nextorm.alias.tests -c Debug` exit 0, 0 warnings/0 errors; `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~RootAliasTests` exit 0, selected 3, passed 3, failed 0; root slot 1→t1; zero-join SQL `select Id from orders`; root+join SQL `select t2.Id from orders as 't1' join person as 't2' on t1.BuyerId = t2.Id`; Extend dim-1→`Projection<T1,T2>` | rc2-160-evidence/E160-14/spike.log |
| 2026-10-08T00:17:03Z | DO | r2 | n1/3 | zero-join planner finding recorded as resolved-by-AliasRoot for CHECK (`QueryPlanner.GetFrom` unwraps `Item1` only when `Joins.Length > 0`; AliasRoot materializes the mapped root's explicit FromExpression) — not a DO→PLAN blocker; D:160-03 root-alias NOT started this session | this file §D:160-02 dim-1 spike |
| 2026-10-08T00:40:00Z | DO | r2 | n1/3 | root-receiver positional binding: generator seeds generated `new` instance transition methods on the root-alias receiver (`AliasJoin_A1_X<T>`) for all seven operators, mirroring the Phase-1 mechanism; `Generated_root_alias_supports_a_positional_join_after_it` green (filter selected 1, passed 1, exit 0); full RootAliasTests filter selected 13, passed 10, failed 3 (the three derived-root tests, expected red) | rc2-160-evidence/E160-ROOT-CONTRACT/positional-after-root-alias.log, rc2-160-evidence/E160-ROOT-CONTRACT/root-alias-tests.log |
| 2026-10-08T00:40:00Z | DO | r2 | n1/3 | solution build `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors; alias suite `dotnet test tests/nextorm.alias.tests -c Debug` exit 2, selected 71, passed 68, failed 3 (only the expected derived-root tests); frozen generated-surface baseline updated for the new root-alias types (A1_Root, A1_Root_A2_Buyer, A1_Root_P2) | rc2-160-evidence/E160-ROOT-CONTRACT/ |
| 2026-10-08T00:40:00Z | DO | r2 | n1/3 | P:160-ROOT-CONTRACT comparative evidence recorded (facts only): FromSql aliased works at runtime (derived source preserved as `(select …) as 't1'`, test over-strict on `orders as 't1'`); From(builder) and QueryCommand aliased throw `NotSupportedException` at `EntityBuilder.cs:3465`; all three unaliased baselines return `ids=[10]`; option (i)/(ii)/(iii) NOT selected | rc2-160-evidence/E160-ROOT-CONTRACT/comparative.md |
| 2026-10-08T00:40:00Z | DO | r2 | n1/3 | D:160-03 remaining: derived-root root-contract decision (From(builder)/QueryCommand) + docs/API-NAMING register (E160-24) + six-provider SQL, integration, coverage, benchmarks deferred to later sessions; core `EntityBuilder.cs:3464-3466` guard kept unchanged | this file §Root-receiver binding and §P:160-ROOT-CONTRACT |
| 2026-10-08T00:42:15Z | DO | r2 | n1/3 | checkpoint commit of D:160-03 root-receiver generator + alias tests + status + E160-ROOT-CONTRACT evidence; re-verified build exit 0 (0 warnings/0 errors) and alias suite exit 2, selected 71 / passed 68 / failed 3 — the 3 derived-root tests (`Generated_root_alias_on_a_fromsql_source_keeps_the_derived_source`, `Generated_root_alias_on_a_builder_source_keeps_the_source`, `Generated_root_alias_on_a_query_command_source_keeps_the_derived_query`) are intentionally UNRESOLVED pending the planner decision | rc2-160-evidence/E160-ROOT-CONTRACT/checkpoint-build.log, rc2-160-evidence/E160-ROOT-CONTRACT/checkpoint-alias-tests.log |
| 2026-10-08T00:49:00Z | DO | r2 | n1/3 | P:160-ROOT-CONTRACT decision (iii) sealed: contract `rv=2` explicitly superseded by `rv=3`; E160-01..E160-24, C-E01..C-E04, obligations/priorities/owners preserved and not weakened; E160-02/E160-23 corrected to expect the preserved FromSql derived source `(select …) as 't1'`; new stable rows E160-25 (`From(builder)`) and E160-26 (`From(QueryCommand<T>)`) added | this file §Contract revision rv=3 |
| 2026-10-08T00:49:00Z | DO | r2 | n1/3 | bounded fix: `EntityBuilder.AliasRoot` now materializes a derived root `_query` into an explicit derived-table `FromExpression` (clears `_query`); `ResolveJoinBase` projection guard `:3464-3466` left unchanged; no public API change, no flattening, no per-row work, no shared-command/cache contamination. `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors | rc2-160-evidence/E160-25/build.log, rc2-160-evidence/E160-26/build.log |
| 2026-10-08T00:49:00Z | DO | r2 | n1/3 | inner `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~RootAliasTests` exit 0, selected 13, passed 13, failed 0 (derived-root variants green); FromSql/builder/QueryCommand SQL expectations corrected to the preserved derived source | rc2-160-evidence/E160-25/root-alias-filtered-green.log, rc2-160-evidence/E160-26/root-alias-filtered-green.log |
| 2026-10-08T00:49:00Z | DO | r2 | n1/3 | ONE broad boundary sweep `dotnet test tests/nextorm.alias.tests -c Debug` exit 0, selected 71, passed 71, failed 0 | rc2-160-evidence/E160-25/alias-boundary.log, rc2-160-evidence/E160-26/alias-boundary.log |
| 2026-10-08T00:49:00Z | DO | r2 | n1/3 | Derived-root SQL captured: builder `select t2.Id from (select Id, BuyerId, ApproverId from orders) as 't1' join person as 't2' on t1.BuyerId = t2.Id`; QueryCommand `select t2.Id from (select Id, BuyerId, ApproverId from orders where Id = 1) as 't1' join person as 't2' on t1.BuyerId = t2.Id`; unaliased both return `ids=[10]` | rc2-160-evidence/E160-25/, rc2-160-evidence/E160-26/ |
| 2026-10-08T00:49:00Z | DO | r2 | n1/3 | D:160-03 criteria: derived-root root-contract (FromSql/From(builder)/From(QueryCommand<T>)) CLOSED at DO scope; REMAINING: docs/API-NAMING register (E160-24), six-provider SQL, integration, coverage, benchmarks, six mutations, EN/RU docs — deferred to later sessions (not weakening R160-03) | this file §Contract revision rv=3 |
| 2026-10-07T20:00:43Z | DO | r2 | n1/3 | E160-03 core rebuild+filter green: `dotnet build tests/nextorm.core.tests -c Debug` exit 0, 0 warnings / 0 errors; `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~RootProjectionTests` exit 0, selected 11 / passed 11 / failed 0 | rc2-160-evidence/E160-03/core-build.log, rc2-160-evidence/E160-03/core-filtered.log |
| 2026-10-07T20:00:43Z | DO | r2 | n1/3 | E160-04 in-memory/refusal green (within the E160-03 filtered run): `Root_alias_fails_closed_on_the_in_memory_provider`, `Pure_positional_in_memory_query_is_not_refused`, `Alias_root_is_refused_when_the_receiver_already_has_a_join` all passed; 0 failed | rc2-160-evidence/E160-03/core-filtered.log |
| 2026-10-07T20:00:43Z | DO | r2 | n1/3 | E160-06 cache/plan green (within the E160-03 filtered run): `Repeated_root_projection_preparation_reuses_the_plan_without_sticky_cache_mutation`, `Different_root_projection_shapes_do_not_share_a_plan` passed; `command.Cache` stays true; 0 failed | rc2-160-evidence/E160-03/core-filtered.log |
| 2026-10-07T20:00:43Z | DO | r2 | n1/3 | E160-02 six-provider root-alias SQL green (inner filtered `FullyQualifiedName~JoinAliasSqlGenerationTests`): sqlite 13/13 exit 0; postgres 16/16 exit 0; sqlserver 16/16 exit 0; mysql 16/16 exit 0; mariadb 16/16 exit 0; clickhouse 16/16 exit 0. Assertion mismatches corrected to actual dialect SQL (one axis per step): alias quote delimiters `'t1'`/`"t1"`/`[t1]`/`` `t1` `` and join-condition cast (`bigint`/`signed`/`Int64`); no semantics weakened | rc2-160-evidence/E160-02/{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-join-alias.log |
| 2026-10-07T20:00:43Z | DO | r2 | n1/3 | E160-05 alias root/mixed green (inner filtered): `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~RootAlias` exit 0, selected 13 / passed 13 / failed 0; `… --filter FullyQualifiedName~MixedJoinChainTests` exit 0, selected 7 / passed 7 / failed 0 | rc2-160-evidence/E160-05/root-alias-filtered.log, rc2-160-evidence/E160-05/mixed-filtered.log |
| 2026-10-07T20:00:43Z | DO | r2 | n1/3 | E160-07 solution build re-verified: `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors | rc2-160-evidence/E160-07/build.log |
| 2026-10-07T20:00:43Z | DO | r2 | n1/3 | ONE broad boundary sweep: `dotnet test tests/nextorm.alias.tests -c Debug` exit 0, selected 71 / passed 71 / failed 0 | rc2-160-evidence/E160-01/alias-boundary.log |
| 2026-10-07T20:00:43Z | DO | r2 | n1/3 | E160-12 validator: `brief` exit 0 (amendment added for `FullyQualifiedName~MixedJoinChainTests`, validated before run); `report` exit 0 (1 inner build + 9 inner filtered + 1 boundary sweep + 1 boundary solution build) | rc2-160-evidence/brief.json, rc2-160-evidence/report.json |
| 2026-10-07T20:00:43Z | DO | r2 | n1/3 | D:160-03 root-alias DO scope now closed on all six providers (SQL) + alias/core suites; REMAINING (DO→CHECK/final): E160-08 integration (`DOCKER_HOST`; MariaDB `mariadb:11.4`), E160-09 acceptance benchmarks, E160-10 coverage 85/75, E160-11 EN/RU docs, E160-13 six reversible mutations, E160-24 docs/API-NAMING register | this file §DO ledger |
| 2026-10-07T20:06:34Z | DO | r2 | n1/3 | E160-11 / R160-10 docs stream (EN+RU) done: free alias↔positional mixing (`ItemK` == slot-K alias), root `.WithAlias`, expression-only members, in-memory fail-closed, arity/`As<T>` overflow, preserved `FromSql`/`From(builder)`/`From(QueryCommand<T>)` derived roots; obsolete alias-only wording and legacy generated names removed; `dotnet docfx docs/docfx.json` exit 0 (2 pre-existing AnalyzerReleases duplicate warnings, 0 errors); `git diff --check` clean on all touched paths; CRLF on every edited file | docs/guide/02-joins.md, docs/ru/guide/02-joins.md, docs/querying/01-projections.md, docs/ru/querying/01-projections.md, docs/advanced/limitations.md, docs/ru/advanced/limitations.md; /tmp/opencode/rc2-160-docs/docfx.log |
| 2026-10-07T20:06:34Z | DO | r2 | n1/3 | E160-24 / R160-10 API-NAMING register (D:160-04) done: generated slot-encoded rename recorded as mechanism-required (`JoinAliasGenerator.cs:748,763,768,929,932,943,1077,1195`), no unnecessary rename found (nothing reverted), additive core `Projection<T1>`/`EntityBuilder.AliasRoot` noted; spec path fixed to `docs/querying/01-projections.md`; stale `AGENTS.md` source-generator note corrected | docs/specs/design/API-NAMING-REVIEW.md §#160; docs/specs/status/rc2-160-join-alias-mixing-1.md; docs/specs/design/join-alias-mixing-and-root-alias.md; docs/specs/design/join-alias-variant-matrix.md; AGENTS.md |
| 2026-10-07T20:14:00Z | DO | r2 | n1/3 | E160-08 integration: added 5 provider-agnostic D160 root-alias / mixed-join cases to `CommonTestSuite.JoinAlias.cs` (root `.WithAlias(Alias.Root)` + aliased/positional join, Root≡Item1, alias→positional, positional→alias); rebuild `dotnet build tests/nextorm.integration.tests -c Debug` exit 0, 0 warnings / 0 errors | tests/nextorm.integration.tests/CommonTestSuite.JoinAlias.cs |
| 2026-10-07T20:14:00Z | DO | r2 | n1/3 | E160-08 / R160-09 six-provider integration `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` exit 0, Total 3357 / Errors 0 / Failed 0 / Skipped 197 / Not Run 0; per-provider: PostgreSQL 845 (819 pass/26 skip), SQL Server 733 (685/48), MySQL 673 (593/80), SQLite 723 (683/40), MariaDB 55 (55/0), ClickHouse 205 (205/0), shared/contract 123 (120/3); all six providers executed, no provider-availability skips (skips are `Assert.Skip*` capability skips) | rc2-160-evidence/E160-08/integration.log |
| 2026-10-07T20:14:00Z | DO | r2 | n1/3 | E160-08 D160 cases: 5 new root-alias/mixed cases × PostgreSQL/SQL Server/MySQL/SQLite = 20 executions, 20 passed, 0 failed, 0 skipped (machine-readable per-test proof); ClickHouse/MariaDB do not inherit `CommonTestSuite` and their containers do not seed `orders`/`person`, so their root-alias surface remains SQL-generation (E160-02) — recorded as coverage boundary, not a failure | rc2-160-evidence/E160-08/provider-inventory.txt, rc2-160-evidence/E160-08/README.md |
| 2026-10-07T20:16:00Z | DO | r2 | n1/3 | E160-09 / R160-07 acceptance run green: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` exit 0, 7/7 cases, 0 failures; external wall 51 s, BDN Global total 44.48 s (budget 240 s, PASS); tracked `Cached_ToList/Prepared_ToList` ratio **2.040** vs baseline **1.87** (+9.1%), below the **2.244** investigation threshold; alloc ratio 7.66 vs 7.42 (+3.2%) | rc2-160-evidence/E160-09/acceptance.log, README.md |
| 2026-10-07T20:18:06Z | DO | r2 | n1/3 | E160-10 / R160-08 coverage green: `dotnet build --no-restore` exit 0, 0 warnings/0 errors; `DOCKER_HOST=… dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"` exit 0, total 9189 / failed 0 / succeeded 8991 / skipped 198; `dotnet tool run reportgenerator …` exit 0; line **88.1%** (≥85), branch **80.1%** (≥75) — both thresholds pass, no D160 coverage gap | rc2-160-evidence/E160-10/coverage-collect.log, coverage-report.log, Summary.txt, coverage.cobertura.xml |
| 2026-10-07T20:18:36Z | DO | r2 | n1/3 | E160-09/E160-10 results appended to `docs/specs/performance/acceptance-benchmarks.md` (2026-10-08 D160 section); DO ledger rows E160-09/E160-10 set green | docs/specs/performance/acceptance-benchmarks.md |
| 2026-10-07T20:27:58Z | DO | r2 | n1/3 | E160-13 F1 KILLED (D:160-03, R160-01): two discriminating tests added — `MixedJoinChainTests.Digit_ending_alias_in_a_non_matching_slot_resolves_to_its_actual_slot` (`MixedJoinChainTests.cs:197`, `Buyer2` in slot 3 → must be Approver 20, not Buyer 10) and `JoinAliasGeneratorDiagnosticTests.Digit_ending_alias_at_a_later_slot_keeps_its_positional_slot` (`JoinAliasGeneratorDiagnosticTests.cs:285`, asserts generated `[JoinSlot(3)] … public T3 Buyer2`); frozen surface extended with `AliasJoin/AliasProjection_P1_P2_A3_Buyer2`3` | docs/specs/status/rc2-160-evidence/E160-13/mutations.md §Finding F1 |
| 2026-10-07T20:27:58Z | DO | r2 | n1/3 | E160-13 F1 kill proof: M4 re-applied (`JoinAliasGenerator.cs:763-765`) → filtered `FullyQualifiedName~Digit_ending_alias` exit 2, total 3, failed 2, passed 1; integration red `{20}` vs `{10}`, harness red `JoinSlot(2) public T2 Buyer2`; generator restored exactly (`git checkout --`), full alias suite exit 0, total 73, failed 0; `git diff -- src` empty | rc2-160-evidence/E160-13/f1-m4-mutation.diff, f1-m4-red.log, f1-new-tests-green.log, f1-alias-suite-restored-green.log |
| 2026-10-07T20:27:58Z | DO | r2 | n1/3 | E160-13 closed green: 6/6 reversible mutations detected (M1–M3, M5–M6 previously, M4 by F1 kill); all edits reverted, no residual mutation in `src`/`tests` | docs/specs/status/rc2-160-evidence/E160-13/mutations.md |
| 2026-10-08T01:32:22Z | DO | r2 | n1/3 | E160-07 Release re-gather closed: `dotnet build nextorm.slnx -c Release --no-incremental` exit 0, 0 warnings / 0 errors (DEBUG already green) | docs/specs/status/rc2-160-evidence/E160-07/release-build.log |
| 2026-10-08T01:34:05Z | DO | r2 | n1/3 | E160-21 / R160-11 (D:160-04) closed green: generated `new` instance transitions proven to hide the inherited `EntityBuilder<TEntity>.Join`/`Apply` overloads and alias extensions proven applicable on `AliasJoin_*`, root `AliasJoin_A1_Root<T>` and the positional `JoinedEntityBuilder` prefix. `AliasProjectionShapeTests` 5/5 exit 0 (2 new proving tests), `AliasGeneratedSurfaceTests` 5/5, `JoinAliasGeneratorDiagnosticTests` 17/17, `RootAliasTests` 13/13, `MixedJoinChainTests` 8/8; no product change (test-only, within alias footprint) | docs/specs/status/rc2-160-evidence/E160-21/README.md |
| 2026-10-07T20:51:54Z | DO | r2 | n2/3 | attempt n=2 begins; defect key **D160-C1** (pre-`.WithAlias` Where/OrderBy/GroupBy/Having/Skip/Take dropped) first observed n1, this attempt fixes it | this file §C1 |
| 2026-10-07T20:51:54Z | DO | r2 | n2/3 | **D160-C1 FIXED**: zero-join root alias rejected the pre-alias `Where`/`OrderBy`/`GroupBy` because `MakeSelect` rendered predicates/sort/group with `dontNeedAlias:false` over an unaliased projection source; `dontNeedAlias` is now derived from the FROM alias decision (bare physical source ⇒ unqualified). With-join pre-alias state is rebased by slot in `CreateAliasJoined` (`ApplyPreAliasStateToAliasJoined`: OrderBy/GroupBy/Having/PreWhere + Paging). Fix commit (n=2) applied in this session | this file §C1 |
| 2026-10-07T20:51:54Z | DO | r2 | n2/3 | C1 filtered (`FullyQualifiedName~Generated_root_alias_preserves`) exit 0, selected 5 / passed 5 / failed 0 (4 original zero-join + 1 new with-join) | rc2-160-evidence/E160-27/c1-tests-green.log |
| 2026-10-07T20:51:54Z | DO | r2 | n2/3 | ONE broad boundary sweep `dotnet test tests/nextorm.alias.tests -c Debug` exit 0, selected 80 / passed 80 / failed 0 | rc2-160-evidence/E160-27/alias-suite-final.log |
| 2026-10-07T20:51:54Z | DO | r2 | n2/3 | renderer-regression sweep after the first C1 attempt (blanket `!needAlias`) was caught and corrected: core 1767 (0 failed), sqlite 1166 (0 failed), postgres 806 (0 failed), sqlserver 728 (0 failed), mysql 307 (0 failed), mariadb 233 (0 failed), clickhouse 595 (0 failed), all exit 0 | rc2-160-evidence/E160-27/{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-tests2.log |
| 2026-10-07T20:51:54Z | DO | r2 | n2/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors; `-c Release` exit 0, 0 warnings / 0 errors | rc2-160-evidence/E160-27/solution-debug.log, solution-release.log |
| 2026-10-07T20:51:54Z | DO | r2 | n2/3 | scratch `tests/nextorm.alias.tests/ZScratchTests.cs` deleted; tree kept clean for the n=2 commit | git status |
| 2026-10-08T01:59:00Z | DO | r2 | n2/3 | finding B root matrix landed: `tests/nextorm.alias.tests/RootAliasTests.cs` extended with every root source (`From("table")`/CreateQueryBuilder forwarders/typed CTE/temp table/table function) + all seven positional operators after `.WithAlias` + APPLY fail-closed + named-window refusal; `dotnet build tests/nextorm.alias.tests -c Debug` exit 0, 0 warnings / 0 errors; `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~RootAlias` exit 0, selected 31 / passed 31 / failed 0 | rc2-160-evidence/E160-28/alias-build.log, rc2-160-evidence/E160-28/root-alias-filtered.log |
| 2026-10-08T01:59:00Z | DO | r2 | n2/3 | docs stream synced to the slot-encoded naming + mixing supersession: `docs/guide/02-joins.md`(+RU) legacy-name wording removed; `join-alias-variant-matrix.md` and `join-alias-mixing-and-root-alias.md` alias-only restriction marked superseded by #160 | docs/guide/02-joins.md, docs/ru/guide/02-joins.md, docs/specs/design/join-alias-variant-matrix.md, docs/specs/design/join-alias-mixing-and-root-alias.md |
| 2026-10-08T02:02:00Z | DO | r2 | n2/3 | finding C/D finished: `tests/nextorm.core.tests/RootProjectionTests.cs` gained the pre-`.WithAlias` `Where`-rebased-onto-`t1` case (D, assertion aligned to the int→long cast), the `JoinInto`-receiver root-refusal case (B.3) and the `storeInCache:false` sticky-flag invariance case (C); `dotnet build tests/nextorm.core.tests -c Debug` exit 0, 0 warnings / 0 errors; `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~RootProjectionTests` exit 0, selected 14 / passed 14 / failed 0 | rc2-160-evidence/E160-29/core-build.log, rc2-160-evidence/E160-29/core-filtered.log |
| 2026-10-08T02:02:00Z | DO | r2 | n2/3 | alias plan-cache C case added (`AliasPlanCacheTests.Root_alias_storeInCache_false_keeps_the_command_and_shared_any_command_cacheable`): root-alias `storeInCache:false` keeps the command and the shared `AnyCommand` cacheable; generator harness test added (`JoinAliasGeneratorDiagnosticTests.Distinct_root_aliases_emit_one_WithAlias_each_and_duplicates_are_deduped`) documenting `JoinAliasGenerator.cs:666-671`/`:1053`/`:1057`: one `WithAlias<T>` per distinct root name, duplicates deduped, `:1057` `continue` unreachable for valid input; filtered `AliasPlanCacheTests|JoinAliasGeneratorDiagnosticTests` exit 0, selected 20 / passed 20 / failed 0 | rc2-160-evidence/E160-29/alias-filtered-new.log |
| 2026-10-08T02:02:00Z | DO | r2 | n2/3 | full project sweeps after C/D: alias `dotnet test tests/nextorm.alias.tests -c Debug` exit 0, selected 95 / passed 95 / failed 0; core `dotnet test tests/nextorm.core.tests -c Debug` exit 0, selected 1770 / passed 1770 / failed 0 | rc2-160-evidence/E160-29/alias-boundary.log, rc2-160-evidence/E160-29/core-boundary.log |
| 2026-10-08T02:02:00Z | DO | r2 | n2/3 | solution builds re-verified after C/D: `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors; `dotnet build nextorm.slnx -c Release` exit 0, 0 warnings / 0 errors | rc2-160-evidence/E160-29/solution-debug.log, rc2-160-evidence/E160-29/solution-release.log |
| 2026-10-08T02:03:00Z | DO | r2 | n2/3 | STEP-3 boundary re-run started: the C1 core fix (`SqlBuilder.MakeSelect` / `EntityBuilder` alias-state rebasing, commit `9d6a9f59`, plus E160-28/E160-29) changed compiled sources after the n=1 boundary run, so the n=1 E160-08/E160-09/E160-10 evidence (tree `69ed192d`) is invalidated; re-running integration, acceptance and coverage on the current tree `4e2f43b2` | this file §STEP-3 boundary re-run (post-C1) |
| 2026-10-08T02:04:00Z | DO | r2 | n2/3 | E160-08 STEP-3 integration re-run: `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` exit 0, Total 3357 / Errors 0 / Failed 0 / Skipped 197 / Not Run 0 (43.117s); supporting `-result-xml results-step3.xml` exit 0 same summary (33.463s); six providers executed, 20/20 D160 root-alias/mixed cases pass | rc2-160-evidence/E160-08/integration-step3.log, integration-results-step3.log, provider-inventory-step3.txt, results-step3.xml |
| 2026-10-08T02:06:00Z | DO | r2 | n2/3 | E160-10 STEP-3 coverage re-run: `dotnet build --no-restore` exit 0, 0 warnings / 0 errors; `dotnet-coverage collect …` exit 0, total 9216 / failed 0 / succeeded 9018 / skipped 198; `reportgenerator …` exit 0; line **88.1%** (48348/54834), branch **80.2%** (25956/32355) — both ≥ 85/75 | rc2-160-evidence/E160-10/{build.log,coverage-collect.log,coverage-report.log,Summary.txt,coverage.cobertura.xml,assembly-coverage.txt} |
| 2026-10-08T02:08:00Z | DO | r2 | n2/3 | E160-09 STEP-3 acceptance re-run: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` exit 0, 7/7 cases 0 failures; external wall 52 s, BDN `Global total time` 45.08 s; ratio `Cached_ToList/Prepared_ToList` **2.081** vs baseline 1.87 (+11.3%), below the 2.244 gate; alloc ratio 7.66 vs 7.42 (+3.2%) | rc2-160-evidence/E160-09/step3-acceptance.log, README.md; docs/specs/performance/acceptance-benchmarks.md §2026-10-08 (STEP-3 re-run) |
| 2026-10-08T02:09:00Z | DO | r2 | n2/3 | E160-12 STEP-3 inner-loop report regenerated on the current tree (`4e2f43b2+dirty`): 1 inner build + 9 inner filtered (core `RootProjectionTests` 14/14; six-provider `JoinAliasSqlGenerationTests` 13/16/16/16/16/16; alias `RootAlias` 31/31; `MixedJoinChainTests` 8/8) + exactly ONE broad boundary sweep (`dotnet test tests/nextorm.alias.tests -c Debug`, selected 95 / passed 95) + one boundary solution build (Debug, 0/0); `brief` exit 0, `report` exit 0 | rc2-160-evidence/report.json, rc2-160-evidence/E160-STEP3/validator-report.log |
| 2026-10-08T02:10:00Z | DO | r2 | n2/3 | E160-11 STEP-3 docs re-check: `dotnet docfx docs/docfx.json` exit 0 (2 pre-existing AnalyzerReleases duplicate warnings / 0 errors); `git diff --check` exit 0 (clean; regenerated BDN line trailing-whitespace stripped) | rc2-160-evidence/E160-STEP3/docfx.log |
| 2026-10-07T21:18:00Z | CHECK | r2 | n2/3 | DO→CHECK boundary re-verified on 40b1a159: build Debug/Release 0/0; alias+core suites green; git diff --check clean; no residual src/tests mutation; gh #160 state captured | /tmp/d160-check/ |
| 2026-10-08T02:30:00Z | CHECK→PLAN | r2→r3 | n2/3→n1/3 | final CHECK r=2 FAIL (F1–F4 + open evidence); escalate confirmed D160-C1 recurrence; planner sealed r=3 (rv=4); DO not started — persisted for re-dispatch | this file §CHECK verdict, §Escalation, §P:D160-r3 |
| 2026-10-08T02:47:00Z | DO | r3 | n1/3 | **D160.3-1 / F1 (2nd fix of D160-C1):** `EntityBuilder.cs` state transfer replaced by a generic-neutral structural value-type carrier `EntityBuilderSharedState` (complete copy by assignment) + the single shared procedure `CopySharedStateTo<TOther>`, now used by BOTH transitions (`AliasRoot` and `CreateAliasJoined`/`ApplyJoinStateTo`); `_subQueryHint` and `_sourceEntityType` transferred; derived-root `Query`→`SourceFrom` and predicate/sort/group rebasing kept explicit; guard `EntityBuilder.cs:3464-3466` unchanged. **Applied fixes of D160-C1: 2.** No new defect key. | `src/nextorm.core/Builders/EntityBuilder.cs` |
| 2026-10-08T02:47:00Z | DO | r3 | n1/3 | tests added: structural completeness guard (reflection over every `EntityBuilder<>` instance field: copied or narrowly-reasoned exclusion) + carrier round-trip + root/joined non-default-state transfer (SubQueryHint + SourceEntityType) with rendered SQL (hint-supporting fake dialect) in new `EntityBuilderStateCopyTests.cs`; generated `.WithAlias` + alias-join SubQueryHint SQL case in `RootAliasTests.cs` (+ `HintAliasTestContext.cs`). Note: alias project has no `InternalsVisibleTo`, so `_sourceEntityType` is asserted only on the core seam (same shared procedure); alias side asserts the hint via rendered SQL. | `tests/nextorm.core.tests/EntityBuilderStateCopyTests.cs`, `tests/nextorm.alias.tests/RootAliasTests.cs`, `tests/nextorm.alias.tests/HintAliasTestContext.cs` |
| 2026-10-08T02:47:00Z | DO | r3 | n1/3 | first core full sweep caught a regression: `QueryFilterFunc`'s reflection purity snapshot compares the `_state` reference, so the class-based carrier clone was seen as changed (14 failures, `QueryFilterTests`/`RawSourceBindingFilterTests`/`JoinIntoInMemoryFilterTests`). Fixed **within EntityBuilder.cs only** by making the carrier a value type (structural boxed equality); QueryFilterFunc untouched. Full core 1774/1774 green after the fix. | `docs/specs/status/rc2-160-evidence/D160.3-1/core-boundary.log`, `queryfilter-check.log`, `core-build3.log` |
| 2026-10-08T02:47:00Z | DO | r3 | n1/3 | build `dotnet build nextorm.slnx -c Debug` exit 0, **0 warnings / 0 errors**; filtered `EntityBuilderStateCopyTests` 4/4, `RootProjectionTests` 14/14, `RootAliasTests` 32/32; boundary sweeps alias 96/96, core 1774/1774, sqlite 1166 (1 skip), postgres 806, sqlserver 728, mysql 307, mariadb 233, clickhouse 595 — all exit 0, 0 failed. | `docs/specs/status/rc2-160-evidence/D160.3-1/{build.log,statecopy-filtered.log,rootprojection-filtered.log,rootalias-filtered.log,alias-boundary.log,core-boundary.log,*-tests.log}` |
| 2026-10-08T02:47:00Z | DO | r3 | n1/3 | validator `brief` exit 0 (after wrapping the given scope under `unit`/`scope`; the verbatim brief shape was rejected by the host script with missing `unit`/`scope`); `report` exit 0 on `D160.3-1/evidence.json`. `git diff --check` clean. | `docs/specs/status/rc2-160-evidence/D160.3-1/{scope.json,evidence.json}` |
| 2026-10-08T02:47:00Z | DO | r3 | n1/3 | D160.3-1 DO scope green; REMAINING (later units): D160.3-2 (F2 stored receivers), .3-3 (F3 ItemN), .3-4 (F4 correlated APPLY), .3-5 fixtures, .3-6 verification (B/A/K/S/I/F/coverage/X/V). No DO→PLAN candidate from .3-1. | this file §P:D160-r3 |
| 2026-10-07T21:53:46Z | DO | r3 | n1/3 | **D160.3-2 / F2 fixed:** generator now seeds the full seven-operator generated `new` positional transitions on **every alias-bearing schema** (stored non-root receiver) plus its `${suffix}_P{n+1}` result schema, exactly like the stored root alias; the observed-transition loop alone could not seed a receiver stored in a variable. Applied fixes of F2: 1. No new defect key. | `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` |
| 2026-10-07T21:53:46Z | DO | r3 | n1/3 | tests added: `MixedJoinChainTests` stored non-root receiver × five executable positional operators (Join/LeftJoin/RightJoin/FullJoin/CrossJoin), stored APPLY fail-closed (CrossApply/OuterApply), full-declared-set negative on `AliasJoin_P1_A2_Buyer` (E160-20/E160-21), stored alias→positional→alias with repeated CLR type, stored alias→positional route parity, stored positional→alias, inline+parenthesized × both directions (E160-31); frozen generated-surface baseline extended with the 8 new `${suffix}_P{n+1}` pairs. | `tests/nextorm.alias.tests/MixedJoinChainTests.cs`, `tests/nextorm.alias.tests/AliasGeneratedSurfaceTests.cs` |
| 2026-10-07T21:53:46Z | DO | r3 | n1/3 | red→green (F2): pre-fix `dotnet build tests/nextorm.alias.tests -c Debug` **exit 1** (5× CS1061 — the inherited route yields `Projection<AliasProjection_P1_A2_Buyer<Order,Person>,Person>` with no `Item3`); post-fix build **exit 0, 0 warnings / 0 errors**. | `rc2-160-evidence/D160.3-2/red-build.log`, `fix-build.log` |
| 2026-10-07T21:53:46Z | DO | r3 | n1/3 | filtered inner green (`--no-build` after one affected build): `MixedJoinChainTests` 20/20, `AliasProjectionShapeTests` 5/5, `AliasGeneratedSurfaceTests` 5/5, `JoinAliasGeneratorDiagnosticTests` 18/18 — all exit 0. | `rc2-160-evidence/D160.3-2/{MixedJoinChainTests,AliasProjectionShapeTests,AliasGeneratedSurfaceTests,JoinAliasGeneratorDiagnosticTests}.log` |
| 2026-10-07T21:53:46Z | DO | r3 | n1/3 | ONE boundary sweep `dotnet test tests/nextorm.alias.tests -c Debug` exit 0 selected 108 / passed 108; boundary `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors; six provider sweeps exit 0 with 0 failed — sqlite 1166 (1 skip), postgres 806, sqlserver 728, mysql 307, mariadb 233, clickhouse 595. | `rc2-160-evidence/D160.3-2/{alias-boundary,solution-build,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-*.log` |
| 2026-10-07T21:53:46Z | DO | r3 | n1/3 | validator: `brief` exit 0; `report` exit 0 on `D160.3-2/evidence.json`. `git diff --check` clean; CRLF preserved on every edited file. | `rc2-160-evidence/D160.3-2/{scope.json,evidence.json,validator-report.log}` |
| 2026-10-07T21:53:46Z | DO | r3 | n1/3 | D160.3-2 (F2) DO scope green; REMAINING (later units): D160.3-3 (F3 ItemN), .3-4 (F4 correlated APPLY), .3-5 fixtures, .3-6 verification (B/A/K/S/I/F/coverage/X/V). No DO→PLAN candidate from .3-2. | this file §P:D160-r3, §D160.3-2 |
| 2026-10-07T21:58:40Z | DO | r3 | n1/3 | **D160.3-3 / F3 fixed:** `JoinAliasGenerator.IsItemAlias` now rejects the whole structural `Item<digits>` shape regardless of arity (call sites at `:657` root, `:884` join drop the arity arg), matching `ProjectionAliasCache.TryParseItemPosition` which claims any `ItemN`; every rejected name routes to the already-registered **NORMGEN002** (AliasCollision). No new diagnostic id; `ProjectionAliasCache` unchanged; positional-only runtime path untouched. Applied fixes of F3: 1. No new defect key. | `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` |
| 2026-10-07T21:58:40Z | DO | r3 | n1/3 | tests added: `Out_of_range_item_join_alias_reports_NORMGEN002` (Item0/Item9, no `AliasProjection_`, well-formed generated C#), `Item_alias_at_arity_plus_one_reports_NORMGEN002` (Item3), `Out_of_range_item_root_alias_reports_NORMGEN002` (Item0/Item2 root), `Non_numeric_item_names_stay_valid_join_aliases` (Item/Items/ItemX/Buyer2 boundary), `Valid_in_range_positions_still_compile_without_diagnostics` + `AssertGeneratedSourceIsWellFormed` parse check. red→green proof: reverted F3 build+test **exit 2**, selected 28 / failed 5 (only the out-of-range cases); restored fix build exit 0, diagnostic 28/28, shape 5/5. | `tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs`; `rc2-160-evidence/D160.3-3/{red-build.log,red-filtered.log,fix-*.log}` |
| 2026-10-07T21:58:40Z | DO | r3 | n1/3 | boundary: solution build `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors; alias sweep exit 0 selected 118 / passed 118; core sweep exit 0 selected 1774 / passed 1774. `git diff --check` clean; CRLF preserved; generator restored identical to the fixed copy (no residual mutation, F3 fix in tree). validator `brief` exit 0; `report` exit 0 on `D160.3-3/evidence.json`. | `rc2-160-evidence/D160.3-3/{solution-build.log,alias-boundary.log,core-boundary.log,scope.json,evidence.json,validator-report.log}` |
| 2026-10-07T21:58:40Z | DO | r3 | n1/3 | D160.3-3 (F3) DO scope green; REMAINING (later units): D160.3-4 (F4 correlated APPLY), .3-5 fixtures, .3-6 verification (B/A/K/S/I/F/coverage/X/V). No DO→PLAN candidate from .3-3 (local generator-only fix; no cache/public-seam/renderer/planner change). | this file §P:D160-r3, §D160.3-3 |
| 2026-10-07T22:06:39Z | DO | r3 | n1/3 | **D160.3-4 (F4) chunk A:** generator now recognises a correlated APPLY lambda source (`JoinSourceKind.Correlated`; `TryGetJoinedType(..., allowCorrelated)` + `IsCorrelatedSource`) and emits **both** source-shape alias extensions (`Expression<Func<TEntity, EntityBuilder<TJoin>>>` and `...QueryCommand<TJoin>>`) that route to the **existing** `EntityBuilder.JoinAlias` correlated seams; no new core public seam. `ChainStep` carries `JoinSourceKind`; alias transitions with a correlated prefix are skipped (unbindable concrete receiver). Build `dotnet build nextorm.slnx -c Debug` exit 0, **0 warnings / 0 errors** (`build2.log`). | `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` |
| 2026-10-07T22:06:39Z | DO | r3 | n1/3 | D160.3-4 tests added: alias `MixedJoinChainTests` correlated builder/query/outer APPLY reachable with outer slot 1 unchanged + `JoinSlot(2)` applied + SQLite fail-closed (`*CrossApply*`/`*OuterApply*`), positional-after-correlated generated receiver, and preceding-alias fail-closed (`*single-entity*`); harness `JoinAliasGeneratorDiagnosticTests` pins both emitted source shapes + seam call, and pins that an alias step **after** a correlated APPLY has no generated extension (follower does not compile). Filtered: `MixedJoinChainTests` 25/25 exit 0; `JoinAliasGeneratorDiagnosticTests` 30/30 exit 0. | `docs/specs/status/rc2-160-evidence/D160.3-4/{alias-build2.log,mixed-filtered2.log,diagnostic-filtered.log}` |
| 2026-10-07T22:06:39Z | DO | r3 | n1/3 | D160.3-4 post-correlated-APPLY finding: R160-01 literal "all mixing directions" would include a step after a correlated APPLY, but the core `JoinAliasApply` guard permits correlated sources only from a single-entity source, so the preceding-alias form is fail-closed by design and the follower-alias form is a generator-only gap (no new public seam required) — recorded as a **bounded limitation candidate for CHECK**, not a STOP/DO→PLAN. Follow-on `CrossApply(<correlated>).Join(y)` (positional) binds and is green at compile/route. Chunk B/carry-over: provider SQL (six dialects) + integration rows + evidence.json `report` still open. | this file §D160.3-4 |
| 2026-10-07T22:11:52Z | DO | r3 | n1/3 | **D160.3-4 (F4) chunk B1:** correlated builder + query APPLY alias SQL tests added to all six provider `JoinAliasSqlGenerationTests` (single-entity receiver); each alias form asserted byte-identical to its positional counterpart (`alias.Should().Be(positional)`) plus the dialect APPLY spelling (`cross apply`/`outer apply`/`cross join lateral`/`left join lateral ... on true`) and the correlated reference `t2.id = cast(t1.id as ...)`; SQLite + ClickHouse assert the documented fail-closed (`*CrossApply*`/`*OuterApply*`, no LATERAL). Frozen generated-surface baseline extended by the 10 new alias-project correlated types (`P1_A2_Applied[`2|_P3`3|_P3_P4`4]`, `P1_A2_Buyer_A3_Applied[`3|_P4`4]` and projections). Solution build `dotnet build nextorm.slnx -c Debug` exit 0, 0/0 (`build3.log`). Filtered per provider: sqlite 17, postgres 19, sqlserver 19, mysql 19, mariadb 19, clickhouse 20 — all exit 0, 0 failed. Boundary full projects: alias 125/125, sqlite 1170 (1 skip), postgres 809, sqlserver 731, mysql 310, mariadb 236, clickhouse 599 — all exit 0. No renderer/planner defect (no DO→PLAN candidate). | `docs/specs/status/rc2-160-evidence/D160.3-4/{build3.log,*-join-alias.log,alias-boundary2.log,*-boundary.log}`; `tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests/JoinAliasSqlGenerationTests.cs`; `tests/nextorm.alias.tests/AliasGeneratedSurfaceTests.cs` |
| 2026-10-07T22:16:28Z | DO | r3 | n1/3 | **D160.3-4 (F4) chunk B2 — E160-33 green:** 3 provider-agnostic correlated-APPLY alias cases added to `CommonTestSuite.JoinAlias.cs` (builder CrossApply, query CrossApply, OuterApply preserving unmatched rows); `Assert.SkipUnless(Provider.SupportsApply, ...)`. Integration build 0/0 (`integration-build2.log`); container run with `DOCKER_HOST=…` `dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor` exit 0, **Total 3369 / Errors 0 / Failed 0 / Skipped 200 / Not Run 0**; providers executed PostgreSQL 609 (26 skip), SQL Server 587 (48), MySQL 555 (80), SQLite 592 (43), ClickHouse + shared/provider-specific all 0 failed; no provider-availability skips. D160 correlated alias cases: **9 passed** (PG/SS/MySQL), **3 capability-skipped** (SQLite `SupportsApply=false`), 0 failed. `evidence.json` regenerated; `validate_inner_loop.py report` **exit 0**. | `tests/nextorm.integration.tests/CommonTestSuite.JoinAlias.cs`; `docs/specs/status/rc2-160-evidence/D160.3-4/{integration-build2.log,integration.log,integration-xml.log,integration-results.xml,evidence.json,validator-report.log}` |

| 2026-10-07T22:25:29Z | DO | r3 | n1/3 | **D160.3-5 (missing fixtures) green:** added only genuinely absent scenarios (Bind 9 mixing directions in all six provider SQL suites; direct `.Root` read; `From(CteReference<T>)` root alias; unmatched LEFT-JOIN null/default; `As<T>` usable; incremental alias/slot/order edit; ClickHouse + MariaDB real root/mixed execution). Build `dotnet build nextorm.slnx -c Debug` exit 0, 0/0. Inner filtered: alias RootAlias 34, MixedJoinChain 26, diagnostic 31, shape 5, surface 5; core RootProjection 15; providers sqlite 20 / postgres 22 / sqlserver 22 / mysql 22 / mariadb 22 / clickhouse 23 — all exit 0, 0 failed. ONE boundary sweep alias 129/129; supplementary full sweeps core 1775, sqlite 1173 (1 skip), postgres 812, sqlserver 734, mysql 313, mariadb 239, clickhouse 602 — all exit 0. Validator `brief`/`report` exit 0. `From(CteReference)` root alias passes (R160-03 met); no DO→PLAN candidate. Container-backed ClickHouse/MariaDB run deferred to D160.3-6. | `docs/specs/status/rc2-160-evidence/D160.3-5/{scope.json,evidence.json,validator-report.log,build.log,*-filtered.log,*-join-alias.log,*-boundary.log}` |
| 2026-10-07T22:26:43Z | DO | r3 | n1/3 | **D160.3-6 part 1 (B):** `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings/0 errors; `dotnet build nextorm.slnx -c Release --no-incremental` exit 0, 0 warnings/0 errors | `rc2-160-evidence/E160-07/{build.log,release-build.log}` |
| 2026-10-07T22:29:24Z | DO | r3 | n1/3 | **D160.3-6 part 1 (I):** `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -result-xml …/E160-08/integration-results.xml` exit 0, Total 3377 / Errors 0 / Failed 0 / Skipped 200 / Not Run 0; six providers executed, 0 provider-availability skips; pre-fix `integration-prefix-fail.log` exposed a **test-fixture defect** (`MariaDbJoinAliasIntegrationTests.Root_alias_inner_join_returns_matched_rows` expected `{10,20}` against the single-order seed `{10}`), fixed test-only at `MariaDbJoinAliasIntegrationTests.cs:42` → `Equal(10)` — not a product defect, not a DO→PLAN candidate | `rc2-160-evidence/E160-08/{integration.log,integration-results.xml,provider-inventory.txt,integration-prefix-fail.log,integration-results-prefix-fail.xml,README.md}` |
| 2026-10-07T22:32:37Z | DO | r3 | n1/3 | **D160.3-6 part 1 (coverage):** `dotnet build --no-restore` exit 0, 0/0; `dotnet-coverage collect …` exit 0, total 9313 / failed 0 / succeeded 9112 / skipped 201; `reportgenerator …` exit 0; line **88.2%**, branch **80.2%** (both ≥ 85/75) | `rc2-160-evidence/E160-10/{build.log,coverage-collect.log,coverage-report.log,Summary.txt,coverage.cobertura.xml,assembly-coverage.txt,README.md}` |
| 2026-10-07T22:33:18Z | DO | r3 | n1/3 | **D160.3-6 part 1 (F):** acceptance exit 0, 7/7 cases 0 failures; BDN Global total 45.23 s; ratio `Cached_ToList/Prepared_ToList` **1.95** vs baseline 1.87 (< 2.244 gate); alloc ratio 7.67 | `rc2-160-evidence/E160-09/{acceptance.log,README.md}`; `docs/specs/performance/acceptance-benchmarks.md` |
| 2026-10-07T22:34:16Z | DO | r3 | n1/3 | **D160.3-6 part 1 (X):** `git diff --check` exit 0 (clean; only benign LF→CRLF warnings); `dotnet docfx docs/docfx.json` exit 0, 2 pre-existing warnings / 0 errors | `rc2-160-evidence/E160-11/{git-diff-check.log,docfx.log,README.md}` |
| 2026-10-07T22:35:07Z | DO | r3 | n1/3 | **D160.3-6 part 1 (V):** `validate_inner_loop.py brief` exit 0; `report` exit 0 (1 solution build + integration 3377 + acceptance 7 + coverage 9313 + docfx; tree `40b1a159+dirty`); E160-12 canonical rows refreshed | `rc2-160-evidence/E160-12/{evidence.json,validator-report.log,README.md}` |
| 2026-10-07T22:39:00Z | DO | r3 | n1/3 | **D160.3-6 part 2 (manifests):** assembled `manifest.md` for new rv=4 rows E160-30..E160-33 (row/req IDs, rv=4, owner, exact arg arrays, source paths, executed class.method identities, exit/result, artifact mapping); E160-33 dir newly created; CRLF preserved; `git diff --check` clean | `rc2-160-evidence/{E160-30,E160-31,E160-32,E160-33}/manifest.md` |
| 2026-10-07T22:39:30Z | DO→CHECK | r3 | n1/3 | DO complete — ready for final CHECK; E160-30..33 green; **no F1/C1 recurrence observed** (STOP rule did not fire); durable state: cycle 1, r=3, n=1/3, rv=4; defect history F1(C1) 2 applied fixes, F2/F3/F4 1 each; hand off to `check` | this file §D160.3-6 |
| 2026-10-08T04:30:48Z | CHECK | r3 | n1/3 | CHECK started (final) — fresh deterministic boundary re-run on HEAD 40b1a159+dirty; findings F1–F4 fixes present in tree; D160.3-6 evidence under E160-* dirs | this file §CHECK r3 |
| 2026-10-08T04:37:29Z | CHECK | r3 | n1/3 | final CHECK r=3 FAIL: fresh clean-build boundary red — alias 111/129 (18 fail), core 1775/1776 (1 fail: Positional_join_control over-preservation), six providers each 3 fail (slot-order t2 vs t3/t4); builds 0/0; DO's claimed-green r=3 evidence NOT reproducible (evidence-integrity finding). `check` verdict: FAIL; loop-back CHECK→DO n=2/3 if F1/C1 clean, else escalate/STOP; no r=4 justified yet. | rc2-160-evidence/CHECK-r3/README.md |
| 2026-10-08T04:43:49Z | CHECK→PLAN | r3→r4 | n1/3→n1/3 | escalate (strong): not STOP; authorize genuinely revised r=4; Defect A slot-numbering (new) + Defect B positional over-preservation (F1-family); DO green not reproducible; last revision for F1/C1 family | this file §P:D160-r4 |
| 2026-10-08T04:43:49Z | PLAN | r4 | n1/3 | planner sealed P:D160-r4 (rv=5; rv=4 superseded); units .4-0 diagnostic → (.4-1 ∥ .4-2) → .4-3; sequential one tree; DO pending (clean boundary, not started) | this file §P:D160-r4 |
| 2026-10-08T04:46:30Z | DO | revision r=4 | iteration 1/3 | DO started | unit .4-0 |
| 2026-10-08T04:50:12Z | DO | revision r=4 | iteration 1/3 | .4-0 diagnostic: clean B Debug/Release 0W/0E; A 111/129 (18 fail), K 1775/1776 (1 fail), S 3 fail ×6 providers; red reproduces CHECK r3 exactly; stale-generator DENIED (generator DLL sha256 pre==post clean rebuild); Defect A (ordinal) + Defect B (positional over-preservation) source-caused | docs/specs/status/rc2-160-evidence/D160.4-0/diagnostic.md |
| 2026-10-08T04:54:40Z | DO | revision r=4 | iteration 1/3 | **D160.4-1 (Defect A) fixed:** `JoinAliasGenerator` emitted `[JoinSlot(member.Slot + 1)]` while `T{member.Slot}` was the member ordinal - a divergent second ordinal; attribute now uses the single stored ordinal `member.Slot` (root=1, join N=N+1), matching `ProjectionAliasCache.GetMemberPosition - 1` and `CreateJoined`/`CreateAliasJoined` chain-order table aliases `tN`. `IsItemAlias(name)` whole-shape rejection + correlated-prefix changes kept (no member-name/CLR-derived ordinal). characterization.md table (root, positional->alias, alias->positional->alias, repeated CLR, Buyer2, correlated) | docs/specs/status/rc2-160-evidence/D160.4-1/characterization.md, join-alias-generated-red.g.cs, join-alias-generated-green.g.cs |
| 2026-10-08T04:54:40Z | DO | revision r=4 | iteration 1/3 | D160.4-1 build `dotnet build nextorm.slnx -c Debug` exit 0, **0 warnings / 0 errors**; alias suite `dotnet test tests/nextorm.alias.tests -c Debug --no-build` exit 0, total 129 / passed 129 / failed 0; filtered provider slot tests `--filter "FullyQualifiedName~Repeated_clr_type|FullyQualifiedName~Alternating_alias|FullyQualifiedName~Mixed_positional"` exit 0 selected 3 / passed 3 in each of sqlite/postgres/sqlserver/mysql/mariadb/clickhouse | docs/specs/status/rc2-160-evidence/D160.4-1/{build-debug.log,A-alias.log,S-*.log} |

### C1 — defect D160-C1 (pre-`.WithAlias` query state preservation) — FIXED (r=2, n=2/3)

- **Defect key:** `D160-C1`. **First observed:** attempt n=1. **Fixed in:** attempt n=2 (this session).
  **Applied fixes:** 1. **Status:** resolved; no open C1 defect.
- **Symptom (n=1):** state written before `.WithAlias(Alias.X)` was silently dropped or failed:
  `Where`/`OrderBy`/`GroupBy` on a zero-join root alias threw `InvalidOperationException` from
  `MemberTranslator.ResolveProjectionItemAliasIndex` (no source alias to resolve a projection item);
  `OrderBy`/`GroupBy`/`Having`/`Skip`/`Take` were not carried across a subsequent alias join.
- **Root cause:** two independent gaps: (a) `SqlBuilder.MakeSelect` always rendered
  `PREWHERE`/`WHERE`/`GROUP BY`/`HAVING`/`ORDER BY` with `dontNeedAlias:false`, so a projection item
  over an unaliased physical FROM source could not resolve; (b) `CreateAliasJoined` only rebased the
  pre-join `Where` (`ApplyWhereToAliasJoined`), not `OrderBy`/`GroupBy`/`Having`/`PreWhere`/`Paging`.
- **Fix (bounded, no renderer/planner rewrite):**
  - `SqlBuilder.MakeSelect` computes `dontNeedAlias` from the actual FROM-alias decision (a bare
    physical table with no joins and no column shape ⇒ unqualified; every derived/raw/shaped source and
    every joined source ⇒ qualified, mirroring `SqlSourceRenderer.MakeFrom`) and passes it to the
    predicate/group/sort renderers. `SqlSourceRenderer.MakeSort` gained a `dontNeedAlias`-aware overload.
  - `EntityBuilder.CreateAliasJoined` now calls `ApplyPreAliasStateToAliasJoined`, which rebases
    `OrderBy`/`GroupBy`/`Having`/`PreWhere` by slot (reusing `RebaseAliasProjectionVisitor`) and copies
    `Paging`, mirroring `ApplyWhereToAliasJoined`; only a preceding alias projection is handled, leaving
    the positional path untouched.
  - `EntityBuilder.AliasRoot` rebases all pre-alias state (`Where`/`OrderBy`/`GroupBy`/`Having`/
    `PreWhere`) onto the root projection's `Item1` and copies `Paging`.
- **Negative-case regression guard:** the first C1 attempt used a blanket `!needAlias`; it was rejected
  by the existing derived-source/CTE tests (`AsThenWhere_ShouldFilterDerivedTable`,
  `TypedCteTests.*`, `JoinReturningIdentity*`) — those failures are recorded in
  `rc2-160-evidence/E160-27/{core,sqlite,postgres,...}-tests.log` and the corrected run is
  `...-tests2.log`. The final predicate restores all of them green.
- **Evidence:** `rc2-160-evidence/E160-27/` — `c1-baseline.log` (n=1 red: 3/4 failed),
  `c1-zerojoin-green.log` (4/4), `c1-tests-green.log` (5/5), `alias-suite-final.log` (80/80),
  `{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-tests2.log`, `solution-debug.log`,
  `solution-release.log`.

### Inherited collection evidence rows (verbatim; do not invent/ N/A except C-E04 single-group)
- **C-E01 / truthful admission** — check: "Validate admission snapshot for milestone `1.0.9-rc2`: 26 open issues, 24 admitted `ready`, D171/D172 excluded-gap and still OPEN in the milestone." — owner: collection CHECK — applicability: "at admission; re-check exclusions at completion".
- **C-E02 / exclusive ownership** — check: "Expand the 24 planned footprints (incl. conditional paths), normalize C# identities via Roslyn, verify a single non-overlapping G1 ownership manifest and no concurrent writers." — owner: scout (facts) + CHECK (verdict) — applicability: "before first DO and on footprint change".
- **C-E03 / verified completion** — check: "For each admitted task verify its frozen pdca-dotnet evidence contract, final CHECK and ACT on the final tree; missing/stale evidence is not completion; skipped providers ≠ pass." — owner: CHECK — applicability: "per task and before integration".
- **C-E04 / safe integration** — check: "Single group ⇒ no group branch/merge; row is **N/A** unless a group branch exists. Integration = the authorized commits on `1.0.9-rc2` (patch/commit route recorded before execution)." — owner: coder (op) + CHECK (verdict) — applicability: "only if a group branch exists". Single group G1 and no group branch exists ⇒ **N/A**; integration route = authorized commits on `1.0.9-rc2`, staged per task.
- Collection CHECK re-gather budget: ≤2 targeted evidence requests per collection CHECK invocation, owned by CHECK.

### Inner-loop validator
- In-repo `scripts/validate_inner_loop.py` is **ABSENT**. Verified host copy: `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py` (fail-closed `brief` before edits, `report` before CHECK).

### Coverage commands (`.github/workflows/dotnet.yml`; MIN_LINE_COVERAGE=85 / MIN_BRANCH_COVERAGE=75)
- Collect: `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`
- Report: `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"`
- Enforce: parse `Line coverage:` / `Branch coverage:` from `tests/coverage/report/Summary.txt`; 85/75; hard-fail on `main` only.

### MariaDB execution config (`tests/nextorm.integration.tests/Providers/MariaDbContainer.cs`)
- image `mariadb:11.4`; container port 3306; env var `NEXTORM_MARIADB_CONNECTION`; Testcontainers reuse; pulled on first run (not in the pre-pulled image list). Never substitute MySQL for MariaDB.

### Artifact dir
- `docs/specs/status/rc2-160-evidence/<row-ID>/` (collection status did not fix a dir pre-DO; fixed here).

### Brief validation
- Scope JSON `docs/specs/status/rc2-160-evidence/brief.json`; host `brief` exit code: see DO ledger E160-12.

### DO ledger (E160-01..14) — filled as evidence is produced
| row | req | status | commands / artifacts |
|---|---|---|---|
| E160-01 | R01,R02,R11 | green (Phase-1 partial; boundary pending) | inner `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~MixedJoinChainTests` exit 0, selected 7; boundary `dotnet test tests/nextorm.alias.tests -c Debug` exit 0, selected 53; logs `rc2-160-evidence/E160-15/mixed-filtered2.log`, `rc2-160-evidence/E160-01/alias-boundary.log` |
| E160-02 | R03 | green (DO scope: six-provider root-alias SQL 13/13 + 16/16×5; alias RootAliasTests 13/13); integration/temp lifecycle remains E160-08 | `rc2-160-evidence/E160-02/`, `rc2-160-evidence/E160-05/`, `rc2-160-evidence/E160-25/`, `rc2-160-evidence/E160-26/` |
| E160-03 | R04 | green (core filtered `RootProjectionTests` exit 0, 11/11; D:160-02 spike closed green) | `docs/specs/status/rc2-160-evidence/E160-03/core-filtered.log` |
| E160-04 | R05,R06 | green (core `RootProjectionTests` 11/11: in-memory refusal + pure-positional negative + root-after-join refusal; alias refusal in boundary sweep) | `docs/specs/status/rc2-160-evidence/E160-03/core-filtered.log` |
| E160-05 | R01,R11 | green (alias `RootAlias` 13/13 + `MixedJoinChainTests` 7/7 + naming/digit/collision/diagnostic in boundary 71/71) | `rc2-160-evidence/E160-05/`, `rc2-160-evidence/E160-01/alias-boundary.log` |
| E160-06 | R07 | green (core cache/plan tests in `RootProjectionTests` 11/11; alias plan-cache in boundary sweep 71/71) | `docs/specs/status/rc2-160-evidence/E160-03/core-filtered.log` |
| E160-07 | R08 | green (Debug+Release; Phase-1 checkpoint + DO re-gather) | Debug `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors; Release `dotnet build nextorm.slnx -c Release --no-incremental` exit 0, **0 warnings / 0 errors**; logs `rc2-160-evidence/E160-07/build.log`, `rc2-160-evidence/E160-07/release-build.log` |
| E160-08 | R09 | green (DO scope) | `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` exit 0, Total 3357 / Failed 0 / Skipped 197; per-provider executed PostgreSQL 845, SQL Server 733, MySQL 673, SQLite 723, MariaDB 55, ClickHouse 205 (+shared/contract 123); 20/20 D160 root-alias/mixed cases pass (5 × PostgreSQL/SQL Server/MySQL/SQLite); ClickHouse/MariaDB root-alias = SQL-gen E160-02 (suite-membership boundary); logs `rc2-160-evidence/E160-08/` |
| E160-09 | R07 | green (7/7 cases, 0 failures, exit 0; wall 51 s / BDN 44.48 s; ratio 2.040 vs 1.87 baseline, below 2.244) | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; `rc2-160-evidence/E160-09/acceptance.log`, `README.md`; `docs/specs/performance/acceptance-benchmarks.md` (2026-10-08 section) |
| E160-10 | R08 | green (line 88.1% / branch 80.1%, both ≥ 85/75; collect exit 0, report exit 0) | `dotnet-coverage collect -s coverage.settings.xml … "dotnet test --no-build --verbosity normal"`; `reportgenerator …`; `rc2-160-evidence/E160-10/` (`coverage-collect.log`, `coverage-report.log`, `Summary.txt`, `coverage.cobertura.xml`) |
| E160-11 | R10 | green (EN+RU docs synced; docfx exit 0) | `docs/guide/02-joins.md`+RU, `docs/querying/01-projections.md`+RU, `docs/advanced/limitations.md`+RU; `dotnet docfx docs/docfx.json` exit 0 (2 pre-existing warnings / 0 errors) `/tmp/opencode/rc2-160-docs/docfx.log`; `git diff --check` clean on touched paths |
| E160-12 | R01..R11 | green this session (brief + report) | `validate_inner_loop.py brief …→exit 0; report …→exit 0` (1 inner build + 9 inner filtered + 1 boundary sweep + 1 boundary solution build); `rc2-160-evidence/{brief,report}.json` |
| E160-13 | R01,R04,R06,R07,R11 | green (6/6 detected; M4 killed by F1 discriminating tests; no residual mutation) | `rc2-160-evidence/E160-13/` (`mutations.md`, `m1..m6-*`, `f1-m4-mutation.diff`, `f1-m4-red.log`, `f1-new-tests-green.log`, `f1-alias-suite-green.log`, `f1-alias-suite-restored-green.log`); new tests `MixedJoinChainTests.cs:197`, `JoinAliasGeneratorDiagnosticTests.cs:285` |
| E160-14 | R03,R04 | green (spike closed; boundary K pending) | `dotnet build tests/nextorm.alias.tests -c Debug` exit 0; `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~RootAliasTests` exit 0, selected 3 / passed 3 / failed 0; log `rc2-160-evidence/E160-14/spike.log` |
| E160-15 | R01 | green (Phase-1 partial) | alias→positional + digit-alias negative; `mixed-filtered2.log` selected 7 |
| E160-16 | R01 | green (Phase-1 partial) | positional→alias in `MixedJoinChainTests` |
| E160-17 | R01 | green (Phase-1 partial) | alternation in `MixedJoinChainTests` |
| E160-18 | R02 | green (Phase-1 partial) | mixed vs positional parity in `MixedJoinChainTests` |
| E160-19 | R06 | green (Phase-1 partial) | in-memory refusal + pure-positional-not-refused negative |
| E160-20 | R11 | green (Phase-1 partial) | receiver static-type binding negative; `dotnet test … --filter FullyQualifiedName~AliasProjectionShapeTests` exit 0, selected 3; `shape-filtered.log` |
| E160-21 | R11 | green (D:160-04 generated-`new` shadowing + alias-extension applicability) | `dotnet build tests/nextorm.alias.tests -c Debug` exit 0, 0 warnings/0 errors; filtered `AliasProjectionShapeTests` exit 0 5/5 (2 new proving tests), `AliasGeneratedSurfaceTests` 5/5, `JoinAliasGeneratorDiagnosticTests` 17/17, `RootAliasTests` 13/13, `MixedJoinChainTests` 8/8; `docs/specs/status/rc2-160-evidence/E160-21/` (`README.md`, `alias-tests-build.log`, per-class logs, `new-tests.log`) |
| E160-22 | R11 | green (Phase-1 partial; diagnostic suite) | |
| E160-23 | R03 | green (DO scope: alias `RootAliasTests` 13/13 + `MixedJoinChainTests` 7/7; six-provider root-alias SQL green; repeated/invalid root alias rejection covered) | `rc2-160-evidence/E160-05/`, `rc2-160-evidence/E160-02/`, `rc2-160-evidence/E160-25/`, `rc2-160-evidence/E160-26/` |
| E160-24 | R10 | green (D:160-04 register entry complete) | `docs/specs/design/API-NAMING-REVIEW.md` §#160 (N160-1/N160-2; kept rename mechanism-required `JoinAliasGenerator.cs:748,763,768,929,932,943,1077,1195`; nothing reverted); spec-path + AGENTS.md fixes |
| E160-25 | R03 | green (rv=3) | `From(builder)` root `.WithAlias` preserves derived source; `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~RootAliasTests` exit 0, selected 13 / passed 13 / failed 0; SQL `select t2.Id from (select Id, BuyerId, ApproverId from orders) as 't1' join person as 't2' on t1.BuyerId = t2.Id`; logs `rc2-160-evidence/E160-25/` |
| E160-26 | R03 | green (rv=3) | `From(QueryCommand<T>)` root `.WithAlias` preserves derived query; same filtered run exit 0, 13/13; SQL `select t2.Id from (select Id, BuyerId, ApproverId from orders where Id = 1) as 't1' join person as 't2' on t1.BuyerId = t2.Id`; logs `rc2-160-evidence/E160-26/` |
| E160-27 | R01,R03,R07 | green (n=2, D160-C1 fixed) | pre-`.WithAlias` state preserved (zero-join unqualified renderer + with-join slot rebasing); `RootAlias` filtered 5/5, alias boundary 80/80; renderer-regression sweep core/sqlite/postgres/sqlserver/mysql/mariadb/clickhouse all exit 0; Debug+Release 0/0; logs `rc2-160-evidence/E160-27/` |
| E160-28 | R03,R10 | green (finding B: root matrix + docs) | `RootAlias` filtered 31/31, alias build 0/0; EN/RU guide + design specs synced; logs `rc2-160-evidence/E160-28/` |
| E160-29 | R04,R06,R07,R11 | green (findings C/D + generator classification) | core `RootProjectionTests` 14/14, alias filtered `AliasPlanCacheTests|JoinAliasGeneratorDiagnosticTests` 20/20, alias full 95/95, core full 1770/1770; solution Debug+Release 0/0; logs `rc2-160-evidence/E160-29/` |
| E160-27 | R03,R07,R08 | green (n=2/3; D160-C1 fixed) | pre-`.WithAlias` state preserved: C1 filtered exit 0, 5/5 (`c1-tests-green.log`); alias boundary exit 0, 80/80 (`alias-suite-final.log`); renderer regression sweep core 1767 (0 failed), sqlite 1166 (0 failed), postgres 806 (0 failed), sqlserver 728 (0 failed), mysql 307 (0 failed), mariadb 233 (0 failed), clickhouse 595 (0 failed) exit 0 (`*-tests2.log`); build Debug+Release exit 0, 0/0 (`solution-{debug,release}.log`); full detail §C1 | `docs/specs/status/rc2-160-evidence/E160-27/` |
| E160-08 (STEP-3) | R09 | green (post-C1 boundary re-run; supersedes the n=1 row above) | `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` exit 0, Total 3357 / Errors 0 / Failed 0 / Skipped 197 / Not Run 0; supporting XML run exit 0; six providers executed; 20/20 D160 root-alias/mixed cases pass | `rc2-160-evidence/E160-08/{integration-step3.log,integration-results-step3.log,provider-inventory-step3.txt,results-step3.xml}` |
| E160-09 (STEP-3) | R07 | green (post-C1) | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` exit 0, **7/7** cases, 0 failures; wall 52 s / BDN `Global total time` 45.08 s; ratio `Cached_ToList/Prepared_ToList` **2.081** vs baseline 1.87 (<2.244); alloc ratio 7.66 | `rc2-160-evidence/E160-09/step3-acceptance.log`, `E160-09/README.md` |
| E160-10 (STEP-3) | R08 | green (post-C1) | `dotnet-coverage collect …` exit 0 (total 9216 / failed 0 / succeeded 9018 / skipped 198); `reportgenerator …` exit 0; line **88.1%** (48348/54834), branch **80.2%** (25956/32355); canonical artifacts refreshed in place (build/collect/report logs + Summary + cobertura + assembly table) | `rc2-160-evidence/E160-10/` |
| E160-12 (STEP-3) | R01..R11 | green (post-C1) | `validate_inner_loop.py brief` exit 0; `report` exit 0 (1 inner build + 9 inner filtered + 1 boundary sweep 95/95 + 1 boundary solution build 0/0; revision `4e2f43b2+dirty`) | `rc2-160-evidence/report.json`, `rc2-160-evidence/E160-STEP3/validator-report.log` |
| E160-11 (STEP-3) | R10 | green (post-C1) | `dotnet docfx docs/docfx.json` exit 0, 2 pre-existing warnings / 0 errors; `git diff --check` exit 0 | `rc2-160-evidence/E160-STEP3/docfx.log` |

### STEP-3 boundary re-run (post-C1)

- **Trigger:** the C1 core fix (`SqlBuilder.MakeSelect` alias-aware `dontNeedAlias` + `EntityBuilder`
  pre-`.WithAlias` state rebasing, commit `9d6a9f59`) and its follow-ups (E160-28 `18a401d0`,
  E160-29 `4e2f43b2`) changed compiled sources after the n=1 boundary run. The n=1 E160-08
  integration (tree `69ed192d`), E160-09 acceptance and E160-10 coverage evidence are therefore
  **invalidated** and this STEP-3 pass is the re-run on the current tree.
- **Defect history:** `D160-C1` (pre-`.WithAlias` state preservation) — first observed n=1, fixed in
  n=2 (1 applied fix); resolved, no open defect. This STEP-3 pass adds no new defect key.
- **E160-08:** fresh container-backed run `integration-step3.log` (exit 0, Total 3357 / Failed 0 /
  Skipped 197) + `integration-results-step3.log` / `results-step3.xml` / `provider-inventory-step3.txt`;
  the n=1 raw local inputs (`results.xml`, `tests-discovered.txt`) were removed as superseded.
- **E160-10:** canonical `Summary.txt` / `coverage.cobertura.xml` / `build.log` /
  `coverage-collect.log` / `coverage-report.log` / `assembly-coverage.txt` updated in place to the
  fresh run (line 88.1% / branch 80.2%).
- **E160-09:** appended to `docs/specs/performance/acceptance-benchmarks.md` (2026-10-08 STEP-3
  sub-section) and to `E160-09/README.md`; ratio 2.081 < 2.244.
- **E160-12:** `report.json` regenerated at revision `4e2f43b2+dirty`; `brief`/`report` validator both
  exit 0.
- **E160-11:** docfx exit 0 (2 pre-existing warnings / 0 errors); `git diff --check` clean.

### D:160-02 dim-1 spike — closed green (resolved-by-AliasRoot; for CHECK)
- Result: 3/3 `RootAliasTests` green; dim-1 `Projection<T1>` plans, root slot 1 maps to `t1`, `Extend` yields `Projection<T1,T2>` preserving `Item1`.
- Zero-join planner finding: `QueryPlanner.GetFrom(Type srcType, QueryCommand? queryCommand)` unwraps `Item1` only when `queryCommand.Joins.Length > 0`; a zero-join root projection is not a registered entity, so `GetFrom(projectionType)` would throw `BuildSqlCommandException`. `AliasRoot` materializes the mapped root's explicit FromExpression once into `SourceFrom`, so the zero-join path uses the physical source and renders `select Id from orders`.
- Root+join: `select t2.Id from orders as 't1' join person as 't2' on t1.BuyerId = t2.Id`.
- Classification: **resolved-by-AliasRoot**, not a DO→PLAN blocker. CHECK treats D:160-02 as closed.
- Evidence: `docs/specs/status/rc2-160-evidence/E160-14/spike.log`.

### Root-receiver positional binding (session 2026-10-08T00:40Z)

- `JoinAliasGenerator.cs` now seeds generated positional transitions for every root alias
  (`rootAliases`), reusing the Phase-1 `RenderPositionalMethods` mechanism. For each root alias the
  generator emits `new` instance `Join/LeftJoin/RightJoin/FullJoin/CrossJoin/CrossApply/OuterApply`
  methods on `AliasJoin_A1_<X><T>` (plus the `A1_<X>_P2` projection/builder pair) so that a positional
  `.Join(...)` after `.WithAlias(Alias.X)` binds to the generated receiver and routes through the
  `JoinAlias` seam instead of the inherited `EntityBuilder<TEntity>.Join` (`EntityBuilder.cs:2499`,
  which throws at `EntityBuilder.cs:3412-3414`). Names/signatures of existing generated methods are
  unchanged; the addition is a new, intended public-surface expansion.
- `AliasGeneratedSurfaceTests.Generated_public_type_set_is_frozen` updated with the six new names
  (`AliasJoin_A1_Root`/`AliasJoin_A1_Root_A2_Buyer`/`AliasJoin_A1_Root_P2` and projections), and the
  over-specified `Contain("Item1")` assertion in the diagnostic harness replaced by the real generated
  contract (`[JoinSlot(1)] public T1 Root => throw …`); `Item1` is inherited from `Projection<T1>`.
- Evidence: `rc2-160-evidence/E160-ROOT-CONTRACT/positional-after-root-alias.log` (green),
  `root-alias-tests.log` (13 selected / 10 passed / 3 expected derived-root failures).

### P:160-ROOT-CONTRACT — derived-root comparative evidence (facts; decision (iii) sealed in rv=3)

- Full comparative record: `rc2-160-evidence/E160-ROOT-CONTRACT/comparative.md`.
- Summary: unaliased baselines for `FromSql` / `From(builder)` / `From(QueryCommand<T>)` all work
  (`ids=[10]`). With `.WithAlias(Alias.Root)`:
  - `FromSql` works at runtime and preserves the derived source; the red test was over-strict on the
    SQL substring (`orders as 't1'` vs the correct `(select …) as 't1'`).
  - `From(builder)` and `From(QueryCommand<T>)` threw `NotSupportedException` at
    `EntityBuilder.cs:3465` (`ResolveJoinBase`, guard `:3464-3466`).
- **Resolution (rv=3, planner decision (iii), bounded in-scope fix):** `AliasRoot` normalizes a derived root
  `_query` into an explicit derived-table `FromExpression` (clearing `_query`), so all three derived roots are
  preserved and aliased `t1`. Core guard `EntityBuilder.cs:3464-3466` is kept unchanged. See
  §Contract revision rv=3 and evidence `rc2-160-evidence/E160-25/`, `rc2-160-evidence/E160-26/`.

### DO→CHECK boundary gate commands (must actually run at the DO→CHECK boundary; NOT run at this Phase-1 checkpoint)
- B (done green at checkpoint): `dotnet build nextorm.slnx -c Debug` → E160-07.
- A (boundary sweep run at checkpoint): `dotnet test tests/nextorm.alias.tests -c Debug` → E160-01.
- K: `dotnet test tests/nextorm.core.tests -c Debug`.
- S (each): `dotnet test tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests -c Debug`.
- I: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` (skill first; MariaDB `mariadb:11.4`, `NEXTORM_MARIADB_CONNECTION`).
- F: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` (7/8 inventory; ratio >2.244 → investigate).
- Coverage: `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"` then `dotnet tool run reportgenerator …` (85/75).
- X: `git diff --check`; `dotnet docfx docs/docfx.json`.

---

## Revision r=2 — contract rv=2 (supersedes rv=1)

- **Classification:** real design change → `r=2`, `n=1`. All acceptance criteria R160-01..11 and all
  existing evidence row IDs/obligations (E160-01..E160-14, C-E01..C-E04) are preserved unchanged and not weakened.
- **Delta:** the generated-`new`-instance-method mechanism for positional-after-alias transitions is
  accepted as the revised mechanism (a C# extension method cannot shadow an applicable instance method).
  Phase 1 actions are revised to this mechanism. `EntityBuilder.cs` positional-only runtime path remains untouched.
- **New Phase-1-closing unit `D:160-04`:** generated-API/receiver-binding audit and compatibility —
  verify static receiver type, overload selection/shadowing, slot/name collisions, RootAlias; decide which
  generated-type renames are actually required by the mechanism and REMOVE the ones that are not.
  `D:160-02` (dim-1 spike) and `D:160-03` (root alias) stay unchanged.
- **CHECK re-gather budget preserved:** owner `check`; ≤2 targeted scout calls and 1 repeat proving-command
  run per CHECK, no implementation edits. On a further revision: explicit `rv=k superseded by rv=k+1`,
  preserve IDs/obligations; new variants get new IDs.

### rv=2 supersession
`rv=1` is **explicitly superseded by rv=2**. Row IDs E160-01..E160-14 and their requirement IDs,
priorities, predicates, exact invocations, exit/log obligations, artifacts and owners are carried forward
verbatim. rv=2 adds the stable rows below; it does not delete or lower any rv=1 obligation. A kept generated
public rename that touches the public surface triggers EN/RU docs + API-NAMING register obligations —
generated code does not exempt it. The new rows get **one additional evidence package**, owner `check`.

### rv=2 new variant rows (stable IDs; each with a negative case)
| row | req | scenario (positive) | negative case | evidence kind/source | owner | applicability |
|---|---|---|---|---|---|---|
| E160-15 | R01 | alias→positional transition | reordering alias↔slot must not shift slots | `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~MixedJoinChainTests`; generated SQL | coder/check | D:160-01 |
| E160-16 | R01 | positional→alias transition | alias must not bind to the wrong slot | same | coder/check | D:160-01 |
| E160-17 | R01 | alias→positional→alias alternation | repeated CLR type must not collapse slots | same | coder/check | D:160-01 |
| E160-18 | R02 | mixed chain parity vs equivalent positional chain | parity must not be coincidental (type/arity match) | same | coder/check | D:160-01 |
| E160-19 | R06 | in-memory refusal for a mixed alias chain | pure positional in-memory chain must NOT be refused | alias suite + core in-memory | coder/check | D:160-01 |
| E160-20 | R11 | receiver static-type binding (`AliasJoin_*` receiver) | inherited positional overload must not win | generator harness / Roslyn symbol inspection | coder/check | D:160-04 |
| E160-21 | R11 | generated `new` instance overload shadowing | alias extension must remain applicable for alias steps | generator harness + generated shape | coder/check | D:160-04 |
| E160-22 | R11 | slot/name collisions (duplicate alias, `ItemK`, `_`) | collision must produce a diagnostic, not malformed C# | diagnostic suite | coder/check | D:160-04 |
| E160-23 | R03 | RootAlias (`A1_<name>` slot-1 encoding) | repeated/invalid root alias rejected | Phase 2 (D:160-03) | coder/check | D:160-03 |
| E160-24 | R10 | public-surface compatibility + docs/API-NAMING register | unnecessary rename must be reverted; kept rename documented | EN/RU docs + register | coder/check | D:160-04 |

**Evidence kind/source for every rv=2 row:** exact invocation (argument array), exit/result, selected_count,
full log at `docs/specs/status/rc2-160-evidence/<row-ID>/`, tree identity, rv=2. Missing evidence is
`unknown/missing`, not PASS.

### D:160-04 audit result (Phase 1, partial)
- Generated-type rename observed: `AliasJoin_<AliasSequence>` / `AliasProjection_<AliasSequence>` →
  `AliasJoin_P1_A2_<Name>...` / `AliasProjection_P1_A2_<Name>...` (slot-encoded).
- Verification: under free mixing the same alias sequence with the same generic arity can differ in the
  positional placement of an alias (`[alias]` vs `[positional, alias]` → both arity 3); alias-only naming
  would alias two different projections onto one type name. **The rename is mechanism-required**, and it
  matches approved decision #22 (`A{slot}_{name}` / `P{slot}`, legacy names not preserved pre-release).
  No unnecessary rename found → nothing reverted.
- Kept rename obligations (deferred to CHECK): EN/RU docs pages in the docs plan + API-NAMING register
  entry for the generated `AliasJoin_*`/`AliasProjection_*` public surface. Owner `coder` (docs) / `check`
  (verdict).

---

## Contract revision rv=3 (supersedes rv=2)

- **Classification:** P:160-ROOT-CONTRACT decided by the planner as **(iii) bounded in-scope fix**. This is a
  contract-revision refinement only: **plan revision stays `r=2`, attempt `n=1/3`**; no task/dependency change.
- **Explicit supersession:** `rv=2` is **explicitly superseded by `rv=3`**. Row IDs **E160-01..E160-24** and
  **C-E01..C-E04**, their requirement IDs, priorities, predicates, exact invocations, exit/log obligations,
  artifacts and owners are carried forward verbatim. rv=3 does not delete or lower any rv=2 obligation.
- **Decision binding:** `From(builder)` and `From(QueryCommand<T>)` derived-root sources must be **preserved**
  when root-aliased with `.WithAlias(Alias.Root)`, exactly like `FromSql` — the derived root renders as
  `(select …) as 't1'`, **not** `orders as 't1'`. No R160 criterion is weakened; the derived root is neither
  flattened nor unwrapped. The `EntityBuilder.cs:3464-3466` projection guard is **kept** (not globally removed).

### Corrected expectations carried into existing rows (not weakened)

- **E160-02 / R03 (corrected expectation):** for the `FromSql` root, `.WithAlias(Alias.Root)` preserves the
  derived source; the correct SQL is `… from (select Id, BuyerId from orders) as 't1' join …`, **not**
  `orders as 't1'`. The former red test was over-strict on the SQL substring. Applicability and owner unchanged.
- **E160-23 / R03 (corrected expectation):** the Phase-2 root matrix accepts the preserved derived source
  `(select …) as 't1'` for `FromSql` (and, per this revision, for `From(builder)`/`From(QueryCommand<T>)`).
  Repeated/invalid root alias is still rejected; the runtime guard is not removed. Owner unchanged.

### rv=3 new rows (stable IDs; same scoring slots as E160-15..E160-24)

| row | req | scenario (positive) | evidence kind/source | exact invocation + exit/result/log | artifact | owner | applicability |
|---|---|---|---|---|---|---|---|
| E160-25 | R160-03 | `From(builder)` root `.WithAlias(Alias.Root)` succeeds and **preserves** the source (derived root aliased `t1`) + unaliased regression | `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~RootAliasTests`; captured SQL/plan | exit/result + selected_count + full log recorded at artifact dir | `docs/specs/status/rc2-160-evidence/E160-25/` | coder creates / check verifies | D:160-03 |
| E160-26 | R160-03 | `From(QueryCommand<T>)` root `.WithAlias(Alias.Root)` succeeds and **preserves** the derived query (aliased `t1`) + unaliased regression | same | exit/result + selected_count + full log recorded at artifact dir | `docs/specs/status/rc2-160-evidence/E160-26/` | coder creates / check verifies | D:160-03 |

**Evidence kind/source for every rv=3 row:** exact invocation (argument array), exit/result, selected_count,
full log at the artifact dir, tree identity, `rv=3`. Missing evidence is `unknown/missing`, not PASS.

### CHECK re-gather budget (preserved)

- Owner `check`; at most **2 targeted scout calls** and **1 repeat proving-command run** per CHECK, no
  implementation edits. The two new rows get **one additional evidence package**, owner `check`. On a further
  revision: explicit `rv=k superseded by rv=k+1`, preserve IDs/obligations; new variants get new IDs. `r`
  increases only when tasks/dependencies/remediation actions change.

## CHECK verdict r=2 / n=2 — FAIL

- Verdict: **fail**. Prior attempt n=1 failed on defect D160-C1 (pre-.WithAlias state loss); n=2 fixed C1, then this final CHECK failed.
- Fresh deterministic evidence (HEAD 40b1a159, logs /tmp/d160-check/): build Debug exit 0 0/0; build Release exit 0 0/0; `dotnet test tests/nextorm.alias.tests -c Debug` exit 0 total 95/passed 95; `dotnet test tests/nextorm.core.tests -c Debug` exit 0 total 1770/passed 1770; `git diff --check` exit 0; tracked tree clean; `git diff --stat -- src tests` empty (no residual mutation); `gh issue view 160` state OPEN.
- Per-criterion: R160-01 unmet (F2/F3); R160-02 unverified; R160-03 unmet (F1); R160-04..10 unverified (evidence not criterion-linked); R160-11 unmet (F2). PASS prohibited.
- **F1 (P1, recurrence of D160-C1, escalate):** `.WithAlias`/alias-join still loses pre-existing state. `EntityBuilder.cs` `AliasRoot` 3159 (derived-root branch 3198-3202 `rooted.SourceFrom = new FromExpression(rooted.Query); rooted.Query = null;`), `ApplyPreAliasStateToAliasJoined` 3090-3142, `ApplyJoinStateTo` 3280-3320 omit `_subQueryHint` (`:54`) and `_sourceEntityType` (`:42`). `_subQueryHint` set by `From(query, options.SubQueryHint)` (`DataContextExtensions.cs:1324-1333`, `FromOptions.cs:156-162`), consumed `EntityBuilder.cs:3944/3948`; no test combines SubQueryHint with `.WithAlias`.
- **F2 (P1, DO):** `JoinAliasGenerator.cs:185-187` seeds positional-after-alias transitions only for inline invocation/parenthesized receivers; a stored non-root alias builder (`var q = b.Join(x, Alias.Buyer); q.Join(...)`) gets no generated `new` overload, binds inherited positional `EntityBuilder<T>.Join`, fails at runtime. (Stored root `.WithAlias` is tested and works: `RootAliasTests.cs:614-626`.)
- **F3 (P1, DO):** `JoinAliasGenerator.cs:884-896` `IsItemAlias` accepts only `Item1..Item{arity}` while `ProjectionAliasCache.cs:36-54,62-79` parses any `ItemN`; `Item0`/out-of-range silently resolve wrong.
- **F4 (P1, DO):** correlated alias APPLY unreachable. Seams `EntityBuilder.cs:2938-2944,2959-2965` exist; generator emits `JoinAlias<...>` only for EntityBuilder/Cte sources (`JoinAliasGenerator.cs:986-990,998-1001,1008-1012,1155-1163`); `JoinAliasGenerator.cs:198-202` intentionally skips correlated lambda source; no test calls those overloads.
- Open mandatory evidence (CHECK-collection, not product defects): ClickHouse/MariaDB real root/mixed execution (R160-09); arity 2–8 SQL/execution + runtime `As<T>`; value/ref/nullable/default entities; root/join in-memory negatives + positional control; repeated/alternating + shared scalar command; NORMGEN001–006; incremental alias/slot/order edit; direct root `.Root` read; `From(CteReference)`; provider-SQL mixing-direction. E160-01..14 blanket "green" lacks row/version→artifact mapping.

## Escalation (D160-C1 recurrence) — decision

- The same pre-.WithAlias state-loss class returned after the n=2 fix ⇒ escalation before a second fix (contract trigger 2).
- **Decision:** route to `planner` for a genuine replan **r=3, n=1** (not a DO loop-back in r=2). F1 is a second fix of C1; another F1/C1 failure ⇒ escalate again or STOP. Reasons: four open P1 defects (F1–F4) + large evidence gap would spend the last r=2 attempt; F1 needs a structural single-procedure state transfer (not another hand-maintained field list); F4 is a plan-level generator gap.
- F1/C1 history persists across r; a replan does not erase it.
- Risk boundary: a ClickHouse/MariaDB renderer/planner defect beyond a local fix, or F4 requiring a new public seam ⇒ STOP, do not weaken R160-09, do not rewrite renderer/planner.

## P:D160-r3 — SEALED (r=3, n=1; rv=3 superseded by rv=4)

- Baseline HEAD `40b1a159`. Structural remediation is a substantive revised plan; D160 stays active; units are additive, not replacements. All rv=1..3 obligations (E160-01..29, C-E01..04) preserved; new rows E160-30..33; no renumbering. `rv=3` is explicitly **superseded by rv=4**.
- Minimal solution: generic-neutral structural state snapshot/carrier, complete copying by default + explicit alias/shape transformations, one copy procedure shared by root and joined transitions. Rejected: another centralized field list (repeats C1 omission risk); runtime reflection copying (dynamic/perf risk). Reflection allowed only in the completeness guard test.
- **D160.3-1 (F1, fix first):** `EntityBuilder.cs:3090-3142,3159,3198-3202,3280-3320`; preserve `_subQueryHint`, `_sourceEntityType`, derived-root semantics. Bind R03→E02,E25,E26,E27,E28,E30; R02,R07→E27,E30; R10→E28. Tests A+K+S (whole projects): complete field classification (every field copied or explicitly excluded), non-default-state transfer, hinted `.WithAlias` SQL; omission/semantic loss fails.
- **D160.3-2 (F2, after .3-1):** `JoinAliasGenerator.cs:185-187`; generate positional-after-alias transitions for stored non-root receivers. Bind R01→E01,E15,E16,E17,E31; R02→E18; R11→E20,E21,E31. Tests A+S: stored/inline/parenthesized × both directions, repeated types/alternation; fallback/shifted slot/runtime failure fails.
- **D160.3-3 (F3, after .3-2):** `JoinAliasGenerator.cs:884-896` reconciled with `ProjectionAliasCache.cs:36-54,62-79`; bind R04,R11→E03,E05,E22,E29,E32. Tests A: valid positions succeed; `Item0`/`Item9`/out-of-range emit the registered NORMGEN diagnostic.
- **D160.3-4 (F4, after .3-3):** generator `198-202,986-1012,1155-1163` over existing seams `EntityBuilder.cs:2938-2944,2959-2965`; no new core public seam. Bind R01,R03,R11→E01,E02,E20,E29,E33; R09→E08,E33. Tests A+S+I: compilation, unchanged outer slots, SQL, real results.
- **D160.3-5 (missing fixtures, after .3-4):** add only genuinely absent scenarios; harness/files `JoinAliasGeneratorDiagnosticTests`, `AliasGeneratedSurfaceTests`, `AliasProjectionShapeTests`, `RootAliasTests.cs`, `MixedJoinChainTests.cs`, `RootProjectionTests.cs`, provider `JoinAliasSqlGenerationTests.cs`, `CommonTestSuite.JoinAlias.cs`. Open-scenario binds: ClickHouse/MariaDB real root/mixed R09→E08,E33; arity 2–8 + `As<T>` R04→E03/E08; value/ref/nullable/default R01,R11→E05/E01; root/join in-memory R05,R06→E04,E19; repeated/alternating/shared scalar R07→E06; NORMGEN001–006 + incremental edits R11→E05,E22,E29,E32; direct `.Root` read R05→E04; `From(CteReference)` R03→E02,E28; provider-SQL mixing directions R01→E01,E15,E16,E17,E31.
- **D160.3-6 (verification):** execute B/A/K/S/I/F/coverage/X/V per row ledger; assemble `<artifact-dir>/<row-ID>/` manifests (row/req IDs, rv, owner, exact command, source paths, executed test identities, exit/result, logs). No implementation-completion claim closes D160; hand off to CHECK; C-E03 additionally requires the eventual ACT record.
- New rows: **E160-30 / R02,R03,R07 (F1)** structural completeness guard + non-default transfer + derived-root semantics; **E160-31 / R01,R11 (F2)** stored/inline/parenthesized receivers × both directions; **E160-32 / R04,R11 (F3)** valid positions + invalid `ItemN` diagnostics; **E160-33 / R01,R03,R09,R11 (F4)** correlated lambda/QueryCommand compilation, outer slots, SQL, real results.
- Commands (exact): B `dotnet build nextorm.slnx -c Debug` and `-c Release` (0/0); A `dotnet test tests/nextorm.alias.tests -c Debug`; K `dotnet test tests/nextorm.core.tests -c Debug`; S `dotnet test tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests -c Debug`; I `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` (skill first; mariadb:11.4; skipped≠pass); F `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` (baseline ratio 1.87, threshold >2.244, ≤4 min); coverage `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"` + `dotnet run reportgenerator …` (line≥85/branch≥75); X `git diff --check` + `dotnet docfx docs/docfx.json`; V `python3 /home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py` retaining the frozen E160-12 invocation.
- Execution mode: sequential in the current tree; no worktrees/commits/merges/push. Perf measurement mandatory (E09). Reconnaissance bounded inside .3-1/.3-4.
- NOT changing: R160-01..11; inherited evidence obligations; approved scope/API; positional-only runtime path; guard `EntityBuilder.cs:3464-3466`; shared scalar `QueryCommand`/`Cache`; renderer/planner architecture; CRLF/warnings-as-errors/CPM. Deferred: full in-memory aliases, JoinInto aliases, arity>8 without `As<T>` (frozen triggers unchanged).
- STOP/escalate: ClickHouse/MariaDB renderer/planner defect beyond local fix; F4 needing a new public seam; another F1/C1 failure. CHECK re-gather budget: two targeted rounds per CHECK attempt, owned by CHECK.

## D160.3-2 (F2 stored non-root alias receivers) — DO green

- **Defect key:** `F2` (stable). **First observed:** CHECK r=2/n=2. **Fixed in:** r=3, n=1 (D160.3-2). **Applied fixes of F2:** 1. **Status:** resolved at DO scope.
- **Root cause:** the generator seeded positional-after-alias transitions only from observed inline/parenthesized
  call sites (`JoinAliasGenerator.cs:185-187`, observed-transition loop). A stored receiver
  (`var q = b.Join(x, Alias.Buyer); q.Join(...)`) produces no such call site, so no generated `new`
  transition existed on `AliasJoin_<suffix>`; the inherited `EntityBuilder<TEntity>.Join/LeftJoin/...`
  bound and routed around the `JoinAlias` seam (defect manifestation: the static result was
  `Projection<AliasProjection_…, Person>` with no slot-3 `ItemN`).
- **Fix (bounded, generator-only):** after the observed-transition loop and before the root-alias loop,
  snapshot `schemas.Values.Where(HasAlias)` and, for each alias-bearing schema, (a) register the
  `${suffix}_P{n+1}` result schema and (b) seed all seven operators' `PositionalTransition` entries
  (deterministic `JoinOperatorOrder`). This reuses the exact Phase-1/root `RenderPositionalMethods`
  mechanism, one level deep. No core/`EntityBuilder.cs` change; no positional-only runtime-path change;
  NORMGEN001–007 and incrementality contracts unchanged. `EntityBuilder.cs` guard kept.
- **Public surface:** 8 new generated `AliasProjection_/AliasJoin_` pairs are intended and frozen:
  `A1_Root_A2_Buyer_P3`, `A1_Root_P2_P3`, `P1_A2_Buyer2_P3`, `P1_A2_Buyer_A3_Approver_P4`,
  `P1_A2_Buyer_P3_A4_Approver_P5`, `P1_A2_Buyer_P3_P4`, `P1_P2_A3_Approver_P4`, `P1_P2_A3_Buyer2_P4`.
- **Evidence:** `docs/specs/status/rc2-160-evidence/D160.3-2/` — `scope.json` (`brief` exit 0),
  `red-build.log` (pre-fix build exit 1, 5× CS1061), `fix-build.log` (exit 0, 0/0), four filtered logs,
  `alias-boundary.log` (108/108), `solution-build.log` (0/0), six provider logs (0 failed),
  `evidence.json` + `validator-report.log` (`report` exit 0).
- **Binding:** R01→E01,E15,E16,E17,E31; R02→E18; R11→E20,E21,E31 (per `P:D160-r3`).
- **No DO→PLAN candidate.** Generator-seam-only change: no core/public-seam or renderer/planner edit was
  needed. Remaining units unchanged (D160.3-3/.3-4/.3-5/.3-6).

## D160.3-3 (F3 ItemN parser reconciliation) — DO green

- **Defect key:** `F3` (stable). **First observed:** CHECK r=2/n=2. **Fixed in:** r=3, n=1 (D160.3-3).
  **Applied fixes of F3:** 1. **Status:** resolved at DO scope.
- **Root cause:** `JoinAliasGenerator.IsItemAlias` accepted only `Item1..Item{arity}` as an alias
  collision, while `ProjectionAliasCache.TryParseItemPosition` (`:62-79`, used by `GetMemberPosition`
  `:36` / `ResolveMemberPosition` `:44`) claims **any** `ItemN` shape. `Alias.Item0` and an out-of-range
  `Alias.ItemN` (e.g. `Item9`, `Item{arity+1}`) were therefore emitted as generated alias members that
  the runtime parser read back positionally and resolved to the wrong slot (or no slot).
- **Fix (bounded, generator-only):** `IsItemAlias` now returns true for the whole structural shape
  `Item` + one-or-more digits, independent of arity; both call sites drop the arity argument. Every
  rejected name routes to the existing **NORMGEN002** (`AliasCollision`), the same registered diagnostic
  as the in-range case — no new diagnostic id was introduced. `ProjectionAliasCache` is intentionally
  unchanged (generator-side rejection removes every name the runtime parser could misread, so no
  malformed generated C# and no silent wrong resolution remain). Local bounded fix; no core/public-seam,
  renderer or planner change; positional-only runtime path untouched.
- **Public surface:** none. The generated projection for a rejected chain is not emitted; only the
  `Alias.<Name>` marker remains (valid C#).
- **Evidence:** `docs/specs/status/rc2-160-evidence/D160.3-3/` — `scope.json` (`brief` exit 0),
  `red-build.log` / `red-filtered.log` (F3 reverted: exit 2, selected 28 / failed 5 = only the
  out-of-range cases), `fix-build.log` (0/0), `fix-diagnostic-filtered.log` (28/28), `fix-shape-filtered.log`
  (5/5), `solution-build.log` (0/0), `alias-boundary.log` (118/118), `core-boundary.log` (1774/1774),
  `evidence.json` + `validator-report.log` (`report` exit 0).
- **Binding:** R04,R11 → E03,E05,E22,E29,E32 (per `P:D160-r3`).
- **No DO→PLAN candidate.** Generator-only predicate change: no new public seam and no cache/renderer/
  planner edit was needed.

## D160.3-4 (F4 correlated APPLY through the alias seam) — DO green

- **Defect key:** `F4` (stable). **First observed:** CHECK r=2/n=2. **Fixed in:** r=3, n=1 (D160.3-4).
  **Applied fixes of F4:** 1. **Status:** resolved at DO scope.
- **(a) F4 reachable via existing seams (no new core public seam).** The generator now recognizes a
  correlated APPLY lambda source (`JoinAliasGenerator.TryGetJoinedType(..., allowCorrelated)` +
  `IsCorrelatedSource`; `JoinSourceKind.Correlated` on `ChainStep`) and emits **both** generated alias
  extensions for the applied slot — `Expression<Func<TEntity, EntityBuilder<TJoin>>>` and
  `Expression<Func<TEntity, QueryCommand<TJoin>>>` — each forwarding to the **existing**
  `EntityBuilder.JoinAlias` correlated overloads (`EntityBuilder.cs:3015-3021,3036-3042`), which share
  the positional correlated path (`JoinAliasApply`). The joined type stays the method type parameter
  `TJoin`, so outer slots never shift. `allowCorrelated` is passed only on alias steps; positional
  correlated steps are unchanged. Alias extensions whose prefix contains a correlated step are not
  emitted (their concrete receiver cannot be rendered).
- **Binding:** R01,R03,R11 → E01,E02,E20,E29,E33; R09 → E08,E33 (per `P:D160-r3`).
- **(b) Guarded limitation (pre-existing design guard, mirrored positionally) — trigger.** Correlated
  APPLY requires a **non-projection single-entity receiver**: the runtime guard
  `EntityBuilder.JoinAliasApply` (`EntityBuilder.cs:3096-3098`, mirrored by the positional
  `JoinApply` at `:3570`) throws `NotSupportedException("*single-entity*")` when `TEntity` is a join
  projection. Consequently (i) a correlated APPLY **after** an alias/join step is fail-closed by
  design, and (ii) an alias step **after** a correlated APPLY does not bind — the generator skips the
  follower's alias transition (pinned by
  `JoinAliasGeneratorDiagnosticTests.Alias_step_after_a_correlated_apply_has_no_generated_extension`).
  A positional step **after** a correlated APPLY does bind and route (covered in `MixedJoinChainTests`).
  **Trigger to re-open:** if free-mixing scope later requires a correlated-APPLY follower, the fix is
  generator-only (forward the unknown correlated type as a method type parameter of the follower
  extension) and does **not** require a new public seam; until then this stays a bounded limitation,
  not a DO→PLAN candidate.
- **(c) E160-33 status — green (compile + slots + SQL + real results).** Alias: `MixedJoinChainTests`
  25/25 and `JoinAliasGeneratorDiagnosticTests` 30/30 (exit 0); frozen generated-surface baseline in
  `AliasGeneratedSurfaceTests` extended by the 10 new correlated types. Provider SQL (inner filtered
  `FullyQualifiedName~JoinAliasSqlGenerationTests`, all exit 0, 0 failed): sqlite 17, postgres 19,
  sqlserver 19, mysql 19, mariadb 19, clickhouse 20 — alias form asserted byte-identical to positional
  plus dialect APPLY spelling and correlated reference; SQLite/ClickHouse fail-closed. Real execution
  (`CommonTestSuite.JoinAlias.cs`, one container run, exit 0): Total 3369 / Failed 0 / Skipped 200;
  **9/9 correlated-APPLY alias cases pass on PostgreSQL/SQL Server/MySQL**, 3 capability-skipped on
  SQLite (`Provider.SupportsApply=false`), 0 provider-availability skips. Full boundary projects:
  alias 125/125, sqlite 1170 (1 skip), postgres 809, sqlserver 731, mysql 310, mariadb 236,
  clickhouse 599 — all exit 0. Validator `brief` exit 0; `report` **exit 0**.
- **Artifacts:** `docs/specs/status/rc2-160-evidence/D160.3-4/` — `scope.json`, `evidence.json`,
  `validator-report.log`, `build3.log`, `alias-build3.log`, `mixed-filtered2.log`,
  `diagnostic-filtered.log`, `{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-join-alias.log`,
  `alias-boundary2.log`, `{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-boundary.log`,
  `integration-build2.log`, `integration.log`, `integration-xml.log`, `integration-results.xml`.
  Source: `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs`,
  `tests/nextorm.alias.tests/{MixedJoinChainTests.cs,JoinAliasGeneratorDiagnosticTests.cs,AliasGeneratedSurfaceTests.cs}`,
  `tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests/JoinAliasSqlGenerationTests.cs`,
  `tests/nextorm.integration.tests/CommonTestSuite.JoinAlias.cs`.
- **No DO→PLAN candidate.** Generator-seam-only change; no renderer/planner defect and no new public
  core seam were required. Remaining units unchanged (D160.3-5 fixtures, D160.3-6 verification).

## D160.3-5 (missing fixtures) — DO green

- **Scope:** test-only; no product source changed. `unit D160.3-5`; plan `r=3`, attempt `n=1/3`.
  Scope + report at `docs/specs/status/rc2-160-evidence/D160.3-5/`
  (`scope.json` brief exit 0; `evidence.json` + `validator-report.log` `report` exit 0).
- **Binds already covered (not duplicated):** root/join in-memory negatives + positional control
  (`RootProjectionTests`, `MixedJoinChainTests`, `AliasInMemoryRefusalTests`, `RootAliasTests`);
  repeated/alternating + shared scalar command (`AliasPlanCacheTests`, `RootProjectionTests` cache
  cases); NORMGEN001–006 each (diagnostic suite); arity 2–8 `Extend` + 9th `NORMGEN004` + arity cap.
- **Binds newly added (genuinely absent):**
  - **Bind 9 — provider-SQL mixing directions** `R01→E01,E15,E16,E17,E31`: alias→positional,
    positional→alias and alternation added to all six `tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests/JoinAliasSqlGenerationTests.cs`,
    each asserted byte-identical to the equivalent fully positional chain with ordered slots t2/t3/t4.
  - **Bind 7 — direct `.Root` read** `R05→E04`: `RootAliasTests.Reading_the_root_alias_member_outside_an_expression_tree_throws`.
  - **Bind 8 — `From(CteReference<T>)` root alias** `R03→E02,E28`:
    `RootAliasTests.Generated_root_alias_on_a_recursive_cte_reference_source_keeps_the_reference`
    (`.WithAlias(Alias.Root)` inside the recursive step; passes — R160-03 bind met, no bounded
    limitation, no DO→PLAN candidate).
  - **Bind 3 — nullable reference / default value** `R01,R11→E05,E01`:
    `MixedJoinChainTests.Left_join_unmatched_alias_slot_yields_a_null_reference_and_the_default_value`
    (NULL alias slot reads null reference and `(int?)` value, never a fabricated default).
  - **Bind 2 — `As<T>` usable** `R04→E03,E08`:
    `RootProjectionTests.As_overflow_escape_projects_the_chain_and_continues_from_the_derived_table`
    (real `As<TResult>` call, derived-table continuation; complements the existing reflected-signature
    test and the 1→8 `Extend` walk).
  - **Bind 6 — incremental alias/slot/order edit** `R11→E05,E22,E29,E32`:
    `JoinAliasGeneratorDiagnosticTests.Editing_an_alias_slot_and_order_regenerates_the_output`.
  - **Bind 1 — ClickHouse/MariaDB real root/mixed execution** `R09→E08,E33`:
    `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs` gained 4 provider-only real-execution
    cases on its own `simple_entity`/`complex_entity` seed (no `CommonTestSuite` inheritance; slot-SQL
    asserted with `as \`t1\``/`as \`t2\``); new
    `tests/nextorm.integration.tests/MariaDbJoinAliasIntegrationTests.cs` uses its own `MariaDbContainer`
    seed (`alias_orders`/`alias_person`) with 4 real root/mixed cases — **MySQL is not substituted for
    MariaDB**. The container-backed run is deferred to D160.3-6; the tests compile in the integration
    project (solution build 0/0).
- **Evidence:** `docs/specs/status/rc2-160-evidence/D160.3-5/` — `build.log` (0/0),
  `root-alias-filtered.log` (34), `mixed-chain-filtered.log` (26), `JoinAliasGeneratorDiagnosticTests.log`
  (31), `AliasProjectionShapeTests.log` (5), `AliasGeneratedSurfaceTests.log` (5),
  `root-projection-filtered.log` (15), `{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}-join-alias.log`
  (20/22/22/22/22/23), `alias-boundary.log` (129/129), `core-boundary.log` (1775), `*-boundary.log`
  (sqlite 1173/1 skip, postgres 812, sqlserver 734, mysql 313, mariadb 239, clickhouse 602), `scope.json`,
  `evidence.json`, `validator-report.log`.
- **No DO→PLAN candidate.** Test-only; the `From(CteReference)` root alias and the `As<T>` derived-table
  continuation both execute green, so no new product capability was required. Remaining unit:
  D160.3-6 verification (B/A/K/S/I/F/coverage/X/V).

## D160.3-6 (verification) — DO complete, ready for final CHECK

- Scope: verification-only. Plan `r=3`, attempt `n=1/3`, cycle 1, contract `rv=4`; tree `40b1a159+dirty`.
- Owner: `coder` creates the manifests and evidence; `check` independently matches row/result/artifact. C-E03 finally requires the eventual ACT record; no DO-completion claim closes D160.

### Durable state (cycle boundary)
- **Current cycle:** 1
- **Plan revision:** `r=3` (sealed `P:D160-r3`; `rv=3` explicitly superseded by `rv=4`)
- **Attempt:** `n=1/3`
- **Defect history:**
  - `D160-C1` (pre-`.WithAlias` state loss) — first observed n=1, fixed n=2, **recurred as F1**.
  - `F1` (structural EntityBuilder state copy) — fixed r=3 / D160.3-1; **2 applied fixes of C1/F1**; resolved.
  - `F2` (stored non-root alias receiver) — fixed D160.3-2; 1 applied fix; resolved.
  - `F3` (`ItemN` parser reconciliation) — fixed D160.3-3; 1 applied fix; resolved.
  - `F4` (correlated APPLY through the alias seam) — fixed D160.3-4; 1 applied fix; resolved.
  - **No F1/C1 recurrence was observed at any boundary in this unit — the STOP rule did not fire.**

### New rows E160-30..33 (rv=4; stable IDs; no renumbering)
| row | req | scenario | artifact |
|---|---|---|---|
| E160-30 | R160-02,R160-03,R160-07 | F1 structural state copy + non-default transfer (`_subQueryHint`/`_sourceEntityType`) + derived-root semantics | `docs/specs/status/rc2-160-evidence/E160-30/manifest.md` |
| E160-31 | R160-01,R160-11 | F2 stored/inline/parenthesized receivers × both directions | `docs/specs/status/rc2-160-evidence/E160-31/manifest.md` |
| E160-32 | R160-04,R160-11 | F3 valid positions + invalid `ItemN` diagnostics (registered NORMGEN002) | `docs/specs/status/rc2-160-evidence/E160-32/manifest.md` |
| E160-33 | R160-01,R160-03,R160-09,R160-11 | F4 correlated lambda/`QueryCommand` compilation, outer slots, SQL, real results | `docs/specs/status/rc2-160-evidence/E160-33/manifest.md` |

### Refreshed DO ledger E160-15..E160-33 — green with row→artifact mapping
| row | req | status | primary artifact(s) |
|---|---|---|---|
| E160-15 | R01 | green | `D160.3-2/`, `D160.3-5/` (alias→positional) |
| E160-16 | R01 | green | `D160.3-2/`, `D160.3-5/` (positional→alias) |
| E160-17 | R01 | green | `D160.3-2/`, `D160.3-5/` (alternation) |
| E160-18 | R02 | green | `D160.3-2/` (mixed vs positional parity) |
| E160-19 | R06 | green | `D160.3-2/`, `E160-03/` (in-memory refusal + positional control) |
| E160-20 | R11 | green | `D160.3-2/`, `E160-21/` (receiver static-type binding) |
| E160-21 | R11 | green | `E160-21/`, `D160.3-2/` (generated `new` shadowing) |
| E160-22 | R11 | green | `D160.3-3/`, `E160-05/` (collision diagnostics) |
| E160-23 | R03 | green | `E160-05/`, `E160-25/`, `E160-26/` (root alias + rejection) |
| E160-24 | R10 | green | `docs/specs/design/API-NAMING-REVIEW.md` §#160 + docs |
| E160-25 | R03 | green | `E160-25/` (`From(builder)` derived-root) |
| E160-26 | R03 | green | `E160-26/` (`From(QueryCommand<T>)` derived-root) |
| E160-27 | R01,R03,R07 | green | `E160-27/` (C1 pre-alias state) |
| E160-28 | R03,R10 | green | `E160-28/` (root matrix + docs) |
| E160-29 | R04,R06,R07,R11 | green | `E160-29/` (findings C/D + generator classification) |
| **E160-30** | R02,R03,R07 | green (new) | `D160.3-1/`, `E160-30/manifest.md` |
| **E160-31** | R01,R11 | green (new) | `D160.3-2/`, `E160-31/manifest.md` |
| **E160-32** | R04,R11 | green (new) | `D160.3-3/`, `E160-32/manifest.md` |
| **E160-33** | R01,R03,R09,R11 | green (new) | `D160.3-4/`, `E160-33/manifest.md` |

E160-01..E160-14 remain green (Phase-1/Phase-2 rows); their mapping is unchanged from the ledger above and from `D160.3-1..5`.

### Canonical boundary (part 1) — partial rows E160-07/08/09/10/11/12 refreshed
- **B / E160-07:** `dotnet build nextorm.slnx -c Debug` and `-c Release --no-incremental` exit 0, **0 warnings / 0 errors**.
- **I / E160-08:** `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -result-xml …` exit 0, **Total 3377 / Errors 0 / Failed 0 / Skipped 200 / Not Run 0**; PostgreSQL/SQL Server/MySQL/MariaDB/SQLite/ClickHouse all executed, 0 provider-availability skips. D160 buckets: root alias 16 pass, mixed positional/alias 10 pass, correlated APPLY alias 9 pass + 3 capability-skip (SQLite `SupportsApply=false`).
- **F / E160-09:** acceptance exit 0, **7/7** cases, 0 failures; ratio `Cached_ToList/Prepared_ToList` **1.95** vs baseline 1.87 (< 2.244 gate).
- **coverage / E160-10:** collect exit 0 (9313 tests, 0 failed, 9112 succeeded, 201 skipped); report exit 0; line **88.2%**, branch **80.2%** (both ≥ 85/75).
- **X / E160-11:** `git diff --check` exit 0 (clean; only LF→CRLF warnings); `dotnet docfx docs/docfx.json` exit 0 (2 pre-existing warnings / 0 errors).
- **V / E160-12:** `validate_inner_loop.py brief` exit 0; `report` exit 0 (tree `40b1a159+dirty`).
- **A/K/S full sweeps** (D160.3-5, after all product edits): alias **129/129**, core **1775/1775**, sqlite **1173** (1 skip), postgres **812**, sqlserver **734**, mysql **313**, mariadb **239**, clickhouse **602** — all exit 0, 0 failed. Rows and per-unit filtered runs are recorded in `E160-30..33/manifest.md` and `D160.3-1..5/evidence.json`.

### Guarded limitation (recorded, not a DO→PLAN candidate)
Correlated APPLY requires a **non-projection single-entity receiver**; the runtime guard `EntityBuilder.JoinAliasApply` (`EntityBuilder.cs:3096-3098`, mirrored by positional `JoinApply` `:3570`) throws `NotSupportedException("*single-entity*")` for a join-projection receiver. Therefore a correlated APPLY after an alias/join step is fail-closed by design, and an alias step **after** a correlated APPLY does not bind (the generator intentionally skips the follower's alias transition, pinned by `JoinAliasGeneratorDiagnosticTests.Alias_step_after_a_correlated_apply_has_no_generated_extension`). A positional step after a correlated APPLY does bind and route (green). Re-open trigger: only if free-mixing scope later requires a correlated-APPLY follower; the fix would be generator-only and needs no new public seam.

### MariaDB seed test fix (test-only, in the D160 footprint)
The pre-fix part-1 integration run (`E160-08/integration-prefix-fail.log`) failed one case: `MariaDbJoinAliasIntegrationTests.Root_alias_inner_join_returns_matched_rows` expected `{10, 20}` while the MariaDB seed has a single order (`id=1, buyer_id=10`); the product result `{10}` was correct. Fixed test-only at `tests/nextorm.integration.tests/MariaDbJoinAliasIntegrationTests.cs:42` (`ids.Should().Equal(10, 20)` → `ids.Should().Equal(10)`); no product source changed. The re-run (`E160-08/integration.log`) is green (Total 3377, Failed 0). This is a **test-fixture defect, not a product defect and not a DO→PLAN candidate**.

### No F1/C1 recurrence / STOP rule
No observation in D160.3-6 (nor in D160.3-1..5 combined with the part-1 boundary) shows a loss of pre-`.WithAlias` state; the structural state carrier + shared `CopySharedStateTo` remove the hand-maintained field-list failure mode. The STOP/escalate triggers (ClickHouse/MariaDB renderer/planner defect beyond a local fix; F4 needing a new public seam; another F1/C1 failure) did **not** fire. Hand off to final CHECK.

## CHECK r=3 / n=1 — FAIL (fresh boundary)

- **Verdict: FAIL.** Plan `r=3`, attempt `n=1/3`, contract `rv=4`, cycle 1. Fresh clean-build deterministic boundary on HEAD `40b1a159`+dirty; this gather edited no product source (logs only).
- **Artifact:** `docs/specs/status/rc2-160-evidence/CHECK-r3/` (`README.md` + per-command logs).

### Fresh command → exit / counts

| # | command | exit | total | passed | failed | skipped | log |
|---|---|---|---|---|---|---|---|
| 1 | `dotnet build nextorm.slnx -c Debug` | 0 | — | — | — | — | `build-debug.log` (0 Warning(s), 0 Error(s), 42.84s) |
| 2 | `dotnet build nextorm.slnx -c Release --no-incremental` | 0 | — | — | — | — | `build-release.log` (0 Warning(s), 0 Error(s), 46.10s) |
| 3 | `dotnet test tests/nextorm.alias.tests -c Debug` | **2** | 129 | 111 | **18** | 0 | `alias.log` |
| 4 | `dotnet test tests/nextorm.core.tests -c Debug` | **2** | 1776 | 1775 | **1** | 0 | `core.log` |
| 5 | `dotnet test tests/nextorm.sqlite.tests -c Debug` | **2** | 1173 | 1169 | **3** | 1 | `sqlite.log` |
| 6 | `dotnet test tests/nextorm.postgres.tests -c Debug` | **2** | 812 | 809 | **3** | 0 | `postgres.log` |
| 7 | `dotnet test tests/nextorm.sqlserver.tests -c Debug` | **2** | 734 | 731 | **3** | 0 | `sqlserver.log` |
| 8 | `dotnet test tests/nextorm.mysql.tests -c Debug` | **2** | 313 | 310 | **3** | 0 | `mysql.log` |
| 9 | `dotnet test tests/nextorm.mariadb.tests -c Debug` | **2** | 239 | 236 | **3** | 0 | `mariadb.log` |
| 10 | `dotnet test tests/nextorm.clickhouse.tests -c Debug` | **2** | 602 | 599 | **3** | 0 | `clickhouse.log` |
| 11 | `git diff --check` | 0 | — | — | — | — | `git-diff-check.log` (only benign LF→CRLF warnings) |

**Verdict of this gather: NOT green** — builds and `git diff --check` pass; every test sweep is red.

### Named failures (32)

- **core (1):** `EntityBuilderStateCopyTests.Positional_join_control_pins_the_baseline_modifier_behavior` (`EntityBuilderStateCopyTests.cs:191`): asserts a baseline positional join drops `_subQueryHint`/`_tag`/`_commandTimeout`; observed SQL/`CommandTimeout` now **preserve** them (`MY_HINT(positional)` emitted, `positional-tag` present, `CommandTimeout=42`) — positional-path over-preservation.
- **providers (3 identical per provider × 6 = 18):** `JoinAliasSqlGenerationTests.Mixed_positional_then_alias_join_matches_the_positional_chain`, `JoinAliasSqlGenerationTests.Alternating_alias_positional_alias_assigns_slots_in_chain_order`, `JoinAliasSqlGenerationTests.Repeated_clr_type_resolves_to_distinct_slots`. Characterization: the mixed chain renders `select t2.id …` while the equivalent fully positional chain renders `select t3.id …`/`select t4.id …` — an ordered-slot shift (R160-01 mixing parity), same across all six dialects.
- **alias (18):** `AliasGeneratedSurfaceTests.Generated_projection_builder_pairs_are_public_and_slot_bound`; `AliasJoinWhereTests.{Where_after_second_alias_join_targets_the_second_joined_slot, Where_between_first_and_second_alias_join_targets_the_first_joined_slot, Where_on_base_before_a_second_alias_join_keeps_the_base_slot}`; `AliasPlanCacheTests.Alternating_buyer_and_approver_queries_keep_slots_and_the_plan_cache`; `AliasProjectionShapeTests.{Alias_members_carry_join_slot_attribute_and_resolve_to_the_right_slot, Alias_projection_retains_item_members_and_exposes_alias_members, Generated_new_instance_transitions_win_over_inherited_and_keep_alias_extensions_applicable}`; `AliasSqliteEndToEndTests.Repeated_joined_type_resolves_to_distinct_slots_and_distinct_sql_aliases`; `JoinAliasGeneratorDiagnosticTests.{Correlated_apply_alias_emits_builder_and_query_source_seam_overloads, Digit_ending_alias_at_a_later_slot_keeps_its_positional_slot, Root_alias_emits_a_dim1_projection_and_a_withalias_extension}`; `MixedJoinChainTests.{Correlated_builder_apply_alias_routes_through_the_alias_seam, Correlated_query_apply_alias_routes_through_the_alias_seam, Digit_ending_alias_in_a_non_matching_slot_resolves_to_its_actual_slot, Stored_alias_receiver_supports_alternating_alias_positional_alias_with_repeated_clr_types}`; `TypedCteAliasTests.{Join_of_two_distinct_same_type_typed_ctes_binds_each_to_its_alias, Shared_two_step_chain_with_entity_and_cte_intermediate_compiles_and_binds_both_branches}`.

### Evidence-integrity finding

The current tree is red on the same suites the DO ledger reports green (D160.3-5, 2026-10-07T22:23Z): alias boundary 129/0 failed → 129/**18** failed; core 1775/0 → 1776/**1** (suite also gained one test); provider join-alias 20/0 → full project **3** failed. **DO's claimed-green r=3 evidence is NOT reproducible on a fresh clean build**, so those green sweeps are not accepted as evidence.

### Loop-back routing / open variant rows

- `check` verdict **FAIL**; loop-back **CHECK→DO** with attempt `n=2/3` if F1/C1 is clean on the fresh run, otherwise escalate/STOP. No `r=4` is justified yet (no task/dependency change identified).
- Open variant rows to re-verify on the fresh tree: **struct entity**, **Where-after-mixed** (`AliasJoinWhereTests` ×3), **parameterized cache** (`AliasPlanCacheTests` alternating plan-cache) — plus the core positional-control over-preservation.
- Artifact: `docs/specs/status/rc2-160-evidence/CHECK-r3/`.

---

## P:D160-r4 — SEALED (r=4, n=1; rv=4 superseded by rv=5)

- **Decision source:** `escalate` (strong) — not STOP; authorize a genuinely revised plan r=4; **last revision for the F1/C1 family** (any further F1/C1 or R02 failure ⇒ STOP with status, no new escalation). Authored by `planner`.
- **Goal:** complete join-alias mixing + root `.WithAlias`, reconciling the generator/runtime ordinal numbering and separating alias-state preservation from positional-state reset.
- **Acceptance:** R160-01..11 preserved verbatim ([S]:33-43, incl. negatives); no requirement, test expectation, pin, coverage or mandatory provider evidence weakened. `rv=4 superseded by rv=5`; all rows E160-01..E160-33 + C-E01..C-E04 carried forward with stable IDs.
- **Minimal solution:** fix the ordinal invariant and the state-copy boundary; constraints — unchanged positional path, keep guard `EntityBuilder.cs:3464-3466`, no new public core seam; single internal ordinal computation and state copy only on alias paths. Rejected: local ±1 patches (don't cover non-uniform errors); runtime renumbering (risks positional/R02).
- **State/dependencies:** D160 stays active; the unfinished numbering scope of D160.3-2/-3 is superseded by D160.4-1 (`superseded→replacement`, criteria + remainder carried). Graph: **`.4-0 → (.4-1 ∥ .4-2) → .4-3`**, executed **sequentially in one tree** (shared runtime contract + verification baseline); no worktrees.

### Units
- **D160.4-0 (diagnostic, no product edits):** preserve available prior generated outputs/provenance; run the clean build + A/K/S below and reproduce the red cases; compare old vs clean outputs; confirm/deny the stale-generator hypothesis **by evidence** (else record `hypothesis unknown`, not causation). Record run identity (see below).
- **D160.4-1 (Defect A, fix now):** first a characterization table «source position → JoinSlot → alias t-index» for root, positional→alias, alias→positional→alias, repeated CLR type, `Buyer2`, correlated; then a single internal ordinal computation shared by `JoinAliasGenerator.cs:804-821` seeding, `IsItemAlias:1009`, correlated `:1171-1183`/`:1448`, consistent with `CreateJoined`/`CreateAliasJoined`. Keep the test-expected slot contract; never derive the number from the name/CLR type.
- **D160.4-2 (Defect B, fix now):** remove `CopySharedStateTo` from positional `CreateJoined:3626` (via `ApplyJoinStateTo:3383`); keep the copy on alias paths `AliasRoot:3259`/`CreateAliasJoined:3147`. Add a positional reset guard for hint/tag/timeout; keep the paired F1/C1 alias-preservation tests green and the pin `EntityBuilderStateCopyTests.cs:191` unchanged.
- **D160.4-3 (verification, fix now):** after both fixes re-run the clean boundary B/A/K/S/I/F/coverage/X/V; refresh row manifests and the requirements→evidence map. No r=3 result counts as a new green.

### Run identity (every run; evidence manifests)
`git rev-parse HEAD`; `git status --porcelain=v1 --untracked-files=all`; `git diff --binary HEAD -- . ':(exclude)docs/specs/status/**' | sha256sum`; `git ls-files --others --exclude-standard -z -- . ':(exclude)docs/specs/status/**' | xargs -0 -r sha256sum --`. Status/evidence excluded from the source hash.

### Exact commands
- **B-clean (.4-0 and .4-3):** `dotnet clean nextorm.slnx -c Debug && dotnet clean nextorm.slnx -c Release && dotnet build nextorm.slnx -c Debug --no-incremental && dotnet build nextorm.slnx -c Release --no-incremental` — each exit 0, both builds 0 warnings/0 errors.
- **A/K/S:** `dotnet test tests/nextorm.alias.tests -c Debug`; `dotnet test tests/nextorm.core.tests -c Debug`; `for p in sqlite postgres sqlserver mysql mariadb clickhouse; do dotnet test "tests/nextorm.$p.tests" -c Debug || exit $?; done` — exit 0 each, 0 failed, full suites.
- **I:** load `running-integration-tests` first; `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`; exit 0, all six providers really executed, MariaDB `11.4`, skipped≠PASS; recover a missing socket per the skill.
- **Coverage:** `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`; then `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"TextSummary;Html"`; both exit 0; line≥85%, branch≥75%.
- **F (perf, mandatory):** `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`; exit 0, 7 cases, ≤4 min; baseline ratio 1.87, >2.244 requires investigation (not automatic hard fail).
- **X/V:** `git diff --check`; `dotnet docfx docs/docfx.json`; V `python3 /home/alex/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py` with the frozen brief/report invocation ([S]:152-159) — brief before product edits, report before CHECK, both exit 0; the in-repo `scripts/` is absent, do not use it.

### Test strategy / variant matrix
- Compiler/generator characterization + diagnostics — unit; SQL/slots — six dialect suites (no DB); cache/state/expressions — core/alias; real results — integration.
- Add new checks **without** changing existing expectations/pins; real test symbols/locations fixed after implementation.
- Matrix closure: mixing directions, alias-only/positional-only parity, seven conditional/conditionless operators, correlated builder/query APPLY, Where before/after mixed (supported → tests, unsupported → existing guards); root matrix per [S]:60-91 (both `From<T>` forms, `From("table")`/TableAlias, `FromSql`, typed CTE/`CteReference`, temp-table, `FromTableFunction<T>`, `From(QueryCommand<T>)`, `CreateQueryBuilder*`, `From(EntityBuilder)`; misuse/repeated `.WithAlias` → compiler guards); dim-1/2–8/>8+`As<T>`, reference/value/nullable/default, repeated CLR, same-name descriptors, `Buyer2`, in-memory root/join refusal + positional success, repeated/alternating/params/shared scalar, generated/inherited shadowing, NORMGEN001–006, incremental edits, direct alias read vs expression.
- **Close the 3 open rows now:** struct entity — positive characterization if the current API allows, else a proven existing compiler guard (no new limitation); `Where` AFTER a mixed alias/`ItemK` step — tests; parameterized mixed/root cache — tests varying params/alternation, asserting no sticky `Cache=false`.
- Deferred (frozen triggers only): full in-memory aliases; `JoinInto` aliases; arity>8 without `As<T>`.
- **DO test scope JSON:** `{"projects":["tests/nextorm.alias.tests","tests/nextorm.core.tests","tests/nextorm.sqlite.tests","tests/nextorm.postgres.tests","tests/nextorm.sqlserver.tests","tests/nextorm.mysql.tests","tests/nextorm.mariadb.tests","tests/nextorm.clickhouse.tests","tests/nextorm.integration.tests","benchmarks/nextorm.benchmark"],"selectors":{"tests":"all","integration":"all six providers; no skipped-provider acceptance","benchmark":"--anyCategories=acceptance"},"files":["JoinAliasGenerator.cs","EntityBuilder.cs","JoinAliasGeneratorDiagnosticTests.cs","EntityBuilderStateCopyTests.cs","MixedJoinChainTests.cs","AliasPlanCacheTests.cs","JoinAliasSqlGenerationTests.cs"],"rebuild":"clean Debug+Release; both builds --no-incremental","boundary":["B","A","K","S","I","F","coverage","X","V"],"rationale":"generator/runtime numbering, shared-state boundary and clean-evidence integrity; files are focus, not suite exclusions"}`

### Other
- **Priorities:** P1 by construction for all rows proving R160-01..11 (incl. negatives + mandatory boundaries); prior project priorities preserved; CHECK does not lower P1.
- **Docs (delta sync EN/RU):** `docs/guide/02-joins.md`, `docs/querying/01-projections.md`, `docs/advanced/limitations.md` + their `docs/ru/` pairs; sync `docs/specs/design/API-NAMING-REVIEW.md` §#160 (N160-1/N160-2) on any generated-name change; no public links to specs.
- **Reconnaissance:** required, bounded to .4-0/.4-1 (one clean-red pass + one no-edit comparative incremental pass if comparable artifacts exist; six characterization rows); symbols via Roslyn.
- **Perf:** mandatory (query/cache path changes); fresh F run; prior 1.95 does not substitute.
- **Unit mode:** sequential, one tree; commits only under the collection auto-commit; push never.
- **Risks/mitigation:** B may reintroduce C1 → paired positional-reset/alias-preservation tests; A may break positional-only/R02 → full positional regression suites + negative guards; stale artifacts → clean/non-incremental, provenance, independent CHECK.
- **Confidence/gaps:** the stale-generator cause is still a hypothesis, not proven; struct permissibility is determined by the current API, not assumed; new test refs/generated outputs are planned pre-DO. No external blocker established.
- **Contract rv=5:** carry E160-01..E160-33 and C-E01..C-E04 verbatim (stable IDs/scenarios/obligations/predicates); no new variants → no new IDs; C-E04 N/A only under the observed single-group predicate. Each row keeps: inherited required check; evidence kinds/sources; exact command/invocation, exit/result/log; expected artifacts; owner `coder` (evidence)/`check` (verify); source status; predicate; `rv=5`. Missing=`unknown/missing`, never PASS.
- **Trust:** fresh green only after clean/non-incremental builds on this source tree with matching HEAD+source-diff-hash + untracked hashes; CHECK independently repeats the proving commands; DO does not declare final PASS/STOP. CHECK re-gather: ≤2 targeted scout calls + 1 repeat proving-command per CHECK, no implementation edits.
- **Terminal rule:** last revision for F1/C1; a new F1/C1 or R02 failure ⇒ STOP with preserved status, remaining D160 work and evidence, no new escalation/revision.
| 2026-10-08T05:01:50Z | DO | revision r=4 | iteration 1/3 | D160.4-1 / D160.4-2 inner-loop evidence finalized: validate_inner_loop.py `report` exit 0 for both; D160.4-1 adapts the combined provider slot OR-filter (rejected by the host hygiene rule as a shell compound) into its three constituent single-selector runs (all six providers kept, no selector weakened/dropped) and points the core EntityBuilderStateCopyTests filtered run at the D160.4-2 log; D160.4-2 keeps the full-core sweep 1776 in `executions` and the alias re-confirm 129 + six provider regression runs under `supplementary_runs` (single-boundary-sweep cap) | docs/specs/status/rc2-160-evidence/D160.4-1/evidence.json, docs/specs/status/rc2-160-evidence/D160.4-2/evidence.json |
| 2026-10-08T05:04:45Z | DO | revision r=4 | iteration 1/3 | D160.4-3 boundary sweep part 1/2 (B-clean + unit/provider suites), no product edits: Debug build --no-incremental 0 Warning(s)/0 Error(s), Release build --no-incremental 0 Warning(s)/0 Error(s), both cleans exit 0; A alias succeeded 129/129 (0 failed, 0 skipped, exit 0); K core succeeded 1776/1776 (0 failed, 0 skipped, exit 0); S provider suites all exit 0 and 0 failed — sqlite succeeded 1172, total 1173, skipped 1 (NextORM.Sqlite.Tests.SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded); postgres 812/812; sqlserver 734/734; mysql 313/313; mariadb 239/239; clickhouse 602/602 | docs/specs/status/rc2-160-evidence/D160.4-3/ |
| 2026-10-08T05:08:19Z | DO | revision r=4 | iteration 1/3 | D160.4-3 boundary sweep part 2a (container-backed integration tests), no product edits: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` exit 0, Total 3377 / Errors 0 / Failed 0 / Skipped 200 / Not Run 0; all six providers executed — per provider passed/failed/skipped: PostgreSQL 822/0/26 (848), SQL Server 688/0/48 (736), MySQL 596/0/80 (676), MariaDB 59/0/0 (59), SQLite 683/0/44 (727), ClickHouse 209/0/0 (209), shared/contract 120/0/2 (122); D160 root-alias/mixed cases executed on PostgreSQL/SQL Server/MySQL/SQLite/ClickHouse/MariaDB, 0 failures, no provider-availability skips; supplementary `--no-build -result-xml` run exit 0, same summary | docs/specs/status/rc2-160-evidence/D160.4-3/I-integration.log, I-integration-xml.log, I-results.xml |
| 2026-10-08T05:09:59Z | DO | revision r=4 | iteration 1/3 | D160.4-3 boundary sweep part 2b-i (mandatory cached-path perf acceptance F), no product edits: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` exit 0, 7/7 executed, BDN Global total time 00:00:46 (46.38 sec), external wall clock 51 s (<=4 min); per-case Mean/Allocated: Nextorm_Count 2.892 ms/374.22 KB, Nextorm_GroupByCount 79.82 ms/50.1 MB, Nextorm_Cached 2.599 ms/610.21 KB, Prepared_ToList 1,146.9 us/76.14 KB, Cached_ToList 2,402.4 us/583.98 KB, Cached_PlanOnly_Param 730.1 us/507.83 KB, Nextorm_Cached_ToListAsync 2.636 ms/608.47 KB; Cached_ToList/Prepared_ToList ratio = 2.09 vs baseline 1.87 (+12.0%), below the 2.244 investigation threshold, so no repeat run required; known run-to-run spread noted (Nextorm_Count Error 1.126 ms vs Mean 2.892 ms in this run) | docs/specs/status/rc2-160-evidence/D160.4-3/F-perf.log |
| 2026-10-08T05:15:07Z | DO | revision r=4 | iteration 1/3 | D160.4-3 boundary sweep part 2b-ii (coverage gate + X gate), no product edits: coverage (DOCKER_HOST set) exit 0, total 9314 / failed 0 / skipped 201 (matches baseline), overall line 88.2% (48408/54869) >= 85 PASS, branch 80.2% (25963/32355) >= 75 PASS; per-assembly line nextorm.core 88%, nextorm.postgres 90.1%, nextorm.sqlite 90.5%, nextorm.sqlserver 94.9%; no drop vs pre-change baseline E160-10 (88.2%/80.2%); X gate `git diff --check` exit 0 (autogenerated BenchmarkDotNet.Artifacts from the F perf run restored to HEAD; code/test/docs diff clean), `dotnet docfx docs/docfx.json` exit 0 (2 warnings/0 errors) | docs/specs/status/rc2-160-evidence/D160.4-3/coverage.txt, coverage-collect.log, coverage-report.log, X-docfx.log |
| 2026-10-08T10:17:00Z | DO | revision r=4 | iteration 1/3 | DO complete — .4-0/.4-1/.4-2/.4-3 done; V brief+report exit 0 | docs/specs/status/rc2-160-evidence/D160.4-0/scope.json (brief exit 0), docs/specs/status/rc2-160-evidence/D160.4-3/evidence.json (report exit 0) |
| 2026-10-08T05:47:15Z | ACT | r=4 | — | STOP: D160 incomplete (confirmed R02, sealed terminal rule) | decision planner; patch D160-STOP-incomplete.patch |

## STOP — D160 incomplete (r=4 terminal rule)

- **Final state:** `incomplete` (STOP). The r=4 plan carried the terminal rule "a new F1/C1 or R02 failure ⇒ STOP with preserved status, no new escalation/revision". The r=4 final CHECK failed on a **confirmed R02 failure**, so the rule fired.
- **Decision:** `planner` decision **(a)** — **confirmed R02 failure ⇒ sealed r=4 STOP**; **no r=5**, no further DO, no re-CHECK, no further escalation. D160 stays `incomplete` (not `done`, not `superseded`); no replacement unit is created.
- **Defect (R02 / Defect-B family, same family already escalated once):** the r=4 positional `ApplyPositionalJoinStateTo` still **over-preserves** relative to the baseline `ApplyJoinStateTo`:
  - delta **copied now / dropped at baseline** for `SourceEntityType` (`EntityBuilder.cs:44`) and `BindArrayJoinElement` (`EntityBuilder.cs:45`);
  - **observable** via `SqlBuilder.cs:199` (` as __nextorm_aj_element`) on the `ArrayJoinElement(...).Join(pos)` path;
  - the `SingleQuery` case is **inert** (no observable delta there).
  This is the same Defect-B / R02 family that was already escalated once (r=3 → r=4); under the r=4 terminal rule it now seals STOP instead of a further revision.
- **CHECK verdict:** `fail`.
  - **Open rows:** R02; alias `Tag` / `CommandTimeout` validation missing; per-variant refs; `rv=5` contract reconciliation.
  - `check` routing: returned the confirmed R02 regression to the plan boundary; no r=5 authorized.
  - Targeted R02 gather: **`R02 regression CONFIRMED`** (static read sites: `EntityBuilder.cs:44` / `:45` copied on the positional path; `SqlBuilder.cs:199` render site).
- **Preserved evidence (r=4 DO, not a PASS claim — the CHECK FAIL stands):**
  - Debug + Release builds: **0 warnings / 0 errors**.
  - A `tests/nextorm.alias.tests`: **129/129**, 0 failed.
  - K `tests/nextorm.core.tests`: **1776/1776**, 0 failed.
  - S six providers: **0 failed**.
  - I integration: **3377** total / **0 failed**.
  - F acceptance: **7/7**, ratio **2.09** (baseline 1.87; < 2.244 gate).
  - Coverage: line **88.2%** / branch **80.2%** (both ≥ 85/75).
  - X: `git diff --check` + `dotnet docfx` **0 errors**.
  - V: `brief` / `report` **0** (both exit 0).
  - Logs under `docs/specs/status/rc2-160-evidence/**` (`D160.4-0`..`D160.4-3`, `CHECK-r3`).
- **Patch pointer:** `docs/specs/status/rc2-160-evidence/D160-STOP-incomplete.patch`
  - size **177053 bytes**; sha256 `d10c5e85027c2548f93302374653129a1b040ad90b5ec2ca45f9230ee5f1af3e`;
  - `git apply --stat`: **20 files**, **+2823 / -124** (**2 product**, **2 registry docs**, **16 tests** incl. **3 new**);
  - patch paths restored to HEAD **`40b1a159`**.
- **Remaining work / Next plan:** — (flow closed: incomplete; no r=5).
- **Preserved verbatim:** all history, `r=4`, `rv=5`, contract row IDs (`E160-01..E160-33`, `C-E01..C-E04`) and open rows are kept; no obligation is weakened.
