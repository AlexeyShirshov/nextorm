# TODO: Mapping scope — scope-зависимый маппинг сущности (базовый слой)
> Tracking issue: [#65](https://github.com/AlexeyShirshov/nextorm/issues/65).

> Рабочий план (design RFC). Базовый слой без знания о шардировании. Продвинутый слой —
> [`todo_sharding.md`](todo_sharding.md) — реализует этот seam.
> Источник: обсуждение multi-tenancy / шардирования (design-спека, регистра нет).

## 1. Пункт и цель

- **Проблема:** метаданные сущности жёстко привязаны к CLR-типу: один `IEntityMetadata` на тип за
  процесс (`DataContextCache.cs:22`). Нельзя отдать для одного `TEntity` разные таблицу/схему/колонки
  одной командой в зависимости от **scope**, вычисленного из самой сущности.
- **Цель (база, ничего не знает про шардирование):**
  1. **scope, зависящий от сущности** — резолвер получает тип и (опционально) инстанс/значения свойств
     и возвращает `MappingScope`;
  2. **подмена названий** — per-scope таблица, схема и имена колонок;
  3. **префикс параметров** — per-scope префикс сгенерированных имён параметров, чтобы варианты одного
     запроса не сталкивались в SQL и кэшах.
- **Критерий приёмки:** при `MappingScope.Default` поведение и производительность не меняются
  (zero-cost); при двух scope один и тот же `From<T>()` даёт разные SQL (таблица/схема/колонки) и разные
  имена параметров; врезка резолвера и провайдера метаданных покрыта тестами.

## 2. Почему это нужно

1. Multi-tenancy «schema-per-tenant» / «table-per-tenant» без дублирования CLR-типов.
2. Точка расширения, на которую опирается шардинг (см. `todo_sharding.md`) и будущие query filters
   (`todo_interceptors.md`, Фаза 2), EF Core integration (`todo_efcore_integration.md`).
3. Изоляция имени параметра нужна для много-вариантных команд (scatter-gather в шардинге).

## 3. Текущее состояние (проверено по коду)

| Механизм | Ключ / область | Где |
|---|---|---|
| Кэш метаданных | `ConcurrentDictionary<Type, IEntityMetadata>` — только тип, процесс | `DataContextCache.cs:22`, `:30` |
| Кэш select-list | `ConcurrentDictionary<Type, SelectExpression[]>` — только тип | `DataContextCache.cs:23`, `:35` |
| Кэш имён колонок | `(PropertyInfo, INamingConvention?)` — процесс | `MemberInfoExtensions.cs:15`, `:22-43` |
| Резолв метаданных | лениво, `configEntity` один раз за процесс | `DataContextExtensions.cs:114-125`, `:665-673` |
| Построение FROM (SELECT) | `new FromExpression(entity.TableName, entity.IsTableNameAuto, t.IsInterface)` | `QueryPlanner.cs:479-481` |
| Prepare / join-update | чтение метаданных по типу | `Query/QueryCommand.QueryPreparer.cs:367`, `Builders/UpdateJoinBuilder.cs:145` |
| DML фиксирует таблицу | `_metadata.TableName!` | `InsertBuilder.cs:421`, `UpdateBuilder.cs:211`, `DeleteBuilder.cs:151`, `MergeBuilder.cs:375`, `TruncateBuilder.cs:62` |
| In-memory отдаёт общий кэш | `Metadata => DataContextCache.Metadata` | `InMemoryDataContext.cs:88` |
| Source | `FromExpression.Table` (только `string`) | `Expressions/FromExpression.cs:67` |
| Рендер | convention для auto; кавычит `schema.table` по точкам | `SqlSourceRenderer.cs:380`, `:845` |
| Имена параметров | `IParameterProvider.GetParamName()` → `pN` | `Query/IParameterProvider.cs:10`, `Query/DefaultParameterProvider.cs:11`, `DataContext/ParamNameCache.cs:12` |
| Runtime-параметры | `NormParam.Prefix = "norm_p"`, статический | `DataContext/NormParam.cs:14`, `:20` |
| Точки создания параметров | visitor/mutation | `Visitors/BaseExpressionVisitor.cs:330`, `Visitors/InValuesTranslator.cs:72/95`, `Visitors/SqlOperandTranslator.cs:71`, `DataContext/QueryPlanner.cs:246`, `DataContext/SqlMutationBuilder.cs:252/336/800`, `Visitors/NormSqlTranslator.cs:75` |
| Provider-параметры в контексте | создаётся по месту | `DataContext.cs:367`, `:410`, `QueryPlanner.cs:85/141/282/310/330`, `SqlMutationBuilder.cs:126/177/242` |

Факты для дизайна:
- `IEntityMetadata` **не имеет** `Schema`; прецедент — `TableFunctionExpression.Schema`
  (`Expressions/TableFunctionExpression.cs:14`, `:40`).
- Ambient-состояние есть: `IContextEnvironment.Properties` (`IContextEnvironment.cs:26`,
  `ContextEnvironment.cs:52`).
- `QueryPlan` хэширует SQL (`QueryPlan.cs:38-51`); plan cache — `[ThreadStatic]`
  (`Cache/QueryPlanStore.cs:18`).

## 4. Дизайн и публичный API

### 4.1. Scope

```csharp
/// <summary>Identifies a mapping variant: how names and parameters are substituted.</summary>
public readonly record struct MappingScope(string? Key)
{
    /// <summary>The default scope; mapping is resolved exactly as before.</summary>
    public static readonly MappingScope Default = new(null);

    /// <summary>Prefix applied to generated parameter names, or empty for the default scope.</summary>
    public string ParameterPrefix => string.IsNullOrEmpty(Key) ? string.Empty : Key + "_";
}
```

- Ключ всех метаданных-кэшей: `(Type, MappingScope)`.
- `Default` (`Key is null`) обязан повторять текущее поведение и не аллоцировать.

### 4.2. Резолвер scope (зависит от сущности)

```csharp
/// <summary>Resolves the mapping scope for an entity type and/or a concrete value source.</summary>
public interface IEntityScopeResolver
{
    /// <summary>Scope for the CLR type alone (static).</summary>
    MappingScope Resolve(Type entityType);

    /// <summary>Scope derived from a concrete instance (for example its key properties).</summary>
    MappingScope Resolve(Type entityType, object entity);

    /// <summary>Scope derived from explicit key values (query predicate, fluent call).</summary>
    MappingScope Resolve(Type entityType, IReadOnlyList<object?> keyValues);
}
```

- Базовая реализация по умолчанию читает scope из `IContextEnvironment.Properties` (context-level) и
  возвращает `Default`, если он не задан. Никакого знания о хэшах/шардах — только переход
  «сущность → scope».
- Продвинутый шардинг подменяет резолвер (`todo_sharding.md` §4.4).

### 4.3. Провайдер метаданных

```csharp
/// <summary>Resolves entity mapping metadata and column names for a mapping scope.</summary>
public interface IEntityMetadataProvider
{
    /// <summary>Metadata for <paramref name="entityType"/> under <paramref name="scope"/>.</summary>
    IEntityMetadata GetMetadata(Type entityType, in MappingScope scope);

    /// <summary>Column name for <paramref name="property"/> under <paramref name="scope"/>.</summary>
    string GetColumnName(PropertyInfo property, in MappingScope scope, INamingConvention? convention);
}
```

- Дефолт: текущая логика `EntityMetadataBuilder` + атрибуты, кэш `(Type, scope)`.
- Провайдер — точка «подмены названий»: table/schema/columns зависят от scope.

### 4.4. `IEntityMetadata.Schema` (extend-only)

```csharp
public interface IEntityMetadata
{
    /// <summary>The mapped schema/owner, or null for an unqualified table.</summary>
    string? Schema => null;
}
```

- Дефолтный член `=> null` — source/binary-compatible (как `IsTableNameAuto`, `IEntityMetadata.cs:27`).
- Fluent: `EntityMetadataBuilder<T>.Schema(string)`.

### 4.5. `FromExpression` со схемой

```csharp
public FromExpression(string table, string? schema, bool isAutoMapped = false, bool sourceIsInterface = false)
```

- Старый ctor делегирует с `schema: null` (source-compatible); модель — по
  `TableFunctionExpression` (`:14`, `:40`).

### 4.6. Префикс параметров

```csharp
/// <summary>Parameter provider that prefixes every generated name with a scope key.</summary>
public sealed class PrefixedParameterProvider(IParameterProvider inner, string prefix) : IParameterProvider
{
    public string GetParamName() => prefix + inner.GetParamName();
}
```

- `NormParam` (`DataContext/NormParam.cs:12`) становится scope-aware:
  `GetName(MappingScope scope, int index)` / `IsName(scope, name)`; кэш `ParamNameCache` — на префикс,
  `"norm_p"` для default (без аллокаций и без смены имён в default-режиме).
- Точки, которые обязаны прокидывать scope в имена: `QueryPlanner.cs:59,108`,
  `Visitors/NormSqlTranslator.cs:75`, `Cache/DbPreparedQueryCommand.cs:119`,
  `Visitors/InValuesTranslator.cs:72/95`, `SqlOperandTranslator.cs:71`,
  `BaseExpressionVisitor.cs:330`, `QueryPlanner.cs:246`, `SqlMutationBuilder.cs:252/336/800`.
- Провайдер параметров создаётся в `DataContext.cs:367/410` и `QueryPlanner`/`SqlMutationBuilder` по
  месту — инстанцирование должно проходить через один хелпер, применяющий scope-префикс.

### 4.7. Регистрация и per-query override

```csharp
public DataContextBuilder UseEntityMetadataProvider(IEntityMetadataProvider provider);
public DataContextBuilder UseScopeResolver(IEntityScopeResolver resolver);

// per-query (fluent, на EntityBuilder<T> / DML-билдерах):
public TBuilder WithScope(MappingScope scope);
```

- Default-реализации регистрируются автоматически; при `MappingScope.Default` — fast-path.

## 5. Точки встраивания

| Что | Точка | Файл:строка |
|---|---|---|
| Резолв метаданных по scope | `ResolveMetadata` / `From<T>` | `DataContextExtensions.cs:114`, `:665` |
| Ключ кэша метаданных | `DataContextCache.Metadata` → `(Type, scope)` | `DataContextCache.cs:22`, `:30` |
| Select-list | `SelectListCache` → `(Type, scope)` | `DataContextCache.cs:23`, `:35` |
| Имена колонок | `_columnNames` → `(pi, convention, scope)` | `MemberInfoExtensions.cs:15`, `:22` |
| FROM | передать `entity.Schema` | `QueryPlanner.cs:479-481` |
| Prepare | scoped-резолв по `srcType` | `Query/QueryCommand.QueryPreparer.cs:367` |
| Join-update | scoped-резолв по `_targetType` | `Builders/UpdateJoinBuilder.cs:145` |
| DML | scoped-резолв и table/schema | `InsertBuilder.cs:421`, `UpdateBuilder.cs:211`, `DeleteBuilder.cs:151`, `MergeBuilder.cs:375`, `TruncateBuilder.cs:62` |
| In-memory | scoped-lookup | `InMemoryDataContext.cs:88` |
| Провайдеры параметров | единая фабрика + префикс | `DataContext.cs:367/410`, `QueryPlanner.cs:85/141/282/310/330`, `SqlMutationBuilder.cs:126/177/242` |
| Runtime-имена | scope-aware `NormParam` | `DataContext/NormParam.cs:12`, `:20`; `Cache/DbPreparedQueryCommand.cs:119` |
| Регистрация | `DataContextBuilder` + `DataContextDependencies` | `DI/DataContextBuilder.cs:146`, `DataContextDependencies.cs:21` |

## 6. Этапы внедрения

- **Фаза 0 (schema, без scope):** `IEntityMetadata.Schema`, `EntityMetadataBuilder.Schema()`,
  `FromExpression` со схемой, рендер `schema.table`; per-query `Table("s.t")`/`Schema("s")`. Кэши не
  трогаем.
- **Фаза 1 (scope + подмена названий):** `MappingScope`, `IEntityScopeResolver`,
  `IEntityMetadataProvider`, составные ключи `Metadata`/`SelectListCache`/`_columnNames`; прокинуть
  scope в `QueryPlanner`, мутации, in-memory; `WithScope`.
- **Фаза 2 (префикс параметров):** `PrefixedParameterProvider` + scope-aware `NormParam`; единая
  фабрика параметров; тест на два scope в одной команде.
- **Вне области:** логика выбора scope по значениям (это уже шардинг, `todo_sharding.md`),
  соединение на scope (connection-per-tenant), транзакции между scope.

## 7. Ограничения и цена

- **Инъекция:** идентификаторы не параметризуются. `TableName`/`Schema`/`ColumnName` из провайдера —
  только доверенный/allow-list источник, никогда не пользовательский ввод. Зафиксировать в XML-doc.
- **План-кэш `[ThreadStatic]`:** scope захватывается в команду на build-time, а не читается в
  `QueryPlan`/терминале на другом потоке.
- **Prepared-команды:** `Prepare`/`DbPreparedQueryCommand` ключуются scope.
- **Zero-cost default:** при `MappingScope.Default` сохранить существующий словарь/ключ (fast-path),
  иначе просядут бенчмарки.
- **Префикс параметров и `norm_p`:** статический `NormParam` — единственное место, где имена не идут
  через `IParameterProvider`. Смена формата имени ломает контракт `SqlFunctions.Parameter`
  (`NormParam.cs:5-10`) — префикс обязан быть обратно совместим для default.
- **Потокобезопасность:** scope неизменяем; смена scope на общем контексте — только через
  `Properties`/`AsyncLocal`.

## 8. План тестов

- **Core (`tests/nextorm.core.tests`):**
  - `MappingScope.Default` — регресс SQL и имён параметров;
  - два scope → разные `IEntityMetadata` (table/schema/column) и разные SQL;
  - разные `ColumnName` per scope → нет протухания `_columnNames`/`SelectListCache`;
  - префикс параметров: `p0` → `<scope>_p0`, `norm_p0` → `<scope>_norm_p0`;
  - кастомные `IEntityScopeResolver`/`IEntityMetadataProvider`.
- **In-memory (`tests/nextorm.core.tests/InMemoryTests.cs`):** scoped-метаданные.
- **SQL-gen (`tests/nextorm.<provider>.tests`):** `FROM "scope_a"."orders"`, quoting по частям.
- **Интеграция (`tests/nextorm.integration.tests/CommonTestSuite.*.cs`):** PostgreSQL + SQL Server —
  две схемы, подмена scope на лету, read/insert/update/delete. Нужен `DOCKER_HOST`
  (skill `running-integration-tests`).
- **Покрытие:** не ниже базового; `coverage.settings.xml` включает
  `nextorm.{core,sqlite,postgres,sqlserver}` — заявить before/after.

## 9. Открытые вопросы

1. **Носитель scope:** `IContextEnvironment.Properties` (Фаза 1) vs `AsyncLocal` (позже) vs явный
   `WithScope`. Рекомендация — оба: ambient по умолчанию, явный override.
2. **Тип ключа scope:** `record struct MappingScope(string?)` vs объект-дескриптор.
3. **Convention × scope:** применять ли `INamingConvention` к auto-именам внутри провайдера.
4. **Схема:** отдельное `Schema` vs `TableName = "schema.table"` (первое чище; второе уже рендерится
   через `QuoteQualifiedIdentifier`, `SqlSourceRenderer.cs:845`).
5. **Формат префикса параметров:** `<key>_pN` vs `<key>.pN` vs хэш ключа (длина/допустимость в
   провайдерах).
6. **Колонки per scope** — отдельная фаза или сразу (инвазивно для кэша колонок)?

## 10. Файлы к изменению

- Новое: `src/nextorm.core/DataContext/Meta/MappingScope.cs`,
  `DataContext/Meta/IEntityScopeResolver.cs`, `DataContext/Meta/DefaultEntityScopeResolver.cs`,
  `DataContext/Meta/IEntityMetadataProvider.cs`, `DataContext/Meta/DefaultEntityMetadataProvider.cs`,
  `Query/PrefixedParameterProvider.cs`.
- Правки: `DataContext/Meta/IEntityMetadata.cs` (`Schema`), `Meta/Implementation/EntityMetadata.cs`,
  `Meta/EntityMetadataBuilder.cs`, `DataContextCache.cs`, `MemberInfoExtensions.cs`,
  `DataContextExtensions.cs`, `DataContext/QueryPlanner.cs`, `DataContext/InMemoryDataContext.cs`,
  `DataContext/Roles/IContextEnvironment.cs` (+`ContextEnvironment.cs`), `DI/DataContextBuilder.cs`,
  `DataContextDependencies.cs`, `Expressions/FromExpression.cs`, `DataContext/SqlSourceRenderer.cs`,
  `DataContext/SqlMutationBuilder.cs`, `DataContext/NormParam.cs`, `DataContext/ParamNameCache.cs`,
  `Query/IParameterProvider.cs`, все `Builders/*Builder.cs`, `Query/QueryCommand.QueryPreparer.cs`.
- Тесты: `tests/nextorm.core.tests/MappingScopeTests.cs`, правки `InMemoryTests.cs`, SQL-gen
  провайдеров, `tests/nextorm.integration.tests/CommonTestSuite.*.cs`.
- Документация: `docs/getting-started/03-entities-and-metadata.md` (+RU), раздел по multi-tenancy,
  `docs/specs/design/API-NAMING-REVIEW.md`, `docs/specs/design/code-smells-review.md`.
