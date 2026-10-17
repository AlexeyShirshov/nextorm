# D246 / T246 — raw DataReader evidence, rv3

- **Task:** D246 / T246.
- **Branch / исходная точка:** `1.0.9-rc3` @ `587397b1`. Это base reference, не fingerprint будущего CHECK tree.
- **Collection:** `docs/specs/status/collection-1.0.9-rc3-open-3.md`; принадлежность существующей группе D246 сохраняется.
- **Variant:** `V2`.
- **Counters:** `N=1`, `r=3`, `rv=3`, `n=1`, iteration `1/3`.
- **plan_state:** ready (rv3).
- **Owner decision:** «обнови пути и продолжи». Разрешение относится к ограниченному rv3-плану ниже; оно не разрешает менять acceptance criteria, caps или production до qualification PASS.
- **Status file:** `docs/specs/status/D246-raw-datareader-3.md`.
- **Evidence root:** `artifacts/pdca/T246/rv3`.
- **CHECK re-gather:** максимум **2** точечных re-gather, владелец — `check`; новые измерения в этот бюджет не входят.
- **Предшественники:** rv1/rv2 сохраняются как исторические evidence. rv2 остаётся terminal GAP.

**P246-rv3:** устранить доказанные harness defects, доказать исправления offline до любого measurement, затем выполнить новый ограниченный protocol cycle с неизменными acceptance criteria.

Это реальная ревизия действий и зависимостей: перед measurement добавлен обязательный Gate H, исправляются три harness defects и вводится read-only `parse-check`. Это не переименование rv2 и не сброс попыток прежнего плана.

**Классификация DO → PLAN:** аддитивное предусловие. Исходный D246 остаётся **active**, до Gate H — `blocked` на harness prerequisite. Его критерии и остаток не заменяются; D246 не становится `superseded` или `done`. Supersession относится к редакции плана/контракта, а не к исходной задаче.

После сохранения этого документа в rv2 добавить **только** следующий append-only блок:

```markdown
## Supersession — 2026-10-17
Approved protocol revision: [D246 rv3](D246-raw-datareader-3.md), r=3 / rv=3.
rv2 remains terminal GAP evidence; no criteria or historical results changed.
```

## Почему rv3

rv2 не доказал host incapacity: qualification завершилась `REJECT`, `valid-run-count:0!=3`, при наличии captured 10k runs и отсутствии frozen protocol lock. Одновременно обнаружены конкретные пробелы harness regression proof:

1. Fixtures используют `Operations=1`, поэтому identity division не проверяет per-operation normalisation.
2. Проверяется literal `Job=Default`, но не корректный resolved out-of-process Default job.
3. Нет regression tests для исключения idle `VBCSCompiler`/build server при сохранении запрета активной конкурирующей работы.

Следовательно, сначала исправляется и доказывается measurement instrument. Повторять measurements на неподтверждённом parser/guard нельзя.

**R100 proof:** актуальность boundary после refactor доказывается отдельно: Roslyn-resolved test scope по текущему tree, фактическое исполнение C-CORE/C-SQLITE/C-INTEGRATION и convention tests, отсутствие product C# changes в rv3. Старые пути, неисполняемые filters и ссылки на pre-refactor implementation не являются evidence.

## Goal / Scope / Out of scope

### Goal

Получить воспроизводимое, tree-bound решение по исходным D246 acceptance criteria после исправления harness, не меняя protocol identity и числовые бюджеты.

### Scope

- Три harness fixes в `eng/perf/t246_evidence.py`.
- Regression tests в `eng/perf/tests/test_t246_evidence.py`.
- Аддитивный read-only `parse-check`.
- Fresh rv3 artifacts, ledger, qualification, oracle, final measurement и gate.
- Однократный .NET boundary sweep.
- Status/evidence records и append-only supersession/lane updates.

### Out of scope

- Product C# changes, новые optimisations, изменения public API.
- Изменения `eng/perf/t246-budgets.json`, включая его `contract_rv: 2`.
- Ослабление caps, confidence requirements, oracle или требований к valid runs.
- Изменение `measure`/`gate` semantics вне трёх согласованных harness fixes.
- Использование rv2 measurements как rv3 baseline или final.
- Public docs, commits, push, merge, worktrees.

### Критерии приёмки

| Требование | PASS | Негативный случай |
|---|---|---|
| Instrument correctness | Gate H полностью green до первого measurement | Identity-only fixture, неподтверждённый effective toolchain, неверная normalisation или contamination classification блокируют measurement |
| Qualification | 3 valid baseline runs в максимум 4 executions; `qualification.json` = PASS; lock создан только `qualify` | 4 executions и меньше 3 valid — terminal GAP |
| Protocol identity | Все прежние числовые значения сохранены; budgets byte-identical; разрешены только перечисленные изменения | Любое изменение cap, seed, job protocol или acceptance tolerance — FAIL |
| Oracle и final | Out-of-process Default oracle; 3 valid final runs в максимум 4 executions; исходный gate PASS | In-process ShortRun, stale qualification или missing lock не принимаются |
| Refactor boundary | Текущие post-refactor tests реально исполнены; integration использует live DBMS | Zero-test filter, provider skip вместо исполнения или stale path не принимаются |
| Evidence completeness | Все binding rows закрыты и оба evidence validators возвращают 0 | Любая applicable `open` row запрещает CHECK PASS |

## Минимальное решение

**Что меняем:** instrument и его доказательства, не product.

**Что непреложно:** исходные D246 criteria, frozen protocol, 3 valid runs на блок, execution/time budgets, qualification-before-production, completeness/tree binding.

**Оптимум:** offline Gate H → один ограниченный fresh cycle → PASS либо terminal GAP без дальнейших повторов.

| Альтернатива | Плюсы | Минусы / риск | Решение |
|---|---|---|---|
| Исправить harness и доказать offline | Устраняет известную причину недоверия до затрат measurements | Нужны regression tests и parser proof | **Выбрано** |
| Сразу повторить rv2 measurements | Минимум подготовительной работы | Повторяет неподтверждённый instrument, расходует бюджет без доказательства | Отклонено |
| Ослабить protocol или оптимизировать product | Может изменить результат | Подменяет исходные критерии и причину неуспеха | Вне scope |

## Contract rv3

`contract_rv=3`. Стабильные ID сохраняются. Новые requirement IDs начинаются с `R246-20`, evidence IDs — с `E246-22`.

### Наследование без изменения обязательств

Ниже **verbatim-by-range** означает нормативное включение соответствующих строк из rv1/rv2 без пересказа, удаления слотов или ослабления predicates. Coder переносит каждую inherited evidence row в rv3 manifest индивидуально. Только **планируемые rv3 output paths** получают замену `rv2` → `rv3`; исторические ссылки и результаты rv2 не переписываются.

| ID / диапазон | Обязательство rv3 |
|---|---|
| `R246-01..R246-19` | Verbatim-by-range из действующего rv2 contract, включая исходные acceptance criteria, protocol invariants и invariant 12 |
| `E246-01..E246-21` | Verbatim-by-range из действующего rv2 contract: те же ID, requirement mappings, проверки, ожидаемые виды evidence, owners, applicability predicates и обязательства; planned execution/output paths направляются в rv3 |
| `R246-20` | Per-operation correctness: Operations > 1, согласование log/JSON, resolved out-of-process Default job |
| `R246-21` | Host guard correctness: idle compiler/build server не является contamination; активные конкурирующие workloads остаются запрещены |
| `R246-22` | Gate H до measurements, ограниченный fresh cycle, актуальный post-refactor boundary и отсутствие обхода completeness |

**`required_rows`: все `E246-01` … `E246-25`, ровно по одному разу.** Диапазон в этом документе не является допустимой заменой отдельных объектов `rows[]` manifest.

Если при переносе inherited строки невозможно восстановить её полный исходный слот, строка остаётся `open`; это препятствие к DO measurement, а не основание ослабить контракт.

### Новые evidence rows

Все перечисленные output sources пока **планируемые**. Для каждой строки сохраняются command log, exit status и tree-bound receipt.

| Row / requirement / rv | Проверка и точный вызов | Ожидаемый результат и негативный случай | Evidence и артефакты | Owner / наблюдаемый predicate |
|---|---|---|---|---|
| **E246-22** / R246-20, R246-21 / **3** | **C-H-UNIT**, ниже | rc=0; 46 existing + ≥3 new tests, все green. Operations>1 действительно влияет на normalisation; resolved Default job принимается; InProcess и активная contamination отклоняются | Test log `harness/unit-tests.log`; machine-readable `harness/regression-proof.json` с counts, фактическими test identities и покрытыми ветвями | `harness`; applicable всегда при rv3 |
| **E246-23** / R246-20, R246-21 / **3** | **C-H-NEGATIVE**, ниже | rc=0; ≥19 cases, все ожидаемые результаты совпали. Включены operation-count mismatch и отсутствие допустимого effective Default toolchain | `harness/gate-negative.json`, command log и receipt | `harness`; applicable всегда |
| **E246-24** / R246-20, R246-22 / **3** | **C-H-PARSE**, ниже | rc=0; каждый captured rv2 10k run проверен; `operations=64`, `relative_diff≤1e-9`; unnormalised interpretation отличается примерно в 64 раза. Missing manifest допустим; missing/ambiguous measurement data — FAIL | `harness/parse-check.json`, command log; input inventory/hashes и read-only proof в `harness/parse-check-inputs.json` | `harness`; applicable всегда, входы — существующие rv2 runs |
| **E246-25** / R246-22 / **3** | **C-BUILD**, **C-CORE**, **C-SQLITE**, **C-CONVENTIONS**, **C-INTEGRATION**, **C-INTEGRATION-SKIPS**, ниже | Все rc=0; filters реально исполнили подходящие tests; integration boundary выполнен ровно один раз с требуемыми DB providers; skip-budget PASS; conventions green; product C# diff отсутствует | `boundary/build.log`, `core.log`, `sqlite.log`, `conventions.log`, `integration.log`, `integration-skip-budget.log`, `r100-boundary.json`, `test-scope.json` | `boundary`; applicable всегда в rv3, не зависит от появления C# diff |

**Приоритеты:** все inherited P1 остаются P1. `R246-20..22`, `E246-22..25`, qualification/lock/order/budget/integration/completeness invariants — **P1 по построению**. CHECK не понижает их из-за отсутствия product edits или измерений.

## Gate H — до ANY measurement execution

### Разрешённые harness changes

1. **Normalisation:** `eng/perf/t246_evidence.py:659–680` и соответствующие parser paths.
   - JSON `Statistics.Mean`, confidence statistics и `Memory.BytesAllocatedPerOperation` трактуются в их per-operation units.
   - Raw log duration делится на фактический operation count ровно один раз.
   - Проверка согласования log/JSON не использует двойное деление или identity-only fixture.

2. **Job classification:** распознавание effective out-of-process Default toolchain, а не обязательного literal `Job=Default`.
   - Принимается verified resolved job
     `Job-NREBDM(InvocationCount=64, IterationCount=10, LaunchCount=3, UnrollFactor=1, WarmupCount=5)`.
   - `Toolchain=InProcess` и in-process jobs отклоняются.
   - Неизвестный/недоказанный effective toolchain не считается Default.
   - Negative case «job without Default toolchain» означает отсутствие **допустимого effective toolchain**, не отсутствие слова `Default` в display name.

3. **Host guard:** idle `VBCSCompiler`/build server исключается из contamination.
   - Исключение ограничено доказанно idle server, а не любым процессом с похожим именем.
   - Активные `csc`, `testhost`, `dotnet build|test|publish`, `dotnet-coverage`, BDN по-прежнему отклоняются.
   - Process classification/reason остаётся наблюдаемым в evidence.

### Exact commands

**C-H-UNIT**

```bash
python3 -m unittest discover -s eng/perf/tests -p 'test_t246_evidence.py' -v
```

**C-H-NEGATIVE**

```bash
python3 eng/perf/t246_evidence.py self-test --output artifacts/pdca/T246/rv3/harness/gate-negative.json
```

**C-H-PARSE** — новый additive CLI surface:

```bash
python3 eng/perf/t246_evidence.py parse-check --runs artifacts/pdca/T246/rv2/perf/baseline > artifacts/pdca/T246/rv3/harness/parse-check.json
```

Контракт `parse-check`:

- `--runs` принимает parent с `run-*` либо отдельный run directory; `manifest.json` не требуется.
- Читает только captured logs/results выбранных runs; не читает и не пишет `protocol-lock.json`.
- Не вызывает BDN, qualification, oracle, measure или gate.
- JSON report направляется в stdout; diagnostics — в stderr; rc=0 только при полном PASS.
- Проверяет все четыре captured rv2 10k runs; недостаток или неоднозначность пары log/JSON — FAIL, не silent skip.
- Для каждого run показывает JSON mean в ns/op, log mean из `WorkloadResult` после деления raw ns на Operations, `operations`, `relative_diff` и unnormalised ratio.
- Для различия используется симметричная относительная ошибка относительно максимума абсолютных сравниваемых means; требуется `≤1e-9`.
- Показывает algebraic proof `raw_duration / duration_per_op = 64` и сравнение unnormalised mean с JSON mean: примерно 64×, а не примерно 1×.
- Input inventory/hash до и после совпадают. Output пишется только shell redirection в rv3.

### Что доказывают tests

- Хотя бы один новый regression использует **Operations>1** и различимые raw/per-op значения: удаление требуемого деления либо двойное деление приводит к FAIL.
- Хотя бы один новый regression принимает указанный resolved Default job и отклоняет in-process alternative.
- Хотя бы один новый regression различает idle compiler/build server и перечисленные active competitors.
- Self-test добавляет минимум два cases к прежним 17: log/JSON operation-count mismatch и недопустимый effective toolchain.
- Фактические новые test identities фиксируются после реализации; будущие symbols здесь не выдумываются.

### Repair budget

Не более **двух offline harness-repair iterations**: H1 и, при необходимости, H2. Каждая завершается полным C-H-UNIT → C-H-NEGATIVE → C-H-PARSE.

Если после H2 любой predicate не green:

- rv3 расходует **ZERO measurement executions**;
- фиксируется terminal GAP / `incomplete`;
- production не изменяется;
- третьего harness repair или measurement «для проверки» нет.

Эти две offline iterations не сбрасывают внешние `r/n` и не дают дополнительных CHECK attempts.

## Protocol unchanged

Нормативный источник всех ранее закреплённых числовых caps и protocol fields — rv2 contract и **неизменённые bytes** `eng/perf/t246-budgets.json`. Их значения не переоцениваются и не выводятся из новых результатов.

**Protocol deltas vs rv2: none** — кроме трёх harness fixes, аддитивного read-only `parse-check` и замены planned paths `rv2` → `rv3`. Caps, argv, counts, lock, seed/sha, oracle, acceptance gates и zero-empty tolerance — byte/verbatim inherited.

### Protocol identity

Допускаются только:

- три согласованных harness fixes;
- additive read-only `parse-check`;
- замена planned rv2 output roots на rv3.

`eng/perf/t246-budgets.json` остаётся **byte-identical**, включая **`contract_rv: 2`**. Это поле принадлежит frozen protocol identity input; cycle evidence contract независимо имеет `contract_rv=3`. Изменение поля ради совпадения номеров нарушило бы identity.

До harness edits сохранить исходные bytes budgets в:

`artifacts/pdca/T246/rv3/protocol/t246-budgets.rv2.json`

После edits и перед measurement выполнить:

```bash
cmp -s eng/perf/t246-budgets.json artifacts/pdca/T246/rv3/protocol/t246-budgets.rv2.json
```

Требуется rc=0. SHA-256 обоих файлов и полный набор inherited caps/strata/arms фиксируются в `protocol/identity.json`. Числовые caps, не приведённые во входной сводке, копируются **точно из frozen source**, не подбираются. Невозможность восстановить их идентичность означает GAP до measurement.

### Counts, lock, seed, oracle

- Qualification block: **3 valid runs**, максимум **4 executions**.
- Final block: **3 valid runs**, максимум **4 executions**.
- Внутренние BDN launches не подменяют число независимых runs.
- Resolved job characteristics: `InvocationCount=64`, `IterationCount=10`, `LaunchCount=3`, `UnrollFactor=1`, `WarmupCount=5`; effective toolchain — out-of-process Default.
- `protocol` создаёт `perf/preflight.json`, seed copy и ledger; **не создаёт protocol lock**.
- Baseline `measure` запрещён при наличии `perf/protocol-lock.json`.
- `qualify` всегда пишет `perf/qualification.json`; lock создаёт **только на PASS**, если его ещё нет.
- Конфликтующий lock даёт `protocol-lock-frozen-mismatch`; удалять/переписывать его ради продолжения нельзя.
- Final `measure` запрещён без frozen protocol lock.
- `gate` fail-closed на `protocol-lock-not-frozen`, `qualification-not-pass`, `stale-qualification-treatment-hash`, `qualification-caps-differ-from-lock`, `qualification-run-coverage`.
- Host mutex: ровно `/tmp/opencode/nextorm-bdn-host.lock`, одно `flock -x -w 3600` на весь R=3 block (не на arm). Это mutex, не `protocol-lock.json`.

Seed:

- Committed source: `benchmarks/nextorm.benchmark/data/test.db`.
- Size: **753,664 B**.
- Runtime path: **`/tmp/nextorm-bench/test.db`**, `eng/perf/t246_evidence.py:60`.
- SHA-256: **`32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295`**, `:62`.
- Preflight copy: `artifacts/pdca/T246/rv3/perf/preflight/test.db`.
- Parity: **rows=10000**, **checksum=422955955263575176**.

Oracle:

- Только **out-of-process Default**.
- Reference reconciliation: **7.86–7.94× empty arm**, без введения новой tolerance.
- In-process ShortRun выведен из acceptance; его исторические результаты не удаляются и не используются для PASS.
- Все прочие caps, strata, arms и comparison rules наследуются буквально из rv2.

### Обязательный порядок

**Gate H → boundary sweep → protocol → baseline measure → qualify/freeze → oracle → final measure → gate.**

### Command registry

**C-PROTOCOL**

```bash
python3 eng/perf/t246_evidence.py protocol --root artifacts/pdca/T246/rv3
```

**C-BASELINE**

```bash
python3 eng/perf/t246_evidence.py measure --phase baseline --runs 3 --max-attempts 4 --root artifacts/pdca/T246/rv3 --lock /tmp/opencode/nextorm-bdn-host.lock --lock-wait-seconds 3600 --run-timeout-seconds 600 --total-measurement-budget-seconds 7200
```

**C-QUALIFY**

```bash
python3 eng/perf/t246_evidence.py qualify --root artifacts/pdca/T246/rv3
```

**C-ORACLE**

```bash
python3 eng/perf/t246_evidence.py oracle --root artifacts/pdca/T246/rv3
```

**C-FINAL**

```bash
python3 eng/perf/t246_evidence.py measure --phase final --runs 3 --max-attempts 4 --root artifacts/pdca/T246/rv3 --lock /tmp/opencode/nextorm-bdn-host.lock --lock-wait-seconds 3600 --run-timeout-seconds 600 --total-measurement-budget-seconds 7200
```

**C-GATE**

```bash
python3 eng/perf/t246_evidence.py gate --budgets eng/perf/t246-budgets.json --runs artifacts/pdca/T246/rv3/perf/final --output artifacts/pdca/T246/rv3/perf/gate.json
```

На каждом шаге требуется rc=0 **и** ожидаемый PASS/result artifact. Один rc=0 не заменяет проверку содержания. Отрицательные fixtures считаются успешными только при ожидаемом reject.

## Budget & execution accounting

| Ограничение | Значение |
|---|---:|
| Offline harness-repair iterations | ≤2 |
| Baseline/qualification valid runs | 3 |
| Baseline executions | ≤4 |
| Final valid runs | 3 |
| Final executions | ≤4 |
| Total executed runs | ≤8 |
| Run timeout | 600 s |
| Lock wait parameter | 3600 s |
| Общий measurement wall-clock | ≤7200 s |
| Integration boundary runs | 1 |
| CHECK re-gather | ≤2 |

- Fresh ledger: `artifacts/pdca/T246/rv3/perf/attempt-ledger.json`.
- rv2 attempts/results не переносятся в счётчики rv3 и не становятся valid rv3 runs.
- В каждом блоке есть только **одна reserve execution**. Harness failure, timeout или invalid attempted run расходует execution и тем самым резерв, а не tolerance.
- После четырёх executions при менее чем трёх valid — немедленный terminal GAP.
- Если резерв уже израсходован, новый invalid run не даёт пятого execution.
- Отсчёт общего 7200 s начинается с первого measurement invocation; обе фазы делят один deadline. Параметр `7200` в двух командах не означает два отдельных бюджета.
- Владелец measurement stream контролирует общий deadline, включая ожидания внутри measurement. Новый run не начинается, если его нельзя закончить в оставшемся бюджете; прерывание по deadline фиксируется как failure/invalid execution.
- В ledger сохраняются timestamps, phase, attempt ordinal, launched/failed/valid status, reason, run directory и cumulative counters.
- Запуски offline tests, boundary tests и `parse-check` — не measurement executions. CLI refusal до допуска run не выдаётся за выполненное измерение; каждая реально начатая measurement attempt учтена.
- Счётчики сверяются с run manifests, process/log evidence и ledger; потерянный artifact не даёт бесплатного повтора.

## GAP rule и reopen precondition — invariant 12

**Invariant 12 из rv2 наследуется verbatim и не ослабляется.**

В rv3 terminal GAP обязателен при:

- Gate H не green после разрешённых offline iterations;
- исчерпании execution/time budget;
- qualification без 3 valid runs;
- недоказанной protocol identity, seed parity или допустимости host/job;
- невозможности получить обязательное evidence в пределах разрешённых действий.

Результат: **`incomplete`, не PASS и не "implementation complete"**. Production остаётся без изменений. Применимые незакрытые строки сохраняются `open` с фактическими evidence и причиной; их нельзя превращать в `na` из-за отсутствия отчёта.

Reopen требует явного owner-authorized решения и нового проверяемого факта, устраняющего конкретную причину GAP: например, доказанного исправления instrument или проверенного изменения host condition. Нужны causal evidence, пересмотренные действия/зависимости и новый ограниченный budget. Переименование, новый session/root, отсутствие отчёта или желание повторить те же inputs не являются reopen precondition.

Низкая уверенность не объявляется host incapacity. При стойкой неопределённости после разрешённых проверок оркестратору рекомендуется `escalate` по trigger 5; молчаливого дополнительного measurement нет.

## Test strategy

### Harness

- Python unit tests проверяют parser arithmetic, effective job classification и host process guards.
- `self-test` проверяет fail-closed gate scenarios.
- Captured rv2 runs используются только для read-only parser reconciliation.
- Gate H обязателен даже при отсутствии product changes.

### Boundary sweep — один раз

До запуска resolve test scope через Roslyn по текущему tree. Сохранить фактические definitions/paths и test counts в `boundary/r100-boundary.json`; symbols не искать через text search. Zero-test result не принимается.

**C-BUILD**

```bash
dotnet build nextorm.slnx -c Debug
```

**C-CORE**

```bash
dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~LobDataReaderTests|FullyQualifiedName~RecordsAffectedTests"
```

**C-SQLITE**

```bash
dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~ToDataReaderSqliteTests|FullyQualifiedName~ResultReaderTests|FullyQualifiedName~D199ScalarInPlanCacheTests"
```

**C-CONVENTIONS**

```bash
dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~AssertionStyleTests|FullyQualifiedName~IntegrationSuiteScopingTests"
```

**C-INTEGRATION**

Перед запуском загрузить `.opencode/skills/running-integration-tests/SKILL.md`, проверить socket и выполнить предписанное восстановление Podman, если требуется.

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
```

Полный stdout/stderr сохранить в `/tmp/opencode/integration.log` и скопировать в `boundary/integration.log`, сохранив исходный exit code команды.

**C-INTEGRATION-SKIPS**

```bash
python3 eng/check-integration-skip-budget.py --log /tmp/opencode/integration.log
```

Требуется rc=0, live DB evidence и исполнение обязательных PostgreSQL, SQL Server, MySQL, ClickHouse, а также SQLite coverage suite по текущему harness. Запуск без `DOCKER_HOST` или с provider skips вместо исполнения не PASS. Административная preflight-проверка не считается suite run; начавшийся failed boundary run фиксируется, а не скрывается дополнительным «однократным» запуском.

### Test scope

Следующий block сохранить как `boundary/test-scope.json`:

```json
{
  "task": "T246",
  "contract_rv": 3,
  "production_changes_planned": false,
  "integration_boundary_runs": 1,
  "harness": {
    "unit_command": "C-H-UNIT",
    "minimum_existing_tests": 46,
    "minimum_new_tests": 3,
    "negative_command": "C-H-NEGATIVE",
    "minimum_negative_cases": 19,
    "captured_input_command": "C-H-PARSE",
    "maximum_offline_repair_iterations": 2
  },
  "boundary_commands": [
    "C-BUILD",
    "C-CORE",
    "C-SQLITE",
    "C-CONVENTIONS",
    "C-INTEGRATION",
    "C-INTEGRATION-SKIPS"
  ],
  "integration": {
    "requires_live_database": true,
    "in_memory_provider_allowed": false,
    "required_container_providers": [
      "postgres",
      "sqlserver",
      "mysql",
      "clickhouse"
    ],
    "provider_skip_is_pass": false
  },
  "coverage_thresholds": {
    "solution_line_percent": 85,
    "solution_branch_percent": 75,
    "integration_line_percent": 65,
    "integration_branch_percent": 75
  },
  "new_coverage_campaign_added_by_rv3": false,
  "inherited_coverage_obligations_unchanged": true
}
```

### Variant matrix

| Вариант | Закрытие | Негативный сценарий |
|---|---|---|
| Raw duration, Operations>1 | **Тест:** arithmetic regression + captured runs | Нет деления / двойное деление |
| JSON per-op mean, CI, allocation | **Тест:** units сохраняются | Повторное деление per-op fields |
| Log/JSON operation count | **Тест:** новый self-test | Несовпадение counts |
| Resolved Default job | **Тест:** exact resolved job принимается | Проверка только literal display name |
| InProcess / Toolchain=InProcess | **Guard + тест** | Попытка использовать для acceptance |
| Unknown effective toolchain | **Guard** | Неявное принятие недоказанного Default |
| Idle compiler/build server | **Тест** | False-positive contamination |
| Active csc/testhost/build/test/publish/coverage/BDN | **Тест + guard** | False-negative contamination |
| Missing manifest у captured run | **Тест / parse-check** | Отказ только из-за отсутствия manifest |
| Missing fields, null, zero Operations, malformed input | **Guard + harness regression coverage** | Default/coercion вместо reject |
| Missing/stale/conflicting protocol lock | **Existing self-tests + guard** | Продолжение с недействительным lock |
| 3 valid / reserve / <3 after 4 | **Guard + ledger reconciliation** | Пятый run или уменьшение valid requirement |
| Seed/hash/parity | **Protocol guard** | Другая DB или checksum |
| C# null/default, value/reference, flags | **Inherited tests + C-CORE/C-SQLITE**; product variants не меняются | Новая product ветвь без отдельного контракта |
| Provider differences | **C-INTEGRATION**, real DBMS | Placeholder/in-memory вместо integration |
| Новая product/API/provider ветвь | **Deferred**, trigger: отдельно одобренный scope change | Включение в текущий rv3 без replanning |

### Conventions и coverage

- Если C# test всё же затронут: FluentAssertions для всех value/exception assertions; `Assert.Skip*` — единственное разрешённое исключение.
- Integration tests требуют real DBMS; in-memory остаётся unit-only.
- `AssertionStyleTests` и `IntegrationSuiteScopingTests` обязательны.
- Порог whole-solution: **85% line / 75% branch**.
- Integration gate: **65% line / 75% branch**; известный baseline **73.6% / 61.9%** после исключения `NextORM.Core.InMemory*` остаётся red по branch.
- rv3 не добавляет coverage campaign для Python-only repair и не объявляет известный red зелёным. Все inherited coverage obligations сохраняются; отсутствие нового collection не закрывает такую строку, если она binding.
- Build warnings — errors. CRLF сохраняется. Только post-refactor paths.

## Perf decision

**Perf measurement нужен:** это предмет D246 acceptance, а не факультативный benchmark.

- Instrument: существующий `eng/perf/t246_evidence.py` + BDN.
- Baseline: fresh rv3 qualification block, только после Gate H.
- Final: fresh rv3 final block после qualification freeze и oracle.
- Captured rv2 runs — только parser evidence, не baseline substitution.
- Correctness-critical normalisation location: `eng/perf/t246_evidence.py:659–680`.
- Seed identity anchors: `:60`, `:62`.
- Product per-row path не изменяется; дополнительного product microbenchmark вне frozen protocol нет.

## Reconnaissance decision

**Новый scout/spike для выбора решения не нужен:** причины и ограниченное решение подтверждены targeted reconnaissance.

Нужен исполняемый offline proof — `parse-check`, уже включённый в DO, а не новая разведка. Его наблюдаемый критерий: все captured runs reconciled, Operations=64, relative difference ≤1e-9, unnormalised interpretation ≈64×, отсутствие lock access и input mutation.

DO-time восстановление inherited slots/caps/host-lock и Roslyn test resolution — детерминированные preconditions. Если они не выполняются, применяется GAP/escalation, а не выдумываются значения.

## Docs plan

**Затронуты:**

- этот status file;
- append-only supersession block rv2;
- существующая collection lane;
- planned artifact reports, receipts и manifest;
- CLI/help description additive `parse-check`, если она размещена рядом с harness.

**Не трогаем:** public docs, `docs/ru/**`, README, public API reference. Из public docs не добавляются ссылки на `docs/specs/**`.

Historical rv1/rv2 rows/results не редактируются. Lane сохраняет существующую group identity и pipe convention.

## Unit execution mode

**Последовательно, один harness stream, одно tree, без worktrees.**

Причина: пересекаются harness, regression tests, protocol identity, shared host lock, ledger и tree-bound evidence. Параллельные build/test/coverage/BDN во время measurements запрещены.

| DO unit | Footprint / действие | Решение |
|---|---|---|
| **D246-H** | `eng/perf/t246_evidence.py:659–680`, job/host guards того же файла; `eng/perf/tests/test_t246_evidence.py` | **Fix now:** три fixes, parse-check, Gate H |
| **D246-B** | Текущие tests, boundary artifacts, Roslyn-resolved R100 mapping | **Fix now:** один boundary sweep, без product refactor |
| **D246-M** | `artifacts/pdca/T246/rv3/perf/**` | **Fix now после H/B:** frozen-order bounded measurement |
| **D246-E** | rv3 manifest/status, rv2 append, collection lane | **Fix now:** provenance, validators, CHECK handoff |
| Product optimisation / новый protocol | Product C#, budgets | **Deferred:** только отдельный owner-approved scope и новый план |

D246 остаётся active на всём пути. Пройденный Gate H завершает prerequisite, но не исходную задачу.

## Чего не сказала постановка

| Пробел | Закрытие |
|---|---|
| Полные тексты inherited R/E rows не повторены во входной сводке | Нормативное verbatim inclusion из rv2; materialisation до DO, без потери слотов |
| Все числовые caps/strata не перечислены в сводке | Frozen bytes budgets + inherited protocol; exact copy и identity report, никаких новых значений |
| Конкретный inherited host mutex path | `/tmp/opencode/nextorm-bdn-host.lock`, verbatim inherited, закрепляется в `protocol/identity.json` |
| Будущие test symbols | Не предполагаются; фактические identities/counts фиксируются после реализации |
| R100 current symbol locations | Roslyn resolution по текущему tree и boundary artifacts до measurement |
| Реальная способность host завершить protocol | Не предполагается; ограниченные runs и fail-closed GAP |
| Будущий tree fingerprint | Вычисляется при freeze; branch SHA не используется вместо него |

## Risks / stop conditions

- **Parser proof не сходится:** не подбирать tolerance; Gate H FAIL.
- **Слишком широкое исключение compiler processes:** active-workload negatives обязательны.
- **Job name принят вместо effective toolchain:** FAIL.
- **Подмена protocol identity при “обновлении rv”:** budgets остаётся byte-identical с `contract_rv:2`.
- **Background activity или нестабильный host:** invalid execution расходует резерв.
- **Stale lock/qualification/treatment hash:** stop, не удалять evidence ради продолжения.
- **Boundary failure:** записать фактический результат; не менять product и не расходовать perf runs для диагностики.
- **Budget exceeded:** terminal GAP, без дополнительного run.
- **Нет отчёта:** сначала разрешённый точечный re-gather; не новая DO iteration автоматически.
- **Scope/security/authority blocker:** рекомендация оркестратору вызвать `escalate`.
- **Стойкая низкая уверенность:** `escalate`, trigger 5, даже без доказанной внешней причины.

**Уверенность:** высокая в необходимости instrument-first repair и frozen-order execution; способность host дать qualification PASS не доказана и не предполагается.

## Manifest & evidence

Manifest:

`artifacts/pdca/T246/rv3/manifest.json`

Обязательная schema:

- `task`: `T246`;
- `tree`: вычисленный evidence-tree fingerprint;
- `contract_rv`: `3`;
- `status_file`: `docs/specs/status/D246-raw-datareader-3.md`;
- `required_rows`: отдельные ID `E246-01` … `E246-25`;
- `rows[]`: отдельная запись на каждый ID с `id`, `status` (`met|open|na`), `evidence[]`, `reason`, `predicate`;
- каждый evidence item: `kind`, `path`, и применимые `command`, `exit`, `value`;
- receipts и `tree_binding` — по фактической версии evidence tooling.

Для каждой строки полный requirement mapping, scenario, expected evidence kinds/sources, exact command, result/log expectations, artifacts, owner, observable applicability predicate и `rv` берутся из этого контракта либо inherited verbatim row.

Правила:

1. До DO coder сохраняет полный контракт и inherited mappings.
2. Planned sources не объявляются существующими.
3. DO → CHECK: manifest и tree binding frozen.
4. CHECK — append-only; без edits реализации, budgets или исходного evidence.
5. `manifest.tree == recomputed tree == every receipt tree == tree_binding`.
6. Paths разрешаются, IDs уникальны, `required_rows ↔ rows` покрытие двустороннее.
7. `na` разрешён только при доказанно false applicability predicate. Failure, missing report или исчерпание бюджета не являются `na`.
8. Нет CHECK PASS при binding `open` row.
9. Изменение tree после freeze требует нового корректного binding и revalidation; чужие/stale receipts не переиспользуются.

**Оба gate обязательны; любой nonzero — FAIL:**

```bash
python3 scripts/validate_inner_loop.py manifest artifacts/pdca/T246/rv3/manifest.json
```

```bash
python3 scripts/pdca_evidence.py lint --repo . --manifest artifacts/pdca/T246/rv3/manifest.json
```

Требуется rc=0 у обоих; rc=2 evidence lint не игнорируется. В terminal GAP также сохраняются результаты validators, но structural validity не превращает незакрытые acceptance rows в PASS.

### CHECK re-gather

Владелец `check`, бюджет **2** точечных обращения:

- восстановление/проверка конкретного existing log, artifact, receipt или tree binding;
- без новых measurements, изменения code и повторного integration boundary.

После исчерпания — вердикт по имеющимся evidence. Отсутствующий отчёт сам по себе не обосновывает contract revision или новый DO cycle.

## Progress log

Формат локального журнала — pipe convention; фактические outcomes добавляются, не предсказываются.

- 2026-10-17 | D246/T246 | D246 rv2 GAP — qualification REJECT, valid-run-count 0!=3, captured 10k runs 1–4, protocol lock absent; rv2 evidence preserved.
- 2026-10-17 | D246/T246 | D246 PLAN rv3 — N=1, r=3, rv=3, n=1, iteration 1/3; owner «обнови пути и продолжи», Gate H pending, rv3 measurement executions 0; persist contract before DO.

В collection lane использовать её **существующий group value** и строку вида `- 2026-10-17 | group | D246 state — numbers; next.`, без переименования группы и переписывания истории.

## First DO step и DO ordering

1. **Сохранить этот план** в указанном status file; materialise inherited contract slots; закрепить `required_rows`, owners и CHECK re-gather budget. Зафиксировать исходный tree/diff и budgets bytes. Проверить обязательные Gate 1 условия.
2. Добавить точный supersession block в rv2 и текущую PLAN-запись collection lane. Историю не менять.
3. Выполнить **D246-H**: три harness fixes и additive `parse-check`; никаких product edits.
4. Пройти C-H-UNIT → C-H-NEGATIVE → C-H-PARSE. Максимум H1/H2. Не green — terminal GAP, **0 measurements**.
5. Выполнить **D246-B**: Roslyn-resolved R100 scope, C-BUILD, C-CORE, C-SQLITE, C-CONVENTIONS, **один** C-INTEGRATION, C-INTEGRATION-SKIPS. Зафиксировать результаты и отсутствие product C# diff.
6. Проверить protocol identity, inherited mutex, seed и budgets byte equality; завершить build/test/coverage activity.
7. C-PROTOCOL; убедиться, что lock отсутствует и ledger fresh.
8. C-BASELINE: 3 valid / максимум 4 executions.
9. C-QUALIFY. Только PASS с корректно frozen lock разрешает продолжение. Иначе GAP.
10. C-ORACLE: out-of-process Default reconciliation.
11. C-FINAL: 3 valid / максимум 4 executions, в общем deadline и ≤8 total executions.
12. C-GATE; сверить ledger, caps, hashes, lock и run coverage.
13. Собрать manifest/receipts, freeze tree-bound handoff, выполнить оба evidence validators.
14. Передать в CHECK. CHECK применяет P1 и completeness без понижения. D246 завершается только по его вердикту, не по DO self-report.
15. После каждого завершённого шага и при старте следующего обновлять todo и factual progress; при stop condition фиксировать GAP, не запускать следующий шаг.
