# Nextorm - высокопроизводительный zero-sql ORM

## Русская документация

### Начало работы

- [Установка](getting-started/01-installation.md)
- [Быстрый старт](getting-started/02-quickstart.md)
- [Сущности и метаданные](getting-started/03-entities-and-metadata.md)
- [Внедрение зависимостей](getting-started/04-dependency-injection.md)

### Руководство

- [Запросы и проекции](querying/index.md)
- [Фильтрация (WHERE)](guide/01-filtering-where.md)
- [Соединения (JOIN)](guide/02-joins.md)
- [Группировка и агрегаты](guide/03-grouping-and-aggregates.md)
- [Сортировка и постраничный вывод](guide/04-sorting-and-paging.md)
- [Подзапросы](guide/05-subqueries.md)
- [Операции над множествами](guide/06-set-operations.md)
- [SELECT DISTINCT](guide/07-distinct.md)
- [Обобщённые табличные выражения (CTE)](guide/08-cte.md)
- [Оконные функции](guide/09-window-functions.md)
- [Пользовательские функции](guide/10-user-defined-functions.md)
- [Табличные функции](guide/11-table-valued-functions.md)
- [Сырой SQL](guide/12-raw-sql.md)
- [Хинты запросов](guide/13-query-hints.md)
- [Поддержка JSON в разных провайдерах](guide/14-json.md)
- [Изменение данных (INSERT)](guide/15-insert-statement.md)
- [Изменение данных (DELETE)](guide/16-delete-statement.md)
- [Изменение данных (UPDATE)](guide/17-update-statement.md)
- [Материализация запроса в таблицу](guide/18-create-table-as.md)
- [Слияние данных (MERGE / upsert)](guide/19-merge-statement.md)
- [Массовая вставка (bulk)](guide/20-bulk-insert.md)
- [Транзакции](guide/21-transactions.md)
- [Колонки-длительности (TimeSpan)](guide/22-duration-columns.md)
- [Выполнение утверждений одним батчем](guide/23-sql-batch.md)
- [Оптимистичная конкурентность и отслеживание изменений](guide/24-optimistic-concurrency.md)
- [Range-колонки (диапазон, хранимый парой скалярных колонок)](guide/25-range-columns.md)
- [Потоковое чтение больших объектов (BLOB/CLOB)](guide/26-large-objects.md)
- [Динамические колонки](guide/27-dynamic-columns.md)
- [Потоковая выдача результатов запроса в CSV](guide/28-csv-export.md)

### Инфраструктура

- [Переиспользование запросов: кэш и Prepare](infrastructure/01-query-reuse-and-caching.md)
- [Подключения](infrastructure/02-connections.md)
- [Интерцепторы](infrastructure/03-interceptors.md)
- [Конвертеры значений и JSON-колонки](infrastructure/04-value-converters.md)
- [Логирование](infrastructure/05-logging.md)

### Скалярные функции

- [Обзор](scalar-functions/index.md)
- [Строковые функции](scalar-functions/01-string-functions.md)
- [Математические функции](scalar-functions/02-math-functions.md)
- [Дата и время](scalar-functions/03-date-and-time.md)
- [Условные функции и приведение](scalar-functions/04-conditionals-and-conversion.md)
- [Агрегаты](scalar-functions/05-aggregates.md)
- [Массивы](scalar-functions/06-arrays.md)
- [JSON и XML](scalar-functions/07-json-and-xml.md)
- [Справочник по провайдерам](scalar-functions/08-reference.md)

### Провайдеры

- [Обзор провайдеров](providers/overview.md)
- [SQLite](providers/sqlite.md)
- [SQL Server](providers/sqlserver.md)
- [PostgreSQL](providers/postgres.md)
- [In-memory](providers/in-memory.md)

### Сравнения

- [Nextorm против Dapper, linq2db и EF Core](comparisons/index.md)

### Продвинутое

- [Глобальные фильтры запросов](advanced/query-filters.md)
- [Интеграция с Entity Framework Core](advanced/integration-efcore.md)
- [Жадная загрузка дочерних коллекций](advanced/eager-loading.md)
- [Связи и однозапросная загрузка](advanced/relationships.md)
- [Ограничения и что вне области](advanced/limitations.md)
- [Краткий справочник API](advanced/api-reference.md)

### Введение

- [Обзор](overview.md)
- [Постановка задачи](motivation.md)

## Overview

Nextorm выполняет две основные функции:

- Генерация SQL-кода
- Преобразование (mapping) реляционных данных в структуры языка (фреймворка): классы, примитивные типы, массивы, списки и т.д.

Nextorm использует библиотеки уровня протокола (например, SqlClient для Microsoft SQL Server или SQLite
для SQLite) и предназначена для создания высокопроизводительного слоя доступа к данным, независимого от
SQL и конкретной СУБД.

## Документация на английском

Полное руководство: [docs/index.md](../index.md).
