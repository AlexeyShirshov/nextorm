# TODO: Массовая вставка (`BulkInsert` / bulk copy)

> Рабочий план (design RFC). Источник: `comparison/linq2db-comparison.md:78` и
> `sql-capabilities-gap-analysis.md` отмечают «bulk copy» как отсутствующий; в закрытых планах DML
> (`todo_insert`/`todo_merge`, удалены после выпуска) он был явно вынесен за область. Общий каркас DML
> (метаданные, ось `MutationCommand`, `SqlMutationBuilder`, роль `IMutationExecutor`,
> параметризация) — в [guide 19](../../guide/19-insert-statement.md); зависит от
> `INSERT ... VALUES` (issue #3).
>
> **Дополнено под G5** (`comparison/linq2db-backlog-gap-analysis.md`): `linq2db#2960` (Returning IDs),
> `#5124` (Bulk Insert Ignore), `#3795` (identity insert) → разделы «Возврат сгенерированных ключей»,
> «Конфликтная политика», «Identity insert» и «Чанкинг (`MaxSqlLengthForBatch`-аналог)». Построчный
> `InsertBuilder` уже умеет `Returning*` (`Builders/InsertBuilder.cs:78-144`,
> `InsertReturningBuilder.Single/ToList`), на массовом пути этого нет.

## Пункт и цель

- Фича: массовая запись большого набора сущностей (или строк-значений) одной операцией, с
  деградацией к переносимому `INSERT ... VALUES` там, где нет нативного bulk API.
- Критерий приёмки:
  - `ctx.BulkInsertInto<TEntity>().Values(entities).BulkInsert()` записывает набор на
    SQLite/PostgreSQL/SQL Server/MySQL/MariaDB; возвращается число записанных строк; in-memory
    отклоняется `NotSupportedException` с явным сообщением;
  - на поддерживающих провайдерах используется нативный путь (`COPY BINARY`, `SqlBulkCopy`,
    `MySqlBulkCopy`, бинарный API ClickHouse), иначе — один (или разбитый на пачки) многострочный
    `VALUES`;
  - опционально `Returning*` возвращает сгенерированные ключи/проекцию **там, где это умеет диалект**
    (PostgreSQL/SQLite/MariaDB/SQL Server — через `RETURNING`/`OUTPUT` на batch-пути; MySQL/ClickHouse
    — `NotSupportedException`; см. «Возврат сгенерированных ключей»);
  - опционально `IgnoreDuplicates()` пропускает конфликтующие строки (диалектный gate; см.
    «Конфликтная политика»);
  - `KeepIdentity()` пишет явные значения identity-колонки (провайдерные формы: SQL Server
    `IDENTITY_INSERT`, PostgreSQL `OVERRIDING SYSTEM VALUE`, …; см. «Identity insert»);
  - `MaxBatchSize`/`MaxParameters`/`MaxSqlLength` (аналог `BulkCopyOptions.MaxSqlLengthForBatch`)
    включают чанкинг batch-пути; по умолчанию nextorm набор не дробит.
- Позиционирование: bulk — это **не** change tracking и не `SaveChanges`, а явная команда записи
  набора, как `BulkCopy` в linq2db. Источник задаётся вызывающим явно. **Нативный** bulk-путь
  (стратегия B) ключи не возвращает — возврат ключей это отдельная, осознанно более медленная
  batch-стратегия (см. ниже). Чанкинг (если включён) — это несколько insert’ов в рамках одного
  вызова, но **без** неявной транзакции: атомарность по-прежнему забота вызывающего.

## Почему это нужно (мотивация)

1. **Пробел в матрице.** Bulk copy — последняя крупная подсистема записи, которой нет в nextorm
   после появления `INSERT`; без неё загрузка наборов уходит в сырой ADO.NET/`SqlBulkCopy` в обход
   параметризации и логирования nextorm.
2. **Реальный сценарий.** Загрузка справочников, аналитических выгрузок и seed-данных — типовой
   случай на десятки тысяч строк, где построчный/многострочный `VALUES` упирается в лимиты
   параметров и в стоимость разбора тысяч placeholder'ов.
3. **Провайдерная неоднородность.** Нативные API радикально различаются (бинарный `COPY` в Npgsql,
   `SqlBulkCopy` в SqlClient, `MySqlBulkCopy` в MySqlConnector, бинарная вставка ClickHouse.Driver);
   это ровно тот случай, для которого существует `ISqlDialect` с capability-флагами и роли-расширения
   (`IMutationExecutor` живёт вне `IDataContext`).
4. **G5: паритет с linq2db.** `BulkCopy` в linq2db умеет возвращать сгенерированные ключи, игнорировать
   конфликты и писать identity-значения. Без этого nextorm-bulk непригоден для связки «загрузить
   набор и связать строки по полученным ключам».

## Текущее состояние и разрыв

Общий разрыв DML закрыт (см. [gap-analysis](sql-capabilities-gap-analysis.md), item 18). Специфично для bulk:

| Слой | Где | Чего не хватает |
|---|---|---|
| Билдер | `Builders/InsertBuilder.Values.cs:108-134` | `.Values(IEnumerable)` есть, но материализует набор (`:113`) и связывает каждую ячейку в `InsertValue` (`:124`); нет bulk-терминала |
| Возврат ключей | `Builders/InsertBuilder.cs:78-144` | `Returning*`/`ReturningIdentity*`/`ReturningKey*` есть **только** для построчного пути (`InsertReturningBuilder`); массового returning нет; native bulk (B) ключей не даёт |
| Команда | `Query/Mutations/MutationCommand.cs` | `InsertCommand` хранит per-row `InsertValue` (боксинг каждой ячейки); нет потокового наборного источника |
| Исполнение | `DataContext/QueryExecutor.cs:109-126` | только параметризованный `ExecuteNonQuery`; нет доступа к нативному API драйвера |
| Роль | `DataContext/Roles/IMutationExecutor.cs` | нет роли bulk-исполнителя |
| Провайдеры | `SqlServerDataContext.cs:58`, … | нет хука-фабрики bulk-исполнителя |

**Ключевой разрыв — потоковость.** Текущий путь материализует весь набор (`InsertBuilder.Values.cs:113`)
и связывает каждую ячейку в `InsertValue`, что приемлемо для десятков строк и неприемлемо для bulk.
Массовый путь должен передавать строки в драйвер **потоком**, не удерживая весь набор в промежуточной
структуре.

## Дизайн: стратегии

### Стратегия A — портируемый `INSERT ... VALUES`

Отправить переданный набор одной многострочной командой `INSERT ... VALUES` через обычный
`SqlMutationBuilder`/`IMutationExecutor`. Плюсы: работает у всех SQL-провайдеров, ноль нового
провайдерного кода. Минусы: это не «настоящий» bulk по скорости, много параметров, и на очень
больших наборах есть риск упереться в лимиты параметров/строк провайдера — разбиение на пачки
сознательно оставлено вызывающему (либо включается явными опциями чанкинга, см. G5).

**Стратегия A — единственная, которая умеет `Returning` и `Ignore`** (G5): у неё есть доступ к
`INSERT ... RETURNING`/`OUTPUT` и к `ON CONFLICT`/`INSERT IGNORE`, которых лишены нативные bulk API.
Поэтому при `Returning*`/`IgnoreDuplicates()` bulk-терминал уходит на A, даже если у провайдера есть
нативный путь B (с предупреждением/документированием, что это медленнее).

### Стратегия B — нативный bulk copy, инкапсулированный в роль

Провайдер сам выбирает API:

| Провайдер | Нативный API | Ключевые особенности |
|---|---|---|
| PostgreSQL | `NpgsqlBinaryImporter` (`BeginBinaryImport("COPY <table> (<cols>) FROM STDIN (FORMAT BINARY)")`, `Write`, `Complete`) | максимальная скорость; `RETURNING`/identity на строку недоступны |
| SQL Server | `SqlBulkCopy` | `KeepIdentity`, `SqlBulkCopyOptions`, `NotifyAfter` (прогресс) |
| MySQL/MariaDB | `MySqlBulkCopy` (MySqlConnector) | поддержку MariaDB проверить (открытый вопрос 3); `LOAD DATA LOCAL INFILE` — альтернатива с `AllowLoadLocalInfile` |
| SQLite | нет bulk API у `Microsoft.Data.Sqlite` | фактически стратегия A: один `INSERT ... VALUES` |
| ClickHouse | `ClickHouseBulkCopy` / бинарная `InsertBinaryAsync` | `VALUES` пригоден только для малых наборов ([providers/clickhouse.md](../../providers/clickhouse.md) `:198`) |

Плюсы: реальная производительность, соответствие ожиданиям (linq2db `BulkCopy`). Минусы: наибольшая
поверхность, отдельная реализация на каждый провайдер, нужен доступ к «сырому» connection (который
`QueryExecutor` намеренно скрывает), **не поддерживает returning/ignore**.

### Стратегия C — гибрид (рекомендуется)

Один публичный API; диалект сообщает о наличии нативного пути флагом, а провайдерный контекст
реализует роль-исполнитель. Где нативного пути нет — используется `VALUES` (стратегия A). Это тот же
приём, что уже применяется для `SupportsReturning`/`SupportsOutput`/`SupportsLastInsertId`
(`ISqlDialect.cs:1009-1032`) и для роли `IMutationExecutor` вне `IDataContext`.

Выбор пути: `A` — если запрошены `Returning*`/`IgnoreDuplicates()` или нативного API нет; `B` — иначе.
`KeepIdentity()` не переключает путь, но меняет форму/обёртку (см. «Identity insert»).

## Дизайн: контракт (вариант C)

### 1. Роль `IBulkInsertExecutor`

Новая роль рядом с `IMutationExecutor` (в `DataContext/Roles/`), **вне** `IDataContext`, поэтому
in-memory и провайдеры без bulk её не обязывают. Реализуется провайдерным `DataContext` и получает
доступ к соединению через существующий `IConnectionManager.GetConnection()` (`DbConnectionManager.cs:64`):

```csharp
internal interface IBulkInsertExecutor
{
    int BulkInsert(BulkInsertCommand command);
    Task<int> BulkInsertAsync(BulkInsertCommand command, CancellationToken cancellationToken);
}
```

Роль остаётся `int`-only: возврат строк/ключей (G5) **не** её ответственность — он переиспользует
существующую returning-машинерию `IMutationExecutor` (`InsertReturningBuilder`), а не нативный bulk.

Фабрика-хук в базовом контексте — по образцу `CreateParam` (`SqlServerDataContext.cs:58`):

```csharp
protected virtual IBulkInsertExecutor? CreateBulkInsertExecutor() => null;
```

Провайдер без нативного API (SQLite) возвращает `null`, и терминал уходит на fallback через
`IMutationExecutor`; in-memory роль не реализует вовсе → `NotSupportedException`.

### 2. Команда `BulkInsertCommand` (потоковая)

```csharp
internal sealed class BulkInsertCommand
{
    public string TableName { get; }                    // до naming convention/квотирования
    public bool IsTableNameAuto { get; }
    public IReadOnlyList<IPropertyMetadata> Columns { get; }
    public IEnumerable<object?[]> Rows { get; }         // или IAsyncEnumerable<object?[]>
    public bool KeepIdentity { get; }
    public BulkConflictPolicy Conflict { get; }         // None | IgnoreDuplicates
    public BulkBatchOptions? Batch { get; }             // MaxBatchSize/MaxParameters/MaxSqlLength
}
```

Провайдер читает значения **по ординалу** и не зависит от рефлексии сущности (тот же принцип, что
`InsertCommand.Columns` + per-row значения). Набор строк остаётся потоковым.

### 3. Возврат сгенерированных ключей (G5, `linq2db#2960`)

**Проблема.** Нативные bulk API ключей не возвращают: `COPY ... FROM STDIN` не поддерживает
`RETURNING`, `SqlBulkCopy`/`MySqlBulkCopy`/`ClickHouseBulkCopy` — тоже (в SQL Server вернуть строки из
`SqlBulkCopy` можно лишь косвенно: предварительно нечего `OUTPUT`-ить, а `OUTPUT INTO`/staging-таблица
— это уже другая стратегия). Вывод: **returning — это batch-стратегия A**, а не нативный путь.

**Механика (стратегия A + RETURNING/OUTPUT).** Набор режется на пачки (см. чанкинг) и каждая пачка
исполняется как обычный возвращающий `INSERT`:

| Провайдер | Форма | Возвращает |
|---|---|---|
| PostgreSQL | `INSERT INTO t (...) VALUES (...), (...) RETURNING <cols>` | все строки пачки |
| SQLite | `INSERT INTO t (...) VALUES (...), (...) RETURNING <cols>` (3.35+) | все строки пачки |
| MariaDB | `INSERT INTO t (...) VALUES (...) RETURNING <cols>` (10.5+) | все строки пачки |
| SQL Server | `INSERT INTO t (...) OUTPUT INSERTED.<cols> VALUES (...), (...)` | все строки пачки |
| MySQL | — (`LAST_INSERT_ID()` даёт только первый auto-increment) | нет → `NotSupportedException` |
| ClickHouse | — (нет `RETURNING`) | нет → `NotSupportedException` |

Переиспользуем уже существующие `SupportsReturning`/`SupportsOutput`/`MakeReturning`/`MakeOutput`
(`ISqlDialect.cs:1009-1085`) и `InsertReturningBuilder` — новых диалектных хуков для возврата не
требуется, только наборная обёртка. Для MySQL `ReturningKey`/`ReturningIdentity` на массовом пути
осознанно бросает `NotSupportedException` (в отличие от построчного `ReturningIdentity`, где
`LAST_INSERT_ID()` применим к одной строке).

**API и семантика.** Терминал становится коллекционным:

```csharp
// возврат ключей
List<int> ids = ctx.BulkInsertInto<ISimpleEntity>().Values(entities)
    .ReturningKey<int>()
    .ToList();

// произвольная проекция
var rows = ctx.BulkInsertInto<ISimpleEntity>().Values(entities)
    .Returning(x => new { x.Id, x.Name })
    .ToList();
```

- Порядок строк результата **не гарантирован**: ни `RETURNING`, ни `OUTPUT` не обязуются сохранять
  порядок входного набора (в SQL Server `OUTPUT` для multi-row `VALUES` обычно следует порядку
  вставки, но это не контракт). Ключи следует связывать с входом только по значению/паре ключей,
  либо явно сортировать. Это фиксируется в XML-doc.
- `Returning` и `Values` работают в потоковом режиме (пачка за пачкой), но результат накапливается —
  это осознанный компромисс; при очень больших наборах предпочтителен `KeepIdentity` (вызывающий сам
  генерирует ключи) или `InsertBuilder`.
- Возврат + `IgnoreDuplicates()`: игнорированные строки в результат не попадают (число строк может
  быть меньше числа входных).

### 4. Конфликтная политика (G5, `linq2db#5124`)

Bulk-вставка с пропуском конфликтующих строк — `IgnoreDuplicates()`. Формы уже частично есть как
upsert-хуки (key-upsert `MergeBuilder<T>`, [guide 19](../../guide/23-merge-statement.md#upsert-key-merge)):

| Провайдер | Форма ignore | Хуки |
|---|---|---|
| PostgreSQL | `INSERT ... ON CONFLICT DO NOTHING` (9.5+) | `SupportsOnConflict`/`MakeOnConflict` (`ISqlDialect.cs:1104/1111`) |
| SQLite | `INSERT OR IGNORE` / `ON CONFLICT DO NOTHING` (3.24+) | `SupportsOnConflict`/`MakeOnConflict`; при `OR IGNORE` — новый хук `MakeInsertOrIgnore` |
| MySQL | `INSERT IGNORE INTO ...` | новый хук (`MakeOnDuplicateKey` — это upsert, не ignore) |
| MariaDB | `INSERT IGNORE` (+ `RETURNING` 10.5+) | см. MySQL |
| SQL Server | `INSERT IGNORE` нет: `MERGE ... WHEN NOT MATCHED THEN INSERT` (без ветки matched) | `SupportsMergeDoNothing` (`:1189`) + `MakeMerge` |
| ClickHouse | нет уникальности → `IgnoreDuplicates()` — no-op (все строки вставляются) | — |

Ключевые решения:

- ignore доступен **только на стратегии A** (нативный bulk API его не умеет); запрос ignore
  переключает путь на A;
- «do nothing» переиспользует ключевой MERGE/`ON CONFLICT` key-upsert'а `MergeBuilder<T>` там, где
  он есть; для MySQL/MariaDB нужен новый `MakeInsertIgnore` (или `SupportsMergeDoNothing`-ветка);
- где форма недостижима (SQL Server без MERGE, если решим не тянуть MERGE сюда) — fail-fast
  `NotSupportedException`, а не молчаливая вставка с ошибкой уникальности.

### 5. Identity insert / `KeepIdentity` (G5, `linq2db#3795`)

`KeepIdentity()` означает: писать **явные** значения identity-колонки (иначе identity/computed
исключаются из набора — см. `Values(IEnumerable)` `InsertBuilder.Values.cs:119`).

| Провайдер | Форма | Замечание |
|---|---|---|
| SQL Server | `SET IDENTITY_INSERT <table> ON` … insert … `OFF` | требует тех же connection/scope; для нативного `SqlBulkCopy` — `SqlBulkCopyOptions.KeepIdentity` |
| PostgreSQL | `OVERRIDING SYSTEM VALUE` для `GENERATED ALWAYS AS IDENTITY`; для `serial`/`GENERATED BY DEFAULT` — без клаузы (явное значение и так принимается) | см. открытый вопрос 6 |
| SQLite | специального синтаксиса нет: явный `rowid`/`INTEGER PRIMARY KEY` вставляется как есть | `KeepIdentity` фактически no-op |
| MySQL/MariaDB | специального синтаксиса нет; при 0-значениях — `NO_AUTO_VALUE_ON_ZERO` | |
| ClickHouse | identity нет | N/A |

### 6. Чанкинг (`MaxSqlLengthForBatch`-аналог) (G5)

Стратегия A отправляет набор одной командой и упирается в лимиты параметров/строк (см. таблицу ниже).
Чтобы вызывающему не пришлось дробить самому, добавляем опции, зеркальные
`BulkCopyOptions.MaxSqlLengthForBatch` в linq2db:

```csharp
public BulkInsertBuilder<TEntity> MaxBatchSize(int rows);       // строк в одном INSERT
public BulkInsertBuilder<TEntity> MaxParameters(int count);     // параметров в одном INSERT
public BulkInsertBuilder<TEntity> MaxSqlLength(int characters); // длина SQL одной команды
```

- Дефолт — **без чанкинга** (сохраняем текущее «nextorm сам ничего не дробит»); диалект может дать
  разумный `MaxBatchSize`, если вызывающий не задал ни одной опции.
- Чанкинг применим только к A (B нативно потоковый и в чанках не нуждается); `Returning` исполняет
  пачки последовательно и конкатенирует результаты.
- Каждый чанк — отдельный `INSERT`; nextorm **не** оборачивает чанки в транзакцию (атомарность — за
  вызывающим), но документирует частичный успех при сбое в середине.

## Дизайн: публичный API

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

// (A) набор сущностей
var n = ctx.BulkInsertInto<ISimpleEntity>()
    .Values(entities)                  // IEnumerable<TEntity> / IAsyncEnumerable<TEntity>
    .BulkInsert();                     // int

// (B) async + прогресс (SQL Server NotifyAfter)
await ctx.BulkInsertInto<ISimpleEntity>()
    .Values(stream)
    .NotifyAfter(50_000, done => Console.WriteLine(done))
    .BulkInsertAsync(ct);

// (C) явные значения identity
ctx.BulkInsertInto<ISimpleEntity>().Values(entities).KeepIdentity().BulkInsert();

// (D) возврат сгенерированных ключей (стратегия A + RETURNING/OUTPUT, чанкинг по лимитам)
var ids = ctx.BulkInsertInto<ISimpleEntity>()
    .Values(entities)
    .MaxBatchSize(1_000)
    .ReturningKey<int>()
    .ToList();

// (E) пропуск конфликтующих строк
ctx.BulkInsertInto<ISimpleEntity>().Values(entities).IgnoreDuplicates().BulkInsert();
```

Черновые сигнатуры:

```csharp
public sealed class BulkInsertBuilder<TEntity>
{
    public BulkInsertBuilder<TEntity> Values(IEnumerable<TEntity> entities);
    public BulkInsertBuilder<TEntity> Values(IAsyncEnumerable<TEntity> entities);
    public BulkInsertBuilder<TEntity> Timeout(int seconds);
    public BulkInsertBuilder<TEntity> MaxBatchSize(int rows);
    public BulkInsertBuilder<TEntity> MaxParameters(int count);
    public BulkInsertBuilder<TEntity> MaxSqlLength(int characters);
    public BulkInsertBuilder<TEntity> KeepIdentity();                       // SQL Server / PostgreSQL
    public BulkInsertBuilder<TEntity> IgnoreDuplicates();                   // ON CONFLICT DO NOTHING / INSERT IGNORE
    public BulkInsertBuilder<TEntity> NotifyAfter(int rows, Action<int> onRows); // прогресс/отмена

    public int BulkInsert();
    public Task<int> BulkInsertAsync(CancellationToken cancellationToken = default);

    // возврат: переключает на batch-стратегию A
    public BulkInsertReturningBuilder<TEntity, TKey> ReturningKey<TKey>();
    public BulkInsertReturningBuilder<TEntity, TResult> Returning<TResult>(
        Expression<Func<TEntity, TResult>> projection);
}

public sealed class BulkInsertReturningBuilder<TEntity, TResult>
{
    public List<TResult> ToList();
    public Task<List<TResult>> ToListAsync(CancellationToken cancellationToken = default);
}
```

Альтернатива — терминал `BulkInsert()` на существующем `InsertBuilder<TEntity>`. Рекомендация:
отдельный `BulkInsertBuilder<TEntity>`, потому что у массового пути свой набор опций
(`Timeout`/`KeepIdentity`/`MaxBatchSize`/`IgnoreDuplicates`/`NotifyAfter`) и своя семантика, а
смешение с построчным билдером раздувает его состояние. Имена SQL-ориентированы (`BulkInsertInto`) и
совместимы с linq2db (`BulkCopy`).

## Провайдерная матрица (путь × форма)

| Провайдер | Нативный путь | Fallback `VALUES` | `SupportsBulkCopy` | Возврат ключа (batch) | `IgnoreDuplicates` | `KeepIdentity` |
|---|---|---|---|---|---|---|
| PostgreSQL | `NpgsqlBinaryImporter` (`COPY BINARY`) | один/чанки `VALUES` | `true` | `RETURNING` (A) | `ON CONFLICT DO NOTHING` | `OVERRIDING SYSTEM VALUE` |
| SQL Server | `SqlBulkCopy` | один/чанки `VALUES` | `true` | `OUTPUT INSERTED` (A) | `MERGE`/fail-fast | `SET IDENTITY_INSERT ON/OFF` |
| MySQL | `MySqlBulkCopy` | один/чанки `VALUES` | `true` | нет → `NotSupportedException` | `INSERT IGNORE` | без клаузы (`NO_AUTO_VALUE_ON_ZERO`) |
| MariaDB | наследует MySQL (`MySqlBulkCopy`; проверить версии) | один/чанки `VALUES` | `true` | `RETURNING` (10.5+) | `INSERT IGNORE` | без клаузы |
| SQLite | нет (`Microsoft.Data.Sqlite`) | один/чанки `INSERT ... VALUES` | `false` | `RETURNING` (3.35+) | `INSERT OR IGNORE` | no-op |
| ClickHouse | бинарная `InsertBinaryAsync`/`ClickHouseBulkCopy` | один `VALUES` (малые) | `true` | нет → `NotSupportedException` | no-op | N/A |
| In-memory | — (нет SQL) | — | — | — | — | роль не реализована → `NotSupportedException` |

Флаг `SupportsBulkCopy` (default `false` в `ISqlDialect`/`SqlDialectBase`, паттерн `Supports*`)
отвечает только за **нативную** ветку; fallback доступен всегда. SQLite — сознательно `false` (у
драйвера нет bulk API), но `BulkInsert` продолжает работать через стратегию A.

### Лимиты портируемого пути (`VALUES`)

Стратегия A по умолчанию отправляет набор **одной** командой `INSERT ... VALUES` и сама набор не
дробит: переносимый лимит — забота вызывающего. С заданными `MaxBatchSize`/`MaxParameters`/`MaxSqlLength`
nextorm дробит набор сам (см. «Чанкинг»).

| Провайдер | Практический лимит одного `INSERT ... VALUES` |
|---|---|
| SQL Server | 1000 строк в `VALUES`; ~2100 параметров |
| SQLite | ~500 параметров (`SQLITE_MAX_VARIABLE_NUMBER`) |
| PostgreSQL | 65535 параметров |
| MySQL / MariaDB | `max_allowed_packet` / `max_prepared_stmt_count` |
| ClickHouse | `VALUES` пригоден только для малых наборов (основной путь — бинарный) |

Если набор превышает лимит, а чанкинг не включён, вызывающий сам разбивает его на пачки (и, при
необходимости, оборачивает вызовы в свою транзакцию) — см. «Ограничения и цена».

## Ограничения и цена

- **Returning — только batch-путь.** Нативный bulk (B) ключей не возвращает; `Returning*` переключает
  на A (медленнее) и материализует результат. Там, где диалект не умеет `RETURNING`/`OUTPUT`
  (MySQL, ClickHouse), терминал бросает `NotSupportedException` — это осознанный fail-fast, а не
  молчаливая пустота. Порядок возвращённых строк не гарантирован.
- **Ignore — только batch-путь.** `IgnoreDuplicates()` недоступен на нативном bulk; где нет
  `ON CONFLICT`/`INSERT IGNORE`/`MERGE` — `NotSupportedException`.
- **Чанкинг без транзакции.** Если включён, каждый чанк — отдельный `INSERT`; при сбое в середине
  часть строк уже записана. Атомарность — на вызывающем.
- **Потоковость vs повторное использование.** Набор, скорее всего, одноразовый (нельзя перечитать
  `IAsyncEnumerable`), поэтому `Prepare()`/план-кэш на bulk не распространяются — SQL либо не
  строится вовсе (нативный путь), либо строится на пачку (стратегия A).
- **Провайдерные различия.** Типы/точность, `null`-семантика и поведение по умолчанию (identity,
  триггеры, `SqlBulkCopyOptions`/`CheckConstraints`), а также формы `KeepIdentity`/ignore отличаются;
  расхождения документируются, а не унифицируются.
- **ClickHouse.** Бинарная вставка — основной путь, `VALUES` — только малые наборы; `IgnoreDuplicates`
  — no-op (нет уникальности), returning/identity отсутствуют.
- **Публичный API расширяется**: `BulkInsertBuilder<TEntity>`, `BulkInsertReturningBuilder<TEntity,TResult>`,
  `DataContextExtensions.BulkInsertInto<T>`, флаг `ISqlDialect.SupportsBulkCopy`, роль
  `IBulkInsertExecutor`, опции конфликта/identity/чанкинга; обновить `API-NAMING-REVIEW.md` и (при
  заморозке) `PublicAPI.*`. Флаг `SupportsBulkCopy` — default-член, поэтому внешние реализации
  `ISqlDialect` не ломаются (как `SupportsReturning`/`SupportsOutput`/`SupportsLastInsertId`).
- **Аллокации.** Нативный путь не должен связывать каждую ячейку в `InsertValue` и не должен
  удерживать весь набор: строка читается из сущности непосредственно перед записью в драйвер.
  Batch-путь (returning/ignore) вынужденно связывает ячейки в параметры — это цена G5-режимов.

## Этапы внедрения

- **Фаза 1 (портируемая):** `BulkInsertBuilder<TEntity>` + `BulkInsertInto<T>()`, `BulkInsertCommand`,
  рендер одного `VALUES` через `SqlMutationBuilder`, `Timeout`, `MaxBatchSize`/`MaxParameters`/
  `MaxSqlLength`. Работает на всех SQL-провайдерах; нативные ветки ещё нет.
- **Фаза 2 (нативная):** `IBulkInsertExecutor` + `SupportsBulkCopy`; PostgreSQL `COPY BINARY`,
  SQL Server `SqlBulkCopy` (+ `KeepIdentity`, `NotifyAfter`), MySQL/MariaDB `MySqlBulkCopy`,
  ClickHouse бинарная вставка.
- **Фаза 3 (G5: returning/ignore/identity):** `ReturningKey`/`Returning` на batch-пути (PostgreSQL/
  SQLite/MariaDB/SQL Server), `IgnoreDuplicates()` (PostgreSQL/SQLite/MySQL/MariaDB; SQL Server —
  решить), `KeepIdentity()` с провайдерными формами (`IDENTITY_INSERT`, `OVERRIDING SYSTEM VALUE`),
  `MaxSqlLength`-чанкинг + `IAsyncEnumerable`, проверка MariaDB. In-memory не планируется: `INSERT` в
  in-memory — вне области (см. [guide 19](../../guide/19-insert-statement.md)).
- **Вне области:** change tracking/`SaveChanges`, upsert/`MERGE` на массовом пути (построчные формы
  готовы — [guide 19](../../guide/23-merge-statement.md#upsert-key-merge)),
  `INSERT ... SELECT`/CTAS — **реализованы** ([Материализация запроса в таблицу](../../guide/22-create-table-as.md)),
  staging-таблица ради возврата строк у нативного `SqlBulkCopy`, streaming LOB.

## План тестов

- Core (`tests/nextorm.core.tests/`): `BulkInsert` без значений бросает; identity/computed колонки
  исключаются; in-memory — `NotSupportedException`; `BulkInsertInto` на контексте без роли —
  понятная ошибка; `ReturningKey` на диалекте без `SupportsReturning`/`SupportsOutput` — fail-fast;
  `MaxBatchSize` режет набор на ожидаемое число команд.
- SQL-gen (`tests/nextorm.{sqlite,postgres,sqlserver,mysql}.tests/SqlGenerationTests.cs`): fallback
  рендерит один `VALUES` и корректную параметризацию; экранирование имён; пустой набор;
  `RETURNING`/`OUTPUT`-форма returning-пачки; `ON CONFLICT DO NOTHING`/`INSERT IGNORE`; `KeepIdentity`
  (`IDENTITY_INSERT`/`OVERRIDING SYSTEM VALUE`); чанкинг по `MaxSqlLength`.
- Диалекты (`*DialectTests.cs`): значения `SupportsBulkCopy` (SQLite — `false`, PostgreSQL/SQL
  Server/MySQL/ClickHouse — `true`).
- Интеграция (`tests/nextorm.integration.tests/`): нативный путь на PostgreSQL/SQL Server/MySQL/
  ClickHouse (большой набор, проверка числа строк и содержимого), fallback на SQLite;
  `Returning`-ключи совпадают с последующим `SELECT`; `IgnoreDuplicates` пропускает дубли;
  `KeepIdentity` (SQL Server, provider-specific); `null`-значения; отмена. Возврат/ignore — только
  там, где диалект умеет; иначе отдельный тест на `NotSupportedException`.
- Покрытие: база 84.9% line / 73.2% branch (порог 75%); новые файлы core входят в
  `coverage.settings.xml` (`nextorm.{core,sqlite,postgres,sqlserver}`); MySQL/ClickHouse-специфика
  покрытием не измеряется — указывается явно.

## Открытые вопросы

1. Отдельный `BulkInsertBuilder<TEntity>` vs терминал `BulkInsert()` на `InsertBuilder<TEntity>`
   (рекомендация — отдельный: свой набор опций и своя семантика returning/ignore).
2. Потоковый источник: обязателен `IAsyncEnumerable<TEntity>` или достаточно `IEnumerable`?
   Как быть с повторной попыткой/ретраем одноразового потока при транзиентной ошибке?
3. MariaDB и `MySqlBulkCopy`: подтвердить поддержку версий; иначе fallback на `LOAD DATA`.
4. Прогресс и отмена на нативном пути: единый делегат `NotifyAfter` поверх разных API
   (`SqlBulkCopy.NotifyAfter`, счётчик в `COPY`-цикле) vs отказ от прогресса в фазе 1.
5. **Порядок при returning.** Ни `RETURNING`, ни `OUTPUT` не гарантируют соответствие входному
   порядку. Вводить ли явный `OrderBy` по ключу в результате (дорого), документировать как «порядок
   не определён», или связывать по значениям на стороне вызывающего?
6. **`KeepIdentity` и serial vs identity.** Как отличить PostgreSQL `GENERATED ALWAYS` (нужен
   `OVERRIDING SYSTEM VALUE`) от `serial`/`GENERATED BY DEFAULT` (клауза не нужна/недопустима), чтобы
   не выдать невалидный SQL? Достаточно ли текущего `IsIdentity` или нужен признак способа генерации?
7. **Ignore на SQL Server/MySQL.** Тянуть ли сюда ключевой `MERGE` (`SupportsMergeDoNothing`) ради
   SQL Server, или честно `NotSupportedException`? Для MySQL/MariaDB — вводить `MakeInsertIgnore` или
   обобщить до `SupportsMergeDoNothing`?
8. **Комбинации.** `Returning` + `IgnoreDuplicates` + чанкинг: агрегировать ключи по всем пачкам и
   молча терять игнорированные строки — приемлемо ли, или нужна диагностика (число пропущенных)?

## Файлы к изменению

- Новое: `src/nextorm.core/Builders/BulkInsertBuilder.cs`,
  `src/nextorm.core/Builders/BulkInsertReturningBuilder.cs`,
  `src/nextorm.core/Query/Mutations/BulkInsertCommand.cs`,
  `src/nextorm.core/DataContext/Roles/IBulkInsertExecutor.cs`.
- Правки: `src/nextorm.core/DataContext/DataContext.cs` (фабрика роли, реализация fallback’а и
  returning/ignore batch-пути), `DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs`
  (`SupportsBulkCopy`, `MakeInsertOrIgnore`/`MakeInsertIgnore`), провайдерные диалекты и контексты
  (`Postgres/SqlServer/MySql/SQLite/ClickHouse`, формы `RETURNING`/`OUTPUT`, `IDENTITY_INSERT`,
  `OVERRIDING SYSTEM VALUE`), `DataContextExtensions.cs`, `DataContext/InMemoryDataContext.cs`.
- Тесты: `tests/nextorm.{core,sqlite,postgres,sqlserver,mysql,clickhouse}.tests/`,
  `tests/nextorm.integration.tests/*SpecificTests.cs` (нативные пути и формы returning/ignore/identity
  не единообразны — provider-specific).
- Документация: `docs/guide/19-insert-statement.md` (+RU), `docs/providers/*` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/comparison/linq2db-comparison.md` (+RU) и `linq2db-backlog-gap-analysis.md`,
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/design/API-NAMING-REVIEW.md`.
