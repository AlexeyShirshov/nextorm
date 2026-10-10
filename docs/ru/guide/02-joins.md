# Соединения

> Объединяйте строки из двух или более сущностей, производных запросов или необработанных таблиц с помощью [`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})), [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) и [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})).

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Запросы и проекции](../querying/index.md) · [Фильтрация (WHERE)](01-filtering-where.md)

## Обзор

Каждый `EntityBuilder<T>` предоставляет семь методов соединения: [`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) (inner), [`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})),
[`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})), [`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})), [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) и [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})). Условие соединения — это выражение над двумя
сторонами, которое генерируется как предложение `ON` соединения, именно там, где построитель может его
транслировать. [`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})), [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) и [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) не принимают условие; первый генерирует
`cross join`, а последние два — форму lateral/apply конкретного провайдера (см.
[APPLY и LATERAL](#apply-и-lateral)).

Правая сторона может быть:

* другой типизированной сущностью, `EntityBuilder<TJoinEntity>`;
* `QueryCommand<TJoinEntity>` — подзапрос, который отображается как производная таблица;
* необработанной таблицей, [`EntityBuilder`](xref:NextORM.Core.EntityBuilder), созданной через [`From`](xref:NextORM.Core.DataContext.From(System.String)), столбцы которой
  читаются через индексатор [`TableAlias`](xref:NextORM.Core.TableAlias) (`t["id"]`).

Прежде чем перейти к примерам, важно понять две вещи:

1. **Арность соединений ограничена восемью таблицами на этапе компиляции.** Первое соединение
   возвращает [`JoinedEntityBuilder<T1, T2>`](xref:NextORM.Core.JoinedEntityBuilder`2), следующее — [`JoinedEntityBuilder<T1, T2, T3>`](xref:NextORM.Core.JoinedEntityBuilder`3) и так далее вплоть до
   `JoinedEntityBuilder<T1..T8>`. `JoinedEntityBuilder` намеренно не предоставляет дальнейших методов
   [`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions}))/[`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions}))/[`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions}))/[`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions}))/[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})), а `Projection<T1..T8>` не реализует
   [`IExtendableProjection`](xref:NextORM.Core.IExtendableProjection), поэтому девятое соединение не компилируется.
   Потолок можно снять, назвав накопленную проекцию через
   [`As`](xref:NextORM.Core.EntityBuilder`1.As``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) (см. [Именование промежуточной проекции](#именование-промежуточной-проекции-as)).
2. **Накопленная проекция адресуется как `p.Item1`, `p.Item2`, … `p.Item8`.** После первого соединения
   условие получает эту проекцию вместо обычной сущности, поэтому цепочка соединений ссылается на
   уже соединённые таблицы через `p.tN`.

Сгенерированный SQL ссылается на каждую таблицу по позиционному псевдониму: `t1` для базовой
таблицы и `t2`, `t3`, … для присоединённых таблиц, в порядке их добавления. Псевдонимы экранируются
в зависимости от провайдера (`'t1'` в SQLite, `[t1]` в SQL Server, `"t1"` в PostgreSQL).

## Внутреннее соединение

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

```sql
select t1.id, t2.requiredstring from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Id | RequiredString |
|----|----------------|
| 1 | sdf |
| 2 | asdfgoi |
| 3 | 34mfs |

`SimpleEntity.Id` имеет тип `int`, тогда как `ComplexEntity.Id` — `long`, поэтому более узкая
сторона расширяется с помощью `cast(t1.id as bigint)`.

`WHERE` после соединения применяется к накопленной проекции и может ссылаться на любую сторону:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.Item2.Boolean ?? false)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

[`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})), размещённый до соединения, сначала фильтрует левую сторону; для внутренних соединений эти
два варианта эквивалентны, но для внешних соединений они различаются:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Where(it => it.Id > 2)
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.Item2.RequiredString == "34mfs")
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

### Адресация проекции

Накопленная проекция позиционна и следует tuple-конвенции: `Item1` — базовый (левый) источник,
`Item2` — первый присоединённый, `ItemN` — *N*-й добавленный, в порядке соединений. Имена не
семантичны — из `Item1`/`Item2` не видно, где «заказ», а где «клиент», — поэтому придайте им смысл,
спроецировав в именованный тип сразу после соединения и используя его дальше в запросе:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { Order = p.Item1.Id, Customer = p.Item2.RequiredString })
    .ToListAsync();
```

```sql
select t1.id as 'Order', t2.requiredstring as 'Customer' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

Член проекции обычно — колонка одной из соединённых сущностей (`p.Item1.Id`), но целый элемент можно
выбрать и напрямую: `Select(p => p.Item1)` / `Select(p => p.Item2)` выбирает только mapped-колонки этой
сущности и материализует её — либо `null` на отсутствующей стороне outer join. Если нужны отдельные
колонки той же стороны без материализации сущности, перечислите их явно.

```csharp
var children = await dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => p.Item2)
    .ToListAsync();
```

```sql
select t2.id, t2.nullableint, t2.somestring, ... from simple_entity as 't1' left join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

Wildcard `SELECT *` не генерируется никогда — mapped-колонки перечисляются явно, — а сравнение целых
сущностей в предикате и приведение элемента к несвязанному типу результата не поддержаны.

## Именованные псевдонимы соединений

Позиционная адресация выше не меняется и всегда доступна. Соединение может дополнительно нести
необязательный завершающий аргумент `Alias.<Name>`, который именует новый слот, а позиционные и
именованные шаги можно **свободно смешивать** в одной цепочке и в любом порядке — псевдоним
необязателен на каждом шаге. Именованный слот и позиционный `ItemK` того же слота — одна и та же
таблица: после `Alias.Buyer` члены `p.Buyer` и `p.Item2` указывают на слот 2, который по-прежнему
отображается на SQL-псевдоним `t2`. Именованные псевдонимы нужны, когда два соединённых источника
имеют один CLR-тип — например покупатель и утверждающий оба `Person`, — потому что позиционные члены
их не различают. Сгенерированная проекция раскрывает каждое имя как типизированное свойство:

```csharp
using NextORM.Generated.MyAssembly; // Alias живёт в NextORM.Generated.<имя вашей сборки>

var people = dataContext.From<Person>(b => b.Table("person"));

var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .Join<Person>(people, (o, buyer) => o.BuyerId == buyer.Id, Alias.Buyer)
    .Join<Person>(people, (o, approver) => o.Item1.ApproverId == approver.Id, Alias.Approver)
    .Select(p => new { Buyer = p.Buyer.Id, Approver = p.Approver.Id })
    .ToListAsync();
```

```sql
select t2.id as 'Buyer', t3.id as 'Approver' from orders as 't1' join person as 't2' on t1.buyerid = t2.id join person as 't3' on t1.approverid = t3.id
```

`Alias.<Name>` — маркер времени компиляции. Генератор исходного кода, поставляемый внутри пакета
`nextorm` как анализатор Roslyn (дополнительный пакет или ссылка не нужны), для каждой найденной
цепочки слотов генерирует `public`-тип проекции `AliasProjection_<suffix>` с одним свойством на
именованный слот и `public`-тип построителя `AliasJoin_<suffix>`, повторяющий операторы соединения.
Суффикс кодирует всю цепочку слот за слотом — `P{slot}` для позиционного слота и `A{slot}_{name}` для
именованного, — поэтому `From<Order>().Join<Person>(people, …, Alias.Buyer)` даёт
`AliasJoin_P1_A2_Buyer<Order, Person>`. Всё это попадает в зарезервированное пространство имён
`NextORM.Generated.<имя сборки>`, поэтому проекцию не нужно объявлять заранее. Сгенерированные
свойства несут [`JoinSlot(n)`](xref:NextORM.Core.JoinSlotAttribute) — 1-базовую позицию сущности в
цепочке: первый присоединённый источник — слот `2`, следующий — `3` и так далее. Поскольку слот задан
явно, два псевдонима одного CLR-типа разрешаются в разные таблицы, а поскольку видовая буква суффикса
разбирает токены однозначно (имя псевдонима не может содержать `_`), имя, оканчивающееся цифрой
(`Buyer2`), — это имя, а не номер слота.

Сгенерированные члены-псевдонимы **существуют только для выражений**: они нужны, чтобы выражение
`Select`/`Where` могло назвать соединённую таблицу слота, и транслятор переписывает их в эту таблицу.
Чтение такого члена вне дерева выражений — например, при прямой материализации `p.Buyer` — по
проекту бросает `NotSupportedException`, а не возвращает значение по умолчанию; вместо этого
проецируйте нужную колонку (`Select(p => p.Buyer.Id)`). Позиционные члены `ItemN` остаются обычными
свойствами.

Все семь операторов проекционных соединений принимают псевдоним; `JoinInto` среди них нет, у него
нет поверхности с псевдонимами, поэтому однозапросный загрузчик связей никогда не принимает аргумент
`Alias.<Name>`. Три оператора без условия принимают только источник и маркер:

```csharp
var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .CrossJoin(people, Alias.Person)
    .Select(p => p.Person.Id)
    .ToListAsync();
```

Внутри одной цепочки сгенерированная проекция сохраняет позиционные члены `ItemN` наряду с
именованными, и те и другие указывают на одни и те же слоты; смешивание свободно в обе стороны и
может чередоваться:

```csharp
var chained = dataContext.From<Order>(b => b.Table("orders"))
    .Join<Person>(people, (o, p) => o.BuyerId == p.Id, Alias.Buyer)      // слот 2, именованный
    .Join(people, (p, x) => p.Item2.Id == x.Id)                          // слот 3, позиционный
    .Join<Person>(people, (p, a) => p.Item3.Id == a.Id, Alias.Approver); // слот 4, именованный
```

```sql
... join person as 't2' on t1.buyerid = t2.id join person as 't3' on t2.id = t3.id join person as 't4' on t3.id = t4.id
```

Псевдоним должен быть корректным идентификатором C# без `_` (зарезервирован для сборки имён
сгенерированных типов); зарезервированное ключевое слово генерируется экранированным (`@class`).
Класс-маркер — единственная допустимая форма аргумента псевдонима: `Alias.Buyer<int>` или
`Alias.Buyer.Approver` отклоняются. Арность ограничена восемью слотами через `Projection<T1..T8>`.

Сгенерированные перегрузки с псевдонимами принимают и типизированный дескриптор CTE — [`Cte<T>`](xref:NextORM.Core.Cte`1)
вместо источника [`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1). Присоединяемый тип выводится из дескриптора, поэтому явный
аргумент типа `<T>` и преобразование (`dataContext.From(cte)`) можно опустить:

```csharp
var peopleCte = dataContext.From<Person>(b => b.Table("person"))
    .ToCommand()
    .AsCte("people_cte");

var rows = await dataContext.From<Order>(b => b.Table("orders"))
    .Join(peopleCte, (o, p) => o.BuyerId == p.Id, Alias.Buyer)
    .Select(p => new { OrderId = p.Item1.Id, BuyerId = p.Buyer.Id, BuyerName = p.Buyer.Name })
    .ToListAsync();
```

```sql
-- SQLite: CTE присоединяется по имени, без обёртки в производную (select ...) таблицу
... join people_cte as 't2' on t1.buyerid = t2.Id
```

Именованный путь доступен только на SQL-провайдерах: провайдер in-memory бросает
`NotSupportedException`. Запрос может пересекать границу метода, но метод обязан назвать в своей
сигнатуре сгенерированный тип построителя, потому что этот тип генерируется и не может быть выведен
из написанного вручную имени:

```csharp
private static AliasJoin_P1_A2_Buyer_A3_Approver<Order, Person, Person> AddApprover(
    AliasJoin_P1_A2_Buyer<Order, Person> builder,
    EntityBuilder<Person> people)
    => builder.Join<Person>(people, (o, a) => o.Item1.ApproverId == a.Id, Alias.Approver);
```

Генератор сообщает диагностики `NORMGEN001`–`NORMGEN008` (дубликат псевдонима; коллизия псевдонима
со сгенерированным членом; недопустимый идентификатор; арность больше восьми; аргумент не в форме
`Alias.<Name>`; имя сборки, которое не нормализуется в пространство имён; коллизия сигнатур
extension-метода соединения-псевдонима; и `.WithAlias` не на корневом источнике). См.
[Ограничения](../advanced/limitations.md).

`NORMGEN007` защищает от перегрузок соединений-псевдонимов, которые имели бы одинаковую сигнатуру,
но разные тела. На текущем генераторе такая ситуация **структурно недостижима**, поэтому диагностика
**защитная**: тело сгенерированного метода — чистая функция от сгенерированной сигнатуры, а
присоединяемый тип последнего шага всегда рендерится как параметр типа `TJoin`. Два соединения-
псевдонима, которые используют общий `Alias.X`, но проецируют разные типы последнего шага, **оба
поддерживаются**: они используют одну общую generic-перегрузку `Join<TJoin>`, а не порождают две
конфликтующие перегрузки — отдельные типизированные построители при этом не создаются. Давайте
каждому слоту своё имя псевдонима (как `Alias.Buyer` и `Alias.Approver` выше), чтобы каждая цепочка
получила собственный типизированный построитель; это выбор гранулярности построителя, а не
проверенный способ устранения `NORMGEN007`.

### Именование корневого источника (`.WithAlias`)

Корневой источник занимает слот 1, поэтому его можно именовать цепочным `.WithAlias(Alias.X)` на
простом билдере источника до любого соединения. Псевдоним именует существующий слот 1 — он не
добавляет ни источник, ни слот — и работает для любого семейства корневых источников: `From<T>`
(mapped-сущность), `From("table")` и `CreateQueryBuilder("table")` (источник [`TableAlias`](xref:NextORM.Core.TableAlias)),
`FromSql`, типизированный [`Cte<T>`](xref:NextORM.Core.Cte`1), ленивая временная таблица,
`FromTableFunction<T>`, `From(QueryCommand<T>)` и forwarder'ы `CreateQueryBuilder*`. Физический
источник сохраняется: производный корень (`FromSql`, `From(query)`, `From(builder)`) остаётся
производной таблицей с псевдонимом `t1`, а mapped-сущность по-прежнему читает свою физическую
таблицу. `Where`, `OrderBy`, `GroupBy`, `Having` и paging, применённые до `.WithAlias`, переносятся на
корневую проекцию.

```csharp
var rooted = dataContext.From<Order>(b => b.Table("orders")).WithAlias(Alias.Root); // слот 1

var rows = await rooted
    .Join<Person>(people, (o, p) => o.Root.BuyerId == p.Id, Alias.Buyer)
    .Select(p => new { OrderId = p.Root.Id, Buyer = p.Buyer.Id })
    .ToListAsync();
```

```sql
select t1.id as 'OrderId', t2.id as 'Buyer' from orders as 't1' join person as 't2' on t1.buyerid = t2.id
```

`p.Root` — тот же слот 1, что и `p.Item1`. После корневого псевдонима может следовать позиционное
соединение (сгенерированный позиционный переход привязывается к корню), поэтому именование корня не
заставляет именовать остальную цепочку. `.WithAlias` доступен только на корне: применение после
соединения или второго корневого псевдонима — ошибка времени компиляции (`NORMGEN008`), а runtime-шов
падает закрыто. Как и любой именованный псевдоним, он существует только для выражений, а провайдер
in-memory бросает `NotSupportedException`.

## Внешние соединения

[`LeftJoin`](xref:NextORM.Core.EntityBuilder`1.LeftJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) сохраняет каждую строку левой стороны и заполняет правую сторону значением `NULL`, когда
совпадения нет; [`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) и [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) ведут себя симметрично.

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { LeftId = p.Item1.Id, RightString = p.Item2.RequiredString })
    .ToList();
```

```sql
select t1.id as 'LeftId', t2.requiredstring as 'RightString' from simple_entity as 't1' left join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

[`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) и [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) генерируются в той же форме:

```csharp
var right = dataContext.From<IComplexEntity>()
    .RightJoin(dataContext.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
    .Select(p => new { LeftString = p.Item1.RequiredString, RightId = p.Item2.Id })
    .ToList();

var full = dataContext.From<ISimpleEntity>()
    .FullJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { LeftId = p.Item1.Id, RightString = p.Item2.RequiredString })
    .ToList();
```

[`RightJoin`](xref:NextORM.Core.EntityBuilder`1.RightJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) и [`FullJoin`](xref:NextORM.Core.EntityBuilder`1.FullJoin``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) отклоняются с `NotSupportedException` только тогда, когда диалект сообщает
`SupportsRightFullJoin == false`; все поставляемые с nextorm провайдеры заявляют о поддержке.

## Перекрёстное соединение

[`CrossJoin`](xref:NextORM.Core.EntityBuilder`1.CrossJoin``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) не принимает условие и порождает декартово произведение:

```csharp
var count = dataContext.From<ISimpleEntity>().CrossJoin(dataContext.From<IComplexEntity>()).Count();
```

```sql
select count(*) from simple_entity as 't1' cross join complex_entity as 't2'
```

Вывод:

| Count |
|-------|
| 30 |

## APPLY и LATERAL

[`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) и [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) генерируют форму lateral-источника конкретного провайдера. Правая сторона —
это тот же набор источников, что принимает обычное соединение: типизированная сущность, производная
таблица `QueryCommand<T>`, необработанная таблица или табличная функция, — но без условия `ON`:

* [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) оставляет только те строки левой стороны, для которых применяемый источник возвращает
  хотя бы одну строку (SQL Server `CROSS APPLY`, PostgreSQL/MySQL/MariaDB `CROSS JOIN LATERAL`, либо
  обычный `CROSS JOIN` для простой таблицы — `LATERAL` допустим только перед подзапросом или функцией);
* [`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) дополнительно сохраняет строки левой стороны с пустым применяемым источником, заполняя
  правую сторону значением `NULL` (SQL Server `OUTER APPLY`, PostgreSQL/MySQL/MariaDB
  `LEFT JOIN LATERAL ... ON true`, либо обычный `LEFT JOIN ... ON true` для простой таблицы).

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .CrossApply(dataContext.From<IComplexEntity>())
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

```sql
-- SQL Server
select t1.id, t2.somestring from simple_entity as [t1] cross apply complex_entity as [t2]
-- PostgreSQL / MySQL / MariaDB
select t1.id, t2.somestring from simple_entity as t1 cross join complex_entity as t2
```

`OUTER APPLY` по производной таблице:

```csharp
var subQuery = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id, c.RequiredString });

var rows = dataContext.From<ISimpleEntity>()
    .OuterApply(subQuery)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToList();
```

```sql
-- SQL Server
... from simple_entity as [t1] outer apply (select id, somestring from complex_entity where ...) as [t2]
-- PostgreSQL / MySQL / MariaDB
... from simple_entity as t1 left join lateral (select ...) as t2 on true
```

### Коррелированный APPLY / LATERAL

Применяемый источник может ссылаться на столбцы строки левой стороны, если построить его внутри
лямбды, принимающей эту строку. [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions}))/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) принимают такую лямбду в двух формах: возвращающую
`QueryCommand<T>` (производный запрос с проекцией) и возвращающую `EntityBuilder<T>` (сущность целиком).

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .CrossApply(s => dataContext.From<IComplexEntity>()
        .Where(c => c.Id == s.Id)
        .Select(c => new { c.Id, c.RequiredString }))
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

```sql
-- SQL Server
... from simple_entity as [t1] cross apply (select ... from complex_entity as [t2] where t2.id = t1.id) as [t3]
-- PostgreSQL / MySQL / MariaDB
... from simple_entity as t1 cross join lateral (select ...) as t3
```

Параметр лямбды ведёт себя как внешний параметр коррелированного скалярного подзапроса: столбец строки
левой стороны (здесь `s.Id`) становится внешней ссылкой и разрешается в псевдоним левой таблицы.
[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) дополнительно сохраняет строки левой стороны с пустым применяемым источником, заполняя их `NULL`.

Корреляция требует lateral-источника: диалекты с `SupportsApply == false` (SQLite, ClickHouse) отклоняют
коррелированный apply через `NotSupportedException`, как и in-memory-провайдер. Коррелированный apply
не может ссылаться на проекцию соединения — применяйте его к источнику из одной сущности.

### APPLY по табличной функции

Применяемый источник может быть табличной функцией (см. [Табличные функции](11-table-valued-functions.md)):
[`FromTableFunction`](xref:NextORM.Core.DataContextExtensions.FromTableFunction``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{System.Linq.IQueryable{``0}}})) возвращает `EntityBuilder<T>`, поэтому передаётся точно так же, как сущность. Некоррелированная функция применяется напрямую:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .CrossApply(dataContext.FromTableFunction(() => Tvf.AllRows()))
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQL Server
select t1.id, t2.value from simple_entity as [t1] cross apply all_rows() as [t2]
-- PostgreSQL / MySQL / MariaDB
select t1.id, t2.value from simple_entity as t1 cross join lateral all_rows() as t2
```

Корреляция функции со строкой левой стороны передаёт столбец этой строки аргументом функции; вызов тогда оборачивается в lateral-производную таблицу:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .CrossApply(s => dataContext.FromTableFunction(() => Tvf.ById(s.Id)))
    .Select(p => new { p.Item1.Id, p.Item2.Value })
    .ToList();
```

```sql
-- SQL Server
select t1.id, t3.value from simple_entity as [t1] cross apply (select t2.id, t2.value from rows_by_id(cast(t1.id as bigint)) as [t2]) as [t3]
-- PostgreSQL / MySQL / MariaDB
select t1.id, t3.value from simple_entity as t1 cross join lateral (select t2.id, t2.value from rows_by_id(cast(t1.id as bigint)) as t2) as t3
```

Как и любой коррелированный apply, коррелированная форма функции требует lateral-источника и отклоняется с `NotSupportedException` в SQLite/ClickHouse и в in-memory-провайдере; некоррелированная форма подчиняется правилам источника конкретного провайдера (`CROSS APPLY` в SQL Server, `CROSS JOIN LATERAL` в PostgreSQL/MySQL/MariaDB).

## Соединение с подзапросом

`QueryCommand<T>` можно присоединить напрямую. Он заключается в скобки и получает псевдоним как
производная таблица; псевдоним теперь выдаётся всегда (даже в SQLite, где он необязателен), чтобы все
ссылки на производный источник — условие `ON`, `WHERE`, `ORDER BY` и проекция — разрешались через одну
идентичность псевдонима. Член, который проекция производной таблицы переименовывает (`c.String`,
отображённый на `requiredstring`), экспонируется под именем проекции (`requiredstring as 'String'`) и
снаружи указывается по этому имени (`t1.'String'`), а не по физическому имени колонки:

```csharp
var subQuery = dataContext.From<IComplexEntity>()
    .Where(it => it.Id == 3)
    .Select(it => new { it.Id, it.RequiredString, it.Boolean });

var rows = await dataContext.From<ISimpleEntity>()
    .Join(subQuery, (s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
    .ToListAsync();
```

```sql
select t1.id, t2.requiredstring from simple_entity as 't1' join (select id, requiredstring, b as 'Boolean' from complex_entity where id = 3) as 't2' on cast(t1.id as bigint) = t2.id
```

Типизированный CTE — **не** подзапрос: передайте [`Cte<T>`](xref:NextORM.Core.Cte`1) прямо в те же семь операторов, и он
присоединяется по имени — без обёртки в производную `(select ...)` — ровно как если бы вы преобразовали
его через `dataContext.From(cte)`. См.
[Прямое соединение типизированного CTE](08-cte.md#прямое-соединение-типизированного-cte).

## Производный запрос как первичный источник

`QueryCommand<T>` может быть и **первичным** источником `FROM`, а присоединяемая таблица пишется второй:

```csharp
var derived = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 0)
    .Select(c => new { c.Id, c.RequiredString });

var rows = await dataContext.From(derived)
    .Join(dataContext.From<ISimpleEntity>(), (d, s) => d.Id == s.Id)
    .Select(p => new { p.Item1.Id, SId = p.Item2.Id })
    .ToListAsync();
```

```sql
select t1.id, t2.id as 'SId' from (select id, requiredstring from complex_entity where (id > 0)) as 't1' join simple_entity as 't2' on cast(t1.id as bigint) = t2.id
```

`Where` можно написать до соединения — он применяется к производной таблице (`d => d.Id > 5`
превращается в фильтр производного источника, `where t1.id > 5`). Любой другой модификатор
(`OrderBy`, `GroupBy`, `Distinct`, paging, ...) нужно применять **внутри** производного запроса:
перенос его за соединение изменил бы смысл запроса, поэтому билдер бросает `NotSupportedException`, а
не переносит модификатор молча. Соединение работает во всех SQL-провайдерах; провайдер in-memory его
отвергает.

Производный запрос может и сам содержать соединения: его проекция `Select` становится колонками
производной таблицы, и на эти члены можно ссылаться в последующих `Where`/`Join`. Каждый источник
получает позиционный алиас (`t1`, `t2`, ...), поэтому внешнее соединение никогда не переиспользует
алиас изнутри производного запроса:

```csharp
var derived = dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.RequiredString });

var rows = await dataContext.From(derived)
    .Where(d => d.CustomerName != null)
    .Join(dataContext.From<IComplexEntity>(), (d, c2) => d.OrderId == c2.Id)
    .Select(p => new { p.Item1.OrderId, p.Item1.CustomerName, Third = p.Item2.RequiredString })
    .ToListAsync();
```

```sql
select t3.OrderId, t3.CustomerName, t4.requiredstring as 'Third' from (select t1.id as 'OrderId', t2.requiredstring as 'CustomerName' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id) as 't3' join complex_entity as 't4' on cast(t3.OrderId as bigint) = t4.id where t3.CustomerName is not null
```

## Соединение с необработанной таблицей

`From("table")` создаёт неуниверсальную [`EntityBuilder`](xref:NextORM.Core.EntityBuilder), столбцы которой доступны через [`TableAlias`](xref:NextORM.Core.TableAlias). Левую
и правую стороны можно свободно смешивать с типизированными сущностями:

```csharp
var rows = dataContext
    .From("simple_entity")
    .Join(dataContext.From("complex_entity"), (s, c) => s["id"] == c["id"])
    .Select(p => new { Id = p.Item1["id"].AsInt, Str = p.Item2["someString"].AsString })
    .ToList();

var mixed = dataContext
    .From("simple_entity")
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s["id"].AsInt == c.Id)
    .Select(p => new { Id = p.Item1["id"].AsInt, Str = p.Item2.String })
    .ToList();
```

Соединение с необработанной таблицей (`TableAlias`) теперь учитывает явно заданный источник на
присоединяемом построителе. Если присоединяемая сторона — это необработанный SQL (`FromSql`),
переопределение таблицы (`b => b.Table("...")` / `WithTableName`), производный запрос
(`From(query)`), табличная функция (`FromTableFunction`) или типизированный CTE (`Cte<T>`), в
качестве `FROM` соединения используется именно этот источник, а не метаданные сущности. Для обычной
сопоставленной сущности без явного источника сохраняется запасной путь через метаданные.
`SemiJoin`/`AntiJoin` не затрагиваются: присоединяемый тип по-прежнему разрешается через метаданные
сущности.

## Цепочки соединений и арность 2..8

Каждый вызов в цепочке добавляет одну таблицу. Условие получает накопленную на данный момент
проекцию и новую сущность:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)          // JoinedEntityBuilder<SimpleEntity, ComplexEntity>
    .Join(dataContext.From<ISimpleEntity>(), (p, s) => p.Item2.Id == s.Id)        // JoinedEntityBuilder<...>
    .Join(dataContext.From<IComplexEntity>(), (p, c) => p.Item3.Id == c.Id)       // JoinedEntityBuilder<...>
    .Select(p => new { A = p.Item1.Id, B = p.Item2.RequiredString, C = p.Item3.Id, D = p.Item4.RequiredString })
    .ToList();
```

```sql
select t1.id as 'A', t2.requiredstring as 'B', t3.id as 'C', t4.requiredstring as 'D' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id join simple_entity as 't3' on t2.id = cast(t3.id as bigint) join complex_entity as 't4' on cast(t3.id as bigint) = t4.id
```

Тот же приём наращивает арность вплоть до `JoinedEntityBuilder<T1..T8>` (восемь таблиц). При восьми
таблицах проекция предоставляет `Item1`..[`Item8`](xref:NextORM.Core.Projection`8.Item8):

```csharp
var e = new[]
{
    dataContext.From<ISimpleEntity>(), dataContext.From<ISimpleEntity>(),
    dataContext.From<ISimpleEntity>(), dataContext.From<ISimpleEntity>(),
    dataContext.From<ISimpleEntity>(), dataContext.From<ISimpleEntity>(),
    dataContext.From<ISimpleEntity>(), dataContext.From<ISimpleEntity>(),
};

var sql = e[0]
    .Join(e[1], (a, b) => a.Id == b.Id)
    .Join(e[2], (p, c) => p.Item2.Id == c.Id)
    .Join(e[3], (p, c) => p.Item3.Id == c.Id)
    .Join(e[4], (p, c) => p.Item4.Id == c.Id)
    .Join(e[5], (p, c) => p.Item5.Id == c.Id)
    .Join(e[6], (p, c) => p.Item6.Id == c.Id)
    .Join(e[7], (p, c) => p.Item7.Id == c.Id)
    .Select(p => new { A = p.Item1.Id, B = p.Item2.Id, C = p.Item3.Id, D = p.Item4.Id,
                       E = p.Item5.Id, F = p.Item6.Id, G = p.Item7.Id, H = p.Item8.Id });
```

Прямые перегрузки [`Cte<T>`](xref:NextORM.Core.Cte`1) доступны и на каждом продолжении: каждый
[`JoinedEntityBuilder<T1..Tn>`](xref:NextORM.Core.JoinedEntityBuilder`2) (арности приёмника 2–7) принимает типизированный CTE вместо
источника `EntityBuilder<T>` для всех семи операторов, поэтому CTE можно присоединить на любом шаге.
Проекция при этом по-прежнему ограничена восемью слотами — восьмая арность не предоставляет
дальнейшего соединения, и `Cte<T>` не поднимает этот потолок.

## Именование промежуточной проекции: `As`

[`As`](xref:NextORM.Core.EntityBuilder`1.As``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) проецирует каждую соединённую строку в именованный тип и предоставляет результат как
производную таблицу, поэтому последующий `Join` начинается с именованных членов, а не с `p.Item1`,
`p.Item2`, …:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .As(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.String })
    .Join(dataContext.From<IComplexEntity>(), (d, c2) => d.OrderId == c2.Id)
    .Select(p => new { p.Item1.OrderId, p.Item1.CustomerName, Third = p.Item2.Id })
    .ToList();
```

```sql
select t3.OrderId, t3.CustomerName, t4.id as 'Third' from (select t1.id as 'OrderId', t2.somestring as 'CustomerName' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id) as 't3' join complex_entity as 't4' on cast(t3.OrderId as bigint) = t4.id
```

`As` объявлен один раз на `EntityBuilder<T>` и наследуется каждым `JoinedEntityBuilder<T1..Tn>`,
поэтому per-arity перегрузки нет. Поскольку результат — производная таблица, это также снимает
восьмитабличный потолок времени компиляции: `.As(...)` сбрасывает счётчик арности, и следующий
`Join` возвращает новый `JoinedEntityBuilder<TResult, …>`.

После `As` видны только спроецированные члены, а проекция становится границей материализации,
поэтому столбцы более ранних источников недоступны в последующих соединениях. In-memory-провайдер
компилирует соединения в делегаты и не может соединять производный источник, поэтому `Join` после
`As` выбрасывает `NotSupportedException`.

## Захваченные параметры в соединении

Захваченная локальная переменная в условии соединения становится параметром и извлекается заново
при каждом выполнении, в том числе при неявном попадании в кэш планов:

```csharp
for (var i = 1; i <= 3; i++)
{
    var id = i;
    var rows = dataContext.From<ISimpleEntity>()
        .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
        .Where(p => p.Item2.Id == id)
        .Select(p => new { p.Item1.Id, p.Item2.RequiredString })
        .ToList();
}
```

## Специфичные для провайдера модификаторы соединения (ClickHouse)

ClickHouse добавляет два модификатора соединения, которых нет у остальных диалектов: модификатор
**строгости** (`ANY`/`ALL`/`ASOF`) и распределённый префикс `GLOBAL`. Оба передаются соединению через
его лямбду `Action<JoinOptions>`: [`JoinOptions.WithStrictness`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.WithStrictness(NextORM.Core.JoinOptions,NextORM.Core.JoinStrictness)) и
[`JoinOptions.Global`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.Global(NextORM.Core.JoinOptions)):

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id,
        j => j.WithStrictness(JoinStrictness.Any))
    .Select(p => new { p.Item1.Id, p.Item2.String })
    .ToList();
```

```sql
select t1.id, t2.somestring from simple_entity as `t1` left any join complex_entity as `t2` on cast(t1.id as bigint) = t2.id
```

[`JoinStrictness.Any`](xref:NextORM.Core.JoinStrictness.Any) рендерит `<type> any join` и оставляет одну
правую строку на каждую левую; [`JoinStrictness.All`](xref:NextORM.Core.JoinStrictness.All) оставляет все
совпадения; [`JoinStrictness.Asof`](xref:NextORM.Core.JoinStrictness.Asof) рендерит `asof join`, для
которого нужна одна колонка равенства и завершающее неравенство.
[`JoinOptions.Global`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.Global(NextORM.Core.JoinOptions)) рендерит префикс `GLOBAL`, используемый в
распределённых запросах, и сочетается с модификатором строгости в любом порядке
(`global left any join`):

```csharp
var global = dataContext.From<ISimpleEntity>()
    .LeftJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id,
        j => j.Global().WithStrictness(JoinStrictness.Any));
```

```sql
... from simple_entity as `t1` global left any join complex_entity as `t2` on ...
```

Опции копируются в соединение, при объявлении которого они заданы, поэтому они привязаны к этому
соединению и не протекают в другой запрос или следующее соединение. Модификатор принимается только
на соединениях `INNER`/`LEFT`/`RIGHT`/`FULL`
(для `CROSS`/`APPLY` — `NotSupportedException`). Модификаторы доступны только в ClickHouse
([`SupportsJoinStrictness`](xref:NextORM.Core.ISqlDialect.SupportsJoinStrictness),
[`SupportsGlobalJoin`](xref:NextORM.Core.ISqlDialect.SupportsGlobalJoin)); остальные провайдеры и
контекст in-memory отклоняют их через `NotSupportedException`.

### Соединения SEMI / ANTI / PASTE

`SEMI`, `ANTI` и `PASTE` — это *виды* соединения, а не модификаторы: они меняют набор колонок и строк,
которые даёт соединение, поэтому для них есть отдельные построители, а не опция `j => j.WithStrictness(...)`:

```csharp
var ids = dataContext.From<ISimpleEntity>()
    .SemiJoin(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(s => s.Id)
    .ToList();
```

```sql
select t1.id from simple_entity as `t1` left semi join complex_entity as `t2` on cast(t1.id as bigint) = t2.id
```

- [`SemiJoin`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.SemiJoin``2(NextORM.Core.EntityBuilder{``0},NextORM.Core.EntityBuilder{``1},System.Linq.Expressions.Expression{System.Func{``0,``1,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) оставляет только левые колонки — по одной на каждую
  левую строку, у которой есть хотя бы одно совпадение справа;
- [`AntiJoin`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.AntiJoin``2(NextORM.Core.EntityBuilder{``0},NextORM.Core.EntityBuilder{``1},System.Linq.Expressions.Expression{System.Func{``0,``1,System.Boolean}},System.Action{NextORM.Core.JoinOptions})) оставляет только левые колонки для левых строк без
  совпадения (дополнение к `SemiJoin`);
- [`PasteJoin`](xref:NextORM.ClickHouse.ClickHouseEntityBuilderExtensions.PasteJoin``2(NextORM.Core.EntityBuilder{``0},NextORM.Core.EntityBuilder{``1},System.Action{NextORM.Core.JoinOptions})) сопоставляет два источника по позиции строки без `ON`;
  проекция содержит обе стороны, а строк — сколько у более короткой стороны.

`SemiJoin`/`AntiJoin` возвращают ту же форму проекции (правые колонки недоступны), а `PasteJoin`
добавляет один элемент. Они доступны только в ClickHouse
([`SupportsSemiAntiJoin`](xref:NextORM.Core.ISqlDialect.SupportsSemiAntiJoin)/[`SupportsPasteJoin`](xref:NextORM.Core.ISqlDialect.SupportsPasteJoin));
остальные провайдеры и контекст in-memory отклоняют их через `NotSupportedException`. Полный каталог —
в разделе [Специфичный для провайдеров SQL](provider-specific/overview.md).

## Различия между провайдерами

| Провайдер | Позиционные SQL-псевдонимы таблиц | Псевдоним производной таблицы | Внешние соединения | APPLY / LATERAL |
|---|---|---|---|---|
| SQLite | `as 't1'` | выдаётся всегда (в самом SQLite необязателен) | поддержаны left/right/full | не поддерживается (`NotSupportedException`) |
| SQL Server | `as [t1]` | обязателен | поддержаны left/right/full | `CROSS APPLY` / `OUTER APPLY` |
| PostgreSQL | `as "t1"` | обязателен | поддержаны left/right/full | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` (простые таблицы: `CROSS JOIN` / `LEFT JOIN ... ON true`) |
| MySQL / MariaDB | `as \`t1\`` | обязателен | поддержаны left/right (без full) | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` (простые таблицы: `CROSS JOIN` / `LEFT JOIN ... ON true`) |
| ClickHouse | `as \`t1\`` | обязателен | поддержаны left/right/full | не поддерживается (`NotSupportedException`) |
| In-memory | неприменимо (выполнение через делегаты) | неприменимо | поддержаны inner/left/right/full/cross; APPLY и источники в виде табличных функций — нет | не поддерживается (включая коррелированный apply) |

Провайдер in-memory компилирует условие соединения в делегат и выполняет цикл, поэтому он не
генерирует SQL; он поддерживает соединения [`Inner`](xref:NextORM.Core.JoinType.Inner), [`Left`](xref:NextORM.Core.JoinType.Left), [`Right`](xref:NextORM.Core.JoinType.Right), [`Full`](xref:NextORM.Core.JoinType.Full) и [`Cross`](xref:NextORM.Core.JoinType.Cross) (см.
`tests/nextorm.core.tests/InMemoryJoinTests.cs`). [`CrossApply`](xref:NextORM.Core.EntityBuilder`1.CrossApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions}))/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1.OuterApply``1(NextORM.Core.EntityBuilder{``0},System.Action{NextORM.Core.JoinOptions})) существуют только для SQL и
выбрасывают `NotSupportedException` в провайдере in-memory, как и остальные неподдерживаемые типы
соединений. Соединения через `JoinedEntityBuilder<T1..T8>` разрешаются на этапе построения запроса у каждого
провайдера.

## См. также

- [Подзапросы](05-subqueries.md) - присоединённый `QueryCommand<T>` — это производная таблица.
- [Группировка и агрегаты](03-grouping-and-aggregates.md) - агрегат по соединению.
- [Хинты запросов](13-query-hints.md) - хинты уровня инструкции, например SQL Server `OPTION (RECOMPILE)`.
- [Связи и однозапросная загрузка (`JoinInto`)](../advanced/relationships.md) - объявленные метаданные связей и однозапросный загрузчик дочерней коллекции.
- [Специфичный для провайдеров SQL](provider-specific/overview.md) - полный каталог конструкций, доступных только у отдельных провайдеров.
- [Запросы и проекции](../querying/index.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.Join.cs:9`,
`tests/nextorm.core.tests/InMemoryJoinTests.cs:14`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:320`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:379`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:312`
(and the other provider `SqlGenerationTests.cs`).
