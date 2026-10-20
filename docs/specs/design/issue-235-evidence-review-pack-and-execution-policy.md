# #235 — проверяемый пакет evidence, фактические интеграционные запуски и ACT cleanup

## 0. Статус и метаданные

- **Issue:** #235 — https://github.com/AlexeyShirshov/nextorm/issues/235 — проверено на GitHub.
- **Milestone:** **1.0.9-rc3** (номер 4) — фактическое проверенное значение на 2026-10-16.
- **Дата:** 2026-10-16.
- **Статус:** scope и **письменная спецификация APPROVED пользователем 2026-10-16**, включая явное
  уточнение ownership (§1.3). Независимый builder-check выполнен, ничего существенного не осталось.
  Implementation plan **не создан/не approved**, разрешения реализации **нет**; этот документ — design,
  а не реализация.
- **Родительский issue:** #218 закрыт (`CLOSED`) и не пересматривается.
- **Тип изменения:** process/evidence-контракт. Canonical владелец общих правил — глобальный
  `pdca-dotnet` (`SKILL.md` + `pdca-dotnet/scripts/validate_inner_loop.py` в каталоге global skill);
  отслеживаемая копия `scripts/validate_inner_loop.py` в nextorm **синхронизируется**. Проектный overlay
  `.opencode/skills/nextorm-pdca/SKILL.md` **не** меняется и общие правила не дублирует. Продуктовые
  SQL/C# API не затрагиваются.
- **Путь документа:** `docs/specs/design/issue-235-evidence-review-pack-and-execution-policy.md` —
  сознательно внутренний `docs/specs/**`; в DocFX не публикуется, публичных ссылок на него нет.

## 1. Намерение и контекст исторического сбоя

### 1.1 Намерение

Исправить процесс подготовки и независимой проверки evidence в будущих циклах **без** расширения
чтения medium-tier CHECK, **без** раздувания его контекста и **без** воздействия на SQL-продукт. Все
изменения относятся к #235, а не к истории #218.

### 1.2 Контекст исторического сбоя

- **E07.** Artifact содержал diffstat и технические doc checks, но registry-entries manifest ссылались
  на оригиналы `docs/specs/roadmap/sql-function-coverage-gap.md`,
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`,
  `docs/specs/comparison/evidence-01-provider-specific-features.md` — за пределами CHECK read scope.
  Staged bodies и независимого row-level вердикта не было.
- **E10.** Восемь **дополнительных уже выполненных** запусков (включая integration) были намеренно
  **не добавлены** в `report.json` с мотивом соблюсти cap. При этом не утверждается доказанное удаление
  существовавших записей.
- **Проблема фактического валидатора.** `scripts/validate_inner_loop.py:313-335` (`_rule_boundary`)
  считает boundary `dotnet test` без effective filter «полным прогоном» (comprehensive sweep) без
  различия unit/integration. Это не желаемая политика.

### 1.3 Ownership общих правил (уточнено пользователем 2026-10-16)

- **Canonical владелец** общих правил — глобальный `pdca-dotnet`: его `SKILL.md` и
  `pdca-dotnet/scripts/validate_inner_loop.py` в каталоге global skill
  (`~/.config/opencode/skills/pdca-dotnet/`).
- Репозиторный `scripts/validate_inner_loop.py` в nextorm — **синхронизируемая копия**, а **не**
  независимый владелец политики. Исправление только проектной копии **недостаточно**.
- Проектный overlay `.opencode/skills/nextorm-pdca/SKILL.md` **не изменяется** и общие правила **не**
  дублирует; его read roots/source specialization остаются прежними.
- **Global contract update — обязательная часть будущего implementation scope**, а не необязательная
  внешняя зависимость. Порядок: сначала canonical global `pdca-dotnet`, затем синхронизация repo copy.
  Future plan проверяет владельца каталога global skill и его repo/permissions и получает нужный доступ
  **отдельным шагом** — это не скрытое разрешение писать куда угодно/в другие репо.
- **В ЭТОМ сеансе** записи за пределами текущего repo нет: мы лишь корректируем дизайн; глобальные
  skill/script сейчас **не** меняются.

## 2. Согласованные требования к review pack / E07

- Исполнитель складывает в `artifacts/pdca/<task>/rv<k>/` **полные тела** изменённых файлов, необходимых
  независимому CHECK. Их нельзя заменять только diffstat, пересказом, ссылкой на недоступный оригинал
  или отдельными вырванными строками.
- Это staging, а не перенос исходных файлов: **оригиналы сохраняются**.
- Для каждого staged файла нужны: исходный путь; путь копии; привязка к фактически проверяемой
  ревизии/состоянию working tree; digest содержимого. Конкретные имена полей и layout оставлены
  планировщику — с **переиспользованием существующей схемы**, без создания второго конкурирующего
  evidence-контракта. При незакоммиченных правках одного HEAD недостаточно.
- Пакет включает необходимые подтверждения изменённых утверждений реестров; зеркалировать весь
  репозиторий не требуется.
- CHECK читает адресно row-level evidence **внутри разрешённых roots** и не обязан загружать все staged
  файлы в контекст.
- Автор пакета и независимый заверитель — **разные роли**: CHECK сам сопоставляет каждое изменённое
  registry-утверждение с представленными подтверждениями и записывает row-level результат/причину.
- **Разделение транспорта/технической source-сверки и независимого смыслового вердикта.** Автор/coder
  готовит полные staged bodies; source-side техническую сверку выполняет **отдельный дешёвый read-only
  gather-поток (scout)** с узким доступом к исходным путям — он фиксирует оригинальный путь, staged
  path, digests, source identity и **равенство содержимого**. Оркестратор поручает эту проверку **до**
  передачи medium CHECK. CHECK **не получает** новых read/MCP прав и читает только компактный receipt
  плюс нужные bodies/evidence; CHECK независимо оценивает содержательные утверждения, но независимость
  семантического вердикта **не** означает, что medium обязан прочитать live repo.
- Receipt сохраняет **фактический вывод gather**, а не авторскую декларацию coder (точный транспорт —
  план). Whole repo не зеркалируется. Это **source-copy fidelity**, а **не** доказательство
  execution-time `committed == tested` из #238 и **не** новый atomic generator из #234. Альтернатива на
  планировании — доверенный **детерминированный repo tool** с той же проверяемой технической
  семантикой; выбранный путь не должен вырождаться в просто самоподписанный текст автора.
- **Roslyn — на стороне source-side gather.** Когда нужна C#-семантика, Roslyn работает в этом узком
  независимом gather на **зафиксированной ревизии** соответствующего footprint; пакет получает результат
  с той же source identity. Medium CHECK оценивает пакет и **не** запускает Roslyn с недоступным
  whole-solution доступом. Данные source-сверки и C#-resolution привязаны к **одному snapshot**: нельзя
  проверить одну ревизию через Roslyn, а в receipt назвать другую.
- **Drift → invalidation.** С момента frozen capture и до CHECK запрещён дрейф относящегося к задаче
  проверяемого footprint без invalidation: drift ⇒ прежний verdict не принимается, актуальная ревизия
  получает новый `rv`/receipt/check. Это **не** глобальный запрет параллельных работ по несвязанным файлам.
- До CHECK проверяется доступность всех необходимых reviewer materials **внутри разрешённых roots**;
  исходный путь остаётся metadata, а не единственным путём для чтения.
- Недостающий body, mismatch, неполное обоснование или отсутствие независимого вердикта ⇒ **evidence
  open/blocked**, не product FAIL и не PASS.
- Права CHECK **не расширяются**; **Roslyn-first** для семантической C#-проверки сохраняется и
  выполняется в source-side gather; snapshot не заменяет семантическое разрешение символов там, где оно
  требуется.

## 3. Явная политика исполнения / E10

- **Лимит действует на ФАКТИЧЕСКИЙ ПРОГОН ИНТЕГРАЦИОННЫХ ТЕСТОВ, а не на число записей в отчёте.**
- Дорогие интеграционные тесты допускаются на границе DO→CHECK, а не после каждого unit/правки.
  Unit-тесты разрешены в неограниченном количестве внутри DO и на границах фаз.
- Наличие/отсутствие `--filter` **НЕ** является критерием unit vs integration и **НЕ** поводом
  ограничить unit execution. Классификация должна соответствовать фактически выбранным тестовым
  проектам/наборам; смешанный solution run, реально запускающий integration, не становится unit из-за
  названия команды или фильтра.
- PLAN заранее задаёт необходимый integration boundary scope и разрешённые запуски; #235 **не
  придумывает** отдельный универсальный числовой лимит unit/команд. Любой допустимый числовой
  integration budget относится к реальным запускам, а не к report.
- Дополнительный фактический integration run вне разрешённого плана/границы — **видимое отклонение**,
  подлежащее существующим gates; оно не маскируется фазой/переименованием. Изменения иных ограничений
  (например boundary solution build) в scope не входят.
- Каждый реально начавшийся запуск unit/integration отражается в execution ledger / `report.json`
  **независимо** от exit code, отмены, повтора, превышения лимита или пригодности как acceptance
  evidence. Для **не стартовавшего** запуска — явный execution state, не выдуманный факт.
- Учёт факта исполнения и оценка соблюдения политики — **отдельные действия**: запись нарушения не
  легализует запуск, отсутствие записи не отменяет его.
- Минимальные сведения переиспользуют существующий report: команда, фактический kind/scope, phase,
  tested source identity, состояние/результат и evidence/log reference. Технические имена полей
  определит план.
- Нужны **все** raw executions плюс отдельно compliance verdict; никакой выборки «записываем только
  зачтённые» вместо полного ledger. При pipeline-сбое неполный учёт — явно blocked/unresolved, а не PASS.
- **Обязательный execution capture path (canonical contract).** Механизм/contract — canonical в global
  `pdca-dotnet`, отслеживаемая repo copy синхронизируется (§1.3). Минимальный обязательный путь захвата
  для test-команд этого цикла: событие **start** фиксируется при запуске, результат **дописывается**
  после завершения; report **проецируется из этого журнала**, а не вручную отбирается автором. Точные
  runner/JSON/schema filenames — планировщику. Это фиксация **факта/состояния исполнения**, а **не**
  механизм execution-time per-source digests/provenance из #238; atomic general generator / stale-hash
  lint из #234 здесь не разрабатываем.
- Путь исполнения обязан оценить **actual integration policy до запуска**, насколько test scope
  определим; нарушение попытки исполнения и уже реально исполненное нарушение отражаются **разными
  состояниями**, без выдуманного факта старта. Запуск **вне** обязательного capture path — нарушение
  процесса; технически обнаружить любой скрытый произвольный shell-запуск мы **не** обещаем.
- **Измеримый completeness-тест:** runner породил start/finish записи; projection report с **пропущенной**
  execution отклоняется; over-budget run **не удаляется** из raw journal. Чтобы лимит не был «лишь на
  бумаге», политика относится к **actual selected integration scope** независимо от report и `--filter`.
  Возможные доп. integration запуски подчиняются **заранее разрешённому PLAN** и не получают silent
  exemption.
- **Эволюция существующей схемы report/validator.** Разрешено развивать существующую схему, сохраняя
  **один канонический контракт**; «reuse» не означает запрет добавлять поля состояния. Явно различаются
  состояния **started/running**, **finished**, **interrupted/canceled**, **failed-to-start/not-started**.
  exit code и selected count **не выдумываются** и могут быть неизвестны для незавершённого/не
  стартовавшего. Точный enum/nullable mapping — план. Незавершённая или отменённая попытка **не**
  удовлетворяет required successful evidence, но **сохраняется** даже при последующем успешном повторе.
  Поведение legacy rows/versioning прописывается в плане; отдельный конкурирующий report-контракт не
  вводится.
- **Global contract update — обязательная часть implementation scope.** unit-unlimited /
  actual-integration-only-на-boundary — согласованная **целевая политика**, и она должна закрепляться в
  **canonical** глобальном `pdca-dotnet` (`SKILL.md` + `pdca-dotnet/scripts/validate_inner_loop.py`), а
  затем **синхронизироваться** в репозиторную копию (ownership — §1.3). Это **не** необязательная
  внешняя зависимость и **не** «global optional alignment handoff». Пользовательский scope **не**
  подменяется прежним cap на любые unfiltered команды.

## 4. E08 — allocation oracle

- Пользователь **отверг** смену allocation oracle на matched A/B.
- Сохраняется существующий **заранее замороженный** критерий: allocations не выше baseline;
  неоднозначность — `unresolved`.
- Нового tolerance/методики/бенчмарка в #235 не вводим; задним числом oracle не подменяем.
- #218 и его `done-with-caveat` остаются нетронутыми.

## 5. ACT cleanup и lifecycle

- До завершения независимого CHECK, ревью и закрытия задачи **staged evidence сохраняется**; чистить
  на каждом DO→CHECK нельзя.
- При действительном закрытии задачи в ACT обязательно удалить тяжёлые staged-копии файлов и временные
  материалы **этой** задачи. При STOP/incomplete/ожидании решения/продолжении работы преждевременной
  уборки нет.
- Сохраняются: компактный итоговый отчёт, frozen manifest, digests/привязка к source, результаты
  независимой сертификации и компактная запись очистки (что удалено / когда / почему).
- Нельзя blanket-`rm` всего `artifacts/task` и нельзя удалять чужие task/`rv` по широким шаблонам.
- Frozen manifest **не переписывается** и не превращается задним числом в иной evidence: post-ACT
  удалённые staged paths объясняются **отдельной cleanup-записью** с прежним hash и lifecycle state.
- До закрытия отсутствующий body — ошибка; после корректного ACT очистка отражена как **planned
  lifecycle**, не «исходного evidence никогда не было».
- Компактные retained records **не обещают** возможность заново провести полную проверку без очищенных
  тел. Универсальную retention/архивацию всей истории #238 не обещаем.
- Final report независимого CHECK сохраняется; уборка **не заменяет** проверки. Ранее существующие
  artifacts исторических задач, в частности #218, в #235 **не** перепаковываются/не чистятся.
- Ни текущий brainstorm, ни апрув спеки **не** запускает cleanup/закрытие issue.
- **ACT cleanup receipt — машинно проверяемый компактный артефакт, а не только проза.** Он привязан к
  task/rv, **approved terminal closure** и независимому final verdict; содержит удалённые пути с
  **прежними hashes**, retained records и результат очистки. Gate **до** cleanup проверяет право terminal
  closure и scope удаления; преждевременная очистка — **rejected**. Очищаются только явно cleanable
  snapshots/temp — не обязательные retained report/manifest/digests/certification и не чужие файлы.
  Report/manifest frozen не переписываются. Последующая проверка archived pack **учитывает receipt**:
  отсутствие разрешённо очищенного snapshot не означает, что его никогда не было; отсутствие snapshot
  **до** ACT или без валидного receipt остаётся **ошибкой**. Archived validation **не заявляет** повторную
  смысловую сертификацию по удалённым телам. Конкретный schema/layout/mode — планировщику.

## 6. Границы

- **Ownership.** Canonical владелец общих правил — глобальный `pdca-dotnet` (`SKILL.md` +
  `pdca-dotnet/scripts/validate_inner_loop.py` в каталоге global skill). Репозиторный
  `scripts/validate_inner_loop.py` — **синхронизируемая копия**, а не независимый владелец политики;
  проектный overlay `.opencode/skills/nextorm-pdca/SKILL.md` **не изменяется** и общие правила не
  дублирует, его read roots/source specialization остаются прежними.
- **Global contract update — обязательная часть implementation scope**, а не необязательная внешняя
  зависимость. Порядок: сначала canonical global `pdca-dotnet`, затем синхронизация repo copy; политика
  canonical script и repo copy должна стать **одинаковой** после синхронизации. Будущий план проверяет
  владельца каталога global skill и его repo/permissions и **получает нужный доступ отдельным шагом**;
  это не скрытое разрешение писать куда угодно/в другие репо.
- **В ЭТОМ сеансе** записи за пределами текущего repo **нет**: мы лишь корректируем дизайн; глобальные
  skill/script сейчас **не** меняются, локальный скрипт/оверлей/продукт/тесты не трогаются. Продуктовые
  C# API/SQL не меняются; реализация — отдельным шагом.
- Текущая несогласованность global status-file lifetime vs local overlay **не чинится** этой задачей;
  cleanup касается staged artifacts, а не удаления status-документа.
- Не дублировать #234 (atomic evidence generator / stale-hash lint) и #238 (исторический transitive
  inventory / execution-time provenance): используем существующую source binding и **не** объявляем
  решённым `committed == tested`.

## 7. Acceptance для будущего implementation plan

- (a) Changed registry full body представлен CHECK внутри allow roots + independently certified row
  verdict; diffstat-only/outside-only negative case — blocked.
- (b) Multiple unfiltered unit runs внутри DO и на boundary разрешены без искусственных namespace
  filters.
- (c) Integration внутри DO детектируется как фактическое policy violation, запись при этом всё равно
  существует.
- (d) Boundary integration отражает planned scope/count; смешанные runs корректно classified.
- (e) Все начавшиеся executions включены, включая fail/cancel/retry/over-budget; omission ведёт к
  completeness failure, а не к способу пройти cap.
- (f) Approved allocation criterion не изменён; никаких retro-T218 changes.
- (g) Успешный ACT удаляет только копии/temp, сохраняя final report/manifest/hashes/certification/
  cleanup receipt; cleanup до terminal closure отвергается; retained frozen manifest не изменён; чужие
  task artifacts не трогаются.
- (h) Source-side независимый gather произвёл **receipt** (оригинальный путь, staged path, digests,
  source identity, равенство содержимого) **до** medium CHECK; receipt фиксирует фактический вывод
  gather, а не author-декларацию; CHECK читает receipt+bodies без новых прав; witness/receipt не
  смешивают ревизии (Roslyn snapshot = source identity пакета); drift footprint ⇒ invalidation и новый
  `rv`/receipt/check.
- (i) Global contract update (обязательная часть scope): после реализации политика **одинакова** в
  canonical global `pdca-dotnet` script и в синхронизированной repo copy; в
  `.opencode/skills/nextorm-pdca` **нет** новых дублирующих общих правил; в этой дизайн-работе глобальные
  skill/script **не изменены** (only design). Future plan проверяет владельца каталога global skill и его
  repo/permissions и получает нужный доступ отдельным шагом.
- (j) Execution journal: runner порождает start/finish записи; projection report с пропущенной execution
  отклоняется; over-budget run сохраняется в raw journal; pre-run оценка integration policy различает
  attempt-violation и executed-violation.
- (k) Схема различает started/running, finished, interrupted/canceled, failed-to-start/not-started;
  exit code/selected count nullable и не выдумываются; незавершённая попытка не удовлетворяет required
  success, но сохраняется после успешного повтора.
- (l) Cleanup receipt машинно проверяем; gate до cleanup проверяет terminal closure и scope; premature
  cleanup rejected; archived validation учитывает receipt и не заявляет ре-сертификацию по удалённым телам.
- План должен определить **измеримый** сценарий проверки actual ledger completeness **без reliance на
  author claim**, в рамках существующего process tooling, не обещая невозможной абсолютной проверки
  невидимых shell-запусков.

## 8. Decisions vs builder calls

- **User decisions (пользовательские решения — прямо перечислить):** artifacts-only CHECK; full changed
  file bodies; unlimited unit tests; actual integration-only restriction на DO→CHECK; complete reporting;
  no A/B oracle; do not alter #218; ACT clean heavy copies/temp, keep compact final report/manifest/hashes.
- **Architect calls (архитектурные решения — не приписывать пользователю отдельно):** адресная row-level
  загрузка; metadata/reuse existing schema (с правом эволюции, один канонический контракт); cleanup
  receipt + immutable frozen manifest; no premature STOP cleanup; no universal numeric cap invented;
  разделение транспорта/source-сверки (дешёвый read-only gather) и независимого смыслового вердикта;
  Roslyn в source-side gather на единой зафиксированной ревизии; drift ⇒ invalidation; обязательный
  execution capture path со start/finish и проекцией report; явные состояния started/running, finished,
  interrupted/canceled, failed-to-start/not-started; машинно проверяемый ACT cleanup receipt; global
  contract update как **обязательная** часть implementation scope (canonical `pdca-dotnet` + sync repo copy).
- **Оставлено планировщику:** ownership-проверка каталога global skill и его repo/permissions (и способ
  получить доступ отдельным шагом); filenames/schema mapping; transport/receipt protocol детали;
  integration scope detection и tests implementation; package copying implementation; narrowly scoped
  cleanup implementation; runner/JSON/schema filenames и enum/nullable mapping; legacy rows/versioning;
  sync canonical ↔ repo copy.
- **Open user questions:** нет; независимый builder-check завершён. Письменная спека **APPROVED
  пользователем 2026-10-16** (с уточнением ownership, §1.3). Plan не создан/не approved; реализация не
  авторизована.

## 9. Sources

Canonical (global skill dir):

- `~/.config/opencode/skills/pdca-dotnet/SKILL.md` — canonical владелец общих правил.
- `~/.config/opencode/skills/pdca-dotnet/scripts/validate_inner_loop.py` — canonical validator
  (в этом сеансе не изменялся).

Repo-local:

- `.opencode/skills/nextorm-pdca/SKILL.md` — проектный overlay (не изменяется, общие правила не
  дублирует; read roots/source specialization прежние).
- `docs/specs/design/issue-152-pdca-evidence-contract.md`
- `scripts/validate_inner_loop.py` — **синхронизируемая копия** canonical validator.
- `docs/specs/status/D218-sqlserver-sequences-1.md`

Исторические artifacts указываются как **локальные пути** (каталог `artifacts/` перечислен в
`.gitignore`, поэтому в чистом checkout их может не быть), а не как фейковые blob URL.
