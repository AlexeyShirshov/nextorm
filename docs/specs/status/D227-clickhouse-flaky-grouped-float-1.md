# T227 (#227) — D227-clickhouse-flaky-grouped-float, cycle N=1

## Identity and state

- task_id: T227; collection_id: `1.0.9-todo`; group: `G1`; issue: #227
- title: ClickHouse flaky grouped float — seed-order / isolated-run parity fix
- selected_variant: pdca-dotnet
- base: branch `1.0.9` @ `58025147`; collection HEAD `d242778f`; milestone `1.0.9`
- cycle: N=1
- plan_revision: r=1
- attempt: n=1/3
- evidence_contract: rv=1
- plan_state: ready (готовность плана, не завершённость реализации/evidence)
- mode: autonomous collection own-PLAN; single group `G1` → текущий worktree/ветка `1.0.9`, без веток/worktree/merge/push
- permitted auto-commit: только этот статус-файл, префикс `#227`; push никогда
- phase: **PLAN complete — collection handoff** (DO не стартовал и не авторизуется этим файлом)
- status_file: `docs/specs/status/D227-clickhouse-flaky-grouped-float-1.md`

## Task

Устранить flaky-падение ClickHouse-теста групповой проекции float: `AssertGroupedParity()` исполняет портируемый oracle до первого обращения к `_sut`, из-за чего oracle может видеть пустую/отсутствующую Memory-таблицу, а native-запрос — уже засеянные строки. Исправить порядок инициализации так, чтобы оба запроса наблюдали засеянное состояние, включая изолированные прогоны, без изменения запросов, ассертов и продакшн-кода.

## Cycle goal

Оба запроса (портируемый oracle и native) наблюдают одно и то же засеянное fixture-состояние — в том числе при изолированном исполнении метода; изолированный прогон метода 16 раз подряд даёт 16/16 green, весь класс и весь ClickHouse-набор остаются green, без skip/retry/ослабления ассертов и без продакшн/driver/storage-изменений.

## Current state

- phase: DO in progress — D227-00…D227-03 executed: scope/runtime gate + inner red/green evidence + boundary (solution build and one all-ClickHouse sweep) green + provenance/manifest/report frozen (rv=1); CHECK227 pending.
- `n=1/3`; активный `P:` = этот PLAN r=1; активные `D:` = `D227-00` … `D227-03` (все `pending`), плюс независимая верификация `CHECK227`.
- DO начат: scope-brief/runtime gate пройдены, правка `AssertGroupedParity` применена, inner-evidence собран; исходные `D227-*` остаются активными (не `done`) до CHECK, отчёт исполнителя не переводит их в `done`.
- Препятствий/blocker-отчётов нет; первичный PLAN, содержательных ревизий и supersession нет.
- Code 216 (`QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING`) остаётся `deferred + trigger` в milestone `1.0.9`; в T227 не исследуется и не обходится.

## Durable state

- Current cycle N: 1
- Plan revision r: 1
- Attempt n: 1/3
- Evidence contract rv: 1
- plan_state: ready
- defect_key registry: (пусто)
- Defect history: none yet — дефектов не наблюдалось; ни одна попытка/ревизия не сбрасывалась.

## Acceptance criteria

Env: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`; executable `./tests/nextorm.integration.tests/bin/linux/Debug/net10.0/nextorm.integration.tests`.

| ID | Критерий и проверка | Негативный случай |
|---|---|---|
| T227-REQ-01 | Scope brief проходит до правок репозитория; runtime-предусловия доступны. | Ненулевой exit brief либо недоступный runtime блокирует. |
| T227-REQ-02 | Инициализация `_sut` предшествует oracle; запросы/датасеты/ассерты не изменены; нет out-of-scope правок. | Изменён запрос/ассерт/датасет либо затронут файл вне footprint. |
| T227-REQ-03 | Историческое red-доказательство изолированного прогона сохранено: 16/16 failures, exit 1. | Отсутствующий/несогласованный baseline = evidence GAP. |
| T227-REQ-04 | 16 отдельных последовательных изолированных исполнений: каждое selected=1 passed=1 failed=0 skipped=0 exit 0. | Любой fail/skip — провал; поздний успех не заменяет наблюдение. |
| T227-REQ-05 | Весь класс: selected=35 passed=35 failed=0 skipped=0 exit 0. | Любой fail/skip/меньший count. |
| T227-REQ-06 | `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings, 0 errors (и affected build). | Warning/error/ненулевой exit. |
| T227-REQ-07 | Все ClickHouse-классы: selected=265 passed=265 failed=0 skipped=0 exit 0. | Любой fail/skip/меньший count. |
| T227-REQ-08 | Валидные scope/report/manifest, замороженная source/artifact provenance, каждая применимая строка удовлетворена. | Открытая строка/stale binary/невалидный валидатор/несвязанное evidence препятствует PASS. |
| T227-REQ-09 | Статус фиксирует ограниченный design, полные variant dispositions, неизменённый Code 216 deferral. | Неполные секции либо изменённый Code 216-статус. |

Точные argv:

```
["./tests/nextorm.integration.tests/bin/linux/Debug/net10.0/nextorm.integration.tests","-method","NextORM.Integration.Tests.ClickHouseExtremeRowNativeSpecificTests.FloatNativeParity_GroupedSingleFloat64_MinAndMax_AcrossDatasets","-noColor"]
["./tests/nextorm.integration.tests/bin/linux/Debug/net10.0/nextorm.integration.tests","-class","NextORM.Integration.Tests.ClickHouseExtremeRowNativeSpecificTests","-noColor"]
["./tests/nextorm.integration.tests/bin/linux/Debug/net10.0/nextorm.integration.tests","-class","NextORM.Integration.Tests.ClickHouse*","-noColor"]
```

16 green-прогонов — независимые наблюдения, не retry. GAP-обработка: если решающий критерий становится недостижим, записать точный GAP и withheld PASS; никогда не заменять container-backed исполнение no-runtime прогоном, не освобождать ассерт и не заявлять недоступное evidence.

## Design decisions / minimal solution

**Root cause:** `AssertGroupedParity()` исполняет портируемый oracle до первого обращения к `_sut`: oracle на `tests/nextorm.integration.tests/ClickHouseExtremeRowNativeSpecificTests.cs:629`; первое обращение к native-context ~`:635`; это обращение инициализирует репозиторий через `tests/nextorm.integration.tests/ProviderTestSuite.cs:24-31`, включая `ClickHouseTestProvider.EnsureSeeded():66-89`. Oracle может видеть отсутствующую/пустую Memory-таблицу, а native-запрос — засеянные строки; container reuse (`ClickHouseContainer.cs:87-96`) делает это наблюдаемым после restart. Red baseline: 16 изолированных падений; прогоны класса/all-ClickHouse проходят, потому что sibling засеивает первым.

**Goal:** оба запроса наблюдают засеянное fixture-состояние, включая изолированные прогоны.

**Non-negotiable:** сохранить оба запроса и все ассерты на `:644-649`; никакого skip/retry/ослабленного ассерта/продакшн/driver/storage-изменения.

**Minimum change:** эагерно обратиться к `_sut` в начале `AssertGroupedParity()`, до построения/исполнения портируемого oracle; запросы/ассерты не трогать.

| Alternative | Benefit | Risk | Decision |
|---|---|---|---|
| Initialize `_sut` before oracle | existing fixture lifecycle, smallest footprint | verify statement order | Selected |
| Seed explicitly in helper | seed before oracle | duplicates lifecycle knowledge, couples to provider internals | Reject |
| Change persistence/reuse/retry | hides symptom | broader scope, masks defect | Reject |

Fix now: порядок инициализации в `ClickHouseExtremeRowNativeSpecificTests.cs:625-650`. Deferred: Code 216 и спекулятивные fixture-lifecycle изменения.

## Provider contract

Область — только ClickHouse integration runtime; движок/провайдер/driver не меняются.

- Fixture seeding: `ClickHouseTestProvider.EnsureSeeded():66-89`, вызывается лениво при первом обращении к context через `ProviderTestSuite.cs:24-31`.
- Container reuse: `ClickHouseContainer.cs:87-96` — переиспользованный сервер сохраняет ранее засеянные строки между прогонами.
- Native context: `_sut` инициализирует репозиторий/таблицу; портируемый oracle читает ту же Memory-таблицу, поэтому обязан идти после инициализации.
- Прочие провайдеры (PG/SQL Server/MySQL/ClickHouse-прочее/SQLite) не затронуты; их контракт и прогоны сохраняются неизменными.
- Code 216 (`QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING`) — `deferred + trigger`, milestone `1.0.9`; без driver-upgrade/query-ID/production-обхода в T227.

## What the statement did not say

| Пробел | Закрытие |
|---|---|
| Что eager context access действительно засеивает таблицу? | Установлено через `ProviderTestSuite.cs:24-31`, `ClickHouseTestProvider.cs:66-89`; правка только меняет порядок. |
| Нужен ли server restart/удаление таблицы? | Нет; минимальная правка порядка обходится без них. |
| Доказывает ли проходящий default suite порядок использования? | Нет; успех с sibling-засевом не доказывает изолированный случай — измеряем 16 изолированных прогонов. |
| Как заморозить identity без коммита реализации? | `HEAD` SHA + source patch + hash + assembly hash + build provenance; поле `tree` = фактический frozen HEAD SHA, не подразумевает коммит изменения. |
| Допустим ли no-runtime exit 0? | Нет; skipped provider не принимается как pass. |
| Измеряется ли coverage заново? | Нет; пороги line 85% / branch 75% не меняются, новое измерение не заявляется. |
| Что авторизует autocommit? | Только коммит статус-файла с префиксом `#227`. |

## DO units/streams

| Unit | state | Criteria covered | Dependencies |
|---|---|---|---|
| D227-00 gate prep | pending | T227-REQ-01, T227-REQ-09 | create/validate scope; persist plan+identity; record design checklist → `/tmp/scope.json`; T227 status. |
| D227-01 correction | pending | T227-REQ-02 | init `_sut` before oracle; retain queries/assertions; CRLF → the one test file. |
| D227-02 focused evidence | pending | T227-REQ-03, T227-REQ-04, T227-REQ-05, T227-REQ-06 | preserve red baseline; affected build; 16 isolated runs; whole-class run; review bounded diff → T227 evidence dir + allowed scratch. |
| D227-03 boundary/freeze | pending | T227-REQ-06, T227-REQ-07, T227-REQ-08, T227-REQ-09 | solution build; one all-ClickHouse run; freeze provenance; write+validate manifest/report → T227 evidence dir, scratch, status progress. |
| CHECK227 independent verification | pending | T227-REQ-01…T227-REQ-09 | review every P1 row, applicability, logs, identity, validators; bounded re-gather if needed → append-only evidence + status progress. |

Потоки: sequential, один worktree, ветка `1.0.9`; единственная группа; без worktree/branch/merge/parallel writer. Предварительный отчёт «implementation complete» не закрывает `D227-01`. Autocommit статуса: только статус-файл, сообщение начинается с `#227`; никогда `git add -A`; никогда push.

## Variant matrix

| Ряд | Статус | Detail |
|---|---|---|
| Datasets finite/mixednan/nanfirst/nanlast/allnan/inf/zeros → Test (isolated method, Min+Max) | test | selected=1 покрывает 7 datasets × 2 extrema; не отчитываться как 14. |
| Grouped `F64` value projection | test | — |
| Already-seeded table | test | — |
| Absent/empty table before helper entry | guard | Unconditional init before either query; no destructive reset. |
| Isolated execution | test | 16 processes. |
| Whole-class | test | 35. |
| Full ClickHouse suite | test | 265. |
| Full multi-provider suite | deferred+trigger | Shared-fixture change or cross-provider effect. |
| Reuse enabled | test+guard | — |
| Reuse disabled | guard | Order not reuse-dependent. |
| Sequential isolated processes | test | — |
| Existing collection scheduling | test | — |
| Forced alternate parallel/sequential scheduling | deferred+trigger | — |
| Missing `DOCKER_HOST`/skipped provider | guard | Reject as evidence. |
| Null/default/reference projections or other numeric types | deferred+trigger | — |
| Other providers/feature flags/driver config | deferred+trigger | — |
| Code 216 | deferred+trigger | — |

## Test strategy

Scope brief `/tmp/scope.json`: projects `["tests/nextorm.integration.tests"]`; selectors = exact isolated method + exact whole-class; files = the test file + T227 status; rebuild `affected`; boundary = one DO→CHECK sweep (solution build + all-ClickHouse run); rationale = test-only fixture ordering, isolated regression + class containment. Validate BEFORE edits: `python3 scripts/validate_inner_loop.py brief /tmp/scope.json` (exit 0; exit 2 violation / exit 1 usage).

Sequence:

1. Load `.opencode/skills/running-integration-tests/SKILL.md`; check Podman socket (start machine if needed via the skill).
2. Apply the one-helper correction.
3. `dotnet build tests/nextorm.integration.tests -c Debug` (exit 0, 0 warnings).
4. 16 isolated runs + whole-class run.
5. Boundary `dotnet build nextorm.slnx -c Debug` then all-ClickHouse argv.
6. Write `/tmp/evidence.json` (exact argv, phase, exit code, selected count; scope; manifest-validator exit code).
7. `python3 scripts/validate_inner_loop.py report /tmp/evidence.json` (exit 0).

Inner executions `phase=inner`; all-ClickHouse `phase=boundary`. No whole-project/solution test run in the inner loop. Coverage thresholds unchanged (line 85%, branch 75%); do not claim measured. All contract rows T227-E01..E09 are P1.

The 16 green runs are independent observations, not retries.

## Docs plan

Touch only the T227 internal status file. Do NOT touch public EN/RU docs, readme, API docs, provider roadmap, other status files, or DocFX output. Record #227, milestone 1.0.9, baseline/evidence paths. Do not invent an issue URL.

## Perf measurement

No benchmark. One-time test-helper initialization-order change; no production per-row work; no SQL change; 16-run series measures reproducibility not throughput. Measure, never assert.

## Reconnaissance

No spike/recon. Diagnosis, lifecycle path, negative observation, red baseline and selectors are established; reading the integration skill + runtime check are execution prerequisites, not exploration.

## Unit mode

Sequential, one worktree, branch `1.0.9`; single group; no worktree/branch/merge/parallel writer. Push — никогда.

## Deferred + trigger

| Объём | Триггер |
|---|---|
| Code 216 (`QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING`) | Recurrence in a normal integration run, or evidence of explicit/shared driver query-ID configuration. |
| Full multi-provider suite / shared-fixture change | Shared-fixture change or cross-provider effect. |
| Forced alternate parallel/sequential scheduling | Введение принудительного планирования. |
| Null/default/reference projections or other numeric types | Появление такого варианта. |
| Other providers/feature flags/driver config | Изменение конфигурации провайдеров/флагов/driver. |

Code 216 остаётся `deferred + trigger` в milestone `1.0.9`; failure Code 216 всё равно сообщается честно и никогда не превращается в pass.

## Versioned evidence contract — rv=1

Task slug: `D227-clickhouse-flaky-grouped-float`. Root A = `artifacts/pdca/D227-clickhouse-flaky-grouped-float/rv1`. Frozen manifest: `artifacts/pdca/D227-clickhouse-flaky-grouped-float/rv1/manifest.json`. Каждая строка contract revision `rv=1`, applicability predicate — always applicable (все «always applicable»), ни одна строка не optional/`na`.

| requirement_id | row_id | rv | required check/scenario | evidence kind/source | exact command/invocation | required exit/result + log | artifacts/locations | owner stream | applicability predicate |
|---|---|---|---|---|---|---|---|---|---|
| T227-REQ-01 | T227-E01 | 1 | pre-edit scope gate; runtime probe | scope brief + Podman socket probe + skill loading | `python3 scripts/validate_inner_loop.py brief /tmp/scope.json` and `test -S /mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock` | both exit 0; brief precedes edits; skill loading + effective env recorded | A/scope.json, A/brief-validator.log, A/runtime.log | D227-00 (runtime part D227-02) | always applicable |
| T227-REQ-02 | T227-E02 | 1 | changed-files/structural review: init before oracle; queries/datasets/assertions unchanged | git diff + structural review | `git diff --check`; `git diff --name-only d242778f`; `git diff d242778f -- tests/nextorm.integration.tests/ClickHouseExtremeRowNativeSpecificTests.cs` | exit 0; review confirms init before oracle; queries/datasets/assertions unchanged; only allowed files changed; no coverage/config/production changes; CRLF preserved | A/change.patch, A/changed-files.txt, A/diff-check.log, A/structural-review.md | D227-01, checked by CHECK227 | always applicable |
| T227-REQ-03 | T227-E03 | 1 | historical isolated red baseline | captured logs + sha256 | `cat /tmp/nextorm-t227/summary.tsv`; `sha256sum /tmp/nextorm-t227/summary.tsv /tmp/nextorm-t227/iter{00..15}.log` | exit 0; records 16 isolated failures with historical exit 1 + diagnosed mismatch/unknown-table | A/red/, A/red-review.md, A/red-sha256.txt | D227-02 | always applicable |
| T227-REQ-04 | T227-E04 | 1 | 16 sequential isolated runs | test runner logs | exact isolated argv ×16 sequential separate processes | each exit 0, selected=1 passed=1 failed=0 skipped=0; preserve all outcomes | A/isolated/iter00.log…iter15.log, A/isolated/summary.tsv | D227-02 | always applicable |
| T227-REQ-05 | T227-E05 | 1 | whole-class run | test runner log | exact whole-class argv | exit 0, selected=35 passed=35 failed=0 skipped=0 | A/class.log + exit record | D227-02 | always applicable |
| T227-REQ-06 | T227-E06 | 1 | affected + solution build | build logs | `dotnet build tests/nextorm.integration.tests -c Debug` then boundary `dotnet build nextorm.slnx -c Debug` | both exit 0, 0 warnings, 0 errors | A/build-affected.log, A/build-solution.log, exit records | D227-02/D227-03 | always applicable |
| T227-REQ-07 | T227-E07 | 1 | all-ClickHouse boundary run | test runner log | exact all-ClickHouse argv once at the boundary | exit 0, selected=265 passed=265 failed=0 skipped=0 | A/all-clickhouse.log + exit record | D227-03 | always applicable |
| T227-REQ-08 | T227-E08 | 1 | provenance + manifest/report validation | git/hashes + validators | `git rev-parse HEAD`; `git diff --binary HEAD -- tests/nextorm.integration.tests/ClickHouseExtremeRowNativeSpecificTests.cs`; `sha256sum` of test file + apphost + dll; `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D227-clickhouse-flaky-grouped-float/rv1/manifest.json`; `... report /tmp/evidence.json` | all exit 0; correct phases/counts/argv; provenance agrees; every required row present, none open | A/frozen-head.txt, A/frozen-source.patch, A/source-artifact-sha256.txt, A/evidence.json, A/manifest.json, A/manifest-validator.log, A/report-validator.log, frozen status snapshot | D227-03; CHECK227 validates | always applicable |
| T227-REQ-09 | T227-E09 | 1 | status completeness + variant/deferral review | git diff + review | `git diff d242778f -- docs/specs/status/D227-clickhouse-flaky-grouped-float-1.md`; compare variant dispositions/deferred triggers with §§1-4,11 | exit 0; review confirms complete sections, fixed r/rv, all variants closed, unchanged Code 216 deferral, correct milestone, no public-doc/sibling changes | A/status-review.md, A/status.diff, status file | D227-00/D227-03; CHECK227 validates | always applicable |

Manifest: `required_rows` = [T227-E01..T227-E09]; fields `task`=D227-clickhouse-flaky-grouped-float, `tree`=actual frozen HEAD SHA, `contract_rv`=1, `status_file`, rows[] with id/status met|open|na and evidence {kind,path,command?,exit?,value?} / reason / predicate. No PASS while any required row open. Uncommitted-source identity: `tree` = actual frozen HEAD SHA (must not imply the test change is committed); additionally bind tested source via frozen test patch + source hash + compiled test-assembly hash + successful build logs. No `--no-build` planned.

**CHECK re-gather budget:** owner CHECK227, at most two targeted re-gather actions for rv=1 (read-only recovery first; a narrowly selected isolated/class run may supply genuinely missing evidence against the unchanged frozen source — not a retry of a verified failure; do not repeat the boundary sweep). Missing reporting alone ≠ product FAIL/DO iteration/contract revision. Revision policy: rv=1 initial; a justified new required variant → PLAN supersedes with rv=2, retains IDs, adds new ones.

## Write footprint

Repo write footprint: `docs/specs/status/D227-clickhouse-flaky-grouped-float-1.md` and `tests/nextorm.integration.tests/ClickHouseExtremeRowNativeSpecificTests.cs`.

Operational artifacts within evidence roots, primarily A and `/tmp/nextorm-t227/**`; `/tmp/scope.json`, `/tmp/evidence.json` are validator inputs.

Preserve CRLF; keep CPM/runner/provider/production/coverage config unchanged; Roslyn for symbols.

**Не менять:** public EN/RU docs, readme, `docs/api`, provider roadmap, other status files, `docs/specs/status/collection-1.0.9-todo.md`, shared fixtures, siblings/T53.

## Sibling / T53 requirements

No edits to T53, other status files, collection rows, shared fixtures or siblings; do not claim sibling/T53 acceptance from T227; report a shared-fixture prerequisite to PLAN rather than cross-task edits; nothing leaves milestone 1.0.9.

## Risks / known issues

- Runtime availability (skill recovery or precise GAP).
- Stale artifacts (provenance + builds mandatory).
- Selector/count drift (fail or PLAN return, never lower counts).
- Concurrent external mutation of reused server (preserve failure, no retry).
- Code 216 recurrence (report + activate trigger).
- Formatting churn (diff review rejects unrelated edits).

Confidence high in the minimal correction; post-fix success is planned evidence not a fact.

## Open questions

- None — открытые design-вопросы отсутствуют.
- Runtime/container availability — предусловие исполнения, восстанавливается по integration skill; недоступный runtime блокирует, но не является новым design-вопросом.

## Next plan + next todo

- `plan_state=ready`.
- **next allowed step** = coder creates/validates the scope brief, then persists this plan to the status file recording `plan_state: ready`, `selected_variant: pdca-dotnet`, `cycle N=1`, `plan_revision r=1`, `evidence_contract rv=1`. Repository persistence by coder is the final gate-1 step.
- Design checklist passed. **ALL barrier:** goal/acceptance/units/risks/tests/coverage/variants/docs/perf/recon/unit-mode/design-checklist/evidence-contract complete in this decision; DO must not start before the persisted status has `plan_state: ready`.
- Классификация DO → PLAN: отсутствующий tool/socket/fixture — in-cycle предусловие (исходный D активен, `blocked`); истинный внешний blocker — рекомендация `escalate`; недостаток доказательств — точечный scout; стойкая низкая уверенность — `escalate`. Аддитивное предусловие не supersedes; замена scope — явный `superseded → replacement`.

## Changed files

| Path | Kind |
|---|---|
| `docs/specs/status/D227-clickhouse-flaky-grouped-float-1.md` | this status file (persisted) |
| `tests/nextorm.integration.tests/ClickHouseExtremeRowNativeSpecificTests.cs` | planned footprint (pending) |
| `artifacts/pdca/D227-clickhouse-flaky-grouped-float/rv1/**` | planned evidence artifacts (pending) |

## Pointers

- `tests/nextorm.integration.tests/ClickHouseExtremeRowNativeSpecificTests.cs:625-650` (helper; oracle `:629`, init `:635`, queries/assertions `:644-649`)
- `tests/nextorm.integration.tests/ProviderTestSuite.cs:24-31` (repository init)
- `tests/nextorm.integration.tests/Providers/ClickHouseTestProvider.cs:66-89` (`EnsureSeeded`)
- `tests/nextorm.integration.tests/Providers/ClickHouseContainer.cs:87-96` (container reuse)
- `.opencode/skills/running-integration-tests/SKILL.md`
- `scripts/validate_inner_loop.py`
- `artifacts/pdca/D227-clickhouse-flaky-grouped-float/rv1/manifest.json`

## Progress log

2026-10-13T03:37:25Z | PLAN | r1 | n1/3 | PLAN r=1 persisted with plan_state=ready (approved plan transcribed; gate-1 final step) | docs/specs/status/D227-clickhouse-flaky-grouped-float-1.md

2026-10-13T03:37:25Z | PLAN | r1 | n1/3 | rv=1 evidence contract + frozen manifest path artifacts/pdca/D227-clickhouse-flaky-grouped-float/rv1/manifest.json registered; D227-00…D227-03 + CHECK227 active (pending); defect history empty; no supersession | docs/specs/status/D227-clickhouse-flaky-grouped-float-1.md

2026-10-13T04:39:04Z | DO | r1 | n1/3 | applicability re-check: HEAD 9f11957d0c726196d5295de6ab8ad34860cc1efb; test file byte-identical to aea0a9ba before edit (helper AssertGroupedParity :625, oracle :629, first native init :635 unchanged); scope brief /tmp/scope.json validator exit 0; Podman socket present (test -S exit 0) — DO started | A/brief-validator.log, A/runtime.log, A/scope.json

2026-10-13T04:39:04Z | DO | r1 | n1/3 | red baseline preserved: /tmp/nextorm-t227/summary.tsv 15 FAIL rows + iter00..iter15 (16 failing logs, all exit 1, assertion :644); discrepancy flagged — plan expected 16/16 but summary.tsv records 15 rows (extra iter00.log present but not in summary) | A/red/, A/red-review.md, A/red-sha256.txt

2026-10-13T04:39:04Z | DO | r1 | n1/3 | D227-01 applied: eager `_ = _sut.DataProvider;` inserted at ClickHouseExtremeRowNativeSpecificTests.cs:633, before `using var portable = Portable();` at :635, so lazy EnsureSeeded runs before the portable oracle; queries/datasets/group key/assertions unchanged; CRLF preserved; git diff --check exit 0 | A/change.patch, A/changed-files.txt, A/diff-check.log, A/structural-review.md

2026-10-13T04:39:04Z | DO | r1 | n1/3 | affected build `dotnet build tests/nextorm.integration.tests -c Debug` exit 0, 0 Warning(s), 0 Error(s) | A/build-affected.log

2026-10-13T04:39:04Z | DO | r1 | n1/3 | 16 sequential isolated runs (separate processes) each selected=1 passed=1 failed=0 skipped=0 exit 0; whole-class run selected=35 passed=35 failed=0 skipped=0 exit 0 | A/isolated/summary.tsv, A/isolated/iter00..15.log, A/class.log

2026-10-13T04:39:04Z | DO | r1 | n1/3 | /tmp/evidence.json (18 inner executions) report validator exit 0; D227-02 inner evidence complete; D227-03 boundary/CHECK pending, units NOT marked done | A/report-validator.log, A/evidence.json

2026-10-13T04:44:02Z | DO | r1 | n1/3 | boundary solution build `dotnet build nextorm.slnx -c Debug` exit 0, 0 Warning(s), 0 Error(s) | A/build-solution.log

2026-10-13T04:44:02Z | DO | r1 | n1/3 | boundary all-ClickHouse run (single comprehensive sweep) selected=265 passed=265 failed=0 skipped=0 exit 0 | A/all-clickhouse.log

2026-10-13T04:44:02Z | DO | r1 | n1/3 | red baseline reconciled: iter00 is a genuine isolated red run (same :644 assertion, Total:1 Failed:1, separate process), not appended because run.sh truncates summary.tsv and iterates 01..15; genuine isolated FAIL observations = 16 (summary.tsv records 15) | A/red-review.md

2026-10-13T04:44:02Z | DO | r1 | n1/3 | provenance frozen at pre-commit HEAD 9f11957d0c726196d5295de6ab8ad34860cc1efb: frozen-source.patch (non-empty), source+apphost+dll sha256, status.diff; test change still uncommitted at freeze time | A/frozen-head.txt, A/frozen-source.patch, A/source-artifact-sha256.txt, A/status.diff

2026-10-13T04:44:02Z | DO | r1 | n1/3 | /tmp/evidence.json updated with phase=boundary executions (solution build + all-ClickHouse selected=265); report validator exit 0; manifest rv=1 (tree=9f11957d, rows T227-E01..T227-E09 all met) validator exit 0 | A/evidence.json, A/manifest.json, A/report-validator.log, A/manifest-validator.log
