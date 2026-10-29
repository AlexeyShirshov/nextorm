# Сортировка и постраничная выборка

> Упорядочивайте строки с помощью [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32))/[`OrderByDescending`](xref:NextORM.Core.EntityBuilder`1.OrderByDescending(System.Int32)), разбивайте их на страницы с помощью [`Limit`](xref:NextORM.Core.Paging.Limit)/[`Offset`](xref:NextORM.Core.Paging.Offset)/[`Page`](xref:NextORM.Core.EntityBuilder`1.Page(System.Int32,System.Int32)) и читайте одну строку или логическое значение терминальными методами для одной строки.

**Предварительные требования:** [Запросы и проекции](../engine/index.md) · [Фильтрация (WHERE)](03-filtering-where.md)

## Обзор

Упорядочивание и постраничная выборка применяются к команде до её выполнения, поэтому они становятся
частью генерируемого оператора, а не клиентской операцией:

* [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) предоставляет `OrderBy(Expression<Func<TEntity, object?>>, OrderDirection)`,
  `OrderBy(expr)`, `OrderByDescending(expr)` и перегрузки с порядковым номером `OrderBy(int)`,
  `OrderBy(int, OrderDirection)`, `OrderByDescending(int)`, а также перегрузки с явным размещением NULL
  `OrderBy(expr, NullsPosition)` / `OrderBy(expr, OrderDirection, NullsPosition)` /
  `OrderByDescending(expr, NullsPosition)` и семейство `ThenBy`/`ThenByDescending` (см.
  [Упорядочивание NULL и размещение](#упорядочивание-null-и-размещение)).
* [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) также предоставляет `Limit(int)`, `Offset(int)` и `Page(int limit, int offset)`.
* После [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) возвращаемый [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) повторяет поверхность построителя: перегрузки по выражению
  `OrderBy(expr)`, `OrderBy(expr, OrderDirection)` и `OrderByDescending(expr)`, перегрузки с порядковым номером
  `OrderBy(int columnIndex, OrderDirection direction)`, `OrderBy(int)` и `OrderByDescending(int)`, а также
  `Limit(int)`, `Offset(int)` и `Page(int limit, int offset)` (см. [Сортировка и пейджинг спроецированной команды](#сортировка-и-пейджинг-спроецированной-команды)).

Каждый вызов добавляется к неизменяемому построителю, поэтому второй [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)) добавляет ключ
дополнительной сортировки и оставляет первый на месте. Терминальный метод ([`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])),
[`AnyAsync`](xref:NextORM.Core.EntityBuilderExtensions.AnyAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), ...) выполняет команду.

## Упорядочивание по выражению

```csharp
var last = await dataContext.From<SimpleEntity>()
    .OrderByDescending(it => it.Id)
    .Select(it => it.Id)
    .FirstAsync();
```

```sql
-- SQLite
select id
 from simple_entity order by id desc limit 1
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Id |
|----|
| 10 |

По возрастанию - значение по умолчанию, и оно явно не записывается:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .OrderBy(it => it.Int)
    .OrderByDescending(it => it.Id)
    .Select(it => new { it.Id })
    .ToListAsync();
```

```sql
select id
 from complex_entity order by nullableint, id desc
```

Ключи генерируются в порядке выполнения вызовов и разделяются запятыми.

## Упорядочивание по порядковому номеру проецируемой колонки

После проекции ключом сортировки может быть порядковый номер колонки списка выборки (счёт с 1). Это
обычный способ упорядочить по вычисляемой колонке без повторения выражения:

```csharp
var last = await dataContext.From<SimpleEntity>()
    .Select(it => it.Id)
    .OrderByDescending(1)
    .First();
```

```sql
-- SQLite
select id
 from simple_entity order by 1 desc limit 1
```

Вывод:

| Id |
|----|
| 10 |

Построитель сущности также принимает порядковый номер с явным направлением, например
`.OrderBy(2, OrderDirection.Asc)`.

## Сортировка и пейджинг спроецированной команды

Спроецированный [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) поддерживает сортировку по выражению над
`TResult` и пейджинг, поэтому сгруппированный/агрегированный запрос не обязан повторять агрегат во
внешнем запросе. Каждый член выражения сортировки разрешается в выражение проекции, которое произвело
соответствующую выходную колонку, поэтому в `ORDER BY` попадает исходное выражение, а не алиас:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .GroupBy(it => it.Int)
    .Select(it => new { it.Int, Count = SqlFunctions.Sql.count() })
    .OrderByDescending(it => it.Count)
    .Page(10, 0)
    .ToListAsync();
```

```sql
-- SQLite
select nullableint as 'Int', count(*) as 'Count'
 from complex_entity
 group by nullableint
 order by count(*) desc
limit 10 offset 0
```

`Limit(int)` задаёт только размер страницы, `Offset(int)` — только число пропускаемых строк; границы
те же, что и у методов построителя (неотрицательные). `OrderBy(int)`/`OrderByDescending(int)` по-прежнему
принимают порядковый номер выходной колонки (счёт с 1), поэтому `.Select(it => it.Id).OrderByDescending(1)`
продолжает работать. Член сортировки, отсутствующий в проекции, отклоняется на этапе подготовки запроса.

## Упорядочивание NULL и размещение

Каждый ключ сортировки принимает явное размещение NULL через [`NullsPosition`](xref:NextORM.Core.NullsPosition):

| Член | Значение | Смысл |
|---|---|---|
| [`Default`](xref:NextORM.Core.NullsPosition.Default) | `0` | не генерировать клаузу и сохранить умолчание провайдера (значение любого вызова без размещения) |
| [`First`](xref:NextORM.Core.NullsPosition.First) | `1` | значения `NULL` идут перед всеми не-null значениями независимо от направления |
| [`Last`](xref:NextORM.Core.NullsPosition.Last) | `2` | значения `NULL` идут после всех не-null значений независимо от направления |

Размещение **не зависит от направления**: `First` оставляет группу `NULL` первой и в восходящем, и в
нисходящем ключе, а направление упорядочивает только не-null значения.

### Перегрузки

Каждая форма ниже есть у [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) и — после
[`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) — у [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1):

* `OrderBy(expr, NullsPosition)`, `OrderBy(expr, OrderDirection, NullsPosition)` и
  `OrderByDescending(expr, NullsPosition)`;
* семейство вторичных ключей `ThenBy(expr)`, `ThenBy(expr, OrderDirection)`,
  `ThenBy(expr, NullsPosition)`, `ThenBy(expr, OrderDirection, NullsPosition)`,
  `ThenByDescending(expr)` и `ThenByDescending(expr, NullsPosition)`;
* те же семейства по порядковому номеру выходной колонки (счёт с 1): `OrderBy(int, NullsPosition)`,
  `OrderBy(int, OrderDirection, NullsPosition)`, `OrderByDescending(int, NullsPosition)`, `ThenBy(int)`,
  `ThenBy(int, OrderDirection)`, `ThenBy(int, NullsPosition)`, `ThenBy(int, OrderDirection, NullsPosition)`,
  `ThenByDescending(int)` и `ThenByDescending(int, NullsPosition)`.

`ThenBy` добавляет ключ точно так же, как второй `OrderBy`; имя лишь выражает намерение. Каждый ключ несёт
своё направление и размещение, поэтому смешанные вторичные ключи допустимы:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .OrderBy(it => it.Int, NullsPosition.Last)
    .ThenBy(it => it.Id, OrderDirection.Desc, NullsPosition.First)
    .Select(it => new { it.Id })
    .ToListAsync();
```

```sql
-- SQLite
select id
 from complex_entity order by nullableint NULLS LAST, id desc NULLS FIRST
```

```sql
-- SQL Server
select id
 from complex_entity order by CASE WHEN nullableint IS NULL THEN 1 ELSE 0 END ASC, nullableint, CASE WHEN id IS NULL THEN 0 ELSE 1 END ASC, id desc
```

Порядковый ключ разрешается в выражение выбранной колонки до применения размещения, поэтому эмуляция
проверяет настоящий ключ (`CASE WHEN nullableint IS NULL …`), а не литеральный номер
(`CASE WHEN 1 IS NULL …`); индекс, не разрешающийся в выбранную колонку, падает fail-closed.

### Отрисовка по провайдерам

SQLite, PostgreSQL и ClickHouse понимают нативный модификатор
([`SupportsNullsOrdering`](xref:NextORM.Core.ISqlDialect.SupportsNullsOrdering) равен `true`). В SQL Server,
MySQL и MariaDB синтаксиса `NULLS FIRST`/`NULLS LAST` нет, поэтому они получают портируемую эмуляцию, у
которой ранг-префикс **всегда восходящий** — даже для нисходящего ключа — а запрошенное направление несёт
только сам ключ:

| Провайдер | `ASC` / `First` | `DESC` / `Last` |
|---|---|---|
| SQLite, PostgreSQL, ClickHouse | `k NULLS FIRST` | `k desc NULLS LAST` |
| SQL Server, MySQL, MariaDB | `CASE WHEN k IS NULL THEN 0 ELSE 1 END ASC, k` | `CASE WHEN k IS NULL THEN 1 ELSE 0 END ASC, k desc` |

`k` — квалифицированный и заквотированный SQL ключа. У эмулирующих провайдеров параметризованный ключ
встречается в инструкции **дважды** (`CASE WHEN (k * @factor) IS NULL … END ASC, (k * @factor)`), и оба
вхождения привязываются к одному параметру команды; нативная форма генерирует ключ один раз.

### Умолчания провайдеров

`NullsPosition.Default` не генерирует клаузу, поэтому размещение определяется самим провайдером — и
различается по провайдеру и по направлению:

| Провайдер | `ASC` | `DESC` |
|---|---|---|
| SQLite | nulls first | nulls last |
| SQL Server | nulls first | nulls last |
| MySQL | nulls first | nulls last |
| MariaDB | nulls first | nulls last |
| PostgreSQL | nulls last | nulls first |
| ClickHouse | **nulls last** | **nulls last** |
| In-memory | nulls first | nulls last |

ClickHouse (25.8) ставит `NULL` последними в **обоих** направлениях, поэтому, в отличие от остальных
SQL-провайдеров, `DESC` не переносит группу `NULL` в начало. Не полагайтесь на единый для всех провайдеров
порядок null в общих запросах — указывайте `NullsPosition.First`/`Last`, когда размещение является частью
результата.

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .OrderByDescending(it => it.Int)
    .Select(it => new { it.Id })
    .ToListAsync();
```

С этим умолчанием строка с `null` в `nullableint` (id `1`) идёт первой в PostgreSQL и последней в SQL
Server.

### Формы, падающие fail-closed

Эмуляция через `CASE` повторяет ключ сортировки, поэтому в SQL Server, MySQL и MariaDB явное размещение
отклоняется с `BuildSqlCommandException` — до отправки любой инструкции — там, где инструкция не может
законно нести повторённый ключ: запрос `DISTINCT`/`DISTINCT ON` (каждое выражение `ORDER BY` должно быть в
списке выборки) и `LIMIT BY` в стиле ClickHouse. Размещение никогда не отбрасывается молча. Нативные
провайдеры (SQLite, PostgreSQL, ClickHouse) ничего не повторяют и не ограничены: `DISTINCT`, `DISTINCT ON`
и `LIMIT BY` сохраняют нативный модификатор. См. [Ограничения](../advanced/limitations.md).

### In-memory

Провайдер in-memory соблюдает то же явное размещение с той же семантикой и совпадает с SQL-оракулом
результата ячейка в ячейку. Он сравнивает компаратором CLR по умолчанию, поэтому строки подчиняются
сравнению CLR (`Comparer<string>.Default`), а **не** коллации базы данных.

## Limit и Offset

`Limit(int)` ограничивает число строк, а `Offset(int)` пропускает строки перед результатом:

```csharp
var page = await dataContext.From<SimpleEntity>()
    .Offset(1)
    .Select(it => it.Id)
    .FirstAsync();
```

```sql
-- SQLite
select id
 from simple_entity limit 1 offset 1
```

Вывод:

| Id |
|----|
| 2 |

[`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) применяет свой лимит постраничной выборки для одной строки (`limit 1` в SQLite и
PostgreSQL, `top(1)` в SQL Server; см. ниже). Обычный `Limit(5).Select(it => it.Id)` даёт
`select id from simple_entity limit 5` в SQLite и PostgreSQL, и
`select top(5) id from simple_entity` в SQL Server.

## Page

`Page(limit, offset)` задаёт обе границы одним вызовом:

```csharp
var page = await dataContext.From<SimpleEntity>()
    .Page(5, 10)
    .Select(it => it.Id)
    .ToListAsync();
```

| Провайдер | SQL для `Page(5, 10)` |
|---|---|
| SQLite | `select id from simple_entity limit 5 offset 10` |
| PostgreSQL | `select id from simple_entity limit 5 offset 10` |
| SQL Server | `select id from simple_entity order by (select null as anyorder) offset 10 rows fetch next 5 rows only` |

SQL Server отклоняет `OFFSET ... FETCH` без `ORDER BY`, поэтому провайдер подставляет
`order by (select null as anyorder)` при постраничной выборке без явной сортировки. Когда [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32))
присутствует, используется он, и ничего не подставляется.

Генерация offset/limit по провайдерам:

| Провайдер | `Limit(5)` | `Offset(10)` | `Page(5, 10)` |
|---|---|---|---|
| SQLite | `limit 5` | `limit -1 offset 10` | `limit 5 offset 10` |
| SQL Server | `select top(5) ...` | `offset 10 rows` (плюс подставленный `ORDER BY`) | `offset 10 rows fetch next 5 rows only` (плюс подставленный `ORDER BY`) |
| PostgreSQL | `limit 5` | `offset 10` | `limit 5 offset 10` |

В SQLite нет `OFFSET` без `LIMIT`, поэтому запрос только с offset генерирует сигнальное значение
`limit -1`.

## Limit By (ClickHouse)

`LimitBy(limit, expr)` генерирует ClickHouse [`LIMIT n BY expr`](xref:NextORM.Core.ISqlDialect.LimitBy):
не более `limit` строк на каждое значение ключа. Ключом может быть одна колонка или анонимный тип для
нескольких колонок; перегрузка принимает `offset` на группу (`LIMIT offset, n BY expr`). Клауза идёт
после `ORDER BY` и перед финальным `LIMIT`:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .OrderBy(x => x.Id)
    .LimitBy(2, x => x.Int)
    .Select(x => new { x.Id, x.Int })
    .ToListAsync();
```

```sql
select id, nullableint
 from complex_entity order by id limit 2 by nullableint
```

Поддерживается только ClickHouse; остальные SQL-провайдеры и контекст in-memory бросают
`NotSupportedException`.

## With Ties (PostgreSQL, SQL Server)

[`WithTies`](xref:NextORM.Core.EntityBuilder`1.WithTies) превращает страницу в запрос `WITH TIES`:
результат сохраняет все строки, равные последней строке страницы по `ORDER BY`. Требует
положительного лимита страницы и `ORDER BY`:

```csharp
var rows = await dataContext.From<IComplexEntity>()
    .OrderBy(x => x.Int)
    .Limit(3)
    .WithTies()
    .Select(x => new { x.Id, x.Int })
    .ToListAsync();
```

```sql
-- PostgreSQL
select id, nullableint
 from complex_entity order by nullableint fetch first 3 rows with ties
```

```sql
-- SQL Server (без offset): TOP(n) WITH TIES
select top(3) with ties id, nullableint
 from complex_entity order by nullableint
```

`WITH TIES` поддерживают только PostgreSQL и SQL Server; остальные SQL-провайдеры и in-memory
контекст бросают `NotSupportedException`. Сочетание с `DISTINCT`/`DISTINCT ON` отклоняется при
генерации SQL с `BuildSqlCommandException`; сам fluent-вызов `WithTies()` сочетание не проверяет.

В SQL Server модификатор поддерживается только в форме `TOP(n) WITH TIES` без offset. Положительный
`OFFSET` вместе с `WithTies()` — `Page(5, 3).WithTies()` или `Offset(3).Limit(5).WithTies()` —
бросает `NotSupportedException` во время подготовки запроса, до того как SQL отрендерен, отправлен или
закэширован: T-SQL принимает `WITH TIES` только в `TOP`, но не в `OFFSET … FETCH … ONLY`. Используйте
`WithTies()` без `OFFSET` либо обычное разбиение на страницы без ties.

## First, FirstOrDefault, Single, SingleOrDefault

Терминальные методы для одной строки доступны в синхронной и асинхронной формах:

| Терминальный метод | Результат |
|---|---|
| [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) / [`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | первая строка; выбрасывает `InvalidOperationException`, если последовательность пуста |
| [`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0})) / [`FirstOrDefaultAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefaultAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | первая строка или `default`, если пуста |
| [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) / [`SingleAsync`](xref:NextORM.Core.EntityBuilderExtensions.SingleAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | ровно одна строка; выбрасывает, если пуста или больше одной |
| [`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})) / [`SingleOrDefaultAsync`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefaultAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | единственная строка или `default`, если пуста; выбрасывает, если больше одной |

```csharp
var first = await dataContext.From<SimpleEntity>()
    .OrderBy(it => it.Id)
    .Select(it => it.Id)
    .FirstAsync();
```

```sql
-- SQLite
select id
 from simple_entity order by id limit 1
```

Вывод:

| Id |
|----|
| 1 |

```csharp
var only = await dataContext.From<SimpleEntity>()
    .Where(it => it.Id == 2)
    .Select(it => it.Id)
    .SingleAsync();
```

```sql
-- SQLite
select id
 from simple_entity where id = 2 limit 2
```

Вывод:

| Id |
|----|
| 2 |

Лимит - это способ обеспечить одну строку без второго обращения к базе:

* [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) / [`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0})) устанавливают `Paging.Limit = 1` (и [`SingleRow`](xref:NextORM.Core.QueryCommand.SingleRow)) для команды.
* [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) / [`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})) устанавливают `Paging.Limit = 2`; если провайдер возвращает две строки,
  терминальный метод выбрасывает исключение, поэтому [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) никогда не может молча усечь набор
  результатов.

Эти лимиты являются частью формы команды и ключа кэша плана запроса, и они применяются независимо от
того, были ли уже у запроса [`Limit`](xref:NextORM.Core.Paging.Limit)/``

## Any

[`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) и [`AnyAsync`](xref:NextORM.Core.EntityBuilderExtensions.AnyAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) доступны как в [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1), так и в [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1). Они
генерируют предикат `exists(...)` и читают одно логическое значение:

```csharp
var exists = await dataContext.From<SimpleEntity>()
    .Where(it => it.Id == 100)
    .AnyAsync();
```

```sql
-- SQLite
select exists(select *
 from simple_entity where id = 100)
```

В SQL Server нет логического скаляра, поэтому он генерирует тот же предикат как
`select cast(case when exists(...) then 1 else 0 end as bit)`. В отличие от [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})), [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) не нужны
данные строки, поэтому проекция отбрасывается и проверяется только существование.

## Различия провайдеров

| Провайдер | Поведение |
|---|---|
| SQLite | `LIMIT` / `OFFSET`; offset без limit генерирует `limit -1 offset n`; null по умолчанию сортируются как наименьшее значение, явное размещение нативное (`NULLS FIRST`/`NULLS LAST`). |
| SQL Server | `TOP(n)`, когда нет offset; иначе `OFFSET n ROWS` / `FETCH NEXT n ROWS ONLY` с подставленным `ORDER BY`; null по умолчанию сортируются как наименьшее значение, явное размещение эмулируется префиксом `CASE`. |
| PostgreSQL | `LIMIT` / `OFFSET`; null по умолчанию сортируются как наибольшее значение, явное размещение нативное (`NULLS FIRST`/`NULLS LAST`). |
| MySQL | `LIMIT` / `OFFSET`; offset без limit генерирует `limit 18446744073709551615 offset n`; null по умолчанию сортируются как наименьшее значение (первыми по возрастанию), явное размещение эмулируется префиксом `CASE`. |
| MariaDB | То же, что MySQL. |
| ClickHouse | `LIMIT` / `OFFSET`; offset без limit генерирует `limit 18446744073709551615 offset n`; null по умолчанию идут последними в **обоих** направлениях, явное размещение нативное (`NULLS FIRST`/`NULLS LAST`). |
| In-memory | [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)) / [`OrderByDescending`](xref:NextORM.Core.EntityBuilder`1.OrderByDescending(System.Int32)) выполняются через LINQ; null при сортировке по возрастанию идут первыми (компаратор CLR по умолчанию), а явное размещение соблюдается в процессе. |

## См. также

* [Запросы и проекции](../engine/index.md)
* [Фильтрация (WHERE)](03-filtering-where.md)
* [Повторное использование запросов: cache и Prepare](../engine/03-plan-cache-and-reuse.md)
* [Обзор провайдеров](../connecting-providers/overview.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:479`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:489`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:499`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:510`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:521`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:532`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:540`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:551`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:562`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:573`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:592`,
`tests/nextorm.integration.tests/CommonTestSuite/Raw/CommonTestSuite.SqlCommand.cs:602`,
`tests/nextorm.integration.tests/CommonTestSuite/Queries/CommonTestSuite.LinqExtensions.cs:8`,
`tests/nextorm.integration.tests/Postgres/PostgresSpecificTests.cs:24`,
`tests/nextorm.integration.tests/SqlServer/SqlServerSpecificTests.cs:24`,
`tests/nextorm.integration.tests/SqlServer/SqlServerSpecificTests.cs:43`,
`tests/nextorm.integration.tests/Sqlite/SqliteSpecificTests.cs:44`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:133`,
`tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:142`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:146`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:160`,
`tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:173`,
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:130`,
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:139`;
`src/nextorm.core/Query/QueryCommand.TResult.cs:147`,
`src/nextorm.core/Query/QueryCommand.TResult.cs:197`.
