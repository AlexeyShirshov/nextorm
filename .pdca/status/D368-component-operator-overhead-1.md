# Cycle status: D368-component-operator-overhead / cycle 1

## Current state

- task: `D368-component-operator-overhead` · form: `cycle` · specialized skill: `pdca-coder` + `pdca-dotnet`
- mode: `autonomous`
- cycle: `N=1` · revision: `r=1` · attempt: `n=3/3`
- scope: устранение двух названных в issue #368 накладных расходов компонентных операторов (`RawShape` без копирования `ComponentShape[n]` и аллокации `ComponentShape` на каждый capture; хранение одиночного компонента без одноэлементного `IQueryComponent[]` и без аллокации `ComponentState` на каждый `Select`) в `src/nextorm.core` с подтверждением по протоколу A-B-B-A и сохранением семантики SQL, инвариантов кэша планов и конфигурации покрытия · status path: `.pdca/status/D368-component-operator-overhead-1.md`
- goal: производственный код `nextorm.core`, в котором реализованы два названных в задаче сокращения накладных расходов компонентных операторов, подтверждённый парным измерением A-B-B-A по времени и аллокациям на 10 операторных и 9 контрольных сценариях без ухудшения конфигурации покрытия и проектных инвариантов — §PLAN record и §Original task авторитетны
- phase: `ACT` · record: `completed` · outcome: `incomplete` (STOP) — CHECK r4 terminal after `n=3/3`; AC1/AC3/AC5 `met`, AC2/AC4 `unmet` on the session-4 time numbers; product code unchanged since attempt 2; no PASS, no `done`, no fourth attempt; handoff in §ACT record
- source: GitHub issue #368 (milestone `1.0.9-rc5`, `AlexeyShirshov/nextorm`); пользовательская инструкция «pdca 368 автономно автокомит» · context refs: branch `1.0.9-rc5`, HEAD `9e0117b5fd11f072f05aad693a797f80b345bd8f`, baseline `f991d5ea1081f9598506a37b3fe21deeed23e620`, milestone `1.0.9-rc5`, tracking #362 (D362 status `.pdca/status/D362-selectgroup-extensions-1.md`, замеры `.pdca/D362-selectgroup-extensions/rv1/perf-final2/VERDICT.md`)
- criteria: `AC1–AC5` — точный текст в §PLAN record r1 (источник: эта запись), не перепечатывать
- evidence root: `.pdca/D368-component-operator-overhead/rv1/`

## Original task

**Исходная задача (verbatim, GitHub issue #368, milestone `1.0.9-rc5`):**

Title: Производительность: убрать накладные расходы компонентных операторов (SelectGroup/SelectGroupHaving)
Milestone: 1.0.9-rc5

## Контекст

Поведение `SelectGroup` / `SelectGroupHaving` по #362 (https://github.com/AlexeyShirshov/nextorm/issues/362) **реализовано и принято пользователем** 2026-11-06: перенос в `nextorm.extensions` поверх публичного декларативного механизма компонентов, SQL-семантика и покрытие подтверждены. Измеренные накладные расходы компонентных операторов **приняты** как достаточные для поставки; этот issue — отдельный follow-up на оптимизацию, не блокирующий принятое поведение.

Текущий статус цикла D362: `.pdca/status/D362-selectgroup-extensions-1.md`; замеры: `.pdca/D362-selectgroup-extensions/rv1/perf-final2/VERDICT.md`; внутренний дизайн: `docs/superpowers/specs/2026-11-05-extensible-selectgroup-design.md` (repo path; публичные документы на internal specs не ссылаются).

## Измеренный baseline

Counterbalanced A-B-B-A, SDK 10.0.401, SQLite in-process (baseline `f991d5ea`):

| Сценарий | Δ времени | A/A band |
|---|---:|---:|
| `Warm_RawHit` | +15.0 % | 2.0 % |
| `Prepared_FallbackHit` | +14.4 % | 4.2 % |
| `Prepare_Cold` | +6.5 % | 0.8 % |
| `Construct` | +9.0 % | 4.8 % |
| `Execute_ConstantProjection` | +5.4 % | 3.9 % |
| `Execute_PositionalRebind` | +2.1 % | 1.2 % |

Аллокации: **+9.5…+22.9 % на всех десяти операторных сценариях**. Покомпонентно: **+1272 B** на операцию — экземпляр компонента + один `WithComponent` builder clone (**424 B**) + `Components[]` + `RawShape.ComponentShape[]` + обход при prepare. Контрольные сценарии без компонентов после PF3 уже в пределах собственных A/A band (нулевая дельта).

## Известные конкретные сокращения (ещё не реализованы)

1. `RawShape` должен ссылаться на уже неизменяемый список компонентов, а не копировать `ComponentShape[n]` и не аллоцировать объекты `ComponentShape` на каждый capture (путь capture в `QueryCommand.QueryPreparer`).
2. Хранить один компонент без одноэлементного `IQueryComponent[]` и без аллокации `ComponentState` на каждый `Select` (`EntityBuilder.ApplyCommandOptions`, `QueryCommand.Components`).

## Жёсткий floor

Принятый `WithComponent(...).Select(...)` требует нового неизменяемого builder (`Clone()` — **424 B** измерено) + один компонент (**~48 B**). Net-zero, вероятно, требует компенсирующей экономии в самом `EntityBuilder` — крупный рефакторинг, который должен быть обоснован измерением, а не предположением.

## Acceptance

- Повторить тот же counterbalanced A-B-B-A протокол и per-metric A/A bands.
- Время: в пределах band **или** явно принятый пользователем остаточный разрыв.
- Аллокации: не хуже принятого floor, с применёнными и измеренными двумя сокращениями выше.
- Контрольные сценарии без компонентов: нулевая дельта.
- Без изменения порогов и без новых исключений покрытия.

Refs: #362. Milestone: `1.0.9-rc5` (не переносить в более поздний milestone).

**Инструкция пользователя по режиму и автокоммиту (дословно, не критерий):** «pdca 368 автономно автокомит» (режим AUTONOMOUS, автокоммит разрешён в ветке `1.0.9-rc5` включая ACT-коммит; `git push` КАТЕГОРИЧЕСКИ ЗАПРЕЩЁН; merge запрещены; milestone не менять; трекаемый issue #368).

## PLAN record

### r1

Source: `pdca-planner` (medium tier). План ниже записан дословно; первая строка (`# План PDCA — Issue #368...`) — начало маркера для read-back/hash-match.

````markdown
# План PDCA — Issue #368: Производительность: убрать накладные расходы компонентных операторов (`SelectGroup` / `SelectGroupHaving`)

## 1. Классификация входа, форма и область (Entry classification)

- **Контракт и форма:** `contract: pdca`, `form: cycle` (обычный цикл).
- **Специализированные скиллы (оверлеи):** `pdca-dotnet` (наследует `pdca-coder`) + проектный оверлей `nextorm-pdca`.
- **Обоснование формы (в одну строку):** Метод и объём работ определены и ограничены самой задачей: названы два конкретных сокращения накладных расходов и точки кода (`QueryCommand.QueryPreparer.cs`, `EntityBuilder.cs`, `QueryCommand.cs`), а измерительный базис (counterbalanced A-B-B-A относительно baseline `f991d5ea1081f9598506a37b3fe21deeed23e620` на 10 операторных и 9 контрольных сценариях) зафиксирован.
- **Входная область (Entry scope):** Устранение названных в задаче накладных расходов захвата формы (`RawShape` / `ComponentShape` в `QueryCommand.QueryPreparer`) и хранения одиночного компонента (`EntityBuilder.ApplyCommandOptions`, `QueryCommand.Components`) в `src/nextorm.core` (и связанных точках вызова/тестах в области задачи) с подтверждением по протоколу A-B-B-A и сохранением семантики SQL, инвариантов кэша планов и конфигурации покрытия.
- **Привязка к снимку (Re-anchor):** Репозиторий `/home/alex/sources/nextorm`, ветка `1.0.9-rc5`, `HEAD = 9e0117b5fd11f072f05aad693a797f80b345bd8f` (чистое рабочее дерево); референсный докомпонентный baseline для парного A-B-B-A сравнения — коммит `f991d5ea1081f9598506a37b3fe21deeed23e620`.
- **Привязка доказательств (Evidence location):** Единый корень привязки — рабочее дерево `/home/alex/sources/nextorm` (каталог цикла в `.pdca/`, статус в `.pdca/status/`).

---

## 2. Цель, запрашиваемый и поставляемый результат, критерии приёмки

### Исходная постановка задачи (verbatim, GitHub issue #368, milestone `1.0.9-rc5`)

> **Title:** Производительность: убрать накладные расходы компонентных операторов (SelectGroup/SelectGroupHaving)
> **Milestone:** 1.0.9-rc5
>
> **Контекст**
> Поведение `SelectGroup` / `SelectGroupHaving` по #362 реализовано и принято пользователем 2026-11-06: перенос в `nextorm.extensions` поверх публичного декларативного механизма компонентов, SQL-семантика и покрытие подтверждены. Измеренные накладные расходы компонентных операторов приняты как достаточные для поставки; этот issue — отдельный follow-up на оптимизацию, не блокирующий принятое поведение.
>
> Текущий статус цикла D362: `.pdca/status/D362-selectgroup-extensions-1.md`; замеры: `.pdca/D362-selectgroup-extensions/rv1/perf-final2/VERDICT.md`; внутренний дизайн: `docs/superpowers/specs/2026-11-05-extensible-selectgroup-design.md`.
>
> **Измеренный baseline**
> Counterbalanced A-B-B-A, SDK 10.0.401, SQLite in-process (baseline `f991d5ea`):
> - `Warm_RawHit`: Δ времени +15.0 %, A/A band 2.0 %
> - `Prepared_FallbackHit`: Δ времени +14.4 %, A/A band 4.2 %
> - `Prepare_Cold`: Δ времени +6.5 %, A/A band 0.8 %
> - `Construct`: Δ времени +9.0 %, A/A band 4.8 %
> - `Execute_ConstantProjection`: Δ времени +5.4 %, A/A band 3.9 %
> - `Execute_PositionalRebind`: Δ времени +2.1 %, A/A band 1.2 %
>
> Аллокации: +9.5…+22.9 % на всех десяти операторных сценариях. Покомпонентно: +1272 B на операцию — экземпляр компонента + один `WithComponent` builder clone (424 B) + `Components[]` + `RawShape.ComponentShape[]` + обход при prepare. Контрольные сценарии без компонентов после PF3 уже в пределах собственных A/A band (нулевая дельта).
>
> **Известные конкретные сокращения (ещё не реализованы)**
> 1. `RawShape` должен ссылаться на уже неизменяемый список компонентов, а не копировать `ComponentShape[n]` и не аллоцировать объекты `ComponentShape` на каждый capture (путь capture в `QueryCommand.QueryPreparer`).
> 2. Хранить один компонент без одноэлементного `IQueryComponent[]` и без аллокации `ComponentState` на каждый `Select` (`EntityBuilder.ApplyCommandOptions`, `QueryCommand.Components`).
>
> **Жёсткий floor**
> Принятый `WithComponent(...).Select(...)` требует нового неизменяемого builder (`Clone()` — 424 B измерено) + один компонент (~48 B). Net-zero, вероятно, требует компенсирующей экономии в самом `EntityBuilder` — крупный рефакторинг, который должен быть обоснован измерением, а не предположением.
>
> **Acceptance**
> - Повторить тот же counterbalanced A-B-B-A протокол и per-metric A/A bands.
> - Время: в пределах band или явно принятый пользователем остаточный разрыв.
> - Аллокации: не хуже принятого floor, с применёнными и измеренными двумя сокращениями выше.
> - Контрольные сценарии без компонентов: нулевая дельта.
> - Без изменения порогов и без новых исключений покрытия.

### Запрашиваемый и поставляемый результат

- **Запрашиваемый результат (Requested result):** Производственный код `nextorm.core`, в котором реализованы два названных в задаче сокращения накладных расходов компонентных операторов (`RawShape` без копирования `ComponentShape[n]` и аллокации `ComponentShape` на каждый capture; хранение одиночного компонента без одноэлементного `IQueryComponent[]` и без аллокации `ComponentState` на каждый `Select`), подтверждённый парным измерением A-B-B-A по времени и аллокациям на 10 операторных и 9 контрольных сценариях без ухудшения конфигурации покрытия и проектных инвариантов.
- **Поставляемый результат (Delivered result):** Тот же самый объект — изменения в производственном коде (и затронутых тестах при изменении внутренних сигнатур) с зафиксированными результатами парного измерения A-B-B-A и покрытия в статусе цикла.

### Критерии приёмки (пользовательский набор verbatim + наблюдаемые предикаты)

Пользовательские критерии из секции `## Acceptance` воспроизводятся **дословно** без добавления новых критериев:

- **AC1:** «Повторить тот же counterbalanced A-B-B-A протокол и per-metric A/A bands.»
  - *Наблюдаемое условие:* Замер выполнен в порядке `A1 → B1 → B2 → A2` в одной сессии для всех 10 операторных сценариев `benchmarks/nextorm.benchmark/SelectGroup/D362SelectGroupBenchmarks.cs` (`Construct`, `Prepare_Cold`, `Warm_RawHit`, `Prepared_FallbackHit`, `Execute_Keyed`, `Execute_CompositeKey`, `Execute_GlobalAggregate_WithHaving`, `Execute_ConstantProjection`, `Execute_CapturedCondition`, `Execute_PositionalRebind`) и всех 9 контрольных сценариев `benchmarks/nextorm.benchmark/SelectGroup/D362ControlBenchmarks.cs` (`D362ControlGroupByBenchmarks`: `Prepare_Cold_GroupBy`, `Warm_Hit_GroupBy`, `Execute_GroupBy`; `D362ControlExtremaBenchmarks`: `Prepare_Cold_SelectWhereMin`, `Warm_Hit_SelectWhereMin`, `Prepare_Cold_SelectWhereMax`, `Warm_Hit_SelectWhereMax`, `Execute_SelectWhereMin`, `Execute_SelectWhereMax`), при этом `Band = max(|A1 − A2| / A1, |B1 − B2| / B1)` вычислен индивидуально для каждого из 19 сценариев и отдельно для времени и памяти.
  - *Негативное условие:* Пропуск любого из 4 плеч `A1, B1, B2, A2` или любого из 19 сценариев, подмена baseline `f991d5ea1081f9598506a37b3fe21deeed23e620`, либо использование константного/нулевого band по умолчанию вместо расчёта из пары `A1/A2` и `B1/B2` не допускаются.

- **AC2:** «Время: в пределах band или явно принятый пользователем остаточный разрыв.»
  - *Наблюдаемое условие:* Для каждого из 10 операторных сценариев `D362SelectGroupBenchmarks` относительная дельта времени `ΔTime = (B − A) / A` относительно baseline `f991d5ea1081f9598506a37b3fe21deeed23e620` удовлетворяет:
    1. условию `ΔTime ≤ Band_Time` данного сценария, **или**
    2. (ветка уже зафиксированного пользовательского принятия D362 от 2026-11-06 в автономном режиме, где нет интерактивного согласования нового остатка) на сценариях, для которых пользователь в D362 (`perf-final2/VERDICT.md`, таблица в #368) принял остаточный разрыв (`Warm_RawHit` +15.0 %, `Prepared_FallbackHit` +14.4 %, `Prepare_Cold` +6.5 %, `Construct` +9.0 %, `Execute_ConstantProjection` +5.4 %, `Execute_PositionalRebind` +2.1 %), `ΔTime` не превышает принятый в D362 уровень с учётом полосы шума (`ΔTime ≤ ΔTime_D362_accepted + Band_Time`).
  - *Негативное условие:* Выход за `Band_Time` любого из 4 сценариев (`Execute_Keyed`, `Execute_CompositeKey`, `Execute_GlobalAggregate_WithHaving`, `Execute_CapturedCondition`), бывших в пределах band в D362, либо превышение принятого в D362 уровня на остальных 6 сценариях, либо объявление в автономном режиме нового (не зафиксированного в D362/#368) остаточного разрыва «принятым пользователем» не допускаются.

- **AC3:** «Аллокации: не хуже принятого floor, с применёнными и измеренными двумя сокращениями выше.»
  - *Наблюдаемое условие:*
    1. Оба названных в задаче сокращения структурно присутствуют в коде (наблюдаемо в CHECK):
       - Сокращение 1: `RawShape` ссылается на уже неизменяемый список/представление компонентов и не копирует `ComponentShape[n]` и не аллоцирует объекты `ComponentShape` на каждый capture в `QueryCommand.QueryPreparer`;
       - Сокращение 2: одиночный компонент хранится и передаётся без одноэлементного массива `IQueryComponent[]` и без аллокации `ComponentState` на каждый `Select` (`EntityBuilder.ApplyCommandOptions`, `QueryCommand.Components`).
    2. Принятый floor задан самой задачей как структурный floor цепочки `WithComponent(...).Select(...)` (`EntityBuilder.Clone()` 424 B измерено + один компонент ~48 B поверх плеча `A`, и не хуже принятого в D362 уровня `perf-final2/VERDICT.md`). Измеренное по тому же протоколу A-B-B-A значение `Allocated` (`B/op`) на путях, задействующих `ApplyCommandOptions` и `CaptureRawShape`, ниже зафиксированного в D362 `B/op` с учётом пометрического `Band_Alloc` как меры шума измерения, а ни на одном из 10 операторных сценариев дельта аллокаций не ухудшает принятый floor с учётом `Band_Alloc`.
  - *Негативное условие:* Отсутствие любого из двух сокращений в коде, сохранение аллокаций `ComponentShape[n]`/`ComponentShape` на каждый capture или `IQueryComponent[1]`/`ComponentState` на каждый `Select` при одиночном компоненте, либо отсутствие измеримого снижения `B/op` относительно D362 на затронутых путях (с учётом `Band_Alloc`) не допускаются.

- **AC4:** «Контрольные сценарии без компонентов: нулевая дельта.»
  - *Наблюдаемое условие:* На всех 9 контрольных сценариях `D362ControlBenchmarks` (`Prepare_Cold_GroupBy`, `Warm_Hit_GroupBy`, `Execute_GroupBy`, `Prepare_Cold_SelectWhereMin`, `Warm_Hit_SelectWhereMin`, `Prepare_Cold_SelectWhereMax`, `Warm_Hit_SelectWhereMax`, `Execute_SelectWhereMin`, `Execute_SelectWhereMax`) относительно baseline `f991d5ea1081f9598506a37b3fe21deeed23e620` дельты времени и памяти находятся в пределах собственных полос шума (`ΔTime ≤ Band_Time` и `ΔAlloc ≤ Band_Alloc`) без добавления новых аллокаций на пути без компонентов.
  - *Негативное условие:* Превышение собственной полосы шума (`ΔTime > Band_Time` или `ΔAlloc > Band_Alloc`) на любом из 9 контрольных сценариев или появление дополнительных аллокаций на пути без компонентов является нарушением критерия.

- **AC5:** «Без изменения порогов и без новых исключений покрытия.»
  - *Наблюдаемое условие:* Конфигурация порогов и фильтров в `coverage.settings.xml` (`MIN_LINE_COVERAGE=85`, `MIN_BRANCH_COVERAGE=75`), `coverage.integration.settings.xml` (`MIN_INTEGRATION_LINE_COVERAGE=65`, `MIN_INTEGRATION_BRANCH_COVERAGE=75`, `-classfilters:"-NextORM.Core.InMemory*"`) и `.github/workflows/dotnet.yml` остаётся неизменной; в код не добавлены новые атрибуты `[ExcludeFromCodeCoverage]` или фильтры классов; измеренные показатели line/branch coverage зафиксированы и сопоставлены с порогами `85/75` (решение) и `65/75` (интеграционный пакет; hard-fail на `main` является политикой CI, цикл фиксирует измеренные числа и неизменность конфигурации).
  - *Негативное условие:* Любое изменение порогов или фильтров в `coverage.settings.xml`, `coverage.integration.settings.xml` или `.github/workflows/dotnet.yml`, либо добавление новых `[ExcludeFromCodeCoverage]` запрещено.

---

## 3. Метод измерения (Measurement method)

- **Метрики и единицы измерения:**
  1. Время операции: `Mean` (`µs/op` или `ns/op` в единой шкале).
  2. Аллокации управляемой памяти на операцию: `Allocated` (`B/op`, `[MemoryDiagnoser]`).
  3. Покрытие кода: `Line coverage (%)` и `Branch coverage (%)` по Cobertura-отчёту.
- **Знаменатель (Denominator) и базис сравнения:**
  - Плечо `A` (Baseline): коммит `f991d5ea1081f9598506a37b3fe21deeed23e620` (докомпонентная реализация).
  - Плечо `B` (Candidate): текущая ветка `1.0.9-rc5` с применёнными изменениями.
  - Порядок прогона внутри одной сессии на каждый набор сценариев: `A1 → B1 → B2 → A2`.
  - Формулы агрегации:
    - `A = (A1 + A2) / 2`, `B = (B1 + B2) / 2`
    - Относительная дельта: `Δ = (B − A) / A` (в `%`)
    - Абсолютная дельта аллокаций: `ΔAlloc_B = B_alloc − A_alloc` (в `B/op`)
    - Пометрическая полоса шума сценария: `Band = max(|A1 − A2| / A1, |B1 − B2| / B1)` (вычисляется отдельно для времени и памяти по каждому из 19 сценариев).
- **Фильтры включения/исключения (ровно 19 сценариев):**
  - **10 операторных сценариев** (`benchmarks/nextorm.benchmark/SelectGroup/D362SelectGroupBenchmarks.cs`):
    1. `Construct` (`:49`)
    2. `Prepare_Cold` (`:57`)
    3. `Warm_RawHit` (`:66`)
    4. `Prepared_FallbackHit` (`:74`)
    5. `Execute_Keyed` (`:82`)
    6. `Execute_CompositeKey` (`:85`)
    7. `Execute_GlobalAggregate_WithHaving` (`:88`)
    8. `Execute_ConstantProjection` (`:91`)
    9. `Execute_CapturedCondition` (`:94`)
    10. `Execute_PositionalRebind` (`:101`)
  - **9 контрольных сценариев** (`benchmarks/nextorm.benchmark/SelectGroup/D362ControlBenchmarks.cs`):
    - `D362ControlGroupByBenchmarks`:
      1. `Prepare_Cold_GroupBy` (`:34`)
      2. `Warm_Hit_GroupBy` (`:43`)
      3. `Execute_GroupBy` (`:51`)
    - `D362ControlExtremaBenchmarks`:
      4. `Prepare_Cold_SelectWhereMin` (`:75`)
      5. `Warm_Hit_SelectWhereMin` (`:84`)
      6. `Prepare_Cold_SelectWhereMax` (`:92`)
      7. `Warm_Hit_SelectWhereMax` (`:101`)
      8. `Execute_SelectWhereMin` (`:109`)
      9. `Execute_SelectWhereMax` (`:114`)
  - **Покрытие:** по `coverage.settings.xml` (`nextorm.{core,sqlite,postgres,sqlserver}`) и `coverage.integration.settings.xml` (все провайдерные сборки, на которые ссылается `tests/nextorm.integration.tests`, со штатным фильтром `-classfilters:"-NextORM.Core.InMemory*"`).
- **Условия сопоставимости (Comparability conditions):**
  - Одинаковый хост, ОС, .NET SDK `10.0.401` / runtime `10.0.12`, конфигурация `Release`, `InProcessEmitToolchain`, `Job.ShortRun` (launch 1, warmup 3, iterations 3), `[MemoryDiagnoser]`, SQLite фикстура на 1000 строк (`id`, `score = i % 50`, `category = 'c' + (i % 10)`), последовательный прогон `A1 → B1 → B2 → A2` в одной сессии без конкурирующей нагрузки и при подтверждённой идентичности порождаемого SQL (`D362SqlParity`).
- **Пороги и их происхождение (Provenance):**
  - Пороги для 10 операторных и 9 контрольных сценариев происходят непосредственно из задачи (#368) и протокола D362 (`perf-final2/PROTOCOL.md` v2, `perf-final2/VERDICT.md`):
    - Контрольные сценарии (9 шт.): `ΔTime ≤ Band_Time`, `ΔAlloc ≤ Band_Alloc` (нулевая дельта).
    - Операторные сценарии — аллокации (10 шт.): структурный floor из задачи (`Clone()` 424 B + один компонент ~48 B поверх плеча `A`, и не хуже принятого в D362 уровня) при применённых обоих сокращениях и измеренном снижении `B/op` ниже уровня D362 на затронутых путях с учётом `Band_Alloc`.
    - Операторные сценарии — время (10 шт.): `ΔTime ≤ Band_Time` либо не хуже принятого пользователем в D362 остаточного разрыва (`perf-final2/VERDICT.md`) с учётом `Band_Time`.
  - Проектная перф-приёмка закэшированного пути (`nextorm-pdca` §Перф-приёмка, `docs/specs/performance/acceptance-benchmarks.md:88-95`): референсное отношение cached/prepared составляет `1.87` по времени и `7.42` по аллокациям; по правилу проекта рост сопоставимого отношения более чем на 20 % относительно baseline является основанием для расследования и записи результата (а не автоматическим hard-fail).
  - Пороги покрытия происходят из `.github/workflows/dotnet.yml`, `coverage.settings.xml` (`85` line / `75` branch) и `coverage.integration.settings.xml` (`65` line / `75` branch).

---

## 4. Ограничения и допущения (Constraints & Assumptions)

### Ограничения
1. **Запрещённые побочные эффекты и проектные инварианты (`nextorm-pdca` 1–12):**
   - Запрещено изменять публичный API и семантику SQL (`SelectGroup`, `SelectGroupHaving`, `ExtremeRowExtensions`, `IQueryComponent`, `IQueryComponentShape`).
   - Запрещено мутировать разделяемые экземпляры `QueryCommand` (`QueryCache.AnyCommand` и др.) и выставлять sticky-флаг `_dontCache` (`queryCommand.Cache = false`).
   - Закэшированный в `QueryPlanStore` экземпляр `RawShape` и состояние компонентов не должны разделять мутабельное состояние с последующими запросами.
   - На пути без компонентов (`_componentState is null`) запрещены новые аллокации и отключение кэша планов.
   - `TreatWarningsAsErrors=true` во всех проектах и конфигурациях; окончания строк `CRLF`; в тестах — только `FluentAssertions` (кроме `Assert.Skip*`); соблюдение `AssertionStyleTests` и `IntegrationSuiteScopingTests`.
   - Запрещено менять пороги и фильтры покрытия или добавлять `[ExcludeFromCodeCoverage]`.
2. **Git и режим работы:**
   - Режим **AUTONOMOUS** с разрешённым автокоммитом в текущей ветке `1.0.9-rc5` и закрытием `gh issue close 368` при успешном завершении цикла (`done`).
   - **`git push` КАТЕГОРИЧЕСКИ ЗАПРЕЩЁН** (пушит только пользователь).
   - Milestone `1.0.9-rc5` менять запрещено.
3. **Ресурсные и средовые ограничения:**
   - Дерево baseline `/tmp/nextorm-baseline-f991d5ea` (или checkout коммита `f991d5ea1081f9598506a37b3fe21deeed23e620`) используется как плечо `A`.
   - При запуске контейнеризованных интеграционных тестов требуется `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`.

### Допущения
- Внутренние члены `RawShape`, `ComponentShape` и `ComponentState` не входят в публичный контракт библиотеки; при изменении их внутренних сигнатур в рамках двух сокращений DO вправе синхронно адаптировать внутренние модульные тесты, сохраняя проверяемые ими ветки и покрытие.

---

## 5. Закрытие пробелов (Gaps)

| Пробел в постановке | Способ закрытия | Решение / Источник |
|---|---|---|
| Требуется ли для приёмки #368 компенсирующий рефакторинг `EntityBuilder.Clone()` (424 B)? | **Факты из задачи (#368 «Жёсткий floor» и `AC3`)** | Нет: задача фиксирует `Clone()` (424 B) + 1 компонент (~48 B) как принятый жёсткий floor и требует реализации и измерения двух конкретных сокращений не хуже этого floor. |
| Как трактуется «явно принятый пользователем остаточный разрыв» в `AC2` при автономном прогоне? | **Факты из задачи (#368 и запись D362)** | Относится только к уже зафиксированному пользовательскому принятию D362 от 2026-11-06 (`perf-final2/VERDICT.md`, таблица в #368). Новые разрывы сверх принятых в D362 в автономном режиме не могут быть приняты. |
| Как проверяется применение двух сокращений в `AC3`? | **Факты из формулировки `AC3`** | Совместно: структурным присутствием обоих сокращений в коде и измеренным снижением `B/op` ниже уровня D362 по протоколу A-B-B-A с учётом `Band_Alloc`. |

---

## 6. Минимальное решение и альтернативы (Minimal solution & Alternatives)

- Конкретные структуры данных, внутреннее представление и декомпозицию единиц выполнения **определяет DO** в пределах требований `AC3` и ограничений секции 4:
  1. Убрать копирование `ComponentShape[n]` и аллокацию объектов `ComponentShape` на каждый capture в `QueryCommand.QueryPreparer` (`RawShape` ссылается на уже неизменяемый список/представление компонентов).
  2. Убрать аллокацию одноэлементного массива `IQueryComponent[]` и аллокацию `ComponentState` на каждый `Select` при хранении одного компонента (`EntityBuilder.ApplyCommandOptions`, `QueryCommand.Components`).

---

## 7. Handoff в фазу DO (Plan-to-DO handoff)

Фаза DO получает по ссылке:
- **Исходную задачу verbatim:** GitHub issue #368 (milestone `1.0.9-rc5`), секция 2 выше.
- **Критерии приёмки и метод измерения:** `AC1`–`AC5` (секция 2) и спецификация метода измерения (секция 3).
- **Область кода (Code scope):**
  - `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` (`:4075` `RawShape`, `:4135` `Components`, `:4145` `ComponentShape`, `:4167` `ComponentsMatch`, `:4411-4566` `CaptureRawShape`, `:4779-4787`, `:4931-4942`);
  - `src/nextorm.core/Query/QueryCommand.cs` (`:698-809` `Components`, `PreparedComponents`, `ComponentState`, `_componentState`);
  - `src/nextorm.core/Builders/EntityBuilder.cs` (`:39-44` `_componentState`, `:430-455` `ApplyCommandOptions`, `:3158-3280` `WithComponent`, `Clone`, `CopyTo`);
  - сопряжённые точки обращения к компонентам и внутренние тесты (`src/nextorm.core/DataContext/QueryPlanner.cs`, `src/nextorm.extensions/SelectGroupComponent.cs`, `tests/**`).
- **Обязательные запреты:**
  - Никаких изменений публичного API и SQL-семантики.
  - Никакой мутации разделяемых `QueryCommand` и никакого выставления sticky-флага `Cache = false`.
  - Никакого разделения мутабельного состояния внутри закэшированного `RawShape`.
  - Никаких новых аллокаций на пути без компонентов (`_componentState is null`).
  - Никакого изменения порогов покрытия или добавления `[ExcludeFromCodeCoverage]` / фильтров классов.
  - Никакого `git push`.

---

## 8. Риски и условия остановки (Risks & Stop conditions)

### Риски
1. **Нарушение неизменяемости закэшированного `RawShape`:** Если `RawShape` в кэше планов сохранит ссылку на коллекцию компонентов, которая впоследствии мутируется при добавлении новых компонентов в производный билдер, ключи в `QueryPlanStore` будут повреждены. Требование к результату: представление компонентов, на которое ссылается `RawShape`, должно быть строго неизменяемым после захвата.
2. **Потеря покрытия веток в существующих unit-тестах `RawShape`:** Внутренние тесты (`RawShapeBranchCoverage2Tests`, `SqlitePlanCacheShapeCoverageTests`, `PreparationBranchCoverage3Tests` и др.) проверяют ветки сопоставления компонентов в `RawShape`. Требование к результату: сохранить полноту тестового покрытия веток без добавления исключений покрытия.
3. **Межсессионный шум при замерах времени:** Параллельная нагрузка во время бенчмарков может исказить `Band_Time`. Требование к результату: прогон `A1 → B1 → B2 → A2` выполняется изолированно в одной сессии с расчётом индивидуальных `Band` по протоколу.

### Условия остановки (Stop conditions)
- **Успешное завершение (`done`):** Все критерии `AC1`–`AC5` подтверждены независимым CHECK; изменения закоммичены в ветку `1.0.9-rc5`, issue #368 закрыт (`gh issue close 368`), без `git push`.
- **Исчерпание бюджета попыток (`n = 3/3`):** Если за 3 попытки DO→CHECK в текущей ревизии плана не достигнуто выполнение всех `AC1`–`AC5` — эскалация (`pdca-escalate`) или терминальный исход `incomplete / STOP` с записью фактических результатов и истории дефектов.
- **Выявление недостижимости критерия при заданных ограничениях:** Возврат из CHECK к PLAN с доказательствами для ревизии контракта или терминального исхода.

---

## 9. Уверенность и открытые допущения (Confidence & Gaps)

- **Проверено по фактам (Verified):**
  - Точки кода для обоих сокращений локализованы (`QueryCommand.QueryPreparer.cs:4135,4145,4430-4443`, `QueryCommand.cs:698-809`, `EntityBuilder.cs:39-44,447-455`).
  - Полный список из 10 операторных сценариев (`D362SelectGroupBenchmarks.cs`) и 9 контрольных сценариев (`D362ControlBenchmarks.cs`), а также baseline `f991d5ea1081f9598506a37b3fe21deeed23e620` и протокол A-B-B-A верифицированы.
- **Остаётся допущением до проверки в DO/CHECK:**
  - Точные численные значения `ΔTime` и `ΔAlloc` после применения двух сокращений будут определены парным контрбалансированным замером A-B-B-A.
````

## Approval record

`not applicable: autonomous mode`

## DO result

Stages 1–4 completed. Correction of the line that stood here: the A-B-B-A benchmark (stage 2) DID run in the same cycle before the host restart; the stage-1 and stage-2a paragraphs below are kept verbatim. Stage 3 (valid whole-suite + coverage) and stage 4 (frozen manifest + evidence gates) were finished afterwards by the flat-primary fallback.

- Reduction 1 (`RawShape` capture): `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` — `RawShape.Components` is now `IReadOnlyList<IQueryComponent>?` referencing the already-immutable component list instead of `ComponentShape[]?` copied per capture; `CaptureRawShape` validates `IQueryComponentShape` and stores `cmd.Components` directly; `ComponentsMatch` reads through `IQueryComponentShape`; fingerprint/scan sites iterate via `ShapeSelectors`. `ComponentShape` nested class retained.
- Reduction 2 (single component): `src/nextorm.core/Query/QueryCommand.cs` — flattened `ComponentState` holder into direct fields (`_components` polymorphic single/list, `_componentsView`, `_preparedComponents`, `_preparedComponentsView`, `_componentRenderShape`); added `SetComponents(object?)` for allocation-free forwarding; `Components`/`PreparedComponents`/`ComponentRenderShape` properties updated. `src/nextorm.core/Builders/EntityBuilder.cs` `ApplyCommandOptions` now calls `cmd.SetComponents(...)` (no one-element `IQueryComponent[]`, no holder allocation). `src/nextorm.core/Query/QueryCommand.Clone.cs` updated to share fields and clear render shape.
- Build: `dotnet build nextorm.slnx -c Debug` → 0 Warning(s) 0 Error(s) (log `.pdca/D368-component-operator-overhead/rv1/build/build.log`).
- Inner-loop tests: core 3915/0/0 skipped 0; sqlite 2306/0/1 skipped; extensions 154/0/1 skipped; querycomponents 63/0/0; postgres 1946/0/0; sqlserver 2076/0/8 skipped; mysql 1215/0/0; mariadb 1058/0/0; clickhouse 1505/0/0 — all exit 0.
- Structural alloc probe (in-process `GC.GetAllocatedBytesForCurrentThread`, component-bearing `NoopComponent`, 2000 iters): construct_per_call=2568.00 B, capture_per_call=3232.00 B (numbers `.pdca/.../rv1/alloc-probe/numbers.txt`). Component-free path unchanged (no new allocations on `_componentState is null`).

Stage 2a (A-B-B-A measurement) completed:
- Baseline identity: `/tmp/nextorm-baseline-f991d5ea` @ `f991d5ea1081f9598506a37b3fe21deeed23e620`; candidate identity patch sha256 `af5750a3e975d5bb1c00bd6d074aebc464594877c901bedc8265ece1ea1d95f6` (`frozen/candidate.diff`, `frozen/touched-files.sha256`).
- Both arms build Release 0/0. Run order A1→B1→B2→A2 for operator set and control set, all exit 0 (logs `runs/*-operator.log`, `runs/*-controls.log`).
- SQL parity: identical (empty diff, `parity.diff` 0 lines).
- D362 alloc probe: baseline list_object=32 capture=4848.00 construct=4016.00 warmhit=5224.00; candidate list_object=32 capture=4784.00 construct=4056.00 warmhit=5160.00 (see `alloc-baseline.txt`, `alloc-candidate.txt`).
- Computed table: `perf/TABLES.md`, raw data `perf/measurement.json`; protocol `perf/PROTOCOL.md` (SDK 10.0.401, runtime 10.0.12).
- Evidence helpers verified byte-identical to `/home/alex/.config/opencode/skills/pdca-dotnet/scripts/` (sha256 recorded).

Stage 3 (valid whole-suite + coverage, re-gather — the earlier `coverage/solution.log` run is not evidence):
- Previous coverage run invalid: `DOCKER_HOST` was unset, so `tests/nextorm.extensions.integration.tests` failed closed against `unix:///var/run/docker.sock` and one SQL Server test hit a pre-login handshake timeout; exit code 2.
- Re-run with `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`, `DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0`, after `dotnet build nextorm.slnx -c Debug` (0 warnings / 0 errors): whole suite 23 419 total / 23 211 succeeded / 1 failed / 207 skipped, 10 m 17 s, exit 2 (`coverage/solution2.log`).
- The single failure is a pre-existing order-dependent assertion, not the change: `T300ScalarDerivedSourceLiveMatrix.NullableScalarMaterialize_Live(providerName: "clickhouse")` compares a `UnionAll` result with `Should().Equal(1, 2, 2, 3)` while the query has no `ORDER BY`; the observed sequence was `{2, 3, 1, 2}`. The test source is untouched by this cycle (last change `12451047`, 2026-10-26). Isolated re-run of the same method: 5 total / 0 failed / 0 skipped, exit 0.
- Coverage (reportgenerator over `coverage/solution2.cobertura.xml`): solution **89.3 % line / 83.7 % branch** against 85 / 75; integration gate run `coverage/integration2.log` 8 659 total / 8 462 succeeded / 0 failed / 197 skipped, exit 0, coverage **80.3 % line / 74.7 % branch** against 65 / 75 (integration branch is below 75 exactly as on CI for `9e0117b5`; hard-fails on `main` only — recorded, not waived).
- Thresholds and filters unchanged: `coverage.settings.xml`, `coverage.integration.settings.xml` and `.github/workflows/dotnet.yml` are untouched; the candidate diff is the four footprint files only, and no `[ExcludeFromCodeCoverage]` or class filter was added.

Stage 4 (frozen evidence manifest):
- `.pdca/D368-component-operator-overhead/rv1/manifest.json` — tree `7fff06fd…` recomputed from `base_ref` `9e0117b5` + the four-file footprint; rows AC1–AC5; AC2 recorded `open` with its reason (two scenarios outside their own band), the other rows `met`.
- `python3 .pdca/scripts/validate_inner_loop.py manifest …` → rc 0; `python3 .pdca/scripts/pdca_evidence.py lint --repo . --manifest …` → rc 0.

### Attempt 2 (after CHECK r1 = `not accepted → DO`)

CHECK r1 findings answered here: `D368-C1-time` (two operator time deltas outside band), `D368-C2-single-array` (the `Components` getter still built `new[] { single }`), `D368-C3-red-suite` (ClickHouse order flake in the whole-suite run), `D368-C4-audit-gap` (four mandatory audit skills could not be loaded by CHECK), `D368-C5-branch-gap` (no branch delta / missing-branch list).

- C2 code change: component state is now a `QueryCommand.ComponentSet` readonly struct (none / one / immutable list) stored in the command and carried by `RawShape` by value — no one-element array on any path. The single-component view is a `SingleComponentList` object created only on the first read of the public `Components` property, never on `Select`, capture, comparison or fingerprint. The capture path uses `ComponentsState`, so it materialises no view at all. The dead `RawShape.ComponentShape` class is deleted. Files: `QueryCommand.cs`, `QueryCommand.QueryPreparer.cs` (clone/EntityBuilder unchanged from attempt 1).
- Build: `dotnet build nextorm.slnx -c Debug` → 0 Warning(s) / 0 Error(s) (1 m 32 s). Inner-loop suites after the change: core 3915, sqlite 2306 (1 skip), extensions 154 (1 skip), querycomponents 63, postgres 1946, sqlserver 2076 (8 skips), mysql 1215, mariadb 1058, clickhouse 1505 — all exit 0.
- C1/C3/C5 measurement and suite evidence (see the manifest for the authoritative row status):
  - Attempt 2 of the A-B-B-A was discarded as contaminated: load average 24 during the session; a 627 µs baseline arm against a 337 µs candidate arm on the same method.
  - Two further sessions were run on a quiet host. Session 3 (`attempt3/`): all 10 operator methods satisfy AC2 (`Prepare_Cold` +4.90 % vs band 2.61 % and `Prepared_FallbackHit` +6.46 % vs 3.84 % pass through the D362-accepted-residual branch; `Execute_CapturedCondition` −32.59 %, `Execute_Keyed` −12.20 %). Session 4 (`attempt4/`): `Execute_CapturedCondition` +12.48 % vs band 8.26 % and `Execute_Keyed` +2.64 % vs 0.69 % fail, and two controls leave their band by 1–2 pp.
  - Attempt 1 (`runs/`) failed AC2 on `Execute_GlobalAggregate_WithHaving` (+2.79 % vs 0.80 %) and `Execute_CapturedCondition` (+2.07 % vs 0.90 %). Three valid sessions therefore disagree on identical code, and the recorded AC2/AC4 rows stay `open` rather than being declared met.
  - Allocations are consistent across sessions and below the D362 level on all ten operator methods: e.g. `Construct` 3533 B/op vs 3640, `Warm_RawHit` 4557 vs 4864, `Execute_ConstantProjection` 3871 vs 4127, `Execute_PositionalRebind` 16548 vs 16932 (candidate B/op below D362 B/op by 100–400 B, i.e. outside the measured allocation bands).
  - SQL parity on the nine scenarios: identical to the baseline apart from the process/time header (`attempt3/parity.diff` — one header line).
  - Structural alloc probe (`--d362-alloc-probe`): baseline `capture 4848 / construct 4016 / warmhit 5224`, candidate `4784 / 4056 / 5160`.
- C3 suite: a clean whole-suite run with `DOCKER_HOST` and the reused containers purged — 23 419 total / 23 212 succeeded / **0 failed** / 207 skipped, exit 0 (`coverage3/solution3.log`). Coverage solution 89.3 % line / 83.8 % branch (85/75); integration gate run 8 659 / 0 failed / 197 skipped, exit 0, 80.3 % / 74.7 % (65/75). Two intermediate runs were invalid and are kept as evidence of the environment failure: `coverage3/solution.log` (129 failures, MariaDB/MySQL seeding races) and `coverage3/solution2.log` (3 644 failures after the ClickHouse and SQL Server containers died — exit 137, memory pressure); the stale reused containers were removed before the clean run.
- C5 branch delta: `coverage3/branch-delta.md` — per-file branch rates (EntityBuilder 85.8 %, QueryCommand 81.7 %, QueryCommand.Clone 100.0 %, QueryCommand.QueryPreparer 89.3 %) and the missing-branch list restricted to the changed lines: `QueryCommand.cs:783`, `QueryCommand.QueryPreparer.cs:4161, 4167, 4170, 4767, 4925`; all other changed lines are fully branch-covered.
- Manifest refreshed (tree `a9f56018…`, rows AC1 `met`, AC3 `met`, AC5 `met`, AC2/AC4 `open` with the cross-session reasons); `validate_inner_loop.py manifest` rc 0 and `pdca_evidence.py lint` rc 0.
- C4 audit gap: the four audit skills are still unavailable to `pdca-check` (permission denied); the checklists must be supplied inline by the driver in the next CHECK dispatch instead of widening CHECK's permissions.

### Evidence round after CHECK#2 (no product change; counter stays at n=2/3)

CHECK#2 rejected again on the session-4 time numbers and asked for attribution rather than another "successful" run. The product was not changed; this round adds two controls that measure the statistic itself.

- **A/A control** (`aacontrol/`, four arms, all of them the candidate binary `a9f56018…`): the same estimator on identical code passes **19/19** comparisons; deltas range −4.87…+3.86 % (time) and −1.57…+1.38 % (allocations), with bands 1.38–11.27 %. A pristine A/A session therefore does not manufacture a failure.
- **Cross-session reproducibility probe** (`aacontrol/REPRODUCIBILITY.md`): session-3 B arms treated as `A` and session-4 B arms as `B` — identical code on both sides — gives **1 violation out of 38** comparisons: `Execute_CapturedCondition` time +8.66 % vs band 8.26 %; the allocation metrics are identical between the sessions except for that method's 16 691 → 16 732 B/op. That single violation is on the same method the session-4 A/B comparison flags, so the probe does not dismiss the session-4 violations as noise.
- Consequence for the record: `D368-C1-time` and `D368-C6-control-time` stay real findings on the session-4 numbers; the remaining time overhead is the accepted structural floor (`EntityBuilder.Clone()` 424 B + one component), and no further product reduction was identified for the `Execute_*` scenarios, where the candidate's allocation advantage is already real (all ten below the D362 level) and the time delta changes sign between sessions (−32.59 % / +12.48 % on `Execute_CapturedCondition` for the same binary).
- `D368-C7-process-record` fixed in this same update: counter refreshed, defect history filled, `perf/PROTOCOL.md` corrected to the one-sided `Δ ≤ Band`.
- `D368-C5-branch-gap` not re-gathered in r1 (CHECK#2 forbade a second evidence round for it and routed the residual to escalation): the candidate per-file branch counts and the changed-line missing-branch list exist, but there is no pre-change baseline coverage run to subtract.

## Expert review (escalation) and the third attempt

CHECK#3 routed the acceptance-risk question to `pdca-escalate`; the expert answered on the attribution question.

- **Conclusion (forwarded without re-decision):** the residual time overhead cannot be attributed to the candidate code under the prescribed method; no real implementation defect that a third product attempt could fix was identified. Sign flip on identical code (`Execute_CapturedCondition` −32.59 % in session 3, +12.48 % in session 4), component-free controls moving +2.5…+3.2 % in one session with an unchanged code path, and the implausibility of a +12 % (~34 µs) effect from one flat-set pass are the direct reasons. DO must not change product code; if the third attempt is used it must be non-product work: obtain the pre-change branch baseline (closes `D368-C5`) and record the inter-session dispersion. Residual risk: a real effect ≤ ~2 % cannot be excluded without CPU pinning or an isolated host. Only CHECK can decide the verdict; a criterion revision or a new accepted residual is the user's decision, not the cycle's.
- **Third attempt (non-product):** the pre-change branch baseline was measured on a clean worktree at `9e0117b5` with the identical suite command and `coverage.settings.xml` — 23 419 tests, 0 failed, 208 skipped, 89.3 % line / 83.8 % branch (42 292 of 50 447). Before/after for the touched files (`coverage3/branch-delta.md`): `EntityBuilder` 85.8 → 85.8 %, `QueryCommand` 82.7 → 81.7 %, `QueryCommand.Clone` 100.0 → 100.0 %, `QueryCommand.QueryPreparer` 89.4 → 89.3 %; aggregate 0.0 pp. The changed-line missing-branch list is unchanged (`QueryCommand.cs:783`; `QueryCommand.QueryPreparer.cs:4161, 4167, 4170, 4767, 4925`). `D368-C5` is therefore closed by measurement, not waived.
- **Inter-session dispersion (recorded):** per-method time deltas for identical code ranged from −32.59 % to +12.48 % across sessions 3 and 4; within-session bands ranged 0.69–90 %; the A/A control kept 19/19 inside band and the cross-session B/B probe 37/38. The acceptance statistic therefore cannot discriminate a ≤ ~2 % effect on this host — recorded as process defect `PD5` in `docs/specs/design/D368-pdca-process-defects.md`.
- Product code unchanged since attempt 2; the attempt counter is consumed by this non-product attempt and now reads `n=3/3`.

## CHECK record

### CHECK r1 (attempt n=1/3) — `pdca-check`, independent

Verdict: **not accepted → DO**. Criteria assessment: AC1–AC5 read from §PLAN record r1, cover the original task, no contradiction, no unmeasurable criterion; the recorded rule is `Δ ≤ Band`, not `|Δ| ≤ Band`.

| Criterion | Status | Basis |
|---|---|---|
| AC1 | met | all four arms, 10 operator + 9 control methods, identical SDK 10.0.401 / runtime 10.0.12, bands computed per metric |
| AC2 | unmet | `Execute_GlobalAggregate_WithHaving` +2.79 % > 0.80 %; `Execute_CapturedCondition` +2.07 % > 0.90 %; no user-accepted residual for either |
| AC3 | unmet | `ComponentShape[]` copy removed, but a single component was still materialised through `new[] { single }` in the `Components` getter, which capture used — the one-element array moved rather than disappeared |
| AC4 | met | all nine controls satisfy the one-sided inequalities; GroupBy ΔAlloc 0, extrema −1.45…−2.09 % |
| AC5 | met | frozen diff changes no configuration and adds no exclusion; solution 89.3/83.7 vs 85/75, integration 80.3/74.7 vs 65/75 (0.3 pp below, recorded) |

Findings: `D368-C1-time` (high, confirmed), `D368-C2-single-array` (high, confirmed), `D368-C3-red-suite` (high, confirmed; historical attribution unverified — an isolated pass does not prove absence of candidate influence, exception not accepted), `D368-C4-audit-gap` (blocking, confirmed; the four mandatory audit skills were denied to CHECK), `D368-C5-branch-gap` (medium, unverified; no branch delta / missing-branch list).

Next authorised transition: → DO, without changing criteria — fix the retained one-element-array path and the two AC2 regressions and repeat the prescribed A-B-B-A; provide a green whole-suite or a proven historical attribution with a substitute check; deliver the mandatory audit instructions by an authorised route without widening permissions; provide the branch delta and missing-branch list plus targeted invariant tests. Confidence: high on the AC2/AC3 refusal; limited on audit completeness and failure attribution. Infeasibility not established.

### CHECK r2 (attempt n=2/3) — `pdca-check`, independent

Verdict: **not accepted → DO**. Matrix: AC1 `met`; AC2 `unmet` (session 4: `Execute_CapturedCondition` +12.48 % vs 8.26 %, `Execute_Keyed` +2.64 % vs 0.69 %); AC3 `met` (both reductions present; all ten operator allocations below the D362 level by ~107–384 B/op); AC4 `unmet` (session 4: `Execute_GroupBy` +2.47 % vs 1.53 %, `Warm_Hit_SelectWhereMin` +3.16 % vs 1.41 %); AC5 `met` (89.3/83.8 vs 85/75; 80.3/74.7 vs 65/75 with the 0.3 pp integration-branch shortfall recorded). Findings: `D368-C1-time` confirmed; `D368-C2-single-array` refuted; `D368-C3-red-suite` refuted (23 419 / 0 failed); `D368-C4-audit-gap` unverified/partially closed; `D368-C5-branch-gap` confirmed (no before/after delta); `D368-C6-control-time` confirmed; `D368-C7-process-record` confirmed (stale counter in `Current state`, empty defect history, two-sided rule in `perf/PROTOCOL.md`). Routing: → DO without changing criteria; the C5 residual goes to escalation, no second evidence round in r1; the attempt budget stays 3/3 with no fourth.

### CHECK r3 (recheck after the evidence round) — `pdca-check`, independent

Verdict: **not accepted → DO**. AC1 `met`, AC2 `unmet`, AC3 `met`, AC4 `unmet`, AC5 `met` — unchanged on the same session-4 numbers. The A/A control (19/19 comparisons, 38 including allocations) is a single control session, not proof that the statistic never false-positives; the B/B probe's one violation (`Execute_CapturedCondition` +8.66 % vs 8.26 %) does not localise the cause of the four session-4 failures, and an allocation-level reproducibility does not establish a product cause. `D368-C7-process-record` still partially open: this record's `cycle` line still said `n=1/3`, the CHECK#2 report was absent, and the B/B sentence named the wrong method — all corrected in this update. C4/C5 residuals and the unattributed time findings are routed to escalation; the remaining third attempt is reserved for a product correction; on 3/3 with the mandatory expert review and no criteria defect the outcome is ACT `incomplete`/`STOP`, never a PASS and never a fourth attempt.

## ACT record

**Outcome: `incomplete` / `STOP`.** Recorded from CHECK r4 (terminal, `not accepted → ACT`); no `PASS`, no `done`. Counters at close: `N=1`, `r=1`, `n=3/3`; no fourth attempt (the expert review preceded it and found no product defect to fix).

- **Cause.** The cycle's time criteria cannot be established for this candidate: valid sessions on identical code disagree (`attempt3`: 10/10 operator and 9/9 control methods within band; `attempt4`: `Execute_CapturedCondition` +12.48 % vs 8.26 %, `Execute_Keyed` +2.64 % vs 0.69 %, and controls `Execute_GroupBy` +2.47 % vs 1.53 %, `Warm_Hit_SelectWhereMin` +3.16 % vs 1.41 %). The prescribed statistic uses a within-session A/A band from a single 3-iteration pair, whose dispersion (0.69–90 % across sessions) exceeds the sub-5 % effect it must judge. The expert review found the residual time **not attributable** to the candidate and no product fix available; it did not grant any criterion change.
- **Completed.** Both named reductions are implemented on the public component mechanism without touching public API or SQL semantics. `RawShape` carries the command's `ComponentSet` struct by value (no `ComponentShape[]` copy, no `ComponentShape` per capture; the dead nested class removed); a single component is stored and forwarded without a one-element `IQueryComponent[]` or `ComponentState` holder, and the public view is a lazily created one-item object never built on `Select`/capture. Measured allocations are below the D362 level on all ten operator methods (e.g. `Construct` 3533 vs 3640 B/op, `Warm_RawHit` 4557 vs 4864), i.e. the accepted structural floor is met and improved. SQL parity on the nine scenarios is identical apart from the process/time header. The whole suite is green on the final candidate (23 419 tests, 0 failed, 207 skipped) and on the pre-change base (23 419, 0, 208). Coverage: solution 89.3/83.8 (85/75), integration 80.3/74.7 (65/75, 0.3 pp below on branch — recorded, hard-fails on `main` only), configuration untouched. Branch delta before/after is measured and the changed-line missing-branch list is itemised.
- **Remaining (`unmet`).** `AC2` (operator time) and `AC4` (control time) on the session-4 numbers; `AC1`, `AC3`, `AC5` are `met`. `D368-C1-time` and `D368-C6-control-time` stay open with product causality unestablished.
- **Residual (`unverified`).** `D368-C4-audit-gap`: the manual diff audit ran with counts (no new slop, no public-signature change), but there is no automated API-inventory mechanism in the repository to check against; `D368-C5` is refuted. The design smell noted by CHECK (a four-level nested fingerprint loop on changed lines) is not a functional defect and was not registered in the review registers.
- **Handoff (CHECK's text, forwarded unchanged):** «Два структурных сокращения и снижение аллокаций подтверждены; актуальный полный suite проходит. Выполнение временных AC2/AC4 не подтверждено совокупностью измерений: валидные сессии расходятся, продуктовая причина не установлена, малый эффект не исключён. Цикл завершён как `incomplete`/`STOP`, без PASS и без четвёртой попытки. Решение об изменении метода/критерия либо явном принятии остаточного разрыва принадлежит пользователю. Issue #368 не закрывать как успешно выполненный; milestone не менять, push не выполнять.»
- **Next authorised action.** The user decides: (a) accept the residual time gap / revise the measurement method (inter-session pooling, CPU pinning, a fixed repeat count) as a new explicit instruction and a fresh PLAN, or (b) keep the change as delivered and close #368 with the allocation evidence only. The candidate is committed on `1.0.9-rc5` for that decision; nothing was pushed and #368 stays open. The process-side lesson is recorded as `PD5` in `docs/specs/design/D368-pdca-process-defects.md`.

## Revision history

`none`

## Defect history

| Key | Observed | Applied fix | Last outcome |
|---|---|---|---|
| `D368-C1-time` | CHECK#1 (attempt 1): `Execute_GlobalAggregate_WithHaving` +2.79 % vs band 0.80 %, `Execute_CapturedCondition` +2.07 % vs 0.90 %; CHECK#2 (attempt 2): `Execute_CapturedCondition` +12.48 % vs 8.26 % and `Execute_Keyed` +2.64 % vs 0.69 % in session 4, while session 3 passes 10/10 | no product change targeted at time (the remaining overhead is the accepted `EntityBuilder.Clone()` floor); evidence added: a quiet-host re-measure (sessions 3 and 4), an A/A control on the same binary, and a cross-session B/B reproducibility probe | **open** — confirmed by CHECK#2 in the session-4 numbers; attribution to host drift is not established (the B/B probe shows only 1/38 violations) |
| `D368-C2-single-array` | CHECK#1: single component materialised through `new[] { single }` in the `Components` getter used by capture | `ComponentSet` struct stored by value, `ComponentsState` for capture/compare/fingerprint, `SingleComponentList` view only on first public read, dead `RawShape.ComponentShape` removed | **refuted** by CHECK#2 |
| `D368-C3-red-suite` | CHECK#1: whole-suite red on a ClickHouse order-dependent assertion | clean run with `DOCKER_HOST` after purging reused containers | **refuted** by CHECK#2 (23 419 / 0 failed) |
| `D368-C4-audit-gap` | CHECK#1: the four mandatory audit skills were denied to CHECK | rule sets delivered inline in the CHECK#2 dispatch; manual diff audit with counts | **partially closed** by CHECK#2 (manual audit done; full API inventory still unverified) |
| `D368-C5-branch-gap` | CHECK#1 and CHECK#2: no before/after branch delta, only candidate counts | pre-change baseline coverage measured on a clean worktree at `9e0117b5` with the identical suite command; before/after per touched file in `coverage3/branch-delta.md` (EntityBuilder 85.8→85.8 %, QueryCommand 82.7→81.7 %, Clone 100→100 %, QueryPreparer 89.4→89.3 %; aggregate 0.0 pp) | **refuted** by CHECK#4 (baseline and delta exist; the residual local branch drop is listed line by line) |
| `D368-C6-control-time` | CHECK#2: session 4 puts `Execute_GroupBy` (+2.47 % vs 1.53 %) and `Warm_Hit_SelectWhereMin` (+3.16 % vs 1.41 %) outside their time bands; allocation deltas unchanged or negative | same evidence package as C1 (session 3 is 9/9; A/A control 19/19) | **open** |
| `D368-C7-process-record` | CHECK#2: `## Current state` still carried `n=1/3`; this section said `none` despite the CHECK#1 findings; `perf/PROTOCOL.md` stated the wrong (two-sided) comparison rule | this update; PROTOCOL.md corrected to the one-sided `Δ ≤ Band`; CHECK#1/#2 reports recorded in §CHECK record | **fixed** |

The measurement-method instability behind C1/C6 was not turned into a product or criteria rewrite: the protocol is a user requirement (issue #368) and stays unchanged.

The cycle's **process** defects are recorded separately in `docs/specs/design/D368-pdca-process-defects.md` (PD1 stale counter, PD2 empty defect history, PD3 protocol oracle contradiction, PD4 measurement under load, PD5 per-session band instability, PD6 invalid suite runs, PD7 transcription error, PD8 driver fallback). The keys above are product/evidence findings and do not duplicate that record.

## Changed files

Product (candidate, uncommitted at the DO→CHECK boundary):
- `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs` — reduction 1: `RawShape.Components` carries the command's `QueryCommand.ComponentSet` by value; `CaptureRawShape` no longer allocates `ComponentShape[n]`/`ComponentShape` per capture (the nested class is removed); `ComponentsMatch`, fingerprint and closure-scan sites read through `IQueryComponentShape` on the set.
- `src/nextorm.core/Query/QueryCommand.cs` — reduction 2: `ComponentState` holder removed; the state is a `ComponentSet` readonly struct (none / single `IQueryComponent` / immutable list) with a lazily created `SingleComponentList` view for the public property and a render-scoped shape; `SetComponents(object?)` and `ComponentsState` added for allocation-free forwarding and capture.
- `src/nextorm.core/Query/QueryCommand.Clone.cs` — clone shares the flat component/prepared state and drops the render shape.
- `src/nextorm.core/Builders/EntityBuilder.cs` — `ApplyCommandOptions` forwards the component state without a one-element `IQueryComponent[]` or holder allocation.

Evidence and bookkeeping: `.pdca/status/D368-component-operator-overhead-1.md`, `.pdca/D368-component-operator-overhead/rv1/**` (build, tests, alloc probes, eight A-B-B-A runs, frozen candidate diff and hashes, perf table/raw data/protocol, coverage logs and summaries, `manifest.json`).

## Git state

status path: `.pdca/status/D368-component-operator-overhead-1.md` — created and modified this cycle · committed by the ACT commit on `1.0.9-rc5` together with the four product files and `docs/specs/design/D368-pdca-process-defects.md` · evidence root `.pdca/D368-component-operator-overhead/` stays untracked (local evidence, not versioned by project policy) · baseline `HEAD 9e0117b5fd11f072f05aad693a797f80b345bd8f`. Auto-commit is authorised by the user («pdca 368 автономно автокомит»); `git push` is forbidden and not performed; issue #368 stays open because the outcome is `incomplete`/`STOP`.

## Progress log

append-only, one row per event, never edited retroactively:

`2026-11-06T10:49:45Z | PLAN | revision r1 | attempt n=1/3 | plan ready (pdca-planner, form=cycle, overlays=pdca-dotnet+pdca-coder+nextorm-pdca); autonomous mode, no plan-approval wait; DO not started | evidence: §PLAN record r1`

`2026-11-06T11:03:29Z | DO | revision r1 | attempt n=1/3 | DO started (stage 1) | evidence: .pdca/D368-component-operator-overhead/rv1/`
`2026-11-06T11:03:29Z | DO | revision r1 | attempt n=1/3 | build succeeded (0 warnings, 0 errors) | evidence: .pdca/D368-component-operator-overhead/rv1/build/build.log`
`2026-11-06T11:03:29Z | DO | revision r1 | attempt n=1/3 | inner-loop tests green (core 3915, sqlite 2306, extensions 154, querycomponents 63, postgres 1946, sqlserver 2076, mysql 1215, mariadb 1058, clickhouse 1505) | evidence: .pdca/D368-component-operator-overhead/rv1/tests/*.log`
`2026-11-06T11:03:29Z | DO | revision r1 | attempt n=1/3 | structural alloc probe recorded (construct 2568.00 B/op, capture 3232.00 B/op) | evidence: .pdca/D368-component-operator-overhead/rv1/alloc-probe/numbers.txt`
`2026-11-06T11:03:29Z | DO | revision r1 | attempt n=1/3 | evidence helpers verified byte-identical (validate_inner_loop.py sha256 59ed8d88..., pdca_evidence.py sha256 d3bb29dc...) | evidence: .pdca/scripts/`

`2026-11-06T11:16:01Z | DO | revision r1 | attempt n=1/3 | candidate frozen (patch sha256 af5750a3..., touched-files.sha256) | evidence: frozen/candidate.diff`
`2026-11-06T11:16:01Z | DO | revision r1 | attempt n=1/3 | operator A-B-B-A complete (A1/B1/B2/A2 exit 0) | evidence: runs/A1-operator.log ... A2-operator.log`
`2026-11-06T11:16:01Z | DO | revision r1 | attempt n=1/3 | control A-B-B-A complete (A1/B1/B2/A2 exit 0) | evidence: runs/A1-controls.log ... A2-controls.log`
`2026-11-06T11:16:01Z | DO | revision r1 | attempt n=1/3 | SQL parity identical (parity.diff 0 lines) | evidence: parity.diff`
`2026-11-06T11:16:01Z | DO | revision r1 | attempt n=1/3 | alloc probe recorded (baseline capture 4848.00, candidate 4784.00) | evidence: alloc-baseline.txt, alloc-candidate.txt`
`2026-11-06T11:16:01Z | DO | revision r1 | attempt n=1/3 | TABLES.md + measurement.json emitted | evidence: perf/TABLES.md, perf/measurement.json`

`2026-11-06T12:24:15Z | RECOVERY | revision r1 | attempt n=1/3 | Recovered after compaction: two host restarts aborted the pdca-orchestrator dispatch; state rebuilt from this status file (phase DO, stage 2a measured) | evidence: this record`
`2026-11-06T12:24:15Z | RECOVERY | revision r1 | attempt n=1/3 | Notice: pdca-orchestrator unavailable (both dispatches were interrupted by host restarts, no session to resume); autonomous cycle is driven by the flat primary under the contract's documented fallback | evidence: this record`
`2026-11-06T16:20:00Z | DO | revision r1 | attempt n=1/3 | whole-suite coverage run re-executed with DOCKER_HOST (the earlier run was invalid: no DOCKER_HOST, containers unreachable, exit 2) — 23419 total, 23211 succeeded, 1 failed, 207 skipped, exit 2 | evidence: coverage/solution2.log`
`2026-11-06T16:20:00Z | DO | revision r1 | attempt n=1/3 | sole failure is a pre-existing order-dependent ClickHouse assertion on an unordered UNION ALL (T300ScalarDerivedSourceLiveMatrix.NullableScalarMaterialize_Live); isolated re-run 5/5 passed, source untouched by this cycle | evidence: coverage/solution2.log`
`2026-11-06T16:30:00Z | DO | revision r1 | attempt n=1/3 | solution coverage recorded 89.3 % line / 83.7 % branch (thresholds 85/75) | evidence: coverage/solution2-report/Summary.txt`
`2026-11-06T16:36:00Z | DO | revision r1 | attempt n=1/3 | integration gate run 8659 total, 0 failed, 197 skipped, exit 0; coverage 80.3 % line / 74.7 % branch (thresholds 65/75; branch warning-only off main) | evidence: coverage/integration2.log, coverage/integration2-report/Summary.txt`
`2026-11-06T16:37:30Z | DO | revision r1 | attempt n=1/3 | frozen manifest written; validate_inner_loop manifest rc 0, pdca_evidence lint rc 0 | evidence: manifest.json`
`2026-11-06T16:37:30Z | DO | revision r1 | attempt n=1/3 | DO completed → CHECK: candidate with evidence; AC2 recorded open (two operator scenarios outside their own band) | evidence: §DO result`
`2026-11-06T16:38:00Z | CHECK | revision r1 | attempt n=1/3 | verdict not accepted → DO; findings D368-C1-time, D368-C2-single-array, D368-C3-red-suite, D368-C4-audit-gap, D368-C5-branch-gap | evidence: §CHECK record`
`2026-11-06T20:10:00Z | DO | revision r1 | attempt n=2/3 | attempt 2 started: single-element array path removed (`ComponentSet` struct + `SingleComponentList`), dead `RawShape.ComponentShape` deleted, `ComponentsState` used by capture; build 0/0; inner-loop suites green (core 3915, sqlite 2306/1 skip, extensions 154/1, querycomponents 63, postgres 1946, sqlserver 2076/8, mysql 1215, mariadb 1058, clickhouse 1505) | evidence: §DO result attempt 2`
`2026-11-06T20:35:00Z | DO | revision r1 | attempt n=2/3 | A-B-B-A session under load average 24 discarded as contaminated | evidence: attempt2/perf/TABLES.md`
`2026-11-06T22:09:00Z | DO | revision r1 | attempt n=2/3 | quiet-host A-B-B-A session 3 complete: all 10 operator methods within band (2 via the D362-accepted residual); controls within band | evidence: attempt3/perf/TABLES.md`
`2026-11-06T22:31:00Z | DO | revision r1 | attempt n=2/3 | control session 4 complete: `Execute_CapturedCondition` +12.48 % vs 8.26 and `Execute_Keyed` +2.64 % vs 0.69 outside band; two controls outside band by 1-2 pp — recorded open, not waived | evidence: attempt4/perf/TABLES.md`
`2026-11-06T22:39:00Z | DO | revision r1 | attempt n=2/3 | two invalid whole-suite runs recorded (seeding races, then container deaths under memory pressure); stale reused containers purged | evidence: coverage3/solution.log, coverage3/solution2.log`
`2026-11-06T22:47:00Z | DO | revision r1 | attempt n=2/3 | clean whole-suite run on the final candidate: 23419 total, 0 failed, 23212 succeeded, 207 skipped, exit 0 | evidence: coverage3/solution3.log`
`2026-11-06T22:52:00Z | DO | revision r1 | attempt n=2/3 | coverage recorded: solution 89.3/83.8 (85/75), integration 80.3/74.7 (65/75); branch delta + touched-line missing-branch list emitted | evidence: coverage3/solution3-report/Summary.txt, coverage3/integration-report/Summary.txt, coverage3/branch-delta.md`
`2026-11-06T23:00:00Z | DO | revision r1 | attempt n=2/3 | SQL parity identical (header-only diff), alloc probe recorded, manifest refreshed (tree a9f56018...), validate/lint rc 0 | evidence: attempt3/parity.diff, attempt3/alloc-*.txt, manifest.json`
`2026-11-06T23:00:00Z | DO | revision r1 | attempt n=2/3 | DO completed → CHECK: candidate with evidence; AC1/AC3/AC5 met, AC2/AC4 open (cross-session band instability) | evidence: §DO result attempt 2`
`2026-11-06T23:55:00Z | CHECK | revision r1 | attempt n=2/3 | verdict not accepted → DO; C2/C3 refuted, C1/C6 confirmed on the session-4 numbers, C4 partially closed, C5 residual routed to escalation, C7 process record | evidence: §CHECK record`
`2026-11-07T00:30:00Z | DO | revision r1 | attempt n=2/3 | A/A control on the same binary: 19/19 comparisons pass (deltas -4.87…+3.86 % time) | evidence: aacontrol/perf/TABLES.md`
`2026-11-07T00:35:00Z | DO | revision r1 | attempt n=2/3 | cross-session B/B probe on identical code: 1/38 violations — the estimator is reproducible, session-4 findings are not dismissed as noise | evidence: aacontrol/REPRODUCIBILITY.md`
`2026-11-07T00:40:00Z | DO | revision r1 | attempt n=2/3 | process-record fixes: counter refreshed, defect history filled, PROTOCOL.md comparison rule corrected to one-sided Δ ≤ Band | evidence: this record, perf/PROTOCOL.md`
`2026-11-07T00:40:00Z | DO | revision r1 | attempt n=2/3 | evidence round complete → CHECK recheck (no product change; attempt counter unchanged) | evidence: §DO result evidence round`

`2026-11-07T01:20:00Z | CHECK | revision r1 | attempt n=2/3 | verdict not accepted → DO; C7 partially open (stale counter, missing CHECK#2 record, wrong B/B method); C4/C5 and the time attribution routed to escalation | evidence: §CHECK record r3`
`2026-11-07T01:50:00Z | ESCALATE | revision r1 | attempt n=2/3 | expert: residual time overhead not attributable to the candidate under the prescribed method; no product change justified; the third attempt is non-product work (pre-change branch baseline + inter-session dispersion) | evidence: §Expert review`
`2026-11-07T02:30:00Z | DO | revision r1 | attempt n=3/3 | process-defect spec written (PD1–PD8) and referenced from §Defect history | evidence: docs/specs/design/D368-pdca-process-defects.md`
`2026-11-07T03:10:00Z | DO | revision r1 | attempt n=3/3 | pre-change branch baseline measured at 9e0117b5 (23419 tests, 0 failed, 89.3/83.8); before/after per touched file emitted; C5 closed | evidence: coverage-base/base.log, coverage3/branch-delta.md`
`2026-11-07T03:10:00Z | DO | revision r1 | attempt n=3/3 | DO completed → CHECK recheck after escalation (product unchanged) | evidence: §Expert review and the third attempt`

`2026-11-07T03:40:00Z | CHECK | revision r1 | attempt n=3/3 | verdict not accepted → ACT incomplete/STOP; AC1/AC3/AC5 met, AC2/AC4 unmet; C2/C3/C5 refuted, C4 partially, C1/C6/C7 confirmed; handoff recorded | evidence: §CHECK record r4`
`2026-11-07T03:45:00Z | ACT | revision r1 | attempt n=3/3 | ACT closed: outcome incomplete/STOP, no PASS, no done, no fourth attempt; status file finalized and kept; issue #368 stays open, push not performed | evidence: §ACT record`
