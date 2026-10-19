# Bulk insert: provider-native options and paths (#305)

> **Status: written design, awaiting user approval.**
>
> Requirements and design were discussed with the user in chat. This document records that
> discussion; it does **not** record written approval, and no implementation permission has been
> granted. The next artifact after approval is an implementation/test plan, not execution. No
> product code, tests, or commits are part of this document.
>
> A fresh independent builder check has completed. Its significant compatibility question was
> answered by the user with **YES**: besides the timeout breaking change, **stricter validation of
> existing previously ignored unsupported options is explicitly authorized** (see
> [§8.2](#82-rest-of-the-surface)). Remaining builder-check gaps were settled by the architect calls
> recorded in [§14](#14-independent-review-and-approval-status); they are architect decisions, not
> separate explicit user votes.
>
> The issue's original extend-only criterion is **superseded for two explicitly authorized
> compatibility changes**: the timeout API replacement and the stricter validation of previously
> ignored unsupported options. The rest of the public surface stays extend-only; see
> [§8](#8-compatibility-and-breaking-change).

| Field | Value |
|---|---|
| Task | [#305](https://github.com/AlexeyShirshov/nextorm/issues/305) — close the residual linq2db `BulkCopyOptions` surface |
| Milestone | `1.0.9-rc4` ([#22](https://github.com/AlexeyShirshov/nextorm/milestone/22)), verified **OPEN** |
| Related | [#92](https://github.com/AlexeyShirshov/nextorm/issues/92) — closed baseline bulk-copy parity issue; relation retained |
| Stage | written design; independent builder check complete; awaiting user approval |
| Approval | requirements/design discussed in chat; independent builder check completed; compatibility question answered YES; no separate GitHub approval or comment exists |
| Implementation permission | **none** |
| Next artifact | implementation/test plan after approval |

## 0. How to read this document

- **FACT** — observed in the repository or in accessible upstream documentation/source at the cited
  location at the time of writing. Facts are not promises about future driver versions.
- **DESIGN** — behavior agreed for this task. It is the target contract, not shipped code.
- **ARCHITECT CALL** — a recommendation made by the architect. It is not a separate user promise,
  and it does not widen the agreed scope.
- **PLANNING** — an exact name, grouping, adapter, or verification artifact deliberately left to
  the implementation/test plan. The design constrains it; the plan chooses it.

The design is intentionally about **capabilities the providers actually support**, not about
finding a use case or cloning linq2db. Provider-specific public options are accepted. Native paths
are added wherever real native driver support exists. Every option and targeting facility that is
not implemented must fail explicitly or be documented as out of scope; silent ignoring is not an
acceptable outcome.

## 1. Context

### 1.1 What exists today

- **FACT:** `BulkInsertOptions` and `BulkInsertOptionsBuilder` are the typed public option model
  (`src/nextorm.core/Builders/BulkInsertOptions.cs:9-146` and `:152-353`). The record exposes
  `MaxBatchSize`, `MaxParameters`, `MaxSqlLength`, `KeepIdentity`, `IgnoreDuplicates`,
  `CheckConstraints`, `TableLock`, `KeepNulls`, `FireTriggers`, `TableName`, `TableSchema`,
  `TimeoutSeconds`, `Progress`, `ProgressCancellationTokenSource`, and `NotifyEvery`.
- **FACT:** `TimeoutSeconds` is an `int?` and the builder entry point is `Timeout(int seconds)`
  (`BulkInsertOptions.cs:92`, `:290`). The value is passed straight through the bulk builder
  (`src/nextorm.core/Builders/BulkInsertBuilder.cs:410`).
- **FACT:** SQL Server declares a native bulk path (`src/nextorm.sqlserver/SqlServerDialect.cs:55`)
  and uses `SqlBulkCopy`; its timeout is set as `BulkCopyTimeout = commandTimeoutSeconds ?? 30`
  (`src/nextorm.sqlserver/SqlServerDataContext.cs:682-690`).
- **FACT:** PostgreSQL declares a native bulk path (`src/nextorm.postgres/PostgresDialect.cs:79`)
  and currently rejects a timeout before `COPY` (`PostgresDataContext.cs:346-418`,
  `EnsureCopyTimeoutSupported`). PostgreSQL's timeout is therefore blocked today.
- **FACT:** MySQL declares no native bulk path (`src/nextorm.mysql/MySqlDialect.cs:48`).
- **FACT:** ClickHouse declares no native bulk path (`src/nextorm.clickhouse/ClickHouseDialect.cs:65-70`).
  The current comment is explicitly based on the obsolete `ClickHouseBulkCopy` wrapper versus
  `ClickHouseClient.InsertBinaryAsync`; that blanket limitation is superseded by this design once
  the native path exists.
- **FACT:** package versions are centrally pinned in `Directory.Packages.props`:
  `ClickHouse.Driver 1.4.0` (line 9), `Microsoft.Data.SqlClient 6.1.7` (line 17),
  `Microsoft.Data.Sqlite 10.0.12` (line 19), `MySqlConnector 2.6.2` (line 30),
  `Npgsql 10.0.3` (line 31).
- **FACT:** the user-facing bulk guide is `docs/guide/22-bulk-insert.md` (EN) and
  `docs/ru/guide/22-bulk-insert.md` (RU); limitations are tracked in
  `docs/advanced/limitations.md` and `docs/ru/advanced/limitations.md`; provider pages live under
  `docs/connecting-providers/` and `docs/ru/connecting-providers/`. The internal gap row is
  `docs/specs/comparison/linq2db-backlog-gap-analysis.md` (G30).

### 1.2 The residual problem

Issue #305 lists seven residual linq2db `BulkCopyOptions` members, plus the ClickHouse
parallelism/session members that were previously deferred as driver-blocked. In addition, native
MySQL/MariaDB and ClickHouse paths are missing even though the pinned drivers expose native bulk
APIs. The task is to close the residual surface with a coherent option model and explicit
failure behavior — not to expose every property of every driver.

## 2. Intent and non-goals

**Intent (DESIGN):** expose capabilities actually supported by the provider drivers; accept
provider-specific public options; add native paths where real native driver support exists.

**In scope (DESIGN):**

- the seven residual items from #305;
- missing native MySQL/MariaDB and ClickHouse integration;
- timeout support on PostgreSQL and on the portable path;
- SQL Server and PostgreSQL native paths retained;
- SQLite portable semantics (SQLite has no special native bulk-copy API);
- provider-specific typed options needed for the above;
- destination qualification and existing temporary-target support as described below;
- stricter validation, explicitly authorized by the user, of existing options that were previously
  silently ignored when unsupported by the provider or path (see [§8.2](#82-rest-of-the-surface));
- EN/RU user documentation and the internal gap analysis.

**Out of scope (DESIGN):**

- wholesale exposure of every driver property; additional driver knobs outside this scope are
  separate tasks;
- new table DDL APIs (the bulk API must not create/drop tables);
- implicit connection rerouting or implicit security-setting changes (notably MySQL
  `AllowLoadLocalInfile` / server `local_infile`);
- a new transaction abstraction;
- exposing arbitrary `QueryOptions`/driver settings through the bulk options;
- promising identical progress granularity or throughput gains across drivers;
- unrelated refactoring.

**ARCHITECT CALL:** preserve existing progress callbacks, user cancellation, supported input
shapes, and the bounded streaming adaptation. Do not promise identical progress granularity or
throughput gains.

## 3. Execution modes

**DESIGN:** the public model gains three execution modes:

| Mode | Contract |
|---|---|
| `Auto` | **Default mode for every provider.** Prefer a native path when it is compatible with every requested option and every preflight prerequisite. Fall back to the portable SQL batch path only when the portable path can express **all** requested options. If neither path can satisfy the request, fail explicitly before consuming input or sending writes where determinable. |
| `Native` | Use the provider native path. Never silently fall back. A missing prerequisite or an unsupported option/path combination throws `NotSupportedException` before reading input or sending writes where determinable. |
| `Portable` | Explicitly choose the portable SQL batch path. If a requested option is native-only and cannot be expressed portably, throw `NotSupportedException`; never ignore it. |

Additional mode rules (DESIGN):

- **`Auto` is the default for all providers, including the new native paths.** Existing
  MySQL/MariaDB and ClickHouse calls that effectively used the portable path may switch to the
  native path when preflight-compatible. This is an intended **behavior shift** and must be called
  out in EN/RU release documentation; callers who need the old path pin `Portable` explicitly.
  PostgreSQL and SQL Server keep their existing native `Auto` behavior. SQLite remains portable
  because no native path exists.
- **Strict `Native` failure stays as agreed:** `Native` never falls back, and a missing prerequisite
  or an option/path combination that the native path cannot honor produces an actionable
  `NotSupportedException` before writes where determinable.
- **No fallback after native work starts.** A native runtime failure, including a write failure, is
  propagated with its original error. There is no replay, no duplicate retry, and no second attempt
  through the portable path.
- **No silent fallback in `Auto`** merely because a native path is difficult; `Auto` chooses
  portable only under the expressibility rule above.
- **Validation taxonomy:** unsupported option/path combinations throw `NotSupportedException`;
  invalid values (for example a non-positive timeout) throw `ArgumentException` subclasses
  (`ArgumentOutOfRangeException` / `ArgumentException`).
- **Existing native paths retained:** SQL Server `SqlBulkCopy` and PostgreSQL `COPY` remain native.
  SQLite remains portable and does not get a fabricated "native" mode.

The exact enum/type name for the mode is **PLANNING**, constrained by the semantics above.

## 4. Option model and provider-specific options

**DESIGN:** the general option model remains the typed public `BulkInsertOptions` /
`BulkInsertOptionsBuilder` surface. Provider-specific options are added as typed groups or typed
extensions — never as a `Dictionary<string, object>`, raw driver flags, or an untyped escape hatch.
The exact names and grouping are **PLANNING**, provided the plan publishes an explicit availability
and validation matrix.

Rules (DESIGN):

- Existing non-timeout public members are preserved (see [§8](#8-compatibility-and-breaking-change)).
- A provider-specific option is meaningful only on its provider/path. Requesting it on another
  provider, or on a mode-selected portable path that cannot express it, throws
  `NotSupportedException`; it is never silently ignored.
- Native interactions with `KeepIdentity`, `IgnoreDuplicates`, and `Returning*` preserve the
  existing capabilities: `Auto` may choose portable only when every requested option is expressible
  there; `Native` rejects a combination it cannot implement.
- `Returning*` still forces the portable path where the existing design does so; the mode selector
  must not pretend a native path can return rows it cannot.
- The availability/validation matrix must state, per provider and per mode, which options are
  supported, which are rejected, and why. This matrix is part of the plan, not an implicit promise.

No new general options beyond the timeout replacement and the provider-specific groups required by
this scope.

### 4.1 Option-value semantics (explicit `false`, defaults, and limits)

**ARCHITECT CALL (builder-check resolution, not a separate explicit user vote):**

- **An explicitly requested `false` is a real requested value**, not automatically equivalent to
  "absent". Where absence and `false` differ, provider options are tri-state/nullable so the plan can
  distinguish them. Neither `Auto` nor `Native` may erase a requested `false` and proceed.
- **Actual default-equivalent values are honored.** Availability is decided by the **semantic
  outcome** that the request demands, not by whether the driver happens to expose a property. For
  example, `FireTriggers(false)` on a path whose driver flag is absent produces exactly the
  requested non-firing semantics and is therefore honored, not rejected.
- **An option whose requested semantic outcome cannot be produced is unsupported.** For example,
  `CheckConstraints(false)` where the server inevitably checks constraints is a meaningful request
  that cannot be honored: it fails preflight with `NotSupportedException`. Under `Auto`, another
  compatible path may be selected instead; the requested `false` is never discarded.
- The availability/validation matrix must contain explicit rows for **explicit `false`** and for
  **default-equivalent** values, and tests must prove both the honored and rejected cases. The
  matrix covers only options in this design's scope — it is not a promise about arbitrary
  unsupported driver options.

**ARCHITECT CALL — numeric path-specific limits:**

- `MaxParameters` and `MaxSqlLength` are explicitly requested constraints on the **portable SQL
  shape only**. The native/binary protocols have no SQL parameter list or SQL-length payload, so these
  values do **not** select a path, do **not** force `Auto` away from native, and must **not** be
  reinterpreted as native row-byte or payload limits. The availability matrix documents them as
  portable-only: on a native path they are inapplicable by definition, never silently claimed as
  enforced against the binary protocol. This is documented inapplicability, not silent ignoring.
- `MaxBatchSize`, where specified, is a real upper bound on native chunks where the path supports
  chunking. If the selected native path cannot honor it, the request fails preflight (and `Auto` may
  choose another compatible path). A PostgreSQL `COPY` batch limit must never be silently ignored.
  `null` means no nextorm batch bound.

**ARCHITECT CALL — `KeepIdentity` with no identity column:** when the destination has no identity
values to override, the requested behavior applies to zero rows and is **vacuously satisfied**; it is
documented explicitly as a no-effect semantic rather than reported as unsupported.

## 5. Native/provider paths

### 5.1 SQL Server — `SqlBulkCopy`

**FACT:** the pinned driver is `Microsoft.Data.SqlClient 6.1.7`; `SqlBulkCopy` is the native API,
and `DestinationTableName` accepts a qualified destination name
(<https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlbulkcopy.destinationtablename?view=sqlclient-dotnet-6.1>).

**DESIGN:**

- Retain the native `SqlBulkCopy` path and the existing flag mapping
  (`CheckConstraints`, `TableLock`, `KeepNulls`, `FireTriggers`).
- An internal transaction is a **provider-specific SQL Server option mapped to the actual
  `SqlBulkCopy` driver batch**, not a whole-operation semantic invented by nextorm. The API does
  **not** guarantee whole-operation atomicity when the operation spans multiple driver batches.
- `MaxBatchSize = null` retains the driver's `BatchSize = 0`, so the whole input is **one driver
  batch**. Internal transaction may therefore be atomic for that configuration; no artificial finite
  batch size is required and none is imposed as a default.
- Tests must assert the mapping and the multi-batch non-guarantee, and must not assert that an
  internal transaction can never be whole-load atomic.
- An external context/ambient transaction combined with the internal-transaction option is rejected
  **before writes**. The external transaction is never dropped, reused incorrectly, or silently
  replaced. An explicit context transaction is the general whole-load atomicity solution, but it
  cannot be combined with the internal-transaction flag.
- Whole-load atomicity otherwise is available only through an explicit context transaction; nextorm
  does not invent one.
- Destination qualification follows SQL Server's documented `database.schema.table` model where
  the database/schema parts are actually supplied and supported.
- Existing temporary targets such as `#temp` are supported through the same connection; precise
  temp-kind verification is **PLANNING**.

### 5.2 PostgreSQL — `COPY` / `NpgsqlBinaryImporter`

**FACT:** the pinned driver is `Npgsql 10.0.3`. `NpgsqlBinaryImporter.Timeout` exists
(<https://www.npgsql.org/doc/api/Npgsql.NpgsqlBinaryImporter.html#Npgsql_NpgsqlBinaryImporter_Timeout>),
which corrects older documentation/assumptions that `COPY` has no timeout.

**DESIGN:**

- Retain native `COPY` through `NpgsqlBinaryImporter`.
- The importer must be completed with `Complete` on success.
- Cancellation or failure disposes the importer appropriately; no detached writer may continue
  after the public call returns.
- Respect the context transaction.
- Honor the remaining whole-invocation timeout budget through the importer's timeout where
  supported; never extend the deadline.
- Destination qualification supports schema and schema-only targets. PostgreSQL temporary tables
  are supported through the same connection, subject to the existing temp-target rules.
- The old "PostgreSQL rejects a timeout" behavior is removed; PostgreSQL timeout is no longer a
  blocked limitation.

### 5.3 MySQL and MariaDB — `MySqlBulkCopy`

**FACT:** the pinned driver is `MySqlConnector 2.6.2`; its bulk-copy API is documented at
<https://mysqlconnector.net/api/mysqlconnector/mysqlbulkcopytype/>. `LOAD DATA LOCAL INFILE`
behavior and troubleshooting are documented at <https://mysqlconnector.net/> and
<https://mysqlconnector.net/troubleshooting/load-data-local-infile/>.

**DESIGN:**

- Add the native `MySqlBulkCopy` path for MySQL. MariaDB inherits the shared implementation, but
  MariaDB support must be proven against a live server; it is not inferred from MySQL.
- `AllowLoadLocalInfile` and the server-side `local_infile` permission are **not enabled
  automatically**. nextorm must not mutate security settings implicitly.
- `Auto` mode without the required client permission explicitly enabled chooses the portable path
  when the request is expressible there.
- Strict `Native` mode produces an actionable preflight error when the required client permission
  is not enabled.
- If feasible, preflight the remote server's `local_infile` capability as well. A runtime
  `LOAD`/permission error is **not** a fallback trigger.
- Do **not** discover missing server permission through trial writes and replay. The current
  context connection and transaction are used; no implicit connection rerouting.
- No capability that requires enabling a security setting implicitly is in scope.

### 5.4 ClickHouse — `InsertBinaryAsync`

**FACT:** the pinned driver is `ClickHouse.Driver 1.4.0`. `ClickHouseBulkCopy` is obsolete for
production use; the non-obsolete native API is `ClickHouseClient.InsertBinaryAsync`
(<https://github.com/ClickHouse/clickhouse-cs/tree/e26ccd238b26744dbb1da0feb86ed2ea487e76e7>).
The pinned source shows `InsertBinaryAsync` overloads and an `InsertOptions` carrying
`MaxDegreeOfParallelism` and `UseSession`; sessions require a session id, and sessions combined
with `MaxDegreeOfParallelism > 1` are rejected by the driver.

**DESIGN:**

- Implement the production native path with `InsertBinaryAsync`; do not implement it with the
  obsolete `ClickHouseBulkCopy` wrapper.
- Expose parallelism and per-operation session behavior. By default preserve the configured
  session.
- **No session + temporary target** is rejected. The native path is unavailable without a session;
  under `Auto`, portable fallback is allowed only if the existing connection actually provides the
  same target visibility, otherwise the request is rejected.
- **Active session + parallelism > 1** is rejected unless the user explicitly disables the session
  on a non-temporary target. nextorm does not disable the session automatically. Disabling the
  session for a temporary target is likewise rejected. These contradictory option combinations are
  **rejected**; they are never resolved by falling back to a path that ignores them.
- Temporary targets must keep the **same session id**. A session flag alone without an id is
  insufficient, per the driver source.
- Add `ClickHouseClient`/`ClickHouseDataSource`-based registration/constructors so the context's
  native path can use a shared client.
- For the existing `DbConnection` path, a separate native client/pool is built from a **copy of the
  full driver settings** — not a connection-string round-trip — preserving credentials, custom HTTP
  configuration/headers/settings, resolvers, logging, and session id. This full-settings-copy path
  for an actual `ClickHouseConnection` is **required supported native integration**, not an optional
  or deferred client-only substitute.
- No reflection and no access to driver internals.
- Preserve caller ownership: do not dispose caller-owned clients, connections, or HTTP resources.
  Dispose only resources nextorm genuinely owns.
- **Native prerequisite failure under `Auto`:** when the native path cannot be prepared without
  dropping configuration/session (for example a `DbConnection` wrapper that does not expose the
  typed driver settings needed for the copy), `Auto` selects the portable path **only if** that path
  honors every requested option against the existing connection. Strict `Native` produces an
  actionable `NotSupportedException`. "Fail if the configuration/session cannot be preserved" means
  the native path may not execute by discarding settings or session; it is not a blanket failure that
  forbids an eligible `Auto` fallback.
- No trial writes, probing writes, or fallback after native work starts. A runtime native failure is
  propagated, never replayed portably.
- Never silently mutate the caller's client/settings.
- ClickHouse has no transactions; no transaction semantics are fabricated. Temporary targets do not
  imply an automatic transaction.
- `InsertBinaryAsync` accepts `IEnumerable`, not `IAsyncEnumerable`, and exposes no public
  progress callback. A bounded adaptation/chunk adapter is therefore required. It must not buffer
  the whole asynchronous source. The exact chunk adapter is **PLANNING**.
- Aggregate operation counts/progress across native chunks and preserve cancellation. Expose
  meaningful completed-batch progress, not an exact per-row server-commit guarantee.
- Integration of driver batching/parallelism is **PLANNING**; an unbounded producer queue is
  prohibited.

### 5.5 SQLite — portable only

**FACT:** `Microsoft.Data.Sqlite` is pinned at `10.0.12`. Microsoft documents bulk-insert guidance
as batching/transaction usage rather than a special native bulk-copy class
(<https://learn.microsoft.com/dotnet/standard/data/sqlite/bulk-insert>).

**DESIGN:** SQLite uses the portable path with real SQLite semantics. No "native SQLite bulk copy"
is invented. Tests use a real file-based or in-process SQLite database, not a mocked provider.

## 6. Destination qualification

**DESIGN:** the destination is independent per identifier part:

- `TableName`, `SchemaName`, `DatabaseName`, and `ServerName` are separate option parts.
- Missing parts continue to come from the mapping/context.
- Schema-only targeting is supported.
- Each part is quoted separately. A dotted string is **not** a raw SQL escape hatch.
- `TableSchema` is kept for compatibility. Architect call: it is an **alias** for the schema part;
  equal old/new values are accepted, conflicting values are rejected.
- `ServerName` is a SQL identifier qualification only where the provider/path actually supports it.
  It is **never** connection rerouting.
- Provider/path support must be validated; do not pretend that every server/database/schema
  combination is universal.
- SQL Server documents `database.schema.table`.
- The exact support matrix is verified in the plan. Server qualification is unsupported unless
  there is driver/server evidence and a live test; otherwise it is rejected explicitly.
- Database vs schema follows the dialect: MySQL and ClickHouse treat schema as database (schema ≡
  database); conflicting aliases are rejected.

## 7. Temporary targets

**DESIGN:**

- Existing temporary table targets are supported; the bulk API performs no create/drop DDL.
- A typed temporary-target semantic is sufficient to choose name qualification and session
  handling.
- Do **not** import linq2db `CreateIfNotExists`/`DropIfExists` as no-op flags.
- Existing temp targets need the correct connection/session and lifetime.
- Unsupported temp kinds are rejected explicitly.
- Temporary tables do **not** imply an automatic transaction.
- SQL Server `#temp` and PostgreSQL temp targets are supported through the same connection;
  MySQL/SQLite use the corresponding connection where the driver allows it; ClickHouse requires the
  same session id.
- Precise provider-supported temp kinds are **PLANNING** verification work, not an assumption that
  every kind is supported everywhere.

## 8. Compatibility and breaking change

### 8.1 Timeout replacement (user-approved breaking change)

The user explicitly decided **not** to retain `TimeoutSeconds` or `Timeout(int)`. This is the
signature-level breaking change, and it supersedes the issue's extend-only criterion **for the
timeout API**; the user separately authorized stricter validation of previously ignored options
[§8.2](#82-rest-of-the-surface), which is a behavior-level break.

**DESIGN:**

- Replace `int? TimeoutSeconds` with public nullable `TimeSpan Timeout`.
- Replace builder `Timeout(int seconds)` with `Timeout(TimeSpan timeout)`.
- **No compatibility shims and no legacy `int` overload** for the removed timeout APIs; the old
  `Timeout(int)` entry point does not survive as an alias.
- The timeout is one **whole public bulk invocation** budget, measured on an absolute monotonic
  clock. The budget starts when execution enters the bulk operation, **not** when the builder was
  constructed. It spans:
  - connection acquisition/open;
  - setup/preflight;
  - asynchronous enumeration;
  - batching;
  - driver work;
  - progress work.
- The budget does not reset per chunk or per batch.
- `null` means no additional nextorm deadline; driver defaults may still apply.
- Values `<= 0` are rejected. There is no special "infinity" sentinel. Positive overflow during
  conversion to driver units must be handled safely.
- Combine the deadline token with the user's cancellation token. Honor the remaining budget in the
  native/portable command timeout where supported; never increase the initial deadline.
- For synchronous, uninterruptible input `MoveNext`, progress callbacks, or arbitrary user code,
  the deadline is cooperative: check before and after such work. Arbitrary user code cannot be
  forcibly preempted. Document this limit and never claim hard real-time behavior.
- For asynchronous driver work, propagate cancellation. Do not return while writes continue
  detached.
- **ARCHITECT CALL — deadline diagnostics are now fixed:** a nextorm budget expiry surfaces as
  `TimeoutException`; cancellation from the caller's token surfaces as `OperationCanceledException`
  carrying that token. When both are observed as canceled, **user cancellation takes precedence**
  over the deadline diagnosis. Underlying driver timeout errors keep their driver-specific exception
  types unless the nextorm deadline has actually expired, in which case the nextorm deadline
  diagnosis applies. This is no longer a planning item.
- Timeout/cancellation is **not** a rollback guarantee. Already accepted batches may remain unless
  a real transaction provides atomicity.
- Update all API docs and tests in EN and RU for the removed timeout APIs.
- All other existing options keep their names and shapes; their validation becomes stricter where
  they were previously ignored (see [§8.2](#82-rest-of-the-surface)).

### 8.2 Rest of the surface

**DESIGN:** apart from the timeout replacement, existing non-timeout public members keep their names
and shapes. The option model may be extended additively with typed provider-specific groups.

**User-authorized second compatibility change:** stricter validation of **existing** options that
were previously silently ignored when unsupported by the provider or path is explicitly authorized
(the user answered **YES** to the independent-review compatibility question). This is a behavior
break, not a signature break:

- a request that used to be accepted and silently ignored may now fail preflight with
  `NotSupportedException` (or, under `Auto`, select another path that honors it);
- no non-timeout public member is removed or renamed by this change;
- the stricter validation is limited to options in this design's scope and to their previously
  ignored unsupported combinations — it is not authorization to reject arbitrary driver options;
- EN/RU release documentation and the guide must state the compatibility note, including the `Auto`
  default behavior shift described in [§3](#3-execution-modes).

No further breaking change is authorized by this design.

## 9. Seven-item disposition

This table records the design direction for every residual item. The implementation plan must
finish each item as **implemented / out-of-scope / blocked with reason** and attach evidence; a
design direction is not an implementation claim.

| # | Residual item | Design disposition | Reason / boundary |
|---|---|---|---|
| 1 | `BulkCopyType` (`Default`/`RowByRow`/`MultipleRows`/`ProviderSpecific`) | **New nextorm mode `Auto` / `Native` / `Portable`** | `BulkCopyType` is a **linq2db gap-analysis item**, not a public enum that nextorm previously shipped and removed; there is no `BulkCopyType.Default` compatibility obligation. The new mode API is fresh surface whose exact names are **PLANNING**. Explicit mode selection with strict preflight and no silent fallback. |
| 2 | `UseInternalTransaction` | **Implemented on SQL Server native only, mapped to the actual `SqlBulkCopy` driver batch** | Atomicity holds only while the operation is a single driver batch (e.g. `MaxBatchSize = null` → driver `BatchSize = 0`). No whole-operation guarantee across multiple batches and no artificial finite batch default. External transaction + internal transaction is rejected before writes. Other drivers get no fabricated internal-transaction semantics. |
| 3 | `ServerName` / `DatabaseName` | **`DatabaseName` implemented per dialect; `ServerName` evidence-gated** | Server qualification only with driver/server evidence and a live test; otherwise explicit rejection. `DatabaseName` follows the provider's schema/database model. |
| 4 | `SchemaName` without `TableName` | **Implemented** | Schema-only targeting is supported; missing table comes from mapping/context. |
| 5 | `TableOptions` (temp-table flags) | **Implemented as typed temporary-target semantics; DDL flags out of scope** | No `CreateIfNotExists`/`DropIfExists` no-op flags and no create/drop DDL in the bulk API. Unsupported temp kinds are rejected. |
| 6 | `BulkCopyTimeout` outside SQL Server | **Implemented as whole-invocation `TimeSpan Timeout` on every path** | PostgreSQL importer timeout is real; portable/native deadlines are enforced by nextorm checkpoints plus remaining command timeout where supported. Budget starts at execution entry. Deadline = `TimeoutException`; user token = `OperationCanceledException`, with user cancellation taking precedence when both are observed. Breaking API replacement. |
| 7 | ClickHouse `MaxDegreeOfParallelism` / `WithoutSession` | **Implemented through `InsertBinaryAsync`; `Auto` prerequisite failure falls back only to a fully honoring portable path** | Contradictory combinations (session + parallelism > 1, disabling session for temp) are rejected, never ignored. A session flag without an id is insufficient. `Auto` uses portable only when the existing connection honors every request and provides the same target visibility; strict `Native` fails actionably. |
| — | Missing native MySQL/MariaDB | **Implemented with `MySqlBulkCopy`; MariaDB live evidence required** | MariaDB inherits the shared implementation but is not certified from MySQL alone. |
| — | Missing native ClickHouse | **Implemented with `InsertBinaryAsync`** | Replaces the obsolete-wrapper limitation. |
| — | SQLite | **Portable only** | No special native bulk-copy API; real SQLite database evidence required. |

## 10. Validation, errors, and transactions

**DESIGN:**

- Validate option values as early as possible, at the builder call site where the builder already
  validates, and again before input consumption/writes where the combination depends on the
  provider/path.
- Validation is **semantic**: an explicitly requested value (including `false`) is honored only when
  the path actually produces that outcome. A previously ignored unsupported request now fails
  preflight (`NotSupportedException`) or makes `Auto` select another fully honoring path; it is never
  erased. Explicitly authorized by the user ([§8.2](#82-rest-of-the-surface)).
- The availability matrix must cover explicit `false`, default-equivalent values, `KeepIdentity`
  without an identity column (vacuous no-effect), and the portable-only numeric limits described in
  [§4.1](#41-option-value-semantics-explicit-false-defaults-and-limits).
- Deadline diagnostics are fixed: `TimeoutException` for nextorm budget expiry,
  `OperationCanceledException` for caller-token cancellation, caller cancellation winning if both
  are observed; driver timeout types are preserved unless the nextorm deadline expired.
- Unsupported path/option combinations fail **before** reading input or sending writes where
  determinable. In particular, strict `Native` must not open a write session and only then discover
  a missing prerequisite.
- Invalid values are `ArgumentException` subclasses (`ArgumentOutOfRangeException` for
  non-positive timeout/limits). Unsupported capabilities are `NotSupportedException`.
- Preserve original driver errors on runtime failure. No catch-and-replay as portable.
- SQL Server internal transaction is mapped to the actual driver batch; a single driver batch (for
  example `BatchSize = 0` when `MaxBatchSize` is null) may be atomic, but there is no whole-load
  atomicity guarantee across multiple batches. Whole-load atomicity otherwise requires an explicit
  context transaction, which cannot be combined with the internal-transaction flag.
- PostgreSQL and MySQL/MariaDB respect the context transaction; no duplicate transaction is
  created.
- ClickHouse has no transaction support and no fabricated transaction semantics.
- Timeout/cancellation never promises rollback. Rows already accepted by the server may remain.

## 11. Acceptance criteria

The implementation is accepted only when all of the following are evidenced. This design document
does not claim any of them today.

### 11.1 Functional and disposition

- [ ] All seven #305 items have a final **implemented / out-of-scope / blocked** disposition with a
      reason.
- [ ] Missing native MySQL/MariaDB and ClickHouse paths are implemented, and SQL Server/PostgreSQL
      native paths are retained.
- [ ] SQLite portable semantics are verified against a real file-based/in-process SQLite database.
- [ ] `Auto`/`Native`/`Portable` strictness is covered, including no fallback after native work
      starts and no replay after a runtime failure.
- [ ] `Auto` is the default on every provider; MySQL/MariaDB and ClickHouse select the native path
      when preflight-compatible, the behavior shift is documented in EN/RU release docs, and pinning
      `Portable` restores the old path.
- [ ] Stricter validation of previously ignored unsupported options is covered: such requests fail
      preflight (`NotSupportedException`) or make `Auto` pick a fully honoring path, and are never
      silently erased.
- [ ] Explicit-`false` and default-equivalent semantics are covered per the availability matrix,
      including a default-equivalent honored case and an unhonorable `false` case;
      `KeepIdentity` without an identity column is covered as documented no-effect.
- [ ] `MaxParameters`/`MaxSqlLength` are covered as portable-only constraints that do not select the
      path and are not reinterpreted as native payload limits; `MaxBatchSize` bounds native chunks or
      fails rather than being ignored.
- [ ] ClickHouse `Auto` prerequisite failure falls back to portable only when every requested option
      and the target visibility are honored; contradictory session/parallel/temp combinations are
      rejected, not ignored.
- [ ] Missing MySQL `LOCAL INFILE` permission causes portable selection in `Auto` (when
      expressible) or an actionable preflight failure in `Native`, with **no** security-setting
      mutation and no trial-write replay.
- [ ] Option compatibility with `KeepIdentity`, `IgnoreDuplicates`, and `Returning*` is covered on
      each applicable path.
- [ ] SQL Server internal-transaction semantics are mapped to the actual driver batch; a single
      driver batch may be atomic (including `MaxBatchSize = null` / `BatchSize = 0`), no whole-load
      guarantee across multiple batches is asserted, and external + internal transaction is rejected
      before writes.
- [ ] Destination qualification is escaped per part; schema-only and alias conflicts are covered.
- [ ] Temporary targets and the ClickHouse session/parallelism conflict are covered.
- [ ] The whole-operation timeout spans multiple batches, including portable and slow-source cases,
      and starts at execution entry rather than builder construction.
- [ ] The removed timeout APIs are absent (no `int` overload); the new nullable `TimeSpan Timeout`
      and `Timeout(TimeSpan)` API is covered, with `TimeoutException` on deadline expiry,
      `OperationCanceledException` on caller cancellation, and caller cancellation taking precedence.
- [ ] Progress, cancellation (no detached writes, no false rollback promises), ownership, and full
      ClickHouse driver-settings preservation are covered, including the required full-settings-copy
      native path for a real `ClickHouseConnection`.

### 11.2 Test evidence

- [ ] Native paths are tested against **real** SQL Server, PostgreSQL, MySQL, MariaDB, and
      ClickHouse.
- [ ] MariaDB support has real-server evidence; it is not inferred from MySQL.
- [ ] SQL generation/validation unit tests live in the matching provider unit projects
      (`tests/nextorm.<provider>.tests`), not in the integration project.
- [ ] Assertions use FluentAssertions only; the repository's assertion-style convention test stays
      green.
- [ ] The solution builds under .NET 10 with warnings-as-errors.
- [ ] Provider coverage is **not claimed** for any provider that was not actually run.
- [ ] Before any future integration run, read
      `.opencode/skills/running-integration-tests/SKILL.md`. (Not done in this design task.)

### 11.3 Documentation

- [ ] `docs/guide/22-bulk-insert.md` and `docs/ru/guide/22-bulk-insert.md` describe the modes,
      provider options, qualification, temporary targets, and the new timeout semantics, **and**
      contain the compatibility notes: `Auto` is the default and may switch existing MySQL/MariaDB/
      ClickHouse calls to native, and previously ignored unsupported options are now validated
      strictly.
- [ ] `docs/advanced/limitations.md` and `docs/ru/advanced/limitations.md` are updated: PostgreSQL
      timeout is no longer blocked; ClickHouse no longer carries the blanket "no native path because
      the wrapper is obsolete" limitation.
- [ ] The relevant `docs/connecting-providers/*.md` and `docs/ru/connecting-providers/*.md` pages
      (SQL Server, PostgreSQL, MySQL, MariaDB, ClickHouse, SQLite) reflect the actual support.
- [ ] `docs/specs/comparison/linq2db-backlog-gap-analysis.md` is updated internally, with **no
      public spec links** from public documentation.
- [ ] CRLF is preserved in every edited file.

## 12. Planning items and risks

These are deliberately left for the implementation/test plan; they do not reopen the design:

1. Exact names and grouping of the typed provider-specific options, the exact name of the new mode
   enum (there is no nextorm `BulkCopyType` to preserve — it is a linq2db gap item), plus the
   availability and validation matrix per provider/mode.
2. Exact `ServerName` evidence per provider; unsupported combinations must be rejected, not
   guessed.
3. Exact supported temporary-target kinds per provider and their lifetime/connection/session rules.
4. ClickHouse bounded adapter/chunking strategy over `IEnumerable`; no whole-source buffering and
   no unbounded producer queue.
5. Feasibility and exact mechanism of the MySQL remote `local_infile` preflight.
6. Whether the SQL Server internal-transaction option is named/grouped separately from the
   destination options (it must remain typed and provider-scoped).
7. Test matrix and evidence-file layout for real-server runs.

## 13. References

### 13.1 Repository evidence (FACT)

- `src/nextorm.core/Builders/BulkInsertOptions.cs:9-146`, `:152-353` — option record and builder.
- `src/nextorm.core/Builders/BulkInsertBuilder.cs:410`, `:454` — timeout pass-through and native
  path condition.
- `src/nextorm.sqlserver/SqlServerDialect.cs:55`; `src/nextorm.sqlserver/SqlServerDataContext.cs:682-690`
  — SQL Server native path and timeout mapping.
- `src/nextorm.postgres/PostgresDialect.cs:79`; `src/nextorm.postgres/PostgresDataContext.cs:346-418`
  — PostgreSQL native path and current timeout rejection.
- `src/nextorm.mysql/MySqlDialect.cs:48` — no native path today.
- `src/nextorm.clickhouse/ClickHouseDialect.cs:65-70` — no native path today and the obsolete-wrapper
  rationale.
- `Directory.Packages.props:9,17,19,30,31` — pinned driver versions.
- `docs/guide/22-bulk-insert.md`, `docs/ru/guide/22-bulk-insert.md`,
  `docs/advanced/limitations.md`, `docs/ru/advanced/limitations.md`,
  `docs/connecting-providers/*`, `docs/ru/connecting-providers/*`,
  `docs/specs/comparison/linq2db-backlog-gap-analysis.md` — documentation surfaces to update.

### 13.2 Upstream documentation and source (FACT at the cited version/URL)

- Npgsql `NpgsqlBinaryImporter.Timeout`:
  <https://www.npgsql.org/doc/api/Npgsql.NpgsqlBinaryImporter.html#Npgsql_NpgsqlBinaryImporter_Timeout>
- MySqlConnector bulk copy:
  <https://mysqlconnector.net/api/mysqlconnector/mysqlbulkcopytype/>;
  <https://mysqlconnector.net/>;
  <https://mysqlconnector.net/troubleshooting/load-data-local-infile/>
- ClickHouse driver source at commit `e26ccd238b26744dbb1da0feb86ed2ea487e76e7`:
  <https://github.com/ClickHouse/clickhouse-cs/tree/e26ccd238b26744dbb1da0feb86ed2ea487e76e7>
- SQL Server `SqlBulkCopy.DestinationTableName` (`Microsoft.Data.SqlClient 6.1`):
  <https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlbulkcopy.destinationtablename?view=sqlclient-dotnet-6.1>
- SQLite bulk insert guidance:
  <https://learn.microsoft.com/dotnet/standard/data/sqlite/bulk-insert>

## 14. Independent review and approval status

- **Fresh independent builder check: COMPLETE.** Its findings are incorporated in this revision.
- **Significant compatibility question: answered YES by the user.** Stricter validation of existing
  options that were previously silently ignored when unsupported is explicitly authorized, in
  addition to the timeout breaking change. Recorded in [§8.2](#82-rest-of-the-surface) and in the
  acceptance criteria; the earlier "no other breaking change authorized" contradiction is removed.
- **Remaining builder-check gaps settled by ARCHITECT CALL.** They were made in the context of the
  preceding message, in which the user heard the `Auto`/default and SQL Server behavior; they are
  architect decisions and are **not** claimed as independent explicit user votes:
  1. Default mode `Auto` on all providers including the new native paths; existing MySQL/MariaDB/
     ClickHouse calls may switch to native when preflight-compatible; release docs note the behavior
     shift; strict `Native` failure as agreed ([§3](#3-execution-modes)).
  2. Option-value semantics: explicit `false` is a real request; default-equivalent values are
     honored by semantic outcome; unhonorable requests fail or make `Auto` choose a compatible path;
     `MaxParameters`/`MaxSqlLength` are portable-only constraints; `MaxBatchSize` bounds native
     chunks or fails; `KeepIdentity` without an identity column is a documented no-effect
     ([§4.1](#41-option-value-semantics-explicit-false-defaults-and-limits)).
  3. SQL Server `UseInternalTransaction` maps to the actual driver batch; `MaxBatchSize = null`
     means one driver batch and may be atomic for that configuration; no artificial batch default
     and no whole-operation guarantee across multiple batches ([§5.1](#51-sql-server--sqlbulkcopy)).
  4. ClickHouse `Auto` native-prerequisite failure falls back portably only when every request and
     target visibility are honored; contradictory option combinations are rejected; the
     full-settings-copy native path for a real `ClickHouseConnection` is required
     ([§5.4](#54-clickhouse--insertbinaryasync)).
  5. Deadline diagnostics fixed: `TimeoutException` for nextorm budget expiry,
     `OperationCanceledException` for caller-token cancellation, caller cancellation taking
     precedence, driver timeout types preserved unless the nextorm deadline expired; budget starts
     at execution entry; no legacy `int` overload ([§8.1](#81-timeout-replacement-user-approved-breaking-change)).
  6. `BulkCopyType` was never a public nextorm enum that was removed — it is a linq2db gap-analysis
     item. The new nextorm mode API naming remains **PLANNING** ([§9](#9-seven-item-disposition)).
- **Written spec approval: PENDING.** Implementation is **NOT** authorized; no planning or
  implementation has been started. The next artifact after approval is an implementation/test plan.
- Tracking: GitHub issue #305 remains **OPEN** in milestone `1.0.9-rc4`; its body is synchronized
  with this document.
