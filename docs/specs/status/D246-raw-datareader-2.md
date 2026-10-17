# D246 — Perf: ToDataReader raw reader (rv2: measurement-protocol revision)

**Task:** T246 · **Issue:** #246 (https://github.com/AlexeyShirshov/nextorm/issues/246) · **Milestone:** `1.0.9-rc3`
**Plan state:** `plan_state: ready` · **Phase:** PLAN → protocol qualification · **Selected variant:** inherited `V2`
**Counters:** `N=1` · `r=2` · `n=1` · iteration `1/3` · `rv=2` · CHECK re-gather budget `2`
**Base:** branch `1.0.9-rc3` @ `adcdb0f9`; collection baseline remains `01815f3a`.
**Supersession:** rv2 supersedes rv1's executable measurement protocol, not its criteria, evidence or terminal GAP record.
**Mode:** autonomous; coder persists this plan before DO; no user approval or invitation is required.

## Why rv2

Owner decision: «Ревизия протокола T246, затем очередь». This satisfies rv1's explicit reopen condition, not permission to rerun the failed protocol blindly.
rv1 stopped at D246-01 before production changes: clean-window baseline2 failed `Error/Mean ≤10 %` and spread `≤19 %` (`D246-raw-datareader-1.md:465-472`).
Preserved evidence: `artifacts/pdca/T246/rv1/perf/baseline2/`; driver exit 0, three runs of 22/22/23 seconds, one exclusive lock.
Observed B bimodality: ratios `1.192602/1.236039/0.823603`, spread `50.08 %`; fast run had `n=70`, others approximately `13`.
Observed empty: allocation `7.94/7.86/7.86×`; nextorm Error/Mean `8.49–11.74 %`, linq2db `9.46–12.33 %`.
The old monitor had only three pre-run snapshots; it proves neither in-run frequency stability nor absence of thermal effects.

## Scope of the revision

Change protocol, precision qualification, mode handling and allocation-oracle publication only; no production code changes during protocol qualification.
Inherited production scope, acceptance IDs, unsupported-provider guards, lifetime/cancellation/cache invariants and substantive gates remain unchanged.
**Binding:** `time_upper_ratio ≤ 1.00`, `allocation_ratio ≤ 1.00`, empty-arm tolerance **0 %**.
**Owner-visible consequence:** canonical out-of-process empty allocation is **7.86–7.94×**, not 2.31×. Closing T246 therefore requires a real production allocation reduction; this revision makes the target harder and honest, not easier.
Minimal solution: retain out-of-process `Job.Default`, bound iteration counts, split short/long timing strata without changing benchmark bodies, qualify precision before inherited V2 work.

| Alternative | Benefit | Cost / risk | Decision |
|---|---|---|---|
| One global invocation count | Simplest job | Empty needs thousands; 10k would make qualification unnecessarily long | Reject |
| Per-method count attributes | Short/long counts in one invocation | Harness annotations create multiple resolved jobs and add precedence/maintenance risk | Reject |
| Two filtered CLI invocations, fixed counts | Same Default toolchain; explicit independent strata; no count attributes | Two resolved job identities, both strictly validated and matched baseline/final | Choose |

## Acceptance criteria

| ID | Наблюдаемый критерий | Негативный случай |
|---|---|---|
| **R246-01** | Для 10k выполнен ratio gate времени и allocation, описанный ниже | Превышение, NA, пропущенный arm или недостаточная точность не являются PASS |
| **R246-02** | Empty arm проходит тот же gate с допуском **0 %** | Не применять самостоятельно «owner-approved tolerance»: его нет |
| **R246-03** | Значения, число строк/полей, metadata, ordinals, single/multi-column/blob проекции и параметры сохранены; материализации нет | Потеря столбца, locator в пользовательском результате, неверное значение/ordinal, появление materializer |
| **R246-04** | Close/Dispose/DisposeAsync идемпотентны; per-call reader и command освобождаются, connection не становится собственностью owner; `IsClosed`/`RecordsAffected` сохранены | Утечка, двойное освобождение, преждевременное закрытие connection, неверные terminal properties |
| **R246-05** | Sync/async и оба вида токена сохраняют отмену до открытия, после открытия и при чтении | Non-cancellable fast path проглатывает отмену либо async теряет per-call token |
| **R246-06** | Поддерживаемые провайдеры и все guards сохраняют поведение | Unsupported provider/dialect, in-memory или запрещённый temp-table source неожиданно исполняется |
| **R246-07** | `ToStream`/`ToTextReader`, LOB locator/sequential behavior и CSV не меняются | Новое raw-reader поведение попадает в LOB/CSV или меняет empty LOB semantics |
| **R246-08** | Повторная подготовка изолирована по terminal mode и всем необходимым вариантам shape/behavior; mutable command/reader/owner не кэшируются; общий `QueryCommand.Cache` не меняется | Коллизия raw/LOB/CSV/materialized preparation, stale shape/values либо sticky cache mutation |
| **R246-09** | Добавлены воспроизводимый reader gate и строки 10k/empty; parser fail-closed | Missing/duplicate/NA, неверный job, dataset или несовпавший run принимается |
| **R246-10** | Публичные benchmark-таблицы и follow-up отражают B и empty, точность измерений и фактический outcome | Скрыты Error/StdDev, невалидный запуск назван зелёным, generator `--check` падает |
| **R246-11** | Coverage ≥85 % line и ≥75 % branch; новые runtime-развилки покрыты, целевые мутации убиты | Только compilation failure вместо killed mutation; новый непокрытый P1 branch |
| **R246-12** | Manifest и frozen-tree evidence валидны, обязательные строки закрыты доказательствами | Отсутствующий отчёт трактуется как выполненная проверка |
| **R246-13** | До изменения production-кода получены воспроизводимый baseline и факты о preparation reuse | Оптимизация выбрана по старому ShortRun или reuse введён без доказательства изоляции |
| **R246-14** | Protocol lock фиксирует argv, jobs, hashes, dataset, host/runtime и matching treatment | Job/count/environment/DB mismatch или изменение lock после qualification отвергается |
| **R246-15** | Qualification R=3 доказывает precision до первой production-правки; thresholds заморожены формулой ниже | Экстраполяция sample count, старый baseline или production-first не считаются доказательством |
| **R246-16** | Единственный canonical oracle — out-of-process Default; исторические jobs явно разделены | In-process ratio либо смешанные toolchains используются для PASS |
| **R246-17** | Бимодальность классифицирована наблюдаемыми фактами; gate проверяет худший допустимый product mode | Усреднение modes, удаление медленного mode или необъяснённая классификация noise |
| **R246-18** | Issue #246 и публичные таблицы согласованно исправляют oracle, precision и outcome | Старые 2.31× названы canonical; GAP назван решённой задачей |
| **R246-19** | Final R=3 совпадает с qualified treatment и проходит все original gates | Один удачный run, missing mode/arm или устаревший final после runtime/build-правки |

## Canonical measurement protocol (rv2)

Canonical family: **out-of-process `Job.Default`**, BDN `0.15.8`, Release, .NET 10; verify actual child runtime/JIT against the captured qualification identity.
`NEXTORM_BENCH_FULL=1` selects Default (`NextormConfig.cs:11-26`); CLI counts mutate that job, not add another. Never pass `-j`.
Two strata: `10k` selects four arms, `empty` selects three. Each benchmark/launch uses its own BDN child process; never one shared process for all arms.
Primary counts: `InvocationCount=64` for 10k, `4096` for empty; both `UnrollFactor=1`, `LaunchCount=3`, `WarmupCount=5`, `IterationCount=10`.
InvocationCount serves iteration duration, not an independent precision target; IterationCount remains 10 throughout rv2.
No per-method count attributes; retain `[IterationSetup] ResetReaderSink()`. Confirm bodies consume/dispose every invocation and parity remains correct.
R=3 means three complete runs of **both** strata, in order `10k → empty`, under one lock acquisition for the complete block, including any replacement run.
`A=/home/alex/sources/nextorm/artifacts/pdca/T246/rv2`; all paths below are planned, not an instruction to create a skeleton now.
Keep the **Python measure driver**: one hash-pinned stdlib implementation makes lock lifetime, DB preparation, continuous monitoring and budget accounting auditable without a second shell orchestration layer.

| Stratum | InvocationCount and duration justification | WarmupCount | IterationCount | LaunchCount | Executions per launch | Executions per arm | Estimated seconds per arm, including warm-up |
|---|---|---|---|---|---|---|---|
| 10k | 64; about 0.32–0.40 s/iteration at 5–6.2 ms/op | 5 | 10 | 3 | 640 | 1,920 | About 14–18 s |
| Empty | 4096; linq2db 72.35 µs gives about 0.296 s/iteration; nextorm about 0.869 s | 5 | 10 | 3 | 40,960 | 122,880 | About 13–39 s; Dapper measured separately |

Estimated seven-arm run: **2.5–3 minutes**, including 21 child launches; exact duration is recorded, not assumed.
Counts above exclude warm-up from execution/sample accounting; wall estimates use `(5+10)×InvocationCount×3×op_time`.
Empty 1024/2048 would give only about 74/148 ms for the **faster comparator**; choose 4096 rather than sizing from nextorm alone against the approximately 200 ms duration target.
These execution counts are not independent BDN observations: exported `Statistics.N` counts workload-result iterations, nominally 10 per launch before any reported filtering. Never replace that N with invocation executions.

```bash
# Planned driver: one lock for the entire block; exact BDN argv below.
python3 eng/perf/t246_evidence.py measure --phase baseline --runs 3 --max-attempts 4 \
  --root artifacts/pdca/T246/rv2 --lock /tmp/opencode/nextorm-bdn-host.lock \
  --lock-wait-seconds 3600 --run-timeout-seconds 600 \
  --total-measurement-budget-seconds 7200
# Final: same invocation, changing only --phase baseline to --phase final.
```

Driver executes the equivalent of `flock -x -w 3600 /tmp/opencode/nextorm-bdn-host.lock` once, never once per arm.
For `PHASE=baseline|final`, `I=1,2,3` and `STRATUM=10k|empty`, exact primary BDN argv is:

```bash
A=/home/alex/sources/nextorm/artifacts/pdca/T246/rv2
OUT="$A/perf/$PHASE/run-$I/$STRATUM"
DB="/tmp/opencode/T246/rv2/$PHASE/run-$I/test.db"
if [ "$STRATUM" = 10k ]; then INV=64; CATS=(A_RawReader B_RawReader); else INV=4096; CATS=(B_RawReader_Empty); fi
NEXTORM_BENCH_FULL=1 NEXTORM_BENCH_DB="$DB" \
dotnet run --project benchmarks/nextorm.benchmark -c Release -- \
  --filter "*SqliteBenchmarkStreaming*" --anyCategories "${CATS[@]}" \
  --invocationCount "$INV" --unrollFactor 1 --launchCount 3 \
  --warmupCount 5 --iterationCount 10 --exporters json csv --artifacts "$OUT"
```

Authorized duration remedy **during pre-production qualification only**: if `MinIterationTime` survives, double the affected stratum's InvocationCount once, `64→128` or `4096→8192`; record the changed argv and discard incompatible qualification runs.
The displayed BDN argv then changes only `INV` to that authorized value. Require three valid baseline runs at the selected counts within the same four-attempt ceiling.
Never raise IterationCount. A warning after the authorized increase, insufficient remaining slots to requalify, or a duration warning after protocol freeze causes protocol GAP.
Freeze selected invocation counts with the measured thresholds before production; final must use those exact counts. No final-only count increase or retrospective baseline substitution.

DB bootstrap, once under recorded preflight: `mkdir -p /tmp/nextorm-bench`; if absent, `cp benchmarks/nextorm.benchmark/data/test.db /tmp/nextorm-bench/test.db`.
Never overwrite an existing mismatching DB: report it and use an explicit in-cycle bootstrap dependency. Never synthesize rows.
Before each complete run, create its directory and byte-copy `/tmp/nextorm-bench/test.db` to `$DB`; both strata use that copy; verify unchanged SHA before/after each stratum.
Capture full SHA256 with `sha256sum`; expected committed seed is 753,664 B, abbreviated SHA `32bf10a0…72295`, `large_table=10000`, parity `rows=10000, checksum=422955955263575176`.
Existing class setup/parity/warm-up remains unchanged; empty SQL remains `where id < 0`. Prepared arm exists only for non-empty data.
Preflight uses the exact BDN argv plus `--list flat`; require exit 0 and precisely 4/3 selected benchmark methods. Validate resolved settings again in actual reports.
CSV preserves job/count/Error/StdDev; full JSON preserves raw statistics/memory; `.log` must provide independently recoverable statistics for every launch (`SqliteBenchmarkStreaming.cs:29-32`).
Before qualification, prove extraction of all three launches, requested/actual result-iteration counts, Mean/Error/StdDev/N, medians and job identity. Never copy pooled statistics into launch evidence.
Quiet window: no foreign benchmark/test/build/publish/coverage or active compiler work; suspend sibling collection measurements; idle compiler servers alone are not contamination.
Require 60 seconds pre-run quiet, load ≤`nproc/2`, then sample process CPU, load, CPU frequency availability/readings and thermal availability/readings every second through the block.
Record runtime knobs, GC mode, affinity, power/governor, SDK/runtime, OS, harness/config/driver hashes and pre/post foreign tracked-artifact snapshots.
Reject foreign CPU activity ≥one core for five consecutive samples, thermal throttling, or >15% sustained frequency shift between matched arms; preserve observations, not just a “quiet” verdict.
Unavailable frequency/thermal sensors are explicitly recorded; they cannot be cited to dismiss a bimodal host-state hypothesis.
Invalid/discarded runs remain in attempt-numbered directories, consume budget, and include reason/exit/monitor evidence. A reserve replaces a complete seven-arm run, never a favorable subset.
No invalidation merely because a ratio is bad or a product mode is slow. No mean/ratio averaging across runs.

## Precision predicates (rv2)

Qualification uses the selected authorized argv and the unchanged production tree. Its three valid runs become the canonical baseline; no second baseline selection.
For each launch, use BDN's unrounded reported confidence Error at its recorded confidence level, not StandardError or rounded markdown. Require confidence level **99.9 %** and positive `MeanL−ErrL`.
Qualification cap: every gate arm/launch has `e=Error/Mean ≤0.027`; every matched-mode ratio spread `max/min−1 ≤0.04`.
Let `e_q` be the measured worst applicable launch error fraction, and `s_q` the worst applicable matched-mode ratio spread.
Freeze `E*=ceil(10000×(e_q+0.003))/10000 ≤0.0300`; freeze `S*=ceil(10000×(s_q+0.010))/10000 ≤0.0500`.
These deterministic margins are **0.3 percentage point** for Error/Mean and **1 percentage point** for spread; store measured operands and exact resulting numbers in `perf/protocol-lock.json`.
Final requires every launch `e≤E*` and every represented matched-mode spread `≤S*`; qualification/final never exceed rv1's 10%/19% caps.
Execution-count precision projection uses `e_pred=e_rv1×sqrt(n_rv1/n_new)`:

| Arm class | rv1 worst / reference n | New execution count per launch / arm | Optimistic projection per launch / arm |
|---|---|---|---|
| 10k | 1.56% / 13 | 640 / 1,920 | `1.56%×sqrt(13/640)=0.222%` / `1.56%×sqrt(13/1920)=0.128%` |
| Empty | 12.33% / 13 | 40,960 / 122,880 | `12.33%×sqrt(13/40960)=0.220%` / `12.33%×sqrt(13/122880)=0.127%` |

These are **optimistic predictions, not evidence**: invocations share iteration/process state and are not independent statistical samples. Actual BDN confidence intervals, exported N and measured mode spreads alone authorize production.
The 2.7%/4% qualification caps remain unchanged; no attainability claim follows solely from the arithmetic.
Three independent launches per run expose process-start variation; do not pool them to conceal an imprecise process mode.
For each run and paired launch, calculate `time_upper_ratio=(MeanN+ErrN)/(MeanL−ErrL)`; require every pair ≤1.00 and report the maximum. Separately require all per-run pooled original gates.
Allocation uses unrounded `Memory.BytesAllocatedPerOperation`; require each A/B/empty per-run nextorm/linq2db ratio ≤1.00. Do not invent per-launch allocations if the exporter only reports a run aggregate.
Dapper is retained as a required diagnostic arm, not substituted for the linq2db denominator.

| Error bound on each side | Effective mean-ratio ceiling `(1−e)/(1+e)` |
|---|---|
| 1.0% | `0.99/1.01 = 0.980198` |
| 2.0% | `0.98/1.02 = 0.960784` |
| 3.0%, maximum frozen bound | `0.97/1.03 = 0.941748` |

Recompute and publish the effective ceiling using the **actual frozen E*** after qualification; the rows above are illustrative bounds, not a claim that E*=3%.
Precision does not relax `≤1.00`: at the maximum bound, mean nextorm time must be about 5.83% lower. Qualification must establish precision before optimization.

## Bimodality policy

Fixed counts remove adaptive-pilot/N drift; they do not authorize suppressing real runtime states. Preserve per-launch chronology, GC statistics and warnings.

| Hypothesis | Discriminating observation | Classification / action |
|---|---|---|
| Tiered JIT / OSR / PGO | Mode correlates with recorded tier/OSR events or within-process transition after warm-up | Default-runtime product state: retain and gate worst; no disabling tiering to obtain PASS |
| GC mode / collections | Mode correlates with child GC configuration or GC activity | Config mismatch invalid; legitimate default-runtime GC state retained |
| Process start | Stable different launch plateaus under identical recorded inputs | Reproduced product state retained; not an outlier |
| DB page cache / query preparation | Mode correlates with setup order, parity/warm-up omission, or measured preparation/cache transition | Missing prescribed setup invalid; legitimate warmed product state retained |
| Adaptive pilot / N drift | Requested counts differ from `10`, invocation counts differ from the lock, or filtering/count extraction is unexplained | Protocol violation; reject, never compare adaptive and pinned samples |
| Host frequency / thermal | Mode aligns with monitored frequency/throttling change or foreign CPU work | Contaminated run; replacement consumes reserve |
| Foreign session | Monitor identifies overlapping bench/test/build or sustained foreign CPU | Contaminated run; replacement consumes reserve |

Mode detector is fixed before qualification: sort launch medians; split only at adjacent gaps ≥15%; otherwise one mode. No post-result tuning of the boundary.
Multiple modes qualify only with an explanatory product-state report and each mode reproduced in at least one launch in **each of the three runs**; within-mode precision/spread caps still apply.
For multimodal comparators, evaluate every observed eligible nextorm/linq2db mode pairing within each matched run; the largest upper ratio governs. Do not select a favorable pairing.
Mode-specific spreads use those individual pairings across runs, not averaged centers. Report overall spread too; an explained product-mode separation is not hidden as precision noise.
If no stable split explains the spread, a mode is unreplicated, or host/product attribution remains unresolved within the qualification budget: terminal GAP.
Final may remove a baseline product mode only with unchanged treatment, all nine launch observations and an explicit elimination audit; never drop a slow observation from final evidence.

## Allocation-oracle reconciliation

Canonical oracle is out-of-process Default: independent benchmark children avoid the in-process toolchain's shared-host measurement regime.
Historical source is `Job.ShortRun + InProcessEmitToolchain`, 3 warm-ups/3 iterations/1 launch: historical report markdown `:8-9,39,41`, CSV `:19,21`; also `feature-benchmarks-2026-10-17.md:17,22-23` and `acceptance-benchmarks.md:20-21,49-50`.
Allocation arithmetic: `12740−8256=4484 B`, `5520−1040=4480 B`; common additive component ≈4.48 KB, discrepancy 4 B.
Between-arm differential: historical `7220 B`, Default run-1 `7216 B`, runs 2–3 `7136 B`. The toolchain overhead compresses the ratio; it does not remove the reader's allocation deficit.
Time residuals are `25.56 µs` and `36.85 µs`, **not** a common ≈130 µs constant. Historical and Default timings are job/regime-incomparable.
Decision: no additional two-job probe; arithmetic plus known toolchain/regime difference suffices to retire in-process figures as an acceptance oracle, not to claim a proven causal time-overhead model.
Historical 2.31× describes its historical regime; current 7.86–7.94× is a pre-existing production allocation problem under the canonical oracle.
Correct issue/public rows with both job labels, absolute allocations/differentials, precision and actual outcome; do not manufacture a conversion factor between jobs.
A contradiction in raw canonical byte counts, job identity or dataset parity blocks acceptance and follows the GAP rule below.

## Contract rows

All sources/artifacts are **planned** unless explicitly identified as existing rv1 evidence. Artifact cells below are relative to `artifacts/pdca/T246/rv2/`.

| Row / rv | Requirements; сценарий | Evidence kind и источник | Вызов; требуемый результат | Планируемые артефакты | Владелец | Applicability predicate |
|---|---|---|---|---|---|---|
| **E246-01 / 1** | R13: preflight, reuse feasibility, allocation attribution | semantic scout report через Roslyn; probe raw output | `C-RECON`, exit 0; положительный key/invalidation audit; измерения и unattributed remainder | `recon/**`, `recon/semantic-audit.md`, probe source/hash | scout + coder | Всегда (V2 выбран) |
| **E246-02 / 1** | R13: новый baseline | BDN machine reports, logs, dataset parity | `C-BDN`, baseline I=1..3; все exit 0; Default, все arms, accuracy protocol | `perf/baseline/run-{1,2,3}/**` | perf-coder | Всегда |
| **E246-03 / 1** | R3, R5, R6: wrapper/guards/cancellation | Unit test logs и discovery counts | `C-CORE`; exit 0, selected ≥1, новые branches закрыты | `tests/core-inner.log`, report JSON | test-coder | Всегда |
| **E246-04 / 1** | R3, R4: SQLite values, empty и lifetime | SQLite test results | `C-SQLITE`; exit 0, selected ≥1, owner/resource cases проходят | `tests/sqlite-inner.log`, branch map | test-coder | Всегда |
| **E246-05 / 1** | R8: cache/mode/shape/parameter isolation | Tests + reviewed diff + Roslyn impact | `C-CORE`, `C-SQLITE`; exit 0; нет shared flag mutation/кэширования mutable owner resources | `tests/reuse-isolation.md`, logs | coder + scout | Всегда (reuse — часть V2) |
| **E246-06 / 1** | R3–R8: полный boundary и warnings | Build/test output | `C-BOUNDARY`; каждый exit 0, tests selected >0, warnings-as-errors соблюдены | `boundary/**` | test-coder | Всегда |
| **E246-07 / 1** | R4–R7: cross-provider behavior/report | Реальные provider results и capability inventory | `C-INTEGRATION`; exit 0; SQLite/PG/SQL Server и unsupported checks MySQL/ClickHouse реально исполнены | `integration/**`, `cross-provider.md` | integration-coder | Всегда (planner/wrapper seam меняется) |
| **E246-08 / 1** | R1, R2: final 10k/empty | BDN raw reports + ratio calculation | `C-BDN`, final I=1..3, затем `C-GATE`; все exit 0 и все условия oracle | `perf/final/**`, `perf/gate.json` | perf-coder | Всегда |
| **E246-09 / 1** | R9: fail-closed gate | Negative fixture results | `C-GATE-NEGATIVE`; exit 0 = все плохие fixtures отвергнуты; перечислить каждый случай | `perf/gate-negative.json` | tooling-coder | Всегда |
| **E246-10 / 1** | R8: existing cached acceptance | Полные BDN outputs, existing acceptance result | `C-CACHED`; exit 0 плюс PASS существующего acceptance contract, не только process exit | `perf/cached-acceptance/**` | perf-coder | Всегда (V2) |
| **E246-11 / 1** | R11: coverage и branch delta | Coverage XML/Cobertura, branch map | `C-COVERAGE`; exit 0; line ≥85 %, branch ≥75 %; новые branches подтверждены | `coverage/**` | test-coder | Всегда |
| **E246-12 / 1** | R4, R5, R8, R11: mutations | Mutation patches, executions, restoration hash | `C-MUTATION`; exit 0; все валидные targeted mutations killed; clean rerun PASS | `mutation/**` | test-coder | Всегда |
| **E246-13 / 1** | R10: docs и generated table | Generated diff, precision/report review | `C-DOCS`; оба exit 0; B и оба empty представлены; результат не преувеличен | `docs/generator.log`, `docs/review.md` | docs-coder | Всегда |
| **E246-14 / 1** | R7, R12: LOB/CSV/footprint unchanged outside allowance | Full test outputs, semantic/diff audit | `git diff --check` exit 0; `C-BOUNDARY`/integration; review подтверждает scope, CRLF и LOB/CSV isolation | `review/footprint.md`, `review/lob-csv.md`, diff | coder + scout | Всегда |
| **E246-15 / 1** | R12: validated evidence bound to tree | Validator reports + frozen-tree manifest | команды ниже; каждая exit 0; required_rows точно совпадают, evidence не пуст | `inner-loop-*.json`, `manifest.json`, lint logs | coder; CHECK проверяет | Всегда |
| **E246-16 / 2** | R14: locked protocol and matched treatment | Driver/config hashes, resolved jobs, DB/host records | `C-PROTOCOL`; exit 0, exact 4+3 arms/counts and matching identities | `perf/protocol-lock.json`, `perf/preflight.json`, `perf/*/host-monitor.jsonl` | perf-coder | Always; qualification and final |
| **E246-17 / 2** | R15: demonstrated pre-production precision | Three valid baseline runs, launch statistics, production hash chronology | `C-QUALIFY`; exit 0, caps met, E*/S* frozen before production diff | `perf/qualification.json`, `perf/protocol-lock.json`, `review/production-hashes.json` | perf-coder; CHECK verifies | Always |
| **E246-18 / 2** | R16: one canonical allocation oracle | Raw byte counts, historical source references, reconciliation | `C-ORACLE`; exit 0, Default canonical, no mixed-job gate | `perf/oracle.json`, `perf/oracle-reconciliation.md` | perf-coder | Always |
| **E246-19 / 2** | R17: explain modes and gate worst state | Per-launch chronology, monitor, mode classification and pair ratios | `C-MODES`; exit 0, replicated explained modes, worst pairing retained | `perf/baseline/modes.json`, `perf/final/modes.json`, `perf/mode-decision.md` | perf-coder + scout | Always; unimodal outcome also recorded |
| **E246-20 / 2** | R18: synchronized oracle publication | Public diff, generator check, verified issue body | `C-DOCS`, `C-PUBLISH`; all exit 0, jobs/precision/outcome agree | `docs/review.md`, `docs/issue-246.md`, `docs/issue-246.json` | docs-coder | Always, including terminal GAP reporting |
| **E246-21 / 2** | R19, R1–R2: matched final R=3 and unchanged gates | Full final raw reports, lock comparison, fail-closed gate | `C-GATE`; exit 0 only for complete matched R=3 and every ≤1.00 gate | `perf/final/**`, `perf/gate.json`, `perf/matched-treatment.json` | perf-coder; CHECK verdict | Always |

Тексты требований/предикатов строк E246-01..15 не изменены; заменены только пути артефактов rv1→rv2.
Unaffected obligations: E01,03–07,10–12,14–15. E02,08–09,13 retain their text but reference rv2 protocol/precision/oracle evidence; no original obligation is removed.
E16–21 add protocol variants under rv2; their row revision is 2 while inherited row definitions remain `/1`.

### Exact command registry

All new `t246_evidence.py` subcommands below are **planned CLI**, not claims that the tool already exists; implement with Python stdlib and fail closed.
`C-BDN` is the driver invocation above; final uses `--phase final`. Both phases enforce the shared total measurement-time budget and immutable attempt ledger.
`C-PROTOCOL`: `python3 eng/perf/t246_evidence.py protocol --root artifacts/pdca/T246/rv2`; verifies list output, hashes, counts, exports and monitoring prerequisites.
`C-QUALIFY`: `python3 eng/perf/t246_evidence.py qualify --root artifacts/pdca/T246/rv2`; writes qualification and immutable numeric thresholds only after three valid baseline runs.
`C-ORACLE`: `python3 eng/perf/t246_evidence.py oracle --root artifacts/pdca/T246/rv2`; validates byte arithmetic, source identities and canonical-only selection.
`C-MODES`: `python3 eng/perf/t246_evidence.py modes --root artifacts/pdca/T246/rv2 --phase baseline`; repeat with `--phase final` for the final block.
`C-GATE`: `python3 eng/perf/t246_evidence.py gate --budgets eng/perf/t246-budgets.json --runs artifacts/pdca/T246/rv2/perf/final --output artifacts/pdca/T246/rv2/perf/gate.json`.
`C-GATE-NEGATIVE`: `python3 eng/perf/t246_evidence.py self-test --output artifacts/pdca/T246/rv2/perf/gate-negative.json`.
`C-CORE`: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~LobDataReaderTests|FullyQualifiedName~RecordsAffectedTests"`; include discovered new tests in the pinned selector inventory.
`C-SQLITE`: `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~ToDataReaderSqliteTests|FullyQualifiedName~ResultReaderTests|FullyQualifiedName~D199ScalarInPlanCacheTests"`.
`C-BOUNDARY`: `dotnet build nextorm.slnx -c Debug`, then `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test nextorm.slnx -c Debug`; both exit 0.
`C-INTEGRATION`: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor`.
`C-COVERAGE`: under the same `DOCKER_HOST`, run `dotnet-coverage collect -s coverage.settings.xml -f cobertura -o artifacts/pdca/T246/rv2/coverage/coverage.cobertura.xml "dotnet test nextorm.slnx -c Debug"`, then `reportgenerator "-reports:artifacts/pdca/T246/rv2/coverage/coverage.cobertura.xml" "-targetdir:artifacts/pdca/T246/rv2/coverage/report" "-reporttypes:Cobertura;TextSummary"`; verify ≥85% line/≥75% branch.
`C-RECON`, `C-CACHED`, `C-MUTATION`: `python3 eng/perf/t246_evidence.py replay --recipe artifacts/pdca/T246/rv2/recon/commands.json --id <ID> --root artifacts/pdca/T246/rv2`, substituting the named ID.
The replay recipe is a prerequisite scout output: exact existing cached-acceptance invocation/contract, measured attribution-probe invocation, and targeted mutation/test/restore invocations; every argv is pinned before execution. No guessed test symbols or replacement acceptance contract.
`C-DOCS`: `python3 eng/perf/update_benchmark_tables.py`, then `python3 eng/perf/update_benchmark_tables.py --check`; capture both exits and review generated output map before permitting writes.
`C-PUBLISH`: `gh issue edit 246 --repo AlexeyShirshov/nextorm --body-file artifacts/pdca/T246/rv2/docs/issue-246.md`, then `gh issue view 246 --repo AlexeyShirshov/nextorm --json number,url,body`; verify body against the artifact.
All command logs record argv, environment, start/end, exit and selected counts; exit 0 alone never substitutes for a required semantic result.

## Run budget

Qualification: exactly three valid complete runs, at most **four complete run executions**, including one invalid-run reserve; no parameter exploration or two-job probe.
Final: **one decisive validation attempt**—one matched R=3 block, hence no more than three final validation attempts—containing three valid runs and one invalid-run reserve; at most **four complete run executions**.
The reserve is additional to the three valid runs, not one of them. No second final block is authorized by rv2.
Total ceiling: **eight complete seven-arm run executions**, including both reserves. Discarded, crashed, timed-out, warning-invalidated or contaminated runs consume their slots.
Expected qualification ≈7.5–9 minutes; final ≈7.5–9 minutes; reserves and preflight bring the expected bounded measurement campaign to approximately **20–30 minutes**.
Hard ceiling: **two hours cumulative measurement-window wall time**, including preflight/list work, lock waiting, monitoring and all attempted runs across both phases; production/test work between phases is outside this measurement budget.
Driver enforces 600 seconds per complete run and the shared 7,200-second ledger; count increases do not enlarge either ceiling or authorize extra attempts.
CHECK re-gather owner: CHECK, routed to scout/coder; budget **2 total** for bounded extraction/validator/report repair from existing evidence, no additional BDN executions.
A third failed CHECK under r=2 escalates; no fourth attempt. Different defects do not reset counters; new wording or a new session does not create r=3.
A valid failing final gate is an implementation failure; any request for another final block returns to PLAN for an explicit evidence-backed budget/action decision, not an automatic rerun.
Any unused budget is permission only for its stated purpose, not for exploratory tuning or selecting favorable runs.

## GAP rules

Terminal rv2 GAP if three qualified runs cannot be obtained within four executions, `MinIterationTime` persists after the authorized InvocationCount increase, precision caps fail, launch evidence is unavailable, or bimodality remains unresolved.
Terminal GAP also applies to an unresolved canonical-oracle contradiction in raw counts/job/dataset identity after the bounded evidence checks, or exhausted invalid-run reserves.
A product ratio >1.00 with a valid oracle is an implementation failure, **not** measurement noise; it remains a failed acceptance criterion and follows the bounded DO/CHECK attempts.
Preserve all raw attempts, rejected-run reasons, monitoring, hashes, protocol lock, original rv1, criteria, manifests and unfinished production work; never label the task done.
On GAP publish the actual outcome and keep unresolved applicable rows `open`; a missing report alone is not a protocol revision or a new implementation iteration.
Reopen requires an explicit approved, evidence-backed bounded revision identifying the failed mechanism, changed actions/argv, new finite budget and pre-production obtainability proof; original ≤1.00 gates and zero empty tolerance remain binding.

## Test strategy

Gate-tool unit tests: `python3 -m unittest discover -s eng/perf/tests -p 'test_t246_evidence.py' -v`; also run `python3 -m unittest discover -s eng/perf/tests -v`.
Negative fixtures: missing/duplicate/NA/rounded data; missing empty/Dapper/launch; job/count/runtime mismatch; DB-SHA/parity mismatch; nonpositive denominator; error/spread overflow; unsupported confidence level; stale hashes.
Mode fixtures: unexplained bimodality rejected; replicated product modes accepted only as evidence, slowest pairing governs; averaging/hiding modes rejected; contaminated-run replacement consumes budget.
Positive fixtures prove exact boundary ≤1.00, immutable threshold derivation, launch extraction, allocation arithmetic and unchanged original gate behavior. No pytest.
Driver fixtures additionally prove the single lock lifetime, eight-execution ceiling, shared two-hour ledger, one authorized InvocationCount increase, fixed IterationCount, and rejection of execution counts substituted for statistical N.
Runtime unit/SQLite matrix: empty/10k; prepared/unprepared; single/multiple/blob columns; reference/value/nullable/default values; sync/async; both token sources and cancellation before/open/read; normal/error/double disposal; raw/LOB/CSV/materialized cache modes.
Every matrix edge is test-covered or an explicit existing guard: unsupported dialect/provider, in-memory and forbidden temp-table sources must retain typed rejection; no silent fallback.
Real integration boundary: SQLite, PostgreSQL, SQL Server; execute and report MySQL/ClickHouse unsupported behavior and existing supported LOB/CSV paths. No provider skipped is counted as passing.
Mutation targets: disposal idempotence/ownership, cancellation propagation, cache mode/shape isolation and sticky shared-cache mutation. Require valid compiled mutants killed by behavioral tests, then restored hashes and clean rerun.
All E01–21 and new runtime branches are **P1**: direct acceptance invariants/execution paths; CHECK cannot downgrade them. Coverage ≥85% line and ≥75% branch is binding even where CI only warns.

```yaml
test_scope:
  projects: [tests/nextorm.core.tests, tests/nextorm.sqlite.tests, tests/nextorm.integration.tests]
  selectors: ["FullyQualifiedName~LobDataReaderTests|FullyQualifiedName~RecordsAffectedTests", "FullyQualifiedName~ToDataReaderSqliteTests|FullyQualifiedName~ResultReaderTests|FullyQualifiedName~D199ScalarInPlanCacheTests"]
  files: [tests/nextorm.core.tests/LobDataReaderTests.cs, tests/nextorm.core.tests/RecordsAffectedTests.cs, tests/nextorm.sqlite.tests/ToDataReaderSqliteTests.cs, tests/nextorm.sqlite.tests/ResultReaderTests.cs, tests/nextorm.sqlite.tests/D199ScalarInPlanCacheTests.cs, tests/nextorm.integration.tests/CommonTestSuite.Lob.cs, tests/nextorm.integration.tests/SqliteSpecificTests.cs, eng/perf/tests/test_t246_evidence.py]
  rebuild: affected
  boundary: "dotnet build nextorm.slnx -c Debug; full solution tests with configured DOCKER_HOST; stdlib perf-tool tests"
  rationale: "Protocol parser plus inherited reader/planner/cache/owner seam; full boundary catches LOB/CSV and provider regressions"
  integration_projects: [tests/nextorm.integration.tests]
  integration_boundary_runs: 1
```

Rebuild affected compiled inputs before selected tests; rebuild the Release harness before qualification/final. No `--no-build` without matching hash evidence.
The integration boundary is one complete real-provider run after final production changes; rerun after any affected correction and log actual provider execution, never accept skips.
The broad solution sweep belongs to the boundary commands, not to the non-broad inner-loop selectors.

## Docs plan

Fix now: `docs/comparisons/benchmarks.md`, `docs/comparisons/feature-benchmarks-2026-10-17.md`, `docs/specs/performance/acceptance-benchmarks.md`, `docs/specs/performance/feature-benchmark-followups-2026-10-17.md`, and issue #246 body.
Show A/B/empty nextorm and comparators, job/counts, Error/StdDev, raw allocation values, qualification status and final PASS/failure/GAP without hiding historical context.
Public docs must not link to `docs/specs/**`; retain reasoning inline. No public API rename or guide renumbering is planned.
Scout the generator's complete output map before execution; unknown RU/README writes are forbidden. Required expansion returns to PLAN with exact paths, never silently broadens this footprint.

## Perf-measurement decision

Required: this is a per-query/per-row reader path (`QueryCommandExtensions.cs:308-397`, `DataContext.cs:469-583`, `QueryPlanner.cs:603-656,820-829`, `LobDataReader.cs:20-398`).
Baseline is qualified rv2 R=3, not historical ShortRun or failed rv1. Reuse `t246_evidence.py`/budgets as the **planned rv1 §Gate interface**, not an existing implementation.
Create two budget rows, 10k and empty, both time/allocation 1.00; require A and B independently for 10k. Do not alter sibling `iteration14_gate.py` or its logical-operations contract.

## Reconnaissance decision

Required bounded scout/spike: Roslyn key/invalidation/terminal-mode audit and measured allocation attribution before production reuse; report positive isolation evidence and unattributed remainder.
Protocol reconnaissance is the fixed qualification block, not open-ended tuning. Observe actual resolved counts, iteration duration, per-launch availability, mode stability and precision.
No separate in-process reconciliation experiment is required; it cannot change the canonical oracle or excuse the measured empty deficit.

## Unit execution mode

Sequential units in the current tree: tooling/preflight → qualification → precision gate → inherited production/tests → final → publication/CHECK.
Shared harness, gate contract, reader/cache seams and host lock overlap; parallel units or worktrees would add integration/measurement risk without isolation benefit.
No commits, push, merge or broad restore/staging. Keep todos current after each completed step and when starting the next.

## Variance / risk register

| Risk / variance | Disposition |
|---|---|
| Fixed samples still cannot attain precision | Qualification before production; terminal GAP within bounded budget |
| Default-runtime product modes remain | Retain, explain, replicate and gate worst pairing |
| Host telemetry unavailable or ambiguous | Record absence; never infer stability; unresolved mode attribution becomes GAP |
| Empty allocations require structural improvement | Preserve target; no pooling by default, no tolerance workaround |
| Preparation reuse could leak mutable state | Positive Roslyn audit and isolation tests before implementation |
| ~275 foreign dirty `docs/**` paths | Snapshot exact starting diff; preserve bytes; no broad stage/restore/generator overwrite |
| Cached/probe/mutation recipes or generator map differ from expectations | Active in-cycle prerequisite; original work remains blocked, not completed or superseded |
| Long measurement duration | Sequential lock, finite counts/timeouts; no retry-budget reset |
| Invocation executions mistaken for independent samples | Gate reads actual BDN N/confidence intervals; projections never authorize production |
| Measurement duration or invocation promotion consumes reserve | Shared finite ledger; no iteration-count increase or retry-budget reset |

Deferred + триггер (всё в milestone `1.0.9-rc3`): command/owner pooling — до доказанного остаточного bottleneck и отдельного PLAN; perf benchmarking неподдерживаемых провайдеров — до появления raw-reader support (typed-error tests сейчас); дополнительные absolute host-specific budgets — до согласования canonical host; owner-approved empty tolerance — только явное будущее решение owner с отдельным PLAN, не способ закрыть текущий провал.

## DO task list

- **D246-02 — fix now:** build stdlib gate/driver/tests; capture baseline tree and harness hashes; scout exact recipes/output map; bootstrap/parity; list/dry validation; execute qualification R=3 within four complete run executions.
- **P2 — precision demonstration:** C-PROTOCOL/C-QUALIFY/C-ORACLE/C-MODES must all pass; persist selected invocation counts, measured thresholds and unchanged-production proof. This is a dependency gate, not production implementation.
- **D246-03 — fix now, blocked on P2 and E01:** resume inherited V2 work at the four production ranges; minimize allocation/preparation overhead while preserving all R03–08, then unit/integration/coverage/mutations/cached acceptance. No production edits before both prerequisites pass.
- **D246-04 — fix now, blocked on D246-03:** freeze Release harness/runtime tree; execute the authorized matched final R=3 block; C-GATE must pass every original/additional predicate. Any subsequent runtime/build-affecting edit invalidates that final block and requires a budget/action decision before further measurement.
- **D246-05 — fix now:** correct public/internal tables and issue; generator check; frozen-tree manifest and independent CHECK. GAP uses truthful GAP publication, not fabricated final evidence.
- Additive prerequisites keep their original D active and `blocked`; no `superseded→replacement` mapping unless production scope actually changes. No DO report alone marks a D done.

## Requirements on predecessor/sibling outputs

Scout supplies Roslyn-backed key/invalidation/shape/terminal-mode findings, actual recipe argv/test discovery, allocation-probe source/hash/results and generator output map before dependent writes.
Collection parent guarantees the exclusive host window and preserves sibling #243/#245 gates; queue ordering must prevent simultaneous benchmark/build/test workloads.
Issue #246 URL is verified by the supplied handoff; refresh body before editing so unrelated issue content survives.
CHECK consumes one frozen manifest, appends evidence only, applies P1 priorities unchanged and owns its two re-gathers.

## Write footprint

Production, only after P2: `src/nextorm.core/Query/QueryCommandExtensions.cs:308-397`; `src/nextorm.core/DataContext/DataContext.cs:469-583`; `src/nextorm.core/DataContext/QueryPlanner.cs:603-656,820-829`; `src/nextorm.core/DataContext/LobDataReader.cs:20-398`.
Tests: the five core/SQLite files in `test_scope`, `tests/nextorm.integration.tests/CommonTestSuite.Lob.cs`, `tests/nextorm.integration.tests/SqliteSpecificTests.cs`.
Harness/tooling: `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkStreaming.cs`; `eng/perf/t246_evidence.py`; `eng/perf/t246-budgets.json`; `eng/perf/tests/test_t246_evidence.py`; `eng/perf/update_benchmark_tables.py`.
Docs: the four exact paths under Docs plan; evidence under `artifacts/pdca/T246/rv2/`.
**Bookkeeping remains writable by coder but outside the frozen-tree footprint:** `docs/specs/status/D246-raw-datareader-2.md`, `docs/specs/status/collection-1.0.9-rc3-open-3.md`, and the strictly limited rv1 pointer below.
`CommandReaderOwner.cs`, `NextormConfig.cs`, seed DB, sibling gates and rv1 artifacts are read-only. Preserve CRLF; normalize only intentionally edited files and inspect the scoped diff.
The **only** permitted change to `docs/specs/status/D246-raw-datareader-1.md` is appending these exact three lines:
```text
## Supersession — 2026-10-17
Approved protocol revision: [D246 rv2](D246-raw-datareader-2.md), r=2 / rv=2.
rv1 remains terminal GAP evidence; no criteria or historical results changed.
```
Collection T246 row: set only its state to `plan_state=ready (rv2)` and its plan link to this status file; preserve unrelated cells/rows. Append exact lane-log line:
```text
- 2026-10-17 | PLAN/T246 | rv2 ready — N=1; r=2; rv=2; n=1; iteration 1/3; bounded measurement-protocol revision; production blocked until precision PASS; both ≤1.00 gates and empty tolerance 0 % unchanged.
```

## Versioned evidence contract — rv2

This contract supersedes `artifacts/pdca/T246/rv1/contract.json` revision 1; retain that file unchanged. Every required row is P1 and unconditional; none permits `na`.
Coder materializes rows from the table with stable requirement/row IDs, definition revision, exact call/result, planned sources/artifacts, owner and observable applicability; aliases resolve through the pinned command registry/recipe.
Freeze manifest at DO→CHECK, bound to tree and rv2; CHECK is append-only, not a second manifest. `met` requires nonempty evidence; unresolved work is `open` with reason.
**Exclusion resolution: option (a).** The three status/collection files are cycle bookkeeping, omitted from `footprint` and excluded consistently from tree binding; their explicit write permissions above remain unchanged.
Use the same exclusions for manifest generation, recomputation and lint so `manifest.tree == recomputed == tree_binding`; excluded bookkeeping still receives scoped diff/CRLF review.

```yaml
task: T246
issue: 246
status_file: docs/specs/status/D246-raw-datareader-2.md
path: artifacts/pdca/T246/rv2/contract.json
base_ref: adcdb0f9
collection_baseline_ref: 01815f3a
contract_rv: 2
supersedes: 1
footprint:
  - src/nextorm.core/Query/QueryCommandExtensions.cs
  - src/nextorm.core/DataContext/DataContext.cs
  - src/nextorm.core/DataContext/QueryPlanner.cs
  - src/nextorm.core/DataContext/LobDataReader.cs
  - tests/nextorm.core.tests/LobDataReaderTests.cs
  - tests/nextorm.core.tests/RecordsAffectedTests.cs
  - tests/nextorm.sqlite.tests/ToDataReaderSqliteTests.cs
  - tests/nextorm.sqlite.tests/ResultReaderTests.cs
  - tests/nextorm.sqlite.tests/D199ScalarInPlanCacheTests.cs
  - tests/nextorm.integration.tests/CommonTestSuite.Lob.cs
  - tests/nextorm.integration.tests/SqliteSpecificTests.cs
  - benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkStreaming.cs
  - eng/perf/t246_evidence.py
  - eng/perf/t246-budgets.json
  - eng/perf/tests/test_t246_evidence.py
  - eng/perf/update_benchmark_tables.py
  - docs/comparisons/benchmarks.md
  - docs/comparisons/feature-benchmarks-2026-10-17.md
  - docs/specs/performance/acceptance-benchmarks.md
  - docs/specs/performance/feature-benchmark-followups-2026-10-17.md
exclusions: [artifacts/**, docs/specs/status/**]
required_rows: [E246-01, E246-02, E246-03, E246-04, E246-05, E246-06, E246-07, E246-08, E246-09, E246-10, E246-11, E246-12, E246-13, E246-14, E246-15, E246-16, E246-17, E246-18, E246-19, E246-20, E246-21]
```

Manifest fields: `task`, `tree`, `contract_rv: 2`, `status_file`, exact `required_rows`, unique `rows[]`; each `met` evidence entry has `{kind,path,command?,exit?,value?}`. No PASS with any applicable row open.
```bash
python3 scripts/validate_inner_loop.py brief artifacts/pdca/T246/rv2/inner-loop-brief.json
python3 scripts/validate_inner_loop.py report artifacts/pdca/T246/rv2/inner-loop-report.json
python3 scripts/pdca_evidence.py generate --repo . --contract artifacts/pdca/T246/rv2/contract.json --output artifacts/pdca/T246/rv2
python3 scripts/validate_inner_loop.py manifest artifacts/pdca/T246/rv2/manifest.json
python3 scripts/pdca_evidence.py lint --repo . --manifest artifacts/pdca/T246/rv2/manifest.json
```
Every validator must exit 0; exit 2 is violation, exit 1 usage failure. Exclusions do not waive evidence existence, foreign-diff protection or the scoped CRLF audit.

## Prerequisites / what the brief did not say

| Gap | Evidence / assumption / blocking dependency |
|---|---|
| Precision of pinned counts is unmeasured | Explicit qualification dependency; actual BDN confidence intervals authorize production, not execution-count projections |
| Reuse key/invalidation and attribution are incomplete | E01 Roslyn audit plus measured probe; block D246-03, do not guess isolation |
| Cached/probe/mutation command recipes are not supplied | Scout extracts existing contract/real symbols and pins exact argv in `recon/commands.json`; replay rejects absent/unverified recipes |
| Full generator RU/README output map unknown | Read-only scout before generator writes; outside-footprint necessity returns to PLAN |
| Per-launch recoverability under LaunchCount=3 unproved | Preflight extraction required; absent statistics trigger bounded-protocol GAP, never pooled substitutes |
| DB bootstrap was missing in rv1 | Explicit committed-seed copy above; full SHA/parity captured, no synthesized dataset |
| Podman may be stopped | Load `.opencode/skills/running-integration-tests/SKILL.md`; start machine and retry socket as instructed, never accept provider skips |
| Foreign docs changes overlap task pages | Capture starting patch/hashes; apply only task hunks; unresolved overlap blocks that write |
| No new concurrency guarantees authorized | Preserve existing guarantees; no pooling or broadened concurrent-use promises |

Confidence is high in the measured rv1 diagnosis and job distinction; attainability, mode explanation and production feasibility remain deliberately unproven until their gates. Missing evidence first requests targeted scout; persistent low confidence routes to `escalate`, trigger 5.

## Gate-1 self-check

Goal, immutable acceptance criteria, concrete tasks, negative cases, risks, unit/integration strategy, variants, coverage, priorities, docs, perf/recon decisions and sequential execution mode are present.
Design checklist: minimal protocol change selected; alternatives compared; no production-first tuning, mutable-resource caching, shared-cache mutation, tolerance widening or undocumented footprint expansion.
The DO brief uses `rebuild: affected` and integer `integration_boundary_runs: 1`; bookkeeping permissions and frozen-tree exclusions no longer overlap.
Coder must persist this status body and materialize the pinned contract before DO; no artifact skeleton is requested by PLAN itself.
Autonomous mode waives only user approval. Qualification prerequisites remain active tasks; production approval is the measured P2 dependency, not an assumption.

## Progress log (seed)

2026-10-17 | P | revision r=2 (rv2) | iteration 1/3 | PLAN ready (bounded measurement-protocol revision) — canonical out-of-process oracle; precision before production; ≤1.00 time/allocation gates and empty tolerance 0 % unchanged.

## Terminal GAP — 2026-10-17 (rv2)

**Verdict:** `TERMINAL_GAP` · **Reason:** `qualification exhausted: 0/3 valid runs from 4 executions` · **Phase:** DO → GAP.
**Counters unchanged:** `N=1` · `r=2` · `rv=2` · `n=1` · iteration `1/3`.
**Production:** no production edit; `src/**`, `tests/**`, `benchmarks/**` untouched. Files added by this task are harness-only (`eng/perf/t246_evidence.py`, `eng/perf/t246-budgets.json`, `eng/perf/tests/test_t246_evidence.py`); no production fix is authorized.
**Disposition artifacts:** `artifacts/pdca/T246/rv2/GAP.md`, `artifacts/pdca/T246/rv2/gap-disposition.json`, `artifacts/pdca/T246/rv2/perf/attempt-ledger.json`, `artifacts/pdca/T246/rv2/perf/baseline/TERMINAL_GAP.json`.

### Qualification outcome

4 executions, all protocol-invalid with `invalid-report:10k:log-json-launch-mean-mismatch:A_Nextorm_Prepared_ToDataReader:1`; each attempt ran the 10k stratum only (4/7 arms: `A`, `B`, `Dapper`, `Linq2Db`), process exit `0`, durations 99.661 / 75.537 / 76.066 / 77.393 s; shared budget used 409.64856605099885 s of 7200 s. **The empty stratum never started** — no empty-arm conclusion is available for rv2. Qualification artifact `status: REJECT` (valid-run-count `0!=3`).

### Harness defects found (all fixed; checks preserved/strengthened)

1. **Per-op vs iteration-total normalisation.** BDN log `WorkloadResult` reports the iteration total in ns while JSON launch means are ns/op; the comparison lacked normalisation, so every attempt was falsely rejected as a log/JSON mean mismatch. Fixed by dividing each logged total by its logged operation count before comparing, keeping the strict `relative_diff <= 1e-9` check.
2. **Literal `Job.Default` id vs resolved job.** BDN resolves the configured default to `Job-NREBDM(InvocationCount=64, IterationCount=10, LaunchCount=3, UnrollFactor=1, WarmupCount=5)` with `Toolchain=Default` and out-of-process execution. Validation now checks resolved job semantics plus exact counts instead of the literal id, and the treatment identity string is aligned; rejection of in-process/non-Default jobs and count mismatches is retained.
3. **Idle `VBCSCompiler` misclassified as foreign activity.** `ps` `%CPU` is a lifetime average (34–40 % lifetime, 0.00 % instantaneous), so an idle shared build server could reject a clean window. Build-server exclusion (`VBCSCompiler`, `/nodemode:N`) retained, active compiler work still detected via `csc`, `testhost`, `dotnet build|test|publish`, `dotnet-coverage` and BDN.

Defect #1 was found only after all 4 attempt slots of the phase had already been consumed; the corrected parser could not be exercised on a fresh execution inside rv2. Harness self-checks pass: 46/46 unittest OK (exit 0), gate negative fixtures `PASS`.

### Measured 10k evidence (protocol-invalid: evidence only, not a gate verdict)

Source: `/tmp/opencode/T246/rv2/verify/run-numbers.txt`; BDN artifacts preserved under `artifacts/pdca/T246/rv2/perf/baseline/run-{1,2,3,4}/10k/**`.
Worst `e = Error/Mean`: A **0.05161** (r1/L3), B **0.06569** (r2/L3), Dapper **0.02814** (r1/L1), Linq2Db **0.04131** (r1/L1).
Breaches above the 2.7 % cap: **17 of 48 arm×launch cells** — A 3/12, B 9/12, Dapper 2/12, Linq2Db 3/12 (the earlier 13/48 figure is arithmetically impossible; the artifact count is authoritative).
Max `time_upper_ratio` **1.282672** (run 1, arm B, `nL1-cL3`). Allocation: A `962840/960976 = 1.001940`, B `965432/960976 = 1.004637` — both above the `≤1.00` gate. Empty arm: **no data**.
Caps in force: `Error/Mean ≤ 2.7 %`, spread `≤ 4 %`, `time_upper_ratio ≤ 1.00`, `allocation_ratio ≤ 1.00`, empty tolerance **0 %**.
Canonical out-of-process empty allocation is **7.86–7.94×**, not the historical in-process `ShortRun` **2.31×** (regime-incomparable, no conversion factor). rv2 produced no qualified final result.

### Contract status

No contract row is claimed `met` beyond the preserved preflight/tooling checks. `open` with reason `terminal GAP — qualification exhausted`: R246-01, R246-02, R246-09, R246-10, R246-13, R246-14, R246-15, R246-16, R246-17, R246-19; R246-03..R246-08 and R246-11 were never reached; R246-18 is only partially addressed by the publication correction. Protocol-invalid launches never support a `met` row.

### Required protocol change for any future approved revision

- **(a)** normalise BDN log totals by the logged operation count before comparing with JSON per-op ns, and regression-test it;
- **(b)** validate resolved job semantics (`Toolchain=Default`, out-of-process) plus exact counts instead of the literal `Job.Default` id, and align the treatment identity string;
- **(c)** keep the build-server exclusion + active-csc detection and regression-test both;
- **(d)** a future revision must be explicitly owner-approved with a precision-spike-justified `IterationCount > 10`, a fresh finite budget, complete seven-arm requalification (the empty stratum must actually execute) and a new freeze before production;
- **(e)** no production change is authorized; the `≤1.00` gates, the 2.7 % / 4 % precision caps and the 0 % empty tolerance stay binding.

Issue **#246 stays open**; the lane continues **T244 → T245 → T247**. rv1 remains terminal evidence, unchanged.

2026-10-17 | DO | revision r=2 (rv2) | iteration 1/3 | terminal GAP — 4 executions, 0/3 valid runs; harness report-precision defect consumed the attempt ceiling; empty stratum never started | artifacts/pdca/T246/rv2/GAP.md
