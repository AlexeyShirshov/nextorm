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
  **НЕ вызываются**: их инструкции встроены ниже в фазы Plan/Check.
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

Выход Plan: severity (🔴/🟡/ℹ️), `file:line`, one-line fix, применимый инвариант
(1–7); split **fix now** vs **deferred с триггером**; lens-префикс
(`[SRP]`/`[OCP]`/`[LSP]`/`[ISP]`/`[DIP]`/`[DRY]`/`[TYPE]`/`[PERF]`).

## DO: формирование гайдов (документация)

- **Крупный блок новой функциональности выносится в отдельный гайд.** Если большой
  законченный блок новой функциональности не относится ни к одному существующему
  гайду, он оформляется **своим гайдом** (`docs/<тема>/index.md` + нумерованные
  страницы + `toc.yml`), а не дописывается в чужой гайд.
- **Гайд > 15 подразделов — выделять в отдельный блок.** Когда гайд разрастается
  больше чем на 15 подразделов, его выделяют в отдельный гайд-блок — как это сделано
  с гайдом scalar functions (`docs/scalar-functions/`).

## CHECK: nextorm-аудит ⊇ общее ревью (nextorm-code-auditor + dotnet-code-review-agent)

Аудитор — суперсет: сначала общий ревью-проход, затем аудит smells/API. Оба такта
идут в **Gather** (DeepSeek + команды); **судит сводку `check` (GPT-6 Sol)** в такте 2
(§CHECK контракта `pdca-dotnet`). Дифф режется по файлам/чанкам и ревьюится отдельно.

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
- Закрытие фиксируется в `docs/specs/design/API-NAMING-REVIEW.md` §«Закрытие планов»:
  путь удалённого файла + закрытый tracking issue; живой трекинг ведут этот реестр и
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`.
- Ссылки на удалённый план (`[label](todo_<feature>.md)`) **де-линкуются** по всему
  `docs/**` — остаётся только текст-label, чтобы не оставлять битых относительных
  ссылок. Инлайновые упоминания в исторических записях (audit/status/release) — это
  исторические артефакты, переписывать прозу не нужно.
- `rm` и правки регистров делает `coder` в фазе Do/ACT; CRLF сохраняется.
- **Перед закрытием потока.** Сам статус-файл удаляется по глобальному контракту (§ACT →
  «Status-file lifetime»); nextorm-специфика — оставшиеся открытыми пункты `Deferred + триггер`
  предварительно переносятся в живой трекинг: регистры `docs/specs/design/*`,
  `docs/specs/roadmap/todo_*.md`, issue.
