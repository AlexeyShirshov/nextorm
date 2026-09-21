# TODO: Транзакции (enlistment, общая транзакция с EF Core / Dapper)

> Рабочий план (design RFC). Источник: GitHub issue
> [#32 «TODO: Transactions»](https://github.com/AlexeyShirshov/nextorm/issues/32), milestone `1.0-a.4`.
> Дополняет [todo_interceptors.md](todo_interceptors.md) (перехват соединения) и
> [comparison/linq2db-comparison.md](comparison/linq2db-comparison.md:56) (у linq2db интеграция с EF Core есть — у nextorm нет).

## Пункт и цель

- Фича: использование запросов nextorm внутри транзакции — как созданной самим nextorm, так и
  **переданной извне** (EF Core `Database.BeginTransaction()`, Dapper, сырой ADO.NET).
- Критерий приёмки: контекст умеет начать/принять `DbTransaction`, все его команды автоматически
  получают `DbCommand.Transaction`, а запрос, выполненный внутри чужой транзакции, видит
  незакоммиченные изменения и откатывается вместе с ней.
- Библиотека остаётся **read-only**: транзакция управляет видимостью чтения и участием в чужой
  транзакции, но не добавляет DML/`SaveChanges`/change tracking.

## Почему это нужно (мотивация)

1. **EF Core / Dapper interop.** Провайдеры уже принимают чужой `DbConnection`
   (`UseSqlite(DbConnection)`, `UsePostgres(DbConnection)`), т.е. nextorm умеет работать на том же
   соединении, что `DbContext`. Без транзакций этот сценарий ломается: как только EF открывает
   транзакцию, выполнение команды nextorm на том же соединении падает у провайдера (см. ниже).
2. **Консистентность чтения.** Несколько nextorm-запросов в одной транзакции видят один снимок
   данных — это ожидаемая семантика для отчётов/сложных read-сценариев.
3. **Пробел в матрице.** В прежних провайдерных бэклогах транзакции перечислены как
   отсутствующие; issue #32 — их общий трекер.

## Текущее состояние и разрыв

Токена `transaction` в `src/nextorm.core` нет вообще. Соединение — ось `DbConnectionManager`
(`DataContext/DbConnectionManager.cs:15`), а `DataContext` реализует роль `IConnectionManager`
(`DataContext/DataContext.cs:7`, `DataContext/Roles/IConnectionManager.cs:10`).

| Слой | Где | Чего не хватает |
|---|---|---|
| Роль соединения | `IConnectionManager.cs:10` | нет `BeginTransaction`/`CurrentTransaction`/`UseTransaction` |
| Жизненный цикл | `DbConnectionManager.cs:44-62`, `:131-155` | нет ветки транзакции; `DisposeConnection` не завершает её |
| Построение команды | `DataContext/Cache/DbPreparedQueryCommand.cs:41-114` | `GetDbCommand` биндит только `Connection` (`:107-111`), не `Transaction` |
| Исполнение (буфер) | `DataContext/QueryExecutor.cs:59-69`, `:72-82` | команда не получает транзакцию |
| Исполнение (стрим) | `DataContext/ResultSetEnumerator.cs:155-162`, `:163-192` | то же |
| План-кэш | `DbPreparedQueryCommand.ResetConnection` (`:115-123`) | при смене соединения нужно снимать и транзакцию |

**Наблюдаемый эффект без фикса.** `DbConnectionManager.EnsureConnectionOpen` (`:44-62`) открывает
соединение и не привязывает активную транзакцию. Затем:
- SQL Server: `InvalidOperationException` — «requires the command to have a transaction when the
  connection ... is in a pending local transaction»;
- Npgsql / MySqlConnector / Microsoft.Data.Sqlite: аналогичная проверка при `ExecuteReader`.

То есть даже простое `db.Database.BeginTransaction()` + nextorm-запрос на том же соединении сейчас
не работает.

## Дизайн: роль `ITransactionManager`

`IDataContext` намеренно **не** включает `IConnectionManager`, чтобы in-memory контекст не
реализовывал no-op (`IDataContext.cs:13-15`). Транзакция следует той же логике: отдельная роль,
которую реализует только контекст с соединением.

```csharp
namespace NextORM.Core;

/// <summary>
/// Owns the transaction associated with the context's connection: starting one, enlisting in a
/// caller-supplied one, and exposing the active transaction to the execution path.
/// Deliberately separate from <see cref="IConnectionManager"/> (SRP) and not part of
/// <see cref="IDataContext"/>, so the in-memory context is not forced to implement it.
/// </summary>
public interface ITransactionManager
{
    /// <summary>The active transaction, or <c>null</c> when none is enlisted.</summary>
    DbTransaction? CurrentTransaction { get; }

    DbTransaction BeginTransaction();
    DbTransaction BeginTransaction(IsolationLevel isolationLevel);
    Task<DbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<DbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enlists an externally owned transaction (for example the one behind
    /// <c>DbContext.Database.CurrentTransaction</c>). The context never commits, rolls back or
    /// disposes it; pass <c>null</c> to detach.
    /// </summary>
    void UseTransaction(DbTransaction? transaction);
}
```

Имена выбраны под BCL/EF Core: `BeginTransaction` (ADO.NET) и `Database.UseTransaction` /
`Database.CurrentTransaction` (EF Core). `CurrentTransaction` — свойство, а не метод, чтобы не
конфликтовать с `UseTransaction`.

### Владение (ownership)

Симметрично соединению (`DbConnectionManager.cs:94-105`, `:141-153`):

| Транзакция | Кто завершает/освобождает |
|---|---|
| Создана `BeginTransaction*` | контекст: завершает и/или освобождает при `Dispose`, если пользователь не сделал этого сам |
| Передана `UseTransaction` | вызывающий: контекст только привязывает и снимает привязку |

### Провайдерная матрица

| Провайдер | `ITransactionManager` | Комментарий |
|---|---|---|
| SQLite | да | `Microsoft.Data.Sqlite` поддерживает локальные транзакции |
| PostgreSQL | да | полноценный `BEGIN`/`COMMIT`/`SAVEPOINT` (savepoint — вне фазы 1) |
| SQL Server | да | полноценный; здесь и возникал `InvalidOperationException` |
| MySQL / MariaDB | да | `MySqlConnector` |
| ClickHouse | **нет** | HTTP-протокол, транзакций в ADO-смысле нет; `BeginTransaction*` → `NotSupportedException` с явным сообщением |
| In-memory | **нет** | только `IDataContext`, как и для соединения |

`DataContext` реализует `ITransactionManager` (делегируя в менеджер), а `ClickHouseDataContext`
переопределяет begin на `NotSupportedException` — по образцу capability-флагов диалекта.

### Хранение состояния

Расширяется `DbConnectionManager` (ось соединения): транзакция не живёт дольше соединения и должна
сниматься там же, где сейчас сбрасывается привязка команд (`ConnDisposed`, `DbConnectionManager.cs:107-119`).
Имя можно оставить или уточнить до `DbConnectionLifecycle` — открытый вопрос.

## Точки встраивания

1. **`DbConnectionManager`** — новое состояние `_currentTransaction` + begin/use; в `ConnDisposed`
   и `DisposeConnection` (`:107-119`, `:131-155`) очищать транзакцию и сбрасывать её на кэшированных
   командах (рядом с `ResetConnection`).
2. **`DbPreparedQueryCommand.GetDbCommand`** (`:41-114`) — принимать `DbTransaction?` и выставлять
   `cmd.Transaction` по смене значения (по аналогии с `DbCommandConnection`). `ResetConnection`
   (`:115-123`) обнуляет и `DbCommandTransaction`.
3. **`QueryExecutor`** (`:59-69`, `:72-82`) — читать текущую транзакцию через новый делегат
   `Func<DbTransaction?>` (рядом с `_createParam`, `QueryExecutor.cs:25-37`) и передавать её в
   `GetDbCommand`.
4. **`ResultSetEnumerator`** (`:155-162`, `:163-192`) — тот же делегат в `InitEnumerator`; выставить
   `cmd.Transaction` перед `ExecuteReader(Async)`.
5. **`DataContext`** (`DataContext.cs`) — реализовать `ITransactionManager`, прокинуть делегат в
   `QueryExecutor`/`ResultSetEnumerator`.

Делегат `Func<DbTransaction?>` предпочтительнее добавления `CurrentTransaction` в
`IConnectionManager`: `QueryExecutor` уже принимает делегаты и не зависит от конкретного контекста
(DIP), а роль соединения остаётся про жизненный цикл (`todo_interceptors.md:158-159`).

## Взаимодействие с планом-кэшем

Кэшированный `DbCommand` переиспользуется (план-кэш `[ThreadStatic]`, `QueryPlanner.cs:186`) и
сейчас перепривязывается к соединению на каждом исполнении (`GetDbCommand`, `:107-111`).
Транзакция — тоже runtime-состояние, поэтому:

- `cmd.Transaction` выставляется на **каждом** исполнении, а не при построении плана;
- ключ плана не меняется (транзакция не влияет на SQL);
- при `ResetConnection`/`ConnDisposed` транзакция снимается, чтобы план не держал мёртвый
  `DbTransaction`;
- контекст не thread-safe для параллельного изменения транзакции — как и ADO.NET-соединение;
  задокументировать.

## Публичный API (черновик)

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

// (A) nextorm владеет транзакцией
await using var tx = await ((ITransactionManager)ctx).BeginTransactionAsync();
var rows = ctx.From<ISimpleEntity>().Where(x => x.Id > 10).ToList();
await tx.CommitAsync();

// (B) чужая транзакция (EF Core) — контекст только встраивается
using var db = new MyDbContext(options);                    // EF Core
await using var efTx = await db.Database.BeginTransactionAsync();
db.Add(entity); await db.SaveChangesAsync();                // EF пишет и трекает

using var next = new DataContextBuilder()
    .UsePostgres(db.Database.GetDbConnection())
    .CreateDataContext();
((ITransactionManager)next).UseTransaction(efTx.GetDbTransaction());

var hot = next.From<ISimpleEntity>().Where(x => x.Id == entity.Id).FirstOrDefault(); // видит незакоммиченное
await efTx.CommitAsync();
```

Доступ через приведение к `ITransactionManager` — как и у `IConnectionManager`
(`docs/guide/16-connections-and-logging.md:106-111`); при желании можно добавить
`DataContext.BeginTransaction()`-удобства на конкретном типе.

## Ограничения и цена

- **Только вложенное участие, без DML.** Коммит/откат управляют ADO-транзакцией; у nextorm нет
  insert/update/delete/`SaveChanges`.
- **Вложенность и savepoints — вне фазы 1.** Повторный `BeginTransaction` при активной транзакции →
  `InvalidOperationException`. `TransactionScope` (ambient) — фаза 2: у провайдеров разная политика
  авто-энлиста, а `EnsureConnectionOpen` может открыть соединение раньше scope.
- **ClickHouse** транзакции не поддерживает — `NotSupportedException`, а не молчаливый no-op.
- **Аллокации.** При отсутствии транзакции (`CurrentTransaction is null`) путь исполнения не должен
  добавлять ни одного бокса/замыкания — иначе просядут бенчмарки
  (`docs/specs/performance/benchmark-report.md`).
- **Публичный API «запирается»** (`API-NAMING-REVIEW.md`): добавляем один интерфейс (+ перегрузки
  методов) в фазе 1, не весь набор сразу.
- **`DbConnectionManager`/`QueryExecutor`/`ResultSetEnumerator` — `internal`**: состояние
  прокидывается конструктором/делегатом, без зависимости на конкретный `DataContext`.

## Этапы внедрения

- **Фаза 1 (MVP):** `ITransactionManager` (begin/use/current), привязка `cmd.Transaction` во всех
  путях (буфер + стрим), владение и очистка, ClickHouse/in-memory без поддержки. Закрывает
  сценарии (A) и (B).
- **Фаза 2:** `TransactionScope`/ambient, savepoints и вложенные транзакции, `BeginTransactionAsync`
  с таймаутом, интеграция с интерцепторами (`IConnectionInterceptor`, `todo_interceptors.md`).
- **Вне области:** DML/`SaveChanges`, change tracking, распределённые транзакции.

## План тестов

- Core (`tests/nextorm.core.tests/`): `InMemoryDataContext` **не** реализует `ITransactionManager`;
  `UseTransaction(null)` снимает привязку; повторный `BeginTransaction` бросает.
- SQLite (`tests/nextorm.sqlite.tests/TransactionTests.cs`): begin/commit; rollback отменяет
  изменения; запрос внутри транзакции видит незакоммиченное; `CurrentTransaction`; `cmd.Transaction`
  выставлен; `UseTransaction` на чужий транзакции; dispose контекста откатывает незавершённую
  транзакцию; снятие транзакции при disposal соединения (рядом с
  `ConnectionManagementTests.cs:24-101`).
- Интеграция (`tests/nextorm.integration.tests/`, `CommonTestSuite`): begin/commit/rollback на
  PostgreSQL/SQL Server/MySQL; **EF Core shared transaction** (EF Core SQLite уже центрально
  запинен, `Directory.Packages.props:19-20`) — nextorm-запрос внутри `db.Database.BeginTransaction()`.
- ClickHouse: `BeginTransaction` → `NotSupportedException`.
- Регресс: раньше запрос внутри чужой транзакции падал у провайдера — тест-предохранитель на
  `command.Transaction`.
- Покрытие: не ниже базового (84.9% line / 73.2% branch); `coverage.settings.xml` включает
  `nextorm.{core,sqlite,postgres,sqlserver}` — новые файлы core учитываются.

## Открытые вопросы

1. Возвращать `DbTransaction` или обёртку `INextOrmTransaction` с `CommitAsync`/`RollbackAsync` и
   `DisposeAsync` (эргономика владения)? Фаза 1 — `DbTransaction`.
2. `ITransactionManager` vs `IDbTransactionManager`; `UseTransaction` vs `SetTransaction`.
3. Оставлять состояние в `DbConnectionManager` или вынести `DbTransactionManager` и переименовать
   менеджер соединения в `DbConnectionLifecycle`.
4. `CurrentTransaction` на `IConnectionManager` vs отдельный `Func<DbTransaction?>` в
   `QueryExecutor`/`ResultSetEnumerator` (рекомендация — второе).
5. Нужен ли `DataContext.BeginTransaction()` как удобство поверх приведения к роли.
6. ClickHouse: бросать из begin или не реализовывать роль (рекомендация — бросать).

## Файлы к изменению

- Новое: `src/nextorm.core/DataContext/Roles/ITransactionManager.cs`.
- Правки: `DataContext/DataContext.cs`, `DataContext/DbConnectionManager.cs`,
  `DataContext/QueryExecutor.cs`, `DataContext/ResultSetEnumerator.cs`,
  `DataContext/Cache/DbPreparedQueryCommand.cs`, `src/nextorm.clickhouse/ClickHouseDataContext.cs`.
- Тесты: `tests/nextorm.core.tests/`, `tests/nextorm.sqlite.tests/TransactionTests.cs`,
  `tests/nextorm.integration.tests/` (CommonTestSuite + shared-EF-transaction).
- Документация: `docs/guide/16-connections-and-logging.md` (+ `docs/ru/guide/`),
  `docs/advanced/api-reference.md` (+RU), `docs/advanced/limitations.md` (+RU),
  `docs/specs/comparison/linq2db-comparison.md` (+RU), `docs/specs/roadmap/todo_*.md`.
