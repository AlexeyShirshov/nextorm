# COALESCE (`??`) и CAST

`a ?? b` отображается на двухаргументную замену null у провайдера. Числовое преобразование числового
операнда — приведение C#, такое как `(double)e.Id`, или вызов `Convert.ToXxx(value)` — отображается на
`cast(x as <type>)`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new { V = e.String ?? "" })
    .ToList();

var halves = dataContext.From<IComplexEntity>()
    .Select(e => (double)e.Id / 2.0)
    .ToList();
```

```sql
-- SQLite
select ifnull(somestring, '') from complex_entity
select (cast(id as double precision) / 2) from complex_entity

-- SQL Server
select isnull(somestring, '') from complex_entity
select (cast(id as float) / 2) from complex_entity

-- PostgreSQL
select coalesce(somestring, '') from complex_entity
select (cast(id as double precision) / 2) from complex_entity
```

Целевые типы числового приведения берутся из [`MakeTypeName`](xref:NextORM.Core.ISqlDialect.MakeTypeName(System.Type)):

| Тип CLR | SQLite / PostgreSQL | SQL Server |
|---|---|---|
| `byte` | `smallint` | `tinyint` |
| `short` | `smallint` | `smallint` |
| `int` | `integer` | `int` |
| `long` | `bigint` | `bigint` |
| `float` | `real` | `real` |
| `double` | `double precision` | `float` |
| `decimal` | `numeric` | `decimal(38, 10)` |

## Условные функции

`SqlFunctions.Sql.nullif` — ANSI и работает на всех SQL-провайдерах; `greatest`/`least` включаются флагом
[`SupportsGreatestLeast`](xref:NextORM.Core.ISqlDialect.SupportsGreatestLeast) (его включают PostgreSQL, MySQL/MariaDB, ClickHouse, SQL Server 2022+ и SQLite; SQLite рендерит `max`/`min`). Обработка NULL зависит от провайдера: PostgreSQL, SQL Server 2022+ и ClickHouse 24.12+ игнорируют NULL-аргументы и возвращают NULL, только если все аргументы NULL, тогда как MySQL/MariaDB и SQLite возвращают NULL, если хотя бы один аргумент NULL:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        NoZero = SqlFunctions.Sql.nullif(e.Int, 0),
        Hi = SqlFunctions.Sql.greatest(e.Id, 10L),
        Lo = SqlFunctions.Sql.least(e.Id, 10L)
    })
    .ToList();
```

```sql
select nullif(nullableint, 0) as "NoZero", greatest(id, 10) as "Hi", least(id, 10) as "Lo" from complex_entity
```

| C# | SQL |
|---|---|
| `SqlFunctions.Sql.nullif(a, b)` | `nullif(a, b)` |
| `SqlFunctions.Sql.greatest(a, b, ...)` | `greatest(a, b, ...)` |
| `SqlFunctions.Sql.least(a, b, ...)` | `least(a, b, ...)` |
| `SqlFunctions.Postgres.num_nulls(a, b, ...)` | `num_nulls(a, b, ...)` |
| `SqlFunctions.Sql.iif(condition, a, b)` | `iif(...)` (SQL Server, SQLite 3.32+), `if(...)` (MySQL/MariaDB, ClickHouse), `case when ... then ... else ... end` (PostgreSQL) |
| `SqlFunctions.SqlServer.choose(index, a, b, ...)` | `choose(index, a, b, ...)` (SQL Server) |
| `SqlFunctions.Postgres.num_nonnulls(a, b, ...)` | `num_nonnulls(a, b, ...)` |
| `SqlFunctions.ClickHouse.multi_if(when(c1, v1), ..., otherwise(v))` | `multiIf(c1, v1, ..., v)` (ClickHouse) |

`num_nulls`/`num_nonnulls` входят в расширенную библиотеку скалярных функций
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions)).
`iif` переносим ([`Iif`](xref:NextORM.Core.ISqlDialect.Iif)), и каждый диалект задаёт своё
нативное написание через [`IIifRenderer.Render`](xref:NextORM.Core.IIifRenderer.Render(System.String,System.String,System.String)); `choose` остаётся только для
SQL Server ([`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose)). Вызов `iif` через
специализированную поверхность `SqlFunctions.SqlServer` по-прежнему работает по наследованию.
C#-тернарник `condition ? a : b` отдельный и всегда рендерит переносимый `case when ... end`.

Помимо этого ClickHouse предоставляет многоветвевную поверхность `multiIf`
([`MultiIf`](xref:NextORM.Core.ISqlDialect.MultiIf),
[`IMultiIfRenderer.Render`](xref:NextORM.Core.IMultiIfRenderer.Render(System.Collections.Generic.IReadOnlyList{System.String},System.Type))): каждая ветвь собирается через
`when(condition, value)`, а завершает вызов `otherwise(value)` (обязательно последним). Остальные
провайдеры используют `case when` — это уже переносимая форма за `iif`/C#-тернарником, поэтому
нативное написание ClickHouse они отвергают.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        e.Id,
        Bucket = SqlFunctions.ClickHouse.multi_if(
            SqlFunctions.ClickHouse.when(e.Id == 1L, "one"),
            SqlFunctions.ClickHouse.when(e.Id == 2L, "two"),
            SqlFunctions.ClickHouse.otherwise("many"))
    })
    .ToList();
```

```sql
-- ClickHouse
select id, multiIf((id = 1), 'one', (id = 2), 'two', 'many') as `Bucket` from complex_entity
```

## Метаданные, checksum и прочие скаляры SQL Server

Остальные скаляры, специфичные для T-SQL, живут на `SqlFunctions.SqlServer` и гейтятся по имени
[`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions) (SQL Server — единственный провайдер,
реализующий флаг, поэтому все остальные бросают `NotSupportedException`). `isdate`/`isnumeric`
намеренно возвращают нативный T-SQL `int` (1/0), **не** `bit`, поэтому проецируются как обычные
целочисленные значения и не материализуются в предикат
`cast(case when ... then 1 else 0 end as bit)`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Rand = SqlFunctions.SqlServer.rand(),
        Seeded = SqlFunctions.SqlServer.rand(42),
        Replaced = SqlFunctions.SqlServer.stuff(e.String, 2, 3, "xy"),
        Check = SqlFunctions.SqlServer.checksum(e.Id, e.String),
        Zipped = SqlFunctions.SqlServer.compress(e.String),
        IsDate = SqlFunctions.SqlServer.isdate(e.String),
        Number = SqlFunctions.SqlServer.str(1.5, 10, 2)
    })
    .ToList();
```

```sql
select rand() as [Rand], rand(42) as [Seeded], stuff(somestring, 2, 3, 'xy') as [Replaced],
       checksum(id, somestring) as [Check], compress(somestring) as [Zipped],
       isdate(somestring) as [IsDate], str(1.5, 10, 2) as [Number]
from complex_entity
```

| C# | SQL |
|---|---|
| `rand()` / `rand(seed)` | `rand()` / `rand(seed)` |
| `stuff(value, start, length, newValue)` (строка) | `stuff(...)` |
| `checksum(values...)` / `binary_checksum(values...)` | `checksum(...)` / `binary_checksum(...)` |
| `compress(value)` (строка и `byte[]`) / `decompress(value)` | `compress(...)` / `decompress(...)` |
| `isdate(value)` / `isnumeric(value)` | `isdate(...)` / `isnumeric(...)` |
| `str(value)` / `str(value, length)` / `str(value, length, decimalPlaces)` | `str(...)` |
| `formatmessage(message, args...)` / `formatmessage(messageId, args...)` | `formatmessage(...)` |

* `rand` без seed **недетерминирован** и вычисляется сервером; фиксированный целый seed делает
  последовательность воспроизводимой. `checksum`/`binary_checksum` — некриптографические контрольные
  суммы для обнаружения изменений, не замена `hashbytes`; им нужен хотя бы один аргумент
  (`checksum()`/`binary_checksum()` бросают), а форма с подстановочным знаком `checksum(*)` не
  открыта.
* `compress`/`decompress` используют GZIP и работают с `varbinary(max)`; `decompress` возвращает
  `null` для некорректного или обрезанного значения.
* `str` фиксирует общую длину (по умолчанию 10) и число знаков после запятой (по умолчанию 0);
  `formatmessage` принимает строку формата или id из `sys.messages` и не более 20 аргументов
  форматирования (больше — `NotSupportedException`).

### Функции метаданных

Поверхность метаданных — выверенное подмножество каталога метаданных T-SQL, сгруппированное A-D.
Каждая функция принимает значения (имена столбцов, id, выражения) и возвращает значение; отсутствующий
объект/столбец/индекс/статистика даёт `null`, как и нативная функция, а метаданные разрешаются
сервером во время выполнения запроса. Необязательные вторые аргументы (id базы, тип объекта) открыты
перегрузками.

| C# | SQL | Группа |
|---|---|---|
| `col_length(table, column)` | `col_length(...)` | A |
| `col_name(tableId, columnId)` | `col_name(...)` | A |
| `ident_incr(table)` / `ident_seed(table)` | `ident_incr(...)` / `ident_seed(...)` | A |
| `index_col(table, indexId, keyId)` | `index_col(...)` | A |
| `object_definition(objectId)` | `object_definition(...)` | A |
| `object_id(name)` / `object_id(name, type)` | `object_id(...)` | A |
| `object_name(id)` / `object_name(id, databaseId)` | `object_name(...)` | A |
| `object_schema_name(id)` / `object_schema_name(id, databaseId)` | `object_schema_name(...)` | A |
| `stats_date(tableId, statsId)` | `stats_date(...)` | A |
| `db_id()` / `db_id(database)` | `db_id(...)` | B |
| `db_name()` / `db_name(databaseId)` | `db_name(...)` | B |
| `original_db_name()` | `original_db_name()` | B |
| `schema_id()` / `schema_id(schema)` | `schema_id(...)` | B |
| `schema_name()` / `schema_name(schemaId)` | `schema_name(...)` | B |
| `type_id(typeName)` / `type_name(typeId)` | `type_id(...)` / `type_name(...)` | B |
| `filegroup_id(name)` / `filegroup_name(id)` | `filegroup_id(...)` / `filegroup_name(...)` | C |
| `file_id(name)` / `file_idex(name)` / `file_name(id)` | `file_id(...)` / `file_idex(...)` / `file_name(...)` | C |
| `current_timezone()` / `current_timezone_id()` | `current_timezone()` / `current_timezone_id()` | D |
| `getansinull()` / `getansinull(database)` | `getansinull(...)` | D |
| `parsename(objectName, piece)` | `parsename(...)` | D |
| `publishingservername()` | `publishingservername()` | D |

`object_definition` возвращает исходный текст T-SQL только при наличии прав (иначе `null`);
`file_idex` отличается от `file_id` тем, что не ограничен текущей базой данных.

### Не открыто

Десять имён области соединения, сессии или инструкции из того же каталога T-SQL намеренно **не**
смоделированы: они сообщают состояние соединения или инструкции, а не вычисляют значение на строку,
поэтому проекция в запросе вводила бы в заблуждение. Они остаются доступными через сырой SQL или
обёртку `[SqlFunction]`, и у каждого есть триггер пересмотра при появлении конкретного построчного
применения.

| Функция | Область |
|---|---|
| `CURRENT_REQUEST_ID` | соединение / запрос |
| `CURRENT_TRANSACTION_ID`, `XACT_STATE` | транзакция |
| `APP_NAME`, `HOST_ID`, `HOST_NAME` | сессия |
| `IDENT_CURRENT` | сессия / таблица |
| `MIN_ACTIVE_ROWVERSION` | база / транзакция |
| `ROWCOUNT_BIG` | инструкция |
| `SCOPE_IDENTITY` | сессия / область |
