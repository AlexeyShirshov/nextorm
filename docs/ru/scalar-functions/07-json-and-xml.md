# JSON и JSONB (PostgreSQL)

PostgreSQL — единственный поддерживаемый провайдер с типами `json`/`jsonb`. Операнд JSON должен быть
выражением `json`/`jsonb`: колонка, другая JSON-функция или параметр, runtime-значение которого —
`JsonDocument`, `JsonElement` или `JsonNode` (Npgsql привязывает их как `jsonb`). Обычный параметр-строка с JSON
привязывается как `text`; её можно разобрать явно через `SqlFunctions.Postgres.json_cast(value)`
(`cast(value as jsonb)`). Исключение — шесть аксессоров `json_get`, `json_get_text`, `json_get_path` и
`json_get_path_text` (перегрузки по строковому ключу и по индексу): они применяют **контекстную
SQL-типизацию** к сырому клиентскому значению документа — литералу, захваченному значению или явному
параметру — и разбирают его как `jsonb` в сгенерированном SQL. Нативные колонки `json`/`jsonb` и серверные
JSON-выражения не приводятся и сохраняют типы операнда и результата.

```csharp
using System.Text.Json;

var document = JsonDocument.Parse("""{"name":"Alice","tags":["a","b"]}""");

var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.json_get_text(SqlFunctions.Parameter<JsonDocument>(0), "name") == "Alice")
    .Select(e => new { e.Id })
    .ToList(document);
```

```sql
select id
 from complex_entity where ((@norm_p0 ->> 'name') = 'Alice')
```

Агрегаты сворачивают набор строк в один JSON-документ:

```csharp
var json = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.jsonb_agg(e.String))
    .First();

var person = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.jsonb_build_object("id", e.Id, "name", e.String))
    .First();
```

```sql
select jsonb_agg(somestring)
 from complex_entity
select jsonb_build_object('id', id, 'name', somestring)
 from complex_entity
```

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_agg(x)` / `jsonb_agg(x)` | `json_agg(x)` / `jsonb_agg(x)` |
| `SqlFunctions.Postgres.json_object_agg(k, v)` / `jsonb_object_agg(k, v)` | `json_object_agg(k, v)` / `jsonb_object_agg(k, v)` |
| `SqlFunctions.Postgres.json_build_object("a", x, ...)` | `json_build_object('a', x, ...)` |
| `SqlFunctions.Postgres.jsonb_build_object("a", x, ...)` | `jsonb_build_object('a', x, ...)` |
| `SqlFunctions.Postgres.json_build_array(x, y)` / `jsonb_build_array(x, y)` | `json_build_array(x, y)` / `jsonb_build_array(x, y)` |
| `SqlFunctions.Postgres.json_array(x, y)` / `jsonb_array(x, y)` | `json_array(x, y)` / `json_array(x, y returning jsonb)` |
| `SqlFunctions.Postgres.to_json(x)` / `to_jsonb(x)` | `to_json(x)` / `to_jsonb(x)` |
| `SqlFunctions.Postgres.json_cast(x)` | `cast(x as jsonb)` |
| `SqlFunctions.Postgres.json_get(json, "key")` / `json_get(json, 0)` | `json -> key` / `json -> 0` |
| `SqlFunctions.Postgres.json_get_text(json, "key")` / `json_get_text(json, 0)` | `json ->> key` / `json ->> 0` |
| `SqlFunctions.Postgres.json_get_path(json, path)` / `json_get_path_text(json, path)` | `json #> path` / `json #>> path` |
| `SqlFunctions.Postgres.json_contains(a, b)` | `a @> b` |
| `SqlFunctions.Postgres.json_exists(json, "key")` | `json ? 'key'` |
| `SqlFunctions.Postgres.json_exists_any(json, keys)` / `json_exists_all(json, keys)` | `json ?\| keys` / `json ?& keys` |
| `SqlFunctions.Postgres.json_array_length(json)` / `jsonb_array_length(json)` | `json_array_length(json)` / `jsonb_array_length(json)` |
| `SqlFunctions.Postgres.json_typeof(json)` / `jsonb_typeof(json)` | `json_typeof(json)` / `jsonb_typeof(json)` |
| `SqlFunctions.Postgres.jsonb_set(json, path, value[, create])` | `jsonb_set(...)` |
| `SqlFunctions.Postgres.jsonb_insert(json, path, value[, after])` | `jsonb_insert(...)` |
| `SqlFunctions.Postgres.jsonb_strip_nulls(json)` | `jsonb_strip_nulls(json)` |
| `SqlFunctions.Postgres.jsonb_pretty(json)` | `jsonb_pretty(json)` |
| `SqlFunctions.Postgres.jsonb_delete(json, "key")` / `jsonb_delete(json, 0)` | `json - 'key'` / `json - 0` |
| `SqlFunctions.Postgres.json_concat(a, b)` | `a \|\| b` |
| `SqlFunctions.Postgres.row_to_json(row)` | `row_to_json(row)` |
| `SqlFunctions.Postgres.array_to_json(array)` | `array_to_json(array)` |
| `SqlFunctions.Postgres.jsonb_path_exists(json, path)` | `jsonb_path_exists(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.jsonb_path_match(json, path)` | `jsonb_path_match(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.jsonb_path_query_first(json, path)` | `jsonb_path_query_first(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.jsonb_path_query_array(json, path)` | `jsonb_path_query_array(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.json_value(json, path)` / `json_query(json, path)` | `json_value(json, cast(path as jsonpath))` / `json_query(json, cast(path as jsonpath))` |
| `SqlFunctions.Postgres.json_exists(json, path, fromJsonPath)` | `json_exists(json, cast(path as jsonpath))` |

Четыре оператора доступа типизируют оба операнда. Сырое клиентское значение документа (литерал,
захваченное значение или явный параметр) разбирается как `jsonb` — `json_get("{\"a\":1}", "a")` отрисует
`cast('{"a":1}' as jsonb) -> cast('a' as text)`, — а нативная колонка `json`/`jsonb` или серверное
JSON-выражение выводятся без приведения (`doc -> cast('a' as text)`), поэтому типы операнда и результата не
меняются. Ключ/индекс/путь типизируется по перегрузке: `cast(<key> as text)`, `cast(<index> as integer)`,
`cast(<path> as text[])`. Привязка параметров провайдером не меняется; обычный параметр-`string`
по-прежнему привязывается как `text`.

Операнд `path`/`keys` — это `string[]`, привязываемый как **один параметр-массив** (см.
[Массивы](06-arrays.md#массивы-postgresql)), поэтому `SqlFunctions.Postgres.json_get_path(json, new[] { "a", "b" })` отрисует
`json #> cast(@p0 as text[])`. Функции JSONPath принимают путь как обычную строку и отрисовывают его как
`cast(<path> as jsonpath)`; `JSON_EXISTS`, `jsonpath` и соседние JSON-функции это правило типизации не
меняет.

## Конструкторы SQL/JSON, пути и читатели (PostgreSQL 17)

Эти члены дополняют JSON-поверхность PostgreSQL. Все они существуют только на PostgreSQL: скалярные и
путевые формы проходят через
[`SupportsPostgresJsonSql`](xref:NextORM.Core.ISqlDialect.SupportsPostgresJsonSql), а читатели — через
`SupportsTableFunction`, поэтому любой другой провайдер (включая in-memory) при трансляции выбрасывает
`NotSupportedException`. Агрегатные члены семейства перечислены на странице
[агрегатов](05-aggregates.md); здесь документированы только скалярные члены и члены, возвращающие строки.

### Конструкторы SQL/JSON

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json(x)` | `json(cast(x as text) format json)` |
| `SqlFunctions.Postgres.json_scalar(x)` | `json_scalar(x)` |
| `SqlFunctions.Postgres.json_serialize(x)` | `json_serialize(cast(x as text) format json returning text)` |

`json` и `json_serialize` принимают JSON-**текст**: аргумент отрисовывается как `cast(x as text)`, а
`FORMAT JSON` заставляет сервер его разобрать, поэтому невалидный текст даёт `22P02` (измерено), а
обычному параметру-строке не нужен отдельный каст. `json` возвращает `json`, а `json_serialize` называет
свой текстовый результат через `returning text`, поэтому читается как `text`. `json_scalar` — конструктор
значения: строка становится JSON-строкой (`"abc"`), число — JSON-числом (`5`), а SQL NULL остаётся SQL
NULL. Все три — конструкции уровня парсера PostgreSQL 17 без строки в `pg_proc`.

### Извлечение по пути

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_extract_path(doc, path)` | `json_extract_path(cast(doc as json), variadic cast(path as text[]))` |
| `SqlFunctions.Postgres.json_extract_path_text(doc, path)` | `json_extract_path_text(cast(doc as json), variadic cast(path as text[]))` |
| `SqlFunctions.Postgres.jsonb_extract_path(doc, path)` | `jsonb_extract_path(cast(doc as jsonb), variadic cast(path as text[]))` |
| `SqlFunctions.Postgres.jsonb_extract_path_text(doc, path)` | `jsonb_extract_path_text(cast(doc as jsonb), variadic cast(path as text[]))` |

Документ — это JSON-текст, приводимый к `json`/`jsonb`, а путь `string[]` привязывается как **один**
параметр `text[]` и передаётся через `variadic`, что требуется PostgreSQL, когда массив приходит одним
выражением (измерено: форма с голым `text[]` не разрешается ни в одну функцию). Отсутствующий путь,
JSON-элемент `null` и SQL NULL в пути дают SQL NULL; формы `_text` читают JSON-элемент `null` как SQL
NULL, а пустой путь возвращает сам документ (измерено). `json_extract_path` возвращает `json`; формы
`_text` возвращают `text`, а `jsonb_extract_path` — `jsonb`.

### `jsonb_set_lax`

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.jsonb_set_lax(doc, path, newValue, createIfMissing, nullValueTreatment)` | `jsonb_set_lax(cast(doc as jsonb), cast(path as text[]), cast(newValue as jsonb), createIfMissing, nullValueTreatment)` |

Все пять аргументов явные — оба значения по умолчанию PostgreSQL отрисовываются, а не опускаются.
Документ и новое значение — JSON-текст, приводимый к `jsonb`, путь — один `text[]` (не variadic), а
`createIfMissing` для значения по умолчанию равен `true`. `nullValueTreatment` действует только когда
`newValue` — SQL NULL, и принимает одно из значений: `use_json_null` (по умолчанию, записывает JSON
`null`), `delete_key` (удаляет целевой ключ), `return_target` (возвращает документ без изменений) или
`raise_exception` (выбрасывает ошибку). Неизвестное значение проверяется сервером, и его сообщение
называет `null_value_treatment`; SQL-NULL документ остаётся NULL.

### Скаляры JSONPath с учётом часового пояса

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.jsonb_path_exists_tz(doc, path, vars, silent)` | `jsonb_path_exists_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent)` |
| `SqlFunctions.Postgres.jsonb_path_match_tz(doc, path, vars, silent)` | `jsonb_path_match_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent)` |
| `SqlFunctions.Postgres.jsonb_path_query_array_tz(doc, path, vars, silent)` | `jsonb_path_query_array_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent)` |
| `SqlFunctions.Postgres.jsonb_path_query_first_tz(doc, path, vars, silent)` | `jsonb_path_query_first_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent)` |

Документ и `vars` — JSON-текст, приводимый к `jsonb`, путь приводится к `jsonpath` (параметр-строка не
приводится неявно), а `silent` — обычный логический аргумент. Форма `_tz` обязательна для пути, который
сравнивает значения даты/времени с зависящим от часового пояса (`timestamptz`) преобразованием, например
для метода `.datetime()`: обычные соседние функции неизменяемы и на таком пути дают ошибку сервера (измеренное
сообщение: "… without time zone usage"), а эти четыре учитывают часовой пояс. Пустой набор совпадений
даёт `[]` у `query_array` и SQL NULL у `query_first`; SQL-NULL документ даёт SQL NULL (измерено для
`exists`).

### Читатели, возвращающие строки

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_array_elements(doc)` | `json_array_elements(cast(doc as json))` |
| `SqlFunctions.Postgres.json_array_elements_text(doc)` | `json_array_elements_text(cast(doc as json))` |
| `SqlFunctions.Postgres.json_each(doc)` | `json_each(cast(doc as json))` |
| `SqlFunctions.Postgres.json_each_text(doc)` | `json_each_text(cast(doc as json))` |
| `SqlFunctions.Postgres.jsonb_path_query_tz(doc, path, vars, silent)` | `(select jsonb_path_query_tz from jsonb_path_query_tz(cast(doc as jsonb), cast(path as jsonpath), cast(vars as jsonb), silent))` |
| `SqlFunctions.Postgres.json_to_record<TRow>(doc)` | `json_to_record(cast(doc as json)) as x(col type, ...)` |
| `SqlFunctions.Postgres.json_to_recordset<TRow>(doc)` | `json_to_recordset(cast(doc as json)) as x(col type, ...)` |

Читатели без `jsonb` принимают JSON-**текст**, поэтому документ приводится к `json` прямо в вызове (на
параметре-строке функция иначе не разрешается). Формы строк — локальные интерфейсы в `SqlFunctions`:

- `json_array_elements`/`json_array_elements_text` возвращают
  [`IJsonArrayElementsRow`](xref:NextORM.Core.SqlFunctions.IJsonArrayElementsRow): одна колонка
  `[Column("value")] string? Value` на элемент — документ `json`, отрисованный как текст, либо обычный
  текст из формы `_text`, где JSON-элемент `null` — это SQL NULL.
- `json_each`/`json_each_text` возвращают [`IJsonbEachRow`](xref:NextORM.Core.SqlFunctions.IJsonbEachRow):
  `[Column("key")] string? Key` и `[Column("value")] string? Value` — документ `json`, отрисованный как
  текст, либо обычный текст из формы `_text`.
- `jsonb_path_query_tz` возвращает
  [`IJsonPathQueryTzRow`](xref:NextORM.Core.SqlFunctions.IJsonPathQueryTzRow): единственная колонка
  `[Column("jsonb_path_query_tz")] string? Value` содержит одно совпадение — документ `jsonb`,
  отрисованный как текст; подзапрос с одной колонкой восстанавливает это имя после алиаса FROM.
- `json_to_record<TRow>`/`json_to_recordset<TRow>` строят список определений колонок после алиаса из
  имён и типов `[Column]` типа `TRow`, поэтому форму строки объявляет вызывающий код; `json_to_record`
  читает один объект в одну строку, а `json_to_recordset` — по строке на элемент массива.

Пустой массив, пустой объект и SQL-NULL документ не дают строк, путь без совпадений не выбирает ни
одной строки (измерено), а отсутствующее свойство JSON оставляет объявленную вызывающим кодом колонку
SQL NULL.

### Отказы

| Семейство | Отклонённый член | Проверенная причина |
|---|---|---|
| `json_populate_record(set)` | `SqlFunctions.Postgres.json_populate_record<TRow>(baseRecord, json, useJsonAsText)` | Первый аргумент — базовая строка `anyelement`: ни именованный составной тип, ни псевдотип `record` нельзя назвать из CLR-типа, поэтому достоверный вызов построить невозможно. |
| `json_populate_record(set)` | `SqlFunctions.Postgres.json_populate_recordset<TRow>(baseRecord, json, useJsonAsText)` | Та же базовая строка `anyelement`, что и у `json_populate_record`. |
| `jsonb_populate_record(set)` | `SqlFunctions.Postgres.jsonb_populate_record<TRow>(baseRecord, json)` | Тип строки результата — составной тип базовой строки, который не может назвать ни один CLR-тип. |
| `jsonb_populate_record(set)` | `SqlFunctions.Postgres.jsonb_populate_recordset<TRow>(baseRecord, json)` | Та же базовая строка `anyelement`, что и у `jsonb_populate_record`. |
| `jsonb_populate_record_valid` | `SqlFunctions.Postgres.jsonb_populate_record_valid(baseRecord, json)` | Нужна та же базовая строка `anyelement`; без базовой строки сервер поднимает `0A000` (could not determine row type). |
| `json_table` | `SqlFunctions.Postgres.json_table<TRow>(json, path)` | Нужен пункт `COLUMNS (...)` внутри скобок вызова для каждого вызова, а модель выражений отрисовывает объявленную схему только как список определений колонок после алиаса. |

Каждый член отклоняется с `NotSupportedException` при композиции источника или выражения, до
генерации SQL. Сама конструкция `json_table` работает на PostgreSQL 17; её не выражает только модель
рендеринга.

## JSON как текст (SQL Server, MySQL/MariaDB)

SQL Server хранит JSON в обычной колонке `nvarchar` — а в 2025+ с контекстом, построенным из явного `Version(17, 0)`, — в своём опциональном нативном типе `json` ([`SupportsJson`](xref:NextORM.Core.ISqlDialect.SupportsJson);
только корни-объекты и массивы, см. [Поддержка JSON](../guide/16-json.md#нативный-json-в-sql-server-2025)) — и предоставляет текстовое подмножество функций
([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)); MySQL/MariaDB предоставляют ту же
поверхность через семейство `JSON_EXTRACT`/`JSON_SET`. Путь — это строка JSONPath (`'$.name'`); `json_value` возвращает
скаляр, а `json_query` — фрагмент-объект/массив:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Id = SqlFunctions.SqlServer.json_value(e.String, "$.id"),
        Name = SqlFunctions.SqlServer.json_query(e.String, "$.name"),
        Updated = SqlFunctions.SqlServer.json_modify(e.String, "$.id", "1")
    })
    .ToList();
```

```sql
select json_value(somestring, '$.id') as [Id], json_query(somestring, '$.name') as [Name], json_modify(somestring, '$.id', '1') as [Updated]
 from complex_entity
```

| C# | SQL |
|---|---|
| `SqlFunctions.SqlServer.json_value(json, path)` | `json_value(json, path)` |
| `SqlFunctions.SqlServer.json_query(json, path)` | `json_query(json, path)` |
| `SqlFunctions.SqlServer.json_modify(json, path, value)` | `json_modify(json, path, value)` |
| `SqlFunctions.SqlServer.isjson(value)` | `isjson(value)` |

В MySQL/MariaDB те же вызовы рендерятся как `json_unquote(json_extract(...))`, `json_extract(...)`,
`json_set(...)` и `json_valid(...)`.

`SqlFunctions.SqlServer.isjson` возвращает логическое значение: в предикате он отрисовывается как `(isjson(x)) = 1`
(T-SQL `ISJSON` возвращает `int`), а при проецировании как значение приводится к `bit`. `isjson`
можно также использовать прямо в `WHERE` (`Where(e => SqlFunctions.SqlServer.isjson(e.String))`).

## Методы типа XML (SQL Server)

Тип `xml` SQL Server предоставляет постфиксные методы
([`XmlFunctions`](xref:NextORM.Core.ISqlDialect.XmlFunctions)). Они
вызываются через `SqlFunctions.SqlServer` и рендерятся как `xmlcol.method(...)`; XQuery и SQL-тип
обязаны быть строковыми литералами (оба эмитятся дословно, одиночные кавычки экранируются):

```csharp
var rows = dataContext.From<IXmlEntity>()
    .Select(x => new
    {
        Value = SqlFunctions.SqlServer.xml_value<string>(x.Payload, "(/root/item)[1]", "nvarchar(100)"),
        Fragment = SqlFunctions.SqlServer.xml_query(x.Payload, "/root/item[1]"),
        Exists = SqlFunctions.SqlServer.xml_exist(x.Payload, "/root/item[2]")
    })
    .ToList();
```

```sql
select payload.value('(/root/item)[1]', 'nvarchar(100)') as [Value], payload.query('/root/item[1]') as [Fragment], payload.exist('/root/item[2]') as [Exists]
 from xml_entity
```

Аргументы-литералы, требуемые грамматикой (ключи и документы JSON, XQuery и шаблоны формата), рендерятся в
собственной строковой форме литерала провайдера (PostgreSQL `E'…'`, SQL Server `N'…'`, MySQL/MariaDB
`_utf8mb4 X'…'`, SQLite и ClickHouse — в кавычках с удвоенными апострофами), а аргумент PostgreSQL,
содержащий `U+0000`, отвергается с `NotSupportedException`.

| C# | SQL |
|---|---|
| `SqlFunctions.SqlServer.xml_value<T>(xml, xpath, sqlType)` | `xml.value('xpath', 'sqlType')` |
| `SqlFunctions.SqlServer.xml_query(xml, xpath)` | `xml.query('xpath')` |
| `SqlFunctions.SqlServer.xml_exist(xml, xpath)` | `xml.exist('xpath')` |
| `SqlFunctions.SqlServer.xml_nodes(xml, xpath)` | `xml.nodes('xpath') as [alias]([value])` (источник APPLY) |

`xml_exist` возвращает `bit`: в предикате рендерится как `(xml.exist('xpath')) = 1`, при проецировании
остаётся bit.

Строковый метод `.nodes` — это коррелированный источник, а не скаляр: используйте его как источник
`CrossApply`/`OuterApply` и проецируйте развёрнутый `IXmlNodesRow.Value` скалярными методами выше. Он
разворачивает XML-значение в строки — по одной на узел, выбранный XQuery, — и рендерит
`<xml>.nodes('xpath') as [alias]([value])`:

```csharp
var rows = dataContext.From<IXmlEntity>()
    .CrossApply(x => SqlFunctions.SqlServer.xml_nodes(x.Payload, "/root/item"))
    .Select(p => new
    {
        Id = SqlFunctions.SqlServer.xml_value<int>(p.Item2.Value, "(.)[1]/@id", "int"),
        Text = SqlFunctions.SqlServer.xml_value<string>(p.Item2.Value, "(.)[1]", "nvarchar(100)")
    })
    .ToList();
```

```sql
select t2.value.value('(.)[1]/@id', 'int') as [Id], t2.value.value('(.)[1]', 'nvarchar(100)') as [Text]
 from xml_entity as [t1]
 cross apply t1.payload.nodes('/root/item') as [t2](value)
```

Операнд обязан быть колонкой внешней строки, а XQuery — строковым литералом; все прочие провайдеры
отвергают `xml_nodes` с `NotSupportedException`, как и in-memory-провайдер.

## Функции JSON (ClickHouse, #286 T286b)

JSON-поверхность ClickHouse диспетчеризуется `JsonExtractSqlTranslator` и проходит через
`ISqlDialect.SupportsJsonExtract`. Члены ниже дополняют каталог; формы результатов объявлены явно
(`long?`, `ulong?`, `bool?`, `double?`, `string`, `Tuple<string, string>[]`, `string[]`,
`Dictionary<string, string>`) и нормализуются через `toNullable`/`toInt64` там, где вендор выводит
более узкий тип.

| Функция | SQL |
|---|---|
| `SqlFunctions.ClickHouse.json_merge_patch(a, b)` | `JSONMergePatch(a, b)` |
| `SqlFunctions.ClickHouse.json_array_length(j)` | `JSONArrayLength(j)` |
| `SqlFunctions.ClickHouse.json_dynamic_paths(j)` / `json_dynamic_paths_with_types(j)` | `JSONDynamicPaths` / `JSONDynamicPathsWithTypes` |
| `SqlFunctions.ClickHouse.json_shared_data_paths(j)` / `json_shared_data_paths_with_types(j)` | `JSONSharedDataPaths` / `JSONSharedDataPathsWithTypes` |
| `SqlFunctions.ClickHouse.json_key(j, path)` | `JSONKey(j, path)` |
| `SqlFunctions.ClickHouse.json_extract_keys_and_values_raw(j)` | `JSONExtractKeysAndValuesRaw(j)` |
| `SqlFunctions.ClickHouse.json_extract_uint(j, path)` | `JSONExtractUInt(j, path)` |
| `SqlFunctions.ClickHouse.is_valid_json(s)` | `isValidJSON(s)` |
| `SqlFunctions.ClickHouse.simple_json_extract_raw` / `_int` / `_uint` / `_float` / `_bool` / `_string` | `simpleJSONExtractRaw` / `Int` / `UInt` / `Float` / `Bool` / `String` |

Извлекатели строкового JSON (`JSONExtractString`/`Int`/`Float`/`Bool`/`Raw`/`JSONHas`/`JSONType`,
`json_length`, `visitParamExtract*`), массивные формы (`json_extract_keys`/`json_extract_array_raw`/
`json_extract_keys_and_values<T>`) и формы нативного JSON (`json_all_paths`,
`json_all_paths_with_types`, `to_json_string`, скаляры `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS`)
документированы в [ClickHouse-specific SQL](../guide/provider-specific-sql/clickhouse.md#json-словари-и-функции-массивов).
`pretty_print_json` (появился в 26.4) и `json_extract_uint_or_default` (такой функции нет; дефолт
вендора уже покрывает поведение) отклонены с точной причиной.
