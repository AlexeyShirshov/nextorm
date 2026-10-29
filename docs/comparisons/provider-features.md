# Provider features: Nextorm vs linq2db

A dated, per-engine comparison of the **provider-specific API surface**: for each SQL engine both libraries
support, how much of that engine's own feature set is exposed through a dedicated API wired to the query
builder. Marks are judgement calls against the public documentation and source of both libraries, as of
**2026-10-04**.

This page answers a narrower question than [Capabilities](capabilities.md). Capabilities asks whether a
construct can be expressed; this page asks whether there is a **dedicated provider API** for it. A `no` means
no dedicated API was found — the construct may still be reachable through raw SQL (or, for linq2db, through
`Sql.Ext`/`Sql.Function`).

## How to read the marks

- **yes** — a first-class provider API is present.
- **partial** — present with a named limitation, or reachable only through a generic mechanism.
- **no** — no dedicated provider API found.
- **n/a** — the feature does not exist on that engine.

Each table ends with a **Total** row: the yes / partial / no counts per library (`n/a` is excluded).

## Provider footprint

| Engine | Nextorm | linq2db |
|---|---|---|
| SQLite | yes | yes |
| SQL Server | yes | yes |
| PostgreSQL | yes | yes |
| MySQL | yes (own provider) | yes |
| MariaDB | yes (own provider) | partial (shared MySQL provider, `MySqlVersion.MariaDB10`; no separate extension surface) |
| ClickHouse | yes | yes |
| In-memory object context | yes | no |
| Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, DuckDB, Ydb | no | yes |
| **Total** | 7 / 0 / 1 | 6 / 1 / 1 |

linq2db does not ship an in-memory object context. Nextorm splits MySQL and MariaDB into separate providers;
linq2db models MariaDB as a MySQL version.

## Key finding

**Nextorm covers provider-specific features more broadly.** Across the six SQL engines both libraries
support, Nextorm exposes a dedicated provider-specific API on a wider set of surfaces than linq2db: a
per-provider scalar-function library for every engine, the ClickHouse
clause/aggregate/table-function/tuple/native-JSON surface, PostgreSQL
full-text/regex/`jsonb_path_*`/range/data-modifying CTE/table functions, SQL Server sequences and row locking,
MySQL/MariaDB JSON/regex/sequences, and SQLite JSON1/regex/aggregates/LOB/FTS5 maintenance — all wired to the
query builder rather than raw SQL.

This is a statement about **per-engine feature depth, not the number of engines**: linq2db connects to more
database products (Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, DuckDB, Ydb), while
Nextorm adds an in-memory object context and splits MySQL/MariaDB into separate providers. linq2db remains
wider on SQL Server scalar-function breadth and hint variety (issue [#182](https://github.com/AlexeyShirshov/nextorm/issues/182)).

## PostgreSQL

| Feature | Nextorm | linq2db |
|---|---|---|
| Arrays + `any`/`all` quantifiers | yes | yes |
| Range / multirange (operators, mapping) | yes | partial (type mapping only) |
| Full-text `tsvector`/`tsquery` | yes | no |
| Regex (`~`, `~*`, `regexp_replace`) | yes | no |
| JSONB + `jsonb_path_*` | yes | partial (type mapping only) |
| Data-modifying CTE (`WITH x AS (INSERT ... RETURNING ...)`) | yes | no |
| DML `RETURNING` (including update/delete join) | yes | yes |
| `MERGE` (15+) | yes | yes |
| Upsert `ON CONFLICT` | yes | yes |
| Query hints `/*+ ... */` | yes | partial (subquery table hints and locking only) |
| CTAS / temporary / `ON COMMIT` / `WITH NO DATA` | yes | yes |
| Bulk copy (`COPY BINARY`) | yes | yes |
| Sequences + runtime settings (`current_setting`/`set_config`) | yes | no |
| Boolean / bit / statistical / regression aggregates, ordered-set percentiles | yes | partial |
| Table-valued parameters (array / `jsonb_to_recordset`) | yes | no |
| Named windows / `GROUPS` / `EXCLUDE` | yes | yes |
| `DISTINCT ON`, `TABLESAMPLE`, `WITH TIES` | yes | no |
| Table functions / set-returning functions (`generate_series`, `unnest`, `regexp_matches`, `jsonb_*`, `ts_stat`, ...) | yes | partial (`unnest`/`generate_series` only) |
| Row locking (`FOR UPDATE` / `FOR SHARE`, per-source `OF`, wait modes) | yes | yes |
| Session/server identity (`pg_backend_pid`, `inet_*`) | yes | no |
| **Total** | 20 / 0 / 0 | 8 / 5 / 7 |

Net: **Nextorm leads** — full-text, regex, `jsonb_path_*`, range operators, data-modifying CTE,
sequences/settings, table-valued parameters and the aggregate families have dedicated Nextorm APIs but no
linq2db ones.

## SQL Server

| Feature | Nextorm | linq2db |
|---|---|---|
| `OUTPUT` / `OUTPUT ... INTO` | yes | yes |
| `MERGE` (branches, `BY SOURCE`) | yes | yes |
| Hints (table / query / join / tables-in-scope / index) | yes | yes (wider) |
| Temporal `FOR SYSTEM_TIME` | yes | yes |
| `FOR JSON` / `FOR XML` | yes | partial |
| XML data-type methods (`.value`/`.query`/`.exist`/`.nodes`) | yes | partial |
| `OPENJSON` / `STRING_SPLIT` / `CONTAINSTABLE` / `FREETEXTTABLE` | yes | yes |
| Native `PIVOT` / `UNPIVOT` | yes | no |
| Full-text `contains` / `freetext` | yes | yes |
| Text JSON (`json_value`/`json_query`/`json_modify`/`isjson`) | yes | partial |
| Regex (`REGEXP_LIKE`/`REGEXP_REPLACE`, 2025+) | yes | no |
| Native table-valued parameters | yes | yes |
| Bulk copy + `SqlBulkCopyOptions` | yes | yes |
| Stored procedures | yes | yes |
| Window percentiles (`WITHIN GROUP ... OVER`) | yes | partial |
| Provider scalar-function breadth | yes | yes |
| Sequences (`NEXT VALUE FOR`) | yes | yes |
| Row locking (`ForUpdate`/`ForShare`, `LockWaitMode` → `with (updlock)`/`with (holdlock)`) | yes | yes |
| Session/server identity (`@@SERVERNAME`, `HOST_NAME`, `APP_NAME`, `SUSER_NAME`, `ORIGINAL_LOGIN`) | yes | yes |
| **Total** | 19 / 0 / 0 | 13 / 4 / 2 |

Net: **parity on the audited T-SQL scalar catalogue, with linq2db still wider overall**. Issue
[#182](https://github.com/AlexeyShirshov/nextorm/issues/182) reconciled 61 pinned linq2db `SqlFn.cs` entries
against Nextorm: 51 covered and 10 excluded, where the excluded set is live, mutable
connection/session/statement-scope state rather than per-row query scalars. The identity subset
(`@@SERVERNAME`, `HOST_ID`, `HOST_NAME`, `APP_NAME`, `SUSER_NAME`, `ORIGINAL_LOGIN`) shipped in
[#219](https://github.com/AlexeyShirshov/nextorm/issues/219), leaving the rest tracked in
[#248](https://github.com/AlexeyShirshov/nextorm/issues/248). Nextorm leads on first-class builders for
`PIVOT`/`UNPIVOT`, `FOR JSON`/`FOR XML`, XML methods, `OPENJSON`/`STRING_SPLIT` and the 2025 regex functions.

## MySQL / MariaDB

| Feature | Nextorm | linq2db |
|---|---|---|
| Text JSON (`json_value`/`json_query`/`json_modify`/`isjson`) + mutation | yes | no |
| Regex (`REGEXP_LIKE`/`REGEXP_REPLACE`) | yes | no |
| Full-text `MATCH ... AGAINST` | yes | yes |
| Upsert `ON DUPLICATE KEY UPDATE` + `INSERT IGNORE` | yes | yes |
| Index hints `USE`/`FORCE`/`IGNORE INDEX` | yes | yes |
| Query hints `/*+ ... */` | yes | yes |
| DML `RETURNING` / `OUTPUT` | partial (MariaDB `UPDATE ... RETURNING`, 13.0+; MySQL none) | no |
| Full `MERGE` statement (branches, `BY SOURCE`) | no (engine; the key-upsert `Merge()` lowers to `ON DUPLICATE KEY UPDATE`) | no (engine) |
| CTAS / temporary / column list | yes | yes |
| Table-valued parameters (`JSON_TABLE`) | yes | no |
| Window functions (`percent_rank`/`nth_value`) | yes | partial (MySQL 8+) |
| Binary UUID helpers (`uuid_to_bin`/`bin_to_uuid`) | yes | no |
| Provider scalar-function library | yes | partial (thin; full-text only) |
| **MariaDB** temporal `FOR SYSTEM_TIME` | yes | no |
| **MariaDB** sequences (`NEXT VALUE FOR`, `nextval`/`setval`/`lastval`) | yes | no |
| **MariaDB** `NVL`/`NVL2`/`ADD_MONTHS`/`MONTHS_BETWEEN`/`TO_CHAR`/`JSON_DETAILED`/`XXH3` | yes | no |
| **MariaDB** `INTERSECT ALL` / `EXCEPT ALL` | yes | partial |
| Arbitrary-value aggregate (`any` → `ANY_VALUE(x)` on MySQL; MariaDB rejects) | yes | no |
| Row locking (`FOR UPDATE`/`FOR SHARE`; MariaDB source-level `OF` unsupported by design, fail-closed) | partial | yes |
| Session/server identity (`CONNECTION_ID()`, `USER()`) | yes | no |
| **Total** | 17 / 2 / 1 | 6 / 3 / 11 |

Net: **Nextorm leads** — JSON, regex, UUID helpers, table-valued parameters and the MariaDB-specific families
(temporal, sequences, string/date functions, set-operation `ALL`) have no linq2db provider API; linq2db is
ahead only on hint variety.

## SQLite

| Feature | Nextorm | linq2db |
|---|---|---|
| LOB streaming via `rowid` locator | yes | no |
| DML `RETURNING` | yes | yes |
| Upsert `ON CONFLICT DO UPDATE` / `DO NOTHING` + `INSERT OR IGNORE` | yes | yes |
| Index hints `INDEXED BY` / `NOT INDEXED` | yes | yes |
| JSON1 (`json()`/`jsonb()`/`json_extract`, `->`/`->>`, `json_each`/`json_tree`) | yes | no |
| Regex (`regexp`/`regexp_replace`) | yes | no |
| Custom aggregates (`stdev`/`stdevp`/`var`/`varp`) | yes | no |
| Named windows / `GROUPS` / `EXCLUDE`, `percent_rank`/`cume_dist`/`nth_value` | yes | partial |
| Table-valued parameters (`json_each`) | yes | no |
| CTAS / temporary table | yes | yes |
| Full-text query + `FROM` (FTS3 / FTS4 / FTS5) | yes | yes (extensive) |
| FTS5 maintenance/control (`automerge`/`crisismerge`/`merge`/`optimize`/`rebuild`/`integrity-check`) | yes | yes (also FTS3/FTS4) |
| Provider scalar-function library | yes | no (full-text only) |
| Session/server identity | no (fails closed; only `version()` is rendered) | no |
| **Total** | 13 / 0 / 1 | 6 / 1 / 7 |

Net: **mixed, net Nextorm broader** — Nextorm adds JSON1, regex, custom aggregates, window frame
`GROUPS`/`EXCLUDE`, table-valued parameters, LOB streaming, the SQLite full-text **query + FROM** surface
([#181](https://github.com/AlexeyShirshov/nextorm/issues/181)) and the **FTS5 maintenance/control** command
builder ([#195](https://github.com/AlexeyShirshov/nextorm/issues/195)/[#196](https://github.com/AlexeyShirshov/nextorm/issues/196)).
linq2db still leads only on FTS3/FTS4 maintenance and the FTS5 `delete`/`delete-all`/`pgsz`/`usermerge`
commands.

## ClickHouse

| Feature | Nextorm | linq2db |
|---|---|---|
| String JSON (`JSONExtract*`/`visitParamExtract*`, `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS`) + `JSONAllPaths`/`toJSONString` | yes | no |
| Arrays + higher-order (lambda) functions | yes | partial |
| `ARRAY JOIN` clause | yes | no |
| Regex (`match`/`replaceRegexpAll`, RE2) | yes | partial |
| Join strictness `ANY`/`ALL`/`ASOF`, `GLOBAL`, `SEMI`/`ANTI`/`PASTE` | yes | partial |
| `FINAL` / `SAMPLE` / `PREWHERE` / `SETTINGS` | yes | partial |
| `LIMIT n BY expr` | yes | no |
| `GROUP BY ... WITH TOTALS` | yes | no |
| Distributed `GLOBAL IN` | yes | no |
| Dictionaries (`dictGet`/`dictHas`/...) | yes | no |
| `uniq`/quantile/top-K/sequence aggregates, `argMin`/`argMax`, `-If` combinator, bitmap/`sumMap` | yes | partial |
| `lagInFrame`/`leadInFrame` frame functions | yes | no |
| Named windows / frame `GROUPS`, `percent_rank`/`cume_dist`/`nth_value` | yes | partial |
| Table functions (`numbers`/`zeros`/`generateRandom`/`values`/`url`/`s3`/`file`/`remote*`/`cluster*`) | yes (declared) | no |
| Date conversion surface (`toDate`/`toStartOf*`/`toYYYYMM`/...) | yes | partial |
| Map / hash / `generateULID` scalars | yes | no |
| CTAS `ENGINE = MergeTree ... AS SELECT` | yes | yes |
| DML mutations (`ALTER TABLE ... UPDATE`/`DELETE`) | yes | no |
| Table-valued parameters (`Array(T)`/`Array(Tuple)` + `arrayJoin`) | yes | no |
| Stored procedures / bulk copy / transactions / LOB streaming | no (engine) | no (engine) |
| Tuple surface (`tuple(...)`, `tupleElement`, arity 1-7) | yes | no |
| Native JSON column type (`[JsonColumn]`, `JsonObject`/`JsonDocument`/`JsonElement`) | yes | no |
| `any`/`anyLast` overloads + `multi_if` (multi-branch conditional) | yes | no |
| Session/server identity (`connectionId()`, `hostName()`, `tcpPort()`) | yes | no |
| **Total** | 23 / 0 / 1 | 1 / 7 / 16 |

Net: **Nextorm leads decisively** — linq2db's public ClickHouse-specific surface is essentially `AsClickHouse`
plus hints, while Nextorm exposes the clause set, aggregate families, dictionaries, table functions and the
scalar library.

## Net assessment

Across the six shared engines, Nextorm exposes a dedicated provider-specific API on a wider set of surfaces.
The strongest Nextorm-only areas, with no linq2db equivalent found:

- A per-provider scalar-function library for **all** providers: `SqlFunctions.Postgres`,
  `SqlFunctions.SqlServer`, `SqlFunctions.MySql`, `SqlFunctions.Sqlite` and `SqlFunctions.ClickHouse`, plus the
  shared session-info gate that carries the identity scalars of
  [#219](https://github.com/AlexeyShirshov/nextorm/issues/219) across four providers.
- PostgreSQL: full-text, regex, `jsonb_path_*`, range operators, data-modifying CTE, table functions
  (`generate_series`/`unnest`/`jsonb_*`/`ts_stat`), sequences/settings, boolean/bit/statistical/regression
  aggregates, ordered-set percentiles, table-valued parameters.
- MySQL/MariaDB: JSON, regex, binary UUID, the arbitrary-value aggregate (`any` → `ANY_VALUE`), and the whole
  MariaDB-specific family (temporal, sequences, `NVL`/`ADD_MONTHS`/`TO_CHAR`/`JSON_DETAILED`/`XXH3`,
  set-operation `ALL`).
- SQLite: JSON1, regex, custom aggregates, window frame `GROUPS`/`EXCLUDE`, table-valued parameters,
  `rowid` LOB streaming, FTS5 maintenance/control.
- ClickHouse: the clause set (`FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS`/`LIMIT BY`/`ARRAY JOIN`/`WITH TOTALS`/
  `GLOBAL IN`), aggregate families with `-If` combinators and `any`/`anyLast`, dictionaries, table functions,
  the tuple surface, native `JSON` columns, `multi_if`, the scalar library.
- First-class fluent builders wired to the query builder (`OUTPUT INTO`, `MERGE`, `PIVOT`/`UNPIVOT`,
  `FOR JSON`/`FOR XML`, temporal, `WITH TOTALS`) rather than raw SQL.

Where linq2db leads or matches:

- SQL Server scalar-function breadth and hint variety.
- SQLite full-text **FTS3/FTS4 maintenance** and the FTS5 `delete`/`delete-all`/`pgsz`/`usermerge` commands;
  Nextorm ships the **query + FROM** surface and the **FTS5 maintenance/control** command builder.
- A broader engine list (Oracle, Firebird, DB2, SAP HANA, Informix, Sybase, Access, SQL CE, DuckDB, Ydb).

Parity (both, engine permitting): core DML `RETURNING`/`OUTPUT`/`MERGE`/upsert, bulk copy, stored procedures,
sequences, row locking, generic CTAS/temporary tables, index/query hints, named windows.

## Caveats

- A `no` is a finding from searching each library's provider-specific extension surface, not proof of
  impossibility: the construct remains expressible through raw SQL (or `Sql.Ext`/`Sql.Function` in linq2db).
  The claim is about the width of the **dedicated** provider API.
- Some Nextorm entries (distributed ClickHouse table functions) are declared and gated but intentionally out of
  scope end-to-end — see the [ClickHouse provider](../connecting-providers/clickhouse.md).
- linq2db's provider surfaces evolve; the marks are dated **2026-10-04**.
- MySQL and MariaDB are separate Nextorm providers but a single linq2db provider, so the MySQL/MariaDB table
  folds both libraries' shapes into one list.

## See also

- [Capabilities](capabilities.md) — the construct-level matrix across Dapper, linq2db and EF Core.
- [Benchmarks](benchmarks.md) and [Feature benchmarks (2026-10-17)](feature-benchmarks-2026-10-17.md).
- [Provider overview](../connecting-providers/overview.md).
- [Provider-specific SQL](../guide/provider-specific-sql/overview.md).
- [Scalar functions](../scalar-functions/index.md).
- [Limitations and out-of-scope features](../advanced/limitations.md).
