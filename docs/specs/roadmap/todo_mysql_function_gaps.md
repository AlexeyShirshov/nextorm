# TODO: Пробелы функций MySQL (`FIND_IN_SET`, `FIELD`, `ELT`, `SUBSTRING_INDEX`, `STR_TO_DATE`, `DATE_FORMAT`, `FROM_UNIXTIME`, JSON-mutation, `UUID_TO_BIN`, …)
> Tracking issue: [#80](https://github.com/AlexeyShirshov/nextorm/issues/80).

> **Статус: SHIPPED.** Поверхность `SqlFunctions.MySql` (`MySqlFunctions`) заведена, все перечисленные
> имена рендерятся нативно и гейтятся по имени через `ISqlDialect.MySqlFunctions`; MariaDB наследует
> набор (кроме `UUID_TO_BIN`/`BIN_TO_UUID`). Публичная документация:
> [`guide/provider-specific/mysql.md`](../../guide/provider-specific/mysql.md) (EN+RU). Раздел ниже —
> сохранённый рабочий план (RFC); итоговые решения — в §«Итог и решения».

> Рабочий план (design RFC). Источник: таблица «Summary: highest-value gaps»,
> строка **MySQL**, в [`sql-function-coverage-gap.md`](sql-function-coverage-gap.md)
> (§«MySQL»); новый пункт §4 в
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md).
> Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

## 1. Пункт и цель

- **Фича:** завести провайдерную поверхность `SqlFunctions.MySql` (`MySqlFunctions`), которой у
  MySQL/MariaDB сегодня **нет**, и вынести на неё функции из gap-анализа:
  `FIND_IN_SET`, `FIELD`, `ELT`, `SUBSTRING_INDEX`, `FORMAT`, `STR_TO_DATE`, `DATE_FORMAT`,
  `FROM_UNIXTIME`, `UNIX_TIMESTAMP`, `MD5`/`SHA1`/`SHA2`, `INET_ATON`/`INET_NTOA`,
  `JSON_INSERT`/`JSON_REPLACE`/`JSON_SET`/`JSON_REMOVE`/`JSON_MERGE_PATCH`/`JSON_MERGE_PRESERVE`/
  `JSON_ARRAY_APPEND`/`JSON_ARRAY_INSERT`/`JSON_DEPTH`/`JSON_KEYS`/`JSON_LENGTH`/`JSON_TYPE` и
  `UUID_TO_BIN`/`BIN_TO_UUID`.
- **Критерий приёмки:** каждое имя рендерится нативной формой MySQL (и наследуется MariaDB там,
  где она совместима); прочие провайдеры отклоняют; SQL-gen тесты + MySQL/MariaDB-интеграция.
- **Уже есть, не трогаем:** `LAST_DAY` рендерится через портируемый `end_of_month`, `group_concat`
  через `string_agg`, текстовый JSON (`json_value`/`json_query`/`json_modify`/`isjson`) — через
  `SqlServerFunctions` под общим `SupportsTextJson`.
- **Не входит:** общая кросс-провайдерная hash/JSON-mutation/date-conversion поверхность — это
  отдельные follow-up (см. [`todo_cross_provider_scalar_functions.md`](todo_cross_provider_scalar_functions.md));
  здесь — MySQL-only спеллинги/сигнатуры.

## 2. Текущее состояние (проверено по коду)

| Слой | Где | Сейчас |
|---|---|---|
| Публичная поверхность | `src/nextorm.core/Query/SqlFunctions.*.cs` | `MySqlFunctions` **отсутствует**; `SqlFunctions.MySql` нет |
| Диалект | `src/nextorm.mysql/MySqlDialect.cs` | `MySqlSessionInfoFunctions` только (session/info); функций-хуков нет |
| Покрытие | CommonFunctions + CLR | `group_concat`←`string_agg`, `last_day`←`end_of_month`, `date_add`/`date_diff`, `iif`/`greatest`/`least`/`nullif`/`coalesce` |
| MariaDB | `src/nextorm.mariadb/MariaDbDialect.cs` | наследует MySQL-поведение (см. `todo_mariadb_function_gaps.md`) |

## 3. Матрица провайдеров (провайдер × форма)

Источники: MySQL 8.4 built-in function reference (string, mathematical, date-and-time, json,
encryption, miscellaneous); MariaDB built-in functions; PostgreSQL 18; Microsoft Learn T-SQL;
SQLite `lang_corefunc`/`json1`/`lang_datefunc`; ClickHouse function reference.

| Функция | MySQL | MariaDB | PostgreSQL | SQL Server | SQLite | ClickHouse |
|---|---|---|---|---|---|---|
| `FIND_IN_SET(x, list)` | `FIND_IN_SET` | есть | `= ANY(string_to_array(list,','))` (иначе) | — | `instr(','\|\|list\|\|',', ','\|\|x\|\|',')` (хак) | `has(splitByString(',', list), x)` |
| `FIELD(x, …)` | `FIELD` | есть | — | — | — | `indexOf([...], x)` |
| `ELT(n, …)` | `ELT` | есть | `(ARRAY[...])[n]` | `CHOOSE` | `CASE` | `[...][n]` |
| `SUBSTRING_INDEX(s, d, n)` | есть | есть | `split_part` (только n>0) | — | — | `splitByString`+индекс |
| `FORMAT(x, d)` | `FORMAT` (числа) | `FORMAT` | `to_char` | `FORMAT` | `printf` | `format` |
| `STR_TO_DATE(s, fmt)` | есть | есть | `to_date`/`to_timestamp` (иной шаблон) | `TRY_CONVERT`/`PARSE` | — | `parseDateTime` |
| `DATE_FORMAT(d, fmt)` | есть | есть | `to_char` | `FORMAT` | `strftime`/`date` | `formatDateTime` |
| `LAST_DAY(d)` | есть | есть | — | `EOMONTH` | — | — | **покрыто `end_of_month`** |
| `FROM_UNIXTIME(ts)` | есть | есть | `to_timestamp(double)` | `DATEADD(S, ts, '1970-01-01')` | `datetime(ts,'unixepoch')` | `fromUnixTimestamp` |
| `UNIX_TIMESTAMP(d)` | есть | есть | `extract(epoch from d)` | `DATEDIFF_BIG(S, '1970-01-01', d)` | `unixepoch(d)` | `toUnixTimestamp` |
| `MD5`/`SHA1`/`SHA2` | есть | есть | `md5`/`sha256`/`digest` | `HASHBYTES` | — | `MD5`/`SHA1`/`SHA256` |
| `INET_ATON`/`INET_NTOA` | есть | есть | `inet`/`host()` | — | — | `IPv4StringToNum`/`IPv4NumToString` |
| JSON mutation (`JSON_SET`/`JSON_INSERT`/…) | есть | есть | `jsonb_set`/`jsonb_insert`/`\|\|` | `JSON_MODIFY` | `json_set`/`json_patch`/`json_remove` | — / `JSONMergePatch` |
| `UUID_TO_BIN`/`BIN_TO_UUID` | 8.0 | — (нет; `CAST(... AS BINARY(16))`/`CAST(... AS UUID)`) | — | — | — | — |

**Единообразие провайдеров:** все перечисленные — MySQL/MariaDB-специфичны по спеллингу/сигнатуре
(`FORMAT` — числовое форматирование с группировкой, `DATE_FORMAT`/`STR_TO_DATE` — `%`-шаблоны,
`FIND_IN_SET`/`FIELD`/`ELT`/`SUBSTRING_INDEX` — MySQL-идиомы). Поэтому — поверхность `MySqlFunctions`
(MySQL+MariaDB), прочие гейтятся per name. Пары с совпадающей семантикой (`MD5`/`SHA2`, JSON-mutation,
`FROM_UNIXTIME`/`UNIX_TIMESTAMP`) — кандидаты на промотирование в `CommonFunctions` отдельной волной.

## 4. Ближайший CLR-аналог и тир

- **tier (b)**: `MySqlFunctions` + транслятор + per-name capability. `[SqlFunction]` (tier c) годится
  только для чистых name-swap без параметров/различий (`FIND_IN_SET`, `FIELD`, `ELT`), но всё равно
  требует гейта на диалекте — предпочтителен единый capability-объект.
- `LAST_DAY` — **не добавлять**: уже покрыт `end_of_month` (иначе дубль поверхности, запрещено
  границами skill).

## 5. Дизайн и публичный API

```csharp
public static MySqlFunctions MySql => default!;   // новый маркер

public static class MySqlFunctions
{
    public static int? find_in_set(string? value, string? set);
    public static int? field<T>(T? value, params T?[] values);
    public static T? elt<T>(int index, params T?[] values);
    public static string? substring_index(string? value, string? delimiter, int count);
    public static string? format(decimal? value, int decimals);
    public static DateTime? str_to_date(string? value, string? format);
    public static string? date_format(DateTime? value, string? format);
    public static DateTime? from_unixtime(long? unixTimestamp);
    public static long? unix_timestamp(DateTime? value);
    public static string? md5(string? value);
    public static string? sha1(string? value);
    public static string? sha2(string? value, int hashLength);
    public static long? inet_aton(string? value);
    public static string? inet_ntoa(long? value);
    public static string? json_set(string? json, string? path, string? value);
    public static string? json_insert(string? json, string? path, string? value);
    public static string? json_replace(string? json, string? path, string? value);
    public static string? json_remove(string? json, string? path);
    public static string? json_merge_patch(string? a, string? b);
    public static string? json_merge_preserve(string? a, string? b);
    // ... JSON_DEPTH/KEYS/LENGTH/TYPE, JSON_ARRAY_APPEND/INSERT
    public static string? uuid_to_bin(string? uuid);
    public static string? bin_to_uuid(string? binary);
}
```

- `json_set`/`json_insert`/`json_replace`: **не** конфликтуют с `JsonSqlTranslator`, если живут на
  `MySqlFunctions`; но при промотировании в `CommonFunctions` учесть существующие имена.
- `format` — числовое форматирование (группировка + d знаков), не путать с SQL Server `FORMAT`
  (там .NET-строка) и `SqlFunctions.Postgres.format` (PG-шаблон).

## 6. Диалект-план

- `ISqlDialect`: свойство `IMySqlFunctions? MySqlFunctions { get; }` (default `null`), по образцу
  `ISessionInfoFunctions`; `Supports(string name)`/`Render(name, args)` per function.
- `MySqlDialect`/`MariaDbDialect` реализуют (MariaDB наследует MySQL и переопределяет лишь отличия,
  напр. при отсутствии какого-то `JSON_*`).
- Транслятор: новый `MySqlFunctionTranslator.cs` поверх `BuiltinFunctionTranslator`.
- `SqlFunctions.MySql` маркер добавляется в `SqlFunctions.cs` рядом с `Postgres`/`SqlServer`/`ClickHouse`.

## 7. Этапы внедрения

1. Каркас `MySqlFunctions` + `SqlFunctions.MySql` + capability-объект + пустой MySQL-рендер; SQL-gen.
2. Строковые/условные (`find_in_set`/`field`/`elt`/`substring_index`/`format`/`str_to_date`/
   `date_format`); SQL-gen.
3. Дата/числа (`from_unixtime`/`unix_timestamp`/`md5`/`sha1`/`sha2`/`inet_*`); SQL-gen.
4. JSON-mutation + `UUID_TO_BIN`/`BIN_TO_UUID`; интеграция.
5. Документация EN+RU; обновить `sql-function-coverage-gap.md`.

## 8. План тестов

- SQL-gen: `tests/nextorm.mysql.tests/SqlGenerationTests.cs` + `MySqlDialectTests.cs`; то же для
  `tests/nextorm.mariadb.tests`.
- Гейт: `tests/nextorm.<other>.tests` — `MySqlFunctions.find_in_set`/`uuid_to_bin` → `NotSupportedException`.
- Интеграция: `tests/nextorm.integration.tests/MySqlSpecificTests.cs` (и MariaDB, если есть
  provider-suite) — JSON-mutation round-trip, `FIND_IN_SET`-фильтр, `FROM_UNIXTIME`.
- Coverage: `nextorm.mysql`/`nextorm.mariadb` **не входят** в `coverage.settings.xml` — числа не
  сдвинут; указать явно и всё равно добавить SQL-gen.

## 9. Открытые вопросы

1. Один общий `MySqlFunctions` для MySQL и MariaDB или MariaDB-специфика отдельно (`todo_mariadb_function_gaps.md`
   предполагает надстройку)?
2. `format` (числовой) — отдельный член или общая кросс-провайдерная `format`-поверхность с
   provider-рендером (учитывая несовместимые языки шаблонов, gap §5.15)?
3. Какие имена промотировать в `CommonFunctions` сразу (`md5`/`sha2`, JSON-mutation, `from_unixtime`)?
4. `UUID_TO_BIN` возвращает `binary(16)` — как типизировать (`byte[]`), и нужен ли `swap_flag`.
5. Нужны ли перегрузки JSON-mutation с несколькими path/value-парами (MySQL принимает variadic).

## 10. Файлы к изменению

- Новое: `src/nextorm.core/Query/SqlFunctions.MySql.cs`, `src/nextorm.core/Visitors/MySqlFunctionTranslator.cs`.
- Правки: `src/nextorm.core/Query/SqlFunctions.cs` (маркер `MySql`),
  `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `SqlDialectBase.cs`,
  `DialectCapabilities.cs`, `src/nextorm.mysql/MySqlDialect.cs`, `src/nextorm.mariadb/MariaDbDialect.cs`.
- Тесты: `tests/nextorm.mysql.tests/*`, `tests/nextorm.mariadb.tests/*`,
  `tests/nextorm.integration.tests/MySqlSpecificTests.cs`, негативные — `tests/nextorm.<other>.tests`.
- Доки: `docs/guide/provider-specific/mysql.md` (+RU), `docs/guide/11-scalar-functions.md` (+RU),
  `docs/providers/mysql.md`/`mariadb.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`, `docs/specs/roadmap/sql-function-coverage-gap.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **нужен пересмотр §5/§6 — 2 блокера** (не компилирующийся тип + перехват диспетча) **+ блокер контракта** (`IMySqlFunctions`).

- **[TYPE] 🔴** `:79` `public static class MySqlFunctions` (+ `:77` `public static MySqlFunctions MySql => default!`): static-класс нельзя использовать как тип (CS0718) и он не может наследовать `CommonFunctions`, поэтому `SqlFunctions.MySql.count(...)`/`.iif(...)` не резолвятся (в отличие от `PostgresFunctions : CommonFunctions` `SqlFunctions.Postgres.cs:12`). Fix: `public class MySqlFunctions : CommonFunctions`.
- **[OCP]/[LSP] 🔴** Name-based диспетчер перехватит `format` и `md5`: `ExtendedScalarFunctionTranslator` матчит по имени (`:65`), `format`/`md5` уже в наборах (`:50`,`:34`) и вызывается раньше провайдерных (`NormSqlTranslator.cs:452`); на MySQL `SupportsExtendedScalarFunctions == false` → `RequireExtended` (`:248`) бросит до рендера `MySqlFunctions.format` (`:82`)/`.md5` (`:90`). Fix: `MySqlFunctionTranslator` вызывать раньше + гейт по `DeclaringType == typeof(MySqlFunctions)` (прецедент `JsonSqlTranslator.cs:30-33`).
- **[TYPE] 🟡** `:102-103` `uuid_to_bin`/`bin_to_uuid` типизированы `string?`, а MySQL `UUID_TO_BIN` даёт `binary(16)`; прецедент бинарного возврата — `byte[]?` (`SqlFunctions.Postgres.cs:341`). Fix: решить §9.4 (`:147`) до объявления типа (предпочтительно `byte[]?`).
- **[DRY] 🟡** `:90-92` `md5`/`sha1`/`sha2` — третья параллельная hash-поверхность (PG `SqlFunctions.Postgres.cs:332`, CH `todo_clickhouse_function_gaps.md:123-125`). Три потребителя — порог Rule-of-Three пройден (инвариант 1). Fix: промоутировать hash в `CommonFunctions` этой волной или зафиксировать явный триггер.
- **[DRY]/[KISS] 🟡** `:114-115` `IMySqlFunctions` (`Supports`/`Render`) дублирует форму `IScalarFunctions` (`DialectCapabilities.cs:340-351`); альтернатива — хук `MakeDictionaryFunction` (`ISqlDialect.cs:571`). Fix: обосновать новый интерфейс или переиспользовать существующий (инвариант 2).
- **[TYPE]/[DRY] ℹ️** `:82-83` `params T?[]` для `field`/`elt` должен требовать inline-массив (как `greatest`/`multi_func`; `BuiltinFunctionTranslator.FlattenParams:498`), в плане лимитация не описана. `:95` `json_set` семантически покрыт существующим `json_modify→json_set` (`MySqlDialect.cs:200`); действительно новые — `json_insert`/`json_replace`. Fix: зафиксировать в XML-doc; отметить в плане.
## 11. Итог и решения (shipped)

- **Публичная поверхность:** `public class MySqlFunctions : CommonFunctions` (нестатический —
  наследуемый) в `src/nextorm.core/Query/SqlFunctions.MySql.cs`; маркер `SqlFunctions.MySql`.
  28 членов: `find_in_set`, `field`, `elt`, `substring_index`, `format`, `str_to_date`, `date_format`,
  `from_unixtime`, `unix_timestamp`, `md5`, `sha1`, `sha2`, `inet_aton`, `inet_ntoa`, `json_set`,
  `json_insert`, `json_replace`, `json_remove`, `json_merge_patch`, `json_merge_preserve`,
  `json_array_append`, `json_array_insert`, `json_depth`, `json_keys`, `json_length`, `json_type`,
  `uuid_to_bin`, `bin_to_uuid`.
- **Типизация:** `uuid_to_bin(string?) -> byte[]?`, `bin_to_uuid(byte[]?) -> string?` (native
  `binary(16)`); `field<T>`/`elt<T>` дженерики с `params`; `format(decimal?, int)` — числовое
  форматирование (не PG/SQL-Server `format`).
- **Диалект:** новый `IMySqlFunctions { bool Supports(string); string Render(string, IReadOnlyList<string>); }`
  (+ DIM `ISqlDialect.MySqlFunctions => null`, `SqlDialectBase` virtual). `MySqlNativeFunctions`
  (`internal`, не `sealed` — шов для #79) рендерит весь набор; `MariaDbDialect` переопределяет
  `MySqlFunctions` декоратором `MariaDbNativeFunctions`, который наследует набор и **отклоняет**
  `uuid_to_bin`/`bin_to_uuid` (в MariaDB их нет — проверено на `mariadb:11.4`; преобразование через
  `CAST(... AS BINARY(16))`/`CAST(... AS UUID)`).
- **Транслятор:** `MySqlFunctionTranslator` (распознаёт по `DeclaringType`, поэтому одноимённые
  `PostgresFunctions.md5`/`format` продолжают обрабатываться `ExtendedScalarFunctionTranslator`);
  встроен в диспетчер перед расширенными скалярами. In-memory: `InMemoryScalarFunctionRewriter`
  отклоняет `MySqlFunctions` явным `NotSupportedException` (вместо NRE).
- **Тесты:** SQL-gen каждый имя — `tests/nextorm.mysql.tests/MySqlFunctionsSqlGenerationTests.cs`,
  `tests/nextorm.mariadb.tests/MySqlFunctionsSqlGenerationTests.cs`; dialect-хуки — `MySqlDialectTests`/
  `MariaDbDialectTests`; негатив у прочих провайдеров — `tests/nextorm.<p>.tests/MySqlFunctionsRejectionTests.cs`
  (postgres/sqlite/sqlserver/clickhouse) + `tests/nextorm.core.tests/InMemoryMySqlFunctionsTests.cs`;
  container-интеграция — `MySqlSpecificTests` (MySQL) и `MariaDbFunctionsIntegrationTests` +
  `Providers/MariaDbContainer.cs` (MariaDB 11.4, новый провайдер-контейнер; MySQL Testcontainers-модуль
  не подходит из-за `mysqladmin` → используется `healthcheck.sh`).
- **Coverage:** `nextorm.mysql`/`nextorm.mariadb` не входят в `coverage.settings.xml` — числа не
  сдвинуты (SQL-gen добавлен независимо).
- **Открытые вопросы §9:** (1) одна общая поверхность с per-name гейтом — да; (2) `format` оставлен
  MySQL-специфичным; (3) промоушен `md5`/`sha2`/JSON-mutation/`from_unixtime` в `CommonFunctions` —
  отдельной волной (`todo_cross_provider_scalar_functions.md`); (4) `uuid_to_bin` типизирован `byte[]?`,
  `swap_flag` не поддержан; (5) вариативные JSON-пары не добавлены (одна пара на вызов).

