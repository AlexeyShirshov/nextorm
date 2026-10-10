# D179 / issue #179 — JSON streaming: partial-output/error policy
- task_id: D179
- issue: #179
- selected_variant: pdca-dotnet
- base: branch 1.0.9-rc2 @ 2ac20818 (rebased; D177 native path committed at 2ac20818)
- cycle: 1
- plan_revision: r2
- iteration: n=2/3
- plan_state: ready
- rv: 2
- status: ACT complete; CHECK PASS
- mode: autonomous (collection record); auto-commit only per collection exception; push never

## Goal
Установить проверяемое, одинаково описанное поведение JSON streaming после ошибки (**fail-stop / partial-output**), не обещая атомарность произвольного destination.

## Decision (policy)
- ошибка или отмена прекращает операцию и распространяется вызывающему;
- нет намеренного rollback, повторной записи или recovery-вызова `Complete`;
- валидность всего результата после ошибки не гарантируется;
- destination остаётся открытым и принадлежит вызывающему.
Обоснование: опубликованные EN/RU документы уже описывают partial-output (`docs/guide/28-streaming-data.md:95-97`, RU `:96-98`); CSV закрепляет аналогичный контракт (`tests/nextorm.sqlite.tests/CsvStreamTests.cs:293,301,322,370`); Q5 оставлен несогласованным (`docs/specs/roadmap/todo_json_streaming.md:282`). Откат/валидный хвост отвергнуты (произвольный non-seekable destination, ошибка внутри строки/неисправный sink). Эскалация не требуется — политика decidable по evidence.

## Scope / out of scope
В области: существующие sync/async `WriteJson`/`WriteJsonAsync`, array/NdJson, обе публичные поверхности (`QueryCommand.TResult.cs:145-190`, `EntityBuilderExtensions.cs:158-203`), ошибки/отмена, ownership, fail-closed.
Вне области: новые policy options/атомарный API, изменение сигнатур/`params`, Phase 2/3, SQL/cache/planner, новые правила конвертации и типы исключений — **#180**.

## Acceptance criteria
- R179-01: при ошибке до нормального завершения нет намеренных rollback/recovery-tail.
- R179-02: array может остаться неполным; у NdJson последняя запись может быть неполной; полная валидность после ошибки не обещается.
- R179-03: исходная ошибка распространяется; cancellation не превращается в success; cleanup не маскирует ошибку.
- R179-04: destination открыт, префикс не откатывается; библиотека не добавляет `Stream.Flush`.
- R179-05: pre-output validation, in-memory и temp-table отказ оставляют destination неизменным.
- R179-06: успешный вывод, sync/async parity, rollover и defaults не регрессируют.
- R179-07: EN/RU, XML docs, API reference и Q5 согласованы; #180 carve-out сохранён.
Негативные контроли: oracle отвергает recovery-tail, rollback префикса, закрытый destination.

## Variant matrix (V01–V21)
V01 sync×array×serialize/read-abort — тест; V02 sync×array×CT — guard; V03 sync×array×sink I/O — тест; V04 sync×NdJson×serialize/read-abort — тест; V05 sync×NdJson×CT — guard; V06 sync×NdJson×sink I/O — тест; V07 async×array×serialize/read-abort — тест; V08 async×array×cancellation после префикса — тест; V09 async×array×sink I/O — тест; V10 async×NdJson×serialize/read-abort — тест; V11 async×NdJson×cancellation — тест; V12 async×NdJson×sink I/O — тест; V13 pre-output validation×sync/async×режимы — тест (байты неизменны); V14 in-memory×sync/async — тест fail-closed; V15 temp-table×sync/async — тест fail-closed; V16 обе публичные поверхности — тест; V17 empty/nonempty/успешные/parity/rollover — тест; V18 seekable/non-seekable sink с префиксом — тест (без rollback); V19 SQLite/PG/SQL Server/MySQL/MariaDB/ClickHouse — guard общей managed-ветки + integration regression; V20 default/явные options и validation — тест/guard; V21 value/reference payload/provider conversion — guard неизменённого row writer; новые conversion-правила deferred → #180.
Для V01/V04/V07/V10: (а) delegate бросает после начала текущей строки; (б) abort между строками после успешного префикса — это writer-lifecycle, не выдавать за provider fault injection; guard связывает с `QueryExecutor.cs:1170-1171,1203-1204` и `JsonStreamWriter.cs:118-126`.

## D-tasks
- D179-01 (fix now): persist план/контракт, синхронизировать collection entry; ready ≠ DO done.
- D179-02 (fix now): unit seam + публичные fault-тесты V01–V20; усилить `JsonStreamingTests.cs:508,523,451-490,536`.
- D179-03 (fix now): EN/RU/XML/API-reference/Q5 + cross-reference #180.
- D179-04 (fix now): affected rebuild, regression, integration, coverage, acceptance, CHECK evidence.
- D179-05 (deferred): production fix только при доказанном нарушении R179-01…06 → replan.

## Footprint
- `tests/nextorm.core.tests/JsonStreamWriterTests.cs` [новый]
- `tests/nextorm.core.tests/InMemoryTests.cs`
- `tests/nextorm.sqlite.tests/JsonStreamingTests.cs`
- `src/nextorm.core/Query/QueryCommand.TResult.cs` [XML docs]
- `src/nextorm.core/Builders/EntityBuilderExtensions.cs` [XML docs]
- `docs/guide/28-streaming-data.md`, `docs/ru/guide/28-streaming-data.md`
- `docs/advanced/api-reference.md`, `docs/ru/advanced/api-reference.md`
- `docs/specs/roadmap/todo_json_streaming.md` (Q5)
- `docs/specs/status/rc2-179-json-error-policy-1.md` [этот файл]
- `docs/specs/status/rc2-180-json-stream-provider-conversion-1.md` [cross-ref: superseded only «Q5 policy deferred», не сама D180]
Не трогать: `docs/guide/14-json.md:40-41`, `docs/guide/26-large-objects.md:11` + RU. Executable production delta ожидается 0; если DO выявит необходимость runtime fix — replan.

## Predecessors / assumptions / prerequisites
- #176/#177/#178/#180 — plans ready, НЕ DO done (`rc2-176-…:12`, `rc2-177-…:8`, `rc2-178-…:12`, `rc2-180-…:11`). #180 `:24/:48/:61/:77` явно откладывает runtime partial-output/recovery policy на #179.
- Collection table `docs/specs/status/collection-1.0.9-rc2.md:40-44` устарела (D176–D180 показаны pending при ready-статусах) — синхронизировать; **но этот PLAN-шаг сам collection status не правит**.
- **ALL-barrier не пройден** (`collection-1.0.9-rc2.md:9`): индивидуальный DO не стартует до барьера.
- Пересечение с #177 по `28-streaming-data.md`/roadmap/тестам — сериализовать.
- Base `1.0.9-rc2 @ 18659e41`; CRLF; .NET 10; TreatWarningsAsErrors; чужой diff не перезаписывать.

## Test strategy
- Unit seam: новый `tests/nextorm.core.tests/JsonStreamWriterTests.cs` — isolated `IDataRecord` fake + управляемый `JsonRowWriter` delegate; ctor `JsonStreamWriter(Stream, JsonRowWriter, JsonStreamOptions)` (`JsonStreamWriter.cs:28`), IVT `nextorm.core.csproj:54,57`.
- SQLite: обе публичные поверхности, async cancellation, sink faults (`JsonStreamingTests.cs:234-250,199-231`).
- Core: in-memory fail-closed (`InMemoryTests.cs:128,142`).
- Integration: успешная регрессия SQL-провайдеров; перед запуском load `running-integration-tests`; skipped providers ≠ pass.
- Inner loop: `rebuild: affected` — `dotnet build tests/nextorm.core.tests -c Debug`; `dotnet build tests/nextorm.sqlite.tests -c Debug`; `dotnet test … --no-build` с `--filter "FullyQualifiedName~InMemoryTests"` / `"FullyQualifiedName~JsonStreamingTests"`.
- Boundary: `dotnet build -c Debug`; full `dotnet test` по core/sqlite/clickhouse `--no-build`; integration с `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`.
- Coverage 85/75 (line/branch) для core/sqlite/postgres/sqlserver; collect/report — CI-команды `dotnet.yml:44-59`; hard-fail порогов только на `main`, иначе warning.
- Branch delta: 0 production branches (docs/tests-only); mutation threshold не вводится.

## Perf-measurement decision
Консервативно обязателен acceptance gate: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` (baseline до implementation + after). JSON не входит в 7 кейсов (`docs/specs/performance/acceptance-benchmarks.md:37-45`) → это НЕ JSON performance evidence. Отдельный JSON-замер не нужен: row path неизменён (`JsonStreamWriter.cs:47-86`, `QueryExecutor.cs:1165-1204`). Если DO потребует runtime fix — рассмотреть JSON baseline/after в replan.

## Reconnaissance decision
Разведка закрыта (constructor/IVT, options, fault helpers подтверждены). Дополнительный spike не нужен.

## Unit execution mode
Один shared tree, последовательное исполнение пересекающихся footprints; worktree не нужен.

## Docs plan
EN+RU `28-streaming-data.md` (partial-output/error явно), `api-reference.md` (+RU), XML docs обоих public surfaces, Q5 закрыть решением, cross-ref #180. Public docs не ссылаются на specs. Крупный блок не выделяется (существующий гайд 28).

## Evidence contract rv1 (planned)
E179-01/R01–04 V01–V12,V16,V18 (core unit + SQLite filtered); E179-02/R05 V13–V15 (byte-prefix unchanged); E179-03/R06 V17,V20 (core/sqlite/clickhouse); E179-04/R06 V19 integration (providers executed, skips ≠ pass); E179-05/R07 EN/RU/XML/Q5/#180 diff (CHECK); E179-06 build `0/0`; E179-07 coverage commands `dotnet.yml:44-59` (PASS or WARNING-only-off-main; missing report = fail); E179-08 acceptance baseline/after; E179-09 scope/base/CRLF/executable-delta=0 (CHECK). Все P1; CHECK re-gather ≤2 раунда, owner check; отсутствующий отчёт = не доказано, не N/A.

## Revised plan r2 (rv2) — D177 native route
1. **Rationale.** D177 added a second execution path (`JsonNativeStream`, native branches in `QueryExecutor.cs:1167-1171,1219-1223,1254-1280`) that bypasses the managed `JsonStreamWriter` seam; the plan is rebased onto `2ac20818`. R179-01..07 are unchanged; the production executable delta remains **0** unless a reproducible R179-01..06 violation is found (then D179-05 routes back to PLAN).
2. **Re-anchored production/execution footprint.** `JsonStreamWriter.cs:12`; `JsonRowWriterFactory.cs:17`; `JsonNativeStream.cs:20,39,131,185`; `JsonShapePlan` (namespace `NextORM.Core`) `JsonShapePlan.cs:131,149,152,155,162,177`; `QueryExecutor.cs:1167-1171,1186-1197,1219-1223,1235-1246,1254-1280`; `PooledStreamBufferWriter.cs:119-129,166`; public surfaces `QueryCommand.TResult.cs:145,155,172,185,208`, `EntityBuilderExtensions.cs:158,172,187,203,224,249`.
3. **Matrix N (native).** `native × sync/async × array/NdJson × pre-output-refusal/mid-stream-failure` = 8 candidate cells; classify each as **test** (genuinely eligible) / **guard** (`IsEligible == false`, assert fallback) / **deferred+trigger**. Evidence: `JsonNativeStream.IsEligible` (`JsonNativeStream.cs:39-99`) admits only non-scalar, flat (`Shape is null`), `Array` mode, no Root/indent/naming-policy, ≥1 column, no DefaultOnNull, simple ASCII alias, non-enum, direct pass-through, `ProviderType == ValueType`, kinds String/Bool/short/int/long; native is SQL Server-only (`ISqlDialect.SupportsForJson`, `SqlServerDialect.cs:304`, base false `SqlDialectBase.cs:62`), decided before execution (no retry).
4. **New rows (all rv1 row IDs preserved).**
   - `E179-N01 | R179-01..06 | rv2: native matrix + native success/rollover; sources = native unit tests + public SQLite tests; execute inner loop; exit 0, selected_count≥1, explicit route/guard + scenario results; owner D179-02; applicability: native eligibility observably selected, else guard evidence.`
   - `E179-N02 | R179-01..06 | rv2: managed/native regression + provider boundary; sources = boundary + integration; exit 0, selected_count≥1, no failed/omitted required providers; owner D179-04; applicability always, native cells classified by demonstrated eligibility.`
   - R179-07 evidence extends to route-independent policy + synchronized EN/RU/XML/API wording + Q5 closure (#180 carve-out preserved; cross-ref `docs/specs/status/rc2-180-json-stream-provider-conversion-1.md`).
5. **Route observability (for tests).** No runtime route hook; native eligibility via internal `JsonNativeStream.IsEligible` (IVT) in core.tests; native runtime proof via output bytes (integration, SQL Server `FOR JSON` raw vs STJ escaped) and SQL text (`IQueryInterceptor`, `JsonStreamingTests.cs:326-337`; managed no `for json` at `:1880-1882`).

## Progress log
- 2026-10-09T00:00Z | PLAN | r2 | n=1/3 | rebased onto 2ac20818 (D177 committed); rv2 section appended; rv1 criteria/matrix/evidence preserved | docs/specs/status/rc2-179-json-error-policy-1.md
- 2026-10-09T00:00Z | DO | r2 | n=1/3 | DO started (tests + docs) | docs/specs/status/rc2-179-json-error-policy-1.md
- PLAN started (scout gather: JsonStreamWriter/entry points/error sources/siblings/roadmap/tests/registers/docs/providers/toolchain).
- PLAN gap raised by planner (coverage commands, test seams, predecessors) → targeted scout closed.
- PLAN sealed: plan_state=ready, r=1, rv=1, n=1/3. No DO started. No code/branches/worktrees/commits. Collection status file untouched.
- 2026-10-09T07:07Z | DO | r2 | n=1/3 | boundary evidence re-gather: the earlier exploratory unfiltered per-project runs (core 1888/1888, sqlite 1287 pas + 1 skip, clickhouse 601/601, exit 0; logs /tmp/d179_boundary_{core,sqlite,clickhouse}.log) were superseded by a single comprehensive solution-level boundary sweep per validate_inner_loop.py single-sweep cap; their results are retained as provenance only | /tmp/d179_boundary_solution.log
- 2026-10-09T07:20Z | DO | r2 | n=2/3 | CHECK FAIL (evidence-completeness) loop-back → DO r=2 n=2/3; tests-only (production executable delta 0). 5 fixes: (1) public-surface prefix retention — new `ThrowingAfterBytesStream` + sync/async/NdJson prefix tests in `JsonStreamingTests.cs` (exact prefix retained, no `]`/recovery tail, no Flush, `CanWrite`); (2) async temp-table refusal twin `TempTableQueryAsync_ShouldThrow` (`[unsupported-execution-form]`, destination unchanged); (3) async NdJson success parity `NdJson_Async_ShouldProduceIdenticalBytes`; (4) vacuous `ObserveCancellation` renamed `Pump_Async_ShouldMatchSync` + honest mid-pump cancellation witness `WriteDocumentAsync_CancellationMidPump_…` in `JsonNativeStreamTests.cs`; (5) native `Read()`-fault tests using the previously-unused `throwOnReadCall` hook (sync+async). Evidence: core build 0, sqlite build 0; inner JsonStreamWriterTests 7/7, JsonNativeStreamTests 51/51, InMemoryTests 162/162, JsonStreamingTests 135/135 (all exit 0, 0 skipped); boundary `dotnet test nextorm.slnx --no-build` 9656 total / 0 failed / 6837 passed / 2819 skipped exit 0; `validate_inner_loop.py report` exit 0 | /tmp/d179_r2_{build_core,build_sqlite,boundary_build,inner_writer,inner_native,inner_inmem,inner_sqlite,boundary_solution}.log; /tmp/d179_evidence.json
- 2026-10-09T07:26Z | CHECK | r2 | n=2/3 | CHECK PASS: all evidence rows P1 satisfied on the green r2 tree; R179-01..07 met; integration all 5 providers; coverage 86.8/79.3 | docs/specs/status/rc2-179-json-error-policy-1.md
- 2026-10-09T07:30Z | ACT | r2 | n=2/3 | finalized: status=ACT complete; CHECK PASS; issue #179 close attempted; D179 row → done; collection next-allowed-step → D185 | docs/specs/status/collection-1.0.9-rc2.md

## Done / Verified
- Outcome: **done** — JSON streaming fail-stop/partial-output error policy documented and verified; no production executable delta (tests+docs only).
- Acceptance criteria met: R179-01..07 ✔ (fail-stop, no rollback/recovery-tail, destination ownership, pre-output refusal leaves destination unchanged, sync/async parity/rollover/defaults, EN/RU/XML/API-reference/Q5 synchronized, #180 carve-out preserved).
- DO r2 n=2/3 fixes (CHECK loop-back): (1) public-surface prefix-retention tests (`ThrowingAfterBytesStream`, sync/async/NdJson); (2) async temp-table refusal twin; (3) async NdJson success parity; (4) native mid-pump cancellation witness + renamed vacuous `Pump_Async_ShouldMatchSync`; (5) native `Read()`-fault tests via `throwOnReadCall`.
- Test evidence (exit 0): JsonStreamWriterTests 7/7, JsonNativeStreamTests 51/51, InMemoryTests 162/162, JsonStreamingTests 135/135; boundary 9656 total / 0 failed / 6837 passed / 2819 skipped; integration all 5 providers (3551/3354/0, 197 capability skips); coverage 86.8% line / 79.3% branch.
- Evidence paths: `/tmp/d179_r2_{build_core,build_sqlite,boundary_build,inner_writer,inner_native,inner_inmem,inner_sqlite,boundary_solution}.log`, `/tmp/d179_evidence.json`; this status file.
- Commit: D179 task files committed on branch `1.0.9-rc2` with message `#179 D179 JSON streaming error policy (tests+docs, CHECK PASS r2 n2/3)`.
