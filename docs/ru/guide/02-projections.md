# Проекции

Проекция задаёт, какие значения вернёт запрос. Первый пример использует таблицу `orders` из
[быстрого старта](../getting-started/02-quickstart.md). В нём используются созданные там
`dataContext` и сущность `IOrder`; справочные примеры ниже используют тестовые сущности и данные,
описанные на этой странице.

## Выберите нужные поля

Следующий запрос возвращает идентификатор каждого заказа и сумму в центах. Остальные колонки он не
возвращает:

```csharp
var rows = await dataContext.From<IOrder>()
    .OrderBy(order => order.Id)
    .Select(order => new { order.Id, order.TotalCents })
    .ToListAsync();
```

Для SQLite запрос выглядит так:

```sql
select id, total_cents
 from orders
 order by id
```

Запрос возвращает:

| Id | TotalCents |
|---:|-----------:|
| 1 | 2550 |
| 2 | 1800 |
| 3 | 4250 |

Анонимный результат содержит только два члена, указанных в `Select`. Используйте такую форму, когда
вызывающему коду нужны отдельные значения, а не вся сущность.

## Как строится проекция

Лямбда в `Select` задаёт выбранные колонки; колонки вне проекции не читаются. NextORM строит
возвращаемый объект по строкам результата. Если имя выходного члена совпадает с именем сопоставленной
колонки без учёта регистра, NextORM может выбрать её без переименования. Если член переименован или
вычисляется, NextORM назначает SQL-выражению псевдоним, совпадающий с именем выходного члена.
Провайдеры оформляют псевдонимы по-разному: например, `Calc` выглядит как `'Calc'` в SQLite,
`[Calc]` в SQL Server и `"Calc"` в PostgreSQL.

Аргументы конструктора, элементы кортежа и присваивания в инициализаторе объекта становятся
значениями выборки. Провайдер in-memory не генерирует SQL: он выполняет то же выражение над данными
в памяти.

В справочных примерах ниже используется отдельная тестовая модель `SimpleEntity` и `ComplexEntity`,
а не сущности `IOrder` и `ICustomer` из быстрого старта. Таблицы результатов используют тестовые
данные интеграционных тестов: `simple_entity` содержит идентификаторы `1`–`10`, а `complex_entity` —
три строки (`tests/nextorm.integration.tests/Providers/TestProviders/SqliteTestProvider.cs`). О
сопоставлении собственных сущностей см. раздел [Сущности и метаданные](../getting-started/03-entities-and-metadata.md).

## Анонимный тип

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { entity.Id })
    .ToListAsync();
```

```sql
select id
 from simple_entity
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
select (id + 1) as 'Id'
 from simple_entity
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
select id, (id + 1) as 'Calc'
 from complex_entity
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
select id, ((somestring || '/') || requiredstring) as 'Display'
 from complex_entity
```

Вывод:

| Id | Display |
|----|---------|
| 1 | dadfasd/sdf |
| 2 | xxx/asdfgoi |
| 3 | null |

## Колонки по имени

Сопоставленная сущность предоставляет доступ к колонкам через объявленные свойства. Если нужно
прочитать колонку, для которой в классе нет свойства (например, колонку широкой таблицы ClickHouse),
используйте
[`SqlFunctions.Column<T>`](xref:NextORM.Core.SqlFunctions.Column``1(System.Object,System.String)):

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(entity => new { Region = SqlFunctions.Column<ulong>(entity, "region_id") })
    .ToListAsync();
```

```sql
-- SQLite
select region_id as 'Region'
 from simple_entity
```

Первым аргументом должен быть параметр лямбды запроса (источник). Укажите **точное имя** колонки в
базе; провайдер безопасно заключит его в кавычки независимо от опции контекста
[`UseQuotedIdentifiers`](xref:NextORM.Core.DataContextBuilder.UseQuotedIdentifiers(System.Boolean))
(`` `region_id` `` в ClickHouse и MySQL, `"region_id"` в PostgreSQL и SQLite, `[region_id]` в SQL
Server). Точка в имени — обычный символ, а не разделитель схемы и псевдонима. NextORM не
интерпретирует имя как SQL, поэтому передавать имя в кавычках нельзя:
`Column(t, "[1]")` — литеральное трёхсимвольное имя `[1]` (рендерится `[[1]]]` в SQL Server), а
колонка с именем `1` — это `Column(t, "1")`. NextORM преобразует полученное значение в тип `T`,
поэтому тип должен поддерживаться провайдером при чтении результата запроса. В отличие от свойства
сущности, указанная колонка не сверяется с метаданными: опечатка в имени вызовет ошибку СУБД.

Тот же доступ работает в предикате и на join-проекции:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Join<ComplexEntity>((s, c) => s.Id == c.Id)
    .Where(p => SqlFunctions.Column<long>(p.Item2, "region_id") > 0)
    .Select(p => new { p.Item1.Id, Region = SqlFunctions.Column<long>(p.Item2, "region_id") })
    .ToListAsync();
```

Для источника вообще без типа сущности (`From("table")`) колонки читаются через
[`TableAlias`](xref:NextORM.Core.TableAlias) — см. [Joins](04-joins.md) и [CTE](10-cte.md).
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
select id
 from simple_entity
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

DTO (объект передачи данных) можно использовать как тип результата запроса к сопоставленной сущности:
NextORM связывает его свойства со столбцами этой сущности. Для **производного** источника,
прочитанного без внешнего `Select`, действуют другие ограничения. Например, NextORM отклоняет DTO во
внутренней проекции `From(query)`, если его свойства требуют повторной трансляции; это ограничение
формы проекции, а не анонимности типа. Если DTO — результат внутреннего запроса, укажите столбцы во
внешнем `Select` (см. [Подзапросы](07-subqueries.md#подзапрос-как-источник-from)). Поддерживаются DTO,
создаваемые через инициализатор свойств, и позиционные записи. Для класса, который создаётся только
через конструктор, известна ошибка проекции (`Cannot find inner column Id`), поэтому используйте одну
из поддерживаемых форм. См.
[Ограничения](../advanced/limitations.md#ограничения).

## Запись (record)

Позиционные записи также проецируются через свой конструктор:

```csharp
public record SimpleEntityRecord(long Id);

var rows = await dataContext.From("simple_entity")
    .Select(tbl => new SimpleEntityRecord(tbl.GetInt64("id")))
    .ToListAsync();
```

```sql
select id
 from simple_entity
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
select id
 from simple_entity
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

`Tuple.Create` задаёт SQL-конструктор для значения из нескольких частей. Он формирует одну колонку
значения-кортежа там, где провайдер поддерживает такие значения и контекст разрешает эту операцию.
Например, SQLite принимает её как операнд сравнения, но не как проекцию `SELECT`. `ValueTuple` —
другой тип: вызов верхнеуровневого `ValueTuple.Create(...)` и явная конструкция
`new ValueTuple<...>(...)` **не поддерживаются в проекции**. Оба варианта отклоняются при подготовке
запроса с `NotSupportedException`, в сообщении которого указаны тип проекции и позиция в `Select`.
Ошибка возникает до генерации SQL и обращения к базе, а не при чтении строк результата. Вместо этого
проецируйте DTO или анонимный тип; для вложенных членов-кортежей действуют прежние ограничения.

## Инициализатор членов

Вместо конструктора проекция может использовать инициализатор объекта:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .ToListAsync();
```

```sql
select id
 from simple_entity
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
select id
 from simple_entity where (id < 5)
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
select b
 from complex_entity where b = 1
```

Вывод:

| Boolean |
|---------|
| true |

Если единственная колонка имеет тип `byte[]` или `string`, её можно читать потоком, не загружая
целиком в память. `ToStream` и `ToTextReader` (а также их асинхронные формы) открывают потоковый
доступ провайдера к этой колонке. См. [Потоковое чтение больших объектов](28-large-objects.md).

Единственная скалярная форма, которой нужен конвертер значений, — колонка **enum-типа**: проекция
простого `enum` или nullable-enum без конвертера (`Select(it => it.State)`,
`Select(it => it.MaybeState)`) отклоняется при подготовке запроса с `NotSupportedException` —
`Enum scalar projections without a value converter are not supported in a server-side query.` — вместо
создания объекта-строки. Настройте конвертер для колонки (см.
[Конвертеры значений и JSON-колонки](../infrastructure/03-value-converters.md)), и её скалярная
проекция ведёт себя как любая другая сконвертированная колонка. Отказ относится только к подготовке
серверного запроса: провайдер in-memory строит проекцию из исходных объектов, поэтому тип `enum` там
продолжает работать.

## Проекция всей сущности

Сопоставленный источник с одной сущностью можно спроецировать целиком через `Select(it => it)`.
Запрос использует обычный список её колонок: перечисляет каждую колонку явно, а не через `*`, и
создаёт сущности так же, как запрос без проекции:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Where(it => it.Id < 3)
    .Select(it => it)
    .ToListAsync();
```

```sql
select id
 from simple_entity where (id < 3)
```

Сущность с несколькими колонками выбирает их в порядке объявления. Текст SQL совпадает с текстом,
который тот же запрос возвращает через `ToCommand()`. Тождественная проекция не использует отдельный
конвейер: NextORM строит её по тому же списку колонок и той же записи кэша.

Формы «объекта целиком», которые не являются тождественной проекцией сопоставленной сущности,
закрываются ошибкой на этапе подготовки, а не рендерят повреждённую инструкцию. Приведение строки
к несвязанному типу (`Select(it => (object)it)`) отклоняется с `DataContextException`, а
подготовленный список выборки, не разрешивший ни одной колонки, отклоняется с
[`BuildSqlCommandException`](xref:NextORM.Core.BuildSqlCommandException) до любого рендеринга.

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
select id, Calc
 from (select id, (somestring || somestring) as 'Calc'
 from complex_entity)
```

SQL Server требует, чтобы производная таблица имела псевдоним, и PostgreSQL аналогично:

```sql
-- SQL Server
select id, Calc
 from (select id, (somestring + somestring) as [Calc]
 from complex_entity) as [t1]
```

Чтение делает работоспособным именно внешний `Select`: внешний запрос адресует выходные столбцы
внутреннего `SELECT`, поэтому именованный или вычисляемый внутренний столбец переиспользуется как есть.
Чтение того же внутреннего запроса без внешней проекции (`dataContext.From(inner).ToList()`) допускается,
только если каждый внутренний столбец уже является такой разрешённой ссылкой; проекция, построенная из
выражений над членом строки (анонимная `new { it.Id }` или DTO `new Dto { Id = it.Id }`), отклоняется на
этапе подготовки с `QueryPreparationException` и должна читаться через внешний `Select`. Это ограничение
формы выражения, а не требование анонимного типа — см.
[Подзапросы](07-subqueries.md#подзапрос-как-источник-from).

## Коллекционная проекция

Член проекции может назвать **объявленную** коллекционную навигацию и получить коллекцию этого родителя в
результате — это *слот коллекционной проекции*. Прямая форма проецирует член как есть; форма через адаптер
поднимает его в конвейер на родителя:

```csharp
// прямо: вся коллекция на родителя
var posts = ctx.From<Post>()
    .OrderBy(p => p.Id)
    .Select(p => new { p.Id, p.Comments })
    .ToList();

// адаптер: фильтрация, упорядочивание и страница коллекции каждого родителя до проекции
var pageSize = 2;
var digest = ctx.From<Post>()
    .OrderBy(p => p.Id)
    .Select(p => new
    {
        p.Id,
        Recent = p.Comments
            .AsEntityBuilder<Comment>()
            .Where(c => c.Active)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id, OrderDirection.Desc)
            .Limit(pageSize)
            .Select(c => new CommentRow { Id = c.Id, CreatedAt = c.CreatedAt })
            .ToList(),
    })
    .ToList();
```

- **Связь должна быть объявлена** (`HasMany`/`HasManyThrough`/`[Relationship]`); поддерживаются слоты
  one-to-many и many-to-many, а слоты вкладываются через `Select(...)` адаптера на любую глубину со своей
  фильтрацией, упорядочиванием и страницей на каждом уровне.
- **Материализация — от жадной загрузки.** Граф сущностей загружается обычным маршрутом split (по умолчанию)
  или single-query — две инструкции для слота one-to-many глубины один, одна инструкция в режиме single-query —
после чего спроецированная форма вычисляется **в памяти по загруженным сущностям**. Отсечения колонок в
  SQL в этом релизе нет.
- **Принимаются семь форм назначения:** `List<T>`, `T[]`, `IEnumerable<T>`, `ICollection<T>`, `IList<T>`,
  `IReadOnlyCollection<T>`, `IReadOnlyList<T>`. Любая другая форма падает закрыто.
- **Именно из-за адаптера важны беспараметрические терминалы.** `EntityBuilder<TEntity>.ToList()`/`ToArray()` и
  `QueryCommand<TResult>.ToList()`/`ToArray()` существуют как беспараметрические перегрузки, чтобы цепочка была
  выразима в дереве выражений (на пары с `params ReadOnlySpan<object?>` дерево ссылаться не может).

Набор операторов, отказы до любого SQL, правило кардинальности для спроецированной ссылки и ограничения v1
описаны в [Слотах коллекционной проекции](../relationships/collection-projection.md).

## Именованные псевдонимы соединений

Соединённый запрос может именовать свои слоты вместо позиционной адресации. Соединение несёт
необязательный завершающий аргумент `Alias.<Name>`, именующий новый слот, а корневой источник можно
именовать через `.WithAlias(Alias.X)`:

```csharp
var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .WithAlias(Alias.Order)
    .Join<Person>(people, (p, buyer) => p.Order.BuyerId == buyer.Id, Alias.Buyer)
    .Select(p => new { OrderId = p.Order.Id, Buyer = p.Buyer.Id })
    .ToListAsync();
```

Именованные члены **существуют только для выражений**: они нужны, чтобы выражение `Select`/`Where`
могло назвать соединённую таблицу слота, и транслятор переписывает их в эту таблицу. Чтение такого
члена вне дерева выражений — например, при прямой материализации `p.Buyer` — по проекту бросает
`NotSupportedException`, а не возвращает значение по умолчанию; вместо этого проецируйте нужную
колонку (`Select(p => p.Buyer.Id)`). Именованный член можно использовать везде, где использовался бы
позиционный `p.Item2.Id` — в `Select`, `Where` и последующих соединениях. Чисто позиционная цепочка
использует типы-аккумуляторы `Projection<T1>`..`Projection<T1..T8>`, чьи члены `ItemN` — обычные
свойства; корневой псевдоним использует `Projection<T1>` — единственную проекцию размерности 1,
простой `IProjection`, не расширяемый напрямую, поскольку переход слота 1 поставляется
сгенерированными позиционными членами, — а именованный слот — это expression-only свойство поверх
того же аккумулятора. См. [Соединения](04-joins.md#именованные-псевдонимы-соединений).

## Что дальше

Продолжите с раздела [Соединения](04-joins.md), чтобы объединить значения из связанных таблиц.

