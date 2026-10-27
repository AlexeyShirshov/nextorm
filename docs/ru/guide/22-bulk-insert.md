# Массовая вставка (bulk)

`CreateBulkInsertBuilder<TEntity>()` записывает весь набор одним явным вызовом, без change tracking. Где у
провайдера есть нативный bulk API, он может использоваться — бинарный `COPY` в PostgreSQL, `SqlBulkCopy` в
SQL Server, `MySqlBulkCopy` в MySQL/MariaDB и `InsertBinaryAsync` в ClickHouse; иначе набор пишется
параметризованным `INSERT ... VALUES`, при необходимости — чанками. Какой путь выполняется, выбирает
[режим выполнения](#режимы-выполнения), а не один только провайдер.

Массовая вставка работает поверх обычного маппинга; объявление ключа, identity и computed-колонок — в
[Изменении данных (INSERT)](17-insert-statement.md).

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

// Auto в PostgreSQL берёт нативный COPY; у остальных провайдеров путь определяется опциями
var written = ctx.CreateBulkInsertBuilder<IOrder>().Values(orders).BulkInsert();
// written == 3
```

Источник `IAsyncEnumerable<TEntity>` тоже принимается и стримится — см.
[Источники-стримы](#источники-стримы). Пустой набор ничего не пишет и возвращает `0`.

Глобальные фильтры запросов не добавляются в bulk-запись; строки проверяются на соответствие активным
фильтрам цели до записи (`QueryFilterException` при нарушении). Синхронный источник проверяется целиком
до первого батча; асинхронный — по мере потока, поэтому при нарушении в поздней строке ранее записанные
строки остаются. См. [INSERT и MERGE (проверка)](../advanced/query-filters.md#insert-и-merge-проверка).

## Режимы выполнения

[`BulkInsertMode`](xref:NextORM.Core.BulkInsertMode) выбирает путь. [`Mode`](xref:NextORM.Core.BulkInsertOptions.Mode)
по умолчанию `Auto` у всех провайдеров:

| Режим | Поведение |
|---|---|
| `Auto` (по умолчанию) | Предпочитает нативный bulk-путь провайдера, если тот может выполнить все заданные опции и все предварительные условия; иначе берёт портируемый путь `INSERT ... VALUES`, и только если тот тоже выполняет запрос. |
| `Native` | Использует нативный bulk-путь и никогда не откатывается: отсутствующее предусловие, невыразимая нативным путём опция или неподдерживаемый адрес назначения бросают `NotSupportedException` с указанием причины — до чтения источника и до записи строки. |
| `Portable` | Явно выбирает параметризованный путь `INSERT ... VALUES`. Нативная опция отклоняется по имени, а не игнорируется, и запись не становится нативной неявно. |

```csharp
// оставить SQL-батч даже там, где доступна нативная загрузка
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o.Mode(BulkInsertMode.Portable))
    .Values(orders)
    .BulkInsert();
```

`Returning*` всегда пишет через портируемый путь, потому что нативные bulk API не возвращают строки;
запрос `Mode.Native` вместе с возвращающим терминалом отклоняется, а не понижается молча.

> **Совместимость:** Auto may switch existing MySQL/MariaDB/ClickHouse calls to native; Portable restores SQL-batch selection. Previously ignored unsupported options now validate strictly.
>
> По-русски: `Auto` может перевести существующие вызовы MySQL/MariaDB/ClickHouse на нативный путь;
> `Portable` возвращает выбор SQL-батча. Ранее игнорировавшиеся неподдерживаемые опции теперь
> валидируются строго.

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
| PostgreSQL нативный `COPY` | да — каждая строка пишется по мере чтения, набор не буферизуется | нет; лимит чанка запускает новый importer на каждый ограниченный чанк |
| SQL Server `SqlBulkCopy` | нет — набор сначала буферизуется в `DataTable`, так как `SqlBulkCopy` нужны метаданные типов колонок | весь набор; `MaxBatchSize` задаёт `BatchSize` драйвера, но не режет буфер |
| MySQL/MariaDB нативный `MySqlBulkCopy` | по чанкам — источник читается ограниченными чанками, каждый чанк загружается один раз | один чанк (`MaxBatchSize`, по умолчанию 10 000 строк) |
| ClickHouse нативный `InsertBinaryAsync` | по чанкам — адаптер вычитывает чанк, дожидается всех вызовов драйвера, затем берёт следующий | один чанк (`MaxBatchSize`, по умолчанию 10 000 строк) |
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
применяется, а необязательная схема квотируется отдельно от таблицы. Работает и на нативном, и на
портируемом пути `INSERT ... VALUES`.

### Части адреса назначения

Таблицу, схему и базу можно задавать и независимо, не повторяя таблицу: `Schema(name)`, `Database(name)` и
`Server(name)` задают `SchemaName`, `DatabaseName` и `ServerName`.

```csharp
// оставить замапленную таблицу и переопределить только её схему
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o.Schema("archive"))
    .Values(orders)
    .BulkInsert();
```

- Запрос **только схемы** сохраняет замапленную (или переопределённую `Table(...)`) таблицу и меняет лишь
  часть схемы.
- `TableSchema` и `SchemaName` — два псевдонима одной части: равные значения схлопываются в неё, а
  различающиеся — **ошибка аргумента**, возникающая до чтения строк.
- Каждая часть квотируется отдельно, поэтому квалифицированное имя никогда не превращается в один
  идентификатор с точками.
- **`DatabaseName` зависит от диалекта.** PostgreSQL адресует только текущую базу соединения: её
  именование — no-op, а другая база отклоняется по имени. MySQL/MariaDB и ClickHouse адресуют другую базу
  как квалификацию, SQL Server рендерит её как часть многочастного имени. Диалект, который не умеет
  выразить другую базу, отклоняет запрос, а не отбрасывает часть.
- **`ServerName` недоступен и отклоняется**: bulk-вставка никогда не перенаправляется на другой сервер.
  Используйте соединение с нужным сервером. Перенаправление не реализовано и не обещается.

## Локальные временные таблицы

`TargetKind(BulkInsertTargetKind.LocalTemporary)` пишет во временную таблицу, которая уже существует на
собственном соединении контекста и, где у провайдера есть сессия, в той же сессии. nextorm никогда не
создаёт и не удаляет таблицу и не открывает неявную транзакцию.

```csharp
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o
        .TargetKind(BulkInsertTargetKind.LocalTemporary)
        .Table("#orders_stage"))
    .Values(orders)
    .BulkInsert();
```

Временная таблица живёт во временном хранилище текущей сессии, поэтому запрос, противоречащий этому,
отклоняется по имени: `DatabaseName` вместе с локальной временной целью, квалификатор схемы в SQL Server
(`#temp` его не принимает), безсессионная вставка ClickHouse (`UseSession=false`) и параллелизм ClickHouse
больше единицы. Создание, изменение и удаление временной таблицы — вне области.

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
и SQL Server (через `OUTPUT`). MySQL, MariaDB и ClickHouse отклоняют его на bulk-пути явным
`NotSupportedException` — там ключи читаются обычным `CreateInsertBuilder`. Версионно-зависимый
`RETURNING` MariaDB (явно настроенный 13.0+) относится к однотабличному `UPDATE`, а не к bulk-`INSERT`;
bulk `Returning*` там остаётся отклонённым.

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
атомарность, начните транзакцию сами. Каждая запись ожидается (awaited) до чтения следующего чанка, и
nextorm освобождает только созданные им ресурсы — никогда источник, соединение или объект вызывающего.

Колбэк `NotifyAfter` выполняется inline и только **после завершения батча** (записанного батча на
портируемом пути, завершённого чанка загрузки в MySQL/MariaDB/ClickHouse, тика `SqlBulkCopy.NotifyAfter`
в SQL Server, завершённого чанка/importer в PostgreSQL). Он не вызывается на каждую строку и никогда не
опережает запись. Держите его быстрым и не бросающим: исключение из колбэка пробрасывается наружу, а уже
записанные строки остаются на месте; блокирующийся колбэк останавливает вызов до своего возврата. nextorm
не даёт обещаний rollback, throughput или per-row commit.

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

## Таймаут

`Timeout(TimeSpan)` задаёт **один** бюджет на весь вызов, отсчитываемый от входа в выполнение. Он
покрывает получение соединения, настройку, перечисление источника, все батчи, работу драйвера и
колбэки прогресса; он никогда не сбрасывается на батч, поэтому медленный источник и медленный драйвер
делят один дедлайн.

```csharp
var written = await ctx.CreateBulkInsertBuilder<IOrder>(o => o.Timeout(TimeSpan.FromMinutes(2)))
    .Values(ordersStream)
    .BulkInsertAsync(cancellationToken);
```

- Бюджет, равный нулю или меньший, — ошибка аргумента.
- Истёкший бюджет сообщается как `TimeoutException`.
- `CancellationToken` вызывающего всегда побеждает: отмена пользователем проявляется как
  `OperationCanceledException`, а не как таймаут, даже когда события гоняются.
- `Timeout` — единственная поверхность таймаута: прежняя опция `TimeoutSeconds` и метод builder
  `Timeout(int)` **удалены без shims**, поэтому переходите на `TimeSpan`
  (`Timeout(TimeSpan.FromSeconds(30))`). Бюджет теперь действует на всех путях, а не только на нативном
  SQL Server, и прежнее ограничение PostgreSQL «таймаут не поддерживается» больше не существует.

## Явные значения identity и пропуск дублей

`KeepIdentity` записывает значения identity из сущностей, а не позволяет серверу их генерировать.
`IgnoreDuplicates` пропускает строки, нарушающие уникальность, вместо падения всей записи.

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

`KeepIdentity` выполняется по провайдерам, на том пути, который это умеет:

| Провайдер | `KeepIdentity=true` |
|---|---|
| SQL Server | нативный `SqlBulkCopyOptions.KeepIdentity`; портируемое утверждение оборачивается в `SET IDENTITY_INSERT ... ON/OFF` |
| PostgreSQL | нативный `COPY` с явной identity-колонкой в списке колонок; портируемое утверждение добавляет `OVERRIDING SYSTEM VALUE` |
| MySQL / MariaDB | явная identity-колонка на обоих путях под scoped-режимом `NO_AUTO_VALUE_ON_ZERO`, поэтому явный `0` записывается, а не заменяется сгенерированным значением |
| ClickHouse | явное значение в нативной вставке; записываемы только обычные колонки и колонки `DEFAULT` — замапленная колонка ALIAS или MATERIALIZED отклоняется по имени |
| SQLite | явная identity-колонка в портируемом утверждении |

`IgnoreDuplicates` использует ту форму, которая есть у выбранного пути:

| Провайдер | `IgnoreDuplicates=true` |
|---|---|
| SQL Server | отклоняется по имени (формы ignore нет ни на одном пути) |
| PostgreSQL | `ON CONFLICT DO NOTHING` (портируемо) |
| SQLite | `INSERT OR IGNORE` (портируемо) |
| MySQL / MariaDB | `INSERT IGNORE` (портируемо) или `ConflictOption.Ignore` на нативной загрузке |
| ClickHouse | отклоняется по имени: уникальности нет, и обычная вставка записала бы все строки |

> `KeepIdentity()` **не даёт эффекта**, если в маппинге сущности нет identity-колонки: явные значения для
> не-identity ключей всё равно пишутся, но форма включения identity (`SET IDENTITY_INSERT`,
> `OVERRIDING SYSTEM VALUE`, guard нулевого режима MySQL) не эмитится — поэтому таблица без identity никогда
> не падает с ошибкой SQL Server 8106, и выбор пути не меняется. Проверка идёт по метаданным
> сущности-донора, а не по фактической целевой таблице: при перенацеливании через `Table(...)`
> ответственность за identity-форму целевой таблицы лежит на вызывающем.

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
который его не выражает, бросает `NotSupportedException` с перечислением всех offending-флагов (а не
игнорирует их молча) — `COPY` PostgreSQL, MySQL/MariaDB, ClickHouse и портируемый `INSERT ... VALUES`
отклоняют `TableLock=true` и `FireTriggers=true`, а `CheckConstraints=false` принимают только SQL Server
(нативный путь), MariaDB и SQLite (scoped-контроль сессии).

`KeepNulls=false` означает «применить DEFAULT целевой колонки вместо явного NULL». Он принимается только
для **default-equivalent формы** — каждая записываемая колонка nullable, а её SQL-default равен NULL или
отсутствует — и иначе отклоняется по имени на любом пути, поэтому он не бывает принят и молча
проигнорирован.

### `SqlServer(SqlServerBulkInsertOptions)`

Специфичные для SQL Server нативные опции живут в типизированной группе, поэтому их нельзя спутать с
портируемыми:

```csharp
var written = ctx.CreateBulkInsertBuilder<IOrder>(o => o.SqlServer(new SqlServerBulkInsertOptions
    {
        UseInternalTransaction = true,
    }))
    .Values(orders)
    .BulkInsert();
```

| Поле | Действие |
|---|---|
| `UseInternalTransaction` | `SqlBulkCopy` выполняет собственную транзакцию вокруг **батча драйвера** (`SqlBulkCopyOptions.UseInternalTransaction`). Незаданное/`false` значение не выставляет флаг. Это не гарантия на всю загрузку: при нескольких батчах драйвера покрыт только текущий. Комбинация с внешней транзакцией контекста отклоняется по имени до чтения источника. Портируемый путь отклоняет `true`. |

Пустая группа (все значения `null`) не создаёт запроса. Заданное поле **чужой** группы отклоняется, когда
целевой провайдер известен, — включая явный `false`. Default-equivalent значение на своём провайдере
принимается по **результату**, а не по наличию флага драйвера.

### `ClickHouse(ClickHouseBulkInsertOptions)`

Нативные опции ClickHouse — вторая типизированная группа, применяемая к `InsertBinaryAsync`:

```csharp
var written = await ctx.CreateBulkInsertBuilder<IOrder>(o => o.ClickHouse(new ClickHouseBulkInsertOptions
    {
        MaxDegreeOfParallelism = 4,
    }))
    .Values(ordersStream)
    .BulkInsertAsync(cancellationToken);
```

| Поле | Действие |
|---|---|
| `MaxDegreeOfParallelism` | Параллелизм, запрашиваемый нативной вставкой (`null` сохраняет настроенное значение по умолчанию). Сессия и параллелизм больше единицы противоречат друг другу и отклоняются по имени. Портируемое утверждение пишет один поток, поэтому принимает `null`/`1` как один и тот же результат, а значение больше единицы отклоняет по имени. Неположительное значение — ошибка аргумента на любом пути. |
| `UseSession` | `null` сохраняет настроенную сессию, `true` требует фактический session id, `false` явно запрашивает безсессионную нетемпоральную вставку. На портируемом пути запрос выполняется, только если соединение контекста уже несёт эту сессию, и иначе отклоняется по имени. |

Пустая группа не создаёт запроса. Группа отклоняется, если передана другому провайдеру.

Класс `ClickHouseBulkCopy` из `ClickHouse.Driver` 1.4.0 **не** устарел — устарел только его метод
`InitAsync`, — поэтому причиной использования `InsertBinaryAsync` является не класс: бинарный API вставки
даёт копию настроек на конкретную вставку и контракт session/parallelism выше, и работает через
собственный клиент контекста.

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
| `Mode` | `Mode(BulkInsertMode)` | путь выполнения: `Auto` (по умолчанию), `Native`, `Portable` |
| `TargetKind` | `TargetKind(BulkInsertTargetKind)` | `Regular` (по умолчанию) или `LocalTemporary` |
| `MaxBatchSize` | `MaxBatchSize(rows)` | строк на утверждение/батч на портируемом пути; размер чанка на нативных путях MySQL/MariaDB/ClickHouse; `SqlBulkCopy.BatchSize` в SQL Server; новый importer на чанк в PostgreSQL |
| `MaxParameters` | `MaxParameters(count)` | параметров на утверждение (портируемый путь) |
| `MaxSqlLength` | `MaxSqlLength(chars)` | примерная длина SQL на утверждение (портируемый путь) |
| `TableName` / `TableSchema` | `Table(name)` / `Table(schema, name)` | переопределение целевой таблицы, при желании schema-квалифицированной |
| `SchemaName` | `Schema(schema)` | переопределение только схемы при сохранении замапленной/явной таблицы |
| `DatabaseName` | `Database(database)` | часть базы там, где диалект умеет адресовать другую базу |
| `ServerName` | `Server(server)` | **отклоняется**: bulk-вставка никогда не перенаправляется на другой сервер |
| `KeepIdentity` | `KeepIdentity()` | явные значения identity на пути, который это умеет; без эффекта, если у сущности нет identity-колонки |
| `IgnoreDuplicates` | `IgnoreDuplicates()` | пропуск строк, нарушающих уникальность, где есть форма ignore |
| `CheckConstraints` | `CheckConstraints()` | проверять ограничения CHECK/FOREIGN KEY; `false` — только там, где есть scoped-контроль |
| `TableLock` | `TableLock()` | табличная bulk-блокировка; только нативный путь SQL Server |
| `KeepNulls` | `KeepNulls()` | `true`: явные `NULL`; `false`: только default-equivalent формы |
| `FireTriggers` | `FireTriggers()` | запускать `INSERT`-триггеры; только нативный путь SQL Server |
| `Timeout` | `Timeout(TimeSpan)` | один бюджет на весь вызов для всех путей |
| `SqlServer` | `SqlServer(SqlServerBulkInsertOptions)` | нативная группа SQL Server (`UseInternalTransaction`) |
| `ClickHouse` | `ClickHouse(ClickHouseBulkInsertOptions)` | нативная группа ClickHouse (`MaxDegreeOfParallelism`, `UseSession`) |
| `Progress` / `NotifyEvery` | `NotifyAfter(rows, onRows)` | прогресс с накопленным числом записанных строк, после завершённых батчей |
| `ProgressCancellationTokenSource` | `ProgressCancellationTokenSource(source)` | токен, передаваемый в колбэк прогресса |

Опция, которую выбранный провайдер/путь не может выполнить, отклоняется `NotSupportedException` с её
именем — до чтения источника; ни одна опция не принимается и не отбрасывается молча.

На возвращаемом builder:

| Метод | Действие |
|---|---|
| `Values(IEnumerable<TEntity>)` / `Values(IAsyncEnumerable<TEntity>)` | набор, читается один раз; пустой набор ничего не пишет и возвращает `0` |
| `ReturningKey<TKey>()` / `Returning(projection)` | материализация ключей/проекции записанных строк через `RETURNING`/`OUTPUT` |
| `BulkInsert()` / `BulkInsertAsync(ct)` | записать набор и вернуть число записанных строк |
| `ToSql()` | отрендерить SQL первого батча без выполнения |

## Поддержка провайдеров

| Провайдер | Путь `Auto`/`Native` | Квалификация цели | `Returning` | `IgnoreDuplicates` | `KeepIdentity` | Bulk-copy флаги |
|---|---|---|---|---|---|---|
| PostgreSQL | бинарный `COPY` | `schema.table`; `DatabaseName` — только текущая база | `RETURNING` (портируемо) | `ON CONFLICT DO NOTHING` | `OVERRIDING SYSTEM VALUE` (портируемо) / явный список колонок `COPY` | — (отклоняются) |
| SQL Server | `SqlBulkCopy` | `database.schema.table` | `OUTPUT` (портируемо) | — (отклоняется) | `SqlBulkCopyOptions.KeepIdentity` / `SET IDENTITY_INSERT` | `CheckConstraints`/`TableLock`/`KeepNulls`/`FireTriggers` |
| MySQL | `MySqlBulkCopy` (`LOAD DATA LOCAL INFILE`) | `db.table` | — (отклоняется) | `ConflictOption.Ignore` / `INSERT IGNORE` | явная колонка + guard нулевого режима | — (отклоняются) |
| MariaDB | `MySqlBulkCopy` (`LOAD DATA LOCAL INFILE`) | `db.table` | — (отклоняется) | `ConflictOption.Ignore` / `INSERT IGNORE` | явная колонка + guard нулевого режима | — (отклоняются) |
| SQLite | — (только портируемый; `Native` отклоняется) | `schema.table` | `RETURNING` (портируемо) | `INSERT OR IGNORE` | явная колонка | — (отклоняются) |
| ClickHouse | `InsertBinaryAsync` | `db.table` | — (отклоняется) | — (отклоняется) | явное значение, только записываемые колонки | — (отклоняются) |
| In-memory | — | — | — | — | `NotSupportedException` (только чтение) | — (только чтение) |

## Предварительные условия драйверов

`Auto` берёт нативный путь, только если выполнены все условия ниже; `Native` отклоняет по имени;
портируемому пути ни одно из них не нужно. Отсутствующее условие никогда не исправляется втихую — nextorm
не меняет ни настройку драйвера, ни серверную переменную, ни строку подключения.

| Провайдер | Что нужно для нативного пути |
|---|---|
| SQL Server | соединение `Microsoft.Data.SqlClient` `SqlConnection`; внутренняя транзакция включается явно через `SqlServer(new SqlServerBulkInsertOptions { UseInternalTransaction = true })` и несовместима с внешней транзакцией контекста |
| PostgreSQL | соединение `Npgsql`; бинарный importer `COPY` пишет на этом соединении, а конечный `MaxBatchSize` запускает по одному importer на ограниченный чанк (завершается при успехе, освобождается при ошибке или отмене) |
| MySQL / MariaDB | соединение MySqlConnector с `AllowLoadLocalInfile=true` **и** серверная переменная `@@GLOBAL.local_infile=1`. Клиентская опция читается из строки подключения, серверная — read-only preflight-запросом (`select @@global.local_infile`); nextorm не включает ни то, ни другое. Без них нативный путь недоступен: `Auto` откатывается, только если портируемый путь выполняет весь запрос, а `Native` отклоняет по имени |
| ClickHouse | клиент `ClickHouse.Driver` (`ClickHouseClient`/`ClickHouseDataSource` — перегрузки `UseClickHouse`), потому что `InsertBinaryAsync` требует его; настройки клиента копируются для вставки, а запрос сессии разрешается по фактическому session id. Параллелизм больше единицы нельзя сочетать с сессией (одна конкурентная команда на сессию) |
| SQLite | ничего: нативного bulk API нет, поэтому каждая запись идёт портируемым путём на собственном соединении контекста, а запрос `Mode.Native` отклоняется. Сертифицированная конфигурация — файловая база (живой набор использует её, а не межпроцессный сервер) |

## См. также

- [Изменение данных (INSERT)](17-insert-statement.md)
- [Слияние данных (MERGE / upsert)](21-merge-statement.md)
- [Глобальные фильтры запросов](../advanced/query-filters.md)
- [Ограничения и что вне области](../advanced/limitations.md)
- [Обзор провайдеров](../connecting-providers/overview.md)
