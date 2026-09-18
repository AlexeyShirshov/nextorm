# Провайдер In-memory

> `InMemoryContext` выполняет запросы по внутрипроцессным коллекциям CLR; используйте его для модульных тестов и тестов формы запросов/кэша планов, где запускать базу данных не нужно.

**Предварительные требования:** [Provider overview](overview.md) · [Dependency injection](../getting-started/04-dependency-injection.md)

## Обзор

Провайдер in-memory встроен в `nextorm` (без дополнительного пакета) и находится в
`src/nextorm.core/DataContext/InMemoryDataContext.cs`. Публичный тип — `InMemoryContext`, который
напрямую реализует `IDataContext` — у него нет диалекта, нет соединения и нет SQL, и он вычисляет
дерево выражений запроса по данным, которые вы к нему присоединяете.

В отличие от SQL-провайдеров, `InMemoryContext` не требует строки подключения. Метаданные запросов и списки
выборки являются общими для процесса с SQL-провайдерами через `DataContextCache`, тогда как скомпилированные
делегаты привязаны к экземпляру (они захватывают контекст, для которого были скомпилированы).

Используйте его, когда вы хотите:

- модульные тесты для кода приложения, который зависит от `IDataContext`;
- быстрые тесты формы запросов, проекции и поведения соединений без контейнера или файла;
- тесты неявного кэша планов (`QueryPlanEqualityComparer`, `GetCacheVersion`).

Это не SQL-движок: он не проверяет, что запрос выполнился бы на реальном провайдере, и несколько
возможностей намеренно не поддерживаются (см. ниже).

## Регистрация и использование

С внедрением зависимостей:

```csharp
using nextorm.core;

services.AddNextOrmContext<InMemoryContext>();
```

```csharp
using nextorm.core;

using var ctx = new InMemoryContext();
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

`WithData` и `WithAsyncData` (`src/nextorm.core/Builders/InMemoryCommandBuilder.cs`) присоединяют
коллекцию к словарю `Data` контекста с ключом по типу сущности; `WithData` принимает `IEnumerable<T>`,
а `WithAsyncData` — `IAsyncEnumerable<T>`. Оба являются пустыми операциями для других провайдеров.

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
| `Where`, включая `==` по nullable и захваченным значениям | `InMemoryTests.TestWhere`, `Contains_ShouldFilterData` |
| Подзапрос, используемый как источник `FROM` (`ctx.From(subQuery)`) | `InMemoryTests.TestWhere_Subquery` |
| Буферизованные и асинхронные источники (`WithData` / `WithAsyncData`) | `InMemoryTests.TestAsync` |
| Потоковая передача с `Pipeline`, с соблюдением отмены | `InMemoryTests.TestFetch`, `TestFetch_PipelineStopsOnCancellation` |
| `Limit` / `Offset` / `First` / `Single` и их формы `OrDefault` | `InMemoryTests.Top_ShouldLimitData`, `First_ShouldReturnFirst`, `Single_ShouldReturnSingle` |
| `OrderBy` / `OrderByDescending`, включая асинхронные источники | `InMemoryTests.OrderBy_ShouldSortData`, `OrderByOverAsyncSource_ShouldSortData` |
| Материализаторы `ToArray` / `ToHashSet` / `ToDictionary` (и async) | `InMemoryTests.ToArray_ShouldReturnData`, `ToHashSet_ShouldReturnData`, `ToDictionary_ShouldReturnData` |
| `Any` и проецируемый `exists` | `InMemoryTests.SelectAny_ShouldReturnData`, `Any` |
| `Distinct` по проекциям с равенством по значению | `InMemoryTests.TestDistinct` |
| Соединения: inner, left, right, full, cross, цепочкой до 8 таблиц | `InMemoryJoinTests.TestJoin`, `TestLeftJoin`, `TestRightJoinChained`, `TestFullJoinChained`, `TestCrossJoin8Tables_ShouldCloneAndMaterializeAtEveryArity` |
| Агрегаты `Count`/`Sum`/`Min`/`Max`/`Avg`/`Stdev`/`Var` (буферизованные источники) | `InMemoryTests.Count_ShouldReturnRowCount`, `Sum_ShouldReturnSum`, `MinMax_ShouldReturnBounds`, `Avg_ShouldReturnAverage` |
| `GroupBy` / `Having` с агрегатами по группам (буферизованные источники) | `InMemoryTests.GroupBy_ShouldAggregatePerGroup`, `GroupBy_Having_ShouldFilterGroups`, `GroupBy_Avg_ShouldAggregatePerGroup`, `GroupBy_OrderByColumn_ShouldSortGroups` |
| Set-операции `UNION` / `UNION ALL` / `INTERSECT` / `INTERSECT ALL` / `EXCEPT` / `EXCEPT ALL` | `InMemoryTests.Union_ShouldRemoveDuplicates`, `UnionAll_ShouldKeepDuplicates`, `Intersect_ShouldReturnOnlyCommonRows`, `Except_ShouldReturnOnlyLeftRows`, `SetOperations_WhenChained_ShouldApplyLeftToRight` |
| `Last` / `LastOrDefault` по упорядоченному запросу (обратный `ORDER BY`) | `InMemoryTests.Last_ShouldReturnLastOrderedRow`, `LastOrDefault_ShouldReturnLastOrderedRow` |
| Буферизованный подзапрос-источник (`ctx.From(cmd)`) с синхронной материализацией и агрегатами | `InMemoryTests.SubquerySource_SyncToList_ShouldReturnRows`, `SubquerySource_Count_ShouldReturnRowCount`, `SetOperation_AsSubquery_Count_ShouldReturnRowCount` |
| `SelectMany` (разворот, коррелированный) и `GroupJoin` (сгруппированные строки внутренней стороны) | `InMemorySelectManyTests.SelectMany_Correlated_ShouldFlattenPerRow`, `GroupJoin_ShouldGroupInnerRows` |

Провайдер in-memory разделяет тот же fluent-API `EntityBuilder<T>`, что и SQL-провайдеры, поэтому один и тот же объект запроса
работает с обоими — **кроме** `SelectMany`/`GroupJoin`: они доступны только в in-memory, а на SQL-провайдере
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

- **`Distinct` по ссылочному типу без равенства по значению** бросает `NotSupportedException` с упоминанием
  `DISTINCT`: SQL сравнивает по значению, что тип CLR не может воспроизвести без переопределения
  `Equals`/`GetHashCode`. Спроецируйте анонимный тип или тип-значение, либо переопределите равенство.

  ```csharp
  ctx.From<SimpleEntity>().Distinct().ToList();
  // NotSupportedException: "DISTINCT is not supported by the in-memory provider for projection type ..."
  ```

- **Raw SQL** (`PrepareFromSql`) не реализован (`NotImplementedException`).
- **Агрегаты или `GroupBy` по источнику `IAsyncEnumerable`** бросают `NotSupportedException`; и то, и другое
  вычисляется для буферизованных источников `IEnumerable`.
- **Агрегаты с фильтром** (`NORM.SQL.count(e => filter)` и перегрузки `(value, filter)`) и
  **упорядочивание групп по выражению** бросают `NotSupportedException`; упорядочивайте группы по индексу колонки.
- **Set-операции по источнику `IAsyncEnumerable`** бросают `NotSupportedException`; операнды буферизуются.
- **`Last`/`LastOrDefault` без `ORDER BY`** бросают `InvalidOperationException`: у запроса без
  упорядочивания нет определённой последней строки.
- **Агрегаты по асинхронному подзапросу-источнику** бросают `NotSupportedException`; буферизованные
  подзапросы сворачиваются.

## Различия провайдеров

| Аспект | In-memory |
|---|---|
| Пакет | ядро `nextorm` |
| Соединение | нет |
| SQL-текст | нет (дерево выражений вычисляется в процессе) |
| Плейсхолдеры параметров | не применимо |
| Источники TVF | `NotSupportedException` |
| Raw SQL | `NotImplementedException` |
| `Distinct` по типам без равенства по значению | `NotSupportedException` |
| `SelectMany` / `GroupJoin` | поддерживаются (эти операторы доступны только в in-memory) |

## См. также

- [Provider overview](overview.md)
- [SQLite](sqlite.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [Query reuse: cache vs Prepare](../guide/15-query-reuse.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `test/nextorm.core.tests/InMemoryTests.cs:28,46,55,212,226,244,349,374`,
`test/nextorm.core.tests/InMemoryJoinTests.cs:14,47,64,107,145,162`,
`src/nextorm.core/DataContext/InMemoryDataContext.cs`,
`src/nextorm.core/Builders/InMemoryCommandBuilder.cs`.
