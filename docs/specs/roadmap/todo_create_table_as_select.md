# TODO: `CREATE [TEMPORARY] TABLE ... AS SELECT` (CTAS) и временные таблицы

## Предмет

- **Backlog:** gap-analysis §4 #21 (новый провайдерный пробел). Ранее материализация во временную
  таблицу была явно выведена за область в [todo_insert.md](todo_insert.md) («вне области: …
  временные таблицы») — этот файл пересматривает то решение.
- **Целевые провайдеры:** PostgreSQL (origin), SQLite, MySQL, MariaDB (форма
  `CREATE [TEMPORARY] TABLE ... AS SELECT`); SQL Server — фаза 2 (другая форма, `SELECT ... INTO #t`);
  ClickHouse — gated (`CREATE TEMPORARY TABLE ... AS SELECT` не выразим).
- **Критерий приёмки:** пользователь строит `SELECT` обычным билдером и материализует его в
  (временную) таблицу одним терминалом; параметры тела `SELECT` корректно переносятся в общий
  аккумулятор; созданную таблицу видно через `From("t")` на том же соединении; провайдер без формы
  бросает `NotSupportedException` с понятным сообщением.

## Зачем (сценарий)

`WITH ... AS MATERIALIZED` (уже поддержано через `CteQuery`/`CteDefinition`) переиспользует результат
только **внутри одного** statement. CTAS нужен для **межзапросового** переиспользования: один раз
материализовать тяжёлый/дорогой `SELECT` в session-scoped таблицу, навесить на неё индексы (SQL-ом
через `ExecuteNonQuery`, отдельная тема) и обращаться к ней из последующих запросов через `From("t")`.
Это не дублирует CTE-путь и не конкурирует с ним.

## Провайдерная матрица (шаг 1: провайдер × форма)

Матрица заполнена по документации самих провайдеров, а не по коду nextorm.

| Провайдер | Нативная форма | Temp + `AS SELECT`? | Источник |
|---|---|---|---|
| PostgreSQL | `CREATE [ [GLOBAL\|LOCAL] {TEMPORARY\|TEMP} \| UNLOGGED] TABLE [IF NOT EXISTS] t [(cols)] [USING ...] [WITH (...)] [ON COMMIT {PRESERVE ROWS\|DELETE ROWS\|DROP}] [TABLESPACE ...] AS <query> [WITH [NO] DATA]` | да | postgresql.org/docs/current/sql-createtableas.html |
| SQLite | `CREATE [TEMP\|TEMPORARY] TABLE [IF NOT EXISTS] [schema.]t AS <select-stmt>` (при `AS SELECT` список колонок не задаётся) | да | sqlite.org/lang_createtable.html §2.1 |
| MySQL | `CREATE [TEMPORARY] TABLE [IF NOT EXISTS] t [(cols)] [IGNORE\|REPLACE] [AS] <select>` | да | MySQL 8.4 Reference Manual: `CREATE TABLE ... SELECT` (dev.mysql.com вернул 403 краулеру; форма совпадает с MariaDB) |
| MariaDB | как MySQL + `CREATE OR REPLACE [TEMPORARY] TABLE ... [AS] SELECT` | да | mariadb.com/kb/en/create-table (`CREATE TABLE ... SELECT`) |
| SQL Server | нет `CREATE TABLE ... AS SELECT` на обычном движке (CTAS — Azure Synapse). Аналог: `SELECT <list> INTO #t [ON filegroup] FROM ...`; локальная temp — имя с `#`, глобальная — `##` | **другая форма** (не префикс, а `INTO` внутри `SELECT`) | learn: `SELECT - INTO clause (Transact-SQL)` |
| ClickHouse | persistent: `CREATE TABLE [IF NOT EXISTS] t [(cols)] ENGINE = Memory AS <select>`; temporary: `CREATE [OR REPLACE] TEMPORARY TABLE t (columns) [ENGINE = ...]` — **без** `AS SELECT` | нет (для temp) | clickhouse.com docs: `CREATE TABLE` (`from-select-query`) + `CREATE TEMPORARY TABLE` |
| In-memory | — нет SQL | — | CLR-контекст без БД; роль `IMutationExecutor` им не реализуется (как в INSERT) |

## Единообразие провайдеров (решение шага 1)

- Форма `CREATE ... TABLE ... AS SELECT` **одна** у PostgreSQL, SQLite, MySQL, MariaDB (и у
  persistent-CTAS ClickHouse), поэтому заводим **кросс-провайдерную** точку входа и пару
  `SupportsCreateTableAsSelect` / `MakeCreateTableAsSelect` в `ISqlDialect`/`SqlDialectBase`
  (default `false`/throw), а не поверхность уровня `PostgresFunctions`. Написание `TEMPORARY`/`TEMP`,
  список колонок, `IF NOT EXISTS`, `ON COMMIT`, `WITH [NO] DATA` различаются — их параметризует
  `Make*`.
- **SQL Server** — отдельная конструкция (нужен `INTO` после select-list, а не префикс). Прятать это
  за тем же `MakeCreateTableAsSelect` нельзя: тело `SELECT` перестраивается. Фаза 2 — собственный
  `Make` (`SELECT ... INTO #t ...`) или gated `NotSupportedException`; в фазе 1 — `false` с явной
  причиной.
- **ClickHouse**: `CREATE TEMPORARY TABLE ... AS SELECT` не выразим (temp-таблицы только с явными
  колонками). Поэтому `SupportsCreateTableAsSelect = false` для временного варианта. При этом
  persistent `ENGINE = Memory AS SELECT` выразим — отдельный вопрос, вне области этой фичи.
- **In-memory**: роль `IMutationExecutor` не реализуется, входная точка бросает `NotSupportedException`
  (тот же паттерн, что `Insert`).

## Ближайший C#-аналог и tier

Скалярной аналогии нет — это statement, а не функция, поэтому лестница tier'ов (a/b/c) из скилла
применяется по духу: **tier b — новый публичный метод + хук диалекта**, без `[SqlFunction]`.
Готовые аналоги в репозитории: ось `InsertCommand`/`SqlMutationBuilder`/`IMutationExecutor` (исполнение
DDL/DML) и `CteDefinition` (композиция вокруг `QueryCommand`). Никакого нового рендерера SELECT не
пишем — тело рендерит `SqlBuilder.MakeSelect` в тот же `List<Parameter>`.

## План диалекта

`ISqlDialect` (+ `SqlDialectBase`, default `false`):

```csharp
bool SupportsCreateTableAsSelect { get; }
bool SupportsCreateTableAsSelectColumnList { get; }   // PG, MySQL, MariaDB; SQLite — нет
bool SupportsCreateTableAsSelectOnCommit { get; }     // только PostgreSQL
string MakeCreateTableAsSelect(in CreateTableAsClause clause, string selectSql);
```

`CreateTableAsClause` — внутренняя структура: имя (сырое), `Temporary`, `IfNotExists`, `Columns`,
`OnCommit`, `WithData`. Значения флагов:

| Провайдер | `SupportsCreateTableAsSelect` | column list | `ON COMMIT` | `WITH [NO] DATA` |
|---|---|---|---|---|
| PostgreSQL | `true` | `true` | `true` | `true` |
| SQLite | `true` | `false` | `false` | `false` |
| MySQL | `true` | `true` | `false` | `false` |
| MariaDB | `true` | `true` | `false` | `false` |
| SQL Server | `false` (фаза 2) | — | — | — |
| ClickHouse | `false` (temp-форма не выразима) | — | — | — |

## Модель и рендер

- `SqlStatementType` += `CreateTableAsSelect`; `MutationCommand` += `CreateTableAsCommand` с
  `TargetName`, опциями и `QueryCommand Source` (недженерик-база `QueryCommand`).
- `SqlMutationBuilder.MakeCreateTableAsSelect(...)`: `MakeSelect(command.Source)` для тела,
  **тот же** `Params`, затем обёртка из `dialect.MakeCreateTableAsSelect(...)`. Имя цели — сырое:
  `QuoteIdentifier` по `QuoteIdentifiers`, `INamingConvention` не применяется (это не сущность).
- `IMutationExecutor.Execute` → `QueryExecutor.ExecuteNonQuery(sql, parameters)`; транзакция
  привязывается так же, как у SELECT (после [todo_transactions.md](todo_transactions.md)).

## Публичный API (черновик)

```csharp
public enum TempTableOnCommit { PreserveRows, DeleteRows, Drop }

public sealed record TempTableOptions
{
    public bool Temporary { get; init; } = true;
    public bool IfNotExists { get; init; }
    public bool WithData { get; init; } = true;
    public TempTableOnCommit OnCommit { get; init; } = TempTableOnCommit.PreserveRows;
    public IReadOnlyList<string>? Columns { get; init; }
}

public static class TempTableExtensions
{
    public static void ToTempTable<TResult>(this EntityBuilder<TResult> builder, string name, TempTableOptions? options = null);
    public static Task ToTempTableAsync<TResult>(this EntityBuilder<TResult> builder, string name, TempTableOptions? options = null, CancellationToken cancellationToken = default);
    public static void ToTable<TResult>(this EntityBuilder<TResult> builder, string name, TempTableOptions? options = null);
    public static Task ToTableAsync<TResult>(this EntityBuilder<TResult> builder, string name, TempTableOptions? options = null, CancellationToken cancellationToken = default);
}
```

Терминал дергает `builder.ToCommand()`, строит `CreateTableAsCommand` и исполняет через
`IMutationExecutor` (бросая `NotSupportedException`, если роль не реализована). Возврат — `void`, а не
`int`: `CREATE TABLE AS SELECT` не даёт осмысленного affected-rows (`ExecuteNonQuery` вернёт `-1`).

## In-memory

В фазе 1 — `NotSupportedException` из входной точки (роль `IMutationExecutor` не реализуется), как у
`Insert`. Материализация в синтетическую store — отдельный вопрос, вне области.

## План тестов

- Core (`tests/nextorm.core.tests/`): модель `CreateTableAsCommand`; in-memory бросает
  `NotSupportedException`; SQL Server/ClickHouse бросают.
- SQL-gen (`tests/nextorm.{postgres,sqlite,mysql,mariadb}.tests/SqlGenerationTests.cs`): префикс
  `create [temporary] table ... as`, quoting имени, `if not exists`, список колонок (PG/MySQL),
  `on commit` (PG), `with no data` (PG); **перенос параметров** из `WHERE` тела в общий список;
  тело с CTE/union.
- Диалекты (`*DialectTests.cs`): значения `SupportsCreateTableAsSelect` (+ column list/on commit) и
  `MakeCreateTableAsSelect` у каждого провайдера.
- Интеграция (`tests/nextorm.integration.tests/CommonTestSuite.CreateTableAs.cs`): создать temp из
  запроса с параметром → прочитать через `From("t")` на том же контексте; повторный запуск; PG —
  `ON COMMIT DROP`/`WITH NO DATA`. ClickHouse/SQL Server — `NotSupportedException`.
- Покрытие: база на момент планирования — 85.4% line / 74.5% branch (gap-analysis). Новые файлы в
  `nextorm.core` учитываются `coverage.settings.xml`; покрытие не должно упасть ниже базы.

## Документация

- Новая guide-страница `docs/guide/20-create-table-as.md` (+ `docs/ru/guide/20-create-table-as.md` +
  записи в `toc.yml` с обеих сторон): семантика, отличия от `WITH`/materialized CTE, пример,
  матрица провайдеров, время жизни и `ON COMMIT`.
- `docs/providers/overview.md` (+RU), `docs/advanced/limitations.md` (+RU),
  `docs/advanced/api-reference.md` (+RU), `docs/specs/comparison/linq2db-comparison.md` (+RU).
- При публичном API: `docs/specs/design/API-NAMING-REVIEW.md`.
- `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4/§5 и статус этого файла (при релизе —
  удалить, перенеся выводы в доки).

## Открытые вопросы

1. Форма API: терминал `EntityBuilder<T>.ToTempTable(name)` (рекомендуется) vs
   `ctx.CreateTempTable(name).As(query)` vs `ctx.CreateTableAs(name, query)`; окончательно —
   через API-NAMING-REVIEW.
2. Имя терминала: `ToTempTable`/`ToTable` vs `CreateTableAs`/`CreateTempTableAs` (linq2db-стиль).
3. Возврат `void` (рекомендуется) vs `int`; DDL не даёт affected-rows.
4. Нужен ли в этой же фиче `DropTempTable`/`DropTable` терминал или отдельным item'ом.
5. Судьба SQL Server: единый `Make*` с select-list-aware `INTO` vs отдельная команда (фаза 2).
6. ClickHouse: не заводить ли отдельно persistent CTAS `ENGINE = Memory AS SELECT`.
7. Материализованная таблица под `IEntityMetadata` (маппинг на сущность) или только raw `From("t")`
   в фазе 1 (рекомендуется raw).
8. Нужны ли индексы на созданной temp-таблице в рамках фичи (только через `ExecuteNonQuery`?).

## Файлы к изменению

- Новое: `src/nextorm.core/Query/Mutations/CreateTableAsCommand.cs` (или в существующем
  `MutationCommand.cs`), `src/nextorm.core/Builders/TempTableExtensions.cs`.
- Правки: `src/nextorm.core/Query/Mutations/MutationCommand.cs` (`SqlStatementType`),
  `src/nextorm.core/DataContext/SqlMutationBuilder.cs`, `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`,
  `SqlDialectBase.cs`, провайдерные диалекты (`nextorm.{postgres,sqlite,mysql}/**`),
  `src/nextorm.core/DataContext/DataContext.cs` (`IMutationExecutor`), `Builders/EntityBuilder.cs`
  (`ToCommand()` доступ), `DataContextExtensions.cs`.
- Тесты: `tests/nextorm.{core,postgres,sqlite,mysql,mariadb}.tests/`, `CommonTestSuite.CreateTableAs.cs`,
  `*SpecificTests.cs`.
- Документация: перечислено выше.
