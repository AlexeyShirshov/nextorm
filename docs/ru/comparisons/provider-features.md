# Функции провайдеров: Nextorm против linq2db

Датированное сравнение по движкам: **поверхность провайдер-специфичного API** — насколько широко каждая
библиотека выставляет собственные возможности конкретного движка через выделенный API, подключённый к
конструктору запросов. Оценки — суждения по публичной документации и исходникам обеих библиотек на
**2026-10-04**.

Страница отвечает на более узкий вопрос, чем [«Возможности»](capabilities.md). «Возможности» — можно ли
выразить конструкт вообще; здесь — есть ли для него **выделенный провайдерный API**. `no` означает, что
выделенного API не найдено; конструкт при этом может быть выразим через сырой SQL (в linq2db — через
`Sql.Ext`/`Sql.Function`).

## Как читать оценки

- **yes** — выделенный провайдерный API есть.
- **partial** — есть с оговорённым ограничением либо достижим только общим механизмом.
- **no** — выделенного провайдерного API не найдено.
- **n/a** — такой возможности у движка нет.

Каждую таблицу закрывает строка **Итого**: количество yes / partial / no по каждой библиотеке (`n/a` не
учитывается).

## Провайдерное покрытие

| Движок | Nextorm | linq2db |
|---|---|---|
| SQLite | yes | yes |
| SQL Server | yes | yes |
| PostgreSQL | yes | yes |
| MySQL | yes (собственный провайдер) | yes |
| MariaDB | yes (собственный провайдер) | partial (общий MySQL-провайдер, `MySqlVersion.MariaDB10`; отдельной extension-поверхности нет) |
| ClickHouse | yes | yes |
| In-memory object context | yes | no |
| Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, DuckDB, Ydb | no | yes |
| **Итого** | 7 / 0 / 1 | 6 / 1 / 1 |

У linq2db нет in-memory object context. Nextorm разделяет MySQL и MariaDB на отдельные провайдеры; linq2db
моделирует MariaDB как версию MySQL.

## Ключевой вывод

**Nextorm покрывает провайдер-специфичные возможности шире.** На шести SQL-движках, которые поддерживают обе
библиотеки, Nextorm выставляет выделенный провайдерный API на большем числе поверхностей, чем linq2db:
библиотека скалярных функций для каждого движка, поверхность ClickHouse
(клаузы/агрегаты/табличные функции/tuple/нативный JSON), PostgreSQL
(full-text/regex/`jsonb_path_*`/range/data-modifying CTE/табличные функции), последовательности и блокировки
строк в SQL Server, JSON/regex/последовательности в MySQL/MariaDB, JSON1/regex/агрегаты/LOB/FTS5
maintenance в SQLite — всё подключено к конструктору запросов, а не к сырому SQL.

Это утверждение о **глубине по каждому движку, а не о числе движков**: linq2db подключается к большему числу
продуктов (Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, DuckDB, Ydb), тогда как Nextorm
добавляет in-memory object context и разделяет MySQL/MariaDB. linq2db остаётся шире по библиотеке скалярных
функций SQL Server и разнообразию хинтов (issue [#182](https://github.com/AlexeyShirshov/nextorm/issues/182)).

## PostgreSQL

| Возможность | Nextorm | linq2db |
|---|---|---|
| Массивы + квантификаторы `any`/`all` | yes | yes |
| Range / multirange (операторы, маппинг) | yes | partial (только маппинг типов) |
| Full-text `tsvector`/`tsquery` | yes | no |
| Regex (`~`, `~*`, `regexp_replace`) | yes | no |
| JSONB + `jsonb_path_*` | yes | partial (только маппинг типов) |
| Data-modifying CTE (`WITH x AS (INSERT ... RETURNING ...)`) | yes | no |
| DML `RETURNING` (включая update/delete join) | yes | yes |
| `MERGE` (15+) | yes | yes |
| Upsert `ON CONFLICT` | yes | yes |
| Хинты запроса `/*+ ... */` | yes | partial (только подзапросные table hints и блокировки) |
| CTAS / временные таблицы / `ON COMMIT` / `WITH NO DATA` | yes | yes |
| Массовое копирование (`COPY BINARY`) | yes | yes |
| Последовательности + runtime-настройки (`current_setting`/`set_config`) | yes | no |
| Boolean / bit / статистические / регрессионные агрегаты, ordered-set перцентили | yes | partial |
| Табличные параметры (массив / `jsonb_to_recordset`) | yes | no |
| Именованные окна / `GROUPS` / `EXCLUDE` | yes | yes |
| `DISTINCT ON`, `TABLESAMPLE`, `WITH TIES` | yes | no |
| Табличные функции / set-returning functions (`generate_series`, `unnest`, `regexp_matches`, `jsonb_*`, `ts_stat`, ...) | yes | partial (только `unnest`/`generate_series`) |
| Блокировки строк (`FOR UPDATE` / `FOR SHARE`, `OF` на источник, режимы ожидания) | yes | yes |
| Session/server identity (`pg_backend_pid`, `inet_*`) | yes | no |
| **Итого** | 20 / 0 / 0 | 8 / 5 / 7 |

Итог: **Nextorm впереди** — full-text, regex, `jsonb_path_*`, операторы range, data-modifying CTE,
последовательности/настройки, табличные параметры и семейства агрегатов имеют выделенный API в Nextorm и не
имеют его в linq2db.

## SQL Server

| Возможность | Nextorm | linq2db |
|---|---|---|
| `OUTPUT` / `OUTPUT ... INTO` | yes | yes |
| `MERGE` (ветки, `BY SOURCE`) | yes | yes |
| Хинты (table / query / join / tables-in-scope / index) | yes | yes (шире) |
| Temporal `FOR SYSTEM_TIME` | yes | yes |
| `FOR JSON` / `FOR XML` | yes | partial |
| Методы XML-типа (`.value`/`.query`/`.exist`/`.nodes`) | yes | partial |
| `OPENJSON` / `STRING_SPLIT` / `CONTAINSTABLE` / `FREETEXTTABLE` | yes | yes |
| Нативный `PIVOT` / `UNPIVOT` | yes | no |
| Full-text `contains` / `freetext` | yes | yes |
| Текстовый JSON (`json_value`/`json_query`/`json_modify`/`isjson`) | yes | partial |
| Regex (`REGEXP_LIKE`/`REGEXP_REPLACE`, 2025+) | yes | no |
| Нативные табличные параметры | yes | yes |
| Массовое копирование + `SqlBulkCopyOptions` | yes | yes |
| Хранимые процедуры | yes | yes |
| Оконные перцентили (`WITHIN GROUP ... OVER`) | yes | partial |
| Широта библиотеки скалярных функций | yes | yes |
| Последовательности (`NEXT VALUE FOR`) | yes | yes |
| Блокировки строк (`ForUpdate`/`ForShare`, `LockWaitMode` → `with (updlock)`/`with (holdlock)`) | yes | yes |
| Session/server identity (`@@SERVERNAME`, `HOST_NAME`, `APP_NAME`, `SUSER_NAME`, `ORIGINAL_LOGIN`) | yes | yes |
| **Итого** | 19 / 0 / 0 | 13 / 4 / 2 |

Итог: **паритет по выверенному каталогу T-SQL-скаляров, но в целом linq2db шире**. Issue
[#182](https://github.com/AlexeyShirshov/nextorm/issues/182) свёл 61 закреплённую запись `SqlFn.cs` из linq2db
с Nextorm: 51 покрыта и 10 исключены, причём исключённое — живое изменяемое состояние
connection/session/statement-scope, а не построчные скаляры запроса. Подмножество identity
(`@@SERVERNAME`, `HOST_ID`, `HOST_NAME`, `APP_NAME`, `SUSER_NAME`, `ORIGINAL_LOGIN`) поставлено в
[#219](https://github.com/AlexeyShirshov/nextorm/issues/219), остальное отслеживается в
[#248](https://github.com/AlexeyShirshov/nextorm/issues/248). Nextorm впереди по выделенным билдерам для
`PIVOT`/`UNPIVOT`, `FOR JSON`/`FOR XML`, методов XML, `OPENJSON`/`STRING_SPLIT` и regex-функций 2025.

## MySQL / MariaDB

| Возможность | Nextorm | linq2db |
|---|---|---|
| Текстовый JSON (`json_value`/`json_query`/`json_modify`/`isjson`) + мутации | yes | no |
| Regex (`REGEXP_LIKE`/`REGEXP_REPLACE`) | yes | no |
| Full-text `MATCH ... AGAINST` | yes | yes |
| Upsert `ON DUPLICATE KEY UPDATE` + `INSERT IGNORE` | yes | yes |
| Index hints `USE`/`FORCE`/`IGNORE INDEX` | yes | yes |
| Хинты запроса `/*+ ... */` | yes | yes |
| DML `RETURNING` / `OUTPUT` | partial (MariaDB `UPDATE ... RETURNING`, 13.0+; в MySQL нет) | no |
| Полный оператор `MERGE` (ветки, `BY SOURCE`) | no (движок; key-upsert-форма `Merge()` сводится к `ON DUPLICATE KEY UPDATE`) | no (движок) |
| CTAS / временные таблицы / список колонок | yes | yes |
| Табличные параметры (`JSON_TABLE`) | yes | no |
| Оконные функции (`percent_rank`/`nth_value`) | yes | partial (MySQL 8+) |
| Бинарные UUID-хелперы (`uuid_to_bin`/`bin_to_uuid`) | yes | no |
| Библиотека скалярных функций провайдера | yes | partial (тонкая; только full-text) |
| **MariaDB** temporal `FOR SYSTEM_TIME` | yes | no |
| **MariaDB** последовательности (`NEXT VALUE FOR`, `nextval`/`setval`/`lastval`) | yes | no |
| **MariaDB** `NVL`/`NVL2`/`ADD_MONTHS`/`MONTHS_BETWEEN`/`TO_CHAR`/`JSON_DETAILED`/`XXH3` | yes | no |
| **MariaDB** `INTERSECT ALL` / `EXCEPT ALL` | yes | partial |
| Агрегат произвольного значения (`any` → `ANY_VALUE(x)` в MySQL; MariaDB отклоняет) | yes | no |
| Блокировки строк (`FOR UPDATE`/`FOR SHARE`; `OF` на уровне источника в MariaDB не поддержан by design, fail-closed) | partial | yes |
| Session/server identity (`CONNECTION_ID()`, `USER()`) | yes | no |
| **Итого** | 17 / 2 / 1 | 6 / 3 / 11 |

Итог: **Nextorm впереди** — JSON, regex, UUID-хелперы, табличные параметры и MariaDB-специфичные семейства
(temporal, последовательности, строковые/датные функции, `ALL` для операций над множествами) не имеют
провайдерного API в linq2db; linq2db впереди только по разнообразию хинтов.

## SQLite

| Возможность | Nextorm | linq2db |
|---|---|---|
| Потоковое чтение LOB по локатору `rowid` | yes | no |
| DML `RETURNING` | yes | yes |
| Upsert `ON CONFLICT DO UPDATE` / `DO NOTHING` + `INSERT OR IGNORE` | yes | yes |
| Index hints `INDEXED BY` / `NOT INDEXED` | yes | yes |
| JSON1 (`json()`/`jsonb()`/`json_extract`, `->`/`->>`, `json_each`/`json_tree`) | yes | no |
| Regex (`regexp`/`regexp_replace`) | yes | no |
| Пользовательские агрегаты (`stdev`/`stdevp`/`var`/`varp`) | yes | no |
| Именованные окна / `GROUPS` / `EXCLUDE`, `percent_rank`/`cume_dist`/`nth_value` | yes | partial |
| Табличные параметры (`json_each`) | yes | no |
| CTAS / временная таблица | yes | yes |
| Full-text запрос + `FROM` (FTS3 / FTS4 / FTS5) | yes | yes (обширно) |
| FTS5 maintenance/control (`automerge`/`crisismerge`/`merge`/`optimize`/`rebuild`/`integrity-check`) | yes | yes (а также FTS3/FTS4) |
| Библиотека скалярных функций провайдера | yes | no (только full-text) |
| Session/server identity | no (fail-closed; рендерится только `version()`) | no |
| **Итого** | 13 / 0 / 1 | 6 / 1 / 7 |

Итог: **смешанно, в сумме шире Nextorm** — Nextorm добавляет JSON1, regex, пользовательские агрегаты, рамки
окон `GROUPS`/`EXCLUDE`, табличные параметры, потоковое чтение LOB, поверхность full-text **запроса + FROM**
в SQLite ([#181](https://github.com/AlexeyShirshov/nextorm/issues/181)) и билдер команд
**FTS5 maintenance/control**
([#195](https://github.com/AlexeyShirshov/nextorm/issues/195)/[#196](https://github.com/AlexeyShirshov/nextorm/issues/196)).
linq2db впереди только по **FTS3/FTS4 maintenance** и командам FTS5
`delete`/`delete-all`/`pgsz`/`usermerge`.

## ClickHouse

| Возможность | Nextorm | linq2db |
|---|---|---|
| Строковый JSON (`JSONExtract*`/`visitParamExtract*`, `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS`) + `JSONAllPaths`/`toJSONString` | yes | no |
| Массивы + higher-order (lambda) функции | yes | partial |
| Клауза `ARRAY JOIN` | yes | no |
| Regex (`match`/`replaceRegexpAll`, RE2) | yes | partial |
| Строгость join `ANY`/`ALL`/`ASOF`, `GLOBAL`, `SEMI`/`ANTI`/`PASTE` | yes | partial |
| `FINAL` / `SAMPLE` / `PREWHERE` / `SETTINGS` | yes | partial |
| `LIMIT n BY expr` | yes | no |
| `GROUP BY ... WITH TOTALS` | yes | no |
| Распределённый `GLOBAL IN` | yes | no |
| Словари (`dictGet`/`dictHas`/...) | yes | no |
| Агрегаты `uniq`/quantile/top-K/sequence, `argMin`/`argMax`, комбинатор `-If`, bitmap/`sumMap` | yes | partial |
| Оконные функции `lagInFrame`/`leadInFrame` | yes | no |
| Именованные окна / рамка `GROUPS`, `percent_rank`/`cume_dist`/`nth_value` | yes | partial |
| Табличные функции (`numbers`/`zeros`/`generateRandom`/`values`/`url`/`s3`/`file`/`remote*`/`cluster*`) | yes (объявлены) | no |
| Поверхность конвертации дат (`toDate`/`toStartOf*`/`toYYYYMM`/...) | yes | partial |
| Скаляры Map / hash / `generateULID` | yes | no |
| CTAS `ENGINE = MergeTree ... AS SELECT` | yes | yes |
| DML-мутации (`ALTER TABLE ... UPDATE`/`DELETE`) | yes | no |
| Табличные параметры (`Array(T)`/`Array(Tuple)` + `arrayJoin`) | yes | no |
| Хранимые процедуры / массовое копирование / транзакции / потоковое чтение LOB | no (движок) | no (движок) |
| Поверхность tuple (`tuple(...)`, `tupleElement`, арность 1-7) | yes | no |
| Нативный тип колонки JSON (`[JsonColumn]`, `JsonObject`/`JsonDocument`/`JsonElement`) | yes | no |
| Перегрузки `any`/`anyLast` + `multi_if` (многоветвесный условный оператор) | yes | no |
| Session/server identity (`connectionId()`, `hostName()`, `tcpPort()`) | yes | no |
| **Итого** | 23 / 0 / 1 | 1 / 7 / 16 |

Итог: **Nextorm впереди решительно** — публичная ClickHouse-специфичная поверхность linq2db — это по сути
`AsClickHouse` плюс хинты, тогда как Nextorm выставляет набор клауз, семейства агрегатов, словари, табличные
функции и библиотеку скаляров.

## Общая оценка

На шести общих движках Nextorm выставляет выделенный провайдерный API на большем числе поверхностей.
Сильнейшие зоны, где эквивалента в linq2db не найдено:

- Библиотека скалярных функций для **всех** провайдеров: `SqlFunctions.Postgres`, `SqlFunctions.SqlServer`,
  `SqlFunctions.MySql`, `SqlFunctions.Sqlite` и `SqlFunctions.ClickHouse`, плюс общий гейт session-info,
  который несёт identity-скаляры из [#219](https://github.com/AlexeyShirshov/nextorm/issues/219) по четырём
  провайдерам.
- PostgreSQL: full-text, regex, `jsonb_path_*`, операторы range, data-modifying CTE, табличные функции
  (`generate_series`/`unnest`/`jsonb_*`/`ts_stat`), последовательности/настройки,
  boolean/bit/статистические/регрессионные агрегаты, ordered-set перцентили, табличные параметры.
- MySQL/MariaDB: JSON, regex, бинарные UUID, агрегат произвольного значения (`any` → `ANY_VALUE`), и всё
  MariaDB-специфичное семейство (temporal, последовательности,
  `NVL`/`ADD_MONTHS`/`TO_CHAR`/`JSON_DETAILED`/`XXH3`, `ALL` для операций над множествами).
- SQLite: JSON1, regex, пользовательские агрегаты, рамки окон `GROUPS`/`EXCLUDE`, табличные параметры,
  потоковое чтение LOB по `rowid`, FTS5 maintenance/control.
- ClickHouse: набор клауз (`FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS`/`LIMIT BY`/`ARRAY JOIN`/`WITH TOTALS`/
  `GLOBAL IN`), семейства агрегатов с комбинаторами `-If` и `any`/`anyLast`, словари, табличные функции,
  поверхность tuple, нативные `JSON`-колонки, `multi_if`, библиотека скаляров.
- Выделенные fluent-билдеры, подключённые к конструктору запросов (`OUTPUT INTO`, `MERGE`, `PIVOT`/`UNPIVOT`,
  `FOR JSON`/`FOR XML`, temporal, `WITH TOTALS`), а не сырой SQL.

Где linq2db впереди или на равных:

- Широта скалярных функций SQL Server и разнообразие хинтов.
- **FTS3/FTS4 maintenance** в SQLite и команды FTS5 `delete`/`delete-all`/`pgsz`/`usermerge`; Nextorm
  поставляет поверхность **запроса + FROM** и билдер команд **FTS5 maintenance/control**.
- Более широкий список движков (Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, DuckDB,
  Ydb).

Паритет (у обеих, если позволяет движок): ядро DML `RETURNING`/`OUTPUT`/`MERGE`/upsert, массовое
копирование, хранимые процедуры, последовательности, блокировки строк, общие CTAS/временные таблицы,
index/query hints, именованные окна.

## Оговорки

- `no` — это находка по результатам поиска в провайдер-специфичной extension-поверхности библиотеки, а не
  доказательство невозможности: конструкт остаётся выразимым через сырой SQL (в linq2db — через
  `Sql.Ext`/`Sql.Function`). Утверждение — о ширине **выделенного** провайдерного API.
- Часть записей Nextorm (распределённые табличные функции ClickHouse) объявлены и закрыты гейтом, но
  намеренно не покрыты end-to-end — см. [провайдер ClickHouse](../connecting-providers/clickhouse.md).
- Провайдерные поверхности linq2db развиваются; оценки датированы **2026-10-04**.
- MySQL и MariaDB — отдельные провайдеры Nextorm, но один провайдер linq2db, поэтому таблица
  MySQL/MariaDB сводит формы обеих библиотек в один список.

## См. также

- [«Возможности»](capabilities.md) — матрица на уровне конструктов для Dapper, linq2db и EF Core.
- [Бенчмарки](benchmarks.md) и [Feature-бенчмарки (2026-10-17)](feature-benchmarks-2026-10-17.md).
- [Обзор провайдеров](../connecting-providers/overview.md).
- [Провайдер-специфичный SQL](../guide/provider-specific-sql/overview.md).
- [Скалярные функции](../scalar-functions/index.md).
- [Ограничения и что вне области](../advanced/limitations.md).
