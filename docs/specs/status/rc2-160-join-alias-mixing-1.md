# D160 — Join aliases: free mixing positional/alias + root alias (.WithAlias)

- task_id: D160
- GitHub issue: #160 — https://github.com/AlexeyShirshov/nextorm/issues/160
- branch: 1.0.9-rc2
- selected_variant: pdca-dotnet
- cycle_id: 1
- plan_revision: 2
- contract_revision: rv=3
- attempt: n=1/3
- phase: DO
- plan_state: r=2 revision issued by planner; rv=2 superseded by rv=3 (P:160-ROOT-CONTRACT sealed to bounded in-scope fix (iii)). All acceptance criteria R160-01..11 preserved; no rows/obligations weakened.
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
| E160-07 | R08 | green (Phase-1 checkpoint) | `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors; log `rc2-160-evidence/E160-07/build.log` |
| E160-08 | R09 | green (DO scope) | `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` exit 0, Total 3357 / Failed 0 / Skipped 197; per-provider executed PostgreSQL 845, SQL Server 733, MySQL 673, SQLite 723, MariaDB 55, ClickHouse 205 (+shared/contract 123); 20/20 D160 root-alias/mixed cases pass (5 × PostgreSQL/SQL Server/MySQL/SQLite); ClickHouse/MariaDB root-alias = SQL-gen E160-02 (suite-membership boundary); logs `rc2-160-evidence/E160-08/` |
| E160-09 | R07 | pending DO→CHECK (acceptance benchmarks) | |
| E160-10 | R08 | pending DO→CHECK (coverage 85/75) | |
| E160-11 | R10 | green (EN+RU docs synced; docfx exit 0) | `docs/guide/02-joins.md`+RU, `docs/querying/01-projections.md`+RU, `docs/advanced/limitations.md`+RU; `dotnet docfx docs/docfx.json` exit 0 (2 pre-existing warnings / 0 errors) `/tmp/opencode/rc2-160-docs/docfx.log`; `git diff --check` clean on touched paths |
| E160-12 | R01..R11 | green this session (brief + report) | `validate_inner_loop.py brief …→exit 0; report …→exit 0` (1 inner build + 9 inner filtered + 1 boundary sweep + 1 boundary solution build); `rc2-160-evidence/{brief,report}.json` |
| E160-13 | R01,R04,R06,R07,R11 | pending (six reversible mutations at boundary) | |
| E160-14 | R03,R04 | green (spike closed; boundary K pending) | `dotnet build tests/nextorm.alias.tests -c Debug` exit 0; `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~RootAliasTests` exit 0, selected 3 / passed 3 / failed 0; log `rc2-160-evidence/E160-14/spike.log` |
| E160-15 | R01 | green (Phase-1 partial) | alias→positional + digit-alias negative; `mixed-filtered2.log` selected 7 |
| E160-16 | R01 | green (Phase-1 partial) | positional→alias in `MixedJoinChainTests` |
| E160-17 | R01 | green (Phase-1 partial) | alternation in `MixedJoinChainTests` |
| E160-18 | R02 | green (Phase-1 partial) | mixed vs positional parity in `MixedJoinChainTests` |
| E160-19 | R06 | green (Phase-1 partial) | in-memory refusal + pure-positional-not-refused negative |
| E160-20 | R11 | green (Phase-1 partial) | receiver static-type binding negative; `dotnet test … --filter FullyQualifiedName~AliasProjectionShapeTests` exit 0, selected 3; `shape-filtered.log` |
| E160-21 | R11 | pending (overload shadowing audit, D:160-04) | |
| E160-22 | R11 | green (Phase-1 partial; diagnostic suite) | |
| E160-23 | R03 | green (DO scope: alias `RootAliasTests` 13/13 + `MixedJoinChainTests` 7/7; six-provider root-alias SQL green; repeated/invalid root alias rejection covered) | `rc2-160-evidence/E160-05/`, `rc2-160-evidence/E160-02/`, `rc2-160-evidence/E160-25/`, `rc2-160-evidence/E160-26/` |
| E160-24 | R10 | green (D:160-04 register entry complete) | `docs/specs/design/API-NAMING-REVIEW.md` §#160 (N160-1/N160-2; kept rename mechanism-required `JoinAliasGenerator.cs:748,763,768,929,932,943,1077,1195`; nothing reverted); spec-path + AGENTS.md fixes |
| E160-25 | R03 | green (rv=3) | `From(builder)` root `.WithAlias` preserves derived source; `dotnet test tests/nextorm.alias.tests -c Debug --filter FullyQualifiedName~RootAliasTests` exit 0, selected 13 / passed 13 / failed 0; SQL `select t2.Id from (select Id, BuyerId, ApproverId from orders) as 't1' join person as 't2' on t1.BuyerId = t2.Id`; logs `rc2-160-evidence/E160-25/` |
| E160-26 | R03 | green (rv=3) | `From(QueryCommand<T>)` root `.WithAlias` preserves derived query; same filtered run exit 0, 13/13; SQL `select t2.Id from (select Id, BuyerId, ApproverId from orders where Id = 1) as 't1' join person as 't2' on t1.BuyerId = t2.Id`; logs `rc2-160-evidence/E160-26/` |

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
