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
`type_id`/`type_name`, `current_timezone`/`current_timezone_id`, `getansinull`,
`publishingservername` and the live connection/transaction/statement facts (`current_request_id`,
`current_transaction_id`, `xact_state`, `ident_current`, `min_active_rowversion`, `rowcount_big`,
`scope_identity`) report the current database, schema, type, time zone, replication server and live
server state; they are gated per name by
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

### Refused statement and error-scope names (SQL Server)

Ten SQL Server system names are **refused before SQL** instead of being modelled. Each member exists on
[`SqlFunctions.SqlServer`](../guide/provider-specific-sql/sqlserver.md) so the refusal is reachable and
testable, returns a callable placeholder (the placeholder type is **not** a materialization contract),
and throws `NotSupportedException` with the stable message
`SqlFunctions.SqlServer.<member> (<TOKEN>) is refused: <reason>`. The refusal is raised at the
SQL-builder command boundary before any argument is evaluated, so a member used as one projection of a
larger query fails the same way; no statement reaches the server and no command is created.

| C# | SQL token | CLR placeholder | Reason |
|---|---|---|---|
| `session_context(key)` | `SESSION_CONTEXT` | `object?` | Returns `sql_variant`, for which nextorm has no faithful CLR result contract; no lossy cast substitution is performed. A **scope/CLR limitation**, not a version gap. |
| `session_id()` | `SESSION_ID` | `string?` | Is **not a SQL Server engine function**: the documented function belongs to Azure Synapse (SQL Server reports Msg 195). SQL Server exposes session identity through `@@SPID` or `sys.dm_exec_sessions.session_id`; no substitution is performed. |
| `rowcount()` | `@@ROWCOUNT` | `int` | Describes the previous statement and is **statement-scoped**; a per-row query projection cannot expose that value faithfully. |
| `error()` | `@@ERROR` | `int` | The same previous-statement scope as `@@ROWCOUNT`. |
| `error_number()` | `ERROR_NUMBER` | `int?` | Requires a SQL Server `CATCH`-block scope; outside `CATCH` it returns NULL, and nextorm does not establish that scope. |
| `error_severity()` | `ERROR_SEVERITY` | `int?` | The same `CATCH`-only scope. |
| `error_state()` | `ERROR_STATE` | `int?` | The same `CATCH`-only scope. |
| `error_line()` | `ERROR_LINE` | `int?` | The same `CATCH`-only scope. |
| `error_message()` | `ERROR_MESSAGE` | `string?` | The same `CATCH`-only scope. |
| `error_procedure()` | `ERROR_PROCEDURE` | `string?` | The same `CATCH`-only scope. |

The distinction matters: `SESSION_CONTEXT`, `@@ROWCOUNT`/`@@ERROR` and the six `ERROR_*` names are
**scope/CLR limitations** of a per-row projection (they describe a statement or a `CATCH` block, and
`sql_variant` has no faithful CLR carrier), while `SESSION_ID` is an **observed engine rejection** —
the name a different product documents is simply absent from the SQL Server engine. Neither kind is
deferred: all ten are deterministic, tested refusals. The supported live facts of the same surface
(`rowcount_big`, `xact_state`, `scope_identity`, `db_name`, `current_user`, …) are unaffected and keep
rendering.

## Cryptographic random bytes

`SqlFunctions.Sql.crypt_gen_random(int? length)` returns `length` cryptographically strong random bytes
as `byte[]?`. The portable domain is 1–1024 bytes: a **constant** length outside it throws
`ArgumentOutOfRangeException` before any SQL is emitted, while a runtime value outside it keeps the
provider's own error (no uniform message is promised). A NULL length yields SQL NULL.

| Provider | SQL |
|---|---|
| SQL Server | `CASE WHEN length IS NULL THEN CAST(NULL AS varbinary(8000)) ELSE CRYPT_GEN_RANDOM(length) END` |
| PostgreSQL | `CASE WHEN length IS NULL THEN CAST(NULL AS bytea) ELSE gen_random_bytes(length) END` |
| MySQL / MariaDB | `CASE WHEN length IS NULL THEN CAST(NULL AS BINARY) ELSE RANDOM_BYTES(length) END` |

The PostgreSQL path calls `gen_random_bytes` from the **`pgcrypto`** extension: install it on the server
(`CREATE EXTENSION pgcrypto`); nextorm neither installs extensions nor detects their absence. ClickHouse,
SQLite and the in-memory provider reject the member with
`The crypt_gen_random function is not supported by this provider.` The generated bytes are never
predictable or reproducible from the query, and no test promises their content or entropy.

MariaDB information, cryptographic, system and Galera functions, including verification qualifications,
are listed in the [MariaDB catalogue](08-reference.md#mariadb-catalogue).

## Live state (SQL Server)

**Live mutable state is not an exclusion criterion.** The T-SQL names that report the current
connection-, transaction-, session- or statement-scope state are exposed as-is, with the native SQL
Server semantics; nextorm makes **no guarantee** of per-row stability, connection continuity between
queries or identity retrieval. Each value is the native snapshot of the moment the statement runs, and
the client never invents a guard around it. Use `ReturningIdentity<TKey>()` when a last-insert key is
required.

| C# | CLR | Caveat |
|---|---|---|
| `current_request_id()` | `short?` | NULL outside a request |
| `current_transaction_id()` | `long?` | NULL outside a transaction |
| `xact_state()` | `short` | native 0 (none) / 1 (committable) / -1 (uncommittable); no invented NULL |
| `ident_current(table)` | `decimal?` | last identity generated by **any** session for the table; racy, not a last-insert API |
| `min_active_rowversion()` | `byte[]` | native `binary(8)`, not a numeric counter |
| `rowcount_big()` | `long` | previous command on the same connection; a separate nextorm query is its own command |
| `scope_identity()` | `decimal?` | only in the same batch as the `INSERT`; a separate query sees NULL |

Stable configuration and identity constants of the same scope stay on the same surface
(`current_timezone`/`current_timezone_id` and the session/server-identity subset) — see
[Session and server identity](#session-and-server-identity) above.
