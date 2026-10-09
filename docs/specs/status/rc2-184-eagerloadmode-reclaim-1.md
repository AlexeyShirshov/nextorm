# rc2-184-eagerloadmode-reclaim-1 — LoadWith: fold AsSingleQuery into an EagerLoadMode parameter

- collection: rc2-reclaim (single lane; current worktree/branch 1.0.9-rc2)
- selected_variant: pdca-dotnet; cycle N=1; plan_revision r=1; evidence contract rv=2
- baseline: cf34f910; issue #184 (milestone 1.0.9-rc2 #20)
- supersedes: prior terminal attempt docs/specs/status/rc2-184-loadwith-eagerloadmode-1.md (r=2, evidence-contract gap; preserved unchanged)
- plan_state: ready (persisted at PLAN->DO boundary; DO not started in P-phase)
- saved reference patch (NOT evidence): artifacts/pdca/rc2-184/D184-blocked.patch

## Goal
Replace public `AsSingleQuery()` with `LoadWith(..., EagerLoadMode mode = EagerLoadMode.Default)`; whole-builder mode selection; `Default` inherits; no selection = split; conflicting explicit selections rejected; remove old API; migrate consumers/tests/EN+RU docs. Recover the product-green implementation via a guarded patch applicability check and replace the incomplete evidence contract with a complete executable crosswalk.

## Acceptance criteria (each with negative)
- R184-01 enum Default=0/SplitQuery=1/SingleQuery=2 + unsupported `ArgumentOutOfRangeException` (neg: unknown must not silently select).
- R184-02 SingleQuery preserves former single-query SQL/results, one denormalized command (neg: duplication/missing relationship/extra command).
- R184-03 omitted/Default/SplitQuery = split; empty + 1001-key negatives (neg: broken chunk/filter/extra child queries).
- R184-04 whole-builder effect incl. earlier/later LoadWith; Default inherits (neg: not per-navigation, no reset).
- R184-05 equal explicit selections allowed; opposing throw `NotSupportedException` (neg: silent override).
- R184-06 copies/modifiers preserve mode, no leakage (neg: copy mutation of original).
- R184-07 `AsSingleQuery` absent, consumers migrated, build 0W/0E (neg: shim/unresolved ref/obsolete sample).
- R184-08 executed scenarios + full integration + coverage line>=85/branch>=75 (neg: zero-selected/unavailable-provider/coverage-below).
- R184-09 EN+RU docs/examples/internal disposition consistent (neg: old API examples/broken or public->spec links).

## DO task list (sequential; deps in order)
- D184-01 contract/preconditions: persist rv=2; link #184/#20; load required skills/integration instr; verify toolchain/assets/tools/provider readiness. Unconditional.
- D184-02 baseline+applicability: record HEAD/worktree; baseline build+core+sqlite; verify patch manifest/size 206698B; `git apply --check --exclude=docs/specs/status/collection-1.0.9-rc2.md artifacts/pdca/rc2-184/D184-blocked.patch`; classify overlaps.
- D184-03 recover implementation: apply guarded patch (exclude stale collection-status hunk) or port; recover enum/whole-builder state/validity-conflict rules/copy propagation/API removal over builder/join/expression footprint.
- D184-04 scenario tests+consumers: recover/migrate core/sqlite/integration/benchmark consumers; close V01-18 gaps using actual behavior; discover real test symbols via Roslyn (no predeclared symbols).
- D184-05 docs/internal disposition: recover EN/RU advanced/{eager-loading,limitations,query-filters,relationships}.md + docs/index.md (+RU), roadmap evidence-01; update current records.
- D184-06 gates+ledger: final build, selected/full tests, container integration, coverage, DocFX; record per row ID+rv.
- D184-07 CHECK handoff: scenario mapping + design/perf audits + full collection schema crosswalk.
- Deferred: new key types (trigger: requested support / regression in a supported type); ClickHouse eager-specific extension (trigger: feature request / behavior change). Full container integration NOT deferred.

## Variant matrix (disposition)
V01 omitted/Default/Split one collection=test(R03); V02 Single one/many=test(R02); V03 Default->Single=test; V04 Single/Split->Default inherits=test; V05 equal explicit=test; V06 opposing->NotSupportedException=test; V07 unknown enum->ArgumentOutOfRangeException=test; V08 choice->copy->extra LoadWith(Default)=test; V09 non-stitching terminals=test+guard; V10 empty/duplicate=test; V11 existing value/ref/null keys=test/guard (new key types deferred); V12 split 1001 keys=test; V13 sync/async=test; V14 in-memory/SQLite no DB=test; V15 SQLite/PG/SS/MySQL live=test; V16 ClickHouse live gate=test (eager extension deferred); V17 XML/bench/tests/docs/inherited=test/guard; V18 legacy single call without LoadWith=test/guard.

## Test strategy
Baseline before edits: B, C-CORE, C-SQLITE. Focused core selectors NextORM.Core.Tests.{EagerLoadingTests,EagerLoadingSingleQueryTests,EagerLoadingSingleQueryP1Tests,JoinIntoManyToManySingleQueryTests}; sqlite NextORM.Sqlite.Tests.EagerLoadingSqlGenerationTests; full core/sqlite after edits; full solution unit run; full CommonTestSuite integration (load integration skill; recover podman socket per skill, no provider-skip pass); coverage line>=85/branch>=75 (aggregate; main-only hard-fail does not waive this cycle's criterion). Each required scenario needs an executed-test mapping (positive class count alone insufficient).

## Docs plan
Touch EN: docs/advanced/{eager-loading,limitations,query-filters,relationships}.md, docs/index.md, XML comments/examples/benchmark consumers, docs/specs/roadmap/evidence-01-eager-loading-single-query-api.md + 4 RU counterparts. Do NOT touch historical terminal status. No public->docs/specs links.

## Performance decision
Not needed IF the recovered diff is builder-time only (anchors EntityBuilder.cs:542:35, replaced API :591:35). S-PERF must substantiate that the patch's join/expression/stitching files change only builder-time mode selection/state propagation. Trigger: any new execution/cache/stitching/per-row behavior -> return to PLAN and pin a relevant benchmark.

## Reconnaissance decision
No spike. Design + targeted recon supplied. Patch applicability + semantic audits are bounded verification. If the patch cannot safely represent the intended behavior -> observed failure triggers replan.

## Unit execution mode
Sequential, one tree. Builder contracts/shared tests/docs/collection records overlap; no worktrees/parallel writes. Autocommit limited to task files.

## Design-checklist verdict
Acceptable subject to execution evidence: minimal public addition (enum + optional param, no shim); centralized whole-builder validity/conflict; explicit copy propagation/isolation; no new hierarchy; no per-call mutation of shared QueryCommand.Cache/sticky cache-disable; no TVP/decimal changes; CRLF/warnings-as-errors/async-naming preserved; no unrelated refactor.

## Evidence contract (rv=2; unconditional unless split noted; owner coder unless scout/check)
Artifact root A = artifacts/pdca/rc2-184/reclaim-1.
Commands: C-ID `git rev-parse HEAD`/`git branch --show-current`/`git status --short`/`dotnet --info`; C-PATCH `wc -c <patch>` + `git apply --stat <patch>` + `git apply --check --exclude=docs/specs/status/collection-1.0.9-rc2.md <patch>`; C-APPLY (only after guard passes) same with --exclude; C-BUILD `dotnet build --no-restore`; C-CORE filtered core (EagerLoadingTests|EagerLoadingSingleQueryTests|EagerLoadingSingleQueryP1Tests|JoinIntoManyToManySingleQueryTests); C-CORE-ALL full core; C-SQLITE filtered sqlite; C-UNIT `dotnet test --no-build --verbosity normal`; C-INTEGRATION `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`; C-AVAILABLE `grep -c "is not available" A/integration.log`; C-COVERAGE `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"`; C-REPORT `dotnet tool run reportgenerator -reports:tests/coverage/coverage.cobertura.xml -targetdir:tests/coverage/report -reporttypes:"Html;TextSummary;Cobertura" -riskhotspotassemblyfilters:"+nextorm.*"`; C-DOCFX `dotnet docfx docs/docfx.json`; C-DOC-SCAN `git grep -n -e AsSingleQuery -e 'docs/specs/' -- docs docs/ru readme.md`; C-DIFF `git diff --check`/`--stat`/`git diff`.
- E184-01 preconditions/skills/CI/GitHub -> C-ID + S-PRE -> preconditions.md/identity.log.
- E184-02 baseline+applicability -> C-BUILD,C-CORE,C-SQLITE,C-PATCH -> baseline/, patch-guard.md/.log.
- E184-03 mode/state V01-09,V13 -> C-CORE-ALL + S-MAP -> core-all.log, case-map.md.
- E184-04 SQL/chunk/filter V02,V10,V12 -> C-CORE,C-SQLITE,C-INTEGRATION,S-MAP.
- E184-05 full container integration -> C-INTEGRATION,C-AVAILABLE -> integration.log, availability.log (grep exit 1 on zero).
- E184-06 API removal/migration -> S-API,C-BUILD -> api.md, build.log.
- E184-07 coverage -> C-COVERAGE,C-REPORT -> 85/75.
- E184-08 EN/RU/public refs -> C-DOC-SCAN,S-DOC,C-DOCFX.
- E184-09 contract/supersession/scope -> C-DIFF,S-CONTRACT.
- E184-10 perf applicability -> S-PERF -> perf-applicability.md.
- E184-11 design/sealedness/cache -> S-DESIGN -> design.md.
- R184-01..09 acceptance rows A184-01..09 mapped to the above commands.
- AC1..AC5 rows: build 0W/0E; unit 6247=6246+1skip/0fail; integration 3555/0err/0fail/197skip + availability 0; coverage 86.8/79.4 >=85/75; docfx 2w/0e.
CHECK re-gather budget: 2 rounds, owner check. Prior gap fixed by: complete slots on every E/R/V/AC row; stable row->requirement + scenario->evidence crosswalk; explicit V11/V16 dispositions; collection §2/§3/§4/§4b/§5; ledger rows tagged row ID+rv=2 incl. failed/not-run/blocked; CHECK verifies artifact existence + full applicability. Missing evidence != product defect.

## Risks / assumptions
- Patch applicability to cf34f910 not assumed; dry-run + semantic guard decides apply vs port.
- Uncommitted changes preserved; unrelated overlapping edits block apply -> port.
- New test symbols discovered post-recovery via Roslyn; never counted as evidence.
- Supported key-type boundary preserved; new types deferred.
- Pinned totals/coverage retained; observed drift -> explicit reconciliation, not auto-lowering.
- Historical product-green verdict is recovery context only, not current-rv evidence.
- Prior classification: evidence-contract insufficiency, not product defect/external blocker; no escalation now.

## Handoff metadata
- Write footprint: patch's builder/eager-loading/join/expression/enum files; core/sqlite/shared integration tests; benchmark consumer; EN/RU articles/index; roadmap evidence-01; new status + collection records. Exclude patch's stale collection-status hunk.
- Footprint uncertainty: final port may touch a smaller subset or a newly discovered consumer; expansion requires PLAN.
- Predecessors: preserve R184-01..09 obligations; prior terminal status immutable.
- Refs: patch artifacts/pdca/rc2-184/D184-blocked.patch; bases 18659e41/4bd18c82; baseline cf34f910; prior status rc2-184-loadwith-eagerloadmode-1.md; contract docs/specs/status/collection-1.0.9-rc2-C-evidence.md; issue https://github.com/AlexeyShirshov/nextorm/issues/184.

## Prior attempt (superseded, preserved)
rc2-184-loadwith-eagerloadmode-1.md: r=2, rv=1, blocked — evidence-contract gap (product gates green; CHECK STOP on recurrence). No product defect.

## Progress log
- PLAN persisted (r=1, rv=2) — collection P-phase boundary; plan_state=ready; DO not started.
