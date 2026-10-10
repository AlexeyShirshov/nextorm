---
name: nextorm-pdca
description: "Проектный оверлей nextorm к глобальному PDCA-скиллу `pdca-dotnet`: инварианты дизайна (Plan), nextorm-специфика аудита (регистры, toolchain, перф-приёмка cached path) и гигиена планов (Act). Загружать вместе с `pdca-dotnet`, до фазы PLAN, когда цикл ведётся в этом репозитории — вместе они дают полный контракт."
---

# nextorm: PDCA-оверлей (Plan/Check)

Проектные дополнения к глобальному скиллу **`pdca-dotnet`**. Загружай их **вместе** с ним и
**до PLAN**: сначала `pdca-dotnet` (машина состояний, гейты, делегирование), затем этот
оверлей. Здесь — только nextorm-специфика: инварианты, регистры, перф-приёмка и гигиена
планов. Гейты и машину состояний оверлей **не меняет** — уточняет содержание фаз.

## Политика агентов

- `nextorm-code-auditor`, `nextorm-design-engineer`, `nextorm-*-perf-analyst`
  **НЕ вызываются как агенты**: их предметные инструкции встроены ниже в фазы Plan/Check.
- Встраивание **не отменяет** общий §CHECK глобального `pdca-dotnet`: воркфлоу CHECK (шаги 1–13),
  **загрузка скиллов аудита** (шаг 1, список — §CHECK контракта) и **обязательный скан
  suppression/slop со счётчиками и ratio** (шаг 4), а также per-finding вывод (шаг 8), выполняются
  в полном объёме. Оверлей ниже — только проектное **дополнение** к ним, а не сокращённая замена.
- Записи в регистры (`docs/specs/design/*`) и правки кода/конфигов делает `coder`
  в фазе Do, не оркестратор.

## PLAN: nextorm-инварианты (из nextorm-design-engineer)

Приоритетнее generic-советов; ограничивают fix, а не только review.

1. **Абстракция только на втором потребителе / наблюдаемом шве.** Не предлагать
   `IX`/`X` «потому что чище»: при единственном потребителе сплит запрещён
   (`solid-review.md` F12; F3/F7 — `IConnectionFactory`, `IColumnMapper`).
2. **KISS = число концептов.** Новый тип/интерфейс/индирекция обязан назвать
   конкретного потребителя, которого разблокирует.
3. **Measure, never assert.** «Аллоцирует / медленнее» — только из кода (аллокация
   на per-row пути) или в пользу perf-аналитика. Дельты <~20% — шум на этой машине
   (разброс до 19%; `docs/specs/performance/performance-findings.md`).
4. **Не переоткрывать settled findings без нового измерения.** Регистры смотреть
   on-demand (только нужную находку): `docs/specs/design/solid-review.md` (F1..F13),
   `docs/specs/design/code-smells-review.md`, `docs/specs/design/API-NAMING-REVIEW.md`
   (P0-n), `docs/specs/performance/*` (M*/I*/R*).
5. **Public API — сквозной.** Rename/remove публичного типа/члена требует и
   `docs/**`, и `docs/ru/**` в том же изменении (AGENTS.md).
6. **Build discipline задаёт fix.** `TreatWarningsAsErrors=true`, nullable, CRLF,
   CPM (`Version` только в `Directory.Packages.props`). Изменение с warning — не fix.
7. **Известные факты архитектуры не репортить заново:** `DbContext` — тонкий фасад
   над `QueryExecutor`/`QueryPlanner`/`DbConnectionManager`/`ContextEnvironment`/
   `QueryCache`; `InMemoryContext` делегирует фасад `InMemoryQueryExecutor`;
   `ISqlDialect` — намеренно 97-членный композит (F12). Длины файлов читать из
   текущего дерева.
8. **Ничего не выносится за текущий milestone.** Если unit/фаза не решается целиком в
   этом цикле, её **можно засплитить**, но все новые задачи/срезы/tracking-issue
   остаются в **том же milestone**, что и исходная. Перенос в следующий milestone
   (например `1.1`) запрещён: нереализованный срез — это отдельный unit/issue с
   текущим milestone, а не `deferred` в будущий релиз. Запись ограничения в
   `docs/advanced/limitations.md` фиксирует поведение, но **не заменяет** задачу в
   текущем milestone.
9. **PLAN отвечает за качество плана, а не за качество входа.** Сырой материал (расплывчатый issue,
   неполные критерии, без списка краевых случаев) PLAN **доводит**: выводит/дополняет критерии приёмки
   (наблюдаемое поведение + негативный кейс), строит **variant matrix** из **пути исполнения** (а не из
   формулировки требований) и закрывает все строки, поднимает невысказанные неизвестные. Незавершённость
   в DO не транслируется. Пропущенная ветка — это **дефект PLAN**: гейт 1 не проходит, а при
   обнаружении в CHECK — **loop-back CHECK → PLAN**, а не сноска в отчёте. Слабая постановка
   **повышает** планку PLAN, а не понижает. Контракт: `pdca-dotnet` §PLAN → «PLAN owns the quality of the plan».
10. **Поиск — ignore-aware; символы — `roslyn`.** Для C#-символов (типы/члены/`refs`/`callers`/overloads/
    implementations/renames) — только инструмент `roslyn`, не текстовый поиск. Для текста (доки,
    комментарии, строковые литералы, имена SQL) — `rg`/`git grep` с ограничением по исходникам
     (`rg -n --glob '*.cs' "<lit>" src tests`). **Никогда `grep -r`/`grep -rn`/`find`** по дереву: они
     игнорируют `.gitignore` и читают `bin/`/`obj/`/`TestResults/` (≈1.3 GB артефактов под `src`/`tests`),
     т.е. ~1 GB диска на один вызов. См. `AGENTS.md` → «Searching text».
11. **Недоказуемый критерий — GAP, а не `ready`.** Критерий, чьё решающее evidence невоспроизводимо из
    закоммиченных/декларируемых источников (напр. «красное» плечо red↔green есть только в
    отклонённом/незакоммиченном артефакте, или probe не исполняется против baseline), — это gap:
    `planner` **переопределяет оракул** (или задача остаётся gap'ом), а не тащит его в DO как
    «in-cycle prerequisite». Получаемость решающего probe **демонстрируется до фиксации плана**.
12. **Повторный заход после терминального `incomplete`/STOP** не перезапускает провалившийся критерий
    без изменений: прежняя терминальная причина — обязательный вход PLAN; `planner` либо показывает,
    что доказательство теперь получаемо (новое evidence/починенный харнесс), либо переформулирует
    критерий (переопределяет оракул).

Выход Plan: severity (🔴/🟡/ℹ️), `file:line`, one-line fix, применимый инвариант
(1–10); split **fix now** vs **deferred с триггером** (deferred остаётся в текущем
milestone, см. инвариант 8); lens-префикс
(`[SRP]`/`[OCP]`/`[LSP]`/`[ISP]`/`[DIP]`/`[DRY]`/`[TYPE]`/`[PERF]`).

При изменении пути исполнения PLAN фиксирует **variant matrix** (оси: тип/`null`/отсутствующий ctor/
проекция/провайдер/флаги; каждая строка — `тест` / `guard` / `deferred + триггер`) и план **branch**-дельты
и **mutation**-тестинга. Контракт — `pdca-dotnet` §PLAN → «Test strategy»; проверка — в CHECK
(см. «Краевые ветки и покрытие»).

### Класс-приоритет nextorm (по умолчанию)

Общий принцип «приоритет фиксирует PLAN, CHECK применяет его и не назначает заново» — в
`pdca-dotnet` §PLAN («PLAN assigns priority»). Ниже — nextorm-классы и их обязательные строки
(**P1 по построению**): CHECK закрывает каждую (тест / guard / `deferred + триггер`) либо объявляет
находку. Для класса, которого в таблице нет, строки авторит PLAN. Таблица — не потолок:
PLAN/CHECK вправе **добавлять** строки, но не удалять обязательные.

| класс изменения | обязательные строки (P1 по построению) |
|---|---|
| стриминговый терминал | обе поверхности (`QueryCommand<TResult>` + `EntityBuilder<TEntity>`); sync **и** async; `CancellationToken` у async; `params`-паритет с сиблингами; fail-closed на неподдержанной форме; владение ресурсами (приёмник не закрывать/не `Dispose`; flush-семантика); покрытие провайдеров |
| новая SQL-функция/оператор | паритет формы с SQL-сиблингом; capability-флаг по диалектам; тест на каждом провайдере; nullable-семантика |
| изменение query-path / plan cache | cached-vs-prepared ratio (перф-приёмка); не мутировать shared `QueryCommand`; покрытие temp-table/TVP-веток |

## DO: формирование гайдов (документация)

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

## DO: применение фиксов (coder)

Фиксы (из PLAN-ревью, из design/type/perf-линз или из CHECK loop-back) применяет `coder` по
общему «Coder editing discipline» (`pdca-dotnet` §Delegation), с nextorm-конкретикой:

- **Одна ось за шаг**, затем сборка+тесты — не паковать несвязанные рефакторы.
- **Сборка — гейт:** `dotnet build nextorm.slnx -c Debug` = `0 Warning(s) 0 Error(s)`
  (`TreatWarningsAsErrors=true`); изменение с warning — не фикс.
- **CRLF всегда.** После правки любого файла: `perl -pi -e 's/\r?\n/\r\n/g' <file>`;
  LF-only/смешанные окончания не оставлять.
- **Тесты до «готово»:** `dotnet test tests/<project> -c Debug`; интеграционные — по
  `.opencode/skills/running-integration-tests/SKILL.md` (без `DOCKER_HOST` прогон со
  `Skipped: 376` — это не pass).
- **CPM:** версии только в `Directory.Packages.props`.
- **Никаких `git push`/коммитов** без явной просьбы (AGENTS.md).
- **BDN-артефакты трекаются:** после бенчмарка вернуть `benchmarks/BenchmarkDotNet.Artifacts`.
- **Без бенчмарк-выводов** (дельты <~20% — шум); числа — на perf-аналитика (`task`).

## CHECK: nextorm-аудит ⊇ общее ревью (nextorm-code-auditor + dotnet-code-review-agent)

Аудитор — суперсет: сначала общий ревью-проход, затем аудит smells/API. Оба такта
идут в **Gather** (DeepSeek + команды); **судит сводку `check` (GPT-6 Sol)** в такте 2
(§CHECK контракта `pdca-dotnet`). Дифф режется по файлам/чанкам и ревьюится отдельно.
Встраивание — это перенос **содержания**, а не понижение планки: проектные скиллы аудита
(шаг 1) и измеримые счётчики/ratio (шаг 4) обязательны при любой модели `check` (см. ниже).
Молча принимать «всё ОК» от слабого `check` запрещено.

Общее ревью (из `dotnet-code-review-agent`), per-finding severity
Critical/Warning/Suggestion + роутинг специалистам:

- **Корректность:** баги/логические ошибки, необработанные исключения, null-проверки,
  async (sync-over-async, fire-and-forget без обработки), disposal ресурсов.
- **Стандарты:** нейминг, современный C# (pattern matching, target-typed new,
  collection expressions), NRT-аннотации, форматирование.
- **Архитектура:** несоответствие DI-лайфтаймов, нарушения слоёв, тесная связность,
  отсутствующие абстракции.
- **Perf red flags:** аллокации на горячем пути, LINQ в тесных циклах, неограниченный
  рост коллекций, N+1, отсутствие `AsNoTracking()` на read-only запросах.
- **Security red flags:** SQL-инъекции (конкатенация/сырой SQL без параметров),
  отсутствие валидации ввода, hardcoded секреты, небезопасная десериализация,
  отсутствие авторизации.
- **Тесты:** есть ли тесты на изменённые пути; какие типы тестов нужны.
- **Роутинг** при глубоком анализе: async/`ValueTask`/pipelines → `dotnet-async-performance-specialist`;
  гонки/дедлоки → `dotnet-csharp-concurrency-specialist`; профилирование/GC →
  `dotnet-performance-analyst`; OWASP/крипто/секреты → `security-auditor`.

Аудит smells/API (из `nextorm-code-auditor`). Регистры (правки — через `coder`):

- `docs/specs/design/code-smells-review.md` — подавления warning, корректность
  `IDisposable`, LINQ на горячем пути, god classes, cache hash keys, optional `null`.
- `docs/specs/design/API-NAMING-REVIEW.md` — нейминг public API (`P0`/`P1`/`P2`),
  XML-doc coverage, «Шаг 5 — закрепление» surface lock.

Дополнительно к общему §CHECK:

- **Скиллы аудита обязательны (шаг 1 §CHECK).** Грузятся: `dotnet-csharp-code-smells`,
  `slopwatch`, `dotnet-api-surface-validation`, `api-design` (+ on-demand:
  `dotnet-library-api-compat`, `dotnet-editorconfig`, `dotnet-add-analyzers`, `dotnet-api-docs`,
  `dotnet-csharp-nullable-reference-types`). CHECK без загруженных профильных скиллов — незачётный.
- **Счётчики обязательны (шаг 4 §CHECK).** Отчёт suppression/slop содержит числа
  (suppressed vs justified) и ratio (напр. `0/6`, `12/15`), а не «пробежался». Суждение без
  измеримого артефакта не принимается.
- **Само-сертификация запрещена.** Если `check` в текущей конфигурации не на strong-модели,
  CHECK не имеет права закрывать цикл «на слово»: незакрытая строка variant matrix, отсутствие
  счётчиков/ratio или невыполненный шаг 1 — это **loop-back CHECK → PLAN/DO**, а не запись «done».
- **Автор ≠ заверитель регистра (nextorm).** Запись в `docs/specs/design/code-smells-review.md` /
  `docs/specs/design/API-NAMING-REVIEW.md` делает `coder`, но **заверяет** её независимый
  read-only поток CHECK: по каждому новому утверждению открывает указанный `file:line` и сверяет с
  кодом; расхождение или само-переоценка (`deferred`/`acceptable` без опоры на код) — **находка**.
  Общий принцип «author ≠ certifier» — в `pdca-dotnet` §CHECK.
- Не переоткрывать `Отмечено, но менять не рекомендуется` / `Исключения (по решению
  автора)` / «Чистые категории».
- Optional-`null` референс: `TempTableExtensions.cs`
  (`ToTempTable`/`ToTempTableAsync`).
- Toolchain: CPM — версии только в `Directory.Packages.props`
  (`Microsoft.CodeAnalysis.Analyzers` 5.9.0); severity прибиты в `.editorconfig`
  (`dotnet_diagnostic.<ID>.severity`), `TreatWarningsAsErrors=true` → включённое
  правило валит сборку (gate: `dotnet build nextorm.slnx -c Release` = 0/0); inert
  Sonar `S*` без пакета `SonarAnalyzer` — мёртвые записи; `slopwatch` не установлен
  локально (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx) —
  либо поставь, либо скажи, что сканировал вручную; public surface ещё не заперт
  (нет `PublicAPI.Shipped/Unshipped.txt`, ApiCompat, API-approval); baseline XML-doc
  и 109 недокументированных public-типов — Appendix A `API-NAMING-REVIEW.md`; любой
  public rename доходит и до `docs/**`, и до `docs/ru/**`.
- Границы: править только два регистра; renames/analyzer-policy — в Do; out of scope:
  profiling (`nextorm-*-perf-analyst`), security, ASP.NET/HTTP, дизайн-рефакторинг
  сверх smell-отчёта.
- **Краевые ветки и покрытие (обязательно).** По диффу: есть ли **variant matrix** и закрыта ли
  каждая строка (тест / guard / `deferred + триггер`); **CHECK дополняет** матрицу строками, которых
  план не назвал, — по edge-списку **сиблинга** (готовые тесты существующего аналога,
  напр. `ToStream`/`ToTextReader`/`ToList`) и по nextorm-классу (см. «Класс-приоритет nextorm»).
  Отчитана **branch**-дельта (не только line); прогнан **mutation testing** (Stryker.NET) по
  изменённым типам — выжившие мутанты убиты или обоснованы. Ветки без теста перечислить явно, не
  маскировать строчным процентом. Общий контракт (PLAN авторит / CHECK дополняет / PASS-гейт /
  итерации ≠ полнота) — `pdca-dotnet` §CHECK → «Test strategy». Ориентир-пример дыры: непокрытая
  ветка value-type-сущности в #94 (dynamic columns).

### Обязательный evidence contract

**PLAN обязан закрепить контракт до DO:** применяются разделы «Versioned evidence contract»
и «CHECK completeness gate — mandatory evidence contract» глобального скилла `pdca-dotnet`.
Этот оверлей не вводит отдельную схему контракта.

**Frozen evidence manifest (nextorm).** Путь — базовый дефолт `pdca-dotnet` §«Versioned evidence
contract» (`artifacts/pdca/<task>/rv<k>/manifest.json`, `<task>` — `D<issue>`/kebab-slug,
`<k>` — ревизия контракта `rv`); nextorm его **не переопределяет** и не дублирует. Схема — там же
(оверлей схему не дублирует); валидируется in-repo
`python3 scripts/validate_inner_loop.py manifest <путь>` (покрытие `required_rows` + форма строк и
evidence). Пишет его DO на границе DO→CHECK; CHECK открывает его первым и по каждой строке читает
`path`.

**Проектные evidence-корни (nextorm).** Портативный агент `check` даёт scoped `read` только на
generic-корни (`docs/specs/status/**`, `artifacts/**`, `TestResults/**`); nextorm-специфику
объявляют **на стороне проекта**, а не в общем агенте/ассете:
- статус/контракт — `docs/specs/status/**`;
- evidence-артефакты — `artifacts/**` (в т.ч. `artifacts/pdca/**`);
- тест-артефакты — `TestResults/**`;
- scratch вне worktree — `/tmp/nextorm*/**` (через `external_directory`, см. проектный `opencode.json`).


Строки проектной таблицы приоритетов классов — строки контракта с обязательными слотами
и сохранёнными приоритетами. Безусловные проектные проверки нельзя снимать через N/A.

**Недостающее evidence — не продуктовый дефект:** его дособирают внутри CHECK в пределах
закреплённого бюджета, без итерации DO и без product FAIL только из-за отсутствия отчёта.
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

- Приёмка обязательна для query-path изменений (план/cache/executor/материализация).
  Для in-memory execution набор напрямую покрывает только агрегаты `Count`/`GroupBy`;
  прочие in-memory изменения требуют дополнительных релевантных бенчмарков помимо набора.
- Убедиться, что выполнено ровно **7** кейсов и нет падений; рядом с baseline
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
  или TVP, PLAN обязан явно решить вопрос замера: либо прогон/бенчмарк, либо аргумент
  с `file:line`, что изменение one-time/per-column, а не в цикле по строкам
  (per-row циклы: `Project`/`BuildRows`/`WriteJson`/`ToTypedArray`). «Не нужен» без
  аргумента — gate 1 не проходит; per-row работу без замера цикл не закрывает.

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
  то, когда вся задача завершена (все её циклы/срезы), `coder` сразу после ACT-коммита закрывает
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
- `rm` и правки регистров делает `coder` в фазе Do/ACT; CRLF сохраняется.
- **Milestone при сплите не меняется.** Частично реализованный план сохраняется (см. выше),
  а его оставшиеся фазы/провайдеры остаются в **текущем** milestone: новые issue/срезы
  наследуют milestone исходной задачи. Создавать issue с будущим milestone или переносить туда
  остаток — запрещено (инвариант 8). Заблокированный срез остаётся открытым пунктом плана в
  текущем milestone с триггером пересмотра, а не выносится за его пределы.
- **Перед закрытием потока.** Сам статус-файл удаляется по глобальному контракту (§ACT →
  «Status-file lifetime»); nextorm-специфика — оставшиеся открытыми пункты `Deferred + триггер`
  предварительно переносятся в живой трекинг: регистры `docs/specs/design/*`,
  `docs/specs/roadmap/todo_*.md`, issue.
