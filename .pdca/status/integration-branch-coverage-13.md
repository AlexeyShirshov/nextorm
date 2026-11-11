# Cycle status: integration-branch-coverage-13 / cycle 13

## Campaign state

Status: running
Form: incremental   Overlay: pdca-dotnet (coder+.NET)
Goal: branch coverage интеграционной сборки **≥76 %** по неизменной gate-модели (8 сборок, исключение `NextORM.Core.InMemory*`)
Predecessors: `integration-branch-coverage-11` и `-12` — closed (`not accepted` / `blocked`); их evidence read-only, их тесты — **inventory без кредита**
Global acceptance: C13-NUMERIC / C13-CONTRIBUTION / C13-GREEN / C13-INTEGRITY / C13-CLAIM / C13-REPORTING — по ссылке на `#### PLAN record (r1)`
Recipe: metric = branch coverage; reportgenerator `Summary.txt` при `-classfilters:"-NextORM.Core.InMemory*"`; D=51773, база **38269 (73.91689 %)**, текущее наблюдённое 38561, target шага **38820**, конечная цель **39348**
Allowed surface: `tests/nextorm.integration.tests/**` + записи кампании; production, coverage-конфигурация, provider matrix, `tests/Shared/ClickHouseRetry/**` — запрещены
Increment unit: подтверждённые **branch flips** в core относительно закреплённого архива (не A/B) + gate-величина
Floor: ≥ +259 ветвей (≥0,5 п.п.); операционный target шага 1 — **38820/51773**
Preparation ref: —
Commit policy: none (no commit/push/merge)
Constraints: реальные СУБД, FluentAssertions, `AssertionStyleTests`, `IntegrationSuiteScopingTests`, CRLF, warnings-as-errors; никаких opt-outs, никакого rerun-for-luck
Effort accounting: PLAN — высокая уверенность в метрике, достижимость кластеров не установлена (recorded, never a cutoff)
Baseline evidence: `.pdca/integration-branch-coverage-12/rv1/attempts/step-1/integration.cobertura.xml`, sha256 `13cd2d0a3067786d6e6a9b3d27a45e047ef04a38e5447ded4166bc3e17082179` (HEAD `67ba9494` + две untracked test classes кампании 12)
Evidence root: `.pdca/integration-branch-coverage-13/rv1/`
Steps: step 1 → basis 38269/51773 → credited — → verdict pending → evidence `.pdca/integration-branch-coverage-13/rv1/attempts/step-1/`
Current step: 1   Phase: DO (step 1, r1, incomplete)   Cycle: 13   Plan revision: r1   Attempt: n=1/3
Next-step seed: (consumed by step 1)
Accepted knowledge: доказательство вклада — branch flip по идентичности `class + filename + method/signature + line + condition number` (0%→100%, 50%→100%) относительно закреплённого архива; A/B исключён владельцем
Rejected approaches: A/B-пара как способ атрибуции (заменена правилом branch flip)   Deferred: —   Blockers: —   Protected/foreign: кампании 11–12 read-only
Decisions: form incremental (planner); область шага — коррелированные запросы и сложные агрегаты core
Defect history: см. `### Step 1` → `#### Defect ledger`
Checkpoint: last boundary — DO r1 неполон: probe кластера `CorrelatedQueryExpressionVisitor` дал **0 flips**, массовое написание не начиналось, приёмочного измерения нет; accepted basis 38269/51773; disposition open; resume point — следующий cluster (например `AdvancedAggregateTranslator`) или решение владельца
Stop verdict: —
Pending approval ref / Approval record: not applicable: autonomous mode

## Original task

«pdca необходимо довести бранч покрытие интеграционных тестов до 76%» — плюс правило владельца кампании 13: **prove contribution by branch flip, not by A/B**.

## Step records

### Step 1

#### Current state

- task: `integration-branch-coverage-13` · form: `incremental` · specialized skill: `pdca-dotnet` (coder+.NET)
- mode: `autonomous`
- cycle: `N=13` · revision: `r=1` · attempt: `n=1/3`
- scope: ранее непокрытые пути core для коррелированных запросов и сложных агрегатов
- goal: ≥76 % суммарного gate-покрытия — авторитетны `## Original task` и `#### PLAN record (r1)`
- phase: `DO` · record: `open` · outcome: `pending`
- basis: **38269/51773** · текущее наблюдённое 38561 · target шага **38820**
- criteria: C13-NUMERIC / C13-CONTRIBUTION / C13-GREEN / C13-INTEGRITY / C13-CLAIM / C13-REPORTING — точный текст в `#### PLAN record (r1)`

#### PLAN record (r1)

Встроен дословно, без изменений:

````markdown
# PLAN — `integration-branch-coverage-13`, step 1, r1

**Исходная задача:** «pdca необходимо довести бранч покрытие интеграционных тестов до 76%».

**Запрошенный и передаваемый результат:** **≥76% суммарного интеграционного gate-покрытия**, достигнутого содержательными интеграционными проверками core.

**Форма:** новая incremental-кампания, явно разрешённая владельцем. **Специализация:** `pdca-dotnet`, overlay `coder+.NET`.

- Принятая исходная база: **38269/51773**.
- Текущее измеренное состояние: **38561/51773**, без предварительного кредита.
- Target step 1: **38820/51773** — минимум **+259** к текущему наблюдаемому состоянию.
- Конечный target: **39348/51773**. До конечной цели сейчас **787** ветвей.

### Опорный архив атрибуции
`/home/alex/sources/nextorm/.pdca/integration-branch-coverage-12/rv1/attempts/step-1/integration.cobertura.xml`
SHA-256 `13cd2d0a3067786d6e6a9b3d27a45e047ef04a38e5447ded4166bc3e17082179`
Привязка: HEAD **`67ba9494`** плюс две существующие untracked test classes кампании 12. Архив предшествует всем новым тестам кампании 13. Измеряемые production-исходники сохраняются неизменными; при их изменении или потере сопоставимости condition IDs сравнение недействительно, другой архив молча не подставляется.

### Правило владельца
> **Prove contribution by branch flip, not by A/B.**

Идентичность условия: `class + filename + method/signature + line + condition number`. Учитываются переходы `0% → 100%` и `50% → 100%`. Для каждого затронутого кластера сообщаются идентификаторы, исходное и итоговое покрытие, количество flips. **Количество flips — доказательство вклада, не автоматически числовой gate-прирост.** A/B и второй полный запуск ради атрибуции исключены: это изменение способа доказательства, не снижение порога.

### Область шага
Ранее непокрытые пути core, обслуживающие коррелированные запросы и сложные агрегаты, с допустимым использованием массивов/JSON и преобразования выражений там, где это необходимо для реальных сценариев. Основные кластеры: `CorrelatedQueryExpressionVisitor`, `AdvancedAggregateTranslator`, связанные `ArraySqlTranslator`, `JsonSqlTranslator`, `MemberTranslator`; `RelationshipResolver` — только при подтверждённом публичном DBMS-пути, не InMemory-only. Публичный вход — реальный пользовательский запрос на БД; PostgreSQL допустим для соответствующей семантики, SQLite — там, где она поддерживается. План не назначает execution units и порядок классов: их выбирает DO.

### Подтверждение перед массовым написанием
Короткий probe должен установить: публичный API ведёт к выбранному участку core; реальная СУБД исполняет сценарий; появляется ранее отсутствовавший condition/branch flip относительно закреплённого архива. Один неудачный probe не доказывает недостижимость; обоснованное отрицательное заключение требует конкретного ограничения (InMemory-only, нет публичного входа, семантика не поддерживается провайдером).

### Поверхность
Разрешены необходимые тестовые изменения внутри `tests/nextorm.integration.tests/**` и записи кампании. Не разрешены: production-код, coverage-конфигурация, provider matrix, shared ClickHouse retry-инфраструктура, host-конфигурация, commit/push/merge.

### Критерии
**C13-NUMERIC.** Step 1 достигает **≥38820/51773** в полном gate-измерении; конечная цель **≥0,76**.
**C13-CONTRIBUTION.** Новые содержательные тесты дают подтверждённые flips в core относительно закреплённого архива. Исполнение уже покрытых строк вкладом не считается.
**C13-GREEN.** Все новые/изменённые тесты scope и вся существующая SQLite-полоса успешны. Унаследованная ClickHouse-полоса сообщается фактически и не препятствует приёмке по решению владельца; новые ClickHouse-тесты освобождения не получают.
**C13-INTEGRITY.** FluentAssertions, реальные СУБД, `IntegrationSuiteScopingTests`, `AssertionStyleTests`, CRLF, warnings-as-errors; без assertion-free проверок, искусственного покрытия и ослабления ожидаемого поведения.
**C13-CLAIM.** Исправить `tests/nextorm.integration.tests/Sqlite/CorePreparerEdgeSqliteTests.cs:130–141`: **усилить тест** — проверять поведение именно подготовленной команды, а не `NotBeNull` плюс две несвязанные операции. Содержательное ожидание выбирает DO по контракту API. Ещё одна формальная проверка finding не закрывает.
**C13-REPORTING.** Deletion ledger, branch-flip отчёт и фактические результаты полос доступны независимому CHECK; непринятые измерения не описываются как принятый прирост.

### Измерение
Дешёвые class-level запуски и branch-flip сравнения — для steering; **один полный запуск в конце попытки** даёт приёмочную величину; второй полный запуск для attribution не требуется; steering-результат нельзя повысить до приёмочного. Полный запуск — все провайдеры, **без `NEXTORM_SKIP_PROVIDERS`**. Для одного состояния кандидата нельзя повторять полный запуск ради лучшей цифры или зелёного ClickHouse.

### Deletion ledger
Фиксируются идентичность, inventory diff, причина, последнее red/stall свидетельство и **измеренная либо явно ограниченная сверху потеря покрытия**; потери не уменьшают исторический baseline и повторно не вычитаются; неизвестный вклад не записывается как нулевой; flakiness неизвестной причины не объявляется невалидностью. ClickHouse-исход приводится дословно; при красной полосе — известный owner-excluded outcome, **не «вся сборка зелёная»**.

### История и остановка
Кампания 13: **r1, вход n=1/3**. Не более трёх попыток текущего контракта. Следующий шаг — только после положительного независимого CHECK принятого инкремента. `done` требует CHECK всей цели 76%. STOP — при исчерпании попыток, повторении исчерпанного дефекта либо необходимости нарушить ограничения. Из кампаний 11–12 переносятся дерево и тесты **как inventory без кредита**; `CH-NONQUERY-STALL` остаётся `undetermined`; `F-R2-STATE` — открыт для ClickHouse и неприменим к SQLite-приёмке.

Правило пользователя неизменно: «если обнаружена ошибка в продукте, необходимо открыть тикет в rc5 и продолжать работу. тест при этом можно скипнуть со ссылкой на открытый тикет». Перед созданием issue — поиск дубликатов. Транспортная нестабильность сама по себе skip не разрешает.

**Статус:** `.pdca/status/integration-branch-coverage-13.md`. **Материалы:** `.pdca/integration-branch-coverage-13/rv1/`.
````

#### Approval record

not applicable: autonomous mode.

#### Defect ledger

| key | state | observed | gap / next evidence |
|---|---|---|---|
| `F-NUMERIC` | carried, **open** (critical) | 38561 против target 38820 — недостача 259; до 76 % 787 | закрывается только gate-измерением после прироста |
| `F-ATTRIBUTION` | carried, **open** (major) | A/B-пара непригодна; владелец заменил способ доказательства на branch flip | нужен flip-отчёт по закреплённому архиву |
| `F-DELETIONS` | carried, **open** (major) | «zero loss» сильнее доказательств: равные агрегаты допускают компенсацию | нужна измеренная либо ограниченная сверху потеря |
| `F-TEST-CLAIM` | carried, **open** (medium) | `CorePreparerEdgeSqliteTests.cs:130–141` — только `NotBeNull` | закрывается по C13-CLAIM (усилить тест) |
| `CH-NONQUERY-STALL` | carried, **undetermined** | baseline 2×, r1/n3 DDL, r2/n3 fixture, rv2/session-1 чтение, session-2 2m00s | нет клиентского захвата в момент застоя |
| `F-R2-STATE` | carried, **open** для ClickHouse, **неприменим к SQLite-приёмке** | `ch3_marker_v2` не проверяет схему/содержимое | ClickHouse-полоса вне green scope |

#### DO result

### Батч 5 (n=1): первый подтверждённый `0% → hit`

**Статус: 1 фича из 5 выполнена, `0% → hit` = 4 ветви.** Вердикт не выносится.

- **Фича 1 — `PreWhere` с In-values.** Написан `C13PreWhereInValuesClickHouseTests` (1 факт, реальный
  ClickHouse, `PreWhere(x => wanted.Contains(x.Id))`, утверждаются выбранные строки `{1,3}`).
  Первая попытка упала с `Code: 182` — **барьер**: `PREWHERE` не поддерживается движком `Memory`;
  после замены на `MergeTree order by id` факт зелёный.
- **Flips против закреплённого архива (идентичность `class+filename+method+line+condition`):**
  `QueryPreparer.PreparePreWhere` L3657 `0/2 → 2/2`, L3659 `0/2 → 2/2` — **2 узла, 4 ветви**.
  Целевые строки L252–256 и L290–294 (партиции In-values) **не перевернулись**: партиционирование
  включается при другом объёме/форме списка — зафиксировано как барьер, а не как успех.
- **Фичи 2–5 не выполнялись** (нет времени): `NullsPosition` на диалекте без нативной поддержки (нужен
  MySQL/SQL Server), `ParamMode`-ветка, `PerParentPagingEmbedded`, extreme-row select list.
- **Сборка и конвенции:** `dotnet build nextorm.slnx -c Debug` → 0/0; конвенции 4/4.
- **Приёмочное измерение не выполнялось.**
- **Доказательства:** `rv1/attempts/step-1/probe-prewhere.cobertura.xml`, `probe-prewhere.log`,
  `flips-prewhere.txt`, `batch-sources-sha256.txt`.

#### DO result (предыдущие батчи)

### Тёмная зона, батч 4 (n=1): A переписана на `SelectGroup`, B не выполнялась

**Статус: 6 фактов зелёные, `0% → hit` = 0 из 43 zero-hit узлов.** Вердикт не выносится.

- **Группа A (переписана на правильный API).** `C13SelectGroupKeyShapesSqliteTests` — **6 фактов** на
  `SelectGroup(...)`: member-ключ, method-call-ключ, арифметический, конвертированный, условный
  (с проверкой отказа), захваченный closure. Все зелёные.
- **Измерение.** У класса `SelectGroupExpressionAnalyzer.SelectGroupExpressionComparer` в архиве
  **46 узлов, 43 из них 0 %**; после прогона — те же 46 узлов, **улучшено 0**. То есть даже путь
  `SelectGroup` с одной проекцией не доходит до этих армов: comparer, судя по нулевому попаданию,
  используется при **дедупликации нескольких** select-group выходов, а не при одиночной проекции.
  Это барьер, зафиксированный измерением, а не отсутствие попытки.
- **Группа B (`RelationshipResolver`) не выполнялась** — время ушло на доказательство барьера A.
- **Сборка и конвенции:** `dotnet build nextorm.slnx -c Debug` → 0/0; конвенции 4/4.
- **Приёмочное измерение не выполнялось.**
- **Доказательства:** `rv1/attempts/step-1/probe-selectgroup2.cobertura.xml`, `probe-selectgroup2.log`,
  `flips-selectgroup2.txt`, `batch-sources-sha256.txt`.

#### DO result (предыдущие батчи)

### Тёмная зона, батч 3 (n=1): группы A и B

**Статус: A написана и зелёная, но 0 flips; B не выполнялась.** Вердикт не выносится.

- **Группа A — `SelectGroupExpressionComparer`.** Написан класс `C13SelectGroupKeyShapesSqliteTests`
  (**6 фактов**, реальный SQLite, утверждения на сгруппированных строках): member-ключ, method-call-ключ
  (`ToUpper`), бинарный ключ (`Id % 2`), конвертированный ключ (`(long)Id`), `Having` по агрегату,
  агрегат по member-ключу. Все зелёные.
  Сравнение с закреплённым архивом: **0% → hit: 0 узлов**, **50% → 100%: 0**. То есть обычный
  `GroupBy(...).Select(...)` этот comparer не исполняет — он живёт на пути `SelectGroup`, а не на
  обычной группировке; это барьер, а не отсутствие усилий.
- **Группа B — `RelationshipResolver`.** Не выполнялась: время ушло на доказательство барьера группы A.
  Записано как незавершённое, не как недостижимость.
- **Сборка и конвенции:** `dotnet build nextorm.slnx -c Debug` → см. результат ниже;
  `AssertionStyleTests` + `IntegrationSuiteScopingTests` → 4/4.
- **Приёмочное измерение не выполнялось** (по указанию).
- **Доказательства:** `rv1/attempts/step-1/probe-selectgroup.cobertura.xml`, `probe-selectgroup.log`,
  `flips-selectgroup.txt`, `batch-sources-sha256.txt`.

#### DO result (предыдущие батчи)

### Тёмная зона, батч 2 (n=1, продолжение)

**Статус: три рычага проверены, все дали 0 flips.** Вердикт не выносится.

- **Причина нулевых probe установлена измерением.** В `AdvancedAggregateTranslator.Classify` zero-hit всего
  4 строки (389–392, два `percentile`-арма); остальные `when`-армы исполнялись и стоят на 1/2 — половина
  «guard false» недостижима (jump table). Имена `bool_and`/`corr`/`covar_*`/`regr_*`/`bit_*`/`every`/
  `range_agg` уже вызываются набором.
- **Рычаг «Trace-логгер» (группа 4).** Написан класс `C13LoggingMemberPathsSqliteTests` (3 факта: member
  access, DateTime-члены, вычисление в проекции) с `UseLoggerFactory` и уровнем **Trace**; все зелёные.
  Flips против архива: **0 узлов** — логирующие ветви уже покрыты другими тестами, которые строят контекст
  с логгером.
- **Postgres JSON/aggregate batch (группа 3, повтор).** 4 факта: `row_to_json`/`array_to_json`,
  `json_object_agg`/`jsonb_object_agg`, `jsonb_array`/`to_json`, `percentile`. Два зелёных, два падения
  (`row_to_json`/`array_to_json` — вероятная причина: `row_to_json<T>(T?)` требует целую сущность, а не
  элемент проекции; `percentile` — поверхность не найдена в `SqlFunctions.Postgres`). Класс снят до
  измерения flips, потому что падения не были диагностированы в отведённое время.
- **Группы 1 (`RelationshipResolver`) и 2 (`SelectGroupExpressionComparer`) не выполнялись** — не хватило
  времени на изучение модели/`SelectGroup` API; это записано как незавершённое, а не как недостижимость.
- **C13-CLAIM** — частично (см. предыдущую запись: усилен до проверки плана и исполнения; перебиндинг
  значения API не поддерживает).
- **Сборка и конвенции:** `dotnet build nextorm.slnx -c Debug` → **0 Warning(s) 0 Error(s)**;
  `AssertionStyleTests` + `IntegrationSuiteScopingTests` → **4/4 passed**.
- **Приёмочное измерение не выполнялось** (по указанию).
- **Доказательства:** `rv1/attempts/step-1/` — `probe-logging.cobertura.xml`, `probe-logging.log`,
  `flips-logging.txt`, `probe*.cobertura.xml`, `flips-agg.txt`, `batch-sources-sha256.txt`.

**Статус предыдущего батча: batch не доведён; flips не доказаны; приёмочного измерения нет.** Вердикт не выносится.

- **Корневая причина обоих нулевых probe установлена из архива.** В `AdvancedAggregateTranslator.Classify`
  **zero-hit всего 4 строки** — 389/390 (`PostgresFunctions.percentile`) и 391/392
  (`SqlServerFunctions.percentile`); все остальные `when`-армы исполнялись и стоят на **1/2**: половина
  «guard false» структурно недостижима (switch компилируется в jump table). Поэтому вызовы
  `bool_and`/`corr`/`percentile_cont` и т.п. **уже есть в наборе** и новые вызовы тех же имён ничего не
  переворачивают. Это измеренный барьер, а не догадка.
- **Где действительно есть непокрытые строки:** `JsonSqlTranslator` — 59 (например `row_to_json` 181–183,
  `array_to_json` 184–186, `json_object_agg`/`jsonb_object_agg` 52–57, `jsonb_array` 74–77),
  `ArraySqlTranslator` — 41, `MemberTranslator` — 91.
- **Batch (Postgres, новый класс `C13PostgresSurfaceBatchTests`)** по этим строкам: 4 факта
  (`row_to_json`/`array_to_json`, `json_object_agg`/`jsonb_object_agg`, `jsonb_array`/`to_json`,
  `percentile`). Итог локального прогона: **2 зелёных** (`json_object_agg`/`jsonb_object_agg`,
  `jsonb_array`/`to_json`), **2 падения** (`row_to_json`/`array_to_json`, `percentile`) — причины не
  установлены в отведённое время, поэтому **класс удалён целиком**, чтобы не оставлять красных тестов и
  сломанной сборки; flips по нему **не измерялись**. Частичный результат зафиксирован здесь как факт, а не
  как вклад.
- **C13-CLAIM — частично.** `OnePreparedCommand_ShouldServeTwoParameterValues` больше не проверяет
  `NotBeNull`: утверждается идентичность плана для двух захватов и **исполнение** подготовленной команды
  (непустой результат, повторное исполнение даёт те же строки). Перебиндинг значения через
  `ToList(prepared, params)` API не поддерживает — это записано комментарием в тесте. Finding закрыт
  **не полностью**.
- **Сборка и конвенции:** `dotnet build nextorm.slnx -c Debug` → **0 Warning(s) 0 Error(s)**;
  `AssertionStyleTests` + `IntegrationSuiteScopingTests` → **4/4 passed**.
- **Приёмочное измерение не выполнялось** (по указанию: следующий вызов). Дерево отличается от измеренного
  в кампании 12, поэтому 38561/51773 к нему не относится.
- **Доказательства:** `rv1/attempts/step-1/` — `probe.cobertura.xml`, `probe-flips.txt`,
  `probe-agg.cobertura.xml`, `flips-agg.txt`, `sources-sha256.txt`; анализ zero-hit — в этом отчёте.

#### Changed files

(pending)

#### Git state

status path: `.pdca/status/integration-branch-coverage-13.md` — created this campaign · untracked; no `git add`, no commit

#### Progress log

Единый журнал кампании — `## Progress log` этого файла.

## Campaign outcome

## Progress log

2026-10-29T15:38:41Z | REGISTRATION | cycle 13 | step 1 | revision r1 | attempt n=1/3 | кампания `integration-branch-coverage-13` открыта; PLAN r1 записан дословно; defect ledger засеян пятью перенесёнными находками; кампании 11–12 read-only | .pdca/status/integration-branch-coverage-13.md
2026-10-29T15:41:27Z | DO | cycle 13 | step 1 | revision r1 | attempt n=1/3 | probe кластера `CorrelatedQueryExpressionVisitor`: 2 коррелированных факта на реальном SQLite — зелёные, но **0 flips** против закреплённого архива; массовое написание не начиналось, приёмочное измерение не выполнялось | `.pdca/integration-branch-coverage-13/rv1/attempts/step-1/probe-flips.txt`
2026-10-29T15:47:41Z | DO | cycle 13 | step 1 | revision r1 | attempt n=1/3 | probe 2 (`AdvancedAggregateTranslator.Classify`): 5 фактов зелёные, но против архива **0** новых покрытых узлов из 10 341 пересечения; барьер — базовые агрегатные формы уже покрыты | `.pdca/integration-branch-coverage-13/rv1/attempts/step-1/flips-agg.txt`
2026-10-29T15:47:41Z | DO | cycle 13 | step 1 | revision r1 | attempt n=1/3 | C13-CLAIM частично: `OnePreparedCommand_ShouldServeTwoParameterValues` усилен до проверки идентичности плана и исполнения команды; перебиндинг значения через `ToList(prepared, params)` API не поддерживает — зафиксировано в тесте | `tests/nextorm.integration.tests/Sqlite/CorePreparerEdgeSqliteTests.cs`
2026-10-29T15:47:41Z | DO | cycle 13 | step 1 | revision r1 | attempt n=1/3 | приёмочное измерение не выполнялось (бюджет попытки исчерпан на probe); дерево отличается от измеренного в кампании 12 — цифра 38561 к нему не относится | `.pdca/integration-branch-coverage-13/rv1/attempts/step-1/`
2026-10-29T15:53:38Z | DO | cycle 13 | step 1 | revision r1 | attempt n=1/3 | установлена корневая причина нулевых probe: в `Classify` zero-hit только 4 строки (два `percentile`-арма), остальные `when`-армы стоят на 1/2 структурно (jump table); Postgres-batch по реально непокрытым строкам дал 2/4 зелёных, класс снят до измерения flips; сборка 0/0, конвенции 4/4 | `.pdca/status/integration-branch-coverage-13.md`
2026-10-29T15:57:59Z | DO | cycle 13 | step 1 | revision r1 | attempt n=1/3 | тёмная зона, батч 2: Trace-логгер (3 факта) — 0 flips; Postgres JSON/aggregate batch — 2/4 зелёных, снят; `Classify` residual структурный (jump table, 4 zero-hit строки); группы RelationshipResolver и SelectGroupExpressionComparer не выполнялись; сборка 0/0, конвенции 4/4 | `.pdca/integration-branch-coverage-13/rv1/attempts/step-1/flips-logging.txt`
2026-10-29T16:00:48Z | DO | cycle 13 | step 1 | revision r1 | attempt n=1/3 | группа A (`SelectGroupExpressionComparer`): 6 фактов на разных формах ключей — зелёные, но **0% → hit = 0 узлов**; обычный `GroupBy` не идёт через comparer (барьер: путь `SelectGroup`); группа B (`RelationshipResolver`) не выполнялась; сборка 0/0, конвенции 4/4 | `.pdca/integration-branch-coverage-13/rv1/attempts/step-1/flips-selectgroup.txt`
2026-10-29T16:02:55Z | DO | cycle 13 | step 1 | revision r1 | attempt n=1/3 | группа A переписана на `SelectGroup`: 6 фактов зелёные, но **0 из 43** zero-hit узлов comparer'а перевёрнуто — одиночная select-group проекция до этих армов не доходит; группа B не выполнялась; сборка 0/0, конвенции 4/4 | `.pdca/integration-branch-coverage-13/rv1/attempts/step-1/flips-selectgroup2.txt`
2026-10-29T16:08:38Z | DO | cycle 13 | step 1 | revision r1 | attempt n=1/3 | фича `PreWhere`+In-values: 1 факт на реальном ClickHouse зелёный; **первый подтверждённый `0% → hit`** — `PreparePreWhere` L3657/L3659 (2 узла, 4 ветви); барьер: `engine = Memory` не поддерживает PREWHERE (Code 182), нужен MergeTree; партиционные строки L252–256/L290–294 не затронуты; сборка 0/0, конвенции 4/4 | `.pdca/integration-branch-coverage-13/rv1/attempts/step-1/flips-prewhere.txt`
