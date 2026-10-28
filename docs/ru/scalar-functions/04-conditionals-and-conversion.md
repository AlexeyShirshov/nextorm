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
select ifnull(somestring, '')
 from complex_entity
select (cast(id as double precision) / 2)
 from complex_entity

-- SQL Server
select isnull(somestring, '')
 from complex_entity
select (cast(id as float) / 2)
 from complex_entity

-- PostgreSQL
select coalesce(somestring, '')
 from complex_entity
select (cast(id as double precision) / 2)
 from complex_entity
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
select nullif(nullableint, 0) as "NoZero", greatest(id, 10) as "Hi", least(id, 10) as "Lo"
 from complex_entity
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
select id, multiIf((id = 1), 'one', (id = 2), 'two', 'many') as `Bucket`
 from complex_entity
```

## Null-safe-сравнение (`IS [NOT] DISTINCT FROM`)

`SqlFunctions.Sql.is_not_distinct_from` и `SqlFunctions.Sql.is_distinct_from` — явное, включаемое по
желанию null-safe-сравнение. Оба принимают один общий тип операндов `T` и возвращают non-nullable
`bool`:

```csharp
bool is_not_distinct_from<T>(T left, T right);
bool is_distinct_from<T>(T left, T right);
```

Результат всегда двузначен — это никогда не SQL `NULL`/UNKNOWN, поэтому такое сравнение не может
отбросить строку так, как это делает обычное сравнение:

| `left` | `right` | `is_not_distinct_from` | `is_distinct_from` |
|---|---|---|---|
| `1` | `1` | `true` | `false` |
| `1` | `2` | `false` | `true` |
| `NULL` | `NULL` | `true` | `false` |
| `NULL` | `1` | `false` | `true` |
| `1` | `NULL` | `false` | `true` |

`is_distinct_from` — дополнение `is_not_distinct_from`. Обычные `==` / `!=` **не меняются**: они
сохраняют трёхзначную логику SQL и никогда не перезаписываются на этот помощник (сравнение с `NULL` не
даёт строки в `WHERE`). Помощники нужны, когда «оба `NULL`» должно считаться равенством.

```csharp
int? previous = 42;

var rows = await dataContext.From<ComplexEntity>()
    .Where(e => SqlFunctions.Sql.is_not_distinct_from(e.Int, previous))
    .Select(e => new { e.Id })
    .ToListAsync();
```

| Провайдер | `is_not_distinct_from(a, b)` | `is_distinct_from(a, b)` | Реализация |
|---|---|---|---|
| PostgreSQL | `(a is not distinct from b)` | `(a is distinct from b)` | нативная |
| SQLite | `(a is b)` | `(a is not b)` | нативная |
| MySQL, MariaDB | `(a <=> b)` | `(not (a <=> b))` | нативная |
| SQL Server | `((a is not null and b is not null and a = b) or (a is null and b is null))` | `(not ((a is not null and b is not null and a = b) or (a is null and b is null)))` | эмуляция |
| ClickHouse | `((a is null and b is null) or (a is not null and b is not null and a = b))` | `(not ((a is null and b is null) or (a is not null and b is not null and a = b)))` | эмуляция |

Логическую проекцию оформляет диалект: SQL Server легализует предикат как
`cast(case when ... then 1 else 0 end as bit)`, остальные провайдеры проецируют его напрямую (в
ClickHouse охраняемый предикат уже имеет тип `UInt8`).

Свёртка, типизация и параметры ведут себя детерминированно:

* два литеральных `NULL` сворачиваются в `1 = 1` (`is_not_distinct_from`) или `1 = 0`
  (`is_distinct_from`);
* ровно один литеральный `NULL` понижается до `is null` / `is not null` на другом операнде;
* **захваченный** операнд остаётся одним параметром команды, а его SQL-тип берётся из маппинга
  сравниваемой колонки (или её конвертера), а не из значения во время выполнения — захваченный `NULL`
  работает и никогда не специализирует кэшированный SQL;
* два статически non-nullable CLR-операнда **типа значения**, не читающие источник запроса (например,
  две захваченные локальные `int`), могут понижаться до `=` / `<>`; nullable-значение, колонка,
  навигация и операнд, расширяемый до NULL внешним соединением, всегда сохраняют null-safe-рендеринг.
  Nullable-аннотация ссылочного типа **не** является доказательством на этапе выполнения: ссылка,
  объявленная non-nullable, но фактически содержащая `NULL`, всё равно идёт null-safe-путём.

Единый тип операндов `T` выводится, когда оба аргумента уже имеют его. У литерала `null` своего типа
нет, поэтому укажите его явно — `is_not_distinct_from<int?>(null, null)` — либо приведите
(`(int?)null`). Операнды с разными эффективными CLR-типами (после разворачивания nullable)
отклоняются с `NotSupportedException` и `Null-safe comparison requires compatible operand types.`
вместо молчаливого преобразования. В SQL Server SQL-тип null-операнда должен выводиться из метаданных
(сначала маппинг связанной колонки/конвертера, затем CLR-тип операнда); операнд без маппинга падает до
выполнения с `Null-safe comparison requires a SQL Server type mapping for null operands.`

In-memory-провайдер применяет ту же таблицу истинности — оба `NULL` равны, ровно один `NULL` не равен,
иначе `EqualityComparer<T>.Default` — поэтому ссылка `NULL` никогда не разыменовывается. Равенство
строк в CLR — **не** коллация базы: in-memory-провайдер сравнивает строки CLR, а SQL-провайдер — с
коллацией колонки, поэтому равенство при разных коллациях не обещается.

## BASE64 и разбор decimal

`SqlFunctions.Sql.base64_encode(byte[]?)` и `base64_decode(string?)` рендерят стандартный BASE64 по
RFC 4648 (`+`, `/`, паддинг `=`, без переносов строк):

| C# | SQL Server 2025 | PostgreSQL | MySQL / MariaDB |
|---|---|---|---|---|
| `base64_encode(value)` | `BASE64_ENCODE(value)` | `replace(encode(value, 'base64'), E'\n', '')` | `CAST(REPLACE(REPLACE(TO_BASE64(value), CHAR(13), ''), CHAR(10), '') AS CHAR)` |
| `base64_decode(value)` | `BASE64_DECODE(value)` | `decode(value, 'base64')` | `FROM_BASE64(value)` |

SQL NULL (литерал, параметр или nullable-столбец) даёт SQL NULL; пустой вход даёт пустой результат.
Невалидный или URL-safe BASE64 находится вне переносимого входного домена, и ошибка провайдера не
маскируется. ClickHouse, SQLite и in-memory провайдер отклоняют оба члена с
`The {name} function is not supported by this provider.` В ClickHouse есть нативные `base64Encode`/
`base64Decode`, но nextorm не открывает их в этом срезе: адаптер привязывает аргумент `byte[]` как
`Array(UInt8)` и читает результат `String` как `string`, поэтому сигнатуру `byte[]` соблюсти нельзя.
В SQL Server `base64_decode` принимает `varchar`, поэтому столбец `nvarchar` требует явного приведения.

`SqlFunctions.SqlServer.parse(string?)` и `try_parse(string?)` разбирают `decimal(38,18)` с
фиксированной культурой `en-US` — никогда не с языком сессии и никогда не с аргументом культуры.
`parse` отдаёт серверную ошибку на неверном тексте (`Microsoft.Data.SqlClient.SqlException`),
`try_parse` возвращает NULL. Оба возвращают `decimal?`, и неверный текст никогда не уходит в
клиентский разбор.

## SQL Server TRY_CAST и TRY_CONVERT

`SqlFunctions.SqlServer.try_cast<T>(value)` и `try_convert<T>(value[, style])` рендерят T-SQL
`TRY_CAST`/`TRY_CONVERT`: некорректное, переполняющее или выходящее за диапазон значение даёт SQL NULL
вместо ошибки. Перегрузки разделены по форме цели:

| C# | SQL |
|---|---|
| `try_cast<T>(value)` | `try_cast(value as <type>)` |
| `try_cast(value)` / `try_convert(value)` | `try_cast(value as nvarchar(4000))` / `try_convert(nvarchar(4000), value)` |
| `try_convert<T>(value)` | `try_convert(<type>, value)` |
| `try_convert<T>(value, style)` / `try_convert(value, style)` | `try_convert(<type>, value, style)` |

Цель — **закрытое отображение из CLR-типа `T`**, никогда не имя CLR-типа:

| CLR-цель | Тип SQL Server |
|---|---|
| `byte` / `short` / `int` / `long` | `tinyint` / `smallint` / `int` / `bigint` |
| `float` / `double` | `real` / `float(53)` |
| `decimal` | `decimal(38,10)` |
| `bool` | `bit` |
| `Guid` | `uniqueidentifier` |
| `DateTime` / `DateTimeOffset` | `datetime2(7)` / `datetimeoffset(7)` |
| `TimeSpan` | `time(7)` |
| `string` (негенерические перегрузки) | `nvarchar(4000)` |

Цель вне отображения отклоняется до подготовки команды: `The TRY_CAST function cannot map CLR target
'MyType' to a supported SQL Server conversion type.` Длины и точности фиксированы — аргумента длины или
точности пока нет — и сервер применяет своё нативное усечение: значение длиннее 4000 символов
преобразуется ровно в 4000 символов (`try_cast(<5000 символов> as nvarchar(4000))` даёт 4000 символов /
8000 байт), а не ошибку.

Аргумент style — это нативный стиль `datetime`/`float`/money, а не селектор цели. Его отсутствие и
передача NULL-стиля — **разные перегрузки**: NULL-стиль рендерится как `CAST(NULL AS int)`, и тогда SQL
Server возвращает NULL **независимо от значения** — `try_convert(int, '42', CAST(NULL AS int))` равно
NULL, а не `42`.

Неверные *значения* становятся NULL; недопустимое *преобразование* остаётся ошибкой движка, потому что
рендер никогда не переписывает цель. Сегодня закрытое отображение не открывает такой цели, и `xml`
намеренно не является публичной целью — «сырой» оператор `TRY_CAST(1 AS xml)` даёт `Msg 529` («Explicit
conversion from data type int to xml is not allowed.»), и именно это унаследует любая будущая цель такого
рода. Обе функции доступны на любом объявлении SQL Server (15, 16, 17 и неверсионированном), так как
`TRY_CAST`/`TRY_CONVERT` появились до 2022; любой другой провайдер отклоняет член с `The TRY_CAST function
is not supported by this provider.`

## Предикаты типов SQL Server

`SqlFunctions.SqlServer.isdate(value)` и `isnumeric(value)` рендерятся как `isdate(...)` / `isnumeric(...)`
и гейтятся по имени через [`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions) (только
SQL Server):

| C# | SQL |
|---|---|
| `isdate(value)` / `isnumeric(value)` | `isdate(...)` / `isnumeric(...)` |

Они намеренно возвращают нативный T-SQL `int` (1/0), **не** `bit`, поэтому проецируются как обычные
целочисленные значения и не материализуются в предикат
`cast(case when ... then 1 else 0 end as bit)`.

## Приведение типов (ClickHouse)

Поверхность `to*`/`accurateCast`/`reinterpretAs` ClickHouse типизирована на `SqlFunctions.ClickHouse` и
проходит через общий шлюз скалярных функций, поэтому все остальные диалекты её отклоняют. Цели
точные: `Int8` расширяется до `short`/`Int16`, а `UInt16` — до `int`, потому что в общем построителе
строк нет аксессоров `sbyte`/`ushort`; `to_decimal32`/`to_decimal64` принимают проверенный
compile-time масштаб, а `accurate_cast_*` поставлен как двенадцать типизированных проверяемых целей
(базовая, `_or_null`, `_or_default`), а не схлопывается в плечо `to*`.

| Функция | SQL |
|---|---|
| `SqlFunctions.ClickHouse.to_string(v)` / `to_bool(v)` / `to_fixed_string(v, n)` / `to_low_cardinality(v)` | `toString` / `toBool` / `toFixedString` / `toLowCardinality` |
| `SqlFunctions.ClickHouse.to_int8(v)` … `to_int64(v)` | `toInt8` … `toInt64` |
| `SqlFunctions.ClickHouse.to_uint8(v)` … `to_uint64(v)` | `toUInt8` … `toUInt64` |
| `SqlFunctions.ClickHouse.to_float32(v)` / `to_float64(v)` | `toFloat32` / `toFloat64` |
| `SqlFunctions.ClickHouse.to_decimal32(v, scale)` / `to_decimal64(v, scale)` | `toDecimal32` / `toDecimal64` |
| `SqlFunctions.ClickHouse.accurate_cast_int8(v)` … плюс `_or_null` / `_or_default` для каждой цели | `accurateCast` / `accurateCastOrNull` / `accurateCastOrDefault` |
| `SqlFunctions.ClickHouse.reinterpret_as_uint8` … `reinterpret_as_uint64`, `_int8` … `_int64`, `_float32`/`_float64`, `_date`/`_date_time`/`_string`/`_fixed_string`/`_uuid` | `reinterpretAs*` |

`reinterpretAs*` сохраняет little-endian битовый шаблон. 128/256-битные целые, `Decimal128`/
`Decimal256` и одиннадцать единиц `to_interval_*` остаются отклонёнными (нет точного CLR-носителя —
`BigInteger`, 96-битная мантисса `System.Decimal` и типы `Interval*`, существующие только в
ClickHouse соответственно). См.
[ClickHouse-specific SQL › Пока не поддерживается](../guide/provider-specific-sql/clickhouse.md#пока-не-поддерживается).
