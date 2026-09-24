# TODO: Интеграция с EF Core (`nextorm.entityframeworkcore`)

> Рабочий план (design RFC). Источник: [`comparison/linq2db-comparison.md:83`](../comparison/linq2db-comparison.md) —
> в linq2db есть пакет `linq2db.EntityFrameworkCore`, у nextorm интеграции нет. Дополняет
> [Transactions (enlistment, общая транзакция с EF Core / Dapper)](../../guide/25-transactions.md): без enlistment в чужую транзакцию
> совместная работа на одном соединении неполна.

## Пункт и цель

- Фича: использовать nextorm рядом с существующим EF Core `DbContext` — на том же `DbConnection`,
  в текущей транзакции EF и с маппингом сущностей, прочитанным из EF-модели (`IModel`), без
  дублирования `[Table]`/`[Column]`/fluent-маппинга.
- Поздние фазы (вне MVP): трансляция EF `IQueryable<T>` → nextorm-запрос; `UseNextOrm` на
  `DbContextOptionsBuilder` + DI; opt-in мост DML/`SaveChanges`.
- Критерий приёмки MVP:
  - `db.CreateNextOrmContext(...)` возвращает `IDataContext`, который читает через то же соединение,
    что `DbContext`;
  - если EF открыл транзакцию (`db.Database.BeginTransaction()`), nextorm-запрос видит
    незакоммиченные строки и откатывается вместе с ней;
  - SQL, построенный nextorm для типа из `db.Model`, ссылается на таблицу/колонки EF-модели (в т.ч.
    переименованные) без nextorm-атрибутов на сущности;
  - библиотека остаётся read-only: без change tracking/`SaveChanges` в MVP (фаза 4 — opt-in).
- Не-цели: миграции, `Include`/навигации, design-time, замена EF.

## Почему это нужно

1. **Паритет с linq2db.** `linq2db.EntityFrameworkCore` — заметная часть экосистемы linq2db; строка
   «EF Core integration» в `comparison/linq2db-comparison.md` — единственная крупная незакрытая
   ячейка не в пользу nextorm.
2. **Соединение уже общее.** Провайдеры принимают чужой `DbConnection`
   (`UsePostgres(DbConnection)`, `UseSqlite(DbConnection)`), т.е. nextorm умеет работать на том же
   соединении, что `DbContext`.
3. **Маппинг не дублируется.** Сейчас потребитель вынужден повторно объявлять
   `From<T>(e => e.Table("...").Property(x => x.Id).HasColumnName("..."))`, хотя всё это уже описано
   в EF-модели.
4. **Разделение ответственности.** Тяжёлый аналитический read (окна/CTE/агрегаты) остаётся за
   nextorm, а запись/трекинг/навигации — за EF Core.

## Текущее состояние и разрыв

| Слой | Где | Состояние |
|---|---|---|
| Соединение | `src/nextorm.sqlite/DI/SqliteDataContextOptionsBuilderExtensions.cs:33`, `src/nextorm.postgres/DI/PostgresDataContextOptionsBuilderExtensions.cs:33` | принимает чужой `DbConnection` — **готово** |
| Транзакция | [Transactions](../../guide/25-transactions.md) | `ITransactionManager` реализован — **блокер снят** |
| Маппинг | `src/nextorm.core/DataContext/DataContextCache.cs:22-30` | процесс-глобальный кэш по `Type`, нет per-context источника |
| Резолв источника | `src/nextorm.core/DataContext/QueryPlanner.cs:213-231`, статический `_fromCache` (`:210`) | таблица только из глобального кэша |
| Резолв колонки | `src/nextorm.core/MemberInfoExtensions.cs:22-43`, статический `_columnNames` (`:15`) | то же |
| Список колонок | `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:357`, `DataContextCache.SelectListCache` | процесс-глобальный по типу |
| EF-зависимость | `src/nextorm.core/nextorm.core.csproj`, `Directory.Packages.props` | EF Core есть только у tests/benchmarks |
| `IQueryable` | — | nextorm строит запросы fluent-билдером; `FromTableFunction` лишь *принимает* `Expression<Func<IQueryable<T>>>` как синтаксис |
| Выбор провайдера | `UsePostgres(DbConnection)` | по имени EF-провайдера (`db.Database.ProviderName`) ничего не определяется |

Следствие: «просто дать EF-модель» нельзя — нужно либо предзаполнить глобальный кэш до первого
запроса (MVP), либо ввести контекстно-локальный резолвер маппинга (фаза 2, core-рефакторинг).

## Что делает linq2db.EntityFrameworkCore (референс)

| Механизм linq2db | Назначение | Наш аналог |
|---|---|---|
| `LinqToDBForEFTools.Initialize()` | включает перехват `IQueryable` | в MVP не нужен (нет `IQueryable`); фаза 3 |
| `CreateLinqToDBConnection()` | соединение + текущая транзакция EF | `CreateNextOrmContext()` |
| `EFCoreMetadataReader` + `IModel` | маппинг из EF-модели | `NextOrmModelMapper` |
| `ToLinqToDB()` / `ToLinqToDBTable()` | EF `IQueryable`/`DbSet` → linq2db | фаза 3 |
| `ILinqToDBForEFTools` | точка расширения/кастомизации | `INextOrmEfOptions` + реестр провайдеров |
| `UseLinqToDB` | опции в `DbContextOptionsBuilder` | `UseNextOrm` (фаза 2) |

## Матрица: EF Core provider ↔ nextorm

| EF-провайдер (`Database.ProviderName`) | nextorm-пакет/контекст | Соединение | Транзакция | Маппинг из `IModel` | Заметки |
|---|---|---|---|---|---|
| `Microsoft.EntityFrameworkCore.SqlServer` | `nextorm.sqlserver` | да | да | да | `SqlServerDataContext` |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `nextorm.postgres` | да | да | да | полноценный `BEGIN`/`COMMIT` |
| `Microsoft.EntityFrameworkCore.Sqlite` | `nextorm.sqlite` | да | да | да | базовый провайдер тестов |
| `Pomelo.EntityFrameworkCore.MySql` / `MySql.EntityFrameworkCore` | `nextorm.mysql` | да | да | да | имя `ProviderName` зависит от пакета |
| `MariaDB.EntityFrameworkCore` / Pomelo-MariaDB | `nextorm.mariadb` | да | да | да | имя `ProviderName` уточнить при реализации |
| ClickHouse EF-провайдер (community) | `nextorm.clickhouse` | да | **нет** | да | HTTP-протокол, транзакций нет (nextorm `ITransactionManager` их тоже отклоняет) |
| `Microsoft.EntityFrameworkCore.InMemory` | in-memory контекст nextorm | нет | нет | да | только маппинг, соединения нет |

Источник: имена `ProviderName` — из документации соответствующего EF-провайдера; поддержка транзакций —
из провайдерной матрицы транзакций (см. [Transactions](../../guide/25-transactions.md)). Ячейка «нет» — только после проверки документации
провайдера. Полный список EF-провайдеров шире (Oracle, Firebird, DB2 …) — при отсутствии nextorm-пакета
строка добавляется по мере появления целевого провайдера.

## Дизайн

### Проект

- Новый `src/nextorm.entityframeworkcore/nextorm.entityframeworkcore.csproj`, `PackageId`
  `nextorm.entityframeworkcore`, target `net10.0`.
- Ссылки: `nextorm` (core) + `Microsoft.EntityFrameworkCore.Relational` (добавить `PackageVersion` в
  `Directory.Packages.props`, 10.0.12 — как остальные EF-пакеты).
- **Не** ссылается на провайдерные `nextorm.*`: выбор провайдера — через реестр (ниже) или явную
  конфигурацию.
- Добавить проект в `nextorm.sln`.

### Соединение и транзакция (MVP)

```csharp
public static class NextOrmDbContextExtensions
{
    public static IDataContext CreateNextOrmContext(
        this DbContext dbContext,
        Action<DataContextBuilder>? configure = null);
}
```

- `dbContext.Database.GetDbConnection()` → `DataContextBuilder.Use<Provider>(connection)`.
- После реализации `ITransactionManager`: `((ITransactionManager)ctx).UseTransaction(dbContext.Database.CurrentTransaction?.GetDbTransaction())`.
- Владение: nextorm не открывает/не закрывает чужое соединение, не коммитит/не роллбэкает
  EF-транзакцию; `Dispose` nextorm-контекста не трогает чужую транзакцию.
- Если у EF активна транзакция, а соединение ещё не открыто — открывать через EF, чтобы не сломать
  проверку провайдера (см. [Provider support](../../guide/25-transactions.md#provider-support)).

### Маппинг из `IModel` (MVP)

`NextOrmModelMapper.Map(IModel model)`:

- обходит `model.GetEntityTypes()`; для каждого — `entityType.ClrType`,
  `entityType.GetTableName()`/`GetViewName()`, `GetSchema()`;
- по свойствам: `property.GetColumnName()`, участие в ключе
  (`entityType.FindPrimaryKey()?.Properties.Contains(property)`), `property.ValueGenerated`
  (`OnAdd` → identity, `OnAddOrUpdate` → computed); пропуск shadow properties и навигаций;
- регистрирует `IEntityMetadata` в `DataContextCache.Metadata[clrType]` **до** первого запроса.

**Core-шов.** Сейчас `IEntityMetadata`/`EntityMetadata`/`PropertyMetadata` — `internal`, а публичный
`EntityMetadataBuilder<T>` — generic и требует `Expression<Func<T,object>>`. Интеграции нужен
не-генерик способ собрать метаданные по `Type`/`PropertyInfo`. Предлагается публичный не-генерик
`EntityMetadataBuilder` (ctor `(Type)`, `Property(PropertyInfo)`, `Table(string)`, `Build()`) либо
статическая фабрика `EntityMetadataFactory.Create(...)`. Это публичный API → запись в
`docs/specs/design/API-NAMING-REVIEW.md`.

**Ограничение MVP.** `DataContextCache.Metadata` ключуется только `Type`, поэтому одно сопоставление
на CLR-тип на процесс (уже действующее ограничение nextorm). Документировать. Фаза 2 — контекстно-локальный
`IEntityMetadataResolver` (`DataContext`/`QueryPlanner`/`MemberInfoExtensions`/`SelectListCache`), если
конфликт маппингов станет реальным.

### Автоопределение провайдера

- `NextOrmProviderRegistry.Register(string efProviderName, Func<DbConnection, DataContextBuilder, DataContextBuilder>)`
  — реестр заполняют либо сами провайдерные пакеты (opt-in extension, чтобы core не тянул EF), либо
  пользователь.
- `CreateNextOrmContext` без явного `configure` ищет `db.Database.ProviderName` в реестре; при промахе —
  понятный `InvalidOperationException` со списком зарегистрированных имён.
- Явный `configure` перекрывает автоопределение.

### `UseNextOrm` + DI (фаза 2)

- `DbContextOptionsBuilder.UseNextOrm(Action<DataContextBuilder>? configure)` — хранит опции в
  `IDbContextOptionsExtension`.
- `db.GetNextOrmContext()` использует сохранённые опции + connection/transaction/model.
- DI: `services.AddNextOrmFromDbContext<TDbContext>()` регистрирует scoped `IDataContext` фабрикой,
  резолвящей `TDbContext`; lifetime совпадает с EF `DbContext` (scoped).

### Фаза 3: EF `IQueryable<T>` → nextorm

- `db.Set<T>().ToNextOrm()` / `IQueryable<T>.ToNextOrm()` → nextorm-запрос.
- Подход: разобрать `IQueryable.Expression`, распознать EF-корень
  (`EntityQueryRootExpression`/`QueryRootExpression`), снять EF-специфичные узлы (`AsNoTracking`,
  `TagWith`, `IgnoreQueryFilters`), остальные операторы (`Where`/`Select`/`OrderBy`/`Join`/`GroupBy`)
  транслировать в вызовы `EntityBuilder<T>`.
- `EF.Property<T>(e, "Name")` → nextorm column access; `Include`/навигации — `NotSupportedException`.
- Реалистично — поддерживаемое подмножество; границы зафиксировать тестами (непокрытый узел бросает с
  явным сообщением).
- Альтернатива (оценить прототипом): `IQueryable<T>`-shim над `EntityBuilder<T>`, чтобы EF-LINQ-синтаксис
  работал поверх nextorm. По сути свой LINQ-provider — дорого; решать по итогам прототипа.

### Фаза 4: мост DML/`SaveChanges`

- MVP остаётся read-only. Варианты: (a) вне области (документировать); (b) opt-in: `InsertInto` nextorm
  возвращает detached-сущности, интеграция при желании `db.Attach` их (аналог
  `LinqToDBForEFTools.EnableChangeTracker`).
- Риски: fixup идентификаторов, owned types, concurrency tokens; менять трекер по умолчанию нельзя.
- Рекомендация: (b) как отдельная opt-in фаза, не в MVP.

## Публичный API (черновик)

```csharp
using Microsoft.EntityFrameworkCore;
using NextORM.Core;

public static class NextOrmDbContextExtensions
{
    public static IDataContext CreateNextOrmContext(this DbContext dbContext, Action<DataContextBuilder>? configure = null);
    public static IDataContext GetNextOrmContext(this DbContext dbContext); // фаза 2, из UseNextOrm-опций
}

public static class NextOrmProviderRegistry
{
    public static void Register(string efProviderName, Func<DbConnection, DataContextBuilder, DataContextBuilder> configure);
}

public static class NextOrmEntityFrameworkExtensions // фаза 2
{
    public static DbContextOptionsBuilder UseNextOrm(this DbContextOptionsBuilder builder, Action<DataContextBuilder>? configure = null);
    public static IServiceCollection AddNextOrmFromDbContext<TDbContext>(this IServiceCollection services) where TDbContext : DbContext;
}

// фаза 3
public static class NextOrmQueryableExtensions
{
    public static EntityBuilder<T> ToNextOrm<T>(this IQueryable<T> query);
}
```

## Ограничения и цена

- **Зависимость от `ITransactionManager`** (фаза 1) — **реализован**
  ([Transactions](../../guide/25-transactions.md)).
- **Глобальный кэш маппинга по `Type`** — один маппинг на тип на процесс (MVP); per-context резолвер —
  отдельный core-рефакторинг (фаза 2).
- **Не покрывается EF-моделью:** schema (nextorm хранит одно `TableName`), TPH/TPT/TPC, owned types /
  table splitting, shadow properties, keyless views, value converters, query filters, temporal tables.
  Перечислить в `docs/advanced/limitations.md`.
- **Нет change tracking** — by design.
- **ClickHouse** — без транзакций (см. [Transactions](../../guide/25-transactions.md)).
- **Публичный API «запирается»** — MVP добавляет расширения в новом пакете + не-генерик builder в core;
  соблюсти extend-only (`api-design` skill).
- **Hot path не трогаем** — интеграция работает на создании контекста, не в построении SQL/исполнении.

## Этапы внедрения

- **Фаза 0 (предусловие):** фаза 1 транзакций (`ITransactionManager`, `UseTransaction`,
  `cmd.Transaction`) — **реализована** ([Transactions](../../guide/25-transactions.md)).
- **Фаза 1 (MVP):** проект; `CreateNextOrmContext`; `NextOrmModelMapper` из `IModel`; enlist в
  транзакцию EF; реестр провайдеров; core-шов для метаданных; unit-тесты SQL-генерации без БД +
  интеграционный SQLite-тест (модель + соединение + транзакция) + shared-transaction на
  PostgreSQL/SQL Server/MySQL.
- **Фаза 2:** `UseNextOrm` + DI; контекстно-локальный резолвер маппинга (если нужно).
- **Фаза 3:** трансляция EF `IQueryable` (подмножество) — прототип, затем границы.
- **Фаза 4:** opt-in мост DML/трекера (или явно вне области).
- **Вне области:** миграции, design-time, `Include`/навигации, замена EF.

## План тестов

- Новый `tests/nextorm.entityframeworkcore.tests` (подключить к решению). Без БД: собрать EF-модель
  (`DbContextOptionsBuilder().UseSqlite("Data Source=:memory:")`), вызвать `CreateNextOrmContext` над
  `SqliteConnection` и проверить `query.ToSql()` — SQL должен использовать имена таблиц/колонок из
  EF-модели (в т.ч. переименованные, snake_case через `UseSnakeCaseNamingConvention`).
- Интеграция (`tests/nextorm.integration.tests`, `CommonTestSuite`): nextorm-запрос внутри
  `db.Database.BeginTransaction()` видит незакоммиченное, откатывается; провайдеры
  PostgreSQL/SQL Server/MySQL (SQLite — локально). EF SQLite уже в `Directory.Packages.props`; для
  PG/SQL Server/MySQL EF-провайдеры добавить при реализации.
- Регресс: прогоны core/sqlite/postgres без изменений; отсутствие EF-зависимости у существующих
  пакетов сохраняется.
- Coverage: `coverage.settings.xml` включает только `nextorm.{core,sqlite,postgres,sqlserver}` — новый
  пакет в цифру не войдёт; указать это явно и добавить SQL-generation тесты.

## Открытые вопросы

1. Имя пакета: `nextorm.entityframeworkcore` vs `nextorm.efcore`.
2. Маппинг: предзаполнение глобального кэша (MVP) vs контекстно-локальный резолвер (правильнее,
   core-рефакторинг) — порог, когда пора второе.
3. Schema-qualified имена: поддерживать `schema.table` (quoting?) или явно ограничить.
4. Автоопределение провайдера: единый реестр в core vs glue-пакеты
   `nextorm.<provider>.entityframeworkcore`.
5. Границы фазы 3 (какие узлы `IQueryable` поддерживаем).
6. Фаза 4: opt-in attach vs явный out-of-scope.
7. Владение соединением/транзакцией и взаимодействие с `EnsureConnectionOpen`/`Dispose`.

## Файлы к изменению

- Новое: `src/nextorm.entityframeworkcore/**`, `tests/nextorm.entityframeworkcore.tests/**`.
- Core (шов метаданных): `src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs` (+ не-генерик
  форма), возможно `src/nextorm.core/DataContext/DataContextCache.cs`.
- Предусловие: `src/nextorm.core/DataContext/Roles/ITransactionManager.cs` — **реализовано**
  ([Transactions](../../guide/25-transactions.md)).
- Сборка: `nextorm.sln`, `Directory.Packages.props` (`Microsoft.EntityFrameworkCore.Relational`), при
  необходимости `coverage.settings.xml`, `.github/workflows/dotnet.yml`.
- Документация: новый `docs/advanced/integration-efcore.md` (+ `docs/ru/advanced/...` + `toc.yml`),
  `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/comparison/linq2db-comparison.md` (+RU),
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §6, регистр
  `docs/specs/design/API-NAMING-REVIEW.md`.

## See also

- [nextorm vs linq2db: functionality comparison](../comparison/linq2db-comparison.md)
- [Transactions (enlistment, общая транзакция с EF Core / Dapper)](../../guide/25-transactions.md)
- [Capability matrix: nextorm vs EF Core и linq2db](../comparison/capability-matrix.md)
