# Источники запроса

Эта страница поможет выбрать источник строк для запроса. Обычно запрос начинается с отображённой
сущности: `From<TEntity>()` выбирает её таблицу из маппинга, `Where` фильтрует строки, а `Select`
определяет, какие значения нужно прочитать. Здесь описан выбор источника и его параметры; сведения об
отдельных операторах приведены в связанных руководствах.

## Начните с отображённой сущности

Для обычного запроса к таблице начните с `From<TEntity>()`, а затем добавьте нужные операторы:

```csharp
var rows = dataContext.From<SimpleEntity>()
    .Where(it => it.Id > 8)
    .Select(it => new { it.Id })
    .ToList();
```

`Where` оставляет строки, в которых `Id` больше 8, а `Select` включает в результат только значение
`Id`. `ToList` выполняет запрос и возвращает список этих значений.

Используйте другую перегрузку `From`, если строки поступают из подзапроса, сырого SQL, общего табличного
выражения (CTE), временной таблицы или табличной функции.

## Предоставьте типизированные источники через контекст приложения

Чтобы предоставить именованные источники сущностей, например `app.Orders`, и использовать
их в `From` или `Join`, см. [«Репозитории и контекст приложения»](37-repositories.md).

## Источники запроса: краткий обзор

У [`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) есть перегрузка для каждого вида источника. Все они возвращают [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1), поэтому с любым источником можно использовать один и тот же набор основных операторов (`Where`, `Join`, `GroupBy`, `Having`, `OrderBy`, `Limit` и `Select`) с учётом ограничений источника и провайдера:

| Источник | Точка входа |
|---|---|
| Отображённая сущность (её таблица из метаданных) | [`From<TEntity>()`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) |
| Имя сырой таблицы или CTE (доступ через [`TableAlias`](xref:NextORM.Core.TableAlias)) | [`From(string)`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) |
| Подзапрос / уже построенный запрос | [`From(QueryCommand<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.QueryCommand{``0})), [`From(EntityBuilder<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.EntityBuilder{``0})) |
| Сырой фрагмент SQL | [`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object)) — [Сырой SQL](14-raw-sql.md) |
| Типизированный CTE | [`From(Cte<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.Cte{``0})), [`From(CteReference<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.CteReference{``0})), объявленный через [`With`](xref:NextORM.Core.DataContextExtensions.With(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand)) / [`WithRecursive`](xref:NextORM.Core.DataContextExtensions.WithRecursive(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand,System.Nullable{System.Int32})) — [CTE](10-cte.md) |
| Ленивая временная таблица | [`From(TempTableSource<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.TempTableSource{``0})) — [`AsTempTable`](20-create-table-as.md) |
| Табличная функция (runtime-фабрика) | [`From(() => ...)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{NextORM.Core.EntityBuilder{``0}}})) — [Табличные функции](13-table-valued-functions.md) |

Каждый источник также принимает колбэк `FromOptions` ([`FromOptions`](xref:NextORM.Core.FromOptions)) для своих модификаторов, например `TABLESAMPLE`/`SAMPLE` и хинтов таблиц или индексов. Пример приведён в разделе [Сэмплирование таблицы](#сэмплирование-таблицы-tablesample). Методы `CreateQueryBuilder*` предоставляют соответствующие точки входа; их сигнатуры см. в [справочнике API](../advanced/api-reference.md). Чтобы изменить таблицу или SQL-источник только для одного запроса, см. раздел [Переопределение источника для запроса](#переопределение-источника-для-запроса).

## Подзапрос в качестве источника

`From(query)` также оборачивает отфильтрованный запрос, что соответствует подзапросу в SQL `FROM`:

```csharp
var inner = dataContext.From<SimpleEntity>()
    .Where(it => it.Id > 8)
    .Select(it => new { it.Id });

var rows = await dataContext.From(inner)
    .Select(it => new { it.Id })
    .ToListAsync();
```

```sql
-- SQLite
select id
 from (select id
 from simple_entity where (id > 8))
```

Вывод:

| Id |
|----|
| 9 |
| 10 |

Использование запроса как источника состоит из двух шагов. `From(inner)` включает внутренний запрос
как производную таблицу, но не читает строки. Добавьте внешний `Select`, чтобы выбрать столбцы,
объявленные внутренней проекцией, как в примере выше.

Вызов `From(inner).ToList()` без внешнего `Select` допускается, только если внутренняя проекция уже
содержит разрешённые ссылки на столбцы: столбец сущности, переименованный член проекции или
вычисляемый скаляр, читаемый по выходному имени. Проекция вида `new { it.Id }` или
`new Dto { Id = it.Id }` содержит выражения над членами строки и отклоняется при подготовке запроса
через `QueryPreparationException`. Ограничение зависит от **формы выражения** внутренней проекции, а не
от CLR-типа результата.

Вариант с внешним `Select` работает с DTO, создаваемыми через инициализатор членов, и позиционными
записями. Для проверенной проекции класса, создаваемого только через конструктор, известна ранее
обнаруженная ошибка (`Cannot find inner column Id`), поэтому используйте одну из поддерживаемых форм.
См. [Ограничения](../advanced/limitations.md#ограничения). Разделение «построение/материализация»
описано в [Подзапросах](07-subqueries.md#подзапрос-как-источник-from).

Производный источник может также проецировать вычисляемый скаляр вместо именованного члена.
Внешний запрос обращается к этому значению по выходному столбцу, который объявляет производный
список выборки, поэтому его можно сортировать, фильтровать и проецировать как любой другой столбец:

```csharp
var inner = dataContext.From<SimpleEntity>()
    .Where(it => it.Id <= 2)
    .Select(it => it.Id * 2);

var rows = dataContext.From(inner)
    .OrderBy(x => x)
    .Select(x => x * 3)
    .ToList();
```

```sql
-- SQLite
select (c0 * 3)
 from (select (id * 2) as 'c0'
 from simple_entity
 where (id <= 2)) as 't1'
 order by t1.c0
```

Вывод: `6`, `12`.

> В сгенерированном SQL вычисляемый столбец получает имя `c0`. Это технический псевдоним, а не часть
> публичного API. Читайте значение через построитель (как выше), а не по псевдониму: его написание
> зависит от провайдера и может измениться.

## Табличная функция в качестве источника

`dataContext.From(() => ...)` использует табличную функцию (TVF) — функцию базы данных, которая
возвращает строки, — как источник `FROM`. Лямбда содержит вызов функции и её аргументы (см.
[Табличные функции](13-table-valued-functions.md)). Встроенные
помощники покрывают распространённые функции, возвращающие набор; `SqlFunctions.Postgres.unnest`
разворачивает массив PostgreSQL в одну строку на элемент:

```csharp
var elements = dataContext
    .From(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

```sql
select unnest as "Value"
 from unnest(@norm_p0) as "t1"
```

Если полные имена `SqlFunctions.*` кажутся слишком громоздкими, подключите статические члены типа
через `using static NextORM.Core.SqlFunctions;` и опустите префикс класса:

```csharp
using static NextORM.Core.SqlFunctions;

var elements = dataContext
    .From(() => Postgres.unnest(Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

`SqlFunctions.Postgres.generate_series(start, stop)` аналогично генерирует числовую последовательность.
Встроенные помощники включаются провайдером ([`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String))), а
пользовательская функция объявляется как фабрика `TvfEntityBuilder<T>`; см.
[Табличные функции](13-table-valued-functions.md).

## Сэмплирование таблицы (`TABLESAMPLE`)

[`FromOptions.TableSample`](xref:NextORM.Core.FromOptions.TableSample(System.Double,NextORM.Core.TableSampleMethod,System.Nullable{System.Double})) добавляет модификатор `TABLESAMPLE` к
основной таблице, поэтому база читает только процент её строк вместо полного сканирования таблицы.
Сэмплирование — это опция источника на время запроса, поэтому она задаётся в вызове `From`. Процент
должен находиться в диапазоне `(0, 100]`; метод сэмплирования по умолчанию —
[`TableSampleMethod.System`](xref:NextORM.Core.TableSampleMethod.System), а необязательное зерно (seed)
делает выборку повторяемой:

```csharp
var rows = await dataContext.From<SimpleEntity>(o => o.TableSample(10, TableSampleMethod.System, seed: 42))
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
-- PostgreSQL
select id
 from simple_entity tablesample system (10) repeatable (42)

-- SQL Server
select id
 from simple_entity tablesample (10 percent) repeatable (42)
```

PostgreSQL поддерживает и `System`, и [`Bernoulli`](xref:NextORM.Core.TableSampleMethod.Bernoulli);
SQL Server поддерживает только `System`. Все остальные провайдеры выбрасывают `NotSupportedException`
при построении SQL ([`TableSample`](xref:NextORM.Core.ISqlDialect.TableSample) и
[`ITableSampleMethods.Render`](xref:NextORM.Core.ITableSampleMethods.Render(NextORM.Core.TableSampleMethod,System.Double,System.Nullable{System.Double},NextORM.Core.KeywordCase))). Модификатор применяется только к
основной таблице запроса.

## Переопределение источника для запроса

По умолчанию запрос читает таблицу, заданную в маппинге (`EntityMetadataBuilder<T>.Table(...)`) или
переданную в `From("table")`. Методы `With*` у [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1)
меняют таблицу или SQL-выражение, из которого читает этот запрос; маппинг и остальные запросы не
затрагиваются:

```csharp
public EntityBuilder<TEntity> WithTableName(string name);
public EntityBuilder<TEntity> WithSchema(string schema);
public EntityBuilder<TEntity> WithDatabase(string database);
public EntityBuilder<TEntity> WithServer(string server);
public EntityBuilder<TEntity> WithTableExpression(string sql);
```

```csharp
var rows = await dataContext.From<IOrder>()
    .WithSchema("sales")
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
-- SQL Server / PostgreSQL
select id
 from sales.orders
```

Провайдер определяет, какие префиксы схемы, базы данных и сервера он поддерживает. При построении SQL
генерируется `NotSupportedException`, если провайдер не поддерживает такой уровень:

| Провайдер | `WithSchema` | `WithDatabase` | `WithServer` |
|---|---|---|---|
| PostgreSQL | `schema.table` | отклоняется | отклоняется |
| SQL Server | `schema.table` | `database.schema.table` | `server.database.schema.table` |
| MySQL / MariaDB | `db.table` (schema = database) | `db.table` | отклоняется |
| SQLite | `db.table` (присоединённая база данных) | `db.table` | отклоняется |
| ClickHouse | `db.table` | `db.table` | отклоняется |
| In-memory | отклоняется | отклоняется | отклоняется |

В MySQL, MariaDB, ClickHouse и SQLite имена схемы и базы данных используют один и тот же префикс,
поэтому можно задать только одно из них. Если включено оформление идентификаторов в кавычки или
скобки (см. [`WithQuotedIdentifiers`](xref:NextORM.Core.EntityBuilder`1.WithQuotedIdentifiers(System.Boolean))), провайдер
обрамляет каждую часть имени отдельно: `[srv].[db].[sales].[orders]`, `` `db`.`orders` ``, `"sales"."orders"`.

[`WithTableExpression`](xref:NextORM.Core.EntityBuilder`1.WithTableExpression(System.String)) оборачивает сырой SQL-фрагмент в
производный источник (`(sql) AS alias`) и не сочетается с отдельно заданными именами схемы, базы и
сервера. Фрагмент подставляется дословно, поэтому передавайте только доверенный SQL. Как и для
`WithSql`, вы отвечаете за корректность и безопасность от SQL-инъекций:

```csharp
var rows = await dataContext.From<IOrder>()
    .WithTableExpression("select id from orders_2025 union all select id from orders_2026")
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
-- PostgreSQL
select id
 from (select id from orders_2025 union all select id from orders_2026) as "t1"
```

Переопределение входит в ключ кэша планов запросов, поэтому два запроса, различающиеся только им, не
используют общий кэшированный план. Без переопределения генерируемый SQL не меняется. У
in-memory-провайдера нет SQL, который можно переписать, поэтому он отклоняет любое переопределение.

