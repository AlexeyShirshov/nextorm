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
`type_id`/`type_name`, `current_timezone`/`current_timezone_id`, `getansinull`,
`publishingservername` и живые факты соединения/транзакции/инструкции (`current_request_id`,
`current_transaction_id`, `xact_state`, `ident_current`, `min_active_rowversion`, `rowcount_big`,
`scope_identity`) сообщают текущую базу, схему, тип, часовой пояс, сервер репликации и живое
состояние сервера; гейт по имени через
[`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions):

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
| `current_request_id()` | `current_request_id()` |
| `current_transaction_id()` | `current_transaction_id()` |
| `xact_state()` | `xact_state()` |
| `ident_current(table)` | `ident_current(...)` |
| `min_active_rowversion()` | `min_active_rowversion()` |
| `rowcount_big()` | `rowcount_big()` |
| `scope_identity()` | `scope_identity()` |

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

### Отклонённые имена инструкций и области ошибок (SQL Server)

Десять системных имён SQL Server **отклоняются до SQL**, а не моделируются. Каждый член существует на
[`SqlFunctions.SqlServer`](../guide/provider-specific-sql/sqlserver.md), чтобы отказ был достижим и
проверяем, возвращает вызываемый плейсхолдер (тип плейсхолдера **не** является контрактом
материализации) и бросает `NotSupportedException` со стабильным сообщением
`SqlFunctions.SqlServer.<member> (<TOKEN>) is refused: <reason>`. Отказ поднимается на границе команды
SQL-билдера до вычисления любого аргумента, поэтому член, использованный как одна проекция большего
запроса, падает так же; на сервер не уходит инструкция и не создаётся команда.

| C# | Токен SQL | Плейсхолдер CLR | Причина |
|---|---|---|---|
| `session_context(key)` | `SESSION_CONTEXT` | `object?` | Возвращает `sql_variant`, для которого у nextorm нет достоверного контракта результата CLR; замена с потерями (lossy cast) не выполняется. **Ограничение области/CLR**, а не версионный пробел. |
| `session_id()` | `SESSION_ID` | `string?` | **Не является функцией движка SQL Server**: документированная функция принадлежит Azure Synapse (SQL Server отвечает Msg 195). Идентичность сессии SQL Server выражается через `@@SPID` или `sys.dm_exec_sessions.session_id`; подстановка не выполняется. |
| `rowcount()` | `@@ROWCOUNT` | `int` | Описывает предыдущую инструкцию и **привязан к инструкции**; построчная проекция запроса не может достоверно отдать это значение. |
| `error()` | `@@ERROR` | `int` | Та же область предыдущей инструкции, что у `@@ROWCOUNT`. |
| `error_number()` | `ERROR_NUMBER` | `int?` | Требует области блока `CATCH` SQL Server; вне `CATCH` возвращает NULL, а nextorm не создаёт эту область. |
| `error_severity()` | `ERROR_SEVERITY` | `int?` | Та же область только `CATCH`. |
| `error_state()` | `ERROR_STATE` | `int?` | Та же область только `CATCH`. |
| `error_line()` | `ERROR_LINE` | `int?` | Та же область только `CATCH`. |
| `error_message()` | `ERROR_MESSAGE` | `string?` | Та же область только `CATCH`. |
| `error_procedure()` | `ERROR_PROCEDURE` | `string?` | Та же область только `CATCH`. |

Различие существенно: `SESSION_CONTEXT`, `@@ROWCOUNT`/`@@ERROR` и шесть имён `ERROR_*` — это
**ограничения области/CLR** построчной проекции (они описывают инструкцию или блок `CATCH`, а у
`sql_variant` нет достоверного носителя CLR), тогда как `SESSION_ID` — **наблюдаемое отклонение
движком**: имя, которое документирует другой продукт, в движке SQL Server просто отсутствует. Ни один
из видов не отложен: все десять — детерминированные, протестированные отказы. Поддерживаемые живые
факты той же поверхности (`rowcount_big`, `xact_state`, `scope_identity`, `db_name`, `current_user`, …)
не затронуты и продолжают рендериться.

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

Информационные, криптографические, системные функции и функции Galera в MariaDB, включая оговорки к
результатам проверки, перечислены в [каталоге MariaDB](08-reference.md#mariadb-catalogue).

## Живое состояние (SQL Server)

**Живое изменяемое состояние не является критерием исключения.** Имена T-SQL, сообщающие текущее
состояние соединения, транзакции, сессии или инструкции, открыты как есть, с нативной семантикой SQL
Server; nextorm **не гарантирует** построчную стабильность, непрерывность соединения между запросами
или получение identity. Каждое значение — нативный снимок момента выполнения инструкции, и клиент не
изобретает вокруг него защиту. Для ключа последней вставки используйте `ReturningIdentity<TKey>()`.

| C# | CLR | Оговорка |
|---|---|---|
| `current_request_id()` | `short?` | NULL вне запроса |
| `current_transaction_id()` | `long?` | NULL вне транзакции |
| `xact_state()` | `short` | нативные 0 (нет) / 1 (фиксируемая) / -1 (нефиксируемая); NULL не изобретается |
| `ident_current(table)` | `decimal?` | последняя identity таблицы в **любой** сессии; гонка, не API последней вставки |
| `min_active_rowversion()` | `byte[]` | нативный `binary(8)`, а не числовой счётчик |
| `rowcount_big()` | `long` | предыдущая инструкция того же соединения; отдельный запрос nextorm — своя инструкция |
| `scope_identity()` | `decimal?` | только в том же батче, что и `INSERT`; отдельный запрос видит NULL |

Стабильные конфигурационные константы и константы идентичности той же области остаются на той же
поверхности (`current_timezone`/`current_timezone_id` и подмножество идентичности сессии/сервера) — см.
выше раздел [Идентичность сессии и сервера](#идентичность-сессии-и-сервера).
