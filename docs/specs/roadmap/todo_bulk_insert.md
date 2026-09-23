# TODO: Массовая вставка (`BulkInsert` / bulk copy)

> Рабочий план (design RFC). Источник: `comparison/linq2db-comparison.md:78` и
> `sql-capabilities-gap-analysis.md` отмечают «bulk copy» как отсутствующий; в планах DML он явно
> вынесен за область ([todo_insert.md](todo_insert.md) `:250,266`, [todo_merge.md](todo_merge.md)
> `:160,175`). Общий каркас DML (метаданные, ось `MutationCommand`, `SqlMutationBuilder`, роль
> `IMutationExecutor`, параметризация) описан в [todo_insert.md](todo_insert.md); зависит от
> `INSERT ... VALUES` (issue #3) и согласуется с [todo_transactions.md](todo_transactions.md).

## Пункт и цель

- Фича: массовая запись большого набора сущностей (или строк-значений) одной операцией, с
  деградацией к переносимому чанкованному `INSERT ... VALUES` там, где нет нативного bulk API.
- Критерий приёмки: `ctx.BulkInsertInto<TEntity>().Values(entities).BulkInsert()` записывает набор
  на SQLite/PostgreSQL/SQL Server/MySQL/MariaDB;   на поддерживающих провайдерах используется нативный
  путь (`COPY BINARY`, `SqlBulkCopy`, `MySqlBulkCopy`, бинарный API ClickHouse), иначе — чанкованный
  многострочный `VALUES`; возвращается число записанных строк; in-memory отклоняется
  `NotSupportedException` с явным сообщением.
- Позиционирование: bulk — это **не** change tracking и не `SaveChanges`, а явная команда записи
  набора, как `BulkCopy` в linq2db. Источник задаётся вызывающим явно; ключи из БД массовый путь
  не возвращает (см. «Ограничения»).

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

## Текущее состояние и разрыв

Общий разрыв DML — в [todo_insert.md](todo_insert.md#текущее-состояние-и-разрыв). Специфично для bulk:

| Слой | Где | Чего не хватает |
|---|---|---|
| Билдер | `Builders/InsertBuilder.cs:114-143` | `.Values(IEnumerable)` есть, но **всегда** делает `.ToList()`; нет `BatchSize`, нет bulk-терминала |
| Команда | `Query/Mutations/MutationCommand.cs` | `InsertCommand` хранит per-row `InsertValue` (боксинг каждой ячейки); нет потокового наборного источника |
| Рендер | `DataContext/SqlMutationBuilder.cs:24-95` | нет чанкинга под лимит параметров/строк |
| Исполнение | `DataContext/QueryExecutor.cs:109-126` | только параметризованный `ExecuteNonQuery`; нет доступа к нативному API драйвера |
| Роль | `DataContext/Roles/IMutationExecutor.cs` | нет роли bulk-исполнителя |
| Провайдеры | `SqlServerDataContext.cs:58`, ... | нет хука-фабрики bulk-исполнителя |
| Транзакции | `todo_transactions.md` | нативные bulk API требуют connection/transaction, которых в коде пока нет |

**Ключевой разрыв — потоковость.** Текущий путь материализует весь набор (`InsertBuilder.cs:121`)
и связывает каждую ячейку в `InsertValue`, что приемлемо для десятков строк и неприемлемо для bulk.
Массовый путь должен передавать строки в драйвер **потоком**, не удерживая весь набор в промежуточной
структуре.

## Дизайн: три стратегии

### Стратегия A — портируемый чанкованный `INSERT ... VALUES`

Разбить набор на батчи под лимиты параметров/строк провайдера и выполнить пачкой через обычный
`SqlMutationBuilder`/`IMutationExecutor`. Плюсы: работает у всех SQL-провайдеров, ноль нового
провайдерного кода, заодно закрывает задокументированное ограничение
([guide 19](../guide/19-insert-statement.md) `:223`, [todo_insert.md](todo_insert.md) `:248-250`).
Минусы: это не «настоящий» bulk по скорости, много параметров, каждый чанк — отдельный round-trip.

### Стратегия B — нативный bulk copy, инкапсулированный в роль

Провайдер сам выбирает API:

| Провайдер | Нативный API | Ключевые особенности |
|---|---|---|
| PostgreSQL | `NpgsqlBinaryImporter` (`BeginBinaryImport("COPY <table> (<cols>) FROM STDIN (FORMAT BINARY)")`, `Write`, `Complete`) | максимальная скорость; `RETURNING`/identity на строку недоступны |
| SQL Server | `SqlBulkCopy` | нужен `SqlConnection`/`SqlTransaction`; `BatchSize`, `NotifyAfter` (прогресс), `KeepIdentity`, `SqlBulkCopyOptions` |
| MySQL/MariaDB | `MySqlBulkCopy` (MySqlConnector) | поддержку MariaDB проверить (открытый вопрос 5); `LOAD DATA LOCAL INFILE` — альтернатива с `AllowLoadLocalInfile` |
| SQLite | нет bulk API у `Microsoft.Data.Sqlite` | фактически стратегия A: prepared `INSERT` в транзакции |
| ClickHouse | `ClickHouseBulkCopy` / бинарная `InsertBinaryAsync` | `VALUES` пригоден только для малых батчей ([providers/clickhouse.md](../providers/clickhouse.md) `:198`) |

Плюсы: реальная производительность, соответствие ожиданиям (linq2db `BulkCopy`). Минусы: наибольшая
поверхность, отдельная реализация на каждый провайдер, нужен доступ к «сырому» connection/transaction
(которые `QueryExecutor` намеренно скрывает).

### Стратегия C — гибрид (рекомендуется)

Один публичный API; диалект сообщает о наличии нативного пути флагом, а провайдерный контекст
реализует роль-исполнитель. Где нативного пути нет — используется чанкованный `VALUES` (стратегия A).
Это тот же приём, что уже применяется для `SupportsReturning`/`SupportsOutput`/`SupportsLastInsertId`
(`ISqlDialect.cs:987-1038`) и для ролей `ITransactionManager`/`IMutationExecutor` вне `IDataContext`.

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

Фабрика-хук в базовом контексте — по образцу `CreateParam` (`SqlServerDataContext.cs:58`):

```csharp
protected virtual IBulkInsertExecutor? CreateBulkInsertExecutor() => null;
```

Провайдер без нативного API (SQLite) возвращает `null`, и терминал уходит на чанкованный fallback
через `IMutationExecutor`; in-memory роль не реализует вовсе → `NotSupportedException`.

### 2. Команда `BulkInsertCommand` (потоковая)

```csharp
internal sealed class BulkInsertCommand
{
    public string TableName { get; }                    // до naming convention/квотирования
    public bool IsTableNameAuto { get; }
    public IReadOnlyList<IPropertyMetadata> Columns { get; }
    public IEnumerable<object?[]> Rows { get; }         // или IAsyncEnumerable<object?[]>
    public int BatchSize { get; }
    public bool KeepIdentity { get; }
}
```

Провайдер читает значения **по ординалу** и не зависит от рефлексии сущности (тот же принцип, что
`InsertCommand.Columns` + per-row значения). Набор строк остаётся потоковым; `BatchSize` — подсказка
для `SqlBulkCopy.BatchSize`/размера чанка fallback'а.

### 3. Чанкинг fallback'а

Лимиты: SQL Server ~1000 строк и 2100 параметров на команду, SQLite ~500/32766 переменных (зависит
от версии), PostgreSQL 65535 параметров, MySQL — `max_allowed_packet`/65535 placeholder'ов. Точные
константы — свойство диалекта (`MaxParametersPerStatement`/`MaxRowsPerInsert`, default — консервативно),
чтобы чанкер был общим. Рендер чанка переиспользует `SqlMutationBuilder.MakeInsert`; параметры —
существующий `DefaultParameterProvider` (`Query/DefaultParameterProvider.cs`).

### 4. Транзакция и атомарность

Нативные API принимают `DbTransaction`; чанкованный fallback **обязан** выполняться в транзакции,
иначе частичный сбой оставит набор наполовину записанным. До появления `ITransactionManager`
([todo_transactions.md](todo_transactions.md)) предлагается: если у контекста есть активная
транзакция — вливаться в неё; иначе открывать собственную `DbTransaction` на время операции. После
`todo_transactions.md` эта логика переезжает на общий менеджер транзакций.

### 5. Ключи и `RETURNING`

Нативные bulk API не возвращают сгенерированные ключи по строкам (PostgreSQL `COPY ... RETURNING`
не поддерживает, `SqlBulkCopy` — только через staging-таблицу с `OUTPUT`). Поэтому `BulkInsert`
возвращает только `int` (число записанных строк); терминалы `Returning*` на массовом пути бросают
`NotSupportedException`, а построчный `InsertBuilder` остаётся штатным способом получить ключ.
`KeepIdentity()` (SQL Server) позволяет записать явные значения identity-колонки.

## Дизайн: публичный API

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

// (A) набор сущностей
var n = ctx.BulkInsertInto<ISimpleEntity>()
    .Values(entities)                  // IEnumerable<TEntity> / IAsyncEnumerable<TEntity>
    .BatchSize(10_000)
    .BulkInsert();                     // int

// (B) async + прогресс (SQL Server NotifyAfter)
await ctx.BulkInsertInto<ISimpleEntity>()
    .Values(stream)
    .NotifyAfter(50_000, done => Console.WriteLine(done))
    .BulkInsertAsync(ct);

// (C) явные значения identity
ctx.BulkInsertInto<ISimpleEntity>().Values(entities).KeepIdentity().BulkInsert();
```

Черновые сигнатуры:

```csharp
public sealed class BulkInsertBuilder<TEntity>
{
    public BulkInsertBuilder<TEntity> Values(IEnumerable<TEntity> entities);
    public BulkInsertBuilder<TEntity> Values(IAsyncEnumerable<TEntity> entities);
    public BulkInsertBuilder<TEntity> BatchSize(int rows);
    public BulkInsertBuilder<TEntity> Timeout(int seconds);
    public BulkInsertBuilder<TEntity> KeepIdentity();                       // SQL Server
    public BulkInsertBuilder<TEntity> NotifyAfter(int rows, Action<int> onRows); // прогресс/отмена

    public int BulkInsert();
    public Task<int> BulkInsertAsync(CancellationToken cancellationToken = default);
}
```

Альтернатива — терминал `BulkInsert()` на существующем `InsertBuilder<TEntity>`. Рекомендация:
отдельный `BulkInsertBuilder<TEntity>`, потому что у массового пути свой набор опций
(`BatchSize`/`Timeout`/`KeepIdentity`/`NotifyAfter`) и своя семантика (нет `Returning`), а смешение
с построчным билдером раздувает его состояние. Имена SQL-ориентированы (`BulkInsertInto`) и
совместимы с linq2db (`BulkCopy`).

## Провайдерная матрица (путь × форма)

| Провайдер | Нативный путь | Fallback `VALUES` | `SupportsBulkCopy` | Возврат ключа |
|---|---|---|---|---|
| PostgreSQL | `NpgsqlBinaryImporter` (`COPY BINARY`) | чанки | `true` | нет |
| SQL Server | `SqlBulkCopy` | чанки | `true` | нет (`KeepIdentity` — запись явных значений) |
| MySQL | `MySqlBulkCopy` | чанки | `true` | нет |
| MariaDB | наследует MySQL (`MySqlBulkCopy`; проверить версии) | чанки | `true` | нет |
| SQLite | нет (`Microsoft.Data.Sqlite`) | prepared `INSERT` в транзакции = чанки | `false` | нет |
| ClickHouse | бинарная `InsertBinaryAsync`/`ClickHouseBulkCopy` | чанки (малые) | `true` | нет |
| In-memory | — (нет SQL) | — | — | роль не реализована → `NotSupportedException` |

Флаг `SupportsBulkCopy` (default `false` в `ISqlDialect`/`SqlDialectBase`, паттерн
`Supports*`) отвечает только за **нативную** ветку; fallback доступен всегда. SQLite —
сознательно `false` (у драйвера нет bulk API), но `BulkInsert` продолжает работать через чанки.

## Ограничения и цена

- **Нет возврата сгенерированных ключей** из массового пути; `RETURNING`/`OUTPUT` не применяются.
  Это осознанное решение (см. «Ключи и `RETURNING`»), а не молчаливая деградация.
- **Транзакции.** До [todo_transactions.md](todo_transactions.md) массовый путь сам открывает
  транзакцию для атомарности; нативный путь требует `DbTransaction` от драйвера.
- **Потоковость vs повторное использование.** Набор, скорее всего, одноразовый (нельзя перечитать
  `IAsyncEnumerable`), поэтому `Prepare()`/план-кэш на bulk не распространяются — SQL либо не
  строится вовсе (нативный путь), либо строится чанкером.
- **Провайдерные различия.** Типы/точность, `null`-семантика и поведение по умолчанию (identity,
  триггеры, `SqlBulkCopyOptions`/`CheckConstraints`) у нативных API отличаются; расхождения
  документируются, а не унифицируются.
- **ClickHouse.** Транзакций нет; бинарная вставка — основной путь, `VALUES` — только малые батчи.
- **Публичный API расширяется**: новый `BulkInsertBuilder<TEntity>`,
  `DataContextExtensions.BulkInsertInto<T>`, флаг `ISqlDialect.SupportsBulkCopy` и роль
  `IBulkInsertExecutor`; обновить `API-NAMING-REVIEW.md` и (при заморозке) `PublicAPI.*`.
  Флаг `SupportsBulkCopy` — default-член, поэтому внешние реализации `ISqlDialect` не ломаются (как
  `SupportsReturning`/`SupportsOutput`/`SupportsLastInsertId`).
- **Аллокации.** Нативный путь не должен связывать каждую ячейку в `InsertValue` и не должен
  удерживать весь набор: строка читается из сущности непосредственно перед записью в драйвер.

## Этапы внедрения

- **Фаза 1 (портируемая):** `BulkInsertBuilder<TEntity>` + `BulkInsertInto<T>()`, `BulkInsertCommand`,
  чанкинг `VALUES` через `SqlMutationBuilder`, `BatchSize`, `Timeout`, собственная транзакция.
  Работает на всех SQL-провайдерах; нативные ветки ещё нет. Закрывает ограничение «no chunking».
- **Фаза 2 (нативная):** `IBulkInsertExecutor` + `SupportsBulkCopy`; PostgreSQL `COPY BINARY`,
  SQL Server `SqlBulkCopy` (+ `KeepIdentity`, `NotifyAfter`), MySQL/MariaDB `MySqlBulkCopy`,
  ClickHouse бинарная вставка.
- **Фаза 3:** интеграция с `ITransactionManager`, отмена/прогресс, `IAsyncEnumerable`, проверка
  MariaDB и тонкая настройка лимитов чанкера per-provider. In-memory не планируется: `INSERT` в
  in-memory — вне области (см. [todo_insert.md](todo_insert.md)).
- **Вне области:** change tracking/`SaveChanges`, upsert/`MERGE` (см. [todo_merge.md](todo_merge.md)),
  `INSERT ... SELECT`/CTAS (см. [todo_create_table_as_select.md](todo_create_table_as_select.md)),
  streaming LOB.

## План тестов

- Core (`tests/nextorm.core.tests/`): расчёт размера чанка по лимитам; `BulkInsert` без значений
  бросает; identity/computed колонки исключаются; in-memory — `NotSupportedException`;
  `BulkInsertInto` на контексте без роли — понятная ошибка.
- SQL-gen (`tests/nextorm.{sqlite,postgres,sqlserver,mysql}.tests/SqlGenerationTests.cs`): fallback
  рендерит ожидаемое число чанков и корректную параметризацию; экранирование имён; пустой набор.
- Диалекты (`*DialectTests.cs`): значения `SupportsBulkCopy` (SQLite — `false`, PostgreSQL/SQL
  Server/MySQL/ClickHouse — `true`) и лимитов чанкера.
- Интеграция (`tests/nextorm.integration.tests/`): нативный путь на PostgreSQL/SQL Server/MySQL/
  ClickHouse (большой набор, проверка числа строк и содержимого), fallback на SQLite
  (набор > лимита параметров одним вызовом); откат при ошибке в середине набора (атомарность);
  `BatchSize`; `KeepIdentity` (SQL Server, provider-specific); `null`-значения; отмена.
- Покрытие: база 84.9% line / 73.2% branch (порог 75%); новые файлы core входят в
  `coverage.settings.xml` (`nextorm.{core,sqlite,postgres,sqlserver}`); MySQL/ClickHouse-специфика
  покрытием не измеряется — указывается явно.

## Открытые вопросы

1. Отдельный `BulkInsertBuilder<TEntity>` vs терминал `BulkInsert()` на `InsertBuilder<TEntity>`
   (рекомендация — отдельный: свой набор опций и отсутствие `Returning`).
2. Потоковый источник: обязателен `IAsyncEnumerable<TEntity>` или достаточно `IEnumerable`?
   Как быть с повторной попыткой/ретраем одноразового потока при транзиентной ошибке?
3. Транзакция: всегда открывать собственную или требовать внешнюю (после `todo_transactions.md` —
   делегировать менеджеру)?
4. Гранулярность `BatchSize`: одна опция у билдера vs переопределение через диалект/провайдер.
5. MariaDB и `MySqlBulkCopy`: подтвердить поддержку версий; иначе fallback на чанки/`LOAD DATA`.
6. Прогресс и отмена на нативном пути: единый делегат `NotifyAfter` поверх разных API
   (`SqlBulkCopy.NotifyAfter`, счётчик в `COPY`-цикле) vs отказ от прогресса в фазе 1.
7. Клиентские ограничения (SQL Server 2100 параметров, SQLite `SQLITE_MAX_VARIABLE_NUMBER`) —
   зашивать константой в диалект или брать из соединения (`ServerVersion`)?

## Файлы к изменению

- Новое: `src/nextorm.core/Builders/BulkInsertBuilder.cs`,
  `src/nextorm.core/Query/Mutations/BulkInsertCommand.cs`,
  `src/nextorm.core/DataContext/Roles/IBulkInsertExecutor.cs`.
- Правки: `src/nextorm.core/DataContext/DataContext.cs` (фабрика роли, реализация fallback’а),
  `DataContext/QueryExecutor.cs` (доступ к connection/transaction для чанков),
  `DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs` (`SupportsBulkCopy` и лимиты),
  провайдерные диалекты и контексты (`Postgres/SqlServer/MySql/SQLite/ClickHouse`),
  `DataContextExtensions.cs`, `DataContext/InMemoryDataContext.cs`.
- Тесты: `tests/nextorm.{core,sqlite,postgres,sqlserver,mysql,clickhouse}.tests/`,
  `tests/nextorm.integration.tests/*SpecificTests.cs` (нативные пути не единообразны — provider-specific).
- Документация: `docs/guide/19-insert-statement.md` (+RU), `docs/providers/*` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/comparison/linq2db-comparison.md` (+RU),
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/design/API-NAMING-REVIEW.md`.
