# API reference

> A curated index of nextorm's public types, grouped by namespace, each linked to its generated API reference.

**Prerequisites:** [Quickstart](../getting-started/02-quickstart.md) · [Provider overview](../providers/overview.md)

## Overview

This is a **curated** index; the reference generated from the XML doc comments on the source types is
published in the **API reference** section of this site (see the top navigation). Each entry below gives the
type, a one-line description and, through the type name itself, a link to its generated API reference page.

Types are listed under their defining namespace. All of the query API is in [`NextORM.Core`](xref:NextORM.Core); each provider
package adds a context, a dialect and a [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) extension class in its own namespace.

## Namespace [`NextORM.Core`](xref:NextORM.Core)

### Context and roles

| Type | Description |
|---|---|
| [`IDataContext`](xref:NextORM.Core.IDataContext) | Composite facade over the context roles; the entry point consumers normally depend on. |
| [`IQueryExecutor`](xref:NextORM.Core.IQueryExecutor) | Executes a prepared command and materialises its result (terminals). |
| [`IQueryMaterializer`](xref:NextORM.Core.IQueryMaterializer) | Narrowest contract for planning + row reading, without terminals. |
| [`IQueryPlanner`](xref:NextORM.Core.IQueryPlanner) | Builds/resets execution plans and resolves [`From`](xref:NextORM.Core.DataContextExtensions) sources. |
| [`IRowReaderFactory`](xref:NextORM.Core.IRowReaderFactory) | Creates row readers/enumerators over a prepared command. |
| [`IQueryCache`](xref:NextORM.Core.IQueryCache) | Holds cached plans and the shared [`Any`](xref:NextORM.Core.EntityBuilder`1) plan. |
| [`IContextEnvironment`](xref:NextORM.Core.IContextEnvironment) | Ambient state: loggers, mapping mode, property bag. |
| [`IConnectionManager`](xref:NextORM.Core.IConnectionManager) | Owns the connection lifecycle (not part of [`IDataContext`](xref:NextORM.Core.IDataContext); the in-memory provider does not implement it). |
| [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) | Built-in in-memory [`IDataContext`](xref:NextORM.Core.IDataContext) over CLR collections. |

### Query builders

| Type | Description |
|---|---|
| [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) | Fluent, immutable query builder for a mapped entity type. |
| [`EntityBuilderExtensions`](xref:NextORM.Core.EntityBuilderExtensions) | Terminal operators of [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1) ([`Any`](xref:NextORM.Core.EntityBuilder`1)/[`ToList`](xref:NextORM.Core.EntityBuilder`1)/[`First`](xref:NextORM.Core.EntityBuilder`1)/[`Single`](xref:NextORM.Core.EntityBuilder`1)/[`Last`](xref:NextORM.Core.EntityBuilder`1)/[`Count`](xref:NextORM.Core.EntityBuilder`1)/aggregates/`To*`/[`Prepare`](xref:NextORM.Core.EntityBuilder`1)) as extension methods. |
| [`EntityBuilder`](xref:NextORM.Core.EntityBuilder) | Fluent builder for an alias/table source that has no entity type ([`TableAlias`](xref:NextORM.Core.TableAlias) mode). |
| [`JoinedEntityBuilder<T1,T2>`](xref:NextORM.Core.JoinedEntityBuilder`2) … [`JoinedEntityBuilder<T1..T8>`](xref:NextORM.Core.JoinedEntityBuilder`8) | Accumulated join builders; arity 2 through 8. |
| [`EntityMetadataBuilder<T>`](xref:NextORM.Core.EntityMetadataBuilder`1) | Fluent entity-metadata configuration used by `IDataContext.From<T>(...)`. |
| [`INamingConvention`](xref:NextORM.Core.INamingConvention) | Translates auto-derived table/column names (see `DataContextBuilder.UseNamingConvention`); built-in [`SnakeCaseNamingConvention`](xref:NextORM.Core.SnakeCaseNamingConvention) maps `SimpleEntity` to `simple_entity` and `FirstName` to `first_name`. |
| [`Projection<T1,T2>`](xref:NextORM.Core.Projection`2) … [`Projection<T1..T8>`](xref:NextORM.Core.Projection`8) | Result shape of a joined query; exposes `Item1`…`` |
| [`IProjection`](xref:NextORM.Core.IProjection) / [`IExtendableProjection`](xref:NextORM.Core.IExtendableProjection) | Markers for accumulated join projections (arity 8 is not extendable). |
| [`CteQuery`](xref:NextORM.Core.CteQuery) | Fluent scope collecting `WITH` declarations. |
| [`CteDefinition`](xref:NextORM.Core.CteDefinition) | One CTE: name, defining query, recursive flag and optional max recursion. |
| [`TableAlias`](xref:NextORM.Core.TableAlias) / [`TableColumn`](xref:NextORM.Core.TableColumn) | Alias-mode column accessors ([`GetInt32`](xref:NextORM.Core.TableAlias), [`GetString`](xref:NextORM.Core.TableAlias), …) and typed column wrapper ([`AsInt`](xref:NextORM.Core.TableColumn.AsInt), [`AsString`](xref:NextORM.Core.TableColumn.AsString), …). |
| [`Paging`](xref:NextORM.Core.Paging) | [`Limit`](xref:NextORM.Core.Paging.Limit) / [`Offset`](xref:NextORM.Core.Paging.Offset) / [`HasWithTies`](xref:NextORM.Core.Paging.HasWithTies) value used by every query builder. |

### Commands, plans and functions

| Type | Description |
|---|---|
| [`QueryCommand`](xref:NextORM.Core.QueryCommand) | Non-generic query command holding the plan/state shared by all results. |
| [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1) | Typed query command with terminals ([`ToList`](xref:NextORM.Core.EntityBuilder`1), [`First`](xref:NextORM.Core.EntityBuilder`1), [`Union`](xref:NextORM.Core.QueryCommand`1), [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct), [`Hint`](xref:NextORM.Core.QueryCommand`1), [`ForJson`](xref:NextORM.Core.QueryCommand`1), [`ForXml`](xref:NextORM.Core.QueryCommand`1), [`WithTableHint`](xref:NextORM.Core.EntityBuilder`1), [`Prepare`](xref:NextORM.Core.EntityBuilder`1), …). |
| [`QueryDefinition`](xref:NextORM.Core.QueryDefinition) | Immutable query shape (projection/entity source, condition, joins, paging, sorting, grouping, logger) taken by the command constructors and [`CreateCommand`](xref:NextORM.Core.DataContextExtensions). |
| [`PrepareFromSqlMode`](xref:NextORM.Core.PrepareFromSqlMode) | Flags for [`PrepareFromSql`](xref:NextORM.Core.EntityBuilder`1): `None` (buffered/scalar), [`Streaming`](xref:NextORM.Core.PrepareFromSqlMode.Streaming), `` |
| [`PreparedCommandOptions`](xref:NextORM.Core.PreparedCommandOptions) | Non-generic setup of [`DbPreparedQueryCommand<TResult>`](xref:NextORM.Core.DbPreparedQueryCommand`1) (single row, raw SQL, no params, parameter refresh). |
| [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1) | Prepared command; its default members execute it against a supplied [`IDataContext`](xref:NextORM.Core.IDataContext). |
| [`SqlFunctions`](xref:NextORM.Core.SqlFunctions) | Static entry point: [`Sql`](xref:NextORM.Core.SqlFunctions.Sql), [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres), [`SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer), [`ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) and [`Parameter`](xref:NextORM.Core.SqlFunctions). |
| [`CommonFunctions`](xref:NextORM.Core.CommonFunctions) | Cross-provider SQL function surface: `exists`, `like`, `@in`, `any`/`all` (subquery), the conditional `iif` ([`Iif`](xref:NextORM.Core.ISqlDialect.Iif)/[`IIifRenderer.Render`](xref:NextORM.Core.IIifRenderer.Render)), aggregates (including filtered aggregates, `string_agg` and the arbitrary-value `any_agg` on MySQL/ClickHouse), window functions (including `percent_rank`/`cume_dist` and `nth_value`, the latter gated by [`SupportsNthValue`](xref:NextORM.Core.ISqlDialect.SupportsNthValue), plus the window percentiles `percentile_cont`/`percentile_disc` on SQL Server/MariaDB), `nullif`/`greatest`/`least`/`date_trunc`/`date_add`/`date_diff`/`date_from_parts`/`end_of_month`/`extract`/`date_part`, session/information functions (`current_user`/`session_user`/`current_schema`/`current_database`/`version`), the UUID generators (`gen_random_uuid`/`uuidv7`) and full-text predicates (`contains`/`freetext`). |
| [`Postgres`](xref:NextORM.Core.SqlFunctions.Postgres) | PostgreSQL-only surface: arrays (`any`/`all`, `cardinality`, `array_*`, `array_shuffle`/`array_sample`, `string_to_array`), native JSON/JSONB, the extended scalar library (`asin`, `split_part`, `lpad`, `regexp_*`, `to_char`, `setseed`, `pg_typeof`, …), the crypto hashes `md5`/`digest` (pgcrypto)/`sha256`, the PostgreSQL-only aggregates (`bool_*`, `bit_*`, `regr_*`, `percentile_*`, `mode`, `array_agg`), the set-returning table functions (`generate_series`/`unnest`, `regexp_matches`/`regexp_split_to_table`, `jsonb_array_elements(_text)`, `jsonb_each(_text)`, `jsonb_object_keys`, `jsonb_path_query`, `ts_stat`), and the native text-search surface (`to_tsvector`/`to_tsquery`/`ts_rank`/`ts_rank_cd`/`ts_headline`/`@@`). |
| [`SqlServer`](xref:NextORM.Core.SqlFunctions.SqlServer) | Text-JSON surface (`json_value`/`json_query`/`json_modify`/`isjson`) for SQL Server and MySQL/MariaDB, the SQL Server-only conditional function `choose` ([`SupportsChoose`](xref:NextORM.Core.ISqlDialect.SupportsChoose)), the postfix XML data-type methods `xml_value`/`xml_query`/`xml_exist` and the `xml_nodes` rowset ([`XmlFunctions`](xref:NextORM.Core.ISqlDialect.XmlFunctions); row shape [`IXmlNodesRow`](xref:NextORM.Core.SqlFunctions.IXmlNodesRow)), plus the SQL Server `string_split`/`openjson` table functions and the full-text ranking table functions `containstable`/`freetexttable` (row shape [`IKeyRankRow<TKey>`](xref:NextORM.Core.SqlFunctions.IKeyRankRow`1)). |
| [`ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) | ClickHouse-only surface: the `arg_min`/`arg_max`, `uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12`, the parameterised `quantile`/`quantile_exact`/`quantile_timing`/`quantiles`/`median` aggregates, the most-frequent `top_k`/`top_k_weighted` aggregates, the row-picking `any_last` aggregate, the sequence/funnel `window_funnel`/`sequence_match`/`retention` aggregates, the frame-respecting `lag_in_frame`/`lead_in_frame` window functions, the multi-branch `multi_if` conditional (built with `when`/`otherwise`), the string-JSON `json_extract_string`/`json_extract_int`/`json_extract_float`/`json_extract_bool`/`json_extract_raw`/`json_has`/`json_length`/`json_type` family plus `visit_param_extract_string`/`_int`/`_float`/`_bool`/`_raw` the JSONPath `json_value`/`json_query`/`json_exists`, the array-returning `json_extract_keys`/`json_extract_array_raw` (`string[]`) and `json_extract_keys_and_values<T>` (`Tuple<string, T>[]`), the dictionary functions `dict_get`/`dict_get_or_default`/`dict_has`/`dict_get_hierarchy`/`dict_get_children`/`dict_is_in`, the `-If` combinator (`count_if`/`sum_if`/`avg_if`/`min_if`/`max_if`), the array-returning aggregates `group_array`/`group_uniq_array` (`groupArray`/`groupUniqArray`), the distributed `global_in` predicate, the `numbers`/`numbers_mt` and `zeros`/`zeros_mt` table functions, the server/cluster table functions `url`/`s3`/`file`/`remote`/`remote_secure`/`cluster`/`cluster_all_replicas` (generic `TRow` row interface), and the array functions over `Array(T)` columns/expressions (`array_join`, `length`, `has`, `index_of`, `has_any`, `has_all`, `starts_with`, `ends_with`, `has_substr`, `array_string_concat`, `split_by_char`, `array_sort`, `array_reverse`, `array_distinct`, `range`, `array_enumerate`, `array_cum_sum`, `array_slice`, `array_push_back`), and the higher-order (lambda) array functions (`array_map`, `array_filter`, `array_exists`, `array_all`, `array_count`, `array_first`/`array_first_index`, `array_last`/`array_last_index`). The tuple surface maps the CLR `Tuple.Create`/`System.Tuple<...>.ItemN` to `tuple(...)`/`tupleElement(t, n)` under [`SupportsTupleFunctions`](xref:NextORM.Core.ISqlDialect.SupportsTupleFunctions). |
| [`WindowFunction<T>`](xref:NextORM.Core.WindowFunction`1) | Unfinished window call; complete it with `Over(...)`. `Over(string)` references a named window declared on the query. |
| [`WindowOrder`](xref:NextORM.Core.WindowOrder) | An ordered window key plus [`OrderDirection`](xref:NextORM.Core.OrderDirection). |
| [`WindowDefinition`](xref:NextORM.Core.WindowDefinition), [`NamedWindowOrderKey`](xref:NextORM.Core.NamedWindowOrderKey) | A named window (`WINDOW w AS (...)`) declared with `EntityBuilder.Window` and its ordered keys. |
| [`WindowFrame`](xref:NextORM.Core.WindowFrame), [`WindowFrameBound`](xref:NextORM.Core.WindowFrameBound), [`WindowFrameType`](xref:NextORM.Core.WindowFrameType), [`WindowFrameBoundKind`](xref:NextORM.Core.WindowFrameBoundKind), [`WindowFrameExclusion`](xref:NextORM.Core.WindowFrameExclusion) | `ROWS`/`RANGE`/`GROUPS` frame specification, its boundaries and the `EXCLUDE` variants. |
| [`PivotAggregate`](xref:NextORM.Core.PivotAggregate), [`PivotValue`](xref:NextORM.Core.PivotValue), [`UnpivotColumn`](xref:NextORM.Core.UnpivotColumn), [`PivotExpression`](xref:NextORM.Core.PivotExpression) | The native SQL Server `PIVOT`/`UNPIVOT` source construct: `EntityBuilder.Pivot`/`Unpivot` reshape a plain table or a derived query into named columns ([`Pivot`](xref:NextORM.Core.ISqlDialect.Pivot)). |
| [`IIifRenderer`](xref:NextORM.Core.IIifRenderer), [`ISessionInfoFunctions`](xref:NextORM.Core.ISessionInfoFunctions), [`IUuidGenerators`](xref:NextORM.Core.IUuidGenerators), [`ILimitByRenderer`](xref:NextORM.Core.ILimitByRenderer) | Dialect capability objects exposed by [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) (`Iif`, `SessionInfoFunctions`, `UuidGenerators`, `LimitBy`). The object's presence is the capability, so support and rendering come from a single fact and can never diverge; the legacy `Supports*`/`Make*` members were removed in favour of the objects. |
| [`IXmlFunctions`](xref:NextORM.Core.IXmlFunctions), [`ISequenceAggregateRenderer`](xref:NextORM.Core.ISequenceAggregateRenderer), [`IUniqAggregateRenderer`](xref:NextORM.Core.IUniqAggregateRenderer), [`IQuantileAggregateRenderer`](xref:NextORM.Core.IQuantileAggregateRenderer), [`ITopKAggregateRenderer`](xref:NextORM.Core.ITopKAggregateRenderer), [`IMultiIfRenderer`](xref:NextORM.Core.IMultiIfRenderer), [`IDistinctOnRenderer`](xref:NextORM.Core.IDistinctOnRenderer), [`ITableSampleMethods`](xref:NextORM.Core.ITableSampleMethods), [`IPivotRenderer`](xref:NextORM.Core.IPivotRenderer), [`IArrayJoinRenderer`](xref:NextORM.Core.IArrayJoinRenderer), [`IDateConversionRenderer`](xref:NextORM.Core.IDateConversionRenderer), [`IStringSplitRenderer`](xref:NextORM.Core.IStringSplitRenderer), [`ILockRenderer`](xref:NextORM.Core.ILockRenderer) | The remaining dialect capability objects exposed by [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) (`XmlFunctions`, `SequenceAggregates`, `UniqAggregates`, `QuantileAggregates`, `TopKAggregates`, `MultiIf`, `DistinctOn`, `TableSample`, `Pivot`, `ArrayJoinClause`, `DateConversion`, `StringSplit`, `Lock`). Each nullable object is the single source of the capability and its renderer, so the XML/sequence/uniq/quantile/topK/multiIf/distinct-on/tablesample/PIVOT/unpivot/array-join/date-conversion/split/lock capabilities can never be "declared but unimplemented". |
| [`DataContextExtensions`](xref:NextORM.Core.DataContextExtensions) | Provider-independent helpers: [`From`](xref:NextORM.Core.DataContextExtensions), [`From`](xref:NextORM.Core.DataContextExtensions), [`FromTableFunction`](xref:NextORM.Core.DataContextExtensions), [`FromSql`](xref:NextORM.Core.DataContextExtensions), CTE entry points ([`With`](xref:NextORM.Core.DataContextExtensions) / [`WithRecursive`](xref:NextORM.Core.DataContextExtensions)) and prepared-command terminals. |

### Mapping attributes

| Type | Description |
|---|---|
| [`SqlTableAttribute`](xref:NextORM.Core.SqlTableAttribute) | Maps a class or interface to a table name (`[SqlTable("name")]`). |
| [`SqlFunctionAttribute`](xref:NextORM.Core.SqlFunctionAttribute) | Maps a CLR method (or its declaring type) to a scalar database function; optional `Name`/`Schema`. |
| [`SqlTableFunctionAttribute`](xref:NextORM.Core.SqlTableFunctionAttribute) | Maps a static method (or its declaring type) to a table-valued function used as a `FROM` source. |

### Supporting expression types

| Type | Description |
|---|---|
| [`OrderDirection`](xref:NextORM.Core.OrderDirection) | [`Asc`](xref:NextORM.Core.OrderDirection.Asc) / `` |
| [`JoinType`](xref:NextORM.Core.JoinType) | [`Inner`](xref:NextORM.Core.JoinType.Inner), [`Left`](xref:NextORM.Core.JoinType.Left), [`Right`](xref:NextORM.Core.JoinType.Right), [`Full`](xref:NextORM.Core.JoinType.Full), [`Cross`](xref:NextORM.Core.JoinType.Cross), [`FullCross`](xref:NextORM.Core.JoinType.FullCross), [`CrossApply`](xref:NextORM.Core.JoinType.CrossApply), [`OuterApply`](xref:NextORM.Core.JoinType.OuterApply). |
| [`JoinExpression`](xref:NextORM.Core.JoinExpression) | A single join: condition, type and joined source. |
| [`JoinStrictness`](xref:NextORM.Core.JoinStrictness) | ClickHouse join modifier: [`Default`](xref:NextORM.Core.JoinStrictness.Default), [`Any`](xref:NextORM.Core.JoinStrictness.Any), [`All`](xref:NextORM.Core.JoinStrictness.All), [`Asof`](xref:NextORM.Core.JoinStrictness.Asof). Apply with [`EntityBuilder.WithStrictness`](xref:NextORM.Core.EntityBuilder`1); the ClickHouse `GLOBAL` variant uses [`EntityBuilder.Global`](xref:NextORM.Core.EntityBuilder`1). |
| [`ArrayJoinKind`](xref:NextORM.Core.ArrayJoinKind) | ClickHouse <c>ARRAY JOIN</c> kind: [`Inner`](xref:NextORM.Core.ArrayJoinKind.Inner) via [`EntityBuilder.ArrayJoin`](xref:NextORM.Core.EntityBuilder`1) (drops empty arrays) or [`Left`](xref:NextORM.Core.ArrayJoinKind.Left) via [`EntityBuilder.LeftArrayJoin`](xref:NextORM.Core.EntityBuilder`1) (keeps them); expands one row per array element. |
| [`ArrayJoinProjection<TEntity, TElement>`](xref:NextORM.Core.ArrayJoinProjection`2) | Projection returned by [`EntityBuilder.ArrayJoinElement`](xref:NextORM.Core.EntityBuilder`1)/[`EntityBuilder.LeftArrayJoinElement`](xref:NextORM.Core.EntityBuilder`1): [`Item1`](xref:NextORM.Core.ArrayJoinProjection`2.Item1) is the original entity, [`Element`](xref:NextORM.Core.ArrayJoinProjection`2.Element) the expanded array element. Requires a dialect with the <c>ARRAY JOIN</c> clause. |
| [`FromExpression`](xref:NextORM.Core.FromExpression) / [`SelectExpression`](xref:NextORM.Core.SelectExpression) | FROM source and projected column metadata. |
| [`TableSampleMethod`](xref:NextORM.Core.TableSampleMethod) | Sampling algorithm for [`EntityBuilder.TableSample`](xref:NextORM.Core.EntityBuilder`1): [`System`](xref:NextORM.Core.TableSampleMethod.System) / [`Bernoulli`](xref:NextORM.Core.TableSampleMethod.Bernoulli) (PostgreSQL only for Bernoulli). |
| [`LockMode`](xref:NextORM.Core.LockMode) | Row-locking strength for [`EntityBuilder.ForUpdate`](xref:NextORM.Core.EntityBuilder`1)/[`EntityBuilder.ForShare`](xref:NextORM.Core.EntityBuilder`1): [`Update`](xref:NextORM.Core.LockMode.Update) / [`Share`](xref:NextORM.Core.LockMode.Share). |
| [`TemporalKind`](xref:NextORM.Core.TemporalKind) / [`TemporalClause`](xref:NextORM.Core.TemporalClause) | `FOR SYSTEM_TIME` clause for [`EntityBuilder.ForSystemTime`](xref:NextORM.Core.EntityBuilder`1): [`AsOf`](xref:NextORM.Core.TemporalKind.AsOf)/[`Between`](xref:NextORM.Core.TemporalKind.Between)/[`FromTo`](xref:NextORM.Core.TemporalKind.FromTo)/[`ContainedIn`](xref:NextORM.Core.TemporalKind.ContainedIn)/[`All`](xref:NextORM.Core.TemporalKind.All), built with the static factory methods. |
| [`UnionType`](xref:NextORM.Core.UnionType) | `None`, [`Distinct`](xref:NextORM.Core.EntityBuilder`1.Distinct), [`All`](xref:NextORM.Core.UnionType.All), [`Intersect`](xref:NextORM.Core.QueryCommand`1), [`IntersectAll`](xref:NextORM.Core.QueryCommand`1), [`Except`](xref:NextORM.Core.QueryCommand`1), [`ExceptAll`](xref:NextORM.Core.QueryCommand`1). |

### Dependency injection

| Type | Description |
|---|---|
| [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) | Provider options builder: [`UseLoggerFactory`](xref:NextORM.Core.DataContextBuilder), [`LogSensitiveData`](xref:NextORM.Core.DataContextBuilder), [`Factory`](xref:NextORM.Core.DataContextBuilder.Factory), [`UseQuotedIdentifiers`](xref:NextORM.Core.DataContextBuilder), [`UseNamingConvention`](xref:NextORM.Core.DataContextBuilder). |
| [`ServiceCollectionExtensions`](xref:NextORM.Core.ServiceCollectionExtensions) | [`AddNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions) / [`AddKeyedNextOrmContext`](xref:NextORM.Core.ServiceCollectionExtensions) (generic and options-driven). |

## Namespace [`NextORM.Sqlite`](xref:NextORM.Sqlite)

| Type | Description |
|---|---|
| [`SqliteDataContext`](xref:NextORM.Sqlite.SqliteDataContext) | [`DataContext`](xref:NextORM.Core.DataContext) over `Microsoft.Data.Sqlite`; registers the custom aggregates. |
| [`SqliteDialect`](xref:NextORM.Sqlite.SqliteDialect) | SQLite [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) singleton ([`Instance`](xref:NextORM.Sqlite.SqliteDialect.Instance)). |
| [`SqliteDataContextOptionsBuilderExtensions`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions) | `UseSqlite(string filepath)` and `UseSqlite(DbConnection)`. |

## Namespace [`NextORM.Postgres`](xref:NextORM.Postgres)

| Type | Description |
|---|---|
| [`PostgresDataContext`](xref:NextORM.Postgres.PostgresDataContext) | [`DataContext`](xref:NextORM.Core.DataContext) over `Npgsql`. |
| [`PostgresDialect`](xref:NextORM.Postgres.PostgresDialect) | PostgreSQL [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) singleton ([`Instance`](xref:NextORM.Postgres.PostgresDialect.Instance)). |
| [`PostgresDataContextOptionsBuilderExtensions`](xref:NextORM.Postgres.PostgresDataContextOptionsBuilderExtensions) | `UsePostgres(string connectionString)` and `UsePostgres(DbConnection)`. |

## Namespace [`NextORM.SqlServer`](xref:NextORM.SqlServer)

| Type | Description |
|---|---|
| [`SqlServerDataContext`](xref:NextORM.SqlServer.SqlServerDataContext) | [`DataContext`](xref:NextORM.Core.DataContext) over `Microsoft.Data.SqlClient`, with numeric column conversion. |
| [`SqlServerDialect`](xref:NextORM.SqlServer.SqlServerDialect) | SQL Server [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) singleton ([`Instance`](xref:NextORM.SqlServer.SqlServerDialect.Instance)). |
| [`SqlServerDataContextOptionsBuilderExtensions`](xref:NextORM.SqlServer.SqlServerDataContextOptionsBuilderExtensions) | `UseSqlServer(string connectionString)` and `UseSqlServer(DbConnection)`. |

## Namespace [`NextORM.MySql`](xref:NextORM.MySql)

| Type | Description |
|---|---|
| [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext) | [`DataContext`](xref:NextORM.Core.DataContext) over `MySqlConnector`. |
| [`MySqlDialect`](xref:NextORM.MySql.MySqlDialect) | MySQL [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) singleton ([`Instance`](xref:NextORM.MySql.MySqlDialect.Instance)); non-sealed so MariaDB can derive from it. |
| [`MySqlDataContextOptionsBuilderExtensions`](xref:NextORM.MySql.MySqlDataContextOptionsBuilderExtensions) | `UseMySql(string connectionString)` and `UseMySql(DbConnection)`. |

## Namespace [`NextORM.MariaDb`](xref:NextORM.MariaDb)

| Type | Description |
|---|---|
| [`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext) | [`DataContext`](xref:NextORM.Core.DataContext) over `MySqlConnector`, deriving from [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext). |
| [`MariaDbDialect`](xref:NextORM.MariaDb.MariaDbDialect) | MariaDB [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) singleton ([`Instance`](xref:NextORM.MariaDb.MariaDbDialect.Instance)); MySQL rendering plus `INTERSECT ALL`/`EXCEPT ALL`. |
| [`MariaDbDataContextOptionsBuilderExtensions`](xref:NextORM.MariaDb.MariaDbDataContextOptionsBuilderExtensions) | `UseMariaDb(string connectionString)` and `UseMariaDb(DbConnection)`. |

## Namespace [`NextORM.ClickHouse`](xref:NextORM.ClickHouse)

| Type | Description |
|---|---|
| [`ClickHouseDataContext`](xref:NextORM.ClickHouse.ClickHouseDataContext) | [`DataContext`](xref:NextORM.Core.DataContext) over the official `ClickHouse.Driver` ADO.NET provider. |
| [`ClickHouseDialect`](xref:NextORM.ClickHouse.ClickHouseDialect) | ClickHouse [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) singleton ([`Instance`](xref:NextORM.ClickHouse.ClickHouseDialect.Instance)). |
| [`ClickHouseDataContextOptionsBuilderExtensions`](xref:NextORM.ClickHouse.ClickHouseDataContextOptionsBuilderExtensions) | `UseClickHouse(string connectionString)` and `UseClickHouse(DbConnection)`. |

## See also

- [Provider overview](../providers/overview.md)
- [SQLite](../providers/sqlite.md)
- [SQL Server](../providers/sqlserver.md)
- [PostgreSQL](../providers/postgres.md)
- [MySQL](../providers/mysql.md)
- [MariaDB](../providers/mariadb.md)
- [ClickHouse](../providers/clickhouse.md)
- [In-memory](../providers/in-memory.md)

---

Source: `src/nextorm.core/**`, `src/nextorm.sqlite/**`, `src/nextorm.postgres/**`,
`src/nextorm.sqlserver/**`, `src/nextorm.mysql/**`, `src/nextorm.mariadb/**`,
`src/nextorm.clickhouse/**` (XML doc comments are the authoritative API documentation).
