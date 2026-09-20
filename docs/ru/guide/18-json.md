# Поддержка JSON в разных провайдерах

> Единого переносимого API для JSON нет: PostgreSQL предоставляет нативную поверхность `json`/`jsonb`, SQL Server возвращает документ завершающим предложением `FOR JSON` и предлагает текстовые JSON-функции, MySQL/MariaDB предоставляют те же текстовые JSON-функции через семейство `JSON_EXTRACT`/`JSON_SET`, а остальные провайдеры отклоняют JSON-конструкции.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Скалярные функции](11-scalar-functions.md#json-и-jsonb-postgresql) · [Табличные функции](13-table-valued-functions.md#встроенные-табличные-функции) · [Обзор провайдеров](../providers/overview.md)

## Обзор

В nextorm намеренно нет кросс-провайдерного метода для JSON. «Работа с JSON» означает разное у разных
провайдеров, и движок держит эти механизмы раздельно, а не делает вид, что это одна функция:

* **SQL Server** имеет две независимые поверхности. [`ForJson`](xref:NextORM.Core.QueryCommand`1) добавляет
  завершающее предложение `FOR JSON PATH`/`FOR JSON AUTO`, поэтому весь набор строк возвращается одним
  JSON-документом, а [`SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer) предоставляет текстовые JSON-функции
  (`json_value`, `json_query`, `json_modify`, `isjson`) и табличную функцию `openjson`.
* **PostgreSQL** имеет нативные типы `json`/`jsonb` (единственный провайдер с ними) и поверхность
  функций/операторов на `SqlFunctions.Postgres`: конструирование, агрегация, доступ, вложенность и
  JSONPath.
* **MySQL и MariaDB** хранят JSON в текстовых колонках и предоставляют ту же поверхность текстовых
  JSON-функций, что и SQL Server ([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)),
  через семейство `JSON_EXTRACT`/`JSON_UNQUOTE`/`JSON_SET`.
* **ClickHouse** отображает извлекающие функции строкового JSON (`JSONExtractString`, `JSONExtractInt`,
  `JSONExtractFloat`, `JSONExtractBool`, `JSONExtractRaw`, `JSONHas`, `JSONLength`, `JSONType`) и быстрый
  разбор плоского JSON (`visitParamExtractString`/`Int`/`Float`/`Bool`/`Raw`) через
  [`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract), а также JSONPath-скаляры
  `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (методы `json_value`/`json_query`/`json_exists`) через тот же
  флаг; его нативный тип `JSON`
  пока не отображён.
* **SQLite** не предоставляет ни одной JSON-конструкции. В СУБД есть JSON1, но nextorm его пока не
  отображает, поэтому построение SQL бросает `NotSupportedException`.

Поскольку механизмы разные, один и тот же концептуальный результат записывается по-разному. Читайте
раздел своего провайдера; в [матрице провайдеров](#матрица-провайдеров) и разделе
[выбор подхода](#выбор-подхода) показаны соответствия.

## Матрица провайдеров

| Провайдер | Вывод `ForJson` | Нативные `json`/`jsonb` | Текстовые JSON-функции | `openjson` как `FROM` |
|---|---|---|---|---|
| SQL Server | Поддерживается ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson)) | Нет — JSON хранится в `nvarchar` | Поддерживаются ([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)) | Поддерживается |
| PostgreSQL | Нет | Поддерживаются ([`SupportsJson`](xref:NextORM.Core.ISqlDialect.SupportsJson)) | Нет | Нет |
| SQLite | Нет | Нет | Нет | Нет |
| MySQL / MariaDB | Нет | Не отображаются | Поддерживаются ([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)) | Нет |
| ClickHouse | Нет | Не отображаются | Строковый JSON + JSONPath-скаляры ([`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract)) | Нет |
| In-memory | Не применимо (нет SQL) | Не применимо | Не применимо | Не применимо |

«Нет» означает, что команда отклоняется через `NotSupportedException` при построении SQL, а не то, что
в СУБД нет поддержки JSON.

## SQL Server

### Вернуть весь набор строк одним JSON-документом

[`ForJson`](xref:NextORM.Core.QueryCommand`1) добавляет завершающее предложение `FOR JSON`. Тогда СУБД
возвращает результат из одной строки и одной колонки, поэтому проецируйте одну колонку и читайте её
как строку через `First()`/`FirstOrDefault()`:

```csharp
var json = dataContext.From<IComplexEntity>()
    .Select(e => e.String)
    .ForJson(ForJsonMode.Path, root: "items", includeNullValues: true)
    .First();
```

```sql
select somestring from complex_entity for json path, root('items'), include_null_values
```

[`Path`](xref:NextORM.Core.ForJsonMode.Path) формирует документ по псевдонимам проекции (по умолчанию), а
[`Auto`](xref:NextORM.Core.ForJsonMode.Auto) — по структуре таблицы:

```csharp
var json = dataContext.From<IComplexEntity>()
    .Select(e => e.String)
    .ForJson(ForJsonMode.Auto)
    .First();
```

```sql
select somestring from complex_entity for json auto
```

Необязательный `root` оборачивает документ в `ROOT('name')`, а `includeNullValues` добавляет
`INCLUDE_NULL_VALUES`. Предложение размещается после `ORDER BY` и перед завершающим `OPTION (...)`,
поэтому сочетается с [`Hint`](xref:NextORM.Core.QueryCommand`1): запрос
`for json path option (recompile)` корректен. (Табличные хинты,
[`WithTableHint`](xref:NextORM.Core.EntityBuilder`1), прикрепляются к таблице `FROM` и не
зависят от JSON-предложения.) Диалект без
поддержки предложения отклоняет команду, а совмещение `ForJson` с `ForXml` бросает
`NotSupportedException("FOR JSON and FOR XML cannot be combined.")`.

### Читать JSON, хранящийся в текстовой колонке

Ни в SQL Server, ни в MySQL/MariaDB нет нативного типа JSON: JSON хранится в обычной текстовой колонке
и обрабатывается текстовыми функциями
([`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson)). Путь — это строка JSONPath
(`'$.name'`); `json_value` возвращает скаляр, `json_query` — фрагмент объекта/массива, а `json_modify`
— изменённую копию. В MySQL/MariaDB те же выражения рендерятся как `json_unquote(json_extract(...))`,
`json_extract(...)` и `json_set(...)`:

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
select json_value(somestring, '$.id') as [Id], json_query(somestring, '$.name') as [Name], json_modify(somestring, '$.id', '1') as [Updated] from complex_entity
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
select id from complex_entity where (isjson(somestring)) = 1
```

### Развернуть JSON в строки через `openjson`

`SqlFunctions.SqlServer.openjson(json)` — табличная функция (SQL Server 2016+), используемая через
[`FromTableFunction`](xref:NextORM.Core.DataContextExtensions). Её
схема по умолчанию выдаёт свойства JSON-объекта или элементы JSON-массива в виде
[`SqlFunctions.IOpenJsonRow`](xref:NextORM.Core.SqlFunctions.IOpenJsonRow) (`Key`/`Value`/`Type`):

```csharp
var json = """{"a":1,"b":2}""";

var entries = dataContext
    .FromTableFunction(() => SqlFunctions.SqlServer.openjson(json))
    .Select(r => new { r.Key, r.Value, r.Type })
    .ToList();
```

```sql
select [key] as [Key], value, type from openjson(@json) as [t1]
```

Для типизированной проекции объявите собственный `[SqlTableFunction("openjson")]`-враппер, форма строки
которого совпадает с предложением `WITH (...)`; nextorm только генерирует вызов и не создаёт функцию.
`SqlFunctions.SqlServer.string_split` устроен так же для строки с разделителем. Обе функции включаются
через [`SupportsTableFunction`](xref:NextORM.Core.ISqlDialect), поэтому их генерирует только SQL Server.

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
select id from complex_entity where ((@norm_p0 ->> 'name') = 'Alice')
```

Обычная JSON-строка привязывается как `text`; разберите её явно через
`SqlFunctions.Postgres.json_cast(value)` (`cast(value as jsonb)`).

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
select jsonb_build_object('id', id, 'name', somestring) as "V" from complex_entity
```

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.jsonb_build_object("a", x, ...)` | `jsonb_build_object('a', x, ...)` |
| `SqlFunctions.Postgres.jsonb_build_array(x, y)` | `jsonb_build_array(x, y)` |
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
select jsonb_agg(jsonb_build_object('id', id, 'name', somestring)) from complex_entity
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

Операнд `path`/`keys` — это `string[]`, привязываемый одним параметром-массивом, поэтому
`json_get_path(json, new[] { "a", "b" })` отрисует `json #> @p0`. Функции JSONPath принимают путь
обычной строкой и приводят его к `jsonpath`.

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
select id from complex_entity where (@norm_p0 @> @norm_p1) and (@norm_p2 ? 'key')
```

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
| Развернуть JSON в строки | `openjson(...)` как источник `FROM` | встроенного помощника нет; объявите `[SqlTableFunction]`-враппер |

## Ограничения

* Переносимой абстракции JSON нет. Код, написанный для JSON-поверхности одного провайдера, бросает
  `NotSupportedException` на другом; если нужно ветвление, используйте флаги возможностей
  `ISqlDialect`.
* `ForJson`/`ForXml` есть только в SQL Server ([`SupportsForJson`](xref:NextORM.Core.ISqlDialect.SupportsForJson) /
  [`SupportsForXml`](xref:NextORM.Core.ISqlDialect.SupportsForXml)), и они взаимоисключающи в одной команде.
* Поверхность `json`/`jsonb` PostgreSQL (`SqlFunctions.Postgres`) требует [`SupportsJson`](xref:NextORM.Core.ISqlDialect.SupportsJson);
  текстовые функции SQL Server и MySQL/MariaDB требуют
  [`SupportsTextJson`](xref:NextORM.Core.ISqlDialect.SupportsTextJson). Каждая бросает исключение на
  чужом провайдере.
* Извлекающие функции строкового JSON ClickHouse (`JSONExtract*`/`visitParamExtract*`) требуют
  [`SupportsJsonExtract`](xref:NextORM.Core.ISqlDialect.SupportsJsonExtract); они не пересекаются с
  набором `SupportsTextJson`.
* В SQLite есть JSON-возможности в СУБД, но nextorm их пока не отображает; расширение JSON1 в SQLite
  тоже не отображено.
* Провайдер in-memory не генерирует SQL, поэтому `ForJson`/`ForXml` и JSON-поверхности к нему не
  применимы.
* PostgreSQL — единственный провайдер, чьи параметры отображаются на нативный JSON-тип; JSON в
  SQL Server и MySQL/MariaDB всегда текст.

## См. также

- [Запросы и проекции](01-querying-and-projections.md) — `ForJson`/`ForXml` для SQL Server.
- [Скалярные функции](11-scalar-functions.md#json-и-jsonb-postgresql) — полная поверхность JSON/JSONB в PostgreSQL и текстовые JSON-функции SQL Server и MySQL/MariaDB.
- [Табличные функции](13-table-valued-functions.md#встроенные-табличные-функции) — `openjson` и `string_split`.
- [Провайдер PostgreSQL](../providers/postgres.md) — привязка JSON-параметров и массивы.
- [Провайдер SQL Server](../providers/sqlserver.md) — `FOR JSON`, `FOR XML` и текстовый JSON.
- [Провайдер MySQL](../providers/mysql.md) и [провайдер MariaDB](../providers/mariadb.md) — текстовый JSON через `JSON_EXTRACT`/`JSON_SET`.
- [Ограничения и что вне области](../advanced/limitations.md)

---

Source: `src/nextorm.core/Visitors/JsonSqlTranslator.cs`, `src/nextorm.core/Query/SqlFunctions.Postgres.cs`,
`src/nextorm.core/Query/SqlFunctions.SqlServer.cs`, `src/nextorm.core/Expressions/ForJson.cs`,
`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `src/nextorm.sqlserver/SqlServerDialect.cs`,
`src/nextorm.postgres/PostgresDialect.cs`; tests `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:443,695,1313,1390`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:848,868,887,930,950,1831,1861`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1773`.
