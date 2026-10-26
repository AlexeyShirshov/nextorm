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

Провайдер, который не умеет функцию, выбрасывает `NotSupportedException` при трансляции.

## Идентичность сессии и сервера

Общая поверхность `SqlFunctions.Sql` несёт функции идентичности, гейтящиеся **по имени** через ту же
возможность session-info ([`SessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SessionInfoFunctions),
[`ISessionInfoFunctions.Supports`](xref:NextORM.Core.ISessionInfoFunctions.Supports(System.String))): сведения
PostgreSQL о backend/сети (`pg_backend_pid`, `inet_server_addr`, `inet_server_port`, `inet_client_addr`,
`inet_client_port`), id соединения (`connection_id`) и сообщённая клиентом учётная запись (`user`).
Провайдерные члены живут на `SqlFunctions.SqlServer` (гейт
[`ISqlServerFunctions.SupportsSessionIdentity`](xref:NextORM.Core.ISqlServerFunctions.SupportsSessionIdentity))
и `SqlFunctions.ClickHouse`:

| C# | CLR | PostgreSQL | SQL Server | MySQL/MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|---|
| `SqlServer.server_name()` | `string?` | — | `@@SERVERNAME` | — | — | — |
| `SqlServer.host_id()` | `string?` | — | `HOST_ID()` | — | — | — |
| `SqlServer.host_name()` | `string?` | — | `HOST_NAME()` | — | — | — |
| `SqlServer.app_name()` | `string?` | — | `APP_NAME()` | — | — | — |
| `SqlServer.suser_name()` | `string?` | — | `SUSER_NAME()` | — | — | — |
| `SqlServer.original_login()` | `string?` | — | `ORIGINAL_LOGIN()` | — | — | — |
| `Sql.pg_backend_pid()` | `int` | `pg_backend_pid()` | — | — | — | — |
| `Sql.inet_server_addr()` | `string?` | `CAST(inet_server_addr() AS text)` | — | — | — | — |
| `Sql.inet_server_port()` | `int?` | `inet_server_port()` | — | — | — | — |
| `Sql.inet_client_addr()` | `string?` | `CAST(inet_client_addr() AS text)` | — | — | — | — |
| `Sql.inet_client_port()` | `int?` | `inet_client_port()` | — | — | — | — |
| `Sql.connection_id()` | `ulong` | — | — | `CAST(CONNECTION_ID() AS UNSIGNED)` | `CAST(connectionId() AS UInt64)` | — |
| `Sql.user()` | `string` | — | — | `USER()` | — | — |
| `ClickHouse.host_name()` | `string` | — | — | — | `hostName()` | — |
| `ClickHouse.tcp_port()` | `int` | — | — | — | `CAST(tcpPort() AS Int32)` | — |

Все пятнадцать — без аргументов. `—` означает `NotSupportedException` при трансляции: SQL-фолбэка нет,
и провайдер, не включивший функцию, падает безопасно (SQLite не поддерживает ни одну из них).

**Сервер, клиент и учётная запись.** `server_name()` сообщает экземпляр сервера, к которому подключён
клиент. NextORM рендерит документированный `@@SERVERNAME`; нативной функции `SERVER_NAME()` в SQL Server
нет, поэтому такое имя не эмитится никогда. `host_id()`, `host_name()` и `app_name()` описывают
**клиентский** хост и приложение, которые клиент сам сообщил при подключении. В ClickHouse, наоборот,
`host_name()` — **серверный** хост, ответивший на запрос, а `tcp_port()` — его порт прослушивания,
поэтому SQL Server `host_name()` и ClickHouse `host_name()` — разные факты. `user()` — пара
`user@host` подключающегося клиента MySQL/MariaDB, и она **не** заменяет `current_user()`, который
остаётся эффективной аутентифицированной учётной записью; на серверной стороне
`current_user()`/`session_user()` — это идентичность безопасности, а пара SQL Server
`suser_name()`/`original_login()` вычисляется из токена безопасности (`original_login()` переживает
переключение контекста `EXECUTE AS`).

**Семантика NULL.** `server_name()`, `host_id()`, `host_name()`, `app_name()`, `suser_name()` и
`original_login()` — `string?`, потому что SQL Server может вернуть SQL NULL (экземпляр без
настроенного имени или клиент, ничего не сообщивший). Значения PostgreSQL `inet` возвращаются текстом
(новый CLR-тип сети не вводится) и равны SQL NULL при unix-socket соединении, поэтому
`inet_server_addr()` и `inet_client_addr()` — `string?`, а `inet_server_port()` и `inet_client_port()`
— `int?`; `pg_backend_pid()` — ненулевой `int`. `connection_id()` — ненулевой `ulong`: id соединения
на серверной стороне (не id пользователя), расширенный до 64 бит в MySQL/MariaDB и ClickHouse.

**Оговорка по безопасности.** `host_id()`, `host_name()` и `app_name()` возвращают **сообщённые
клиентом, недоверенные** значения: это диагностические факты сессии, а не идентичность безопасности.
Для авторизации надёжна только аутентифицированная идентичность (`current_user()`, `session_user()` и
производные от токена `suser_name()`/`original_login()`).

## Провайдерная информация о сессии и сервере

Провайдеры также отдают сведения о сессии, сервере и каталоге, у которых нет переносимого аналога.
Они живут на провайдерной поверхности и гейтятся по имени; на любом другом провайдере выбрасывают
`NotSupportedException`. Подмножество идентичности сервера/сессии (`server_name`, `host_id`, `host_name`,
`app_name`, `suser_name`, `original_login` и общие сетевые/соединительные факты `Sql`) описано выше в
разделе [Идентичность сессии и сервера](#идентичность-сессии-и-сервера).

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

## Криптографические случайные байты

`SqlFunctions.Sql.crypt_gen_random(int? length)` возвращает `length` криптографически стойких случайных
байт как `byte[]?`. Переносимый домен — 1–1024 байта: **константная** длина вне его бросает
`ArgumentOutOfRangeException` до генерации SQL, а runtime-значение вне домена сохраняет ошибку
провайдера (единое сообщение не обещается). NULL-длина даёт SQL NULL.

| Провайдер | SQL |
|---|---|
| SQL Server | `CASE WHEN length IS NULL THEN CAST(NULL AS varbinary(8000)) ELSE CRYPT_GEN_RANDOM(length) END` |
| PostgreSQL | `CASE WHEN length IS NULL THEN CAST(NULL AS bytea) ELSE gen_random_bytes(length) END` |
| MySQL / MariaDB | `CASE WHEN length IS NULL THEN CAST(NULL AS BINARY) ELSE RANDOM_BYTES(length) END` |

Путь PostgreSQL вызывает `gen_random_bytes` из расширения **`pgcrypto`**: установите его на сервере
(`CREATE EXTENSION pgcrypto`); nextorm не устанавливает расширения и не детектирует их отсутствие.
ClickHouse, SQLite и in-memory провайдер отклоняют член с
`The crypt_gen_random function is not supported by this provider.` Сгенерированные байты не
предсказуемы и не воспроизводимы из запроса, и ни один тест не обещает их содержимое или энтропию.

## Не открыто (SQL Server)

Семь имён T-SQL, сообщающих **живое изменяемое состояние соединения, сессии или инструкции**, не
смоделированы: их значение меняется независимо от запроса — начинается или заканчивается транзакция,
выполняется предыдущая инструкция, другая сессия вставляет строку, — а не вычисляется построчно из
колонок строки. Они остаются доступными через сырой SQL или обёртку `[SqlFunction]`, и у каждого есть
триггер пересмотра при появлении конкретного построчного применения.

Стабильные конфигурационные константы и константы идентичности той же области открыты:
`CURRENT_TIMEZONE`/`CURRENT_TIMEZONE_ID` и подмножество идентичности сессии/сервера (`SERVER_NAME`/
`@@SERVERNAME`, `HOST_ID`, `HOST_NAME`, `APP_NAME`, `SUSER_NAME`, `ORIGINAL_LOGIN`) — см. выше раздел
[Идентичность сессии и сервера](#идентичность-сессии-и-сервера).

| Функция | Область |
|---|---|
| `CURRENT_REQUEST_ID` | соединение / запрос |
| `CURRENT_TRANSACTION_ID`, `XACT_STATE` | транзакция |
| `IDENT_CURRENT` | сессия / таблица |
| `MIN_ACTIVE_ROWVERSION` | база / транзакция |
| `ROWCOUNT_BIG` | инструкция |
| `SCOPE_IDENTITY` | сессия / область |
