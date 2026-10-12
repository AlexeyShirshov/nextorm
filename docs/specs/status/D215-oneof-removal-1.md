# D215 — Убрать зависимость OneOf (task T215 / issue #215)

- **Task:** T215 · **evidence task:** D215
- **Issue:** #215 — https://github.com/AlexeyShirshov/nextorm/issues/215 (verified via webfetch; title «Убрать зависимость OneOf», state Open, milestone 1.0.9, body "No description provided.")
- **Milestone:** 1.0.9
- **Cycle variant:** `pdca-dotnet` · **Collection:** `1.0.9-todo`, group **G1** (single group → current worktree, no branches/worktrees, no merge)
- **N=1 · r=1 · n=1 · rv=1**
- **plan_state: ready** · **phase: PLAN** (frozen at PLAN→DO boundary; DO NOT started)
- **Worktree:** `/home/alex/sources/nextorm` · **Branch:** `1.0.9` · **HEAD at freeze:** `03a79285`
- **Collection base (historical only, not task delta base):** `58025147`
- **Frozen evidence manifest:** `artifacts/pdca/D215/rv1/manifest.json`
- **This status file:** `docs/specs/status/D215-oneof-removal-1.md`

> `plan_state: ready` относится к готовности ПЛАНА, не означает завершение D-единиц или CHECK PASS. Collection status `docs/specs/status/collection-1.0.9-todo.md` **вне task write scope** (владелец — collection parent; task его не редактирует).

## A. Цель и недостающие условия

**Цель:** удалить неиспользуемую зависимость NextORM от OneOf вместе с зависимыми тестовыми файловыми ссылками и устаревшим описанием зависимостей. Не вводить замену, не менять исполняемый production-код или публичные сигнатуры.

| Пробел постановки | Закрытие |
|---|---|
| Replacement strategy отсутствует | Активных source-потребителей нет: **чистое удаление без замены** |
| Acceptance criteria отсутствуют | AC215-01…06 ниже |
| Breaking-change policy отсутствует | Сигнатуры NextORM сохраняются; изменение транзитивного package graph явно документируется |
| Metadata reference исходного `nextorm.core.dll` неизвестен | Не выдаём его отсутствие за проверенный факт. Решающий oracle: свежий разрешённый dependency graph |
| Manifest schema неизвестна | Закрыто: `SKILL.md:956–970`; схема и validator подтверждены |
| Получаемость dependency graph неизвестна | Закрыто: CLI exit 0, parseable JSON, 25 проектов; baseline сохранён |
| Verified issue URL отсутствовал | Закрыто: https://github.com/AlexeyShirshov/nextorm/issues/215, Open, milestone 1.0.9 |
| Пересечение с грязным деревом | Закрыто: семь изменяемых content-путей CLEAN; 39 чужих entries сохраняются |
| Путь diagnostic-test файла противоречив | **DO guard**: проверить существование до source edits |
| Режим normal/autonomous | Применить выбранный оркестратором режим; сначала записать план/контракт |

Все работы входят в milestone **1.0.9**. Deferred findings отсутствуют.

## B. Минимальное решение и стратегия замены

1. **Цель:** OneOf отсутствует в декларациях и разрешённом графе NextORM.
2. **Ограничения:** публичные сигнатуры и runtime behavior неизменны; CPM, CRLF, build 0/0, сохранность чужих изменений.
3. **Оптимум:** удалить две package-декларации, две test metadata references, один комментарий; исправить EN/RU документацию.

| Альтернатива | Плюсы | Цена / риск |
|---|---|---|
| **Удаление без замены** | Минимальный diff; потребителей нет | Нужно исправить test references и описать транзитивную совместимость |
| Новый union/type/package | Полезен только при живом потребителе | Потребителя нет; лишняя абстракция/API (нарушает «абстракция на втором потребителе») |
| Оставить OneOf ради тестов | Меньше тестовых правок | Не выполняет #215, сохраняет ненужную зависимость |

**Выбор: удаление без замены.** В `src/nextorm.core/Expressions/FromExpression.cs:96` удалить **только** комментарий с прежним `OneOf`-членом; живой член на `:98` не менять. Других production `.cs` изменений не допускается.

## C. Совместимость, T53 и точный write-footprint

- **Публичная source/binary API NextORM:** запланированная delta = **0**; публичных сигнатур с OneOf не найдено.
- **Транзитивная зависимость** удаляется: потребителям, использующим OneOf через NextORM, потребуется собственный `PackageReference`; возможна source/runtime incompatibility именно у таких потребителей.
- **T53 остаётся HELD**; T215 его не разблокирует.

**Текст для учёта родителем/T53:**
> T215 (#215), 1.0.9: удалена неиспользуемая зависимость OneOf. Публичные типы, члены и сигнатуры NextORM не изменены; публичных сигнатур с OneOf не обнаружено. Изменён package dependency graph. Потребители, использовавшие транзитивно предоставляемый OneOf, должны добавить собственный PackageReference. API-sensitive файлы: `src/nextorm.core/nextorm.core.csproj`, `Directory.Packages.props`; в `src/nextorm.core/Expressions/FromExpression.cs` удалён только комментарий, живой API сохранён. T53 остаётся HELD до решения владельца.

### Разрешённые tracked paths — ровно восемь

| Путь | Разрешённая правка | Public-API flag |
|---|---|---|
| `Directory.Packages.props` | Удалить `PackageVersion OneOf` | **Public-API-sensitive package contract**, не сигнатура |
| `src/nextorm.core/nextorm.core.csproj` | Удалить `PackageReference OneOf` | **Public-API-sensitive package contract**, не сигнатура |
| `src/nextorm.core/Expressions/FromExpression.cs` | Удалить только указанный комментарий | Файл с публичным членом; **API delta = 0** |
| `tests/nextorm.alias.tests/JoinedDeleteReturningApiCompilationTests.cs` | Удалить metadata reference на `"OneOf.dll"` | Нет |
| `tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs` | То же; сначала подтвердить путь | Нет |
| `docs/getting-started/01-installation.md` | Dependency list и compatibility note | Документация package contract |
| `docs/ru/getting-started/01-installation.md` | Эквивалентная RU-правка | Документация package contract |
| `docs/specs/status/D215-oneof-removal-1.md` | Единственный task status artifact | Нет |

Дополнительно разрешены evidence под `artifacts/pdca/D215/rv1/`, штатные build/test outputs, и удаление stale `tests/nextorm.alias.tests/bin/linux/Debug/net10.0/OneOf.dll`.

**Явно OUT OF SCOPE:** редактирование `docs/specs/status/collection-1.0.9-todo.md`. Не менять остальные docs, benchmark artifacts, конфигурации или исходники. Разрешения на commit этот план не добавляет.

## D. Критерии приёмки и порядок D-единиц

| ID | Положительный критерий / oracle | Негативный случай |
|---|---|---|
| **AC215-01** | В tracked `.csproj/.props/.targets` нет деклараций OneOf; literal scan и diff | Осталась package declaration |
| **AC215-02** | После fresh restore solution JSON graph содержит все ожидаемые проекты/TFM и **ни одного OneOf**, direct или transitive | OneOf найден; проект пропущен; JSON/restore некорректен |
| **AC215-03** | `dotnet build nextorm.slnx -c Debug`: exit 0, **0 warnings / 0 errors** | Любая ошибка, warning или ненулевой exit |
| **AC215-04** | Полные alias/core test projects проходят; обе затронутые alias groups выполнены; файловые references удалены; OneOf.dll отсутствует в alias output | Failure, file-not-found, retained reference, невыполненные целевые tests или stale DLL |
| **AC215-05** | EN/RU больше не называют OneOf текущей зависимостью и одинаково предупреждают о транзитивных потребителях | Старая claim или односторонняя языковая правка |
| **AC215-06** | Task delta ограничена allowlist; единственная production `.cs` delta — удаление комментария; API и чужие изменения сохранены; CRLF соблюдён | Runtime/API change, новый пакет/абстракция, правка collection status или повреждение чужого diff |

Получаемость основного oracle уже доказана baseline-запуском.

### D-единицы — последовательно, все fix now

1. **D215-01 — preflight и package removal.** До source edits подтвердить оба test paths: `git ls-files --error-unmatch -- tests/nextorm.alias.tests/JoinedDeleteReturningApiCompilationTests.cs tests/nextorm.alias.tests/JoinAliasGeneratorDiagnosticTests.cs`. Снять исходное dirty state и task-path snapshots. `src/nextorm.core/nextorm.core.csproj:13`: удалить OneOf `PackageReference`. `Directory.Packages.props:32`: удалить OneOf `PackageVersion`. Никаких replacement packages.
2. **D215-02 — source-comment и test-reference cleanup.** `FromExpression.cs:96`: удалить только комментарий. В двух подтверждённых alias-test файлах удалить `"OneOf.dll"` metadata references; остальные references и test behavior сохранить. Обязательно: без пакета DLL перестаёт гарантированно присутствовать, а `MetadataReference.CreateFromFile` иначе бросает file-not-found.
3. **D215-03 — EN/RU docs.** Исправить dependency list в обеих installation pages; добавить согласованное предупреждение для потребителей транзитивного OneOf. Публичные docs не ссылаются на internal specs.
4. **D215-04 — gates, evidence, manifest, task status.** CRLF для изменённых tracked text files; clean → удалить только stale alias `OneOf.dll` при необходимости → forced restore → build → output guards → tests → final graph; проверить scope/API/чужое состояние; записать actual evidence и manifest; выполнить validator; обновить только task status.

Если второй test path отсутствует: не создавать файл и не считать reference удалённым; выполнить точечное обнаружение (Roslyn либо literal `"OneOf.dll"`); при необходимости расширения footprint — вернуть в PLAN. Исходная D-единица остаётся активной, не `done` и не `superseded`.

## E. Режим исполнения

Текущий worktree, ветка `1.0.9`, без веток/worktrees. Порядок: **D215-01 → D215-02 → D215-03 → D215-04**. Единицы связаны общим контрактом «core package graph + alias compilation», выполняются последовательно.

## F. Тест-стратегия и матрица вариантов

- `dotnet test tests/nextorm.alias.tests -c Debug`
- `dotnet test tests/nextorm.core.tests -c Debug`

Запускаются целые проекты; evidence подтверждает выполнение обеих затронутых alias groups, не только старт процесса. Solution build и полный package graph закрывают проекты/провайдеры.

**Database integration:** N/A для разрешённого diff (SQL, provider execution, cache, executor, materialization не меняются). Условная строка E215-10 сохраняется.

**Coverage:** пороги line **85%**, branch **75%** (на `1.0.9` — warning, на `main` — hard-fail); не понижаем. Отдельный coverage-прогон не назначается (production executable lines не меняются).

### Red ↔ green
- **Red уже получен:** OneOf в 24 из 25 проектов; в core — top-level, requested/resolved `3.0.271`.
- **Green:** тот же CLI oracle после fresh restore показывает отсутствие OneOf во всех проектах.

Не добавлен `GetReferencedAssemblies()` assertion: исходный DLL мог уже не ссылаться на неиспользуемую сборку.

| Вариант | Закрытие |
|---|---|
| OneOf присутствует в исходном package graph | **test:** имеющийся baseline |
| OneOf отсутствует в final graph | **test:** AC215-02 |
| Исходная assembly reference present/absent | **guard:** не выбранный oracle; неизвестность сохранена |
| Alias reference оставлен, DLL отсутствует | **guard:** source scan + AC215-04 |
| References удалены, DLL отсутствует | **test:** output guard + alias suite |
| Stale DLL остался | **guard:** clean и absence-проверка |
| Удалён reference, оставлена CPM version | **guard:** AC215-01 |
| Изменены только docs | **guard:** package/graph criteria не пройдут |
| Обновлён только один язык | **guard:** AC215-05 |
| Direct/transitive; core/providers/tests | **test:** graph всех 25 проектов, всех TFM |
| `null/default`, value/reference, runtime flags | **guard/N/A:** executable code не меняется (AC215-06) |
| В diff появились runtime/provider changes | **guard → PLAN:** scope нарушен; conditional integration применима |

Новых helpers/abstractions/shared production contracts нет.

## G. Документация и регистры

Меняем обе installation pages и task status. Не меняем: `AGENTS.md`, readme (OneOf отсутствует), `API-NAMING-REVIEW.md` (OneOf section отсутствует), code-smells register (закрытие неизвестного пункта не заявляется), generated `docs/api`, `docs/_site`, **collection status (запрещён task write)**.

## H. Performance measurement

**Cached-path benchmark N/A.** Изменения: package declarations (`core.csproj:13`, `Directory.Packages.props:32`), комментарий (`FromExpression.cs:96`), test metadata references, docs. Нет per-row операции или изменения query/cache/executor/materialization. BenchmarkDotNet не является oracle. Не заявляем скорость/allocations/размер без измерения. При runtime/cache diff аргумент перестаёт действовать → PLAN с замером.

## I. Разведка / прототип

Design spike не нужен (замены нет). Readiness-разведка завершена. Осталась узкая DO-проверка существования diagnostic-test path (guard известного противоречия). C# semantic discovery — только Roslyn; `git grep` — для literal `"OneOf.dll"`; `git ls-files` — для существования tracked path. Никаких `grep -r`/`find`.

## J. Приоритеты и возвраты PLAN

| Строки | Приоритет |
|---|---|
| AC215-01/02 | **P1:** сущность постановки |
| AC215-03 | **P1:** build invariant |
| AC215-04 | **P1:** затронутый test execution path |
| AC215-05 | **P1:** dependency docs и EN/RU invariant |
| AC215-06 | **P1:** API/scope/dirty-tree preservation |
| Manifest completeness/validation | **P1:** PDCA contract |

**DO → PLAN:** подготовительный недостаток — новая зависимость, исходный D активен `blocked`; неизвестная metadata reference при доказанном final graph — допустимый явно записанный пробел; неясный путь/лог/result — точечный scout, при стойкой низкой уверенности `escalate` (триггер 5); истинный внешний blocker — `escalate`. Реальный replan требует новой `P:`-задачи и фактического изменения действий/зависимостей. После третьего провального CHECK одной ревизии — эскалация, не четвёртая попытка.

## K. Frozen evidence contract rv=1

**Manifest (schema `SKILL.md:956–970`, validator `scripts/validate_inner_loop.py:495–610`; exit 0 valid / 2 violation / 1 malformed; `open` допустим при подготовке, но запрещает PASS в CHECK):**

- `task`: `"D215"`
- `tree`: `"/home/alex/sources/nextorm"`
- `contract_rv`: `1`
- `status_file`: `"docs/specs/status/D215-oneof-removal-1.md"`
- `required_rows`: `["E215-00","E215-01","E215-02","E215-03","E215-04","E215-05","E215-06","E215-07","E215-08","E215-09","E215-10","E215-11"]`
- `rows`: ровно по объекту на каждый ID.

| Row / requirement | Точная команда / проверка | Evidence и требуемый результат | Owner; predicate |
|---|---|---|---|
| **E215-00 / readiness** | Консолидировать P215-01 results, schema/validator, verified URL, CLI help | `readiness.md`, `package-list-help.log`. Подтверждены protocol, oracle, URL, TargetDir; не заявлять успешный `gh` | coder/status; **всегда** |
| **E215-01 / AC215-02 baseline** | `dotnet list nextorm.slnx package --include-transitive --format json` (уже выполнено) | **Actual:** `baseline-packages.json` exit 0; `baseline-restore.log`. 25 проектов, OneOf в 24, core top-level `3.0.271` | coder/config; **всегда, до правок** |
| **E215-02 / AC215-01** | `git grep -n -i 'oneof' -- ':(glob)**/*.csproj' ':(glob)**/*.props' ':(glob)**/*.targets'` | `package-declarations.log`; exit **1**, совпадений нет. Exit >1 — ошибка | coder/config; **всегда** |
| **E215-03 / AC215-02** | `dotnet restore nextorm.slnx --force`; затем `dotnet list nextorm.slnx package --include-transitive --format json` | `final-restore.log`, `final-packages.json`, `package-graph-verdict.md`; exits 0; 25 проектов, TFM не потеряны, OneOf нигде нет | coder/config; **всегда** |
| **E215-04 / AC215-03** | `dotnet clean nextorm.slnx -c Debug`; затем `dotnet build nextorm.slnx -c Debug` | `clean.log`, `build.log`; exits 0; **0 warnings / 0 errors** | coder/build; **всегда** |
| **E215-05 / AC215-04** | `git ls-files --error-unmatch -- <2 alias test paths>`; после правок `git grep -n -F '"OneOf.dll"' -- <2 alias test paths>`; `test ! -e tests/nextorm.alias.tests/bin/linux/Debug/net10.0/OneOf.dll` | `alias-path-guard.log`, `alias-reference-scan.log`, `alias-output-guard.log`; exits **0, 1, 0**. Отсутствующий source path не трактовать как успешное удаление | coder/tests; **всегда** |
| **E215-06 / AC215-04** | `dotnet test tests/nextorm.alias.tests -c Debug` | `alias-tests.log`, `alias-test-execution.md`; exit 0, failures 0; обе groups выполнены, не silent empty/skip | coder/tests; **всегда** |
| **E215-07 / AC215-04** | `dotnet test tests/nextorm.core.tests -c Debug` | `core-tests.log`; exit 0, failures 0, nonempty execution | coder/tests; **всегда** |
| **E215-08 / AC215-05** | `git diff -- <2 docs paths>`; `git grep -n -i 'oneof' -- <2 docs paths>` | `docs.diff`, `docs-oneof-context.log`, `docs-verdict.md`. Diff exit 0; scan exit 0 допустим для note; обе версии согласованы, нет текущей dependency claim | coder/docs; **всегда** |
| **E215-09 / AC215-06** | До/после: `git status --porcelain=v1`; `git diff`; `git diff --cached`; review snapshots/delta и CRLF | `baseline-status.txt`, `baseline-working.diff`, `baseline-staged.diff`, `baseline-task-paths/`, `final-status.txt`, `task-delta.diff`, `scope-api-crlf-verdict.md`. API delta 0; production `.cs` только комментарий; 39 чужих entries сохранены; collection status не менялся | coder/integration; **всегда** |
| **E215-10 / provider guard** | Review `task-delta.diff`; при применимости: загрузить `running-integration-tests`, затем `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor` | `integration-applicability.md`; при true — `integration-tests.log` и provider results. false → `na` с наблюдаемым diff-обоснованием; true → exit 0, реальные provider runs, skipped не passing | coder/integration; **если diff затрагивает provider/runtime/query/cache/executor/materialization behavior** |
| **E215-11 / manifest** | `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D215/rv1/manifest.json` | `manifest.json`, `manifest-validation.log`; exit **0**. Сначала валидировать с этой строкой `open`, затем заполнить и повторно валидировать | coder/manifest; **всегда** |

**Supersession:** отсутствует (начальный rv=1). **CHECK re-gather:** бюджет 2 раунда, владелец `check`, факты через scout, команды через coder, production edits запрещены; после исчерпания — GAP/PLAN, при стойкой низкой уверенности `escalate`.

## L. Риски и уверенность

- **Исходная DLL metadata reference** неизвестна; не влияет на получаемый graph oracle.
- **Stale alias DLL** исходно есть (101376 B); проверка отсутствия значима.
- **Diagnostic-test path:** проверить tracked existence до edits; при расхождении не расширять allowlist молча.
- **Транзитивные потребители:** предупреждение EN/RU и T53 note; не обещать отсутствие compatibility impact.
- **Грязное дерево:** исходная точка — HEAD `03a79285` плюс dirty state. Никаких reset/stash/clean всего дерева.
- **Collection ownership:** collection status строго вне task scope; итог передаётся родителю.
- **Evidence completeness:** plan ready ≠ tests passed; final manifest и CHECK подтверждают каждое обязательство.

**Уверенность:** высокая в чистом удалении и готовности решающего oracle. План **freeze-ready r=1/rv=1**.

## P215-01 readiness — CLOSED

- Schema/validator: `SKILL.md:956–970` (ключи `task`, `tree`, `contract_rv`, `status_file`, `required_rows`, `rows[]`; `status ∈ met|open|na`), `scripts/validate_inner_loop.py:495–610`.
- Verified issue URL: https://github.com/AlexeyShirshov/nextorm/issues/215 (Open, milestone 1.0.9, body "No description provided."). `gh` недоступен → webfetch.
- Baseline: `dotnet list nextorm.slnx package --include-transitive --format json` exit 0, parseable, 25 проектов, OneOf в 24; core top-level requested=resolved `3.0.271`; только `nextorm.core.sourcegenerator` без OneOf.
- Alias TargetDir: `/home/alex/sources/nextorm/tests/nextorm.alias.tests/bin/linux/Debug/net10.0/` — существует; `OneOf.dll` присутствует (101376 B).
- Семь изменяемых content-путей CLEAN; в дереве 39 чужих entries (benchmark artifacts + docs).
- Path contradiction (`JoinAliasGeneratorDiagnosticTests.cs`) — DO existence guard, не blocker.

## Execution / CHECK handoff

- D-единицы D215-01…D215-04 — состояние в DO (не в этом файле при PLAN-freeze); D не помечать `done` по одному отчёту о готовности.
- Actual evidence (команды, exits, artifacts) заполняет `coder` в DO; CHECK PASS от имени coder не записывается.
- Frozen manifest: `artifacts/pdca/D215/rv1/manifest.json`.

## Progress log

- `PLAN r=1 frozen; plan_state=ready` — план и evidence contract rv=1 записаны; граница PLAN→DO, DO не запущен.

## Parent handoff

- Итог T215 для collection parent: plan_state=ready, r=1, rv=1, footprint из восьми путей (см. C), public-API delta = 0 (T53 note в C).
- Task **не** редактировал `docs/specs/status/collection-1.0.9-todo.md`.
- Следующий разрешённый шаг: DO в лейне (D215-01 → D215-04), затем DO→CHECK.
