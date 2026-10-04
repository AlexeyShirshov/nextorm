# Обобщённые табличные выражения (CTE)

> Объявляйте один или несколько именованных `with`-запросов и используйте их в качестве источника `from`
> для запроса, включая рекурсивные CTE для последовательностей и иерархий.

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Запросы и проекции](../querying/index.md) · [Операции над множествами](06-set-operations.md)

## Обзор

CTE объявляется с помощью `With(name, query)` (нерекурсивный) или `WithRecursive(name, query, maxRecursion)`
(рекурсивный) в [`IDataContext`](xref:NextORM.Core.IDataContext). Оба являются методами расширения ([`DataContextExtensions`](xref:NextORM.Core.DataContextExtensions)), и оба возвращают
область видимости [`CteQuery`](xref:NextORM.Core.CteQuery), которая хранит объявления, собранные на данный момент, в [`Ctes`](xref:NextORM.Core.CteQuery.Ctes):

```csharp
public static CteQuery With(this IDataContext dataContext, string name, QueryCommand query);

public static CteQuery WithRecursive(this IDataContext dataContext, string name, QueryCommand query,
    int? maxRecursion = null);
```

Объявления неизменяемы: каждый вызов [`With`](xref:NextORM.Core.DataContextExtensions.With(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand))/[`WithRecursive`](xref:NextORM.Core.DataContextExtensions.WithRecursive(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand,System.Nullable{System.Int32})) возвращает **новую** область видимости, которая
добавляет [`CteDefinition`](xref:NextORM.Core.CteDefinition) к предыдущим. Определение фиксирует имя, [`QueryCommand`](xref:NextORM.Core.QueryCommand), который его создаёт, и
может ли тело ссылаться на собственное имя.

[`From`](xref:NextORM.Core.CteQuery.From(NextORM.Core.CteDefinition)) (или `From(CteDefinition)`) начинает новый запрос, чей `from` — один из
объявленных CTE, перенося каждое объявление в результирующую команду. Возвращается обычный
[`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1) над режимом [`TableAlias`](xref:NextORM.Core.TableAlias) без сущности, поэтому доступен
**полный набор операторов** — [`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}}))/[`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}},System.Action{NextORM.Core.JoinOptions}))/[`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}}))/[`Having`](xref:NextORM.Core.EntityBuilder`1.Having(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}}))/[`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32))/[`Limit`](xref:NextORM.Core.EntityBuilder`1.Limit(System.Int32))/[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})).
Столбцы CTE читаются по имени (`t["id"].AsInt` или `t.GetInt64("id")`). Рекурсивные тела ссылаются на
собственное имя тем же способом (`dataContext.From("nums")` внутри шагового запроса).

Рендеринг: диалекты, использующие форму ANSI, выводят `with recursive`, когда любое определение рекурсивно
(SQLite, PostgreSQL); SQL Server объявляет рекурсивный CTE только с `with` и добавляет параметр глубины
после инструкции.

## Нерекурсивный CTE

```csharp
var recent = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id });

var rows = dataContext
    .With("recent", recent)
    .From("recent")
    .Select(t => new { Id = t["id"].AsInt })
    .ToList();
```

```sql
-- SQLite (all providers produce the same shape)
with recent as (select id from complex_entity where (id > 1)) select id from recent
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Id |
|----|
| 2 |
| 3 |

## Типизированный CTE

`With` читает CTE через [`TableAlias`](xref:NextORM.Core.TableAlias), поэтому к каждому столбцу обращаются
строкой. Если определяющий запрос ещё под рукой, [`AsCte`](xref:NextORM.Core.QueryCommand`1.AsCte(System.String))
объявляет тот же обычный CTE типизированным дескриптором, а `From(Cte<T>)` читает его с доступом к членам и
выводом типов:

```csharp
var recent = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id })
    .AsCte("recent");

var rows = dataContext
    .From(recent)
    .Select(r => new { r.Id })
    .ToList();
```

```sql
-- SQLite
with recent as (select id from complex_entity where (id > 1)) select id from recent as 't1'
```

[`QueryCommand<T>.AsCte`](xref:NextORM.Core.QueryCommand`1.AsCte(System.String)) возвращает неизменяемый
[`Cte<TResult>`](xref:NextORM.Core.Cte`1), чей `TResult` — ровно проекция `Select`;
`From(Cte<T>)` возвращает обычный [`EntityBuilder<TResult>`](xref:NextORM.Core.EntityBuilder`1), поэтому
доступен **полный набор операторов** (`Where`/`Join`/`GroupBy`/`OrderBy`/`Limit`/`Select`), а члены (`r.Id`)
заменяют `t["id"]` / `t.GetInt64("id")`. CTE — это **типизированный источник проекции, а не mapped-сущность**:
whole-entity `TResult` не делает внешний источник таблицей, а члены разрешаются в выходные алиасы
определяющей проекции, перенося её алиасы и сконфигурированные конвертеры/provider-типы — без DTO-remapping.

Строковый API не меняется: `With`/`WithRecursive`/`From(string)`/`From(CteDefinition)` по-прежнему
объявляют и читают CTE по имени и остаются доступными. Рекурсивный CTE можно также объявить через
типизированную поверхность `AsRecursiveCte`, описанную ниже.

Типизированное объявление переиспользует тот же механизм, поэтому разнородные дескрипторы и self-join
компонуются обычным образом. Один и тот же экземпляр дескриптора, использованный дважды, объявляется один
раз; разные `TResult` независимы:

```csharp
var recent = dataContext.From<IComplexEntity>()
    .Select(x => new { x.Id, x.String })
    .AsCte("recent");
var other = dataContext.From<ISimpleEntity>()
    .Select(y => new { y.Id })
    .AsCte("other");

var joined = dataContext.From(recent)
    .Join(dataContext.From(other), (r, o) => r.Id == o.Id)
    .Select(p => new { Id = p.Item1.Id, Name = p.Item1.String, Other = p.Item2.Id })
    .ToList();

// self-join: одно объявление, два внешних алиаса
var pairs = dataContext.From(recent)
    .Join(dataContext.From(recent), (a, b) => a.Id == b.Id)
    .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
    .ToList();
```

```sql
-- SQLite: разнородные дескрипторы
with recent as (select id as 'Id', somestring as 'String' from complex_entity), other as (select id as 'Id' from simple_entity) select t1.Id, t1.String, t2.Id from recent as 't1' join other as 't2' on t1.Id = cast(t2.Id as bigint)

-- SQLite: self-join одного дескриптора -> одно объявление, два алиаса
with recent as (select id as 'Id', somestring as 'String' from complex_entity) select t1.Id, t2.Id from recent as 't1' join recent as 't2' on t1.Id = t2.Id
```

Зависимости поднимаются автоматически: `From(Cte<T>)` присоединяет объявление дескриптора вместе с каждым
объявлением, на которое его тело транзитивно ссылается (вложенные тела CTE, join, derived-подзапросы, ветви
set-операций), в порядке «зависимость раньше потребителя», и опускает объявления, которые тело несёт, но
никогда не использует. Дескриптор, использованный более одного раза, даёт одно объявление; два разных
объявления с одним именем по-прежнему отклоняются, как и в строковом API.

Глобальные entity-фильтры **не** внедряются в типизированный источник — ни в основной `From(Cte<T>)`, ни в
присоединённый; собственные фильтры определяющего запроса остаются внутри тела CTE.

## Типизированный рекурсивный CTE

Типизированный рекурсивный CTE объявляется из своего **якоря** через `AsRecursiveCte`. Он принимает имя,
колбэк, строящий шаг из типизированной самоссылки, и (во второй перегрузке) обязательный предел глубины
рекурсии:

```csharp
public Cte<TResult> AsRecursiveCte(string name,
    Func<CteReference<TResult>, QueryCommand<TResult>> step);

public Cte<TResult> AsRecursiveCte(string name,
    Func<CteReference<TResult>, QueryCommand<TResult>> step, int maxRecursion);
```

Колбэк вызывается **ровно один раз**, во время вызова `AsRecursiveCte`; он никогда не вызывается повторно
при подготовке, генерации SQL или выполнении. Внутри него
[`dataContext.From(reference)`](xref:NextORM.Core.CteReference`1) возвращает обычный
[`EntityBuilder<TResult>`](xref:NextORM.Core.EntityBuilder`1), читающий CTE, а доступ к членам разрешается по
форме проекции якоря. Шаговый запрос объединяется с якорем как `anchor UNION ALL step`
**автоматически** — ручного `UnionAll` и второго объявления нет.

Самоссылка **привязана к владельцу**: [`CteReference<TResult>`](xref:NextORM.Core.CteReference`1) действительна
только пока выполняется получивший её колбэк. Чтение её вне этого колбэка (захваченная или чужая ссылка,
даже с тем же именем) либо чтение самоссылки из якоря бросает `InvalidOperationException`
**до построения любой команды БД**. Взаимной рекурсии нет: рекурсивный CTE может ссылаться только на
собственную самоссылку и никогда на другое рекурсивное определение.

```csharp
// Якорь: id 1. Шаг: прибавляем 1, пока значение меньше 5 -> 1, 2, 3, 4, 5.
var numbers = dataContext.From<ISimpleEntity>()
    .Where(s => s.Id == 1)
    .Select(s => s.Id)
    .AsRecursiveCte("nums", self => dataContext.From(self)
        .Where(n => n < 5)
        .Select(n => n + 1));

var rows = dataContext.From(numbers).ToList();
```

```sql
-- SQLite
with recursive nums as (select id as 'Id' from simple_entity
 where id = 1
 union all
 select (Id + 1) as 'c0' from nums as 't1'
 where (t1.Id < 5)) select id from nums as 't1'
```

Та же поверхность покрывает анонимную проекцию с несколькими слотами:

```csharp
var numbers = dataContext.From<ISimpleEntity>()
    .Where(s => s.Id == 1)
    .Select(s => new { s.Id, Next = s.Id + 1 })
    .AsRecursiveCte("nums", self => dataContext.From(self)
        .Where(n => n.Id < 4)
        .Select(n => new { Id = n.Id + 1, Next = n.Next + 1 }));

var rows = dataContext.From(numbers)
    .Select(n => new { n.Id, n.Next })
    .ToList();
```

```sql
-- SQLite
with recursive nums as (select id as 'Id', (id + 1) as 'Next' from simple_entity
 where id = 1
 union all
 select (id + 1) as 'Id', (Next + 1) as 'Next' from nums as 't1'
 where (t1.Id < 4)) select Id, Next from nums as 't1'
```

... и DTO-проекция, например member-init `CteNumberRow` из строкового API ниже:

```csharp
public sealed class CteNumberRow
{
    public int n { get; set; }
}

var numbers = dataContext.From<ISimpleEntity>()
    .Where(s => s.Id == 1)
    .Select(s => new CteNumberRow { n = s.Id })
    .AsRecursiveCte("nums", self => dataContext.From(self)
        .Where(n => n.n < 5)
        .Select(n => new CteNumberRow { n = n.n + 1 }));

var rows = dataContext.From(numbers)
    .Select(n => new CteNumberRow { n = n.n })
    .ToList();
```

```sql
-- SQLite
with recursive nums as (select id as 'n' from simple_entity
 where id = 1
 union all
 select (n + 1) as 'n' from nums as 't1'
 where (t1.n < 5)) select n from nums as 't1'
```

### Форму задаёт якорь

Читаемые столбцы рекурсивного CTE, их порядок и алиасы берутся из проекции **якоря**, а не шага. Шаг
должен воспроизвести эту форму слот в слот; собственные алиасы шага не переименовывают столбцы CTE. До
того как любой SQL достигнет базы, каждая подготовка проверяет шаг против якоря по каждому листу и
бросает `InvalidOperationException`, если различается:

* **число** столбцов;
* **слот члена** самоссылки в этой позиции (простой доступ к члену должен называть столбец якоря);
* объявленный **тип CLR**;
* связанный **provider-тип** (тип, привязанный конвертером, или подготовленный provider-тип);
* **nullability** (является ли тип CLR типом `Nullable<T>`).

Сообщение называет CTE и позицию шага, например:

```text
The recursive common table expression 'reordered' is invalid at step position 0: the step reads the
self-reference member 'Total' at position 0, but the anchor names that column 'Id'.
```

Проверка выполняется при каждой подготовке, включая попадание в кэш планов, поэтому кэшированный план не
может обойти контракт «якорь/шаг».

### Глубина рекурсии (`maxRecursion`)

Двухаргументная перегрузка **не** выводит подсказку глубины: SQL Server использует собственное значение
по умолчанию (`100`). Трёхаргументная перегрузка принимает обязательный `int` (nullable-значения по
умолчанию нет) и выводит `option (maxrecursion n)` только на SQL Server; `0` означает «без ограничения»,
положительное значение — максимальная глубина рекурсии. Остальные провайдеры игнорируют подсказку и
используют собственное значение по умолчанию — ровно как строковый API.

```csharp
var numbers = dataContext.From<ISimpleEntity>()
    .Where(s => s.Id == 1)
    .Select(s => s.Id)
    .AsRecursiveCte("nums", self => dataContext.From(self)
        .Where(n => n < 5)
        .Select(n => n + 1), 100);

var rows = dataContext.From(numbers).ToList();
```

```sql
-- SQL Server: без ключевого слова `recursive`, опция глубины в конце
with nums as (select id as [Id] from simple_entity
 where id = 1
 union all
 select (Id + 1) as [c0] from nums as [t1]
 where (t1.Id < 5)) select id from nums as [t1] option (maxrecursion 100)
```

### Различия провайдеров (типизированная рекурсия)

| Провайдер | Типизированный рекурсивный CTE |
|---|---|
| SQLite | Поддерживается; объявляет `with recursive`; `maxRecursion` игнорируется. |
| PostgreSQL | Поддерживается; объявляет `with recursive`; `maxRecursion` игнорируется. |
| SQL Server | Поддерживается; объявляет `with` (без ключевого слова `recursive`); двухаргументная перегрузка не выводит подсказку (значение сервера по умолчанию), трёхаргументная выводит `option (maxrecursion n)` (`0` = без ограничения, положительное = глубина). |
| MySQL | Поддерживается; объявляет `with recursive`; `maxRecursion` игнорируется. |
| MariaDB | Поддерживается; объявляет `with recursive`; `maxRecursion` игнорируется. |
| ClickHouse | **Исключён**: типизированный рекурсивный CTE бросает `NotSupportedException` до вывода любого SQL. Обычный (нерекурсивный) типизированный CTE на ClickHouse по-прежнему работает. |
| In-memory | Не применимо: рекурсию рендерят SQL-диалекты. |

Устаревший строковый `WithRecursive` **не** гейтится этой проверкой возможности и сохраняет прежнее
поведение на каждом провайдере.

Каждый пример выше ограничен завершающим предикатом в шаге; никогда не стройте неограниченный
рекурсивный CTE.

## Цепочка объявлений

Каждый [`With`](xref:NextORM.Core.DataContextExtensions.With(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand)) добавляется к предыдущей области видимости, поэтому более поздний CTE может быть определён
через более ранний. Объявления рендерятся в порядке объявления:

```csharp
var first = dataContext.From<IComplexEntity>()
    .Where(x => x.Id > 1)
    .Select(x => new { x.Id });

var second = dataContext.From("first")
    .Select(t => new { id = t["id"].AsInt });

var rows = dataContext
    .With("first", first)
    .With("second", second)
    .From("second")
    .Select(t => new { id = t["id"].AsInt })
    .ToList();
```

```sql
with first as (select id from complex_entity where (id > 1)), second as (select id from first) select id from second
```

> **Вложенные CTE поднимаются автоматически.** Тело CTE, само несущее `WITH` — запрос, построенный как
> `dataContext.With(...).From(...)` и переданный телом другого `With`, — разворачивается в один общий
> `with` верхнего уровня, а не рендерится буквально вложенным. Поднятие выводит каждую зависимость до
> потребляющего её CTE, даже если зависимость — соседнее объявление, расположенное ниже, и сохраняет
> исходный относительный порядок объявлений, не зависящих друг от друга. Объявление печатается только
> один раз, если многократно ссылаются на один и тот же экземпляр `CteDefinition`; два разных
> экземпляра с одним именем отклоняются через `InvalidOperationException` (один `WITH` не может связать
> одно имя с двумя определениями).
>
> Тогда каждый провайдер видит плоскую переносимую форму. В частности, SQL Server всегда печатает
> обычный список `with name as (...)` — но никогда `with recursive`; в T-SQL рекурсивный CTE
> объявляется одним `with`.
>
> Объявление, которое невозможно перенести в `WITH` верхнего уровня — вложенное в derived-table
> подзапрос, коррелированную ссылку или ветвь set-операции, — приводит к немедленному
> `InvalidOperationException` до рендеринга SQL, вместо непереносимой вложенной формы.

`From(CteDefinition)` эквивалентен `From(definition.Name)` и удобен, когда вы сохранили область видимости,
а не имя:

```csharp
var cte = dataContext.With("recent", recent);
var rows = cte.From(cte.Ctes[0])
    .Select(t => new { id = t["id"].AsInt })
    .ToList();
```

## Композиция поверх CTE

Источник CTE — это обычный generic-билдер, поэтому над CTE работает всё, что работает над таблицей:
фильтрация, агрегация, сортировка, пагинация и join. Более поздний CTE может агрегировать более ранний
по имени:

```csharp
var first = dataContext.From<IComplexEntity>()
    .Select(x => new { x.Id, name = x.String });

var second = dataContext.From("first")
    .GroupBy(t => new { name = t.GetString("name") })
    .Select(t => new { name = t.GetString("name"), count = SqlFunctions.Sql.count() });

var rows = dataContext
    .With("first", first)
    .With("second", second)
    .From("second")
    .OrderByDescending(t => t.GetInt32("count"))
    .Select(t => new { name = t.GetString("name"), count = t.GetInt32("count") })
    .ToList();
```

```sql
-- SQLite
with first as (select id, somestring as 'name' from complex_entity),
     second as (select name, count(*) as 'count' from first group by name)
select name, count from second order by count desc
```

CTE можно соединить с другим CTE (или с таблицей). Обе стороны адресуются по имени, и каждый столбец
`TableAlias` квалифицируется именем таблицы, поэтому общий столбец двух CTE не становится
неоднозначным:

```csharp
var left = dataContext.From<IComplexEntity>().Select(x => new { x.Id });
var right = dataContext.From<ISimpleEntity>().Select(x => new { x.Id });

var rows = dataContext
    .With("l", left)
    .With("r", right)
    .From("l")
    .Join(dataContext.From("r"), (l, r) => l.GetInt64("id") == r.GetInt64("id"))
    .Select(p => new { Id = p.Item1.GetInt64("id"), Other = p.Item2.GetInt64("id") })
    .ToList();
```

```sql
-- SQLite
with l as (select id from complex_entity), r as (select id from simple_entity) select t1.id, t2.id from l as 't1' join r as 't2' on t1.id = t2.id
```

> У источника CTE нет сопоставленной сущности, поэтому столбцы читаются по имени (`t.GetInt64("id")` /
> `t["id"].AsInt`), и имена должны совпадать с выходными алиасами тела CTE. Называйте элементы проекции
> по SQL-алиасам (нижний `snake_case`), чтобы внешние ссылки оставались точными.

Скоуп CTE также управляет multi-table `UPDATE`/`DELETE`. Разместите CTE на **присоединяемой** стороне,
а целью оставьте физическую таблицу; объявление поднимается перед мутацией:

```csharp
var recent = dataContext
    .With("recent", dataContext.From<IOrder>().Where(o => o.Id > 1000).Select(o => new { o.Id }));

dataContext.From<IOrder>()
    .Join(recent.From("recent"), (o, r) => o.Id == r.GetInt64("id"))
    .CreateUpdateJoinBuilder()
    .Set(p => p.Item1.Status, "archived")
    .Update();
```

```sql
-- PostgreSQL
with recent as (select id from orders where (id > 1000)) update orders as "t1" set status = @p0 from recent as "t2" where t1.id = t2.id
```

Это работает на каждом провайдере с `UPDATE ... FROM`/`JOIN` (и `DELETE ... USING`/join), на любой
позиции join, и для рекурсивных CTE. См. [Data modification (UPDATE)](17-update-statement.md#обновление-из-join)
и [Data modification (DELETE)](16-delete-statement.md#удаление-по-соединению).

## Рекурсивный CTE: числовая последовательность

Рекурсивный CTE — это `union all` **якоря** (нерекурсивного запроса) и **шага**, который читает CTE по
имени и останавливается, когда предикат перестаёт совпадать. Вызовите [`WithRecursive`](xref:NextORM.Core.DataContextExtensions.WithRecursive(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand,System.Nullable{System.Int32})), передав объединение
в качестве тела:

```csharp
public sealed class CteNumberRow
{
    public int n { get; set; }
}

var anchor = dataContext.From<ISimpleEntity>()
    .Where(s => s.Id == 1)
    .Select(s => new CteNumberRow { n = s.Id });

var step = dataContext.From("nums")
    .Where(t => t["n"].AsInt < 5)
    .Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });

var body = anchor.UnionAll(step);

var numbers = dataContext
    .WithRecursive("nums", body)
    .From("nums")
    .Select(t => new CteNumberRow { n = t["n"].AsInt })
    .ToList();

// numbers -> 1, 2, 3, 4, 5
```

```sql
-- SQLite: `with recursive` prefix
with recursive nums as (select id as 'n' from simple_entity where id = 1 union all select (n + 1) as 'n' from nums where (n < 5)) select n from nums
```

Вывод:

| n |
|---|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |

`maxRecursion` — необязательный предел глубины. Только SQL Server имеет опцию уровня инструкции для него,
и диалект добавляет `option (maxrecursion n)` в конец инструкции; SQLite и PostgreSQL игнорируют его и
используют собственное значение по умолчанию:

```csharp
var numbers = dataContext
    .WithRecursive("nums", body, maxRecursion: 100)
    .From("nums")
    .Select(t => new CteNumberRow { n = t["n"].AsInt })
    .ToList();
```

```sql
-- SQL Server: no `recursive` keyword, depth option appended
with nums as (select id as [n] from simple_entity where id = 1 union all select (n + 1) as [n] from nums where (n < 5)) select n from nums option (maxrecursion 100)
```

## CTE и захваченный параметр

Захваченное значение в теле CTE становится параметром точно так же, как и везде, и проход извлечения
параметров обходит предложение `with`, а не только внешнюю инструкцию:

```csharp
var threshold = 1L;

var prepared = dataContext
    .With("recent", dataContext.From<IComplexEntity>()
        .Where(x => x.Id > threshold)
        .Select(x => new { x.Id }))
    .From("recent")
    .Select(t => new { id = t["id"].AsInt })
    .Prepare();

var ids = prepared.ToList(dataContext);
```

```sql
-- SQLite parameter placeholder; SQL Server/PostgreSQL use @threshold
with recent as (select id from complex_entity where (id > $threshold)) select id from recent
```

## Переиспользование кэша планов

Определения CTE участвуют в ключе кэша планов, поэтому два запроса, отличающиеся только телом CTE,
**не** разделяют кэшированный план. Наоборот, заново построенные, но структурно одинаковые цепочки
переиспользуют кэшированный план, включая рекурсивный CTE, тело которого — `union all` двух свежих команд:

* кэшированный план CTE заново извлекает захваченный параметр CTE при попадании в кэш
  (`PlanCacheTests.Cte_WithCapturedParam_RepeatedExecution_ShouldRefreshParam`);
* два разных тела CTE порождают два плана
  (`PlanCacheTests.Cte_DifferentDefinitions_ShouldNotSharePlan`);
* эквивалентный рекурсивный CTE, построенный снова, переиспользует кэшированный план
  (`PlanCacheTests.RecursiveCte_FreshCommand_ShouldReuseCachedPlan`).

См. [Переиспользование запросов: кэш против Prepare](../infrastructure/01-query-reuse-and-caching.md) о времени жизни и правилах
инвалидации кэша планов.

## Модифицирующий CTE (PostgreSQL)

PostgreSQL — единственный поддерживаемый провайдер, принимающий модифицирующую инструкцию как тело CTE
(`WITH <имя> AS (<INSERT|UPDATE|DELETE> ... RETURNING ...)`); гейтится
[`SupportsDataModifyingCtes`](xref:NextORM.Core.ISqlDialect.SupportsDataModifyingCtes). nextorm открывает
её перегрузками `With(имя, mutation)` у `IDataContext`/`CteQuery`, принимающими возвращающий строки
`INSERT`, `UPDATE` или `DELETE`, и возвращающими
[`MutationCteQuery<TResult>`](xref:NextORM.Core.MutationCteQuery`1), типизированный по проекции
`RETURNING`. Проекция `RETURNING` обязательна — тело только с побочным эффектом вне области охвата — и
принимаются как однотабличные мутации, так и join/multi-table на INNER-соединениях PostgreSQL для
арностей 2–8: multi-table `UPDATE ... FROM` через
[`UpdateJoinBuilder<TProjection>.Returning()`](xref:NextORM.Core.UpdateJoinBuilder`1.Returning) или
[`Returning(projection)`](xref:NextORM.Core.UpdateJoinBuilder`1.Returning``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})),
а multi-table `DELETE ... USING` — через
[`CreateDeleteJoinBuilder()`](xref:NextORM.Core.DataContextExtensions.CreateDeleteJoinBuilder``2(NextORM.Core.JoinedEntityBuilder{``0,``1}))
у соединённого билдера, возвращающий [`DeleteJoinBuilder<TProjection>`](xref:NextORM.Core.DeleteJoinBuilder`1),
чей [`Returning()`](xref:NextORM.Core.DeleteJoinBuilder`1.Returning)/[`Returning(projection)`](xref:NextORM.Core.DeleteJoinBuilder`1.Returning``1(System.Linq.Expressions.Expression{System.Func{`0,``0}}))
переключает на терминал возврата строк. `Returning()` — эквивалент `Returning(p => p)` — возвращает всю
соединённую проекцию; форма с явной проекцией не меняется. Остальные провайдеры отклоняют
`With(имя, mutation)` с `NotSupportedException`, так как их тело CTE обязано быть `SELECT`. В отличие от
read-CTE, инструкция, в `WITH` которой есть модифицирующий CTE, никогда не попадает в кэш планов (она
имеет побочный эффект) и планируется заново при каждом вызове.

```csharp
// Тело UPDATE по одной таблице: обновляем, затем типизированно читаем обновлённые строки.
var updated = dataContext
    .With("upd", dataContext.CreateUpdateBuilder<IOrder>()
        .Set(x => x.Total, 0)
        .Where(x => x.CustomerId == 7)
        .Returning(x => new { x.Id, x.Total }))
    .From("upd")
    .Select(r => new { r.Id, r.Total })
    .ToList();
```

```sql
-- PostgreSQL
with upd as (update orders set total = @p0 where customer_id = 7 returning id, total) select id, total from upd as "t1"
```

```csharp
// Тело multi-table DELETE: удаляем строки цели, совпавшие по соединению, возвращая колонки обеих сторон.
var doomed = dataContext
    .From<IOrder>()
    .Join(dataContext.From<ICustomer>(), (o, c) => o.CustomerId == c.Id)
    .CreateDeleteJoinBuilder()
    .Returning(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.Name });

var removed = dataContext
    .With("del", doomed)
    .From("del")
    .Select(r => new { r.OrderId, r.CustomerName })
    .ToList();
```

```sql
-- PostgreSQL
with del as (delete from orders as "t1" using customers as "t2" where t1.customer_id = t2.id returning t1.id as "OrderId", t2.name as "CustomerName") select "OrderId", "CustomerName" from del as "t1"
```

```csharp
// Тело по всей проекции: .Returning() возвращает каждый слот, а читающая сторона адресует их по ItemN.
var doomed = dataContext
    .From<IOrder>()
    .Join(dataContext.From<ICustomer>(), (o, c) => o.CustomerId == c.Id)
    .CreateDeleteJoinBuilder()
    .Returning();   // эквивалент .Returning(p => p)

var removed = dataContext
    .With("del", doomed)
    .From("del")
    .Select(r => new { OrderId = r.Item1.Id, CustomerName = r.Item2.Name })
    .ToList();
```

Тело по всей проекции выводит **каждую** возвращаемую колонку обоих слотов под детерминированными
алиасами `__sN_*` (`__s1_*` — цель, `__s2_*` — присоединённая сторона), поэтому читающая сторона
адресует сохранённый алиас, а не имя CLR-члена:

```sql
-- PostgreSQL: читающая сторона тела по всей проекции выше
select "__s1_id", "__s2_name" from del as "t1"
```

Write-CTE документируется вместе с поверхностью записи, к которой принадлежит — типизированное чтение через
`From`/`FromTable`, тело `VALUES` или `INSERT ... SELECT`, чтение более раннего read-CTE и питание
главного `INSERT ... SELECT` — в разделе
[Изменение данных (INSERT): Модифицирующий CTE](15-insert-statement.md#модифицирующий-cte-postgresql).
Общая поверхность `UPDATE` — в [Изменении данных (UPDATE)](17-update-statement.md), `DELETE` — в
[Изменении данных (DELETE)](16-delete-statement.md).

## Различия между провайдерами

| Провайдер | Поведение |
|---|---|
| SQLite | `with` для нерекурсивных, `with recursive` для рекурсивных; опции глубины нет. |
| SQL Server | Рекурсивные CTE объявляются только с `with` (без ключевого слова `recursive`); `maxRecursion` рендерится как `option (maxrecursion n)` в конце инструкции. |
| PostgreSQL | `with` / `with recursive`; опции глубины нет. |
| MySQL | `with` для нерекурсивных, `with recursive` для рекурсивных; опции глубины нет. |
| MariaDB | `with` / `with recursive`; опции глубины нет. |
| ClickHouse | Каждый CTE объявляется простым `with`; рекурсивные CTE не поддерживаются. |
| In-memory | Не применимо: CTE рендерятся SQL-диалектами и не являются частью провайдера in-memory. |

## См. также

* [Операции над множествами](06-set-operations.md) — [`UnionAll`](xref:NextORM.Core.QueryCommand`1.UnionAll``1(NextORM.Core.QueryCommand{``0})) и другие, используются для построения рекурсивного тела.
* [Соединения](02-joins.md) — соединение CTE с таблицей, как в `CommonTestSuite.Cte.cs`.
* [Необработанный SQL](12-raw-sql.md) — когда вся инструкция написана вручную.
* [Переиспользование запросов: кэш против Prepare](../infrastructure/01-query-reuse-and-caching.md) — как кэшируются планы CTE.

---

Source: `src/nextorm.core/Builders/CteQuery.cs:7`, `src/nextorm.core/DataContext/DataContextExtensions.cs:9`;
`tests/nextorm.integration.tests/CommonTestSuite.Cte.cs:14`, `tests/nextorm.integration.tests/CommonTestSuite.Cte.cs:33`;
`tests/nextorm.core.tests/CteQueryTests.cs:8`;
типизированный рекурсивный CTE: `src/nextorm.core/Cte.cs:15`, `src/nextorm.core/Query/QueryCommand.TResult.cs:1100`,
`src/nextorm.core/DataContext/SqlSourceRenderer.cs:35`, `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:981`;
`tests/nextorm.core.tests/TypedCteTests.cs:1057`, `tests/nextorm.sqlite.tests/TypedCteTests.cs:718`;
`tests/nextorm.sqlite.tests/PlanCacheTests.cs:190`, `tests/nextorm.sqlite.tests/PlanCacheTests.cs:343`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1188`, `:1202`, `:1216`, `:1231`, `:1248`;
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:827`, `:856`;
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:759`, `:788`.
