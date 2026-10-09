# D189 — ToDataReader/ToDataReaderAsync: support SQLite for non-LOB projections

- task_id: D189
- issue: #189 (milestone 1.0.9-rc2)
- selected_variant: pdca-dotnet
- collection: 1.0.9-rc2 (`docs/specs/status/collection-1.0.9-rc2.md`)
- branch: 1.0.9-rc2 · baseline ref: 18659e41
- cycle: N=1 · plan revision: r=3 · attempt: n=1
- contract: rv=3
- plan_state: ready
- phase: STOP (r=3/rv=3/n=1) — E09 perf predicate unresolved; #189 OPEN; awaiting user authorization
- predecessor: D134 / #134 (commit 65477266; `docs/specs/status/rc1-tail-134-sqlite-datareader-1.md`; CHECK r=1/n=3 PASS)
- design source: `docs/specs/design/issue-189-todatareader-sqlite.md` (variant A; notice :8-14)

## Goal
Подтвердить результат D134 на базе rc2 18659e41, устранить доказанные устаревшие комментарии и закрыть #189 как реализованный; не повторять реализацию variant A.

## Why not gap
Актуальная проверка rc2 и reconciliation остаются проверяемыми deliverables; возможное противоречие — `tests/nextorm.integration.tests/Providers/ITestProvider.cs:152-157` против `SqliteTestProvider.cs:47`.

## Scope
IN: верификация R01–R10, точечные тесты при отсутствующих сценариях, комментарии/XML-doc тестовой инфраструктуры, status/collection/issue.
OUT: изменение runtime, API, маршрутизации, chunked SQLite BLOB, milestone.
Runtime/product files не изменяются, включая XML-doc `QueryCommandExtensions.cs:300-378`; изменения тестов не считаются изменением продуктовых типов. Обнаруженный runtime-дефект требует CHECK→PLAN, а не скрытого расширения этой задачи.

## Acceptance criteria (AC, each with negative case)
- **AC01 [R01,R02,R05]:** SQLite — правильные FieldCount/Select-order, buffered single/mixed LOB, sync/async и все четыре существующих overload. Негатив: лишний rowid, неверный ordinal, locator вместо значения, расхождение overload — FAIL.
- **AC02 [R03,R04]:** PG/SQL Server сохраняют sequential-путь; неподдерживаемые провайдеры/in-memory и lazy temp fail closed; CSV сохраняет своё сообщение. Негатив: открытый unsupported reader, неверный terminal либо изменение PG/SQL Server — FAIL.
- **AC03 [R06,R07]:** caller ownership, освобождение reader+per-call command, повторное использование контекста, `storeInCache:false`, отсутствие sticky shared-command mutation. Негатив: утечка ресурсов/флага или поломка следующего запроса — FAIL.
- **AC04 [R08,R09]:** streaming class-row и отличие buffered/sequential документированы; ложные комментарии исправлены, верные сохранены. Негатив: обещание chunked SQLite BLOB или объявление работающего SQLite unsupported — FAIL.
- **AC05 [R10]:** Debug/Release — 0 warnings/errors; реальные SQLite/PG/SQL Server/MySQL/ClickHouse выполнены; coverage и predecessor perf доказательства доступны. Негатив: skipped обязательного провайдера, недоступный применимый evidence или один build вместо двух — PASS запрещён.

## Task list
- **D189.1 — fix now:** закрепить этот PLAN/contract в `docs/specs/status/rc2-189-sqlite-datareader-1.md` до DO; затем проверить base/predecessor, toolchain, issue URL, существующие test identities и evidence inventory.
- **D189.2 — fix now:** проверить `tests/nextorm.integration.tests/Providers/ITestProvider.cs:152-157`; при подтверждении заменить устаревшее XML-описание на locator-free buffered SQLite support.
- **D189.3 — fix now:** проверить `tests/nextorm.core.tests/LobStreamingTests.cs:11,302`; «relational-only» само по себе верно при неподдерживаемом in-memory — не менять без конкретного противоречия.
- **D189.4 — fix now:** выполнить матрицу существующими тестами; недостающие перечисленные assertions добавить только в `LobDataReaderTests.cs`, `ToDataReaderSqliteTests.cs`, `ResultReaderTests.cs`, `CommonTestSuite.Lob.cs`; не придумывать заранее новые test symbols.
- **D189.5 — fix now:** провести один boundary sweep, заполнить ledger actual/failed/not-run/blocked по каждой строке rv=1 и передать CHECK. Runtime-дефект или новая обязательная разновидность → новая P-задача.

## Deferred + trigger
- true chunked SQLite BLOB — только отдельная утверждённая feature.
- новые interceptor-specific тесты — при изменении reader/cache/interception seam.
- оптимизация cached path — при подтверждённой регрессии, не в D189.

## Variant matrix (M1–M8)
Правило развёртки: строки M1–M4 разворачиваются по всем указанным projection × sync/async; для каждой проверяются FieldCount/ordinals, затем cache/interceptor строки M5–M7. Это полный контракт, не выборочная иллюстрация.
- **M1 test:** SQLite locator × {non-LOB, single byte[]/string LOB, mixed LOB+non-LOB} × sync/async → locator-free buffered values, Select-order/FieldCount, forward-only; negative rowid/locator/неожиданный NotSupportedException.
- **M2 test:** PG и SQL Server non-locator × те же projections × sync/async → existing sequential semantics; ordinal/FieldCount и ownership; negative переключение на buffered или sequential-regression.
- **M3 guard+test:** MySQL/MariaDB/ClickHouse non-sequential × все projections × sync/async → terminal-named NotSupportedException до чтения; FieldCount/ordinals N/A лишь после наблюдаемого отказа.
- **M4 guard+test:** in-memory × все projections × sync/async → fail closed; FieldCount/ordinals/provider command N/A лишь после отказа, не по отсутствию отчёта.
- **M5 test+source guard:** M1–M4 × cached/prepared состояние контекста → supported per-call preparation не сохраняет план и не мутирует shared QueryCommand; unsupported вызов не меняет его; последующий cached query остаётся работоспособен.
- **M6 guard:** M1–M4 × interceptor absent/present → Roslyn call-path/source comparison подтверждает неизменность существующего interception seam; absent покрывается запуском тестов. Новые present-specific сценарии deferred с триггером изменения seam; R01–R10 этим не ослабляются.
- **M7 test/guard:** span/object[] и все существующие overload; token/default/cancellation на поддерживающих token overload; lazy temp failure и CSV wording. TVP — guard неизменного пути, новые TVP-reader сценарии deferred при его изменении.
- **M8 test/guard:** value/reference projections, class rows, null/DBNull/default значения; отсутствующий ctor — guard reader не материализует TResult. Null API-аргументы сохраняют существующий контракт, который D1 фиксирует, а D4 проверяет на затронутых overload.
- **Surface/flush:** QueryCommand<TResult> — applicable; EntityBuilder<TEntity> прямой terminal N/A только после Roslyn-подтверждения Select→QueryCommand (D134 R08); target/flush N/A: reader terminal не принимает writer/target и не flush-ит.

## Test strategy
unit — core dialect/fail-closed/wrapper/cache и SQLite actual-reader/SQL; integration — настоящий provider execution, включая поддерживаемые и fail-closed провайдеры. Сначала focused commands F1–F3; полный solution sweep B — ровно один на границе DO→CHECK.

Artifacts root: `A=artifacts/pdca/D189/r1` (coder создаёт каталог в DO). Все команды с сохранением exit code (`pipefail` при tee), stdout+stderr → `$A/<command-ID>.log`; ожидается exit 0, кроме явно проверяемых exception assertions внутри тестов.

- **F1:** `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~LobDataReaderTests|FullyQualifiedName~LobStreamingTests"`
- **F2:** `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~ToDataReaderSqliteTests|FullyQualifiedName~ResultReaderTests|FullyQualifiedName~LobSqlGenerationTests"`
- **F3:** `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~ToDataReader|FullyQualifiedName~Lob"`
- **B, единственный boundary sweep:** `dotnet build -c Debug` → `dotnet build -c Release` → `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet-coverage collect -s coverage.settings.xml -f cobertura -o "$A/coverage.xml" "dotnet test -c Debug"` → `reportgenerator "-reports:$A/coverage.xml" "-targetdir:$A/coverage-report" "-reporttypes:Html;TextSummary"`
- **Runner prerequisite:** coder сначала загружает `.opencode/skills/running-integration-tests/SKILL.md`; при отсутствующем socket запускает `"/mnt/c/Program Files/RedHat/Podman/podman.exe" machine start`, ожидает socket и повторяет F3/B. Пропущенные containers не дают зелёного результата.
- **Coverage/branch/mutation:** полный B отчёт; 85% line/75% branch, hard на main, warning на этой ветке, оба результата записать. Structural runtime branch-delta должен быть 0; sibling edge-list сверить с M1–M8. Stryker.NET на изменённых product types: ожидаемый набор пуст; N/A только по observed diff+Roslyn inventory, иначе возврат PLAN.

## Docs plan
Проверить без изменения `docs/guide/26-large-objects.md`, `docs/ru/guide/26-large-objects.md`, `docs/advanced/api-reference.md`, `docs/ru/advanced/api-reference.md`; остальные EN/RU файлы R09 — по точному predecessor inventory. Исправляем лишь подтверждённые D2–D3 артефакты; runtime XML-doc четырёх overload не трогаем.

## Perf-measurement decision
Новый замер **не нужен**, потому что runtime path не меняется (`QueryCommandExtensions.cs:334-336,388-390`; `DataContext.cs:468-488`). Обязательны inherited D134 R10 actual cached-path baseline/результат и R07 cached-vs-prepared данные плюс доказанный нулевой runtime diff; при изменении пути — replan с новым baseline/замером.

## Reconnaissance decision
Design spike не нужен: variant A уже утверждён и реализован (`design:8-14,43`). Нужна ограниченная fact-разведка D1: реальные test identities, комментарии, seam и архивный evidence; наблюдаемый результат — заполненный inventory без догадок.

## Unit execution mode
Последовательно в одном дереве — общий reader/cache contract и пересекающиеся тесты; дополнительный worktree для D189 не нужен. Размещение группы/DAG и разрешённый collection auto-commit устанавливает оркестратор до исполнения; прямой commit в `1.0.9-rc2` не разрешается этим планом.

## Footprint
четыре тестовых файла (D4), два comment/XML файла (D2–D3), новый status и `docs/specs/status/collection-1.0.9-rc2.md:48`; точные новые test locations появятся в ledger. `src/**` и публичные docs — read-only.

## Predecessor-result requirements
`65477266`, status `rc1-tail-134-sqlite-datareader-1.md:8,31-54`; E01 связывает все R01–R10 с actual predecessor evidence, а E02–E09 проверяют их применимость/current rc2 result. Один `done` не заменяет ledger/артефакты.

## Evidence contract (rv=1)
Общий формат: каждая строка содержит requirement ID/row ID, scenario, kinds+sources, invocation/result/log, artifacts, owner, predicate, rv. Все будущие источники/артефакты ниже **planned**; статус сохраняет actual evidence отдельно.
- **R01–R10/E01 | rv=1 | predecessor:** документальные evidence+provenance из D134 status/его ссылок; `git show 65477266:docs/specs/status/rc1-tail-134-sqlite-datareader-1.md` и `git show --stat 65477266`, exit 0; `$A/predecessor.md`, `$A/predecessor-map.md`, логи; owner DO-audit; predicate always; map требует actual evidence каждого R, включая perf.
- **R01,R02,R05/E02 | rv=1 | M1/M7/M8:** test reports+assertion inventory из SQLite файлов `:147,159,189,216,386`; F2 exit 0, matched tests >0, все обязательные variants привязаны к actual identities; `$A/F2.log`, `$A/variant-ledger.md`; owner DO-tests; predicate always.
- **R03,R04,R10/E03 | rv=1 | M2–M4 real providers:** execution reports из `CommonTestSuite.Lob.cs:787-925` и provider fixtures; F3 exit 0, matched >0, требуемые providers executed/not skipped; `$A/F3.log`, `$A/provider-ledger.md`; owner DO-integration; predicate always.
- **R04,R06,R07/E04 | rv=1 | fail-closed/ownership/cache/temp:** assertions из core `LobDataReaderTests.cs:30-155,270-446` плюс SQLite; F1+F2 exit 0, matched >0, ledger показывает failure guards/disposal/reuse/cache invariants; `$A/F1.log`, `$A/F2.log`, `$A/variant-ledger.md`; owner DO-tests; predicate always.
- **R03,R05,R07,R08/E05 | rv=1 | routing/surfaces/cache/interceptor/TVP:** semantic/source evidence; tool invocation `roslyn(action="structure")`, затем `members`/`refs` для IDs, полученных этим вызовом, плюс `git diff --exit-code 18659e41 -- src`; successful tool results, diff exit 0; `$A/semantic-audit.md`, `$A/runtime-diff.log`; owner DO-audit; predicate always, surface N/A только по M30 observations.
- **R08,R09/E06 | rv=1 | docs/comments:** text evidence из четырёх публичных файлов D40, predecessor R09 inventory, `ITestProvider.cs:152-157`, `LobStreamingTests.cs:11,302`; `git diff --check` и `git diff -- tests/nextorm.integration.tests/Providers/ITestProvider.cs tests/nextorm.core.tests/LobStreamingTests.cs`, exit 0; `$A/docs-audit.md`, `$A/comment-diff.log`; owner DO-docs; predicate always.
- **R10/E07 | rv=1 | builds+boundary/provider completeness:** build/test logs из B; все вызовы exit 0, оба build 0 warnings/errors, required tests executed; `$A/B-debug.log`, `$A/B-release.log`, `$A/B-test.log`, `$A/provider-ledger.md`; owner DO-boundary; predicate always.
- **R10/E08 | rv=1 | coverage+edges+mutation:** B coverage/report commands exit 0, line+branch metrics и policy result; E05 invocation доказывает empty changed-product-type set; `$A/coverage.xml`, `$A/coverage-report/`, `$A/branch-mutation.md`; owner DO-quality; coverage/edge predicate always, Stryker applicability iff observed changed product types nonempty — тогда этот план блокируется для replan.
- **R07,R10/E09 | rv=1 | cached ratio/perf inheritance:** actual D134 measurement evidence+current E05; E01 invocation exit 0 и readable numeric baseline/result/ratio с source identities, E05 diff exit 0; `$A/perf-inheritance.md`, predecessor artifact locations из его ledger; owner DO-perf-audit; predicate always; отсутствующие архивы = open, не N/A.
- **R10/E09.M01 | rv=3 | measurement execution (supersedes the rv=2 E09 procedure):** run exactly the discovered acceptance cases against the D134 historical baseline; invocation `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter <reconciled set>`; all 7 historical cases executed, exit 0; `$A/measurement.log`, `$A/discovery.log`; owner DO-perf; predicate: every case has a numeric result.
- **R07/E09.M02 | rv=3 | membership reconciliation:** reconcile the discovered case set against the D134 documented historical set (`docs/specs/performance/comparison-benchmark-scenarios.md`, D134 status); no substituted or invented cases; `$A/membership.md`; owner DO-perf-audit; predicate: 7/7 names match, 0 added, 0 dropped.
- **R07,R10/E09 closure | rv=3 | closure:** global `G_current ≤ 48.08 s` AND ratio `G_current/48.08 ≤ 1.0000`; all 7 historical cases executed; membership reconciled; `$A/perf-closure.md`; owner DO-perf; predicate: both inequalities hold and E09.M01+E09.M02 PASS.
- **R01–R10/E10 | rv=1 | footprint/format:** `git diff --name-only`, `git diff --check`, `git diff --exit-code 18659e41 -- src`; exit 0, scope соответствует D20, runtime нет, редактированные файлы CRLF; `$A/footprint.md`, `$A/format.log`; owner DO-audit; predicate always.
- **R01–R10/E11 | rv=1 | CHECK reconciliation:** invocation `Task(check, "D189 r=1 N=1: reconcile rv=1 E01–E12 ledger; validate M1–M8 and predecessor map")`; structured PASS/FAIL/blocked result и все applicable evidence rows закрыты до PASS; `$A/check-verdict.md`; owner CHECK; predicate always.
- **R09,R10/E12 | rv=1 | tracking/ACT:** `gh issue view 189 --json number,url,state,milestone`, exit 0, verified URL recorded before handoff; после PASS `gh issue close 189 --comment "Implemented by D134; verified and reconciled on rc2 by D189. Evidence: docs/specs/status/rc2-189-sqlite-datareader-1.md."`, exit 0, повторный view подтверждает CLOSED; `$A/issue-before.json`, `$A/issue-after.json`, status+collection row; owner ACT; identity check always, close predicate observed CHECK PASS.

Contract policy: rv=1 первоначальная, supersession нет. Новая обязательная разновидность требует justified rv=2, явного supersession rv=1, сохранения ID/обязательств и новых row IDs; отсутствие отчёта само по себе ревизию не оправдывает.
CHECK re-gather budget: owner CHECK, максимум **2 точечных gather-запроса суммарно**, каждый — по явно открытым row IDs; без изменения продукта, новой DO-попытки или повторного полного sweep. После исчерпания applicable open rows → blocked/escalate; проверенный дефект → FAIL.

## Priorities
Все строки E01–E12 — P1 по инвариантам R01–R10 и overlay `:90,:92`; streaming surfaces/token/params/ownership/providers и query-cache входят явно. CHECK не понижает их из-за небольшого diff.

## Risks / assumptions
- predecessor artifacts могут быть недоступны.
- MTP selector может не совпасть с реальным identity — zero matches не успех, уточнить из runner/Roslyn inventory.
- socket/tools восстанавливаются в D1; не принимать provider skips за PASS.
Классификация возможного DO→PLAN: setup — additive prerequisite с исходным D189 active/blocked; нехватка evidence — targeted scout, не продуктовый FAIL; стойкая низкая уверенность — escalate trigger 5; доказанный внешний ресурсный blocker — рекомендовать вызов escalate.
Уверенность: высокая в выборе verification-only и SQLite buffered semantics; архивная полнота, точные test identities и stale comments ещё требуют наблюдения. Эти неопределённости не ослабляют AC.

## Gate 1 / ACT
coder записал полный PLAN+rv=1 до DO; autonomous collection не ждёт `go`. Design checklist пройден при соблюдении no-runtime scope. ACT после PASS закрывает D189, обновляет collection row и #189, фиксирует связь с #134/D134; исторический milestone discrepancy не переносится.

## Progress log
- PLAN r=1 (P:D189-P01) written to this status file. plan_state=ready. DO not started. Contract rv=1 pinned.
- 2026-10-09T06:29:29Z | DO | revision r=1 | iteration n=1/3 | DO started; durable state N=1,r=1,n=1; gate pinned (base 18659e41, HEAD f7a6d728, dotnet 10.0.401, Podman socket present, scope.json brief exit 0) | /tmp/nextorm-rc2-189-r1/gate.md
- 2026-10-09T06:38:31Z | DO | revision r=1 | iteration n=1/3 | D189.1–D189.5 complete. D189.2 stale XML fixed in tests/nextorm.integration.tests/Providers/ITestProvider.cs (SQLite described locator-free buffered, not rowid/fail-closed). D189.3 LobStreamingTests.cs:11,302 "relational-only" verified NOT stale (SQLite is relational), unchanged. D189.4 added ToDataReader_Sqlite_SingleBlobProjection_ShouldReadWholeBufferedValue + Async (ToDataReaderSqliteTests.cs:273,300) for the previously uncovered single byte[] LOB projection; mixed LOB+non-LOB already covered by SqliteSpecificTests.cs:570,601 (recorded, not duplicated); in-memory guards already covered (M4). D189.5 boundary: nextorm.slnx Debug+Release 0/0; integration F3 real providers (PG/SQLServer/MySQL/SQLite executed, capability skips only); broad sweep 9787 total/0 failed/9589 passed/198 skipped; coverage Line 88.3% / Branch 80.3% (≥85/75). ClickHouse/MariaDB do not derive CommonTestSuite → ToDataReader fail-closed recorded reachable only via core dialect gate (LobDataReaderTests.cs:79-104), not a defect. Footprint: only the two test files changed; git diff --check clean; working-tree src diff empty (D189 makes no runtime change; `git diff 18659e41 -- src` is non-empty because 18659e41 is the pre-existing rc2 collection baseline). Evidence: /tmp/nextorm-rc2-189-r1/{gate.md,scope.json,evidence.json,variants.md}, logs under green/ and boundary/. validate_inner_loop.py report exit 0. Awaiting CHECK.
- 2026-10-09T06:50:22Z | PLAN | revision r=2 | iteration n=1/3 | Planner revised plan r=2 / rv=2 / N=1 / n=1 (same task); rv=2 supersedes ONLY the source-identity baseline of E05/E09/E10 from rv=1 (rv=1 checked `git diff 18659e41 -- src`, where 18659e41 is the pre-existing rc2 collection baseline, not the D189 start). Requirement IDs preserved; E01–E04/E06–E08/E11/E12 retained unchanged; rv=1 re-gather budget stays recorded as 2/2 consumed. New pinned start identity = f7a6d728, predecessor = 65477266. New artifact dir /tmp/nextorm-D189-r2-n1/. | docs/specs/status/rc2-189-sqlite-datareader-1.md
- 2026-10-09T06:50:22Z | DO | revision r=2 | iteration n=1/3 | rv=2 E05/E09/E10 re-baselined on f7a6d728 (see /tmp/nextorm-D189-r2-n1/e05-results.txt, e10-results.txt, e09-applicability.md).
- 2026-10-09T06:54:49Z | PLAN | revision r=3 | iteration n=1/3 | Planner revised plan r=3 / rv=3 / N=1 / n=1. rv=3 supersedes rv=2 for the E09 evidence procedure ONLY: E09 stable requirement links R10 (gates) and R07 (cached-path/perf invariants); the E09 row ID is preserved and split into subrows E09.M01 (measurement execution) and E09.M02 (membership reconciliation) plus the existing E09 closure row. E01–E04/E06–E08/E10–E12 unchanged. Planner decision: targeted acceptance measurement against the D134 historical baseline (no new benchmark scaffolding). E09 closure predicate: global G_current ≤ 48.08 s AND ratio G_current/48.08 ≤ 1.0000, all 7 historical cases executed, membership reconciled, no substituted or invented cases. Artifact root A=/tmp/nextorm-d189-e09-rv3-f7a6d728. | A
- 2026-10-09T06:54:49Z | DO | revision r=3 | iteration n=1/3 | rv=3 provisioning + discovery started (provision.exit, discovery.log); measurement NOT executed pending scout membership reconciliation. | A
- 2026-10-09T06:58:20Z | DO | revision r=3 | iteration n=1/3 | E09.M02 membership reconciled: 7/7 frozen (discovery == D134 historical set), 0 added/dropped/substituted/invented ($A/membership.md). E09.M01 measurement executed once, exit 0; raw BDN line `Global total time: 00:00:55 (55.05 sec), executed benchmarks: 7`; G_current=55.05 s vs G_baseline=48.08 s; ratio=1.1450; 7/7 cases executed. Closure predicate FAIL (G_current > 48.08 and ratio > 1.0000); E09 remains OPEN, no noise allowance invented. Per-case Means/Allocated and cached/prepared ratio 0.4905 in $A/comparison.md; evidence reports in $A/BenchmarkDotNet.Artifacts/. Working-tree guard `git diff --quiet f7a6d728 -- src benchmarks …` exit 0; tracked benchmarks/BenchmarkDotNet.Artifacts clean. DO→CHECK: E09 FAIL needs CHECK/planner decision. | /tmp/nextorm-d189-e09-rv3-f7a6d728/{membership.md,comparison.md,measurement.log,measurement.exit}
- 2026-10-09T07:00:48Z | STOP | revision r=3 | iteration n=1/3 | **D189 STOP (escalate decision).** State r=3 / rv=3 / n=1. E09 numeric closure predicate FAIL: `G_current = 55.05 s`, `ratio = G_current / 48.08 = 1.1450`, 7/7 cases executed, measurement exit 0; E09.M01 membership satisfied (7/7 frozen, 0 added/dropped/substituted). E05 (pinned start f7a6d728, D134 ancestor, `src` immutability) and E10 (final no-runtime-change guard) GREEN. Escalate decision (verbatim-summarized): **E09 must NOT be closed; no post-hoc predicate change and no unproved environmental attribution** (the single ShortRun observation is not re-interpreted as noise). D189 is functionally confirmed (D134 behaviour verified, stale comment fixed, test-only assertions added) but **not closeable**; #189 remains OPEN. The two edited test files remain uncommitted; green test-only patch preserved at `docs/specs/status/rc2-189-evidence/D189-STOP.patch`. Await explicit user authorization for either (a) a pre-declared paired A/B measurement (`git worktree` on `65477266` vs HEAD, alternating order, pre-fixed per-case tolerance; `Global total time` informational), or (b) an explicit written contract waiver (`ACCEPTED-DEVIATION`). Remaining measurement/re-gather budget: **0**. Evidence archived under `docs/specs/status/rc2-189-evidence/`. | docs/specs/status/rc2-189-evidence/
