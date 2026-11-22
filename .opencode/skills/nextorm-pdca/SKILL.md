---
name: nextorm-pdca
description: "nextorm overlay for the global `pdca-dotnet` cycle: project implementation constraints, the DO design pass for code changes, independent CHECK assessment, cached-path performance acceptance, and ACT hygiene. Load together with `pdca-dotnet` before PLAN whenever a PDCA cycle runs in this repository."
---

# nextorm: PDCA-оверлей (DO/Check)

Проектные дополнения к глобальному скиллу **`pdca-dotnet`**. Загружай их **вместе** с ним и
**до PLAN**: сначала `pdca-dotnet` (машина состояний, гейты, делегирование), затем этот
оверлей. Здесь — nextorm-ограничения реализации, DO-дизайн-проход, CHECK-оценка его evidence,
перф-приёмка и гигиена ACT. Гейты и машину состояний оверлей **не меняет** — уточняет содержание фаз.

## Политика агентов

- Предметные агенты (`nextorm-design-engineer`, `nextorm-code-auditor`,
  `nextorm-db-perf-analyst`, `nextorm-inmemory-perf-analyst`) существуют для интерактивной
  работы. В цикле их **не диспатчат**. Нужные ограничения и процедура дизайн-прохода встроены
  ниже: standalone `nextorm-design-engineer` не требуется читать или запускать.
- Driver передаёт применимые инструкции этого оверлея **inline** в DO и каждый CHECK. В DO
  `pdca-executor` готовит design/audit evidence; CHECK получает отчёт и независимые наблюдения,
  достаточные для суждения. CHECK не загружает скиллы, не читает product source, не запускает
  сканы и не создаёт субагентов. Недостающее evidence запрашивается driver у уже разрешённых ролей;
  permission и роль CHECK не расширяются.
- Записи в регистры (`docs/specs/design/*`) и правки кода/конфигов делает `pdca-executor`
  в фазе Do, не оркестратор.

## Владение фазами и проектные ограничения

### Граница PLAN / DO / CHECK

Базовый контракт `pdca`/`pdca-dotnet` авторитетен: PLAN определяет только результат, критерии
приёмки, ограничения, границы, риски и условия остановки. Этот оверлей не добавляет критерии и
не поручает PLAN проектировать исполнение или проверку.

- Вариантную матрицу из фактических путей исполнения, test strategy, coverage/mutation strategy,
  конкретные команды, oracle/evidence methods и подготовку артефактов выбирает и выполняет DO
  (`pdca-executor`) в пределах утверждённой задачи. Project policy может войти в PLAN как
  ограничение, но не как замороженная процедура проверки.
- CHECK самостоятельно выбирает метод и достаточность проверки по базовому контракту. PLAN не
  обязан заранее доказать конкретный oracle или закрепить evidence-source/command. Если факт о
  measurability действительно неясен, base допускает один bounded choice probe; unusable method
  при CHECK пере-выбирается, а недостающее evidence дособирается. Только критерий, который нельзя
  измерить доступными средствами, использует существующий PLAN → ACT outcome.
- Design/audit отчёт — candidate evidence от DO, не самосертификация. Каждый CHECK оценивает его
  применимость, полноту и findings; правила и конкретный формат приведены ниже.

Следующие nextorm-инварианты ограничивают реализацию и fix; они не превращают findings или
технические решения в обязанности PLAN.

1. **Абстракция только на втором потребителе / наблюдаемом шве.** Не предлагать
   `IX`/`X` «потому что чище»: при единственном потребителе сплит запрещён
   (`solid-review.md` F12; F3/F7 — `IConnectionFactory`, `IColumnMapper`).
2. **KISS = число концептов.** Новый тип/интерфейс/индирекция обязан назвать
   конкретного потребителя, которого разблокирует.
3. **Measure, never assert.** «Аллоцирует / медленнее» — только из кода (аллокация
   на per-row пути) или в пользу perf-аналитика. Дельты <~20% — шум на этой машине
   (разброс до 19%; `docs/specs/performance/decisions-and-lessons.md`).
4. **Не переоткрывать settled findings без нового измерения.** Регистры смотреть
   on-demand (только нужную находку): `docs/specs/design/solid-review.md` (F1..F13),
   `docs/specs/design/code-smells-review.md`, `docs/specs/design/API-NAMING-REVIEW.md`
   (P0-n), `docs/specs/performance/*` (M*/I*/R*).
5. **Public API — сквозной.** Rename/remove публичного типа/члена требует и
   `docs/**`, и `docs/ru/**` в том же изменении (AGENTS.md).
6. **Build discipline задаёт fix.** `TreatWarningsAsErrors=true`, nullable, CRLF,
   CPM (`Version` только в `Directory.Packages.props`). Изменение с warning — не fix.
7. **Известные факты архитектуры не репортить заново:** `DataContext` — тонкий фасад
   над `QueryExecutor`/`QueryPlanner`/`DbConnectionManager`/`ContextEnvironment`/
   `QueryCache`; `InMemoryDataContext` делегирует фасад `InMemoryQueryExecutor`;
   `ISqlDialect` — намеренно 97-членный композит (F12). Длины файлов читать из
   текущего дерева.
8. **Ничего не выносится за текущий milestone.** Если unit/фаза не решается целиком в
   этом цикле, её **можно засплитить**, но все новые задачи/срезы/tracking-issue
   остаются в **том же milestone**, что и исходная. Перенос в следующий milestone
   (например `1.1`) запрещён: нереализованный срез — это отдельный unit/issue с
   текущим milestone, а не `deferred` в будущий релиз. Запись ограничения в
   `docs/advanced/limitations.md` фиксирует поведение, но **не заменяет** задачу в
   текущем milestone.
9. **Поиск — ignore-aware; символы — `roslyn`.** Для C#-символов (типы/члены/`refs`/`callers`/overloads/
    implementations/renames) — только инструмент `roslyn`, не текстовый поиск. В V2 `roslyn` не
    top-level: вызов — **внутри `execute`** (`const r = await tools.roslyn({ action: "members",
    target: "Ns.Type" }); return r`), батч — одним `execute` (`Promise.all`); если его нет в каталоге
    Code Mode, сначала `search({ query: "roslyn" })`. Для текста (доки,
    комментарии, строковые литералы, имена SQL) — `rg`/`git grep` с ограничением по исходникам
    (`rg -n --glob '*.cs' "<lit>" src tests`). **Никогда `grep -r`/`grep -rn`/`find`** по дереву: они
    игнорируют `.gitignore` и читают `bin/`/`obj/`/`TestResults/` (≈1.3 GB артефактов под `src`/`tests`),
    т.е. ~1 GB диска на один вызов. См. `AGENTS.md` → «Searching text».
10. **Повторный заход после терминального `incomplete`/STOP** не перезапускает провалившийся критерий
    без изменений: прежняя терминальная причина — обязательный вход PLAN; `planner` либо показывает,
    что новые факты/evidence устранили терминальную причину. Это не разрешает менять supplied
    criteria и не создаёт отдельного правила oracle для PLAN.

### Вариантная матрица DO/CHECK (не PLAN)

При изменении code path DO строит матрицу по затронутым путям исполнения, а не только по тексту
требований. Каждая применимая строка получает `тест` / `guard` / `deferred + триггер`. CHECK
сверяет её с candidate и наблюдениями, добавляет пропущенные фактические варианты и сообщает о
незакрытых строках. Это project-policy guide для исполнения/оценки, не новая PLAN-criteria set.
Ниже — nextorm-классы и обязательные строки (**P1 по построению**); для других изменений DO
определяет строки из фактического scope. Таблица не потолок: CHECK может добавить релевантные строки.

| класс изменения | обязательные строки (P1 по построению) |
|---|---|
| стриминговый терминал | обе поверхности (`QueryCommand<TResult>` + `EntityBuilder<TEntity>`); sync **и** async; `CancellationToken` у async; `params`-паритет с сиблингами; fail-closed на неподдержанной форме; владение ресурсами (приёмник не закрывать/не `Dispose`; flush-семантика); покрытие провайдеров |
| новая SQL-функция/оператор | паритет формы с SQL-сиблингом; capability-флаг по диалектам; тест на каждом провайдере; nullable-семантика |
| изменение query-path / plan cache | cached-vs-prepared ratio (перф-приёмка); не мутировать shared `QueryCommand`; покрытие temp-table/TVP-веток |

## DO: дизайн-проход для изменений кода

Каждый DO, меняющий .NET/C# source (product или tests), выполняет scoped design pass. Он не расширяет
критерии и не запускает полный репозиторный аудит.

1. **Load the procedure.** До анализа загрузи `dotnet-csharp` и его always-load baseline, включая
   `references/solid-principles.md`, а также `references/type-design-performance.md`. Загрузи
   `analyzing-dotnet-performance` и topic references, выбранные его workflow по сигналам.
   `references/` относятся к каталогу соответствующего skill. Если skill не зарегистрирован,
   прочитай его `SKILL.md` и нужные references из установленного полного пакета
   `~/.config/opencode/skills-deepseek/<skill>/`; недоступный обязательный skill/reference запиши
   с точным именем и незавершённой частью как blocker, не называй отчёт полным.
2. **Scope and order.** Рассмотри изменённый код и только затронутые execution paths/соседний контекст,
   нужный для проверки контракта. Выполни последовательность: **SOLID/DRY design → type design →
   performance recipes** из загруженных материалов.
   - SOLID/DRY: SRP/OCP/LSP/ISP/DIP, god classes/fat interfaces, leaky contracts, switch-on-type,
     дублированное знание и `IFoo` без второго consumer.
   - Type design: sealing (с учётом инварианта ниже), mutable/defensive-copy structs, public
     collection return types, `ValueTask`, `Span<T>` в async и per-call `Dictionary`/`List`.
3. **Recipe evidence.** Для recipe set `analyzing-dotnet-performance` сохрани execution checklist:
   каждый recipe имеет число hit'ов, включая явные нули; `N/A` допустим только с обоснованием по
   scope. Не делай performance-выводов без измерения или очевидной аллокации на per-row пути; числа
   и интерпретацию передавай профильному perf analyst.
4. **Structural sealing.** Посчитай sealed/unsealed classes в явно названном scope, укажи оба числа,
   denominator и ratio. Это не verdict по единственному экземпляру; если классов нет, объясни N/A.
5. **Findings.** Для каждого finding укажи severity (🔴 Critical / 🟡 Moderate / ℹ️ Info) и lens
   (`[SRP]`/`[OCP]`/`[LSP]`/`[ISP]`/`[DIP]`/`[DRY]`/`[TYPE]`/`[PERF]`), count, точный `file:line`,
   one-line fix, применимый проектный инвариант и disposition: `fix now` в утверждённом scope либо
    `deferred + trigger`/обоснованно неактуально. Используй severity/scale rules загруженных skills;
    повышай severity на подтверждённых hot paths; findings сами по себе не меняют scope. Регистр смотри
    on-demand — только относящуюся находку;
   settled finding не открывай снова без нового evidence/измерения.
6. **Fix and verify.** Исправления делай по одной связной оси за шаг. Проверяй каждый фикс по
   применимому проектному build/test gate и сохраняй результат. Обновляй только соответствующий
   регистр, если изменился status/evidence находки. Все оставшиеся findings и причины disposition
   явно остаются в отчёте.
7. **Candidate-bound report.** Сохрани `design-review.md` в текущем `evidence_root`, привязав его к
   candidate согласно базовому evidence identity (не вводи отдельный hash gate). Включи scope/identity,
   загруженные skills/references, полный recipe checklist, sealing ratio с denominator, findings,
   фиксы и их verification, unresolved/deferred items и логи. После изменений кода обнови все
   затронутые части анализа и отчёта. Для повторного CHECK того же неизменившегося candidate валидный
   отчёт переиспользуется; новый CHECK всё равно оценивает его заново.

Для DO без code changes design pass не нужен; каждый CHECK обязан явно записать обоснованный `N/A`
по фактическому diff (см. CHECK ниже).

## DO: формирование гайдов (документация)

- **Любая новая функциональность требует пользовательской документации.** В том же изменении обнови
  подходящие опубликованные страницы или добавь новые, если подходящих нет. Синхронизируй версии
  EN и RU, исправь устаревшие описания и добавь новые страницы в соответствующие `toc.yml`.
  Объясни, как воспользоваться новой возможностью и какие ограничения действуют. Внутренняя
  спецификация, запись PDCA и XML-комментарии не заменяют пользовательский гайд. В handoff укажи
  пути и разделы обновлённых страниц.
- **Крупный блок новой функциональности выносится в отдельный гайд.** Если большой
  законченный блок новой функциональности не относится ни к одному существующему
  гайду, он оформляется **своим гайдом** (`docs/<тема>/index.md` + нумерованные
  страницы + `toc.yml`), а не дописывается в чужой гайд.
- **Гайд > 15 подразделов — выделять в отдельный блок.** Когда гайд разрастается
  больше чем на 15 подразделов, его выделяют в отдельный гайд-блок — как это сделано
  с гайдом scalar functions (`docs/scalar-functions/`).
- **Перенумерация после выноса.** Когда гайд (или его часть) выносится в отдельный
  раздел/блок либо удаляется, номера оставшихся нумерованных страниц `docs/guide/NN-*`
  пересчитываются подряд, без пропусков. В том же изменении обновляются все ссылки на
  переименованные страницы и записи `toc.yml` с обеих сторон (EN+RU); «дырку» в
  нумерации (например, `30 → 32`) оставлять нельзя.

## DO: применение фиксов (pdca-executor)

Фиксы в пределах утверждённых критериев и scope (из DO design/type/perf-прохода или CHECK loop-back)
применяет `pdca-executor` по общему «Coder editing discipline» (`pdca-dotnet` §Delegation), с
nextorm-конкретикой:

- **Одна ось за шаг**, затем сборка+тесты — не паковать несвязанные рефакторы.
- **Сборка — гейт:** `dotnet build nextorm.slnx -c Debug` = `0 Warning(s) 0 Error(s)`
  (`TreatWarningsAsErrors=true`); изменение с warning — не фикс.
- **CRLF всегда.** После правки любого файла: `perl -pi -e 's/\r?\n/\r\n/g' <file>`;
  LF-only/смешанные окончания не оставлять.
- **CPM:** версии только в `Directory.Packages.props`.
- **Тесты до «готово»:** `dotnet test tests/<project> -c Debug`; интеграционные — по
  `.opencode/skills/running-integration-tests/SKILL.md` (суита fail-closed: без
  `DOCKER_HOST` контейнерные провайдеры **падают**, а не скипаются — такой прогон
  не evidence).
- **Никаких `git push`/коммитов** без явной просьбы (AGENTS.md).
- **BDN-артефакты трекаются:** после бенчмарка вернуть `benchmarks/BenchmarkDotNet.Artifacts`.
- **Без бенчмарк-выводов** (дельты <~20% — шум); числа — на perf-аналитика (через субагента).

## CHECK: независимая оценка audit evidence

Для каждой новой функциональности проверь выполнение требования из
§«DO: формирование гайдов (документация)» на том же кандидате. Если пользовательская
документация отсутствует, неполна или противоречит реализации, верни работу в DO; не принимай
функциональность без документации.

Каждый CHECK, включая recheck, явно оценивает design-review report для текущего candidate. CHECK
не выполняет сам дизайн-проход: он получает inline применимые инструкции, report из `evidence_root`
и независимые source observations, собранные driver через уже разрешённые роли. Авторский report —
candidate evidence, не самосертификация; его выводы сверяются с наблюдениями и identity candidate.

### Обязательная оценка design review на каждом CHECK

CHECK возвращает все пункты:

1. **Applicability и identity.** Есть ли code changes в scope; если да, относится ли `design-review.md`
   к тому же candidate и охватывает ли изменённый код и affected paths. Если code changes нет, записать
   обоснованный `N/A` по фактическому diff в этом CHECK.
2. **Completeness.** Для применимого отчёта проверены ли три линзы в требуемом порядке, загрузка
   обязательных skills/references, каждый recipe checklist item (включая нули/обоснованные N/A),
   sealing ratio с scope и denominator, все findings с полями и disposition, а для fixes — актуальные
   build/test evidence. Ни одно поле не считается выполненным только со слов автора.
3. **Findings.** По каждому finding оценить связь с criteria и project invariants по независимым
   source observations: действительный defect в candidate маршрутизировать CHECK → DO; report-only
   улучшение вне утверждённого scope явно оставить unresolved/deferred с trigger. Не скрывать ни один
   unresolved finding и не расширять scope/criteria.
4. **Повторный CHECK.** При неизменившихся code inputs можно переиспользовать валидный report, но
   текущий CHECK всё равно заново оценивает его применимость, полноту и findings. Любое изменение
   затронутого кода после report делает затронутый анализ stale: driver запрашивает у executor его
   refresh; это evidence re-gather, а не автоматический product defect/attempt.
5. **Недостаток evidence.** Отсутствующий/неполный report, source observation или run сначала
   помечается `unverified`; driver организует bounded re-gather через существующую разрешённую роль
   (обычно executor для design report, scout для независимого наблюдения) и возвращает evidence на
   CHECK. Отсутствие evidence само по себе не доказывает defect, но незавершённый/невалидный review
   не может молча пройти или быть помечен PASS. Если mandatory evidence не получить, применяются
   базовые recovery/STOP правила.

CHECK не загружает skills, не читает product source, не запускает сканы и не создаёт субагентов.
Driver доставляет все применимые инструкции inline; недостающее наблюдение/evidence получает через
уже разрешённые роли. Права CHECK не расширяются.

Design/audit evidence охватывает общее ревью и применимые smells/API findings; CHECK выносит
независимое суждение medium-tier по §CHECK `pdca-dotnet`. Дифф и observation evidence организуются
по файлам/чанкам; самостоятельное исполнение scan/review в CHECK не требуется.

Общее ревью (из `dotnet-code-review-agent`), per-finding severity
Critical/Warning/Suggestion + роутинг специалистам:

- **Корректность:** баги/логические ошибки, необработанные исключения, null-проверки,
  async (sync-over-async, fire-and-forget без обработки), disposal ресурсов.
- **Стандарты:** нейминг, современный C# (pattern matching, target-typed new,
  collection expressions), NRT-аннотации, форматирование.
- **Архитектура:** несоответствие DI-лайфтаймов, нарушения слоёв, тесная связность,
  отсутствующие абстракции.
- **Perf red flags:** аллокации на горячем пути, LINQ в тесных циклах, неограниченный
  рост коллекций, N+1.
- **Security red flags:** SQL-инъекции (конкатенация/сырой SQL без параметров),
  отсутствие валидации ввода, hardcoded секреты, небезопасная десериализация,
  отсутствие авторизации.
- **Тесты:** есть ли тесты на изменённые пути; какие типы тестов нужны.
- **Роутинг** при глубоком анализе: async/`ValueTask`/pipelines → `dotnet-async-performance-specialist`;
  гонки/дедлоки → `dotnet-csharp-concurrency-specialist`; профилирование/GC →
  `dotnet-performance-analyst`; OWASP/крипто/секреты → `pdca-security-auditor`
  (`dotnet-security-reviewer` — для .NET-специфики).

Аудит smells/API (из `nextorm-code-auditor`). Регистры (правки — через `pdca-executor`):

- `docs/specs/design/code-smells-review.md` — подавления warning, корректность
  `IDisposable`, LINQ на горячем пути, god classes, cache hash keys, optional `null`.
- `docs/specs/design/API-NAMING-REVIEW.md` — нейминг public API (`P0`/`P1`/`P2`),
  XML-doc coverage, «Шаг 5 — закрепление» surface lock.

Дополнительно к общему §CHECK:

- **Подготовка audit skills/scans — не работа CHECK.** Если scope требует smell/API audit, DO или
  иной уже разрешённый gather загружает `dotnet-csharp` (baseline и `references/api-design.md`),
  `dotnet-api` (`references/api-surface-validation.md`) и `dotnet-testing`
  (`references/slopwatch.md`). По необходимости дополнительно загружает из соответствующего
  skill `dotnet-api/references/library-api-compat.md`, `dotnet-api/references/api-docs.md`,
  `dotnet-csharp/references/editorconfig.md`, `dotnet-csharp/references/nullable-reference-types.md`
  и `dotnet-tooling/references/add-analyzers.md`. Незарегистрированный skill и его references
  читаются из полного пакета по правилу DO выше; недоступное обязательное содержание фиксируется
  как blocker, а не подменяется утверждением о выполнении. Gather готовит report/наблюдения;
  CHECK получает инструкции inline и оценивает evidence, но не загружает skills и не сканирует source.
- **Счётчики обязательны.** Report suppression/slop содержит числа (suppressed vs justified) и ratio
  (напр. `0/6`, `12/15`), а не «пробежался». CHECK проверяет полноту этих чисел по supporting evidence;
  оценка без измеримого артефакта не принимается.
- **Самосертификация запрещена.** CHECK самостоятельно сличает report с candidate identity и
  независимыми source observations. Незакрытая variant row, неполный report или отсутствие
  счётчиков/ratio означают `unverified` и re-gather; действительный defect — CHECK → DO. Не принимать
  вывод «всё ОК» только по авторскому отчёту.
- **Автор ≠ заверитель регистра (nextorm).** Запись в `docs/specs/design/code-smells-review.md` /
  `docs/specs/design/API-NAMING-REVIEW.md` делает `pdca-executor`, но **заверяют** её независимые
  source observations, переданные driver, и CHECK: по каждому новому утверждению сверяется указанный
  `file:line` и доказательная опора; расхождение или само-переоценка (`deferred`/`acceptable` без
  опоры) — **находка**. CHECK не открывает product source. Общий принцип «non-author» — в
  `pdca-dotnet` §CHECK.
- Не переоткрывать `Отмечено, но менять не рекомендуется` / `Исключения (по решению
  автора)` / «Чистые категории».
- Optional-`null` референс: `TempTableExtensions.cs`
  (`ToTempTable`/`ToTempTableAsync`).
- Toolchain: CPM — версии только в `Directory.Packages.props`
  (`Microsoft.CodeAnalysis.Analyzers` 5.9.0); severity прибиты в `.editorconfig`
  (`dotnet_diagnostic.<ID>.severity`), `TreatWarningsAsErrors=true` → включённое
  правило валит сборку (gate: `dotnet build nextorm.slnx -c Release` = 0/0); inert
  Sonar `S*` без пакета `SonarAnalyzer` — мёртвые записи; `slopwatch` не установлен
  локально (`.config/dotnet-tools.json` — coverage/reportgenerator/docfx/dotnet-stryker): DO/gather
  либо готовит скан установленным инструментом, либо явно фиксирует ручной scan и его scope; CHECK
  оценивает evidence, но сам source не сканирует. Mutation testing — `dotnet-stryker`;
  public surface ещё не заперт (нет `PublicAPI.Shipped/Unshipped.txt`, ApiCompat,
  API-approval); baseline XML-doc и недокументированные public-типы — Appendix A
  `API-NAMING-REVIEW.md` (число берётся из реестра, не хардкодится); любой public
  rename доходит и до `docs/**`, и до `docs/ru/**`.
- **Границы CHECK.** CHECK не правит код или регистры. `pdca-executor` обновляет соответствующий
  registry только когда изменился finding/status/evidence; smell/API-аудит остаётся в пределах двух
  регистров выше. Renames/analyzer-policy и design fixes выполняются в DO, только если они входят в
  утверждённый scope. Profiling и выводы о производительности маршрутизируются профильному
  `nextorm-*-perf-analyst`; security и ASP.NET/HTTP — соответствующим специалистам.
- **Краевые ветки и покрытие (обязательно).** DO готовит variant matrix по фактическому diff/path;
  CHECK оценивает её через предоставленные observations, закрытие каждой строки (тест / guard /
  `deferred + триггер`) и дополняет фактическими ветками, включая edge-список сиблинга (готовые тесты
  существующего аналога, напр. `ToStream`/`ToTextReader`/`ToList`) и nextorm-класса (см. таблицу выше).
  DO готовит **branch delta** (не только line) по изменённым файлам/типам с явным denominator и
  применимое **mutation testing** (Stryker.NET) по изменённым типам; CHECK оценивает отчёты, выжившие
  мутанты убиты или обоснованы. Ветки без теста перечислить явно, не маскировать строчным процентом.
  Branch delta не заменяет project coverage gates ниже. Общий контракт (criteria/required evidence /
  PASS-гейт / итерации ≠ полнота) — `pdca-dotnet` §CHECK → «Test strategy». Ориентир дыры:
  непокрытая ветка value-type-сущности в #94 (dynamic columns).

#### Coverage evidence lifecycle (nextorm)

Это проектное уточнение к `pdca-coder` §Coverage evidence lifecycle и `pdca-dotnet` §Coverage
comparison and report reuse; прочие правила identity, повторного сбора и безопасности остаются
базовыми. При coverage-проверке передай CHECK этот подраздел и абзац «Связь `rv` и coverage-run»
ниже **целиком и verbatim inline**; не направляй CHECK открывать installed overlay или `AGENTS.md`.

- **Пороги и scope неизменны.** Whole-solution gate `coverage.settings.xml` по-прежнему охватывает
  `nextorm.{core,sqlite,postgres,sqlserver}` и требует `MIN_LINE_COVERAGE=85` /
  `MIN_BRANCH_COVERAGE=75`. Integration gate `coverage.integration.settings.xml` требует
  `MIN_INTEGRATION_LINE_COVERAGE=65` / `MIN_INTEGRATION_BRANCH_COVERAGE=75` на полном union
  `tests/nextorm.integration.tests` + `tests/nextorm.extensions.integration.tests` и настроенном
  denominator для assemblies `nextorm.{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse,entityframeworkcore,extensions}`;
  исключение `NextORM.Core.InMemory*` сохраняется. Используй объявленные команды и фильтры без
  изменений; обе проверки, как и прежде, hard-fail только на `main`. Branch delta не заменяет эти
  gates; явно предписанная global coverage
  campaign сохраняет свою команду и scope.
- **Локальная дельта и baseline.** Обычный DO inner loop — затронутые тесты без instrumentation.
  Обязательная branch delta — наблюдаемый результат для изменённых файлов/типов с указанным
  denominator и сопоставимыми baseline/candidate; это локальная диагностика/атрибуция, не полный
  gate. Исходники baseline и candidate обычно различаются. Переиспользуй baseline между `r`, `n` и
  сессиями, пока его собственные inputs и basis сравнения сопоставимы; один лишь новый `r`, `n`,
  session или `rv` не требует recollect. Когда coverage criterion/project gate применим, на границе
  stream/DO→CHECK собирай полную обязательную coverage scope на coherent candidate только если
  подходящего полного evidence нет или его валидность потеряна; CHECK может переиспользовать
  соответствующие Cobertura и full logs.
- **Integration union и отчёт.** Полное integration evidence включает оба raw Cobertura отчёта,
  собранных по `coverage.integration.settings.xml`, с существующими denominator/exclusions; один
  suite не закрывает gate. Храни raw reports неизменно; ReportGenerator запускай один раз после
  готовности полного релевантного набора на candidate/configuration. Пока inputs/configuration
  эквивалентны, переиспользуй raw reports и summary. При ошибке ReportGenerator исправь генерацию
  и пересобери summary из валидных неизменённых raw reports — не recollect и не rebuild их. Изменение
  только report/filter config также требует лишь новой summary, если collection inputs не изменились.
- **Инвалидация и сбои.** Изменения product/tests/build/provider/runtime/tool/coverage config
  инвалидируют только зависящие от них evidence; общий Core может влиять на несколько suite-отчётов,
  поэтому разбиение по проектам само по себе не доказывает независимость unit/integration или двух
  integration suites. Status/report-only правка не требует свежей сборки или collection. Сохраняй
  каждый failed run; retry допустим как infrastructure только при подтверждающей диагностике и
  доказанном восстановлении причины, пока evidence ещё нужен. Завершившийся assertion failure —
  результат теста, не infrastructure (`pdca-coder` §Test run safety).

### Доказательства для CHECK

Executor передаёт независимому CHECK утверждённый PLAN по точной ссылке, идентификатор проверенного
кандидата и ссылки на первичные результаты по критериям PLAN. Например, для критерия о покрытии
укажи конфигурацию, исходный отчёт покрытия и команду, которая его получила. CHECK оценивает
каждый критерий: `met`, `unmet` или `unverified`.

`rv` — ревизия контракта, а не номер запуска. Используй прежний результат, если его существенные
входы и условия соответствуют кандидату. Для сравнения с baseline укажи его собственную версию
и условия измерения отдельно.

**Проектные evidence-корни (nextorm).** Портативный агент `check` даёт scoped `read` на `.pdca/**`,
`TestResults/**` и на исторические `docs/specs/status/**`, `artifacts/**`; nextorm-специфику
объявляют **на стороне проекта**, а не в общем агенте/асете:
- статус/контракт — `.pdca/status/**` (новые циклы); `docs/specs/status/**` — историческое, read-only;
- evidence-артефакты — `.pdca/<task>/rv<k>/**` (новые циклы); `artifacts/**` — историческое evidence, read-only;
- тест-артефакты — `TestResults/**`;
- scratch вне worktree — `/tmp/nextorm*/**` (через `external_directory`, см. проектный `opencode.json`).


Применяй проектные проверки по таблице приоритетов классов. Безусловные проверки нельзя снимать через N/A.

**Недостающее evidence — не продуктовый дефект:** driver организует re-gather через уже разрешённые
роли в пределах закреплённого бюджета, без product FAIL только из-за отсутствия отчёта. CHECK
оценивает пополненное evidence; сама роль CHECK не запускает gather и не расширяет permissions.
Доказанный дефект требует FAIL; новый обязательный вариант — обоснованного CHECK → PLAN
с явным supersession ревизии контракта и сохранением обязательств.

Пока применимая обязательная строка открыта, PASS запрещён. При исчерпании бюджета CHECK
остаётся неразрешённым; применяются существующие правила блокеров/эскалации, без новой фазы.

### Перф-приёмка cached path

Обязательна для циклов, затрагивающих планирование запросов / plan cache,
executor / материализацию и выполнение запросов in-memory. Одна команда, без
контейнеров:

```
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance
```

- **Чья это предметная область.** In-memory (`InMemoryDataContext`, LINQ по спискам,
  `InMemoryBenchmark*`) — `nextorm-inmemory-perf-analyst`; ADO (`DataContext`, plan
  cache/`Prepare()`, `QueryExecutor`, SQL builders, `SqliteBenchmark*`) —
  `nextorm-db-perf-analyst`. Симптом на обоих контекстах — начинать с того, что
  первым на горячем пути, и назвать допущение.
- Приёмка обязательна для query-path изменений (план/cache/executor/материализация).
  Для in-memory execution набор напрямую покрывает только агрегаты `Count`/`GroupBy`;
  прочие in-memory изменения требуют дополнительных релевантных бенчмарков помимо набора.
- Убедиться, что команда выбрала хотя бы один кейс и выполнила все выбранные
  кейсы без пропусков и падений. Рядом с baseline
  (`docs/specs/performance/acceptance-benchmarks.md`) записать wall time, Mean и
  Allocated по каждому кейсу, а также cached/prepared ratio.
- Рост **comparable** cached-vs-prepared ratio больше **20 %** относительно baseline —
  повод расследовать и записать исход; это **не** автоматический hard fail. Baseline
  содержит высокую погрешность измерения (у `Nextorm_Count` `Error` > `Mean`), поэтому
  расследование обязано учитывать разброс прогон-к-прогону: повторить на той же машине,
  при необходимости — реплицировать, и зафиксировать исход «шум / воспроизвелось».
- Несопоставимые прогоны (другая ОС/CPU/config/набор кейсов, `NEXTORM_BENCH_FULL=1`,
  out-of-process vs in-process toolchain) и шумовые различия регрессией **не называть**.
- Цель стоимости: **≤4 мин** wall clock на текущем хосте для `ShortRun`. Превышение —
  провал gate: зафиксировать измеренное время; критерий молча не менять.
- Для несвязанных documentation-only циклов приёмка не требуется.
- **Параметро-биндинг / TVP (per-row путь).** Если цикл трогает биндинг параметров
  или TVP, DO явно решает вопрос замера: либо прогон/бенчмарк, либо аргумент с `file:line`,
  что изменение one-time/per-column, а не в цикле по строкам (per-row циклы:
  `Project`/`BuildRows`/`WriteJson`/`ToTypedArray`). CHECK оценивает это evidence; «не нужен» без
  аргумента не принимается, а per-row работу без замера не закрывает.

Выход: русский, H2/H3, `P0/P1/P2` или `Находка N` с `Было`/`Стало`/`Проверка`;
вперёд — baseline сборки + suppression ratio; в конце — summary table и
non-determinism disclaimer.

## ACT: nextorm-гигиена планов

- **Полностью реализованный TODO удаляется.** Как только все фазы/пункты плана
  `docs/specs/roadmap/todo_<feature>.md` реализованы и его tracking issue закрыт,
  файл плана **удаляется** (`rm`) в том же цикле ACT, а не оставляется «на память».
  Частично реализованный план (остались отложенные фазы/провайдеры) сохраняется.
- **Связанный issue закрывается в автономном режиме с автокомитом.** Если пользователь явно
  запросил автономную работу с автокоммитом (`pdca-dotnet` §Autonomous mode → «Auto-commit»),
  то, когда вся задача завершена (все её циклы/срезы), `pdca-executor` сразу после ACT-коммита закрывает
  её связанный GitHub-issue: `gh issue close <N> --repo <owner/repo> --comment "<итог + ссылка на
  проверку>"`, где `<N>` — из статус-файла/префикса коммита (`#N`). При сплите или оставшихся
  срезах issue остаётся открытым (инвариант 8), остаток идёт в статус-файл и финальный отчёт.
  В ручном режиме (без явного автокомита) и при блокере/STOP issue **не** закрывается. Сбой
  закрытия (сеть/доступ) — `Notice:` в логе и в финальном отчёте, без ложного «закрыто»; push
  не выполняется.
- Закрытие фиксируется в `docs/specs/design/API-NAMING-REVIEW.md` §«Закрытие планов»:
  путь удалённого файла + закрытый tracking issue; живой трекинг ведут этот реестр и
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`.
- Ссылки на удалённый план (`[label](todo_<feature>.md)`) **де-линкуются** по всему
  `docs/**` — остаётся только текст-label, чтобы не оставлять битых относительных
  ссылок. Инлайновые упоминания в исторических записях (audit/status/release) — это
  исторические артефакты, переписывать прозу не нужно.
- `rm` и правки регистров делает `pdca-executor` в фазе Do/ACT; CRLF сохраняется.
- **Milestone при сплите не меняется.** Частично реализованный план сохраняется (см. выше),
  а его оставшиеся фазы/провайдеры остаются в **текущем** milestone: новые issue/срезы
  наследуют milestone исходной задачи. Создавать issue с будущим milestone или переносить туда
  остаток — запрещено (инвариант 8). Заблокированный срез остаётся открытым пунктом плана в
  текущем milestone с триггером пересмотра, а не выносится за его пределы.
- **Перед закрытием потока.** Сам статус-файл удаляется по глобальному контракту (§ACT →
  «Status-file lifetime»); nextorm-специфика — оставшиеся открытыми пункты `Deferred + триггер`
  предварительно переносятся в живой трекинг: регистры `docs/specs/design/*`,
  `docs/specs/roadmap/todo_*.md`, issue.
