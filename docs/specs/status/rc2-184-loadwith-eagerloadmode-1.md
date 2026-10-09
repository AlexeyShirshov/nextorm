# D184 — LoadWith: fold AsSingleQuery into an EagerLoadMode parameter

- task_id: D184
- issue: #184 (milestone 1.0.9-rc2)
- selected_variant: pdca-dotnet
- cycle: N=1
- revision: r=2 | iteration 1/3
- evidence contract: EC184-ModeSemantics-v1, rv=1
- plan_state: ready
- base: branch 1.0.9-rc2 @ 18659e41
- status file: docs/specs/status/rc2-184-loadwith-eagerloadmode-1.md
- collection status: docs/specs/status/collection-1.0.9-rc2.md
- phase: DO->CHECK (r=2, n=1) — awaiting CHECK.

## PLAN (r1)

### 1. Goal

D184, issue #184: заменить отдельный публичный `AsSingleQuery()` параметром `EagerLoadMode mode = EagerLoadMode.Default` у `LoadWith`, сохранив существующие SQL, материализацию и число запросов. Режим относится ко всему builder: `Default` наследует ранее выбранный режим, отсутствие выбора означает split, несовместимые явные режимы отвергаются. Удалить старый API без deprecated-алиаса; согласованно обновить потребителей, тесты и EN/RU-документацию.

### 2. Scope

**В scope:** новый публичный enum и изменение единственного `LoadWith` — `src/nextorm.core/Builders/EntityBuilder.cs:518`; удаление `AsSingleQuery` — там же `:567`; представление режима, его копирование и проверки конфликтов; сохранение существующего пути исполнения через `SingleQuery`, `EntityBuilderExtensions.cs:419–469`; все семантически найденные потребители старого API (core, benchmarks, core/SQLite/integration tests); XML-комментарии; API-surface тест вместо отсутствующего public-API tracking; EN/RU-страницы, примеры, ссылки и затронутые якоря; явное supersession прежнего решения в `docs/specs/roadmap/evidence-01-eager-loading-single-query-api.md:44–71`.

**Вне scope:** новая архитектура eager loading, изменение JOIN/stitching/chunking, SQL-планирования или cache policy; добавление PublicApiAnalyzers/ApiCompat, новой общей абстракции либо инфраструктуры mutation testing; поддержка нового eager-loading пути ClickHouse (**deferred**, триггер — включение провайдера в общий eager suite либо отдельное требование; существующие ClickHouse integration tests входят в полную регрессию); коммиты, push, merge, worktrees и перенос работы в другой milestone.

Старый internal spec сохраняется как историческое свидетельство, но его решение о standalone-методе помечается **superseded by D184/#184** (enum, whole-builder semantics, отсутствие compatibility alias). Публичные страницы не ссылаются на internal spec.

### 3. Acceptance criteria

| ID | Наблюдаемый критерий | Негативный случай |
|---|---|---|
| R184-01 | Публичный enum содержит `Default=0`, `SplitQuery=1`, `SingleQuery=2`; `LoadWith` имеет последний необязательный параметр с default `Default`. | Произвольное значение enum отвергается `ArgumentOutOfRangeException`, `ParamName == "mode"`, без изменения исходного builder. |
| R184-02 | `SingleQuery` сохраняет прежние SQL, результаты и один denormalized LEFT JOIN command для всех объявленных коллекций; существующие assertions не ослабляются. | Несколько коллекций, пустые коллекции и повторяющиеся join-строки не превращаются в N+1, не теряют родителей и не дублируют объекты сверх прежнего поведения. |
| R184-03 | Omitted mode, явный `Default` без выбранного режима и `SplitQuery` сохраняют split; один непустой chunk — родительский и один дочерний запрос; при chunking действует прежняя формула. | 1001 ключ не превращается в один неограниченный `IN`, single-query или запрос на каждого родителя; для пустого результата не навязываются «два запроса». |
| R184-04 | Явный режим действует на весь builder, в том числе на коллекции, объявленные раньше с `Default`; последующий `Default` наследует выбор. | `Default` не сбрасывает режим, а поздний `SingleQuery` не оставляет раннюю коллекцию в split. |
| R184-05 | Повтор одинакового явного режима разрешён; `SingleQuery → SplitQuery` и обратный порядок бросают **`NotSupportedException`** с сообщением, называющим конфликтующие режимы и whole-builder ограничение. | Второй режим не побеждает молча, не выполняет запрос и не меняет исходную ветвь builder. |
| R184-06 | Copy/modifier сохраняют режим и load specs; независимые ветви immutable-style builder не влияют друг на друга; существующие terminal guards сохраняются. | Изменение режима клона не меняет исходник; non-stitching terminal не получает новую материализацию или скрытое изменение cache flag. |
| R184-07 | `AsSingleQuery` отсутствует в публичном/унаследованном API; все потребители мигрированы, полный build проходит с 0 warnings/0 errors. | Старое выражение не компилируется; нет alias, extension-замены либо оставшегося наследуемого метода. |
| R184-08 | Все выбранные eager core/SQL-generation tests и полная integration-регрессия проходят; покрытие включённых модулей line ≥85%, branch ≥75%. | Ноль выбранных тестов, skipped обязательного провайдера, незапущенный container-backed suite или отсутствующий coverage report не считаются успехом. |
| R184-09 | EN/RU описывают параметр, значения, наследование и исключение; все публичные ссылки разрешаются, standalone `AsSingleQuery` не упоминается. | Нет старого `.AsSingleQuery()` в публичных примерах, сломанного якоря или ссылки на `docs/specs/**`. |

`NotSupportedException` выбран по существующим eager-path guards — `EntityBuilder.cs:1032/1037/1161/1171`, `EntityBuilderEagerLoading.cs:176`. Duplicate-member `InvalidOperationException` на `EntityBuilder.cs:537` не меняется.

### 4. Variant matrix derived from the execution path

| ID | Варианты/пересечения | Закрытие |
|---|---|---|
| V01 | Omitted / `Default` / `SplitQuery`; одна коллекция | **test:** прежние split results и command count. |
| V02 | `SingleQuery`; одна и несколько коллекций | **test:** прежние SQL/results/один command; SQLite SQL-gen и common integration. |
| V03 | `Default → Single`, `Default → Default → Single` | **test:** все ранее объявленные коллекции переходят в single. |
| V04 | `Single → Default`, `Split → Default`, несколько `Default` после выбора | **test:** наследование, без сброса режима. |
| V05 | `Single → Single`, `Split → Split` | **test:** разрешены; одинаковая стратегия всех коллекций. |
| V06 | `Single → Split`, `Split → Single`, с промежуточным `Default` | **guard + test:** `NotSupportedException`, сообщение и отсутствие мутации. |
| V07 | Неизвестное числовое значение enum, в том числе после допустимого выбора | **guard + test:** `ArgumentOutOfRangeException`; исходник неизменён. |
| V08 | Выбор режима → copy/modifier → дополнительный `LoadWith(Default)`; разветвление одного builder | **test:** расширение `EagerLoadingSingleQueryP1Tests.cs:224`. |
| V09 | Non-stitching terminals и существующие запрещённые eager shapes | **test:** сохранить assertions P1 и many-to-many, меняя только способ задания режима. **guard:** существующие runtime ограничения. |
| V10 | Пустой parent result, пустая collection, несколько children/collections, повторные join-строки | **test:** existing split/single fixtures + новая whole-builder комбинация. |
| V11 | Ключи value/reference, `null`/default, nullable reference mismatch | **guard:** прежний `TKey:notnull` `EntityBuilder.cs:518`; **test:** существующие key/null scenarios сохраняются. Новые типы ключей deferred (триггер — изменение key mapping/chunking). |
| V12 | Split 1001 ключ; single того же размера; filter scopes | **test:** SQLite `:162`, integration `:176`, `:288/:396`. |
| V13 | Sync/async materialization × split/single | **test:** оба dispatch-пути `EntityBuilderExtensions.cs:426–469`. |
| V14 | In-memory и SQLite без DB | **test:** core и SQLite SQL-generation suites. |
| V15 | SQLite/PG/SQL Server/MySQL с DB | **test:** common eager suite и полная integration-регрессия; фактическое выполнение каждого. |
| V16 | ClickHouse | **test:** существующая общая integration-регрессия; **deferred eager coverage**. |
| V17 | src/XML, benchmark, core tests, SQL tests, integration, public docs; generic и inherited builder | **test:** build, reflection surface, Roslyn inventory, docs checks. `JoinedEntityBuilder` наследует изменённый API; собственного overload не добавлять. |
| V18 | Legacy single-mode states без `LoadWith` | **guard:** после удаления метода публично недостижимы. **test:** сохранить проверки достижимых terminal guards. |

**Приоритеты:** R184-01…09 и V01…10/V12…15/V17 — **P1 по построению**. CHECK не понижает эти строки. V16 eager-extension и новые key variants — deferred с указанными триггерами.

### 5. Concrete task list

**Минимальное решение:** режим — состояние всего builder, не каждой load specification. Непреложные ограничения: split default, наследование `Default`, отказ при explicit conflict, immutable-style копирование, отсутствие старого API и изменения исполнения. Оптимум: хранить выбранный `EagerLoadMode` (изначально `Default`), сохранив внутренний `SingleQuery` как вычисляемый признак для существующего dispatch.

| Подход | Плюсы | Минусы / цена / риск |
|---|---|---|
| **Enum-state + вычисляемый `SingleQuery`** (выбран) | Различает unresolved default и explicit split; минимально затрагивает consumers. | Нужно проверить все copy paths. Низкая цена/риск. |
| Bool + отдельный «явно выбран» flag | Можно сохранить bool. | Два поля и недопустимые комбинации; дороже reasoning/copy tests. |
| Режим в каждом `EagerLoadSpec` | Локальные спецификации. | Противоречит whole-builder contract. Не выбирать. |

Не создавать стратегию/интерфейс: второго потребителя новой абстракции нет.

**Единицы:**
- **P:184-01 — текущий PLAN, fix now.** Зафиксировать r=1/rv=1, scope, матрицу и границу COLLECTION. Завершение — этот текст записан coder; никаких D-действий.
- **P:184-02 — предпосылки будущего DO, fix before DO.** Точечный scout по отсутствующим контрактам/командам из §14.
- **D:184-01 — baseline и inventory, после разрешения DO.** Проверить base/branch; Roslyn `refs` для обоих методов, copy paths, inherited API, terminal cases; запустить прежние eager tests, сохранить baseline assertions/results; зафиксировать issue URL и milestone.
- **D:184-02 — API и state.** `EntityBuilder.cs:49/99/111/518/567`; enum, optional parameter, invalid-value/conflict guards; удалить метод; проверить clone/modifier paths. Dispatch `EntityBuilderExtensions.cs:419–469` не перестраивать.
- **D:184-03 — tests/API surface.** `EagerLoadingTests.cs`, `EagerLoadingSingleQueryTests.cs`, `EagerLoadingSingleQueryP1Tests.cs`, `JoinIntoManyToManySingleQueryTests.cs`; V01–13/V17–18 и reflection-check.
- **D:184-04 — consumers и provider evidence.** `SqliteBenchmarkEagerLoading.cs:118`, SQLite SQL tests `:93/206/253/306`, `CommonTestSuite.EagerLoading.cs`; мигрировать все Roslyn references.
- **D:184-05 — docs и supersession.** Страницы из §8, XML `EntityBuilder.cs:107/1149`, internal spec и записи ACT.
- **D:184-06 — boundary verification.** Выполнить §15; D остаются активными до принятия CHECK.
- **Deferred:** ClickHouse eager support, новые key semantics, API tooling, mutation infrastructure — по триггерам.

Зависимости: D01 → D02 → D03/D04 → D05 → D06. D03/D04 последовательно из-за пересечения тестовых файлов.

### 6. Risks

Главный: bool не различает явный split и default. Режим может потеряться в copy/modifier path (`EagerLoadingSingleQueryP1Tests.cs:224`). Механическая замена `.AsSingleQuery()` может выбрать режим не на том builder. «Два round trips» могут ошибочно применить к пустому результату. SQL snapshots могут быть изменены вместо доказательства equivalence. Интеграция может сообщить green при skipped provider. Точные CI commands не подтверждены (см. §14) — предпосылка-блокер DO. Суммарные scout counts приблизительны: закрывать по новому Roslyn inventory.

### 7. Test strategy

**Shared contract:** `EC184-ModeSemantics-v1` (R184-01…09/V01…18). Tests, XML и EN/RU одинаково описывают whole-builder mode, `Default`, конфликт и exception type.

- **Unit:** state machine, оба порядка конфликтов, unknown enum, copy/branch isolation, inherited surface, non-stitching guards.
- **Behavioral core:** прежняя материализация, несколько коллекций, empty/duplicates, sync/async.
- **SQL-generation:** неизменность LEFT JOIN single и chunked split IN; filter-scope parity.
- **Integration:** реальные results и command counts, все доступные common eager providers; полная suite проверяет существующий ClickHouse.
- **Coverage:** line ≥85%, branch ≥75% по `coverage.settings.xml` (core/sqlite/postgres/sqlserver); оба порога обязательны и на `1.0.9-rc2`.
- **Branch coverage:** явно пройти unresolved/default, выбор обоих режимов, повтор, обе стороны конфликта, unknown value и копирование.
- **Mutation:** не вводить Stryker (конфигурации нет); обосновано локальным API/state изменением и прямыми branch/negative tests.

**Structured test scope:**

| Поле | Значение |
|---|---|
| Projects | `tests/nextorm.core.tests`, `tests/nextorm.sqlite.tests`, `tests/nextorm.integration.tests`; solution build и benchmark build. |
| Inner-loop selectors | core: `FullyQualifiedName~EagerLoading`, отдельно `~JoinIntoManyToManySingleQueryTests`; SQLite: `~EagerLoadingSqlGenerationTests`. |
| Files | Четыре core-файла §5, `EagerLoadingSqlGenerationTests.cs`, `CommonTestSuite.EagerLoading.cs`. |
| Rebuild | После каждого изменения API/state — solution Debug build; не использовать `--no-build` с устаревшим API. Boundary — повторный build после всех правок. |
| Boundary | Полные core/SQLite tests, полная container-backed integration suite, coverage, docs build, semantic/API checks. |
| Rationale | Фильтры дают быстрый локальный цикл; полные suites ловят соседние guards и shared-command regressions; integration нельзя заменить SQL snapshots. |

### 8. Docs plan

**Обязательные обе языковые версии:** `docs/advanced/eager-loading.md`, `docs/ru/advanced/eager-loading.md`, `docs/advanced/relationships.md`, `docs/ru/advanced/relationships.md`, `docs/advanced/query-filters.md`, `docs/ru/advanced/query-filters.md`. Последние четыре пути — планируемые, scout назвал basename без подтверждения каталога: **перед записью получить точные пути**, не создавать дубликаты по предположению.

Также проверить/исправить: `docs/index.md`, `docs/ru/index.md`, `docs/toc.yml`, `docs/ru/toc.yml` (RU hit `:160`); все остальные публичные mentions под `docs/**`, `docs/ru/**`, `readme.md`; XML-комментарии `EntityBuilder.cs:107/1149` и документацию `LoadWith`.

Описать все enum values, whole-builder ordering, исключение, split/chunking и breaking removal. Примеры показывают параметр `mode`. Не переносить/перенумеровывать страницы без необходимости.

**Internal:** supersession в `docs/specs/roadmap/evidence-01-eager-loading-single-query-api.md:44–71`; status/ACT отражают новое решение, отсутствие public-API registry и выбранный surface test. Исторические упоминания `AsSingleQuery` в internal spec допустимы; публичные — нет.

### 9. Performance-measurement decision

**Acceptance benchmark не нужен.** Выбор/валидация режима — при построении builder (`EntityBuilder.cs:518`); bool-предикат потребляется до построения/материализации command (`EntityBuilderExtensions.cs:419–426`). Не меняются per-row stitching, chunking, query planner и plan-cache policy; новых аллокаций на строку не добавлять. Benchmark consumer `SqliteBenchmarkEagerLoading.cs:118` мигрировать и собрать, но `--anyCategories=acceptance` не обязателен для этой ревизии.

**Триггер пересмотра:** DO меняет command execution, cache policy, stitching или добавляет per-row работу — тогда PLAN добавляет measurement row и baseline на `18659e41`.

### 10. Reconnaissance/prototype decision

Архитектурный spike не нужен (три состояния enum достаточны, dispatch/execution paths известны). Точечная разведка до DO нужна: глобальный versioned evidence contract и repo overlay; CI coverage/tool commands; точные doc paths, copy/terminal consumers и API surface; container prerequisites и issue/milestone verification. Наблюдаемый результат — выдержки контрактов, точные команды/пути, Roslyn symbols/references и подтверждённый URL. Отсутствие отчёта не даёт права ослабить контракт или начать DO.

### 11. Unit execution mode

**Последовательно в одном текущем дереве**, после отдельного разрешения COLLECTION на DO. API, state tests, consumer migration и docs делят один контракт и пересекающиеся файлы; изоляция worktree не даёт выгоды. Сейчас: только запись PLAN.

### 12. Footprint

Планируемые записи при будущем DO:
- `src/nextorm.core/EagerLoadMode.cs` — новый файл (публичный namespace согласовать с `EntityBuilder<TEntity>`)
- `src/nextorm.core/Builders/EntityBuilder.cs`
- `benchmarks/nextorm.benchmark/SqliteBenchmarkEagerLoading.cs`
- `tests/nextorm.core.tests/{EagerLoadingTests,EagerLoadingSingleQueryTests,EagerLoadingSingleQueryP1Tests,JoinIntoManyToManySingleQueryTests}.cs`
- `tests/nextorm.sqlite.tests/EagerLoadingSqlGenerationTests.cs`
- `tests/nextorm.integration.tests/CommonTestSuite.EagerLoading.cs`
- публичные docs и internal spec из §8
- `docs/specs/status/rc2-184-loadwith-eagerloadmode-1.md`
- evidence artifacts: `artifacts/pdca/rc2-184/r1/**`

`EntityBuilderExtensions.cs` и `EntityBuilderEagerLoading.cs` — read/verification footprint; не планировать runtime-переписывание.

**Uncertainty:** точные relationship/query-filter paths, дополнительные public mentions, места всех copy assignments и наличие внешнего registry overlay не подтверждены. Roslyn/text inventory закрывает эти пробелы. CRLF сохранять; CPM/tool versions не менять.

### 13. Predecessor-result requirements

- Функциональных predecessor tasks не заявлено.
- Перед DO подтвердить ветку `1.0.9-rc2`, base `18659e41` и отсутствие конфликтующих локальных изменений.
- Если collection интегрировала другие результаты — записать фактический HEAD и их footprint; не сбрасывать дерево на base.
- Исходные eager tests должны проходить либо иметь отдельно подтверждённый существующий дефект.
- Issue #184 должен оставаться в milestone `1.0.9-rc2`; verified URL записать до implementation handoff.

### 14. Assumptions / prerequisites

| Пробел | Закрытие / классификация |
|---|---|
| Полный текст `Versioned evidence contract` глобального `pdca-dotnet`/`nextorm-pdca` не передан в PLAN-бриф. | **Предусловие будущего DO:** точечный scout (P:184-02) передаёт определения/обязательства; соответствие подтверждается до DO. |
| Точные CI coverage commands/tool setup отсутствуют. | **Предусловие:** scout читает `.github/workflows/dotnet.yml`, `coverage.settings.xml` и tool manifests; команды §15 проверяются на соответствие окружению. |
| Integration skill/socket status отсутствуют. | **Предусловие:** загрузить `.opencode/skills/running-integration-tests/SKILL.md`; обеспечить containers; missing socket → предусмотренное skill восстановление. |
| Точные doc paths/registry overlay неизвестны. | Scout подтверждает; не создавать предполагаемые registry/pages. |
| Namespace enum и полный clone footprint не переданы. | Roslyn-first inspection до D02. |
| GitHub URL не проверен. | Scout проверяет remote → issue #184 → milestone. |
| ClickHouse не входит в common eager suite. | Принятое ограничение coverage §2. |

**Классификация:** недостаток evidence и аддитивные предусловия, не доказанный внешний блокер. D-единицы остаются активными, до выполнения зависимостей — blocked, не superseded. Если targeted scout сохраняет низкую уверенность — `escalate` по триггеру 5. Уверенность высокая в mode-state решении и scope, средняя в полноте operational commands. Новая ревизия r из-за отсутствующего отчёта не создаётся.

### 15. Evidence contract

**Контракт:** `EC184-ModeSemantics-v1`, rv=1. Все источники ниже — планируемые, не полученные. Stable requirement IDs — R184-01…09; stable row IDs — E184-01…11. Предыдущей ревизии нет.

Все команды выполняются из repo root только в будущем DO/CHECK. `$E` = `artifacts/pdca/rc2-184/r1`; coder заранее создаёт каталог. stdout/stderr сохраняются в лог, фактический exit code — в `exit-codes.txt` (`tee` с `set -o pipefail`).

**Планируемые вызовы:**

C01 — окружение/base:
```text
git branch --show-current
git rev-parse --short=8 HEAD
git status --short
dotnet --info
```

C02 — semantic inventory:
```text
roslyn(action="refs", symbol="NextORM.EntityBuilder<TEntity>.AsSingleQuery")
roslyn(action="refs", symbol="NextORM.EntityBuilder<TEntity>.LoadWith")
```
Точные Roslyn symbol keys подтверждает `structure/types/members`, если display name не принимается; текстовым поиском символ не разрешать.

C03 — builds:
```text
dotnet build nextorm.slnx -c Debug
dotnet build benchmarks/nextorm.benchmark -c Release
```

C04 — inner loop:
```text
dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~EagerLoading"
dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~JoinIntoManyToManySingleQueryTests"
dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~EagerLoadingSqlGenerationTests"
```

C05 — полные локальные suites:
```text
dotnet test tests/nextorm.core.tests -c Debug
dotnet test tests/nextorm.sqlite.tests -c Debug
```

C06 — обязательная integration boundary:
```text
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
```

C07 — coverage collection/report (планируемое):
```text
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet-coverage collect --settings coverage.settings.xml --output artifacts/pdca/rc2-184/r1/coverage.cobertura.xml --output-format cobertura "bash -c 'dotnet test tests/nextorm.core.tests -c Debug && dotnet test tests/nextorm.sqlite.tests -c Debug && dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor'"
reportgenerator "-reports:artifacts/pdca/rc2-184/r1/coverage.cobertura.xml" "-targetdir:artifacts/pdca/rc2-184/r1/coverage-report" "-reporttypes:Html;TextSummary;Cobertura"
```
Exact tool availability/CI соответствие проверяются P:184-02. CHECK читает line/branch totals: оба ≥85/75, четыре модуля, exclusions как в CI.

C08 — public text/docs:
```text
git grep -n -F "AsSingleQuery" -- docs/advanced docs/ru/advanced docs/guide docs/ru/guide docs/index.md docs/ru/index.md docs/toc.yml docs/ru/toc.yml readme.md
git grep -n -F "AsSingleQuery" -- docs
dotnet docfx docs/docfx.json
```
Первый вызов ожидает exit 1 (нет matches в перечисленных публичных областях). Второй может вернуть exit 0 из-за historical/internal evidence; CHECK классифицирует каждый match вне `docs/specs/**`. DocFX: exit 0 без unresolved-link/xref diagnostics.

**Строки контракта:**

| Row / requirement | Проверка и predicate | Evidence / вызов | Результат, артефакты | Владелец / rv |
|---|---|---|---|---|
| E184-01 / R184-01…09 | Preconditions, безусловно | C01; scout-выписки skills/CI; verified GitHub metadata | Exit 0 C01; branch/base либо согласованный predecessor HEAD; полный gate checklist; `$E/preconditions.md`, `$E/environment.log` | scout → coder; CHECK / 1 |
| E184-02 / R184-02/03/06 | Baseline до API-правки, безусловно | Прежние assertions + C04; inventory C02 | selectors >0 tests, 0 failures; исходные SQL/results/count oracles перечислены; `$E/baseline.log`, `$E/baseline-oracles.md` | coder / 1 |
| E184-03 / R184-01/04/05/06 | Все mode/state variants V01–09/V13 | Test results C04/C05, diff review | Exit 0, >0 selected, 0 failures; обе конфликтные ветви, invalid enum, inheritance/copy доказаны; `$E/core-focused.log`, `$E/core-full.log`, `$E/variant-map.md` | coder; CHECK / 1 |
| E184-04 / R184-02/03 | SQL/chunk/filter variants V10/V12 | C04/C05 SQLite | Exit 0; неизменные SQL/count assertions; `$E/sqlite-focused.log`, `$E/sqlite-full.log` | coder; CHECK / 1 |
| E184-05 / R184-02/03/08 | Container integration, безусловно; eager ClickHouse не заявлен | C06 | Exit 0, 0 failures; eager tests SQLite/PG/SQLServer/MySQL выполнены; ClickHouse regression выполнена; provider skip ⇒ не PASS; `$E/integration.log`, `$E/provider-matrix.md` | coder / integration; CHECK / 1 |
| E184-06 / R184-07 | API removal/migration, безусловно | C02; reflection tests; C03 | Старый symbol отсутствует; reflection surface без метода; новый optional enum parameter корректен; build 0/0; `$E/api-inventory.md`, `$E/build-debug.log`, `$E/build-benchmark.log` | coder; CHECK / 1 |
| E184-07 / R184-08 | Coverage, безусловно | C07 | Collection/report exit 0; line ≥85%, branch ≥75%; raw report, `coverage-report/**`, `$E/coverage.log`, `$E/coverage-verdict.md` | coverage stream; CHECK / 1 |
| E184-08 / R184-09 | EN/RU/public references, безусловно | C08 | Exit rules C08; каждый public old-name match устранён; links/xrefs разрешены; `$E/docs-search.log`, `$E/docfx.log`, `$E/docs-review.md` | docs/coder; CHECK / 1 |
| E184-09 / R184-01…09 | Shared contract/internal supersession/scope | CHECK сопоставляет R/V с tests/docs/status и internal spec §8 | Ни одна P1 строка не понижена; old decision superseded; issue/milestone verified; `$E/contract-review.md` | CHECK / 1 |
| E184-10 / R184-02/06 | Perf applicability review (measurement только при execution/cache/per-row изменениях) | CHECK diff vs `EntityBuilder.cs:518`, `EntityBuilderExtensions.cs:419–469` | `$E/perf-applicability.md` подтверждает отсутствие таких изменений; predicate true ⇒ возврат PLAN | CHECK / 1 |
| E184-11 / R184-01/06/07 | Design/sealedness/cache invariants | Roslyn surface/impact + diff | Нет новой абстракции без второго consumer, sticky `Cache=false`, изменений TVP/precision или sealing базового builder; `$E/design-review.md` | CHECK / 1 |

**CHECK re-gather budget:** максимум два адресных раунда внутри одного CHECK; первый — конкретный отсутствующий artifact, второй — конкретное противоречие. Исчерпание бюджета не превращает missing evidence в PASS.

### 16. Design checklist verdict

| Область | Verdict / действие |
|---|---|
| SOLID/type design | **PASS по плану:** enum выражает ограниченный выбор; mode принадлежит builder, не load specs; runtime guards для invalid enum и conflict обязательны. |
| DRY/abstraction | **PASS:** один mode resolver/validation path; не добавлять strategy abstraction при одном consumer. |
| Compatibility | **Намеренный breaking change:** метод удаляется; optional parameter не обещает binary compatibility старой сигнатуры; записать в ACT. |
| Perf anti-patterns | **PASS при соблюдении scope:** нет per-row branch/allocations, N+1, смены chunking и sticky command cache flag. |
| Sealedness | Enum не требует решения о sealing. `EntityBuilder<TEntity>` не seal: `JoinedEntityBuilder<T1,T2>` наследует его (`JoinedEntityBuilder.cs:13`); новые классы не нужны. |
| Existing registries | Public-API files/analyzers не обнаружены; в ACT записать выбранный reflection surface check; прежний API decision register superseded, не удалён. |
| Project invariants | CRLF, warnings-as-errors, milestone rc2, EN/RU совместно; TVP metadata/decimal precision не затрагиваются. |

**Итог:** минимальное решение выбрано; исходные acceptance обязательства сохранены. Document = phase-P план. Остановиться на PLAN→DO; gate 1 не объявлять закрытым до получения обязательных контрактов и подтверждения operational prerequisites.

## Progress log

- P: PLAN(r1) recorded by coder; plan_state=ready; DO not started. Phase P (COLLECTION TASK PLAN) stopped at the PLAN→DO boundary.
- 2026-10-08T06:29Z | DO | revision r=1 | iteration 1/3 | DO completed (D02-D06) | evidence: artifacts/pdca/rc2-184/r1/{evidence-d06.json,build-debug-d06.log,build-release-d06.log,build-benchmark-release-d06.log,core-full-d06.log,sqlite-full-d06.log,integration-eager-d06.log,integration-full-d06.log,coverage-report/Summary.txt,docfx-d06.log,docs-search.log}; boundary: Debug/Release build 0 warnings/0 errors; core 1765 passed/0 failed/0 skipped; sqlite 1161 passed/0 failed/1 skipped; eager integration 52 passed/0 failed, 4 providers no skips; full integration 3337 total/0 failed; coverage line 85.9% branch 76.5%; docfx exit 0. Awaiting CHECK.
- 2026-10-08T06:42Z | CHECK | revision r=1 | iteration 1/3 | CHECK re-gather: manifest + full integration + coverage bound to tree | evidence: artifacts/pdca/rc2-184/r1/{manifest.json,integration-full-regather.log,integration-perprovider-Postgres.log,integration-perprovider-SqlServer.log,integration-perprovider-MySql.log,integration-perprovider-ClickHouse.log,integration-perprovider-Sqlite.log,integration-eager-regather.log,integration-eager-perprovider-*.log,coverage-regather/coverage.cobertura.xml,coverage-regather/report/Summary.txt,coverage-regather/core-sqlite-only/report/Summary.txt}; HEAD 4bd18c82f5700a3694730a26edefc3ceab91186f; full integration total 3337 / 0 failed / 197 skipped, exit 0; eager 52 passed / 0 failed / 0 skipped; coverage C07 (core+sqlite+integration) line 85.9% branch 76.5%, exit 0; core+sqlite-tests-only line 77.2% branch 70.0%; manifest 25 D184 files sha256
- 2026-10-08T06:49Z | DO | revision r=1 | iteration 2/3 | DO iteration 2 (n=2/3): closed 6 mandatory variant assertions | evidence: artifacts/pdca/rc2-184/r1/{evidence-n2.json,scope-n2.json,exit-codes-n2.txt,build-debug-n2.log,core-eagermode-n2.log,core-eager-n2.log,core-full-n2.log,sqlite-full-n2.log,integration-eager-n2.log}; file: tests/nextorm.core.tests/EagerLoadingModeTests.cs; build 0W/0E; EagerLoadingModeTests 15 passed/0 failed; EagerLoading 102 passed/0 failed; core full 1771 passed/0 failed/0 skipped; sqlite full 1160 passed/1 skipped; eager integration (DOCKER_HOST) 52 passed/0 failed/0 skipped; validate_inner_loop brief=0 report=0. Phase DO->CHECK awaiting CHECK (n=2/3).
- 2026-10-08T06:57Z | DO | revision r=1 | iteration 3/3 | DO iteration 3 (n=3/3): shared-AnyCommand cache-invariance test | evidence: artifacts/pdca/rc2-184/r1/{evidence-n3.json,scope-n3.json,build-debug-n3.log,core-eagermode-n3.log,core-full-n3.log,core-eagermode-n3-red.log}; file: tests/nextorm.core.tests/EagerLoadingModeTests.cs; build 0W/0E; EagerLoadingModeTests 16 passed/0 failed; core full 1772 passed/0 failed/0 skipped; red probe (shared AnyCommand.Cache=false) 1 failed exit 2 then reverted; validate_inner_loop brief=0 report=0. Phase DO->CHECK awaiting CHECK (n=3/3).
2026-10-08T07:07Z | CHECK | revision r=1 | iteration 3/3 | E184-02/03 evidence-completion: baseline API inventory (old AsSingleQuery declaration + base usages) and full-integration totals reconciliation | evidence: artifacts/pdca/rc2-184/r1/{baseline-inventory.txt,integration-totals-reconciliation.txt}
2026-10-08T07:13Z | CHECK | revision r=1 | iteration 3/3 | E184-01/08 evidence completion: regenerated manifest bound to the iteration-3 tree (26 files; EagerLoadingModeTests.cs hash drift fixed) and wrote preconditions.md / environment.log / docs-review.md; C08a public-scope AsSingleQuery exit=1, DocFX exit 0 (2 pre-existing duplicate-file warnings, 0 errors) | evidence: artifacts/pdca/rc2-184/r1/{manifest.json,preconditions.md,environment.log,docs-review.md,docs-review-c08.log}

- 2026-10-08T12:27Z | DO | revision r=2 | iteration 1/3 | r2 evidence-only re-run: contract-named artifact bundle under `artifacts/pdca/rc2-184/r2/` + fresh pre-API baseline at `4bd18c82` | evidence: artifacts/pdca/rc2-184/r2/{preconditions.md,environment.log,baseline.log,baseline-oracles.md,core-focused.log,core-full.log,variant-map.md,sqlite-focused.log,sqlite-full.log,sqlite-skip.md,integration.log,provider-matrix.md,api-inventory.md,build-debug.log,build-benchmark.log,coverage.log,coverage-verdict.md,docs-search.log,docfx.log,docs-review.md,contract-review.md,perf-applicability.md,design-review.md,check-evidence-matrix.md}; baseline build 0W/0E exit 0, core eager 87/87/0, sqlite eager 8/8/0, m2m 3/3/0, all exit 0; ClickHouse eager deferred. Phase DO->CHECK (r=2, n=1).
