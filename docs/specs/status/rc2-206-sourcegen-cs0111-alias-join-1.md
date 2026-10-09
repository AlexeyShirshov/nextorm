# Task T206 / #206 — Source generator CS0111 signature collision (alias joins)

- collection: `rc2-final`
- intent: own PLAN (COLLECTION TASK PLAN); phase P (no DO yet)
- selected_variant: `pdca-dotnet`
- cycle_id: N=1
- plan_revision: r=1
- baseline: `9a2a2871`
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
