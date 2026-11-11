# Транзакции

> Контекст nextorm может выполнять свои запросы внутри транзакции базы данных: как начатой им самим, так и той, которой владеет EF Core, Dapper или сырой ADO.NET, — в неё контекст встраивается. Роль отделена от поверхности запросов, поэтому контекст без соединения (in-memory) её не реализует, а ClickHouse — работающий по HTTP и не имеющий транзакций — её отклоняет. Изменения не отслеживаются, `SaveChanges` нет: запись остаётся явными командами.

**Предварительно:** [Подключения](../infrastructure/01-connections.md) · [Изменение данных (INSERT)](17-insert-statement.md) · [Обзор провайдеров](../connecting-providers/overview.md)

## Обзор

[`ITransactionManager`](xref:NextORM.Core.ITransactionManager) — ось транзакций [`DataContext`](xref:NextORM.Core.DataContext), симметричная [`IConnectionManager`](xref:NextORM.Core.IConnectionManager). Приведите контекст к роли, чтобы начать транзакцию или встроиться в существующую; каждая выполненная контекстом на соединении команда затем привязывается к активной транзакции.

```csharp
public interface ITransactionManager
{
    DbTransaction? CurrentTransaction { get; }
    DbTransaction BeginTransaction();
    DbTransaction BeginTransaction(IsolationLevel isolationLevel);
    Task<DbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<DbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    void UseTransaction(DbTransaction? transaction);
    void CreateSavepoint(string name);
    Task CreateSavepointAsync(string name, CancellationToken cancellationToken = default);
    void RollbackToSavepoint(string name);
    Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken = default);
    void ReleaseSavepoint(string name);
    Task ReleaseSavepointAsync(string name, CancellationToken cancellationToken = default);
}
```

## Запуск транзакции

Приведите контекст к роли и используйте методы `BeginTransaction*` в стиле ADO.NET; фиксируйте или откатывайте полученной [`DbTransaction`](https://learn.microsoft.com/dotnet/api/system.data.common.dbtransaction):

```csharp
using var ctx = new PostgresDataContext(connectionString, new DataContextBuilder());
var transactions = (ITransactionManager)ctx;

await using var tx = await transactions.BeginTransactionAsync();
ctx.CreateInsertBuilder<IOrder>().Values(new Order { Id = 42, Total = 10m }).Insert();
ctx.From<IOrder>().Where(x => x.Id == 42).ToList(); // видит незакоммиченную строку
await tx.CommitAsync();
```

## Встраивание в существующую транзакцию

Чтобы выполнять запросы nextorm внутри транзакции, которой владеет EF Core, Dapper или сырой ADO.NET, встройтесь в неё через `UseTransaction`. Контекст только привязывает её и никогда не фиксирует, не откатывает и не освобождает:

```csharp
using var db = new MyDbContext(options);                       // EF Core
await using var efTx = await db.Database.BeginTransactionAsync();
db.Orders.Add(new Order { Id = 42, Total = 10m });
await db.SaveChangesAsync();                                   // EF пишет (не закоммичено)

using var next = new PostgresDataContext(db.Database.GetDbConnection(), new DataContextBuilder());
((ITransactionManager)next).UseTransaction(efTx.GetDbTransaction());

var row = next.From<IOrder>().Where(x => x.Id == 42).FirstOrDefault(); // видит незакоммиченную строку EF
await efTx.RollbackAsync();                                    // nextorm сбрасывает завершённую транзакцию
```

Транзакция должна принадлежать соединению контекста (контекст это проверяет).

## Переиспользование существующей транзакции

Переиспользуемый метод не должен предполагать, что транзакция принадлежит ему. [`TryBeginTransaction`](xref:NextORM.Core.ITransactionManager.TryBeginTransaction(System.Data.Common.DbTransaction@)) начинает её, только если активной нет, и никогда не падает, если она уже есть:

```csharp
var transactions = (ITransactionManager)ctx;

var started = transactions.TryBeginTransaction(out var tx);   // при started == false в tx — активная транзакция
try
{
    // ... запись через ctx ...
    if (started) tx!.Commit();
}
catch
{
    if (started) tx!.Rollback();
    throw;
}
```

`TryBeginTransaction` возвращает `false` — не бросая исключение — если транзакция уже активна (в том числе встроенная через `UseTransaction`), отдавая активную транзакцию через `out`; и возвращает `false` с `null`, если у провайдера транзакций нет вовсе (ClickHouse). Коммитит или откатывает только тот, кто её начал (`started == true`); вложенный вызов просто работает в существующей транзакции. Асинхронный близнец [`TryBeginTransactionAsync`](xref:NextORM.Core.ITransactionManager.TryBeginTransactionAsync(System.Threading.CancellationToken)) сообщает тот же результат кортежем `(bool Started, DbTransaction? Transaction)`.

## Владение и жизненный цикл

* Одновременно активна только одна транзакция: повторный `BeginTransaction` или встраивание при активной бросают `InvalidOperationException`. См. [Вложенные транзакции и savepoints](#вложенные-транзакции-и-savepoints).
* Начатая через `BeginTransaction*` транзакция принадлежит контексту: если она ещё открыта к моменту освобождения контекста, контекст откатывает её. `UseTransaction(null)` её не отвязывает — зафиксируйте, откатите или освободите её.
* Переданная в `UseTransaction` транзакция принадлежит вызывающему: контекст никогда её не фиксирует, не откатывает и не освобождает. Чтобы отвязать, передайте `null` (отвязать можно только встроенную транзакцию).
* Уже зафиксированная, откаченная или освобождённая транзакция сбрасывается лениво, поэтому последующие запросы контекста выполняются вне неё.
* Как и само соединение, роль не потокобезопасна: не начинайте, не отвязывайте и не завершайте транзакцию параллельно с выполнением запросов на том же контексте.

## Вложенные транзакции и savepoints

Модель транзакций плоская: у контекста **одна** транзакция в каждый момент, и роль не реентрантна.
Второй `BeginTransaction*` при активной транзакции — как и привязка ещё одной через `UseTransaction(tx)`
— бросает `InvalidOperationException`:

```csharp
await using var tx = await transactions.BeginTransactionAsync();

await transactions.BeginTransactionAsync();   // InvalidOperationException
// A transaction is already in progress on this context. Nested transactions are not supported;
// use the active transaction instead.
```

Ambient- или scope-транзакции у nextorm тоже нет: `TransactionScope` не используется, и транзакция не
пробрасывается между контекстами. Транзакционную работу составляют, передавая по цепочке вызовов **тот
же** контекст (или тот же `DbTransaction`), поэтому вложенные вызовы выполняются в уже открытой
транзакции и сами её не коммитят и не откатывают:

```csharp
// оба вызова идут в одной транзакции; коммитит только внешний владелец
await repository.ReserveAsync(order, cancellationToken);
await payment.ChargeAsync(order, cancellationToken);
```

`UseTransaction(externalTx)` — это хук для разделения *внешне принадлежащей* транзакции (EF Core,
Dapper, сырой ADO.NET): он привязывает контекст к этой транзакции, а не способ вложенности.

**Savepoints.** Savepoint позволяет внутреннему шагу откатить только свою работу, не закрывая внешнюю
транзакцию. Роль предоставляет все три операции ADO.NET, каждую с асинхронным близнецом:

```csharp
var transactions = (ITransactionManager)ctx;

await using var tx = await transactions.BeginTransactionAsync();
await transactions.CreateSavepointAsync("before_batch");
try
{
    // ... шаг, которому может понадобиться частичный откат ...
    await transactions.ReleaseSavepointAsync("before_batch");
}
catch
{
    await transactions.RollbackToSavepointAsync("before_batch");
    throw;                                        // внешняя транзакция всё ещё открыта
}
await tx.CommitAsync();
```

`CreateSavepoint`, `RollbackToSavepoint` и `ReleaseSavepoint` (и их `*Async` формы) уходят напрямую в
[`DbTransaction`](https://learn.microsoft.com/dotnet/api/system.data.common.dbtransaction) провайдера:
nextorm создаёт именованные savepoints, откатывается к ним и освобождает их через `ITransactionManager`;
уникальность имён и вложенность по-прежнему остаются за вызывающим. Реестра и стека savepoints нет,
транзакция никогда не коммитится и не откатывается целиком, состояние команд не меняется, а владение
транзакцией не меняется тем более — поэтому встроенная через `UseTransaction` транзакция получает
savepoints наравне с собственной.

**Имена.** Правило имён — портируемая политика API, а не ограничение драйвера: 1–32 ASCII-символа
идентификатора — `[A-Za-z_][A-Za-z0-9_]*` — проверяются ординально (читаются только ASCII-диапазоны,
поэтому культура на результат не влияет). Null, пустое имя, имя с ведущей цифрой, пробелом, кавычкой,
пунктуацией, не-ASCII или длиннее 32 символов бросает `ArgumentException` до вызова драйвера; предел в 32
символа заодно исключает обрезание в SQL Server. Дубликаты имён делегируются без изменений — guard'а
уникальности нет, — а вложенные savepoints работают, потому что каждый новый savepoint отмечает свою точку.

**Guard'ы.** Проверки идут в фиксированном порядке, и первая применимая побеждает, не трогая драйвер:
поддержка транзакций → поддержка savepoints → наличие активной транзакции → валидное имя. Поэтому вызов
savepoint без активной транзакции бросает `InvalidOperationException` даже у провайдера, который savepoints
поддерживает.

**Особенности провайдеров.**

* SQLite, PostgreSQL, SQL Server, MySQL и MariaDB поддерживают savepoints: `ISqlDialect.SupportsSavepoints`
  там `true`, а MariaDB наследует значение MySQL. Сторонний диалект по умолчанию получает `false`.
* В SQL Server `ReleaseSavepoint` всё равно вызывает драйвер, и там это **не бросающий no-op, сохраняющий
  данные**: инструкции `RELEASE SAVEPOINT` в T-SQL нет. Откат *после* release определяется провайдером и в
  поддерживаемый контракт не входит.
* PostgreSQL (Npgsql) применяет savepoint сразу: create-then-rollback работает даже без промежуточной
  команды.
* В ClickHouse ADO.NET-транзакции нет вовсе, поэтому `BeginTransaction*` падает раньше любого
  savepoint-guard, а любой вызов savepoint бросает `NotSupportedException`.
* Откат к savepoint сохраняет строки и значения `NULL`, записанные до него, и отбрасывает последующую
  работу; судьбу транзакции решают только внешние commit или rollback.

## Поддержка провайдеров

`ITransactionManager` реализован для SQLite, PostgreSQL, SQL Server и MySQL/MariaDB, и контекст выставляет `DbCommand.Transaction` на всех путях исполнения (буфер, стриминг и мутации). Savepoints доступны на всех пяти — release в SQL Server это no-op, сохраняющий данные, — а ClickHouse отклоняет их на guard'е транзакций. ClickHouse работает по HTTP и не имеет ADO.NET-транзакций: его диалект сообщает [`SupportsTransactions`](xref:NextORM.Core.ISqlDialect.SupportsTransactions) как `false`, а `BeginTransaction*`/`UseTransaction` бросают `NotSupportedException`. Провайдер in-memory роль не реализует.

## См. также

- [Подключения](../infrastructure/01-connections.md)
- [Изменение данных (INSERT)](17-insert-statement.md)
- [Обзор провайдеров](../connecting-providers/overview.md)
- [Краткий справочник API](../advanced/api-reference.md)
