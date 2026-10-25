# Возможности: Nextorm против Dapper, linq2db и EF Core

Одна матрица на уровне конструктов: что поддерживает каждая библиотека и где нет. Обозначения: **yes** —
поддержка первого класса; **partial** — поддержка с оговорённым ограничением либо реализуемо, но не
реализовано; **no** — не поддерживается. В ячейке Nextorm ограничение самой СУБД указано в скобках; оценку
это не понижает. Пробелы конкурентов с открытым issue даны ссылкой. На **2026-10-04**.

Строки сгруппированы по областям — запросы, типы и маппинг, провайдерные функции, запись, модель и
инструменты. Матрицу закрывает строка **Итого**: количество yes / partial / no в этом столбце (`n/a` не
учитывается, перечень провайдеров не оценивается). Ширина API по каждому движку — на странице
[«Функции провайдеров»](provider-features.md).

| Область | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Проекции (сущность, DTO, anonymous, record, tuple, скаляр; несопоставленные колонки через `SqlFunctions.Column`) | yes | yes | yes |
| Предикаты (`WHERE`: сравнение, `and`/`or`/`!`, арифметика, битовые операции/сдвиг) | yes | yes | yes |
| Null-safe-сравнение (`IS [NOT] DISTINCT FROM`; `SqlFunctions.Sql.is_distinct_from` / `is_not_distinct_from`) | yes (нативно на PostgreSQL, SQLite и MySQL/MariaDB; эмуляция в SQL Server и ClickHouse) | yes (`Sql.Is[Not]DistinctFrom`; покрытие зависит от провайдера) | no |
| JOIN (`INNER`/`LEFT`/`RIGHT`/`FULL`/`CROSS`, join к производным таблицам, до 8 источников) | yes (`FULL JOIN` не на MySQL/MariaDB) | yes | partial (`RIGHT`/`FULL` требуют обхода) |
| Именованные алиасы join (`Alias.<Name>`) | yes (только SQL-провайдеры) | no | no |
| `APPLY` / `LATERAL` | yes (выключено там, где у движка нет lateral-источника) | yes | partial |
| Строгость join (`ANY`/`ALL`/`ASOF`) и `GLOBAL` в ClickHouse | yes | yes | no |
| Подзапросы (`FROM`, скалярные, коррелированные `EXISTS`/`IN`/`ANY`/`ALL`) | yes (in-memory вычисляет только глубину один) | yes | yes |
| `IN` по списку/массиву | yes | partial (нет распределённого `GLOBAL IN` в ClickHouse) | yes |
| `GROUP BY` / `HAVING` / агрегаты (`FILTER`, ClickHouse `-If`, статистические/квантильные/ordered-семейства) | yes | yes (`Sql.Ext`) | partial (нет `FILTER`, меньше семейств агрегатов) |
| `ROLLUP` / `CUBE` / `GROUPING SETS` / `WITH TOTALS` | yes | yes | partial |
| `LIMIT n BY expr` | yes на ClickHouse | no | no |
| Модификаторы запроса ClickHouse (`FINAL`, `SAMPLE`, `PREWHERE`, `SETTINGS`) | yes | partial (только `FINAL` и `SETTINGS`) | no |
| `ORDER BY` / пагинация (`LIMIT`/`OFFSET`/`TOP`/`FETCH`) | yes | yes | yes |
| Оконные функции (`OVER`, ранжирование, рамковые агрегаты, `lag`/`lead`, перцентили) | yes (именованные окна, `GROUPS`/`EXCLUDE`) | partial (нет именованных окон и рамок `GROUPS`/`EXCLUDE`) | partial |
| Операции над множествами (`UNION`/`UNION ALL`/`INTERSECT`/`EXCEPT`, `ALL` где он есть у движка) | yes | yes | yes |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` | yes (по провайдерам) | no | no |
| CTE (рекурсивные, типизированные `AsCte`/`AsRecursiveCte`; data-modifying CTE на PostgreSQL) | yes | yes (нет data-modifying CTE) | partial (нет data-modifying CTE) |
| Выбор экстремальной строки (`SelectWhereMax`/`SelectWhereMin`) | yes | partial (оконные функции + `OrderBy`/`Take`) | partial (`OrderBy`+`First`) |
| Per-query переопределения источника (таблица/схема/база/сервер) | yes | partial | partial (на уровне модели) |
| Temporal-таблицы (`FOR SYSTEM_TIME`) | yes (SQL Server, MariaDB) | no | partial (только SQL Server) |
| Блокировки строк (`FOR UPDATE`/`FOR SHARE`, `NOWAIT`/`SKIP LOCKED`) | yes | yes | no (только сырой SQL) |
| Хинты запроса / таблицы / индекса | yes | partial | no (сырой SQL или интерцептор) |
| Сырой SQL (целый запрос, composable-источник, сырые команды с параметрами/выходными параметрами/несколькими наборами результатов) | yes | yes | yes |
| Батч / несколько наборов результатов из одного вызова | yes (PostgreSQL, SQL Server, MySQL, MariaDB, SQLite) | yes | no |
| Хранимые процедуры (`ExecuteProcedure`, `CommandType.StoredProcedure`) | yes (SQL Server, PostgreSQL, MySQL/MariaDB) | yes | yes |
| Стриминг result-set (поток LOB, `ToDataReader`) | yes (последовательное потоковое чтение LOB на PostgreSQL/SQL Server; SQLite буферизует; MySQL/MariaDB/ClickHouse и in-memory отклоняют) | yes | partial (сырой reader) |
| Экспорт result-set в поток (JSON, CSV) | yes | partial (клиентская сериализация) | partial (клиентская сериализация) |
| `CASE WHEN` / тернарный оператор / `switch`, `COALESCE`, числовой `CAST` | yes | yes | yes |
| Скалярные функции строк / математики / дат, `LIKE`, конкатенация, `NULLIF` | yes | yes | yes |
| CLR `Regex` (`IsMatch`/`Replace`, константный шаблон) | yes (PostgreSQL, MySQL/MariaDB, ClickHouse, SQLite, SQL Server 2025+) | partial (открытый [linq2db#698](https://github.com/linq2db/linq2db/issues/698)) | no |
| `string.Format` / интерполяция в SQL | yes (culture-invariant подмножество) | partial ([linq2db#5921](https://github.com/linq2db/linq2db/issues/5921)) | no |
| Арифметика дат (`date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts`, `DateTime.Add*`) | yes | yes | yes |
| Полнотекстовый поиск | yes (SQL Server, PostgreSQL, MySQL/MariaDB; SQLite FTS3/4/5 — запрос + `FROM`, билдер FTS5 maintenance/control) | yes | partial |
| Условные функции (`iif`/`choose`/`multi_if`) | yes | no | no |
| Пользовательские скалярные функции | yes (`[SqlFunction]`) | yes (`DbFunction` / `Sql.Ext`) | yes |
| Табличные функции | yes (`[SqlTableFunction]`) | yes (`TableFunction`; TVF из БД можно скаффолдить) | yes |
| Динамическая схема результата табличных функций | yes | no | no |
| Нативный источник `PIVOT` / `UNPIVOT` | yes (SQL Server) | no | no |
| `FOR JSON` / `FOR XML` | yes (SQL Server) | yes | partial |
| Методы XML-типа (`.value`/`.query`/`.exist`/`.nodes`) | partial (только SQL Server) | yes | no |
| `GREATEST` / `LEAST` | yes (обработка `NULL` зависит от провайдера) | partial | partial |
| `STRING_AGG` / `ARRAY_AGG` | yes (`array_agg` на PostgreSQL) | yes | partial |
| Конвертеры значений | yes | yes | yes (`HasConversion`) |
| Динамические колонки (хранилище `[DynamicColumns]`) | yes | yes | no (обход через JSON/TPH) |
| JSON-колонка ↔ объект | yes | partial ([linq2db#1661](https://github.com/linq2db/linq2db/issues/1661)) | yes (JSON-колонки) |
| Колонки-длительности / interval | yes (PostgreSQL `interval` → `TimeSpan` — только для представимых значений с нулевыми месяцами) | yes | partial (маппинг interval провайдером) |
| Нативные JSON-документы | yes на PostgreSQL | partial (тип `json`/`jsonb` плюс `@>`, `#>>` и `Json.Value`; не полная библиотека `jsonb_*`) | partial (маппинг JSON-колонок + Npgsql `EF.Functions.Json*`) |
| JSON-скаляры (`json_value`/`json_query`/`json_modify`, `isjson`) | yes (SQL Server, MySQL/MariaDB) | yes | partial |
| Строковый JSON + нативные JSON- и dictionary-функции (ClickHouse) | yes на ClickHouse | no | no |
| Массивы и higher-order функции (`cardinality`/`array_*`/`@>`/`&&`, `Array(T)`, `ARRAY JOIN`) | yes на PostgreSQL и ClickHouse | partial (только операторы массивов PostgreSQL) | partial |
| Row values / tuple (`ROW`/`(a, b)`, доступ к элементу, сравнение строк) | yes — PostgreSQL `ROW`/`(row).fN` и ClickHouse `tuple`/`tupleElement`; плоский `(a, b)` в MySQL/MariaDB/SQLite | yes (`Sql.Row`; эмуляция там, где у провайдера нет нативной строки) | partial |
| Range-типы и range поверх пары скалярных колонок (`Range<T>`, `Overlaps`, `range_contains`, инспекция границ) | yes — нативные range/multirange на PostgreSQL; пара скалярных колонок (`[RangeColumns]`) в остальных | partial (только провайдерный маппинг `NpgsqlRange<T>`/multirange; нет переносимого `Range<T>`/пары) | partial |
| Collation и ordinal-семантика строк | yes | partial ([linq2db#5927](https://github.com/linq2db/linq2db/issues/5927)) | partial |
| Соглашения об именах (например snake_case) | yes (opt-in, встроенный `SnakeCaseNamingConvention`) | partial (нет встроенного соглашения) | partial |
| Квотирование идентификаторов | yes (opt-in) | yes (включено по умолчанию) | yes (всегда) |
| Регистр ключевых слов SQL (upper/lower) | yes (opt-in `KeywordCase.Upper`) | no (ключевые слова выводятся в каноническом регистре провайдера) | no |
| `INSERT` / `UPDATE` / `DELETE` / `MERGE` | yes | yes | partial (нет `MERGE`) |
| Возврат затронутых строк | yes | yes | partial (`ExecuteUpdate`/`ExecuteDelete` возвращают счётчик; значения строк — через `SaveChanges`) |
| Массовое копирование / вставка / временные таблицы | partial (нативный `COPY`/`SqlBulkCopy` или пакетный `VALUES`, `ToTable`/`ToTempTable` — уже, чем bulk/temp-поверхность linq2db) | yes | partial |
| Табличные параметры | yes (нативно в SQL Server; эмуляция массивом/JSON/`Array(T)`+`arrayJoin` в PostgreSQL, MySQL/MariaDB, SQLite и ClickHouse) | yes (`TableParameterValue`) | no |
| `CREATE TABLE AS SELECT`, временные таблицы | yes | yes | no |
| `TRUNCATE` | yes (SQLite/in-memory отклоняют) | yes | no (только сырой SQL) |
| Оптимистичная конкурентность | partial (явный паттерн с токеном; метаданных concurrency-токена нет) | yes | yes |
| `OUTPUT ... INTO` | yes (SQL Server) | partial (`…WithOutputInto` пишет в таблицу; совмещённый returning+INTO открыт [#3832](https://github.com/linq2db/linq2db/issues/3832)) | no |
| Транзакции (собственные и переданные извне) | yes (SQLite, PostgreSQL, SQL Server, MySQL/MariaDB; ClickHouse и in-memory отклоняют) | yes | yes |
| Поиск по захваченной коллекции (`dict[column]`, `list[column]`) | yes | no (открытый [linq2db#5879](https://github.com/linq2db/linq2db/issues/5879); поддержан только `Contains` → `IN`) | no (индексатор не транслируется; клиентская оценка заблокирована) |
| Класс сущности необязателен (запрос без маппинга типа) | yes | partial | no |
| Навигационные свойства / связи / eager loading | partial (декларативные метаданные O2M/M2O/O2O/M2M, `JoinInto`, eager loading `LoadWith` — split или single-query, вложенность уровня 2+ с единым режимом на граф и порядком/top-N на уровне — и неявная навигация по объявленным связям; вывод по конвенции FK отсутствует и в linq2db) | yes (`[Association]`, `LoadWith`) | yes |
| Конфигурация контекста, логирование и DI | yes | yes | yes |
| Кэш планов запросов | yes | yes (компилируемые запросы, кэш запросов) | yes |
| Явный `Prepare()` | yes (`IPreparedQueryCommand<TResult>`) | yes | no |
| Отслеживание изменений / identity map | no (по замыслу) | partial | yes |
| Миграции | no | partial (schema API; миграции через сторонние) | yes |
| Генерация по существующей БД | no (маппинг объявляется в коде) | yes | yes |
| Интеграция с EF Core | yes (read-only MVP, `nextorm.entityframeworkcore`) | yes (`linq2db.EntityFrameworkCore`) | n/a |
| Провайдеры | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | SQL Server, PostgreSQL, MySQL/MariaDB, SQLite, Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, ClickHouse, DuckDB, Ydb | SQL Server, PostgreSQL, MySQL, SQLite, Oracle и другие |
| `SelectMany` / `GroupJoin` | partial (только in-memory; SQL-провайдеры отклоняют) | yes (трансляция в SQL) | yes |
| In-memory-провайдер (только чтение, зарегистрированные CLR-наборы) | yes | partial | partial |
| Жизненный цикл соединения (`IConnectionManager`: переданное vs собственное, переиспользование, dispose) | yes | yes | yes |
| Расширяемость (диалект, `[SqlFunction]`/`[SqlTableFunction]`, интерцепторы, глобальные фильтры, сырой SQL) | yes | yes (обширно) | yes |
| **Итого** | 72 / 5 / 3 | 49 / 21 / 10 | 27 / 27 / 25 |

## Где Nextorm впереди

На общей поверхности Nextorm не уступает linq2db или превосходит её; сверх этого он добавляет:

- Полную аналитическую поверхность запросов: все типы join, включая `APPLY`/`LATERAL` (с коррелированными
  источниками), join к производным таблицам, строгость join/`GLOBAL` в ClickHouse, операции над
  множествами, `DISTINCT` (плюс `DISTINCT ON`/`WITH TIES`), CTE (рекурсивные и data-modifying на
  PostgreSQL), оконные функции (именованные окна, `GROUPS`, `EXCLUDE`, перцентили),
  `ROLLUP`/`CUBE`/`GROUPING SETS`/`WITH TOTALS`, temporal-таблицы, блокировки строк, хинты и сырой SQL на
  целый запрос.
- Несопоставленные колонки (`SqlFunctions.Column`), именованные алиасы join (`Alias.<Name>`) и per-query
  переопределения источника — каждое входит в ключ плана.
- `SelectWhereMax`/`SelectWhereMin` как отдельный оператор экстремальной строки с нативной стратегией на
  PostgreSQL и ClickHouse; linq2db собирает это из оконных функций и `OrderBy`/`Take`.
- `[JsonColumn]` с хранилищем Auto/Native/Text — маппинг, которого у linq2db до сих пор нет
  ([linq2db#1661](https://github.com/linq2db/linq2db/issues/1661)).
- Полную явную поверхность записи: `INSERT`/`UPDATE`/`DELETE`/полный `MERGE`, сгенерированные ключи и
  возвращаемые строки, key upsert, массовая вставка, `CREATE TABLE AS SELECT`/временные таблицы,
  data-modifying CTE на PostgreSQL и транзакции — всё явными командами, без отслеживания изменений и
  `SaveChanges`.
- Декларативные связи и eager loading: O2M/M2O/O2O/M2M через `JoinInto`, плюс `LoadWith` (split-запрос по
  умолчанию, opt-in single-query, вложенность уровня 2+, порядок/top-N на родителя) и неявная навигация по
  объявленным связям, с глобальными фильтрами на дочерних уровнях.
- Пакет интеграции с EF Core (`nextorm.entityframeworkcore`), использующий соединение, маппинг модели и
  транзакцию EF.
- Кросс-провайдерные row values, переносимый range поверх пары колонок (`[RangeColumns]`), index hints,
  настраиваемый регистр ключевых слов, полнотекстовый поиск вместе с SQLite-поверхностью FTS3/4/5 и
  билдером FTS5 maintenance/control, а также трансляцию CLR `Regex`.
- Два пути переиспользования (неявный кэш планов и явный `Prepare()`), параметризацию запросов и
  низкоаллокационный дизайн с бенчмарками на уровне или выше Dapper, EF Core и linq2db на поставляемых
  сценариях.

## Осознанные границы: что Nextorm оставляет linq2db

Это сознательные решения по scope в сфокусированной модели Nextorm без отслеживания изменений — не пробелы
на поверхности запросов, где Nextorm не уступает:

- **Отслеживание изменений и identity map**: каждая запись остаётся явной командой; нет `SaveChanges` и
  автоматического отслеживания. linq2db сбрасывает отслеживаемый unit of work.
- **Database-first-инструменты**: linq2db поставляет CLI/T4-генератор, который скаффолдит маппинги сущностей
  и табличных функций по живой БД; Nextorm объявляет маппинги в коде.
- **Широта провайдеров**: помимо общих провайдеров linq2db дополнительно покрывает Oracle, Firebird, DB2,
  SAP HANA, Informix, Sybase, Access, SQL CE, DuckDB и Ydb, тогда как Nextorm добавляет in-memory object
  context и отдельные провайдеры MySQL/MariaDB.
- **DDL / управление схемой**: у linq2db есть `CreateTable`/`DropTable` и schema API; Nextorm меняет схему
  только через `CREATE TABLE AS SELECT` — ad-hoc DDL идёт через `ExecuteRaw` (вне области).

Корреляция на SQL-провайдерах единообразна (произвольная глубина вложенности). In-memory-провайдер вычисляет
коррелированные скаляр/агрегат/`EXISTS`/`IN` глубины один один раз на внешнюю строку и отклоняет только
более глубокие формы (внешняя ссылка в проекции или `ORDER BY` внутреннего запроса, коррелированные
`GROUP BY`/`HAVING`, асинхронный внутренний источник).

## Архитектурные различия

| Аспект | Nextorm | linq2db |
|---|---|---|
| Модель | Конструктор запросов и мапер без отслеживания изменений, с полной явной поверхностью записи (`INSERT`/`UPDATE`/`DELETE`/`MERGE`), массовой вставкой и транзакциями | Явный CRUD-ORM со связями; без автоматического отслеживания изменений |
| Требование к сущности | Класс сущности необязателен — маппинг атрибутами/fluent/конвенциями либо без него, через `From("table")` + `TableAlias` | Нужен сопоставленный класс (атрибуты, fluent или конвенции) |
| Переиспользование | Неявный кэш планов и `Prepare()` | Компилируемые запросы, кэш запросов |
| Потребление результата | Терминалы на запросе без материализации: потоковое чтение LOB одной колонки (`ToStream`/`ToTextReader`), `DbDataReader` во владении вызывающего (`ToDataReader`) и экспорт JSON/CSV (`WriteJson`/`WriteCsv`), стримящие строки без построения `TResult` | Всегда материализует сопоставленный тип построчно; сырой доступ только через `DataConnection.ExecuteReader`/`DataReaderWrapper`, без LOB- и JSON/CSV-терминалов |

## Итог

Для чтения, отчётности и явного изменения данных поверх существующей схемы Nextorm — более сильный выбор: он
покрывает практически всю аналитическую поверхность запросов linq2db — провайдерные семейства функций,
конструкты ClickHouse, кросс-провайдерные row values, табличные функции и многое другое — с меньшим
аллокационным следом, результатами бенчмарков на уровне или выше Dapper, EF Core и linq2db на поставляемых
сценариях и более настраиваемым выводом SQL (квотирование идентификаторов, соглашения об именах и регистр
ключевых слов — opt-in и переопределяются для команды). linq2db остаётся лучшим выбором только там, где тот
же слой должен ещё и отслеживать изменения либо генерировать слой данных по живой схеме — перечисленные выше
осознанные границы.

## См. также

- [Функции провайдеров](provider-features.md) — ширина API по каждому движку против linq2db.
- [Бенчмарки](benchmarks.md) — поставляемые сценарии.
- [Ограничения и что вне области](../advanced/limitations.md).
- [Обзор провайдеров](../connecting-providers/overview.md).
- [JOIN](../guide/04-joins.md) — `CrossApply`/`OuterApply`.
- [Range-колонки](../guide/27-range-columns.md) — `Range<T>` как пара скалярных колонок.
- [Хинты запросов](../guide/15-query-hints.md).
