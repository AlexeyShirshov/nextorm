# #152 — PDCA: обязательный versioned evidence contract на PLAN

## 1. Статус и метаданные

- **Issue:** #152 — https://github.com/AlexeyShirshov/nextorm/issues/152 — проверено на GitHub:
  state = `OPEN`, текущий milestone = **1.0.9-rc2** (номер 20).
- **Historical provenance:** debt 9 из #144 изначально зафайлен под milestone **1.0.9-b**; это
  историческая ссылка происхождения, а не противоречие с текущим milestone #152.
- **Дата:** 2026-10-02.
- **Статус:** письменная спека одобрена пользователем в этой сессии (точная фраза `спеки ок`,
  2026-10-02); ранее в чате был одобрен только архитектурный дизайн — это история. Written spec
  approved; implementation plan not created/approved, execution not requested/started in THIS
  interview; next eligible phase planning but not automatically begun. Заявлений об отсутствии кода
  где-либо вне этого интервью нет. Этот файл фиксирует одобренную спеку, а не авторизацию
  исполнения.
- **Цель:** устранить evidence-only CHECK-циклы (#143 D2, #144 debt 9), не допускать ложного PASS и не
  терять обнаружение реальных дефектов — через обязательный контракт доказательств, фиксируемый на PLAN.
- **Scope:** глобальный `pdca-dotnet` + необходимые `assets/agents/planner.md`, `check.md`
  (и orchestrator-шаблон) И проектный overlay `nextorm-pdca`. Глобальные файлы сейчас не редактируются.
- **Non-goals:** no code/ORM, no arbitrary external repo modifications/push, no new platform runner,
  no new state-machine фаза, no implementation plan, no global edits в этой сессии.

## 2. Наблюдаемые факты (существующие ссылки)

- #152 body: три вида приёмки «acceptance row → test `file:line`, exit codes, artifact paths».
- `docs/specs/status/native-extreme-row-144-1.md:248-250` — CHECK#1 (реальный дефект) против
  CHECK#2/#3 (evidence-only); `:278-286` — CHECK#3 anchors.
- `docs/specs/status/identity-returning-143-1.md:96-112` — matrix row → test `file:line`; D2 (process),
  D3 (scope).
- Глобальный `assets/agents/check.md:52` уже содержит «missing required report is re-gathered, not
  invented as a project defect»; правило «приоритет не понижается» и «добавленные CHECK-риски
  сохраняются» действуют.
- Overlay `.opencode/skills/nextorm-pdca/SKILL.md:85-86` — обязательные строки класса нельзя удалять.

## 3. Согласованный дизайн (6 частей)

### 3.1 PLAN — обязательный versioned evidence contract (пишет planner)

- Для каждого требования — стабильный acceptance/requirement ID.
- Поля контракта: required check/scenario; expected evidence kinds и источник (planned existing
  tests/symbol anchors **или** planned test role); команда + требования к exit-code/log; artifact
  expectations; owner stream; applicability predicate; revision `rv`; stable row ID across revisions.
- Эти семантические поля (stable row ID, revision `rv`, expected check/evidence, owner, applicability)
  **уже заданы как обязательные**; будущий PLAN выбирает только сериализацию/table layout и **не может**
  ослаблять или опускать поля.
- Future actual test `file:line`/outputs **не фабрикуются**; не требовать предсказывать несуществующие
  номера строк.
- Контракт применяется **до DO** через существующий PLAN-гейт; новая фаза не вводится.

### 3.2 DO — ledger заполняет фактическое evidence

- По ID/версии контракта: test symbol/`file:line` и результат; точная команда/exit-code и пути
  log/artifact (где применимо); явный статус not-run/failed/missing/blocked.
- Не каждая строка требует всех видов artifact — зависит от required check.
- Пути должны быть проверяемы reviewing stream; success-текст без evidence недостаточен.
- Роли: planner владеет требованиями; coder-executor записывает команды; scout — только факты; check
  владеет вердиктом; orchestrator — presence+dispatch, а **не** авторит план.
- Дорогой primary не читает raw code.

### 3.3 CHECK — completeness gate

- Missing reference/report → **re-gather внутри CHECK**: не product FAIL, не повторный product DO, не
  расходует product-fix iteration; нет PASS, пока обязательное доказательство отсутствует.
- Если сам check не исполнен → собрать **исполняющий** required check (coder-runner для shell по tier),
  а не только «отчёт покрасивее».
- Infra/tool failures → явный blocked/incomplete по существующим state/status-конвенциям; не новая
  фаза, успех не заявляется.
- Evidence, демонстрирующее реальный дефект → FAIL и соответствующий loop-back.
- Новый uncovered required variant/risk → обоснованный CHECK→PLAN с ревизией контракта, а не молчаливое
  требование доказательств задним числом как product-дефект; сохраняется глобальное правило «uncovered
  required matrix row — дефект/replan, не missing log».
- Reviewer новую находку ссылает на original version, новое требование и grounds; реальные риски не
  подавляются.

### 3.4 Bounded gather

- Gathering ограничен наблюдаемым фактом, а не произвольным временем: **успешное получение завершает
  сбор**; первый подтверждённый неустранимый access/tool/environment blocker → явный `blocked` + next
  action.
- Не повторять идентичную заведомо провалившуюся операцию без изменившегося условия; transient-сбои
  retry только с явной причиной. Числовую timeout/retry-политику не выдумывать.
- Это существующее CHECK-состояние `blocked`/`incomplete`; новая фаза не вводится и PASS не заявляется.
- Re-gather не обходит integration-требования, coverage/assertions и т.п.; missing-report и фактический
  провал команды/теста — разные вещи.

### 3.5 N/A и неизменность обязательств

- N/A только для conditional check, с observable predicate/reason, проверяемым check.
- Unconditional CHECK streams/ядерные обязательства незыблемы; mandatory-требования нельзя удалять или
  понижать.
- Требование, заменённое из-за genuine scoped plan correction, трассирует supersession, **не** стирая
  обязательство молча.
- Version record additions: reason/owner/date + ledger links; точная schema встраивается в существующие
  шаблоны будущего плана; общего waiver-authority не изобретать.

### 3.6 Концептуальная консолидация

- Глобально — один source of truth; проектные check-anchors не дублируют и не противоречат глобальному
  контракту.
- Необходимые шаблоны получают REQUIRED слоты вывода contract/ledger/version, а не reminders в прозе.
- Новой state-machine фазы нет; четыре CHECK streams/conditional security и дорогое/дешёвое routing
  сохраняются.
- Точное согласование routing/state-wording — в плане, а не обещание, что живые текущие файлы уже
  прочитаны, пока это не verified.

## 4. Приёмка и тестирование (writing-skills discipline)

- Baseline pressure-сценарии — это **future acceptance**, выполняемые **до** правок; на данный момент
  они **ещё не выполнены**. В brainstorm глобальные файлы не редактируются; 3+ pressure-ограничения,
  если проверяется дисциплина; control и тот же сценарий с новым правилом.
- Future behavior tests: planned nonexistent test line; missing log реально прошедшего check; test not
  run; actual defect; new risk/uncovered matrix; valid conditional N/A vs prohibited unconditional N/A;
  inaccessible artifact → explicit blocker; stable version после replan и sibling reviewers на одном
  контракте.
- Различать single-shot симуляции и реальный live-harness; не заявлять statistical/full live proof;
  для wording-микротестов ≥5 независимых samples на control/variant.
- Structural: существующие шаблоны — гейты/routing без противоречий; project/global consistency;
  frontmatter intact; CRLF в workspace/global по фактической политике; whitespace.
- Product build не нужен только ради skill-изменений. После будущих config-time правок пользователь
  перезапускает OpenCode; текущая сессия может держать старый cached text.

## 5. Global scope и планируемые артефакты (verify при реализации)

- Intended global: `/home/alex/.config/opencode/skills/pdca-dotnet/SKILL.md`,
  `assets/agents/planner.md`, `assets/agents/check.md`.
- Перед правкой verify source-managed copy и repo-boundaries; никаких commit/push ни в этом, ни в
  любом другом репозитории.

## 6. Handoff

- Письменная спека одобрена. `writing-plans` — следующая допустимая фаза, но она не начата
  автоматически. Способ исполнения пользователь выбирает отдельно. Сейчас plan/commit/push/merge
  не выполняются. Обновление issue не является авторизацией исполнения.
