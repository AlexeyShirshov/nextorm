# T236 (#236) — D236-build-advisories, cycle 1

## Identity and state

- task: T236
- issue: #236
- collection: 1.0.9-todo
- group: G1
- variant: pdca-dotnet
- cycle: 1
- plan_revision: r=5
- iteration: n=1/3
- evidence_contract: rv=4
- plan_state: done
- phase: ACT / EXIT (closed)
- branch / HEAD at cycle start: `1.0.9-rc3` @ `5e1c31f3`
- single group → current worktree/branch, no worktree, no branch, no merge, no push

Notice: the host profile for this cycle exposes no `todowrite` tool, so the todo/sidebar
mirror is unavailable; this status file is the only progress record.

## Plan revision history

- **r=1 → r=2 — scope artifact only.** The r=1 structured `test scope` was rejected by
  `scripts/validate_inner_loop.py brief` (exit 2): `FAIL: missing required key: unit`,
  `FAIL: missing required key: scope`, and — with the envelope repaired — `FAIL: fictitious
  rebuild for docs-only scope` (`rebuild: "affected"` with a `.props`-only `scope.files`).
- PLAN r=2, n=1 corrects the scope artifact only. `scope.rebuild: "none"` satisfies the
  validator's no-compiled-source heuristic; it does not classify this dependency change as
  documentation-only or waive any restore/build obligation. All existing C1–C9 commands in
  D1/D3/D5, including re-restore, `--no-incremental` builds and required provider checks,
  remain mandatory.
- Corrected scope artifact: `artifacts/pdca/D236-build-advisories/rv2/scope.json`
  (validator `brief` exit 0). Copy of the validated brief: `/tmp/opencode/D236-scope.json`.
- Plan mechanism, acceptance criteria, evidence contract (rv=1) and DO task list are
  **unchanged** from r=1; the r=2 correction is documentary (scope artifact envelope).

- **r=2 → r=3 (evidence_contract rv=1 → rv=2) — additive only.** `planner` adjudicated the two
  D2/D3 findings and issued r=3 with **all existing acceptance criteria and R236-01…R236-09
  obligations preserved and NOT weakened**; only new DO actions were added (D2a: remove the
  provably-unused example import; D2b: three preservation guards restoring the four drifted
  resolutions). rv2 supersedes rv1 as the current contract; `artifacts/pdca/D236-build-advisories/rv1/evidence-contract.json`
  remains the frozen rv1 record, and the current manifest is
  `artifacts/pdca/D236-build-advisories/rv2/evidence-contract.json`.
  **rv=2 scope:** `artifacts/pdca/D236-build-advisories/rv3/scope.json` (`mode=3` layout: scope
  under `rv3/`, evidence under `rv2/`), `validate_inner_loop.py brief` exit 0; `scope.files` gained
  `examples/nextorm.examples.mssql.adventureworks/AdventureWorksQueries.cs` and `scope.rebuild`
  went `none` → `affected`, with one appended rationale clause (both planner-stated deltas only).

- **r=3 → r=4 (evidence_contract rv=2 → rv=3) — item A re-scoped, everything else additive.**
  `planner` withdrew the r=3 item-A mechanism after its premise was **falsified by measurement**:
  removing the example's "unused" import produced 4 NEW errors (CS0103 `Alias` ×3 + CS1061
  `WithAlias` ×1), so the import is *not* unused — `Alias.Root/.SalesPerson/.Manager` is emitted
  into `NextORM.Generated.nextorm_examples_mssql_adventureworks` by
  `src/nextorm.core.sourcegenerator`. Item A is now handled **in-cycle by wiring that generator**
  into the example project. The item-B guards stay applied. **rv3 supersedes rv2 (and rv1): every
  row and obligation R236-01…R236-11 is carried forward unchanged and NOT weakened**; R236-10 is
  `na` solely because its mechanism was withdrawn (negative-case evidence preserved), and the two
  new rows R236-A-META / R236-A-BUILD are additive. Current manifest:
  `artifacts/pdca/D236-build-advisories/rv3/evidence-contract.json`.

- **r=4 → r=5 (evidence_contract rv=3 → rv=4) — MECHANISM REVISION: transitive pinning rejected.**
  `escalate` ruled and `planner` replaced the mechanism. Three rounds of `CentralPackageTransitivePinningEnabled`
  proved uncontainable: r=2 produced 4 drifted entries in 2 projects, the r=3 guards "fixed" those but a
  third round surfaced **8 drifted entries in `tests/nextorm.alias.tests`** — a project the 13-project
  graph guard never looked at (the capture set covered 13 of the 25 inventory projects, i.e. it was blind
  to 12; the plan brief said 11 — measured value reported). Every guard was project-conditioned, so each
  round just moved the blast radius. **Replacement:** one direct
  `<PackageReference Include="System.Security.Cryptography.Pkcs" />` (no `Version`, CPM supplies 10.0.13)
  in `src/nextorm.sqlserver`, which flows to its consumers through the existing `ProjectReference` graph —
  no global property, no guard, no transitive promotion for unrelated packages.
  **Authorized tracked change set is now 3 files, 1 added line each:** `Directory.Packages.props`,
  `src/nextorm.sqlserver/nextorm.sqlserver.csproj`, `examples/nextorm.examples.mssql.adventureworks/nextorm.examples.mssql.adventureworks.csproj`.
  **Superseded by r=5:** all r=2/r=3 pinning scaffolding is REMOVED (`CentralPackageTransitivePinningEnabled`
  and the four `Update` guards); the r=2 `Directory.Packages.props` block and the r=3 item-B guards are
  historical only. The r=4 analyzer wiring in the example `.csproj` is RETAINED.

### Supersession ledger (rv=4)

| Row | Disposition at rv=4 |
|---|---|
| R236-01 | carried (red-baseline evidence unchanged; the pre-fix defect is historical fact) |
| R236-02, R236-03, R236-04, R236-05, R236-06, R236-07, R236-08, R236-09 | carried-with-new-run (same obligations, re-measured under the new mechanism; R236-05…09 still open) |
| R236-10 | **withdrawn** — premise falsified by measurement (removing the example import produced CS0103 `Alias` ×3 + CS1061 `WithAlias` ×1); negative-case evidence preserved |
| R236-11 | **rewritten** for the 25-project inventory (was a 13-project graph delta) |
| R236-A-META, R236-A-BUILD | carried, re-stated against the new mechanism |
| **R236-MECH** (new) | mechanism proof: one direct Pkcs reference, only in `src/nextorm.sqlserver`; central version 10.0.13; pinning property and all four guards gone; analyzer reference retained |
| **R236-DEPTH** (new) | depth-tie: example + integration projects both resolve Pkcs 10.0.13, no downgrade/conflict (NU1605) |
| **R236-NUSPEC** (new) | packaging: `nextorm.sqlserver` nuspec declares `System.Security.Cryptography.Pkcs >= 10.0.13` |

## Progress log

| UTC time | phase | revision | iteration | event | evidence pointer |
|---|---|---|---|---|---|
| 2026-10-15T04:31Z | PLAN | r=1 | n=1/3 | r=1 scope rejected by validator (exit 2) → STOP, no edits | `/tmp/opencode/D236-scope.json` |
| 2026-10-15T04:31Z | PLAN | r=2 | n=1/3 | corrected scope validated, `brief` exit 0 | `artifacts/pdca/D236-build-advisories/rv2/scope.json` |
| 2026-10-15T04:31Z | PLAN | r=2 | n=1/3 | D0 status/contract written, `plan_state: ready` | this file |
| 2026-10-15T04:32Z | DO | r=2 | n=1/3 | D1 red baseline captured (workspace): restore exit 1 (13 failed projects), build exit 1, 0 Warning(s) / 26 Error(s) | `artifacts/pdca/D236-build-advisories/rv1/before/workspace/{restore,build}.{log,exit}` |
| 2026-10-15T04:32Z | DO | r=2 | n=1/3 | D1 red baseline captured (clean `git archive HEAD`, 6115 files): restore exit 1, build exit 1, 0 Warning(s) / 26 Error(s) | `artifacts/pdca/D236-build-advisories/rv1/before/clean/{restore,build}.{log,exit}` |
| 2026-10-15T04:32Z | DO | r=2 | n=1/3 | D1 evaluated-property probe: 13/13 exit 0; CPM=true, CTPP unset, TWAE/audit unchanged; examples adventureworks CPM=true (gap (a) closed) | `artifacts/pdca/D236-build-advisories/rv1/before/workspace/evaluated.json` |
| 2026-10-15T04:32Z | DO | r=2 | n=1/3 | D1 initial state: HEAD `5e1c31f3`, index empty, 47 tracked files dirty + untracked task/evidence paths | `artifacts/pdca/D236-build-advisories/rv1/before/workspace/git-state.txt` |
| 2026-10-15T04:52Z | DO | r=2 | n=1/3 | D2 applied: 3 added lines, 0 removed in `Directory.Packages.props` (CRLF 47 / lone-LF 0) | `artifacts/pdca/D236-build-advisories/rv1/after/Directory.Packages.props.diff` |
| 2026-10-15T04:56Z | DO | r=2 | n=1/3 | D3 restore GREEN: `dotnet restore nextorm.slnx --force --no-cache` exit 0, 0 NU19xx; advisory gone | `artifacts/pdca/D236-build-advisories/rv1/after/restore.{log,exit}` |
| 2026-10-15T04:58Z | DO | r=2 | n=1/3 | D3 build **RED** (not a package error): `--no-incremental` exit 1, 0 Warning(s) / 1 Error(s) — CS0234 in examples/…adventureworks; proven pre-existing at HEAD via props-only overlay on the HEAD archive. No second fix attempted | `artifacts/pdca/D236-build-advisories/rv1/after/build-nonincremental.log`, `artifacts/pdca/D236-build-advisories/rv1/after/diag-head-example-build.log` |
| 2026-10-15T04:59Z | DO | r=2 | n=1/3 | D3 audit GREEN: 25 projects, 0 vulnerable entries, no Pkcs advisory; Pkcs resolves 10.0.13 in all 13 projects; probe 13/13 exit 0 (`CTPP=true` incl. examples, audit/TWAE enabled, no NU19xx suppression) | `artifacts/pdca/D236-build-advisories/rv1/after/{audit.json,evaluated.json}` |
| 2026-10-15T05:02Z | DO | r=2 | n=1/3 | D3 graph diff (isolated arms, linux host, identical HEAD tree): **17 entries** — 13 allowed Pkcs 9.0.11→10.0.13, Console 8.0.0→8.0.0 preserved, **4 unexpected** → AC236-03 not met | `artifacts/pdca/D236-build-advisories/rv1/after/graph-diff.json` |
| 2026-10-15T05:03Z | DO | r=2 | n=1/3 | rv=1 manifest written and validated | `artifacts/pdca/D236-build-advisories/rv1/evidence-contract.json` (`validate_inner_loop.py manifest` exit 0) |
| 2026-10-15T05:14Z | PLAN | r=3 | n=1/3 | rv2 scope written (`files` += example `.cs`, `rebuild` `none`→`affected`) and validated | `artifacts/pdca/D236-build-advisories/rv3/scope.json` (`validate_inner_loop.py brief` exit 0) |
| 2026-10-15T05:16Z | DO | r=3 | n=1/3 | D2b applied: 3 preservation guards (props now 6 added / 0 removed lines, CRLF 50 / lone-LF 0) | `artifacts/pdca/D236-build-advisories/rv2/props.diff` |
| 2026-10-15T05:18Z | DO | r=3 | n=1/3 | **D2a FAIL-CLOSED**: removing the example `using NextORM.Generated.nextorm_examples_mssql_adventureworks;` surfaced 4 NEW errors (CS0103 `Alias` ×3, CS1061 `WithAlias` ×1) → reverted that one line, no workaround, no analyzer wiring. The import is *not* unused: `Alias.Root/.SalesPerson/.Manager` is generator-emitted | `artifacts/pdca/D236-build-advisories/rv2/d2a-fail-closed.json`, `artifacts/pdca/D236-build-advisories/rv2/build-nonincremental.log` |
| 2026-10-15T05:19Z | DO | r=3 | n=1/3 | D2a revert verified: `AdventureWorksQueries.cs` byte-identical to HEAD (`git diff` empty, CRLF 549 / lone-LF 0). Task edits = `Directory.Packages.props` only | `git diff --numstat` → `6 0 Directory.Packages.props` |
| 2026-10-15T05:19Z | DO | r=3 | n=1/3 | D3 re-run **stopped** at the fail-closed gate: rv2 `restore` exit 0 (0 NU19xx); `build-nonincremental` exit 1, 0 Warning(s) / 4 Error(s). No audit/package-diff/probe/graph re-measurement at rv2 | `artifacts/pdca/D236-build-advisories/rv2/{restore,build-nonincremental}.{log,exit}` |
| 2026-10-15T05:20Z | DO | r=3 | n=1/3 | rv2 manifest (R236-01…R236-11) written and validated | `artifacts/pdca/D236-build-advisories/rv2/evidence-contract.json` |
| 2026-10-15T05:30Z | PLAN | r=4 | n=1/3 | rv3 scope (`files` += example `.csproj`, `rebuild` stays `affected`) validated | `artifacts/pdca/D236-build-advisories/rv3/scope.json` (`validate_inner_loop.py brief` exit 0) |
| 2026-10-15T05:32Z | DO | r=4 | n=1/3 | D:A1 generator wired into the example (1 added line, 0 removed); `AdventureWorksQueries.cs` byte-identical to HEAD; metadata probe exit 0, segment match confirmed | `artifacts/pdca/D236-build-advisories/rv3/{example-csproj.diff,metadata.log,query.diff}` |
| 2026-10-15T05:35Z | DO | r=4 | n=1/3 | D:A2 build gates ALL GREEN: example build 0/0, clean exit 0, solution build 0/0, solution build `--no-incremental` 0/0 | `artifacts/pdca/D236-build-advisories/rv3/{example-build,clean,build,build-nonincremental}.{log,exit}` |
| 2026-10-15T05:37Z | DO | r=4 | n=1/3 | `git diff --check` unscoped exit 2 from TWO PRE-EXISTING dirty benchmark artifacts; task-scoped check exit 0 | `artifacts/pdca/D236-build-advisories/rv3/{diff-check.log,diff-check-scoped.log}` |
| 2026-10-15T05:40Z | DO | r=4 | n=1/3 | Item B measured: restore exit 0, audit 0 vulnerable entries, 13/13 probe exit 0 (CTPP true everywhere, no NU19xx suppression) | `artifacts/pdca/D236-build-advisories/rv3/{restore,audit.json,evaluated.json}` |
| 2026-10-15T05:43Z | DO | r=4 | n=1/3 | Item B graph diff: **13 entries, all allowed Pkcs 9.0.11→10.0.13, zero drift** — the r=3 guards restored benchmark CodeAnalysis 3.11.0/4.14.0/4.14.0 (Common followed CSharp) and core.tests Xunit.DependencyInjection 12.0.0; Console 8.0.0 | `artifacts/pdca/D236-build-advisories/rv3/{graphs.json,graph-diff.json}` |
| 2026-10-15T05:45Z | DO | r=4 | n=1/3 | **NEW DEFECT (outside the 13-project capture set):** CLI package diff = 25 entries — 8 extra in `tests/nextorm.alias.tests` (unguarded `Xunit.DependencyInjection` 12.0.0→12.0.1 + cascade 8.0.0→10.0.12, `Microsoft.CodeAnalysis.Analyzers` 5.9.0-1.26328.17→5.9.0) and 4 from the pre-existing dirty `examples/Directory.Packages.props` | `artifacts/pdca/D236-build-advisories/rv3/{package-diff.txt,graph-diff14.json,dirty-examples-props.diff}` |
| 2026-10-15T05:47Z | DO | r=4 | n=1/3 | Change set exact: two task files only (`Directory.Packages.props` 6/0, example `.csproj` 1/0); index empty, no commits | `artifacts/pdca/D236-build-advisories/rv3/task-change-set.txt` |
| 2026-10-15T05:48Z | DO | r=4 | n=1/3 | rv3 manifest (13 rows: R236-01…R236-11 + R236-A-META + R236-A-BUILD) written and validated | `artifacts/pdca/D236-build-advisories/rv3/evidence-contract.json` |
| 2026-10-15T06:00Z | PLAN | r=5 | n=1/3 | rv4 scope validated (`files` = props + sqlserver csproj + example csproj, `rebuild: none`) | `artifacts/pdca/D236-build-advisories/rv4/scope.json` (`validate_inner_loop.py brief` exit 0) |
| 2026-10-15T06:05Z | DO | r=5 | n=1/3 | D5 mechanism replacement applied: pinning property + 4 guards removed from `Directory.Packages.props` (net 1 added line); direct Pkcs `PackageReference` added to `src/nextorm.sqlserver` (1 line); r=4 analyzer wiring retained; example `.cs` byte-identical to HEAD; numstat = exactly 3 files × `1 0` | `artifacts/pdca/D236-build-advisories/rv4/{edit/*.diff,act/numstat.txt}` |
| 2026-10-15T06:12Z | DO | r=5 | n=1/3 | D5b rv4 scope validated (`brief` exit 0); runner `rv4/verify.py` authored (records argv/cwd/log/exit for every child process) | `artifacts/pdca/D236-build-advisories/rv4/{scope.json,verify.py}` |
| 2026-10-15T06:14Z | DO | r=5 | n=1/3 | D6 carry/mechanism/scope-crlf-index GREEN: numstat exactly 3 × `1 0`, example `.cs` HEAD-clean, index empty, one direct Pkcs reference only in `src/nextorm.sqlserver`, no residual pin/guard, analyzer reference retained | `artifacts/pdca/D236-build-advisories/rv4/{act/carry.json,mechanism.json,act/scope-crlf-index.json}` |
| 2026-10-15T06:18Z | DO | r=5 | n=1/3 | D6 R236-DEPTH GREEN: example + integration both resolve Pkcs 10.0.13, no NU1605, no refinement needed (runner bug in my own `dotnet list` argv found and fixed first) | `artifacts/pdca/D236-build-advisories/rv4/depth-tie/depth-tie.json` |
| 2026-10-15T06:20Z | DO | r=5 | n=1/3 | D6 R236-A-META / R236-A-BUILD GREEN: segment `nextorm_examples_mssql_adventureworks`, `.cs` HEAD-clean, scoped diff-check exit 0, example build 0 W / 0 E | `artifacts/pdca/D236-build-advisories/rv4/{example/*,after/workspace/example-build.*}` |
| 2026-10-15T06:26Z | DO | r=5 | n=1/3 | D6 R236-02 GREEN: clean exit 0, restore exit 0 (0 NU19xx), solution build 0 W / 0 E, `--no-incremental` 0 W / 0 E | `artifacts/pdca/D236-build-advisories/rv4/after/workspace/green.json` |
| 2026-10-15T06:28Z | DO | r=5 | n=1/3 | D6 R236-03 GREEN: 25-project audit 0 vulnerable entries; 13/13 probe exit 0 (CPM/TWAE/NuGetAudit true, no NU19xx suppression, pinning property unset everywhere) | `artifacts/pdca/D236-build-advisories/rv4/after/{audit.json,evaluated.json,audit-properties.json}` |
| 2026-10-15T06:31Z | DO | r=5 | n=1/3 | D6 **R236-11 GREEN**: 25-project inventory diff = **17 entries** — 13 allowed Pkcs 9.0.11→10.0.13, 4 named pre-existing dirty-`examples/Directory.Packages.props` entries (`nextorm*` 1.0.8-b→1.0.9-rc2), **0 other**; single kind change `src/nextorm.sqlserver` Pkcs transitive→topLevel; the 8 alias.tests drift entries are GONE | `artifacts/pdca/D236-build-advisories/rv4/{package-diff.txt,inventory.json,package-kind-diff.json}` |
| 2026-10-15T06:33Z | DO | r=5 | n=1/3 | D6 R236-04 GREEN: graph diff 13/13 allowed Pkcs entries in both the 13- and 14-project arms (alias.tests included), Console preserved at 8.0.0 (runner key-mismatch bug found and fixed first) | `artifacts/pdca/D236-build-advisories/rv4/{graph-diff.json,graph-diff14.json,graphs-verify.json}` |
| 2026-10-15T06:35Z | DO | r=5 | n=1/3 | D6 R236-NUSPEC GREEN: `dotnet pack` exit 0; nuspec dependency `<dependency id="System.Security.Cryptography.Pkcs" version="10.0.13" exclude="Build,Analyzers" />`; no stray `.nupkg` outside the artifact root | `artifacts/pdca/D236-build-advisories/rv4/pack/{nuspec.json,nuspec.xml}` |
| 2026-10-15T06:37Z | DO | r=5 | n=1/3 | rv4 manifest (16 rows) written and validated | `artifacts/pdca/D236-build-advisories/rv4/evidence-contract.json` (`validate_inner_loop.py manifest` exit 0) |
| 2026-10-15T07:00Z | DO | r=5 | n=1/3 | Skill `running-integration-tests` loaded before containers; Podman socket present, machine `Currently running`, API ping OK | `/mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` |
| 2026-10-15T07:12Z | DO | r=5 | n=1/3 | D7 **R236-05 GREEN**: 8/8 non-integration projects exit 0, discovered>0, 0 failed (totals 2529/948/976/454/336/1747/733/126; 9 authored skips, no availability skip) | `artifacts/pdca/D236-build-advisories/rv4/tests/summary.json` |
| 2026-10-15T07:15Z | DO | r=5 | n=1/3 | D7 **R236-06 GREEN**: integration sweep exit 0, total 3922 / failed 0 / skipped 198 (all capability skips); PostgreSQL 931, SQL Server 831, MySQL 700, ClickHouse 290 executed; 5 reuse containers observed | `artifacts/pdca/D236-build-advisories/rv4/tests/{integration.log,providers.json}` |
| 2026-10-15T07:22Z | DO | r=5 | n=1/3 | D8 **R236-07 GREEN**: clean tree `/tmp/opencode/T236-clean` (archive HEAD + only the 3 task files; 6115 files, 0 bin/obj) — restore 0, build 0 W/0 E, `--no-incremental` 0 W/0 E, audit 0, 0 vulnerable | `artifacts/pdca/D236-build-advisories/rv4/clean-source/{manifest.json,overlay.diff,*.log,*.exit}` |
| 2026-10-15T07:25Z | DO | r=5 | n=1/3 | DO→CHECK gate: report-mode inner-loop evidence written and validated (12 executions: 8 inner + 4 boundary; one comprehensive solution build recorded, budget respected) | `artifacts/pdca/D236-build-advisories/rv4/evidence.json` (`validate_inner_loop.py report` exit 0) |
| 2026-10-15T07:26Z | DO | r=5 | n=1/3 | rv4 contract row statuses updated to observed: R236-05/06/07 met; R236-08/09 remain open (ACT/CHECK); manifest re-validated | `artifacts/pdca/D236-build-advisories/rv4/evidence-contract.json` (exit 0) |
| 2026-10-15T07:45Z | ACT | r=5 | n=1/3 | ACT (D10) under the collection auto-commit authorization: routed evidence corrections applied (R236-05 artifacts, R236-06 erratum, graph-instrument label, `check/verdict.json` preserved as non-passing) and the 4 deferred-risk rows recorded; product commit `c554d5e8` (exit 0; staged paths = exactly the 3 authorized files); closure commit follows | `artifacts/pdca/D236-build-advisories/rv4/{check/verdict.json,act/commits.log,evidence-contract.json}` |

## Defect history

| defect key | first seen | plan rev / attempt | fixes applied | evidence | last outcome |
|---|---|---|---|---|---|
| `D236-AC01-example-cs0234` | r=2 / n=1 (D3) | r=2 / n=1 | 0 (out of the authorized single-axis edit; not mine to fix) | `after/build.log`, `after/build-nonincremental.log`, `after/diag-head-example-build.log` | **open** — pre-existing at HEAD; blocks AC236-01 / R236-02 → planner decision (widen scope or narrow the AC) |
| `D236-AC03-transitive-pin-drift` | r=2 / n=1 (D3) | r=2 / n=1 | 0 (needs a planner decision; no unilateral second fix) | `after/graph-diff.json` (4 entries: benchmark `Microsoft.CodeAnalysis.{Analyzers,CSharp,Common}` 3.11.0/4.14.0→5.9.0; core.tests `Xunit.DependencyInjection` 12.0.0→12.0.1) | **open** — `CentralPackageTransitivePinningEnabled` promotes every existing `PackageVersion`; AC236-03 not met as written |
| `D236-AC01-example-cs0234` | r=2 / n=1 (D3) | r=2 → r=4 | 0 (out of the then-authorized axis) → resolved by the r=4 analyzer wiring | `rv3/example-build.log`, `rv3/build.log`, `rv3/build-nonincremental.log`, `rv3/example-csproj.diff` | **closed** — the generator wiring makes the example (and the whole solution) build with 0 W / 0 E |
| `D236-AC01-example-using-not-unused` | r=3 / n=1 (D2a) | r=3 → r=4 | 1 (reverted the removal; no workaround, no analyzer wiring) | `rv2/build-nonincremental.log` (CS0103 `Alias` 62,54 / 63,122 / 64,121 + CS1061 `WithAlias` 62,44), `rv2/d2a-fail-closed.json` | **closed by r=4** — the mechanism was withdrawn; the falsified premise stands as the negative-case evidence for R236-10 (`na`) |
| `D236-AC03-transitive-pin-drift` | r=2 / n=1 (D3) | r=2 → r=4 | 3 preservation guards (r=3, item B) | `rv3/graph-diff.json` (13 allowed entries, 0 drift) | **closed for the 13-project scope** — benchmark `Microsoft.CodeAnalysis.{Analyzers,CSharp,Common}` back to 3.11.0/4.14.0/4.14.0 and core.tests `Xunit.DependencyInjection` back to 12.0.0 |
| `D236-AC03-alias-tests-drift` | r=4 / n=1 (D3 item-B measurement) | r=4 → r=5 | 0 (no fourth guard added — explicitly forbidden) → resolved by the r=5 mechanism replacement | `rv3/package-diff.txt` (8 entries in `tests/nextorm.alias.tests`), `rv4/package-diff.txt` (0 entries), `rv4/graph-diff14.json` | **closed by r=5** — the 8 alias.tests entries are absent under the direct-reference mechanism; the earlier note read: — `Xunit.DependencyInjection` guard is conditioned on `nextorm.core.tests` only, but `tests/nextorm.alias.tests` also resolves it transitively (12.0.0→12.0.1), cascading `Microsoft.Extensions.{Configuration,Configuration.Abstractions,Configuration.Binder,Logging.Configuration,Logging.Console,Options.ConfigurationExtensions}` 8.0.0→10.0.12 and `Microsoft.CodeAnalysis.Analyzers` 5.9.0-1.26328.17→5.9.0. AC236-03 is not met solution-wide. Outside the 13-project capture set of R236-04 |
| `D236-MECH-transitive-pinning-uncontainable` | r=2 / n=1 → r=4 / n=1 | r=2 → r=5 | 0 fixes that held (2 rounds of guards: 4 drift entries → 8 drift entries in a project outside the guard/capture scope) | `rv2/props.diff`, `rv3/package-diff.txt`, `rv3/graph-diff14.json` | **closed by mechanism replacement** — `escalate`/`planner` rejected global transitive pinning at r=5 and replaced it with one direct `PackageReference`; verified by R236-MECH/R236-DEPTH |
| `D236-gitdiffcheck-preexisting-whitespace` | r=4 / n=1 (D:A2) | r=4 / n=1 | 0 (pre-existing, outside this task's footprint) | `rv3/diff-check.log` (unscoped, exit 2), `rv3/diff-check-scoped.log` (task files, exit 0) | **open** — trailing whitespace in two BenchmarkDotNet report artifacts that were already dirty at the D1 baseline |
| `D236-evidence-graphcapture-host-merge` | r=2 / n=1 (D3) | r=2 / n=1 | 1 (host-aware capture: `<host>:<tfm>` keys + `--hosts linux`) | `before/graphs-merged-stale-hosts.json` (+ `.note.json`), `after/graphs.json` | **fixed** — the first capture let stale `obj/windows` assets overwrite the current `obj/linux` values and masked the Pkcs change for 3 projects |

---

# T236 — PLAN r=1 content (mechanism/ACs/contract unchanged; header r=2)

## Goal

P236.1: remove the vulnerable transitive dependency without weakening audit or changing
unrelated package resolutions.

## Acceptance criteria (all P1)

- **AC236-01:** `dotnet build nextorm.slnx -c Debug` exits 0 with `0 Warning(s) / 0 Error(s)`.
  Negative: nonzero exit or either count nonzero.
- **AC236-02:** `System.Security.Cryptography.Pkcs` resolves to **10.0.13**, neither cited
  advisory present, audit and warnings-as-errors remain enabled. Negative: vulnerable
  resolution, suppression, disabled audit.
- **AC236-03:** unrelated resolved package identities/versions unchanged, including
  `tests/nextorm.core.tests`' transitive `Microsoft.Extensions.Logging.Console/8.0.0`.
  Negative: unexplained graph drift.
- **AC236-04:** all affected test projects pass; PostgreSQL/SQL Server/MySQL/ClickHouse
  integration cases actually execute (not skipped). Negative: zero discovery, failures,
  skipped providers.
- **AC236-05:** clean-source verification passes independently of existing dirty files.
  Negative: success depends on unrelated/untracked edits.
- **AC236-06:** only authorized task paths committed; status/collection closure accurate.
  Negative: unrelated staging, missing evidence, premature `done`.

## Decision (mechanism) — chosen

Central transitive pinning + one preservation guard. Exactly three additions to
`Directory.Packages.props`:

1. In the existing `<PropertyGroup>` next to `ManagePackageVersionsCentrally` (line 4):
   `<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>`.
2. After the existing `Microsoft.Data.SqlClient` `PackageVersion` (line 17):
   `<PackageVersion Include="System.Security.Cryptography.Pkcs" Version="10.0.13" />`.
3. After the existing `Microsoft.Extensions.Logging.Console` `PackageVersion` (line 25) and
   before the ItemGroup close (line 42):
   `<PackageVersion Update="Microsoft.Extensions.Logging.Console" Version="8.0.0" Condition="'$(MSBuildProjectName)' == 'nextorm.core.tests'" />`.

Version rationale: both 9.0.21 and 10.0.13 are listed patched by GHSA-846f-f764-76mx and
GHSA-2fmv-rv2c-59x7; 10.0.13 is chosen because the repo is `net10.0`
(`Directory.Build.props:29`) and its EF/Extensions band is 10.0.12, and 10.0.13 ships a
net10.0 dependency group. Rejected: direct references in 13 projects (artificial deps, broad
churn); `Microsoft.Data.SqlClient` upgrade alone (7.1.1's Pkcs floor 9.0.18 is still inside
the affected range); any audit suppression / `NuGetAudit=false` / `NoWarn` (masks
vulnerabilities) — the planner explicitly forbids a suppression fallback.

## Rejected alternatives

| Alternative | Benefit | Cost/risk | Decision |
|---|---|---|---|
| Central transitive pin + Console preservation guard | Three config additions; covers transitive consumers | Must verify every project graph | Chosen |
| Direct references in 13 projects | Explicit graph control | Artificial dependencies, broad project churn | Rejected |
| SqlClient upgrade or suppression | Superficially simple | Upgrade alone stays vulnerable; suppression hides vulnerabilities | Rejected |

## DO tasks

- D0 status/contract (this step); D1 fresh red baseline; D2 dependency fix (the 3 additions,
  CRLF); D3 green + graph comparison; D4 affected tests; D5 clean-source green
  (`git archive HEAD` archive + only `Directory.Packages.props` overlaid); D6 CHECK/ACT.

## Evidence contract (rv=1)

Manifest path: `artifacts/pdca/D236-build-advisories/rv1/evidence-contract.json`.

| Row | Req | Scenario | Required result / artifact |
|---|---|---|---|
| R236-01 | AC236-01 | fresh red: `dotnet restore nextorm.slnx`, `dotnet build nextorm.slnx -c Debug` | exit 1 with both named Pkcs advisories; `before/{workspace,clean}/{restore,build}.{log,exit}` |
| R236-02 | AC236-01 | green: `dotnet restore nextorm.slnx --force --no-cache`, `dotnet build nextorm.slnx -c Debug --no-incremental`, in workspace + clean archive | all exit 0, 0 warnings/0 errors; `after/*.log/.exit` |
| R236-03 | AC236-02 | `dotnet list nextorm.slnx package --include-transitive --vulnerable --format json --no-restore` + per-project evaluated-property probe (`dotnet msbuild <csproj> -getProperty:...`) | exit 0; Pkcs 10.0.13, no advisory, audit not suppressed; `after/{audit.json,evaluated.json}` |
| R236-04 | AC236-03 | complete before/after resolved-graph snapshots + diff | only the allowed Pkcs closure delta; Console stays 8.0.0 in core.tests; `{before,after}/graphs.json`, `after/graph-diff.json` |
| R236-05 | AC236-04 | per affected non-integration test project | each exit 0, nonzero discovered, 0 failed; `tests/<project>.{log,exit}` |
| R236-06 | AC236-04 | `DOCKER_HOST=… dotnet test tests/nextorm.integration.tests -c Debug` | exit 0, providers actually execute; `tests/integration.{log,exit,providers.json}` |
| R236-07 | AC236-05 | `git archive HEAD` → archive dir, overlay only `Directory.Packages.props`, build there | exit 0 without dirty files; `clean-source/{manifest.json,overlay.diff}` + clean logs |
| R236-08 | AC236-06 | scope/CRLF/index checks, explicit-path commits | only authorized paths, CRLF, commit exits 0; `act/{scope.json,crlf.json,commits.log}` |
| R236-09 | AC236-06 | status/collection review + manifest validation | both T236 rows done, unblock note accurate; `check/verdict.json`, `act/status-review.json` |

### rv=2 additions (superseding contract; rv=1 rows above remain in force)

| Row | Req | Scenario | Required result / artifact |
|---|---|---|---|
| R236-10 | AC236-01 | remove the example's unused `using NextORM.Generated.nextorm_examples_mssql_adventureworks;`; then `dotnet build nextorm.slnx -c Debug --no-incremental` and `dotnet build nextorm.slnx -c Debug` | both exit 0, 0 Warning(s) / 0 Error(s); `rv2/example.diff`, `rv2/build-nonincremental.{log,exit}`, `rv2/build.{log,exit}` |
| R236-11 | AC236-03 | full before/after package inventory (`dotnet list … package --include-transitive --format json`) in the fixed tree and in the isolated HEAD arm (host-aware tooling), diffed | exactly 13 × `Pkcs 9.0.11 → 10.0.13`; `Microsoft.Extensions.Logging.Console` = 8.0.0 in core.tests; drift restored (`Microsoft.CodeAnalysis.Analyzers` 3.11.0, `Microsoft.CodeAnalysis.CSharp` 4.14.0, `Microsoft.CodeAnalysis.Common` 4.14.0 by resolution, `Xunit.DependencyInjection` 12.0.0); zero other entries; `rv2/packages.json`, `rv2/packages-head.json`, `rv2/package-diff.txt` |

**Ownership note (verbatim):** "T236 clears a #232-introduced breakage; T232 must not re-break it".
`examples/nextorm.examples.mssql.adventureworks/AdventureWorksQueries.cs` is inside T232's
authorized footprint (`docs/specs/status/D232-sqlserver-pivot-unpivot-1.md:673`, `:874`).

### rv=3 additions (superseding contract)

| Row | Req | Scenario | Required result / artifact |
|---|---|---|---|
| R236-A-META | AC236-01 | msbuild `AssemblyName`/`RootNamespace` probe + normalized segment match; `git diff HEAD` on the example `.cs` empty; whitespace check | probe exit 0 and segment `nextorm_examples_mssql_adventureworks`; empty `.cs` diff; `git diff --check` exit 0; `rv3/{metadata.log,query.diff,diff-check.log}` |
| R236-A-BUILD | AC236-01 | example build, `clean`, solution build, solution build `--no-incremental` | all exit 0 with 0 Warning(s) / 0 Error(s); no unresolved generated namespace / `Alias` / `WithAlias`; `rv3/{example-build,clean,build,build-nonincremental}.{log,exit}` |

**r=3 → r=4 history (recorded):** the r=3 "unused import" premise was falsified by measurement —
removing `using NextORM.Generated.nextorm_examples_mssql_adventureworks;` produced 4 new errors
(CS0103 `Alias` ×3 at :62,54 / :63,122 / :64,121 and CS1061 `Alias`-dependent
`EntityBuilder<CustomerMetricRow>.WithAlias` at :62,44; build exit 1, 0 W / 4 E). The line was
reverted and the analyzer wiring replaces the removal.

**Ownership record (verbatim):** "T236 wires the source generator into
`examples/nextorm.examples.mssql.adventureworks/…csproj` (intentional overlap with T232's
footprint, `D232-…md:673`, `:874`; #232 commit `001f9c9c`; D232's unresolved `E232-13` packaging
evidence at `D232-…md:286`). T232 owns the query work; T236 must not alter or claim it."

## Variant matrix

| ID | Predicate | Closure |
|---|---|---|
| V236-01 | net10.0 consumer with Pkcs | test: patched assets, clean build, suites |
| V236-02 | source-generator netstandard2.0 / graph without Pkcs | guard: no new Pkcs injection or drift |
| V236-03 | direct SqlClient consumers incl. example | test: evaluated CPM, restore/build, SQL Server boundary |
| V236-04 | transitive consumers incl. EF SQL Server | test: per-project assets + suites |
| V236-05 | core tests' transitive Console 8.0.0 | guard + test: exact preservation + core suite |
| V236-06 | restore-only / incremental / fresh nonincremental | test: C1–C4 incl. clean archive |
| V236-07 | configured/default provider connectivity | test: integration, no skipped container providers |
| V236-08 | other runtime TFMs | guard: no public API / executable-code change; extra TFMs deferred until introduced |

## Decisions

- **Unit execution mode:** one sequential unit in the current worktree/branch; a shared
  central dependency contract forbids parallel edits. No worktree/branch/merge/push.
- **Docs plan:** internal status files only. No public API, dependency-usage or changelog
  change; `docs/**` and `docs/ru/**` untouched.
- **Perf measurement:** cached-path acceptance benchmark **not applicable** —
  `Directory.Packages.props` affects restore/package selection, not query/plan-cache/executor/
  materialization code.
- **Reconnaissance:** none; gather F3 established the safe versions. D1's evaluated-property
  + graph probes close the remaining evidence gaps.
- **Gaps:** (a) examples' evaluated CPM inheritance unproven → D1/D2 must probe it;
  (b) several baseline asset graphs absent → fresh restore + complete graph capture must
  close it before graph preservation is accepted.
- **Confidence:** high on advisory/version choice; graph completeness, inheritance,
  clean-source build and runtime compatibility unproven until their mandatory evidence passes.

## Risks / rollback

Risks: major-version compatibility, hidden central pins, examples inheritance, unavailable
audit/provider services. Rollback before commit: restore only the task-owned hunks from the
captured pre-edit file. After commit: `git revert <sha>` with authorization + corrective
status entry.

## Structured test scope (r=2, validated)

`artifacts/pdca/D236-build-advisories/rv2/scope.json` — `validate_inner_loop.py brief` exit 0.

- Inner subset (8 projects, `-c Debug --filter FullyQualifiedName~.`):
  `nextorm.core.tests`, `nextorm.sqlserver.tests`, `nextorm.postgres.tests`,
  `nextorm.mysql.tests`, `nextorm.mariadb.tests`, `nextorm.sqlite.tests`,
  `nextorm.clickhouse.tests`, `nextorm.entityframeworkcore.tests`.
- Boundary sweep (single): `tests/nextorm.integration.tests` with
  `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`.
- Scope files: `Directory.Packages.props`; `rebuild: "none"` (validator token only, see
  Plan revision history).

---

# ACT (D10) — closure

- **Checkpoint counts:** entry state "T236 implementation verified (r=5 / rv=4); ACT/evidence closure
  pending" → exit state **"T236 implementation verified AND ACT/evidence closed"**. `check` returned
  `FAIL — evidence/closure gate only` (AC236-01…05 met; AC236-06 unverified only because ACT had not
  run), routing **ACT-only corrections and evidence completion — no DO, no PLAN, no escalate**. The
  non-passing verdict is preserved verbatim in
  `artifacts/pdca/D236-build-advisories/rv4/check/verdict.json` and is **not** rewritten as a pass.
- **Plan revision:** r=5, iteration n=1/3, evidence_contract rv=4.
- **Authorized product paths (3, one added line each):**
  `Directory.Packages.props`, `src/nextorm.sqlserver/nextorm.sqlserver.csproj`,
  `examples/nextorm.examples.mssql.adventureworks/nextorm.examples.mssql.adventureworks.csproj`.
- **Commits:** product `c554d5e8`, closure `CLOSURE_SHA` (recorded by the follow-up ACT record commit). Both taken with explicit paths
  (`--only`, never `git add -A`), message prefix `#236`; argv/exit/SHA/path lists in
  `artifacts/pdca/D236-build-advisories/rv4/act/commits.log`. **Nothing was pushed** (`git push` never
  run).
- **Mechanism:** one versionless direct `<PackageReference Include="System.Security.Cryptography.Pkcs" />`
  in `src/nextorm.sqlserver` (CPM supplies **10.0.13**), flowing to consumers through the existing
  `ProjectReference` graph. No global property, no per-project guards.
- **Nuspec floor:** `<dependency id="System.Security.Cryptography.Pkcs" version="10.0.13" exclude="Build,Analyzers" />`.
- **Rejected — global transitive pinning.** `CentralPackageTransitivePinningEnabled=true` (r=2) plus four
  project-conditioned `Update` guards (r=3) was rejected at r=5: it caused recurring **unrelated** graph
  drift (2 projects/4 entries → then 8 entries in `tests/nextorm.alias.tests`) and the 13-project graph
  guard was blind to 12 of the 25 solution projects (the plan brief said 11; measured 12). Every guard
  was project-conditioned, so each round only moved the blast radius.
- **Verified test counts:** inner 8/8 projects exit 0, 0 failed, discovered 2529 / 948 / 976 / 454 / 336 /
  1747 / 733 / 126, 9 authored in-test skips; boundary integration sweep exit 0, total **3922**,
  **failed 0**, **198 skips — all authored capability skips** (`availability_skip_reasons: []`), with all
  four container-backed providers executing (PostgreSQL 931, SQL Server 831, MySQL 700, ClickHouse 290;
  MariaDB 117, SQLite 783).
- **Evidence corrections applied at ACT:** R236-05 artifact pointers replaced with the eight concrete
  `tests/nextorm.*.tests.log` files; R236-06 **erratum** appended (obligation = no required *provider*
  skipped; never claimed "zero skipped tests"; no zero-total-skip waiver); the 13/14-project
  resolved-graph snapshots labelled **supplementary only** while AC236-03's accepted instrument is the
  independently reconciled **25-project** `dotnet list package` inventory (17 entries = 13 × Pkcs
  `9.0.11 → 10.0.13` + 4 named pre-existing `nextorm*` entries, 0 other) — the instrument that caught the
  `alias.tests` drift the graph guard missed.

## Deferred risks and triggers (recorded, not closed)

| # | Deferred item | Why it is not closed | Trigger |
|---|---|---|---|
| 1 | `coverlet.collector 10.0.1` embeds Pkcs **10.0.8**, inside both affected ranges | Not repairable via the package graph; reachability unestablished; dev-only collector | Next collector upgrade/use — sooner if exposure is ever established. **No claim is made that all vulnerable binaries were eradicated** |
| 2 | Stale `src/nextorm.sqlserver/obj/windows/project.assets.json` still resolves Pkcs **9.0.11** | Local stale build artifact for the other host; not tracked | Fresh restore before any Windows use |
| 3 | `tests/nextorm.alias.poc/nextorm.alias.poc.csproj:7` sets `TreatWarningsAsErrors=false` | Project is not in `nextorm.slnx`, so it is outside this gate | Before solution inclusion or gating |
| 4 | `src/nextorm.core.sourcegenerator` audit mode `direct` (netstandard2.0) | Its transitive advisories are not surfaced | On its next dependency/TFM change |

## Closure statement

**"T236 done; #236 build blocker cleared; T232 may resume its own outstanding work."**

Scope limits of this claim: T232's query work is **not** claimed done, T232's remaining packaging
obligations (its unresolved `E232-13` evidence) are **not** claimed done, and the working tree is **not**
claimed clean (47 pre-existing modified + the task-external untracked entries remain untouched).

## Pre-existing reconciliation item (not fixed here)

`docs/specs/status/collection-1.0.9-todo.md` carries an internal mismatch that pre-dates T236: the T232
tasks-table row says `plan_revision: r=1`, while the file's appended T232 section reports
`plan r=3 / rv=3 / n=2/3`. This was **left as-is** (not "fixed" by inference) and is recorded as a
collection/T232 reconciliation item for T232 or the collection owner.
