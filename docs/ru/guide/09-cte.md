# Обобщённые табличные выражения (CTE)

> Объявляйте один или несколько именованных `with`-запросов и используйте их в качестве источника `from`
> для запроса, включая рекурсивные CTE для последовательностей и иерархий.

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Запросы и проекции](01-querying-and-projections.md) · [Операции над множествами](07-set-operations.md)

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
**полный набор операторов** — [`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}}))/[`Join`](xref:NextORM.Core.EntityBuilder`1.Join``1(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``0,System.Boolean}}))/[`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}}))/[`Having`](xref:NextORM.Core.EntityBuilder`1.Having(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}}))/[`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32))/[`Limit`](xref:NextORM.Core.EntityBuilder`1.Limit(System.Int32))/[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})).
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
with recursive nums as (select id as 'n' from simple_entity where (id = 1) union all select (n + 1) as 'n' from nums where (n < 5)) select n from nums
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
with nums as (select id as [n] from simple_entity where (id = 1) union all select (n + 1) as [n] from nums where (n < 5)) select n from nums option (maxrecursion 100)
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

См. [Переиспользование запросов: кэш против Prepare](15-query-reuse.md) о времени жизни и правилах
инвалидации кэша планов.

## Модифицирующий CTE (PostgreSQL)

PostgreSQL — единственный поддерживаемый провайдер, принимающий модифицирующую инструкцию как тело CTE
(`WITH <имя> AS (INSERT ... RETURNING ...)`); гейтится
[`SupportsDataModifyingCtes`](xref:NextORM.Core.ISqlDialect.SupportsDataModifyingCtes). nextorm открывает
её перегрузкой `With(имя, insert)` у `IDataContext`/`CteQuery`, возвращающей
[`MutationCteQuery<TResult>`](xref:NextORM.Core.MutationCteQuery`1), типизированный проекцией `RETURNING`;
остальные провайдеры отклоняют её с `NotSupportedException`.

Write-CTE документируется вместе с поверхностью записи, к которой принадлежит — типизированное чтение через
`From`/`FromTable`, тело `VALUES` или `INSERT ... SELECT`, чтение более раннего read-CTE и питание
главного `INSERT ... SELECT` — в разделе
[Изменение данных (INSERT): Модифицирующий CTE](19-insert-statement.md#модифицирующий-cte-postgresql).
Тела `UPDATE` и `DELETE` запланированы и будут жить в своих гайдах.

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

* [Операции над множествами](07-set-operations.md) — [`UnionAll`](xref:NextORM.Core.QueryCommand`1.UnionAll``1(NextORM.Core.QueryCommand{``0})) и другие, используются для построения рекурсивного тела.
* [Соединения](03-joins.md) — соединение CTE с таблицей, как в `CommonTestSuite.Cte.cs`.
* [Необработанный SQL](14-raw-sql.md) — когда вся инструкция написана вручную.
* [Переиспользование запросов: кэш против Prepare](15-query-reuse.md) — как кэшируются планы CTE.

---

Source: `src/nextorm.core/Builders/CteQuery.cs:7`, `src/nextorm.core/DataContext/DataContextExtensions.cs:9`;
`tests/nextorm.integration.tests/CommonTestSuite.Cte.cs:14`, `tests/nextorm.integration.tests/CommonTestSuite.Cte.cs:33`;
`tests/nextorm.core.tests/CteQueryTests.cs:8`;
`tests/nextorm.sqlite.tests/PlanCacheTests.cs:190`, `tests/nextorm.sqlite.tests/PlanCacheTests.cs:343`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1188`, `:1202`, `:1216`, `:1231`, `:1248`;
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:827`, `:856`;
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:759`, `:788`.
