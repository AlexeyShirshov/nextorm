# Как работает движок

> Обзор того, что стоит между fluent-вызовом `From<T>()...Select(...)` и полученными строками: команда запроса, конвейер её превращения в SQL, кэш планов, исполнение и материализация строк. Пошаговое использование — в [Руководстве](../guide/03-filtering-where.md); правила кэширования — в разделе [Кэш планов и переиспользование](03-plan-cache-and-reuse.md).

**Предварительные требования:** [Quickstart](../getting-started/02-quickstart.md) · [Entities and metadata](../getting-started/03-entities-and-metadata.md).

## Конвейер в целом

Любой запрос идёт по одной и той же схеме:

- **Фабрика билдера** на контексте создаёт fluent-билдер: `ctx.From<TEntity>()` возвращает [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1); fluent-операторы (`Where`, `Join`, `Select`, ...) накапливаются в [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1). `From` открывает запрос — у остальных видов утверждений свои фабрики (см. [точки входа построителей](#точки-входа-построителей)).
- Терминал (`ToListAsync`, `FirstAsync`, `AnyAsync`, ...) запускает **подготовку**: команда превращается в структурный ключ `QueryPlan` и ищется в кэше планов.
- При **промахе** запрос подготавливается, транслируется в SQL-текст с параметрами и связывается со скомпилированным маппером строк и `DbCommand`; план сохраняется обратно в кэш.
- Команда исполняется против базы данных, а `DbDataReader` превращается в строки результата маппером или передаётся потоком.

| Компонент | Ответственность |
|---|---|
| [`IDataContext`](xref:NextORM.Core.IDataContext) / провайдерный `DataContext` | Соединение, метаданные, диалект, создание команд и **фабрики построителей** — `From*` для запросов, `Create*Builder` для DML, батчей и CTAS. |
| [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) | Построитель запроса: fluent LINQ-поверхность (`Where`, `Join`, `Select`, ...), создаётся фабрикой `From*`. |
| [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) | Построенный запрос — источник, проекция, фильтры, соединения, сортировка, хинты — и его терминалы. |
| `QueryPreparer` | Превращает дерево выражений в подготовленные фрагменты клауз и хэши плана (через expression-visitors). |
| `QueryPlan` / `QueryPlanStore` | Структурный ключ плана и потоково-локальный неявный кэш планов. |
| `SqlBuilder` + `ISqlDialect` | Рендерит диалект-специфичный SQL-текст и список параметров. |
| `RowMapperFactory` / `RowMaterializer` | Компилирует маппер строк (`IDataRecord` → сущность / DTO). |
| `DbCommand` (через `DataContext.CreateCommand`) | Провайдерная команда с SQL-текстом и привязанными параметрами. |
| `QueryExecutor` | Исполняет команду и владеет ADO.NET-ридером. |
| `DbDataReader` | ADO.NET-ридер поверх результирующего набора. |
| Терминалы | Все формы результата: буферизованные коллекции, отдельные строки, агрегаты и скаляры, поток строк, JSON/CSV и «сырые» ридеры/потоки. |

## Точки входа построителей

Любое утверждение начинается с фабрики на [`IDataContext`](xref:NextORM.Core.IDataContext), которая возвращает построитель для этой формы утверждения. `From` — фабрика **запросов**, но не единственная:

| Точка входа | Возвращает | Строит |
|---|---|---|
| `ctx.From<T>()`, `ctx.From<T>(o => ...)` | `EntityBuilder<T>` | `SELECT` по маппингу сущности, с [опциями источника](../guide/01-query-sources.md) |
| `ctx.From("table")` | `EntityBuilder<TableAlias>` | `SELECT` по «сырому» имени таблицы или CTE |
| `ctx.FromSql(sql, parameters)` | `EntityBuilder<TableAlias>` | `SELECT` по derived-table SQL |
| `ctx.FromTableFunction(() => Db.MyTvf(...))` | `EntityBuilder<T>` | `SELECT` по табличной функции |
| `ctx.From(query)` / `ctx.From(cte)` / `ctx.From(tempTable)` / `ctx.From(builder)` | `EntityBuilder<TResult>` (`EntityBuilder<TableAlias>` для временной таблицы) | `SELECT` по подзапросу, CTE, ленивой временной таблице или другому билдеру |
| `ctx.CreateInsertBuilder<T>()`, `CreateBulkInsertBuilder<T>()`, `CreateUpdateBuilder<T>()`, `CreateDeleteBuilder<T>()`, `CreateMergeBuilder<T>()`, `CreateTruncateBuilder<T>()` | `InsertBuilder<T>`, `BulkInsertBuilder<T>`, `UpdateBuilder<T>`, `DeleteBuilder<T>`, `MergeBuilder<T>`, `TruncateBuilder<T>` | утверждения DML |
| `ctx.CreateBatchBuilder()` | `BatchBuilder` | несколько утверждений за один round trip |
| `builder.ToTable(name)` / `.ToTempTable(name)` / `.AsTempTable()` | команда CTAS / `TempTableSource<T>` | материализация запроса в таблицу |

`CreateQueryBuilder<T>()` — явный псевдоним `From<T>()`, возвращает тот же построитель. DML, батчи и CTAS разобраны в Руководстве: [INSERT](../guide/17-insert-statement.md), [UPDATE](../guide/19-update-statement.md), [DELETE](../guide/18-delete-statement.md), [MERGE](../guide/21-merge-statement.md), [массовая вставка](../guide/22-bulk-insert.md), [SQL-батч](../guide/25-sql-batch.md), [CREATE TABLE AS](../guide/20-create-table-as.md). Остальная часть раздела идёт по пути запроса (`From`).

## Путь выполнения команды

Диаграмма показывает полный путь — от fluent-билдера через кэш планов и генерацию SQL к исполнению и мапперу строк:

![Путь выполнения команды: EntityBuilder → QueryCommand → QueryPlan → поиск плана в кеше → QueryExecutor → DbDataReader → RowMapperFactory → буферизованные/потоковые/скалярные/сериализующие/сырые терминалы; при промахе QueryPreparer → SqlBuilder генерируют SQL, а QueryPlanStore обслуживает поиск.](../assets/diagrams/nextorm-command-execution.svg)

## Явный `Prepare()`

Вторая диаграмма показывает явный путь переиспользования, где запрос компилируется один раз в [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1) и исполняется сколько угодно раз — кэш планов не задействуется:

![Явный Prepare: EntityBuilder → QueryCommand.Prepare() → GetPreparedQueryCommand → QueryPreparer → SqlBuilder, от которого идут две ветви — RowMapperFactory и CreateCommand, обе сливаются в IPreparedQueryCommand<TResult>; затем скомпилированная команда исполняется N раз через prepared.ToList(db)/First(db) → QueryExecutor → DbDataReader → строки результата, а QueryPlanStore обходится.](../assets/diagrams/nextorm-command-execution-prepared.svg)

## В этом разделе

* [Построение запроса: fluent к SQL](01-query-pipeline.md) — команда, подготовка, ключ плана и генерация SQL.
* [Исполнение и строки](02-execution-and-rows.md) — запуск команды и формирование результата.
* [Кэш планов и переиспользование: `Prepare`](03-plan-cache-and-reuse.md) — неявный кэш и явные подготовленные команды.

## См. также

* [Фильтрация (WHERE)](../guide/03-filtering-where.md)
* [Соединения](../infrastructure/01-connections.md)
* [Обзор провайдеров](../connecting-providers/overview.md)
* [Провайдер in-memory](../connecting-providers/in-memory.md)
