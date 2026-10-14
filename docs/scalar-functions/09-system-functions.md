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

A provider that cannot express a function throws `NotSupportedException` at translation.

## Session and server identity

The shared `SqlFunctions.Sql` surface carries identity functions gated **per name** through the same
session-info capability ([`SessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SessionInfoFunctions),
[`ISessionInfoFunctions.Supports`](xref:NextORM.Core.ISessionInfoFunctions.Supports(System.String))): the PostgreSQL
backend/network facts (`pg_backend_pid`, `inet_server_addr`, `inet_server_port`, `inet_client_addr`,
`inet_client_port`), the connection id (`connection_id`) and the client-reported account (`user`).
Provider-only members live on `SqlFunctions.SqlServer` (gated by
[`ISqlServerFunctions.SupportsSessionIdentity`](xref:NextORM.Core.ISqlServerFunctions.SupportsSessionIdentity))
and `SqlFunctions.ClickHouse`:

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

All fifteen are zero-argument. `—` means `NotSupportedException` at translation: there is no SQL
fallback, and a provider that does not opt in fails closed (SQLite supports none of them).

**Server, client and account.** `server_name()` reports the server instance the client is connected to.
NextORM renders the documented `@@SERVERNAME`; SQL Server has no native `SERVER_NAME()` function, so
that name is never emitted. `host_id()`, `host_name()` and `app_name()` describe the **client** host
and application that the client itself reported when it connected. On ClickHouse, by contrast,
`host_name()` is the **server** host that answered the query and `tcp_port()` its listening port, so
SQL Server `host_name()` and ClickHouse `host_name()` are different facts. `user()` is the
MySQL/MariaDB `user@host` pair of the connecting client and does **not** replace `current_user()`, which
remains the effective authenticated account; server-side, `current_user()`/`session_user()` are the
security identity, and the SQL Server pair `suser_name()`/`original_login()` is resolved from the
security token (`original_login()` survives an `EXECUTE AS` context switch).

**Null semantics.** `server_name()`, `host_id()`, `host_name()`, `app_name()`, `suser_name()` and
`original_login()` are `string?` because SQL Server can return SQL NULL (an instance without a
configured name, or a client that reported nothing). PostgreSQL `inet` values are returned as text (no
new CLR network type) and are SQL NULL on a unix-socket connection, so `inet_server_addr()` and
`inet_client_addr()` are `string?` while `inet_server_port()` and `inet_client_port()` are `int?`;
`pg_backend_pid()` is a non-null `int`. `connection_id()` is a non-null `ulong` — the server-side
connection id (not a user id), widened to 64 bits on MySQL/MariaDB and ClickHouse.

**Security caveat.** `host_id()`, `host_name()` and `app_name()` return **client-supplied, untrusted**
values: they are diagnostic session facts, not a security identity. Only the authenticated identity
(`current_user()`, `session_user()`, and the token-derived `suser_name()`/`original_login()`) is
trustworthy for authorization.

## Provider-specific session and server information

Providers also expose facts about the session, server and catalog that have no portable equivalent.
These live on the provider surface and are gated per name; on any other provider they throw
`NotSupportedException`. The session/server-identity subset (`server_name`, `host_id`, `host_name`,
`app_name`, `suser_name`, `original_login` and the shared `Sql` network/connection facts) is documented
in [Session and server identity](#session-and-server-identity) above.

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

Seven T-SQL connection-, session- or statement-scope names are not modelled: they report the state of
the connection or statement rather than compute a per-row value. They remain reachable through raw SQL
or a `[SqlFunction]` wrapper, and each has a review trigger if a concrete per-row use appears. The
session/server-identity subset (`SERVER_NAME`/`@@SERVERNAME`, `HOST_ID`, `HOST_NAME`, `APP_NAME`,
`SUSER_NAME`, `ORIGINAL_LOGIN`) is shipped — see
[Session and server identity](#session-and-server-identity) above.

| Function | Scope |
|---|---|
| `CURRENT_REQUEST_ID` | connection / request |
| `CURRENT_TRANSACTION_ID`, `XACT_STATE` | transaction |
| `IDENT_CURRENT` | session / table |
| `MIN_ACTIVE_ROWVERSION` | database / transaction |
| `ROWCOUNT_BIG` | statement |
| `SCOPE_IDENTITY` | session / scope |
