# TODO: глобальные фильтры запросов (query filters) по образцу linq2db / EF Core
> Tracking issue: [#67](https://github.com/AlexeyShirshov/nextorm/issues/67).

> Рабочий план (design RFC). Источники: **G? (extensibility)** из
> [`linq2db-comparison.md`](../comparison/linq2db-comparison.md:96) («Extensibility (interceptors,
> custom SQL, query filters): extensive vs minimal»), out-of-scope-строка `linq2db#4543` в
> [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md:73) и Фаза 2
> [`todo_interceptors.md`](todo_interceptors.md:166). Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

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
   extensibility-блока (после интерцепторов, Фаза 1 которых в `todo_interceptors.md`).

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

Это главный риск (и причина, по которой фича отложена в `todo_interceptors.md` на Фазу 2):

1. **Идентичность фильтра в ключе плана.** `QueryPlanEqualityComparer` должен включать набор активных
   фильтров (ключи/хэши лямбд) и набор `IgnoreFilters`. Иначе запрос из контекста A переиспользует
   план контекста B.
2. **Контекстно-зависимые значения — параметры, не константы.** `dc.TenantId`/`dc.IsSoftDeleteEnabled`
   должны становиться SQL-параметрами с динамическим аксессором (аналог linq2db
   `RegisterDynamicExpressionAccessor`), иначе кэшированный SQL зафиксирует первое значение.
3. **Статический `DataContextCache.Metadata`.** Метаданные кэшируются на процесс (`DataContextExtensions.cs:731-735`),
   поэтому хранить в них нужно **лямбду**, а не вычисленные значения; привязка к контексту —
   на построении плана.
4. **Нулевая цена.** Пустой список фильтров не должен добавлять ветвлений/аллокаций в горячий путь
   (см. требования интерцепторов, `todo_interceptors.md`).

## 7. Этапы внедрения

- **Фаза 1 (MVP):** анонимный предикат `(entity, IDataContext) => bool` через fluent и `[QueryFilter]`;
  применение к основному источнику, join-ам и подзапросам; `IgnoreFilters()` / `IgnoreFilters(params Type[])`;
  in-memory; идентичность фильтра в ключе плана; динамические параметры. Закрывает soft-delete и
  multi-tenancy.
- **Фаза 2:** именованные/keyed-фильтры (`filterKey`), `FilterFunc` (`IQueryable`-форма), выборочное
  отключение (`filterKeys` × entityTypes), атрибут на интерфейсных маппингах.
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

## 9. Открытые вопросы

1. Scope: фильтры только для `SELECT` или и для `UPDATE`/`DELETE` (soft-delete обычно SELECT-only)?
2. Как именно фильтр попадает в ключ плана: по ключу (для именованных) или по структурному хэшу лямбды
   (для анонимных)?
3. Источник контекстных значений: известные свойства `IDataContext` или пользовательский интерфейс
   `IQueryFilterContext`?
4. Применять ли фильтры к nav-подгрузкам — N/A (нет навигации/`Include`).
5. `FilterFunc` (`IQueryable`) — нужен ли вообще, или предиката достаточно (плюс EF Core bridge)?
6. Взаимодействие с `DataContextCache.Metadata`: как инвалидировать метаданные, если `configEntity`
   вызван позже с другим фильтром (сейчас — «только первый вызов»).
7. Именование публичного API согласовать с `API-NAMING-REVIEW.md` (атрибут `QueryFilterAttribute` vs
   `GlobalFilterAttribute`; метод `HasQueryFilter` vs `Filter`).

## 10. Файлы к изменению

- Новое: `src/nextorm.core/Meta/QueryFilterAttribute.cs`, `Meta/IQueryFilterMetadata.cs`,
  `Visitors/QueryFilterExpressionVisitor.cs` (или ветка в подготовке плана).
- Правки: `DataContext/Meta/IEntityMetadata.cs`, `Meta/EntityMetadataBuilder.cs`,
  `DataContext/DataContextExtensions.cs` (`From<T>`), `Query/QueryCommand.QueryPreparer.cs`,
  `DataContext/QueryPlanner.cs`, `Query/QueryPlanEqualityComparer.cs`, `DataContext/Cache/QueryPlanStore.cs`,
  `Builders/EntityBuilder.cs` (`IgnoreFilters`), `DataContext/InMemoryDataContext.cs`,
  `DataContext/InMemoryJoin.cs`, `DI/DataContextBuilder.cs` (регистрация/дефолты).
- Доки: `docs/guide/01-querying-and-projections.md` (+RU) или новый гайд `docs/guide/26-query-filters.md`
  (+RU), `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/providers/overview.md` (+RU), `comparison/linq2db-comparison.md` (EN+RU),
  `comparison/linq2db-backlog-gap-analysis.md`, `specs/design/API-NAMING-REVIEW.md`.
