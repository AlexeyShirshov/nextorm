# T247 — JSON streaming performance: PLAN rv2

| Поле | Значение |
|---|---|
| Issue | [#247](https://github.com/AlexeyShirshov/nextorm/issues/247), OPEN |
| Milestone / collection | `1.0.9-rc3` / `1.0.9-rc3-open-3` |
| Branch / исходный HEAD | `1.0.9-rc3` / `ed1ffff5` |
| Фаза | PLAN-only; **STOP на PLAN→DO** |
| Plan state | **ready-for-DO-boundary** |
| Счётчики | `N=1`, `r=1`, `n=1`, без сброса |
| Evidence contract | `rv=2`, supersedes `rv=1`; ID и обязательства сохраняются |
| Status | `docs/specs/status/D247-json-streaming-2.md` |
| Предыдущий status | `docs/specs/status/D247-json-streaming-1.md` |
| Evidence root | `.pdca/T247/rv2/` |
| Manifest | `.pdca/T247/rv2/manifest.json` |

**Основание status-пути:** явно заданный dispatcher путь, действующая практика этой collection lane и ссылка её строки в `docs/specs/status/collection-1.0.9-rc3-open-3.md`. rv1 — текущий in-flight status этой задачи, не архив другого завершённого цикла. Новый status сохраняется по указанному пути; evidence остаётся в skill workspace `.pdca/`.

Это завершение прежнего PLAN-ответа с полученными уточнениями, **не новая ревизия `r` и не новый DO attempt**. `rv=2` supersedes evidence contract rv1; предыдущие тексты сохраняются как историческое evidence. Ничего не выполнено; прежний `incomplete` не означает `done`.

## Цель и непреложные ограничения

Сделать managed JSON streaming конкурентным на 1k/10k строках, сохранить преимущество на 100k и существенно уменьшить фиксированную стоимость повторного небольшого экспорта.

Сохраняются:

- все R247-01…11 без ослабления;
- публичный API, JSON-байты и установленная семантика ошибок/отмены;
- состояние общего `QueryCommand` и query plan cache;
- per-reader validation, включая cache hit;
- native JSON eligibility и managed fallback;
- context-owned bounded cache: максимум **64 записи**, LRU;
- только immutable descriptors/delegates в cache; никаких reader, destination, command или execution state;
- FluentAssertions, CRLF, CPM, warnings-as-errors, отсутствие новых suppressions/skips;
- #250 остаётся в `1.0.9-rc3`, но не становится объёмом T247;
- **никаких commits, push, merge или staging в этой фазе**;
- `docs/specs/status/collection-1.0.9-rc3-open-3.md` не редактируется: строку T247 обновляет collection primary;
- D244/D245 status, evidence, scripts и реализация warm-CTE/set-op не затрагиваются.

T247 меняет JSON execution path: `src/nextorm.core/Query/Json/**`, JSON-участки `DataContext/DataContext.cs` и `DataContext/DataContextCache.cs`, JSON unit/integration cases, fixed-cost harness и планируемый phase arm. **Семантического пересечения с warm-CTE/set-op областью нет.** Общие контейнерные файлы всё же требуют координации: при пересечении физических файлов записи сериализуются, без изменения чужих участков.

## Критерии приёмки

### Дословный перенос rv1

Coder механически вставляет полные строки:

| ID | Источник |
|---|---|
| R247-01…R247-11 | **carried verbatim from `D247-json-streaming-1.md` `:34-44` (post-pointer current tree: `:35-45`)** |

| ID | Required result and reproducible oracle | Negative case |
|---|---|---|
| R247-01 | In each of two final full-job rounds: 1k DTO streaming mean / same-run linq2db mean ≤1.2; 10k ratio ≤1.0 and allocation ≤446,095 B/op, conservatively converting 435.64 KiB; 100k ratio ≤the corresponding pre-change same-session ratio. | Missing/mismatched rows, slower ratios, or allocation overflow fail; historical absolute timings cannot establish a pass. |
| R247-02 | Add and report NextORM `ToList_Dto + JsonSerializer` at all three row counts; validate byte equality outside timing. Capture pre-change measurements after benchmark-only instrumentation, before production edits. | Materialize-only `ToList_Dto`, different output, or a post-change “baseline” is insufficient. |
| R247-03 | Existing JSON tests remain green; matrix below closes managed/native, shape, option, surface and boundary variants with byte-identical output. | Different naming, order, null/default handling, framing, whitespace or newline fails. |
| R247-04 | Cancellation and faults retain existing partial-output policy, including exact completed-row prefix; destination remains open and destination Flush count stays zero. | Lost completed-row prefix, extra bytes, swallowed cancellation, disposal or destination Flush fails. |
| R247-05 | Repeated calls preserve command/plan-cache state; cached delegates are safely shared, keys distinguish writer-relevant shapes/options, and storage remains bounded. | Cross-shape output, shared mutable writer state, stale bindings, unbounded entries or disabled plan caching fails. |
| R247-06 | No public API change; in-memory calls remain fail-closed; SQL Server native path and managed fallback remain correct. | Public signature delta, in-memory acceptance, or changed native selection/output fails. |
| R247-07 | Build exits 0 with zero warnings/errors; full affected projects and real-provider integration boundary sweep pass with no missing provider evidence. | Filtered-only evidence or skipped required providers is not a pass. |
| R247-08 | CRLF preserved; new suppression/skip counters zero; changed-branch coverage reviewed; project coverage ≥85% lines / ≥75% branches. | Silent warning suppression, uncovered changed behavior, mixed EOLs or sub-threshold coverage fails this cycle. |
| R247-09 | JSON 1k/10k/100k enter the perf gate; existing allocation rows retain their semantics; seven-case acceptance set remains seven, ≤240 s, with no case >120% of its paired baseline mean in either round. | Missing params/jobs, altered legacy normalization, unbudgeted JSON timing, or acceptance regression fails. |
| R247-10 | Report, EN/RU comparisons, follow-up registry and findings agree; distinguish current measurements from historical issue numbers. | One-language update, unsupported performance claim, or public link to internal specs fails. |
| R247-11 | Complete rv1 manifest passes both evidence validators; CHECK respects its finite re-gather budget. | Missing evidence cannot be relabeled N/A or used alone to justify another DO iteration. |

Пересказ не заменяет исходные строки. Сохраняются, в частности:

- 1k ratio ≤1.2, 10k ratio ≤1.0;
- 100k не хуже paired pre-change same-session;
- 10k allocation ≤446 095 B/op, прежняя семантика остальных allocation rows;
- ровно семь acceptance cases ≤240 s;
- ни один acceptance case не превышает 120% paired baseline mean в любом round.

Упоминание rv1 manifest внутри исходной R247-11 не переписывается; rv2 manifest проверяется дополнительно.

### Новые строки

Численные сравнения используют **новый pinned pre-change baseline**, а не приблизительные исторические числа issue.

| ID | Требуемый результат | Негативный случай | Проверка / artifact |
|---|---|---|---|
| **R247-12** | Для warmed повторных `EntityBuilder`, sync, 3-column `WriteJson`: в **каждом из двух full rounds** среднее на 100 строках ≤**0.50** paired baseline; fitted intercept по Rows=1,10,100 ≤**0.50** paired baseline intercept. Оба intercept положительны. Это дополнительный gate, не замена R247-01. | Любой round превышает границу; nonpositive intercept; отсутствует хотя бы одна точка; разные fixtures/fingerprints; discordant rounds → inconclusive. | `B`, `F`, `C`; существующий `SqliteBenchmarkJsonFixedCost.cs`, arm 3Col; `small-row.json`, paired BDN exports, `intercept-fit.json`. |
| **R247-13** | Provider-independent writer key: одинаковая семантическая shape/options переиспользует writer при разных runtime reader storage types и выдаёт правильные одинаковые JSON-байты; несовместимый reader отвергается существующей validation. | Provider ID/runtime storage type искусственно дробит key; cached writer фиксирует storage type первого reader; validation обходится. | `U`, `I`; unit result + provider inventory/result, `key-contract.json`. |
| **R247-14** | Naming policy, `IgnoreNull`, прочие writer-relevant options и разные shapes не сталкиваются; неподдерживаемые converter/policy metadata **bypass cache**, сохраняя корректность. | Неправильный hit, stale configuration, приблизительное равенство, публикация неподдерживаемого descriptor. | `U`, `I`; case inventory и `key-contract.json`. |
| **R247-15** | P1 streaming matrix закрыта на `QueryCommand<TResult>` и `EntityBuilder<T>`, sync/async: cancellation на async, params parity, ownership/flush, supported/unsupported forms, native/fallback и temp-table/TVP boundaries. Cached и uncached путь эквивалентны. | Отсутствующий вариант считается GAP; exception/skip вместо evidence не считается закрытием. | `U`, `I`; `variant-matrix.json`, test results и provider inventory. |
| **R247-16** | Compile-only spike отдельно измеряет setup-built-shape compilation, steady writer и public total; явно сообщает неатрибутированный остаток. Warm reusable entry компилируется один раз до eviction; unsupported metadata bypass допускает повторную compilation. | Fixed-cost fit выдан за измерение compilation; остаток назван измеренной prepare/clone/shape фазой; repeated compilation на обычном warm hit. | `P`, `U`; `phase-attribution.json`, BDN exports, cache lifecycle tests. |
| **R247-17** | Полный rv2 manifest сохраняет все E247-01…19 и проходит **оба исходных валидатора** плюс проверку новых строк. Все evidence привязаны к исходникам/командам/exit codes. | Missing report, нулевой matching, непроверенный inherited validator, изменённый denominator или неподтверждённая applicability. | `V1`, `V2`; `manifest.json`, оба validator logs и CHECK completeness report. |

**Intercept oracle:** обычная линейная регрессия `T(rows)=a+b×rows` по средним трёх точек 1/10/100 отдельно для baseline/final и каждого round. Публикуются исходные точки, `a`, `b`, residuals. Это **операционный small-row intercept**, не независимое измерение preparation. Плохое качество fit требует анализа; менять модель ради прохождения порога нельзя.

## Минимальное решение

**Что меняем:** устраняем повторную compilation managed row writer на reusable warm path.

**Что сохраняем:** command cloning, query preparation и shape construction остаются на прежнем execution path. Prepared query/terminal plan здесь не кешируется.

**Почему:** новые данные показывают ~1 690 µs фиксированной стоимости, ~232 µs на дополнительный столбец, ~480 µs scalar→object и ~100 µs numeric differential. Они усиливают compilation hypothesis, но **не разделяют clone/prepare/shape/compile** и не доказывают безопасность более широкого cache.

| Вариант | Цена / преимущество | Риск | Решение |
|---|---|---|---|
| JSON-local compiled-writer cache | Минимальный scope, прямое устранение repeated compile | Shape/prepare могут оставить большой residual | **Выбран** |
| Cache полного terminal plan | Может убрать больше intercept | Новые lifetime/invalidation dependencies без decomposition | Deferred: measured failure выбранного решения → PLAN |
| Общая JSON/CSV abstraction сейчас | Общая инфраструктура | Только один реализованный consumer; расширяет #247 | Отклонён |

Если cache не проходит R247-12 или прежние ratios, критерии **не снижаются**. CHECK возвращает измеренные данные в PLAN.

### Ownership и синхронизация

Интеграционные anchors: `DataContext.cs:385,418,427-428`, `DataContextCache.cs:150-156`.

- JSON-local cache, context-owned, максимум 64 published entries, bounded LRU.
- Lookup, cold compilation/publication и eviction синхронизированы.
- Минимальный вариант компилирует под cache lock, исключая duplicate builds resident entry.
- Delegates вызываются **вне lock**.
- Нового обещания общей thread-safety `DataContext` нет.
- После eviction повторная compilation допустима.
- Descriptor/options snapshot immutable; неподдерживаемый snapshot → bypass.
- `ValidateReaderBinding` выполняется для каждого reader, включая hit: `JsonRowWriterFactory.cs:161`, `QueryExecutor.cs:1183,1232`.
- Native path не создаёт managed cache entry.
- Shared command flags не мутируются. Preparation сохраняет call-local `storeInCache:false`.

### Точный rv2 cache key

Provider-independent **semantic structural key**:

1. Упорядоченные ordinal/name descriptors, включая результирующие JSON property names.
2. Declared CLR/value/enum types и converter-bound semantic type, если он влияет на writer.
3. Kind, recursive structure, nullable/default handling, direct-pass-through.
4. Все writer-relevant option values, включая `IgnoreNull`.
5. Naming-policy discriminator/configuration при корректном стабильном immutable representation.
6. Converter identity/configuration и остальные converter semantics при корректном immutable representation.

**Не входят:** provider/context implementation identity, runtime reader storage types, текущий reader, SQL text как самостоятельный discriminator и CSV `SequentialAccess`.

Converter-bound `ProviderType` — семантика converter-а, а не storage type конкретного reader. Его включение при необходимости не отменяет provider independence.

`JsonShapePlan`/`JsonStreamOptions` reference identity не используются как whole key. Неразрешимая mutable converter/policy configuration **bypasses cache**, а не приблизительно сравнивается.

Четыре обязательства:

- **Runtime storage dispatch:** reused delegate корректен для одинакового CLR property при разных reported storage types; validation остаётся per-reader.
- **Converter metadata:** неподдерживаемое representation даёт bypass, корректные байты и отсутствие публикации.
- **Naming/IgnoreNull:** relevant policy/configuration и `IgnoreNull` различают entries.
- **Non-collision:** разные shapes/options не получают неверный writer.

Другие provider-dependent captured decisions, если обнаружатся, требуют PLAN re-entry, а не скрытого расширения key.

### D247-01: required, narrowed

Compile-only spike **сохраняется**, fixed-cost fit его не заменяет:

- shape строится в setup;
- отдельно измеряются compilation, steady writer и public total;
- не требуется повторный широкий exploratory sweep только для доказательства наличия fixed cost.

Принимаемый residual: `total − measured compile` — **неатрибутированный остаток**, потенциально включающий clone/prepare/shape и различия measurement boundaries. Это не независимо измеренная preparation phase.

### Связь с #250

Выбор **(b): JSON-local cache сейчас; extraction при втором consumer в #250**.

Общий terminal-plan-cache type в T247 не вводится. Цена — возможный последующий extraction/refactor; риск — divergence. В T247 report и follow-up #250 фиксируются capacity/LRU/locking/lifetime и различия key.

CSV сохраняет resolved reader storage types + applied `SequentialAccess`; `ValueTransform` bypass. #250 не блокирует T247 и остаётся в `1.0.9-rc3`.

## Матрица вариантов и приоритетов

Все строки ниже **P1 по построению**. CHECK не может их понизить.

| Execution variant | Закрытие |
|---|---|
| EntityBuilder / QueryCommand × sync / async | Unit + real-DB integration; cached/uncached byte equality |
| Async cancellation до начала / во время чтения / записи | Tests: exact completed-row prefix и установленная partial-output policy |
| Reader/write fault, empty result | Tests: destination open, destination Flush count=0 |
| Scalar / object / nested supported shape | Unit byte comparisons; representative DB projections |
| Value/reference, nullable/default, enum | Unit matrix и применимые DB cases |
| Null/default inputs, unsupported forms | Tests существующей семантики + fail-closed guards |
| Default/nondefault naming, IgnoreNull, relevant options | Equality/non-collision tests |
| Representable / unsupported converter или policy metadata | Hit tests / bypass guard + tests |
| Same CLR property, differing numeric storage types | Controlled-reader unit tests + live providers |
| Cold / warm / concurrent lookup / eviction / >64 shapes | Lifecycle tests, resident single-build и boundedness |
| Independent contexts | Isolation test |
| Shared command, последующие Any/Count/All | State-preservation regression tests |
| Ordinary / temp-table / TVP preparation | Boundary tests на применимых реальных providers |
| Managed / SQL Server native / managed fallback | Real-DB integration |
| In-memory unsupported terminal | Unit fail-closed test; не integration |
| Params parity sibling terminals | Contract tests обеих поверхностей |
| Full terminal-plan cache / CSV implementation | Deferred: measured failure R247-12 / исполнение #250 |

Existing test symbols и call sites устанавливаются через Roslyn. План не выдумывает будущие test names.

## DO-задачи и зависимости

Все задачи активны. Blocker report и прежний `incomplete` не завершают D.

| Unit | Работа | Статус решения |
|---|---|---|
| **D247-00** | Сохранить rv2. Механически скопировать R247-01…11 из rv1 `:34-44` (post-pointer current tree: `:35-45`), E247-01…19 из `:182-200` (post-pointer current tree: `:183-201`), budget line `:204` (post-pointer current tree: `:205`), без переформулировки. Добавить rv2 command/revision bindings, новые rows и final pointer. Закрепить `V1`/`V2` ниже. | fix now; prerequisite |
| **D247-01** | Narrowed compile-only spike: `JsonRowWriterFactory.cs:90`, fixed-cost harness `:10-11`; benchmark-only instrumentation и pinned baseline | fix now; before production cache |
| **D247-02** | JSON-local cache/key: `DataContext.cs:385,428`, `DataContextCache.cs:150-156` | fix now; после baseline/spike |
| **D247-03** | Unit/key/lifecycle/surface matrix; `tests/nextorm.core.tests/Json/…` | fix now |
| **D247-04** | Real-provider boundaries, coverage capture, full-round final measurements | fix now |
| **D247-05** | Report, EN/RU comparison, findings, #250 и integration-baseline follow-up | fix now |
| **D247-06** | DO→CHECK manifest/receipts, оба validators, CHECK handoff | fix now; final verdict принадлежит CHECK |

**Режим:** последовательно, одно дерево. Cache/key/harness contracts и пересекающиеся файлы не оправдывают parallel implementation. Worktree не требуется.

С T244→T245 координируются shared-file writes и measurement/frozen-tree windows. Чужие изменения не включаются в T247 ownership. Receipts не смешивают разные frozen trees.

## Тест-стратегия и coverage

- Unit: JSON/key/bypass/controlled readers/state/ownership/lifecycle, FluentAssertions.
- Integration: реальный SQLite и все зарегистрированные provider variants; PostgreSQL, SQL Server, MySQL, ClickHouse не могут исчезнуть из evidence. MariaDB и остальные variants — по harness inventory.
- Перед integration загрузить `running-integration-tests`; при missing socket выполнить предусмотренный recovery.
- Native/fallback и temp-table/TVP проверяются на применимых providers; applicability доказывается capability/harness evidence.
- Focused selectors добавляются только после Roslyn discovery. Базовые команды запускают полные проекты.
- T247 coverage obligations: **changed-branch delta + no regression + основной gate ≥85% lines / ≥75% branches + passing integration boundary sweep без missing provider evidence**.

### Закрытое решение о красном integration baseline

Известный baseline **74.8% line / 64.8% branch** при integration gate **65/75**, с установленным `InMemory*` exclusion, — **предсуществующий факт, не дефект T247**.

- Пороги не снижаются.
- T247 не обещает сделать общий integration gate зелёным.
- Baseline evidence и новый результат показываются отдельно; красный gate не называется passing.
- Regression не маскируется изменением exclusions/denominator.
- Работы вне acceptance #247, необходимые для общего 75% integration branch coverage, оформляются отдельной единицей/issue в **том же `1.0.9-rc3`**.

Follow-up registry ID: **F247-INT-COV** — «Raise pre-existing integration branch baseline to 75%». В D247-05 сначала поиск существующего issue; затем ссылка на найденный либо новый issue с verified URL и milestone `1.0.9-rc3`. T247 не поглощает его реализацию.

## Измерения и команды

**Перф-замер нужен:** per-call/per-row path, `DataContext.cs:385-428`, `QueryExecutor.cs:1162,1214`.

**Разведка нужна:** D247-01 доказывает отдельную compile cost и безопасный warm reuse. Наблюдаемые результаты: phase artifacts, resident single-build, runtime storage dispatch. Fixed-cost fit сам этого не доказывает.

Driver `.pdca/scripts/perf/t247_json_gate.py` — **планируемый источник**, не уже полученное evidence. Внутреннего flock нет.

### Точные invocation aliases

```text
B:
flock -x /tmp/opencode/nextorm-bdn.lock python3 .pdca/scripts/perf/t247_json_gate.py baseline --rounds 2 --evidence-root .pdca/T247/rv2

P:
flock -x /tmp/opencode/nextorm-bdn.lock python3 .pdca/scripts/perf/t247_json_gate.py phase --rounds 2 --evidence-root .pdca/T247/rv2

F:
flock -x /tmp/opencode/nextorm-bdn.lock python3 .pdca/scripts/perf/t247_json_gate.py final --rounds 2 --evidence-root .pdca/T247/rv2

C:
python3 .pdca/scripts/perf/t247_json_gate.py compare --evidence-root .pdca/T247/rv2

U:
dotnet test tests/nextorm.core.tests -c Debug

I:
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug

BUILD:
dotnet build -c Debug

V1:
python3 .pdca/scripts/validate_inner_loop.py manifest .pdca/T247/rv2/manifest.json

V2:
python3 .pdca/scripts/pdca_evidence.py lint --repo . --manifest .pdca/T247/rv2/manifest.json
```

**Другого validation command не вводится.** `C` — performance comparison, не замена manifest/provenance validators.

### Measurement protocol

- Existing fixed-cost source: `benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkJsonFixedCost.cs`.
- Baseline снимается после **benchmark-only instrumentation**, до production changes.
- Фиксируются HEAD, patch, SDK/runtime, fixtures/configuration, source fingerprints.
- Same host/runtime/fixtures и согласованные fingerprints baseline/final; различия production implementation явно привязаны к paired baseline.
- `NEXTORM_BENCH_FULL=1`, full `Job.Default` с установленным `InProcessEmitToolchain`.
- ShortRun — только diagnostic; ~20% band не является tolerance.
- `logical_ops_per_invocation=1`.
- Public total включает обычную preparation, не setup-prepared surrogate.
- Warmup обеспечивает resident entry; baseline получает эквивалентный warmup.
- Fixed-cost 3Col: Rows=1/10/100/1000/10000. Existing arm устанавливается discovery, не предполагаемым новым symbol.
- Public JSON filters сохраняются из rv1: `*SqliteBenchmarkWriteJson*`.
- Phase filters rv1: `*SqliteBenchmarkJsonStreamPhase2*` / `*JsonStreamPhase2WriterBenchmark*`; обязательный nonzero discovery. Новый compile-only arm помечается planned до реализации/discovery.
- Два full rounds; literal thresholds применяются к каждому. Discordant rounds → inconclusive, не усреднённый pass.
- Empty/zero-match/missing case → failure.
- Acceptance set остаётся **ровно семь cases ≤240 s**; каждый ≤120% paired baseline mean в обоих rounds.
- Fixed-cost и phase sweeps отдельные, не восьмой acceptance case.
- R247-01 ratios и allocation semantics сохраняются; ToList+JsonSerializer controls и byte equality — по R247-02, вне timing.
- Единственный host lock — outer `flock -x /tmp/opencode/nextorm-bdn.lock` для `B/P/F`; nested lock запрещён.
- Dedicated BDN dirs, raw exports, inventory и mapping каждого результата на artifact.
- Driver записывает child argv/exit code и discovered/executed case counts.
- `B/P/F` exit 0 только при полном валидном наборе измерений. `C` exit 0 только при passing gates; inconclusive — nonzero с явной причиной.

Measurement и receipt capture проводятся в согласованном quiet window shared tree. Изменения другой lane не разрешают выдавать mixed-tree evidence за согласованный результат.

## Versioned evidence contract rv=2

Определение контракта: глобальный `pdca-dotnet`, разделы **Versioned evidence contract** и **CHECK completeness gate — mandatory evidence contract**. Overlay добавляет provenance gate `V2`, не ослабляя глобальные обязательства.

### Supersession и inherited rows

`rv2 supersedes rv1` как текущий контракт; исторические тексты/evidence сохраняются.

D247-00 копирует неизменными:

- R247-01…11 — rv1 `:34-44` (post-pointer current tree: `:35-45`);
- E247-01…19 — rv1 `:182-200` (post-pointer current tree: `:183-201`);
- CHECK re-gather budget — rv1 `:204` (post-pointer current tree: `:205`).

| Row / requirement | Required check; kinds and sources | Exact invocation; required result | Expected artifact; owner; applicability; rv |
|---|---|---|---|
| E247-01 / R247-02,05 | Baseline facts, WIP regression results, CLI help, repository URL, artifact/lease inventory | `python3 scripts/perf/t247_json_gate.py preflight --output artifacts/pdca/D247/rv1/preflight` →0, no unresolved prerequisites | `$A/preflight/**`; F+C; always; 1 |
| E247-02 / R247-01,02 | Two pre-production BDN rounds, raw exports, fingerprints, frozen budgets | Baseline driver command above →0, all 33 control-inclusive cases represented per round | `$A/baseline/**`; C; always; 1 |
| E247-03 / R247-02 | Phase distributions and compile-only evidence; benchmark source diff | `python3 scripts/perf/t247_json_gate.py phases --output artifacts/pdca/D247/rv1/phases` →0, no unsupported attribution | `$A/phases/**`; C; always; 1 |
| E247-04 / R247-03,04,05 | Core targeted tests, assertion-to-matrix mapping, binding/parallel/bound tests | Core filtered command above →0; TRX/output copied by helper | `$A/tests/core-focused/**`; C; always; 1 |
| E247-05 / R247-03,04,05 | SQLite targeted tests, all overloads/buckets/exact prefixes | SQLite filtered command above →0; matrix rows identified by actual discovered symbols | `$A/tests/sqlite-focused/**`; C; always; 1 |
| E247-06 / R247-03,06 | Provider SQL-lowering/native eligibility results | `python3 scripts/perf/t247_json_gate.py lowering --output artifacts/pdca/D247/rv1/lowering` →0; executes listed filtered provider commands | `$A/lowering/**`; C; always; 1 |
| E247-07 / R247-07 | Build log | `dotnet build nextorm.slnx -c Debug` →0, 0 warnings/0 errors; capture full log | `$A/build.log`; C; always; 1 |
| E247-08 / R247-03..07 | Full affected non-integration suites, totals and raw logs | `python3 scripts/perf/t247_json_gate.py boundary --scope nonintegration --output artifacts/pdca/D247/rv1/boundary` →0, all six projects pass | `$A/boundary/**`; C; always; 1 |
| E247-09 / R247-03,04,06,07 | Unfiltered real-provider boundary suite; provider/container logs | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug` →0; no required provider skipped | `$A/integration/**`; C; always; 1 |
| E247-10 / R247-08 | Coverage raw data, CI-equivalent summary, changed-branch map | `python3 scripts/perf/t247_json_gate.py coverage --output artifacts/pdca/D247/rv1/coverage` →0, ≥85/75; collect during the single boundary sweep, not an extra integration sweep | `$A/coverage/**`; C; always; 1 |
| E247-11 / R247-05,08 | Fault-seeding diffs/results/restoration proof; explicit no-Stryker statement | `python3 scripts/perf/t247_json_gate.py fault-checks --output artifacts/pdca/D247/rv1/fault-checks` →0 only if all three faults killed and restored tests pass | `$A/fault-checks/**`; C; always; 1 |
| E247-12 / R247-01,02 | Two final full BDN rounds and per-bucket oracle calculations | Final driver command above →0 only when every literal R247-01 comparison passes | `$A/final/**`; C; always; 1 |
| E247-13 / R247-09 | Parser regression tests: legacy100/JSON1, params, missing jobs/results, unit conversion | `python3 -m unittest discover -s scripts/perf/tests -p 'test_t247_json_gate.py'` →0, all positive/negative cases pass | `$A/gate-tests.log`; C; always; 1 |
| E247-14 / R247-01,09 | Legacy allocation gate plus three-bucket JSON timing/allocation gate | `python3 scripts/perf/t247_json_gate.py gate --baseline artifacts/pdca/D247/rv1/baseline --final artifacts/pdca/D247/rv1/final --output artifacts/pdca/D247/rv1/gate` →0, underlying iteration14 invocation above exits0 | `$A/gate/**`; C; always; 1 |
| E247-15 / R247-09 | Two paired acceptance runs, seven-case count and elapsed time | `python3 scripts/perf/t247_json_gate.py acceptance --baseline artifacts/pdca/D247/rv1/baseline --output artifacts/pdca/D247/rv1/acceptance` →0, seven cases, ≤240s and ratio limits | `$A/acceptance/**`; C; always; 1 |
| E247-16 / R247-06,08 | Public API comparison, CRLF byte audit, diff/suppression/skip counters, final footprint | `python3 scripts/perf/t247_json_gate.py audit --base 01815f3a --output artifacts/pdca/D247/rv1/audit` →0; public delta/new suppressions/new skips=0 | `$A/audit/**`; F+C; always; 1 |
| E247-17 / R247-10 | EN/RU/registry/report consistency and link audit | `python3 scripts/perf/t247_json_gate.py docs --output artifacts/pdca/D247/rv1/docs` →0; both languages present, claims trace to exports, no public specs links | `$A/docs/**`; C; always; 1 |
| E247-18 / R247-11 | Manifest structure and complete required-row inventory | `python3 scripts/validate_inner_loop.py manifest artifacts/pdca/D247/rv1/manifest.json` →0, all required rows accounted for | `$A/manifest-validation.log`; K; always; 1 |
| E247-19 / R247-11 | Evidence source/applicability lint | `python3 scripts/pdca_evidence.py lint --repo . --manifest artifacts/pdca/D247/rv1/manifest.json` →0, no unresolved errors | `$A/evidence-lint.log`; K; always; 1 |

**Contract delta rv1 → rv2**

- inherited rows сохраняют текст дословно; перепривязываются только invocation bindings;
- `scripts/perf/t247_json_gate.py` → `.pdca/scripts/perf/t247_json_gate.py` (inherited rows сохраняют старый literal-текст);
- `artifacts/pdca/D247/rv1/` → `.pdca/T247/rv2/` (evidence root);
- `scripts/validate_inner_loop.py` / `scripts/pdca_evidence.py` → `.pdca/scripts/…`;
- inherited `rv=1` rows трактуются как единый действующий revised contract; одна manifest entry на ID (без duplicate historical rows);
- действующие валидаторы — ровно `V1` (`validate_inner_loop.py manifest .pdca/T247/rv2/manifest.json`) и `V2` (`pdca_evidence.py lint --repo . --manifest .pdca/T247/rv2/manifest.json`).

CHECK re-gather budget: **2 total across DO/CHECK**, owner **check**; maintain counter and exact requests in this status. No third re-gather; unresolved evidence → blocker/escalation, not fabricated pass.

Для активной rv2 интерпретации inherited evidence:

- стабильные ID и обязательства остаются;
- текущая ревизия — `rv=2`;
- invocation bindings используют исправленный driver path и подтверждённые post-refactor source paths;
- оригинальный carried block не переписывается: binding corrections фиксируются отдельным contract delta;
- manifest содержит **одну запись на ID**, без duplicate historical/current rows.

Все обязательные slots каждой active строки: requirement ID, evidence ID, scenario/check, expected evidence kinds/sources, exact invocation, exit/result/log requirements, artifacts, owner, observable applicability predicate, `rv`. Planned sources явно маркируются planned.

### Дополнительные evidence rows

Все будущие sources/artifacts ниже — **planned**, не утверждение о выполненных проверках.

| ID ↔ requirement | Check / invocation | Требуемый exit/result/log | Artifact / source | Owner | Applicability predicate | rv |
|---|---|---|---|---|---|---|
| **E247-20 ↔ R247-12** | `B`, `F`, `C` | По 2 full rounds; пять row counts; 100-row/intercept gates pass в каждом; no zero matches | `small-row.json`, `intercept-fit.json`, raw BDN exports; fixed-cost harness + planned driver | coder/perf | Managed preparation/writer path изменяется: true | 2 |
| **E247-21 ↔ R247-13** | `U`, `I` | Exit 0; differing-storage cases executed; applicable live providers present; hit/validation доказаны | Test logs/results, `key-contract.json`, provider inventory | coder/tests | Structural cache включён; differing-storage cases перечислены | 2 |
| **E247-22 ↔ R247-14** | `U`, `I` | Exit 0; shape/option isolation и metadata bypass executed; no approximate hits | `key-contract.json`, case inventory, test results | coder/tests | Shapes/options/converters участвуют в key: true | 2 |
| **E247-23 ↔ R247-15, R247-03…07** | `U`, `I` | Exit 0; обе поверхности × sync/async; каждый P1 branch закрыт test/доказанным guard; providers не потеряны | `variant-matrix.json`, test results, provider inventory | coder/tests | Каждый перечисленный branch; исключения только по capability evidence | 2 |
| **E247-24 ↔ R247-16** | `P`, `U` | Exit 0; compile-only/steady/public различены; residual не назван preparation; resident single-build pass | `phase-attribution.json`, BDN exports, lifecycle results | coder/perf | Reusable managed writer compilation существует: true | 2 |
| **E247-25 ↔ R247-01,02,09** | `B`, `F`, `C` | Legacy ratios/allocations/controls pass; ровно 7 acceptance cases ≤240 s; per-case ≤1.20 в обоих rounds | `comparison.json`, acceptance inventory, byte-control results, BDN exports | coder/perf | Performance intervention: true | 2 |
| **E247-26 ↔ R247-07,08** | `BUILD`, `U`, `I`; inherited coverage invocations | 0 warnings/errors; tests pass; main gate, no regression и changed-branch evidence; integration red baseline явно отделён | Build/test logs, coverage reports, baseline binding, CRLF/suppression/skip inventory | coder/tests | Changed production/test files: true | 2 |
| **E247-27 ↔ R247-10** | CHECK review report/EN/RU/findings against `C` outputs и document inventory | Все обязательные документы согласованы; historical/current измерения различены; #250 и F247-INT-COV имеют tracking | `docs-inventory.json`, performance report, verified follow-up URLs, CHECK report | coder/docs produces; check reviews | Новые measurements/design decisions: true | 2 |
| **E247-28 ↔ R247-11,17** | `V1`, `V2` | Оба rc 0; E01…30 представлены по applicability; required/actual rows согласованы; receipts привязаны к одному frozen tree | `manifest.json`, `validate-inner-loop.log`, `evidence-lint.log`, contract delta | coder produces; check verifies | Всегда | 2 |
| **E247-29 ↔ R247-08,17** | CHECK invokes skills `dotnet-csharp-code-smells`, `slopwatch`, `dotnet-api-surface-validation`, `api-design` | Все четыре audits выполнены; measured suppression/slop counters и ratios, denominator и before/after basis опубликованы | CHECK audit report и source inventory | check | Changed production/test/API footprint: true | 2 |
| **E247-30 ↔ R247-08,17** | CHECK calculates changed-branch delta from coverage evidence and runs Stryker.NET on changed types | Branch delta и no-regression verdict опубликованы; mutation result не подменён branch coverage; фактические argv/exit/results сохранены | Branch-delta report, Stryker config/results/log, CHECK report | check | Изменены executable types: true | 2 |

E247-29/30 — **CHECK obligations, не DO work**. Конкретный Stryker invocation фиксирует CHECK по обнаруженной project tool/configuration; план не выдумывает установленную версию, future symbol или отсутствующий mutation threshold.

### Обязательные manifest/provenance checks

**V1:**

```text
python3 .pdca/scripts/validate_inner_loop.py manifest .pdca/T247/rv2/manifest.json
```

Требует корректной row shape (`status/evidence/reason/predicate`) и покрытия `required_rows` ↔ `rows`.

**V2:**

```text
python3 .pdca/scripts/pdca_evidence.py lint --repo . --manifest .pdca/T247/rv2/manifest.json
```

Требует rc 0 и:

```text
manifest.tree == recomputed frozen tree
              == tree/tree_binding каждого receipt
```

Проверяются **все receipts, включая non-selected**. Path escape, duplicate IDs и placeholder trees не допускаются.

Manifest записывает DO на **DO→CHECK boundary**. CHECK **открывает manifest первым**, прежде чем принимать отчёты.

CHECK evidence roots проекта: `.pdca/**`, `TestResults/**`, read-only `docs/specs/status/**`, `artifacts/**`.

### CHECK re-gather budget

Скопированная verbatim строка rv1 `:204` (post-pointer current tree: `:205`) сохраняется. Budget: **2 точечных re-gather requests суммарно**, owner — `check`.

Каждый запрос указывает missing slot, источник и ожидаемый результат. Простое отсутствие отчёта сначала закрывается re-gather; само по себе оно не обосновывает contract revision или DO iteration. После исчерпания budget missing evidence остаётся GAP/inconclusive и маршрутизируется по контракту цикла.

## Документация и tracking

Затронуты:

- `docs/specs/performance/T247-json-streaming-report.md`;
- существующие EN/RU comparison pages и follow-up/findings registries из rv1;
- #247: результаты с pinned baseline/fingerprints;
- #250: JSON-local decision и extraction при втором consumer;
- F247-INT-COV: отдельный baseline-coverage follow-up в `1.0.9-rc3`.

Точные comparison paths переносятся из rv1, не угадываются. Перед созданием issue — поиск open/closed duplicates; при совпадении обновляется существующий issue.

Public API не меняется, XML-doc expansion не требуется. Публичные docs не ссылаются на internal specs.

## Что не сказала постановка: закрытие и остаточные риски

| Вопрос | Решение |
|---|---|
| Status path / исторический rv1 | Закрыт: explicit lane path, current in-flight status, minimal pointer |
| Inherited contract / validators | Закрыт: mechanical carry anchors, V1/V2, global contract |
| Красный integration coverage baseline | Закрыт: known baseline, no regression; отдельный F247-INT-COV в текущем milestone |
| Доля compilation в total | D247-01; до замера неизвестна |
| Атрибуция residual | Принимается как неатрибутированный; не выдаётся за measured preparation |
| Mutable/opaque converter или policy metadata | Conservative bypass |
| Provider independence вне известного dispatch | Обязательные R247-13 tests; обнаруженная зависимость → PLAN |
| Contention cold compilation под lock | Принятый минимальный риск; менять только по измерению |
| Достижимость ≤50% small-row/intercept | **Остаточный риск, не GAP**; при провале replan без снижения criteria |
| Shared-tree interference T244/T245 | Coordinated writes и quiet measurement/frozen-tree windows |

**Уверенность:** высокая в scope и cache constraints; средняя в достижимости нового 50% gate до spike. Открытых path/contract вопросов нет.

Если данные докажут, что выбранного вмешательства недостаточно, формируется новая P-задача с фактически изменённым планом. Отсутствие отчёта или переименование задачи не создаёт новую ревизию.

## Final supersession pointer для rv1

Добавить ровно одну строку; **каждый другой byte rv1 оставить без изменения**, без reflow, удаления или переформулировки:

> Plan state | Evidence contract rv=1 superseded by rv=2 in D247-json-streaming-2.md; historical content retained verbatim; N=1, r=1, n=1 unchanged; no DO executed.

Collection status не редактировать.

## Предусловия DO и первый шаг

До начала implementation:

1. Coder сохраняет этот план, механически вставляет inherited строки и добавляет pointer.
2. Закрепляет полный rv2 contract с единственными `V1`/`V2`, artifacts и applicability.
3. Проверяет ненулевой harness discovery и согласует shared-tree measurement window.
4. Выполняется обязательный gate 1 выбранного режима. В обычном режиме approval следует **после сохранения плана**; autonomous mode не требует вопроса, но не отменяет gate.
5. Текущий запуск заканчивается здесь: **STOP на PLAN→DO**, ничего не исполнять.

Прежний terminal `incomplete` возник до DO. Теперь oracle конкретизирован существующим harness, planned driver, baseline-before-production sequence и отдельным small-row gate; отсутствие прежнего execution не маскируется как pass.

**Первый последующий DO шаг:** benchmark-only instrumentation и реализация driver, затем pinned pre-change `B` и narrowed D247-01. Production cache до capture не менять.

## Журнал прогресса (append-only)

2026-10-18 | DO | D247-01 driver+instrumentation — landed `.pdca/scripts/perf/t247_json_gate.py` (14 subcommands, stdlib-only, no CLI-configurable threshold), `.pdca/scripts/perf/tests/test_t247_json_gate.py` (30 unittest cases, positive + negative), compile-only spike arm `Nextorm_CompileOnly_3Col` and steady-writer arm `Nextorm_SteadyWriter_3Col` in `SqliteBenchmarkJsonFixedCost.cs`, and the R247-02 control `Nextorm_ToList_Dto_Json` in `SqliteBenchmarkWriteJson.cs`; **no `src/**` change**. Commands + exit codes: `python3 -m unittest discover -s .pdca/scripts/perf/tests -p test_t247_json_gate.py` → 0 (30 tests, 0 failures); `validate_inner_loop.py brief` → 0, `project` → 0, `report` → 0; driver smoke `preflight` → 0, `docs` → 1 (report page not yet written), `compare` → 2 (no baseline yet, fail-closed), `fault-checks` → 5 (fault specs not pinned), `gate` → 2 (no final rounds), `audit --base 1b140d23` → 0, `coverage` → 2 (boundary not run). Evidence: `.pdca/T247/rv2/logs/t247_driver_tests.log`, `.pdca/T247/rv2/logs/validate_inner_loop.log`, `.pdca/T247/rv2/logs/driver_smoke.log`.

2026-10-18T03:06:10Z | DO | r=1 | n=1/3 | D247-01 driver + benchmark-only instrumentation landed; inner-loop Python unittest 30/30 pass; `validate_inner_loop.py brief/project/report` 0/0/0 | `.pdca/T247/rv2/scope.json`, `.pdca/T247/rv2/journal.json`, `.pdca/T247/rv2/report.json`, `.pdca/T247/rv2/logs/`

**Notice: host lock name discrepancy (recorded, NOT silently resolved).** Plan text names `/tmp/opencode/nextorm-bdn.lock`; `docs/specs/status/collection-1.0.9-rc3-open-3.md:45` pins `/tmp/opencode/nextorm-bdn-host.lock` as canonical and states it supersedes every per-plan lock name; only the `-host` variant exists on disk (the T244/D243/D246 lanes used it). `t247_json_gate.py` therefore takes the OUTER lock as `flock -x /tmp/opencode/nextorm-bdn-host.lock` (strictly safer: it also excludes a foreign host-lock holder) and MUST NOT nest a second lock. The driver does not flock itself — the caller wraps it — but it detects an overlap (a foreign `dotnet build|test|run`, BDN/testhost or coverage collector inside its block) and a `HEAD`/fingerprint move, and invalidates the round with reason code `round-invalid` (exit 4). The plan's literal lock name is retained as a recorded input fact (`lock.plan_text_name` in every `B/P/F` payload).

**Fact: T247 gate rows are measurement-derived and deferred to the freeze step.** `iteration14-budgets.json` currently carries 28 rows; T247 is the third gate owner and must ADD 3 rows (`Json_1k_WriteJson`, `Json_10k_WriteJson`, `Json_100k_WriteJson`) without relaxing any existing row. Their numeric budgets cannot be invented before the paired baseline is measured, so `gate` requires exactly those 3 ids and fails closed with `t247-budget-rows-missing` (exit 5) until they are pinned at D247-04. No existing threshold was relaxed.

**Fact: R247-02 control placement.** The control arm `Nextorm_ToList_Dto_Json` was added to `SqliteBenchmarkWriteJson.cs` rather than `SqliteBenchmarkJsonFixedCost.cs`: that class owns the 1k/10k/100k row counts and the `WriteJson_Array_Dto` byte-equality counterpart, and its 11 control-inclusive arms × 3 row counts = the 33 cases E247-02 pins. The compile-only spike and steady-writer arms live in `SqliteBenchmarkJsonFixedCost.cs` as planned.

**Fact: the benchmark C# was not compiled in this unit.** The unit's inner loop is the Python unittest driver test only (no `dotnet build` of the solution, no integration run), so the two `.cs` edits are unverified by a compiler here; the affected benchmark build is the measurement unit's first step.

2026-10-18T05:45:00Z | DO | r=1 | n=1/3 | D247-01 baseline measurement attempt BLOCKED (driver rc=4, both rounds invalid on `source state moved during the block`); no baseline/spike numbers exist, phase block NOT started | `.pdca/T247/rv2/invalid-baseline-1b140d23/` (baseline.json+both round.json+33/40 raw BDN exports), `.pdca/T247/rv2/logs/invalid-baseline-driver-1b140d23.log`

**Fact: baseline block argv, lock and rc.** Wrapper (outer lock, no nested lock, driver never flocks itself):
`flock -x /tmp/opencode/nextorm-bdn-host.lock bash -lc 'cd /home/alex/sources/nextorm && NEXTORM_BENCH_FULL=1 python3 .pdca/scripts/perf/t247_json_gate.py baseline --rounds 2 --evidence-root .pdca/T247/rv2'`
Driver argv: `baseline --rounds 2 --evidence-root .pdca/T247/rv2` (each round runs `public-json` = `*SqliteBenchmarkWriteJson*` then `fixed-cost` = `*SqliteBenchmarkJsonFixedCost*`). Start 2026-10-18T03:18:39Z, finish 2026-10-18T05:44:35Z (2 h 25 min 56 s wall). **rc = 4** (`EXIT_OVERLAP`), captured by the wrapper marker `BASELINE_RC=4` in `.pdca/T247/rv2/logs/invalid-baseline-driver-1b140d23.log`; `baseline.json` records `status=invalid`, `reason_code=round-invalid`. The round was never interrupted, restarted or signalled; no threshold, tolerance, budget or denominator was relaxed and no case was dropped.

**Fact: per-round validity and counts (both rounds INVALID, numbers deliberately discarded).** round-1 03:18:39Z→04:34:38Z (4558.7 s): `public-json` exit 0 with 33/33 discovered/executed, `fixed-cost` exit 0 with 40/40, reason `source state moved during the block`; fingerprint before `head=1b140d23 dirty_paths=2 porcelain_sha256=f3a4297f6b4c…`, after `head=1b140d23 dirty_paths=8 porcelain_sha256=42e41216e232…`. round-2 04:34:38Z→05:44:35Z (4197.9 s): `public-json` exit 0 33/33, `fixed-cost` exit 0 40/40, reason `source state moved during the block`; fingerprint before `head=1b140d23 dirty_paths=8 porcelain_sha256=42e41216e232…`, after `head=c99cc512 dirty_paths=0 porcelain_sha256=e3b0c44298fc…`. Because a round is invalid the driver zeroes its `cases`, so `baseline.json` has `discovered_case_count=0`/`executed_case_count=0` and there are **no baseline or spike numbers** to report: R247-02's 33-case control-inclusive expectation was discovered and executed twice (33 each) but is not admissible evidence from a moved tree.

**Cause (external, not a driver defect).** A parallel lane edited six `src/nextorm.core` files at 03:49:39Z (`EntityMetadata.cs`, `SqlSourceRenderer.cs`, `MemberInfoExtensions.cs`, `MemberTranslator.cs`, `RangeColumnsSqlTranslator.cs`, `SqliteFunctionTranslator.cs`, +94/−18) inside round-1's fixed-cost invocation, then committed twice inside round-2: `66f7125f` at 04:56:24Z and `c99cc512` at 05:07:33Z. round-2's BDN `dotnet build` therefore compiled a tree that HEAD left mid-round; the two rounds are also on two different fingerprints, so they could never form a valid pair. The driver behaved correctly (fail-closed), so **no driver or unit-test change was made** and `.pdca/scripts/perf/tests/test_t247_json_gate.py` was not touched.

**Fact: DB digests matched the pin exactly, on both paths the rounds used.** `sha256sum /tmp/nextorm-bench/test.db` = `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295`; `sha256sum benchmarks/nextorm.benchmark/data/test.db` = `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295` (pinned value, unchanged). Working tree after the block: `HEAD=c99cc512`; whole-repo `git status --porcelain` = 5 entries; scoped fingerprint pathspec (`src`, `benchmarks/nextorm.benchmark`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nextorm.slnx`, `.editorconfig`) = 0 dirty paths.

**Notice: lock name (repeated for this block).** The plan's literal lock is `/tmp/opencode/nextorm-bdn.lock`; `docs/specs/status/collection-1.0.9-rc3-open-3.md:45` pins `/tmp/opencode/nextorm-bdn-host.lock` as canonical and superseding, only the `-host` variant exists on disk, and a `nextorm-bdn.lock` holder would not be mutually excluded. The block therefore ran under **`/tmp/opencode/nextorm-bdn-host.lock`** (`lock.mode=inherited flock -x`, `inherited_fd=3`): strictly safer, no threshold change.

**Artifacts preserved, not counted.** The invalid attempt is kept on disk as `.pdca/T247/rv2/invalid-baseline-1b140d23/` (moved there by a parallel lane, not by this unit): `baseline.json`, `baseline.log`, `round-{1,2}/round.json`, `round-{1,2}/public-json.console.log`, `round-{1,2}/fixed-cost.console.log` and the raw BDN exports under `round-N/{public-json,fixed-cost}/results/` (33 and 40 cases per round). They are never to be counted as valid evidence.

**Not done, deliberately.** The `phase` block (`NEXTORM_BENCH_FULL=1 … t247_json_gate.py phase --rounds 2 …`) and `phases` attribution were **not** started: their precondition — two valid baseline rounds on ONE scoped fingerprint — was never met, and a phase block would likewise be invalidated by the same concurrent commits. `phase-attribution.json` therefore does not exist yet. `journal.json` (unit D247-01) was left untouched: this unit produced no green execution to append, and appending a non-green measurement event would falsely invalidate D247-01's recorded boundary run; the failed attempt lives in this section with its exact paths and rc instead.

**Question for the orchestrator/planner (blocker).** Which revision is the pinned pre-change baseline: (a) re-run the baseline on the now-clean, committed `c99cc512` (it contains **no** T247 production change — `DataContext*.cs`/JSON cache untouched — but does contain the unrelated `66f7125f` derived-entity fix), or (b) reconstruct `1b140d23` + only the two D247-01 benchmark files in an isolated worktree so the foreign six-file src change is excluded? Either way the sibling lanes must hold commits/builds for the ~2.5 h window, otherwise every round is invalidated again.

**Follow-up fact (append-only, 2026-10-18T05:45:59Z)**: a sibling lane under the same orchestrator started the retry itself — pid tree 1205286 `bash -c` → 1205287 `flock -x /tmp/opencode/nextorm-bdn-host.lock` → 1205288 `python3 .pdca/scripts/perf/t247_json_gate.py baseline --rounds 2 --evidence-root .pdca/T247/rv2` → 1205327 `dotnet run --project benchmarks/nextorm.benchmark` — i.e. option (a) in flight, on the clean committed `c99cc512` tree, writing the regenerated `.pdca/T247/rv2/baseline/` and `.pdca/T247/rv2/logs/baseline-driver.log`. This unit did NOT start it and did not interfere with it; the outcome of that block is evidence for the owning unit, not for this one.
2026-10-18T07:38:00Z | DO | r=1 | n=1/3 | baseline re-anchored to `c99cc512` (option (a)); `BASELINE_RC=0` with 2/2 VALID rounds on ONE scoped fingerprint; 33/33 control-inclusive public-json cases and 40/40 fixed-cost cases per round; phase/spike block started next on the same fingerprint | `.pdca/T247/rv2/baseline/round-{1,2}/round.json`, `.pdca/T247/rv2/baseline/baseline.json`, `.pdca/T247/rv2/logs/baseline-driver.log`
**Decision: baseline re-anchored to `c99cc512` (option (a)) - admissible.** The plan's original HEAD `1b140d23` advanced by exactly two intervening commits, `66f7125f` (derived-entity member mapping + docs/specs consolidation) and `c99cc512` (.gitignore + versioned PDCA status files). Over `1b140d23..c99cc512` the only `src/`+`benchmarks/` deltas are: (i) the two D247-01 benchmark files (`benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkJsonFixedCost.cs` +137, `SqliteBenchmarkWriteJson.cs` +51 - this unit's own instrumentation), (ii) six foreign files from the derived-entity fix (`DataContext/Meta/Implementation/EntityMetadata.cs`, `DataContext/SqlSourceRenderer.cs`, `MemberInfoExtensions.cs`, `Visitors/MemberTranslator.cs`, `Visitors/RangeColumnsSqlTranslator.cs`, `Visitors/SqliteFunctionTranslator.cs`), (iii) BDN artifact json/md/csv/html under `benchmarks/BenchmarkDotNet.Artifacts/`. **No file of the JSON execution path is touched:** `src/nextorm.core/Query/Json/**`, `QueryExecutor.cs`, `DataContext/DataContext.cs` and `DataContext/DataContextCache.cs` all return zero diff (`git diff --name-only 1b140d23 c99cc512 -- <those paths>`). The JSON write/stream path measured at `c99cc512` is therefore identical to the path at the plan's original HEAD and the baseline stays a valid pre-change anchor. The paired `final` must later differ from `c99cc512` only by T247's files; **no worktree was created** (single-tree standing rule).
**Fact: baseline block argv, lock and rc.** Wrapper (outer lock only, no nested lock, driver never flocks itself): `flock -x /tmp/opencode/nextorm-bdn-host.lock bash -lc 'cd /home/alex/sources/nextorm && NEXTORM_BENCH_FULL=1 python3 .pdca/scripts/perf/t247_json_gate.py baseline --rounds 2 --evidence-root .pdca/T247/rv2'`, whose final line `BASELINE_RC=0` is in `.pdca/T247/rv2/logs/baseline-driver.log`. Driver argv: `baseline --rounds 2 --evidence-root .pdca/T247/rv2`; `status=pass`, `reason_code=ok`, 146/146 discovered/executed, `expected_case_count=33`; `lock.mode=inherited flock -x` (`inherited_fd=3`, canonical `/tmp/opencode/nextorm-bdn-host.lock`). rc = 0 (`EXIT_OK`). Total wall 1 h 44 min 4 s (round-1 05:45:24Z -> 06:42:33Z, round-2 06:42:33Z -> 07:29:28Z).
**Fact: per-round validity, counts and fingerprints (one fingerprint for both rounds - required condition met).** round-1 (3437.451 s): `valid=true`, no reasons, `public-json` rc 0 with 33/33 discovered/executed, `fixed-cost` rc 0 with 40/40. round-2 (2840.508 s): `valid=true`, no reasons, `public-json` rc 0 with 33/33, `fixed-cost` rc 0 with 40/40. Every `fingerprint_before` and `fingerprint_after` of both rounds is byte-identical: `head=c99cc512450006f7d96cadc6fffde78ce9fe5ad6`, `dirty_paths=0`, `porcelain_sha256=e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` over the pathspec `src`, `benchmarks/nextorm.benchmark`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `nextorm.slnx`, `.editorconfig`. External writes during the window (docs/**, AGENTS.md, .github/workflows, tests/** from sibling lanes) all fall outside that pathspec, which is why no round moved.
**Fact: all control-inclusive cases represented in BOTH rounds (E247-02).** `public-json` = 11 arms x 3 row counts (1000/10000/100000) = 33 distinct cases per round, zero zero-match and zero duplicates: `WriteJson_Array_Scalar`, `WriteJson_Array_ScalarPayload`, `WriteJson_Array_Dto`, `WriteJson_Array_WideDto`, `WriteJsonAsync_Array_Scalar`, `WriteJsonAsync_Array_ScalarPayload`, `ToList_Dto`, `Nextorm_ToList_Dto_Json`, `Dapper_ToList_Json`, `EFCore_ToList_Json`, `Linq2Db_ToList_Json`. `fixed-cost` = 8 arms x 5 row counts (1/10/100/1000/10000) = 40 per round. A zero/empty match would have failed the driver with `zero-matched-cases`; it did not.
**Fact: baseline numbers the driver produced (mean of round-1/round-2, microseconds unless noted).** `public-json`: `WriteJson_Array_Scalar` 1063.8 / 6185.9 / 54915.9 and `WriteJson_Array_Dto` 3679.6 / 10908.5 / 89224.8 and `Linq2Db_ToList_Json` 650.3 / 9527.5 / 96847.8 and `Nextorm_ToList_Dto_Json` 678.0 / 7799.1 / 88763.4 at Rows 1k/10k/100k. R247-01 same-run ratio `WriteJson_Array_Dto / Linq2Db_ToList_Json`, per round: 1k 5.9509 / 5.4164 (mean 5.6837), 10k 1.1275 / 1.1608 (mean 1.1442), 100k 0.8108 / 1.0441 (mean 0.9274). R247-01 10k allocation of `WriteJson_Array_Dto`: 445338 B / 444706 B. `fixed-cost`: `Nextorm_WriteJson_3Col` 2150.2 / 1876.7 / 1954.0 / 3072.0 / 16604.3 at Rows 1/10/100/1000/10000; `Nextorm_CompileOnly_3Col` 1753.6 / 1602.1 / 1612.2 / 1961.5 / 1992.5; `Nextorm_SteadyWriter_3Col` 0.2 / 1.7 / 15.5 / 149.7 / 1539.5. The three gate rows `Json_{1k,10k,100k}_WriteJson` are pinned at D247-04 from these measurements; nothing was pinned, relaxed or invented here.
**Fact: DB digests and tree state.** `sha256sum /tmp/nextorm-bench/test.db` = `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295` and `sha256sum benchmarks/nextorm.benchmark/data/test.db` = `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295` (pin unchanged). `HEAD=c99cc512450006f7d96cadc6fffde78ce9fe5ad6`; whole-repo `git status --porcelain` = 18 entries at report time (docs/**, AGENTS.md, .github/workflows/dotnet.yml, tests/** added by sibling lanes); the scoped fingerprint pathspec has 0 dirty entries, as the round records prove.
**Fact: integration-coverage baseline in AGENTS.md (unchanged gate, still red by design).** `AGENTS.md` now states 77.4 % line / 68.8 % branch after the `NextORM.Core.InMemory*` exclusion, under the unchanged `MIN_INTEGRATION_LINE_COVERAGE=65` / `MIN_INTEGRATION_BRANCH_COVERAGE=75` gate; at dispatch the file still read 77.0 % / 68.3 % and a sibling coverage lane updated the figure during this block. The thresholds were never lowered, the denominator was never altered, and this cycle does not chase that gate green.
2026-10-18T07:44:00Z | DO | r=1 | n=1/3 | phase/spike block started on the same fingerprint (wrapper 1315364 -> flock 1315365 -> driver 1315367 -> dotnet run 1315405); nonzero discovery already observed (`SqliteBenchmarkJsonStreamPhase2`, benchmarkId 5) | `.pdca/T247/rv2/logs/phase-driver.log` (empty until completion), `.pdca/T247/rv2/phase/round-1/phase2.console.log`

**Incident: the launch produced a SECOND, blocked flock waiter for the same phase argv (pid 1317359, parent 1317357).** The shell tool executed the backgrounded command twice while handing it off: the first copy is the one measuring (1315364 -> 1315365 -> 1315367 -> 1315405), the second copy sits in `flock -x /tmp/opencode/nextorm-bdn-host.lock` and will acquire the lock as soon as the first block releases it, i.e. it would run the whole `phase --rounds 2` block AGAIN and rewrite `.pdca/T247/rv2/phase/round-{1,2}` and `phase.json`. Both wrappers redirect to the same `.pdca/T247/rv2/logs/phase-driver.log`. This unit sent no signal and touched no lock (hard rules); stopping pid 1317359 is an orchestrator decision. The running block is unaffected and must not be interrupted.

**Notice: duplicate wrapper terminated on orchestrator order; duplicate guard added (host lock protocol unchanged).** Дедупликация вещания: tool double-launch; guard added; host lock protocol unchanged; no threshold change. argv was verified before acting: 1317359 = `flock -x /tmp/opencode/nextorm-bdn-host.lock bash -lc 'cd /home/alex/sources/nextorm && NEXTORM_BENCH_FULL=1 python3 .pdca/scripts/perf/t247_json_gate.py phase --rounds 2 --evidence-root .pdca/T247/rv2'` (blocked in `locks_lock_inode_wait`) with parent 1317357 = its `bash -c` wrapper; no other queued host-lock holder or `t247_json_gate.py phase` process existed. 1317359 was sent `SIGTERM` first, then 1317357; both are gone (`/proc` absent). The legitimate chain 1315364 -> 1315365 -> 1315367 -> 1315405 was **never signalled** and was re-verified alive with exactly one phase chain remaining after the kill; the lock file was not touched. Side effect recorded, not hidden: both wrappers used `>` on the same `phase-driver.log`, so the duplicate's shell wrote `Terminated` / `PHASE_RC=143` into it after the signal; those two lines belong to the **duplicate**, not to the running block (`sha256 9bd249360821c34a078de121df174cf181d57e6a337ea5c19ef2fc4fc35f0ecf`, preserved as `.pdca/T247/rv2/logs/phase-driver-duplicate-residue.log`). Round artifacts were unaffected: the duplicate never acquired the host lock and never started a driver, so `.pdca/T247/rv2/phase/` kept being written by 1315367 only. **Guard:** future measurement blocks launch through `.pdca/scripts/perf/t247_measure.py`, which first takes a NON-BLOCKING marker lock `/tmp/opencode/t247-measure.guard` (`flock -n`; on contention prints `T247-DUPLICATE-GUARD: another measurement is running` and exits **90**) and only then execs the blocking `flock -x /tmp/opencode/nextorm-bdn-host.lock` around the driver. The marker is a duplicate guard on its own file, **not** a nested host lock: the single canonical host lock and every threshold/tolerance/budget/denominator are unchanged. Self-tests: `DuplicateGuardWrapperTests` (3 cases) in `.pdca/scripts/perf/tests/test_t247_json_gate.py`, rc 0, log `.pdca/T247/rv2/logs/t247_guard_wrapper_tests.log`.

2026-10-18T09:02:00Z | DO | r=1 | n=1/3 | phase/spike block complete: `PHASE_RC=0` (`phase: PASS (128 case(s) over 2 round(s))`), 2/2 VALID rounds on ONE fingerprint (`c99cc512`, 0 dirty paths, unchanged before/after each round), 64/64 discovered/executed per round (`SqliteBenchmarkJsonStreamPhase2` 20/20 + `JsonStreamPhase2WriterBenchmark` 4/4 + `SqliteBenchmarkJsonFixedCost` 40/40), `phases` rc 0; attribution Rows=100: measured compile 1902.8/1760.6 µs, steady writer 13.8/15.3 µs, public total 2679.6/2246.4 µs, **un-attributed residual** 776.7/485.8 µs (never relabelled preparation); duplicate wrapper 1317359/1317357 terminated, exactly ONE chain remained; duplicate guard added | `.pdca/T247/rv2/phase/round-{1,2}/round.json`, `.pdca/T247/rv2/phase/phase.json`, `.pdca/T247/rv2/phases/phase-attribution.json`, `.pdca/T247/rv2/logs/phase-driver.log`, `.pdca/T247/rv2/logs/phases-driver.log`

**Fact: phase/spike block argv, lock and rc (D247-01 narrowed compile-only spike).** Wrapper: `flock -x /tmp/opencode/nextorm-bdn-host.lock bash -lc 'cd /home/alex/sources/nextorm && NEXTORM_BENCH_FULL=1 python3 .pdca/scripts/perf/t247_json_gate.py phase --rounds 2 --evidence-root .pdca/T247/rv2'`; driver argv `phase --rounds 2 --evidence-root .pdca/T247/rv2`. Per round the driver ran two children: (1) `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkJsonStreamPhase2* *JsonStreamPhase2WriterBenchmark* --exporters json csv --artifacts .pdca/T247/rv2/phase/round-N/phase2`, (2) the same with `--filter *SqliteBenchmarkJsonFixedCost* --artifacts .../fixed-cost`; all four children exited 0 (`phase/phase.log`). `lock.mode=inherited flock -x`, `inherited_fd=3`, canonical `/tmp/opencode/nextorm-bdn-host.lock`; rc = 0 (`EXIT_OK`). Wall: round-1 07:29:41Z→08:10:05Z (2424.119 s), round-2 08:10:05Z→08:58:48Z (2924.116 s), block 07:29:41Z→08:58:48Z (89 min). The phase-driver log final content is `phase: PASS (128 case(s) over 2 round(s))` + `PHASE_RC=0` (the duplicate's `PHASE_RC=143` residue was preserved separately, see the duplicate-guard Notice).

**Fact: mandatory NONZERO discovery held for both phase filters in both rounds.** `SqliteBenchmarkJsonStreamPhase2` 20 cases and `JsonStreamPhase2WriterBenchmark` 4 cases per round (24/24 executed, zero zero-match), plus 40/40 fixed-cost cases — a zero match would have failed the driver with `zero-matched-cases` (exit 2). `phase.json`: `status=pass`, `reason_code=ok`, `discovered_case_count=128`, `executed_case_count=128`, `expected_case_count=null` (that expectation belongs to baseline/final only). Both `round.json` records: `valid=true`, no reasons, byte-identical `fingerprint_before`/`fingerprint_after` = `head=c99cc512450006f7d96cadc6fffde78ce9fe5ad6`, `dirty_paths=0`, `porcelain_sha256=e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`.

**Fact: compile/steady/total/residual (R247-16 / E247-24), 3Col at Rows=100, mean ns per round-1/round-2, not converted into a threshold).** `measured_compile` (`Nextorm_CompileOnly_3Col`) 1902843.85 / 1760643.79 ns; `steady_writer` (`Nextorm_SteadyWriter_3Col`) 13803.56 / 15308.59 ns; `public_total` (`Nextorm_WriteJson_3Col`) 2679559.76 / 2246443.08 ns; `unattributed_residual` = `public_total - measured_compile` = 776715.91 / 485799.29 ns. The residual is reported **only** as un-attributed (potentially clone/prepare/shape and measurement-boundary differences) and is **not** an independently measured preparation phase; no phase was invented, no threshold/tolerance/budget/denominator was changed, and the fit from `compare` was not substituted for these measurements.

**Fact: driver test suite re-run after the guard change.** `python3 -m unittest discover -s .pdca/scripts/perf/tests -p 'test_t247_json_gate.py'` → rc 0, 33 tests (30 pre-existing + 3 `DuplicateGuardWrapperTests`), log `.pdca/T247/rv2/logs/t247_driver_tests-phase-guard.log`. Journal/projection: `validate_inner_loop.py journal` rc 0, `project` rc 0, `report --journal` rc 0. No commit, no `src/**` or `tests/**` edit, HEAD unchanged at `c99cc512`.
2026-10-18T09:02Z | DO | D247-01 pinned baseline `B` + narrowed compile-only spike `P` — measurement receipt of this unit (the duplicate-dispatch incident and its guard are recorded above and are not rewritten). This unit edited no `src/**` and no `tests/**`; it staged, reverted and committed nothing. **Environment facts:** `NEXTORM_BENCH_DB` unset; `benchmarks/nextorm.benchmark/Infrastructure/BenchDb.cs:11-16` resolved `/tmp/nextorm-bench/test.db` (exists). `sha256sum /tmp/nextorm-bench/test.db` = `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295`; `sha256sum benchmarks/nextorm.benchmark/data/test.db` = `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295` — both byte-identical to the pinned digest, so the pinned copy was used; nothing was substituted or regenerated. **Build gate (R247-07 input):** `dotnet build nextorm.slnx -c Debug` at `1b140d23` -> rc 0, 0 Warning(s) / 0 Error(s) (`logs/build-debug.log`); re-run on the final tree `c99cc512` -> rc 0, 0/0, 25.96 s (`logs/build-debug-c99cc512.log`). `preflight --evidence-root .pdca/T247/rv2` -> rc 0. **First `B` attempt invalidated (evidence retained, never counted):** a foreign session committed `66f7125f` + `c99cc512` during round 1 (HEAD `1b140d23`->`c99cc512`; scoped dirty 2->8->0), the driver reported `source state moved during the block` for both rounds and exited **4**; the 33/40 cases per invocation were discarded (`cases: []`), artifacts are preserved under `invalid-baseline-1b140d23/` and the log as `logs/invalid-baseline-driver-1b140d23.log`. That foreign commit also committed this cycle's already-written driver/test/benchmark files. **`B` valid:** `flock -x /tmp/opencode/nextorm-bdn-host.lock bash -lc 'cd /home/alex/sources/nextorm && NEXTORM_BENCH_FULL=1 python3 .pdca/scripts/perf/t247_json_gate.py baseline --rounds 2 --evidence-root .pdca/T247/rv2'` -> driver **rc 0**. Both rounds share ONE scoped fingerprint, identical before and after inside each round: `head=c99cc512450006f7d96cadc6fffde78ce9fe5ad6`, `porcelain_sha256=e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` (empty status), `dirty_paths=0`. Child argv/exit codes: (1) `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkWriteJson* --exporters json csv --artifacts .pdca/T247/rv2/baseline/round-N/public-json` -> **0**, 33 discovered / 33 executed (11 arms x 3 row counts = 33; nonzero and equal in both rounds); (2) the same argv with `--filter *SqliteBenchmarkJsonFixedCost*` -> **0**, 40/40 (8 arms x 5 row counts). Per round 73/73, total 146; walls 3437.5 s and 2840.5 s; the outer lock was detected as an inherited `flock -x` (fd 3) in both rounds. **Baseline numbers, per round (`WriteJson_Array_Dto / Linq2Db_ToList_Json`):** 1k 5.9509 / 5.4164; 10k 1.1275 / 1.1608; 100k 0.8108 / 1.0441; 10k allocation of `WriteJson_Array_Dto` 445,338 / 444,706 B/op (limit 446,095). **`P` valid:** the same single host lock, `phase --rounds 2` -> **rc 0**; same single fingerprint `c99cc512...`/`e3b0c442...`/0 dirty before and after both rounds; child (1) `--filter *SqliteBenchmarkJsonStreamPhase2* *JsonStreamPhase2WriterBenchmark*` -> **0**, 24/24 (nonzero mandatory discovery); child (2) `--filter *SqliteBenchmarkJsonFixedCost*` -> **0**, 40/40; per round 64/64, total 128; walls 2424.1 s / 2924.1 s. **`phases` -> rc 0** (`.pdca/T247/rv2/phases/phase-attribution.json`): measured compile-only `Nextorm_CompileOnly_3Col` **1,902,844 / 1,760,644 ns** (round 1 / round 2); steady writer `Nextorm_SteadyWriter_3Col` **13,804 / 15,309 ns**; public total `Nextorm_WriteJson_3Col` at Rows=100 **2,679,560 / 2,246,443 ns**; explicitly labelled **un-attributed residual** (`public total - measured compile`; potentially clone/prepare/shape and measurement-boundary differences, NOT a measured preparation phase) **776,716 / 485,799 ns**. Benchmark source diff vs `1b140d23` attached at `logs/benchmark-instrumentation.diff`. Driver self-tests after the blocks: `python3 -m unittest discover -s .pdca/scripts/perf/tests -p test_t247_json_gate.py` -> rc 0, **33 tests OK** (`logs/t247_driver_tests_recheck.log`). **Outer lock actually used:** `/tmp/opencode/nextorm-bdn-host.lock`. **Notice: host-lock name discrepancy (recorded, NOT silently resolved).** The plan literal is `/tmp/opencode/nextorm-bdn.lock`; collection P.4 pins `/tmp/opencode/nextorm-bdn-host.lock` as canonical and superseding, only the `-host` variant exists on disk, and a holder of `nextorm-bdn.lock` would NOT be mutually excluded — the strictly safer `-host` lock was used; no threshold, tolerance, budget or denominator changed. Artifacts: `.pdca/T247/rv2/{baseline,phase,phases}/**` with the raw BDN exports under each `round-N/<tag>/results/`; logs `.pdca/T247/rv2/logs/{baseline-driver.log,phase-driver.log,phases.log,build-debug.log,build-debug-c99cc512.log,benchmark-instrumentation.diff}`.
**Notice: `NEXTORM_BENCH_FULL=1` job/toolchain discrepancy (recorded, NOT silently resolved).** The plan/brief pin "full `Job.Default` + `InProcessEmitToolchain`", but `benchmarks/nextorm.benchmark/Infrastructure/NextormConfig.cs:23-25` maps `NEXTORM_BENCH_FULL=1` to plain `Job.Default` (out-of-process) and uses `Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance)` only for the quick mode; the public docs (`docs/comparisons/benchmarks.md:305`) document FULL as the full out-of-process job, and the D246 baseline used the same mapping. Every case therefore records `DefaultJob` / toolchain `Default` in its BDN `DisplayInfo`; no job/toolchain modifier was added and no threshold, tolerance, budget, denominator or case was changed or dropped.
