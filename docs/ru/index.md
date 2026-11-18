# Nextorm - высокопроизводительный zero-sql ORM

Эта документация предназначена для разработчиков на .NET, которым нужно выполнять запросы к базе данных из типизированных C#-выражений. Путь ниже ведёт от установки пакетов к первому запросу SQLite, а затем показывает, как отфильтровать строки, выбрать нужные поля и соединить таблицы. Здесь рассматривается API запросов NextORM, а не проектирование баз данных и не основы SQL.

## С чего начать

1. [Установите NextORM и пакет провайдера](getting-started/01-installation.md).
2. [Выполните первый запрос](getting-started/02-quickstart.md).
3. [Отфильтруйте строки](guide/03-filtering-where.md).
4. [Выберите нужные поля](guide/02-projections.md).
5. [Соедините таблицы](guide/04-joins.md).

Если проект уже настроен, откройте нужную тему в каталоге документации ниже.

## Русская документация

### Начало работы

- [Установка](getting-started/01-installation.md)
- [Быстрый старт](getting-started/02-quickstart.md)
- [Сущности и метаданные](getting-started/03-entities-and-metadata.md)
- [Внедрение зависимостей](getting-started/04-dependency-injection.md)

### Руководство

- [Источники запроса](guide/01-query-sources.md)
- [Проекции](guide/02-projections.md)
- [Фильтрация (WHERE)](guide/03-filtering-where.md)
- [Соединения (JOIN)](guide/04-joins.md)
- [Группировка и агрегаты](guide/05-grouping-and-aggregates.md)
- [Сортировка и постраничный вывод](guide/06-sorting-and-paging.md)
- [Подзапросы](guide/07-subqueries.md)
- [Операции над множествами](guide/08-set-operations.md)
- [SELECT DISTINCT](guide/09-distinct.md)
- [Обобщённые табличные выражения (CTE)](guide/10-cte.md)
- [Оконные функции](guide/11-window-functions.md)
- [Пользовательские функции](guide/12-user-defined-functions.md)
- [Табличные функции](guide/13-table-valued-functions.md)
- [Сырой SQL](guide/14-raw-sql.md)
- [Хинты запросов](guide/15-query-hints.md)
- [Поддержка JSON в разных провайдерах](guide/16-json.md)
- [Изменение данных (INSERT)](guide/17-insert-statement.md)
- [Изменение данных (DELETE)](guide/18-delete-statement.md)
- [Изменение данных (UPDATE)](guide/19-update-statement.md)
- [Материализация запроса в таблицу](guide/20-create-table-as.md)
- [Слияние данных (MERGE / upsert)](guide/21-merge-statement.md)
- [Массовая вставка (bulk)](guide/22-bulk-insert.md)
- [Транзакции](guide/23-transactions.md)
- [Колонки-длительности (TimeSpan)](guide/24-duration-columns.md)
- [Выполнение утверждений одним батчем](guide/25-sql-batch.md)
- [Оптимистичная конкурентность и отслеживание изменений](guide/26-optimistic-concurrency.md)
- [Range-колонки (диапазон, хранимый парой скалярных колонок)](guide/27-range-columns.md)
- [Потоковое чтение больших объектов (BLOB/CLOB)](guide/28-large-objects.md)
- [Динамические колонки](guide/29-dynamic-columns.md)
- [Потоковое сохранение данных](guide/30-streaming-data.md)
- [Специфичные для провайдеров возможности](guide/31-provider-specific-features.md)

### Движок

- [Как работает движок](engine/index.md)
- [Построение запроса: fluent к SQL](engine/01-query-pipeline.md)
- [Исполнение и строки](engine/02-execution-and-rows.md)
- [Кэш планов и переиспользование](engine/03-plan-cache-and-reuse.md)

### Связи

- [Связи и однозапросная загрузка](relationships/relationships.md)
- [Неявные навигационные запросы](relationships/implicit-navigation.md)
- [Жадная загрузка дочерних коллекций](relationships/eager-loading.md)
- [Постраничная выборка на родителя при жадной загрузке](relationships/eager-loading-per-parent-paging.md)

### Инфраструктура

- [Подключения](infrastructure/01-connections.md)
- [Интерцепторы](infrastructure/02-interceptors.md)
- [Конвертеры значений и JSON-колонки](infrastructure/03-value-converters.md)
- [Логирование](infrastructure/04-logging.md)

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
- [Системные функции](scalar-functions/09-system-functions.md)
- [Последовательности и генераторы UUID](scalar-functions/10-sequences.md)

### Подключение провайдеров

- [Обзор провайдеров](connecting-providers/overview.md)
- [SQLite](connecting-providers/sqlite.md)
- [SQL Server](connecting-providers/sqlserver.md)
- [PostgreSQL](connecting-providers/postgres.md)
- [In-memory](connecting-providers/in-memory.md)

### Сравнения

- [Nextorm против Dapper, linq2db и EF Core](comparisons/index.md)

### Продвинутое

- [Глобальные фильтры запросов](advanced/query-filters.md)
- [Интеграция с Entity Framework Core](advanced/integration-efcore.md)
- [Ограничения и что вне области](advanced/limitations.md)
- [Нативные стратегии выбора экстремальной строки](advanced/select-where-extrema-native.md)
- [Расширяемость: пользовательские SQL-диалекты и рендереры](advanced/extensibility.md)
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

## Релизы

### 1.0.10

В этом релизе появились векторный поиск, варианты маппинга и маршрутизация данных. Расширилась
проверенная область поддержки NativeAOT. Пакеты по-прежнему предназначены для .NET 10 и .NET 11.

- **Несовместимое изменение для низкоуровневых расширений.** `OuterRefMarker<T>` и
  `ReplaceConstantsExpressionVisitor.GetOuterRefMarkerCI(Type)` удалены. Если вы строили внешние
  ссылки вручную, перейдите на `OuterReference.Create` и пересоберите расширение. Запросы через
  fluent API менять не нужно. См. [ссылки на значения из внешнего запроса](advanced/extensibility.md#ссылка-на-значение-из-внешнего-запроса).
- **Векторный поиск.** `VectorSearch` выполняет точный или приближённый поиск ближайших соседей
  в PostgreSQL и SQL Server 2025+. PostgreSQL также поддерживает типы pgvector `vector`, `halfvec`,
  `sparsevec`, `bit` и их массивы. `Vector` сравнивает координаты по значению, а не ссылки на
  объекты. Требования к серверу и индексам описаны в [руководстве по векторному поиску](guide/36-ai-and-vector-search.md).
- **Маппинг и маршрутизация.** [Вариант маппинга](guide/33-runtime-mapping-variant.md) выбирает
  физическое отображение сущности, а [маршрутизация](guide/34-routing-data-to-topologies.md) —
  хост. [Шардирование с консистентным хешированием](guide/35-sharding-and-horizontal-partitioning.md)
  поддерживает размещение бакетов и запросы по нескольким бакетам. `GroupByBucket` группирует
  строки перед массовой записью. Попытка удалить все строки шардированной сущности завершается
  ошибкой, а не удаляет данные из таблицы по умолчанию.
- **NativeAOT.** На .NET 11 `linux-x64` проверены сценарии для всех шести провайдеров, включая
  запросы, запись, сырой SQL, потоковую выдачу и предзагрузку связей. Мост EF Core по-прежнему
  требует JIT. Подробности и ограничения описаны в [руководстве по NativeAOT](infrastructure/05-nativeaot.md).
- **Коррелированные запросы.** Подзапросы, ссылающиеся на внешнюю строку через навигационное
  свойство, корректно находят псевдоним соединения ([#304](https://github.com/AlexeyShirshov/nextorm/issues/304)).

[Изменения с версии 1.0.9](https://github.com/AlexeyShirshov/nextorm/compare/v1.0.9...1.0.10)

## Документация на английском

Полное руководство: [docs/index.md](../index.md).
