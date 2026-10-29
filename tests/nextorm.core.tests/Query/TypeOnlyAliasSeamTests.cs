using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using BuyerBuilder = NextORM.Core.EntityBuilder<
    NextORM.Core.Tests.RootProjectionTests.RootJoinProjection<
        NextORM.Core.Tests.RootProjectionTests.Order, NextORM.Core.Tests.RootProjectionTests.Person>>;
using BuyerProjection = NextORM.Core.Tests.RootProjectionTests.RootJoinProjection<
    NextORM.Core.Tests.RootProjectionTests.Order, NextORM.Core.Tests.RootProjectionTests.Person>;
using Order = NextORM.Core.Tests.RootProjectionTests.Order;
using Person = NextORM.Core.Tests.RootProjectionTests.Person;
using RootProjection = NextORM.Core.Tests.RootProjectionTests.RootProjection<
    NextORM.Core.Tests.RootProjectionTests.Order>;
using TestContext = NextORM.Core.Tests.RootProjectionTests.TestContext;

namespace NextORM.Core.Tests;

/// <summary>
/// Issue #322 (cycle N=1, r1, D2) coverage for the source-less alias-join seam
/// <c>EntityBuilder&lt;TEntity&gt;.JoinAlias&lt;TNext, TNextEntity, TJoinEntity&gt;(create, joinCondition,
/// joinType, options)</c>: the implicit <c>FROM</c> source is created from the receiver's context and the
/// join is assembled by the same projection/state path as the source-explicit overload.
/// <para>
/// Covers A05 (the options callback runs exactly once and its hint/lock effect matches the explicit-source
/// baseline, omitted/null options keep the defaults), A06 (a null condition throws before the callback runs
/// and before any receiver state changes), A02 (JoinType routing for Inner/Left/Right/Full) and M08
/// (projection-backed receiver, repeated preparation and state preservation). Runs against the provider-free
/// <see cref="RootProjectionTests.TestContext"/> (fake dialect) so planning and SQL rendering are exercised
/// without a database. Runs in the "Query cache controls" collection because the repeated-preparation case
/// reads the process-wide plan cache.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public class TypeOnlyAliasSeamTests
{
    private static BuyerBuilder SourceLess(
        EntityBuilder<Order> source,
        JoinType joinType = JoinType.Inner,
        Action<JoinOptions>? options = null) =>
        source.JoinAlias<BuyerBuilder, BuyerProjection, Person>(
            static dc => new BuyerBuilder(dc),
            (a, b) => a.BuyerId == b.Id,
            joinType,
            options);

    private static BuyerBuilder SourceLessFromProjection(
        EntityBuilder<RootProjection> source,
        JoinType joinType = JoinType.Inner,
        Action<JoinOptions>? options = null) =>
        source.JoinAlias<BuyerBuilder, BuyerProjection, Person>(
            static dc => new BuyerBuilder(dc),
            (a, b) => a.Root.BuyerId == b.Id,
            joinType,
            options);

    private static BuyerBuilder SourceExplicit(
        EntityBuilder<Order> source,
        EntityBuilder<Person> joined,
        JoinType joinType = JoinType.Inner,
        Action<JoinOptions>? options = null) =>
        source.JoinAlias<BuyerBuilder, BuyerProjection, Person>(
            static dc => new BuyerBuilder(dc),
            joined,
            (a, b) => a.BuyerId == b.Id,
            joinType,
            options);

    private static JoinExpression SingleJoin(QueryCommand command)
    {
        command.Joins.Should().NotBeNull();
        command.Joins.Should().ContainSingle("one alias join is added per call");
        return command.Joins![0];
    }

    // ------------------------------------------------------------------ options: exactly once ----------

    [Fact]
    public void Source_less_alias_join_applies_the_options_callback_exactly_once_and_matches_the_explicit_baseline()
    {
        using var ctx = new TestContext();
        var calls = 0;
        Action<JoinOptions> options = o =>
        {
            calls++;
            o.WithJoinHint("loop").ForUpdate();
        };

        var sourceLess = SingleJoin(SourceLess(ctx.From<Order>(), JoinType.Left, options).Select(p => p.Buyer.Id));
        calls.Should().Be(1, "the seam must invoke the callback once, not once per wrapper and helper");
        sourceLess.JoinType.Should().Be(JoinType.Left);

        calls = 0;
        var baseline = SingleJoin(SourceExplicit(ctx.From<Order>(), ctx.From<Person>(), JoinType.Left, options).Select(p => p.Buyer.Id));
        calls.Should().Be(1, "the source-explicit overload is the reference: it also applies the callback once");

        // The applicable hint/lock effect of the supplied callback must match that baseline exactly.
        sourceLess.JoinHint.Should().Be("loop");
        sourceLess.JoinHint.Should().Be(baseline.JoinHint);
        sourceLess.From.SourceLock.Should().NotBeNull();
        sourceLess.From.SourceLock!.Mode.Should().Be(LockMode.Update);
        sourceLess.From.SourceLock!.Mode.Should().Be(baseline.From.SourceLock!.Mode);
        sourceLess.IsGlobal.Should().Be(baseline.IsGlobal);
        sourceLess.Strictness.Should().Be(baseline.Strictness);
    }

    // ------------------------------------------------------------------ options: omitted/null ----------

    [Fact]
    public void Omitted_and_explicit_null_options_both_preserve_the_join_defaults()
    {
        using var ctx = new TestContext();

        var omitted = SourceLess(ctx.From<Order>()).Select(p => p.Buyer.Id);
        var explicitNull = SourceLess(ctx.From<Order>(), JoinType.Inner, null).Select(p => p.Buyer.Id);

        foreach (var command in new[] { omitted, explicitNull })
        {
            var join = SingleJoin(command);
            join.JoinType.Should().Be(JoinType.Inner);
            join.JoinHint.Should().BeNull();
            join.IsGlobal.Should().BeFalse();
            join.Strictness.Should().Be(JoinStrictness.Default);
            join.From.SourceLock.Should().BeNull();
        }

        RootProjectionTests.SqlOf(ctx, omitted).Should()
            .Be(RootProjectionTests.SqlOf(ctx, explicitNull), "omitted and explicit-null options are the same join");
    }

    // ------------------------------------------------------------------ null-first guard (A06) ---------

    [Fact]
    public void A_null_condition_throws_before_the_callback_and_without_touching_the_receiver()
    {
        using var ctx = new TestContext();
        var receiver = ctx.From<Order>().Where(o => o.Id > 0);

        var callbacks = 0;
        var factoryCalls = 0;
        Action<JoinOptions> options = _ => callbacks++;
        Func<IDataContext, BuyerBuilder> create = dc =>
        {
            factoryCalls++;
            return new BuyerBuilder(dc);
        };
        Expression<Func<Order, Person, bool>>? condition = null;

        Action act = () => receiver.JoinAlias<BuyerBuilder, BuyerProjection, Person>(
            create,
            condition!,
            JoinType.Left,
            options);

        act.Should().Throw<ArgumentNullException>().WithParameterName("joinCondition");
        callbacks.Should().Be(0, "the callback must not run when the condition is rejected");
        factoryCalls.Should().Be(0, "the target builder must not even be created for an invalid call");

        // Receiver state is unchanged: no join, the pre-join Where is intact.
        receiver.Joins.Should().BeNull();
        receiver.Condition.Should().NotBeNull();
        receiver.Select(o => o.Id).Joins.Should().BeNull();

        // And the same receiver still accepts a valid call afterwards.
        var valid = SourceLess(receiver, JoinType.Left).Select(p => p.Buyer.Id);
        SingleJoin(valid).JoinType.Should().Be(JoinType.Left);
        valid.Joins.Should().ContainSingle();
    }

    // ------------------------------------------------------------------ JoinType routing (A02) ---------

    [Theory]
    [InlineData(JoinType.Inner)]
    [InlineData(JoinType.Left)]
    [InlineData(JoinType.Right)]
    [InlineData(JoinType.Full)]
    public void The_join_type_is_routed_through_the_shared_assembly_path(JoinType joinType)
    {
        using var ctx = new TestContext();

        var sourceLessCommand = SourceLess(ctx.From<Order>(), joinType).Select(p => p.Buyer.Id);
        var sourceLess = SingleJoin(sourceLessCommand);
        sourceLess.JoinType.Should().Be(joinType);

        var baselineCommand = SourceExplicit(ctx.From<Order>(), ctx.From<Person>(), joinType).Select(p => p.Buyer.Id);
        var baseline = SingleJoin(baselineCommand);
        baseline.JoinType.Should().Be(joinType);

        // Source-less and source-explicit must render the same SQL for the same operator.
        var sql = RootProjectionTests.SqlOf(ctx, sourceLessCommand);
        sql.Should().Be(RootProjectionTests.SqlOf(ctx, baselineCommand));
        sql.Should().Contain("t1").And.Contain("t2");
    }

    // ------------------------------------------------------------------ state / repeated prep (M08) ----

    [Fact]
    public void A_projection_receiver_keeps_its_state_and_repeated_preparation_reuses_the_plan()
    {
        using var ctx = new TestContext();
        var receiver = ctx.From<Order>()
            .Where(o => o.BuyerId == o.Id)
            .AliasRoot<EntityBuilder<RootProjection>, RootProjection>(static dc => new EntityBuilder<RootProjection>(dc));

        var command = SourceLessFromProjection(receiver, JoinType.Left).Select(p => p.Buyer.Id);
        var join = SingleJoin(command);
        join.JoinType.Should().Be(JoinType.Left);
        // The ON predicate binds the receiver's projection shape (the generated AliasJoin_* receiver
        // derives from EntityBuilder<Projection>); the joined side is slot 2 of the extended projection.
        join.JoinCondition!.Parameters[0].Type.Should().Be<RootProjection>();
        join.JoinCondition.Parameters[1].Type.Should().Be<Person>();

        // The pre-alias Where is rebased onto slot 1 of the extended projection, not dropped.
        var sql = RootProjectionTests.SqlOf(ctx, command);
        sql.Should().Contain("where t1.buyer_id =");

        // A repeat call on the same receiver starts from the pristine chain, not from the first join.
        receiver.Joins.Should().BeNull("the seam must not mutate the receiver's join chain");
        receiver.Condition.Should().NotBeNull("the pre-join Where stays on the receiver");
        SingleJoin(SourceLessFromProjection(receiver, JoinType.Left).Select(p => p.Buyer.Id)).JoinType.Should().Be(JoinType.Left);

        // Repeated preparation of the same command reuses the cached plan and keeps the command cacheable.
        command.Cache.Should().BeTrue();
        var first = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);
        var second = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);
        second.Should().BeSameAs(first);
        command.Cache.Should().BeTrue("preparation must not set the sticky _dontCache flag");
        receiver.Joins.Should().BeNull();
    }
}
