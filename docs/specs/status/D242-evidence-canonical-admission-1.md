# D242 — T242 evidence canonical global admission — cycle status

task: T242
issue: https://github.com/AlexeyShirshov/nextorm/issues/242
issue_title: Evidence pipeline: canonical global pdca-dotnet admission + required_rows coverage fold-in
milestone: 1.0.9-rc3
collection: 1.0.9-rc3-open-2
collection_status_path: docs/specs/status/collection-1.0.9-rc3-open-2.md
group: G1 (single group; no worktree; current branch `1.0.9-rc3`)
selected_variant: pdca-coder
cycle: N=1
attempt: n=1
plan_revision: r=1
evidence_contract_rv: 1
plan_state: ready
status: incomplete
blocked_reason: BLOCKED — owner decision required (STOP routed from `escalate`)
baseline_ref: 83e78d37
base_ref: 83e78d37 (= collection_baseline_ref)
branch: 1.0.9-rc3
worktree: /home/alex/sources/nextorm
execution_mode: single sequential stream in the current tree (no worktree; no commit/push/merge)
evidence_dir: artifacts/pdca/D242/rv1/
frozen_manifest: artifacts/pdca/D242/rv1/manifest.json
contract_input: artifacts/pdca/D242/rv1/contract.json
global_snapshot: docs/specs/status/D242-global-admission-files.json
check_regather_budget: max 2 targeted re-gathers (owner `check`)
dependency: T241 → T242 (start DO only after T241 in the collection group)

## PLAN (r=1)

### Goal (essence, not issue wording)

Admit the in-repo evidence gate into the authoritative GLOBAL PDCA skill and close the
manifest-coverage gap. Follow-up to #234 (`docs/specs/status/D234-evidence-pipeline-hardening-1.md:189-196`).

- **Obligation A — canonical global admission.** Promote in-repo `scripts/pdca_evidence.py`,
  `scripts/tests/pdca_evidence_harness.py`, `scripts/tests/test_pdca_evidence.py`,
  `scripts/tests/fixtures/pdca_evidence/cases.json`, `scripts/README.pdca-evidence.md` into global
  `~/.config/opencode/skills/pdca-dotnet/{scripts,tests}` and wire `SKILL.md` (`:922–983` esp.
  `:976`; `:1150–1172`) so the evidence contract / CHECK gate reference the canonical CLI; keep
  existing global `scripts/validate_inner_loop.py`, `scripts/pdca_cost.py` and global `tests/*`
  self-consistent and green.
- **Obligation B.** Fold the `required_rows` ↔ `rows` two-way coverage check into
  `scripts/pdca_evidence.py lint` (`_lint_manifest_doc :394–459`); `scripts/validate_inner_loop.py
  manifest` stays unchanged (interim guard, regression oracle).
- **Owner authorization (2026-10-16):** the global write is explicitly authorized. Global dir is
  writable but **NOT a git repo** → mandatory pre-write timestamped backup + minimal/reversible edits.

### Acceptance criteria (observable + negative)

| ID | Observable | Negative |
|---|---|---|
| R242-A | global files installed + SKILL.md wired to the canonical CLI | local-only CLI or files without wiring = not admission |
| R242-B | two-way set equality enforced in `lint` | missing/extra row must be rc 2, not rc 1 |
| R242-C | `validate_inner_loop.py manifest` unchanged and still rc 2 on both coverage violations | — |
| R242-D | in-repo + global suites green and CLI coverage ≥90 % | zero collected tests, unexpected skips, or coverage below threshold = FAIL |
| R242-E | frozen evidence tree-bound, global files additionally bound by saved SHA-256 | `open`/stale tree or a global file changed after capture = FAIL |
| R242-F | verified backup before the global write, changes limited to the authorized footprint | no backup, unexpected changed file, or a write to a neighbouring repo = FAIL |
| R242-G | the stale "canonical admission not yet done" claim removed and durability limits kept explicit | docs claim admission without global install or hide the rsync-overwrite risk |

### Minimal solution & variants

Chosen: **port with LF + portable paths** (single implementation, test infra made path-portable;
small diff; verified CLI identity).

Rejected:
- independent global reimplementation — duplication/drift;
- mirroring to both upstream repos — outside authorized footprint; divergent copies.

Do NOT replace the global `validate_inner_loop.py` with the local one.

### Task list (DO units; all `fix now` unless noted)

- **D242-01** preconditions/baseline/backup: verify branch/HEAD, dependency T241, python, global dir,
  host config; record git status; take the backup (§Backup) BEFORE any global write; run global
  baseline with and without host config; forbid new `.pyc`.
- **D242-02** close the coverage gap: add two-way coverage + `required_rows` validation to
  `scripts/pdca_evidence.py:394–459`, consistent with the interim validator; **do not modify**
  `scripts/validate_inner_loop.py:1217–1224`; add extra/missing-row, malformed/empty/duplicate
  `required_rows`, and exact-set positive scenarios to `scripts/tests/test_pdca_evidence.py` +
  `fixtures/pdca_evidence/cases.json`. Do not change tree-binding, atomic publication, locking,
  scope checks, operational-error classification.
- **D242-03** test-infra portability: make `scripts/tests/pdca_evidence_harness.py:46–48` and
  `test_pdca_evidence.py:22–27` resolve paths from file location for BOTH layouts (repo
  `scripts/tests/` next to `scripts/pdca_evidence.py`; global `tests/` next to `scripts/`); no cwd
  dependence. Add **planned** `scripts/tests/test_pdca_global_admission.py` — a read-only admission
  audit of the installed dir (backup presence, change whitelist, LF, normalized parity,
  inventory+SHA-256 snapshot, doc wiring), env-driven (`PDCA_ADMISSION_SKILL_DIR`,
  `PDCA_ADMISSION_BACKUP_DIR`, `PDCA_ADMISSION_SNAPSHOT`), with self-tests on temp dirs.
- **D242-04** minimal global port (after local tests pass): copy the 5 sources to
  `scripts/pdca_evidence.py`, `scripts/README.pdca-evidence.md`, `tests/pdca_evidence_harness.py`,
  `tests/test_pdca_evidence.py`, `tests/fixtures/pdca_evidence/cases.json`; convert CRLF→LF; keep all
  other global files unchanged. Wire global `SKILL.md:922–983` (esp. `:976`) to the canonical
  `generate`/`lint` incl. exit codes, frozen manifest and source/tree binding; wire
  `SKILL.md:1150–1172` so the CHECK completeness gate invokes the global CLI and forbids PASS on
  open/coverage/staleness failure; reference the installed-skill path form
  (`~/.config/opencode/skills/pdca-dotnet/scripts/pdca_evidence.py`); preserve the sections' headings
  and existing obligations; do not mass-rewrite other inner-loop-validator references.
- **D242-05** docs + snapshot: update `scripts/README.pdca-evidence.md` (canonical installation, both
  coverage directions, exit codes, local interim guard); replace the stale claim at
  `.opencode/skills/nextorm-pdca/SKILL.md:232–237` (admission now done) keeping the overlay .NET-only
  scope and gates; add a dated T242 link + new interim-guard meaning to
  `docs/specs/status/D234-evidence-pipeline-hardening-1.md:189–196` preserving history; create the
  global SHA-256 snapshot `docs/specs/status/D242-global-admission-files.json` (ordered inventory,
  absolute paths, SHA-256, excluding `__pycache__`/`.pyc`); normalize changed repo files to CRLF;
  freeze footprint.
- **D242-06** run the §Commands and the evidence contract; record real logs/rc.
- **Deferred:** writes to `/home/alex/sources/skills/**` and
  `/home/alex/sources/opencode-config/**` — deferred, trigger = separate explicit owner authorization
  per repo + agreement on the authoritative upstream. Long-term reinstall/`rsync --delete` durability
  check — deferred to that authorization; risk stays open.

### Unit execution mode

Single sequential stream in the current tree (no worktree); global tests read `SKILL.md`, so
install + wiring + suite + snapshot must run in order.

### Commands & evidence paths

Export `E=/home/alex/sources/nextorm/artifacts/pdca/D242/rv1`,
`G=/home/alex/.config/opencode/skills/pdca-dotnet`,
`S=/home/alex/sources/nextorm/docs/specs/status/D242-global-admission-files.json`,
`PYTHONDONTWRITEBYTECODE=1`; each named call saved to `$E/<name>.log` with rc.

- `repo-unit` = `python3 -m unittest discover -s scripts/tests -p 'test_pdca_evidence.py' -v`
- `repo-negative` = `python3 scripts/tests/pdca_evidence_harness.py negative --output "$E/repo-negative"`
- `repo-coverage` = `python3 scripts/tests/pdca_evidence_harness.py coverage --output "$E/repo-coverage"`
- `repo-mutate` = `python3 scripts/tests/pdca_evidence_harness.py mutate --output "$E/repo-mutate"`
- `global-host` = `OPENCODE_CONFIG_REPO=/home/alex/sources/opencode-config PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s /home/alex/.config/opencode/skills/pdca-dotnet/tests -p 'test_*.py' -v`
- `global-nohost` = `env -u OPENCODE_CONFIG_REPO PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s /home/alex/.config/opencode/skills/pdca-dotnet/tests -p 'test_*.py' -v`
- `global-negative` / `global-coverage` / `global-mutate` = as above against `"$G/tests/pdca_evidence_harness.py"`
- `validator-unchanged` = `git diff --exit-code 83e78d37 -- scripts/validate_inner_loop.py`
- `admission-audit` = `PDCA_ADMISSION_SKILL_DIR="$G" PDCA_ADMISSION_BACKUP_DIR="$(cat "$E/global-backup.path")" PDCA_ADMISSION_SNAPSHOT="$S" python3 -m unittest discover -s scripts/tests -p 'test_pdca_global_admission.py' -v`
- `generate` = `python3 "$G/scripts/pdca_evidence.py" generate --repo /home/alex/sources/nextorm --contract "$E/contract.json" --output "$E"`
- `local-lint` = `python3 scripts/pdca_evidence.py lint --repo /home/alex/sources/nextorm --manifest "$E/manifest.json"`
- `canonical-lint` = `python3 "$G/scripts/pdca_evidence.py" lint --repo /home/alex/sources/nextorm --manifest "$E/manifest.json"`
- `interim-manifest` = `python3 scripts/validate_inner_loop.py manifest "$E/manifest.json"`

Also: run the global suite once from `/tmp/opencode` (cwd independence); `repo-negative` must record
real CLI + rc for extra/missing/open/stale and the same extra/missing fixtures via the interim
validator.

### Test strategy

Unit (schema/coverage/exit codes/path resolution/admission audit on temp trees); Integration
(disposable git repos, frozen tree, receipts, publication); E2E (installed global CLI + global suite
+ D242 manifest + external snapshot). Coverage ≥90 % for the evidence CLI via the harness (project
.NET 85/75 thresholds are NOT applicable and are not changed).

Edge matrix: clean tree → lint rc 0; dirty tree within footprint → frozen tree covers changes, rc 0,
no HEAD-tree-equality claim; footprint change after capture → rc 2; `open`/stale row → rc 2; extra
row / missing row → both validators rc 2; missing/null/default/empty/wrong-type `required_rows` and
duplicate IDs → contract violation rc 2 while malformed JSON stays operational/usage rc 1; exact set
in different order → set comparison rc 0; `met` / valid `na` / invalid `na` → evidence or proven
predicate+reason, `na` never bypasses coverage; global file changed after snapshot → admission audit
FAIL; LF global vs CRLF repo → normalized parity + line-ending guards; global suite with
`OPENCODE_CONFIG_REPO` → host checks really run (no host-related skips); without it → only documented
host skips, other tests really run; global run from another cwd → no repo-relative dependency;
upstream mirroring → `na` by authorization predicate, deferred; DB providers / .NET flags → guard (not
applicable to a Python/filesystem CLI). Each matrix row closed as test / guard / deferred-with-trigger.

### Docs plan

In-repo: `scripts/README.pdca-evidence.md`, `.opencode/skills/nextorm-pdca/SKILL.md:232–237`,
`docs/specs/status/D234-evidence-pipeline-hardening-1.md:189–196`, this status file, the global
snapshot JSON, two global `SKILL.md` sections. Public `docs/**`, `docs/ru/**`, root `readme.md` and
API reference are NOT touched (no public API change).

### Perf decision

No perf measurement: the change in `scripts/pdca_evidence.py:394–459` is a single validation pass over
manifest rows; use sets, O(required+rows), no quadratic search; BenchmarkDotNet is not applicable.

### Recon decision

No extra spike needed — the gap and safe anchors are established; a baseline experiment of the global
suite with/without host config is mandatory (expected rc 0 + expected executed/skipped composition);
on FAIL, a targeted scout, not a widened footprint.

### Toolchain & lenses

Toolchain: `python3 -m unittest`, stdlib harness, git, SHA-256 (record the Python version in the log).
Lenses bound to generic roles: correctness/anti-pattern → CHECK via `scout` (read-only
diff/commands/evidence); test-quality → CHECK via `scout` (scenarios, assertion strength,
coverage/mutation reports); docs → CHECK via `scout` (anchors, canonical path, residuals). Verdict
belongs to `check`; implementation/runs to `coder`. nextorm .NET lenses are NOT used.

### Design checklist — PASS at plan level

CLI/exit-code compatibility preserved; the new coverage invariant lives in the authoritative `lint`,
not only in the external guard; no new runtime deps, no parallel CLI implementation, no weakening of
evidence obligations; path resolution portable; line endings split per environment;
tree-binding/atomic publish/scope isolation not redesigned; global install whitelist-limited and
reversible; no future test symbols/results declared as existing.

### Backup & rollback

Backup OUTSIDE `skills/` so opencode does not load it as a skill:

```bash
stamp=$(date -u +%Y%m%dT%H%M%S.%NZ)
B="/home/alex/.config/opencode/pdca-backups/T242/$stamp/pdca-dotnet"
mkdir -p "$(dirname "$B")"; test ! -e "$B"
cp -a -- "$G" "$B"
printf '%s\n' "$B" > "$E/global-backup.path"
diff -qr -- "$B" "$G" > "$E/backup-verify.log"
```

(rc 0 required; ordered log with UTC timestamp showing the backup precedes installation; no
bytecode-generating command before the backup).

Restore:

```bash
B=$(cat "$E/global-backup.path"); test -d "$B"
F="$(dirname "$B")/failed-$(date -u +%Y%m%dT%H%M%S.%NZ)"; test ! -e "$F"
mv -- "$G" "$F"; cp -a -- "$B" "$G"; diff -qr -- "$B" "$G"
```

Restores the whole original dir with metadata; the failed version is preserved separately; repo
changes are not rolled back by this; after a rollback the admission manifest is no longer valid.

### Risks

SKILL.md anchor guards (whole-file `assertNotIn(":757")`, `count("cheap orchestrator")>=3`,
`assertNotIn("the orchestrator (primary)")`, `assertNotIn("any primary")`; section-scoped guards) →
preserve headings/obligations, real global suite after wiring; not-a-git global dir → backup +
whitelist diff + SHA inventory + no concurrent writers; reinstall/rsync overwrite → open residual, do
not sync neighbours; CRLF/LF → byte-level guards + normalized parity; repo-relative paths → run global
from `/tmp/opencode`; host baseline may show a pre-existing external FAIL → do not hide, do not fix
the host repo without authorization; self-referential evidence → the manifest must not contain its own
hash or a receipt about itself, terminal verification happens after publication; scope freeze →
changing the footprint after capture requires regeneration, not renaming old evidence.

### DO → PLAN classification

Additive prerequisite → new active task, keep the original `D` blocked (not superseded); true
outside-cycle blocker → recommendation to `escalate`; persistent low confidence after a targeted scout
→ `escalate` (trigger 5).

### Versioned evidence contract — rv=1

Pinned as above; `supersedes = null`; all sources are **PLANNED** until actually obtained. Repo
footprint = the explicit file list of D242-02/03/05 incl. status + global snapshot;
`scripts/validate_inner_loop.py` is a controlled unchanged source. Exclusions: `artifacts/**`,
generated bytecode, the collection lifecycle record. Global files are not part of the git tree — they
are bound by the included snapshot + the repeat admission audit.

Rows (each with requirement ID, row ID, applicability predicate, scenario, command + expected rc,
evidence kinds/sources, owner, rv=1):

| Row → req | Predicate | Scenario | Command / expected rc | Evidence | Owner |
|---|---|---|---|---|---|
| E242-01 / R242-F | always | pre-write backup | §Backup `diff` rc 0, timestamp precedes first install; kinds: filesystem backup + command log | `$E/global-backup.path`, `backup-verify.log`, install timeline | DO-install |
| E242-02 / R242-D | always | in-repo suite | `repo-unit` rc 0, non-zero test count, new scenarios executed | `$E/repo-unit.log` | DO-tests |
| E242-03 / R242-B | always | exact row coverage | `repo-negative` + `repo-unit` rc 0; clean CLI rc 0, extra/missing CLI rc 2 separately | scenario report + CLI logs | DO-tests |
| E242-04 / R242-C | always | interim guard unchanged | `validator-unchanged` rc 0; `repo-negative` records extra/missing via validator rc 2; `interim-manifest` rc 0 | `$E/validator-unchanged.log`, negative report, `interim-manifest.log` | DO-tests |
| E242-05 / R242-D | always | coverage + assertion strength | `repo-coverage`, `global-coverage`, `repo-mutate`, `global-mutate` rc 0; coverage ≥90 %; required mutations killed | `$E/{repo,global}-{coverage,mutate}*` | DO-tests |
| E242-06 / R242-A,F | always | files installed, LF, portable paths, whitelist | `admission-audit` rc 0; global run from `/tmp/opencode` rc 0; existing global validator/cost/tests/assets unchanged | `$S`, `$E/admission-audit.log`, cwd-run log | DO-install |
| E242-07 / R242-A,D | always, host config dir must exist | global host suite | `global-host` rc 0, non-zero count, no host-related skips; baseline and post recorded separately | `$E/global-host-{baseline,post}.log` | DO-tests |
| E242-08 / R242-D | always | global suite without env | `global-nohost` rc 0; only documented host skips; other tests really run | `$E/global-nohost-{baseline,post}.log` | DO-tests |
| E242-09 / R242-A,G | always | canonical wiring + anchor guards | `admission-audit` + `global-host` rc 0; both sections contain the canonical CLI; completeness obligations preserved | anchored document assertions + global suite + snapshot | DO-docs |
| E242-10 / R242-E | always | open/stale/dirty-tree variants | `repo-negative`, `global-negative` rc 0; synthetic `open` and post-capture tree change give CLI rc 2; clean/dirty coherent captures pass | `$E/{repo,global}-negative*` | DO-tests |
| E242-11 / R242-A,E | always | canonical generation + final manifest | `generate`, `local-lint`, `canonical-lint`, `interim-manifest` rc 0; receipt/manifest tree bindings consistent; no open required row | `$E/manifest.json`, `generations/**`, four logs | DO-evidence; terminal verification CHECK |
| E242-12 / R242-E,F | always | external snapshot current after freeze | repeat `admission-audit` rc 0 before the verdict; inventory and SHA match; no unexpected files | `$S`, `$E/check-global-snapshot.log` | CHECK |
| E242-13 / R242-G | always | coherent documentation | `admission-audit` rc 0 + docs-lens review; no prior admission denial; upstream overwrite risk retained | README/overlay/D234/D242/global SKILL, `$E/docs-review.*` | DO-docs / CHECK-docs |
| E242-14 / R242-F,G | predicate: separate explicit authorization to write each upstream repo — currently **false** | mirroring commands **N/A**; no writes to `/home/alex/sources/skills` or `/home/alex/sources/opencode-config` | authorization/scope record + residual; `status=na` with predicate+reason | `$E/scope-review.*` | PLAN / CHECK-scope |

### Freeze & CHECK

Freeze after the repo/global changes, tests, docs and snapshot are complete; prepare real
command-result evidence (never "planned" text); run canonical `generate`, then both lints and the
interim manifest validator; those terminal logs are closure evidence — do not append the manifest for
its own result; CHECK repeats the snapshot audit and reconciles all `required_rows` with
rows/receipts/closure evidence (a source is required even for `na`); any open required row, stale
evidence or lost external snapshot forbids PASS.

Re-gather budget: max 2 targeted re-gathers, owner `check` (one for a missing/unreadable command
artifact, one for ambiguous provenance/applicability); no automatic new DO iteration for a missing
report; on exhaustion → INCOMPLETE/back to planner; persistent low confidence → `escalate` trigger 5.

### Confidence & next gate

High confidence in the minimal solution; actual host-enabled global-suite compatibility not yet
proven. Assumption: no concurrent writer of the global skill during installation.

This is a PLAN-only checkpoint — DO is not started; the collection lane resumes the same cycle at DO
after the approval gate (autonomous mode: no user wait, but only after gate 1). This plan authorizes
no commit, push, merge or upstream mirroring.

### Progress log

2026-10-16 | PLAN | revision r=1 | iteration 1/3 | PLAN r=1 written; plan_state=ready; status=planned; stopped at PLAN→DO boundary (collection own-PLAN phase; no DO started, no commit) | docs/specs/status/D242-evidence-canonical-admission-1.md
2026-10-16T18:02Z | DO | revision r=1 | iteration 1/3 | D242-01/02/03 (repo side, no global write) complete: pre-write backup diff rc0; repo-unit rc0 (62 tests); repo-negative rc0 (24 cases; extra/missing CLI rc2 + interim validator rc2); repo-coverage rc0 (95.35% >= 90); repo-mutate rc0 (7/7 killed); admission self-tests rc0 (17 run, 1 env-gated skip); global baselines rc1 (pre-existing host FAIL 14F/6E) / rc0 (28 skips); validator-unchanged rc0 | artifacts/pdca/D242/rv1/

## FINALIZATION — STOP (incomplete / BLOCKED — owner decision required)

status: incomplete (was `planned`)
plan_state: ready (unchanged)
reason: **BLOCKED — owner decision required (STOP routed from `escalate`)**
plan_revision: r=1 (unchanged; keep r=1, rv=1 — all row IDs and the evidence dir `artifacts/pdca/D242/rv1/` preserved verbatim; no `supersedes`, no rv2 dir, no manifest migration, no attempt reset)
global skill dir `$G` (`/home/alex/.config/opencode/skills/pdca-dotnet`): **NOT written** (byte-identical to pre-write backup; verified by `diff -qr` rc 0)
`$HOST` (`/home/alex/sources/opencode-config`) and `/home/alex/sources/skills`: **untouched**
commit / push / merge: **none**

### Blocker pack

- `global-nohost-baseline` rc 0 (`Ran 217`, `OK (skipped=28)`, all 28 = the documented host-unavailable reason) → `$E/global-nohost-baseline.log`.
- `global-host-baseline` **rc 1** (`Ran 217 in 2.448s`, `FAILED (failures=14, errors=6)`) → `$E/global-host-baseline.log`.
- Cause: `/home/alex/sources/opencode-config/config/agents/*.md` use plural `permissions:` (list) while `$G/tests/test_routing_consistency.py:105-106` parses singular `permission:` → `{}`; `grep '^permission:'` on `$HOST/config/agents` → no matches; error sites `:129,173,186,199,212`, assertion site `:229`, all in `RolePermissionTest`, no other module failed (197 ok).
- Safety fact: T242's planned edits provably cannot change that failure set (no host-gated assertion compares `$G` to `$HOST/config/skills/**`; SKILL reads bind `parents[1]`→`$G`; the two edited sections are not `between(...)` anchors; no test enumerates `$G/scripts`/`$G/tests`).

### Row ledger (factual execution state, no general PASS)

- **E242-01 PASS** — backup (`diff -qr` rc 0), re-verified.
- **E242-02 PASS** — `repo-unit` rc 0, 62 tests.
- **E242-03 PASS** — `repo-negative` rc 0, 24 cases; extra/missing ⇒ CLI rc 2 AND interim `scripts/validate_inner_loop.py manifest` rc 2.
- **E242-04 PARTIAL** — `validator-unchanged` rc 0 + negative-fixture rc 2 done; final-manifest `interim-manifest` not run.
- **E242-05 PARTIAL** — repo-coverage 95.35 % ≥ 90, repo-mutate 7/7 killed; global side not executed.
- **E242-06 NOT EXECUTED.**
- **E242-07 BLOCKED** — its contract fields/requirements retained verbatim (see rv=1 table above); appended status text:
  - `E242-07 execution status: BLOCKED. Pre-install global-host rc=1; 217 tests; 14 failures + 6 errors, all in RolePermissionTest. Required global-host rc=0 with no host-related skips remains unsatisfied. Baseline-equivalent failures alone do not satisfy this row.`
  - `External blocker: global tests read singular permission via test_routing_consistency.py:105–106, while $HOST/config/agents/*.md use plural permissions; failures at :129,173,186,199,212,229. Host modifications are outside the authorized footprint.`
- **E242-08 PARTIAL** — nohost baseline rc 0, 217/28 skips; post-install not executed.
- **E242-09..E242-13 NOT EXECUTED.**
- **E242-14 stays `na`** — predicate "separate explicit authorization per upstream repo" still false; unchanged.

### Dependent units

- `D242-04`, `D242-05`, `D242-06` = **active / blocked** (not done, not superseded).
- `D242-01..03` = done (repo-side scope). Completed repo evidence preserved as-is in the working tree (uncommitted).

### Owner questions (recorded verbatim; not acted on)

1. Where to promote — `$G` (stale, and `rsync -a --delete` per `/home/alex/sources/opencode-config/README.md:103` would erase it) or the canonical `$HOST/config/skills/pdca-dotnet` (explicit write authorization needed)?
2. If `$G`: does the owner authorize a contract revision (rv2 with `supersedes`) replacing E242-07 by E242-07a (no-env suite rc 0, 217 tests, exactly the 28 host skips) + E242-07b (host-enabled run judged by byte-comparison of the failure set against the recorded baseline), accepting the residual "20 `RolePermissionTest` failures from the `$HOST` agent-schema divergence"?
3. Or does the owner fix `$HOST` separately (align `permissions:`/`permission:` or the parser), after which E242-07 is re-run unchanged?

### Residual risk accepted

T242 not closed; Obligation B is proven only in-repo; the divergence + rsync-overwrite hazard remain.

Follow-up (recorded as text only; no issue created): the host-repo owner should reconcile `permissions:`/`permission:` and reduce the `$HOST`↔`$G` tree drift to one source.

### Patch

`artifacts/pdca/D242/rv1/task.patch` (reference only; tree not reverted).

### Progress log (finalization)

2026-10-16T18:07Z | ACT | revision r=1 | iteration 1/3 | STOP routed from `escalate`: status=incomplete / BLOCKED — owner decision required; r=1, rv=1, row IDs and evidence dir preserved; `$G` byte-identical to backup (diff -qr rc 0, global-untouched-verify.log); $HOST and /home/alex/sources/skills untouched; task.patch saved (31503 bytes, reference only, tree not reverted); no commit/push/revert/global-write | artifacts/pdca/D242/rv1/global-untouched-verify.log, artifacts/pdca/D242/rv1/stopped-tree-status.log, artifacts/pdca/D242/rv1/task.patch
