# Запросы и проекции

> Формируйте результат запроса с помощью [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})): одна колонка, анонимный тип, DTO или запись (record), кортеж, инициализатор членов, вложенная сущность или вычисляемая колонка.

**Предварительные требования:** [Быстрый старт](../getting-started/02-quickstart.md) · [Сущности и метаданные](../getting-started/03-entities-and-metadata.md)

## Обзор

`dataContext.From<TEntity>()` возвращает [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1). Каждый запрос начинается с
проецирования этой сущности с помощью [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})):

```csharp
public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
```

Лямбда не выполняется - она транслируется в список `SELECT` генерируемого оператора.
[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) возвращает [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1); терминальный метод ([`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])),
[`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`AnyAsync`](xref:NextORM.Core.EntityBuilderExtensions.AnyAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), ...) выполняет его. См. [Сортировку и постраничную выборку](05-sorting-and-paging.md)
для терминальных методов и их асинхронных форм.

Правила, применимые к любой проекции:

* Форма лямбды определяет список выборки. Колонки, которые не проецируются, не читаются.
* Проецируемый член, имя которого совпадает с именем отображаемой колонки (без учёта регистра),
  генерируется без псевдонима. Переименованный или вычисляемый член получает псевдоним: `as 'Calc'` в
  SQLite, `as [Calc]` в SQL Server, `as "Calc"` в PostgreSQL.
* Аргументы конструктора, элементы кортежа и присваивания в инициализаторе членов отображаются в
  элементы списка выборки; материализатор времени выполнения строит объект из reader'а.
* В in-memory провайдере SQL не существует; то же самое выражение компилируется и выполняется над
  набором данных в памяти.

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных
тестов: `simple_entity` содержит идентификаторы `1`-`10`, а `complex_entity` — три строки
(`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

## Анонимный тип

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { entity.Id })
    .ToListAsync();
```

```sql
select id from simple_entity
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

`entity.Id` отображается на колонку `id` таблицы `simple_entity`, поэтому псевдоним не генерируется.

## Изменённые и вычисляемые колонки

Член может вычисляться из других колонок или констант:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { Id = entity.Id + 1 })
    .ToListAsync();
```

```sql
select (id + 1) as 'Id' from simple_entity
```

Вывод:

| Id |
|----|
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |
| 11 |

Арифметическое выражение заключается в скобки и, поскольку оно не является обычной колонкой, получает
псевдоним с именем проецируемого члена. Именование члена делает псевдоним стабильным для объемлющего
запроса:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Select(it => new { it.Id, Calc = it.Id + 1 })
    .ToListAsync();
```

```sql
select id, (id + 1) as 'Calc' from complex_entity
```

Строковые члены конкатенируются оператором конкатенации провайдера (`||` в SQLite и PostgreSQL, `+` в
SQL Server):

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Select(it => new { it.Id, Display = it.String + "/" + it.RequiredString })
    .ToListAsync();
```

```sql
-- SQLite
select id, ((somestring || '/') || requiredstring) as 'Display' from complex_entity
```

Вывод:

| Id | Display |
|----|---------|
| 1 | dadfasd/sdf |
| 2 | xxx/asdfgoi |
| 3 | null |

## Колонки по имени

Mapped-сущность раскрывает только объявленные в ней колонки. Колонка без свойства — например, в
широкой таблице ClickHouse — проецируется через
[`SqlFunctions.Column<T>`](xref:NextORM.Core.SqlFunctions.Column``1(System.Object,System.String)):

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { Region = SqlFunctions.Column<ulong>(entity, "region_id") })
    .ToListAsync();
```

```sql
-- SQLite
select region_id as 'Region' from simple_entity
```

Первым аргументом должен быть параметр лямбды запроса (источник); имя сверяется с именем колонки в
базе дословно, поэтому кавычки зависят от провайдера (`` `region_id` `` в ClickHouse и MySQL,
`"region_id"` в PostgreSQL и SQLite, `[region_id]` в SQL Server). Значение материализуется как `T`,
поэтому тип должен поддерживаться row reader. В отличие от mapped-члена, колонка не сверяется с
метаданными сущности: опечатка в имени проявится на базе.

Тот же доступ работает в предикате и на join-проекции:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Join(dataContext.From<ComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => SqlFunctions.Column<long>(p.Item2, "region_id") > 0)
    .Select(p => new { p.Item1.Id, Region = SqlFunctions.Column<long>(p.Item2, "region_id") })
    .ToListAsync();
```

Для источника вообще без типа сущности (`From("table")`) колонки читаются через
[`TableAlias`](xref:NextORM.Core.TableAlias) — см. [Joins](03-joins.md) и [CTE](09-cte.md).
In-memory-провайдер не имеет понятия имени колонки и отклоняет `SqlFunctions.Column`.

## DTO

Неанонимный тип проецируется через свой конструктор:

```csharp
public class SimpleEntityDto(int id)
{
    public int Id { get; } = id;
}

var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new SimpleEntityDto(entity.Id))
    .ToListAsync();
```

```sql
select id from simple_entity
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

## Запись (record)

Позиционные записи также проецируются через свой конструктор:

```csharp
public record SimpleEntityRecord(long Id);

var rows = await dataContext.From("simple_entity")
    .Select(tbl => new SimpleEntityRecord(tbl.GetInt64("id")))
    .ToListAsync();
```

```sql
select id from simple_entity
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

`tbl.GetInt64("id")` уже имеет тип члена записи, поэтому преобразование не добавляется. Когда проецируемый
тип шире или уже исходной колонки, nextorm отображает преобразование как `cast(...)`.

## Кортеж

```csharp
var rows = await dataContext.From("simple_entity")
    .Select(tbl => new Tuple<long>(tbl.GetInt64("id")))
    .ToListAsync();
```

```sql
select id from simple_entity
```

Вывод:

| Item1 |
|-------|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

## Инициализатор членов

Вместо конструктора проекция может использовать инициализатор объекта:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .ToListAsync();
```

```sql
select id from simple_entity
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

## Примитивная и скалярная проекция

[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) может возвращать одно значение вместо объекта строки:

```csharp
var ids = await dataContext.From<SimpleEntity>()
    .Where(it => it.Id < 5)
    .Select(it => it.Id)
    .ToListAsync();
```

```sql
select id from simple_entity where (id < 5)
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |

Логический член работает так же:

```csharp
var flags = await dataContext.From<ComplexEntity>()
    .Where(it => it.Boolean == true)
    .Select(it => it.Boolean)
    .ToListAsync();
```

```sql
select b from complex_entity where b = 1
```

Вывод:

| Boolean |
|---------|
| true |

## Вложенная сущность и вычисляемые колонки поверх проекции

[`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) сам может использоваться как источник другого запроса с помощью
`dataContext.From(query)`, поэтому внутреннюю проекцию (включая вычисляемые колонки) можно прочитать и
спроецировать снова:

```csharp
var inner = dataContext.From<ComplexEntity>()
    .Select(it => new { it.Id, Calc = it.String + it.String });

var rows = await dataContext.From(inner)
    .Select(t => new { t.Id, t.Calc })
    .ToListAsync();
```

```sql
-- SQLite: no derived-table alias is required
select id, Calc from (select id, (somestring || somestring) as 'Calc' from complex_entity)
```

SQL Server требует, чтобы производная таблица имела псевдоним, и PostgreSQL аналогично:

```sql
-- SQL Server
select id, Calc from (select id, (somestring + somestring) as [Calc] from complex_entity) as [t1]
```

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
select id from (select id from simple_entity where (id > 8))
```

Вывод:

| Id |
|----|
| 9 |
| 10 |

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
select unnest as "Value" from unnest(@norm_p0) as "t1"
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
select id from simple_entity tablesample system (10) repeatable (42)

-- SQL Server
select id from simple_entity tablesample (10 percent) repeatable (42)
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
select id from sales.orders
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
select id from (select id from orders_2025 union all select id from orders_2026) as "t1"
```

Переопределение входит в ключ кэша планов, поэтому два запроса, различающиеся только им, не делят
кэшированный план; без переопределения генерируемый SQL не меняется. У in-memory-провайдера нет SQL,
который можно переписать, поэтому он отклоняет любое переопределение.

## JSON-вывод (SQL Server)

[`ForJson`](xref:NextORM.Core.QueryCommand`1.ForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean,System.Object[])) — **терминальный оператор**: он выполняет запрос и возвращает
весь набор результатов одним JSON-документом ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)). Как терминал он не
добавляет неявный `TOP 1`, поэтому документ покрывает все строки; тип элемента запроса не важен,
так как база возвращает одну колонку-документ:

```csharp
string? json = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id, e.String })
    .ForJson(ForJsonMode.Path, root: "items", includeNullValues: true);
```

```sql
select id, somestring from complex_entity for json path, root('items'), include_null_values
```

[`Path`](xref:NextORM.Core.ForJsonMode.Path) строит документ по псевдонимам проекции, [`Auto`](xref:NextORM.Core.ForJsonMode.Auto) — по структуре
таблицы. `ForJson` возвращает `null`, если запрос не вернул строк (SQL Server отдаёт SQL NULL для
пустого результата `FOR JSON`). Предложение ставится после `ORDER BY` и перед завершающим
`OPTION (...)`; остальные провайдеры выбрасывают `NotSupportedException`. Используйте
[`WithForJson`](xref:NextORM.Core.QueryCommand`1.WithForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean)), чтобы только *присоединить* предложение и сохранить команду
композируемой (для дальнейших хинтов или просмотра SQL).

## XML-вывод (SQL Server)

[`ForXml`](xref:NextORM.Core.QueryCommand`1.ForXml(NextORM.Core.ForXmlMode,System.String,System.String,System.Boolean,System.Object[])) — XML-аналог и терминал ([`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml)); поддерживаются
`RAW`, `AUTO`, `EXPLICIT` и `PATH`, с необязательным именем элемента строки, обёрткой `ROOT('...')` и
флагом `ELEMENTS`:

```csharp
string? xml = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id })
    .ForXml(ForXmlMode.Raw, elementName: "row", root: "items", elements: true);
```

```sql
select id from complex_entity for xml raw('row'), root('items'), elements
```

Как и `ForJson`, `ForXml` возвращает `null` для пустого набора, а
[`WithForXml`](xref:NextORM.Core.QueryCommand`1.WithForXml(NextORM.Core.ForXmlMode,System.String,System.String,System.Boolean)) присоединяет предложение без выполнения. `FOR JSON` и `FOR XML` взаимно исключают
друг друга; их сочетание выбрасывает `NotSupportedException`.

## Блокировка строк (`FOR UPDATE` / `FOR SHARE`)

[`ForUpdate`](xref:NextORM.Core.EntityBuilder`1.ForUpdate) и
[`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare) блокируют выбранные строки до конца
окружающей транзакции. PostgreSQL, MySQL и MariaDB генерируют завершающее предложение, которое
ставится последним — после `WHERE`, `ORDER BY` и запроса страницы; SQL Server вместо этого
привязывает табличный хинт `WITH (updlock)`/`WITH (holdlock)` к основной таблице:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Where(x => x.Id > 5)
    .ForUpdate()
    .ToListAsync();
```

```sql
-- PostgreSQL / MySQL / MariaDB
select id from simple_entity where (id > 5) for update

-- SQL Server
select id from simple_entity with (updlock) where (id > 5)
```

`ForUpdate()` блокирует строки монопольно; [`ForShare`](xref:NextORM.Core.EntityBuilder`1.ForShare)
берёт разделяемую блокировку — PostgreSQL генерирует `for share`, MySQL/MariaDB генерируют
`lock in share mode`, а SQL Server — `holdlock` (разделяемая) против `updlock` для `ForUpdate`.
Предложение реализовано в PostgreSQL, MySQL, MariaDB и SQL Server
([`Lock`](xref:NextORM.Core.ISqlDialect.Lock); в SQL Server — через
[`ILockRenderer.UsesTableHints`](xref:NextORM.Core.ILockRenderer.UsesTableHints));
все остальные провайдеры выбрасывают `NotSupportedException` при построении SQL.

Режим ожидания задаётся [`LockWaitMode`](xref:NextORM.Core.LockWaitMode): если строку уже удерживает
другая транзакция, [`NoWait`](xref:NextORM.Core.LockWaitMode.NoWait) падает немедленно вместо
ожидания, а [`SkipLocked`](xref:NextORM.Core.LockWaitMode.SkipLocked) исключает занятые строки из
результата — стандартный приём для очередей и пулов воркеров:

```csharp
var claimed = await dataContext.From<Job>()
    .Where(x => x.State == "pending")
    .ForUpdate(LockWaitMode.SkipLocked)
    .ToListAsync();
```

```sql
-- PostgreSQL / MySQL / MariaDB
select id from job where (state = 'pending') for update skip locked

-- SQL Server (READPAST приближает SKIP LOCKED)
select id from job with (updlock, readpast) where (state = 'pending')
```

PostgreSQL и MySQL дописывают `nowait`/`skip locked` в конец
(`FOR UPDATE`/`FOR SHARE [NOWAIT | SKIP LOCKED]`); разделяемая блокировка с режимом переключает MySQL
с `lock in share mode` на `for share`, потому что `LOCK IN SHARE MODE` не принимает lock-option.
MariaDB дописывает режим и к `for update`, и к `lock in share mode` (`NOWAIT` с 10.3+, `SKIP LOCKED`
с 10.6+). SQL Server добавляет `nowait` или `readpast` в тот же табличный хинт
(`with (updlock, nowait)` / `with (updlock, readpast)`); `readpast` пропускает любую заблокированную
строку, а не только строку, удержанную другим писателем, поэтому он приближает, а не в точности
повторяет `SKIP LOCKED`. По умолчанию [`Wait`](xref:NextORM.Core.LockWaitMode.Wait) сохраняет
блокирующее поведение.

## Различия провайдеров

| Провайдер | Поведение |
|---|---|
| SQLite | Псевдонимы колонок заключаются в одинарные кавычки (`as 'Calc'`); производные таблицы не требуют псевдонима. |
| SQL Server | Псевдонимы колонок заключаются в квадратные скобки (`as [Calc]`); каждая производная таблица должна иметь псевдоним (`as [t1]`). Конкатенация строк использует `+`. |
| PostgreSQL | Псевдонимы колонок заключаются в двойные кавычки (`as "Calc"`); производные таблицы должны иметь псевдоним (`as "t1"`). |
| MySQL | Псевдонимы колонок заключаются в обратные кавычки (`` as `Calc` ``); производные таблицы должны иметь псевдоним (`` as `t1` ``). Конкатенация строк использует `concat(a, b)`. |
| MariaDB | То же, что MySQL: обратные кавычки для псевдонимов, обязательный псевдоним производной таблицы и конкатенация через `concat(a, b)`. |
| ClickHouse | Псевдонимы колонок заключаются в обратные кавычки (`` as `Calc` ``); производные таблицы должны иметь псевдоним (`` as `t1` ``). Конкатенация строк использует `concat(a, b)`. |
| In-memory | SQL не генерируется; делегаты проекции компилируются и выполняются над объектами в памяти. |

## См. также

* [Фильтрация (WHERE)](02-filtering-where.md)
* [Сортировка и постраничная выборка](05-sorting-and-paging.md)
* [Табличные функции](13-table-valued-functions.md)
* [Сущности и метаданные](../getting-started/03-entities-and-metadata.md)
* [Обзор провайдеров](../providers/overview.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:20`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:26`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:45`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:75`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:94`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:103`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:112`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:122`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:352`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:367`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:382`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:727`;
`tests/nextorm.integration.tests/TestModels.cs:3`;
`tests/nextorm.integration.tests/TestModels.cs:12`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:111`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:217`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:242`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:255`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:268`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:293`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:223`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:248`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1117`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1132`.
