# SELECT DISTINCT

> Удаляйте дубликаты строк проекции с помощью [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct). `DISTINCT` сравнивает всю строку, а не отдельный столбец.

**Предварительные требования:** [Запросы и проекции](../engine/index.md) · [Сортировка и постраничный вывод](06-sorting-and-paging.md) · [Операции над множествами](08-set-operations.md)

## Обзор

`DISTINCT` удаляет повторяющиеся строки проекции: строки считаются одинаковыми, только если
совпадают все выбранные значения. В nextorm есть два способа добавить `SELECT DISTINCT`:

* [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) — устанавливает флаг в построителе, до [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})):

  ```csharp
  dataContext.From<IComplexEntity>().Distinct().Select(x => new { x.Int })
  ```

* [`Distinct`](xref:NextORM.Core.QueryCommand`1.Distinct) — устанавливает флаг в уже спроецированном запросе:

  ```csharp
  dataContext.From<IComplexEntity>().Select(x => new { x.Int }).Distinct()
  ```

Оба способа дают один и тот же SQL. Выберите любой: `Distinct` можно вызвать до или после `Select`.

## Базовый distinct

В этом примере выбирается один столбец, а повторяющиеся значения в результате удаляются:

```csharp
// complex_entity stores nullableint values null, 1, 1 so DISTINCT must collapse the two 1s.
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Int })
    .Distinct()
    .ToList();

var same = dataContext.From<IComplexEntity>()
    .Distinct()
    .Select(e => new { e.Int })
    .ToList();
```

```sql
select distinct nullableint as 'Int'
 from complex_entity
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/TestProviders/SqliteTestProvider.cs`).

Вывод:

| Int |
|-----|
| null |
| 1 |

## Distinct поверх соединения

В результатах соединения могут повторяться значения проекции. [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) удаляет такие повторяющиеся строки:

```csharp
// complex_entity booleans are true, false, false, so the projected join has duplicate rows.
var rows = dataContext.From<ISimpleEntity>()
    .Join<IComplexEntity>((s, c) => s.Id == c.Id)
    .Select(p => new { p.Item2.Boolean })
    .Distinct()
    .ToList();
```

```sql
select distinct t2.b as 'Boolean'
 from simple_entity as 't1'
 join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

Перекрёстное соединение порождает полное декартово произведение, которое [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) затем сводит к
различным значениям проецируемого столбца:

```csharp
var all = dataContext.From<ISimpleEntity>()
    .CrossJoin(dataContext.From<IComplexEntity>())
    .Select(p => new { p.Item2.Id })
    .ToList(); // 30 rows

var distinct = dataContext.From<ISimpleEntity>()
    .CrossJoin(dataContext.From<IComplexEntity>())
    .Select(p => new { p.Item2.Id })
    .Distinct()
    .ToList(); // 3 rows
```

```sql
select distinct t2.id
 from simple_entity as 't1'
 cross join complex_entity as 't2'
```

## Distinct и постраничный вывод

Сначала удаляются дубликаты, затем к результату применяется постраничный вывод. В цепочке
построителя вызовите [`Limit`](xref:NextORM.Core.Paging.Limit) или
[`Page`](xref:NextORM.Core.EntityBuilder`1.Page(System.Int32,System.Int32)) перед
[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})).
Порядок ключевых слов в SQL зависит от провайдера:

```csharp
var distinct = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Boolean })
    .Distinct()
    .ToList(); // 2 rows

var firstPage = dataContext.From<IComplexEntity>()
    .Limit(1)
    .Select(e => new { e.Boolean })
    .Distinct()
    .ToList(); // 1 row
```

```sql
-- SQLite / PostgreSQL: the distinct result is paged
select distinct b as 'Boolean'
 from complex_entity limit 1
```

```sql
-- SQL Server: DISTINCT must precede TOP
select distinct top(1) b as 'Boolean'
 from complex_entity
```

В SQL Server конструкция `select top(1) distinct ...` недопустима в T-SQL. Поэтому диалект ставит
`distinct` перед `top(n)`. Тест генерации SQL фиксирует порядок на примере
`e.Distinct().Limit(5).Select(x => x.Id)` → `select distinct top(5) id from simple_entity`.

## Distinct и операции над множествами

[`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) относится к отдельному запросу, а не ко
всей цепочке операций над множествами. Он удаляет дубликаты только в той ветви, где его вызвали:

```csharp
var cmd = dataContext.From<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .Union(dataContext.From<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count(); // 2
```

```sql
-- The left branch carries DISTINCT; UNION removes the remaining duplicates.
select distinct nullableint
 from complex_entity
 union 
select nullableint
 from complex_entity
```

При [`UnionAll`](xref:NextORM.Core.QueryCommand`1.UnionAll``1(NextORM.Core.QueryCommand{``0})) ветви
соединяются без дополнительного удаления дубликатов. Левая ветвь уже distinct, а правая — нет:

```csharp
var cmd = dataContext.From<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .UnionAll(dataContext.From<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count();
// Left branch is distinct ({null, 1}); UNION ALL keeps the right branch ({null, 1, 1}) => 5 rows.
```

```sql
select distinct nullableint
 from complex_entity
 union all 
select nullableint
 from complex_entity
```

## `DISTINCT ON` (PostgreSQL)

PostgreSQL также поддерживает `DISTINCT ON (expr, ...)`: эта форма оставляет первую строку для
каждого ключа согласно `ORDER BY`. Начальные выражения сортировки должны совпадать с ключом.
Используйте [`DistinctOn`](xref:NextORM.Postgres.PostgresEntityBuilderExtensions.DistinctOn``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}}))
вместо `Distinct`. PostgreSQL считает эти формы взаимоисключающими, поэтому их сочетание вызывает
исключение.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .DistinctOn(e => e.String)
    .OrderBy(e => e.String)
    .Select(e => new { e.Id, e.String })
    .ToList();
```

```sql
select distinct on (somestring) id, somestring
 from complex_entity order by somestring
```

Для составного ключа можно использовать анонимный тип. `DISTINCT ON` поддерживает только PostgreSQL;
остальные провайдеры отклоняют его при построении SQL.

## Различия между провайдерами

| Провайдер | `DISTINCT` + limit | `DISTINCT` поверх соединения |
|---|---|---|
| SQLite | `select distinct ... limit N` | поддерживается |
| SQL Server | `select distinct top(N) ...` (DISTINCT до TOP) | поддерживается |
| PostgreSQL | `select distinct ... limit N` | поддерживается |
| MySQL | `select distinct ... limit N` | поддерживается |
| MariaDB | `select distinct ... limit N` | поддерживается |
| ClickHouse | `select distinct ... limit N` | поддерживается |
| In-memory | дубликаты удаляются перечислителем in-memory (учитывается [`IsDistinct`](xref:NextORM.Core.QueryCommand.IsDistinct)) | не покрыто набором тестов in-memory |

## См. также

- [Операции над множествами](08-set-operations.md) - [`Union`](xref:NextORM.Core.QueryCommand`1.Union``1(NextORM.Core.QueryCommand{``0})) уже удаляет дубликаты; [`UnionAll`](xref:NextORM.Core.QueryCommand`1.UnionAll``1(NextORM.Core.QueryCommand{``0})) — нет.
- [Сортировка и постраничный вывод](06-sorting-and-paging.md) - [`Limit`](xref:NextORM.Core.Paging.Limit), [`Offset`](xref:NextORM.Core.Paging.Offset) и [`Page`](xref:NextORM.Core.EntityBuilder`1.Page(System.Int32,System.Int32)).
- [Группировка и агрегаты](05-grouping-and-aggregates.md) - выбор экстремальной строки через `SelectWhereMax` / `SelectWhereMin`.
- [Запросы и проекции](../engine/index.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite/Queries/Rows/CommonTestSuite.Distinct.cs:9`,
`tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:26`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:26,35`,
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:25`.
