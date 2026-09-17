# SELECT DISTINCT

> Удаляйте дубликаты строк с помощью `Distinct()` в построителе сущности или в спроецированном запросе, в том числе поверх соединений, постраничного вывода и операций над множествами.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Сортировка и постраничный вывод](05-sorting-and-paging.md) · [Операции над множествами](07-set-operations.md)

## Обзор

nextorm предоставляет две точки входа для `SELECT DISTINCT`:

* `Entity<TEntity>.Distinct()` — устанавливает флаг в построителе, до `Select`:

  ```csharp
  dataContext.Create<IComplexEntity>().Distinct().Select(x => new { x.Int })
  ```

* `QueryCommand<TResult>.Distinct()` — устанавливает флаг в уже спроецированном запросе:

  ```csharp
  dataContext.Create<IComplexEntity>().Select(x => new { x.Int }).Distinct()
  ```

Оба варианта дают один и тот же SQL. `DISTINCT` применяется ко всему списку выборки, поэтому
удаляемые дубликаты — это дубликаты проецируемого кортежа, а не отдельного столбца.

## Базовый distinct

```csharp
// complex_entity stores nullableint values null, 1, 1 so DISTINCT must collapse the two 1s.
var rows = dataContext.Create<IComplexEntity>()
    .Select(e => new { e.Int })
    .Distinct()
    .ToList();

var same = dataContext.Create<IComplexEntity>()
    .Distinct()
    .Select(e => new { e.Int })
    .ToList();
```

```sql
select distinct nullableint as 'Int' from complex_entity
```

## Distinct поверх соединения

Спроецированное соединение может содержать дублирующиеся строки; `Distinct` сворачивает их:

```csharp
// complex_entity booleans are true, false, false, so the projected join has duplicate rows.
var rows = dataContext.Create<ISimpleEntity>()
    .Join(dataContext.Create<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { p.t2.Boolean })
    .Distinct()
    .ToList();
```

```sql
select distinct t2.b as 'Boolean' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
```

Перекрёстное соединение порождает полное декартово произведение, которое `Distinct` затем сводит к
различным значениям проецируемого столбца:

```csharp
var all = dataContext.Create<ISimpleEntity>()
    .CrossJoin(dataContext.Create<IComplexEntity>())
    .Select(p => new { p.t2.Id })
    .ToList(); // 30 rows

var distinct = dataContext.Create<ISimpleEntity>()
    .CrossJoin(dataContext.Create<IComplexEntity>())
    .Select(p => new { p.t2.Id })
    .Distinct()
    .ToList(); // 3 rows
```

```sql
select distinct t2.id from simple_entity as 't1' cross join complex_entity as 't2'
```

## Distinct и постраничный вывод

Постраничный вывод применяется к результату distinct. Вызов `Limit`/`Page` размещается до `Select`,
а проверенная форма (`Distinct`, затем `Limit`) определяется порядком ключевых слов провайдера:

```csharp
var distinct = dataContext.Create<IComplexEntity>()
    .Select(e => new { e.Boolean })
    .Distinct()
    .ToList(); // 2 rows

var firstPage = dataContext.Create<IComplexEntity>()
    .Limit(1)
    .Select(e => new { e.Boolean })
    .Distinct()
    .ToList(); // 1 row
```

```sql
-- SQLite / PostgreSQL: the distinct result is paged
select distinct b as 'Boolean' from complex_entity limit 1
```

```sql
-- SQL Server: DISTINCT must precede TOP
select distinct top(1) b as 'Boolean' from complex_entity
```

В SQL Server конструкция `select top(1) distinct ...` недопустима в T-SQL, поэтому диалект
генерирует `distinct` перед `top(n)` (тест генерации SQL фиксирует порядок с помощью
`e.Distinct().Limit(5).Select(x => x.Id)` → `select distinct top(5) id from simple_entity`).

## Distinct и операции над множествами

`Distinct` — это свойство отдельного запроса, а не всей цепочки, поэтому он устраняет дубликаты
только в той ветви, к которой привязан:

```csharp
var cmd = dataContext.Create<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .Union(dataContext.Create<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count(); // 2
```

```sql
-- The left branch carries DISTINCT; UNION removes the remaining duplicates.
select distinct nullableint from complex_entity
 union 
select nullableint from complex_entity
```

При `UnionAll` ветви конкатенируются без дополнительного устранения дубликатов, поэтому левая ветвь
содержит distinct, а правая — нет:

```csharp
var cmd = dataContext.Create<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .UnionAll(dataContext.Create<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count();
// Left branch is distinct ({null, 1}); UNION ALL keeps the right branch ({null, 1, 1}) => 5 rows.
```

```sql
select distinct nullableint from complex_entity
 union all 
select nullableint from complex_entity
```

## Различия между провайдерами

| Провайдер | `DISTINCT` + limit | `DISTINCT` поверх соединения |
|---|---|---|
| SQLite | `select distinct ... limit N` | поддерживается |
| SQL Server | `select distinct top(N) ...` (DISTINCT до TOP) | поддерживается |
| PostgreSQL | `select distinct ... limit N` | поддерживается |
| In-memory | дубликаты удаляются перечислителем in-memory (учитывается `IsDistinct`) | не покрыто набором тестов in-memory |

## См. также

- [Операции над множествами](07-set-operations.md) - `Union` уже удаляет дубликаты; `UnionAll` — нет.
- [Сортировка и постраничный вывод](05-sorting-and-paging.md) - `Limit`, `Offset` и `Page`.
- [Запросы и проекции](01-querying-and-projections.md)

---

Source: `test/nextorm.integration.tests/CommonTestSuite.Distinct.cs:9`,
`test/nextorm.sqlite.tests/SqlGenerationTests.cs:26`,
`test/nextorm.sqlserver.tests/SqlGenerationTests.cs:26,35`,
`test/nextorm.postgres.tests/SqlGenerationTests.cs:25`.
