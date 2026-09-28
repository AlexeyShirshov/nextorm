# TODO: глобальные фильтры запросов (query filters) по образцу linq2db / EF Core
> Tracking issue: [#67](https://github.com/AlexeyShirshov/nextorm/issues/67).

> Рабочий план (design RFC). Источники: **G? (extensibility)** из
> [`linq2db-comparison.md`](../comparison/linq2db-comparison.md:96) («Extensibility (interceptors,
> custom SQL, query filters): extensive vs minimal»), out-of-scope-строка `linq2db#4543` в
> [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md:73) и фаза 2
> расширяемости (этот план). Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

## 0. Статус реализации (обновлено 28.09.2026)

- **Фаза 1 — shipped** (issue #67, ветка `1.0.9-a`): fluent `HasQueryFilter` (обе перегрузки), атрибут
  `[QueryFilter(FilterLambda = nameof(...))]`, применение к основному источнику/join-ам/подзапросам,
  контекст как runtime-параметр (идентичность фильтра в ключе плана), `IgnoreFilters()`, in-memory
  parity. Публичный API — `QueryFilterAttribute`, `IQueryFilterMetadata`,
  `EntityMetadataBuilder<T>.HasQueryFilter`, `EntityBuilder<T>.IgnoreFilters`; доки EN+RU —
  [`advanced/query-filters.md`](../../advanced/query-filters.md).
- **Фаза 2 — PR1–PR3 отгружены, PR4 отложен (актуализировано 28.09.2026, issue
  [#108](https://github.com/AlexeyShirshov/nextorm/issues/108)):** keyed-фильтры
  (`HasQueryFilter(string, …)`, `FilterKey`), селективный `IgnoreFilters` (по типам/ключам) и
  фильтры на DML (`UPDATE`/`DELETE` — 1:1 с linq2db; `INSERT`/`MERGE` — цель без фильтра плюс
  **валидация** вставляемых строк + `QueryFilterException`). **PR4 `FilterFunc` отложен** — spike
  (D5) признал форму реализуемой: контракт — fluent
  `HasQueryFilter([key,] Func<EntityBuilder<T>, IDataContext, EntityBuilder<T>>)` плюс атрибут с
  именем статического члена; предикат должен ссылаться на `IDataContext`/`SqlParameters`, чтобы
  значения оставались параметризованными; форма «снимок в локальную переменную» запрещена —
  ломает план-кэш и in-memory. Детали — §11.6, §11.13.
- **Фаза 3 — отложена:** EF Core bridge (проброс keyed-фильтров EF Core 10).

## 1. Пункт и цель

- **Фича:** фильтр-предикат, привязанный к **типу сущности** в mapping-метаданных, который nextorm
  автоматически подмешивает в каждый запрос, где сущность участвует (основной `FROM`, join-источники,
  подзапросы). Аналог EF Core global query filters и linq2db query filters.
- **Критерий приёмки:** объявленный на `From<T>(m => m.HasQueryFilter(...))` (или атрибутом `[QueryFilter]`)
  предикат:
  1. применяется к запросам над `T` без явного `Where`;
  2. применяется к `T` в join-ах и подзапросах;
  3. может читать `IDataContext` (tenant, флаг soft-delete) и корректно участвует в план-кэше;
  4. выключается на конкретном запросе через `IgnoreFilters()`;
  5. воспроизводится в in-memory провайдере;
  6. не ломает нулевую аллокацию при отсутствии фильтров.

## 2. Почему это нужно

1. Soft-delete и multi-tenancy — типовые требования; сейчас их приходится писать вручную в каждом
   `Where` (легко забыть → утечка данных другого тенанта).
2. linq2db (`HasQueryFilter`/`[QueryFilter]`, `IgnoreFilters`) и EF Core (`HasQueryFilter`,
   `IgnoreQueryFilters`, keyed-фильтры в EF10) это умеют — у nextorm нет.
3. Разрыв по расширяемости уже зафиксирован в сравнении; query filters — последний крупный элемент
   extensibility-блока (после [интерцепторов](../../guide/25-interceptors.md), фаза 1 которых отгружена).

## 3. Текущее состояние (проверено по коду)

- Точка входа запроса: `DataContextExtensions.From<T>(Action<EntityMetadataBuilder<T>>?)` —
  `DataContextExtensions.cs:729-737`. Метаданные строятся **один раз** и кладутся в статический
  кросс-процессный кэш `DataContextCache.Metadata[typeof(T)]`; `configEntity` выполняется только при
  первом обращении к типу.
- Метаданные не имеют слота под фильтр: `IEntityMetadata` (`Meta/IEntityMetadata.cs`) знает только
  `Properties` и `TableName`; fluent-поверхность `EntityMetadataBuilder<T>` — `Table`/`Property`.
- `EntityBuilder` умеет только явный `Where` (`Builders/EntityBuilder.cs:1764`); авто-инъекции нет.
- План-кэш: `DataContext/Cache/QueryPlanStore.cs`, ключ — `Query/QueryPlanEqualityComparer.cs`;
  в ключе сейчас выражение запроса, а не состояние контекста.
- Per-context scoping отсутствует: фильтр, зашитый в статические `DataContextCache.Metadata`, был бы
  общим на процесс, поэтому контекстно-зависимый фильтр должен применяться **на построении плана**, а
  не храниться как константа.
- In-memory провайдер: `DataContext/InMemoryDataContext.cs` + `InMemoryJoin.cs` — фильтр нужно
  применять и здесь, иначе семантика SQL/in-memory разойдётся.
- Глобальная конфигурация живёт в `DI/DataContextBuilder.cs` (`UseNamingConvention`, `UseKeywordCase`,
  `CreateDataContext`).

## 4. Как это устроено в linq2db (референс)

| Аспект | linq2db |
|---|---|
| Декларация | fluent `EntityMappingBuilder<T>.HasQueryFilter(...)`; атрибут `QueryFilterAttribute` (`FilterLambda` / `FilterFunc`, `FilterKey`) |
| Формы | предикат `Expression<Func<T,bool>>` / `Expression<Func<T,IDataContext,bool>>`; функция `Func<IQueryable<T>,IDataContext,IQueryable<T>>` |
| Применение | `TableBuilder.ApplyQueryFilters` — ко всем обращениям к типу: `ITable<T>`, join, подзапрос; несколько фильтров — AND |
| Контекст | лямбда получает `IDataContext` (tenant, флаг soft-delete) |
| Именованные (свежее) | `HasQueryFilter(string filterKey, …)`, `QueryFilterAttribute.FilterKey`, выборочный `IgnoreFilters(IEnumerable<string>, params Type[])`; keyed-фильтры EF Core 10 пробрасываются через `linq2db.EntityFrameworkCore` |
| Отключение | `IgnoreFilters()` / `IgnoreFilters(params Type[])` / именованный overload |
| Кэш | динамические выражения регистрируются аксессором (`RegisterDynamicExpressionAccessor`), иначе кэш вернёт неверный результат |

EF Core: `modelBuilder.Entity<T>().HasQueryFilter(e => ...)`, `IgnoreQueryFilters()`, в EF10 — keyed
(`HasQueryFilter("key", …)`).

## 5. Дизайн и публичный API (предложение)

```csharp
// атрибут — рядом с SqlTableAttribute
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = true)]
public sealed class QueryFilterAttribute : Attribute
{
    public string? FilterKey { get; set; }
    public LambdaExpression? FilterLambda { get; set; }   // (T, IDataContext) => bool
    public LambdaExpression? FilterFunc { get; set; }     // (IQueryable<T>, IDataContext) => IQueryable<T>
}

// fluent — на EntityMetadataBuilder<T>
public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, bool>> filter);
public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, IDataContext, bool>> filter);
public EntityMetadataBuilder<T> HasQueryFilter(string filterKey, Expression<Func<T, IDataContext, bool>> filter);

// отключение — на EntityBuilder<T> (и, при DML-scope, на мутациях)
public EntityBuilder<T> IgnoreFilters();
public EntityBuilder<T> IgnoreFilters(params Type[] entityTypes);
public EntityBuilder<T> IgnoreFilters(IEnumerable<string> filterKeys, params Type[] entityTypes);
```

- Хранение: `IEntityMetadata.Filters` — новый read-only список `IQueryFilterMetadata`
  (`{ string? Key; LambdaExpression Lambda; }`); заполняется из атрибута или fluent-вызова.
- Применение: на этапе подготовки плана (`QueryCommand.QueryPreparer` / `QueryPlanner`) к каждому
  `FromExpression`, указывающему на сущность с фильтрами, — как `Where` с `(entity, IDataContext)`.
  Основной источник и join-источники обрабатываются одинаково.
- In-memory: тот же предикат применяется к последовательности до проекции/join.
- Совместимость: extend-only; при отсутствии фильтров путь не аллоцирует (проверка `Filters.Count == 0`).

## 6. Критично: план-кэш и per-context scoping

> **Уточнено 28.09.2026 (§11.3).** Пункт 1 ниже («ключ должен включать набор `IgnoreFilters`»)
> реализован иначе: фильтры инжектятся в `_condition`/`ON` **до** вычисления `WherePlanHash`/
> `JoinPlanHash`, поэтому эффективный набор уже в ключе; отдельного поля в `QueryPlanEqualityComparer`
> не добавляем. Disabled-set — состояние команды, а не ключ. Остальные пункты §6 действуют.

Это главный риск (и причина, по которой фича вынесена из [интерцепторов](../../guide/25-interceptors.md) в отдельный план):

1. **Идентичность фильтра в ключе плана.** `QueryPlanEqualityComparer` должен включать набор активных
   фильтров (ключи/хэши лямбд) и набор `IgnoreFilters`. Иначе запрос из контекста A переиспользует
   план контекста B. *(См. уточнение выше: в реализации это покрывается инжектированными
   выражениями.)*
2. **Контекстно-зависимые значения — параметры, не константы.** `dc.TenantId`/`dc.IsSoftDeleteEnabled`
   должны становиться SQL-параметрами с динамическим аксессором (аналог linq2db
   `RegisterDynamicExpressionAccessor`), иначе кэшированный SQL зафиксирует первое значение.
3. **Статический `DataContextCache.Metadata`.** Метаданные кэшируются на процесс (`DataContextExtensions.cs:731-735`),
   поэтому хранить в них нужно **лямбду**, а не вычисленные значения; привязка к контексту —
   на построении плана.
4. **Нулевая цена.** Пустой список фильтров не должен добавлять ветвлений/аллокаций в горячий путь
   (см. требования интерцепторов, [гайд 27](../../guide/25-interceptors.md)).

## 7. Этапы внедрения

- **Фаза 1 (MVP) — shipped (27.09.2026):** анонимный предикат `(entity, IDataContext) => bool` через fluent и `[QueryFilter]`;
  применение к основному источнику, join-ам и подзапросам; `IgnoreFilters()` / `IgnoreFilters(params Type[])`;
  in-memory; идентичность фильтра в ключе плана; динамические параметры. Закрывает soft-delete и
  multi-tenancy.
- **Фаза 2:** keyed-фильтры, селективный `IgnoreFilters` (типы × ключи), DML-фильтры и валидация
  `INSERT`, `FilterFunc` над `EntityBuilder<T>` — полный дизайн в §11.
- **Фаза 3:** EF Core bridge — проброс keyed-фильтров EF Core 10 (смежно `todo_efcore_integration.md`).
- **Вне области:** фильтры на DML (`INSERT`/`UPDATE`/`DELETE`) в MVP — отдельное решение (риск
  неожиданной потери строк); фильтры на `FromSql`/raw-источники.

## 8. План тестов

- Core (`tests/nextorm.core.tests/QueryFilterTests.cs`): применение к основному источнику; к join-у
  (обе стороны); к подзапросу; AND при нескольких фильтрах; `IgnoreFilters()` и по типу; фильтр,
  читающий `IDataContext`.
- План-кэш: два контекста с разными `TenantId` — либо разные планы, либо параметры (не константы);
  смена динамического значения между вызовами даёт корректный результат.
- In-memory: тот же набор (soft-delete, tenant) на `InMemoryTests`.
- SQL-gen (`tests/nextorm.*.tests/SqlGenerationTests.cs`): наличие `WHERE`-предиката фильтра,
  параметризация, отсутствие предиката при `IgnoreFilters()`.
- Интеграция (`CommonTestSuite`): soft-delete и multi-tenancy на PostgreSQL/SQL Server/MySQL/MariaDB/SQLite;
  `*SpecificTests.cs` — отличия.
- Покрытие: не ниже базового (`MIN_LINE_COVERAGE` = 75%); новые файлы в core учитываются
  `coverage.settings.xml`.

## 9. Открытые вопросы — закрыты 28.09.2026 (см. §11)

1. **Scope DML** — решён: `UPDATE`/`DELETE` — фильтр в `WHERE`; `INSERT`/`MERGE`/`UPSERT` — цель без
   фильтра + валидация вставляемых строк (§11.4, §11.5).
2. **Ключ плана** — решён: отдельного поля нет; эффективный набор фильтров уже в
   `WherePlanHash`/`JoinPlanHash`, т.к. фильтры инжектятся до хэширования (§11.3).
3. **Источник контекста** — без изменений: `IDataContext` через `QueryFilterContext` (фаза 1).
4. **Nav-подгрузки/`JoinInto`** — вне этого цикла (пересечение с работой по навигациям; учесть при
   интеграции).
5. **`FilterFunc`** — решён: форма `Func<EntityBuilder<T>, IDataContext, EntityBuilder<T>>` (нет
   `IQueryable`); механизм мержа — spike (§11.6).
6. **Инвалидация метаданных** — не меняем: process-global, first-registration-wins (ограничение
   документируется, §11.7).
7. **Нейминг** — не переименовываем (`QueryFilterAttribute`, `HasQueryFilter`); новые имена внести в
   `API-NAMING-REVIEW.md`.

## 10. Файлы к изменению

- Новое: `src/nextorm.core/Meta/QueryFilterAttribute.cs`, `Meta/IQueryFilterMetadata.cs`,
  `Visitors/QueryFilterExpressionVisitor.cs` (или ветка в подготовке плана).
- Правки: `DataContext/Meta/IEntityMetadata.cs`, `Meta/EntityMetadataBuilder.cs`,
  `DataContext/DataContextExtensions.cs` (`From<T>`), `Query/QueryCommand.QueryPreparer.cs`,
  `DataContext/QueryPlanner.cs`, `Query/QueryPlanEqualityComparer.cs`, `DataContext/Cache/QueryPlanStore.cs`,
  `Builders/EntityBuilder.cs` (`IgnoreFilters`), `DataContext/InMemoryDataContext.cs`,
  `DataContext/InMemoryJoin.cs`, `DI/DataContextBuilder.cs` (регистрация/дефолты).
- Доки: `docs/querying/index.md` (+RU) или новый гайд `docs/guide/26-query-filters.md`
  (+RU), `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/providers/overview.md` (+RU), `comparison/linq2db-comparison.md` (EN+RU),
  `comparison/linq2db-backlog-gap-analysis.md`, `specs/design/API-NAMING-REVIEW.md`.

## 11. Фаза 2 — дизайн (согласовано 28.09.2026)

> Сверено с linq2db `Source/LinqToDB/Internal/Linq/Builder/*` (commit `7c5d5b5`) и с кодом nextorm
> фазы 1. Tracking: #67; workstream 33 в `sql-capabilities-gap-analysis.md`.
>
> **Статус (28.09.2026, #108).** **PR1–PR3 отгружены**: keyed-фильтры + `FilterKey`, селективный
> `IgnoreFilters` (типы/ключи), DML-фильтры (`UPDATE`/`DELETE`, включая key-формы), валидация
> INSERT/MERGE + `QueryFilterException`; SQL-gen и integration на всех 5 SQL-провайдерах
> (PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse) плюс SQLite, доки EN+RU. **PR4 (`FilterFunc`,
> §11.6) отложен** — spike признал форму реализуемой (см. §0). §11.7 в силе. Follow-ups
> `_sorting`/`FindProperty` — deferred с триггером (§11.13).

### 11.1 Объём
Keyed-фильтры; селективный `IgnoreFilters` (типы × ключи); DML-фильтры (1:1 с linq2db + наш opt-out);
валидация `INSERT` (наш дефект сверх linq2db); `FilterFunc` над `EntityBuilder<T>`.

### 11.2 Публичный API

```csharp
namespace NextORM.Core;

/// Ключ фильтров, объявленных без явного ключа.
public static class QueryFilters { public const string AnonymousKey = ""; }

[AttributeUsage(Class | Interface, AllowMultiple = true, Inherited = true)]
public sealed class QueryFilterAttribute : Attribute
{
    public string? FilterKey { get; set; }     // null → анонимный
    public string? FilterLambda { get; set; }  // имя статического члена (предикат)
    public string? FilterFunc { get; set; }    // имя статического члена (builder-функция)
}

// EntityMetadataBuilder<T>
public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, bool>> filter);
public EntityMetadataBuilder<T> HasQueryFilter(Expression<Func<T, IDataContext, bool>> filter);
public EntityMetadataBuilder<T> HasQueryFilter(string filterKey, Expression<Func<T, IDataContext, bool>> filter);
public EntityMetadataBuilder<T> HasQueryFilter(Func<EntityBuilder<T>, IDataContext, EntityBuilder<T>> filter);
public EntityMetadataBuilder<T> HasQueryFilter(string filterKey, Func<EntityBuilder<T>, IDataContext, EntityBuilder<T>> filter);

// EntityBuilder<T> и DML-билдеры (UpdateBuilder/DeleteBuilder/UpdateJoinBuilder)
public EntityBuilder<T> IgnoreFilters();
public EntityBuilder<T> IgnoreFilters(params Type[] entityTypes);
public EntityBuilder<T> IgnoreFilters(IEnumerable<string> filterKeys);
public EntityBuilder<T> IgnoreFilters(IEnumerable<string> filterKeys, params Type[] entityTypes);

public interface IQueryFilterMetadata
{
    string Key { get; }                  // анонимный → QueryFilters.AnonymousKey
    LambdaExpression? Lambda { get; }    // null у func-only
    LambdaExpression? Func => null;      // DIM
}

public sealed class QueryFilterException : InvalidOperationException { /* валидация DML */ }
```

Правила:
- **Анонимные:** `Key == QueryFilters.AnonymousKey`; повторы **множественны и AND** (без регрессии
  фазы 1); `IgnoreFilters([QueryFilters.AnonymousKey])` гасит все анонимные.
- **Именованные:** ключ — слот; повтор в одной конфигурации заменяет, `null` удаляет (linq2db);
  derived-overrides-base — по мере выразимости в process-global модели.
- **`IgnoreFilters`:** пустой/`null` список ключей не отключает ничего; вызовы накапливаются
  (объединение).

### 11.3 Пайплайн и план-ключ

- `bool _ignoreFilters` → неизменяемый `QueryFilterScope { bool All; IReadOnlySet<string> Keys;
  IReadOnlySet<Type> Types; }` на `EntityBuilder<T>`, `QueryCommand` и DML-билдерах.
- `QueryCommand.QueryPreparer.GetFilters(cmd, type)`: пустой scope → все объявленные; `All` → ни
  одного; иначе отбрасываем фильтр, если `Types.Contains(type)` **или** (`Keys.Contains(filter.Key)`
  и (типов нет **или** `Types.Contains(type)`)).
- Инжект как в фазе 1: основной источник → `_condition`, join → `ON`, подзапрос → при подготовке
  своего команды.
- **План-ключ не трогаем:** фильтры инжектятся до `WherePlanHash`/`JoinPlanHash`, поэтому эффективный
  набор уже в ключе; разные scope с одинаковым SQL делят план. Disabled-set — состояние команды, не
  поле ключа. Тест: два scope → правильные строки, без кросс-загрязнения; повтор → cache hit.

### 11.4 DML — 1:1 с linq2db

| Операция | Фильтр | Реализация |
|---|---|---|
| `SELECT` main/join/subquery | применяется | фаза 1 |
| `UPDATE` предикат | в `WHERE` | уже: `From<T>().Where(...)` → `RenderPredicate` |
| `UPDATE` key/entity | в `WHERE` (key ∧ filter) | **добавить** AND фильтров к key-предикату |
| `DELETE` предикат | в `WHERE` | уже |
| `DELETE` key/entity | в `WHERE` (key ∧ filter) | **добавить** |
| `INSERT` value/entity | цель **НЕ** фильтруется | как есть (нет `From<T>`) |
| `INSERT…SELECT` | источник фильтруется, цель — нет | как есть |
| `MERGE`/`UPSERT` | цель **НЕ** фильтруется | как есть |

- `IgnoreFilters(scope)` — на `UpdateBuilder`, `DeleteBuilder`, `UpdateJoinBuilder` и key-формах; для
  предикатной формы прокидывается в `From<T>()`, для key-форм — в составной предикат.
- План-кэш DML: фильтры уже в prepared condition/составном предикате → в ключе; добавить DML
  cache-тест.

### 11.5 Валидация `INSERT` (сверх linq2db)

- entity/value и batch: активные фильтры цели (минус scope) проверяются in-memory против вставляемых
  значений; нарушение → `QueryFilterException` **до** выполнения.
- `BulkCopy`: строки в памяти — та же проверка.
- `INSERT…SELECT` и source-derived `MERGE`: pre-check `EXISTS(... NOT filter)` по source; нарушение →
  исключение, вставка не выполняется; внутри транзакции — иначе документируем гонку (либо требуем
  транзакцию).
- `MERGE`/`UPSERT` insert-ветка: те же правила.
- `IgnoreFilters` исключает фильтры и из валидации; `FilterFunc` участвует (см. §11.6).

### 11.6 `FilterFunc`

- Форма: `Func<EntityBuilder<T>, IDataContext, EntityBuilder<T>>` (nextorm не реализует `IQueryable`).
- Применение на планировании: билдер поверх source → применить func → слить результат. Where-only
  merge ≡ предикат; richer (join/select/group) требует wrap источника в derived table.
- **Обязателен spike** на механизм мержа и поддерживаемый subset; минимально Where-only, richer — по
  итогам. Требование чистоты (без side-effect'ов).
- Валидация `FilterFunc` для in-memory строк — по итогам spike.

### 11.7 Ограничения

- Метаданные process-global, first-registration-wins — не меняем (повторный `From<T>(cfg)` теряется).
- `FilterFunc` subset — по итогам spike.
- Валидация `INSERT…SELECT` вне транзакции — возможна гонка (документировать).
- Named slot replace / derived-override — насколько выразимо в process-global модели.

### 11.8 Приёмка (матрица)

| Кейс | SQL (PG/MSSQL/MySQL/MariaDB/CH/SQLite) | In-memory |
|---|---|---|
| keyed select / ignore-by-key / derived / null-remove | SQL-gen + `CommonTestSuite` | `QueryFilterTests` |
| anonymous constant + `IgnoreFilters([AnonymousKey])` | SQL-gen + `CommonTestSuite` | `QueryFilterTests` |
| `IgnoreFilters` types / keys / intersection / empty | SQL-gen + `CommonTestSuite` | `QueryFilterTests` |
| scope в план-кэше (различает/шарит) | core cache test | core cache test |
| `UPDATE`/`DELETE` фильтр в `WHERE` | SQL-gen + `CommonTestSuite` | — |
| `UPDATE`/`DELETE` key-форма ∧ фильтр | SQL-gen + `CommonTestSuite` | — |
| `INSERT`/`MERGE` цель без фильтра | SQL-gen + `CommonTestSuite` | — |
| `INSERT…SELECT` source фильтруется | SQL-gen + `CommonTestSuite` | — |
| валидация entity/value/batch/bulk | unit + `CommonTestSuite` | in-memory rows |
| валидация `INSERT…SELECT` (pre-check) | unit + `CommonTestSuite` | — |
| `FilterFunc` | по итогам spike | по итогам spike |

### 11.9 Тесты

- Core `tests/nextorm.core.tests/QueryFilterTests.cs` — матрица (keyed/ignore/derived/null), DML,
  валидация, план-кэш.
- Провайдерные `tests/nextorm.<p>.tests/QueryFilterSqlGenerationTests.cs` — наличие/отсутствие
  предиката, параметризация, DML.
- Интеграция `tests/nextorm.integration.tests/CommonTestSuite.QueryFilter*.cs` — soft-delete, tenant,
  DML, валидация, на PostgreSQL/SQL Server/MySQL/MariaDB/SQLite/ClickHouse.
- Покрытие ≥ `MIN_LINE_COVERAGE`.

### 11.10 Documentation

- `docs/advanced/query-filters.md` + `docs/ru/advanced/query-filters.md`
- `docs/advanced/limitations.md` + RU
- `docs/advanced/api-reference.md` + RU (если перечисляет фильтры)
- `docs/specs/design/API-NAMING-REVIEW.md` — новые имена
- `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §33
- `docs/specs/comparison/linq2db-comparison.md` (EN+RU)
- (опц.) новый гид `docs/guide/26-query-filters.md` + RU

### 11.11 PR

1. **PR1** — keyed + selective `IgnoreFilters` (SELECT): API, `QueryFilterScope`, интроспекция, тесты.
2. **PR2** — DML-фильтры (`IgnoreFilters` на DML + key-формы 1:1) + тесты.
3. **PR3** — валидация `INSERT` (entity/value/batch/bulk + `INSERT…SELECT` pre-check + `MERGE`) +
   `QueryFilterException`.
4. **PR4** — `FilterFunc` (после spike).

### 11.12 Файлы (фаза 2)

- Правки: `Builders/EntityBuilder.cs`, `DataContext/Meta/EntityMetadataBuilder.cs`,
  `DataContext/Meta/QueryFilterAttribute.cs`, `DataContext/Meta/IQueryFilterMetadata.cs`,
  `DataContext/Meta/Implementation/QueryFilterMetadata.cs`, `Query/QueryCommand.QueryPreparer.cs`
  (`GetFilters`), `Query/QueryFilterContext.cs`, `Builders/{UpdateBuilder,DeleteBuilder,UpdateJoinBuilder}.cs`,
  `DataContext/SqlMutationBuilder.cs` и DML-исполнители (hook валидации).
- Новое: `DataContext/Meta/QueryFilterScope.cs`, `QueryFilterException.cs`, валидатор `INSERT`.

### 11.13 Follow-ups — Deferred + триггер (после PR1–PR3)

Оба пункта найдены в ACT цикла 4b (#108) при переносе статус-файла; зеркало —
`docs/specs/design/code-smells-review.md`, «Аудит цикла 4b (#108, query filters Фаза 2, 2026-09-28)».
Открытых P0/P1 нет; обе — 🟡 P2.

- **`_sorting` разделяется по ссылке между клонами `QueryCommand`.** `QueryCommand._sorting`
  присваивается из `definition.Sorting` без копии (`Query/QueryCommand.cs:163`), тогда как `_joins`
  уже клонируется (`Query/QueryCommand.cs:161`); `PrepareSorting` пишет `sort.PreparedExpression` на
  месте по `ref` (`Query/QueryCommand.QueryPreparer.cs:829`), поэтому подготовка одного клона
  переписывает `PreparedExpression` сортировки исходной команды и соседних клонов — тот же класс,
  что исправленная утечка `_joins`. **Триггер:** воспроизводимая утечка `PreparedExpression` между
  командами или любое влияние на фильтры/результаты.
- **`FindProperty` сравнивает `PropertyInfo` по ссылке в остальных билдерах/трансляторах.**
  `UpdateBuilder.FindProperty` (`Builders/UpdateBuilder.cs:383`), `DeleteBuilder.FindProperty`
  (`Builders/DeleteBuilder.cs:279`), `BulkInsertBuilder.FindProperty`
  (`Builders/BulkInsertBuilder.cs:498`), `UpdateJoinBuilder.FindProperty`
  (`Builders/UpdateJoinBuilder.cs:215`), `EntityMetadata.FindProperty`
  (`DataContext/Meta/Implementation/EntityMetadata.cs:42`), `MemberTranslator.FindProperty`
  (`Visitors/MemberTranslator.cs:109,126`) — только `InsertBuilder` починен в этом цикле
  (`InsertBuilder.FindProperty`/`SameMember`, `Builders/InsertBuilder.cs:574,590`). Ломается на
  членах, объявленных в базовом типе и скрытых через `new` (lookup по ссылке не находит
  метаданные). **Триггер:** запрос/мутация, адресующая base-declared или `new`-hidden свойство,
  даёт `null`/неверную метаданную.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **нужен пересмотр — 3 блокера** (атрибут, интерфейс, привязка контекстных значений); сам план-кэш/plan-identity проработан на уровне RFC.

- **[TYPE] 🔴** Атрибутная форма невыразима: `FilterLambda`/`FilterFunc` объявлены как `LambdaExpression?` (`:75-76`), но named-аргумент атрибута не может быть лямбдой (`[QueryFilter(FilterLambda = x => …)]` не компилируется) — linq2db хранит **строку** имени. Fix: `string? FilterLambda/FilterFunc` (имя метода/свойства) либо атрибут в Фазу 2 (инвариант 5).
- **[ISP] 🔴** Ломается публичный `IEntityMetadata`: новый `IReadOnlyList<IQueryFilterMetadata> Filters` без дефолта source-breaking для внешних реализаторов, хотя рядом принят DIM-паттерн `bool IsTableNameAuto => false` (`src/nextorm.core/DataContext/Meta/IEntityMetadata.cs:27`; план `:90`). Fix: DIM `=> Array.Empty<IQueryFilterMetadata>()` **или** хранить фильтры во внутреннем реестре (`DataContextCache`), не трогая интерфейс (инварианты 5/7).
- **[DIP] 🔴** Нет механизма «контекстное значение → runtime-параметр»: план требует «`dc.TenantId` параметром, не константой» (`:105-107`) и перевод `(entity, IDataContext)` (`:92-93`), но `IDataContext` — пустой композит (F2), tenant живёт только в `IContextEnvironment.Properties` (`DataContext/Roles/IContextEnvironment.cs:26`), а существующий рантайм-параметр — `SqlFunctions.Parameter<T>` (`Query/SqlFunctions.cs:72`) + `NormParam` (`DataContext/NormParam.cs:12-22`), которые план не называет. Fix: явно зафиксировать монтирование чтений фильтра в `SqlFunctions.Parameter`-слоты на этапе планирования, иначе значения запекаются в кэшированный SQL (инвариант 3).
- **[DRY] 🟡** Метаданные кэшируются на процесс и `configEntity` выполняется один раз (`DataContext/DataContextExtensions.cs:178-189`, `DataContextCache.cs:22,30`) ⇒ фильтр, объявленный вторым `From<T>(...)`, молча теряется (Q6, `:150-151`). Deferred с триггером: второй конфиг фильтра на тот же тип; тогда ключевать по (type, config identity).
- **[LSP] 🟡** Односторонний `IgnoreFilters`: только на `EntityBuilder<T>` (`:85-88`), join/подзапросы и lower-level `QueryCommand` отключить не могут; `IgnoreFilters(params Type[])` требует per-source состояния, которого у `FromExpression` нет (`Expressions/FromExpression.cs:7-114` — sealed/immutable). Deferred: MVP — единый all-or-nothing `IgnoreFilters()`; per-type/keyed — по 2-му потребителю (инвариант 1).
- **[TYPE] 🟡** Расширяются незапечатанные публичные типы: `EntityMetadataBuilder<T>` (`DataContext/Meta/EntityMetadataBuilder.cs:12`) и `EntityBuilder<T>` (`Builders/EntityBuilder.cs:21`) — `public class` без документированной точки наследования. Fix: запечатать (или задокументировать extension point). Verify-the-Inverse: 6 unsealed в зоне против 8 sealed.
- **[DIP] ℹ️** `IgnoreFilters(IEnumerable<string>, params Type[])` (`:87`) — `params` после не-`params`. Fix: `IEnumerable<Type>?` с дефолтом.
- **[DRY] ℹ️** Устаревшие указатели §10: `Meta/IEntityMetadata.cs` → `DataContext/Meta/…`; `Builders/EntityBuilder.cs:1764` (это `WithKeywordCase`) → `Where` на `:295`/`:1721`.
