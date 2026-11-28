# Cycle status: collection-issues-420-422-item-422 / cycle 1

## Current state

- task: `collection-issues-420-422-item-422` · form: `cycle` · specialized skill: `pdca-dotnet`
- mode: `autonomous`
- cycle: `N=1` · revision: `r=1` · attempt: `n=1`
- scope: полный объём #422 по § Original task и PLAN r1 · status path: `.pdca/status/collection-issues-420-422-item-422-1.md`
- goal: именованные `IDataContext.From…` для всех уже поддерживаемых встроенных PostgreSQL TVF — § PLAN record и § Original task являются источниками
- phase: `ACT` · record: `completed` · outcome: `accepted`
- source: `#422` · context refs: `.pdca/status/collection-issues-420-422.md` § Collection input; #420 и #421 — только контекст зависимостей
- criteria: `422-AC1`–`422-AC3` — § PLAN record r1, оригинал в § Original task
- item: `422` · cycle_id: `collection-issues-420-422-item-422-1` · item status: `done` · disposition: `finalized` · `plan_state=ready`
- contract: `pdca` · overlay: `coder+.NET` (`pdca-coder` + `pdca-dotnet`) · project overlay: `nextorm-pdca`
- evidence_root: `.pdca/collection-issues-420-422-item-422/rv1/` · mechanics: `manual`
- checkpoint: независимый CHECK accepted; ACT завершён, #422/G1 done; внешний автокоммит `d2648cb466457725112f6b3a535c43e17de9db6b` выполнен. CHECK интегрированного дерева не запускался; следующий владелец — primary.

### Collection scheduling metadata

- footprint_ref: итоговый PLAN r1, строки 62–64; оценка: публичный API `IDataContext`/PostgreSQL TVF, связанные тесты и EN/RU руководства TVF. Пересечение файлов руководства EN/RU с #421 подтверждено; точное пересечение тестовых файлов неизвестно.
- prerequisite_ref: итоговый PLAN r1, строки 65–66; требуется существующий lambda-based `From`. Формальные предшественники не установлены; приёмка #420 не следует из HEAD, публичный API позиции #421 не предполагается; #320 и #147 — связанные задачи, не предшественники.
- requirement_ref: § Original task; constraints_ref: § PLAN record r1. Назначение: G1, #420 → #421 → #422, текущие ветка и worktree по родительскому § Groups.

## Original task

`{"path":".pdca/collection-issues-420-422/rv1/issue-422.md","sha256":"bfdd01a4ea8d0c9e961917b80c48bbc87e1eff1ac55e0114a96c07ffb3857498"}` — полный оригинал задачи и критериев.

## PLAN record

### r1

- PLAN (`pdca-planner`, `ses_de9839948ffeMAq9BMuo49OWnS`, последний завершённый отчёт): `{"path":".pdca/collection-issues-420-422-item-422/rv1/report-8519f2afcb76f61206b761c87f7065d6ab968b416b1a8141daf9c66570c3988d.md","sha256":"8519f2afcb76f61206b761c87f7065d6ab968b416b1a8141daf9c66570c3988d"}`
- Исходный фактический отчёт (`pdca-scout`, `ses_de982c2bfffeD6yAb1tDu8BDmC`, последний завершённый отчёт): `{"path":".pdca/collection-issues-420-422-item-422/rv1/report-564843ca40c031bf6109401eb63c2ac3b7d34dbcf1f2176e9f89dce0bf916cf0.md","sha256":"564843ca40c031bf6109401eb63c2ac3b7d34dbcf1f2176e9f89dce0bf916cf0"}`
- Исходные ограничения: `.pdca/status/collection-issues-420-422.md` § Collection input, строки 15–21, и § Constraints, строки 23–25. Объём и запреты #422: оригинал § Original task и pinned PLAN r1, § Bounds, risks, and handoff.
- Запрошенный перечень целей (`pdca-scout`, `ses_de972f2daffe0NtoXzKVN7XTqs`, последний завершённый отчёт): `{"path":".pdca/collection-issues-420-422-item-422/rv1/report-65dc3c88be2366523db03133f43c2ed7f4f6dc3bfc17d6b566c2a3276aac3652.md","sha256":"65dc3c88be2366523db03133f43c2ed7f4f6dc3bfc17d6b566c2a3276aac3652"}`.
- Итоговый PLAN r1 (`pdca-planner`, `ses_de9839948ffeMAq9BMuo49OWnS`, уточнённый завершённый отчёт): `{"path":".pdca/collection-issues-420-422-item-422/rv1/report-a42eb2dde9d51318c3b12b5cbbdf4f8b33ef53582329cbd7cc6572dfdb754e59.md","sha256":"a42eb2dde9d51318c3b12b5cbbdf4f8b33ef53582329cbd7cc6572dfdb754e59"}`. Только этот отчёт определяет актуальный план; прежний неполный отчёт сохранён как история. Цели и решение по формам `unnest` — § Pointable scope; ограничения — § Bounds and handoff.

### Saved-PLAN applicability preflight

- #422: фактический отчёт `pdca-scout` (`ses_de3610553ffe5CJE8ZrrpIqrY2`): `{"path": ".pdca/collection-issues-420-422-item-422/rv1/report-6e8cf1490683cfa77c9a076a0d95681060578de1e15101cff8a5fc3cdf29e9a8.md", "sha256": "6e8cf1490683cfa77c9a076a0d95681060578de1e15101cff8a5fc3cdf29e9a8"}`. Решение ожидается.

- #422: решение `pdca-planner` (`ses_de34738cfffe7UcgMZe4CiqELF`) — итоговый r1 применим, ревизия не нужна: `{"path": ".pdca/collection-issues-420-422-item-422/rv1/report-40e776054b0081320d82e5c615c22e0caf6f8d9c799c325f3c07347faa251caa.md", "sha256": "40e776054b0081320d82e5c615c22e0caf6f8d9c799c325f3c07347faa251caa"}`.

## Approval record

`not applicable: autonomous mode`

## DO result

Кандидат реализован; полный исходный отчёт DO: `{"path": ".pdca/collection-issues-420-422-item-422/rv1/report-11dd585530513ba96648bd57357ce24e50e76064653f9ff999c0fa5ff757ec1f.md", "sha256": "11dd585530513ba96648bd57357ce24e50e76064653f9ff999c0fa5ff757ec1f"}`.

- Идентификация: `.pdca/collection-issues-420-422-item-422/rv1/attempts/do1/candidate-final.sha256`.
- Пакет для независимого CHECK: `{"path": ".pdca/collection-issues-420-422-item-422/rv1/attempts/do1/check-25a5022ca31389c294738b0ba192ca24c15e7e980fa6bfe3f766721eb52c87fc.md", "sha256": "25a5022ca31389c294738b0ba192ca24c15e7e980fa6bfe3f766721eb52c87fc"}`.

## CHECK record

- Полный независимый CHECK (`pdca-check`, `ses_de30cd296ffexOe05KTCLkCs4q`): `{"path": ".pdca/collection-issues-420-422-item-422/rv1/report-7cedeefdcb5d3a7a936c33819dee2de6461d1a5bad938ccf5c9622d6ea3842ca.md", "sha256": "7cedeefdcb5d3a7a936c33819dee2de6461d1a5bad938ccf5c9622d6ea3842ca"}`.
- `not accepted`: `422-AC1` — `met`; `422-AC2` и `422-AC3` — `unverified` из-за пробела в наблюдении существующих утверждений тестов и примеров. Дефект продукта не установлен.
- Один объединённый запрос `pdca-scout` с `file:line`: (1) точные существующие сравнения SQL обоих API и привязки параметров/порядка аргументов для маркера и захваченных значений; (2) точная семантическая проверка отказа SQLite для обоих API; (3) точные места примеров в EN/RU и что они показывают. Зафиксировать отсутствие проверки или примера, если их нет. Только чтение на том же кандидате `100c4f635fe10cb197a06b4dd33e87b8fa64c781` плюс шесть файлов из `attempts/do1/candidate-final.sha256` под `evidence_root`; без сборки, тестов или изменений продукта.

- Единственное целевое наблюдение `pdca-scout` (`ses_de305e839ffe2ZPkfSV6cKNc2s`): `{"path": ".pdca/collection-issues-420-422-item-422/rv1/report-1c1fccb961de599531fe486c31ca58123e9c6077edb0a02718dff881a83cd47b.md", "sha256": "1c1fccb961de599531fe486c31ca58123e9c6077edb0a02718dff881a83cd47b"}`. Прямой записи значений и порядка коллекции параметров нет.
- Свежий пакет независимого CHECK: `{"path": ".pdca/collection-issues-420-422-item-422/rv1/check-5fd3ea31f5428cec0ed581f8561e80d2c7218d116630480a33d690cd11bcae5c.md", "sha256": "5fd3ea31f5428cec0ed581f8561e80d2c7218d116630480a33d690cd11bcae5c"}`; исход отправки зафиксирован ниже.

- Итоговый независимый CHECK `accepted`; `422-AC1`, `422-AC2`, `422-AC3` — `met`. Полный отчёт `pdca-check` (`ses_de30cd296ffexOe05KTCLkCs4q`): `{"path": ".pdca/collection-issues-420-422-item-422/rv1/report-799d56d35610b1f44aca2eeb69d5434cb1f69ca4c85947cd219b80e70dab4177.md", "sha256": "799d56d35610b1f44aca2eeb69d5434cb1f69ca4c85947cd219b80e70dab4177"}`. Тот же кандидат: HEAD `100c4f635fe10cb197a06b4dd33e87b8fa64c781` плюс шесть файлов из `attempts/do1/candidate-final.sha256`; `N=1, r=1, n=1/3`. Дособор без новой реализации или запусков тестов. Вердикт не распространяется на интеграцию коллекции.

## ACT record

ACT завершён: исход `accepted` по § CHECK record; `422-AC1`, `422-AC2`, `422-AC3` — `met`. Полный неизменённый отчёт ретроспективы: `{"path": ".pdca/collection-issues-420-422-item-422/rv1/report-bdcb4f3374c1850e4219740b9af420aa9c008621f4fd4b83089c323a2c3db83e.md", "sha256": "bdcb4f3374c1850e4219740b9af420aa9c008621f4fd4b83089c323a2c3db83e"}`. Внешний автокоммит кандидата #422 выполнен: `d2648cb466457725112f6b3a535c43e17de9db6b` (пути в § Git state). Следующее действие — передача интегрированного кандидата на CHECK; владелец primary. GitHub #422 не закрывался. CHECK интегрированного дерева не запускался.

## Lessons learned

Полный неизменённый отчёт независимой ретроспективы `pdca-check` (`ses_de30cd296ffexOe05KTCLkCs4q`): `{"path": ".pdca/collection-issues-420-422-item-422/rv1/report-bdcb4f3374c1850e4219740b9af420aa9c008621f4fd4b83089c323a2c3db83e.md", "sha256": "bdcb4f3374c1850e4219740b9af420aa9c008621f4fd4b83089c323a2c3db83e"}`.

## Revision history

`none`

## Defect history

`none`

## Changed files

- Создан этот статус. Два исходных отчёта закреплены под `evidence_root`; точные ссылки в § PLAN record r1. Изменений продукта нет.
- Под `evidence_root` дополнительно закреплены запрошенный перечень целей и итоговый PLAN r1; ссылки в § PLAN record r1.

- Обновлён этот статус; закреплён фактический отчёт проверки применимости: `{"path": ".pdca/collection-issues-420-422-item-422/rv1/report-6e8cf1490683cfa77c9a076a0d95681060578de1e15101cff8a5fc3cdf29e9a8.md", "sha256": "6e8cf1490683cfa77c9a076a0d95681060578de1e15101cff8a5fc3cdf29e9a8"}`. Изменений продукта и тестов нет.

- PLAN → DO #422: обновлены `.pdca/status/collection-issues-420-422-item-422-1.md` и `.pdca/status/collection-issues-420-422.md` (untracked); закреплён `.pdca/collection-issues-420-422-item-422/rv1/report-40e776054b0081320d82e5c615c22e0caf6f8d9c799c325f3c07347faa251caa.md` (ignored). Без изменений продукта, тестов, индекса, коммитов или issue.


- #422: `src/nextorm.core/DataContext/PostgresTvfExtensions.cs`
- #422: `src/nextorm.core/PublicAPI.Unshipped.txt`
- #422: `docs/guide/13-table-valued-functions.md`
- #422: `docs/ru/guide/13-table-valued-functions.md`
- #422: `tests/nextorm.postgres.tests/T320/PostgresTvfShortcutSqlTests.cs`
- #422: `tests/nextorm.integration.tests/T320/PostgresTvfShortcutTests.cs`

- DO → CHECK #422: обновлены только этот статус и `.pdca/status/collection-issues-420-422.md`; оба untracked, пакет и доказательства не изменены.

- Дособор #422: обновлены статус элемента и родительский статус; отчёт scout и пакет CHECK закреплены под `evidence_root` (см. § CHECK record). Кандидат и ledger не менялись.

- CHECK → ACT #422: обновлены `.pdca/status/collection-issues-420-422-item-422-1.md` и `.pdca/status/collection-issues-420-422.md` (untracked); закреплён `.pdca/collection-issues-420-422-item-422/rv1/report-799d56d35610b1f44aca2eeb69d5434cb1f69ca4c85947cd219b80e70dab4177.md` (ignored). Без изменений продукта, тестов, индекса, коммитов или issue.

## Git state

- CHECK → DO #422: обновлены два untracked статуса (этот и родительский); полный CHECK закреплён под `evidence_root` (ignored), точная ссылка в § CHECK record. Кандидат и его ledger не менялись; без staging, коммита или действий с issue.

- status path: `.pdca/status/collection-issues-420-422-item-422-1.md` — создан в этом цикле, на диске, не добавлялся в индекс и не коммитился; untracked по проверке этого шага. Закреплённый отчёт игнорируется Git.
- Родительский baseline: `4d03dca71dfd7392101c9ce948da7eca522c48f3` (`.pdca/status/collection-issues-420-422.md` § Git state); свежая проверка Git не выполнялась.

- CHECK → ACT #422: обновлены `.pdca/status/collection-issues-420-422-item-422-1.md` и `.pdca/status/collection-issues-420-422.md` (untracked); закреплён `.pdca/collection-issues-420-422-item-422/rv1/report-799d56d35610b1f44aca2eeb69d5434cb1f69ca4c85947cd219b80e70dab4177.md` (ignored). Без изменений продукта, тестов, индекса, коммитов или issue.

- ACT #422: два статуса обновлены (untracked); pinned retrospective `.pdca/collection-issues-420-422-item-422/rv1/report-bdcb4f3374c1850e4219740b9af420aa9c008621f4fd4b83089c323a2c3db83e.md` (ignored). Без GitHub-операций.

- Внешний автокоммит #422: commit и новый HEAD `d2648cb466457725112f6b3a535c43e17de9db6b` на `refs/heads/1.0.12`, parent `100c4f635fe10cb197a06b4dd33e87b8fa64c781`. Принятые пути: `M` `docs/guide/13-table-valued-functions.md`, `M` `docs/ru/guide/13-table-valued-functions.md`, `A` `src/nextorm.core/DataContext/PostgresTvfExtensions.cs`, `M` `src/nextorm.core/PublicAPI.Unshipped.txt`, `A` `tests/nextorm.integration.tests/T320/PostgresTvfShortcutTests.cs`, `A` `tests/nextorm.postgres.tests/T320/PostgresTvfShortcutSqlTests.cs`. Индекс пуст; статусы и evidence не коммитились; #423 неизменён и untracked. GitHub #422 не закрывался. Доказательство: `.pdca/collection-issues-420-422-item-422/rv1/autocommit-receipt.log`.

## Progress log

2026-11-26T06:26:17.152Z | PLAN | revision 1 | attempt 0 | plan ready; plan_handoff, DO не запускался | § PLAN record r1
2026-11-26T06:31:25.634Z | PLAN | revision 1 | attempt 0 | Notice: прежняя готовность отозвана; plan_state=running, планировщик запросил фактический перечень TVF с точными указателями; передача запрещена до завершения того же PLAN | § Current state; § PLAN record r1
2026-11-26T07:28:02.028Z | PLAN | revision 1 | attempt 0 | Получен и закреплён запрошенный фактический перечень TVF; plan_state=running до завершения того же PLAN | § PLAN record r1, запрошенный перечень целей
2026-11-26T07:37:47.660Z | PLAN | revision 1 | attempt 0 | plan ready; сохранён итоговый уточнённый PLAN r1; DO не запускался | § PLAN record r1, итоговый PLAN
2026-11-27T10:39:23.781Z | PLAN | revision 1 | attempt 0 | G1/#422: pending → in-progress; начата предварительная проверка применимости сохранённого PLAN, далее pdca-scout и pdca-planner; DO не начат | § PLAN record r1, итоговый PLAN
2026-11-27T11:15:51Z | PLAN | revision 1 | attempt 0 | G1/#422: Существующая предпосылка lambda-based `IDataContext.From` сохраняется; #421 добавил публичный `TvfEntityBuilder.WithOrdinality`, но не именованный shortcut `IDataContext.From…`; пересечение файлов руководства EN/RU подтверждено, точное пересечение тестовых файлов неизвестно; сохранённые инвентарь и whitelist TVF на месте. Решение `pdca-planner` о применимости того же сохранённого r1 ожидается; очередь и расписание неизменны; DO не начат. | .pdca/collection-issues-420-422-item-422/rv1/report-6e8cf1490683cfa77c9a076a0d95681060578de1e15101cff8a5fc3cdf29e9a8.md
2026-11-27T11:19:53Z | DO | revision 1 | attempt 0 | G1/#422: переход PLAN → DO разрешён; итоговый r1 применим без ревизии; полный объём и критерии #422, последовательная очередь G1 неизменны; DO не начат | `{"path": ".pdca/collection-issues-420-422-item-422/rv1/report-40e776054b0081320d82e5c615c22e0caf6f8d9c799c325f3c07347faa251caa.md", "sha256": "40e776054b0081320d82e5c615c22e0caf6f8d9c799c325f3c07347faa251caa"}`
2026-11-27T11:23:29Z | DO | revision 1 | attempt 1 | DO started | § DO result
2026-11-27T11:36:47Z | DO | revision 1 | attempt 1 | Recovered after compaction; same DO job sh_21cc94549001I56w5Ll71ws6Ea retained; provider build exit 0, integration build in flight, no test run receipt yet; N=1,r=1,n=1, queue unchanged | .pdca/status/collection-issues-420-422-item-422-1.md#do-result
2026-11-27T12:02:40Z | DO | revision 1 | attempt 1 | Recovered after compaction; build job cancelled by restart; completed provider build reused, integration build completion not established; same lane and counters | .pdca/status/collection-issues-420-422-item-422-1.md#do-result
2026-11-27T12:11:03Z | DO | revision 1 | attempt 1 | Recovered after compaction; preserved provider launcher exit 1, re-reconciled original native events without rerun; stale integration artifact after exit 124, resumed only incomplete lane as sh_21cea631c001DQhaBh6IuRiH1X | .pdca/status/collection-issues-420-422-item-422-1.md#do-result
2026-11-27T12:12:12Z | DO | revision 1 | attempt 1 | Кандидат реализован, scoped evidence записано; ожидается независимый CHECK | `{"path": ".pdca/collection-issues-420-422-item-422/rv1/attempts/do1/check-25a5022ca31389c294738b0ba192ca24c15e7e980fa6bfe3f766721eb52c87fc.md", "sha256": "25a5022ca31389c294738b0ba192ca24c15e7e980fa6bfe3f766721eb52c87fc"}`
2026-11-27T12:17:29Z | CHECK | revision 1 | attempt 1 | Кандидат готов; DO → CHECK, отправка независимому CHECK ожидается; запись открыта, исход pending | § DO result — существующий пакет CHECK
2026-11-27T12:23:36Z | CHECK | revision 1 | attempt 1 | Независимый CHECK not accepted; AC1 met, AC2/AC3 unverified; продуктовый дефект не установлен | § CHECK record
2026-11-27T12:23:36Z | DO | revision 1 | attempt 1 | CHECK → DO: один целевой дособор через драйвер на том же кандидате; без новой попытки; ACT pending | § CHECK record
2026-11-27T12:32:29Z | CHECK | revision 1 | attempt 1 | G1/#422: один целевой отчёт scout закреплён; свежий CHECK на том же кандидате готов к отправке; исход и ACT pending, очередь неизменна | `{"path": ".pdca/collection-issues-420-422-item-422/rv1/report-1c1fccb961de599531fe486c31ca58123e9c6077edb0a02718dff881a83cd47b.md", "sha256": "1c1fccb961de599531fe486c31ca58123e9c6077edb0a02718dff881a83cd47b"}`; `{"path": ".pdca/collection-issues-420-422-item-422/rv1/check-5fd3ea31f5428cec0ed581f8561e80d2c7218d116630480a33d690cd11bcae5c.md", "sha256": "5fd3ea31f5428cec0ed581f8561e80d2c7218d116630480a33d690cd11bcae5c"}`
2026-11-27T12:37:12Z | CHECK | revision 1 | attempt 1 | G1/#422: независимый CHECK `accepted`; `422-AC1`, `422-AC2`, `422-AC3` — `met`. Тот же кандидат, без новой реализации или тестов | `{"path": ".pdca/collection-issues-420-422-item-422/rv1/report-799d56d35610b1f44aca2eeb69d5434cb1f69ca4c85947cd219b80e70dab4177.md", "sha256": "799d56d35610b1f44aca2eeb69d5434cb1f69ca4c85947cd219b80e70dab4177"}`
2026-11-27T12:37:12Z | ACT | revision 1 | attempt 1 | G1/#422: CHECK → ACT, запись открыта; ретроспектива и закрытие ожидаются; #422/G1 in-progress, очередь неизменна | .pdca/status/collection-issues-420-422-item-422-1.md#act-record
2026-11-27T12:39:50Z | ACT | revision 1 | attempt 1 | G1/#422: пакет ретроспективы элемента готов; отправка pdca-check ожидается. ACT открыт, принятый кандидат неизменён, #422/G1 in-progress, очередь неизменна; интеграция не начата | `{"path": ".pdca/collection-issues-420-422-item-422/rv1/check-ece4d6473d51ffbfcf9f838b4689d76deb3f361ffd6d75c68e2d35898f10329e.md", "sha256": "ece4d6473d51ffbfcf9f838b4689d76deb3f361ffd6d75c68e2d35898f10329e"}`
2026-11-27T12:46:03Z | ACT | revision 1 | attempt 1 | G1/#422: ACT completed/accepted; #422/G1 done; outer autocommit pending; integrated CHECK pending/not run, next owner primary | {"path": ".pdca/collection-issues-420-422-item-422/rv1/report-bdcb4f3374c1850e4219740b9af420aa9c008621f4fd4b83089c323a2c3db83e.md", "sha256": "bdcb4f3374c1850e4219740b9af420aa9c008621f4fd4b83089c323a2c3db83e"}
2026-11-27T12:46:50Z | ACT | revision 1 | attempt 1 | G1/#422 outer item autocommit d2648cb466457725112f6b3a535c43e17de9db6b; six accepted paths; GitHub #422 not closed; integrated CHECK pending/not run, next owner primary | .pdca/collection-issues-420-422-item-422/rv1/autocommit-receipt.log
