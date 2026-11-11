# #319 — совместимый внутренний descriptor path

**Статус:** полный документ и architect choices одобрены пользователем 2026-11-02 («да»). Следующий разрешённый этап — только implementation planning; реализация не авторизована. Эмпирические gates и неизвестные остаются открытыми.

**Tracking:** #319 https://github.com/AlexeyShirshov/nextorm/issues/319  
**Milestone:** 1.0.9-rc5 https://github.com/AlexeyShirshov/nextorm/milestone/23  
**Контекст ветки:** `branch1.0.9-rc5`, HEAD `ceb6669356657f6499944cd3ff6aab73aaf810bd`. Это лишь контекст; baseline реализации должен быть заново закреплён, а не унаследован от старого T319 `838a6f01`.

## 1. Решение и границы согласия

Пользователь ранее подтвердил следующие ограничения: цель — оптимизация построения запросов движка, а не примеров; публичный API остаётся неизменным; совместимость важнее устранения каждого отдельного command allocation. Позднее полное письменное одобрение всего документа и architect choices дано 2026-11-02: «да» в ответ на «Одобряешь документ для перехода к планированию?». Это одобрение не закрывает эмпирические gates/unknowns и не разрешает реализацию.

Цель — уменьшить аллокации при построении свежих fluent-запросов в `src/**`, сохраняя наблюдаемое runtime-поведение, публичные и protected контракты. Нет требования иметь ровно один объект на оператор или устранять все промежуточные объекты.

Предлагается гибрид: существующее изменяемое состояние builder остаётся авторитетным; откладывается только внутреннее построение повторно составляемого predicate; полный обычный `QueryCommand` создаётся на существующих границах `Select`/`ToCommand` и других API, которые возвращают или потребляют команду.

Тела CTE и существующие `QueryCommand` сохраняются. Не меняются публичные типы/сигнатуры/поля и проверяемые layout gates. Документ не обещает ABI-совместимость произвольного размера или private object layout.

## 2. Исходные свидетельства и пределы выводов

Предыдущая кампания T319 остановлена на предыдущем шаге: `.pdca/status/T319-rc5-1.md:169,193-199`. Это только знание/свидетельство, не принятый результат продукта и не разрешение перезапускать старый PDCA.

#317 («меньше allocation на свежих командах») признан infeasible; #318 (устранить пустые копии) не нашёл устранимых копий. Ни один не landed. Требуется изменить буквальную issue prerequisite и зафиксировать эти исследования как терминальные; нельзя молча считать prerequisite выполненным.

Прежняя цель производительности не была достигнута: T319 cached time ratios 1.0441–1.147. #319 остаётся OPEN, пока не достигнуты исходные hard bars.

`.pdca/CORE-ATTR-01/ledger/PerfPhase.cs` и `RESULTS-ledger.md` описывают process allocation ledger. Общий process total закрывается примерно до 40 B на окно; `Outer` включает вызывающий код, construction, materialization и любую немаркированную работу.

У ledger нет чистого construction subtotal. 115 sampled ticks — не количественная атрибуция байтов. Нулевые Render/command означают отсутствующее отдельное покрытие, а не нулевую работу.

Для `AircraftDelayChains` ledger показывает примерно 63.3–65.6 KB общего allocation и около 16.2 KB prepare. Исторические 20,990.4 B/call Method-A — диагностический paired delta, не универсальная цена вызова.

Не обещать 24 KB экономии на construction или 16 KB на prepare. В `AircraftDelayChains` три `QueryCommand`: два для CTE body `Select` и один финальный `Select`, а не один на каждый fluent node.

Отделять выведенное число от измеренного. Сейчас три builder boundary создают `QueryDefinition` record; их размер и потенциальная экономия не измерены.

Исходные точки для проверки реализации: `EntityBuilder.cs:406-451,547-583,596-609,3185-3252`; `QueryCommand.cs:13-55,249-402`; `DataContextExtensions.cs:20-50`.

## 3. Рассмотренные варианты

**Локальная настройка текущего eager пути.** Низкий риск и допустимая возможность, но #317/#318 исчерпали только конкретные гипотезы, а не всякую tuning-возможность. Любая новая локальная настройка требует собственной измеримой причины.

**Полностью ленивый публичный `QueryCommand`.** Отклоняется: он нарушает или ставит под риск непосредственную изменяемую семантику, включая прямое использование и protected subclass hooks.

**Внутренний гибридный descriptor.** Предпочтительный кандидат для прототипа: value-представление на границе command construction и отложенная композиция только для повторных `Where`.

Не вводить новый plan cache, глобальное состояние, воскресший copy-on-write, per-instance memo как замену выигрыша на свежем вызове, hoist выражений в примерах или record-struct projection edits как продуктовый рычаг.

Не переписывать preparation/SQL rendering, не добавлять source generator, не оптимизировать connection/materializer в рамках этого дизайна. Будущий redesign prepare cache требует отдельного одобренного дизайна.

## 4. Представление и владение состоянием

### 4.1 Builder state

Существующие публичные изменяемые `Sorting`, `Joins`, `Paging` и protected `_joins` остаются eager и авторитетными.

Списки и поля не сигнализируют о мутации. Поэтому запрещены shadow-copy descriptor, угадывание инвалидирования и setter-only tracking.

На command boundary считываются текущие значения, с существующими правилами владения и копирования. Изменения пользователем после branching и до terminal operation должны наблюдаться ровно как в baseline.

### 4.2 Command value

Предложение: внутреннее `readonly` value-представление создания команды; переиспользовать/дополнить `QueryShape`, не вводить публичный тип и не создавать heap `QueryDefinition` на eligible path.

Существующий `QueryShape` — обычный readonly struct, не `ref struct` и не обещание stack-only размещения; сейчас он неполон и не используется.

До общего переключения доказать полноту и parity: все поля, defaults и значения, пересылаемые конструктором `QueryDefinition`, плюс существующие post-constructor source/options. Сохранить scope фильтрации и mapping `IgnoreFilters`, а также advanced options.

Порядок и precedence post-constructor members не менять. Ничего не опускать: `ProjectionType`, `IdentitySlotAliases`, `IsSelectGroup`, `IgnoreFilters`, `GroupByWithTotals`, `LimitBy`, `DistinctOn`, `ExtremeRow`, `TableSample`, `Temporal`, `RowLock`, `PrimarySourceLock`, `NavigationPaths`.

Не boxing-овать value, не хранить snapshots между вызовами и не вводить cross-call cache. Внутренние readonly-ref/signature варианты — решение реализации; копирования крупной структуры измерить по времени.

### 4.3 Только повторный generic Where

Первая целевая группа — повторный generic `EntityBuilder<TEntity>.Where`. Один непустой `Where` хранит lambda напрямую, без descriptor node.

Начиная со второго `Where`, immutable pending predicate append chain удерживает ранее накопленное predicate-представление и текущую lambda вместо создания каждого промежуточного `LambdaExpression`.

Узлы не удерживают parent builder или context. Ветки могут разделять immutable nodes. Кэш materialized lambda локален builder; общий node никогда не записывается.

`Where(null)` на COPY очищает predicate и pending chain только этой копии. Wholesale `Condition` replacement (включая re-rooting и потребителей join/alias) удаляет pending chain на target.

Все внутренние чтения `_condition` идут через один accessor материализации; прямое чтение устаревшего состояния недопустимо. `CopyTo` переносит immutable predicate representation и допустимый local cache; timing/count `CloneImp`/`CopyTo` и dispatch subclass сохраняются.

### 4.4 Материализация, семантика и ограничение цепи

Материализовать при первом внутреннем потреблении и обязательно перед возвратом публичного `QueryCommand`, prepare или SQL inspection.

Повторить точный порядок eager алгоритма: сохранить parameter list первого predicate; использовать тот же `ReplaceParameterExpressionVisitor` (сейчас сопоставление по parameter TYPE — не «исправлять» на identity); тот же левосторонний `Expression.AndAlso` и short-circuit; не менять обработку nested lambda.

Итоговое дерево/shape эквивалентно eager алгоритму. Пропустить можно только отброшенные промежуточные lambda wrappers. Не балансировать, не переупорядочивать и не выполнять partial evaluation.

Предлагаемый предел pending chain — 16 (предложение архитектора, не согласовано пользователем). При достижении материализовать prefix и начать новый bounded сегмент; replay должен быть итеративным или иметь bounded глубину.

Считать стоимость node + lambda + prefix и временных buffers. Если descriptor nodes стоят столько же или дороже сэкономленных wrappers, удалить этот sublever, не оставлять scaffolding.

### 4.5 Eligible типы и неизменяемая команда

Изначально eligible только runtime exact `EntityBuilder<TEntity>`. Custom subclasses, joined/TVF и non-generic пути остаются legacy eager до отдельного доказательства.

Неизвестные типы никогда не получают скрытую ленивость. Измерить стоимость eligibility check, copy и miss-path, включая исключённые пути.

Путь materialized value для одиночного `Where` может принести пользу независимо, если цепочка не проходит gate.

Публичные конструкторы `QueryDefinition` не менять. Не менять 11 protected fields и 109 public members `QueryCommand`, hooks `Clone/CreateSelf/CopyTo`, modifiers, `AsCte`, recursive/set-op поведение.

Не возвращать lazy shell или sidecar вместо обычного `QueryCommand`.

## 5. Граница команды и downstream

Boundary применяет существующее source resolution/options и владение, возвращая полностью заполненный обычный `QueryCommand`.

Planner сохраняет тот же тип и состояние. `CaptureRawShape`, prepared recipe, plan equality, parameter refresh и SQL mapper cache не меняются.

Captured values свежие для каждого вызова; descriptor ограничен живым builder/command. Нет static или per-thread cache.

Не затрагивать temp tables, context filters, shared Any/Count и флаг `Cache`; `storeInCache:false` остаётся локальным механизмом вызова.

Eager fallback для неподдержанных semantics не является скрытым успешным performance arm.

## 6. Прототип: условия допуска к оценке

Это критерии дизайна, не разрешение писать реализацию. В последующем одобренном плане оценить по отдельности command value-path, repeated-Where path, затем комбинацию.

Fresh fluent workload в обоих сравниваемых вариантах; baseline и candidate используют один pinned source, различающийся только предполагаемыми `src` изменениями. Не применять static expression hoisting в candidate.

Считать все descriptor nodes, builder/command shells, временные buffers, construction, materialization и полный terminal total. Не приписывать command-only delta одному этапу без соответствующих измерений.

Удалить sublever, если net allocation не положителен или cached time регрессирует/неопределён после заранее объявленного noise protocol.

Не начинать полный graph или preparation redesign на основании спекулятивной атрибуции. Локальный измеренный выигрыш можно оставить на review, даже если общий target не достигнут.

Оставить #319 OPEN, если не выполнены все AC. Не делать автоматический revert достоверного локального выигрыша только из-за недостижения aspirational ratio; correctness и compatibility regression блокируют продвижение.

Не выводить утверждение «три записи дают 24 KB».

## 7. Acceptance criteria и hard bars

Сохранить смысл исходных AC319-01..04 без ослабления.

**AC319-01 — время:** каждый cached arm, включая `B_Nextorm_Cached_CaseWhen`, не хуже 1.00× same-round baseline, с учётом неопределённости.

**AC319-02 — allocation:** строго меньше там, где применимо; нулевая регрессия на остальных arms.

**AC319-03 — совместимость:** публичный/protected API неизменен, включая E19.

**AC319-04 — проверка:** полный benchmark matrix и все тестовые suites, определённые планом цикла.

Нельзя скрывать регрессию агрегированием или вводить новое разрешённое slowdown. Если шум пересекает 1.00×, результат unverified, не pass.

Финальный repeat budget и статистический oracle должны быть PREDECLARED в implementation plan, не после получения результатов. Нет придуманной цели в bytes.

Первый узкий prototype gate — не полная acceptance. Финальные изменения должны удовлетворять AC4 и всем остальным hard bars.

## 8. Производительность: матрица

Обнаруживать executable inventory, а не кодировать историческое число как истину.

`*SqliteBenchmarkFeaturesFairCached*`: исторически 55 (17 NextORM + 38 comparators). Включить все фактически обнаруженные arms.

`*SqliteBenchmarkWarmDecompose*`: исторически 58, но текущий source содержит 57; перечислить реально обнаруженные cases и объяснить расхождение. Не hardcode устаревшее число.

`--anyCategories=acceptance`: исторически 7; перечислить реально обнаруженные cases.

17 cached NextORM имен: `CaseWhen`, `Contains`, `Cte`, `Distinct`, `Except`, `In_AtIn_Captured`, `In_AtIn_Inline`, `In_ListContains_Captured`, `In_ListContains_Inline`, `Intersect`, `Join4`, `LeftJoin`, `RecursiveCte`, `RowNumber`, `SumOver`, `ToUpper`, `Udf`.

Добавить focused axes для single/repeated `Where`, ветвления, mutable options и custom-derived sentinel. Не подменять arm меньшей нагрузкой.

Исходные методы имеют Iterations 10/100 invocation; не трактовать это как `OperationsPerInvoke`. Нормализовать ровно один раз, хранить raw totals.

PostgreSQL: три примера `DelayPercentilesByModel`, `AircraftDelayChains`, `AirportOnTimePerformance`; ClickHouse: пять `Funnel`, `Sessions`, `RollingActivity`, `SessionDepth`, `Retention`.

Для примеров сохранять тот же caller code, source-pinned engine и paired linq2db, SQL и полное равенство результата на live DB. Это не закрывает автоматически #267–274.

Не заявлять успех для неподдержанного provider, если arm отсутствует. Benchmark project refs к engine и standalone PostgreSQL rc4 package refs различаются: не мерить старый package как candidate engine.

Диагностировать project/reference binding без редактирования source примеров.

## 9. Метод измерения

Для process allocation использовать `GC.GetTotalAllocatedBytes` с async diagnostics. Если применяются маркеры — process-global exclusive/nested учёт. Возможны фоновые provider/runtime allocations; нужны idle windows, повторы и residual.

Thread-local deltas не представляют process total. Закрытие ledger не является attribution. Инструментировать достаточно allocation types, чтобы сравнить node cost и net; не выводить bytes из 115 ticks.

Парные сравнения — same round/process/source/JIT/runtime/hardware/database dataset, по одному provider за раз. Не перекрывать измерения.

DB path: `NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db`. Проверить `DOCKER_HOST` socket `unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`; reuse pinned named DB volumes.

Зафиксировать воспроизводимость baseline против текущего source, raw logs, SQL/results, timing distributions, GC/op. Если commits будут разрешены отдельно — snapshots статуса на commit; сейчас commits запрещены.

## 10. Корректность и совместимость

Проверять `Where` 1/N/null, длинные цепи, short-circuit, nested same-type lambdas, branch и наблюдаемость intermediate builder.

Проверять прямые мутации `Sorting`/`Joins`/`Paging` после branching и до terminal operation.

Проверять derived `CloneImp`/`CopyTo` hooks и возвращаемые типы. Добавить downstream fixture для builder inheritance: существующие repo tests этого не покрывают.

Проверять QueryDefinition против value-path field-by-field, включая advanced options и filters; публичные commands prepared/mutated/cloned остаются прежними.

Покрыть CTE aliases, correlated/recursive CTE callback once, set operations, tuple-IN cardinality changes, temp tables.

Проверить closure refresh и изоляцию cross-context filter/config; shared Any/Count не мутирует sticky `Cache`.

Существующие ориентиры: `T240BuilderConstructionTests`, `T297ProtectedFieldCompatibilityTests`/E19, `T304QueryCommandIsolationTests`, `CachedPreparationRecipeTests`, `TypedCteTests`, `PlanKeyUniquenessTests`.

Только FluentAssertions для assertions. Build с warnings-as-errors. Все 11 unit/dialect projects, включая publicextensibility и alias, и полная live-provider integration suite без пропусков provider как доказательства. Не снижать gates.

Публичные EN/RU docs менять только если поведенческий контракт действительно требует документации; публичные страницы не ссылаются на внутреннюю спецификацию.

## 11. Риски и fallback

Большой struct может удорожить копирование и ухудшить время. Descriptor nodes могут жить дольше и удерживать closures.

Deferred computation переносит работу и исключения к моменту наблюдения. Сохранять observable exception timing; validation, которая сейчас падает раньше, должна остаться eager. Для неразрешённого изменения — eager fallback.

Широкое множество readers `Condition` повышает риск stale reads. Advanced fields/defaults и порядок вызова source methods должны быть точными; source methods выполняются ровно один раз и в том же порядке.

Текущая публичная операция не обещает будущим queries immutable snapshots изменяемых списков. Сохранять фактическое baseline ownership, не усиливать и не ослаблять его.

Никакого нового обещания async/thread-safety.

Любая неподтверждённая семантика направляется в eager путь, а не получает оптимизацию по предположению.

## 12. Одобрение и handoff

Проверка свежих builder boundaries завершена. Полный документ, включая перечисленные architect choices, одобрен пользователем 2026-11-02; точная provenance: «да» в ответ на «Одобряешь документ для перехода к планированию?». Одобрение не является разрешением реализации.

Следующий допустимый этап — только implementation plan. Требуемый `superpowers:writing-plans` skill недоступен в текущей primary session: это blocker handoff; план не создан и skill не заявляется как загруженный. Это не разрешение начать реализацию.

Выход за гибридную границу требует отдельного одобрения пользователя. Эмпирические вопросы (net/time/node retention) решаются только gated prototype по последующему одобренному плану.

Архитекторские choices, submitted for full-document review: value `QueryShape`; отсутствие cache; repeated-Where-only; depth 16; exact-type fallback; неизменность prepare; отсутствие обещания 24 KB.

Tracking amendment #319 выполнен через GitHub CLI: prerequisite #317/#318 заменён описанием terminal investigations; это не возобновляет и не переписывает исторические PDCA записи. Issue #319 остаётся OPEN, milestone 1.0.9-rc5; все gates остаются неизменными.
