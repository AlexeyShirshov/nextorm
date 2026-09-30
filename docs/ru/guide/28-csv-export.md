# Потоковая выдача результатов запроса в CSV

> Пишите результат `Select` в предоставленный вызывающим `Stream` как CSV по RFC 4180, строка за строкой, не материализуя набор результатов.

**Предварительные требования:** [Запросы и проекции](../querying/index.md) · [Проекции](../querying/01-projections.md) · [Потоковое чтение больших объектов (BLOB/CLOB)](26-large-objects.md) · [Ограничения](../advanced/limitations.md)

## Обзор

`WriteCsv`/`WriteCsvAsync` превращают запрос в CSV и пишут его прямо в предоставленный вами `Stream`. Строки читаются из провайдерского reader'а и форматируются по одной: набор результатов **не** материализуется в список, экземпляр `TResult` на строку не создаётся, а построчный путь читает каждую поддерживаемую колонку без боксинга. Это касается и числовых колонок SQL Server: терминал CSV читает их через storage-типизированный getter провайдера (hook `MapTypedColumnExpression`, получающий фактический тип поля reader'а) и конвертирует типизированным `Convert.To<T>` — без `GetValue` и без `Convert.ChangeType(object)`. Колонка, чей storage-тип неизвестен или не поддаётся конвертации, отклоняется с `NotSupportedException` до любого вывода. Память — O(строки), поэтому запрос на миллионы строк можно отдать в файл или в HTTP-ответ без буферизации; исключение — бинарные (`byte[]`) колонки: каждая читается и кодируется в Base64 как поле целиком за строку, поэтому память ограничена наибольшим полем/строкой, а не фиксированным буфером (см. [Ограничения](#ограничения)).

Это потоковый терминал для **табличного** вывода. Однозначные терминалы [`byte[]`/`string`](26-large-objects.md) отдают одно большое значение; терминалы CSV отдают все колонки и строки проекции.

## Терминалы

Sync-перегрузки принимают параметры как `params ReadOnlySpan<object?>`; async-перегрузки — как `params object?[]`:

```csharp
public static void WriteCsv<TResult>(this QueryCommand<TResult> command, Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default, params ReadOnlySpan<object?> parameters);
public static Task WriteCsvAsync<TResult>(this QueryCommand<TResult> command, Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default, params object?[] parameters);
```

Те же два терминала есть прямо на построителе запроса, поэтому проекции целой сущности явный `Select` не нужен:

```csharp
public static void WriteCsv<TEntity>(this EntityBuilder<TEntity> builder, Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default, params ReadOnlySpan<object?> parameters);
public static Task WriteCsvAsync<TEntity>(this EntityBuilder<TEntity> builder, Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default, params object?[] parameters);
```

Форма `QueryCommand<TResult>` — это тип, возвращаемый `Select`; форма `EntityBuilder<TEntity>` форвардит в неё. Обе готовят **свежую per-call команду с `storeInCache: false`**, поэтому экспорт CSV не читает и не пишет кэш планов и никогда не переиспользует общую буферизованную команду той же формы запроса.

## Использование

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

// Поверхность QueryCommand<TResult>: проецируйте колонки, которые нужны в файле.
await using var file = File.Create("orders.csv");
ctx.From<Order>()
    .Where(x => x.Total > 100)
    .Select(x => new { x.Id, x.CustomerName, x.Total, x.CreatedAt })
    .WriteCsv(file);

// Поверхность EntityBuilder<TEntity>: вся сущность, с параметром и токеном.
await using var report = File.Create("report.csv");
await ctx.From<Order>()
    .Where(x => x.CreatedAt >= from)
    .WriteCsvAsync(report, null, cancellationToken, from);
```

## Опции

```csharp
public sealed class CsvStreamOptions
{
    public bool IncludeHeader { get; set; } = true;
    public char Delimiter { get; set; } = ',';
    public string NullMarker { get; set; } = "\\N";
    public bool ExcelMode { get; set; }
    public Func<object?, string?>? ValueTransform { get; set; }
}
```

Передайте `null` (или опустите аргумент) для значений по умолчанию либо экземпляр, чтобы изменить диалект. Опции читаются один раз, когда экспорт начинается:

```csharp
ctx.From<Order>()
    .Select(x => new { x.Id, x.CustomerName })
    .WriteCsv(file, new CsvStreamOptions
    {
        Delimiter = ';',
        IncludeHeader = false,
        NullMarker = "NULL",
        ExcelMode = true,
        ValueTransform = v => v is DateTime d ? d.ToString("yyyy-MM-dd") : v?.ToString(),
    });
```

| Опция | По умолчанию | Значение |
|---|---|---|
| `IncludeHeader` | `true` | выводит строку заголовка из имён проекции перед строками данных |
| `Delimiter` | `,` | разделитель полей |
| `NullMarker` | `\N` (два символа: обратный слэш, `N`) | литеральный текст, выводимый для SQL `NULL` |
| `ExcelMode` | `false` | включает защиту от инъекции формул |
| `ValueTransform` | `null` | построчное преобразование значений; `null` сохраняет box-free форматирование по умолчанию |

Остальной диалект остаётся фиксированным (см. [таблицу диалекта](#диалект)): UTF-8 без BOM, экранирование по RFC 4180, терминаторы CRLF и invariant-форматы значений.

### NULL, пустые значения и маркер

* **SQL NULL → `NullMarker` дословно.** SQL `NULL`/`DBNull` пишется ровно как `NullMarker`, без кавычек и никогда не проходит через `ValueTransform`.
* **Пустая строка → пустое поле.** Пустой `string`, не равный `NULL` (или пустой `byte[]`), пишется как пустое поле, поэтому читатель отличает отсутствующее значение от пустого.
* **Текст, равный маркеру, принудительно квотируется.** Не-`NULL` значение, чей форматированный текст в точности равен `NullMarker`, оборачивается в `"` (`"\N"`), чтобы его нельзя было прочитать обратно как NULL.
* **Маркер проверяется до любого вывода.** `NullMarker` должен быть непустым и не содержать `Delimiter`, `"`, CR или LF; нарушающее значение отклоняется до записи заголовка (и любой строки данных).

### Excel-защита

При `ExcelMode = true` поле, чей форматированный текст начинается с `=`, `+`, `-` или `@`, получает префикс из апострофа (`'`), чтобы табличный процессор воспринял его как текст, а не как формулу. Защита срабатывает **после** `ValueTransform` и **до** экранирования по RFC 4180, поэтому апостроф становится частью экранированного поля (оказывается внутри кавычек), и преобразование не может протащить формулу мимо защиты. Она применяется к форматированному тексту каждой колонки, включая отрицательные числовые значения.

### Преобразование значений

`ValueTransform` получает каждое не-`NULL` значение (упакованное в `object`) и возвращает текст, заменяющий форматирование по умолчанию, либо `null`, чтобы вывести маркер NULL. Он выполняется до Excel-защиты и до экранирования CSV; SQL NULL его никогда не вызывает. Путь по умолчанию (без преобразования) остаётся box-free — бокинг происходит только когда задан `ValueTransform`.

## Диалект

| Аспект | Поведение |
|---|---|
| Кодировка | UTF-8 **без** byte-order mark |
| Заголовок | одна строка перед данными, когда `IncludeHeader` равно `true` (по умолчанию); имена берутся из имён свойств проекции, с фолбэком `Column1`, `Column2`, … для безымянных членов |
| Разделитель строк | CRLF (`\r\n`) |
| Разделитель полей | `options.Delimiter`, по умолчанию `,` |
| Экранирование | RFC 4180: поле оборачивается в `"`, если содержит разделитель, `"`, CR или LF; внутренняя `"` удваивается (`""`) |
| `null` / `DBNull` | литеральный `options.NullMarker` (по умолчанию `\N`), дословно и без кавычек |
| Пустая строка / пустой `byte[]` | пустое поле (без кавычек) |
| Данные, равные маркеру | принудительно квотируются (`"\N"`), чтобы их нельзя было прочитать как NULL |
| Excel-защита | при `ExcelMode = true` значение, начинающееся с `=`/`+`/`-`/`@`, получает ведущий `'` (после `ValueTransform`, до экранирования) |
| `ValueTransform` | необязательное построчное преобразование не-`NULL` значений; выполняется до Excel-защиты и экранирования |
| Культура | `CultureInfo.InvariantCulture` для каждого значения |
| `DateTime` / `DateTimeOffset` | формат round-trip `"O"` |
| `Guid` | формат `"D"` |
| `bool` | `true` / `false` |
| `byte[]` | строка Base64 |
| Числа | форматирование по умолчанию в invariant-культуре |

## Провайдеры

| Провайдер | `WriteCsv` / `WriteCsvAsync` |
|---|---|
| PostgreSQL | поддерживается |
| SQL Server | поддерживается |
| SQLite | поддерживается |
| MySQL / MariaDB | поддерживается |
| ClickHouse | поддерживается |
| In-memory | `NotSupportedException` |

В отличие от LOB-терминалов экспорт CSV не требует ни поддержки sequential access, ни локатора `rowid`, поэтому работает на каждом реляционном провайдере. У провайдера in-memory нет `DbDataReader`, из которого можно стримить, и он отклоняет терминал через `NotSupportedException`, точно как `ToDataReader`; буферизованного фолбэка в памяти нет — материализуйте запрос и пишите CSV сами, если это нужно.

## Запись в HTTP-ответ или файл

Терминал пишет в `destination` и **никогда не закрывает и не освобождает его**: временем жизни потока владеет вызывающий. Именно поэтому потоку можно отдать тело HTTP-ответа или `FileStream` и сохранить контроль.

```csharp
// Minimal API: стримим запрос прямо в тело ответа.
app.MapGet("/orders.csv", async (HttpContext http, CancellationToken ct) =>
{
    using var ctx = new DataContextBuilder()
        .UsePostgres(connectionString)
        .CreateDataContext();

    http.Response.ContentType = "text/csv; charset=utf-8";
    http.Response.Headers.ContentDisposition = "attachment; filename=orders.csv";

    await ctx.From<Order>()
        .Select(x => new { x.Id, x.CustomerName, x.Total, x.CreatedAt })
        .WriteCsvAsync(http.Response.Body, cancellationToken: ct);
});
```

Телом `Response.Body` владеет ASP.NET Core и освобождает его после возврата обработчика; терминал — нет. Та же форма работает и для файла, который вызывающий открывает и освобождает:

```csharp
await using var file = File.Create("orders.csv");   // файлом владеет вызывающий
await ctx.From<Order>().WriteCsvAsync(file);
```

## Владение, отмена и ошибки

* **Владение потоком.** Терминал никогда не закрывает `destination` — ни при успехе, ни при отмене, ни при ошибке. Освобождайте поток сами, если открывали его.
* **Владение reader'ом/командой.** Терминал открывает per-call команду и reader и освобождает оба при возврате (или исключении); [`DataContext`](xref:NextORM.Core.DataContext) остаётся живым и пригодным.
* **Отмена.** Токен учитывается при чтении строк и, на асинхронном пути, при записи в destination; отменённый экспорт бросает `OperationCanceledException`, не закрывая поток назначения.
* **Ошибки записи.** Исключение из потока назначения (оборванный сокет, заполненный диск) пробрасывается после освобождения reader'а и команды; поток назначения по-прежнему остаётся открытым.

## Ограничения

* **Контекст in-memory — `NotSupportedException`.** `DbDataReader` отсутствует, поэтому терминал падает fail-closed вместо буферизации; см. [Провайдеры](#провайдеры).
* **Ленивые источники временных таблиц — `NotSupportedException` до любого вывода.** Запрос, читающий источник, созданный через [`AsTempTable`](18-create-table-as.md#ленивые-временные-таблицы-astemptable), — не одна инструкция: нужен батч `DROP` + `CREATE TEMPORARY TABLE ... AS SELECT` + чтение на одной сессии, а терминал CSV не умеет его стримить. Терминал падает fail-closed с `NotSupportedException` до записи заголовка (и любой строки данных) и никогда не выполняет батч, поэтому в потоке назначения не остаётся частичного файла; материализуйте запрос (например, `ToList`/`ToListAsync`) и пишите строки сами.
* **Неподдерживаемые проекции — отказ до записи любой строки.** Колонка проекции, у которой нет единой скалярной CSV-формы либо чей storage-тип неизвестен и не поддаётся конвертации, отклоняется сразу: вложенная сущность или коллекция, массив, отличный от `byte[]`, кортеж, словарь, произвольный объект, а также колонка, чей маппинг требует объектного конвертера или фолбэка на `GetValue`. Отказ происходит до записи заголовка, поэтому в потоке назначения не остаётся частичного файла.
* **Бинарные (`byte[]`) поля читаются целиком — память ограничена наибольшим полем/строкой.** Колонка `byte[]` материализуется типизированным `GetFieldValue<byte[]>` и затем кодируется в Base64 для строки (сам кодировщик идёт 3-байтными чанками), поэтому пиковая память — не фиксированный буфер: экспорт строк с очень крупными BLOB держит в памяти как минимум одно поле целиком. Chunked LOB streaming — запись бинарного поля чанками из sequential-access reader'а — будущий срез (триггер: спрос на экспорт крупных бинарных колонок). Чтобы сегодня стримить одно большое значение с памятью O(буфера), используйте однозначные [`byte[]`/`string` LOB-терминалы](26-large-objects.md).
* **Инъекция формул в табличные процессоры (CWE-1236) — опциональна.** Защита по умолчанию выключена (`ExcelMode = false`), поэтому значения полей пишутся дословно после экранирования по RFC 4180, если её не включить; одно экранирование не нейтрализует значение, первый символ которого — `=`, `+`, `-`, `@` либо ведущий таб или CR. Если любое из экспортируемых значений может прийти из недоверенного ввода, включите `ExcelMode = true`, чтобы добавить префикс из одинарной кавычки (или нейтрализуйте значение сами / отключите вычисление формул в приложении-потребителе).

## См. также

* [Проекции](../querying/01-projections.md)
* [Потоковое чтение больших объектов (BLOB/CLOB)](26-large-objects.md)
* [Сырой SQL](12-raw-sql.md)
* [Краткий справочник API](../advanced/api-reference.md)
* [Ограничения и возможности вне области охвата](../advanced/limitations.md)

---

Источник: `src/nextorm.core/Query/QueryCommandExtensions.cs`, `src/nextorm.core/Builders/EntityBuilderExtensions.cs`, `src/nextorm.core/Query/CsvStreamOptions.cs`.
