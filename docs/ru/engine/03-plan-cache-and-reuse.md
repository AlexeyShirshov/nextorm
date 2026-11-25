# Кэш планов и переиспользование: [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))

> Повторно используйте результат подготовки часто выполняемого запроса — SQL и преобразователь строк. Обычные терминалы используют неявный кэш планов; [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) возвращает [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1), которую вы храните и запускаете явно.

**Предварительные требования:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Dependency injection](../getting-started/04-dependency-injection.md).

## Обзор

Терминал, например [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), не просто читает строки. Он превращает [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1), построенную цепочкой методов запроса, в SQL и преобразователь строк, подготавливает `DbCommand` и читает результат. Два механизма ниже позволяют повторно использовать уже выполненную подготовку:

* **неявный кэш планов** — автоматический, с ключом по структурной форме запроса; для APPLY-соединения эта форма включает выражение источника APPLY, поэтому два запроса с одним типом проекции, но разными источниками APPLY — это разные планы;
* **[`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))** — явный, возвращает [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1), который вы храните и выполняете сами.

Внешне механизмы похожи, но различаются тем, как определяется и хранится подготовленный запрос, где он доступен и попадает ли он в кэш:

| | Неявный кэш планов | Явный [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) |
|---|---|---|
| Точка входа | любой терминал на [`EntityBuilder`](xref:NextORM.Core.EntityBuilder) / [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) ([`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})), [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])), ...) | [`Prepare`](xref:NextORM.Core.QueryCommand`1.Prepare(NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) / `EntityBuilder<T>.Prepare()` |
| Ключ поиска | структурный хеш формы запроса (для APPLY-соединения — включая выражение источника APPLY); не готовый SQL-текст | отсутствует — вы храните возвращённую команду |
| Время жизни | до `PurgeQueryCache()` или завершения процесса | пока вы храните ссылку |
| Область действия | **на поток**, разделяется всеми [`IDataContext`](xref:NextORM.Core.IDataContext) в этом потоке | экземпляр, который вы храните |
| Заполняет кэш | да | **нет** |
| Безопасен для конкурентного использования | да, по построению (запись на поток) | да между **разными** соединениями (одна команда на соединение); на одном соединении — только последовательные выполнения |
| Потоковый (`IAsyncEnumerable`) | да | только с `PrepareOptions { Streaming = true }` |

Эта страница помогает найти нужный API и правила кэширования. Измеренная стоимость вариантов здесь не приводится.

## Терминалы на [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1)

`Select(...)` возвращает [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1), на котором объявлены терминальные операторы. Для готового значения выбирайте буферизованный или скалярный терминал, а для перебора строк по мере чтения — потоковый. Каждый терминал из этого раздела использует кэш планов.

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

Аргумент `params object[]` (или `ReadOnlySpan<object?>`) передаёт значения времени выполнения для плейсхолдеров [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)) в запросе.

Например, запрос выбирает `Id` каждой сущности и возвращает строки списком:
```csharp
using var ctx = new SqliteDataContext("Data Source=app.db", new DataContextBuilder());

var rows = await ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .ToListAsync();
```

Запрос формирует такой SQL:
```sql
select id
 from simple_entity
```

Ключ кэша описывает структуру запроса, а не экземпляр лямбды или имена её параметров. Поэтому вызовы с разными экземплярами лямбды, но одинаковой формой используют одну запись кэша — это удобно для запросов внутри цикла. Для APPLY-соединения в форму входит выражение источника APPLY: если источник меняется, запрос получает отдельный план и SQL, даже если тип проекции остаётся тем же. Ключом никогда не бывает готовый SQL:

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

Обе итерации используют одну запись кэша, хотя `entityId` меняется.

## Явный [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))

Явный путь один раз компилирует запрос в [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1), который можно выполнять повторно. `QueryPlanStore` при этом не используется:

Явный путь показан диаграммой в [обзоре движка](index.md).

[`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) объявлен и на [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1), и на `EntityBuilder<T>` и возвращает [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1):

```csharp
public IPreparedQueryCommand<TResult> Prepare(PrepareOptions options = default, CancellationToken cancellationToken = default)
```

Значение по умолчанию (`default(PrepareOptions)`) оптимизировано для буферизованных и скалярных результатов и переиспользует одну базовую команду на соединение. [`PrepareOptions`](xref:NextORM.Core.PrepareOptions) выбирает форму (`Streaming`) и политику переиспользования команды (`Cache`); обе политики описаны в разделе [`PrepareOptions` и `PreparedCommandCache`](#prepareoptions-и-preparedcommandcache). Возвращённая команда не привязана к контексту, который её создал: каждый терминал принимает [`IDataContext`](xref:NextORM.Core.IDataContext) первым аргументом, поэтому одну и ту же подготовленную команду можно выполнить против другого контекста того же провайдера.

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

### `PrepareOptions` и `PreparedCommandCache`

`Prepare` принимает значение с параметрами настройки. Вызов без аргументов (`default(PrepareOptions)`) задаёт буферизованный результат и одну команду, переиспользуемую на соединение.

```csharp
public enum PreparedCommandCache { Connection = 0, None = 1 }

public readonly record struct PrepareOptions
{
    public PreparedCommandCache Cache { get; init; }  // по умолчанию: Connection
    public bool Streaming { get; init; }              // по умолчанию: false (буферизация)
}
```

| `Cache` | Что получает каждое выполнение | Время жизни |
|---|---|---|
| `Connection` (по умолчанию) | команду, уже кэшированную для выполняющего соединения, либо свежесозданную при первом выполнении на нём | одна команда на соединение; живёт, пока соединение не станет собираемым или состояние не будет вытеснено |
| `None` | свежую команду, созданную только для этого выполнения | освобождается по окончании выполнения; для потокового результата — когда освобождается считыватель или перечислитель |

`Cache` управляет повторным использованием команды в подготовленном запросе. Это отдельный механизм, не связанный с неявным кэшем планов, описанным ниже.

В примерах показаны настройки буферизованных и потоковых подготовленных запросов:

```csharp
// Буферизация, одна команда на соединение (безаргументная форма).
var cached = ctx.From<ISimpleEntity>().Select(x => x.Id).Prepare();

// Буферизация, свежая команда на каждое выполнение.
var fresh = ctx.From<ISimpleEntity>().Select(x => x.Id)
    .Prepare(new PrepareOptions { Cache = PreparedCommandCache.None });

// Потоковая передача, одна команда на соединение.
var streamed = ctx.From<ISimpleEntity>().Select(x => x.Id)
    .Prepare(new PrepareOptions { Streaming = true });
```

Если значение `Cache` отсутствует в перечислении, `Prepare` выбрасывает `ArgumentOutOfRangeException` до каких-либо выделений памяти. Ни одна из этих политик не сохраняет план в неявный кэш.

**Состояние на соединение.** Подготовленная команда не владеет одним изменяемым `DbCommand`. Для каждого соединения отдельно хранятся команда, позиции параметров, привязка транзакции и резервирование команды на время выполнения. Подготовленная команда хранит слабую ссылку на это состояние. Поэтому она не удерживает соединение от сборки мусора; когда соединение становится доступным для сборки, запись состояния освобождается. Подготовленная команда также не удерживает транзакцию, контекст или считыватель.

Такое разделение позволяет одновременно выполнять одну подготовленную команду на разных соединениях. У каждого соединения будут свои параметры и свой считыватель.

**Ограничение на одно соединение.** Два *перекрывающихся* выполнения одного подготовленного объекта на **одном** соединении сразу завершаются с `InvalidOperationException`, до изменения состояния команды. Ограничение не распространяется на другие команды или неявный кэш планов этого соединения: это не планировщик для всех команд на соединении. Последовательные выполнения одной подготовленной команды на нём допустимы.

**Жизненный цикл потокового результата.** При `None` команда принадлежит одному выполнению и освобождается вместе со считывателем или перечислителем. При `Connection` закрытие считывателя снимает резервирование команды и возвращает её в состояние этого соединения. Потоковый результат удерживает резервирование, пока вы его не освободите; оставленный открытым поток занимает соединение для этой подготовленной команды.

> **Правила совместного использования.** Одну подготовленную команду можно использовать из разных потоков и контекстов, если каждое выполнение идёт через своё соединение. На одном соединении выполняйте её последовательно и освобождайте каждый потоковый результат.
>
> Значения параметров, транзакция и считыватель относятся к одному выполнению на одном соединении, а не к подготовленному объекту. Для неявного кэша действует другое правило: он привязан к потоку.

### Инспекция и выполнение подготовленной команды

Конкретная подготовленная команда провайдера ([`DbPreparedQueryCommand<TResult>`](xref:NextORM.Core.DbPreparedQueryCommand`1)) предоставляет для просмотра [`SqlStmt`](xref:NextORM.Core.DbPreparedQueryCommand`1.SqlStmt) и [`Behavior`](xref:NextORM.Core.DbPreparedQueryCommand`1.Behavior). Команда, которую выполнение получает на своём соединении, — деталь реализации: к ней привязываются параметры и транзакция этого выполнения, а само выполнение резервирует команду на своё время. Команда не выдаётся наружу, поэтому подготовленный объект не удерживает соединение, транзакцию, контекст или считыватель.

Выбирайте способ работы по тому, что именно нужно просмотреть или выполнить:

| Что нужно | Что использовать |
|---|---|
| подготовленный SQL-текст и заданный режим считывания | `SqlStmt` / `Behavior` на подготовленной команде |
| строки, одна строка или скаляр | типизированные терминалы [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1): `ToList(...)`, `First(...)`, `Single(...)`, `ExecuteScalar(...)` и их async-формы |
| потоковый результат | `ToAsyncEnumerable(...)`, `ToEnumerable(...)`, `CreateEnumerator(...)` / `CreateAsyncEnumerator(...)` |
| выполнение запроса на другом соединении | выполните ту же подготовленную команду против контекста на этом соединении; её состояние на соединение создаётся по требованию и освобождается вместе с соединением |

### Потоковая передача требует `Streaming = true`

Подготовленная команда, созданная со значениями по умолчанию, не владеет [`ResultSetEnumerator<TResult>`](xref:NextORM.Core.ResultSetEnumerator`1), потому что буферизованной форме он не нужен. Буферизованные и скалярные терминалы работают; потоковый терминал завершается ошибкой `InvalidOperationException`. Подготовьте команду для потоковой передачи — задайте `PrepareOptions.Streaming` равным `true`. [`ToEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])) — это ленивый итератор, поэтому на этом пути исключение появляется при начале перечисления, а не в момент вызова.

```csharp
// Буферизация по умолчанию: перечислитель не создаётся.
var buffered = ctx.From<ISimpleEntity>().Select(x => x.Id).Prepare();
buffered.ToList(ctx); // fine

// Потоковая передача: единственный способ получить потоковую подготовленную команду.
var streaming = ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .Prepare(new PrepareOptions { Streaming = true });

await foreach (var id in streaming.ToAsyncEnumerable(ctx))
{
    Console.WriteLine(id);
}
```

### [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) не заполняет кэш планов

[`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) вызывает планировщик с `storeInCache: false`, поэтому его нельзя найти последующим неявным поиском, и он не может «загрязнить» кэш:

```csharp
ctx.PurgeQueryCache();

var prepared = ctx.From<ISimpleEntity>().Select(x => x.Id).Prepare();
prepared.ToList(ctx); // executes, but adds no plan-cache entry

// A later implicit terminal builds its own plan.
var implicitRows = ctx.From<ISimpleEntity>().Select(x => x.Id).ToList();
```

## Область действия кэша планов и его очистка

Неявный кэш (`DataContext._queryPlanCache`) — словарь `[ThreadStatic]` с ключом `(ContextType, QueryPlan)`. Иными словами, план ищется по типу контекста и форме запроса. Для APPLY-соединения форма включает выражение источника APPLY; готовый SQL ключом не служит:

* **На поток.** Поток B не видит запись, созданную в потоке A. После перехода на другой поток пула первый запрос промахнётся, пока этот поток не встретит такую форму.
* **На тип контекста.** SQLite, PostgreSQL и SQL Server порождают разный SQL и разные реализации `DbCommand` для одной и той же формы, поэтому получают отдельные записи.
* **Автоматической инвалидации нет.** Записи хранятся, пока не вызвать `PurgeQueryCache()` или пока не завершится процесс. После изменения схемы очистите кэш либо пересоздайте контекст, прежде чем повторно использовать затронутые формы запросов.
* `QueryCommand<TResult>.Cache = false` отключает хеширование и поиск для этой команды. Это не дешёвый переключатель «пропустить кэш»: он заставляет полностью пересобирать план при каждом вызове.

Чтобы очистить планы одного контекста, вызовите:

```csharp
ctx.PurgeQueryCache();
```

У остальных кэшей другая область действия. Метаданные, списки выборки, скомпилированные делегаты выражений и аксессоры для списков `IN` общие для всего процесса. Кэш выражений в памяти относится к конкретному экземпляру контекста, потому что его записи захватывают сам контекст:

```csharp
using var ctx = new InMemoryDataContext();

ReferenceEquals(ctx.Metadata, DataContextCache.Metadata);                 // true  (process-wide)
ReferenceEquals(ctx.SelectListCache, DataContextCache.SelectListCache);   // true  (process-wide)
ReferenceEquals(ctx.ExpressionsCache, DataContextCache.ExpressionsCache); // false (per instance)
```

## Управление кэшем

`PurgeQueryCache()` очищает планы одного контекста. Следующие параметры очищают общие кэши процесса или задают работу кэша планов для контекста. Флаг `QueryCommand<TResult>.Cache` не сбрасывается после изменения: заданное значение остаётся в команде. Эти параметры его не меняют, поэтому такое отключение кэша не затрагивает общую команду `Any`:

* [`DataContextCache.Clear()`](xref:NextORM.Core.DataContextCache.Clear) очищает все кэши процесса: метаданные, списки выборки, скомпилированные делегаты для выражений и списков `IN`, селекторы идентичности `JoinInto`, преобразователи строк, псевдонимы проекций и источники `FROM`. При следующем обращении каждого потока к кэшу планов его записи будут сброшены. При следующем использовании селекторы идентичности перестраиваются по текущим метаданным, поэтому зарегистрированная после очистки конфигурация ключа заменяет устаревшую. Вызывайте `Clear()` после изменения схемы.

  `Clear()` также удаляет глобальные фильтры запросов, импортированные мостом EF Core. При следующем запросе или построении DML контекст, привязанный к мосту, безопасно завершится с документированным `InvalidOperationException`, а не выполнит запрос без фильтра. Чтобы восстановить работу, создайте контекст через мост или привяжите его заново (см. [Мост фильтров запросов EF Core](../advanced/integration-efcore.md#жизненный-цикл-восстановление-и-ограниченная-конкурентность)).
* [`DataContextBuilder.UseQueryCache(false)`](xref:NextORM.Core.DataContextBuilder.UseQueryCache(System.Boolean)) — или свойство `DataContext.QueryCacheEnabled` / `InMemoryDataContext.QueryCacheEnabled` — перестраивает каждый план вместо кэширования. Значение передаётся как `storeInCache: false` для вызова и не трогает `QueryCommand<TResult>.Cache`.
* [`DataContextBuilder.UseCacheSlidingExpiration(ttl)`](xref:NextORM.Core.DataContextBuilder.UseCacheSlidingExpiration(System.TimeSpan)) включает ленивое вытеснение кэшей процесса. Если к записи не обращались в течение `ttl`, её удаляют при следующем обращении; каждое чтение продлевает срок её жизни. `TimeSpan.Zero` (по умолчанию) отключает вытеснение. Маппинг сущности с ключом `HasShardKey` не вытесняется по времени. Если так удалён фильтр запросов EF Core, контекст, привязанный к мосту, завершит следующий запрос или построение DML с документированным `InvalidOperationException`, а не выполнит его без фильтра — так же, как после `Clear`.

```csharp
var builder = new DataContextBuilder().UseQueryCache(false);
using var ctx = new SqliteDataContext("Data Source=app.db", builder); // каждый терминал пересобирает план

new DataContextBuilder().UseCacheSlidingExpiration(TimeSpan.FromMinutes(10)); // ленивое вытеснение, на процесс

DataContextCache.Clear(); // очистить общие кэши и кэш планов
ctx.PurgeQueryCache();    // очистить планы этого контекста
```

## Захваченные коллекции `in`/`Contains`

Список `in` или вызов `Contains` по захваченной коллекции становится параметризованным предикатом `IN`. Ключ плана учитывает *форму* коллекции — вычисленное количество элементов и наличие `null`, — но не сами *значения*. От этого зависит, когда можно повторно использовать план:

* Вызовы с одинаковой длиной коллекции и одинаковым наличием `null` используют один план, даже если они находятся в разных местах кода.
* Если перед следующим выполнением коллекция выросла или была заменена, хэш формы меняется. NextORM создаёт план с новым числом параметров и читает текущие значения через кэшированный механизм доступа к ним.

В этом примере сначала строится план для одного значения, а после изменения коллекции — новый план для двух:
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

Для запроса с двумя значениями формируется такой SQL:
```sql
id in ($p0, $p1)
```

`values.Contains(c.Id)` обрабатывается так же. Элемент `null` добавляет ветвь `is null` (например, `(nullableint in ($p0) or nullableint is null)`), а пустая коллекция или коллекция целиком из null сворачивается в константу (`1 = 0` / `is null`) без параметров.

Захваченная коллекция, индексируемая выражением запроса (`dict[column]`), сворачивается так же: число записей входит в ключ плана, поэтому сгенерированный `CASE` перестраивается при изменении коллекции. См. [Фильтрация (WHERE)](../guide/03-filtering-where.md).

## Различия провайдеров

Механизм повторного использования одинаков для провайдеров баз данных, но каждый из них создаёт свой тип команды. Режим постраничной выборки SQL Server также входит в форму плана. У in-memory-провайдера собственный кэш скомпилированных запросов, как показано ниже:

| Провайдер | Поведение |
|---|---|
| SQLite | Кэш планов и [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) — основное поведение; подготовленная команда оборачивает `SqliteCommand`. |
| SQL Server | Тот же механизм; разбиение на страницы `TOP` против `OFFSET ... FETCH` является частью записанной формы плана, поэтому режим разбиения на страницы «запекается» в кэшированный SQL. |
| PostgreSQL | Тот же механизм; подготовленная команда оборачивает `NpgsqlCommand`. |
| MySQL | Кэш планов и [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) — основное поведение; подготовленная команда оборачивает `MySqlCommand`. |
| MariaDB | Тот же механизм; подготовленная команда оборачивает `MySqlCommand` (драйвер MySQL). |
| ClickHouse | Кэш планов и [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) — основное поведение; подготовленная команда оборачивает `ClickHouseCommand`. |
| In-memory | [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) хранит собственный кэш скомпилированных запросов и возвращает [`InMemoryPreparedQueryCommand<TResult>`](xref:NextORM.Core.InMemoryPreparedQueryCommand`1); [`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) работает, но [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String)) для raw-SQL не поддерживается (`NotSupportedException`). |

## См. также

* [Connections](../infrastructure/01-connections.md)
* [Dependency injection](../getting-started/04-dependency-injection.md)
* [Documentation index](../index.md)

---

Source: `tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:48` (buffered then streaming),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:108` (`Prepare(new PrepareOptions { Streaming = true })`),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:133` (buffered reuse),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:160` (streaming the default throws),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:404` ([`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) does not populate the cache);
`tests/nextorm.sqlite.tests/PlanCache/InListCacheTests.cs:41` (same-shape captured collection),
`tests/nextorm.sqlite.tests/PlanCache/InListCacheTests.cs:92` (reassigned array),
`tests/nextorm.sqlite.tests/PlanCache/InListCacheTests.cs:117` (grown list);
`tests/nextorm.core.tests/Caching/DataContextCacheScopeTests.cs:20` (cache sharing scope);
`tests/nextorm.core.tests/Caching/QueryCacheControlsTests.cs:45` (clear, disable without mutating `Cache`, sliding expiration);
`tests/nextorm.sqlite.tests/PlanCache/DataContextCacheClearTests.cs:31` (`DataContextCache.Clear` invalidates the plan cache);
`tests/nextorm.integration.tests/CommonTestSuite/Caching/CommonTestSuite.Cache.cs:6`;
`src/nextorm.core/Query/Commands/QueryCommand.TResult.cs:81` ([`Prepare`](xref:NextORM.Core.EntityBuilderExtensions.Prepare``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))),
`src/nextorm.core/DataContext/Cache/Preparation/PrepareOptions.cs:6` (`PreparedCommandCache` / `PrepareOptions`),
`src/nextorm.core/DataContext/Cache/Commands/DbPreparedQueryCommand.cs:199` (`Acquire`: состояние на соединение и отказ при перекрытии на одном соединении),
`src/nextorm.core/DataContext/Cache/Commands/IPreparedQueryCommand.cs:5` (prepared terminals),
`src/nextorm.core/DataContext/DataContext.cs:315` (`GetPreparedQueryCommand`).
