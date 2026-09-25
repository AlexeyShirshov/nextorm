# TODO: Пробелы функций SQL Server (`PATINDEX`, `QUOTENAME`, `SOUNDEX`, `DIFFERENCE`, `TRANSLATE`, `FORMAT`, `DATENAME`, `DATE_BUCKET`, `HASHBYTES`, JSON-агрегаты, …)
> Tracking issue: [#83](https://github.com/AlexeyShirshov/nextorm/issues/83).

> **Статус: отгружено (shipped) для SQL Server-only имён.** Реализовано как per-name capability-объект
> `ISqlServerFunctions` на `ISqlDialect` (default `null`), члены `SqlServerFunctions`, транслятор
> `SqlServerScalarFunctionTranslator`; in-memory — явный `NotSupportedException`.
> Публичные доки: [Scalar functions](../../guide/11-scalar-functions.md#provider-specific-functions)
> (+RU), [SQL Server provider](../../providers/sqlserver.md) (+RU).
>
> **Решения по границам (важно):**
> - `ASCII`/`CHAR`/`TRANSLATE` **не дублируются**: уже в `CommonFunctions` (кросс-провайдерный todo
>   отгружен, `IScalarFunctions`). `LOG10` **не заводится**: достижим через `Math.Log10` (BCL-путь,
>   tier a) — тот же YAGNI-аргумент, которым в кросс-провайдерном todo сняты `mod`/`log10`/`power`.
> - `JSON_ARRAY`/`JSON_OBJECT`/`JSON_*AGG`/`JSON_CONTAINS`/`JSON_PATH_EXISTS` имеют аналоги на ≥2
>   провайдерах, поэтому в перспективе — `CommonFunctions`; в этом todo они закрываются **SQL
>   Server-only** формой (кандидаты на промоушен в отдельной волне SQL/JSON-конструкторов).
> - per-name capability выбран вместо семейного флага: `SQUARE`/`NEWSEQUENTIALID`/`UNICODE`/`NCHAR`
>   держатся отдельно от промотируемых имён.

> Рабочий план (design RFC). Источник: таблица «Summary: highest-value gaps»,
> строка **SQL Server**, в [`sql-function-coverage-gap.md`](sql-function-coverage-gap.md)
> (§«SQL Server»); новый пункт §4 в
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md).
> Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

## 1. Пункт и цель

- **Фича:** дополнить `SqlServerFunctions` недостающими нативными T-SQL функциями, документированными
  в gap-анализе: `PATINDEX`, `QUOTENAME`, `SOUNDEX`, `DIFFERENCE`, `STRING_ESCAPE`, `TRANSLATE`,
  `FORMAT`, `ASCII`/`CHAR`/`UNICODE`/`NCHAR`, `ACOS`/`ASIN`/`ATAN`/`ATN2`/`COT`/`DEGREES`/`RADIANS`/
  `PI`/`LOG10`/`SQUARE`, `DATENAME`, `DATE_BUCKET`, `HASHBYTES`, `NEWSEQUENTIALID`,
  `JSON_ARRAY`/`JSON_OBJECT`/`JSON_ARRAYAGG`/`JSON_OBJECTAGG`/`JSON_CONTAINS`/`JSON_PATH_EXISTS`.
- **Критерий приёмки:** каждая функция рендерится `SqlServerFunctions` в нативную форму; прочие
  провайдеры отклоняют её; SQL-gen тесты + SQL Server-интеграция (`SqlServerSpecificTests`).
- **Разграничение с кросс-провайдерным todo:** там, где форма совпадает по семантике у ≥2
  провайдеров, член уходит в `CommonFunctions`
  ([`todo_cross_provider_scalar_functions.md`](todo_cross_provider_scalar_functions.md)), а не
  дублируется на SQL Server. Этот todo закрывает **SQL Server-only** имена и специфичные для T-SQL
  сигнатуры/семантику (см. матрицу ниже).

## 2. Текущее состояние (проверено по коду)

`SqlServerFunctions` (`src/nextorm.core/Query/SqlFunctions.SqlServer.cs`) сегодня содержит только:
`json_value`/`json_query`/`json_modify`/`isjson`, `choose`, `string_split`, `openjson`,
`containstable`/`freetexttable`, `xml_value`/`xml_query`/`xml_exist`/`xml_nodes`.
Строковых/математических/датовых скаляров и JSON-агрегатов нет. Гейты: `SupportsTextJson`,
`SupportsChoose`, `SupportsTableFunction`. `FOR JSON`/`FOR XML` уже реализованы (`ForJson`/`ForXml`).

## 3. Матрица провайдеров (провайдер × форма)

Источники: Microsoft Learn T-SQL function reference (string, mathematical, date-and-time, json,
cryptographic, bit-manipulation, system) — проверено MCP `mslearn` (`CONCAT_WS`/`TRANSLATE` — 2017+,
`JSON_ARRAY`/`JSON_OBJECT` — 2022+, `JSON_ARRAYAGG`/`JSON_OBJECTAGG`/`JSON_CONTAINS` — 2025+,
`DATE_BUCKET`/`SQUARE` — 2022+); PostgreSQL 18; MySQL 8.4 / MariaDB; SQLite `lang_corefunc`/`json1`;
ClickHouse function reference.

| Функция | SQL Server | Аналоги у других | Решение |
|---|---|---|---|
| `PATINDEX(pattern, expr)` | `PATINDEX` | PG `position`/MySQL `LOCATE`/`INSTR` — **другая** семантика (`%`-wildcards, не regex) | SQL Server-only |
| `QUOTENAME(s[, quote])` | `QUOTENAME` | PG `quote_ident`/`quote_literal`; MySQL `QUOTE` (иначе) | SQL Server-only |
| `SOUNDEX` | collation-sensitive | MySQL/MariaDB `SOUNDEX`, PG `soundex` (fuzzystrmatch), ClickHouse `soundex`, SQLite `soundex` (флаг) | **кросс-провайдер** (кандидат) — пока SQL Server-only |
| `DIFFERENCE(a,b)` | `DIFFERENCE` | PG `difference` (fuzzystrmatch) — аналог | SQL Server-only |
| `STRING_ESCAPE(s,'json')` | `STRING_ESCAPE` | нет прямого аналога | SQL Server-only |
| `TRANSLATE` | `TRANSLATE` (2017+) | PG/ClickHouse `translate`; MySQL/MariaDB — нет | **кросс-провайдер** (см. отдельный todo) |
| `FORMAT(value, fmt[, culture])` | `FORMAT` (.NET format) | PG `to_char`, MySQL `DATE_FORMAT`/`FORMAT`, SQLite `strftime`/`printf`, ClickHouse `formatDateTime`/`format` | SQL Server-only (языки шаблонов несовместимы, gap §5.15) |
| `ASCII`/`CHAR`/`UNICODE`/`NCHAR` | есть | PG `ascii`/`chr`; MySQL/MariaDB `ASCII`/`CHAR`; SQLite `unicode`/`char`; ClickHouse `char` | **кросс-провайдер** (`char`/`ascii`), `UNICODE`/`NCHAR` — SQL Server-only |
| `ACOS`/`ASIN`/`ATAN`/`ATN2`/`COT`/`DEGREES`/`RADIANS`/`PI`/`LOG10`/`SQUARE` | есть (`ATN2` = `atan2`) | почти все кросс-провайдерны (`Math.*`/PG-обёртки) | **кросс-провайдер** (вторая волна) + SQL Server-only `SQUARE` |
| `DATENAME(part, date)` | `DATENAME` | PG `to_char`, MySQL `DAYNAME`/`MONTHNAME` — иначе | SQL Server-only |
| `DATE_BUCKET(part, width, date[, origin])` | `DATE_BUCKET` (2022+) | PG `date_bin`, ClickHouse `toStartOfInterval` | **кросс-провайдер** (кандидат) — пока SQL Server-only |
| `HASHBYTES('SHA2_256'/'SHA2_512'/'SHA3_*', data)` | `HASHBYTES` | PG `sha256`/`digest`, MySQL `SHA2`, ClickHouse `SHA*` | SQL Server-only (кросс-провайдерный hash-поверхность — follow-up §Cross-provider) |
| `NEWSEQUENTIALID()` | `NEWSEQUENTIALID` | PG `gen_random_uuid` — иная семантика | SQL Server-only |
| `JSON_ARRAY`/`JSON_OBJECT` | `JSON_ARRAY`/`JSON_OBJECT` (2022+) | PG `json_build_array`/`json_array`, MySQL/MariaDB `JSON_ARRAY`/`JSON_OBJECT`, SQLite/sql JSON1, ClickHouse `toJSONString` | **кросс-провайдер** (SQL/JSON-конструкторы) |
| `JSON_ARRAYAGG`/`JSON_OBJECTAGG` | 2025+ | PG `json_agg`/`json_object_agg`, MySQL/MariaDB, SQLite JSON1 | **кросс-провайдер** (SQL/JSON-агрегаты) |
| `JSON_CONTAINS`/`JSON_PATH_EXISTS` | 2025+ | PG `@>`/`jsonb_path_exists`, MySQL/MariaDB `JSON_CONTAINS` | **кросс-провайдер** (кандидат) |

**Единообразие провайдеров:** в этом todo остаются только ячейки, где ≥2 провайдера **не** выражают
функцию той же формой: `PATINDEX`, `QUOTENAME`, `DIFFERENCE`, `STRING_ESCAPE`, `FORMAT`, `DATENAME`,
`HASHBYTES`, `NEWSEQUENTIALID`, `UNICODE`, `NCHAR`, `SQUARE`. Функции, отмеченные **кросс-провайдер**,
реализуются в общем `SqlFunctions.Sql` (или в `todo_cross_provider_scalar_functions.md`), чтобы не
дублировать поверхность; здесь они перечислены для полноты и как зависимости.

## 4. Ближайший CLR-аналог и тир

- **tier (b)** для всех: точного BCL-аналога нет.
- `FORMAT` — не путать с уже существующим CLR-format-транслятором (`IStringFormatFunctions`):
  это нативная T-SQL `FORMAT` с .NET-строкой; держать на `SqlServerFunctions` отдельно.
- `NEWSEQUENTIALID` валиден только в `DEFAULT`/`INSERT`-контексте (не в обычном `SELECT`) — отметить
  в XML-doc и, при необходимости, запретить вне write-контекста.

## 5. Дизайн и публичный API

```csharp
public static int? patindex(string? pattern, string? expression);
public static string? quotename(string? value, string? quote = null);
public static string? soundex(string? value);
public static int? difference(string? a, string? b);
public static string? string_escape(string? value, string? type = "json");
public static int? unicode(string? value);
public static string? nchar(int code);
public static string? datename(string? datepart, DateTime? date);
public static DateTime? date_bucket(string? datepart, int width, DateTime? date, DateTime? origin);
public static byte[]? hashbytes(string? algorithm, byte[]? data);
public static Guid? newsequentialid();
public static double? square(double? value);
public static string? json_array(params object?[] values);
public static string? json_object(params object?[] keyValuePairs);
public static string? json_arrayagg<T>(T? value);
public static string? json_objectagg<TKey, TValue>(TKey? key, TValue? value);
public static bool? json_contains(string? json, string? path, string? searchValue);
public static bool? json_path_exists(string? json, string? path);
```

- `json_array`/`json_object`/`json_arrayagg`/`json_objectagg`/`json_contains`/`json_path_exists` —
  если решено промотировать в `CommonFunctions`, объявляются там, а `SqlServerFunctions` не дублирует
  (см. §3 и отдельный todo).
- `hashbytes` принимает строку алгоритма (`SHA2_256`/`SHA2_512`/`SHA3_256`/…), валидировать набор.

## 6. Диалект-план

- Новый per-name capability-объект `ISqlServerFunctions { bool Supports(string name); string Render(string name, IReadOnlyList<string> args); }`
  на `ISqlDialect` (default `null`), по образцу `ISessionInfoFunctions`; SQL Server реализует.
  Это позволяет `SQUARE`/`NEWSEQUENTIALID`/`UNICODE`/`NCHAR` держать отдельно от промотируемых имён
  и не гейтить семейным флагом.
- Транслятор: `BuiltinFunctionTranslator.cs` / новый `SqlServerScalarFunctionTranslator.cs`.
- `FORMAT` рендерить как `FORMAT(value, 'fmt')` (с опциональным culture, если понадобится).

## 7. Этапы внедрения

1. Строковые однопровайдерные: `patindex`, `quotename`, `difference`, `string_escape`, `unicode`,
   `nchar`; SQL-gen тесты.
2. Датовые: `datename`, `date_bucket`; SQL-gen тесты.
3. Бинарные/системные: `hashbytes`, `newsequentialid`, `square`; SQL-gen тесты.
4. JSON: конструкторы/агрегаты/`json_contains`/`json_path_exists` — после решения о промотировании;
   интеграция.
5. Документация EN+RU; обновить `sql-function-coverage-gap.md`.

## 8. План тестов

- SQL-gen: `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs` (все имена) + `SqlServerDialectTests.cs`
  (per-name `Supports`/`Render`).
- Гейт: `tests/nextorm.<other>.tests` — `SqlServerFunctions.patindex`/`hashbytes` → `NotSupportedException`.
- Интеграция: `tests/nextorm.integration.tests/SqlServerSpecificTests.cs` — `PATINDEX`/`SOUNDEX`/
  `DATENAME`/`DATE_BUCKET`/`HASHBYTES`/JSON-агрегаты на реальной БД (SQL Server 2025 в контейнере).
- Coverage: `nextorm.sqlserver` входит в `coverage.settings.xml` — отчитаться до/после.

## 9. Открытые вопросы

1. Какие из «кросс-провайдер»-ячеек (SOUNDEX, DATE_BUCKET, JSON-конструкторы/агрегаты, `json_contains`)
   промотировать сразу, а какие оставить SQL Server-only до второй волны?
2. `hashbytes`: узкий набор (`SHA2_256`/`SHA2_512`) или весь, включая `SHA3_*` (2025) и legacy
   (`MD5`/`SHA1`)?
3. `FORMAT` с `culture` — поддерживать или только инвариантный?
4. `NEWSEQUENTIALID` вне DEFAULT — разрешать (риск ошибки сервера) или запрещать в валидаторе?
5. `QUOTENAME` второй аргумент (кавычка) — поддержать сейчас или опустить.

## 10. Файлы к изменению

- Правки: `src/nextorm.core/Query/SqlFunctions.SqlServer.cs`,
  новый `src/nextorm.core/Visitors/SqlServerScalarFunctionTranslator.cs` (или ветка
  `BuiltinFunctionTranslator.cs`), `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`,
  `SqlDialectBase.cs`, `DialectCapabilities.cs`, `src/nextorm.sqlserver/SqlServerDialect.cs`.
- Тесты: `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`/`SqlServerDialectTests.cs`,
  `tests/nextorm.integration.tests/SqlServerSpecificTests.cs`, негативные — `tests/nextorm.<other>.tests`.
- Доки: `docs/guide/provider-specific/sqlserver.md` (+RU), `docs/guide/11-scalar-functions.md` (+RU),
  `docs/providers/sqlserver.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`, `docs/specs/roadmap/sql-function-coverage-gap.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **нужен пересмотр — 2 блокера** (статическая форма §5 + параллельный `ISqlServerFunctions`).

- **[DRY] 🟡** `:104` предлагает новый `ISqlServerFunctions { bool Supports(string); string Render(string, IReadOnlyList<string>); }` — 1:1 повторяет отгруженный `NextORM.Core.IScalarFunctions` (`DialectCapabilities.cs:340`), уже реализованный `SqlServerScalarFunctions` (`SqlServerDialect.cs:804`, отдаётся `:590`). Fix: расширять существующий per-name объект или назвать 2-го потребителя (инварианты 1/3).
- **[TYPE] 🟡** `:77-94` объявляют `public static int? patindex(...)` и т.д., тогда как провайдерные члены — **instance** внутри вложенного класса (`SqlFunctions.SqlServer.cs:21`, маркер `SqlFunctions.cs:43`). Fix: instance-члены на `SqlServerFunctions`.
- **[DRY] 🟡** `:89-94` `json_array`/`json_object`/`json_arrayagg`/`json_objectagg`/`json_contains`/`json_path_exists` объявлены здесь **и** в `todo_postgres_function_gaps.md:113-117`, **и** в `todo_sqlite_function_gaps.md:114-117`; `ExtendedScalarFunctionTranslator.cs:19-60` матчит по `node.Method.Name` без `DeclaringType` → коллизия. Fix: решить один раз в `CommonFunctions` (правило §3) и протащить declaring-type в диспетчер (как `JsonSqlTranslator.cs:32`).
- **[DRY] 🟡** `:108` «новый `SqlServerScalarFunctionTranslator.cs` (или ветка `BuiltinFunctionTranslator.cs`)» — провайдерные скаляры уже имеют дом `ExtendedScalarFunctionTranslator.cs:17`. Fix: указать существующий владелец, не плодить 4-й скалярный транслятор.
- **[TYPE] ℹ️** `:86` `hashbytes(string? algorithm, byte[]? data)`: алгоритм обязан быть compile-time константой (образец `SqlLiteral.TryGetConstantString`, `ScalarFunctionTranslator.cs:121`). Fix: зафиксировать требование в XML-doc/валидаторе.
- **[TYPE] ℹ️** `:85` `date_bucket(datepart,width,date,origin)` делает `origin` обязательным при наличии 3-арг формы T-SQL (PG-план даёт 3-арг `date_bin`, `todo_postgres_function_gaps.md:102`). Fix: 3-арг перегрузка либо документировать расхождение.
- **[SRP] ℹ️** `:137` (open Q4) `newsequentialid()` вне `DEFAULT` — форма API не определена. Deferred: решить валидатором до кодирования.
