using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// T324 D6a — E04/E05 (AC04, AC05) unit side: the correlated APPLY's outer reference resolves to the
/// exact logical source occurrence, never by CLR type, parameter name or slot arithmetic.
/// <para>
/// Covers: repeated CLR types including the same-type FROM+JOIN shape that produced the unqualified-column
/// bug, a positional receiver and a hand-written generated-alias receiver (the source generator is not
/// attached to this project, so the alias projection stands in for <c>AliasJoin_*</c> and the named-slot
/// <see cref="JoinSlotAttribute"/> path is exercised verbatim), the APPLY-to-APPLY left prefix, same-name
/// fresh parameters across two preparations, the fail-closed binding guards, a nested non-zero render
/// scope, alias-allocation invariance and clone/reprepare stability. SQL is rendered by the real SQL
/// Server planner over a placeholder connection string; no connection is opened.
/// </para>
/// </summary>
public class T324ApplyBindingTests
{
    // Exact fail-closed messages (MemberTranslator.BoundOuterReferenceMessage and the render-frame slot
    // guard in SqlSourceRenderer.RegisterApplyLeftSlots); asserted verbatim, not by wildcard.
    private const string BoundOuterReferenceMessage =
        "The correlated CROSS/OUTER APPLY outer reference '0' (left-row slot 2) cannot be resolved to a source occurrence of the active render frame. A bound reference must name one of the left row's FROM/join sources that are already in scope; no alias was emitted because a missing or wrong alias would silently change the query.";

    private const string FrameSlotMessage =
        "The correlated APPLY left-row slot 3 is not registered in the active render frame: the applied source may only reference this command's FROM source and the joins that precede it.";

    private static SqlServerDataContext CreateContext()
        => new(T324ApplyFixture.SqlServerConnectionString, new DataContextBuilder());

    // ---------------------------------------------------------------------------------------------
    // AC04 — positional receiver, repeated CLR types: the same-type FROM+JOIN shape.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Positional_same_type_from_and_join_qualifies_the_correlation_with_the_join_occurrence()
    {
        using var ctx = CreateContext();

        var toJoin = ctx.From<T324A>()
            .Join(ctx.From<T324A>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324A> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Code));
        var toFrom = ctx.From<T324A>()
            .Join(ctx.From<T324A>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324A> p) => ctx.From<T324C>().Where(c => c.Id == p.Item1.Code));

        var joinSql = T324ApplyFixture.SqlOf(ctx, toJoin.Select(q => new { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id }));
        var fromSql = T324ApplyFixture.SqlOf(ctx, toFrom.Select(q => new { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id }));

        // Both slots are the same CLR type (and the same physical table), so only the alias proves which
        // occurrence was addressed: slot 2 is the join, slot 1 the FROM.
        joinSql.Should().Contain("where t3.Id = t2.Code", "slot 2 is the joined occurrence, not the FROM");
        joinSql.Should().NotContain("where t3.Id = t1.Code");
        fromSql.Should().Contain("where t3.Id = t1.Code", "slot 1 is the FROM occurrence");
        fromSql.Should().NotContain("where t3.Id = t2.Code");

        // No unqualified column may survive: that is the exact defect shape this case exists for.
        joinSql.Should().NotContain("= Code");
        fromSql.Should().NotContain("= Code");
    }

    // ---------------------------------------------------------------------------------------------
    // AC04/AC05 — generated-alias receiver: repeated CLR types and the named-slot member path.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Alias_receiver_with_repeated_types_qualifies_by_the_join_slot_attribute_not_the_trailing_digit()
    {
        using var ctx = CreateContext();

        // "Buyer3" carries [JoinSlot(2)]: a trailing-digit parse would read slot 3, which the left prefix
        // does not have, so a wrong resolution would fail loudly instead of silently qualifying.
        var slot = ProjectionAliasCache.GetMemberSlot(typeof(AliasOf<T324A, T324A>).GetProperty(nameof(AliasOf<T324A, T324A>.Buyer3))!);
        slot.Should().Be(2, "the named-slot member path derives the slot from JoinSlotAttribute");

        var aliased = ctx.From<T324A>().JoinAlias<EntityBuilder<AliasOf<T324A, T324A>>, AliasOf<T324A, T324A>, T324A>(
            static dc => new EntityBuilder<AliasOf<T324A, T324A>>(dc),
            (a, b) => a.Id == b.Id);
        var applied = aliased.JoinAlias<EntityBuilder<AliasOf3<T324A, T324A, T324C>>, AliasOf3<T324A, T324A, T324C>, T324C>(
            static dc => new EntityBuilder<AliasOf3<T324A, T324A, T324C>>(dc),
            p => ctx.From<T324C>().Where(c => c.Id == p.Buyer3.Code),
            JoinType.CrossApply);

        var sql = T324ApplyFixture.SqlOf(ctx, applied.Select(q => new { S1 = q.Item1.Id, S2 = q.Buyer3.Code, S3 = q.Third.Id }));

        sql.Should().Contain("where t3.Id = t2.Code", "the named slot addresses the joined occurrence");
        sql.Should().NotContain("where t3.Id = t1.Code");
        sql.Should().NotContain("= Code");
    }

    // ---------------------------------------------------------------------------------------------
    // AC04 — same-name fresh parameters across two preparations never resolve to each other.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Fresh_parameters_with_the_same_name_keep_their_own_binding()
    {
        using var ctx = CreateContext();

        static QueryCommand<T324FlatRow> Build(IDataContext dataContext) => dataContext.From<T324A>()
            .Join(dataContext.From<T324A>(), (p, b) => p.Id == b.Id)
            .CrossApply((Projection<T324A, T324A> p) => dataContext.From<T324C>().Where(c => c.Id == p.Item2.Code))
            .Select(q => new T324FlatRow { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id });

        var first = Build(ctx);
        var second = Build(ctx);

        var firstSql = T324ApplyFixture.SqlOf(ctx, first);
        var secondSql = T324ApplyFixture.SqlOf(ctx, second);

        var firstOwner = first.Joins!.Single(j => j.ApplySource is not null).ApplySource!.Parameters[0];
        var secondOwner = second.Joins!.Single(j => j.ApplySource is not null).ApplySource!.Parameters[0];

        firstOwner.Name.Should().Be("p");
        secondOwner.Name.Should().Be(secondOwner.Name);
        firstOwner.Should().NotBeSameAs(secondOwner, "each preparation owns a fresh parameter instance");

        // Binding identity follows the instance, not the name: each command records its own owner.
        first.GetOuterReferenceBinding(0).Should().NotBeNull();
        second.GetOuterReferenceBinding(0).Should().NotBeNull();
        first.GetOuterReferenceBinding(0)!.Value.Owner.Should().BeSameAs(firstOwner);
        second.GetOuterReferenceBinding(0)!.Value.Owner.Should().BeSameAs(secondOwner);
        second.GetOuterReferenceBinding(0)!.Value.Owner.Should().NotBeSameAs(firstOwner);

        // Each render still qualifies with its own frame's joined occurrence.
        firstSql.Should().Be(secondSql);
        firstSql.Should().Contain("where t3.Id = t2.Code");
    }

    // ---------------------------------------------------------------------------------------------
    // AC04 — an APPLY addresses only its own left prefix: prior APPLY slot visible, current unavailable.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void An_apply_never_addresses_its_own_or_a_later_slot()
    {
        using var ctx = CreateContext();

        var applied = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Code))
            .CrossApply((Projection<T324A, T324B, T324C> p) => ctx.From<T324D>().Where(d => d.Id == p.Item3.Code));

        var applyJoins = applied.Joins!.Where(j => j.ApplySource is not null).ToArray();
        applyJoins.Should().HaveCount(2);
        applyJoins[0].ApplyLeftSlots!.Should().HaveCount(2, "FROM plus the one preceding join");
        applyJoins[1].ApplyLeftSlots!.Should().HaveCount(3, "FROM plus the join plus the first APPLY");

        // The current APPLY is never one of its own addressable slots, so no slot can name itself.
        var secondOwner = applyJoins[1].ApplySource!.Parameters[0];
        applyJoins[1].ApplyLeftSlots!.Select(s => s.Slot).Should().Equal(new[] { 1, 2, 3 });
        applyJoins[1].ApplyLeftSlots.Should().OnlyContain(s => ReferenceEquals(s.Owner, secondOwner));
        typeof(Projection<T324A, T324B, T324C>).GetProperty("Item4")
            .Should().BeNull("the applied body of the second APPLY cannot name its own (fourth) slot");

        var sql = T324ApplyFixture.SqlOf(ctx, applied.Select(q => new
        {
            S1 = q.Item1.Id,
            S2 = q.Item2.Id,
            S3 = q.Item3.Id,
            S4 = q.Item4.Id,
        }));

        // The prior APPLY slot is visible and resolves to that APPLY's wrapper alias (t4), not to the
        // plain join (t2) and not to the second APPLY's own body alias (t5).
        sql.Should().Contain("where t5.Id = t4.Code");
        sql.Should().NotContain("where t5.Id = t2.Code");
    }

    // ---------------------------------------------------------------------------------------------
    // AC04 — missing bound registration and an out-of-range slot list both fail closed, verbatim.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_missing_bound_registration_fails_with_the_exact_outer_reference_message()
    {
        using var ctx = CreateContext();
        var command = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Code))
            .Select(q => new { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id });

        // First preparation registers the render-frame slots and renders normally.
        T324ApplyFixture.SqlOf(ctx, command).Should().Contain("where t3.Id = t2.Code");

        var applyJoin = command.Joins!.Single(j => j.ApplySource is not null);
        applyJoin.ApplyLeftSlots.Should().NotBeNull();
        applyJoin.ApplyLeftSlots = null;

        // The reference is still bound, but the frame can no longer resolve its occurrence: fail closed
        // with exactly the named message rather than emit an unqualified column or a guessed alias.
        Action act = () => T324ApplyFixture.SqlOf(ctx, command);
        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Be(BoundOuterReferenceMessage);
    }

    [Fact]
    public void An_out_of_range_slot_list_fails_with_the_missing_frame_slot_message()
    {
        using var ctx = CreateContext();
        var command = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Code))
            .Select(q => new { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id });

        T324ApplyFixture.SqlOf(ctx, command).Should().Contain("where t3.Id = t2.Code");

        var applyJoin = command.Joins!.Single(j => j.ApplySource is not null);

        // A third slot would name the applied source (or a non-existent join): the frame has two.
        applyJoin.ApplyLeftSlots = JoinExpression.CreateApplyLeftSlots(applyJoin.ApplySource!.Parameters[0], 3);

        Action act = () => T324ApplyFixture.SqlOf(ctx, command);
        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Be(FrameSlotMessage);
    }

    // ---------------------------------------------------------------------------------------------
    // AC05 — nested non-zero scope: the outer slot resolves from its own frame, no alias is stolen.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_nested_non_zero_scope_resolves_the_outer_slot_from_the_outer_frame()
    {
        using var ctx = CreateContext();

        var applied = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>()
                .Where(c => c.Id == ctx.From<T324D>().Where(d => d.Id == p.Item2.Code).Select(d => d.Id).First()));

        var sql = T324ApplyFixture.SqlOf(ctx, applied.Select(q => new { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id }));

        // The correlation sits two frames deep (inside the derived source of the applied body). It must
        // still name the APPLY's slot-2 occurrence (t2) — never the nested frame's own alias (t4).
        sql.Should().Contain("where t4.Id = t2.Code");
        sql.Should().NotContain("where t4.Id = t4.Code");
        sql.Should().NotContain("where t4.Id = t3.Code");
    }

    // ---------------------------------------------------------------------------------------------
    // AC05 — the slot mapping changes the target alias only, never the alias allocation.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Resolving_a_different_slot_does_not_change_the_alias_allocation()
    {
        using var ctx = CreateContext();

        var toFrom = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item1.Code));
        var toJoin = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Code));

        var fromSql = T324ApplyFixture.SqlOf(ctx, toFrom.Select(q => new { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id }));
        var joinSql = T324ApplyFixture.SqlOf(ctx, toJoin.Select(q => new { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id }));

        Aliases(fromSql).Should().Equal(Aliases(joinSql), "the resolved occurrence must not add, drop or reorder an alias");
        Aliases(fromSql).Should().Equal(new[] { "t1", "t2", "t3", "t4" });

        fromSql.Should().Contain("where t3.Id = t1.Code");
        joinSql.Should().Contain("where t3.Id = t2.Code");
        fromSql.Should().NotBe(joinSql, "only the qualified occurrence differs between the two renders");
    }

    // ---------------------------------------------------------------------------------------------
    // AC05 — reprepare and clone rebuild the same binding and render identically.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Repreparation_and_clone_render_identically_and_keep_the_slot_binding()
    {
        using var ctx = CreateContext();

        QueryCommand<T324FlatRow> command = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Code))
            .Select(q => new T324FlatRow { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id });

        var first = T324ApplyFixture.SqlOf(ctx, command);
        var second = T324ApplyFixture.SqlOf(ctx, command);
        var clone = (QueryCommand<T324FlatRow>)command.Clone();
        var clonedSql = T324ApplyFixture.SqlOf(ctx, clone);

        first.Should().Be(second, "a re-render of the same preparation must be identical");
        clonedSql.Should().Be(first, "the clone rebuilds the same runtime binding");

        var owner = command.Joins!.Single(j => j.ApplySource is not null).ApplySource!.Parameters[0];
        command.Joins!.Single(j => j.ApplySource is not null).ApplyLeftSlots!.Should().HaveCount(2);
        command.GetOuterReferenceBinding(0)!.Value.Owner.Should().BeSameAs(owner);
    }

    private static string[] Aliases(string sql)
        => System.Text.RegularExpressions.Regex
            .Matches(sql, @"\bas \[(t\d+)\]")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToArray();

    /// <summary>
    /// Hand-written stand-in for the generated <c>AliasJoin_*</c> projection over two slots: the source
    /// generator is not attached to this project, so the alias surface is declared by hand. The joined
    /// slot is named <c>Buyer3</c> on purpose — its trailing digit must never be read as the position.
    /// </summary>
    private sealed class AliasOf<T1, T2> : Projection<T1, T2>
    {
        [JoinSlot(2)]
        public T2 Buyer3 => throw new NotSupportedException();
    }

    /// <summary>Three-slot alias projection receiving the APPLY: slots 1–2 are the left prefix, 3 the APPLY.</summary>
    private sealed class AliasOf3<T1, T2, T3> : Projection<T1, T2, T3>
    {
        [JoinSlot(2)]
        public T2 Buyer3 => throw new NotSupportedException();

        [JoinSlot(3)]
        public T3 Third => throw new NotSupportedException();
    }
}
