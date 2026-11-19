# Обобщённые табличные выражения (CTE)

> Объявляйте один или несколько именованных `with`-запросов и используйте их в качестве источника `from`
> для запроса, включая рекурсивные CTE для последовательностей и иерархий.

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Запросы и проекции](../engine/index.md) · [Операции над множествами](08-set-operations.md)

## Обзор

Обобщённое табличное выражение (CTE) даёт запросу имя, чтобы другой запрос мог использовать его
результат как источник. Для обычного CTE вызовите `With(name, query)`, а для рекурсивного —
`WithRecursive(name, query, maxRecursion)`. Оба метода расширяют
[`IDataContext`](xref:NextORM.Core.IDataContext) через
[`DataContextExtensions`](xref:NextORM.Core.DataContextExtensions):

```csharp
public static CteQuery With(this IDataContext dataContext, string name, QueryCommand query);

public static CteQuery WithRecursive(this IDataContext dataContext, string name, QueryCommand query,
    int? maxRecursion = null);
```

Каждый метод возвращает область [`CteQuery`](xref:NextORM.Core.CteQuery), которая собирает объявления
в [`Ctes`](xref:NextORM.Core.CteQuery.Ctes). Объявления неизменяемы: каждый вызов
[`With`](xref:NextORM.Core.DataContextExtensions.With(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand))
или
[`WithRecursive`](xref:NextORM.Core.DataContextExtensions.WithRecursive(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand,System.Nullable{System.Int32}))
возвращает **новую** область и добавляет в неё
[`CteDefinition`](xref:NextORM.Core.CteDefinition). Определение хранит имя, создающий его
[`QueryCommand`](xref:NextORM.Core.QueryCommand) и признак того, может ли тело ссылаться на собственное
имя.

Вызовите [`From`](xref:NextORM.Core.CteQuery.From(NextORM.Core.CteDefinition)) с определением, чтобы
начать запрос по этому CTE. Новая команда переносит объявления с собой. `From` возвращает обычный
[`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1) в режиме
[`TableAlias`](xref:NextORM.Core.TableAlias) без сущности, поэтому доступны `Where`, `Join`, `GroupBy`,
`Having`, `OrderBy`, `Limit` и `Select`.

В строковом API обращайтесь к столбцам CTE по имени (`t["id"].AsInt` или `t.GetInt64("id")`). Тело
рекурсивного CTE ссылается на себя так же: через `dataContext.From("nums")` в шаговом запросе.

Синтаксис зависит от диалекта. SQLite и PostgreSQL добавляют `with recursive`, если есть хотя бы одно
рекурсивное определение. SQL Server использует только `with` и добавляет параметр глубины после
инструкции.

## Нерекурсивный CTE

В примере запрос фильтрует строки, получает имя `recent`, а затем читает их во внешнем запросе:

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
with recent as (select id
 from complex_entity where (id > 1)) select id
 from recent
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/TestProviders/SqliteTestProvider.cs`).

Вывод:

| Id |
|----|
| 2 |
| 3 |

## Типизированный CTE

Строковый API `With` открывает столбцы CTE через
[`TableAlias`](xref:NextORM.Core.TableAlias), поэтому к ним обращаются по имени. Если определяющий
запрос ещё доступен, вызовите [`AsCte`](xref:NextORM.Core.QueryCommand`1.AsCte(System.String)), чтобы
создать типизированный дескриптор. После этого `From(Cte<T>)` читает CTE через члены проекции и
выводит их типы:

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
with recent as (select id
 from complex_entity where (id > 1)) select id
 from recent as 't1'
```

[`QueryCommand<T>.AsCte`](xref:NextORM.Core.QueryCommand`1.AsCte(System.String)) возвращает
неизменяемый [`Cte<TResult>`](xref:NextORM.Core.Cte`1), где `TResult` в точности соответствует
проекции `Select`. `From(Cte<T>)` возвращает обычный
[`EntityBuilder<TResult>`](xref:NextORM.Core.EntityBuilder`1), поэтому доступны `Where`, `Join`,
`GroupBy`, `OrderBy`, `Limit` и `Select`. Обращайтесь к членам, например `r.Id`, вместо `t["id"]` или
`t.GetInt64("id")`.

CTE — это **типизированный источник проекции, а не сопоставленная сущность**. Даже если `TResult`
описывает сущность целиком, внешний источник не становится таблицей. Члены разрешаются в выходные
алиасы определяющей проекции и наследуют её алиасы, настроенные конвертеры и типы провайдера; DTO
повторно не сопоставляется.

Строковый API остаётся доступен наряду с типизированным: используйте `With`, `WithRecursive`,
`From(string)` или `From(CteDefinition)`, чтобы объявлять и читать CTE по имени. Для типизированного
рекурсивного CTE используйте описанный ниже `AsRecursiveCte`.

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
with recent as (select id as 'Id', somestring as 'String'
 from complex_entity), other as (select id as 'Id'
 from simple_entity) select t1.Id, t1.String, t2.Id
 from recent as 't1'
 join other as 't2' on t1.Id = cast(t2.Id as bigint)

-- SQLite: self-
join одного дескриптора -> одно объявление, два алиаса
with recent as (select id as 'Id', somestring as 'String'
 from complex_entity) select t1.Id, t2.Id
 from recent as 't1'
 join recent as 't2' on t1.Id = t2.Id
```

`From(Cte<T>)` также автоматически собирает зависимости дескриптора. В результат входят все
объявления, на которые тело ссылается прямо или через вложенные тела CTE, соединения, производные
подзапросы и ветви операций над множествами. Зависимость располагается перед использующим её CTE.
Неиспользуемые объявления опускаются.

Повторное использование одного дескриптора добавляет только одно объявление. Два разных объявления
с одинаковым именем по-прежнему отклоняются, как и в строковом API.

Если принимающий билдер уже содержит контекст, передайте дескриптор прямо в метод соединения и не
преобразуйте его предварительно через `dataContext.From(cte)`. См.
[Прямое соединение типизированного CTE](#прямое-соединение-типизированного-cte). Такой вызов работает
со всеми операторами соединения, включая дескрипторы `AsCte` с анонимной проекцией.

Глобальные entity-фильтры **не** внедряются в типизированный источник — ни в основной `From(Cte<T>)`, ни в
присоединённый; собственные фильтры определяющего запроса остаются внутри тела CTE.

## Типизированный рекурсивный CTE

Типизированный рекурсивный CTE начинается с запроса **якоря**. Вызовите `AsRecursiveCte`, чтобы
определить **шаг**: метод принимает имя CTE и колбэк, который строит шаг по типизированной самоссылке.
Вторая перегрузка также принимает обязательный предел глубины рекурсии:

```csharp
public Cte<TResult> AsRecursiveCte(string name,
    Func<CteReference<TResult>, QueryCommand<TResult>> step);

public Cte<TResult> AsRecursiveCte(string name,
    Func<CteReference<TResult>, QueryCommand<TResult>> step, int maxRecursion);
```

Колбэк вызывается **ровно один раз** при вызове `AsRecursiveCte`. Подготовка, генерация SQL и
выполнение не вызывают его повторно. Внутри колбэка
[`dataContext.From(reference)`](xref:NextORM.Core.CteReference`1) возвращает обычный
[`EntityBuilder<TResult>`](xref:NextORM.Core.EntityBuilder`1) для чтения CTE. Доступ к членам
определяется формой проекции якоря.

nextorm **автоматически** объединяет запросы якоря и шага как `anchor UNION ALL step`. Вызывать
`UnionAll` вручную или объявлять CTE второй раз не нужно.

Самоссылка принадлежит колбэку, который её получил. Объект
[`CteReference<TResult>`](xref:NextORM.Core.CteReference`1) действителен, только пока выполняется этот
колбэк. Использование ссылки вне колбэка — в том числе захваченной или чужой ссылки с тем же именем —
или чтение самоссылки из якоря вызывает `InvalidOperationException` **до построения команды БД**.

Рекурсивные CTE не могут ссылаться друг на друга. Каждый из них может ссылаться только на собственную
самоссылку.

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
with recursive nums as (select id as 'Id'
 from simple_entity
 where id = 1
 union all
 select (Id + 1) as 'c0'
 from nums as 't1'
 where (t1.Id < 5)) select id
 from nums as 't1'
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
with recursive nums as (select id as 'Id', (id + 1) as 'Next'
 from simple_entity
 where id = 1
 union all
 select (id + 1) as 'Id', (Next + 1) as 'Next'
 from nums as 't1'
 where (t1.Id < 4)) select Id, Next
 from nums as 't1'
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
with recursive nums as (select id as 'n'
 from simple_entity
 where id = 1
 union all
 select (n + 1) as 'n'
 from nums as 't1'
 where (t1.n < 5)) select n
 from nums as 't1'
```

### Форму задаёт якорь

Проекция **якоря** определяет читаемые столбцы рекурсивного CTE, их порядок и алиасы. Шаг должен
повторить ту же форму слот за слотом; его алиасы не переименовывают столбцы CTE.

Перед отправкой SQL в базу каждая подготовка проверяет каждый элемент шага на соответствие якорю.
Если различается хотя бы одно из перечисленного, подготовка вызывает `InvalidOperationException`:

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

nextorm выполняет проверку при каждой подготовке, даже при попадании в кэш планов. Поэтому
кэшированный план не обходит требования к форме якоря и шага.

### Глубина рекурсии (`maxRecursion`)

Двухаргументная перегрузка **не** выводит подсказку глубины. В этом случае SQL Server использует
значение по умолчанию (`100`). Трёхаргументная перегрузка требует `int` и выводит
`option (maxrecursion n)` только для SQL Server. Значение `0` означает «без ограничения», а
положительное значение задаёт максимальную глубину рекурсии. Остальные провайдеры игнорируют
подсказку и используют собственное значение по умолчанию, как и в строковом API.

Если рекурсивный CTE переносится в другую инструкцию — `INSERT … SELECT`, `MERGE`, `CTAS` /
`SELECT … INTO` или шаг batch, — явно заданный предел переносится вместе с ним. SQL Server добавляет
`option (maxrecursion n)` в конец всей инструкции, перед завершающим маркером `MERGE`. Поэтому явно
заданный `maxRecursion` сохраняется, а не заменяется значением сервера по умолчанию. Провайдеры без
такой опции не затронуты и выдают побайтно тот же SQL.

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
with nums as (select id as [Id]
 from simple_entity
 where id = 1
 union all
 select (Id + 1) as [c0]
 from nums as [t1]
 where (t1.Id < 5)) select id
 from nums as [t1] option (maxrecursion 100)
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

Для строкового `WithRecursive` эта проверка поддержки не применяется: метод работает на всех
провайдерах, кроме одного ограничения SQL Server, описанного ниже.

### SQL Server: рекурсивный CTE требует `UNION ALL`

Для SQL Server рекурсивный CTE с `UNION` без сохранения дубликатов (`anchor.Union(step)`) недопустим:
сервер отвечает *«does not contain a top-level UNION ALL operator»*. nextorm обнаруживает это при
подготовке запроса и вызывает `NotSupportedException` **до** выполнения и помещения запроса в кэш
планов. В сообщении названы SQL Server и обе поддерживаемые формы:

```csharp
// Отвергается на SQL Server: distinct UNION в рекурсивном теле.
var body = anchor.Union(step);

// Поддерживаемые варианты:
var body = anchor.UnionAll(step);                        // строковый API, явный UNION ALL
var nums = anchor.AsRecursiveCte("nums", self => ...);   // типизированный API, всегда UNION ALL
```

PostgreSQL, SQLite, MySQL и MariaDB принимают `UNION` без сохранения дубликатов в рекурсивном CTE.

Каждый пример выше ограничен завершающим предикатом в шаге; никогда не стройте неограниченный
рекурсивный CTE.

## Цепочка объявлений

Каждый вызов [`With`](xref:NextORM.Core.DataContextExtensions.With(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand))
добавляет CTE в предыдущую область. Поэтому более поздний CTE может использовать более ранний.
Объявления выводятся в порядке их добавления:

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
with first as (select id
 from complex_entity where (id > 1)), second as (select id
 from first) select id
 from second
```

> **Вложенные CTE переносятся автоматически.** Если тело CTE уже содержит `WITH` — например, это
> запрос, построенный через `dataContext.With(...).From(...)` и переданный в другой `With`, — nextorm
> объединяет их в один `with` верхнего уровня.
>
> Каждая зависимость выводится раньше использующего её CTE, даже если она объявлена позже как соседняя.
> Относительный порядок объявлений, не зависящих друг от друга, сохраняется. Один и тот же экземпляр
> `CteDefinition` выводится только один раз. Два разных экземпляра с одинаковым именем вызывают
> `InvalidOperationException`: одно имя в `WITH` нельзя связать с двумя определениями.
>
> Так все провайдеры получают плоскую переносимую форму. SQL Server всегда выводит обычный список
> `with name as (...)`, но не `with recursive`: в T-SQL рекурсивный CTE объявляется с одним `with`.
>
> Если объявление нельзя перенести в `WITH` верхнего уровня — например, оно вложено в подзапрос
> производной таблицы, коррелированную ссылку или ветвь операции над множествами, — nextorm вызывает
> `InvalidOperationException` до формирования SQL, вместо того чтобы вывести непереносимую вложенную
> форму.

`From(CteDefinition)` эквивалентен `From(definition.Name)` и удобен, когда вы сохранили область видимости,
а не имя:

```csharp
var cte = dataContext.With("recent", recent);
var rows = cte.From(cte.Ctes[0])
    .Select(t => new { id = t["id"].AsInt })
    .ToList();
```

## Композиция поверх CTE

Источник CTE использует обычный универсальный построитель запросов, поэтому к нему применимы операции
для таблиц: фильтрация, агрегация, сортировка, постраничный вывод и соединение. Более поздний CTE
может агрегировать более ранний по имени:

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
with first as (select id, somestring as 'name'
 from complex_entity),
     second as (select name, count(*) as 'count'
 from first group by name)
select name, count
 from second order by count desc
```

CTE можно соединить с другим CTE или таблицей. В строковом API укажите имя каждого источника.
nextorm уточняет, к какой таблице относится столбец `TableAlias`, поэтому одноимённые столбцы не
становятся неоднозначными:

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
with l as (select id
 from complex_entity), r as (select id
 from simple_entity) select t1.id, t2.id
 from l as 't1'
 join r as 't2' on t1.id = t2.id
```

> У источника CTE нет сопоставленной сущности, поэтому столбцы читаются по имени (`t.GetInt64("id")` /
> `t["id"].AsInt`), и имена должны совпадать с выходными алиасами тела CTE. Называйте элементы проекции
> по SQL-алиасам (нижний `snake_case`), чтобы внешние ссылки оставались точными.

### Прямое соединение типизированного CTE

Если у вас уже есть дескриптор, передайте его напрямую в любой оператор соединения: `Join`,
`LeftJoin`, `RightJoin`, `FullJoin`, `CrossJoin`, `CrossApply` или `OuterApply`. Оператор преобразует
[`Cte<T>`](xref:NextORM.Core.Cte`1) с помощью контекста принимающего построителя, затем вызывает
обычную перегрузку для источника [`EntityBuilder<T>`](xref:NextORM.Core.EntityBuilder`1). Прямой вызов
даёт тот же SQL, параметры и материализованный результат, что и предварительное преобразование:

```csharp
var left = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id })
    .AsCte("l");
var right = dataContext.From<ISimpleEntity>()
    .Select(s => new { s.Id })
    .AsCte("r");

// прямо: дескриптор преобразуется этим контекстом
var rows = dataContext.From(left)
    .Join(right, (l, r) => l.Id == r.Id)
    .Select(p => new { Id = p.Item1.Id, Other = p.Item2.Id })
    .ToList();

// эквивалентная явная форма
var converted = dataContext.From(left)
    .Join(dataContext.From(right), (l, r) => l.Id == r.Id)
    .Select(p => new { Id = p.Item1.Id, Other = p.Item2.Id })
    .ToList();
```

```sql
-- SQLite: обе формы дают это
with l as (select id as 'Id'
 from complex_entity where (id > 1)), r as (select id as 'Id'
 from simple_entity) select t1.Id, t2.Id
 from l as 't1'
 join r as 't2' on t1.Id = cast(t2.Id as bigint)
```

#### Выберите перегрузку соединения

Три оператора без условия (`CrossJoin`, `CrossApply` и `OuterApply`) принимают только дескриптор.
Остальные четыре принимают предикат и могут принимать завершающий `Action<JoinOptions>`. У
перегрузок с опциями **нет** значения по умолчанию. Чтобы выбрать короткую перегрузку, опустите
аргумент опций:

```csharp
var rows = dataContext.From(left)
    .Join(right, (l, r) => l.Id == r.Id, o => o.SuppressCartesianWarning());
```

В остальном прямой дескриптор ведёт себя так же, как предварительно преобразованный. Зависимости
переносятся раньше использующего их CTE; повторное использование одного экземпляра создаёт одно
объявление; отдельный дескриптор с тем же именем по-прежнему отклоняется. Фильтры сущности остаются
в теле определения, а повторные вызовы не меняют общую команду и флаг кэширования.

Для [`JoinedEntityBuilder<T1..Tn>`](xref:NextORM.Core.JoinedEntityBuilder`2) эти перегрузки доступны
приёмникам с арностями 2–7, поэтому CTE можно присоединить на любом следующем шаге. Плоская
типизированная проекция по-прежнему ограничена восемью слотами: для восьми слотов следующая
перегрузка соединения не объявлена. Унаследованные соединения универсальной базовой реализации
остаются доступны.

#### Используйте CTE в соединённой мутации

Область CTE может служить источником для многотабличного `UPDATE` или `DELETE`. Разместите CTE на
**присоединяемой** стороне, а целью оставьте физическую таблицу. Объявление переносится перед
изменяющей инструкцией:

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
with recent as (select id
 from orders where (id > 1000)) update orders as "t1" set status = @p0 from recent as "t2" where t1.id = t2.id
```

Это работает у провайдеров с `UPDATE ... FROM`/`JOIN` или `DELETE ... USING`/join, на любой позиции
соединения и для рекурсивных CTE. См. [Data modification (UPDATE)](19-update-statement.md#обновление-из-join)
и [Data modification (DELETE)](18-delete-statement.md#удаление-по-соединению).

## Рекурсивный CTE: числовая последовательность

Рекурсивный CTE строит результат, объединяя **якорь** (нерекурсивный запрос) и **шаг**. Шаг читает
CTE по имени и повторяется, пока выполняется предикат. Объедините запросы через `UnionAll` и передайте
результат в [`WithRecursive`](xref:NextORM.Core.DataContextExtensions.WithRecursive(NextORM.Core.IDataContext,System.String,NextORM.Core.QueryCommand,System.Nullable{System.Int32})):

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
with recursive nums as (select id as 'n'
 from simple_entity where id = 1 union all select (n + 1) as 'n'
 from nums where (n < 5)) select n
 from nums
```

Вывод:

| n |
|---|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |

`maxRecursion` позволяет задать предел глубины рекурсии. В этом примере опцию уровня инструкции
поддерживает только SQL Server; его диалект добавляет `option (maxrecursion n)` в конец инструкции.
SQLite и PostgreSQL игнорируют опцию и используют собственные значения по умолчанию:

```csharp
var numbers = dataContext
    .WithRecursive("nums", body, maxRecursion: 100)
    .From("nums")
    .Select(t => new CteNumberRow { n = t["n"].AsInt })
    .ToList();
```

```sql
-- SQL Server: no `recursive` keyword, depth option appended
with nums as (select id as [n]
 from simple_entity where id = 1 union all select (n + 1) as [n]
 from nums where (n < 5)) select n
 from nums option (maxrecursion 100)
```

## CTE и захваченный параметр

Захваченное значение в теле CTE становится параметром, как и в обычном запросе. При извлечении
параметров nextorm обходит всё предложение `with`, а не только внешнюю инструкцию:

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
with recent as (select id
 from complex_entity where (id > $threshold)) select id
 from recent
```

## Переиспользование кэша планов

Определения CTE входят в ключ кэша планов. Два запроса с разными телами CTE **не** используют один
кэшированный план. Заново построенные цепочки с одинаковой структурой, наоборот, переиспользуют план,
в том числе рекурсивный CTE с телом `union all` из двух новых команд:

* кэшированный план CTE заново извлекает захваченный параметр CTE при попадании в кэш
  (`PlanCacheTests.Cte_WithCapturedParam_RepeatedExecution_ShouldRefreshParam`);
* два разных тела CTE порождают два плана
  (`PlanCacheTests.Cte_DifferentDefinitions_ShouldNotSharePlan`);
* эквивалентный рекурсивный CTE, построенный снова, переиспользует кэшированный план
  (`PlanCacheTests.RecursiveCte_FreshCommand_ShouldReuseCachedPlan`).

См. [Переиспользование запросов: кэш против Prepare](../engine/03-plan-cache-and-reuse.md) о времени жизни и правилах
инвалидации кэша планов.

## Модифицирующий CTE (PostgreSQL)

PostgreSQL — единственный поддерживаемый провайдер, который принимает изменяющую инструкцию как тело
CTE: `WITH <имя> AS (<INSERT|UPDATE|DELETE> ... RETURNING ...)`. Эта возможность связана с
[`SupportsDataModifyingCtes`](xref:NextORM.Core.ISqlDialect.SupportsDataModifyingCtes).

Передайте возвращающий строки `INSERT`, `UPDATE` или `DELETE` в перегрузку `With(имя, mutation)` у
`IDataContext` или `CteQuery`. Она возвращает
[`MutationCteQuery<TResult>`](xref:NextORM.Core.MutationCteQuery`1), типизированный по проекции
`RETURNING`. Проекция `RETURNING` обязательна: тело, которое только меняет данные и ничего не
возвращает, не поддерживается.

PostgreSQL принимает однотабличные мутации, а также многотабличные мутации с INNER-соединениями
арностей 2–8. Для многотабличного `UPDATE ... FROM` используйте
[`UpdateJoinBuilder<TProjection>.Returning()`](xref:NextORM.Core.UpdateJoinBuilder`1.Returning) или
[`Returning(projection)`](xref:NextORM.Core.UpdateJoinBuilder`1.Returning``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})),
а для многотабличного `DELETE ... USING` — через
[`CreateDeleteJoinBuilder()`](xref:NextORM.Core.DataContextExtensions.CreateDeleteJoinBuilder``2(NextORM.Core.JoinedEntityBuilder{``0,``1}))
соединённого построителя. Он возвращает
[`DeleteJoinBuilder<TProjection>`](xref:NextORM.Core.DeleteJoinBuilder`1), чей
[`Returning()`](xref:NextORM.Core.DeleteJoinBuilder`1.Returning) или
[`Returning(projection)`](xref:NextORM.Core.DeleteJoinBuilder`1.Returning``1(System.Linq.Expressions.Expression{System.Func{`0,``0}}))
возвращает строки. `Returning()` эквивалентен `Returning(p => p)` и возвращает всю проекцию
соединения; форма с явной проекцией не меняется.

Остальные провайдеры отклоняют `With(имя, mutation)` с `NotSupportedException`, поскольку тело их
CTE должно быть `SELECT`. В отличие от CTE для чтения, инструкция с изменяющим CTE не попадает в кэш
планов: у неё есть побочный эффект, поэтому nextorm заново строит план при каждом вызове.

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
with upd as (update orders set total = @p0 where customer_id = 7 returning id, total) select id, total
 from upd as "t1"
```

```csharp
// Тело multi-table DELETE: удаляем строки цели, совпавшие по соединению, возвращая колонки обеих сторон.
var doomed = dataContext
    .From<IOrder>()
    .Join<ICustomer>((o, c) => o.CustomerId == c.Id)
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
with del as (delete from orders as "t1" using customers as "t2" where t1.customer_id = t2.id returning t1.id as "OrderId", t2.name as "CustomerName") select "OrderId", "CustomerName"
 from del as "t1"
```

```csharp
// Тело по всей проекции: .Returning() возвращает каждый слот, а читающая сторона адресует их по ItemN.
var doomed = dataContext
    .From<IOrder>()
    .Join<ICustomer>((o, c) => o.CustomerId == c.Id)
    .CreateDeleteJoinBuilder()
    .Returning();   // эквивалент .Returning(p => p)

var removed = dataContext
    .With("del", doomed)
    .From("del")
    .Select(r => new { OrderId = r.Item1.Id, CustomerName = r.Item2.Name })
    .ToList();
```

Тело по всей проекции выводит **каждый** возвращаемый столбец обоих слотов под детерминированными
псевдонимами `__sN_*` (`__s1_*` — цель, `__s2_*` — присоединённая сторона). Поэтому при чтении
обращайтесь к сохранённому псевдониму, а не к имени члена CLR:

```sql
-- PostgreSQL: читающая сторона тела по всей проекции выше
select "__s1_id", "__s2_name"
 from del as "t1"
```

Write-CTE документируется вместе с поверхностью записи, к которой принадлежит — типизированное чтение через
`From`/`FromTable`, тело `VALUES` или `INSERT ... SELECT`, чтение более раннего read-CTE и питание
главного `INSERT ... SELECT` — в разделе
[Изменение данных (INSERT): Модифицирующий CTE](17-insert-statement.md#модифицирующий-cte-postgresql).
Общая поверхность `UPDATE` — в [Изменении данных (UPDATE)](19-update-statement.md), `DELETE` — в
[Изменении данных (DELETE)](18-delete-statement.md).

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

* [Операции над множествами](08-set-operations.md) — [`UnionAll`](xref:NextORM.Core.QueryCommand`1.UnionAll``1(NextORM.Core.QueryCommand{``0})) и другие, используются для построения рекурсивного тела.
* [Соединения](04-joins.md) — соединение CTE с таблицей, как в `CommonTestSuite.Cte.cs`.
* [Необработанный SQL](14-raw-sql.md) — когда вся инструкция написана вручную.
* [Переиспользование запросов: кэш против Prepare](../engine/03-plan-cache-and-reuse.md) — как кэшируются планы CTE.

---

Source: `src/nextorm.core/Builders/Cte/CteQuery.cs:7`, `src/nextorm.core/DataContext/DataContextExtensions.cs:9`;
`tests/nextorm.integration.tests/CommonTestSuite/Cte/CommonTestSuite.Cte.cs:14`, `tests/nextorm.integration.tests/CommonTestSuite/Cte/CommonTestSuite.Cte.cs:33`;
`tests/nextorm.core.tests/Cte/CteQueryTests.cs:8`;
типизированный рекурсивный CTE: `src/nextorm.core/Cte.cs:15`, `src/nextorm.core/Query/Commands/QueryCommand.TResult.cs:1100`,
`src/nextorm.core/DataContext/Sql/SqlSourceRenderer.cs:35`, `src/nextorm.core/Query/Commands/QueryCommand.QueryPreparer.cs:981`;
`tests/nextorm.core.tests/Cte/TypedCteTests.cs:1057`, `tests/nextorm.sqlite.tests/Cte/TypedCteTests.cs:718`;
`tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:190`, `tests/nextorm.sqlite.tests/PlanCache/PlanCacheTests.cs:343`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:1188`, `:1202`, `:1216`, `:1231`, `:1248`;
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:827`, `:856`;
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:759`, `:788`.
