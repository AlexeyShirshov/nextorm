# Collection 1.0.9-rc2 — CHECK (C) evidence contract

Status artifact for the integrated collection CHECK. Self-contained; all claims either cite a
`file:line` in a per-task status file or a staged raw log under
`artifacts/pdca/collection-1.0.9-rc2/C/`. Nothing here re-runs the suite; AC logs are already staged.

## 1. Scope & inputs

- **Collection id:** `1.0.9-rc2` (GitHub milestone #20; 26 open issues).
- **Branch / HEAD:** `1.0.9-rc2` @ `efd1b30a` (`MANIFEST.txt`: product HEAD `efd1b30a486d82b3b33c7491986d7f6968b5824f`).
- **Mode:** single group G1 in the **current worktree** — per `collection-1.0.9-rc2.md:22`, no group/task
  worktrees, no group branches, no `git merge --no-ff`; integration = the authorized commits on `1.0.9-rc2`.
- **Admission:** 24 admitted `ready`, 2 excluded-gap (D171/D172, still OPEN) — `collection-1.0.9-rc2.md:14-18`.
- **Terminal DO state:** 16 DONE + 8 INCOMPLETE + 2 excluded — `collection-1.0.9-rc2.md:108`.
- **Reopened C cause:** the prior integrated CHECK was **`blocked-for-evidence`**, not a product FAIL —
  `collection-1.0.9-rc2.md:9,92,110-111`: the pinned collection-level `rv` / mandatory-row+version ledger /
  priority+variant matrices / coder-scout provenance on the integrated tree were not delivered, and the
  AC1–AC5 artifact references (`/tmp/opencode/rc2-check/*`) were not attached to the verdict. This artifact
  supplies those rows. No integrated regression attributable to a DONE task was found; no corrective task opens.
- **Inputs:** collection status `docs/specs/status/collection-1.0.9-rc2.md`; per-task
  `docs/specs/status/rc2-*.md`; raw logs `artifacts/pdca/collection-1.0.9-rc2/C/` (+ `MANIFEST.txt`,
  `coverage-Summary.txt`).

## 2. Pinned rv table

`selected_variant` is `pdca-dotnet` for every row (`collection-1.0.9-rc2.md:40-68`). `rv` = pinned
evidence-contract revision actually used; `r` = plan revision.

| task | selected_variant | rv | r | source status file:`line` |
|---|---|---|---|---|
| D153 | pdca-dotnet | 1 | 1 | `rc2-153-public-extensibility-1.md:12` (rv=1), `:8` (r=1) |
| D154 | pdca-dotnet | 1 | 1 | `rc2-154-dto-public-ctors-1.md:8` (rv=1), `:7` (r=1) |
| D157 | pdca-dotnet | 1 | 1 | `rc2-157-register-finding-25-1.md:5` (r=1); rv=1 from `:170` |
| D159 | pdca-dotnet | 2 | 2 | `rc2-159-cte-direct-joins-1.md:9` (rv=2), `:8` (r=2), `:10` CHECK PASS |
| D162 | pdca-dotnet | 2 | 2 | `rc2-162-navigation-temp-tvp-fail-closed-1.md:13` (rv=2), `:12` (r=2) |
| D167 | pdca-dotnet | 1 | 1 | `rc2-167-csv-chunked-read-1.md:9` (rv=1), `:7` (r1) |
| D169 | pdca-dotnet | 1 | 1 | `rc2-169-dml-scope-hints-1.md:6` (r=1, rv=1) |
| D173 | pdca-dotnet | 1 | 1 | `rc2-173-projection-folding-member-1.md:6` (r=1, rv1) |
| D174 | pdca-dotnet | 1 | 1 | `rc2-174-identity-selector-cache-1.md:8` (r1), `:30` (rv1) |
| D176 | pdca-dotnet | 2 | 2 | `rc2-176-json-nested-1.md:262` (rv=2), `:404` (r=2) |
| D178 | pdca-dotnet | 1 | 1 | `rc2-178-json-enum-1.md:8` (r=1), `:10` (rv=1) |
| D179 | pdca-dotnet | 2 | 2 | `rc2-179-json-error-policy-1.md:10` (rv=2), `:7` (r2), `:94` (rv2) |
| D180 | pdca-dotnet | 2 | 2 | `rc2-180-json-stream-provider-conversion-1.md:9` (rv=2), `:8` (r=2), tail `:205` CHECK PASS r=2/n=2/rv=2 |
| D185 | pdca-dotnet | 2 | 2 | `rc2-185-bindentity-registration-1.md:8` (r=2, rv2) |
| D190 | pdca-dotnet | 1 | 1 | `rc2-190-join-whole-entity-1.md:7` (r1), `:36` (rv1), `:865` CHECK PASS r1/rv1 |
| D191 | pdca-dotnet | 1 | 1 | `rc2-191-provider-extensions-1.md:10` (r1), `:32` (rv1), `:562` CHECK PASS snapshot-n2 |

**Resolved discrepancies.**

- **D159** — status file `rc2-159-cte-direct-joins-1.md:8` `plan_revision r: 2`, `:9` `contract_revision rv: 2`,
  `:10` `phase: ACT/EXIT complete — CHECK PASS (r=2, n=1, rv=2)`. The collection table
  `collection-1.0.9-rc2.md:46` records `plan_revision r=1`. **Authoritative = the status file's own CHECK
  line: `r=2, rv=2`.** The collection-table `r=1` is a stale/inconsistent value (noted, not authoritative).
- **D180** — stale head block `rc2-180-...:8-11` reads `plan revision: r=2`, `rv: rv=2`, `iteration: n=1
  (DO not started)`, `phase: PLAN complete`. The tail (`:196-205`, `:210`) records DO at `r=2/n=2`, ACT
  finalized, and **`CHECK PASS r=2/n=2/rv=2`**. **Authoritative = tail: `r=2, rv=2`** (head metadata stale).
- **D157** — **per-task CHECK verdict: `missing`.** The status file's journal ends at `:222` (`D6 finding 25
  reconciled CLOSED`), with no CHECK/ACT verdict line. `git log -1 5978556f` = `#157 Reconcile register
  finding 25 (WITH TIES guard regression tests + docs timing)`; `rg -n "D157|#157|5978556f" docs/specs/status
  artifacts/pdca` finds no CHECK verdict elsewhere. Completion rests on `collection-1.0.9-rc2.md:45` +
  commit `5978556f`.

## 3. Mandatory-row-version ledger

Row IDs are cited from each status file's evidence contract; the satisfying evidence is the file's own
CHECK/ACT pointer. Row IDs grouped where the contract enumerates contiguous rows.

| task | mandatory rows (rv) | satisfying evidence (command/exit/artifact) |
|---|---|---|
| D153 | `EV-01..EV-06` (rv1; `:174-181`) | CHECK r1/n2 PASS, AC-01..06 + all EV rows closed — `:217`; artifacts `TestResults/rc2-153-1/`; commit `8401ca51` |
| D154 | `EV-154-01..15` (rv1; `:169`) | CHECK r1/n1 PASS, AC-01..10 + EV-154-01..15 closed — `:169`; artifacts `TestResults/rc2-154-1/`; commit `5d72a79` |
| D157 | `E01..E09` (rv1; `:176-184`) | DO evidence table `:203-217` (C1 build 0/0 exit 0; C2 7/7, C3 5/5, C4 791/791, C5 712/712, C9 no-match exit 1, C10); **per-task CHECK verdict `missing`**; commit `5978556f` |
| D159 | `E159-01..E159-21` (rv2; `:172-192`) | CHECK re-gather `:331+` (M1–M4 4/4 killed, E159-15; coverage line 87.8/branch 80.4); CHECK PASS r=2/n=1/rv=2 `:10`; commit `078a7a2d` |
| D162 | `E162-01..E162-18` (rv2; `:496`) | CHECK PASS rv=2, R162-01..07 met, all rows closed/superseded/N-A — `:22`, `:57`; artifacts `/tmp/nextorm-D162-r2/`; commit `ae946293` |
| D167 | `EV-01..EV-21` (rv1; `:561+`) | CHECK PASS r1/N=1/rv=1 — `:12`, `:636`; boundary `9760/9562/198/0`, CSV integration 69/69 across 6 providers; artifacts `artifacts/pdca/D167/r1/`; commit `0b4241b9` |
| D169 | `E169-01..E169-12` (rv1; `:279`) | CHECK PASS r=1/rv=1/n=2 — `:58`; boundary `9785/0/9587/198`, coverage 88.3/80.3; artifacts `/tmp/nextorm-rc2-169-r1/`; commit `bbc3b47e79e2659f52ccfbbcd489ae70b3df6c9a` |
| D173 | `E173-00..E173-13` (rv1; `:217`) | CHECK PASS r1/rv1/n2 — `:304`; unit 5802/0/1, integration 3337/0/197, coverage 88.1/80.2, perf 7/7; artifacts `/tmp/nextorm-D173-r1/`; commit `29e794fd` |
| D174 | `E174-01..E174-10` (rv1; `:356`) | CHECK r1/n2 closure — `:10`, `:454`; core 1767/0/0, sqlite 1161/0/1, integration 17/17 ×4 providers; artifacts `TestResults/D174/`; commit `6bc4ae41` |
| D176 | `E176.01..E176.14` (rv2; `:301`) | CHECK PASS r=2/rv=2 `:404`; 6/6 mutants killed, coverage 86.8/79.3, container 3511/0/197; evidence `docs/specs/status/evidence/rc2-176/`; commit `f760e5521f7630e56a2841e6b1e097aaae92273d` |
| D178 | `E178-01..E178-12` (rv1; `:186`) | CHECK PASS r=1/n=2/rv=1, T01–T17 closed — `:12`, `:232`; boundary 9548/9350/0/198, enum 30/30 across 6 providers; artifacts `artifacts/pdca/178/r1/`; commit `31c9e9d9...` |
| D179 | `E179-01..E179-09` + `E179-N01/N02` (rv2; `:92,:99-100`) | CHECK PASS r2/n2/3 `:11`, `:112`; R179-01..07 met; boundary 9656/0/6837/2819, coverage 86.8/79.3; commit `#179 …` |
| D180 | `E180-01..E180-18` (rv2; `:157-176`) | CHECK PASS r=2/n=2/rv=2 — `:205`, `:210`; red→green `n2.red.log` (2 failed exit 2) → `n2.build.log` 0/0, core 203, sqlite 125; evidence `TestResults/D180/r2/rv2/`; commit `93714a8e` |
| D185 | `E00..E16` (rv2; `:102-140`) | CHECK PASS r=2/n=2 — `:164`; core 87/0/0, sqlite 27/0/0, integration 29/29, coverage changed-lines 100% (core 86.9/79.3), perf 7/7 +12.3%, M1–M4 detected; evidence `/tmp/nextorm-D185-r2/`; commit `0d0b7fab` |
| D190 | `EV190-{PREFLIGHT,SCOPE,RED,ROOT,NULL,REGRESSION,CACHE,SHARED,TEMP,SQL,INTEGRATION-DIRECT,INTEGRATION-REGRESSION,INFRA,BUILD,COVERAGE,BRANCH,MUTATION,PERF,AUDIT,DOCS,REGISTERS}` (rv1; `:517-737`) | CHECK PASS r1/rv1 evidence-completion `:865`; boundary 9235/0/198, coverage 88.1/80.1, integration 6 providers 0 skipped; mutation disclosed as `not usable`; commit `1ff47762` |
| D191 | `E01..E19` (rv1; `:442-461`) | CHECK PASS snapshot-n2 `:562`; E01–E19 ledger `$EV/ledger.json`, allocation gate exit 0; evidence `artifacts/pdca/rc2-191/r1/`; commit `e25f558e` |

**AC1–AC5 mapping** (see §5): every DONE task's contract includes build/unit/integration/coverage/docs
obligations satisfied by the corresponding AC artifact in §5; the per-task rows above are the granular
form. No row was fabricated; unresolved rows are in §8.

## 4. Priority + variant matrix

| task | selected_variant | priority class/rows | variant matrix | rows closed |
|---|---|---|---|---|
| D153 | pdca-dotnet | `:188` Priority matrix | y — `:44` | test/guard/deferred per matrix `:46-60` |
| D154 | pdca-dotnet | `:125` Priority matrix | y — `:41` | per matrix |
| D157 | pdca-dotnet | `:151` P1 by construction; P2 baseline | y — `:60` | 11 closed incl. deferred with triggers `:75-78` |
| D159 | pdca-dotnet | all P1 (`:166` bindings) — no separate priority heading | y — `:59` | V159-01..17, no row deferred `:83` |
| D162 | pdca-dotnet | `:246` + `:606` (rv=2, P1) | y — `:219` | per matrix, deferrals tracked |
| D167 | pdca-dotnet | `:426` Матрица приоритетов | y — `:262` §8 (provider×mode matrix) | per matrix; EV rows `:561+` |
| D169 | pdca-dotnet | `:193` variant+priorities | y — `:193` | V01–V15; V11 P2 deferred, V15 unreachable |
| D173 | pdca-dotnet | `missing` — no explicit priority matrix; P1/P2 inferred only | y — `:66` | per matrix |
| D174 | pdca-dotnet | `:255` Priority matrix | y — `:213` | per matrix + `:194` |
| D176 | pdca-dotnet | `:196` Priority matrix and risks | y — `:166` (Closed variant matrix) | all rows closed |
| D178 | pdca-dotnet | `:90` Priority matrix | y — `:67` | T01–T17 all closed |
| D179 | pdca-dotnet | P1 in contract `:92` — no separate priority heading | y — `:38` (V01–V21) | V01–V21, V21 deferred→#180 |
| D180 | pdca-dotnet | `:180` Priority classes (P1) | y — `:61` (V01–V15) | per matrix |
| D185 | pdca-dotnet | P1/P2 in rows `:96-100,:107+` — no separate priority section | y — `:49` | per matrix |
| D190 | pdca-dotnet | `:431` Приоритеты и design checklist | y — `:109` Матрица вариантов | per matrix |
| D191 | pdca-dotnet | `:245` Priority matrix | y — `:218` Test strategy и variant matrix | E01–E19 closed |

## 5. AC1–AC5 references

Raw paths are under `artifacts/pdca/collection-1.0.9-rc2/C/`. Freshness rule from `MANIFEST.txt`:
`git diff --name-only e67aa5f8..HEAD` = `docs/specs/status/collection-1.0.9-rc2.md` only ⇒ docs-only
diff, so **AC2/AC3/AC4 logs reused**; **AC1/AC5 re-run fresh** on HEAD `efd1b30a`.

| AC | raw path(s) | key evidence |
|---|---|---|
| AC1 build | `build-debug.log`, `build-release.log` | each tail: `Build succeeded. 0 Warning(s) 0 Error(s)`; exit 0; fresh on `efd1b30a` (`MANIFEST.txt`) |
| AC2 unit | `unit-core.log` (1953), `unit-sqlite.log` (1310 + 1 skip), `unit-sqlserver.log` (754), `unit-postgres.log` (844), `unit-mysql.log` (323), `unit-mariadb.log` (242), `unit-clickhouse.log` (607), `unit-clickhouse.extensions.log` (31), `unit-alias.log` (46), `unit-entityframeworkcore.log` (126), `unit-publicextensibility.log` (10) | 11 projects; Σsucceeded 6246 + 1 skipped = **6247 / 0 failed / 1 skipped**; the 1 skip is the SQLite LOB probe (`unit-sqlite.log` `skipped: 1`) |
| AC3 integration | `integration.log`, `integration-rerun.log`, `integration-results.xml` | tail: `nextorm.integration.tests Total: 3555, Errors: 0, Failed: 0, Skipped: 197, Not Run: 0` (both runs exit 0); providers RAN = PostgreSQL, SQL Server, MySQL, ClickHouse, SQLite, MariaDB; `grep -c "is not available"` = **0**, so the 197 are per-test capability skips (e.g. `This provider cannot run a CTAS batch.`), not provider-unavailable |
| AC4 coverage | `coverage-Summary.txt`, `coverage-collect.log`, `reportgenerator.log` | Line **86.8%** (49718/57237); Branch **79.4%** (26852/33809) — both ≥ 85 / 75; reportgenerator exit 0 |
| AC5 docfx | `docfx.log` | tail: `Build succeeded with warning. 2 warning(s) / 0 error(s)`; exit 0; 2 pre-existing duplicate `AnalyzerReleases` md warnings |

## 6. Provenance

Per-class producing role and origin (all lines in the per-task status files unless noted):

| class | producer(s) | origin |
|---|---|---|
| D153 | coder (plan persisted by coder) | `rc2-153-...:206-219`; `TestResults/rc2-153-1/` |
| D154 | coder | `rc2-154-...:161-171`; `TestResults/rc2-154-1/` |
| D157 | coder (DO); **CHECK role absent** | `rc2-157-...:192-222`; `/tmp/nextorm-D157-r1/` |
| D159 | coder (plan/DO) + check (re-gather) | `rc2-159-...:217-345`; `TestResults/pdca/D159/r1/{n1,n2,n3}` |
| D162 | planner (r1→r2 replan) + coder DO + check | `rc2-162-...:46-57`; `/tmp/nextorm-D162-r2/` |
| D167 | coder + check ledger | `rc2-167-...:620-636`; `artifacts/pdca/D167/r1/` |
| D169 | **authored by planner, persisted by coder** | `rc2-169-...:12` (`PLAN(r=1) authored by planner; persisted by coder`); `/tmp/nextorm-rc2-169-r1/` |
| D173 | coder + check | `rc2-173-...:217-304`; `/tmp/nextorm-D173-r1/` |
| D174 | owner streams **SCOUT / DO-CORE / DO-TEST / DO-DOC / DO-PERF / CHECK** | `rc2-174-...:356-369`; `TestResults/D174/` |
| D176 | coder + check | `rc2-176-...:301-318`; `docs/specs/status/evidence/rc2-176/` |
| D178 | coder + check | `rc2-178-...:186-235`; `artifacts/pdca/178/r1/` |
| D179 | coder (tests+docs) + check | `rc2-179-...:105-120`; `/tmp/d179_r2_*.log` |
| D180 | **DO `coder` / CHECK `check` / facts `scout`** | `rc2-180-...:155` (`DO producer coder, CHECK evidence owner check, D180.1 facts by scout`); `TestResults/D180/r2/rv2/` |
| D185 | coder + **E07 check** | `rc2-185-...:158`; `/tmp/nextorm-D185-r2/` |
| D190 | **author `coder` / certifier `check`** | `rc2-190-...:791-912`; `artifacts/pdca/rc2-190/r1/` |
| D191 | coder (relocation) + check (snapshot-n2) | `rc2-191-...:544-562`; `artifacts/pdca/rc2-191/r1/` |

Required named examples are present: D153 coder-persisted; D174 SCOUT/DO-CORE/DO-TEST/DO-DOC/DO-PERF/CHECK;
D180 DO coder / CHECK check / facts scout; D185 coder + E07 check; D190 author coder / certifier check;
D169 authored by planner, persisted by coder.

## 7. Non-green characterization

| task | classification | preserved patch / artifact |
|---|---|---|
| D151 | terminal STOP — CHECK evidence-certification `fail-by-evidence` (r1 n1/2/3 + r2 n1); no product defect; no r=3 | `docs/specs/status/rc2-151-evidence/D151-STOP-incomplete.patch` — **exists** |
| D160 | terminal `incomplete`, r=4 STOP (R02 over-preservation family); set aside by user direction | `docs/specs/status/rc2-160-evidence/D160-STOP-incomplete.patch` — **missing in this worktree**; **exists on branch `wip/d160-incomplete`** (also `rc2-160-join-alias-mixing-1.md` is on the branch only) |
| D170 | terminal `incomplete`, r=3 STOP (R170-02/R170-03 unmet) | `docs/specs/status/rc2-170-evidence/D170-STOP-incomplete.patch` — **exists** |
| D175 | terminal `incomplete`, r=1 STOP (rv1 evidence-completeness / row-bound ledger provenance; 4× CHECK fail) | `docs/specs/status/rc2-175-evidence/D175-STOP-incomplete.patch` — **exists** |
| D177 | terminal `incomplete`, CHECK n=3 completeness/variant gate FAIL (no P1 product defect); decision (c) STOP | `docs/specs/status/rc2-177-evidence/D177-INCOMPLETE.patch` — **exists** |
| D184 | blocked — evidence-contract gap (product gates green); CHECK invoked STOP on recurrence | `artifacts/pdca/rc2-184/D184-blocked.patch` — **exists**; artifacts `artifacts/pdca/rc2-184/{r1,r2}/` |
| D188 | `incomplete` — CHECK `fail-for-pass`, predecessor-blocked (D189 incomplete, EC03 unmet); non-zero partial delivered | `docs/specs/status/rc2-188-evidence/D188-nonzero.patch` — **exists** |
| D189 | `incomplete` — r=4 paired A/B proven regression (`case3 L=1.0519 > 1.05`); functionally confirmed, E09 uncloseable | `docs/specs/status/rc2-189-evidence/D189-STOP.patch` — **exists**; r4 evidence `docs/specs/status/rc2-189-evidence/r4-ab/` |
| D171 | excluded-gap, `revisit on demand`; issue OPEN | none (not-actionable-now) — `collection-1.0.9-rc2.md:16,53` |
| D172 | excluded-gap, awaiting-consumer; issue OPEN | none (not-actionable-now) — `collection-1.0.9-rc2.md:17,54` |

## 8. Honest gaps / open rows

| # | gap | status | where declared |
|---|---|---|---|
| G1 | D157 per-task CHECK verdict | **`missing`** — journal ends at `:222` (D6); no CHECK/ACT line in the file or elsewhere (`rg` over `docs/specs/status artifacts/pdca`) | `rc2-157-register-finding-25-1.md`; `collection-1.0.9-rc2.md:45` |
| G2 | D160 status file in this worktree | **`missing`** (`docs/specs/status/rc2-160-join-alias-mixing-1.md` absent); **exists on `wip/d160-incomplete`** | `collection-1.0.9-rc2.md:32,47` |
| G3 | D160 preserved patch in this worktree | **`missing`** — declared at `docs/specs/status/rc2-160-evidence/D160-STOP-incomplete.patch`, **not present in this worktree**; present on branch `wip/d160-incomplete` (branch HEAD `214c1323`, tip `40b1a159`) | `collection-1.0.9-rc2.md:32,47` |
| G4 | D173 explicit priority matrix | **`missing`** — no priority-matrix heading; only P1/P2 inferred from acceptance/evidence text | `rc2-173-projection-folding-member-1.md` |
| G5 | D179 explicit priority section | **`missing` as a heading** — priority stated inline as "Все P1" in the contract `:92` | `rc2-179-json-error-policy-1.md:92` |
| G6 | D185 explicit priority section | **`missing` as a heading** — P1/P2 recorded per evidence row `:96-100,:107+` | `rc2-185-bindentity-registration-1.md` |
| G7 | D159 collection-table plan revision | **inconsistent** — collection says `r=1` (`:46`) vs status `r=2` (`:8`); authoritative = status `r=2/rv=2` | `collection-1.0.9-rc2.md:46`; `rc2-159-...:8-10` |
| G8 | D180 head metadata | **stale** — head `r=2/n=1/PLAN complete` vs tail `r=2/n=2/rv=2/CHECK PASS`; authoritative = tail | `rc2-180-...:8-11` vs `:205,:210` |
| G9 | D154 issue close | **incomplete outside the cycle** — issue #154 close blocked by GitHub API HTTP 500; retry pending | `collection-1.0.9-rc2.md:102` |
| G10 | Push-rule violation | remote `origin/1.0.9-rc2` = `40b1a159`; local ahead 35 / behind 16; user must resolve remotely (never push) | `collection-1.0.9-rc2.md:35,113` |

## 4b. Per-row priority/variant disposition ledger (all 16 DONE tasks)

Scope: the 16 DONE collection tasks (D153, D154, D157, D159, D162, D167, D169, D173, D174, D176,
D178, D179, D180, D185, D190, D191). Every priority and row cites its source status file
`file:line`; dispositions are `test` / `guard` / `deferred`+trigger / `N-A` as recorded, else
`missing`. Evidence binds to the matrix closure site plus the task CHECK/ACT verdict and artifact
root (named test symbols where the file names them). No suite was re-run for this ledger; every
pointer is read from the committed status files. Matrices that carry no explicit row IDs use their
axis text as the row label (`no id in source`).

### 4b.1 Per-task priority + source

| task | priority (recorded) | source `file:line` |
|---|---|---|
| D153 | P1 (all acceptance rows P1 by construction) | `rc2-153-public-extensibility-1.md:188-192` |
| D154 | P1 (by requirement/exec-path/invariant); P2 supporting docs+register | `rc2-154-dto-public-ctors-1.md:125-126` |
| D157 | P1 (AC1–AC8 + evidence rows); P2 baseline comparison | `rc2-157-register-finding-25-1.md:151` |
| D159 | P1 (all acceptance criteria; class-table rows P1) | `rc2-159-cte-direct-joins-1.md:28,166,244` |
| D162 | P1 (task invariant / exec-path / regression); P2 docs+bookkeeping | `rc2-162-navigation-temp-tvp-fail-closed-1.md:246-253` |
| D167 | P1 (REQ-01..14 by construction) | `rc2-167-csv-chunked-read-1.md:426-435` |
| D169 | P1 (R169-01..04 + DML rows); P2 (V11–V14); P1-conditional (V15) | `rc2-169-dml-scope-hints-1.md:215` |
| D173 | P1 (R173-01..05, R173-06/07/09); R173-08 completion gate; latent-P2 label does not downgrade | `rc2-173-projection-folding-member-1.md:171-173` |
| D174 | issue severity P2; evidence rows E174-01..09 P1; E174-10 P2 mandatory | `rc2-174-identity-selector-cache-1.md:257-271` |
| D176 | P1 by construction (R176.01–08) | `rc2-176-json-nested-1.md:198-208` |
| D178 | P1 per row; P2 supplemental prose/style | `rc2-178-json-enum-1.md:90-104` |
| D179 | P1 (all evidence rows) | `rc2-179-json-error-policy-1.md:92` |
| D180 | P1 by construction (streaming-terminal invariants + exec path) | `rc2-180-json-stream-provider-conversion-1.md:180-182` |
| D185 | P1 (E00–E16 evidence/stream rows); P2 design-guard (structural sealedness) | `rc2-185-bindentity-registration-1.md:96-100,107-128` |
| D190 | P1 by construction (listed EV190 rows) | `rc2-190-join-whole-entity-1.md:431-443` |
| D191 | E03–E07/E10–E14 P1; E01–E02/E15/E18 P1; E08–E09 P1; E16–E17 P2 | `rc2-191-provider-extensions-1.md:245-256` |

### 4b.2 Per-task rows (axis → disposition → evidence)

Format: `row-id/axis → disposition — evidence pointer`. Artifact roots are per-task as recorded.

**D153** — matrix `rc2-153-...:44-60`; CHECK PASS r1/n2 `:217`; root `TestResults/rc2-153-1/` (consumer 10/10, PG 37/37, CH 59/59, integration 51/51 — `:211`).
- PG subclass ctor (parameterless + `Version?` incl. `null`) → test — new consumer tests, `TestResults/rc2-153-1/evidence.json`.
- CH subclass existing ctor → test — new consumer tests (same).
- PG custom renderer accepts → test — public delegation consumer test.
- CH custom renderer accepts → test — consumer test.
- PG/CH renderer declines (`CanRender=false`) → test — portable result; `Render` not called.
- PG/CH subclass returns `null` → test — portable strategy assertion.
- Public interface impl outside production assemblies → test — consumer both providers.
- Consumer without IVT → guard — assembly-metadata guard + build (`:55`).
- Default `Instance`, native-eligible input → test — existing provider SQL-gen + native integration regressions.
- Default renderer, unsupported shapes → test/guard — reuse established guard/fallback tests.
- Existing nullable/value/ref payload + tie cases → test — reuse native regressions.
- Direct DTO construction → deferred → #154 (`:59`).
- Renderer laziness / callback counts → deferred → #155 (`:60`).

**D154** — matrix `rc2-154-...:41-63`; CHECK PASS r1/n1 `:169`; root `TestResults/rc2-154-1/` (external mariadb 218/0, core 39, PG 37, CH 59, integration PG 16/CH 35 0-skipped — `:164`); EV-154-01..15 `:137-151`.
- Column global/grouped/composite/empty; column ref vs value `ClrType`; column 3 bools × 8 combos × 2 CLR; description eager lists both `IsMax`; request shapes both `IsMax` × every `KeywordCase`; internal factory no-call; non-forcing prep; payload forces once; borrowed-identity; valid overlap; empty/independently-sized; whole-entity ref/value path; PG/CH rendering+plan-cache → **test** — external suite `ExtremeRowPublicConstructionTests`, `TestResults/rc2-154-1/{external-tests,core,postgres,clickhouse}.log`.
- Public eager description payload validation; null required refs one-at-a-time; null collection element; `SourceSql` null/empty/whitespace; aliases null/empty/whitespace-only; `KeywordCase` undefined numeric; valid DTO rejected by `CanRender` / supported passed to `Render` → **test+guard** — same logs.
- Later/concurrent mutation of borrowed lists → **guard** — caller contract (documented prohibited; `:63`).

**D157** — matrix `rc2-157-...:62-78`; DO evidence `:203-217`; per-task CHECK verdict `missing` (`:222`, gap G1).
- PG DISTINCT×WITH TIES both call orders → test — `WithTies_WithDistinct_InBothCallOrders_ShouldRejectCombination` (PG `SqlGenerationTests.cs:3845`); C2 7/7.
- SQL Server DISTINCT×WITH TIES both orders → test — same method name (SS `:2675`); C3 5/5.
- PG DISTINCT ON×WITH TIES both orders → test — `WithTies_WithDistinctOn_InBothCallOrders_ShouldRejectCombination` (PG `:3863`); C2.
- Neither modifier, valid/offset → test — existing `WithTies_ShouldUseFetchFirstWithTies` / `WithTies_ShouldUseTopWithTies` (AC4).
- WITH TIES without positive limit → guard — existing `SqlBuilder.cs:78-79`; `WithTies_WithoutLimit_ShouldThrow`.
- WITH TIES unsupported dialect → guard — existing `SqlBuilder.cs:75-76`.
- DISTINCT + DISTINCT ON → guard — `EntityBuilder.cs:1801-1802,1820-1821` (outside finding).
- `HasWithTies=false` short-circuit → guard — `SqlBuilder.cs:81`.
- WITH TIES missing/incompatible ORDER BY → deferred (ordering-validation task).
- DISTINCT ON + WITH TIES → deferred (provider support task).
- In-memory WITH TIES semantics → deferred (separate task).
- Null selector/default entity/value-vs-ref entity → deferred (`:78`).

**D159** — matrix `rc2-159-...:59-81`; r=2 rows `:390-391`; CHECK PASS r2/n1/rv2 `:445`; commit `078a7a2d`; root `TestResults/pdca/D159/{r1,r2}/`.
- V159-01 seven × generic receiver × first join → test (`:65`).
- V159-02 seven × non-generic/TableAlias receiver × first join → test; `TypedCte_TableAliasReceiver_AllFourteenOverloads_CompileAndBuild` (`:290,:319`).
- V159-03 seven × receiver arities 2–7 × continuation → test (`:67`).
- V159-04 arity-eight; positional-after-alias → guard+test; `Positional_join_cannot_follow_an_alias_join` (`:361`).
- V159-05 seven × positional/core × concise/configured/null options → test; `Direct_typed_cte_join_forwards_the_options_callback` (`:360`).
- V159-06 predicate/CTE null guards → guard+test; `TypedCte_DirectJoin_NullPredicate_ShouldFailBeforeSourceWork` (`:339`).
- V159-07 seven × alias first/subsequent × named/anonymous → test (`:71`).
- V159-08 alias factory receiver + required ref marker → guard+test; M2 kill (`:340`).
- V159-09 `Cte<T>`/`EntityBuilder<T>`/`QueryCommand<T>` resolution → test (`:73`).
- V159-10 `CteReference<T>`; unrelated same-name; untyped null → guard/compile test; `Unrelated_user_type_named_Cte_is_not_recognized_as_a_Nextorm_CTE` (`:341`).
- V159-11 projections supported/null/default → test; unsupported → guard (`:75`).
- V159-12 Table→CTE, CTE→CTE, cross-context, repeated CLR, different projections → test (`:76`).
- V159-13 dependency chain / repeated descriptor / duplicate-name conflict → test (`:77`).
- V159-14 six providers × seven → test/guard (`:78`).
- V159-15 five providers × seven executable/unsupported → integration test (`:79`).
- V159-16 seven × filtered CTE; repeated exec/shared command; alias-cache/refusal → test (`:80`).
- V159-17 alias ceiling / old markers / correlated → guard+regression test (`:81`).
- E159-R2-SA Semi/Anti keep metadata fallback → test; `TypedCte_TableAliasReceiver_SemiAntiJoin_ShouldKeepEntityMetadataFallback` (`:390`).
- E159-R2-TVF TableAlias generic join resolves TVF via `JoinSourceResolver` → test; `TypedCte_TableAliasReceiver_TableValuedFunctionSource_ShouldUseJoinedSource` (`:391`).

**D162** — matrix `rc2-162-...:219-244`; priority `:246-253`; DO ledger `:39-43`; CHECK PASS rv2 `:22,:57`; root `/tmp/nextorm-D162-r2/`.
- V01 mapped entity nav + scalar projection (SQLite prep) → test — positive nav control.
- V02 temp root typed column projection (SQLite/PG/MySQL/MariaDB) → test — `ImplicitNavigationV32TempTableTests` + provider guards (`:42`).
- V03 temp root whole-alias/entity → guard — support/rejection (`:227`).
- V04 navigation after temp boundary → guard — non-expressibility / explicit rejection (`:228`).
- V05 temp request SQL Server/ClickHouse → guard — `TempTableMaterializationRejectionTests` (`:42,:229`).
- V06 TVP root explicit columns (SQLite/PG/MySQL/SS/CH) → test+guard — `TableValuedParameterTests`, `TableValuedParameterBindingTests` (`:43,:230`).
- V07 TVP root whole entity/nav → guard (`:231`).
- V08 TVP root MariaDB → guard/test after inspection (`:232`; `:43` conformance).
- V09 ordinary `TableAlias` root → guard+regression test (`:233`).
- V10 derived mapped source → test (`:234`).
- V11 `FromSql` root → guard/regression test (`:235`).
- V12 sync + async materialization/read → test (`:236`).
- V13 temp/TVP navigation in-memory → guard or deferred (trigger new public route) (`:237`).
- V14 null/default source/payload; empty input → test/guard (`:238`).
- V15 cache flags; repeat after rejected → test (`:239`; V10 cache test `:45`).
- V16 typed navigation over temp/TVP → deferred (trigger: approved implementation) (`:240`).

**D167** — matrix `rc2-167-...:290-309` (description rows, no ids); CHECK PASS r1/N=1/rv=1 `:12,:636`; root `artifacts/pdca/D167/r1/`; defect history `:21-28`.
- NULL; non-null empty → test — unit + SQLite/integration.
- Lengths 1/2/3/4; `B±2`; `2B+1`; 1 MiB; `B=12288` → test — exact-byte unit/large-field tests.
- Short reads 1/2/irregular → test — carry/offset unit tests.
- Two BLOBs, scalar before/between/after → test — ordinal probe + SQLite/PG/SS.
- Chunked / buffered / unknown-non-sequential mode → test — chunked only with provenance; buffered legacy kept.
- PG / SQL Server / SQLite → test — real chunked runs.
- MySQL / MariaDB / ClickHouse → test — real buffered compatibility runs; chunking not claimed.
- Transform absent/identity/other/`null` result → test; transform → buffered guard.
- Direct storage byte[] / converter / typed mapping → test/guard (`CsvTypedColumnMappingTests`).
- Standard delimiter/quoting/NULL/newline/header → test.
- Delimiter intersects Base64 alphabet → buffered policy guard + test.
- Both surfaces × sync/async × default/custom options × params → test — REQ-06 loop-back (defect `:28`); new SQLite surface/params test `artifacts/pdca/D167/r1/D6/`.
- CT default/pre-cancel/cancel between chunks/provider error/sink error → test.
- Destination open; flush success/failure; pooled buffers returned → test.
- SQLite DISTINCT/JOIN/aggregate/projection without BLOB → test — locator not added.
- `ToStream`/`ToTextReader`/`ToDataReader` → test — LOB regression.
- `byte[]` reference/`null`; nullable/default options → test.
- Extend streaming to `capability=false` providers → deferred (trigger: confirmed capability + provider tests).

**D169** — matrix `rc2-169-...:197-215`; priority `:215`; CHECK PASS r=1/rv=1/n=2 `:58`; root `/tmp/nextorm-rc2-169-r1/`; tests `:26-30,44-45`.
- V01 PG DELETE/UPDATE scope hints → test P1 — inline-comment SQL tests (`DeleteJoin_DmlScopeHint_ShouldComposeWithJoinHintInOneComment` PG `:335`; `UpdateJoin_DmlScopeHint_...`).
- V02 SQLite nonempty scope hints → test P1 — rejection (`..._ShouldThrowBecauseNotSupported`), hint-free guards (`:27`).
- V03 SQL Server target + join → test P1 — structural `WITH` tests (`DeleteJoin_DmlScopeHint_ShouldMergeWithJoinTableHint` `:151`).
- V04 MySQL DELETE/UPDATE scope hints → test P1 — inline tests (`:87`).
- V05 ClickHouse capability → guard P1 — existing DML rejection / scope-hint rejection (`:27`; green 1/1).
- V06 no-hints / null/default / empty list → guard P1 — byte-identical guards.
- V07 structural providers + explicit joined-table hints → test P1.
- V08 inline providers + joined/statement hints → test P1.
- V09 mapped ref/value/nullable columns in JOIN/SET → guard P1.
- V10 cached/prepared hinted→unhinted→hinted → test P1 — `UpdateJoin_DmlScopeHint_SameContextRepeatedCalls_ShouldNotLeakCachePolicy` SS `:111`; `Source.Cache==true`.
- V11 explicit target-only table hints → deferred P2 — unchanged (`:29,:81`).
- V12 INSERT…SELECT → guard P2 (`:210`).
- V13 APPLY/PIVOT → deferred P2 (`:211`).
- V14 temporal/index hints → deferred P2 (`:212`).
- V15 temp-table/TVP branches → P1-conditional; proven non-reachable (`:29,:213`).

**D173** — matrix `rc2-173-...:66-86` (description rows, no ids); priority `:171-173`; DO/loop-back `:283-297`; CHECK PASS r1/rv1/n2 `:304`; root `/tmp/nextorm-D173-r1/`.
- null/null member; null/non-null both orders; equal non-null member identity; different non-null members (primary); same name different declaring types; same slot different `EntityType`; same type different slot; scalar vs entity item; both scalar columns; ctor-position `Member==null`; value/ref-type mapped result → **test** — `SelectExpressionPlanEqualityComparerTests`, `RowMapperFactoryStreamingKeyTests`, `RowMaterializerBuilderTests` (`:286,:294,:297`).
- Production `Item1`/`Item2` tuple bindings → **guard** + existing tests (`:82`).
- Existing streaming/non-streaming flag → **guard** — `RowMapperFactoryStreamingKeyTests` (`:84`).
- Other select metadata/converters/defaults/duration/provider type → **guard** — existing tests + diff review (`:85`).
- SQL providers → **test** — common integration boundary (`:86`; integration 3337/0/0, 197 capability skips `:287`).

**D174** — matrix `rc2-174-...:215-231`; priority `:257-271`; partial plan risk `:392`; CHECK PASS r1/n2 `:461`; root `TestResults/D174/`; tests `JoinIntoIdentitySelectorCacheTests` (`:420`).
- Registered ref type, one key → test.
- Registered type, composite keys → test (`JoinIntoSpec.cs:672-699`).
- Multiple distinct keyed types → test.
- Missing metadata → test.
- Registered keyless type → test.
- Empty cache / repeated clear → test.
- Same CLR type, changed single-key config → test.
- Key property null value → test (else guard from mapping validation) (`:224`).
- Value-type entity → test (else guard from constraints) (`:225`; loop-back `:445`).
- Null entity to compiled selector → guard (`:226`).
- Concurrent build/clear → test (`ConcurrentBuildAndClearRemainSafe`).
- Spec constructor callers → guard (`:228`).
- Stitch/execution callers → test + guard (`:229`).
- Provider independence → test + guard — SQLite/PG/SS dialect suites (`:230`).
- MySQL/MariaDB/ClickHouse-specific → guard — no provider production change (`:231`).
- Deferred findings (`:112-115`): unbounded growth, atomic clear/build, `Count` observer, Stryker automation — all **deferred** with triggers (no matrix row).

**D176** — matrix `rc2-176-...:168-194`; priority `:198-208`; CHECK PASS r2/rv2 `:19,:404`; root `docs/specs/status/evidence/rc2-176/`; rows E176.01..E176.14 `:303-318`.
- Existing scalar/flat + nullable/default/value/ref leaves; nested anonymous `new`; named/member-init; null-valued conditional nested (translatable); `Projection<T1,T2>` slots; scalar/entity + scalar/scalar slots; inner/outer joins matched/unmatched; same name different scopes; native rank-one `T[]`; jagged native arrays; null/empty arrays + nullable elements; `byte[]` incl. inside jagged; cancellation/reader/destination failure/disposal → **test** (`:170-183,:192`; E176.02/03/04/13).
- Opaque factory/method object or unresolvable ctor member → **guard**, reject before writing (`:174`; E176.14).
- Duplicate effective name within one object → **guard + test** (`:178`).
- Multidimensional arrays; unsupported array element types; `List<T>`/`IEnumerable<T>`/dictionaries/converter-backed collections; excessive depth/unsupported recursive shape → **guard + test** (`:184-185,:191`).
- Providers without native array source → **guard / capability record** (`:190`).
- Enum/new converter types → **guard**; deferred → #178 (`:186`).
- Child-collection queries → **guard**; deferred → #172 (`:187`).
- New serializer options/naming policy → **deferred** → #177 (`:188`).
- DB-side JSON → **guard** against accidental route; deferred (explicit scope approval) (`:189`).

**D178** — matrix `rc2-178-...:68-88`; priority `:90-104`; CHECK PASS r=1 n=2 rv=1 `:12,:234`; root `artifacts/pdca/178/r1/`.
- T01 numeric widths (8 integral) → test (direct controlled `IDataRecord`; SQLite not used for `ulong.MaxValue`).
- T02 undefined + flags numeric → test.
- T03 nullable + `DefaultOnNull` → test (neg: no zero substitution).
- T04 scalar + flat member → test.
- T05 string attribute on type + member → test (both stock converter forms; member precedence).
- T06 string converter edge values → test; derived custom converter → guard (`:76`).
- T07 unsupported converter before output → test (custom attribute / non-null `column.Converter`).
- T08 numeric storage + text rejection → test; string field / `EnumToStringConverter` / unsupported native → guard/test (`:78`).
- T09 numeric provider mismatch / out-of-range → test (no truncation).
- T10 both surfaces sync/async → test.
- T11 cancellation + ownership → test (pre-cancel defect fixed `:13`; `Enum_PreCancelled_ShouldThrowAndKeepOwnership`).
- T12 in-memory fail-closed → test (`InMemoryTests.cs:127-154`).
- T13 empty + non-empty → test.
- T14 modes + options → test; NdJson+Root / NdJson+WriteIndented → guard (`:84`).
- T15 provider field types (6 providers) → test; integration 30/30 0-skipped (`:237`).
- T16 existing streaming regression → test.
- T17 parameters + shared cache → test + audit.
- Native/text enum parsing; arbitrary converters → **guard**, expansion **deferred**+trigger (`:88,:184`).

**D179** — matrix `rc2-179-...:39` (V01–V21); priority `:92`; CHECK PASS r2/n2 `:112`; root `/tmp/d179_r2_*.log`; tests `JsonStreamWriterTests`, `JsonNativeStreamTests`, `JsonStreamingTests` (`:118`).
- V01 sync×array×serialize/read-abort; V03 sync×array×sink I/O; V04 sync×NdJson×serialize/read-abort; V06 sync×NdJson×sink I/O; V07 async×array×serialize/read-abort; V08 async×array×cancellation-after-prefix; V09 async×array×sink I/O; V10 async×NdJson×serialize/read-abort; V11 async×NdJson×cancellation; V12 async×NdJson×sink I/O; V13 pre-output validation×sync/async×modes; V14 in-memory×sync/async fail-closed; V15 temp-table×sync/async fail-closed; V16 both public surfaces; V17 empty/nonempty/success/parity/rollover; V18 seekable/non-seekable sink with prefix (no rollback); V20 default/explicit options+validation → **test**.
- V02 sync×array×CT; V05 sync×NdJson×CT → **guard**.
- V19 SQLite/PG/SS/MySQL/MariaDB/ClickHouse → **guard** of the shared managed branch + integration regression.
- V21 value/reference payload/provider conversion → **guard** of the unchanged row writer; new conversion rules **deferred** → #180 (`:39,:63`).
- Prefix-retention tests (`ThrowingAfterBytesStream`), async temp-table refusal, async NdJson parity, native mid-pump cancellation + `Read()`-fault added at r2/n2 (`:111,:118`).

**D180** — matrix `rc2-180-...:63-79`; priority `:180-182`; CHECK PASS r=2/n=2/rv=2 `:11,:205`; root `TestResults/D180/r2/rv2/`; rows E180-01..18 `:157-176`.
- V01 both surfaces × sync/async; V02 async CT; V03 numeric types (guard: unsupported binding before output); V04 bool/string/Guid/DateTime/byte[]; V05 six providers integration; V06 SS storage-typed / CH decimal / unsigned-sbyte experiment+regression; V07 null/default + ignore-null; V08 scalar/ref-DTO/value-type/empty framing; V09 complex/converter/LOB/empty/duplicate-name rejection (guard+test); V10 mode/option boundaries (guard+test); V11 in-memory/temp-table unsupported (guard+test); V12 destination ownership/flush; V13 `params` parity (signature guard+test); V14 enum/nested fail-closed (guard; support deferred to #178/#176) → **test/guard** as marked (`:65-78`).
- V15 runtime malformed/overflow/non-finite → **test** (runtime vs preflight distinction; recovery #179).

**D185** — matrix `rc2-185-...:49-61`; priority `:96-100,107-128`; CHECK PASS r=2/n=2 `:164`; root `/tmp/nextorm-D185-r2/`; rows E00..E16 `:107-128`.
- Cold interface + typed projection (`FromSql`/`From(string)`) → test R01/R02.
- Cold class + typed projection / whole entity → test R02/R03.
- Registered auto mapping + interface/class → test cold/warm parity.
- Registered configured mapping + projection/materialization → test R04.
- Interface + whole entity → test baseline parity; new support **deferred** (separate requirement).
- TVP-only cold; TVP-warm→Bind; configured→TVP→Bind → test R05.
- Bind-auto→explicit `From<T>(cfg)`→re-bind → test R04.
- `TEntity==TableAlias`; repeated bind → **guard+test** R07/R02.
- Null/empty/invalid columns; missing mapped column; unmappable type → **guard+test** R06/R07.
- Nullable/default + converter provider type → test R03/R04; value-type entity **deferred** (confirmed scenario).
- InMemory raw source → **guard+test** original refusal.
- Shared-command/cache flags; concurrent reconfiguration → **guard**; new concurrency guarantee **deferred**.

**D190** — matrix `rc2-190-...:113-132`; priority `:431-443`; CHECK PASS r1/rv1 `:865-912`; root `artifacts/pdca/rc2-190/r1/`; mandatory EV190-* enumerated in §4b.3.
- Direct `Item1`/middle/last `ItemN`, arities 2–8 → test (`:115`).
- Self-join different sides same EntityType → test (`:116`).
- Inner join / nullable left side / both sides → test (`:117`).
- Absent reference item / all-NULL DB item → test (`:118`).
- Present entity with `0`/`false`/default scalars+nullables → test (`:119`).
- Null/default/uninitialized in-memory item → test (`:120`).
- Reference entities → test (`:121`); value entities → test/guard (`:122`).
- Missing usable ctor / missing-invalid mapping → guard+test (`:123`).
- Direct root; nested whole object; explicit scalar/composite; bare → test (`:124`).
- Materialized entity not a joined `ItemN`; entity comparison in WHERE → guard (`:125`).
- Explicit cast to object/unrelated; arbitrary whole-object → deferred+trigger (`:126`).
- Cached/prepared reuse one context → test + perf (`:127`).
- Provider SQLite/PostgreSQL/SS/MySQL/CH → test; MariaDB standalone live → deferred+trigger (`:128-131`).
- Temp-table/TVP-specific branches → guard / conditional N-A (`:132`).

**D191** — matrix `rc2-191-...:222-241`; priority `:245-256`; closure map `:750-758`; CHECK PASS r1/n2 snapshot-n2 `:562`; root `artifacts/pdca/rc2-191/r1/`; rows E01..E19 `:440-462`.
- V-API relocated method correct provider assembly/namespace → test + Roslyn audit; E03 (`:224,:758`).
- V-NEUTRAL core path unaffected → test.
- V-DEFAULT default/null/missing clause → test; prohibited cases → guard.
- V-TYPES ref/value + projection types → test; unrepresentable → guard.
- V-COPY clone/projection change/repeated clone/independence → test.
- V-HAVING → test.
- V-JOIN EntityBuilder/QueryCommand source, first/subsequent → test.
- V-JOIN-OPTIONS default/each option → test; invalid combos → guard.
- V-CH ClickHouse settings/prewhere/array-join/source binding → SQL tests + CH integration; closure `ClickHouseExtensionStateTests` 5 (`:752`).
- V-PG PG `DistinctOn` × clone/projection/order/join → SQL tests + PG integration (`:754`).
- V-OTHER other reviewed provider methods → test + provider SQL suite.
- V-CACHE prepared/cached repeated calls → test + benchmark.
- V-TEMP temp-table `storeInCache:false` → test + integration.
- V-TVP metadata precedence/Clear/repeated use → test + SQL Server integration; `TvpMetadataCacheTests` 4 (`:756`).
- V-EAGER allowed vs forbidden join-source state → test + guard; `ProviderExtensionEagerGuardTests` 2 (`:752`).
- V-PROVIDERS six-provider integration + dialect suites → test (`:757`).
- V-ALIAS-POC `tests/nextorm.alias.poc` consumer → guard; Roslyn inventory => N/A predicate (`:240,:758`).
- V-D168 non-default SQL Server D168 benchmark → deferred+trigger (separate contract; `:241`).

### 4b.3 D190 exact mandatory row IDs — EV190-* (all 21; `rc2-190-...:517-737`) and R190-01..09

**EV190-* mandatory rows** (no wildcard; every id, with requirement and closure evidence):
1. `EV190-PREFLIGHT` (R190-09) — base/skill/ownership preflight; `$E/preflight/*`; closed CHECK PASS `:867`.
2. `EV190-SCOPE` (R190-09) — test-scope safety; 13 single-class selectors, validator `brief`/`report` rc 0 (`:847-849`).
3. `EV190-RED` (R190-01,R190-02,R190-04) — meaningful red before fix: core 5 selected/3 failed exit 2; sqlite 7/3 exit 2 (`:794`).
4. `EV190-ROOT` (R190-01) — direct slots/arities/typed root: core `~EntityItemProjection` 7/0, sqlite 15/0 (`:884-885`).
5. `EV190-NULL` (R190-02) — absent/all-NULL/present-default semantics: `null-semantics.md` §1–§3 (`:873-874`).
6. `EV190-REGRESSION` (R190-03) — nested/scalar/composite/bare + mapping/ctor/value guards preserved (`:875`).
7. `EV190-CACHE` (R190-04) — shape/source/slot distinctions and equivalent reuse: `~SelectExpressionPlanEqualityComparer` 12/0, `~PlanKeyUniqueness` 13/0 (`:884-885`).
8. `EV190-SHARED` (R190-04) — no shared `QueryCommand` mutation: `cache-isolation.md`; clean (`:876`).
9. `EV190-TEMP` (R190-04) — temp/TVP applicability audit: N/A with demonstrated no changed temp/TVP branch (`:609-612`).
10. `EV190-SQL` (R190-01,R190-05) — six-provider SQL shape: `JoinWholeEntitySqlGenerationTests`; clean (`:877`).
11. `EV190-INTEGRATION-DIRECT` (R190-02,R190-05) — direct/outer-null/cache: `~JoinWholeEntity` 20/0/0 skipped (`:886`).
12. `EV190-INTEGRATION-REGRESSION` (R190-03,R190-05) — typed CTE/self-join/arity: 9/0/0 (`:798`).
13. `EV190-INFRA` (R190-05) — Podman socket ping + recovery; PASS (`:800`).
14. `EV190-BUILD` (R190-06) — Debug/Release 0W/0E (`:883`).
15. `EV190-COVERAGE` (R190-06,R190-09) — boundary sweep 9235 total/0 failed/198 capability skips; line 88.1% / branch 80.1% (`:887-888`).
16. `EV190-BRANCH` (R190-06) — baseline→final branch delta: QueryPreparer 91.3/85.2, RowMaterializerBuilder 93.3/86.6 (`:850-853`).
17. `EV190-MUTATION` (R190-06) — CLOSED by authorized no-tool disclosure; Stryker 5.0.0 M1–M6 exit codes; residual 182/1035 unverified mutants declared (`:891-903`).
18. `EV190-PERF` (R190-07) — exactly 7 cases, 0 failures, wall 51 s, cached/prepared 2.171 vs 1.87 (+16.1% <20%) (`:889-890`).
19. `EV190-AUDIT` (R190-04,R190-09) — suppression/slop/sealedness audit: `$E/audit/*`; clean (`:713-722`).
20. `EV190-DOCS` (R190-08) — EN/RU docs; DocFX exit 0 (2 pre-existing warnings) (`:801,:878`).
21. `EV190-REGISTERS` (R190-08) — register reconciliation/issue #190 milestone; closure after PASS (`:912`).

**R190-01..09 P1 rows** (`rc2-190-...:871-881`; all closed/clean):
- `R190-01` direct `Select(p => p.ItemN)` root projection / typed result — clean (`:872`).
- `R190-02` absent outer side ⇒ `null`; present entity (incl. defaults) not null — clean (`null-semantics.md` §1–§3; `:873-874`).
- `R190-03` scalar/composite/nested/bare + mapping/ctor/value guards preserved — clean (`:875`).
- `R190-04` cache identity / no sticky mutation, alternating shapes — clean (`:876`).
- `R190-05` six-provider SQL shape + real container integration (0 skipped) — clean (`:877`).
- `R190-06` build/coverage/branch/mutation — clean, mutation via authorized no-tool disclosure (`:878,:891`).
- `R190-07` acceptance benchmark — clean (`:879`).
- `R190-08` EN/RU docs + registers — clean (`:880`).
- `R190-09` scope / test safety / single boundary sweep / evidence completeness — clean (`:881`).

### 4b.4 D173 priority disposition

**D173 priority is NOT missing.** The earlier §4/G4 claim of "no explicit priority matrix; P1/P2
inferred only" is corrected by the status file: `rc2-173-projection-folding-member-1.md:171-173`
records an explicit mapping — "CHECK priority: R173-01..05 and their null/identity/key/store/compat
assertions are **P1**; R173-06/07/09 **P1**; R173-08 + register/scope hygiene = completion gate; …
the latent-P2 label does not downgrade." The only P2 element is the pre-existing latent label, which
the file states does not downgrade the P1 rows. Dispositions in §4b.2 (all test/guard; none deferred)
are the bound rows.
