# Cycle status: collection-405-407-item-405 / cycle 1

## Current state

- task: `collection-405-407-item-405` · form: `cycle` · specialized skill: `pdca-dotnet` (`pdca-coder` + `nextorm-pdca`)
- mode: `autonomous`
- cycle: `N=1` · revision: `r=1` · attempt: `n=1`
- scope: весь #405 · status path: `.pdca/status/collection-405-407-item-405.md`
- goal: исходный результат в §Original task
- phase: `ACT` · record: `completed` · outcome: `accepted` · plan_state: `ready` · disposition: `finalized`
- Возврат после CHECK: `CHECK → DO`: единственный консолидированный evidence-only re-gather завершён; allowance `spent` для `AC-405-01` и `AC-405-02`. Оригинал scout (`ses_df3298c28ffedk3Ko4oioB0a94`): `{"path": ".pdca/collection-405-407/items/405/rv1/report-51cb31ef60bfe4f1131c4df6d526782175dfd9b740f2eb83250db61325320b0a.md", "sha256": "51cb31ef60bfe4f1131c4df6d526782175dfd9b740f2eb83250db61325320b0a"}` (§Tests, §API and guards, §Documentation, §Unconfirmed). Scout наблюдал checkout, Git/хеши не проверял; отдельная идентичность executor: `.pdca/collection-405-407/items/405/rv1/candidate-sources.sha256`, `.pdca/collection-405-407/items/405/rv1/candidate-baseline.log`. Диагностика `.pdca/collection-405-407/items/405/rv1/sqlserver-regather-presence-01.log`: `unset` (Boolean, exit 0), разрешённого live маршрута нет; обе JOIN-мутации не запускались. Прежний CHECK сохранён; новый CHECK ещё не запускался. Пакет recheck: `{"path": ".pdca/collection-405-407/items/405/rv1/check-5842d6ae316fe652cc866e9912545dd6175ebf8e1b47edc6157c3d79ea46c888.md", "sha256": "5842d6ae316fe652cc866e9912545dd6175ebf8e1b47edc6157c3d79ea46c888"}`.
- source: #405 · context refs: `.pdca/status/collection-405-407.md`, `.pdca/collection-405-407/items/405/rv1/item-plan-brief.md`
- criteria: `AC-405-01`, `AC-405-02` · §PLAN record `r1`
- PLAN: получены факты применимости #405 (`pdca-scout`); решение planner: сохранённый PLAN r1 применим. `{"path": ".pdca/collection-405-407/items/405/rv1/report-d680959c90cfb752a85e306e46b8fdbe89598ba1ef46a289fa4b30d34020e0f1.md", "sha256": "d680959c90cfb752a85e306e46b8fdbe89598ba1ef46a289fa4b30d34020e0f1"}`.

- SQL Server (#405), оригинал `pdca-scout`: `{"path": ".pdca/collection-405-407/items/405/rv1/report-ba46d860ba0b0307a9e64be29426493d31345a23c580477a0e47d8f27ae14959.md", "sha256": "ba46d860ba0b0307a9e64be29426493d31345a23c580477a0e47d8f27ae14959"}`. Разрешённый существующий live SQL Server маршрут не подтверждён; состояние и значение `NEXTORM_SQLSERVER_CONNECTION` не проверялись, значение не раскрывалось. Podman, ручной wslc, runner, endpoint probes и тесты не запускались.
- Указание пользователя: Если разрешённого маршрута нет, реализовать candidate в рамках сохранённого PLAN r1 и передать независимому CHECK с точным пробелом в доказательствах live SQL Server. Это не разрешение на Podman, изменение тестов или объёма, PASS либо commit. Маршрут: DO по сохранённому PLAN r1.

- #405: в окружении executor `NEXTORM_SQLSERVER_CONNECTION` — `unset` (проверено только наличие); разрешённый live SQL Server маршрут не подтверждён. Evidence: `.pdca/collection-405-407/items/405/rv1/sqlserver-config-presence.log`.

- Пользовательская подсказка о доступности (дословно, требует проверки, не разрешение): `сервер есть в контейрене на винде в wslc`. Наличие сервера не разрешает запускать, останавливать, перенастраивать или выделять контейнер, изменять общую БД, использовать Podman или менять маршрут. Учётные данные не логируются. Следующее разрешённое действие: read-only исследование возможностей маршрута. Только при подтверждённом поддерживаемом приватном/одноразовом маршруте — возобновить ту же live SQL Server проверку на том же замороженном кандидате; иначе сохраняется блокер.

- Возможности маршрута (только чтение fixture/runner/workflow): оригинал `pdca-scout` из `ses_df2e790e9ffeim4752MWYWJAzv` — `{"path": ".pdca/collection-405-407/items/405/rv1/report-220b9b54195f8a61dc3d8e2da44c5e75c7272a9a1f227267ccdd0a649a67effa.md", "sha256": "220b9b54195f8a61dc3d8e2da44c5e75c7272a9a1f227267ccdd0a649a67effa"}`. При непустом `NEXTORM_SQLSERVER_CONNECTION` fixture использует внешний connection и выходит до создания Testcontainers builder или `StartAsync()`. Единственное наблюдение состояния переменной — прежнее `unset` (`.pdca/collection-405-407/items/405/rv1/sqlserver-regather-presence-01.log:1`); значение не читалось и не выводилось, проверка не повторялась.
- В просмотренном `tools/integration/` отдельный lease-aware launcher для внешнего endpoint не найден. Capped runner устанавливает Podman `DOCKER_HOST`, имеет lock и caps, но timeout по умолчанию `0`; summary/receipt на уровне класса без сверки native xUnit ID. Skill описывает manual wslc/exact-ID workflow, но соответствующий launcher не обнаружен; `AGENTS.md` запрещает manual wslc provisioning до появления lease-aware route.
- Фактический контейнер, endpoint, readiness, приватное владение и disposable DB не проверялись. Пользовательская подсказка о wslc — не подтверждение маршрута и не разрешение; неоднозначное уведомление о перезапуске не считается подтверждением SQL Server. Блокер: нет доказанного поддерживаемого и разрешённого приватного/disposable маршрута для live SQL Server. Предпосылка возобновления: подтверждение такого маршрута с поддерживаемым lease-aware workflow и сверкой native xUnit ID; затем только та же live-проверка того же замороженного кандидата. Новых разрешений, перехода или re-gather нет.

### #405 — разрешение на recovery, preflight pending

Дословный источник разрешения: `не знаю, можешь созать свою БД, если текущая не подходит`; `нет, давай через wslc`; `эти правила я отменил`.

Разрешение для будущего отдельного запуска: только собственный одноразовый SQL Server 2025 и отдельная новая test DB через wslc, без Podman. Сохранить `ctxsql-1795349697` (`e32cac8dc294`, порт 1433) и все существующие `ctx*` контейнеры/схемы. Новый уникальный порт на `127.0.0.1`; случайный временный секрет только в restricted env-file для предназначенного контейнера/процесса без вывода или логирования. Отменены только фиксированные cgroup значения. Сохраняются `DOTNET_GCHeapHardLimit=0x80000000`, `DOTNET_gcServer=0`, timeout ≤600s, эксклюзивный lease/lock до проверенного удаления только собственного ID, проверка приватности/владения/readiness, версии и отдельной БД через Ubuntu SqlClient, exact native ID reconciliation.

Оригинал parent executor из `ses_df2afe1d7ffek56dUA2q0vHZwD`: `{"path": ".pdca/collection-405-407/items/405/rv1/report-fdc9569d97a38300afc269513366673876cbaa79c5915ef104d9f07cc3c501b4.md", "sha256": "fdc9569d97a38300afc269513366673876cbaa79c5915ef104d9f07cc3c501b4"}`. Источники без повторного discovery/build: `.pdca/collection-405-407/items/405/rv1/discovery-roster.json`, `discovery-native.json`, `discovery-receipt.log`, `discovery-before.sha256`, `discovery-after.sha256`. UPDATE `6ff6a563c871e856990f644f6a75aed783872e557d86c98314f243fe56a8b2f6`; DELETE `9841a8e8dbd1ebafd896c8c4532fafa1be6e6fb44e18f3752e3351e01a584a5e`. Требуется ровно один native terminal result на каждый ожидаемый ID.

`running-integration-tests` загружен. Из read-only CLI проверок: `wslc-create-help.log` подтверждает `--pull never`, `wslc-image-cache.log` — локальный `mcr.microsoft.com/mssql/server:2025-latest`; оба exit 0. `.pdca/collection-405-407/items/405/rv1/launcher-preflight.py` — только модель lock/fault cleanup (не live launcher); `launcher-syntax.log` и `launcher-fault-dry-run.log` exit 0. Реальный launcher и подтверждённый маршрут пока отсутствуют: dry-run не доказывает teardown реального контейнера. Этот вызов не создавал контейнер/БД, не запускал сеть, SQL или тесты. CHECK остаётся paused, allowance spent.


Продолжение recovery: canonical lock был свободен в момент допуска (`.pdca/collection-405-407/items/405/rv1/recovery-live-lock-check.log`, exit 0), но не удерживается. `launcher-preflight.py` — симуляция, не исполняет ownership/teardown для WSLc. В имеющихся cycle artifacts нет готового Ubuntu SqlClient worker для утверждённого предварительного authenticated readiness/version и создания выделенной БД; `csi`, `dotnet-script`, `pwsh` отсутствуют на PATH (`.pdca/collection-405-407/items/405/rv1/recovery-live-worker-precondition.log`). При запрете нового build не запускался неподтверждённый маршрут. Для возобновления нужен готовый Ubuntu SqlClient worker либо явно разрешённая отдельная сборка вспомогательного readiness worker без пересборки кандидата, и реальный launcher с пройденным fault/signal preflight. Контейнеров/БД не создавали, live тесты не запущены; CHECK остаётся paused, счётчики и квота без изменений.

## Original task

- Исходный запрос коллекции: `.pdca/status/collection-405-407.md` → `## Original task`.
- #405: `.pdca/collection-405-407/rv1/report-da29dc008e1e39ed68b912853d73972a549e3eee3bca38f73d868ab456a2377a.md`:3,9–22, исходные заголовок, тело и `## Приёмка` без изменений. Дополнение #407 неприменимо.

## PLAN record

### r1

`{"path": ".pdca/collection-405-407/items/405/rv1/report-ae18c7a15b0713020a9f1600d93b0fe6cb163e5a35dc3234e65a083eb14cd85b.md", "sha256": "ae18c7a15b0713020a9f1600d93b0fe6cb163e5a35dc3234e65a083eb14cd85b"}`

Ограничения и предпосылки: §«Границы решения и предпосылки» и §«Ограничения, пробелы и стоп-условия» того же источника; рамки текущей подготовки — `.pdca/collection-405-407/items/405/rv1/item-plan-brief.md`. Обязательных предшественников не установлено; живой SQL Server для приёмки не подтверждён.

Оригинал решения planner из `ses_df3704de7ffeJp1OMJ8MOK4nRw`: `{"path": ".pdca/collection-405-407/items/405/rv1/report-18be86f6e03733f2acae31e8a59dc82c7f950c18509adaf9b6b418d00168a073.md", "sha256": "18be86f6e03733f2acae31e8a59dc82c7f950c18509adaf9b6b418d00168a073"}`.

## Approval record

`not applicable: autonomous mode` — первый DO запущен по подтверждённому маршруту.

## DO result

Кандидат реализован и заморожен, готов к независимому CHECK. Идентичность: `.pdca/collection-405-407/items/405/rv1/candidate-sources.sha256`; все 11 хешей подтверждены.

- `AC-405-01`: получено локальное SQL-generation evidence обеих мутаций; live SQL Server — `unverified`, нет результата UPDATE и DELETE, подтверждающего выбор JOIN + предикатом и чтение захвата в том же батче. Два live кейса скомпилированы, ни один не запускался.
- `AC-405-02`: имеющееся evidence — отрицательный тест проекции, SQL Server + PostgreSQL unit и гайды EN/RU; решение принадлежит CHECK.
- `.pdca/collection-405-407/items/405/rv1/do-runs.md`: команды, exit codes, счётчики, полные логи; финальные проверки exit 0, три builds 0 warning/0 error; SQL Server 2109/0/8, PostgreSQL 2084/0/0, целевые 3/0/0 (passed/failed/skipped).
- `.pdca/collection-405-407/items/405/rv1/scoped-design.md`, `.pdca/collection-405-407/items/405/rv1/candidate-baseline.log`: дизайн и совместимость; существующие тесты не изменены.
- CHECK handoff: `{"path": ".pdca/collection-405-407/items/405/rv1/check-83496c84dbe3c1aa7147da93ffadff2af0270d2774f8f1acfc5fad3d25ac4301.md", "sha256": "83496c84dbe3c1aa7147da93ffadff2af0270d2774f8f1acfc5fad3d25ac4301"}`.

## CHECK record

Оригинал независимого CHECK (`ses_df339579affe6oZMvTEqXnCqOx`): `{"path": ".pdca/collection-405-407/items/405/rv1/report-81494fab7ba9cdb4056a117b73824dae1566b98b973b8befe865ee4d3c292762.md", "sha256": "81494fab7ba9cdb4056a117b73824dae1566b98b973b8befe865ee4d3c292762"}`. Вердикт: `not accepted`; `AC-405-01` — `unverified`; `AC-405-02` — `unverified`. Подтверждённых дефектов продукта или критериев нет. Точные основания и запрос: §Criteria matrix, §Findings и §Next authorized transition and consolidated re-gather request оригинала.

`CHECK → DO`: единственный консолидированный evidence-only re-gather завершён; allowance `spent` для `AC-405-01` и `AC-405-02`. Оригинал scout (`ses_df3298c28ffedk3Ko4oioB0a94`): `{"path": ".pdca/collection-405-407/items/405/rv1/report-51cb31ef60bfe4f1131c4df6d526782175dfd9b740f2eb83250db61325320b0a.md", "sha256": "51cb31ef60bfe4f1131c4df6d526782175dfd9b740f2eb83250db61325320b0a"}` (§Tests, §API and guards, §Documentation, §Unconfirmed). Scout наблюдал checkout, Git/хеши не проверял; отдельная идентичность executor: `.pdca/collection-405-407/items/405/rv1/candidate-sources.sha256`, `.pdca/collection-405-407/items/405/rv1/candidate-baseline.log`. Диагностика `.pdca/collection-405-407/items/405/rv1/sqlserver-regather-presence-01.log`: `unset` (Boolean, exit 0), разрешённого live маршрута нет; обе JOIN-мутации не запускались. Прежний CHECK сохранён; новый CHECK ещё не запускался. Пакет recheck: `{"path": ".pdca/collection-405-407/items/405/rv1/check-5842d6ae316fe652cc866e9912545dd6175ebf8e1b47edc6157c3d79ea46c888.md", "sha256": "5842d6ae316fe652cc866e9912545dd6175ebf8e1b47edc6157c3d79ea46c888"}`.

### Recheck #405 — текущий CHECK

Оригинал независимого `pdca-check` (`ses_df308c31cffeUy1oZPgrp4AGSN`): `{"path": ".pdca/collection-405-407/items/405/rv1/report-93e710dca663102774648e4c2121441f55d60272efd9e425b53ffc3f69d740a7.md", "sha256": "93e710dca663102774648e4c2121441f55d60272efd9e425b53ffc3f69d740a7"}`. Вердикт `not accepted`: `AC-405-02=met`; `AC-405-01=unverified` только из-за отсутствия живого исполнения SQL Server. Re-gather allowance `spent`; recoverable CHECK blocker; тот же CHECK активен без дальнейшего перехода, не ACT/STOP. `N=1,r=1,n=1/3`. #405 `disposition=paused`; #405/G1 `in-progress`, #406 `pending`.


### #405 — историческое evidence D404

Оригинал read-only `pdca-scout` (`ses_df2c46319ffePvqrQSa5BwLr7X`): `{"path": ".pdca/collection-405-407/items/405/rv1/report-d16a8255dd55618668a975884bd7c00424aac5560b73aeb5cd97c0e7f9b754f2.md", "sha256": "d16a8255dd55618668a975884bd7c00424aac5560b73aeb5cd97c0e7f9b754f2"}`. Исторические записи `D404-3.md:185,209,233,245,249,253,471,483,486–488` фиксируют успешный authenticated Ubuntu SqlClient `SELECT` через loopback, SQL Server `17.0.5065.9`, 1199 exact IDs (1153 passed, 46 skipped), отдельный запуск с 76 failures без successful execution credit и последующее 89/89. Это последовательные исторические результаты, не текущий прогон.

Точные имя/путь/команда user-space launcher, списки native IDs, детали shared lock, настройки timeout/cgroup и нынешняя доступность из этих строк не установлены. Старые логи `.pdca/D404-isolation` указаны отсутствующими во входе; scout их не читал, источником служили только исторические строки статуса. Текущий endpoint, readiness и ownership не проверялись. Сохраняется имеющийся диагностический факт `unset` из `.pdca/collection-405-407/items/405/rv1/sqlserver-regather-presence-01.log`, без повторной проверки. История не даёт доказательства исполнения, разрешения или основания менять CHECK.


- Диагностика восстановления #405 (только внешние наблюдения), `N=1,r=1,n=1`: `wslc.exe` обнаружил один запущенный контейнер-кандидат с образом SQL Server 2022 и публикацией `127.0.0.1:1433`; TCP из воркера доступен (`rc=0`). `NEXTORM_SQLSERVER_CONNECTION` — `unset`; нужна утверждённая приватная конфигурация только для тестового воркера и подтверждение разрешения и изоляции тестовой БД. Аутентифицированного запроса и подтверждения версии нет. `AC-405-01` остаётся `unverified`; `CHECK` остаётся `paused`, allowance не меняется. Точный отчёт: `{"path": ".pdca/collection-405-407/items/405/rv1/report-5df838cace74165fc979d411b82d82811975a89d1b8a3f31c7196b71c7a13d28.md", "sha256": "5df838cace74165fc979d411b82d82811975a89d1b8a3f31c7196b71c7a13d28"}`.

### #405 — CHECK после восстановления live-маршрута

Оригинал `pdca-check` (`ses_df1d712a6ffets5ZFoC3295Ab5`): `{"path": ".pdca/collection-405-407/items/405/rv1/report-dcf278fa08fc4b880819ffd1b20d4437b81aaca81a48ceae4a2b08c9dc27d1d2.md", "sha256": "dcf278fa08fc4b880819ffd1b20d4437b81aaca81a48ceae4a2b08c9dc27d1d2"}`. Источник вынес `accepted`; `AC-405-01=met`, `AC-405-02=met`, live 2/2 по точным native ID; cleanup подтверждён. N=1/r=1/n=1/3; re-gather spent.

## ACT record

`completed`, `accepted`, `N=1/r=1/n=1`. Исход независимого CHECK: `{"path": ".pdca/collection-405-407/items/405/rv1/report-dcf278fa08fc4b880819ffd1b20d4437b81aaca81a48ceae4a2b08c9dc27d1d2.md", "sha256": "dcf278fa08fc4b880819ffd1b20d4437b81aaca81a48ceae4a2b08c9dc27d1d2"}`; `AC-405-01=met`, `AC-405-02=met`, live 2/2 с точной сверкой и подтверждённой очисткой. Снимок из 11 файлов сохранён в `f7c2905cd13773c9e8cf5d39b9e53bb859ddc964`. У #405 незавершённых критериев нет; следующий по расписанию #406 `pending/ready`, не запущен.

## Lessons learned

`retrospective: complete`; оригинал `pdca-check` из `ses_df1d712a6ffets5ZFoC3295Ab5`, три урока: `{"path": ".pdca/collection-405-407/items/405/rv1/report-a34b53825f60ec01ab23ff4d89d281e600588e0d0e901fb6751287a30b034027.md", "sha256": "a34b53825f60ec01ab23ff4d89d281e600588e0d0e901fb6751287a30b034027"}`.

## Revision history

`none`

## Defect history

- `D405-START-DIAGNOSTICS`, `D405-WINDOWS-PORT-SELECTION`, `D405-CHECK-SCOPED-OBSERVATION`, `D405-HANDOFF-TRANSPORT` — [процессные дефекты #405](../../docs/specs/design/D405-pdca-process-defects.md).

## Changed files

Изменения #405, вне индекса:

- `docs/guide/25-sql-batch.md`
- `docs/ru/guide/25-sql-batch.md`
- `src/nextorm.core/Builders/Bulk/BatchBuilder.cs`
- `src/nextorm.core/Builders/Dml/JoinCaptureProjection.cs`
- `src/nextorm.core/DataContext/DataContext.cs`
- `src/nextorm.core/DataContext/QueryPlanner.cs`
- `src/nextorm.core/DataContext/Sql/SqlBuilder.cs`
- `src/nextorm.core/DataContext/Sql/SqlMutationBuilder.cs`
- `src/nextorm.core/PublicAPI.Unshipped.txt`
- `tests/nextorm.sqlserver.tests/Dml/T405JoinBatchCaptureTests.cs`
- `tests/nextorm.integration.tests/SqlServer/T405JoinBatchCaptureTests.cs`

Статусы item/collection и evidence: `.pdca/collection-405-407/items/405/rv1/`.

## Git state

- Точный ledger 11 файлов принятого snapshot и двух записей статуса (без индексации): `.pdca/collection-405-407/items/405/rv1/act-touched-git-ledger.log`.
status path: `.pdca/status/collection-405-407-item-405.md` — создан, untracked; baseline `c9f2dd90a79a489e8a68a3da16e82be237ab3663`, ветка `pdca/405-407`; без индексации или коммита.

- #405: коммит `f7c2905cd13773c9e8cf5d39b9e53bb859ddc964` на `pdca/405-407`, ровно 11 файлов; индекс после коммита пуст. Оба статуса и отдельная процессная спецификация остаются untracked; evidence вне индекса. Фактический readback: `.pdca/collection-405-407/items/405/rv1/act-commit-readback.log`.

## Progress log

2026-11-24T06:54:34Z | PLAN | revision 1 | attempt 0 | Собственный PLAN #405 закреплён; plan_state ready, DO не начат | .pdca/collection-405-407/items/405/rv1/report-ae18c7a15b0713020a9f1600d93b0fe6cb163e5a35dc3234e65a083eb14cd85b.md
2026-11-24T08:29:39Z | DO | revision 1 | attempt 1 | #405: решение planner закреплено; DO started по сохранённому PLAN r1, расписание без изменений | `{"path": ".pdca/collection-405-407/items/405/rv1/report-18be86f6e03733f2acae31e8a59dc82c7f950c18509adaf9b6b418d00168a073.md", "sha256": "18be86f6e03733f2acae31e8a59dc82c7f950c18509adaf9b6b418d00168a073"}`
2026-11-24T08:31:53Z | DO | revision 1 | attempt 1 | #405: SQL Server config presence unset; live route не подтверждён, live run не начат | .pdca/collection-405-407/items/405/rv1/sqlserver-config-presence.log
2026-11-24T08:55:29Z | DO | revision 1 | attempt 1 | #405: заморожены 11 файлов, локальный run sh_20cc27934001ZCZPKzBIzKWN9z запущен | .pdca/collection-405-407/items/405/rv1/do-runs.md
2026-11-24T08:57:22Z | DO | revision 1 | attempt 1 | #405: реализованный кандидат готов к независимому CHECK; обязательное live SQL Server evidence отсутствует | `{"path": ".pdca/collection-405-407/items/405/rv1/check-83496c84dbe3c1aa7147da93ffadff2af0270d2774f8f1acfc5fad3d25ac4301.md", "sha256": "83496c84dbe3c1aa7147da93ffadff2af0270d2774f8f1acfc5fad3d25ac4301"}`
2026-11-24T09:08:22Z | CHECK | revision 1 | attempt 1 | #405: независимый CHECK not accepted, оба критерия unverified | `{"path": ".pdca/collection-405-407/items/405/rv1/report-81494fab7ba9cdb4056a117b73824dae1566b98b973b8befe865ee4d3c292762.md", "sha256": "81494fab7ba9cdb4056a117b73824dae1566b98b973b8befe865ee4d3c292762"}`
2026-11-24T09:08:22Z | DO | revision 1 | attempt 1 | #405: CHECK → DO evidence-only re-gather pending, allowance open | `{"path": ".pdca/collection-405-407/items/405/rv1/report-81494fab7ba9cdb4056a117b73824dae1566b98b973b8befe865ee4d3c292762.md", "sha256": "81494fab7ba9cdb4056a117b73824dae1566b98b973b8befe865ee4d3c292762"}`
2026-11-24T09:16:28Z | DO | revision 1 | attempt 1 | #405: route diagnostic unset, exit 0; re-gather in-progress, allowance open | .pdca/collection-405-407/items/405/rv1/sqlserver-regather-presence-01.log
2026-11-24T09:25:24Z | DO | revision 1 | attempt 1 | #405: единственный re-gather завершён, allowance spent для AC-405-01/02; пакет recheck готов | `{"path": ".pdca/collection-405-407/items/405/rv1/check-5842d6ae316fe652cc866e9912545dd6175ebf8e1b47edc6157c3d79ea46c888.md", "sha256": "5842d6ae316fe652cc866e9912545dd6175ebf8e1b47edc6157c3d79ea46c888"}`
2026-11-24T10:01:31Z | CHECK | revision 1 | attempt 1 | #405: recheck not accepted; recoverable CHECK blocker, disposition paused, без перехода по вердикту | `{"path": ".pdca/collection-405-407/items/405/rv1/report-93e710dca663102774648e4c2121441f55d60272efd9e425b53ffc3f69d740a7.md", "sha256": "93e710dca663102774648e4c2121441f55d60272efd9e425b53ffc3f69d740a7"}`
2026-11-24T10:53:29Z | CHECK | revision 1 | attempt 1 | #405: закреплено read-only наблюдение возможностей маршрута; recoverable CHECK blocker сохраняется, перехода нет | `{"path": ".pdca/collection-405-407/items/405/rv1/report-220b9b54195f8a61dc3d8e2da44c5e75c7272a9a1f227267ccdd0a649a67effa.md", "sha256": "220b9b54195f8a61dc3d8e2da44c5e75c7272a9a1f227267ccdd0a649a67effa"}`
2026-11-24T11:24:27Z | CHECK | revision 1 | attempt 1 | #405: добавлено только историческое наблюдение D404; recoverable CHECK blocker без изменений | `{"path": ".pdca/collection-405-407/items/405/rv1/report-d16a8255dd55618668a975884bd7c00424aac5560b73aeb5cd97c0e7f9b754f2.md", "sha256": "d16a8255dd55618668a975884bd7c00424aac5560b73aeb5cd97c0e7f9b754f2"}`
2026-11-24T11:30:15Z | CHECK | revision 1 | attempt 1 | #405 N=1: внешняя диагностика записана; TCP rc=0, override unset, пауза и allowance без изменений | .pdca/collection-405-407/items/405/rv1/report-5df838cace74165fc979d411b82d82811975a89d1b8a3f31c7196b71c7a13d28.md
2026-11-24T12:45:40Z | CHECK | revision 1 | attempt 1 | #405 N=1, внешняя диагностика: управление через wslc.exe доступно; прямой Linux-сокет не обнаружен в проверенных настройках и путях; sqlcmd в контейнере есть, настроенная аутентификация без запроса не обнаружена; NEXTORM_SQLSERVER_CONNECTION unset; аутентифицированного запроса и live-теста нет. Прежнее TCP-наблюдение не отменено, повторной проверки не было. CHECK/paused и allowance без изменений | `{"path": ".pdca/collection-405-407/items/405/rv1/report-c700de25df651a98ad817dc86ea54151ac8a342b0341eed2e19539822fbcb1a2.md", "sha256": "c700de25df651a98ad817dc86ea54151ac8a342b0341eed2e19539822fbcb1a2"}`
2026-11-24T12:52:04Z | CHECK | revision 1 | attempt 1 | #405 N=1: пользователь выбрал «нет, давай через wslc»; Podman не разрешён. Внешний инфраструктурный preflight: опции CLI, кэш образов и блокировка проверены; MemoryHigh=3G, MemoryMax=4G, MemorySwapMax=1G не обеспечиваются (user bus недоступна, system scope требует авторизации, текущая cgroup недоступна на запись). Контейнер не менялся, launcher не создан, provisioning и тестов нет; live proof, CHECK/paused и allowance без изменений. Следующий шаг только после демонстрации разрешённой возможности обеспечить все три лимита (user systemd scope или делегированная cgroup v2), затем — временный маршрут с lease и точными native ID. Первичный лог: .pdca/collection-405-407/items/405/rv1/wslc-lease-preflight.log (SHA-256 8ed9404c9de7dc539092d889c4ac3ebcee2b08415593b555961412666e20084e) | `{"path": ".pdca/collection-405-407/items/405/rv1/report-fda9d3f7c72a86d7dedc25f5818af9ee0a34490aaf6e3563feb9b9ec128f0720.md", "sha256": "fda9d3f7c72a86d7dedc25f5818af9ee0a34490aaf6e3563feb9b9ec128f0720"}`
2026-11-24T13:12:12Z | CHECK | revision 1 | attempt 1 | #405 N=1: новое указание пользователя дословно: «эти правила я отменил» (после «я давно снял это обязательные лимиты, проверь»). Узкое исключение только для фиксированных WSLc cgroup MemoryHigh=3G, MemoryMax=4G, MemorySwapMax=1G; правило в документации не менялось. Сохраняются проектный .NET heap cap, общий lease, ограниченный timeout, точные native ID, подтверждённый изолированный endpoint, владение и cleanup; только wslc. Предыдущий preflight — исторический инфраструктурный факт, не дефект продукта. CHECK/paused до конкретного соответствующего маршрута с lease и доказательств; без нового DO/CHECK, изменения лимита повторного сбора, счётчиков или перехода | текущий пользовательский диспетч; .opencode/skills/running-integration-tests/SKILL.md:19
2026-11-24T13:28:25Z | CHECK | revision 1 | attempt 1 | #405: recovery authorization recorded; preflight pending, CHECK paused | `{"path": ".pdca/collection-405-407/items/405/rv1/report-fdc9569d97a38300afc269513366673876cbaa79c5915ef104d9f07cc3c501b4.md", "sha256": "fdc9569d97a38300afc269513366673876cbaa79c5915ef104d9f07cc3c501b4"}`
2026-11-24T14:06:59Z | CHECK | revision 1 | attempt 1 | #405: live recovery не допущен без готового SqlClient readiness worker и проверенного реального launcher | .pdca/collection-405-407/items/405/rv1/recovery-live-worker-precondition.log
2026-11-24T15:11:54Z | CHECK | revision 1 | attempt 1 | Notice: direct fallback takeover; orchestrator interrupted after provider.invalid-output, former nested executor idle (per driver); recovery preflight only, no counter or phase change | .pdca/collection-405-407/items/405/rv1/discovery-roster.json
2026-11-24T15:15:19Z | CHECK | revision 1 | attempt 1 | #405: recovery shell sh_20e1f7095001Q01sgUqD616bJE завершён exit 1; create=0, start=1, remove=0, полное owned ID удалено и отсутствие повторно подтверждено; SQL readiness и тесты не запускались, frozen=0; CHECK/paused, allowance spent без изменений | .pdca/collection-405-407/items/405/rv1/recovery-live-execution.log; .pdca/collection-405-407/items/405/rv1/recovery-start-diagnosis.log
2026-11-24T15:17:48Z | CHECK | revision 1 | attempt 1 | #405: wslc start exit 1 (WSAEADDRINUSE на loopback 48623); единственная проверка исправления на проверенном 23306 — тот же WSAEADDRINUSE. Оба create=0, remove=0, точное отсутствие подтверждено под lease. SqlClient и тесты не запускались; CHECK/paused, allowance spent | .pdca/collection-405-407/items/405/rv1/recovery-live-start-diagnostic-02.log; .pdca/collection-405-407/items/405/rv1/recovery-live-port-23306-03.log; .pdca/collection-405-407/items/405/rv1/recovery-port-diagnosis-02.log
2026-11-24T15:19:42Z | CHECK | revision 1 | attempt 1 | #405: вне wslc Windows ExclusiveAddressUse TcpListener на 127.0.0.1:23306 exit 1 (10048 AddressAlreadyInUse), Ubuntu bind/listen/close exit 0, occupied rows=0 excluded=false. Windows отказ воспроизводится без launcher и контейнера, владелец или источник отказа не установлен; CHECK/paused и allowance без изменений | .pdca/collection-405-407/items/405/rv1/recovery-direct-loopback-bind.log
2026-11-24T15:21:19Z | CHECK | revision 1 | attempt 1 | #405: Windows TcpListener 127.0.0.1:0 exit 0, выделен 60773 под lease; локальный guard 20000..59999 отклонил порт (launcher exit 1), lease освобождён; create/start, SQL и тесты не запускались. CHECK/paused, allowance spent | .pdca/collection-405-407/items/405/rv1/recovery-windows-port0-04.log
2026-11-24T15:24:11Z | CHECK | revision 1 | attempt 1 | #405: live SQL Server JOIN UPDATE/DELETE: exit 0, 2/2 exact native passed, SQL 17.0.5065.9, verified dedicated database and owned teardown; frozen hashes unchanged. Новый независимый вердикт ещё не вынесен; счётчики и allowance spent прежние; полный лог .pdca/collection-405-407/items/405/rv1/recovery-windows-port0-05.log | {"path": ".pdca/collection-405-407/items/405/rv1/check-7c642b756036662b8f7c25e628cff020314e737ebdcb3dfff10a27ddf35a4c22.md", "sha256": "7c642b756036662b8f7c25e628cff020314e737ebdcb3dfff10a27ddf35a4c22"}
2026-11-24T15:33:23Z | CHECK | revision 1 | attempt 1 | #405: независимый accepted зафиксирован, оба критерия met, без изменения кандидата | `{"path": ".pdca/collection-405-407/items/405/rv1/report-dcf278fa08fc4b880819ffd1b20d4437b81aaca81a48ceae4a2b08c9dc27d1d2.md", "sha256": "dcf278fa08fc4b880819ffd1b20d4437b81aaca81a48ceae4a2b08c9dc27d1d2"}`
2026-11-24T15:33:23Z | ACT | revision 1 | attempt 1 | #405: CHECK → ACT, ретроспектива pending, элемент не закрыт | `{"path": ".pdca/collection-405-407/items/405/rv1/check-0f1a815341cd007510dfd0d8351365fc46f26f4babafb94eab0e0899973b4b7e.md", "sha256": "0f1a815341cd007510dfd0d8351365fc46f26f4babafb94eab0e0899973b4b7e"}`
2026-11-24T15:38:32Z | ACT | revision 1 | attempt 1 | #405: независимая ретроспектива закреплена, ACT completed/accepted, commit f7c2905cd13773c9e8cf5d39b9e53bb859ddc964, #406 не запущен | `{"path": ".pdca/collection-405-407/items/405/rv1/report-a34b53825f60ec01ab23ff4d89d281e600588e0d0e901fb6751287a30b034027.md", "sha256": "a34b53825f60ec01ab23ff4d89d281e600588e0d0e901fb6751287a30b034027"}`
