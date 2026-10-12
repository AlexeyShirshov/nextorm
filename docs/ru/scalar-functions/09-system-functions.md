# Системные функции

Кросс-провайдерные функции, сообщающие состояние текущей сессии или сервера, и провайдерные аналоги
того же рода сведений.

## Информация о сессии и сервере (кросс-провайдерная)

`SqlFunctions.Sql.current_user()`, `session_user()`, `current_schema()`, `current_database()` и
`version()` — кросс-провайдерные
([`SessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SessionInfoFunctions)):

| C# | PostgreSQL | SQL Server | MySQL/MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|
| `current_user()` | `current_user` | `current_user` | `current_user()` | `currentUser()` | — |
| `session_user()` | `session_user` | `session_user` | `session_user()` | — | — |
| `current_schema()` | `current_schema` | `schema_name()` | `schema()` | — | — |
| `current_database()` | `current_database()` | `db_name()` | `database()` | `currentDatabase()` | — |
| `version()` | `version()` | `@@version` | `version()` | `version()` | `sqlite_version()` |

Провайдер, который не умеет функцию, выбрасывает `NotSupportedException`.

## Провайдерная информация о сессии и сервере

Провайдеры также отдают сведения о сессии, сервере и каталоге, у которых нет переносимого аналога.
Они живут на провайдерной поверхности и гейтятся по имени; на любом другом провайдере выбрасывают
`NotSupportedException`. Функции идентичности сервера/сессии (`server_name`, `host_id`, `host_name`,
`app_name`) пока не открыты — см.
[#219](https://github.com/AlexeyShirshov/nextorm/issues/219).

### PostgreSQL

`SqlFunctions.Postgres.current_setting(...)` / `set_config(...)` читают и задают runtime GUC-настройки;
входят в расширенную библиотеку скалярных функций
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions),
только PostgreSQL):

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.current_setting(name)` / `current_setting(name, missingOk)` | `current_setting(name[, missing_ok])` |
| `SqlFunctions.Postgres.set_config(name, value, isLocal)` | `set_config(name, value, is_local)` |

### SQL Server

`SqlFunctions.SqlServer.db_id`/`db_name`/`original_db_name`, `schema_id`/`schema_name`,
`type_id`/`type_name`, `current_timezone`/`current_timezone_id`, `getansinull` и
`publishingservername` сообщают текущую базу, схему, тип, часовой пояс и сервер репликации; гейт по
имени через [`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions):

| C# | SQL |
|---|---|
| `db_id()` / `db_id(database)` | `db_id(...)` |
| `db_name()` / `db_name(databaseId)` | `db_name(...)` |
| `original_db_name()` | `original_db_name()` |
| `schema_id()` / `schema_id(schema)` | `schema_id(...)` |
| `schema_name()` / `schema_name(schemaId)` | `schema_name(...)` |
| `type_id(typeName)` / `type_name(typeId)` | `type_id(...)` / `type_name(...)` |
| `current_timezone()` / `current_timezone_id()` | `current_timezone()` / `current_timezone_id()` |
| `getansinull()` / `getansinull(database)` | `getansinull(...)` |
| `publishingservername()` | `publishingservername()` |

Метаданные уровня объектов, колонок, индексов и файлов — на той же поверхности:

| C# | SQL |
|---|---|
| `col_length(table, column)` | `col_length(...)` |
| `col_name(tableId, columnId)` | `col_name(...)` |
| `ident_incr(table)` / `ident_seed(table)` | `ident_incr(...)` / `ident_seed(...)` |
| `index_col(table, indexId, keyId)` | `index_col(...)` |
| `object_definition(objectId)` | `object_definition(...)` |
| `object_id(name)` / `object_id(name, type)` | `object_id(...)` |
| `object_name(id)` / `object_name(id, databaseId)` | `object_name(...)` |
| `object_schema_name(id)` / `object_schema_name(id, databaseId)` | `object_schema_name(...)` |
| `stats_date(tableId, statsId)` | `stats_date(...)` |
| `filegroup_id(name)` / `filegroup_name(id)` | `filegroup_id(...)` / `filegroup_name(...)` |
| `file_id(name)` / `file_idex(name)` / `file_name(id)` | `file_id(...)` / `file_idex(...)` / `file_name(...)` |

Отсутствующий объект/столбец/индекс/статистика даёт `null`, как и нативная функция; необязательные
вторые аргументы (id базы, тип объекта) открыты перегрузками. `object_definition` возвращает исходный
текст T-SQL только при наличии прав (иначе `null`); `file_idex` отличается от `file_id` тем, что не
ограничен текущей базой данных.

## Не открыто (SQL Server)

Десять имён области соединения, сессии или инструкции T-SQL не смоделированы: они сообщают состояние
соединения или инструкции, а не вычисляют значение на строку. Они остаются доступными через сырой SQL
или обёртку `[SqlFunction]`, и у каждого есть триггер пересмотра при появлении конкретного построчного
применения. Подмножество идентичности сессии/сервера (`APP_NAME`, `HOST_ID`, `HOST_NAME`, а также
`SERVER_NAME`) запланировано на общую session-info поверхность — [#219](https://github.com/AlexeyShirshov/nextorm/issues/219).

| Функция | Область |
|---|---|
| `CURRENT_REQUEST_ID` | соединение / запрос |
| `CURRENT_TRANSACTION_ID`, `XACT_STATE` | транзакция |
| `APP_NAME`, `HOST_ID`, `HOST_NAME` | сессия |
| `IDENT_CURRENT` | сессия / таблица |
| `MIN_ACTIVE_ROWVERSION` | база / транзакция |
| `ROWCOUNT_BIG` | инструкция |
| `SCOPE_IDENTITY` | сессия / область |
