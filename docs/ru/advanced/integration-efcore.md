# Интеграция с Entity Framework Core

> Запускайте чтения и запись nextorm поверх соединения и транзакции существующего EF Core `DbContext`, переиспользуя маппинг из EF-модели вместо повторного его объявления.

**Предварительные требования:** [Provider overview](../providers/overview.md) · [Транзакции](../guide/21-transactions.md) · [API reference](api-reference.md)

## Обзор

Пакет `nextorm.entityframeworkcore` связывает EF Core `DbContext` с контекстом nextorm. `db.CreateNextOrmContext()` возвращает [`IDataContext`](xref:NextORM.Core.IDataContext), который:

- выполняет каждый запрос на том самом [`DbConnection`](https://learn.microsoft.com/dotnet/api/system.data.common.dbconnection), которым уже владеет `DbContext`;
- встраивается в транзакцию, открытую EF (`db.Database.CurrentTransaction`), если она есть;
- читает маппинг таблиц/колонок из EF `IModel`, поэтому классам сущностей не нужны nextorm-атрибуты `[SqlTable]`/`[Column]` (или fluent-конфигурация).

Это позволяет оставить запись, отслеживание изменений и связывание навигаций в EF Core, а аналитические чтения nextorm (окна, CTE, агрегаты) выполнять на том же соединении и в той же транзакции. Мост делит это соединение и транзакцию и никогда не коммитит, не откатывает и не закрывает их; владение обоими остаётся за EF Core.

## Требования

- EF Core **Relational** (`Microsoft.EntityFrameworkCore.Relational`) с одним из поддерживаемых провайдеров: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.SqlServer`, `Pomelo.EntityFrameworkCore.MySql` (MySQL и MariaDB), Oracle `MySql.EntityFrameworkCore` (MySQL) или `Microsoft.EntityFrameworkCore.Sqlite`. Распознаются оба MySQL-провайдера; поскольку у Pomelo пока нет релиза под EF Core 10, путь общей транзакции на MySQL проверен через Oracle `MySql.EntityFrameworkCore` 10.x (ADO.NET-драйвер `MySql.Data`) — см. [Общее соединение и транзакция](#общее-соединение-и-транзакция).
- Пакет `nextorm.entityframeworkcore`; он ссылается на `nextorm.core` и четыре провайдерных пакета, поэтому отдельная ссылка на nextorm-провайдер для маппинга не нужна.
- Нереляционный провайдер EF `InMemory` не поддерживается (см. [Ограничения](#ограничения)).

## Минимальный пример

```csharp
using Microsoft.EntityFrameworkCore;
using NextORM.Core;                 // From<T>() и терминальные методы-расширения
using NextORM.EntityFrameworkCore; // CreateNextOrmContext()

using var db = new AppDbContext(options); // ваш EF Core-контекст

// nextorm работает на соединении db и, если EF её открыл, в текущей транзакции EF.
using var next = db.CreateNextOrmContext();

var top = next.From<Order>()
    .Where(o => o.Total > 100)
    .OrderBy(o => o.Total)
    .Select(o => new { o.Id, o.Total })
    .ToList();
```

`CreateNextOrmContext` также принимает необязательный колбэк `configure`, который вызывается после выбора провайдера по `Database.ProviderName`; используйте его, чтобы переопределить умолчания nextorm (логгер, соглашение об именах, тайм-аут команды, …):

```csharp
using var next = db.CreateNextOrmContext(builder => builder
    .UseLoggerFactory(loggerFactory)
    .UseCommandTimeout(30));
```

Чтобы зарегистрировать маппинг без создания контекста, вызовите [`NextOrmModelMapper.Register`](xref:NextORM.EntityFrameworkCore.NextOrmModelMapper.Register(Microsoft.EntityFrameworkCore.Metadata.IModel)) напрямую с `db.Model` — см. [Отображение модели](#отображение-модели).

## Опции и внедрение зависимостей

`UseNextOrm` сохраняет необязательную конфигурацию nextorm в опциях EF Core, поэтому каждый последующий вызов моста её подхватывает, не принимая делегат заново:

```csharp
DbContextOptionsBuilder UseNextOrm(this DbContextOptionsBuilder optionsBuilder, Action<DataContextBuilder>? configure = null);

DbContextOptionsBuilder<TContext> UseNextOrm<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder, Action<DataContextBuilder>? configure = null)
    where TContext : DbContext;
```

```csharp
public sealed class AppDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
            .UseNpgsql(connectionString)
            .UseNextOrm(builder => builder.UseCommandTimeout(30));
}

using var db = new AppDbContext();

using var next = db.GetNextOrmContext();

var top = next.From<Order>()
    .Where(o => o.Total > 100)
    .Select(o => new { o.Id, o.Total })
    .ToList();
```

`GetNextOrmContext` не принимает аргументов — соединение, текущую транзакцию и модель он читает из `db`, а конфигурацию — из сохранённой `UseNextOrm`:

```csharp
IDataContext GetNextOrmContext(this DbContext dbContext);
```

`AddNextOrmFromDbContext<TDbContext>` регистрирует `IDataContext` как **scoped**-сервис, построенный из `TDbContext` текущей области, поэтому его время жизни совпадает с EF-контекстом:

```csharp
IServiceCollection AddNextOrmFromDbContext<TDbContext>(this IServiceCollection services, Action<DataContextBuilder>? configure = null)
    where TDbContext : DbContext;
```

```csharp
services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseNextOrm(builder => builder.UseCommandTimeout(30)));

services.AddNextOrmFromDbContext<AppDbContext>();
```

После этого класс может получать `IDataContext` через конструктор. Колбэк `configure`, переданный в `AddNextOrmFromDbContext`, вызывается после делегата, сохранённого `UseNextOrm`, поэтому может переопределить его для конкретной регистрации.

## Трансляция EF-запроса через `ToNextOrm`

`ToNextOrm` преобразует EF Core `IQueryable<T>` с корнем `DbSet<T>` в эквивалентный nextorm [`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1), поэтому LINQ-to-Entities-запрос выполняется на том же соединении и в той же транзакции. Обе перегрузки требуют класс-сущность:

```csharp
EntityBuilder<T> ToNextOrm<T>(this DbSet<T> source) where T : class;

EntityBuilder<T> ToNextOrm<T>(this IQueryable<T> source, DbContext dbContext) where T : class;
```

Перегрузка `DbSet<T>` сама определяет владеющий `DbContext`; передавайте контекст явно через перегрузку `IQueryable<T>, DbContext` для запроса, уже составленного из операторов:

```csharp
var all = db.Orders
    .ToNextOrm()
    .Select(o => new { o.Id, o.Total })
    .ToList();

var top = db.Orders
    .Where(o => o.Total > 100)
    .OrderBy(o => o.Total)
    .ThenBy(o => o.Id)
    .Skip(20)
    .Take(10)
    .ToNextOrm(db)
    .Select(o => new { o.Id, o.Total })
    .ToList();
```

### Поддерживаемые операторы

Транслируются только операторы, которые nextorm умеет рендерить без клиентского фолбэка:

| Оператор EF Core | nextorm |
|---|---|
| `Where` | `Where(predicate)` |
| `OrderBy` / `OrderByDescending` | единственный primary `OrderBy` / `OrderByDescending` |
| `ThenBy` / `ThenByDescending` | продолжения ключей сортировки |
| `Skip(n)` | `Offset(n)` |
| `Take(n)` | `Limit(n)` |
| `Distinct()` | `Distinct()` |
| `AsNoTracking()` / `AsTracking()` / `TagWith(...)` | игнорируются |

**`Select` в nextorm терминальный.** `ToNextOrm` возвращает `EntityBuilder<T>`, поэтому проецируйте **после** него средствами nextorm (`Select`/проекция); `Select`, поставленный перед `ToNextOrm(...)`, распознаётся как неподдерживаемый оператор.

## Общее соединение и транзакция

`CreateNextOrmContext` читает `dbContext.Database.GetDbConnection()` и строит контекст nextorm поверх этого самого экземпляра `DbConnection`. Если соединение закрыто к моменту выполнения запроса nextorm, nextorm открывает его и оставляет открытым; он никогда не закрывает, не фиксирует и не откатывает заимствованные соединение или транзакцию. EF Core владеет ими всё время их жизни, и освобождение контекста nextorm не освобождает ни то, ни другое.

Когда `dbContext.Database.CurrentTransaction` активна, мост встраивается в неё через `ITransactionManager.UseTransaction(...)`. Пока EF-транзакция открыта, nextorm видит незакоммиченные строки EF, а откат EF их убирает:

```csharp
await using var tx = await db.Database.BeginTransactionAsync();

db.Rows.Add(new Row { Name = "pending" });
await db.SaveChangesAsync(ct);          // не закоммичено, всё ещё внутри tx

using var next = db.CreateNextOrmContext();
next.From<Row>().Where(r => r.Name == "pending").ToList(); // видит строку

await tx.RollbackAsync(ct);
next.From<Row>().Where(r => r.Name == "pending").ToList(); // пусто
```

Путь общего соединения/транзакции проверен end-to-end container-backed интеграционными тестами на **PostgreSQL**, **SQL Server** и **MySQL** и локальными тестами на **SQLite**. На PostgreSQL, SQL Server и MySQL публичный мост `CreateNextOrmContext()` прогоняется против живого сервера: nextorm заимствует тот самый `DbConnection`, которым владеет EF, видит незакоммиченные строки EF внутри открытой транзакции, откат EF их удаляет, `InsertInto` nextorm внутри транзакции EF виден EF и затем коммитится или откатывается вместе с ней, а освобождение nextorm-контекста оставляет соединение EF открытым, а транзакцию EF — активной. На SQLite тесты покрывают видимость незакоммиченных строк EF и их удаление откатом EF. Проверка PostgreSQL, SQL Server и MySQL требует прогона интеграционной сюиты против поднятых контейнеров (Testcontainers); SQLite-сценарии работают без контейнера.

> **MySQL проверен через провайдер Oracle.** Поскольку у `Pomelo.EntityFrameworkCore.MySql` нет релиза под EF Core 10 (последний 9.0.0 нацелен на EF Core 9), container-backed тесты MySQL выполняются на EF Core 10 с Oracle `MySql.EntityFrameworkCore` 10.0.9 и его ADO.NET-драйвером `MySql.Data`. Мост распознаёт оба имени провайдера — `Pomelo.EntityFrameworkCore.MySql` и `MySql.EntityFrameworkCore`; чтобы обслужить второй драйвер, `nextorm.mysql` создаёт параметры через выполняемую команду (`command.CreateParameter()`), а обычный путь `MySqlConnector` остаётся без изменений. Совместная работа в транзакции EF на MySQL, таким образом, проверена, а не отложена. MariaDB обслуживается только провайдером Pomelo.

Контракт встраивания, общий с Dapper и сырым ADO.NET, описан в разделе [Транзакции](../guide/21-transactions.md).

## Отображение модели

[`NextOrmModelMapper.Register(db.Model)`](xref:NextORM.EntityFrameworkCore.NextOrmModelMapper.Register(Microsoft.EntityFrameworkCore.Metadata.IModel)) проецирует EF-модель в метаданные nextorm и вызывается автоматически из `CreateNextOrmContext`:

- **Таблица / представление** — имя таблицы (`entityType.GetTableName()`) или имя представления (`GetViewName()`) для типа, отображённого на представление.
- **Колонки** — имя колонки из `property.GetColumnName()`, поэтому переименованные колонки (`HasColumnName("selected_id")`) учитываются без nextorm-атрибута.
- **Ключ / identity / computed** — первичный ключ (`FindPrimaryKey()`), `ValueGenerated.OnAdd` (identity) и `ValueGenerated.OnAddOrUpdate` (computed) переносятся.
- **Пропускаются** — shadow-свойства (без CLR-члена), owned-типы и типы сущностей без имени таблицы/представления или без отображённых CLR-свойств.
- **Отклоняются** — формы модели, которые интеграция не может представить корректно и иначе отобразила бы неверно: таблица со схемой (в метаданных nextorm нет члена схемы), глобальный фильтр запросов (фильтрация soft-delete / мультитенантность была бы потеряна) и любая иерархия наследования — TPH/TPT/TPC (nextorm не переносит дискриминатор). Каждая форма бросает `NotSupportedException`.

Поскольку кэш метаданных nextorm процесс-глобальный и ключуется по `Type` (`DataContextCache.Metadata`), **на процесс существует не более одного маппинга на CLR-тип**. Повторная регистрация идентичного маппинга ничего не делает; регистрация *другого* макета таблицы/колонок для уже отображённого типа бросает `InvalidOperationException`. Очистите кэш (`DataContextCache.Clear()`), чтобы отобразить этот тип заново.

## Выбор провайдера

`CreateNextOrmContext` смотрит на `dbContext.Database.ProviderName` и подключает соответствующий контекст nextorm поверх EF-соединения:

| `Database.ProviderName` (точно) | Провайдер nextorm |
|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `nextorm.postgres` ([`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext)) |
| `Microsoft.EntityFrameworkCore.SqlServer` | `nextorm.sqlserver` ([`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext)) |
| `Pomelo.EntityFrameworkCore.MySql` | `nextorm.mysql` ([`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext)) |
| `MySql.EntityFrameworkCore` | `nextorm.mysql` ([`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext)) |
| `Microsoft.EntityFrameworkCore.Sqlite` | `nextorm.sqlite` ([`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext)) |

Имена провайдеров сопоставляются точно (ordinal), поэтому похожее имя отклоняется. Всё остальное — включая `Microsoft.EntityFrameworkCore.InMemory`, отсутствующее имя провайдера или EF-провайдер базы, которую nextorm не связывает (например ClickHouse или Oracle) — бросает `InvalidOperationException` до создания контекста. Оба имени MySQL-провайдера направляются в один и тот же контекст `nextorm.mysql`; MariaDB обслуживается только MySQL-провайдером Pomelo. Колбэк `configure` не может добавить провайдера; постройте контекст nextorm сами поверх EF-соединения, если вам нужен другой провайдер nextorm.

## Ограничения

- **Нет моста отслеживания изменений и `SaveChanges`.** Мост работает на соединении EF Core и, если EF открыл транзакцию, внутри неё: чтения и запись nextorm, выполненные там, участвуют в этой транзакции, поэтому `InsertInto` nextorm внутри открытой транзакции EF виден EF и коммитится или откатывается вместе с ней (проверено на PostgreSQL, SQL Server и MySQL). Это по-прежнему не мост `SaveChanges` — DML nextorm идёт в обход трекера изменений EF, поэтому записанные им строки трекеру неизвестны. Пишите через EF Core, когда нужно отслеживание; возвращённый [`IDataContext`](xref:NextORM.Core.IDataContext) используйте для запросов и для DML, который должен встраиваться в ту же транзакцию.
- **`ToNextOrm` транслирует ограниченное подмножество операторов.** Любой оператор вне [поддерживаемого набора](#поддерживаемые-операторы) — `Select`/`SelectMany`, `Include`/навигации, `Join`/`GroupJoin`, `GroupBy`, `EF.Property`/`EF.Functions`, `IgnoreQueryFilters`, `AsSplitQuery`, корни с сырым SQL, подзапросы, второй primary `OrderBy` после начала сортировки, `ThenBy` без primary и неверный порядок сортировки/`Distinct`/пагинации — бросает `NotSupportedException`. Ничего не вычисляется в памяти молча; в отличие от `Select`, который в nextorm терминальный и должен применяться после `ToNextOrm(...)`.
- **Один маппинг CLR на процесс.** Маппинг живёт в процесс-глобальном кэше метаданных nextorm с ключом по `Type`; повторная регистрация идентичного маппинга допустима, но второй `DbContext`, отображающий уже отображённый CLR-тип по-другому, бросает `InvalidOperationException` (очистите `DataContextCache.Metadata`, чтобы сбросить).
- **EF InMemory не поддерживается.** У `Microsoft.EntityFrameworkCore.InMemory` нет `DbConnection`, и его имя провайдера отклоняется как неподдерживаемое до обращения к соединению. Связываются только реляционные EF-провайдеры.
- **Неподдерживаемые формы модели отклоняются, а не отображаются молча.** Фильтры запросов, таблицы со схемой и наследование (TPH/TPT/TPC) бросают `NotSupportedException`, потому что в nextorm нет поддержки фильтров запросов, схем или дискриминаторов и иначе генерировался бы неверный SQL. Owned-типы, table splitting, shadow-свойства, конвертеры значений и temporal-таблицы также не проецируются.

## См. также

- [Транзакции](../guide/21-transactions.md)
- [Подключения](../infrastructure/02-connections.md)
- [Provider overview](../providers/overview.md)
- [API reference](api-reference.md)

---

Source: `src/nextorm.entityframeworkcore/**` (XML doc comments are the authoritative API documentation).
