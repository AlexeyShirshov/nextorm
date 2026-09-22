# Провайдер In-memory

> [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) выполняет запросы по внутрипроцессным коллекциям CLR; используйте его для модульных тестов и тестов формы запросов/кэша планов, где запускать базу данных не нужно.

**Предварительные требования:** [Provider overview](overview.md) · [Dependency injection](../getting-started/04-dependency-injection.md)

## Обзор

Провайдер in-memory встроен в `nextorm` (без дополнительного пакета) и находится в
`src/nextorm.core/DataContext/InMemoryDataContext.cs`. Публичный тип — [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext), который
напрямую реализует [`IDataContext`](xref:NextORM.Core.IDataContext) — у него нет диалекта, нет соединения и нет SQL, и он вычисляет
дерево выражений запроса по данным, которые вы к нему присоединяете.

В отличие от SQL-провайдеров, [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) не требует строки подключения. Метаданные запросов и списки
выборки являются общими для процесса с SQL-провайдерами через [`DataContextCache`](xref:NextORM.Core.DataContextCache), тогда как скомпилированные
делегаты привязаны к экземпляру (они захватывают контекст, для которого были скомпилированы).

Используйте его, когда вы хотите:

- модульные тесты для кода приложения, который зависит от [`IDataContext`](xref:NextORM.Core.IDataContext);
- быстрые тесты формы запросов, проекции и поведения соединений без контейнера или файла;
- тесты неявного кэша планов ([`QueryPlanEqualityComparer`](xref:NextORM.Core.QueryPlanEqualityComparer), [`GetCacheVersion`](xref:NextORM.Core.QueryPlan.GetCacheVersion)).

Это не SQL-движок: он не проверяет, что запрос выполнился бы на реальном провайдере, и несколько
возможностей намеренно не поддерживаются (см. ниже).

## Регистрация и использование

С внедрением зависимостей:

```csharp
using NextORM.Core;

services.AddNextOrmContext<InMemoryDataContext>();
```

```csharp
using NextORM.Core;

using var ctx = new InMemoryDataContext();
ctx.From<SimpleEntity>().WithData(new[]
{
    new SimpleEntity { Id = 1 },
    new SimpleEntity { Id = 2 },
});

var ids = ctx.From<SimpleEntity>()
    .Where(it => it.Id == 1)
    .Select(it => new { it.Id })
    .SingleOrDefault();

// ids.Id == 1
```

[`WithData`](xref:NextORM.Core.InMemoryCommandBuilderExtensions.WithData``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IEnumerable{``0})) и [`WithAsyncData`](xref:NextORM.Core.InMemoryCommandBuilderExtensions.WithAsyncData``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IAsyncEnumerable{``0})) (`src/nextorm.core/Builders/InMemoryCommandBuilder.cs`) присоединяют
коллекцию к словарю `Data` контекста с ключом по типу сущности; [`WithData`](xref:NextORM.Core.InMemoryCommandBuilderExtensions.WithData``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IEnumerable{``0})) принимает `IEnumerable<T>`,
а [`WithAsyncData`](xref:NextORM.Core.InMemoryCommandBuilderExtensions.WithAsyncData``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IAsyncEnumerable{``0})) — `IAsyncEnumerable<T>`. Оба являются пустыми операциями для других провайдеров.

```csharp
ctx.From<SimpleEntity>().WithAsyncData(GetRows());

static async IAsyncEnumerable<SimpleEntity> GetRows()
{
    yield return new SimpleEntity { Id = 1 };
    await Task.Delay(0);
    yield return new SimpleEntity { Id = 2 };
}
```

## Поддерживаемое подмножество возможностей

Покрывается `InMemoryTests` и `InMemoryJoinTests`:

| Возможность | Подтверждение |
|---|---|
| Проекция: анонимная, примитив/скаляр, `Tuple` | `InMemoryTests.SelectPrimitive_ShouldReturnData`, `TestTuples` |
| [`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})), включая `==` по nullable и захваченным значениям | `InMemoryTests.TestWhere`, `Contains_ShouldFilterData` |
| Подзапрос, используемый как источник `FROM` (`ctx.From(subQuery)`) | `InMemoryTests.TestWhere_Subquery` |
| Буферизованные и асинхронные источники ([`WithData`](xref:NextORM.Core.InMemoryCommandBuilderExtensions.WithData``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IEnumerable{``0})) / [`WithAsyncData`](xref:NextORM.Core.InMemoryCommandBuilderExtensions.WithAsyncData``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IAsyncEnumerable{``0}))) | `InMemoryTests.TestAsync` |
| Потоковая передача с [`Pipeline`](xref:NextORM.Core.QueryCommand`1.Pipeline(System.Object[])), с соблюдением отмены | `InMemoryTests.TestFetch`, `TestFetch_PipelineStopsOnCancellation` |
| [`Limit`](xref:NextORM.Core.Paging.Limit) / [`Offset`](xref:NextORM.Core.Paging.Offset) / [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) / [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) и их формы `OrDefault` | `InMemoryTests.Top_ShouldLimitData`, `First_ShouldReturnFirst`, `Single_ShouldReturnSingle` |
| [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)) / [`OrderByDescending`](xref:NextORM.Core.EntityBuilder`1.OrderByDescending(System.Int32)), включая асинхронные источники | `InMemoryTests.OrderBy_ShouldSortData`, `OrderByOverAsyncSource_ShouldSortData` |
| Материализаторы [`ToArray`](xref:NextORM.Core.EntityBuilderExtensions.ToArray``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) / [`ToHashSet`](xref:NextORM.Core.EntityBuilderExtensions.ToHashSet``1(NextORM.Core.EntityBuilder{``0},System.Collections.Generic.IEqualityComparer{``0},System.ReadOnlySpan{System.Object})) / [`ToDictionary`](xref:NextORM.Core.EntityBuilderExtensions.ToDictionary``2(NextORM.Core.EntityBuilder{``0},System.Func{``0,``1},System.Collections.Generic.IEqualityComparer{``1},System.ReadOnlySpan{System.Object})) (и async) | `InMemoryTests.ToArray_ShouldReturnData`, `ToHashSet_ShouldReturnData`, `ToDictionary_ShouldReturnData` |
| [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) и проецируемый `exists` | `InMemoryTests.SelectAny_ShouldReturnData`, [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) |
| [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) по проекциям с равенством по значению | `InMemoryTests.TestDistinct` |
| Соединения: inner, left, right, full, cross, цепочкой до 8 таблиц | `InMemoryJoinTests.TestJoin`, `TestLeftJoin`, `TestRightJoinChained`, `TestFullJoinChained`, `TestCrossJoin8Tables_ShouldCloneAndMaterializeAtEveryArity` |
| Агрегаты [`Count`](xref:NextORM.Core.EntityBuilderExtensions.Count``1(NextORM.Core.EntityBuilder{``0}))/[`Sum`](xref:NextORM.Core.EntityBuilderExtensions.Sum``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}}))/[`Min`](xref:NextORM.Core.EntityBuilderExtensions.Min``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}}))/[`Max`](xref:NextORM.Core.EntityBuilderExtensions.Max``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}}))/[`Avg`](xref:NextORM.Core.EntityBuilderExtensions.Avg``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}}))/[`Stdev`](xref:NextORM.Core.EntityBuilderExtensions.Stdev``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}}))/[`Var`](xref:NextORM.Core.EntityBuilderExtensions.Var``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}})) (буферизованные источники) | `InMemoryTests.Count_ShouldReturnRowCount`, `Sum_ShouldReturnSum`, `MinMax_ShouldReturnBounds`, `Avg_ShouldReturnAverage` |
| [`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) / [`Having`](xref:NextORM.Core.EntityBuilder`1.Having(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) с агрегатами по группам (буферизованные источники) | `InMemoryTests.GroupBy_ShouldAggregatePerGroup`, `GroupBy_Having_ShouldFilterGroups`, `GroupBy_Avg_ShouldAggregatePerGroup`, `GroupBy_OrderByColumn_ShouldSortGroups` |
| Set-операции `UNION` / `UNION ALL` / `INTERSECT` / `INTERSECT ALL` / `EXCEPT` / `EXCEPT ALL` | `InMemoryTests.Union_ShouldRemoveDuplicates`, `UnionAll_ShouldKeepDuplicates`, `Intersect_ShouldReturnOnlyCommonRows`, `Except_ShouldReturnOnlyLeftRows`, `SetOperations_WhenChained_ShouldApplyLeftToRight` |
| [`Last`](xref:NextORM.Core.EntityBuilderExtensions.Last``1(NextORM.Core.EntityBuilder{``0})) / [`LastOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.LastOrDefault``1(NextORM.Core.EntityBuilder{``0})) по упорядоченному запросу (обратный `ORDER BY`) | `InMemoryTests.Last_ShouldReturnLastOrderedRow`, `LastOrDefault_ShouldReturnLastOrderedRow` |
| Буферизованный подзапрос-источник (`ctx.From(cmd)`) с синхронной материализацией и агрегатами | `InMemoryTests.SubquerySource_SyncToList_ShouldReturnRows`, `SubquerySource_Count_ShouldReturnRowCount`, `SetOperation_AsSubquery_Count_ShouldReturnRowCount` |
| [`SelectMany`](xref:NextORM.Core.EntityBuilder`1.SelectMany``1(System.Linq.Expressions.Expression{System.Func{`0,System.Collections.Generic.IEnumerable{``0}}})) (разворот, коррелированный) и [`GroupJoin`](xref:NextORM.Core.EntityBuilder`1.GroupJoin``3(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``1}},System.Linq.Expressions.Expression{System.Func{``0,``1}},System.Linq.Expressions.Expression{System.Func{`0,System.Collections.Generic.IEnumerable{``0},``2}})) (сгруппированные строки внутренней стороны) | `InMemorySelectManyTests.SelectMany_Correlated_ShouldFlattenPerRow`, `GroupJoin_ShouldGroupInnerRows` |

Провайдер in-memory разделяет тот же fluent-API `EntityBuilder<T>`, что и SQL-провайдеры, поэтому один и тот же объект запроса
работает с обоими — **кроме** [`SelectMany`](xref:NextORM.Core.EntityBuilder`1.SelectMany``1(System.Linq.Expressions.Expression{System.Func{`0,System.Collections.Generic.IEnumerable{``0}}}))/[`GroupJoin`](xref:NextORM.Core.EntityBuilder`1.GroupJoin``3(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``1}},System.Linq.Expressions.Expression{System.Func{``0,``1}},System.Linq.Expressions.Expression{System.Func{`0,System.Collections.Generic.IEnumerable{``0},``2}})): они доступны только в in-memory, а на SQL-провайдере
сразу бросают `NotSupportedException` (см. [Ограничения](../advanced/limitations.md)).

## Не поддерживается

Провайдер громко падает вместо возврата неверных результатов:

- **Табличные функции** бросают `NotSupportedException` с сообщением, упоминающим провайдер in-memory.
  Табличную функцию базы данных нельзя вычислить в процессе.

  ```csharp
  var act = () => ctx
      .FromTableFunction(() => Tvf.SimpleTvf())
      .Select(it => new { it.Id })
      .ToList();
  // NotSupportedException: "... not supported by the in-memory provider."
  ```

- **[`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) по ссылочному типу без равенства по значению** бросает `NotSupportedException` с упоминанием
  `DISTINCT`: SQL сравнивает по значению, что тип CLR не может воспроизвести без переопределения
  `Equals`/`GetHashCode`. Спроецируйте анонимный тип или тип-значение, либо переопределите равенство.

  ```csharp
  ctx.From<SimpleEntity>().Distinct().ToList();
  // NotSupportedException: "DISTINCT is not supported by the in-memory provider for projection type ..."
  ```

- **Raw SQL** ([`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String))) не поддерживается (`NotSupportedException`).
- **Агрегаты или [`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) по источнику `IAsyncEnumerable`** бросают `NotSupportedException`; и то, и другое
  вычисляется для буферизованных источников `IEnumerable`.
- **Агрегаты с фильтром** (`SqlFunctions.Sql.count(e => filter)` и перегрузки `(value, filter)`) и
  **упорядочивание групп по выражению** бросают `NotSupportedException`; упорядочивайте группы по индексу колонки.
- **Set-операции по источнику `IAsyncEnumerable`** бросают `NotSupportedException`; операнды буферизуются.
- **[`Last`](xref:NextORM.Core.EntityBuilderExtensions.Last``1(NextORM.Core.EntityBuilder{``0}))/[`LastOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.LastOrDefault``1(NextORM.Core.EntityBuilder{``0})) без `ORDER BY`** бросают `InvalidOperationException`: у запроса без
  упорядочивания нет определённой последней строки.
- **Агрегаты по асинхронному подзапросу-источнику** бросают `NotSupportedException`; буферизованные
  подзапросы сворачиваются.
- **Коррелированные подзапросы** (подзапрос, ссылающийся на внешнюю строку, например
  `SqlFunctions.Sql.exists(inner.Where(i => i.Id == outer.Id))`) бросают `NotSupportedException`: у
  провайдера нет построчной привязки внешней строки, а отбрасывание корреляции молча вернуло бы
  неверные строки. Для коррелированных запросов используйте SQL-провайдер.

## Различия провайдеров

| Аспект | In-memory |
|---|---|
| Пакет | ядро `nextorm` |
| Соединение | нет |
| SQL-текст | нет (дерево выражений вычисляется в процессе) |
| Плейсхолдеры параметров | не применимо |
| Источники TVF | `NotSupportedException` |
| Raw SQL | `NotSupportedException` |
| [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) по типам без равенства по значению | `NotSupportedException` |
| Коррелированные подзапросы | `NotSupportedException` |
| [`SelectMany`](xref:NextORM.Core.EntityBuilder`1.SelectMany``1(System.Linq.Expressions.Expression{System.Func{`0,System.Collections.Generic.IEnumerable{``0}}})) / [`GroupJoin`](xref:NextORM.Core.EntityBuilder`1.GroupJoin``3(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{`0,``1}},System.Linq.Expressions.Expression{System.Func{``0,``1}},System.Linq.Expressions.Expression{System.Func{`0,System.Collections.Generic.IEnumerable{``0},``2}})) | поддерживаются (эти операторы доступны только в in-memory) |

## См. также

- [Provider overview](overview.md)
- [SQLite](sqlite.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [Query reuse: cache vs Prepare](../guide/15-query-reuse.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `tests/nextorm.core.tests/InMemoryTests.cs:28,46,55,212,226,244,349,374`,
`tests/nextorm.core.tests/InMemoryJoinTests.cs:14,47,64,107,145,162`,
`src/nextorm.core/DataContext/InMemoryDataContext.cs`,
`src/nextorm.core/Builders/InMemoryCommandBuilder.cs`.
