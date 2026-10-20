using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;

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

/// <summary>
/// T212-D7 row D: MariaDB inherits the MySQL dialect, so a nested (level 2+) single-query LoadWith must
/// render the same level-per-join chain as MySQL and reject an inexpressible nested child shape before
/// SQL. A distinct nested rendering would block the inherited-parity claim.
/// </summary>
[Trait("Feature", "T212-D7")]
public sealed class MariaDbNestedEagerLoadingTests
{
    private const string ConnectionString =
        "Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm";

    private static IDataContext CreateMySql() => new MySqlDataContext(ConnectionString, new DataContextBuilder());

    private static IDataContext CreateMariaDb() => new MariaDbDataContext(ConnectionString, new DataContextBuilder());

    private static string SqlOf(IDataContext ctx, QueryCommand cmd)
        => ((dynamic)ctx.GetPreparedQueryCommand((dynamic)cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    private static EntityBuilder<EagerStratNestedParent> Nested(IDataContext ctx)
        => ctx.From<EagerStratNestedParent>(b => b.HasMany(p => p.Mids, m => m.ParentId))
            .LoadWith(
                p => p.Mids,
                c => c.From<EagerStratNestedMid>().LoadWith(m => m.Leaves, l => l.From<EagerStratNestedLeaf>(), m => m.Id, l => l.MidId),
                p => p.Id,
                m => m.ParentId,
                EagerLoadMode.SingleQuery);

    [Fact]
    public void MariaDb_NestedSingleQuery_RendersTheSameChainAsMySql()
    {
        using var mysql = CreateMySql();
        using var maria = CreateMariaDb();

        var myBuilder = Nested(mysql);
        var (mySpecs, myJoins) = myBuilder.BuildSingleQueryJoins();
        var mySql = SqlOf(mysql, myBuilder.CreatePairCommand(mySpecs, myJoins));

        var mariaBuilder = Nested(maria);
        var (mariaSpecs, mariaJoins) = mariaBuilder.BuildSingleQueryJoins();
        var mariaSql = SqlOf(maria, mariaBuilder.CreatePairCommand(mariaSpecs, mariaJoins));

        mariaSql.Should().Be(mySql, "MariaDB inherits the MySQL dialect; the nested chain must not diverge");
    }

    [Fact]
    public void MariaDb_NestedSingleQuery_InexpressibleGrandChildShape_FailsClosed()
    {
        using var ctx = CreateMariaDb();

        var builder = ctx.From<EagerStratNestedParent>(b => b.HasMany(p => p.Mids, m => m.ParentId))
            .LoadWith(
                p => p.Mids,
                c => c.From<EagerStratNestedMid>().LoadWith(m => m.Leaves, l => l.From<EagerStratNestedLeaf>().Distinct(), m => m.Id, l => l.MidId),
                p => p.Id,
                m => m.ParentId,
                EagerLoadMode.SingleQuery);

        var act = () => builder.BuildSingleQueryJoins();

        act.Should().Throw<NotSupportedException>().WithMessage("*Distinct*");
    }
}
