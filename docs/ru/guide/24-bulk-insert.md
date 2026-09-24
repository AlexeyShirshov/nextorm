# Массовая вставка (bulk)

`BulkInsertInto<TEntity>()` записывает весь набор одним явным вызовом, без change tracking. Где у
провайдера есть нативный bulk API, он и используется — бинарный `COPY` в PostgreSQL и `SqlBulkCopy` в
SQL Server; иначе набор пишется параметризованным `INSERT ... VALUES`, при необходимости — чанками.

Массовая вставка работает поверх обычного маппинга; объявление ключа, identity и computed-колонок — в
[Изменении данных (INSERT)](19-insert-statement.md).

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
var written = ctx.BulkInsertInto<IOrder>().Values(orders).BulkInsert();
// written == 3
```

Источник `IAsyncEnumerable<TEntity>` тоже принимается, и есть асинхронный терминал:

```csharp
await ctx.BulkInsertInto<IOrder>().Values(ordersStream).BulkInsertAsync(cancellationToken);
```

Пустой набор ничего не пишет и возвращает `0`.

## Получение сгенерированных ключей

Нативные bulk API не возвращают строки, поэтому `Returning*` переключает на портируемый путь и пишет
набор как серию возвращающих `INSERT ... VALUES ... RETURNING`/`OUTPUT`. Для ключевой колонки —
`ReturningKey<TKey>()`, для произвольной проекции — `Returning(projection)`; терминалы —
`ToList()` / `ToListAsync()`.

```csharp
// только ключи — ReturningKey<TKey>() читает ключевую колонку из метаданных
IReadOnlyList<int> ids = ctx.BulkInsertInto<IOrder>()
    .Values(orders)
    .ReturningKey<int>()
    .ToList();
// ids = [1, 2, 3]

// проекция на каждую записанную строку
var rows = ctx.BulkInsertInto<IOrder>()
    .Values(orders)
    .Returning(o => new { o.Id, o.CustomerId })
    .ToList();
// rows = [ { Id = 1, CustomerId = 1 }, { Id = 2, CustomerId = 2 }, ... ]

// проекция одного скаляра
IReadOnlyList<int> customerIds = ctx.BulkInsertInto<IOrder>()
    .Values(orders)
    .Returning(o => o.CustomerId)
    .ToList();

// асинхронно
IReadOnlyList<int> keys = await ctx.BulkInsertInto<IOrder>()
    .Values(ordersStream)
    .ReturningKey<int>()
    .ToListAsync(cancellationToken);
```

> **Порядок результата не гарантирован** относительно порядка источника (ни `RETURNING`, ни `OUTPUT`
> его не обещают). Связывайте строки по бизнес-ключу, а не по позиции:

```csharp
var idByCustomer = ctx.BulkInsertInto<IOrder>()
    .Values(orders)
    .Returning(o => new { o.CustomerId, o.Id })
    .ToList()
    .ToDictionary(o => o.CustomerId, o => o.Id);

var orderIdForCustomer2 = idByCustomer[2];
```

`Returning*` требует провайдер с `RETURNING`/`OUTPUT`: PostgreSQL и SQLite 3.35+ (через `RETURNING`)
и SQL Server (через `OUTPUT`). MySQL, MariaDB и ClickHouse отклоняют его явным
`NotSupportedException` — там ключи читаются обычным `InsertInto`.

## Ограничение батча и прогресс

Портируемый путь по умолчанию отправляет весь набор одним утверждением. Лимиты чанка и прогресс — это
опции записи, передаваемые в `BulkInsertInto<TEntity>`: либо записью `BulkInsertOptions`, либо через
fluent-колбэк `BulkInsertOptionsBuilder`:

```csharp
var written = 0;

await ctx.BulkInsertInto<IOrder>(o => o
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

await ctx.BulkInsertInto<IOrder>(o => o
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
ctx.BulkInsertInto<IOrder>(o => o.KeepIdentity()).Values(pinned).BulkInsert();

// батч, повторяющий 9001: дубликат пропускается, остальные строки пишутся
var written = ctx.BulkInsertInto<IOrder>(o => o.KeepIdentity().IgnoreDuplicates())
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

## Просмотр SQL

`ToSql()` рендерит параметризованный SQL, который выполнил бы портируемый путь для первого батча, не
открывая соединение:

```csharp
var sql = ctx.BulkInsertInto<IOrder>().Values(orders).ToSql();
// insert into orders (customer_id, total) values (@p0, @p1), (@p2, @p3), (@p4, @p5)

var returningSql = ctx.BulkInsertInto<IOrder>().Values(orders).ReturningKey<int>().ToSql();
// insert into orders (customer_id, total) values (@p0, @p1), ... returning id
```

## Опции

Опции записи передаются в `BulkInsertInto<TEntity>` — записью `BulkInsertOptions` или, короче, через
колбэк `BulkInsertOptionsBuilder`:

```csharp
ctx.BulkInsertInto<IOrder>(new BulkInsertOptions { MaxBatchSize = 1_000, IgnoreDuplicates = true });
ctx.BulkInsertInto<IOrder>(o => o.MaxBatchSize(1_000).IgnoreDuplicates());
```

| Опция | Метод builder | Действие |
|---|---|---|
| `MaxBatchSize` | `MaxBatchSize(rows)` | дробление портируемого пути (по умолчанию — одно утверждение) |
| `MaxParameters` | `MaxParameters(count)` | параметров на утверждение |
| `MaxSqlLength` | `MaxSqlLength(chars)` | примерная длина SQL на утверждение |
| `KeepIdentity` | `KeepIdentity()` | запись явных значений identity (`OVERRIDING SYSTEM VALUE` в PostgreSQL, `SET IDENTITY_INSERT ... ON/OFF` в SQL Server) |
| `IgnoreDuplicates` | `IgnoreDuplicates()` | пропуск строк, нарушающих уникальность |
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

| Провайдер | Нативный bulk | `Returning` | `IgnoreDuplicates` | `KeepIdentity` |
|---|---|---|---|---|
| PostgreSQL | бинарный `COPY` | `RETURNING` (портируемо) | `ON CONFLICT DO NOTHING` | `OVERRIDING SYSTEM VALUE` |
| SQL Server | `SqlBulkCopy` | `OUTPUT` (портируемо) | — (отклоняется) | `SET IDENTITY_INSERT` |
| MySQL | — (портируемо) | — | `INSERT IGNORE` | явные значения |
| MariaDB | — (портируемо) | — | `INSERT IGNORE` | явные значения |
| SQLite | — (портируемо) | `RETURNING` (портируемо) | `INSERT OR IGNORE` | явные значения |
| ClickHouse | — (портируемо) | — | no-op (нет уникальности) | — |
| In-memory | — | — | — | `NotSupportedException` (только чтение) |

## См. также

- [Изменение данных (INSERT)](19-insert-statement.md)
- [Слияние данных (MERGE / upsert)](23-merge-statement.md)
- [Ограничения и что вне области](../advanced/limitations.md)
- [Обзор провайдеров](../providers/overview.md)
