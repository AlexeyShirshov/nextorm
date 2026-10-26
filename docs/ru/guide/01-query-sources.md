# Источники запроса

## Источники запроса: краткий обзор

Запрос начинается с источника, и у [`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) по одной перегрузке на каждый вид источника. Все они возвращают [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1), поэтому полный набор операторов (`Where`/`Join`/`GroupBy`/`Having`/`OrderBy`/`Limit`/`Select`) доступен независимо от того, откуда приходят строки:

| Источник | Точка входа |
|---|---|
| Отображённая сущность (её таблица из метаданных) | [`From<TEntity>()`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) |
| Имя сырой таблицы или CTE (доступ через [`TableAlias`](xref:NextORM.Core.TableAlias)) | [`From(string)`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) |
| Подзапрос / уже построенный запрос | [`From(QueryCommand<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.QueryCommand{``0})), [`From(EntityBuilder<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.EntityBuilder{``0})) |
| Сырой фрагмент SQL | [`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object)) — [Сырой SQL](14-raw-sql.md) |
| Типизированный CTE | [`From(Cte<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.Cte{``0})), [`From(CteReference<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.CteReference{``0})), объявленный через [`With`](xref:NextORM.Core.DataContextExtensions.With(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand)) / [`WithRecursive`](xref:NextORM.Core.DataContextExtensions.WithRecursive(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand,System.Nullable{System.Int32})) — [CTE](10-cte.md) |
| Ленивая временная таблица | [`From(TempTableSource<TResult>)`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,NextORM.Core.TempTableSource{``0})) — [`AsTempTable`](20-create-table-as.md) |
| Табличная функция | [`FromTableFunction<T>`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})) — [Табличные функции](13-table-valued-functions.md) |

Каждый источник также принимает колбэк `FromOptions` ([`FromOptions`](xref:NextORM.Core.FromOptions)) для модификаторов уровня источника, таких как `TABLESAMPLE`/`SAMPLE` и хинты таблиц/индексов (см. [Сэмплирование таблицы](#сэмплирование-таблицы-tablesample) ниже). Аддитивные форвардеры `CreateQueryBuilder*` зеркалят эти перегрузки один-к-одному (см. [Справочник API](../advanced/api-reference.md)). Переопределения, меняющие рендер источника уже выбранной сущности, описаны в [Переопределении источника для запроса](#переопределение-источника-для-запроса).

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

Создание производного источника и его материализация — два разных шага. `From(inner)` составляет
производную таблицу; чтобы прочитать её строки, нужен **внешний `Select`**, потому что внешний запрос
может адресовать только выходные столбцы, объявленные внутренним `SELECT`. «Голое» чтение
`From(inner).ToList()` допускается только тогда, когда внутренняя проекция уже состоит из разрешённых
ссылок на столбцы (столбец сущности, переименованный спроецированный член, вычисляемый скаляр, читаемый
по своему выходному имени). Проекция, члены которой являются выражениями над членом строки, —
анонимная `new { it.Id }` или DTO `new Dto { Id = it.Id }` — так переиспользована быть не может и
отклоняется на этапе подготовки с `QueryPreparationException`; ограничение относится к **форме
выражения** внутренней проекции, а не к CLR-типу результата. Добавьте внешний выбор
(`From(inner).Select(it => it.Id)`, как выше), чтобы прочитать такой источник. Этот вариант работает с
DTO, создаваемыми через инициализатор членов, и позиционными записями; для проверенной проекции класса,
создаваемого только через конструктор, существует ранее выявленная ошибка
(`Cannot find inner column Id`), поэтому используйте одну из этих поддерживаемых форм. См.
[Ограничения](../advanced/limitations.md#ограничения). Разделение
«построение/материализация» описано в [Подзапросах](07-subqueries.md#подзапрос-как-источник-from).

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

> Сгенерированное имя выходного столбца в примере (`c0`) — это то, как отрендеренный SQL
> экспонирует вычисляемый столбец; публичным соглашением об именовании оно не является. Читайте
> значение через построитель (как выше), а не по жёстко записанному написанию, которое зависит от
> провайдера и может измениться.

## Табличная функция в качестве источника

`dataContext.FromTableFunction(() => ...)` использует табличную функцию как источник `FROM`. Встроенные
помощники покрывают распространённые функции, возвращающие набор; `SqlFunctions.Postgres.unnest`
разворачивает массив PostgreSQL в одну строку на элемент:

```csharp
var elements = dataContext
    .FromTableFunction(() => SqlFunctions.Postgres.unnest(SqlFunctions.Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

```sql
select unnest as "Value"
 from unnest(@norm_p0) as "t1"
```

Если полные имена `SqlFunctions.*` кажутся слишком громоздкими, импортируйте поверхность через
`using static NextORM.Core.SqlFunctions;` и опустите префикс класса:

```csharp
using static NextORM.Core.SqlFunctions;

var elements = dataContext
    .FromTableFunction(() => Postgres.unnest(Parameter<long[]>(0)))
    .Select(r => r.Value)
    .ToList(new long[] { 1, 2, 3 });
```

`SqlFunctions.Postgres.generate_series(start, stop)` аналогично генерирует числовую последовательность.
Встроенные помощники включаются провайдером ([`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String))), а
пользовательская функция объявляется через `[SqlTableFunction]`; см.
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
меняют рендер источника **только для этого запроса**; маппинг и остальные запросы не затрагиваются:

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

Квалификаторы schema/database/server зависят от провайдера; недоступные провайдеру уровни
отклоняются `NotSupportedException` при построении SQL:

| Провайдер | `WithSchema` | `WithDatabase` | `WithServer` |
|---|---|---|---|
| PostgreSQL | `schema.table` | отклоняется | отклоняется |
| SQL Server | `schema.table` | `database.schema.table` | `server.database.schema.table` |
| MySQL / MariaDB | `db.table` (schema = database) | `db.table` | отклоняется |
| SQLite | `db.table` (attached-база) | `db.table` | отклоняется |
| ClickHouse | `db.table` | `db.table` | отклоняется |
| In-memory | отклоняется | отклоняется | отклоняется |

В MySQL, MariaDB, ClickHouse и SQLite схема и имя базы — это один и тот же единственный квалификатор,
поэтому можно задать только одно из двух. Части квотируются разделителем провайдера, когда включено
квотирование идентификаторов (см. [`WithQuotedIdentifiers`](xref:NextORM.Core.EntityBuilder`1.WithQuotedIdentifiers(System.Boolean))),
каждая часть отдельно: `[srv].[db].[sales].[orders]`, `` `db`.`orders` ``, `"sales"."orders"`.

[`WithTableExpression`](xref:NextORM.Core.EntityBuilder`1.WithTableExpression(System.String)) заменяет доступ к таблице сырым SQL,
отрендеренным как derived-источник (`(sql) AS alias`), и не сочетается с квалификаторами имени.
Фрагмент подставляется дословно, поэтому передавайте только доверенный SQL (ответственность за
корректность и инъекции — на вызывающем, как и у `WithSql`):

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

Переопределение входит в ключ кэша планов, поэтому два запроса, различающиеся только им, не делят
кэшированный план; без переопределения генерируемый SQL не меняется. У in-memory-провайдера нет SQL,
который можно переписать, поэтому он отклоняет любое переопределение.

