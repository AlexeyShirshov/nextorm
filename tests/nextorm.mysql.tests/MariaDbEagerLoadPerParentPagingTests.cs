using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.MySql.Tests;

/// <summary>
/// #211 per-parent top-N SQL generation on the MariaDB dialect. MariaDB shares the MySQL connection
/// behaviour but renders its own dialect, so the portable ranked per-parent paging lowering is proved
/// against <see cref="MariaDbDataContext"/> independently of the MySQL provider. These tests never open
/// a connection.
/// </summary>
[Trait("Feature", "D211")]
public sealed class MariaDbEagerLoadPerParentPagingTests
{
    private const string ConnectionString =
        "Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm";

    private static IDataContext CreateCtx() => new MariaDbDataContext(ConnectionString, new DataContextBuilder());

    private static string SqlOf(IDataContext ctx, QueryCommand cmd)
        => ((dynamic)ctx.GetPreparedQueryCommand((dynamic)cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void MariaDb_Split_SkipAndTake_RendersPartitionedRanking()
    {
        using var ctx = CreateCtx();

        var builder = ctx.From<EagerLoadPerParentPagingTests.D211Parent>().LoadWith(
            p => p.Children,
            c => c.From<EagerLoadPerParentPagingTests.D211Child>().Where(x => x.Flag == 1).OrderBy(x => x.SortKey).Offset(1).Limit(2),
            p => p.Id, c => c.ParentId);

        var spec = builder.LoadSpecs![0];
        _ = spec.ToJoinIntoSpec(ctx, QueryFilterScope.None, out var childSource);
        var child = childSource.SubQuery!;
        child.PerParentPagingEmbedded = false;
        var sql = SqlOf(ctx, child);

        var lower = sql.ToLowerInvariant();
        lower.Should().Contain("row_number()").And.Contain("over (").And.Contain("partition by parent_id");
        lower.Should().Contain("order by sort_key");
        sql.Should().Contain("t1.`__nextorm_rn` > 1");
        sql.Should().Contain("t1.`__nextorm_rn` <= 3");
        sql.TrimEnd().Should().EndWith("order by t1.sort_key");
    }

    [Fact]
    public void MariaDb_SingleQuery_NestedDerivedSourceHasNoStatementLevelOrderBy()
    {
        using var ctx = CreateCtx();

        var builder = ctx.From<EagerLoadPerParentPagingTests.D211Parent>().LoadWith(
            p => p.Children,
            c => c.From<EagerLoadPerParentPagingTests.D211Child>().OrderBy(x => x.SortKey).Offset(1).Limit(2),
            p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery).OrderBy(p => p.Id);

        var (specs, joins) = builder.BuildSingleQueryJoins();
        var sql = SqlOf(ctx, builder.CreatePairCommand(specs, joins));

        sql.ToLowerInvariant().Should().Contain("left join").And.Contain("row_number()").And.Contain("partition by parent_id");
        var lastRank = sql.LastIndexOf("__nextorm_rn", StringComparison.Ordinal);
        var derivedClose = sql.IndexOf(") as ", lastRank, StringComparison.OrdinalIgnoreCase);
        sql[lastRank..derivedClose].ToLowerInvariant().Should().NotContain("order by");
        sql.Should().Contain("order by t1.id, t3.`SortKey`");
    }

    [Theory]
    [InlineData("Distinct")]
    [InlineData("GroupBy")]
    public void MariaDb_SingleQuery_UnsupportedDerivedSourceShape_FailsClosed(string shape)
    {
        using var ctx = CreateCtx();

        var act = () => ctx.From<EagerLoadPerParentPagingTests.D211Parent>()
            .LoadWith(p => p.Children, c => UnsupportedChild(ctx, shape), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery)
            .BuildSingleQueryJoins();

        act.Should().Throw<NotSupportedException>().WithMessage($"*{shape}*");
    }

    private static EntityBuilder<EagerLoadPerParentPagingTests.D211Child> UnsupportedChild(IDataContext ctx, string shape) => shape switch
    {
        "Distinct" => ctx.From<EagerLoadPerParentPagingTests.D211Child>().Distinct().OrderBy(x => x.Id).Limit(1),
        "GroupBy" => ctx.From<EagerLoadPerParentPagingTests.D211Child>().GroupBy(x => x.ParentId).OrderBy(x => x.Id).Limit(1),
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "unknown child shape"),
    };
}
