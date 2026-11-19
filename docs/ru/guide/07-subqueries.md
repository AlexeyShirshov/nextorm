# Подзапросы

> Встраивайте запрос в другой запрос: читайте его строки через `FROM`, используйте одно значение как скалярный подзапрос или проверяйте результат через `EXISTS` и `IN`.

**Предварительные требования:** [Запросы и проекции](../engine/index.md) · [Фильтрация (WHERE)](03-filtering-where.md) · [Соединения](04-joins.md)

## Обзор

Выберите форму подзапроса по тому, что внешнему запросу нужно получить от внутреннего:

* Используйте **производную таблицу**, если внешнему запросу нужны строки внутреннего запроса как
  источник. Передайте команду в [`From`](xref:NextORM.Core.DataContext.From(System.String)).
* Используйте **скалярный подзапрос**, если внешнему запросу нужно одно значение в проекции, `WHERE`,
  `ORDER BY` или `HAVING`. Вызовите внутри внешнего выражения метод для одной строки, например
  [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) или
  [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})).
* Используйте **предикат**, если нужно проверить результат внутреннего запроса. Выберите
  `SqlFunctions.Sql.exists(...)`, `SqlFunctions.Sql.@in(column, query)`,
  `SqlFunctions.Sql.any(query)` или `SqlFunctions.Sql.all(query)`.

nextorm независимо подготавливает внутренний запрос и помещает его в круглые скобки. Коррелированный
подзапрос также использует значение из внешнего запроса; nextorm указывает его псевдоним внешней
таблицы.

Об одном ограничении провайдера стоит знать заранее: `SqlFunctions.Sql.any` и `SqlFunctions.Sql.all` являются
допустимым SQL только в SQL Server и PostgreSQL. В SQLite нет ни одного из этих операторов, поэтому
запрос доходит до базы данных и завершается с `SqliteException` во время выполнения.

**Маршрутизированные запросы.** Подзапрос едет внутри своего statement и отдельного перехода не
добавляет: все маршрутизированные источники statement должны сходиться в одну топологию, а шардированный
источник несёт вариант своего бакета. См.
[«Маршрутизация данных по топологиям»](34-routing-data-to-topologies.md) и
[«Шардирование и горизонтальное партиционирование»](35-sharding-and-horizontal-partitioning.md).

## Подзапрос как источник FROM

[`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) принимает подготовленный `QueryCommand<T>` (или `EntityBuilder<T>`) и создаёт построитель по его
столбцам:

```csharp
var nested = dataContext.From<IComplexEntity>().Select(x => new { x.Id });
var rows = dataContext.From(nested).Select(t => new { t.Id }).ToList();
```

```sql
-- SQLite (derived tables do not require an alias)
select id
 from (select id
 from complex_entity)
```

```sql
-- SQL Server / PostgreSQL (the derived table is aliased)
select id
 from (select id
 from complex_entity) as [t1]
```

Производную таблицу можно так же проецировать, фильтровать и соединять, как любой источник.
Столбец, переименованный во внутренней проекции, снаружи упоминается по своему спроецированному
имени:

```csharp
var nested = dataContext.From<IComplexEntity>().Select(x => new { x.Id, Calc = x.String + x.String });
var rows = dataContext.From(nested).Select(t => new { t.Id, t.Calc }).ToList();
```

`From(nested)` создаёт производный источник; чтение его строк — отдельный шаг. В обычном случае
добавьте внешний `Select` и укажите нужные выходные столбцы. Внешний запрос может обращаться только
к столбцам, объявленным внутренним `SELECT`.

Вызов `From(nested).ToList()` без внешнего `Select` работает лишь тогда, когда внутренняя проекция уже
содержит разрешённые ссылки на столбцы: столбец сущности, переименованный выше `Calc` или вычисляемый
скаляр, доступный по выходному имени. Проекцию из выражений над членами строки — например, анонимный
тип `new { x.Id }` или DTO `new Dto { Id = x.Id }` — пришлось бы повторно переводить относительно
производного источника. Поэтому подготовка отклоняет её с `QueryPreparationException`.

Ограничение связано с **формой выражения**, а не с анонимными типами. DTO-проекции поддерживаются при
прямом чтении (см. [Проекции](02-projections.md)), но это не означает, что тот же DTO можно прочитать
из производного источника без внешнего `Select`. Добавьте `Select` (`From(nested).Select(t => t.Id)`).
Такой вариант работает с DTO через инициализатор членов и с позиционными записями. Для проверенной
проекции класса с одним конструктором известна ранее существовавшая ошибка
(`Cannot find inner column Id`), поэтому используйте одну из поддерживаемых форм.

**Скалярный производный источник с одним столбцом** — например, проекция члена или запрос с операцией
над множествами `left.UnionAll(right)` — не требует формы строки и сразу материализуется в `List<T>`.
Это работает и при пустом общем кэше списка выборки для типа, и после его заполнения предыдущим чтением
скаляра. Производное чтение создаёт собственную форму из одного столбца и не использует и не меняет
запись в кэше. До исправления при заполненном кэше повторно использовался список для материализации
объектов, и возникала ошибка `QueryPreparationException: Cannot get ctor from T`; при пустом кэше
чтение работало.

В примерах используются два вида псевдонимов. **Псевдоним таблицы** (`t1`, `t2`, ...) задаёт имя
производного источника в `FROM` и используется в ссылках на его столбцы (`t1.id`). Его создаёт
провайдер — задавать его не нужно. **Псевдоним выходного столбца** объявляет производный `SELECT`:
это имя спроецированного члена (`Calc`, `requiredstring as 'String'`) или, для вычисляемого скаляра
без имени, сгенерированное имя, на которое ссылается внешний запрос.

Провайдер сам выбирает и способ заключения сгенерированного имени в кавычки (`'c0'`, `"c0"`, `[c0]`,
`` `c0` ``). Это внутренняя деталь сформированного запроса, а не публичная гарантия именования.
Получайте значение через построитель, а не по жёстко заданному имени:

```csharp
var inner = dataContext.From<IComplexEntity>().Where(x => x.Id <= 2).Select(x => x.Id * 2);
var rows = dataContext.From(inner).OrderBy(x => x).Select(x => x * 3).ToList(); // 6, 12
```

## Именование производной таблицы через `As`

`As<TResult>(...)` оборачивает проекцию в производную таблицу в `FROM`: он проецирует каждую строку
в именованный тип и возвращает построитель по результату, поэтому спроецированные члены можно
соединять, фильтровать и проецировать дальше. Поскольку производная таблица сбрасывает счётчик
арности соединения, именно так продолжают композицию после соединения, в том числе за
восьмитабличным потолком:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Join<IComplexEntity>((s, c) => s.Id == c.Id)
    .As(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.String })
    .Where(d => d.OrderId > 100)
    .Select(d => new { d.OrderId, d.CustomerName })
    .ToList();
```

После `As` видны только спроецированные члены, а in-memory-провайдер отклоняет `Join` по
производной таблице с `NotSupportedException`. Полный оператор описан в разделе
[Соединения](04-joins.md#именование-промежуточной-проекции-as).

## Скалярный подзапрос в SELECT

Чтобы встроить внутренний запрос в проекцию как скалярный столбец, вызовите метод для одной строки:
[`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})),
[`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0})),
[`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) или
[`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})).
Имя внешнего свойства становится псевдонимом скалярного столбца:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, sid = dataContext.From<ISimpleEntity>().Where(it => it.Id == 1).Select(it => it.Id).First() })
    .ToListAsync();
```

```sql
select id, (select id
 from simple_entity where (id = 1) limit 1) as 'sid'
 from complex_entity
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/TestProviders/SqliteTestProvider.cs`).

Вывод:

| Id | sid |
|----|-----|
| 1 | 1 |
| 2 | 1 |
| 3 | 1 |

Внутренний запрос также можно отсортировать:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, sid = dataContext.From<ISimpleEntity>().OrderByDescending(it => it.Id).Select(it => it.Id).First() })
    .ToListAsync();
```

Вывод:

| Id | sid |
|----|-----|
| 1 | 10 |
| 2 | 10 |
| 3 | 10 |

## Скалярный подзапрос в WHERE

Тот же терминал для одной строки, использованный в предикате, становится скалярным подзапросом в
правой части:

```csharp
var row = await dataContext.From<IComplexEntity>()
    .Where(it => it.Id == dataContext.From<ISimpleEntity>().OrderBy(it => it.Id).Select(it => it.Id).First())
    .Select(it => new { it.Id })
    .FirstOrDefaultAsync();
```

Вывод:

| Id |
|----|
| 1 |

## Скалярный подзапрос в ORDER BY

Ключ `ORDER BY` может быть выражением, содержащим скалярный подзапрос. Первый [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)) использует
подзапрос, второй разрешает равенство:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .OrderBy(_ => dataContext.From<ISimpleEntity>().Where(it => it.Id == 1).Select(it => it.Id).First())
    .OrderBy(it => it.Id)
    .Select(it => new { it.Id })
    .ToList();
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |

## Коррелированный скалярный подзапрос

Скалярный подзапрос может ссылаться на член внешнего запроса; nextorm квалифицирует его псевдонимом
внешней таблицы. Это работает у каждого SQL-провайдера и в любой позиции значения (проекция,
`WHERE`, `ORDER BY`, `HAVING`):

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, sid = dataContext.From<ISimpleEntity>().Where(s => s.Id == it.Id).Select(s => s.Id).First() })
    .ToListAsync();
```

```sql
select t1.id, (select t2.id
 from simple_entity as 't2' where t2.id = t1.id limit 1) as 'sid'
 from complex_entity as 't1'
```

Внешний запрос квалифицирует только упомянутый член; внутренний запрос сохраняет свои столбцы и
псевдонимы. [`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0}))/[`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})) дают `NULL`, когда во внутреннем запросе нет строк.

Упомянутый член может также приходить из join-проекции: `p.Item1.Id` определяет псевдоним таблицы
по позиции элемента проекции, а столбец — по его разметке, поэтому подзапрос может коррелировать с
любым из соединённых источников:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Join<IComplexEntity>((s, c) => s.Id == c.Id)
    .Select(p => new { p.Item1.Id, cid = dataContext.From<IComplexEntity>().Where(c => c.Id == p.Item1.Id).Select(c => c.Id).First() })
    .ToListAsync();
```

```sql
select t1.id, (select top(1) t3.id
 from complex_entity as [t3]
 where t3.id = cast(t1.id as bigint)) as [cid]
 from simple_entity as [t1]
 join complex_entity as [t2] on cast(t1.id as bigint) = t2.id
```

Подзапрос может также стоять в `HAVING`, где он может ссылаться на ключ группировки. Он
подготавливается тем же коррелированным visitor'ом, что и `WHERE`:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Where(e => e.Int != null)
    .GroupBy(e => new { e.Int })
    .Having(g => SqlFunctions.Sql.exists(dataContext.From<IComplexEntity>().Where(c => c.Int == g.Int)))
    .Select(g => new { g.Int, count = SqlFunctions.Sql.count() })
    .ToListAsync();
```

Внешняя ссылка может быть обёрнута в скалярную функцию над внешним столбцом (например
`e.String.ToUpper()`); функция отображается вокруг квалифицированного внешнего псевдонима так же,
как и любое другое выражение.

### Проецируйте коррелированный агрегат

В коррелированных **проекциях** поддерживаемые агрегатные методы построителя запросов
([`Count`](xref:NextORM.Core.EntityBuilderExtensions.Count``1(NextORM.Core.EntityBuilder{``0})), `LongCount`,
`Sum`, `Min`, `Max`, `Avg`, `Stdev`, `Stdevp`, `Var` и `Varp`) транслируются в агрегатные скалярные
подзапросы. Поэтому их можно использовать вместе со ссылкой на внешнюю строку. `LongCount()` возвращает
количество типа `long` для каждой внешней строки, включая `0`, если внутренний запрос не нашёл строк.
Поддержка в проекциях сама по себе не означает, что агрегатные методы можно использовать в предикатах
`WHERE`:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Select(it => new { it.Id, cnt = dataContext.From<IComplexEntity>().Where(c => c.Id == it.Id).Count() })
    .ToListAsync();
```

```sql
select t1.id, (select count(*)
 from complex_entity as 't2'
 where cast(t2.id as bigint) = t1.id
limit 1) as 'cnt'
 from complex_entity as 't1'
```

Упорядоченный результат наглядно показывает тип счётчика: `LongCount()` возвращает ту же
последовательность, что и `Count()`, но с типом `long`. Для внешней строки без совпадений результат —
`0L`.

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .OrderBy(it => it.Id)
    .Select(it => new
    {
        it.Id,
        cnt = dataContext.From<IComplexEntity>().Where(c => c.Id == it.Id).Count(),
        longCnt = dataContext.From<IComplexEntity>().Where(c => c.Id == it.Id).LongCount()
    })
    .ToListAsync();
// cnt:     [1, 2, 3]
// longCnt: [1L, 2L, 3L]
```


### Фильтруйте по коррелированному агрегату

Поддерживаемый агрегатный метод можно поставить слева от сравнения в фильтре. Тогда он становится
коррелированным скалярным подзапросом, который вычисляется для каждой внешней строки:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .Where(it => dataContext.From<IComplexEntity>().Where(c => c.Id == it.Id).Sum(c => c.Int) > 6)
    .Select(it => it.Id)
    .ToListAsync();
```

```sql
select t1.id
 from complex_entity as 't1'
 where ((select sum(t2.nullableint)
 from complex_entity as 't2'
 where t2.id = t1.id
limit 1) > 6)
```

`Count`, `Sum`, `Min`, `Max` и `Avg` работают так с упорядоченными сравнениями, `==`/`!=`,
арифметическими и логическими операторами. Сравнение остаётся за пределами внутреннего `select`.

Здесь действует семантика SQL для `NULL`: сумма по пустому набору или по значениям `NULL` равна
`NULL`. Строка с таким агрегатом не удовлетворяет сравнению — значение не считается нулём. В
приведённых данных у родителя `8001` суммы дочерних строк равны 5 и 7, у `8002` есть только `NULL`,
а у `8003` нет дочерних строк. Предикат возвращает только `[8001]`.

Здесь работает и `LongCount`: он возвращает 64-битное значение (`count_big(*)`, если провайдер его
поддерживает, иначе `count(*)`). Метод построителя вне списка поддерживаемых агрегатов (например,
скалярный `First`/`Single`) в таком сравнении участвовать не может: подготовка завершается явной
ошибкой `QueryPreparationException` с именем терминала и оператора, а не внутренней ошибкой дерева
выражений.

Корреляция вкладывается произвольно: коррелированный подзапрос может сам содержать коррелированный
подзапрос, и каждая внешняя ссылка разрешается в псевдоним того scope, который её объявил.

На in-memory провайдере коррелированный подзапрос тоже вычисляется один раз на внешнюю строку, поэтому
скалярные подзапросы, агрегатные терминалы, `EXISTS` и `IN` работают там и для корреляции глубины
один. По-прежнему отклоняются с `NotSupportedException`: глубина корреляции больше одной, ссылка на
внешнюю строку во внутренней проекции или `ORDER BY`, коррелированный `GROUP BY`/`HAVING` и
асинхронный источник. Для этих форм используйте SQL-провайдер.

На SQLite числовой `Single`/`SingleOrDefault` при более чем одной строке поднимает ошибку БД через
рендер-гард по количеству строк (провайдер не проверяет кардинальность скалярного подзапроса);
нечисловая проекция отклоняется с `NotSupportedException`.

## Коррелированный EXISTS

`SqlFunctions.Sql.exists(query)` возвращает логическое значение и транслируется в предикат `exists(...)`.
Подзапрос, ссылающийся на внешний параметр, является коррелированным; nextorm генерирует внешний
псевдоним внутри внутреннего предиката:

```csharp
var rows = dataContext.From<ISimpleEntity>()
    .Where(s => SqlFunctions.Sql.exists(dataContext.From<IComplexEntity>().Where(c => c.Id == s.Id)))
    .Select(it => it.Id)
    .ToList();
```

```sql
select t1.id
 from simple_entity as 't1' where exists(select *
 from complex_entity where (id = cast(t1.id as bigint)))
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |

`EntityBuilder<T>` также можно передать напрямую в `exists`, когда важно лишь её существование:

```csharp
var all = await dataContext.From<IComplexEntity>().Select(it => new { it.Id, exists = SqlFunctions.Sql.exists(dataContext.From<ISimpleEntity>()) }).ToListAsync();
var none = await dataContext.From<IComplexEntity>().Select(it => new { it.Id, exists = SqlFunctions.Sql.exists(dataContext.From<ISimpleEntity>().Where(it => it.Id == 100)) }).ToListAsync();
```

Коррелированный `exists` (как и `@in`, `any`, `all`) — это булев предикат, поэтому он свободно
комбинируется логическими операторами `&&`, `||` и `!` и рендерится в соответствующий SQL
`exists`/`and`/`or`/`not`:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Where(s => SqlFunctions.Sql.exists(dataContext.From<IComplexEntity>().Where(c => c.Id == s.Id))
                || s.Id == 100)
    .Select(s => s.Id)
    .ToListAsync();
```

```sql
-- SQLite
select t1.id
 from simple_entity as 't1'
where (exists(select *
 from complex_entity as 't2' where (t2.id = cast(t1.id as bigint))) or (t1.id = 100))
```

Отрицание предиката-подзапроса (`!SqlFunctions.Sql.exists(...)`) рендерится как `not (exists(...))`.
In-memory провайдер вычисляет такие комбинации по одной внешней строке для корреляции глубины один.

## IN с подзапросом

`SqlFunctions.Sql.@in(column, query)` генерирует `IN (SELECT ...)`:

```csharp
var row = await dataContext.From<IComplexEntity>()
    .Where(it => SqlFunctions.Sql.@in((int)it.Id, dataContext.From<ISimpleEntity>().Where(it => it.Id == 2).Select(it => it.Id)))
    .Select(it => it.Id)
    .FirstOrDefaultAsync();
```

```sql
select id
 from complex_entity where (cast(id as integer) in (select id
 from simple_entity where (id = 2)))
```

Вывод:

| Id |
|----|
| 2 |

Тот же метод также принимает `IEnumerable<T>` или `params T[]` литеральных значений (см.
[Фильтрация (WHERE)](03-filtering-where.md)); перегрузка с `QueryCommand<T>` — это форма подзапроса.

## ANY и ALL

`SqlFunctions.Sql.any(query)` и `SqlFunctions.Sql.all(query)` создают скаляр, который сравнивается с внешним
столбцом:

```csharp
var rows = await dataContext.From<ISimpleEntity>()
    .Where(it => it.Id == SqlFunctions.Sql.any(dataContext.From<IComplexEntity>().Select(it => it.Id)))
    .Select(it => it.Id)
    .ToListAsync();
```

```sql
-- SQL Server / PostgreSQL
select id
 from simple_entity where (cast(id as bigint) = any(select id
 from complex_entity))
```

SQLite не реализует `ANY` или `ALL`; тот же запрос принимается транслятором, но при выполнении
выбрасывает `Microsoft.Data.Sqlite.SqliteException` (покрыто в
`tests/nextorm.integration.tests/Sqlite/SqliteSpecificTests.cs:16` и `:30`).

## Различия между провайдерами

| Провайдер | Псевдоним производной таблицы | Скалярный подзапрос | `any` / `all` |
|---|---|---|---|
| SQLite | необязателен | поддерживается (в т. ч. коррелированный) | **не поддерживается** - `SqliteException` при выполнении |
| SQL Server | обязателен (`as [t1]`) | поддерживается (в т. ч. коррелированный) | поддерживается |
| PostgreSQL | обязателен (`as "t1"`) | поддерживается (в т. ч. коррелированный) | поддерживается |
| MySQL | обязателен (`` as `t1` ``) | поддерживается (в т. ч. коррелированный) | поддерживается |
| MariaDB | обязателен (`` as `t1` ``) | поддерживается (в т. ч. коррелированный) | поддерживается |
| ClickHouse | обязателен (`` as `t1` ``) | поддерживается (в т. ч. коррелированный) | поддерживается |
| In-memory | неприменимо | поддерживается (коррелированный скаляр/`EXISTS`/`IN` глубины один); более глубокие формы выбрасывают `NotSupportedException` | не покрыто |

Подзапрос, ссылающийся на внешний запрос, вынуждает присваивать псевдоним (`t1`) внешнему `FROM` у
каждого SQL-провайдера. В SQL Server булев предикат подзапроса, проецируемый как скаляр,
оборачивается в `cast(case when ... then 1 else 0 end as bit)`, потому что в T-SQL нет логического
скалярного типа.

## Ограничения

* Вложенная корреляция (подзапрос, ссылающийся на внешнюю ссылку другого подзапроса) отклоняется с
  `NotSupportedException`; явная ошибка предотвращает привязку внешнего маркера к чужому запросу.
* In-memory провайдер поддерживает коррелированный скаляр/`EXISTS`/`IN` глубины один и отклоняет более глубокие формы, перечисленные выше.

## См. также

- [Соединения](04-joins.md) - соединение `QueryCommand<T>` как производной таблицы.
- [Операции над множествами](08-set-operations.md)
- [Фильтрация (WHERE)](03-filtering-where.md) - `IN` по списку литералов.

---

Source: `tests/nextorm.integration.tests/CommonTestSuite/Queries/Structure/CommonTestSuite.CorrelatedQuery.cs`,
`tests/nextorm.sqlite.tests/SqlGeneration/Plan/CorrelatedQueryTests.cs`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:612,623,633,643,653,663`,
`tests/nextorm.integration.tests/Sqlite/SqliteSpecificTests.cs:16,30`,
`tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:255`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:293`,
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:248`.
