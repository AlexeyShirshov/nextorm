# TODO: Проекция join в пользовательский тип (`As`) и снятие потолка арности
> Tracking issue: [#76](https://github.com/AlexeyShirshov/nextorm/issues/76).

> Рабочий план (design RFC). Источник: PoC в изолированных worktree
> `/home/alex/sources/nextorm-worktrees/poc-projmapping` (Option B) и
> `/home/alex/sources/nextorm-worktrees/poc-sourcegen` (Option A, source gen).
> Связано: [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md) §6, строка 2
> «Join arity > 3 (4..8)»; текущий потолок зафиксирован в
> [`docs/guide/03-joins.md`](../../guide/03-joins.md) (+RU).

## Пункт и цель

- Проблема: результат join аккумулируется в фиксированный позиционный тип
  `Projection<T1..T8>` (`src/nextorm.core/Builders/Projection.cs`), а каждый join-метод объявлен
  per-arity в `JoinedEntityBuilder<T1..Tn>` (`src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs`).
  Следствия: (1) потолок арности 8 (compile-time), (2) промежуточная проекция называется
  `Item1..ItemN`, семантики нет, (3) после `Select` (терминального) join продолжить нельзя.
- Цель: дать пользователю **промежуточную проекцию в именованный тип** (`As`) и, как следствие,
  снять потолок арности; выбрать между безопасным sugar-вариантом (derived table) и inline
  projection-mapping.
- Критерий приёмки: `ctx.From<A>().Join(...).As(p => new { p.Item1.Id, p.Item2.Name }).Join(...)`
  компилируется на любой арности и генерирует корректный SQL на всех SQL-провайдерах; публичный
  API аддитивен; для inline-фазы — отдельный приёмочный набор на alias-identity и plan-key (см. B2).

## Контекст: результаты PoC

Измерено (harness `poc/PocHarness`, `poc/PocMap`; Release, in-process):

- `As` через derived table (B1) — SQL корректен на **SQLite, PostgreSQL, SQL Server, MySQL, MariaDB,
  ClickHouse** (см. матрицу ниже); in-memory join производного источника не поддерживает
  (`docs/guide/03-joins.md`, «the in-memory provider rejects it»).
- Inline projection-mapping (B2) — cost-center'ы: построение карты `member→source` 1.6 µs/join,
  словарный резолв +0.5 µs на 8 членов, compiled selector per-row 21 ns/32 B против `Extend`
  51 ns/72 B (`poc/PocMap`). То есть per-row inline **не хуже**; риск B2 не в производительности.
- Source gen (Option A) — runtime побайтово идентичен (аллокации ±0 на `Build_Join2/4/8` и
  `Cached_ToList`), +82 KB DLL, +~0.4 s clean build, 12-табличный join `t1..t12` корректен. Как
  отдельный вариант снятия лимита оставлен в стороне: он не улучшает эргономику (`ItemN` остаются).

## Вариант B1 — `As` через производную таблицу (низкий риск) — ВАЛИДИРОВАН

- Реализация: **один** метод на базовом билдере, per-arity код не нужен:

  ```csharp
  // EntityBuilder<TEntity>
  public EntityBuilder<TResult> As<TResult>(Expression<Func<TEntity, TResult>> selector)
      => DataProvider.From(Select(selector));
  ```

  На `JoinedEntityBuilder<T1..Tn>` (`TEntity = Projection<T1..Tn>`) он даёт проекцию в `TResult`, а
  `From(QueryCommand<TResult>)` (`src/nextorm.core/DataContext/DataContextExtensions.cs:959`)
  оборачивает результат в производную таблицу, которую можно снова `Join`/`Where`/`Select`.
- Плюсы: переиспользует существующий, покрытый тестами путь `From(derived).Join(...)`; план-кэш и
  alias-резолвинг не трогаются; работает на всех диалектах; API аддитивен.
- Ограничения: лишний уровень вложенности (обычно схлопывается оптимизатором СУБД); после `As`
  доступны только спроецированные члены (не «любая колонка»); outer-join семантика меняется
  (проекция становится границей материализации); in-memory не поддерживает join derived-source.
- Диалект-план: изменений в `ISqlDialect`/`Make*` **не требуется** (derived-table рендерит
  `SqlSourceRenderer`); in-memory — либо `NotSupportedException` с явным сообщением, либо отдельная
  задача на derived-source join.

### Совместимость: старая проекция сохраняется, лимит арности снимается без source gen

- B1 **чисто аддитивен**: `Projection<T1..T8>`, `Item1..Item8`, `IExtendableProjection`,
  `JoinedEntityBuilder<T1..T8>` и позиционный резолвинг остаются без изменений. `As` объявляется
  **один раз** на базовом `EntityBuilder<TEntity>` и наследуется всеми `JoinedEntityBuilder<T1..Tn>` —
  per-arity код, правки `Projection.cs` и wire'инг генератора не нужны.
- В отличие от source-gen PoC (где терминал арности-8 приходилось **открывать**: добавлять
  `Extend`/`Join`), здесь рукописные типы арности 2..8 не трогаются вообще.
- Практический потолок 8 снимается **без** source gen: после `JoinedEntityBuilder<T1..T8>` вызов
  `.As(p => new { ... })` даёт `EntityBuilder<TResult>`, следующий `.Join(9-й)` — снова
  `JoinedEntityBuilder<TResult, T9>` (арность 2 со стартовым типом `TResult`), и цепочка идёт ещё до
  8. Каждый `As` «сбрасывает» счётчик через производную таблицу, так что достижимо 8 + 8 + … без
  изменения публичной поверхности `Projection`.
- Единственная семантическая плата за это: после `As` в следующем `ON`/`WHERE` доступны только
  **спроецированные** члены (колонки производной таблицы), а не «любая колонка» ранних источников.
  Для ≤8 прямой путь с `ItemN` сохраняет полный доступ к источникам.
- Итог: старую проекцию оставляем как есть; B1 даёт и эргономику (`p.Order` вместо `p.Item1`), и
  арность > 8, не требуя ни генератора, ни переписывания позиционной модели.

## Вариант B2 — inline projection mapping (высокий риск, опционально)

Цель — тот же `As`, но без вложенного подзапроса (алиасы источников остаются в одном scope). Это
замена позиционной модели проекций на карту `member → (source, column)` и она упирается в:

| # | Опасность | Доказательство | Severity |
|---|---|---|---|
| H1 | Карта не входит в ключ плана → кэш отдаёт чужой SQL (`As(…new P(a.Id,b.Id))` vs `new P(b.Id,a.Id)`) | `Query/QueryPlanEqualityComparer.cs` сравнивает `EntityType`/`ResultType`/выражения/joins, карты нет | критично |
| H2 | Идентичность алиаса для однотипных источников | `Query/DefaultColumnsProvider.cs` ключует по `Type` + occurrence; `PopSourceScope` документирует уже случавшийся баг с same-typed outer source (`:31-45`) | критично |
| H3 | In-memory завязан на `Projection<T>` | `DataContext/InMemoryProjectionFactory.cs:20-72` (тип по имени `NextORM.Core.Projection\`{dim}`, reflection, `miLoopJoin` на `IProjection`), `InMemoryQueryBuilder.cs:244-…` | высокий |
| H4 | Позиционные допущения | `TypeExtensions.TryGetProjectionDimension` = generic arity (`:44-53`); `AliasFromProjectionVisitor.cs:28-39` (парсинг цифр); `QueryPlanner.cs:524` (`GetProperty("Item1")`); `MemberTranslator.cs:116` | средний |

Вывод PoC: B2 — это **performance-оптимизация поверх B1**, а не условие существования фичи.
Пускаться в неё стоит только если замеры на реальных СУБД покажут, что вложенность критична.

## Провайдерная матрица

`As` (B1) — SQL-генерация проверена PoC (`poc/PocMap/Program.cs`, `ProbeAs`), кроме указанного:

| Провайдер | `As` + последующий `Join` | Форма |
|---|---|---|
| SQLite | ✅ | `... from (select t1.id as 'A', t2.requiredString as 'B' from ...) as 't3' join complex_entity as 't4' on t3.A = cast(t4.id as integer)` |
| PostgreSQL | ✅ | `... as "t3" ... on t3."A" = cast(t4.id as integer)` |
| SQL Server | ✅ | `... as [t3] ... on t3.[A] = cast(t4.id as int)` |
| MySQL | ✅ | `... as \`t3\` ... on t3.\`A\` = cast(t4.id as signed)` |
| MariaDB | ✅ | то же, что MySQL |
| ClickHouse | ✅ | `... as \`t3\` ... on t3.\`A\` = cast(t4.id as Int32)` |
| In-memory | ❌ (ожидаемо) | join производного источника не поддержан (`NotSupportedException`) |

Единообразие: `As` — кросс-провайдерный sugar, новых диалектных флагов нет. In-memory — отдельная
задача (или явный отказ на первом `Join` после `As`).

## Критерии приёмки

1. `As` доступен на любом `EntityBuilder<T>` (включая `JoinedEntityBuilder<T1..Tn>`), без per-arity
   дублирования; публичный API аддитивен, `CS1591` — с XML-доком.
2. SQL-generation тесты на всех 6 SQL-провайдерах: `As` + повторный `Join`/`Where`, снимок SQL.
3. `CommonTestSuite.Join.cs` (+RU) — прогон на реальных БД (Testcontainers), проверка результата.
4. In-memory: тест на `NotSupportedException` (B1) либо реализация derived-source join (отдельно).
5. Покрытие line ≥ `MIN_LINE_COVERAGE`; `dotnet build nextorm.sln -c Release` — 0/0.
6. Обновление `docs/guide/03-joins.md` (+`docs/ru/guide/03-joins.md`) и `docs/guide/06-subqueries.md`,
   где уместно; ссылки на `docs/specs/**` из публичных доков не добавлять.

## Шаги

1. Добавить `EntityBuilder<TEntity>.As<TResult>` (B1) + XML-док; проверить, что не конфликтует с
   `Select`/`From` перегрузками.
2. SQL-generation тесты на 6 провайдерах (`tests/nextorm.<p>.tests/SqlGenerationTests.cs`), проверить
   корректность алиасов `tN` и отсутствие коллизий при однотипных источниках.
3. `CommonTestSuite.Join.cs` — поведенческий тест на контейнерах (см. скилл
   `running-integration-tests`).
4. In-memory: решить (отказ vs реализация), добавить тест.
5. Док EN+RU (`docs/guide/03-joins.md`, `docs/ru/...`), обновить `Projection.cs` XML-док о потолке.
6. Закрыть строку в `sql-capabilities-gap-analysis.md` §6 и удалить этот файл (скилл
   `implementing-todo-features`). При выборе B2 — отдельный todo с тестами H1/H2 и переписыванием
   in-memory (H3).

## Открытые вопросы

1. Семантика outer-join после `As`: принимаем «проекция = граница» (derived table) или нужно
   сохранять доступ к непроецированным колонкам (тогда только B2)?
2. Нужен ли overload `As((e1, e2) => new {...})` для запуска нового join (сахар над `Item1/Item2`),
   или достаточно `As(p => new { p.Item1.…, p.Item2.… })`?
3. In-memory: отказ или реализация derived-source join (отдельная оценка)?
4. Пускаться ли в B2 когда-либо, и если да — по какому замеру вложенности на реальных СУБД.

## Файлы к изменению (B1)

- `src/nextorm.core/Builders/EntityBuilder.cs` — метод `As` (+XML-док).
- `src/nextorm.core/Builders/Projection.cs`, `src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs`
  — док о потолке арности (не код).
- `tests/nextorm.<p>.tests/SqlGenerationTests.cs` (6 провайдеров),
  `tests/nextorm.integration.tests/CommonTestSuite.Join.cs`, `tests/nextorm.core.tests` (in-memory).
- `docs/guide/03-joins.md` + `docs/ru/guide/03-joins.md`; `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §6.

## Риски

- B1: низкий — только аддитивный метод; риск регрессий близок к нулю (переиспользует проверенный
  derived-table путь). Основной вопрос — продуктовая семантика outer-join (см. открытый вопрос 1).
- B2: высокий — H1–H4; без тестов на alias-identity и plan-key даёт трудноуловимые ошибки SQL из кэша.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **дизайн-здоров, 0 блокеров**; 1 параметр риска (вложенность) и 1 тест-пробел.

- **[PERF] 🟡** `As` всегда оборачивает в производную таблицу, даже когда join'ов нет (`:45`, `:69-78`); план признаёт «лишний уровень… схлопывается оптимизатором» (`:54`) — не измерено, но факт «обёртка строится всегда» есть. Deferred с триггером: замер на реальных СУБД, план-пункт (открытый вопрос 4, `:144`), либо рендер `As` без derived table при отсутствии `_joins`/`_query`.
- **[LSP]/[TYPE] 🟡** Смена семантики outer-join после `As` не покрыта приёмочным тестом (`:74-76,118`): после `As` доступны только спроецированные члены, `ResolveJoinBase` перестаёт срабатывать (`EntityBuilder.cs:1302-1304`). Fix: регресс `LeftJoin → As(subset) → Join` на корректность NULL-семантики + правило в XML-doc `As`.
- **[TYPE] ℹ️** Имя `As` омонимично семейству `TableAlias.AsInt/AsString` (приведение). Публичной коллизии нет. Deferred (заморозка `PublicAPI`): добавить `<remarks>` о различии, пересмотреть имя только если до заморозки.
- **[SRP] ℹ️** Позитив: `As` не вводит ни типа, ни интерфейса, не трогает `ISqlDialect`/диалекты (инварианты 1/7); B2 корректно отложен с триггером (`:92-93`).
