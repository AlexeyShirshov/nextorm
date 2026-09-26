# nextorm: PDCA-оверлей (Plan/Check)

Локальные уточнения к глобальному PDCA-контракту oc-dev. Действуют в этом
репозитории поверх общих §PLAN/§CHECK.

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

## CHECK: nextorm-аудит ⊇ общее ревью (nextorm-code-auditor + dotnet-code-review-agent)

Аудитор — суперсет: сначала общий ревью-проход, затем аудит smells/API. Оба такта
идут в **Gather** (DeepSeek + команды), Opus только триажит сводку (§CHECK
глобального профиля). Дифф режется по файлам/чанкам и ревьюится отдельно.

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
  `dotnet-performance-analyst`; OWASP/крипто/секреты → `dotnet-security-reviewer`.

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

Выход: русский, H2/H3, `P0/P1/P2` или `Находка N` с `Было`/`Стало`/`Проверка`;
вперёд — baseline сборки + suppression ratio; в конце — summary table и
non-determinism disclaimer.
