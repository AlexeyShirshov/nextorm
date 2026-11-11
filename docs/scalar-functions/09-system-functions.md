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

The `pg_*` system-information surface is PostgreSQL-only and gated **per name**: every scalar member is
advertised by [`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String)),
each set-returning reader by `SupportsTableFunction`, and any other provider rejects the call with
`NotSupportedException`. It covers the 79 `pg_*` names of the PostgreSQL 17 system-information expansion:
56 delivered scalars, 2 tested refusals, the already-covered `pg_backend_pid`, and 20 set-returning
readers.

**Semantics measured on PostgreSQL 17.11.** Results of the OID-alias and transaction types that Npgsql has
no CLR mapping for — `oid`, `regclass`, `regtype`, `xid8` and `pg_lsn` — are bridged to `text`
server-side (`cast(... as text)`, or `::text` inside a row projection), and a `text` OID argument is cast
back to `oid`/`regclass`/`regtype`/`regrole`, because a text parameter does not resolve those overloads.
`pg_current_logfile()` returns NULL when no log destination is file-based (`logging_collector` off, or
logging to `syslog`), not an empty string; the named form takes `stderr`, `csvlog` or `jsonlog`, and any
other value raises on the server. `pg_xact_commit_timestamp` raises SQLSTATE `55000` (`could not
get commit timestamp data`) when `track_commit_timestamp` is off — it does not return NULL. The
object-definition helpers return NULL for an argument that names no object instead of raising
(`pg_get_indexdef`, `pg_get_viewdef`, `pg_get_ruledef`, `pg_get_triggerdef`, `pg_get_partkeydef`,
`pg_get_partition_constraintdef`, `pg_get_statisticsobjdef`). The family is read-only: destructive or
state-changing server controls (`pg_terminate_backend`, `pg_cancel_backend`, `pg_switch_wal`,
`pg_promote`, `pg_create_restore_point`, `pg_reload_conf`, `pg_rotate_logfile`) are not exposed, and the
two blocking helpers only report process ids — they never act on a session.

#### Session and server identity

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.current_role()` | `current_role` → `name` (bare keyword) |
| `SqlFunctions.Postgres.current_catalog()` | `current_catalog` → `name` (bare keyword) |
| `SqlFunctions.Postgres.current_schemas(includeImplicit)` | `current_schemas(include_implicit)` → `name[]` |
| `SqlFunctions.Postgres.pg_postmaster_start_time()` | `pg_postmaster_start_time()` |
| `SqlFunctions.Postgres.pg_current_logfile()` | `pg_current_logfile()` |
| `SqlFunctions.Postgres.pg_current_logfile(format)` | `pg_current_logfile(format)` |
| `SqlFunctions.Postgres.pg_conf_load_time()` | `pg_conf_load_time()` |
| `SqlFunctions.Postgres.pg_jit_available()` | `pg_jit_available()` |
| `SqlFunctions.Postgres.pg_my_temp_schema()` | `cast(pg_my_temp_schema() as text)` → `oid` as text |
| `SqlFunctions.Postgres.pg_is_other_temp_schema(schemaOid)` | `pg_is_other_temp_schema(schemaOid)` → `oid` argument |
| `SqlFunctions.Postgres.pg_notification_queue_usage()` | `pg_notification_queue_usage()` |
| `SqlFunctions.Postgres.pg_blocking_pids(pid)` | `pg_blocking_pids(pid)` → `integer[]` |
| `SqlFunctions.Postgres.pg_safe_snapshot_blocking_pids(pid)` | `pg_safe_snapshot_blocking_pids(pid)` → `integer[]` |
| `SqlFunctions.Postgres.pg_trigger_depth()` | `pg_trigger_depth()` |

`pg_backend_pid()` stays on the shared `SqlFunctions.Sql` surface and is documented in
[Session and server identity](#session-and-server-identity) above; `pg_typeof` is documented with the
type helpers in [Date and time](03-date-and-time.md).

#### Catalog visibility and role privilege

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.pg_collation_is_visible(oid)` | `pg_collation_is_visible(oid)` |
| `SqlFunctions.Postgres.pg_conversion_is_visible(oid)` | `pg_conversion_is_visible(oid)` |
| `SqlFunctions.Postgres.pg_function_is_visible(oid)` | `pg_function_is_visible(oid)` |
| `SqlFunctions.Postgres.pg_opclass_is_visible(oid)` | `pg_opclass_is_visible(oid)` |
| `SqlFunctions.Postgres.pg_operator_is_visible(oid)` | `pg_operator_is_visible(oid)` |
| `SqlFunctions.Postgres.pg_opfamily_is_visible(oid)` | `pg_opfamily_is_visible(oid)` |
| `SqlFunctions.Postgres.pg_statistics_obj_is_visible(oid)` | `pg_statistics_obj_is_visible(oid)` |
| `SqlFunctions.Postgres.pg_table_is_visible(oid)` | `pg_table_is_visible(oid)` |
| `SqlFunctions.Postgres.pg_ts_config_is_visible(oid)` | `pg_ts_config_is_visible(oid)` |
| `SqlFunctions.Postgres.pg_ts_dict_is_visible(oid)` | `pg_ts_dict_is_visible(oid)` |
| `SqlFunctions.Postgres.pg_ts_parser_is_visible(oid)` | `pg_ts_parser_is_visible(oid)` |
| `SqlFunctions.Postgres.pg_ts_template_is_visible(oid)` | `pg_ts_template_is_visible(oid)` |
| `SqlFunctions.Postgres.pg_type_is_visible(oid)` | `pg_type_is_visible(oid)` |
| `SqlFunctions.Postgres.pg_has_role(role, privilege)` | `pg_has_role(role, privilege)` → `regrole` argument |
| `SqlFunctions.Postgres.pg_has_role(role, membership, privilege)` | `pg_has_role(role, membership, privilege)` → two `regrole` arguments |

The visibility helpers return NULL when the OID names no object; `pg_has_role` accepts a role name or a
numeric OID in text form in either role position.

#### Encoding and catalog description

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.pg_char_to_encoding(name)` | `pg_char_to_encoding(name)` → `-1` for an unknown name |
| `SqlFunctions.Postgres.pg_encoding_to_char(code)` | `pg_encoding_to_char(code)` → the empty string for an unknown code |
| `SqlFunctions.Postgres.pg_basetype(type)` | `cast(pg_basetype(type) as text)` → `regtype` argument and result |
| `SqlFunctions.Postgres.pg_describe_object(classOid, objectOid, objectSubId)` | `pg_describe_object(classid, objid, objsubid)` |
| `SqlFunctions.Postgres.pg_tablespace_location(oid)` | `pg_tablespace_location(oid)` → the empty string for the default tablespace |
| `SqlFunctions.Postgres.pg_settings_get_flags(setting)` | `pg_settings_get_flags(setting)` → `text[]` |
| `SqlFunctions.Postgres.pg_index_has_property(index, property)` | `pg_index_has_property(index, property)` |
| `SqlFunctions.Postgres.pg_indexam_has_property(accessMethodOid, property)` | `pg_indexam_has_property(amoid, property)` |
| `SqlFunctions.Postgres.pg_index_column_has_property(index, columnNumber, property)` | `pg_index_column_has_property(index, column, property)` |
| `SqlFunctions.Postgres.pg_input_is_valid(value, typeName)` | `pg_input_is_valid(value, type)` |
| `SqlFunctions.Postgres.pg_get_serial_sequence(table, column)` | `pg_get_serial_sequence(table, column)` |

`pg_settings_get_flags` returns an empty array for a setting with no flags and NULL for an unknown
setting; an unrecognised index property is NULL; `pg_describe_object` is NULL when the OIDs name no object;
`pg_input_is_valid` never raises for invalid input but an unknown type name is still an error;
`pg_get_serial_sequence` is NULL for a column without a sequence.

#### Size helpers

No size helper is exposed: `pg_column_size`, `pg_relation_size`, `pg_total_relation_size`,
`pg_indexes_size`, `pg_database_size`, `pg_size_pretty` and `pg_size_bytes` have no member on
`SqlFunctions.Postgres`. The only block and segment sizes on the surface are the read-only
`pg_control_init()` columns in [Control files](#control-files) below.

#### Object definitions

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.pg_get_function_arguments(functionOid)` | `pg_get_function_arguments(oid)` |
| `SqlFunctions.Postgres.pg_get_function_identity_arguments(functionOid)` | `pg_get_function_identity_arguments(oid)` |
| `SqlFunctions.Postgres.pg_get_function_result(functionOid)` | `pg_get_function_result(oid)` |
| `SqlFunctions.Postgres.pg_get_functiondef(functionOid)` | `pg_get_functiondef(oid)` |
| `SqlFunctions.Postgres.pg_get_indexdef(indexOid)` | `pg_get_indexdef(oid)` |
| `SqlFunctions.Postgres.pg_get_indexdef(indexOid, columnNumber, pretty)` | `pg_get_indexdef(oid, integer, boolean)` |
| `SqlFunctions.Postgres.pg_get_partition_constraintdef(relationOid)` | `pg_get_partition_constraintdef(oid)` |
| `SqlFunctions.Postgres.pg_get_partkeydef(relationOid)` | `pg_get_partkeydef(oid)` |
| `SqlFunctions.Postgres.pg_get_ruledef(ruleOid)` | `pg_get_ruledef(oid)` |
| `SqlFunctions.Postgres.pg_get_ruledef(ruleOid, pretty)` | `pg_get_ruledef(oid, boolean)` |
| `SqlFunctions.Postgres.pg_get_statisticsobjdef(statisticsOid)` | `pg_get_statisticsobjdef(oid)` |
| `SqlFunctions.Postgres.pg_get_triggerdef(triggerOid)` | `pg_get_triggerdef(oid)` |
| `SqlFunctions.Postgres.pg_get_triggerdef(triggerOid, pretty)` | `pg_get_triggerdef(oid, boolean)` |
| `SqlFunctions.Postgres.pg_get_userbyid(roleOid)` | `pg_get_userbyid(oid)` → `unknown (OID=N)` for a missing role |
| `SqlFunctions.Postgres.pg_get_viewdef(view)` | `pg_get_viewdef(to_regclass(view))` |
| `SqlFunctions.Postgres.pg_get_viewdef(view, pretty)` | `pg_get_viewdef(to_regclass(view), boolean)` |
| `SqlFunctions.Postgres.pg_get_viewdef(viewOid, pretty)` | `pg_get_viewdef(to_regclass(viewOid), integer)` |

The view argument is a view name or a numeric OID in text form. `pg_get_functiondef` is an error for an
aggregate or a window function, and `pg_get_indexdef(oid, integer, boolean)` is an error for an
out-of-range column; both keep the native server error.

#### Snapshots and transactions

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.pg_current_snapshot()` | `cast(pg_current_snapshot() as text)` → `pg_snapshot` as text |
| `SqlFunctions.Postgres.pg_current_xact_id()` | `cast(pg_current_xact_id() as text)` → `xid8` as text |
| `SqlFunctions.Postgres.pg_current_xact_id_if_assigned()` | `cast(pg_current_xact_id_if_assigned() as text)` |
| `SqlFunctions.Postgres.pg_snapshot_xmax(snapshot)` | `cast(pg_snapshot_xmax(snapshot) as text)` |
| `SqlFunctions.Postgres.pg_snapshot_xmin(snapshot)` | `cast(pg_snapshot_xmin(snapshot) as text)` |
| `SqlFunctions.Postgres.pg_visible_in_snapshot(xid, snapshot)` | `pg_visible_in_snapshot(xid, snapshot)` → `xid8` and `pg_snapshot` arguments |
| `SqlFunctions.Postgres.pg_xact_commit_timestamp(xid)` | `pg_xact_commit_timestamp(xid)` → `xid` argument |
| `SqlFunctions.Postgres.pg_xact_status(xid)` | `pg_xact_status(xid)` → `xid8` argument |

The textual snapshot is the value `pg_current_snapshot()` returns, and the same text feeds
`pg_snapshot_xmax`, `pg_snapshot_xmin` and `pg_visible_in_snapshot`. `pg_current_xact_id()` assigns an id
to a write transaction that has none yet; `pg_current_xact_id_if_assigned()` does not and returns NULL
instead.

#### Refusals

| Family | Refused member | Tested reason |
|---|---|---|
| system information | `pg_get_expr(pg_node_tree, oid)` / `pg_get_expr(pg_node_tree, oid, boolean)` | `0A000: cannot accept a value of type pg_node_tree`; a value of the internal `pg_node_tree` type cannot be produced from CLR, so no form of the function can be prepared. |
| system information | `pg_get_constraintdef(oid)` / `pg_get_constraintdef(oid, boolean)` | `0A000: cannot accept a value of type pg_node_tree`; the sibling `pg_get_expr` cannot be called at all, and the pair is refused rather than half-exposed. Read the constraint text from `pg_constraint.conbin` through a raw command instead. |

#### Control files

The four control-file readers are `TvfEntityBuilder<...>` FROM sources that yield a single row each:

| C# | SQL | Row interface | Columns (C# ← SQL: CLR) |
|---|---|---|---|
| `SqlFunctions.Postgres.pg_control_system()` | `pg_control_system()` | `IPgControlSystemRow` | `PgControlVersion int? ← pg_control_version`; `CatalogVersionNo int? ← catalog_version_no`; `SystemIdentifier long? ← system_identifier`; `PgControlLastModified DateTime? ← pg_control_last_modified` |
| `SqlFunctions.Postgres.pg_control_init()` | `pg_control_init()` | `IPgControlInitRow` | `MaxDataAlignment int? ← max_data_alignment`; `DatabaseBlockSize int? ← database_block_size`; `BlocksPerSegment int? ← blocks_per_segment`; `WalBlockSize int? ← wal_block_size`; `BytesPerWalSegment int? ← bytes_per_wal_segment`; `MaxIdentifierLength int? ← max_identifier_length`; `MaxIndexColumns int? ← max_index_columns`; `MaxToastChunkSize int? ← max_toast_chunk_size`; `LargeObjectChunkSize int? ← large_object_chunk_size`; `Float8PassByValue bool? ← float8_pass_by_value`; `DataPageChecksumVersion int? ← data_page_checksum_version` |
| `SqlFunctions.Postgres.pg_control_recovery()` | `pg_control_recovery()` | `IPgControlRecoveryRow` | `MinRecoveryEndLsn string? ← min_recovery_end_lsn`; `MinRecoveryEndTimeline int? ← min_recovery_end_timeline`; `BackupStartLsn string? ← backup_start_lsn`; `BackupEndLsn string? ← backup_end_lsn`; `EndOfBackupRecordRequired bool? ← end_of_backup_record_required` |
| `SqlFunctions.Postgres.pg_control_checkpoint()` | `pg_control_checkpoint()` | `IPgControlCheckpointRow` | `CheckpointLsn string? ← checkpoint_lsn`; `RedoLsn string? ← redo_lsn`; `RedoWalFile string? ← redo_wal_file`; `TimelineId int? ← timeline_id`; `PrevTimelineId int? ← prev_timeline_id`; `FullPageWrites bool? ← full_page_writes`; `NextXid string? ← next_xid`; `NextOid string? ← next_oid`; `NextMultixactId uint? ← next_multixact_id`; `NextMultiOffset uint? ← next_multi_offset`; `OldestXid uint? ← oldest_xid`; `OldestXidDbId string? ← oldest_xid_dbid`; `OldestActiveXid uint? ← oldest_active_xid`; `OldestMultiXid uint? ← oldest_multi_xid`; `OldestMultiDbId string? ← oldest_multi_dbid`; `OldestCommitTsXid uint? ← oldest_commit_ts_xid`; `NewestCommitTsXid uint? ← newest_commit_ts_xid`; `CheckpointTime DateTime? ← checkpoint_time` |

`pg_control_recovery()` and `pg_control_checkpoint()` read the `pg_lsn` columns as text; the OID and
`xid` columns of the checkpoint row follow the same bridge (`next_oid`/`oldest_xid_dbid`/
`oldest_multi_dbid` as text, the `xid` values as `uint`).

#### WAL and transaction readers

| C# | SQL | Row interface | Columns (C# ← SQL: CLR) |
|---|---|---|---|
| `SqlFunctions.Postgres.pg_available_wal_summaries()` | `pg_available_wal_summaries()` | `IPgAvailableWalSummaryRow` | `TimelineId long? ← tli`; `StartLsn string? ← start_lsn`; `EndLsn string? ← end_lsn` |
| `SqlFunctions.Postgres.pg_get_wal_summarizer_state()` | `pg_get_wal_summarizer_state()` | `IPgWalSummarizerStateRow` | `SummarizedTli long? ← summarized_tli`; `SummarizedLsn string? ← summarized_lsn`; `PendingLsn string? ← pending_lsn`; `SummarizerPid int? ← summarizer_pid` |
| `SqlFunctions.Postgres.pg_wal_summary_contents(timelineId, startLsn, endLsn)` | `pg_wal_summary_contents(tli, start_lsn, end_lsn)` | `IPgWalSummaryContentRow` | `RelFileNode string? ← relfilenode`; `RelTablespace string? ← reltablespace`; `RelDatabase string? ← reldatabase`; `RelForkNumber short? ← relforknumber`; `RelBlockNumber long? ← relblocknumber`; `IsLimitBlock bool? ← is_limit_block` |
| `SqlFunctions.Postgres.pg_last_committed_xact()` | `pg_last_committed_xact()` | `IPgCommittedXactRow` | `Xid uint? ← xid`; `Timestamp DateTime? ← timestamp`; `RoIdent string? ← roident` |
| `SqlFunctions.Postgres.pg_get_multixact_members(multixactId)` | `pg_get_multixact_members(xid)` | `IPgMultixactMemberRow` | `Xid uint? ← xid`; `Mode string? ← mode` |
| `SqlFunctions.Postgres.pg_xact_commit_timestamp_origin(xid)` | `pg_xact_commit_timestamp_origin(xid)` | `IPgXactCommitOriginRow` | `Timestamp DateTime? ← timestamp`; `RoIdent string? ← roident` |
| `SqlFunctions.Postgres.pg_snapshot_xip(snapshot)` | `pg_snapshot_xip(snapshot)` | `ISnapshotXipRow` | `Xid string? ← pg_snapshot_xip` |

`pg_available_wal_summaries()` is empty while `summarize_wal` is off; `pg_last_committed_xact()` yields
zero or one row and yields no row until a commit has been recorded under `track_commit_timestamp`;
`pg_xact_commit_timestamp_origin` yields NULL columns for a transaction without a commit timestamp;
`pg_get_multixact_members` yields no row for a multixact id the server has not created;
`pg_snapshot_xip` reads each `xid8` as text.

#### Catalog, keyword and object readers

| C# | SQL | Row interface | Columns (C# ← SQL: CLR) |
|---|---|---|---|
| `SqlFunctions.Postgres.pg_get_keywords()` | `pg_get_keywords()` | `IPgKeywordRow` | `Word string? ← word`; `CatCode string? ← catcode`; `BareLabel bool? ← barelabel`; `CatDesc string? ← catdesc`; `BareDesc string? ← baredesc` |
| `SqlFunctions.Postgres.pg_listening_channels()` | `pg_listening_channels()` | `IListeningChannelRow` | `Channel string? ← pg_listening_channels` |
| `SqlFunctions.Postgres.pg_tablespace_databases(tablespaceOid)` | `pg_tablespace_databases(oid)` | `ITablespaceDatabaseRow` | `DatabaseOid string? ← pg_tablespace_databases` |
| `SqlFunctions.Postgres.pg_get_catalog_foreign_keys()` | `pg_get_catalog_foreign_keys()` | `IPgCatalogForeignKeyRow` | `FkTable string? ← fktable`; `FkColumns string[]? ← fkcols`; `PkTable string? ← pktable`; `PkColumns string[]? ← pkcols`; `IsArray bool? ← is_array`; `IsOptional bool? ← is_opt` |
| `SqlFunctions.Postgres.pg_options_to_table(options)` | `pg_options_to_table(options)` | `IPgOptionRow` | `OptionName string? ← option_name`; `OptionValue string? ← option_value` |
| `SqlFunctions.Postgres.pg_get_object_address(type, objectNames, objectArgs)` | `pg_get_object_address(type, object_names, object_args)` | `IPgObjectAddressRow` | `ClassId string? ← classid`; `ObjId string? ← objid`; `ObjSubId int? ← objsubid` |
| `SqlFunctions.Postgres.pg_identify_object(classId, objectId, objectSubId)` | `pg_identify_object(classid, objid, objsubid)` | `IPgIdentifyObjectRow` | `Type string? ← type`; `Schema string? ← schema`; `Name string? ← name`; `Identity string? ← identity` |
| `SqlFunctions.Postgres.pg_identify_object_as_address(classId, objectId, objectSubId)` | `pg_identify_object_as_address(classid, objid, objsubid)` | `IPgIdentifyObjectAddressRow` | `Type string? ← type`; `ObjectNames string[]? ← object_names`; `ObjectArgs string[]? ← object_args` |
| `SqlFunctions.Postgres.pg_input_error_info(value, typeName)` | `pg_input_error_info(value, type_name)` | `IPgInputErrorInfoRow` | `Message string? ← message`; `Detail string? ← detail`; `Hint string? ← hint`; `SqlErrorCode string? ← sql_error_code` |

`pg_listening_channels()` is empty until the same connection issues `LISTEN`; the `catcode` of
`pg_get_keywords` is the one-byte `"char"` code read as text; `pg_identify_object` yields one all-NULL row
for an object that does not exist; `pg_input_error_info` yields one all-NULL row for a value that parses.

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
