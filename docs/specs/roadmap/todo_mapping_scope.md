# TODO: Mapping scope — scope-зависимый маппинг сущности (базовый слой)
> Tracking issue: [#65](https://github.com/AlexeyShirshov/nextorm/issues/65) — **открыт**,
> milestone `1.0.10-a.1` (актуализирован 2026-11-11; в файле milestone ранее не был указан).

> Рабочий план (design RFC). Базовый слой без знания о шардировании. Продвинутый слой —
> [`todo_sharding.md`](todo_sharding.md) — реализует этот seam.
> Источник: обсуждение multi-tenancy / шардирования (design-спека, регистра нет).
>
> **Актуализация 2026-11-11.** В `src/` нет ни `MappingScope`, ни `IEntityScopeResolver`/
> `IEntityMetadataProvider`/`PrefixedParameterProvider`; `IEntityMetadata` не имеет `Schema`
> (`src/nextorm.core/DataContext/Meta/Entities/IEntityMetadata.cs:11-56`). Фича не начата:
> ниже — план и проверенные точки встраивания.

## 1. Пункт и цель

- **Проблема:** метаданные сущности жёстко привязаны к CLR-типу: один `IEntityMetadata` на тип за
  процесс (`DataContext/Caches/DataContextCache.cs:23`). Нельзя отдать для одного `TEntity` разные
  таблицу/схему/колонки одной командой в зависимости от **scope**, вычисленного из самой сущности.
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
   (сама фича query filters уже отгружена — `DataContext/Meta/QueryFilters/`), EF Core integration
   (shipped — `nextorm.entityframeworkcore`).
3. Изоляция имени параметра нужна для много-вариантных команд (scatter-gather в шардинге).

## 3. Текущее состояние (проверено по коду)

| Механизм | Ключ / область | Где |
|---|---|---|
| Кэш метаданных | `ConcurrentDictionary<Type, IEntityMetadata>`-совместимый `TimedDictionary` — только тип, процесс | `DataContext/Caches/DataContextCache.cs:23`, `:52` |
| Кэш select-list | `TimedDictionary<Type, SelectExpression[]>` — только тип | `DataContext/Caches/DataContextCache.cs:26`, `:75` |
| Кэш имён колонок | `(PropertyInfo, INamingConvention?, Type?)` — процесс | `Utilities/MemberInfoExtensions.cs:16`, `:71-105` |
| Резолв метаданных | лениво, `configEntity` один раз за процесс | `DataContext/DataContextExtensions.cs:239`, `:334` |
| Построение FROM (SELECT) | `new FromExpression(entity.TableName, entity.IsTableNameAuto, t.IsInterface)` | `DataContext/QueryPlanner.cs:1153` |
| Prepare / join-update | чтение метаданных по типу | `Query/Commands/QueryCommand.QueryPreparer.cs:670`, `Builders/Dml/Update/UpdateJoinBuilder.cs:244` |
| DML фиксирует таблицу | `_metadata.TableName!` | `Builders/Dml/Insert/InsertBuilder.cs:637`, `Builders/Dml/Update/UpdateBuilder.cs:287`, `Builders/Dml/Delete/DeleteBuilder.cs:217`, `Builders/Merge/MergeBuilder.cs:747`, `Builders/Ddl/TruncateBuilder.cs:69` |
| In-memory отдаёт общий кэш | `Metadata => DataContextCache.Metadata` | `DataContext/InMemory/InMemoryDataContext.cs:88` |
| Source | `FromExpression.Table` (только `string`) | `Expressions/FromExpression.cs:101` |
| Рендер | convention для auto; кавычит `schema.table` по точкам | `DataContext/Sql/SqlSourceRenderer.cs:821`, `:1623` |
| Имена параметров | `IParameterProvider.GetParamName()` → `pN` | `Query/Providers/IParameterProvider.cs:16`, `Query/Providers/DefaultParameterProvider.cs:16`, `DataContext/Parameters/ParamNameCache.cs:12` |
| Runtime-параметры | `NormParam.Prefix = "norm_p"`, статический | `DataContext/Parameters/NormParam.cs:14`, `:18-22` |
| Точки создания параметров | visitor/mutation | `Visitors/Core/BaseExpressionVisitor.cs:758/774/999`, `Visitors/Parameters/InValuesTranslator.cs:80/103`, `Visitors/Sql/SqlOperandTranslator.cs:83`, `DataContext/QueryPlanner.cs:512`, `DataContext/Sql/SqlMutationBuilder.cs:317`, `Visitors/Sql/NormSqlTranslator.cs:154` |
| Provider-параметры в контексте | создаётся по месту | `DataContext/DataContext.cs:1305/1321/1439`, `DataContext/QueryPlanner.cs:161/227/691`, `DataContext/Sql/SqlMutationBuilder.cs:163/241` |

Факты для дизайна:
- `IEntityMetadata` **не имеет** `Schema` (`DataContext/Meta/Entities/IEntityMetadata.cs:11-56`);
  прецедент — `TableFunctionExpression.Schema` (`Expressions/Sources/TableFunctionExpression.cs:13`,
  `:46`).
- Ambient-состояние есть: `IContextEnvironment.Properties`
  (`DataContext/Roles/IContextEnvironment.cs:32`, `DataContext/Infrastructure/ContextEnvironment.cs:62`).
- `QueryPlan` хэширует SQL (`DataContext/Cache/Plans/QueryPlan.cs:51`); plan cache — `[ThreadStatic]`
  (`DataContext/Cache/Plans/QueryPlanStore.cs:31`).

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

- Дефолтный член `=> null` — source/binary-compatible (как `IsTableNameAuto`,
  `DataContext/Meta/Entities/IEntityMetadata.cs:27`).
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

- `NormParam` (`DataContext/Parameters/NormParam.cs:14`) становится scope-aware:
  `GetName(MappingScope scope, int index)` / `IsName(scope, name)`; кэш `ParamNameCache` — на префикс,
  `"norm_p"` для default (без аллокаций и без смены имён в default-режиме).
- Точки, которые обязаны прокидывать scope в имена:
  `Visitors/Sql/NormSqlTranslator.cs:154`, `DataContext/Cache/Commands/DbPreparedQueryCommand.cs:442/640`,
  `Visitors/Parameters/InValuesTranslator.cs:80/103`, `Visitors/Sql/SqlOperandTranslator.cs:83`,
  `Visitors/Core/BaseExpressionVisitor.cs:758/774/999`, `DataContext/QueryPlanner.cs:512`,
  `DataContext/Sql/SqlMutationBuilder.cs:317`.
- Провайдер параметров инстанцируется по месту: `DataContext/DataContext.cs:1305/1321/1439`,
  `DataContext/QueryPlanner.cs:161/227/691`, `DataContext/Sql/SqlMutationBuilder.cs:163/241` —
  инстанцирование должно проходить через единый хелпер, применяющий scope-префикс.

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
| Резолв метаданных по scope | `ResolveMetadata` / `From<T>` | `DataContext/DataContextExtensions.cs:239`, `:334` |
| Ключ кэша метаданных | `DataContextCache.Metadata` → `(Type, scope)` | `DataContext/Caches/DataContextCache.cs:23`, `:52` |
| Select-list | `SelectListCache` → `(Type, scope)` | `DataContext/Caches/DataContextCache.cs:26`, `:75` |
| Имена колонок | `_columnNames` → `(pi, convention, scope)` | `Utilities/MemberInfoExtensions.cs:16`, `:71` |
| FROM | передать `entity.Schema` | `DataContext/QueryPlanner.cs:1153` |
| Prepare | scoped-резолв по `srcType` | `Query/Commands/QueryCommand.QueryPreparer.cs:670` |
| Join-update | scoped-резолв по `_targetType` | `Builders/Dml/Update/UpdateJoinBuilder.cs:244` |
| DML | scoped-резолв и table/schema | `Builders/Dml/Insert/InsertBuilder.cs:637`, `Builders/Dml/Update/UpdateBuilder.cs:287`, `Builders/Dml/Delete/DeleteBuilder.cs:217`, `Builders/Merge/MergeBuilder.cs:747`, `Builders/Ddl/TruncateBuilder.cs:69` |
| In-memory | scoped-lookup | `DataContext/InMemory/InMemoryDataContext.cs:88` |
| Провайдеры параметров | единая фабрика + префикс | `DataContext/DataContext.cs:1305/1321/1439`, `DataContext/QueryPlanner.cs:161/227/691`, `DataContext/Sql/SqlMutationBuilder.cs:163/241` |
| Runtime-имена | scope-aware `NormParam` | `DataContext/Parameters/NormParam.cs:14`, `:18-22`; `DataContext/Cache/Commands/DbPreparedQueryCommand.cs:442/640` |
| Регистрация | `DataContextBuilder` + `DataContextDependencies` (хуки планировщика) | `DI/DataContextBuilder.cs`, `DataContext/DataContextDependencies.cs:28` |

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
  (`DataContext/Parameters/NormParam.cs:4-12`) — префикс обязан быть обратно совместим для default.
- **Потокобезопасность:** scope неизменяем; смена scope на общем контексте — только через
  `Properties`/`AsyncLocal`.

## 8. План тестов

- **Core (`tests/nextorm.core.tests`):**
  - `MappingScope.Default` — регресс SQL и имён параметров;
  - два scope → разные `IEntityMetadata` (table/schema/column) и разные SQL;
  - разные `ColumnName` per scope → нет протухания `_columnNames`/`SelectListCache`;
  - префикс параметров: `p0` → `<scope>_p0`, `norm_p0` → `<scope>_norm_p0`;
  - кастомные `IEntityScopeResolver`/`IEntityMetadataProvider`.
- **In-memory (`tests/nextorm.core.tests/InMemory/Queries/InMemoryTests.cs`):** scoped-метаданные.
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
   через `QuoteQualifiedIdentifier`, `DataContext/Sql/SqlSourceRenderer.cs:1623`).
5. **Формат префикса параметров:** `<key>_pN` vs `<key>.pN` vs хэш ключа (длина/допустимость в
   провайдерах).
6. **Колонки per scope** — отдельная фаза или сразу (инвазивно для кэша колонок)?

## 10. Файлы к изменению

- Новое: `src/nextorm.core/DataContext/Meta/MappingScope.cs`,
  `DataContext/Meta/IEntityScopeResolver.cs`, `DataContext/Meta/DefaultEntityScopeResolver.cs`,
  `DataContext/Meta/IEntityMetadataProvider.cs`, `DataContext/Meta/DefaultEntityMetadataProvider.cs`,
  `Query/Providers/PrefixedParameterProvider.cs`.
- Правки: `DataContext/Meta/Entities/IEntityMetadata.cs` (`Schema`),
  `Meta/Implementation/EntityMetadata.cs`, `Meta/Entities/EntityMetadataBuilder.cs`,
  `DataContext/Caches/DataContextCache.cs`, `Utilities/MemberInfoExtensions.cs`,
  `DataContext/DataContextExtensions.cs`, `DataContext/QueryPlanner.cs`,
  `DataContext/InMemory/InMemoryDataContext.cs`,
  `DataContext/Roles/IContextEnvironment.cs` (+`DataContext/Infrastructure/ContextEnvironment.cs`),
  `DI/DataContextBuilder.cs`, `DataContext/DataContextDependencies.cs`,
  `Expressions/FromExpression.cs`, `DataContext/Sql/SqlSourceRenderer.cs`,
  `DataContext/Sql/SqlMutationBuilder.cs`, `DataContext/Parameters/NormParam.cs`,
  `DataContext/Parameters/ParamNameCache.cs`, `Query/Providers/IParameterProvider.cs`,
  все `Builders/**/*Builder.cs`, `Query/Commands/QueryCommand.QueryPreparer.cs`.
- Тесты: `tests/nextorm.core.tests/MappingScopeTests.cs`, правки
  `tests/nextorm.core.tests/InMemory/Queries/InMemoryTests.cs`, SQL-gen
  провайдеров, `tests/nextorm.integration.tests/CommonTestSuite/*.cs`.
- Документация: `docs/getting-started/03-entities-and-metadata.md` (+RU), раздел по multi-tenancy,
  `docs/specs/design/API-NAMING-REVIEW.md`, `docs/specs/design/code-smells-review.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **нужен пересмотр до старта фазы 1 — 1 блокер + 8 🟡**.
>
> **Актуализация 2026-11-11:** решения ревью в силе (реализация не начиналась). Актуальные якоря —
> таблицы §3/§5 выше; `_fromCache` сейчас `DataContext/QueryPlanner.cs:1142-1154`.

- **[TYPE] 🔴** `QueryPlanner._fromCache` не scope-aware: `QueryPlanner.cs:509-519` — process-wide `ConcurrentDictionary<Type, FromExpression>` по типу; при двух scope второй получит `FromExpression` default-scope (table/schema). В «Точках встраивания» (`:169-184`) и «Файлах» (`:242-254`) `_fromCache` отсутствует. Fix: ключ `(Type, MappingScope)` или перенос кэша в scoped-провайдер.
- **[TYPE]/[DIP] 🟡** Scope не протянут через `SqlBuildContext`/`VisitorOptions`: `MemberInfoExtensions.cs:22-43` резолвит имя колонки из process-wide `DataContextCache.Metadata` без scope/provider; рендер получает параметры через `SqlBuildContext` (`SqlBuildContext.cs:44`) и `VisitorOptions` (`VisitorOptions.cs:67`), где scope нет. Fix: добавить `MappingScope`/scoped-resolver в эти два типа.
- **[SRP]/[KISS] 🟡** Публичные seam'ы при одном потребителе (`:80-110,159-167`): `IEntityScopeResolver`/`IEntityMetadataProvider` имеют только default-реализацию, 2-й потребитель — нереализованный `todo_sharding.md:100-106`. Инвариант 1 запрещает `IX/X` «на будущее». Fix: оставить seam внутренним до шипнутого потребителя либо явно принять YAGNI-цену.
- **[SRP] 🟡** Две из трёх перегрузок резолвера без call-site (`:80-90`): `Resolve(Type, object)` и `Resolve(Type, IReadOnlyList<object?>)`. Fix: в фазе 1 только `Resolve(Type)`.
- **[TYPE] 🟡** Публичная форма кэшей ломается: `DataContextCache.Metadata`/`SelectListCache` — `public static IDictionary<Type, …>` (`DataContextCache.cs:30,35`); смена ключа на `(Type, MappingScope)` — бинарный public-API брейк, тогда как `:167,206` обещает сохранить словарь/ключ. Fix: `Type`-словарь fast-path для `Default` + отдельный scoped-кэш; брейк оформить осознанно через EN+RU (инвариант 5).
- **[DRY] 🟡** `IEntityMetadataProvider.GetColumnName` дублирует знание о колонке (`:102-109` vs `IEntityMetadata.Properties[].ColumnName` и `MemberInfoExtensions._columnNames` `:15`) — три источника истины. Fix: имя колонки только через метаданные.
- **[DRY] 🟡** Параллельный механизм префикса поверх существующего: уже есть `SqlBuildContext.ParameterNamePrefix` (`:44`) и `VisitorOptions.ParameterNamePrefix` (`:67`), протянутые через `RenderSource`/`RenderPredicate`/`RenderAssignments` (`QueryPlanner.cs:83,137,221`). Fix: расширить существующий prefix-путь либо обосновать wrapper.
- **[TYPE]/[PERF] 🟡** `:69` `ParameterPrefix => Key + "_"` аллоцирует на каждое чтение; `:147-149` делает `NormParam` статическим по scope — process-wide `ParamNameCache` без эвикции (`ParamNameCache.cs:12-41`), при tenant/shard-ключах неограниченный рост. Fix: кэшировать префикс в структуре, norm-кэш — per-context/с ограничением.
- **[TYPE]/[LSP] 🟡** `Schema` не доезжает до DML (`:115-123,128-135,177`): мутации несут лишь `TableName`/`IsTableNameAuto` (`MutationCommand.cs:147-150`, `InsertBuilder.cs:425`) и рендерятся через `SqlMutationBuilder.ResolveTableName` (`:888-891`) без схемы → `Schema("s")` молча не квалифицирует `INSERT/UPDATE/DELETE/MERGE/TRUNCATE`. Fix: добавить `Schema` в mutation-команды и `ResolveTableName`/`Make*Head`. ℹ️ (30.09.2026, #123) правка target-filter MERGE затронула `SqlMutationBuilder.AppendMergeOnKeys`/`AppendMergeWhen` и `MakeMerge`, но `Schema` в mutation-команды по-прежнему не добавлена — расхождение этой находки сохраняется.
- **[TYPE] ℹ️** `:244-247` `DefaultEntityScopeResolver`/`DefaultEntityMetadataProvider` без `sealed`; `:105` `in MappingScope` на 8-байтной структуре выгоды не даёт. Fix: `sealed`/`internal`, по значению.
- **[SRP] ℹ️** Ambient scope и `Properties`: `IContextEnvironment.cs:26` — мутабельный `Dictionary<string,object>`; план оговаривает потокобезопасность (`:211-212`). Deferred: документировать «не менять во время запроса»/снапшот.
- **ℹ️** `:184` ссылается на `DataContextDependencies.cs:21` как на регистрацию — фактически это внутренние хуки, регистрация в `DataContextBuilder.cs:68-140`. Fix.
