# TODO: DDL/DML + читающий запрос в одном SQL-батче (`CREATE TEMP TABLE ... AS SELECT` → `SELECT`)

> Рабочий план (RFC). Источник — обсуждение необходимости отправить **создание таблицы и запрос с её
> использованием в рамках одного SQL-батча**. Мотивация: temp-таблица session-scoped (backend), а
> connection-level пулер (pgbouncer в `transaction` mode) не гарантирует, что две отдельные команды
> уйдут на один backend. Связано с `todo_output_into.md` (материализация вывода в таблицу) и
> `todo_stored_procedures.md` (произвольные параметризованные команды). Публичный API — новый; при
> добавлении — регистр `docs/specs/design/API-NAMING-REVIEW.md`.

## 1. Пункт и цель

- **Проблема:** `ToTempTable`/`ToTable` (CTAS) и последующий `From("<name>")` — это **две отдельные**
  `DbCommand`. Временная таблица живёт в сессии backend'а; под pgbouncer в `transaction` режиме
  (или иным connection-level балансировщиком) команды могут уйти на разные backend'ы, и вторая падает
  с `relation "<name>" does not exist`. Даже без пулера это два round-trip.
- **Цель:** дать способ отправить «создание таблицы + запрос(ы) с её использованием» **одним
  SQL-батчем** (один round-trip, один backend) с типизированным результатом читающей команды.
- **Критерий приёмки:** публичный терминал собирает `CREATE [TEMPORARY] TABLE <name> AS <select>` и
  следующий за ним построенный запрос в один батч; на PostgreSQL он идёт через `NpgsqlBatch`
  (implicit transaction → один backend под transaction-pooling), параметры обеих команд не
  конфликтуют; провайдер без батч-формы получает явный `NotSupportedException`, а не молчаливую
  деградацию в два стейтмента.

## 2. Воспроизведение / контекст

Текущий кейс cross-statement reuse (см. `docs/guide/22-create-table-as.md:25`):

```csharp
ctx.From<IOrder>()
   .Where(x => x.Total > minTotal)
   .Select(x => new { x.Id, x.Total })
   .ToTempTable("recent_orders");                     // команда 1

var rows = ctx.From("recent_orders")                  // команда 2
   .Select(t => new { Id = t.GetInt32("id"), Total = t.GetDecimal("total") })
   .ToList();
```

Соединение одно и то же (`DbConnectionManager` держит открытый `_conn`), но под pgbouncer
`transaction` mode оно не закрепляет backend между транзакциями: `CREATE TEMP TABLE` и `SELECT`
могут выполниться на разных серверах. Нужен один протокольный обмен / одна транзакция.

Замечание: intra-statement reuse (CTE) этой проблемы не имеет, но CTE ≠ материализованная таблица и
не покрывает сценарий с повторным чтением/индексами в том же батче.

## 3. Текущее состояние (проверено по коду)

- Исполнитель всегда создаёт **один** `DbCommand`:
  - мутации — `QueryExecutor.CreateMutationCommand` (`src/nextorm.core/DataContext/QueryExecutor.cs:92`);
  - чтение — `ResultSetEnumerator.InitReader` / `InitReaderAsync`
    (`src/nextorm.core/DataContext/ResultSetEnumerator.cs:203`, `:227`).
- `DbBatch` / `NpgsqlBatch` / `SqlBatch` / `MySqlBatch` **нигде не используются** (по репозиторию —
  пусто).
- CTAS: команда `CreateTableAsCommand`
  (`src/nextorm.core/Query/Mutations/CreateTableAsCommand.cs`), рендер
  `SqlMutationBuilder.MakeCreateTableAsSelect` (`src/nextorm.core/DataContext/SqlMutationBuilder.cs:432`),
  терминалы `TempTableExtensions` (`src/nextorm.core/Builders/TempTableExtensions.cs:28`–`:158`).
  Терминал исполняет **только** тело CTAS, отдельной командой.
- Прецеденты склейки в один текст/батч уже есть, но **без публичного API**:
  - SQL Server identity: `sql + "; " + Dialect.MakeIdentityFunction` (`DataContext.cs:267`, `:276`);
    `SCOPE_IDENTITY()` batch-scoped, поэтому отправляется вместе с insert;
  - SQL Server `SET IDENTITY_INSERT ... ON; insert; ... OFF`
    (`src/nextorm.core/DataContext/SqlMutationBuilder.cs:67`–`:68`).
- Plan cache: prepared-команды привязаны к соединению (`ResetConnection`); батч в кэш в фазе 1 не
  кладём.
- InMemory: CTAS/мутации не поддержаны → батч тоже `NotSupportedException`.

## 4. Матрица провайдеров

Механизм «один round-trip / один backend» для связки DDL + читающий запрос. Колонка `DbBatch` — есть ли
в драйвере реализация `System.Data.Common.DbBatch` (`DbConnection.CanCreateBatch`/`CreateBatch`).
Источники — документация самих провайдеров (не код nextorm).

| Провайдер | `DbBatch` (`CanCreateBatch`) | Text-склейка `;` | Temp-таблица в батче | Рекомендуемая форма | Источник |
|---|---|---|---|---|---|
| PostgreSQL (Npgsql 10.0.3) | да — `NpgsqlBatch`/`NpgsqlBatchCommand` | нет с параметрами (extended protocol) | да | `NpgsqlBatch` (без явной транзакции оборачивается в implicit transaction → один backend) | npgsql.org: *Batching* / *Performance* |
| SQL Server (Microsoft.Data.SqlClient 6.1.7) | да — `SqlBatch`/`SqlBatchCommand` | да (разделитель `;`, параметры общие) | да (`#temp`), но CTAS пока не выведен (gap #21: нужен `SELECT ... INTO #t`) | `SqlBatch` (generic batch); CTAS-батч — после #21 | MS Learn: `SqlBatch`, *Commands Generating Multiple-Rowset Results* |
| MySQL (MySqlConnector 2.6.2) | да — `MySqlBatch` | разрешена всегда (`AllowBatch` — no-op: «batch statements are always allowed») | да | `MySqlBatch` | MySqlConnector docs: `MySqlBatch`, `CanCreateBatch`, connection-options |
| MariaDB (MySqlConnector 2.6.2) | да — `MySqlBatch` | так же | да | `MySqlBatch` (MariaDB 10.2+ шлёт одним батчем) | MySqlConnector docs |
| SQLite (Microsoft.Data.Sqlite 10.0.12) | **нет** (`CanCreateBatch=false`; batch-типов в сборке нет) | да — statement batching через `;` в одном `DbCommand` | да | один `DbCommand` с `;`-текстом | MS Learn: *Batching* (dotnet/standard/data/sqlite) |
| ClickHouse (ClickHouse.Driver 1.4.0) | **нет** (публичного batch-типа нет) | не подтверждено | нет временной `AS SELECT` (gap #21) | — (gate-off до проверки) | разбор сборки драйвера + доки ClickHouse (уточнить multi-statement) |
| InMemory | — | — | — | `NotSupportedException` (read-only context) | код nextorm |

**Единообразие (фаза 1):** примитив батча реализуется через `DbBatch`, где `CanCreateBatch == true`
(PostgreSQL, MySQL/MariaDB, SQL Server), и через `;`-склейку в один `DbCommand` там, где `DbBatch`
нет, но мультистейтмент поддержан (SQLite). CTAS-батч доступен на PostgreSQL/MySQL/MariaDB/SQLite
(SQL Server и ClickHouse CTAS не умеют — гейт по `SupportsCreateTableAsSelect`). ClickHouse/InMemory —
gate-off `SupportsBatch=false`. Ключевой кейс (pgbouncer) закрывает PostgreSQL через `NpgsqlBatch`.

## 5. Дизайн

> Это не scalar-function item: шаги 1–4 скилла (translator/`CommonFunctions`) не применяются. Ближайший
> C# аналог — ADO.NET `DbBatch` (`DbConnection.CreateBatch`) поверх `DbBatchCommand`; реализация — новый
> executor-role + диалектные гейты, а не translator.

### 5.1 Примитив

1. **Выбор механизма.** `conn.CanCreateBatch` → `conn.CreateBatch()`, добавляем по
   `DbBatchCommand` на стейтмент (`CommandText` + `Parameters`). Иначе — склейка `;` в один
   `DbCommand`; параметры при этом **перенумеровываются** во избежание коллизий имён между шагами.
2. **Выполнение и result-set.** `batch.ExecuteReader()` → `NextResult()` до result-set читающей
   команды (DDL-result set не даёт). Асинхронно — `ExecuteReaderAsync`. Материализация — через
   существующий `RowMapperFactory` + `SelectExpression[]`.
3. **Типизация.** `TResult` берётся с последней (или явно помеченной как результато-несущей) команды.
4. **Параметры.** `_createParam`/`IParameterProvider` создаёт `DbParameter` для данного соединения →
   кладём в `Parameters` соответствующего `DbBatchCommand` (или в общий `DbCommand` при `;`-склейке).
5. **Plan cache.** Батч не кэшируем (как мутации) — план собирается на каждый вызов.
6. **Транзакция.** Явный `BEGIN/COMMIT` вокруг батча **не добавляем** в фазе 1: `NpgsqlBatch` сам
   оборачивает в implicit transaction; при необходимости позже — опция.

### 5.2 Публичный API

**Вариант A (рекомендуемый, узкий) — continuation после CTAS:**

```csharp
public static TempTableBatch<TResult> ToTempTableThen<TResult>(
    this QueryCommand source,
    string name,
    QueryCommand<TResult> query,
    CreateTableAsOptions? options = null);

public sealed class TempTableBatch<TResult>
{
    public IReadOnlyList<TResult> ToList();
    public Task<IReadOnlyList<TResult>> ToListAsync(CancellationToken cancellationToken = default);
    public IAsyncEnumerable<TResult> AsAsyncEnumerable(CancellationToken cancellationToken = default);
    public string ToSql();
}
```

Плюс перегрузки на `EntityBuilder` и `ToTableThen` (persistent). Закрывает исходный кейс и минимально
расширяет поверхность.

**Вариант B (общий, фаза 2) — произвольный батч:**

```csharp
public static BatchBuilder Batch(this IDataContext context);
public sealed class BatchBuilder
{
    public BatchBuilder Add(QueryCommand command);                     // без результата
    public BatchBuilder CreateTableAs(string name, QueryCommand source, CreateTableAsOptions? options = null);
    public BatchQuery<TResult> Query<TResult>(QueryCommand<TResult> query);
}
```

Рекомендация: фаза 1 — вариант A; вариант B — отдельной фазой, когда появится спрос на >2 команды.

### 5.3 Диалект и гейты

- `ISqlDialect.SupportsBatch` (default `false`) + `SqlDialectBase`; override `true` на
  PostgreSQL/MySQL/MariaDB/SQLite (SQL Server — для generic-батча).
- `ISqlDialect.BatchGuaranteesSingleSession` — обещает ли механизм один backend/сессию (PG через
  `NpgsqlBatch` implicit transaction — да; SQLite — тривиально да; MySQL/MariaDB — да; ClickHouse — нет).
- CTAS-батч дополнительно гейтится существующим `SupportsCreateTableAsSelect`; ClickHouse/InMemory
  отсекаются обоими флагами с понятным сообщением.

### 5.4 Обходной путь без нового API (уже сегодня)

Явная транзакция поверх существующего `ITransactionManager` закрывает исходный кейс: pgbouncer в
`transaction` mode закрепляет backend на время `BEGIN … COMMIT`, поэтому `ToTempTable` и чтение,
выполненные **в одной транзакции**, попадают на один сервер:

```csharp
var transactions = (ITransactionManager)ctx;
await using var tx = await transactions.BeginTransactionAsync();
ctx.From<IOrder>().Select(x => new { x.Id }).ToTempTable("recent");
var rows = ctx.From("recent").Select(t => new { Id = t.GetInt32("id") }).ToList();
await tx.CommitAsync();
```

Условия: оба шага в одной транзакции (не коммитить между CTAS и чтением); `ON COMMIT = PRESERVE ROWS`
(не `DROP`); один контекст/соединение (nextorm биндит `DbCommand.Transaction` на всех путях,
`docs/guide/16-connections-and-logging.md`). После `COMMIT` backend может смениться, поэтому таблица
не переживает транзакцию. Batch-фича нужна для одного round-trip без ручного управления транзакцией,
для >2 команд и/или когда пулер в `statement` mode (транзакция не помогает).

## 6. Этапы

1. Примитив: role `IBatchExecutor` + построение `DbBatch`/`;`-команды в `QueryExecutor`, чтение по
   `NextResult`; SQL-gen тесты на форму батча.
2. Вариант A: `ToTempTableThen` (+ `EntityBuilder`/`ToTableThen`), sync/async/`ToSql`.
3. Гейты и негативные тесты (ClickHouse/InMemory, SQL Server CTAS-батч до #21).
4. Общий `Batch()` (фаза 2) — по спросу; не блокирует фазу 1.
5. Доки EN+RU.

## 7. План тестов

- SQL-gen (`tests/nextorm.{postgres,mysql,mariadb,sqlite}.tests`): форма
  `create temporary table <n> as <select>; select ... from <n>`; для `DbBatch`-провайдеров — проверка,
  что это две команды батча, а не одна склеенная строка.
- Интеграция: PostgreSQL (главный кейс) — `CommonTestSuite.CreateTableAs.cs` читает таблицу **из того же
  батча**; опционально pgbouncer через внешний `NEXTORM_POSTGRES_CONNECTION`.
- Параметры: обе команды с captured-параметрами — отсутствие коллизий имён (особенно `;`-fallback).
- Негатив: ClickHouse/InMemory → `NotSupportedException`; SQL Server CTAS-батч → `NotSupportedException`
  (до gap #21).
- Покрытие: `nextorm.{core,sqlite,postgres,sqlserver}` в `coverage.settings.xml`; MySQL/MariaDB —
  SQL-gen вне порога (зафиксировать).
- Нет регресса `CreateTableAs_*` и identity/`IDENTITY_INSERT` путей (они тоже склеивают строку).

## 8. Открытые вопросы

1. API: узкий continuation (A) или сразу общий `Batch()` (B)?
2. Возврат при нескольких result-set — только последний типизированный, или `IReadOnlyList<object>`/по
   индексу?
3. `;`-fallback: перенумерация параметров vs запрет параметров на этом пути.
4. ClickHouse multi-statement — проверить драйвер / задокументировать gate-off.
5. SQL Server `SELECT ... INTO #t` (gap #21) — синхронизировать форму CTAS-батча.
6. Нужен ли явный `BEGIN/COMMIT` вокруг батча под pgbouncer, или полагаться на implicit transaction
   `NpgsqlBatch`?
7. Стриминг: разрешать ли `AsAsyncEnumerable` для батча (reader держит открытым весь батч) или в фазе 1
   только buffered `ToList`?
8. Взаимодействие с `Prepare`/plan cache и `StoreInCache` — батчи не кэшируем, подтвердить политику.

## 9. Файлы к изменению

- `src/nextorm.core/DataContext/Roles/` — новый `IBatchExecutor` (или методы на существующих ролях).
- `src/nextorm.core/DataContext/QueryExecutor.cs` — построение `DbBatch`/`;`-команды, reader по
  `NextResult`.
- `src/nextorm.core/DataContext/ResultSetEnumerator.cs` — инициализация reader'а на батче.
- `src/nextorm.core/DataContext/DataContext.cs` — реализация роли.
- `src/nextorm.core/Builders/TempTableExtensions.cs` — API continuation.
- `src/nextorm.core/DataContext/SqlMutationBuilder.cs` — сборка текста батча + рендер.
- `src/nextorm.core/Query/Mutations/MutationCommand.cs` — дескриптор шага батча (при необходимости).
- `src/nextorm.core/DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs` и `src/nextorm.<provider>/*Dialect.cs` —
  `SupportsBatch` / `BatchGuaranteesSingleSession`.
- Тесты: `tests/nextorm.{postgres,mysql,mariadb,sqlite,sqlserver}.tests`,
  `tests/nextorm.integration.tests/{CommonTestSuite.CreateTableAs.cs,PostgresSpecificTests.cs,SqliteSpecificTests.cs}`.
- Доки: `docs/guide/22-create-table-as.md` (+RU), `docs/advanced/limitations.md` (+RU),
  `docs/providers/overview.md` (+RU).
- Спеки: этот файл; `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 (пункт 24);
  `docs/specs/design/API-NAMING-REVIEW.md` (новый публичный API).
