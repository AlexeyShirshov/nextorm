# Evidence 01 — nextorm SQL build & plan cache (input for a SQL-formatting perf assessment)

Repo: `/home/alex/sources/nextorm` (branch `1.0.9`, HEAD `c689f518`, collected 2026-10-12 20:21 +05).
Read-only fact gathering. No product code or test was modified.

## Question

How does nextorm build and cache SQL text? Specifically: is the SQL assembled into a StringBuilder,
are newlines/indentation emitted today, what exactly is cached (built SQL command vs expression/plan),
is the SQL rebuilt per execution, what is the cache key, how does the cached SQL reach the DbCommand,
which benchmarks exercise the cached/prepared path, and does SQL text length/content matter anywhere
other than sending it to the DB. Later judgement (out of scope here): whether adding
linq2db-style formatting (FROM/JOIN/WHERE on separate indented lines) can affect performance.

## Commands

All run from `/home/alex/sources/nextorm`.

```
ls -la src/nextorm.core/DataContext/
grep -rn "AppendLine|Environment.NewLine|\\n" src/nextorm.core/DataContext/ src/nextorm.core/Visitors/ src/nextorm.core/Expressions/ --include=*.cs
grep -rn "CommandText =" src --include=*.cs
grep -rn "GetDbCommand|ExecuteReader|ExecuteDbDataReader" src/nextorm.core/DataContext/ResultSetEnumerator.cs
grep -rn "\\.Prepare()" src --include=*.cs                 # DbCommand.Prepare() search
grep -rn "AppendLine|Environment.NewLine|\\n" src/nextorm.sqlserver src/nextorm.postgres src/nextorm.mysql src/nextorm.mariadb src/nextorm.clickhouse src/nextorm.sqlite --include=*.cs
grep -rn '\\n' tests/nextorm.sqlite.tests/SqlGenerationTests.cs
git log --oneline -3 ; git branch --show-current ; date '+%Y-%m-%d %H:%M:%S %Z'
```

Key files read in full or in relevant ranges:
`src/nextorm.core/DataContext/SqlBuilder.cs`, `SqlSourceRenderer.cs`,
`QueryPlanner.cs`, `DataContext.cs`, `BatchRunner.cs`, `QueryExecutor.cs`,
`DataContext/Cache/{QueryPlanStore,QueryPlan,QueryCache,DbPreparedQueryCommand}.cs`,
`Query/QueryCommand.Plan.cs`, `DataContext/MapperCache.cs`, `DataContext/RowMapperFactory.cs`,
`benchmarks/nextorm.benchmark/{SqliteBenchmarkCachedPlan,SqliteBenchmarkCache}.cs`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs`.

## Facts

### 1. SQL text building

- SQL is assembled into pooled `StringBuilder`s, not string concatenation per call. `SqlBuilder.MakeSelect`:
  - `src/nextorm.core/DataContext/SqlBuilder.cs:62-63`
    ```csharp
    var sqlBuilder = _ctx.ParamMode ? null : StringBuilderPool.Shared.Get();
    var selectBuilder = _ctx.ParamMode ? null : StringBuilderPool.Shared.Get();
    ```
  - Pool: `src/nextorm.core/DataContext/StringBuilderPool.cs:16-19` (`ObjectPool<StringBuilder> Shared`).
- The select list is built in `selectBuilder` and prepended to `sqlBuilder` at the end:
  `SqlBuilder.cs:562` `sqlBuilder!.Insert(0, selectBuilder!.ToString());` — so the final text is one
  `ToString()` of a pooled builder (`SqlBuilder.cs:603-614`, returned via `StringBuilderPool.Shared.Return` at `620-624`).
- **Newlines are already emitted** for many clauses using `StringBuilder.AppendLine()` (which appends
  `Environment.NewLine`). Occurrences in `SqlBuilder.cs`:
  `211, 219, 224, 267, 307, 330, 340, 349, 360, 391, 415, 427, 434, 448, 678, 820, 880, 885, 1023, 1317, 1322, 1372`.
  Examples:
  - `SqlBuilder.cs:224` `if (!_ctx.ParamMode) sqlBuilder!.AppendLine().Append(Kw(" where "));`
  - `SqlBuilder.cs:267` `sqlBuilder!.AppendLine().Append(Kw(" group by "));`
  - `SqlBuilder.cs:307` `if (!_ctx.ParamMode) sqlBuilder!.AppendLine().Append(Kw(" having "));`
  - `SqlBuilder.cs:360` `if (!_ctx.ParamMode) sqlBuilder!.AppendLine().Append(Kw(" order by "));`
- **FROM and JOIN are NOT placed on their own lines**; they are appended inline with a leading space:
  - `SqlBuilder.cs:151` `sqlBuilder!.Append(Kw(" from ")).Append(fromStr);`
  - `SqlBuilder.cs:186` `if (!_ctx.ParamMode) sqlBuilder!.Append(joinSql);`
  - `SqlSourceRenderer.MakeFrom` (`src/nextorm.core/DataContext/SqlSourceRenderer.cs:432-...`) and
    `MakeJoin` (`SqlSourceRenderer.cs:186-...`) return single strings with no newline; a join is built at
    `SqlSourceRenderer.cs:233` (`Append(ctx.Dialect.MakeJoinKeyword(...))`) and `:243` (`Append(fromSql)`).
  - No indentation helper exists for SQL clause layout (the only `indent` hits under `src/nextorm.core`
    are JSON/`json_pretty` options, not SQL emission).
- Actual emitted SQL (test string literals; `SqlGenerationTests.Normalize` maps `\r\n` -> `\n`,
  `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:19`):
  - `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:215`
    ```csharp
    .Should().Be("select \"id\" from \"simple_entity\"\n where (\"id\" > 1)");
    ```
  - join stays on one line: `SqlGenerationTests.cs:3028`
    ```
    "select t1.id, t2.id as 'SId', t1.String from (select id, somestring as 'String' from complex_entity\n where (id > 0)) as 't1' join simple_entity as 't2' on t1.id = cast(t2.id as bigint)"
    ```
  - union: `SqlGenerationTests.cs:560` `"select id from simple_entity\n union \nselect id from simple_entity"`.
  - docs sample is single-line: `docs/guide/14-json.md:87`
    `select id, somestring from complex_entity for json path, root('items'), include_null_values`.
- Concise shape today: `select <cols> from <src> <joins>` then each later clause (`where`, `group by`,
  `having`, `order by`, union, paging, ...) starts on a new line; there is no per-clause indentation.

### 2. Caching

- What is cached: a compiled/prepared command object holding the **built `DbCommand` (with its SQL text
  and bound parameters) plus the compiled row mapper**, not a raw expression tree.
  - `src/nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs:20-29` (`DbCommand DbCommand; DbParameterCollection DbCommandParams;`), `:54` `public readonly string? SqlStmt;`.
  - Created on a miss at `src/nextorm.core/DataContext/QueryPlanner.cs:691-719`:
    `var dbCommand = _createCommand(sql!);` ... `new DbPreparedQueryCommand<TResult>(dbCommand, map, ...)`.
  - Stored at `QueryPlanner.cs:732-735`: `QueryPlanStore.Set(_contextType, queryPlan!.GetCacheVersion(), compiledQuery);`
- The SQL string is **not rebuilt across executions of the same query shape**: on a cache hit the
  stored `DbPreparedQueryCommand` is returned and its existing `DbCommand` reused.
  - Lookup `QueryPlanner.cs:612-620` (`QueryPlanStore.TryGet(...)`), hit branch `:739-773`.
  - Hit path only refreshes parameter values (`:763-764` `if (!compiledQuery.NoParams && compiledQuery.NeedsParamRefresh) RefreshParameters(...)`); it does not re-render SQL.
  - A cache miss re-renders SQL via `MakeSelect` (`QueryPlanner.cs:775-780` -> `:518-550`).
- Cache key type: `QueryPlanStore.Key` = `record struct Key(Type ContextType, QueryPlan Plan)` stored in a
  `[ThreadStatic] Dictionary<Key, Entry>`.
  - `src/nextorm.core/DataContext/Cache/QueryPlanStore.cs:22` `private readonly record struct Key(Type ContextType, QueryPlan Plan);`
  - `:31-32` `[ThreadStatic] private static Dictionary<Key, Entry>? _cache;`
  - `:52-70` `TryGet` / `Set`; key built as `new Key(contextType, plan)`.
- `QueryPlan` itself is the canonical hashable descriptor:
  - `src/nextorm.core/DataContext/Cache/QueryPlan.cs:8` `public sealed class QueryPlan : IEquatable<QueryPlan>`.
  - It hashes the command via the `QueryPlanEqualityComparer`, the eager-load mode/shape, and (only when
    known) a SQL string: `QueryPlan.cs:82-96` (`if (sql is not null) { ... hash.Add(sql); ... }`),
    equality at `:129` (`_sql == obj._sql && ... && _comparer.Equals(...)`).
  - The `sql` argument is non-null only for a raw-SQL override: `QueryPlanner.cs:614`
    `queryPlan = queryCommand.GetOrCreatePlanKey(ext?.ManualSql);` (`ext` is `queryCommand.CustomData as RawSqlOverride`, `:610`). For ordinary LINQ queries the plan key does not contain the rendered SQL.
- Plan key memoization per command: `src/nextorm.core/Query/QueryCommand.Plan.cs:30-44`
  (`GetOrCreatePlanKey`, reuses `_planKey` while prepared and value-list shape unchanged).
- The sticky cache-off flag is `QueryCommand.Cache` backed by `_dontCache`:
  `src/nextorm.core/Query/QueryCommand.cs:344-349` (`get => !_dontCache; set => _dontCache = !value;`).

### 3. Execution path (SQL handed to provider DbCommand)

- The SQL string is assigned to `DbCommand.CommandText` exactly once at preparation time:
  - `QueryPlanner.cs:691` `var dbCommand = _createCommand(sql!);`, where
    `QueryPlanner.cs:29,:61` `_createCommand = hooks.CreateCommand` and the hook is
    `src/nextorm.core/DataContext/DataContext.cs:114` `new ProviderHooks(MapColumn, _createParam, CreateCommand)`.
  - `src/nextorm.core/DataContext/DataContext.cs:296-303`:
    ```csharp
    public DbCommand CreateCommand(string sql)
    {
        var cmd = _connectionManager.GetConnection().CreateCommand();
        cmd.CommandText = sql;
        ...
    }
    ```
- Execution sends the cached command's `CommandText` as-is; the reuse path never re-assigns it:
  - `src/nextorm.core/DataContext/ResultSetEnumerator.cs:291` `_compiledQuery.GetDbCommand(...)` then
    `:295` `_reader = ExecuteReader(sqlCommand);`, `:224-230` `command.ExecuteReader(_compiledQuery.Behavior)`.
  - `QueryExecutor.cs` execution path: `:241-249` `compiledQuery.GetDbCommand(...)` and
    `:181-189` `command.ExecuteReader(behavior)`, `:759` etc.
  - In `DbPreparedQueryCommand.GetDbCommandCore` the CommandText assignment lines are **commented out**:
    `src/nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs:246-264` (e.g. `:249 // cmd.CommandText = CommandText;`).
    So a cached hit reuses the already-assigned text.
- Mutations/raw/procedures create a fresh command each time and assign `CommandText` in
  `QueryExecutor.cs:303-318` (`:318` `cmd.CommandText = commandText;`).

### 4. Benchmarks (`benchmarks/nextorm.benchmark`)

60 classes named `*Benchmark` were listed (plus helper types such as `BenchmarkQueryCommand`). Classes that
measure the cached/prepared (plan-reuse) path:
- `SqliteBenchmarkCachedPlan` (`benchmarks/nextorm.benchmark/SqliteBenchmarkCachedPlan.cs:12`) — explicit
  cached vs prepared vs build arms: `Prepared_ToList` `:45`, `Cached_ToList` `:60`,
  `Cached_PlanOnly_Param` `:74`, `Cached_PlanOnly_NoParam` `:88`, `Build_Sql` `:102`
  (comment `:99-100` "No plan cache ... SQL is regenerated every iteration"), `Build_Sql_Join` `:115`,
  `RePrepare_PlanOnly_Param` `:144`, `M12_NoCache_PlanOnly_Param` `:160`, `M12_NoCache_ToList` `:174`.
- `SqliteBenchmarkCache` (`benchmarks/nextorm.benchmark/SqliteBenchmarkCache.cs:14`) — `NextormCached`
  `:63`, `NextormPrepared` `:82` (both purge/execute cached/prepared paths vs Dapper/Linq2Db).
- `SqliteBenchmarkFeaturePlanCache` (`.../SqliteBenchmarkFeaturePlanCache.cs:20`) —
  `Warm_PlanOnly_Distinct` `:63`, `Warm_PlanOnly_In_AtIn_Captured` `:72`, `Warm_PlanOnly_In_AtIn_Inline`
  `:81`, `Warm_PlanOnly_In_ListContains_Captured` `:90`, `Warm_PlanOnly_In_ListContains_Inline` `:99`.
- `SqliteBenchmarkWarmDecompose` (`.../SqliteBenchmarkWarmDecompose.cs`) — `Cte_Warm_PlanOnly` `:132`,
  `RecursiveCte_Warm_PlanOnly` `:164`, plus `*_Prepare_Hash`/`*_Prepare_NoHash`/`*_Construct` arms.
- `RawSourcePlanReuseBenchmark` (`.../RawSourcePlanReuseBenchmark.cs:36`) — `Unbound_Fresh_Cold` `:86`,
  `Unbound_Reused_Warm` `:98` (baseline), `Bound_Fresh_Cold` `:109`, `Bound_Reused_Warm` `:124`.
- `SqliteBenchmarkFeaturesFairCached` (`.../SqliteBenchmarkFeaturesFairCached.cs`) —
  `B_Nextorm_Cached_LeftJoin` `:131`, `B_Nextorm_Cached_Join4` `:174`, vs EF Core/Linq2Db/Dapper.
- `SqliteBenchmarkFeaturePlanBuild` (`.../SqliteBenchmarkFeaturePlanBuild.cs:33`) and
  `SqliteBenchmarkCachedPlan.Build_Sql*` measure the *build* (cache-miss) path for comparison.
- `SqliteBenchmarkMakeSelect` exists but all its `[Benchmark]` methods are commented out
  (`.../SqliteBenchmarkMakeSelect.cs:30,40,48`).

### 5. Where SQL text length/content matters beyond sending it

- **Row-mapper cache key includes the SQL text** (process-wide, bounded at 4096 entries):
  `src/nextorm.core/DataContext/MapperCache.cs:17`
  `internal readonly record struct MapperCacheKey(Type ProviderType, Type ResultType, string Sql, int ColumnsSignature, bool OneColumn, bool Streaming);`
  built at `src/nextorm.core/DataContext/RowMapperFactory.cs:395-403`
  (`:403 new MapperCacheKey(providerType, typeof(TResult), sql ?? string.Empty, signature, ...)`), looked up
  in `MapperCache` (`MapperCache.cs:63-90`).
- **Plan-cache key** includes the SQL only for a raw-SQL override; for ordinary queries the rendered
  SQL is not part of the plan key (`QueryPlan.cs:82-96,129` + `QueryPlanner.cs:614`).
- **Logging**: `src/nextorm.core/DataContext/ResultSetEnumerator.cs:324`
  `_logger!.LogDebug("Executing query: {sql}" + Environment.NewLine, sqlCommand.CommandText);` (and
  `:335-350` buffered variant reading `sqlCommand.CommandText`); batch logging
  `src/nextorm.core/DataContext/BatchRunner.cs:258` `_logger!.LogDebug("Executing batch: {sql}", plan.ToSql());`.
  Debug build logs the generated SQL at `SqlBuilder.cs:611` and `QueryPlanner.cs:626`.
- **Batches**: statements are joined with `;` and a separator computed from their lengths;
  `src/nextorm.core/DataContext/Roles/IBatchExecutor.cs:212-239` (`:220-222` sums `Sql.Length`, `:227`
  `separator = multiline ? '\n' : ' '`), `BatchRunner.cs:236` `cmd.CommandText = plan.ToSql();`.
- **Query tag comment neutralizes newlines** so a tag cannot inject lines: `SqlBuilder.cs:1500-1546`
  (`:1510-1516` `\r`/`\n` -> `' '`).
- **Temp tables**: a lazy temp-table read plan is always prepared with `storeInCache: false` and never
  cached (`DataContext.cs:448-467`), because the source query's captured parameters must be re-rendered;
  SQL is rebuilt per preparation.
- **`Tokenizer`/raw SQL uniqueness**: `QueryPlanner.cs:607-608`
  `if (!_ctx.ParamMode && cmd.From?.RawSqlSource is not null) EnsureUniqueParameterNames(_ctx.Params);`.
- No DB-side plan cache key, no `DbCommand.Prepare()` call, no distributed/out-of-process SQL cache was found (see Not found).

### 6. Provider SQL builders

Provider projects (sqlserver/postgres/mysql/mariadb/clickhouse/sqlite) contain almost no SQL text
assembly — statement assembly is in core. Newline emission found outside core:
- `src/nextorm.sqlserver/SqlServerDialect.cs:639-646` (`MakePage`): `:644`
  `sqlBuilder.AppendLine().Append(Kw(keywordCase, "fetch next "))...` — newline before `fetch next`.
- `src/nextorm.clickhouse/ClickHouseExtremeRowRenderer.cs:84-120` — manually lays out the extreme-row
  SQL across lines with `AppendLine()` and `AppendLine().Append(' ')` (e.g. `:86,:88,:91,:94,:110,:112,:115,:118`).
- No newline/indentation emission found in `src/nextorm.postgres`, `src/nextorm.mysql`,
  `src/nextorm.mariadb`, or `src/nextorm.sqlite` (the grep returned only the two files above).

## Not found

- No `DbCommand.Prepare()` call anywhere under `src` (grep `\.Prepare()` matched only NextORM's own
  `Prepare` fluent API: `QueryCommand.Prepare.cs`, `EntityBuilderExtensions.cs:932`, join-source helpers).
- No distributed/out-of-process SQL cache (`IDistributedCache`, `DistributedCache`, `Redis`) under `src`.
- No DB-side plan-cache key management.
- No SQL indentation helper for clause layout; `AppendLine()` is the only layout primitive, and FROM/JOIN
  are not newline-separated today.
- `SqliteBenchmarkMakeSelect` benchmark methods are all commented out (`SqliteBenchmarkMakeSelect.cs:30,40,48`).

## Open questions

- Is a formatted (multi-line, indented) SQL string intended to be produced on the cold/build path only,
  or on every render (including the uncached `storeInCache:false` paths such as temp tables and LOB/CSV
  streams)? This determines whether the added formatting cost is paid once per shape or per execution.
- Should the formatted text be the string in `MapperCacheKey.Sql` and the `DbCommand.CommandText`, or
  should a separate display/log representation be formatted (SQL sent to the DB left single-line)?
- Does the SqlServer `fetch next` and ClickHouse extreme-row multi-line output set a precedent that any
  formatting change must preserve (i.e. are those newlines asserted by tests)?
- Are there integration/acceptance tests asserting exact SQL text that would pin the current whitespace
  (the sqlite dialect tests assert exact strings with `\n` before `where`, etc.)?
