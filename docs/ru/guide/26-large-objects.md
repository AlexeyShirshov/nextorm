# Потоковое чтение больших объектов (BLOB/CLOB)

> Читайте одно большое бинарное или текстовое значение как `Stream`/`TextReader`, не загружая его целиком в managed-память.

**Предварительные требования:** [Запросы и проекции](../querying/index.md) · [Проекции](../querying/01-projections.md) · [PostgreSQL](provider-specific/postgresql.md) · [SQL Server](provider-specific/sqlserver.md) · [Ограничения](../advanced/limitations.md)

## Обзор

По умолчанию проекция `byte[]`/`string` материализуется целиком: row reader вызывает `GetValue`/`GetString`, и всё значение попадает в managed-массив или строку. Для колонки с файлом, изображением или документом в несколько мегабайт это лишняя аллокация, которая может исчерпать память. Терминалы LOB вместо этого открывают потоковые аксессоры провайдера (`DbDataReader.GetStream`/`GetTextReader`) и возвращают объект, который вы читаете постепенно.

Для целого набора результатов, а не одной LOB-колонки, ту же идею применяет к строкам терминал потоковой записи JSON: [`WriteJson`](14-json.md#потоковая-запись-json-в-stream) / `WriteJsonAsync` записывают проекцию `Select` запроса в принадлежащий вызывающему `Stream` с живой памятью O(buffer) и никогда не закрывают приёмник. См. [Потоковая запись JSON в Stream](14-json.md#потоковая-запись-json-в-stream).

## Терминалы

Sync-перегрузки принимают параметры как `params ReadOnlySpan<object?>`:

```csharp
public static Stream ToStream(this QueryCommand<byte[]> command, params ReadOnlySpan<object?> parameters);
public static Stream ToStream(this QueryCommand<byte[]> command, CancellationToken cancellationToken, params ReadOnlySpan<object?> parameters);

public static TextReader ToTextReader(this QueryCommand<string> command, params ReadOnlySpan<object?> parameters);
public static TextReader ToTextReader(this QueryCommand<string> command, CancellationToken cancellationToken, params ReadOnlySpan<object?> parameters);
```

Async-перегрузки принимают параметры как `params object?[]` (открытие асинхронно; перегрузка с `CancellationToken` отменяет и само открытие):

```csharp
public static Task<Stream> ToStreamAsync(this QueryCommand<byte[]> command, params object?[] parameters);
public static Task<Stream> ToStreamAsync(this QueryCommand<byte[]> command, CancellationToken cancellationToken, params object?[] parameters);

public static Task<TextReader> ToTextReaderAsync(this QueryCommand<string> command, params object?[] parameters);
public static Task<TextReader> ToTextReaderAsync(this QueryCommand<string> command, CancellationToken cancellationToken, params object?[] parameters);
```

Терминалы — extension-методы на [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1), том самом типе, который возвращает [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})), поэтому запрос должен сначала спроецировать одну колонку `byte[]` или `string`. Они создаются с `storeInCache: false`: LOB-команда является per-call и никогда не переиспользует общую буферизованную команду той же формы запроса.

## Использование

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

// BLOB
await using var stream = ctx.From<BinaryEntity>()
    .Where(x => x.Id == 1)
    .Select(x => x.Payload)
    .ToStream();                         // reader открыт с CommandBehavior.SequentialAccess

var buffer = new byte[81920];
int read;
while ((read = await stream.ReadAsync(buffer)) > 0)
    await destination.WriteAsync(buffer.AsMemory(0, read));

// CLOB
using var reader = ctx.From<Document>()
    .Where(x => x.Id == 1)
    .Select(x => x.Body)
    .ToTextReader();

var chars = new char[8192];
int n;
while ((n = await reader.ReadAsync(chars, 0, chars.Length)) > 0)
    _ = chars[n];
```

Асинхронная форма открывает команду на базе; свои параметры передавайте в завершающем списке `params`:

```csharp
Stream stream = await ctx.From<BinaryEntity>()
    .Where(x => x.Id == id)
    .Select(x => x.Payload)
    .ToStreamAsync(cancellationToken, id);
```

## Именованные колонки и построчный стриминг

Потоковые аксессоры доступны и на поверхности именованных колонок (`From("table")`), и их можно
читать построчно в проекции `IAsyncEnumerable`. Член проекции `Stream`/`TextReader` открывается
лениво из sequential-access reader'а и **валиден только до перехода перечислителя к следующей
строке** (`MoveNext`): нижележащим reader'ом и его per-call командой владеет перечислитель, поэтому
значение нужно полностью прочитать внутри тела цикла и не сохранять за пределами шага. Освобождайте каждый построчный stream/reader до следующей итерации: это освобождает per-row handle провайдера (обязательно для SQLite, где локатор `rowid` удерживает statement, пока значение не освобождено).

После `MoveNext` handle **использовать нельзя**: его поведение не определено и зависит от
провайдера. PostgreSQL может вернуть байты **следующей** строки, SQL Server бросает
`ObjectDisposedException`, а SQLite возвращает конец потока на конце blob'а. Единственная гарантия —
это safety-свойство: устаревший handle никогда не возвращает данные наблюдённой строки. Поэтому
считайте значение недействительным в момент перехода перечислителя и не полагайтесь на конкретный
сценарий отказа.

Аксессоры именованных колонок —
[`TableAlias.GetStream`](xref:NextORM.Core.TableAlias.GetStream(System.String)) /
[`TableAlias.GetTextReader`](xref:NextORM.Core.TableAlias.GetTextReader(System.String)), а также
члены индексатора [`TableColumn.AsStream`](xref:NextORM.Core.TableColumn.AsStream) /
[`TableColumn.AsTextReader`](xref:NextORM.Core.TableColumn.AsTextReader). Потоковый член обязан быть
**последним** в проекции, чтобы sequential-access reader дошёл до LOB-колонки после всех скалярных:

```csharp
await foreach (var row in ctx.From("documents")
    .Where(t => t.GetInt32("id") == id)
    .Select(t => new { Id = t.GetInt32("id"), Data = t.GetStream("data") })
    .ToAsyncEnumerable(cancellationToken))
{
    // Поток валиден только до следующего MoveNext: прочитайте и освободите его до следующей итерации.
    await using var data = row.Data;
    await data.CopyToAsync(destination, cancellationToken);
}
```

Строковая проекция, содержащая потоковый член, готовится заново с
`CommandBehavior.SequentialAccess` и никогда не пишется в кэш планов, поэтому обычная буферизованная
проекция той же формы сохраняет свой план. В SQLite к таким строкам тоже добавляется локатор
`rowid`, поэтому источник обязан быть rowid-таблицей. Провайдер без поддержки sequential access
(MySQL/MariaDB, ClickHouse) бросает `NotSupportedException` при выполнении запроса; используйте там
буферизованную проекцию `byte[]`/`string`.

## Провайдеры

| Провайдер | `ToStream` / `ToTextReader` |
|---|---|
| PostgreSQL | поддерживается (`bytea` / `text`) |
| SQL Server | поддерживается (`varbinary(max)` / `nvarchar(max)`; потоковый режим — `CommandBehavior.SequentialAccess`) |
| SQLite | поддерживается (`blob` / `text`; источник должен раскрывать `rowid`) |
| MySQL / MariaDB | `NotSupportedException` |
| ClickHouse | `NotSupportedException` |
| In-memory | поддерживается (`MemoryStream`/`StringReader` над единственным материализованным значением; см. [In-memory](#in-memory)) |

Стриминг реализован для **PostgreSQL, SQL Server и SQLite** в этом выпуске. Терминал стриминга — opt-in: обычная буферизованная проекция `byte[]`/`string` продолжает работать у каждого провайдера, а запись LOB — вне области охвата. MySQL/MariaDB и ClickHouse отклоняют терминал через `NotSupportedException`, в сообщении которого назван провайдер. Провайдер in-memory тоже поддерживает скалярные терминалы, но стримить не из чего — `DbDataReader` нет, поэтому он возвращает обычный BCL-объект над материализованным значением; см. [In-memory](#in-memory).

Многоколоночный терминал [`ToDataReader`](#многоколоночность-todatareader) доступен на PostgreSQL и SQL Server. SQLite его отклоняет, потому что его потоковая проекция всегда несёт локатор `rowid`; MySQL/MariaDB и ClickHouse отклоняют его, потому что у них нет поддержки sequential access; провайдер in-memory отклоняет его, потому что у него вообще нет `DbDataReader`:

| Провайдер | `ToDataReader` / `ToDataReaderAsync` |
|---|---|
| PostgreSQL | поддерживается |
| SQL Server | поддерживается |
| SQLite | `NotSupportedException` (локатор `rowid`) |
| MySQL / MariaDB | `NotSupportedException` |
| ClickHouse | `NotSupportedException` |
| In-memory | `NotSupportedException` |

У SQLite нет собственного локатора строк, поэтому `Microsoft.Data.Sqlite` возвращает настоящий потоковый `SqliteBlob` только когда запрос выбирает ещё и `rowid`. Диалект добавляет собственный завершающий столбец-локатор — [`ISqlDialect.LobLocatorColumn`](xref:NextORM.Core.ISqlDialect.LobLocatorColumn), `"rowid"` в SQLite и `null` в PostgreSQL/SQL Server, — так что payload остаётся на позиции `0` и не является частью пользовательской проекции. Поэтому источник запроса обязан быть обычной rowid-таблицей: на `view` или таблице `WITHOUT ROWID` команда падает fail-closed с сырым `Microsoft.Data.Sqlite.SqliteException: no such column: rowid` — буферизованного фолбэка нет.

### In-memory

У [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) нет `DbDataReader`, поэтому скалярные LOB-терминалы не открывают провайдерский reader. Они выполняют запрос к in-memory источнику как обычно и оборачивают единственное спроецированное значение в обычный BCL-объект, принадлежащий вызывающему: `ToStream`/`ToStreamAsync` возвращают `MemoryStream` над `byte[]`, а `ToTextReader`/`ToTextReaderAsync` — `StringReader` над `string`. Проекция по-прежнему обязана быть ровно одной колонкой `byte[]`/`string`.

Читается только первая строка. Пустой результат и `NULL` в первом значении оба возвращают `Stream.Null`/`TextReader.Null` — скалярный API in-memory не различает «нет строки» и «строка с `NULL` LOB». На контексте нечего освобождать, и контексту не нужно оставаться живым, пока читается возвращённый объект: это обычный `MemoryStream`/`StringReader`, который не владеет ни reader'ом, ни командой. Поскольку значение уже материализовано, выигрыша O(буфера) в памяти нет — всё значение и так находится в managed-памяти.

`ToDataReader`/`ToDataReaderAsync` на провайдере in-memory по-прежнему не поддерживаются (`NotSupportedException`): отдавать нечего — `DbDataReader` отсутствует.

### Формы запросов в SQLite

Локатор SQLite можно добавить только к **одноисточниковой проекции, раскрывающей `rowid`**. Запросы, соединяющие несколько источников, а также `DISTINCT`/`DISTINCT ON`, `UNION`, `GROUP BY` или агрегат, **не поддерживаются**; они могут проявиться ошибкой драйвера SQLite, а не аккуратным ранним отказом. Для одноколоночной проекции переопределение сырым [`WithSql`](xref:NextORM.Core.EntityExtensions.WithSql``1(NextORM.Core.EntityBuilder{``0},System.String)) отклоняется с `NotSupportedException`, потому что `rowid`-локатор нельзя безопасно добавить к SQL, поставляемому вызывающим кодом; сырая проекция из двух и более колонок отклоняется обычным `InvalidOperationException`.

## Ровно одна колонка

Терминал читает колонку с порядковым номером `0` и требует **ровно одну** колонку: ноль или несколько колонок бросают `InvalidOperationException`. Единственная колонка должна быть корректным `byte[]`/`string`; при одном столбце неподходящего типа ошибку вернёт провайдерский `GetStream`/`GetTextReader` (конкретный тип исключения зависит от драйвера). Чтобы прочитать несколько колонок — или несколько строк — через `DbDataReader`, используйте escape hatch [`ToDataReader`](#многоколоночность-todatareader) ниже.

## Многоколоночность: `ToDataReader`

Когда в проекции больше одной колонки или нужны все строки без материализации результата, используйте терминалы `ToDataReader`/`ToDataReaderAsync`. Они возвращают принадлежащий вызывающему [`DbDataReader`](https://learn.microsoft.com/dotnet/api/system.data.common.dbdatareader) поверх той же sequential-access команды:

```csharp
public static DbDataReader ToDataReader<TResult>(this QueryCommand<TResult> command, params ReadOnlySpan<object?> parameters);
public static DbDataReader ToDataReader<TResult>(this QueryCommand<TResult> command, CancellationToken cancellationToken, params ReadOnlySpan<object?> parameters);

public static Task<DbDataReader> ToDataReaderAsync<TResult>(this QueryCommand<TResult> command, params object?[] parameters);
public static Task<DbDataReader> ToDataReaderAsync<TResult>(this QueryCommand<TResult> command, CancellationToken cancellationToken, params object?[] parameters);
```

```csharp
using var reader = ctx.From<Document>()
    .Where(x => x.Id == id)
    .Select(x => new { x.Id, x.Body })
    .ToDataReader(id);

while (reader.Read())
{
    var id2 = reader.GetInt64(0);
    var body = reader.IsDBNull(1) ? null : reader.GetString(1);
}
```

Терминал **не** применяет проверку «ровно одна колонка»: он отдаёт provider-reader как есть, поэтому контракт [потоковой проекции](#владение-и-освобождение) по-прежнему действует:

* **Только вперёд, последовательный доступ.** Читайте колонки в порядке возрастания порядкового номера и не читайте колонку дважды; LOB-колонку нужно прочитать до любой последующей колонки.
* **Владение.** Возвращённый reader владеет нижележащим `DbDataReader` и per-call `DbCommand`; освободите его (`await using` на асинхронном пути), чтобы освободить и то и другое. Контекст остаётся живым и пригодным.
* **Отмена.** Токен отменяет открытие reader'а; он также связывается внутри `Read`/`ReadAsync` и `NextResult`/`NextResultAsync`.

`ToDataReader` **не поддерживается в SQLite**, потому что потоковая проекция SQLite всегда добавляет локатор `rowid`, и терминал выставил бы колонку, которую вызывающий не проецировал; он падает fail-closed с `NotSupportedException`. Для одной LOB-колонки в SQLite используйте `ToStream`/`ToTextReader`. MySQL/MariaDB и ClickHouse отклоняют терминал, потому что у них нет поддержки sequential access, а провайдер in-memory — потому что у него нет `DbDataReader`.

## `SequentialAccess` и кэш планов

Команда открывается с [`CommandBehavior.SequentialAccess`](https://learn.microsoft.com/dotnet/api/system.data.commandbehavior): провайдер отдаёт байты LOB по мере чтения, а не буферизует значение. Терминал стриминга готовит **свежую per-call команду** с `storeInCache: false`, поэтому он **вообще не трогает кэш планов** — ни читает, ни пишет запись. Буферизованный запрос той же SQL-формы сохраняет свой обычный план (без `SequentialAccess`) и не переиспользует LOB-команду. Наблюдаемая гарантия та же — буферизованная и потоковая формы никогда не разделяют план, — но механизм — это per-call подготовка, а не дискриминатор в ключе кэша планов.

## Владение и освобождение

Возвращённый `Stream`/`TextReader` **владеет** нижележащим `DbDataReader` и per-call `DbCommand`. Освобождение потока (предпочтительно через `await using` на асинхронном пути) освобождает и то и другое и возвращает соединение в пул; сам [`DataContext`](xref:NextORM.Core.DataContext) при этом **не** закрывается и может обслуживать дальнейшие запросы. Контекст должен оставаться живым, пока поток читается: открытие терминала на уже освобождённом контексте бросает `ObjectDisposedException`.

На провайдере in-memory ничего из этого не действует: возвращённый `MemoryStream`/`StringReader` не владеет ни reader'ом, ни командой, поэтому его освобождение ничего не освобождает на контексте, и контексту не нужно оставаться живым, пока объект читается (открытие самого терминала на уже освобождённом контексте по-прежнему бросает `ObjectDisposedException`).

## Память и асинхронность

Память — O(буфера): материализуется только запрошенный вами фрагмент, а не всё значение. `ToStreamAsync`/`ToTextReaderAsync` асинхронно открывают reader и команду, но сам геттер провайдера (`GetStream`/`GetTextReader`) синхронен — поэтому асинхронной является та часть чтения, которой вы управляете через `Stream.ReadAsync`/`TextReader.ReadAsync` на возвращённом объекте. Отмена учитывается при открытии и проявляется исключением, не оставляя утечек reader'а или команды.

На провайдере in-memory значение материализуется до того, как терминал обернёт его, поэтому память — O(значения), а не O(буфера), а `ReadAsync` на возвращённом `MemoryStream`/`StringReader` — это обычная асинхронная поверхность BCL без обращения к провайдеру.

## См. также

* [Проекции](../querying/01-projections.md)
* [Потоковая запись JSON в Stream](14-json.md#потоковая-запись-json-в-stream)
* [Сырой SQL](12-raw-sql.md)
* [Провайдер PostgreSQL](../providers/postgres.md) · [Провайдер SQL Server](../providers/sqlserver.md)
* [Ограничения и возможности вне области охвата](../advanced/limitations.md)

---

Источник: `src/nextorm.core/Query/QueryCommandExtensions.cs`, `src/nextorm.core/DataContext/CommandReaderOwner.cs`,
`src/nextorm.core/DataContext/LobDataReader.cs`,
`tests/nextorm.integration.tests/CommonTestSuite.Lob.cs` и тесты SQL-генерации/диалектов для `SupportsSequentialAccess`.
