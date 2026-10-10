## Collection rc2-final — own PLAN (2026-10-09, HEAD 9a2a2871)

- task: T151 / #151; selected_variant: `pdca-dotnet`; cycle_id: N=1; plan_revision: r=2
- baseline: `9a2a2871`; evidence contract: rv2 (supersedes rv1) pinned at artifacts/pdca/D151/rv2/manifest.json (15/15 required rows met)
- plan_state: **done/verified** — CHECK **PASS** (r=2, rv2, 15/15 rows closed)
- issue: #151 **closed** — https://github.com/AlexeyShirshov/nextorm/issues/151
- next step: none
- P: replace narrative-based certification with a frozen raw-artifact bundle and reproducible, row-level CHECK verification.
- CLASSIFICATION: evidence/tooling deliverable; no established product defect. Prior STOP remains closed-incomplete, not retroactively passed.

---
# T151 / #151 — PLAN: certify the native extreme-row mutation campaign
Collection `rc2-final`; variant `pdca-dotnet`; cycle N=1; plan r=1; evidence rv=1.
Status `docs/specs/status/rc2-151-stryker-mutation-1.md`; phase PLAN; DO not started.
Revision lineage: this cycle's rv=1 is new, not a renaming of prior rv=2. Preserve prior rv=1→rv=2 supersession and all historical artifacts.

## Goal and acceptance criteria
- AC1 — Two independently executed, renderer-only campaigns use pinned Stryker 5.0.0, `perTest` + `mtp`, finish exit 0. Negative: timeout/124, coverage-capture failure, another mutation scope, or copied-as-B execution cannot count.
- AC2 — Raw reports demonstrate valid coverage for executable mutants: nonempty `coveredBy`, preserved `killedBy`, no Survived or NoCoverage. Negative: historical invalid all-empty `coveredBy:[]` capture cannot pass, regardless of reported score or exit 0.
- AC3 — A/B normalized mutant identities and dispositions match exactly; every report entry retained and individually classified. Negative: missing entries, A/B mismatch, or treating Timeout/CompileError/Ignored as Killed fails.
- AC4 — Positive control removes `" desc"` and produces an attributable assertion failure; baseline passes and original source bytes are restored. Negative: compilation failure, timeout, no failure, or an un-restored source fails the control.
- AC5 — CHECK opens raw evidence by frozen path, verifies SHA-256 hashes and all row-level predicates, and produces an independent exit-0 verification receipt. Negative: an aggregate brief, absent raw file, changed hash, or producer receipt alone is insufficient.
- Historical baseline: denominator 97; Killed 56, Survived 0, NoCoverage 0, Timeout 3, CompileError 27, Ignored 11. Reconcile any difference explicitly; never silently redefine scope/acceptance.

## Minimal solution
Essential outcome: close #144 debt 7 with automatically verifiable coverage, control, reproducibility, individual disposition, retention. Hard constraints: no lasting product change; preserve raw bytes and prior evidence; no reduced mutant scope; no certification through `thresholds.break:0` alone. Optimum: reuse the pinned config + evidence helper; add a documented evidence interface, frozen inventory, and independent CHECK receipt.
| Alternative | Benefit | Cost/risk | Decision |
|---|---|---|---|
| Another aggregate brief | Cheapest | Repeats the demonstrated certification failure | Reject |
| Frozen raw bundle + semantic verifier + CHECK row ledger | Directly repairs the evidence gap | Bounded tooling changes and two campaigns | **Choose** |
| Replace runner/framework or change renderer | Broader intervention | Unjustified scope and behavior risk | Deferred; trigger: reproducible failure of the pinned working mode |

## What was not specified / predecessors
- Four files reportedly committed in `e67aa5f8`, while STOP says uncommitted: verify HEAD, ancestry, tracked contents, working-tree status; do not reapply the preserved patch blindly.
- Existing helper CLI/schema and raw report locations not supplied: D1 confirms; D2 supplies the planned interface.
- No verified issue URLs supplied: scout obtains #151/#144 URLs before issue handoff.
- Predecessors: #144 debt 7 and prior #151 campaigns are historical inputs, not passing CHECK evidence.
- The old 50-mutant/900s experiment is historical; bound each fresh execution at 3600 s.
- Evidence-root collision: archive existing material before publishing; never overwrite/delete historical evidence.

## DO tasks — all active, ordered
- D1 — provenance/CLI scout; inspect `.config/dotnet-tools.json:26-31`, `stryker-config.json:1-25`, helper modes, source/report identities, the four-file commit claim. Record immutable inputs and starting diff.
- D2 — `tools/stryker/d151-evidence.py`; implement/document the planned interface, complete mutant ledger, strict validation, negative self-tests, atomic freeze.
- D3 — run PostgreSQL tests and reversible control; reuse `ExtremeRowNativeSqlGenerationTests.cs:571`. No extra product tests unless an evidenced coverage gap requires PLAN review.
- D4 — execute A/B sequentially; retain complete reports, commands, output, exit codes, timestamps, identities; generate comparison and individual dispositions.
- D5 — freeze bundle, run independent verification, update status/debt disposition and handoff.
- Deferred: renderer/API changes and expanded provider mutation campaigns.
- Mode: sequential units, current worktree. No additional worktrees.
- Footprint: existing four files reported tracked/committed; expected edits are helper, narrowly necessary config/test adjustments, status/docs, retained evidence. Product-source edit is temporary control only.
- Design checklist: renderer-only scope; explicit exclusions; no shared-command/cache mutation; no public API changes; deterministic normalization; raw-byte preservation; fail-closed validation; restoration in cleanup; CRLF; no commit/push implied.

## Test strategy and closed variant matrix
Coverage source: full `tests/nextorm.postgres.tests` suite incl. renderer responsibility tests (9 methods/15 cases). Tooling self-tests exercise valid fixtures plus missing files, hash corruption, invalid coverage, survivors, A/B mismatch, false control, nonzero execution. Thresholds remain line 85% / branch 75%; mutation score is not a substitute.
| Variant | Closure |
|---|---|
| Renderer campaign A | test: fresh independent execution, complete raw report, exit 0 |
| Renderer campaign B | test: second fresh execution with identical inputs, exit 0 |
| Control probe | test: baseline pass, deliberate assertion failure, byte-exact restoration |
| Byte-identical A/B | guard: separate execution provenance mandatory, no reuse/copy-as-B |
| Survivors = 0 | test: derive from raw entries; fabricated/nonzero survivor fixture must fail |
| Non-zero exit | guard: any campaign nonzero/124 fails even if a report exists |
| Coverage missing/empty | guard: executable mutants without valid coverage fail |
| Timeout / CompileError / Ignored | test: separate per-mutant reasons and raw pointers; never relabel as Killed |
| Legacy capture mode / `thresholds.break:0` | guard: neither establishes validity |
| Other providers / runtime null-default-value-reference cases | guard: no production behavior change; expanded campaigns deferred |

## Priority, docs, performance, reconnaissance, risks
P1 rows by construction: scope/pin/provenance, valid coverage, A/B reproducibility, zero survivors, positive control/restoration, complete disposition, hashes/raw accessibility, independent verification. CHECK cannot downgrade.
Docs: update this status, evidence README/interface, #144 debt cross-reference/#151 handoff; public EN/RU API docs untouched. No public links to internal specs.
Performance measurement: no acceptance benchmark required — `stryker-config.json`, `.config/dotnet-tools.json`, tests concern offline tooling/tests, not cached execution/per-row production work. Record campaign durations only for the time budget.
Reconnaissance: targeted D1 scout for commit-state contradiction, CLI compatibility, report schema and paths.
Risks: Stryker 5.0.0/MTP quirks; stale binaries; nondeterministic IDs/paths; transient timeouts; exclusion-heavy score; control restoration; evidence retention size.
Confidence high in the evidence-gap diagnosis and selected mode; commit state, helper interface, raw field completeness remain unverified prerequisites.

## Versioned evidence contract — rv1
Root E=`artifacts/pdca/D151/rv1`; anchor `$E/manifest.json`; sources PLANNED until produced. D2 implements these invocations; flags are planned API.
`manifest --freeze` derives comparison/disposition, performs producer verification, writes its receipt, atomically hashes the inventory. Every mutant ledger entry carries report path/hash, JSON pointer, file-qualified mutant ID, location/mutation/status, original `coveredBy`/`killedBy`, disposition; absent required fields fail.
Rows: R-PROV/E01 `git rev-parse HEAD`; `git status --porcelain`; `git show --name-status e67aa5f8`; `dotnet tool restore`; `dotnet tool run dotnet-stryker --version` → 5.0.0. R-TEST/E02 `dotnet test tests/nextorm.postgres.tests -c Debug` exit 0 nonempty. R-COV/E03 `timeout 3600 dotnet stryker --config-file stryker-config.json --output "$E/run-a"` exit 0 no capture failure, raw report retained. R-REPRO/E04 campaign B same. R-CONTROL/E05 `python3 tools/stryker/d151-evidence.py control-probe --evidence-root "$E"` exit 0 only on baseline=0/altered nonzero/restoration verified. R-COMPARE/E06 `... manifest --evidence-root "$E" --freeze` exact A/B match. R-DISPOSITION/E07 same invocation zero survivors/NoCoverage complete ledger. R-RETENTION/E08 same invocation manifest enumerates every artifact path/SHA-256/row + verifier receipt. R-NEGATIVE/E09 `python3 tools/stryker/d151-evidence.py self-test --evidence-root "$E"`. R-CHECK/E10 `python3 tools/stryker/d151-evidence.py verify --evidence-root "$E" --read-only --result artifacts/pdca/D151/check-rv1/verifier-result.json` independent receipt; CHECK opens raw reports, control, comparison, disposition, manifest and producer receipt.
CHECK re-gather budget: at most 2 targeted passes. A justified contract revision explicitly supersedes rv=1, retains IDs/obligations, adds new-variant IDs.

## Compact handoff
Persist this complete PLAN first; no DO in this session. Resume D1→D5 later, then CHECK against the frozen bundle. Prior product-defect status stays "not established"; close debt only after raw row-level certification.
Refs: #151; #144 debt 7; `docs/specs/status/native-extreme-row-144-1.md`; prior STOP/status history; `docs/specs/status/rc2-151-evidence/D151-STOP-incomplete.patch`; `e67aa5f8`.

---
# PDCA rc2 — Issue #151: Stryker mutation re-run (native extreme-row)

- task_id: D151
- issue: #151
- variant: pdca-dotnet
- cycle: N=1
- plan revision: r=2 (terminal; r=1 exhausted at n=3, r=2 at n=1)
- evidence contract revision: rv=2 (supersedes rv=1)
- plan_state: closed-incomplete (STOP)
- status: **incomplete — terminal STOP** (CHECK evidence-certification failure; no product defect; no r=3/re-run permitted)
- closure (historical): documentation/ledger-only per `escalate` decision (2026-10-09); no product changes, no commit; attempt n=2 (see closure record below)
- base: 1.0.9-rc2 @ 18659e41
- milestone: 1.0.9-rc2
- predecessor-result requirements: none
- status file: docs/specs/status/rc2-151-stryker-mutation-1.md
- note: PLAN-only handoff superseded by the terminal STOP record at the end of this file; the substantive campaign is valid and reproducible, but CHECK could not certify evidence-completeness.
- patch: `docs/specs/status/rc2-151-evidence/D151-STOP-incomplete.patch` (uncommitted D151 test/config/tooling changes)

## Паспорт плана

- **Задача:** D151, issue **#151**, milestone `1.0.9-rc2`.
- **Цикл:** `N=1`; **ревизия плана:** `r=1`; первая попытка будущего DO/CHECK: `n=1`.
- **Контракт evidence:** `rv=1`, без предшествующей ревизии.
- **plan_state=ready**.
- Репозиторий: `/home/alex/sources/nextorm`; исходная точка: `1.0.9-rc2 @ 18659e41`.
- Статус-файл: **`docs/specs/status/rc2-151-stryker-mutation-1.md`**.
- Это **PLAN-only handoff**. `coder` записывает план; DO здесь не запускается и продолжается позднее в коллекции после её обязательного гейта. **`docs/specs/status/collection-1.0.9-rc2.md` не изменять.**
- Все будущие проверки, источники и артефакты ниже — **планируемые**, не полученные результаты.

## Цель, scope и критерии приёмки

Получить **валидный автоматический и воспроизводимый** mutation campaign исключительно для исходного PostgreSQL extreme-row renderer.

### Границы

**Включено:** настройка и диагностика Stryker, локально закреплённый инструмент, тесты ответственности renderer, проверка контрольной мутации, два воспроизводимых запуска, индивидуальный disposition ledger и инструкции повторения.

**Не включено:** мутация ClickHouse, всего core или соседних SQL-builder методов; изменение TFM, замена MTP/xunit-v3; числовой mutation-score gate; ручной анализ вместо автоматического запуска.

### Критерии приёмки

| ID | Положительный критерий | Негативный случай — не принимаем |
|---|---|---|
| D151-REQ-01 | Исходные и итоговые тесты проходят; build — 0 warnings; сохранены .NET 10 и существующий test stack. | Падение baseline, подавленные диагностические ошибки, downgrade TFM или подмена runner. |
| D151-REQ-02 | Закреплены точная версия Stryker, рабочий repo-local config, команды и однозначный mutation/test scope. | Использование только глобальной установки; незадокументированный запуск; активные мутанты вне renderer. |
| D151-REQ-03 | Финальный автоматический запуск валиден; режим coverage доказан и явно указан. | `coverage capture failed`, неподтверждённая активация мутантов, принятие некорректного score. |
| D151-REQ-04 | Контрольная мутация направления сортировки действительно активируется и убивается тестами. | Только наличие мутанта в JSON; ошибка компиляции вместо test failure; ручной просмотр вместо automatic kill. |
| D151-REQ-05 | Каждый `Survived`/`NoCoverage` имеет тест, который затем убивает мутант, либо индивидуальное воспроизводимое обоснование equivalence/out-of-scope. Остальные статусы также учтены. | Blanket justification, необъяснённые timeouts/exclusions, незавершённый ledger. |
| D151-REQ-06 | Закрыты Min/Max, применимые NULL-сценарии, composite order, empty suppression и winning-row identity с обозначением границ ответственности. | Мутация core ради расширения scope; объявление core-поведения проверенным только renderer-тестами. |
| D151-REQ-07 | Два финальных запуска одной конфигурации дают одинаковые нормализованные множества Killed/Survived и согласованный disposition. | Сравнение только процентов или числовых IDs; необъяснённая нестабильность. |
| D151-REQ-08 | Сохранены валидные отчёты, логи, ledger, manifest воспроизводимости; исторические 2% явно названы невалидными и debt anchors сохранены. | Отчёт без команды/версии; потеря артефактов; утверждение, что долг закрыт при incompatibility. |

**Подтверждённая несовместимость всех допустимых режимов — BLOCKER, а не успешный отрицательный результат.**

## Минимальное решение

### Три ответа

1. **Что решаем:** достоверность автоматического mutation testing выбранного renderer, а не повышение произвольного процента.
2. **Что непреложно:** узкий mutation scope, .NET 10/MTP/xunit-v3, baseline, активированный и убитый control, индивидуальные disposition и воспроизводимость.
3. **Минимальный оптимум:** сначала изолированный scope/runner probe; затем самый информативный **доказанно рабочий** coverage mode, необходимые тесты и два одинаково настроенных campaign.

### Альтернативы и выбор

| Подход | Плюсы | Минусы / цена / риск |
|---|---|---|
| Scoped `perTest` | Атрибуция coverage и kill по тестам; эффективный campaign. | Возможная несовместимость фильтрации MTP/xunit-v3; пустой coverage нельзя принять. |
| Scoped `all`, если поддерживается закреплённой версией | Возможна рабочая агрегированная оптимизация без per-test фильтрации. | Нужны отдельные доказательства корректного отбора; отсутствие per-test attribution допустимо только явно. |
| Scoped `off` | Не зависит от coverage-based test selection; весь выбранный no-DB проект исполняется для мутантов. | Дороже по времени; отсутствие `coveredBy` ожидаемо, но активацию/control/kill требуется доказать другими evidence. |

**Выбор:** `perTest → all, если доступен → off`. Переход допускается только после записанного результата probe. Неработающий `perTest` не является обязательным условием финального campaign. `off` — полноценная допустимая альтернатива, но не молчаливое признание испорченного coverage валидным.

Планом фиксируется Stryker **5.0.0** как первая локальная версия для проверки; её совместимость **не предполагается доказанной**. Произвольное обновление инструмента вне этой последовательности не делать: при необходимости другого toolchain — возврат в PLAN.

## Baseline и исторические артефакты

**Для воспроизведения исходного дефекта выбираем исторический B0: 50 mutants / 2%**, документированный в `native-extreme-row-144-1.md:132-135`. Не выдаём неподтверждённый 97-mutant отчёт за воспроизведённый baseline.

- Повторить известную историческую команду один раз, **до создания автоматически подхватываемого `stryker-config.json`**.
- Воспроизведение относится к признаку невалидного capture, а не к обязательному совпадению числа мутантов. Отличия версии, ревизии исходников и числа мутантов записать.
- Если симптом сейчас не воспроизводится, записать `not reproduced`; это не легализует исторические 2%.
- **B1:** существующий `StrykerOutput/d151/campaign/reports/mutation-report.json` с 97 renderer mutants — отдельный входной диагностический артефакт. Сохранить JSON/HTML и hashes до запуска; команда и логи неизвестны, поэтому его нельзя использовать как финальный результат.
- Findings B1, включая `775`, `795` и четыре timeout, использовать как диагностические подсказки. В новых отчётах сопоставлять их по исходному фрагменту, span и replacement, **не по стабильности числовых IDs**.

## Чего не сказала постановка

| Пробел | Закрытие |
|---|---|
| H1 или H2 является причиной сбоя? | Ограниченный scope/runner probe; решение по наблюдаемым результатам. |
| Поддерживает ли 5.0.0 режим `all` и предлагаемые config keys? | Проверить локальный `--help` и фактическую загрузку config; неподдерживаемый `all` закрывается guard и пропускается. Не копировать непроверенные camelCase keys. |
| Какая точная команда создала B1? | Неизвестность фиксируется в provenance; B1 не считается воспроизведённым результатом. |
| Какие нынешние мутанты соответствуют старым IDs? | Semantic/source mapping при анализе новых JSON; IDs не выдумывать заранее. |
| Является ли `Keys.Count >= 0` эквивалентным в разрешённом контексте? | Roslyn refs/callers + отрицательный guard-тест либо внутренний тест, если отрицательный случай достижим. Без доказательства — не justification. |
| Что означает timeout alias-collision? | Сопоставление mutation diff, ограниченный повтор и тест collision-сценария; не считать автоматически убитым. |
| Где хранить большие/generated отчёты после коллекции? | `StrykerOutput/d151/r1-n1` сохраняется как artifact directory; manifest с hashes и путями — в статусе. Перед очисткой требуется проверенная долговечная копия/collection artifact. Не заявлять upload, которого не было. |
| Номер группы и её исходная ревизия? | Предшественника нет. При запуске после другой группы записать фактический commit и повторить baseline; пересечение footprint разрешить до DO. |
| Доступность Podman/coverage tool? | Проверяемое предусловие DO; следовать integration skill, включая запуск машины. Не превращать skip в pass. |

## Предусловия и predecessor-result requirements

- **Предшественник:** отсутствует; никаких результатов другой задачи для D151 не требуется.
- Если коллекция выполняет D151 после иных изменений: предоставить фактическую ревизию дерева, перечень пересечений footprint и свежий passing baseline.
- Доступны .NET 10, локальные инструменты из manifest, Python 3 для небольшого evidence verifier.
- Перед контейнерными тестами исполнитель загружает `.opencode/skills/running-integration-tests/SKILL.md`.
- Должны быть доступны renderer исходной ответственности и существующие PostgreSQL tests; если их контракт изменён другой группой, не адаптировать scope молча — вернуть PLAN.
- Локальный Stryker install/restore не даёт права менять зависимости test stack.
- Git push запрещён. Коммиты — только при отдельно разрешённом collection auto-commit. Сам этот план разрешения не даёт.

## Задачи DO — в порядке исполнения

Все задачи ниже **активные, не выполненные**.

| Единица | Действие и anchors | Решение |
|---|---|---|
| **D151-D01** | Зафиксировать дерево/toolchain; сохранить B1; добавить локальный Stryker 5.0.0 в `.config/dotnet-tools.json`; выполнить build и baseline tests. Проверить root policy и CRLF. | **fix now** |
| **D151-D02** | Ограниченно воспроизвести B0 по исторической команде, `native-extreme-row-144-1.md:132-135`; записать exit/log/result. Получить локальный Stryker help и подтвердить допустимые параметры. | **fix now** |
| **D151-D03** | Scope/runner spike: одна production mutation file, один PostgreSQL no-DB test project; проверить `perTest`, при его непригодности `all`/`off`. Зафиксировать рабочий режим и его доказательства до окончательной настройки config. | **fix now** |
| **D151-D04** | Закрыть тестовые gaps в PostgreSQL SQL-generation tests: short, converter rejection, non-integral groups, renderer-local empty path, alias collisions, grouped SQL boundary. Anchors: renderer `:34-92`, tests `:166,:178,:191,:441`. Проверить достижимость старого `775`; старый `795` разобрать индивидуально. | **fix now** |
| **D151-D05** | Создать repo-local config, evidence verifier и точные инструкции. Закрепить фактический контрольный direction mutant у renderer `:81`: предпочтительно inversion; допустима автоматически создаваемая потеря `" desc"`, если она доказанно обнаруживается Max-тестами. | **fix now** |
| **D151-D06** | Автоматический campaign A; для каждого Survived/NoCoverage — тест либо воспроизводимая justification. Все CompileError/Ignored/Timeout проверить и записать; unexplained timeout не закрывать. После изменения тестов финальный campaign A повторяется. | **fix now** |
| **D151-D07** | Campaign B на неизменённых config/source/tests; сравнить нормализованные множества; итоговые unit/core/container checks, coverage evidence, verifier и CRLF/diff review. | **fix now** |
| **D151-D08** | Заполнить ledger, manifest и итоговый статус; сохранить historical debt references `native-extreme-row-144-1.md:132-135,209-210` и связь debt 7 → #151 `:304,:315`. | **fix now** |

### Deferred

- Расширение mutation testing на core/ClickHouse — **deferred**, триггер: отдельная согласованная задача с новым scope.
- Замена test stack/TFM — **не разрешена**, не fallback.
- Проверка иной версии Stryker — **deferred**, триггер: подтверждённая непригодность всех разрешённых режимов 5.0.0 и новый PLAN.
- Несвязанные рефакторинги renderer/core — **deferred**, триггер: отдельный воспроизводимый production defect. Не исправлять их попутно.

## Footprint и режим единиц

### Файлы записи

**Планируемые постоянные изменения:**

- `.config/dotnet-tools.json` — локальный tool pin.
- `stryker-config.json` — рабочий, проверенный config.
- `tests/nextorm.postgres.tests/ExtremeRowNativeSqlGenerationTests.cs` — необходимые gap tests.
- `tools/stryker/d151-evidence.py` — небольшой verifier/provenance/control helper.
- `docs/specs/status/rc2-151-stryker-mutation-1.md` — план, evidence index, ledger или ссылка на его artifact, re-run instructions.

**Условные изменения, ограниченные тестами:**

- `tests/nextorm.postgres.tests/ExtremeRowNativeCacheStabilityTests.cs`;
- `tests/nextorm.postgres.tests/ExtremeRowPayloadLazinessTests.cs`;
- `tests/nextorm.integration.tests/PostgresExtremeRowNativeSpecificTests.cs`.

Они нужны только если соответствующий gap нельзя закрыть в SQL-generation suite; причину записать.

**Временная запись:** `src/nextorm.postgres/PostgresExtremeRowRenderer.cs` для control probe с обязательным восстановлением и hash check; Stryker workspace/output.

**Generated evidence:** `StrykerOutput/d151/r1-n1/**`. Существующий `StrykerOutput/d151/campaign/**` не перезаписывать.

**Не изменять:** production core/ClickHouse, TFM/test-package declarations, исторические status files, collection status, public docs.

### Режим

**Последовательно в одном дереве.** Config, tool manifest, контрольная временная мутация и тесты имеют общий контракт и пересекаются по исполнению. Параллельные Stryker/control runs запрещены. Worktree не требуется; при collision с другой collection unit — сериализовать общий footprint, а не автоматически создавать worktree.

C# definitions/refs/callers и достижимость исследовать **Roslyn-only**. CRLF сохранить; после правок нормализовать изменённые текстовые файлы. CPM соблюдается; версии NuGet не добавлять в `.csproj`.

## Тест-стратегия и матрица вариантов

### Стратегия

- Mutation runner выбирает **только** `tests/nextorm.postgres.tests/nextorm.postgres.tests.csproj`.
- Core tests проверяют соседние обязанности, но не добавляются в Stryker test scope.
- PostgreSQL container tests подтверждают SQL semantics, которые нельзя доказать строковой генерацией.
- Дополнительные тесты добавляются по наблюдаемому gap, без выдуманных заранее test symbols.
- Coverage policy окружения: **line 85%, branch 75%**; hard gate только на `main`, на `1.0.9-rc2` — предупреждение. Targeted coverage не выдавать за полный repository coverage. Mutation-score threshold не вводить.

### Матрица вариантов

| Вариант / anchor | Закрытие |
|---|---|
| Непустые keys, `CanRender :34-37` | **test**: существующие SQL-generation tests. |
| Пустые/default keys и мутант старого `775` | **guard + test**: доказать public-path invariant Roslyn; проверить отказ/защиту. Если внутренняя достижимость есть — internal negative test. |
| Integral groups | **test**: существующий grouped/composite SQL. |
| Non-integral groups | **test**: добавить renderer rejection/portable fallback case. |
| `short`, `short?` | **test**: добавить acceptance cases для обоих. |
| `int`, `int?`, `long`, `long?` | **test**: подтвердить существующие cases; недостающий элемент добавить, не считать nullable автоматически эквивалентным. |
| `string`, `DateTime`, `double`, expression, temporary-table keys | **test/guard**: существующие rejection/fallback cases; сопоставить каждый вариант с результатом. |
| `UsesConverter == true` | **test**: добавить rejection; явный контраст с тем же direct integral key без converter. |
| Value/reference и NULL | **test/guard**: integral value/nullable выше; reference-key rejection; all-null и mixed-null semantics — core anchor `SqlBuilder.cs:715-745`, особенно `:736`, плюс PostgreSQL integration. |
| Min/Max; asc/desc | **test + control**: tests `:53,:61-71`, renderer `:81`. |
| Single/composite/group ordering | **test**: cases `:166,:178,:191`; проверка порядка всех компонентов. |
| Empty input и renderer empty-key/default path | **test/guard**: SQL-generation regression для применимого renderer path; отсутствие строки — container case `:271`, core source/select anchors. |
| Winning-row identity, payload и aliases | **test**: SQL-generation/payload suites и integration projections; обязанности core `:754-781,:827-856`. |
| Derived-alias collision | **test**: key collision `:441` плюс group/payload/повторный collision; отдельно сопоставить прежние timeout. |
| Grouped SQL literal `" from ("`, старый `795` | **test**: assertion полного grouped SQL boundary, не только общего token. |
| PostgreSQL renderer dispatch | **test/guard**: dialect guard suite; core gate `:621-655`. |
| ClickHouse и прочие provider mutation variants | **deferred**, триггер: отдельный mutation-scope issue; текущая матрица явно ограничена PG. |
| `perTest`, `all`, `off` | **test/guard**: probe и conditional evidence ниже; unsupported `all` — documented guard. |
| `cache`/повторное использование, payload laziness | **test**: существующие 3+3 tests; не менять shared command flags. |

**NULL, empty suppression и identity не присваивать целиком renderer:** в ledger указывать конкретный core anchor и проверку применимой ответственности.

### Матрица приоритетов

| Приоритет | Строки/обязательства |
|---|---|
| **P1 по постановке** | Все `D151-REQ-01…08`; `D151-EC-01…10`; valid automatic execution, control, scope, stack preservation, individual disposition, critical-category matrix, reproducibility и debt provenance. |
| **P1 по execution path** | renderer `:34-92`; неизменяемые responsibility anchors core `:621-655,:715-856`; соответствующие variant rows. |
| **P2** | Удобство HTML-навигации и визуальное оформление; оно не заменяет JSON/log/ledger. |

CHECK не понижает P1 и не принимает общий score вместо выполнения строк.

## Разведка и performance

### Разведка — нужна

Один ограниченный spike **до окончательного закрепления config**:

1. Подтвердить scope одного renderer и одного no-DB test project.
2. Проверить baseline discovery и исполнение контрольного direction mutant.
3. Разделить coverage/test-filter failure и простой scope/config failure.
4. Проверить максимум **три режима**: `perTest`, поддерживаемый `all`, `off`; одна первичная попытка на режим. Дополнительные экспериментальные переборы — новый PLAN.

**Наблюдаемый критерий выбора:** baseline проходит; control реально исполняется и killed; нет capture ERR в выбранном режиме; test/mutation scope соответствует config; для оптимизации доказан корректный отбор. Для `perTest` нужны непустые `coveredBy` и `killedBy` хотя бы у одного renderer mutant, включая проверяемый control, если report предоставляет эти поля для него.

### Performance — runtime benchmark не нужен

Production query path не изменяется: renderer `:40-61,:79-92` остаётся исходным; работа D151 — one-time tooling, исторический запуск — `native-extreme-row-144-1.md:132-135`. BenchmarkDotNet не отвечает на вопрос валидности mutation capture.

**Измерять wall-clock/tool cost:** время B0 reproduction, каждого probe и двух campaign; baseline для стоимости — scoped probe выбранного режима. Эти измерения диагностические, без нового числового performance gate.

## План документации

- **Изменить:** только новый внутренний status file с tool version, проверенной конфигурацией, режимом, точными командами, manifests, disposition и re-run steps.
- **Не трогать:** исторические debt files, collection status, public `docs/**`, `docs/ru/**`, readme.
- Сохранить ссылки на исторические debt anchors внутри внутреннего статуса.
- Не создавать публичных ссылок на `docs/specs/**`.
- Операционный config не объявлять рабочим до успешной загрузки и campaign.

## Планируемые команды и артефакты

Все команды исполняются из корня репозитория. Для каждого вызова сохраняются stdout/stderr, реальный exit code и elapsed time; при `tee` требуется `pipefail`. Каталог:

`E=StrykerOutput/d151/r1-n1`

Создать каталоги `E`; подготовительный сбой не скрывать. CLI/config schema подтверждается D151-D02; если обязательная опция не поддерживается, зафиксировать несовместимость, не подменять команду молча.

| ID вызова | Точный планируемый вызов |
|---|---|
| **C01** | `dotnet --info`; `git rev-parse HEAD`; `git status --short`; `dotnet tool list --local` |
| **C02** | Если локальный Stryker отсутствует: `dotnet tool install dotnet-stryker --version 5.0.0`; затем `dotnet tool restore`; `dotnet tool list --local`; `dotnet stryker --help` |
| **C03** | `dotnet build -c Debug` |
| **C04** | `dotnet test tests/nextorm.postgres.tests -c Debug` |
| **C05** | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~InMemoryExtremeRowTests|FullyQualifiedName~ExtremeRowDialectGuardTests|FullyQualifiedName~ExtremeRowDescriptionLazinessTests"` |
| **C06** | До создания root config: `timeout 900 dotnet stryker --project src/nextorm.postgres/nextorm.postgres.csproj --mutate "**/PostgresExtremeRowRenderer.cs"` |
| **C07-P** | `timeout 900 dotnet stryker --config-file stryker-config.json --test-project tests/nextorm.postgres.tests/nextorm.postgres.tests.csproj --coverage-analysis perTest --output "$E/probe-perTest"` |
| **C07-A** | Если `all` поддерживается: `timeout 900 dotnet stryker --config-file stryker-config.json --test-project tests/nextorm.postgres.tests/nextorm.postgres.tests.csproj --coverage-analysis all --output "$E/probe-all"` |
| **C07-O** | `timeout 900 dotnet stryker --config-file stryker-config.json --test-project tests/nextorm.postgres.tests/nextorm.postgres.tests.csproj --coverage-analysis off --output "$E/probe-off"` |
| **C08** | `python3 tools/stryker/d151-evidence.py control-probe --project tests/nextorm.postgres.tests/nextorm.postgres.tests.csproj --source src/nextorm.postgres/PostgresExtremeRowRenderer.cs --evidence "$E"` |
| **C09-A** | `timeout 3600 dotnet stryker --config-file stryker-config.json --output "$E/run-a"` |
| **C09-B** | `timeout 3600 dotnet stryker --config-file stryker-config.json --output "$E/run-b"` |
| **C10** | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~PostgresExtremeRowNativeSpecificTests"` |
| **C11** | `dotnet-coverage collect "dotnet test tests/nextorm.postgres.tests -c Debug" -f cobertura -o "$E/coverage.cobertura.xml" --settings coverage.settings.xml`; `reportgenerator "-reports:$E/coverage.cobertura.xml" "-targetdir:$E/coverage" "-reporttypes:Html;TextSummary"` |
| **C12** | `python3 tools/stryker/d151-evidence.py --self-test`; `python3 tools/stryker/d151-evidence.py verify --evidence "$E" --contract docs/specs/status/rc2-151-stryker-mutation-1.md --rv 1` |
| **C13** | `git diff --check`; `git diff --stat`; `git diff -- src/nextorm.postgres/PostgresExtremeRowRenderer.cs Directory.Build.props Directory.Packages.props global.json` |

Python CLI — **планируемый интерфейс нового helper**, не существующий инструмент. Helper должен проверять реальную структуру JSON, а не заранее придуманные mutant IDs.

### Обязательное содержание config/helper

- Config: фактически поддерживаемые 5.0.0 keys; один project, один `test-project`, один renderer glob, выбранный coverage mode; JSON и HTML reports.
- Не вводить mutation-score acceptance threshold. Если schema требует `break`, использовать `0` только для исключения числового gate; exit `0` всё равно не означает выполнение качественных критериев.
- Control helper: baseline проходит; временно активируется закреплённая direction mutation; тесты падают именно на assertion, не на compile/discovery; источник восстанавливается даже при ошибке; hashes совпадают. Затем **тот же семантический control должен быть Killed в автоматическом Stryker report**.
- Verifier: schema/scope/provenance/control/ledger/reproducibility checks; отрицательные self-test fixtures для capture ERR, отсутствующего control, unresolved survivor и mismatched sets.

## Версионированный evidence contract — `rv=1`

Общее для **каждой строки**:

- Status всех источников: **planned**.
- Источники baseline/probe не могут подменять финальные campaign.
- `exit 124` от внешнего timeout — незавершённое evidence, не mutation kill.
- Report `Killed` проверяется вместе с валидностью исполнения; `CompileError`/`Ignored` — не Killed.
- Все артефакты перечисляются в `E/manifest.json` с командой, версией, ревизией дерева, SHA-256 и относительным путём.
- При следующей обоснованной ревизии сохраняются IDs и обязательства; указывается `rv=2 supersedes rv=1`, новые варианты получают новые IDs. Здесь supersession отсутствует.

| Строка / requirement / rv | Проверка и точный вызов | Виды и планируемые источники evidence; ожидаемый результат/лог | Артефакты | Владелец / наблюдаемый предикат применимости |
|---|---|---|---|---|
| **D151-EC-01** / REQ-01 / **1** | C01–C05 до и после изменений; C13 | Environment/tool listing, build/test logs, tracked diff. C03–C05 exit `0`; build 0 warnings; net10/MTP/xunit сохранены. | `environment.txt`, `baseline-*.log`, `final-*.log`, `source-hashes.json`, diff record | **coder** / безусловно |
| **D151-EC-02** / REQ-02,08 / **1** | C06; архивирование B1; C02 | Исторический command/log/exit; B1 JSON/HTML + hashes; локальная версия 5.0.0. B0 может завершиться ошибкой/124: это диагностический результат, не acceptance. | `historical-reproduction.log`, `historical-result.json`, `baseline-b1/**`, `tooling.txt` | **coder** / безусловно; B1 — при наличии входных файлов |
| **D151-EC-03** / REQ-02,03 / **1** | C07-P, условно C07-A/C07-O; C12 | Resolved config, discovery/invocation logs, JSON scope. Один test project; активные мутанты только renderer. Выбранный режим: exit `0`, нет capture ERR. Для отклонённых modes — точный дефект. | `stryker-config.json`, `probe-*/**`, `mode-decision.json` | **coder** / scope probe безусловно; A/O — если предыдущий режим непригоден; A только если поддержан |
| **D151-EC-04** / REQ-03 / **1** | C09-A/B; C12 | **При `perTest`:** валидные `coveredBy`/`killedBy`, control и отсутствие capture ERR. **При `all`:** доказательства корректного агрегированного отбора. **При `off`:** resolved mode и исполнение полного выбранного проекта без coverage-based отбора. Непустые attribution поля для `off` не требуются. | `run-a/reports/*`, `run-b/reports/*`, campaign logs, `coverage-mode-validation.json` | **coder** / одна из трёх ветвей определяется фактическим config; обязательство валидного исполнения безусловно |
| **D151-EC-05** / REQ-04 / **1** | C08, C09-A/B, C12 | Control source diff/span/replacement и mapped report mutant; baseline exit `0`, control tests nonzero с assertion failure, восстановление hash. В обоих automatic reports соответствующий mutant **Killed**; execution evidence отличает активированный мутант от простой регистрации. | `control.json`, `control-baseline.log`, `control-active.log`, восстановительные hashes, report links | **coder** / безусловно |
| **D151-EC-06** / REQ-05 / **1** | C09-A/B; C12; targeted repeats через C04 после необходимых tests | По каждой нормализованной mutation identity — status, причина, сценарий/команда, результат и evidence. Каждый Survived/NoCoverage killed либо individually justified. CompileError подтверждён diagnostics; Ignored объяснён; Timeout разрешён повтором/доказательством, а не молча зачтён. | `disposition.json`, `disposition.md`, repeat logs, test diff | **coder** / для каждого фактически полученного мутанта; пустое множество подтверждается JSON |
| **D151-EC-07** / REQ-06 / **1** | C04, C05, C10; planned Roslyn `members`/`refs`/`callers` для `CanRender` и dispatch | Variant-to-test/guard mapping, Roslyn anchors, unit/core/integration logs. Exit `0`, selected PostgreSQL container tests реально выполнены, не skipped. Каждая критическая категория имеет evidence или доказанную границу ответственности. | `variant-matrix.md`, `responsibility-map.md`, `integration.log`, semantic evidence | **coder** / безусловно; дополнительные test changes только при обнаруженном gap |
| **D151-EC-08** / REQ-07 / **1** | C09-A/B; C12 | Одинаковые tool/config/source/test hashes; сравнение по file+span+original/replacement+mutator, не numeric ID. Campaign exit `0`; одинаковые Killed/Survived sets, согласованный ledger. | `repro-comparison.json`, hashes, manifest | **coder** / безусловно |
| **D151-EC-09** / REQ-01,08 / **1** | C11, C12, C13; review статуса | Coverage commands exit `0`; targeted scope явно обозначен; 85/75 policy отражена как branch warning, не mutation gate. Self-test/verify exit `0`; отрицательные fixtures отвергнуты; diff без stack/scope regressions, CRLF подтверждён helper. | cobertura, coverage summary, verifier logs, `diff-review.md` | **coder** / безусловно; числовой hard coverage gate только если фактическая ветка `main` |
| **D151-EC-10** / REQ-08 / **1** | C12; CHECK review статуса и manifest | Проверяемые report paths/hashes, exact re-run steps, historical invalid-2% statement и debt anchors; artifact retention подтверждён. Никакого утверждения о непроизведённом upload/run. | статус, `manifest.json`, JSON/HTML reports, retention record | **coder** / безусловно |

### CHECK и бюджет re-gather

- **Владелец:** `check`.
- **Бюджет:** максимум **2 адресных re-gather раунда на CHECK попытку**, каждый — не более двух ограниченных запросов конкретных отсутствующих evidence.
- Re-gather получает существующие logs/reports, checksums, semantic anchors и результат verifier; не запускает новый campaign под видом добывания отчёта.
- Только отсутствие отчёта **не обосновывает** новую ревизию или DO-итерацию: сначала re-gather.
- После бюджета: evidence остаётся missing; CHECK не выдаёт PASS. Возврат в PLAN с точным пробелом; при стойкой низкой уверенности — `escalate`, триггер 5.
- P1-строки и conditional obligations CHECK применяет без понижения.

## Классификация, риски и уверенность

### Классификация текущего входа

Это старт PLAN, не возврат DO → PLAN. Старый capture failure — **подтверждённое невалидное evidence**, но внешняя непреодолимая несовместимость ещё не доказана.

На будущем возврате:

- Исправимая настройка/предусловие → новая активная in-cycle задача; исходный D остаётся **blocked, не superseded**, критерии и остаток сохраняются.
- Допустимое отсутствие attribution при доказанном `off` → явное допущение/риск, без ослабления acceptance.
- Непригодность всех разрешённых вариантов, подтверждённая пробами → рекомендация оркестратору вызвать **`escalate`**; не закрывать D151.
- Неясные logs/activation/timeout → сначала точечный `scout`; стойкая низкая уверенность → `escalate`, **триггер 5**.
- Только реальное изменение задач/зависимостей/исправлений создаёт `r=2,n=1`; переименование и недостающий отчёт попытки не сбрасывают.

### Риски

- Stryker 5.0.0 может быть несовместим с текущим runner даже при `off`.
- Mutation population и IDs могут отличаться от B0/B1.
- Aliasing mutants могут зависать; внешний timeout не заменяет их disposition.
- Container environment может потребовать запуска Podman; selected-but-skipped PostgreSQL tests не принимаются.
- Global/root config collisions в коллекции; локальный manifest требуется согласовать по footprint.
- Generated output может быть удалён сборкой/cleanup; обязательны manifest и retention до очистки.
- Нельзя применять sticky `QueryCommand.Cache=false` в тестовой подготовке или исправлениях.

**Уверенность:** высокая в границах задачи и известных gaps; средняя в рабочем Stryker режиме. Совместимость, control activation и валидность campaign пока **не проверены** — именно они являются обязательными результатами DO, а не допущениями плана.

## Сводка

D151: `r=1`, `N=1`, `rv=1`, `plan_state=ready`; записать только `rc2-151-stryker-mutation-1.md`, DO — позднее в коллекции.
Выбор: scoped probe `perTest → поддерживаемый all → off`, затем необходимые tests, доказанный automatic control и два воспроизводимых renderer-only campaign.
PASS возможен только с валидными JSON/logs, индивидуальным disposition и сохранёнными артефактами; подтверждённая incompatibility — BLOCKER, исторические 2% остаются невалидными.

## DO results — D151-D01…D03 (r=1, N=1, rv=1; recorded 2026-10-09 08:58 UTC)

> Appended by `coder`. План выше не изменён. Replan не делался: `r=1`, `N=1`, `rv=1` сохранены.

### Ревизия / окружение (D01)
- Дерево на старте DO: `HEAD 4acb477261af3b227c4c2ae8716df39b5e3a5c57` (база плана была `18659e41`; ветка `1.0.9-rc2`, 34 ahead / 16 behind `origin/1.0.9-rc2`), рабочее дерево грязное (посторонние benchmark/integration/status-файлы других unit коллекции). Продуктовый код этим unit не менялся.
- `dotnet --info`: .NET SDK **10.0.401**, host 10.0.12, runtime net10.0; MSBuild 18.9.11. Evidence: `StrykerOutput/d151/r1-n1/environment.txt`.
- Stryker закреплён локально: `dotnet tool install dotnet-stryker --version 5.0.0` → exit **0**, запись добавлена в `.config/dotnet-tools.json`; `dotnet tool list --local` показывает `dotnet-stryker 5.0.0`. Evidence: `tooling.txt`, `stryker-version.txt`, `stryker-help.txt`.
- B1 сохранён: `StrykerOutput/d151/campaign/reports/mutation-report.{json,html}` SHA-256 в `baseline-b1.sha256` (json `0b63…41de`, html `be59…f231`); не перезаписан.

### Baseline (D01/D02)
- `dotnet build -c Debug` (полный `nextorm.slnx`): exit **0**, **0 warnings**; лог `build-debug.log`.
- `dotnet test tests/nextorm.postgres.tests -c Debug --no-build`: exit **0**, total **829** / failed **0** / skipped **0** (2s 137ms); лог `baseline-postgres-tests.log`.

### C06 — воспроизведение исторического B0 (D02)
Точная историческая команда (repo-root `stryker-config.json` отсутствовал):
`timeout 900 dotnet stryker --project src/nextorm.postgres/nextorm.postgres.csproj --mutate "**/PostgresExtremeRowRenderer.cs"`
- exit **124** (внешний `timeout`, остановлено на 900s — прогон не завершён). Логи `historical-reproduction.log`, `historical-reproduction.exit`.
- 922 mutants created; воспроизведён `[13:48:40 ERR] It looks like the test coverage capture failed. Disable coverage based optimisation.`; 118 CompileError / 11 Ignored(block) / 734 Ignored(mutate filter); 59 mutants поставлены в тест; финальных counts/отчёта нет (`StrykerOutput/2026-10-09.13-43-48/` пуст) — incomplete, не mutation score.
- Итог: **приоритетный симптом (capture failure) воспроизведён** на дефолтном `vstest` test-runner; исторические 50 mutants / 2.00% остаются невалидными (пустой `coveredBy`).

### Runner/scope spike (D03)
Факт по CLI-схеме (`stryker-help.txt`): у Stryker **5.0.0 нет CLI-опции `--coverage-analysis`**; `coverage-analysis` — **ключ config-файла** (`stryker-config.json` → `stryker-config.coverage-analysis`, default `perTest`). Точный планируемый argv отклоняется:
- argv `dotnet stryker --config-file stryker-config.json --test-project tests/nextorm.postgres.tests/nextorm.postgres.tests.csproj --coverage-analysis <mode> --output $E/probe-<mode>` → exit **1**, `Unrecognized option '--coverage-analysis'` для каждого из `perTest`/`all`/`off`; логи `probe-<mode>.literal-cli.log`.
- 5.0.0 также несёт `Stryker.TestRunner.MicrosoftTestPlatform.dll` и принимает `test-runner: "mtp"`; стек тестов репозитория — MTP/xunit-v3 (`global.json`), поэтому дефолтный `vstest` — корневая причина capture failure.

Config-based probe (эквивалентный argv, задокументированное отклонение):
`dotnet stryker --config-file StrykerOutput/d151/r1-n1/probe-perTest/stryker-config.json -O StrykerOutput/d151/r1-n1/probe-perTest`
- **perTest (+ `test-runner: mtp`)** — exit **0**, elapsed **605s**. Найдено 9437 tests; **coverage capture прошёл** для 9437 tests / 9 assemblies (`per-test coverage capture complete`), capture ERR нет. 922 mutants created → 118 CompileError / 11 Ignored(block) / 734 Ignored(mutate filter); **59 tested: Killed 51 / Survived 4 / Timeout 4 / Errors 0; score 93.22%**. Файл renderer суммарно **97 mutants** (совпадает с B1): Killed 51, Survived 4, CompileError 27, Ignored 11, Timeout 4. Непустые `coveredBy` у **70** mutants и `killedBy` у **51** — валидная per-test attribution. **Приоритетный симптом НЕ воспроизводится.** Отчёт `probe-perTest/reports/mutation-report.json` (SHA-256 `495e…faa4`), лог `probe-perTest.log`.
- **all** — не исполнялся (ранний стоп: `perTest` дал правдоподобный валидный signal по bounded-инструкции). Config есть: `probe-all/stryker-config.json`.
- **off** — не исполнялся (та же причина раннего стопа). Config есть: `probe-off/stryker-config.json`.

**Решение:** рабочий режим = **`perTest` + `test-runner: mtp`**. BLOCKER отсутствует: минимум один разрешённый режим обнаруживает coverage и даёт валидный автоматический signal; исторические 2% не легализованы.

### Журнал
| UTC | phase | rev | iter | event | evidence |
|---|---|---|---|---|---|
| 2026-10-09 08:58 | DO | r=1 | n=1/3 | D151-D01 локальный Stryker 5.0.0 закреплён; baseline build exit0/0warn, pg tests exit0 829/0/0 | `StrykerOutput/d151/r1-n1/{tooling,build-debug,baseline-postgres-tests}.log` |
| 2026-10-09 08:58 | DO | r=1 | n=1/3 | D151-D02 B0 воспроизвёл capture failure, exit124 (timeout); исторические 2% остаются невалидными | `.../historical-reproduction.log` |
| 2026-10-09 08:58 | DO | r=1 | n=1/3 | D151-D03 perTest+mtp валиден (93.22%, coveredBy/killedBy непусты); all/off пропущены (ранний стоп) | `.../probe-perTest/reports/mutation-report.json` |
| 2026-10-09 09:02 | DO | r=1 | n=1/3 | D151-D04 gap-тесты `ExtremeRowRendererUnitTests` добавлены; filtered `~ExtremeRow` exit0 53/0/0 | `.../d04-filtered-tests.log` |
| 2026-10-09 09:03 | DO | r=1 | n=1/3 | D151-D05 root config + verifier; control direction mutant (:81 `" desc"`→`""`) killed 14 assertion failures, restored md5-identical | `.../control.json` |
| 2026-10-09 09:13 | DO | r=1 | n=1/3 | D151-D06 campaign A exit0 100.00%: renderer 97 = K56/S0/NC0/TO3/CE27/IG11 | `.../run-a/reports/mutation-report.json` |
| 2026-10-09 09:23 | DO | r=1 | n=1/3 | D151-D07 campaign B exit0 100.00%; normalized sets equal to A | `.../repro-comparison.json` |
| 2026-10-09 09:25 | DO | r=1 | n=1/3 | D151-D08 verifier self-test+verify exit0; manifest 53 files; product tree clean | `.../{verifier-result,manifest}.json` |

## DO results — D151-D04…D08 (r=1, N=1, rv=1; recorded 2026-10-09 09:25 UTC)

> Appended by `coder`. План выше не изменён. Replan не делался: `r=1`, `N=1`, `rv=1` сохранены.
> `E=StrykerOutput/d151/r1-n1`.

### D04 — gap-тесты (renderer `:34-92`; anchors `:166,:178,:191,:441`)
- Добавлен класс `ExtremeRowRendererUnitTests` в `tests/nextorm.postgres.tests/ExtremeRowNativeSqlGenerationTests.cs`: 9 методов / 15 test cases — `CanRender` пустые keys, integral direct key/group (`short`/`short?`/`int`/`int?`/`long`/`long?`), converter rejection, computed-key rejection, non-integral group rejection; `Render` global `order by ... limit 1`, grouped full boundary `) * from (...) __nextorm_extreme order by ...`, payload-only alias collision, group alias collision, embedded-quote doubling.
- Транзиентный stale-`bin`: прогон `dotnet test tests/nextorm.postgres.tests --no-build` сразу после кампаний дал 45 failed (TypeLoad/MissingMethod — Stryker оставил в `bin/linux/Debug` частично мутированный `nextorm.postgres.dll`). После инкрементального `dotnet build` (1.71s, 0 warnings) — чисто. **Это finding про toolchain, не дефект исходников.**
- Команды и exit: filtered `--filter "FullyQualifiedName~ExtremeRow"` → exit **0**, total **53** / failed **0** / skipped **0**; full project → exit **0**, total **844** / failed **0** / skipped **0**; core filtered (`InMemoryExtremeRowTests|ExtremeRowDialectGuardTests|ExtremeRowDescriptionLazinessTests`) → exit **0**, **39** / 0 / 0. Логи: `d04-build-postgres-tests.log`, `d04-filtered-tests.log`, `final-rebuild.log`, `final-postgres-tests.log`, `final-core-extremerow-tests.log`.

### D05 — config, verifier, control-direction mutant
- Создан repo-local `stryker-config.json` (SHA-256 `ae29d4e3…55b38`): project `src/nextorm.postgres`, test-project `tests/nextorm.postgres.tests`, mutate `**/PostgresExtremeRowRenderer.cs`, `coverage-analysis: perTest`, `test-runner: mtp`, reporters Progress/Html/Json, `thresholds.break: 0` (только чтобы отключить числовой gate).
- Создан verifier `tools/stryker/d151-evidence.py` (SHA-256 `0a4c5b70…bee9`): `control-probe` (md5/sha256 до/после, build/active assertion, restore), `manifest`, `verify` (scope/control/reproducibility/disposition), `self-test` (негативные fixtures: out-of-scope file, пустой отчёт, mismatched sets, missing control, survived control, unresolved survivor).
- Control direction mutant `:81` `" desc"`→`""`: baseline exit **0**/0 failed; active build **compiled** (exit 0), tests exit **2** с **14** assertion failures; restore — `sha256_after == sha256_before`, md5 `efc0cc15…` до и после. Product tree после probe пуст (`git diff --stat -- src/nextorm.postgres`). Evidence: `control.json`, `control-baseline.log`, `control-active.log`, `control-active-build.log`, `control-source.md5.{before,after}`.
- `control-probe` exit **0**; `self-test` exit **0** (`verifier-self-test.log`).

### D06/D07 — campaigns A и B (одна конфигурация)
- C09-A: `timeout 3600 dotnet stryker --config-file stryker-config.json --output "$E/run-a"` → exit **0**, elapsed **9m51s**, score **100.00%**. C09-B идентично → exit **0**, elapsed **10m13s**, score **100.00%**.
- Renderer (`PostgresExtremeRowRenderer.cs`), обе кампании: **97** mutants — **Killed 56 / Survived 0 / NoCoverage 0 / Timeout 3 / CompileError 27 / Ignored 11**. У всех 56 Killed непустые `coveredBy` и `killedBy` (perTest attribution валидна).
- D03-выжившие закрыты: `780` (empty keys), `800` (`" from ("`), `860` (`||` collision predicate), `872` (quote doubling) → Killed. Прежний timeout `857` (`while(!Collides)`) также стал Killed благодаря payload-only collision тесту. Остались 3 Timeout: `858` (`:112`), `867` (`:126`), `869` (`:130`).
- Воспроизводимость (по file+span+status, не по числовым id): normalized sets A==B, Killed/Survived sets равны → `repro-comparison.json`. **Divergence отсутствует.**
- Control direction mutant (`:81` `" desc"`→`""`) — **Killed** в обоих автоматических отчётах.

### Disposition — Survived / NoCoverage / Timeout / CompileError / Ignored
- `Survived = 0`, `NoCoverage = 0` — нечего individually закрывать; JSON подтверждает пустые множества.
- `Timeout = 3`, воспроизводимо в A и B, все в extension derived-alias: `858` `candidate += ""`, `867` `!string.Equals`, `869` `return true`. Классификация: **non-terminating-alias-extension** — мутация делает предикат коллизии всегда истинным, поэтому `while (Collides(...))` не завершается и не может дойти до assertion; корректное поведение пинится `Render_Grouped_*AliasCollision_*`. Это воспроизводимое объяснение, не молчаливое зачтение.
- `CompileError = 27` (renderer) — стандартные несобираемые мутации конкатенации `+/-` и `Count()→Sum()`; кода не исполняют. `Ignored = 11` — mutate-filter exclusions. Ledger: `disposition.json`.

### D08 — ledger / manifest / retention
- `manifest.json`: **53** файла с SHA-256, git HEAD `4acb4772…`, Stryker `5.0.0`, rv=1.
- `verify --evidence "$E" --contract <этот файл> --rv 1` → exit **0**, `passed: true` (`verifier-result.json`).
- Product tree: `git diff --stat -- src/nextorm.postgres` пусто; `git status --short -- src/nextorm.postgres` пусто; `git diff --check` clean (только информационный CRLF-warning по утилитарному `.config/dotnet-tools.json` из D01).
- **Исторические 2% остаются невалидными** (B0: 50 mutants / 2.00%, `native-extreme-row-144-1.md:132-135`): в D02 воспроизведён симптом `coverage capture failed`, `coveredBy` пуст; score не легализуется. Debt anchors сохранены: `native-extreme-row-144-1.md:132-135,209-210`; debt 7 → #151 `:304,:315`.
- Артефакты: `run-a|run-b/reports/mutation-report.{json,html}`, `run-{a,b}.log`, `disposition.json`, `repro-comparison.json`, `control.json`, `manifest.json`, `source-hashes.json`, `verifier-result.json`, `coverage` не пересчитывался (mutation-scope only, targeted coverage не выдаётся за repository coverage).


## DO results — D151-EC-07/EC-09 supporting evidence (r=1, N=1, rv=1; recorded 2026-10-09 14:29 UTC)

> Appended by `coder`. План выше не изменён. Replan не делался: `r=1`, `N=1`, `rv=1` сохранены.
> `E=StrykerOutput/d151/r1-n1`.

### EC-07 — контейнерные PostgreSQL integration tests (C10)
- Предусловие: socket Podman присутствует, `_ping` → `OK`; запуск машины не требовался.
- argv: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~PostgresExtremeRowNativeSpecificTests"`
- exit **0**, elapsed **9s**; total **16** / succeeded **16** / failed **0** / **skipped 0**. PostgreSQL tests реально выполнены (не skipped). Лог `ec07/integration.log`; exit `ec07/integration.exit`; `ec07/EC07-evidence.md`.

### Residual reconciliation — renderer mutants (run-a)
- `src/nextorm.postgres/PostgresExtremeRowRenderer.cs`: Killed **56** / Survived **0** / NoCoverage **0** / Timeout **3** / CompileError **27** / Ignored **11** / RuntimeError **0**; denominator **97**.
- Project-wide (6 файлов в JSON): Killed 56 / Timeout 3 / CompileError **118** / Ignored 737; denominator **914**. Survived/NoCoverage/RuntimeError = 0.
- «27 listed vs 118 total» = renderer-only (27) vs все файлы проекта (118 = renderer 27 + PostgresDialect 67 + PostgresDataContext 20 + PostgresRange 4). Это all-project vs renderer-only, не расхождение.
- CompileError diagnostics присутствуют в JSON (`statusReason = "Mutant caused compile errors"`), 27 строк; по mutator: Arithmetic 10, Equality 6, `Count()→Sum()` 6, `Append()→Prepend()` 5. Полностью `ec07/renderer-compileerrors.json`; counts `ec07/mutant-reconciliation.json`.

### EC-09 — coverage (C11)
- Отклонение: `dotnet-coverage`/`reportgenerator` — repo-local tools (не на PATH); вызваны как `dotnet dotnet-coverage` / `dotnet reportgenerator` с теми же аргументами.
- C11a `dotnet dotnet-coverage collect "dotnet test tests/nextorm.postgres.tests -c Debug" -f cobertura -o "$E/coverage.cobertura.xml" --settings coverage.settings.xml` → exit **0**; tests 844/0/0.
- C11b `dotnet reportgenerator "-reports:$E/coverage.cobertura.xml" "-targetdir:$E/coverage" "-reporttypes:Html;TextSummary"` → exit **0**.
- Targeted scope (не repository coverage): assemblies 3; **line 44.9%** (25577/56879), **branch 41.1%** (13719/33325); `nextorm.postgres` line 77.9%; `PostgresExtremeRowRenderer` **100%**.
- Branch: 85/75 — hard gate только на `main`; на `1.0.9-rc2` это warning-threshold. Mutation-score gate не вводится. Evidence: `ec09/{coverage-collect.log,coverage-collect.exit,reportgenerator.log,reportgenerator.exit,coverage-summary.txt,EC09-evidence.md}`, `coverage/Summary.txt`, `coverage/index.html`.

### Журнал (продолжение)
| UTC | phase | rev | iter | event | evidence |
|---|---|---|---|---|---|
| 2026-10-09 14:27 | DO | r=1 | n=1/3 | D151-EC-07 C10 PG integration exit0 16/0/0 skipped0 (не skip) | `ec07/integration.log` |
| 2026-10-09 14:28 | DO | r=1 | n=1/3 | D151-EC-09 C11 coverage exit0 line44.9%/branch41.1%; PG renderer100% | `ec09/coverage-summary.txt` |
| 2026-10-09 14:28 | DO | r=1 | n=1/3 | Residual reconcile renderer 56/0/0/3/27/11/0 denom97; 118=project-wide CompileError | `ec07/mutant-reconciliation.json` |

### C12/C13 boundary recheck — после EC-07/EC-09
- `python3 tools/stryker/d151-evidence.py self-test` (корректный subcommand; `--self-test` в C12 — plan-level опечатка) → exit **0**, `passed: true`.
- `manifest --evidence "$E" --rv 1` → exit **0**, перегенерирован: **634** файла (coverage HTML добавляет assets), rv=1, HEAD `4acb4772…`. Это заменяет прежнюю запись «53 файла» (та была до EC-07/EC-09); ничего не удалено.
- `verify --evidence "$E" --contract <этот файл> --rv 1` → exit **0**, `passed: true`.
- `git diff --check` clean (только известный CRLF-warning по `.config/dotnet-tools.json` из D01); `git status --short -- src/nextorm.postgres` пусто — продуктовый код этим evidence-блоком не затронут. Evidence: `verifier-self-test.log`, `manifest.log`, `manifest.json`, `verifier-verify.log`, `verifier-result.json`.

## Closure record — documentation/ledger-only (r=1, N=1, n=2, rv=2 supersedes rv=1; recorded 2026-10-09 09:33 UTC)

> Appended by `coder` per the `escalate` decision. Replan не делался: `r=1` сохранён; попытка `n=2`; evidence-contract `rv=2` **supersedes rv=1**, IDs сохранены. **Продуктовый код не меняется** (`src/nextorm.postgres` byte-identical), коммита/push нет. Читаемый ledger вынесен в `StrykerOutput/d151/r1-n1/README.md` (в manifest, 635 files).

### Содержание closure (steps 1–5)

1. **Команда + scope verbatim:** `timeout 3600 dotnet stryker --config-file stryker-config.json --output "$E/run-a"` (и `.../run-b`), tool `dotnet-stryker 5.0.0`; config keys `coverage-analysis: perTest`, `test-runner: mtp`; mutation scope `**/PostgresExtremeRowRenderer.cs`; test scope `tests/nextorm.postgres.tests/nextorm.postgres.tests.csproj`. Причина отсутствия CLI-флага: в 5.0.0 `coverage-analysis` — только ключ `stryker-config.json` (default `perTest`); literal CLI отклоняется `Unrecognized option '--coverage-analysis'`, exit 1 (`probe-*.literal-cli.log`). Подробно — README §1.
2. **Ignored/Timeout/CompileError ledger:** 11 Ignored (`Block removal mutation`, JSON `statusReason = "Removed by block already covered filter"`) — disposition **intentionally excluded**; **исправлена прежняя неточность** `disposition.json` (это не «mutate-filter exclusions outside the renderer», а block-already-covered внутри renderer'а). 3 Timeout `:112/:126/:130` — `statusReason` в артефакте **отсутствует → reason not established**, причина из `disposition.json` (non-terminating-alias-extension). 27 CompileError — `statusReason = "Mutant caused compile errors"`, kinds: Arithmetic 10 / Equality 6 / `Count()→Sum()` 6 / `Append()→Prepend()` 5. Status-таблица: Killed 56 + Timeout 3 + CompileError 27 + Ignored 11 = **97**. Подробно — README §2.
3. **Score wording:** «Killed 56 + Timeout 3 = 59/59 determined mutants; Survived 0, NoCoverage 0; Timeout 3 (listed); Ignored 11 and CompileError 27 accounted separately». Bare «100%» не используется (лог инструмента печатает `100.00 %`, но это не формулировка ledger'а); numeric mutation gate не вводится; исторические 2% остаются явно невалидными.
4. **Debt anchors:** `native-extreme-row-144-1.md:132-135` (B0 50/2.00%), `:209-210` (invalidated disposition), `:304` (debt 7 re-run), `:315` (debt 7 → #151). Сохранены: `git diff --stat -- docs/specs/status/native-extreme-row-144-1.md` пусто (exit 0), `git status --short -- <файл>` пусто; `git diff --stat -- src/nextorm.postgres` пусто; renderer sha256 `97345986…421537` = `source-hashes.json`. Ничего не восстановлено/не затронуто.
5. **REQ/EC mapping:** 18 строк (REQ-01..08, EC-01..10) с цитатой предиката, статусом и артефактом — README §5. Честно отмечено **not established**: планируемые отдельные артефакты `mode-decision.json`, `disposition.md`, `variant-matrix.md`, `responsibility-map.md`, `diff-review.md`, `coverage-mode-validation.json` не создавались (content несёт статус/README); ничего не выдумано.
- **Verifier:** `self-test` exit **0** (`passed: true`); `verify --evidence "$E" --contract <этот файл> --rv 2` exit **0**, `passed: true`, `errors: []` (`verifier-verify.log`, `verifier-result.json`). `manifest --rv 2` перегенерирован: **635** files, HEAD `4acb4772…`.
- **Product tree clean:** `git status --short -- src/nextorm.postgres` пусто; renderer byte-identical. Исторические 2% не легализованы.

### Журнал (closure)
| UTC | phase | rev | iter | event | evidence |
|---|---|---|---|---|---|
| 2026-10-09 09:33 | DO→closure | r=1 | n=2/3 | documentation/ledger-only closure: README ledger + REQ/EC 18 rows; manifest 635; self-test/verify exit0 passed; renderer byte-identical | `StrykerOutput/d151/r1-n1/{README.md,manifest.json,verifier-self-test.log,verifier-verify.log,verifier-result.json}` |

## Completion of the rv=2 evidence contract — plan-named artifacts (r=1, N=1, n=2, rv=2; recorded 2026-10-09 09:38 UTC)

> Appended by `coder`. Documentation/ledger-only per the `escalate` decision. Replan не делался: `r=1`, `N=1`, `n=2`, `rv=2` сохранены, IDs не менялись. Продуктовый код не меняется (`src/nextorm.postgres` byte-identical), коммита/push нет. `E=StrykerOutput/d151/r1-n1`.

Six plan-named artifacts that were previously "not established" are now produced from existing evidence (no new campaigns):

1. **`E/mode-decision.json`** — D03 decision: `coverage-analysis: perTest` + `test-runner: mtp`; why 5.0.0 has no `--coverage-analysis` CLI flag (config-only key; literal CLI exit 1); perTest probe result (exit **0**, coverage captured, coveredBy/killedBy non-empty); `all`/`off` prepared-but-not-executed (not needed after valid perTest), their support **not established**.
2. **`E/disposition.md`** — per-mutant ledger: Ignored **11** (`statusReason="Removed by block already covered filter"` → intentionally excluded), Timeout **3** `:112/:126/:130` (artifact `statusReason` absent → **reason not established**; cause non-terminating-alias-extension), CompileError **27** (`statusReason="Mutant caused compile errors"`; Arithmetic 10 / Equality 6 / `Count()→Sum()` 6 / `Append()→Prepend()` 5), Killed **56**; table sums to **97**; Survived **0** / NoCoverage **0**.
3. **`E/variant-matrix.md`** — REQ-06 matrix (Min/Max, applicable NULL cases, composite order, empty suppression, winning-row identity) closed as test/guard with `file:line` anchors and the renderer-vs-core responsibility boundary.
4. **`E/responsibility-map.md`** — renderer-owned vs core-owned behavior with `file:line`.
5. **`E/diff-review.md`** — D151 change-set review; confirms no `src/**` product change.
6. **`E/coverage-mode-validation.json`** — perTest yields valid `coveredBy`/`killedBy` in campaigns A and B; branch `1.0.9-rc2` ≠ `main` → 85/75 warning only; no numeric mutation gate.

`E/README.md` §5 updated: **REQ-06, EC-03, EC-06, EC-07, EC-09 now PASS** (their artifacts exist); all other rows unchanged. The only residual "not established" item is the per-mutant artifact `statusReason` of the three Timeout mutants (kept explicit, not invented).

- `manifest --rv 2` regenerated: **641** files, HEAD `4acb4772…`.
- `self-test` exit **0**, `passed: true`; `verify --evidence "$E" --contract <этот файл> --rv 2` exit **0**, `passed: true`, `errors: []` (`verifier-self-test.log`, `verifier-verify.log`, `verifier-result.json`).
- Product tree clean: `git diff --stat -- src` и `git status --short -- src` пусты; renderer sha256 `97345986…421537` неизменён.

### Журнал (completion)
| UTC | phase | rev | iter | event | evidence |
|---|---|---|---|---|---|
| 2026-10-09 09:38 | DO→closure | r=1 | n=2/3 | rv=2 evidence contract completed: 6 plan-named artifacts produced; README §5 REQ-06/EC-03/06/07/09 → PASS; self-test/verify exit0 passed; manifest 641; renderer byte-identical | `StrykerOutput/d151/r1-n1/{mode-decision.json,disposition.md,variant-matrix.md,responsibility-map.md,diff-review.md,coverage-mode-validation.json,README.md,manifest.json,verifier-result.json}` |

## DO results — CHECK digest (r=2, N=1, n=1; rv=2 preserved; recorded 2026-10-09 09:45 UTC)

> Appended by `coder`. Documentation-only per D151 `r=2`: created `StrykerOutput/d151/r1-n1/check-digest.md` (523 lines, CRLF) with verbatim raw facts so CHECK can certify without opening the evidence files. No product/test change; no Stryker/build/test/campaign re-run; no commit/push. Plan revision advanced to `r=2`, cycle `N=1`, attempt `n=1`; evidence contract `rv=2` preserved (not superseded, IDs kept). `docs/specs/status/native-extreme-row-144-1.md` is a read-only source and was not modified. `manifest.json` regenerated to include the digest.

### Журнал (CHECK digest)
| UTC | phase | rev | iter | event | evidence |
|---|---|---|---|---|---|
| 2026-10-09 09:45 | DO | r=2 | n=1/3 | CHECK digest `check-digest.md` written (523 lines): verbatim REQ-01/05/06/08 + EC-01/02/03/06/07/10 facts; manifest regenerated to include it; renderer byte-identical | `StrykerOutput/d151/r1-n1/{check-digest.md,manifest.json}` |

## STOP — terminal `incomplete` (r=1 exhausted n=1/2/3 + r=2 n=1; rv=2 preserved; recorded 2026-10-09 UTC)

> Appended by `coder` per the `escalate` decision. Replan не делался: `r=1` exhausted at `n=3`, затем `r=2 n=1`; evidence contract `rv=2` **preserved** (не superseded; IDs сохранены). Продуктовый код не менялся (`src/**` clean). Коммита D151-изменений нет; они сохранены патчем. `docs/specs/status/native-extreme-row-144-1.md` не изменялся.

- **status: INCOMPLETE (terminal STOP).** `escalate` постановил STOP: никакого `r=3`, никакого повторного автоматического прогона/CHECK не разрешено.
- **Certification blocker (не продуктовый дефект):** CHECK не смог сертифицировать evidence-completeness — check-роль не смогла воспроизвести row-level факты из агрегированного brief (aggregated narrative), а не из сырых артефактов. Все вердикты CHECK по tracer'ам — **`fail-by-evidence`**: `r=1 n=1`, `r=1 n=2`, `r=1 n=3`, `r=2 n=1`. Продуктовый дефект **не установлен**.
- **Substantive state = valid automatic reproducible campaign** (не отбрасывается, не выдаётся за PASS):
  - renderer `PostgresExtremeRowRenderer.cs`, denominator **97**: **Killed 56 / Survived 0 / NoCoverage 0 / Timeout 3 / CompileError 27 / Ignored 11**.
  - Campaign A и B одной config (perTest + `test-runner: mtp`): нормализованные Killed/Survived множества **идентичны** (`repro-comparison.json`); оба exit 0.
  - Control direction mutant `:81` (`" desc"` → `""`): baseline exit 0; активные тесты падают **14** assertion failures (не compile/discovery); источник восстановлен md5/sha256-identical; в обоих автоматических отчётах — **Killed**.
  - PostgreSQL container `PostgresExtremeRowNativeSpecificTests`: exit 0, **16 / 0 / 0** (skipped 0).
  - Verifier `self-test` + `verify --rv 2`: exit 0, `passed: true` (`verifier-result.json`).
  - Product tree clean: `git status --short -- src` пусто; renderer byte-identical (`source-hashes.json`).
- **Patch (D151 test/config/tooling changes, uncommitted):** `docs/specs/status/rc2-151-evidence/D151-STOP-incomplete.patch` — `tests/nextorm.postgres.tests/ExtremeRowNativeSqlGenerationTests.cs`, `stryker-config.json`, `tools/stryker/d151-evidence.py`, `.config/dotnet-tools.json`. Статус-файлы и `StrykerOutput/` evidence **исключены**. Изменения оставлены в рабочем дереве, не откатывались (`git checkout` не выполнялся).
- **Historical debt preserved:** `native-extreme-row-144-1.md:132-135` (B0 50 mutants / 2.00%, invalidated), `:209-210`, `:304,:315` (debt 7 → #151). Исторические 2% остаются явно невалидными.
- **Issue #151 остаётся OPEN.** Задача #151 — Stryker mutation campaign (не продуктовый фикс).
- **Next allowed step:** none for D151.

### Журнал (terminal STOP)
| UTC | phase | rev | iter | event | evidence |
|---|---|---|---|---|---|
| 2026-10-09 09:48 | STOP | r=2 | n=1/3 | terminal `incomplete`: CHECK evidence-certification `fail-by-evidence` (r1 n1/2/3 + r2 n1); no product defect; no r=3/re-run; campaign valid (renderer 97 = 56/0/0/3/27/11; A/B identical; control `:81` killed+restored; PG 16/0/0; verifier `passed:true` rv=2; product clean); patch preserved; #151 OPEN | `docs/specs/status/rc2-151-evidence/D151-STOP-incomplete.patch` |

## DO — freeze the rv1 row-level ledger (r=1, N=1, n=1/3; recorded 2026-10-10 02:03 UTC)

> Appended by `coder`. DO for T151 at `r=1` `n=1/3`: froze the raw-artifact mutation-evidence bundle into a row-level ledger so CHECK can certify from raw files. No new Stryker campaign; no mutation re-run; no product/test C# change; no commit/push. The historical STOP block above is preserved intact.

- **Provenance gate — PASS.** `git diff e67aa5f8..HEAD -- src/nextorm.postgres/PostgresExtremeRowRenderer.cs stryker-config.json tests/nextorm.postgres.tests/ExtremeRowNativeSqlGenerationTests.cs` is empty; every entry of `source-hashes.json` matches current HEAD (renderer `97345986…421537`, config `ae29d4e3…`, test `0994a86c…`, verifier `0a4c5b70…`, tool manifest `2849695f…`). `campaign-head.txt` records `4acb4772`, the pre-D151 base; the D151 files were committed in `e67aa5f8` (a descendant of `4acb4772`), so the campaign is representative. Freeze HEAD `8f48d3aa…` (brief expected `a956598a`; two later commits landed — inputs unchanged).
- **Frozen row ledger:** `artifacts/pdca/D151/rv1/manifest.json` — `required_rows` `R-PROV,R-TEST,R-COV,R-REPRO,R-CONTROL,R-COMPARE,R-DISPOSITION,R-RETENTION,R-NEGATIVE,R-CHECK`, one row per id, all `met`; `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D151/rv1/manifest.json` exit **0**.
- **Hash inventory:** `artifacts/pdca/D151/rv1/row-hashes.json` (39 files: every referenced immutable raw artifact + renderer/config/test/verifier script/tool manifest + both campaign reports + historical B1). The verifier's own mutable receipts (`verifier-result.json`, `verifier-verify.log`, `verifier-self-test.log`, `verifier-result-rv2.backup.json`) are **excluded** as helper outputs captured separately; `sha256sum` recheck 39/39 matched, 0 mismatched (`artifacts/pdca/D151/rv1/hash-recheck.log`).
- **Provenance record:** `artifacts/pdca/D151/rv1/provenance.md` (trees, tool 5.0.0, exact campaign command, perTest+mtp, mutation scope, renderer 97 summary, historical-2% invalid statement, re-run steps).
- **Helper (history preserved):** `verifier-result.json` backed up to `verifier-result-rv2.backup.json`; `self-test` exit **0** (`passed:true`); `verify --evidence StrykerOutput/d151/r1-n1 --contract docs/specs/status/rc2-151-stryker-mutation-1.md --rv 1` exit **0** (`passed:true`, `errors:[]`); logs `artifacts/pdca/D151/rv1/{self-test.log,verify.log}`. No helper edit.
- **Branch green:** `dotnet build nextorm.slnx -c Debug` exit **0**, **0 Warning(s) / 0 Error(s)** (`artifacts/pdca/D151/rv1/build.log`); `git diff --check` exit **0**.

### Журнал (DO rv1 freeze)
| UTC | phase | rev | iter | event | evidence |
|---|---|---|---|---|---|
| 2026-10-10 02:03 | DO | r=1 | n=1/3 | rv1 row-level ledger frozen; provenance gate PASS (`e67aa5f8..HEAD` empty, all source-hashes match); manifest exit0 (10/10 rows met); self-test/verify exit0; build 0W/0E; no campaign re-run; no commit | `artifacts/pdca/D151/rv1/{manifest.json,row-hashes.json,provenance.md,self-test.log,verify.log,build.log}` |
| 2026-10-09 21:08 | DO | r=1 | n=1/3 | CHECK-gather consistency fix: `row-hashes.json` recomputed over immutable raw evidence only (39 files; excluded the 4 verifier receipts) → `sha256sum` 39/39 matched, 0 mismatched; status header reconciled (evidence contract rv1 pinned at manifest 10/10, plan_state DO complete); `manifest`-validate/self-test/verify exit0; no campaign re-run; no commit | `artifacts/pdca/D151/rv1/{row-hashes.json,hash-recheck.log,manifest-validate.log,provenance.md,self-test.log,verify.log}` |

## DO — accessible frozen re-gather (r=1, N=1, n=1/3; recorded 2026-10-10 02:12 UTC)

> Appended by `coder`. CHECK (medium) returned **FAIL** on evidence certification: every row's evidence pointed under `StrykerOutput/**`, which is outside CHECK's permitted read locations; CHECK also requires complete per-mutant entries and comparison predicates directly inspectable. This re-gather publishes an **accessible, byte-identical frozen bundle + complete row-level extracts** under the permitted `artifacts/` root, adds an auditable frozen-path mapping, and repoints the manifest rows. It **does not** change acceptance criteria, does not weaken obligations, leaves the historical STOP closed-incomplete, re-runs no campaign, and makes no commit.

- **Byte-identical frozen bundle:** `artifacts/pdca/D151/rv1/raw/` — **33** files copied with `shutil.copy2` preserving relative paths (two `run-a|run-b/reports/mutation-report.json`, `run-a.log`/`run-b.log`, `control.json`, `control-baseline.log`, `control-active.log`, `final-postgres-tests.log`, `d04-filtered-tests.log`, `final-core-extremerow-tests.log`, `repro-comparison.json`, `disposition.json`, `disposition.md`, `manifest.json`, `README.md`, `check-digest.md`, `mode-decision.json`, `variant-matrix.md`, `responsibility-map.md`, `diff-review.md`, `coverage-mode-validation.json`, `source-hashes.json`, `baseline-b1.sha256`, `campaign-head.txt`, `environment.txt`, `ec07/**`, and the historical B1 `campaign/reports/mutation-report.{json,html}` under `raw/campaign/reports/`). The three test logs are included because manifest row **R-TEST** references them; the helper's mutable `verifier-*` receipts are **not** copied.
- **Frozen-path mapping:** `artifacts/pdca/D151/rv1/frozen-path-mapping.json` — **33** entries `{original, frozen, sha256}`; `sha256(original) == sha256(frozen)` asserted at copy time (byte-identical).
- **Row-level extracts (all directly readable, CRLF):** `renderer-mutants-a.csv` / `renderer-mutants-b.csv` — **97** rows each with `id,file,line,column,endLine,endColumn,mutatorName,replacement,status,statusReason,coveredByCount,killedByCount,killedByTests`; `mutant-comparison.json` — **97** entries keyed by `file:startLine:startCol-endLine:endCol|mutatorName|replacement` (not numeric id), **matched 97 / mismatched 0**; `disposition-all.csv` — **97** rows with per-mutant `status` + `dispositionReason` (3 Timeouts `:112/:126/:130` = explicit `non-terminating-alias-extension` + span, 27 CompileError `statusReason="Mutant caused compile errors"`, 11 Ignored `"Removed by block already covered filter"`, 56 Killed with killing tests); `control-extract.json` — baseline exit 0 / failed 0, active exit 2 / failed 14 / assertion_failure true, restored true, `sha256_before==sha256_after=97345986…`, direction mutant `:81 col61-68 "\" desc"→"\"\""` (replacement `""`), status Killed in A and B.
- **Manifest repointed:** every evidence `path` in `artifacts/pdca/D151/rv1/manifest.json` now points under `artifacts/` (raw copies / extracts / `check-rv1/verifier-result.json`); each item keeps its `original_path`; originals are enumerated in `frozen-path-mapping.json`. `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D151/rv1/manifest.json` → exit **0** (10/10 rows).
- **Hash inventory:** `row-hashes.json` recomputed over the accessible files only — **46** entries (33 raw copies + 5 extracts + `frozen-path-mapping.json` + `verify.log`/`self-test.log` + 5 source anchors); `sha256sum -c row-hashes.sha256` → **46/46 OK**, exit **0** (`hash-recheck.log`).
- **Helper:** `self-test` exit **0** (`passed:true`); `verify --evidence StrykerOutput/d151/r1-n1 --contract docs/specs/status/rc2-151-stryker-mutation-1.md --rv 1` exit **0** (`passed:true`, `errors:[]`). `check-rv1/verifier-result.json` receipt updated: `hashes_ok=true`, `hashes_matched/hashes_total=46/46`, `accessible_root="artifacts/pdca/D151/rv1/raw"`.
- **Branch green:** `dotnet build nextorm.slnx -c Debug` exit **0**, 0 Warning(s) / 0 Error(s) (`build.log`); `git diff --check` exit **0**.
- **Acceptance unchanged / STOP preserved:** no campaign re-run, no product/test C# change, no acceptance weakening; historical STOP above remains **closed-incomplete**; no commit/push.

### Журнал (accessible re-gather)
| UTC | phase | rev | iter | event | evidence |
|---|---|---|---|---|---|
| 2026-10-10 02:12 | DO | r=1 | n=1/3 | accessible frozen re-gather: 33 byte-identical raw copies + mapping; 97-row extracts (A/B/comparison/disposition/control); manifest rows repointed under artifacts/ (validator exit0); row-hashes 46/46 sha256 matched; self-test/verify exit0; check receipt accessible_root set; build 0W/0E; STOP preserved; no campaign re-run; no commit | `artifacts/pdca/D151/rv1/{raw,frozen-path-mapping.json,renderer-mutants-a.csv,renderer-mutants-b.csv,mutant-comparison.json,disposition-all.csv,control-extract.json,manifest.json,row-hashes.json,row-hashes.sha256,hash-recheck.log,manifest-validate.log,self-test.log,verify.log,build.log}` |

## DO — contract-complete per-mutant ledger (re-gather #2) (r=1, N=1, n=1/3; recorded 2026-10-09 21:18 UTC)

> Appended by `coder`. CHECK rv1 (2nd pass) returned **FAIL** on one remaining **evidence-completeness** gap (accessibility is closed): `artifacts/pdca/D151/rv1/renderer-mutants-{a,b}.csv` + `disposition-all.csv` are status tables, not the **contract-complete per-mutant ledger** required by the rv1 contract (plan line 75). This pass publishes `mutant-ledger.json` covering **all 97 renderer mutants in BOTH runs** (194 entries) with report path+sha256, RFC 6901 JSON pointer, file-qualified stable id, location/span, mutation/status, original `coveredBy`/`killedBy` and a per-entry disposition. It **re-runs no campaign**, changes no product/test C#, does not weaken acceptance, keeps the historical STOP **closed-incomplete**, and makes **no commit**.

- **Contract-complete ledger:** `artifacts/pdca/D151/rv1/mutant-ledger.json` — top-level `renderer_file` + `runs.{a,b}` (each `report_path`, `report_sha256`, 97 `entries`) + `comparison` (97 rows). Entry fields: `id`, `stable_id` (`file:span|mutator|replacement`), `json_pointer` (+ `json_pointer_ordinal`), `file`, `span`, `mutator`, `replacement`, `status`, `statusReason`, `coveredBy` (verbatim), `killedBy` (verbatim), `killedByTests`, `disposition`, `dispositionReason`. All 194 pointers resolve against the raw reports by id.
- **Report anchors:** run A `raw/run-a/reports/mutation-report.json` sha256 `e245e785c60d774525f9e8be4be6972d77de8e1718615e1c4d08b7f8ed41fcae`; run B `raw/run-b/reports/mutation-report.json` sha256 `678090336b2d0a619c323fbb5f415ab9aeeea9569e4e2f134301fe95f2dc78fe` (== `row-hashes.json`).
- **Dispositions (run A; run B identical):** Killed **56** → `killed` + killing test names; Timeout **3** (`:112`, `:126`, `:130`) → `non-terminating-alias-extension` with explicit cause + span; CompileError **27** → `compile-error` + `statusReason`; Ignored **11** → `block-already-covered` + `statusReason`; Survived **0** / NoCoverage **0**. Timeout/CompileError/Ignored are recorded separately from Killed.
- **A/B comparison:** 97 rows by `file:span|mutator|replacement` (not numeric id) — `matched 97 / mismatched 0`.
- **Human summary:** `artifacts/pdca/D151/rv1/mutant-ledger.md` — dispositions by status; states Timeout/CompileError/Ignored are separate from Killed.
- **Manifest repointed:** `R-DISPOSITION` and `R-CHECK` (plus `R-COV`/`R-REPRO`/`R-COMPARE`) now carry `mutant-ledger.json` (+ `.md` where applicable) as evidence; other rows unchanged. `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D151/rv1/manifest.json` → exit **0**.
- **Hash inventory:** `row-hashes.json` recomputed to include the new ledger files — **48** entries; `sha256sum -c row-hashes.sha256` → **48/48 OK**, exit **0** (`hash-recheck.log`).
- **Helper:** `self-test` exit **0** (`passed:true`, `failures:[]`); `verify --evidence StrykerOutput/d151/r1-n1 --contract docs/specs/status/rc2-151-stryker-mutation-1.md --rv 1` exit **0** (`passed:true`, `errors:[]`). `check-rv1/verifier-result.json` updated: `ledger_entries=194`, `ledger_complete=true`, `hashes_matched/hashes_total=48/48`.
- **Branch green:** `dotnet build nextorm.slnx -c Debug` exit **0**, 0 Warning(s) / 0 Error(s) (`build.log`); `git diff --check` exit **0**.
- **Acceptance unchanged / STOP preserved:** no campaign re-run, no product/test C# change, no acceptance weakening; historical STOP above remains **closed-incomplete**; no commit/push.

### Журнал (DO re-gather #2)
| UTC | phase | rev | iter | event | evidence |
|---|---|---|---|---|---|
| 2026-10-09 21:18 | DO | r=1 | n=1/3 | re-gather #2: contract-complete per-mutant ledger (194 entries = 97x2, report sha `e245e785`/`67809033`, RFC6901 pointers, verbatim coveredBy/killedBy, dispositions Killed56/Timeout3/CompileError27/Ignored11, A/B matched97); `mutant-ledger.md`; manifest R-DISPOSITION/R-CHECK (+R-COV/R-REPRO/R-COMPARE) repointed (validator exit0); row-hashes 48/48 matched; self-test/verify exit0; check receipt ledger_entries194/ledger_complete/48-48; build 0W/0E; STOP preserved; no campaign re-run; no commit | `artifacts/pdca/D151/rv1/{mutant-ledger.json,mutant-ledger.md,manifest.json,row-hashes.json,row-hashes.sha256,hash-recheck.log,self-test.log,verify.log,build.log}` + `artifacts/pdca/D151/check-rv1/verifier-result.json` |

---

## P151-r2-ledger-raw — r=2 (n=1, rv2 supersedes rv1)

> Appended by `coder` (DO stage A, `r=2, n=1, N=1`; evidence contract `rv=2` **supersedes** `rv=1`). This block is the r=2 plan and is the operative contract; the historical STOP block and all earlier DO/closure records above remain intact and separate. `E=artifacts/pdca/D151/rv1`; `U=artifacts/pdca/D151/rv2`; `S=docs/specs/status/rc2-151-stryker-mutation-1.md`. No new Stryker campaign; no product/renderer/config/test change; no commit/push.

### State
- `r=2, n=1, N=1`; `contract rv=2 (rv1 superseded)`; collection `rc2-final`; branch `1.0.9-rc2`; HEAD `8f48d3aa`.

### Goal
- Close defect 1 (per-entry report binding) and defect 2 (independent ledger→raw verification); no new campaign.

### Acceptance criteria (retained from rv1; unchanged, not weakened)
- AC1 — Two independently executed, renderer-only campaigns use pinned Stryker 5.0.0, `perTest` + `mtp`, finish exit 0.
- AC2 — Raw reports demonstrate valid coverage for executable mutants: nonempty `coveredBy`, preserved `killedBy`, no Survived or NoCoverage.
- AC3 — A/B normalized mutant identities and dispositions match exactly; every report entry retained and individually classified.
- AC4 — Positive control removes `" desc"` and produces an attributable assertion failure; baseline passes and original source bytes are restored.
- AC5 — CHECK opens raw evidence by frozen path, verifies SHA-256 hashes and all row-level predicates, and produces an independent exit-0 verification receipt.

### Supersession
- `rv=2` supersedes `rv=1`; the rv1 row IDs and obligations are retained and new variants receive new IDs. The rv1 bundle `E` stays byte-identical and is the frozen source of the raw reports and source anchors.

### DO tasks (r=2)
- D151.2.1 — migrate all 194 entries from `E/mutant-ledger.json` into `U/mutant-ledger.json`, adding per-entry `report_path`/`report_sha256`; keep run-level fields and all original values; record predecessor path + full sha256 in `U/provenance.md` as a supersession addendum; leave `E` intact.
- D151.2.2 — add `verify-ledger` to `tools/stryker/d151-evidence.py` (or extend `cmd_verify`) that consumes the contract, frozen `E/raw`, `source-hashes.json`/`frozen-path-mapping.json` and the ledger, and independently re-derives identities WITHOUT calling ledger-generation code; remove/repurpose the unused `verify --contract`.
- D151.2.3 — compare verbatim `coveredBy`, `killedBy`, `status`, `statusReason`, location, mutator, replacement, `stable_id`; bind per-entry path/hash to run metadata; check raw vs `source-hashes.json`/`frozen-path-mapping.json`/run hashes, ledger vs `row-hashes.json`; assert A=97/B=97, per-run statuses 56/0/0/3/27/11, matched=97/unmatched=0, control `:81` Killed both; nonzero exit on any divergence.
- D151.2.4 — emit `U/ledger-verify.log` (one line per entry: stable_id/run/pointer/OK/canonical-node sha256; + totals + exit) and lossless `U/raw-slices-{a,b}.jsonl` (verbatim raw mutant nodes, ≤~2 KB/line where possible, never truncated).
- D151.2.5 — negative control: corrupt one ledger entry's `killedBy`/`json_pointer` in a `/tmp` copy and require `verify-ledger` to exit nonzero identifying a raw-field divergence (not merely a hash error); retain `U/negative/ledger-verify.log`, receipt and the corrupted-fixture sha256 (the `/tmp` fixture is not committed and not placed in the frozen tree).
- D151.2.6 — extend `self-test` with the rv2 negative fixture cases; CRLF on all edited text files; `git diff --check` exit 0.

### rv2 row list
- Retained: R-PROV, R-TEST, R-COV, R-REPRO, R-CONTROL, R-COMPARE, R-DISPOSITION, R-RETENTION, R-NEGATIVE, R-CHECK.
- Added: **R-ENTRY-BIND** (per-entry report path/hash bound to the run metadata), **R-RAW-TRACE** (verbatim raw mutant node per entry via `raw-slices-{a,b}.jsonl`), **R-LEDGER-NEGATIVE** (corrupted ledger → nonzero raw-field divergence), **R-LOOP** (independent re-derivation loop over all 194 entries), **R-BOUNDARY** (scope: only `tools/stryker/**` + `artifacts/pdca/D151/**` + `S`; no campaign, no product change).

### CHECK re-gather budget
- At most **2 targeted re-gather passes** for `r=2`; each pass is bounded to two specific missing-evidence lookups. A missing report alone does not justify a new revision; P1 rows and conditional obligations are applied without downgrade.

### Boundaries
- Only `tools/stryker/**` and `artifacts/pdca/D151/**` (plus `S`). Historical rv1 artifacts untouched. No product/renderer/config/test change; no new Stryker campaign; never `git add -A`; no commit/push.

### Журнал (DO r=2)
| UTC | phase | rev | iter | event | evidence |
|---|---|---|---|---|---|
| 2026-10-09 21:29 | DO | r=2 | n=1/3 | r=2 stage A: rv1 ledger migrated to `U/mutant-ledger.json` (194 entries + per-entry `report_path`/`report_sha256`; predecessor sha `c6f2682c`); `verify-ledger` added (rv1 `verify --contract` removed); positive exit0 entries194 A/B97 matched97 unmatched0 statuses56/0/0/3/27/11 control:81 killed both; negative (`--ledger /tmp/...` killedBy corruption) exit1 raw-field divergence; self-test exit0; `E` untouched | `artifacts/pdca/D151/rv2/{mutant-ledger.json,provenance.md,ledger-verify.log,raw-slices-a.jsonl,raw-slices-b.jsonl,verifier-result.json,negative/{ledger-verify.log,verifier-result.json,receipt.md}}` |
| 2026-10-09 21:34 | DO | r=2 | n=1/3 | r=2 stage B: helper `manifest --freeze` (producer verification in-process) atomically wrote `U/row-hashes.json` (44 files; manifest.json excluded, no self-hash) + `U/manifest.json` (15/15 rows met; `validate_inner_loop.py manifest` exit0); `inner-loop-check` exit0 (invokes validator CLI `manifest`); `verify-ledger --out C` exit0 entries194 matched97 unmatched0; negative-control exit1 raw-field divergence; build 0W/0E; renderer sha256 unchanged; no product/campaign change; no commit | `artifacts/pdca/D151/rv2/{row-hashes.json,manifest.json,provenance.md,build.log,git-diff-e67aa5f8.log,renderer-hash.log,git-diff-check.log}` + `artifacts/pdca/D151/check-rv2/{inner-loop-manifest.json,inner-loop-check.log,verifier-result.json,ledger-verify.log}` |

## DO — CHECK re-gather pass 1 (r=2, N=1, n=1/3; recorded 2026-10-10 02:40 UTC)

> Appended by `coder`. Targeted evidence re-gather for the open CHECK rows of `r=2 n=1`. No new campaign; no product/config/test change; only `tools/stryker/**` + `artifacts/pdca/D151/**` (+ this status). Evidence `E=artifacts/pdca/D151/rv1`, `U=artifacts/pdca/D151/rv2`, `C=artifacts/pdca/D151/check-rv2`.

- **Hash attestation corrected (R-RETENTION/R-CHECK).** `verify-ledger` now reports the **current** ledger: `ledger_sha256_detail` = `7e65ed7e…fead4c` (`U/mutant-ledger.json`) and `ledger_sha256_ok` compares it to the `U/row-hashes.json` pin; the rv1 predecessor hash is retained explicitly as `predecessor_ledger_sha256` = `c6f2682c…db6d97` (also in `U/provenance.md`). `verify-ledger --out C` → exit **0**, `passed:true`, errors 0, entries 194, matched 97 / unmatched 0; `C/verifier-result.json` now carries the current hash. `manifest --freeze` seeds the in-process check with the freshly computed current-ledger hash.
- **Hash-check receipt:** `U/row-hashes.sha256` (44 entries) + `U/hash-check.log` = actual `sha256sum -c` output → **44/44 OK**, exit 0.
- **Current self-test receipt (R-NEGATIVE):** `U/self-test-rv2.log` — `self-test` now emits per-case outcomes: **11/11 cases passed**, exit 0, helper sha256 `e86d9522…a6434f`; includes the rv2 forged `killedBy` / `json_pointer` / `stable_id` / `report_sha256` fixtures plus the rv1 scope/repro/control/disposition fixtures.
- **Manifest repointed:** `R-NEGATIVE` → `U/self-test-rv2.log`; `R-RETENTION` includes `U/hash-check.log`; `R-CHECK` → current `C/verifier-result.json`. Freeze re-run: `U/row-hashes.json` 44 files (both manifests excluded), `U/manifest.json` 15/15 rows `met`; `validate_inner_loop.py manifest U/manifest.json` exit **0**; `C/inner-loop-manifest.json` refreshed from `U/manifest.json`; `inner-loop-check` exit **0**.
- **Build:** not run — no compiled input changed (`src/**`, `tests/**` untouched); only `tools/stryker/**`, `artifacts/pdca/D151/**` and this status. `git diff --check` exit **0**. Only `tools/stryker/d151-evidence.py` and this status are tracked changes. No commit/push; collection status T151 left `in-progress`.

### Журнал (CHECK re-gather pass 1)
| UTC | phase | rev | iter | event | evidence |
|---|---|---|---|---|---|
| 2026-10-10 02:40 | DO | r=2 | n=1/3 | CHECK re-gather pass 1: `verify-ledger` reports current ledger hash `7e65ed7e…` (rv2) vs `U/row-hashes.json`, predecessor `c6f2682c…` kept separate; `hash-check.log` 44/44 OK; `self-test-rv2.log` 11/11 cases exit0 helper `e86d9522…`; manifest R-NEGATIVE/R-RETENTION repointed + freeze (44 files, 15/15 met, validator exit0); `C` receipt + inner-loop-check exit0; build not run (no compiled input); no commit | `artifacts/pdca/D151/rv2/{manifest.json,row-hashes.json,self-test-rv2.log,hash-check.log,verifier-result.json}` + `artifacts/pdca/D151/check-rv2/{verifier-result.json,inner-loop-check.log}` |

## ACT — finalize r=2 (N=1, n=1/3; recorded 2026-10-10 02:45 UTC)

> Appended by `coder`. CHECK returned **PASS** at `r=2`/`rv2` (**15/15** required rows met). This block finalizes the cycle: `plan_state` **done/verified**; **next step: none**; issue **#151 closed**. The historical STOP block above remains intact and clearly separated. No campaign re-run; no product/test C# change.

- **CHECK verdict:** PASS — `N=1`, `r=2`, evidence contract `rv2`; `artifacts/pdca/D151/rv2/manifest.json` 15/15 rows `met`; `verify-ledger` exit 0 (entries 194, matched 97 / unmatched 0); negative control exit 1 (raw-field divergence); A/B normalized sets identical; control `:81` Killed in both with byte-exact source restore.
- **Task commit:** `#151` — `tools/stryker/d151-evidence.py` + this status file (explicit paths only).
- **Issue:** #151 **closed** — https://github.com/AlexeyShirshov/nextorm/issues/151.
- **Non-blocking documentation fix:** `artifacts/pdca/D151/rv2/provenance.md` helper hash corrected to current `e86d9522…a6434f` (gitignored; no commit impact; other frozen evidence untouched).

### Журнал (ACT)
| UTC | phase | rev | iter | event | evidence |
|---|---|---|---|---|---|
| 2026-10-10 02:45 | ACT | r=2 | n=1/3 | CHECK **PASS** (r=2, rv2, 15/15 rows); `plan_state` done/verified; next step none; issue #151 closed; provenance helper-hash `e86d9522…a6434f` corrected (gitignored); no campaign re-run | `docs/specs/status/rc2-151-stryker-mutation-1.md`, `artifacts/pdca/D151/rv2/manifest.json`, `artifacts/pdca/D151/rv2/provenance.md` |
