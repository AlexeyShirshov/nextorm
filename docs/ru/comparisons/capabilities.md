# Возможности: Nextorm против linq2db и EF Core

Сводка по категориям: что поддерживает каждая библиотека. Обозначения: **yes** — поддержка первого класса;
**partial** — поддержка с оговорённым ограничением либо реализуемо, но не реализовано; **no** — не
поддерживается. В ячейке Nextorm ограничение самой СУБД указано в скобках; оценку это не понижает. Пробелы
конкурентов с открытым issue даны ссылкой. На **2026-10-04**.

Это верхнеуровневый обзор — категории, важные при выборе библиотеки, а не каждый конструкт.

## Запросы

| Область | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Проекции (сущность, DTO, record, tuple, скаляр) | yes | yes | yes |
| JOIN (`INNER`/`LEFT`/`RIGHT`/`FULL`/`CROSS`); именованные алиасы соединений | yes (`FULL JOIN` не на MySQL/MariaDB; alias-типы только на SQL-провайдерах) | yes (без именованных алиасов) | partial (`RIGHT`/`FULL` требуют обхода) |
| `APPLY` / `LATERAL` | yes (выключено там, где у движка нет lateral-источника) | yes | partial |
| Подзапросы (скалярные, коррелированные, `EXISTS`/`IN`/`ANY`/`ALL`) | yes | yes | yes |
| `GROUP BY` / `HAVING` / агрегаты | yes | yes | partial (нет `FILTER`, меньше семейств агрегатов) |
| `ROLLUP` / `CUBE` / `GROUPING SETS` | yes | yes | partial |
| Оконные функции (`OVER`, ранжирование, рамки) | yes | partial | partial |
| Операции над множествами (`UNION`/`INTERSECT`/`EXCEPT` и `ALL`) | yes | yes | yes |
| CTE, включая рекурсивные | yes (data-modifying CTE на PostgreSQL) | yes (нет data-modifying CTE) | partial (нет data-modifying CTE) |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` | yes (зависит от провайдера) | no | no |
| Выбор экстремальной строки (`SelectWhereMax`/`SelectWhereMin`) | yes | partial (оконные функции + `OrderBy`/`Take`) | partial (`OrderBy`+`First`; `MaxBy`/`MinBy` в EF Core 11) |
| Per-query переопределения источника (таблица/схема/база/сервер) | yes | partial | partial (на уровне модели) |
| Temporal-таблицы (`FOR SYSTEM_TIME`) | yes (SQL Server, MariaDB) | no | no |
| Блокировки строк (`FOR UPDATE`, `NOWAIT`/`SKIP LOCKED`) | yes | yes | no (только сырой SQL) |
| Хинты запроса / таблицы / индекса | yes | partial | no (сырой SQL или интерцептор) |
| Сырой SQL (целый запрос, composable-источник и сырые команды с параметрами/выходными параметрами/несколькими наборами результатов) | yes | yes | yes |
| Несколько наборов результатов из одного батча (`AddQuery<TResult>` + `Execute`/`ExecuteAsync`) | yes (PostgreSQL, SQL Server, MySQL, MariaDB, SQLite) | yes | no |
| Хранимые процедуры (`ExecuteProcedure`, `CommandType.StoredProcedure`) | yes (SQL Server, PostgreSQL, MySQL/MariaDB) | yes | yes |
| Стриминг result-set (поток LOB, `ToDataReader`) | yes | yes | partial (сырой reader) |
| Экспорт result-set в поток (JSON, CSV) | yes | partial (клиентская сериализация) | partial (клиентская сериализация) |

## Типы и маппинг

| Область | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Конвертеры значений | yes | yes | yes (`HasConversion`) |
| Динамические колонки (хранилище `[DynamicColumns]`) | yes | yes | no (обход через JSON/TPH) |
| JSON-колонка ↔ объект | yes | partial ([linq2db#1661](https://github.com/linq2db/linq2db/issues/1661)) | yes (JSON-колонки) |
| Колонки-длительности / interval | yes | yes | partial (только маппинг interval провайдером) |
| Нативные JSON-документы | yes на PostgreSQL | partial (тип + `@>`/`#>>`/`Json.Value`; не полная библиотека `jsonb_*`) | partial (маппинг JSON-колонок + Npgsql `EF.Functions.Json*`) |
| Массивы и higher-order функции над массивами | yes на PostgreSQL и ClickHouse | partial (операторы массивов PostgreSQL) | partial |
| Row values / tuple | yes — PostgreSQL `ROW`/`(row).fN` и ClickHouse `tuple`/`tupleElement`; плоский операнд сравнения `(a, b)` в MySQL/MariaDB/SQLite | yes | partial |
| Range-типы и range поверх пары скалярных колонок | yes | partial | partial |
| Collation и ordinal-семантика строк | yes | partial ([linq2db#5927](https://github.com/linq2db/linq2db/issues/5927)) | partial |
| Соглашения об именах (например snake_case) | yes (opt-in, встроенное) | partial | partial |
| Квотирование идентификаторов | yes (opt-in) | yes (включено по умолчанию) | yes (всегда) |
| Регистр ключевых слов SQL | yes (opt-in) | no | no |

## Провайдерные функции

| Область | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Переносимая библиотека скалярных функций (строки, математика, даты, условия) | yes | yes | partial (расширения провайдеров) |
| Полнотекстовый поиск | yes (SQL Server, PostgreSQL, MySQL/MariaDB) | yes | partial |
| Нативная библиотека строк / regexp | yes (только для провайдера) | yes | partial |
| Трансляция CLR `Regex` | yes (включая SQL Server 2025+) | partial ([linq2db#698](https://github.com/linq2db/linq2db/issues/698)) | no |
| `string.Format` / интерполяция в SQL | yes (culture-invariant подмножество) | partial ([linq2db#5921](https://github.com/linq2db/linq2db/issues/5921)) | no |
| Табличные функции | yes | yes | yes |
| Динамическая схема результата табличных функций | yes | no | no |
| Нативный источник `PIVOT` / `UNPIVOT` | yes (SQL Server) | no | no |
| `FOR JSON` / `FOR XML` | yes (SQL Server) | yes | partial |

## Запись данных

| Область | Nextorm | linq2db | EF Core |
|---|---|---|---|
| `INSERT` / `UPDATE` / `DELETE` / `MERGE` | yes | yes | partial (нет `MERGE`) |
| Возврат затронутых строк | yes | yes | partial (`ExecuteUpdate`/`ExecuteDelete` возвращают счётчик; значения строк — через `SaveChanges`) |
| Массовая вставка | yes (нативный copy или пакетный `VALUES`) | yes | partial |
| Табличные параметры | yes (нативно в SQL Server; эмуляция массивом/JSON в PostgreSQL, MySQL/MariaDB, SQLite и ClickHouse) | yes | no |
| `CREATE TABLE AS SELECT`, временные таблицы | yes | yes | no |
| `TRUNCATE` | yes (SQLite/in-memory отклоняют) | yes | no (только сырой SQL) |
| Оптимистичная конкурентность | partial (явный паттерн с токеном) | yes | yes |
| `OUTPUT ... INTO` | yes (SQL Server) | partial (`…WithOutputInto` пишет в таблицу; совмещённый returning+INTO открыт [#3832](https://github.com/linq2db/linq2db/issues/3832)) | no |
| Транзакции (собственные и переданные извне) | yes | yes | yes |
| Поиск по захваченной коллекции (`dict[column]`, `list[column]`) | yes | no (открытый [linq2db#5879](https://github.com/linq2db/linq2db/issues/5879); поддержан только `Contains` → `IN`) | no (индексатор не транслируется; клиентская оценка заблокирована) |

## Модель и инструменты

| Область | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Класс сущности необязателен (запрос без маппинга типа) | yes | partial | no |
| Навигационные свойства / связи / eager loading | partial (декларативные метаданные связей O2M/M2O/O2O/M2M и однозапросный загрузчик `JoinInto`, плюс eager loading уровня 1 `LoadWith` и неявная навигация по объявленным связям; вывод по конвенции FK отсутствует и в linq2db) | yes | yes |
| Конфигурация контекста, логирование, DI и кэш планов (`Prepare()`) | yes | yes | yes |
| Отслеживание изменений / identity map | no (по замыслу) | partial | yes |
| Миграции | no | partial (schema API; миграции через сторонние) | yes |
| Генерация по существующей БД | no (маппинг объявляется в коде) | yes | yes |
| Интеграция с EF Core | yes (read-only MVP, `nextorm.entityframeworkcore`) | yes | n/a |
| Провайдеры | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | SQL Server, PostgreSQL, MySQL/MariaDB, SQLite, Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, ClickHouse, DuckDB, Ydb | SQL Server, PostgreSQL, MySQL, SQLite, Oracle и другие |

## См. также

- [Бенчмарки](benchmarks.md) — поставляемые сценарии.
- [Ограничения и что вне области](../advanced/limitations.md).
- [Обзор провайдеров](../providers/overview.md).
