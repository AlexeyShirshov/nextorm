using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

[CollectionDefinition("T302 S1 cache", DisableParallelization = true)]
public sealed class T302S1CacheCollectionDefinition;

[Collection("T302 S1 cache")]
public sealed class T302S1CacheTests : IDisposable
{
    private const string Ddl = """
        drop table if exists t302_s1_cache;
        create table t302_s1_cache (id integer primary key, score int null, flag int not null, label text not null);
        insert into t302_s1_cache (id, score, flag, label) values
            (1, 10, 0, 'ten'),
            (2, 9, 1, 'nine-a'),
            (3, 9, 1, 'nine-b'),
            (4, 5, 1, 'five');
        """;

    private readonly string _databasePath;
    private readonly SqliteDataContext _ctx;

    public T302S1CacheTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"nextorm.t302s1.cache.{Guid.NewGuid():N}.db");
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

    private static DbPreparedQueryCommand<CacheRow> Prepare(DataContext context, QueryCommand<CacheRow> command, bool storeInCache = true)
        => (DbPreparedQueryCommand<CacheRow>)context.GetPreparedQueryCommand(
            command, createEnumerator: false, storeInCache: storeInCache, CancellationToken.None);

    private static object? ExtremeRowOf(QueryCommand<CacheRow> cmd)
        => cmd.GetType().GetProperty("ExtremeRow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(cmd);

    [SqlTable("t302_s1_cache")]
    public sealed class CacheRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("score")]
        public int? Score { get; set; }

        [Column("flag")]
        public int Flag { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;
    }

    [Fact]
    public void SameLogicalForm_DifferentBoundValues_ReusesPlanAndReturnsCurrentWinner()
    {
        _ctx.PurgeQueryCache();

        int capturedFlag = 1;
        var command = _ctx.From<CacheRow>()
            .Where(e => e.Flag == capturedFlag)
            .SelectWhereMax(e => e.Score, ExtremeRowTies.All)
            .ToCommand();

        var first = Prepare(_ctx, command);
        _ctx.ToList(first).Select(r => r.Score).Should().Equal(9, 9);

        capturedFlag = 0;
        var second = Prepare(_ctx, command);
        ReferenceEquals(second, first).Should().BeTrue("the same logical form must reuse the cached plan");
        _ctx.ToList(second).Select(r => r.Score).Should().Equal(10);
    }

    [Fact]
    public void DifferentWhereShape_DifferentPlans()
    {
        _ctx.PurgeQueryCache();

        int captured = 1;
        var cmdA = _ctx.From<CacheRow>()
            .Where(e => e.Flag == captured)
            .SelectWhereMax(e => e.Score, ExtremeRowTies.All)
            .ToCommand();
        var cmdB = _ctx.From<CacheRow>()
            .Where(e => e.Score > captured)
            .SelectWhereMax(e => e.Score, ExtremeRowTies.All)
            .ToCommand();

        var pa = Prepare(_ctx, cmdA);
        var pb = Prepare(_ctx, cmdB);

        _ctx.ToList(pa).Select(r => r.Score).Should().Equal(9, 9);
        _ctx.ToList(pb).Select(r => r.Score).Should().Equal(10);

        var comparer = cmdA.GetQueryPlanEqualityComparer();
        comparer.Equals(cmdA, cmdB).Should().BeFalse("a different Where shape must not share a plan");
    }

    [Fact]
    public void RawShape_ColdAndHit_ReturnsCorrectResults()
    {
        _ctx.PurgeQueryCache();

        // Value-free graph: a captured closure member makes the shape ineligible for the
        // pre-preparation raw index (QueryCommand.HasCapturedValue), so the predicate is a literal.
        // The extreme-row value selector is deliberately ONE shared expression instance: RawShape.Matches
        // compares the ExtremeRow clause by record equality, i.e. reference equality of its
        // LambdaExpression members, while every other lambda is compared structurally.
        Expression<Func<CacheRow, int?>> selector = e => e.Score;

        QueryCommand<CacheRow> Command() => _ctx.From<CacheRow>()
            .Where(e => e.Flag == 1)
            .SelectWhereMax(selector, ExtremeRowTies.All)
            .ToCommand();

        // Cold: before the first preparation the shape has no published candidate at all.
        var cold = Command();
        var coldShape = QueryCommand.CaptureRawShape(cold);
        coldShape.Should().NotBeNull("a literal predicate over a plain table has a value-free raw shape");
        QueryPlanStore.TryGetRaw(_ctx.GetType(), coldShape!, cold, out var planBeforeCold)
            .Should().BeFalse("no cached plan may exist for the shape before the first (cold) call");
        planBeforeCold.Should().BeNull();

        var first = Prepare(_ctx, cold);
        _ctx.ToList(first).Select(r => r.Score).Should().Equal(9, 9);

        // Cold → published: the first call must leave a live raw candidate for the same shape.
        QueryPlanStore.TryGetRaw(_ctx.GetType(), coldShape!, cold, out var planAfterCold)
            .Should().BeTrue("the cold call must publish a raw cached plan for the shape");
        planAfterCold.Should().NotBeNull();

        // Hit: a fresh, equivalent command resolves the published candidate before preparing.
        var warm = Command();
        var warmShape = QueryCommand.CaptureRawShape(warm);
        warmShape.Should().NotBeNull();
        QueryPlanStore.TryGetRaw(_ctx.GetType(), warmShape!, warm, out var planOnHit)
            .Should().BeTrue("the equivalent fresh command must hit the published raw candidate");
        planOnHit.Should().BeSameAs(planAfterCold, "the hit must resolve the identical cached plan instance");

        var second = Prepare(_ctx, warm);
        _ctx.ToList(second).Select(r => r.Score).Should().Equal(9, 9);
        ReferenceEquals(second, first).Should()
            .BeTrue("the warm execution must reuse the very plan instance the cold call cached");

        // The warm execution must not republish: the raw candidate still resolves the same plan.
        QueryPlanStore.TryGetRaw(_ctx.GetType(), warmShape!, warm, out var planAfterHit)
            .Should().BeTrue();
        planAfterHit.Should().BeSameAs(planOnHit);
    }

    [Fact]
    public void StickyCache_NotMutated_ByLoweringRender()
    {
        _ctx.PurgeQueryCache();

        int capturedFlag = 1;
        var command = _ctx.From<CacheRow>()
            .Where(e => e.Flag == capturedFlag)
            .SelectWhereMax(e => e.Score, ExtremeRowTies.All)
            .ToCommand();

        var prepared = Prepare(_ctx, command);
        _ctx.ToList(prepared).Should().NotBeEmpty();

        var cacheBefore = command.Cache;
        var extremeRowBefore = ExtremeRowOf(command);
        var fromBefore = command.From;
        var selectListBefore = command.SelectList;

        // Re-render through a non-caching preparation must not mutate the original command.
        var prepared2 = Prepare(_ctx, command, storeInCache: false);
        _ctx.ToList(prepared2).Should().NotBeEmpty();

        command.Cache.Should().Be(cacheBefore, "lowering must not flip the sticky Cache flag");
        ExtremeRowOf(command).Should().BeSameAs(extremeRowBefore, "lowering must not mutate ExtremeRow");
        command.From.Should().BeSameAs(fromBefore, "lowering must not mutate From");
        command.SelectList.Should().BeSameAs(selectListBefore, "lowering must not mutate SelectList");
    }

    [Fact]
    public void ParameterDeterminism_IdenticalSqlAndParameterSequence()
    {
        _ctx.PurgeQueryCache();

        int capturedFlag = 1;
        var command = _ctx.From<CacheRow>()
            .Where(e => e.Flag == capturedFlag)
            .SelectWhereMax(e => e.Score, ExtremeRowTies.All)
            .ToCommand();

        var first = Prepare(_ctx, command, storeInCache: true);
        var sql1 = first.DbCommand.CommandText;
        var params1 = first.DbCommand.Parameters.Cast<DbParameter>()
            .Select(p => (p.ParameterName, p.Value)).ToList();

        var second = Prepare(_ctx, command, storeInCache: false);
        var sql2 = second.DbCommand.CommandText;
        var params2 = second.DbCommand.Parameters.Cast<DbParameter>()
            .Select(p => (p.ParameterName, p.Value)).ToList();

        sql2.Should().Be(sql1, "repeated renders must produce identical SQL shape");
        params2.Should().Equal(params1, "repeated renders must produce identical parameter name/value sequence");
        (sql1.Split(new[] { "$capturedFlag" }, StringSplitOptions.None).Length - 1)
            .Should().Be(2, "the declared condition is duplicated (E then O)");
        params1.Should().ContainSingle().Which.Value.Should().Be(1);
    }
}
