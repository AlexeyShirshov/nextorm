# Массовая вставка (bulk)

`CreateBulkInsertBuilder<TEntity>()` записывает весь набор одним явным вызовом, без change tracking. Где у
провайдера есть нативный bulk API, он и используется — бинарный `COPY` в PostgreSQL и `SqlBulkCopy` в
SQL Server; иначе набор пишется параметризованным `INSERT ... VALUES`, при необходимости — чанками.

Массовая вставка работает поверх обычного маппинга; объявление ключа, identity и computed-колонок — в
[Изменении данных (INSERT)](15-insert-statement.md).

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;

[SqlTable("orders")]
public interface IOrder
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    int Id { get; set; }
    [Column("customer_id")]
    int CustomerId { get; set; }
    [Column("total")]
    decimal Total { get; set; }
}

public sealed class Order : IOrder
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public decimal Total { get; set; }
}
```

## Запись набора

Identity- и computed-колонки исключаются автоматически, поэтому указываются только записываемые.

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

var orders = new[]
{
    new Order { CustomerId = 1, Total = 10m },
    new Order { CustomerId = 2, Total = 20m },
    new Order { CustomerId = 3, Total = 30m },
};

// нативный COPY в PostgreSQL; портируемый INSERT ... VALUES на остальных провайдерах
var written = ctx.CreateBulkInsertBuilder<IOrder>().Values(orders).BulkInsert();
// written == 3
```

Источник `IAsyncEnumerable<TEntity>` тоже принимается и стримится — см.
[Источники-стримы](#источники-стримы). Пустой набор ничего не пишет и возвращает `0`.

Глобальные фильтры запросов не добавляются в bulk-запись; строки проверяются на соответствие активным
фильтрам цели до записи (`QueryFilterException` при нарушении). Синхронный источник проверяется целиком
до первого батча; асинхронный — по мере потока, поэтому при нарушении в поздней строке ранее записанные
строки остаются. См. [INSERT и MERGE (проверка)](../advanced/query-filters.md#insert-и-merge-проверка).

## Источники-стримы

`Values` принимает две формы источника, и стримится только одна из них:

| Источник | Чтение | Терминал | Буферизуется до первой записи |
|---|---|---|---|
| `IEnumerable<TEntity>` | один раз, синхронно | `BulkInsert()` или `BulkInsertAsync()` | да — набор материализуется и проверяется заранее |
| `IAsyncEnumerable<TEntity>` | один раз, асинхронно | только `BulkInsertAsync()` | нет — строки вычитываются по мере записи |

Источник стримится, только если передан `IAsyncEnumerable<TEntity>` **и** вызов завершается
`BulkInsertAsync()`:

```csharp
// любой IAsyncEnumerable<IOrder> — Channel-ридер, асинхронный цикл постраничного чтения, EF AsAsyncEnumerable(), ...
IAsyncEnumerable<IOrder> ordersStream = ...;

await ctx.CreateBulkInsertBuilder<IOrder>()
    .Values(ordersStream)
    .BulkInsertAsync(cancellationToken);
```

Источник-стрим — это всегда последовательность отображённых сущностей: перегрузок под `DbDataReader`,
байтовый `Stream` или сырые массивы значений нет, поэтому проецируйте прочитанное в `TEntity` и
отдавайте поток. Синхронный источник (массив, `List<T>`, LINQ-запрос) **буферизуется**:
`BulkInsert()`/`BulkInsertAsync()` материализуют весь набор и прогоняют
[проверку глобальными фильтрами](../advanced/query-filters.md#insert-и-merge-проверка) по каждой строке
до отправки первой команды, поэтому набор должен помещаться в память. Передача асинхронного источника в
синхронный `BulkInsert()` — как и в `ToSql()` — бросает `InvalidOperationException`; используйте
`BulkInsertAsync()`.

Насколько глубоко стрим доходит до базы, зависит от пути:

| Путь | Стримит построчно | Буфер |
|---|---|---|
| PostgreSQL нативный `COPY` | да — каждая строка пишется по мере чтения, набор не буферизуется | нет; лимиты чанка игнорируются |
| SQL Server `SqlBulkCopy` | нет — набор сначала буферизуется в `DataTable`, так как `SqlBulkCopy` нужны метаданные типов колонок | весь набор; лимит чанка его не режет |
| Портативный `INSERT ... VALUES` | по батчам — строки набираются в буфер до лимита чанка или до конца, затем батч пишется, а буфер переиспользуется | задаётся `MaxBatchSize`/`MaxParameters`/`MaxSqlLength` |

Буфер настраивает только портативный путь — см.
[Ограничение батча и прогресс](#ограничение-батча-и-прогресс) про три лимита, их значения по умолчанию и
колбэк прогресса.

`Returning*` принимает оба источника: читайте `ToList()` (синхронный источник) или `ToListAsync()`
(асинхронный). На стриминговом пути сущность проверяется по мере проекции, поэтому поздняя строка,
нарушившая глобальный фильтр, может оставить ранее записанные батчи уже закоммиченными — синхронный
источник сначала проверяется целиком. nextorm вычитывает источник ровно один раз и никогда его не
освобождает. См. [INSERT и MERGE (проверка)](../advanced/query-filters.md#insert-и-merge-проверка).

## Перенацеливание целевой таблицы

`Table(name)` / `Table(schema, name)` переопределяет таблицу, замапленную на сущность, для одной
записи — так один замапленный набор можно скопировать в таблицу с другим именем без второго
`[SqlTable]`-маппинга:

```csharp
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o.Table("archive", "orders_2024"))
    .Values(orders)
    .BulkInsert();
```

Override имеет приоритет над маппингом `[SqlTable]`/`Table(...)`, naming convention к нему не
применяется, а необязательная схема квотируется отдельно от таблицы. Работает и на нативном пути
(`COPY`/`SqlBulkCopy`), и на портируемом `INSERT ... VALUES`, и сочетается с `Returning*` и
`KeepIdentity`.

## Получение сгенерированных ключей

Нативные bulk API не возвращают строки, поэтому `Returning*` переключает на портируемый путь и пишет
набор как серию возвращающих `INSERT ... VALUES ... RETURNING`/`OUTPUT`. Для ключевой колонки —
`ReturningKey<TKey>()`, для произвольной проекции — `Returning(projection)`; терминалы —
`ToList()` / `ToListAsync()`.

```csharp
// только ключи — ReturningKey<TKey>() читает ключевую колонку из метаданных
IReadOnlyList<int> ids = ctx.CreateBulkInsertBuilder<IOrder>()
    .Values(orders)
    .ReturningKey<int>()
    .ToList();
// ids = [1, 2, 3]

// проекция на каждую записанную строку
var rows = ctx.CreateBulkInsertBuilder<IOrder>()
    .Values(orders)
    .Returning(o => new { o.Id, o.CustomerId })
    .ToList();
// rows = [ { Id = 1, CustomerId = 1 }, { Id = 2, CustomerId = 2 }, ... ]

// проекция одного скаляра
IReadOnlyList<int> customerIds = ctx.CreateBulkInsertBuilder<IOrder>()
    .Values(orders)
    .Returning(o => o.CustomerId)
    .ToList();

// асинхронно
IReadOnlyList<int> keys = await ctx.CreateBulkInsertBuilder<IOrder>()
    .Values(ordersStream)
    .ReturningKey<int>()
    .ToListAsync(cancellationToken);
```

> **Порядок результата не гарантирован** относительно порядка источника (ни `RETURNING`, ни `OUTPUT`
> его не обещают). Связывайте строки по бизнес-ключу, а не по позиции:

```csharp
var idByCustomer = ctx.CreateBulkInsertBuilder<IOrder>()
    .Values(orders)
    .Returning(o => new { o.CustomerId, o.Id })
    .ToList()
    .ToDictionary(o => o.CustomerId, o => o.Id);

var orderIdForCustomer2 = idByCustomer[2];
```

`Returning*` требует провайдер с `RETURNING`/`OUTPUT`: PostgreSQL и SQLite 3.35+ (через `RETURNING`)
и SQL Server (через `OUTPUT`). MySQL, MariaDB и ClickHouse отклоняют его явным
`NotSupportedException` — там ключи читаются обычным `CreateInsertBuilder`.

## Ограничение батча и прогресс

Портируемый путь по умолчанию отправляет весь набор одним утверждением. Лимиты чанка и прогресс — это
опции записи, передаваемые в `CreateBulkInsertBuilder<TEntity>`: либо записью `BulkInsertOptions`, либо через
fluent-колбэк `BulkInsertOptionsBuilder`:

```csharp
var written = 0;

await ctx.CreateBulkInsertBuilder<IOrder>(o => o
        .MaxBatchSize(1_000)                                 // строк на утверждение
        .MaxParameters(20_000)                               // параметров на утверждение
        .NotifyAfter(10_000, (total, _) => written = total)) // вызывается с накопленным числом
    .Values(ordersStream)
    .BulkInsertAsync(cancellationToken);
```

Чанки пишутся отдельными `INSERT`; nextorm **не** оборачивает их в транзакцию — если нужна
атомарность, начните транзакцию сами.

Колбэк `NotifyAfter` выполняется inline — после зафиксированного чанка на портируемом пути или на
нативном тике прогресса провайдера (`SqlBulkCopy.NotifyAfter` в SQL Server, цикл записи в
PostgreSQL). Держите его быстрым и не бросающим: исключение из колбэка пробрасывается наружу, а уже
записанные строки остаются закоммиченными (nextorm транзакцию не открывает); блокирующийся колбэк
останавливает вызов до своего возврата.

Его второй аргумент — `CancellationToken`. Передайте источник через `ProgressCancellationTokenSource`,
чтобы кооперативный колбэк мог остановиться досрочно — writer бросит `OperationCanceledException`,
как только текущий колбэк вернётся:

```csharp
using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(30));

await ctx.CreateBulkInsertBuilder<IOrder>(o => o
        .ProgressCancellationTokenSource(budget)
        .NotifyAfter(10_000, (total, token) => Report(total, token)))
    .Values(ordersStream)
    .BulkInsertAsync(cancellationToken);
```

> Токен не может прервать колбэк, который его игнорирует (колбэк выполняется inline в потоке записи);
> он лишь позволяет колбэку, который его соблюдает, остановиться досрочно. nextorm никогда не отменяет
> и не освобождает источник.

## Явные значения identity и пропуск дублей

`KeepIdentity` записывает значения identity из сущностей (`OVERRIDING SYSTEM VALUE` в PostgreSQL,
`SET IDENTITY_INSERT ... ON/OFF` в SQL Server). `IgnoreDuplicates` пропускает строки, нарушающие
уникальность, вместо падения всей записи.

```csharp
// явные id
var pinned = new[]
{
    new Order { Id = 9001, CustomerId = 1, Total = 5m },
    new Order { Id = 9002, CustomerId = 2, Total = 6m },
};
ctx.CreateBulkInsertBuilder<IOrder>(o => o.KeepIdentity()).Values(pinned).BulkInsert();

// батч, повторяющий 9001: дубликат пропускается, остальные строки пишутся
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o.KeepIdentity().IgnoreDuplicates())
    .Values([
        new Order { Id = 9001, CustomerId = 1, Total = 5m },   // конфликт -> пропуск
        new Order { Id = 9003, CustomerId = 3, Total = 7m },   // записана
    ])
    .BulkInsert();
// written == 1
```

`IgnoreDuplicates` доступен в PostgreSQL (`ON CONFLICT DO NOTHING`), SQLite (`INSERT OR IGNORE`) и
MySQL/MariaDB (`INSERT IGNORE`); на SQL Server он отклоняется, а в ClickHouse это no-op (уникальности
нет, поэтому пишутся все строки).

> `KeepIdentity()` игнорируется, если в маппинге сущности нет identity-колонки (как в linq2db):
> явные значения для не-identity ключей всё равно пишутся, но форма включения identity
> (`SET IDENTITY_INSERT`, `OVERRIDING SYSTEM VALUE`) не эмитится — поэтому таблица без identity
> никогда не падает с ошибкой SQL Server 8106. Проверка идёт по метаданным сущности-донора, а не по
> фактической целевой таблице: при перенацеливании через `Table(...)` ответственность за identity-форму
> целевой таблицы лежит на вызывающем.

## Опции bulk copy в SQL Server

Нативный путь `SqlBulkCopy` даёт четыре флага, которые не выразимы ни в `COPY` PostgreSQL, ни в
портируемом `INSERT ... VALUES`. По умолчанию они выключены — как `SqlBulkCopyOptions.Default`:

```csharp
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o
        .TableLock()          // SqlBulkCopyOptions.TableLock
        .CheckConstraints()   // SqlBulkCopyOptions.CheckConstraints
        .KeepNulls()          // SqlBulkCopyOptions.KeepNulls
        .FireTriggers())      // SqlBulkCopyOptions.FireTriggers
    .Values(orders)
    .BulkInsert();
```

| Опция | Метод builder | Действие |
|---|---|---|
| `CheckConstraints` | `CheckConstraints()` | проверять ограничения CHECK / FOREIGN KEY при копировании (по умолчанию SQL Server их игнорирует) |
| `TableLock` | `TableLock()` | взять табличную bulk-блокировку на время копирования |
| `KeepNulls` | `KeepNulls()` | писать явные `NULL` вместо `DEFAULT` целевой колонки |
| `FireTriggers` | `FireTriggers()` | запускать `INSERT`-триггеры для копируемых строк |

`null` и `false` равнозначны: флаг не выставляется, поведение записи не меняется. Запрос флага на пути,
который его не выражает, бросает `NotSupportedException` с перечислением флагов (а не игнорирует их
молча) — это касается `COPY` PostgreSQL, портируемого `INSERT ... VALUES` в MySQL/MariaDB/SQLite/ClickHouse
и самого SQL Server, когда `Returning*` или `KeepIdentity` переключает запись на портируемый путь
(поэтому `KeepIdentity` и bulk-copy флаги не комбинируются).

## Просмотр SQL

`ToSql()` рендерит параметризованный SQL, который выполнил бы портируемый путь для первого батча, не
открывая соединение:

```csharp
var sql = ctx.CreateBulkInsertBuilder<IOrder>().Values(orders).ToSql();
// insert into orders (customer_id, total) values (@p0, @p1), (@p2, @p3), (@p4, @p5)

var returningSql = ctx.CreateBulkInsertBuilder<IOrder>().Values(orders).ReturningKey<int>().ToSql();
// insert into orders (customer_id, total) values (@p0, @p1), ... returning id
```

## Опции

Опции записи передаются в `CreateBulkInsertBuilder<TEntity>` — записью `BulkInsertOptions` или, короче, через
колбэк `BulkInsertOptionsBuilder`:

```csharp
ctx.CreateBulkInsertBuilder<IOrder>(new BulkInsertOptions { MaxBatchSize = 1_000, IgnoreDuplicates = true });
ctx.CreateBulkInsertBuilder<IOrder>(o => o.MaxBatchSize(1_000).IgnoreDuplicates());
```

| Опция | Метод builder | Действие |
|---|---|---|
| `MaxBatchSize` | `MaxBatchSize(rows)` | дробление портируемого пути (по умолчанию — одно утверждение) |
| `MaxParameters` | `MaxParameters(count)` | параметров на утверждение |
| `MaxSqlLength` | `MaxSqlLength(chars)` | примерная длина SQL на утверждение |
| `TableName` / `TableSchema` | `Table(name)` / `Table(schema, name)` | переопределение целевой таблицы, при желании schema-квалифицированной |
| `KeepIdentity` | `KeepIdentity()` | запись явных значений identity (`OVERRIDING SYSTEM VALUE` в PostgreSQL, `SET IDENTITY_INSERT ... ON/OFF` в SQL Server); игнорируется, если у сущности нет identity-колонки |
| `IgnoreDuplicates` | `IgnoreDuplicates()` | пропуск строк, нарушающих уникальность |
| `CheckConstraints` | `CheckConstraints()` | проверять ограничения CHECK/FOREIGN KEY; только нативный путь SQL Server |
| `TableLock` | `TableLock()` | табличная bulk-блокировка; только нативный путь SQL Server |
| `KeepNulls` | `KeepNulls()` | писать явные `NULL` вместо `DEFAULT`; только нативный путь SQL Server |
| `FireTriggers` | `FireTriggers()` | запускать `INSERT`-триггеры; только нативный путь SQL Server |
| `TimeoutSeconds` | `Timeout(seconds)` | таймаут команды; только нативный путь SQL Server |
| `Progress` / `NotifyEvery` | `NotifyAfter(rows, onRows)` | прогресс с накопленным числом записанных строк |
| `ProgressCancellationTokenSource` | `ProgressCancellationTokenSource(source)` | токен, передаваемый в колбэк прогресса |

На возвращаемом builder:

| Метод | Действие |
|---|---|
| `Values(IEnumerable<TEntity>)` / `Values(IAsyncEnumerable<TEntity>)` | набор, читается один раз; пустой набор ничего не пишет и возвращает `0` |
| `ReturningKey<TKey>()` / `Returning(projection)` | материализация ключей/проекции записанных строк через `RETURNING`/`OUTPUT` |
| `BulkInsert()` / `BulkInsertAsync(ct)` | записать набор и вернуть число записанных строк |
| `ToSql()` | отрендерить SQL первого батча без выполнения |

## Поддержка провайдеров

| Провайдер | Нативный bulk | Override цели | `Returning` | `IgnoreDuplicates` | `KeepIdentity` | Bulk-copy флаги |
|---|---|---|---|---|---|---|
| PostgreSQL | бинарный `COPY` | `schema.table` | `RETURNING` (портируемо) | `ON CONFLICT DO NOTHING` | `OVERRIDING SYSTEM VALUE` | — (отклоняются) |
| SQL Server | `SqlBulkCopy` | `schema.table` | `OUTPUT` (портируемо) | — (отклоняется) | `SET IDENTITY_INSERT` | `CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers` |
| MySQL | — (портируемо) | `db.table` | — | `INSERT IGNORE` | явные значения | — (отклоняются) |
| MariaDB | — (портируемо) | `db.table` | — | `INSERT IGNORE` | явные значения | — (отклоняются) |
| SQLite | — (портируемо) | `schema.table` | `RETURNING` (портируемо) | `INSERT OR IGNORE` | явные значения | — (отклоняются) |
| ClickHouse | — (портируемо) | `db.table` | — | no-op (нет уникальности) | — | — (отклоняются) |
| In-memory | — | — | — | — | `NotSupportedException` (только чтение) | — (только чтение) |

## См. также

- [Изменение данных (INSERT)](15-insert-statement.md)
- [Слияние данных (MERGE / upsert)](19-merge-statement.md)
- [Глобальные фильтры запросов](../advanced/query-filters.md)
- [Ограничения и что вне области](../advanced/limitations.md)
- [Обзор провайдеров](../providers/overview.md)
