using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Issue #160 (cycle N=2, r=1) mutation-gap tests. They pin the behaviour the first Stryker pass
/// reported as <c>Survived</c>/<c>NoCoverage</c> inside the #160-changed regions, all using the
/// provider-free <see cref="RootProjectionTests.TestContext"/> (fake dialect) so the planner and SQL
/// renderer are exercised without a database.
/// <para>
/// Covered seams: the <c>ProjectionAliasCache.GetItem1Property</c> memoisation; the <c>AliasRoot</c>
/// argument guard; the pre-<c>.WithAlias</c> query-state rebasing on <c>AliasRoot</c> itself and the
/// re-basing performed when an alias join follows (OrderBy/GroupBy/Having/Paging).
/// Runs in the "Query cache controls" collection (parallelization disabled) because the shared
/// process-wide plan cache is read.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public class JoinAliasMutationGapTests
{
    /// <summary>
    /// A root projection type used only by this fixture. <c>ProjectionAliasCache</c> memoizes the
    /// resolved <c>Item1</c> per closed projection type, so the type must not be shared with another
    /// test: otherwise the cache is populated before this test runs and the resolution lambda — the
    /// code under mutation — never executes here.
    /// </summary>
    private sealed class GapRootProjection<T> : Projection<T>
    {
    }

    /// <summary>Parent with a declared collection navigation, used for the eager-load guard.</summary>
    private sealed class GapParent
    {
        [System.ComponentModel.DataAnnotations.Key]
        public int Id { get; set; }

        public ICollection<GapChild> Children { get; } = new List<GapChild>();
    }

    /// <summary>Child of <see cref="GapParent"/>.</summary>
    private sealed class GapChild
    {
        [System.ComponentModel.DataAnnotations.Key]
        public int Id { get; set; }

        public int ParentId { get; set; }
    }

    private static EntityBuilder<RootProjectionTests.RootProjection<RootProjectionTests.Order>> AliasRoot(
        EntityBuilder<RootProjectionTests.Order> source) =>
        source.AliasRoot<
            EntityBuilder<RootProjectionTests.RootProjection<RootProjectionTests.Order>>,
            RootProjectionTests.RootProjection<RootProjectionTests.Order>>(static dc => new(dc));

    // ------------------------------------------------------------------ Item1 memoisation ---------

    [Fact]
    public void GetItem1Property_resolves_public_instance_item1_and_memoizes_the_result()
    {
        var projectionType = typeof(GapRootProjection<RootProjectionTests.Order>);

        var first = ProjectionAliasCache.GetItem1Property(projectionType);
        var second = ProjectionAliasCache.GetItem1Property(projectionType);

        // Resolving must find the inherited public instance Item1 (not a static, not nothing).
        first.Should().NotBeNull();
        first!.Name.Should().Be("Item1");
        first.GetMethod.Should().NotBeNull();
        first.GetMethod!.IsStatic.Should().BeFalse();

        // The same closed projection type must return the same PropertyInfo instance (memoized).
        second.Should().BeSameAs(first);

        // A type without Item1 is a stable null result (AliasRoot turns it into a fail-closed error).
        ProjectionAliasCache.GetItem1Property(typeof(RootProjectionTests.Order)).Should().BeNull();
    }

    // ------------------------------------------------------------------ AliasRoot guards ----------

    [Fact]
    public void AliasRoot_rejects_a_null_factory_before_touching_the_provider()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<RootProjectionTests.Order>()
            .AliasRoot<
                EntityBuilder<RootProjectionTests.RootProjection<RootProjectionTests.Order>>,
                RootProjectionTests.RootProjection<RootProjectionTests.Order>>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AliasRoot_is_refused_after_eager_load_state_is_declared()
    {
        using var ctx = new RootProjectionTests.TestContext();

        var parent = ctx.From<GapParent>(b => b.HasMany(p => p.Children, c => c.ParentId))
            .LoadWith(p => p.Children, c => c.From<GapChild>(), p => p.Id, c => c.ParentId);

        Action act = () => parent.AliasRoot<
            EntityBuilder<RootProjectionTests.RootProjection<GapParent>>,
            RootProjectionTests.RootProjection<GapParent>>(static dc => new(dc));

        act.Should().Throw<NotSupportedException>().WithMessage("*LoadWith*WithAlias*");
    }

    [Fact]
    public void AliasRoot_is_refused_after_a_named_window_is_declared()
    {
        using var ctx = new RootProjectionTests.TestContext();
        var orders = ctx.From<RootProjectionTests.Order>();
        var windowed = orders.Window("w", orderBy: [orders.Asc(o => o.Id)]);

        Action act = () => AliasRoot(windowed);

        // The guard must carry its diagnostic message/context, not an empty or generic exception: the
        // mutation run reported the message string literal as surviving.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Named windows must be declared after joins*Window cannot be combined*");
    }

    // ------------------------------------------------------------------ AliasRoot rebasing --------

    [Fact]
    public void AliasRoot_preserves_order_by_applied_before_it()
    {
        using var ctx = new RootProjectionTests.TestContext();

        var sql = RootProjectionTests.SqlOf(ctx,
            AliasRoot(ctx.From<RootProjectionTests.Order>().OrderBy(o => o.Id)).Select(p => p.Root.Id));

        sql.Should().Contain("order by Id");
    }

    [Fact]
    public void AliasRoot_preserves_group_by_and_having_applied_before_it()
    {
        using var ctx = new RootProjectionTests.TestContext();

        var sql = RootProjectionTests.SqlOf(ctx,
            AliasRoot(ctx.From<RootProjectionTests.Order>()
                .GroupBy(o => o.BuyerId)
                .Having(o => SqlFunctions.Sql.count() > 1))
            .Select(p => p.Root.BuyerId));

        sql.Should().Contain("group by buyer_id");
        sql.Should().Contain("having (count(*) > 1)");
    }

    // ------------------------------------------------------------------ pre-alias seam + join -----

    [Fact]
    public void Alias_join_rebases_order_by_applied_before_the_alias()
    {
        using var ctx = new RootProjectionTests.TestContext();
        var people = ctx.From<RootProjectionTests.Person>();

        var rooted = AliasRoot(ctx.From<RootProjectionTests.Order>().OrderBy(o => o.Id));

        var joined = rooted.JoinAlias<
            EntityBuilder<RootProjectionTests.RootJoinProjection<RootProjectionTests.Order, RootProjectionTests.Person>>,
            RootProjectionTests.RootJoinProjection<RootProjectionTests.Order, RootProjectionTests.Person>,
            RootProjectionTests.Person>(
            static dc => new(dc),
            people,
            (a, b) => a.Root.BuyerId == b.Id);

        var sql = RootProjectionTests.SqlOf(ctx, joined.Select(p => p.Buyer.Id));

        // The pre-alias OrderBy must survive the join, rebased onto the root slot t1; dropping it
        // would silently lose the modifier written before .WithAlias.
        sql.Should().Contain("order by t1.Id");
    }

    [Fact]
    public void Alias_join_rebases_group_by_applied_before_the_alias()
    {
        using var ctx = new RootProjectionTests.TestContext();
        var people = ctx.From<RootProjectionTests.Person>();

        var rooted = AliasRoot(ctx.From<RootProjectionTests.Order>().GroupBy(o => o.BuyerId));

        var joined = rooted.JoinAlias<
            EntityBuilder<RootProjectionTests.RootJoinProjection<RootProjectionTests.Order, RootProjectionTests.Person>>,
            RootProjectionTests.RootJoinProjection<RootProjectionTests.Order, RootProjectionTests.Person>,
            RootProjectionTests.Person>(
            static dc => new(dc),
            people,
            (a, b) => a.Root.BuyerId == b.Id);

        var sql = RootProjectionTests.SqlOf(ctx, joined.Select(p => p.Buyer.Id));

        sql.Should().Contain("group by t1.buyer_id");
    }

    // ------------------------------------------------- descending rebasing (Direction preserved) --

    [Fact]
    public void AliasRoot_preserves_a_descending_order_by_applied_before_it()
    {
        using var ctx = new RootProjectionTests.TestContext();

        var sql = RootProjectionTests.SqlOf(ctx,
            AliasRoot(ctx.From<RootProjectionTests.Order>().OrderByDescending(o => o.Id)).Select(p => p.Root.Id));

        // The rebase must carry the direction, not just the key: a lost Direction defaults to Asc and
        // silently flips the caller's descending order.
        sql.Should().Contain("order by Id desc");
    }

    [Fact]
    public void Alias_join_rebases_a_descending_order_by_applied_before_the_alias()
    {
        using var ctx = new RootProjectionTests.TestContext();
        var people = ctx.From<RootProjectionTests.Person>();

        var rooted = AliasRoot(ctx.From<RootProjectionTests.Order>().OrderByDescending(o => o.Id));

        var joined = rooted.JoinAlias<
            EntityBuilder<RootProjectionTests.RootJoinProjection<RootProjectionTests.Order, RootProjectionTests.Person>>,
            RootProjectionTests.RootJoinProjection<RootProjectionTests.Order, RootProjectionTests.Person>,
            RootProjectionTests.Person>(
            static dc => new(dc),
            people,
            (a, b) => a.Root.BuyerId == b.Id);

        var sql = RootProjectionTests.SqlOf(ctx, joined.Select(p => p.Buyer.Id));

        // As above, but through the alias-join seam: descending must survive the rebase onto t1.
        sql.Should().Contain("order by t1.Id desc");
    }

    // ------------------------------------------------- derived-root rebasing (Query -> From) ------

    [Fact]
    public void AliasRoot_on_a_derived_root_preserves_the_derived_source_and_aliases_it()
    {
        using var ctx = new RootProjectionTests.TestContext();

        // A derived root (From(builder) / From(QueryCommand<T>)) is carried as Query; re-rooting must
        // materialize it as an explicit derived-table FromExpression (and clear Query) so the physical
        // derived source, not the projection type, resolves and is aliased 't1'.
        var derived = ctx.From(ctx.From<RootProjectionTests.Order>().Where(o => o.Id > 0));

        var rooted = derived.AliasRoot<
            EntityBuilder<RootProjectionTests.RootProjection<RootProjectionTests.Order>>,
            RootProjectionTests.RootProjection<RootProjectionTests.Order>>(static dc => new(dc));

        var sql = RootProjectionTests.SqlOf(ctx, rooted.Select(p => p.Root.Id));

        sql.Should().Contain("from (select Id, buyer_id as 'BuyerId' from orders");
        sql.Should().Contain(") as 't1'");
    }

    [Fact]
    public void AliasRoot_on_a_derived_QueryCommand_root_preserves_the_derived_source()
    {
        using var ctx = new RootProjectionTests.TestContext();

        // The QueryCommand overload takes the same derived-root branch as From(builder).
        QueryCommand<int> inner = ctx.From<RootProjectionTests.Order>().Select(o => o.Id);

        var rooted = ctx.From(inner).AliasRoot<
            EntityBuilder<RootProjectionTests.RootProjection<int>>,
            RootProjectionTests.RootProjection<int>>(static dc => new(dc));

        var sql = RootProjectionTests.SqlOf(ctx, rooted.Select(p => p.Root));

        sql.Should().Contain("from (select Id from orders) as 't1'");
    }

    // ------------------------------------------------- having/prewhere-only root rebasing --------

    [Fact]
    public void AliasRoot_having_only_is_rebased_onto_the_root_slot()
    {
        using var ctx = new RootProjectionTests.TestContext();

        // Only Having is set before '.WithAlias' (no GroupBy): the GroupBy is added after re-rooting so
        // the rendered HAVING is observable. ApplyJoinStateTo deliberately does not copy `_having`, so if
        // the root seam skipped the `_having` disjunct the clause would be dropped from the result.
        var rooted = AliasRoot(ctx.From<RootProjectionTests.Order>()
                .Having(o => SqlFunctions.Sql.count() > 1))
            .GroupBy(p => p.Root.BuyerId);

        var sql = RootProjectionTests.SqlOf(ctx, rooted.Select(p => p.Root.BuyerId));

        sql.Should().Contain("group by");
        sql.Should().Contain("having (count(*) > 1)");
    }

    [Fact]
    public void AliasRoot_prewhere_only_is_rebased_onto_the_root_slot()
    {
        using var ctx = new RootProjectionTests.TestContext();

        var source = ctx.From<RootProjectionTests.Order>();
        source.PreWhereCondition = (Expression<Func<RootProjectionTests.Order, bool>>)(o => o.Id > 0);

        var rooted = AliasRoot(source);
        var command = rooted.Select(p => p.Root.Id);

        // PreWhere is copied verbatim by ApplyJoinStateTo, so a skipped rebase is observable in the
        // predicate's type: the seam must retype it over the root projection (Item1).
        command.PreWhere.Should().NotBeNull("the pre-'.WithAlias' PreWhere must survive the re-rooting");
        command.PreWhere!.Parameters[0].Type.Should().Be(
            typeof(RootProjectionTests.RootProjection<RootProjectionTests.Order>));
        command.PreWhere.Body.ToString().Should().Contain("Item1");
    }

    // ------------------------------------------------- rebasing through an alias join -------------

    [Fact]
    public void Alias_join_rebases_having_applied_before_the_alias()
    {
        using var ctx = new RootProjectionTests.TestContext();
        var people = ctx.From<RootProjectionTests.Person>();

        var rooted = AliasRoot(ctx.From<RootProjectionTests.Order>()
                .Having(o => SqlFunctions.Sql.count() > 1))
            .GroupBy(p => p.Root.BuyerId);

        var joined = rooted.JoinAlias<
            EntityBuilder<RootProjectionTests.RootJoinProjection<RootProjectionTests.Order, RootProjectionTests.Person>>,
            RootProjectionTests.RootJoinProjection<RootProjectionTests.Order, RootProjectionTests.Person>,
            RootProjectionTests.Person>(
            static dc => new(dc),
            people,
            (a, b) => a.Root.BuyerId == b.Id);

        var sql = RootProjectionTests.SqlOf(ctx, joined.Select(p => p.Buyer.Id));

        // `_having` is not copied by ApplyJoinStateTo, so the alias-join seam must rebase it onto the
        // extended projection; otherwise the HAVING written before '.WithAlias' is silently dropped.
        sql.Should().Contain("having (count(*) > 1)");
    }

    [Fact]
    public void Alias_join_rebases_prewhere_applied_before_the_alias()
    {
        using var ctx = new RootProjectionTests.TestContext();
        var people = ctx.From<RootProjectionTests.Person>();

        var source = ctx.From<RootProjectionTests.Order>();
        source.PreWhereCondition = (Expression<Func<RootProjectionTests.Order, bool>>)(o => o.Id > 0);

        var joined = AliasRoot(source).JoinAlias<
            EntityBuilder<RootProjectionTests.RootJoinProjection<RootProjectionTests.Order, RootProjectionTests.Person>>,
            RootProjectionTests.RootJoinProjection<RootProjectionTests.Order, RootProjectionTests.Person>,
            RootProjectionTests.Person>(
            static dc => new(dc),
            people,
            (a, b) => a.Root.BuyerId == b.Id);

        var command = joined.Select(p => p.Buyer.Id);

        // The alias-join seam must retype the pre-alias PreWhere over the extended projection, not leave
        // it over the root projection it was copied from.
        command.PreWhere.Should().NotBeNull();
        command.PreWhere!.Parameters[0].Type.Should().Be(
            typeof(RootProjectionTests.RootJoinProjection<RootProjectionTests.Order, RootProjectionTests.Person>));
    }

    // ------------------------------------------------- root-only refusal --------------------------

    [Fact]
    public void AliasRoot_is_refused_when_the_receiver_is_already_aliased_or_joined()
    {
        using var ctx = new RootProjectionTests.TestContext();

        // Applied twice: the receiver is already a root-alias projection (dim-1), not a plain source.
        var rooted = AliasRoot(ctx.From<RootProjectionTests.Order>());
        Action again = () => rooted.AliasRoot<
            EntityBuilder<RootProjectionTests.RootProjection<RootProjectionTests.Order>>,
            RootProjectionTests.RootProjection<RootProjectionTests.Order>>(static dc => new(dc));
        again.Should().Throw<NotSupportedException>();

        // Applied after a join: the receiver already carries a join chain.
        var joined = ctx.From<RootProjectionTests.Order>()
            .Join(ctx.From<RootProjectionTests.Person>(), (o, p) => o.BuyerId == p.Id);
        Action afterJoin = () => joined.AliasRoot<
            EntityBuilder<RootProjectionTests.RootProjection<RootProjectionTests.Order>>,
            RootProjectionTests.RootProjection<RootProjectionTests.Order>>(static dc => new(dc));
        afterJoin.Should().Throw<NotSupportedException>().WithMessage("*before any Join*");
    }

    // ------------------------------------------------- derived-root + alias join -----------------

    [Fact]
    public void Alias_join_after_a_derived_root_alias_keeps_the_derived_source()
    {
        using var ctx = new RootProjectionTests.TestContext();
        var people = ctx.From<RootProjectionTests.Person>();

        // A derived root (From(builder)) is carried as Query; re-rooting materializes it as an explicit
        // derived-table FromExpression and clears Query, so the derived source (not the projection type)
        // resolves and is aliased 't1' even when an alias join follows.
        var derived = ctx.From(ctx.From<RootProjectionTests.Order>().Where(o => o.Id > 0));
        var rooted = derived.AliasRoot<
            EntityBuilder<RootProjectionTests.RootProjection<RootProjectionTests.Order>>,
            RootProjectionTests.RootProjection<RootProjectionTests.Order>>(static dc => new(dc));

        var joined = rooted.JoinAlias<
            EntityBuilder<RootProjectionTests.RootJoinProjection<RootProjectionTests.Order, RootProjectionTests.Person>>,
            RootProjectionTests.RootJoinProjection<RootProjectionTests.Order, RootProjectionTests.Person>,
            RootProjectionTests.Person>(
            static dc => new(dc),
            people,
            (a, b) => a.Root.BuyerId == b.Id);

        var sql = RootProjectionTests.SqlOf(ctx, joined.Select(p => p.Buyer.Id));

        sql.Should().Contain("from (select Id, buyer_id as 'BuyerId' from orders");
        sql.Should().Contain(") as 't1'");
        sql.Should().Contain("join person as 't2'");
        sql.Should().Contain("on t1.BuyerId = t2.Id");
    }
}
