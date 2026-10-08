# SELECT DISTINCT

> Удаляйте дубликаты строк с помощью [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) в построителе сущности или в спроецированном запросе, в том числе поверх соединений, постраничного вывода и операций над множествами.

**Предварительные требования:** [Запросы и проекции](../querying/index.md) · [Сортировка и постраничный вывод](04-sorting-and-paging.md) · [Операции над множествами](06-set-operations.md)

## Обзор

nextorm предоставляет две точки входа для `SELECT DISTINCT`:

* [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) — устанавливает флаг в построителе, до [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})):

  ```csharp
  dataContext.From<IComplexEntity>().Distinct().Select(x => new { x.Int })
  ```

* [`Distinct`](xref:NextORM.Core.QueryCommand`1.Distinct) — устанавливает флаг в уже спроецированном запросе:

  ```csharp
  dataContext.From<IComplexEntity>().Select(x => new { x.Int }).Distinct()
  ```

Оба варианта дают один и тот же SQL. `DISTINCT` применяется ко всему списку выборки, поэтому
удаляемые дубликаты — это дубликаты проецируемого кортежа, а не отдельного столбца.

## Базовый distinct

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
select distinct nullableint as 'Int' from complex_entity
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Int |
|-----|
| null |
| 1 |

## Distinct поверх соединения

Спроецированное соединение может содержать дублирующиеся строки; [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) сворачивает их:

```csharp
// complex_entity booleans are true, false, false, so the projected join has duplicate rows.
var rows = dataContext.From<ISimpleEntity>()
    .Join(dataContext.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Select(p => new { p.Item2.Boolean })
    .Distinct()
    .ToList();
```

```sql
select distinct t2.b as 'Boolean' from simple_entity as 't1' join complex_entity as 't2' on cast(t1.id as bigint) = t2.id
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
select distinct t2.id from simple_entity as 't1' cross join complex_entity as 't2'
```

## Distinct и постраничный вывод

Постраничный вывод применяется к результату distinct. Вызов [`Limit`](xref:NextORM.Core.Paging.Limit)/[`Page`](xref:NextORM.Core.EntityBuilder`1.Page(System.Int32,System.Int32)) размещается до [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})),
а проверенная форма ([`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct), затем [`Limit`](xref:NextORM.Core.Paging.Limit)) определяется порядком ключевых слов провайдера:

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

[`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct) — это свойство отдельного запроса, а не всей цепочки, поэтому он устраняет дубликаты
только в той ветви, к которой привязан:

```csharp
var cmd = dataContext.From<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .Union(dataContext.From<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count(); // 2
```

```sql
-- The left branch carries DISTINCT; UNION removes the remaining duplicates.
select distinct nullableint from complex_entity
 union 
select nullableint from complex_entity
```

При [`UnionAll`](xref:NextORM.Core.QueryCommand`1.UnionAll``1(NextORM.Core.QueryCommand{``0})) ветви конкатенируются без дополнительного устранения дубликатов, поэтому левая ветвь
содержит distinct, а правая — нет:

```csharp
var cmd = dataContext.From<IComplexEntity>().Select(it => it.Int)
    .Distinct()
    .UnionAll(dataContext.From<IComplexEntity>().Select(it => it.Int));

var count = dataContext.From(cmd).Count();
// Left branch is distinct ({null, 1}); UNION ALL keeps the right branch ({null, 1, 1}) => 5 rows.
```

```sql
select distinct nullableint from complex_entity
 union all 
select nullableint from complex_entity
```

## `DISTINCT ON` (PostgreSQL)

PostgreSQL дополнительно поддерживает `DISTINCT ON (expr, ...)`, который оставляет первую строку
каждого уникального ключа согласно `ORDER BY` (ведущие выражения сортировки должны совпадать с
ключом). Вместо `Distinct` используйте
[`DistinctOn`](xref:NextORM.Postgres.PostgresEntityBuilderExtensions.DistinctOn``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}})); их сочетание бросает исключение,
поскольку PostgreSQL считает их взаимоисключающими.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .DistinctOn(e => e.String)
    .OrderBy(e => e.String)
    .Select(e => new { e.Id, e.String })
    .ToList();
```

```sql
select distinct on (somestring) id, somestring from complex_entity order by somestring
```

Ключом может быть анонимный тип для составного ключа. `DISTINCT ON` реализован только в PostgreSQL;
остальные провайдеры отклоняют его на этапе построения SQL.

## Выбор экстремальной строки (`SelectWhereMax` / `SelectWhereMin`)

[`SelectWhereMax`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMax``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) и
[`SelectWhereMin`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMin``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) оставляют строку (или строки),
значение которой по селектору максимально или минимально в источнике. Это фильтр строк, а не
агрегат: выжившая строка сохраняет все колонки.

**Глобально и по группам.** Без ключа группировки экстремум берётся по всему источнику; с ключом
группировки экстремальные строки выбираются внутри каждой отдельной группы. Ключ `null` — это
отдельная группа.

```csharp
// Глобально: единственная строка с максимальным score.
var top = dataContext.From<ExtremaEntity>()
    .SelectWhereMax(e => e.Score)
    .ToList();

// По группам: одна экстремальная строка на категорию.
var perGroup = dataContext.From<ExtremaEntity>()
    .SelectWhereMax(e => e.Score, ExtremeRowTies.One, e => e.Category)
    .ToList();
```

```sql
-- PostgreSQL
-- глобально: целочисленный ключ проходит нативно — ORDER BY ... LIMIT 1
select t1.id, t1.score, t1.category, t1.label
from (select * from extrema_entity
 where score is not null order by "score" desc limit 1) as "t1"

-- по группам: строковый ключ группировки не проходит нативно, остаётся переносимое оконное понижение
select t1.id, t1.score, t1.category, t1.label
from (select *, row_number() over (partition by category order by score desc) as "__nextorm_rn" from extrema_entity
 where score is not null) as "t1"
 where t1."__nextorm_rn" = 1
```

[`ExtremeRowTies`](xref:NextORM.Core.ExtremeRowTies) управляет обработкой совпадающих значений. [`One`](xref:NextORM.Core.ExtremeRowTies.One) (по умолчанию)
оставляет одну строку — при равенстве значений провайдер выбирает одну из них, — а [`All`](xref:NextORM.Core.ExtremeRowTies.All)
оставляет все строки, совпадающие по экстремальному значению. Совпадение определяется только
селектором, поэтому результат `All` содержит все такие строки, а `One` — ровно одну произвольную.

**Форма с проекцией.** Перегрузки
[`SelectWhereMax`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMax``2(System.Linq.Expressions.Expression{System.Func{`0,``0}},System.Linq.Expressions.Expression{System.Func{`0,``1}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) /
[`SelectWhereMin`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMin``2(System.Linq.Expressions.Expression{System.Func{`0,``0}},System.Linq.Expressions.Expression{System.Func{`0,``1}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) принимают проекцию и применяют
её к каждой выжившей строке, возвращая типизированную команду запроса:

```csharp
var projected = dataContext.From<ExtremaEntity>()
    .SelectWhereMax(e => e.Score, e => new { e.Id, e.Label })
    .ToList();
```

```sql
-- PostgreSQL
select t1.id, t1.label
from (select * from extrema_entity
 where score is not null order by "score" desc limit 1) as "t1"
```

**Семантика NULL.** Сравниваемые значения `null` игнорируются, поэтому они никогда не выигрывают
экстремум и не попадают в результат. Группа, у которой сравниваемое значение всегда `null`, не даёт
ни одной строки, а глобальная форма возвращает пустой результат, когда все значения `null`.

**Переносимая реализация.** Диалект понижает запрос до производной таблицы с оконной функцией —
`row_number()` для [`One`](xref:NextORM.Core.ExtremeRowTies.One), `rank()` для [`All`](xref:NextORM.Core.ExtremeRowTies.All), `over (partition by <группа> order by <значение> [desc])` —
с фильтром по рангу `1`, а условие источника объединяется через `AND` с фильтром `is not null` по
значению:

```sql
-- переносимая форма: ранжируем строки источника, затем оставляем ранг 1
select <проекция> from (
    select *, row_number() over (partition by <группа> order by <значение> desc) as rn
    from <источник>
    where <значение> is not null and <условие>
) t where t.rn = 1
```

Это реализация на каждом SQL-провайдере (SQLite, SQL Server, PostgreSQL, MySQL, MariaDB и
ClickHouse) и на провайдере in-memory. На PostgreSQL и ClickHouse нативно подходящий запрос с `One`
вместо этого рендерится нативно, автоматически, по провайдеру и форме запроса — см.
[Нативные стратегии выбора экстремальной строки](../advanced/select-where-extrema-native.md). Любая
другая форма сохраняет переносимое понижение выше.

Запрос отклоняется на этапе построения SQL при сочетании с другим модификатором формы строк, который
не выражается через производную таблицу: [`DistinctOn`](xref:NextORM.Postgres.PostgresEntityBuilderExtensions.DistinctOn``2(NextORM.Core.EntityBuilder{``0},System.Linq.Expressions.Expression{System.Func{``0,``1}})), [`GroupBy`](xref:NextORM.Core.EntityBuilder`1.GroupBy``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})), соединение, `Having`,
именованные окна, `LimitBy`, `ArrayJoin`, `PreWhere`, постраничный вывод, операция над множествами
или нефизический источник.

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

- [Операции над множествами](06-set-operations.md) - [`Union`](xref:NextORM.Core.QueryCommand`1.Union``1(NextORM.Core.QueryCommand{``0})) уже удаляет дубликаты; [`UnionAll`](xref:NextORM.Core.QueryCommand`1.UnionAll``1(NextORM.Core.QueryCommand{``0})) — нет.
- [Сортировка и постраничный вывод](04-sorting-and-paging.md) - [`Limit`](xref:NextORM.Core.Paging.Limit), [`Offset`](xref:NextORM.Core.Paging.Offset) и [`Page`](xref:NextORM.Core.EntityBuilder`1.Page(System.Int32,System.Int32)).
- [Запросы и проекции](../querying/index.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.Distinct.cs:9`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:26`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:26,35`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:25`,
`tests/nextorm.integration.tests/CommonTestSuite.SelectWhereExtrema.cs:41`,
`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:1353`.
