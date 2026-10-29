using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// T323 (#323) D5b live parity: a <c>SelectGroup</c> projection and the equivalent explicit
/// <c>GroupBy(...).Select(...)</c> form must return the same materialised rows on every provider that
/// derives the shared suite (<c>SqliteIntegrationTests</c>, <c>PostgresIntegrationTests</c>,
/// <c>SqlServerIntegrationTests</c>, <c>MySqlIntegrationTests</c>). The fixture is class-owned
/// (<c>t323_sg_sales</c>, portable DDL): the keys 1, 2 and 3 plus a two-row NULL key, so the keyed,
/// global-aggregate, captured-HAVING and NULL-key cells all read the same seven rows. Group order is
/// undefined in SQL, so every cell compares both sides as sets. The facts carry the
/// <c>T323SelectGroup</c> prefix so both the unit selector and the whole-goal filter select them.
/// </summary>
public abstract partial class CommonTestSuite
{
    /// <summary>One sale line: an optional group key and a non-null amount.</summary>
    [SqlTable("t323_sg_sales")]
    public interface IT323Sales
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("grp")]
        int? Grp { get; set; }

        [Column("amount")]
        int Amount { get; set; }
    }

    private const string T323SalesDdl = "drop table if exists t323_sg_sales";

    private const string T323SalesCreate =
        "create table t323_sg_sales (id int not null primary key, grp int null, amount int not null)";

    private const string T323SalesSeed =
        "insert into t323_sg_sales (id, grp, amount) values "
        + "(1, 1, 10), (2, 1, 20), (3, 2, 30), (4, 2, 40), (5, 3, 50), (6, null, 60), (7, null, 70)";

    /// <summary>
    /// Keyed parity: three non-null keys plus one NULL key, with <c>sum</c> and <c>count</c> output
    /// members. The single-plus-NULL group facts are pinned by key, so both sides being equally wrong
    /// cannot satisfy the cell.
    /// </summary>
    [Fact]
    public void T323SelectGroup_ShouldMatchExplicitGrouping()
    {
        T323CreateSalesFixture();
        var ctx = _sut.DataProvider;

        var inferred = ctx.From<IT323Sales>()
            .SelectGroup(x => new
            {
                x.Grp,
                S = SqlFunctions.Sql.sum(x.Amount),
                C = SqlFunctions.Sql.count(),
            })
            .ToList();
        var explicitRows = ctx.From<IT323Sales>()
            .GroupBy(x => x.Grp)
            .Select(x => new
            {
                x.Grp,
                S = SqlFunctions.Sql.sum(x.Amount),
                C = SqlFunctions.Sql.count(),
            })
            .ToList();

        inferred.Should().HaveCount(4, "three non-null keys plus one NULL key form four groups");
        inferred.Should().BeEquivalentTo(explicitRows);

        var first = inferred.Single(g => g.Grp == 1);
        first.S.Should().Be(30);
        first.C.Should().Be(2);

        var third = inferred.Single(g => g.Grp == 3);
        third.S.Should().Be(50);
        third.C.Should().Be(1);

        var nullGroup = inferred.Single(g => g.Grp is null);
        nullGroup.S.Should().Be(130);
        nullGroup.C.Should().Be(2);
    }

    /// <summary>
    /// A key-less projection folds the whole filtered source into exactly one row, and that row
    /// carries the same values as the explicit key-less aggregate over the same source.
    /// </summary>
    [Fact]
    public void T323SelectGroup_GlobalAggregate_ShouldReturnOneRow()
    {
        T323CreateSalesFixture();
        var ctx = _sut.DataProvider;

        var inferred = ctx.From<IT323Sales>()
            .SelectGroup(x => new
            {
                S = SqlFunctions.Sql.sum(x.Amount),
                C = SqlFunctions.Sql.count(),
            })
            .ToList();
        var explicitRows = ctx.From<IT323Sales>()
            .Select(x => new
            {
                S = SqlFunctions.Sql.sum(x.Amount),
                C = SqlFunctions.Sql.count(),
            })
            .ToList();

        inferred.Should().ContainSingle("a global aggregate emits no GROUP BY and returns one row");
        inferred.Should().BeEquivalentTo(explicitRows);
        inferred[0].S.Should().Be(280);
        inferred[0].C.Should().Be(7);
    }

    /// <summary>
    /// A captured threshold filters whole groups through <c>HAVING</c>. The capture is a local, not a
    /// <c>const</c>, so it binds as a parameter instead of compiling into the SQL text.
    /// </summary>
    [Fact]
    public void T323SelectGroup_HavingCapturedThreshold_ShouldFilterGroups()
    {
        T323CreateSalesFixture();
        var ctx = _sut.DataProvider;
        var threshold = 60;

        var inferred = ctx.From<IT323Sales>()
            .Having(x => SqlFunctions.Sql.sum(x.Amount) > threshold)
            .SelectGroup(x => new
            {
                x.Grp,
                S = SqlFunctions.Sql.sum(x.Amount),
            })
            .ToList();
        var explicitRows = ctx.From<IT323Sales>()
            .GroupBy(x => x.Grp)
            .Having(x => SqlFunctions.Sql.sum(x.Amount) > threshold)
            .Select(x => new
            {
                x.Grp,
                S = SqlFunctions.Sql.sum(x.Amount),
            })
            .ToList();

        inferred.Should().HaveCount(2, "only the group sums 70 and 130 exceed the captured threshold");
        inferred.Should().BeEquivalentTo(explicitRows);
        inferred.Should().OnlyContain(g => g.S > threshold);
        inferred.Should().NotContain(g => g.Grp == 1 || g.Grp == 3);
    }

    /// <summary>
    /// A NULL key value forms exactly one group (never one group per row), and that group aggregates
    /// both of its rows the same way the explicit form does.
    /// </summary>
    [Fact]
    public void T323SelectGroup_NullKey_ShouldGroupOnce()
    {
        T323CreateSalesFixture();
        var ctx = _sut.DataProvider;

        var inferred = ctx.From<IT323Sales>()
            .SelectGroup(x => new
            {
                x.Grp,
                S = SqlFunctions.Sql.sum(x.Amount),
                C = SqlFunctions.Sql.count(),
            })
            .ToList();
        var explicitRows = ctx.From<IT323Sales>()
            .GroupBy(x => x.Grp)
            .Select(x => new
            {
                x.Grp,
                S = SqlFunctions.Sql.sum(x.Amount),
                C = SqlFunctions.Sql.count(),
            })
            .ToList();

        inferred.Count(g => g.Grp is null).Should().Be(1, "a NULL key value forms exactly one group");

        var nullGroup = inferred.Single(g => g.Grp is null);
        nullGroup.S.Should().Be(130);
        nullGroup.C.Should().Be(2);

        inferred.Should().BeEquivalentTo(explicitRows);
    }

    private void T323CreateSalesFixture()
    {
        var ctx = _sut.DataProvider;
        T323Execute(ctx, T323SalesDdl);
        T323Execute(ctx, T323SalesCreate);
        T323Execute(ctx, T323SalesSeed);
    }

    private static void T323Execute(IDataContext ctx, string sql)
    {
        var dataContext = (DataContext)ctx;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.CreateCommand(sql);
        command.ExecuteNonQuery();
    }
}
