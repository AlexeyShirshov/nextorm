# Nextorm - высокопроизводительный zero-sql ORM

## Русская документация

### Начало работы

- [Установка](getting-started/01-installation.md)
- [Быстрый старт](getting-started/02-quickstart.md)
- [Сущности и метаданные](getting-started/03-entities-and-metadata.md)
- [Внедрение зависимостей](getting-started/04-dependency-injection.md)

### Руководство

- [Запросы и проекции](guide/01-querying-and-projections.md)
- [Фильтрация (WHERE)](guide/02-filtering-where.md)
- [Соединения (JOIN)](guide/03-joins.md)
- [Группировка и агрегаты](guide/04-grouping-and-aggregates.md)
- [Сортировка и постраничный вывод](guide/05-sorting-and-paging.md)
- [Подзапросы](guide/06-subqueries.md)
- [Операции над множествами](guide/07-set-operations.md)
- [SELECT DISTINCT](guide/08-distinct.md)
- [Обобщённые табличные выражения (CTE)](guide/09-cte.md)
- [Оконные функции](guide/10-window-functions.md)
- [Скалярные функции](guide/11-scalar-functions.md)
- [Пользовательские функции](guide/12-user-defined-functions.md)
- [Табличные функции](guide/13-table-valued-functions.md)
- [Сырой SQL](guide/14-raw-sql.md)
- [Переиспользование запросов: кэш и Prepare](guide/15-query-reuse.md)
- [Подключения и логирование](guide/16-connections-and-logging.md)
- [Хинты запросов](guide/17-query-hints.md)
- [Поддержка JSON в разных провайдерах](guide/18-json.md)

### Провайдеры

- [Обзор провайдеров](providers/overview.md)
- [SQLite](providers/sqlite.md)
- [SQL Server](providers/sqlserver.md)
- [PostgreSQL](providers/postgres.md)
- [In-memory](providers/in-memory.md)

### Продвинутое

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
