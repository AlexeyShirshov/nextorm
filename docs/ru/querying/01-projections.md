# Проекции

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
[`TableAlias`](xref:NextORM.Core.TableAlias) — см. [Joins](../guide/02-joins.md) и [CTE](../guide/08-cte.md).
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

Когда эта единственная колонка — `byte[]` или `string`, её можно прочитать как поток, а не материализовать: `ToStream`/`ToTextReader` (и их асинхронные формы) открывают LOB-аксессоры провайдера для этой одной колонки — см. [Потоковое чтение больших объектов](../guide/26-large-objects.md).

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

## Именованные псевдонимы соединений

Соединённый запрос может именовать свои слоты вместо позиционной адресации, и два стиля можно
смешивать в одной цепочке:

```csharp
var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .Join<Person>(people, (o, buyer) => o.BuyerId == buyer.Id, Alias.Buyer)
    .Select(p => new { Buyer = p.Buyer.Id })
    .ToListAsync();
```

`p.Buyer` — **тот же слот**, что и позиционный `p.Item2`, поэтому `p.Buyer.Id` можно использовать
везде, где использовался бы позиционный `p.Item2.Id` — в `Select`, `Where` и последующих
соединениях. Члены-псевдонимы **существуют только для выражений**: они нужны, чтобы дерево
выражений `Select`/`Where` могло назвать соединённую таблицу, а чтение такого члена вне дерева
выражений (например, прямая материализация `p.Buyer`) бросает `NotSupportedException`; позиционные
члены `ItemN` остаются обычными свойствами. Генератор эмитит одну `public`-пару
проекция/построитель на каждую использованную схему слотов со slot-encoded именами (`P{slot}` для
позиционного шага, `A{slot}_{имя}` для шага с псевдонимом), поэтому одни и те же имена псевдонимов
при разном позиционном размещении дают другой тип — например
`AliasProjection_P1_A2_Buyer_A3_Approver<Order, Person, Person>`. Цепочка ограничена восемью слотами;
`As<T>` снимает потолок точно так же, как для позиционных соединений. См.
[Соединения](../guide/02-joins.md#именованные-псевдонимы-соединений).

### Именование корневого источника

Корневой источник тоже можно именовать через `.WithAlias(Alias.<Name>)`: это привязывает слот 1 и
даёт проекцию размерности 1 (`Projection<T1>` с сохранённым `Item1` плюс именованный член). Вызов
root-only и доступен только на SQL-провайдерах; провайдер in-memory падает закрыто с
`NotSupportedException`.

```csharp
var rooted = dataContext.From<Order>(b => b.Table("orders")).WithAlias(Alias.Root);

var rows = await rooted.Select(p => new { p.Root.Id, p.Item1.Id }).ToListAsync();
```

```sql
select Id, Id from orders
```

Производный корневой источник (`FromSql`, `From(builder)`, `From(QueryCommand<T>)`) при получении
псевдонима сохраняется — остаётся производной таблицей `(select …) as 't1'`, — а `p.Root` — тот же
слот, что и `p.Item1`.

