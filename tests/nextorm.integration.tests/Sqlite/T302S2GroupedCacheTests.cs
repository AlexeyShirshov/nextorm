using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

[CollectionDefinition("T302 S2 cache", DisableParallelization = true)]
public sealed class T302S2CacheCollectionDefinition;

[Collection("T302 S2 cache")]
public sealed class T302S2GroupedCacheTests : IDisposable
{
    private const string Ddl = """
        drop table if exists t302_s2_cache;
        create table t302_s2_cache (id integer primary key, score int null, flag int not null, grp text null);
        insert into t302_s2_cache (id, score, flag, grp) values
            (1, 10, 0, 'a'),
            (2, 9, 1, 'a'),
            (3, 9, 1, 'a'),
            (4, 5, 1, 'b'),
            (5, 7, 1, null);
        """;

    private readonly string _databasePath;
    private readonly SqliteDataContext _ctx;

    public T302S2GroupedCacheTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"nextorm.t302s2.cache.{Guid.NewGuid():N}.db");
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

    [SqlTable("t302_s2_cache")]
    public sealed class CacheRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("score")]
        public int? Score { get; set; }

        [Column("flag")]
        public int Flag { get; set; }

        [Column("grp")]
        public string? Grp { get; set; }
    }

    [Fact]
    public void SameLogicalForm_DifferentBoundValues_ReusesPlanAndReturnsCurrentGroupedWinners()
    {
        _ctx.PurgeQueryCache();

        int capturedFlag = 1;
        var command = _ctx.From<CacheRow>()
            .Where(e => e.Flag == capturedFlag)
            .SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp)
            .ToCommand();

        var first = Prepare(_ctx, command);
        // flag == 1: group 'a' ties on 9 (ids 2,3), 'b' wins 5 (id 4), NULL group wins 7 (id 5).
        _ctx.ToList(first).Select(r => r.Score).OrderBy(v => v).Should().Equal(5, 7, 9, 9);

        capturedFlag = 0;
        var second = Prepare(_ctx, command);
        ReferenceEquals(second, first).Should().BeTrue("the same grouped logical form must reuse the cached plan");
        // flag == 0: only 'a' has rows and its winner changes to 10.
        var secondRows = _ctx.ToList(second);
        secondRows.Should().ContainSingle().Which.Score.Should().Be(10);
        secondRows[0].Id.Should().Be(1);
    }

    [Fact]
    public void DifferentWhereShape_DifferentPlans()
    {
        _ctx.PurgeQueryCache();

        int flag = 1;
        int floor = 5;
        var cmdA = _ctx.From<CacheRow>()
            .Where(e => e.Flag == flag)
            .SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp)
            .ToCommand();
        var cmdB = _ctx.From<CacheRow>()
            .Where(e => e.Score > floor)
            .SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp)
            .ToCommand();

        var pa = Prepare(_ctx, cmdA);
        var pb = Prepare(_ctx, cmdB);

        _ctx.ToList(pa).Select(r => r.Score).OrderBy(v => v).Should().Equal(5, 7, 9, 9);
        // score > 5: 'a' wins 10 (id 1), 'b' has no eligible row, NULL group wins 7 (id 5).
        _ctx.ToList(pb).Select(r => r.Id).OrderBy(v => v).Should().Equal(1, 5);

        var comparer = cmdA.GetQueryPlanEqualityComparer();
        comparer.Equals(cmdA, cmdB).Should().BeFalse("a different Where shape must not share a grouped plan");
    }

    [Fact]
    public void RawShape_ColdAndHit_ReturnsCorrectGroupedResults()
    {
        _ctx.PurgeQueryCache();

        // Value-free graph over a plain table: the literal predicate keeps the command eligible for
        // the pre-preparation raw index. RawShape.Matches compares the ExtremeRow clause by record
        // equality, i.e. reference equality of its LambdaExpression members, so both the value
        // selector and the group selector are shared instances across the cold and warm commands.
        Expression<Func<CacheRow, int?>> selector = e => e.Score;
        Expression<Func<CacheRow, object?>> group = e => e.Grp;

        QueryCommand<CacheRow> Command() => _ctx.From<CacheRow>()
            .Where(e => e.Flag == 1)
            .SelectWhereMax(selector, ExtremeRowTies.All, group)
            .ToCommand();

        // Cold: before the first preparation the shape has no published candidate at all.
        var cold = Command();
        var coldShape = QueryCommand.CaptureRawShape(cold);
        coldShape.Should().NotBeNull("a literal predicate over a plain table has a value-free raw shape");
        QueryPlanStore.TryGetRaw(_ctx.GetType(), coldShape!, cold, out var planBeforeCold)
            .Should().BeFalse("no cached plan may exist for the grouped shape before the first (cold) call");
        planBeforeCold.Should().BeNull();

        var first = Prepare(_ctx, cold);
        _ctx.ToList(first).Select(r => r.Score).OrderBy(v => v).Should().Equal(5, 7, 9, 9);

        // Cold -> published.
        QueryPlanStore.TryGetRaw(_ctx.GetType(), coldShape!, cold, out var planAfterCold)
            .Should().BeTrue("the cold call must publish a raw cached plan for the grouped shape");
        planAfterCold.Should().NotBeNull();

        // Hit: a fresh, equivalent command resolves the published candidate before preparing.
        var warm = Command();
        var warmShape = QueryCommand.CaptureRawShape(warm);
        warmShape.Should().NotBeNull();
        QueryPlanStore.TryGetRaw(_ctx.GetType(), warmShape!, warm, out var planOnHit)
            .Should().BeTrue("the equivalent fresh grouped command must hit the published raw candidate");
        planOnHit.Should().BeSameAs(planAfterCold, "the hit must resolve the identical cached plan instance");

        var second = Prepare(_ctx, warm);
        _ctx.ToList(second).Select(r => r.Score).OrderBy(v => v).Should().Equal(5, 7, 9, 9);
        ReferenceEquals(second, first).Should()
            .BeTrue("the warm execution must reuse the very plan instance the cold call cached");

        // The warm execution must not republish.
        QueryPlanStore.TryGetRaw(_ctx.GetType(), warmShape!, warm, out var planAfterHit)
            .Should().BeTrue();
        planAfterHit.Should().BeSameAs(planOnHit);
    }

    [Fact]
    public void StickyCache_NotMutated_ByGroupedLoweringRender()
    {
        _ctx.PurgeQueryCache();

        int capturedFlag = 1;
        var command = _ctx.From<CacheRow>()
            .Where(e => e.Flag == capturedFlag)
            .SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp)
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

        command.Cache.Should().Be(cacheBefore, "grouped lowering must not flip the sticky Cache flag");
        ExtremeRowOf(command).Should().BeSameAs(extremeRowBefore, "grouped lowering must not mutate ExtremeRow");
        command.From.Should().BeSameAs(fromBefore, "grouped lowering must not mutate From");
        command.SelectList.Should().BeSameAs(selectListBefore, "grouped lowering must not mutate SelectList");
    }

    [Fact]
    public void ParameterDeterminism_GroupedMaxAndMin_IdenticalSqlAndParameterSequence()
    {
        _ctx.PurgeQueryCache();

        int capturedFlag = 1;
        var maxCommand = _ctx.From<CacheRow>()
            .Where(e => e.Flag == capturedFlag)
            .SelectWhereMax(e => e.Score, ExtremeRowTies.All, e => e.Grp)
            .ToCommand();
        var minCommand = _ctx.From<CacheRow>()
            .Where(e => e.Flag == capturedFlag)
            .SelectWhereMin(e => e.Score, ExtremeRowTies.All, e => e.Grp)
            .ToCommand();

        foreach (var command in new[] { maxCommand, minCommand })
        {
            var first = Prepare(_ctx, command, storeInCache: true);
            var sql1 = first.DbCommand.CommandText;
            var params1 = first.DbCommand.Parameters.Cast<DbParameter>()
                .Select(p => (p.ParameterName, p.Value)).ToList();

            var second = Prepare(_ctx, command, storeInCache: false);
            var sql2 = second.DbCommand.CommandText;
            var params2 = second.DbCommand.Parameters.Cast<DbParameter>()
                .Select(p => (p.ParameterName, p.Value)).ToList();

            sql2.Should().Be(sql1, "repeated grouped renders must produce identical SQL shape");
            params2.Should().Equal(params1, "repeated grouped renders must produce identical parameter name/value sequence");
            (sql1.Split(new[] { "$capturedFlag" }, StringSplitOptions.None).Length - 1)
                .Should().Be(2, "the declared condition is duplicated in the grouped form (E then O)");
            params1.Should().ContainSingle().Which.Value.Should().Be(1);
        }

        // Max and Min keep the same shape with the aggregate function swapped, never mixed.
        Prepare(_ctx, maxCommand, storeInCache: false).DbCommand.CommandText.ToLowerInvariant().Should().Contain("max(");
        Prepare(_ctx, minCommand, storeInCache: false).DbCommand.CommandText.ToLowerInvariant().Should().Contain("min(");
    }
}
