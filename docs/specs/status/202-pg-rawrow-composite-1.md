---
# PDCA 202 — Fix: PostgresRawRowTests misclassify unmapped hstore/ltree as named composite on a clean Postgres

- **Issue:** #202 — https://github.com/AlexeyShirshov/nextorm/issues/202
- **Milestone:** 1.0.9-rc1
- **Ветка:** 1.0.9-rc1
- **Цикл:** 1 · **Ревизия:** r4 · **Итерация:** n=1
- **Режим:** normal (ведёт cheap-оркестратор; DO стартует по `go`)
- **Фаза:** ACT / EXIT (цикл 1 завершён; E09 — post-cycle release gate)

## Цель

Вернуть корректную классификацию raw-row, чтобы CI job `build` снова стал зелёным и релиз `v1.0.9-rc1` разблокировался, **не** ослабляя диагностику настоящих композитов и **не** внося Npgsql-зависимость в `nextorm.core`.

## Первопричина

`RawMapperFactory.TryGetUnresolvableCompositeColumn` (`src/nextorm.core/DataContext/RawMapperFactory.cs:369-401`) классифицирует колонку как неразрешимый named composite **только по форме исключения драйвера**: `if (probeError is not InvalidCastException) continue;` (`:380`), затем `BuildEntitySelectList` бросает `UnsupportedRawRow(...)` (`NotSupportedException`, `:617-618`). На **чистом** каталоге Npgsql 10.0.3 на `public.hstore`/`public.ltree` (не композиты) кидает `InvalidCastException` → ложная классификация; на **reuse**-контейнере extensions уже есть → `NotSupportedException` → guard пропускает → обычная ошибка (маскировка локально, красно на чистом CI).

## Критерии приёмки

| ID | Наблюдаемое | Негативный кейс |
|---|---|---|
| R01 | hstore на **чистом** каталоге (multi-col, `PostgresRawRowTests.cs:505-524`) → `InvalidOperationException`, `None of the result-set columns*`, без `named composite` | ДО фикса должно воспроизводиться падение (red↔green) |
| R02 | То же для ltree (`:526-545`) | Композитная диагностика не появляется лишь из-за `InvalidCastException` |
| R03 | Настоящий **незарегистрированный** композит сохраняет named-composite rejection | Никакого тихого фолбэка в обычную ошибку |
| R04 | Зарегистрированный композит по-прежнему материализуется; preserve existing intentional registered rejections | Регистрация не делает неподдержанную форму молча валидной |
| R05 | Single- и multi-column пути используют одну авторитетную классификацию; форма исключения / dotted-имя / clean-warm не меняют вердикт | `public.hstore`/`public.ltree` — не композиты |
| R06 | Core остаётся без Npgsql; детект без каталог-запроса и вне row-loop | Нет per-row детекта, нет exception/name-only фолбэка |
| R07 | Build 0W/0E; нужные тесты и реальный CI `build` зелёные; контейнерные провайдеры исполняются, не скипаются | Локальный warm-only успех или skipped-provider прогон не закрывают #202 |

## Решение (подход)

**Provider-specific authoritative composite-type detection**, общий для single- и multi-column путей. Не выводить «композит» из типа исключения или dotted-имени.

- **Seam:** на существующей границе провайдера (`DataContext.cs:677-681` `SupportsRawRowColumns`/`RawRowColumnsSupported`; Postgres override `PostgresDataContext.cs:231`). Postgres даёт авторитетную классификацию (по метаданным ридера/типа Npgsql); core использует её в `RawMapperFactory.cs:331-401`, сохраняя решения на `:617-618`.
- **Отклонено:** запрос `pg_type.typtype` (лишний DB-round-trip на per-result-set пути); сужение exception-shape / повтор dotted-name (hstore тоже dotted → не различает).
- **Confidence:** диагностика — высокая; механизм — средняя до пробы D1 (какой именно Npgsql-API даёт авторитетную классификацию при clean/stale каталоге).

## Задачи

- **D0** Предпосылки/политики (см. ниже) — записи в status до DO.
- **D1** Проба метаданных (Обязательная рекогносцировка): семантика Npgsql-API при (a) загрузке типов до создания extension, (b) независимой DDL-связи, (c) чтении hstore/ltree, (d) настоящем незарегистрированном композите, (e) warm-повторе. Pass-предикат: метаданные ридера отличают настоящий композит и исключают extensions в обоих состояниях **без** доп. SQL. Fail → DO → PLAN.
- **D2** Регресс-фикстуры и тесты: `tests/nextorm.core.tests/RawRowMaterializerTests.cs:434-449,777-785,823-824` (разделить семантическую классификацию и форму исключения); `PostgresRawRowTests.cs:505-545` (изолированные clean/warm сценарии, воспроизвести исходное clean-падение до D3).
- **D3** Продуктовая правка: минимальная capability на границе провайдера по результату D1; Postgres даёт классификацию, core использует консистентно.
- **D4** Остаток матрицы + регрессы: core-тесты, контейнерная интеграция, roslyn impact, CRLF/warnings-as-errors.
- **D5** Документация EN/RU + XML-doc новой protected-capability; build/tests/coverage/docs; сбор реального CI `build` для ревизии.

**Deferred (в milestone 1.0.9-rc1, с триггером):** catalog-query и domain-over-composite — только по триггеру (запрос поддержки/наблюдённая регрессия/проба показывает влияние на R03/R05).

## Test strategy и variant matrix

Unit доказывает классификацию независимо от формы исключения драйвера; integration — реальное поведение Npgsql.

| Строка \ состояние | Single clean | Single warm | Multi clean | Multi warm |
|---|---|---|---|---|
| V01 genuine registered supported | тест | тест | тест | тест |
| V02 genuine unregistered rejection-shape | тест | тест | тест | тест |
| V03 unknown non-composite hstore | тест | тест | тест | тест |
| V04 unknown non-composite ltree | тест | тест | тест | тест |
| V05 ordinary domain | тест | тест | тест | тест |
| V06 ordinary enum | тест | тест | тест | тест |

Плюс: `DBNull` vs non-null (классификация метаданными, не значением); reference/value цели без новой поддержки; другие провайдеры — полный solution-прогон; без новых флагов/cache-key измерений (`MapperCache.cs:45-52`).

## Docs plan

Обновить EN + RU: `docs/guide/12-raw-sql.md`, `docs/guide/provider-specific/postgresql.md`, `docs/advanced/limitations.md` (+ зеркала RU). Уточнить, что named-composite диагностика — про **настоящие** композиты, а не любой незамапленный тип PG. Не подразумевать новую поддержку hstore/ltree/domain/enum. Публичных rename/removal нет; не линковать публичные страницы на specs.

## Perf / Recon / Unit mode

- **Perf:** приёмочный бенчмарк **не нужен** — детект идёт раз на result set до row-loop (`ProcedureResult.cs:136`), правка заменяет дискриминатор ограниченным in-memory чтением метаданных; это не speed-claim. D1 подтверждает отсутствие доп. SQL и отсутствие ухода в row-loop; при ином — вернуться в PLAN с планом замера.
- **Recon:** обязательная проба D1 (единственная материальная неизвестность).
- **Unit mode:** sequential, **одно рабочее дерево** (общий контракт и пересекающиеся файлы); без commit/merge/push.

## Evidence contract (rv=1)

| Ряд | Требование | Сценарий/источник | Инвокация / требуемый результат | Владелец |
|---|---|---|---|---|
| E01 | R05,R06 | проба D1: наблюдения ридера | `dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~PostgresRawRowTests"` (с DOCKER_HOST); exit 0, манифест D1 | coder |
| E02 | R01-R05 | unit-классификация | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~RawRowMaterializerTests"`; exit 0 | coder/check |
| E03 | R01-R05 | все V01-V06 + исходное clean-падение до D3 | `DOCKER_HOST=... dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~PostgresRawRowTests"`; post-fix exit 0 | coder/check |
| E04 | R06 | semantic impact | `scout`: roslyn-first (capability defs/refs, provider impls, package refs) | scout |
| E05 | R07 | полная сборка | `dotnet build nextorm.slnx -c Debug`; exit 0, 0W/0E | coder/check |
| E06 | R03-R07 | полный regression | `DOCKER_HOST=... dotnet test nextorm.slnx -c Debug`; exit 0, провайдеры исполнены | coder/check |
| E07 | R07 | coverage | `dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build"` + `reportgenerator`; Line ≥85 / Branch ≥75 | coder/check |
| E08 | R06,R07 | доки EN/RU + CRLF | `scout`: 6-страничный mirror-чеклист, API-change review, diff формата | scout/check |
| E09 | R07 | реальный CI `build` | `scout`: URL рана, ревизия, conclusion = success | scout/check |

## D0 — предпосылки (разрешены)

- Integration skill: `.opencode/skills/running-integration-tests/SKILL.md`; `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`; прогон со скипом провайдеров — не pass.
- Coverage: команда из `.github/workflows/dotnet.yml`; пороги Line 85 / Branch 75 (`main` — hard-fail).
- CRLF; build gate `dotnet build nextorm.slnx -c Debug` = 0W/0E; CPM — версии только в `Directory.Packages.props`.
- Инварианты nextorm: абстракция только на 2-м потребителе/наблюдаемом шве; KISS; measure-never-assert; ничего за пределы milestone 1.0.9-rc1.

## Ревизия r2 (DO → PLAN)

- D1 проба не выполнила pass-предикат (см. Progress log): в clean-каталоге hstore/ltree и настоящий composite неразличимы in-memory.
- **Решение r2 (вариант C):** resolved-тип → `reader.GetPostgresType(i) is PostgresCompositeType`; `UnknownBackendType` → авторитетная серверная классификация по OID (`pg_catalog.pg_type` + `pg_namespace`), кэш в пределах подготовки одного result set. **R05 сохраняется без ослабления.** Отклонены A (только прогрев fixture), B (каталог всегда), D (ослабить R05).
- **Задачи r2:** P:r2 (узкая эскалация — способ lookup при открытом reader/транзакции) → D2 (проверка доступа к метаданным при открытом reader, вкл. транзакцию) → D3 (provider-классификатор + общий schema seam, result-set кэш) → D4 (изолированные clean/warm тесты, single/multi, clean genuine-composite) → D5 (замер I/O и latency) → D6 (docs EN/RU + build/coverage/полный provider CHECK). Незавершённые продуктовые D r1 остаются активными, **blocked** на P:r2.
- **Перф-льгота r1 снята:** замер нужен (warm — 0 доп. lookup; unresolved — ≤1 пакетный lookup на result set; число lookup не растёт со строками).
- **Contract supersession:** rv1 → rv2, после решения эскалации (сохранить все существующие ID).
- **Unit mode:** sequential, одно дерево.

## Ревизия r3 (sealed) — r=3, n=1, contract rv2

- **Решение (эскалация):** выполнять **D** механизмом **A** — менять только тесты/фикстуру; продуктовый код и публичная поверхность не меняются. B/C (OID-lookup) отклонены.
- **R05 → R05′:** «Классификация неразрешённой колонки опирается только на метаданные, наблюдаемые драйвером на открытом reader (без доп. обращений к серверу и без per-row детекции) и детерминирована для состояния каталога типов. Состояние, в котором каталог загружен до создания типа, — известное ограничение драйвера: такая колонка неотличима от незарегистрированного композита и получает диагноз named-composite. Фикстуры обязаны явно обеспечивать тёплый каталог.» Требование «одна общая классификация single/multi путей» снято из приёмки → follow-up. R01–R04, R06, R07 сохранены.
- **Задачи (sequential, одно дерево):**
  - **D-PROBE** (recon): как `CreateContext()` получает data source; проверить `NpgsqlConnection.ReloadTypes()` vs `NpgsqlDataSource.ReloadTypesAsync()` vs init-script на **фактических** контекстных соединениях. Pass: контекстные соединения дают warm (hstore → `public.hstore`/`PostgresBaseType`). Fail: доказательство относится лишь к DDL-соединению / чужому data source.
  - **D-FIXTURE**: реализовать доказанный прогрев; изолировать clean-сценарии от warm-пула/каталога; init-script fallback — только на свежем изолированном контейнере.
  - **D-TESTS**: warm R01/R02/R03 + V01–V06, clean genuine-composite, clean-limitation (без пиннинга типа/текста), R04 без изменений.
  - **D-RECORDS/DOCS**: записать r3/rv2, supersession rv1→rv2 (E01–E09 сохранены); ограничение в `docs/guide/12-raw-sql.md`, `docs/guide/provider-specific/postgresql.md`, `docs/advanced/limitations.md` + RU-зеркала; follow-up на унификацию путей и на вводящую в заблуждение диагностику `-.-`.
  - **D-VERIFY**: E01–E12.
- **Матрица:** warm R01/R02/R03; existing V01–V06 + R04; clean genuine-composite (явно); clean-limitation (фиксирует ограничение, не пиннит исключение); прочие провайдеры — полный прогон. Coverage Line ≥85 / Branch ≥75.
- **Docs (предлагаемая строка):** «PostgreSQL composite types created after Npgsql has loaded type metadata may require a metadata reload on the connections used by the context; creating a fresh context alone does not guarantee that those types become available.» (RU-перевод в зеркалах; без ссылок на specs).
- **Perf:** не нужен — продуктовый путь исполнения не меняется, прогрев в тест-фикстуре. **Recon:** D-PROBE обязателен.
- **rv2 evidence contract:** E01–E09 сохранены (ID/обязательства/приоритеты), добавлены **E10** (warm-up probe), **E11** (warm/clean matrix + clean genuine-composite), **E12** (clean-limitation). CHECK re-gather: ≤2 адресных запроса; исчерпание → unmet, без новой DO-итерации.

## Ревизия r4 (rv3) — r=4, n=1, contract rv3

- **Повод:** CHECK r3 вернул loop-back; замечания W1–W5.
- **R01/R02 переформулированы под R05′:** тёплый каталог → обычная ошибка no-match (`None of the result-set columns*`, без `named composite`); чистый каталог / тип, отсутствующий в загруженном каталоге, → документированная диагностика named-composite (продукт при этом не меняется).
- **W1** принят решением (accepted-by-decision), правка не требуется.
- **Фиксы W2/W3/W4/W5:** W2 — точная проверка наблюдаемого cold-состояния по маркерам (`named composite` присутствует, `None of the result-set columns` отсутствует) без пиннинга класса/полного текста исключения; W3 — выделенная непараллельная коллекция `PostgresRawRow`; W4 — setup внутри `try` + `finally` с восстановлением hstore и прогревом каталога; W5 — уточнение ограничения на шести doc-страницах EN/RU.
- **Supersession:** rv3 вытесняет rv2; идентификаторы E01–E12 сохранены.
- **CHECK r3:** провал засчитан как 1/3 (n=1, контракт rv3).
- **Unit mode:** sequential, одно дерево; продуктовый код (`src/**`) не меняется.

## ACT — accept-with-record (эскалация)

- **CHECK-вердикт:** 3× FAIL только по полноте упаковки evidence; доказанных продуктовых дефектов нет. Эскалация разрешила **вариант (1): ACT authorized**, orchestrator доводит evidence-агрегат; DO/replan не запускаются, счётчик попыток не меняется.
- **Продукт `src/**` не менялся:** `git diff --name-only -- src` пуст; HEAD `f6bd8fa0`.

### Evidence-агрегат

| Проверка | Команда | Exit | Результат | Артефакт |
|---|---|---|---|---|
| build | `dotnet build nextorm.slnx -c Debug` | 0 | 0W/0E | `/tmp/opencode/202-r4-verify-build.log:28-30` |
| full suite | `DOCKER_HOST=… dotnet test nextorm.slnx -c Debug --no-build` | 0 | total 8933, failed 0, skipped 198 (capability-only, все 6 провайдеров) | `/tmp/opencode/202-r4-verify-fulltest.log:631-634` |
| class | `… -class nextorm.integration.tests.PostgresRawRowTests` (fresh container) | 0 | Total 39 / Failed 0 / Skipped 0 | `/tmp/opencode/202-r4.log` |
| unit | `dotnet test tests/nextorm.core.tests --filter RawRowMaterializerTests` | 0 | 45 / 0 failed | `/tmp/opencode/202-r4.log` |
| coverage | `dotnet-coverage collect …` + `reportgenerator` | 0 / 0 | Line 87% / Branch 79.2% | `tests/coverage/report/Summary.txt:7,:12` |
| warm-up probe (E10) | D-PROBE | 0 | `ReloadTypes()` прогревает implicit data source | `/tmp/opencode/202-warm-probe.log` |

### Матрица R01–R07 (evidence: file:line)

- **R01** warm hstore → ordinary no-match: `tests/nextorm.integration.tests/PostgresRawRowTests.cs:515`, guard `:524`, assert `:533-535`.
- **R02** warm ltree: `PostgresRawRowTests.cs:539`, guard `:548`, assert `:557-559`.
- **R03** незарегистрированный composite → named-composite guard: `:200, :403, :431, :447, :487, :667`.
- **R04** зарегистрированный composite материализуется: `:172, :229, :259, :314, :352, :474`.
- **R05′** metadata-only/no-round-trip/не per-row (source-аудит): `src/nextorm.core/DataContext/RawMapperFactory.cs:272,290,339,386`; `ProcedureResult.cs:136/139,170/172,362/365,375/381`; `RowMapperFactory.cs:296`; cold hstore `:563` (assert `:603-606`), cold ltree `:617` (`:651-654`); helpers `:710/:721/:748/:763`; docs EN `docs/guide/12-raw-sql.md:404`, `docs/guide/provider-specific/postgresql.md:348`, `docs/advanced/limitations.md:47` + RU `:411/:352/:47`.
- **R06** core без Npgsql: Npgsql только `src/nextorm.postgres/nextorm.postgres.csproj:14`; CPM `Directory.Packages.props:30`.
- **R07** build/suite/coverage зелёные (таблица выше).
- Docs-проверка: CRLF во всех шести страницах; ссылок на `docs/specs/**` не добавлено; EN/RU паритет.

### Открытые обязательства и follow-up

- **E09 — post-cycle release gate:** владелец пушит ветку `1.0.9-rc1` (агенты не пушят); после push job `build` в `.github/workflows/workflows` должен быть зелёным, включая оба cold-теста. Внутри цикла E09 не закрывается.
- **Follow-up #203** (milestone 1.0.9-rc1): продуктовая авторитетная классификация на чистом каталоге (вернуть R05) + унификация single/multi путей — https://github.com/AlexeyShirshov/nextorm/issues/203.
- **Результат для владельца:** uncommitted diff (тесты + 6 docs EN/RU + этот status-файл); коммит/пуш не выполнялись.

## Progress log

- PLAN: gather (scout) выполнен; решение planner записано; план зафиксирован, ожидает `go`.
- DO started: go получен, старт реализации (D1 проба метаданных).
- D1 проба (recon) — pass-предикат НЕ выполнен. Warm-каталог: Npgsql различает (PostgresCompositeType vs PostgresBaseType; hstore→GetFieldType Dictionary<string,string>, ltree→String). Clean-каталог: hstore/ltree и настоящий composite неразличимы — все GetDataTypeName="-.-", GetFieldType→InvalidCastException, GetPostgresType→UnknownBackendType (OID 0); отличаются только серверные OID (16708/16836/17029). In-memory авторитетного дискриминатора для clean-состояния НЕТ. → DO → PLAN (ревизия r2).
- PLAN r2: D1 failed; вариант C выбран, R05 сохранён; ожидается escalate по способу lookup (P:r2).
- PLAN r3 sealed (rv2): вариант D/A, продуктовый код не меняется; старт D-PROBE.
- D-PROBE: доказан механизм A1 — throwaway `new NpgsqlConnection(cs).Open(); ReloadTypes(); Dispose()` прогревает общий implicit data source (context-соединения резолвят hstore → public.hstore/PostgresBaseType). A2 (отдельный NpgsqlDataSource) НЕ работает; init-script не нужен.
- D-VERIFY (DO→CHECK boundary sweep): build `dotnet build nextorm.slnx -c Debug` exit 0, 0W/0E (`/tmp/opencode/202-verify-build.log`). Full solution `DOCKER_HOST=... dotnet test nextorm.slnx -c Debug --no-build` exit 0: total 8932, failed 0, skipped 198 (все скипы capability-based — «не поддерживается провайдером», не «провайдер недоступен»); провайдеры исполнены реально: PostgreSQL (после purge стартовал свежий контейнер f969dc828379), SQL Server, MySQL, MariaDB, ClickHouse, SQLite (`/tmp/opencode/202-verify-fulltest.log`). Coverage CI-рецепт: collect exit 0, report exit 0; Line 87% (47289/54302), Branch 79.2% (25559/32239) — ≥85/≥75 (`tests/coverage/report/Summary.txt`, `/tmp/opencode/202-verify-coverage.log`). Все 3 лога и пороги зафиксированы; продуктовый код не менялся; commit/push не выполнялись.
- PLAN r4 (rv3): R01/R02 согласованы с R05′; фиксы W2/W3/W4/W5.
- D-VERIFY (r4): build `dotnet build nextorm.slnx -c Debug` exit 0, 0W/0E (`/tmp/opencode/202-r4-verify-build.log`). Full solution `DOCKER_HOST=... dotnet test nextorm.slnx -c Debug --no-build` exit 0: total 8933, failed 0, skipped 198 — все скипы capability-based, провайдер-unavailable скипов нет; провайдеры исполнены реально: PostgreSQL (reuse-контейнер b08b42c7870c purge перед прогоном), SQL Server, MySQL, MariaDB, ClickHouse, SQLite (`/tmp/opencode/202-r4-verify-fulltest.log`). Coverage CI-рецепт: collect exit 0, report exit 0; Line 87% (47289/54302), Branch 79.2% (25559/32239) — ≥85/≥75 (`tests/coverage/report/Summary.txt`, `/tmp/opencode/202-r4-verify-coverage.log`). Продуктовый код `src/**` не менялся; commit/push не выполнялись.
- ACT: accept-with-record (эскалация, вариант 1); evidence-агрегат записан; follow-up #203; E09 — post-cycle.
---
