using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// S3/T302: parameter determinism and plan-cache invariants for composite value-key <c>All</c>. The
/// user condition <c>C</c> is rendered once per stage and once in the outer join-back (<c>n+1</c>
/// occurrences), so the SQL pass and the parameter-extraction pass must agree on count, values and
/// order, a cached plan must be reused with the current invocation's values, and the shared command's
/// sticky state must stay untouched.
/// </summary>
[CollectionDefinition("T302 S3 cache", DisableParallelization = true)]
public sealed class T302S3CacheCollectionDefinition;

[Collection("T302 S3 cache")]
public sealed class T302S3CompositeAllCacheTests : IDisposable
{
    private const string Ddl = """
        drop table if exists t302_s3_cache;
        create table t302_s3_cache (id integer primary key, v1 int null, v2 int null, v3 int null, flag int not null, grp text null, payload int null);
        insert into t302_s3_cache (id, v1, v2, v3, flag, grp, payload) values
            (1, 9, 2, 0, 1, 'a', 10),
            (2, 8, 100, 100, 0, 'a', 20),
            (3, 9, 2, 5, 0, 'b', 30),
            (4, 9, 100, 0, 1, 'b', 40),
            (5, 7, 50, 50, 1, 'b', 50);
        """;

    private readonly string _databasePath;
    private readonly SqliteDataContext _ctx;

    public T302S3CompositeAllCacheTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"nextorm.t302s3.cache.{Guid.NewGuid():N}.db");
        _ctx = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ExecuteRaw(Ddl);
    }

    public void Dispose() => _ctx.Dispose();

    private void ExecuteRaw(string sql)
    {
        _ctx.EnsureConnectionOpen();
        using var command = _ctx.CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    private static DbPreparedQueryCommand<T> Prepare<T>(DataContext context, QueryCommand<T> command, bool storeInCache = true)
        => (DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(
            command, createEnumerator: false, storeInCache: storeInCache, CancellationToken.None);

    private static object? ExtremeRowOf(QueryCommand<CacheRow> cmd)
        => cmd.GetType().GetProperty("ExtremeRow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(cmd);

    private static bool DontCacheOf(QueryCommand command)
        => (bool)typeof(QueryCommand)
            .GetField("_dontCache", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(command)!;

    /// <summary>Named composite key so the value selector can be a shared expression-tree instance.</summary>
    public sealed record Key2(int? V1, int? V2);

    [SqlTable("t302_s3_cache")]
    public sealed class CacheRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("v1")]
        public int? V1 { get; set; }

        [Column("v2")]
        public int? V2 { get; set; }

        [Column("v3")]
        public int? V3 { get; set; }

        [Column("flag")]
        public int Flag { get; set; }

        [Column("grp")]
        public string? Grp { get; set; }

        [Column("payload")]
        public int? Payload { get; set; }
    }

    [Fact]
    public void ParameterDeterminism_CompositeArity2And3_SqlPassAndParameterListAgree()
    {
        _ctx.PurgeQueryCache();

        int capturedFlag = 1;
        int floor = 7;
        var arity2 = _ctx.From<CacheRow>()
            .Where(e => e.Flag == capturedFlag && e.V1 >= floor)
            .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All)
            .ToCommand();
        var arity3 = _ctx.From<CacheRow>()
            .Where(e => e.Flag == capturedFlag && e.V1 >= floor)
            .SelectWhereMax(e => new { e.V1, e.V2, e.V3 }, ExtremeRowTies.All)
            .ToCommand();

        foreach (var (command, expectedOccurrences) in new[] { (arity2, 3), (arity3, 4) })
        {
            var first = Prepare(_ctx, command, storeInCache: true);
            var sql1 = first.DbCommand.CommandText;
            var params1 = first.DbCommand.Parameters.Cast<DbParameter>()
                .Select(p => (p.ParameterName, p.Value)).ToList();

            var second = Prepare(_ctx, command, storeInCache: false);
            var sql2 = second.DbCommand.CommandText;
            var params2 = second.DbCommand.Parameters.Cast<DbParameter>()
                .Select(p => (p.ParameterName, p.Value)).ToList();

            sql2.Should().Be(sql1, "repeated composite renders must produce identical SQL");
            params2.Should().Equal(params1, "the parameter name/value sequence must be deterministic");

            // C is rendered n+1 times: once per stage and once in O.
            (sql1.Split(new[] { "$capturedFlag" }, StringSplitOptions.None).Length - 1)
                .Should().Be(expectedOccurrences, "the flag condition is duplicated once per stage and once in O");
            (sql1.Split(new[] { "$floor" }, StringSplitOptions.None).Length - 1)
                .Should().Be(expectedOccurrences, "the floor condition is duplicated once per stage and once in O");

            // The SQL pass and the extracted list agree: every distinct placeholder is bound once.
            Regex.Matches(sql1, "\\$[A-Za-z_][A-Za-z0-9_]*")
                .Select(m => m.Value).Distinct().Count()
                .Should().Be(params1.Count, "the SQL placeholders and the extracted parameters must agree one-to-one");
            params1.Select(p => p.Value).Should().Equal(new object?[] { 1, 7 });
        }
    }

    [Fact]
    public void SameLogicalForm_DifferentBoundValues_ReusesPlanAndReturnsCurrentWinners()
    {
        _ctx.PurgeQueryCache();

        int capturedFlag = 1;
        var command = _ctx.From<CacheRow>()
            .Where(e => e.Flag == capturedFlag)
            .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All)
            .ToCommand();

        var first = Prepare(_ctx, command);
        // flag == 1: (9,2), (9,100), (7,50) -> the composite winner is (9,100).
        _ctx.ToList(first).Select(r => r.Id).Should().Equal(4);

        capturedFlag = 0;
        var second = Prepare(_ctx, command);
        ReferenceEquals(second, first).Should().BeTrue(
            "the same composite logical form must reuse the cached plan");
        // flag == 0: (8,100), (9,2) -> the composite winner shifts to (9,2).
        _ctx.ToList(second).Select(r => r.Id).Should().Equal(3);
    }

    [Fact]
    public void DifferentWhereShape_DifferentPlans()
    {
        _ctx.PurgeQueryCache();

        int captured = 1;
        var cmdA = _ctx.From<CacheRow>()
            .Where(e => e.Flag == captured)
            .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All)
            .ToCommand();
        var cmdB = _ctx.From<CacheRow>()
            .Where(e => e.V2 > 50 && e.V1 < 9)
            .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All)
            .ToCommand();

        var pa = Prepare(_ctx, cmdA);
        var pb = Prepare(_ctx, cmdB);

        _ctx.ToList(pa).Select(r => r.Id).Should().Equal(4);
        // v2 > 50 and v1 < 9: only (8,100) has an eligible composite key.
        _ctx.ToList(pb).Select(r => r.Id).Should().Equal(2);

        var comparer = cmdA.GetQueryPlanEqualityComparer();
        comparer.Equals(cmdA, cmdB).Should().BeFalse("a different Where shape must not share a composite plan");
    }

    [Fact]
    public void RawShape_ColdAndHit_ReturnsCorrectWinners()
    {
        _ctx.PurgeQueryCache();

        // Value-free graph over a plain table: the literal predicate keeps the command eligible for
        // the pre-preparation raw index. RawShape.Matches compares the ExtremeRow clause by record
        // equality, i.e. reference equality of its LambdaExpression members, so the value selector is
        // one shared instance across the cold and warm commands.
        Expression<Func<CacheRow, Key2>> selector = e => new Key2(e.V1, e.V2);

        QueryCommand<CacheRow> Command() => _ctx.From<CacheRow>()
            .Where(e => e.Flag == 1)
            .SelectWhereMax(selector, ExtremeRowTies.All)
            .ToCommand();

        // Cold: before the first preparation the shape has no published candidate at all.
        var cold = Command();
        var coldShape = QueryCommand.CaptureRawShape(cold);
        coldShape.Should().NotBeNull("a literal predicate over a plain table has a value-free raw shape");
        QueryPlanStore.TryGetRaw(_ctx.GetType(), coldShape!, cold, out var planBeforeCold)
            .Should().BeFalse("no cached plan may exist for the composite shape before the first (cold) call");
        planBeforeCold.Should().BeNull();

        var first = Prepare(_ctx, cold);
        _ctx.ToList(first).Select(r => r.Id).Should().Equal(4);

        // Cold -> published.
        QueryPlanStore.TryGetRaw(_ctx.GetType(), coldShape!, cold, out var planAfterCold)
            .Should().BeTrue("the cold call must publish a raw cached plan for the composite shape");
        planAfterCold.Should().NotBeNull();

        // Hit: a fresh, equivalent command resolves the published candidate before preparing.
        var warm = Command();
        var warmShape = QueryCommand.CaptureRawShape(warm);
        warmShape.Should().NotBeNull();
        QueryPlanStore.TryGetRaw(_ctx.GetType(), warmShape!, warm, out var planOnHit)
            .Should().BeTrue("the equivalent fresh composite command must hit the published raw candidate");
        planOnHit.Should().BeSameAs(planAfterCold, "the hit must resolve the identical cached plan instance");

        var second = Prepare(_ctx, warm);
        _ctx.ToList(second).Select(r => r.Id).Should().Equal(4);
        ReferenceEquals(second, first).Should()
            .BeTrue("the warm execution must reuse the very plan instance the cold call cached");

        QueryPlanStore.TryGetRaw(_ctx.GetType(), warmShape!, warm, out var planAfterHit)
            .Should().BeTrue();
        planAfterHit.Should().BeSameAs(planOnHit);
    }

    [Fact]
    public void StickyCache_NotMutated_ByCompositeLoweringRender()
    {
        _ctx.PurgeQueryCache();

        int capturedFlag = 1;
        var command = _ctx.From<CacheRow>()
            .Where(e => e.Flag == capturedFlag)
            .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All)
            .ToCommand();

        var prepared = Prepare(_ctx, command);
        _ctx.ToList(prepared).Should().NotBeEmpty();

        var cacheBefore = command.Cache;
        var dontCacheBefore = DontCacheOf(command);
        var extremeRowBefore = ExtremeRowOf(command);
        var fromBefore = command.From;
        var selectListBefore = command.SelectList;

        // Re-render through a non-caching preparation must not mutate the original command.
        var prepared2 = Prepare(_ctx, command, storeInCache: false);
        _ctx.ToList(prepared2).Should().NotBeEmpty();

        command.Cache.Should().Be(cacheBefore, "composite lowering must not flip the sticky Cache flag");
        DontCacheOf(command).Should().Be(dontCacheBefore, "composite lowering must not flip the sticky _dontCache field");
        ExtremeRowOf(command).Should().BeSameAs(extremeRowBefore, "composite lowering must not mutate ExtremeRow");
        command.From.Should().BeSameAs(fromBefore, "composite lowering must not mutate From");
        command.SelectList.Should().BeSameAs(selectListBefore, "composite lowering must not mutate SelectList");
    }

    [Fact]
    public void Grouped_SameForm_DifferentCapturedValues_ReusesPlanWithFreshWinners()
    {
        _ctx.PurgeQueryCache();

        int capturedFlag = 1;
        var command = _ctx.From<CacheRow>()
            .Where(e => e.Flag == capturedFlag)
            .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All, e => e.Grp)
            .ToCommand();

        var first = Prepare(_ctx, command);
        var firstRows = _ctx.ToList(first).OrderBy(r => r.Id).ToList();
        firstRows.Select(r => r.Id).Should().ContainInOrder(1, 4);
        firstRows.Select(r => r.Grp).Should().ContainInOrder("a", "b");
        firstRows.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 10, 40 });

        capturedFlag = 0;
        var second = Prepare(_ctx, command);
        ReferenceEquals(second, first).Should().BeTrue("the grouped composite form must reuse the cached plan");

        // Executed with the second invocation's captured value: the winners are re-evaluated, not stale.
        var secondRows = _ctx.ToList(second).OrderBy(r => r.Id).ToList();
        secondRows.Select(r => r.Id).Should().ContainInOrder(2, 3);
        secondRows.Select(r => r.Grp).Should().ContainInOrder("a", "b");
        secondRows.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 20, 30 });
    }

    [Fact]
    public void CompositeArity3_SameForm_DifferentCapturedValues_ReusesPlanWithFreshWinners()
    {
        _ctx.PurgeQueryCache();

        int capturedFlag = 1;
        var command = _ctx.From<CacheRow>()
            .Where(e => e.Flag == capturedFlag)
            .SelectWhereMax(e => new { e.V1, e.V2, e.V3 }, ExtremeRowTies.All)
            .ToCommand();

        var first = Prepare(_ctx, command);
        var firstRows = _ctx.ToList(first);
        firstRows.Should().ContainSingle();
        firstRows[0].Id.Should().Be(4);
        firstRows[0].V3.Should().Be(0);
        firstRows[0].Payload.Should().Be(40);

        capturedFlag = 0;
        var second = Prepare(_ctx, command);
        ReferenceEquals(second, first).Should().BeTrue("the arity-3 composite form must reuse the cached plan");

        var secondRows = _ctx.ToList(second);
        secondRows.Should().ContainSingle();
        secondRows[0].Id.Should().Be(3);
        secondRows[0].V3.Should().Be(5);
        secondRows[0].Payload.Should().Be(30);
    }

    [Fact]
    public void PreparedAndFresh_GroupedParity_NullablePayload_AndOuterOrderBy()
    {
        _ctx.PurgeQueryCache();
        ExecuteRaw("delete from t302_s3_cache");
        ExecuteRaw("""
            insert into t302_s3_cache (id, v1, v2, v3, flag, grp, payload) values
                (1, 9, 2, 0, 1, 'a', null),
                (2, 8, 100, 100, 1, 'a', 20),
                (3, 9, 2, 5, 1, 'b', 30),
                (4, 8, 7, 7, 1, 'c', null);
            """);

        var command = _ctx.From<CacheRow>()
            .Where(e => e.Flag == 1)
            .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All, e => e.Grp)
            .OrderBy(e => e.Id)
            .ToCommand();
        var preparedRows = _ctx.ToList(Prepare(_ctx, command));

        var freshRows = _ctx.From<CacheRow>()
            .Where(e => e.Flag == 1)
            .SelectWhereMax(e => new { e.V1, e.V2 }, ExtremeRowTies.All, e => e.Grp)
            .OrderBy(e => e.Id)
            .ToList();

        preparedRows.Select(r => r.Id).Should().ContainInOrder(1, 3, 4);
        freshRows.Select(r => r.Id).Should().ContainInOrder(1, 3, 4);
        preparedRows.Select(r => r.Payload).Should().ContainInOrder(new int?[] { null, 30, null });
        freshRows.Select(r => r.Payload).Should().ContainInOrder(new int?[] { null, 30, null });
        preparedRows.Select(r => r.Grp).Should().ContainInOrder("a", "b", "c");
        freshRows.Select(r => r.Grp).Should().ContainInOrder("a", "b", "c");
    }

    [Fact]
    public void DistinctAndOrderBy_OnReusedGroupedPlan_PreservesBehaviour()
    {
        _ctx.PurgeQueryCache();
        ExecuteRaw("delete from t302_s3_cache");
        ExecuteRaw("""
            insert into t302_s3_cache (id, v1, v2, v3, flag, grp, payload) values
                (1, 9, 2, 0, 1, 'a', 10),
                (2, 9, 2, 0, 1, 'b', 10),
                (3, 8, 100, 100, 1, 'c', 20);
            """);

        var command = _ctx.From<CacheRow>()
            .Where(e => e.Flag == 1)
            .SelectWhereMax(e => new { e.V1, e.V2 }, e => new { e.V1, e.V2, e.Payload }, ExtremeRowTies.All, e => e.Grp)
            .Distinct()
            .OrderBy(e => e.Payload);

        var first = Prepare(_ctx, command);
        var firstRows = _ctx.ToList(first);
        firstRows.Should().HaveCount(2, "DISTINCT collapses the identical (9,2,10) group winners");
        firstRows.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 10, 20 });

        var second = Prepare(_ctx, command);
        ReferenceEquals(second, first).Should().BeTrue("the DISTINCT grouped form reuses its cached plan");
        var secondRows = _ctx.ToList(second);
        secondRows.Select(r => r.Payload).Should().ContainInOrder(new int?[] { 10, 20 });
    }
}
