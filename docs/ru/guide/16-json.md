# Поддержка JSON в разных провайдерах

> Единого переносимого API для JSON нет: PostgreSQL предоставляет нативную поверхность `json`/`jsonb`, SQL Server возвращает документ завершающим предложением `FOR JSON` и предлагает текстовые JSON-функции, MySQL/MariaDB предоставляют те же текстовые JSON-функции через семейство `JSON_EXTRACT`/`JSON_SET`, а остальные провайдеры отклоняют JSON-конструкции.

**Предварительные требования:** [Запросы и проекции](../engine/index.md) · [Скалярные функции](../scalar-functions/07-json-and-xml.md#json-и-jsonb-postgresql) · [Табличные функции](13-table-valued-functions.md#встроенные-табличные-функции) · [Обзор провайдеров](../connecting-providers/overview.md)

## Обзор

В nextorm намеренно нет кросс-провайдерной поверхности JSON-функций. «Работа с JSON» означает разное у разных
провайдеров, и движок держит эти механизмы раздельно, а не делает вид, что это одна функция:

* **SQL Server** имеет две независимые поверхности. [`ForJson`](xref:NextORM.Core.QueryCommand`1.ForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean,System.Object[])) выполняет запрос и
  возвращает весь набор строк одним JSON-документом (`FOR JSON PATH`/`FOR JSON AUTO`), а [`SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer) предоставляет текстовые JSON-функции
  (`json_value`, `json_query`, `json_modify`, `isjson`) и табличную функцию `openjson`. Кроме того, в
  SQL Server 2025 (17.x) есть отдельный нативный тип колонки `json`, доступный для `[JsonColumn]` только
  через явный гейт версии — см. [Нативный `json` в SQL Server 2025+](#нативный-json-в-sql-server-2025).
* **PostgreSQL** имеет нативные типы `json`/`jsonb` и единственную поверхность функций/операторов
  `json`/`jsonb` (`SqlFunctions.Postgres`): конструирование, агрегация, доступ, вложенность и
  JSONPath.
* **MySQL и MariaDB** предоставляют ту же поверхность текстовых JSON-функций, что и SQL Server
  ([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)), через семейство
  `JSON_EXTRACT`/`JSON_UNQUOTE`/`JSON_SET`. Их JSON-*колонки* — не одно и то же: в MySQL 8 есть
  настоящий тип `json` (сервер валидирует, нормализует и сообщает его как `json`), а `JSON` в MariaDB —
  это псевдоним `LONGTEXT` плюс CHECK `json_valid()` (сообщается как `longtext`). Обе используются через
  **текстовый транспорт** — свойство `[JsonColumn]` сериализуется в текст, который разбирает сервер, —
  потому что ни у одного из провайдеров нет привязываемого нативного представления JSON, и
  `JsonColumnStorage.Native` для них отклоняется (см.
  [Отобразить CLR-объект на нативную JSON-колонку](#отобразить-clr-объект-на-нативную-json-колонку)).
* **ClickHouse** отображает извлекающие функции строкового JSON (`JSONExtractString`, `JSONExtractInt`,
  `JSONExtractFloat`, `JSONExtractBool`, `JSONExtractRaw`, `JSONHas`, `JSONLength`, `JSONType`) — плюс
  возвращающие массивы `JSONExtractKeys`/`JSONExtractArrayRaw` (проецируются как `string[]`) и
  `JSONExtractKeysAndValues` (проецируется как `Tuple<string, T>[]`) — и быстрый
  разбор плоского JSON (`visitParamExtractString`/`Int`/`Float`/`Bool`/`Raw`) через
  [`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract), JSONPath-скаляры
  `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (методы `json_value`/`json_query`/`json_exists`) и функции
  нативного JSON `JSONAllPaths`/`JSONAllPathsWithTypes`/`toJSONString` (методы `json_all_paths`/
  `json_all_paths_with_types`/`to_json_string`) через тот же флаг. Они принимают нативное значение
  `JSON` (`CAST(col AS JSON)` для колонки `String`); `JSONAllPaths` проецируется как `string[]`, а
  `JSONAllPathsWithTypes` — как `Map(String, String)` → `Dictionary<string, string>`. У ClickHouse есть
  и нативный *тип колонки* `JSON` (с корнем-объектом): свойство `[JsonColumn]` и «голые»
  `JsonObject`/`JsonDocument`/`JsonElement` в проекции/параметре отображаются на него
  (см. [Отобразить CLR-объект на нативную JSON-колонку](#отобразить-clr-объект-на-нативную-json-колонку)).
* **SQLite** предоставляет поверхность JSON1 через провайдерную `SqlFunctions.Sqlite` (`json`/`jsonb`/
  `json_extract`, операторы `->`/`->>`, `json_set`/`json_patch` и табличные функции `json_each`/`json_tree`).
  Кросс-провайдерный JSON-API на неё не отображён: нативный тип `json`/`jsonb` и текстовые имена
  (`json_value`/`json_query`/`json_modify`/`isjson`) бросают `NotSupportedException`, поэтому используйте
  члены JSON1 напрямую (см. [Провайдер SQLite](../connecting-providers/sqlite.md#функции-специфичные-для-sqlite)).
* **Каждый SQL-провайдер** использует один общий JSON-*терминал*: [`WriteJson`](xref:NextORM.Core.QueryCommand`1.WriteJson(System.IO.Stream)) /
  [`WriteJsonAsync`](xref:NextORM.Core.QueryCommand`1.WriteJsonAsync(System.IO.Stream,System.Threading.CancellationToken)) записывают проекцию запроса в принадлежащий вызывающему `Stream` как JSON-массив или
  NDJSON, сериализуя на клиенте через `System.Text.Json` (см. [Потоковое сохранение данных](30-streaming-data.md#json)).
  В SQL Server подходящую плоскую проекцию вместо этого обслуживает сама СУБД через завершающее
  `FOR JSON PATH`, а единственная колонка-документ копируется в поток ограниченными чанками — это
  внутренняя оптимизация с тем же контрактом вывода, не меняющая скалярный `ForJson` ниже (см.
  [Нативный быстрый путь SQL Server](30-streaming-data.md#нативный-быстрый-путь-sql-server)).
  Предварительная валидация использует единый контракт исключений — `NotSupportedException` с
  соглашением о сообщении `JSON streaming validation [<token>]: <context>.` и фиксированными токенами
  `mode-options`, `projection`, `names`, `unsupported-column`, `reader-binding`, `in-memory` и
  `unsupported-execution-form` — а несовместимая схема reader'а отклоняется до любого вывода, включая
  обрамление. Это терминал сериализации, а не SQL-функция JSON: он не меняет провайдерные
  JSON-поверхности ниже.

Поскольку механизмы разные, один и тот же концептуальный результат записывается по-разному. Читайте
раздел своего провайдера; в [матрице провайдеров](#матрица-провайдеров) и разделе
[выбор подхода](#выбор-подхода) показаны соответствия.

## Матрица провайдеров

| Провайдер | Вывод `ForJson` | Нативные `json`/`jsonb` | Текстовые JSON-функции | `openjson` как `FROM` |
|---|---|---|---|---|
| SQL Server | Поддерживается ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)) | `json` в 2025+ ([`SupportsJson`](xref:NextORM.Core.ISqlDialect.SupportsJson)), включается объявленной major-версией 17+ или редакцией движка Azure/Fabric SQL database; только корни-объекты и массивы. Иначе JSON хранится в `nvarchar` | Поддерживаются ([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)) | Поддерживается |
| PostgreSQL | Нет | Поддерживаются ([`SupportsJson`](xref:NextORM.Core.ISqlDialect.SupportsJson)) | Нет | Нет |
| SQLite | Нет | Нет | Нет (JSON1 через `SqlFunctions.Sqlite`) | Нет |
| MySQL / MariaDB | Нет | Настоящая колонка `json` (MySQL) / `JSON` = псевдоним `LONGTEXT` + CHECK `json_valid()` (MariaDB); обе — через **текстовое** хранение, `Native` отклоняется | Поддерживаются ([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)) | Нет |
| ClickHouse | Нет | Нативный `JSON` ([`SupportsJson`](xref:NextORM.Core.ISqlDialect.SupportsJson); корень-объект, через `[JsonColumn]`/`JsonObject`/`JsonDocument`/`JsonElement`) | Строковый JSON + JSONPath-скаляры + функции нативного JSON (`JSONAllPaths`/`JSONAllPathsWithTypes`/`toJSONString`; [`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract)) | Нет |
| In-memory | Не применимо (нет SQL) | Не применимо | Не применимо | Не применимо |

«Нет» означает, что команда отклоняется через `NotSupportedException` при построении SQL, а не то, что
в СУБД нет поддержки JSON.

## SQL Server

### Вернуть весь набор строк одним JSON-документом

[`ForJson`](xref:NextORM.Core.QueryCommand`1.ForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean,System.Object[])) — **терминальный оператор**: он выполняет запрос и возвращает весь
набор результатов одним JSON-документом (форму документа задаёт проекция; тип элемента запроса не
важен, так как база возвращает одну колонку-документ):

```csharp
string? json = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id, e.String })
    .ForJson(ForJsonMode.Path, root: "items", includeNullValues: true);
```

```sql
select id, somestring
 from complex_entity for json path, root('items'), include_null_values
```

[`Path`](xref:NextORM.Core.ForJsonMode.Path) формирует документ по псевдонимам проекции (по умолчанию), а
[`Auto`](xref:NextORM.Core.ForJsonMode.Auto) — по структуре таблицы:

```csharp
string? json = dataContext.From<IComplexEntity>()
    .Select(e => new { e.Id, e.String })
    .ForJson(ForJsonMode.Auto);
```

```sql
select id, somestring
 from complex_entity for json auto
```

Необязательный `root` оборачивает документ в `ROOT('name')`, а `includeNullValues` добавляет
`INCLUDE_NULL_VALUES`. `ForJson` возвращает `null`, если запрос не вернул строк (SQL Server отдаёт SQL
NULL для пустого результата `FOR JSON`). Тот же серверный рендеринг `FOR JSON PATH` лежит и в основе
нативного быстрого пути `WriteJson` для подходящей плоской проекции; этот путь — внутренняя
оптимизация и не меняет сам `ForJson` (см.
[Нативный быстрый путь SQL Server](30-streaming-data.md#нативный-быстрый-путь-sql-server)). Предложение размещается после `ORDER BY` и перед завершающим
`OPTION (...)`, поэтому сочетается с [`Hint`](xref:NextORM.Core.QueryCommand`1.Hint(System.String[])) (сначала хинт, затем
терминал): запрос `for json path option (recompile)` корректен. Используйте
[`WithForJson`](xref:NextORM.Core.QueryCommand`1.WithForJson(NextORM.Core.ForJsonMode,System.String,System.Boolean)), чтобы только присоединить предложение и сохранить
команду композируемой. (Табличные хинты,
[`WithTableHint`](xref:NextORM.Core.FromOptions.WithTableHint(System.String[])), прикрепляются к таблице `FROM` и не
зависят от JSON-предложения.) Диалект без
поддержки предложения отклоняет команду, а совмещение `ForJson` с `ForXml` бросает
`NotSupportedException("FOR JSON and FOR XML cannot be combined.")`.

### Читать JSON, хранящийся в текстовой колонке

До SQL Server 2025, а также в MySQL/MariaDB нет привязываемого нативного *представления* JSON: JSON хранится
в обычной текстовой колонке и обрабатывается текстовыми функциями
([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)). Путь — это строка JSONPath
(`'$.name'`); `json_value` возвращает скаляр, `json_query` — фрагмент объекта/массива, а `json_modify`
— изменённую копию. В MySQL/MariaDB те же выражения рендерятся как `json_unquote(json_extract(...))`,
`json_extract(...)` и `json_set(...)`. В SQL Server 2025 эти же функции работают и поверх колонки
`nvarchar`, и поверх нативного типа `json` (см. [Нативный `json` в SQL Server 2025+](#нативный-json-в-sql-server-2025)):

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

`SqlFunctions.SqlServer.isjson` проверяет, является ли текстовое значение корректным JSON. В SQL Server
предикат отрисовывается как `(isjson(x)) = 1`, потому что T-SQL `ISJSON` возвращает `int`, а как
проецируемое значение приводится к `bit`; в MySQL/MariaDB он рендерится как `json_valid(x)`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.SqlServer.isjson(e.String))
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id
 from complex_entity where (isjson(somestring)) = 1
```

### Нативный `json` в SQL Server 2025+

В SQL Server 2025 (17.x) есть **отдельный встроенный тип `json`** — не псевдоним `nvarchar` — и он же есть
в движках Azure SQL, построенных на том же релизе. nextorm достигает его тем же отображением `[JsonColumn]`,
но гейт — это **объявленное** состояние: контекст, построенный с `Version(17, 0)` или выше, **либо** с
явной редакцией движка `SqlServerEngineEdition` — `SqlDatabase` (5), `AzureSqlManagedInstance` (8) или
`MicrosoftFabricSqlDatabase` (12). Конструктор по умолчанию (без версии) остаётся текстовым, поэтому уже
существующее отображение на `nvarchar` не меняется незаметно для вас:

```csharp
// Azure SQL Database / база данных SQL в Microsoft Fabric: объявите редакцию движка, а не выдуманную major-версию.
using var azure = new SqlServerDataContext(SqlServerEngineEdition.SqlDatabase, connectionString);

// Конструктор «версия первой»: объявленная major-версия открывает возможности, зависящие от версии.
using var dataContext = new SqlServerDataContext(new Version(17, 0), connectionString);

[JsonColumn(Storage = JsonColumnStorage.Native)]   // Auto и Text здесь столь же допустимы
public Address Address { get; set; } = new();
```

Представление остаётся обычной CLR-строкой `string`: параметры выводятся как `nvarchar`, DOM-параметра нет,
`SqlDbType.Json` не выставляется, двойной сериализации нет. В контексте с открытым гейтом `Auto`, `Text` и
`Native` разрешаются в один и тот же текстовый конвертер, поэтому сохраняемые байты идентичны текстовому
хранению — различается тип колонки, а не конвертер. Нативная колонка добавляет ограничение **сервера**: она
хранит только документы-объекты и массивы, а скалярные корни (строка, число, булево) и документ JSON `null`
отклоняет ошибкой сервера **13609** — для таких полезных нагрузок используйте текстовое хранение. Это
отклонение сервера, возникающее при выполнении; собственных клиентских правил nextorm не добавляет.

Редакция 11 («Azure Synapse serverless SQL pool, или Microsoft Fabric») **не** даёт возможности по
редакции; её дают только 5, 8 и 12. nextorm никогда не опрашивает `SERVERPROPERTY('EngineEdition')` —
редакция является объявлением, неизменяемым для типа контекста, и генерация SQL зависит только от
объявленного состояния. Azure SQL Database, Managed Instance и база данных SQL в Microsoft Fabric
проверены **только модульными тестами**: живого экземпляра Azure/Fabric для этой проверки не было, и
утверждение не распространяется на каждую нагрузку Fabric. nextorm также не генерирует DDL колонок,
поэтому перевод существующей колонки в `json` остаётся решением по развёртыванию/миграции.

### Развернуть JSON в строки через `openjson`

`SqlFunctions.SqlServer.openjson(json)` — табличная функция (SQL Server 2016+), используемая через
[`From`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Linq.Expressions.Expression{System.Func{NextORM.Core.EntityBuilder{``0}}})) с вызовом фабрики. Её
схема по умолчанию выдаёт свойства JSON-объекта или элементы JSON-массива в виде
[`SqlFunctions.IOpenJsonRow`](xref:NextORM.Core.SqlFunctions.IOpenJsonRow) (`Key`/`Value`/`Type`):

```csharp
var json = """{"a":1,"b":2}""";

var entries = dataContext
    .From(() => SqlFunctions.SqlServer.openjson(json))
    .Select(r => new { r.Key, r.Value, r.Type })
    .ToList();
```

```sql
select [key] as [Key], value, type
 from openjson(@json) as [t1]
```

Для типизированной проекции объявите собственную фабрику `TvfEntityBuilder<T>`, форма строки которой
совпадает с предложением `WITH (...)`, и передайте тело предложения как `withClause`; nextorm только
генерирует вызов и не создаёт функцию.
`SqlFunctions.SqlServer.string_split` устроен так же для строки с разделителем. Обе функции включаются
через [`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect.SupportsTableFunction(System.String)), поэтому их генерирует только SQL Server.

## PostgreSQL

### Передать JSON-значение как параметр

Параметр, чьё значение во время выполнения — `JsonDocument`, `JsonElement` или `JsonNode`,
привязывается как `jsonb`, поэтому его можно сразу использовать с JSON-операторами:

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

Привязка параметров провайдером не меняется: обычная строка с JSON по-прежнему привязывается как `text`.
Исключение — шесть аксессоров `json_get`, `json_get_text`, `json_get_path` и `json_get_path_text` (обе
перегрузки: по строковому ключу и по целочисленному индексу): они применяют **контекстную SQL-типизацию** —
сырое клиентское значение документа (литерал, захваченное значение или явный параметр) разбирается как
`jsonb` в сгенерированном SQL, а правый операнд типизируется по перегрузке (`text` для строкового ключа,
`integer` для индекса, `text[]` для пути). Нативные колонки `json`/`jsonb` и серверные JSON-выражения
**не** приводятся и сохраняют типы операнда и результата. Для всех остальных JSON-функций разбирайте
строку явно через `SqlFunctions.Postgres.json_cast(value)` (`cast(value as jsonb)`).

### Отобразить CLR-объект на нативную JSON-колонку

Свойство `[JsonColumn]` хранит любой сериализуемый CLR-объект в колонке как JSON. По умолчанию
`JsonColumnStorage.Auto` использует нативный тип там, где он есть, поэтому в PostgreSQL свойство
отображается на нативную колонку `jsonb` и на чтение, и на запись — insert, update и `RETURNING`;
`Native` принудительно включает `jsonb`, а `Text` — текстовую колонку. Свойство участвует в проекциях и
сравнениях с константой как любая другая сконвертированная колонка:

```csharp
[SqlTable("customers")]
public sealed class Customer
{
    public long Id { get; set; }

    [JsonColumn]
    public Address Address { get; set; } = new();
}
```

Отображение покрывает свойство, а не схему: nextorm не генерирует DDL колонки, поэтому колонка `jsonb`
уже должна существовать в таблице (то же верно для нативной колонки `json` в SQL Server — см.
[Нативный `json` в SQL Server 2025+](#нативный-json-в-sql-server-2025)). Fluent-форма `JsonColumn(o => ...)` и настройки сериализатора —
в разделе [Конвертеры значений](../infrastructure/03-value-converters.md#json-колонки).

В **MySQL и MariaDB** тот же атрибут сохраняет **текстовое** хранение: у этих провайдеров есть JSON
*колонка*, но нет привязываемого нативного *представления* JSON (`MySqlConnector` отклоняет параметры
`JsonElement`, `JsonDocument` и `JsonNode`, `MySqlDbType.JSON` принимает только строки, а колонка `json`
всегда читается как `string`). Это полноценное поддерживаемое хранение, а не отказ от JSON-колонок:
таблица MySQL 8 может объявить колонку как `json` — сервер валидирует и нормализует документ, — а
таблица MariaDB может объявить её как `JSON`, что сервер сообщает как `longtext` с CHECK `json_valid()`.
Явный `JsonColumnStorage.Native` на обоих движках завершается отказом с `NotSupportedException`,
называющим диалект (`MySqlDialect`/`MariaDbDialect`), до выполнения любой команды; используйте `Auto`
(текст) или `Text`. Учтите также, что MySQL сравнивает документ `json` как типизированное значение —
строковый операнд ему не равен, — и nextorm не обещает трансляцию сравнения для такой колонки.

«Голые» CLR-типы JSON обрабатываются на границе иначе, чем отображённая колонка:

* Значение `JsonDocument` или `JsonElement` материализуется из колонки `json`/`jsonb` в именованной
  проекции, а параметр любого из этих типов привязывается как `jsonb`.
* «Голое» свойство `JsonNode`/`JsonNode?` (объявленное ровно как `JsonNode`, не `JsonObject`/`JsonArray`)
  читает нативную колонку `json`/`jsonb`: текст читается и разбирается для корней-объектов, массивов и
  скаляров, как в скалярной (`Select(x => x.Data)`), так и в составной проекции. SQL `NULL` и
  JSON-литерал `null` оба становятся CLR `null`. Объявленные свойства `JsonObject`/`JsonArray` и «голая»
  *скалярная* проекция `JsonDocument`/`JsonElement` в эту поддержку не входят — отображайте их через
  `[JsonColumn]` или проецируйте в именованной форме. Параметр `JsonNode` по-прежнему привязывается как
  `jsonb`.
* Обычная `string` отображается на `text`. Шесть аксессоров выше (`json_get`, `json_get_text`,
  `json_get_path`, `json_get_path_text`, перегрузки по строковому ключу и по индексу) по-прежнему
  принимают сырую строку документа — литерал, захваченное значение или явный параметр, — потому что
  применяют контекстную SQL-типизацию и разбирают её как `jsonb` в сгенерированном SQL, типизируя
  ключ/индекс/путь как `text`/`integer`/`text[]`. Нативная колонка `json`/`jsonb` или серверное
  JSON-выражение остаются без приведения, поэтому их типы операнда и результата не меняются. Для любой
  другой JSON-функции разбирайте строку через `SqlFunctions.Postgres.json_cast(value)`.

На ClickHouse тот же раздел описывает нативную колонку `JSON`, чей физический тип — `JSON` и чей
корень — **объект**: она принимает JSON-объекты в корне и отклоняет массивы и скалярные корни.
Свойство `[JsonColumn]` со storage `Auto` или `Native` отображает POCO с корнем-объектом, `JsonObject`,
`JsonDocument` или `JsonElement` на нативный тип `JSON` и на чтение, и на запись, а `Text` сохраняет
колонку `String`; модель, не сериализующаяся в объект, отклоняется с `NotSupportedException`. Те же
DOM-значения с корнем-объектом отображаются и как «голые» проекции и параметры. Проецировать нужно
через именованную форму — `Select(x => new { x.Doc })` или DTO, — потому что «голый» скаляр верхнего
уровня `Select(x => x.Doc)` **не** обрабатывается ядровым классификатором проекции. Пустой объект `{}`
остаётся непустым значением, а SQL `NULL` в колонке `Nullable(JSON)` остаётся **отличным** от него
(`null` для `JsonObject`/`JsonDocument`, `default`/`JsonValueKind.Undefined` для value-типа
`JsonElement`). Чтение «голой» `string` из колонки `JSON` не отображается. Легаси-алиас `Object('json')`
в этой интеграции **не поддерживается**: на `ClickHouse.Driver` 1.4.0.0 и ClickHouse 25.8.33.6
connection-level параметр `allow_experimental_object_type=1` позволяет создать тип, но чтение
материализуется как `Tuple<SByte,String>` (путь → значение) и не читается через нативный транспорт
`JsonObject` (`GetFieldValue<JsonObject>` бросает `InvalidCastException`). Сам nextorm
`allow_experimental_object_type` **не** включает.

### Собрать JSON-объект или массив в проекции

Функции конструирования собирают значение `json`/`jsonb` из обычных SQL-выражений. Строковые
аргументы-константы становятся ключами:

```csharp
var document = dataContext.From<IComplexEntity>()
    .Select(e => new { V = SqlFunctions.Postgres.jsonb_build_object("id", e.Id, "name", e.String) })
    .First()
    .V;
```

```sql
select jsonb_build_object('id', id, 'name', somestring) as "V"
 from complex_entity
```

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.jsonb_build_object("a", x, ...)` | `jsonb_build_object('a', x, ...)` |
| `SqlFunctions.Postgres.jsonb_build_array(x, y)` | `jsonb_build_array(x, y)` |
| `SqlFunctions.Postgres.json_array(x, y)` / `jsonb_array(x, y)` | `json_array(x, y)` / `json_array(x, y returning jsonb)` |
| `SqlFunctions.Postgres.to_jsonb(x)` | `to_jsonb(x)` |

### Свернуть набор строк в один документ (аналог `FOR JSON`)

В PostgreSQL нет суффикса `FOR JSON`; эквивалент — агрегат `jsonb_agg` поверх проекции. Используйте
`jsonb_agg`/`json_agg` для массива значений или `jsonb_object_agg` для словаря:

```csharp
var json = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.jsonb_agg(
        SqlFunctions.Postgres.jsonb_build_object("id", e.Id, "name", e.String)))
    .First();
```

```sql
select jsonb_agg(jsonb_build_object('id', id, 'name', somestring))
 from complex_entity
```

Вложенность выражается через `GroupBy`: внешний агрегат делает документ, внутренний — массив каждой
группы:

```csharp
var groups = dataContext.From<IComplexEntity>()
    .GroupBy(e => new { e.Int })
    .Select(e => new
    {
        e.Int,
        Items = SqlFunctions.Postgres.jsonb_agg(
            SqlFunctions.Postgres.jsonb_build_object("id", e.Id, "name", e.String))
    })
    .ToList();
```

```sql
-- jsonb_agg(jsonb_build_object('id', id, 'name', somestring)) grouped by int
```

`json_object_agg(key, value)` / `jsonb_object_agg(key, value)` дают JSON-объект вместо массива
(`jsonb_object_agg(id, somestring)`).

### Читать и фильтровать JSON

Операторы доступа и предикаты доступны как методы `SqlFunctions.Postgres`:

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_get(json, "key")` / `json_get(json, 0)` | `json -> key` / `json -> 0` |
| `SqlFunctions.Postgres.json_get_text(json, "key")` / `json_get_text(json, 0)` | `json ->> key` / `json ->> 0` |
| `SqlFunctions.Postgres.json_get_path(json, path)` / `json_get_path_text(json, path)` | `json #> path` / `json #>> path` |
| `SqlFunctions.Postgres.json_contains(a, b)` | `a @> b` |
| `SqlFunctions.Postgres.json_exists(json, "key")` | `json ? 'key'` |
| `SqlFunctions.Postgres.json_exists_any(json, keys)` / `json_exists_all(json, keys)` | `json ?\| keys` / `json ?& keys` |
| `SqlFunctions.Postgres.jsonb_path_exists(json, path)` / `jsonb_path_match(json, path)` | `jsonb_path_exists(json, cast(path as jsonpath))` / `jsonb_path_match(...)` |
| `SqlFunctions.Postgres.jsonb_path_query_first(json, path)` / `jsonb_path_query_array(json, path)` | `jsonb_path_query_first(...)` / `jsonb_path_query_array(...)` |

Четыре аксессора выше применяют **контекстную SQL-типизацию**: сырое клиентское значение документа
(литерал, захваченное значение или явный параметр) разбирается как `jsonb`, поэтому
`json_get("{\"a\":1}", "a")` отрисует `cast('{"a":1}' as jsonb) -> cast('a' as text)`, а нативная колонка
`json`/`jsonb` или серверное JSON-выражение выводятся без приведения (`doc -> cast('a' as text)`), сохраняя
типы операнда и результата. Ключ/индекс/путь всегда типизируется по перегрузке: `cast(<key> as text)`,
`cast(<index> as integer)`, `cast(<path> as text[])` — литеральный ключ встраивается как
`cast('a' as text)`. Сама привязка параметров не меняется: обычный параметр-`string` по-прежнему
привязывается как `text`.

Операнд `path`/`keys` — это `string[]`, привязываемый одним параметром-массивом, поэтому
`json_get_path(json, new[] { "a", "b" })` отрисует `json #> cast(@p0 as text[])`. Функции JSONPath
принимают путь обычной строкой и приводят его к `jsonpath`; `JSON_EXISTS`, `jsonpath` и соседние
JSON-функции сохраняют собственное независимое поведение.

```csharp
var path = new[] { "a", "b" };

var rows = dataContext.From<IComplexEntity>()
    .Where(e => SqlFunctions.Postgres.json_contains(
        SqlFunctions.Parameter<JsonDocument>(0),
        SqlFunctions.Parameter<JsonDocument>(1)))
    .Where(e => SqlFunctions.Postgres.json_exists(SqlFunctions.Parameter<JsonDocument>(2), "key"))
    .Select(e => new { e.Id })
    .ToList();
```

```sql
select id
 from complex_entity where (@norm_p0 @> @norm_p1) and (@norm_p2 ? 'key')
```

### SQL/JSON-функции запроса

SQL/JSON-функции запроса принимают `jsonpath` (приводится из строки `path`) и выбирают значение, а не
ключ:

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.json_value(json, path)` | `json_value(json, cast(path as jsonpath))` (возвращает `text`) |
| `SqlFunctions.Postgres.json_query(json, path)` | `json_query(json, cast(path as jsonpath))` (возвращает `jsonb`) |
| `SqlFunctions.Postgres.json_exists(json, path, fromJsonPath)` | `json_exists(json, cast(path as jsonpath))` (возвращает `boolean`) |

```csharp
var json = SqlFunctions.Postgres.jsonb_build_object("a", 1);

var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Value = SqlFunctions.Postgres.json_value(json, "$.a"),
        Exists = SqlFunctions.Postgres.json_exists(json, "$.a", true)
    })
    .ToList();
```

```sql
select json_value(jsonb_build_object('a', 1), cast('$.a' as jsonpath)) as "Value",
       json_exists(jsonb_build_object('a', 1), cast('$.a' as jsonpath)) as "Exists"
 from complex_entity
```

У `json_exists` две формы: двухаргументный `json_exists(json, "key")` — это оператор `?` верхнего
уровня, а трёхаргументный `json_exists(json, path, fromJsonPath)` — SQL/JSON-функция пути (третий
аргумент — дискриминатор, в SQL не рендерится).

Функции конструирования и агрегации возвращают `string?`; десериализуйте через
`JsonSerializer.Deserialize<T>(...)`, когда нужен объект .NET.

## Выбор подхода

| Цель | SQL Server | PostgreSQL |
|---|---|---|
| Один JSON-документ для всего результата | `.ForJson(...)` | `jsonb_agg(jsonb_build_object(...))` |
| Один JSON-объект на строку | `json_query`/конкатенация текста или сборка на клиенте | `jsonb_build_object(...)` |
| Прочитать поле из JSON-значения | `json_value(col, '$.x')` | `col ->> 'x'` |
| Проверить, что значение — JSON | `isjson(col)` | значение уже типизировано как `json`/`jsonb` колонкой |
| Фильтр по содержимому JSON | `json_value(col, '$.x') = ...` | `col @> ...`, `col ? 'x'`, `col #> ...` |
| Развернуть JSON в строки | `openjson(...)` как источник `FROM` | встроенного помощника нет; объявите фабрику `TvfEntityBuilder<T>` |

## Ограничения

* Переносимой абстракции JSON нет. Код, написанный для JSON-поверхности одного провайдера, бросает
  `NotSupportedException` на другом; если нужно ветвление, используйте флаги возможностей
  `ISqlDialect`.
* Терминал потоковой JSON-записи валидирует свои опции и проекцию заранее и падает с
  `NotSupportedException` по стабильному соглашению `JSON streaming validation [<token>]: <context>.`
  (токены `mode-options`, `projection`, `names`, `unsupported-column`, `reader-binding`, `in-memory`,
  `unsupported-execution-form`); несовместимая схема reader'а отклоняется до любого вывода, включая
  обрамление массива/`Root`. Ошибки жизненного цикла остаются
  `InvalidOperationException`/`ObjectDisposedException`, а ошибки приёмника/провайдера и отмена
  сохраняют свою категорию. См.
  [Исключения валидации и ошибки времени выполнения](30-streaming-data.md#исключения-валидации-и-ошибки-времени-выполнения).
* `ForJson`/`ForXml` есть только в SQL Server ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson) /
  [`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml)), и они взаимоисключающи в одной команде.
* Поверхность `json`/`jsonb` PostgreSQL (`SqlFunctions.Postgres`) требует
  [`SupportsPostgresJsonSql`](xref:NextORM.Core.ISqlDialect.SupportsPostgresJsonSql) (только PostgreSQL);
  текстовые функции SQL Server и MySQL/MariaDB требуют
  [`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson). Каждая бросает исключение на
  чужом провайдере.
* JSON-поверхность ClickHouse (`JSONExtract*`/`visitParamExtract*`, JSONPath-скаляры и функции нативного
  JSON `JSONAllPaths`/`JSONAllPathsWithTypes`/`toJSONString`) требует
  [`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract); она не пересекается с
  набором `SupportsTextJson`. Функции нативного JSON принимают нативное значение `JSON` (колонку
  `String` приведите через `CAST(col AS JSON)`); `json_all_paths` проецируется как `string[]`, а
  `json_all_paths_with_types` — как `Dictionary<string, string>` (`mapKeys`/`mapValues` превращают map в
  коллекцию). Нативная *колонка* `JSON` хранит нативный `JSON` для `[JsonColumn]` (`Auto`/`Native`,
  POCO/`JsonObject`/`JsonDocument`/`JsonElement` с корнем-объектом) и для «голых» `JsonObject`/
  `JsonDocument`/`JsonElement` с корнем-объектом в проекции/параметре
  (см. [Отобразить CLR-объект на нативную JSON-колонку](#отобразить-clr-объект-на-нативную-json-колонку));
  массивы/скалярные корни, «голая» `string` и легаси-алиас `Object('json')` не поддерживаются, а
  «голый» скаляр верхнего уровня `Select(x => x.Doc)` не обрабатывается ядровым классификатором
  проекции.
* Расширение JSON1 в SQLite отображается только через провайдерную поверхность `SqlFunctions.Sqlite`
  (`json_extract`, `->`/`->>`, `json_each`/`json_tree`, ...); кросс-провайдерный native/`json_*` API на неё
  не отображён, поэтому эти имена бросают `NotSupportedException`.
* Провайдер in-memory не генерирует SQL, поэтому `ForJson`/`ForXml` бросают `NotSupportedException`, а
  JSON-поверхности к нему не применимы.
* PostgreSQL отображает свойство `[JsonColumn]` на нативную колонку `jsonb` (`Auto` storage) на чтение
  и запись (insert, update и `RETURNING`), а параметр `JsonDocument`/`JsonElement`/`JsonNode`
  привязывается как `jsonb`. ClickHouse отображает `[JsonColumn]` `Auto`/`Native` и «голые»
  `JsonObject`/`JsonDocument`/`JsonElement` с корнем-объектом на нативную колонку `JSON` с корнем-объектом.
  SQL Server отображает `[JsonColumn]` `Auto`/`Native` на нативный тип `json` **только в контексте,
  объявившем major-версию 17 или выше либо редакцию движка Azure/Fabric SQL database**, и только для
  документов-объектов/массивов — скалярный
  корень и документ JSON `null` отклоняет сервер с ошибкой 13609, поэтому таким полезным нагрузкам нужен
  `Text`. В контексте до 2025 без объявленной редакции, эквивалентной версии 17, JSON в SQL Server всегда текст; JSON-колонки MySQL/MariaDB
  достигаются текстовым транспортом на обоих движках (`Auto`/`Text`), а `Native` отклоняется с
  `NotSupportedException`, называющим диалект.

## См. также

- [Запросы и проекции](../engine/index.md) — `ForJson`/`ForXml` для SQL Server.
- [Скалярные функции](../scalar-functions/07-json-and-xml.md#json-и-jsonb-postgresql) — полная поверхность JSON/JSONB в PostgreSQL и текстовые JSON-функции SQL Server и MySQL/MariaDB.
- [Табличные функции](13-table-valued-functions.md#встроенные-табличные-функции) — `openjson` и `string_split`.
- [Провайдер PostgreSQL](../connecting-providers/postgres.md) — JSON-колонки, привязка параметров и массивы.
- [Провайдер SQL Server](../connecting-providers/sqlserver.md) — `FOR JSON`, `FOR XML` и текстовый JSON.
- [Провайдер MySQL](../connecting-providers/mysql.md) и [провайдер MariaDB](../connecting-providers/mariadb.md) — текстовый JSON через `JSON_EXTRACT`/`JSON_SET`.
- [Ограничения и что вне области](../advanced/limitations.md)

---

Source: `src/nextorm.core/Visitors/JsonSqlTranslator.cs`, `src/nextorm.core/Query/SqlFunctions.Postgres.cs`,
`src/nextorm.core/Query/SqlFunctions.SqlServer.cs`, `src/nextorm.core/Expressions/ForJson.cs`,
`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `src/nextorm.sqlserver/SqlServerDialect.cs`,
`src/nextorm.postgres/PostgresDialect.cs`; tests `tests/nextorm.sqlserver.tests/SqlGeneration/SqlGenerationTests.cs:443,695,1313,1390`,
`tests/nextorm.postgres.tests/SqlGeneration/SqlGenerationTests.cs:848,868,887,930,950,1831,1861`,
`tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:1773`.
