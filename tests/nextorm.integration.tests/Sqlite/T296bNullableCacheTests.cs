using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// T296b (#296, D4b): cache identity and parameter refresh of the approved total comparison, executed
/// against a real file-backed SQLite database.
///
/// <para>
/// Four approved positions are driven through the real preparation path
/// (<c>GetPreparedQueryCommand(cmd, createEnumerator: false, storeInCache: true, …)</c>):
/// <c>WHERE</c>, a correlated <c>Exists</c>, a boolean <c>SELECT</c> projection and a <c>JOIN ON</c>.
/// The captured value is a mutable local closed over by the expression, so a cache hit must refresh
/// the existing parameter instead of freezing the first observed value.
/// </para>
///
/// <para>
/// The class runs in the serialized "Query plan cache" collection: the plan store is thread-static and
/// shared by every context on the thread, so a concurrent purge from another test would turn a
/// deliberate cold preparation into an ambiguous hit. Each fact owns its own database file, so the
/// live reads (not just the generated SQL) are the evidence.
/// </para>
/// </summary>
[Collection("Query plan cache")]
public sealed class T296bNullableCacheTests : IDisposable
{
    private const string Ddl = """
        drop table if exists t296b_cache_child;
        drop table if exists t296b_cache_parent;
        drop table if exists t296b_cache_row;
        create table t296b_cache_row (id bigint not null primary key, n int null, s varchar(64) null);
        insert into t296b_cache_row (id, n, s) values
            (1, null, null), (2, 42, 'alpha'), (3, 7, 'beta'), (4, 42, 'alpha'), (5, null, 'gamma');
        create table t296b_cache_parent (id bigint not null primary key);
        insert into t296b_cache_parent (id) values (1), (2), (3);
        create table t296b_cache_child (id bigint not null primary key, parent_id bigint not null, amount int null);
        insert into t296b_cache_child (id, parent_id, amount) values (11, 1, null), (12, 1, 5), (21, 2, 42), (22, 2, 42), (31, 3, null);
        """;

    private readonly string _databasePath;
    private readonly SqliteDataContext _ctx;

    public T296bNullableCacheTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"nextorm.t296b.cache.{Guid.NewGuid():N}.db");
        _ctx = CreateContext();
        ExecuteRaw(Ddl);
    }

    // The database file lives in the temp folder and is intentionally left to the OS: SQLite pooling
    // may keep the handle, and an ignored delete failure would be an unobservable empty catch.
    public void Dispose() => _ctx.Dispose();

    private SqliteDataContext CreateContext() => new($"Data Source='{_databasePath}'", new DataContextBuilder());

    private void ExecuteRaw(string sql)
    {
        _ctx.EnsureConnectionOpen();
        using var command = _ctx.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private static DbPreparedQueryCommand<TResult> Prepare<TResult>(DataContext context, QueryCommand<TResult> command)
        => (DbPreparedQueryCommand<TResult>)context.GetPreparedQueryCommand(
            command, createEnumerator: false, storeInCache: true, CancellationToken.None);

    private static int CountPlaceholders(string sql, string placeholder)
    {
        var count = 0;
        var index = 0;
        while ((index = sql.IndexOf(placeholder, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += placeholder.Length;
        }

        return count;
    }

    private static string FormatMatches(IEnumerable<(long Id, bool Match)> rows)
        => string.Join(",", rows.Select(r => $"{r.Id}:{(r.Match ? "true" : "false")}"));

    private static string PlaceholderOf(System.Data.Common.DbParameter parameter)
        => parameter.ParameterName.StartsWith('$') ? parameter.ParameterName : "$" + parameter.ParameterName;

    /// <summary>
    /// Appends one raw evidence line when <c>NEXTORM_T296B_CACHE_DUMP</c> names a file, so the rv1
    /// artifacts are projected from observed SQL/rows rather than from a claim in the test name.
    /// </summary>
    private static void Dump(string message)
    {
        var path = Environment.GetEnvironmentVariable("NEXTORM_T296B_CACHE_DUMP");
        if (!string.IsNullOrEmpty(path))
            File.AppendAllText(path, message.Replace('\r', ' ').Replace('\n', ' ') + "\n");
    }

    // ---------------------------------------------------------------------------------------------
    // A6/A7 — cold cache: null-first and value-first first executions render the same SQL shape.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ColdPreparation_NullFirstAndValueFirst_ShouldRenderIdenticalSql()
    {
        var coldNull = CreateContext();
        try
        {
            coldNull.PurgeQueryCache();
            var nullRun = ColdWhere(coldNull, null);
            coldNull.PurgeQueryCache();
            var valueRun = ColdWhere(coldNull, 42);

            nullRun.Ids.Should().Equal(1L, 5L);
            valueRun.Ids.Should().Equal(2L, 4L);
            valueRun.Sql.Should().Be(nullRun.Sql, "the first observed value must not change the rendered shape");
            nullRun.Sql.Should().Contain("is null");
            valueRun.Sql.Should().NotContain("42");
            Dump($"cold position=where run=null rows=[{string.Join(",", nullRun.Ids)}] sql={nullRun.Sql}");
            Dump($"cold position=where run=value rows=[{string.Join(",", valueRun.Ids)}] sql={valueRun.Sql}");
        }
        finally
        {
            coldNull.Dispose();
        }

        var coldCorrelated = CreateContext();
        try
        {
            coldCorrelated.PurgeQueryCache();
            var nullRun = ColdCorrelated(coldCorrelated, null);
            coldCorrelated.PurgeQueryCache();
            var valueRun = ColdCorrelated(coldCorrelated, 42);

            nullRun.Ids.Should().Equal(1L, 3L);
            valueRun.Ids.Should().Equal(2L);
            valueRun.Sql.Should().Be(nullRun.Sql);
            Dump($"cold position=correlated run=null rows=[{string.Join(",", nullRun.Ids)}] sql={nullRun.Sql}");
            Dump($"cold position=correlated run=value rows=[{string.Join(",", valueRun.Ids)}] sql={valueRun.Sql}");
        }
        finally
        {
            coldCorrelated.Dispose();
        }

        var coldSelect = CreateContext();
        try
        {
            coldSelect.PurgeQueryCache();
            var nullRun = ColdSelect(coldSelect, null);
            coldSelect.PurgeQueryCache();
            var valueRun = ColdSelect(coldSelect, 42);

            nullRun.Rows.Where(r => r.Match).Select(r => r.Id).Should().Equal(1L, 5L);
            valueRun.Rows.Where(r => r.Match).Select(r => r.Id).Should().Equal(2L, 4L);
            valueRun.Sql.Should().Be(nullRun.Sql);
            Dump($"cold position=select run=null rows=[{FormatMatches(nullRun.Rows)}] sql={nullRun.Sql}");
            Dump($"cold position=select run=value rows=[{FormatMatches(valueRun.Rows)}] sql={valueRun.Sql}");
        }
        finally
        {
            coldSelect.Dispose();
        }

        var coldJoin = CreateContext();
        try
        {
            coldJoin.PurgeQueryCache();
            var nullRun = ColdJoin(coldJoin, null);
            coldJoin.PurgeQueryCache();
            var valueRun = ColdJoin(coldJoin, 42);

            nullRun.Pairs.Should().Equal("1:11", "3:31");
            valueRun.Pairs.Should().Equal("2:21", "2:22");
            valueRun.Sql.Should().Be(nullRun.Sql);
            Dump($"cold position=join run=null pairs=[{string.Join(",", nullRun.Pairs)}] sql={nullRun.Sql}");
            Dump($"cold position=join run=value pairs=[{string.Join(",", valueRun.Pairs)}] sql={valueRun.Sql}");
        }
        finally
        {
            coldJoin.Dispose();
        }
    }

    private static (string Sql, long[] Ids) ColdWhere(DataContext context, int? captured)
    {
        var prepared = Prepare(context, context.From<T296bCacheRow>().Where(x => x.N == captured).OrderBy(x => x.Id).Select(x => x.Id));
        var ids = context.ToList(prepared).ToArray();
        return (prepared.DbCommand.CommandText, ids);
    }

    private static (string Sql, long[] Ids) ColdCorrelated(DataContext context, int? captured)
    {
        var parents = context.From<T296bCacheParent>();
        var children = context.From<T296bCacheChild>();
        var prepared = Prepare(
            context,
            parents
                .Where(p => SqlFunctions.Sql.exists(children.Where(c => c.ParentId == p.Id && c.Amount == captured)))
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                );
        var ids = context.ToList(prepared).ToArray();
        return (prepared.DbCommand.CommandText, ids);
    }

    private static (string Sql, (long Id, bool Match)[] Rows) ColdSelect(DataContext context, int? captured)
    {
        var prepared = Prepare(
            context,
            context.From<T296bCacheRow>()
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, Match = x.N == captured })
                );
        var rows = context.ToList(prepared).Select(r => (r.Id, r.Match)).ToArray();
        return (prepared.DbCommand.CommandText, rows);
    }

    private static (string Sql, string[] Pairs) ColdJoin(DataContext context, int? captured)
    {
        var parents = context.From<T296bCacheParent>();
        var children = context.From<T296bCacheChild>();
        var prepared = Prepare(
            context,
            parents
                .Join(children, (p, c) => p.Id == c.ParentId && c.Amount == captured)
                .Select(p => new { Parent = p.Item1.Id, Child = p.Item2.Id }));
        var pairs = context.ToList(prepared)
            .Select(r => $"{r.Parent}:{r.Child}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        return (prepared.DbCommand.CommandText, pairs);
    }

    // ---------------------------------------------------------------------------------------------
    // A8 — warm transition on one command: null -> 42 -> null keeps SQL bytes and plan identity,
    // refreshes the parameter and returns fresh rows without mutating the sticky Cache flag.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void WarmTransition_Where_ShouldKeepSqlAndPlanIdentityAndRefreshRows()
    {
        _ctx.PurgeQueryCache();

        int? captured = null;
        var command = _ctx.From<T296bCacheRow>().Where(x => x.N == captured).Select(x => x.Id);
        command.Cache.Should().BeTrue("a captured nullable comparison must stay cacheable");

        var first = Prepare(_ctx, command);
        var sql = first.DbCommand.CommandText;
        first.DbCommand.Parameters.Cast<DbParameter>().Should().ContainSingle();
        var nullRows = _ctx.ToList(first).ToArray();
        nullRows.Should().Equal(1L, 5L);

        captured = 42;
        var second = Prepare(_ctx, command);
        ReferenceEquals(second, first).Should().BeTrue("the same command must hit the cached plan");
        second.DbCommand.CommandText.Should().Be(sql);
        second.DbCommand.Parameters[0].Value.Should().Be(42, "the cache hit must refresh the captured value");
        var valueRows = _ctx.ToList(second).ToArray();
        valueRows.Should().Equal(2L, 4L);
        sql.Should().NotContain("42", "the captured value is never inlined");

        captured = null;
        var third = Prepare(_ctx, command);
        ReferenceEquals(third, first).Should().BeTrue();
        third.DbCommand.CommandText.Should().Be(sql);
        third.DbCommand.Parameters[0].Value.Should().Be(DBNull.Value);
        var backToNullRows = _ctx.ToList(third).ToArray();
        backToNullRows.Should().Equal(new long[] { 1L, 5L }, "the null execution must not reuse the 42 row set");

        // A re-prepare through the caching path is still the same plan object.
        ReferenceEquals(Prepare(_ctx, command), first).Should().BeTrue();

        // A call-local non-caching preparation re-plans without touching the sticky flag or the entry.
        var uncached = (DbPreparedQueryCommand<long>)_ctx.GetPreparedQueryCommand(
            command, createEnumerator: false, storeInCache: false, CancellationToken.None);
        ReferenceEquals(uncached, first).Should().BeFalse("storeInCache:false bypasses the plan cache");
        uncached.DbCommand.CommandText.Should().Be(sql);
        _ctx.ToList(uncached).Should().Equal(1L, 5L);
        command.Cache.Should().BeTrue("storeInCache:false is call-local and must never flip QueryCommand.Cache");
        ReferenceEquals(Prepare(_ctx, command), first).Should()
            .BeTrue("the call-local non-caching preparation leaves the cached plan intact");

        Dump($"warm position=where sql={sql} placeholders={CountPlaceholders(sql, PlaceholderOf(first.DbCommand.Parameters[0]))} " +
             $"parameters={first.DbCommand.Parameters.Count} plan_identity=three_runs_and_cached_reprepare " +
             $"rows=null:[{string.Join(",", nullRows)}];42:[{string.Join(",", valueRows)}];null:[{string.Join(",", backToNullRows)}] " +
             $"cache_flag={command.Cache} uncached_plan_distinct=true");
    }

    [Fact]
    public void WarmTransition_CorrelatedExists_ShouldKeepSqlAndPlanIdentityAndRefreshOuterValue()
    {
        _ctx.PurgeQueryCache();

        var parents = _ctx.From<T296bCacheParent>();
        var children = _ctx.From<T296bCacheChild>();
        int? captured = null;
        var command = parents
            .Where(p => SqlFunctions.Sql.exists(children.Where(c => c.ParentId == p.Id && c.Amount == captured)))
            .OrderBy(p => p.Id)
            .Select(p => p.Id);
        command.Cache.Should().BeTrue();

        var fallbacksBefore = QueryPlanner.FallbackRefreshes;

        var first = Prepare(_ctx, command);
        var sql = first.DbCommand.CommandText;
        sql.Should().Contain("exists");
        first.DbCommand.Parameters.Cast<DbParameter>().Should().ContainSingle();
        first.ParamRecipe.Should().BeNull(
            "a correlated shape with outer references is not modelled by the immutable refresh recipe, so every hit uses the ExtractParams fallback");
        var nullRows = _ctx.ToList(first).ToArray();
        nullRows.Should().Equal(1L, 3L);

        captured = 42;
        var second = Prepare(_ctx, command);
        ReferenceEquals(second, first).Should().BeTrue();
        second.DbCommand.CommandText.Should().Be(sql);
        second.DbCommand.Parameters[0].Value.Should().Be(42);
        var valueRows = _ctx.ToList(second).ToArray();
        valueRows.Should().Equal(2L);

        captured = null;
        var third = Prepare(_ctx, command);
        ReferenceEquals(third, first).Should().BeTrue();
        third.DbCommand.CommandText.Should().Be(sql);
        third.DbCommand.Parameters[0].Value.Should().Be(DBNull.Value);
        var backToNullRows = _ctx.ToList(third).ToArray();
        backToNullRows.Should().Equal(new long[] { 1L, 3L }, "the correlated outer value must never be stale");

        var fallbackDelta = QueryPlanner.FallbackRefreshes - fallbacksBefore;
        fallbackDelta.Should()
            .BeGreaterThan(0, "the cache hits re-extract the captured outer value through the ExtractParams fallback");

        Dump($"warm position=correlated sql={sql} parameters=1 param_recipe=null fallback_refreshes_delta={fallbackDelta} " +
             $"plan_identity=three_runs rows=null:[{string.Join(",", nullRows)}];42:[{string.Join(",", valueRows)}];null:[{string.Join(",", backToNullRows)}] " +
             $"cache_flag={command.Cache}");
    }

    [Fact]
    public void WarmTransition_BooleanSelect_ShouldKeepSqlAndPlanIdentityAndRefreshRows()
    {
        _ctx.PurgeQueryCache();

        int? captured = null;
        var command = _ctx.From<T296bCacheRow>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Match = x.N == captured });
        command.Cache.Should().BeTrue();

        var first = Prepare(_ctx, command);
        var sql = first.DbCommand.CommandText;
        first.DbCommand.Parameters.Cast<DbParameter>().Should().ContainSingle();
        var nullRows = _ctx.ToList(first).Select(r => (r.Id, r.Match)).ToArray();
        nullRows.Should().Equal(
            (1L, true), (2L, false), (3L, false), (4L, false), (5L, true));

        captured = 42;
        var second = Prepare(_ctx, command);
        ReferenceEquals(second, first).Should().BeTrue();
        second.DbCommand.CommandText.Should().Be(sql);
        second.DbCommand.Parameters[0].Value.Should().Be(42);
        var valueRows = _ctx.ToList(second).Select(r => (r.Id, r.Match)).ToArray();
        valueRows.Should().Equal(
            (1L, false), (2L, true), (3L, false), (4L, true), (5L, false));

        captured = null;
        var third = Prepare(_ctx, command);
        ReferenceEquals(third, first).Should().BeTrue();
        third.DbCommand.CommandText.Should().Be(sql);
        third.DbCommand.Parameters[0].Value.Should().Be(DBNull.Value);
        var backToNullRows = _ctx.ToList(third).Select(r => (r.Id, r.Match)).ToArray();
        backToNullRows.Should().Equal(
            (1L, true), (2L, false), (3L, false), (4L, false), (5L, true));

        sql.Should().Contain("is null", "the projection is still the total presence predicate");
        sql.Should().NotContain("42");

        Dump($"warm position=select sql={sql} parameters=1 plan_identity=three_runs " +
             $"rows=null:[{FormatMatches(nullRows)}];42:[{FormatMatches(valueRows)}];null:[{FormatMatches(backToNullRows)}] " +
             $"cache_flag={command.Cache}");
    }

    [Fact]
    public void WarmTransition_JoinOn_ShouldKeepSqlAndPlanIdentityAndRefreshPairs()
    {
        _ctx.PurgeQueryCache();

        var parents = _ctx.From<T296bCacheParent>();
        var children = _ctx.From<T296bCacheChild>();
        int? captured = null;
        var command = parents
            .Join(children, (p, c) => p.Id == c.ParentId && c.Amount == captured)
            .Select(p => new { Parent = p.Item1.Id, Child = p.Item2.Id });
        command.Cache.Should().BeTrue();

        static string[] Pairs(List<(long Parent, long Child)> rows)
            => rows.Select(r => $"{r.Parent}:{r.Child}").OrderBy(x => x, StringComparer.Ordinal).ToArray();

        var first = Prepare(_ctx, command);
        var sql = first.DbCommand.CommandText;
        first.DbCommand.Parameters.Cast<DbParameter>().Should().ContainSingle();
        var nullPairs = Pairs(_ctx.ToList(first).Select(r => (r.Parent, r.Child)).ToList());
        nullPairs.Should().Equal("1:11", "3:31");

        captured = 42;
        var second = Prepare(_ctx, command);
        ReferenceEquals(second, first).Should().BeTrue();
        second.DbCommand.CommandText.Should().Be(sql);
        second.DbCommand.Parameters[0].Value.Should().Be(42);
        var valuePairs = Pairs(_ctx.ToList(second).Select(r => (r.Parent, r.Child)).ToList());
        valuePairs.Should().Equal("2:21", "2:22");

        captured = null;
        var third = Prepare(_ctx, command);
        ReferenceEquals(third, first).Should().BeTrue();
        third.DbCommand.CommandText.Should().Be(sql);
        third.DbCommand.Parameters[0].Value.Should().Be(DBNull.Value);
        var backToNullPairs = Pairs(_ctx.ToList(third).Select(r => (r.Parent, r.Child)).ToList());
        backToNullPairs.Should().Equal("1:11", "3:31");

        sql.Should().Contain(" on ", "the total predicate stays in the join condition");
        sql.Should().NotContain("42");

        Dump($"warm position=join sql={sql} parameters=1 plan_identity=three_runs " +
             $"pairs=null:[{string.Join(",", nullPairs)}];42:[{string.Join(",", valuePairs)}];null:[{string.Join(",", backToNullPairs)}] " +
             $"cache_flag={command.Cache}");
    }

    // ---------------------------------------------------------------------------------------------
    // A15 — one logical parameter with several placeholder occurrences, all refreshed per execution,
    // strings and both operand orders included.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void WarmTransition_Inequality_ShouldRefreshEveryOccurrenceOfOneLogicalParameter()
    {
        _ctx.PurgeQueryCache();

        int? captured = null;
        var command = _ctx.From<T296bCacheRow>().Where(x => x.N != captured).Select(x => x.Id);
        command.Cache.Should().BeTrue();

        var first = Prepare(_ctx, command);
        var sql = first.DbCommand.CommandText;
        var parameter = first.DbCommand.Parameters.Cast<DbParameter>().Should().ContainSingle(
            "all occurrences of the captured operand are one logical parameter").Subject;
        var placeholderCount = CountPlaceholders(sql, PlaceholderOf(parameter));
        placeholderCount.Should().Be(
            4, "the three-arm inequality renders the captured placeholder four times");
        var nullRows = _ctx.ToList(first).ToArray();
        nullRows.Should().Equal(2L, 3L, 4L);

        captured = 42;
        var second = Prepare(_ctx, command);
        ReferenceEquals(second, first).Should().BeTrue();
        second.DbCommand.CommandText.Should().Be(sql);
        second.DbCommand.Parameters[0].Value.Should().Be(42);
        var valueRows = _ctx.ToList(second).ToArray();
        valueRows.Should().Equal(new long[] { 1L, 3L, 5L }, "every occurrence must observe the current value");

        captured = null;
        var third = Prepare(_ctx, command);
        ReferenceEquals(third, first).Should().BeTrue();
        third.DbCommand.CommandText.Should().Be(sql);
        third.DbCommand.Parameters[0].Value.Should().Be(DBNull.Value);
        var backToNullRows = _ctx.ToList(third).ToArray();
        backToNullRows.Should().Equal(2L, 3L, 4L);
        sql.Should().NotContain("42");

        Dump($"warm position=where_inequality sql={sql} placeholders={placeholderCount} parameters={first.DbCommand.Parameters.Count} " +
             $"plan_identity=three_runs rows=null:[{string.Join(",", nullRows)}];42:[{string.Join(",", valueRows)}];null:[{string.Join(",", backToNullRows)}] " +
             $"cache_flag={command.Cache}");
    }

    [Fact]
    public void WarmTransition_StringAndOperandOrder_ShouldRenderTheSameShapeAndRefresh()
    {
        _ctx.PurgeQueryCache();

        string? captured = null;
        var forward = _ctx.From<T296bCacheRow>()
            .Where(x => x.S == captured)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        var reversed = _ctx.From<T296bCacheRow>()
            .Where(x => captured == x.S)
            .OrderBy(x => x.Id)
            .Select(x => x.Id);
        forward.Cache.Should().BeTrue();
        reversed.Cache.Should().BeTrue();

        var forwardFirst = Prepare(_ctx, forward);
        var reversedFirst = Prepare(_ctx, reversed);
        reversedFirst.DbCommand.CommandText.Should()
            .Be(forwardFirst.DbCommand.CommandText, "both operand orders render the canonical column-first shape");
        var nullRows = _ctx.ToList(forwardFirst).ToArray();
        var nullRowsReversed = _ctx.ToList(reversedFirst).ToArray();
        nullRows.Should().Equal(1L);
        nullRowsReversed.Should().Equal(1L);

        captured = "alpha";
        var forwardSecond = Prepare(_ctx, forward);
        var reversedSecond = Prepare(_ctx, reversed);
        ReferenceEquals(forwardSecond, forwardFirst).Should().BeTrue();
        ReferenceEquals(reversedSecond, reversedFirst).Should().BeTrue();
        forwardSecond.DbCommand.CommandText.Should().Be(forwardFirst.DbCommand.CommandText);
        reversedSecond.DbCommand.CommandText.Should().Be(forwardFirst.DbCommand.CommandText);
        forwardSecond.DbCommand.Parameters[0].Value.Should().Be("alpha");
        reversedSecond.DbCommand.Parameters[0].Value.Should().Be("alpha");
        var alphaRows = _ctx.ToList(forwardSecond).ToArray();
        var alphaRowsReversed = _ctx.ToList(reversedSecond).ToArray();
        alphaRows.Should().Equal(2L, 4L);
        alphaRowsReversed.Should().Equal(2L, 4L);
        forwardFirst.DbCommand.Parameters.Cast<DbParameter>().Should().ContainSingle();
        forwardSecond.DbCommand.CommandText.Should().NotContain("alpha", "the captured string is never inlined");

        captured = null;
        var forwardThird = Prepare(_ctx, forward);
        ReferenceEquals(forwardThird, forwardFirst).Should().BeTrue();
        forwardThird.DbCommand.Parameters[0].Value.Should().Be(DBNull.Value);
        var backToNullRows = _ctx.ToList(forwardThird).ToArray();
        backToNullRows.Should().Equal(1L);
        forward.Cache.Should().BeTrue();
        reversed.Cache.Should().BeTrue();

        Dump($"warm position=where_string operand_orders=forward,reversed shape_identical=true " +
             $"parameters={forwardFirst.DbCommand.Parameters.Count} plan_identity=three_runs_each " +
             $"rows=null:[{string.Join(",", nullRows)}];alpha:[{string.Join(",", alphaRows)}];null:[{string.Join(",", backToNullRows)}] " +
             $"cache_flag={forward.Cache}/{reversed.Cache} sql={forwardFirst.DbCommand.CommandText}");
    }

    // ---------------------------------------------------------------------------------------------
    // A20 (SQLite side) — the cold null binds the compared column's provider type, and the metadata
    // stays stable across null -> 42 -> null (the SQL Server artifact is rv1/live/sqlserver-metadata.json).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ColdNullParameter_ShouldBeTypedAndStableAcrossTheTransition()
    {
        _ctx.PurgeQueryCache();

        int? captured = null;
        var command = _ctx.From<T296bCacheRow>().Where(x => x.N == captured).Select(x => x.Id);
        var prepared = Prepare(_ctx, command);

        var records = new List<(string Step, System.Data.DbType DbType, byte Precision, byte Scale, string Value)>();

        void Record(string step)
        {
            var parameter = prepared.DbCommand.Parameters[0];
            records.Add((step, parameter.DbType, parameter.Precision, parameter.Scale,
                parameter.Value is DBNull ? "DBNull" : parameter.Value?.ToString() ?? "null"));
        }

        Record("null");
        _ctx.ToList(prepared).Should().Equal(1L, 5L);

        captured = 42;
        Prepare(_ctx, command);
        Record("42");
        _ctx.ToList(prepared).Should().Equal(2L, 4L);

        captured = null;
        Prepare(_ctx, command);
        Record("null");
        _ctx.ToList(prepared).Should().Equal(1L, 5L);

        records.Select(r => r.DbType).Distinct().Should().ContainSingle()
            .Which.Should().Be(System.Data.DbType.Int32, "the captured int? is typed from the compared int column");
        records.Select(r => (r.Precision, r.Scale)).Distinct().Should().ContainSingle()
            .Which.Should().Be(((byte)0, (byte)0));
        records.Select(r => r.Value).Should().Equal("DBNull", "42", "DBNull");
        records.Select(r => r.Step).Should().Equal("null", "42", "null");

        Dump($"parameter_metadata position=where provider=sqlite steps=" +
             string.Join(";", records.Select(r =>
                 $"{r.Step}:DbType={r.DbType},Precision={r.Precision},Scale={r.Scale},Value={r.Value}")) +
             $" sql={prepared.DbCommand.CommandText}");
    }

    // ---------------------------------------------------------------------------------------------
    // Reconnect — a NEW context on the same database stays correct for both captured values.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void NewContextOnTheSameDatabase_ShouldReturnCorrectRowsForBothValues()
    {
        _ctx.PurgeQueryCache();

        var firstContext = CreateContext();
        try
        {
            int? captured = null;
            var prepared = Prepare(firstContext, firstContext.From<T296bCacheRow>().Where(x => x.N == captured).Select(x => x.Id));
            var firstNullRows = firstContext.ToList(prepared).ToArray();
            firstNullRows.Should().Equal(1L, 5L);
            Dump($"reconnect context=first rows=null:[{string.Join(",", firstNullRows)}]");
        }
        finally
        {
            firstContext.Dispose();
        }

        using var secondContext = CreateContext();

        int? secondCaptured = null;
        var nullPlan = Prepare(
            secondContext,
            secondContext.From<T296bCacheRow>().Where(x => x.N == secondCaptured).Select(x => x.Id));
        var secondNullRows = secondContext.ToList(nullPlan).ToArray();
        secondNullRows.Should().Equal(1L, 5L);

        secondCaptured = 42;
        var valuePlan = Prepare(
            secondContext,
            secondContext.From<T296bCacheRow>().Where(x => x.N == secondCaptured).Select(x => x.Id));
        var secondValueRows = secondContext.ToList(valuePlan).ToArray();
        secondValueRows.Should().Equal(2L, 4L);

        secondCaptured = 7;
        var otherPlan = Prepare(
            secondContext,
            secondContext.From<T296bCacheRow>().Where(x => x.N == secondCaptured).Select(x => x.Id));
        var secondOtherRows = secondContext.ToList(otherPlan).ToArray();
        secondOtherRows.Should().Equal(3L);

        secondCaptured = null;
        var backToNull = Prepare(
            secondContext,
            secondContext.From<T296bCacheRow>().Where(x => x.N == secondCaptured).Select(x => x.Id));
        var secondBackToNullRows = secondContext.ToList(backToNull).ToArray();
        secondBackToNullRows.Should().Equal(1L, 5L);

        Dump($"reconnect context=new rows=null:[{string.Join(",", secondNullRows)}];42:[{string.Join(",", secondValueRows)}];" +
             $"7:[{string.Join(",", secondOtherRows)}];null:[{string.Join(",", secondBackToNullRows)}]");
    }
}

/// <summary>Cache fixture row: nullable integer and string columns with null, duplicate and unequal values.</summary>
[SqlTable("t296b_cache_row")]
public interface T296bCacheRow
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("n")]
    int? N { get; set; }

    [Column("s")]
    string? S { get; set; }
}

/// <summary>Cache fixture parent: parent 3 owns only a NULL-amount child, so it exists for a null capture.</summary>
[SqlTable("t296b_cache_parent")]
public interface T296bCacheParent
{
    [Key]
    [Column("id")]
    long Id { get; set; }
}

/// <summary>Cache fixture child: parents own distinct children, with null and duplicated amounts.</summary>
[SqlTable("t296b_cache_child")]
public interface T296bCacheChild
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("parent_id")]
    long ParentId { get; set; }

    [Column("amount")]
    int? Amount { get; set; }
}
