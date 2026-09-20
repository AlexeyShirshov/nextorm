# WIP: Стриминг BLOB/CLOB (`Stream` / `TextReader`)

> Рабочий план (design RFC). Источник: GitHub issue
> [#27 «TODO: BLOB/CLOB support»](https://github.com/AlexeyShirshov/nextorm/issues/27),
> milestone `1.1-a.1`. Продолжение уже сделанной буферизованной поддержки `byte[]`
> (`CommonTestSuite.Binary.cs`): здесь речь только о **потоковом** чтении больших значений.

## Пункт и цель

- Фича: читать большие бинарные (`BLOB`/`bytea`/`varbinary`) и текстовые (`CLOB`/`text`/`nvarchar(max)`)
  значения как `Stream` / `TextReader`, не загружая значение целиком в managed-память.
- Критерий приёмки: значение в 8 МБ читается с памятью O(размера буфера), а не O(размера
  значения); возвращённый поток владеет `DbDataReader`/`DbCommand` и корректно их освобождает;
  драйверы без поддержки (ClickHouse) бросают `NotSupportedException` с явным сообщением.
- **Не путать с потоковой выдачей строк.** `ToAsyncEnumerable` (`nonStreamUsing: false`) уже
  отдаёт строки по одной, но **каждое поле** по-прежнему материализуется целиком
  (`SelectExpression.cs:79-82,111-116`).

## Почему это нужно (мотивация)

1. **Пробел, замаскированный «поддержкой».** `docs/advanced/limitations.md:49` относит
   `byte[]` к «не ограничениям», но чтение идёт через `GetValue`/`GetString` — значение целиком
   попадает в массив/строку. Для файлов/изображений/документов это OOM и лишние копии.
2. **Паритет с EF Core / ADO.NET.** `DbDataReader.GetStream`/`GetTextReader` — стандартный способ
   читать LOB; linq2db/EF Core его предоставляют, nextorm — нет.
3. **Дешёвая база.** `DbDataReader` уже доступен внутри `QueryExecutor`
   (`QueryExecutor.cs:131,151,177,191`), а `byte[]`/`string` — единственные два LOB-типа; нужно
   добавить `CommandBehavior.SequentialAccess` и терминал-обёртку, без переписывания план-кэша.

## Текущее состояние и разрыв

| Слой | Где | Чего не хватает |
|---|---|---|
| Геттеры полей | `Expressions/SelectExpression.cs:63-119` | `string`→`GetString` (`:79-82`), `byte[]`→`GetValue` (`:111-116`) — оба буферизуют |
| Поведение ридера | `Cache/DbPreparedQueryCommand.cs:12,28-29`, `QueryPlanner.cs:132-137` | только `Behavior = 0`/`SingleRow`; `SequentialAccess` не выставляется |
| Чтение | `QueryExecutor.cs:131,151,177,191` | `GetStream`/`GetTextReader`/`GetChars` не вызываются нигде |
| Строковый стрим | `ResultSetEnumerator.cs:125,135,149` | маппер срабатывает внутри `MoveNext`; ленивый `Stream` станет невалидным после следующего `MoveNext` |
| Публичный доступ | `Cache/IPreparedQueryCommand.cs:8-67` | нет терминала и нет выхода к `DbDataReader` |
| Named-column режим | `Builders/TableAlias.cs:21,33,51-52` | `GetBytes`/`AsBytes` — заглушки, стриминга нет |
| Диалект | `Dialect/ISqlDialect.cs:354-359` | нет флага `SupportsSequentialAccess` |

**Провайдерные особенности (проверено по XML-докам драйверов, версии из `Directory.Packages.props`):**

- Microsoft.Data.Sqlite 10.0.12: `SqliteDataReader.GetStream` возвращает настоящий `SqliteBlob`
  **только если в проекции есть `rowid`** (или его алиас), иначе всё значение читается в
  `MemoryStream`; `GetTextReader`/`GetChars` есть.
- Microsoft.Data.SqlClient 6.1.7: `GetStream`/`GetTextReader`/`GetChars` есть; для потокового
  поведения обязателен `CommandBehavior.SequentialAccess`, иначе значение буферизуется.
- Npgsql 10.0.3: `GetStream`/`GetTextReader`/`GetChars` поддерживаются.
- MySqlConnector 2.6.2: `GetBytes`/`GetChars` есть; поддержку `GetStream`/`GetTextReader` нужно
  подтвердить тестом (открытый вопрос №1).
- ClickHouse.Driver 1.4.0: членов потокового чтения нет — `NotSupportedException`.

## Дизайн

Три уровня, фаза 1 — минимальный полезный срез.

### Фаза 1 — скалярный LOB-терминал

Один выбранный столбец, одно значение; возвращаемый поток **владеет** ридером и командой до
`Dispose`:

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

// BLOB
Stream s = ctx.From<BinaryEntity>()
    .Where(x => x.Id == 1)
    .Select(x => x.Data)
    .ToStream();                    // reader открыт с SequentialAccess

// CLOB
TextReader r = ctx.From<Document>()
    .Where(x => x.Id == 1)
    .Select(x => x.Body)
    .ToTextReader();

// async-открытие (сам GetStream синхронный — см. ограничения)
Stream s2 = await ctx.From<BinaryEntity>().Where(x => x.Id == 1).Select(x => x.Data)
    .ToStreamAsync(cancellationToken);
```

Черновые сигнатуры:

```csharp
public static Stream ToStream<TResult>(this EntityBuilder<TResult> builder, params object[] @params);
public static Stream ToStream<TResult>(this EntityBuilder<TResult> builder, CancellationToken cancellationToken, params object[] @params);
public static Task<Stream> ToStreamAsync<TResult>(this EntityBuilder<TResult> builder, params object[] @params);
public static Task<Stream> ToStreamAsync<TResult>(this EntityBuilder<TResult> builder, CancellationToken cancellationToken, params object[] @params);

public static TextReader ToTextReader<TResult>(this EntityBuilder<TResult> builder, params object[] @params);
public static Task<TextReader> ToTextReaderAsync<TResult>(this EntityBuilder<TResult> builder, params object[] @params);
```

Аналогичные перегрузки для `EntityBuilder<TResult>` и `QueryCommand<TResult>` (по образцу
`ToAsyncEnumerable` в `Builders/EntityBuilderExtensions.cs:14-16`).

Правила:

- проекция обязана быть **одним** столбцом типа `byte[]`/`string`; иначе — `InvalidArgumentException`
  с подсказкой использовать `ToDataReader` (фаза 2);
- терминал не идёт через `RowMapperFactory`, а читает `reader.GetStream(0)`/`GetTextReader(0)`
  напрямую — кэш мапперов и ключ плана не меняются;
- владение: поток-обёртка при `Dispose`/`DisposeAsync` освобождает `DbDataReader` и `DbCommand`
  (соединение возвращается в пул); контекст должен оставаться живым;
- исключение «владения»: если контекст `Dispose`-нули раньше потока — операция чтения бросает
  понятную ошибку.

### Фаза 2 — escape hatch к `DbDataReader` и чанковое чтение

- `ToDataReader()` / `ToDataReaderAsync()` — ридер с `SequentialAccess` для нескольких столбцов
  и строк; владелец — вызывающий.
- `GetBytes`/`GetChars`-терминалы чанками (для драйверов, где `GetStream` неудобен или требует
  `rowid`, — прежде всего SQLite).
- `TableAlias.GetStream`/`GetTextReader`/`GetChars` + `TableColumn.AsStream` в named-column режиме
  (`Builders/TableAlias.cs`).

### Фаза 3 (опционально) — `Stream`/`TextReader` в проекции строкового стрима

Разрешить `Select(x => new { x.Id, Data = x.Payload.AsStream() })` в `ToAsyncEnumerable` с явным
контрактом: поток валиден **только до следующего `MoveNext`**, должен быть прочитан/освобождён
внутри итерации. Требует флага стриминга в ключе маппера
(`RowMapperFactory.BuildKey`, `DataContext/RowMapperFactory.cs:117-139`). Опасный API, поэтому
последним и опционально.

## Диалектный план

- `ISqlDialect`/`SqlDialectBase` (base `true`): `SupportsSequentialAccess`.
  `ClickHouseDialect` — `false` (как `SupportsCommandBehaviorSingleRow`,
  `nextorm.clickhouse/ClickHouseDialect.cs:133-135`).
- Опционально `SupportsGetStream`/`SupportsGetTextReader` для точечных различий драйверов.
- `DbPreparedQueryCommand.Behavior` дополняется `| CommandBehavior.SequentialAccess`, когда
  prepared-команда помечена как LOB-стриминг (рядом с `SingleRow`, `DbPreparedQueryCommand.cs:28-29`;
  решение — в `QueryPlanner.cs:132-137`).
- SQLite: терминал должен либо добавить `rowid` в SELECT (для `SqliteBlob`), либо пойти чанковым
  `GetBytes` — открытый вопрос №2.

## Провайдерная матрица

| Провайдер | `GetStream` | `GetTextReader` | `GetBytes`/`GetChars` | Комментарий |
|---|---|---|---|---|
| SQLite | да, `SqliteBlob` при `rowid` в проекции, иначе `MemoryStream` | да | да | true streaming требует `rowid` |
| PostgreSQL (Npgsql) | да | да | да | |
| SQL Server | да (нужен `SequentialAccess`) | да | да | без `SequentialAccess` буферизует |
| MySQL / MariaDB (MySqlConnector) | подтвердить тестом | подтвердить тестом | да | |
| ClickHouse | нет | нет | нет | `NotSupportedException` |
| In-memory | `MemoryStream` над значением (фаза 2) | `StringReader` (фаза 2) | — | фаза 1 — `NotSupportedException` |

## Ограничения и цена

- **`SequentialAccess` меняет порядок чтения.** Столбцы нельзя читать в произвольном порядке и
  повторно; LOB-столбец должен идти последним/единственным. Терминал фазы 1 проектирует один
  столбец, поэтому ограничение не проявляется.
- **Время жизни `Stream`.** Он жив, пока открыт ридер; в стриме строк — только до следующего
  `MoveNext` (фаза 3).
- **`GetStream` синхронен.** `ToStreamAsync` асинхронно открывает ридер/команду, но сам геттер
  синхронный; асинхронное чтение — уже через `Stream.ReadAsync` на возвращённом потоке.
- **Только чтение.** Запись BLOB/CLOB — в DML (#3–#6), вне этой фичи.
- **Буферизованный путь остаётся дефолтом.** `byte[]`/`string` в POCO по-прежнему материализуются
  целиком; стриминг — opt-in через новый терминал (обратная совместимость).
- **Публичный API** (`ToStream`/`ToTextReader`/обёртки) — обновить `API-NAMING-REVIEW.md`; при
  заморозке поверхности — `PublicAPI.*`.
- **Покрытие/аллокации**: базовое 84.9% line / 73.2% branch; новый путь не должен добавлять
  боксинга на буферизованном дефолте.

## Этапы внедрения

- **Фаза 1 (MVP):** `SupportsSequentialAccess`, `ToStream`/`ToTextReader` (+async) для одного
  `byte[]`/`string`-столбца, владение ридером/командой, SQLite-обработка `rowid`,
  ClickHouse/in-memory — `NotSupportedException`.
- **Фаза 2:** `ToDataReader`, чанковые `GetBytes`/`GetChars`, `TableAlias`-аксессоры, in-memory.
- **Фаза 3 (опционально):** `Stream`/`TextReader` в проекции строкового стрима с контрактом
  времени жизни.
- **Вне области:** запись/`BulkCopy`, сжатие, шифрование потока, серверные LOB-операции.

## План тестов

- SQL-gen (`tests/nextorm.{sqlite,postgres,sqlserver,mysql}.tests/SqlGenerationTests.cs`):
  для LOB-терминала `DbPreparedQueryCommand.Behavior` содержит `SequentialAccess`
  (проверка на prepared-команде, а не тексте SQL); SQLite — `rowid` в проекции, если выбран этот
  путь.
- Диалекты (`*DialectTests.cs`): `SupportsSequentialAccess` (`false` у ClickHouse).
- Интеграция (`tests/nextorm.integration.tests/`): round-trip 8 МБ BLOB и 8 МБ CLOB через
  `ToStream`/`ToTextReader`; побайтовое равенство; `Dispose` потока освобождает ридер и не рвёт
  контекст; повторное чтение после `Dispose` бросает; ClickHouse — `NotSupportedException`.
  SQLite — отдельный `*SpecificTests.cs` на два режима (`rowid` → `SqliteBlob` vs fallback).
- Память: тест-предохранитель на аллокации (не более N× от размера буфера) — по образцу
  `docs/specs/performance/benchmark-report.md`; опционально бенчмарк в `benchmarks/nextorm.benchmark`.
- Покрытие: не ниже базы; новые файлы core входят в `coverage.settings.xml`.

## Открытые вопросы

1. Поддерживает ли `MySqlConnector.GetStream`/`GetTextReader` потоково и нужен ли ему
   `SequentialAccess` — подтвердить тестом до реализации.
2. SQLite: добавлять `rowid` в SELECT автоматически (риск конфликта с явной проекцией/`DISTINCT`)
   или всегда идти чанковым `GetBytes` (проще, но не «настоящий» `SqliteBlob`).
3. Минимальный API фазы 1: только `ToStream`/`ToTextReader` или сразу `ToDataReader`.
4. Имена обёрток: `NextOrmStream`/`NextOrmTextReader` vs `LobStream`/`LobTextReader`; `ToStream` vs
   `AsStream`.
5. Как сочетать `SequentialAccess` и `SingleRow` (`SupportsCommandBehaviorSingleRow`) и не сломать
   ClickHouse-ветку.
6. Нужен ли `Stream`/`TextReader` в проекциях POCO вообще (фаза 3), учитывая опасный контракт
   времени жизни.
7. Реагировать ли на `CommandBehavior.SequentialAccess` в `DbPreparedQueryCommand` автоматически
   по типу проекции или только через явный LOB-терминал.

## Файлы к изменению

- Новое: `src/nextorm.core/DataContext/LobStream.cs`, `src/nextorm.core/DataContext/LobTextReader.cs`
  (обёртки владения), `src/nextorm.core/DataContext/Roles/ILobReader.cs` (если нужна отдельная роль),
  `tests/nextorm.integration.tests/CommonTestSuite.Lob.cs`.
- Правки: `src/nextorm.core/Builders/EntityBuilderExtensions.cs`,
  `src/nextorm.core/Query/QueryCommandExtensions.cs`, `DataContext/QueryExecutor.cs`,
  `DataContext/ResultSetEnumerator.cs`, `DataContext/Cache/DbPreparedQueryCommand.cs`,
  `DataContext/QueryPlanner.cs`, `DataContext/Dialect/ISqlDialect.cs`,
  `DataContext/Dialect/SqlDialectBase.cs`, `DataContext/InMemoryDataContext.cs`,
  `Builders/TableAlias.cs` (фаза 2), провайдерные `*Dialect.cs`.
- Тесты: `tests/nextorm.{core,sqlite,postgres,sqlserver,mysql}.tests/`,
  `tests/nextorm.integration.tests/CommonTestSuite.Lob.cs`, `*SpecificTests.cs`.
- Документация: `docs/advanced/limitations.md` (уточнить буферизацию `byte[]`, +RU),
  `docs/advanced/api-reference.md` (+RU), `docs/guide/` (раздел о больших значениях, +RU),
  `docs/providers/*` (+RU), `todo_*`/`sql-capabilities-gap-analysis.md` (если потребуется),
  `docs/specs/design/API-NAMING-REVIEW.md`.
