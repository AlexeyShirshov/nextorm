# T233 (#233) — D233-window-over-overload, cycle N=1

## Identity and state

- task_id: `T233`; issue: `#233`
- title: «Window functions: перегрузка `Over` с одним `partitionBy` и одним `WindowOrder` (без массивов)»
- selected_variant: pdca-dotnet
- cycle: `N=1`
- plan_revision: `r=1`
- iteration: `n=1/3` (not started)
- plan_state: `ready`
- evidence_contract: `rv=1` (frozen)
- milestone: `1.0.9-rc3`; collection run: `1.0.9-todo`; group: `G1` (single group — no worktree, no branches, no merge, current worktree/branch)
- phase: `PLAN` complete → awaiting DO
- mode: autonomous collection with authorized auto-commit (explicit paths, `#233` prefix, never `git add -A`, never push)
- status file: `docs/specs/status/D233-window-over-overload-1.md`

## Progress log

2026-10-14T17:41:57Z | PLAN | r1 | n1/3 | PLAN r=1 persisted by coder (PLAN-only step); evidence contract rv=1 frozen; DO not started (PLAN→DO boundary). | docs/specs/status/D233-window-over-overload-1.md

## Collection handoff

- `plan_state: ready`
- `plan_revision: r1`
- `evidence_contract_revision: rv1`
- `planned_writes_public_api: src/nextorm.core/Query/WindowFunctions.cs (PUBLIC API addition — add one overload)`
- `planned_writes_other`: `tests/nextorm.core.tests/WindowFunctionMarkerTests.cs`; `tests/nextorm.sqlite.tests/SqlGenerationTests.cs`; `tests/nextorm.postgres.tests/SqlGenerationTests.cs`; `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`; `tests/nextorm.mysql.tests/SqlGenerationTests.cs`; `tests/nextorm.mariadb.tests/SqlGenerationTests.cs`; `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`; `tests/nextorm.integration.tests/CommonTestSuite.Window.cs`; `docs/guide/09-window-functions.md`; `docs/ru/guide/09-window-functions.md`; `docs/specs/design/API-NAMING-REVIEW.md`; `docs/specs/status/D233-window-over-overload-1.md`
- `prerequisites`: (1) existing user approval covers **only the plan** — a separate recorded approval is required before any implementation edit; (2) T233 runs **after** T232 within G1 — requires the T232 handoff (final diff + public-API/worker/capability changes), no parallel edits to `Query/WindowFunctions.cs` / `Builders/**` / `Dialect/**`; (3) mandatory compiler compatibility probe (baseline→candidate) for untyped `default`/`null` at the old call sites must pass before the public API edit; (4) required containers/toolchain must be available (integration + coverage).
- `requirements_on_siblings`: T232 completes and is certified in G1 / `1.0.9-rc3`; hands off its final diff; stops touching the shared window builder / `Builders/**` / `Dialect/**`; if T232 already added an overlapping signature or changed the arg mapper/capability boundary, T233 returns to PLAN (no duplication).
- `all_barrier_contribution`: T233 PLAN complete = plan_state ready; the task's contribution to the collection ALL-barrier for the PLAN phase is satisfied (this run does not execute DO).
- `next_allowed_step`: `DO (in a later, separately authorized run) — unit D:233.1 (gate + compatibility probe); no implementation before recorded approval`

## PLAN r=1

---
# T233 — PLAN r=1

**Краткая сводка**
- **Решение:** план r=1 подготовлен; evidence contract **rv=1 заморожен**.
- **DO запрещён:** текущий запуск — PLAN only; отдельно требуется утверждение этого плана.
- **Дизайн:** один публичный marker-overload, без изменения SQL renderer.
- **Зависимость:** T233 выполняется **после T232** в G1, в текущем дереве.
- **Основной риск:** совместимость вызовов с нетипизированным `default` должна быть доказана; отсутствие defaults у первых параметров само по себе этого не доказывает.

## 1. Goal; scope / non-goals

### Цель

Позволить выразить окно с **одним partition expression и одним `WindowOrder` без пользовательских массивов**, сохранив SQL, существующее поведение и ограничения провайдеров.

### В scope

- Один overload `WindowFunction<T>.Over`.
- Marker-, compiler-compatibility-, dialect- и integration-проверки.
- EN/RU руководства, реестр API.
- Доказательства, статус и независимая CHECK-сертификация.

### Не в scope

- Новые SQL-функции, направления сортировки, `NULLS FIRST/LAST`.
- Новые capability flags или расширение поддержки окон.
- Изменение существующих overloads, translator, planner, executor, cache.
- Массовая миграция существующих array-call sites.
- Новые branches/worktrees, merge или push.
- Настройка PublicApiAnalyzers/ApiCompat для всего проекта.

**Freeze/public API:** новая сигнатура — аддитивное изменение публичной поверхности, но не автоматически source-compatible изменение. До отдельного утверждения плана разрешена только его фиксация в статусе; правки реализации, тестов, публичных документов и реестра запрещены. Этот запуск заканчивается на PLAN→DO и **DO не начинает**.

## 2. Acceptance criteria

| ID | Наблюдаемый критерий | Негативный случай |
|---|---|---|
| **R233-1** | Для одинаковых partition/order/frame scalar- и array-формы дают **побайтно одинаковую SQL-строку** на всех шести dialect-проектах. Проверяются `asc`, `desc`, `partitionBy: null`, nullable partition. | Потеря partition/order/frame, смена направления или любое различие SQL — fail. |
| **R233-2** | Существующие сигнатуры неизменны; старые 39 call sites собираются без изменений. Типизированные expression/order/array/named/optional формы сохраняют binding и поведение. Differential compiler-probe сохраняет успешность ранее допустимых `null`/`default` вызовов. | Новый CS0121, смена выбранного overload или необходимость «починить» старый вызов — fail, возврат DO→PLAN. |
| **R233-3** | `frame:` поддерживается при позиционных и именованных первых аргументах; `WindowFrame.Rows` даёт ту же SQL и результат, что array-форма. `Groups`/exclusion не получают новых возможностей. | Frame игнорируется, неверно отображается или обходит существующее ограничение провайдера — fail. |
| **R233-4** | Build: exit 0, **0 warnings / 0 errors**. Обязательные тесты проходят; контейнерные провайдеры реально исполнены. Coverage по `coverage.settings.xml`: **line ≥85%, branch ≥75%**, независимо от warning-only режима RC-ветки. | Недобор любого порога, skipped обязательного провайдера, неполный отчёт или warning — не pass. |
| **R233-5** | EN и RU показывают scalar-форму, array-эквивалент и `frame:`. Реестр фиксирует точную публичную сигнатуру и ограничения совместимости; отдельный CHECK-поток сертифицирует запись. | Только один язык, неверная optionality, устаревшая ссылка в затронутой записи или самосертификация автором — fail. |

**Доказуемость:** перечисленные критерии проверяемы. Для R233-2 есть **открытый compatibility gap**, а не доказательство совместимости: нетипизированный `default` может сделать новый overload конкурентом старому expression-overload. Этот риск **не разрешает ослабить R233-2**.

## 3. Chosen design — r=1

Добавить в `src/nextorm.core/Query/WindowFunctions.cs:16–57`:

```csharp
public T Over(
    Expression<Func<object?>>? partitionBy,
    WindowOrder orderBy,
    WindowFrame? frame = null)
```

Первые два параметра **обязательны**. Имена `partitionBy`, `orderBy`, `frame` являются частью контракта с существующим argument mapper.

Реализация — marker, возвращающий `default` так же, как соседние overloads. Не вызывать array-overload, не создавать массивы, не исполнять expressions.

### Обоснование binding

- `:28`: типизированный `WindowOrder` не является `Expression<Func<object?>>`; `Over(expr, expr)` остаётся старой формой.
- `:34`: форма order-only не конкурирует с новым обязательным partition.
- `:42`: named-window форма не меняется.
- `:49`: требуется массив order.
- `:57`: требуются массивы; типизированные array-вызовы не конкурируют.
- `Over()`, `Over(frame: ...)`, partition-only вызовы не получают нового applicable candidate.
- **Нетипизированные `default` и, в зависимости от типа `WindowOrder`, `null` требуют отдельного compiler-probe.** Разведочное заключение об отсутствии ambiguity для типизированных аргументов нельзя расширять на эти литералы.

### Translator

Изменения **не нужны**:

- `Visitors/WindowSql.cs:139–171` распределяет аргументы по именам.
- `WindowSql.cs:63–77,102–120` уже обрабатывает single expressions/order.
- `Visitors/WindowFunctionTranslator.cs:29–33` распознаёт методы, объявленные на `WindowFunction<T>`.
- Общий rendering остаётся `WindowFunctionTranslator.cs:107–179`.

Если тест обнаружит необходимость правки worker, **не расширять scope самостоятельно**: новая `P:`-задача, фактический replan с сохранением критериев.

### XML documentation

**Обязательна:** summary, все три `<param>`, marker/default-return semantics и назначение scalar-формы. Не обещать исполнение метода в памяти или новую поддержку frames.

### Альтернативы

| Подход | Плюсы | Минусы / цена / риск | Решение |
|---|---|---|---|
| Прямой marker-overload | Минимальный API; использует существующий mapper | Требует compiler-compatibility доказательства | **Выбран, с обязательным probe до API-правки** |
| Новый overload плюс специальная ветка renderer | Явный отдельный путь | Дублирование; лишняя query-path поверхность без необходимости | Отклонён |
| Иное имя/extension helper | Может избежать overload competition | Не выполняет требование `WindowFunction<T>.Over`; translator ограничен declaring type | Отклонён |

## 4. Variant matrix

| Вариант | Закрытие |
|---|---|
| Single partition × single `asc`/`desc` × frame omitted/null | **Тест:** точное SQL parity на шести dialect-проектах; integration parity. |
| `partitionBy: null` × single order | **Тест:** отсутствие `PARTITION BY`; parity с array-null/order-only формой. |
| Nullable value/reference partition expression | **Тест:** SQL parity и NULL-partition semantics в общей integration-проверке. |
| `T`: value, nullable value, reference | **Тест:** marker возвращает соответствующий `default`, expressions не исполняются. |
| `frame: WindowFrame.Rows(...)` | **Тест:** positional/named binding, SQL parity и исполняемый результат. |
| `Groups` / exclusion, capability=true | **Тест:** scalar/array SQL parity; существующая поддержка без изменений. |
| `Groups` / exclusion, capability=false | **Guard:** нет новой поддержки. Если array-форма отвергается — scalar даёт тот же отказ; если renderer допускает SQL — проверяется SQL parity, но unsupported frame не исполняется как positive DB case. |
| Старые expression-, array-, order-only и optional формы | **Тест:** compiler corpus, существующие marker/dialect suites и полный build. |
| Named-window | **Тест:** старый binding и существующие suites; **guard:** новый overload не является named-window API. |
| Нетипизированный `default`; `null`; typed defaults | **Тест:** baseline→candidate differential compilation. Любая новая ошибка старого успешного вызова блокирует выбранный дизайн. |
| SQLite/PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse | **Тест:** каждый dialect-проект, без исключений. |
| SQLite + четыре существующих container-backed providers | **Тест:** общая integration suite; обязательные window-cases не skipped. |
| Отдельная MariaDB container integration | **Guard:** не добавляется новый fixture/provider launcher; MariaDB обязательно покрывается dialect-тестами. Триггер дополнительной DB-проверки — наличие MariaDB integration fixture в результате T232 или появление provider-specific изменения. Тогда prerequisite/replan в том же milestone. |
| `NULLS FIRST/LAST`, новые функции/flags | **Deferred:** только отдельная явно согласованная задача в текущем milestone; T233 их не вводит. |
| Глобальная настройка API analyzers | **Deferred:** отдельная задача public-API baseline в том же milestone; ручной реестр и независимая сертификация обязательны сейчас. |

## 5. Test strategy

### Unit / compiler

1. `tests/nextorm.core.tests/WindowFunctionMarkerTests.cs:28–51`:
   - новая scalar-форма;
   - value/nullable/reference `T`;
   - null partition и frame;
   - expressions не исполняются;
   - существующие marker-формы остаются.

2. Disposable compiler corpus:
   - baseline API **после T232, до API-правки T233**;
   - candidate API;
   - старые сигнатуры, named/positional/optional/array формы;
   - отдельно `Over(partitionExpression, default)` и null/typed-default варианты.
   - baseline-success → candidate-failure запрещён.
   - временные проекты только в `/tmp/nextorm-D233-compat/`; будущие test symbols не фиксируются заранее.

3. Все шесть dialect SQL-generation suites:
   - paired scalar/array assertions;
   - exact SQL equality;
   - asc/desc, nullable/null partition, omitted/null/Rows frame;
   - supported Groups/exclusion и сохранение existing unsupported behavior.

### Integration

Добавить parity-сценарий в `tests/nextorm.integration.tests/CommonTestSuite.Window.cs:14–131`, используя существующие window fixtures:

- scalar и array дают одинаковые упорядоченные результаты;
- уникальный order key исключает случайный порядок ties;
- NULL partition;
- `Rows` frame;
- SQLite, PostgreSQL, SQL Server, MySQL, ClickHouse реально исполняются.

Перед запуском загрузить `running-integration-tests`. Если socket отсутствует — выполнить документированный startup/retry; отсутствие контейнеров не превращать в pass.

### Coverage

Полный предусмотренный сбор по `coverage.settings.xml`; оба порога обязательны. Отдельный changed-lines coverage threshold не вводится.

### Structured `test scope`

```json
{
  "projects": [
    "tests/nextorm.core.tests",
    "tests/nextorm.sqlite.tests",
    "tests/nextorm.postgres.tests",
    "tests/nextorm.sqlserver.tests",
    "tests/nextorm.mysql.tests",
    "tests/nextorm.mariadb.tests",
    "tests/nextorm.clickhouse.tests",
    "tests/nextorm.integration.tests"
  ],
  "selectors": {
    "marker_fast": "FullyQualifiedName~WindowFunctionMarkerTests",
    "dialect_final": "all tests in each listed dialect project",
    "integration_final": "all tests in nextorm.integration.tests; required window cases must execute on SQLite, PostgreSQL, SQL Server, MySQL and ClickHouse",
    "coverage": "solution test run under coverage.settings.xml",
    "compatibility": "baseline/candidate compiler corpus; no invented future test names"
  },
  "files": [
    "tests/nextorm.core.tests/WindowFunctionMarkerTests.cs",
    "tests/nextorm.sqlite.tests/SqlGenerationTests.cs",
    "tests/nextorm.postgres.tests/SqlGenerationTests.cs",
    "tests/nextorm.sqlserver.tests/SqlGenerationTests.cs",
    "tests/nextorm.mysql.tests/SqlGenerationTests.cs",
    "tests/nextorm.mariadb.tests/SqlGenerationTests.cs",
    "tests/nextorm.clickhouse.tests/SqlGenerationTests.cs",
    "tests/nextorm.integration.tests/CommonTestSuite.Window.cs"
  ],
  "rebuild": {
    "required": true,
    "configuration": "Debug",
    "solution": "nextorm.slnx",
    "no_build_for_first_run": false,
    "baseline": "capture API after T232 and before T233 API edit",
    "final": "rebuild and rerun against the final unchanged diff"
  },
  "boundary": "New WindowFunction<T>.Over signature and shared argument mapping/rendering; existing overloads, six dialects and existing real-database window paths are regression boundaries.",
  "rationale": "A public overload can change compilation even when generated SQL is unchanged. Shared rendering requires provider parity and mandatory database evidence."
}
```

## 6. Documentation plan

**Fix now после approval:**

- `docs/guide/09-window-functions.md:105–116`;
- `docs/ru/guide/09-window-functions.md:106–116`.

В обеих страницах: scalar пример, эквивалентная array-форма, `frame: Rows`, обязательность первых двух аргументов. Compatibility guidance должна соответствовать доказанному compiler behavior, а не маскировать регрессию.

- `docs/specs/design/API-NAMING-REVIEW.md:2055,2076–2081` и соответствующие W1–W4/NW2:
  - точная новая сигнатура;
  - additive public-API отметка;
  - существующие overloads сохранены;
  - compiler evidence;
  - автор записи и отдельный CHECK-certifier.

**Stale `10-window-functions.md` references:** **fix now**, только существующие ссылки на window-guide в затронутых W1–W4/NW2, заменить на `09`. Не делать общий cleanup реестра.

**Не трогаем:** guide numbering, TOCs, indexes, advanced API overview, examples, capability roadmap/matrix. Нет переименования страницы или существующего symbol. Public docs не получают ссылок на `docs/specs/**`.

## 7. Evidence contract — frozen rv=1

### Идентичность и хранение

- Task slug: `D233-window-over-overload`.
- Status: `docs/specs/status/D233-window-over-overload-1.md`.
- Frozen manifest: `artifacts/pdca/D233-window-over-overload/rv1/manifest.json`.
- Все перечисленные ниже источники и артефакты **планируемые**, не существующие доказательства.
- `r=1`, `rv=1`; supersession отсутствует.
- Любая обоснованная ревизия должна явно supersede rv=1, сохранить ID и обязательства строк, добавить новые variant IDs. Отсутствующий отчёт не основание для ревизии.

### Общие обязательные слоты каждой строки

Для каждой строки таблицы:

- `row_id`, стабильные `requirement_ids`, `rv=1`;
- сценарий, ожидаемые evidence kinds/sources;
- точная команда/вызов через каталог ниже;
- требуемые exit/result/log;
- `path = artifacts/pdca/D233-window-over-overload/rv1/rows/<row_id>.json`;
- ожидаемые raw logs/artifacts в соседнем каталоге `<row_id>/`;
- поток-владелец, applicability predicate, приоритет.

`path` — **входной evidence-пакет**, не фиктивный положительный verdict. Владелец сохраняет команду, timestamp, revision/diff identity, exit code, результаты и ссылки на raw evidence. DO формирует manifest на DO→CHECK boundary. CHECK **сначала открывает manifest**, затем читает `path` каждой строки.

Независимые CHECK-certificates создаются при CHECK отдельно; до этого они отмечены pending, а не pass.

### Exact command / invocation catalog

Все shell-команды выполняются из repo root. Capture wrapper использует `pipefail`, сохраняет combined stdout/stderr и настоящий exit code; ненулевой exit не маскируется `tee`.

| Cmd | Точная команда/вызов |
|---|---|
| **C-GATE** | `python3 artifacts/pdca/D233-window-over-overload/rv1/audit.py gate` |
| **C-COMPAT** | `python3 artifacts/pdca/D233-window-over-overload/rv1/compat_probe.py` |
| **C-MARKER** | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~WindowFunctionMarkerTests"` |
| **C-SQLITE** | `dotnet test tests/nextorm.sqlite.tests -c Debug` |
| **C-POSTGRES** | `dotnet test tests/nextorm.postgres.tests -c Debug` |
| **C-SQLSERVER** | `dotnet test tests/nextorm.sqlserver.tests -c Debug` |
| **C-MYSQL** | `dotnet test tests/nextorm.mysql.tests -c Debug` |
| **C-MARIADB** | `dotnet test tests/nextorm.mariadb.tests -c Debug` |
| **C-CLICKHOUSE** | `dotnet test tests/nextorm.clickhouse.tests -c Debug` |
| **C-INTEGRATION** | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` |
| **C-BUILD** | `dotnet build nextorm.slnx -c Debug` |
| **C-COVERAGE** | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet-coverage collect --settings coverage.settings.xml --output-format cobertura --output artifacts/pdca/D233-window-over-overload/rv1/coverage/coverage.cobertura.xml "dotnet test nextorm.slnx -c Debug"` |
| **C-REPORT** | `reportgenerator "-reports:artifacts/pdca/D233-window-over-overload/rv1/coverage/coverage.cobertura.xml" "-targetdir:artifacts/pdca/D233-window-over-overload/rv1/coverage/report" "-reporttypes:JsonSummary;TextSummary"` |
| **C-COVERAGE-GATE** | `python3 artifacts/pdca/D233-window-over-overload/rv1/audit.py coverage` |
| **C-DOCS** | `python3 artifacts/pdca/D233-window-over-overload/rv1/audit.py docs` затем `dotnet docfx docs/docfx.json` |
| **C-BOUNDARY** | `python3 artifacts/pdca/D233-window-over-overload/rv1/audit.py boundary` |
| **C-MANIFEST** | `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D233-window-over-overload/rv1/manifest.json` |
| **I-REGISTRY** | `Task(check): "Открой manifest rv=1 первым. Независимо сертифицируй EC233-REGISTRY: точную Over-сигнатуру, EN/RU ссылки, W1–W4/NW2, compatibility evidence и author≠certifier. Сохрани certificate по контракту; отсутствие доказательства не pass."` |
| **I-CAPABILITY** | `Task(check): "Открой manifest rv=1 первым. Проверь EC233-CAPABILITY: неизменность capability flags и worker, матрицу шести dialects, nullable/frame guards. Используй Roslyn для C# symbols. Сохрани independent certificate; неподтверждённый guard не pass."` |

`audit.py` и `compat_probe.py` — планируемые evidence helpers, не production tooling:

- `gate`: проверяет сохранённое отдельное approval и handoff T232, порядок относительно первого implementation edit.
- `compat_probe`: строит disposable baseline/candidate corpus; сохраняет diagnostics и binding outcomes; exit 0 только без baseline-success→candidate-failure и без смены старого binding.
- `coverage`: читает реальные summary/XML; exit 0 только при обоих порогах.
- `docs`: проверяет обе страницы, точную optionality, ссылки и отсутствие public→spec links.
- `boundary`: проверяет explicit footprint, CRLF, неизменность старых API/worker/capability surface; семантические выводы подтверждаются Roslyn/CHECK.

Если установленный toolchain не поддерживает указанную coverage-команду, это **предусловие**, а не разрешение убрать coverage: вернуть точный diagnostics в PLAN для корректного equivalent invocation по CI workflow.

### Frozen `required_rows`

```text
EC233-GATE
EC233-COMPAT
EC233-MARKER
EC233-SQL-SQLITE
EC233-SQL-POSTGRES
EC233-SQL-SQLSERVER
EC233-SQL-MYSQL
EC233-SQL-MARIADB
EC233-SQL-CLICKHOUSE
EC233-INTEGRATION
EC233-BUILD
EC233-COVERAGE
EC233-DOCS
EC233-REGISTRY
EC233-CAPABILITY
EC233-BOUNDARY
EC233-MANIFEST
```

| Row ID → requirements | Сценарий; evidence kinds / planned sources | Cmd; обязательный результат | Артефакты; владелец; applicability; priority |
|---|---|---|---|
| **EC233-GATE → R233-GATE** | Approval, T232 handoff, edit chronology; authorization/status records | C-GATE; exit 0, оба разрешения подтверждены | Gate report, approval/handoff extracts; orchestrator + evidence coder; **всегда до implementation**; P1 |
| **EC233-COMPAT → R233-2** | Все старые формы, null/default; compiler diagnostics и semantic binding | C-COMPAT; exit 0, ноль regressions | Corpus sources, baseline/candidate logs, binding comparison; compiler-test stream; **всегда**; P1 |
| **EC233-MARKER → R233-2,3** | Scalar marker, value/reference/nullable/default, неисполнение expressions | C-MARKER; exit 0, новые сценарии исполнены | Test log + scenario→actual-test mapping; core-test stream; **всегда**; P1 |
| **EC233-SQL-SQLITE → R233-1,2,3** | Exact parity, nullable, asc/desc, frames; SQLite suite | C-SQLITE; exit 0, mandatory cases passed | Log, actual test map, paired SQL evidence; dialect-test stream; **всегда**; P1 |
| **EC233-SQL-POSTGRES → R233-1,2,3** | То же, PostgreSQL | C-POSTGRES; те же требования | То же; dialect-test stream; **всегда**; P1 |
| **EC233-SQL-SQLSERVER → R233-1,2,3** | То же, SQL Server | C-SQLSERVER; те же требования | То же; dialect-test stream; **всегда**; P1 |
| **EC233-SQL-MYSQL → R233-1,2,3** | То же, MySQL | C-MYSQL; те же требования | То же; dialect-test stream; **всегда**; P1 |
| **EC233-SQL-MARIADB → R233-1,2,3** | То же, MariaDB | C-MARIADB; те же требования | То же; dialect-test stream; **всегда**; P1 |
| **EC233-SQL-CLICKHOUSE → R233-1,2,3** | То же, ClickHouse, сохранение in-frame boundary | C-CLICKHOUSE; те же требования | То же; dialect-test stream; **всегда**; P1 |
| **EC233-INTEGRATION → R233-1,3,4** | Result/NULL/Rows parity; пять существующих DB providers | C-INTEGRATION; exit 0, обязательные window cases реально executed, не skipped | Log, provider×scenario execution table, results; integration stream; **всегда**; P1 |
| **EC233-BUILD → R233-2,4** | Solution и старые call sites | C-BUILD; exit 0, 0 warnings/errors | Полный build log; build stream; **всегда**; P1 |
| **EC233-COVERAGE → R233-4** | Project-configured line и branch coverage | C-COVERAGE, C-REPORT, C-COVERAGE-GATE; все exit 0; ≥85/≥75 | Cobertura XML, Json/Text summaries, threshold report; coverage stream; **всегда**; P1 |
| **EC233-DOCS → R233-5** | EN/RU examples, frame, links, DocFX | C-DOCS; оба exit 0, нет новых broken references | Audit/DocFX logs, docs diff; docs-test stream; **всегда**; P1 |
| **EC233-REGISTRY → R233-5** | API entry, stale references, независимость | I-REGISTRY; explicit certified result | DO input diff/report; `certifications/EC233-REGISTRY.json`; **CHECK, не автор записи**; **всегда**; P1 |
| **EC233-CAPABILITY → R233-1,2,3** | Flag/worker invariants, nullable и unsupported-frame guards | I-CAPABILITY; explicit verified result по всей матрице | Capability table с actual evidence locations; `certifications/EC233-CAPABILITY.json`; CHECK semantic-review stream; **всегда**; P1 |
| **EC233-BOUNDARY → R233-2,5,R233-SCOPE** | API-only scope, CRLF, no worker/cache changes; perf applicability | C-BOUNDARY; exit 0; CHECK подтверждает semantic review | Boundary/diff report, perf decision; boundary stream + CHECK; **всегда**; P1 |
| **EC233-MANIFEST → R233-EVIDENCE** | Все required rows/paths, rv, артефакты и источники | C-MANIFEST; exit 0; затем CHECK читает каждый path | Manifest validation log и completeness review; evidence coder + CHECK; **всегда**; P1 |

**Приоритеты по построению:** все строки выше P1: исходные инварианты, public-API compatibility либо проектная таблица класса «новая SQL-функция/оператор». Последняя применяется консервативно: parity, capability boundary, каждый provider, nullable semantics обязательны. CHECK не понижает эти строки.

### CHECK re-gather budget

- Владелец: **check**.
- Бюджет: **2 адресных re-gather запроса за CHECK**, каждый для одной семьи evidence rows.
- Сначала читать существующие `path` и raw artifacts; затем запросить отсутствующее доказательство у соответствующего evidence owner.
- Повторный сбор без source edits не является новой DO-итерацией.
- Исчерпанный бюджет → явно unresolved row. При стойко низкой уверенности — рекомендация orchestrator вызвать `escalate`, **триггер 5**.
- Отсутствие отчёта само по себе не оправдывает rv/r revision или исправительную DO.

## 8. Performance-measurement decision

**Не нужен benchmark в выбранном scope.**

Основание: marker API находится в `Query/WindowFunctions.cs:16,28–57`; новый метод только представляет expression-call. Mapping/rendering остаются `WindowSql.cs:139–171` и `WindowFunctionTranslator.cs:107–179`. Не добавляется per-row работа, allocation, planner/cache/executor/materializer путь; SQL одинаков.

Проверка применимости обязательна в EC233-BOUNDARY. Если фактическое решение потребует изменения query-path/plan-cache/executor/materialization — DO→PLAN; тогда обязательны baseline и проектные **ровно 7 acceptance benchmark cases**, без молчаливого исключения.

## 9. Reconnaissance decision

**Нужен ограниченный compiler experiment**, но не prototype SQL renderer.

Что доказывает: сохранение overload resolution для старых `null`/`default` и optional форм.

Наблюдаемый критерий: каждый baseline-success пример остаётся candidate-success с прежним binding; нет нового CS0121.

Выполняется только в последующем разрешённом запуске, **перед публичной API-правкой**. Провал инвалидирует выбранный дизайн и требует нового `P:`, а не изменения старых call sites или criteria.

Отдельный renderer spike не нужен: имеющийся mapping по именам и существующие scalar-order paths дают достаточную основу для выбранного минимального решения.

## 10. Unit execution mode

**Последовательно, один writer, одно дерево.**

Причины: G1 принудительно одна lane; общий публичный файл и registry, плюс зависимость от T232. Не создавать worktrees/branches.

Write-footprint: публичный API, core marker tests, шесть dialect SQL-generation files, общий integration window file, два guide files, registry, status; generated evidence subtree отдельно.

## 11. Design checklist

- **SOLID:** marker остаётся marker; rendering/validation не переносится в API.
- **DRY:** используются существующие mapping и frame renderer.
- **Type design:** существующие `Expression`, `WindowOrder`, `WindowFrame`; новые types/constraints отсутствуют.
- **API:** первые параметры не optional; старые сигнатуры не редактируются; compiler compatibility доказана отдельно.
- **Perf:** нет массивов/closures/compile/Invoke в новом marker body.
- **Cache:** никакой mutation `QueryCommand.Cache` или shared-command state.
- **Toolchain:** Roslyn-first для symbols/binding; CPM без новых packages; CRLF; warnings-as-errors.
- **Review:** docs/registry author не сертифицирует собственную запись.

## 12. Risks, assumptions, prerequisites and sibling results

### Чего не сказала постановка

| Пробел | Закрытие |
|---|---|
| Compatibility для untyped `default` | **Не допущение:** обязательный compiler probe; failure блокирует выбранный дизайн. |
| Семантика default/null `WindowOrder` | Сравнить с существующим order-only поведением; новую семантику не изобретать. |
| Конкретные nullable fixtures | Использовать существующие window fixtures; required data setup ограничить window-test files. Если нужны shared fixture edits — additive prerequisite/replan, без потери исходного D. |
| MariaDB DB fixture | Не предполагается существующим; обязательный dialect test остаётся. При наличии fixture после T232 — включить DB evidence. |
| Состояние T232 | Требуется подтверждённый handoff; до него T233 blocked. |
| Новое implementation approval | Текущий issue comment его не даёт; требуется отдельная сохранённая авторизация. |
| Полная tool availability для coverage | Проверяется prerequisite; tool failure не отменяет thresholds. |

### Требования к T232

T233 выполняется **после T232**. Ожидается:

1. T232 завершён и сертифицирован в G1 / `1.0.9-rc3`.
2. Передан окончательный diff и описание public-API/worker/capability изменений.
3. `Query/WindowFunctions.cs`, `Builders/**`, `Dialect/**` больше не изменяются параллельно.
4. Сохранён baseline после T232 для compatibility и build.
5. Если T232 уже добавил пересекающуюся сигнатуру или изменил mapper/capability boundary — T233 возвращается в PLAN; дублирование запрещено.

### Блокеры и классификация

- Approval и T232 — **предусловия**, не завершение D и не STOP от DO.
- Недостающий report — **недостаток доказательств**, сначала bounded targeted gathering.
- Container/toolchain recovery — in-cycle prerequisite по проектным инструкциям.
- Доказанная несовместимость выбранной сигнатуры — реальное основание для replan.
- Истинное препятствие вне разрешений/ресурсов → рекомендация orchestrator вызвать `escalate`.
- Никакая обнаруженная incompatibility не принимается как «допустимый риск» ценой ослабления R233-2.

## 13. Ordered task list

### Сейчас — только PLAN

**P:233.1 — Persist and freeze.**  
`coder` записывает этот план в `docs/specs/status/D233-window-over-overload-1.md`, фиксирует `r=1`, `rv=1`, N=1, pending approval/T232 и frozen contract. **Затем остановка.** Ни implementation edits, ни тестовые запуски.

### После отдельного разрешения и нового запуска DO

1. **D:233.1 — Gate и compatibility prerequisite.** Touch: status, generated evidence subtree, disposable `/tmp/nextorm-D233-compat/`. **Fix now:** approval/T232 evidence, baseline build, differential probe. При несовместимости — DO→PLAN до API-правки.
2. **D:233.2 — PUBLIC API ADDITION, approval-required.** Touch: `src/nextorm.core/Query/WindowFunctions.cs:16–57`. **Fix now:** один выбранный overload и XML docs. Старые overloads неизменны; worker untouched.
3. **D:233.3 — Marker tests.** Touch: `tests/nextorm.core.tests/WindowFunctionMarkerTests.cs:28–51`. **Fix now:** scalar/default/nullability/nonexecution cases.
4. **D:233.4 — Six-dialect parity.** Touch: шесть `SqlGenerationTests.cs` из footprint. Anchors: PostgreSQL `2551,2658`; SQLite `2316,2349,2365,2402,2441,2469`; SQL Server `1705,1738,1754,1812,2361`; MySQL `271,469,486,503`; MariaDB `248,360,377,394`; ClickHouse `676,703,720,737,1108`. **Fix now:** матрица parity/frame/nullable и capability boundary.
5. **D:233.5 — Integration parity.** Touch: `tests/nextorm.integration.tests/CommonTestSuite.Window.cs:14–131`. **Fix now:** deterministic scalar/array results, NULL partition, Rows.
6. **D:233.6 — EN/RU documentation.** Touch: два `09-window-functions.md`. **Fix now:** paired examples, frame, корректная optionality.
7. **D:233.7 — Registry authoring.** Touch: `docs/specs/design/API-NAMING-REVIEW.md:2055,2076–2081`, W1–W4/NW2. **Fix now:** API entry и stale window-guide references. Не сертифицировать собственную правку.
8. **D:233.8 — Final evidence boundary.** Touch: status и generated evidence subtree. **Fix now:** CRLF, final build/tests/coverage/docs, все row packages и manifest; вызвать CHECK для независимой сертификации. Последний source edit требует релевантного повторного evidence.

**Deferred:** только явно перечисленные non-goals из matrix. Ни один обязательный критерий T233 не deferred.

При additive prerequisite исходный D сохраняется **active/blocked** с неизменными criteria и остатком. Только фактическая замена scope допускает `superseded→replacement`. Любой исправительный возврат получает новую `P:`; `r` увеличивается лишь при реальной смене действий/зависимостей, не при переформулировке.

## 14. Planned write-footprint

**13 tracked paths:**

1. **PUBLIC API:** `src/nextorm.core/Query/WindowFunctions.cs`
2. `tests/nextorm.core.tests/WindowFunctionMarkerTests.cs`
3. `tests/nextorm.sqlite.tests/SqlGenerationTests.cs`
4. `tests/nextorm.postgres.tests/SqlGenerationTests.cs`
5. `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`
6. `tests/nextorm.mysql.tests/SqlGenerationTests.cs`
7. `tests/nextorm.mariadb.tests/SqlGenerationTests.cs`
8. `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`
9. `tests/nextorm.integration.tests/CommonTestSuite.Window.cs`
10. `docs/guide/09-window-functions.md`
11. `docs/ru/guide/09-window-functions.md`
12. `docs/specs/design/API-NAMING-REVIEW.md`
13. `docs/specs/status/D233-window-over-overload-1.md`

Generated/uncommitted evidence:

- `artifacts/pdca/D233-window-over-overload/rv1/**`
- `/tmp/nextorm-D233-compat/**`
- test-runner outputs under approved `TestResults/**`.

`Builders/**`, `Dialect/**`, translator, examples, workflow/config files **не входят** в разрешённый footprint T233.

В последующем разрешённом collection execution auto-commit допустим только после CHECK, с **явно перечисленными paths**, префиксом `#233`. Никогда `git add -A`, push, branch или merge.

## 15. Evidence-contract & gate self-check

- **Goal/scope:** определены сущность, ограничения и non-goals.
- **Acceptance:** R233-1…5 имеют наблюдаемые проверки и негативные случаи.
- **Minimal design:** точная сигнатура, optionality, XML docs и alternatives зафиксированы.
- **Public-API approval:** жёсткий запрет DO до отдельного approval сохранён.
- **Compatibility:** default/null gap не скрыт; обязательный blocking probe предусмотрен.
- **Decomposition:** ordered D, dependencies, completion и replan rules определены.
- **Variants:** input/null/default/value/reference/provider/frame/flags закрыты test/guard/deferred.
- **Test scope:** обязательный structured JSON присутствует.
- **Priorities:** обязательные строки P1; CHECK не понижает.
- **Docs:** EN/RU и registry обязательны; stale references fix now.
- **Perf:** неприменимость аргументирована `file:line`; scope-change trigger указан.
- **Recon:** compiler experiment имеет наблюдаемый критерий.
- **Execution:** single writer/tree, T232→T233, без worktree/branches.
- **Evidence:** rv=1, stable requirement/row IDs, commands, results, artifacts, owners и predicates закреплены.
- **Manifest:** frozen path, required_rows, DO→CHECK production и manifest-first CHECK определены.
- **Re-gather:** конечный бюджет 2, владелец CHECK; missing evidence не создаёт фиктивный replan.
- **Certification:** registry author≠certifier.
- **Project rules:** CRLF, Roslyn-first, thresholds и реальные контейнеры сохранены.
- **Current boundary:** записать план и остановиться; DO сейчас запрещён.

Verdict: **r=1 ready; DO blocked**, evidence contract rv=1 frozen.
Chosen signature: `Over(Expression<Func<object?>>? partitionBy, WindowOrder orderBy, WindowFrame? frame = null)`.
Footprint size: **13 tracked paths**, один public-API file; generated evidence отдельно.
Open assumptions: T232 handoff, новое approval, compiler compatibility null/default и доступность существующих fixtures.
Next allowed step: **coder сохраняет этот план в status file и останавливается; DO не запускается**.
---
