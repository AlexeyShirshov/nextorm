# System functions

Cross-provider functions that report the current session or server, and provider-specific equivalents
for the same kind of facts.

## Session and server information (cross-provider)

`SqlFunctions.Sql.current_user()`, `session_user()`, `current_schema()`, `current_database()` and
`version()` are cross-provider
([`SessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SessionInfoFunctions)):

| C# | PostgreSQL | SQL Server | MySQL/MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|
| `current_user()` | `current_user` | `current_user` | `current_user()` | `currentUser()` | — |
| `session_user()` | `session_user` | `session_user` | `session_user()` | — | — |
| `current_schema()` | `current_schema` | `schema_name()` | `schema()` | — | — |
| `current_database()` | `current_database()` | `db_name()` | `database()` | `currentDatabase()` | — |
| `version()` | `version()` | `@@version` | `version()` | `version()` | `sqlite_version()` |

A provider that cannot express a function throws `NotSupportedException`.

## Provider-specific session and server information

Providers also expose facts about the session, server and catalog that have no portable equivalent.
These live on the provider surface and are gated per name; on any other provider they throw
`NotSupportedException`. Session/server-identity functions (`server_name`, `host_id`, `host_name`,
`app_name`) are not exposed yet — see
[#219](https://github.com/AlexeyShirshov/nextorm/issues/219).

### PostgreSQL

`SqlFunctions.Postgres.current_setting(...)` / `set_config(...)` read and set runtime GUC settings;
they belong to the extended scalar library
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions),
PostgreSQL only):

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.current_setting(name)` / `current_setting(name, missingOk)` | `current_setting(name[, missing_ok])` |
| `SqlFunctions.Postgres.set_config(name, value, isLocal)` | `set_config(name, value, is_local)` |

### SQL Server

`SqlFunctions.SqlServer.db_id`/`db_name`/`original_db_name`, `schema_id`/`schema_name`,
`type_id`/`type_name`, `current_timezone`/`current_timezone_id`, `getansinull` and
`publishingservername` report the current database, schema, type, time zone and replication server;
they are gated per name by [`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions):

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

The object-, column-, index- and file-level catalog metadata is on the same surface:

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

A missing object/column/index/statistic yields `null`, like the native function; optional second
arguments (database id, object type) are exposed as overloads. `object_definition` returns the T-SQL
source only when the caller has the permission (otherwise `null`); `file_idex` differs from `file_id`
in that it is not bounded to the current database.

## Not exposed (SQL Server)

Ten T-SQL connection-, session- or statement-scope names are not modelled: they report the state of the
connection or statement rather than compute a per-row value. They remain reachable through raw SQL or a
`[SqlFunction]` wrapper, and each has a review trigger if a concrete per-row use appears. The
session/server-identity subset (`APP_NAME`, `HOST_ID`, `HOST_NAME`, plus `SERVER_NAME`) is planned for
the common session-info surface — [#219](https://github.com/AlexeyShirshov/nextorm/issues/219).

| Function | Scope |
|---|---|
| `CURRENT_REQUEST_ID` | connection / request |
| `CURRENT_TRANSACTION_ID`, `XACT_STATE` | transaction |
| `APP_NAME`, `HOST_ID`, `HOST_NAME` | session |
| `IDENT_CURRENT` | session / table |
| `MIN_ACTIVE_ROWVERSION` | database / transaction |
| `ROWCOUNT_BIG` | statement |
| `SCOPE_IDENTITY` | session / scope |
