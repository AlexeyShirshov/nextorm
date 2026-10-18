# Интеграция с Entity Framework Core

> Запускайте чтения и запись nextorm поверх соединения и транзакции существующего EF Core `DbContext`, переиспользуя маппинг из EF-модели вместо повторного его объявления.

**Предварительные требования:** [Provider overview](../connecting-providers/overview.md) · [Транзакции](../guide/23-transactions.md) · [API reference](api-reference.md)

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

Путь общего соединения/транзакции проверен end-to-end container-backed интеграционными тестами на **PostgreSQL**, **SQL Server** и **MySQL** и локальными тестами на **SQLite**. На PostgreSQL, SQL Server и MySQL публичный мост `CreateNextOrmContext()` прогоняется против живого сервера: nextorm заимствует тот самый `DbConnection`, которым владеет EF, видит незакоммиченные строки EF внутри открытой транзакции, откат EF их удаляет, `CreateInsertBuilder` nextorm внутри транзакции EF виден EF и затем коммитится или откатывается вместе с ней, а освобождение nextorm-контекста оставляет соединение EF открытым, а транзакцию EF — активной. На SQLite тесты покрывают видимость незакоммиченных строк EF и их удаление откатом EF. Проверка PostgreSQL, SQL Server и MySQL требует прогона интеграционной сюиты против поднятых контейнеров (Testcontainers); SQLite-сценарии работают без контейнера.

> **MySQL проверен через провайдер Oracle.** Поскольку у `Pomelo.EntityFrameworkCore.MySql` нет релиза под EF Core 10 (последний 9.0.0 нацелен на EF Core 9), container-backed тесты MySQL выполняются на EF Core 10 с Oracle `MySql.EntityFrameworkCore` 10.0.9 и его ADO.NET-драйвером `MySql.Data`. Мост распознаёт оба имени провайдера — `Pomelo.EntityFrameworkCore.MySql` и `MySql.EntityFrameworkCore`; чтобы обслужить второй драйвер, `nextorm.mysql` создаёт параметры через выполняемую команду (`command.CreateParameter()`), а обычный путь `MySqlConnector` остаётся без изменений. Совместная работа в транзакции EF на MySQL, таким образом, проверена, а не отложена. MariaDB обслуживается только провайдером Pomelo.

Контракт встраивания, общий с Dapper и сырым ADO.NET, описан в разделе [Транзакции](../guide/23-transactions.md).

## Отображение модели

[`NextOrmModelMapper.Register(db.Model)`](xref:NextORM.EntityFrameworkCore.NextOrmModelMapper.Register(Microsoft.EntityFrameworkCore.Metadata.IModel)) проецирует EF-модель в метаданные nextorm и вызывается автоматически из `CreateNextOrmContext`:

- **Таблица / представление** — имя таблицы (`entityType.GetTableName()`) или имя представления (`GetViewName()`) для типа, отображённого на представление.
- **Колонки** — имя колонки из `property.GetColumnName()`, поэтому переименованные колонки (`HasColumnName("selected_id")`) учитываются без nextorm-атрибута.
- **Ключ / identity / computed** — первичный ключ (`FindPrimaryKey()`), `ValueGenerated.OnAdd` (identity) и `ValueGenerated.OnAddOrUpdate` (computed) переносятся.
- **Пропускаются** — shadow-свойства (без CLR-члена), owned-типы и типы сущностей без имени таблицы/представления или без отображённых CLR-свойств.
- **Импортируются** — глобальные фильтры запросов модели (именованные/keyed и анонимные) транслируются в фильтры nextorm, поэтому scoping soft-delete и мультитенантности сохраняется (см. [Глобальные фильтры запросов из EF-модели](#глобальные-фильтры-запросов-из-ef-модели)).
- **Отклоняются** — формы модели, которые интеграция не может представить корректно и иначе отобразила бы неверно: таблица со схемой (в метаданных nextorm нет члена схемы) и любая иерархия наследования — TPH/TPT/TPC (nextorm не переносит дискриминатор). Каждая форма бросает `NotSupportedException`. Иерархию по-прежнему можно смоделировать для nextorm вручную — см. [Наследование и производные сущности](../guide/32-inheritance-and-derived-entities.md).

Поскольку кэш метаданных nextorm процесс-глобальный и ключуется по `Type` (`DataContextCache.Metadata`), **на процесс существует не более одного маппинга на CLR-тип**. Повторная регистрация идентичного маппинга ничего не делает; регистрация *другого* макета таблицы/колонок для уже отображённого типа бросает `InvalidOperationException`. Очистите кэш (`DataContextCache.Clear()`), чтобы отобразить этот тип заново. Очистка кэша также удаляет глобальные фильтры запросов, импортированные мостом; тогда контекст, привязанный к мосту, **отказывает безопасно** документированным `InvalidOperationException` на следующем запросе или сборке DML — не неотфильтрованными строками, — а восстановление состоит в создании или пере-привязке контекста моста. См. [Жизненный цикл, восстановление и ограниченная конкурентность](#жизненный-цикл-восстановление-и-ограниченная-конкурентность).

## Глобальные фильтры запросов из EF-модели

Когда интеграция с EF Core регистрирует модель `DbContext`, она импортирует глобальные фильтры запросов, объявленные в этой модели — именованные (keyed) и анонимные, — в конвейер фильтров nextorm, поэтому предикаты soft-delete и мультитенантности EF применяются и к чтениям, и к записи nextorm, выполняемым на том же соединении.

EF Core 10 хранит глобальные фильтры запросов в модели (`IQueryFilter`) отдельно от метаданных фильтров nextorm. Мост `nextorm.entityframeworkcore` закрывает этот разрыв: [`NextOrmModelMapper.Register(db.Model)`](xref:NextORM.EntityFrameworkCore.NextOrmModelMapper.Register(Microsoft.EntityFrameworkCore.Metadata.IModel)) — вызываемый автоматически из `db.CreateNextOrmContext()` — перечисляет **корневые объявленные** фильтры каждой отображённой сущности и транслирует каждый из них в фильтр nextorm.

После этого обе поверхности фильтров ведут себя одинаково: импортированный EF-фильтр подмешивается через `and` в каждый запрос, где участвует сущность, входит в план-ключ nextorm по идентичности (а не по значению) и может быть отключён для запроса той же областью `IgnoreFilters`/`IgnoreQueryFilters`. В этом разделе описано, что мост импортирует, как восстанавливается владеющий контекст во время выполнения, какие объявления отклоняются и где возможности моста заканчиваются.

### Импорт EF-фильтров

Мост читает каждый корневой фильтр один раз, при регистрации EF-модели:

- **анонимный** EF-фильтр (`Key == null`) отображается на [`QueryFilters.AnonymousKey`](xref:NextORM.Core.QueryFilters.AnonymousKey) nextorm (`""`);
- **именованный (keyed)** EF-фильтр сохраняет свой ключ и становится именованным фильтром nextorm с тем же ключом.

```csharp
public sealed class AppDbContext : DbContext
{
    public DbSet<Document> Documents => Set<Document>();

    public int TenantId { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Анонимный: аддитивен с любым другим анонимным фильтром.
        modelBuilder.Entity<Document>().HasQueryFilter(d => !d.IsDeleted);

        // Именованный (keyed): адресуется через IgnoreQueryFilters("soft-delete").
        modelBuilder.Entity<Document>().HasQueryFilter("soft-delete", d => !d.Archived);
    }
}
```

Каждый контекст nextorm, построенный из этого `DbContext`, применяет оба фильтра к `From<Document>()` и к остальным операторам, подмешивающим фильтры (соединения, подзапросы, DML):

```csharp
using var next = db.CreateNextOrmContext();

var visible = next.From<Document>().ToList(); // применены фильтры soft-delete и archived
```

Отображённый производный тип сущности наследует фильтры, объявленные на его корневом типе, а параметр сущности предиката адаптируется к корневому CLR-типу. Позиция маппера модели не меняется: отображение самой иерархии наследования (TPH/TPT/TPC) по-прежнему отклоняется (см. [Интеграция с EF Core](#ограничения)).

### Семантика живого владельца

EF-фильтр запроса — это замыкание над владеющим экземпляром `DbContext`, и EF пересчитывает его по **текущему** контексту на каждом запросе. Мост это сохраняет:

- любое чтение `DbContext` внутри фильтра (`this.TenantId`, `this.CurrentTenant.Threshold`, вложенная цепочка членов) переписывается в поиск **владельца** исполняющего контекста nextorm;
- владелец восстанавливается из живого контекста nextorm (привязанного к тому самому `DbContext`, который его создал), а не из экземпляра, который случайно построил модель;
- идентичность владельца и значения, производные от контекста, **не** входят ни в процесс-глобальные метаданные nextorm, ни в план-ключ — входит только идентичность фильтра.

Поэтому два контекста, чьи фильтры читают разные значения, разделяют один кэшированный план, и каждое выполнение перечитывает своё значение, в том числе при **прогретом/кэшированном** выполнении:

```csharp
using var east = new AppDbContext { TenantId = 1 };
using var west = new AppDbContext { TenantId = 2 };

using (var next = east.CreateNextOrmContext())
    Console.WriteLine(next.From<Document>().Count()); // east.TenantId

using (var next = west.CreateNextOrmContext())
    Console.WriteLine(next.From<Document>().Count()); // west.TenantId, та же форма плана
```

Поскольку значение читается из живого владельца, **нулевое вложенное значение владельца** (например, `CurrentTenant` равен `null`) детерминированно приводит к ошибке во время выполнения: мост выдаёт документированную ошибку владельца вместо голого null-reference, а нулевое **листовое** значение (владелец присутствует) по-прежнему связывается как `null`, и предикат сохраняется. Фильтр никогда не снимается молча.

Импортированный фильтр живёт в процесс-глобальных метаданных, поэтому может выполниться и на обычном контексте nextorm, созданном не через мост. Тогда поиск владельца **отказывает безопасно** с `InvalidOperationException`, называющей отсутствующего EF-владельца и требование моста, вместо оставления нулевого владельца. Запускайте импортированные фильтры на контексте, созданном через `CreateNextOrmContext`, `GetNextOrmContext`, `ToNextOrm` или `AddNextOrmFromDbContext`.

### Защита от захватов

Владелец определяется во время выполнения, поэтому значение, захваченное при построении модели, невозможно учесть, не заморозив неверное значение. Мост поэтому отклоняет такие объявления **при создании моста**, до публикации каких-либо метаданных:

- **захват локальной переменной замыкания** (чтение члена у сгенерированного компилятором замыкания) отклоняется;
- **захват статического члена** (доступ к члену без экземпляра, например `Seed.TenantId`) отклоняется;
- вызовы и обращения к членам `EF.Property(...)` / `EF.Functions` отклоняются;
- **фильтры с навигацией/подзапросом** (вызовы `Queryable`/`Enumerable`, вложенный корень запроса) отклоняются;
- лямбда с подписью, отличной от ровно одного параметра-сущности и тела `bool`, отклоняется;
- несовместимый тип контекста (захваченное значение `DbContext`, не присваиваемое объявленному типу контекста его доступа к члену) отклоняется;
- некорректный именованный ключ (`null`/пустой ключ/ключ из пробелов объявляет *анонимный* фильтр, а не именованный) и дублирующийся именованный ключ на одной сущности отклоняются;
- неподдерживаемый провайдер/маппинг или конфликтующие метаданные отклоняются.

Три из этих проверок — **защитные предохранители транслятора**, а не обещанные случаи воспроизведения через публичный EF: некорректная подпись лямбды и несовместимый тип контекста возникают только из вручную собранных или иных неподдерживаемых входных данных транслятора, а EF может перезаписать повторные регистрации именованных ключей до импорта модели. Поэтому отклонение дубликата ключа применяется к дубликатам, наблюдаемым во входных данных после импорта/нормализации, а не к истории регистраций, которую EF уже перезаписал.

```csharp
// Отклоняется: захват локальной переменной, зафиксированный при построении модели.
var tenant = 1;
modelBuilder.Entity<Document>().HasQueryFilter(d => d.TenantId == tenant);

// Отклоняется: захват статического члена.
modelBuilder.Entity<Document>().HasQueryFilter(d => d.TenantId == Seeds.TenantId);

// Поддерживается: литеральная скалярная константа сворачивается в bound-параметр.
modelBuilder.Entity<Document>().HasQueryFilter(d => d.Value >= 100);
```

Читайте значение через член `DbContext`, а не захватывайте его; мост переписывает такое чтение на живого владельца (см. [Семантика живого владельца](#семантика-живого-владельца)).

### Коллизии и идемпотентность

Импорт фильтра проходит те же правила коллизий, что и поверхность nextorm, но с более строгим правилом: две независимые **именованные** регистрации никогда не заменяют друг друга молча.

| Объявление | Исход |
|---|---|
| Именованный EF-фильтр против именованного фильтра nextorm с тем же ключом на одной сущности | **ошибка** — две регистрации конфликтуют |
| Два именованных EF-фильтра с тем же ключом на одной сущности | **ошибка**, даже если предикаты идентичны |
| Анонимный EF-фильтр против анонимного фильтра nextorm | **аддитивно** — оба предиката объединяются через `and` (ключ `""`) |
| Один и тот же именованный ключ на **разных** типах сущностей | допускается |
| Повтор идентичной полной регистрации моста | **идемпотентно** — по отпечатку маппинга + происхождения + нормализованных тел фильтров |
| Повтор регистрации с изменённым маппингом или телом фильтра | **ошибка** |

Отпечаток, управляющий идемпотентностью, исключает идентичность владельца и значения, производные от контекста, поэтому повторная регистрация той же модели — это no-op, а не коллизия. Правило коллизии именованного ключа применяется к различимым регистрациям, видимым в импортированной модели: повторная регистрация именованного ключа могла быть перезаписана EF до импорта, и тогда мост видит только выжившую регистрацию.

### Fail-fast и атомарная регистрация

Регистрация работает по принципу «всё или ничего». Мост сначала подготавливает всю модель, нормализованные фильтры и отпечатки регистрации, затем перепроверяет конфликты по всему подготовленному пакету и только после этого публикует новые метаданные. Привязка владельца выполняется **после** успешной публикации.

Импорт отказоустойчив (failure-atomic): неудачная публикация сохраняет прежние метаданные. Атомарность снимка для параллельных читателей не гарантируется.

Если хотя бы одна сущность или фильтр в пакете некорректны, вся регистрация отклоняется, а ранее опубликованные метаданные сохраняются — **частичного импорта нет**: вы никогда не получите часть сущностей отображёнными, а часть фильтров отброшенными. Как обычно, метаданные процесс-глобальны, и побеждает первая регистрация (см. [Глобальные фильтры запросов](query-filters.md#ограничения)), поэтому отклонённая регистрация оставляет в силе прежний маппинг и фильтры.

### Жизненный цикл, восстановление и ограниченная конкурентность

Импортированный фильтр живёт в процесс-глобальном кэше метаданных, поэтому метаданные могут исчезнуть у контекста «из-под ног». Тогда контекст, привязанный к мосту, **отказывает безопасно**: на следующем запросе или сборке DML он бросает документированный `InvalidOperationException` (в сообщении сказано, что импортированный фильтр больше не присутствует и мост отказывается выполняться без фильтра), а не возвращает неотфильтрованные строки. Это касается и команды, построенной до потери метаданных и лишь теперь отрендеренной или выполненной.

Метаданные теряются при:

- явном `DataContextCache.Clear()`, или
- скользящем вытеснении (`UseCacheSlidingExpiration`), которое удаляет запись, не прочитанную в течение её TTL, при следующем обращении.

**Восстановление** — создать или пере-привязать контекст через мост: `CreateNextOrmContext`, `GetNextOrmContext`, `ToNextOrm` или `AddNextOrmFromDbContext`. Регистрация фильтров EF происходит при создании контекста, поэтому свежий/пере-привязанный контекст заново публикует импортированные фильтры и пере-записывает ожидание; уже созданный контекст починить на месте нельзя.

Конкурентность ограничена, но не свободна: писатели метаданных сериализуются, однако атомарность снимка для параллельных читателей **не** гарантируется (см. [Fail-fast и атомарная регистрация](#fail-fast-и-атомарная-регистрация)). Не выполняйте `DataContextCache.Clear()` или импорт моста параллельно с запросами на контексте моста.

Идентичность импортированного фильтра защищают два предохранителя:

- **Идентичность параметров.** Каждое чтение `DbContext` (`this.TenantId`, `this.CurrentTenant.Threshold`, вложенная цепочка) именуется по всей host-free цепочке членов, поэтому две различные цепочки, оканчивающиеся одним и тем же членом (`…TenantId` против `…Settings.TenantId`), связываются с разными параметрами. Схлопывание, которое переиспользовало бы одно имя параметра для двух разных цепочек владельца, отклоняется до генерации любого SQL, а не связывает молча одно значение вместо другого.
- **Кэш аксессоров.** Скомпилированные аксессоры владельца принимают исполняющий контекст аргументом и ключуются host-free, поэтому кэш не удерживает живой `IDataContext`/`DbContext` (или его соединение) после завершения области вызывающего кода.

### Проброс IgnoreQueryFilters

Сконвертированный EF-запрос может отключить импортированные фильтры собственными операторами `IgnoreQueryFilters` EF; `ToNextOrm` транслирует их в query-local область фильтров nextorm:

```csharp
// Отключает все фильтры этого запроса, включая анонимные.
var all = db.Documents
    .IgnoreQueryFilters()
    .ToNextOrm(db)
    .Select(d => d.Id)
    .ToList();

// Отключает только именованный фильтр, сохраняя анонимные.
var active = db.Documents
    .IgnoreQueryFilters("soft-delete")
    .ToNextOrm(db)
    .Select(d => d.Id)
    .ToList();
```

Правила проброса:

- **беспараметрическая** форма отключает **все** фильтры, именованные и анонимные;
- **именованная** форма отключает только перечисленные ключи и **сохраняет** анонимные фильтры;
- **неизвестный** ключ не совпадает ни с чем, **пустой ключ/ключ из пробелов** никогда не выбирает анонимный фильтр, а **пустая** коллекция — no-op;
- дубликаты ключей схлопываются, **цепочечные** именованные вызовы объединяют свои ключи, а беспараметрический вызов доминирует независимо от порядка;
- область **query-local**: она применяется только к сконвертированному запросу. Она не затрагивает прямой `From<T>()` на том же контексте и не записывается в общее состояние команд `Any`/`Count`/`All` или в глобальные метаданные.

`IgnoreQueryFilters` принимается только как EF-оператор, транслируемый `ToNextOrm`. Прямой `From<T>()` использует собственные перегрузки `IgnoreFilters` nextorm (см. [Отключение фильтров для одного запроса](query-filters.md#отключение-фильтров-для-одного-запроса)).

### Поддерживаемые адаптеры и ограничения

Импорт фильтров работает везде, где мост отображает EF-модель, то есть для EF-адаптеров, которые распознаёт интеграция:

| Адаптер EF Core | Провайдер `nextorm` |
|---|---|
| `Microsoft.EntityFrameworkCore.Sqlite` | `nextorm.sqlite` |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `nextorm.postgres` |
| `Microsoft.EntityFrameworkCore.SqlServer` | `nextorm.sqlserver` |
| `Pomelo.EntityFrameworkCore.MySql` / `MySql.EntityFrameworkCore` (MySQL) | `nextorm.mysql` |

Нереляционный провайдер `Microsoft.EntityFrameworkCore.InMemory` **не** поддерживается (у него нет `DbConnection`), поэтому через него нельзя импортировать ни один фильтр. У ClickHouse сегодня нет EF Core-провайдера, который распознавал бы мост, а путь общего соединения MariaDB обслуживается только MySQL-провайдером Pomelo. Срез «ClickHouse без EF-адаптера» и сертификация общего соединения специально для MariaDB отслеживаются как открытые единицы вехи `1.0.9-b`; до их появления используйте поддерживаемые адаптеры выше.

Остальные ограничения наследуются от моста и подсистемы фильтров:

- один процесс-глобальный маппинг на CLR-тип, побеждает первая регистрация;
- таблицы со схемой и иерархии наследования по-прежнему отклоняются маппером модели;
- фильтр, читающий сырой источник `FromSql`/`From(string)`, требует `BindEntity<TEntity>` (см. [Фильтры на привязанных сырых источниках](query-filters.md#фильтры-на-привязанных-сырых-источниках));
- моста `SaveChanges`/отслеживания изменений нет; фильтр применяется к чтениям и записи nextorm, но не к собственному трекеру изменений EF.

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

- **Нет моста отслеживания изменений и `SaveChanges`.** Мост работает на соединении EF Core и, если EF открыл транзакцию, внутри неё: чтения и запись nextorm, выполненные там, участвуют в этой транзакции, поэтому `CreateInsertBuilder` nextorm внутри открытой транзакции EF виден EF и коммитится или откатывается вместе с ней (проверено на PostgreSQL, SQL Server и MySQL). Это по-прежнему не мост `SaveChanges` — DML nextorm идёт в обход трекера изменений EF, поэтому записанные им строки трекеру неизвестны. Пишите через EF Core, когда нужно отслеживание; возвращённый [`IDataContext`](xref:NextORM.Core.IDataContext) используйте для запросов и для DML, который должен встраиваться в ту же транзакцию.
- **`ToNextOrm` транслирует ограниченное подмножество операторов.** Любой оператор вне [поддерживаемого набора](#поддерживаемые-операторы) — `Select`/`SelectMany`, `Include`/навигации, `Join`/`GroupJoin`, `GroupBy`, `EF.Property`/`EF.Functions`, `AsSplitQuery`, корни с сырым SQL, подзапросы, второй primary `OrderBy` после начала сортировки, `ThenBy` без primary и неверный порядок сортировки/`Distinct`/пагинации — бросает `NotSupportedException`. Ничего не вычисляется в памяти молча; в отличие от `Select`, который в nextorm терминальный и должен применяться после `ToNextOrm(...)`. `IgnoreQueryFilters`/`IgnoreQueryFilters(keys)` **принимаются** и пробрасываются в query-local область фильтров nextorm (см. [Глобальные фильтры запросов из EF-модели](#глобальные-фильтры-запросов-из-ef-модели)).
- **Один маппинг CLR на процесс.** Маппинг живёт в процесс-глобальном кэше метаданных nextorm с ключом по `Type`; повторная регистрация идентичного маппинга допустима, но второй `DbContext`, отображающий уже отображённый CLR-тип по-другому, бросает `InvalidOperationException` (очистите `DataContextCache.Metadata`, чтобы сбросить).
- **EF InMemory не поддерживается.** У `Microsoft.EntityFrameworkCore.InMemory` нет `DbConnection`, и его имя провайдера отклоняется как неподдерживаемое до обращения к соединению. Связываются только реляционные EF-провайдеры.
- **Неподдерживаемые формы модели отклоняются, а не отображаются молча.** Таблицы со схемой и наследование (TPH/TPT/TPC) бросают `NotSupportedException`, потому что в nextorm нет поддержки схем или дискриминаторов и иначе генерировался бы неверный SQL. Глобальные фильтры запросов не отклоняются: они импортируются и применяются (см. [Глобальные фильтры запросов из EF-модели](#глобальные-фильтры-запросов-из-ef-модели)). Owned-типы, table splitting, shadow-свойства, конвертеры значений и temporal-таблицы также не проецируются.
- **Импортированные фильтры и жизненный цикл моста.** Импорт отказоустойчив (failure-atomic): отклонённая регистрация сохраняет ранее опубликованные метаданные (атомарность снимка для параллельных читателей не гарантируется). Контекст, привязанный к мосту, чьи импортированные метаданные фильтров позже удалены — через `DataContextCache.Clear()` или скользящее вытеснение, — отказывает безопасно документированным `InvalidOperationException` на следующем запросе или сборке DML вместо выполнения без фильтра; восстановление — свежий или пере-привязанный контекст моста. Импортированный фильтр — это процесс-глобальные метаданные, поэтому на контексте, созданном не через мост, поиск владельца отказывает безопасно с `InvalidOperationException`, а не просто отбрасывает фильтр. Некорректная подпись лямбды, несовместимый тип контекста и дублирующийся именованный ключ — это защитные предохранители транслятора, а не обещанные случаи воспроизведения через публичный EF: EF может перезаписать повторную регистрацию именованного ключа до импорта, поэтому проверка дубликата применяется только к дубликатам, наблюдаемым во входных данных после импорта/нормализации.

## См. также

- [Транзакции](../guide/23-transactions.md)
- [Подключения](../infrastructure/01-connections.md)
- [Provider overview](../connecting-providers/overview.md)
- [Глобальные фильтры запросов](query-filters.md)
- [API reference](api-reference.md)

---

Source: `src/nextorm.entityframeworkcore/**` (XML doc comments are the authoritative API documentation).
