# Транзакции

> Контекст nextorm может выполнять свои запросы внутри транзакции базы данных: как начатой им самим, так и той, которой владеет EF Core, Dapper или сырой ADO.NET, — в неё контекст встраивается. Роль отделена от поверхности запросов, поэтому контекст без соединения (in-memory) её не реализует, а ClickHouse — работающий по HTTP и не имеющий транзакций — её отклоняет. Изменения не отслеживаются, `SaveChanges` нет: запись остаётся явными командами.

**Предварительно:** [Подключения и логирование](16-connections-and-logging.md) · [Изменение данных (INSERT)](19-insert-statement.md) · [Обзор провайдеров](../providers/overview.md)

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
}
```

## Запуск транзакции

Приведите контекст к роли и используйте методы `BeginTransaction*` в стиле ADO.NET; фиксируйте или откатывайте полученной [`DbTransaction`](https://learn.microsoft.com/dotnet/api/system.data.common.dbtransaction):

```csharp
using var ctx = new PostgresDataContext(connectionString, new DataContextBuilder());
var transactions = (ITransactionManager)ctx;

await using var tx = await transactions.BeginTransactionAsync();
ctx.InsertInto<IOrder>().Values(new Order { Id = 42, Total = 10m }).Insert();
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

## Владение и жизненный цикл

* Одновременно активна только одна транзакция: повторный `BeginTransaction` или встраивание при активной бросают `InvalidOperationException`. Вложенные транзакции и savepoints в эту поверхность не входят.
* Начатая через `BeginTransaction*` транзакция принадлежит контексту: если она ещё открыта к моменту освобождения контекста, контекст откатывает её. `UseTransaction(null)` её не отвязывает — зафиксируйте, откатите или освободите её.
* Переданная в `UseTransaction` транзакция принадлежит вызывающему: контекст никогда её не фиксирует, не откатывает и не освобождает. Чтобы отвязать, передайте `null` (отвязать можно только встроенную транзакцию).
* Уже зафиксированная, откаченная или освобождённая транзакция сбрасывается лениво, поэтому последующие запросы контекста выполняются вне неё.
* Как и само соединение, роль не потокобезопасна: не начинайте, не отвязывайте и не завершайте транзакцию параллельно с выполнением запросов на том же контексте.

## Поддержка провайдеров

`ITransactionManager` реализован для SQLite, PostgreSQL, SQL Server и MySQL/MariaDB, и контекст выставляет `DbCommand.Transaction` на всех путях исполнения (буфер, стриминг и мутации). ClickHouse работает по HTTP и не имеет ADO.NET-транзакций: его диалект сообщает [`SupportsTransactions`](xref:NextORM.Core.ISqlDialect.SupportsTransactions) как `false`, а `BeginTransaction*`/`UseTransaction` бросают `NotSupportedException`. Провайдер in-memory роль не реализует.

## См. также

- [Подключения и логирование](16-connections-and-logging.md)
- [Изменение данных (INSERT)](19-insert-statement.md)
- [Обзор провайдеров](../providers/overview.md)
- [Краткий справочник API](../advanced/api-reference.md)
