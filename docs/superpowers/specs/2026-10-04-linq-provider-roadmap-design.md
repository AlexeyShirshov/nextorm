# LINQ-провайдер для nextorm: дизайн и roadmap

- **Дата:** 2026-10-04
- **Статус:** Архитектура и roadmap согласованы и одобрены 2026-10-04; работа architect по этой задаче завершена. Дальнейшую детализацию и планирование реализации выполняет отдельный скилл по выбору пользователя. Подготовленный план этапа 0 сохранён как неутверждённый справочный материал; его review и выбор исполнения в текущей сессии не требуются. Этап 0 не выполнен; продуктовая реализация не разрешена этим handoff.
- **Журнал одобрений:** 2026-10-04 — пользователь: «апрув»; одобрена письменная спецификация roadmap, не implementation plan и не выполнение. 2026-10-04 — пользователь: «го»; авторизована подготовка подробного плана этапа 0 (не исполнение). 2026-10-04 — пользователь: «не нужна детализация реализации, ее сделает отдельный скил»; это **division of responsibility** (архитектура/roadmap остаются за architect, детализацию реализации ведёт отдельный скилл по выбору пользователя), **не** approval спецификации/плана-реализации и не разрешение execution.
- **Тип документа:** общий **roadmap** (карта работ), а не детальный план реализации. Каждый workstream получает собственную спеку → одобрение → детальный implementation plan → исполнение. Один «гигантский» план не создаётся; оценки трудозатрат и даты здесь не изобретаются.
- **Трекинг:** GitHub issue [#20](https://github.com/AlexeyShirshov/nextorm/issues/20) — «TODO: LINQ support», статус OPEN; milestone **1.2-a.1** ([milestone/10](https://github.com/AlexeyShirshov/nextorm/milestone/10)) — проверено (VERIFIED) через `gh` 2026-10-04. Тело issue содержит тот же честный статус.
- **Локальный путь этой спеки:** `docs/superpowers/specs/2026-10-04-linq-provider-roadmap-design.md` (файл репозитория, не опубликованный blob; в рамках этой задачи не коммитится).
- **Доказательная база:** ссылки на код ниже — навигационные line pointers, а не воспроизведённые тесты. В этой docs-only задаче сборка/тесты не запускались.

## 1. Намерение и критерии успеха

Цель — **полное измеримое relational LINQ read-query coverage**, сравнимое с pinned baseline linq2db, и устранение имеющихся границ «ToNextOrm-only»-композиции через **общий нативный `IQueryable` translator** плюс **EF-адаптер**.

- Целевые нативные SQL-провайдеры: **SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse**.
- EF-вход — **только** для реально поддерживаемых EF-провайдеров (PostgreSQL, SQL Server, SQLite, MySQL). Выражение «нативный all-6» **не означает** «EF-адаптер для all-6»: MariaDB требует фактической совместимой EF-провайдерной/адаптерной сертификации; для ClickHouse EF-провайдер отсутствует.
- **InMemory исключён** из новой parity-приёмки; существующие InMemory/builders сохраняются как есть.

Критерий успеха формулируется как **измеримая матрица** operator × query-context × result-shape × provider с явными acceptance-case ID, а не как лозунг «паритет с linq2db». Отсутствие заявленной поддержки должно быть явно классифицировано (unsupported-by-baseline / provider limitation / out-of-scope), а не замаскировано.

## 2. Scope

- Стандартные методы .NET 10 `Queryable` и их перегрузки.
- Server-context `Enumerable` там, где baseline его поддерживает.
- Композиция запросов (многоступенчатая).
- Терминальные операторы: sync и async; явное перечисление (enumeration).
- EF-специфика: `Include`/`ThenInclude`, navigation, properties, functions, filters, raw SQL, стратегии (`AsSplitQuery`/`AsSingleQuery`).
- Расширяемые зарегистрированные трансляции методов.

Существующие SQL-возможности (CTE, window, hints и т. п.) и существующие нативные API **сохраняются**; roadmap их не отменяет.

## 3. Non-goals

- Change tracking / identity map между запросами.
- `SaveChanges` (EF-мутации).
- LINQ DML.
- API-паритет по именам с linq2db.
- Отдельный LINQ-проект для CTE / window functions / hints как часть этого roadmap.
- Произвольные незарегистрированные C# extension-методы / comparers / клиентские делегаты.
- Глобальная замена существующих builders или обязательная миграция.

## 4. Исследовательский baseline (pinned)

- linq2db **6.5.0**, tag `v6.5.0`, commit `47ed37b1db6c58eaf2cfd8a7fbe6aac294aae305`.
- EF10 bridge **linq2db.EntityFrameworkCore 10.6.0**.
- Ссылки: <https://www.nuget.org/packages/linq2db/6.5.0>, <https://github.com/linq2db/linq2db/releases/tag/v6.5.0>, <https://www.nuget.org/packages/linq2db.EntityFrameworkCore/10.6.0>.

Baseline используется **только как тестовый ориентир**; **runtime-зависимость от linq2db не предлагается**. linq2db сам **не является универсальной EF-семантикой** — в частности, его bridge срезает методы split/single. Наш согласованный стратегический контракт: такие вещи либо **явно соблюдаются**, либо **fail-closed**, без тихого отбрасывания.

Operator-inventory baseline **ещё не создан** — это конкретный deliverable фазы 0. Мы не заявляем, что он уже полон, и не делаем вид, что какие-то даты/неизвестные подмножества уже реализованы.

## 5. Существующие факты и ограничения (с указателями)

- **Существующий EF-мост.** `ToNextOrm` для `DbSet<T>` и для `IQueryable<T> + DbContext` возвращает `EntityBuilder<T>` (`where T : class`): `src/nextorm.entityframeworkcore/NextOrmQueryableExtensions.cs:34,56`. Границы моста заданы явно: bounded operator dispatcher `:113-253`; lambda guard `:356-463`; всё остальное бросает `NotSupportedException`, а не уходит в клиентскую оценку.
- **Нативного `IQueryable`-провайдера не найдено** (проверено Roslyn). Нативный projected/derived источник существует: `src/nextorm.core/DataContext/DataContextExtensions.cs:1233` (builder из `QueryCommand<TResult>`) и внутреннее разрешение query-backed источника `src/nextorm.core/Builders/EntityBuilder.cs:3543`.
- **`SelectMany` / `GroupJoin` нативный SQL не поддерживаются** — только InMemory: `src/nextorm.core/Builders/EntityBuilder.cs:2394,2417,2445,2473` (`EnsureInMemory`, явный `NotSupportedException` «SQL providers do not translate SelectMany/GroupJoin yet»).
- **Подготовленные колонки/shape** уже есть: `src/nextorm.core/Expressions/SelectExpression.cs`, `src/nextorm.core/DataContext/RowMaterializerBuilder.cs`; сборка nested collection вложена в `src/nextorm.core/Builders/Joins/JoinIntoStitcher.cs` (JoinInto/nested collection assembly).
- **EF model import неполон.** Owned-типы пропускаются, inheritance (TPH/TPT/TPC) и schema-квалификация отклоняются, shadow-properties пропускаются: `src/nextorm.entityframeworkcore/NextOrmModelMapper.cs:69-106`. Существующие core-relationships сохраняются, но EF-relationships **не импортируются** (`:260`).
- Нативные **declared relationships** #105/#148 — это **уже поставленный (delivered) scope**, а не undone новым roadmap. EF-метаданные **явно объявляют** отношения; это не новая политика FK-convention.
- **Composite EF key flags сами по себе** не дают полной трансляции composite navigation.
- Про отсутствие `IGrouping` не делаем выводов из grep/symbol speculation: **материализация grouped sequence требует отдельного дизайна** сверх текущей SQL `GroupBy`-поверхности.

## 6. Альтернативы и выбранная архитектура

Рассмотренные альтернативы:

1. **Рекомендовано:** общий **immutable query IR + binding + result-shape lowering** поверх существующего backend.
2. Прямая трансляция «method-to-builder» — проще как первый шаг, но быстро упирается в cross-operator scope/shape-сложность.
3. Замена/дублирование SQL-компилятора собственным backend — высокий риск, **отклонено**.

Выбран вариант 1: не переписывать SQL-компилятор, а нормализовать LINQ в общий IR и опускать его в существующие `QueryCommand`/`QueryDefinition`/planner/dialects/cache.

```
Native IQueryable + EF adaptation
        │  (нормализация корней)
        ▼
   normalized LINQ
        │
        ▼
 immutable query IR / result shape
        │  (lowering)
        ▼
 QueryCommand / QueryDefinition
        │
        ▼
 existing planner / dialects / cache
        │
        ▼
 execute / materialize
```

## 7. Архитектура и новые компоненты (концептуально)

Концептуальные модули (это **не** финализированные имена классов/интерфейсов):

- **Native QueryProvider** — корень и исполнение нативного `IQueryable`.
- **EF adapter** — нормализует корни и metadata, **без параллельного relational translator**.
- **Operator catalog** — по `MethodInfo` + overload.
- **Scope binding / member rebinding**.
- **Relational IR** — сохраняет stage boundaries.
- **Result-shape plan** — entity / scalar / DTO / group / collection.
- **Lowering capability checks**.
- **Execution** — per-call values/resources.
- **Public API** — явное expansion / server method translations + opt-in final client projection.

## 8. API

**Согласовано пользователем (CORRECTION):** `ToNextOrm` возвращает **нативные builders** — **не** называть его legacy/deprecated. Его сигнатуры и builder API сохраняются как first-class; это **не** blanket deprecation и **не** обязательная миграция.

Предложения (новые имена, кроме уже согласованного `ToNextOrmQueryable`, остаются предложениями до API-этапа):

```csharp
// Новый нативный entry point: неограниченный T, обычный IQueryable
public static IQueryable<T> ToNextOrmQueryable<T>(this IQueryable<T> source, DbContext dbContext);
// T без ограничения

// Нативный вход из builder (имя — предложение; в phase1 рассмотреть неоднозначность с System.Linq.AsQueryable)
public static IQueryable<T> AsQueryable<T>(this EntityBuilder<T> source);
```

Пример (EF `Select` anonymous → `ToNextOrmQueryable` → `GroupBy` → aggregate projection); показан **запрос, не исполнение**:

```csharp
var q =
    db.Orders
      .Select(o => new { o.CustomerId, o.Total })
      .ToNextOrmQueryable(db)
      .GroupBy(x => x.CustomerId)
      .Select(g => new { g.Key, Sum = g.Sum(x => x.Total) });
```

- Оригинальный `ToNextOrm` может переиспользовать общий compiler для запросов, представимых его builder result-контрактом; универсальные advanced/group/collection-семантики для builder-возврата **не обещаются ложно**.
- Async-имена следуют репозиторию: суффикс `Async` только при наличии sync-близнеца.

## 9. Семантика (fail-closed)

- Сохранять **sequence/correct derived-table boundaries**: `Where` после paging, повторные `Skip`/`Take`/`Distinct`, `Select`→`Where`, joins после projection. **Никаких небезопасных pushdown'ов.**
- Любой корректно построенный повторный `Queryable.OrderBy`/`OrderByDescending` поддерживается. Он задаёт **новую первичную сортировку** текущей последовательности, а не `ThenBy`. Если между сортировками есть `Skip`/`Take`/`Distinct` либо иной оператор, влияющий на границу, сохраняются необходимые стадии запроса; сортировку **нельзя** переносить через такую границу без доказанной эквивалентности. `ThenBy` требует ordered-source; некорректное дерево **отклоняется**. Порядок строк с равными ключами **не обещается** сверх контракта LINQ-провайдера; phase0 фиксирует случаи для regression/conformance.
- `ThenBy` без ordered-source — malformed дерево, **reject**.
- `SelectMany` (flatten) vs `GroupJoin` (grouped) vs `DefaultIfEmpty` (LEFT JOIN) — **отдельное** покрытие.
- Различать null scalar / missing joined entity / empty collection. Aggregate на пустом входе, cardinality и defaults — **явно**.
- SQL ordering/collation/null behavior документируется по провайдеру; избегать ложного оракула LINQ-to-Objects.
- **Fail-closed по умолчанию.** Opt-in — только final client projection. Последующий server-оператор требует полностью транслируемой композиции **или отклонения**; никогда не implicit client filtering и т. п.
- Граница клиента — явные `AsEnumerable`/materialization, обычный .NET.
- `Include`/`ThenInclude` — явный load graph; `AsSplitQuery`/`AsSingleQuery` соблюдаются на поддерживаемых shape/provider **или** точный fail-closed (строже baseline; не заявлять одинаковую read-consistency split vs single). `AsTracking` для read-only **не поддерживается**; EF tracking поддержки нет.
- Identity внутри собранного graph — отличие от глобального tracking/identity map.
- Диагностика: metadata / method / unsupported type / root / dialect / shape — с exact method overload, tree location и provider.
- Raw SQL — параметризованно, сохраняя EF parameter contracts; **без конкатенации**. Roots из разных contexts — **reject** (если позже не спроектировано отдельно); никакого тихого shared connection.
- Неизвестные методы — только через **явные** зарегистрированные трансляции; произвольные comparers не обещаются.

## 10. Cache и ownership

- Shape **не** захватывает runtime values; refresh на каждом исполнении.
- Ключ включает релевантную identity/metadata EF-модели, dialect/version capability, result strategy.
- План **не может** удерживать `DbContext`/closure lifetimes через неправильный глобальный кэш.
- Immutable query inputs vs execution-local parameters/commands; cancellation/resource lifecycle.
- **Не менять** общий `AnyCommand` sticky `Cache=false` (инвариант AGENTS); при необходимости — call-local `storeInCache:false`.
- Correctness важнее автоматического fallback или performance.

## 11. Roadmap (10 этапов)

| # | Этап | Содержание | Gate/зависимость |
|---|------|-----------|------------------|
| 0 | Inventory | Матрица .NET 10 `Queryable` overloads / contextual `Enumerable` / EF10 против pinned baseline; для каждого случая — семантика, shape, SQL-провайдеры, test status, отдельно: required / unsupported-baseline / provider limitation / out-of-scope. Включить indexed variants, `*By`, `Range`/`Index`, `Reverse`/`SkipWhile`/`TakeWhile`/`Zip` и т. д. в INVENTORY с фактической проверкой baseline; никакой непроверенной blanket-поддержки/исключения. Добавить acceptance-case ID и broad composition cases. Deliverable — **frozen scoped required set**; отложить required case ≠ объявить паритет завершённым. | Старт; вход для всех |
| 1 | Foundations | Provider + immutable IR + binding + shape + native root lowering + parameterization + diagnostics; backend smoke; **builders не тронуты**. | после 0 |
| 2 | Linear | `Where`, `Select`, sorts, `Distinct`, paging — произвольные валидные комбинации, derived boundaries; scalar/DTO/entity composable. | после 1 |
| 3 | Terminals | quantifiers, `Contains`, aggregates, `First`/`Single`/`Last`/`ElementAt`/`*OrDefault`, sync/async enumeration; explicit final client opt-in; cardinality, empties, null, cancellation, disposal. | после 2 |
| 4 | Multiple roots | `Join`, `SelectMany`, `GroupJoin`, `DefaultIfEmpty` patterns; set-операции `Concat`/`Union`/`Intersect`/`Except`; local collection в поддерживаемых контекстах; outer-join binding shapes. **Non-flattened `GroupJoin` явно завершается в phase6** — не называть phase4 полностью поддержанным. | параллельно 3–5 после 2 |
| 5 | GroupBy | Overloads, aggregates/HAVING, correlated EXISTS/IN/scalar nested queries; корректная группировка vs grouped sequence (**полные grouped results — phase6**). | параллельно 3–4 после 2 |
| 6 | Complex shapes | non-flat `GroupJoin`, `IGrouping`, nested collections/DTO, multiple-query plans, no hidden N+1; ordering/batching/reassembly, resource lifecycle. Provider/parameter limit chunking и identity-within-graph вводятся как **design criteria**, а не выдуманные реализованные пороги. | зависит 4–5 |
| 7 | EF import/roots | navigation, `Include`/`ThenInclude`, filters, `EF.Property`, `EF.Functions`, raw SQL, split/single. Required inventory: owned/complex, schema/shadow/converters/composite relationships/inheritance — **сверяется с фактическим baseline перед обещанием**; не включать безусловно TPT, если baseline его не поддерживает. Query filters/parameter values — актуальные. Никакого EF lazy loading/tracking. EF model import может начаться после contract'ов фазы 1; EF Include/loading зависит от фазы 6. | 1 (metadata), 6 (loading) |
| 8 | Closure | Закрыть остаток матрицы: варианты overloads, indexed, новые overloads, `*By`, `Range`/`Index`, `Cast`/`OfType`, method expansion/translations. Inventory полностью учтён, required-записи **фактически** сделаны, а не свалка optional. | после 3–7 |
| 9 | Certify | 6 провайдеров, regression/cache/performance, docs EN+RU, builder/native `IQueryable`/EF примеры, limitations и **честная** матрица. | все required done |

DAG: `0 → 1 → 2`; далее 3–5 частично параллельны; 6 зависит от 4–5; EF model import может стартовать после contract'ов 1, EF Include/loading зависит от 6; 8 закрывает inventory; 9 — после всех required. Многоэтапные частичные релизы **явно не** маркируются как full.

## 12. Verification / приёмка

- Unit-тесты: normalization, binding, lowering, shape; тесты invalid tree/provider diagnostics.
- SQL snapshots — **дополнительны**, не заменяют исполнение.
- Реальное исполнение против БД.
- Baseline differential + LINQ-to-Objects там, где валидно; relational-ожидания специфицируются **отдельно**.
- Единица покрытия — **exact overload × query context / result shape × provider**; failures/limitations linq2db **классифицируются**, а не копируются автоматически.
- Кейсы: empty/duplicate/null ties, post-projection/order paging, group correlated, multiple runs с разными params/contexts/models (cache poisoning), ownership/transaction/cancellation, sync/async streaming/buffering, no-N+1 command counts.
- Regression: builders и существующие query filters.
- **All-6 native certification:** прогон без `skipped`, который нельзя считать зелёным. MariaDB — **истинный** провайдер (не подмена MySQL); ClickHouse — **истинная** БД. Инструкции интеграционных тестов — `.opencode/skills/running-integration-tests/SKILL.md`, использовать во время исполнения; восстановление Podman — по AGENTS. **Для этой docs-only задачи тесты не запускаются.**
- EF-adapter coverage — только реально поддерживаемые EF-комбинации; отсутствующий EF-провайдер — **не** повод заявлять нативную поддержку.
- Performance gate framework: per-case SQL command count, cold compile/cache-hit allocations/throughput baseline и reviewed budgets **на каждый subplan**; проценты не изобретать. Projection и прочее — производительность осмысленна, без переписывания на второй backend.

## 13. Риски и митигации

| Риск | Митигация |
|------|-----------|
| Nested collection batching/consistency и streaming | посвящённая спека фазы 6; chunking/identity — design criteria |
| Model import больше, чем dispatch | фаза 7 со сверкой с baseline |
| .NET overload / provider variance | фаза 0 inventory + exact overload units |
| Derived alias nullability | явные shape-тесты |
| Cache identity / closure lifetime | §10 |
| Одна огромная фича | утверждаемые **per-stage** спеки; этот roadmap не план |

## 14. Settled vs open

**Уже решено:** scope/non-goals, выбор архитектуры (immutable IR + binding + shape lowering), fail-closed semantics, сохранение builders и `ToNextOrm` first-class, 10-этапный roadmap, DAG, acceptance framework, статус tracking.

**Открыто (детальный дизайн):** точное нативное root-имя, package placement, IR-классы; public registration shape; grouped assembly strategy / provider emulations (выбираются per spec); authoritative method matrix создаётся в phase 0; supported mapping — по baseline. Settled scope не переоткрывать; неодобренных архитектурных веток не заводить.

## 15. Tracking и handoff

- **Трекинг:** [#20](https://github.com/AlexeyShirshov/nextorm/issues/20), milestone **1.2-a.1** ([milestone/10](https://github.com/AlexeyShirshov/nextorm/milestone/10)); в теле issue — scope/architecture/10-stage checklist/acceptance/status.
- **Следующий шаг:** Передать отдельному скиллу утверждённую спецификацию roadmap, issue #20 и справочный draft этапа 0. Этот скилл готовит собственный детальный план в рамках утверждённого дизайна и применяет необходимые review/approval gates. Архитектурный этап 0 (матрица паритета) и последующие этапы не отменены и не отмечаются выполненными.
- Product-реализация требует отдельного **утверждённого** плана и пройденных approval gates; этим handoff **не** разрешена.
- Skill writing-plans здесь недоступен и не вызывался; подробный план этапа 0 подготовлен вручную, без заявления о вызове skill.
- Коммиты/push/merge в этой задаче **не выполняются**.
