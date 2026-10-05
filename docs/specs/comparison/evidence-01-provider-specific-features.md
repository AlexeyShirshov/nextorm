# Evidence 01: nextorm provider-specific query features per SQL provider

## Question

Per-provider inventory of nextorm's PROVIDER-SPECIFIC / provider-gated query features for the 6 SQL
providers (SQLite, SQL Server, PostgreSQL, MySQL, MariaDB, ClickHouse), for a nextorm-vs-linq2db
comparison. Sources: `docs/providers/*.md`, `docs/guide/provider-specific/*`, `docs/scalar-functions/*`,
`src/nextorm.<provider>/`, core `ISqlDialect` capability flags, provider translation visitors,
`src/nextorm.core/Query/SqlFunctions.{Postgres,SqlServer,MySql,Sqlite,ClickHouse}.cs`. For each row:
feature | nextorm support (yes/partial/no) | concrete API/symbol | file:line evidence. No
recommendations, no fixes.

## Commands

```
$ git -C /home/alex/sources/nextorm log -1 --format='%H %ci'
1ad3775f10a2fa9e3cff20bd4a5ac787226f15cb 2026-10-03 23:20:04 +0500

$ wc -l src/nextorm.core/Query/SqlFunctions*.cs
 1036 SqlFunctions.ClickHouse.cs
  257 SqlFunctions.MySql.cs
 1149 SqlFunctions.Postgres.cs
  328 SqlFunctions.SqlServer.cs
  274 SqlFunctions.Sqlite.cs
  961 SqlFunctions.cs

$ wc -l src/nextorm.{sqlite,sqlserver,postgres,mysql,mariadb,clickhouse}/*Dialect.cs
 466 sqlite/SqliteDialect.cs
1021 sqlserver/SqlServerDialect.cs
 744 postgres/PostgresDialect.cs
 747 mysql/MySqlDialect.cs
 165 mariadb/MariaDbDialect.cs
1114 clickhouse/ClickHouseDialect.cs

# roslyn (wrapped `roslynq`) `members` on NextORM.Core.FromOptions / JoinOptions / EntityBuilder`1 /
# QueryCommand`1 / DataContextExtensions / MergeBuilder`1 — file:line of each public fluent member.
# ignore-aware text search only for docs/SQL string literals (rg on docs/**, and on src string literals).
```

## Facts

### F0. Shared capability contract (the gate for every provider-specific feature)

- `ISqlDialect` is the single capability surface: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`.
  Defaults are `false`/`null`; a provider opts in by overriding. Key flags (line in ISqlDialect.cs):
  `SupportsArrays:207`, `SupportsArrayFunctions:216`, `SupportsHigherOrderArrayFunctions:225`,
  `SupportsRanges:234`, `SupportsRangeColumns:244`, `SupportsJson:285`, `SupportsTextJson:292`,
  `SupportsFullText:298`, `SupportsJsonExtract:605`, `SupportsDictionaries:655`,
  `SupportsGlobalPredicates:1241`, `SupportsJoinStrictness:69`, `SupportsGlobalJoin:74`,
  `SupportsFinal:1284`, `SupportsSample:1290`, `SupportsPreWhere:1329`, `SupportsSettings:1342`,
  `SupportsGroupByWithTotals:622`, `LimitBy:1250`, `DistinctOn:1257`, `ArrayJoinClause:1336`,
  `SupportsTemporalTable:1305`, `SupportsTemporalKind:1311`, `SupportsDataModifyingCtes:1419`,
  `SupportsReturning:1388`, `SupportsOutput:1395`, `SupportsOutputInto:1494`,
  `SupportsLastInsertId:1402`, `SupportsIdentityFunction:1411`, `SupportsOnConflict:1640`,
  `SupportsOnDuplicateKey:1655`, `SupportsMerge:1679`, `SupportsMergeStatement:1708`,
  `SupportsInsertIgnore:1581`, `SupportsOnConflictDoNothing:1597`, `SupportsBulkCopy:1531`,
  `SupportsBatch:1543`, `SupportsStoredProcedures:1563`, `SupportsTableValuedParameters:1573`,
  `SupportsCreateTableAsSelect:1943`, `SupportsTemporaryCreateTableAsSelect:1951`,
  `SupportsNamedWindows:340`, `SupportsWindowFrameGroups:346`, `SupportsWindowFrameExclusion:353`,
  `SupportsPercentileWindow:334`, `SupportsPercentRankCumeDist:319`, `SupportsNthValue:325`,
  `SupportsBooleanAggregates:520`, `SupportsBitAggregates:526`, `SupportsStatisticalAggregates:531`,
  `SupportsRegressionAggregates:539`, `SupportsArgMinMax:544`, `SupportsOrderedAggregates:669`.
- Cross-provider builder entry points (all in `src/nextorm.core`): `FromOptions.WithTableHint`
  (FromOptions.cs:82), `WithIndex` (:104/:117), `WithoutIndex` (:143), `WithSubQueryHint` (:156),
  `TableSample` (:29); `JoinOptions.WithJoinHint` (JoinOptions.cs:66), `WithJoinTableHint` (:102),
  `WithStrictness` (:38), `Global` (:50); `QueryCommand<TResult>.Hint` (QueryCommand.TResult.cs:756),
  `WithTag` (:775), `ForJson` (:898), `ForXml` (:920), `WithForJson` (:860), `WithForXml` (:879);
  `DataContextExtensions.CreateMergeBuilder` (DataContextExtensions.cs:157),
  `CreateBulkInsertBuilder` (:92/:113/:134), `CreateTruncateBuilder` (:984), `FromTableFunction`
  (:1387), `FromSql` (:1216).

### F1. SQLite (`src/nextorm.sqlite/`)

| Feature | Support | API / symbol | Evidence |
|---|---|---|---|
| LOB streaming via `rowid` locator | yes | `SupportsSequentialAccess=true`, `LobLocatorColumn=>"rowid"` | SqliteDialect.cs:15,18 |
| DML `RETURNING` | yes | `SupportsReturning=true`; `MakeReturning` (default) | SqliteDialect.cs:32; ISqlDialect.cs:1477 |
| Upsert `ON CONFLICT ... DO UPDATE` | yes | `SupportsOnConflict=true`; `MakeOnConflict` | SqliteDialect.cs:83; ISqlDialect.cs:1647 |
| `INSERT OR IGNORE` / `ON CONFLICT DO NOTHING` | yes | `SupportsInsertIgnore=true`; `SupportsOnConflictDoNothing=true` | SqliteDialect.cs:88,90 |
| Index hints `INDEXED BY`/`NOT INDEXED` | yes | `IndexHints`; `SqliteIndexHintRenderer` | SqliteDialect.cs:329,361-376 |
| Range over scalar column pair | yes | `SupportsRangeColumns=true`; `RangeColumnsSqlTranslator.cs` | SqliteDialect.cs:35 |
| Regex (CLR `Regex` -> `regexp`/`regexp_replace`) | yes | `SupportsRegex=true`; `MakeRegexMatch`; registered functions | SqliteDialect.cs:191,194; SQLiteFunctions.cs:42-46 |
| CTAS / temporary table | yes | `SupportsCreateTableAsSelect=true`; `SupportsCreateTableAsSelectIfNotExists=true`; no `TRUNCATE` | SqliteDialect.cs:332,335 |
| Identity (`last_insert_rowid()`) | yes | `SupportsLastInsertId=true`; `MakeLastInsertId` default | SqliteDialect.cs:37; ISqlDialect.cs:1517 |
| Table-valued parameters (`json_each` doc) | yes | `SupportsTableValuedParameters=true` | SqliteDialect.cs:27 |
| Stored procedures | no | `SupportsStoredProcedures=false` | SqliteDialect.cs:24 |
| Named windows / frame `GROUPS` / `EXCLUDE` | yes | `SupportsNamedWindows=true`; `SupportsWindowFrameGroups=true`; `SupportsWindowFrameExclusion=true` | SqliteDialect.cs:137,143,146 |
| Window `percent_rank`/`cume_dist`/`nth_value` | yes | `SupportsPercentRankCumeDist=true`; `SupportsNthValue=true` | SqliteDialect.cs:131,134 |
| Provider-specific scalar library (JSON1/printf/hex/glob/…) | yes | `SqlFunctions.Sqlite`; `ISqlDialect.SqliteFunctions` | SqlFunctions.Sqlite.cs:12,88,94,115,187-273; SqliteDialect.cs:119 |
| JSON1 (`json()`, `jsonb()`, `json_extract`, `->`/`->>`, `json_each`/`json_tree`) | yes | `SqlFunctions.Sqlite.*` | SqlFunctions.Sqlite.cs:187,190,197,200,203,247-273 |
| Custom aggregate family (`stdev`/`stdevp`/`var`/`varp`) | yes | registered `CreateAggregate` | SQLiteFunctions.cs:23-37 |
| `string_agg` -> `group_concat` | yes | `SupportsStringAgg=true`; `MakeStringAgg` | SqliteDialect.cs:106-110 |
| Full-text FTS3/FTS4/FTS5 query + FROM (FTS5 maintenance/control deferred to [#195](https://github.com/AlexeyShirshov/nextorm/issues/195)/[#196](https://github.com/AlexeyShirshov/nextorm/issues/196)) | yes | `SqlFunctions.Sqlite` FTS members; `ISqlDialect.SqliteFunctions`; FTS5 table-valued `[SqlTableFunction]` | SqlFunctions.Sqlite.cs:276-354; SqliteFunctionRenderer.cs:26-27,44-53; SqliteDialect.cs (SupportsTableFunction/WrapTableFunction); SqliteSpecificTests.cs FTS tests |
| Arrays / range types / temporal / data-modifying CTE / OUTPUT / MERGE / query hints / bulk copy | no | not overridden (defaults) | ISqlDialect defaults; docs/providers/sqlite.md:155-171 |

### F2. SQL Server (`src/nextorm.sqlserver/`)

| Feature | Support | API / symbol | Evidence |
|---|---|---|---|
| `OUTPUT inserted.<col>` / `OUTPUT ... INTO` | yes | `SupportsOutput=true`; `SupportsOutputInto=true`; `MakeOutputInto`; `OutputIntoBuilder` | SqlServerDialect.cs:27,39; ISqlDialect.cs:1482,1502 |
| Full `MERGE` (branches, `BY SOURCE`, conditions) | yes | `SupportsMerge=true`; `MakeMerge`; `SupportsMergeStatement=true`; `SupportsMergeBySourceDelete=true`; `SupportsMergeConditionalBranches=true`; `MergeBuilder<TEntity>` | SqlServerDialect.cs:116,119,125,128,134 |
| Hints: table / query / join / tables-in-scope / index | yes | `SupportsTableHints=true`; `SupportsQueryHints=true`; `SupportsJoinHints=true`; `SupportsTablesInScopeHints=true`; `IndexHints` | SqlServerDialect.cs:258,265,291,298,675; FromOptions.cs:82; QueryCommand.TResult.cs:756 |
| Temporal tables `FOR SYSTEM_TIME` (all kinds) | yes | `SupportsTemporalTable=true`; `SupportsTemporalKind`; `EntityBuilder.ForSystemTime` | SqlServerDialect.cs:242,245; EntityBuilder.cs:1984 |
| `FOR JSON` / `FOR XML` | yes | `SupportsForJson=true`; `SupportsForXml=true`; `ForJson`/`ForXml`/`WithForJson`/`WithForXml` | SqlServerDialect.cs:304,322; QueryCommand.TResult.cs:898,920,860,879 |
| XML data-type methods (`.value`/`.query`/`.exist`/`.nodes`) | yes | `XmlFunctions`/`SqlServerXmlFunctions`; `SqlFunctions.SqlServer.xml_*` | SqlServerDialect.cs:229,756-764; SqlFunctions.SqlServer.cs:50-87 |
| `OPENJSON` / `STRING_SPLIT` / `CONTAINSTABLE`/`FREETEXTTABLE` | yes | `SupportsTableFunction`; `SqlFunctions.SqlServer.openjson/string_split/containstable/freetexttable` | SqlServerDialect.cs:251-252; SqlFunctions.SqlServer.cs:97,110,122,129 |
| Native `PIVOT` / `UNPIVOT` | yes | `Pivot`; `EntityBuilder.Pivot`/`Unpivot` | SqlServerDialect.cs:248; EntityBuilder.cs:2010,2037 |
| Full-text `contains`/`freetext` | yes | `SupportsFullText=true`; `MakeFullText` | SqlServerDialect.cs:232,235 |
| Text JSON (`json_value`/`json_query`/`json_modify`/`isjson`) | yes | `SupportsTextJson=true`; `MakeIsJson` | SqlServerDialect.cs:222,225 |
| Regex (`REGEXP_LIKE`/`REGEXP_REPLACE`, 2025+) | yes | `SupportsRegex=true`; `MakeRegexMatch` | SqlServerDialect.cs:436,439,443 |
| Range over scalar column pair | yes | `SupportsRangeColumns=true` | SqlServerDialect.cs:33 |
| CTAS via `SELECT ... INTO`; temp via `ToTable("#name")` | yes/partial | `SupportsCreateTableAsSelect=true`; `CreateTableAsSelectUsesSelectInto=true`; `SupportsTemporaryCreateTableAsSelect=false` | SqlServerDialect.cs:82,85,91,94 |
| Native bulk copy + flags | yes | `SupportsBulkCopy=true`; `SqlBulkCopyOptions` (`BulkInsertOptions`) | SqlServerDialect.cs:55; docs/providers/overview.md:49 |
| Identity (`SCOPE_IDENTITY()`, `SET IDENTITY_INSERT`) | yes | `SupportsIdentityFunction=true`; `RequiresIdentityInsertToggle=true` | SqlServerDialect.cs:46,70,162 |
| Table-valued parameters (native UDT) | yes | `SupportsTableValuedParameters=true`; `ProcedureParameter.Table(name, typeName, rows)` | SqlServerDialect.cs:64; docs/providers/sqlserver.md:193-232 |
| Stored procedures | yes | `SupportsStoredProcedures=true` | SqlServerDialect.cs:61 |
| Window percentiles (`within group ... over`) | yes | `SupportsPercentileWindow=true` | SqlServerDialect.cs:363 |
| Named windows / `GROUPS` / `EXCLUDE` / `NTH_VALUE` | no | not overridden (SQL Server has no `WINDOW` clause) | ISqlDialect.cs:340,346,353,325 |
| Provider-specific aggregate family | yes (native `stdev`/`var`, `string_agg`) | `SupportsStringAgg=true`; `MakeCount` `count_big` | SqlServerDialect.cs:366,629 |
| Provider-specific scalar library | yes | `SqlFunctions.SqlServer`; `ISqlDialect.SqlServerFunctions` | SqlFunctions.SqlServer.cs:15-327; SqlServerDialect.cs:696; SqlServerDialect.cs:962+ |
| Sequences (`NEXT VALUE FOR`) | not found | no symbol | see Not found |

### F3. PostgreSQL (`src/nextorm.postgres/`)

| Feature | Support | API / symbol | Evidence |
|---|---|---|---|
| Native `json`/`jsonb` | yes | `SupportsJson=true`; `JsonSqlTranslator`; `SqlFunctions.Postgres.json*` | PostgresDialect.cs:305; SqlFunctions.Postgres.cs:105-270 |
| Native arrays + `any`/`all` quantifiers | yes | `SupportsArrays=true`; `SqlFunctions.Postgres.any/all/array_*` | PostgresDialect.cs:258; SqlFunctions.Postgres.cs:19-103 |
| Native range + multirange | yes | `SupportsRanges=true`; `PostgresRangeSqlTranslator`; `Range<T>`/`Range<T>[]`; `MakeTypeName` maps range/multirange | PostgresDialect.cs:262,143-148; SqlFunctions.Postgres.cs:738-1148 |
| Range over scalar column pair | yes | `SupportsRangeColumns=true` | PostgresDialect.cs:265 |
| Full-text `contains`/`freetext` + native `tsvector` surface | yes | `SupportsFullText=true`; `SupportsTextSearchFunctions=true`; `MakeFullText`; `Postgres.ts_*` | PostgresDialect.cs:356,359,388; SqlFunctions.Postgres.cs:519-563 |
| Regex (POSIX `~`/`~*`, `regexp_replace`) | yes | `SupportsRegex=true`; `MakeRegexMatch` | PostgresDialect.cs:476,479,483 |
| Data-modifying CTE (`WITH x AS (INSERT ... RETURNING ...)`) | yes | `SupportsDataModifyingCtes=true`; `With(name, insert)`; `MutationCteQuery<TResult>` | PostgresDialect.cs:34; DataContextExtensions.cs:1551; Builders/MutationCteQuery.cs |
| DML `RETURNING` (insert/update/delete + join) | yes | `SupportsReturning=true`; `SupportsUpdateJoinReturning=true`; `SupportsDeleteJoinReturning=true` | PostgresDialect.cs:22,25,28 |
| Full `MERGE` (15+), `DO NOTHING`, conditions | yes | `SupportsMergeStatement=true`; `SupportsMergeDelete=true`; `SupportsMergeDoNothing=true`; `SupportsMergeConditionalBranches=true`; `MakeMergeReturning` | PostgresDialect.cs:58,61,64,67,73 |
| Upsert `ON CONFLICT ... DO UPDATE`/`DO NOTHING` | yes | `SupportsOnConflict=true`; `SupportsOnConflictDoNothing=true` | PostgresDialect.cs:37,52 |
| Query hints (`/*+ ... */`, subquery, tables-in-scope) | yes | `SupportsQueryHints=true`; `SupportsInlineHints=true`; `SupportsSubQueryHints=true` | PostgresDialect.cs:189,192,195 |
| Index hints | no | `IndexHints` not overridden (`null`) | ISqlDialect.cs:725 |
| CTAS / temporary / `ON COMMIT` / `WITH NO DATA` | yes | `SupportsCreateTableAsSelect=true`; `SupportsTemporaryCreateTableAsSelect` (inherits true); `SupportsCreateTableAsSelectColumnList`; `OnCommit`; `WithNoData` | PostgresDialect.cs:519,522,525,528,531,534,549 |
| Native bulk copy (`COPY BINARY`) | yes | `SupportsBulkCopy=true` | PostgresDialect.cs:40 |
| Identity (`lastval()`) + sequences | yes | `SupportsIdentityFunction=true`; `MakeIdentityFunction`; `Postgres.nextval/setval/currval/lastval` | PostgresDialect.cs:77,127; SqlFunctions.Postgres.cs:615-625 |
| Runtime settings (`current_setting`/`set_config`) | yes | `SqlFunctions.Postgres.current_setting/set_config` | SqlFunctions.Postgres.cs:500-510 |
| `jsonb_path_*` family | yes | `SqlFunctions.Postgres.jsonb_path_exists/match/query_first/query_array`; `jsonpath` cast | SqlFunctions.Postgres.cs:227-245,700-704 |
| Table-valued parameters (typed array / `jsonb_to_recordset`) | yes | `SupportsTableValuedParameters=true`; `SupportsResultSchema(AliasColumnList)`; `jsonb_to_record(set)` | PostgresDialect.cs:49,277; SqlFunctions.Postgres.cs:716,728 |
| Stored procedures | yes | `SupportsStoredProcedures=true` | PostgresDialect.cs:46 |
| Named windows / `GROUPS` / `EXCLUDE` | yes | `SupportsNamedWindows=true`; `SupportsWindowFrameGroups=true`; `SupportsWindowFrameExclusion=true` | PostgresDialect.cs:324,327,330 |
| Ordered-set percentiles (`within group`) | yes | `SupportsOrderedAggregates=true`; `Postgres.percentile_cont/percentile_disc/mode` | PostgresDialect.cs:408; SqlFunctions.Postgres.cs:607-613 |
| Boolean / bit / statistical / regression aggregates | yes | `SupportsBooleanAggregates=true`; `SupportsBitAggregates=true`; `SupportsStatisticalAggregates=true`; `SupportsRegressionAggregates=true` | PostgresDialect.cs:400-406; SqlFunctions.Postgres.cs:565-599 |
| Provider-specific scalar library | yes | `SupportsExtendedScalarFunctions=true`; `SqlFunctions.Postgres` (`to_char`, `split_part`, `regexp_*`, `digest`/`sha*`, `pg_typeof`, `num_nulls`) | PostgresDialect.cs:379; SqlFunctions.Postgres.cs:281-516 |
| `DISTINCT ON` / `TABLESAMPLE` / `WITH TIES` | yes | `DistinctOn`; `TableSample`; `SupportsWithTies=true` | PostgresDialect.cs:365,374,513 |
| `generate_series`/`unnest`/`regexp_matches`/`jsonb_*` TVFs | yes | `SupportsTableFunction`; `WrapTableFunction` | PostgresDialect.cs:268-274,297-301 |

### F4. MySQL (`src/nextorm.mysql/`)

| Feature | Support | API / symbol | Evidence |
|---|---|---|---|
| Text JSON (`json_value`/`json_query`/`json_modify`/`isjson`) | yes | `SupportsTextJson=true`; `MakeTextJsonFunction`; `MakeIsJson` | MySqlDialect.cs:202,217,227 |
| JSON mutation + introspection + binary UUID | yes | `SqlFunctions.MySql.json_*/uuid_to_bin/bin_to_uuid`; `MySqlNativeFunctions` | SqlFunctions.MySql.cs:99-143; MySqlDialect.cs:699-745 |
| Range over scalar column pair | yes | `SupportsRangeColumns=true` | MySqlDialect.cs:29 |
| Full-text `match ... against` | yes | `SupportsFullText=true`; `MakeFullText` | MySqlDialect.cs:190,193 |
| Regex (`REGEXP_LIKE`/`REGEXP_REPLACE`) | yes | `SupportsRegex=true`; `MakeRegexMatch` | MySqlDialect.cs:438,441,445 |
| Index hints `USE`/`FORCE`/`IGNORE INDEX` | yes | `IndexHints`; `MySqlIndexHintRenderer` | MySqlDialect.cs:495,576-593 |
| Query hints (`/*+ ... */`, subquery, tables-in-scope) | yes | `SupportsQueryHints=true`; `SupportsInlineHints=true`; `SupportsSubQueryHints=true` | MySqlDialect.cs:109,112,115 |
| Upsert `ON DUPLICATE KEY UPDATE` + `INSERT IGNORE` | yes | `SupportsOnDuplicateKey=true`; `MakeUpsertValueReference` -> `VALUES(col)`; `SupportsInsertIgnore=true` | MySqlDialect.cs:96,98,60,63 |
| CTAS / temporary / column list | yes | `SupportsCreateTableAsSelect=true`; `...IfNotExists=true`; `...ColumnList=true` | MySqlDialect.cs:501,504,507 |
| Identity (`LAST_INSERT_ID()`) | yes | `SupportsLastInsertId=true`; `MakeLastInsertId` | MySqlDialect.cs:26,31 |
| Table-valued parameters (`JSON_TABLE` doc) | yes | `SupportsTableValuedParameters=true` | MySqlDialect.cs:57 |
| Stored procedures | yes | `SupportsStoredProcedures=true` | MySqlDialect.cs:54 |
| Named windows / `percent_rank`/`nth_value` | yes | `SupportsNamedWindows=true`; `SupportsPercentRankCumeDist=true`; `SupportsNthValue=true` | MySqlDialect.cs:176,179,182 |
| Frame `GROUPS` / `EXCLUDE` / window percentiles | no | not overridden (MySQL has `ROWS`/`RANGE` only; `PERCENTILE_CONT` is MariaDB-only) | ISqlDialect.cs:346,353,334 |
| Native aggregate surface (`stddev_samp`/`ANY_VALUE`/`group_concat`) | yes | `MakeAggregate`; `SupportsAnyValueAggregate=true`; `SupportsStringAgg=true` | MySqlDialect.cs:459,205,232,235 |
| `FULL JOIN` | no | `SupportsFullJoin=false` | MySqlDialect.cs:166 |
| Provider-specific scalar library | yes | `SqlFunctions.MySql`; `IMySqlFunctions.MySqlNativeFunctions` | SqlFunctions.MySql.cs:17-143; MySqlDialect.cs:214,699 |
| System-versioned / temporal | no | not overridden | ISqlDialect.cs:1305 |
| Sequences (`NEXT VALUE FOR`) | no (MariaDB-only) | not in `MySqlNativeFunctions.Supports` | MySqlDialect.cs:704-711 |

### F5. MariaDB (delta over MySQL, `src/nextorm.mariadb/`)

| Feature | Support | API / symbol | Evidence |
|---|---|---|---|
| Temporal tables `FOR SYSTEM_TIME` (no `CONTAINED IN`) | yes | `SupportsTemporalTable=true`; `SupportsTemporalKind` | MariaDbDialect.cs:53,56 |
| `INTERSECT ALL` / `EXCEPT ALL` | yes | `SupportsIntersectExceptAll=true` | MariaDbDialect.cs:34 |
| Window percentiles (`PERCENTILE_CONT`/`PERCENTILE_DISC`) | yes | `SupportsPercentileWindow=true` | MariaDbDialect.cs:37 |
| `ANY_VALUE` (arbitrary-value aggregate) | no | `SupportsAnyValueAggregate=false` | MariaDbDialect.cs:47 |
| UUID generators (`uuid_v4()`/`uuid_v7()`) | yes | `UuidGenerators`/`MariaDbUuidGenerators` | MariaDbDialect.cs:50,153-164 |
| Regex (`REGEXP` operator; PCRE `(?i)`/`(?-i)`) | yes | `SupportsRegex=true`; `MakeRegexMatch` | MariaDbDialect.cs:17,20,24 |
| Extended regexp / `NVL`/`NVL2` / `ADD_MONTHS`/`MONTHS_BETWEEN` / `TO_CHAR`/`TO_DATE`/`TO_NUMBER` / `KDF` / `XXH3`/`XXH32` / `JSON_DETAILED`/`JSON_COMPACT` | yes | `SqlFunctions.MySql.*`; `MariaDbNativeFunctions` | MariaDbDialect.cs:82-130; SqlFunctions.MySql.cs:145-232 |
| Sequences (`NEXT VALUE FOR`, `nextval`, `setval`, `lastval`) | yes | `SqlFunctions.MySql.next_value_for/nextval/setval/lastval`; `MariaDbNativeFunctions` | SqlFunctions.MySql.cs:238-256; MariaDbDialect.cs:117-120,88-93 |
| MySQL native surface minus `uuid_to_bin`/`bin_to_uuid` | partial | `MariaDbNativeFunctions.Supports` | MariaDbDialect.cs:88-97 |
| Row locking `LOCK IN SHARE MODE` + `NOWAIT`/`SKIP LOCKED` | yes | `Lock`/`MariaDbLockRenderer` | MariaDbDialect.cs:62,132-151 |
| Bulk copy | no | `SupportsBulkCopy=false` | MariaDbDialect.cs:31 |

### F6. ClickHouse (`src/nextorm.clickhouse/`)

| Feature | Support | API / symbol | Evidence |
|---|---|---|---|
| String JSON (`JSONExtract*`/`visitParamExtract*`) + JSONPath scalars | yes | `SupportsJsonExtract=true`; `MakeJsonExtract` | ClickHouseDialect.cs:287,294; docs/providers/clickhouse.md:225 |
| Native-JSON functions (`JSONAllPaths`, `JSONAllPathsWithTypes`, `toJSONString`) | partial | same gate; native `JSON` *column* type not mapped | docs/providers/clickhouse.md:226,235 |
| Arrays + higher-order (lambda) array functions | yes | `SupportsArrayFunctions=true`; `SupportsHigherOrderArrayFunctions=true`; `MakeArrayFunction` | ClickHouseDialect.cs:129,136,155; SqlFunctions.ClickHouse.cs:478-642 |
| `ARRAY JOIN` clause + scalar `array_join` | yes | `SupportsArrayJoin=true`; `ArrayJoinClause`; `ClickHouseEntityBuilderExtensions.ArrayJoin/LeftArrayJoin/ArrayJoinElement` | ClickHouseDialect.cs:145,461,882-891; Extensions/ClickHouseEntityBuilderExtensions.cs:33,39,45,51 |
| Range over scalar column pair | yes | `SupportsRangeColumns=true` | ClickHouseDialect.cs:41 |
| Regex (`match`/`replaceRegexpAll`, RE2) | yes | `SupportsRegex=true`; `MakeRegexMatch`/`MakeRegexReplace`; `ClickHouse.match/extract/replace_regexp_*` | ClickHouseDialect.cs:559,562,566; SqlFunctions.ClickHouse.cs:816-850 |
| Join strictness `ANY`/`ALL`/`ASOF`, `GLOBAL`, `SEMI`/`ANTI`/`PASTE` | yes | `SupportsJoinStrictness`; `SupportsGlobalJoin`; `SupportsSemiAntiJoin`; `SupportsPasteJoin`; `MakeJoinKeyword`; `JoinOptions.WithStrictness`/`Global` | ClickHouseDialect.cs:112,115,118,121,163; Extensions/ClickHouseEntityBuilderExtensions.cs:123,129 |
| `FINAL` / `SAMPLE` / `PREWHERE` / `SETTINGS` | yes | `SupportsFinal`/`SupportsSample`/`SupportsPreWhere`/`SupportsSettings`; `Final`/`Sample`/`PreWhere`/`Settings` | ClickHouseDialect.cs:452,455,458,464; Extensions/ClickHouseEntityBuilderExtensions.cs:15,21,27,111-120 |
| `LIMIT n BY expr` | yes | `LimitBy`/`ClickHouseLimitByRenderer` | ClickHouseDialect.cs:449,795-804; Extensions/ClickHouseEntityBuilderExtensions.cs:57,63 |
| `GROUP BY ... WITH TOTALS` | yes | `SupportsGroupByWithTotals=true`; `MakeGroupByTotals`; `WithTotals` | ClickHouseDialect.cs:388,391; Extensions/ClickHouseEntityBuilderExtensions.cs:69 |
| Distributed `GLOBAL IN` | yes | `SupportsGlobalPredicates=true`; `ClickHouseFunctions.global_in` | ClickHouseDialect.cs:446; SqlFunctions.ClickHouse.cs:320-326 |
| Dictionaries (`dictGet`/`dictHas`/…) | yes | `SupportsDictionaries=true`; `MakeDictionaryFunction`; `ClickHouseFunctions.dict_*` | ClickHouseDialect.cs:340,343; SqlFunctions.ClickHouse.cs:284-312 |
| Distinct-count / quantile / top-K / sequence aggregates; `argMin`/`argMax`; `-If` combinator; bitmap/sumMap | yes | `UniqAggregates`; `QuantileAggregates`; `TopKAggregates`; `SequenceAggregates`; `SupportsArgMinMax`; `AggregateFilterStyle=IfCombinator` | ClickHouseDialect.cs:237-257,368; SqlFunctions.ClickHouse.cs:41-154,957-985 |
| Frame-respecting `lagInFrame`/`leadInFrame` | yes | `SupportsInFrameWindowFunctions=true`; `ClickHouseFunctions.lag_in_frame`/`lead_in_frame` | ClickHouseDialect.cs:269; SqlFunctions.ClickHouse.cs:766-786 |
| Named windows / frame `GROUPS` | yes | `SupportsNamedWindows=true`; `SupportsWindowFrameGroups=true` | ClickHouseDialect.cs:272,281 |
| `percent_rank`/`cume_dist`/`nth_value` | yes | `SupportsPercentRankCumeDist=true`; `SupportsNthValue=true` | ClickHouseDialect.cs:260,263 |
| Frame `EXCLUDE` / window percentiles | no | not overridden | ISqlDialect.cs:353,334 |
| Table functions `numbers`/`numbers_mt`, `zeros`/`zeros_mt`, `generateRandom`, `values` | yes | `SupportsTableFunction`; `WrapTableFunction`; `ClickHouseFunctions.numbers/zeros/generate_random/values` | ClickHouseDialect.cs:399-401,423; SqlFunctions.ClickHouse.cs:335-401 |
| Server/cluster/file table functions `url`/`s3`/`file`/`remote`/`remoteSecure`/`cluster`/`clusterAllReplicas` | yes (declared) | `SupportsTableFunction`; `ClickHouseFunctions.url/s3/file/remote/remote_secure/cluster/cluster_all_replicas` | ClickHouseDialect.cs:399-401; SqlFunctions.ClickHouse.cs:415-471 |
| Date conversion/part surface (`toDate`/`toDateTime`/`toStartOf*`/`toYYYYMM`/…) | yes | `DateConversion`/`ClickHouseDateConversionRenderer` | ClickHouseDialect.cs:223,893-928; SqlFunctions.ClickHouse.cs:649-721 |
| Tuple constructor / `tupleElement` | yes | `Tuple`/`ClickHouseTupleRenderer` | ClickHouseDialect.cs:142,931-938 |
| Map family + hash family + `generateULID` (native scalars) | yes | `ClickHouseFunctions.map*/md5/sha*/xxHash*/murmur*/generate_ulid` | SqlFunctions.ClickHouse.cs:918-1035 |
| CTAS `CREATE TABLE ... ENGINE = MergeTree ... AS SELECT` | yes (persistent); no temp CTAS | `SupportsCreateTableAsSelect=true`; `MakeCreateTableAsSelect`; `SupportsTemporaryCreateTableAsSelect=false` | ClickHouseDialect.cs:92,98,101 |
| DML `UPDATE`/`DELETE` mutations (`ALTER TABLE ... SETTINGS mutations_sync=1`) | yes | `MakeUpdateHead`/`MakeDeleteHead`/`Make*Suffix`; `UpdateRequiresWhere`/`DeleteRequiresWhere` | ClickHouseDialect.cs:67-83 |
| Upsert / `MERGE` / `RETURNING`/`OUTPUT` / `TRUNCATE` | partial | `SupportsInsertIgnore=true` (no-op); no `SupportsOnConflict`/`OnDuplicateKey`/`Merge`; `SupportsTruncate=true` | ClickHouseDialect.cs:62,65,86 |
| Table-valued parameters (`Array(T)`/`Array(Tuple)` + `arrayJoin`) | yes | `SupportsTableValuedParameters=true` | ClickHouseDialect.cs:48 |
| Stored procedures / bulk copy / transactions / LOB streaming | no | `SupportsStoredProcedures` default false; `SupportsBulkCopy=false`; `SupportsTransactions=false` | ClickHouseDialect.cs:56,377; docs/providers/clickhouse.md:236 |

### F7. Cross-provider gates worth pinning (for the comparison)

- `*ALL` set ops: Postgres `SupportsIntersectExceptAll=true` (PostgresDialect.cs:246), MariaDB
  (MariaDbDialect.cs:34), ClickHouse (ClickHouseDialect.cs:89); SQLite/SQL Server/MySQL false.
- `MARK`/`MERGE` capability split: SQL Server key-upsert `SupportsMerge` (SqlServerDialect.cs:116),
  Postgres general `SupportsMergeStatement` (PostgresDialect.cs:58).
- LOB streaming: SQLite (rowid locator, SqliteDialect.cs:15,18), SQL Server (SqlServerDialect.cs:30),
  Postgres (PostgresDialect.cs:31); MySQL/MariaDB/ClickHouse/in-memory reject.
- `TABLESAMPLE`: only Postgres (both methods + seed, PostgresDialect.cs:374,599-613) and SQL Server
  (`SYSTEM` only, SqlServerDialect.cs:239,766-779).
- TVP emulation per provider is documented in `docs/providers/overview.md:45`.

## Not found

- SQL Server `NEXT VALUE FOR` / sequence access API: searched `docs/providers/sqlserver.md`,
  `docs/guide/provider-specific/sqlserver.md`, `src/nextorm.core/Query/SqlFunctions.SqlServer.cs`,
  `src/nextorm.sqlserver/SqlServerDialect.cs` for `next value for` / `sequence` — only TVP "empty
  sequence" prose (docs/providers/sqlserver.md:195,232) matched; no sequence function symbol.
- `SupportsTemporalTable` on MySQL (only SQL Server and MariaDB override it).
- ClickHouse `native JSON` column mapping: explicitly not mapped (docs/providers/clickhouse.md:235,260-263).
- PostgreSQL / ClickHouse index hints: `IndexHints` is `null` (not overridden).

## Open questions

- SQL Server sequence functions (`NEXT VALUE FOR`) may exist outside the searched symbols; not found in
  the four files above.
- The ClickHouse server/cluster/file table functions are declared and gated (F6), but their end-to-end
  execution is not exercised by unit SQL-generation tests; the guide marks distributed table functions
  as out of scope (docs/guide/provider-specific/clickhouse.md:258-260).
- linq2db side is out of scope for this fact pass (nextorm-only inventory).
