# Task T206 / #206 — Source generator CS0111 signature collision (alias joins)

- collection: `rc2-final`
- intent: own PLAN (COLLECTION TASK PLAN); phase P (no DO yet)
- selected_variant: `pdca-dotnet`
- cycle_id: N=1
- plan_revision: r=3
- attempt: n=1/3
- contract_rv: 3
- milestone: `1.0.9-rc2`
- overall_task_state: RESOLVED
- resolution: resolved-by-verification (2026-10-10; see `## RESOLUTION` below)
- baseline: `a60ecb84`
- plan_state: ready
- status file: `docs/specs/status/rc2-206-sourcegen-cs0111-alias-join-1.md`
- persisted: 2026-10-09

---
# T206 / #206 — PLAN
Collection `rc2-final`; variant `pdca-dotnet`; cycle N=1; r=1; n=1; plan_state=ready.
Baseline branch `1.0.9-rc2` @ `9a2a2871`; status `docs/specs/status/rc2-206-sourcegen-cs0111-alias-join-1.md`.
Planning deliverable only: persist this plan; do not start DO.

## Goal and acceptance criteria
Deliver an explicit, deterministic guard for unrepresentable alias-join overloads, not support for both conflicting projections.
- **R01 / P1:** Supported-syntax last-step collision fixture demonstrates CS0111 in reconstructed unguarded output, but no CS0111 in guarded output. Negative: zero generated candidates, unrelated compiler errors, or testing only the intermediate-step case cannot satisfy this.
- **R02 / P1:** Same-signature/different-body groups emit no arbitrarily selected overload; NORMGEN007 identifies every distinct affected invocation location and explains remediation. Negative: first-conflicting-body retention, silent drop, or order-dependent output fails.
- **R03 / P1:** Byte-identical duplicates emit one method; the already-closed intermediate `IsCte` case remains supported. Negative: NORMGEN007 for identical bodies, duplicate declarations, or lost intermediate methods fails.
- **R04 / P1:** Valid generated public signatures and existing frozen-surface/typed-CTE tests remain unchanged. Negative: updating frozen expectations to accommodate an accidental API change fails.
- **R05:** EN/RU joins guides describe NORMGEN007, its limitation, and a verified workaround. Negative: documenting only NORMGEN001–006, suggesting unsupported issue-body syntax, or claiming both conflicting projections now work fails.
- **R06 / P1:** Builds, required suites, evidence provenance, and repository hygiene pass. Negative: infra failure counted as red, skipped providers as passing, or missing artifacts fails.

## Minimal solution
Purpose: prevent ambiguous generated overload selection and give users an actionable failure.
Constraints: preserve valid public signatures, generator wiring, diagnostic ID, CRLF, warning-clean builds; no runtime/query-cache changes.
Optimum: aggregate existing signature groups before emission; emit one member for identical bodies, suppress the whole conflicting group, and diagnose its distinct source locations.

| Alternative | Benefit | Cost/risk | Decision |
|---|---|---|---|
| Put projection/JoinedType identity into params/receiver | Could support both shapes | Identity alone cannot distinguish overloads; inferability/API compatibility unproven | Deferred until approved API design + compiling spike |
| Strengthen NORMGEN007 guard and documentation | Small, deterministic, preserves valid surface | Both conflicting invocations remain unsupported | **Selected** |

Existing first-wins suppression is insufficient (encounter-order dependent).

## Unsaid requirements, assumptions, prerequisites
- Baseline already guards this class; **do not claim baseline `9a2a2871` emits CS0111**.
- The issue-body invocation is not a valid generator candidate. Preflight requests a bounded scout for an owner-valid last-step fixture with Alias-rooted final argument.
- Confirm existing signature identity, diagnostic severity/location behavior, compatible workaround.
- Obtain verified URLs for #206/#113/#159 from collection tracking, plus the applicable `pdca-dotnet` evidence-contract and `nextorm-pdca` overlay requirements.
- #159's accepted NORMGEN007 risk is context, not an unfinished prerequisite.
- If preflight contradicts the selected guard or adds a mandatory obligation, return to PLAN before DO.

## Concrete tasks
- **D1 — fix now:** `JoinAliasGeneratorDiagnosticTests.cs:93–178,240`: characterize a real last-step fixture; add GeneratorDriver red/green, group suppression, diagnostic-location/order, duplicate and invalid-shape regressions. Record baseline results before production edits.
- **D2 — fix now:** `JoinAliasGenerator.cs:683–787`: group by existing C# signature identity, distinguish complete rendered bodies, collapse identical bodies, suppress conflicting groups deterministically. `:110–117`: retain NORMGEN007 identity, make its explanation actionable.
- **D3 — fix now:** `docs/guide/02-joins.md:216` + `docs/ru/guide/02-joins.md:221`: add NORMGEN007 guidance + verified remediation. Update `AnalyzerReleases.Unshipped.md:14` only if inaccurate.
- **D4 — fix now:** run checks, collect provenance/artifacts, persist CHECK-ready evidence. Do not edit frozen signatures to make tests pass.
- **Deferred:** signature-disambiguating API (trigger: approved compatibility design); stale AGENTS source-generator Layout claim (trigger: next Layout maintenance).
- **Mode:** sequential D1→D2→D3→D4 in the current worktree. No worktrees/commits/merges/pushes.
- **Footprint:** generator, diagnostic tests, EN/RU joins guides, cycle status; analyzer release description conditional. Evidence under `artifacts/pdca/D206/rv1/`.
- Verification-only: `AliasGeneratedSurfaceTests.cs:137–150`, `TypedCteAliasTests.cs:234–279`.

## Design checklist, test strategy and closed variant matrix
Checklist: signature identity excludes return type/body and respects C# overload rules; stable diagnostic source locations; no first-wins semantics; stable output order; no mutable cross-run state; no new package/API; CRLF + warnings-as-errors.

| Variant | Closure | Observable result |
|---|---|---|
| Intermediate-step `IsCte` class | test | Valid compilation; no duplicate member or NORMGEN007 |
| Last-step JoinedType absent, two projections | test + guard | Reconstructed unguarded output has CS0111; guarded output has none and rejects the group |
| Byte-identical duplicate | test | Exactly one declaration; no collision diagnostic |
| Same signature, different bodies | test + guard | No member from group; diagnostics at distinct affected sites; order permutations equivalent |
| Existing frozen surface | test | Existing assertions unchanged and passing |
| Unresolvable generation shape | test + guard | Appropriate existing diagnostic; no malformed member |

P1 rows R01–R04/R06. Coverage context: CI line ≥85%, branch ≥75%; generator excluded from coverage settings — report that limitation, do not claim measured generator coverage; map changed branches to the tests above.

## Docs, performance, reconnaissance, risks
Docs: both joins guides; no generated API pages or unrelated renumbering.
Performance measurement: **no runtime/cached-path benchmark** — `JoinAliasGenerator.cs:683–787` runs at build time; keep bounded grouping; revisit if asymptotic behavior worsens.
Reconnaissance: bounded scout for fixture/contract/workaround facts. Pass condition: two individually valid supported chains yield the same final signature and different bodies, and their combined guarded behavior is observed.
Risks: suppressing a whole group may add follow-on missing-overload errors; document NORMGEN007 as primary cause. Confidence high in guard strategy; medium in precise fixture until preflight.

## Exact execution calls
C1 `dotnet test tests/nextorm.alias.tests -c Debug --filter "FullyQualifiedName~JoinAliasGeneratorDiagnosticTests"`; C2 `dotnet test tests/nextorm.alias.tests -c Debug`; C3 `dotnet build nextorm.slnx -c Debug`; C4 `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet-coverage collect "dotnet test nextorm.slnx -c Debug" -s coverage.settings.xml -f cobertura -o artifacts/pdca/D206/rv1/coverage.cobertura.xml`; C5 reportgenerator; C6 `DOCKER_HOST=... dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`; C7 `dotnet docfx docs/docfx.json`; C8 `git diff --check`. Load the integration skill before C4/C6. Record SQLite/PG/SQL Server/MySQL/ClickHouse individually.

## Versioned evidence contract — rv1
Pin before DO in this status and `artifacts/pdca/D206/rv1/manifest.json`. Rows: E00/R01,R05,R06 prerequisites (scout); E01/R01,R02 red characterization (baseline reconstructed CS0111); E02/R01–R04 green compiler/diagnostic tests; E03/R03,R04,R06 frozen surface + build; E04/R06 solution tests + coverage; E05/R06 real provider integration; E06/R05,R06 docs + bilingual review; E07/R04,R06 diff/line-endings/seal. CHECK owns at most 2 targeted re-gather rounds.

## Handoff and refs
Task=T206/#206; variant=pdca-dotnet; plan_state=ready; r=1; baseline=9a2a2871; footprint=generator/diagnostic-tests/EN+RU-joins/status/evidence; uncertainty=fixture, workaround and contract preflight; predecessors=#113 wiring, #159 risk context; prerequisites=E00, .NET 10/tools, integration skill and live containers.
Refs: #206 owner clarification; #113; #159; `docs/specs/design/join-alias-variant-matrix.md:76–84`; `docs/specs/status/rc2-159-cte-direct-joins-1.md`.


---

## DO - attempt n=1 (STOP: blocker, not a replan)

**Durable state:** Current cycle **N=1**; Plan revision **r=1** (unchanged - STOP, no replan); Attempt **n=1/3**; event phase **DO**; `tier=cheap`; HEAD `e37f6c7a`.
**Defect history:** new key `t206-unreachable-normgen007` -> observed r1 n1, **0 applied fixes**, evidence `artifacts/pdca/D206/rv1/{BLOCKER.md,D1-red.log,probe.log,D1D2-evidence.json}`, result **blocked (STOP), not resolved**. No prior T206 defect carried.

| UTC time | phase | revision | iteration | event | evidence pointer |
|---|---|---|---|---|---|
| 2026-10-09T18:37Z | DO | r1 | n1/3 | **STOP (blocker):** the mandated last-step-`JoinedType` different-body collision fixture is **not constructible** - `RenderExtension` (`JoinAliasGenerator.cs:721-787`) emits the body as a pure function of the emitted signature and replaces the last-step `JoinedType` with `TJoin` (`:747-751`), so NORMGEN007 (`:692-697`) is unreachable dead code. Closest fixture emits **one byte-identical** method with 0 NORMGEN007 and 0 compiler errors; the required location test is red with **0** diagnostics (not `Location.None`). Preflight contradicts the selected guard, so per the plan prerequisites this returns to PLAN before further DO. Tree restored to HEAD; **no production/test edits**. | `artifacts/pdca/D206/rv1/BLOCKER.md`; `D1-red.log` (exit 2, 11/12); `probe.log` (exit 2); `D1D2-evidence.json` (inner-loop report validator exit 2); scope brief exit 0 |

**Replan options (for planner):** (1) make the last-step `JoinedType` participate in the emitted identity - the plan's **Deferred** signature-disambiguating alternative (needs approved compat design + compiling spike; changes generated public surface); (2) keep NORMGEN007 defensive and move R01/R02 to a synthetic `AppendExtensions`/`ChainModel` test seam (internal test-only exposure, no public API change); (3) narrow T206 to R03 (byte-identical collapse + deterministic order-independent behaviour) and drop the unreachable R01/R02 fixture.

---

## P206.close — terminal incomplete (2026-10-09)

- status: **terminal incomplete**; plan_state: **terminal**; cycle **N=1**, plan revision **r=1** closed without DO completion; no `r=2`.
- Reason: "Required NORMGEN007 conflict is unreachable because emitted bodies are determined by emitted signatures (`JoinAliasGenerator.cs:721-787`, esp. `:747-751`); the collision branch `:692-697` cannot fire; the original acceptance remains unmet."
- Measured facts: probe fixture (same base, same `Alias.X`, last steps `Person` vs `Other`) emits one byte-identical `Join` method, 0 generator diagnostics, 0 compiler errors; red characterization exit 2, 11/12 alias diagnostic tests passed, 0 NORMGEN007 found.
- Acceptance disposition (all IDs preserved): **R01 unmet**; **R02 unmet** (fixture unsatisfiable); **R03 retained** (probe supports identical-duplicate collapse; intermediate-`IsCte` obligation not established by this attempt); **R04 retained**; **R05 unmet** (do not publish a purported reachable NORMGEN007 scenario or unverified workaround); **R06 retained**. None silently deferred/reclassified/passed.
- No product/test paths broken by this attempt; **D1/D2 recorded unfinished**; no fictitious supersession.
- Preserved evidence: `artifacts/pdca/D206/rv1/{BLOCKER.md,D1-red.log,probe.log,D1D2-evidence.json,scope.json}`.
- Lane released to **T208 (#208)**.

---

## PLAN r=2 (ready, 2026-10-10)

**Durable state:** Current cycle **N=1**; Plan revision **r=2**; Attempt **n=1/3**; `plan_state=ready`; `contract_rv=2`; milestone `1.0.9-rc2`; baseline `a60ecb84`.
**Defect history:** key `t206-unreachable-normgen007` -> observed r1 n1 + r2 n1, **0 applied fixes** (no generator change; the gap is retained, not fixed), evidence `artifacts/pdca/D206/rv1/*` and `artifacts/pdca/D206/rv2/*`, result **GAP-OPEN (not resolved)**.

### Goal / minimum solution
Verify supported duplicate collapse + deterministic generation + intermediate `IsCte`; document defensive NORMGEN007 accurately; **NO generator/public-signature redesign**.
- Chosen = **option-3 reproducible work + option-4 explicit GAP**: keep the supported, provable behaviour (duplicate collapse, determinism, intermediate `IsCte`) as candidate-MET tests/docs, and retain R01/R02 as an explicit open gap with a reopen trigger.
- **Rejected option 1** (make the last-step `JoinedType` participate in the emitted identity): changes the generated public surface, violates R04.
- **Rejected option 2** (synthetic `AppendExtensions`/`ChainModel` seam): a synthetic seam cannot prove a supported-syntax collision.
- **Rejected option 3-alone** (narrow to R03 only): silently dropping R01/R02 weakens acceptance; the gap must stay visible.

### Acceptance criteria R01..R07
- **R01 / P1 — GAP-OPEN.** A supported-syntax last-step collision fixture must demonstrate CS0111 in reconstructed unguarded output and no CS0111 in guarded output. Status: **unsatisfiable on the current generator**, retained as an open gap. Negative: zero generated candidates, unrelated compiler errors, or testing only the intermediate-step case cannot satisfy this.
- **R02 / P1 — GAP-OPEN.** Same-signature/different-body groups must emit no arbitrarily selected overload and NORMGEN007 must identify every distinct affected invocation location with remediation. Status: **branch is structurally unreachable**, retained as an open gap. Negative: first-conflicting-body retention, silent drop, or order-dependent output fails.
- **R03 / P1 — candidate-MET.** Byte-identical duplicates emit exactly one `Join<TJoin>` method, **0 NORMGEN007**, **0 compiler errors**; the intermediate `IsCte` case passes. Negative: NORMGEN007 for identical bodies, duplicate declarations, or lost intermediate methods fails.
- **R04 / P1 — candidate-MET.** Valid generated public signatures and the existing frozen-surface/typed-CTE tests remain unchanged; alias suite **103 passed**. Negative: updating frozen expectations to accommodate an accidental API change fails.
- **R05 — OPEN.** EN/RU joins guides must describe NORMGEN007 accurately, including that it is defensive and that diverging last-step projections are not independently supported. Negative: documenting only NORMGEN001–006, suggesting unsupported issue-body syntax, promising an unverified workaround, or claiming both conflicting projections now work fails.
- **R06 / P1 — OPEN.** Builds, required suites, evidence provenance and repository hygiene pass. Negative: infra failure counted as red, skipped providers as passing, or missing artifacts fails.
- **R07 / P1 — candidate-MET.** Forward, reversed and repeated generation are byte-identical and non-empty (`forward == reversed == repeat`). Negative: order-dependent or empty output fails.

### Tasks
- **D206-PREFLIGHT** — done.
- **D206-RECON** — done; **+3 retained tests** in `tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs`.
- **D206-TEST** — keep the existing fixtures/`RunSource`; retain the frozen surface assertions as-is.
- **D206-DOC** — EN `docs/guide/02-joins.md` + RU `docs/ru/guide/02-joins.md` (defensive NORMGEN007 wording).
- **D206-EVIDENCE** — rv2 manifest + logs under `artifacts/pdca/D206/rv2/`.
- **D206-GAP** — retain R01/R02 in #206 under the same milestone `1.0.9-rc2`; reopen trigger = a committed supported-syntax reproducer **or** an approved design that invalidates the purity proof.

### Test strategy and variant matrix
| Variant | Disposition |
|---|---|
| Duplicate + last-step projection | TEST |
| Reversed / repeat | TEST |
| Intermediate `IsCte` | TEST |
| Different-body, same-signature collision | GAP |
| Reference/value/nullable/default forms | GAP until fixture inventory |
| Determinism / incrementality + frozen / typed-CTE | TEST |
| SQLite end-to-end | alias-suite TEST |
| Container providers | required integration TEST |

### Docs plan
EN `docs/guide/02-joins.md` + RU `docs/ru/guide/02-joins.md`; no generated pages; no links into `docs/specs`.

### Perf decision
**No benchmark** — the generator algorithm is unchanged and generation happens at build time (`JoinAliasGenerator.cs:145-157,763`), not per query or per row.

### Unit mode
Sequential, existing tree, no branches/worktrees, no commits.

### Command registry C1..C9
| ID | Command | Exit |
|---|---|---|
| C1 | `dotnet test tests/nextorm.alias.tests -c Debug --filter "FullyQualifiedName~JoinAliasGeneratorDiagnosticTests"` | observed 0 (21 passed) |
| C2 | `dotnet test tests/nextorm.alias.tests -c Debug` | observed 0 (103 passed) |
| C3 | `dotnet build nextorm.slnx -c Debug` | observed 0 |
| C4 | `DOCKER_HOST=... dotnet-coverage collect "dotnet test nextorm.slnx -c Debug" -s coverage.settings.xml -f cobertura -o artifacts/pdca/D206/rv2/coverage.cobertura.xml` | planned, required 0 |
| C5 | reportgenerator | planned, required 0 |
| C6 | `DOCKER_HOST=... dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` | planned, required 0 |
| C7 | `dotnet docfx docs/docfx.json` | planned, required 0 |
| C8 | `git diff --check` | planned, required 0 |
| C9 | `python validate_inner_loop.py manifest` | planned, required 0 |

### rv2 required_rows
- **E206-01 / R01** — OPEN-GAP.
- **E206-02 / R02** — OPEN-GAP.
- **E206-03 / R03** — candidate-MET.
- **E206-04 / R07** — candidate-MET.
- **E206-05 / R04** — candidate-MET.
- **E206-06 / R05** — RECONCILED in r=3: split into `E206-06D` (docs limitation = `met`) and GAP `E206-06/R05` (demonstrated workaround = `open`); rv2's `OPEN` here and the rv2 manifest's `met` are both historical — see `## PLAN r=3` below.
- **E206-07 / R06** — RECONCILED in r=3: superseded by the rv3 `E206-07` row (`met` for builds/suites/provenance); rv2's `OPEN` here and the rv2 manifest's `met` are both historical — see `## PLAN r=3` below.
- **E206-08 / R06** — OPEN (provenance/check audit).

GAP rows stay `open` (never `na`/`met`).

### ACT disposition
#206 is **NOT closable**; it remains **OPEN** with R01/R02 GAP in milestone `1.0.9-rc2`.

---

| UTC time | phase | revision | iteration | event | evidence pointer |
|---|---|---|---|---|---|
| 2026-10-10 | DO | r2 | n1/3 | DO started | `artifacts/pdca/D206/rv2/` |
| 2026-10-10T14:53Z | DO | r2 | n1/3 | D206-PREFLIGHT **done** | scope validated (`scripts/validate_inner_loop.py brief`) exit 0 |
| 2026-10-10T14:53Z | DO | r2 | n1/3 | D206-RECON **done** | alias suite 103 passed; `artifacts/pdca/D206/rv2/recon.log` |
| 2026-10-10T14:53Z | DO | r2 | n1/3 | D206-TEST **done** | R03/R07 oracle tests green; `tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs` |
| 2026-10-10T14:53Z | DO | r2 | n1/3 | D206-DOC **done** (limitation only) | `docs/guide/02-joins.md`, `docs/ru/guide/02-joins.md`; R05 workaround sub-obligation retained open |
| 2026-10-10T14:53Z | DO | r2 | n1/3 | D206-EVIDENCE **done** | `artifacts/pdca/D206/rv2/manifest.json`; validator exit 0 |
| 2026-10-10T14:53Z | DO | r2 | n1/3 | D206-GAP **retained/open** | R01/R02 + R05 workaround GAP; `artifacts/pdca/D206/rv2/GAP.md` |
| 2026-10-10T14:53Z | DO->CHECK | r2 | n1/3 | DO to CHECK boundary: C1 0/21, C2 0/103, C3 0, C4 0, C5 0, C6 0, C7 0, C8 0, C9 0; coverage line 88.4% branch 80.4%; docs-build exit 0; integration 3635 total 0 failed | `artifacts/pdca/D206/rv2/{recon.log,build-release.log,coverage-report/Summary.json,integration.log,docs-build.log,hygiene.log,manifest.json}` |


---

## PLAN r=3 (ready, 2026-10-10) — contract supersession

**Durable state:** Current cycle **N=1**; Plan revision **r=3**; Attempt **n=1/3**; `plan_state=ready`; `contract_rv=3`; milestone `1.0.9-rc2`; baseline `a60ecb84`.
**Defect history:** key `t206-unreachable-normgen007` -> observed r1 n1 (STOP/blocker) + r2 n1 (CHECK FAIL) + r3 n1, **0 applied fixes** (generator unchanged throughout); evidence `artifacts/pdca/D206/rv1/*`, `rv2/*`, `rv3/*`; result **GAP-OPEN (not resolved)**.

### Reason r=3 (genuine revision — tasks/dependencies changed)
CHECK r2 **FAILED**: (a) the rv2 oracles were insufficient — the R03/R07 tests were not decisive (the `CountOccurrences == 1` assertion passed vacuously for a one-call-site generator) and the negative/positive controls were absent; (b) the rv2 `required_rows` representation was self-blocking — it listed E206-01/E206-02 (and R05's demonstrated-workaround) as `open` while those rows were also demanded for PASS, so no scoped PASS was reachable; (c) the rv2 plan table contradicted the rv2 manifest on E206-06/E206-07 (RECONCILED above). r=3 **changes the task set and dependencies**: it adds the two-candidate provenance control and the declaration-removal negative, splits the docs row from the workaround row (`E206-06D` vs `E206-06/R05`), and scopes PASS to rows that are all satisfiable on the current generator. A rejected candidate without a plan change would not be a new revision; this is a real replan, so `r` increments and `n` resets to 1.

### Scoped PASS-blocking `required_rows` (all must be `met`)
`E206-03` (R03), `E206-04` (R07), `E206-05` (R04), `E206-06D` (R05 docs limitation), `E206-07` (R06 builds/suites/provenance), `E206-08` (R06 provenance/check audit). Frozen in `artifacts/pdca/D206/rv3/manifest.json`.

### GAP ledger (durable #206 obligations, milestone `1.0.9-rc2`; NOT PASS-blocking rows)
- **E206-01 / R01** — `open`: a reachable supported-syntax last-step same-signature/different-body collision is **unreachable by construction** (emitted body is a pure function of the emitted signature; the last-step `JoinedType` is rendered as `TJoin`). Reopen trigger: a committed supported-syntax reproducer **or** an approved design that invalidates the purity proof.
- **E206-02 / R02** — `open`: the reject-all / `NORMGEN007`-every-distinct-location branch cannot fire, for the same purity reason. Reopen trigger: same as E206-01.
- **E206-06 / R05 (original demonstrated-workaround)** — `open`: no verified `NORMGEN007` workaround can be demonstrated while the diagnostic is unreachable; documenting one would violate the R05 negative. Reopen trigger: a reachable collision with an observable remediation.

The GAP rows stay `open` (never `na`/`met`) and are recorded in `artifacts/pdca/D206/rv3/gap-ledger.json`.

### Oracle-fix acceptance (R03/R07)
- **R03** requires a **two-candidate provenance control** (`R03_control_distinct_aliases_process_both_call_sites`): the same two call sites with **distinct** aliases must yield exactly **two** overloads, so a generator that silently processes only one call site fails the control. The collapse test asserts exactly one `Join<TJoin>`, 0 `NORMGEN007`, 0 compiler errors, plus the intermediate-`IsCte` continuation.
- **R07** requires the expected declarations asserted **before** the byte compare (non-vacuity), `forward == reversed == repeat`, and a **declaration-removal negative** demonstrated to fail (exit 2) and then restored green (exit 0).

### ACT disposition
After the scoped PASS (all six required rows `met`), ACT commits under `#206 ` (no push); **#206 stays OPEN** in milestone `1.0.9-rc2`; the report is **"scoped deliverable accepted; issue unresolved"** (R01/R02 and the R05 workaround remain the tracked GAP).

---

| UTC time | phase | revision | iteration | event | evidence pointer |
|---|---|---|---|---|---|
| 2026-10-10T10:03Z | PLAN | r3 | n1/3 | **PLAN r=3 ready** — CHECK r2 FAIL: insufficient oracles + self-blocking rv2 rows; genuine revision (tasks/dependencies changed) | `docs/specs/status/rc2-206-sourcegen-cs0111-alias-join-1.md` |
| 2026-10-10T10:03Z | DO | r3 | n1/3 | DO started | `artifacts/pdca/D206/rv3/` |
| 2026-10-10T10:03Z | DO->CHECK | r3 | n1/3 | DO -> CHECK boundary (r3): Debug 0/0, Release 0/0, alias-suite 0 (104/104), docfx 0, hygiene 0; coverage/integration reused from rv2 (`src/` unchanged since those runs) | `artifacts/pdca/D206/rv3/{build-debug.log,build-release.log,alias-suite.log,docs-build.log,hygiene.log}` |

---

## ACT (r=3, 2026-10-10)

- **Verdict:** scoped deliverable **ACCEPTED** (CHECK **PASS**, r=3 n=1).
- **Criterion dispositions:** R03 / R04 / R05-docs / R06 / R07 **met**; R01, R02 and the R05 demonstrated-workaround obligation remain **OPEN GAP** (durable, milestone `1.0.9-rc2`; not closed by this ACT). R01/R02 stay unreachable by construction (emitted body is a pure function of the emitted signature; NORMGEN007 unreachable); reopen trigger = a committed supported-syntax reproducer or an approved rendering/signature change that invalidates the purity proof.
- **Evidence roots:** `artifacts/pdca/D206/rv2/**`, `artifacts/pdca/D206/rv3/**`.
- **Hygiene:** no TODO plan file to delete; no public API / naming / registry change, so no `API-NAMING-REVIEW.md` or `code-smells-review.md` edit is required.
- **Issue:** #206 remains **OPEN** in milestone `1.0.9-rc2`.

- **ACT commit:** `8f31c9a4`.

---

## RESOLUTION — resolved-by-verification (2026-10-10)

- **Outcome:** the #206 `CS0111` collision does **not** reproduce on the current generator (post-#159/#160); the "different queries reuse one `Alias.X` with different joined types" scenario is **already supported** at compile time and at runtime. Closed by verification, not by a generator change.
- **Mechanism (current tree):** the emitted alias-extension body/signature is a pure function of the non-last-step inputs; the last step's `JoinedType` is rendered as the method type parameter `TJoin`. Two independent chains that reuse one alias with different joined types therefore collapse into ONE generic `Join<TJoin>` extension. The historical `RenderExtension` references above (`:721-787`, `:692-697`, `:747-751`) are stale: the method is now `RenderAliasExtension` (`JoinAliasGenerator.cs:1125-1191`), and the collapse/diagnostic site is `:1049-1068` (`AliasExtensionCollision` = NORMGEN007 at `:126-133`). Historical records are left intact.
- **Decisive evidence:**
  - Compile: `JoinAliasGeneratorDiagnosticTests.R03_byte_identical_distinct_chains_collapse_to_one_method` — two `Alias.X` chains with different last-step joined types → exactly one `Join<TJoin>`, 0 NORMGEN007, 0 compiler errors (no `CS0111`).
  - Runtime (new): `tests/nextorm.alias.tests/AliasSameAliasRuntimeTests.Same_alias_different_joined_types_across_two_queries` — two independent queries reuse `Alias.Target` with `Person` vs `Product`; SQLite returns `[10]`/`[100]`; SQL `person as 't2'` / `product as 't2'`.
  - Alias suite: **105/105 passed**; solution build Debug + Release 0/0.
- **R01/R02 disposition:** previously an explicit GAP (NORMGEN007 unreachable). With the confirmed reproduction of the *supported* scenario and the decision that "both projections must work" is achieved via the generic collapse, R01/R02 are **superseded by verification** — the intended user outcome is met; the NORMGEN007 same-signature/different-body branch remains a documented defensive no-op (kept, not removed — design decision D2).
- **NORMGEN007:** kept as a defensive guard; EN/RU guides already describe it as structurally unreachable. No generator change.
- **Surface:** `AliasGeneratedSurfaceTests.Generated_public_type_set_is_frozen` updated to include the intentional `AliasJoin/AliasProjection_P1_A2_Target\`2` pair (pre-release: no frozen surface).
- **Issue:** #206 **closed** (milestone `1.0.9-rc2`) with an evidence comment.
