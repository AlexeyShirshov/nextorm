# Выполнение утверждений одним батчем

> Батч отправляет несколько SQL-операторов в базу **за одно обращение к базе данных в рамках одной серверной сессии**. Именно это делает session-scoped временную таблицу пригодной за пулером уровня соединения, держит мутацию и читающий её запрос на одном backend и убирает лишнее обращение к базе данных, когда материализация сразу же читается. Собрать его можно через [`BatchExtensions.CreateBatchBuilder`](xref:NextORM.Core.BatchExtensions).

**Предварительно:** [Материализация запроса в таблицу](20-create-table-as.md) · [Транзакции](23-transactions.md) · [Обзор провайдеров](../connecting-providers/overview.md)

## Зачем нужен батч

Отправка каждого SQL-оператора отдельной командой требует отдельного обращения к базе данных. При пулере уровня соединения, например PgBouncer в режиме `transaction`, следующий запрос может попасть и на другой backend. Если один SQL-оператор подготавливает данные для другого или следующий запрос должен увидеть мутацию, отдельные команды могут нарушить эту зависимость.

Открытое клиентское соединение само по себе не закрепляет серверную сессию. Это делает явная транзакция: PgBouncer закрепляет backend на весь период `BEGIN … COMMIT`, поэтому транзакция подходит, когда таблицу должны читать несколько запросов. Если нужно лишь выполнить SQL-операторы по порядку и прочитать результат, **батч** удерживает их в одной сессии за одно обращение к базе данных, не открывая транзакцию. SQL-операторы отправляются вместе, поэтому каждый видит результат предыдущего.

Выбирайте форму батча по ожидаемому результату:

| Задача | Используйте |
|---|---|
| Создать временную таблицу и прочитать её за один вызов | `CreateBatchBuilder()` с `CreateTempTable(...)` и `Query(...)` |
| Повторно читать декларативный источник временной таблицы из разных вызовов терминалов | `AsTempTable()` и чтение через `From(...)`; каждое чтение создаёт таблицу заново |
| Выполнить упорядоченные шаги материализации, DML или сырого SQL перед одним запросом | `CreateBatchBuilder()` с `Query(...)` в последнем шаге |
| Вернуть несколько наборов результатов | `AddQuery<TResult>(...)` и `Execute()` |

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
var orders = ctx.CreateBatchBuilder()
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
create temporary table recent_orders as select id, total
 from orders
 where (total > @b0_minTotal);
select id, total
 from recent_orders
```

`Query<TResult>` возвращает терминал `BatchQuery<TResult>`:

| Член | Действие |
|---|---|
| `ToList()` | Выполняет батч и возвращает строки результата. |
| `ToListAsync(cancellationToken = default)` | Асинхронный двойник `ToList`. |
| `ToAsyncEnumerable(cancellationToken = default)` | Стримит строки результата; reader остаётся открытым на весь батч, поэтому батч выполняется при старте перечисления и держит соединение до его завершения. |
| `ToSql()` | Рендерит батч (SQL-операторы, соединённые через `;`) без выполнения. |

## Ленивые временные таблицы

Ленивая временная таблица из [`AsTempTable`](20-create-table-as.md#ленивые-временные-таблицы-astemptable) декларативно задаёт такой батч. Построение источника ничего не выполняет; чтение через `From(...)` объединяет материализацию и чтение в один батч. Это удобно, если одна и та же материализация читается несколько раз или разными вызовами терминалов: перед каждым чтением таблица создаётся заново, поэтому пулер не сможет направить чтение на backend, где её нет.

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
create temporary table __nextorm_temp_xxxxxxxx as select id, total
 from orders
 where (total > @b0_minTotal);
select id, total
 from __nextorm_temp_xxxxxxxx
```

`ToBatchSql()` рендерит батч без выполнения. Каждый терминал (`ToList`, `First`, `Single`, `Count`, `Any`, `ToAsyncEnumerable`, …) выполняет его. Если ленивый источник читает другой ленивый источник, таблицы сначала материализуются в порядке зависимостей. Команда чтения рендерится при каждом выполнении, поэтому не попадает в plan cache (планы батчей не кэшируются), а захваченные переменные, как обычно, становятся параметрами.

`AsTempTable` доступен ровно там, где `CreateTempTable` — PostgreSQL, SQLite, MySQL и MariaDB; SQL Server (используйте `CreateTable` с именем с префиксом `#`), ClickHouse и in-memory-контекст отклоняют его при рендере запроса.

## Сборка батча напрямую

`ctx.CreateBatchBuilder()` возвращает `BatchBuilder` для нескольких SQL-операторов, DML- или сырого SQL-шага с побочным эффектом либо явного порядка.

Материализация + чтение:

```csharp
var rows = ctx.CreateBatchBuilder()
    .CreateTempTable("recent_orders", ctx.From<IOrder>().Where(x => x.Total > minTotal).Select(x => new { x.Id, x.Total }))
    .CreateTempTable("recent_ids", ctx.From("recent_orders").Select(t => new { Id = t.GetInt32("id") }))
    .Query(ctx.From("recent_ids").Select(t => new { Id = t.GetInt32("id") }))
    .ToList();
```

```sql
-- PostgreSQL
create temporary table recent_orders as select id, total
 from orders
 where (total > @b0_minTotal);
create temporary table recent_ids as select id
 from recent_orders;
select id
 from recent_ids
```

Замена постоянной таблицы — `CreateTable` с `DropExisting` сначала удаляет её, поэтому шаг можно выполнять повторно, а не падать на втором прогоне:

```csharp
var rows = ctx.CreateBatchBuilder()
    .CreateTable("order_archive", ctx.From<IOrder>().Select(x => new { x.Id, x.Total }),
        o => o.DropExisting())
    .Query(ctx.From("order_archive").Select(t => new { Id = t.GetInt32("id") }))
    .ToList();
```

```sql
-- PostgreSQL
drop table if exists order_archive;
create table order_archive as select id, total
 from orders;
select id
 from order_archive
```

`DropExisting` допустим только для постоянного `CreateTable` (у `CreateTempTable` таблица живёт в сессии, и опция отклоняется) и несовместим с `IfNotExists`.

Мутация + чтение — запрос видит обновление на той же сессии:

```csharp
var updated = ctx.CreateBatchBuilder()
    .Update(ctx.CreateUpdateBuilder<IOrder>().Set(x => x.Status, "shipped").Where(x => x.Id == id))
    .Query(ctx.From<IOrder>().Where(x => x.Id == id).Select(x => new { x.Id, x.Status }))
    .ToList();
```

```sql
-- PostgreSQL
update orders set status = @p0 where (id = @b0_id);
select id, status
 from orders
 where (id = @b1_id)
```

* `CreateTempTable(name, source, options?)` и `CreateTable(name, source, options?)` добавляют материализации, по порядку, сколько угодно. Опции можно передать как `CreateTableOptions` или как callback `CreateTableOptionsBuilder` (`o => o.DropExisting()`); постоянный `CreateTable` с `DropExisting` добавляет перед шагом `DROP TABLE IF EXISTS`, и шаг заменяет существующую таблицу.
* `Insert(insert)`, `Update(update)`, `Delete(delete)` и `Truncate(truncate)` добавляют DML-оператор с побочным эффектом; таких шагов можно добавить сколько угодно, в нужном порядке. Они принимают те же билдеры, что и `ctx.CreateInsertBuilder<T>()`, `ctx.CreateUpdateBuilder<T>()`, `ctx.CreateDeleteBuilder<T>()`, `ctx.CreateTruncateBuilder<T>()`; терминал билдера (`Insert()`, `Update()`, …) не вызывается — оператор выполняет батч.
* `OutputIntoTableVariable(mutation, "@t", definitions)` добавляет **шаг захвата**: одна инструкция, которая объявляет табличную переменную и пишет в неё возвращённые строки мутации (`DECLARE @t TABLE (...); <DML> OUTPUT ... INTO @t (...)`). В отличие от fluent `Returning(...).OutputIntoTableVariable(...)` на самой мутации, эта форма не добавляет автоматического чтения и не создаёт собственного набора результатов: захваченные строки читают запросы самого батча через `FromOutput<TResult>`/`FromOutputTable`, поэтому их можно отфильтровать, соединить или агрегировать до того, как батч их вернёт. Только SQL Server; неизвестное имя или имя, захваченное в батче дважды, отклоняется при сборке батча.
* `Raw(sql)` добавляет сырой SQL-шаг с побочным эффектом, по порядку, сколько угодно: текст отправляется как есть, параметры не привязываются. Он подчиняется тому же правилу порядка, что и остальные шаги с побочным эффектом, — добавляется до `Query`/`AddQuery` (см. [Сырые шаги](#сырые-шаги)).
* `Query<TResult>(query)` добавляет единственный результат-несущий запрос и возвращает терминал; он должен быть последним. Второй `Query` или любой SQL-оператор после `Query` бросают `InvalidOperationException`. Чтобы пронести несколько наборов результатов, используйте `AddQuery<TResult>` и `Execute`/`ExecuteAsync` (см. [Несколько наборов результатов](#несколько-наборов-результатов)).
* Каждый SQL-оператор должен быть построен на контексте самого батча; оператор, привязанный к другому `IDataContext`, отклоняется с `ArgumentException`.

Параметры нумеруются по всему батчу, а захваченная переменная, используемая более чем одним SQL-оператором, получает отдельный префикс для каждого оператора (`b0_`, `b1_`, …), поэтому имена placeholders не конфликтуют — в том числе в форме с операторами, соединёнными через `;`. Захваченная переменная, использованная в одном операторе дважды, даёт одну запись параметра.

Шаг захвата вместе с составным запросом — батчевый способ превратить `OUTPUT` мутации в запрос за то же обращение к базе данных (только SQL Server — см. [Захват в рамках батча](17-insert-statement.md#захват-в-рамках-батча-batchbuilderoutputintotablevariable)):

```csharp
public sealed class CapturedOrder
{
    public long Id { get; set; }
    public string? Status { get; set; }
}

var batch = ctx.CreateBatchBuilder();
batch.OutputIntoTableVariable(
    ctx.CreateUpdateBuilder<IOrder>()
        .Set(x => x.Status, "shipped")
        .Where(x => x.Id == id)
        .Returning(x => new CapturedOrder { Id = x.Id, Status = x.Status }),
    "@t",
    "id bigint, status nvarchar(50)");

var shipped = batch
    .Query(batch.FromOutput<CapturedOrder>("@t")
        .OrderBy(r => r.Id)
        .Select(r => new CapturedOrder { Id = r.Id, Status = r.Status }))
    .ToList();
```

```sql
-- SQL Server
declare @t table (id bigint, status nvarchar(50)); update orders set status = @p0 output inserted.id, inserted.status into @t (id, status) where (id = @b0_id); select id, status
 from @t as [t1]
 order by t1.id
```

Захват не добавляет автоматического чтения и не создаёт набора результатов: объявление едет вместе с мутацией, а составной запрос — обычный шаг-запрос батча, поэтому вся операция по-прежнему требует одного обращения к базе данных.

Блоки SQL выше — рендер PostgreSQL. По умолчанию `ToSql()` печатает SQL-операторы одной строкой через `; `; чтобы получить разбивку по строкам, как в примерах, включите `DataContextBuilder.UseMultilineBatchSql()`. `UseMultilineBatchSql()` управляет только **разделением между операторами** — это не переключатель раскладки предложений: перевод строки перед `FROM`/`JOIN` внутри каждого SQL-оператора писатель SQL выдаёт безусловно (см. [Ограничения](../advanced/limitations.md)). На SQL Server та же связка выглядит иначе: `CREATE TEMPORARY TABLE ... AS SELECT` там нет, `CreateTempTable` бросает `NotSupportedException`, поэтому временная цель задаётся `CreateTable` с именем, начинающимся с `#`:

```sql
-- SQL Server
select id, total into #recent_orders
 from orders
 where (total > @b0_minTotal);
select id, total
 from #recent_orders
```

## Сырые шаги

`Raw(sql)` добавляет **сырой SQL-шаг с побочным эффектом**: строка отправляется ровно как задана, placeholders **не** переписываются, параметры не привязываются — это путь для DDL или специфичных для провайдера SQL-операторов, которые не моделируют типизированные билдеры. Он добавляется до результат-несущего `Query`/`AddQuery`, как и любой другой шаг с побочным эффектом: `Raw` после `Query` или после первого `AddQuery` бросает `InvalidOperationException`, а батч всё равно обязан завершаться хотя бы одним результат-несущим запросом — батч из одних `Raw` отклоняется. Текст выполняется как есть и не привязывает параметров, поэтому передавайте только доверенный SQL — никогда не склеивайте `Raw`-строку с недоверенным пользовательским вводом.

`Raw` участвует в `ToSql()`, а в многорезультатной форме `AddQuery`/`Execute` является одним из шагов с побочным эффектом, идущих перед запросами. SQL Server может прочитать результат хранимой процедуры через временную таблицу одним батчем, без транзакции:

```csharp
var rows = ctx.CreateBatchBuilder()
    .Raw("create table #r (id int, total decimal(18,2))")
    .Raw("insert into #r (id, total) exec dbo.MyProc @p = 42")
    .Query(ctx.From("#r").Select(t => new { Id = t.GetInt32("id") }))
    .ToList();
```

`INSERT … EXEC` не вкладывается (процедура, которая сама выполняет `INSERT … EXEC`, падает), и процедура должна вернуть **единственный** результат-набор, колонки которого совпадают с `#r` — текст передаётся как есть, поэтому NextORM не может проверить ни то, ни другое. `Raw` не привязывает параметров: `@p = 42` выше — доверенный литерал в тексте T-SQL, а не связанный параметр; значение, которое должно меняться, вызывающий обязан подставить сам.

## Несколько наборов результатов

Батч может нести **больше одного** результат-несущего запроса. Добавляйте каждый через `AddQuery<TResult>` вместо терминала `Query<TResult>` для единственного результата, затем выполните весь батч один раз через `Execute()`/`ExecuteAsync(...)` и прочитайте наборы в порядке добавления:

```csharp
record OrderTotal(int Id, decimal Total);
record OrderLine(int Id, string Status);

var result = ctx.CreateBatchBuilder()
    .AddQuery(ctx.From<IOrder>().Where(x => x.Status == "open")
        .Select(x => new OrderTotal(x.Id, x.Total)))
    .AddQuery(ctx.From<IOrder>().Where(x => x.Status == "shipped")
        .Select(x => new OrderLine(x.Id, x.Status)))
    .Execute();

foreach (var set in result)
{
    if (set.Index == 0)
    {
        IReadOnlyList<OrderTotal> open = set.Read<OrderTotal>();
    }
    else
    {
        IReadOnlyList<OrderLine> shipped = set.Read<OrderLine>();
    }
}
```

Оба запроса уходят в базу **одним** батчем — за одно обращение к базе данных и в рамках одной серверной сессии:

```sql
-- PostgreSQL
select id, total
 from orders
 where (status = @b0_status);
select id, status
 from orders
 where (status = @b1_status)
```

`AddQuery` возвращает билдер, поэтому вызовы можно связывать в цепочку. Каждый результат-несущий запрос должен идти после SQL-операторов с побочным эффектом: материализация или DML-шаг, добавленный после первого `AddQuery`, бросает `InvalidOperationException`. `AddQuery` и `Query` взаимно исключают друг друга: `Query` завершает батч, поэтому последующий `AddQuery` бросает исключение, и `Query` после `AddQuery` — тоже. `Execute()`/`ExecuteAsync()` без добавленного результат-несущего запроса бросает `InvalidOperationException`.

### Чтение наборов результатов

`Execute()` возвращает [`BatchResult`](xref:NextORM.Core.BatchResult). Перечислите его через `foreach (var set in result)` или `await foreach (var set in result)`, чтобы получить курсор [`ResultSet`](xref:NextORM.Core.ResultSet) для каждого запроса, в порядке добавления. Каждый курсор предоставляет `Index`, `FieldCount` и `ColumnNames` и читает **свой** набор через `Read<T>()` или `ReadAsync<T>(ct)`.

Также наборы можно читать по позиции через `BatchResult.Read<TResult>()`:

| Член | Действие |
|---|---|
| `ResultSetCount` | Число наборов результатов, которые произвёл батч. |
| `RecordsAffected` | Итог драйвера по числу затронутых строк для всего батча, либо `-1`, когда у батча нет метаданных о затронутых строках. |
| `Read<TResult>()` | Возвращает следующий набор позиционно, в порядке добавления запросов; каждый набор читается один раз. |

`Read<TResult>()` типизирован: запрошенный тип должен совпадать с типом проекции следующего запроса. При несовпадении бросается `InvalidOperationException` **без расхода набора**; чтение после последнего набора вызывает то же исключение.

Выберите перечисление или чтение по позиции: смешивать их нельзя, иначе будет `InvalidOperationException`. `BatchResult` **нетерпеливый** (eager): он буферизует все наборы до возврата `Execute()` и не владеет ни reader'ом, ни соединением, поэтому не освобождается и не стримит. Это единственное отличие от потокового `ProcedureResult`; курсоры и правила одноразового обхода совпадают. Полный контракт курсоров см. в разделе [Несколько наборов результатов](14-raw-sql.md#несколько-наборов-результатов).

### Как интерпретировать `RecordsAffected`

`RecordsAffected` — **единый итог драйвера для всего батча**: он включает строки, затронутые всеми DML-инструкциями, независимо от того, отправил провайдер их через нативный `DbBatch` или одной командой с разделителем `;`. Это не `ResultSetCount`, не число материализованных строк и не сумма, вычисляемая nextorm; разбивки по инструкциям нет.

Батч выполняется целиком до возврата `Execute()` или `ExecuteAsync()`, поэтому значение уже зафиксировано. Чтение свойства не обращается к базе и безопасно в любой момент. Как и у [`ProcedureResult`](14-raw-sql.md#затронутые-строки-recordsaffected), `-1` означает неизвестный итог или отсутствие метаданных (например, когда все инструкции — запросы), а `0` — реальный итог DML, не изменившей строк. ClickHouse отклоняет батчи целиком (см. [Поддержка провайдерами](#поддержка-провайдерами)).

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

* Батч завершается одним или несколькими результат-несущими запросами — одним, добавленным терминалом `Query<TResult>`, либо несколькими, добавленными `AddQuery<TResult>` и выполненными через `Execute`/`ExecuteAsync`, — и они последние. SQL-операторы перед ними имеют побочный эффект: материализации, DML (`INSERT`/`UPDATE`/`DELETE`/`TRUNCATE`) и сырые SQL-шаги, не возвращающие колонок.
* Многорезультатное выполнение **последовательное и нетерпеливое**: `BatchResult` выдаёт свои наборы в порядке добавления запросов (через перечисление или `Read<TResult>()`), каждый набор читается один раз, а все наборы полностью буферизуются в памяти до возврата `Execute()`/`ExecuteAsync()` — стриминга и произвольного доступа нет. Стримит только терминал `BatchQuery<TResult>` с единственным результатом, через `ToAsyncEnumerable`.
* Мутация, добавленная в батч, не может **возвращать** строки клиенту: терминалы `Returning()`/`ReturningIdentity()` дают другой тип билдера и не принимаются `Insert`/`Update`/`Delete`, которые берут только не-возвращающие билдеры. Возвращающий билдер принимает только батчевый захват `OutputIntoTableVariable(...)`, который пишет свои строки в табличную переменную для запросов самого батча вместо возврата клиенту. Многотабличные `CreateUpdateJoinBuilder`/`DeleteJoin` и обычный терминал `Merge` не являются батч-шагами; `MERGE` входит в батч через тот же захват. Сырой SQL/DDL выражается вербатим-шагом `Raw(sql)` — см. [Сырые шаги](#сырые-шаги).
* `ToSql()` показывает текст с SQL-операторами, соединёнными через `;`, без выполнения: на терминале `BatchQuery<TResult>` для единственного результата и на `BatchBuilder` для многорезультатной формы `AddQuery`/`Execute` (`BatchBuilder.ToSql()` рендерит весь батч и требует хотя бы одного результат-несущего шага). На провайдере с `DbBatch` при выполнении SQL-операторы всё равно отправляются отдельными командами одного батча.
* Выполнение батча **не** проходит через интерцепторы запросов: их события несут `DbCommand`, тогда как команда `DbBatch` — это `DbBatchCommand`.
* Рантайм-плейсхолдеры `SqlFunctions.Parameter` нельзя использовать в батч-запросе; захватите значение в локальную переменную (как и в любом запросе, рендерящемся как источник). Захваченные переменные становятся параметрами автоматически.
* Планы батча не кэшируются: каждый вызов терминала рендерит батч заново.
* Батч использует одну сессию и одно обращение к базе данных, поэтому выполняется на **одном хосте**: типы сущностей всех его шагов должны сходиться в одну топологию и один вариант сопоставления. Хост выбирает первый маршрутизированный шаг, шаг без правила следует за ним, а шаг, которому нужна другая топология или другой вариант, отклоняется при построении батча — см. [«Один батч — одна топология»](34-routing-data-to-topologies.md).

## См. также

- [Материализация запроса в таблицу](20-create-table-as.md)
- [Транзакции](23-transactions.md)
- [Обзор провайдеров](../connecting-providers/overview.md)
