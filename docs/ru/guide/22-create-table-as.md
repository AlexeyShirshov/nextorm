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

Имя цели используется дословно (применяется только кавычение идентификатора); соглашение об именовании
**не** применяется, потому что сырое имя — не сущность. Захваченные в запросе значения становятся
параметрами и переносятся вместе с телом запроса, как в обычном запросе.

Временная таблица живёт в пределах сессии. Создавайте и читайте её на **одном контексте** (контекст
держит одно соединение открытым), а читайте обратно через `From("name")` аксессорами
[`TableAlias`](xref:NextORM.Core.TableAlias):

```csharp
var orders = ctx.From("recent_orders")
    .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") })
    .ToList();
```

`ToTempTableSql(name)` рендерит SQL без открытия соединения; `ToTempTable` / `ToTempTableAsync` выполняют его.

## Создание постоянной таблицы

`ToTable(name)` использует тот же запрос, но рендерит `CREATE TABLE` без ключевого слова `TEMPORARY`:

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
| `IfNotExists` | Добавляет `IF NOT EXISTS`, поэтому повтор команды становится безоперационным. | PostgreSQL, SQLite, MySQL, MariaDB |
| `Columns` | Задаёт имена колонок цели вместо вывода их из запроса. | PostgreSQL, MySQL, MariaDB (SQLite выводит все колонки) |
| `OnCommit` | `ON COMMIT { PRESERVE ROWS \| DELETE ROWS \| DROP }` временной таблицы; допустимо только с `ToTempTable`. | PostgreSQL |
| `WithData` | `false` рендерит `WITH NO DATA` (таблица создаётся пустой). | PostgreSQL |

Опция, которую провайдер не умеет выразить, отклоняется `NotSupportedException` при построении SQL и
никогда не игнорируется молча.

## Поддержка провайдерами

| Провайдер | `CREATE TABLE ... AS SELECT` |
|---|---|
| PostgreSQL | да (`TEMPORARY`/`TEMP`, список колонок, `ON COMMIT`, `WITH [NO] DATA`) |
| SQLite | да (`TEMP`/`TEMPORARY`; без списка колонок, `ON COMMIT` и `WITH NO DATA`) |
| MySQL / MariaDB | да (`TEMPORARY`, список колонок) |
| SQL Server | нет (форма `SELECT ... INTO #t` пока не подключена) |
| ClickHouse | нет (временная таблица не допускает `AS SELECT`) |
| In-memory | нет (контекст только для чтения) |

На провайдере без формы и на in-memory-контексте терминал бросает `NotSupportedException`.

## Примечания и ограничения фазы 1

* Терминал возвращает `void` (не счётчик строк): `CREATE TABLE AS SELECT` не даёт осмысленного affected-rows.
* Поддерживается только чтение через сырой `From("t")`; маппинг созданной таблицы на сущность — вне области.
* Удаление таблицы (`DROP TABLE`) и индексы на ней в этот API не входят; используйте `ExecuteNonQuery`
  (см. [Сырой SQL](14-raw-sql.md)).

## См. также

- [Обобщённые табличные выражения](09-cte.md)
- [Изменение данных (INSERT)](19-insert-statement.md)
- [Обзор провайдеров](../providers/overview.md)
