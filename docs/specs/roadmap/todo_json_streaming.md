# TODO: Потоковая выдача JSON в `Stream` (`WriteJson` / `WriteJsonAsync`)

> Рабочий план (design RFC). Источник: обсуждение потоковой выдачи результата `Select` в виде
> JSON. **Не путать** с [`todo_streaming_lob.md`](todo_streaming_lob.md) (issue #27): там речь о
> чтении одного LOB-значения как `Stream`/`TextReader`, здесь — о сериализации строк результата в
> JSON без материализации `TResult`.

## Пункт и цель

- Фича: терминал над `QueryCommand<TResult>` (и `EntityBuilder<TEntity>`), который пишет результат
  `Select` как JSON прямо в выходной `Stream`, **не создавая `TResult` на строку и не бокся
  значения**.
- Критерий приёмки: аллокации O(1) на вызов (арендованный буфер + один `Utf8JsonWriter` + один раз
  скомпилированный делегат записи), а не O(размера результата); JSON совпадает с
  `JsonSerializer.Serialize(ToList())`; выходной `Stream` не закрывается; работет на всех
  провайдерах БД, у in-memory — задокументированный fallback.
- **Не путать**: `ToAsyncEnumerable` (`QueryCommand.TResult.cs:115`) уже отдаёт строки по одной, но
  каждая строка — это материализованный `TResult`; здесь строка не создаётся вообще.

## Почему это нужно (мотивация)

1. **Сквозной стрим от БД до HTTP.** Типовой сценарий ASP.NET Core — `Response.Body` с
   `text/event-stream`/JSON; сегодня приходится делать `ToListAsync()` (весь результат в память в
   виде объектов), а затем `JsonSerializer.SerializeAsync`. Двойная цена: объекты + второй проход.
2. **Ноль промежуточных объектов на строку.** `RowMapperFactory` компилирует
   `Func<IDataRecord, TResult>` (`RowMapperFactory.cs:63,120`) и материализует объект; для JSON
   объект не нужен — нужен только typed-доступ к колонкам, который уже описан в `SelectList`.
3. **Дешёвая база.** `SelectExpression` (`Expressions/SelectExpression.cs:20,22,26,78`) несёт
   `Index`/`PropertyName`/`PropertyType` и готовый `GetDataRecordMethod()`; `Utf8JsonWriter` входит
   в общий фреймворк `net10.0` (NuGet не нужен — STJ уже используется, например
   `src/nextorm.postgres/PostgresDataContext.cs`).

## Текущее состояние и разрыв

| Слой | Где | Чего не хватает |
|---|---|---|
| Проекция | `Query/QueryCommand.cs:191,219` | `SelectList`/`OneColumn` уже есть — источник метаданных для writer'а |
| Описание колонки | `Expressions/SelectExpression.cs:20,22,26,35,78` | `Index`/`PropertyName`/`PropertyType`/`Nullable`/`DefaultOnNull`/`GetDataRecordMethod` — всё есть |
| Материализация | `DataContext/RowMapperFactory.cs:24,63,120`, `DataContext/RowMaterializerBuilder.cs:21` | компилируется только `Func<IDataRecord,TResult>` (объект); JSON-писателя нет |
| Кэш маппера | `DataContext/MapperCache.cs:26` | `MapperCacheKey` есть; для writer'а нужен параллельный кэш (ключ + shape-опции) |
| Чтение | `DataContext/ResultSetEnumerator.cs:49,211,233`, `DataContext/QueryExecutor.cs:16,241,261` | ридер открывается только под `MapDelegate`; цикла «reader → JSON» нет |
| Роли | `DataContext/IDataContext.cs:17`, `Roles/IQueryExecutor.cs` | нет роли/метода для JSON-стрима; `IQueryExecutor` расширять нежелательно |
| In-memory | `DataContext/InMemoryRowMaterializer.cs:51,73` | нет `IDataRecord`; JSON возможен только fallback'ом |
| DB-side JSON | `DataContext/Dialect/ISqlDialect.cs` (`SupportsForJson`), `nextorm.sqlserver/SqlServerDialect.cs` | `FOR JSON`/`json_agg`/`JSONEachRow` есть как диалектный факт, но не как путь вывода |
| Публичный API | `Builders/EntityBuilderExtensions.cs` | терминала нет |

## Дизайн

### Фаза 1 — плоские скалярные `Select`

Покрывает `Select(x => new { x.Id, x.Name })`, DTO и сам `IEntity` с плоскими скалярами. Вложенные
проекции/коллекции — фаза 2, DB-side JSON — фаза 3.

**API.**

```csharp
// QueryCommand<TResult>
public Task WriteJsonAsync(Stream output, JsonStreamOptions? options = null,
                           CancellationToken cancellationToken = default, params object[] @params);
public void WriteJson(Stream output, JsonStreamOptions? options = null,
                      params ReadOnlySpan<object?> @params);

public sealed class JsonStreamOptions
{
    public JsonStreamMode Mode { get; set; } = JsonStreamMode.Array; // Array | NdJson
    public string? Root { get; set; }               // "items" -> {"items":[...]}
    public bool IgnoreNull { get; set; }            // пропускать свойства со значением NULL
    public bool WriteIndented { get; set; }
    public JsonNamingPolicy? PropertyNamingPolicy { get; set; }
}

public enum JsonStreamMode { Array, NdJson }
```

Перегрузки для `EntityBuilder<TEntity>` — в `Builders/EntityBuilderExtensions.cs` по образцу
`ToAsyncEnumerable` (`ToCommand()` + делегирование в `QueryCommand`).

**Контракт.** Выходной `Stream` — владение вызывающего: не закрывается и не `Dispose`-ится,
выполняется только `Flush`/`FlushAsync`. Ридер и команда освобождаются внутри метода. Требуется
явный `Select` (см. «Валидация»).

### Компилируемый writer (сердце фичи)

Из `QueryCommand.SelectList` (та же метаданка, что у маппера) собирается выражение и компилируется
делегат:

```csharp
public delegate void JsonRowWriter(IDataRecord record, Utf8JsonWriter writer);
```

По столбцу — аналог `RowMapperFactory.MapColumn` (`:24`), но вместо `Expression.New` — вызовы
`Utf8JsonWriter`; используем готовый `SelectExpression.GetDataRecordMethod()`:

| CLR тип | getter (через `GetDataRecordMethod`) | writer |
|---|---|---|
| int/short/byte/long/float/double/decimal | `GetInt32`/… | `WriteNumberValue` |
| bool | `GetBoolean` | `WriteBooleanValue` |
| string | `GetString` | `WriteStringValue` |
| Guid | `GetGuid` | `WriteStringValue(Guid)` |
| DateTime / DateTimeOffset / DateOnly / TimeOnly / TimeSpan | typed getter | `WriteStringValue` (ISO) |
| enum | typed getter | `WriteNumberValue` (default) |
| byte[] | `GetValue` (см. `:130-137`) | `WriteBase64String` |
| fallback (массивы/`Tuple`/`Map`/документ) | `GetValue` | `JsonSerializer.Serialize(writer, value)` |

Правила:
- `if (record.IsDBNull(i))` → `WriteNullValue()`, либо пропуск свойства при `IgnoreNull`; guard
  совпадает с `RowMapperFactory.MapColumn:32-54` (`Nullable`/`DefaultOnNull`);
- имя свойства = `PropertyName` → `PropertyNamingPolicy?.ConvertName(...)`; порядок = порядок
  `SelectList`;
- `IgnoreNull` и naming policy запекаются в делегат → входят в ключ кэша (shape).

### Стриминг и аллокации

- `Utf8JsonWriter` поверх внутреннего `StreamBufferWriter : IBufferWriter<byte>` (аренда
  `ArrayPool<byte>`, возврат в `finally`) — синхронный writer, память O(буфера).
- После каждой строки / при превышении порога — `await output.WriteAsync(buffer.WrittenMemory, ct)`
  и `Reset()`; `Flush()` перед выходом.
- Каркас: `Array` → `[` / `,` / `]` (+ обёртка `Root`); `NdJson` → одна строка = один объект + `\n`.

### Встраивание в исполнение

`QueryCommand.WriteJsonAsync` по образцу `ToAsyncEnumerable` (`QueryCommand.TResult.cs:115`):

1. `_dataContext.GetPreparedQueryCommand(this, streaming: true, nonStreamUsing: true, ct)`;
2. `JsonRowWriterFactory.GetOrBuild(queryCommand, sql, providerType, shapeOpts)` — делегат из
   `SelectList`;
3. если `_dataContext is IJsonStreamWriter w` → `w.WriteJsonAsync(prepared, rowWriter, output,
   options, @params, ct)`; иначе (in-memory) — fallback.

Новая **узкая роль** (не расширяем `IQueryExecutor`):

```csharp
public interface IJsonStreamWriter
{
    Task WriteJsonAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand,
        JsonRowWriter rowWriter, Stream output, JsonStreamOptions options,
        object[]? @params, CancellationToken cancellationToken);
}
```

- `QueryExecutor`/`DataContext` (DB) реализует: `GetDbCommand` (`QueryExecutor.cs:56-77`),
  `ExecuteReaderAsync(Behavior)` (`:241`), цикл `while (await reader.ReadAsync(ct))` + каркас,
  `Dispose` ридера.
- `InMemoryDataContext` роль не реализует → fallback: `ToAsyncEnumerable` + `JsonSerializer` по
  строке (документируется как аллокация на строку; у in-memory нет `IDataRecord`).

### Кэш

`JsonRowWriterFactory` + статический `ConcurrentDictionary<MapperCacheKey, JsonRowWriter>` рядом с
`MapperCache` (`DataContext/MapperCache.cs:26`), ключ — как `RowMapperFactory.BuildKey:165`
(provider + `TResult` + sql + column signature) плюс дискриминатор shape-опций (`IgnoreNull` +
naming policy). Компиляция один раз на форму.

### Валидация

- `SelectList` пуст (`OneColumn`/`IgnoreColumns`, нет `Select`) → `InvalidOperationException` с
  подсказкой использовать `Select(...)`.
- Не-скалярный `PropertyType` (вложенная проекция) в фазе 1 → fallback-сериализация значения
  (не ломаем UX), оформляется как `JsonSerializer.Serialize`.
- `oneColumn=true` (скалярный терминал) → JSON не массив, а одно значение; либо запретить в фазе 1.

## Фазы

- **Фаза 1 (MVP):** плоские скаляры, DB-стриминг, in-memory fallback, `Array`/`NdJson`, `Root`,
  `IgnoreNull`, naming policy.
- **Фаза 2:** вложенные проекции и `Projection<T1,T2>` через рекурсию `StartObject`/`EndObject`;
  коллекции — по контракту STJ.
- **Фаза 3 (опционально):** DB-side JSON fast-path — если `ISqlDialect.SupportsForJson` (SQL Server
  `FOR JSON`), PostgreSQL `json_agg`/`row_to_json`, MySQL `JSON_ARRAYAGG`, ClickHouse `JSONEachRow` —
  просто перекачать байты без managed-сериализации.

## Провайдерная матрица

| Провайдер | Managed JSON (фаза 1) | DB-side JSON (фаза 3) | Комментарий |
|---|---|---|---|
| SQLite | да | нет | |
| PostgreSQL (Npgsql) | да | `json_agg`/`row_to_json` | |
| SQL Server | да | `FOR JSON` (`SupportsForJson`) | |
| MySQL / MariaDB | да | `JSON_ARRAYAGG` | |
| ClickHouse | да | `JSONEachRow` | |
| In-memory | fallback (materialize + STJ) | — | аллокация на строку |

## Ограничения и цена

- **Управление памятью.** Весь документ не держится в памяти — только буфер; но `Utf8JsonWriter`
  синхронен, асинхронность достигается сбросом буфера в поток.
- **Naming.** Имя свойства берётся из CLR `PropertyName` + `JsonNamingPolicy` (как STJ над POCO), а
  не из SQL-алиаса; при необходимости — отдельная опция.
- **Fallback на экзотических типах** (массивы/`Tuple`/`Map`, вложенность) уходит в
  `JsonSerializer.Serialize` и боксет значение — приемлемо, путь не горячий.
- **In-memory** не даёт нулевой аллокации (нет `IDataRecord`) — только fallback.
- **Не закрывает `Stream`** и не откатывает частично записанный JSON при исключении — задокументировать.
- **Публичный API** (`JsonStreamOptions`, `IJsonStreamWriter`, терминалы) — обновить
  `docs/specs/design/API-NAMING-REVIEW.md`; при заморозке поверхности — `PublicAPI.*`.
- **Коллизия имён** с LOB-`ToStream` (`todo_streaming_lob.md`) снята выбором `WriteJson`/`WriteJsonAsync`.

## Этапы внедрения

- **Фаза 1 (MVP):** `JsonRowWriterFactory`, `StreamBufferWriter`, `JsonStreamOptions`,
  `JsonStreamer`, роль `IJsonStreamWriter`, реализация в `QueryExecutor`, терминалы на
  `QueryCommand`/`EntityBuilder`, fallback для in-memory.
- **Фаза 2:** вложенные проекции/`Projection<T1,T2>`.
- **Фаза 3:** DB-side JSON для диалектов с поддержкой.
- **Вне области:** запись JSON, десериализация, JSON как тип параметра.

## План тестов

- Unit (`tests/nextorm.core.tests`): форма JSON для anonymous/DTO (числа, `string`, `bool`, `Guid`,
  `DateTime`, `decimal`, nullable, `byte[]`), `IgnoreNull`, `Root`, `NdJson`, naming policy; проверка,
  что `TResult`-маппер не вызывается.
- SQL-gen/провайдерные: терминал использует streaming-prepared команду, корректный `Behavior`.
- Интеграция (`tests/nextorm.integration.tests/CommonTestSuite`): round-trip на SQLite/PG/SQLServer/
  MySQL/ClickHouse; сверка с `JsonSerializer.Serialize(ToList())`; in-memory fallback.
- Память: предохранитель на аллокации (не O(размера результата)); опционально бенчмарк.

## Открытые вопросы

1. Скалярная (`oneColumn`) проекция — запретить или отдавать одно значение?
2. Нужен ли `Root` в фазе 1 или достаточно `Array`/`NdJson`?
3. Имя роли/делегата: `IJsonStreamWriter`/`JsonRowWriter` vs `IJsonStreamer`/`JsonRowSerializer`.
4. Делать ли DB-side JSON (фаза 3) или ограничиться managed-сериализацией.
5. Формат enum: число (как STJ по умолчанию) или строка по `[JsonConverter]`.
6. Взаимодействие с `Projection<T1,T2>`/join: плоский список `Item1/Item2` (фаза 2).

## Файлы к изменению

- Новое: `src/nextorm.core/DataContext/Json/JsonRowWriterFactory.cs`,
  `DataContext/Json/StreamBufferWriter.cs`, `DataContext/Json/JsonStreamOptions.cs`,
  `DataContext/Json/JsonStreamer.cs`, `DataContext/Roles/IJsonStreamWriter.cs`.
- Правки: `src/nextorm.core/Query/QueryCommand.TResult.cs`,
  `src/nextorm.core/Builders/EntityBuilderExtensions.cs`,
  `src/nextorm.core/DataContext/QueryExecutor.cs`, `DataContext/InMemoryDataContext.cs`,
  `DataContext/DataContext.cs` (реализация роли), опционально диалектные `*Dialect.cs` (фаза 3).
- Тесты: `tests/nextorm.core.tests/` (форма JSON), `tests/nextorm.{sqlite,postgres,sqlserver,mysql,
  clickhouse}.tests/` (SQL-gen), `tests/nextorm.integration.tests/CommonTestSuite.JsonStream.cs`.
- Документация: `docs/advanced/api-reference.md` (+RU), `docs/guide/` (раздел потоковой выдачи,
  +RU), `docs/specs/design/API-NAMING-REVIEW.md`.
