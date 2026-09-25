# TODO: Пробелы функций PostgreSQL (`sha224/384/512`, `regexp_substr`, `make_*`, `age`, `date_bin`, `current_setting`, последовательности, SQL/JSON)
> Tracking issue: [#81](https://github.com/AlexeyShirshov/nextorm/issues/81).

> **Статус: отгружено (shipped).** Публичная поверхность: `SqlFunctions.Postgres` — хэши
> `sha224`/`sha384`/`sha512`, `regexp_substr`, `make_time`/`make_timestamp`, `age`/`date_bin`,
> `current_setting`/`set_config`, `nextval`/`setval`/`currval`/`lastval`, SQL/JSON
> `json_array`/`jsonb_array`/`json_value`/`json_query`/`json_exists(json,path,fromJsonPath)`.
> `make_date` закрыт существующим кросс-провайдерным `CommonFunctions.date_from_parts` (рендерит
> `make_date(...)`), дубликат не заводился; `jsonb_array` рендерится `json_array(... returning jsonb)`
> (в PostgreSQL нет функции `jsonb_array`). Документация: [JSON и JSONB](../../guide/18-json.md) и
> [Scalar functions](../../guide/11-scalar-functions.md) (+RU). См. §9 — закрытые вопросы.

> Рабочий план (design RFC). Источник: таблица «Summary: highest-value gaps»,
> строка **PostgreSQL**, в [`sql-function-coverage-gap.md`](sql-function-coverage-gap.md)
> (§«PostgreSQL»); новый пункт §4 в
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md).
> Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

## 1. Пункт и цель

- **Фича:** закрыть выбранные пробелы `PostgresFunctions` — PG-only функции, которые уже
  документированы как отсутствующие:
  - хэши: `sha224`/`sha384`/`sha512` (сегодня есть `md5`, `sha256`, `digest`);
  - regexp: `regexp_substr` (есть `regexp_replace`/`_like`/`_count`/`_instr`/`_matches`/`_split_to_*`);
  - конструкторы даты: `make_date`/`make_time`/`make_timestamp` (есть `make_interval`);
  - интервалы/бинаризация: `age`, `date_bin`;
  - runtime-настройки: `current_setting`/`set_config`;
  - последовательности: `nextval`/`setval`/`currval`/`lastval`;
  - SQL/JSON: конструктор `json_array`/`jsonb_array` и скаляры `json_value`/`json_query`/`json_exists`;
    `num_nulls`/`num_nonnulls` уже есть.
- **Критерий приёмки:** каждое имя рендерится `PostgresFunctions` в нативную форму PostgreSQL и
  отклоняется прочими провайдерами с понятным `NotSupportedException`; SQL-gen тесты + PG-интеграция
  (`PostgresSpecificTests`).
- **Не входит:** полный PG-инвентарь из §«PostgreSQL» (полнотекстовый `ts_*`, `jsonb_path_*_tz`,
  array/aggregate/session-мелочи) — этот todo берёт «highest-value» подмножество; остальное остаётся
  в `sql-function-coverage-gap.md` как справочник.

## 2. Текущее состояние (проверено по коду)

| Функция | Сейчас в `PostgresFunctions` |
|---|---|
| `sha224`/`sha384`/`sha512` | нет; есть `md5`, `sha256`, `digest` (`SqlFunctions.Postgres.cs:359/383/368`) |
| `regexp_substr` | нет; есть `regexp_replace`/`regexp_like`/`regexp_count`/`regexp_instr`/`regexp_matches`/`regexp_split_to_array` |
| `make_date`/`make_time`/`make_timestamp` | `make_date` — через `CommonFunctions.date_from_parts` (рендерит `make_date`); `make_time`/`make_timestamp` добавлены; есть `make_interval` |
| `age`/`date_bin` | нет |
| `current_setting`/`set_config` | нет |
| `nextval`/`setval`/`currval`/`lastval` | нет |
| `json_array`/`jsonb_array` | нет; есть `json_build_array`/`jsonb_build_array` |
| SQL/JSON `json_value`/`json_query`/`json_exists` | нет PG-варианта; `json_get*`/`json_exists` (путь `text`) есть |
| `num_nulls`/`num_nonnulls` | **есть** (`:452/:455`) |

Гейт: весь `PostgresFunctions` отклоняется прочими провайдерами через
`SupportsExtendedScalarFunctions` (PG-only); для новых имён новых флагов не требуется, если они не
промотируются в кросс-провайдерную поверхность.

## 3. Матрица провайдеров (провайдер × форма)

Источники: PostgreSQL 18 function reference (`functions-string`, `functions-math`,
`functions-datetime`, `functions-json`, `functions-sequence`, `functions-info`); Microsoft Learn
T-SQL (string/mathematical/date/json/sequence `NEXT VALUE FOR`); MySQL 8.4 / MariaDB built-in
functions; SQLite `lang_corefunc` / `lang_datefunc` / `lang_mathfunc` / `json1`; ClickHouse
function reference (hash/date/JSON).

| Функция | PostgreSQL | SQL Server | MySQL | MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|---|
| `sha224`/`sha384`/`sha512` | `sha256`-family, `sha224/384/512` | `HASHBYTES('SHA2_256'/'SHA2_512')` (нет 224/384) | `SHA2(x,224/384/512)` | `SHA2` | `SHA224/384/512` | — (нет) |
| `regexp_substr` | `regexp_substr` | `REGEXP_SUBSTR` (2025) | `REGEXP_SUBSTR` | `REGEXP_SUBSTR` | `extract`/`extractAll` | — (нет) |
| `make_date` | `make_date` | `DATEFROMPARTS` | `MAKEDATE` | `MAKEDATE` | `makeDate` | — (нет) |
| `make_time` | `make_time` | `TIMEFROMPARTS` | `MAKETIME` | `MAKETIME` | `makeDateTime` | — (нет) |
| `make_timestamp` | `make_timestamp` | `DATETIMEFROMPARTS`/`DATETIME2FROMPARTS` | `TIMESTAMP(...)` | `TIMESTAMP(...)` | `makeDateTime64` | — (нет) |
| `age` | `age` | — (только `DATEDIFF`) | — | — | `age` | — |
| `date_bin` | `date_bin` | `DATE_BUCKET` | — | — | `toStartOfInterval` | — |
| `current_setting`/`set_config` | `current_setting`/`set_config` | `SESSION_CONTEXT` (не тот же) | `@@var`/`SET` | `@@var`/`SET` | `getSetting`/`--` | `—` (нет) |
| `nextval`/`setval`/`currval`/`lastval` | `nextval`/`setval`/`currval`/`lastval` | `NEXT VALUE FOR` (только next) | нет (AUTO_INCREMENT) | `NEXT VALUE FOR`/`NEXTVAL`/`SETVAL`/`LASTVAL` | нет | нет |
| `json_array`/`jsonb_array` | `json_array`/`jsonb_array` (SQL/JSON) | `JSON_ARRAY` | `JSON_ARRAY` | `JSON_ARRAY` | `--` (`[a,b]` через `toJSONString`) | `json_array` |
| SQL/JSON `json_value`/`json_query`/`json_exists` | SQL/JSON скаляры | `JSON_VALUE`/`JSON_QUERY`/`JSON_PATH_EXISTS` | `JSON_VALUE`/`JSON_QUERY` | `JSON_VALUE`/`JSON_QUERY` | `json_value`/`json_query`/`json_exists` | `json_extract` |

**Единообразие провайдеров:** большинство имён имеют чужие аналоги, но **другая сигнатура/семантика**
(SQL/JSON path vs PG `jsonpath`, `DATEDIFF` vs `age`, `SESSION_CONTEXT` vs `current_setting`), поэтому
решение — **PG-only поверхность** (`PostgresFunctions`), прочие провайдеры гейтятся. Промотирование в
`CommonFunctions` (вторая волна) возможно для пар, где семантика совпадает: `regexp_substr`,
`sha512`, `make_date`/`make_time`/`make_timestamp`, `nextval` — но это отдельный workstream и требует
per-name capability-объекта. Здесь фиксируем PG-only.

## 4. Ближайший CLR-аналог и тир

- **tier (b)** для всех: точных BCL-аналогов нет (`SHA224`/`SQL/JSON`/`nextval` — серверные понятия).
- `sha*` принимают `byte[]` (как `sha256` сегодня) и возвращают `byte[]`; строковый вариант — через
  `digest`/`convert_to`.
- `nextval`/`setval` — сигнатуры с именем последовательности (`string`) и `bigint`; параметры
  `regclass`/`bigint`.
- `json_value`/`json_query`/`json_exists` PG используют путь `jsonpath`, а не `text`; перегрузки не
  смешивать с существующими `json_exists(object, string)` — новые имена/сигнатуры пометить в плане
  API, чтобы не было двусмысленного резолва (см. открытые вопросы).

## 5. Дизайн и публичный API

```csharp
// хэши
public static byte[]? sha224(byte[]? data);
public static byte[]? sha384(byte[]? data);
public static byte[]? sha512(byte[]? data);
// regexp
public static string? regexp_substr(string? value, string? pattern);
public static string? regexp_substr(string? value, string? pattern, string? flags);
// дата/время
public static DateTime? make_date(int year, int month, int day);
public static TimeSpan? make_time(int hour, int minute, double second);
public static DateTime? make_timestamp(int year, int month, int day, int hour, int minute, double second);
public static TimeSpan? age(DateTime? a, DateTime? b);
public static DateTime? date_bin(string? stride, DateTime? source, DateTime? origin);
// настройки
public static string? current_setting(string? name);
public static string? current_setting(string? name, bool missingOk);
public static string? set_config(string? name, string? value, bool isLocal);
// последовательности
public static long? nextval(string? sequence);
public static long? setval(string? sequence, long value);
public static long? currval(string? sequence);
public static long? lastval();
// SQL/JSON
public static string? json_array(params object?[] values);
public static string? jsonb_array(params object?[] values);
public static string? json_value(object? json, string? path);
public static string? json_query(object? json, string? path);
public static bool? json_exists(object? json, string? path);
```

- Все — на `PostgresFunctions`; XML-doc обязателен; `set_config` рендерит `set_config(name,value,is_local)`.
- `SqlFunctions.SqlServer`/`.ClickHouse` не конфликтуют: PG `json_value` живёт на `PostgresFunctions`,
  ClickHouse `json_value` — на `ClickHouseFunctions`, SQL Server — на `SqlServerFunctions`.

## 6. Диалект-план

- Новых `Supports*` нет: поверхность `PostgresFunctions` = PG-only, гейт существующий
  `SupportsExtendedScalarFunctions`. Если какое-то имя позже промотируется — завести per-name
  capability-объект (по образцу `ISessionInfoFunctions`), а не расширять umbrella-флаг.
- Транслятор: `src/nextorm.core/Visitors/ExtendedScalarFunctionTranslator.cs` (или ветка
  `BuiltinFunctionTranslator`) + `src/nextorm.core/Visitors/JsonSqlTranslator.cs` для SQL/JSON.
- Рендер `sha*` — `sha512(convert_to(x,'UTF8'))` для строкового входа, `sha512(x)` для `byte[]`.

## 7. Этапы внедрения

1. Хэши + `regexp_substr` + `make_*` (простые сигнатуры); SQL-gen тесты.
2. `age`/`date_bin` + `current_setting`/`set_config` + последовательности; SQL-gen тесты.
3. SQL/JSON `json_array`/`jsonb_array` + `json_value`/`json_query`/`json_exists`; интеграция.
4. Документация EN+RU; обновить `sql-function-coverage-gap.md` (отметить закрытое).

## 8. План тестов

- SQL-gen: `tests/nextorm.postgres.tests/SqlGenerationTests.cs` — точный SQL каждого имени.
- Гейт: `tests/nextorm.<other>.tests` — `PostgresFunctions.sha512`/`nextval`/`json_value` →
  `NotSupportedException`.
- Интеграция: `tests/nextorm.integration.tests/PostgresSpecificTests.cs` + `PostgresFunctionsTests.cs`
  — реальные хэши, `nextval` на sequence, `date_bin`/`age`, SQL/JSON на таблице.
- In-memory: не поддерживается (нет SQL) — явный `NotSupportedException`; теста на вычисление нет.
- Coverage: фича трогает `nextorm.core`/`nextorm.postgres` (входят в `coverage.settings.xml`) —
  отчитаться до/после.

## 9. Решения по открытым вопросам

1. `json_value`/`json_query`: новые имена `json_value`/`json_query` (путь рендерится
   `cast(path as jsonpath)`); конфликт с оператором `?` разведён только у `json_exists` — SQL/JSON
   перегрузка `json_exists(json, path, fromJsonPath)` имеет третий параметр-дискриминатор (в SQL не
   рендерится), существующий `json_exists(object, string)` остаётся оператором `?`. `jsonb_array`
   рендерится `json_array(... returning jsonb)`, т.к. функции `jsonb_array` в PostgreSQL нет.
2. `sha*`: оставлены `byte[] -> byte[]` (как `sha256`), без строковой перегрузки.
3. Промоция в `CommonFunctions` — вторая волна, отдельный workstream (per-name capability-объект);
   здесь PG-only под существующим `SupportsExtendedScalarFunctions`/`SupportsCryptoFunctions`/
   `SupportsJson`.
4. `age` возвращает `TimeSpan?`; месячная часть интервала теряется (PostgreSQL трактует месяц как
   30 дней при чтении), ограничение задокументировано в XML-doc.
5. `current_setting(name, missing_ok)` — поддержан (PG 9.6+).
6. `make_date` не дублируется: используется кросс-провайдерный `date_from_parts`.

## 10. Файлы к изменению

- Правки: `src/nextorm.core/Query/SqlFunctions.Postgres.cs`,
  `src/nextorm.core/Visitors/ExtendedScalarFunctionTranslator.cs`,
  `src/nextorm.core/Visitors/JsonSqlTranslator.cs`, `src/nextorm.postgres/PostgresDialect.cs`
  (при необходимости).
- Тесты: `tests/nextorm.postgres.tests/SqlGenerationTests.cs`, `tests/nextorm.integration.tests/
  PostgresSpecificTests.cs`/`PostgresFunctionsTests.cs`, негативные — `tests/nextorm.<other>.tests`.
- Доки: `docs/guide/11-scalar-functions.md` (+RU), `docs/guide/provider-specific/postgresql.md` (+RU),
  `docs/providers/postgres.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`, `docs/specs/roadmap/sql-function-coverage-gap.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **нужен пересмотр — 1 блокер** (дублирующая сигнатура `json_exists`) + 3 DRY/TYPE-риска.

- **[LSP]/[DRY] 🔴** `:117` `public static bool? json_exists(object? json, string? path)` совпадает по сигнатуре с существующим `PostgresFunctions.json_exists(object? json, string? key)` (`SqlFunctions.Postgres.cs:165`) → **CS0111**; вдобавок `JsonSqlTranslator.cs:94` матчит `json_exists` по имени+арности и не различит их. Fix: отдельное имя `json_path_exists` (совпадает с SQL Server-планом `:94`), без перегрузок.
- **[DRY] 🟡** `:14,91-93` `sha224/384/512(byte[])` дублируют `PostgresFunctions.digest(byte[], string)`, чей XML-doc уже перечисляет `sha224…sha512` (`SqlFunctions.Postgres.cs:336-349`). Fix: не добавлять; документировать `digest(data, "sha512")`.
- **[DRY] 🟡** `:16,98-100` `make_date/make_time/make_timestamp` дублируют `CommonFunctions.date_from_parts` (`SqlFunctions.cs:655`, рендер `BuiltinFunctionTranslator.cs:361-380`); кросс-провайдерный план откладывает `make_date` во вторую волну (`todo_cross_provider_scalar_functions.md:37`). Fix: расширять кросс-провайдерный `date_from_parts`.
- **[TYPE] 🟡** `:101` `TimeSpan? age(DateTime?, DateTime?)`: PG `age` возвращает interval с месяцами, `TimeSpan` их не выражает (признано планом, open Q4 `:159`). Fix: не отдавать `TimeSpan?` без гейта/документированного компонентного представления — иначе leaky contract.
- **[TYPE] 🟡** `:91-117` блок API написан `public static`; у провайдеров члены — instance (`SqlFunctions.Postgres.cs:12,356`). Fix: instance-члены на `PostgresFunctions`.
- **[TYPE] ℹ️** `:108-111` `nextval/setval/currval/lastval(string? sequence)`: не описано, рендерится ли имя как идентификатор или строковый литерал (`regclass`). Fix: зафиксировать биндинг (`'name'::regclass` либо verbatim-идентификатор).
- **[DIP] ℹ️** `:126-128` все новые имена под umbrella-гейтом `SupportsExtendedScalarFunctions` (`ExtendedScalarFunctionTranslator.cs:250`), часть имеет кросс-провайдерные аналоги. Deferred: промоушен в per-name `IScalarFunctions` при 2-м реальном потребителе (инвариант 1).
