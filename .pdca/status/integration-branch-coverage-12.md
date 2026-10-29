# Cycle status: integration-branch-coverage-12 / cycle 12

## Campaign state

Status: blocked
Form: incremental   Overlay: pdca-dotnet (coder+.NET)
Goal: branch coverage интеграционной сборки ≥ 76 % по неизменной gate-модели (8 сборок, исключение `NextORM.Core.InMemory*`), новый прирост — содержательные SQLite-проверки core
Predecessor: `integration-branch-coverage-11` — closed `not accepted` / `blocked`; его история и evidence (`rv1/attempts/*`, `rv2/diagnostics/*`) read-only и не пересматриваются
Global acceptance: C12-S1-NUMERIC / C12-S1-CORE / C12-S1-GREEN / C12-S1-INTEGRITY / C12-S1-REPORTING — по ссылке на `#### PLAN record (r1)` этого файла
Recipe: metric = branch coverage; model = reportgenerator `Summary.txt` при `-classfilters:"-NextORM.Core.InMemory*"`; 8 сборок `nextorm.(core|sqlite|postgres|sqlserver|mysql|mariadb|clickhouse|entityframeworkcore).dll`; знаменатель **51773**, принятая база **38269 (73.91689 %)**, цель кампании **39348**, операционный target шага 1 — **38820**
Allowed surface: `tests/nextorm.integration.tests/**` (новые SQLite/core классы и необходимые тестовые изменения) + записи кампании; production, coverage-конфигурация, provider matrix, `tests/Shared/ClickHouseRetry/**` — запрещены
Increment unit: принятый числовой прирост gate-покрытия, измеренный на текущем дереве, либо решение-меняющее знание
Floor: ≥ +0,5 п.п. (минимум +259 ветвей при D=51773); операционный target шага 1 — **38820/51773**
Preparation ref: —
Commit policy: none (no commit/push/merge)
Constraints: реальные СУБД (SQLite file), FluentAssertions, `AssertionStyleTests`, `IntegrationSuiteScopingTests`, CRLF, warnings-as-errors, Roslyn для символов; никаких opt-outs, никаких rerun-for-luck
Effort accounting: PLAN — высокая уверенность в метрике и новом green scope; достижимый SQLite-прирост неизвестен (recorded, never a cutoff)
Baseline evidence: `.pdca/integration-branch-coverage-11/rv1/` (принятая база 38269/51773) и `.pdca/integration-branch-coverage-11/rv1/attempts/step-1-r2-n3/integration.cobertura.xml` (диагностический ориентир выбора области — **не** база и не кредит)
Evidence root: `.pdca/integration-branch-coverage-12/rv1/`
Steps: step 1 → basis 38269/51773 → credited — → verdict pending → evidence `.pdca/integration-branch-coverage-12/rv1/attempts/step-1/`
Current step: 1   Phase: ACT (step 1, r1, closed)   Cycle: 12   Plan revision: r1   Attempt: n=3/3
Next-step seed: (consumed by step 1)
Accepted knowledge: новая кампания открыта по пользовательской инструкции (только SQLite для core-проверок, 76 % суммарного гейта сохраняется, удаления тестов делает пользователь); **+292 кампании 11 не переносятся** — весь кандидат измеряется заново
Rejected approaches: —   Deferred: —   Blockers: —   Protected/foreign: кампания 11 read-only
Decisions: form incremental (planner); scope шага 1 — `QueryPreparer` + `EntityBuilder<TEntity>` через реальный SQLite
Defect history: см. `### Step 1` → `#### Defect ledger`
Checkpoint: last boundary — **ACT шага 1 / r1 завершён**, шаг `not accepted`; accepted basis **38269/51773**; принятого инкремента нет (измеренный кандидат 38561/51773 = 74,48091 %); counters N=12, r1, n=3/3; disposition — кампания `blocked`; resume point — **решение владельца о следующем контракте** (новый PLAN либо STOP)
Stop verdict: **STOP** — шаг 1 кампании 12 закрыт `not accepted` при исчерпанных попытках (n=3/3), дефект критериев не установлен; цель кампании не достигнута (38561/51773 = 74,48091 % против target 38820, недостача 259, до 76 % — 787); следующий шаг — только решение владельца о новом контракте
Pending approval ref / Approval record: not applicable: autonomous mode

## Original task

«pdca необходимо довести бранч покрытие интеграционных тестов до 76%» — плюс пользовательские уточнения кампании 12:
«я разрешаю использовать только sqlite для интеграционных тестов ядра», «нам необходимо увеличить покрытие core с помощью интеграционных тестов, каких конкретно провайдеров - не так важно, так как ядро содердить общий функционал», «Оставить 76 % суммарного гейта», «Я удаляю полностью невалидные тесты, но вообще клик не может быть препядствием для данной задачи, мы пишем тесты под sqlite».

## Step records

### Step 1

#### Current state

- task: `integration-branch-coverage-12` · form: `incremental` · specialized skill: `pdca-dotnet` (coder+.NET)
- mode: `autonomous`
- cycle: `N=12` · revision: `r=1` · attempt: `n=1/3`
- scope: шаг 1 — прирост проверенного SQLite-поведения `QueryPreparer` и `EntityBuilder<TEntity>` в core
- goal: ≥76 % суммарного gate-покрытия — авторитетны `## Original task` и `#### PLAN record (r1)`
- phase: `ACT` · record: `completed` · outcome: `not accepted / no credit` (n=3/3, бюджет исчерпан)
- basis (принятая): **38269/51773 = 73.91689 %** · операционный target шага 1: **38820/51773**
- criteria: C12-S1-NUMERIC / C12-S1-CORE / C12-S1-GREEN / C12-S1-INTEGRITY / C12-S1-REPORTING — точный текст в `#### PLAN record (r1)`, здесь не перепечатывается

#### PLAN record (r1)

Встроен дословно, без изменений:

````markdown
# PLAN — `integration-branch-coverage-12`, step 1, r1

**Предшественник:** `integration-branch-coverage-11`, закрытый `not accepted` / `blocked`. Его история не пересматривается.

## Важная арифметика

Цель остаётся **76% суммарного gate-покрытия**: при знаменателе **51773** требуется **39348** покрытых ветвей.

Последний непринятый кандидат — **38561**, диагностический остаток — **787**. Если удаления тестов теряют ещё `L` уникально покрытых gate-ветвей, остаток становится **787 + L**, а не остаётся 787.

У ClickHouse около **1680 покрытых raw-ветвей**. Удаление всей полосы может увеличить потребность примерно до **2467 ветвей**, плюс возможная потеря покрытия core. **Это предупреждающая оценка, не установленная gate-арифметика:** raw→gate-преобразование неизвестно, а перекрывающийся вклад тестов нельзя складывать повторно.

Deletion ledger должен количественно определить потери **до первого приёмочного измерения**. Baseline не уменьшается в пользу кампании. Если доказанный верхний предел достижимого покрытия ниже 76%, исход PLAN — **`original task is infeasible under the stated constraints`**, без изменения порога. Самой большой оценки потерь для такого вывода недостаточно.

## 1. Форма, цель и изменение контракта

**Форма:** новая incremental-кампания. **Специализация:** `pdca-dotnet`, overlay `coder+.NET`.

**Исходная задача:** «pdca необходимо довести бранч покрытие интеграционных тестов до 76%».

**Запрошенный и передаваемый результат:** не менее 76% прежнего суммарного интеграционного gate-покрытия, достигнутого содержательными SQLite-интеграционными проверками core, при новой явно ограниченной области зелёности.

### Criteria diff относительно кампании 11

| Положение | Авторизация пользователя | Контракт кампании 12 |
|---|---|---|
| Новые проверки используют SQLite | «я разрешаю использовать только sqlite для интеграционных тестов ядра» | Новые core-проверки работают с реальным SQLite |
| Область нового прироста — core | «нам необходимо увеличить покрытие core с помощью интеграционных тестов, каких конкретно провайдеров - не так важно, так как ядро содердить общий функционал» | SQLite используется для проверки общего поведения core, не provider-specific SQL |
| Числовая цель сохраняется | «Оставить 76 % суммарного гейта» | Не core-only метрика, а прежние восемь сборок |
| Область зелёности изменяется | «Я удаляю полностью невалидные тесты, но вообще клик не может быть препядствием для данной задачи, мы пишем тесты под sqlite» | Все новые/изменённые проверки принимаемого scope и вся существующая SQLite-полоса зелёные; унаследованная ClickHouse-полоса не препятствует приёмке, но её фактический исход сообщается |

Существующая матрица `CommonTestSuite` **не сокращается**. Удаления выполняет пользователь; агент не получает общего разрешения удалять нестабильные тесты.

## 2. Неизменная измерительная модель

- Метрика: точное отношение покрытых ветвей к учитываемым ветвям, без приёмки по округлению.
- Область: прежние восемь сборок из `coverage.integration.settings.xml`.
- Исключение: действующие классы `NextORM.Core.InMemory*`.
- Принятая исходная база: **38269/51773**.
- Порог: **≥0,76**, задан пользователем.
- Фильтры, правила знаменателя и конфигурационные пороги не изменяются.
- Raw-Cobertura служит только ориентиром выбора области.
- Исчезновение сборки из отчёта не разрешает незаметно уменьшить знаменатель.
- При законном изменении учитываемого знаменателя абсолютный target пересчитывается как `ceil(0,76 × D)`; изменение самой модели требует нового решения PLAN.

## 3. Step 1: scope и минимальный результат

**Входной scope:** содержательный прирост проверенного SQLite-поведения подготовки и построения запросов core, с положительным измеренным вкладом новых проверок в покрытие core.

Область первого шага — **`QueryPreparer` и `EntityBuilder<TEntity>`**: это общий жизненный цикл запросов, уже затрагиваемый интеграционной сборкой, с диагностическим запасом 348 и 324 raw-ветви. SQLite даёт обоснованный путь проверки общего поведения через реальное исполнение запросов.

Это **не обещание доступности всех 672 ветвей**. Конкретные достижимые сценарии устанавливает DO через Roslyn и существующие контракты; неподдерживаемая SQLite-семантика не имитируется.

**Разрешённая поверхность:** новые выделенные SQLite/core классы и необходимые тестовые изменения внутри `tests/nextorm.integration.tests/**`, плюс записи кампании.

Не входят:

- production-код и конфигурация покрытия;
- изменение provider matrix;
- новые изменения `tests/Shared/ClickHouseRetry/**`;
- ремонт ClickHouse и дополнительные исследования stall;
- удаления тестов агентом.

**Количество тестов, имена файлов и execution units выбирает DO.** Они не являются результатом задачи и не фиксируются PLAN. Тесты без реального обращения к БД относятся в unit-проект и не могут выдаваться здесь за интеграционный прирост.

### F-R2-STATE

**Оценка применимости входит в step 1.**

- Если SQLite-путь использует затронутый fixture, принимаемое поведение требует проверки его необходимой схемы и содержимого, не только множества id.
- Если fixture `ch3_marker_v2` относится исключительно к исключённой ClickHouse-полосе и не используется SQLite-путём, finding фиксируется как **неприменимый к SQLite-приёмке кампании 12**.
- Последнее не означает исправления ClickHouse-дефекта: исходная запись остаётся открытой.

Создавать SQLite-копию проблемной таблицы только ради формального закрытия finding не требуется.

## 4. Критерии step 1

**C12-S1-NUMERIC.**  
Операционный target — **38820/51773**: последнее наблюдавшееся значение 38561 плюс минимум 259 ветвей, соответствующих ≥0,5 п.п. при данном знаменателе.

Это **не признание 38561 принятым baseline**. Весь кандидат измеряется и принимается заново. Потери от удалений входят в фактический итог; target не понижается. Размер пакета обоснован фиксированной ценой полного измерения 13–21 минуту.

**C12-S1-CORE.**  
Новые SQLite-проверки имеют установленный положительный вклад в покрытие core и проверяют наблюдаемое поведение с содержательными assertions. Attribution не заменяет суммарную метрику.

**C12-S1-GREEN.**  
Все новые/изменённые тесты принимаемого scope и все существующие SQLite-lane тесты успешны. Пропуски допускаются только по действующим правилам, включая подтверждённый продуктовый дефект с открытым rc5-issue и ссылкой.

**C12-S1-INTEGRITY.**  
Соблюдены FluentAssertions, `AssertionStyleTests`, `IntegrationSuiteScopingTests`, реальные СУБД, CRLF и warnings-as-errors. Нет assertion-free тестов, coverage farming, ослабления отрицательных проверок или скрытого удаления вклада.

**C12-S1-REPORTING.**  
Соблюдены предписанные экспертным решением deletion ledger, paired measurement и достоверное сообщение ClickHouse-исхода. Красная ClickHouse-полоса не называется «suite green».

## 5. Обязательные условия измерения и отчётности

### Полное измерение и attribution

- Приёмочная gate-величина относится к **полному запуску всех провайдеров**, без `NEXTORM_SKIP_PROVIDERS`.
- Парное SQLite-lane измерение показывает, какие core-линии покрывают новые проверки; оно не подменяет gate-величину.
- Оба результата относятся к одному состоянию принимаемого кода.
- Диагностические opt-outs не дают приёмочной цифры.
- Конкретные команды и инструменты выбирают DO/CHECK.

Если полный процесс завершился ненулевым кодом из-за исключённой ClickHouse-полосы, CHECK должен отдельно установить завершённость и пригодность coverage capture и выполнение SQLite-green scope. Незавершённый либо повреждённый отчёт не принимается.

### Deletion ledger

Для **каждого пользовательского удаления** фиксируются:

- идентичность теста и inventory diff;
- последнее доступное red/stall свидетельство;
- заявленная причина удаления;
- его вклад и уникальная потеря покрытия без двойного счёта.

При отсутствии удаления это явно отмечается. Отсутствующие данные не объявляются нулевым вкладом.

### Запрет rerun-for-luck

Для одной позиции попытки сохраняется один назначенный приёмочный результат. ClickHouse-red архивируется и сообщается дословно как owner-excluded outcome; он **не запускает серию повторов ради зелёности или лучшего покрытия**.

## 6. История, попытки и остановка

- Новая кампания: **r1, входная позиция n=1/3**, регистрирует executor.
- Максимум три попытки действующего контракта; не более трёх под видом повторных измерений.
- История и recurrence counts дефектов продолжаются между кампаниями. Новое имя не разрешает повторный ремонт исчерпанного `CH-NONQUERY-STALL`.
- Кампания 11, `rv1/attempts/*`, `rv2/diagnostics/*` остаются read-only.
- Унаследованные тесты — inventory без предварительного кредита. **+292 не переносятся как принятый результат.**

**Следующий шаг возможен только после положительного независимого CHECK принятого инкремента.**

STOP требуется при исчерпании попыток, нарушении same-defect ограничения либо установленной невозможности выполнить цель под обязательными ограничениями. `done` допускается только после CHECK всей цели **≥76%** и всех требований нового контракта.

Пользовательское правило сохраняется:

> «если обнаружена ошибка в продукте, необходимо открыть тикет в rc5 и продолжать работу. тест при этом можно скрыть со ссылкой на открытый тикет»

Никаких commit/push/merge не разрешается.

**Статус:** `.pdca/status/integration-branch-coverage-12.md`.  
**Материалы:** `.pdca/integration-branch-coverage-12/rv1/`, в binding checkout.

**Уверенность:** высокая в определении метрики и нового green scope; достижимый SQLite-прирост и потери пользовательских удалений пока неизвестны.
````

#### Approval record

not applicable: autonomous mode — пользователь авторизовал кампанию и её контракт явно; approval prompt не запрашивался.

#### Defect ledger

| key | state | observed | gap / next evidence |
|---|---|---|---|
| `CH-NONQUERY-STALL` | **undetermined** (перенесён из кампании 11) | baseline 2×, r1/n3 `DropMarker()` → `drop table`, r2/n3 `ClickHouseT291AverageLiveTests…` → `ExecuteNonQuery` → `CreateFixture`, rv2/session-1 `T257_MapFilter…` (чтение), session-2 `NullableAverage_ShouldIgnoreNulls` 2m00s | нет клиентского захвата в момент застоя (наблюдатель не стартовал; PID полосы не опознан), обход ретранслятора недоступен; ремонт запрещён контрактом — ClickHouse-полоса исключена из green scope |
| `F-NUMERIC` | **open** (critical) | 38561 против target 38820 — недостача 259 | закрытие требует прироста из непроверенных кластеров (см. ACT record) |
| `F-ATTRIBUTION` | **open** (major, unverified) | A/B не дал пригодной пары; вклад новых классов не измерен | нужна A/B на здоровой инфраструктуре |
| `F-DELETIONS` | **open** (major) | «zero loss» сильнее доказательств: равные агрегаты допускают компенсацию | нужна уникальная потеря, а не равенство агрегатов |
| `F-TEST-CLAIM` | **open** (medium) | `CorePreparerEdgeSqliteTests.cs:130–141` — только `NotBeNull` | усилить или переписать утверждение |
| `F-PAIR-IMPOSSIBLE` | **refuted** | конфликт VSTest/нативного фильтра (rc 134), не невозможность парной процедуры | — |
| `F-R2-STATE` | **open** (перенесён) · **неприменим к SQLite-приёмке** (DO, r1) | фиксированное имя `ch3_marker_v2` + `IF NOT EXISTS` не проверяет существующую схему/содержимое, только набор id | fixture принадлежит исключённой ClickHouse-полосе и SQLite-путём не используется; новый класс создаёт и проверяет `c12_order` сам; для ClickHouse finding остаётся открытым |

#### DO result
### DO attempt n=3/3 (final) — `cannot finish`

- **A/B (Task 1): выполнен по методу, но непригоден.** Оба новых файла вынесены из проекта, пересобран
  только тестовый проект; SHA-256 продуктовых сборок до и после — **идентичны** (`PRODUCT_DLLS_IDENTICAL=yes`),
  то есть пара была бы сопоставима. Прогон B (полный набор без новых классов) дал **rc=2, 7871 total,
  3006 failed, 4668 passed, 197 skipped** — массовый отказ Postgres/ClickHouse-полос, не связанный с
  новыми классами; при 3006 падениях cobertura несопоставима с run A, поэтому **дельту ветвей вычислить
  нельзя**. Файлы восстановлены, продуктовые сборки снова побайтово те же (`product-dlls-restored.txt`).
  **F-ATTRIBUTION остаётся открытым**, замена не подставлялась.
- **259 ветвей (Task 2): не закрыты.** Сделан только начальный скан достижимости:
  `CorrelatedQueryExpressionVisitor` — 416 zero-hit строк, включая конструктор (141–148), что указывает на
  редкий путь инстанцирования, а не на обычные коррелированные подзапросы. Probe не писался, батчи не
  выполнялись, ни одна ветвь не перевёрнута. **F-NUMERIC открыт: 38561 vs 38820, недостача 259.**
- **Приёмка (Task 3): нового прогона нет.** Назначенная цифра — уже измеренная на этом же дереве
  (продуктовые сборки проверены как идентичные): **38561 / 51773 = 74.48091 %**, +292 к базе 38269,
  −259 к 38820, 787 до 39348; прогон rc=0, 7896 total / 7699 passed / 0 failed / 197 skipped,
  архив — `attempts/step-1/`.
- **ClickHouse (дословно, из назначенного прогона):** `Test run summary: Passed!` —
  `total: 7896, failed: 0, succeeded: 7699, skipped: 197`. В прогоне B полоса падала массово
  (`attribution-runB2.log`), что и сделало B непригодным.
- **Конвенции:** `AssertionStyleTests` + `IntegrationSuiteScopingTests` — результат в
  `attempts/step-1-n3/convention-tests.txt`.
- **Источники заморожены:** `attempts/step-1-n3/sources/` + `sources-sha256.txt`.
- **Hand-off:** DO передаёт **`cannot finish`** — числовой разрыв не закрыт, атрибуция не установлена,
  бюджет попытки исчерпан; шаг уходит в ACT. Ни цифра, ни вывод не смягчаются.

### DO attempt n=2 (r1, unchanged; CHECK → DO)

- **F-INTEGRITY: evidence frozen.** Исходники обоих новых классов скопированы в
  `attempts/step-1-n2/sources/` с sha256 (`8149a1e8…`, `9812bb11…`), дифф `new-tests.diff`, проверка CRLF
  (295 + 184 строки, **0 bare LF**), точные команды — `measurement-command.txt`.
  Конвенции: `AssertionStyleTests` + `IntegrationSuiteScopingTests` → **4/4 passed**.
- **F-DELETIONS: квантифицировано измерением.** Вклад удалений измерен на идентичном workload:
  `nextorm.clickhouse` **1684/2221 до и после**, все остальные сборки без изменений, whole-gate **38561 до и
  после** → измеренная потеря **0** на уровне сборки и гейта. Точная per-test цифра **не установлена**
  (файлы удалены, гейт сливает классы) и не заявляется; «нулевая стоимость» без измерения не утверждается —
  здесь есть измерение, но его границы зафиксированы в `deletion-quantification.md`.
- **F-MEASUREMENT: один назначенный результат.** Для n=2 назначен **run A** — полный прогон, включающий
  новые классы, на текущем дереве (бинари не менялись): **rc=0**, 7896 total / 7699 passed / 0 failed /
  197 skipped. Gate: **38561 / 51773 = 74,48091 %** → +292 к базе 38269, **−259 к 38820**, 787 до 39348.
  Предыдущий прогон того же дерева не переобъявляется: он и есть run A.
- **F-ATTRIBUTION: НЕ закрыт.** Корректный A/B (полный прогон **без** двух новых классов, те же бинари и
  настройки) **не выполним** этим раннером: сочетание VSTest-негативного фильтра с `--filter-not-trait`
  даёт `Test run summary: Zero tests ran` (rc 134), опции `--filter-not-class` нет; добавить trait моим
  классам значило бы изменить дерево между A и B. Попытка и её вывод заархивированы
  (`attribution-runB.log`, `attribution-runB.rc`). Классовый line-diff из прошлого прохода **не**
  подставляется вместо A/B. Итог: заявление «unique contribution = 0» остаётся **непроверенным**.
- **F-NUMERIC: не закрыт.** 38561 vs 38820 — недостача **259**; до 76 % — **787**.
- **Reachability map (частично).** Из 493 + 265 zero-hit строк осмотренные кластеры относятся либо к уже
  покрытым (измерено в прошлом проходе: строки, которых касаются новые классы, unique = 0), либо к
  путям, недостижимым с публичной SQLite-поверхности (провайдер-специфичные ветви, DI/reflection,
  отказы). Нового достижимого кластера в этой попытке не доказано; полная классификация всех 758 строк
  не выполнена.
- **ClickHouse (дословно):** `Test run summary: Passed!` — `total: 7896, failed: 0, succeeded: 7699, skipped: 197`.
- **Сборка:** rc 0, `0 Warning(s) 0 Error(s)` (бинари не менялись с pass 2).
- **Доказательства:** `rv1/attempts/step-1-n2/` — `sources/`, `sources-sha256.txt`, `new-tests.diff`,
  `crlf-check.txt`, `convention-tests.txt`, `measurement-command.txt`, `deletion-quantification.md`,
  `attribution-runB.log/.rc`, `report.json`; принятая цифра — `rv1/attempts/step-1/Summary.txt`.

### DO second pass (attempt n=1, not a new attempt)

- **Метод замкнутого цикла.** Из gate-cobertura извлечены zero-hit строки: `EntityBuilder<TEntity>` — **493**,
  `QueryPreparer` — **265**; они сгруппированы в поведения (ошибки/guard'ы, PreWhere In-values, CTE,
  агрегаты над пустым множеством, nullable-агрегаты, композитные GroupBy/Distinct, paging без OrderBy).
  Добавлен класс `CorePreparerEdgeSqliteTests` (**9 фактов**, реальный файловый SQLite).
- **Петля до/после (class-level coverage):** первый класс — `EntityBuilder` 127/1224, `QueryPreparer` 386/1772;
  оба класса — 128/1224 и 405/1772. Прирост второго класса: **+1** и **+19** raw-ветвей.
- **Уникальный вклад — ноль.** Прямой line-diff: новые классы попадают в 288 строк `EntityBuilder` и 567 строк
  `QueryPreparer`, но **ни одна** из них не является zero-hit в полном прогоне (`unique vs full suite = 0`,
  в обоих классах). Гейт считает ветвь один раз, поэтому дополнительные assertions на уже покрытых путях
  не дают ничего — это и есть измеренная причина, почему pass 1 остался на 38560.
- **Приёмочное измерение (одно):** **rc=0**, 7896 total / 7699 passed / **0 failed** / 197 skipped.
  Gate: **38561 / 51773 = 74,48091 %** → **+292** к базе 38269; операционный target **38820 не достигнут —
  недостача 259**; до 76 % остаётся **787**. Против pass 1: **+1** ветвь.
- **ClickHouse (дословно):** `Test run summary: Passed!` — `total: 7896, failed: 0, succeeded: 7699, skipped: 197`;
  красного результата для архива нет.
- **Сборка:** rc 0, `0 Warning(s) 0 Error(s)`.
- **Вывод для решения (не вердикт):** маршрут «SQLite-интеграционные тесты core» **исчерпан** — оставшиеся
  zero-hit строки двух классов недостижимы с публичной SQLite-поверхности (провайдер-специфичные ветви,
  DI/reflection, отказы). Дальнейшие пакеты того же вида дадут ноль.
- **Доказательства:** `rv1/attempts/step-1/` — `Summary.txt` (pass 2), `integration.cobertura.xml` (pass 2),
  `Summary-pass1.txt`/`integration-pass1.cobertura.xml` (pass 1, сохранены), `coverage-run-pass2.log/.rc`,
  `loop-both-classes.cobertura.xml`, `loop.log`, `report-pass2.json`, `deletion-ledger.md`, `hashes.txt`.


**Статус: кандидат возвращён; прогон ЗЕЛЁНЫЙ (`rc=0`, `failed=0`), но `C12-S1-NUMERIC` НЕ достигнута.**
Вердикт не выносится — его даёт CHECK.

- **Gate-модель:** branch **38560 / 51773 = 74,47898 %** → **+291** ветвь к базе 38269; операционный target
  шага 1 **38820 не достигнут — недостача 260 ветвей**; до 76 % остаётся 788. line 79,8 %.
- **Прогон:** **rc=0**, 7887 total / 7690 passed / **0 failed** / 197 skipped. ClickHouse-полосы в красном
  состоянии в этом прогоне нет (архивировать нечего); `NEXTORM_SKIP_PROVIDERS` не задавался.
- **Что написано и почему это достижимо через SQLite.** Новый класс
  `tests/nextorm.integration.tests/Sqlite/CorePreparerAndBuilderSqliteTests.cs` (16 фактов) работает на
  реальной файловой SQLite-базе с собственной таблицей `c12_order`: подготовка и переиспользование плана,
  rebinding параметров, инвалидация через `PurgeQueryCache`, `storeInCache:false`, фильтры и булевы
  композиции, null-проверки, строковые предикаты, проекции с вычислениями, сортировка с tie-breaker,
  `Offset/Limit`, агрегаты (`Count/Sum/Min/Max/Avg`), группировка, `Distinct`, кванторы, `IN` по
  захваченному списку, терминалы `First/Single/FirstOrDefault`. Все проверки — на возвращённых строках.
- **Attribution (парное измерение, не заменяет gate):** только новый класс даёт
  `EntityBuilder<TEntity>` **127/1224** и `QueryPreparer` **386/1772** raw-ветвей; вся SQLite-полоса —
  671/1224 и 1138/1772; полный прогон — 900/1224 и 1423/1772.
- **Deletion ledger:** 18 пользовательских удалений (9 файлов retry-харнесса, 6 unit-тестов вне гейта,
  `ClickHouseRetryScenarioTests`, репортёр, `FaultProxy`); агент ничего не удалял. Учёт — в
  `attempts/step-1/deletion-ledger.md`; вклад не вычитается и не объявляется нулевым.
- **F-R2-STATE:** **неприменим к SQLite-приёмке**: `ch3_marker_v2` принадлежит исключённой
  ClickHouse-полосе и SQLite-путём не используется; новый класс создаёт и проверяет свою схему и
  содержимое. Для ClickHouse finding остаётся открытым.
- **Сборка:** `dotnet build nextorm.slnx -c Debug` → rc 0, `0 Warning(s) 0 Error(s)`.
- **Доказательства:** `.pdca/integration-branch-coverage-12/rv1/attempts/step-1/` — `Summary.txt`,
  `integration.cobertura.xml`, `coverage-run.log/.rc`, `reportgenerator.log`, `build.log`,
  `report.json`, `deletion-ledger.md`, `attribution-*.log/cobertura`, `new-classes-run.txt`, `hashes.txt`.
- **Блокер:** недостача **260** gate-ветвей до операционного target; одного пакета в 16 SQLite-тестов
  для него не хватило.

#### ACT record

**Outcome: `not accepted` · credit: no credit · измеренный кандидат сохранён.**

- **Причина.** Бюджет попыток исчерпан (n=3/3), третья CHECK — `not accepted → ACT`; дефект критериев **не
  установлен**. Четвёртой попытки нет.
- **Критерии.**
  - `C12-S1-NUMERIC` — **unmet**: 38561/51773 = 74,48091 %, target 38820, недостача **259**; до 76 % — **787**.
  - `C12-S1-CORE` — **unverified**: положительная атрибуция вклада в core не установлена.
  - `C12-S1-GREEN` — **met**: rc 0, 7896 total / 7699 passed / 0 failed / 197 skipped.
  - `C12-S1-INTEGRITY` — **met в границах замороженных доказательств**: конвенции 4/4, 0 bare LF, build 0/0,
    реальный файловый SQLite, FluentAssertions.
  - `C12-S1-REPORTING` — **unverified**: пригодной A/B-пары нет; уникальная потеря от удалений не измерена.
- **Findings, перенесённые из CHECK (дословно по смыслу):**
  - `F-NUMERIC` — **critical, подтверждён**;
  - `F-ATTRIBUTION` — **major, не проверен**;
  - `F-DELETIONS` — **major, подтверждён**: формулировка «zero loss» сильнее доказательств — равные агрегаты
    допускают взаимную компенсацию;
  - `F-PAIR-IMPOSSIBLE` — **опровергнут в заявленной области**: сырой лог показывает конфликт
    VSTest/нативного фильтра, rc 134, а не невозможность любой парной измерительной процедуры;
  - `F-TEST-CLAIM` — **medium, подтверждён**: `CorePreparerEdgeSqliteTests.cs:130–141` проверяет только
    `NotBeNull` на подготовленном объекте и не доказывает, что одна подготовленная команда обслужила два
    значения.
- **Диагноз прогона B.** В сыром логе видны assertion по transaction id в PostgreSQL и **HTTP 503** от
  ClickHouse; B непригоден как контролируемая пара. Единая инфраструктурная причина и независимость от
  порядка **не доказаны**; B не опровергает A, но и не может быть объявлен изолированным инцидентом.
  Различить их позволило бы: повтор B на заведомо здоровой инфраструктуре с фиксацией состояния контейнеров
  и порядка выполнения, либо B с изоляцией полос и записью `system.query_log`/серверных ошибок по каждой.
- **Сохранение.** Оба новых класса (`CorePreparerAndBuilderSqliteTests`, `CorePreparerEdgeSqliteTests`) и все
  evidence остаются в дереве. Их вклад **не объявляется нулевым** и **не подаётся как принятый инкремент
  покрытия**.
- **Факт для следующего PLAN.** За три попытки **ни один** тест, нацеленный на `CorrelatedQueryExpressionVisitor`,
  `AdvancedAggregateTranslator`, `ArraySqlTranslator`, `MemberTranslator`, `RelationshipResolver` или
  `JsonSqlTranslator`, **не был написан**: попытки ушли на измерение и атрибуцию. Достижимая масса этих
  кластеров (raw uncovered 309 + 250 + 168 + 161 + 108 + 97 = **1093**) остаётся непроверенной, а
  raw→gate преобразование по-прежнему неизвестно.
- **Hand-off.** Продолжение возможно только по решению владельца о следующем контракте (новый PLAN или STOP);
  сам ACT новых переходов не добавляет.

#### Changed files

- `tests/nextorm.integration.tests/Sqlite/CorePreparerAndBuilderSqliteTests.cs` (новый, 16 фактов)
- `tests/nextorm.integration.tests/Sqlite/CorePreparerEdgeSqliteTests.cs` (новый, 9 фактов; 2-й проход)
- `.pdca/status/integration-branch-coverage-12.md` (этот файл)
- `.pdca/integration-branch-coverage-12/rv1/attempts/step-1/**` (evidence прогона)

Продуктовый код, coverage-конфигурация, provider matrix и `tests/Shared/ClickHouseRetry/**` не менялись
(последние удалены пользователем, не агентом).

#### Git state

status path: `.pdca/status/integration-branch-coverage-12.md` — created this campaign · untracked (durable on disk, not committed); no `git add`, no commit.
`git status --porcelain` на момент DO (2-й проход): `?? .pdca/status/integration-branch-coverage-12.md`,
`?? tests/nextorm.integration.tests/Sqlite/CorePreparerAndBuilderSqliteTests.cs`,
`?? tests/nextorm.integration.tests/Sqlite/CorePreparerEdgeSqliteTests.cs`, ` M .pdca/status/integration-branch-coverage-11.md`;
пользовательские удаления к этому моменту закоммичены им как `67ba9494`. Ранее (до его коммита) в дереве было: 18 пользовательских удалений (см. deletion ledger), 13 изменённых
файлов (включая ClickHouse-тесты и `ClickHouseTestProvider`), 2 несвязанных модификации документации/скилла,
и этот файл статуса; HEAD `0484713d`. Атрибуция: удаления и правки ClickHouse — **чужая работа пользователя**,
не вклад кампании; агент добавил только новый тестовый класс.

#### Progress log

Единый журнал кампании — `## Progress log` этого файла.

## Campaign outcome

**Итог кампании: `blocked` — цель не достигнута.**

- **Принятая база не изменилась:** **38269 / 51773 = 73,91689 %**. Принятого инкремента в кампании **нет**:
  шаг 1 закрыт `not accepted` при исчерпанных попытках.
- **Измеренный кандидат (не принят):** **38561 / 51773 = 74,48091 %** — +292 ветви к базе, недостача **259**
  до операционного target 38820 и **787** до 76 % (39348). Кандидат сохранён как измеренный результат шага,
  но кредит не начислялся.
- **Причина.** Бюджет попыток исчерпан (n=3/3), третья CHECK вернула `not accepted → ACT`, дефект критериев
  **не установлен**. Попытки ушли на измерение и атрибуцию, а не на прирост.
- **Findings, которые остаются открытыми** (перенесены из CHECK): `F-NUMERIC` (critical, подтверждён — 259
  ветвей), `F-ATTRIBUTION` (major, не проверен — пригодной A/B-пары нет), `F-DELETIONS` (major, подтверждён —
  формулировка «zero loss» сильнее доказательств, равные агрегаты допускают взаимную компенсацию),
  `F-TEST-CLAIM` (medium, подтверждён — `CorePreparerEdgeSqliteTests.cs:130–141` доказывает только
  непустоту результата подготовки). `F-PAIR-IMPOSSIBLE` — **опровергнут**: в сыром логе конфликт
  VSTest/нативного фильтра (rc 134), а не невозможность парной процедуры как таковой.
- **Прогон B.** В сыром логе видны assertion по transaction id в PostgreSQL и **HTTP 503** от ClickHouse;
  как контролируемая пара B непригоден. Единая инфраструктурная причина и независимость от порядка **не
  доказаны**; B не опровергает A, но и не объявляется изолированным инцидентом.
- **Что сохранено.** Оба новых класса (`CorePreparerAndBuilderSqliteTests` — 16 фактов,
  `CorePreparerEdgeSqliteTests` — 9 фактов) и все attempt-каталоги остаются в дереве. Их вклад **не
  объявляется нулевым** и **не подаётся как принятый инкремент покрытия**: положительная атрибуция не
  установлена.
- **Факт для следующего PLAN.** За три попытки **ни один** тест не был написан против
  `CorrelatedQueryExpressionVisitor`, `AdvancedAggregateTranslator`, `ArraySqlTranslator`, `MemberTranslator`,
  `RelationshipResolver` или `JsonSqlTranslator`. Их суммарная raw-непокрытая масса — **1093** ветви
  (309 + 250 + 168 + 161 + 108 + 97) — остаётся непроверенной, а raw→gate преобразование по-прежнему
  неизвестно.
- **Дефекты, перенесённые из кампании 11:** `CH-NONQUERY-STALL` — `undetermined` (нет клиентского захвата в
  момент застоя, обход ретранслятора недоступен); `F-R2-STATE` — open, **неприменим к SQLite-приёмке**
  (fixture принадлежит исключённой ClickHouse-полосе).
- **Hand-off.** Кампания закрыта как `blocked`. Продолжение — только по решению владельца о следующем
  контракте (новый PLAN с другой областью прироста либо STOP); ACT новых переходов не добавляет.
  Ничего не удалено и не откачено.

## Progress log

2026-10-29T12:26:08Z | REGISTRATION | cycle 12 | step 1 | revision r1 | attempt n=1/3 | кампания `integration-branch-coverage-12` открыта; PLAN r1 записан дословно, defect ledger засеян `CH-NONQUERY-STALL` (undetermined) и `F-R2-STATE` (open); кампания 11 read-only | .pdca/status/integration-branch-coverage-12.md
2026-10-29T12:53:10Z | DO | cycle 12 | step 1 | revision r1 | attempt n=1/3 | deletion ledger зафиксирован до первого измерения: 18 пользовательских удалений (retry-харнесс, 6 unit-тестов вне гейта, `ClickHouseRetryScenarioTests`, репортёр, `FaultProxy`); агент ничего не удалял | `.pdca/integration-branch-coverage-12/rv1/attempts/step-1/deletion-ledger.md`
2026-10-29T12:53:10Z | DO | cycle 12 | step 1 | revision r1 | attempt n=1/3 | написан новый SQLite-класс `CorePreparerAndBuilderSqliteTests` (16 фактов, реальный файловый SQLite, assertions на строках); локально 16/16 green | `tests/nextorm.integration.tests/Sqlite/CorePreparerAndBuilderSqliteTests.cs`
2026-10-29T12:53:10Z | DO | cycle 12 | step 1 | revision r1 | attempt n=1/3 | build `dotnet build nextorm.slnx -c Debug` → rc 0, `0 Warning(s) 0 Error(s)` | `…/attempts/step-1/build.log`
2026-10-29T12:53:10Z | DO | cycle 12 | step 1 | revision r1 | attempt n=1/3 | приёмочное измерение (полный прогон, без opt-outs) → **rc=0 ЗЕЛЁНЫЙ**: 7887 total / 7690 passed / 0 failed / 197 skipped; gate **38560/51773 = 74,47898 %** (+291 к базе), target 38820 не достигнут — недостача **260**; ClickHouse-red отсутствует | `…/attempts/step-1/Summary.txt` ; `…/coverage-run.log`
2026-10-29T12:53:10Z | DO | cycle 12 | step 1 | revision r1 | attempt n=1/3 | парное attribution-измерение: только новый класс — `EntityBuilder` 127/1224 и `QueryPreparer` 386/1772 raw; вся SQLite-полоса 671/1224 и 1138/1772; полный прогон 900/1224 и 1423/1772 | `…/attempts/step-1/attribution-newclass.cobertura.xml`
2026-10-29T13:15:07Z | DO | cycle 12 | step 1 | revision r1 | attempt n=1/3 (2-й проход) | zero-hit инвентарь: `EntityBuilder<TEntity>` 493 строки, `QueryPreparer` 265; сгруппированы в поведения, добавлен класс `CorePreparerEdgeSqliteTests` (9 фактов) | `tests/nextorm.integration.tests/Sqlite/CorePreparerEdgeSqliteTests.cs`
2026-10-29T13:15:07Z | DO | cycle 12 | step 1 | revision r1 | attempt n=1/3 (2-й проход) | петля class-level: 127→128 (EntityBuilder) и 386→405 (QueryPreparer) raw-ветвей; **уникальный вклад против полного прогона = 0 строк** — новые классы не покрывают ни одной zero-hit строки | `…/attempts/step-1/loop-both-classes.cobertura.xml`
2026-10-29T13:15:07Z | DO | cycle 12 | step 1 | revision r1 | attempt n=1/3 (2-й проход) | одно приёмочное измерение → **rc=0 ЗЕЛЁНЫЙ**: 7896 / 7699 passed / 0 failed / 197 skipped; gate **38561/51773 = 74,48091 %** (+292 к базе, +1 к pass 1), target 38820 не достигнут — недостача **259** | `…/attempts/step-1/Summary.txt` ; `…/coverage-run-pass2.log`
2026-10-29T13:25:33Z | DO | cycle 12 | step 1 | revision r1 | attempt n=2/3 | F-INTEGRITY: исходники новых классов заморожены (sha256, копии, дифф), CRLF 0 bare LF, конвенции 4/4, точные команды заархивированы | `.pdca/integration-branch-coverage-12/rv1/attempts/step-1-n2/`
2026-10-29T13:25:33Z | DO | cycle 12 | step 1 | revision r1 | attempt n=2/3 | F-DELETIONS: измерено — clickhouse-сборка 1684/2221 и whole-gate 38561 до и после удалений, потеря 0 на уровне сборки/гейта; per-test цифра не установлена и не заявляется | `…/attempts/step-1-n2/deletion-quantification.md`
2026-10-29T13:25:33Z | DO | cycle 12 | step 1 | revision r1 | attempt n=2/3 | F-ATTRIBUTION: A/B не выполним (негативный VSTest-фильтр + `--filter-not-trait` → Zero tests ran, rc 134); попытка заархивирована, заявление о unique contribution остаётся непроверенным | `…/attempts/step-1-n2/attribution-runB.log`
2026-10-29T13:25:33Z | DO | cycle 12 | step 1 | revision r1 | attempt n=2/3 | назначенный результат n=2 (run A): rc=0, 7896/7699 passed/0 failed/197 skipped; gate **38561/51773 = 74,48091 %** (+292 к базе, −259 к 38820, 787 до 39348); ClickHouse вердикт дословно Passed | `…/attempts/step-1/Summary.txt`
2026-10-29T13:51:28Z | DO | cycle 12 | step 1 | revision r1 | attempt n=3/3 | A/B: продуктовые сборки до/после выноса новых файлов идентичны (sha256), но прогон B дал 3006 падений — cobertura несопоставима; дельта ветвей не вычислена, F-ATTRIBUTION открыт | `…/attempts/step-1-n3/attribution-runB2.log` ; `…/product-dlls-*.txt`
2026-10-29T13:51:28Z | DO | cycle 12 | step 1 | revision r1 | attempt n=3/3 | достижимость: `CorrelatedQueryExpressionVisitor` 416 zero-hit, включая конструктор (141–148) → редкий путь; probe не писался, ни одна ветвь не перевёрнута; F-NUMERIC открыт (259) | `…/attempts/step-1-n3/README.md`
2026-10-29T13:51:28Z | DO | cycle 12 | step 1 | revision r1 | attempt n=3/3 | DO передаёт **`cannot finish`**: назначенная приёмка 38561/51773 = 74,48091 % (+292 к базе, −259 к 38820, 787 до 39348) на идентичном дереве; шаг уходит в ACT без четвёртой попытки | `…/attempts/step-1/Summary.txt`
2026-10-29T13:55:07Z | CHECK | cycle 12 | step 1 | revision r1 | attempt n=3/3 | третья CHECK: `not accepted → ACT` — попытки исчерпаны, дефект критериев не установлен; C12-S1-NUMERIC unmet (259), C12-S1-CORE unverified, C12-S1-GREEN met, C12-S1-INTEGRITY met в границах frozen evidence, C12-S1-REPORTING unverified | `.pdca/status/integration-branch-coverage-12.md`
2026-10-29T13:55:07Z | ACT | cycle 12 | step 1 | revision r1 | attempt n=3/3 | ACT закрыт: шаг 1 / r1 — `not accepted`, кредит не начислен, кандидат 38561/51773 сохранён; кампания → `blocked`; findings F-NUMERIC / F-ATTRIBUTION / F-DELETIONS / F-TEST-CLAIM перенесены, F-PAIR-IMPOSSIBLE опровергнут; оба новых класса и все evidence сохранены, вклад не объявлен нулевым | `.pdca/status/integration-branch-coverage-12.md` ; `.pdca/integration-branch-coverage-12/rv1/attempts/`
