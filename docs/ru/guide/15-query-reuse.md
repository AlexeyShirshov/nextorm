# Повторное использование запросов: кэш планов и [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken))

> Сохраняйте SQL и построчный маппер для «горячего» запроса вместо их пересборки при каждом вызове — либо через неявный кэш планов, используемый каждым терминалом, либо через явный [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1), возвращаемый [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)).

**Предварительные требования:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Dependency injection](../getting-started/04-dependency-injection.md).

## Обзор

Каждый терминал, такой как [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), делает несколько вещей: превращает построенный во fluent-стиле [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) в SQL и построчный маппер, подготавливает [`DbCommand`](xref:NextORM.Core.DbPreparedQueryCommand`1.DbCommand) и читает результат. Два независимых механизма позволяют оплатить первую часть лишь один раз:

* **неявный кэш планов** — автоматический, с ключом по структурной форме запроса;
* **[`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken))** — явный, возвращает [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1), который вы храните и выполняете сами.

Их легко спутать, потому что внешне они похожи. Они различаются ключом, временем жизни, потокобезопасностью и тем, заполняют ли они кэш:

| | Неявный кэш планов | Явный [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) |
|---|---|---|
| Точка входа | любой терминал на [`EntityBuilder`](xref:NextORM.Core.EntityBuilder) / [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) ([`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})), [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])), ...) | [`Prepare`](xref:NextORM.Core.QueryCommand`1.Prepare(System.Boolean,System.Threading.CancellationToken)) / `EntityBuilder<T>.Prepare()` |
| Ключ поиска | структурный хеш формы запроса | отсутствует — вы храните возвращённую команду |
| Время жизни | до `PurgeQueryCache()` или завершения процесса | пока вы храните ссылку |
| Область действия | **на поток**, разделяется всеми [`IDataContext`](xref:NextORM.Core.IDataContext) в этом потоке | экземпляр, который вы храните |
| Заполняет кэш | да | **нет** |
| Безопасен для конкурентного использования | да, по построению (запись на поток) | **нет** (один изменяемый [`DbCommand`](xref:NextORM.Core.DbPreparedQueryCommand`1.DbCommand) и перечислитель) |
| Потоковый (`IAsyncEnumerable`) | да | только с `nonStreamUsing: false` |

На этой странице описаны API и правила кэширования; измеренная стоимость каждого варианта здесь не повторяется.

## Терминалы на [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1)

`Select(...)` (и остальная часть fluent-поверхности) создаёт [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1); терминальные операторы объявлены на этом типе. Они делятся на буферизованные/скалярные и потоковые, и каждый из них проходит через кэш планов.

Буферизованные / скалярные:

| Терминал | Возвращает |
|---|---|
| [`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) / `ToList(params ReadOnlySpan<object?>)` | `List<TResult>` |
| `ToListAsync(...)` (необязательный `CancellationToken`, необязательный `params object[]`) | `Task<List<TResult>>` |
| [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) / `FirstAsync(...)` | `TResult` |
| [`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0})) / `FirstOrDefaultAsync(...)` | `TResult?` |
| [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) / `SingleAsync(...)` | `TResult` |
| [`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})) / `SingleOrDefaultAsync(...)` | `TResult?` |
| [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) / `AnyAsync(...)` | `bool` |
| [`ExecuteScalar`](xref:NextORM.Core.QueryCommand`1.ExecuteScalar(System.ReadOnlySpan{System.Object})) / `ExecuteScalarAsync(...)` | `TResult?` |

Потоковые:

| Терминал | Возвращает |
|---|---|
| `ToAsyncEnumerable(...)` | `IAsyncEnumerable<TResult>` |
| `Pipeline(...)` | `IAsyncEnumerable<TResult>` |
| `ToEnumerable(...)` | `IEnumerable<TResult>` |
| `CreateAsyncEnumerator(...)` | `IAsyncEnumerator<TResult>` |
| `CreateEnumeratorAsync(...)` | `Task<IEnumerator<TResult>>` |

`params object[]` (или `ReadOnlySpan<object?>`), принимаемые терминалами, несут значения времени выполнения для плейсхолдеров [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)) в запросе.

```csharp
using var ctx = new SqliteDataContext("Data Source=app.db", new DataContextBuilder());

var rows = await ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
select id from simple_entity
```

Поскольку ключ кэша структурный, два вызова с *разным экземпляром лямбды*, но одной и той же формой — это попадание в кэш. Именно это делает кэш полезным для запросов, написанных прямо внутри цикла:

```csharp
for (var i = 0; i < 2; i++)
{
    var entityId = i;

    var row = await ctx.From<ISimpleEntity>()
        .Where(entity => entity.Id == entityId)
        .Select(entity => new { Id = (long)entity.Id })
        .FirstOrDefaultAsync();
}
```

Обе итерации переиспользуют одну и ту же запись кэша.

## Явный [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken))

[`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) объявлен и на [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1), и на `EntityBuilder<T>` и возвращает [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1):

```csharp
public IPreparedQueryCommand<TResult> Prepare(bool nonStreamUsing = true, CancellationToken cancellationToken = default)
```

Значение по умолчанию (`nonStreamUsing: true`) оптимизировано для буферизованных и скалярных результатов. Возвращённая команда не привязана к контексту, который её создал: каждый терминал принимает [`IDataContext`](xref:NextORM.Core.IDataContext) первым аргументом, поэтому одну и ту же подготовленную команду можно выполнить против другого контекста того же провайдера.

```csharp
var prepared = ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .Prepare();

// Terminal methods declared on IPreparedQueryCommand<TResult>.
List<int> all = prepared.ToList(ctx);
int first = prepared.First(ctx);
List<int> asyncAll = await prepared.ToListAsync(ctx);
int? scalar = prepared.ExecuteScalar(ctx, throwIfNull: false);

// The IDataContext extension overloads in DataContextExtensions are equivalent.
List<int> all2 = ctx.ToList(prepared);
List<int> asyncAll2 = await ctx.ToListAsync(prepared);
int first2 = await ctx.FirstAsync(prepared);
```

Одну и ту же команду можно повторно выполнять с разными значениями параметров времени выполнения:

```csharp
var byId = ctx.From<ISimpleEntity>()
    .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
    .Select(x => x.Id)
    .Prepare();

var one = await byId.FirstAsync(ctx, 42);    // 42 fills norm_p0
var many = await byId.ToListAsync(ctx, 43);  // same prepared command, new value
```

### Потоковая передача требует `nonStreamUsing: false`

Подготовленная команда, созданная со значением по умолчанию, не владеет [`ResultSetEnumerator<TResult>`](xref:NextORM.Core.ResultSetEnumerator`1).#ctor(NextORM.Core.DbPreparedQueryCommand{`0},Microsoft.Extensions.ObjectPool.ObjectPool{System.Text.StringBuilder}). Буферизованные и скалярные терминалы работают; потоковый терминал завершается ошибкой `InvalidOperationException`, сообщение которой говорит использовать `Prepare(nonStreamUsing: false)`. [`ToEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])) — это ленивый итератор, поэтому на этом пути исключение появляется при начале перечисления, а не в момент вызова.

```csharp
// Buffered default: no enumerator is created.
var buffered = ctx.From<ISimpleEntity>().Select(x => x.Id).Prepare();
buffered.ToList(ctx); // fine

// Streaming: the only way to get a streamable prepared command.
var streaming = ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .Prepare(nonStreamUsing: false);

await foreach (var id in streaming.ToAsyncEnumerable(ctx))
{
    Console.WriteLine(id);
}
```

> **Правила совместного использования.** Подготовленная команда владеет одним изменяемым [`DbCommand`](xref:NextORM.Core.DbPreparedQueryCommand`1.DbCommand) и одним [`ResultSetEnumerator<TResult>`](xref:NextORM.Core.ResultSetEnumerator`1).#ctor(NextORM.Core.DbPreparedQueryCommand{`0},Microsoft.Extensions.ObjectPool.ObjectPool{System.Text.StringBuilder}). Значения параметров, `DbCommand.Connection` и читатель перечислителя перезаписываются при каждом выполнении. Не делите одну подготовленную команду между потоками и не запускайте две перекрывающиеся итерации по одной подготовленной команде. У неявного кэша этой проблемы нет: он привязан к потоку.

### [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) не заполняет кэш планов

[`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) вызывает планировщик с `storeInCache: false`, поэтому его нельзя найти последующим неявным поиском, и он не может «загрязнить» кэш:

```csharp
ctx.PurgeQueryCache();

var prepared = ctx.From<ISimpleEntity>().Select(x => x.Id).Prepare();
prepared.ToList(ctx); // executes, but adds no plan-cache entry

// A later implicit terminal builds its own plan.
var implicitRows = ctx.From<ISimpleEntity>().Select(x => x.Id).ToList();
```

## Область действия кэша планов и его очистка

Неявный кэш (`DataContext._queryPlanCache`) — это словарь `[ThreadStatic]` с ключом `(ContextType, QueryPlan)`:

* **На поток.** Запись, созданная в потоке A, невидима потоку B; при каждом переходе между потоками пула коэффициент попаданий падает до нуля, пока форма не встретится снова в этом потоке.
* **На тип контекста.** SQLite, PostgreSQL и SQL Server порождают разный SQL и разные реализации [`DbCommand`](xref:NextORM.Core.DbPreparedQueryCommand`1.DbCommand) для одной и той же формы, поэтому получают отдельные записи.
* **Автоматической инвалидации нет.** Записи живут, пока не вызван `PurgeQueryCache()` (или пока не завершится процесс). После изменения схемы очистите кэш (или пересоздайте контекст), прежде чем переиспользовать затронутые формы.
* `QueryCommand<TResult>.Cache = false` отключает хеширование и поиск для этой команды. Это не дешёвый переключатель «пропустить кэш»: он заставляет полностью пересобирать план при каждом вызове.

```csharp
ctx.PurgeQueryCache();
```

У других кэшей намеренно другая область совместного использования. Метаданные, списки выборки, скомпилированные делегаты выражений и аксессоры in-list являются общими для процесса; кэш выражений в памяти привязан к экземпляру контекста, потому что его записи захватывают сам контекст:

```csharp
using var ctx = new InMemoryDataContext();

ReferenceEquals(ctx.Metadata, DataContextCache.Metadata);                 // true  (process-wide)
ReferenceEquals(ctx.SelectListCache, DataContextCache.SelectListCache);   // true  (process-wide)
ReferenceEquals(ctx.ExpressionsCache, DataContextCache.ExpressionsCache); // false (per instance)
```

## Захваченные коллекции `in`/`Contains`

Список `in` или вызов `Contains` по захваченной коллекции транслируется в параметризованный предикат `IN`. *Значения* не являются частью ключа плана — *форма* является (вычисленное количество элементов и наличие `null`). Это имеет два следствия:

* две сборки одинаковой формы (одинаковая длина коллекции, одинаковое наличие null) используют один план, даже из разных мест вызова;
* если коллекция вырастает или переприсваивается между выполнениями, хеш формы меняется, поэтому создаётся свежий план, соответствующий новому количеству параметров, а текущие значения читаются через кэшированный аксессор.

```csharp
var values = new long[] { 1 };

var first = ctx.GetPreparedQueryCommand(
    ctx.From<IComplexEntity>().Where(c => SqlFunctions.Sql.@in(c.Id, values)).Select(c => c.Id),
    createEnumerator: false, storeInCache: true, CancellationToken.None);
// one parameter: p0

values = new long[] { 2, 3 };

var second = ctx.GetPreparedQueryCommand(
    ctx.From<IComplexEntity>().Where(c => SqlFunctions.Sql.@in(c.Id, values)).Select(c => c.Id),
    createEnumerator: false, storeInCache: true, CancellationToken.None);
// two parameters: p0, p1
```

```sql
id in ($p0, $p1)
```

`values.Contains(c.Id)` обрабатывается так же. Элемент `null` добавляет ветвь `is null` (например, `(nullableint in ($p0) or nullableint is null)`), а пустая коллекция или коллекция целиком из null сворачивается в константу (`1 = 0` / `is null`) без параметров.

Захваченная коллекция, индексируемая выражением запроса (`dict[column]`), сворачивается так же: число записей входит в ключ плана, поэтому сгенерированный `CASE` перестраивается при изменении коллекции. См. [Фильтрация (WHERE)](02-filtering-where.md).

## Различия провайдеров

| Провайдер | Поведение |
|---|---|
| SQLite | Кэш планов и [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) — основное поведение; подготовленная команда оборачивает `SqliteCommand`. |
| SQL Server | Тот же механизм; разбиение на страницы `TOP` против `OFFSET ... FETCH` является частью записанной формы плана, поэтому режим разбиения на страницы «запекается» в кэшированный SQL. |
| PostgreSQL | Тот же механизм; подготовленная команда оборачивает `NpgsqlCommand`. |
| MySQL | Кэш планов и [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) — основное поведение; подготовленная команда оборачивает `MySqlCommand`. |
| MariaDB | Тот же механизм; подготовленная команда оборачивает `MySqlCommand` (драйвер MySQL). |
| ClickHouse | Кэш планов и [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) — основное поведение; подготовленная команда оборачивает `ClickHouseCommand`. |
| In-memory | [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) хранит собственный кэш скомпилированных запросов и возвращает [`InMemoryPreparedQueryCommand<TResult>`](xref:NextORM.Core.InMemoryPreparedQueryCommand`1); [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) работает, но [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String)) для raw-SQL не поддерживается (`NotSupportedException`). |

## См. также

* [Connections and logging](16-connections-and-logging.md)
* [Dependency injection](../getting-started/04-dependency-injection.md)
* [Documentation index](../index.md)

---

Source: `tests/nextorm.sqlite.tests/PlanCacheTests.cs:49` (buffered then streaming),
`tests/nextorm.sqlite.tests/PlanCacheTests.cs:99` (`Prepare(nonStreamUsing: false)`),
`tests/nextorm.sqlite.tests/PlanCacheTests.cs:124` (buffered reuse),
`tests/nextorm.sqlite.tests/PlanCacheTests.cs:152` (streaming the default throws),
`tests/nextorm.sqlite.tests/PlanCacheTests.cs:257` ([`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken)) does not populate the cache);
`tests/nextorm.sqlite.tests/InListCacheTests.cs:41` (same-shape captured collection),
`tests/nextorm.sqlite.tests/InListCacheTests.cs:92` (reassigned array),
`tests/nextorm.sqlite.tests/InListCacheTests.cs:117` (grown list);
`tests/nextorm.core.tests/DataContextCacheScopeTests.cs:20` (cache sharing scope);
`tests/nextorm.integration.tests/CommonTestSuite.Cache.cs:6`;
`src/nextorm.core/Query/QueryCommand.TResult.cs:38` ([`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},System.Boolean,System.Threading.CancellationToken))),
`src/nextorm.core/DataContext/Cache/IPreparedQueryCommand.cs:5` (prepared terminals),
`src/nextorm.core/DataContext/DataContext.cs:315` (`GetPreparedQueryCommand`).
