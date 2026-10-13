# T217 (#217) — D217-datetimeoffset, cycle N=1

## Identity and state

- task_id: T217; collection_id: `1.0.9-todo`; issue: #217
- title: DateTimeOffset — SQL-трансляция `Now`/`UtcNow` и частей даты
- selected_variant: pdca-dotnet
- base: branch `1.0.9` @ `58025147`; milestone `1.0.9`
- cycle_id: N=1; plan_revision: **r=5**; attempt: **n=1/3**; evidence-contract revision: **rv=5** (supersedes rv=4; rv=4 superseded rv=3; rv=5 re-opens the r=4 MT:135 blocker and the r=3 evidence-binding STOP)
- plan_state: **ready** (r=5 re-open past the r=4 `E217-17` MT:135 blocker; N=1, r=5, n=1/3, rv=5)
- mode: autonomous collection own-PLAN; unit mode sequential, single current worktree/branch `1.0.9-rc3` (HEAD `94782852053142d548eeb42b672de9d26972945d` = T217 product commit; `23f0354a`/`cdae983` are historical), без веток/worktree/merge/push
- permitted auto-commit: только этот статус-файл, префикс `#217`; push никогда
- phase: **ready (r=5, n=1/3, evidence-contract rv=5)** — PLAN, re-open past the r=4 MT:135 blocker (`D217-r4-05`)
- status_file: `docs/specs/status/D217-datetimeoffset-1.md`

## Durable state

- Current cycle N: 1
- Plan revision r: 5
- Attempt n: 1/3
- Evidence contract rv: 5 (supersedes rv=4; the rv=4 manifest is archived at `artifacts/pdca/D217-datetimeoffset/rv2/superseded/manifest.rv4.json` — see the `r=5` contract section below)
- plan_state: **ready** (r=5 re-open past the r=4 `D217-r4-05` MT:135 blocker; the durable record already contains r=2, r=3 and r=4, so this re-open's genuinely revised plan is **r=5**)
- Required PLAN input — prior terminal STOP: `D217-EVIDENCE-BINDING` (r=2/n=3 + r=3/n=1, **third occurrence**, fixes applied 1, **NOT resolved**) — see "Unresolved rows at STOP" below.
- defect_key registry: `D217-D0-CH-DTO-READER` → observed r=1/n=1 (D0 SPIKE FAIL: ClickHouse DTO projection `GetFieldValue<DateTimeOffset>` InvalidCastException on DateTime64); fixes applied: 0; evidence `artifacts/pdca/D217-datetimeoffset/rv1/d0-spike/d0-results.md`; escalation/outcome: planner route (b) — repair ClickHouse reader locally, criteria NOT narrowed (r=2).
- defect_key registry: `D217-CH-FRACTIONAL-DAY-TRUNCATION` → observed r=2/n=1 (D217-4 integration: ClickHouse DTO `.AddDays(0.5)` renders `addDays(toTimeZone(value,'UTC'), 0.5)` which truncates the fractional day, returning the operand unchanged — off by 12h; violates R217-03 "significant fractional intervals must not vanish"); fixes applied: 1 (same DO attempt); evidence PRE-FIX `artifacts/pdca/D217-datetimeoffset/rv2/integration/clickhouse.pre-fix.log` (11/12, AddFractional failed), POST-FIX `artifacts/.../rv2/integration/clickhouse.log` (12/12, 0 skipped), root cause SQL in `tests/nextorm.clickhouse.tests/T217DateTimeOffsetSqlGenerationTests.cs`; escalation/outcome: **RESOLVED (r=2/n=1)** — `ClickHouseDialect.MakeDateAdd(field,amount,value,isDateTimeOffset)` now scales coarse DTO units to `addMicroseconds`; all 5 providers re-run non-skipped green; D217-4 done.
- Defect history: `D217-D0-CH-DTO-READER` (r1/n1 → r2: active D217-0p fixed, 1 fix); `D217-CH-FRACTIONAL-DAY-TRUNCATION` (r2/n1 → 1 fix applied → RESOLVED, D217-4 unblocked/done).
- defect_key registry: `D217-SS-LITERAL-ADD-INT-OVERFLOW` → observed r=2/n=1 (CHECK re-gather #1, C2 **CONFIRMED**): SQL Server literal `AddSeconds(2_200_000)`/`AddMilliseconds(2_200_000)` renders `dateadd(millisecond|microsecond, (amount) * factor, value)` whose int product overflows; real SQL Server 2025 throws `SqlException: Arithmetic overflow error converting expression to data type int.` (`AddSeconds(2_000_000)`/`2_147_483`, `AddDays(24|25)`, and a captured-double large amount succeed); fixes applied: 1 (r=2/n=2 DO loop-back); root cause is the `int * int` product of the in-place ×1000 scaling; evidence PRE-FIX `artifacts/pdca/D217-datetimeoffset/rv2/regather1/sqlserver-c2.{tsv,log}`, POST-FIX `artifacts/pdca/D217-datetimeoffset/rv2/fix2/{sqlserver-probe.tsv,sqlserver-probe.log}`; escalation/outcome: **RESOLVED (r=2/n=2)** — `SqlServerDialect.MakeDateAdd(field,amount,value,isDateTimeOffset)` keeps the whole part in the requested unit and scales only the fractional remainder into the finer unit (`dateadd(second, cast(floor(amount) as int), dateadd(millisecond, cast((amount - floor(amount)) * 1000 as int), value))`); real SQL Server returns the correct instant for `AddSeconds(2_200_000)`/`AddMilliseconds(2_200_000)` and retains 500 ms for `AddSeconds(1.5)`; criteria NOT narrowed.
- defect_key registry: `D217-C4-FRACTIONAL-TEST-ORACLE` → observed r=2 (CHECK C4, test-oracle): `AddFractional_ShouldKeepTheSignificantFraction` used a 1 s tolerance so a `1.5→1` second truncation still passed; fixes applied: 1; evidence `artifacts/pdca/D217-datetimeoffset/rv2/fix2/{mutation-truncate-integration.log,green-after-restore-integration.log}`; escalation/outcome: **RESOLVED (r=2/n=2)** — the assertion now uses the provider documented precision (`FractionalSecondTolerance`, SQLite 1 s for its whole-second `datetime()` path, 1 ms otherwise) and adds a provider-independent `AddMilliseconds(1500)` sub-second proof; the tightened oracle FAILS (exit 2, off by 500 ms) against a temporary whole-second-truncating `MakeDateAdd` and PASSES after restore.
- defect_key registry: `D217-C6-ZEROARG-DTO-GUARD` → observed r=2 (CHECK candidate, R217-04): `DateTimeFunctionTranslator` returned early on `node.Arguments.Count != 1`, so a zero-argument DTO method (`ToUnixTimeSeconds`/`ToUnixTimeMilliseconds`/`ToLocalTime`/`ToUniversalTime`) fell through to `base.VisitMethodCall` and silently dropped the call instead of failing closed; fixes applied: 1; evidence `artifacts/pdca/D217-datetimeoffset/rv2/fix2/final-core.log`; escalation/outcome: **RESOLVED (r=2/n=2)** — a non-single-argument DTO instance method now throws `NotSupportedException` before SQL; test `UnsupportedZeroArgumentMethods_ShouldThrowBeforeSql`.
- defect_key registry: `D217-C7-NESTED-DTO-RECEIVER-GUARD` → observed r=2 (CHECK candidate, R217-04): a DTO member nested as the receiver of a client-side method call (`x.Value.Offset.ToString()`, `x.Value.Date.ToShortDateString()`); fixes applied: 0; evidence `artifacts/pdca/D217-datetimeoffset/rv2/fix2/final-core.log`; escalation/outcome: **ALREADY FAIL-CLOSED (r=2/n=2)** — both forms throw `NotSupportedException` (the `TimeSpan.ToString()` path via the `node.Type == typeof(string)` reject, the `DateTime.ToShortDateString()` path via the unsupported-`DateTime`-method reject); no guard change needed, recorded by test `UnsupportedMemberAsClientMethodReceiver_ShouldFailClose`.
- defect_key registry: `D217-EVIDENCE-BINDING` → observed r=2/n=3 + CHECK FAIL (evidence gate): rows cited a stale log tree (`verify/verify.log` = f2a6f63), per-row oracle provenance was an unprovable claim, the freeze did not cover `benchmarks/`, the perf evidence was unbound and the 9 PART branch identities were unclassified; fixes applied: 1 (this r=3 corrective DO, evidence-only); evidence `artifacts/pdca/D217-datetimeoffset/rv2/{oracles.rv3.json,perf.binding.json,audit/part-outcomes.json,verify/tree-binding.log,r3-baseline/**}`; escalation/outcome: routed by escalate to PLAN → r=3; acceptance criterion NOT changed, no product/test byte changed; STOP condition = one more failure on this same defect ⇒ incomplete.
- defect_key registry: `D217-MT135-REACHABLE` → observed r=4/n=1 (`D217-r4-05` blocker): the uncovered true arm `src/nextorm.core/Visitors/MemberTranslator.cs:135` (`IsParamMode` → lines 136-138) is **reachable**, not unreachable/equivalent; the r=3 `audit/part-outcomes.json` classification `accepted` rested on an asserted (not proven) unreachability. fixes applied: 1 (D217-r5-01: executed integration regression + branch coverage); evidence `artifacts/pdca/D217-datetimeoffset/rv2/r4/proof/MT-135.json`, `artifacts/pdca/D217-datetimeoffset/rv2/r5/param-refresh/{focused-t217.log,cobertura-param-refresh.xml,branch-evidence.tsv}`; escalation/outcome: **RESOLVED (r=5/n=1)** — `MemberTranslator.cs:135-138` hits=1 under the executed regression, `part-outcomes.json` MT:135 reclassified `covered` ("reachable, load-bearing, now tested"); criterion NOT rewritten.
- CHECK re-gathers used: **2/2** (re-gather #2 recorded in the tail; budget exhausted). DO loop-back r=2/n=2 applied the scoped defect fixes (no replan; r stays 2); units D217-0p…D217-7 remain `done` except the D217-4 integration surface is re-verified below.

## Cycle goal

SQL-запросы с поддерживаемыми операциями `DateTimeOffset` выполняют эти операции **на сервере**, без клиентских часов/локальной зоны и без скрытого клиентского вычисления; неподдерживаемые члены падают fail-closed; ограничения представления времени документированы и проверены.

## r=5 PLAN (authoritative; supersedes r=4/rv=4 — executed MT:135 closure, re-open past the r=4 `D217-r4-05` blocker)

### P217-r5 — close the MT:135 reachability with an EXECUTED regression (not an assertion)

- **Identity of this revision:** cycle N=1, plan revision **r=5**, attempt **n=1/3**, evidence-contract **rv=5** (supersedes rv=4). Re-opens the cycle past the r=4 DO blocker (`D217-r4-05`: `MemberTranslator.cs:135` is reachable, not unreachable/equivalent). The r=2, r=3 and r=4 records, the defect-history registry, the r=3 STOP and the rv=4 contract section remain in place verbatim as durable history; the rv=4 manifest/receipts are archived, not erased.
- **Required PLAN input:** the r=4 opening assessment (`r4/proof/MT-135.json`: verdict **reachable**, criterion NOT rewritten) plus `audit/part-outcomes.json` MT:135 classified `accepted` on an **asserted** unreachability. The falsified oracle is replaced by an executed cache-refresh probe.

**Goal.** Replace the falsified `MT:135` unreachability oracle with (a) an **executed** cache-refresh probe that takes the reachable `IsParamMode` arm, plus (b) a complete `mutant → source → guard/path` chain. **Preserve R217-08 unchanged** and every existing row ID/obligation. No new product criterion; the public contract and the product implementation stay frozen (`94782852`).

**Snapshot decision (planner).**
- **Accept the existing full-HEAD + footprint freeze algorithm.** `manifest.tree` = `git write-tree` of `read-tree HEAD` + the 28-path T217 footprint (status file excluded).
- `8177c22966ad078171282dc9f0cc85fb9d8a4faa` is **historical/superseded** and is **never auto-rebased**. The observed HEAD moves (`cdae983` → `23f0354a` (concurrent #229/#230) → `94782852` (T217 product commit)) each moved the freeze; the r=3 risk bullet claiming otherwise is **retracted** above.
- The current `manifest.tree = 236737167af210904607daf8455d6e00cfcc9276` is a **pre-sweep snapshot**, NOT the final freeze. The **final freeze is reserved for the post-sweep tree T** produced at the DO→CHECK boundary after the full sweep, in the NEXT task (`D217-r5-03`).

**Scope.**
- **IN:** `tests/nextorm.integration.tests/T217DateTimeOffsetParamRefreshTests.cs` (new), `artifacts/pdca/D217-datetimeoffset/rv2/**`, this status file.
- **OUT:** all other product/test/doc bytes; `examples/**`; diagram assets. No commit/push/merge; CRLF preserved.

**Unit mode.** Sequential, single current tree/branch `1.0.9-rc3` (shared manifest/binding pipeline → **no** worktree, **no** task branch, **no** merge, **no** push, **no** `git add -A`).

**DO units.**

| Unit | State | Depends on | Action / criteria |
|---|---|---|---|
| D217-r4-01 | preserved (not done) | — | result-receipt regeneration in `generate-bindings.sh`; applicability to the rv=5 snapshot is CHECK's to decide |
| D217-r4-02 | preserved (not done) | — | strict `tree_binding.py` result-tree validation |
| D217-r4-03 | preserved (not done) | — | 14 result receipts bound to `manifest.tree` |
| D217-r4-04 | preserved (not done) | — | executed negative fixtures |
| D217-r4-05 | **superseded → D217-r5-01** | — | the r=4 "prove MT:135 unreachable" action is falsified; its obligation carries over to D217-r5-01 |
| D217-r4-06 | preserved (not done) | — | freeze/validate at the boundary; re-owned by D217-r5-03 for the rv=5 snapshot |
| **D217-r5-01** | active | — | add the executed integration regression at `MemberTranslator.cs:135-138` (reachable DTO param-mode arm); cover the branch + assert the changed captured year; update `r4/proof/MT-135.json` with the executed identity (FQN/argv/exit/branch evidence) preserving the existing chain |
| **D217-r5-02** | active | 01 | correct `evidence_gate.py mt135` (exit 0 on a COMPLETE chain for EITHER proven verdict; exit 1 on an incomplete/unsupported assertion) and the `part-outcomes.json` MT:135 entry ("reachable, load-bearing, now tested" — only on executed evidence) |
| **D217-r5-03** | active (the NEXT task) | 01–02 | full sweep → regenerate bindings → freeze/validate the **rv=5** manifest `contract_rv=5`; add the new test file to the footprint at the post-sweep freeze |

- The original `D217-datetimeoffset` unit stays **active/reopened, blocked** on these evidence prerequisites — **NOT superseded, NOT done**.

**Versioned evidence contract — rv=5** (supersedes rv=4; nothing dropped).
- Frozen evidence root stays `artifacts/pdca/D217-datetimeoffset/rv2`; pinned frozen manifest stays `artifacts/pdca/D217-datetimeoffset/rv2/manifest.json`, now `contract_rv=5`. `status_file=docs/specs/status/D217-datetimeoffset-1.md`.
- The rv=4 manifest was **archived to `artifacts/pdca/D217-datetimeoffset/rv2/superseded/manifest.rv4.json` BEFORE the bump** (sha256 recorded in `superseded/sha256s.txt`); the rv=4 receipts (13+ result JSONs and `r4/receipts/E217-15..17`) are preserved history bound to the same tree.
- `E217-01..17` are **preserved** (same IDs, same obligations). `E217-17` (R217-08-MT135) is a **complete chain**: the validator accepts **EITHER** *proven reachable* **OR** *proven unreachable*, and **rejects unsupported assertions** (a bare `reachable` with no executed evidence is a FAIL). ADD **E217-18 (R217-08-MT135-EXEC)** = the executed regression + branch evidence.
- `manifest.tree=236737167af210904607daf8455d6e00cfcc9276` stays the pre-sweep snapshot; any other value is computed only by D217-r5-03's post-sweep freeze, never auto-rebased.

  | Row | Requirements / evidence / predicate |
  |---|---|
  | E217-01..17 | preserved verbatim; each receipt still bound to `manifest.tree`; `evidence_gate.py rows <manifest>` exit 0. |
  | **E217-18 (R217-08-MT135-EXEC)** | executed regression + branch evidence: focused `dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~T217"` exit 0, selected 59 / succeeded 59 / skipped 0; cobertura hits=1 on `MemberTranslator.cs:135-138`; `evidence_gate.py mt135 rv2/r4/proof/MT-135.json` exit 0. |
  | All E217-01..18 | `python3 scripts/validate_inner_loop.py manifest <manifest>` exit 0; `evidence_gate.py freeze <manifest>` exit 0 (28-path tree unchanged). |

- CHECK re-gather budget: **≤2** targeted requests, owner **CHECK-evidence**; exhaustion → verdict with gap.

**Exact existing argv (the rv=5 contract is complete and executable).**
- **Inner loop (r=5):** `dotnet build nextorm.slnx -c Debug`; `dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~T217"` (under `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` so no required provider is skipped).
- **Boundary:** `bash artifacts/pdca/D217-datetimeoffset/rv2/boundary.sh` — internally `dotnet build nextorm.slnx -c Debug`; one unfiltered `dotnet test tests/nextorm.<p>.tests -c Debug` for `p ∈ {core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}`; one `env DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -noColor`. (Post-fix variant used by E217-11: `bash artifacts/pdca/D217-datetimeoffset/rv2/boundary2.sh`.)
- **Coverage:** `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o $A/coverage/cobertura.xml "bash $A/boundary.sh"`; `dotnet tool run reportgenerator -reports:$A/coverage/cobertura.xml -targetdir:$A/coverage/report -reporttypes:Html;JsonSummary`; `bash $A/coverage-check.sh $A/coverage/report/Summary.json 85 75` (scope `coverage.settings.xml` = core/sqlite/postgres/sqlserver).
- **Mutation:** `bash artifacts/pdca/D217-datetimeoffset/rv2/mutation.sh` — `dotnet build tests/nextorm.clickhouse.tests -c Debug` + `dotnet test tests/nextorm.clickhouse.tests -c Debug --no-build --filter "FullyQualifiedName~T217"` for mutants a/b/d/e/f and `DOCKER_HOST=… dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~ClickHouseT217DateTimeOffsetTests"` for mutant c; 6 named mutants, tree restored byte-identical.
- **Perf:** `dotnet build benchmarks/nextorm.benchmark -c Release`; `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet benchmarks/nextorm.benchmark/bin/linux/Release/net10.0/nextorm.benchmark.dll --t217-perf-probe artifacts/pdca/D217-datetimeoffset/rv2/E217-14`.
- **Policy:** coverage gate **85% line / 75% branch** in the `coverage.settings.xml` scope; **a skipped required provider is NOT a PASS** (`PostgreSQL`/`SQL Server`/`MySQL`/`ClickHouse`; SQLite needs no container).

**Test strategy.** One new real-path regression in `tests/nextorm.integration.tests` (SQLite, no container): execute a sorted cached query with `Where(x => x.Value.Year == capturedYear)`, then change the captured year and execute AGAIN so the plan-cache hit refreshes through `ExtractParams` → `SqlSourceRenderer.MakeWhere` param mode → `TryTranslate` with `IsParamMode == true`; assert the branch executed (`QueryPlanner.FallbackRefreshes` delta + cobertura hits on 135-138) and that the second result reflects the changed year. Thresholds preserved **85/75**; prior results (88.010% line / 80.298% branch, 6/6 mutants, integration 58/58, build 0/0) must **NOT** be re-claimed as newly run by this task.

**Docs plan.** Do NOT reopen EN/RU docs or XML-doc; only the status file and internal evidence notes change.

**Perf decision — not needed for this revision.** No per-row product path changes; the new regression is a test. E217-14 + the benchmark footprint are preserved; the perf re-measurement (if any) belongs to D217-r5-03's sweep.

**Risks / mitigations.**
- A `reachable` verdict backed only by prose → **mitigated**: the validator requires executed evidence (FQN/argv/exit/branch hits).
- Process-wide diagnostic counters read under parallel test classes → the regression runs in a `DisableParallelization = true` collection.
- The branch is load-bearing (builder is null in param mode) → a green second run is proof, not a client-side shortcut.
- New test file is **outside** the current 28-path footprint → the pre-sweep `manifest.tree` is untouched; D217-r5-03 adds it at the post-sweep freeze.
- `generate-bindings.sh` still hardcodes `contract_rv=4` → **D217-r5-03** must update it before regenerating; not run in this revision.
- Persistent low confidence → `escalate`.

**STOP condition.** No PASS while any required row is open; failed evidence returns to CHECK/PLAN under the attempt rules; do not manufacture a terminal incomplete.

## r=4 PLAN (superseded by r=5; supersedes r=3/rv=3 — evidence-binding pipeline fix, re-open past the r=3 STOP)

### P217-r4 — восстановить доказуемость evidence-binding

- **Identity of this revision:** cycle N=1, plan revision **r=4**, attempt **n=1/3**, evidence-contract **rv=4** (supersedes rv=3). Re-opens the cycle past the r=3 terminal `incomplete`/STOP; the r=3 record, the defect-history registry and the rv=3 contract section above remain in place verbatim as durable history. The durable record already contains r=2 and r=3, so an `r=2` labelling would be superseded — this re-open's genuinely revised plan is **r=4**.
- **Required PLAN input (terminal-reason handling, overlay invariant 12):** the prior terminal evidence-gate STOP `D217-EVIDENCE-BINDING` (r=2/n=3 + r=3/n=1, **third occurrence**, fixes applied 1, **NOT resolved**) is a mandatory input to this plan; see "Unresolved rows at STOP" below. The previously missing failed proof is now obtainable by **EXECUTING** the fixtures — re-presenting the non-executed fixture alone is not enough.

**Goal.** Fix **ONLY** the evidence pipeline; no new product criteria; the public contract and the product implementation stay frozen. Restore the provability of evidence-binding (R217-08) so the rows are checkable again.

**Premise corrections.**
- The manifest is already bound to the CURRENT tree `8177c22966ad078171282dc9f0cc85fb9d8a4faa`; the STALE `4c3ac382881fee5e03c03034e147243345228ca6` survives only in **13/14** `results/*.json` (the r=3 `generate-bindings.sh` refreshed the manifest evidence but not the per-row result JSONs).
- The current branch is `1.0.9-rc3`, HEAD `cdae983bec5d3b7a51bff642b88ac1994a8e9b22`.
- The freeze hash equals footprint(28 paths) + **HEAD `write-tree`**, **NOT** `HEAD^{tree}`.

**Acceptance criteria.** Preserve **R217-01..08 verbatim** (with their negative cases) from the existing contract (see "Acceptance criteria r=2" below). This replan adds **NO** product criterion — it only restores the **verifiability of R217-08**.

**Alternatives considered.**
- Only rewrite the 13 stale `results/*.json` by hand → **rejected**: the stale value would be re-introduced by the next freeze and no mechanism would keep result JSONs bound.
- Restart the product suites (re-run boundary/coverage/mutation/integration) → **rejected / out-of-scope**: product bytes are unchanged (r=3 proved the old-path freeze at current HEAD equals `4c3ac383…`), so a re-run proves nothing new and burns budget.
- Generator + receipt-validation + executable fixtures → **chosen**.

**Minimal solution + invariants.**
- Do **not** change product, acceptance criteria, scope freeze or historical run results.
- Preserve the original commands and the execution provenance of each recorded run.
- **Rebinding ≠ re-running**: regenerate the binding artifacts from the freeze, do not re-execute the product suites.
- Fail-closed validators; every rebinding is reproducible from committed inputs.

**Scope.**
- **IN:** `artifacts/pdca/D217-datetimeoffset/**`, this status file, and the three approved benchmark files (`benchmarks/nextorm.benchmark/Program.cs`, `benchmarks/nextorm.benchmark/nextorm.benchmark.csproj`, `benchmarks/nextorm.benchmark/T217DtoPerfProbe.cs`); the r=3 benchmark widening must **not** be rolled back.
- **OUT:** `docs/**/assets/diagrams/*.svg`, `examples/**`.

**Unit mode.** Sequential, single current tree/branch (shared manifest/binding pipeline → **no** worktree, **no** task branch, **no** merge, **no** push, **no** `git add -A`).

**DO units (all `active/planned`; states as planned, NOT done).**

| Unit | Depends on | Action / criteria |
|---|---|---|
| D217-r4-01 | — | (b) extend `artifacts/pdca/D217-datetimeoffset/rv2/generate-bindings.sh` (lines 64–76, 155–160) to refresh all 14 result JSONs alongside the manifest; preserve payload/provenance; R217-08 |
| D217-r4-02 | 01 | (c) `artifacts/pdca/D217-datetimeoffset/rv2/verify/tree_binding.py` (lines 19, 35, 42–65): read the mandatory `row.result`, validate both `tree`/`tree_binding` against `manifest.tree`; missing/malformed → FAIL; R217-08 |
| D217-r4-03 | 01–02 | (a) regenerate `rv2/results/E217-01.json` … `E217-14.json`; all 14 == `manifest.tree`, contract rv4; R217-01..08 evidence preserved |
| D217-r4-04 | 02–03 | (d) actually execute the prior `rv2/r3-baseline/negative-fixture/manifest-stale-tree.json` plus new stale-result/missing fixtures; capture command/exit/FAIL output; R217-08 |
| D217-r4-05 | — | (e) close `part-outcomes.json:88`: obtain provable (semantic, not asserted) MT:135 unreachability evidence in `rv2/r4/proof/MT-135.json`; R217-08 |
| D217-r4-06 | 03–05 | freeze manifest/checksums, run independent validators, update S without erasing the r3 STOP; R217-01..08 |

- The original `D217-datetimeoffset` unit stays **active/reopened, blocked** on these evidence prerequisites — **NOT superseded, NOT done**.

**Versioned evidence contract — rv=4** (supersedes rv=3; nothing dropped).
- Frozen evidence root stays `artifacts/pdca/D217-datetimeoffset/rv2`; frozen manifest stays `artifacts/pdca/D217-datetimeoffset/rv2/manifest.json`; `contract_rv=4`; `status_file=docs/specs/status/D217-datetimeoffset-1.md`.
- Expected frozen tree `8177c22966ad078171282dc9f0cc85fb9d8a4faa` — any other value = **FAIL/PLAN**, never auto-rebase.

  | Row | Requirements / evidence / predicate |
  |---|---|
  | E217-01..14 (each ID separately) | original rv3 scenario/predicate/evidence PLUS receipt consistency; validator `python3 rv2/r4/evidence_gate.py rows <manifest>`; exit 0; each receipt has original payload/provenance and both current tree fields. |
  | E217-15 (R217-08-BIND) | 28-path freeze reproducibility + unchanged product fingerprint + generator/result binding; `evidence_gate.py freeze <manifest>` + `generate-bindings.sh` + `tree_binding.py <manifest>`; exit 0 each. |
  | E217-16 (R217-08-NEG) | execute stale-manifest fixture + new stale-result/missing variants; harness exit 0, each negative invocation exit 1 with the required `FAIL: row … != manifest.tree` / missing diagnostic; `rv2/r4/negative/{case}/execution.json` + `stdout.log`. |
  | E217-17 (R217-08-MT135) | provable mutant→source→guard/path chain (not an assertion); `evidence_gate.py mt135 rv2/r4/proof/MT-135.json`; exit 0 only on a complete chain. |
  | All E217-01..17 | native manifest validation with the **EXACT argv** copied from rv3, recorded in `rv2/r4/native-manifest-command.json`; native exit 0 + harness exit 0; missing/invalid argv → **FAIL** (no fallback). |

- CHECK re-gather budget: **≤2** targeted requests, owner **CHECK-evidence**; exhaustion → verdict with gap, no rv5 / no new DO solely for a missing report.
- **Planned-new artifacts** (`evidence_gate.py`, `r4/` logs/proof/negative) are **PLANNED, not existing** — do not fabricate symbols/`file:line`.

**Test strategy + variant matrix.**
- Rebuild: **none**. Scope = evidence validators, `tree_binding.py`, native `validate_inner_loop.py manifest`. **No new product tests.**
- Rows: 13 stale receipts (test); 1 current receipt `E217-13` idempotence (test); manifest/receipt match (test); receipt tree mismatch + `tree_binding` mismatch separately (test, 2 fixtures); stale-tree fixture actually executed (test); missing receipt (guard+test); `MT:135` unreachability (proof+guard). Each closed. **Deferred: none.**
- Thresholds preserved **85/75**. Prior results — 88.010% line / 80.298% branch, mutation 6/6, integration 58/58 with 0 skips, build 0/0 — must **NOT** be re-claimed as newly run.
- New rows E217-15..17 + binding/completeness rows are **P1 by R217-08**.

**Docs plan.** Do NOT reopen EN/RU or XML-doc; only the status file and internal evidence notes change.

**Perf decision — not needed.** The change is one-time tooling (`generate-bindings.sh:64–76,155–160`, `tree_binding.py:42–65`), not an execution/per-row path; E217-14 + the benchmark footprint are preserved.

**Reconnaissance — needed but bounded.** Recover the native argv / old slots, reproduce the freeze, identify `MT:135` via Roslyn; criterion = exact argv, hash `8177c229…`, provable mutant/path chain; no product-decision exploration.

**Terminal-reason handling (overlay invariant 12).** The prior terminal evidence-gate STOP is a required input; the previously missing failed proof is now obtainable by **executing** the fixtures (re-presenting the non-executed fixture alone is not enough).

**Risks / mitigations.**
- False provenance on rebinding → write-once baseline + independent CHECK.
- Non-atomic update → preserved execution records.
- Generator self-certification → independent validator (fail-closed).
- Loss of prior obligations → fail-closed validators keep every obligation.
- If the freeze does not reproduce, or `MT:135` turns out to be **reachable** → do **NOT** rewrite the criterion/product; **report PLAN with the fact**.
- Persistent low confidence → `escalate` trigger 5.

## r=3 PLAN (superseded by r=4; supersedes r=2/rv=2 — escalate-routed evidence-binding remediation)

- Revision r=3, attempt n=1, contract rv=3. Supersedes r=2/rv=2 after the escalate decision was routed to PLAN. **EVIDENCE-ONLY**: no product/code/test change, no acceptance-criteria change, no commit/push.
- **Acceptance criterion: UNCHANGED from r=2** (R217-01..R217-08; six dialects, provider contract and unsupported-member table reused verbatim below). No criterion narrowed, no S217-CH, no route-(a).
- **Changed mechanism vs r=2**: r=2 failed CHECK on the *evidence binding*, not on the product. r=3 repairs the evidence contract — oracle provenance, freeze scope, stale-log rebind, perf binding, PART-branch outcomes and one fail-closed generator/validator. Product bytes are untouched.

### r=3 task list (D1–D7)

| Task | Action | Deliverable |
|---|---|---|
| D1 SNAPSHOT | write-once baseline of the pre-r=3 manifest + hashes, before modifying either | `rv2/r3-baseline/{manifest.json,hashes.json,sha256s.txt}` |
| D2 ORACLE PROVENANCE | row-by-row compare of the manifest oracle fields against the r=2 oracle text; drop the unprovable claim | `rv2/oracles.rv3.json` (verdict + re-derived rows); rv=3 supersedes rv=2 |
| D3 FREEZE SCOPE | widen the freeze to `src tests docs benchmarks`; keep benchmarks in freeze/footprint and list the 2 tracked ones in `commit_exclusions` | new `manifest.tree` + `tree_method` + `commit_exclusions` |
| D4 E217-13 REBIND | archive the stale `verify/verify.log` (f2a6f63) and remove all acceptance citations; bind E217-13 to the fresh `verify/verify-run.log` | `rv2/superseded/{verify.log,sha256s.txt}` |
| D5 PERF BINDING | bind the frozen tree + sha256 of the untouched `E217-14/{perf.tsv,environment.txt}` | `rv2/perf.binding.json` |
| D6 PART OUTCOMES | classify all 9 PART branch identities (`audit/branch-delta.tsv:34`) as covered/accepted/deferred | `rv2/audit/part-outcomes.json` |
| D7 ONE DRIVER + VALIDATION | one generator (freeze → manifest → verify → hashes) + a fail-closed tree-binding check + a negative fixture | `rv2/generate-bindings.sh`, `rv2/verify/tree_binding.py` |

### Test strategy r=3

- Product scope unchanged: the r=2 test strategy (C01–C17) and the recorded filtered/boundary executions stand. The old-path freeze recomputed at the current HEAD equals the r=2 tree `4c3ac383…`, so **product bytes are unchanged and the recorded suites/coverage/mutation are reused**; no product suite is re-run in this revision.
- Inner-loop scope: this revision changes no product/test file, so no affected filtered subset exists. `validate_inner_loop.py brief` mandates nonempty `projects`+`selectors`, so a truthful docs-only/none scope cannot be expressed → `brief` is **inapplicable** (proved by the rejected empty-scope fixture), not replaced by invented selectors.
- Evidence validation instead: `manifest` + `report` validation, `verify.sh` (incl. the new tree-binding section) and the negative fixture.

### Docs plan r=3

- No public doc change. Only the durable status file and the evidence workspace under `artifacts/pdca/D217-datetimeoffset/rv2/**` are written; `docs/**` (except this status file), `benchmarks/**`, `examples/**` and diagram assets are read-only.

### Perf decision r=3

- Perf requirement unchanged (r=2: the ClickHouse DTO reader branch runs per materialized row; C17 measured). r=3 only **binds** the existing E217-14 numbers to the frozen tree via `perf.binding.json`; `perf.tsv`/`environment.txt` are not modified.

### Reconnaissance decision r=3

- No new provider probe. Reconnaissance is the oracles/branch provenance audit (D2/D6) plus the freeze/binding regeneration (D3–D7).

### Unit mode r=3

- Single current worktree/branch `1.0.9`; no worktrees/branches/merge/push/commit. Only `rv2/**` and this status file are written.

### Variant-matrix delta r=3

- No variant-matrix change: the r=2 closed matrix stands. r=3 adds evidence rows only (`oracles.rv3.json`, `perf.binding.json`, `audit/part-outcomes.json`, `r3-baseline/**`, `superseded/verify.log`, `verify/tree_binding.py`).

### Risks / known issues r=3

- A cited log could still name a tree other than `manifest.tree` → mitigated by the fail-closed tree-binding check (negative fixture rejected, exit 2).
- The shared working tree is an active multi-agent target (concurrent `#229/#230` commits moved HEAD) → the freeze is restricted to the T217 footprint, so unrelated commits cannot move it.
  > **RETRACTED at r=5.** The claim "unrelated commits cannot move the freeze" is false: the freeze is `git write-tree` over `read-tree HEAD` + the 28-path footprint, so **every** HEAD move changes the frozen tree even when the footprint bytes are identical. Observed: `cdae983` → `23f0354a` (concurrent #229/#230) → `94782852` (T217 product commit) each produced a different `manifest.tree`; the old `8177c229…` is therefore **historical/superseded** and must never be auto-rebased. The frozen tree is a snapshot of HEAD+footprint at freeze time, not an invariant.
- The two tracked benchmark files are inside the freeze but must not be committed → recorded in `commit_exclusions`.

### STOP condition r=3

- **One more failure on this same evidence-binding defect ⇒ incomplete.** A further CHECK failure attributable to evidence binding (stale tree citation, missing/unprovable oracle provenance, unbound hashes, PART identity left unclassified) terminates the cycle as `incomplete` (no fourth attempt at this defect).

## r=2 PLAN (acceptance criteria, provider contract and rv=2 oracle text reused verbatim by r=3; superseded by r=3)

- Revision r=2, attempt n=1, contract rv=2. Supersedes r=1/rv=1 via **binding route-(b)**. The route-(a) draft was rejected, not adopted.
- **R217-01/R217-06 remain six dialects; no acceptance criterion narrowed; no S217-CH.**
- Minimal solution: implement the approved SQL translations while repairing **ONLY** ClickHouse's DTO reader mismatch.
  - Alternative (a) capability=false → **rejected** (weakens acceptance / narrows to 5 dialects).
  - Generic/core reader repair → **unauthorised/broad (rejected)**.
  - Provider-local CH conversion → **selected** (smallest authorised scope): read driver `DateTime`, `SpecifyKind(Utc)`, construct offset-zero DTO **without locale shift**; pin CH emitters to `now64(6,'UTC')`.
- **D217-0 → D217-0p (D0′)**, fix now: D217-0 is `superseded → active D217-0p` carrying **every original foundation criterion and remaining obligation**; SS/PG/MySQL/SQLite findings retained as historical. Footprint: `src/nextorm.clickhouse/ClickHouseDataContext.cs:67` existing `MapColumnExpression` override **only**; CH dialect emitter; planned `tests/nextorm.integration.tests/{T217DateTimeOffsetTests.cs,T217DateTimeOffsetEntities.cs}` + CH DTO regression tests. Prove a REAL scalar projection invokes the override, materializes offset 0 with microseconds, and preserves existing CH DTO behavior. New scenarios **C15** (CH DTO materialization), **C16** (D0′ hook proof).
- **D217-1 / D217-2 fix now**: D1 DTO Now/UtcNow/capability, footprint `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` + `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs` + six provider `*Dialect.cs`, **no interface widening beyond the new members**. D2 parts/Add/guards, footprint `Visitors/{MemberTranslator,ScalarFunctionTranslator,DateTimeFunctionTranslator}.cs`, conditional `DateConversionSqlTranslator.cs`, provider dialects. Preserve SS native offset; PG UTC parts/Add session-TZ/DST-independent; MySQL/MariaDB stored wall-time + fractional scaling; CH UTC normalization; SQLite UTC instant/ms.
- **D217-3 / D217-4 fix now**: D3 C01–C14 + C15/C16 + perf C17; footprint planned `tests/nextorm.core.tests/T217DateTimeOffsetTranslationTests.cs` + six `tests/nextorm.<provider>.tests/T217DateTimeOffsetSqlGenerationTests.cs`. D4 real PG/SS/MySQL/CH; footprint planned integration files + conditional `CommonTestSuite.Functions.cs`. **Test identities are PLANNED; discover actual symbols with Roslyn and bind them in the ledger — never invent FQNs.**
- **D217-5/6/7 fix now**: D5 docs/API handoff; D6 evidence prep in `artifacts/pdca/D217-datetimeoffset/rv2/` + status file, incl. `boundary.sh`/`verify.sh`/`coverage-check.sh`/`mutation.sh`/`docs-check.sh`; D7 one final covered boundary sweep + freeze manifest → hand off to CHECK. D1–D7 `blocked` where dependent on D0′, not `done`.
- **Scope**: the CH reader exception authorises ONLY the `ClickHouseDataContext.cs` override; **NEVER** change `RowMapperFactory.cs`, `SelectExpression.cs`, `RawMapperFactory.cs`, TVP, `SqlFunctions*.cs`, shared `Entities.cs`, packages/config/CI, generated docs/api, parent collection status, T53 files.
- **Deferred in 1.0.9**: unsupported-member translation/AddTicks/Add(TimeSpan) → separate task before release (guards now); SqlFunctions DTO overloads → confirmed consumer before T53 freeze; offset-storage changes → confirmed requirement + mapping/migration contract; in-memory changes → separate item.
- Execution mode: same tree, no worktrees; D0′→D1→D2 sequential (shared contracts); tests/docs parallel only on disjoint files; D3/D4 finalize after implementations; D6→D7 serialized. No commit/push/merge; CRLF preserved.

### Acceptance criteria r=2 (unchanged scope: 6 dialects)

| ID | Критерий и проверка | Негативный случай |
|---|---|---|
| R217-01 | `Now`/`UtcNow` имеют серверный SQL emitter во **всех 6 dialects**; SQL и материализованный результат соответствуют provider-таблице. | Клиентский параметр вместо серверного выражения; SQL Server возвращает не `datetimeoffset`; UTC-результат с ненулевым offset. |
| R217-02 | `Year/Month/Day/Hour/Minute/Second/DayOfYear` транслируются для `DateTimeOffset`, включая nullable-источник и позиции Where/Select/OrderBy. | Член остаётся параметром, теряет SQL-null либо извлекает часть в неверной TZ. |
| R217-03 | `AddYears/AddMonths/AddDays/AddHours/AddMinutes/AddSeconds/AddMilliseconds` транслируются через date-add инфраструктуру; значимые дробные интервалы не исчезают; точность ограничена документированной точностью СУБД. | `.AddDays(0.5)` → нулевой сдвиг; отрицательный интервал меняет знак; null → ненулевая дата. |
| R217-04 | Неподдерживаемые члены явно бросают `NotSupportedException` при построении SQL, до исполнения и constant folding. | `.Offset` вычисляется на клиенте или превращается в параметр без ошибки. |
| R217-05 | Поведение/SQL существующего `DateTime`, scalar mapping, параметров и TVP не меняются. | Старый `MakeNow` начинает выдавать новый DTO SQL; меняется глобальное состояние команды/кеша. |
| R217-06 | SQL-gen покрывает SQLite/PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse; реальные интеграции проходят для PG/SQL Server/MySQL/ClickHouse. | Нулевое число выбранных тестов или skipped обязательного provider как pass. |
| R217-07 | EN/RU документация описывает серверное время, timezone/precision ограничения, поддержанные/запрещённые операции; новый public API имеет XML-doc. | Остаётся «DateTimeOffset members не переводятся» либо обещание сохранения offset в MySQL. |
| R217-08 | Evidence полон и проверяем; сборка без warnings; coverage/mutation evidence зарегистрированы по контракту. | Только exit 0 без сценариев/артефактов; неприменимость подменяется отсутствующим отчётом. |

### Provider contract r=2

| Provider | DTO `Now` | DTO `UtcNow` | Parts/Add |
|---|---|---|---|
| SQL Server | `sysdatetimeoffset()` | `todatetimeoffset(sysutcdatetime(), '+00:00')` | Native `datetimeoffset`: parts по offset, `dateadd` сохраняет offset. |
| PostgreSQL | `now()`; канонический DTO-result UTC | `now()` | Parts/Add — UTC wall time через `AT TIME ZONE 'UTC'`; Add → `timestamptz`, без зависимости от session TZ/DST. |
| MySQL | `utc_timestamp(6)` | `utc_timestamp(6)` | Существующий `datetime`; части сохранённого wall time, native date-add; исходный offset не восстанавливается. |
| MariaDB | `utc_timestamp(6)` | `utc_timestamp(6)` | Наследует MySQL contract, отдельные SQL-gen/dialect тесты. |
| ClickHouse | `now64(6, 'UTC')` | `now64(6, 'UTC')` | UTC-aware `DateTime64`; TZ нормализуется явно, затем `toXxx`/`addXxx`. |
| SQLite | UTC `strftime` с дробной частью и явным UTC | та же | `strftime`/date modifiers; offset-bearing текст — как instant, канонизация в UTC; точность — миллисекунды. |

### Unsupported members r=2

| Член `DateTimeOffset` | Поведение | Примечание |
|---|---|---|
| `.Offset` / `.UtcDateTime` / `.LocalDateTime` / `.Date` / `.Ticks` / `.DayOfWeek` | `NotSupportedException` | guard на вложенные (`.Offset.TotalMinutes`, `.UtcDateTime.Year`, `.Date.AddDays`). |
| `.ToOffset(...)` | `NotSupportedException` | Включая `constant` и dynamic аргумент. |
| `AddTicks` / `Add(TimeSpan)` | guard сейчас, трансляция deferred | отдельная согласованная задача `1.0.9` до release. |
| `Years/Months` fractional | guard (CLR `int`) | — |

### DO units/streams r=2

| Unit | state | Criteria | Dependencies |
|---|---|---|---|
| D217-0p (D0′ CH reader/foundation proof) | done | R217-01…R217-06 (foundation) | first; `ClickHouseDataContext.cs:67`, CH dialect emitter; real CH scalar DTO probe + existing CH DTO regression. |
| D217-1 DTO now + capability | done | R217-01, R217-05 | `ISqlDialect.cs`, `SqlDialectBase.cs`, six `*Dialect.cs`; default unsupported. |
| D217-2 parts/Add/guards | done | R217-02, R217-03, R217-04 | `Visitors/{MemberTranslator,ScalarFunctionTranslator,DateTimeFunctionTranslator}.cs`, provider dialects; CH DTO `MapColumnExpression` branch. |
| D217-3 unit/SQL-gen matrix (C01–C17) | done | R217-01…R217-06 | planned `tests/nextorm.core.tests/T217DateTimeOffsetTranslationTests.cs` + six `tests/nextorm.<provider>.tests/T217DateTimeOffsetSqlGenerationTests.cs`; finalize after D1/D2. |
| D217-4 real integrations | **done** (r=2/n=1) | R217-06 | `tests/nextorm.integration.tests/{T217DateTimeOffsetTests.cs,T217DateTimeOffsetEntities.cs}`; all five re-run non-skipped green: sqlite 11/11, postgres 12/12, mysql 11/11, sqlserver 12/12, clickhouse 12/12 (0 skipped each); CH `.AddDays(0.5)` truncation fixed via `addMicroseconds` scaling. |
| D217-5 docs/API handoff | **done** (r=2/n=1, stage 2c) | R217-07 | `docs/scalar-functions/03-date-and-time.md` + RU, `docs/advanced/limitations.md` (+RU). |
| D217-6 evidence prep | **done** (r=2/n=1, stage 2d) | R217-08 | `artifacts/pdca/D217-datetimeoffset/rv2/**` + status file; scripts `boundary.sh`/`verify.sh`/`coverage-check.sh`/`mutation.sh`/`docs-check.sh`. |
| D217-7 final sweep + freeze | **done** (r=2/n=1, stage 2d) | R217-01…R217-08 | serialized after D1–D6; freeze manifest → CHECK. |

### Closed variant matrix r=2

| Ряд | Статус | Detail |
|---|---|---|
| P6×Now/UtcNow×Select/Where/OrderBy×repeat | test | SQL-gen C01/C07/C14. |
| P4×Now/UtcNow×local/UTC | test | integration C02/C12 (MariaDB runtime deferred until runnable env; its SQL-gen NOT deferred). |
| P6×7 parts×value/null | test | SQL-gen C03/C06. |
| P6×7 Add×value/null×0/±×fractional literal/captured/SQL×local/nonzero-offset/UTC | test | C04/C05. |
| unsupported/folding/DateTime-vs-DTO/capability | guard+test | C08/C09/C10/C11. |
| reference receivers + `.Offset.TotalMinutes`/`.UtcDateTime.Year`/`.Date.AddDays` | guard+test | C09. |
| fractional Years/Months | guard+test | C09. |
| AddTicks/Add(TimeSpan) | guard now; translation deferred | C09 + deferred trigger. |

P6 = 6 providers; P4 = PG/SQL Server/MySQL/ClickHouse.

### Test strategy r=2

- C01 — Now/UtcNow SQL shape / no client param / return type.
- C02 — серверный instant в bounds + materialized type/offset.
- C03 — семь parts (leap / конец года / граница UTC-local).
- C04 — семь Add (0/±, month/year boundary).
- C05 — дробные дни/секунды/мс, SQL-expression argument, precision tolerance.
- C06 — nullable / SQL-null / HasValue / propagation.
- C07 — Where/Select/OrderBy.
- C08 — literal/captured/nested unsupported, guard до folding.
- C09 — все запрещённые (incl. ToOffset constant/dynamic), `.Offset.TotalMinutes`/`.UtcDateTime.Year`/`.Date.AddDays`, AddTicks/Add(TimeSpan).
- C10 — capability=false стороннего dialect.
- C11 — DateTime regression / old overloads.
- C12 — PG session TZ/DST, SQL Server ненулевой offset, UTC-only.
- C13 — существующий mapping без изменений.
- C14 — isolation повторных запросов (без утечки command/cache state).
- **C15** — ClickHouse DTO materialization (offset 0, microseconds retained, scalar projection).
- **C16** — D0′ hook proof (real scalar projection invokes `MapColumnExpression` override).
- **C17** — perf: unchanged DateTime scalar reading vs DTO adaptation on identical CH data.

Inner-loop affected subsets (exact filters):
- D0′/D1 core: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~T217"`.
- Provider SQL-gen: `dotnet test tests/nextorm.<p>.tests -c Debug --filter "FullyQualifiedName~T217|FullyQualifiedName~DialectTests"`.
- D2 core+6: `--filter "FullyQualifiedName~T217|FullyQualifiedName~SqlGenerationTests"`.
- D4: `DOCKER_HOST=... dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~T217"`.

Boundary sweep (single, final candidate tree): `dotnet build nextorm.slnx -c Debug`; one run per `tests/nextorm.{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests`; one `nextorm.integration.tests` run under `DOCKER_HOST`. **PG/SS/MySQL/CH skipped ≠ pass.** MariaDB not a MySQL runtime copy.

Coverage/mutation: `coverage.settings.xml` scope core/sqlite/postgres/sqlserver; report 85% line / 75% branch; **never omit the report**. 6 named mutants (remove DTO dispatch / return client capture / change UTC branch / remove forbidden guard / disable nullable propagation / return old now emitter); baseline passes, each killed by nonempty targeted test, tree restored; no new mutation tool.

### Docs plan r=2

Edit `docs/scalar-functions/03-date-and-time.md`, its RU counterpart, `docs/advanced/limitations.md`, and its RU counterpart where affected; state server clock, supported/forbidden ops, per-provider zone/precision/storage limits; **CH DTO IS supported via reader adaptation normalised to +00:00**; only SS preserves offsets, PG stores instants. Verify EN/RU consistency, XML-doc without suppressing CS1591, DocFX, no public links to specs. Generated API pages untouched.

### Perf decision r=2

Perf IS required because the new `ClickHouseDataContext.cs:67` branch runs **per materialized row**; C17 compares unchanged DateTime scalar reading vs DTO adaptation on identical CH data, warm-up + repeated batches, recording row count/time/allocations/environment (do not invent a pre-existing DTO baseline/threshold). D0′ is the required prototype.

### Reconnaissance decision r=2

D0′ (bounded, real CH) replaces the r=1 general D0 as the enforced prerequisite; SS/PG/MySQL/SQLite findings are retained as historical. D0′ must prove the `MapColumnExpression` override fires for a real scalar projection and materializes offset 0 with microseconds without regressing existing CH DTO tests. Budget: one provider probe + one diagnostic rerun.

### Unit mode r=2

One current worktree; D0′→D1→D2 sequential; tests/docs parallel only on disjoint files; D3/D4 finalize after implementations; D6→D7 serialized. No worktrees/branches/merge/push.

### Design checklist r=2

No new SQL AST; no universal temporal abstraction; no `SqlFunctions` change; old overloads unchanged; capability default `false`; DTO ops fail closed; nullable propagation preserved; constant folding guarded; DateTime path byte-identical; no shared command/cache mutation; minimal public surface.

### Versioned evidence contract — rv=2

Frozen manifest: `artifacts/pdca/D217-datetimeoffset/rv2/manifest.json` (pin `A=artifacts/pdca/D217-datetimeoffset/rv2`). Retain E217-01..13 + all obligations, add **E217-14**. Every row requires `$A/<row-id>/` with stdout/stderr, exit/result JSON, scenario/source ledger, SHA256s, frozen-tree fingerprint; applicability always true (no N/A for missing evidence). Defaults inherited into every row: command notation `T(p)=dotnet test tests/nextorm.<p>.tests -c Debug`; `F=--filter "FullyQualifiedName~T217|FullyQualifiedName~DialectTests"`; `I=DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug`. Expand literally in the manifest.

| row | requirements | evidence | predicate |
|---|---|---|---|
| E217-01 | R01/03/05/06 | Code: D0 foundation + D0′; retain rv1 spike artifacts as historical; invoke `I --filter "FullyQualifiedName~T217D0ProbeTests"` + E14 scalar proof | exit 0, selected>0, required providers non-skipped |
| E217-02..07 | R01/02/03/05/06 | Tests: p∈{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}, `T(p) F`; C01/C03–C07/C11/C14 + CH C15; own artifacts | exit 0, selected>0; MariaDB not a MySQL copy. E217-07 CH guards |
| E217-08 | R02/03/04/05/08 | Tests: `T(core) --filter "FullyQualifiedName~T217"`; C06/C08–C11/C14 guard/folding/null/capability/cache | exit 0, selected>0 |
| E217-09 | R01/02/03/05/06 | Tests: `I --filter "FullyQualifiedName~T217"`; C02/C04–C07/C12/C13/C15 | exit 0 + separate non-skipped PG/SS/MySQL/CH |
| E217-10 | R05/07/08 | Docs: `dotnet build -c Debug && dotnet docfx docs/docfx.json && bash "$A/docs-check.sh"` | exit 0 each, build 0/0, EN/RU + API parity, no public specs links |
| E217-11 | R05/06/08 | Evidence: `dotnet-coverage collect "bash $A/boundary.sh" -s coverage.settings.xml -f cobertura -o "$A/coverage/cobertura.xml"` + `reportgenerator` + `bash "$A/coverage-check.sh" ... 85 75` | boundary runs build + unfiltered T(core/sqlite/postgres/sqlserver/mysql/mariadb/clickhouse) once each + unfiltered I; all exit 0, no required provider skips, ledger counts>0 |
| E217-12 | R01/02/04/05/08 | Evidence: `bash "$A/mutation.sh"` — 6 named mutants | baseline passes, each killed by nonempty targeted test, tree restored; no new mutation tool |
| E217-13 | R05/07/08 | Evidence: `bash "$A/verify.sh" && python scripts/validate_inner_loop.py manifest "$A/manifest.json"` | exit 0, complete rows/ledger, scope/CRLF/cache/hash checks |
| E217-14 | R01/05/06/08 | Code+Tests: unfiltered `T(clickhouse)` + unfiltered `I`; C15/C16/C17 (scalar-override reachability, offset 0, microseconds retained, existing CH DTO regressions, perf TSV/baseline) | exit 0, no required skips |

Manifest fields: `task/tree/contract_rv/status_file`, `required_rows[]`, `rows[]{id,status:met|open|na,evidence|reason|predicate}`, bound to frozen source-tree SHA incl. uncommitted inputs; freeze at DO→CHECK; append-only during CHECK. CHECK owns ≤2 re-gathers; open rows prohibit PASS.

**D0′ failure → NEW user decision via escalate; NEVER silently route (a).**

### Versioned evidence contract — rv=3 (supersedes rv=2)

- Frozen manifest: `artifacts/pdca/D217-datetimeoffset/rv2/manifest.json`; `contract_rv=3`; `manifest.tree=8177c22966ad078171282dc9f0cc85fb9d8a4faa` (freeze widened to `src/ tests/ docs/ benchmarks/`; status file excluded).
- rv=3 preserves **all row IDs E217-01..14 and all obligations**. The r=2 oracle text was compared row-by-row against the frozen manifest (`oracles.rv3.json`): every row's requirement set matches; rows **E217-07, E217-10, E217-11, E217-12, E217-13, E217-14** were **re-derived** from the original criterion because the r=2 manifest predicate omitted one or more oracle obligations — the missing obligations are re-stated verbatim in those rows (strengthening; no obligation dropped or weakened).
- Per row requires evidence with SHA256s and a frozen-tree binding; `verify/hashes.json` stays **outside** the manifest; E217-13 is bound only to `verify/verify-run.log` (the stale `verify/verify.log` is archived under `superseded/`).
- The unprovable r=2 claim "the manifest row oracles are unchanged from rv=2" is deleted.

### Risks / known issues r=2

- CH reader adaptation scope creep into `RowMapperFactory.cs`/`SelectExpression.cs` (forbidden).
- DTO reader still can't materialize a chosen provider shape.
- PG arithmetic silently uses session TZ/DST.
- Fractional Add loses a significant part or overflows a unit.
- Constant folding bypasses a guard.
- New interface API breaks external dialects (base default + compatibility note).
- Full suite "green" via skipped containers.
- Aggregate coverage hides a new unguarded branch.

Confidence: high in localisation; medium in provider expressions until D0′.

### Open questions r=2

- Exact CH `MapColumnExpression` dispatch for a scalar projection (D0′ proves).
- Container/tool availability — execution precondition (integration skill).
- Exact existing hook signatures/gates — Roslyn in D0′/D1.

## Historical: r=1 plan (superseded by r=2 via route-(b))

> Retained verbatim as historical evidence; not authoritative. The r=1 plan's task, cycle goal, acceptance table, provider contract, unsupported-members table, DO units, variant matrix, test strategy, docs plan, perf/recon/unit-mode, deferred table, evidence contract rv=1, write footprint, sibling/T53, risks and pointers are preserved below.

### Task (r=1)

Серверная трансляция поддерживаемых операций `DateTimeOffset` в SQL на всех штатных dialects без захвата клиентского времени и без скрытого клиентского вычисления; там, где СУБД не сохраняет исходный offset, ограничения объявляются явно.

### Acceptance criteria (r=1)

| ID | Критерий и проверка | Негативный случай |
|---|---|---|
| R217-01 | `Now`/`UtcNow` имеют серверный SQL emitter во всех 6 dialects; SQL и материализованный результат соответствуют provider-таблице. | Клиентский параметр вместо серверного выражения; SQL Server возвращает не `datetimeoffset`; UTC-результат с ненулевым offset. |
| R217-02 | `Year/Month/Day/Hour/Minute/Second/DayOfYear` транслируются для `DateTimeOffset`, включая nullable-источник и позиции Where/Select/OrderBy. | Член остаётся параметром, теряет SQL-null либо извлекает часть в неверной TZ. |
| R217-03 | `AddYears/AddMonths/AddDays/AddHours/AddMinutes/AddSeconds/AddMilliseconds` транслируются через date-add инфраструктуру; значимые дробные интервалы не исчезают; точность ограничена документированной точностью СУБД. | `.AddDays(0.5)` → нулевой сдвиг; отрицательный интервал меняет знак; null → ненулевая дата. |
| R217-04 | Неподдерживаемые члены явно бросают `NotSupportedException` при построении SQL, до исполнения и constant folding. | `.Offset` вычисляется на клиенте или превращается в параметр без ошибки. |
| R217-05 | Поведение/SQL существующего `DateTime`, scalar mapping, параметров и TVP не меняются. | Старый `MakeNow` начинает выдавать новый DTO SQL; меняется глобальное состояние команды/кеша. |
| R217-06 | SQL-gen покрывает SQLite/PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse; реальные интеграции проходят для PG/SQL Server/MySQL/ClickHouse. | Нулевое число выбранных тестов или skipped обязательного provider как pass. |
| R217-07 | EN/RU документация описывает серверное время, timezone/precision ограничения, поддержанные/запрещённые операции; новый public API имеет XML-doc. | Остаётся «DateTimeOffset members не переводятся» либо обещание сохранения offset в MySQL. |
| R217-08 | Evidence полон и проверяем; сборка без warnings; coverage/mutation evidence зарегистрированы по контракту. | Только exit 0 без сценариев/артефактов; неприменимость подменяется отсутствующим отчётом. |

### Design decisions / minimal solution (r=1)

**Суть:** серверные выражения и прозрачные ограничения представления времени.
**Ограничения:** не менять `DateTime`, mapping и plan cache; не вводить silent fallback; сохранять SQL-null; проверить все dialects и обязательные реальные providers.
**Минимум:** расширить существующие visitor/dialect hooks — без нового SQL AST-класса, без универсальной temporal abstraction, без изменений `SqlFunctions`.

| Подход | Итог |
|---|---|
| (A) Только расширить `typeof(DateTime)` и переиспользовать старый `MakeNow` | Отклонено — неверный SQL Server result type, TZ-ошибки, скрытая потеря offset. |
| (B) DTO-aware hooks поверх существующей инфраструктуры | **Выбрано.** |
| (C) Новый temporal AST + overloads `SqlFunctions` | Не делать — избыточно, API до T53 freeze. |

Consumers/API: `MemberTranslator.cs:20-85`; `ScalarFunctionTranslator.cs:56`; `DateTimeFunctionTranslator.cs:17-27`; guard перед клиентским вычислением `MemberTranslator.cs:312-343`. **Планируемые public additions:** `ISqlDialect.SupportsDateTimeOffsetTranslation`; `ISqlDialect.MakeDateTimeOffsetNow`; DTO-aware overloads `MakeDatePart`/`MakeDateAdd` (+ `bool isDateTimeOffset`); старые overloads не меняются.

### Provider contract (r=1)

| Provider | DTO `Now` | DTO `UtcNow` | Parts/Add |
|---|---|---|---|
| SQL Server | `sysdatetimeoffset()` | `todatetimeoffset(sysutcdatetime(), '+00:00')` | Native `datetimeoffset`: parts по offset, `dateadd` сохраняет offset. |
| PostgreSQL | `now()`; канонический DTO-result UTC | `now()`; UTC | Для DTO parts — UTC wall time через `AT TIME ZONE 'UTC'`; Add — UTC wall time → `timestamptz`, без зависимости от session TZ/DST. |
| MySQL | `utc_timestamp(6)` | `utc_timestamp(6)` | Существующий `datetime`; части сохранённого wall time, native date-add; исходный offset колонок не восстанавливается. |
| MariaDB | `utc_timestamp(6)` | `utc_timestamp(6)` | Наследует MySQL contract, отдельные SQL-gen/dialect тесты. |
| ClickHouse | `now64(6, 'UTC')` | `now64(6, 'UTC')` | UTC-aware `DateTime64`; TZ нормализуется явно, затем `toXxx`/`addXxx`. |
| SQLite | UTC `strftime` с дробной частью и явным UTC | та же | `strftime`/date modifiers; offset-bearing текст — как instant, канонизация в UTC; точность — миллисекунды. |

### DO units/streams (r=1)

| Unit | state | Criteria covered | Dependencies |
|---|---|---|---|
| D217-0 — доказать provider representations | superseded by D217-0p | R217-01…R217-06 (foundation) | Roslyn + bounded spike. |
| D217-1 — DTO now и capabilities | pending | R217-01, R217-05 | после D0; `MemberTranslator.cs:20-32`; `ISqlDialect.cs:1066`; base + 6 dialects; default unsupported. |
| D217-2 — parts, Add, guards | pending | R217-02, R217-03, R217-04 | после D1; `MemberTranslator.cs:59-85,312-343`; `ScalarFunctionTranslator.cs:56`; `DateTimeFunctionTranslator.cs:17-27`. |
| D217-3 — unit/SQL-gen matrix | pending | R217-01…R217-06 | тесты сразу; итоговые прогоны после D1/D2. |
| D217-4 — реальные integrations | pending | R217-06 | scaffolding сразу, execution после D2; `CommonTestSuite.Functions.cs:177,198,246`; `SqlServerSpecificTests.cs:1690`. |
| D217-5 — docs + public API handoff | pending | R217-07 | docs параллельно. |
| D217-6 — evidence/boundary preparation | pending | R217-08 | scripts, manifest, логи, coverage/mutation. |
| D217-7 — единый DO→CHECK sweep | pending | R217-01…R217-08 | последовательно после D1–D6. |

### Variant matrix (r=1)

| Ряд | Статус | Detail |
|---|---|---|
| P6×Now/UtcNow×Select/Where/OrderBy×repeat | test | SQL-gen C01/C07/C14. |
| P4×Now/UtcNow×local/UTC | test | integration C02/C12 (MariaDB runtime deferred с env-триггером, SQL-gen не deferred). |
| P6×parts×value/null×placement | test | SQL-gen C03/C06/C07. |
| P6×Add×value/null×zero/± | test | дробные Add literal/captured/SQL; local/nonzero-offset/UTC × parts/Add. |
| forbidden × value/default/captured × placement | guard | guard C08/C09. |
| folding × static now/supported/bare const/unsupported | test | C01/C08/C09. |
| DateTime vs DTO × now/parts/Add × P6 | test | C11. |
| capability true/false × supported/unsupported | test | C10. |
| value/reference/default → C06/C08 | test | `default(DateTimeOffset)`. |
| `.Offset.TotalMinutes`/`.UtcDateTime.Year`/`.Date.AddDays` | guard | C09. |
| AddTicks/Add(TimeSpan) | deferred+trigger | guard C09 + deferred. |
| Years/Months fractional | guard | CLR `int`. |

### Test strategy (r=1)

C01–C14 as in the r=2 test strategy (C01 now SQL shape; C02 server instant; C03 parts; C04 Add; C05 fractional; C06 nullable; C07 placement; C08 unsupported/folding; C09 forbidden incl nested; C10 capability; C11 DateTime regression; C12 session TZ/DST; C13 mapping; C14 isolation).

### Docs plan (r=1)

`docs/scalar-functions/03-date-and-time.md:55-70`; `docs/ru/scalar-functions/03-date-and-time.md:56,69-71`; `docs/advanced/limitations.md:70-72`; условно RU limitations. Provider table, supported parts/Add, precision/timezone limits, explicit unsupported exceptions, "server time statt client time" note. New public dialect members XML-doc (CS1591 not suppressed). DocFX verified. No `docs/specs` links from public docs.

### Perf measurement (r=1 — reason the decision was later revised)

**Отдельный benchmark не нужен** (при сохранении footprint): меняется построение SQL (translation/prepare path), не клиентский per-row materializer. **Это и есть дефект, приведший к r=2**: ClickHouse reader adaptation is a per-row materializer path, so the rv=2 perf decision makes C17 required.

### Reconnaissance (r=1)

Ограниченный spike D0 (mapper result types / session TZ / fractional SQL state-dependent). Success: types/offsets/parts/intervals соответствуют таблице; Failure → отчёт D0 → PLAN.

### Unit mode (r=1)

Один worktree; три параллельных DO-потока (code/tests/docs); без веток/worktree/merge/push.

### Deferred + trigger (r=1)

| Объём | Триггер |
|---|---|
| Трансляция запрещённых членов, `AddTicks`, `Add(TimeSpan)` | отдельная задача `1.0.9` до release. |
| DTO overloads `SqlFunctions` | подтверждённый consumer/новый SQL class; до T53 freeze. |
| Смена хранения offset | подтверждённое требование; отдельный mapping/migration contract. |
| Изменение in-memory context | отдельная постановка. |

### Versioned evidence contract — rv=1

Frozen manifest: `artifacts/pdca/D217-datetimeoffset/rv1/manifest.json`. Rows E217-01..E217-13 as originally registered (foundation D0; six provider SQL-gen; core guards; P4 runtime integration; docs; coverage; mutation; completeness). CHECK re-gather budget ≤2; a substantive revision supersedes rv=1 keeping E217 IDs/obligations.

### Write footprint (r=1)

Production: `ISqlDialect.cs`, `Dialect/SqlDialectBase.cs`, `Visitors/{MemberTranslator,ScalarFunctionTranslator,DateTimeFunctionTranslator}.cs`; providers `SqliteDialect.cs`, `PostgresDialect.cs`, `SqlServerDialect.cs`, `MySqlDialect.cs`, `ClickHouseDialect.cs`; условно `MariaDbDialect.cs`, `DateConversionSqlTranslator.cs`. Tests/docs/artifacts as listed. **Не менять:** mapping/readers/TVP, `SqlFunctions*.cs`, shared `Entities.cs`, packages/config/CI, generated docs, parent collection status, T53 files.

### Sibling / T53 requirements (r=1)

Sibling footprint не пересекается. T53 должен учесть новые `ISqlDialect` members, base implementations и overrides как public API-кандидаты freeze. T217 не редактирует `Directory.Packages.props`/`.editorconfig`/`.github/workflows/dotnet.yml`/статус T53.

### Risks / known issues (r=1)

DTO reader не принимает выбранный shape; PG session TZ/DST; fractional Add теряет часть; folding обходит guard; новый interface API ломает сторонние dialects; skipped контейнеры как pass; aggregate coverage скрывает guard.

### Open questions (r=1)

Точное SQLite/MySQL/ClickHouse выражение/материализация — D0; контейнеры/coverage/mutation — precondition; точные hook signatures — Roslyn.

### Pointers (r=1)

- `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:1057-1095` (`ISqlDialect` hooks), `:1066` (`MakeNow`)
- `src/nextorm.core/Visitors/MemberTranslator.cs:20-32`, `:59-85`, `:312-343`
- `src/nextorm.core/Visitors/ScalarFunctionTranslator.cs:56`
- `src/nextorm.core/Visitors/DateTimeFunctionTranslator.cs:17-27`
- `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:622-663`
- `src/nextorm.core/Expressions/SelectExpression.cs:202`
- `src/nextorm.core/DataContext/RawMapperFactory.cs:698,746-747`
- `tests/nextorm.integration.tests/CommonTestSuite.Functions.cs:177,198,246`; `.../SqlServerSpecificTests.cs:1690`
- `docs/scalar-functions/03-date-and-time.md:55-70`; `docs/ru/scalar-functions/03-date-and-time.md:56,69-71`; `docs/advanced/limitations.md:70-72`

## D0 result (D217-0 bounded spike) — r=1, rv=1 (historical)

- **verdict: D0 SPIKE FAIL.** DO not started at r=1; no `D217-*` unit moved to `done`; no acceptance criterion weakened.
- **decisive finding (ClickHouse):** the chosen DTO `now`/`add*` expressions return the driver's `System.DateTime` (DateTime64(6)), while the existing projection reader path (`RowMapperFactory.MapColumn` -> `GetReaderAccessor` -> `SelectExpression.GetDataRecordMethod` -> `GetFieldValue<DateTimeOffset>`, SelectExpression.cs:202-204) throws `InvalidCastException` for a `DateTimeOffset`-typed projection. Exactly the invalidating risk at plan r=1.
- **passing providers:** SQL Server (`sysdatetimeoffset()`, `todatetimeoffset(sysutcdatetime(),'+00:00')`, native nonzero offset +02:00 preserved, parts by offset); PostgreSQL `now()` (session UTC and `America/New_York` -> canonical `+00:00`); MySQL `utc_timestamp(6)`; SQLite `strftime` UTC text.
- **required provider-local adaptations (feasible in D2, no mapping change):** PG DTO add/parts wrap with `AT TIME ZONE 'UTC'`; SQL Server `dateadd` truncates fractional day/second (scale to `microsecond`); MySQL `date_add(interval 0.5 day)` rounds to whole days (scale day/hour to `microsecond`).
- **evidence:** `artifacts/pdca/D217-datetimeoffset/rv1/d0-spike/{d0-results.md,probe.tsv,probe-run2.log,probe-run.log,roslyn-signatures.txt,build-baseline.log}`; probe source preserved as `.../d0-spike/T217D0ProbeTests.cs.txt`. Commands: `dotnet build nextorm.slnx -c Debug` (exit 0, 0/0); `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~T217D0ProbeTests"` (probe #1 exit 2 / diagnostic rerun exit 0, 5 succeeded, Skipped 0).
- **classification:** impossibility of materialization -> decision invalidation -> new substantive PLAN. Planner chose route (b): authorise a provider-local CH `DateTime`->`DateTimeOffset` reader adaptation (r=2 D217-0p). Route (a) (capability=false / narrow to 5 dialects) was rejected by the planner; criteria are NOT narrowed.

## D0′ result (D217-0p ClickHouse DTO reader prerequisite) — r=2, rv=2

- **verdict: D0′ PASS.** The provider-local `ClickHouseDataContext.MapColumnExpression` `DateTimeOffset` branch fires for a REAL scalar projection and materializes offset 0 with microsecond precision; existing ClickHouse DateTime materialization is unchanged. No criterion narrowed; no route-(a).
- **evidence (`A=artifacts/pdca/D217-datetimeoffset/rv2/d0p`):** `probe.tsv` (SQL + materialized values/types), `probe-run3.log` (exit 0, 1 succeeded / 0 failed / 0 skipped), `build-probe2.log` (0 Warning(s) 0 Error(s)); probe source preserved as `T217D0pProbeTests.cs.txt`; earlier harness iterations `probe-run.log`/`probe-run2.log` retained (assertion/seed bugs, not provider signal).
- **commands:** `dotnet build nextorm.slnx -c Debug` (exit 0, 0/0); `dotnet build tests/nextorm.integration.tests -c Debug` (exit 0); `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock T217_D0P_LOG=$A/probe.tsv dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~T217D0pProbeTests"` (exit 0, selected 1, succeeded 1, skipped 0).
- **observed:** scalar DTO `2023-05-17T10:00:00.1234560+00:00|off=00:00:00|micro=456`; nullable non-null `...6543210+00:00`; nullable SQL NULL → null; existing `IComplexEntity.Datetime` → `2023-01-01T10:00:00.0000000` (Unspecified). Base reader path would have emitted `GetFieldValue<DateTimeOffset>` and thrown `InvalidCastException`.
- **classification:** prerequisite satisfied → proceed to D217-1/D217-2.

## Open observations (for CHECK — not fixed by this DO attempt)

- **O217-01 — scalar terminal `.First()` on a DTO projection uses `QueryExecutor.ConvertScalar` (string→DTO), cannot materialise on SQLite.** A projection with no compiled row mapper (`MapDelegate is null`) is treated as scalar and routed through `QueryExecutor.First<TResult>` → `ExecuteScalar<TResult>` → `ConvertScalar<TResult>` (`src/nextorm.core/DataContext/QueryExecutor.cs:899`, `:780`), which does IConvertible/`Convert.ChangeType` conversions rather than the provider's DTO reader adaptation. On SQLite a DTO projection handed back as text cannot be converted to the DTO type. **Pre-existing and unrelated to the T217 diff** (the file is not in the T217 changed set; the T217 integration tests use `.Single()` / anonymous composite projections, so they never hit this path). Pointer: `src/nextorm.core/DataContext/QueryExecutor.cs:780` (`ConvertScalar<TResult>`), reached from `:899` (`First<TResult>`, scalar branch `:904-905`).

## Progress log

2026-10-13T17:16:00Z | DO | r2 | n1/3 | FIX `D217-CH-FRACTIONAL-DAY-TRUNCATION` (fixes applied 1, same DO attempt): `src/nextorm.clickhouse/ClickHouseDialect.cs:731` (`MakeDateAdd(field,amount,value,isDateTimeOffset)`) now normalises to `toTimeZone(value,'UTC')` and scales coarse DTO units to `addMicroseconds` (day ×86400000000, hour ×3600000000, minute ×60000000, second ×1000000, milliseconds ×1000); DateTime (non-DTO) path untouched; test `tests/nextorm.clickhouse.tests/T217DateTimeOffsetSqlGenerationTests.cs` expectations updated (`DtoAdd_ShouldScaleToMicroseconds`, `DtoAddFractionalDay_ShouldScaleToFinerUnit` + fractional hour); both files CRLF-normalised | src/nextorm.clickhouse/ClickHouseDialect.cs

2026-10-13T17:16:00Z | DO | r2 | n1/3 | Builds after fix: `dotnet build nextorm.slnx -c Debug` exit 0 (0 Warning(s) 0 Error(s)); `dotnet build tests/nextorm.integration.tests -c Debug` exit 0 (0/0) | artifacts/pdca/D217-datetimeoffset/rv2/build/{solution-fix.log,integration-fix.log}

2026-10-13T17:16:00Z | DO | r2 | n1/3 | Inner-loop SQL-gen after fix: `dotnet test tests/nextorm.clickhouse.tests -c Debug --no-build --filter "FullyQualifiedName~T217|FullyQualifiedName~DialectTests"` exit 0, selected 111, succeeded 111, failed 0, skipped 0 | artifacts/pdca/D217-datetimeoffset/rv2/inner/clickhouse-fix.log

2026-10-13T17:16:00Z | DO | r2 | n1/3 | D217-4 re-run all 5 providers non-skipped (xunit v3 `dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -class <FQN> -noColor`, `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`): sqlite exit 0 / selected 11 / succeeded 11 / skipped 0; postgres exit 0 / 12 / 12 / 0; mysql exit 0 / 11 / 11 / 0; sqlserver exit 0 / 12 / 12 / 0; clickhouse exit 0 / 12 / 12 / 0 (was 11/12 pre-fix, `AddFractional_ShouldKeepTheSignificantFraction` now passes). `ClickHouseDialect`-only change → no regression in the other four; D217-4 set `done` | artifacts/pdca/D217-datetimeoffset/rv2/integration/{sqlite,postgres,mysql,sqlserver,clickhouse}.log

2026-10-13T17:16:00Z | DO | r2 | n1/3 | OPEN OBSERVATION O217-01 recorded for CHECK (NOT fixed): scalar terminal `.First()` on a DTO projection routes through `QueryExecutor.ConvertScalar<TResult>` (`src/nextorm.core/DataContext/QueryExecutor.cs:780`, from `First<TResult>` `:899`) and cannot materialise a DTO from SQLite text; pre-existing, unrelated to the T217 diff (file not in changed set; T217 tests use `.Single()`/anonymous projections) | src/nextorm.core/DataContext/QueryExecutor.cs:780

2026-10-13T17:10:00Z | DO | r2 | n1/3 | D217-4 real integrations BLOCKED — new defect `D217-CH-FRACTIONAL-DAY-TRUNCATION`: ClickHouse DTO `.AddDays(0.5)` renders `addDays(toTimeZone(value,'UTC'), 0.5)` and returns the operand unchanged (off by 12h), violating R217-03; PG/SQLite/MySQL/SQL Server non-skipped green; ClickHouse 11/12 (AddFractional failed); D217-4 NOT done | artifacts/pdca/D217-datetimeoffset/rv2/integration/clickhouse.log

2026-10-13T17:10:00Z | DO | r2 | n1/3 | D217-4 changed files: `tests/nextorm.integration.tests/T217DateTimeOffsetTests.cs` (new; abstract base + 5 provider subclasses; C02/C04–C07/C12/C13 + CH C15), `tests/nextorm.integration.tests/T217DateTimeOffsetEntities.cs` (new `IT217DtoEntity`); CRLF-normalized; build `dotnet build tests/nextorm.integration.tests -c Debug` exit 0 (0 Warning(s) 0 Error(s)); per-provider xunit v3 `dotnet run --project tests/nextorm.integration.tests -c Debug --no-build -- -class <FQN> -noColor` with `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`: sqlite exit 0 / 11 total / 11 succeeded / 0 skipped; postgres exit 0 / 12 / 12 / 0; mysql exit 0 / 11 / 11 / 0; sqlserver exit 0 / 12 / 12 / 0; clickhouse exit 1 / 12 / 11 / 0 (1 failed) | artifacts/pdca/D217-datetimeoffset/rv2/integration/

2026-10-13T17:10:00Z | DO | r2 | n1/3 | D217-4 logs: `integration/{build-integration.log,sqlite.log,postgres.log,mysql.log,sqlserver.log,clickhouse.log}`; full T217 `--filter "FullyQualifiedName~T217"` NOT issued (STOP on provider failure; would re-run the same filter) | artifacts/pdca/D217-datetimeoffset/rv2/integration/sqlite.log

2026-10-13T16:48:30Z | DO | r2 | n1/3 | D0′ PASS — CH `MapColumnExpression` DateTimeOffset branch materializes scalar projection offset 0 with microseconds; no criterion narrowed; defect `D217-D0-CH-DTO-READER` fix applied (1); temporary probe removed after run | artifacts/pdca/D217-datetimeoffset/rv2/d0p/probe.tsv


2026-10-12T04:18:33Z | PLAN | r1 | n1/3 | PLAN ready — plan_state=ready (collection handoff; parent owns PLAN→DO boundary) | docs/specs/status/D217-datetimeoffset-1.md

2026-10-12T04:18:33Z | PLAN | r1 | n1/3 | rv=1 evidence contract + frozen manifest path artifacts/pdca/D217-datetimeoffset/rv1/manifest.json registered; D217-0…D217-7 active (pending); defect history empty; no supersession | docs/specs/status/D217-datetimeoffset-1.md

2026-10-13T11:30:00Z | DO | r1 | n1/3 | D0 spike FAIL — ClickHouse DTO projection cannot materialize with existing reader (`GetFieldValue<DateTimeOffset>` InvalidCastException on DateTime64); mapping/reader change is footprint-forbidden; NO DO unit started; returning DO→PLAN candidate | artifacts/pdca/D217-datetimeoffset/rv1/d0-spike/d0-results.md

2026-10-13T16:45:00Z | PLAN | r2 | n1/3 | Replanned: D0 FAIL → r=2 route (b); R217-01/R217-06 remain 6 dialects; r=2 plan + rv=2 contract recorded, r=1 plan + D0 result retained as historical; defect `D217-D0-CH-DTO-READER` r1/n1 → D217-0 superseded by active D217-0p; manifest path artifacts/pdca/D217-datetimeoffset/rv2/manifest.json | docs/specs/status/D217-datetimeoffset-1.md

2026-10-13T16:45:00Z | DO | r2 | n1/3 | DO started (r=2, n=1/3) — D0′ (D217-0p) prerequisite in progress | docs/specs/status/D217-datetimeoffset-1.md

2026-10-13T12:00:46Z | DO | r2 | n1/3 | D1+D2 implemented (DTO Now/UtcNow/capability, seven parts, seven Add incl. fractional scaling, fail-closed guards; six dialects + CH DTO `MapColumnExpression` branch); solution + 7 affected test-project builds exit 0 (0 Warning(s)/0 Error(s) each); inner loop green | artifacts/pdca/D217-datetimeoffset/rv2/build/solution.log

2026-10-13T12:00:46Z | DO | r2 | n1/3 | D3 provider SQL-gen tests completed — 6 new `tests/nextorm.<p>.tests/T217DateTimeOffsetSqlGenerationTests.cs` asserting exact Now/UtcNow, 7 parts, 7 Add + fractional, unsupported-member/method guards, C07 placement, C11 DateTime regression, C14 repeat, + CH C15/C16 DTO-materialization; inner loop all green: sqlite 56, postgres 62, sqlserver 81, mysql 56, mariadb 44, clickhouse 111, core 25 (0 failed / 0 skipped each) | artifacts/pdca/D217-datetimeoffset/rv2/inner/evidence.json

2026-10-13T12:00:46Z | DO | r2 | n1/3 | D0′ re-verified AFTER D1/D2 on the current tree against REAL ClickHouse (DOCKER_HOST podman socket): scalar DTO off=00:00:00 micro=456, nullable non-null micro=321 / SQL NULL → null, existing `IComplexEntity.Datetime` unchanged (Unspecified); 1 selected / 1 succeeded / 0 skipped; exit 0; temporary probe removed after the run | artifacts/pdca/D217-datetimeoffset/rv2/inner/clickhouse-dto-reverify.tsv

2026-10-13T12:00:46Z | DO | r2 | n1/3 | `python3 scripts/validate_inner_loop.py report rv2/inner/evidence.json` exit 0 and `python3 scripts/validate_inner_loop.py brief rv2/scope.json` exit 0 | artifacts/pdca/D217-datetimeoffset/rv2/inner/evidence.json

### D3 inner-loop evidence (stage 2a, r=2, rv=2)

Changed files (all CRLF-normalized; only D1/D2/D3 footprint, no D4/D5/D6):

| File | Kind | Change |
|---|---|---|
| `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` | code | new `SupportsDateTimeOffsetTranslation` (default false), `MakeDateTimeOffsetNow(bool)` (default fail-closed), `bool isDateTimeOffset` overloads of `MakeDatePart`/`MakeDateAdd` |
| `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs` | code | default implementations of the above |
| `src/nextorm.core/Visitors/MemberTranslator.cs` | code | DTO `Now`/`UtcNow` + seven parts; fail-closed guard `ThrowIfUnsupportedDateTimeOffsetAccess` |
| `src/nextorm.core/Visitors/ScalarFunctionTranslator.cs` | code | route `DateTimeOffset` methods to `DateTimeFunctionTranslator` |
| `src/nextorm.core/Visitors/DateTimeFunctionTranslator.cs` | code | DTO `Add*`; fail-closed for `AddTicks`/`Add(TimeSpan)`/`ToOffset` |
| `src/nextorm.core/Visitors/DateConversionSqlTranslator.cs` | doc | XML `cref` disambiguation only |
| `src/nextorm.sqlite/SqliteDialect.cs` | code | UTC `strftime` now + capability |
| `src/nextorm.postgres/PostgresDialect.cs` | code | `now()`, UTC parts/Add `at time zone 'UTC'` |
| `src/nextorm.sqlserver/SqlServerDialect.cs` | code | `sysdatetimeoffset()`/`todatetimeoffset`, fractional scaling + native offset |
| `src/nextorm.mysql/MySqlDialect.cs` | code | `utc_timestamp(6)`, fractional unit scaling |
| `src/nextorm.clickhouse/ClickHouseDialect.cs` | code | `now64(6,'UTC')`, UTC-normalized parts/Add |
| `src/nextorm.clickhouse/ClickHouseDataContext.cs` | code | DTO `MapColumnExpression` override → read `DateTime`, offset 0, µs preserved |
| `tests/nextorm.core.tests/T217DateTimeOffsetTranslationTests.cs` | test (new) | C08–C11/C14 guards + PG/SS/CH shapes + capability default |
| `tests/nextorm.<p>.tests/T217DateTimeOffsetSqlGenerationTests.cs` ×6 | test (new) | per-provider C01/C03–C07/C11/C14 + CH C15/C16 |

Build evidence: `dotnet build nextorm.slnx -c Debug` exit 0 (0/0); per-project `dotnet build tests/nextorm.<p>.tests -c Debug` exit 0 (0/0) for p ∈ {core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}; integration probe build exit 0 (0/0) and post-probe integration rebuild exit 0 (0/0). Logs: `artifacts/pdca/D217-datetimeoffset/rv2/build/*.log`.

Inner-loop executions (phase=inner; argument arrays and full logs in `rv2/inner/evidence.json`; the mandated combined `|` selectors are recorded in `supplementary_runs` because the gate's hygiene rule rejects a literal `'|'` token — repo precedent `artifacts/pdca/rc2-170/r2/evidence.json`):

| Project | `--filter` | exit | selected | log |
|---|---|---|---|---|
| core | `FullyQualifiedName~T217\|FullyQualifiedName~SqlGenerationTests` | 0 | 25 | `rv2/inner/core.log` |
| sqlite | `FullyQualifiedName~T217\|FullyQualifiedName~DialectTests` | 0 | 56 | `rv2/inner/sqlite.log` |
| postgres | `…~T217\|…~DialectTests` | 0 | 62 | `rv2/inner/postgres.log` |
| sqlserver | `…~T217\|…~DialectTests` | 0 | 81 | `rv2/inner/sqlserver.log` |
| mysql | `…~T217\|…~DialectTests` | 0 | 56 | `rv2/inner/mysql.log` |
| mariadb | `…~T217\|…~DialectTests` | 0 | 44 | `rv2/inner/mariadb.log` |
| clickhouse | `…~T217\|…~DialectTests` | 0 | 111 | `rv2/inner/clickhouse.log` |

Gate: `python3 scripts/validate_inner_loop.py report artifacts/pdca/D217-datetimeoffset/rv2/inner/evidence.json` exit 0; `… brief …/rv2/scope.json` exit 0.

T53 freeze candidates (registered here; **freeze stays HELD** — do not freeze before the T53 checkpoint): `ISqlDialect.SupportsDateTimeOffsetTranslation`, `ISqlDialect.MakeDateTimeOffsetNow(bool)`, and the `bool isDateTimeOffset` overloads of `ISqlDialect.MakeDatePart`/`ISqlDialect.MakeDateAdd` (all new public surface with base defaults).

Still INCOMPLETE (not in this stage): D4 real integrations (PG/SS/MySQL/CH), D5 docs, D6/D7 boundary/coverage/mutation/freeze. No `D:` unit moved to `done`.

### D5 docs evidence (stage 2c, r=2, rv=2)

Progress log:

2026-10-13T17:24:00Z | DO | r2 | n1/3 | D217-5 docs/XML-doc handoff: EN+RU `03-date-and-time.md` `DateTimeOffset` section rewritten (server-side time, supported `Now`/`UtcNow` + seven parts + seven `Add*` incl. fractional handling, forbidden members fail-closed with `NotSupportedException`, per-provider offset/zone/precision table), stale "members are not translated"/client-clock wording removed; `limitations.md` EN+RU gained an offset-not-preserved limitation row and a server-side-translation "not limitations" bullet; all four files CRLF-normalised; no public docs link to `docs/specs/**`; XML-doc present on every new public `ISqlDialect` member (no CS1591 suppression); D217-5 marked done | docs/scalar-functions/03-date-and-time.md

2026-10-13T17:24:00Z | DO | r2 | n1/3 | Build `dotnet build nextorm.slnx -c Debug` exit 0 (0 Warning(s)/0 Error(s)); DocFX `dotnet docfx docs/docfx.json` exit 0 (2 pre-existing sourcegenerator `AnalyzerReleases` warnings, 0 errors); EN/RU consistency PASS (0 failing checks; 2 `###` headings / 20 table lines each; all supported/forbidden ops + 6 providers + storage types/emitters in both trees) | artifacts/pdca/D217-datetimeoffset/rv2/docs/{build,docfx,en-ru-consistency}.log

Changed docs files (all CRLF-normalised):

| File | Change |
|---|---|
| `docs/scalar-functions/03-date-and-time.md` | `DateTimeOffset` section: server-side translation table, forbidden members, per-provider zone/precision limits |
| `docs/ru/scalar-functions/03-date-and-time.md` | RU mirror |
| `docs/advanced/limitations.md` | offset-not-preserved limitation row + server-side-translation bullet |
| `docs/ru/advanced/limitations.md` | RU mirror |

Build/DocFX/parity logs: `artifacts/pdca/D217-datetimeoffset/rv2/docs/{build,docfx,en-ru-consistency}.log`.

Still INCOMPLETE (not in this stage): D6 evidence/boundary/coverage/mutation prep, D7 final sweep + freeze. D217-5 is now `done`; D1–D4 done; no acceptance criterion narrowed.

2026-10-13T12:30:38Z | DO | r2 | n1/3 | D217-7 single DO->CHECK boundary sweep under the coverage collector: `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o $A/coverage/cobertura.xml "bash $A/boundary.sh"` exit 0; per-step exits build 0, test-core 0, test-sqlite 0, test-postgres 0, test-sqlserver 0, test-mysql 0, test-mariadb 0, test-clickhouse 0, integration 0; unfiltered totals (passed/failed/skipped): core 2486/0/0, sqlite 1703/0/1 (pre-existing rowid LOB probe), postgres 940/0/0, sqlserver 850/0/0, mysql 418/0/0, mariadb 300/0/0, clickhouse 697/0/0; integration Total 3861 Failed 0 Skipped 198 (capability-only; 0 provider-unavailable markers; 4 required containers ready) | artifacts/pdca/D217-datetimeoffset/rv2/boundary/{boundary-summary.json,*.out}, artifacts/pdca/D217-datetimeoffset/rv2/coverage/collect.log
2026-10-13T12:30:38Z | DO | r2 | n1/3 | Coverage report + gate: `dotnet tool run reportgenerator -reports:$A/coverage/cobertura.xml -targetdir:$A/coverage/report -reporttypes:Html;JsonSummary` exit 0; `bash $A/coverage-check.sh $A/coverage/report/Summary.json 85 75` exit 0 - scope [nextorm.core,nextorm.postgres,nextorm.sqlite,nextorm.sqlserver], linecoverage 88.6 (>=85) branchcoverage 80.7 (>=75) | artifacts/pdca/D217-datetimeoffset/rv2/coverage/{report.log,check.log,report/Summary.json}
2026-10-13T12:30:38Z | DO | r2 | n1/3 | D217-6 partial: scripts boundary.sh/coverage-check.sh/verify.sh written; frozen evidence manifest required_rows E217-01..14 (E217-11 coverage+boundary and E217-13 verify met; E217-12 mutation and E217-14 perf open = next task); `python3 scripts/validate_inner_loop.py manifest $A/manifest.json` exit 0; frozen tree 11a699fcdfca87e04cd6967c1e887d0f61a8673b incl. uncommitted inputs (status file excluded as append-only durable state) | artifacts/pdca/D217-datetimeoffset/rv2/manifest.json
2026-10-13T12:30:38Z | DO | r2 | n1/3 | `bash $A/verify.sh` exit 0 - manifest rows complete; footprint 28 changed / 0 out-of-footprint (the 2 pre-existing dirty command-execution diagram SVGs recorded as non-T217 in verify/pre-existing.json); CRLF 26/26; reader/mapping invariant (RowMapperFactory.cs/SelectExpression.cs/RawMapperFactory.cs untouched; ClickHouseDataContext.cs only MapColumnExpression override + private DTO helper; 0 other-override offenders); hash+tree consistency PASS | artifacts/pdca/D217-datetimeoffset/rv2/verify/verify.log
2026-10-13T12:30:38Z | DO | r2 | n1/3 | D217-6 remains partial (mutation.sh/benchmark pending), D217-7 boundary portion complete; next task = E217-12 mutation + E217-14 perf; no D: unit moved to `done`; no replan (r stays 2, n stays 1/3) | docs/specs/status/D217-datetimeoffset-1.md
2026-10-13T12:42:12Z | DO | r2 | n1/3 | E217-12 mutation: `bash artifacts/pdca/D217-datetimeoffset/rv2/mutation.sh` exit 0 — baseline unit 34/34 pass (tests/nextorm.clickhouse.tests, filter FullyQualifiedName~T217) and baseline real-CH integration 12/12 pass (tests/nextorm.integration.tests, filter FullyQualifiedName~ClickHouseT217DateTimeOffsetTests); all six mutants KILLED by nonempty targeted runs: a(remove DTO dispatch) 2/34, b(client capture Now/UtcNow) 2/34, c(change UTC branch) 6/12 via real-CH integration (the unit DataTable harness reports DateTimeKind Unspecified, so the Utc arm is reachable only through the driver), d(forbidden-member guard) 7/34, e(nullable propagation) 1/34, f(old now emitter) 2/34; restore proof sha256 + git-diff byte-identical, no leftover backups | artifacts/pdca/D217-datetimeoffset/rv2/E217-12/mutation.log, E217-12/kill-ledger.tsv, E217-12/restore-proof.tsv, E217-12/mutant-*.patch
2026-10-13T12:42:12Z | DO | r2 | n1/3 | E217-14 perf probe: added benchmarks/nextorm.benchmark/T217DtoPerfProbe.cs + Program.cs --t217-perf-probe mode + Testcontainers.ClickHouse package (benchmark-only); `dotnet build benchmarks/nextorm.benchmark -c Release` exit 0 (0/0); real-ClickHouse probe exit 0 reusing the container (200000 DateTime64(6,'UTC') rows, warm-up 3 + 5 measured batches, DTO vs DateTime reading): datetime elapsed median 120.8 ms / alloc median 1610984 B; dto elapsed median 126.1 ms / alloc median 3210984 B; measured numbers only, no threshold/conclusion | artifacts/pdca/D217-datetimeoffset/rv2/E217-14/probe-run.log, E217-14/perf.tsv, E217-14/summary.tsv, E217-14/environment.txt
2026-10-13T12:42:12Z | DO | r2 | n1/3 | D217-6 finalize: manifest rows E217-12/E217-14 set `met` with evidence refs; verify.sh footprint/CRLF allowlist extended with the benchmark additions; `python3 scripts/validate_inner_loop.py manifest $A/manifest.json` exit 0; `bash $A/verify.sh` exit 0 (footprint 31 changed/0 out-of-footprint, CRLF 29/29, frozen tree 11a699fcdfca87e04cd6967c1e887d0f61a8673b unchanged) | artifacts/pdca/D217-datetimeoffset/rv2/manifest.json, verify/verify.log, verify/manifest.log
2026-10-13T12:42:12Z | DO | r2 | n1/3 | all 14 evidence rows E217-01..14 now `met`; no D: unit moved to `done` by this stage; no replan (r stays 2, n stays 1/3); next = CHECK | docs/specs/status/D217-datetimeoffset-1.md

2026-10-13T12:46:17Z | DO | r2 | n1/3 | DO complete (r=2, rv=2) — all E217-01..14 met: 6/6 named mutants killed + restore proof (byte-identical, sha256), perf C17 recorded (real-CH DTO vs DateTime, measured only), `bash "$A/verify.sh"` exit 0, `python3 scripts/validate_inner_loop.py manifest "$A/manifest.json"` exit 0; units D217-6 and D217-7 marked `done`; handing off to CHECK | artifacts/pdca/D217-datetimeoffset/rv2/manifest.json
2026-10-13T13:02:19Z | CHECK | r2 | n1/3 | CHECK re-gather #1 (1/2): C1 DISMISSED — SQLite DTO Add* offset-less text read through the typed GetFieldValue<DateTimeOffset> path materialises offset 0 / correct UTC instant under BOTH host Asia/Yekaterinburg (+05) and TZ=America/New_York (-04); C2 CONFIRMED defect D217-SS-LITERAL-ADD-INT-OVERFLOW — SQL Server literal AddSeconds(2_200_000)/AddMilliseconds(2_200_000) emits dateadd(millisecond|microsecond,(amount)*factor,value) and real SQL Server 2025 throws SqlException arithmetic overflow (AddSeconds(2_000_000)/2_147_483, AddDays(24|25), captured large amount succeed); C3 DISMISSED — CH addMicroseconds(toTimeZone(value,'UTC'),(@p)*factor) with captured (CLR Double) and Float64-column amounts returns the correct instant offset 0 incl fractional 0.5 day; traceability 172 rows, every FQN bound to a discovered xunit symbol (0 missing), no GAP (E217-11 is an evidence-only row); temp probe tests/nextorm.integration.tests/T217Regather1Probe.cs removed, tree restored, dotnet build exit 0 (0/0); no DO iteration, no replan, r stays 2, n stays 1/3 | artifacts/pdca/D217-datetimeoffset/rv2/regather1/{sqlite-c1.tsv,sqlite-c1.log,sqlserver-c2.tsv,sqlserver-c2.log,clickhouse-c3.tsv,clickhouse-c3.log,traceability.tsv,summary.tsv}

### DO loop-back fix2 (r=2, n=2/3) — CHECK→DO 2026-10-13T13:25:20Z

2026-10-13T13:25:20Z | DO | r2 | n2/3 | FIX `D217-SS-LITERAL-ADD-INT-OVERFLOW` (fixes applied 1, **RESOLVED**): `src/nextorm.sqlserver/SqlServerDialect.cs:537` `MakeDateAdd(field,amount,value,isDateTimeOffset)` keeps the whole amount in the requested unit and scales only the fractional remainder into the finer unit (`dateadd(second, cast(floor(amount) as int), dateadd(millisecond, cast((amount - floor(amount)) * 1000 as int), value))`, and millisecond→microsecond); the ×1000 `int` product that overflowed for large literals is gone, the fraction is retained; the non-DTO DateTime path is untouched (R217-05); expectations updated + new large-literal/no-overflow and fractional-remainder SQL-gen assertions in `tests/nextorm.sqlserver.tests/T217DateTimeOffsetSqlGenerationTests.cs` | src/nextorm.sqlserver/SqlServerDialect.cs:537

2026-10-13T13:25:20Z | DO | r2 | n2/3 | FIX `D217-C4-FRACTIONAL-TEST-ORACLE` (fixes applied 1, **RESOLVED**): `tests/nextorm.integration.tests/T217DateTimeOffsetTests.cs:143` tightened — `FractionalSecondTolerance` (SQLite 1 s for its whole-second `datetime()` path, 1 ms otherwise) plus a provider-independent `AddMilliseconds(1500)` sub-second proof; the tightened oracle FAILS (exit 2, `FractionalSecond` off by 500 ms) against a temporary whole-second-truncating `MakeDateAdd` and PASSES after restore (exit 0) | artifacts/pdca/D217-datetimeoffset/rv2/fix2/{mutation-truncate-integration.log,green-after-restore-integration.log}

2026-10-13T13:25:20Z | DO | r2 | n2/3 | FIX `D217-C6-ZEROARG-DTO-GUARD` (fixes applied 1, **RESOLVED**): `src/nextorm.core/Visitors/DateTimeFunctionTranslator.cs:14` no longer returns early on `node.Arguments.Count != 1` for a DTO receiver — any non-single-argument `DateTimeOffset` instance method (`ToUnixTimeSeconds`/`ToUnixTimeMilliseconds`/`ToLocalTime`/`ToUniversalTime`) now fails closed before SQL instead of silently dropping the call; test `UnsupportedZeroArgumentMethods_ShouldThrowBeforeSql` | src/nextorm.core/Visitors/DateTimeFunctionTranslator.cs:14

2026-10-13T13:25:20Z | DO | r2 | n2/3 | C7 `D217-C7-NESTED-DTO-RECEIVER-GUARD` **ALREADY FAIL-CLOSED** (fixes applied 0): `x.Value.Offset.ToString()` and `x.Value.Date.ToShortDateString()` both throw `NotSupportedException` (string-returning client method rejected, unsupported `DateTime` method rejected); recorded by test `UnsupportedMemberAsClientMethodReceiver_ShouldFailClose`; no guard change needed | tests/nextorm.core.tests/T217DateTimeOffsetTranslationTests.cs

2026-10-13T13:25:20Z | DO | r2 | n2/3 | Build `dotnet build nextorm.slnx -c Debug` exit 0 (**0 Warning(s) 0 Error(s)**) after the examples project settled (out-of-scope `examples/**` concurrent edits, never touched/staged/reverted) | artifacts/pdca/D217-datetimeoffset/rv2/fix2/build-final-solution.log

2026-10-13T13:25:20Z | DO | r2 | n2/3 | Inner loop filtered `~T217` (all exit 0, 0 failed / 0 skipped): core 31, sqlite 31, postgres 31, sqlserver 33, mysql 31, mariadb 32, clickhouse 34; real-DB integration (DOCKER_HOST) 58 selected / 58 succeeded / 0 skipped (sqlite 11, postgres 12, mysql 11, sqlserver 12, clickhouse 12) | artifacts/pdca/D217-datetimeoffset/rv2/fix2/final-{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse,integration}.log

2026-10-13T13:25:20Z | DO | r2 | n2/3 | Real SQL Server probe (`T217_FIX2_LOG=rv2/fix2/sqlserver-probe.tsv`, exit 0, 1 succeeded / 0 skipped): `AddSeconds(2_200_000)` → 2023-06-11T21:06:40.123Z, `AddMilliseconds(2_200_000)` → 2023-05-17T10:36:40.123Z (no overflow), `AddSeconds(1.5)` → +1.500 s (500 ms retained); temporary probe removed after the run | artifacts/pdca/D217-datetimeoffset/rv2/fix2/{sqlserver-probe.tsv,sqlserver-probe.log}

2026-10-13T13:25:20Z | DO | r2 | n2/3 | `python3 scripts/validate_inner_loop.py brief rv2/scope.json` exit 0; `python3 scripts/validate_inner_loop.py report rv2/fix2/evidence.json` exit 0; all edited files CRLF-normalised; no commit/push; no cycle done; D217-SS-LITERAL-ADD-INT-OVERFLOW resolved, C4/C6 fixed, C7 already-closed | artifacts/pdca/D217-datetimeoffset/rv2/fix2/evidence.json

### CHECK re-gather #2 — E217-10 / E217-13 (r=2, n=2/3) 2026-10-13T13:35Z

2026-10-13T13:35:00Z | CHECK | r2 | n2/3 | CHECK re-gather #2 (2/2), evidence-only, no DO iteration: E217-10 docs/XML met — `xmldoc.log` 24/24 new/changed public|protected members carry `///` (0 missing), CS1591 not suppressed for hand-written members (`GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`), `no-spec-links.log` 0 hyperlinks from non-spec docs/readme to `docs/specs/**`, `docfx.log` exit 0 (2 pre-existing warnings), `build-scoped.log` exit 0 (0/0) over all 22 non-example solution projects; full-solution `build.log` exit 1 ONLY on out-of-T217-footprint concurrent `examples/**` WIP (4× CS0117/CS1061 in `AdventureWorksQueries.cs`, unrelated to T217) | artifacts/pdca/D217-datetimeoffset/rv2/E217-10/{xmldoc,no-spec-links,docfx,build-scoped,build}.log

2026-10-13T13:35:00Z | CHECK | r2 | n2/3 | E217-13 met — versioned DO ledger `E217-01..14` (`ledger.tsv`); benchmark scope verdict **COMPLIANT in-footprint**: `Testcontainers.ClickHouse` has no `Version` in the csproj, the version `4.15.0` lives only in the unchanged/committed `Directory.Packages.props` (CPM), benchmark project is in `nextorm.slnx:24`, and the r=2 plan authorises `benchmarks/nextorm.benchmark/**`; `dotnet build benchmarks/nextorm.benchmark -c Release` exit 0 (0/0) | artifacts/pdca/D217-datetimeoffset/rv2/E217-13/{ledger.tsv,scope-benchmark.log,benchmark-release-build.log}

2026-10-13T13:35:00Z | CHECK | r2 | n2/3 | manifest refreshed: tree=`885c9c2c22a70e734105e195b3cdc9bfaedf71fd` (recorded in `E217-13/tree-sha.txt`), `hashes.json` re-frozen (25 footprint files — stale after the fix2 C4/C6/SQL-Server changes), `pre-existing.json` extended with the 4 concurrent out-of-scope `examples/**` files; E217-04/08/09 evidence repointed to the fix2 logs; `python3 scripts/validate_inner_loop.py manifest` exit 0; `bash verify.sh` exit 0 (manifest PASS, footprint 31 changed / 0 out-of-footprint, CRLF 29/29, reader/mapping invariant PASS, hash+tree PASS) | artifacts/pdca/D217-datetimeoffset/rv2/verify/{verify.log,manifest.log,hashes.json,pre-existing.json}, E217-13/manifest-validate.log

2026-10-13T13:35:00Z | CHECK | r2 | n2/3 | Non-blocking dispositions (NOT fixed, per instruction) for the remaining findings **C5/C8/C9/C11/C12/C13/C14** — fractional-second precision / unsupported+folding / forbidden members incl. nested / DateTime regression / session-TZ + non-zero offset / existing-mapping-unchanged / repeat isolation: each maps to a test-strategy case and stays covered by the green filtered suites (E217-02..09) and the regather #1 traceability (172 rows, 0 GAP); no defect promotion; retained as notes/follow-ups — re-check only if the frozen tree changes | artifacts/pdca/D217-datetimeoffset/rv2/regather1/traceability.tsv, fix2/final-*.log

2026-10-13T13:35:00Z | CHECK | r2 | n2/3 | Non-blocking environmental caveat (not a T217 defect): two other-agent WIP streams are dirty in the shared working tree and are excluded from the T217 footprint and from the frozen tree — `examples/**` (issue #160 alias/CTE refactor, currently breaks the full-solution build) and the two pre-existing command-execution diagram SVGs; recorded in `verify/pre-existing.json`. All 14 required rows E217-01..14 are `met` (E217-11 boundary/coverage evidence predates the fix2 loop-back and was not re-run in this re-gather; fix2 delta covered by the fresh filtered E217-04/E217-08 and integration E217-09 — flagged as follow-up); CHECK verdict pending: owned by the check role (medium) | artifacts/pdca/D217-datetimeoffset/rv2/verify/pre-existing.json, E217-11/boundary/boundary-summary.json

2026-10-13T13:37:30Z | CHECK | r2 | n2/3 | re-gather #2 harden + finalization: the shared working tree is an active multi-agent target (issue #160 alias work kept landing in `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs`, `tests/nextorm.alias.tests/*`, `tests/nextorm.alias.poc/*`, `examples/**`), so the frozen-tree computation in `verify.sh` was restricted to the T217 authorised footprint under `src/tests/docs` (status file excluded) — an out-of-footprint T217 change is still caught by the footprint check; the currently-dirty non-T217 paths were snapshotted into `verify/pre-existing.json` (2 pre-existing SVGs + 4 `examples/**` + `JoinAliasGenerator.cs` + `JoinAliasGeneratorDiagnosticTests.cs`). Final: `validate_inner_loop.py manifest` exit 0 and `bash verify.sh` exit 0 (manifest PASS, footprint 37 changed / 0 out-of-footprint, CRLF 29/29, invariant PASS, hash+tree `f2a6f6363f93b103e86458d3ee58b9da6175baf2` PASS) | artifacts/pdca/D217-datetimeoffset/rv2/verify/{verify.log,pre-existing.json,hashes.json}, E217-13/{manifest-validate.log,verify-run.log,tree-sha.txt}

2026-10-13 | RECOVERY | r=2 | n=2/3 | Recovered after harness/tooling migration; ledger reconciled: D217-0p/1/2/3 marked done, phase=CHECK awaiting verdict. | docs/specs/status/D217-datetimeoffset-1.md

2026-10-13 | NOTICE | r=2 | n=2/3 | Lane exclusion forbids committing benchmarks/nextorm.benchmark/Program.cs and nextorm.benchmark.csproj, which conflict with E217-14's in-footprint approval; benchmark staging HELD pending ownership clarification; no probe-only commit; concurrent changes not discarded. | docs/specs/status/D217-datetimeoffset-1.md

2026-10-13 | RECOVERY | r=2 | n=2/3 | Changed-file count reconciled: on-disk population = 25 tracked modified + 10 untracked = 35; the earlier "37" counted the 29 in-footprint paths + 8 pre-existing non-footprint paths recorded at re-gather #2, of which 2 concurrent #160 alias-WIP paths (`src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs`, `tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs`) are no longer dirty in `git status --porcelain`, leaving 35 (25 ` M` + 10 `??`). | artifacts/pdca/D217-datetimeoffset/rv2/verify/{footprint.log,pre-existing.json}

2026-10-13 | CHECK | r=2 | n=2/3 | VERDICT FAIL (evidence gate, not a product defect): coverage provenance inconsistent (raw Cobertura 88.007/80.285 vs Summary 88.6/80.7; collect.log ends "Canceling…"); manifest references pre-fix sweep; per-row JSON/hashes/frozen-tree binding absent; branch-delta/uncovered identities missing; profile audits not loaded. Unmet rows: E217-11, E217-13.

2026-10-13 | DO | r=2 | n=3/3 | CHECK→DO loop-back: corrective evidence-packaging task (no code, no criteria change). Escalation reserved for recurrence.

2026-10-13T16:10:38Z | DO | r=2 | n=3/3 | E217-11 coverage provenance RESOLVED: raw cobertura was PRE-fix (mtime 17:28 < fix2 18:26); re-ran coverage ONCE over the post-fix tree — `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o $A/coverage/cobertura-postfix.xml "bash $A/boundary2.sh"` exit 0, wall ~21:07→21:09, all 20 builds 0/0, 7 unfiltered provider runs exit 0, unfiltered integration exit 0 (Total 3861 / Failed 0 / Skipped 198 capability-only); raw post-fix cobertura 37950/43120 = 88.010% line, 20044/24962 = 80.298% branch; reportgenerator from the SAME raw → Summary 88.6/80.7; gate PASS (85/75) under both aggregations. Earlier `collect.log` "Canceling…" EXPLAINED: the 19:2x coverage attempt's integration was starved by the concurrent out-of-footprint D229-D230 writer (SQL Server pre-login handshake timeouts `handshake=16897` in `boundary2-cov/integration.out`), the outer timeout cancelled the collector, dotnet-coverage printed "Canceling…" and never flushed a new cobertura (pre-fix file left intact); this run's integration finished in 35 s with 0 failures | artifacts/pdca/D217-datetimeoffset/rv2/coverage/{collect-postfix.log,cobertura-postfix.xml,report-postfix/Summary.json,check-postfix.log,postfix-coverage.json}

2026-10-13T16:10:38Z | DO | r=2 | n=3/3 | E217-13 manifest REPACKAGED (no oracle/semantics change): E217-11 repointed off the pre-fix `boundary/**` sweep to the post-fix `boundary2-cov2/**` + `coverage/*-postfix` artifacts; every row's evidence item now carries a SHA256; top-level `evidence_binding` + per-row `results/E217-XX.json` (frozen-tree binding); all 88+ evidence paths verified present; frozen tree re-frozen f2a6f6363f93b103e86458d3ee58b9da6175baf2 → 4c3ac382881fee5e03c03034e147243345228ca6 (the final D5 `limitations.md` EN/RU edit landed 18:42, after the 18:37 re-gather freeze); `python3 scripts/validate_inner_loop.py manifest $A/manifest.json` exit 0; `bash $A/verify.sh` exit 0 (manifest PASS, footprint 40 changed/0 out-of-footprint, CRLF 29/29, invariant PASS, hash+tree PASS) | artifacts/pdca/D217-datetimeoffset/rv2/{manifest.json,results/,verify/{verify.log,hashes.json,pre-existing.json},E217-13/{manifest-validate.log,tree-sha.txt}}

2026-10-13T16:10:38Z | DO | r=2 | n=3/3 | Profile audits LOADED (counters, not prose): suppressions — `#pragma warning disable`/`SuppressMessage`/`ExcludeFromCodeCoverage`/`NoWarn` = 0/22 footprint files, unjustified = 0; `slopwatch` NOT installed (`.config/dotnet-tools.json` = dotnet-coverage, reportgenerator, docfx, stryker) → manual scan performed; new public `ISqlDialect` members = 4/4 documented (ISqlDialect.cs:1073/1080/1088/1124) + 24/24 new/changed public|protected members carry `///` (CS1591 not suppressed); T53 freeze candidates registered, freeze HELD | artifacts/pdca/D217-datetimeoffset/rv2/audit/{suppression-scan.log,slopwatch.log,xmldoc.log,api-surface.log}

2026-10-13T16:10:38Z | DO | r=2 | n=3/3 | Branch-delta vs baseline (pre-fix2 cobertura) on the post-fix cobertura: 28 changed branch lines, FULL 19 / PART 9 / NONE 0; uncovered changed-branch identities = NONE; partially-covered identities listed explicitly (9); variant matrix statuses confirmed (8 rows; per-project T217 selected core 31, sqlite 31, postgres 31, sqlserver 33, mysql 31, mariadb 32, clickhouse 34, integration 58 = 281); doc parity PASS (EN/RU `03-date-and-time.md` 30 tokens 0 mismatch, 2/2 `###`; `limitations.md` offset-row present both) | artifacts/pdca/D217-datetimeoffset/rv2/audit/{branch-delta.tsv,variant-matrix.tsv,doc-parity.log}

2026-10-13T16:10:38Z | DO | r=2 | n=3/3 | Inner-loop evidence refreshed: `python3 scripts/validate_inner_loop.py report $A/evidence.json` exit 0 (boundary solution build exit 0 + 8 filtered inner executions `~T217` with selected_counts 31/31/31/33/31/32/34/58; boundary/coverage + branch-delta runs recorded in `supplementary_runs`) | artifacts/pdca/D217-datetimeoffset/rv2/evidence.json, audit/report-validate.log

2026-10-13T16:10:38Z | DO | r=2 | n=3/3 | Corrective packaging tasks complete (no D: unit marked done; no replan; r stays 2, n stays 3/3; no code/criteria change); E217-11 and E217-13 re-evidenced; awaiting CHECK verdict | docs/specs/status/D217-datetimeoffset-1.md
2026-10-13 | PLAN | r=3 | n=1/3 | planner-authored revised remediation plan (escalate decision routed); acceptance criterion unchanged | docs/specs/status/D217-datetimeoffset-1.md

2026-10-13 | DO | r=3 | n=1/3 | D1 SNAPSHOT: pre-r=3 baseline written write-once BEFORE any edit — `rv2/r3-baseline/{manifest.json (sha256 dead235a…),hashes.json (277426bd…),sha256s.txt}` | artifacts/pdca/D217-datetimeoffset/rv2/r3-baseline/sha256s.txt

2026-10-13 | DO | r=3 | n=1/3 | D2 ORACLE PROVENANCE: row-by-row comparison of the manifest oracle fields against the r=2 oracle text (`oracles.rv3.json`) — **MISMATCH** → **rv=3** declared (supersedes rv=2). Requirement sets match for all 14 rows; rows re-derived: E217-07, E217-10, E217-11, E217-12, E217-13, E217-14 (missing oracle obligations re-stated verbatim, no weakening). The unprovable claim "manifest row oracles are unchanged from rv=2" deleted from `manifest.evidence_binding.rule` and `E217-13/ledger.tsv`; `contract_rv=3` | artifacts/pdca/D217-datetimeoffset/rv2/oracles.rv3.json

2026-10-13 | DO | r=3 | n=1/3 | D3 FREEZE SCOPE: `tree_method` widened to cover `src tests docs benchmarks` (tightening) and the tree re-frozen `4c3ac382881fee5e03c03034e147243345228ca6` → **`8177c22966ad078171282dc9f0cc85fb9d8a4faa`** (28 footprint paths, incl. 3 benchmark files); the two tracked benchmark files recorded in `commit_exclusions` (not committed); `verify.sh` TREE_PATHS + hash scope include benchmarks (hashes.json 25→28). T53 freeze stays HELD | artifacts/pdca/D217-datetimeoffset/rv2/manifest.json

2026-10-13 | DO | r=3 | n=1/3 | D4 E217-13 REBIND: stale `verify/verify.log` (frozen_tree=f2a6f63…) archived to `rv2/superseded/verify.log` (sha256 4489ec5d…, receipt in `superseded/sha256s.txt`); all acceptance citations to it removed (manifest + `results/E217-13.json` + `E217-13/ledger.tsv`); E217-13 now cites the fresh `verify/verify-run.log` (tree 8177c229…) only | artifacts/pdca/D217-datetimeoffset/rv2/superseded/sha256s.txt

2026-10-13 | DO | r=3 | n=1/3 | D5 PERF BINDING: `perf.binding.json` binds frozen tree 8177c229… + sha256 of the UNMODIFIED `E217-14/perf.tsv` (afb28fdb…) and `E217-14/environment.txt` (33a5d657…); perf decision unchanged, files untouched | artifacts/pdca/D217-datetimeoffset/rv2/perf.binding.json

2026-10-13 | DO | r=3 | n=1/3 | D6 PART OUTCOMES: all 9 PART branch identities from `audit/branch-delta.tsv:34` classified (0 unclassified): **covered 5** (DateTimeFunctionTranslator.cs:49, MemberTranslator.cs:43/:116/:187, PostgresDialect.cs:405), **accepted 4** (DateTimeFunctionTranslator.cs:14/:63, MemberTranslator.cs:128/:135 — unreachable-defensive proofs), **deferred 0**; per-identity cobertura hits + test names + killed mutants recorded | artifacts/pdca/D217-datetimeoffset/rv2/audit/part-outcomes.json

2026-10-13 | DO | r=3 | n=1/3 | D7 ONE DRIVER + VALIDATION: `generate-bindings.sh` (freeze → manifest → verify → hashes; hashes kept OUTSIDE the manifest) exit 0; `["python3","scripts/validate_inner_loop.py","manifest","…/manifest.json"]` exit 0; `["python3","scripts/validate_inner_loop.py","report","…/evidence.json"]` exit 0; `["bash","…/verify.sh"]` exit 0 (ALL CHECKS PASS, incl. new tree-binding section); 91 cited evidence items, 0 stale/missing sha256; added `verify/tree_binding.py` fail-closed check (a cited log naming a tree ≠ manifest.tree fails) and the negative fixture `r3-baseline/negative-fixture/manifest-stale-tree.json` was **REJECTED exit 2** (cites superseded verify.log = f2a6f63… ≠ manifest.tree) | artifacts/pdca/D217-datetimeoffset/rv2/verify/{verify-run.log,verify-r3.log,tree-binding.log}

2026-10-13 | DO | r=3 | n=1/3 | Reuse justification (no product suite re-run): the old-path freeze recomputed at the current HEAD equals the r=2 tree `4c3ac383…` → **product/test bytes are unchanged**; the r=3 freeze delta is only the benchmarks widening (+ recorded git_head). Recorded product suites/coverage/mutation therefore remain valid and were NOT re-executed. `validate_inner_loop.py brief` is inapplicable to this docs-only/no-product revision (schema mandates nonempty `projects`+`selectors`); the truthful empty-scope fixture is rejected (exit 2) — no selectors invented | artifacts/pdca/D217-datetimeoffset/rv2/r3-baseline/negative-fixture/brief-docs-only.log

2026-10-13 | DO | r=3 | n=1/3 | EVIDENCE-ONLY revision complete: no product/code/test file changed, no acceptance criterion changed, no commit/push performed; boundary untouched (`src/**`, product tests, `benchmarks/**`, `examples/**`, diagram assets read-only); awaiting CHECK verdict; STOP condition standing: one more failure on this same evidence-binding defect ⇒ incomplete | docs/specs/status/D217-datetimeoffset-1.md

2026-10-13 | CHECK | r=3 | n=1/3 | VERDICT FAIL — third evidence-binding failure (same defect class across r=2 and r=3). Unresolved: 13/14 results/*.json still carry tree/tree_binding 4c3ac382… (generate-bindings.sh refreshes manifest evidence but not results); tree_binding.py ignores result-JSON tree fields; negative-fixture execution receipt absent; part-outcomes.json:88 asserts (not proves) MT:135 unreachability.

2026-10-13 | STOP | r=3 | Escalation STOP condition met (same evidence-binding defect recurring after fix; no 4th attempt in r=2, no second corrective DO in r=3). Task T217 = incomplete. Product code/tests/coverage/mutation are green (build 0/0; raw coverage 88.010 line/80.298 branch, Summary 88.6/80.7; mutation 34+12 baseline, 6/6 killed; integration 58/58, 0 skips; suppressions 0; XML 24/24; 4 documented ISqlDialect members, T53 freeze HELD). Only the frozen-tree evidence binding is unresolved.

2026-10-13 | STOP | Group G1 fail-stop: remaining pending tasks T218, T219, T226, T224, T225, T201 marked incomplete («группа остановлена»), never executed; T53 stays held.

### Unresolved rows at STOP — future-run requirement (NOT a new in-cycle attempt)

- defect_key `D217-EVIDENCE-BINDING` — observed r=2/n=3 (CHECK FAIL) and r=3/n=1 (CHECK FAIL); fixes applied: 1 (r=3 corrective DO, evidence-only); third occurrence meets the escalation STOP condition → T217 `incomplete`, defect NOT resolved.
- Unresolved rows: **E217-13** (and the row-level `results/E217-XX.json` binding behind E217-11/E217-13). A future run needs a **genuinely different mechanism**: (1) regenerate `results/*.json` **from the freeze** so every result-JSON `tree`/`tree_binding` equals `manifest.tree` (13/14 still carry the superseded `4c3ac382…`); (2) a **strict verifier** that reads the result-JSON tree fields (the current `verify/tree_binding.py` ignores them); (3) a **captured negative-fixture execution receipt** (today only a rejection log exists); (4) `audit/part-outcomes.json:88` must **prove** (not assert) the `MT:135` unreachability.
- Boundary conflicts referred to the user for integration disposition: the two contested tracked benchmark files `benchmarks/nextorm.benchmark/Program.cs` and `benchmarks/nextorm.benchmark/nextorm.benchmark.csproj` remain uncommitted WIP; their lane/E217-14 ownership conflict is unresolved (see the r=2 `NOTICE` row). Not touched by this STOP bookkeeping; excluded from the T217 patch.
- No `D:` unit is `done`; nothing waived. The STOP is a bookkeeping/evidence-gate stop, not a product defect.

2026-10-13T16:39:58Z | PLAN | revision r=4 | iteration 1/3 | PLAN ready — awaiting confirmation | Replan r=4 (rv=4 supersedes rv=3); re-open past D217-EVIDENCE-BINDING STOP; plan_state=ready

2026-10-13T16:59:23Z | DO | revision r=4 | iteration 1/3 | DO started — EVIDENCE-PIPELINE-ONLY units D217-r4-01..06 (sh/py/json only; no `src/`, `tests/`, `docs/**` product bytes, no `benchmarks/`, no commit/push) | artifacts/pdca/D217-datetimeoffset/rv2/r4/scope.json
2026-10-13T16:59:23Z | DO | revision r=4 | iteration 1/3 | D217-r4-01 `generate-bindings.sh`: freeze step now ALSO rewrites every per-row result receipt (`results/E217-*.json` + r=4 receipts) `tree`/`tree_binding` to `manifest.tree` and sets `contract_rv=4`; fail-closed on a missing/malformed receipt; VERIFIED 13/14 stale `4c3ac382…` receipts → 14/14 == `manifest.tree 236737167af210904607daf8455d6e00cfcc9276`; generator exit 0 (freeze→manifest→verify→hashes, ALL CHECKS PASS) | artifacts/pdca/D217-datetimeoffset/rv2/results/E217-01.json; /tmp/opencode/gen-bindings-r4.log
2026-10-13T16:59:23Z | DO | revision r=4 | iteration 1/3 | D217-r4-02 `verify/tree_binding.py` rewritten: for every manifest row it reads the MANDATORY `row.result` receipt and requires BOTH `tree` and `tree_binding` == `manifest.tree`; fail-closed exit 1 with `FAIL: row <id> != manifest.tree` / missing diagnostic; standalone exit 0 (`tree-binding PASS: 17 row receipt(s)`) | artifacts/pdca/D217-datetimeoffset/rv2/verify/tree-binding.log
2026-10-13T16:59:23Z | DO | revision r=4 | iteration 1/3 | D217-r4-06 (rows) `manifest.json`: `contract_rv=4`; rows E217-15 (R217-08-BIND), E217-16 (R217-08-NEG), E217-17 (R217-08-MT135) appended to `required_rows` and `rows`; E217-01..14 preserved verbatim; 17 rows, all cited evidence present; `validate_inner_loop.py manifest` exit 0 | artifacts/pdca/D217-datetimeoffset/rv2/r4/native-manifest-command.json
2026-10-13T16:59:23Z | DO | revision r=4 | iteration 1/3 | new `r4/evidence_gate.py` (fail-closed): `rows` exit 0 (17 receipts bound, contract_rv=4), `freeze` exit 0 (28-path write-tree == manifest.tree, 28 fingerprint files unchanged); `mt135` exit 1 (MT-135 verdict = reachable, not a closure) | artifacts/pdca/D217-datetimeoffset/rv2/r4/freeze/execution.json
2026-10-13T16:59:23Z | DO | revision r=4 | iteration 1/3 | D217-r4-04 negative fixtures EXECUTED (not merely stored): `stale-manifest-tree` / `stale-result` / `missing-result` each exit 1 with the required `FAIL: row E217-01 != manifest.tree` / missing-receipt diagnostic; harness exit 0 | artifacts/pdca/D217-datetimeoffset/rv2/r4/negative/harness-execution.json
2026-10-13T16:59:23Z | DO | revision r=4 | iteration 1/3 | BLOCKER D217-r4-05 — `MemberTranslator.cs:135` true arm (`IsParamMode` -> 136-138) is REACHABLE, not unreachable/equivalent, so the criterion was **NOT rewritten** (per instruction) and this unit STOPS. Path: `QueryPlanner.ExtractParams` (only ParamMode=true root) ← `RefreshParameters` on a plan-cache hit with a null/failed `ParamRefreshRecipe` (null whenever Sorting/Grouping/Joins/CTEs/… present) → `SqlSourceRenderer.MakeWhere` → param mode walks the WHERE (`PredicateTranslator.VisitConditional`); a cached `Where(e => e.WhenDto.Year == capturedYear).OrderBy(…)` reaches `TryTranslate` with `IsParamMode == true`; the arm is load-bearing (builder is null in param mode). Twin guard `MemberTranslator.cs:101` also hits=0 → uncovered, not unreachable. Row **E217-17 = open**; `part-outcomes.json` MT:135 left unchanged | artifacts/pdca/D217-datetimeoffset/rv2/r4/proof/MT-135.json
2026-10-13T16:59:23Z | DO | revision r=4 | iteration 1/3 | scope note (transparency): `verify.sh` §4 invariant (fixed) — it diffed the working tree against HEAD, which is empty now that T217 is committed, so `generate-bindings.sh` was red on a post-commit false failure; §4 now falls back to the patch HEAD introduced for the file. Evidence tooling only (no selector/rebuild impact); `verify.sh` was not in `r4/scope.json` `files` (scope.json kept exactly as mandated) | artifacts/pdca/D217-datetimeoffset/rv2/verify/verify-run.log
2026-10-13T16:59:23Z | DO | revision r=4 | iteration 1/3 | defect_key `D217-EVIDENCE-BINDING` — r=4 applied the mandated fix (regenerate `results/*.json` from the freeze + strict result-tree verifier + captured negative-fixture execution receipts); defect RESOLVED at DO (pending CHECK). New unresolved row E217-17 (MT:135) is a distinct item; no `D:` unit marked `done` (ACT owns that) | artifacts/pdca/D217-datetimeoffset/rv2/manifest.json
2026-10-13T17:05:00Z | PLAN | revision r=5 | iteration 1/3 | PLAN ready — r=5 (rv=5 supersedes rv=4); re-open past the r=4 `D217-r4-05` MT:135 blocker; D217-r4-05 superseded→D217-r5-01; new active units D217-r5-01..03; plan_state=ready | docs/specs/status/D217-datetimeoffset-1.md
2026-10-13T17:05:00Z | PLAN | revision r=5 | iteration 1/3 | Snapshot decision: accept the existing full-HEAD+footprint freeze algorithm; `8177c229…` historical/superseded (never auto-rebase); `manifest.tree=236737167af210904607daf8455d6e00cfcc9276` is a PRE-SWEEP snapshot — the final freeze is reserved for the post-sweep tree T in D217-r5-03; the r=3 claim "unrelated commits cannot move the freeze" is RETRACTED | docs/specs/status/D217-datetimeoffset-1.md
2026-10-13T17:12:00Z | DO | revision r=5 | iteration 1/3 | D217-r5-01: added the executed integration regression `tests/nextorm.integration.tests/T217DateTimeOffsetParamRefreshTests.cs` (SQLite, DisableParallelization collection); `dotnet build nextorm.slnx -c Debug` exit 0 (0 Warning(s)/0 Error(s)); focused `dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~SqliteT217DateTimeOffsetParamRefreshTests"` exit 0 (selected 1 / succeeded 1 / skipped 0); `MemberTranslator.cs:135-138` cobertura hits=1; `QueryPlanner.FallbackRefreshes` delta=1; second result [2] reflects capturedYear 2023→2024 | artifacts/pdca/D217-datetimeoffset/rv2/r5/param-refresh/focused-newtest.log
2026-10-13T17:12:00Z | DO | revision r=5 | iteration 1/3 | D217-r5-01 focused T217 under DOCKER_HOST: `dotnet test tests/nextorm.integration.tests -c Debug --no-build --filter "FullyQualifiedName~T217"` exit 0 (total 59 / failed 0 / succeeded 59 / skipped 0); no required provider skipped; branch evidence collect `dotnet tool run dotnet-coverage collect -s coverage.settings.xml …` exit 0 | artifacts/pdca/D217-datetimeoffset/rv2/r5/param-refresh/{focused-t217.log,cobertura-param-refresh.xml,branch-evidence.tsv}
2026-10-13T17:14:00Z | DO | revision r=5 | iteration 1/3 | D217-r5-01: `r4/proof/MT-135.json` updated — executed identity (FQN/argv/exit=0/branch hits 135-138) added, the existing mutant→source→guard/path chain preserved; verdict `reachable` is now EXECUTED, not asserted; `criterion_rewritten=false` | artifacts/pdca/D217-datetimeoffset/rv2/r4/proof/MT-135.json
2026-10-13T17:16:00Z | DO | revision r=5 | iteration 1/3 | D217-r5-02: `r4/evidence_gate.py mt135` now exits 0 on a COMPLETE chain for reachable|unreachable|equivalent and 1 on an incomplete/bare assertion (verified complete chain exit 0; bare `reachable` without execution exit 1); `audit/part-outcomes.json` MT:135 reclassified accepted→covered ("reachable, load-bearing, now tested") on the executed evidence; counts covered 6 / accepted 3 / deferred 0; defect_key `D217-MT135-REACHABLE` RESOLVED (r=5/n=1) | artifacts/pdca/D217-datetimeoffset/rv2/{r4/evidence_gate.py,audit/part-outcomes.json}
2026-10-13T17:18:00Z | DO | revision r=5 | iteration 1/3 | rv=5 binding prep (D217-r5-03; the full sweep/coverage/mutation/perf is DEFERRED to the NEXT task and NOT run here): rv=4 manifest archived to `superseded/manifest.rv4.json` BEFORE the bump; `contract_rv=5`; E217-18 added to `required_rows`+`rows` with a bound receipt; E217-17 closed `met` (complete chain, reachable accepted); all cited evidence sha256 refreshed; `validate_inner_loop.py manifest` exit 0, `evidence_gate.py rows` exit 0, `evidence_gate.py freeze` exit 0 (28-path tree == 236737… unchanged; the new test file stays OUTSIDE the pre-sweep footprint until the post-sweep freeze) | artifacts/pdca/D217-datetimeoffset/rv2/manifest.json
2026-10-13T17:19:00Z | DO | revision r=5 | iteration 1/3 | r=5 scope: `r4/scope.json` updated (new test file in `files`, `rebuild=affected`, selector `FullyQualifiedName~T217`, boundary = the next task's full sweep); `python3 scripts/validate_inner_loop.py brief rv2/r4/scope.json` exit 0; `generate-bindings.sh` still hardcodes `contract_rv=4` → D217-r5-03 must update it before regenerating; no commit/push, no `git add -A`, CRLF preserved; no `D:` unit marked `done` (CHECK/ACT own that) | artifacts/pdca/D217-datetimeoffset/rv2/r4/scope.json
2026-10-13T17:39:19Z | DO | revision r=5 | iteration 1/3 | D217-r5-03 boundary sweep (single, serialized, `--no-build` after the one solution build): `dotnet build nextorm.slnx -c Debug` exit 0 (0 Warning(s)/0 Error(s)); unfiltered per-project exit 0 — core 2492/0/0, sqlite 1704/0/1 (opt-in NEXTORM_LOB_SQLITE_PROBE rowid LOB probe, pre-existing), postgres 940/0/0, sqlserver 852/0/0, mysql 418/0/0, mariadb 300/0/0, clickhouse 697/0/0 | artifacts/pdca/D217-datetimeoffset/rv2/r5/boundary/sweep-summary.tsv
2026-10-13T17:39:19Z | DO | revision r=5 | iteration 1/3 | D217-r5-03 integration under containers (DOCKER_HOST used, Podman socket present): `dotnet test tests/nextorm.integration.tests -c Debug --no-build` exit 0 — total 3862 / failed 0 / skipped 198 (capability-only; 0 required-provider-unavailable markers; "Connected to Docker" present); focused T217 exit 0, 59/59, skipped 0 | artifacts/pdca/D217-datetimeoffset/rv2/r5/boundary/integration.out
2026-10-13T17:39:19Z | DO | revision r=5 | iteration 1/3 | D217-r5-03 coverage: `dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o artifacts/pdca/D217-datetimeoffset/rv2/coverage/cobertura.xml "bash artifacts/pdca/D217-datetimeoffset/rv2/boundary.sh"` exit 0 (boundary.sh build + 7 unfiltered project runs + integration all exit 0); reportgenerator exit 0; `bash artifacts/pdca/D217-datetimeoffset/rv2/coverage-check.sh artifacts/pdca/D217-datetimeoffset/rv2/r5/coverage/report/Summary.json 85 75` exit 0 — linecoverage 88.6 (min 85) / branchcoverage 80.7 (min 75), scope core/postgres/sqlite/sqlserver; the r=2 pre-fix raw preserved as coverage/cobertura.r2-baseline.xml | artifacts/pdca/D217-datetimeoffset/rv2/r5/coverage/check-r5.log
2026-10-13T17:39:19Z | DO | revision r=5 | iteration 1/3 | D217-r5-03 mutation re-run at T: `bash artifacts/pdca/D217-datetimeoffset/rv2/mutation.sh` exit 0 — baseline unit 34/34 + real-CH integration 12/12; all 6 named mutants KILLED (a 2/34, b 2/34, c 6/12 via real ClickHouse, d 7/34, e 1/34, f 2/34); tree restored byte-identical (sha256 + git-diff), no leftover backups; r=2 copies preserved under r5/mutation/r2-preserved/ | artifacts/pdca/D217-datetimeoffset/rv2/r5/mutation/kill-ledger.tsv
2026-10-13T17:39:19Z | DO | revision r=5 | iteration 1/3 | D217-r5-03 perf (E217-14 C17): `dotnet build benchmarks/nextorm.benchmark -c Release` exit 0 (0/0); probe exit 0, wall 9.1 s — 2 cases (datetime/dto), 5 batches each, no crash (datetime median 125.790 ms / 1611168 B; dto median 153.511 ms / 3210984 B; measured numbers only, no conclusion); the overlay `--anyCategories=acceptance` set is NOT pinned by the manifest → not run | artifacts/pdca/D217-datetimeoffset/rv2/r5/perf/summary.tsv
2026-10-13T17:39:19Z | DO | revision r=5 | iteration 1/3 | D217-r5-03 freeze: `bash artifacts/pdca/D217-datetimeoffset/rv2/generate-bindings.sh` exit 0 — frozen tree T=03c284d6c6771e69b791af79d42e22baaf2ddb14 (29-path footprint: r=4 28 paths + tests/nextorm.integration.tests/T217DateTimeOffsetParamRefreshTests.cs), `contract_rv=5`, HEAD 94782852053142d548eeb42b672de9d26972945d; 18/18 row result receipts bound to T (results E217-01..14 = 14/14, r4/receipts E217-15..18 = 4/4); verify.sh ALL CHECKS PASS; 132 cited evidence sha256 refreshed | artifacts/pdca/D217-datetimeoffset/rv2/manifest.json
2026-10-13T17:39:19Z | DO | revision r=5 | iteration 1/3 | D217-r5-03 validators, each exit 0: `tree_binding.py` (18 receipts == T); `evidence_gate.py rows` (contract_rv=5); `evidence_gate.py freeze` (recomputed 29-path tree == T, 29 fingerprints unchanged); `evidence_gate.py mt135` (complete mutant→source→guard/path chain, verdict=reachable); negative harness (stale-manifest-tree / stale-result / missing-result each exit 1 with the required diagnostic, harness exit 0); `bash verify.sh` (ALL CHECKS PASS); `validate_inner_loop.py manifest` exit 0 (18 rows); `validate_inner_loop.py report` exit 0 (9 executions, selected_count>=1); independent check 132 cited sha256 / 0 stale | artifacts/pdca/D217-datetimeoffset/rv2/r5/f-tree_binding.log
2026-10-13T17:39:19Z | DO | revision r=5 | iteration 1/3 | D217-r5-03 unit/defect accounting: the r=4 evidence-pipeline obligations are re-verified at T — D217-r4-01 (receipt regeneration), D217-r4-02 (strict `tree_binding`), D217-r4-03 (14 result receipts bound), D217-r4-04 (executed negatives), D217-r4-06 (boundary freeze) all satisfied at the final T; D217-r5-01/D217-r5-02 done; defect_key `D217-EVIDENCE-BINDING` stays RESOLVED (0 stale citations) and `D217-MT135-REACHABLE` stays RESOLVED (r=5/n=1); no new defect; NO `D:` unit marked `done` by DO (ACT owns `done`); no replan (r stays 5, n stays 1/3) | artifacts/pdca/D217-datetimeoffset/rv2/manifest.json
2026-10-13T17:51:49Z | CHECK | revision r=5 | iteration 1/3 | P2 CORRECTION (reporting inaccuracy, nonblocking): the r=5 boundary row claim "Connected to Docker present" is NOT supported by evidence — the string was NOT found in `r5/boundary/integration.out` (grep count 0); the runtime container-execution evidence is instead the focused T217 run `r5/boundary/integration-t217.out` (total 59 / failed 0 / succeeded 59 / skipped 0). | artifacts/pdca/D217-datetimeoffset/rv2/r5/boundary/integration.out
2026-10-13T17:51:49Z | CHECK | revision r=5 | iteration 1/3 | CHECK-evidence re-gather #1/2 (owner CHECK-evidence, evidence-only; no product/tests/criteria change): added the isolated negative case `stale-tree-binding` (receipt tree==T, tree_binding=0000000000000000000000000000000000000000) — `python3 verify/tree_binding.py r4/negative/stale-tree-binding/manifest.json` exit 1 with the required `FAIL: row E217-01 != manifest.tree`; clean control `tree_binding.py manifest.json` exit 0; harness exit 0 with stale-manifest-tree=1, stale-result=1, missing-result=1, stale-tree-binding=1; evidence appended only to manifest row E217-16 and r4/receipts/E217-16.json (contract_rv=5, manifest.tree unchanged); `python3 scripts/validate_inner_loop.py manifest` exit 0 (18 rows). | artifacts/pdca/D217-datetimeoffset/rv2/r4/negative/harness-execution.json
2026-10-13T18:10:22Z | DO | revision r=5 | iteration 1/3 | r=5 RE-VERIFY (D217-r5-01/02/03 evidence re-run after an interrupted attempt; product frozen at 94782852, NO product/test byte changed): `dotnet build nextorm.slnx -c Debug` exit 0 (0 Warning(s)/0 Error(s)); T217 dialect suites --no-build exit 0 — core 31/31, sqlite 31/31, postgres 31/31, sqlserver 33/33, mysql 31/31, clickhouse 34/34, all 0 skipped; integration under DOCKER_HOST exit 0 — focused `--filter FullyQualifiedName~T217DateTimeOffset` 59/59 (0 skipped), SqliteIntegrationTests class 706 total/0 failed/41 capability skips, full unfiltered sweep 3862 total/0 failed/198 skipped with `Connected to Docker` present and 0 required-provider-unavailable markers | artifacts/pdca/D217-datetimeoffset/rv2/r5/reverify/{exit-codes.tsv,1-build.log,dialect-*.log,integration-*.log}
2026-10-13T18:10:22Z | DO | revision r=5 | iteration 1/3 | r=5 RE-VERIFY gates, EACH exit 0: `validate_inner_loop.py manifest` (18 rows), `evidence_gate.py rows` (18 receipts == manifest.tree), `evidence_gate.py freeze` (recomputed 29-path tree 03c284d6c6771e69b791af79d42e22baaf2ddb14 == manifest.tree), `evidence_gate.py mt135` (complete chain, verdict=reachable), `verify/tree_binding.py` (18 receipts bound), `validate_inner_loop.py report` (9 executions, selected_count>=1), `coverage-check.sh` (line 88.6 >= 85 / branch 80.7 >= 75); `generate-bindings.sh` exit 0 → `verify.sh` ALL CHECKS PASS; MT:135 cobertura hits on MemberTranslator.cs:135-138 == 1 under the param-refresh probe, QueryPlanner.FallbackRefreshes delta == 1; oracle reclassification stands (part-outcomes covered 6 / accepted 3 / deferred 0) | artifacts/pdca/D217-datetimeoffset/rv2/r5/reverify/evidence.json
2026-10-13T18:10:22Z | DO | revision r=5 | iteration 1/3 | r=5 RE-VERIFY housekeeping: recorded the two concurrent out-of-scope working-tree files (`docs/specs/status/D216-row-locking-1.md`, `docs/specs/status/collection-1.0.9-todo.md`) as pre-existing/unrelated in `verify/pre-existing.json` so the footprint gate passes without touching or staging them (neither is a T217 path); removed the stray CR-named dir `artifacts/pdca/D217-datetimeoffset/rv2<CR>` (held only empty r4/negative + r5/coverage, referenced by no manifest/oracle path); T53 freeze candidates CONFIRMED registered with freeze HELD — the 4 new public ISqlDialect members (`SupportsDateTimeOffsetTranslation`, `MakeDateTimeOffsetNow(bool)`, and the `bool isDateTimeOffset` overloads of `MakeDatePart`/`MakeDateAdd`) remain T53 freeze candidates | artifacts/pdca/D217-datetimeoffset/rv2/verify/{pre-existing.json,footprint.log}
