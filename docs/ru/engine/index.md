# Как работает движок

> Запрос проходит путь от описания до результата: вы задаёте его форму, NextORM подготавливает запрос и формирует SQL с учётом диалекта, `QueryExecutor` запускает команду, а выбранный терминал задаёт форму результата. В этом разделе разобраны этапы и два способа повторно использовать результаты подготовки: неявный кэш планов и явный `PrepareBuffered`. Пошаговые инструкции см. в [Руководстве](../guide/03-filtering-where.md), правила кэширования — в разделе [Кэш планов и переиспользование](03-plan-cache-and-reuse.md).

**Предварительные требования:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md).

## Конвейер в целом

Терминал — это вызов, который запускает запрос и возвращает результат в выбранной форме.

Любой запрос проходит один и тот же путь — от описания до результата:

- **Фабрика построителя** на контексте задаёт форму команды. Для запроса `ctx.From<TEntity>()` возвращает [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1), а вызовы методов (`Where`, `Join`, `Select`, ...) записывают запрос в [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1). `From` начинает запрос; для других форм команд есть свои фабрики (см. [точки входа построителей](#точки-входа-построителей)).
- Терминал (`ToListAsync`, `FirstAsync`, `AnyAsync`, ...) запускает **подготовку**. Команда становится структурным ключом `QueryPlan`, который NextORM ищет в кэше планов.
- При **промахе** NextORM подготавливает запрос, переводит его в SQL-текст и параметры, а затем связывает `DbCommand` со скомпилированным преобразователем строк. После этого план сохраняется в кэше.
- Команда выполняется в базе данных. `DbDataReader` выдаёт строки результата; преобразователь формирует из них объекты, либо терминал передаёт строки потоком.

По этой карте можно определить, какой компонент отвечает за каждый этап:

| Компонент | Ответственность |
|---|---|
| [`IDataContext`](xref:NextORM.Core.IDataContext) / провайдерный `DataContext` | Соединение, метаданные, диалект, создание команд и **фабрики построителей** — `From*` для запросов, `Create*Builder` для DML, батчей и CTAS. |
| [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) | Построитель запроса с набором методов в стиле LINQ (`Where`, `Join`, `Select`, ...); создаётся фабрикой `From*`. |
| [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) | Построенный запрос — источник, проекция, фильтры, соединения, сортировка, хинты — и его терминалы. |
| `QueryPreparer` | Превращает дерево выражений в подготовленные части запроса и хэши плана с помощью обходчиков выражений. |
| `QueryPlan` / `QueryPlanStore` | Структурный ключ плана и общий для процесса неявный кэш SQL-планов. |
| `SqlBuilder` + `ISqlDialect` | Формирует SQL с учётом диалекта провайдера и список параметров. |
| `RowMapperFactory` / `RowMaterializer` | Компилирует преобразователь строки (`IDataRecord` → сущность / DTO). |
| `DbCommand` (через `DataContext.CreateCommand`) | Провайдерная команда с SQL-текстом и привязанными параметрами. |
| `QueryExecutor` | Исполняет команду и управляет временем жизни считывателя ADO.NET. |
| `DbDataReader` | Считыватель ADO.NET для результирующего набора. |
| Терминалы | Все формы результата: буферизованные коллекции, отдельные строки, агрегаты и скаляры, потоковая выдача, JSON/CSV и передача исходного считывателя или потока. |

## Точки входа построителей

Форма команды выбирается фабрикой на [`IDataContext`](xref:NextORM.Core.IDataContext); каждая фабрика возвращает соответствующий построитель. `From` — фабрика **запросов**, но не единственная:

| Точка входа | Возвращает | Строит |
|---|---|---|
| `ctx.From<T>()`, `ctx.From<T>(o => ...)` | `EntityBuilder<T>` | `SELECT` по маппингу сущности, с [опциями источника](../guide/01-query-sources.md) |
| `ctx.From("table")` | `EntityBuilder<TableAlias>` | `SELECT` по «сырому» имени таблицы или CTE |
| `ctx.FromSql(sql, parameters)` | `EntityBuilder<TableAlias>` | `SELECT` по derived-table SQL |
| `ctx.From(() => Db.MyTvf(...))` | `EntityBuilder<T>` | `SELECT` по табличной функции |
| `ctx.From(query)` / `ctx.From(cte)` / `ctx.From(tempTable)` / `ctx.From(builder)` | `EntityBuilder<TResult>` (`EntityBuilder<TableAlias>` для временной таблицы) | `SELECT` по подзапросу, CTE, ленивой временной таблице или другому билдеру |
| `ctx.CreateInsertBuilder<T>()`, `CreateBulkInsertBuilder<T>()`, `CreateUpdateBuilder<T>()`, `CreateDeleteBuilder<T>()`, `CreateMergeBuilder<T>()`, `CreateTruncateBuilder<T>()` | `InsertBuilder<T>`, `BulkInsertBuilder<T>`, `UpdateBuilder<T>`, `DeleteBuilder<T>`, `MergeBuilder<T>`, `TruncateBuilder<T>` | утверждения DML |
| `ctx.CreateBatchBuilder()` | `BatchBuilder` | несколько утверждений за один round trip |
| `builder.ToTable(name)` / `.ToTempTable(name)` / `.AsTempTable()` | команда CTAS / `TempTableSource<T>` | материализация запроса в таблицу |

`CreateQueryBuilder<T>()` — явный псевдоним `From<T>()`, возвращает тот же построитель. DML, батчи и CTAS разобраны в Руководстве: [INSERT](../guide/17-insert-statement.md), [UPDATE](../guide/19-update-statement.md), [DELETE](../guide/18-delete-statement.md), [MERGE](../guide/21-merge-statement.md), [массовая вставка](../guide/22-bulk-insert.md), [SQL-батч](../guide/25-sql-batch.md), [CREATE TABLE AS](../guide/20-create-table-as.md). Остальная часть раздела идёт по пути запроса (`From`).

## Путь выполнения команды

Диаграмма показывает путь запроса от построителя через поиск плана и генерацию SQL к исполнению и преобразованию строк:

![Путь выполнения команды: EntityBuilder → QueryCommand → QueryPlan → поиск плана в кеше → QueryExecutor → DbDataReader → RowMapperFactory → буферизованные/потоковые/скалярные/сериализующие/сырые терминалы; при промахе QueryPreparer → SqlBuilder генерируют SQL, а QueryPlanStore обслуживает поиск.](../assets/diagrams/nextorm-command-execution.svg)

## Явный `PrepareBuffered()`

Вторая диаграмма показывает явное переиспользование: запрос один раз компилируется в [`IPreparedBufferedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedBufferedQueryCommand`1), который можно выполнять повторно без обращения к неявному кэшу планов:

![Явный Prepare: EntityBuilder → QueryCommand.PrepareBuffered() → GetPreparedQueryCommand → QueryPreparer → SqlBuilder, от которого идут две ветви — RowMapperFactory и CreateCommand, обе сливаются в IPreparedBufferedQueryCommand<TResult>; затем скомпилированная команда исполняется N раз через prepared.ToList(db)/First(db) → QueryExecutor → DbDataReader → строки результата, а QueryPlanStore обходится.](../assets/diagrams/nextorm-command-execution-prepared.svg)

## В этом разделе

* [Построение запроса: fluent к SQL](01-query-pipeline.md) — путь от описания запроса через подготовку и ключ плана к генерации SQL.
* [Исполнение и строки](02-execution-and-rows.md) — выполнение команды и выбор способа получить результат.
* [Кэш планов и переиспользование: `PrepareBuffered`](03-plan-cache-and-reuse.md) — сравнение автоматического кэширования планов и явных подготовленных команд.

## См. также

* [Фильтрация (WHERE)](../guide/03-filtering-where.md)
* [Соединения](../infrastructure/01-connections.md)
* [Обзор провайдеров](../connecting-providers/overview.md)
* [Провайдер in-memory](../connecting-providers/in-memory.md)
