# TODO: MySQL `JSON_STORAGE_FREE` — ограниченный срез каталога #283

> Tracking issue: [#283](https://github.com/AlexeyShirshov/nextorm/issues/283).
> Milestone: `1.0.9-rc4` (#22). Collection: `rc4`, task T283.
> Статус: **срез реализован и проверен** (T283, rv1); остальной каталог #283 остаётся открытым.

## Цель среза

Один явный маркер `MySqlFunctions.json_storage_free(string?) -> long?`:

- `MySQL 8.4` поддерживает имя и рендерит `json_storage_free(<выражение>)`;
- SQL NULL → SQL NULL → CLR `null` (не `0`);
- MariaDB, PostgreSQL, SQL Server, SQLite, ClickHouse и in-memory отклоняют маркер
  `NotSupportedException` без генерации SQL;
- живая проверка сравнивает результат с независимым raw-ADO.NET оракулом на MySQL 8.4.

Срез **не** закрывает #283 и не доказывает отсутствие остальных имён каталога.

## Решение о размещении (портируемость)

`JSON_STORAGE_FREE` остаётся на `MySqlFunctions`, а не на `CommonFunctions`: это учёт свободного
места в **бинарном** представлении `JSON`-колонки MySQL после частичного обновления
(`JSON_SET`/`JSON_REPLACE`/`JSON_REMOVE`), а не длина текста JSON и не размер значения.

Проверенные источники (D1):

- MySQL 8.4, JSON Utility Functions: `https://dev.mysql.com/doc/refman/8.4/en/json-utility-functions.html`
  — для колонки `JSON` возвращает число освобождённых байт; для литерала/пользовательской переменной
  и при отсутствии частичного обновления — `0`; при `NULL` — `NULL`; для невалидного JSON — ошибка.
- MySQL 8.4, Partial Updates of JSON Values: `https://dev.mysql.com/doc/refman/8.4/en/json.html`
  — частичное обновление возможно только для `JSON`-колонки, только через `JSON_SET`/`JSON_REPLACE`/
  `JSON_REMOVE`, только при замене существующего значения не большим.
- MariaDB 11.3 vs MySQL 8.0, Function Differences: `JSON_STORAGE_FREE` указан в разделе
  «Present in MySQL Only»
  (`https://mariadb.com/docs/release-notes/community-server/about/compatibility-and-differences/function-differences-between-mariadb-and-mysql/function-differences-between-mariadb-and-mysql-unmaintained-series/function-differences-between-mariadb-11-3-and-mysql-8-0`);
  индекс JSON-функций MariaDB имени не содержит
  (`https://mariadb.com/docs/server/reference/sql-functions/special-functions/json-functions`).
- PostgreSQL: списки JSON/JSONB-функций без учёта освобождённого места
  (`https://www.postgresql.org/docs/current/functions-json.html`); `pg_column_size` даёт размер
  хранимого значения, не свободные байты, и jsonb не имеет частичного in-place обновления.
- SQL Server: JSON хранится как `nvarchar`, функции JSON не считают освобождённые байты
  (`https://learn.microsoft.com/en-us/sql/t-sql/functions/json-functions-transact-sql`); `DATALENGTH`
  — длина текста, не эквивалент.
- SQLite: JSON1 даёт текст/`jsonb`-представление, учёта свободного места нет
  (`https://sqlite.org/json1.html`).
- ClickHouse: JSON-функции не содержат счётчика освобождённых байт
  (`https://clickhouse.com/docs/en/sql-reference/functions/json-functions`).

Итог: семантически эквивалентной операции у перечисленных провайдеров нет; перенос на
`CommonFunctions` не обоснован.

## Контракт аргумента и результата

- Ровно один скалярный аргумент `string?`; без перегрузок, `params` и обобщений.
- SQL NULL ≠ JSON-текст `"null"`.
- Клиентского разбора/валидации/подстановки NULL нет: поведение невалидного JSON определяет сервер.
- Возврат — nullable `Int64` без потерь; неудачная материализация — дефект, а не повод менять оракул.

## Файлы

Продукт:

- `src/nextorm.core/Query/SqlFunctions.MySql.cs`
- `src/nextorm.mysql/MySqlDialect.cs`
- `src/nextorm.mariadb/MariaDbDialect.cs`

Тесты:

- `tests/nextorm.mysql.tests/Functions/MySqlFunctionsSqlGenerationTests.cs`
- `tests/nextorm.mariadb.tests/Functions/MySqlFunctionsSqlGenerationTests.cs`
- `tests/nextorm.postgres.tests/Functions/MySqlFunctionsRejectionTests.cs`
- `tests/nextorm.sqlserver.tests/Functions/MySqlFunctionsRejectionTests.cs`
- `tests/nextorm.sqlite.tests/Functions/MySqlFunctionsRejectionTests.cs`
- `tests/nextorm.clickhouse.tests/Functions/MySqlFunctionsRejectionTests.cs`
- `tests/nextorm.core.tests/InMemory/InMemoryMySqlFunctionsTests.cs`
- `tests/nextorm.integration.tests/MySql/MySqlFunctionExecutionTests.cs`

Документация и реестры:

- `docs/guide/provider-specific-sql/mysql.md` (+RU)
- `docs/scalar-functions/08-reference.md` (+RU)
- `docs/specs/roadmap/sql-function-coverage-gap.md`
- `docs/specs/roadmap/sql-capabilities-gap-analysis.md`
- `docs/specs/design/API-NAMING-REVIEW.md`

## Живая матрица (MySQL 8.4, raw-оракул)

| Вход | Оракул | Проверяемый результат |
|---|---|---|
| литерал-объект `{"a":1}` | `select json_storage_free('{"a":1}')` → `0` | то же значение, `long?` |
| скаляр `123` | raw → `0` | то же |
| массив `[1,2]` | raw → `0` | то же |
| JSON-текст `null` | raw → `0` | то же |
| SQL NULL | raw → `NULL` | `null`, не `0` |
| captured-строка | raw → `0` | то же |
| bound-параметр | raw → `0` | то же |
| `JSON`-колонка (non-null строка + NULL строка) | raw по строкам | значения совпадают; NULL-строка → `null` |
| после частичного `JSON_SET`, уменьшающего значение | raw до/после | равенство; положительное число **не** предполагается |
| невалидный JSON `{bad` | raw → ошибка MySQL | та же идентичность ошибки на транслированном пути |

## Остаточное владение и согласование с T284

T284 реализует ровно `JSON_EQUALS` и `JSON_NORMALIZE`. T283 сохраняет **остаточное владение**
следующими именами, не реализуя их в этом срезе:

- `JSON_OVERLAPS`
- `JSON_SCHEMA_VALID`
- `FORMAT_PICO_TIME`
- дополняющее имя `FORMAT_BYTES`

Атрибуция этих имён к #284 в §3 `sql-function-coverage-gap.md` (строка MariaDB) исправлена
перекрёстной ссылкой: имена остаются нереализованными/непроверенными и не переносятся в §1.
Запись об исправлении — в `sql-function-coverage-gap.md` (примечание к §3) и в
`sql-capabilities-gap-analysis.md` (запись прогресса #283).
Это запись о владении, а не утверждение о доступности имени на MySQL или MariaDB.

T280 (`months_between`, `to_date`, `to_number`, `uuid_v4`) в срез не входит и не меняется.

## Результат rv1

- Продукт: маркер + поддержка/рендер MySQL + явное исключение MariaDB; транслятор и интерфейсы не менялись.
- Юнит-тесты: точные фрагменты, nullable/bound формы, паритет объявленных имён, отказ у шести провайдеров.
- Живой MySQL 8.4: матрица выше сверена с raw-оракулом; идентичность ошибки для невалидного JSON совпала.
- Документация EN+RU и перемещение `JSON_STORAGE_FREE` из §3 в §1 (только это имя).
- Файл сохраняется как завершённый рабочий план и передача остаточного объёма; не удаляется.
