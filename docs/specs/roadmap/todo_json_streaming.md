# TODO: Потоковая выдача JSON в `Stream` (`WriteJson` / `WriteJsonAsync`)
> Tracking issue: [#39](https://github.com/AlexeyShirshov/nextorm/issues/39).

> Рабочий черновик (working draft), синхронизированный с требованиями issue
> [#39](https://github.com/AlexeyShirshov/nextorm/issues/39); дизайн **не утверждён**, а
> implementation-скетчи ниже подлежат ревью J1–J4 (см. «Дизайн-ревью»). Источник: обсуждение
> потоковой выдачи результата `Select` в виде JSON. **Не путать** с LOB-стримингом (issue #27,
> план удалён после выпуска): там речь о чтении одного LOB-значения как `Stream`/`TextReader`,
> здесь — о сериализации строк результата в JSON без материализации `TResult`.

## Пункт и цель

- Фича: терминал над `QueryCommand<TResult>` (и `EntityBuilder<TEntity>`), который пишет результат
  `Select` как JSON прямо в выходной `Stream`, **не создавая `TResult` на строку и не бокся
  значения**.
- Критерий приёмки: аллокации O(1) на вызов (арендованный буфер + один `Utf8JsonWriter`), а не
  O(размера результата); **цель** — один раз скомпилированный делегат записи (row-writer) на
  переиспользуемую подготовленную форму запроса, но точный механизм переиспользования/кэширования
  в #39 **не утверждён** (см. «Кэш»); JSON совпадает с
  `JsonSerializer.Serialize(ToList())` **только в пределах поддерживаемых в фазе 1 форм проекций и
  опций**; выходной `Stream` не закрывается; работает **только на SQL-провайдерах**; in-memory не
  поддерживается — `NotSupportedException`, без materialization/`ToAsyncEnumerable`/STJ-fallback.
- **Строгий стриминг.** Строка `TResult` не материализуется и значения не боксятся: неподдерживаемые
  shape (вложенные/сложные массивы, `Tuple`, `Map`, document-типы) и кастомные `JsonConverter`
  завершаются fail-fast (исключение), а не тихой managed-сериализацией.
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
| Кэш маппера | `DataContext/MapperCache.cs:15,26,31` | `MapperCacheKey` и bounded-лимит `MaxEntries` уже есть как **кандидат/предпочтительный скетч** (J3); кэширование writer'ов в #39 **не утверждено**, но если применяется — только bounded, точная интеграция/ключ **не решены**; несбондированный параллельный static-словарь запрещён |
| Чтение | `DataContext/ResultSetEnumerator.cs:49,211,233`, `DataContext/QueryExecutor.cs:16,241,261` | ридер открывается только под `MapDelegate`; цикла «reader → JSON» нет |
| Роли | `DataContext/IDataContext.cs:17`, `Roles/IQueryExecutor.cs` | нет роли/метода для JSON-стрима; `IQueryExecutor` расширять нежелательно |
| In-memory | `DataContext/InMemoryRowMaterializer.cs:51,73` | нет `IDataRecord`; терминал не поддерживает in-memory → `NotSupportedException`, fallback отсутствует |
| DB-side JSON | `DataContext/Dialect/ISqlDialect.cs` (`SupportsForJson`), `nextorm.sqlserver/SqlServerDialect.cs` | `FOR JSON`/`json_agg`/`JSONEachRow` есть как диалектный факт, но не как путь вывода |
| Публичный API | `Builders/EntityBuilderExtensions.cs` | терминала нет |

## Дизайн

### Фаза 1 — плоские скалярные `Select`

Фаза 1 покрывает ровно два случая: **(a) скалярную** проекцию `Select(x => x.Id)` — в `Array`
это массив скалярных значений `[1,2]`, в `NdJson` — одно скалярное значение на строку
`1\n2\n`; и **(b) плоскую объектную** проекцию `Select(x => new { x.Id })` / DTO с плоскими
скалярами — массив объектов. Вложенные проекции/коллекции и `Projection<T1,T2>` — фаза 2,
DB-side JSON — фаза 3.

**Фаза 2 реализована (issue [#176](https://github.com/AlexeyShirshov/nextorm/issues/176)).**
Реализована интерпретация: JSON-специфичный захват формы до того, как подготовка сделает вложенную
конструкцию непрозрачной; каждый скалярный лист привязывается к назначенному ordinal'у reader'а
(никакой реконструкции по SQL-алиасу/имени/порядку); вложенные `new`/member-init пишутся как
объектные узлы; реальный `Projection<T1,T2>` даёт верхнеуровневые `Item1`/`Item2`; нативные
rank-one массивы (включая зубчатые) сериализуются рекурсивно, а `byte[]` остаётся Base64
(проверяется до общего массива); уникальность имён проверяется **на область объекта**; присутствие
объекта представлено отдельно от значений (`new` всегда объект; null-ветка условия — `null`; слот
outer-join — `null`, когда все его сопоставленные столбцы — SQL `NULL`); условная вложенная
конструкция опускает скрытую nullable-колонку-сентинел через существующий CASE-путь. Остающиеся
границы вынесены в отдельные issue: `Projection<T1>`/`#160` недоступен, дочерние коллекции —
[#172](https://github.com/AlexeyShirshov/nextorm/issues/172), именование/опции —
[#177](https://github.com/AlexeyShirshov/nextorm/issues/177), перечисления/конвертеры —
[#178](https://github.com/AlexeyShirshov/nextorm/issues/178), DB-side JSON и не-массивы-коллекции —
вне этой области.

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
явный `Select` (см. «Валидация»). Поддерживаются только SQL-провайдеры: если `DataContext` не
реализует роль `IJsonStreamWriter` (in-memory), терминал бросает `NotSupportedException`.
Поведение при частичном выводе/ошибке **ещё не согласовано** в #39 — открытый вопрос, а не часть
контракта (см. «Открытые вопросы»).

### Компилируемый writer (сердце фичи)

Из `QueryCommand.SelectList` (та же метаданка, что у маппера) собирается выражение и компилируется
делегат (предлагаемая internal-форма; J2):

```csharp
internal delegate void JsonRowWriter(IDataRecord record, Utf8JsonWriter writer);
```

По столбцу — аналог `RowMapperFactory.MapColumn` (`:34`), но вместо `Expression.New` — вызовы
`Utf8JsonWriter`. Требуются **одновременно** провайдер-корректная семантика чтения и **нулевой
боксинг**. Блокер снят в #168: SQL Server-ветка общего буферизованного маппера больше не читает
числа через `GetValue`+`Convert.ChangeType`, а диспетчеризует по рантайм `GetFieldType` и читает
storage-типизированным геттером (общий `GetNumericGetter`/`GetTypedConversion` с CSV-хуком), поэтому
переиспользование `mapColumn`/`MapColumnExpression` теперь даёт zero-boxing для закрытого числового
набора; бокинг-фолбэк остаётся только для storage-типа вне набора. Прямые типизированные геттеры
`SelectExpression.GetDataRecordMethod()` по-прежнему не являются гарантированным путём. Таблица ниже —
целевая форма writer'а:

| CLR тип | чтение/конверсия (provider-aware, to design) | writer |
|---|---|---|
| int/short/byte/long/float/double/decimal | provider-aware typed reading/conversion (to design), без боксинга | `WriteNumberValue` |
| bool | provider-aware typed reading/conversion (to design) | `WriteBooleanValue` |
| string | provider-aware typed reading/conversion (to design) | `WriteStringValue` |
| Guid | provider-aware typed reading/conversion (to design) | `WriteStringValue(Guid)` |
| DateTime / DateTimeOffset / DateOnly / TimeOnly / TimeSpan | provider-aware typed reading/conversion (to design, ISO) | `WriteStringValue` |
| enum | provider-aware typed reading/conversion (to design) | `WriteNumberValue` (default, см. «Открытые вопросы») |
| byte[] | provider-aware typed reading/conversion (to design, `GetValue`, см. `:130-137`) | `WriteBase64String` |
| вложенный/сложный массив, `Tuple`, `Map`, document, кастомный `JsonConverter` | — | **fail-fast исключение** (в фазе 1 не поддерживается) |

Правила:
- `if (record.IsDBNull(i))` → `WriteNullValue()`, либо пропуск свойства при `IgnoreNull`; guard
  совпадает с `RowMapperFactory.MapColumn:32-54` (`Nullable`/`DefaultOnNull`). `IgnoreNull`
  управляет свойствами **объекта**; скалярный `null` в скалярной проекции всё равно эмитится как
  `null`;
- имя свойства = `PropertyName` → `PropertyNamingPolicy?.ConvertName(...)`; порядок = порядок
  `SelectList`;
- **никаких** `JsonSerializer.Serialize(writer, value)`-fallback'ов и боксинга: неподдерживаемый
  shape или кастомный `JsonConverter` → fail-fast;
- `IgnoreNull` и naming policy запекаются в делегат → при принятом кэшировании входят в ключ
  кэша (shape).

### Стриминг и аллокации

- `Utf8JsonWriter` поверх внутреннего `StreamBufferWriter : IBufferWriter<byte>` (аренда
  `ArrayPool<byte>`, возврат в `finally`) — синхронный writer, память O(буфера).
- После каждой строки / при превышении порога — `await output.WriteAsync(buffer.WrittenMemory, ct)`
  и `Reset()`; `Flush()` перед выходом.
- Каркас: `Array` → `[` / `,` / `]` (элемент — скаляр или объект; `Root` → обёртка
  `{"<Root>":[...]}` поддерживается для `Array` в MVP); `NdJson` → одна строка = одно скалярное
  значение или объект + `\n`; `Root`/`WriteIndented` в `NdJson` запрещены (см. «Валидация»).

### Встраивание в исполнение

`QueryCommand.WriteJsonAsync` по образцу `ToAsyncEnumerable` (`QueryCommand.TResult.cs:115`):

1. `_dataContext.GetPreparedQueryCommand(this, streaming: true, nonStreamUsing: true, ct)` —
   подготовка в no-mapper режиме, чтобы не компилировать неиспользуемый `Func<IDataRecord,TResult>`
   (J5; флаг — как у L4);
2. `JsonRowWriterFactory.GetOrBuild(queryCommand, sql, providerType, shapeOpts)` — делегат из
   `SelectList`;
3. если `_dataContext is IJsonStreamWriter w` → `w.WriteJsonAsync(prepared, rowWriter, output,
   options, @params, ct)`; иначе (in-memory) — `NotSupportedException` (fallback отсутствует).

Новая **узкая роль** (не расширяем `IQueryExecutor`; **internal**, J2):

```csharp
internal interface IJsonStreamWriter
{
    Task WriteJsonAsync<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand,
        JsonRowWriter rowWriter, Stream output, JsonStreamOptions options,
        object[]? @params, CancellationToken cancellationToken);
}
```

- Синхронный публичный терминал `WriteJson` требует и sync-пути роли; точная сигнатура роли
  (sync + async, J4) — **открытый вопрос**; приведённый интерфейс — рабочий набросок, не финал.
- `QueryExecutor`/`DataContext` (DB) реализует: `GetDbCommand` (`QueryExecutor.cs:56-77`),
  `ExecuteReaderAsync(Behavior)` (`:241`), цикл `while (await reader.ReadAsync(ct))` + каркас,
  `Dispose` ридера.
- `InMemoryDataContext` роль не реализует; правки в `InMemoryDataContext.cs` не требуются — guard
  в терминале (`QueryCommand`) бросает `NotSupportedException`; никакого `ToAsyncEnumerable` +
  `JsonSerializer`-fallback нет.

### Кэш

Кэширование writer'ов в #39 **не утверждено**. Если writer всё же кэшируется, кэш обязан быть
**bounded**; существующий `MapperCache` (`DataContext/MapperCache.cs:15,26,31`, лимит `MaxEntries`)
— **кандидат/предпочтительный скетч** для ревью J3, а не принятое решение. Точная интеграция и
ключ **не решены**; несбондированный параллельный static-словарь (`ConcurrentDictionary`)
запрещён. Черновой набросок ключа (только для обсуждения): как `RowMapperFactory.BuildKey:165`
(provider + `TResult` + sql + column signature) плюс дискриминатор shape-опций (`IgnoreNull` +
naming policy). Компиляция — не чаще одного раза на переиспользуемую подготовленную форму —
**цель**, а не гарантия выбранной (ещё не утверждённой) реализации; вытеснение, если кэш принят, —
по общему лимиту.

### Валидация

- Нет явного `Select`/`SelectList` пуст (`IgnoreColumns`) → `InvalidOperationException` с подсказкой
  использовать `Select(...)`.
- `NdJson` + `Root` **или** `NdJson` + `WriteIndented` → явная **ошибка валидации** (исключение),
  без тихого игнора. В `NdJson` каждая строка — одно скалярное значение или объект, без root/indent.
- Поддерживаемые фазы 2 формы: вложенные `new`/member-init объекты, слоты `Projection<T1,T2>`
  (`Item1`/`Item2`, скалярный слот не оборачивается), нативные rank-one/зубчатые массивы,
  условная null-ветка конструкции; null-семантика — явная конструкция всегда объект, null-ветка
  даёт `null`, outer-join слот — `null` при всех `NULL`-столбцах, массив `NULL`/пустой/элемент —
  `null`/`[]`/`null`. Неподдерживаемое (вложенная коллекция/`List<T>`, `Tuple`, `Map`, document,
  enum/конвертер, многомерный массив, неранжируемый элемент) или кастомный `JsonConverter` →
  **fail-fast** исключение; silent managed-сериализации нет. Отложенные границы: дочерние
  коллекции — #172, именование/опции — #177, enum/конвертеры — #178; `Projection<T1>` (#160)
  недоступен и не добавляет предусловий.
- In-memory (`DataContext` без роли `IJsonStreamWriter`) → `NotSupportedException`.
- Скалярная (`oneColumn`) проекция **поддерживается** в фазе 1: `Array` → массив скалярных значений,
  `NdJson` → одно значение на строку (решение принято, не открытый вопрос).

## Фазы

- **Фаза 1 (MVP):** скалярный `Select(x => x.Id)` и плоская объектная проекция
  (`Select(x => new { x.Id })`/DTO); DB-стриминг только по SQL-провайдерам, in-memory —
  `NotSupportedException` (без fallback); `Array` (по умолчанию)/`NdJson`, `Root` (для `Array`),
  `IgnoreNull`, naming policy; `NdJson` + `Root`/`WriteIndented` — ошибка валидации.
- **Фаза 2 (реализована, #176):** вложенные проекции и `Projection<T1,T2>` через рекурсию
  `StartObject`/`EndObject`; нативные rank-one/зубчатые массивы; условная null-ветка конструкции;
  скалярные листья по контракту STJ. Ранее неподдерживаемые вложенные формы больше не отклоняются;
  дочерние коллекции (#172), enum/конвертеры (#178) и новое именование (#177) остаются
  fail-fast-границами.
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
| In-memory | нет — `NotSupportedException` | — | управляемая выдача не поддерживается; правок в `InMemoryDataContext.cs` не требуется |

## Ограничения и цена

- **Управление памятью.** Весь документ не держится в памяти — только буфер; но `Utf8JsonWriter`
  синхронен, асинхронность достигается сбросом буфера в поток.
- **Naming.** Имя свойства берётся из CLR `PropertyName` + `JsonNamingPolicy` (как STJ над POCO), а
  не из SQL-алиаса; при необходимости — отдельная опция.
- **Экзотические типы** (вложенные/сложные массивы, `Tuple`, `Map`, document, кастомный
  `JsonConverter`) в фазе 1 **не поддерживаются** — fail-fast, без
  `JsonSerializer.Serialize`-fallback и без боксинга.
- **In-memory не поддерживается** (нет `IDataRecord`): `NotSupportedException`, нулевой аллокации
  нет по определению.
- **Частичный вывод/ошибка** — политика (откат/дописать ли валидный JSON) **не согласована** в
  #39; открытый вопрос, а не задокументированное поведение.
- **Публичный API** (`JsonStreamOptions` + терминалы; роль `IJsonStreamWriter` — internal, J2) —
  обновить `docs/specs/design/API-NAMING-REVIEW.md`; при заморозке поверхности — `PublicAPI.*`.
- **Коллизия имён** с LOB-`ToStream` (issue #27) снята выбором `WriteJson`/`WriteJsonAsync`.

## Этапы внедрения

- **Фаза 1 (MVP):** `JsonRowWriterFactory` (provider-aware типизированная конверсия колонок без
  боксинга — J1, **блокер**), `StreamBufferWriter`, `JsonStreamOptions`, `JsonStreamer`, роль
  `IJsonStreamWriter` (internal, J2; sync + async, J4), реализация в `QueryExecutor`, терминалы на
  `QueryCommand`/`EntityBuilder`, guard `NotSupportedException` для in-memory в терминале
  (правки `InMemoryDataContext` не нужны). Кэш writer'ов **не утверждён**; при кэшировании —
  только bounded (`MapperCache` как кандидат, J3).
- **Фаза 2:** вложенные проекции/`Projection<T1,T2>`.
- **Фаза 3:** DB-side JSON для диалектов с поддержкой.
- **Вне области:** запись JSON, десериализация, JSON как тип параметра; string-ветка `ToJson`.

## План тестов

- Unit (`tests/nextorm.core.tests`): форма JSON для скалярной проекции (`[1,2]`, `1\n2\n`) и плоской
  объектной anonymous/DTO (числа, `string`, `bool`, `Guid`, `DateTime`, `decimal`, nullable,
  `byte[]`), `IgnoreNull`, `Root`, `NdJson`, naming policy; проверка, что `TResult`-маппер не
  вызывается.
- Валидация/guard: in-memory → `NotSupportedException`; неподдерживаемые shape (вложенность,
  `Tuple`, `Map`, document) и кастомный `JsonConverter` → fail-fast; `NdJson` + `Root`/`WriteIndented`
  → ошибка валидации.
- SQL-gen/провайдерные: терминал использует streaming-prepared команду в no-mapper режиме,
  корректный `Behavior`; provider-aware typed-конверсия колонок и паритетность значений по
  провайдерам без боксинга (J1).
- Верификация блокера J1: SQL Server numeric converter без боксинга (allocation-free путь) при
  паритетности значений с маппером; до проектирования механизма тест не может быть зелёным.
- Интеграция (`tests/nextorm.integration.tests/CommonTestSuite.JsonStream`): round-trip (скаляр и
  объект) на SQLite/PG/SQLServer/MySQL/ClickHouse; сверка с `JsonSerializer.Serialize(ToList())`
  **в пределах поддерживаемых форм и опций**.
- Память: предохранитель на аллокации (не O(размера результата)); опционально бенчмарк.

## Открытые вопросы

1. Имя роли/делегата: `IJsonStreamWriter`/`JsonRowWriter` vs `IJsonStreamer`/`JsonRowSerializer`.
2. Делать ли DB-side JSON (фаза 3) или ограничиться managed-сериализацией.
3. Формат enum: число (как STJ по умолчанию) или строка по `[JsonConverter]`.
4. ~~Взаимодействие с `Projection<T1,T2>`/join: плоский список `Item1/Item2` (фаза 2).~~
   **Решено в #176**: реальный `Projection<T1,T2>` даёт верхнеуровневые `Item1`/`Item2`
   (объект/скаляр по типу элемента), имена проверяются на область объекта.
5. Поведение при частичном выводе/ошибке (откат/дописать ли валидный JSON) — не согласовано в #39.
6. Точная сигнатура роли: sync + async entry paths (J4) — форма sync-метода пока не определена.
7. Провайдер-специфичные детали конверсии колонок/ридеров (сверх направления J1) и общий
   тип/сообщение исключений валидации (для in-memory зафиксирован `NotSupportedException`).
8. ~~**Блокер:** SQL Server numeric converter без боксинга + провайдерная паритетность
   (`GetValue`+`Convert.ChangeType` боксит) — типизированный эквивалент не спроектирован.~~
   **Снято в #168**: рантайм-диспетчеризация по `GetFieldType` + storage-типизированные геттеры,
   нулевой бокинг и провайдерная паритетность подтверждены paired-бенчмарком
   (`BenchmarkCategory("acceptance")`).

## Файлы к изменению

- Новое: `src/nextorm.core/DataContext/Json/JsonRowWriterFactory.cs`,
  `DataContext/Json/StreamBufferWriter.cs`, `DataContext/Json/JsonStreamOptions.cs`,
  `DataContext/Json/JsonStreamer.cs`, `DataContext/Roles/IJsonStreamWriter.cs` (internal).
- Правки: `src/nextorm.core/Query/QueryCommand.TResult.cs`,
  `src/nextorm.core/Builders/EntityBuilderExtensions.cs`,
  `src/nextorm.core/DataContext/QueryExecutor.cs`, `DataContext/DataContext.cs` (реализация роли),
  опционально диалектные `*Dialect.cs` (фаза 3); `DataContext/MapperCache.cs` — **только если
  кэш writer'ов будет принят** (кандидат, J3), не гарантированная правка;
  `DataContext/InMemoryDataContext.cs` **не изменяется** — guard в терминале.
- Тесты: `tests/nextorm.core.tests/` (форма JSON, in-memory `NotSupportedException`, fail-fast
  shape/`JsonConverter`, невалидные `NdJson`-опции), `tests/nextorm.{sqlite,postgres,sqlserver,mysql,
  clickhouse}.tests/` (SQL-gen), `tests/nextorm.integration.tests/CommonTestSuite.JsonStream.cs`
  (round-trip, сверка с `JsonSerializer.Serialize(ToList())`).
- Документация: `docs/advanced/api-reference.md` (+RU), `docs/guide/` (раздел потоковой выдачи,
  +RU), `docs/specs/design/API-NAMING-REVIEW.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **нужен пересмотр — корректностный блокер J1** + обязательные правки J2/J3/J4.
> Статус после синхронизации с #39: J1 учтён концептуально (провайдерная политика чтения), но
> zero-boxing-эквивалент остаётся нерешённым блокером; J2/J4 учтены в скетчах выше (J4 — частично,
> точная сигнатура открыта), J3 — bounded-кэш обязателен только если кэш принят, `MapperCache` —
> кандидат; J5 — направление принято, J7 снят строгим контрактом. Ниже — исторические факты ревью,
> они не переписываются; пометки «Учтено/Устарело» добавлены только как аннотации.

- **[DRY]/[PERF] 🟡** J1 (корректность) — таблица writer'а (`:96-104`) использует `GetDataRecordMethod()`/типизированные геттеры, обходя провайдерскую политику чтения: SQL Server для чисел читает через `GetValue`+`Convert.ChangeType` (`src/nextorm.sqlserver/SqlServerDataContext.cs:72-104`), а `TimeSpan` без нативного типа требует `DurationStorage.FromStorage` (`RowMapperFactory.cs:39-46`) → `InvalidCastException`/неверные значения. Fix: строить JSON-геттер из того же `mapColumn`/`MapColumnExpression`, что и маппер. **Провайдерная политика учтена концептуально** в разделе «Компилируемый writer», но **zero-boxing-часть не решена**: прямое переиспользование `mapColumn` для SQL Server-чисел боксит (`GetValue`+`Convert.ChangeType`) и потому решением не является; типизированный эквивалент без боксинга — открытый блокер. **Снято в #168**: SQL Server-ветка общего маппера переведена на рантайм-диспетчеризацию по `GetFieldType` со storage-типизированными геттерами (общий `GetNumericGetter`/`GetTypedConversion` с CSV-хуком), поэтому `mapColumn`/`MapColumnExpression` больше не боксит закрытый числовой набор.
- **[DIP]/[ISP] 🟡** J2 — `IJsonStreamWriter`/`JsonRowWriter` заявлены публичными (`:134-140,89-90`), тогда как `IMutationExecutor`/`IBatchExecutor` — **internal** (`Roles/IMutationExecutor.cs:9`, `Roles/IBatchExecutor.cs:15`); сигнатура отдаёт `JsonRowWriter`, собираемый только из внутреннего `SelectList`. Fix: сделать оба `internal`; публичными — при 2-м внешнем потребителе. **Учтено**: роль и делегат переведены в `internal`.
- **[DRY] 🟡** J3 — параллельный static `ConcurrentDictionary<MapperCacheKey, JsonRowWriter>` (`:150-153`) дублирует ключ и ограничение `MaxEntries` (`MapperCache.cs:15,29-40`); незабондированный второй кэш — утечка. Fix: один bounded-кэш/ключ. **Учтено частично**: требование bounded-кэша сохранено, несбондированный static-словарь запрещён; но само кэширование в #39 не утверждено, `MapperCache` — лишь кандидат/предпочтительный скетч, точная интеграция/ключ не решены.
- **[contract] 🟡** J4 — роль объявлена только async (`:134-140`), а публичный терминал sync+async (`:59-62`); путь для `WriteJson` не определён (иначе блокировка). Fix: sync-метод роли либо убрать `WriteJson` из фазы 1. **Частично учтено**: sync + async заявлены, точная сигнатура — открытый вопрос.
- **[PERF]/[SRP] 🟡** J5 — подготовка компилирует неиспользуемый `Func<IDataRecord,TResult>` (`QueryPlanner.cs:401-403`), если команда не в no-mapper режиме. Fix: тот же no-mapper флаг, что L4. **Направление принято** в разделе «Встраивание в исполнение».
- **[OCP] ℹ️** J6 — `WriteJson` сосуществует с существующим `ForJson` (SQL Server `FOR JSON`, `QueryCommand.TResult.cs:678`); зафиксировать в `API-NAMING-REVIEW`.
- **[PERF] ℹ️** J7 — fallback `JsonSerializer.Serialize(writer, value)` боксит; держать только для массивов/`Tuple`/`Map`, вне горячего плоского пути. Deferred. **Устарело/снято**: строгий контракт #39 запрещает fallback — неподдерживаемые shape и кастомные конвертеры завершаются fail-fast. Запись сохранена как историческая.
- **ℹ️** Устаревшие якоря: `RowMapperFactory.cs:24,63,120` → `:34,87,116`; `MapperCache.cs:26` → `:15,31`.
- **[OCP] ℹ️** J9 — как L8: JSON-стрим-путь должен поднимать interceptor-события, иначе потоковая выдача молча выпадает из наблюдаемости. **Учтено**: interceptor-события должны подниматься и на JSON-пути.
