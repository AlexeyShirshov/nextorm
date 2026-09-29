# Выполнение утверждений одним батчем

> Батч отправляет несколько утверждений в базу **одним round trip на одной серверной сессии**. Именно это делает session-scoped временную таблицу пригодной за пулером уровня соединения, держит мутацию и читающий её запрос на одном backend и убирает лишний round trip, когда материализация сразу же читается. Собрать его можно через [`BatchExtensions.Batch`](xref:NextORM.Core.BatchExtensions).

**Предварительно:** [Материализация запроса в таблицу](18-create-table-as.md) · [Транзакции](21-transactions.md) · [Обзор провайдеров](../providers/overview.md)

## Зачем нужен батч

Каждое утверждение, отправленное отдельной командой, — это отдельный round trip, а при пулере уровня соединения (PgBouncer в режиме `transaction` или любой аналогичный балансировщик) следующий запрос может уехать на другой backend. Когда утверждения связаны — одно готовит данные, которое читает следующее, или мутация должна быть видна следующему запросу — эту связь нужно удержать в одной сессии, иначе результат окажется недоступен.

Одного открытого клиентского соединения недостаточно. Явная транзакция решает проблему (PgBouncer закрепляет сервер на весь `BEGIN … COMMIT`) и уместна, когда таблицу читают несколько запросов в рамках транзакции. Когда цель — просто «выполнить несколько утверждений подряд и прочитать результат», **батч** даёт ту же одну сессию за один round trip, без открытия транзакции: утверждения уходят одним пакетом, и промежуточный результат сразу виден следующему утверждению.

Материализация и немедленное чтение — частый, но не единственный случай. `ToTempTable("recent_orders")` создаёт таблицу, поэтому её можно прочитать обратно через `From("recent_orders")`:

```csharp
ctx.From<IOrder>()
    .Where(x => x.Total > minTotal)
    .Select(x => new { x.Id, x.Total })
    .ToTempTable("recent_orders");

var orders = ctx.From("recent_orders")
    .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") })
    .ToList();
```

Это **две команды**, которые пулер уровня соединения как раз может развести по разным backend'ам, и чтение упадёт с `relation "…" does not exist`. Материализация и читающий запрос собираются в один батч:

```csharp
var orders = ctx.Batch()
    .CreateTempTable("recent_orders", ctx.From<IOrder>()
        .Where(x => x.Total > minTotal)
        .Select(x => new { x.Id, x.Total }))
    .Query(ctx.From("recent_orders")
        .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") }))
    .ToList();
```

Рендерится одним батчем:

```sql
-- PostgreSQL
create temporary table recent_orders as select id, total from orders
 where (total > @b0_minTotal);
select id, total from recent_orders
```

`Query<TResult>` возвращает терминал `BatchQuery<TResult>`:

| Член | Действие |
|---|---|
| `ToList()` | Выполняет батч и возвращает строки результата. |
| `ToListAsync(cancellationToken = default)` | Асинхронный двойник `ToList`. |
| `ToAsyncEnumerable(cancellationToken = default)` | Стримит строки результата; reader остаётся открытым на весь батч, поэтому батч выполняется при старте перечисления и держит соединение до его завершения. |
| `ToSql()` | Рендерит батч (утверждения, склеенные `;`) без выполнения. |

## Ленивые временные таблицы

Ленивая временная таблица из [`AsTempTable`](18-create-table-as.md#ленивые-временные-таблицы-astemptable) — декларативная форма этого батча: при построении источника ничего не выполняется, а чтение через `From(...)` компилируется в шаг `CreateTempTable`, за которым идёт чтение, одним батчем. Это удобно, когда одна и та же материализация читается несколько раз или несколькими вызовами терминалов: каждое чтение материализует таблицу заново, поэтому пулер не может отдать чтению backend, на котором таблицы нет.

```csharp
var source = ctx.From<IOrder>()
    .Where(x => x.Total > minTotal)
    .Select(x => new { x.Id, x.Total })
    .AsTempTable();   // ничего не выполнено

var orders = ctx.From(source)   // DROP + CREATE TEMP + чтение, один батч
    .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") })
    .ToList();
```

Каждое чтение добавляет в начало `DROP TABLE IF EXISTS`, поэтому батч самодостаточен и повторяем:

```sql
-- PostgreSQL
drop table if exists __nextorm_temp_xxxxxxxx;
create temporary table __nextorm_temp_xxxxxxxx as select id, total from orders
 where (total > @b0_minTotal);
select id, total from __nextorm_temp_xxxxxxxx
```

`ToBatchSql()` рендерит этот батч без выполнения, а каждый терминал (`ToList`, `First`, `Single`, `Count`, `Any`, `ToAsyncEnumerable`, …) выполняет его. Ленивый источник может сам читать другой ленивый источник; тогда таблицы материализуются в порядке зависимостей до чтения. Команда чтения рендерится заново на каждое выполнение, поэтому в plan cache она не попадает (как и любой план батча), а захваченные переменные становятся параметрами как обычно.

`AsTempTable` доступен ровно там, где `CreateTempTable` — PostgreSQL, SQLite, MySQL и MariaDB; SQL Server (используйте `CreateTable` с именем с префиксом `#`), ClickHouse и in-memory-контекст отклоняют его при рендере запроса.

## Сборка батча напрямую

`ctx.Batch()` возвращает `BatchBuilder` для нескольких утверждений, DML-шага с побочным эффектом или явного порядка.

Материализация + чтение:

```csharp
var rows = ctx.Batch()
    .CreateTempTable("recent_orders", ctx.From<IOrder>().Where(x => x.Total > minTotal).Select(x => new { x.Id, x.Total }))
    .CreateTempTable("recent_ids", ctx.From("recent_orders").Select(t => new { Id = t.GetInt32("id") }))
    .Query(ctx.From("recent_ids").Select(t => new { Id = t.GetInt32("id") }))
    .ToList();
```

```sql
-- PostgreSQL
create temporary table recent_orders as select id, total from orders
 where (total > @b0_minTotal);
create temporary table recent_ids as select id from recent_orders;
select id from recent_ids
```

Замена постоянной таблицы — `CreateTable` с `DropExisting` сначала удаляет её, поэтому шаг можно выполнять повторно, а не падать на втором прогоне:

```csharp
var rows = ctx.Batch()
    .CreateTable("order_archive", ctx.From<IOrder>().Select(x => new { x.Id, x.Total }),
        o => o.DropExisting())
    .Query(ctx.From("order_archive").Select(t => new { Id = t.GetInt32("id") }))
    .ToList();
```

```sql
-- PostgreSQL
drop table if exists order_archive;
create table order_archive as select id, total from orders;
select id from order_archive
```

`DropExisting` допустим только для постоянного `CreateTable` (у `CreateTempTable` таблица живёт в сессии, и опция отклоняется) и несовместим с `IfNotExists`.

Мутация + чтение — запрос видит обновление на той же сессии:

```csharp
var updated = ctx.Batch()
    .Update(ctx.Update<IOrder>().Set(x => x.Status, "shipped").Where(x => x.Id == id))
    .Query(ctx.From<IOrder>().Where(x => x.Id == id).Select(x => new { x.Id, x.Status }))
    .ToList();
```

```sql
-- PostgreSQL
update orders set status = @p0 where (id = @b0_id);
select id, status from orders
 where (id = @b1_id)
```

* `CreateTempTable(name, source, options?)` и `CreateTable(name, source, options?)` добавляют материализации, по порядку, сколько угодно. Опции можно передать как `CreateTableOptions` или как callback `CreateTableOptionsBuilder` (`o => o.DropExisting()`); постоянный `CreateTable` с `DropExisting` добавляет перед шагом `DROP TABLE IF EXISTS`, и шаг заменяет существующую таблицу.
* `Insert(insert)`, `Update(update)`, `Delete(delete)` и `Truncate(truncate)` добавляют DML-утверждение с побочным эффектом, по порядку, сколько угодно. Они принимают те же билдеры, что и `ctx.InsertInto<T>()`, `ctx.Update<T>()`, `ctx.DeleteFrom<T>()`, `ctx.Truncate<T>()`; терминал билдера (`Insert()`, `Update()`, …) не вызывается — утверждение выполняет батч.
* `Query<TResult>(query)` добавляет единственный результат-несущий запрос и возвращает терминал; он должен быть последним. Второй `Query` или любое утверждение после `Query` бросают `InvalidOperationException`. Чтобы пронести несколько наборов результатов, используйте `AddQuery<TResult>` и `Execute`/`ExecuteAsync` (см. [Несколько наборов результатов](#несколько-наборов-результатов)).
* Каждое утверждение должно быть построено на контексте самого батча; утверждение, привязанное к другому `IDataContext`, отклоняется `ArgumentException`.

Параметры нумеруются по всему батчу, а захваченная переменная, используемая более чем одним утверждением, получает префикс на утверждение (`b0_`, `b1_`, …), поэтому имена placeholders не конфликтуют — в том числе в `;`-склеенной форме. Захваченная переменная, использованная в одном утверждении дважды, даёт одну запись параметра.

Блоки SQL выше — рендер PostgreSQL. По умолчанию `ToSql()` печатает утверждения одной строкой через `; `; чтобы получить разбивку по строкам, как в примерах, включите `DataContextBuilder.UseMultilineBatchSql()`. На SQL Server та же связка выглядит иначе: `CREATE TEMPORARY TABLE ... AS SELECT` там нет, `CreateTempTable` бросает `NotSupportedException`, поэтому временная цель задаётся `CreateTable` с именем, начинающимся с `#`:

```sql
-- SQL Server
select id, total into #recent_orders from orders
 where (total > @b0_minTotal);
select id, total from #recent_orders
```

## Несколько наборов результатов

Батч может нести **больше одного** результат-несущего запроса. Добавляйте каждый через `AddQuery<TResult>` вместо терминала `Query<TResult>` для единственного результата, затем выполните весь батч один раз через `Execute()`/`ExecuteAsync(...)` и прочитайте наборы в порядке добавления:

```csharp
record OrderTotal(int Id, decimal Total);
record OrderLine(int Id, string Status);

var result = ctx.Batch()
    .AddQuery(ctx.From<IOrder>().Where(x => x.Status == "open")
        .Select(x => new OrderTotal(x.Id, x.Total)))
    .AddQuery(ctx.From<IOrder>().Where(x => x.Status == "shipped")
        .Select(x => new OrderLine(x.Id, x.Status)))
    .Execute();

var open = result.Read<OrderTotal>();       // первый добавленный запрос
var shipped = result.Read<OrderLine>();     // второй добавленный запрос
```

Оба запроса уходят в базу **одним** батчем — один round trip, одна серверная сессия:

```sql
-- PostgreSQL
select id, total from orders
 where (status = @b0_status);
select id, status from orders
 where (status = @b1_status)
```

`AddQuery` возвращает билдер, поэтому вызовы можно связывать в цепочку. Каждый результат-несущий запрос должен идти после утверждений с побочным эффектом: материализация или DML-шаг, добавленный после первого `AddQuery`, бросает `InvalidOperationException`. `AddQuery` и `Query` взаимно исключают друг друга: `Query` завершает батч, поэтому последующий `AddQuery` бросает исключение, и `Query` после `AddQuery` — тоже. `Execute()`/`ExecuteAsync()` без добавленного результат-несущего запроса бросает `InvalidOperationException`.

Они возвращают [`BatchResult`](xref:NextORM.Core.BatchResult):

| Член | Действие |
|---|---|
| `ResultSetCount` | Число наборов результатов, которые произвёл батч. |
| `Read<TResult>()` | Возвращает следующий набор в порядке добавления запросов; каждый набор читается один раз. |

`Read<TResult>()` типизирован: запрошенный тип должен совпадать с проектируемым типом следующего запроса, иначе бросается `InvalidOperationException`; чтение после последнего набора тоже бросает `InvalidOperationException`. `BatchResult` **нетерпеливый** (eager) и не владеет ни reader'ом, ни соединением — каждый набор буферизуется до возврата `Execute()`, поэтому он не освобождаемый (не `IDisposable`) и не стримит.

Для батча с единственным результатом терминал `Query<TResult>` по-прежнему рендерится без выполнения через `BatchQuery<TResult>.ToSql()`, а многорезультатная форма `AddQuery`/`Execute` рендерит весь батч без выполнения через `BatchBuilder.ToSql()` — оба рендерят тот же текст, что и выполнение, причём `BatchBuilder.ToSql()` требует хотя бы одного результат-несущего шага (иначе бросает `InvalidOperationException`).

## Поддержка провайдерами

| Провайдер | Механизм |
|---|---|
| PostgreSQL | `NpgsqlBatch`; его implicit transaction закрепляет один backend — ровно то, что нужно transaction-mode пулеру. |
| MySQL / MariaDB | `MySqlBatch`. |
| SQLite | Одна `;`-склеенная команда (нет `DbBatch`). |
| SQL Server | Одна `;`-склеенная команда. `SqlBatch` намеренно не используется: он выполняет каждую команду в своей области видимости, поэтому созданная одной командой `#temp` не была бы видна следующей. Таблицу уровня сессии задавайте именем с префиксом `#` и `CreateTable` (на SQL Server нет `CREATE TEMPORARY TABLE ... AS SELECT`). |
| ClickHouse | отклоняет — `NotSupportedException`. |
| In-memory | отклоняет — `NotSupportedException`. |

Возможность — [`ISqlDialect.SupportsBatch`](xref:NextORM.Core.ISqlDialect.SupportsBatch); провайдер без неё (и in-memory-контекст) падает сразу, а не деградирует молча до отдельных команд.

## Ограничения

* Батч завершается одним или несколькими результат-несущими запросами — одним, добавленным терминалом `Query<TResult>`, либо несколькими, добавленными `AddQuery<TResult>` и выполненными через `Execute`/`ExecuteAsync`, — и они последние. Утверждения перед ними — с побочным эффектом: материализации и DML (`INSERT`/`UPDATE`/`DELETE`/`TRUNCATE`), не возвращающие колонок.
* Многорезультатное выполнение **последовательное и нетерпеливое**: `BatchResult.Read<TResult>()` возвращает наборы в порядке добавления запросов, каждый набор читается один раз, а все наборы полностью буферизуются в памяти до возврата `Execute()`/`ExecuteAsync()` — стриминга и произвольного доступа нет. Стримит только терминал `BatchQuery<TResult>` с единственным результатом, через `ToAsyncEnumerable`.
* Мутация, добавленная в батч, не может запрашивать возврат строк: терминалы `Returning()`/`ReturningIdentity()` дают другой тип билдера и не принимаются. Многотабличные `UpdateJoin`/`DeleteJoin` и `Merge` не являются батч-шагами. У сырого SQL/DDL нет командной модели.
* `ToSql()` показывает `;`-склеенный текст без выполнения: на терминале `BatchQuery<TResult>` для единственного результата и на `BatchBuilder` для многорезультатной формы `AddQuery`/`Execute` (`BatchBuilder.ToSql()` рендерит весь батч и требует хотя бы одного результат-несущего шага). На `DbBatch`-провайдере при выполнении утверждения всё равно отправляются отдельными командами одного батча.
* Выполнение батча **не** проходит через интерцепторы запросов: их события несут `DbCommand`, тогда как команда `DbBatch` — это `DbBatchCommand`.
* Рантайм-плейсхолдеры `SqlFunctions.Parameter` нельзя использовать в батч-запросе; захватите значение в локальную переменную (как и в любом запросе, рендерящемся как источник). Захваченные переменные становятся параметрами автоматически.
* Планы батча не кэшируются: каждый вызов терминала рендерит батч заново.

## См. также

- [Материализация запроса в таблицу](18-create-table-as.md)
- [Транзакции](21-transactions.md)
- [Обзор провайдеров](../providers/overview.md)
