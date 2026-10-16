# #238 — исправление глобального PDCA: запрет ложных audit/provenance-гейтов

## 0. Метаданные

- **Дата:** 2026-10-16.
- **GitHub issue:** #238 — https://github.com/AlexeyShirshov/nextorm/issues/238 — проверено на GitHub.
- **Milestone:** **1.0.9-rc3** (номер 4) — фактическое проверенное значение на 2026-10-16.
- **Статус:** направление и scope согласованы в brainstorming **для всех трёх базовых скиллов**
  (`pdca`, `pdca-coder`, `pdca-dotnet`). **Письменная спецификация APPROVED пользователем 2026-10-16**;
  финальный независимый builder-check выполнен, ничего существенного не осталось. Scope all3 **без
  изменений**. Implementation plan **не создан/не approved**, разрешения реализации **нет** (NOT
  authorized).
- **Тип изменения:** исправление **SKILL** во всех трёх базовых global contracts, а **не** продолжение
  T232, не historical audit и не новая provenance-система.
- **Путь документа:** `docs/specs/design/issue-238-pdca-audit-gate-boundaries.md` — сознательно
  внутренний `docs/specs/**`; публичные docs на него **не** ссылаются.

## 1. Намерение и case

### 1.1 Намерение
Исправить **глобальные правила всех трёх базовых скиллов** (`pdca`, `pdca-coder`, `pdca-dotnet`),
чтобы ложные `incomplete`/STOP из-за дополнительных audit/provenance-требований больше не повторялись
в будущих циклах. Это **process fix**, а не закрытие #238 как `not planned`.

### 1.2 Case: T232 (продукт был зелёный)
В финале T232 продукт был зелёный: `dotnet build` **0 errors**, все 8 suites **0 failures**, required
manifest **54/54 met**. CHECK всё равно вернул `incomplete` по двум остаточным основаниям:

- **(a)** transitive hash inventory старых rv1–rv4 не перечислял hashes всех secondary logs/patches,
  хотя сами файлы существовали; rv5-инструмент проходил, потому что его scope refs был уже общего
  слова «complete transitive inventory».
- **(b)** позднее требование execution-time `committed == tested`: после тестов **26/26** current files
  совпадали с commit, но CHECK потребовал proof именно **на момент тестов** — заранее явно не заданный.

**Продуктовый дефект не установлен.** Владелец затем **CLOSED #232** как `done-with-caveat`;
`group_stopped = false`. Не утверждаем, что это реально остановило группу: это **ложный incomplete
CHECK** и риск остановок future.

### 1.3 Решения пользователя
- Задача исправляет **global skill rules** во **всех трёх** базовых скиллах, чтобы повторения не было.
- **#232 и историю не трогаем.**
- **Не** строим graph linking source/build/commit, **не** вводим новые per-path execution digests и
  **не** делаем historical inventory.

## 2. Canonical owner/source (все три скилла)

Владелец общей policy — **все три canonical global `SKILL.md`**:

- `/home/alex/sources/opencode-config/config/skills/pdca/SKILL.md`
- `/home/alex/sources/opencode-config/config/skills/pdca-coder/SKILL.md`
- `/home/alex/sources/opencode-config/config/skills/pdca-dotnet/SKILL.md`

У `pdca-dotnet` — собственные `required_rows`/rv и собственный completeness gate; у generic `pdca`/
`pdca-coder` — `required`/revision. **Схемы вариантов в #238 не унифицируются и не переписываются**:
одинаковая **семантическая** защита встраивается в их **существующие** контракты. Затронуть повторяющие
policy planner/check role prompts **только там, где этот gate фактически есть**; точный matrix
затронутых prompts/layout уточняет plan, без silent scope expansion.

### 2.1 Источники ролей (уточнение)
- `config/agents/{planner,check}.md` — **RU рабочие role definitions**.
- `pdca-dotnet/assets/agents/{planner,check}.md` (и аналогичные `assets/agents/**` у `pdca`/
  `pdca-coder`) — **переносимые EN briefs**, поддерживаются **согласованно вручную**. **Не** утверждать,
  что sync tooling автоматически синхронизирует **смысл**.
- README описывает deploy `rsync config/ → ~/.config/opencode/`, а `config/tools/sync-roles.mjs --check`
  сверяет **только структуру/frontmatter**; описания и тела правятся вручную.

### 2.2 Границы
- `~/.config/opencode/**` — **deployed copy**, не git canonical.
- `pdca-collection` **собственного gate не владеет** — отдельно не меняется.
- `/home/alex/sources/skills/opencode` (отдельный diverged export repo) **не** трогаем.
- В этой сессии — только nextorm spec/tracking; **никакого** implicit permission на записи в
  `opencode-config`. Будущая реализация там требует **явно согласованного workspace/доступа/поручения**.

## 3. Главный нормативный touchpoint: BINDING/APPLICABLE gate

Недостаточно добавить новый абзац. Явно **заменить** формулировку gate `No PASS while ANY required
entry open` в **каждом из трёх canonical contracts** (`pdca`, `pdca-coder`, `pdca-dotnet`) и в
повторяющих её role briefs на:

> **No PASS while any BINDING/APPLICABLE required acceptance entry is open.**

Сохраняются: **append-only frozen manifest**, требование что slot/lens **required**, и запрет
произвольного downgrade. Формулировки gate в трёх контрактах меняются **согласованно** — один semantic
change, **без** schema unification.

### 3.1 Append-only explicit audit disposition
- **Frozen row/её исходный status/текст не переписываются и не исчезают.** Для заранее самовольно
  добавленной лишней audit-строки CHECK добавляет **явный applicability disposition** в существующий
  report/receipt: stable row ID, классификация `diagnostic_nonbinding`, конкретная причина и основание
  в принятом scope / приоритетном global правиле. Это часть **одного canonical evidence contract**, а не
  новый конкурирующий manifest. Точные field names mapping (`required_rows` vs `required`) оставлены
  **plan**; принцип — фиксирован.
- **Raw row может остаться `open`** (не лгать `met`), но её explicit nonbinding audit disposition
  **исключает её из МНОЖЕСТВА ОБЯЗАТЕЛЬНЫХ ACCEPTANCE блокеров.** Final receipt **отдельно** показывает
  binding unmet rows и audit notes; PASS по genuine gates **не противоречит** raw historical audit open.
  Frozen manifest **retained + appended disposition**: нельзя удалить/переписать original row и потом
  заявить, что её не было.

### 3.2 Границы исключения (не лазейка)
Путь §3.1 разрешён **только** для перечисленных unsupported extra audit требований (recursive
historical inventory; retrospective/незапрошенное execution-time `committed == tested`), **не
вытекающих** из согласованной цели задачи. Он **не** является лазейкой объявить diagnostic:
failing/missing test, unverified direct evidence, source-copy mismatch или actual functional/security
variant. Scope basis и reason **проверяет role decision**, а не просто автор говорит «optional». Если
user явно согласовал finite audit goal как **отдельную** audit-задачу, audit row **BINDING** и обычный
fail/re-gather при её невыполнении сохраняется.

### 3.3 Что НЕ требуется менять
В `pdca-dotnet` текущий validator (`scripts/validate_inner_loop.py`) open rows **допускает** и
проверяет exact rows↔required coverage, поэтому в #238 **не** требуется удалять rows или менять
test-budget code. **Semantic PASS gate принадлежит skill/role briefs и должен реально измениться**:
нельзя оставить старые абсолютные запреты в другом параграфе так, чтобы новые notes всё равно давали
FAIL. Applicable existing mandatory completeness budgets **не ослабляются**; отсутствие audit metadata
**не расходует** gather/replan budgets.

## 4. Нормативная граница правил (основной DESIGN)

1. **Mandatory evidence slots/lenses остаются REQUIRED.** Не превращать весь frozen evidence contract в
   optional. Обязательность конкретного acceptance-основания вытекает из согласованного scope/acceptance
   либо безусловных проверок базового skill. PLAN **не может** по своей инициативе добавить hard gate на
   произвольную audit-полноту, весь transitive ref graph, ретроспективные hashes или execution-time
   `committed == tested` в обычную product-задачу.
2. **Для обычной product-задачи** дополнительные audit-metadata перечисляются **отдельно как diagnostic
   notes**, не required acceptance. Они **не** требуют новых DO-итераций/retests, CHECK→PLAN,
   escalation, `incomplete` или STOP только из-за отсутствия/неполноты этих метаданных. Результат по
   обязательным проверкам **не может** быть понижен до FAIL/`incomplete` из-за этих notes; notes не
   считаются product defect.
3. **Scope required evidence — конечный явно заявленный набор** непосредственных доказательств по
   acceptance rows, **не** неограниченная рекурсия всех ссылок во всех прошлых артефактах. Ссылка на
   старый вспомогательный документ сама по себе не создаёт обязательства заново аудировать всю его
   историю/hash descendants. Нельзя писать ничем не ограниченный `complete transitive inventory` как
   default hard requirement. Если пользователь сам явно заказал audit/provenance proof как цель
   **отдельной** audit-задачи, необходимый finite scope может стать acceptance; это исключение **не**
   позволяет CHECK навязать audit обычной feature-задаче.
4. **CHECK не вводит новые acceptance invariants поздно.** Реально найденные uncovered functional
   variants/ошибки/security-дефекты из actual diff продолжают идти существующим маршрутом; запрет
   касается **audit-надстроек**, не legitimate product-проверок. В отчёте — отделять обязательные
   evidence-row findings от diagnostic notes. «No optional slots» означает нельзя опустить required
   streams/reports, а **не** что вся возможная метаинформация автоматически mandatory.
5. **Номер коммита/наличие commit НЕ условие product acceptance.** Допускается честная identity
   непоследнего/незакоммиченного проверяемого working-tree состояния по действующему контракту. Не
   требовать коммитить ради PASS; отсутствие retrospectively provable `tested == committed` — **не**
   FAIL, когда actual required tests/current build evidence представлены и нет установленного mismatch.
   Если фактически выявлена stale `--no-build`/другая сборка/другая revision — это реальная проблема
   freshness, existing gate сохраняется.
6. **Самовольный mandatory-extra audit row** автора PLAN без основания agreed scope — это **дефект
   классификации контракта, НЕ новый product blocker.** CHECK явно фиксирует исходную row и причину
   nonbinding classification по приоритетному global rule, не молча удаляет/не подменяет row. Такой
   самонавязанный гейт **не приобретает** нормативную силу только от слова `frozen`. Согласованные
   реальные обязательства **не понижаются**: audit task, explicitly approved user, остаётся required.
   Append-only explicit applicability disposition описан в §3.1; implementation plan определяет
   минимальный field mapping в существующую единую схему, без нового competing contract.
7. **Не обещать «безусловный PASS».** Обязательные сборки/тесты/coverage/security/прочие applicable
   gates сохраняются. Missing mandatory direct result/report идёт **bounded re-gather** по действующему
   completeness gate и **не** становится optional; product FAIL/STOP для настоящей проблемы
   сохраняются. Истинность statements о результатах не ослабляется.

## 5. Совместимость с #235

Approved design **#235** (не implementation) сохраняет обязательные full file bodies reviewer pack,
source-copy fidelity receipt/digests и полный execution ledger. **#235 этим issue не изменяется.**
**#238 не объявляет любые
хеши/метаданные необязательными**; различаются **direct evidence**, достаточное для независимого
review, и **extra recursive/retrospective audit proofs**. Непредставленный нужный body/подтверждение,
mismatch staged-source, неподтверждённый запуск tests — это **не** «просто audit note». Автоматический
start/finish capture #235 **не** дублировать. **#234** (generator/stale-hash lint) в #238 **не**
реализуется. #238 не меняет loop test budget/validator semantics #235 и не требует новых
hash-ledger/seal generators.

## 6. Scope реализации (для будущего плана)

- Semantic correction в **каждом из трёх** canonical `SKILL.md` (`pdca`, `pdca-coder`, `pdca-dotnet`) —
  замена формулировки gate (§3); согласовать повторяющие её planner/check role briefs и active
  runtime/profile mirrors **только** там, где gate фактически присутствует.
- **Не** унифицировать/рефакторить схемы вариантов (`required_rows`/rv vs `required`/revision) и **не**
  вводить новый provenance graph. `pdca-collection` (своего gate не владеет) и
  `/home/alex/sources/skills/opencode` не менять.
- **Contract-consistency tests** и targeted **behavioral/pressure** scenarios; обновление статических
  tests само по себе **не** выдаётся за behavioral proof.
- `scripts/validate_inner_loop.py` в #238 ради этого требования **не** менять (см. §3.3; #235 —
  отдельный planned fix).
- Если план найдёт реально противоречащий shared rule — явно обозначить его **точную canonical
  location/дельту**, без silent scope creep.

## 7. Acceptance / pressure scenarios

Матрица покрывает **все три** варианта (`pdca`, `pdca-coder`, `pdca-dotnet`): общие сценарии
прогоняются для каждого, variant-specific — только где применимо.

- **A.** Build/tests + 54 required rows green; старый report ссылается на log/patch без отдельного
  ledger hash ⇒ CHECK принимает по required rows, audit note допустима, **нет** incomplete/STOP/нового
  round ради hash.
- **B.** Tests выполнены на текущем uncommitted состоянии; snapshot каждого path во время запуска /
  `committed == tested` proof отсутствует; existing freshness без mismatch ⇒ нет commit/provenance
  blocker и нет требования коммита.
- **C.** CHECK пытается постфактум добавить runtime digests/recursive inventory ⇒ audit-only demand
  отвергается с rationale, product work не перезапускается.
- **D.** PLAN-автор сам freeze-нул generic transitive inventory без user audit acceptance ⇒ правило не
  становится обязательным по инерции; explicit invalid classification recorded, реальная mandatory
  acceptance сохраняется, no false product incomplete.
- **E.** Реальный failed/missing required test result или stale binary ⇒ существующий
  FAIL/re-gather/STOP route сохраняется, не замаскирован audit exemption.
- **F.** Direct reviewer pack source-copy fidelity/required bodies/full execution ledger из #235 **не**
  ослаблены.
- **G.** User explicitly requested finite audit как **отдельную** задачу ⇒ scoped audit acceptance
  **не** превращается автоматически в optional.
- **H.** No files/статусы/артефакты/исторический verdict #232 переписаны; no new hashing graph system;
  runtime и canonical role rules согласованы; results различают diagnostic note vs actual mandatory
  unmet.

### 7.1 Behavioral evidence (без нового сложного runner)
Дизайн **не требует** разработки нового сложного runner. Допускаются **реальные изолированные вызовы
actual PLAN/CHECK ролей** с исправленными инструкциями и небольшими искусственными scenario packets в
рамках доступного agent/headless harness. Для каждого сценария (A–H, плюс необходимые) записать:
variant, instruction revision, вход, **полный структурированный verdict/route** и сравнение с
expected. **Fresh role context**; tier/profile — фактической роли; **model IDs не хардкодить**. Метод,
fixture files и orchestration определяет plan. Markdown-сценарии или text-presence assertions **не**
являются behavioral proof; существующие static consistency tests **дополняют**, а не заменяют реальные
решения. **Нельзя** утверждать, что сценарии прошли, пока они реально не выполнены. Запуск DB suites не
требуется при skill-only правках (если product code не менялся).

### 7.2 Acceptances (дополнительно)
- **Result test ловит D-класс:** raw audit row `open` + valid nonbinding disposition ⇒ **no false
  incomplete**; missing **real** required row ⇒ normal **nonPASS**; explicit user-requested audit
  остаётся **binding**.
- Canonical gate formulations во **всех трёх** contracts изменены **согласованно**; active role copies
  **не содержат** противоположного абсолюта.
- **No** schema unification/refactor и **no** new provenance graph.
- **#235** остаётся mandatory full copies/fidelity/journal — **не изменён**.

## 8. Design calls vs user

Policy, предотвращающая false blockers, и **scope на все три скилла** — **user-confirmed**. Primary
technical clarifications (отдельно **не** приписываемые пользователю): физические source paths и
источники ролей (§2.1), классификация «no optional slots», **handling applicability/append-only**
disposition (§3.1–3.2), **role tests** (§7.1) и сценарии.

Открытых вопросов по scope на момент записи **нет**. **Письменная спецификация APPROVED пользователем
2026-10-16** (финальный builder-check — ничего существенного не осталось). **Next = plan** (отдельный
шаг), **не** implementation.

## 9. Sources (кратко, внутренние)

- Tracked issue #238 (этот документ сопровождает).
- #232 owner closing URL: https://github.com/AlexeyShirshov/nextorm/issues/232#issuecomment-6208127200.
- Status `docs/specs/status/D232-sqlserver-pivot-unpivot-1.md`: `:895-905` (closure r=5, incomplete
  product-green), `:1013-1014` (final CHECK verdict).
- `rv5/check-receipt.json` — gitignored local artifact path (не фейковая blob-ссылка).
- Gate-строки (internal paths, plain code, без fake blob URL):
  - `opencode-config/config/skills/pdca/SKILL.md:313` — `No PASS while any \`required\` entry is \`open\``;
  - `opencode-config/config/skills/pdca-coder/SKILL.md:926` — та же формулировка;
  - `opencode-config/config/skills/pdca-dotnet/SKILL.md:971` — `No PASS while any \`required_rows\`
    entry is \`open\``.
- Approved design **#235** и canonical SKILL sections `### Versioned evidence contract` (строка 919) и
  `### CHECK completeness gate — mandatory evidence contract` (строка 1147) — номера в `pdca-dotnet`.
