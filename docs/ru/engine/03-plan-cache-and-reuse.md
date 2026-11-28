# Кэш планов и переиспользование

> NextORM переиспользует SQL и преобразователь строк через неявный кэш планов. Для явного переиспользования вызовите [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) или [`PrepareStreaming`](xref:NextORM.Core.EntityBuilderExtensions.PrepareStreaming``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) и сохраните возвращённую команду.

**Предварительные требования:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md) · [Dependency injection](../getting-started/04-dependency-injection.md).

## Обзор

Терминал, например [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), не просто читает строки. Он превращает [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1), построенную цепочкой методов запроса, в SQL и преобразователь строк, подготавливает `DbCommand` и читает результат. Два механизма ниже позволяют повторно использовать уже выполненную подготовку:

* **неявный кэш планов** — автоматический, с ключом по структурной форме запроса; для APPLY-соединения эта форма включает выражение источника APPLY, поэтому два запроса с одним типом проекции, но разными источниками APPLY — это разные планы;
* **Явная подготовка** — [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) или [`PrepareStreaming`](xref:NextORM.Core.EntityBuilderExtensions.PrepareStreaming``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) возвращает команду для повторного выполнения.

Внешне механизмы похожи, но различаются тем, как определяется и хранится подготовленный запрос, где он доступен и попадает ли он в кэш:

| | Неявный кэш планов | Явный подготовка |
|---|---|---|
| Точка входа | [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) / [`PrepareStreaming`](xref:NextORM.Core.EntityBuilderExtensions.PrepareStreaming``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) |
| Ключ поиска | совместимые настройки провайдера и маппинга, а также проверенная структура запроса | отсутствует — вы храните возвращённую команду |
| Время жизни | до очистки кэша или завершения процесса; для сравнения некоторых пользовательских форм запросов также нужна живая команда | пока вы храните ссылку |
| Область действия | **весь процесс**, для совместимых SQL-контекстов | экземпляр, который вы храните |
| Заполняет кэш | да | **нет** |
| Конкурентное использование | независимые контексты на **разных** соединениях переиспользуют совместимый план | независимые контексты на **разных** соединениях переиспользуют подготовленный объект |
| Потоковый (`IAsyncEnumerable`) | да | с `PrepareStreaming()` |

Эта страница помогает найти нужный API и правила кэширования. Измеренная стоимость вариантов здесь не приводится.

## Терминалы на [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1)

`Select(...)` возвращает [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1), на котором объявлены терминальные операторы. Для готового значения выбирайте буферизованный или скалярный терминал, а для перебора строк по мере чтения — потоковый. Терминалы из этого раздела используют неявный кэш, если запрос можно кэшировать.

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

При совпадении хеша кэш сравнивает структуру запросов. Разные экземпляры лямбды одинаковой формы могут переиспользовать одну запись. В форму APPLY-соединения входит выражение источника, поэтому его изменение может привести к построению отдельного плана даже при том же типе проекции. Цикл переиспользует план и привязывает своё значение `entityId` при каждом вызове:

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

## Явная подготовка

Явная подготовка компилирует запрос один раз и обходит `QueryPlanStore`.

Явный путь показан диаграммой в [обзоре движка](index.md).

Оба метода доступны на `QueryCommand<TResult>`, `EntityBuilder<T>` и `IRepository<T>`. Выбирайте метод по способу чтения результата:

```csharp
public IPreparedBufferedQueryCommand<TResult> PrepareBuffered(PrepareOptions options = default, CancellationToken cancellationToken = default)
public IPreparedStreamingQueryCommand<TResult> PrepareStreaming(PrepareOptions options = default, CancellationToken cancellationToken = default)
```

По умолчанию оба метода переиспользуют одну базовую команду на соединение. `PrepareOptions.Cache` задаёт политику переиспользования. Каждый терминал принимает `IDataContext` первым аргументом: команду можно выполнить в другом контексте того же провайдера.

```csharp
var prepared = ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .PrepareBuffered();

// Terminal methods declared on IPreparedBufferedQueryCommand<TResult>.
List<int> all = prepared.ToList(ctx);
int first = prepared.First(ctx);
List<int> asyncAll = await prepared.ToListAsync(ctx);
int? scalar = prepared.ExecuteScalar(ctx, throwIfNull: false);
```

Одну и ту же команду можно повторно выполнять с разными значениями параметров времени выполнения:

```csharp
var byId = ctx.From<ISimpleEntity>()
    .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
    .Select(x => x.Id)
    .PrepareBuffered();

var one = await byId.FirstAsync(ctx, 42);    // 42 fills norm_p0
var many = await byId.ToListAsync(ctx, 43);  // same prepared command, new value
```

### `PrepareOptions` и `PreparedCommandCache`

Оба метода принимают `PrepareOptions`. Значение по умолчанию переиспользует одну команду на соединение.

```csharp
public enum PreparedCommandCache { Connection = 0, None = 1 }

public readonly record struct PrepareOptions
{
    public PreparedCommandCache Cache { get; init; }  // по умолчанию: Connection
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
var cached = ctx.From<ISimpleEntity>().Select(x => x.Id).PrepareBuffered();

// Буферизация, свежая команда на каждое выполнение.
var fresh = ctx.From<ISimpleEntity>().Select(x => x.Id)
    .PrepareBuffered(new PrepareOptions { Cache = PreparedCommandCache.None });

// Потоковая передача, одна команда на соединение.
var streamed = ctx.From<ISimpleEntity>().Select(x => x.Id)
    .PrepareStreaming();
```

Если значение `Cache` отсутствует в перечислении, оба метода выбрасывают `ArgumentOutOfRangeException` до подготовки команды. Ни одна политика переиспользования команды не сохраняет план в неявном кэше.

**Состояние на соединение.** Подготовленная команда не владеет одним изменяемым `DbCommand`. Для каждого соединения отдельно хранятся команда, позиции параметров, привязка транзакции и резервирование команды на время выполнения. Подготовленная команда хранит слабую ссылку на это состояние. Поэтому она не удерживает соединение от сборки мусора; когда соединение становится доступным для сборки, запись состояния освобождается. Подготовленная команда также не удерживает транзакцию, контекст или считыватель.

Такое разделение позволяет одновременно выполнять одну подготовленную команду на разных соединениях. У каждого соединения будут свои параметры и свой считыватель.

**Ограничение на одно соединение.** Если выполняющийся запрос уже зарезервировал состояние команды для плана на соединении, второй вызов того же плана на этом соединении получает `InvalidOperationException` до изменения состояния команды. Эта проверка действует для состояния команды данного плана. Другие перекрывающиеся операции на контексте или соединении не поддерживаются и могут не привести к такому исключению.

**Жизненный цикл потокового результата.** При `None` команда принадлежит одному выполнению и освобождается вместе со считывателем или перечислителем. При `Connection` закрытие считывателя снимает резервирование команды и возвращает её в состояние этого соединения. Потоковый результат удерживает резервирование, пока вы его не освободите; оставленный открытым поток занимает соединение для этой подготовленной команды.

> **Правила совместного использования.** Запускайте параллельные запросы через независимые контексты на разных соединениях. На каждом контексте и на каждом соединении допускается только одна активная операция, даже если два контекста используют одно соединение. Последовательное выполнение может продолжиться на другом потоке после `await`. Потоковая операция остаётся активной, пока вы не дочитаете или не освободите считыватель либо перечислитель.

### Буферизованное и потоковое выполнение

`PrepareBuffered()` возвращает команду с `ToList`, `First`, `Single`, `ExecuteScalar` и их асинхронными вариантами. `PrepareStreaming()` возвращает команду с `ToEnumerable`, `ToAsyncEnumerable` и методами создания перечислителей. Каждый интерфейс предоставляет операции только своего режима.

```csharp
// Буферизация по умолчанию: перечислитель не создаётся.
var buffered = ctx.From<ISimpleEntity>().Select(x => x.Id).PrepareBuffered();
buffered.ToList(ctx); // fine

// Потоковая передача: единственный способ получить потоковую подготовленную команду.
var streaming = ctx.From<ISimpleEntity>()
    .Select(x => x.Id)
    .PrepareStreaming();

await foreach (var id in streaming.ToAsyncEnumerable(ctx))
{
    Console.WriteLine(id);
}
```

### Явная подготовка обходит кэш планов

Оба метода вызывают планировщик с `storeInCache: false`:

```csharp
ctx.PurgeQueryCache();

var prepared = ctx.From<ISimpleEntity>().Select(x => x.Id).PrepareBuffered();
prepared.ToList(ctx); // executes, but adds no plan-cache entry

// A later implicit terminal builds its own plan.
var implicitRows = ctx.From<ISimpleEntity>().Select(x => x.Id).ToList();
```

## Область действия кэша планов и его очистка

Неявный кэш SQL-планов общий для процесса. Для переиспользования плана у контекстов должны совпадать провайдер, фактический диалект, маппинг и настройки генерации SQL. Для совместимых запросов действуют следующие правила:

* **Переиспользование плана.** При поиске NextORM сверяет структуру запроса и учитывает то, что меняет форму SQL, например литералы и длину списков `IN`. Смена потока, соединения или обычного захваченного скалярного значения сама по себе не создаёт отдельный план.
* **Одновременный первый вызов.** Два вызова могут построить свои планы для одной формы. При следующих вызовах используется первый опубликованный совместимый план.
* **Состояние выполнения.** NextORM привязывает текущие значения при каждом вызове. У каждого экземпляра `DbConnection` своё состояние команды для плана, а у каждого выполнения — свой считыватель.
* **Отключение для команды.** Задайте `QueryCommand<TResult>.Cache = false`, чтобы пропустить поиск и публикацию плана для этой команды.

Чтобы очистить общие SQL-планы, дождитесь завершения активных операций и вызовите следующий метод на SQL-контексте:

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

На SQL-контексте `PurgeQueryCache()` очищает общие неявные SQL-планы и локальные данные кэша запросов этого контекста. На in-memory-контексте метод очищает скомпилированные запросы этого экземпляра. Очищайте кэш и меняйте маппинг, только когда затронутые запросы не выполняются. Перечисленные ниже средства не меняют флаг `Cache` самой команды:

* [`DataContextCache.Clear()`](xref:NextORM.Core.DataContextCache.Clear) очищает общие неявные SQL-планы, метаданные, преобразователи строк и другие кэши трансляции запросов. Вызывайте `Clear()` после изменения схемы или маппинга. Следующие запросы подготовятся по актуальным метаданным.

  `Clear()` также удаляет глобальные фильтры запросов, импортированные мостом EF Core. При следующем запросе или построении DML контекст, привязанный к мосту, безопасно завершится с документированным `InvalidOperationException`, а не выполнит запрос без фильтра. Чтобы восстановить работу, создайте контекст через мост или привяжите его заново (см. [Мост фильтров запросов EF Core](../advanced/integration-efcore.md#жизненный-цикл-восстановление-и-ограниченная-конкурентность)).
* [`DataContextBuilder.UseQueryCache(false)`](xref:NextORM.Core.DataContextBuilder.UseQueryCache(System.Boolean)) — или свойство `DataContext.QueryCacheEnabled` / `InMemoryDataContext.QueryCacheEnabled` — перестраивает каждый план вместо кэширования. Значение передаётся как `storeInCache: false` для вызова и не трогает `QueryCommand<TResult>.Cache`.
* [`DataContextBuilder.UseCacheSlidingExpiration(ttl)`](xref:NextORM.Core.DataContextBuilder.UseCacheSlidingExpiration(System.TimeSpan)) включает ленивое вытеснение кэшей процесса. Если к записи не обращались в течение `ttl`, её удаляют при следующем обращении; каждое чтение продлевает срок её жизни. `TimeSpan.Zero` (по умолчанию) отключает вытеснение. Маппинг сущности с ключом `HasShardKey` не вытесняется по времени. Если так удалён фильтр запросов EF Core, контекст, привязанный к мосту, завершит следующий запрос или построение DML с документированным `InvalidOperationException`, а не выполнит его без фильтра — так же, как после `Clear`.

```csharp
var builder = new DataContextBuilder().UseQueryCache(false);
using var ctx = new SqliteDataContext("Data Source=app.db", builder); // каждый терминал пересобирает план

new DataContextBuilder().UseCacheSlidingExpiration(TimeSpan.FromMinutes(10)); // ленивое вытеснение, на процесс

DataContextCache.Clear(); // очистить общие кэши и кэш планов
ctx.PurgeQueryCache();    // очистить общие SQL-планы и локальные данные кэша запросов
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
| SQLite | Кэш планов и [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) — основное поведение; подготовленная команда оборачивает `SqliteCommand`. |
| SQL Server | Тот же механизм; разбиение на страницы `TOP` против `OFFSET ... FETCH` является частью записанной формы плана, поэтому режим разбиения на страницы «запекается» в кэшированный SQL. |
| PostgreSQL | Тот же механизм; подготовленная команда оборачивает `NpgsqlCommand`. |
| MySQL | Кэш планов и [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) — основное поведение; подготовленная команда оборачивает `MySqlCommand`. |
| MariaDB | Тот же механизм; подготовленная команда оборачивает `MySqlCommand` (драйвер MySQL). |
| ClickHouse | Кэш планов и [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) — основное поведение; подготовленная команда оборачивает `ClickHouseCommand`. |
| In-memory | [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) хранит собственный кэш скомпилированных запросов и возвращает [`InMemoryPreparedQueryCommand<TResult>`](xref:NextORM.Core.InMemoryPreparedQueryCommand`1); [`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) работает, но [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String)) для raw-SQL не поддерживается (`NotSupportedException`). |

## См. также

* [Connections](../infrastructure/01-connections.md)
* [Dependency injection](../getting-started/04-dependency-injection.md)
* [Documentation index](../index.md)

---

Source: `tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:48` (buffered then streaming),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:108` (`PrepareStreaming()`),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:133` (buffered reuse),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs` (раздельные интерфейсы выполнения),
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:404` ([`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken)) does not populate the cache);
`tests/nextorm.sqlite.tests/PlanCache/InListCacheTests.cs:41` (same-shape captured collection),
`tests/nextorm.sqlite.tests/PlanCache/InListCacheTests.cs:92` (reassigned array),
`tests/nextorm.sqlite.tests/PlanCache/InListCacheTests.cs:117` (grown list);
`tests/nextorm.core.tests/Caching/DataContextCacheScopeTests.cs:20` (cache sharing scope);
`tests/nextorm.core.tests/Caching/QueryCacheControlsTests.cs:45` (clear, disable without mutating `Cache`, sliding expiration);
`tests/nextorm.sqlite.tests/PlanCache/DataContextCacheClearTests.cs:31` (`DataContextCache.Clear` invalidates the plan cache);
`tests/nextorm.integration.tests/CommonTestSuite/Caching/CommonTestSuite.Cache.cs:6`;
`src/nextorm.core/Query/Commands/QueryCommand.TResult.cs:81` ([`PrepareBuffered`](xref:NextORM.Core.EntityBuilderExtensions.PrepareBuffered``1(NextORM.Core.EntityBuilder{``0},NextORM.Core.PrepareOptions,System.Threading.CancellationToken))),
`src/nextorm.core/DataContext/Cache/Preparation/PrepareOptions.cs:6` (`PreparedCommandCache` / `PrepareOptions`),
`src/nextorm.core/DataContext/Cache/Commands/DbPreparedQueryCommand.cs:199` (`Acquire`: состояние на соединение и отказ при перекрытии на одном соединении),
`src/nextorm.core/DataContext/Cache/Commands/IPreparedBufferedQueryCommand.cs`, `src/nextorm.core/DataContext/Cache/Commands/IPreparedStreamingQueryCommand.cs`,
`src/nextorm.core/DataContext/DataContext.cs:315` (`GetPreparedQueryCommand`).
