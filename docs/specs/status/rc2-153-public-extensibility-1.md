# PDCA status — #153 public-extensibility decision (1.0.9-rc2)

- task_id: D153
- issue: https://github.com/AlexeyShirshov/nextorm/issues/153
- selected_variant: pdca-dotnet
- collection: collection-1.0.9-rc2
- cycle: N=1
- plan revision: r=1
- iteration: n=2/3 (r=1; docs-only loop-back fix)
- plan_state: ready
- phase: DO n=2 docs fix + evidence completion done; awaiting CHECK r=1 n=2
- evidence-contract revision: rv=1
- created: 2026-10-07

## Goal

Make the supported external extension boundary of `PostgresDialect`/`ClickHouseDialect`
explicit (it is a public compatibility commitment, not a test detail) and prove that ordinary
consumers — not friend assemblies — can use it: subclass the dialects, implement the public
`IExtremeRowRenderer`, install it through the subclass, force the portable strategy with `null`,
all without any `InternalsVisibleTo` grant, and record the decision in the API register.

## Scope

In scope: public inheritance contract of the two dialects; existing constructors, `Instance`,
overridable public hooks; XML docs; public EN/RU docs; a dedicated non-friend consumer test
assembly; a dated compatibility decision entry in the API review register.

Out of scope: other providers; new public APIs; exposing built-in renderers (stay internal);
DTO constructors (reserved for #154); payload-laziness (belongs to #155); general API-snapshot
infrastructure; performance promises; historical-audit cleanup; commits/pushes/merges.

## Acceptance criteria

| ID | Criterion | Proof / negative case |
|---|---|---|
| AC-01 | External consumers inherit both dialects and use existing constructors without IVT. | Separate non-friend assembly builds+tests; sealing a dialect or losing a constructor breaks that build; evidence from an IVT-granted assembly is rejected. |
| AC-02 | Consumers implement `IExtremeRowRenderer`, install via subclass, exercise accept+decline. | PG and CH: eligible queries reach the custom impl; `CanRender=false` produces portable SQL and `Render` is NOT called. |
| AC-03 | Overriding `ExtremeRowRenderer` with `null` selects portable strategy for both providers. | Execute public query preparation/SQL-generation and assert portable output; property-read-only check rejected. |
| AC-04 | EN/RU pages + XML docs describe the contract consistently. | All designated targets state subclassing, overriding, `null → portable`, unchanged defaults, internal-renderer boundary; no "test-only extensibility", no links to `docs/specs/**`. |
| AC-05 | Register records a public compatibility decision. | Dated row identifies #153, deliberate nonsealing, supported hooks, breaking implications of removing inheritability; not framed as a testing workaround. |
| AC-06 | Default behavior and production public surface unchanged. | Existing PG/CH native regression suites pass (incl. container evidence); production diff is XML-comment/docs only — no declaration, ctor, singleton, renderer-selection or laziness change. |

## Variant matrix (execution path; every row closed)

| Variant / execution path | Disposition |
|---|---|
| PG subclass; parameterless and `Version?` ctors incl. `null` | New consumer compile/runtime tests |
| CH subclass; existing ctor | New consumer compile/runtime tests |
| PG custom renderer accepts eligible request | New consumer test (public delegation) |
| CH custom renderer accepts eligible request | New consumer test |
| PG/CH custom renderer declines (`CanRender=false`) | New consumer test: portable result; `Render` must not be called |
| PG/CH subclass returns `null` | New consumer test: execute and verify portable strategy |
| Public interface implemented outside production assemblies | New consumer implementation, both providers |
| Consumer without IVT to core or either provider | New metadata guard + build |
| Default `Instance`, native-eligible input | Reuse existing provider SQL-generation + native integration regressions |
| Default renderer, unsupported input shapes | Reuse established guard/fallback tests (exact symbols after DO gather) |
| Existing nullable/value/reference payload and tie cases | Reuse existing native regressions; no new payload/laziness assertions |
| Direct DTO construction | Deferred → #154 |
| Renderer laziness / exact callback counts | Deferred → #155 (do not assert timing) |

## Tasks & footprint

- P:153-01 (residual pre-DO gather): test-project conventions + solution/discovery registration;
  verified #153 URL; Roslyn-resolved public query setup and existing native/portable regression
  locations; integration commands/recovery. (Normative evidence-contract + nextorm overlay
  sections already gathered — see below.)
- D:153-01 Record the compatibility decision: `docs/specs/design/API-NAMING-REVIEW.md` — append a
  new dated audit subsection near the current final audit section, using the existing schema
  `| # | Ур. | Место | Оценка | Рекомендация |`. Record public inheritability, existing hooks,
  ctor/default preservation, internal-renderer boundary, verified #153 URL. Do not recalc the
  undocumented-type baseline or rewrite Appendix A.
- D:153-02 Update public + XML docs:
  `docs/advanced/select-where-extrema-native.md:110-114`,
  `docs/ru/advanced/select-where-extrema-native.md:110-114`,
  `docs/advanced/api-reference.md:109`, `docs/ru/advanced/api-reference.md:109`,
  XML comments in `src/nextorm.postgres/PostgresDialect.cs:10,16,24,33,394`,
  `src/nextorm.clickhouse/ClickHouseDialect.cs:12,15,23,278`.
  Matching EN/RU subclass examples returning `null`; explain installing a public-interface impl;
  built-in renderers remain internal and undocumented internals are not a stability commitment.
  Guard (no planned edit): `DialectCapabilities.cs:372-399`, `SqlDialectBase.cs:240`,
  `ISqlDialect.cs:1284` — expand only if a concrete contradiction blocks AC-04.
- D:153-03 Add persistent external-consumer verification (planned new files):
  `tests/nextorm.publicextensibility.tests/nextorm.publicextensibility.tests.csproj`,
  `tests/nextorm.publicextensibility.tests/ExternalDialectExtensibilityTests.cs`; reference core
  + both providers; CPM (no version-bearing refs); register in the discovered `*.sln`/`*.slnx`;
  include an assembly-metadata guard proving no IVT grant from the three referenced production
  assemblies; add no IVT declarations.
- D:153-04 Validate, collect evidence, hand off to CHECK (commands below).

Footprint uncertainty: exact register insertion point (end-of-file structure to confirm before
insert); exact existing regression symbols and query setup (DO gather); whether a new test project
registration is needed or an equivalent discovery mechanism exists; whether `Version?` ctor path
has an eligible native case. Integration endpoint may need the documented socket recovery.

Deferred (with trigger): `code-smells-review.md:3136` / `solid-review.md:966` historical sealed
descriptions → trigger: future refresh or being presented as current API guidance. DTO
accessibility → trigger #154. Laziness → trigger #155.

## Predecessor-result requirements

- #144 landed; nonsealed declarations confirmed at `PostgresDialect.cs:10` and
  `ClickHouseDialect.cs:12`. No further #144 implementation needed.
- If any row's test would assert laziness/callback timing, it depends on #155 — keep such
  assertions out.

## Assumptions / prerequisites

- Current public hooks and constructors remain available; no new IVT will be added for this consumer.
- Code is already unsealed → production change is XML-comment only.
- These are verified requirements, not permissions to weaken acceptance.

## Execution mode

Sequential units in one tree (shared compatibility contract; worktree adds overhead without
useful isolation). Capture the starting working-tree state so #155/pre-existing changes are not
overwritten or attributed to this task.

## Test strategy

New assembly = separate compiled consumer in the same solution, not a friend assembly; public
query construction + SQL generation; no new DB-dependent tests required for the consumer itself.
Integration decision: consumer verification is SQL-generation-level, but repository rules make
container-backed regression evidence mandatory → reuse existing integration tests; skipped PG/CH
cases are not passing. Coverage: retain 85% line / 75% branch (main hard-fail, other branches
warn); no new production executable lines. Branch/mutation: no production branch/mutation expansion
for comment-only production changes; strategy-selection variants are tested behaviorally. Failure
probes: sealing a dialect / losing a ctor / removing interface access breaks the consumer build;
ignoring `null` or `CanRender=false` fails portable assertions; calling `Render` after rejection
fails immediately. Do not build general mutation infrastructure.

## Documentation plan

Four designated public pages (EN+RU) + two provider XML-doc targets + one appended register entry,
as in D:153-01/02. TOCs unchanged (no page move/add). Public pages must not link to `docs/specs/**`.

## Performance / reconnaissance / unit-mode decisions

- Performance measurement: NOT needed — at `PostgresDialect.cs:394` / `ClickHouseDialect.cs:278`
  renderer selection is unchanged; XML/doc edits have no per-row effect; consumer probes run only
  in tests. Non-N/A argument: no query-path/plan-cache/executor/materialization change; nextorm
  overlay explicitly exempts documentation-only cycles. If implementation proposes executable
  production changes → return to PLAN instead of extending scope.
- Reconnaissance: not needed — the solution is binding, declarations/hooks verified, delegation
  uses the existing public interface.
- Unit execution mode: one tree, sequential units.

## Evidence contract — rv=1

Conforms to the global `pdca-dotnet` "Versioned evidence contract" and "CHECK completeness gate"
sections (gathered; nextorm overlay §"Обязательный evidence contract"). Every row below is
unconditional for this task; missing tooling/infrastructure/reports does NOT make a row
inapplicable. Evidence dir: `TestResults/rc2-153-1/`. Run shell pipelines with `set -o pipefail`;
create the dir first.

### Calls

| Call | Command | Required result / artifact |
|---|---|---|
| C-01 | `dotnet build tests/nextorm.publicextensibility.tests/nextorm.publicextensibility.tests.csproj -c Debug` | exit 0; `consumer-build.log`; no warnings under project policy |
| C-02 | `dotnet test tests/nextorm.publicextensibility.tests -c Debug` | exit 0; `consumer-tests.log`; all scenarios executed, none skipped |
| C-03 | `dotnet test tests/nextorm.postgres.tests -c Debug` + `dotnet test tests/nextorm.clickhouse.tests -c Debug` | both exit 0; provider logs; executed, no unexplained skips |
| C-04 | `dotnet docfx docs/docfx.json` | exit 0; `docfx.log`; no new task-related unresolved refs/warnings |
| C-05 | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` | exit 0; `integration.log`; PG/CH native regressions actually execute and pass |
| C-06 | `git diff --check` | exit 0; `diff-check.log` |
| C-07 | `git diff HEAD -- src docs tests '*.sln' '*.slnx'` | exit 0; `task-diff.patch`; compared to captured start state; untracked consumer files reviewed |
| C-08 | `git grep -n -E 'docs/specs/\|(\.\./)+specs/' -- docs/advanced/select-where-extrema-native.md docs/ru/advanced/select-where-extrema-native.md docs/advanced/api-reference.md docs/ru/advanced/api-reference.md` | exit 1, no matches; `public-spec-links.log` |
| C-09 | Invoke `check`: "Review the designated EN/RU pages, provider XML comments, API decision entry, consumer test source/results, baseline-relative diff against AC-01–AC-06. Record each required statement/guard as PASS/FAIL with actual file:line or log refs." | `check-findings.md`; explicit per-row verdicts |

Before C-05 load the repository integration skill and follow its socket/recovery instructions.

### Rows

| Row / req / rv | Scenarios & applicability | Evidence kinds & sources | Exact calls & result requirements | Artifacts | Owner |
|---|---|---|---|---|---|
| EV-01 / AC-01 / 1 | Always; both providers, existing ctors, separate non-friend assembly | compiler; executable IVT guard; reviewed project/source | C-01,C-02,C-09; build success, guard passes, no IVT added | consumer project/source, logs, CHECK findings | Consumer DO; CHECK verifies |
| EV-02 / AC-02 / 1 | Always; PG/CH × accepts/declines | runtime assertions via public query path | C-02,C-09; callbacks exercised, native path, rejection portable & no `Render` | consumer test log + scenario map | Consumer DO; CHECK verifies |
| EV-03 / AC-03 / 1 | Always; PG/CH × renderer `null` | SQL-generation assertions (not property-only) | C-02,C-09; portable output both | consumer test log + scenario map | Consumer DO; CHECK verifies |
| EV-04 / AC-04 / 1 | Always; 4 public pages + 2 XML targets | doc content, EN/RU consistency, DocFX diagnostics, link guard | C-04,C-06,C-08,C-09; statements present; no prohibited links/new diagnostics | docs diff, DocFX/link logs, CHECK checklist | Docs DO; CHECK verifies |
| EV-05 / AC-05 / 1 | Always; dated register decision | reviewed decision row + verified issue URL | C-07,C-09; public compat commitment recorded, not test-only | register diff + CHECK findings | Register DO; CHECK verifies |
| EV-06 / AC-06 / 1 | Always; default native behavior + unchanged production declarations | existing unit/integration results; baseline-relative diff | C-03,C-05,C-07,C-09; regressions pass w/o PG/CH skips; production executable code unchanged | provider/integration logs, diff, CHECK findings | Verification DO; CHECK owns final |

CHECK re-gather budget: at most **2** targeted requests, owned by `check`; no unrestricted rerun.
Revision rule: retain row IDs/obligations; a justified revision states `rv=2 supersedes rv=1`,
preserves row IDs, adds IDs for new variants. Clarifying actual test symbols/attaching reports
does not itself revise the plan.

## Priority matrix

All acceptance rows P1 by construction (explicit external-consumer/exec-path invariants EV-01–03;
public-doc/decision-record obligations EV-04–05; compatibility/unchanged-default EV-06). CHECK must
not downgrade. Historical-audit cleanup is deferred, not a substitute for a P1 row.

## Risks / return classification

High confidence: agreed boundary, nonsealed declarations, public interface, internal renderers, doc
targets. Remaining evidence gaps: test-project wiring, exact regression mappings, integration
availability — explicit gate-1 prerequisites, not assumed successes.
DO returns: missing project wiring → additive prerequisite (D stays active blocked); missing/
ambiguous evidence → targeted scout then CHECK bounded re-gather; persistent low confidence →
orchestrator `escalate` trigger 5; proven outside-cycle blocker → `escalate`. "Implementation
complete" is provisional and never marks a D done without CHECK.

## Progress log

- 2026-10-07: PLAN (r=1, rv=1) persisted by coder; plan_state=ready; PLAN→DO boundary; DO not started.
- 2026-10-07T19:11Z | DO | revision r=1 | iteration n=1/3 | DO started (units D:153-01..04) | docs/specs/status/rc2-153-public-extensibility-1.md, TestResults/rc2-153-1/
- 2026-10-07T19:15Z | DO | revision r=1 | iteration n=1/3 | D:153-01 register decision appended | docs/specs/design/API-NAMING-REVIEW.md
- 2026-10-07T19:15Z | DO | revision r=1 | iteration n=1/3 | D:153-02 EN+RU docs and provider XML comments updated | docs/advanced/select-where-extrema-native.md, docs/ru/advanced/select-where-extrema-native.md, docs/advanced/api-reference.md, docs/ru/advanced/api-reference.md, src/nextorm.postgres/PostgresDialect.cs, src/nextorm.clickhouse/ClickHouseDialect.cs
- 2026-10-07T19:16Z | DO | revision r=1 | iteration n=1/3 | D:153-03 non-friend consumer project created and registered | tests/nextorm.publicextensibility.tests/, nextorm.slnx
- 2026-10-07T19:18Z | DO | revision r=1 | iteration n=1/3 | D:153-04 evidence collected; consumer 10/10, PG ExtremeRow 37/37, CH ExtremeRow 59/59, integration 51/51 (PG=16,CH=35, 0 skipped), docfx 0 errors, validator report exit 0 | TestResults/rc2-153-1/evidence.json
- 2026-10-07T14:31Z | CHECK | revision r=1 | iteration n=1/3 | CHECK r=1 n=1 FAIL (documentation defect; EV rows closed, evidence gaps) — api-reference.md:109 `CanRender` wording; extrema-native renderer sample incoherence (undeclared MyRenderer.Instance, constant-false CanRender, Render returns request.SourceSql); route DO n=2 | TestResults/rc2-153-1/check-findings.md
- 2026-10-07T14:32Z | DO | revision r=1 | iteration n=2/3 | DO n=2 fix started (docs-only loop-back; no replan, r stays 1) | docs/advanced/api-reference.md, docs/ru/advanced/api-reference.md, docs/advanced/select-where-extrema-native.md, docs/ru/advanced/select-where-extrema-native.md
- 2026-10-07T14:33Z | DO | revision r=1 | iteration n=2/3 | DO n=2 fix completed — api-reference.md:109 CanRender wording corrected (null from override / false from CanRender); renderer sample made coherent (MyRenderer accepts global single-key form, CanRender true for that shape, Render returns a real winning-row source) and the `=> null` override example kept; EN/RU in sync; CRLF preserved | docs/advanced/api-reference.md, docs/ru/advanced/api-reference.md, docs/advanced/select-where-extrema-native.md, docs/ru/advanced/select-where-extrema-native.md
- 2026-10-07T14:33Z | DO | revision r=1 | iteration n=2/3 | evidence completion: docfx exit 0 (0 errors / 2 pre-existing warnings); git diff --check exit 0; link guard exit 1 no matches; task-diff.patch regenerated incl. untracked consumer files; PG integration 16/16 skipped 0 exit 0; CH integration 35/35 skipped 0 exit 0; scope-fix brief exit 0; evidence report exit 0 | TestResults/rc2-153-1/{docfx.log,diff-check.log,public-spec-links.log,task-diff.patch,integration-postgres.log,integration-clickhouse.log,scope-fix.json,validator-report.log,validator-report-exit.txt,check-findings.md}
- 2026-10-07T14:37Z | DO | revision r=1 | iteration n=2/3 | DO n=2 pointer-accuracy fix: N153-1 Место anchors updated to current post-XML-doc lines (PG 18,27,39,52,418; CH 20,26,37,297); stable SqlDialectBase.cs:240 / ISqlDialect.cs:1284 unchanged; N153-2/observations untouched | docs/specs/design/API-NAMING-REVIEW.md
- 2026-10-07T14:40Z | CHECK | revision r=1 | iteration n=2/3 | CHECK r=1 n=2 PASS (AC-01..AC-06 met; all EV rows closed) | TestResults/rc2-153-1/check-findings.md
- 2026-10-07T14:40Z | ACT | revision r=1 | iteration n=2/3 | ACT D153: CHECK passed, committing the D153 footprint to 1.0.9-rc2 (autocommit lane; no push) | git
- 2026-10-07T14:41Z | ACT | revision r=1 | iteration n=2/3 | D153 done; commit 8401ca51; issue #153 closed (gh exit 0) | TestResults/rc2-153-1/
