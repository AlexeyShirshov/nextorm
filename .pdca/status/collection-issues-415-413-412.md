# Collection: issues-415-413-412

collection-id: issues-415-413-412
form: collection
plan_cap: 4
lane_cap: 4
mode: autonomous
evidence_root: .pdca/issues-415-413-412/rv1

## Original task

pdca по задачам 415 413 412 автономно автокомит в отдельном ворктри

- issue-415: https://github.com/AlexeyShirshov/nextorm/issues/415
  - Оригинал GitHub (название и полный текст, включая критерии): `{"path": ".pdca/issues-415-413-412/rv1/issue-415.json", "sha256": "1fd13892ce39e0c0aeb01a69815cce2b1ad8b9c5a1e23cdedb95aff5e256d0c2"}`
- issue-413: https://github.com/AlexeyShirshov/nextorm/issues/413
  - Оригинал GitHub (название и полный текст, включая критерии): `{"path": ".pdca/issues-415-413-412/rv1/issue-413.json", "sha256": "443b640d273fd972902dcd506a97c2812f715ca6ef1a278fb85cca3c25633314"}`
- issue-412: https://github.com/AlexeyShirshov/nextorm/issues/412
  - Оригинал GitHub (название и полный текст, включая критерии): `{"path": ".pdca/issues-415-413-412/rv1/issue-412.json", "sha256": "7c75864f774cee1d7a3ace6fad0e66aedd9ea83aec75debcba5c3cdd81f65b1a"}`

## Constraints

- Авторитетный checkout: `/tmp/opencode/nextorm-issues-415-413-412`, `collection/issues-415-413-412/base`. Первый checkout, созданный хостом, оказался в другом репозитории; продуктовых работ там не было (по данным владельца коллекции).
- Базовый набор тестов на HEAD вызывающего на старте: `c9f2dd90a79a489e8a68a3da16e82be237ab3663`; `backward-compatible` по умолчанию, разрешений на изменение базовых тестов нет. Тесты при инициализации не запускаются.
- Автокоммит только после принятия результата в изолированных ветках `collection/**`. Без push и интеграции в дерево вызывающего. Без изменений на GitHub до приёмки. Podman только по явному запросу пользователя.

## Collection phase decisions

- Entry PLAN (точный ответ `ses_df3c99bd1ffe659qR3OI01dR5R`, `msg_20c3b9c21001TyXWzRHJLDKItL`): `{"path": ".pdca/issues-415-413-412/rv1/report-ddd2d9c45083bca6e9c9710b1dd8e0ec24e16144ce4032f7f21c51ca9acc116b.md", "sha256": "ddd2d9c45083bca6e9c9710b1dd8e0ec24e16144ce4032f7f21c51ca9acc116b"}`.
- Scout (точный ответ `ses_df3c7f1a8ffeE6fvHYjIaLSsAn`, `msg_20c3885d4001jwsRSyWKMIp1JI`): `{"path": ".pdca/issues-415-413-412/rv1/report-efd97e92e38d8304422d7436fe7369455972285d6e2fa87345b7bfd3f03085e2.md", "sha256": "efd97e92e38d8304422d7436fe7369455972285d6e2fa87345b7bfd3f03085e2"}`.
- Форма уже выбрана: `collection`. Область #413 и #412 — PostgreSQL и SQLite; источники критериев — оригиналы в §Original task.
- Item PLAN: 3/3 готовы; ALL-barrier: пройден по трём сохранённым PLAN. PLAN revision vector: `issue-415=r1, issue-413=r1, issue-412=r1`. DAG без рёбер; одна последовательная группа G1.
- Решение о группировке: `ses_df3a84f27ffe0sjNGd90TTVkWW` / `msg_20c57c14a001D4gpWb5b1idAok`, точный отчёт `{"path": ".pdca/issues-415-413-412/rv1/report-11eaff4619b5a7c146bbaf13598ba3c8535e6ca8a551878a1ab0256537085257.md", "sha256": "11eaff4619b5a7c146bbaf13598ba3c8535e6ca8a551878a1ab0256537085257"}`.
- Привязка расписания: caps 4/4, фактически одна активная lane и один item; G1 выполняет issue-415 → issue-413 → issue-412. Порядок не означает зависимость. Только текущий изолированный checkout; без новых веток/ворктри и merge. PLAN refs и вектор выше/ниже сохраняются.
- Dispatch: `pdca-orchestrator`, `mode=collection-lane`, autonomous, `group=G1`, `collection_status_path=/tmp/opencode/nextorm-issues-415-413-412/.pdca/status/collection-issues-415-413-412.md`; очередь читать из записи, владелец назначается при запуске. Записи через pdca-executor. Автокоммит только после приёмки; без push. Механика ручная, адаптеры коллекции не избраны.
- Preflight: `OPENCODE_CONFIG=profiles/deepseek.jsonc` (oc-ds), `experimental.subagent_depth=3` в профиле; проектный opencode.json не ограничивает subagent. `opencode debug agents` разрешает build → runner, runner → PLAN/DO/CHECK/escalate/scout и escalate → scout (`.pdca/issues-415-413-412/rv1/lane-role-policy.json`); все шесть PDCA-ролей разрешаются. Установленный orchestrator побайтово совпал с каноническим ассетом (`cmp`, exit 0).
- Контракты загружены в текущем потоке (`pdca`, `pdca-collection`, `pdca-coder`, `pdca-dotnet`, `nextorm-pdca`); runner должен загрузить свой набор по точным ID. Вложенность PLAN/DO-роли наблюдалась на подготовке; CHECK/escalate не пробовались, проверены по политике. Запускать G1 непосредственно из build, без дополнительного слоя: depth 3 нужен для escalate → scout.
- issue-413, N=1, r=1, n=0: PLAN `{"path": ".pdca/issues-415-413-412-issue-413/rv1/report-3b4f8050b35be6018b770a3a1b475ecc8c5b0da050ba0f63eeef3909428f4496.md", "sha256": "3b4f8050b35be6018b770a3a1b475ecc8c5b0da050ba0f63eeef3909428f4496"}`; финальная запись handoff заменяет исходный ресурсный блокер для целей готовности. Автономный режим коллекции, approval ref не применяется.
- Метаданные планирования issue-413 (не критерии): потенциальное пересечение в общих API запросов/временных объектов и тестах SQLite/PostgreSQL; #412 связан, но prerequisite не установлен (таблица ключей предполагается существующей); #60 — контекст материализации.
- ALL-barrier: сверены item status и SHA-256 трёх планов; ссылки в строках задач и в этом разделе. Готовность не авторизует DO. Запись #413 не удостоверяет preflight оверлеев или release; возможность выделения lanes этим обновлением не проверялась.
- issue-415, N=1, r=1, n=0: PLAN `{"path": ".pdca/issues-415-413-412-issue-415/rv1/report-03581450a7408f6460567254c55d360db5959a32c2a22dbe0b9cb89890ad8e71.md", "sha256": "03581450a7408f6460567254c55d360db5959a32c2a22dbe0b9cb89890ad8e71"}`; evidence root `.pdca/issues-415-413-412-issue-415/rv1/`; автономный режим, approval ref не применяется.
- Метаданные планирования issue-415 (не критерии): вероятная область — типизированные JOIN, генерация SQL SQLite, интеграция SQLite; зависимость не установлена, пересечение не проверено.
- issue-412, N=1, r=1, n=0: запись задачи `{"path": ".pdca/status/issues-415-413-412-issue-412-1.md", "sha256": "6d72f4bf0e3a20e1999b707bf89aab1fe9e6237d32e7f01d5a55eff95b8f927d"}`; PLAN `{"path": ".pdca/issues-415-413-412-issue-412/rv1/report-bddf448f77a5bad355c7e55948bf2b6177a13d1879d72cf84e115d05acb22c42.md", "sha256": "bddf448f77a5bad355c7e55948bf2b6177a13d1879d72cf84e115d05acb22c42"}`; автономный режим, approval ref не применяется.
- Метаданные планирования issue-412 (не критерии): общая API временных таблиц, SQLite/PostgreSQL, интеграционные тесты, документация EN/RU; пересечение с #413 возможно, но зависимость не установлена; пересечение с #415 неизвестно.

## Groups

| id | items/order | worktree | branch/ref | status | note |
| --- | --- | --- | --- | --- | --- |
| G1 | issue-415 → issue-413 → issue-412 | `/tmp/opencode/nextorm-issues-415-413-412` | `collection/issues-415-413-412/base` | done | все три элемента завершены; один checkout, без merge |

## Items

| id | group | branch | status | plan_state | form | contract | overlay | cycle_id | status_path | plan_revision | baseline_ref | footprint_ref | requirement_ref | prerequisite_ref | reason | patch_path | status_link | disposition | pending_step_approval_ref | pending_approval_ref | preparation_state | preparation_ref | campaign_ref |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| issue-415 | G1 | `collection/issues-415-413-412/base` | done | ready | cycle | pdca-dotnet | coder+.NET (`pdca-coder` + `pdca-dotnet`; `nextorm-pdca`) | issue-415 / N=1 | `.pdca/status/issues-415-413-412-issue-415-1.md` | r1 | `c9f2dd90a79a489e8a68a3da16e82be237ab3663` | §Collection phase decisions: issue-415 | `.pdca/issues-415-413-412/rv1/issue-415.json` | — | CHECK accepted; 415-AC1/AC2/AC3 met; ACT завершён; §General verification and recovery | — | [issue-415](issues-415-413-412-issue-415-1.md) | finalized | — | — | — | — | — |
| issue-413 | G1 | `collection/issues-415-413-412/base` | done | ready | cycle | pdca | `pdca-coder` + `pdca-dotnet` + `nextorm-pdca` | issue-413 / N=1 | `.pdca/status/issues-415-413-412-issue-413-1.md` | r1 | `c9f2dd90a79a489e8a68a3da16e82be237ab3663` | §Collection phase decisions: issue-413 | `.pdca/issues-415-413-412/rv1/issue-413.json` | — | CHECK accepted; 413-C1–C4 met; ACT завершён | — | [issue-413](issues-415-413-412-issue-413-1.md) | finalized | — | — | — | — | — |
| issue-412 | G1 | `collection/issues-415-413-412/base` | done | ready | cycle | pdca | `pdca-coder` + `pdca-dotnet` + `nextorm-pdca` | issue-412 / N=1 | `.pdca/status/issues-415-413-412-issue-412-1.md` | r1 | `c9f2dd90a79a489e8a68a3da16e82be237ab3663` | §Collection phase decisions: issue-412 | `.pdca/issues-415-413-412/rv1/issue-412.json` | — | CHECK accepted; 412-AC1–AC4 met; ACT завершён | — | [issue-412](issues-415-413-412-issue-412-1.md) | finalized | — | — | — | — | — |

## Integration

- Completeness: 3/3; issue-415, issue-413 и issue-412 `done` / `finalized`.
- Integrated-tree CHECK: `PASS` / `accepted`; все 11 критериев `met`, F1 закрыт; все 3 задачи приняты. Неизменный HEAD `4fe2f25df74a957ce60e0cf5a6a510f68c3feeb6`. Точный отчёт `{"path": ".pdca/issues-415-413-412/rv1/report-893f004e287c796e3829ffab1b12c1502c67d5c397acadbacb0153d11c8aed60.md", "sha256": "893f004e287c796e3829ffab1b12c1502c67d5c397acadbacb0153d11c8aed60"}`.
- Историческое состояние до первого CHECK: не запускался; передаётся родителю на HEAD `4fe2f25df74a957ce60e0cf5a6a510f68c3feeb6` в текущей ветке; merge не выполнялся.
- Preserved results: принятый CHECK кандидат issue-415 сохранён без изменений: `.pdca/issues-415-413-412-issue-415/rv1/candidate-sha256.txt` (`7c550d1cf6a830359f5da287d55777287812d8d4ce41963c6dabb3d76d651735`); доказательства добора `.pdca/issues-415-413-412-issue-415/rv1/attempts/ac3-regather.md`; предыдущий CHECK `{"path": ".pdca/issues-415-413-412-issue-415/rv1/report-ff895bc50e8de50f239b0851cc57bbd497bbc928b867ebda9102b693d4a1e966.md", "sha256": "ff895bc50e8de50f239b0851cc57bbd497bbc928b867ebda9102b693d4a1e966"}`.

### General verification and recovery

- State: `pass`; ACT завершён; коллекция полностью принята (3/3 item ACT и коммиты, 11/11 критериев). Worktree, evidence и внутренний spec сохранены; дальнейших действий нет.
- Кандидат CHECK: `{"path": ".pdca/issues-415-413-412/rv1/integration-candidate.json", "sha256": "d0c241d5d08f038e48300c926bb0448127cf87adc98478d121f6edddc2eb5f27"}` (Git chain, исходные брифы, item CHECK/ACT, DLL). G1 done, без merge; tracked checkout чист.
- Пакет независимого CHECK: `{"path": ".pdca/issues-415-413-412/rv1/check-5ef043e60e6163e74962cca82128cacdcc185d7738711ce460278be94cd3bf17.md", "sha256": "5ef043e60e6163e74962cca82128cacdcc185d7738711ce460278be94cd3bf17"}`; CHECK ещё не запускался; пробел комбинированного прогона передан на оценку.
- Defect history: `D415-413-412-continuation-inputs`, `D415-413-412-wrong-worktree`, `D415-413-412-overlay-order`, `D415-413-412-interim-evidence` — [docs/specs/design/D415-413-412-pdca-process-defects.md](../../docs/specs/design/D415-413-412-pdca-process-defects.md); запись вне кандидата и автокоммитов. Корректирующий продуктовый цикл не открыт.
- Item CHECK issue-415 (`N=1/r=1/n=1`): `accepted`, `415-AC1`, `415-AC2`, `415-AC3` — `met`; исходный отчёт `{"path": ".pdca/issues-415-413-412-issue-415/rv1/report-e083fa17343cd76edf73717643a8c8d747062364d4efc676dca88355172dcdae.md", "sha256": "e083fa17343cd76edf73717643a8c8d747062364d4efc676dca88355172dcdae"}`. Политика: `.pdca/issues-415-413-412-issue-415/rv1/ac3-policy-provenance.md`; квитанция live SQLite: `.pdca/issues-415-413-412-issue-415/rv1/attempts/ac3-policy-receipt.json` (discovery 0, run 0, 1/1 passed); прежние cgroup-пробы сохранены: `.pdca/issues-415-413-412-issue-415/rv1/attempts/ac3-regather.md`.
- Item CHECK issue-413 (`N=1/r=1/n=1`): `accepted` → ACT; `413-C1`, `413-C2`, `413-C3`, `413-C4` — `met`. Кандидат (HEAD и 13 путей), DLL и счётчики неизменны. Точный отчёт: `{"path": ".pdca/issues-415-413-412-issue-413/rv1/report-7023e7afa7abe02e0ee010f493ca2c4989b3411a74726f1c59eca22c10753f62.md", "sha256": "7023e7afa7abe02e0ee010f493ca2c4989b3411a74726f1c59eca22c10753f62"}`. ACT завершён, точная ретроспектива: `{"path": ".pdca/issues-415-413-412-issue-413/rv1/report-4c46d3ab4d7a59c13d0816bc7e66003530545ce630e6d5428647852bacbd545c.md", "sha256": "4c46d3ab4d7a59c13d0816bc7e66003530545ce630e6d5428647852bacbd545c"}`. После ACT #413 группа ещё была `in-progress`; затем выполнен #412 (см. ниже).

- Item CHECK issue-412 (`N=1/r=1/n=1`): `accepted` → ACT; `412-AC1`, `412-AC2`, `412-AC3`, `412-AC4` — `met`. Точный отчёт: `{"path": ".pdca/issues-415-413-412-issue-412/rv1/report-f2ad115ed942bc956e80030ab5cbbc7ed90cd1a9a8dd625cca31753fc1875224.md", "sha256": "f2ad115ed942bc956e80030ab5cbbc7ed90cd1a9a8dd625cca31753fc1875224"}`. HEAD, 10 путей кандидата, DLL-пакет и базовые тесты неизменны; прежние сбои и доказательства сохранены. ACT завершён; ретроспектива: `{"path": ".pdca/issues-415-413-412-issue-412/rv1/report-aa983bddfc8b0ee7feb4f3a81a2304f8bade798d140394dbf2e1fee12ff418ea.md", "sha256": "aa983bddfc8b0ee7feb4f3a81a2304f8bade798d140394dbf2e1fee12ff418ea"}`. G1 — `done`, issue-412 — `done` / `finalized`; внешний автокоммит записан в §Git state.

- Первый independent integrated-tree CHECK: `not accepted`; #412 AC1–4 met, #415/#413 final-artifact evidence unverified (F1). Exact report `{"path": ".pdca/issues-415-413-412/rv1/report-d5363b1d036de493d827efc87913177a95eb27b290a9aa3eecb21eeb22dbf491.md", "sha256": "d5363b1d036de493d827efc87913177a95eb27b290a9aa3eecb21eeb22dbf491"}`. Однократный F1 evidence re-gather разрешён, без дефекта продукта или новых попыток/ревизий; item outcomes unchanged.

- F1: TempViewTests 4/4, SQLite unit 26/26, SQLite live #415 и #413 по 1/1 с native IDs; exit 0. SQLite build exit 0, warnings/errors 0/0. Integration 15/15 до/после; HEAD и test delta неизменны. Evidence `.pdca/issues-415-413-412/rv1/attempts/f1-partial.sha256`.
- F1 остаётся открыт: только live PostgreSQL #413; передача исполнителю `{"path": ".pdca/issues-415-413-412/rv1/f1-postgres-continuation.md", "sha256": "d84eba43342db10c467095ced6c384936862b87b39b1cb4fefde581391ade5f3"}`. Финальный CHECK пакет пока не сформирован.

- F1 completed: PostgreSQL #413 final-tree 1/1, native IDs matched, discovery/execution/reconciliation exit 0. Итоговые доказательства `.pdca/issues-415-413-412/rv1/integration-f1-completed.json`; HEAD и item states неизменны. Тот же независимый CHECK: `{"path": ".pdca/issues-415-413-412/rv1/check-e41210f3e1b32357e809bfc9bf466c6c03bed3c473bc6b146338fa433edf1da3.md", "sha256": "e41210f3e1b32357e809bfc9bf466c6c03bed3c473bc6b146338fa433edf1da3"}`. GitHub issues остаются открытыми в `started`, записей на GitHub нет.

#### Предыдущий CHECK issue-413 и восстановление ресурса (история)

- Item CHECK issue-413 (`N=1/r=1/n=1`): `not accepted`; `413-C1`, `413-C4` met; `413-C2`, `413-C3` unverified; exact report `{"path": ".pdca/issues-415-413-412-issue-413/rv1/report-7e6c50d8140d5ff7cfba02c8c39b29cf40fa3233ba638ec47f3df2c57afe2127.md", "sha256": "7e6c50d8140d5ff7cfba02c8c39b29cf40fa3233ba638ec47f3df2c57afe2127"}`.
- Next allowed step: issue-413 CHECK is paused after the spent C2/C3 re-gather; issue-412 remains `pending`. Resume only once the existing private PostgreSQL wslc endpoint can be verified with its matching nonempty `NEXTORM_POSTGRES_CONNECTION` in the executor session. Evidence: `.pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-regather-endpoint.log`.

- issue-413 remains `in-progress` / `paused`; caller TCP reachability changed, but the approved secure launcher handoff is still missing (`.pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-resume-runtime.md`). issue-412 remains pending.

- PostgreSQL evidence for 413-C2/C3: exact private container authenticated (cluster identity, PG17, database/user, standard search_path and privileges); discovery/run/reconciliation exit 0, 1 expected / 1 selected / 1 passed, 0 failed/skipped/not-run and no ID anomalies. Same HEAD, 13 paths and DLL bundle verified before and after. Evidence: `.pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-authorized-verification.log`, `.pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-authorized-result-verification.log`, `.pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-authorized-roster.json`, `.pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-authorized-receipt.json`, `.pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-authorized-run.command`, `.pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-authorized-run.log`, `.pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-authorized-run.stderr`, `.pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-authorized-run-pre-identity.log`, `.pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-authorized-run-post-identity.log`. Discovery initially had a trailing ANSI reset; corrected by parsing the saved output, without repeating discovery. Container externally owned and left running. CHECK pending review; N=1/r=1/n=1, #412 pending.

- GitHub: отчёт CHECK не выполнял операций с задачами. Отдельно владелец по явной просьбе обновил native `In progress` (`category:started`) для #415, #413, #412. Все остаются открытыми; дальнейших изменений нет.

## Lessons learned

`retrospective_status: complete` — исходный parent-only отчёт `pdca-check`: `{"path": ".pdca/issues-415-413-412/rv1/report-5103d1589626b490461b0e6e5584ef91e99bb61b158fe69a9bbf436c178c0c81.md", "sha256": "5103d1589626b490461b0e6e5584ef91e99bb61b158fe69a9bbf436c178c0c81"}`.

## Git state

- `.pdca/status/collection-issues-415-413-412.md` — создан, untracked (не индексирован), сохранён на диске. Baseline: `c9f2dd90a79a489e8a68a3da16e82be237ab3663`.
- Артефакты подготовки коллекции не индексированы.

- Внешний автокоммит issue-415: `acdcb3fc93214d612f7c5c7133dac9cee44dfb64` (`refs/heads/collection/issues-415-413-412/base`), ровно 9 путей продукта; `.pdca/**` не индексировались.
  - `docs/guide/13-table-valued-functions.md`
  - `docs/ru/guide/13-table-valued-functions.md`
  - `src/nextorm.core/Builders/Entity/EntityBuilder.cs`
  - `src/nextorm.core/Builders/Tvf/TvfJoinSourceExtensions.cs`
  - `src/nextorm.core/DataContext/Sql/SqlSourceRenderer.cs`
  - `src/nextorm.core/PublicAPI.Unshipped.txt`
  - `src/nextorm.core/Query/Commands/QueryCommand.QueryPreparer.cs`
  - `tests/nextorm.integration.tests/T320/SqliteCorrelatedTvfCrossJoinTests.cs`
  - `tests/nextorm.sqlite.tests/T320/CorrelatedTvfCrossJoinSqlTests.cs`

- issue-413: Outer autocommit completed: `30a7fbe3730043dbc9a2f91f1033192ed1ac8948` (`refs/heads/collection/issues-415-413-412/base`); exactly 13 #413 product paths, including only the current delta to the shared API ledger. No `.pdca/**` staged. Реестр 13 путей: `.pdca/issues-415-413-412-issue-413/rv1/candidate-files.txt`.

- Exact #413 commit paths (verified against `.pdca/issues-415-413-412-issue-413/rv1/candidate-files.txt`; evidence: `.pdca/issues-415-413-412-issue-413/rv1/outer-autocommit.log`):
  - `docs/guide/20-create-table-as.md`
  - `docs/ru/guide/20-create-table-as.md`
  - `src/nextorm.core/Builders/TempTables/TempViewExtensions.cs`
  - `src/nextorm.core/DataContext/DataContext.cs`
  - `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs`
  - `src/nextorm.core/DataContext/QueryPlanner.cs`
  - `src/nextorm.core/PublicAPI.Unshipped.txt`
  - `src/nextorm.core/Query/Mutations/Ddl/CreateTempViewCommand.cs`
  - `src/nextorm.core/Query/Mutations/MutationCommand.cs`
  - `src/nextorm.postgres/PostgresDialect.cs`
  - `src/nextorm.sqlite/SqliteDialect.cs`
  - `tests/nextorm.core.tests/Builders/Ddl/TempViewTests.cs`
  - `tests/nextorm.integration.tests/TempViews/TempViewSessionTests.cs`

- Внешний автокоммит #412: `4fe2f25df74a957ce60e0cf5a6a510f68c3feeb6` (`refs/heads/collection/issues-415-413-412/base`), ровно 10 путей продукта; `.pdca/**` не индексировались. Реестр `.pdca/issues-415-413-412-issue-412/rv1/candidate-files.txt`; доказательство `.pdca/issues-415-413-412-issue-412/rv1/outer-autocommit.log`.
  - `docs/guide/20-create-table-as.md`
  - `docs/ru/guide/20-create-table-as.md`
  - `src/nextorm.core/DataContext/DataContext.cs`
  - `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs`
  - `src/nextorm.core/PublicAPI.Unshipped.txt`
  - `src/nextorm.core/Query/Mutations/Ddl/EmptyTempTableSql.cs`
  - `src/nextorm.postgres/PostgresDialect.cs`
  - `src/nextorm.sqlite/SqliteDialect.cs`
  - `tests/nextorm.core.tests/Builders/Ddl/EmptyTempTableTests.cs`
  - `tests/nextorm.integration.tests/TempTables/EmptyTempTableSessionTests.cs`

## Progress log

2026-11-24T06:38:04Z | PLAN | — | — | Инициализирована коллекция; три item PLAN pending, ALL-barrier не пройден | .pdca/issues-415-413-412/rv1
2026-11-24T06:39:02Z | PLAN | — | — | issue-415: родителю разрешён запуск COLLECTION TASK PLAN; только подготовка, без права записи в запись коллекции | .pdca/status/issues-415-413-412-issue-415-1.md
2026-11-24T06:39:02Z | PLAN | — | — | issue-413: родителю разрешён запуск COLLECTION TASK PLAN; только подготовка, без права записи в запись коллекции | .pdca/status/issues-415-413-412-issue-413-1.md
2026-11-24T06:39:02Z | PLAN | — | — | issue-412: родителю разрешён запуск COLLECTION TASK PLAN; только подготовка, без права записи в запись коллекции | .pdca/status/issues-415-413-412-issue-412-1.md
2026-11-24T06:51:43Z | PLAN | revision 1 | attempt 0 | issue-415: plan_state=ready, status=pending; ALL-barrier не пройден | .pdca/status/issues-415-413-412-issue-415-1.md
2026-11-24T06:53:55Z | PLAN | revision 1 | attempt 0 | Notice: issue-412 загрузил nextorm-pdca после первого ответа PLAN; тот же PLAN уточнён до записи (baseline и документация), продуктовой попытки не было | .pdca/status/issues-415-413-412-issue-412-1.md
2026-11-24T06:53:55Z | PLAN | revision 1 | attempt 0 | issue-412: plan_state=ready, status=pending; ALL-barrier не пройден | .pdca/status/issues-415-413-412-issue-412-1.md
2026-11-24T06:56:12Z | PLAN | revision 1 | attempt 0 | issue-413: plan_state=ready, status=pending | .pdca/status/issues-415-413-412-issue-413-1.md
2026-11-24T06:56:12Z | PLAN | — | — | ALL-barrier пройден: 3/3 PLAN; вектор 415=r1, 413=r1, 412=r1; расписание не задано | §Collection phase decisions
2026-11-24T06:59:22Z | PLAN | — | — | Записано расписание G1; группа pending, запуск не выполнялся | .pdca/issues-415-413-412/rv1/report-11eaff4619b5a7c146bbaf13598ba3c8535e6ca8a551878a1ab0256537085257.md
2026-11-24T07:02:13Z | DO | revision 1 | attempt 0 | G1 и issue-415: pending → in-progress; назначенная lane взяла первый элемент очереди, продуктовый DO не начат | .pdca/status/collection-issues-415-413-412.md#items
2026-11-24T07:49:48Z | DO | revision 1 | attempt 1 | issue-415: DO started; in-progress, plan_state=ready | .pdca/status/issues-415-413-412-issue-415-1.md
2026-11-24T08:45:44Z | CHECK | revision 1 | attempt 1 | issue-415: not accepted, 415-AC3 unverified | .pdca/status/issues-415-413-412-issue-415-1.md#check-record
2026-11-24T08:45:44Z | DO | revision 1 | attempt 1 | issue-415: CHECK → DO, однократный добор, in-progress, plan_state=ready | .pdca/status/issues-415-413-412-issue-415-1.md#check-record
2026-11-24T08:47:19Z | DO | revision 1 | attempt 1 | issue-415: однократный добор завершён с ресурсным блокером live запуска; пакет для CHECK | .pdca/issues-415-413-412-issue-415/rv1/attempts/ac3-regather.md
2026-11-24T08:52:43Z | CHECK | revision 1 | attempt 1 | G1 остаётся in-progress; issue-415 in-progress, disposition=paused в CHECK; issue-413 и issue-412 pending | `.pdca/issues-415-413-412-issue-415/rv1/report-ff895bc50e8de50f239b0851cc57bbd497bbc928b867ebda9102b693d4a1e966.md`
2026-11-24T09:32:08Z | CHECK | revision 1 | attempt 1 | G1 и issue-415 in-progress; disposition=active, изменение ресурсного условия; очередь без изменений | `.pdca/issues-415-413-412-issue-415/rv1/ac3-policy-provenance.md`
2026-11-24T09:41:06Z | ACT | revision 1 | attempt 1 | issue-415 CHECK → ACT (accepted); issue-415 и G1 in-progress, disposition=active до завершения ACT | `{"path": ".pdca/issues-415-413-412-issue-415/rv1/report-e083fa17343cd76edf73717643a8c8d747062364d4efc676dca88355172dcdae.md", "sha256": "e083fa17343cd76edf73717643a8c8d747062364d4efc676dca88355172dcdae"}`
2026-11-24T09:49:30Z | ACT | revision 1 | attempt 1 | issue-415: done, disposition=finalized; G1 in-progress | .pdca/status/issues-415-413-412-issue-415-1.md#act-record
2026-11-24T09:50:11Z | ACT | revision 1 | attempt 1 | Внешний автокоммит issue-415: 9 путей | `acdcb3fc93214d612f7c5c7133dac9cee44dfb64`
2026-11-24T09:54:08Z | DO | revision 1 | attempt 0 | G1: issue-413 pending → in-progress, disposition=active; только переход статуса очереди, DO не начат | .pdca/status/collection-issues-415-413-412.md#items
2026-11-24T10:18:42Z | DO | revision 1 | attempt 1 | issue-413: DO started, r1 применяется после #415; issue-412 остаётся pending | .pdca/status/issues-415-413-412-issue-413-1.md
2026-11-24T11:01:02Z | CHECK | revision 1 | attempt 1 | issue-413: CHECK not accepted; C2/C3 unverified | .pdca/status/issues-415-413-412-issue-413-1.md#check-record
2026-11-24T11:01:58Z | CHECK | revision 1 | attempt 1 | issue-413 disposition=paused; no phase transition; issue-412 pending | .pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-regather-endpoint.log
2026-11-24T12:45:42Z | CHECK | revision 1 | attempt 1 | issue-413 remains paused in CHECK; issue-412 pending | .pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-resume-runtime.md
2026-11-24T12:53:24Z | CHECK | revision 1 | attempt 1 | issue-413 active: authenticated endpoint verified; same pending CHECK | .pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-authorized-verification.log
2026-11-24T13:09:08Z | CHECK | revision 1 | attempt 1 | Authenticated PostgreSQL run complete; native IDs reconciled; same CHECK pending | .pdca/issues-415-413-412-issue-413/rv1/attempts/postgres-authorized-receipt.json
2026-11-24T13:16:44Z | ACT | revision 1 | attempt 1 | issue-413 CHECK → ACT (accepted), 413-C1–C4 met; issue-413 и G1 in-progress, issue-413 active, issue-412 pending | .pdca/status/issues-415-413-412-issue-413-1.md#check-record
2026-11-24T13:39:21Z | ACT | revision 1 | attempt 1 | issue-413: done, disposition=finalized; G1 in-progress, issue-412 pending; автокоммит заблокирован пересечением путей | .pdca/status/issues-415-413-412-issue-413-1.md#act-record
2026-11-24T13:46:10Z | ACT | revision 1 | attempt 1 | Issue-413 outer autocommit recorded; 13 exact product paths; #412 pending | `30a7fbe3730043dbc9a2f91f1033192ed1ac8948`
2026-11-24T13:52:00Z | DO | revision 1 | attempt 0 | G1: issue-412 pending → in-progress, disposition=active; только переход статуса очереди, продуктовый DO не начат | .pdca/status/collection-issues-415-413-412.md#items
2026-11-24T14:33:18Z | DO | revision 1 | attempt 1 | issue-412 DO started; r1 unchanged; lane HEAD 30a7fbe3730043dbc9a2f91f1033192ed1ac8948 | `{"path": ".pdca/issues-415-413-412-issue-412/rv1/report-d87f5987f5ca75e76674ab270d4fdf754b8f01a28e4ee2f83c837cb3ecc891d7.md", "sha256": "d87f5987f5ca75e76674ab270d4fdf754b8f01a28e4ee2f83c837cb3ecc891d7"}`
2026-11-24T14:43:20Z | DO | revision 1 | attempt 1 | issue-412 candidate frozen; developer evidence complete, independent CHECK pending | .pdca/issues-415-413-412-issue-412/rv1/do-result.md
2026-11-24T14:50:12Z | CHECK | revision 1 | attempt 1 | issue-412: независимый CHECK accepted; 412-AC1–AC4 met | .pdca/issues-415-413-412-issue-412/rv1/report-f2ad115ed942bc956e80030ab5cbbc7ed90cd1a9a8dd625cca31753fc1875224.md
2026-11-24T14:50:12Z | ACT | revision 1 | attempt 1 | issue-412: CHECK → ACT; ретроспектива ожидается, элемент и G1 остаются in-progress | .pdca/issues-415-413-412-issue-412/rv1/report-f2ad115ed942bc956e80030ab5cbbc7ed90cd1a9a8dd625cca31753fc1875224.md
2026-11-24T15:00:20Z | ACT | revision 1 | attempt 1 | issue-412: done / finalized; G1 done, 3/3; Integrated-tree CHECK pending | .pdca/status/issues-415-413-412-issue-412-1.md#act-record
2026-11-24T15:00:55Z | ACT | revision 1 | attempt 1 | Внешний автокоммит #412 записан: 10 путей; Integrated-tree CHECK остаётся pending у родителя | `4fe2f25df74a957ce60e0cf5a6a510f68c3feeb6` · .pdca/issues-415-413-412-issue-412/rv1/outer-autocommit.log
2026-11-24T15:11:01Z | CHECK | — | — | Подготовлен пакет независимого integrated-tree CHECK; вердикт не получен | .pdca/issues-415-413-412/rv1/check-5ef043e60e6163e74962cca82128cacdcc185d7738711ce460278be94cd3bf17.md
2026-11-24T15:17:15Z | CHECK | — | — | F1: not accepted; разрешён единичный combined-tree evidence re-gather | .pdca/issues-415-413-412/rv1/report-d5363b1d036de493d827efc87913177a95eb27b290a9aa3eecb21eeb22dbf491.md
2026-11-24T15:20:10Z | CHECK | — | — | F1: unit и SQLite live собраны; PostgreSQL передаётся без новой попытки | .pdca/issues-415-413-412/rv1/f1-postgres-continuation.md
2026-11-24T15:30:38Z | CHECK | — | — | F1 evidence complete; recheck ready, no verdict | .pdca/issues-415-413-412/rv1/check-e41210f3e1b32357e809bfc9bf466c6c03bed3c473bc6b146338fa433edf1da3.md
2026-11-24T15:34:55Z | CHECK | — | — | Independent integration PASS, 11/11 met; F1 closed | .pdca/issues-415-413-412/rv1/report-893f004e287c796e3829ffab1b12c1502c67d5c397acadbacb0153d11c8aed60.md
2026-11-24T15:34:55Z | ACT | — | — | ACT открыт, Lessons learned pending | .pdca/status/collection-issues-415-413-412.md#lessons-learned
2026-11-24T15:38:20Z | ACT | — | — | Коллекция полностью принята; ретроспектива записана, ACT завершён; worktree сохранён | .pdca/issues-415-413-412/rv1/report-5103d1589626b490461b0e6e5584ef91e99bb61b158fe69a9bbf436c178c0c81.md
