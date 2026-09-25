# Сортировка и постраничная выборка

> Упорядочивайте строки с помощью [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32))/[`OrderByDescending`](xref:NextORM.Core.EntityBuilder`1.OrderByDescending(System.Int32)), разбивайте их на страницы с помощью [`Limit`](xref:NextORM.Core.Paging.Limit)/[`Offset`](xref:NextORM.Core.Paging.Offset)/[`Page`](xref:NextORM.Core.EntityBuilder`1.Page(System.Int32,System.Int32)) и читайте одну строку или логическое значение терминальными методами для одной строки.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Фильтрация (WHERE)](02-filtering-where.md)

## Обзор

Упорядочивание и постраничная выборка применяются к команде до её выполнения, поэтому они становятся
частью генерируемого оператора, а не клиентской операцией:

* [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) предоставляет `OrderBy(Expression<Func<TEntity, object?>>, OrderDirection)`,
  `OrderBy(expr)`, `OrderByDescending(expr)` и перегрузки с порядковым номером `OrderBy(int)`,
  `OrderBy(int, OrderDirection)`, `OrderByDescending(int)`.
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
select id from simple_entity order by id desc limit 1
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
select id from complex_entity order by nullableint, id desc
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
select id from simple_entity order by 1 desc limit 1
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
select nullableint as 'Int', count(*) as 'Count' from complex_entity
 group by nullableint
 order by count(*) desc
limit 10 offset 0
```

`Limit(int)` задаёт только размер страницы, `Offset(int)` — только число пропускаемых строк; границы
те же, что и у методов построителя (неотрицательные). `OrderBy(int)`/`OrderByDescending(int)` по-прежнему
принимают порядковый номер выходной колонки (счёт с 1), поэтому `.Select(it => it.Id).OrderByDescending(1)`
продолжает работать. Член сортировки, отсутствующий в проекции, отклоняется на этапе подготовки запроса.

## Упорядочивание NULL

nextorm не генерирует `NULLS FIRST` / `NULLS LAST`; размещение null определяется тем, что провайдер
делает по умолчанию:

| Провайдер | `ASC` | `DESC` |
|---|---|---|
| SQLite | nulls first | nulls last |
| SQL Server | nulls first | nulls last |
| PostgreSQL | nulls last | nulls first |
| In-memory | nulls first | nulls last |

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .OrderByDescending(it => it.Int)
    .Select(it => new { it.Id })
    .ToListAsync();
```

В PostgreSQL строка с `null` в `nullableint` (id `1`) идёт первой; в SQL Server - последней. Не
полагайтесь на единый для всех провайдеров порядок null в общих запросах.

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
select id from simple_entity limit 1 offset 1
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
select id, nullableint from complex_entity order by id limit 2 by nullableint
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
select id, nullableint from complex_entity order by nullableint fetch first 3 rows with ties
```

```sql
-- SQL Server (без offset): TOP(n) WITH TIES
select top(3) with ties id, nullableint from complex_entity order by nullableint
```

`WITH TIES` поддерживают только PostgreSQL и SQL Server; остальные SQL-провайдеры и in-memory
контекст бросают `NotSupportedException`. Сочетание с `DISTINCT`/`DISTINCT ON` отклоняется.

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
select id from simple_entity order by id limit 1
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
select id from simple_entity where id = 2 limit 2
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
select exists(select * from simple_entity where id = 100)
```

В SQL Server нет логического скаляра, поэтому он генерирует тот же предикат как
`select cast(case when exists(...) then 1 else 0 end as bit)`. В отличие от [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})), [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) не нужны
данные строки, поэтому проекция отбрасывается и проверяется только существование.

## Различия провайдеров

| Провайдер | Поведение |
|---|---|
| SQLite | `LIMIT` / `OFFSET`; offset без limit генерирует `limit -1 offset n`; null сортируются как наименьшее значение. |
| SQL Server | `TOP(n)`, когда нет offset; иначе `OFFSET n ROWS` / `FETCH NEXT n ROWS ONLY` с подставленным `ORDER BY`; null сортируются как наименьшее значение. |
| PostgreSQL | `LIMIT` / `OFFSET`; null сортируются как наибольшее значение. |
| MySQL | `LIMIT` / `OFFSET`; offset без limit генерирует `limit 18446744073709551615 offset n`; null сортируются как наименьшее значение (первыми по возрастанию). |
| MariaDB | То же, что MySQL. |
| ClickHouse | `LIMIT` / `OFFSET`; offset без limit генерирует `limit 18446744073709551615 offset n`. |
| In-memory | [`OrderBy`](xref:NextORM.Core.EntityBuilder`1.OrderBy(System.Int32)) / [`OrderByDescending`](xref:NextORM.Core.EntityBuilder`1.OrderByDescending(System.Int32)) выполняются через LINQ; null при сортировке по возрастанию идут первыми (компаратор CLR по умолчанию). |

## См. также

* [Запросы и проекции](01-querying-and-projections.md)
* [Фильтрация (WHERE)](02-filtering-where.md)
* [Повторное использование запросов: cache и Prepare](15-query-reuse.md)
* [Обзор провайдеров](../providers/overview.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:479`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:489`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:499`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:510`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:521`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:532`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:540`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:551`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:562`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:573`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:592`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:602`,
`tests/nextorm.integration.tests/CommonTestSuite.LinqExtensions.cs:8`,
`tests/nextorm.integration.tests/PostgresSpecificTests.cs:24`,
`tests/nextorm.integration.tests/SqlServerSpecificTests.cs:24`,
`tests/nextorm.integration.tests/SqlServerSpecificTests.cs:43`,
`tests/nextorm.integration.tests/SqliteSpecificTests.cs:44`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:133`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:142`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:146`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:160`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:173`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:130`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:139`;
`src/nextorm.core/Query/QueryCommand.TResult.cs:147`,
`src/nextorm.core/Query/QueryCommand.TResult.cs:197`.
