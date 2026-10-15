# T232 (#232) — SQL Server PIVOT/UNPIVOT: типизированная проекция результата (вместо TableAlias по имени)

- task_id: `T232`
- issue: [#232](https://github.com/AlexeyShirshov/nextorm/issues/232) — OPEN, label `enhancement`, milestone **1.0.9-rc3**
- collection: `1.0.9-todo` (single group; current worktree/branch `1.0.9-rc3`; no worktree/branch/merge)
- selected_variant: `pdca-dotnet` (+ overlay `nextorm-pdca`)
- current cycle `N=1`; plan_revision: `r=5`; attempt: `n=1`; evidence_contract: `rv=5`
- **Authoritative CHECK at `r=3` / `n=2` / `rv=3` FAILED** (build/security/unsealed evidence); primary route **CHECK → PLAN**. The material `planner` replan accepted the correction and **supersedes rv3 as the current contract (`rv3 → rv4`)**; `r=4` / `n=1` is the next DO attempt. The earlier `n=3` prohibition applied only *inside* the r=3 branch and does not bind r=4.
- r=4 CHECK FAILED (authoritative) → binding escalate decision: new revision r=5 (product scope unchanged; contract changed only per rv5 delta). r=4 history preserved.
- current_plan: `P232-r5-assertion-binding-and-evidence-closure`
- **plan_state: `ready`** (gate 1 satisfied; r=5 corrective replan — D-06/D-07 remain reopened for acceptance, D-08…D-13 remain active, and D232-RV5-01…09 are the new active corrective units)
- starting HEAD: `e9121976` (r=5 source HEAD; the prior r=1 starting HEAD `3e49fe0c` is retained for history)
- committed units: D-01 `9b4bf5d4`, D-02 `04d2289d`, D-03 `8f0d8309`, D-04 `706dd7b7`
- r=3 commits: `3e894f1f`, `9d9c9380` (D-05 partial, historical/provisional), `4674ffde` (repro checkpoint), `b05e94ba` (additive fallback), `001f9c9c` (D-06 docs/example, historical/provisional), `4dc0f6b3` (D-07 boundary/handoff, historical/provisional)
- **D-01…D-05 + `D232-PREREQ-typed-derived` + `D232-AMEND-unpivot-null-oracle`: `done`** (commits preserved).
- **D-06 (docs/example) and D-07 (boundary/handoff): reopened for acceptance — not superseded.** Their r=3 `done` was provisional; the authoritative CHECK rejected it, so their packaging/coverage/variant/ownership/handoff/seal obligations are now active under D-08/D-09/D-10/D-11/D-12. Historical r=3 evidence is preserved in the ledger below.
- **New active remediation units:** D-08 packaging, D-09 variant closure, D-10 coverage, D-11 ownership/handoff, D-12 evidence reconciliation/seal, D-13 security prerequisite coordination. **P1 blocker `B232-AUDIT-DEPENDENCY`** coordinates D-13.

## Task

SQL Server typed PIVOT/UNPIVOT projection. This invocation authored and persisted PLAN for the collection's **PLAN→DO handoff**; no user `go` is awaited — the collection caller owns the boundary.

## Goal

Declare result members and CLR cell types once, derive the PIVOT values from the projection, emit correct aliases, and return a full `EntityBuilder<TShape>` usable in filtering, ordering, selection, derived sources and CTEs.

Approved signatures (immutable):

```csharp
public static EntityBuilder<TShape> Pivot<TEntity, TShape>(
    this EntityBuilder<TEntity> builder,
    PivotAggregate aggregate,
    Expression<Func<TEntity, object?>> aggregateColumn,
    Expression<Func<TEntity, object?>> forColumn,
    Expression<Func<TEntity, PivotValues, TShape>> projection)

public static EntityBuilder<TShape> Unpivot<TEntity, TShape>(
    this EntityBuilder<TEntity> builder,
    Expression<Func<TEntity, UnpivotValues, TShape>> projection,
    params UnpivotColumn[] columns)
```

Accessors: `PivotValues.Value<T>(string)`, `UnpivotValues.Name<T>(string)`, `UnpivotValues.Value<T>(string)`.

**Constraints:** preserve the legacy overloads and their SQL; no new AST node; no `BindEntity` workaround; no automatic nullability inference; no provider capability expansion; no duplicated PIVOT value declarations; no sticky command/plan-cache mutation; preserve warnings-as-errors, CRLF and foreign changes.

**Minimal solution — three answers**
1. Essential outcome: typed result shape, member aliases and source composability.
2. Non-negotiable boundary: the approved surface, the existing PIVOT AST/renderer and legacy behaviour (`SqlServerEntityBuilderExtensions.cs:30,45,59`; `PivotExpression.cs:76-147`; `SqlServerDialect.cs:835-853`; `SqlSourceRenderer.cs:993-1020`).
3. Minimum implementation: provider overloads + a provider-local reference-identity rewriter, two core markers + narrow exact recognition, the existing `Select` path and ordinal materialization.

| Alternative | Benefits | Cost/risk | Decision |
|---|---|---|---|
| Existing PIVOT plus a rewritten `Select` | Reuses rendering, source registration and materialization | Correct grouping-name/source binding needs tests | **Selected** |
| Native node in `QueryPreparer.PrepareColumns` | Specialized centralized handling | More concepts/core changes; duplicates projection support; the approved design rejects it | Rejected |
| `BindEntity<TShape>` or a global `TableAlias` alias change | Superficially smaller local code | Does not rewrite members; changes unrelated/legacy behaviour | Rejected |

**Design checklist**
- Markers must be declared in **core**: core recognition compares declaring types, and core cannot reference a provider-only marker declaration.
- The existing replacement visitors are **type-based** (`ReplaceExpressionVisitor.cs:17,22-25`). Build a **provider-local reference-identity rewriter** replacing only the two captured lambda parameters and their intended accesses.
- Typed grouping/value accesses become `TableAlias`-bound typed column references using the existing `SqlFunctions.Column<T>` mechanism (`SqlFunctions.cs:82-95`; `NormSqlTranslator.cs:45-47,99-109`, `:106-107` sets `ColumnName`).
- Actual grouping SQL names come from effective inner metadata for entities (`MemberInfoExtensions.GetPropertyColumnName:44` — public; `DataContextCache.Metadata` `DataContextCache.cs:51`) or from `QueryCommand.SelectList`/`SelectExpression.PropertyName:24` for derived projections; do not infer them from CLR names alone.
- Core marker translation recognizes explicit generic method definitions, not arbitrary `Value`/`Name` methods (`TableAliasAccessors.IsAccessor:14-21` is name-only and stays `TableAlias`-only).
- Ordinary `Select` marker use binds the relevant `TableAlias` source (`DefaultColumnsProvider.cs:14,121,243` — lookup is `(entityType, fromProjection)`), never a `PivotValues` source registration; an ambiguous unbound marker source fails clearly.
- Public parameterless marker constructors support captured marker instances in ordinary `Select`; accessor methods are translation-only and throw when executed.
- No abstraction beyond the two markers and one internal provider rewriter; each has two observable consumers/routes.
- Materialization stays **ordinal-based** (`RowMapperFactory.cs:52`); aliases serve SQL naming/source consumption, not name-based row mapping.
- No global alias switch, capability flag, public metadata resolver, package dependency or plan-cache redesign.

## Current state

- Approved specification dated 13.10.2026; implementation not started. Issue refs: #231 (examples), #160 (join aliases), #53 (PublicAPI freeze, HELD).
- Pre-gate reconnaissance completed: public naming helper, provider internals access (`nextorm.core.csproj:81` grants `nextorm.sqlserver`), type-keyed source registration (`SqlSourceRenderer.cs:1014`; `DefaultColumnsProvider.cs:121,243`), sorting/source alias rules (`QueryCommand.QueryPreparer.cs:2594,2626-2661`; `SqlSourceRenderer.cs:1165-1186`; `MemberTranslator.cs:811-814`), the real validator schema, the runner/filter patterns, the 7-case acceptance benchmark and container availability.
- Existing replacement visitors match by **type**, not parameter identity.
- Existing `TableAlias` accessors leave `ColumnName` unset (`BaseExpressionVisitor.cs:442-456`); only `NormSqlTranslator.TranslateColumn:106-107` sets it.
- CS1591 is **gated** (`src/Directory.Build.props:11` `GenerateDocumentationFile=true`; `Directory.Build.props:40` `TreatWarningsAsErrors=true`; no `NoWarn`/`CS1591` suppression found in `.props`/`.csproj`/`.editorconfig`) → XML-doc on new public members is a build obligation.
- Podman socket `/mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` exists; zero-skip six-provider runs are recorded in `docs/specs/status/collection-1.0.9-todo.md:116,117,127,140,147`.
- 36 pre-existing foreign dirty entries; no `src/`/`tests/` overlap. Preserved, out of footprint.
- Four execution-only unknowns remain (see Reconnaissance).
- Existing task-local untracked recon: `docs/specs/status/evidence/pivot-typed-overload/evidence-01-recon.md` — preserve.
- Committed legacy exact assertion (must stay): `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:3074` = `.Should().Be("select category, [1], [2]\n from sales pivot (sum(margin) for quarter in ([1], [2])) as [t1]");`

## Durable state

- Complete plan, the rv1 contract and the manifest path are persisted in this file before DO.
- No implementation, test result, coverage figure or benchmark result is claimed by PLAN.
- Gate 1 satisfied: every required planning item and a runnable verification route are specified; implementation outcomes are not asserted as passed.
- This is the finalization of the initial r1 candidate with reconnaissance facts — not a contract supersession and not an attempt reset.
- Manifest path is pinned below; DO writes the manifest, which CHECK reconciles.

## Plan history

```text
r=1 / rv=1: initial candidate; D-01..D-04 executed and committed (9b4bf5d4, 04d2289d, 8f0d8309, 706dd7b7).
r=2 / rv=2: issued in-session, never transcribed or executed; no rv2 artifact directory. Superseded by r=3 / rv=3 after binding escalation authorized narrowly gated engine changes previously excluded.
r=3 / rv=3: additive typed-derived prerequisite and independent oracle amendment; n reset to 1 because implementation actions and authorized footprint materially changed. Original acceptance criteria retained.
(a) correction: the authoritative CHECK that FAILED belongs to r=3 / n=2 / rv=3 (NOT n=1). The r=3 branch reached n=2 via the escalate-authorized fallback fix; the CHECK ran on that sealed tree.
(b) verdict: the r=3 CHECK FAILED on build/security/unsealed evidence; the primary route is CHECK -> PLAN.
(c) material replan: `planner` accepted the correction and explicitly superseded the current contract rv3 -> rv4 (rv3 history preserved, never rewritten). The 30 existing row IDs/obligations are carried forward.
(d) r=4 / n=1: the next DO attempt; plan `P232-r4-sourcebound-variant-coverage-handoff` (D-06/D-07 reopened, D-08..D-13 new active remediation units). r reset to 4 and n reset to 1 because the work and footprint materially changed.
r=5 / rv=5: transparent correction revision. The r=4 CHECK FAILED; a material `planner` replan (accepted from the binding escalate decision) created the rv5 contract. Product scope unchanged; only two contract items are re-worded (AC-12 and the `EC232-RV3-SEAL` obligation). r=4/rv=4 preserved verbatim, never rewritten. n reset to 1 because the revision changed.
```

## Acceptance criteria

| ID | Requirement and verification | Negative case |
|---|---|---|
| AC-01 | «Новые overload `Pivot`/`Unpivot` компилируются, возвращают `EntityBuilder<TShape>`; члены формы доступны в `Where`/`OrderBy`/`Select`.» Compile typed consumers; exact SQL assertions. | An invalid shape/member access must not be hidden by an untyped return. |
| AC-02 | «SQL и данные совпадают с текущим `TableAlias`-путём (интеграционные тесты SQL Server).» Compare deterministic typed/legacy rows, clauses and value order; assert the intended member aliases. | Reordered values, missing aliases or incorrect derived/CTE consumption must be detected. |
| AC-03 | «Существующие `Pivot`/`Unpivot` без изменений; их тесты зелёные.» Discover/run all 14 existing SQL cases and the eager guards; preserve the exact legacy SQL. | The legacy `[1]` must not acquire an unsolicited alias; existing throws remain. |
| AC-04 | «Валидации дают ясные ошибки (fail-closed), динамический PIVOT и опечатка в значении документированы как нетипизируемые.» Assert the exception category/message and the bilingual/XML limitations. | Duplicate/empty values, consumed grouping members and zero UNPIVOT columns must fail clearly. |
| AC-05 | «XML-doc на новые API; гайды EN+RU обновлены (`docs/guide/provider-specific-sql/sqlserver.md` + RU-зеркало).» Build with CS1591 gated; semantic docs review. | A missing public-member XML doc, or a missing RU/runtime-name explanation, fails. |
| AC-06 | «Покрытие не ниже порогов CI.» Candidate line ≥85%, branch ≥75%; comparable touched-area coverage does not decrease. | Either a threshold failure or an area decrease fails, despite the non-`main` warning-only CI policy. |
| AC-07 | Markers work in ordinary `Select`; explicit nullable/reference/value types and the supported shape forms materialize correctly. | Runtime marker execution throws; unrelated same-named methods are not recognized. |
| AC-08 | Entity, derived and TVF inners; derived typed output and `AsCte` preserve the actual names and source qualification. | CLR/mapped-name confusion or ambiguous/wrong aliases fails exact SQL/runtime tests. |
| AC-09 | SQL Server-only capability, eager/single-query guards and plan-cache locality remain unchanged. | Other providers must reject both typed operators; interleaved queries must not reuse stale shape/value state. |
| AC-10 | The AdventureWorks quarterly example uses typed members and declares quarter values only once. | Retained `PivotValue.Create` duplication or by-name cell reads remain detectable. |
| AC-11 | Scope evidence, branch inventory, targeted mutation and the acceptance benchmark are complete and valid. | Missing exits, zero selected cases, a surviving mandated mutant, skipped providers or an unexplained material regression fails. |
| AC-12 | Foreign changes and CRLF are preserved. The sibling handoff contains the green, committed T232 task implementation under the recorded collection authorization. No foreign paths are staged or overwritten, and no dirty implementation paths overlap the sibling handoff footprint. A globally clean worktree is not required. Explicitly retention-authorized, non-overlapping residual paths are inventoried; retention authorization is not commit authorization. | Foreign staging/overwriting, dirty overlapping implementation paths, an unverified retention claim, or absence of the required green committed task implementation fails the handoff. |

AC-12 r=5 clarification: `strict_clean` is a **derived diagnostic** defined as "no foreign dirty paths and no untracked dirty paths"; its value alone is not AC-12. Acceptance separately evaluates ownership, recorded retention authority, overlap, CRLF, foreign staging/overwriting, and the committed green implementation. A globally clean worktree is not required and no commit is authorized on this branch.

## Variant matrix

All new test names below are **planned**, not verified repository symbols.

| Row | Execution variant | Closure |
|---|---|---|
| V01 | Typed PIVOT / typed UNPIVOT | test: exact SQL and SQL Server parity for both |
| V02 | Untyped legacy paths | test: all 14 existing cases; unchanged exact baseline |
| V03 | Entity inner, mapped/naming-convention column | test: correct name at extension time and preparation |
| V04 | Derived inner with a renamed member | test: resolve the SQL-visible projection output name |
| V05 | TVF inner | test: deterministic SQL Server TVF and typed projection |
| V06 | Full / subset grouping | test: both; unused inner members remain grouping inputs |
| V07 | Aggregate/FOR member used as grouping | guard + tests, including converted accesses |
| V08 | Value order | test: NewExpression argument and MemberInit binding order drive `IN` |
| V09 | Duplicate PIVOT values | guard + test naming the offending value |
| V10 | Empty PIVOT value projection | guard + test |
| V11 | Nullable/non-nullable; value/reference/default | test: nullable aggregate, COUNT, reference UNPIVOT and NULL materialization |
| V12 | Anonymous / record / record struct / MemberInit / scalar | test: existing `Select` forms; scalar has no invented member alias |
| V13 | Subsequent `Where` / `OrderBy` / `Select` | test: both operators; defining-expression sort semantics |
| V14 | Derived typed source / `AsCte` | test: exact aliases and SQL Server execution |
| V15 | UNPIVOT zero columns / missing or conflicting roles | guard + tests |
| V16 | UNPIVOT role names and repeated consistent references | test: one identifier per role; conflicting references fail |
| V17 | Constant / captured string / null-empty / row-dependent name | test the first two; guard invalid and row-dependent names without arbitrary execution |
| V18 | Typo or absent data value | test the emitted identifier/missing-cell behaviour; docs: no membership validation |
| V19 | Dynamic PIVOT | guard/documented API boundary; the legacy path remains available |
| V20 | SQLite/PostgreSQL/MySQL/MariaDB/ClickHouse | test: each provider rejects both typed operators |
| V21 | Eager-load / single-query state | test: existing guards and typed equivalents |
| V22 | Join alias generator, #160 | test: typed result joined twice; derived/CTE route, distinct qualification |
| V23 | Ordinary `Select` / unrelated `Value` method / ambiguous marker source | test + ambiguity guard |
| V24 | Cache reuse and interleaved shapes/values | test: stable reuse, distinct keys/results, no command mutation |
| V25 | Null arguments, modifiers and capability defaults | guard/test: inherited rules plus new overload argument checks |
| V26 | Typed derived **builder** route (P1) | test: preserved projection, SQL/data, composition; guard: marker/projection/no-own-modifier conditions, unmarked/modifier-bearing controls unchanged |
| V27 | Typed derived **command** route (P1) | test: equivalent emitted names, qualification, rows |
| V28 | Pivot-free derived recursion probe (P1) | test: baseline/candidate diagnostic with actual outcome + source provenance; guard: no general derived/projection change (a repair needing that expansion invokes STOP) |
| V29 | Identifier case sensitivity (P1) | test: real case-sensitive-database resolution, bare `category` and aliased `[Q1]`, builder/command and derived/CTE/join consumers |

Cover every row and the high-risk intersections V03/V04×V13/V14, V11×V01 and V22×V14; do not invent a full Cartesian product.

## Priority matrix

| Rows | Priority and basis |
|---|---|
| V01,V02,V11,V20 | **P1** — operator-class requirements (overlay class table): SQL sibling parity, capability per dialect, test per provider, nullable semantics |
| V03–V10,V12–V19,V21–V25 | **P1** — approved invariants and the actual translation/source/cache execution path |
| AC-05,AC-06,AC-11,AC-12 | **P1** — explicit acceptance/build/evidence/scope requirements |
| Existing PIV-RD2 | **P2 unchanged** — #53 ownership; not a waiver for T232 |

CHECK applies this matrix without downgrade.

## Evidence contract `rv=1`

Frozen manifest path: **`artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/manifest.json`**.
Below, `A` denotes `artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1`. All future artifact sources are **planned**.

**Real validator schemas** (`scripts/validate_inner_loop.py`, subcommands `brief|report|manifest` at `:586`, exit 0/2/1):
- brief: top-level `unit` + `scope{projects[], selectors[], files[], rationale, rebuild:"affected"|"none", boundary}`; optional `amendments[]{reason, added_selectors[], validated_before_run}`.
- report: brief + `executions[]{command[], phase:"inner"|"boundary", source_revision, artifact_revision, exit_code:int, selected_count:int>=1}`.
- manifest: `task`, `tree`, `status_file`, `contract_rv`, unique `required_rows`, `rows[]{id, status:"met"|"open"|"na", evidence[], reason, predicate}`; `met` ⇒ non-empty `evidence[]{kind, path, exit:int?}`, `open` ⇒ `reason`, `na` ⇒ `predicate`.
- Evidence entries carry **`kind`, `path`, optional integer `exit`**; extra keys are ignored by the validator, not validated. Rich provenance goes to `A/provenance.json`; row obligations to `A/contract.json`; the task-local fulfillment checker validates those separately.

Every execution saves stdout, stderr and numeric exit-code files. Provenance records the exact argv/cwd, tool versions, source/tree digest, producer, timestamps and artifact hashes. A command listing alone is not execution evidence.

**Exact planned invocation catalog**
- C01 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/verify.py preparation`
- C02 `python3 scripts/validate_inner_loop.py brief artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/briefs/D-04.json`
- C03 `python3 scripts/validate_inner_loop.py report artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/reports/D-04.json`
- C04 `dotnet build src/nextorm.sqlserver/nextorm.sqlserver.csproj -c Debug`
- C05 `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~T232"`
- C06 `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~Pivot|FullyQualifiedName~Unpivot|FullyQualifiedName~ProviderExtensionEagerGuardTests"`
- C07 `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~T232TypedOperators_RespectCapability"`
- C08 `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~T232SqlServerTests"`
- C09 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/verify.py mutation`
- C10 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/verify.py coverage baseline`
- C11 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/verify.py boundary`
- C12 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/verify.py coverage candidate`
- C13 `dotnet build examples/nextorm.examples.mssql.adventureworks/nextorm.examples.mssql.adventureworks.csproj -c Debug`
- C14 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/verify.py docs`
- C15 `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`
- C16 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/verify.py footprint`
- C17 `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/manifest.json`
- C18 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/verify.py fulfill`
- C19 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/verify.py scopes`

`verify.py` is task-local evidence plumbing, not a production framework. `scopes` validates each D-01..D-07 brief before execution and the report after, using the existing validator; expanded child argv are preserved. C02/C03 cover the main implementation unit.

| ID / rv | Requirement; scenario | Evidence kinds + sources | Invocation and required result | Artifacts under A | Owner | Applicability predicate |
|---|---|---|---|---|---|---|
| E232-01 / 1 | AC-08,12; preparation/name lifecycle | Recon report, Roslyn/config facts, planned preparation tests | C01: exit 0, a concrete safe name-resolution route proven | `preparation.json`, `scout.md`, `foreign-baseline.json`, `C01.*` | scout/coder-preparation | Always |
| E232-02 / 1 | AC-11; scope/inner-loop gate | Schema-valid briefs/reports and real execution records | C02,C03,C19: exit 0; each test execution selects ≥1 | `briefs/`, `reports/`, `scope-map.json`, logs | coder | Always, per unit |
| E232-03 / 1 | AC-01,05,07; API/build/XML | Public API inspection and library build | C04: exit 0, no warnings/errors, approved signatures | `api-review.json`, `C04.*` | coder-build | APIs introduced |
| E232-04 / 1 | AC-01,02,07,08; typed SQL/shapes/composition | Planned exact SQL tests | C05: exit 0, all designated cases run without skips | `typed-cases.json`, `C05.*` | coder-sql-tests | Typed APIs introduced |
| E232-05 / 1 | AC-04; fail-closed validations | Planned negative tests and message assertions | C05: exit 0; every guard row represented | `guard-cases.json`, `C05.*` | coder-sql-tests | Typed APIs introduced |
| E232-06 / 1 | AC-03,09; legacy/eager preservation | Existing tests at verified anchors and discovery inventory | C06: exit 0; all original 14 plus eager guards executed | `legacy-inventory.json`, `legacy-diff.json`, `C06.*` | coder-regression | Always |
| E232-07 / 1 | AC-09; per-provider capability | Shared planned integration capability cases | C07: exit 0; all six providers executed, five reject both | `provider-matrix.json`, `C07.*` | coder-provider-tests | Always |
| E232-08 / 1 | AC-02,07,08; SQL Server rows/ordinals/sources | Planned container fixture and parity tests | C08: exit 0; no SQL Server skips | `integration-cases.json`, `container-state.json`, `C08.*` | coder-integration | Always |
| E232-09 / 1 | AC-09; cache/eager/join/source-binding | Planned tests and source review | C05,C08: exit 0; V21–V25 covered | `cache-alias-review.json`, associated logs | coder-core-tests | Always |
| E232-10 / 1 | AC-11; targeted mutation/red arms | Controlled patches, assertion failures, restoration proof | C09: exit 0 iff every mandated mutant is killed and the tree restored | `mutation.json`, `mutants/`, `C09.*` | coder-mutation | New rewrite/guard/translation code present |
| E232-11 / 1 | AC-06; coverage/branch delta | Comparable XML, summaries and changed-area mapping | C10,C12: exit 0; final ≥85/75, area does not decrease | `coverage/baseline/`, `coverage/candidate/`, `branch-delta.json` | coder-coverage | Always |
| E232-12 / 1 | AC-03,09,11; final boundary | Full project suites and the six-provider container run | C11: exit 0; required cases/providers present, zero skips | `boundary.json`, `boundary-logs/`, `C11.*` | coder-boundary | Implementation units complete |
| E232-13 / 1 | AC-10; example | Source review and example build | C13: exit 0; the C14 example audit passes | `example-review.json`, `C13.*` | coder-example | Always |
| E232-14 / 1 | AC-04,05; bilingual/XML docs | Content review, XML build evidence and a public link scan | C14: exit 0; semantic EN/RU match, no public specs links | `docs-review.json`, `C14.*` | coder-docs | Always |
| E232-15 / 1 | AC-11; acceptance performance | Actual BDN baseline/candidate reports | C15 twice: exit 0, seven cases; no unexplained material regression | `perf/baseline/`, `perf/candidate/`, `perf-comparison.json` | coder-perf | Query translation/preparation changes |
| E232-16 / 1 | AC-12; scope/CRLF/handoff | Git inventories, staged allowlist and line-ending audit | C16: exit 0; foreign bytes preserved, shared implementation clean at handoff | `footprint.json`, `handoff.json`, `C16.*` | coder-handoff | Always |
| E232-17 / 1 | AC-01…12; schema/fulfillment/sealing | Manifest, provenance and counters | C17,C18: exit 0; zero applicable open rows, valid required evidence/hashes | `manifest.json`, `contract.json`, `provenance.json`, `counters.json` | CHECK | Always |

No `N/A` arm is planned. Tool/container/report absence is not an applicability exemption. Manifest exit 0 proves schema only; **C18** additionally enforces completion, required slots, evidence provenance and hashes. All rows stay in `required_rows`.

**CHECK re-gather:** owner **CHECK**; budget **two targeted batches total per CHECK invocation**, at most one repeat per missing/ambiguous row; exhaustion yields an explicit evidence gap; persistent low confidence routes to planner, then `escalate` trigger 5.

No supersession exists. A future justified rv2 explicitly supersedes rv1, preserves stable IDs/obligations and adds IDs for genuinely new variants. Missing reports alone cannot revise the contract.

## Evidence contract `rv=3` (additive; supersedes the unexecuted rv2)

Frozen manifest path: **`artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/manifest.json`**.
Below, `A` denotes `artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3`. All delta artifact sources are **planned** until their invocation runs.

- **Carried verbatim:** rows **E232-01…E232-17** and invocations **C01…C19** keep their IDs, obligations and their **original `rv1/` artifact paths** (they are *not* rewritten to `rv3/`).
- **rv2 row IDs preserved** with their obligations: `EC232-DERIVED-UNIT`, `EC232-DERIVED-EXEC`, `EC232-SHARED-REGRESSION`, `EC232-ORACLE-AMEND`, `EC232-BUILD`.
- **rv3 supersedes the unexecuted rv2:** rv2 was issued in-session, never transcribed and never executed (no rv2 artifact directory). rv3 retains the same obligations and adds `EC232-REPRO`, the typed derived builder/command/case-sensitivity variants, the pivot-free probe, cache locality, prerequisite mutation and the rv3 seal.
- **CHECK re-gather budget unchanged:** two targeted batches total per CHECK invocation, at most one repeat per missing/ambiguous row; exhaustion yields an explicit evidence gap.

**Delta invocation catalog (planned `rv3/verify.py`; captures argv/cwd, stdout/stderr, numeric exits, selection counts and source+artifact hashes, and fails closed):**
- C20 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py repro --phase baseline --capture`
- C21 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py derived-unit`
- C22 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py derived-exec`
- C23 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py shared-regression`
- C24 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py oracle-amendment`
- C25 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py build`
- C26 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py variant derived-builder`
- C27 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py variant derived-command`
- C28 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py variant pivot-free`
- C29 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py variant case-sensitive-alias`
- C30 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py cache-locality`
- C31 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py prerequisite-mutation`
- C32 `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/manifest.json`
- C33 `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/verify.py fulfill`

Semantics: **C20** runs the isolated repro children *before* production edits; outer exit 0 = the expected baseline failure was demonstrated and the controls recorded. **C25** runs `dotnet build src/nextorm.core/nextorm.core.csproj -c Debug` + `dotnet build src/nextorm.sqlserver/nextorm.sqlserver.csproj -c Debug`. **C23** runs the eight project suites (`core/sqlite/postgres/sqlserver/mysql/mariadb/clickhouse/alias`) plus the container boundary `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`.

| ID / rv | Requirement; scenario | Evidence kinds + sources | Invocation and required result | Artifacts under A | Owner | Applicability predicate |
|---|---|---|---|---|---|---|
| EC232-REPRO / 3 | AC-08,11; V26–V28 | Isolated repro children, production hashes, frozen oracles | C20: exited 0 only after a genuine builder red and records controls | `repro/baseline.json`, `repro/production-before.json`, `repro/logs/` | coder-prerequisite | Always (before production edits) |
| EC232-DERIVED-UNIT / 3 | AC-01,03,08 | Planned derived builder/command unit tests + case inventory | C21: exit 0; every typed derived cell/route case selected | `derived/unit.json`, `derived/case-inventory.json` | coder-sql-tests | Typed derived surface present |
| EC232-DERIVED-EXEC / 3 | AC-02,07,08,09 | Container fixture and execution matrix | C22: exit 0; SQL Server executed, per-provider state recorded | `derived/execution.json`, `derived/provider-matrix.json`, `derived/container-state.json` | coder-integration | Typed derived surface present |
| EC232-SHARED-REGRESSION / 3 | AC-03,08,09,11 | Eight project suites + container boundary, protected-file audit | C23: exit 0; inventory and oracle comparison recorded, no protected file changed | `regression/inventory.json`, `regression/oracle-comparison.json`, `regression/protected-files.json`, `regression/logs/` | coder-regression | Always |
| EC232-ORACLE-AMEND / 3 | AC-02,11 | Before/after oracle text, hashes, source anchor | C24: exit 0; exactly one key amended, before/after hashes match the record | `oracle/before.json`, `oracle/after.json`, `oracle/amendment.json` | coder-oracle | Independent amendment unit |
| EC232-BUILD / 3 | AC-01,05,12 | Core + provider builds, API review | C25: exit 0, 0 warnings / 0 errors for both projects | `build/api-review.json`, `build/gate-review.json` | coder-build | Always |
| EC232-DERIVED-BUILDER / 3 | AC-08,09; V26 | Builder-route variant execution | C26: exit 0; preserved projection/SQL/data, guarded controls | `variants/derived-builder.json` | coder-sql-tests | Builder route changed |
| EC232-DERIVED-COMMAND / 3 | AC-08; V27 | Command-route variant execution | C27: exit 0; equivalent names, qualification and rows | `variants/derived-command.json` | coder-sql-tests | Command route changed |
| EC232-PIVOTFREE-PROBE / 3 | AC-08,11; V28 | Pivot-free diagnostic with source provenance | C28: exit 0; actual outcome recorded | `variants/pivot-free.json` | coder-prerequisite | Always |
| EC232-CASE-SENSITIVE-ALIAS / 3 | AC-08; V29 | Case-sensitive database resolution, bare and aliased identifiers | C29: exit 0; collation and both consumers recorded | `variants/case-sensitive-alias.json`, `variants/database-collation.json` | coder-integration | Derived surface present |
| EC232-CACHE-LOCALITY / 3 | AC-09; V24,26 | Cache locality tests + source review | C30: exit 0; no sticky command/plan-cache state | `cache/locality.json`, `cache/source-review.json` | coder-core-tests | Always |
| EC232-PREREQ-MUTATION / 3 | AC-11; V26,29 | Controlled prerequisite patches, red arms, restoration proof | C31: exit 0 iff every mandated mutant is killed and the tree restored | `mutation/prerequisite.json`, `mutation/patches/`, `mutation/logs/` | coder-mutation | New prerequisite code present |
| EC232-RV3-SEAL / 3 | AC-01…12; CHECK | Manifest, contract, provenance, counters | C32,C33: exit 0; zero applicable open rows, valid required evidence/hashes | `manifest.json`, `contract.json`, `provenance.json`, `counters.json` | CHECK | Always |

Every new row is **P1**.

## Evidence contract `rv=4` (supersedes rv3 as the *current* contract)

Frozen manifest path: **`artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/manifest.json`**. Below, `R` denotes `artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4`.

- **Supersession:** the authoritative CHECK at **r=3 / n=2 / rv=3 FAILED** (build/security/unsealed evidence; primary route CHECK → PLAN). The material `planner` replan accepted the correction and supersedes **rv3 as the current contract** (`rv3 → rv4`). **rv3 history is preserved, not rewritten** — the two rv3 artifacts (`contract.json`, `manifest.json`, `counters.json`) stay on disk.
- **Carried verbatim:** rows **E232-01…E232-17** and **every `EC232-*`** keep their IDs and obligations and their original rv1/rv3 artifact paths.
- **Reconciled claims (never copy the rv3 "27 `met`" figure — recompute counters from rows):**
  - **Carried forward `open`:** `E232-11` (AC-06), `E232-17`, `EC232-RV3-SEAL`.
  - **Reopened `met` → `open` with the CHECK reasons:**
    - `E232-09` (cache/eager/join/source-binding) — sealed `met` via C05/C08, but those runs required `-p:NuGetAudit=false`; the ordinary restore fails, so the environment is not reproducible.
    - `E232-13` (example) — C13 green only against a locally packed feed; the literal C13 build fails (packaging mismatch + advisory).
    - `E232-16` (scope/CRLF/handoff) — the D-06 commit (`001f9c9c`) folded foreign #160 hunks without explicit authorization.
    - `EC232-BUILD` — the current ordinary `dotnet build nextorm.slnx -c Debug` is **not green** (26 NU1902/NU1903 errors).
- **Seeded accounting:** `R/manifest.json` is seeded from rows — **41 required rows** (23 `met` carried with rv3 evidence / 18 `open`), schema-validated (`validate_inner_loop.py manifest` exit 0). `R/accounting-correction.json` supersedes the stale `rv3/counters.json` `attempt` field (the CHECK ran at n=2, not n=1).
- **Driver:** `R/verify.py` (case driver skeleton, fail-closed) — each case writes `R/<case>.json` and `R/logs/<case>.{out,err,exit,cmd.json}`; exit 0 = ok, 2 = violation, 1 = missing input/usage.
- **CHECK re-gather budget:** unchanged — **two targeted batches total per CHECK invocation**, at most one repeat per missing/ambiguous row; exhaustion yields an explicit evidence gap.

**rv4 delta rows (11; every new row is P1).** Exact invocation for each: `python3 "$R/verify.py" --case <case>`.

| ID / rv | Requirement; scenario | Owner | Unit | Case (`--case`) | Expected outcome | Negative acceptance |
|---|---|---|---|---|---|---|
| `EC232-RV4-PACKAGING` / 4 | D-08: example consumes the current checkout (ProjectReference) | coder-example | D-08 | `packaging` | literal C13 exit 0, no CS/packaging error, no override/feed masking | override/feed-masked pass rejected |
| `EC232-RV4-V05` / 4 | D-09: V05 TVF inner closure | coder-sql-tests | D-09 | `v05` | selected ≥ 1, exit 0 | unexecuted/zero-selected rejected |
| `EC232-RV4-V18` / 4 | D-09: V18 typo/absent data value closure | coder-sql-tests | D-09 | `v18` | selected ≥ 1, exit 0 | unexecuted rejected |
| `EC232-RV4-V19` / 4 | D-09: V19 dynamic PIVOT guard | coder-sql-tests | D-09 | `v19` | selected ≥ 1, exit 0 | unexecuted rejected |
| `EC232-RV4-V21` / 4 | D-09: V21 eager/single-query state closure; V14/V22 preserved | coder-sql-tests | D-09 | `v21` | selected ≥ 1, exit 0 | unexecuted rejected |
| `EC232-RV4-COVERAGE` / 4 | D-10: thresholds **and** comparable touched-scope non-decrease | coder-coverage | D-10 | `coverage` | ≥85 line / ≥75 branch **and** no touched-scope decrease | denominator/exclusion change rejected |
| `EC232-RV4-OWNERSHIP` / 4 | D-11: verified #160 owner identity + explicit authorization | coder-handoff | D-11 | `ownership` | record present, T232-only footprint | implicit/blanket authorization rejected |
| `EC232-RV4-HANDOFF` / 4 | D-11: per-hunk byte hashes, clean paths, CRLF | coder-handoff | D-11 | `handoff` | hashes match, no foreign byte changed | foreign byte change rejected |
| `EC232-RV4-SECURITY` / 4 | D-13: advisory capture + duplicate search | coder-security | D-13 | `security` | complete identifiers/paths/exits captured | masking with `NuGetAudit=false` rejected |
| `EC232-RV4-PERF` / 4 | perf re-confirmation on the rv4 tree | coder-perf | D-10 | `perf` | seven cases, no unexplained material regression | unexplained ≥20% regression rejected |
| `EC232-RV4-SEAL` / 4 | D-12: rv4 reconciliation/seal | CHECK | D-12 | `seal` | manifest schema valid **and** zero applicable open rows | sealing with unsupported `met`/open rows rejected |



**Planned assertions**
- Preparation: mapped names available/resolved safely at extension time; the `TableAlias` registration precedes the relevant column use.
- SQL: ordered `IN`, `[1] as [Q1]`, grouping aliases, UNPIVOT role aliases, scalar behaviour.
- Shapes/cells: anonymous, record, record struct, MemberInit; nullable aggregate, COUNT, reference cells and NULL materialization.
- Composition: defining-expression `OrderBy`, `Where`, `Select`, derived source, CTE, TVF and a twice-joined typed result.
- Guards: consumed grouping, duplicates, empty values, zero columns, invalid names, missing/conflicting UNPIVOT roles, null arguments and ambiguous ordinary marker binding.
- Marker recognition: ordinary `Select`, direct execution throws, an unrelated same-named method remains unrelated.
- Cache: repeat the same query and interleave different shapes/values; inspect command state for no sticky mutation.
- Integration: deterministic typed/legacy parity; the capability case inherited by all six provider suites.

**Shared contract fixed in D-02:** alias spelling, value order, role identifiers, fixture rows, NULL/COUNT expectations, exception category/message fragments, provider matrix and ownership. Shared fixture/oracle data is test-only; no production abstraction.

**Per-unit affected subsets**
- D-01: `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~T232PreparationProbeTests"`; C04 actual baseline warning set; C06 baseline.
- D-02: same project, filter `FullyQualifiedName~T232ProjectionContractTests`.
- D-03: same project, filter `FullyQualifiedName~T232MarkerTests`, plus C04.
- D-04: same project, filter `FullyQualifiedName~T232TypedPivotTests`, plus C04 and C06.
- D-05: C05, C07, C08, C09.
- D-06: C04, C13, C14; rerun a narrow affected SQL case if sample-driven production edits occur.
- D-07: the single boundary below; coverage/performance/fulfillment assessment.

New-API tests are introduced together with their compiling implementation unit. Do not report a compile failure as a selected runtime test or fabricate `selected_count`. Every scope amendment is validated before its added execution.

**Single post-implementation boundary**, after D-06:
- Full `dotnet test tests/nextorm.<project>.tests -c Debug` for `core`, `sqlite`, `postgres`, `sqlserver`, `mysql`, `mariadb`, `clickhouse`.
- `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`.
- Require SQLite/PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse execution with no skips.
- The pre-change comparable coverage setup is not an extra post-unit sweep.

Load the running-integration-tests skill before any container execution. A missing socket invokes the documented start/recheck procedure; do not substitute SQLite-only success.

**Coverage:** identical settings, suites and tool versions for baseline/candidate; task-local tools under `A/tools`, versions recorded.
- Collector: `A/tools/dotnet-coverage collect "python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/verify.py coverage-run" -f cobertura -s coverage.settings.xml -o <measurement>/coverage.xml`.
- Reporter: `A/tools/reportgenerator "-reports:<measurement>/coverage.xml" "-targetdir:<measurement>/report" "-reporttypes:JsonSummary;Cobertura"`.
- `<measurement>` is exactly `A/coverage/baseline` or `A/coverage/candidate`; the wrapper records the expanded argv.
- The candidate collection wraps the fixed C11 boundary; C12 assesses it without another sweep.
- Record total/covered branch delta, added branches, uncovered locations and comparable touched-area coverage.

**Mutation:** no Stryker run/config is claimed. Six targeted controlled mutants: (1) remove alias metadata; (2) reverse value order; (3) disable the duplicate guard; (4) disable the consumed-column guard; (5) disable the zero-column guard; (6) force a wrong mapped/derived grouping name. Every mutant must cause its designated assertion to fail, then be restored. This is targeted mutation evidence, not a repository-wide mutation score.

No new guard/rewriter branch may remain untested. Unchanged general translator branches are outside the mutation denominator; discovered uncovered new branches remain active work.

Counters: selected/executed/passed/failed/skipped per provider; guards covered/required; branch delta; mutants killed/attempted; applicable evidence complete/required; sealed hash-valid artifacts/required. Applicable P1/evidence-sealing ratios must be **100%**.

## Evidence contract `rv=5` (supersedes rv4 for current acceptance)

Frozen manifest path: **`artifacts/pdca/D232-sqlserver-pivot-unpivot/rv5/manifest.json`**; `rv=5` supersedes `rv=4` **for current acceptance only** — rv4 stays the historical record and is never rewritten. Below, `R` denotes `artifacts/pdca/D232-sqlserver-pivot-unpivot/rv5` and `B` denotes `artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4`.

- **Carried verbatim:** all 41 inherited row IDs (`E232-01…E232-17`, `EC232-REPRO`…`EC232-RV4-SEAL`), their historical artifact paths and still-required obligations are preserved; historical manifests are never rewritten.
- **Legal statuses:** the only legal statuses are `met` / `open` / `na` — no `superseded` status exists in `scripts/validate_inner_loop.py`. No PASS while any required row is `open`.
- **`EC232-RV3-SEAL` obligation (re-worded):**

  > The historical rv3 seal gap is explicitly reconciled, without manufacturing a retrospective rv3 PASS or changing historical artifacts. The current rv5 seal records the rv3 gap and its disposition, validates the current contract and evidence closure, and has zero applicable open required rows. This row is required and cannot be discharged as `na`.

**rv5 delta rows (13; every new row is P1; applicability `Always: T232 rv5 acceptance is requested`).** For every row, `V="$R/tools/rv5_evidence.py"`, `R=artifacts/pdca/D232-sqlserver-pivot-unpivot/rv5`, `B=artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4`.

| ID / rv | Requirement; AC | Owner | Unit | Exact invocation | Required evidence/result | Negative acceptance |
|---|---|---|---|---|---|---|
| `EC232-RV5-SEAL-RECONCILIATION` / 5 | sealed rv3-gap reconciliation | coder→check | D232-RV5-09 | `python3 "$V" seal --root "$R" --historical-root "$B"` | `seal-reconciliation.json` + `seal.json`; exit 0 only with current closure + explicit rv3 gap + zero applicable open rows | reject `na` substitution or a retrospective rv3 PASS |
| `EC232-RV5-FULFILLMENT` / 5 | rv4-defect accounting + rv5 fulfillment | coder→check | D232-RV5-08 | `python3 "$V" fulfillment --rv4 "$B" --rv5 "$R"` | `fulfillment-rv4.json` + `fulfillment-rv5.json`; exit 0 ⇒ rv4 defects explicitly accounted for and actual rv5 requirements fulfilled, rv4 keeps its historical-invalid disposition | reject the rv1 entry/root, a wrong `rv`, or status-only validation |
| `EC232-RV5-HASH-LEDGER` / 5 | transitive hash inventory | coder→check | D232-RV5-08 | `python3 "$V" hashes --root "$R" --verify` | `hash-ledger.json` + `hash-verification.json`; exit 0, complete transitive inventory, every expected file present/hash-valid | reject missing/stale/substituted/unaccounted evidence |
| `EC232-RV5-V18-EXECUTION` / 5 | V18 declared-but-absent value on SQL Server | coder | D232-RV5-04 | `python3 "$V" run --case v18 --root "$R"` | executes the SQL Server integration selector; `v18/assertion-execution.json` + raw logs show SQL Server execution and the NULL assertion | reject fixture-only/render-only proof, skipped execution or zero selected tests |
| `EC232-RV5-V23-AMBIGUITY` / 5 | V23 ordinary-marker ambiguity guard | coder | D232-RV5-03 | `python3 "$V" run --case v23 --root "$R"` | probe + positive/negative tests + mutation evidence; exit 0 requires actual ambiguous ordinary-marker rejection AND valid single/aliased behaviour | reject first-match binding or blanket rejection |
| `EC232-RV5-HANDOFF` / 5 | rewritten AC-12 handoff | coder→check | D232-RV5-09 | `python3 "$V" handoff --root "$R" --status docs/specs/status/D232-sqlserver-pivot-unpivot-1.md` | `handoff.json`; exit 0 only for the rewritten AC-12 | reject unsupported ownership, foreign changes, dirty overlap or a missing green committed implementation |
| `EC232-RV5-SECURITY-DISPOSITION` / 5 | two inherited identifier-safety High findings | scout/coder | D232-RV5-06 | `python3 "$V" security --root "$R" --milestone 1.0.9-rc3` | search logs + verified issue JSON/URL; both High findings explicitly "accepted risk, not a blocker" | reject a missing duplicate search, an unverifiable issue/milestone or production fixes |
| `EC232-RV5-V05-MAPPING` / 5 | V05 assertion→execution mapping | scout/coder | D232-RV5-05 | `python3 "$V" mapping --variant V05 --root "$R"` | `v05/assertion-execution.json`; exit 0 only when every applicable assertion maps to observed execution or a justified guard | reject test-name-only mappings |
| `EC232-RV5-DOCS-EXAMPLES` / 5 | current bilingual/XML-doc/example audits | scout/coder | D232-RV5-05 | `python3 "$V" docs-examples --root "$R"` | current bilingual/XML-doc/example audits + CS1591 invocation/result + `reports/D-09.json` | reject stale-only audits, an omitted language/example or a silently repaired historical report |
| `EC232-RV5-CONTRADICTIONS` / 5 | historical-contradiction reconciliation | coder→check | D232-RV5-08 | `python3 "$V" reconcile --rv4 "$B" --rv5 "$R"` | `reconciliation.json`; both the SECURITY-state and HEAD contradictions identified, dated and resolved for current acceptance; history unchanged | reject unexplained conflicts or modified rv4 artifacts |
| `EC232-RV5-REGRESSION` / 5 | regression / branch / frozen-oracle / mutation | coder | D232-RV5-07 | `python3 "$V" run --case regression --root "$R"` | exact build/test commands, branch inventory, scope diff, frozen-test comparison, targeted mutation logs; exit 0 with nonempty selected tests | reject provider skips, a changed frozen oracle or a forbidden footprint |
| `EC232-RV5-COVERAGE` / 5 | thresholds + comparable touched-area non-decrease | coder | D232-RV5-07 | `python3 "$V" coverage --baseline-root "$B/coverage" --candidate "$R/coverage/candidate.cobertura.xml" --root "$R"` | comparable coverage/branch-delta; line ≥85, branch ≥75, no comparable touched-area decrease | reject incomparable denominators or aggregate-only substitution |
| `EC232-RV5-PERF` / 5 | seven-case acceptance replay | coder | D232-RV5-07 | `python3 "$V" replay-perf --recorded-root "$B" --root "$R" --expected-cases 7` | resolves and executes the recorded seven-case harness commands; raw results + comparison, zero failed cases / no material regression | reject missing commands, a changed methodology or report-only replay |

- **`rv5_evidence.py` is a planned evidence tool (not an existing repository capability).** It must log every underlying command, exit, artifact source and source-snapshot identity; empty test selections, absent source evidence and wrong-revision roots must fail. Its negative self-tests (wrong revision/root, tampered file, missing evidence, open required row, empty test selection, provider skip) go to `tool-selftests.json` and are **required for fulfillment**.
- **Hash/seal dependency order:** payload reports + manifest → hash the complete payload → seal hashes the payload ledger → the independent CHECK receipt hashes manifest + ledger + seal; envelope files are explicitly identified.
- **Final schema invocation:** `python3 scripts/validate_inner_loop.py manifest "$R/manifest.json"` — expected exit 0. **Schema success alone is not acceptance.**
- **CHECK re-gather budget:** exactly **two targeted batches** (1: V05/D-09/current bilingual+example assertions; 2: handoff ownership/overlap and evidence/provenance contradictions); missing reports first consume the applicable batch and alone do not justify another DO iteration or revision; persistent low confidence after the budget → `escalate` trigger 5.

## DO units (r=5)

Scouts gather facts read-only; every listed unit is coder-owned except where a fact stream is named. Each unit lists owner, write footprint, dependency and the evidence it must produce.

| Unit | Owner | Write footprint | Dependency | Evidence it must produce |
|---|---|---|---|---|
| `D232-RV5-01` contract/source-evidence identities | coder | status file + rv5 root | none | `contract.json`, initial `manifest.json`, `source-snapshot.json`, `foreign-baseline.json`, `tools/rv5_evidence.py`, `tool-selftests.json` |
| `D232-RV5-02` prove the local V23 rejection condition | scout (facts), coder (experiment) | rv5 experiment artifacts; planned `tests/nextorm.sqlserver.tests/T232Rv5AmbiguityTests.cs` | D232-RV5-01 | rv5 experiment artifacts + `v23/probe.json`; **STOP if no valid local discriminator** — never touch `DefaultColumnsProvider.cs` |
| `D232-RV5-03` implement/test the V23 ambiguity guard | coder | `src/nextorm.core/Visitors/NormSqlTranslator.cs:96-106` + local helper | D232-RV5-02 | `v23/assertion-execution.json` + a mutation showing the guard's removal reddens the negative test |
| `D232-RV5-04` V18 SQL Server execution | coder | `tests/nextorm.sqlserver.tests/T232VariantClosureTests.cs:91,113` + planned `tests/nextorm.integration.tests/T232SqlServerTestsRv5.cs` | D232-RV5-01 | execute a typed PIVOT declaring a valid value absent from the fixture, keep a group present through another value, assert the database result is NULL; keep the emission test as complementary |
| `D232-RV5-05` V05 mapping + current docs/example audits + `reports/D-09.json` | scout (audits), coder (transcription) | `docs/**` audits (no guide/example content change planned) | D232-RV5-01 | current V05 mapping audit + bilingual/XML-doc/example audits + `reports/D-09.json` |
| `D232-RV5-06` both inherited security dispositions | scout (duplicate search), coder (issue/evidence) | issue milestone `1.0.9-rc3`; rv5 security artifacts | D232-RV5-01 | issue titled exactly `Identifier safety: SQL Server bracket escaping and raw UNPIVOT/Column<T> names`; both High findings recorded with the exact verdict text `accepted risk, not a blocker`; no production fix, no allowlist extension |
| `D232-RV5-07` regression/coverage/mutation/perf | coder | rv5 regression/coverage/perf artifacts | D232-RV5-03, D232-RV5-04 | regression + comparable coverage (line ≥85 / branch ≥75, no touched-area decrease) + targeted mutation + the seven-case perf replay |
| `D232-RV5-08` reconcile historical contradictions + current fulfillment | coder | rv5 reconciliation/fulfillment artifacts | D232-RV5-07 | `reconciliation.json`, `fulfillment-rv4.json`, `fulfillment-rv5.json`, hash ledger; no rv4 rewrites |
| `D232-RV5-09` finalize AC-12 + seal | coder (gathers), check (decides) | rv5 handoff/seal artifacts | D232-RV5-08 | `handoff.json`, `seal-reconciliation.json`, `seal.json`, `check-receipt.json` |

### r=5 unit ledger

| Unit | State (r=5) | Dependency |
|---|---|---|
| `D232-RV5-01` | active/pending | none |
| `D232-RV5-02` | active/pending | D232-RV5-01 |
| `D232-RV5-03` | active/pending | D232-RV5-02 |
| `D232-RV5-04` | active/pending | D232-RV5-01 |
| `D232-RV5-05` | active/pending | D232-RV5-01 |
| `D232-RV5-06` | active/pending | D232-RV5-01 |
| `D232-RV5-07` | active/pending | D232-RV5-03, D232-RV5-04 |
| `D232-RV5-08` | active/pending | D232-RV5-07 |
| `D232-RV5-09` | active/pending | D232-RV5-08 |

Existing unfinished original `D` units remain active with unchanged criteria/remainder, blocked on these corrective dependencies where applicable; none is silently superseded or completed.

**Gate 1.** Execution mode: `Sequential, current tree, one group. No worktree, branch, merge, commit or push.`

Exact test invocations:
- Build: `dotnet build nextorm.slnx -c Debug`
- `tests/nextorm.sqlserver.tests` filter: `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~T232VariantClosureTests|FullyQualifiedName~T232CoverageClosureTests|FullyQualifiedName~T232TypedPivotTests|FullyQualifiedName~T232MarkerTests|FullyQualifiedName~T232ProjectionContractTests|FullyQualifiedName~T232PreparationProbeTests|FullyQualifiedName~T232DerivedSourceReproTests|FullyQualifiedName~T232Rv5AmbiguityTests"`
- `tests/nextorm.core.tests` filter: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~T232CoverageClosureTests"`
- container filter: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~T232SqlServerTests|FullyQualifiedName~T232CaseSensitiveAliasTests|FullyQualifiedName~T232TypedOperators_RespectCapability"`

Variant delta:
- V18 declared-but-absent value → SQL Server execution + NULL.
- V23 single source → positive; V23 explicit alias → positive; V23 multiple matching sources with an ordinary marker → demonstrated ambiguity + fail-closed guard; unrelated same-named method → unrecognized.
- Plus the carried rows (V01…V17, V19…V22, V24…V29), unchanged.

Coverage: line ≥85 / branch ≥75, comparable touched-area non-decrease, original baseline methodology; the rv4 88.4/80.4 vs 88.3/80.3 observation is **not** a substitute for fresh evidence.

Docs plan: current XML-doc/CS1591 + EN/RU semantic review + example audits; no public→specs links; any newly found docs defect needs an explicit authorized correction.

Perf: **Required** — `NormSqlTranslator.cs:96-106` is on the query preparation/translation path; rerun all seven acceptance cases with the recorded harness/baseline methodology.

Reconnaissance: **Required, bounded** — D232-RV5-02 proves the ambiguity witness and the probe discriminator.

### Allowlist + STOP rules (r=5)

**No extension is granted for r=5.** The retained r=4 authorized set is unchanged, and the planned rv5 code change uses only `NormSqlTranslator.cs` and T232-owned tests.

Retained r=4 authorized set:
- the public marker file (`src/nextorm.core/Query/PivotValues.cs`) and the internal `src/nextorm.core/Visitors/NormSqlTranslator.cs`;
- the authorized example csproj (`examples/nextorm.examples.mssql.adventureworks/nextorm.examples.mssql.adventureworks.csproj`);
- the three `examples/**` README/VERIFICATION docs (`examples/README.md`, `examples/VERIFICATION.md`, `examples/nextorm.examples.mssql.adventureworks/README.md`);
- T232-owned tests (`tests/nextorm.sqlserver.tests/T232*`, `tests/nextorm.core.tests/T232*`, `tests/nextorm.integration.tests/T232*`);
- the cycle status file and the cycle evidence artifacts (`docs/specs/status/D232-sqlserver-pivot-unpivot-1.md`, `docs/specs/status/evidence/pivot-typed-overload/**`, `artifacts/pdca/D232-sqlserver-pivot-unpivot/**`).

Forbidden set: `DefaultColumnsProvider.cs`, `SqlSourceRenderer.cs`, `SqlBuilder.cs`, `Dialect/**`, `PivotExpression.cs`, `BaseExpressionVisitor.cs`, `TableAliasAccessors.cs`, `SqlFunctions.cs`, other `Builders/**`, other project/package/version files, the frozen 14 legacy tests.

**STOP rules (unchanged, binding):** a fix needing `SqlSourceRenderer.cs` / `SqlBuilder.cs` / `Dialect/**`, a frozen oracle change, or a general derived/projection-ownership change STOPS the unit and reports; it does not silently widen the footprint.

## Docs plan

- EN `docs/guide/provider-specific-sql/sqlserver.md:143-207` and the RU mirror `:144-208`: typed examples, grouping/subsets, explicit nullable types, aliases, composition/CTE, UNPIVOT roles and fail-closed errors.
- Explain runtime identifier strings: no compile-time membership guarantee; absent PIVOT values may yield NULL; invalid names/types can fail at runtime.
- Retain and distinguish the legacy/dynamic usage; documentation does not substitute for implementation.
- XML-doc every new public type, constructor, accessor and overload (CS1591 is a build gate).
- Update the AdventureWorks quarterly C#/SQL example to typed members and a single value declaration.
- Add **PIV-TYP1**/#232 to `docs/specs/design/API-NAMING-REVIEW.md`; mark implemented only after accepted evidence. Preserve `PIV-RD2` P2 OPEN/#53.
- Capability tables and `docs/specs/roadmap/sql-capabilities-gap-analysis.md`: **unchanged** (supported operators/providers do not change).
- By-name getting-started guide (`docs/getting-started/03-entities-and-metadata.md:250-287`): unchanged; still valid.
- No new public guide page; no renumbering/toc changes.
- Create `docs/specs/roadmap/todo_sqlserver_typed_pivot_unpivot.md` as the saved feature plan, referencing this status and the immutable acceptance/unit IDs (a **DO** artifact, D-01).
- No links from public documentation to internal specs.

## Perf measurement

**Required.** The change affects query construction/preparation/translation (`EntityBuilder.cs:331,343`; `QueryPreparer.cs:489`; `NormSqlTranslator.cs:99-109`), not per-row mapping (`RowMapperFactory.cs:52`).

Run C15 before production edits and after completion, in an identical environment. Require the confirmed seven acceptance cases. Reference baseline: `docs/specs/performance/acceptance-benchmarks.md:37-45,63-95`, cached/prepared ratio 1.87, allocation ratio 7.42 — historical figures do not replace the live baseline.

Capture tables, allocations, ratios and raw BDN artifacts. A delta below ~20% is noise, not a claimed improvement. An adverse ≥20% delta requires an explanation and one confirmation run; an unresolved material regression fails AC-11.

The suite is a common query-path regression oracle, not a claim to benchmark typed PIVOT directly. No intentional cache-key-policy change.

## Reconnaissance

Read-only reconnaissance is sufficient for planning; no further speculative spike is needed to choose the design.

**Four execution-only unknowns, carried explicitly**
1. **Actual alias emission** — D-01 runs an existing-PIVOT + `SqlFunctions.Column<T>` preparation probe; D-03/D-04 verify the marker/typed `[1] as [Q1]`. Observable exact-SQL oracle exists.
2. **Metadata timing** — D-01 tests a mapped entity and a naming convention at the extension-call lifecycle. D-04 must use registered effective metadata or an existing safe initialization/preparation route; never silently default before registration.
3. **Marker cache equality** — D-03/D-04 add repeat/interleaved marker/shape/value tests and review existing hashing via Roslyn. Observable reuse/isolation is required; no guessed cache-policy change.
4. **Source-registration ordering** — D-01/D-03 execute projection translation over the actual pivot source; failure to bind `TableAlias` is observable, followed by mandatory derived/CTE/runtime cases.

These are implementation hypotheses with obtainable deterministic oracles, **not undemonstrable acceptance arms**. A failed probe adds an in-cycle prerequisite or prompts a real replan; it does not weaken criteria.

**Committed pre-gate regression arms:** the exact legacy `Pivot_ShouldEmitPivotClause` (`SqlGenerationTests.cs:3060/3074`), the existing throw/cache/derived cases `:3060-3267`, and the eager guards `:27,33,39,45`. No committed typed-feature test is claimed; the new alias/cache/guard assertions and mutation red arms are produced during DO.

CS1591 baseline tension: D-01 captures the actual C04 warnings/errors without reopening the "109 undocumented public types" registry finding.

## Unit mode

Sequential, current tree, one collection group. No branch/worktree/merge/push.

The extension, translator and shared test-contract footprints overlap; parallel mutable-file ownership is prohibited. Coordinate T225's guide edits and finish T232's shared-file handoff before T233.

## DO units

| Unit | Ordered work; disposition | Criteria / scope | Write footprint |
|---|---|---|---|
| D-01 Preparation probes | **Fix now:** baseline inventory/build/perf/coverage; executable name-lifecycle/alias/source probes | AC-03,08,11,12; D-01 subset, C01,C06,C10,C15 | Preparation test; status/roadmap; `A/**` |
| D-02 Test contract | **Fix now:** freeze the shared oracle/fixture contract; buildable legacy-vs-typed-column oracle; test data | AC-01…04,07…09; D-02 subset | Projection-contract test; integration fixture; `A/test-contract.json` |
| D-03 Core markers | **Fix now:** documented core markers, translation-only behaviour, narrow recognition/`TableAlias` binding | AC-05,07,09; D-03 subset | Public marker file; internal `NormSqlTranslator.cs`; marker tests |
| D-04 Provider overloads | **Fix now:** approved overloads, identity rewriter, actual grouping names, ordered extraction/guards; legacy construction plus `Select` | AC-01…04,07…09; D-04 subset | Public extension file; internal provider rewriter; typed SQL tests; shared capability test |
| D-05 Integration/mutation | **Fix now:** SQL Server parity/TVF/CTE/join/cache cases, all-provider capability, branch closure and the six mutants | AC-02,07…09,11; D-05 subset | SQL tests and integration files; `A/**` |
| D-06 Docs/example | **Fix now:** typed example, bilingual semantics, XML completion and the registry | AC-04,05,10; D-06 subset | Example C#/SQL, EN/RU guides, registry, XML on the listed public files |
| D-07 Boundary/handoff | **Fix now:** one boundary, coverage/perf assessment, evidence sealing, CHECK and the authorized collection handoff | AC-01…12; C03,C11,C12,C15–C19 | Status/roadmap/registry final state; `A/**`; no new production scope |

No parallel mutable ownership. Maintain the status/todo at every completed and newly started step.

### Unit ledger (r=3)

| Unit | State | Commit / evidence |
|---|---|---|
| D-01 Preparation probes | done | `9b4bf5d4` (rv1 evidence) |
| D-02 Test contract | done | `04d2289d`; only the oracle amendment is added |
| D-03 Core markers | done | `8f0d8309` |
| D-04 Provider overloads | done | `706dd7b7`; D-04 assumption retained |
| `D232-PREREQ-typed-derived` | done (r=3 / n=2) | fallback `b05e94ba` + pass-through fix (this D-05 seal commit); R01–R07 green, V26/V27 green, C31 all 9 mutants killed |
| `D232-AMEND-unpivot-null-oracle` | done | C24 exit 0; `rv3/oracle/amendment.json` (R1 contract key `unpivot_null_cell`) |
| D-05 Integration/mutation | **evidence-complete (r=3 delta)** | C21 29/29; C22 11/11 (0 skips); C23 eight suites exit 0 + integration 3922 total / 0 failed / 198 capability skips / **0 provider skips** (six providers executed); C29 V29 1/1; C07 six-provider 6/6 (0 skips); C31 9/9 mutants killed, restore gate exit 0; C25 0/0 both builds; manifest 16 `met` / 14 `open` (D-06/D-07/CHECK). Docs/example (D-06) + boundary/handoff (D-07) remainder retained |
| D-06 Docs/example | **done (r=3)** | C13 exit 0 (typed AdventureWorks example vs. the locally packed 1.0.9-rc3-local feed; `packaging_caveat` recorded), C14 exit 0 (XML-doc/CS1591 0/0 + EN/RU parity + specs-link scan clean), narrow affected rerun 29/29; XML docs on `PivotValues.cs`/`SqlServerEntityBuilderExtensions.cs`; **PIV-TYP1** registered (pending acceptance); T53 `#232` candidates = 9, freeze HELD; rv3 `example-review.json`/`docs-review.json`/`reports/D-06.json` |
| D-07 Boundary/handoff | **done** | C11 boundary exit 0 (seven suites + six-provider container run, 0 provider skips; `rv1/boundary.json`, `rv1/boundary-summary.json`); C12 coverage candidate exit 0 (line 88.3% / branch 80.2%; `rv1/coverage-summary-candidate.json`, `rv1/branch-delta.json`); C15 candidate exit 0 (7 cases, cached/prepared 2.12 vs 2.01, noise; `rv1/perf-comparison.json`); C16 exit 0 (`rv1/footprint.json`, `rv1/d07-footprint.json`); C17/C18/C19 exit 0; rv3 manifest **27 met / 3 open**; rv3 D-07 brief/report |

No unit is superseded. D-05's r=3 delta is evidence-complete; D-06 and D-07 are done. The whole cycle remains open until CHECK.

### Unit ledger (r=4) — reconciled

D-01…D-05 (incl. the r=3 delta), `D232-PREREQ-typed-derived` and `D232-AMEND-unpivot-null-oracle`: **done** (commits preserved). **D-06 and D-07 are reopened for acceptance — not superseded.**

| Unit | State (r=4) | Commit / evidence |
|---|---|---|
| D-01 Preparation probes | done | `9b4bf5d4` |
| D-02 Test contract | done | `04d2289d` |
| D-03 Core markers | done | `8f0d8309` |
| D-04 Provider overloads | done | `706dd7b7` |
| `D232-PREREQ-typed-derived` | done (r=3 / n=2) | `b05e94ba` + pass-through fix |
| `D232-AMEND-unpivot-null-oracle` | done | C24 exit 0 (`rv3/oracle/amendment.json`) |
| D-05 Integration/mutation | done (r=3 delta evidence-complete) | C21/C22/C23/C29/C31/C07 sealed at rv3 |
| D-06 Docs/example | **reopened for acceptance** | r=3 provisional `001f9c9c`; packaging/docs obligations now active under D-08 (packaging) and D-11 (foreign hunks) |
| D-07 Boundary/handoff | **reopened for acceptance** | r=3 provisional `4dc0f6b3`; coverage/handoff/seal obligations now active under D-10/D-11/D-12 |
| **D-08 Packaging remediation** | **active** | AdventureWorks csproj + example docs: plain build (no version override, no feed masking) and literal C13 exit 0; reject overrides/feed masking. Depends on: none. Evidence row `EC232-RV4-PACKAGING`. |
| **D-09 Variant remediation** | **active** | V05/V18/V19/V21 executable closure (tests + EN/RU guide), V14/V22 preserved. Depends on: none. Rows `EC232-RV4-V05/V18/V19/V21`. |
| **D-10 Coverage remediation** | **active** | thresholds **AND** comparable touched-scope non-decrease; never alter denominators/exclusions to manufacture a pass. Depends on: D-09 (final touched scope). Rows `EC232-RV4-COVERAGE`, `EC232-RV4-PERF`; `E232-11` carried open. |
| **D-11 Ownership/handoff remediation** | **active** | `rv4/ownership.json` + `rv4/handoff.json`: verified #160 sibling-owner identity + explicit post-discovery authorization, per-hunk byte hashes, T232-only footprint, clean paths, CRLF. Depends on: none. Rows `EC232-RV4-OWNERSHIP`, `EC232-RV4-HANDOFF`; `E232-16` reopened. |
| **D-12 Evidence reconciliation/seal** | **active** | rv4 evidence reconciliation and seal: retract unsupported `met` claims; seal rv4 only after the substantive gates. Depends on: D-08…D-11, D-13. Row `EC232-RV4-SEAL`; `E232-17`, `EC232-RV3-SEAL` carried open. |
| **D-13 Security prerequisite coordination** | **active (blocker `B232-AUDIT-DEPENDENCY`)** | advisory capture + duplicate search + separate-issue remedy ownership. Depends on: external authority/resource. Row `EC232-RV4-SECURITY`. |

### Allowlist reconciliation (r=4)

The original planned files are kept. The rv3-approved additive exceptions remain allowed, and **CHECK explicitly rejected the alleged footprint violation** for them (they were the authorized additive fallback, not unapproved production scope):

- `Builders/EntityBuilder.cs` — internal marker/projection-preserving handoff only.
- `DataContext/DataContextExtensions.cs:1377` — gated builder-to-command routing only.
- `Visitors/MemberTranslator.cs:834` — the reproduced guard + neighbouring helper only.
- `Expressions/FromExpression.cs` — internal marker only (indispensable case only).
- `QueryCommand.QueryPreparer.cs` — **the approved additive fallback only** (`TryGetReusableDerivedShape` + the n=2 `DerivedPassThroughColumn`/`TryGetColumnIdentifier` pass-through).

**Exactly one new packaging exception is added for r=4:**

- `examples/nextorm.examples.mssql.adventureworks/nextorm.examples.mssql.adventureworks.csproj` — the authorized D-08 packaging route (ProjectReference in place of the `nextorm`/`nextorm.sqlserver` PackageReferences).

**All other project/package files stay forbidden** (`examples/Directory.Packages.props`, the root version properties and every other `*.csproj`/`*.props`).

### Open defect blocks (r=4)

Originating CHECK: **r=3 / n=2 / rv=3** (failed build/security/unsealed evidence; primary route CHECK → PLAN).

```text
key: D232-D06-example-package-mismatch
severity: P1
state: open
owner: coder-example (D-08)
originating_check: r=3 / n=2 / rv=3
dependent_units: D-08
required_evidence_rows: EC232-RV4-PACKAGING
scenario: the AdventureWorks example consumed published packages; the typed PIVOT/UNPIVOT API is absent from published 1.0.9-rc2, so C13 was green only against a locally packed feed with NuGetAudit=false (version override + feed masking)
required_acceptance: AC-10 + the literal C13 `dotnet build examples/nextorm.examples.mssql.adventureworks/nextorm.examples.mssql.adventureworks.csproj -c Debug` exits 0 with no version override and no local feed
negative_acceptance: a pass manufactured by `-p:NextOrmVersion=...`, `RestoreAdditionalProjectSources`, or any feed masking does NOT satisfy this block
authorized_route: replace the `nextorm`/`nextorm.sqlserver` PackageReferences with ProjectReferences to the current checkout (the packaging exception above)
```

```text
key: P232-variant-closure-incomplete
severity: P1
state: open
owner: coder-sql-tests (D-09)
originating_check: r=3 / n=2 / rv=3
dependent_units: D-09
required_evidence_rows: EC232-RV4-V05, EC232-RV4-V18, EC232-RV4-V19, EC232-RV4-V21
scenario: V05 (TVF inner), V18 (typo/absent data value), V19 (dynamic PIVOT guard) and V21 (eager/single-query state) were not executable-closed at rv3
required_acceptance: each of V05/V18/V19/V21 has an executable closure (selected > 0, exit 0) with the EN/RU guide updated; V14/V22 preserved
negative_acceptance: a variant claimed closed without an executed, selected>=1 case is not accepted
```

```text
key: D232-D07-touched-coverage-regression
severity: P1
state: open
owner: coder-coverage (D-10)
originating_check: r=3 / n=2 / rv=3
dependent_units: D-10
required_evidence_rows: EC232-RV4-COVERAGE
scenario: the rv3 candidate met the absolute thresholds (line 88.3 / branch 80.2) but the comparable touched-scope percentage decreased (SqlServerEntityBuilderExtensions, NormSqlTranslator, QueryCommand.QueryPreparer)
required_acceptance: candidate meets MIN_LINE_COVERAGE/MIN_BRANCH_COVERAGE AND the comparable touched-scope coverage does not decrease
negative_acceptance: never alter denominators/exclusions (coverage.settings), suites or tool versions to manufacture a pass
```

```text
key: D232-D06-foreign-hunks-in-handoff
severity: P1
state: open
owner: coder-handoff (D-11)
originating_check: r=3 / n=2 / rv=3
dependent_units: D-11
required_evidence_rows: EC232-RV4-OWNERSHIP, EC232-RV4-HANDOFF
scenario: the #232 D-06 commit (001f9c9c) folded pre-existing foreign #160 hunks (AsCte/WithAlias) into examples/.../AdventureWorksQueries.cs without explicit authorization
required_acceptance: explicit #160-authorization record, per-hunk byte preservation, a corrected handoff, T232-only footprint and clean paths (CRLF)
negative_acceptance: an implicit/blanket or retro-fitted authorization, or any foreign byte changed, is not accepted
```

```text
key: E232-seal-incomplete
severity: P1
state: open
owner: CHECK (D-12)
originating_check: r=3 / n=2 / rv=3
dependent_units: D-12
required_evidence_rows: EC232-RV4-SEAL
scenario: the rv3 seal carried `met` claims unsupported by the failed CHECK (build/security/unsealed evidence)
required_acceptance: retract the unsupported `met` claims (E232-09/E232-13/E232-16 and EC232-BUILD reopened); seal rv4 only after the substantive gates (D-08…D-13) — zero applicable open rows on a valid contract
negative_acceptance: sealing rv4 while unsupported `met` claims or any applicable open row remain is not accepted
```

```text
key: B232-AUDIT-DEPENDENCY
severity: P1
state: blocker
owner: orchestrator (coordination); remedy owner TBD by the separate issue (D-13)
originating_check: r=3 / n=2 / rv=3
dependent_units: D-13 (coordination), D-12 (seal depends on D-13)
required_evidence_rows: EC232-RV4-SECURITY
scenario: an ordinary `dotnet restore/build` fails on the transitive System.Security.Cryptography.Pkcs 9.0.11 advisory (NU1902 GHSA-2fmv-rv2c-59x7 / NU1903 GHSA-846f-f764-76mx) promoted to errors by TreatWarningsAsErrors; the audit dependency is the security prerequisite
required_acceptance: advisory captured with complete identifiers/package paths/exit codes; duplicate search run; remedy ownership tracked by a separate issue; an ordinary audited restore/build must be green
escalation: if the required external authority/resource is unavailable, escalate rather than mask the failure with NuGetAudit=false
```

### D-11 escalate decision (r=4) — ownership/handoff (binding)

The D-11 ownership/handoff blocker (`D232-D06-foreign-hunks-in-handoff`, AC-12) was routed to `escalate`. The ruling is **binding**; verbatim-summarised here.

- **Route 2+3.** T232 does **not** remove, revert or re-commit the foreign #160 hunks, and history is **never rewritten**: `001f9c9c` stays as-is and is recorded as a **mixed** commit (T232 typed-`QuarterlyPivot` work + pre-existing foreign #160 working-tree work in the same file). `commit_001f9c9c_rewritten: false`.
- **AC-12 stays blocked** on the missing authorization. Exact blocker wording:

  > AC-12 open: foreign #160 hunks in 001f9c9c have no identified owner and no explicit retention authorization from the collection owner

- **The parent collection mandate is not authorization.** It forbade touching `examples/**` beyond the example footprint; disclosure is not authorization, so it is not cited as one.
- **Verbatim request to the collection owner** (pending; not yet answered):

  > Подтвердите явно: сохранить ≈30 чужих хунков #160 в `001f9c9c` (файл `AdventureWorksQueries.cs`) с атрибуцией #160 и без переписи истории, да/нет, кто и когда

- **#160 record (facts only):** issue #160 — author `AlexeyShirshov`, state **CLOSED**, milestone **1.0.9-rc2** (#20), no assignees, no labels, url `https://github.com/AlexeyShirshov/nextorm/issues/160`. The issue author is **not** inferred to be the working-tree author — `sibling_owner: "unidentified"`. **No comment is posted on #160 pending the owner's explicit consent** (`comment_status: "pending owner consent"`).
- **Unblocked, continue independently:** D-09 (variant closure), D-10 (coverage/perf) and D-12 (evidence reconciliation/seal) are **not** gated by this ruling and continue; D-11's handoff half and AC-12 stay open.
- **Evidence:** `rv4/ownership.json`; split patches `rv4/ownership/{t232.patch,foreign.patch,001f9c9c.full.patch,hunks/*.patch}`; `rv4/ownership/{hunk-inventory.json,reconstruction.json}`; measurement: 2 T232 hunks vs 11 foreign hunks (30 discrete foreign edits, 117 added / 97 removed foreign lines). Verification: `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/ownership/split_hunks.py` → **exit 0**, both application orders reconstruct `blob(001f9c9c)` = `a54a2c5f`.

### D-07 final state (r=3) — handoff to CHECK

- **Units:** D-01…D-07 done; the two additive units `D232-PREREQ-typed-derived` and `D232-AMEND-unpivot-null-oracle` are done. No unit is superseded.
- **Boundary:** C11 green — the seven per-project suites plus the six-provider container run (SQLite/PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse all executed, **0 provider skips**); the two earlier transient ClickHouse timeouts and the isolation confirmation are recorded in `rv1/boundary-transients.json`.
- **rv3 manifest: 27 `met` / 3 `open`.** Explicit open rows and owners:
  - `E232-11` (AC-06, coverage/branch delta) — **open / owner CHECK→planner**: thresholds met (line 88.3 / branch 80.2) but the *comparable touched-area percentage* decreased on three touched classes (new mostly-defensive branches); absolute covered counts increased. Evidence + both readings: `rv1/branch-delta.json`. Not faked as `met`.
  - `E232-17` (schema/fulfillment/sealing) — **open / owner CHECK** (CHECK-owned by the contract).
  - `EC232-RV3-SEAL` — **open / owner CHECK** (CHECK-owned; C32/C33 pass but the seal verdict is CHECK's).
- **Packaging caveat (for CHECK):** the AdventureWorks example consumes published packages, so C13 builds against a locally packed `1.0.9-rc3-local` feed (`/tmp/opencode/t232-feed`); the literal build against published `1.0.9-rc2` cannot resolve the typed API until rc3 is published (`rv3/example-review.json#packaging_caveat`).
- **AC-12 item for CHECK:** the `#232` D-06 commit (`001f9c9c`) folded in pre-existing foreign edits to `examples/nextorm.examples.mssql.adventureworks/AdventureWorksQueries.cs` (the #160 `AsCte`/`WithAlias` sibling work); that file was in the D-01 foreign baseline and is no longer dirty because D-06 committed it (`rv1/d07-footprint.json`).
- **Environment note:** `NuGetAudit=false` / the local feed are required for the benchmark and the example build (pre-existing NU1902/NU1903 advisory); this is an environment workaround, not a change exemption.
- **Not closed:** no `ACT` was started and the cycle is not declared closed.

### D-05 evidence (r=3 delta) — sealed (not a CHECK verdict)

Concrete numbers, all from executed runs (logs under `artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/`):

| Invocation | Command (abridged) | Exit | Selected / passed / failed / skipped |
|---|---|---|---|
| C29 | `dotnet test tests/nextorm.integration.tests -c Debug --filter FullyQualifiedName~T232CaseSensitiveAliasTests` (+`DOCKER_HOST`) | 0 | 1 / 1 / 0 / 0 — `Latin1_General_100_CS_AS` |
| C21 | `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter FullyQualifiedName~T232TypedPivotTests` | 0 | 29 / 29 / 0 / 0 |
| C22 | `dotnet test tests/nextorm.integration.tests -c Debug --filter FullyQualifiedName~T232SqlServerTests` (+`DOCKER_HOST`) | 0 | 11 / 11 / 0 / 0 |
| C23 (suites) | `dotnet test tests/nextorm.{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse,alias}.tests -c Debug` | 0 ×8 | core 2529/2529, sqlite 1747/1746/0/1 (pre-existing LOB probe), sqlserver 948/940/0/8 (`Explicit` R-probes), postgres 976/976, mysql 454/454, mariadb 336/336, clickhouse 733/733, alias 147/147 |
| C23 (boundary) | `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` | 0 | 3922 / 3724 / 0 / 198 capability-reason skips, **0 provider skips** |
| C23 (per provider) | `--list-tests` inventory minus skipped | 0 | sqlite 775, sqlserver 815, postgres 916, mysql 685, mariadb 117, clickhouse 290 passed |
| C26/C27/C28 | `variant derived-builder / derived-command / pivot-free` | 0 ×3 | 1 / 1 / 0 / 0 each |
| C25 | `dotnet build src/nextorm.core` + `src/nextorm.sqlserver` | 0 ×2 | 0 warnings / 0 errors both |
| C30 | `dotnet test … --filter FullyQualifiedName~TypedPivot_RepeatedAndInterleavedQueries` | 0 | 1 / 1 / 0 / 0 |
| C31 | 9 controlled prerequisite mutants | — | 9/9 killed; restore gate exit 0 (0 warnings / 0 errors) |
| C24 | UNPIVOT-NULL oracle amendment | 0 | exactly one key amended |
| C32-equivalent | `validate_inner_loop.py manifest rv3/manifest.json` | 0 | 16 `met` / 14 `open` |

Open rows (genuinely unproven at D-05, never faked): `E232-03/04/05/11/12/13/14/15/17` (D-06/D-07/CHECK), `E232-07/08/09/10` (rv1 D-05 rows; the r=3 delta evidence is carried by the `EC232-*` rows, rv1 reconciliation is D-07/CHECK) and `EC232-RV3-SEAL` (CHECK).

**Footprint allowlist (r=3, narrowly gated — engine changes previously excluded):** `Builders/EntityBuilder.cs` (internal marker/projection-preserving handoff only), `DataContext/DataContextExtensions.cs:1377` (gated builder-to-command routing only), `Visitors/MemberTranslator.cs:834` (the reproduced guard + neighbouring helper only), `Expressions/FromExpression.cs` (internal marker only if indispensable; the preferred design needs none), T232-owned tests under core/SQL Server/integration. Still forbidden: `SqlSourceRenderer.cs`, `SqlBuilder.cs`, `Dialect/**`, `PivotExpression.cs`, `BaseExpressionVisitor.cs`, `TableAliasAccessors.cs`, `SqlFunctions.cs`, other `Builders/**`, project/package files, the frozen 14 tests.

**STOP rules (unchanged, binding):** a fix needing `SqlSourceRenderer.cs` / `SqlBuilder.cs` / `Dialect/**`, a frozen oracle change, or a general derived/projection-ownership change STOPS the unit and reports; it does not silently widen the footprint.

## Risks

- **High:** mapping too early or a wrong derived name — D-01 lifecycle proof and D-04 actual-source resolution.
- **High:** aliases valid only at top level — mandatory derived/CTE/join execution.
- **High:** a wrong non-nullable type against SQL NULL — explicit ownership, deterministic tests/docs.
- **High:** marker recognized as a source type or an arbitrary method — `TableAlias` binding and exact new-method recognition.
- **Medium:** cache equality misses shape/value differences — interleaved tests and source review.
- **Medium:** ordinary marker use is ambiguous in joins — fail clearly; typed overloads carry explicit rewritten source binding.
- **Medium:** coverage inputs differ — freeze suites/settings/versions.
- **Medium:** tools/containers fail — an evidence gap, not a skip exemption.
- **Medium:** sibling overlap or foreign artifact damage — path ownership and byte-preservation audit.
- **Low:** baseline documentation-warning tension — capture the actual build; do not broaden into registry cleanup.

## Deferred + trigger

No #232 acceptance requirement is deferred.

- #53 PublicAPI freeze / PIV-RD2 — existing ownership; trigger: #53 resumes (add both new public surfaces to its candidates). Stays in its own milestone tracking; no #232 slice is moved.
- Typed runtime-generated dynamic shapes — outside approved scope; trigger: an explicit new requirement. The legacy API remains usable now.
- Repository-wide Stryker rollout — outside scope; trigger: approved infrastructure work. Targeted mutation remains mandatory now.
- Any unfinished #232 slice remains active in **1.0.9-rc3**; splitting requires an active unit/issue with a duplicate-search first, never a later-release deferral or documentation substitution.

## Decisions made

- **Gate 1 satisfied, `plan_state: ready`**, with the contract pinned before DO.
- The public naming helper and provider internals access eliminate the need for new core mapping/replacement helpers.
- Only the necessary core edits: the public marker file and internal `NormSqlTranslator.cs`.
- A provider-local identity rewriter avoids the existing visitors' type-wide parameter replacement.
- Preserve legacy `TableAlias` behaviour; do not infer why `ColumnName` was historically unset.
- `OrderBy` resolves defining expressions; tests must not assume alias/ordinal sorting.
- Derived naming follows the actual projection descriptors and existing alias rules.
- Duplicate comparison follows the existing identifier representation with ordinal equality; no invented collation equivalence.
- UNPIVOT requires both roles; repeated consistent references reuse a role, conflicting identifiers fail.
- No new capability flag; the existing dialect capability/renderer remains authoritative.
- Supplemental provenance/fulfillment checking is explicit because the existing validator ignores additional evidence fields.
- **D-04 clarification (adjudicated by the planner; `r=1`, `rv=1`, no replan, D-04 stays active, not superseded).** The current design is kept: the typed overloads return `EntityBuilder<TShape>` over a derived source and callers consume it through an explicit `Select(...)` before materialization. No `_exp`/select-list initialization is added to support projection-free terminals. The recorded assumption is:
  > Typed Pivot/Unpivot return an EntityBuilder&lt;TShape&gt; over a derived source. Callers must provide an explicit Select(...) before materialization; direct terminal materialization of that returned builder is outside T232's acceptance scope. This does not relax typed Where/OrderBy/Select, derived-source/CTE composition, SQL/data parity, or the declared shape and cell-type requirements.
- **D-04 oracle corrections (same adjudication, one axis each).** (1) The typed-consumption remark must state only the local typed-consumption contract — the unsupported generalization "like every derived source in the engine" is removed (evidence does not establish it). (2) The cell-type oracle asserts the declared cell member's CLR type (reflected off the returned shape), not the anonymous `TShape`; the `decimal?` expectation is kept. (3) The whole-source oracle is split: the marker-free `new { M = m }` input hits the first-firing `ArgumentException` (`requires at least one value`, `PivotProjectionRewriter.cs:54-57`) and is asserted as rejection of a projection without a value declaration; a separate case with a valid `PivotValues.Value<T>` declaration plus a prohibited whole-source reference targets the real direct-member branch (`PivotProjectionRewriter.cs:329-330`).

## Next plan

On continuation (collection lane resumes this same cycle from DO): execute D-01…D-07 **sequentially**. Before each production unit validate its brief; afterwards validate the report with the actual counts/exits.

**r=4 continuation:** the r=3 CHECK failed, so the next DO runs the rv4 remediation units **sequentially**: D-08 (packaging), D-09 (variant closure), D-10 (coverage/perf), D-11 (ownership/handoff), D-13 (security prerequisite coordination, blocker `B232-AUDIT-DEPENDENCY`), then D-12 (evidence reconciliation/seal). Each delta row's exact invocation is `python3 "artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/verify.py" --case <case>`. Do not seal rv4 while any applicable row is open or an unsupported `met` claim stands.

**r=5 continuation:** execute D232-RV5-01…09 sequentially in the current tree. After rv5 genuinely seals, update the collection ledger and proceed to T233, then T201. If the handoff remains open, retain T232's active remainder and its explicit dependency; do not mark T232 sealed or advance its acceptance state.

If a probe reveals a prerequisite, keep the original `D` active and blocked on the added dependency — not done or superseded. A genuine scope replacement requires an explicit mapping and preserved criteria.

CHECK→PLAN creates a new `P:` task with the actual correction. Only materially changed work increments `r` and resets `n`; absent reports/renaming do not. Apply the no-fourth-attempt and persistent-low-confidence escalation rules.

## Changed files

**Actual:** none claimed by PLAN.

**Planned tracked paths** (public/API flagged)
- **PUBLIC** `src/nextorm.core/Query/PivotValues.cs`
- **PUBLIC** `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs`
- internal `src/nextorm.core/Visitors/NormSqlTranslator.cs`
- internal `src/nextorm.sqlserver/Extensions/PivotProjectionRewriter.cs`
- test `tests/nextorm.sqlserver.tests/T232PreparationProbeTests.cs`
- test `tests/nextorm.sqlserver.tests/T232ProjectionContractTests.cs`
- test `tests/nextorm.sqlserver.tests/T232MarkerTests.cs`
- test `tests/nextorm.sqlserver.tests/T232TypedPivotTests.cs`
- test `tests/nextorm.integration.tests/CommonTestSuite.T232Pivot.cs`
- test `tests/nextorm.integration.tests/T232SqlServerTests.cs`
- test `tests/nextorm.integration.tests/T232PivotFixture.cs`
- example `examples/nextorm.examples.mssql.adventureworks/AdventureWorksQueries.cs`
- example `examples/nextorm.examples.mssql.adventureworks/Sql/mssql_quarterly_pivot.sql`
- docs `docs/guide/provider-specific-sql/sqlserver.md`
- docs `docs/ru/guide/provider-specific-sql/sqlserver.md`
- registry `docs/specs/design/API-NAMING-REVIEW.md`
- plan `docs/specs/roadmap/todo_sqlserver_typed_pivot_unpivot.md`
- status `docs/specs/status/D232-sqlserver-pivot-unpivot-1.md`

**rv=4 additive exception (authorized, D-08)** — exactly one new non-source path joins the allowlist:
- example `examples/nextorm.examples.mssql.adventureworks/nextorm.examples.mssql.adventureworks.csproj` — `nextorm`/`nextorm.sqlserver` `PackageReference` → `ProjectReference` to the current checkout.
- docs `examples/README.md`, `examples/VERIFICATION.md`, `examples/nextorm.examples.mssql.adventureworks/README.md` — note the explicit current-checkout exception.

All other project/package files stay forbidden (`examples/Directory.Packages.props`, root version properties, every other `*.csproj`/`*.props`).

Evidence plumbing/results: `artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/` + `.../rv4/`; generated benchmark outputs are recorded, not indiscriminately staged.

Preserve CRLF; normalize only edited text files. Collection commit only after the authorized continuation/acceptance, with explicit task paths and the `#232` prefix; never `git add -A`, merge or push.

## Pointers

- #232 approved specification; related #231, #160, #53.
- Public naming helper `MemberInfoExtensions.cs:44`; metadata `DataContextCache.cs:51`.
- Type-keyed sources `DefaultColumnsProvider.cs:14,121,243`; `SqlSourceRenderer.cs:1014`.
- Inner/source rendering `SqlSourceRenderer.cs:1002,1008-1009,1023-1025`.
- Identity correction `ReplaceExpressionVisitor.cs:17,22-25`; provider internals `nextorm.core.csproj:81`.
- Alias translation `NormSqlTranslator.cs:45-47,106-107`; `BaseExpressionVisitor.cs:442-475`.
- Sorting `QueryPreparer.cs:2594,2626-2661`; derived names `MemberTranslator.cs:811-814`.
- Ordinal mapping `RowMapperFactory.cs:52`; legacy exact SQL `SqlGenerationTests.cs:3074`.
- Validator `scripts/validate_inner_loop.py:172-277,495-562,586-591`.
- Build/docs warnings `src/Directory.Build.props:11`; `Directory.Build.props:40`.
- Containers `docs/specs/status/collection-1.0.9-todo.md:127,147`; performance reference `docs/specs/performance/acceptance-benchmarks.md:37-45,63-95`.

## Defect history

Stable **defect key** → observed revisions/attempts, number of fixes applied, evidence pointers, latest recurrence/outcome. Session/`task_id` resets never clear this table or `r`/`n`.

| Defect key | Observed in | Fixes applied | Evidence | Status |
|---|---|---|---|---|
| `D232-D04-typed-remark-overgeneralized` | r=1 / n=1 | 1 | `tests/nextorm.sqlserver.tests/T232TypedPivotTests.cs:17-24` | fixed in D-04 (test green) |
| `D232-D04-celltype-oracle-anonymous-shape` | r=1 / n=1 | 1 | `tests/nextorm.sqlserver.tests/T232TypedPivotTests.cs:244-274` | fixed in D-04 (test green) |
| `D232-D04-wholesource-oracle-wrong-branch` | r=1 / n=1 | 1 | `tests/nextorm.sqlserver.tests/T232TypedPivotTests.cs:431-457`; `src/nextorm.sqlserver/Extensions/PivotProjectionRewriter.cs:54-57,329-330` | fixed in D-04 (both cases green) |
| `D232-D05-derived-fallback-insufficient` | r=3 / n=1 | 1 (n=2, escalate-authorized; `n=3` forbidden) | `artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/derived/blocker.json`; `.../rv3/mutation/prerequisite.json`; commit `b05e94ba` (insufficient fallback) → n=2 fix in `QueryCommand.QueryPreparer.cs` | fixed at n=2 (R01–R07 + V26/V27 green; PP1 revert mutant kills the R-probes and the SQL Server derived fact) |
| `D232-D06-example-package-mismatch` | r=3 / n=2 | 0 | `rv4/security/c13-example-build.out`; `rv4/packaging/packaging.json` | **open** (D-08 packaging remediation) |
| `P232-variant-closure-incomplete` | r=3 / n=2 | 0 | `rv4/variants/` (planned) | **open** (D-09 variant remediation) |
| `D232-D07-touched-coverage-regression` | r=3 / n=2 | 0 | `rv1/branch-delta.json`; `rv4/coverage/` (planned) | **open** (D-10 coverage remediation) |
| `D232-D06-foreign-hunks-in-handoff` | r=3 / n=2 → r=4 / n=1 | 0 | `rv4/ownership.json`; `rv4/ownership/hunk-inventory.json`; `rv4/ownership/reconstruction.json`; `rv1/d07-footprint.json`; `rv4/handoff.json` (pending) | **open / AC-12 blocked** — escalate ruling route 2+3: retain the foreign #160 hunks, never rewrite history, `001f9c9c` recorded as a **mixed** commit; awaiting the collection owner's explicit retention authorization |
| `E232-seal-incomplete` | r=3 / n=2 | 0 | `rv3/check/summary.json`; `rv4/manifest.json` | **open** (D-12 evidence reconciliation/seal) |
| `B232-AUDIT-DEPENDENCY` | r=3 / n=2 | 0 | `rv4/security/advisories.json`; `rv4/security/gh-search/`; tracking issue #236 (`rv4/security/issue.json`) | **blocker** (D-13 security prerequisite coordination; remedy owned by #236) |
| `T232.V18.ASSERTION-STRENGTH` | r=4 | 0 (r5 completed fixes = 0) | rv5 unit D232-RV5-04 (planned) | **open** → D232-RV5-04 |
| `T232.V23.AMBIGUITY-GUARD` | r=4 | 0 (r5 completed fixes = 0) | rv5 units D232-RV5-02–03 (planned) | **open** → D232-RV5-02–03 |
| `T232.RV3-SEAL.RECONCILIATION` | r=4 | 0 (r5 completed fixes = 0) | rv5 units D232-RV5-08–09 (planned) | **open** → D232-RV5-08–09 |
| `T232.RV4.FULFILLMENT-PROVENANCE` | r=4 | 0 (r5 completed fixes = 0) | rv5 unit D232-RV5-08 (planned) | **open** → D232-RV5-08 |
| `T232.COUNTERS.HASH-VALIDITY` | r=4 | 0 (r5 completed fixes = 0) | rv5 units D232-RV5-08–09 (planned) | **open** → D232-RV5-08–09 |
| `T232.HANDOFF.STRICT-CLEAN` | r=4 | 0 (r5 completed fixes = 0) | rv5 unit D232-RV5-09 (planned) | **open** → D232-RV5-09 |
| `T232.V05.ASSERTION-EXECUTION` | r=4 | 0 (r5 completed fixes = 0) | rv5 unit D232-RV5-05 (planned) | **open** → D232-RV5-05 |
| `T232.RV4.CONTRADICTORY-ARTIFACTS` | r=4 | 0 (r5 completed fixes = 0) | rv5 unit D232-RV5-08 (planned) | **open** → D232-RV5-08 |
| `T232.SECURITY.BRACKET-ESCAPING` | inherited code | 0 (r5 completed fixes = 0; no code fix) | rv5 unit D232-RV5-06 disposition (planned) | **open disposition** → D232-RV5-06 |
| `T232.SECURITY.RAW-IDENTIFIERS` | inherited code | 0 (r5 completed fixes = 0; no code fix) | rv5 unit D232-RV5-06 disposition (planned) | **open disposition** → D232-RV5-06 |

### Open defect blocks (r=3)

```text
key: D232-D05-typed-derived-source-not-composable
state: closed
classification: additive in-cycle prerequisite
remediation_unit: D232-PREREQ-typed-derived
blocked_unit: D-05
defect_1: projection dropped in From(EntityBuilder<T>) through implicit ToCommand(); mandatory runtime repro before production edits
defect_1_repro: reproduced 2026-10-14 (C20 exit 0) — 4/4 builder+command routes fail with QueryPreparationException 'Select must return new anonymous type' at QueryCommand.QueryPreparer.PrepareColumns (:898) reached through PrepareFrom (:271); actual exception type XunitException wrapping NextORM.Core.QueryPreparationException, 1 selected / 1 failed per isolated child, no timeout
defect_1_fix: additive gated fallback (TryGetReusableDerivedShape) added where the branch previously threw — commit b05e94ba; C34 red (fallback absent) / C35 green (fallback in place)
defect_2: possible translation re-entry; unproven until isolated repro
defect_2_repro: not reproduced 2026-10-14 — the isolated children terminate normally (no stack overflow, no process termination, ~0.6 s each); the recorded D-05 'StackOverflow' label is NOT confirmed by this repro and is superseded by the deterministic QueryPreparationException
pivot_free_control: R05 passes (exit 0) — the pivot-free derived route composes, so the failure is PIVOT-specific and no general derived/projection-ownership change is warranted
preserved_acceptance: AC-08 / V14 / V22 P1; D-04 XML promise unchanged
closure: closed at r=3 / n=2 — the fallback was insufficient (D232-D05-derived-fallback-insufficient) and the escalate-authorized n=2 pass-through fix closed it; R01–R07 green and V26/V27 green on the real SQL Server

key: D232-D05-derived-fallback-insufficient
state: closed
classification: in-cycle defect of the additive fallback (C08 red)
symptom: SqlException "Invalid column name '1'" — the fallback set selectList = reusableShape, so the intermediate derived layer re-rendered the inner projection as "[1] as [Q1]" over a derived table that already exposes the aliased column Q1
red_evidence: artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/derived/blocker.json; commit 4674ffde (V26/V27 red, Explicit)
authorization: binding escalate decision — exactly one more fix inside the authorized r=3 branch (n=2); no planner r=4, no n=3 for this defect
fix: one new private static helper DerivedPassThroughColumn + TryGetColumnIdentifier in QueryCommand.QueryPreparer.cs; both fallback sub-branches clone the derived source's columns into IsProjectionOutputReference columns with OutputName = the identifier MakeColumn actually emits (bare N when SqlFunctions.Column(_,N) matches PropertyName case-insensitively, else PropertyName)
invariants_kept: no existing branch changed; no SelectListCache/Cache/_dontCache write; no source-command mutation; frozen oracles unchanged
closure: R01–R07 (unit) green; V26/V27 container facts green (11/11 T232SqlServerTests, 0 skips); six-provider capability C07 green (0 skips)
```

### Repro probe routes (r=3, `T232DerivedSourceReproTests`, all `Explicit`)

Before the additive fallback each typed route threw `QueryPreparationException : Select must return new anonymous type`; with the fallback but before the n=2 fix the emitted middle layer was invalid (`[1] as [Q1]`). At r=3 / n=2 every probe asserts the **whole statement** and is green.

| Probe | Route | Result |
|---|---|---|
| R01 | `ctx.From(<typed EntityBuilder<TShape>>).Select(...)` | green — `select [category], [Q1] from (select [category], [Q1] from (select category, [1] as [Q1] …) as [t2]) as [t3]` |
| R02 | `ctx.From(<typed EntityBuilder<TShape>>).Where(...).Select(...)` | green — same statement + `where (t3.[Q1] > …)` |
| R03 | `ctx.From(<typed QueryCommand<TShape>>).Select(...)` | green — identical to R01 |
| R04 | self-join of the typed derived command | green — both slots qualified (`t3.[category]`, `t6.[category]`) |
| R05 | pivot-free derived control via `From(QueryCommand)` | green — collapses to the bare two-layer projection |
| R05b | pivot-free builder mirror (`From(EntityBuilder<TShape>)`) | green — intermediate layer `[category], [margin]` |
| R06 | unaliased identifier (grouping column) | green — `[category]`, never `[Category]` (V29 prerequisite) |
| R07 | two derived sources with different pivot values on one context | green — `[1]`/`[2]` stay distinct, repeat identical |

```text
key: D232-D05-unpivot-null-oracle
state: closed
classification: independent test-contract amendment
remediation_unit: D232-AMEND-unpivot-null-oracle
blocked_unit: D-05
correction: SQL Server UNPIVOT excludes NULL source cells
closure: exact one-key amendment, before/after hashes and history verified
retained_units: D-02 done; D-04 done
closed_by: C24 exit 0 (exactly one changed key `null_count_expectations.unpivot_null_cell`; before/after sha256 recorded)
```

## Progress log

```text
2026-10-14 17:54:07Z | PLAN | revision r=1 | iteration n=1/3 | PLAN ready — awaiting confirmation (collection PLAN→DO boundary; no user `go` awaited) | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
2026-10-14 19:16:00Z | DO | revision r=1 | iteration n=1/3 | DO started — D-01 preparation probes (evidence-only; no production code) | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
2026-10-14 19:18:39Z | DO | revision r=1 | iteration n=1/3 | D-01 C04 baseline build (src/nextorm.sqlserver): 0 warnings / 0 errors | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/logs/C04-build-sqlserver.out
2026-10-14 19:18:42Z | DO | revision r=1 | iteration n=1/3 | D-01 C01 preparation probes: 6/6 passed — unknown 1 (alias emission) and unknown 4 (source-registration ordering) confirmed | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/preparation.json
2026-10-14 19:18:48Z | DO | revision r=1 | iteration n=1/3 | D-01 C06 legacy baseline: 19/19 passed (16 pre-existing legacy cases + 3 probe-name overlap; 0 failed, 0 skipped) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/legacy-inventory.json
2026-10-14 19:19:33Z | DO | revision r=1 | iteration n=1/3 | Build gate: `dotnet build nextorm.slnx -c Debug` = 0 warnings / 0 errors | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/logs/gate-solution-build.out
2026-10-14 19:22:53Z | DO | revision r=1 | iteration n=1/3 | D-01 C16 footprint: 36 foreign dirty entries captured; none touched (read-only set verified unchanged) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/foreign-baseline.json
2026-10-14 19:25:16Z | DO | revision r=1 | iteration n=1/3 | D-01 C10 coverage baseline captured (line 88.3% / branch 80.3% within coverage.settings scope); wrapped integration run collect_exit=2 on one transient ClickHouse HttpClient timeout (unit projects exit 0) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/coverage-summary-baseline.json
2026-10-14 19:27:04Z | DO | revision r=1 | iteration n=1/3 | D-01 C15 acceptance baseline: 7 cases, 0 failures, wall 47.98s; cached/prepared 2.01 vs historical 1.87 | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/perf/acceptance-baseline.json
2026-10-14 19:27:44Z | DO | revision r=1 | iteration n=1/3 | D-01 complete: brief/report/manifest validators exit 0, scopes/fulfill exit 0; unit state D-01=done, D-02..D-07=pending | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/reports/D-01.json
2026-10-14 19:34:52Z | DO | revision r=1 | iteration n=1/3 | D-02 started — freeze the shared oracle/fixture contract (test-only; no production code) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/test-contract.json
2026-10-14 19:34:52Z | DO | revision r=1 | iteration n=1/3 | D-02 C04 affected build (src/nextorm.sqlserver): 0 warnings / 0 errors | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/logs/C04-build-sqlserver.out
2026-10-14 19:34:52Z | DO | revision r=1 | iteration n=1/3 | D-02 filtered run: 5/5 passed (0 failed, 0 skipped) — legacy-vs-typed-column oracle pinned | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/logs/D02-contract-tests.out
2026-10-14 19:34:52Z | DO | revision r=1 | iteration n=1/3 | D-02 brief/report validators exit 0; scopes exit 0; unit state D-02=done, D-03..D-07=pending | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/reports/D-02.json
2026-10-14 19:38:45Z | DO | revision r=1 | iteration n=1/3 | D-03 started — core markers (PivotValues/UnpivotValues) + narrow exact-definition recognition | src/nextorm.core/Query/PivotValues.cs
2026-10-14 19:38:45Z | DO | revision r=1 | iteration n=1/3 | D-03 C04 affected build (src/nextorm.sqlserver incl. core): 0 warnings / 0 errors | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/logs/C04-build-sqlserver.out
2026-10-14 19:38:45Z | DO | revision r=1 | iteration n=1/3 | D-03 filtered run: 5/5 passed (0 failed, 0 skipped) — markers in ordinary Select, runtime throw, exact-definition recognition, fail-closed unbound source | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/logs/D03-marker-tests.out
2026-10-14 19:38:45Z | DO | revision r=1 | iteration n=1/3 | D-03 regression: solution gate `dotnet build nextorm.slnx -c Debug` = 0 warnings / 0 errors; legacy Pivot/Unpivot + eager guards 26/26 passed (0 failed, 0 skipped) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/logs/D03-legacy-regression.out
2026-10-14 19:38:45Z | DO | revision r=1 | iteration n=1/3 | D-03 brief/report validators exit 0; scopes exit 0; unit state D-03=done, D-04..D-07=pending | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/reports/D-03.json
2026-10-14 20:01:33Z | DO | revision r=1 | iteration n=1/3 | D-04 in progress: planner adjudication recorded — current design kept (typed overloads return EntityBuilder<TShape> over a derived source; callers pass an explicit Select(...) before materialization; no projection-free terminal support added), r=1/rv=1, D-04 stays active | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
2026-10-14 20:18:11Z | DO | revision r=1 | iteration n=1/3 | D-04 repair 1: typed-consumption remark retargeted to the local contract; the unsupported "like every derived source in the engine" generalization removed (defect D232-D04-typed-remark-overgeneralized, 1 fix) | tests/nextorm.sqlserver.tests/T232TypedPivotTests.cs
2026-10-14 20:18:11Z | DO | revision r=1 | iteration n=1/3 | D-04 repair 2: the cell-type oracle reflects the declared shape member (decimal? kept) instead of returning the anonymous TShape (defect D232-D04-celltype-oracle-anonymous-shape, 1 fix) | tests/nextorm.sqlserver.tests/T232TypedPivotTests.cs
2026-10-14 20:18:11Z | DO | revision r=1 | iteration n=1/3 | D-04 repair 3: whole-source oracle split — the marker-free input asserts the first-firing ArgumentException (requires at least one value; PivotProjectionRewriter.cs:54-57), a new case with a valid PivotValues.Value<T> plus a whole-source reference targets the real direct-member branch (PivotProjectionRewriter.cs:329-330) (defect D232-D04-wholesource-oracle-wrong-branch, 1 fix) | tests/nextorm.sqlserver.tests/T232TypedPivotTests.cs
2026-10-14 20:18:11Z | DO | revision r=1 | iteration n=1/3 | D-04 C04 typed-overload build (src/nextorm.sqlserver): 0 warnings / 0 errors | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/logs/C04-build-sqlserver.out
2026-10-14 20:18:11Z | DO | revision r=1 | iteration n=1/3 | D-04 filtered run: 25/25 passed (0 failed, 0 skipped) — exact typed SQL, shapes/cells, grouping-name resolution, composition, cache locality, every fail-closed guard | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/logs/D04-typed-pivot-tests.out
2026-10-14 20:18:11Z | DO | revision r=1 | iteration n=1/3 | D-04 C06 legacy regression: 51/51 passed (0 failed, 0 skipped) — legacy Pivot/Unpivot + eager guards + typed subset, frozen legacy exact SQL preserved | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/logs/D04-legacy-regression.out
2026-10-14 20:18:11Z | DO | revision r=1 | iteration n=1/3 | D-04 shared six-provider capability case authored (CommonTestSuite.T232Pivot.cs + T232PivotFixture.cs with MySQL/PostgreSQL/SQLite inherited and MariaDB/ClickHouse re-pinned hosts); non-container smoke: 6 selected / 1 passed (SQLite — the assertion executes) / 5 skipped (SQL Server is the supporting provider; PostgreSQL, MySQL, ClickHouse, MariaDB are container-unavailable here, C07 runs them in D-05) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/logs/D04-capability-smoke.out
2026-10-14 20:18:11Z | DO | revision r=1 | iteration n=1/3 | D-04 solution gate `dotnet build nextorm.slnx -c Debug` = 0 warnings / 0 errors; integration capability files compile 0/0 | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/logs/D04-gate-solution-build.out
2026-10-14 20:18:11Z | DO | revision r=1 | iteration n=1/3 | D-04 brief/report validators exit 0; scopes exit 0 (all eight briefs/reports exit 0); unit state D-04=done, D-05..D-07=pending | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/reports/D-04.json
2026-10-14 20:51:44Z | PLAN | revision r=3 | iteration n=1/3 | r=3/rv=3 transcribed: additive typed-derived prerequisite + independent oracle amendment; narrow gated footprint allowlist; V26–V29 and delta rows EC232-REPRO…EC232-RV3-SEAL added; r=2/rv=2 (issued, never executed) superseded; n reset to 1 | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
2026-10-14 20:51:44Z | DO | revision r=3 | iteration n=1/3 | DO resumed at r=3: rv3 evidence plumbing, the independent oracle amendment and the typed-derived baseline repro (evidence-only; no production edit) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/
2026-10-14 20:57:00Z | DO | revision r=3 | iteration n=1/3 | rv3 evidence plumbing created; C32 `manifest` validation exit 0 (30 rows: E232-01..17 carried verbatim + 13 delta rows) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/manifest.json
2026-10-14 20:57:04Z | DO | revision r=3 | iteration n=1/3 | D232-AMEND-unpivot-null-oracle done: C24 exit 0 — exactly one changed key `null_count_expectations.unpivot_null_cell`, before/after sha256 recorded, source anchor T232SqlServerTests.cs:143-149; defect D232-D05-unpivot-null-oracle closed | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/oracle/amendment.json
2026-10-14 21:25:00Z | DO | revision r=3 | iteration n=1/3 | D-05 partial work committed as historical/provisional (explicit D232 test + rv1 evidence paths only; no foreign path) | 9d9c9380
2026-10-14 21:27:00Z | DO | revision r=3 | iteration n=1/3 | D232-PREREQ-typed-derived step 1: 5 planned regression probes added (`T232DerivedSourceReproTests`, all `Explicit` so the default suite is unaffected); `dotnet build tests/nextorm.sqlserver.tests -c Debug` = 0 warnings / 0 errors | tests/nextorm.sqlserver.tests/T232DerivedSourceReproTests.cs
2026-10-14 21:57:14Z | DO | revision r=3 | iteration n=1/3 | C20 baseline repro exit 0: R01–R04 red (QueryPreparationException 'Select must return new anonymous type', 1 selected / 1 failed each, ~0.6 s, NO stack overflow / process termination), R05 pivot-free control PASSES; tree/production/oracle hashes frozen | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/repro/baseline.json
2026-10-14 21:57:14Z | DO | revision r=3 | iteration n=1/3 | C20 acceptance baseline: exactly 7 cases, 0 failures, wall 00:00:45 (45.9 s); Cached_ToList/Prepared_ToList ratio 2.12 (rv1 D-01 baseline 2.01, historical 1.87 — within noise); allocation ratio 7.77 | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/repro/logs/C20-acceptance-baseline.out
2026-10-14 21:00:34Z | DO | revision r=3 | iteration n=1/3 | D232-PREREQ-typed-derived repro captured (C20 exit 0) — all four typed routes red with `QueryPreparationException: Select must return new anonymous type` (R01 typed builder `.Select`, R02 typed builder `+Where+Select`, R03 `From(typed QueryCommand).Select`, R04 self-join of the typed command), pivot-free control R05 PASSES (no stack overflow); recursion hypothesis NOT reproduced; STOP routed to the orchestrator | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/repro/baseline.json
2026-10-15 02:20Z | DO | revision r=3 | iteration n=1/3 | D232-PREREQ-typed-derived additive fallback implemented: `TryGetReusableDerivedShape` reuse branch added immediately before the `Select must return new anonymous type` throw in `QueryCommand.QueryPreparer.PrepareColumns` (fires only where it previously threw; no `SelectListCache` write, no `_dontCache`/source-command mutation) | src/nextorm.core/Query/QueryCommand.QueryPreparer.cs
2026-10-15 02:20Z | DO | revision r=3 | iteration n=1/3 | R05 diagnosis: R05 green because `From(QueryCommand)` embeds the projection command directly (non-empty `SelectList`); R01–R04 red because `From(EntityBuilder<TShape>)` materialises through `ToCommand()` to a projection-less intermediate over a derived `SubQuery` with `ColumnShape == null` → `QueryPreparationException` in `PrepareColumns` before the consumer `Select` resolves | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/repro/candidate/SUMMARY.md
2026-10-15 02:20Z | DO | revision r=3 | iteration n=1/3 | Defect D232-D05-typed-derived-source-not-composable: control R05b (pivot-free builder mirror) was RED before the fix with the identical exception → confirmed a pre-existing general core defect of the metadata-less derived builder, not PIVOT-specific; follow-up issue warranted (not created) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/repro/candidate-before/logs/C34-R05b-pivot-free-builder-derived.out
2026-10-15 02:20Z | DO | revision r=3 | iteration n=1/3 | C34 exit 0 — candidate red baseline with the fallback absent (R01–R04 + R05b red, R05 green; expectation met) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/repro/candidate-before/candidate-before.json
2026-10-15 02:20Z | DO | revision r=3 | iteration n=1/3 | C35 exit 0 — candidate with the fallback in place (all six probes green; expectation met) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/repro/candidate/candidate.json
2026-10-15 02:20Z | DO | revision r=3 | iteration n=1/3 | Build gate `dotnet build nextorm.slnx -c Debug` = exit 0, 0 warnings / 0 errors | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/repro/candidate/build-solution.out
2026-10-15 02:20Z | DO | revision r=3 | iteration n=1/3 | Eight project suites (core/sqlite/postgres/sqlserver/mysql/mariadb/clickhouse/alias) exit 0 (sqlserver 946/940/0/6 — 6 skips are the Explicit R01–R05b probes; sqlite 1747/1746/0/1 pre-existing RowId LOB probe) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/repro/candidate/SUMMARY.md
2026-10-15 02:20Z | DO | revision r=3 | iteration n=1/3 | Tripwire NOT hit — no existing branch condition/behaviour, `FromExpression`/`ColumnShape`, non-pivot `ToCommand` or `SelectList` contract change was required; all forbidden files untouched | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/repro/candidate/SUMMARY.md
2026-10-15 02:20Z | DO | revision r=3 | iteration n=1/3 | D-05 NOT done — integration/capability/mutation remainder carried (C07 six-provider zero-skip, C08, C09/C31, V26/V27/V29 container routes) | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
2026-10-15 02:45Z | DO | revision r=3 | iteration n=2/3 | escalate decision recorded: exactly one more fix of D232-D05-derived-fallback-insufficient INSIDE the authorized r=3 branch (QueryCommand.QueryPreparer.cs only); no planner r=4, no n=3 | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
2026-10-15 02:45Z | DO | revision r=3 | iteration n=2/3 | fix applied: DerivedPassThroughColumn + TryGetColumnIdentifier clone the derived source's columns into IsProjectionOutputReference columns (OutputName = bare N when SqlFunctions.Column(_,N) matches PropertyName case-insensitively, else PropertyName); no existing branch changed, no SelectListCache/Cache/_dontCache write, no source-command mutation | src/nextorm.core/Query/QueryCommand.QueryPreparer.cs
2026-10-15 02:45Z | DO | revision r=3 | iteration n=2/3 | build gate `dotnet build nextorm.slnx -c Debug` = exit 0, 0 warnings / 0 errors | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/mutation/logs/C31-restore-gate.out
2026-10-15 02:45Z | DO | revision r=3 | iteration n=2/3 | R01–R07 rewritten to assert the whole statement; `dotnet test tests/nextorm.sqlserver.tests -c Debug --no-build --filter FullyQualifiedName~T232DerivedSourceReproTests -- -explicit only` = exit 0 (8/8, 0 skipped) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/derived/
2026-10-15 02:45Z | DO | revision r=3 | iteration n=2/3 | C22 derived-exec: `DOCKER_HOST=… dotnet test tests/nextorm.integration.tests -c Debug --filter FullyQualifiedName~T232SqlServerTests` = exit 0 (11/11, 0 skipped); V26/V27 promoted from `[Fact(Explicit=true)]` to ordinary facts | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/derived/execution.json
2026-10-15 02:45Z | DO | revision r=3 | iteration n=2/3 | C07 six-provider capability: `… --filter FullyQualifiedName~T232TypedOperators_RespectCapability` = exit 0 (6/6, 0 skipped; SQL Server positive for both operators, five reject both) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/logs/C07-capability.out
2026-10-15 02:45Z | DO | revision r=3 | iteration n=2/3 | C31 mutation 9/9 killed and restored: 6 original D-05 mutants + PP1-revert-pass-through (unit R-probes 7/8 red AND container derived fact red with `Invalid column name '1'`), PP2-drop-output-reference, PP3-always-property-name; restore gate = exit 0, 0 warnings / 0 errors | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/mutation/prerequisite.json
2026-10-15 02:45Z | DO | revision r=3 | iteration n=2/3 | C29 V29 case-sensitive: `… --filter FullyQualifiedName~T232CaseSensitiveAliasTests` = exit 0 (1/1, 0 skipped) — bare `[category]` resolves on Latin1_General_100_CS_AS | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/variants/case-sensitive-alias.json
2026-10-15 03:00Z | DO | revision r=3 | iteration n=2/3 | C29 harness bug fixed (`verify.py` `cmd_variant`): `case-sensitive-alias` re-targeted to `dotnet test tests/nextorm.integration.tests -c Debug --filter FullyQualifiedName~T232CaseSensitiveAliasTests` with `DOCKER_HOST` (the class lives in `T232PivotFixture.cs:269`; the unit-project route produced `Zero tests ran`, exit 8); C29 exit 0 (1/1, 0 skipped); case-sensitive collation now recorded | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/variants/case-sensitive-alias.json
2026-10-15 03:00Z | DO | revision r=3 | iteration n=2/3 | V29 consumers recorded: bare grouping `[category]` and aliased cells `[Q1]`/`[Q2]` resolve on `Latin1_General_100_CS_AS` (asserted inside the executed container test) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/variants/database-collation.json
2026-10-15 03:01Z | DO | revision r=3 | iteration n=2/3 | C21 derived-unit exit 0 (29/29, 0 skipped) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/derived/unit.json
2026-10-15 03:01Z | DO | revision r=3 | iteration n=2/3 | C22 derived-exec exit 0 (11/11, 0 skipped) — typed derived execution on the SQL Server container | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/derived/execution.json
2026-10-15 03:02Z | DO | revision r=3 | iteration n=2/3 | C26/C27/C28 re-run against the fixed harness: derived-builder 1/1, derived-command 1/1, pivot-free 1/1 (all exit 0) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/variants/
2026-10-15 03:03Z | DO | revision r=3 | iteration n=2/3 | C23 shared-regression exit 0: eight project suites exit 0 (core 2529/2529, sqlite 1747/1746/0/1 pre-existing LOB probe, sqlserver 948/940/0/8 `Explicit` R-probes, postgres 976/976, mysql 454/454, mariadb 336/336, clickhouse 733/733, alias 147/147) + container boundary 3922 / 0 failed / 198 capability-reason skips / 0 provider skips; six providers executed (per-provider passed: sqlite 775, sqlserver 815, postgres 916, mysql 685, mariadb 117, clickhouse 290) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/regression/inventory.json
2026-10-15 03:04Z | DO | revision r=3 | iteration n=2/3 | oracle comparison: every pre-existing frozen oracle byte-identical; only the three delta-owned r=3 test files changed (authorized n=2 promotion) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/regression/oracle-comparison.json
2026-10-15 03:04Z | DO | revision r=3 | iteration n=2/3 | rv3 manifest rows flipped `open`→`met`: EC232-DERIVED-UNIT, EC232-DERIVED-EXEC, EC232-SHARED-REGRESSION, EC232-BUILD, EC232-DERIVED-BUILDER, EC232-DERIVED-COMMAND, EC232-PIVOTFREE-PROBE, EC232-CASE-SENSITIVE-ALIAS, EC232-CACHE-LOCALITY, EC232-PREREQ-MUTATION (EC232-ORACLE-AMEND already `met`); 16 `met` / 14 `open` | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/manifest.json
2026-10-15 03:05Z | DO | revision r=3 | iteration n=2/3 | validators exit 0: `validate_inner_loop.py brief` (rv1 D-05) 0, `report` (rv1 D-05) 0, `manifest` (rv3) 0, and `rv1/verify.py scopes` 0 (every D-01..D-05 brief/report re-validated; rv1 `scope-map.json` content unchanged, only C19 log dates/provenance refreshed) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/validation.json
2026-10-15 03:05Z | DO | revision r=3 | iteration n=2/3 | D-05 evidence-complete (r=3 delta) — **not** a CHECK verdict; EC232-RV3-SEAL, D-06 (docs/example) and D-07 (boundary/handoff) remain open | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
2026-10-15 02:45Z | DO | revision r=3 | iteration n=2/3 | regressions: `FullyQualifiedName~T232` exit 0 (53/45/0/8), C06 legacy filter exit 0 (57/55/0/2), full nextorm.sqlserver.tests exit 0 (948/940/0/8), full nextorm.core.tests exit 0 (2529/2529/0/0); frozen 14 legacy tests + 3e894f1f oracle untouched | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/logs/
2026-10-15 03:15Z | DO | revision r=3 | iteration n=2/3 | D-06 done (docs/example): C13 example build exit 0 (typed AdventureWorks example, 0 warnings / 0 errors), C14 XML-doc/CS1591 build exit 0 (0 warnings / 0 errors) + EN/RU semantic parity + public specs-link scan clean, narrow affected rerun `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter FullyQualifiedName~T232TypedPivotTests` exit 0 (29 selected / 29 passed / 0 failed / 0 skipped); rv3 `example-review.json` + `docs-review.json` refreshed | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/reports/D-06.json
2026-10-15 03:15Z | DO | revision r=3 | iteration n=2/3 | D-06 validators exit 0: `validate_inner_loop.py brief` (rv3 D-06) 0, `report` (rv3 D-06) 0, `manifest` (rv3) 0, and `rv3/verify.py scopes` 0 (12/12 — rv1 D-01..D-05 + rv3 D-06 briefs/reports re-validated, `rv3/scope-map.json`) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/validation.json
2026-10-15 03:15Z | DO | revision r=3 | iteration n=2/3 | registry `docs/specs/design/API-NAMING-REVIEW.md` updated: **PIV-TYP1** added — P2 / Шаг 5, status `registered / pending acceptance` (accepted only in CHECK by committed evidence), `#53`/PIV-RD2 preserved; `find -name 'PublicAPI*.txt'` = 0, PublicApiAnalyzers not enabled, freeze not activated | docs/specs/design/API-NAMING-REVIEW.md
2026-10-15 03:15Z | DO | revision r=3 | iteration n=2/3 | T53 `#232` candidates = **9** (2 new public types `PivotValues`/`UnpivotValues` + 2 public parameterless ctors + 3 accessors + 2 typed overloads), tag `#232`; **freeze `HELD`** (not activated); legacy untyped overloads are not candidates | docs/specs/status/collection-1.0.9-todo.md
2026-10-15 03:15Z | DO | revision r=3 | iteration n=2/3 | **OPEN PACKAGING CAVEAT (for CHECK):** the AdventureWorks example consumes PUBLISHED packages (`examples/Directory.Packages.props` `NextOrmVersion`), and the typed `PIVOT`/`UNPIVOT` API is not in published `1.0.9-rc2` — C13 therefore builds against a locally packed `1.0.9-rc3-local` feed (`/tmp/opencode/t232-feed`, `-p:NextOrmVersion=1.0.9-rc3-local -p:RestoreAdditionalProjectSources=<feed>`); the literal `dotnet build <example> -c Debug` against published rc2 cannot resolve the typed API until rc3 is published. Recorded in `example-review.json` (`packaging_caveat`), not hidden | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/example-review.json
2026-10-15 03:15Z | DO | revision r=3 | iteration n=2/3 | environment note: a fresh restore of the SQL Server test project fails NU1902/NU1903 (`TreatWarningsAsErrors`, unrelated advisory); C13/C14 and the narrow rerun use `-p:NuGetAudit=false` as the documented workaround | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/logs/C14-xml-build.cmd.json
2026-10-14 23:20Z | DO | revision r=3 | iteration n=2/3 | D-07 started — boundary/handoff (no new production scope): C11 boundary, C12 candidate coverage, C15 acceptance perf, C16 footprint, C17/C18/C19 sealing | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
2026-10-14 22:23Z | DO | revision r=3 | iteration n=2/3 | C11 attempt 1: seven per-project suites exit 0, container integration exit 1 — one transient ClickHouse `TaskCanceledException` (HttpClient 120 s); classification: container/network transient, no product code; log preserved (logs/C11-attempt1-boundary-integration.out) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/C11-boundary-attempt1-transient.json
2026-10-14 22:44Z | DO | revision r=3 | iteration n=2/3 | isolation confirmation: the timed-out ClickHouse test passes alone (1/1, exit 0, 8 s) — the timeout is environmental | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/boundary-transients.json
2026-10-14 22:55Z | DO | revision r=3 | iteration n=2/3 | **C11 boundary exit 0** (attempt 3): core 2529/2529, sqlite 1747/1746/0/1 (pre-existing LOB probe), postgres 976/976, sqlserver 948/940/0/8 (Explicit R-probes), mysql 454/454, mariadb 336/336, clickhouse 733/733 + container 3922 total / 3724 passed / 0 failed / 198 capability skips / **0 provider skips** (six providers executed) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/boundary.json
2026-10-14 23:00Z | DO | revision r=3 | iteration n=2/3 | C12 candidate coverage exit 0 (collect 0 / report 0): **line 88.3% / branch 80.2%** (baseline 88.3 / 80.3) — both CI thresholds met | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/coverage-summary-candidate.json
2026-10-14 23:05Z | DO | revision r=3 | iteration n=2/3 | **E232-11 left `open` (AC-06):** touched-class *percentage* coverage decreased for SqlServerEntityBuilderExtensions (line 97.4→95.5, branch 80.5→75.0), NormSqlTranslator (branch 89.6→87.2) and QueryCommand.QueryPreparer (branch 85.3→84.8) because the typed feature added new mostly-defensive branches; absolute covered lines/branches increased and no pre-existing covered line/branch was lost. Uncovered new lines and the absolute-vs-percentage distinction recorded; a CHECK/planner decision, never faked as `met` | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/branch-delta.json
2026-10-14 23:15Z | DO | revision r=3 | iteration n=2/3 | C15 acceptance perf candidate exit 0: exactly 7 cases, 0 failures, wall 46.79 s; cached/prepared **2.12 vs baseline 2.01 (+5.5%, noise)**, allocation ratio 7.92 vs 7.77 (+1.9%, noise) — no material regression; `NuGetAudit=false` exported for the run (argv unchanged) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/perf-comparison.json
2026-10-14 23:30Z | DO | revision r=3 | iteration n=2/3 | C16 footprint exit 0: 36 baseline foreign entries preserved (35 still dirty/untouched); +15 new foreign entries are C15's own generated `benchmarks/BenchmarkDotNet.Artifacts/**`; 1 baseline entry (`examples/.../AdventureWorksQueries.cs`) left the set because it was committed by D-06; no foreign path staged; `src`/`tests` clean; edited text files CRLF | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/d07-footprint.json
2026-10-14 23:34Z | DO | revision r=3 | iteration n=2/3 | C17 `validate_inner_loop.py manifest rv3` exit 0; C18 fulfill exit 0 (rv3 + rv1); C19 scopes exit 0 (rv1 D-01..D-05 + rv3 D-06/D-07 briefs/reports); rv3 manifest **27 `met` / 3 `open`** (`E232-11`, `E232-17`, `EC232-RV3-SEAL`) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv3/validation.json
2026-10-14 23:35Z | DO | revision r=3 | iteration n=2/3 | **AC-12 sibling item (for CHECK):** the `#232` D-06 commit (`001f9c9c`) folded in pre-existing foreign edits to `examples/nextorm.examples.mssql.adventureworks/AdventureWorksQueries.cs` (the #160 `AsCte`/`WithAlias` sibling work) — that file was in the D-01 foreign baseline and is no longer dirty because D-06 committed it; recorded, not hidden | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/d07-footprint.json
2026-10-14 23:35Z | DO | revision r=3 | iteration n=2/3 | **D-07 done — not a CHECK verdict.** Continuous reuse note: the brief's `rv3/verify.py boundary|coverage|footprint` paths were executed as the canonical rv1-catalog invocations C11/C12/C16; `rv3/verify.py` now exposes thin delegates for those literal paths (`footprint` delegate exercised exit 0). `plan_state` unchanged; no cycle closure, no ACT | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
2026-10-14 23:36Z | DO | revision r=3 | iteration n=2/3 | environment note: the acceptance benchmark and the example build need `NuGetAudit=false` / the local feed (NU1902/NU1903 advisory, unrelated to this task) — recorded, not a change exemption | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv1/perf-comparison.json
2026-10-14 23:52Z | PLAN | revision r=4 | iteration n=1/3 | r=4/rv=4 transcribed — material replan after the authoritative **r=3/n=2** CHECK failure (build/security/unsealed evidence; CHECK → PLAN); rv3 superseded as the current contract with history preserved; D-06/D-07 reopened (not superseded); D-08…D-13 active; P1 blocker `B232-AUDIT-DEPENDENCY` | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
2026-10-14 23:53Z | DO | revision r=4 | iteration n=1/3 | DO resumed at r=4 (n=1): rv4 evidence plumbing + D-08 packaging remediation + D-13 capture only (D-09/D-10/D-11/D-12 NOT started) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/
2026-10-14 23:54Z | DO | revision r=4 | iteration n=1/3 | rv4 evidence plumbing created: `contract.json`/`manifest.json` seeded with **41 required rows** (23 met / 18 open), `validate_inner_loop.py manifest` **exit 0**; `verify.py` case-driver skeleton (fail-closed, `--case`); `accounting-correction.json` supersedes the stale rv3 `attempt` field (CHECK ran at n=2, not n=1) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/manifest.json
2026-10-14 23:55Z | DO | revision r=4 | iteration n=1/3 | D-08 packaging remediation: AdventureWorks csproj `nextorm`/`nextorm.sqlserver` PackageReferences → ProjectReferences to `src/nextorm.core`/`src/nextorm.sqlserver`; docs note the explicit current-checkout exception in `examples/README.md`, `examples/VERIFICATION.md` and the AdventureWorks `README.md` | examples/nextorm.examples.mssql.adventureworks/nextorm.examples.mssql.adventureworks.csproj
2026-10-14 23:55Z | DO | revision r=4 | iteration n=1/3 | D-08 command evidence: `dotnet restore nextorm.slnx` **exit 1** (advisory-only), `dotnet build nextorm.slnx -c Debug` **exit 1** (26 errors, all NU1902/NU1903), literal C13 `dotnet build examples/nextorm.examples.mssql.adventureworks/nextorm.examples.mssql.adventureworks.csproj -c Debug` **exit 1** (4 errors: 2 for the example, 2 for nextorm.sqlserver — all NU1902/NU1903; **no CS/packaging error**). D-08 packaging route itself adds no error; the remaining failure is D-13's advisory | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/packaging/packaging.json
2026-10-14 23:56Z | DO | revision r=4 | iteration n=1/3 | D-13 capture: ordinary `dotnet restore nextorm.slnx` exit 1 — `System.Security.Cryptography.Pkcs` 9.0.11 transitive via `Microsoft.Data.SqlClient`: **NU1902 GHSA-2fmv-rv2c-59x7 (moderate)** + **NU1903 GHSA-846f-f764-76mx (high)**, 13 project paths each; no local feed, no version override, NuGetAudit default | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/security/advisories.json
2026-10-14 23:57Z | DO | revision r=4 | iteration n=1/3 | D-13 duplicate search (gh permission **granted**, exit 0): `gh issue list --state all --search NU1902` / `NU1903` / `System.Security.Cryptography.Pkcs` / `Microsoft.Data.SqlClient` = **no results**; `gh search issues --author @me` = 30 issues, none about the advisory → **no duplicate**; no issue created (blind creation forbidden) | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/security/gh-search/
2026-10-14 23:58Z | DO | revision r=4 | iteration n=1/3 | **BLOCKER `B232-AUDIT-DEPENDENCY` (P1, D-13):** the ordinary restore/build is red on the transitive `System.Security.Cryptography.Pkcs` 9.0.11 advisory promoted to errors; remedy ownership must be tracked by a separate issue (not created here); escalate if the required external authority/resource is unavailable rather than masking with `NuGetAudit=false`. D-09/D-10/D-11/D-12 not started; cycle not closed | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/security/advisories.json
2026-10-15 00:02Z | DO | revision r=4 | iteration n=1/3 | B232-AUDIT-DEPENDENCY tracking issue opened/linked: duplicate search re-verified (four keywords + `--author @me`, open+closed) = no match, exit 0; issue **#236** created (milestone 1.0.9-rc3, `Refs #232`) with exact commands/exits (`dotnet restore nextorm.slnx` = 1; `dotnet build nextorm.slnx -c Debug` = 1, 26 errors; example build = 1, 4 errors — all NU1902/NU1903), advisories NU1902 GHSA-2fmv-rv2c-59x7 (moderate) / NU1903 GHSA-846f-f764-76mx (high) on `Microsoft.Data.SqlClient 6.1.7 → System.Security.Cryptography.Pkcs 9.0.11` (`Directory.Packages.props:17`, `src/nextorm.sqlserver/nextorm.sqlserver.csproj:8`), `TreatWarningsAsErrors` (`Directory.Build.props:40`) + CI (`.github/workflows/dotnet.yml:37,39`); no fix claimed | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/security/issue.json
2026-10-15 00:07Z | DO | revision r=4 | iteration n=1/3 | **D-11 escalate decision (binding, route 2+3):** foreign #160 hunks in `001f9c9c` are never removed/reverted and history is never rewritten; `001f9c9c` recorded as a **mixed** commit; **AC-12 stays blocked**: `AC-12 open: foreign #160 hunks in 001f9c9c have no identified owner and no explicit retention authorization from the collection owner`; the parent collection mandate is **not** authorization (it forbade touching `examples/**` beyond the example footprint; disclosure ≠ authorization); verbatim request to the collection owner recorded (pending answer); D-09/D-10/D-12 unblocked and continue | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
2026-10-15 00:07Z | DO | revision r=4 | iteration n=1/3 | D-11 ownership evidence written: `commit 001f9c9c`, file `examples/nextorm.examples.mssql.adventureworks/AdventureWorksQueries.cs`, `classification: mixed`; hunk split **2 T232 / 11 foreign** (`@@ -1,6 +1,8` … `@@ -519,3 +534,16`), CRLF throughout; foreign = 30 discrete pre-existing #160 edit sites (8 `AsCte`, 1 `WithAlias(Alias.Metrics)`, 3 `Alias.*`, 12 alias-member reads, 2 `CustomerMetricRow`, 2 usings, 2 `#160` comments) over 117 added / 97 removed lines; `sibling_owner: "unidentified"` (not guessed); `retention_authorization.status: pending` | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/ownership.json
2026-10-15 00:07Z | DO | revision r=4 | iteration n=1/3 | D-11 hunk-disjointness + reconstruction proof: `python3 artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/ownership/split_hunks.py` **exit 0** — old/new hunk ranges disjoint; `blob(001f9c9c^)` (`3fe2ddd0`, 23466 B) + `t232.patch` + `foreign.patch` == `blob(001f9c9c)` (`a54a2c5f`, 24058 B) in **both** orders (and the full patch), sha256 `26409ccd…a07155d` | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/ownership/reconstruction.json
2026-10-15 00:07Z | DO | revision r=4 | iteration n=1/3 | #160 facts recorded (read-only, no new issue): issue **#160** `CLOSED`, milestone **1.0.9-rc2** (#20), author `AlexeyShirshov`, no assignees/labels; **no comment posted — pending the owner's explicit consent**; `sibling_owner` remains unidentified | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/ownership.json
2026-10-15 06:58Z | CHECK | revision r=4 | iteration n=1/3 | r=4 CHECK FAILED (authoritative): V18 assertion strength, V23 ambiguity guard, EC232-RV3-SEAL na disposition, rv4 fulfillment provenance/counters hash-validity, handoff strict_clean, missing rv4 D-09 report, V05 mapping, contradictory rv4 artifacts; two inherited identifier-safety findings dispositioned out of scope | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv4/
2026-10-15 06:59Z | ESCALATE | revision r=4 | iteration n=1/3 | binding decision: new revision r=5 (product scope unchanged; contract changed only in AC-12 and the EC232-RV3-SEAL obligation); two inherited security High findings → follow-up issue in 1.0.9-rc3, not fixed in T232; no allowlist extension | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
2026-10-15 07:00Z | PLAN | revision r=5 | iteration n=1/3 | r=5/rv=5 transcribed: corrective revision for assertion/binding/evidence closure; D232-RV5-01..09 active; rv5 delta rows EC232-RV5-* created; r=4 history preserved verbatim | docs/specs/status/D232-sqlserver-pivot-unpivot-1.md
2026-10-15 08:38Z | DO | revision r=5 | iteration n=2/3 | corrective (r=5 CHECK FAIL → DO, back to D232-RV5-08/09 — evidence-only, no product change): `tools/rv5_evidence.py` repaired so `hashes` inventories the complete transitive closure of every manifest/counters reference (raw execution logs + every `logs/*.cmd.json`/`.exit`, the tool + its selftests, the referenced `$B` files; 0 required references absent) and the ordered chain is payload/reports → `hash-ledger.json` (payload_digest) → `seal.json` (`ledger_sha256` + `payload_digest`, ledger→seal binding) → `hash-verification.json` (`seal_binding` verified); `handoff.json` now carries the committed-tested proof (per-path worktree sha256 == HEAD blob sha256, no diff vs HEAD) instead of asserting "path exists in HEAD"; selftests 8/8 (new guard `tampered-ledger-digest`); the superseded r=5 ledger is recorded in `rebaseline_history`, never hidden | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv5/hash-ledger.json
2026-10-15 08:44Z | DO | revision r=5 | iteration n=2/3 | rv5 evidence chain finalized after the **r=5 CHECK FAIL on evidence only** (corrective reason: rv5 hash-ledger completeness + the ledger→seal binding + the committed-tested handoff proof; **no product defect was found**, product scope unchanged): the corrective evidence is landed by three authorized `#232` commits — `2bbf0fdc` (V23 ambiguity guard, V18 SQL Server execution, rv5 evidence chain), `521e8f80` (evidence refresh after the guard commit) and the finalization `#232` commit (this journal line + the tail-edited tool + the refreshed evidence artifacts); the tool tail edit adds a `ledger_written` gate so a plain `hashes --verify` never rewrites the ledger — verification cannot invalidate the seal→ledger binding it just checked — and aligns the ledger `note`/`unaccounted_rule` with it; the full ordered chain re-run after the edit exited 0 each: `hashes --verify --rebaseline` → `seal --historical-root $B` → `hashes --verify` (`ledger_written:false`, `seal.ledger_sha256` == the current ledger sha256, no self-invalidation), then `selftest` 8/8, `handoff` (`satisfied=true`, committed-tested proof, `all_paths_committed_and_clean=true`), `fulfillment` (rv4+rv5) and `python3 scripts/validate_inner_loop.py manifest` (54 `met` / 0 `open`); ledger 449 entries; seal→ledger binding `60f1b669…`; tree `521e8f80` before this finalization commit | artifacts/pdca/D232-sqlserver-pivot-unpivot/rv5/hash-ledger.json
```
