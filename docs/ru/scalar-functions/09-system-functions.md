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

Поверхность системной информации `pg_*` существует только для PostgreSQL и гейтится **по имени**:
каждый скалярный член объявляется через
[`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String)), каждый
множественный читатель — через `SupportsTableFunction`, а любой другой провайдер отклоняет вызов
`NotSupportedException`. Она покрывает 79 имён `pg_*` из расширения системной информации PostgreSQL 17:
56 поставленных скалярных функций, 2 проверенных отказа, уже покрытый `pg_backend_pid` и 20
множественных читателей.

**Семантика, измеренная на PostgreSQL 17.11.** Результаты типов-алиасов OID и транзакционных типов,
для которых у Npgsql нет CLR-отображения — `oid`, `regclass`, `regtype`, `xid8` и `pg_lsn` — переводятся
в `text` на стороне сервера (`cast(... as text)` или `::text` внутри проекции строки), а текстовый
OID-аргумент приводится обратно к `oid`/`regclass`/`regtype`/`regrole`, потому что текстовый параметр не
разрешает такие перегрузки. `pg_current_logfile()` возвращает NULL, когда ни одно место назначения
журнала не является файловым (`logging_collector` выключен или журнал пишется в `syslog`), а не пустую
строку; именованная форма принимает `stderr`, `csvlog` или `jsonlog`, а любое другое значение выдаёт
ошибку на сервере. `pg_xact_commit_timestamp` выдаёт SQLSTATE `55000` (`could not get commit timestamp data`),
когда `track_commit_timestamp` выключен — он не возвращает NULL. Помощники определения объектов
возвращают NULL для аргумента, не называющего объект, вместо ошибки (`pg_get_indexdef`,
`pg_get_viewdef`, `pg_get_ruledef`, `pg_get_triggerdef`, `pg_get_partkeydef`,
`pg_get_partition_constraintdef`, `pg_get_statisticsobjdef`). Семейство только читающее: разрушительные
или меняющие состояние элементы управления сервером (`pg_terminate_backend`, `pg_cancel_backend`,
`pg_switch_wal`, `pg_promote`, `pg_create_restore_point`, `pg_reload_conf`, `pg_rotate_logfile`) не
выставлены, а два помощника блокировок только сообщают id процессов — они никогда не действуют на
сессию.

#### Идентичность сессии и сервера

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.current_schemas(includeImplicit)` | `current_schemas(include_implicit)` → `name[]` |
| `SqlFunctions.Postgres.pg_postmaster_start_time()` | `pg_postmaster_start_time()` |
| `SqlFunctions.Postgres.pg_current_logfile()` | `pg_current_logfile()` |
| `SqlFunctions.Postgres.pg_current_logfile(format)` | `pg_current_logfile(format)` |
| `SqlFunctions.Postgres.pg_conf_load_time()` | `pg_conf_load_time()` |
| `SqlFunctions.Postgres.pg_jit_available()` | `pg_jit_available()` |
| `SqlFunctions.Postgres.pg_my_temp_schema()` | `cast(pg_my_temp_schema() as text)` → `oid` как текст |
| `SqlFunctions.Postgres.pg_is_other_temp_schema(schemaOid)` | `pg_is_other_temp_schema(schemaOid)` → аргумент `oid` |
| `SqlFunctions.Postgres.pg_notification_queue_usage()` | `pg_notification_queue_usage()` |
| `SqlFunctions.Postgres.pg_blocking_pids(pid)` | `pg_blocking_pids(pid)` → `integer[]` |
| `SqlFunctions.Postgres.pg_safe_snapshot_blocking_pids(pid)` | `pg_safe_snapshot_blocking_pids(pid)` → `integer[]` |
| `SqlFunctions.Postgres.pg_trigger_depth()` | `pg_trigger_depth()` |

`pg_backend_pid()` остаётся на общей поверхности `SqlFunctions.Sql` и описан выше в разделе
[Идентичность сессии и сервера](#идентичность-сессии-и-сервера); `pg_typeof` описан вместе с помощниками
типов в разделе [Дата и время](03-date-and-time.md).

#### Видимость в каталоге и привилегия роли

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
| `SqlFunctions.Postgres.pg_has_role(role, privilege)` | `pg_has_role(role, privilege)` → аргумент `regrole` |
| `SqlFunctions.Postgres.pg_has_role(role, membership, privilege)` | `pg_has_role(role, membership, privilege)` → два аргумента `regrole` |

Помощники видимости возвращают NULL, когда OID не называет объект; `pg_has_role` принимает имя роли
или числовой OID в текстовой форме в любой позиции роли.

#### Кодировки и описание каталога

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.pg_char_to_encoding(name)` | `pg_char_to_encoding(name)` → `-1` для неизвестного имени |
| `SqlFunctions.Postgres.pg_encoding_to_char(code)` | `pg_encoding_to_char(code)` → пустая строка для неизвестного кода |
| `SqlFunctions.Postgres.pg_basetype(type)` | `cast(pg_basetype(type) as text)` → аргумент и результат `regtype` |
| `SqlFunctions.Postgres.pg_describe_object(classOid, objectOid, objectSubId)` | `pg_describe_object(classid, objid, objsubid)` |
| `SqlFunctions.Postgres.pg_tablespace_location(oid)` | `pg_tablespace_location(oid)` → пустая строка для tablespace по умолчанию |
| `SqlFunctions.Postgres.pg_settings_get_flags(setting)` | `pg_settings_get_flags(setting)` → `text[]` |
| `SqlFunctions.Postgres.pg_index_has_property(index, property)` | `pg_index_has_property(index, property)` |
| `SqlFunctions.Postgres.pg_indexam_has_property(accessMethodOid, property)` | `pg_indexam_has_property(amoid, property)` |
| `SqlFunctions.Postgres.pg_index_column_has_property(index, columnNumber, property)` | `pg_index_column_has_property(index, column, property)` |
| `SqlFunctions.Postgres.pg_input_is_valid(value, typeName)` | `pg_input_is_valid(value, type)` |
| `SqlFunctions.Postgres.pg_get_serial_sequence(table, column)` | `pg_get_serial_sequence(table, column)` |

`pg_settings_get_flags` возвращает пустой массив для настройки без флагов и NULL для неизвестной
настройки; нераспознанное свойство индекса — NULL; `pg_describe_object` равен NULL, когда OID не
называют объект; `pg_input_is_valid` никогда не выдаёт ошибку для некорректного ввода, но неизвестное
имя типа — по-прежнему ошибка; `pg_get_serial_sequence` равен NULL для столбца без последовательности.

#### Помощники размеров

Помощник размеров не выставлен: `pg_column_size`, `pg_relation_size`, `pg_total_relation_size`,
`pg_indexes_size`, `pg_database_size`, `pg_size_pretty` и `pg_size_bytes` не имеют члена на
`SqlFunctions.Postgres`. Единственные размеры блоков и сегментов на поверхности — столбцы только для
чтения `pg_control_init()` в разделе [Файлы управления](#файлы-управления) ниже.

#### Определения объектов

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
| `SqlFunctions.Postgres.pg_get_userbyid(roleOid)` | `pg_get_userbyid(oid)` → `unknown (OID=N)` для отсутствующей роли |
| `SqlFunctions.Postgres.pg_get_viewdef(view)` | `pg_get_viewdef(to_regclass(view))` |
| `SqlFunctions.Postgres.pg_get_viewdef(view, pretty)` | `pg_get_viewdef(to_regclass(view), boolean)` |
| `SqlFunctions.Postgres.pg_get_viewdef(viewOid, pretty)` | `pg_get_viewdef(to_regclass(viewOid), integer)` |

Аргумент view — имя представления или числовой OID в текстовой форме. `pg_get_functiondef` — ошибка
для агрегата или оконной функции, а `pg_get_indexdef(oid, integer, boolean)` — ошибка для столбца вне
диапазона; оба сохраняют нативную ошибку сервера.

#### Снимки и транзакции

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.pg_current_snapshot()` | `cast(pg_current_snapshot() as text)` → `pg_snapshot` как текст |
| `SqlFunctions.Postgres.pg_current_xact_id()` | `cast(pg_current_xact_id() as text)` → `xid8` как текст |
| `SqlFunctions.Postgres.pg_current_xact_id_if_assigned()` | `cast(pg_current_xact_id_if_assigned() as text)` |
| `SqlFunctions.Postgres.pg_snapshot_xmax(snapshot)` | `cast(pg_snapshot_xmax(snapshot) as text)` |
| `SqlFunctions.Postgres.pg_snapshot_xmin(snapshot)` | `cast(pg_snapshot_xmin(snapshot) as text)` |
| `SqlFunctions.Postgres.pg_visible_in_snapshot(xid, snapshot)` | `pg_visible_in_snapshot(xid, snapshot)` → аргументы `xid8` и `pg_snapshot` |
| `SqlFunctions.Postgres.pg_xact_commit_timestamp(xid)` | `pg_xact_commit_timestamp(xid)` → аргумент `xid` |
| `SqlFunctions.Postgres.pg_xact_status(xid)` | `pg_xact_status(xid)` → аргумент `xid8` |

Текстовый снимок — это значение, которое возвращает `pg_current_snapshot()`, и тот же текст принимают
`pg_snapshot_xmax`, `pg_snapshot_xmin` и `pg_visible_in_snapshot`. `pg_current_xact_id()` назначает id
пишущей транзакции, у которой его ещё нет; `pg_current_xact_id_if_assigned()` не назначает и возвращает
NULL.

#### Отказы

| Семейство | Отклонённый член | Проверенная причина |
|---|---|---|
| system information | `pg_get_expr(pg_node_tree, oid)` / `pg_get_expr(pg_node_tree, oid, boolean)` | `0A000: cannot accept a value of type pg_node_tree`; значение внутреннего типа `pg_node_tree` нельзя получить из CLR, поэтому ни одна форма функции не может быть подготовлена. |
| system information | `pg_get_constraintdef(oid)` / `pg_get_constraintdef(oid, boolean)` | `0A000: cannot accept a value of type pg_node_tree`; соседний `pg_get_expr` вообще не может быть вызван, и пара отклонена целиком, а не наполовину. Читайте текст ограничения из `pg_constraint.conbin` сырой командой. |

#### Файлы управления

Четыре читателя файлов управления — это FROM-источники `TvfEntityBuilder<...>`, дающие по одной строке
каждый:

| C# | SQL | Row interface | Columns (C# ← SQL: CLR) |
|---|---|---|---|
| `SqlFunctions.Postgres.pg_control_system()` | `pg_control_system()` | `IPgControlSystemRow` | `PgControlVersion int? ← pg_control_version`; `CatalogVersionNo int? ← catalog_version_no`; `SystemIdentifier long? ← system_identifier`; `PgControlLastModified DateTime? ← pg_control_last_modified` |
| `SqlFunctions.Postgres.pg_control_init()` | `pg_control_init()` | `IPgControlInitRow` | `MaxDataAlignment int? ← max_data_alignment`; `DatabaseBlockSize int? ← database_block_size`; `BlocksPerSegment int? ← blocks_per_segment`; `WalBlockSize int? ← wal_block_size`; `BytesPerWalSegment int? ← bytes_per_wal_segment`; `MaxIdentifierLength int? ← max_identifier_length`; `MaxIndexColumns int? ← max_index_columns`; `MaxToastChunkSize int? ← max_toast_chunk_size`; `LargeObjectChunkSize int? ← large_object_chunk_size`; `Float8PassByValue bool? ← float8_pass_by_value`; `DataPageChecksumVersion int? ← data_page_checksum_version` |
| `SqlFunctions.Postgres.pg_control_recovery()` | `pg_control_recovery()` | `IPgControlRecoveryRow` | `MinRecoveryEndLsn string? ← min_recovery_end_lsn`; `MinRecoveryEndTimeline int? ← min_recovery_end_timeline`; `BackupStartLsn string? ← backup_start_lsn`; `BackupEndLsn string? ← backup_end_lsn`; `EndOfBackupRecordRequired bool? ← end_of_backup_record_required` |
| `SqlFunctions.Postgres.pg_control_checkpoint()` | `pg_control_checkpoint()` | `IPgControlCheckpointRow` | `CheckpointLsn string? ← checkpoint_lsn`; `RedoLsn string? ← redo_lsn`; `RedoWalFile string? ← redo_wal_file`; `TimelineId int? ← timeline_id`; `PrevTimelineId int? ← prev_timeline_id`; `FullPageWrites bool? ← full_page_writes`; `NextXid string? ← next_xid`; `NextOid string? ← next_oid`; `NextMultixactId uint? ← next_multixact_id`; `NextMultiOffset uint? ← next_multi_offset`; `OldestXid uint? ← oldest_xid`; `OldestXidDbId string? ← oldest_xid_dbid`; `OldestActiveXid uint? ← oldest_active_xid`; `OldestMultiXid uint? ← oldest_multi_xid`; `OldestMultiDbId string? ← oldest_multi_dbid`; `OldestCommitTsXid uint? ← oldest_commit_ts_xid`; `NewestCommitTsXid uint? ← newest_commit_ts_xid`; `CheckpointTime DateTime? ← checkpoint_time` |

`pg_control_recovery()` и `pg_control_checkpoint()` читают столбцы `pg_lsn` как текст; столбцы OID и
`xid` строки checkpoint используют тот же мост (`next_oid`/`oldest_xid_dbid`/`oldest_multi_dbid` как
текст, значения `xid` как `uint`).

#### Читатели WAL и транзакций

| C# | SQL | Row interface | Columns (C# ← SQL: CLR) |
|---|---|---|---|
| `SqlFunctions.Postgres.pg_available_wal_summaries()` | `pg_available_wal_summaries()` | `IPgAvailableWalSummaryRow` | `TimelineId long? ← tli`; `StartLsn string? ← start_lsn`; `EndLsn string? ← end_lsn` |
| `SqlFunctions.Postgres.pg_get_wal_summarizer_state()` | `pg_get_wal_summarizer_state()` | `IPgWalSummarizerStateRow` | `SummarizedTli long? ← summarized_tli`; `SummarizedLsn string? ← summarized_lsn`; `PendingLsn string? ← pending_lsn`; `SummarizerPid int? ← summarizer_pid` |
| `SqlFunctions.Postgres.pg_wal_summary_contents(timelineId, startLsn, endLsn)` | `pg_wal_summary_contents(tli, start_lsn, end_lsn)` | `IPgWalSummaryContentRow` | `RelFileNode string? ← relfilenode`; `RelTablespace string? ← reltablespace`; `RelDatabase string? ← reldatabase`; `RelForkNumber short? ← relforknumber`; `RelBlockNumber long? ← relblocknumber`; `IsLimitBlock bool? ← is_limit_block` |
| `SqlFunctions.Postgres.pg_last_committed_xact()` | `pg_last_committed_xact()` | `IPgCommittedXactRow` | `Xid uint? ← xid`; `Timestamp DateTime? ← timestamp`; `RoIdent string? ← roident` |
| `SqlFunctions.Postgres.pg_get_multixact_members(multixactId)` | `pg_get_multixact_members(xid)` | `IPgMultixactMemberRow` | `Xid uint? ← xid`; `Mode string? ← mode` |
| `SqlFunctions.Postgres.pg_xact_commit_timestamp_origin(xid)` | `pg_xact_commit_timestamp_origin(xid)` | `IPgXactCommitOriginRow` | `Timestamp DateTime? ← timestamp`; `RoIdent string? ← roident` |
| `SqlFunctions.Postgres.pg_snapshot_xip(snapshot)` | `pg_snapshot_xip(snapshot)` | `ISnapshotXipRow` | `Xid string? ← pg_snapshot_xip` |

`pg_available_wal_summaries()` пуст, пока `summarize_wal` выключен; `pg_last_committed_xact()` даёт
ноль или одну строку и не даёт строк, пока не записан коммит при `track_commit_timestamp`;
`pg_xact_commit_timestamp_origin` даёт NULL-столбцы для транзакции без отметки времени коммита;
`pg_get_multixact_members` не даёт строк для multixact id, который сервер ещё не создал;
`pg_snapshot_xip` читает каждый `xid8` как текст.

#### Читатели каталога, ключевых слов и объектов

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

`pg_listening_channels()` пуст, пока то же соединение не выполнит `LISTEN`; `catcode` у
`pg_get_keywords` — однобайтовый код `"char"`, прочитанный как текст; `pg_identify_object` даёт одну
полностью NULL-строку для несуществующего объекта; `pg_input_error_info` даёт одну полностью NULL-строку
для значения, которое разбирается.

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
