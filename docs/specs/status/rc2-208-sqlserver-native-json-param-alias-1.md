# Task T208 / #208 — SQL Server native JSON: projected SqlFunctions.Parameter<T> alias

- collection: `rc2-final`
- intent: own PLAN (COLLECTION TASK PLAN); phase P (no DO yet)
- selected_variant: `pdca-dotnet`
- cycle_id: N=1
- plan_revision: r=1
- baseline: `2ac20818`
- planning HEAD: `9a2a2871`
- plan_state: ready
- status file: `docs/specs/status/rc2-208-sqlserver-native-json-param-alias-1.md`
- persisted: 2026-10-09

---
# T208 — SQL Server native JSON: alias projected parameters
- Collection `rc2-final`; issue #208; variant `pdca-dotnet`; cycle N=1; plan revision r=1.
- Branch `1.0.9-rc2`; planning HEAD `9a2a2871`; regression baseline `2ac20818`.
- Status: `docs/specs/status/rc2-208-sqlserver-native-json-param-alias-1.md`; `plan_state=ready`.
- Planning only: persist this plan now; no implementation, tests, commits, or merges in this lane. Implementation prerequisites below remain mandatory.

## Goal and acceptance criteria
Restore native SQL Server JSON execution for eligible object projections containing `SqlFunctions.Parameter<T>`, without disabling native eligibility or changing query/cache state.
- **A208.1 — Alias:** every admitted projected parameter receives its exact projected-member alias. Negative: a bare `@pN` select item must fail the regression assertion; parameter aliases must not appear in WHERE expressions.
- **A208.2 — Execution:** eligible projected parameters execute as native SQL Server JSON and produce the expected parsed JSON. Negative: neither the unnamed-column SqlException nor silently selecting fallback counts as success.
- **A208.3 — Surfaces/state:** sync and async execution on `QueryCommand<TResult>` and `EntityBuilder<TEntity>` bind changed values correctly across repeated calls. Negative: stale values, leaked parameters, or sticky cache-policy changes fail.
- **A208.4 — Boundaries:** existing shape eligibility, unsupported-shape handling, ordinary SELECT behavior, and non-SQL-Server fallback remain valid. Negative: admitting an unsupported shape or forcing another provider onto SQL Server syntax fails.
- **A208.5 — Evidence/quality:** the same projected regression is red before the fix and green afterward; build, applicable tests, container providers, coverage and acceptance benchmarks have complete evidence. Negative: zero selected tests/benchmarks, skipped required providers, or absent reports are not passing evidence.

## Minimal solution and alternatives
- **Essence:** correct alias bookkeeping for a projected parameter expression.
- **Constraints:** preserve eligibility, exact aliases, binding, provider boundaries and cache policy; no serialization workaround or broad renderer rewrite.
- **Optimum:** use the existing alias contract at the expression translator, subject to confirming its projection/reset lifecycle.

| Approach | Benefit | Cost/risk | Decision |
|---|---|---|---|
| Mark the Parameter arm alias-required | Small semantic fix; downstream alias machinery already exists | Shared SELECT translation; verify flags do not leak into WHERE or later columns | Selected |
| Require aliases centrally under `ExactProjectionAliases` | Restricts behavior to exact-alias rendering | Broader renderer policy and other expression cases | Alternative only if scout disproves the selected lifecycle |
| Reject projected Parameter as native-ineligible | Avoids the exception | Loses admitted native behavior and violates A208.2 | Rejected |

Do not invent `ColumnName`, mutate commands, or change eligibility merely to make tests green. A necessary change of approach returns to PLAN.

## Findings: fix now / deferred
- **Fix now:** missing parameter-expression alias signal, `NormSqlTranslator.cs:71-81`; resulting rendering at `SqlSourceRenderer.cs:1115-1136` and `SqlBuilder.cs:511-518`.
- **Fix now:** absent projected-Parameter regression in the SQL-generation, eligibility and live SQL Server test homes.
- **Preserve/test:** admission at `JsonShapePlan.cs:348-368` and `JsonNativeStream.cs:58-96`; admission is not itself evidence of an eligibility defect.
- **Deferred:** unrelated slash-escaping assertion from the historical log; trigger: reproduce independently after parsing JSON rather than comparing escaped text.
- **Deferred:** general unnamed-expression alias audit; trigger: another admitted expression fails the focused alias contract. Do not broaden T208 proactively.

## What the statement did not supply
- Authoritative `pdca-dotnet` contract schema, numbered nextorm invariants 1–10, class-priority registry and integration skill: **D208.0 prerequisite** obtains their text and records applicability.
- Alias-flag reset/projection lifecycle: **targeted Roslyn scout**, not an assumption.
- Array/root eligibility and null-output rules: establish from existing contracts/tests before selecting expected assertions.
- Issue URL, predecessor IDs and shared-file reservations: obtain collection registry evidence.
- Baseline test compatibility, acceptance benchmark coverage, tools and container socket: D208.0 verifies them.

## Tasks and execution mode
- **D208.0 — Prerequisites, fix now:** scout the flag lifecycle and existing shape contracts; load required skills/registries; verify issue tracking, baseline and tools; reserve shared files. Freeze the evidence manifest before implementation.
- **D208.1 — Regression, fix now:** add tests to the three test homes; tag focused projected regressions with `PDCA=T208`. Produce a test-only baseline patch and red evidence against `2ac20818`.
- **D208.2 — Implementation, fix now:** repair alias signaling at `NormSqlTranslator.cs:71-81`; retain existing downstream alias selection. Run focused alias, binding, WHERE and ordinary-SELECT tests.
- **D208.3 — Verification, fix now:** complete the variant matrix, full provider evidence, coverage and acceptance benchmark comparison; produce the manifest and handoff.
- **Mode:** sequential, single lane, current worktree. Shared translators/renderers prohibit concurrent edits.
- **Footprint:** expected production write `src/nextorm.core/Visitors/NormSqlTranslator.cs`; tests `SqlServerNativeJsonSqlTests.cs`, `SqlServerNativeJsonStreamTests.cs`, `JsonShapeWriterTests.cs`; status and `artifacts/pdca/D208/rv1/**`.
- **Uncertainty:** `SqlSourceRenderer.cs`/`SqlBuilder.cs` only if the selected contract cannot carry the alias; that requires replan.

## Test strategy and closed variant matrix
Unit/SQL-generation tests for deterministic alias and eligibility checks; live integration for SQL Server FOR JSON validation, binding and repeated execution. Assert parsed JSON, not slash spelling.

| Variant | Closure | Project / selector |
|---|---|---|
| Native-eligible object projection containing Parameter | test: exact alias plus actual native execution | `nextorm.sqlserver.tests` / `FullyQualifiedName~SqlServerNativeJsonSqlTests`; integration / `PDCA=T208` |
| Same parameter in array/root shapes | test + guard | `nextorm.core.tests` / `FullyQualifiedName~JsonShapeWriterTests`; integration / `PDCA=T208` |
| Sync versus async | test: both, incl. changed values on repeated calls | `nextorm.integration.tests` / `PDCA=T208` |
| QueryCommand and EntityBuilder surfaces | test: both surfaces × both modes | `nextorm.integration.tests` / `PDCA=T208` |
| SQL Server native versus non-SQL-Server fallback | test: observe native branch; exercise SQLite fallback; preserve other-provider suites | integration / `PDCA=T208`, then full integration suite |
| Unaliased-parameter negative | test: baseline missing-alias assertion and live unnamed-column exception | SQL-generation class and integration / `PDCA=T208` |
| Value/reference; default/null; binding placement | test: int nondefault/zero, string nonempty/null where admitted; guard unsupported; WHERE-only control | focused selectors above |

Coverage: included modules core/sqlite/postgres/sqlserver; thresholds **85% line / 75% branch**. Priority: A208.1–A208.4 and their contract rows are **P1 by construction**.

## Design checklist, docs, performance and reconnaissance
- SOLID/DRY: reuse translator-to-renderer alias responsibility; no provider-specific duplicate translator or alternate serialization path.
- Type/performance: preserve generic parameter/provider typing; avoid reflection, extra allocations and cache-key changes.
- Nextorm invariants 1–10: D208.0 records each as applicable/not applicable with reason and evidence-row mapping.
- Docs: update this status and evidence manifest; public guides/API and Russian mirrors **not touched** unless an actual public contract changes.
- **Performance required:** `NormSqlTranslator.cs:71-81` feeds query preparation; native preparation uses `DataContext.cs:397-410` and `QueryPlanner.cs:545`. Query-path/plan-cache work → run `--anyCategories=acceptance`. Baseline clean export of HEAD `9a2a2871` vs candidate; planned tolerance ≤5% median time regression and no allocation increase.
- **Reconnaissance required, bounded:** D208.0 proves flag reset/projection scope, shape policy, baseline compatibility and acceptance-case coverage through Roslyn references plus existing contracts. Observable completion: recorded lifecycle references, expectations and executable selectors.

## Versioned evidence contract — rv1
Pin **before implementation** at `artifacts/pdca/D208/rv1/manifest.json` (planned, not existing). Row IDs: A208.5/E208.00 prerequisites; A208.1–2/E208.01 red projected regression (baseline C2/C4 nonzero from missing alias, no infra failure); A208.1,4/E208.02 green alias + WHERE controls; A208.4/E208.03 shape/value/null guards; A208.2–4/E208.04 native/surfaces/repeated/fallback live; A208.3–5/E208.05 build + full integration (SQLite/PG/SQL Server/MySQL/ClickHouse executed, not skipped); A208.5/E208.06 coverage 85/75; A208.3,5/E208.07 acceptance benchmarks; A208.1–5/E208.08 independent CHECK.
Planned calls: C1 `dotnet build -c Debug`; C2 `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~SqlServerNativeJsonSqlTests"`; C3 `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~JsonShapeWriterTests"`; C4 `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor -trait "PDCA=T208"`; C5 full integration `-noColor`; C6 `bash artifacts/pdca/D208/rv1/run-red.sh 2ac20818`; C7 dotnet-coverage collect; C8 reportgenerator; C9 `bash artifacts/pdca/D208/rv1/run-perf.sh 9a2a2871 candidate`; C10 CHECK. CHECK re-gather budget: two targeted batches.

## Risks, prerequisites and handoff
- Risks: shared alias flags, baseline test-port compatibility, silently falling back instead of proving native execution, provider skips, unrelated changes contaminating performance.
- Predecessors: collection scheduler must identify/release any active owners of translator/render files; no specific predecessor supplied.
- **Handoff:** task=T208/#208; variant=pdca-dotnet; plan_state=ready; r=1; baseline=2ac20818; HEAD=9a2a2871; footprint=NormSqlTranslator + three test homes + status/evidence, conditional benchmark case; uncertainty=flag lifecycle/shape rules/registry prerequisites; predecessors=shared-file owners TBD; prerequisites=D208.0 and pinned rv1.
- **Refs:** `SqlFunctions.cs:80`; `NormSqlTranslator.cs:54-84`; `SqlSourceRenderer.cs:1093-1139`; `SqlBuilder.cs:505-520`; `JsonShapePlan.cs:117-123,276,348-368`; `JsonNativeStream.cs:39-99`; `DataContext.cs:374-418`; `QueryPlanner.cs:545`; `QueryExecutor.cs:1254-1260`; `SqlServerDialect.cs:304,307-310`.
