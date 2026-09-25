# Материализация запроса в таблицу (`CREATE TABLE ... AS SELECT`)

> nextorm умеет выполнить обычный запрос и материализовать его строки в таблицу одной командой — через
> [`ToTempTable<TResult>()`](xref:NextORM.Core.TempTableExtensions) / `ToTable<TResult>()`. В отличие от
> [обобщённого табличного выражения (CTE)](09-cte.md), которое переиспользуется только внутри одного
> запроса, материализованная таблица доступна последующим запросам на том же соединении. Изменения не
> отслеживаются, `SaveChanges` нет: терминал выполняет ровно одну команду.

**Предварительно:** [Обобщённые табличные выражения](09-cte.md) · [Изменение данных (INSERT)](19-insert-statement.md) · [Сырой SQL](14-raw-sql.md) · [Обзор провайдеров](../providers/overview.md)

## Создание временной таблицы

`ToTempTable(name)` строит `CREATE TEMPORARY TABLE <name> AS <select>` из запроса, к которому вызван, и выполняет его:

```csharp
ctx.From<IOrder>()
    .Where(x => x.Total > minTotal)
    .Select(x => new { x.Id, x.Total })
    .ToTempTable("recent_orders");

await ctx.From<IOrder>()
    .Where(x => x.Total > minTotal)
    .Select(x => new { x.Id, x.Total })
    .ToTempTableAsync("recent_orders", cancellationToken: cancellationToken);
```

Вызовите `ToTempTable()` без имени — имя сгенерируется, а терминал вернёт его, чтобы таблицу можно было прочитать обратно. Сгенерированные имена выглядят как `__nextorm_temp_xxxxxxxx`, и соглашение об именовании к ним тоже не применяется:

```csharp
var name = ctx.From<IOrder>()
    .Where(x => x.Total > minTotal)
    .Select(x => new { x.Id, x.Total })
    .ToTempTable();

var orders = ctx.From(name)
    .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") })
    .ToList();
```

Имя цели используется дословно (применяется только кавычение идентификатора); соглашение об именовании
**не** применяется, потому что сырое имя — не сущность. Захваченные в запросе значения становятся
параметрами и переносятся вместе с телом запроса, как в обычном запросе.

Временная таблица живёт в пределах сессии. Создавайте и читайте её на **одном контексте** (контекст
держит одно соединение открытым), а читайте обратно через `From("name")` аксессорами
[`TableAlias`](xref:NextORM.Core.TableAlias). Пул соединений, переназначающий backend на транзакцию,
требует одной транзакции вокруг обоих шагов — см. [Привязка к сессии и пулеры соединений](#привязка-к-сессии-и-пулеры-соединений):

```csharp
var orders = ctx.From("recent_orders")
    .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") })
    .ToList();
```

`ToTempTableSql(name)` рендерит SQL без открытия соединения; `ToTempTable` / `ToTempTableAsync` выполняют его.

## Привязка к сессии и пулеры соединений

Временная таблица живёт в **сессии** базы (на backend-сервере), а не просто на клиентском соединении. Если PostgreSQL стоит за пулером, который переназначает backend на каждую транзакцию, — PgBouncer в режиме `transaction`, или любой балансировщик уровня соединения — две команды на одном клиентском соединении могут уйти на разные backend'ы, и читающая падает с `relation "recent_orders" does not exist`.

Одного открытого клиентского соединения поэтому недостаточно. Оберните `CREATE TEMPORARY TABLE ... AS SELECT` и **каждый** запрос, читающий эту таблицу, в одну явную транзакцию: PgBouncer закрепляет сервер на весь `BEGIN … COMMIT`, и оба шага идут на одном backend:

```csharp
var transactions = (ITransactionManager)ctx;
await using var tx = await transactions.BeginTransactionAsync();

ctx.From<IOrder>()
    .Where(x => x.Total > minTotal)
    .Select(x => new { x.Id, x.Total })
    .ToTempTable("recent_orders");

var orders = ctx.From("recent_orders")
    .Select(t => new { Id = t.GetInt32("id") })
    .ToList();

await tx.CommitAsync();
```

* Оставляйте `OnCommit` по умолчанию — `PreserveRows`: `Drop` и `DeleteRows` очищают или удаляют таблицу на коммите.
* Таблица **не** переживает транзакцию. После `COMMIT` пулер может отдать следующую транзакцию другому backend'у, где таблицы нет; не рассчитывайте на неё между транзакциями.
* В режиме PgBouncer `session` backend закреплён на всю клиентскую сессию, и транзакция не нужна; в режиме `statement` транзакции недостаточно.
* Prepared statements — отдельная проблема режима `transaction`: подготовленный на одном backend'е запрос нельзя выполнить на другом, поэтому может понадобиться `max_prepared_statements=0` на PgBouncer или отключённые prepared statements на стороне провайдера.

## Создание и чтение за один батч

Материализацию и читающий её запрос можно отправить одним батчем — один round trip на одной серверной сессии, поэтому session-scoped таблица видна за пулером уровня соединения. Используйте общий [`BatchBuilder`](xref:NextORM.Core.BatchBuilder); он описан в [Выполнение утверждений одним батчем](28-sql-batch.md).

```csharp
var orders = ctx.Batch()
    .CreateTempTableAs("recent_orders", ctx.From<IOrder>()
        .Where(x => x.Total > minTotal)
        .Select(x => new { x.Id, x.Total }))
    .Query(ctx.From("recent_orders")
        .Select(t => new { Id = t.GetInt32("id") }))
    .ToList();
```

Материализация и чтение уходят в базу одним батчем; `.ToSql()` возвращает `;`-склеенный текст, который отправляется:

```sql
create temporary table recent_orders as select id, total from orders
 where (total > @b0_minTotal); select id from recent_orders
```

Захваченный `minTotal` становится параметром, а его имя получает префикс на утверждение (`b0_minTotal`), поэтому оно не может совпасть с одноимённой переменной в другом утверждении батча.

## Создание постоянной таблицы

`ToTable(name)` использует тот же запрос, но материализует в постоянную таблицу (на SQL Server рендерит `SELECT ... INTO <name>`):

```csharp
ctx.From<IOrder>().Select(x => new { x.Id, x.Total }).ToTable("order_archive");
```

В отличие от временной, постоянная таблица переживает сессию (и не привязана к одному соединению).

## Опции

Передайте [`CreateTableAsOptions`](xref:NextORM.Core.CreateTableAsOptions), чтобы управлять утверждением:

```csharp
ctx.From<IOrder>()
    .Select(x => new { x.Id })
    .ToTempTable("recent_orders", new CreateTableAsOptions
    {
        IfNotExists = true,
        Columns = ["order_id"],
        OnCommit = TempTableOnCommit.Drop,
        WithData = false,
    });
```

| Опция | Действие | Провайдеры |
|---|---|---|
| `IfNotExists` | Добавляет `IF NOT EXISTS`, поэтому повтор команды становится безоперационным. | PostgreSQL, SQLite, MySQL, MariaDB, ClickHouse (`SELECT ... INTO` в SQL Server это отклоняет) |
| `Columns` | Задаёт имена колонок цели вместо вывода их из запроса. | PostgreSQL, MySQL, MariaDB (SQLite выводит все колонки; SQL Server берёт имена из select-list; ClickHouse требует пары `name type`) |
| `OnCommit` | `ON COMMIT { PRESERVE ROWS \| DELETE ROWS \| DROP }` временной таблицы; допустимо только с `ToTempTable`. | PostgreSQL |
| `WithData` | `false` рендерит `WITH NO DATA` (таблица создаётся пустой). | PostgreSQL |

Опция, которую провайдер не умеет выразить, отклоняется `NotSupportedException` при построении SQL и
никогда не игнорируется молча.

## Поддержка провайдерами

| Провайдер | `CREATE TABLE ... AS SELECT` |
|---|---|
| PostgreSQL | да — `CREATE [TEMPORARY] TABLE ... AS SELECT` (`TEMPORARY`/`TEMP`, список колонок, `ON COMMIT`, `WITH [NO] DATA`) |
| SQLite | да — `CREATE [TEMPORARY] TABLE ... AS SELECT` (`TEMP`/`TEMPORARY`; без списка колонок, `ON COMMIT` и `WITH NO DATA`) |
| MySQL / MariaDB | да — `CREATE [TEMPORARY] TABLE ... AS SELECT` (`TEMPORARY`, список колонок) |
| SQL Server | `ToTable` да — `SELECT ... INTO <table>`; `ToTempTable` нет (таблица уровня сессии — это `ToTable("#name")`; без `IF NOT EXISTS`, без списка колонок) |
| ClickHouse | `ToTable` да — `CREATE TABLE ... ENGINE = MergeTree ORDER BY tuple() AS SELECT`; `ToTempTable` нет (временная таблица требует явных колонок и не допускает `AS SELECT`) |
| In-memory | нет (контекст только для чтения) |

На провайдере без запрошенной формы и на in-memory-контексте терминал бросает `NotSupportedException`.

## Примечания и ограничения фазы 1

* Терминал возвращает `void` (не счётчик строк): `CREATE TABLE AS SELECT` не даёт осмысленного affected-rows.
* На SQL Server таблица уровня сессии создаётся именем с префиксом `#` и через `ToTable` (в T-SQL нет `CREATE TEMPORARY TABLE ... AS SELECT`); чтение — `From("#name")`:
  ```csharp
  ctx.From<IOrder>().Select(x => new { x.Id, x.Total }).ToTable("#recent_orders");
  var rows = ctx.From("#recent_orders").Select(t => t.GetInt32("id")).ToList();
  ```
* ClickHouse материализует в `MergeTree` с пустым ключом сортировки (`ENGINE = MergeTree ORDER BY tuple()`, движок обязателен); произвольный движок/сортировка в этот API не входят.
* Поддерживается только чтение через сырой `From("t")`; маппинг созданной таблицы на сущность — вне области.
* Удаление таблицы (`DROP TABLE`) и индексы на ней в этот API не входят; используйте `ExecuteNonQuery`
  соединения (см. [Сырой SQL](14-raw-sql.md)).

## См. также

- [Обобщённые табличные выражения](09-cte.md)
- [Изменение данных (INSERT)](19-insert-statement.md)
- [Выполнение утверждений одним батчем](28-sql-batch.md)
- [Транзакции](25-transactions.md)
- [Обзор провайдеров](../providers/overview.md)
