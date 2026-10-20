using System.Linq.Expressions;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Postgres;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// D240-3 (#240): the <see cref="EntityBuilder{TEntity}"/> fluent-construction path. Proves that every
/// modifier returns a NEW builder and leaves the receiver's values and generated SQL unchanged, that two
/// sibling branches from one parent stay independent — including the shared ordering/window lists, which
/// are detached before the first write — that a clone of a collection-free builder holds no empty lists,
/// and that repeated/nested modifiers and null toggles keep each intermediate independent. Trait
/// <c>Category=T240Builder</c> is the unit's inner-loop selector.
/// </summary>
[Trait("Category", "T240Builder")]
public class T240BuilderConstructionTests
{
    private const string SqlServerPlaceholder =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    private static SqlServerDataContext NewContext() => new(SqlServerPlaceholder, new DataContextBuilder());

    private static PostgresDataContext NewPostgresContext() => new("Host=localhost", new DataContextBuilder());

    private static ClickHouseDataContext NewClickHouseContext() => new("Host=localhost", new DataContextBuilder());

    /// <summary>Renders the generated SQL for a command without ever opening a connection.</summary>
    private static string SqlOf<T>(IDataContext context, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext context, EntityBuilder<T> builder)
        => SqlOf(context, builder.ToCommand());

    // ---- (a) a modifier leaves the previous builder unchanged (values AND generated SQL) -------------

    [Fact]
    public void Where_ReturnsNewBuilderAndLeavesParentConditionAndSqlUnchanged()
    {
        using var ctx = NewContext();
        var parent = ctx.From<SimpleEntity>();
        var parentSql = SqlOf(ctx, parent);

        var child = parent.Where(x => x.Id > 5);

        child.Should().NotBeSameAs(parent);
        parent.Condition.Should().BeNull();
        child.Condition.Should().NotBeNull();
        SqlOf(ctx, parent).Should().Be(parentSql);
        SqlOf(ctx, child).Should().NotBe(parentSql);
    }

    [Fact]
    public void OrderBy_ReturnsNewBuilderAndLeavesParentSortingAndSqlUnchanged()
    {
        using var ctx = NewContext();
        var parent = ctx.From<SimpleEntity>().Where(x => x.Id > 0);
        var parentSql = SqlOf(ctx, parent);

        var child = parent.OrderBy(x => x.Id);

        child.Should().NotBeSameAs(parent);
        parent.Sorting.Should().BeNull();
        child.Sorting.Should().NotBeNull().And.HaveCount(1);
        SqlOf(ctx, parent).Should().Be(parentSql);
        SqlOf(ctx, child).Should().NotBe(parentSql);
    }

    // ---- (b) sibling branches from one parent stay independent ---------------------------------------

    [Fact]
    public void SecondOrderBy_DoesNotGrowTheParentOrFirstBranch()
    {
        using var ctx = NewContext();
        var root = ctx.From<SimpleEntity>();

        var first = root.OrderBy(x => x.Id);
        var second = first.OrderBy(x => x.Id);

        // The ordering list is shared with a clone but detached before the append: neither the root nor
        // the first branch may observe the second key.
        root.Sorting.Should().BeNull();
        first.Sorting.Should().NotBeNull().And.HaveCount(1);
        second.Sorting.Should().NotBeNull().And.HaveCount(2);
    }

    [Fact]
    public void SiblingOrderByBranches_StayIndependentAndRenderDifferentSql()
    {
        using var ctx = NewContext();
        var parent = ctx.From<SimpleEntity>().Where(x => x.Id > 0);

        var ascending = parent.OrderBy(x => x.Id);
        var descending = parent.OrderByDescending(x => x.Id);

        parent.Sorting.Should().BeNull();
        ascending.Sorting.Should().NotBeNull().And.HaveCount(1);
        descending.Sorting.Should().NotBeNull().And.HaveCount(1);
        SqlOf(ctx, ascending).Should().NotBe(SqlOf(ctx, descending));
    }

    [Fact]
    public void SiblingWindowBranches_StayIndependent()
    {
        using var ctx = NewPostgresContext();
        var parent = ctx.From<SimpleEntity>();

        var first = parent.Window("w1", partitionBy: [x => x.Id]);
        var second = parent.Window("w2");

        parent.Windows.Should().BeNull();
        first.Windows.Should().NotBeNull().And.HaveCount(1);
        second.Windows.Should().NotBeNull().And.HaveCount(1);
        first.Windows![0].Name.Should().Be("w1");
        second.Windows![0].Name.Should().Be("w2");

        // The window list is shared with the clone; appending detaches, so the first branch keeps one.
        var extended = first.Window("w3");
        first.Windows.Should().HaveCount(1);
        extended.Windows.Should().HaveCount(2);
    }

    // ---- (c) a collection-free clone holds no empty lists --------------------------------------------

    [Fact]
    public void Clone_OfCollectionFreeBuilder_HoldsNoEmptyCollections()
    {
        using var ctx = NewContext();
        var parent = ctx.From<SimpleEntity>().Where(x => x.Id > 0);

        var clone = parent.Clone();

        clone.Should().NotBeSameAs(parent);
        clone.Condition.Should().NotBeNull();
        clone.Windows.Should().BeNull();
        clone.LoadSpecs.Should().BeNull();
        clone.JoinIntos.Should().BeNull();
        clone.Joins.Should().BeNull();
        clone.ArrayJoins.Should().BeNull();
        clone.SettingsList.Should().BeNull();
        clone.Sorting.Should().BeNull();
    }

    [Fact]
    public void Clone_SharesTheWindowListInsteadOfDuplicatingItPerStep()
    {
        using var ctx = NewPostgresContext();
        var parent = ctx.From<SimpleEntity>().Window("w1");

        var clone = parent.Clone();

        // No writer mutates _windows in place outside Window(), so a clone shares the instance rather
        // than copying it; Window() detaches before its first append (covered above).
        clone.Windows.Should().BeSameAs(parent.Windows);
    }

    // ---- (d) repeated / nested modifiers keep every intermediate independent -------------------------

    [Fact]
    public void RepeatedModifiers_KeepEachIntermediateIndependent()
    {
        using var ctx = NewContext();
        var b0 = ctx.From<SimpleEntity>();
        var b1 = b0.Where(x => x.Id > 0);
        var b2 = b1.OrderBy(x => x.Id);
        var b3 = b2.Limit(3);
        var b4 = b3.Offset(1);

        b0.Condition.Should().BeNull();
        b0.Sorting.Should().BeNull();
        b1.Condition.Should().NotBeNull();
        b1.Sorting.Should().BeNull();
        b2.Sorting.Should().NotBeNull().And.HaveCount(1);
        b2.Paging.Limit.Should().Be(0);
        b3.Paging.Limit.Should().Be(3);
        b3.Paging.Offset.Should().Be(0);
        b4.Paging.Limit.Should().Be(3);
        b4.Paging.Offset.Should().Be(1);

        b1.Should().NotBeSameAs(b0);
        b2.Should().NotBeSameAs(b1);
        b3.Should().NotBeSameAs(b2);
        b4.Should().NotBeSameAs(b3);
    }

    // ---- (e) null / empty toggles --------------------------------------------------------------------

    [Fact]
    public void NullWhere_ClearsThePredicateOnTheCopyOnly()
    {
        using var ctx = NewContext();
        var withCondition = ctx.From<SimpleEntity>().Where(x => x.Id > 0);

        var cleared = withCondition.Where(null!);

        withCondition.Condition.Should().NotBeNull();
        cleared.Condition.Should().BeNull();
        cleared.Should().NotBeSameAs(withCondition);
    }

    [Fact]
    public void NullHaving_ClearsThePredicateOnTheCopyOnly()
    {
        using var ctx = NewContext();
        var withHaving = ctx.From<SimpleEntity>().Having(x => x.Id > 0);

        var cleared = withHaving.Having(null!);

        withHaving.HavingCondition.Should().NotBeNull();
        cleared.HavingCondition.Should().BeNull();
    }

    // ---- (f) provider-extension append paths must not alias a sibling ---------------------------------

    [Fact]
    public void Clone_OfBuilderWithJoins_GivesTheCopyItsOwnJoinList()
    {
        using var ctx = NewContext();
        var join = new JoinExpression(null, JoinType.Cross) { From = new FromExpression(typeof(SimpleEntity)) };
        var parent = ctx.From<SimpleEntity>();
        parent.Joins = [join];

        var copy = parent.Clone();

        // _joins stays copied: provider extensions append in place after a clone, so the copy must own its
        // own list.
        copy.Joins.Should().NotBeSameAs(parent.Joins);
        copy.Joins![0].Should().BeSameAs(join);

        copy.Joins.Add(new JoinExpression(null, JoinType.Inner) { From = new FromExpression(typeof(SimpleEntity)) });
        parent.Joins.Should().HaveCount(1);
        copy.Joins.Should().HaveCount(2);
    }

    [Fact]
    public void ClickHouseSettingsSiblings_DoNotShareTheSettingsList()
    {
        using var ctx = NewClickHouseContext();
        var parent = ctx.From<SimpleEntity>();

        var first = parent.Settings(("a", "1"));
        var second = parent.Settings(("b", "2"));

        parent.SettingsList.Should().BeNull();
        first.SettingsList.Should().NotBeNull().And.HaveCount(1);
        second.SettingsList.Should().NotBeNull().And.HaveCount(1);
    }

    [Fact]
    public void ClickHouseArrayJoinSiblings_DoNotShareTheArrayJoinList()
    {
        using var ctx = NewClickHouseContext();
        var parent = ctx.From<SimpleEntity>();

        var first = parent.ArrayJoin(x => new[] { x.Id });
        var second = parent.LeftArrayJoin(x => new[] { x.Id });
        var extended = first.ArrayJoin(x => new[] { x.Id });

        parent.ArrayJoins.Should().BeNull();
        first.ArrayJoins.Should().NotBeNull().And.HaveCount(1);
        second.ArrayJoins.Should().NotBeNull().And.HaveCount(1);
        extended.ArrayJoins.Should().NotBeNull().And.HaveCount(2);
    }
}
