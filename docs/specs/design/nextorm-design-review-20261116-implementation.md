# Реализация рекомендаций D1–D3 — 16.11.2026

Основание: пользователь явно одобрил реализацию D1–D3 из `nextorm-design-review-20261116.md`, включая продуктовый код и unit regression tests. Это отдельный этап после read-only аудита и восстановления документа; прежние запреты на код/тесты описывают только исторические этапы.

## Ограничения

- Три последовательных шага, одна ось за шаг; build/test после каждого, при падении — остановка следующего шага до разбора.
- Без public API breaks, новых пакетов, unrelated cleanup, benchmark, DocFX, commits/push/merge.
- C# symbol discovery/refactoring — Roslyn-first, редактирование patch, CRLF; assertions FluentAssertions.
- Прогресс, результаты команд и финальные выводы — здесь и в исходном отчёте, не только во временных файлах.
- SDK `global.json`: 11.0.100 (`latestFeature`); `Directory.Build.props`: libraries net10.0/net11.0, tests net11.0, Nullable enabled, LangVersion latest, TreatWarningsAsErrors.
- До реализации загружены using-dotnet/dotnet-advisor/dotnet-csharp и baseline references (coding-standards, async-patterns, solid-principles, code-smells, dotnet-releases); dotnet-tooling/version-detection; dotnet-testing/testing-strategy/xunit. Правила nextorm приоритетнее generic advice.

## Исходное дерево / сохранность чужой работы

Прочитаны актуальный отчёт, git status и diffstat. До изменения кода уже изменены: guide 35 EN/RU, nativeaot EN/RU, D370 ledger, JoinIntoManyToManySpec, RawMapperFactory, PublicAPI.Unshipped, ShardDataContextBuilderExtensions, ShardRoutingTests, AOT runner/scenarios/sqlprobe/integration tests и untracked Writes.cs. Эти файлы не входят в запланированные правки; чужие изменения не откатываются и не нормализуются. Исходный audit-report уже существует, его историческая часть сохраняется.

## Постоянный checklist

- [x] Прочитать отчёт, status/diffstat, SDK/TFM и применимые skills.
- [x] Прочитать scoped diff существующих правок (JoinIntoManyToManySpec, RawMapperFactory, ShardDataContextBuilderExtensions, ShardRoutingTests, PublicAPI.Unshipped); они оставляются без изменений.
- [x] Завершить scoped discovery актуальных D1–D3/test seams через Roslyn + source reads.
- [x] D1: проекция кэшируется на текущей позиции без eager mapping не-DISTINCT; для DISTINCT используется один projection-result при фильтре и выдаче. Sync/async, undefined Current до/после перечисления, Reset/Init и error/cancellation boundaries сохранены.
- [x] D1 tests: selector counts, repeated generic/non-generic Current, DISTINCT/rejected duplicates, lazy non-DISTINCT, sync/async adapter, rebind/no-op Reset/unsupported Reset, end/empty/cancel/selector failure; genuinely suspending async-only source.
- [x] D1 scoped gate: affected core build 0/0 + full core unit project 4310/4310, skipped0; solution gate отдельно заблокирован, см. диагностику ниже.
- [x] D2: минимальный общий internal execution/grouping источник для двух ShardQuery-фасадов; Task.Run/Parallel.ForEach, gate, clamp, order, cancellation/exception semantics неизменны; diff просмотрен ведущим.
- [x] D2 tests: обе формы, vector order, same-shard sequential, cross-shard bound, cancellation/exception behavior без тайминговых assert/Thread.Sleep; добавлены genuinely suspended async sequencing/MaxDegree tests.
- [x] D2 scoped gate: core build 0/0, focused 17/17, full core 4328/4328 skipped0; solution blocker отдельно сохраняется.
- [x] D3: одна internal whitelist для translator и ClickHouse dialect; исходные сообщения/принимаемые значения остаются на своих границах. Существующий friend assembly seam, не новый public API.
- [x] D3 tests: оба entry paths, все восемь units/casing, invalid/subsecond/empty/whitespace/null, literal-only requirement; baseline до extraction и post-fix focused зелёные.
- [x] D3 scoped gate: core/clickhouse builds 0/0 + full core 4328/4328 и clickhouse 1538/1538, skipped0.
- [x] Финальный review собственного diff/новых файлов, Roslyn symbols/refs, CRLF 11 C# файлов, отсутствие unintended public API/project/package changes; основной отчёт обновлён.
- [ ] Общий gate `dotnet build nextorm.slnx -c Debug` 0 warnings / 0 errors — **BLOCKED** внешним benchmark/example compile-reference конфликтом; последний завершившийся запуск exit1/warnings0/errors202. Не отменён и не представлен как pass.

## События и результаты

- PLAN: первоначальная semantic discovery Roslyn вернула InMemoryEnumerator/ShardQuery; два параллельных запроса завершились `daemon closed the connection`. Это не повод symbol-grep; точечные запросы повторяются при работе над соответствующим шагом.
- ENV: `dotnet --version` → `11.0.100`; src/Directory.Build.props задаёт net10.0/net11.0, tests/Directory.Build.props — net11.0. Существующие InternalsVisibleTo core→ClickHouse/core-tests/clickhouse-tests подтверждены; менять friend surface не требуется.
- DO D1 передан scoped coder: только два enumerator-файла и новые core unit tests; код остальных рекомендаций пока не меняется. D2/D3 scout собирает только текущие test/accessibility seams. Build/test результаты записываются после завершения соответствующего шага.
- SCOUT D2/D3 (`ses_e1d4f4a8effetagHey4jCRgZa1`): обе fan-out реализации по-прежнему используют Parallel.ForEach / Task.Run+SemaphoreSlim. Существующий ShardParallelismTests покрывает лишь sync Count и использует SQLite files + Thread.Sleep; новые scheduling tests должны быть детерминированными unit tests без добавления такого шаблона. ShardFanOutTests содержит доступную in-process surface-конфигурацию. Папка tests/nextorm.core.tests/Sharding уже содержит 10 файлов — новый отдельный тест помещать в когерентную подпапку либо расширять подходящий чистый файл, не трогая dirty ShardRoutingTests.
- SCOUT D3: существующий `T286bF04DateTimeFunctionSqlGenerationTests.StartOfInterval_ShouldInlineTheUnitAndRejectANonLiteralOrSubSecondUnit` (`:244–284`) проверяет minute/week, unknown/subsecond/nonliteral и прямой ClickHouseScalars.Render. Матрицу расширить до восьми units/casing и invalid values в обеих точках; сообщения ошибок оставить на своих границах.
- DESIGN D2/D3: допустимы минимальные internal static helpers с двумя конкретными существующими потребителями; без нового public API/интерфейса, без замены scheduler либо Dictionary на FrozenDictionary «заодно». Не добавлять отдельный heap-объект исполнения для каждого projected facade только ради DRY.

### D1 — промежуточная проверка

- Изменены только `InMemoryEnumerator.cs`, `InMemoryEnumeratorAdapter.cs` и новый `tests/nextorm.core.tests/InMemory/Enumeration/InMemoryEnumeratorProjectionCacheTests.cs`.
- Cache хранит успешную проекцию текущей позиции; identity fast path остаётся прямым, non-DISTINCT selector остаётся lazy, Reset no-op/NotSupportedException сохранены. Ошибка selector не кэшируется. Публичные сигнатуры не менялись.
- Red regression: `dotnet test tests/nextorm.core.tests -c Debug --filter 'FullyQualifiedName~InMemoryEnumeratorProjectionCacheTests'` → exit 2, 15 cases, 12 failed / 3 passed до исправления (`D1-red-focused-test.log`).
- Green focused: `dotnet test tests/nextorm.core.tests -c Debug --no-build --filter 'FullyQualifiedName~InMemoryEnumeratorProjectionCacheTests'` → exit 0, сначала 15/15, затем 16/16 (`D1-green-focused-test.log`, `D1-green-focused-final.log`).
- `dotnet build src/nextorm.core/nextorm.core.csproj -c Debug` → exit 0, 0 warnings / 0 errors (`D1-core-build.log`, `D1-core-build-final.log`, `D1-core-build-final2.log`).
- `dotnet test tests/nextorm.core.tests -c Debug`: первый запуск завершился timeout/exit143 (`D1-core-tests.log`), повторные полные запуски exit0: 4308/4308, 4308/4308, 4309/4309, skipped0 (`D1-core-tests-retry.log`, `D1-core-tests-final.log`, `D1-core-tests-final2.log`). Timeout не представлен как pass.
- `dotnet build nextorm.slnx -c Debug` → exit1, warnings0/errors202: benchmark generated row-materializer code не находит IRowMaterializerProvider/RowMaterializerColumn (`D1-solution-build.log`, первая диагностика :47–48). Не benchmark execution. Причина пока не установлена; не объявлять окончательный solution gate зелёным.
- Логи находятся в `docs/specs/design/nextorm-design-review-20261116-evidence/` (некоторые расширения могут быть gitignored, но не хранятся только во временном каталоге).
- REVIEW ведущего: существующие async tests используют синхронно завершающийся DualEnumerator. Запрошена дополнительная проверка genuinely suspending async-only Adapter source, без Thread.Sleep/Task.Delay, и scoped диагностика solution build failure без изменения чужого generator/benchmark/example кода. До ответа D2/D3 product edits не начаты.

### D1 — завершение и установленный внешний build blocker

- Добавлен async-only iterator с Task.Yield, проверкой sync rejection до первого чтения, DISTINCT duplicate skipping и повторной идентичностью Current. Ведущий прочитал тест (`InMemoryEnumeratorProjectionCacheTests.cs:142–200`).
- Первая компиляция дополнительного теста: exit1/xUnit1051, исправлена передачей `TestContext.Current.CancellationToken`, без подавления анализатора (`D1-async-focused.log`).
- `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~InMemoryEnumeratorProjectionCacheTests` → exit0, **17/17**, failed0/skipped0 (`D1-async-focused-retry.log:1,19–28`).
- `dotnet test tests/nextorm.core.tests -c Debug` → exit0, **4310/4310**, failed0/skipped0 (`D1-async-core-full.log:1,27–36`). Это последний полный D1-run.
- Диагностика solution failure: [D1-build-diagnosis.md](nextorm-design-review-20261116-evidence/D1-build-diagnosis.md). `dotnet build benchmarks/nextorm.benchmark/nextorm.benchmark.csproj -c Debug --no-restore` воспроизводит exit1/warnings0/errors202, это только compilation, не BDN-run. `dotnet msbuild benchmarks/nextorm.benchmark/nextorm.benchmark.csproj -property:Configuration=Debug -target:ResolveReferences -getItem:ReferencePath` → exit0; compiler берёт `nextorm.core.dll` из NuGet nextorm/1.0.9, а не workspace assembly. Transitive источник — PostgreSQL example. Roslyn находит недостающие mapping-типы в workspace; чужие project files при этом не изменены.
- Решение по scope: не исправлять benchmark/example dependency graph в D1–D3 и не удалять artifacts как «лечение». Продолжить согласованные D2/D3 после зелёных scoped build/unit gates; **обязательный общий solution gate не выполнен и не отменён**, его blocker остаётся явным ограничением итоговой проверки. Финал не будет заявлять зелёный whole-solution build.
- DO D2 запущен (`ses_e1d1bdf74ffeDn56SETgwAmDtT`): только ShardQuery/shared internal helper и новые deterministic unit tests в Sharding/FanOut. D3 code edits ещё не начаты.

### D2 — реализация и первая полная проверка

- `ShardQueryExecution.cs` (90 строк, internal static) содержит общий Scoped/RunEach/RunEachAsync/DegreeOfParallelism/BucketsByShard. Два потребителя — `ShardQuery<TEntity>` и `ShardQuery<TEntity,TResult>`; их private-обёртки передают source/callback напрямую, streaming использует тот же Scoped. Нового per-query execution object, интерфейса, public API и scheduler-изменений нет.
- Ведущий просмотрел полный diff ShardQuery и helper: сохранены Ordinal owner grouping, исходные позиции, Math.Clamp, Parallel.ForEach, Task.Run с CT на каждую группу, WaitAsync вне try, Release после acquisition, последовательный foreach внутри группы и WhenAll.
- Новые unit tests `tests/nextorm.core.tests/Sharding/FanOut/ShardQueryExecutionTests.cs` используют in-memory builder variant и controlled completion sources, без БД/Thread.Sleep/Task.Delay. Покрыты vector order, sync maxdegree/same-owner/Ordinal, clamp, empty, async token/queued cancellation, sync AggregateException vs async original failure, gate release/waitother и обе projected/unprojected sync/async/streaming фасадные формы.
- Подробные команды/первичные ошибки: [D2-result.md](nextorm-design-review-20261116-evidence/D2-result.md). Первое focused build остановилось на восьми xUnit1051; исправлены test-token arguments без suppression.
- `dotnet build src/nextorm.core/nextorm.core.csproj -c Debug` → exit0, warnings0/errors0 (`D2-core-build-final.log`).
- `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~ShardQueryExecutionTests` → exit0, **15/15**, failed0/skipped0 (`D2-focused-tests-final.log`).
- `dotnet test tests/nextorm.core.tests -c Debug` → exit0, **4326/4326**, failed0/skipped0 (`D2-core-tests-final.log`). Числа относятся к наблюдаемому рабочему дереву, не арифметическому выводу из прежнего snapshot.
- REVIEW: async order test имел синхронно завершающийся callback. Запрошены отдельные suspended async same-owner/overlap и MaxDegree=2 случаи; source helper менять не требуется. Их проверка завершается до D3. Общий solution blocker не устранён и не объявлен pass.

### D2 — завершение async-матрицы

- Добавлен `tests/nextorm.core.tests/Sharding/FanOut/ShardQueryAsyncExecutionTests.cs` (два случая). Ведущий прочитал весь файл: controlled pending tasks гарантируют suspended callbacks, same-owner порядок, cross-owner overlap и actual MaxDegree=2 при большем числе групп; release signals выставляются в finally. Product code в этом follow-up не менялся.
- Focused command и результат: `dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~NextORM.Core.Tests.Sharding.FanOut` → exit0, **17/17**, failed0/skipped0 (`D2-async-focused.log`; точный argv также в `D2-result.md:57`).
- `dotnet test tests/nextorm.core.tests -c Debug` → exit0, **4328/4328**, failed0/skipped0 (`D2-async-core-tests.log`). Это последняя полная проверка шага D2.
- DO D3 запущен (`ses_e1ced013fffeTG1mLf9z1wIDfu`): только две whitelist consumers, один internal источник и ClickHouse SQL-generation tests. Запрошены оба affected builds, полный clickhouse/core unit run и финальный канонический solution build; без исправления постороннего dependency graph.

### D3 — результат и финальная проверка

- Новый internal static `ClickHouseIntervalUnits` содержит одну приватную readonly Dictionary с исходными восемью единицами и OrdinalIgnoreCase; наружу по internal seam отдаётся только `TryGetKeyword`, без mutable collection/public surface.
- Потребители — core `ClickHouseNativeFunctionTranslator` и provider `ClickHouseScalarFunctions` (в тестах alias поля `ClickHouseScalars`). Локальные строки ошибок и проверки границ сохранены; friend configs и project/package files не менялись.
- Ведущий прочитал helper, оба consumer diff и расширенные tests. Финальный Roslyn members/refs подтверждает helper `ClickHouseIntervalUnits.cs:20`, source callers `ClickHouseNativeFunctionTranslator.cs:973`, `ClickHouseDialect.cs:2613`; дубликаты TFM в refs не считаются новыми потребителями.
- Расширенная matrix проверяет восемь units с lower/upper/mixed casing, оба entry paths и 3/4-argument формы; invalid sub-second/unknown/empty/whitespace и captured/null границы. Для translator построен ConstantExpression из template, а captured-valid-unit отдельно остаётся rejected. Точный старый текст ошибки проверяется для каждой границы; direct-render null по-прежнему ArgumentNullException с ParamName key.
- Команды и результаты: [D3-result.md](nextorm-design-review-20261116-evidence/D3-result.md). Pre-extraction baseline `dotnet test tests/nextorm.clickhouse.tests -c Debug --filter FullyQualifiedName~T286bF04DateTimeFunctionSqlGenerationTests` → exit0, 15/15. Та же post-extraction команда → exit0, 15/15. Это общее число тестов расширенного класса, не число новых cases.
- `dotnet build src/nextorm.core/nextorm.core.csproj -c Debug` → exit0, **0/0**, net10.0/net11.0 (`D3-core-build.log:5–14`).
- `dotnet build src/nextorm.clickhouse/nextorm.clickhouse.csproj -c Debug` → exit0, **0/0**, net10.0/net11.0 (`D3-clickhouse-build.log:6–15`).
- `dotnet test tests/nextorm.clickhouse.tests -c Debug` → exit0, **1538/1538**, failed0/skipped0 (`D3-clickhouse-tests.log:2–11`).
- `dotnet test tests/nextorm.core.tests -c Debug` → exit0, **4328/4328**, failed0/skipped0 (`D3-core-tests.log:26–35`).
- `dotnet build nextorm.slnx -c Debug`: первая попытка прервана server restart, без emitted exit status (`D3-final-solution-build-canceled.log`); повтор завершился **exit1, warnings0/errors202** (`D3-final-solution-build.log:455–456`) с тем же benchmark package/source-generator mismatch. Причина не «исправлялась» чисткой, suppression или снятием проекта из solution.

## Итог и оставшиеся ограничения

- **D1–D3 implementation scope выполнен и проверен. Общий solution gate остаётся BLOCKED.** Новый public API, package dependencies и несовместимые изменения сигнатур не введены. Исторический отчёт сохранён с отдельной текущей disposition D1–D3.
- Изменения ограничены семью product C# файлами (два новых internal helpers), четырьмя test C# файлами (три новых) и документами/evidence. Полный перечень — в основном отчёте.
- Финальная metadata-проверка line endings: все **11 C# файлов** имеют CRLF, bare LF0. `git diff --check --` по собственным tracked product/test изменениям → exit0. Собственные новые helpers/tests и документы просмотрены; чужие файлы (в том числе новый параллельный dirty JoinIntoStitcher) не редактировались.
- Gate evidence относится к наблюдаемому рабочему дереву, не к изолированному worktree/замороженному baseline. Чужие параллельные коммиты/правки отмечались, но не откатывались. Не заявляется побайтное тождество всего дерева между всеми запусками.
- Benchmark execution/профилирование/DocFX/live integration не запускались. Counter tests подтверждают число selector calls и scheduling contracts, не численный speedup либо allocation delta.
- Чтобы закрыть глобальный build gate, требуется отдельное согласование минимального исправления benchmark/example dependency graph; затем повтор canonical build. Это за пределами разрешённых D1–D3 и не скрыто под формулировкой «все проверки зелёные».
- Коммитов/push/merge со стороны этой реализации нет. Важные итоги сохранены в docs, не только в игнорируемых raw logs.
