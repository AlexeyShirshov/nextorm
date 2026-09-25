# Возможности: Nextorm против linq2db и EF Core

Сводка по категориям: что поддерживает каждая библиотека. Обозначения: **yes** — поддержка первого класса;
**partial** — поддержка с оговорённым ограничением либо реализуемо, но не реализовано; **no** — не
поддерживается. В ячейке Nextorm ограничение самой СУБД указано в скобках; оценку это не понижает. Пробелы
конкурентов с открытым issue даны ссылкой. На **2026-09-25**.

Это верхнеуровневый обзор — категории, важные при выборе библиотеки, а не каждый конструкт.

## Запросы

| Область | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Проекции (сущность, DTO, record, tuple, скаляр) | yes | yes | yes |
| JOIN (`INNER`/`LEFT`/`RIGHT`/`FULL`/`CROSS`) | yes (`FULL JOIN` не на MySQL/MariaDB) | yes | partial (`RIGHT`/`FULL` требуют обхода) |
| `APPLY` / `LATERAL` | yes (выключено там, где у движка нет lateral-источника) | yes | partial |
| Подзапросы (скалярные, коррелированные, `EXISTS`/`IN`/`ANY`/`ALL`) | yes | yes | yes |
| `GROUP BY` / `HAVING` / агрегаты | yes | yes | partial (нет `FILTER`, меньше семейств агрегатов) |
| `ROLLUP` / `CUBE` / `GROUPING SETS` | yes | yes | partial |
| Оконные функции (`OVER`, ранжирование, рамки) | yes | partial | partial |
| Операции над множествами (`UNION`/`INTERSECT`/`EXCEPT` и `ALL`) | yes | yes | yes |
| CTE, включая рекурсивные | yes (data-modifying CTE на PostgreSQL) | yes | partial (нет data-modifying CTE) |
| `DISTINCT ON` / `WITH TIES` / `TABLESAMPLE` | yes (зависит от провайдера) | no | no |
| Temporal-таблицы (`FOR SYSTEM_TIME`) | yes (SQL Server, MariaDB) | no | no |
| Блокировки строк (`FOR UPDATE`, `NOWAIT`/`SKIP LOCKED`) | yes | yes | no (только сырой SQL) |
| Хинты запроса / таблицы / индекса | yes | partial | no (сырой SQL или интерцептор) |
| Сырой SQL (целый запрос и как composable-источник) | yes | yes | yes |

## Типы и маппинг

| Область | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Конвертеры значений | yes | yes | yes (`HasConversion`) |
| JSON-колонка ↔ объект | yes | partial ([linq2db#1661](https://github.com/linq2db/linq2db/issues/1661)) | yes (JSON-колонки) |
| Колонки-длительности / interval | yes | yes | partial (только маппинг interval провайдером) |
| Нативные JSON-документы | yes на PostgreSQL | yes | yes |
| Массивы и higher-order функции над массивами | yes на PostgreSQL и ClickHouse | no | partial |
| Row values / tuple | yes на PostgreSQL и ClickHouse | yes | partial |
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
| Возврат затронутых строк | yes | yes | yes |
| Массовая вставка | yes (нативный copy или пакетный `VALUES`) | yes | partial |
| `CREATE TABLE AS SELECT`, временные таблицы | yes | yes | no |
| `OUTPUT ... INTO` | yes (SQL Server) | no ([linq2db#3832](https://github.com/linq2db/linq2db/issues/3832)) | no |
| Транзакции (собственные и переданные извне) | yes | yes | yes |
| Поиск по захваченной коллекции (`dict[column]`, `list[column]`) | yes | no | no |

## Модель и инструменты

| Область | Nextorm | linq2db | EF Core |
|---|---|---|---|
| Класс сущности необязателен (запрос без маппинга типа) | yes | partial | no |
| Навигационные свойства / связи / eager loading | no | yes | yes |
| Отслеживание изменений / identity map | no (по замыслу) | partial | yes |
| Миграции | no | partial (schema API; миграции через сторонние) | yes |
| Генерация по существующей БД | no (маппинг объявляется в коде) | yes | yes |
| Интеграция с EF Core | no | yes | n/a |
| Провайдеры | SQL Server, PostgreSQL, MySQL, MariaDB, SQLite, ClickHouse, in-memory | SQL Server, PostgreSQL, MySQL/MariaDB, Oracle, SQLite, Firebird, DB2, SAP HANA, Informix, Sybase, SQL CE | SQL Server, PostgreSQL, MySQL, SQLite, Oracle и другие |

## См. также

- [Бенчмарки](benchmarks.md) — поставляемые сценарии.
- [Ограничения и что вне области](../advanced/limitations.md).
- [Обзор провайдеров](../providers/overview.md).
