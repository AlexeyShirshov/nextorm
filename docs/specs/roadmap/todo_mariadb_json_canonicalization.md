# TODO: MariaDB JSON canonicalisation — ограниченный срез каталога #284

> Tracking issue: [#284](https://github.com/AlexeyShirshov/nextorm/issues/284).
> Milestone: `1.0.9-rc4` (#22). Collection: `rc4`, task T284.
> Статус: **срез реализован и проверен** (T284, rv1); остальной каталог #284 остаётся открытым.

## Цель среза

Ровно два явных маркера на общей поверхности `SqlFunctions.MySql`:

- `MySqlFunctions.json_equals(string?, string?) -> int?` — сравнение двух JSON-документов;
- `MySqlFunctions.json_normalize(string?) -> string?` — канонический текст JSON-документа.

Оба имени поддерживает только MariaDB (`mariadb:11.4`), и рендерятся они как
`json_equals(lhs, rhs)` / `json_normalize(j)`. MySQL, PostgreSQL, SQL Server, SQLite, ClickHouse и
in-memory-провайдер отклоняют оба имени `NotSupportedException` без генерации SQL.

Срез **не** закрывает #284: остальные имена каталога перечислены в разделе «Остаточное владение».

## Решения о форме

- `int?`, а не `bool?`: на `SqlFunctions.MySql` нет возвращающего boolean прецедента
  (`find_in_set -> int?`), документированный результат сервера — `1/0/NULL`, а протокольный тип
  вычисленного `JSON_EQUALS` не доказан.
- Ровно два скалярных аргумента `string?` у `json_equals` и один у `json_normalize`; без перегрузок,
  `params`, обобщений и флагов.
- Клиентского разбора/валидации/подстановки NULL нет: невалидный JSON и NULL-семантику определяет
  сервер. SQL NULL отличается от JSON-текста `"null"`.
- `MySqlDialect.cs` не меняется: имена добавляются только в `MariaDbNativeFunctions`
  (`IsMariaDbOnly` + `Render`), поэтому MySQL их не репортит.
- Неизвестное поведение `json_normalize(NULL)` закрывается **независимым raw-ADO.NET** наблюдением
  на 11.4, записанным **до** утверждения в тесте; ожидание в тесте — константа, а не динамический
  оракул.

## Проверенные источники (D1)

- MariaDB KB, `JSON_EQUALS`: `https://mariadb.com/kb/en/json_equals/` — `1` для равных JSON-значений,
  `0` для разных, `NULL` если любой аргумент SQL NULL.
- MariaDB KB, `JSON_NORMALIZE`: `https://mariadb.com/kb/en/json_normalize/` — каноническая форма:
  ключи объектов отсортированы, лишние пробелы удалены; результат идемпотентен.
- MariaDB KB, JSON-функции: `https://mariadb.com/kb/en/json-functions/` — оба имени в индексе
  MariaDB; в MySQL 8.4 их нет (см. function-differences MariaDB/MySQL и существующий транслятор).

## Контракт аргумента и результата

- `json_equals(NULL, x)`, `json_equals(x, NULL)`, `json_equals(NULL, NULL)` → SQL NULL → CLR `null`.
- `json_equals` равных документов (в том числе с разным порядком ключей) → `1`; разных → `0`.
- `json_normalize` JSON `true` → текст `true`; JSON-объект → отсортированные ключи без пробелов;
  повторное применение не меняет результат.
- `json_normalize(NULL)` → SQL NULL → CLR `null` (подтверждено raw-оракулом `raw-oracle.log`).
- JSON-текст `"null"` — обычное значение, а не SQL NULL.
- Возврат — nullable `int?`/`string?` без потерь; неудачная материализация — дефект, а не повод
  менять оракул.

## Файлы

Продукт:

- `src/nextorm.core/Query/SqlFunctions.MySql.cs`
- `src/nextorm.mariadb/MariaDbDialect.cs`

Тесты:

- `tests/nextorm.mariadb.tests/Functions/MySqlFunctionsSqlGenerationTests.cs`
- `tests/nextorm.mariadb.tests/Dialect/MariaDbDialectTests.cs`
- `tests/nextorm.mysql.tests/Functions/MySqlFunctionsSqlGenerationTests.cs`
- `tests/nextorm.core.tests/InMemory/InMemoryMySqlFunctionsTests.cs`
- `tests/nextorm.postgres.tests/Functions/MySqlFunctionsRejectionTests.cs`
- `tests/nextorm.sqlserver.tests/Functions/MySqlFunctionsRejectionTests.cs`
- `tests/nextorm.sqlite.tests/Functions/MySqlFunctionsRejectionTests.cs`
- `tests/nextorm.clickhouse.tests/Functions/MySqlFunctionsRejectionTests.cs`
- `tests/nextorm.integration.tests/MariaDb/MariaDbFunctionsIntegrationTests.cs`

Документация и реестры:

- `docs/guide/provider-specific-sql/mysql.md` (+RU)
- `docs/scalar-functions/08-reference.md` (+RU)
- `docs/specs/roadmap/sql-function-coverage-gap.md`
- `docs/specs/design/API-NAMING-REVIEW.md`

## Живая матрица (MariaDB 11.4, raw-оракул)

| Вход | Оракул | Проверяемый результат |
|---|---|---|
| `json_equals('{"a":1}','{"a":1}')` | `1` (`INT`) | `1`, CLR `int?` |
| `json_equals('{"a":1}','{"a":2}')` | `0` | `0`, CLR `int?` |
| `json_equals('{"a":1,"b":2}','{"b":2,"a":1}')` | `1` | `1` (порядок ключей не важен) |
| `json_equals(NULL, x)` / `(x, NULL)` / `(NULL, NULL)` | `NULL` | `null`, не `0` |
| `json_equals('null','null')` | `1` | `1` (JSON-текст `null` — значение) |
| `json_normalize('true')` | текст `true` (`VARCHAR`) | `"true"`, CLR `string?` |
| `json_normalize('{ "b" : true, "a" : false }')` | `{"a":false,"b":true}` | та же строка |
| повторный `json_normalize` | `{"a":false,"b":true}` | идемпотентность |
| `json_normalize(NULL)` | `NULL` | `null` (не текст `null`) |
| `json_normalize('null')` | текст `null` | `"null"` |
| `json_normalize('{"quoted":"a b"}')` | `{"quoted":"a b"}` | кавычки и пробел сохранены |
| bound-параметры | `1` / `{"a":false,"b":true}` | привязка, payload не интерполируется |

## Остаточное владение

Не входят в срез и остаются в #284 (следующий срез со своим контрактом):
`SFORMAT`, `NATURAL_SORT_KEY`, `LENGTHB`, `CHR`, `CRC32C`, `SYS_GUID`, `JSON_ARRAY_INTERSECT`,
`JSON_KEY_VALUE`, `JSON_OBJECT_FILTER_KEYS`, `JSON_OBJECT_TO_ARRAY`, `JSON_LOOSE`, `MEDIAN`.
Имена `JSON_OVERLAPS`, `JSON_SCHEMA_VALID`, `FORMAT_PICO_TIME`, `FORMAT_BYTES` — остаточное
расследование #283 (см. примечание к §3 в `sql-function-coverage-gap.md`). Отсутствующие на 11.4
`FORMAT_BYTES` built-in, `TRUNC(date[,fmt])`, `ANY_VALUE` не входят; `PERCENTILE_CONT`/`DISC` уже
покрыты.

## Границы T280 / T283

T280 (`months_between`/`to_date`/`to_number`/`uuid_v4`, пер-именные версионные гейты и
пессимистичная unversioned-политика) не меняется. T283 (`JSON_OVERLAPS`, `JSON_SCHEMA_VALID`,
`FORMAT_PICO_TIME`, `FORMAT_BYTES`) не меняется. `MySqlDialect.cs` и транслятор не меняются.

## Результат rv1

- Продукт: два маркера `MySqlFunctions` + capability/render только в `MariaDbNativeFunctions`.
- Юнит-тесты: точные SQL-формы, колонки/литералы/captured/placeholder, capability и render,
  отказ MySQL/PostgreSQL/SQL Server/SQLite/ClickHouse/in-memory.
- Живой MariaDB 11.4: матрица сверена с raw-оракулом, записанным до утверждения; `int?`/`string?`
  материализуются.
- Документация EN+RU и перемещение ровно двух имён из §3 в §1; реестр имён +2.
- Файл сохраняется как рабочий план и передача остаточного объёма; не удаляется.
