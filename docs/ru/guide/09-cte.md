# Обобщённые табличные выражения (CTE)

> Объявляйте один или несколько именованных `with`-запросов и используйте их в качестве источника `from`
> для запроса, включая рекурсивные CTE для последовательностей и иерархий.

**Предварительные требования:** [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Запросы и проекции](01-querying-and-projections.md) · [Операции над множествами](07-set-operations.md)

## Обзор

CTE объявляется с помощью `With(name, query)` (нерекурсивный) или `WithRecursive(name, query, maxRecursion)`
(рекурсивный) в `IDataContext`. Оба являются методами расширения (`IDataContextExtensions`), и оба возвращают
область видимости `CteQuery`, которая хранит объявления, собранные на данный момент, в `CteQuery.Ctes`:

```csharp
public static CteQuery With(this IDataContext dataContext, string name, QueryCommand query);

public static CteQuery WithRecursive(this IDataContext dataContext, string name, QueryCommand query,
    int? maxRecursion = null);
```

Объявления неизменяемы: каждый вызов `With`/`WithRecursive` возвращает **новую** область видимости, которая
добавляет `CteDefinition` к предыдущим. Определение фиксирует имя, `QueryCommand`, который его создаёт, и
может ли тело ссылаться на собственное имя.

`CteQuery.From(string cteName)` (или `From(CteDefinition)`) начинает новый запрос, чей `from` — один из
объявленных CTE, перенося каждое объявление в результирующую команду. Далее используется режим `TableAlias`
без сущности для чтения столбцов CTE (`t["id"].AsInt`), и применяются обычные операторы `Where`/`Join`/
`Select`. Рекурсивные тела ссылаются на собственное имя тем же способом
(`dataContext.From("nums")` внутри шагового запроса).

Рендеринг: диалекты, использующие форму ANSI, выводят `with recursive`, когда любое определение рекурсивно
(SQLite, PostgreSQL); SQL Server объявляет рекурсивный CTE только с `with` и добавляет параметр глубины
после инструкции.

## Нерекурсивный CTE

```csharp
var recent = dataContext.Create<IComplexEntity>()
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

## Цепочка объявлений

Каждый `With` добавляется к предыдущей области видимости, поэтому более поздний CTE может быть определён
через более ранний. Объявления рендерятся в порядке объявления:

```csharp
var first = dataContext.Create<IComplexEntity>()
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

## Рекурсивный CTE: числовая последовательность

Рекурсивный CTE — это `union all` **якоря** (нерекурсивного запроса) и **шага**, который читает CTE по
имени и останавливается, когда предикат перестаёт совпадать. Вызовите `WithRecursive`, передав объединение
в качестве тела:

```csharp
public sealed class CteNumberRow
{
    public int n { get; set; }
}

var anchor = dataContext.Create<ISimpleEntity>()
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
    .With("recent", dataContext.Create<IComplexEntity>()
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

## Различия между провайдерами

| Провайдер | Поведение |
|---|---|
| SQLite | `with` для нерекурсивных, `with recursive` для рекурсивных; опции глубины нет. |
| SQL Server | Рекурсивные CTE объявляются только с `with` (без ключевого слова `recursive`); `maxRecursion` рендерится как `option (maxrecursion n)` в конце инструкции. |
| PostgreSQL | `with` / `with recursive`; опции глубины нет. |
| In-memory | Не применимо: CTE рендерятся SQL-диалектами и не являются частью провайдера in-memory. |

## См. также

* [Операции над множествами](07-set-operations.md) — `UnionAll` и другие, используются для построения рекурсивного тела.
* [Соединения](03-joins.md) — соединение CTE с таблицей, как в `CommonTestSuite.Cte.cs`.
* [Необработанный SQL](14-raw-sql.md) — когда вся инструкция написана вручную.
* [Переиспользование запросов: кэш против Prepare](15-query-reuse.md) — как кэшируются планы CTE.

---

Source: `src/nextorm.core/Builders/CteQuery.cs:7`, `src/nextorm.core/DataContext/IDataContextExtensions.cs:9`;
`test/nextorm.integration.tests/CommonTestSuite.Cte.cs:14`, `test/nextorm.integration.tests/CommonTestSuite.Cte.cs:33`;
`test/nextorm.core.tests/CteQueryTests.cs:8`;
`test/nextorm.sqlite.tests/PlanCacheTests.cs:190`, `test/nextorm.sqlite.tests/PlanCacheTests.cs:343`;
generated SQL: `test/nextorm.sqlite.tests/SqlGenerationTests.cs:1188`, `:1202`, `:1216`, `:1231`, `:1248`;
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:827`, `:856`;
`test/nextorm.postgres.tests/SqlGenerationTests.cs:759`, `:788`.
