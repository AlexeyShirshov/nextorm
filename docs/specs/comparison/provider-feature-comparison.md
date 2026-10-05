# nextorm vs linq2db: per-provider feature comparison

Internal engineering spec. Date: 2026-10-04. Status: first pass, evidence-backed.

## Purpose

`linq2db-comparison.md` compares nextorm with linq2db at the cross-provider construct level.
This document answers a narrower question: **for each SQL engine both libraries support, how
wide is the provider-specific API surface?** The goal is to test the claim that nextorm covers
provider-specific features more broadly than linq2db.

Method: nextorm facts are read from this repository (`ISqlDialect` capability flags, provider
dialects, `SqlFunctions.<Provider>` libraries, docs) with `file:line`. linq2db facts are read from
its source on GitHub (`linq2db/linq2db`, `master`, commit `5dfadd7eccb99fe18b6b2195897d21103f897eba`)
at file level, with absence claims explicitly marked as source-search findings.

## Legend

- `yes` — first-class provider API present.
- `partial` — present with a named limitation, or reachable only through a generic mechanism.
- `no` — no dedicated provider API found.
- `n/a` — feature does not exist on that engine.

## Provider footprint

| Engine | nextorm | linq2db |
|---|---|---|
| SQLite | yes (`src/nextorm.sqlite`) | yes (`Source/LinqToDB/DataProvider/SQLite`) |
| SQL Server | yes (`src/nextorm.sqlserver`) | yes (`Source/LinqToDB/DataProvider/SqlServer`) |
| PostgreSQL | yes (`src/nextorm.postgres`) | yes (`Source/LinqToDB/DataProvider/PostgreSQL`) |
| MySQL | yes, own provider (`src/nextorm.mysql`) | yes (`Source/LinqToDB/DataProvider/MySql`) |
| MariaDB | yes, own provider (`src/nextorm.mariadb`) | shared MySQL provider, `MySqlVersion.MariaDB10`; no separate extension surface |
| ClickHouse | yes, own provider (`src/nextorm.clickhouse`) | yes (Octonica/Driver/MySql), `Source/LinqToDB/DataProvider/ClickHouse` |
| In-memory object context | yes (`src/nextorm.core` in-memory context) | no provider |
| Oracle / Firebird / DB2 / SAP HANA / Informix / Sybase / Access / SQL CE / DuckDB / Ydb | no | yes |

Note: linq2db's ClickHouse/DuckDB/Ydb providers were added after the first draft of
`linq2db-comparison.md`; that document's provider list and its ClickHouse-derived cells have since been
corrected. linq2db does **not** ship an in-memory object provider.

## Key finding

**nextorm covers provider-specific features more broadly.** Across the six SQL engines both libraries
support, nextorm exposes a dedicated provider-specific API on a wider set of surfaces than linq2db — a
per-provider scalar-function library for every engine, the ClickHouse clause/aggregate/table-function
surface, PostgreSQL full-text/regex/`jsonb_path_*`/range/data-modifying CTE, MySQL/MariaDB JSON/regex/
sequences, and SQLite JSON1/regex/aggregates/LOB — all wired to the query builder rather than raw SQL
(see the per-provider sections and Net assessment).

This is a statement about **per-engine feature depth, not the number of engines**: linq2db connects to
more database products (it adds Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE,
DuckDB and Ydb), while nextorm adds an in-memory object context and splits MySQL/MariaDB into separate
providers. linq2db is ahead on SQL Server scalar-function breadth/hints (residual gap
[#182](https://github.com/AlexeyShirshov/nextorm/issues/182)) and on SQLite full-text
([#181](https://github.com/AlexeyShirshov/nextorm/issues/181)).

## How linq2db models provider specifics

linq2db exposes provider-specific query features through per-provider static extension classes:

- `Source/LinqToDB/DataProvider/PostgreSQL/PostgreSQLExtensions.cs`, `PostgreSQLHints.cs`
- `Source/LinqToDB/DataProvider/SqlServer/SqlServerExtensions.cs`, `SqlFn.cs`, `SqlServerHints.cs`
- `Source/LinqToDB/DataProvider/MySql/MySqlExtensions.cs`, `MySqlHints.cs`
- `Source/LinqToDB/DataProvider/SQLite/SQLiteExtensions.cs`, `SQLiteHints.cs`
- `Source/LinqToDB/DataProvider/ClickHouse/ClickHouseSpecificExtensions.cs`, `ClickHouseHints.cs`

plus provider-agnostic DML in `Source/LinqToDB/LinqExtensions/LinqExtensions.{Insert,Update,Delete,Merge}.cs`
and `Sql.Window` for named windows. Absence of a dedicated extension method below does not prove the
construct is impossible: it is always expressible through raw SQL / `Sql.Ext` / `Sql.Function`. The
claim tested here is width of the *dedicated provider API*.

---

## PostgreSQL

| Feature | nextorm | linq2db | nextorm evidence | linq2db evidence |
|---|---|---|---|---|
| Arrays + `any`/`all` quantifiers | yes | yes | `PostgresDialect.cs:258`; `SqlFunctions.Postgres.cs:19-103` | `PostgreSQLExtensions.cs` (`ARRAY_*`, `CARDINALITY`, `= ANY(...)`, `&&`, `@>`, `<@`) |
| Range / multirange (operators, mapping) | yes | partial | `PostgresDialect.cs:262,143-148`; `SqlFunctions.Postgres.cs:738-1148` | type mapping only; no range operators in `PostgreSQLExtensions.cs` |
| Full-text `tsvector`/`tsquery` | yes | no | `PostgresDialect.cs:356,359,388`; `SqlFunctions.Postgres.cs:519-563` | no `to_tsvector`/`tsquery` API found in `PostgreSQLExtensions.cs` |
| Regex (`~`, `~*`, `regexp_replace`) | yes | no | `PostgresDialect.cs:476,479,483` | no regexp API found |
| JSONB + `jsonb_path_*` | yes | partial | `PostgresDialect.cs:305`; `SqlFunctions.Postgres.cs:105-270,227-245,700-704` | `json`/`jsonb` type mapping only; no `jsonb_path_*` API |
| Data-modifying CTE (`WITH x AS (INSERT ... RETURNING ...)`) | yes | no | `PostgresDialect.cs:34`; `DataContextExtensions.cs:1551`; `Builders/MutationCteQuery.cs` | no dedicated data-modifying-CTE API found |
| DML `RETURNING` (incl. update/delete join) | yes | yes | `PostgresDialect.cs:22,25,28` | generic `...WithOutput` (`LinqExtensions/LinqExtensions.{Insert,Update,Delete}.cs`) |
| `MERGE` (15+) | yes | yes | `PostgresDialect.cs:58,61,64,67,73` | `LinqExtensions/LinqExtensions.Merge.cs` (`IsUpsertWithMergeLoweringSupported = v15+`) |
| Upsert `ON CONFLICT` | yes | yes | `PostgresDialect.cs:37,52` | `IsInsertOrUpdateSupported` |
| Query hints `/*+ ... */` | yes | partial | `PostgresDialect.cs:189,192,195` | subquery table hints only (`PostgreSQLHints.cs`, `FOR UPDATE/SHARE/NOWAIT/SKIP LOCKED`) |
| CTAS / temporary / `ON COMMIT` / `WITH NO DATA` | yes | yes | `PostgresDialect.cs:519-549` | `DataExtensions.CreateTable`, `TempTable.cs` |
| Bulk copy (`COPY BINARY`) | yes | yes | `PostgresDialect.cs:40` | `PostgreSQLBulkCopy` |
| Sequences + runtime settings (`current_setting`/`set_config`) | yes | no | `SqlFunctions.Postgres.cs:500-510,615-625` | scalar surface is `Version`/`Current*` only (`PostgreSQLExtensions.cs`) |
| Boolean / bit / statistical / regression aggregates, ordered-set percentiles | yes | partial | `PostgresDialect.cs:400-408`; `SqlFunctions.Postgres.cs:565-613` | `ARRAY_AGG` + regression over window (`IsLinearRegressionSupported`) |
| Table-valued parameters (array / `jsonb_to_recordset`) | yes | no | `PostgresDialect.cs:49,277`; `SqlFunctions.Postgres.cs:716,728` | no TVP support (`SqlProviderFlags`) |
| Named windows / `GROUPS` / `EXCLUDE` | yes | yes | `PostgresDialect.cs:324,327,330` | `Sql.Window` (`WindowFunctions.FeatureMatrix.md`) |
| `DISTINCT ON`, `TABLESAMPLE`, `WITH TIES` | yes | no | `PostgresDialect.cs:365,374,513` | no dedicated API found |

Net: **nextorm leads** — full-text, regex, `jsonb_path_*`, range operators, data-modifying CTE,
sequences/settings, TVP and the aggregate families have dedicated nextorm APIs but no linq2db ones.

---

## SQL Server

| Feature | nextorm | linq2db | nextorm evidence | linq2db evidence |
|---|---|---|---|---|
| `OUTPUT` / `OUTPUT ... INTO` | yes | yes | `SqlServerDialect.cs:27,39`; `MakeOutputInto` | generic `InsertWithOutput` family; `Output*UseSpecialTable` flags |
| `MERGE` (branches, `BY SOURCE`) | yes | yes | `SqlServerDialect.cs:116-134` | `LinqExtensions.Merge.cs` (2008+) |
| Hints (table/query/join/tables-in-scope/index) | yes | yes (wider) | `SqlServerDialect.cs:258,265,291,298,675` | `SqlServerHints.cs` + generated; typed `WithIndex/WithForceSeek/OptionOptimizeFor/...` |
| Temporal `FOR SYSTEM_TIME` | yes | yes | `SqlServerDialect.cs:242,245` | `SqlServerHints.cs` (`TemporalTableAll/AsOf/FromTo/...`) |
| `FOR JSON` / `FOR XML` | yes | partial | `SqlServerDialect.cs:304,322`; `QueryCommand.TResult.cs:898,920` | no dedicated `FOR JSON`/`FOR XML` extension found |
| XML data-type methods (`.value`/`.query`/`.exist`/`.nodes`) | yes | partial | `SqlServerDialect.cs:229,756-764`; `SqlFunctions.SqlServer.cs:50-87` | built via `SqlFn.cs` / generic; no dedicated method-API grouping |
| `OPENJSON` / `STRING_SPLIT` / `CONTAINSTABLE` / `FREETEXTTABLE` | yes | yes | `SqlFunctions.SqlServer.cs:97,110,122,129` | `SqlServerExtensions.cs` (`CONTAINS`, `FREETEXT`); `SqlFn.cs` |
| Native `PIVOT` / `UNPIVOT` | yes | no | `SqlServerDialect.cs:248`; `EntityBuilder.cs:2010,2037` | no dedicated PIVOT/UNPIVOT API found |
| Full-text `contains` / `freetext` | yes | yes | `SqlServerDialect.cs:232,235` | `SqlServerExtensions.cs` (`CONTAINS`/`FREETEXT`, `CONTAINSTABLE`/`FREETEXTTABLE`) |
| Text JSON (`json_value`/`json_query`/`json_modify`/`isjson`) | yes | partial | `SqlServerDialect.cs:222,225` | JSON as text/native-type mapping; no dedicated JSON-function API |
| Regex (`REGEXP_LIKE`/`REGEXP_REPLACE`, 2025+) | yes | no | `SqlServerDialect.cs:436,439,443` | no regexp API found |
| Native TVP | yes | yes | `SqlServerDialect.cs:64` | `DataType.Structured` → `SqlDbType.Structured` |
| Bulk copy + `SqlBulkCopyOptions` | yes | yes | `SqlServerDialect.cs:55` | `SqlServerBulkCopy` |
| Stored procedures | yes | yes | `SqlServerDialect.cs:61` | `QueryProc`/`ExecuteProc` |
| Window percentiles (`WITHIN GROUP ... OVER`) | yes | partial | `SqlServerDialect.cs:363` | 2012+ windows; no `GROUPS`/`EXCLUDE`; `NTH_VALUE` absent |
| Provider scalar-function breadth | yes | yes (largest) | `SqlFunctions.SqlServer.cs` (328 lines) | `SqlFn.cs` 295 KB, 156 `[Sql.Function]` declarations |

Net: **parity, with linq2db ahead on scalar-function breadth and hint variety** — the residual scalar
gap is concentrated in T-SQL system/metadata functions, the date-part/`*FROMPARTS`/`SWITCHOFFSET` family
and `CHECKSUM`/`COMPRESS` (tracked as nextorm
[#182](https://github.com/AlexeyShirshov/nextorm/issues/182)); nextorm leads on first-class builders for
`PIVOT`/`UNPIVOT`, `FOR JSON`/`FOR XML`, XML methods, `OPENJSON`/`STRING_SPLIT`, and 2025 regex.

---

## MySQL / MariaDB

| Feature | nextorm | linq2db | nextorm evidence | linq2db evidence |
|---|---|---|---|---|
| Text JSON (`json_value`/`json_query`/`json_modify`/`isjson`) + mutation | yes | no | `MySqlDialect.cs:202,217,227`; `SqlFunctions.MySql.cs:99-143` | no JSON API found in `MySqlExtensions.cs` |
| Regex (`REGEXP_LIKE`/`REGEXP_REPLACE`) | yes | no | `MySqlDialect.cs:438,441,445` | no regexp API found |
| Full-text `MATCH ... AGAINST` | yes | yes | `MySqlDialect.cs:190,193` | `MySqlExtensions.cs` (only extension: `Match`/`MatchRelevance`) |
| Upsert `ON DUPLICATE KEY UPDATE` + `INSERT IGNORE` | yes | yes | `MySqlDialect.cs:96,98,60,63` | `IsInsertOrUpdateSupported` |
| Index hints `USE`/`FORCE`/`IGNORE INDEX` | yes | yes | `MySqlDialect.cs:495,576-593` | `MySqlHints.generated.cs` (`Table.*`, `Query.*`) |
| Query hints `/*+ ... */` | yes | yes | `MySqlDialect.cs:109,112,115` | `MySqlHints.cs` (`QueryBlockHint`, `SemiJoinHint`, ...) |
| DML `RETURNING` / `OUTPUT` | no | no | `SqlProviderFlags` defaults | output flags default false |
| `MERGE` | no | no | n/a | `IsUpsertWithMergeLoweringSupported = false` |
| CTAS / temporary / column list | yes | yes | `MySqlDialect.cs:501-507` | `DataExtensions.CreateTable`, `TempTable.cs` |
| Table-valued parameters (`JSON_TABLE`) | yes | no | `MySqlDialect.cs:57` | no TVP support |
| Window functions (`percent_rank`/`nth_value`) | yes | partial | `MySqlDialect.cs:176,179,182` | `IsWindowFunctionsSupported = Version >= MySql80` |
| Binary UUID helpers (`uuid_to_bin`/`bin_to_uuid`) | yes | no | `SqlFunctions.MySql.cs:99-143` | no API found |
| Provider scalar-function library | yes | partial (thin) | `SqlFunctions.MySql.cs` (257 lines) | `MySqlExtensions.cs` is 109 lines, FTS-only |
| **MariaDB** temporal `FOR SYSTEM_TIME` | yes | no | `MariaDbDialect.cs:53,56` | MariaDB modelled as a MySQL version, no separate extension surface |
| **MariaDB** sequences (`NEXT VALUE FOR`, `nextval`/`setval`/`lastval`) | yes | no | `SqlFunctions.MySql.cs:238-256`; `MariaDbDialect.cs:117-120` | no MariaDB-specific sequence API found |
| **MariaDB** `NVL`/`NVL2`/`ADD_MONTHS`/`MONTHS_BETWEEN`/`TO_CHAR`/`JSON_DETAILED`/`XXH3` | yes | no | `MariaDbDialect.cs:82-130`; `SqlFunctions.MySql.cs:145-232` | no dedicated MariaDB API found |
| **MariaDB** `INTERSECT ALL` / `EXCEPT ALL` | yes | partial | `MariaDbDialect.cs:34` | no per-provider gate found |

Net: **nextorm leads** — JSON, regex, UUID helpers, TVP and the MariaDB-specific families (temporal,
sequences, string/date functions, set-op `ALL`) have no linq2db provider API; linq2db is ahead only
on hint variety.

---

## SQLite

| Feature | nextorm | linq2db | nextorm evidence | linq2db evidence |
|---|---|---|---|---|
| LOB streaming via `rowid` locator | yes | no | `SqliteDialect.cs:15,18` | no LOB/Npgsql-style streaming API found |
| DML `RETURNING` | yes | yes | `SqliteDialect.cs:32` | `IsUpdateOutputRowsSupported = true` (3.35+) |
| Upsert `ON CONFLICT DO UPDATE` / `DO NOTHING` + `INSERT OR IGNORE` | yes | yes | `SqliteDialect.cs:83,88,90` | `IsInsertOrUpdateSupported` |
| Index hints `INDEXED BY` / `NOT INDEXED` | yes | yes | `SqliteDialect.cs:329,361-376` | `SQLiteHints.cs` (`IndexedByHint`, `NotIndexedHint`) |
| JSON1 (`json()`/`jsonb()`/`json_extract`, `->`/`->>`, `json_each`/`json_tree`) | yes | no | `SqlFunctions.Sqlite.cs:187-273` | no JSON API found in `SQLiteExtensions.cs` |
| Regex (`regexp`/`regexp_replace`) | yes | no | `SqliteDialect.cs:191,194`; `SQLiteFunctions.cs:42-46` | no regexp API found |
| Custom aggregates (`stdev`/`stdevp`/`var`/`varp`) | yes | no | `SQLiteFunctions.cs:23-37` | no statistical aggregate API |
| Named windows / `GROUPS` / `EXCLUDE`, `percent_rank`/`cume_dist`/`nth_value` | yes | partial | `SqliteDialect.cs:131,134,137,143,146` | `Sql.Window`; RANGE/GROUPS/EXCLUDE per matrix; no statistical |
| Table-valued parameters (`json_each`) | yes | no | `SqliteDialect.cs:27` | no TVP support |
| CTAS / temporary table | yes | yes | `SqliteDialect.cs:332,335` | `DataExtensions.CreateTable`, `TempTable.cs` |
| Full-text FTS3 / FTS4 / FTS5 | no | yes (extensive) | not overridden (default) | `SQLiteExtensions.cs` (900 lines: `FTS3Offsets/MatchInfo/Snippet/Optimize/...`, `FTS5bm25/Highlight/Snippet/CrisisMerge/...`, `Match`/`MatchTable`/`Rank`) |
| Provider scalar-function library | yes | no (FTS-only) | `SqlFunctions.Sqlite.cs` (274 lines) | `SQLiteExtensions.cs` contains no JSON/regex; FTS only |

Net: **mixed, net nextorm broader** — nextorm adds JSON1, regex, custom aggregates, window frame
`GROUPS`/`EXCLUDE`, TVP and LOB streaming; **linq2db leads on full-text** (FTS3/4/5 management and
ranking), where nextorm has no surface. Tracked as nextorm
[#181](https://github.com/AlexeyShirshov/nextorm/issues/181).

---

## ClickHouse

| Feature | nextorm | linq2db | nextorm evidence | linq2db evidence |
|---|---|---|---|---|
| String JSON (`JSONExtract*`/`visitParamExtract*`) + `JSONAllPaths`/`toJSONString` | yes | no | `ClickHouseDialect.cs:287,294`; `docs/providers/clickhouse.md:225` | no JSON API in `ClickHouseSpecificExtensions.cs` |
| Arrays + higher-order (lambda) functions | yes | partial | `ClickHouseDialect.cs:129,136,155`; `SqlFunctions.ClickHouse.cs:478-642` | array functions used only internally in the member translator, no public API |
| `ARRAY JOIN` clause | yes | no | `ClickHouseDialect.cs:145,461`; `Extensions/ClickHouseEntityBuilderExtensions.cs:33-51` | no public API found |
| Regex (`match`/`replaceRegexpAll`, RE2) | yes | partial | `ClickHouseDialect.cs:559,562,566`; `SqlFunctions.ClickHouse.cs:816-850` | internal `replaceRegexpAll` only |
| Join strictness `ANY`/`ALL`/`ASOF`, `GLOBAL`, `SEMI`/`ANTI`/`PASTE` | yes | partial | `ClickHouseDialect.cs:112,115,118,121,163` | `ClickHouseHints.generated.cs` join hints |
| `FINAL` / `SAMPLE` / `PREWHERE` / `SETTINGS` | yes | partial | `ClickHouseDialect.cs:452,455,458,464`; `Extensions/ClickHouseEntityBuilderExtensions.cs:15-120` | `ClickHouseHints` (`FinalHint`, `SettingsHint`) |
| `LIMIT n BY expr` | yes | no | `ClickHouseDialect.cs:449,795-804`; `Extensions/ClickHouseEntityBuilderExtensions.cs:57,63` | no `LIMIT BY` API found |
| `GROUP BY ... WITH TOTALS` | yes | no | `ClickHouseDialect.cs:388,391`; `Extensions/ClickHouseEntityBuilderExtensions.cs:69` | no API found |
| Distributed `GLOBAL IN` | yes | no | `ClickHouseDialect.cs:446`; `SqlFunctions.ClickHouse.cs:320-326` | no API found |
| Dictionaries (`dictGet`/`dictHas`/...) | yes | no | `ClickHouseDialect.cs:340,343`; `SqlFunctions.ClickHouse.cs:284-312` | no API found |
| `uniq`/quantile/top-K/sequence aggregates, `argMin`/`argMax`, `-If` combinator, bitmap/`sumMap` | yes | partial | `ClickHouseDialect.cs:237-257,368`; `SqlFunctions.ClickHouse.cs:41-154,957-985` | thin; `MEDIAN` rejected, `STDDEV_SAMP`/`VAR_SAMP` mapped only |
| `lagInFrame`/`leadInFrame` frame functions | yes | no | `ClickHouseDialect.cs:269`; `SqlFunctions.ClickHouse.cs:766-786` | no public API found |
| Named windows / frame `GROUPS`, `percent_rank`/`cume_dist`/`nth_value` | yes | partial | `ClickHouseDialect.cs:260,263,272,281` | partial (no `CUME_DIST`/`GROUPS`/`EXCLUDE`/percentile per matrix) |
| Table functions `numbers`/`zeros`/`generateRandom`/`values`/`url`/`s3`/`file`/`remote*`/`cluster*` | yes (declared) | no | `ClickHouseDialect.cs:399-401,423`; `SqlFunctions.ClickHouse.cs:335-471` | no table-function API found |
| Date conversion surface (`toDate`/`toStartOf*`/`toYYYYMM`/...) | yes | partial | `ClickHouseDialect.cs:223,893-928`; `SqlFunctions.ClickHouse.cs:649-721` | date/math/string translators only |
| Map / hash / `generateULID` scalars | yes | no | `SqlFunctions.ClickHouse.cs:918-1035` | no API found |
| CTAS `ENGINE = MergeTree ... AS SELECT` | yes | yes | `ClickHouseDialect.cs:92,98,101` | `DataExtensions.CreateTable` |
| DML mutations (`ALTER TABLE ... UPDATE`/`DELETE`) | yes | no | `ClickHouseDialect.cs:67-83` | not exposed as dedicated API found |
| TVP (`Array(T)`/`Array(Tuple)` + `arrayJoin`) | yes | no | `ClickHouseDialect.cs:48` | no TVP support |
| Stored procedures / bulk copy / transactions / LOB streaming | no | no | `ClickHouseDialect.cs:56,377`; `docs/providers/clickhouse.md:236` | same engine limits |

Net: **nextorm leads decisively** — linq2db's public ClickHouse-specific surface is essentially
`AsClickHouse` (`ClickHouseSpecificExtensions.cs`) plus hints (`ClickHouseHints.cs`/generated), while
nextorm exposes the clause set, aggregate families, dictionaries, table functions and scalar library.

---

## Net assessment

Across the six shared engines, nextorm exposes a dedicated provider-specific API on a wider set of
surfaces. The strongest nextorm-only areas, with no linq2db equivalent found:

- A per-provider scalar-function library for **all** providers, gated by `ISqlDialect.<Provider>Functions`:
  `SqlFunctions.{Postgres,SqlServer,MySql,Sqlite,ClickHouse}.cs` (1036/328/257/274/1149 lines).
- PostgreSQL: full-text, regex, `jsonb_path_*`, range operators, data-modifying CTE, sequences/settings,
  boolean/bit/statistical/regression aggregates, ordered-set percentiles, TVP.
- MySQL/MariaDB: JSON, regex, binary UUID, and the whole MariaDB-specific family (temporal, sequences,
  `NVL`/`ADD_MONTHS`/`TO_CHAR`/`JSON_DETAILED`/`XXH3`, set-op `ALL`).
- SQLite: JSON1, regex, custom aggregates, window frame `GROUPS`/`EXCLUDE`, TVP, `rowid` LOB streaming.
- ClickHouse: the clause set (`FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS`/`LIMIT BY`/`ARRAY JOIN`/`WITH TOTALS`/
  `GLOBAL IN`), aggregate families with `-If` combinators, dictionaries, table functions, scalar library.
- First-class fluent builders wired to the query builder (`OUTPUT INTO`, `MERGE`, `PIVOT`/`UNPIVOT`,
  `FOR JSON`/`FOR XML`, temporal, `WITH TOTALS`) rather than raw SQL.

Where linq2db leads or matches:

- SQL Server scalar-function breadth (`SqlFn.cs`, 156 `[Sql.Function]` declarations) and hint variety.
- SQLite full-text (FTS3/4/5), where nextorm has no surface.
- A broader engine list (Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, DuckDB, Ydb).

Parity (both, engine permitting): core DML `RETURNING`/`OUTPUT`/`MERGE`/upsert, bulk copy, stored
procedures, generic CTAS/temp tables, index/query hints, named windows.

## Caveats

- linq2db "no" entries are findings from searching its provider-specific extension files, not proof of
  impossibility: any construct remains expressible via raw SQL / `Sql.Ext` / `Sql.Function`. The claim
  is about dedicated API width.
- Some nextorm entries (distributed ClickHouse table functions) are declared and gated but not exercised
  end-to-end by SQL-generation tests; `docs/guide/provider-specific/clickhouse.md:258-260` marks the
  distributed ones out of scope.
- linq2db is pinned to `master` at commit `5dfadd7eccb99fe18b6b2195897d21103f897eba`; its provider
  surfaces evolve.
- RU mirror of this spec is not yet written.

## Sources

- nextorm: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `src/nextorm.<provider>/*Dialect.cs`,
  `src/nextorm.core/Query/SqlFunctions.*.cs`, `src/nextorm.sqlite/SQLiteFunctions.cs`, `docs/providers/*.md`,
  `docs/guide/provider-specific/*`.
- linq2db: `Source/LinqToDB/DataProvider/<Provider>/*.cs`, `Source/LinqToDB/LinqExtensions/*.cs`,
  `Source/LinqToDB/Sql/WindowFunctions.FeatureMatrix.md`, `Source/LinqToDB/ProviderName.cs`.
- Raw nextorm evidence: `docs/specs/comparison/evidence-01-provider-specific-features.md`.
