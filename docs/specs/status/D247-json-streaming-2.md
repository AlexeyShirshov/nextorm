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
