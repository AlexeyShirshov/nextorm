# integration-branch-coverage-11

## Campaign state

Status: blocked
Form: incremental   Overlay: pdca-dotnet (coder+.NET)
Goal: branch coverage интеграционной сборки ≥ 76 % по counted-модели проекта (InMemory-классы исключены, дерево — текущий checkout)
Global acceptance: критерии AC-76 / AC-VALID / AC-INTEGRITY в том виде, как их вернул planner — см. `### PLAN record (r1, unchanged)` ниже, ссылка вместо пересказа
Recipe: metric = branch coverage; model = reportgenerator `Summary.txt` при `-classfilters:"-NextORM.Core.InMemory*"` (Cobertura parser, 8 сборок `nextorm.(core|sqlite|postgres|sqlserver|mysql|mariadb|clickhouse|entityframeworkcore).dll`, те же флаги, что в CI-гейте); thresholds `MIN_INTEGRATION_LINE_COVERAGE=65` / `MIN_INTEGRATION_BRANCH_COVERAGE=75` unchanged; threshold provenance: user-set target 76 %, **baseline-backed** — гейтовая база **38269 / 51773 = 73.91689 %** (branch) и 64038 / 80309 = 79.740 % (line); целевой числитель `ceil(0.76 × 51773) = 39348`, дефицит **+1079** ветвей; знаменатель цикла 11 = 51773 (pinned 38317 относится к дереву цикла 10 и недействителен); baseline снят КРАСНЫМ захватом и провизорен — см. `## Baseline record`
Allowed surface: `tests/nextorm.integration.tests/**` (new/extended integration tests), plus minimal product fixes strictly required by the criterion; no threshold/exclusion edits
Increment unit: accepted numeric branch-coverage change measured on the current tree under the recipe, or decision-relevant knowledge
Floor: ≥+0,5 процентного пункта; при D=51773 это минимум +259 ветвей   Target: step 1 ≥38528/51773; конечная цель ≥76% (дословно planner — `prep@r2` §Единица инкремента, floor и target)
Preparation ref: integration-branch-coverage-11/prep@r3 (сканируемые критерии уточнены пользовательским указанием про тикет rc5; supersedes prep@r2)
Commit policy: none
Constraints: FluentAssertions only in tests; real DBMS only (fail-closed, no NEXTORM_SKIP_PROVIDERS); warnings-as-errors, nullable, CPM, CRLF; Roslyn for C# symbols; no commit/push/merge; no edits to `.github/workflows/**` thresholds
Effort accounting: PLAN estimate — несколько рабочих дней (recorded, never a cutoff)
Baseline evidence: `.pdca/integration-branch-coverage-11/rv1/` — `baseline.json`, `counts.json`, `coverage-run.log` (rc=2, sha256 `72190cb9…`), `coverage-run.rc`, `coverage-run.tail.log`, `integration.cobertura.xml` (sha256 `aab1c938…`), `Summary.txt` (sha256 `3b2b3388…`), `reportgenerator.log` (rc 0), `skip-budget.log` (rc 1), `build.log` (rc 0), `measure-start.utc`/`measure-end.utc`
Evidence root: `.pdca/integration-branch-coverage-11/rv1/`
Steps: step 1 → basis 38269/51773 → credited — → verdict pending → evidence `.pdca/integration-branch-coverage-11/rv1/attempts/step-1-n2/`
Current step: 1   Phase: ACT (step 1, r2, closed)   Cycle: 11   Plan revision: r2   Attempt: n=3/3
Next-step seed: (consumed by step 1)
Accepted knowledge: гейтовая база текущего дерева = Summary 38269 / 51773 = 73.91689 %, целевой числитель 39348, дефицит +1079 ветвей; захват КРАСНЫЙ и провизорный, инкрементом не принят ; Assumptions and unknowns: достижимость оставшихся 1079 ветвей неизвестна; влияние двух ClickHouse-таймаутов 120 s на числитель неизвестно ; 2026-10-29: baseline bootstrap принят как база отсчёта шага 1 — 38269/51773, дефицит +1079; AC-VALID этим не подтверждён ; 2026-10-29 (DO шага 1): измеренный прирост **+230 gate-ветвей** (38499/51773 = 74,36115 %, +0,44425 п.п.), до цели шага 38528 не хватило 29 ветвей ; MariaDbNativeFunctions 262/604 → 457/604 raw (+195): класс до шага вообще не исполнялся интеграционным прогоном ; MySqlNativeFunctions 802/1130 → 805/1130 raw (+3) при 100 % line coverage — остальные 325 branch-точек лежат на двух compiler-generated pattern-строках (`MySqlDialect.cs:828` Supports, `:857` Render) и недостижимы исполнением новых имён ; следствие: 670 raw-ветвей из gap-map — верхняя оценка, реальный потолок name-driven теста для этих двух классов ≈ +230 ; ClickHouse: два baseline-таймаута в этом прогоне прошли, но прогон красный по другой причине — `T288…CapturedNullThenValue…` упал с server-side `Code 216 QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING` за 573 мс ; 2026-10-29 (DO попытка 2): gate **38567/51773 = 74,49250 %**, **+298** ветвей (+0,57559 п.п.) к базе и +68 к попытке 1, цель шага 38528 превышена на 39, до 76 % остаётся 781 ; `MySqlFunctionTranslator` 142→226/278, `MariaDbNativeFunctions` 262→468/604, `MySqlNativeFunctions` 802→806/1130 (последний исчерпан: остаток — недостижимые половины compiler-generated string-switch) ; ClickHouse в этом прогоне зелёный (216 не повторился), но прогон красный на pre-existing plan-cache identity тесте `T320SqliteTvfTests`, проявившемся из-за изменения расписания ; продуктовое наблюдение: `xxh3`/`xxh32` рекламируются для MariaDB 11.4, сервер отвечает 1305 (issue #352, rc5) ; 2026-10-29 (DO попытка 3): gate **38532/51773 = 74,42489 %**, **+263** (+0,50799 п.п.), цель шага превышена на 4 ветви, до 76 % — 816 ; рендер-онли кейсы сняты (цена −35 к попытке 2), `unknown-name` удалён как недостижимый из ORM ; T320-гонка доказана пробой (purge между prepare ломает identity, 400/400 под конкуренцией) и изолирована коллекцией `T320 tvf plan identity` ; ClickHouse-остаток: 120-секундный застой DDL в эксклюзивной полосе (не 216, не конкуренция)
Rejected approaches: **вторая (финальная) диагностическая сессия rv2** — `…/rv2/diagnostics/session-2/`, кандидат вердикта **`undetermined`** (клиентский захват в момент застоя отсутствует, обход ретранслятора недоступен; §3C — финальный STOP) ; **диагностическая сессия rv2** проведена как сбор фактов, а не попытка — `.pdca/integration-branch-coverage-11/rv2/diagnostics/` (кандидат вердикта `undetermined`, смещение против внешней конкуренции) ; подключение двух ClickHouse-тестов к `ClickHouseReadRetry` как исправление (см. Blockers, CH-COLLECTION) — машинерия вне разрешённой поверхности, лимит «одна retry-операция на тест», а падение DictGet происходит в DDL конструктора, которое `ClickHouseReadOperation.Audited` отвергает по построению ; Deferred: исправление принадлежности к коллекции для четырёх ClickHouse-классов (нужен отдельный прогон для валидации) ; Blockers: **CH-COLLECTION** — четыре тест-класса обращаются к общему ClickHouse-серверу из чужих коллекций и работают параллельно с коллекцией `ClickHouse`, вопреки конвенции в `Providers/ClickHouseContainer.cs`; следствия — коллизия query id (216) и, как лучшая гипотеза, 120-секундные HTTP-застои; фикс внутри разрешённой поверхности, но не применён в этом шаге (валидация потребовала бы второго полного измерения) ; **S1-NUMERIC** — +230 ветвей, на 29 меньше цели шага 38528 ; Protected/foreign: —
Decisions: form incremental (planner), plan persisted before approval ; 2026-10-29: пользователь перевёл кампанию в **autonomous mode** с указанием «делай это автономно, не нужно меняться спрашивать каждый раз» — последующие шаги не требуют approval prompt, драйвером цикла является сам primary без запроса одобрения ; 2026-10-29: пользовательское уточнение критериев (дословно): «если обнаружена ошибка в продукте, необходимо открыть тикет в rc5 и продолжать работу. тест при этом можно скипнуть со ссылкой на открытывй тикет» — записано как preparation revision `prep@r3`: skip разрешён только для доказанного продуктового дефекта при открытом issue с milestone rc5, после поиска дублей, только через `Assert.Skip`; на текущие падения не распространяется
Defect history: F1-NUMERIC fixed (n=3 38532 ≥ цели 38528); F2-CLICKHOUSE: 216 устранён (эксклюзивная полоса), остался pre-existing DDL-застой 120 с; F3-EVIDENCE fixed; F3b-PLANCACHE fixed (коллекция `T320 tvf plan identity`); F-INTEGRITY fixed (рендер-онли → live/удалено); F-TRACKING fixed (issue #352, rc5)
Checkpoint: last boundary — **шаг 1 / r2 `not accepted`** (n=3/3); accepted basis — **38269/51773 = 73,91689 %** (не менялась); accepted increment — **none**; последний некредитованный кандидат — **38561/51773 = 74,48091 %** (+292, до 76 % остаётся 787), diagnostic only; disposition — **paused** (STOP экспертизы и третьего CHECK); resume point — любая будущая `r3` начинается с проверки гипотезы «серверная конкуренция, а не форма теста» (снять `system.processes` / `query_log` в момент таймаута либо изолировать всю ClickHouse-полосу целиком) и требует свежей авторизации пользователя на изменение критерия зелёности; иначе STOP остаётся правильным; кандидат, тесты и все evidence сохранены
Pending approval ref / Approval record: pending — `integration-branch-coverage-11/step-1@plan-r1` (запрошено одобрение шага 1: cycle N=11, plan revision r1, критерии S1-NUMERIC / S1-GREEN / S1-INTEGRITY; normal mode) ; ранее — `integration-branch-coverage-11@plan-r1` — APPROVED (normal mode; explicit user approval received 2026-10-29, task/cycle_id `integration-branch-coverage-11`, cycle N=11, plan revision r1, набор критериев AC-76 / AC-VALID / AC-INTEGRITY по PLAN r1; причина — пользователь одобрил план; stop cap отсутствует: пользователь ответил «Без лимита», и одобрение плана лимитом не является)
Stop verdict: **STOP** — ревизия r2 закрыта как `not accepted`: бюджет попыток r2 исчерпан (три содержательных CHECK, `n=3/3`, все `not accepted`), дефект критериев не установлен; экспертиза рекомендовала закрыть r2, отвергнув и дальнейшее устранение fixture-DDL по классам (опровергнутый механизм / замаскированная четвёртая попытка), и маршрут «тикет rc5 + skip» (продуктовый дефект не доказан); цель кампании не достигнута (см. `## Campaign outcome`). Историческая причина прежнего STOP (r1): критерии оставались неудовлетворёнными, бюджет попыток шага исчерпан (`r1`, `n=3/3`), три CHECK подряд `not accepted`, дефект критериев не установлен; новый шаг или ревизия контракта не открывались, так как это была бы замаскированная четвёртая попытка; обязательная консультация `pdca-escalate` не выполнена — ресурсный блокер `Missing API key`; цель кампании не достигнута (см. `## Campaign outcome`)

### PLAN record (r1, unchanged)

````markdown
## PLAN — начальный контракт

**Исходная задача:** «pdca необходимо довести бранч покрытие интеграционных тестов до 76%»

### Форма и специализация

- **Форма:** `incremental`; драйверу требуется подключить `pdca-incremental`.
- **Специализация:** `pdca-dotnet`, overlay `coder+.NET`, включая контракт `pdca-coder`.
- **Обоснование:** актуальное покрытие и объём достижимого прироста неизвестны: имеющиеся измерения относятся к разным деревьям и знаменателям. Именно эти сведения определяют следующий полезный результат. Одна числовая цель сама по себе не обосновывает обычный ограниченный цикл; слово `pdca` форму не фиксирует.

**Входной scope:** установленное текущее counted-покрытие интеграционной сборки и определённый относительно него дефицит до 76%; устранена неопределённость, позволяющая выбрать следующий полезный прирост. Это первый информационный инкремент, не замена конечной цели.

### Цель и результат

**Запрошенный результат:** branch coverage интеграционных тестов не ниже 76%.

**Передаваемый результат:** текущее дерево проекта с интеграционным branch coverage не ниже 76% по действующей counted-модели проекта.

Число добавленных тестов самостоятельной целью не является.

### Критерии конечной приёмки

**AC-76 — покрытие.**
Для текущего дерева точное отношение покрытых ветвей к общему числу учитываемых ветвей составляет **не менее 0,76**. Округление значения ниже 76% до отображаемых «76%» не считается достижением цели.

Учитываются ветви сборок core, sqlite, postgres, sqlserver, mysql, mariadb, clickhouse и entityframeworkcore с действующим исключением классов `NextORM.Core.InMemory*`. Числитель и знаменатель относятся к одному состоянию дерева и одной модели учёта; literal-показатель не подменяет counted-показатель.

**AC-VALID — полноценность интеграционного результата.**
Достигнутое покрытие относится к успешно работающей интеграционной сборке с реальными СУБД и всеми предусмотренными провайдерами. Ошибки тестов, отключённые через `NEXTORM_SKIP_PROVIDERS` провайдеры или использование in-memory вместо реальной БД не допускают положительной приёмки.

**AC-INTEGRITY — неизменность условий цели.**
Покрытие достигнуто без снижения порогов, расширения исключений или искусственного сокращения учитываемого кода ради процента. Значения `MIN_INTEGRATION_LINE_COVERAGE=65` и `MIN_INTEGRATION_BRANCH_COVERAGE=75` остаются неизменными.

### Ограничения и доступ

- FluentAssertions для проверок; `Xunit.Assert` — только разрешённые skip-примитивы.
- Интеграционные тесты действительно обращаются к БД; in-memory остаётся unit-only.
- Сохраняются warnings-as-errors, nullable, CPM и CRLF.
- Семантические вопросы C# разрешаются через Roslyn.
- Никаких commit, push, merge, изменений host-конфигурации и переписывания legacy-записей.
- При затрагивании публичных имён действуют требования документации EN/RU.
- Состояние контейнерной инфраструктуры и её доступность сейчас не подтверждены. Известный адрес сокета не доказывает работоспособность среды; недоступность требует восстановления либо фиксации конкретного ресурсного блокера.

### Пробелы и основания

| Пробел | Закрытие |
|---|---|
| Точное покрытие текущего дерева | Предмет первого инкремента; старые значения не принимаются за текущий baseline |
| Идентичность показателя | Зафиксирована counted-модель; достижение определяется точным отношением, не округлённым отчётом |
| Объём оставшейся работы | Остаётся неизвестным до первого инкремента; это основание формы `incremental` |
| Доступность всех СУБД | Явный ресурсный риск, не основание пропускать провайдеры |

### Минимальное решение

Предпочтителен минимальный прирост содержательных интеграционных тестов, закрывающих реально достижимые ветви без изменения публичного поведения. Исправление обнаруженных дефектов допустимо в пределах цели и ограничений.

Изменение порогов, исключений или подмена counted-показателя альтернативами не являются решениями задачи.

### Привязка и размещение

- Исходная привязка: ветка `1.0.9-rc4`, HEAD `0290d466`, рабочее дерево чистое.
- Статус: `.pdca/status/integration-branch-coverage-11.md`.
- Рабочие материалы: `.pdca/integration-branch-coverage-11/rv<k>/`.
- Единственный binding root — checkout, в котором выполняется работа и CHECK; при вторичном checkout относительные пути сохраняются.
- Старые закрытые циклы остаются неизменными.

### Оценка, риски и остановка

**Предварительная оценка для normal mode:** несколько рабочих дней; уточняется после первого инкремента и не является лимитом исполнения.

Основные риски: изменение знаменателя, ограниченная достижимость оставшихся ветвей, ресурсные проблемы СУБД, недостоверное округление.

**Завершение `done`:** только положительный независимый CHECK всей цели по AC-76, AC-VALID и AC-INTEGRITY. Локально успешный инкремент сам по себе завершением не является.

Если требуется изменить пользовательскую цель, нарушить обязательные ограничения или отсутствует необходимый доступ, работа не объявляется успешной: фиксируется конкретный блокер либо передаётся решение об эскалации. Один неудачный кандидат не доказывает недостижимость цели.

### Передача в DO

Каждый DO-dispatch несёт ссылку на этот полный контракт: исходную задачу, критерии, ограничения, запреты и текущий scope. Декомпозицию выбирает DO; способы проверки и доказательства — CHECK. Последующие инкременты заранее не назначаются.

**Уверенность:** высокая в контракте метрики и выборе incremental; достижимость 76% на текущем дереве пока не установлена.
````

## Original task

«pdca необходимо довести бранч покрытие интеграционных тестов до 76%»

## Preparation record

- preparation_state: `ready`
- preparation_ref: `integration-branch-coverage-11/prep@r2`
- path: `.pdca/integration-branch-coverage-11/prep-r2.md`
- supersedes: `integration-branch-coverage-11/prep@r1` (r1 сохранена неизменной как история)
- записано: 2026-10-29T07:10:52Z (UTC, `date -u`)
- причина: floor `≈190 ветвей` в r1 посчитан до установки baseline и неверен для D=51773 (0,5 п.п. = минимум +259 ветвей); r1 не содержала floor авторства planner; цель, критерии, рецепт и разрешённая поверхность не изменились

## Baseline record

Захвачен 2026-10-29T05:56:02Z–06:15:16Z (UTC) на дереве HEAD `0290d46637a14ca37d28e24740badee57cb1bf07`
(ветка `1.0.9-rc4`, `git status --porcelain` = 1 — только неотслеживаемый файл статуса), открыт против
`integration-branch-coverage-11/prep@r1`.

- **Точное значение (гейтовая counted-модель):** branch **38269 / 51773 = 73.91689 %**; line **64038 / 80309 =
  79.740 %**. Источник — reportgenerator `Summary.txt` при `-classfilters:"-NextORM.Core.InMemory*"`, то есть ровно та
  модель и те флаги, что стоят в CI-гейте (`.github/workflows/dotnet.yml:61-66`); вычитания констант нет.
- **Целевой числитель для ≥ 0,76:** `ceil(0.76 × 51773) = 39348` (39347 даёт 75.999 %), т.е. **+1079** ветвей к 38269.
- **Диагностическая модель (вторичная, явно диагностическая):** сырой Cobertura per-class detail (InMemory исключён) —
  branch **28495 / 39266 = 72.56914 %**, целевой числитель `ceil(0.76 × 39266) = 29843`, дефицит **+1348**. Полезен её
  per-assembly разбивка (`per_assembly` в `baseline.json` / `counts.json`); гейтовым числом остаётся 38269 / 51773.
- **Историческая строка (не оперативное число):** документированные константы исключений цикла 10 (−65 покрытых /
  −90 всего ветвей) на это дерево **не применимы** и из базы удалены: реальный вклад `NextORM.Core.InMemory*` сегодня —
  **0 / 2082** ветвей (0 покрытых), и он уже снят classfilter'ом.
- **Состав:** модули `nextorm.(core|sqlite|postgres|sqlserver|mysql|mariadb|clickhouse|entityframeworkcore).dll`
  (8 сборок отчёта); исключения — только атрибутные из `coverage.integration.settings.xml` плюс классы
  `NextORM.Core.InMemory*`; новых исключений не добавлено, пороги не менялись.
- **Знаменатель дрогнул относительно literal-фигур цикла 10 (F3):** branch 51773 против 38407 = **+13366**,
  line 80309 против 59949 = **+20360**. Причина — рост дерева, а не инструментарий и не scope: `.config/dotnet-tools.json`
  не менялся с 2026-10-09 (те же dotnet-coverage 18.11.2 / reportgenerator 5.5.11), настройки покрытия не менялись;
  в class-detail 38 файлов, отсутствовавших в цикле 10 (+4741 line / +2325 branch), и +12160 line в 110 уже
  существовавших файлах. Ранее записанные `+13456` / `+20596` считались против pinned 38317 / 59713 — это
  **«against pinned denominator»**, недействительная смешанная база; как оперативные числа они сняты.
  Cycle-10 baseline 28926/38317 (75.49 %) с этим деревом несопоставим, а смешивание числителя цикла 11 с pinned
  38317 недействительно и не цитируется.
- **Статус базы — провизорный:** `provisional red capture (rc=2, 2 ClickHouse HttpClient 120 s timeouts, 0 opt-outs)`.
  Первый шаг, добавляющий тесты, обязан дать зелёный прогон (`rc=0`, `failed: 0`), и финальный CHECK всей цели
  требует `rc=0`; на красном захвате положительная приёмка AC-VALID невозможна.
- **Захват фактически использованный (команды дословно):**
  `dotnet build tests/nextorm.integration.tests -c Debug` → rc **0**, `0 Warning(s) 0 Error(s)`, 00:01:56.98;
  `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 timeout 3600 dotnet tool run dotnet-coverage collect -s coverage.integration.settings.xml -f cobertura -o tests/coverage/integration.cobertura.xml "dotnet test tests/nextorm.integration.tests --no-build --verbosity normal --filter-not-trait Category=Diagnostic"` → rc **2**, wall **1154 s**, suite 19m 08s 200ms, exit 137 / «Out of memory» отсутствуют;
  `dotnet tool run reportgenerator -reports:tests/coverage/integration.cobertura.xml -targetdir:tests/coverage/integration-report -reporttypes:"Html;TextSummary" -riskhotspotassemblyfilters:"+nextorm.*" -classfilters:"-NextORM.Core.InMemory*"` → rc **0**, 21.3 s (флаги байт-в-байт как в CI-шаге `.github/workflows/dotnet.yml:61-66`);
  `python3 tools/integration/check-integration-skip-budget.py --log .pdca/integration-branch-coverage-11/rv1/coverage-run.log` → rc **1**.
- **Exit status захвата: КРАСНЫЙ.** suite 7665 total / 7466 succeeded / **2 failed** / 197 skipped / 0 not run.
  Оба падения — ClickHouse-таймауты `HttpClient.Timeout of 120 seconds`:
  `ClickHousePlanKeyBranchCoverage5Tests.Settings_SameAndChangedValue_ShouldKeySeparately` (2m 40s) и
  `Cycle7ClickHouseDialectTests.DictGet_ExistingKey_ShouldReturnTheAttribute` (2m 00s). Числа выше — измерение этого
  красного прогона: зелёным baseline он не является и положительную приёмку AC-VALID сам по себе не поддерживает.
  Rerun не выполнялся (правило брифа: red остаётся red). Провайдеры не отключались: `NEXTORM_SKIP_PROVIDERS` не
  задавался, 197 skips — capability-бюджет, не opt-out.
- **Skip-budget gate:** rc **1** — `FAIL: no 'Skipped: N' summary`. Причина форматная: скрипт ждёт форму лога
  `dotnet run -- -notrait` (`<name> [SKIP]` + `Skipped: N`), а этот захват — CI-форма `dotnet test`, где сводка
  печатает `skipped: 197`, а записи — `skipped <FQN>`. В логе ровно 197 skip-записей, столько же, сколько в сводке;
  `tools/**` вне поверхности измерения, поэтому скрипт не правился и прогон в другой форме не повторялся.
- **UTC-метки:** build 05:53:59–05:55:57Z; measure 05:56:02–06:15:16Z.
- **Доказательства:** `.pdca/integration-branch-coverage-11/rv1/` — `baseline.json` (полная запись: команды, rc,
  версии инструментов, per-assembly разбивка, suite counts), `counts.json`, `coverage-run.log`, `coverage-run.rc`,
  `coverage-run.tail.log`, `integration.cobertura.xml`, `Summary.txt`, `reportgenerator.log`, `skip-budget.log`,
  `build.log`, `measure-c11.py`, `measure-start.utc` / `measure-end.utc`.
- **Дерево заморожено на момент захвата:** HEAD `0290d466…`. После окончания прогона параллельная сессия увела HEAD
  `0290d466…` → `2bd6841d…` (bench budget re-pin) → `29fdcf89…` (ci: drop the allocation regression gate) и закоммитила
  свою правку `.github/workflows/dotnet.yml` и удаление `tools/benchmarks/iteration14*` в 06:19:56Z — это чужая работа:
  она не заявлена, в измеренных бинарях не участвовала (`--no-build` от сборки 05:53:59Z), а `git status --porcelain`
  на момент записи baseline = 1 (только неотслеживаемый файл этого статуса).
- **Рабочий head:** `29fdcf89` — `src/` и `tests/` идентичны `0290d466`, измеренные бинари собраны из `0290d466`.
- **Исправление по CHECK (cycle 11, CHECK/DO):** F1–F3 к базовой записи — неверная базовая фигура и дефицит,
  неприменимое вычитание констант цикла 10 и неразмеченная диагностическая модель, арифметика дрейфа знаменателя —
  **устранены**; F4 (красный захват) — **принят как провизорное состояние**, не как green. Skip-budget:
  `recorded exception — tooling incompatibility`; substitute evidence — 197 разобранных skip-записей,
  `coverage-run.log:1008`; rc 1 не переклассифицируется в pass.

## Checkpoint

last boundary — **шаг 1 закрыт `not accepted`** (ACT, plan revision r1, attempt n=3/3); accepted basis — baseline **38269/51773 = 73,91689 %** (не менялась); accepted increment — **none** (последнее измерение 38532/51773 = 74,42489 %, +263, до 76 % остаётся 816 — диагностическое, не новая база); disposition — **paused**: STOP планировщика, кампания `blocked`, цель не достигнута; resume point — снять блокер эскалации `Missing API key` либо получить явное указание пользователя, меняющее контракт, затем заново войти в PLAN; непринятый кандидат, тесты и все evidence сохранены

## Step records

### Step 1

#### Current state

- task: `integration-branch-coverage-11` · form: `incremental` · specialized skill: `pdca-dotnet` (coder+.NET)
- mode: `autonomous` (переключён пользователем 2026-10-29 после старта шага)
- cycle: `N=11` · revision: `r=1` · attempt: `n=3/3` (бюджет попыток исчерпан)
- scope: step 1 — содержательный прирост branch coverage интеграционной сборки (кластер native functions MariaDB/MySQL плюс надёжность двух существующих ClickHouse-проверок) · status path: `.pdca/status/integration-branch-coverage-11.md`
- goal: довести branch coverage интеграционной сборки до ≥76 % — авторитетны `## Original task` и `#### PLAN record`
- phase: `ACT` · record: `completed` · outcome: `not accepted / no credit` (ревизия r2, n=3/3; S1-GREEN unmet, F-R2-STATE open)
- basis (принятая): baseline **38269/51773 = 73,91689 %** · target шага 1: **≥38528/51773**
- criteria: step-1 критерии **S1-NUMERIC / S1-GREEN / S1-INTEGRITY** — точный текст в `#### PLAN record` (source: `integration-branch-coverage-11/step-1@plan-r1`), здесь не перепечатываются
- source: `pdca-planner` — step-boundary call после принятия bootstrap · context refs: `integration-branch-coverage-11/prep@r2`, `## Baseline record`

#### Original task

По ссылке, без пересказа: `## Original task` этого файла — единственная каноническая копия, дословная формулировка пользователя. Ссылка действительна против неизменяемого исходника.

#### PLAN record

r1 — `integration-branch-coverage-11/step-1@plan-r1`, получен от `pdca-planner` (step-boundary call после принятия bootstrap). Встроен дословно, без изменений:

```text
## GOAL

**Исходная задача:** «pdca необходимо довести бранч покрытие интеграционных тестов до 76%».

**Решение границы:** принять независимо проверенный bootstrap как информационный результат и открыть **step 1**. Форма `incremental`, специализация `pdca-dotnet` / `coder+.NET` и обоснование формы сохраняются по ссылке на `plan@r1`.

Запрошенный и конечный передаваемый результат остаются одинаковыми: интеграционное branch coverage **≥76%**. Результат этого шага — содержательный числовой прирост при полностью зелёной интеграционной сборке.

## CURRENT STATE

- Авторитетная база: **38269/51773**, примерно **73,9169%**, по принятой gate-модели с исключением InMemory.
- Конечная цель при неизменном знаменателе: **39348 покрытых ветвей**, остаток **1079**.
- Raw-Cobertura **28495/39266** используется только для поиска пробелов, не для приёмки.
- Красный baseline сохраняется без переписывания: два ClickHouse timeout, 0 assertion failures, 0 opt-outs. Его принятие не означает выполнения AC-VALID.
- Измерены бинарники `0290d466`; рабочая привязка — `29fdcf89`. Подтверждённая идентичность `src/` и `tests/` позволяет продолжить без повторения baseline только ради нового SHA. Чужие изменения не являются вкладом кампании.

## HARD CONSTRAINTS

- Изменяемая продуктовая поверхность: **`tests/nextorm.integration.tests/**`**. Записи кампании ведутся отдельно в её существующем PDCA workspace.
- Только реальные СУБД; никаких in-memory подстановок, opt-outs, новых исключений покрытия или искусственного удаления учитываемых ветвей.
- Сохраняются AC-76, AC-VALID, AC-INTEGRITY, действующие пороги и skip-policy.
- FluentAssertions; Roslyn для C#-символов; CRLF, nullable и warnings-as-errors.
- Никаких commit, push, merge, изменений production-кода, workflow, coverage-конфигурации или `tools/**`.

Два ClickHouse timeout разрешено устранить **внутри этого шага**, если необходимая корректировка находится в разрешённой тестовой поверхности и сохраняет смысл проверок. Это не отдельный автоматически засчитываемый инкремент. Запрещены подавление ошибок, перевод тестов в skips, бесконечные ожидания и маскирующие retries. Изменение ожиданий допустимо лишь с обоснованием, не как способ скрыть неисправность.

Если причина требует изменений вне разрешённой поверхности, фиксируется конкретный блокер; полномочия не расширяются, зелёность не отменяется.

## SCOPE

Область первого прироста — **семантика native functions MariaDB и MySQL**, проверяемая на соответствующих реальных СУБД, плюс необходимая надёжность двух существующих ClickHouse-проверок.

Почему этот кластер:

- У `MariaDbNativeFunctions` диагностически остаются 342 ветви; MariaDB-контейнер существует, но MySQL-host не создаёт MariaDB-типы. Это указывает на конкретный пробел провайдерного охвата.
- У `MySqlNativeFunctions` остаются 328 ветвей и нет текущего покрытия интеграционной сборкой.
- Реальные соответствующие БД дают обоснованный путь к проверке поддерживаемой функциональности, а не только к исполнению вспомогательного кода.

**Оговорка:** 670 raw-ветвей — потенциальная область, не обещание достижимого gate-прироста. Достижимость каждой ветви, поддерживаемые возможности серверов и числовой выход остаются рисками. DO выбирает реализацию и декомпозицию.

**Не входят:** остальные кластеры core/Postgres/EF Core, общий разбор всех 197 skips, исправление skip-parser, production-рефакторинг, покрытие недостижимых DEBUG-ветвей и общий ремонт инфраструктуры.

## ACCEPTANCE

- **S1-NUMERIC:** точное gate-покрытие выросло минимум на **0,5 процентного пункта** относительно `38269/51773`. При неизменном знаменателе это **≥38528/51773**, то есть **минимум +259 покрытых ветвей**. Округлённого отображения недостаточно.
- **S1-GREEN:** полноценная интеграционная сборка завершена успешно: **rc=0, failed=0**, все предусмотренные провайдеры участвуют, opt-outs отсутствуют. Действующая skip-policy соблюдена; число 197 не становится новым автоматически разрешённым бюджетом.
- **S1-INTEGRITY:** прирост получен при соблюдении всех ограничений и без ослабления смысла существующих проверок; новые проверки действительно проверяют поведение соответствующих реальных СУБД.

Восстановление покрытия ранее упавших ClickHouse-тестов нельзя автоматически описывать как эффект новых тестов.

При изменении знаменателя абсолютное число 38528 перестаёт быть достаточным: сохраняется точное требование прироста **≥0,005** к базовому отношению, без подмены модели.

Локальная приёмка шага не означает `done`. Завершение кампании требует независимого CHECK всей цели по неизменным AC-76/AC-VALID/AC-INTEGRITY.

## TARGETS

**Числовой target step 1:** **+0,5 п.п.; минимум +259 ветвей при D=51773**.

Размер составляет около четверти текущего дефицита. При фиксированной цене полного измерения около 19 минут это обоснованный размер содержательного пакета: не микроприрост и не обязательство закрыть все 1079 ветвей за один шаг. Это калибровка, не доказательство оптимальности.

**Требуется supersede-запись подготовки, обозначаемая `prep@r2`:**

> Floor числового инкремента: ≥+0,5 процентного пункта. При D=51773 это минимум +259 ветвей. Target step 1: ≥38528/51773. Конечная цель: ≥76%.

Причина — исправление ошибочного пересчёта «≈190» после установленного baseline, а не изменение пользовательской цели. Старую запись сохранить; `plan@r1` и историю попыток не обнулять. Регистрацию выполняет executor.

**Оценка:** ориентировочно 1–3 рабочих дня, с существенной неопределённостью достижимого выхода и ClickHouse-стабильности; это не cutoff. Уверенность в арифметике высокая, в достижении выбранным кластером требуемого прироста — средняя.
```

#### Approval record

pending — `integration-branch-coverage-11/step-1@plan-r1` (запрошено 2026-10-29T07:11:48Z UTC, normal mode; cycle N=11, plan revision r1, критерии S1-NUMERIC / S1-GREEN / S1-INTEGRITY); решение пользователя ещё не получено. 2026-10-29: пользователь перевёл кампанию в autonomous mode («делай это автономно, не нужно спрашивать каждый раз»); DO шага 1 выполнен автономно по одобренному контракту `step-1@plan-r1` без отдельного approval prompt, критерии не менялись — уточнена только skip-policy (`prep@r3`).

#### DO result

### Revision r2 (n=3/3) — текущий (финальная попытка r2)

**Статус: кандидат возвращён; `S1-RELIABILITY` закрыта по трём пунктам, но прогон КРАСНЫЙ (`rc=2`) по
одному ClickHouse-таймауту вне fixture-пути.** Вердикт не выносится — его даёт CHECK.

- **F-R2-OBS — провенанс.** Артефакт теперь сам несёт привязку: каждая строка учёта содержит UTC-метку,
  `pid`, `run_id` (из `NEXTORM_CLICKHOUSE_FIXTURE_RETRY_RUN_ID`) и `process_touch_utc`, плюс шапку
  `CLICKHOUSE-FIXTURE-RETRY-PROVENANCE`. Для этого прогона: `pid=3936625`, `run_id=step-1-r2-n3`,
  `process_touch_utc=2026-10-29T10:47:50.9987539Z`, mtime файла `2026-10-29T10:57:01.436Z` лежит внутри
  интервала измерения `10:41:37Z … 10:57:03Z`; sha256 `766bfb6c…`. Точная, несокращённая команда с
  окружением — в `measurement-command.txt`.
- **F-R2-IDEMPOTENCE + F-R2-STATE — новая схема изоляции.** Сессионная temp-таблица в ClickHouse
  невозможна (драйвер открывает соединение на запрос), а `[SqlTable]` статичен, поэтому выбран
  разрешённый вариант «детерминированный ключ + дедуплицирующий движок»: таблица
  **`ch3_marker_v2`**, движок **`ReplacingMergeTree order by (id)`**, все чтения фактов идут через
  **`.Final()`**. Повторный insert тех же четырёх id схлопывается; `count()` остаётся лишь быстрым
  путём, корректность даёт ключ+`FINAL`. Суффикс `_v2` — идентичность схемы: таблица старой схемы
  (движок `Memory`) не наследуется через `if not exists` и не читается. После сида фикстура
  утверждает точный набор id `{1,2,3,4}` через `FINAL`, а не число строк. DDL-застоя нет: `drop`
  отсутствует, бюджет retry (1/операцию, 1/сборку) и fixture-only разделение сохранены.
- **Измерение (gate-модель):** branch **38561 / 51773 = 74,48091 %** → **+292 ветви, +0,56400 п.п.** к
  базе 38269; цель шага **38528 превышена на 33**; до 76 % остаётся **787**. line 64153/80309. Число
  совпадает с принятым прогоном r2 (38561).
- **Прогон — КРАСНЫЙ: `rc=2`**, 7871 total / 7673 passed / **1 failed** / 197 skipped. Падение —
  `ClickHouseT291AverageLiveTests.EmptySetAverage_ShouldBeNull`, `HttpClient.Timeout of 120 seconds`
  на **чтении**, не в fixture: операция `ClickHouseDialectBranchCoverage3Tests.marker` заняла 0 секунд,
  одна попытка, `retries_used=0`. Это тот же платформенный класс транспортного застоя, что и раньше;
  в разрешённой поверхности исправления нет, таймаут не расширялся, повторов не делалось.
- **Учёт retry (из процесса измерения, `run_id=step-1-r2-n3`):** одна попытка `marker`, `outcome=succeeded`,
  `retry=0`; итог `retries_used=0 per_operation_limit=1 per_suite_limit=1 budget_exhausted=false`.
- **Точечная проверка до измерения:** ClickHouse 30/30, PlanKey 5/5, Cycle7 27/27, MariaDB 69/69,
  MySQL 137/137. **Сборка:** `dotnet build nextorm.slnx -c Debug` → rc 0, `0 Warning(s) 0 Error(s)`.
- **Доказательства.** `.pdca/integration-branch-coverage-11/rv1/attempts/step-1-r2-n3/` — `Summary.txt`,
  `integration.cobertura.xml`, `coverage-run.log/.rc/.tail.log`, `reportgenerator.log`, `build.log`,
  `fixture-retry-accounting.txt`, `provenance.txt`, `measurement-command.txt`, `hashes.txt`,
  `report.json`, `git-status.txt`, `git-diff-stat.txt`, `tracked-changes.diff`, `new-files.diff`,
  `targeted-runs.txt`, UTC-метки.

### Revision r2 (n=2/3) — superseded

**Статус: кандидат возвращён; прогон ЗЕЛЁНЫЙ (`rc=0`, `failed=0`); `S1-RELIABILITY` закрыта по существу.**
Вердикт не выносится — его даёт CHECK.

- **F-R2-OBS — исправлено.** Учёт fixture-retry теперь пишет файл из **того же процесса**, что и полное
  измерение: путь из `NEXTORM_CLICKHOUSE_FIXTURE_RETRY_ACCOUNTING`, каждая попытка дописывается сразу
  (`operation`, `attempt`, `outcome`, `reason`, `retry`), итоговая строка — на `ProcessExit`. Файл
  `fixture-retry-accounting.txt` этого прогона содержит ровно одну строку попытки и одну итоговую:
  `CLICKHOUSE-FIXTURE-RETRY-SUMMARY retries_used=0 per_operation_limit=1 per_suite_limit=1
  budget_exhausted=false`. Нулевой случай теперь доказан, а не выведен.
- **F-R2-IDEMPOTENCE — исправлено.** `create table if not exists ch3_marker …` (безопасно повторять);
  `drop table if exists` удалён совсем (с `if not exists` и сторожевым счётчиком ему нечего было делать,
  и именно он зависал); сид больше не может задвоиться: внутри той же retry-операции перед `SeedSql`
  читается `select count() from ch3_marker` — сид выполняется только при 0 строк, при 4 считается
  готовым, при любом другом числе бросается исключение вместо повторной вставки. Вся marker-подготовка —
  одна fixture-операция; утверждения и продуктовые чтения остаются вне её; нетранзиентные ошибки
  пробрасываются как есть.
- **Измерение (gate-модель):** branch **38560 / 51773 = 74,47898 %** → **+291 ветвь, +0,56207 п.п.** к
  базе 38269; цель шага 38528 **превышена на 32**; до 76 % остаётся **788**. line 64153/80309.
  Прогон: **rc=0**, 7871 total / 7674 passed / **0 failed** / 197 skipped / 0 not run.
  **Против прогона r2 (38561) — минус одна ветвь**, и это сказано прямо: единственная потеря в
  `nextorm.core` (21241 → 21240) — переписанная ClickHouse-подготовка больше не исполняет
  `drop table if exists`, который задевал одну ветвь ядра; остальные сборки не двигались. Уровень
  остаётся выше цели шага на 32 ветви и выше базы на 291.
- **Диагностический raw** (не модель приёмки): 28786/39266 = 73,31024 % (+291); mariadb 476, mysql 1357,
  clickhouse 1684, core 21240; `MariaDbNativeFunctions` 463/604, `MySqlFunctionTranslator` 226/278,
  `MySqlNativeFunctions` 805/1130 — без изменений к r2.
- **Точечная проверка до измерения:** ClickHouse 30/30, PlanKey 5/5, Cycle7 27/27, T320 10/10,
  MariaDB 69/69, MySQL 137/137.
- **Сборка.** `dotnet build nextorm.slnx -c Debug` → rc 0, `0 Warning(s) 0 Error(s)`.
- **Доказательства.** `.pdca/integration-branch-coverage-11/rv1/attempts/step-1-r2-n2/` — `Summary.txt`,
  `integration.cobertura.xml`, `coverage-run.log/.rc/.tail.log`, `reportgenerator.log`, `build.log`,
  `hashes.txt`, `fixture-retry-accounting.txt` (учёт из процесса измерения), `report.json`,
  `git-status.txt`, `git-diff-stat.txt`, `tracked-changes.diff`, `new-files.diff`, `targeted-runs.txt`,
  UTC-метки.

### Revision r2 (n=1/3) — superseded

**Статус: кандидат возвращён; прогон ЗЕЛЁНЫЙ (`rc=0`, `failed=0`), `S1-NUMERIC` превышена на 33 ветви.**
Вердикт не выносится — его даёт CHECK.

- **Измерение (gate-модель):** `Summary.txt` при `-classfilters:"-NextORM.Core.InMemory*"`:
  branch **38561 / 51773 = 74,48091 %** → **+292 ветви, +0,56400 п.п.** к базе 38269; цель шага 38528
  **превышена на 33**; до 76 % остаётся **787**. line 64153/80309 = 79,88270 % (+115).
  Прогон: **rc=0**, 7871 total / 7674 passed / **0 failed** / 197 skipped / 0 not run, 13 м 02 с.
- **`S1-RELIABILITY` — что удалено, а что повторяется.** Удалён сам зависавший DDL: в
  `ClickHouseDialectBranchCoverage3Tests` больше нет ни per-test `drop table if exists ch3_marker`, ни
  цикла drop/create/seed на каждый факт — вместо этого один `EnsureMarker()` на процесс (static gate +
  lock), потому что все 7 фактов класса только читают. Добавлен узкий fixture-путь
  `tests/Shared/ClickHouseRetry/ClickHouseFixtureRetry.cs`: только setup/cleanup, **1 retry на
  операцию и 1 на сборку**, только уже классифицированные transient-причины, 120 s `HttpClient.Timeout`
  не менялся, нетранзиентные ошибки пробрасываются как есть. Использовано retry: **0**.
  Замечание: строка отчёта `CLICKHOUSE-FIXTURE-RETRY` не попала в захваченный вывод ни полного прогона,
  ни точечного прогона — счётчик 0 следует из устройства (путь просто не исполнялся) и из зелёного
  прогона без застоя; это остаточное замечание к наблюдаемости, а не заявленное доказательство.
- **`S1-INTEGRITY`, дефект 1 (version-gate).** Контекст версии 12.3 теперь готовит probe-таблицу на
  **своём** соединении (`EnsureProbe`) и выполняет положительный контроль (`lengthb` по той же таблице)
  до обращения к gated-имени. Ответ сервера сменился с **1146** (нет таблицы) на **1305** (нет функции) —
  прямое доказательство, что раньше утверждение читало ошибку уровня таблицы.
- **`S1-INTEGRITY`, дефект 2 (capability-refusal).** Теория
  `NamesMariaDbLacks_ShouldBeRefusedByTheCapabilityGate` восстановлена дословно из n=2 поверх
  сохранившейся таблицы `Refusals` — все 14 случаев снова проверяются; `MariaDbNativeFunctions`
  432/604 → **463/604** (+31 к n=3).
- **Числа по классам** (диагностический raw, не модель приёмки: 28787/39266 = 73,31279 %, +292):
  `MySqlFunctionTranslator` **226/278**, `MariaDbNativeFunctions` **463/604**, `MySqlNativeFunctions`
  **805/1130**. По сборкам: core +88, mariadb +201, mysql +3, остальные 0.
- **Сборка.** `dotnet build nextorm.slnx -c Debug` → rc 0, `0 Warning(s) 0 Error(s)`.
- **Доказательства.** `.pdca/integration-branch-coverage-11/rv1/attempts/step-1-r2/` — `Summary.txt`,
  `integration.cobertura.xml`, `coverage-run.log/.rc/.tail.log`, `reportgenerator.log`, `build.log`,
  `hashes.txt`, `report.json`, `git-status.txt`, `git-diff-stat.txt`, `tracked-changes.diff`,
  `new-files.diff`, `targeted-runs.txt`, `probe-classes.*`, `fixture-retry-accounting.log`.

### Attempt 3 (n=3/3) — superseded

**Статус: кандидат возвращён; `S1-NUMERIC` достигнута с запасом 4 ветви, прогон КРАСНЫЙ по одному
pre-existing ClickHouse-застою.** Вердикт не выносится — его даёт CHECK.

- **`S1-NUMERIC` — достигнута.** `Summary.txt` при `-classfilters:"-NextORM.Core.InMemory*"`:
  branch **38532 / 51773 = 74,42489 %**; против базы 38269 — **+263 ветви, +0,50799 п.п.** (порог ≥ +259
  и ≥ 0,5 п.п. выполнен); цель шага 38528 превышена на **4** ветви; до 76 % остаётся **816**.
  Против попытки 2 (38567) — **−35**: цена, которую CHECK потребовал за F-INTEGRITY (рендер-онли кейсы
  убраны из интеграционного кандидата). line 64157 / 80309 = 79,88768 %.
- **Диагностический raw-Cobertura** (не модель приёмки): 28758/39266 = 73,23893 % (+263).
  По сборкам: core +90, mariadb +170, mysql +3, остальные 0. По классам:
  `MySqlFunctionTranslator` 142 → **229/278**, `MariaDbNativeFunctions` 262 → **432/604** (в попытке 2 было
  468 — минус 36 за снятые рендер-онли кейсы), `MySqlNativeFunctions` 802 → **805/1130**.
- **F-INTEGRITY — конвертация рендер-онли кейсов.** `XxhNames_ShouldRenderTheVendorSpelling` → **live**
  `AdvertisedXxhNames_ShouldBeRefusedByThePinnedServer` (исполняет запрос, пинит `select version()` = 11.4
  и утверждает серверный отказ 1305); `VersionGatedNames_…RenderedAboveIt` (рендер) → **live**
  `GatedNames_ShouldRefuseUnversionedAndReachTheServerOnA12_3Context` (unversioned отказ + контекст 12.3
  реально доходит до сервера, отказ 1146); `SequenceNames_ShouldUnquoteOnlyAFullyQuotedToken` (рендер) →
  **live** `SequenceAccess_ShouldAdvanceAndReadTheSessionSequence` (restart, nextval, lastval,
  next value for, setval, nextval). `UnknownName_ShouldBeRefusedByTheRenderer` **удалён**: из ORM
  неизвестное имя до рендерера не доходит, живого сценария нет. Запас проверен до полного прогона
  точечным coverage-прогоном двух классов: `MariaDbNativeFunctions` 382/604 против 357/604 у класса
  попытки 1 в том же режиме (+25), `MySqlFunctionTranslator` 225/278.
- **F-GREEN / F-CAUSE — T320 доказан и изолирован.** `QueryPlanStore` — `internal static class` с
  `private static long _generation` (`src/nextorm.core/DataContext/Cache/QueryPlanStore.cs:29`) и
  `static void Clear() => Interlocked.Increment(ref _generation)` (`:181`); `DataContext.PurgeQueryCache()`
  (`DataContext.cs:1996` → `QueryCache.PurgeQueryCache()`) бампает её, и каждый поток на следующем
  обращении выбрасывает свой plan-dictionary. Замеры (временный probe, удалён после снятия): один
  `PurgeQueryCache()` между двумя prepare детерминированно форкает identity
  (`PROBE single-threaded sameInstance=False`), purger в другом потоке форкает **400/400** итераций.
  `T320SqliteTvfTests` не имел `[Collection]` и утверждает process-wide identity. Фикс: новая
  `[CollectionDefinition("T320 tvf plan identity", DisableParallelization = true)]` + `[Collection]`
  на `T320SqliteTvfTests`, `T320SqlServerTvfTests`, `T320MariaDbTvfTests`, `T320MySqlTvfTests`,
  `T320RuntimeTvfPostgresTests` (ClickHouse-класс остался в уже эксклюзивной ClickHouse-коллекции).
  Проверка: 3 точечных прогона T320-семейства вместе с чистящими кэш классами — 42/42 каждый;
  в полном прогоне T320 не падает.
- **F-TRACKING — тикет rc5 заведён.** `gh` доступен (первый таймаут был транзиентным, повтор с
  `timeout` сработал). Milestone `1.0.9-rc5` существует (номер 23, open). Поиск дублей по `xxh3`,
  `xxh32`, `XXH`, `1305`, `native function`, `advertised but absent`, `function does not exist`:
  покрывающих открытых issues нет (ближайшие — закрытые #280 и #79). Создан **issue #352**
  «MariaDB: xxh3 / xxh32 are advertised but absent on 11.4 (error 1305)»,
  https://github.com/AlexeyShirshov/nextorm/issues/352, milestone **1.0.9-rc5**, OPEN.
  Skip не применён: сценарий остаётся живым и утверждает серверный отказ (сохраняя покрытие).
- **Прогон.** rc **2**, 13 м 02 с: **7857 total / 7659 passed / 1 failed / 197 skipped / 0 not run**.
  Единственное падение — `ClickHouseDialectBranchCoverage3Tests.ScalarFunctions_DialectBranches_ShouldReadLiveValues(fn: "var_pop")`,
  2 м 00 с 165 мс, `HttpClient.Timeout of 120 seconds` в `DropMarker()` → `Execute("drop table …")`
  (`ClickHouseDialectBranchCoverage3Tests.cs:352`). Полоса ClickHouse в этом дереве **эксклюзивна**
  (`DisableParallelization`), т.е. это не межколлекционная конкуренция, а pre-existing застой
  DDL/транспорта; ретраев, skip и расширения таймаутов нет. `CLICKHOUSE-RETRY-SUMMARY … retried_tests=0`.
- **Сборка.** `dotnet build nextorm.slnx -c Debug` → rc 0, `0 Warning(s) 0 Error(s)`.
- **Доказательства.** `.pdca/integration-branch-coverage-11/rv1/attempts/step-1-n3/` — `Summary.txt`,
  `integration.cobertura.xml`, `coverage-run.log/.rc/.tail.log`, `reportgenerator.log`, `build.log`,
  `hashes.txt`, `report.json`, `git-status.txt`, `git-diff-stat.txt`, `tracked-test-changes.diff`,
  `new-test-files.diff`, `clickhouse-collections.txt` (все `CollectionDefinition`), `t320-evidence.txt`,
  `t320-repro.txt`, `t320-probe.txt`, `xxh-1305-repro.txt`, `github.txt`, `probes-n3-classes.cobertura.xml`.

### Attempt 2 (n=2/3) — superseded

**Статус: кандидат возвращён, прогон КРАСНЫЙ по одному pre-existing тесту; числовая цель шага достигнута.**
Вердикт не выносится — его даёт CHECK.

- **S1-NUMERIC — достигнута на gate-модели.** `Summary.txt` при `-classfilters:"-NextORM.Core.InMemory*"`:
  branch **38567 / 51773 = 74,49250 %**; против базы 38269 — **+298 ветвей, +0,57559 п.п.**; против
  попытки 1 (38499) — **+68**; цель шага 38528 **превышена на 39**; до 76 % остаётся **781** ветвь.
  line 64153 / 80309 = 79,88270 % (+115 к базе, +44 к попытке 1).
- **Что дало прирост** (диагностический raw-Cobertura, не модель приёмки: 28793/39266 = 73,32807 %,
  +298): `NextORM.Core.MySqlFunctionTranslator` 142/278 → **226/278** (+84): добавлены translate-time
  проверки для JSON-грамматики (`JSON_OBJECT` нечётная арность, `one_or_all`, нелитеральные пути,
  escape, `MEMBER OF`), `GET_FORMAT` (неизвестный формат, неконстантный kind, все три keyword-арма),
  `NAME_CONST` (null и неконстантное имя), `weight_string` (неконстантный cast/length, нулевая длина,
  BINARY-арм), `DEFAULT(col)`, `EXPORT_SET` (бит-каунт 65), минимальная арность `field`/`make_set` и
  MariaDB-only `column_*`/`json_object_filter_keys`, а также `->` (литерал слева, неконстантный путь).
  `MariaDbNativeFunctions` 262/604 → **468/604** (+206; +11 к попытке 1): оставшиеся MariaDB-only имена
  (`nvl`, `nvl2`, `add_months`, `to_char`, `regexp_substr/instr`, `json_detailed/compact/equals/normalize`),
  рендер `xxh3`/`xxh32`, отказ неизвестного имени в `Render`, `RenderGated` с конкретной версией 11.4 и
  `SequenceName` (unquoted / quoted / escaped quote). `MySqlNativeFunctions` 802/1130 → 806/1130 (+4).
  По сборкам: core +88, mariadb +206, mysql +4, остальные без изменений.
- **ClickHouse — зелёный в этом прогоне.** Ни одного ClickHouse-падения: `Code 216` не повторился, оба
  baseline-таймаута 120 с отсутствуют; `CLICKHOUSE-RETRY-SUMMARY … retried_tests=0` (ретраев не было).
  Исправление: `[CollectionDefinition("ClickHouse", DisableParallelization = true)]` в
  `Support/ClickHouseCollection.cs` — ClickHouse-полоса теперь эксклюзивна, а не только сериализована
  сама с собой (обе документированные причины отказа межколлекционные: коллизия query id и насыщение
  connection relay, кончающееся 120-секундным `HttpClient.Timeout`), плюс `[Collection("ClickHouse")]`
  для `CrossProvider/ProviderCapabilityContractTests`, у которого атрибута не было вовсе. Три класса,
  названные CHECK, **уже** изолированы своими `[CollectionDefinition(…, DisableParallelization = true)]`
  (`T327 array rewind cache`, `T282 builtin catalog`, `T300 scalar derived source`) — их атрибуты не
  менялись, иначе их защита от гонки plan-cache была бы ослаблена; строки атрибутов приложены в evidence.
- **S1-GREEN — НЕ достигнута, причина вне кластера шага.** Прогон: 7875 total / 7677 passed /
  **1 failed** / 197 skipped / 0 not run (7830 + 45 новых кейсов = 7875). Единственное падение —
  `T320SqliteTvfTests.FromOptions_OnATvfSource_ShouldBeNeutralOrFailClosedWithLiveRows` (13 с 423 мс):
  `ReferenceEquals(hintedPrepared, plainPrepared)` ложно, т.е. гонка identity plan-cache. Класс **не
  имеет** `[Collection]` (T320SqliteTvfTests.cs:74) и утверждает process-wide identity, поэтому любой
  параллельный `PurgeQueryCache` из другой коллекции его ломает; это pre-existing гонка, которую
  проявило изменение расписания в этой попытке. Готовый фикс — дать T320 TVF identity-классам
  `DisableParallelization`-коллекцию, как уже сделано для `Query plan cache` и `T327 array rewind cache`;
  в этой попытке он **не применён**, потому что брифинг разрешает один полный прогон (дерево под
  измерением = отчитываемое дерево).
- **Сборка.** `dotnet build nextorm.slnx -c Debug` → rc 0, `0 Warning(s) 0 Error(s)`.
- **Прогон.** rc **2**, wall 15 м 07 с (08:38:33Z–08:53:40Z), suite 15m 01s 767ms; `NEXTORM_SKIP_PROVIDERS`
  не задавался, 197 skips — capability-бюджет.
- **Новое наблюдение (продукт).** `MariaDbNativeFunctions` рекламирует `xxh3`/`xxh32` для закреплённого
  MariaDB 11.4, а сервер отвечает `1305 FUNCTION … does not exist` для обоих. Тикет не заводился:
  `gh auth status` падает по таймауту логина, поэтому ни поиск дублей, ни issue с milestone rc5
  выполнить нельзя, а `prep@r3` требует именно проверенный тикет. Рендер-армы покрыты проверкой
  отрендеренного текста, исполнение не утверждается.
- **Доказательства.** `.pdca/integration-branch-coverage-11/rv1/attempts/step-1-n2/` — `Summary.txt`,
  `integration.cobertura.xml` (sha256 в `hashes.txt`), `coverage-run.log/.rc/.tail.log`,
  `reportgenerator.log`, `build.log`, `git-status.txt`, `git-diff-stat.txt`,
  `tracked-test-changes.diff`, `new-test-files.diff`, `clickhouse-collections.txt`, `report.json`,
  UTC-метки.

### Attempt 1 (n=1/3) — superseded

**Статус: кандидат возвращён, прогон КРАСНЫЙ.** Вердикт не выносится — его даёт CHECK.

- **Выполнено.** Добавлены два интеграционных класса на реальных СУБД, покрывающих весь native-function
  surface обоих провайдеров:
  `tests/nextorm.integration.tests/MariaDb/MariaDbNativeFunctionLiveTests.cs` (52 кейса) и
  `tests/nextorm.integration.tests/MySql/MySqlNativeFunctionLiveTests.cs` (113 кейсов). Протокол один:
  значение, которое возвращает отрендеренный вызов, сравнивается со значением, которое тот же вендорный
  вызов вернул серверу в той же сессии; тип результата проверяется отдельно. Плюс содержательные
  проверки поведения: отказ capability-gate для 14 имён, которых у MariaDB нет; version-gate
  `months_between`/`to_date`/`to_number` (unversioned отказ, 12.3 — рендер); `binlog_gtid_pos` → ошибка
  1381 на сервере без binlog; `natural_sort_key` по порядку; `sys_guid`/`rand`/`random_bytes`/`uuid_short`
  по форме; session-ридеры по состоянию сессии.
- **Число (заявка шага, gate-модель).** `Summary.txt` при `-classfilters:"-NextORM.Core.InMemory*"`:
  branch **38499 / 51773 = 74,36115 %** (было 38269 / 51773 = 73,91691 %) → **+230 ветвей, +0,44425 п.п.**;
  line 64109 / 80309 = 79,82791 % (+71 строка, +0,08841 п.п.). Цель шага 38528 → **недостача 29 ветвей**;
  до 76 % остаётся **849** ветвей.
- **Диагностический вид (raw Cobertura, не модель приёмки).** 28725/39266 = 73,15489 % против
  28495/39266 = 72,56914 % → те же +230. По сборкам: mariadb 275→470 (+195), core 21153→21185 (+32),
  mysql 1354→1357 (+3), остальные без изменений. По классам: `MariaDbNativeFunctions` 262/604 → **457/604**
  (+195), `MySqlNativeFunctions` 802/1130 → **805/1130** (+3).
- **Ключевой вывод.** `MySqlNativeFunctions` уже был на пределе: при 100 % line coverage исполнение всех
  112 имён даёт всего +3 ветви, потому что остальные 325 branch-точек — половины compiler-generated
  string-switch на `MySqlDialect.cs:828`/`:857`, недостижимые исполнением. MariaDB дала +195 именно
  потому, что класс раньше не исполнялся ни одним прогоном (все его имена жили только в
  `Category=Diagnostic`-классе, который измерение исключает). Потолок name-driven теста для пары
  классов ≈ +230, а не 670 из gap-map.
- **ClickHouse.** Оба baseline-таймаута (`ClickHousePlanKeyBranchCoverage5Tests.Settings_…`,
  `Cycle7ClickHouseDialectTests.DictGet_…`) в этом прогоне **прошли**; прогон всё равно красный по
  другой причине: `T288NullSafeComparisonClickHouseIntegrationTests.T288_ClickHouse_CapturedNullThenValue_ShouldRebindWithoutValueDependentSql`
  упал за 573 мс с серверной ошибкой `Code: 216 QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING`. Решение по
  надёжности зафиксировано как блокер `CH-COLLECTION` (см. `Blockers`): четыре тест-класса выходят на
  общий ClickHouse-сервер из чужих коллекций и работают параллельно с коллекцией `ClickHouse`, вопреки
  конвенции в `Providers/ClickHouseContainer.cs`; nextorm не задаёт query id нигде, значит коллизия —
  следствие двух параллельных одинаковых statement'ов. Подключение к `ClickHouseReadRetry` отвергнуто:
  `tests/Shared/ClickHouseRetry/**` вне разрешённой поверхности, лимит «одна retry-операция на тест» при
  4–6 чтениях на факт, а падение DictGet — в DDL конструктора, которое `Audited` отвергает по построению
  (`ClickHouseReadCategory.Ddl`), и ретрай не лечит 216. Таймауты не расширялись, skip не применялся,
  тикет не заводился (продуктовый дефект не установлен).
- **Сборка.** `dotnet build nextorm.slnx -c Debug` → rc 0, `0 Warning(s) 0 Error(s)`.
- **Прогон.** Команда baseline дословно, rc **2**, wall 21 м 11 с, suite 21m 05s 418ms:
  **7830 total / 7632 passed / 1 failed / 197 skipped / 0 not run** (7665 + 52 + 113 = 7830 — ровно новые
  кейсы). Провайдеры не отключались: `NEXTORM_SKIP_PROVIDERS` не задавался, 197 skips — capability-бюджет.
- **Доказательства.** `.pdca/integration-branch-coverage-11/rv1/attempts/step-1/` — `Summary.txt`,
  `integration.cobertura.xml` (sha256 `60797e1d…`), `coverage-run.log`, `coverage-run.rc`,
  `coverage-run.tail.log`, `reportgenerator.log`, `build.log`, `hashes.txt`, `report.json`,
  UTC-метки, `probes/` (два точечных прогона: MariaDB-класс и T349).

#### CHECK record

**CHECK выполнялся — три независимых прогона, все вернули `not accepted`** (source: `pdca-check`, medium
tier; связки переданы драйвером). Полные оригиналы отчётов CHECK в ACT **не переданы** — записи ниже
помечены как пересказ драйвера, а не как отчёт CHECK; недостающий источник фиксируется как
отсутствующий и не реконструируется.

- **CHECK после попытки 1 (`n=1/3`)** — `not accepted`: `S1-NUMERIC` не достигнута (38499/51773, на 29
  ветвей меньше цели), `S1-GREEN` красный (`Code 216 QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING` в
  `T288NullSafeComparisonClickHouseIntegrationTests`), `S1-INTEGRITY` не проверяема — не было диффов и
  строк коллекций. Evidence: `rv1/attempts/step-1/`.
- **CHECK после попытки 2 (`n=2/3`)** — `not accepted`: `S1-INTEGRITY` — рендер-онли кейсы в
  `new-test-files.diff:244-289` нарушают интеграционный контракт; `F-TRACKING` — обязательный тикет rc5
  не заведён (в n=2 GitHub был недоступен); `S1-GREEN` красный — новая, не связанная с кластером гонка
  `T320SqliteTvfTests` (plan-cache identity). Evidence: `rv1/attempts/step-1-n2/`.
- **CHECK после попытки 3 (`n=3/3`)** — `not accepted`: `S1-NUMERIC` достигнута (38532/51773 = 74,42489 %,
  цель превышена на 4 ветви), но `S1-GREEN` красный — один ClickHouse DDL-застой 120 с в эксклюзивной
  полосе; остаются два открытых дефекта по `S1-INTEGRITY` (см. остаточные обязательства в
  `## Campaign outcome`). Evidence: `rv1/attempts/step-1-n3/`.

#### ACT record

**Revision r2 (n=3/3) — Outcome: `not accepted` · credit: no credit · красный кандидат сохранён.**
ACT записывает то, что определили экспертиза и третий CHECK; вердикта не выносит и ничего не реализует.

- **Причина.** Бюджет попыток ревизии r2 исчерпан: три содержательных CHECK по r2, все `not accepted`;
  дефект самих критериев не установлен. Экспертиза, ставшая доступной, рекомендовала **закрыть r2**, а
  не идти четвёртым проходом.
- **Критерии по ревизии r2.**
  - `S1-NUMERIC` — **met** на последнем некредитованном измерении: **38561/51773 = 74,48091 %**,
    **+292** ветви к базе, цель шага 38528 превышена на **33**.
  - `S1-INTEGRITY` — **met** (оба дефекта r1 устранены и подтверждены: version-gate 1146 → 1305 через
    probe-таблицу на своём соединении и положительный контроль; восстановленная 14-кейсовая теория).
  - `S1-RELIABILITY` — **partially met**. `F-R2-OBS` закрыт (учёт из процесса измерения с `pid`,
    `run_id`, `process_touch_utc`, UTC-метками и шапкой PROVENANCE, mtime внутри интервала);
    `F-R2-IDEMPOTENCE` закрыт (`ReplacingMergeTree order by (id)` + чтения `.Final()`); **`F-R2-STATE`
    остаётся открытым**: фиксированное имя `ch3_marker_v2` с `IF NOT EXISTS` не проверяет
    существующую схему и содержимое — проверяется только набор id `{1,2,3,4}`.
  - `S1-GREEN` — **unmet** на финальном прогоне r2/n3 (`rc=2`).
- **Корректировка прежней формулировки (находка экспертизы).** Утверждение «`S1-GREEN` красный в каждом
  прогоне» **неверно** и заменяется точной записью: прогоны **r2/n1 и r2/n2 были `rc=0` (зелёные)** и
  не были приняты из-за находок по `S1-INTEGRITY` / `S1-RELIABILITY`, а не из-за красного прогона.
  Кандидат r2/n3 — другое состояние кода, поэтому те зелёные результаты на него не переносятся.
- **Остаётся (`unmet`/`unverified`).** `S1-GREEN` (финальный прогон красный) и `F-R2-STATE`
  (`S1-RELIABILITY` незавершён).
- **Ограничения.** Дефект `CH-NONQUERY-STALL` не устранён: устранение DDL по классам **перенесло** застой,
  а не убрало его. Консультация экспертизы выполнена; четвёртая попытка r2 запрещена.
- **Evidence.** `.pdca/integration-branch-coverage-11/rv1/attempts/step-1-r2/` (n=1, `Summary.txt`,
  `coverage-run.rc` = 0), `…/step-1-r2-n2/` (n=2, `rc` = 0), `…/step-1-r2-n3/` (n=3, `rc` = 2,
  `fixture-retry-accounting.txt`, `provenance.txt`, `measurement-command.txt`, `report.json`).
- **Handoff.** Любая будущая ревизия `r3` требует сначала проверки гипотезы «серверная конкуренция, а не
  форма теста» (см. `## Campaign outcome`) и свежей авторизации пользователя на изменение критерия
  зелёности; иначе STOP остаётся правильным исходом.

### Revision r1 (history, superseded)

**Outcome: `not accepted` · credit: no credit · красный результат сохранён.** ACT записывает то, что
определила завершающая фаза; вердикта не выносит и ничего не реализует.

- **Причина.** Три содержательных CHECK подряд вернули `not accepted`. *(Корректировка, внесённая при
  закрытии r2: формулировка «красный в каждом прогоне» относилась к прогонам **r1**; зелёными были
  r2/n1 и r2/n2. Ниже — как было записано по r1.)* В прогонах r1 `S1-GREEN` был красным, и каждый раз
  в ClickHouse-полосе: `n=1` — `Code 216 QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING`
  (`T288…CapturedNullThenValue…`), `n=2` — гонка plan-cache `T320SqliteTvfTests`, `n=3` — 120-секундный
  DDL-застой в `ClickHouseDialectBranchCoverage3Tests.DropMarker`. `S1-INTEGRITY` остаётся с двумя
  открытыми дефектами качества кандидата (12.3-version-gate тест и потерянное 14-кейсовое
  capability-refusal покрытие, см. остаточные обязательства ниже).
- **Числовой результат.** `S1-NUMERIC` достигнута на последнем (некредитованном) измерении:
  38532/51773 = 74,42489 %, +263 ветви к базе 38269 (+0,50799 п.п.), цель шага 38528 превышена на 4;
  до 76 % остаётся 816 ветвей. Инкремент **не принят** и базой кампании не становится.
- **Что сделано и остаётся.** Работа и её доказательства сохранены: два новых интеграционных класса
  (`MariaDbNativeFunctionLiveTests`, `MySqlNativeFunctionLiveTests`), коллекционные фиксы
  (`ClickHouse` эксклюзивна, `T320 tvf plan identity`), тикет **#352** (rc5). Остаётся (`unmet` /
  `unverified`): зелёный полный прогон (`S1-GREEN`), два остаточных обязательства по `S1-INTEGRITY`,
  доказательность ClickHouse-застоя.
- **Ограничения.** Бюджет попыток шага исчерпан — plan revision `r1`, `n=3/3`; четвёртая попытка
  запрещена. Выплата экспертизы невозможна: `pdca-escalate` (strong tier) не запускается —
  ресурсный блокер **`Missing API key`**; экспертного мнения по существу не существует.
  Планировщик отказался открывать новый шаг или менять контракт: это была бы замаскированная
  четвёртая попытка.
- **Критерии.** `S1-NUMERIC` — met (последнее измерение); `S1-GREEN` — unmet (красный каждый прогон);
  `S1-INTEGRITY` — unmet (два открытых дефекта).
- **Evidence.** `.pdca/integration-branch-coverage-11/rv1/attempts/step-1/`,
  `…/step-1-n2/`, `…/step-1-n3/` (Summary, cobertura, логи прогонов с rc, диффы, `report.json`,
  `github.txt`, T320- и XXH-доказательства).
- **Handoff.** Будущая сессия: сначала снять блокер эскалации (получить рабочий strong-tier ключ) или
  получить **явное указание пользователя, меняющее контракт**; затем войти заново в PLAN. Непринятый
  кандидат, все тесты и все доказательства остаются на месте — ничего не удаляется и не откатывается.

#### PLAN record (r2)

r2 — `integration-branch-coverage-11/step-1@plan-r2`, получен от `pdca-planner` после завершённой
эскалации и пользовательской инструкции. Встроен дословно, без изменений; запись r1 выше сохранена
рядом и не тронута.

````markdown
## STEP 1 — контракт r2

### GOAL

**Исходная задача:** «pdca необходимо довести бранч покрытие интеграционных тестов до 76%».

Запрошенный и конечный передаваемый результат неизменны: **интеграционное branch coverage ≥76%**.

**Решение:** принять рекомендацию завершившейся эскалации и выдать **r2 того же step 1**, не новый шаг. Форма `incremental`, специализация `pdca-dotnet` / `coder+.NET` сохраняются по прежнему обоснованию.

Основание пользователя:

> «ключ для стронг тира починил, правку разрешаю, можно перезапускать»

**Содержательное изменение контракта:** помимо числового прироста и корректных интеграционных проверок результат теперь включает ограниченный, наблюдаемый механизм надёжности fixture setup/cleanup, отделённый от операции продукта под проверкой. Простое расширение списка файлов или смена реализации сами по себе ревизию не оправдали бы.

### CURRENT STATE

- Принятый baseline: **38269/51773**.
- Последний непринятый результат: **38532/51773**, **+263 ветви**.
- Step 1 r1 завершён на **n=3/3**, все CHECK — `not accepted`. Этот результат не превращается в принятый.
- Сохранённый кандидат можно использовать как исходный материал r2, **не как новый принятый baseline**. Приёмка требует актуального измерения после исправлений.
- Прежний блокер эскалации устранён; предыдущий STOP и сообщение `Missing API key` остаются в истории. Теперь имеется экспертная рекомендация r2.
- Незакрытые integrity-дефекты и ClickHouse cleanup-stall остаются обязательными ограничениями приёмки.

**Учёт:** executor регистрирует r2; позиция следующей попытки — **r2, n=1/3**. Planner сам счётчики не изменяет. История r1, ключи дефектов, applied-fix counts и отрицательные исходы сохраняются.

Это не четвёртая попытка r1: разрешён изменённый контракт после новой инструкции и эскалации. Однако новая ревизия **не обнуляет историю повторяемого дефекта** и не гарантирует три дополнительных попытки.

### HARD CONSTRAINTS

**Разрешённая поверхность:**

- `tests/nextorm.integration.tests/**`;
- **`tests/Shared/ClickHouseRetry/**`** — только необходимое обеспечение fixture-надёжности и его контрактных проверок;
- существующие статусные записи и материалы этой кампании в `.pdca/`.

Расширение необходимо потому, что общий retry-механизм находится вне прежней поверхности. Оно **не разрешает** менять production-код, другие shared-компоненты, workflow, coverage-фильтры, пороги или `tools/**`.

Обязательные ограничения нового fixture-пути:

- доступен только setup/cleanup; операция продукта под проверкой через него не проходит;
- повторяемые действия идемпотентны и изолированы между тестами;
- разрешены только уже классифицированные transient-причины;
- глобальное запрещение DDL в обычном read-retry пути не ослабляется;
- `HttpClient.Timeout` **120 s не увеличивается**;
- assertion failures и ошибки операции под проверкой не превращаются в успех.

**Лимит нового fixture-пути:** максимум **один retry на операцию** и **один retry суммарно за полную интеграционную сборку**. Это консервативный бюджет на единичный transient, а не разрешение многократно пережидать структурную неисправность. Все использованные retries учитываются и сообщаются; превышение бюджета — отрицательный результат.

Сохраняются реальные СУБД, FluentAssertions, Roslyn, CRLF, warnings-as-errors, запреты commit/push/merge.

Правило пользователя о дефектах остаётся неизменным:

> «если обнаружена ошибка в продукте, необходимо открыть тикет в rc5 и продолжать работу. тест при этом можно скипнуть со ссылкой на открытывй тикет»

Оно применяется согласно `prep@r3`, не к транспортной нестабильности. Issue **#352** не оправдывает skips несвязанных ClickHouse-проверок.

### SCOPE

Результат r2 охватывает:

- сохранённый содержательный прирост MariaDB/MySQL native-function интеграционных проверок;
- надёжность fixture lifecycle на общем ClickHouse-сервере без маскирования проверяемого поведения;
- устранение обоих integrity-дефектов.

Обязательные свойства исправленного результата:

1. **Version-gate:** ошибка отсутствующей временной таблицы не может быть принята за отсутствие функции. Предусловия обращения к таблице действительны для используемого соединения.
2. **Capability-refusal:** сохранён эквивалентный содержательный охват **всех 14 случаев** исчезнувшей theory.
3. **Fixture reliability:** зависающее setup/cleanup не скрывается глобальной retry-политикой. Устранение необходимости проблемного DDL предпочтительнее добавления повторов; конкретную реализацию выбирает DO.

Новые coverage-кластеры, production-fixes и общий рефакторинг shared-инфраструктуры **не входят**. Дополнительные проверки в текущем кластере нужны лишь постольку, поскольку итог после исправлений иначе не достигает прежнего порога.

### ACCEPTANCE

**S1-NUMERIC — без изменения.**
Точное gate-покрытие превышает baseline минимум на **0,5 процентного пункта**. При `D=51773`: **C≥38528**, минимум **+259 ветвей**.

Модель: учитываемые ветви восьми принятых сборок, действующее исключение `NextORM.Core.InMemory*`, без raw-Cobertura подмены. При изменении знаменателя сохраняется требование:

`C/D ≥ 38269/51773 + 0,005`

Сопоставимость требует неизменных условий включения/исключения; изменение метода измерения не оформляется этой ревизией.

**S1-GREEN.**
Полноценная интеграционная сборка успешна: **rc=0, failed=0**, все предусмотренные провайдеры участвуют, opt-outs отсутствуют. Допустимы только штатные skips и исключение `prep@r3`.

**S1-INTEGRITY.**
Оба указанных integrity-дефекта устранены; реальные результаты БД, корректные предпосылки и отрицательные capability-контракты не подменены более слабыми проверками.

**S1-RELIABILITY — новое обязательство r2.**
Fixture setup/cleanup отделён от операции под проверкой, удовлетворяет ограничениям выше; retries наблюдаемы, сообщены и укладываются в установленный бюджет. Исчерпание retry-лимита остаётся ошибкой.

**Не добавляется критерий «targeted checks обязательно раньше полного запуска».** Это последовательность проверки, которую выбирают DO/CHECK, а не свойство принимаемого результата.

### TARGETS И ОСТАНОВКА

- **Target step 1:** прежний — **≥38528/51773** при неизменном знаменателе.
- **Конечный target:** **≥39348/51773**; 816 ветвей остаются лишь диагностическим остатком от непринятого результата.
- Дополнительный прирост сверх уже измеренных +263 не назначается обязательным: необходимо сделать весь кандидат приемлемым, а не повысить цель задним числом.

**Повторный cleanup-stall после ограниченных retries означает STOP по same-defect guard**, не ещё одну попытку исправления того же механизма. Наличие свободных позиций n=2/3 или n=3/3 это правило не отменяет. Неуспех r2 не является основанием для автоматического r3 или нового шага.

При положительном независимом CHECK принимается именно step 1; завершение всей кампании требует отдельного подтверждения всех конечных критериев. **Уверенность средняя:** контракт определён, но устранение структурной нестабильности пока не доказано.

Полный этот отчёт передаётся executor без пересказа; DO получает его вместе с неизменными требованиями кампании.
````

#### Revision history

r1 — step 1 brief from the step-boundary call after bootstrap acceptance

r2 — genuine contract revision after the user's instruction «ключ для стронг тира починил, правку разрешаю, можно перезапускать» and the expert disposition; surface widened to tests/Shared/ClickHouseRetry/** (fixture reliability only); r1 history and defect keys retained

#### Defect history

| key | observed | fix applied | evidence | outcome |
|---|---|---|---|---|
| F1-NUMERIC | CHECK n=1: 38499/51773, на 29 ветвей меньше цели 38528 | добавлены translate-time и native-function кейсы; `MySqlFunctionTranslator` 142→226/278, `MariaDbNativeFunctions` 262→468/604 | `attempts/step-1-n2/Summary.txt` (38567/51773) | **fixed** (+298 к базе, цель превышена на 39) |
| F2-CLICKHOUSE | CHECK n=1: red `rc=2`, `Code 216 QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING` в `T288…CapturedNullThenValue…` | `ClickHouse`-коллекция стала `DisableParallelization = true`; `ProviderCapabilityContractTests` получил `[Collection("ClickHouse")]`; три уже изолированных класса не тронуты | `attempts/step-1-n2/clickhouse-collections.txt`; в `coverage-run.log` нет ClickHouse-падений, `retried_tests=0` | **fixed** для ClickHouse; проявилась другая pre-existing гонка (F3b) |
| F3-EVIDENCE | CHECK n=1: S1-INTEGRITY/Q2/Q5 не проверяемы — не было диффов и атрибутов коллекций | в `attempts/step-1-n2/` положены `git-status.txt`, `git-diff-stat.txt`, `tracked-test-changes.diff`, `new-test-files.diff` (полный текст новых файлов), `clickhouse-collections.txt` | те же файлы | **fixed** |
| F3b-PLANCACHE | n=2: `T320SqliteTvfTests…FromOptions…` — `ReferenceEquals` ложно (identity plan-cache) | доказан пробой (purge между prepare ломает identity: single-threaded `sameInstance=False`, concurrent 400/400) и изолирован коллекцией `T320 tvf plan identity` с `DisableParallelization` | `attempts/step-1-n3/t320-evidence.txt`; полный прогон без T320-падений | **fixed** |
| F-INTEGRITY | CHECK n=2: рендер-онли кейсы (`Render`/message без обращения к серверу) в `new-test-files.diff:244-289` | три кейса сконвертированы в живые (xxh → серверный 1305; version-gate → исполнение на 12.3-контексте → 1146; sequence → live restart/nextval/lastval/setval), один удалён (unknown-name недостижим из ORM) | `tests/nextorm.integration.tests/MariaDb/MariaDbNativeFunctionLiveTests.cs`; цена — 36 ветвей `MariaDbNativeFunctions` | **fixed** |
| F-TRACKING | CHECK n=2: rc5-тикет обязателен, а в n=2 GitHub был недоступен | повтор с `timeout`: `gh` доступен, milestone `1.0.9-rc5` (23) проверен, дубли исключены, создан **#352** | `attempts/step-1-n3/github.txt`; https://github.com/AlexeyShirshov/nextorm/issues/352 | **fixed** |
| R2-INTEGRITY-VGATE | CHECK r1: версия-гейт тест мог читать ошибку отсутствующей таблицы (1146) вместо отсутствия функции | probe-таблица на соединении версионного контекста + положительный контроль; ответ сервера 1146 → **1305**, что и доказывает исправление | `tests/nextorm.integration.tests/MariaDb/MariaDbNativeFunctionLiveTests.cs` | **fixed** |
| R2-INTEGRITY-REFUSAL | CHECK r1: потеряно покрытие 14 кейсов capability-refusal между n2 и n3 | теория `NamesMariaDbLacks_ShouldBeRefusedByTheCapabilityGate` восстановлена дословно; `MariaDbNativeFunctions` 432→463/604 | `attempts/step-1-r2/Summary.txt` | **fixed** |
| F-R2-OBS | CHECK r2: `retries_used = 0` недоказуем — зелёный прогон совместим с одним успешным retry | файловый учёт из процесса измерения (`ProcessExit` + построчная запись), путь из env-переменной; в evidence лежит файл с одной попыткой и итогом `retries_used=0` | `attempts/step-1-r2-n2/fixture-retry-accounting.txt` | **fixed** |
| F-R2-IDEMPOTENCE | CHECK r2: `create table` без `IF NOT EXISTS`, сид мог задвоиться при потерянном ответе | `create … if not exists`; `drop` удалён; сид под `select count()` (0 → вставить, 4 → готово, иначе исключение) внутри одной retry-операции | `tests/nextorm.integration.tests/ClickHouse/ClickHouseDialectBranchCoverage3Tests.cs` | **fixed** |
| F-R2-OBS-R2 | CHECK r2: артефакт учёта не привязан к прогону | формат учёта несёт UTC, `pid`, `run_id`, `process_touch_utc` и шапку PROVENANCE; mtime внутри интервала измерения; sha256 и точная команда в evidence | `attempts/step-1-r2-n3/provenance.txt` ; `measurement-command.txt` | **fixed** |
| F-R2-IDEMPOTENCE-R2 | CHECK r2: guard `count()`→`INSERT` не покрывает неизвестный исход первой вставки | `ReplacingMergeTree order by (id)` + чтения `.Final()`: повторная вставка тех же id не видна; `count()` — только быстрый путь | `tests/nextorm.integration.tests/ClickHouse/ClickHouseDialectBranchCoverage3Tests.cs` | **fixed** |
| F-R2-STATE | CHECK r2: изоляция остающейся таблицы | имя `ch3_marker_v2` фиксирует схему (старая `Memory`-таблица не наследуется и не читается), жизненный цикл — create-once per process, владелец — класс; фикстура утверждает точный набор id через `FINAL` | тот же файл | **fixed** |
| CH-NONQUERY-STALL | ClickHouse 120 s `HttpClient.Timeout` на non-query / fixture-выражениях: baseline (2×), r1/n3 (`DropMarker()` → `drop table`), r2/n3 (`ClickHouseT291AverageLiveTests.EmptySetAverage_ShouldBeNull` → `ExecuteNonQuery` → `CreateFixture`) | устранение DDL по классам (create-once-per-process + `ReplacingMergeTree` + `FINAL`) — **перенесло** застой, а не убрало его; **исправление атрибуции**: прежнее отнесение падения r2/n3 к «чтению» было неверным — CHECK установил `ExecuteNonQuery` в `CreateFixture` | `attempts/step-1-r2-n3/coverage-run.log:5` ; `attempts/step-1/coverage-run.log` (2× baseline) | **open** — тот же механизм, same-defect guard |
| R2-RELIABILITY | CHECK r1: ClickHouse cleanup-stall 120 s | удалён сам cleanup DDL (один `EnsureMarker()` на процесс) + узкий fixture-retry (1/операцию, 1/сборку), использовано 0; 120 s не менялись | `tests/Shared/ClickHouseRetry/ClickHouseFixtureRetry.cs`; `attempts/step-1-r2/coverage-run.log` (rc=0) | **fixed** (наблюдаемость строки — остаточное замечание) |
| F-GREEN | n=2/n=3: прогон красный | n=2: T320 (исправлен в n=3); n=3: один ClickHouse DDL-застой 120 с в эксклюзивной полосе | `attempts/step-1-n3/coverage-run.log:11` | **open** — вне разрешённой поверхности |

#### Changed files

- `tests/nextorm.integration.tests/MariaDb/MariaDbNativeFunctionLiveTests.cs` (новый; 52 кейса в попытке 1, 73 в попытке 2)
- `tests/nextorm.integration.tests/MySql/MySqlNativeFunctionLiveTests.cs` (новый; 113 кейсов в попытке 1, 137 в попытке 2)
- `tests/nextorm.integration.tests/Support/ClickHouseCollection.cs` (изменён: `DisableParallelization = true` у коллекции `ClickHouse`)
- `tests/nextorm.integration.tests/CrossProvider/ProviderCapabilityContractTests.cs` (изменён: добавлен `[Collection("ClickHouse")]`)
- `tests/nextorm.integration.tests/T320/T320TvfPlanIdentityCollection.cs` (новый: `CollectionDefinition(…, DisableParallelization = true)`)
- `tests/Shared/ClickHouseRetry/ClickHouseFixtureRetry.cs` (новый, r2: fixture-только retry, 1/операцию и 1/сборку)
- `tests/nextorm.integration.tests/ClickHouse/ClickHouseDialectBranchCoverage3Tests.cs` (изменён, r2: удалён per-test cleanup DDL, один `EnsureMarker()` на процесс)
- `tests/nextorm.integration.tests/Support/ClickHouseRetryReporter.cs` (изменён, r2: строка учёта fixture-retry)
- `tests/Shared/ClickHouseRetry/ClickHouseFixtureRetry.cs` (изменён, r2 n=2: файловый учёт попыток и итога через `ProcessExit`)
- `tests/nextorm.integration.tests/ClickHouse/ClickHouseDialectBranchCoverage3Tests.cs` (изменён, r2 n=2/n=3: `ch3_marker_v2` + `ReplacingMergeTree order by (id)`, чтения через `.Final()`, сид с проверкой точного набора id)
- `tests/nextorm.integration.tests/T320/T320SqliteTvfTests.cs`, `T320SqlServerTvfTests.cs`, `T320MariaDbTvfTests.cs`, `T320MySqlTvfTests.cs`, `T320RuntimeTvfPostgresTests.cs` (изменены: `[Collection("T320 tvf plan identity")]`)
- `.pdca/integration-branch-coverage-11/prep-r3.md` (новая ревизия preparation, supersedes r2)
- `.pdca/status/integration-branch-coverage-11.md` (этот файл)
- `.pdca/integration-branch-coverage-11/rv1/attempts/step-1/**` (evidence прогона)

Продуктовый код `src/**`, workflow, пороги и исключения не менялись.

#### Git state

status path: `.pdca/status/integration-branch-coverage-11.md` — modified this cycle · untracked (durable on disk, not committed); `git status --porcelain` на момент DO (попытка 2): `?? .pdca/status/integration-branch-coverage-11.md`, `?? tests/nextorm.integration.tests/MariaDb/MariaDbNativeFunctionLiveTests.cs`, `?? tests/nextorm.integration.tests/MySql/MySqlNativeFunctionLiveTests.cs`, ` M tests/nextorm.integration.tests/Support/ClickHouseCollection.cs`, ` M tests/nextorm.integration.tests/CrossProvider/ProviderCapabilityContractTests.cs`, `?? tests/nextorm.integration.tests/T320/T320TvfPlanIdentityCollection.cs`, ` M tests/nextorm.integration.tests/T320/T320SqliteTvfTests.cs`, ` M tests/nextorm.integration.tests/T320/T320SqlServerTvfTests.cs`, ` M tests/nextorm.integration.tests/T320/T320MariaDbTvfTests.cs`, ` M tests/nextorm.integration.tests/T320/T320MySqlTvfTests.cs`, ` M tests/nextorm.integration.tests/T320/T320RuntimeTvfPostgresTests.cs`; HEAD `29fdcf89`, baseline отсутствует в брифе — атрибуция по факту; no `git add`, no commit, no push, no merge; продуктовые файлы не менялись

#### Progress log

Единый журнал кампании — `## Progress log` этого файла (campaign `integration-branch-coverage-11`, cycle N=11, Step 1, r1, n=2/3). Отдельная хронология не ведётся.

## Campaign outcome

**Итог кампании: `blocked` — цель не достигнута.**

- **Принятая база не изменилась:** **38269 / 51773 = 73,91689 %**. Принятого инкремента в кампании
  **нет**: шаг 1 закрыт как `not accepted` на ревизии r2, кредит не начислялся.
- **Последний некредитованный кандидат — диагностическое число, не новая база:** **38561 / 51773 =
  74,48091 %**, **+292** ветви к базе (+0,56400 п.п.); до 76 % остаётся **787** ветвей. Число снято с
  прогона r2/n3, который был **красным** (`rc=2`), поэтому оно не является ни принятым инкрементом, ни
  базой отсчёта.
- **Причина.** Бюджет попыток ревизии r2 исчерпан: три содержательных CHECK по r2, все `not accepted`;
  дефект самих критериев **не установлен**. Экспертиза (теперь доступная) рекомендовала **закрыть r2**,
  а не идти четвёртым проходом, и отвергла два альтернативных маршрута: продолжать устранять fixture-DDL
  по классам (опровергнутый механизм — тот же застой всплывает в другом месте, то есть замаскированная
  четвёртая попытка) и маршрут «открыть тикет rc5 и скипнуть» (доказанного продуктового дефекта нет;
  `#352` касается `xxh3`/`xxh32` и не покрывает транспортную нестабильность).
- **Незакрытый дефект — `CH-NONQUERY-STALL`.** Диагностика rv2 (`.pdca/integration-branch-coverage-11/rv2/diagnostics/`): в момент застоя сервер простаивал, ни один запрос не длился >200 мс, чужих сессий не было, load1 ≤ 2.44 — кандидат вердикта `undetermined` со смещением против внешней конкуренции. ClickHouse отдаёт 120-секундный `HttpClient.Timeout` на
  non-query / fixture-выражениях: baseline (2 наблюдения), r1/n3 (`DropMarker()` → `drop table`), r2/n3
  (`ClickHouseT291AverageLiveTests.EmptySetAverage_ShouldBeNull` → `ExecuteNonQuery` → `CreateFixture`).
  Устранение DDL по классам (create-once-per-process + `ReplacingMergeTree` + `FINAL`) **перенесло**
  застой, а не устранило его. Прежняя атрибуция r2/n3 к «чтению» была неверна — CHECK установил
  `ExecuteNonQuery` в `CreateFixture`.
- **Критерии по ревизии r2.** `S1-NUMERIC` — met на последнем некредитованном измерении (38561/51773,
  +292, цель 38528 превышена на 33); `S1-INTEGRITY` — met; `S1-RELIABILITY` — **partially met**
  (`F-R2-OBS` закрыт, `F-R2-IDEMPOTENCE` закрыт, **`F-R2-STATE` открыт**: фиксированное имя
  `ch3_marker_v2` с `IF NOT EXISTS` не проверяет существующую схему и содержимое, проверяется только
  набор id `{1,2,3,4}`); **`S1-GREEN` — unmet** на финальном прогоне.
- **Уточнение записи.** Прогоны `r2/n1` и `r2/n2` были **`rc=0` (зелёные)** и не были приняты из-за
  находок по `S1-INTEGRITY` / `S1-RELIABILITY`, а не из-за красного прогона; кандидат `r2/n3` — другое
  состояние кода, и те зелёные результаты на него не переносятся.
- **Отслеживаемый продуктовый дефект.** **#352** — `xxh3`/`xxh32` рекламируются для MariaDB 11.4,
  сервер отвечает 1305, milestone `1.0.9-rc5`, OPEN:
  https://github.com/AlexeyShirshov/nextorm/issues/352. Он не даёт права пропускать посторонние
  транспортные таймауты.
- **Предусловие для любой будущей `r3` (экспертиза).** Сначала проверить гипотезу **«серверная
  конкуренция, а не форма теста»**: снять ClickHouse `system.processes` / `query_log` в момент
  таймаута либо изолировать всю ClickHouse-полосу целиком. Кроме того, любое изменение критерия
  зелёности требует **свежей авторизации пользователя**. Иначе STOP остаётся правильным исходом.
- **Handoff.** Будущая сессия не продолжает r2; она либо выполняет предусловие выше и получает
  авторизацию на ревизию, либо оставляет кампанию закрытой. Непринятый кандидат, изменённые и новые
  тесты, `prep-r{1,2,3}.md`, оба PLAN-документа и все каталоги evidence остаются в дереве без
  изменений: ничего не удалено и не откачено.

## Progress log

2026-10-29T05:50:17Z | PLAN | cycle 11 | step — | revision r1 | attempt n=1/3 | файл статуса создан с контейнерной схемой кампании; все секции в начальном состоянии, продукт не менялся | .pdca/status/integration-branch-coverage-11.md
2026-10-29T05:50:17Z | PLAN | cycle 11 | step — | revision r1 | attempt n=1/3 | PLAN r1 записан дословно в `## Campaign state` → `### PLAN record (r1, unchanged)`; критерии AC-76 / AC-VALID / AC-INTEGRITY определены, форма incremental, overlay pdca-dotnet | .pdca/status/integration-branch-coverage-11.md
2026-10-29T05:50:17Z | PLAN | cycle 11 | step — | revision r1 | attempt n=1/3 | awaiting user approval: `integration-branch-coverage-11@plan-r1` — normal mode, явного одобрения пользователя ещё нет; preparation record и bootstrap baseline не начаты | .pdca/status/integration-branch-coverage-11.md
2026-10-29T05:52:34Z | APPROVAL | cycle 11 | step — | revision r1 | attempt n=1/3 | одобрение записано: `integration-branch-coverage-11@plan-r1` одобрен пользователем явно (normal mode, 2026-10-29); stop cap отсутствует — «Без лимита»; preparation record и bootstrap baseline разрешены | .pdca/status/integration-branch-coverage-11.md
2026-10-29T05:52:34Z | PREPARATION | cycle 11 | step — | revision r1 | attempt n=1/3 | preparation record r1 записан: `preparation_state: ready`, ref `integration-branch-coverage-11/prep@r1`; цель не измерялась, продукт не менялся; следующий шаг — bootstrap baseline | .pdca/integration-branch-coverage-11/prep-r1.md
2026-10-29T05:53:56Z | PREPARATION | cycle 11 | step — | revision r1 | attempt n=1/3 | infrastructure check: Podman-сокет присутствует, `GET /v1.40/_ping` → OK, машина `podman-machine-default` Currently running; `podman machine start` не потребовался, `NEXTORM_SKIP_PROVIDERS` не задавался | .pdca/integration-branch-coverage-11/rv1/
2026-10-29T05:55:57Z | DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | build `dotnet build tests/nextorm.integration.tests -c Debug` → rc 0, `0 Warning(s) 0 Error(s)`, 00:01:56.98, HEAD `0290d466`, дерево чистое (только неотслеживаемый статус) | .pdca/integration-branch-coverage-11/rv1/build.log
2026-10-29T06:15:16Z | DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | measurement (CI-команда дословно, `--filter-not-trait Category=Diagnostic`) → rc **2** КРАСНЫЙ: 7665 total / 7466 succeeded / 2 failed / 197 skipped / 0 not run, wall 1154 s, suite 19m 08s 200ms, exit 137 и OOM отсутствуют; оба падения — ClickHouse `HttpClient.Timeout of 120 seconds`; rerun не выполнялся (red остаётся red) | .pdca/integration-branch-coverage-11/rv1/coverage-run.log
2026-10-29T06:19:06Z | DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | reportgenerator (флаги CI) → rc 0, 21.3 s; literal branch 38269/51773 = 73.917 %, line 64038/80309 = 79.740 %; skip-budget gate → rc 1 `FAIL: no 'Skipped: N' summary` (формат лога `dotnet test`, 197 skip-записей = 197 в сводке; tools/** не правился) | .pdca/integration-branch-coverage-11/rv1/Summary.txt ; .pdca/integration-branch-coverage-11/rv1/skip-budget.log
2026-10-29T06:20:00Z | DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | baseline записан: counted branch **38204/51683 = 73.92 %**, line 63856/80073 = 79.75 %, дефицит до 76 % = +1076 ветвей; знаменатель цикла 10 (38317) недействителен (drift +13456 branch / +20596 line, причина — рост дерева); захват КРАСНЫЙ, инкрементом не принят; Checkpoint переведён на CHECK (baseline) | .pdca/integration-branch-coverage-11/rv1/baseline.json ; .pdca/integration-branch-coverage-11/rv1/counts.json
2026-10-29T06:49:43Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F1: Recipe переписан на гейтовую модель — reportgenerator `Summary.txt` с `-classfilters:"-NextORM.Core.InMemory*"` = **38269 / 51773 = 73.91689 %**, целевой числитель `ceil(0.76 × 51773) = 39348`, дефицит **+1079**; вычитание констант цикла 10 из формулы убрано; строка 06:20:00Z выше этой правкой перекрыта | .pdca/status/integration-branch-coverage-11.md
2026-10-29T06:49:43Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F1 (производное): Floor `≈190 branches` → `≈259 ветвей от 51773` (0,5 pp от неверного знаменателя) | .pdca/status/integration-branch-coverage-11.md
2026-10-29T06:49:43Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F1/F4: `Phase:` → `CHECK (baseline record corrected, awaiting recheck)`; Next-step seed без изменений | .pdca/status/integration-branch-coverage-11.md
2026-10-29T06:49:43Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F1: Accepted knowledge переписан — гейтовая база 38269/51773 = 73.91689 %, цель 39348, дефицит +1079, захват КРАСНЫЙ и провизорный | .pdca/status/integration-branch-coverage-11.md
2026-10-29T06:49:43Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction п.4: Checkpoint в `## Campaign state` синхронизирован — last boundary baseline bootstrap; accepted basis none yet (awaiting CHECK); disposition open; resume accept baseline → step 1 brief | .pdca/status/integration-branch-coverage-11.md
2026-10-29T06:49:43Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F1/F2: `## Baseline record` — counted 38204/51683 и +1076 заменены гейтовой моделью 38269/51773 = 73.91689 %, цель 39348, дефицит +1079; диагностическая (вторичная) модель 28495/39266 = 72.56914 %, цель 29843, дефицит +1348; формула вычитания констант цикла 10 удалена | .pdca/status/integration-branch-coverage-11.md
2026-10-29T06:49:43Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F2/F3: историческая строка о неприменимости констант (−65/−90) с реальным вкладом InMemory 0/2082 (снят classfilter'ом); drift пересчитан против literal 38407/59949 = **+13366** branch / **+20360** line, прежние +13456/+20596 помечены «against pinned denominator» | .pdca/status/integration-branch-coverage-11.md
2026-10-29T06:49:43Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F4/п.5/п.6: провизорность `provisional red capture (rc=2, 2 ClickHouse HttpClient 120 s timeouts, 0 opt-outs)`, требование зелёного прогона (rc=0, failed: 0) от первого тест-шага и rc=0 на финальном CHECK; рабочий head `29fdcf89` (src/tests идентичны `0290d466`, бинари из `0290d466`); F1–F3 устранены; skip-budget `recorded exception — tooling incompatibility`, 197 skip-записей, `coverage-run.log:1008` | .pdca/status/integration-branch-coverage-11.md
2026-10-29T06:49:43Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction п.4: секция `## Checkpoint` синхронизирована с Campaign state (last boundary baseline bootstrap; accepted basis none yet; resume accept baseline → step 1 brief) | .pdca/status/integration-branch-coverage-11.md
2026-10-29T06:50:42Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F1/F2: baseline.json — `literal`/`counted` заменены на `gate_model` (38269/51773 = 73.91689, target 39348, deficit 1079) и `diagnostic_raw_cobertura` (28495/39266 = 72.56914, target 29843, deficit 1348); блоки `counted_on_cycle10_pinned_denominator` и `subtraction_constants` удалены, добавлен `historical_not_applicable` | .pdca/integration-branch-coverage-11/rv1/baseline.json
2026-10-29T06:50:42Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F3: baseline.json `denominator_drift_vs_cycle10` → branch_delta **13366** / line_delta **20360**, добавлено `note` о прежних 13456/20596 как «against pinned denominator» | .pdca/integration-branch-coverage-11/rv1/baseline.json
2026-10-29T06:50:42Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F2: baseline.json `per_assembly_source` явно помечен как диагностический raw-Cobertura вид; гейтовое число остаётся 38269/51773 | .pdca/integration-branch-coverage-11/rv1/baseline.json
2026-10-29T06:50:42Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F4/п.5/п.6: baseline.json `status` → provisional red capture; добавлен блок `check_correction` (F1–F3 resolved, F4 принят как провизорный, skip-budget recorded exception — tooling incompatibility, 197 записей, coverage-run.log:1008) | .pdca/integration-branch-coverage-11/rv1/baseline.json
2026-10-29T06:50:42Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F1/F2/F3: counts.json — `counted_live_denominator`, `subtraction_constants` и `counted_on_cycle10_pinned_denominator` удалены; добавлены `gate_model` (38269/51773 = 73.91689, target 39348, deficit 1079), `diagnostic_raw_cobertura` (28495/39266 = 72.56914, target 29843, deficit 1348) и `historical_not_applicable`; drift 13366/20360 | .pdca/integration-branch-coverage-11/rv1/counts.json
2026-10-29T06:50:42Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F2: counts.json — добавлен `per_assembly_role`: per-assembly разбивка = диагностический raw-Cobertura вид, не гейт | .pdca/integration-branch-coverage-11/rv1/counts.json
2026-10-29T06:51:05Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction complete: базовая запись приведена к авторитетной гейтовой модели (38269/51773 = 73.91689 %, цель 39348, +1079) с явной диагностической моделью (28495/39266 = 72.56914 %, цель 29843, +1348); F1–F4 закрыты по записи, baseline остаётся провизорным; продукт/тесты/конфиги не менялись, прогонов не было; CHECK перезапускается по этой записи | .pdca/status/integration-branch-coverage-11.md ; .pdca/integration-branch-coverage-11/rv1/baseline.json ; .pdca/integration-branch-coverage-11/rv1/counts.json
2026-10-29T06:51:31Z | CHECK/DO | cycle 11 | step baseline | revision r1 | attempt n=1/3 | correction F2 (косметика записи): baseline.json `artifacts` — описание counts.json «literal + counted + per-assembly + suite counts» → «gate model + diagnostic raw-cobertura + denominator drift + per-assembly + suite counts» | .pdca/integration-branch-coverage-11/rv1/baseline.json
2026-10-29T07:10:52Z | PREPARATION | cycle 11 | step 1 | revision r1 | attempt n=1/3 | preparation revision prep@r2 записана: `preparation_state: ready`, ref `integration-branch-coverage-11/prep@r2`, supersedes `prep@r1`; floor/target — дословно planner (≥+0,5 п.п., минимум +259 при D=51773, target step 1 ≥38528/51773, цель ≥76 %), provenance `baseline-backed` (38269/51773); цель, критерии, рецепт и поверхность без изменений; prep@r1 сохранена | `.pdca/integration-branch-coverage-11/prep-r2.md`
2026-10-29T07:11:48Z | PLAN | cycle 11 | step 1 | revision r1 | attempt n=1/3 | step 1 brief записан в `## Step records` → `### Step 1`, `#### PLAN record` r1 (дословно, `integration-branch-coverage-11/step-1@plan-r1`); критерии S1-NUMERIC / S1-GREEN / S1-INTEGRITY; baseline 38269/51773 принят как basis; checkpoint сдвинут на границу step 1 PLAN | `.pdca/status/integration-branch-coverage-11.md`
2026-10-29T07:11:48Z | PLAN | cycle 11 | step 1 | revision r1 | attempt n=1/3 | awaiting user approval: `integration-branch-coverage-11/step-1@plan-r1` — normal mode, одобрение шага 1 ещё не получено; DO не начат | `.pdca/status/integration-branch-coverage-11.md`
2026-10-29T08:04:13Z | DO | cycle 11 | step 1 | revision r1 | attempt n=1/3 | новые тесты дописаны и доведены до зелёного: 6 красных кейсов MySQL были ошибками самого теста (string-vs-temporal форма вендорного statement'а для adddate/subdate/addtime/subtime, байтовый аргумент bin_to_uuid, неверное ожидание NULL у current_role) — исправлены, ticket-backed skip не применялся; `MySqlNativeFunctionLiveTests` → 113/113 passed, `MariaDbNativeFunctionLiveTests` → 52/52 passed | tests/nextorm.integration.tests/MySql/MySqlNativeFunctionLiveTests.cs ; tests/nextorm.integration.tests/MariaDb/MariaDbNativeFunctionLiveTests.cs
2026-10-29T08:04:13Z | DO | cycle 11 | step 1 | revision r1 | attempt n=1/3 | ClickHouse decision: подключение двух тестов к `ClickHouseReadRetry` **отвергнуто** (`tests/Shared/ClickHouseRetry/**` вне разрешённой поверхности; лимит «одна retry-операция на тест» при 4–6 чтениях на факт; падение DictGet — в DDL конструктора, `ClickHouseReadCategory.Ddl` не принимается `Audited`; ретрай не лечит 216); причина зафиксирована блокером `CH-COLLECTION` — четыре тест-класса выходят на общий ClickHouse-сервер из чужих коллекций и работают параллельно с коллекцией `ClickHouse`; таймауты не расширялись, skip не применялся, тикет не заводился | tests/nextorm.integration.tests/Providers/ClickHouseContainer.cs ; tests/nextorm.integration.tests/ClickHouse/ClickHouseExtendedFunctionTests.cs:26 ; tests/nextorm.integration.tests/T282/BuiltinCatalogExecutionTests.cs:23 ; tests/nextorm.integration.tests/CrossProvider/ProviderCapabilityContractTests.cs:22 ; tests/nextorm.integration.tests/ScalarDerivedSourceIntegrationTests.cs:270
2026-10-29T07:40:26Z | DO | cycle 11 | step 1 | revision r1 | attempt n=1/3 | build gate `dotnet build nextorm.slnx -c Debug` → rc 0, `0 Warning(s) 0 Error(s)`, 62 s (07:39:24Z–07:40:26Z) | .pdca/integration-branch-coverage-11/rv1/attempts/step-1/build.log
2026-10-29T08:01:43Z | DO | cycle 11 | step 1 | revision r1 | attempt n=1/3 | measurement (команда baseline дословно, `--filter-not-trait Category=Diagnostic`) → rc **2** КРАСНЫЙ, wall 21 м 11 с (07:40:32Z–08:01:43Z), suite 21m 05s 418ms: 7830 total / 7632 passed / **1 failed** / 197 skipped / 0 not run; единственное падение — pre-existing `T288NullSafeComparisonClickHouseIntegrationTests.T288_ClickHouse_CapturedNullThenValue_ShouldRebindWithoutValueDependentSql` (573 мс, `Code 216 QUERY_WITH_SAME_ID_IS_ALREADY_RUNNING`), оба baseline-таймаута прошли; 7665 + 52 + 113 = 7830 подтверждает, что новых кейсов ровно 165 | .pdca/integration-branch-coverage-11/rv1/attempts/step-1/coverage-run.log
2026-10-29T08:02:37Z | DO | cycle 11 | step 1 | revision r1 | attempt n=1/3 | reportgenerator (флаги CI, `-classfilters:"-NextORM.Core.InMemory*"`) → rc 0; gate-модель: branch **38499/51773 = 74,36115 %** → **+230 ветвей / +0,44425 п.п.** против 38269/51773, до цели шага 38528 недостача **29** ветвей, до 76 % остаётся **849**; line 64109/80309 = 79,82791 % (+71 строка / +0,08841 п.п.); диагностический raw-Cobertura: 28725/39266 = 73,15489 % (+230), `MariaDbNativeFunctions` 262/604 → 457/604 (+195), `MySqlNativeFunctions` 802/1130 → 805/1130 (+3) | .pdca/integration-branch-coverage-11/rv1/attempts/step-1/Summary.txt ; .pdca/integration-branch-coverage-11/rv1/attempts/step-1/report.json
2026-10-29T08:03:42Z | PREPARATION | cycle 11 | step 1 | revision r1 | attempt n=1/3 | preparation revision `prep@r3` записана (supersedes prep@r2, r2 и r1 не тронуты): цель, AC-76/AC-VALID/AC-INTEGRITY, floor/target, рецепт и поверхность без изменений; изменена только skip-policy — skip лишь для доказанного продуктового дефекта при открытом issue с milestone rc5 (поиск дублей обязателен, `Assert.Skip`, только затронутый сценарий), покрытие остаётся фактическим, виртуального кредита нет | .pdca/integration-branch-coverage-11/prep-r3.md
2026-10-29T08:04:13Z | DO | cycle 11 | step 1 | revision r1 | attempt n=1/3 | DO result, Changed files, Git state записаны; `Campaign state` → `Phase: CHECK (step 1)`, Preparation ref → prep@r3, Checkpoint сдвинут на границу DO → CHECK; кандидат возвращён на CHECK, вердикт не выносился | .pdca/status/integration-branch-coverage-11.md
2026-10-29T08:04:13Z | DECISION | cycle 11 | step 1 | revision r1 | attempt n=1/3 | кампания переведена пользователем в **autonomous mode** («делай это автономно, не нужно спрашивать каждый раз») и выдано уточнение критериев: «если обнаружена ошибка в продукте, необходимо открыть тикет в rc5 и продолжать работу. тест при этом можно скипнуть со ссылкой на открытывй тикет»; записано в `Decisions`, дальнейшие шаги не требуют approval prompt | .pdca/status/integration-branch-coverage-11.md
2026-10-29T08:30:00Z | DO | cycle 11 | step 1 | revision r1 | attempt n=2/3 | попытка 2: добавлены translate-time кейсы `MySqlFunctionTranslator` (JSON-грамматика, GET_FORMAT, NAME_CONST, weight_string, DEFAULT(col), EXPORT_SET, минимальная арность, `->`) и native-кейсы MariaDB (`nvl`, `nvl2`, `add_months`, `to_char`, `regexp_*`, `json_detailed/compact/equals/normalize`, `xxh3`/`xxh32` render, unknown-name, `RenderGated` 11.4, `SequenceName`); классы зелёные: MySQL 137/137, MariaDB 73/73; конвенции core.tests 4/4 | tests/nextorm.integration.tests/MySql/MySqlNativeFunctionLiveTests.cs ; tests/nextorm.integration.tests/MariaDb/MariaDbNativeFunctionLiveTests.cs
2026-10-29T08:31:00Z | DO | cycle 11 | step 1 | revision r1 | attempt n=2/3 | ClickHouse fix: `[CollectionDefinition("ClickHouse", DisableParallelization = true)]` — полоса стала эксклюзивной; `ProviderCapabilityContractTests` получил `[Collection("ClickHouse")]`; три класса из FINDING F2 уже были изолированы своими `DisableParallelization`-коллекциями и не менялись (иначе ослабла бы их защита plan-cache) | tests/nextorm.integration.tests/Support/ClickHouseCollection.cs ; tests/nextorm.integration.tests/CrossProvider/ProviderCapabilityContractTests.cs ; .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n2/clickhouse-collections.txt
2026-10-29T08:38:29Z | DO | cycle 11 | step 1 | revision r1 | attempt n=2/3 | build gate `dotnet build nextorm.slnx -c Debug` → rc 0, `0 Warning(s) 0 Error(s)` (03:45, всё up-to-date после сборки тестового проекта) | .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n2/build.log
2026-10-29T08:53:40Z | DO | cycle 11 | step 1 | revision r1 | attempt n=2/3 | measurement (команда baseline дословно) → rc **2** КРАСНЫЙ, wall 15 м 07 с, suite 15m 01s 767ms: 7875 total / 7677 passed / **1 failed** / 197 skipped / 0 not run; единственное падение — pre-existing `T320SqliteTvfTests.FromOptions_OnATvfSource_ShouldBeNeutralOrFailClosedWithLiveRows` (13 с 423 мс, plan-cache identity); ClickHouse-падений нет, `Code 216` не повторился, `retried_tests=0` | .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n2/coverage-run.log
2026-10-29T08:54:07Z | DO | cycle 11 | step 1 | revision r1 | attempt n=2/3 | reportgenerator (флаги CI) → rc 0; gate-модель: branch **38567/51773 = 74,49250 %** → **+298 ветвей / +0,57559 п.п.** к базе и **+68** к попытке 1, цель шага 38528 **превышена на 39**, до 76 % остаётся 781; line 64153/80309 = 79,88270 %; диагностический raw: 28793/39266 = 73,32807 % (+298), `MySqlFunctionTranslator` 142→226/278, `MariaDbNativeFunctions` 262→468/604, `MySqlNativeFunctions` 802→806/1130 | .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n2/Summary.txt ; .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n2/report.json
2026-10-29T08:56:00Z | DO | cycle 11 | step 1 | revision r1 | attempt n=2/3 | evidence package попытки 2 собран (по требованию CHECK F3): `git-status.txt`, `git-diff-stat.txt`, `tracked-test-changes.diff`, `new-test-files.diff` (полный текст новых файлов), `clickhouse-collections.txt`, `hashes.txt`, UTC-метки; DO result/Changed files/Git state/Defect history обновлены | .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n2/
2026-10-29T09:10:00Z | DO | cycle 11 | step 1 | revision r1 | attempt n=3/3 | T320 доказан и изолирован: probe показал `sameInstance=False` при purge между prepare и 400/400 форков под конкурирующим purger'ом; `QueryPlanStore._generation` — process-wide (`QueryPlanStore.cs:29`, `Clear()` `:181`); создана коллекция `T320 tvf plan identity` (`DisableParallelization = true`) и на неё переведены пять T320-классов; 3 точечных прогона T320 + чистящие кэш классы → 42/42 | .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n3/t320-evidence.txt ; tests/nextorm.integration.tests/T320/T320TvfPlanIdentityCollection.cs
2026-10-29T09:15:00Z | DO | cycle 11 | step 1 | revision r1 | attempt n=3/3 | F-INTEGRITY: рендер-онли кейсы конвертированы в живые (xxh → серверный отказ 1305; version-gate → исполнение на 12.3-контексте → 1146; sequence → живой restart/nextval/lastval/setval) либо удалены (unknown-name); запас проверен точечным coverage: MariaDbNativeFunctions 382/604 против 357/604 у класса попытки 1 | tests/nextorm.integration.tests/MariaDb/MariaDbNativeFunctionLiveTests.cs ; .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n3/probes-n3-classes.cobertura.xml
2026-10-29T09:20:00Z | TRACKING | cycle 11 | step 1 | revision r1 | attempt n=3/3 | GitHub доступен при повторе с `timeout`; milestone `1.0.9-rc5` (23, open) проверен; дубли по 7 запросам не найдены; создан issue **#352** «MariaDB: xxh3 / xxh32 are advertised but absent on 11.4 (error 1305)», rc5, OPEN; skip не применялся — сценарий остаётся живым | https://github.com/AlexeyShirshov/nextorm/issues/352 ; .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n3/github.txt
2026-10-29T09:26:00Z | DO | cycle 11 | step 1 | revision r1 | attempt n=3/3 | build gate `dotnet build nextorm.slnx -c Debug` → rc 0, `0 Warning(s) 0 Error(s)` | .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n3/build.log
2026-10-29T09:41:00Z | DO | cycle 11 | step 1 | revision r1 | attempt n=3/3 | measurement (команда baseline дословно) → rc **2** КРАСНЫЙ, suite 13m 02s 344ms: 7857 total / 7659 passed / **1 failed** / 197 skipped / 0 not run; единственное падение — pre-existing ClickHouse DDL-застой 120 с (`ClickHouseDialectBranchCoverage3Tests…var_pop`, `DropMarker`); T320 и новые классы зелёные | .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n3/coverage-run.log
2026-10-29T09:42:00Z | DO | cycle 11 | step 1 | revision r1 | attempt n=3/3 | reportgenerator (флаги CI) → rc 0; gate-модель: branch **38532/51773 = 74,42489 %** → **+263** ветви / **+0,50799 п.п.** к базе, цель шага 38528 превышена на 4, до 76 % — 816; против n2 −35 (цена снятия рендер-онли кейсов); line 64157/80309 = 79,88768 %; raw: 28758/39266 = 73,23893 %, `MySqlFunctionTranslator` 229/278, `MariaDbNativeFunctions` 432/604, `MySqlNativeFunctions` 805/1130 | .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n3/Summary.txt ; .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n3/report.json
2026-10-29T09:45:00Z | DO | cycle 11 | step 1 | revision r1 | attempt n=3/3 | evidence package попытки 3 собран (Summary, cobertura, логи, диффы, все `CollectionDefinition`, T320-проба и targeted-прогоны, XXH-1305 repro, вывод github-команд); DO result/Changed files/Git state/Defect history/Checkpoint обновлены; attempt budget n=3/3 исчерпан | .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n3/
2026-10-29T09:35:27Z | PLAN | cycle 11 | step 1 | revision r1 | attempt n=3/3 | boundary decision: **STOP** от планировщика на границе шага 1 — критерии неудовлетворены, бюджет попыток исчерпан (r1, n=3/3), три CHECK подряд `not accepted`, дефект критериев не установлен; новый шаг и ревизия контракта не открывались (это была бы замаскированная четвёртая попытка) | .pdca/status/integration-branch-coverage-11.md
2026-10-29T09:35:27Z | ESCALATION | cycle 11 | step 1 | revision r1 | attempt n=3/3 | ресурсный блокер: обязательная консультация `pdca-escalate` (strong tier) не выполнена — **`Missing API key`**; экспертного мнения по существу не существует, эскалация остаётся открытой | .pdca/status/integration-branch-coverage-11.md
2026-10-29T09:35:27Z | ACT | cycle 11 | step 1 | revision r1 | attempt n=3/3 | ACT закрыт: шаг 1 — `not accepted`, кредит не начислен, КРАСНЫЙ результат сохранён; `S1-NUMERIC` met на последнем измерении (38532/51773 = 74,42489 %, +263), `S1-GREEN` unmet, `S1-INTEGRITY` unmet (два открытых дефекта); кампания → `blocked`, цель не достигнута, база 38269/51773 не менялась, до 76 % остаётся 816 ветвей; непринятый кандидат, тесты и evidence сохранены, ничего не удалено | .pdca/status/integration-branch-coverage-11.md ; .pdca/integration-branch-coverage-11/rv1/attempts/step-1-n3/
2026-10-29T09:48:00Z | USER | cycle 11 | step 1 | revision r2 | attempt n=1/3 | инструкция пользователя: «ключ для стронг тира починил, правку разрешаю, можно перезапускать» — разрешает ревизию контракта и повторный запуск | .pdca/status/integration-branch-coverage-11.md
2026-10-29T09:48:00Z | ESCALATION | cycle 11 | step 1 | revision r2 | attempt n=1/3 | эскалация завершена: блокер **`Missing API key`** устранён пользователем; получена экспертная рекомендация — выдать r2 того же шага 1, а не новую попытку r1 | .pdca/status/integration-branch-coverage-11.md
2026-10-29T09:48:00Z | PLAN | cycle 11 | step 1 | revision r2 | attempt n=1/3 | ревизия контракта `r2` зарегистрирована дословно в `#### PLAN record (r2)`; поверхность расширена на `tests/Shared/ClickHouseRetry/**` (только fixture-надёжность); критерии S1-NUMERIC / S1-GREEN / S1-INTEGRITY + новое S1-RELIABILITY; бюджет fixture-retry: 1 на операцию и 1 на полную сборку; r1 история и ключи дефектов сохранены | .pdca/status/integration-branch-coverage-11.md
2026-10-29T10:10:37Z | DO | cycle 11 | step 1 | revision r2 | attempt n=1/3 | DO r2 выполнен: удалён зависавший cleanup DDL в `ClickHouseDialectBranchCoverage3Tests` (один `EnsureMarker()` на процесс), добавлен fixture-только retry `ClickHouseFixtureRetry` (1/операцию, 1/сборку, использовано 0), исправлены оба integrity-дефекта (version-gate 1146→1305 + положительный контроль; восстановлена 14-кейсовая theory) | tests/Shared/ClickHouseRetry/ClickHouseFixtureRetry.cs ; tests/nextorm.integration.tests/ClickHouse/ClickHouseDialectBranchCoverage3Tests.cs ; tests/nextorm.integration.tests/MariaDb/MariaDbNativeFunctionLiveTests.cs
2026-10-29T10:10:37Z | DO | cycle 11 | step 1 | revision r2 | attempt n=1/3 | build `dotnet build nextorm.slnx -c Debug` → rc 0, `0 Warning(s) 0 Error(s)`; точечные прогоны до измерения: ClickHouse 30/30, PlanKey 5/5, Cycle7 27/27, T320 10/10, MariaDB+MySQL 206/206 | .pdca/integration-branch-coverage-11/rv1/attempts/step-1-r2/targeted-runs.txt ; …/build.log
2026-10-29T10:10:37Z | DO | cycle 11 | step 1 | revision r2 | attempt n=1/3 | measurement r2 (команда baseline дословно) → **rc=0 ЗЕЛЁНЫЙ**: 7871 total / 7674 passed / **0 failed** / 197 skipped / 0 not run, 13 м 02 с; gate **38561/51773 = 74,48091 %** → **+292** ветви (+0,56400 п.п.) к базе, цель шага превышена на 33, до 76 % — 787; line 64153/80309 | .pdca/integration-branch-coverage-11/rv1/attempts/step-1-r2/coverage-run.log ; …/Summary.txt
2026-10-29T10:31:20Z | DO | cycle 11 | step 1 | revision r2 | attempt n=2/3 | F-R2-OBS исправлено: фикстур-retry учитывается файлом из процесса измерения (env-путь, построчные попытки, итог на `ProcessExit`); в evidence `retries_used=0` с одной строкой попытки — нулевой случай доказан | .pdca/integration-branch-coverage-11/rv1/attempts/step-1-r2-n2/fixture-retry-accounting.txt
2026-10-29T10:31:20Z | DO | cycle 11 | step 1 | revision r2 | attempt n=2/3 | F-R2-IDEMPOTENCE исправлено: `create table if not exists`, `drop table if exists` удалён из подготовки, сид защищён серверным `select count()` (0→вставить, 4→готово, иначе исключение), вся marker-подготовка — одна fixture-операция | tests/nextorm.integration.tests/ClickHouse/ClickHouseDialectBranchCoverage3Tests.cs ; tests/Shared/ClickHouseRetry/ClickHouseFixtureRetry.cs
2026-10-29T10:31:20Z | DO | cycle 11 | step 1 | revision r2 | attempt n=2/3 | measurement r2 n=2 → **rc=0 ЗЕЛЁНЫЙ**: 7871 / 7674 passed / 0 failed / 197 skipped; gate **38560/51773 = 74,47898 %** → **+291** ветвь (+0,56207 п.п.), цель шага превышена на 32; против прогона r2 минус одна ветвь (удалённый `drop table` в core — сказано прямо, без перезапуска) | .pdca/integration-branch-coverage-11/rv1/attempts/step-1-r2-n2/coverage-run.log ; …/Summary.txt
2026-10-29T11:02:07Z | DO | cycle 11 | step 1 | revision r2 | attempt n=3/3 | F-R2-OBS закрыто: учёт пишет UTC, pid, run_id, process_touch_utc и PROVENANCE-шапку; mtime файла внутри интервала измерения, sha256 и несокращённая команда в evidence | attempts/step-1-r2-n3/provenance.txt ; …/measurement-command.txt
2026-10-29T11:02:07Z | DO | cycle 11 | step 1 | revision r2 | attempt n=3/3 | F-R2-IDEMPOTENCE + F-R2-STATE закрыто: `ch3_marker_v2` с `ReplacingMergeTree order by (id)` и чтениями `.Final()`; повторная вставка тех же id не видна, `count()` — лишь быстрый путь; имя фиксирует схему, фикстура утверждает точный набор id {1,2,3,4} | tests/nextorm.integration.tests/ClickHouse/ClickHouseDialectBranchCoverage3Tests.cs
2026-10-29T11:02:07Z | DO | cycle 11 | step 1 | revision r2 | attempt n=3/3 | measurement r2 n=3 → **rc=2 КРАСНЫЙ**: 7871 / 7673 passed / 1 failed / 197 skipped; gate **38561/51773 = 74,48091 %** (+292, цель шага +33); падение — `ClickHouseT291AverageLiveTests.EmptySetAverage_ShouldBeNull`, 120 s timeout на чтении вне fixture (fixture: 0 с, retries_used=0); без перезапуска | attempts/step-1-r2-n3/coverage-run.log ; …/Summary.txt ; …/fixture-retry-accounting.txt
2026-10-29T11:06:57Z | CHECK | cycle 11 | step 1 | revision r2 | attempt n=3/3 | третий CHECK по r2: `not accepted`; S1-NUMERIC met (38561/51773, +292, цель +33), S1-INTEGRITY met, S1-RELIABILITY **partially met** (F-R2-OBS и F-R2-IDEMPOTENCE закрыты, F-R2-STATE открыт), S1-GREEN unmet (rc=2) | .pdca/status/integration-branch-coverage-11.md ; .pdca/integration-branch-coverage-11/rv1/attempts/step-1-r2-n3/
2026-10-29T11:06:57Z | ESCALATION | cycle 11 | step 1 | revision r2 | attempt n=3/3 | экспертиза выполнена и рекомендовала **закрыть r2**, отвергнув дальнейшее устранение fixture-DDL по классам (опровергнутый механизм / замаскированная четвёртая попытка) и маршрут «тикет rc5 + skip» (продуктовый дефект не доказан); установлено предусловие будущей r3: проверить гипотезу «серверная конкуренция, а не форма теста» и получить свежую авторизацию на изменение критерия зелёности | .pdca/status/integration-branch-coverage-11.md
2026-10-29T11:06:57Z | ACT | cycle 11 | step 1 | revision r2 | attempt n=3/3 | ACT закрыт: шаг 1 / r2 — `not accepted`, кредит не начислен, красный кандидат сохранён; зафиксирован дефект `CH-NONQUERY-STALL` (перенос застоя, исправлена прежняя атрибуция r2/n3 к «чтению»); кампания → `blocked`, база 38269/51773 не менялась, последний некредитованный кандидат 38561/51773 = 74,48091 %, до 76 % остаётся 787; ничего не удалено и не откачено | .pdca/status/integration-branch-coverage-11.md ; .pdca/integration-branch-coverage-11/rv1/attempts/step-1-r2-n3/report.json
2026-10-29T11:16:53Z | DIAGNOSTIC | cycle 11 | step 1 | revision r2 | attempt n=3/3 | начата ограниченная диагностическая сессия `CH-NONQUERY-STALL` (не попытка, не ревизия): сэмплеры `system.processes`/host каждые 2 с, контейнер `3c0a0cfbc06e` через Docker API + `clickhouse-client` exec | .pdca/integration-branch-coverage-11/rv2/diagnostics/
2026-10-29T11:16:53Z | DIAGNOSTIC | cycle 11 | step 1 | revision r2 | attempt n=3/3 | итерация 1 (11:10:39Z–11:15:14Z, 861 тест): застой воспроизведён — `ClickHouseFunctionExecutionTests.T257_MapFilter…`, ровно 2m, `HttpClient.Timeout of 120 seconds` на **чтении**; итерации 2–3 не выполнялись, факт зафиксирован | .pdca/integration-branch-coverage-11/rv2/diagnostics/iter1.log
2026-10-29T11:16:53Z | DIAGNOSTIC | cycle 11 | step 1 | revision r2 | attempt n=3/3 | факты: `system.query_log` 9057 строк, стартов по минутам 1205/866/62/125/1585/675 (трафик падает на минутах застоя); **ни один запрос не дольше 200 мс**; чужих сессий нет; load1 0.63–2.44; кандидат вердикта **`undetermined`** со смещением против внешней конкуренции | .pdca/integration-branch-coverage-11/rv2/diagnostics/summary.json ; …/query-log-window.tsv
2026-10-29T11:48:37Z | DIAGNOSTIC | cycle 11 | step 1 | revision r2 | attempt n=3/3 | мандат сессии-2 получен (пользователь: «ок, давай А»): сокеты/эфемерные порты в момент таймаута + один прогон без ретранслятора; бюджет 60 мин и максимум два прогона полосы | .pdca/integration-branch-coverage-11/rv2/diagnostics/session-2/
2026-10-29T11:48:37Z | DIAGNOSTIC | cycle 11 | step 1 | revision r2 | attempt n=3/3 | маршрут: ретранслятор `localhost:45597/ping` → 200; прямой контейнерный `10.88.14.107:8123` и шлюз `10.88.0.1:8123` → curl exit 28 (нет маршрута). Настоящего обхода ретранслятора нет, суррогат не выдавался за эквивалент | .pdca/integration-branch-coverage-11/rv2/diagnostics/session-2/summary.json
2026-10-29T11:48:37Z | DIAGNOSTIC | cycle 11 | step 1 | revision r2 | attempt n=3/3 | прогоны полосы: №1 (11:38:18Z–11:39:27Z) зелёный 861/850/0 failed; №2 (11:39:43Z–11:47:37Z) 2 failed, из них `ClickHouseT291AverageLiveTests.NullableAverage_ShouldIgnoreNulls` ровно 2м00с (`HttpClient.Timeout 120 s`) | .pdca/integration-branch-coverage-11/rv2/diagnostics/session-2/lane-run-2.log
2026-10-29T11:48:37Z | DIAGNOSTIC | cycle 11 | step 1 | revision r2 | attempt n=3/3 | серверная сторона за 11:39:40–11:48:00: стартов по минутам 457/241/63/240/62/176/555/1719/1153, максимум длительности **63 мс** — на минутах застоя трафик проваливается, медленных запросов нет | .pdca/integration-branch-coverage-11/rv2/diagnostics/session-2/summary.json
2026-10-29T11:48:37Z | DIAGNOSTIC | cycle 11 | step 1 | revision r2 | attempt n=3/3 | итог сессии-2: кандидат вердикта **`undetermined`** — клиентская половина шкалы отсутствует для момента застоя (наблюдатель не стартовал из-за CRLF; PID полосы не опознан ни разу), обход ретранслятора недоступен; по §3C это финальный STOP, третьей сессии нет; +292 остаются непринятыми, F-R2-STATE открыт | .pdca/integration-branch-coverage-11/rv2/diagnostics/session-2/README.md
