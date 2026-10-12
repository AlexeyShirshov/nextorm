# T217 (#217) — D217-datetimeoffset, cycle N=1

## Identity and state

- task_id: T217; collection_id: `1.0.9-todo`; issue: #217
- title: DateTimeOffset — SQL-трансляция `Now`/`UtcNow` и частей даты
- selected_variant: pdca-dotnet
- base: branch `1.0.9` @ `58025147`; milestone `1.0.9`
- cycle_id: N=1; plan_revision: **r=1**; attempt: **n=1/3**; evidence-contract revision: **rv=1** (первоначальная ревизия, без supersession)
- plan_state: **ready** (готовность плана, не завершённость реализации/evidence)
- mode: autonomous collection own-PLAN; single group → текущий worktree/ветка `1.0.9`, без веток/worktree/merge/push
- permitted auto-commit: только этот статус-файл, префикс `#217`; push никогда
- phase: **PLAN complete — collection handoff** (DO не стартовал и не авторизуется этим файлом)
- status_file: `docs/specs/status/D217-datetimeoffset-1.md`

## Task

Серверная трансляция поддерживаемых операций `DateTimeOffset` в SQL на всех штатных dialects без захвата клиентского времени и без скрытого клиентского вычисления; там, где СУБД не сохраняет исходный offset, ограничения объявляются явно.

## Cycle goal

SQL-запросы с поддерживаемыми операциями `DateTimeOffset` выполняют эти операции **на сервере**, без клиентских часов/локальной зоны и без скрытого клиентского вычисления; неподдерживаемые члены падают fail-closed; ограничения представления времени документированы и проверены.

## Current state

- phase: PLAN complete — collection handoff.
- `n=1/3`; активный `P:` = этот PLAN r=1; активные `D:` = `D217-0` … `D217-7` (все `pending`).
- DO ещё не начат; исходные `D217-*` остаются активными до CHECK, отчёт исполнителя не переводит их в `done`.
- Препятствий/blocker-отчётов нет; первичный PLAN, содержательных ревизий и supersession нет.

## Durable state

- Current cycle N: 1
- Plan revision r: 1
- Attempt n: 1/3
- Evidence contract rv: 1
- plan_state: ready
- defect_key registry: (пусто)
- Defect history: none yet — дефектов не наблюдалось; ни одна попытка/ревизия не сбрасывалась.

## Acceptance criteria

| ID | Критерий и проверка | Негативный случай |
|---|---|---|
| R217-01 | `Now`/`UtcNow` имеют серверный SQL emitter во всех 6 dialects; SQL и материализованный результат соответствуют provider-таблице. | Клиентский параметр вместо серверного выражения; SQL Server возвращает не `datetimeoffset`; UTC-результат с ненулевым offset. |
| R217-02 | `Year/Month/Day/Hour/Minute/Second/DayOfYear` транслируются для `DateTimeOffset`, включая nullable-источник и позиции Where/Select/OrderBy. | Член остаётся параметром, теряет SQL-null либо извлекает часть в неверной TZ. |
| R217-03 | `AddYears/AddMonths/AddDays/AddHours/AddMinutes/AddSeconds/AddMilliseconds` транслируются через date-add инфраструктуру; значимые дробные интервалы не исчезают; точность ограничена документированной точностью СУБД. | `.AddDays(0.5)` → нулевой сдвиг; отрицательный интервал меняет знак; null → ненулевая дата. |
| R217-04 | Неподдерживаемые члены явно бросают `NotSupportedException` при построении SQL, до исполнения и constant folding. | `.Offset` вычисляется на клиенте или превращается в параметр без ошибки. |
| R217-05 | Поведение/SQL существующего `DateTime`, scalar mapping, параметров и TVP не меняются. | Старый `MakeNow` начинает выдавать новый DTO SQL; меняется глобальное состояние команды/кеша. |
| R217-06 | SQL-gen покрывает SQLite/PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse; реальные интеграции проходят для PG/SQL Server/MySQL/ClickHouse. | Нулевое число выбранных тестов или skipped обязательного provider как pass. |
| R217-07 | EN/RU документация описывает серверное время, timezone/precision ограничения, поддержанные/запрещённые операции; новый public API имеет XML-doc. | Остаётся «DateTimeOffset members не переводятся» либо обещание сохранения offset в MySQL. |
| R217-08 | Evidence полон и проверяем; сборка без warnings; coverage/mutation evidence зарегистрированы по контракту. | Только exit 0 без сценариев/артефактов; неприменимость подменяется отсутствующим отчётом. |

## Design decisions / minimal solution

**Суть:** серверные выражения и прозрачные ограничения представления времени.

**Ограничения:** не менять `DateTime`, mapping и plan cache; не вводить silent fallback; сохранять SQL-null; проверить все dialects и обязательные реальные providers.

**Минимум:** расширить существующие visitor/dialect hooks — без нового SQL AST-класса, без универсальной temporal abstraction, без изменений `SqlFunctions`.

| Подход | Итог |
|---|---|
| (A) Только расширить `typeof(DateTime)` и переиспользовать старый `MakeNow` | Отклонено — неверный SQL Server result type, TZ-ошибки, скрытая потеря offset. |
| (B) DTO-aware hooks поверх существующей инфраструктуры | **Выбрано.** |
| (C) Новый temporal AST + overloads `SqlFunctions` | Не делать — избыточно, API до T53 freeze. |

Consumers/API: `MemberTranslator.cs:20-85`; `ScalarFunctionTranslator.cs:56`; `DateTimeFunctionTranslator.cs:17-27`; guard перед клиентским вычислением `MemberTranslator.cs:312-343`.

**Планируемые public additions:** `ISqlDialect.SupportsDateTimeOffsetTranslation`; `ISqlDialect.MakeDateTimeOffsetNow` (форма как `MakeNow`); DTO-aware overloads `MakeDatePart`/`MakeDateAdd` (+ явный `bool isDateTimeOffset`); **старые overloads не меняются**. Base: capability `false`, DTO-операции явно unsupported. 6 штатных dialects объявляют capability `true` после реализации. Имена фиксированы; точные существующие сигнатуры — Roslyn в D0. Не добавлять: `SqlFunctions` overloads, новые SQL function/operator classes, свободные `now/utcnow`, новые зависимости.

## Provider contract

| Provider | DTO `Now` | DTO `UtcNow` | Parts/Add |
|---|---|---|---|
| SQL Server | `sysdatetimeoffset()` | `todatetimeoffset(sysutcdatetime(), '+00:00')` | Native `datetimeoffset`: parts по offset, `dateadd` сохраняет offset. |
| PostgreSQL | `now()`; канонический DTO-result UTC | `now()`; UTC | Для DTO parts — UTC wall time через `AT TIME ZONE 'UTC'`; Add — UTC wall time → `timestamptz`, без зависимости от session TZ/DST. |
| MySQL | `utc_timestamp(6)` | `utc_timestamp(6)` | Существующий `datetime`; части сохранённого wall time, native date-add; исходный offset колонок не восстанавливается. |
| MariaDB | `utc_timestamp(6)` | `utc_timestamp(6)` | Наследует MySQL contract, отдельные SQL-gen/dialect тесты. |
| ClickHouse | `now64(6, 'UTC')` | `now64(6, 'UTC')` | UTC-aware `DateTime64`; TZ нормализуется явно, затем `toXxx`/`addXxx`. |
| SQLite | UTC `strftime` с дробной частью и явным UTC | та же | `strftime`/date modifiers; offset-bearing текст — как instant, канонизация в UTC; точность — миллисекунды. |

Общее: `DateTimeOffset.Now` = серверный источник времени в доступном provider-представлении, не часы/локальная зона клиента; полное сохранение offset только у SQL Server; PG хранит instant; MySQL/MariaDB/ClickHouse не сохраняют offset как часть значения. Точное SQLite выражение/материализация подтверждаются D0; если mapper не читает выбранное представление без изменения mapping — инвалидация решения → PLAN.

Add: поддержать семь `Add*` (literal/captured/SQL-expression); `AddYears`/`AddMonths` — native календарная; остальные — существующий `MakeDateAdd` + provider-local масштабирование; проверять отрицательные/дробные; дробь не должна терять значимую единицу; `AddTicks` и `Add(TimeSpan)` — `NotSupportedException`.

## Unsupported members

| Член `DateTimeOffset` | Поведение | Примечание |
|---|---|---|
| `.Offset` | `NotSupportedException` | Включая `constant`; guard на вложенные (напр. `.Offset.TotalMinutes`). |
| `.UtcDateTime` | `NotSupportedException` | В т.ч. `.UtcDateTime.Year`. |
| `.LocalDateTime` | `NotSupportedException` | — |
| `.ToOffset(...)` | `NotSupportedException` | Включая `constant` и dynamic аргумент. |
| `.Date` | `NotSupportedException` | В т.ч. `.Date.AddDays(...)`. |
| `.Ticks` | `NotSupportedException` | — |
| `.DayOfWeek` | `NotSupportedException` | — |
| `AddTicks` / `Add(TimeSpan)` | Deferred (guard сейчас) | Отдельная согласованная задача `1.0.9` до release. |

Bare DTO constants/params остаются поддержанными существующим mapping; in-memory не меняется.

## What the statement did not say

| Пробел | Закрытие |
|---|---|
| Сохранять исходный offset на каждом provider? | Невозможно для существующих типов; принят provider contract; ограничения — часть acceptance/docs. |
| Влияние PG session timezone на DTO parts/Add? | UTC contract фиксирован; D0+интеграции проверяют две session TZ, включая DST. |
| Совместимость chosen now expressions с readers? | Не доказано; D0 — обязательный spike. |
| Дробные Add без потери? | Проверяемые кейсы + provider-local адаптация в D2. |
| Точные dialect сигнатуры/gates? | Roslyn D0. |
| Новые тесты существуют? | Никакие; источники/имена **планируемые**. |
| Контейнеры/coverage tools? | Предусловие исполнения; восстановить по integration skill, skips не принимать. |
| Нужны `SqlFunctions` overloads? | Нет; если появится новый API — PLAN + parity contract. |

## DO units/streams

| Unit | state | Criteria covered | Dependencies |
|---|---|---|---|
| D217-0 — доказать provider representations | pending | R217-01…R217-06 (foundation) | первая в code-потоке; Roslyn (`ISqlDialect.cs:1057-1095`, `MemberTranslator.cs:312-343`, `RawMapperFactory.cs:698,746-747`), ограниченный spike. |
| D217-1 — DTO now и capabilities | pending | R217-01, R217-05 | после D0; `MemberTranslator.cs:20-32`; `ISqlDialect.cs:1066`; base + 6 dialects; default unsupported. |
| D217-2 — parts, Add, guards | pending | R217-02, R217-03, R217-04 | после D1; `MemberTranslator.cs:59-85,312-343`; `ScalarFunctionTranslator.cs:56`; `DateTimeFunctionTranslator.cs:17-27`; TZ/precision adapters, nullable, ранний rejection. |
| D217-3 — unit/SQL-gen matrix | pending | R217-01…R217-06 | тесты сразу; итоговые прогоны после D1/D2; guards, folding, capabilities, DateTime regression, 6 providers. |
| D217-4 — реальные integrations | pending | R217-06 | scaffolding сразу, execution после D2; `CommonTestSuite.Functions.cs:177,198,246`; `SqlServerSpecificTests.cs:1690`; planned T217 fixture/entity; PG/SS/MySQL/CH + SQLite. |
| D217-5 — docs + public API handoff | pending | R217-07 | docs параллельно; финальная сверка после D2/D4. |
| D217-6 — evidence/boundary preparation | pending | R217-08 | scripts, manifest, логи, coverage/mutation; CRLF/footprint audit; владелец evidence — Tests. |
| D217-7 — единый DO→CHECK sweep | pending | R217-01…R217-08 | последовательно после D1–D6; один неизменяемый candidate tree; после sweep без правок кода без нового evidence. |

Потоки: **code** (production `.cs` + XML-doc; D0→D1→D2), **tests** (tests + evidence artifacts; D3/D4 scaffolding параллельно, execution после D2), **docs** (EN/RU pages; D5 параллельно). Общий temporal contract фиксирован планом; API handoff code → tests/docs; сборки/контейнеры/mutation последовательно.

## Variant matrix

| Ряд | Статус | Detail |
|---|---|---|
| P6×Now/UtcNow×Select/Where/OrderBy×repeat | test | SQL-gen C01/C07/C14. |
| P4×Now/UtcNow×local/UTC | test | integration C02/C12 (MariaDB runtime deferred с env-триггером, SQL-gen не deferred). |
| P6×parts×value/null×placement | test | SQL-gen C03/C06/C07 (static Now null неприменим: nonnullable). |
| P6×Add×value/null×zero/± | test | дробные Add literal/captured/SQL; local/nonzero-offset/UTC × parts/Add. |
| forbidden × value/default/captured × placement | guard | guard C08/C09. |
| folding × static now/supported/bare const/unsupported | test | C01/C08/C09. |
| DateTime vs DTO × now/parts/Add × P6 | test | C11. |
| capability true/false × supported/unsupported | test | C10. |
| value/reference/default → C06/C08 (reference receiver — guard CLR typing) | test | `default(DateTimeOffset)` как value. |
| `.Offset.TotalMinutes`/`.UtcDateTime.Year`/`.Date.AddDays` | guard | C09. |
| AddTicks/Add(TimeSpan) | deferred+trigger | guard C09 + deferred (триггер — отдельная задача `1.0.9`). |
| Years/Months fractional | guard | CLR `int` — guard. |

P6 = 6 providers; P4 = PG/SQL Server/MySQL/ClickHouse.

## Test strategy

Naming token planned future sources = `T217` (не существующие symbols).

Сценарии:

- C01 — Now/UtcNow SQL shape / no client param / return type.
- C02 — серверный instant в bounds + materialized type/offset.
- C03 — семь parts (leap / конец года / граница UTC-local).
- C04 — семь Add (0/±, month/year boundary).
- C05 — дробные дни/секунды/мс, SQL-expression argument, precision tolerance.
- C06 — nullable / SQL-null / HasValue / propagation.
- C07 — Where/Select/OrderBy.
- C08 — literal/captured/nested unsupported, guard до folding.
- C09 — все запрещённые (incl. ToOffset constant/dynamic), `.Offset.TotalMinutes`/`.UtcDateTime.Year`/`.Date.AddDays`.
- C10 — capability=false стороннего dialect.
- C11 — DateTime regression / old overloads.
- C12 — PG session TZ/DST, SQL Server ненулевой offset, UTC-only.
- C13 — существующий mapping без изменений.
- C14 — isolation повторных запросов (без утечки command/cache state).

Affected subsets (per-unit, exact filters):

- D0/D1 core: `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~T217"` + каждый provider `--filter "FullyQualifiedName~T217|FullyQualifiedName~DialectTests"`.
- D2 core+6: `--filter "FullyQualifiedName~T217|FullyQualifiedName~SqlGenerationTests"`.
- D3 core+6: `--filter "FullyQualifiedName~T217"`.
- D4: `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~T217"`.
- D5: `dotnet build -c Debug` + `dotnet docfx docs/docfx.json`.

Boundary sweep (один, на финальном candidate tree): `dotnet build -c Debug`; затем по одному `dotnet test tests/nextorm.{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests -c Debug`; затем `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug`. Оформить planned `boundary.sh` (`set -euo pipefail`, сохранение stdout/stderr/exit codes/provider outcomes). Coverage collection оборачивает sweep, не второй полный sweep. **PG/SS/MySQL/CH skipped ≠ pass.** MariaDB не подменять MySQL runtime.

Coverage/mutation: `coverage.settings.xml` scope — только core/sqlite/postgres/sqlserver (для mysql/mariadb/clickhouse coverage из этого отчёта не заявлять); line/branch totals + пороги 85/75; targeted mutation (убрать DTO dispatch, вернуть client capture, изменить UTC branch, убрать forbidden guard, отключить nullable propagation, вернуть старый now emitter) — каждый применимый mutant убит focused test; неприменимость — с branch-основанием; общий mutation tool не добавлять.

## Docs plan

Изменить: `docs/scalar-functions/03-date-and-time.md:55-70`; `docs/ru/scalar-functions/03-date-and-time.md:56,69-71`; `docs/advanced/limitations.md:70-72`; условно `docs/ru/advanced/limitations.md`. Добавить provider table, поддержанные parts/Add, precision/timezone ограничения, явные unsupported exceptions, заметку «server time вместо client time — наблюдаемое изменение». Новые public dialect members — XML-doc (CS1591 не подавлять). DocFX проверяется. Generated API pages не редактировать, не ссылаться из public docs на `docs/specs`.

## Perf measurement

**Отдельный benchmark не нужен** (при сохранении footprint): меняется построение SQL (`MemberTranslator.cs:20-85`, `ScalarFunctionTranslator.cs:56`, `DateTimeFunctionTranslator.cs:17-27`, dialect emitters `SqlDialectBase.cs:622-663`) — translation/prepare path, не клиентский per-row materializer; `SelectExpression.cs:202` и `RawMapperFactory.cs:698,746-747` не меняются; `QueryCache`/`QueryCommand.Cache`/planner cache flags не затрагиваются. D0 подтверждает call path; E217-13 проверяет diff и repeated-context C14. Если понадобится reader/materializer/cache — аргумент теряет силу → возврат PLAN с benchmark/baseline.

## Reconnaissance

**Нужен ограниченный spike D0** (mapper result types / session TZ / fractional SQL state-dependent).

Проверить: (1) CLR return type/offset/precision chosen now expressions; (2) PG при UTC и не-UTC session TZ: parts/Add вокруг DST; (3) SQL Server native ненулевой offset и UTC zero-offset; (4) MySQL/ClickHouse UTC materialization без изменения mapper; (5) SQLite `strftime` форму, offset-bearing input, миллисекундный Add; (6) дробные Add `0.5` дня / `0.5` сек / `1.5` мс; (7) Roslyn call paths и точные hook signatures.

Success: types/offsets/parts/intervals соответствуют таблице и DB precision; logs содержат SQL/session settings/materialized values; mapping без изменений. Failure: конкретный result type/error/TZ divergence/потерянный дробный интервал → отчёт D0 → PLAN; критерии не ослаблять.

Бюджет D0: один прогон каждого предусмотренного provider probe + один диагностический повтор после исправления окружения.

## Unit mode

Один текущий worktree; **три параллельных DO-потока**: code (production `.cs` + XML-doc) / tests (tests + evidence artifacts) / docs (EN/RU pages). Worktree не нужен (пересечения файлов нет; collection запрещает ветки/worktree/merge). Push — никогда.

## Deferred + trigger

| Объём | Триггер |
|---|---|
| Трансляция запрещённых членов, `AddTicks`, `Add(TimeSpan)` | Отдельная согласованная задача `1.0.9` до release; до этого guards обязательны. |
| DTO overloads `SqlFunctions` | Подтверждённый consumer/новый SQL class; до завершения T53 freeze. |
| Смена хранения offset в MySQL/MariaDB/ClickHouse/SQLite | Подтверждённое требование сохранения offset; отдельный mapping/migration contract в `1.0.9`. |
| Изменение in-memory context | Отдельная постановка в `1.0.9`. |

Все deferred-объёмы остаются в milestone `1.0.9`.

## Versioned evidence contract — rv=1

Frozen manifest: `artifacts/pdca/D217-datetimeoffset/rv1/manifest.json`. Источники/скрипты/новые tests — **планируемые**. Для каждой строки: requirement_id, row_id, rv, scenario IDs, evidence kinds/sources, точный invocation, exit/result/log, artifacts, owner, applicability predicate, observed applicability. Дополнительно: candidate tree fingerprint, tool versions, hashes, test discovery ledger. Отсутствующий artifact ≠ неприменимость.

| requirement_id | row_id | rv | scenario(s) | evidence kind/source | exact invocation | exit/result/log | artifacts | owner | applicability predicate |
|---|---|---|---|---|---|---|---|---|---|
| R217-01…R217-06 foundation | E217-01 | 1 | D0 probes | Roslyn/probes (D0) | Roslyn queries + ограниченный provider spike D0 | зафиксированные types/offsets/parts/signatures; logs со SQL/session/materialized | `artifacts/pdca/D217-datetimeoffset/rv1/d0/**` | Code | всегда |
| R217-01, R217-05 | E217-02 | 1 | C01,C03,C04,C06,C11 | SQL-gen SQLite | `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~T217|FullyQualifiedName~SqlGenerationTests|FullyQualifiedName~DialectTests"` | exit 0 + ≥1 selected test | `.../rv1/sqlgen/sqlite.log` | Tests | всегда |
| R217-01, R217-02, R217-03 | E217-03 | 1 | C01,C03,C04,C12 | SQL-gen PostgreSQL | `dotnet test tests/nextorm.postgres.tests -c Debug --filter "FullyQualifiedName~T217|FullyQualifiedName~SqlGenerationTests|FullyQualifiedName~DialectTests"` | exit 0 + ≥1 selected test | `.../rv1/sqlgen/postgres.log` | Tests | всегда |
| R217-01, R217-02, R217-03 | E217-04 | 1 | C01,C03,C04,C12 | SQL-gen SQL Server | `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~T217|FullyQualifiedName~SqlGenerationTests|FullyQualifiedName~DialectTests"` | exit 0 + ≥1 selected test | `.../rv1/sqlgen/sqlserver.log` | Tests | всегда |
| R217-01…R217-06 | E217-05 | 1 | C01…C12 | SQL-gen MySQL | `dotnet test tests/nextorm.mysql.tests -c Debug --filter "FullyQualifiedName~T217|FullyQualifiedName~SqlGenerationTests|FullyQualifiedName~DialectTests"` | exit 0 + ≥1 selected test | `.../rv1/sqlgen/mysql.log` | Tests | всегда |
| R217-01…R217-06 | E217-06 | 1 | C01…C12 | SQL-gen MariaDB (отдельный результат, не копия MySQL) | `dotnet test tests/nextorm.mariadb.tests -c Debug --filter "FullyQualifiedName~T217|FullyQualifiedName~SqlGenerationTests|FullyQualifiedName~DialectTests"` | exit 0 + ≥1 selected test | `.../rv1/sqlgen/mariadb.log` | Tests | всегда |
| R217-01, R217-02, R217-03 | E217-07 | 1 | C01,C03,C04,C12 | SQL-gen ClickHouse | `dotnet test tests/nextorm.clickhouse.tests -c Debug --filter "FullyQualifiedName~T217|FullyQualifiedName~SqlGenerationTests|FullyQualifiedName~DialectTests"` | exit 0 + ≥1 selected test | `.../rv1/sqlgen/clickhouse.log` | Tests | всегда |
| R217-04, R217-01, R217-05 | E217-08 | 1 | C06,C08,C09,C10,C11,C13,C14 | core guards/folding/null/capability | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~T217"` | exit 0 + ≥1 selected test | `.../rv1/core/t217.log` | Tests | всегда |
| R217-06 | E217-09 | 1 | C02,C12 | P4 runtime integration (каждый без skip) | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~T217"` | exit 0, каждый из PG/SS/MySQL/CH executed (Skipped 0) | `.../rv1/integration/{postgres,sqlserver,mysql,clickhouse,sqlite}.log` | Tests | контейнеры доступны; недоступность = blocked, не pass |
| R217-07, R217-05 | E217-10 | 1 | — | build/XML-doc/DocFX/EN-RU/API handoff | `dotnet build -c Debug` + `dotnet docfx docs/docfx.json` + EN/RU consistency verifier | exit 0, 0 warnings/errors; EN/RU паритет | `.../rv1/docs/{build,docfx,en-ru-consistency}.log`; API diff | Docs | всегда |
| R217-08 | E217-11 | 1 | full boundary + coverage | coverage collection | `dotnet-coverage collect "bash artifacts/pdca/D217-datetimeoffset/rv1/boundary.sh" --settings coverage.settings.xml --output .../coverage.cobertura.xml --output-format cobertura` + `reportgenerator` | exit 0; line/branch totals ≥85/75 (core/sqlite/pg/ss scope) | `.../rv1/coverage/{coverage.cobertura.xml,summary.txt}`; `.../rv1/boundary/**` | Tests | всегда |
| R217-08 | E217-12 | 1 | targeted mutation | planned `mutation.sh` | `bash artifacts/pdca/D217-datetimeoffset/rv1/mutation.sh` | каждый применимый P1 mutant killed; неприменимость — branch-основание | `.../rv1/mutation/**` | Tests | применимость по установленным mutants |
| R217-08 | E217-13 | 1 | C13,C14 | completeness/scope/CRLF/cache invariant | planned `verify_evidence.py` / `verify_scope.py` | exit 0; footprint/diff/CRLF/cache invariant подтверждены | `.../rv1/verify/**` | CHECK/Tests | всегда |

**CHECK re-gather budget:** owner CHECK; ≤2 адресных re-gather суммарно на CHECK-попытки; exhausted без evidence → PLAN (scout → escalate trigger 5); отсутствие отчёта не меняет rv/критерии и не запускает новую DO; содержательная ревизия явно supersedes rv=1, сохраняет E217 IDs/обязательства.

Команды покрытия выбора провайдера: `E217-02…07` используют единый фильтр `~T217|~SqlGenerationTests|~DialectTests`; `E217-08` — `~T217`; `E217-09` — integration `~T217`.

## Write footprint

Точный footprint (production, `src/nextorm.core/`): `ISqlDialect.cs`, `Dialect/SqlDialectBase.cs`, `Visitors/MemberTranslator.cs`, `Visitors/ScalarFunctionTranslator.cs`, `Visitors/DateTimeFunctionTranslator.cs`.

Providers: `src/nextorm.sqlite/SqliteDialect.cs`, `src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.mysql/MySqlDialect.cs`, `src/nextorm.clickhouse/ClickHouseDialect.cs`.

Условно: `src/nextorm.mariadb/MariaDbDialect.cs` (если наследование недостаточно), `src/nextorm.core/Visitors/DateConversionSqlTranslator.cs` (переиспользование ClickHouse TZ).

Tests (планируемые новые): `tests/nextorm.core.tests/T217DateTimeOffsetTranslationTests.cs`; `tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests/T217DateTimeOffsetSqlGenerationTests.cs` (6 файлов); `tests/nextorm.integration.tests/T217DateTimeOffsetTests.cs`, `tests/nextorm.integration.tests/T217DateTimeOffsetEntities.cs`; условно `tests/nextorm.integration.tests/CommonTestSuite.Functions.cs`.

Docs/status/artifacts: `docs/scalar-functions/03-date-and-time.md`, `docs/ru/scalar-functions/03-date-and-time.md`, `docs/advanced/limitations.md`, условно `docs/ru/advanced/limitations.md`, `docs/specs/status/D217-datetimeoffset-1.md`, `artifacts/pdca/D217-datetimeoffset/rv1/**`.

**Не менять:** mapping/readers/TVP, `SqlFunctions*.cs`, shared `Entities.cs`, package/config/CI, generated `docs/api`/`docs/_site`, parent collection status (`docs/specs/status/collection-1.0.9-todo.md`), файлы T53. Необходимость запрещённой области → возврат PLAN.

## Sibling / T53 requirements

Sibling footprint не пересекается с этими файлами (по переданной сводке). T53 должен учесть новые `ISqlDialect` members, base implementations и overrides как public API-кандидаты freeze. До T53 handoff передать фактический API diff, XML-doc, compatibility note; `SqlFunctions` overloads отсутствуют. T217 не редактирует `Directory.Packages.props`/`.editorconfig`/`.github/workflows/dotnet.yml`/статус T53. Если sibling начнёт менять temporal visitors/dialects — параллельность на пересекающихся файлах прекращается до фиксации общего контракта.

## Risks / known issues

- DTO reader не принимает выбранный SQLite/MySQL/ClickHouse result shape.
- PG arithmetic незаметно использует session TZ/DST.
- Fractional Add теряет значимую часть либо переполняет unit.
- Constant folding обходит guard.
- Новый interface API ломает сторонние dialects (base fallback + compatibility note обязательны).
- Полный suite «зелёный» за счёт skipped контейнеров.
- Aggregate coverage скрывает новый непроверенный guard.

Уверенность: высокая в локализации; средняя в provider expressions до D0. Неуспех решающего probe → новый PLAN.

## Open questions

- Точное SQLite выражение/материализация и совместимость с существующим mapper — только D0.
- Доступность контейнеров/coverage-mutation инструментов — предусловие исполнения (восстанавливается по integration skill).
- Точные существующие сигнатуры hook'ов и dialect gates — Roslyn D0.

Открытые вопросы ограничены D0/environment; решение/критерии не оставлены TBD.

## Next plan + next todo

- `plan_state=ready`.
- **next allowed step = parent resumes the SAME cycle N=1 at DO** (applicability check), not a new PLAN.
- Предикат старта/возобновления DO: (1) план r=1 и контракт rv=1 сохранены, слоты сверены со skills; (2) работа в исходном дереве, чужие изменения не перезаписываются; (3) нет нового пересечения footprint с активным sibling либо согласован общий контракт; (4) зарегистрированы три потока, зависимости и владельцы evidence; (5) исходный `D217-*` активен до CHECK, отчёт исполнителя не переводит его в `done`; (6) зависимости задачи выполнены (D0 не блокирует подготовку тестов/доков, но блокирует финальную provider-реализацию). При невыполнении — `blocked` с наблюдаемой причиной, не «завершено».
- Классификация DO → PLAN: отсутствующий tool/socket/fixture — in-cycle предусловие (исходный D активен, `blocked`); установленная DB precision loss в рамках contract — допустимый риск при выполнении acceptance; невозможность материализации/сохранения интервала/TZ semantics — инвалидация решения, новая содержательная P; истинный внешний blocker — рекомендация `escalate`; недостаток доказательств — точечный scout, стойкая низкая уверенность — `escalate` триггер 5. Аддитивное предусловие не supersedes; замена scope — явный `superseded → replacement`.

## Changed files

| Path | Kind |
|---|---|
| `docs/specs/status/D217-datetimeoffset-1.md` | this status file (persisted) |
| `src/nextorm.core/ISqlDialect.cs` | planned footprint (pending) |
| `src/nextorm.core/Visitors/MemberTranslator.cs` | planned footprint (pending) |
| `src/nextorm.core/Visitors/ScalarFunctionTranslator.cs` | planned footprint (pending) |
| `src/nextorm.core/Dialect/SqlDialectBase.cs` | planned footprint (pending) |
| providers + tests + docs | planned footprint (pending) |

## Pointers

- `src/nextorm.core/ISqlDialect.cs:1057-1095` (`ISqlDialect` hooks), `:1066` (`MakeNow`)
- `src/nextorm.core/Visitors/MemberTranslator.cs:20-32` (now hooks), `:20-85` (DTO consumers), `:59-85`, `:312-343` (guard против клиентского вычисления)
- `src/nextorm.core/Visitors/ScalarFunctionTranslator.cs:56`
- `src/nextorm.core/Visitors/DateTimeFunctionTranslator.cs:17-27`
- `src/nextorm.core/Dialect/SqlDialectBase.cs:622-663` (date-add emitters)
- `src/nextorm.core/Expressions/SelectExpression.cs:202`
- `src/nextorm.core/DataContext/RawMapperFactory.cs:698,746-747`
- `tests/nextorm.integration.tests/CommonTestSuite.Functions.cs:177,198,246`; `.../SqlServerSpecificTests.cs:1690`
- `docs/scalar-functions/03-date-and-time.md:55-70`; `docs/ru/scalar-functions/03-date-and-time.md:56,69-71`; `docs/advanced/limitations.md:70-72`

## Progress log

2026-10-12T04:18:33Z | PLAN | r1 | n1/3 | PLAN ready — plan_state=ready (collection handoff; parent owns PLAN→DO boundary) | docs/specs/status/D217-datetimeoffset-1.md

2026-10-12T04:18:33Z | PLAN | r1 | n1/3 | rv=1 evidence contract + frozen manifest path artifacts/pdca/D217-datetimeoffset/rv1/manifest.json registered; D217-0…D217-7 active (pending); defect history empty; no supersession | docs/specs/status/D217-datetimeoffset-1.md
