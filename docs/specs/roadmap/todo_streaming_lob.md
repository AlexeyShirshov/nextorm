# TODO: Стриминг BLOB/CLOB (`Stream` / `TextReader`)

> Рабочий план (design RFC). Источник: GitHub issue
> [#27 «TODO: BLOB/CLOB support»](https://github.com/AlexeyShirshov/nextorm/issues/27),
> milestone `1.0.8-b`. Продолжение уже сделанной буферизованной поддержки `byte[]`
> (`CommonTestSuite.Binary.cs`): здесь речь только о **потоковом** чтении больших значений.

> **Закрыто (2026-09-27).** Issue #27 закрыт: ядро фаз 1–2 отгружено — скалярный стриминг
> `ToStream`/`ToTextReader` (+async) на PostgreSQL/SQL Server/SQLite, `ToDataReader`/`ToDataReaderAsync`
> фазы 2 на PostgreSQL/SQL Server, in-memory скалярный стриминг (цикл 5); критерий приёмки выполнен.
> Невыполнимый/без-потребителя остаток вынесен scope-reduction'ом в follow-up:
> [#100](https://github.com/AlexeyShirshov/nextorm/issues/100) (server-side LOB chunking —
> MySQL/MariaDB streaming и чанковые `GetBytes`/`GetChars`) и
> [#101](https://github.com/AlexeyShirshov/nextorm/issues/101) (`TableAlias`-аксессоры и фаза 3).
> Детали — раздел «Follow-up» ниже; status-файлы циклов 1–5 удалены при закрытии (перенос — регистр
> `docs/specs/design/code-smells-review.md` §«Перенесено из status закрытого потока `lob-streaming`» и
> раздел «Deferred + триггер» ниже).

> **Пересмотр 2026-09-26.** Терминалы вешаются только на `QueryCommand<T>` (не на
> `EntityBuilder<TResult>`). Владение reader'ом переиспользует владельца из **фазы 0**
> `todo_stored_procedures.md` с per-call `DbCommand` — это снимает
> конфликт с общим кэшированным `DbCommand`. Стриминг-дискриминатор входит в **ключ плана**, а
> `Behavior |= CommandBehavior.SequentialAccess` строится в `QueryPlanner` при создании команды
> (`Behavior` — readonly). Sync-терминалы принимают `params ReadOnlySpan<object?>`. Зависимость от #70
> (фаза 0) — первой. **Готово (2026-09-26):** фаза 0 #70 реализована — `CommandReaderOwner` владеет
> per-call `DbCommand` + `DbDataReader`; зависимость снята.

> **Отгружено (2026-09-27), фаза 1.** Терминалы `ToStream`/`ToStreamAsync`/`ToTextReader`/`ToTextReaderAsync`
> на `QueryCommand<T>` реализованы для **PostgreSQL и SQL Server** (`CommandBehavior.SequentialAccess`,
> владение reader'ом и per-call командой до `Dispose`, ровно одна колонка `byte[]`/`string`). Остальные
> провайдеры (SQLite, MySQL/MariaDB, ClickHouse, in-memory) в этом срезе отклоняют терминал через
> `NotSupportedException`. Фазы 2 (`ToDataReader`, чанковое чтение) и 3 остаются планом; issue #27 открыт.
> Верификация цикла 1 — `docs/specs/status/lob-streaming-1.md`; перф-замер — `docs/specs/performance/lob-streaming-benchmarks.md`.

> **Ревизия цикла 2 (2026-09-27, experiment-only).** Открытые вопросы №1 и №2 закрыты на реальных
> драйверах: MySQL/MariaDB — **NEGATIVE** (MySqlConnector 2.6.2 буферизует значение; остаются
> `NotSupportedException`), SQLite — **POSITIVE только для проекции `payload, rowid`** (настоящий
> `SqliteBlob`, память O(buffer)). Реализация SQLite-шва — цикл 3; product-код циклом 2 не менялся.
> Логи/вердикт — `docs/specs/status/lob-streaming-2.md`.

> **Отгружено (2026-09-27), цикл 3 — SQLite binary + text.** SQLite подключён к существующим
> терминалам `ToStream`/`ToTextReader` (+async) через **trailing locator**: новый `ISqlDialect.LobLocatorColumn`
> (`string?`, DIM `=> null;`), `SqlDialectBase` — `virtual string? => null;`, `SqliteDialect` —
> `SupportsSequentialAccess => true` + `LobLocatorColumn => "rowid"`. SQLite LOB-SQL: `SELECT <payload>, rowid FROM ...`,
> payload остаётся **ordinal 0**. Провайдеры: **PostgreSQL, SQL Server, SQLite — поддержаны**;
> **MySQL/MariaDB — нет** (MySqlConnector буферизует значение — результат цикла 2); **ClickHouse/in-memory —
> `NotSupportedException`**. Ограничения SQLite (внутренняя заметка): raw `WithSql` с одной колонкой →
> `NotSupportedException` (locator некуда безопасно добавить), raw ≥2 колонок → `InvalidOperationException`;
> join/multi-source/`DISTINCT`/`UNION`/`GROUP BY`/агрегаты — не поддержаны и могут падать драйверной ошибкой
> (`no such column: rowid` или иной), **deferred** с триггером «конкретный пользовательский сценарий».
> Фазы 2 (`ToDataReader`) и 3 (проекция) остаются планом; issue #27 открыт.
> Верификация цикла — `docs/specs/status/lob-streaming-3.md`.

> **Отгружено (2026-09-27), цикл 5 — in-memory (срез A).** Скалярные терминалы `ToStream`/`ToStreamAsync`/
> `ToTextReader`/`ToTextReaderAsync` работают на провайдере **in-memory**: поскольку `DbDataReader` нет,
> терминал возвращает обычный BCL-объект над единственным материализованным значением — `MemoryStream`
> над `byte[]` и `StringReader` над `string` (владение у вызывающего, на контексте освобождать нечего).
> Пустой результат и `NULL` в первой строке дают `Stream.Null`/`TextReader.Null`; при нескольких строках
> читается только первая. `ToDataReader`/`ToDataReaderAsync` на in-memory остаются `NotSupportedException`
> (нет `DbDataReader`). Чанковые `GetBytes`/`GetChars`, `TableAlias`-аксессоры и фаза 3 остаются планом;
> issue #27 открыт. Верификация цикла — `docs/specs/status/lob-streaming-5.md`.

> **Отгружено (2026-09-27), цикл 4 — `ToDataReader`/`ToDataReaderAsync`.** Фаза 2 частично:
> терминалы `ToDataReader<TResult>`/`ToDataReaderAsync<TResult>` (sync+async, с/без
> `CancellationToken`) на `QueryCommand<TResult>` возвращают владеемый вызывающим forward-only
> `DbDataReader` (`internal sealed LobDataReader`, владеет `CommandReaderOwner`). Поддержаны
> **PostgreSQL/SQL Server** (нужен `SupportsSequentialAccess`); **SQLite — fail-closed**
> (`NotSupportedException`: локатор `rowid` в general multi-column reader этим циклом не поддержан);
> **MySQL/MariaDB/ClickHouse — `NotSupportedException`** (in-memory поддержан циклом 5). Чанковые
> `GetBytes`/`GetChars` и MySQL/MariaDB-streaming вынесены в #100, `TableAlias`-аксессоры и фаза 3 —
> в #101; issue #27 **закрыт**.
> Верификация цикла — `docs/specs/status/lob-streaming-4.md`.

> **Отгружено (2026-09-27), #101 — `TableAlias`-аксессоры и фаза 3.** Именованные потоковые
> аксессоры `TableAlias.GetStream(string)`/`GetTextReader(string)` и члены `TableColumn.AsStream`/
> `AsTextReader` (throws-маркеры query-expression), терминалы на `QueryCommand<Stream>`/
> `QueryCommand<TextReader>` (+async), а также **фаза 3** — построчный стриминг `Stream`/`TextReader`
> внутри `ToAsyncEnumerable`. Скоуп провайдеров — только `SupportsSequentialAccess`:
> **PostgreSQL/SQL Server/SQLite** (SQLite добавляет trailing `rowid`), **MySQL/MariaDB/ClickHouse
> бросают `NotSupportedException`**. Контракт времени жизни: значение валидно **только до следующего
> `MoveNext`**; после — undefined/зависит от провайдера, гарантируется лишь безопасность (прочитать/
> освободить внутри итерации). Стриминг-план готовится `storeInCache: false` и не кэшируется; ключ
> маппера получает флаг `Streaming`, нестриминговые пути сохраняют прежний ключ. Вынесенные пункты —
> раздел «Deferred/accepted — #101» ниже; status-файл цикла удалён при коммите #101.

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
- MySqlConnector 2.6.2: `GetBytes`/`GetChars` есть; `GetStream`/`GetTextReader` **буферизуют**
  значение целиком (`MemoryStream` поверх буферизованной строки / `StringReader(GetString())`),
  `SequentialAccess` — no-op (подтверждено циклом 2, вопрос №1 закрыт).
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
public static Stream ToStream(this QueryCommand<byte[]> command, params ReadOnlySpan<object?> @params);
public static Stream ToStream(this QueryCommand<byte[]> command, CancellationToken cancellationToken, params ReadOnlySpan<object?> @params);
public static Task<Stream> ToStreamAsync(this QueryCommand<byte[]> command, params object[] @params);
public static Task<Stream> ToStreamAsync(this QueryCommand<byte[]> command, CancellationToken cancellationToken, params object[] @params);

public static TextReader ToTextReader(this QueryCommand<string> command, params ReadOnlySpan<object?> @params);
public static Task<TextReader> ToTextReaderAsync(this QueryCommand<string> command, params object[] @params);
```

Терминалы — **только** на `QueryCommand<TResult>` (не на `EntityBuilder<TResult>`): `Select` возвращает
`QueryCommand<TResult>` (`src/nextorm.core/Builders/EntityBuilder.cs:1952`), поэтому пример выше
разрешается на этом ресивере. Sync-терминалы принимают `params ReadOnlySpan<object?>` (sync-образец —
`Builders/EntityBuilderExtensions.cs:181`); `ToAsyncEnumerable` — `Builders/EntityBuilderExtensions.cs:14-16`.

Правила:

- проекция обязана быть **одним** столбцом типа `byte[]`/`string`; иначе — `InvalidOperationException`
  с подсказкой использовать `ToDataReader` (фаза 2);
- терминал не идёт через `RowMapperFactory`, а читает `reader.GetStream(0)`/`GetTextReader(0)`
  напрямую; стриминг-дискриминатор входит в **ключ плана** (см. диалектный план), поэтому тот же
  SQL-shape не переиспользуется с буферизованным `Behavior=0`;
- владение: поток-обёртка — это **владелец reader'а из фазы 0** #70
  (`todo_stored_procedures.md`): держит `DbDataReader` + **per-call**
  `DbCommand` и при `Dispose`/`DisposeAsync` освобождает их (соединение возвращается в пул и остаётся
  во владении контекста). Общий кэшированный `DbCommand` из `DbPreparedQueryCommand`
  (`src/nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs:21`) не используется — нет конфликта с
  повторным исполнением формы; контекст должен оставаться живым;
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
- `Behavior` — `readonly`, выставляется только в ctor (`DbPreparedQueryCommand.cs:16,75-76`), поэтому
  `| CommandBehavior.SequentialAccess` строится в `QueryPlanner` при создании команды
  (`PreparedCommandOptions(...)`, `QueryPlanner.cs:451`), а не «дополняется» после. Стриминг-
  дискриминатор входит в **ключ плана**, иначе тот же SQL-shape вернётся с буферизованным `Behavior=0`.
- SQLite: терминал добавляет **trailing `rowid`** в SELECT через LOB-only шов проекции (payload
  остаётся ordinal 0) для настоящего `SqliteBlob`; view/`WITHOUT ROWID` — fail-closed. Чанковый
  `GetBytes` **отклонён** (вопрос №2 закрыт циклом 2; реализация — цикл 3).

## Провайдерная матрица

| Провайдер | `GetStream` | `GetTextReader` | `GetBytes`/`GetChars` | Комментарий |
|---|---|---|---|---|
| SQLite | да, `SqliteBlob` (trailing `rowid`) | да | да | true streaming через `LobLocatorColumn => "rowid"`; **отгружено циклом 3** |
| PostgreSQL (Npgsql) | да | да | да | |
| SQL Server | да (нужен `SequentialAccess`) | да | да | без `SequentialAccess` буферизует |
| MySQL / MariaDB (MySqlConnector) | нет (буферизует в `MemoryStream`) | нет (`StringReader(GetString())`) | да | цикл 2 → `NotSupportedException` |
| ClickHouse | нет | нет | нет | `NotSupportedException` |
| In-memory | `MemoryStream` над значением — **✅ цикл 5** | `StringReader` — **✅ цикл 5** | — | скалярные терминалы поддержаны (срез A); `ToDataReader` — `NotSupportedException` (нет `DbDataReader`) |

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

Зависимость: фаза 1 может идти параллельно с #70, но **требует владельца reader'а из фазы 0**
`todo_stored_procedures.md` (per-call `DbCommand`), поэтому фаза 0 #70 —
первой.

- **Фаза 1 (MVP) — ✅ отгружена (2026-09-27, PostgreSQL + SQL Server):** `SupportsSequentialAccess`, `ToStream`/`ToTextReader` (+async) для одного
   `byte[]`/`string`-столбца, владение ридером/командой; SQLite-обработка `rowid` подтверждена циклом 2
   (positive) и **реализована в цикле 3**; ClickHouse — `NotSupportedException`, а in-memory добавлен срезом A в цикле 5.
- **Фаза 2 — частично ✅ (2026-09-27, циклы 4–5):** `ToDataReader`/`ToDataReaderAsync` отгружены для
  PostgreSQL/SQL Server; **SQLite — fail-closed** (general multi-column reader с локатором `rowid`
  не поддержан). **In-memory streaming (срез A) ✅ цикл 5:** скалярные `ToStream`/`ToTextReader` (+async)
  возвращают `MemoryStream`/`StringReader` над материализованным значением (пустой результат/NULL →
  `Stream.Null`/`TextReader.Null`). Чанковые `GetBytes`/`GetChars` и MySQL/MariaDB-streaming вынесены в
  **#100** (открыт; это остаток фазы 2), `TableAlias`-аксессоры — **✅ отгружены #101 (2026-09-27)**.
- **Фаза 3 — ✅ отгружена (2026-09-27, #101):** `Stream`/`TextReader` в проекции строкового стрима
  внутри `ToAsyncEnumerable` с контрактом времени жизни (валиден до следующего `MoveNext`), только
  для `SupportsSequentialAccess`-провайдеров (PostgreSQL/SQL Server/SQLite); MySQL/MariaDB/ClickHouse —
  `NotSupportedException`.
- **Вне области:** запись/`BulkCopy`, сжатие, шифрование потока, серверные LOB-операции.

## Follow-up (scope-reduction при закрытии #27, 2026-09-27)

Отгружено по #27: фаза 1 (PostgreSQL, SQL Server, SQLite — скалярный `ToStream`/`ToTextReader`),
фаза 2 `ToDataReader`/`ToDataReaderAsync` (PostgreSQL/SQL Server; SQLite/in-memory — fail-closed),
in-memory скалярный стриминг (цикл 5). Status-файлы циклов 1–5 удалены при закрытии #27; их история —
разделы «Цикл #27 — …» в `docs/specs/design/code-smells-review.md`.

Невыполнимые или не имеющие потребителя пункты чеклиста вынесены:

- **[#100](https://github.com/AlexeyShirshov/nextorm/issues/100) — server-side LOB chunking
  (remaining deferred).** MySQL/MariaDB streaming + публичные чанковые `GetBytes`/`GetChars`
  (отвергнуты для SQLite циклом 2, оставлены для остальных сценариев).
  **Разведка 2026-09-27 (negative result):** на закреплённом `MySqlConnector 2.6.2`
  (MySQL 8.4 + MariaDB 11.4) memory-bounded пути чтения нет. Измеренные отношения аллокаций
  1→8 МиБ (пробник `tests/nextorm.integration.tests/LobCapabilityProbeTests.cs`, env-gate
  `NEXTORM_LOB_PROBE=1`): baseline `GetStream` **7.99**; фиксированный буферный цикл `GetBytes`
  **7.99** (byte-exact); server-side пагинация `SUBSTRING` **8.00** (byte-exact; ограничивает пик,
  но не кумулятивную аллокацию); цикл `GetChars` **59.20** (O(n²)). **Решение:** чанковые
  `GetBytes`/`GetChars`-терминалы как streaming API **не отгружать**; #100 остаётся deferred с
  триггером «принять по-настоящему стриминговый драйвер MySQL/MariaDB». План-файл сохранён
  (фаза 2 чанкового чтения частично остаётся нереализованной).
- **✅ [#101](https://github.com/AlexeyShirshov/nextorm/issues/101) — `TableAlias`-аксессоры и фаза 3
  (отгружено 2026-09-27 на `1.0.9-a`).** `TableAlias.GetStream`/`GetTextReader` и
  `TableColumn.AsStream`/`AsTextReader` (named-column режим) + фаза 3 (`Stream`/`TextReader` в проекции
  строкового стрима). `GetChars` намеренно не выставлен — чанковое чтение остаётся в **#100**.
  Открытые пункты перенесены в раздел «Deferred/accepted — #101»; issue закрыт.

## Deferred + триггер (перенесено из удалённых status-файлов, 2026-09-27)

Открытые пункты `docs/specs/status/lob-streaming-{1..5}.md` (удалены при закрытии #27; вынесенное в
#100/#101 здесь не дублируется). Зеркало — `docs/specs/design/code-smells-review.md`
§«Перенесено из status закрытого потока `lob-streaming`».

- **Mid-read cancel.** Токен действует только на открытие ридера; чтение уже возвращённого
  `Stream`/`TextReader` не отменяется. Триггер: следующая правка LOB-ридера.
- **SQLite locator-backed multi-column `ToDataReader`.** Нужна отдельная модель скрытия `rowid` +
  доказательство порядка чтения. Триггер: конкретный пользовательский сценарий.
- **Двойное освобождение inner reader** (`LobDataReader.Dispose` + `CommandReaderOwner.Dispose`) —
  безвредно (идемпотентно по ADO.NET). Триггер: следующая правка владения ридером.
- **`GetSchemaTable()` не делегируется** во внутренний ридер. Триггер: первый потребитель
  schema-метаданных.
- **DRY: четыре почти идентичные in-memory-ветки** (`ToStream`/`ToTextReader` sync/async) — разные
  sentinel'ы и типы. Триггер: пятый терминал или новая настройка вида `writable`.
- **`(object[])parameters` на async-пути** — корректный идиом; чистый фикс (расширение публичного
  `ExecuteScalarAsync` до `object?[]`) — отдельное cross-cutting изменение. Триггер: следующий проход
  по nullable-аннотациям.
- **Ограничение (не TODO):** многоколоночный `ToDataReader` на in-memory остаётся
  `NotSupportedException` (у in-memory нет `DbDataReader`); зафиксировано в публичных доках EN+RU.

## Deferred/accepted — #101 (2026-09-27)

Принятые (осознанно оставленные) решения цикла #101; не блокируют отгрузку, но фиксируются с
триггером на пересмотр. Status-файл `docs/specs/status/lob-alias-streaming-1.md` удалён при коммите.

- **Enumerator owns reader/command.** Транзиентный построчный enumerator берёт на себя владение и
  освобождает per-call `DbCommand`/`DbDataReader`; вызывающий освобождает только per-row
  `Stream`/`TextReader`. Триггер пересмотра: тест ресурсов на early-break итерации.
- **Нет финализатора у `LobStream`/`LobTextReader`.** Освобождение только явное/через `Dispose`;
  полагаемся на владельца (`CommandReaderOwner`) и документированный контракт. Триггер: утечка при
  отказе от явного `Dispose` в потребителе.
- **Sync `Dispose` внутри `DisposeAsync`.** Драйверное освобождение синхронное; `DisposeAsync`
  делегирует `Dispose`. Триггер: async-only драйвер или перф-замечание на больших объёмах.
- **Raw mapper path всегда буферизуется.** У raw/`WithSql`-маппера нет флага `Streaming`; потоковая
  проекция через raw-путь не поддержана (fail-closed). Триггер: запрос на raw-стриминг.
- **Streaming-план исключён из кэша локально.** Хэш/ключ плана для него вычисляется, но сам план в
  кэш не кладётся (`storeInCache: false`). Это оптимизационная возможность на будущее — кэшировать
  стриминговый shape отдельно. Триггер: горячий стриминговый сценарий.
- **Нет in-memory ветки для терминалов `Stream`/`TextReader`-проекции.** In-memory отклоняет
  построчный стриминг (`NotSupportedException`), как и прочие non-`SupportsSequentialAccess`
  провайдеры. Триггер: требование паритета in-memory (симметрично срезу A скалярного пути).
- **NRT `= null!` на `AsStream`/`AsTextReader`.** Члены индексатора аннотированы non-null без
  runtime-инициализации (throws-маркеры). Триггер: ужесточение аннотаций/анализаторов.
- **Отсутствует `<remarks>` про NULL/empty.** XML-doc покрывает контракт времени жизни и
  `NotSupportedException`, но не описывает отдельно NULL/пустое значение. Триггер: первый вопрос
  потребителя или правка доков.

**Контракт времени жизни (сводно).** Per-row `Stream`/`TextReader` в `ToAsyncEnumerable` валиден
**только до следующего `MoveNext`**; после — поведение undefined/зависит от провайдера, и
гарантируется лишь безопасность (значение не должно использоваться и обязано быть освобождено внутри
итерации). Контракт зафиксирован в публичных доках EN+RU и XML-doc аксессоров.

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

> **Цикл 2 (2026-09-27, experiment-only):** вопросы №1 и №2 закрыты на реальных драйверах.
> **Цикл 3 (2026-09-27):** оба закрытия **реализованы** — MySQL/MariaDB остаются unsupported,
> SQLite выбран путь trailing `rowid` (не чанковый `GetBytes`). Логи и вердикт — `docs/specs/status/lob-streaming-2.md`,
> верификация реализации — `docs/specs/status/lob-streaming-3.md`.

1. ~~Поддерживает ли `MySqlConnector.GetStream`/`GetTextReader` потоково и нужен ли ему
   `SequentialAccess`?~~ **Закрыт: NEGATIVE (2026-09-27).** `MySqlConnector 2.6.2` буферизует значение
   целиком: `GetStream` = `MemoryStream` поверх буферизованной строки, `GetTextReader` =
   `StringReader(GetString())`, `CommandBehavior.SequentialAccess` — no-op; измеренный alloc ratio
   1→8 МиБ ≈ **8.0** на `mysql:8.4` и `mariadb:11.4`. MySQL/MariaDB остаются `NotSupportedException`.
   Новый триггер: драйвер получает настоящий стриминг, либо появляется server-side chunking design
   (`SUBSTRING`-чанки).
2. ~~SQLite: добавлять `rowid` в SELECT автоматически или всегда идти чанковым `GetBytes`?~~
   **Закрыт (2026-09-27): trailing `rowid`.** На `Microsoft.Data.Sqlite 10.0.12` запрос
   `SELECT payload, rowid FROM t` + `GetStream(0)` возвращает настоящий `SqliteBlob` с памятью
   O(buffer) (allocated флэт ~2.9 КБ на 1/8/32 МиБ против `MemoryStream` ≈ размер без `rowid`);
   `view`/`WITHOUT ROWID` падают fail-closed (`SqliteException: no such column: rowid`).
   Проекция-шов сохраняет payload в ordinal 0; чанковый `GetBytes` **не выбран**. **Реализовано (цикл 3).**
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

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **нужен пересмотр — 1 блокер (L1)** + обязательные решения L2/L3/L4.

- **[TYPE]/[KISS] 🔴** Терминал повешен на `EntityBuilder<TResult>` (`:86-92`), но `EntityBuilder.Select` возвращает `QueryCommand<TResult>` (`src/nextorm.core/Builders/EntityBuilder.cs:170`), поэтому пример `...Select(x => x.Data).ToStream()` (`:67-70`) на этом ресивере не разрешается, а `EntityBuilder<TResult>`-перегрузка бессмысленна (сущность не бывает `byte[]`/`string`). Fix: терминалы только на `QueryCommand<T>` с compile-time-ограничением — `ToStream(this QueryCommand<byte[]>, …)`, `ToTextReader(this QueryCommand<string>, …)`.
- **[TYPE]/[LSP] 🟡** «поток владеет `DbCommand`» (`:104-105`) конфликтует с кэшированным планом: `DbPreparedQueryCommand.DbCommand` (`DataContext/Cache/DbPreparedQueryCommand.cs:21`) — один на форму запроса; держать открытой до `Dispose` = блокировать повторное исполнение формы на контексте. Fix: per-call команда для LOB-терминала либо явно задокументировать эксклюзивность владения.
- **[OCP]/[PERF] 🟡** `Behavior` — `public readonly`, выставляется только в ctor (`DbPreparedQueryCommand.cs:16,68-69`), «дополнить» после нельзя (`:132-134`); переиспользуя план-кэш, тот же SQL-shape может быть закэширован буферизованным (`Behavior=0`), и LOB-терминал получит буфер. Fix: дискриминатор стриминга в ключе плана, `Behavior` строится в `QueryPlanner` (`QueryPlanner.cs:437-439`).
- **[DRY]/[PERF] 🟡** «терминал не идёт через `RowMapperFactory`» (`:102`), но `GetPreparedQueryCommand` всё равно компилирует `Func<IDataRecord,TResult>` для любого не-`DocumentMode`/не-`SingleRow&&OneColumn` (`QueryPlanner.cs:401-403`) → лишний `Expression.Compile()` + запись в `MapperCache`. Fix: no-mapper режим (аналог `DocumentMode`).
- **[OCP]/[ISP] 🟡** `SupportsSequentialAccess` (+`SupportsGetStream`/`SupportsGetTextReader`) — поведение ADO.NET-драйвера, не SQL-диалекта; F12 уже помечал `SupportsCommandBehaviorSingleRow` кандидатом на переезд в ось исполнения (`docs/specs/design/solid-review.md:743-746`); evidence F12 устарел (1727 строк, ~103 метода, ≥63 свойства в `ISqlDialect.cs`). Deferred с триггером (ревизия F12 / driver-behavior seam).
- **[ISP]/[KISS] 🟡** Новый публичный `ILobReader` «если нужна отдельная роль» (`:210`) при одном потребителе — нарушение инварианта 1. Fix: не заводить (или `internal`).
- **[PERF] ℹ️** Sync-перегрузка `ToStream(..., params object[] @params)` (`:86-87`) аллоцирует массив; sync-терминалы проекта используют `params ReadOnlySpan<object?>` (`src/nextorm.core/Builders/EntityBuilderExtensions.cs:181`). Fix: `params ReadOnlySpan<object?>`.
- **[OCP] ℹ️** LOB-чтение идёт напрямую, минуя `ResultSetEnumerator`, поэтому `IQueryInterceptor.CommandExecuting/Executed/Failed` не поднимутся, хотя гайд обещает покрытие (`docs/guide/27-interceptors.md`). Fix: поднимать события через `InterceptorHooks` (`DataContext/DataContextDependencies.cs:86-124`).
- **ℹ️** Устаревшие якоря: `SelectExpression.cs:79-82` → `:119-122`; `:111-116` → `:155-162`; `QueryPlanner.cs:132-137` → `:437-439`.

> Обновление 2026-09-26: блокер L1 (терминал на `EntityBuilder`) снят — терминалы только на
> `QueryCommand<T>`. Конфликт владения с кэшированным `DbCommand` снят общим владельцем reader'а фазы 0
> #70 (per-call `DbCommand`). `Behavior` строится в `QueryPlanner` и входит в ключ плана. Sync-терминалы
> принимают `params ReadOnlySpan<object?>`.
