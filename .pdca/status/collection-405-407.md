# Collection status: collection-405-407

collection-id: collection-405-407
form: collection
plan_cap: 4
lane_cap: 4
mode: autonomous

## Current state

### Новый запуск — G1 активирована

- #405: `ACT/completed`, `accepted`, `done`; G1 `done`. #406: `ACT/completed`, `accepted`, `done`, N=1/r=1/n=1. #407 N=2 — `ACT/completed`, `accepted`, `done`, `N=2/r=1/n=1`. #408 `ACT/completed`, `accepted`, `done`, `N=1/r=1/n=1`; все четыре элемента закрыты. Итоговый интеграционный CHECK: `pass` на HEAD `6c73b7442c02a702fc71b83e7bbfe14a10f6b7a9`. Полнота целей: 4/4 whole-goal accepted + ACT/completed, незавершённых нет. Родитель `ACT/completed`, коллекция закрыта с исходом `accepted`: полнота 4/4, интеграционный `pass` записан отдельно. Режим `autonomous`, форма `pdca-collection` сохранены.
- ALL-barrier разрешён. Расписание P.4 закреплено; `G1=in-progress`, первый элемент `#405=in-progress`. Применимость сохранённого PLAN подтверждена. Оригинал решения planner из `ses_df3704de7ffeJp1OMJ8MOK4nRw`: `{"path": ".pdca/collection-405-407/items/405/rv1/report-18be86f6e03733f2acae31e8a59dc82c7f950c18509adaf9b6b418d00168a073.md", "sha256": "18be86f6e03733f2acae31e8a59dc82c7f950c18509adaf9b6b418d00168a073"}`. DO #405 начат (`N=1,r=1,n=1`); первый CHECK завершён, re-gather завершён, пакет recheck готов; #407 остаётся терминальным без группового стопа.
- Автокоммит — только для файлов соответствующего элемента, в ACT после whole-goal CHECK PASS; сейчас коммита нет. Podman не запускать, политику доступа не обходить; разрешённый существующий маршрут для live SQL Server выясняется отдельно в CHECK/DO.
- Ворк­три `/home/alex/sources/nextorm-worktrees/pdca-405-407`, ветка `pdca/405-407`, HEAD `c9f2dd90a79a489e8a68a3da16e82be237ab3663`; существующий статус изменён без stage/commit.

- #405: `done` / `plan_state=ready`; источник `/home/alex/sources/nextorm-worktrees/pdca-405-407/.pdca/collection-405-407/rv1/report-da29dc008e1e39ed68b912853d73972a549e3eee3bca38f73d868ab456a2377a.md:9–22`; черновой статус `/home/alex/sources/nextorm-worktrees/pdca-405-407/.pdca/status/collection-405-407-item-405.md`; evidence root `/home/alex/sources/nextorm-worktrees/pdca-405-407/.pdca/collection-405-407/items/405/rv1/`; бриф `/home/alex/sources/nextorm-worktrees/pdca-405-407/.pdca/collection-405-407/items/405/rv1/item-plan-brief.md`.
- #406: `pending` / `plan_state=ready`; источник `/home/alex/sources/nextorm-worktrees/pdca-405-407/.pdca/collection-405-407/rv1/report-da29dc008e1e39ed68b912853d73972a549e3eee3bca38f73d868ab456a2377a.md:33–46`; черновой статус `/home/alex/sources/nextorm-worktrees/pdca-405-407/.pdca/status/collection-405-407-item-406.md`; evidence root `/home/alex/sources/nextorm-worktrees/pdca-405-407/.pdca/collection-405-407/items/406/rv1/`; бриф `/home/alex/sources/nextorm-worktrees/pdca-405-407/.pdca/collection-405-407/items/406/rv1/item-plan-brief.md`.
- #407 N=2: `pending/ready`, r=1, n=0; P.4 привязана, выполнение ждёт #405 и #406. Статус: `.pdca/status/collection-405-407-item-407-2.md`. #407 N=1 сохранён в исторической проекции.

### История предыдущего запуска

- Фаза: ACT · запись завершена (`completed`) · итог: `blocked` · исход входного PLAN: `acceptance criteria cannot be determined`.
- Контракты: `pdca`, `pdca-collection`; оверлеи: `pdca-coder`, `pdca-dotnet`, `nextorm-pdca`.
- Полнота: не достигнута; принято 0 из 3, индивидуальных терминальных решений 0, попыток DO 0, сбоев исполнения 0. Результатов для интеграции нет; интеграционный вердикт отсутствует.

### #407 — PLAN-pending / reentry (история до переутверждения)

- #407 вправе пройти новый собственный PLAN у collection owner. Прежний финальный ACT и terminal history сохраняются; новый PLAN ещё не выполнен, до планового решения #407 не runnable. Текущая очередь не меняется.

- #405: в окружении executor `NEXTORM_SQLSERVER_CONNECTION` — `unset` (проверено только наличие); разрешённый live SQL Server маршрут не подтверждён. Evidence: `.pdca/collection-405-407/items/405/rv1/sqlserver-config-presence.log`.

- #405: кандидат реализован, заморожен и готов к независимому CHECK; live SQL Server не подтверждён. CHECK завершён: `not accepted`. Точный результат и файлы — в item status. Evidence: `.pdca/collection-405-407/items/405/rv1/candidate-sources.sha256`.

- Пакет CHECK #405: `{"path": ".pdca/collection-405-407/items/405/rv1/check-83496c84dbe3c1aa7147da93ffadff2af0270d2774f8f1acfc5fad3d25ac4301.md", "sha256": "83496c84dbe3c1aa7147da93ffadff2af0270d2774f8f1acfc5fad3d25ac4301"}`.

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

pdca по задачам 405 406 407 автономно автокомит в отдельном ворктри

Входные цели и критерии задач #405, #406, #407: точная ссылка `{"path": ".pdca/collection-405-407/rv1/report-da29dc008e1e39ed68b912853d73972a549e3eee3bca38f73d868ab456a2377a.md", "sha256": "da29dc008e1e39ed68b912853d73972a549e3eee3bca38f73d868ab456a2377a"}`, соответственно `### Issue #405`, `### Issue #406`, `### Issue #407` → `Body (verbatim)` → `## Результат` и `## Приёмка`.

### Дополнение к входу — новый запуск

Источник: новое явное указание пользователя в диспетчерском сообщении этого запуска. Дословно:

> поведение Returing может изменится в процессе реализации

Только для #407: разрешено изменение наблюдаемого поведения `Returning()`. Отдельное требование о батчевом захвате, #405/#406 и исходные тексты задач и критериев не меняются. Дополнительных разрешений на изменение существующих тестов или на Podman нет.

### Дополнение пользователя от 2026-11-24 — батчевый захват #407

Источник: явное пользовательское дополнение в этой сессии от 2026-11-24. Дословно:

```text
но батчевый захват должен изменится, если мы хотим реализовать задучу
это изменение публичной поверхности которое мы принимаем
```

Границы и маршрут из того же сообщения, дословно:

```text
It removes the #407 preservation of old batch-capture behavior constraint and explicitly accepts the necessary #407 public API/behavior change; not a blanket authorization to edit baseline tests, alter other goals, run Podman, or skip acceptance. Parent collection owner will route #407's new own PLAN, not reopen its finalized prior ACT or change the running queue.
```

### Дополнение пользователя — #408

> добавь в эту сессию задачу 408

#408 requested, original/PLAN pending from parent. Не входит в runnable queue или G1; отдельный PLAN и решение по расписанию ещё не записаны. #407 продолжает действующий DO без расширения охвата.

Источник #408 теперь закреплён: `{"path": ".pdca/collection-405-407/items/408/rv1/report-8e0c58229644727e638f60da6e287556702620b28992f6feb12a4fbf5fc72b57.md", "sha256": "8e0c58229644727e638f60da6e287556702620b28992f6feb12a4fbf5fc72b57"}`, §«Точный заголовок и тело», строки 11–26; `408-A1` / `408-A2` — строки 23–24. Дословный запрос и прежняя отметка ожидания сохранены как история.

## PLAN record

- Входной PLAN: `{"path": ".pdca/collection-405-407/rv1/report-b5173d3a20de98d053f98aa67c953ff34308d956cfee1c4ca85178f0dd1f979f.md", "sha256": "b5173d3a20de98d053f98aa67c953ff34308d956cfee1c4ca85178f0dd1f979f"}`.
- Входные факты: `{"path": ".pdca/collection-405-407/rv1/report-da29dc008e1e39ed68b912853d73972a549e3eee3bca38f73d868ab456a2377a.md", "sha256": "da29dc008e1e39ed68b912853d73972a549e3eee3bca38f73d868ab456a2377a"}`.
- Передача контроллера: `{"path": ".pdca/collection-405-407/rv1/report-4a3c1da602ada5a0e7699f72bc3b0b5c4c91b662a766f57dcb4020b26c1f0c19.md", "sha256": "4a3c1da602ada5a0e7699f72bc3b0b5c4c91b662a766f57dcb4020b26c1f0c19"}`. Первоначально pinned ref не создавался: контроллер сообщает, что ему не передали status path. В этой записи оригиналы закреплены и проверены.
- Не хватает явного критерия для обязательного требования #407: «Не менять существующее поведение `Returning()` и батчевого захвата.» Согласно PLAN, оба пункта `## Приёмка` допускают регрессию этих путей. Scout получил полные тела трёх задач через GitHub API; дополнительное чтение не меняет заданный набор. PLAN указывает на необходимость явной инструкции пользователя о критерии; одобрение плана её не заменяет.
- Доступ отдельно: разрешение на Podman не предоставлено, разрешённый доступ к живому SQL Server не подтверждён; не причина терминального PLAN. В этом ACT обращения к SQL Server и запуска Podman не было.
- Индивидуальные PLAN и чартеры отсутствуют; ALL-barrier не пройден, DAG, группы, дорожки и стратегия интеграции не назначались, вектора ревизий нет.

### #408 — подготовка P.2 до добавочной P.4 (история)

PLAN `ready`, `cycle` / `pdca` / `none`, `N=1/r=1/n=0`, `.pdca/status/collection-405-407-item-408.md`. Оригинальный PLAN `{"path": ".pdca/collection-405-407/items/408/rv1/report-55d84bfb834456d0ce73a2ca6a4862d9896c3fe6a213f5b5e49a26aada0cd1b2.md", "sha256": "55d84bfb834456d0ce73a2ca6a4862d9896c3fe6a213f5b5e49a26aada0cd1b2"}`; `plan_handoff` `{"path": ".pdca/collection-405-407/items/408/rv1/plan-handoff.json", "sha256": "5aeca6272f9e43d6ab5a0180fa633cce4dc55c076868f8529ddb7e5aceb91aec"}`. Требуется самостоятельное явное решение post-ALL P.4; действующая очередь, G1 и #407 не изменены. Предложенная docs-only поверхность — `docs/specs/design/D408-dml-capture-provider-matrix.md` и собственная evidence; это не бронь.

### Items

| id | group | branch | status | plan_state | form | contract | overlay | cycle_id | status_path | plan_revision | baseline | footprint_ref | requirement_ref | prerequisite_ref | reason | patch_path | disposition | pending_step_approval_ref | pending_approval_ref | preparation_state | preparation_ref | attempts | terminal |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 405 | G1 | pdca/405-407 | done | ready | cycle | pdca | pdca-coder + pdca-dotnet + nextorm-pdca | collection-405-407-item-405/1 | .pdca/status/collection-405-407-item-405.md | 1 | c9f2dd90a79a489e8a68a3da16e82be237ab3663 | .pdca/collection-405-407/items/405/rv1/report-ae18c7a15b0713020a9f1600d93b0fe6cb163e5a35dc3234e65a083eb14cd85b.md §Границы решения и предпосылки; резерв: .pdca/collection-405-407/rv1/report-8e340857f9f3954c15a4acf56bdf8fb10f679edd44c6b4f36ea67557d6f6936d.md §Расписание / таблица, #405 | .pdca/collection-405-407/rv1/report-da29dc008e1e39ed68b912853d73972a549e3eee3bca38f73d868ab456a2377a.md:3,9–22 | .pdca/collection-405-407/items/405/rv1/report-ae18c7a15b0713020a9f1600d93b0fe6cb163e5a35dc3234e65a083eb14cd85b.md §Ограничения, пробелы и стоп-условия; .pdca/collection-405-407/rv1/report-8e340857f9f3954c15a4acf56bdf8fb10f679edd44c6b4f36ea67557d6f6936d.md §DAG обязательных зависимостей / §Расписание | — | — | finalized | — | — | — | — | 1 | — |
| 406 | G1 | pdca/405-407 | done | ready | cycle | pdca | pdca-coder + pdca-dotnet + nextorm-pdca | collection-405-407-item-406/1 | .pdca/status/collection-405-407-item-406.md | 1 | f7c2905cd13773c9e8cf5d39b9e53bb859ddc964 | .pdca/collection-405-407/items/406/rv1/report-3a717ed352eb0dc04a4f0889d5f378b8a8564b2aaa41d0b32e5adbf23d7fe109.md §Консервативный охват и предпосылки; резерв: .pdca/collection-405-407/rv1/report-8e340857f9f3954c15a4acf56bdf8fb10f679edd44c6b4f36ea67557d6f6936d.md §Расписание / таблица, #406 | .pdca/collection-405-407/rv1/report-da29dc008e1e39ed68b912853d73972a549e3eee3bca38f73d868ab456a2377a.md:27,33–46 | .pdca/collection-405-407/items/406/rv1/report-3a717ed352eb0dc04a4f0889d5f378b8a8564b2aaa41d0b32e5adbf23d7fe109.md §Якорь и ограничения; §Пробелы, риски и стоп-условия; .pdca/collection-405-407/rv1/report-8e340857f9f3954c15a4acf56bdf8fb10f679edd44c6b4f36ea67557d6f6936d.md §DAG обязательных зависимостей / §Расписание | — | — | finalized | — | — | — | — | 1 | — |
| 407 | G1 | pdca/405-407 | done | ready | cycle | pdca | pdca-coder + pdca-dotnet + nextorm-pdca | collection-405-407-item-407/2 | .pdca/status/collection-405-407-item-407-2.md | 1 | ca1f81a230d42f2fc18ce78a05a8ea6193e375f5 | .pdca/collection-405-407/items/407/cycle-2/rv1/report-aeab22bbcbe42b94ee6c2f0f476ee2bbd0854b1026dbec72a6d899ecdbf9ec27.md §Форма и охват; .pdca/collection-405-407/rv1/report-d29da95b17e25f6d46e5a01e8e8baf6119d25bf8f14f9fc422fc26d326e02889.md | .pdca/status/collection-405-407-item-407-2.md §Original task | .pdca/collection-405-407/rv1/report-d29da95b17e25f6d46e5a01e8e8baf6119d25bf8f14f9fc422fc26d326e02889.md §Привязка P.4; положительная приёмка #405 и #406 | — | — | finalized | — | — | — | — | 1 | — |
| 408 | G1 | pdca/405-407 | done | ready | cycle | pdca | none | collection-405-407-item-408/1 | .pdca/status/collection-405-407-item-408.md | 1 | 7309e7e4948c74d8a587ba8f96397536350b7b55 | .pdca/collection-405-407/rv1/report-4af60f9259bac9fcc42fa6bdc9c780623307f34740ff063cf14f1b418bcd4075.md:7 | .pdca/collection-405-407/items/408/rv1/report-8e0c58229644727e638f60da6e287556702620b28992f6feb12a4fbf5fc72b57.md:11–26 | .pdca/collection-405-407/rv1/report-4af60f9259bac9fcc42fa6bdc9c780623307f34740ff063cf14f1b418bcd4075.md:18 | Очередь: завершение и локальный коммит #407, затем applicability и фактический HEAD; не техническая зависимость | — | finalized | — | — | — | — | 1 | — |

`—` означает отсутствие назначения/записи, а не индивидуальное PLAN-решение; источники требований приведены в §Original task.

### COLLECTION SCHEDULE P.4 — прежняя привязка (история)

- Точный оригинал: `{"path": ".pdca/collection-405-407/rv1/report-8e340857f9f3954c15a4acf56bdf8fb10f679edd44c6b4f36ea67557d6f6936d.md", "sha256": "8e340857f9f3954c15a4acf56bdf8fb10f679edd44c6b4f36ea67557d6f6936d"}`.
- Вектор: `[(#405, r=1), (#406, r=1)]`; точные пары по элементам см. «Точные привязки» в закреплённом P.4. `plan_cap=4`, `lane_cap=4`, глубина профиля `3`; фактически одна группа, не более одного активного элемента.
- DAG без подтверждённых обязательных рёбер; сериализация из-за недоказанного непересечения SQL Server batch, не зависимость от успешного результата. Резерв и предпосылки — таблица «Поверхность, зарезервированная за G1» и абзацы «DAG обязательных зависимостей», «Расписание» закреплённого P.4.
- #407 не входит в исполняемый вектор. Используется текущий ворктри/ветка; новых веток, ворктри, merge, push и плановых согласований нет. Автокоммит допустим только после фактического PASS в ACT, не при этой записи.
- Локатор брифа делегированного `G1`: `/home/alex/sources/nextorm-worktrees/pdca-405-407/.pdca/collection-405-407/rv1/g1-lane-dispatch.md`. Запись расписания не запускает runner.

### Кандидат изменения P.4 — #407 N=2 (история до переутверждения)

- Совместимость только в хвосте `G1: #405 → #406 → #407 (N=2)`; точный оригинал planner: `{"path": ".pdca/collection-405-407/rv1/report-d29da95b17e25f6d46e5a01e8e8baf6119d25bf8f14f9fc422fc26d326e02889.md", "sha256": "d29da95b17e25f6d46e5a01e8e8baf6119d25bf8f14f9fc422fc26d326e02889"}`.
- Для активации нужны явно записанное автономное переутверждение planner и отдельная привязка новой P.4. До обоих событий действует прежняя P.4; очередь G1 и исполняемые строки не меняются.
- #405 остаётся в `CHECK/paused`, #406 — `pending/ready`. #407 N=1 сохранён как терминальная история; #407 N=2 — `PLAN ready`, `N=2,r=1,n=0`, ждёт решения по расписанию, вне исполняемой очереди. Статус нового цикла: `.pdca/status/collection-405-407-item-407-2.md`.

### COLLECTION SCHEDULE P.4 — действующая привязка после барьера

- Автономное `approved`: `{"path": ".pdca/collection-405-407/rv1/report-64f8fa256c80ac5dff0ae8a49bebd04bccf2712ac1e641fa28b3fcd88b0f9be9.md", "sha256": "64f8fa256c80ac5dff0ae8a49bebd04bccf2712ac1e641fa28b3fcd88b0f9be9"}`. Неизменяемый кандидат: `{"path": ".pdca/collection-405-407/rv1/report-d29da95b17e25f6d46e5a01e8e8baf6119d25bf8f14f9fc422fc26d326e02889.md", "sha256": "d29da95b17e25f6d46e5a01e8e8baf6119d25bf8f14f9fc422fc26d326e02889"}`. Прежняя P.4 с исходными привязками сохранена в истории.
- Вектор `[(#405,1/1/1),(#406,1/1/0),(#407,2/1/0)]` (`N/r/n`); `plan_cap=4`, `lane_cap=4`. G1: #405 → #406 → #407 (N=2) в текущем ворктри/ветке.
- PLAN #405: `{"path": ".pdca/collection-405-407/items/405/rv1/report-ae18c7a15b0713020a9f1600d93b0fe6cb163e5a35dc3234e65a083eb14cd85b.md", "sha256": "ae18c7a15b0713020a9f1600d93b0fe6cb163e5a35dc3234e65a083eb14cd85b"}`.
- PLAN #406: `{"path": ".pdca/collection-405-407/items/406/rv1/report-3a717ed352eb0dc04a4f0889d5f378b8a8564b2aaa41d0b32e5adbf23d7fe109.md", "sha256": "3a717ed352eb0dc04a4f0889d5f378b8a8564b2aaa41d0b32e5adbf23d7fe109"}`.
- PLAN #407: `{"path": ".pdca/collection-405-407/items/407/cycle-2/rv1/report-aeab22bbcbe42b94ee6c2f0f476ee2bbd0854b1026dbec72a6d899ecdbf9ec27.md", "sha256": "aeab22bbcbe42b94ee6c2f0f476ee2bbd0854b1026dbec72a6d899ecdbf9ec27"}`.

- #405 остаётся CHECK/paused на прежнем замороженном кандидате; #406 pending; #407 N=2 pending/ready за успешной приёмкой обоих. Привязка не разрешает DO, тесты, повторный сбор или обход паузы. Live-блокер не изменён.

### Историческая проекция #407 N=1 — не очередь

| id | group | branch | status | plan_state | form | contract | overlay | cycle_id | status_path | plan_revision | baseline | footprint_ref | requirement_ref | prerequisite_ref | reason | patch_path | disposition | pending_step_approval_ref | pending_approval_ref | preparation_state | preparation_ref | attempts | terminal |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 407 | — | — | incomplete | pending | — | — | — | — | .pdca/status/collection-405-407-item-407.md | — | — | — | — | — | В приёмке нет сохранения батчевого захвата; .pdca/collection-405-407/items/407/rv1/report-f6f2d25046fb94785d6b5eed6bd20681ffa18aa3b94d7360dd3af9f147507333.md | — | finalized | — | — | — | — | 0 | criteria-undetermined |

### COLLECTION SCHEDULE P.4 — добавочная привязка #408

Полный оригинал planner (`ses_df1875f7fffeRUtTU8P5BNOyf5`): `{"path": ".pdca/collection-405-407/rv1/report-4af60f9259bac9fcc42fa6bdc9c780623307f34740ff063cf14f1b418bcd4075.md", "sha256": "4af60f9259bac9fcc42fa6bdc9c780623307f34740ff063cf14f1b418bcd4075"}`. Прежняя P.4 сохранена; добавлена только хвостовая позиция G1: #405 → #406 → #407 (N=2) → #408. Вектор `[#405@N1/r1,#406@N1/r1,#407@N2/r1,#408@N1/r1]`; `plan=4` / `lane=4`, тот же worktree и ветка, без новой топологии.

Для #408 закреплены `docs/specs/design/D408-dml-capture-provider-matrix.md`, `.pdca/status/collection-405-407-item-408.md` и `.pdca/collection-405-407/items/408/`; точные связки plan_handoff / PLAN / original — в решении P.4:11–16. #408 `pending/ready`, `N=1/r=1/n=0`; запуск только после независимой приёмки, полного ACT и разрешённого локального коммита #407, освобождения G1, решения применимости и привязки к фактическому HEAD. База DO пока не назначена; это порядок очереди, не техническая зависимость.

### Groups

| id | items/order | worktree | branch/ref | status |
| --- | --- | --- | --- | --- |
| G1 | #405 → #406 → #407 (N=2) → #408 | /home/alex/sources/nextorm-worktrees/pdca-405-407 | pdca/405-407 | done |

## Approval record

`not applicable: autonomous mode` — нет готового плана.

## DO result

`not reached: PLAN → ACT` — нет работ по задачам, попыток или кандидата.

## CHECK record

`not run: PLAN → ACT` — не было ни индивидуального, ни интеграционного CHECK.

### #405 — независимый CHECK текущего запуска

Оригинал независимого CHECK (`ses_df339579affe6oZMvTEqXnCqOx`): `{"path": ".pdca/collection-405-407/items/405/rv1/report-81494fab7ba9cdb4056a117b73824dae1566b98b973b8befe865ee4d3c292762.md", "sha256": "81494fab7ba9cdb4056a117b73824dae1566b98b973b8befe865ee4d3c292762"}`. Вердикт: `not accepted`; `AC-405-01` — `unverified`; `AC-405-02` — `unverified`. Подтверждённых дефектов продукта или критериев нет. Точные основания и запрос: §Criteria matrix, §Findings и §Next authorized transition and consolidated re-gather request оригинала.

`CHECK → DO`: единственный консолидированный evidence-only re-gather завершён; allowance `spent` для `AC-405-01` и `AC-405-02`. Оригинал scout (`ses_df3298c28ffedk3Ko4oioB0a94`): `{"path": ".pdca/collection-405-407/items/405/rv1/report-51cb31ef60bfe4f1131c4df6d526782175dfd9b740f2eb83250db61325320b0a.md", "sha256": "51cb31ef60bfe4f1131c4df6d526782175dfd9b740f2eb83250db61325320b0a"}` (§Tests, §API and guards, §Documentation, §Unconfirmed). Scout наблюдал checkout, Git/хеши не проверял; отдельная идентичность executor: `.pdca/collection-405-407/items/405/rv1/candidate-sources.sha256`, `.pdca/collection-405-407/items/405/rv1/candidate-baseline.log`. Диагностика `.pdca/collection-405-407/items/405/rv1/sqlserver-regather-presence-01.log`: `unset` (Boolean, exit 0), разрешённого live маршрута нет; обе JOIN-мутации не запускались. Прежний CHECK сохранён; новый CHECK ещё не запускался. Пакет recheck: `{"path": ".pdca/collection-405-407/items/405/rv1/check-5842d6ae316fe652cc866e9912545dd6175ebf8e1b47edc6157c3d79ea46c888.md", "sha256": "5842d6ae316fe652cc866e9912545dd6175ebf8e1b47edc6157c3d79ea46c888"}`. `#405` и `G1` остаются `in-progress`; disposition #405 на момент re-gather — `active`.

### Recheck #405 — текущий CHECK

Оригинал независимого `pdca-check` (`ses_df308c31cffeUy1oZPgrp4AGSN`): `{"path": ".pdca/collection-405-407/items/405/rv1/report-93e710dca663102774648e4c2121441f55d60272efd9e425b53ffc3f69d740a7.md", "sha256": "93e710dca663102774648e4c2121441f55d60272efd9e425b53ffc3f69d740a7"}`. Вердикт `not accepted`: `AC-405-02=met`; `AC-405-01=unverified` только из-за отсутствия живого исполнения SQL Server. Re-gather allowance `spent`; recoverable CHECK blocker; тот же CHECK активен без дальнейшего перехода, не ACT/STOP. `N=1,r=1,n=1/3`. #405 `disposition=paused`; #405/G1 `in-progress`, #406 `pending`.


### #405 — историческое evidence D404

Оригинал read-only `pdca-scout` (`ses_df2c46319ffePvqrQSa5BwLr7X`): `{"path": ".pdca/collection-405-407/items/405/rv1/report-d16a8255dd55618668a975884bd7c00424aac5560b73aeb5cd97c0e7f9b754f2.md", "sha256": "d16a8255dd55618668a975884bd7c00424aac5560b73aeb5cd97c0e7f9b754f2"}`. Исторические записи `D404-3.md:185,209,233,245,249,253,471,483,486–488` фиксируют успешный authenticated Ubuntu SqlClient `SELECT` через loopback, SQL Server `17.0.5065.9`, 1199 exact IDs (1153 passed, 46 skipped), отдельный запуск с 76 failures без successful execution credit и последующее 89/89. Это последовательные исторические результаты, не текущий прогон.

Точные имя/путь/команда user-space launcher, списки native IDs, детали shared lock, настройки timeout/cgroup и нынешняя доступность из этих строк не установлены. Старые логи `.pdca/D404-isolation` указаны отсутствующими во входе; scout их не читал, источником служили только исторические строки статуса. Текущий endpoint, readiness и ownership не проверялись. Сохраняется имеющийся диагностический факт `unset` из `.pdca/collection-405-407/items/405/rv1/sqlserver-regather-presence-01.log`, без повторной проверки. История не даёт доказательства исполнения, разрешения или основания менять CHECK.


### General verification and recovery

- Интеграционная проверка: `blocked`; не дошла до запуска, кандидата нет. Команд, exit status и логов интеграции нет.
- Дефекты интеграции, исправления и корректирующий цикл: отсутствуют.
- Следующих действий в этом запуске нет; повторный вход требует отдельной авторизации и устранения причины входного PLAN.

- Диагностика восстановления #405 (только внешние наблюдения), `N=1,r=1,n=1`: `wslc.exe` обнаружил один запущенный контейнер-кандидат с образом SQL Server 2022 и публикацией `127.0.0.1:1433`; TCP из воркера доступен (`rc=0`). `NEXTORM_SQLSERVER_CONNECTION` — `unset`; нужна утверждённая приватная конфигурация только для тестового воркера и подтверждение разрешения и изоляции тестовой БД. Аутентифицированного запроса и подтверждения версии нет. `AC-405-01` остаётся `unverified`; `CHECK` остаётся `paused`, allowance не меняется. Точный отчёт: `{"path": ".pdca/collection-405-407/items/405/rv1/report-5df838cace74165fc979d411b82d82811975a89d1b8a3f31c7196b71c7a13d28.md", "sha256": "5df838cace74165fc979d411b82d82811975a89d1b8a3f31c7196b71c7a13d28"}`.

### #405 — CHECK после восстановления live-маршрута

Оригинал `pdca-check` (`ses_df1d712a6ffets5ZFoC3295Ab5`): `{"path": ".pdca/collection-405-407/items/405/rv1/report-dcf278fa08fc4b880819ffd1b20d4437b81aaca81a48ceae4a2b08c9dc27d1d2.md", "sha256": "dcf278fa08fc4b880819ffd1b20d4437b81aaca81a48ceae4a2b08c9dc27d1d2"}`. Источник вынес `accepted`; `AC-405-01=met`, `AC-405-02=met`, live 2/2 по точным native ID; cleanup подтверждён. N=1/r=1/n=1/3; re-gather spent.

### #406 — исход независимого CHECK

Оригинал `pdca-check` (`ses_df1adbddcffexXL40sZlHRyYdR`): `{"path": ".pdca/collection-405-407/items/406/rv1/report-fc52db93d37291e2f2ed57443a1e9386ad12dad7c9c6a793254cf5f172596b5a.md", "sha256": "fc52db93d37291e2f2ed57443a1e9386ad12dad7c9c6a793254cf5f172596b5a"}`. `accepted`, `406-A1=met`, `406-A2=met`; SQL Server unit 17/17, live 2/2 по точным native ID, одна команда SqlClient и один roundtrip в каждом кейсе; удаление подтверждено. Кандидат final-03, `N=1/r=1/n=1/3`.

### #407 N=2 — исход независимого CHECK

Оригинал `pdca-check` (`ses_df184f89effeV6oOdKI6aOBzeU`): `{"path": ".pdca/collection-405-407/items/407/cycle-2/rv1/report-b6ee489f6e1f734898bddcf9183636fbcbbd0e7c554eff73e18bb495801aec24.md", "sha256": "b6ee489f6e1f734898bddcf9183636fbcbbd0e7c554eff73e18bb495801aec24"}`. `accepted`, `407-C1=met`, `407-C2=met`; unit 46/46, live 4/4 по точным native ID (две формы #407 и регрессии #266/#406), очистка подтверждена. Неизменный final-03, 13 файлов; `N=2/r=1/n=1/3`.

### #408 — исход независимого CHECK

Оригинал `pdca-check` (`ses_df166efe0ffeQbQMenpnGLBEbj`): `{"path": ".pdca/collection-405-407/items/408/rv1/report-378c21db51ec631fa36ff82da008fbc543154140ba9964fc9628278b3015ebb1.md", "sha256": "378c21db51ec631fa36ff82da008fbc543154140ba9964fc9628278b3015ebb1"}`. `accepted`, `408-A1=met`, `408-A2=met`; один неизменный документ, SHA-256 `ea20a5736a5542a352a06df5fab6eba3b89edcf8dacd291e31f65c767fabe498`, четыре выполненные SQLite-пробы, остальные движки — документированные воспроизводимые сценарии без live-прогона. `N=1/r=1/n=1/3`.

### Итоговый интеграционный CHECK — готов к передаче

`pending`, независимый CHECK ещё не запускался. Готовый locator: `{"path": ".pdca/collection-405-407/rv1/check-15ea5d809294ddc11d79461c55563432f0328ac68c5979830b78d9332d94d0e7.md", "sha256": "15ea5d809294ddc11d79461c55563432f0328ac68c5979830b78d9332d94d0e7"}`. Финальные HEAD/tree и диапазоны путей: `.pdca/collection-405-407/rv1/final-integrated-tree.log`, `.pdca/collection-405-407/rv1/final-integrated-path-ranges.log`. G1 `done`, все четыре элемента закрыты; один линейный worktree, без merge, пропущенных групп нет. Нет отдельного счётчика попыток интеграции.

### General verification and recovery — итог текущего запуска

- Integrated-tree `pass`: оригинал `pdca-check` (`ses_df15f1474ffe8zJBTPQDqIpujo`) `{"path": ".pdca/collection-405-407/rv1/report-af1c96a5d41dc72081e056f360ae58a80a7252b86d32791b3438ec4d8b14dfec.md", "sha256": "af1c96a5d41dc72081e056f360ae58a80a7252b86d32791b3438ec4d8b14dfec"}`. HEAD `6c73b7442c02a702fc71b83e7bbfe14a10f6b7a9`, tree `c4f308d27b4a09e7dcd94500009bc6f26b5d0f38`; G1 `done`, четыре принятых элемента, 8 критериев и интеграция `met`. CHECK выбрал сохранённые доказательства, новых запусков не требовал.
- Полнота целей отдельно: 4/4, незавершённых/пропущенных групп нет. Дефекта интеграции или corrective cycle не зафиксировано. Следующий разрешённый шаг — parent-only ретроспектива, не запуск тестов.

## ACT record

Ранний ACT (`PLAN → ACT`) завершён после записи независимой ретроспективы. Итог `blocked`: `acceptance criteria cannot be determined`; исход входного PLAN сохранён, результат не принят. Незавершённый объём: #405, #406, #407 целиком, без оценок отдельных критериев. Ворк­три и ветка остаются на месте; интеграция и очистка не выполнялись.

### #405 — закрытие ACT элемента

`ACT/completed`, `accepted`, `N=1/r=1/n=1`; CHECK `{"path": ".pdca/collection-405-407/items/405/rv1/report-dcf278fa08fc4b880819ffd1b20d4437b81aaca81a48ceae4a2b08c9dc27d1d2.md", "sha256": "dcf278fa08fc4b880819ffd1b20d4437b81aaca81a48ceae4a2b08c9dc27d1d2"}`, ретроспектива `{"path": ".pdca/collection-405-407/items/405/rv1/report-a34b53825f60ec01ab23ff4d89d281e600588e0d0e901fb6751287a30b034027.md", "sha256": "a34b53825f60ec01ab23ff4d89d281e600588e0d0e901fb6751287a30b034027"}`. Коммит только 11 принятых файлов: `f7c2905cd13773c9e8cf5d39b9e53bb859ddc964`. #406 `pending/ready`; интеграционный CHECK коллекции не проводился.

### #406 — закрытие ACT элемента

`ACT/completed`, `accepted`, `N=1/r=1/n=1`; CHECK `{"path": ".pdca/collection-405-407/items/406/rv1/report-fc52db93d37291e2f2ed57443a1e9386ad12dad7c9c6a793254cf5f172596b5a.md", "sha256": "fc52db93d37291e2f2ed57443a1e9386ad12dad7c9c6a793254cf5f172596b5a"}`, ретроспектива `{"path": ".pdca/collection-405-407/items/406/rv1/report-1feb6e11f25d0463eef05173d70289179ae2dee899a405666c16e2183d1720a4.md", "sha256": "1feb6e11f25d0463eef05173d70289179ae2dee899a405666c16e2183d1720a4"}`. Коммит `ca1f81a230d42f2fc18ce78a05a8ea6193e375f5` включает семь принятых файлов. #407 N=2 `pending/ready`; интеграционный CHECK коллекции не проводился.

### #407 N=2 — закрытие ACT элемента

`ACT/completed`, `accepted`, `N=2/r=1/n=1`; CHECK `{"path": ".pdca/collection-405-407/items/407/cycle-2/rv1/report-b6ee489f6e1f734898bddcf9183636fbcbbd0e7c554eff73e18bb495801aec24.md", "sha256": "b6ee489f6e1f734898bddcf9183636fbcbbd0e7c554eff73e18bb495801aec24"}`, ретроспектива `{"path": ".pdca/collection-405-407/items/407/cycle-2/rv1/report-782e5cb9799f6eb1f875412427208c2db91245c7a279664c212967ed815299f7.md", "sha256": "782e5cb9799f6eb1f875412427208c2db91245c7a279664c212967ed815299f7"}`. Коммит `7309e7e4948c74d8a587ba8f96397536350b7b55` включает 13 принятых файлов. G1 сохраняет добавочную P.4; #408 `pending/ready`, фактический HEAD для будущей применимости `7309e7e4948c74d8a587ba8f96397536350b7b55`. Интеграционный CHECK коллекции не проводился.

### #408 — закрытие ACT элемента

`completed`, `accepted`, `N=1/r=1/n=1`; CHECK `{"path": ".pdca/collection-405-407/items/408/rv1/report-378c21db51ec631fa36ff82da008fbc543154140ba9964fc9628278b3015ebb1.md", "sha256": "378c21db51ec631fa36ff82da008fbc543154140ba9964fc9628278b3015ebb1"}`, ретроспектива `{"path": ".pdca/collection-405-407/items/408/rv1/report-64957db2d9ef10fdd34ffc429734ca125ece360febda58d78c5ac4f69b2b2813.md", "sha256": "64957db2d9ef10fdd34ffc429734ca125ece360febda58d78c5ac4f69b2b2813"}`, коммит одного документа `6c73b7442c02a702fc71b83e7bbfe14a10f6b7a9`. Все элементы G1 закрыты, отдельный интеграционный CHECK не запускался.

### Текущий запуск — C → ACT

`ACT/completed`, исход `accepted`. Полнота: все четыре whole-goal результата приняты и закрыты, незавершённых нет. Отдельно: integrated-tree `pass` по `{"path": ".pdca/collection-405-407/rv1/report-af1c96a5d41dc72081e056f360ae58a80a7252b86d32791b3438ec4d8b14dfec.md", "sha256": "af1c96a5d41dc72081e056f360ae58a80a7252b86d32791b3438ec4d8b14dfec"}`, HEAD `6c73b7442c02a702fc71b83e7bbfe14a10f6b7a9`, tree `c4f308d27b4a09e7dcd94500009bc6f26b5d0f38`. G1 `done`, не `merged`. Родительская ретроспектива закреплена в §Lessons learned. Ворктри, ветка, статусы и evidence сохранены; новых проверок, очистки и удалённых операций не было. #408: последний readback `OPEN / In progress` неизменён.

## Lessons learned

- Текущий запуск: `retrospective: complete`; полный независимый parent-only оригинал `pdca-check` (`ses_df15f1474ffe8zJBTPQDqIpujo`): `{"path": ".pdca/collection-405-407/rv1/report-de474247b9680cb3b1e636cf2b5db92c2e0d2ab830f53cfe829612d0c33ec550.md", "sha256": "de474247b9680cb3b1e636cf2b5db92c2e0d2ab830f53cfe829612d0c33ec550"}`. Три урока, новых дефектов навыка не установлено; прежние ключи сохранены, compaction — событие хоста. Анализ ограничен оркестрацией коллекции и не заменяет ретроспективы элементов. Историческая запись сохранена.

`retrospective: complete` — независимый отчёт `pdca-check`, оригинал сессии `ses_df3bc9de9ffevc2dqlHn1mhTmA`: `{"path": ".pdca/collection-405-407/rv1/report-53121eeaae93126912057e6df907f3f0774467f520e9641ae3cb968d0469c430.md", "sha256": "53121eeaae93126912057e6df907f3f0774467f520e9641ae3cb968d0469c430"}`.

## Revision history

Ревизий отдельных задач и общего вектора нет; коллекция не добавляет счётчики цикла.

## Defect history

- `PDCA-405-407-LATE-PLAN-PIN`, `PDCA-405-407-BATCH-TERMINAL-SCOPE` — [спецификация процессных дефектов](../../docs/specs/design/405-407-pdca-process-defects.md).

- `D405-START-DIAGNOSTICS`, `D405-WINDOWS-PORT-SELECTION`, `D405-CHECK-SCOPED-OBSERVATION`, `D405-HANDOFF-TRANSPORT` — [процессные дефекты #405](../../docs/specs/design/D405-pdca-process-defects.md).

- `D406-INDIRECT-COMMAND-ORACLE` — [процессный дефект #406](../../docs/specs/design/D406-pdca-process-defects.md).

## Changed files

Только статус, пакет и журналы в `.pdca/collection-405-407/rv1/`, локальная копия `.pdca/scripts/status_lint.py` и внутренний процессный разбор `docs/specs/design/405-407-pdca-process-defects.md`; продукт не изменялся.

## Git state

- Worktree: `/home/alex/sources/nextorm-worktrees/pdca-405-407`; branch: `pdca/405-407`; baseline и проверенный HEAD: `c9f2dd90a79a489e8a68a3da16e82be237ab3663`.
- `.pdca/status/collection-405-407.md`: создан в этом запуске, untracked; ничего не индексировано или закоммичено этим ACT. `docs/specs/design/405-407-pdca-process-defects.md`: создан при закрытии, untracked.

- #405: коммит `f7c2905cd13773c9e8cf5d39b9e53bb859ddc964` на `pdca/405-407`, ровно 11 файлов; индекс после коммита пуст. Оба статуса и отдельная процессная спецификация остаются untracked; evidence вне индекса. Фактический readback: `.pdca/collection-405-407/items/405/rv1/act-commit-readback.log`.

- #406: `ca1f81a230d42f2fc18ce78a05a8ea6193e375f5` на `pdca/405-407`, ровно 7 файлов; индекс пуст. Статусы и отдельная процессная спецификация остались untracked, evidence вне индекса. Readback: `.pdca/collection-405-407/items/406/rv1/act-commit-readback.log`.

- #407 N=2: `7309e7e4948c74d8a587ba8f96397536350b7b55` на `pdca/405-407`, ровно 13 файлов; индекс пуст, исходные статусы и evidence не включены. Readback: `.pdca/collection-405-407/items/407/cycle-2/rv1/act-commit-readback.log`.

- #408: `6c73b7442c02a702fc71b83e7bbfe14a10f6b7a9` на `pdca/405-407`, ровно один файл, индекс пуст; статус и evidence не включены. Readback: `.pdca/collection-405-407/items/408/rv1/act-commit-readback.log`.

- Финальный статус коллекции на диске: `.pdca/status/collection-405-407.md` — обновлён, `untracked`; не индексирован и не закоммичен. Замороженный HEAD и tree сохранены.

## Progress log

2026-11-24T06:33:47Z | PLAN | revision — | attempt — | Записан терминальный исход входного PLAN; индивидуальных PLAN нет | .pdca/collection-405-407/rv1/report-b5173d3a20de98d053f98aa67c953ff34308d956cfee1c4ca85178f0dd1f979f.md
2026-11-24T06:33:47Z | ACT | revision — | attempt — | Начат ранний ACT; DO и CHECK не запускались; ожидается ретроспектива коллекции | .pdca/collection-405-407/rv1/retrospective-brief.md
2026-11-24T06:37:13Z | ACT | revision — | attempt — | ACT завершён: blocked; исход входного PLAN сохранён, задачи #405–#407 не приняты | .pdca/collection-405-407/rv1/report-53121eeaae93126912057e6df907f3f0774467f520e9641ae3cb968d0469c430.md
2026-11-24T06:48:27Z | PLAN | revision — | attempt — | Открыта подготовка нового запуска; пути трёх индивидуальных PLAN закреплены, все pending | §Новый запуск — подготовка
2026-11-24T06:51:25Z | PLAN | revision — | attempt — | Запущен собственный PLAN #405; item pending, plan_state running, attempts=0 | ses_df3ada75dffeR1bxiDwzy6S8MS
2026-11-24T06:51:25Z | PLAN | revision — | attempt — | Запущен собственный PLAN #406; item pending, plan_state running, attempts=0 | ses_df3ada75bffeFoKRiDhNsYaDOp
2026-11-24T06:51:25Z | PLAN | revision — | attempt — | Запущен собственный PLAN #407; item pending, plan_state running, attempts=0 | ses_df3ada70effe8WxYd14IgPJVRB
2026-11-24T06:53:50Z | PLAN | revision — | attempt — | #407: терминальный PLAN → ACT; барьер ждёт завершения ACT, терминальная проекция ещё не внесена | .pdca/status/collection-405-407-item-407.md · .pdca/collection-405-407/items/407/rv1/report-f6f2d25046fb94785d6b5eed6bd20681ffa18aa3b94d7360dd3af9f147507333.md
2026-11-24T06:54:34Z | PLAN | revision — | attempt — | #405: собственный PLAN готов; item pending, attempts=0; барьер не пройден | .pdca/status/collection-405-407-item-405.md · .pdca/collection-405-407/items/405/rv1/report-ae18c7a15b0713020a9f1600d93b0fe6cb163e5a35dc3234e65a083eb14cd85b.md
2026-11-24T06:55:16Z | PLAN | revision — | attempt — | #406: собственный PLAN готов; item pending, attempts=0; барьер ждёт ACT #407 | .pdca/status/collection-405-407-item-406.md · .pdca/collection-405-407/items/406/rv1/report-3a717ed352eb0dc04a4f0889d5f378b8a8564b2aaa41d0b32e5adbf23d7fe109.md
2026-11-24T06:57:02Z | ACT | revision — | attempt — | #407: завершён индивидуальный ACT; терминальная проекция без группового стопа | .pdca/status/collection-405-407-item-407.md · .pdca/collection-405-407/items/407/rv1/report-4a3c881bd2edf8c771791a180e60700fde4374fccad4033a036b4fc182f74a38.md
2026-11-24T06:57:02Z | PLAN | revision — | attempt — | ALL-barrier разрешён: #405/#406 ready, #407 criteria-undetermined; расписание не выбрано | .pdca/collection-405-407/items/407/rv1/barrier-verify.log
2026-11-24T07:00:33Z | PLAN | revision — | attempt — | P.4 закреплён; G1 pending, runner не запущен | .pdca/collection-405-407/rv1/report-8e340857f9f3954c15a4acf56bdf8fb10f679edd44c6b4f36ea67557d6f6936d.md · .pdca/collection-405-407/rv1/g1-lane-dispatch.md
2026-11-24T07:03:54Z | PLAN | revision 1 | attempt 0 | G1 и #405: pending → in-progress; применимость и привязки ожидают подтверждения scout + planner; item DO не запущен | .pdca/collection-405-407/rv1/report-8e340857f9f3954c15a4acf56bdf8fb10f679edd44c6b4f36ea67557d6f6936d.md
2026-11-24T07:31:44Z | PLAN | revision — | attempt — | #407: PLAN-pending / reentry; новый собственный PLAN маршрутизирует collection owner, PLAN ещё не выполнен; до планового решения не runnable; прежний финальный ACT и terminal history сохранены, очередь неизменна | §Current state → #407 — PLAN-pending / reentry · §Original task → Дополнение пользователя от 2026-11-24 — батчевый захват #407
2026-11-24T07:41:32Z | PLAN | revision 1 | attempt 0 | #405: получены факты применимости от pdca-scout; решение planner pending | `{"path": ".pdca/collection-405-407/items/405/rv1/report-d680959c90cfb752a85e306e46b8fdbe89598ba1ef46a289fa4b30d34020e0f1.md", "sha256": "d680959c90cfb752a85e306e46b8fdbe89598ba1ef46a289fa4b30d34020e0f1"}`
2026-11-24T07:52:07Z | PLAN | revision 1 | attempt 0 | #405: Разрешённый существующий live SQL Server маршрут не подтверждён; состояние и значение `NEXTORM_SQLSERVER_CONNECTION` не проверялись, значение не раскрывалось. Podman, ручной wslc, runner, endpoint probes и тесты не запускались. Если разрешённого маршрута нет, реализовать candidate в рамках сохранённого PLAN r1 и передать независимому CHECK с точным пробелом в доказательствах live SQL Server. Это не разрешение на Podman, изменение тестов или объёма, PASS либо commit. Решение planner о применимости остаётся `pending`; DO не начат. | `{"path": ".pdca/collection-405-407/items/405/rv1/report-ba46d860ba0b0307a9e64be29426493d31345a23c580477a0e47d8f27ae14959.md", "sha256": "ba46d860ba0b0307a9e64be29426493d31345a23c580477a0e47d8f27ae14959"}`
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
2026-11-24T11:23:26Z | PLAN | revision 1 | attempt 0 | #407 N=2: parent-owned PLAN зарегистрирован из ses_df2cbc1f6ffeBZNiLIb2S5cqqt; ready, pending post-barrier compatibility/schedule decision; DO не начат | .pdca/status/collection-405-407-item-407-2.md · `{"path": ".pdca/collection-405-407/items/407/cycle-2/rv1/report-aeab22bbcbe42b94ee6c2f0f476ee2bbd0854b1026dbec72a6d899ecdbf9ec27.md", "sha256": "aeab22bbcbe42b94ee6c2f0f476ee2bbd0854b1026dbec72a6d899ecdbf9ec27"}` · §Original task → обе поправки #407
2026-11-24T11:24:27Z | CHECK | revision 1 | attempt 1 | #405: добавлено только историческое наблюдение D404; recoverable CHECK blocker без изменений | `{"path": ".pdca/collection-405-407/items/405/rv1/report-d16a8255dd55618668a975884bd7c00424aac5560b73aeb5cd97c0e7f9b754f2.md", "sha256": "d16a8255dd55618668a975884bd7c00424aac5560b73aeb5cd97c0e7f9b754f2"}`
2026-11-24T11:30:15Z | CHECK | revision 1 | attempt 1 | #405 N=1: внешняя диагностика записана; TCP rc=0, override unset, пауза и allowance без изменений | .pdca/collection-405-407/items/405/rv1/report-5df838cace74165fc979d411b82d82811975a89d1b8a3f31c7196b71c7a13d28.md
2026-11-24T11:32:13Z | PLAN | revision — | attempt — | #407 N=2: совместимый кандидат хвоста G1; ждёт явного автономного переутверждения и новой привязки P.4; очередь не изменена | .pdca/collection-405-407/rv1/report-d29da95b17e25f6d46e5a01e8e8baf6119d25bf8f14f9fc422fc26d326e02889.md
2026-11-24T11:35:26Z | PLAN | revision — | attempt — | Переутверждённая P.4 привязана; G1 #405 → #406 → #407 N=2; CHECK #405 остаётся paused | .pdca/collection-405-407/rv1/report-64f8fa256c80ac5dff0ae8a49bebd04bccf2712ac1e641fa28b3fcd88b0f9be9.md · .pdca/collection-405-407/rv1/report-d29da95b17e25f6d46e5a01e8e8baf6119d25bf8f14f9fc422fc26d326e02889.md
2026-11-24T12:45:40Z | CHECK | revision 1 | attempt 1 | #405 N=1, внешняя диагностика: управление через wslc.exe доступно; прямой Linux-сокет не обнаружен в проверенных настройках и путях; sqlcmd в контейнере есть, настроенная аутентификация без запроса не обнаружена; NEXTORM_SQLSERVER_CONNECTION unset; аутентифицированного запроса и live-теста нет. Прежнее TCP-наблюдение не отменено, повторной проверки не было. CHECK/paused и allowance без изменений | `{"path": ".pdca/collection-405-407/items/405/rv1/report-c700de25df651a98ad817dc86ea54151ac8a342b0341eed2e19539822fbcb1a2.md", "sha256": "c700de25df651a98ad817dc86ea54151ac8a342b0341eed2e19539822fbcb1a2"}`
2026-11-24T12:52:04Z | CHECK | revision 1 | attempt 1 | #405 N=1: пользователь выбрал «нет, давай через wslc»; Podman не разрешён. Внешний инфраструктурный preflight: опции CLI, кэш образов и блокировка проверены; MemoryHigh=3G, MemoryMax=4G, MemorySwapMax=1G не обеспечиваются (user bus недоступна, system scope требует авторизации, текущая cgroup недоступна на запись). Контейнер не менялся, launcher не создан, provisioning и тестов нет; live proof, CHECK/paused и allowance без изменений. Следующий шаг только после демонстрации разрешённой возможности обеспечить все три лимита (user systemd scope или делегированная cgroup v2), затем — временный маршрут с lease и точными native ID. Первичный лог: .pdca/collection-405-407/items/405/rv1/wslc-lease-preflight.log (SHA-256 8ed9404c9de7dc539092d889c4ac3ebcee2b08415593b555961412666e20084e) | `{"path": ".pdca/collection-405-407/items/405/rv1/report-fda9d3f7c72a86d7dedc25f5818af9ee0a34490aaf6e3563feb9b9ec128f0720.md", "sha256": "fda9d3f7c72a86d7dedc25f5818af9ee0a34490aaf6e3563feb9b9ec128f0720"}`
2026-11-24T13:12:12Z | CHECK | revision 1 | attempt 1 | #405 N=1: новое указание пользователя дословно: «эти правила я отменил» (после «я давно снял это обязательные лимиты, проверь»). Узкое исключение только для фиксированных WSLc cgroup MemoryHigh=3G, MemoryMax=4G, MemorySwapMax=1G; правило в документации не менялось. Сохраняются проектный .NET heap cap, общий lease, ограниченный timeout, точные native ID, подтверждённый изолированный endpoint, владение и cleanup; только wslc. Предыдущий preflight — исторический инфраструктурный факт, не дефект продукта. CHECK/paused до конкретного соответствующего маршрута с lease и доказательств; без нового DO/CHECK, изменения лимита повторного сбора, счётчиков или перехода | текущий пользовательский диспетч; .opencode/skills/running-integration-tests/SKILL.md:19
2026-11-24T13:28:25Z | CHECK | revision 1 | attempt 1 | #405: recovery authorization recorded; preflight pending, CHECK paused | `{"path": ".pdca/collection-405-407/items/405/rv1/report-fdc9569d97a38300afc269513366673876cbaa79c5915ef104d9f07cc3c501b4.md", "sha256": "fdc9569d97a38300afc269513366673876cbaa79c5915ef104d9f07cc3c501b4"}`
2026-11-24T14:06:59Z | CHECK | revision 1 | attempt 1 | #405: live recovery не допущен без готового SqlClient readiness worker и проверенного реального launcher | .pdca/collection-405-407/items/405/rv1/recovery-live-worker-precondition.log
2026-11-24T15:33:23Z | CHECK | revision 1 | attempt 1 | #405: независимый accepted зафиксирован, оба критерия met, без изменения кандидата | `{"path": ".pdca/collection-405-407/items/405/rv1/report-dcf278fa08fc4b880819ffd1b20d4437b81aaca81a48ceae4a2b08c9dc27d1d2.md", "sha256": "dcf278fa08fc4b880819ffd1b20d4437b81aaca81a48ceae4a2b08c9dc27d1d2"}`
2026-11-24T15:33:23Z | ACT | revision 1 | attempt 1 | #405: CHECK → ACT, ретроспектива pending, элемент не закрыт | `{"path": ".pdca/collection-405-407/items/405/rv1/check-0f1a815341cd007510dfd0d8351365fc46f26f4babafb94eab0e0899973b4b7e.md", "sha256": "0f1a815341cd007510dfd0d8351365fc46f26f4babafb94eab0e0899973b4b7e"}`
2026-11-24T15:38:32Z | ACT | revision 1 | attempt 1 | #405: независимая ретроспектива закреплена, ACT completed/accepted, commit f7c2905cd13773c9e8cf5d39b9e53bb859ddc964, #406 не запущен | `{"path": ".pdca/collection-405-407/items/405/rv1/report-a34b53825f60ec01ab23ff4d89d281e600588e0d0e901fb6751287a30b034027.md", "sha256": "a34b53825f60ec01ab23ff4d89d281e600588e0d0e901fb6751287a30b034027"}`
2026-11-24T15:48:05Z | DO | revision 1 | attempt 1 | #406: DO started; r1 unchanged, baseline f7c2905cd13773c9e8cf5d39b9e53bb859ddc964; applicability {"path": ".pdca/collection-405-407/items/406/rv1/report-9da7728fc59402835a5403e48285158d6d826acb456e6a606494f2028c5bf8a3.md", "sha256": "9da7728fc59402835a5403e48285158d6d826acb456e6a606494f2028c5bf8a3"}, scoped scout {"path": ".pdca/collection-405-407/items/406/rv1/report-0c29f27639d303e0d09573d8f0ca317ee2a58205afd90eec8e62a104d3dbf7a3.md", "sha256": "0c29f27639d303e0d09573d8f0ca317ee2a58205afd90eec8e62a104d3dbf7a3"} | .pdca/collection-405-407/items/406/rv1/do-start-refs.log
2026-11-24T16:03:14Z | DO | revision 1 | attempt 1 | #406: кандидат заморожен, SQL Server unit 17/17, live 2/2 по native ID, одна команда в каждом кейсе, cleanup подтверждён; готов CHECK | `{"path": ".pdca/collection-405-407/items/406/rv1/check-b8aabbde1342d80bb8cf997377ce6e6b5fe175817c73cc4807b188c1e0a08734.md", "sha256": "b8aabbde1342d80bb8cf997377ce6e6b5fe175817c73cc4807b188c1e0a08734"}`
2026-11-24T16:13:58Z | CHECK | revision 1 | attempt 1 | #406: независимый accepted закреплён, оба критерия met | `{"path": ".pdca/collection-405-407/items/406/rv1/report-fc52db93d37291e2f2ed57443a1e9386ad12dad7c9c6a793254cf5f172596b5a.md", "sha256": "fc52db93d37291e2f2ed57443a1e9386ad12dad7c9c6a793254cf5f172596b5a"}`
2026-11-24T16:13:58Z | ACT | revision 1 | attempt 1 | #406: CHECK → ACT/open, ретроспектива pending, элемент не закрыт | `{"path": ".pdca/collection-405-407/items/406/rv1/check-0cba5f26a201b09b2993bdaac48ac50556e6c1b90ce12dd6519aa7bce7854ea8.md", "sha256": "0cba5f26a201b09b2993bdaac48ac50556e6c1b90ce12dd6519aa7bce7854ea8"}`
2026-11-24T16:17:08Z | ACT | revision 1 | attempt 1 | #406: ретроспектива закреплена, ACT completed/accepted, коммит ca1f81a230d42f2fc18ce78a05a8ea6193e375f5; #407 N=2 остаётся pending/ready | `{"path": ".pdca/collection-405-407/items/406/rv1/report-1feb6e11f25d0463eef05173d70289179ae2dee899a405666c16e2183d1720a4.md", "sha256": "1feb6e11f25d0463eef05173d70289179ae2dee899a405666c16e2183d1720a4"}`
2026-11-24T16:27:25Z | DO | revision 1 | attempt 1 | #407 N=2: DO started, r1 прежний, baseline ca1f81a230d42f2fc18ce78a05a8ea6193e375f5, применимость и P.4 закреплены | .pdca/collection-405-407/items/407/cycle-2/rv1/do-start-refs.log
2026-11-24T16:48:14Z | DO | revision 1 | attempt 1 | #407 N=2: кандидат заморожен, unit 46/46, live 4/4 с подтверждённым удалением; готов пакет для первого CHECK после адресного scout | `{"path": ".pdca/collection-405-407/items/407/cycle-2/rv1/check-900453dd8871d4607a13e195a5c04c9a47cbe2c2bbea45c4c7046ee78afcecf6.md", "sha256": "900453dd8871d4607a13e195a5c04c9a47cbe2c2bbea45c4c7046ee78afcecf6"}`
2026-11-24T16:48:14Z | DO | revision 1 | attempt 1 | #408: закреплено дословное дополнение; original/PLAN pending от parent, без строки очереди; #407 N=2 не изменён | .pdca/status/collection-405-407.md §Original task
2026-11-24T16:51:17Z | DO | revision 1 | attempt 1 | Recovered after compaction: #407 N=2; scoped read-only `ses_df18deb08ffeqg1UW8tOTY88o4`. Последующий кандидат DO/open заменяет только историческое наблюдение «кандидата нет»; без перехода и изменения счётчиков | .pdca/status/collection-405-407-item-407-2.md
2026-11-24T16:51:17Z | PLAN | revision 1 | attempt 0 | #408 N=1: P.2 plan_state ready; за границей исполнения до отдельного post-ALL P.4, без строки очереди | `{"path": ".pdca/collection-405-407/items/408/rv1/plan-handoff.json", "sha256": "5aeca6272f9e43d6ab5a0180fa633cce4dc55c076868f8529ddb7e5aceb91aec"}`
2026-11-24T16:55:33Z | PLAN | revision 1 | attempt 0 | #408: добавочная P.4 закреплена, G1 хвост pending/ready; baseline DO не назначен, запуска нет | `{"path": ".pdca/collection-405-407/rv1/report-4af60f9259bac9fcc42fa6bdc9c780623307f34740ff063cf14f1b418bcd4075.md", "sha256": "4af60f9259bac9fcc42fa6bdc9c780623307f34740ff063cf14f1b418bcd4075"}`
2026-11-24T16:58:33Z | CHECK | revision 1 | attempt 1 | #407 N=2: независимый accepted закреплён, оба критерия met | `{"path": ".pdca/collection-405-407/items/407/cycle-2/rv1/report-b6ee489f6e1f734898bddcf9183636fbcbbd0e7c554eff73e18bb495801aec24.md", "sha256": "b6ee489f6e1f734898bddcf9183636fbcbbd0e7c554eff73e18bb495801aec24"}`
2026-11-24T16:58:33Z | ACT | revision 1 | attempt 1 | #407 N=2: CHECK → ACT/open, ретроспектива pending, G1 не освобождена | `{"path": ".pdca/collection-405-407/items/407/cycle-2/rv1/check-b210f8b76fe4352524d1fac575fe283abdc2a7b87cbb33327d7ffb9c67ac0bf2.md", "sha256": "b210f8b76fe4352524d1fac575fe283abdc2a7b87cbb33327d7ffb9c67ac0bf2"}`
2026-11-24T17:01:09Z | ACT | revision 1 | attempt 1 | #407 N=2: ретроспектива закреплена, ACT completed/accepted, коммит 7309e7e4948c74d8a587ba8f96397536350b7b55; #408 pending/ready до решения применимости | `{"path": ".pdca/collection-405-407/items/407/cycle-2/rv1/report-782e5cb9799f6eb1f875412427208c2db91245c7a279664c212967ed815299f7.md", "sha256": "782e5cb9799f6eb1f875412427208c2db91245c7a279664c212967ed815299f7"}`
2026-11-24T17:09:43Z | ACT | revision 1 | attempt 1 | G1: #407 N=2 принят и закоммичен; его позиция освобождена для #408, применимость подтверждена, HEAD 7309e7e4948c74d8a587ba8f96397536350b7b55 | .pdca/collection-405-407/items/408/rv1/do-start-refs.log
2026-11-24T17:09:43Z | DO | revision 1 | attempt 1 | #408 N=1: DO started по тому же r1, база 7309e7e4948c74d8a587ba8f96397536350b7b55; только зарезервированная matrix и собственные evidence/status | .pdca/collection-405-407/items/408/rv1/do-start-refs.log
2026-11-24T17:20:06Z | DO | revision 1 | attempt 1 | #408: матрица 5 движков заморожена, SQLite 3 ожидаемых отказа + 1 положительная проба; готовы пакет CHECK и явные пределы доказательств | `{"path": ".pdca/collection-405-407/items/408/rv1/check-eed2e554c9d229e49ff211a123d513dd3a0a3e5aa0107ff128afa133306e90ed.md", "sha256": "eed2e554c9d229e49ff211a123d513dd3a0a3e5aa0107ff128afa133306e90ed"}`
2026-11-24T17:26:59Z | DO | revision 1 | attempt 1 | #408: дословное указание «408 category:started» исполнено драйвером; независимый readback native status.name=In progress, status.category.key=started, state=OPEN. Не смена проекта/метки, этапа PDCA или новое разрешение | https://github.com/AlexeyShirshov/nextorm/issues/408
2026-11-24T17:30:12Z | CHECK | revision 1 | attempt 1 | #408: независимый accepted закреплён, оба критерия met | `{"path": ".pdca/collection-405-407/items/408/rv1/report-378c21db51ec631fa36ff82da008fbc543154140ba9964fc9628278b3015ebb1.md", "sha256": "378c21db51ec631fa36ff82da008fbc543154140ba9964fc9628278b3015ebb1"}`
2026-11-24T17:30:12Z | ACT | revision 1 | attempt 1 | #408: CHECK → ACT/open, ретроспектива pending, интеграционный CHECK не запущен; последний readback GitHub OPEN / In progress (started) сохраняется без нового запроса | `{"path": ".pdca/collection-405-407/items/408/rv1/check-ace6d293c07b77f156f7c225641d88c46a7abe6dc36c12212ee0bafaf80f5086.md", "sha256": "ace6d293c07b77f156f7c225641d88c46a7abe6dc36c12212ee0bafaf80f5086"}`
2026-11-24T17:33:34Z | ACT | revision 1 | attempt 1 | #408: точная ретроспектива закреплена; ACT completed/accepted, коммит 6c73b7442c02a702fc71b83e7bbfe14a10f6b7a9; G1 done, отдельный C pending; GitHub остаётся по последнему readback OPEN / In progress, новых remote операций нет | `{"path": ".pdca/collection-405-407/items/408/rv1/report-64957db2d9ef10fdd34ffc429734ca125ece360febda58d78c5ac4f69b2b2813.md", "sha256": "64957db2d9ef10fdd34ffc429734ca125ece360febda58d78c5ac4f69b2b2813"}`
2026-11-24T17:35:32Z | ACT | revision 1 | attempt 1 | Граница закрытого #408: готов пакет итогового C; независимый CHECK коллекции pending, без собственного счётчика | `{"path": ".pdca/collection-405-407/rv1/check-15ea5d809294ddc11d79461c55563432f0328ac68c5979830b78d9332d94d0e7.md", "sha256": "15ea5d809294ddc11d79461c55563432f0328ac68c5979830b78d9332d94d0e7"}`
2026-11-24T17:40:59Z | CHECK | revision — | attempt — | интеграционный C: независимый accepted закреплён, integrated-tree pass, 4/4 цели закрыты | `{"path": ".pdca/collection-405-407/rv1/report-af1c96a5d41dc72081e056f360ae58a80a7252b86d32791b3438ec4d8b14dfec.md", "sha256": "af1c96a5d41dc72081e056f360ae58a80a7252b86d32791b3438ec4d8b14dfec"}`
2026-11-24T17:40:59Z | ACT | revision — | attempt — | родительский ACT/open, итоговая ретроспектива pending, без счётчиков и закрытия | `{"path": ".pdca/collection-405-407/rv1/check-d61a49082b9bd5c7d06103f83f84a2374d15438fcc37ff4a2285eb555b025f30.md", "sha256": "d61a49082b9bd5c7d06103f83f84a2374d15438fcc37ff4a2285eb555b025f30"}`
2026-11-24T17:44:11Z | ACT | revision — | attempt — | родительский ACT completed/accepted: полнота 4/4, интеграционный pass отдельно, ретроспектива complete; G1 done без merge, работа сохранена | `{"path": ".pdca/collection-405-407/rv1/report-de474247b9680cb3b1e636cf2b5db92c2e0d2ab830f53cfe829612d0c33ec550.md", "sha256": "de474247b9680cb3b1e636cf2b5db92c2e0d2ab830f53cfe829612d0c33ec550"}`
