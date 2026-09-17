# Повторное использование запросов: кэш планов и `Prepare`

> Сохраняйте SQL и построчный маппер для «горячего» запроса вместо их пересборки при каждом вызове — либо через неявный кэш планов, используемый каждым терминалом, либо через явный `IPreparedQueryCommand<TResult>`, возвращаемый `Prepare()`.

**Предварительные требования:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Dependency injection](../getting-started/04-dependency-injection.md).

## Обзор

Каждый терминал, такой как `ToListAsync`, делает несколько вещей: превращает построенный во fluent-стиле `QueryCommand<TResult>` в SQL и построчный маппер, подготавливает `DbCommand` и читает результат. Два независимых механизма позволяют оплатить первую часть лишь один раз:

* **неявный кэш планов** — автоматический, с ключом по структурной форме запроса;
* **`Prepare()`** — явный, возвращает `IPreparedQueryCommand<TResult>`, который вы храните и выполняете сами.

Их легко спутать, потому что внешне они похожи. Они различаются ключом, временем жизни, потокобезопасностью и тем, заполняют ли они кэш:

| | Неявный кэш планов | Явный `Prepare()` |
|---|---|---|
| Точка входа | любой терминал на `Entity` / `QueryCommand<TResult>` (`ToList`, `ToListAsync`, `ToAsyncEnumerable`, ...) | `QueryCommand<TResult>.Prepare()` / `Entity<T>.Prepare()` |
| Ключ поиска | структурный хеш формы запроса | отсутствует — вы храните возвращённую команду |
| Время жизни | до `PurgeQueryCache()` или завершения процесса | пока вы храните ссылку |
| Область действия | **на поток**, разделяется всеми `IDataContext` в этом потоке | экземпляр, который вы храните |
| Заполняет кэш | да | **нет** |
| Безопасен для конкурентного использования | да, по построению (запись на поток) | **нет** (один изменяемый `DbCommand` и перечислитель) |
| Потоковый (`IAsyncEnumerable`) | да | только с `nonStreamUsing: false` |

На этой странице описаны API и правила кэширования. Измеренная стоимость каждого варианта и обоснование выбора одного из них приведены в [Prepared vs cached](../../prepared-vs-cached.md) и здесь не повторяются.

## Терминалы на `QueryCommand<TResult>`

`Select(...)` (и остальная часть fluent-поверхности) создаёт `QueryCommand<TResult>`; терминальные операторы объявлены на этом типе. Они делятся на буферизованные/скалярные и потоковые, и каждый из них проходит через кэш планов.

Буферизованные / скалярные:

| Терминал | Возвращает |
|---|---|
| `ToList()` / `ToList(params ReadOnlySpan<object?>)` | `List<TResult>` |
| `ToListAsync(...)` (необязательный `CancellationToken`, необязательный `params object[]`) | `Task<List<TResult>>` |
| `First()` / `FirstAsync(...)` | `TResult` |
| `FirstOrDefault()` / `FirstOrDefaultAsync(...)` | `TResult?` |
| `Single()` / `SingleAsync(...)` | `TResult` |
| `SingleOrDefault()` / `SingleOrDefaultAsync(...)` | `TResult?` |
| `Any()` / `AnyAsync(...)` | `bool` |
| `ExecuteScalar()` / `ExecuteScalarAsync(...)` | `TResult?` |

Потоковые:

| Терминал | Возвращает |
|---|---|
| `ToAsyncEnumerable(...)` | `IAsyncEnumerable<TResult>` |
| `Pipeline(...)` | `IAsyncEnumerable<TResult>` |
| `ToEnumerable(...)` | `IEnumerable<TResult>` |
| `CreateAsyncEnumerator(...)` | `IAsyncEnumerator<TResult>` |
| `CreateEnumeratorAsync(...)` | `Task<IEnumerator<TResult>>` |

`params object[]` (или `ReadOnlySpan<object?>`), принимаемые терминалами, несут значения времени выполнения для плейсхолдеров `NORM.Param<T>(index)` в запросе.

```csharp
using var ctx = new SqliteDbContext("Data Source=app.db", new DbContextBuilder());

var rows = await ctx.Create<ISimpleEntity>()
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

    var row = await ctx.Create<ISimpleEntity>()
        .Where(entity => entity.Id == entityId)
        .Select(entity => new { Id = (long)entity.Id })
        .FirstOrDefaultAsync();
}
```

Обе итерации переиспользуют одну и ту же запись кэша.

## Явный `Prepare()`

`Prepare()` объявлен и на `QueryCommand<TResult>`, и на `Entity<T>` и возвращает `IPreparedQueryCommand<TResult>`:

```csharp
public IPreparedQueryCommand<TResult> Prepare(bool nonStreamUsing = true, CancellationToken cancellationToken = default)
```

Значение по умолчанию (`nonStreamUsing: true`) оптимизировано для буферизованных и скалярных результатов. Возвращённая команда не привязана к контексту, который её создал: каждый терминал принимает `IDataContext` первым аргументом, поэтому одну и ту же подготовленную команду можно выполнить против другого контекста того же провайдера.

```csharp
var prepared = ctx.Create<ISimpleEntity>()
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
var byId = ctx.Create<ISimpleEntity>()
    .Where(x => x.Id == NORM.Param<int>(0))
    .Select(x => x.Id)
    .Prepare();

var one = await byId.FirstAsync(ctx, 42);    // 42 fills norm_p0
var many = await byId.ToListAsync(ctx, 43);  // same prepared command, new value
```

### Потоковая передача требует `nonStreamUsing: false`

Подготовленная команда, созданная со значением по умолчанию, не владеет `ResultSetEnumerator`. Буферизованные и скалярные терминалы работают; потоковый терминал завершается ошибкой `InvalidOperationException`, сообщение которой говорит использовать `Prepare(nonStreamUsing: false)`. `ToEnumerable` — это ленивый итератор, поэтому на этом пути исключение появляется при начале перечисления, а не в момент вызова.

```csharp
// Buffered default: no enumerator is created.
var buffered = ctx.Create<ISimpleEntity>().Select(x => x.Id).Prepare();
buffered.ToList(ctx); // fine

// Streaming: the only way to get a streamable prepared command.
var streaming = ctx.Create<ISimpleEntity>()
    .Select(x => x.Id)
    .Prepare(nonStreamUsing: false);

await foreach (var id in streaming.ToAsyncEnumerable(ctx))
{
    Console.WriteLine(id);
}
```

> **Правила совместного использования.** Подготовленная команда владеет одним изменяемым `DbCommand` и одним `ResultSetEnumerator`. Значения параметров, `DbCommand.Connection` и читатель перечислителя перезаписываются при каждом выполнении. Не делите одну подготовленную команду между потоками и не запускайте две перекрывающиеся итерации по одной подготовленной команде. У неявного кэша этой проблемы нет: он привязан к потоку.

### `Prepare()` не заполняет кэш планов

`Prepare()` вызывает планировщик с `storeInCache: false`, поэтому его нельзя найти последующим неявным поиском, и он не может «загрязнить» кэш:

```csharp
ctx.PurgeQueryCache();

var prepared = ctx.Create<ISimpleEntity>().Select(x => x.Id).Prepare();
prepared.ToList(ctx); // executes, but adds no plan-cache entry

// A later implicit terminal builds its own plan.
var implicitRows = ctx.Create<ISimpleEntity>().Select(x => x.Id).ToList();
```

## Область действия кэша планов и его очистка

Неявный кэш (`DbContext._queryPlanCache`) — это словарь `[ThreadStatic]` с ключом `(ContextType, QueryPlan)`:

* **На поток.** Запись, созданная в потоке A, невидима потоку B; при каждом переходе между потоками пула коэффициент попаданий падает до нуля, пока форма не встретится снова в этом потоке.
* **На тип контекста.** SQLite, PostgreSQL и SQL Server порождают разный SQL и разные реализации `DbCommand` для одной и той же формы, поэтому получают отдельные записи.
* **Автоматической инвалидации нет.** Записи живут, пока не вызван `PurgeQueryCache()` (или пока не завершится процесс). После изменения схемы очистите кэш (или пересоздайте контекст), прежде чем переиспользовать затронутые формы.
* `QueryCommand<TResult>.Cache = false` отключает хеширование и поиск для этой команды. Это не дешёвый переключатель «пропустить кэш»: он заставляет полностью пересобирать план при каждом вызове.

```csharp
ctx.PurgeQueryCache();
```

У других кэшей намеренно другая область совместного использования. Метаданные, списки выборки, скомпилированные делегаты выражений и аксессоры in-list являются общими для процесса; кэш выражений в памяти привязан к экземпляру контекста, потому что его записи захватывают сам контекст:

```csharp
using var ctx = new InMemoryContext();

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
    ctx.Create<IComplexEntity>().Where(c => NORM.SQL.@in(c.Id, values)).Select(c => c.Id),
    createEnumerator: false, storeInCache: true, CancellationToken.None);
// one parameter: p0

values = new long[] { 2, 3 };

var second = ctx.GetPreparedQueryCommand(
    ctx.Create<IComplexEntity>().Where(c => NORM.SQL.@in(c.Id, values)).Select(c => c.Id),
    createEnumerator: false, storeInCache: true, CancellationToken.None);
// two parameters: p0, p1
```

```sql
id in ($p0, $p1)
```

`values.Contains(c.Id)` обрабатывается так же. Элемент `null` добавляет ветвь `is null` (например, `(nullableint in ($p0) or nullableint is null)`), а пустая коллекция или коллекция целиком из null сворачивается в константу (`1 = 0` / `is null`) без параметров.

## Различия провайдеров

| Провайдер | Поведение |
|---|---|
| SQLite | Кэш планов и `Prepare()` — основное поведение; подготовленная команда оборачивает `SqliteCommand`. |
| SQL Server | Тот же механизм; разбиение на страницы `TOP` против `OFFSET ... FETCH` является частью записанной формы плана, поэтому режим разбиения на страницы «запекается» в кэшированный SQL. |
| PostgreSQL | Тот же механизм; подготовленная команда оборачивает `NpgsqlCommand`. |
| In-memory | `InMemoryContext` хранит собственный кэш скомпилированных запросов и возвращает `InMemoryPreparedQueryCommand<TResult>`; `Prepare()` работает, но `PrepareFromSql` для raw-SQL не реализован (`NotImplementedException`). |

## См. также

* [Prepared vs cached: reusing a query](../../prepared-vs-cached.md) — стоимость, бенчмарки и ограничения.
* [Connections and logging](16-connections-and-logging.md)
* [Dependency injection](../getting-started/04-dependency-injection.md)
* [Documentation index](../index.md)

---

Source: `test/nextorm.sqlite.tests/PlanCacheTests.cs:49` (buffered then streaming),
`test/nextorm.sqlite.tests/PlanCacheTests.cs:99` (`Prepare(nonStreamUsing: false)`),
`test/nextorm.sqlite.tests/PlanCacheTests.cs:124` (buffered reuse),
`test/nextorm.sqlite.tests/PlanCacheTests.cs:152` (streaming the default throws),
`test/nextorm.sqlite.tests/PlanCacheTests.cs:257` (`Prepare` does not populate the cache);
`test/nextorm.sqlite.tests/InListCacheTests.cs:41` (same-shape captured collection),
`test/nextorm.sqlite.tests/InListCacheTests.cs:92` (reassigned array),
`test/nextorm.sqlite.tests/InListCacheTests.cs:117` (grown list);
`test/nextorm.core.tests/DataContextCacheScopeTests.cs:20` (cache sharing scope);
`test/nextorm.integration.tests/CommonTestSuite.Cache.cs:6`;
`src/nextorm.core/Query/QueryCommand.TResult.cs:38` (`Prepare`),
`src/nextorm.core/DataContext/Cache/IPreparedQueryCommand.cs:5` (prepared terminals),
`src/nextorm.core/DataContext/DbContext.cs:315` (`GetPreparedQueryCommand`).
