using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Translation of a captured collection indexed from a query (<c>dict[column]</c>): a portable
/// <c>CASE WHEN</c> over the evaluated entries, a parameter fold for a constant key, and an explicit
/// rejection outside the prepared condition. These tests only build SQL; they never open a connection.
/// </summary>
public class DictionaryLookupSqlGenerationTests
{
    private static Dictionary<int, DateTime> CapturedLookup = new()
    {
        [1] = new DateTime(2024, 1, 1),
        [2] = new DateTime(2024, 2, 1),
    };

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void Dictionary_IndexedByColumn_ShouldEmitCase()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => CapturedLookup[x.Int!.Value] < x.Datetime).Select(x => x.Id));

        sql.Should().Be(
            "select id from complex_entity\n where (case when nullableint = $p0 then $p1 when nullableint = $p2 then $p3 end < dt)");
    }

    [Fact]
    public void Dictionary_IndexedByColumn_ShouldBindKeyValueParameters()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var prepared = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(x => CapturedLookup[x.Int!.Value] < x.Datetime).Select(x => x.Id), false, false, CancellationToken.None);

        prepared.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName).Should().Equal("p0", "p1", "p2", "p3");
        prepared.DbCommandParams[0].Value.Should().Be(1);
        prepared.DbCommandParams[1].Value.Should().Be(new DateTime(2024, 1, 1));
        prepared.DbCommandParams[2].Value.Should().Be(2);
        prepared.DbCommandParams[3].Value.Should().Be(new DateTime(2024, 2, 1));
    }

    [Fact]
    public void List_IndexedByColumn_ShouldEmitCaseOverIndices()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var lookup = new List<DateTime> { new(2024, 1, 1), new(2024, 2, 1) };
        var sql = SqlOf(ctx, e.Where(x => lookup[x.Int!.Value] < x.Datetime).Select(x => x.Id));

        sql.Should().Be(
            "select id from complex_entity\n where (case when nullableint = $p0 then $p1 when nullableint = $p2 then $p3 end < dt)");
    }

    [Fact]
    public void Dictionary_IndexedByConstant_ShouldFoldToParameter()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var prepared = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(x => CapturedLookup[2] < x.Datetime).Select(x => x.Id), false, false, CancellationToken.None);

        var sql = prepared.DbCommand.CommandText.Replace("\r\n", "\n");
        sql.Should().NotContain("case");
        sql.Should().Be("select id from complex_entity\n where ($p0 < dt)");
        prepared.DbCommandParams[0].Value.Should().Be(new DateTime(2024, 2, 1));
    }

    [Fact]
    public void Dictionary_IndexedOutsideWhere_OnCacheableCommand_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => ctx.GetPreparedQueryCommand(
            e.Select(x => new { Value = CapturedLookup[x.Int!.Value] }), false, true, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage("*WHERE*");
    }

    [Fact]
    public void Dictionary_SameShape_ShouldReuseCachedPlan()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        var first = ctx.GetPreparedQueryCommand(
            e.Where(x => CapturedLookup[x.Int!.Value] < x.Datetime).Select(x => x.Id), false, true, CancellationToken.None);
        var second = ctx.GetPreparedQueryCommand(
            e.Where(x => CapturedLookup[x.Int!.Value] < x.Datetime).Select(x => x.Id), false, true, CancellationToken.None);

        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void Dictionary_IndexedByExpressionKey_ShouldKeepParametersAligned()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();
        var lookup = new Dictionary<int, int> { [1] = 10, [2] = 20 };
        var bias = 0;

        var cmd = e.Where(x => lookup[x.Int!.Value + bias] == 20).Select(x => x.Id);
        var first = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
        var second = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

        // The cache hit re-extracts parameters in parameter mode; the captured `bias` inside the key must
        // be collected there too or every parameter after it shifts (Debug.Assert in QueryPlanner).
        ReferenceEquals(first, second).Should().BeTrue();
        second.DbCommandParams.Cast<DbParameter>().Select(p => p.ParameterName).Should().Equal("bias", "p0", "p1", "p2", "p3");
        second.DbCommandParams[0].Value.Should().Be(0);
        second.DbCommandParams[1].Value.Should().Be(1);
        second.DbCommandParams[2].Value.Should().Be(10);
        second.DbCommandParams[3].Value.Should().Be(2);
        second.DbCommandParams[4].Value.Should().Be(20);
    }

    [Fact]
    public void Dictionary_PreparedWithoutHashThenCached_ShouldTranslate()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();
        var lookup = new Dictionary<int, int> { [1] = 10 };

        var cmd = e.Where(x => lookup[x.Int!.Value] == 10).Select(x => x.Id);

        // First pass without plan-cache hashing (storeInCache:false), then the same command reused through
        // the cache: the shape is folded in on reuse, so the lookup is not mistaken for an out-of-condition one.
        ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        var act = () => ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);

        act.Should().NotThrow();
    }

    [Fact]
    public void Dictionary_GrownBetweenBuilds_ShouldNotReuseStalePlan()
    {
        using var ctx = SqliteTestContext.Create();
        ctx.PurgeQueryCache();
        var e = ctx.From<IComplexEntity>();

        CapturedLookup = new Dictionary<int, DateTime> { [1] = new DateTime(2024, 1, 1) };
        var first = ctx.GetPreparedQueryCommand(
            e.Where(x => CapturedLookup[x.Int!.Value] < x.Datetime).Select(x => x.Id), false, true, CancellationToken.None);

        CapturedLookup = new Dictionary<int, DateTime>
        {
            [1] = new DateTime(2024, 1, 1),
            [2] = new DateTime(2024, 2, 1),
        };
        var second = ctx.GetPreparedQueryCommand(
            e.Where(x => CapturedLookup[x.Int!.Value] < x.Datetime).Select(x => x.Id), false, true, CancellationToken.None);

        ReferenceEquals(first, second).Should().BeFalse();
        ((DbPreparedQueryCommand<long>)second).DbCommandParams.Count.Should().Be(4);
    }

    [Fact]
    public void ReadOnlyList_IndexedByColumn_ShouldEmitCase()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        IReadOnlyList<int> lookup = [10, 20, 30];

        var sql = SqlOf(ctx, e.Where(x => lookup[x.Int!.Value] == 20).Select(x => x.Id));

        sql.Should().Contain("case when nullableint = $p0 then $p1 when nullableint = $p2 then $p3 when nullableint = $p4 then $p5 end");
    }

    [Fact]
    public void ReadOnlyList_IndexedByConstant_ShouldFoldToParameter()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        IReadOnlyList<int> lookup = [10, 20, 30];

        var prepared = (DbPreparedQueryCommand<long>)ctx.GetPreparedQueryCommand(
            e.Where(x => lookup[0] == 10).Select(x => x.Id), false, false, CancellationToken.None);

        prepared.DbCommand.CommandText.Replace("\r\n", "\n").Should().NotContain("case");
        prepared.DbCommandParams[0].Value.Should().Be(10);
    }
}
