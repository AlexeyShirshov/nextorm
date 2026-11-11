using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// T324 D6a — E01/E02/E08 (AC01, AC02, AC08) unit side: compile-level binding of the flat correlated
/// APPLY transitions, the flat total range 3-8, the positional ninth-source refusal (with a
/// non-evaluation sentinel) and the in-memory refusal. SQL assertions render through the real SQL Server
/// planner over a placeholder connection string; no connection is opened.
/// </summary>
public class T324ApplySurfaceTests
{
    private static SqlServerDataContext CreateContext()
        => new(T324ApplyFixture.SqlServerConnectionString, new DataContextBuilder());

    // ---------------------------------------------------------------------------------------------
    // AC01 — compile-level binding, positional receiver, preceding arity 2 (flat total 3).
    // The explicit flat variable declarations are the compile-time proof; the declarations are also
    // asserted through their static type so a nested result type cannot pass.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Positional_after_join_binds_flat_transitions_at_preceding_arity_two()
    {
        using var ctx = CreateContext();
        var joined = ctx.From<T324A>().Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id);

        JoinedEntityBuilder<T324A, T324B, T324C> crossEntity = joined.CrossApply(
            (Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item1.Id));
        JoinedEntityBuilder<T324A, T324B, T324C> outerEntity = joined.OuterApply(
            (Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Id));
        JoinedEntityBuilder<T324A, T324B, T324C> crossQuery = joined.CrossApply(
            (Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item1.Id).Select(c => c));
        JoinedEntityBuilder<T324A, T324B, T324C> outerQuery = joined.OuterApply(
            (Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Id).ToCommand());

        JoinedEntityBuilder<T324A, T324B, T324C>[] results = [crossEntity, outerEntity, crossQuery, outerQuery];

        foreach (var applied in results)
        {
            T324ApplyFixture.StaticTypeOf(applied).Should().Be(typeof(JoinedEntityBuilder<T324A, T324B, T324C>));
            typeof(EntityBuilder<Projection<Projection<T324A, T324B>, T324C>>)
                .IsAssignableFrom(applied.GetType())
                .Should().BeFalse("the flat transition must never nest the left projection as the result's first slot");
            applied.Joins.Should().HaveCount(2, "the preceding join is carried and the APPLY is appended exactly once");
        }

        joined.Joins.Should().ContainSingle("the four transitions copy the receiver's joins and never mutate it");
    }

    [Fact]
    public void Positional_after_join_binds_flat_transitions_at_preceding_arity_seven()
    {
        using var ctx = CreateContext();
        var joined = T324ApplyFixture.BuildSeven(ctx);

        Expression<Func<Projection<T324A, T324B, T324C, T324D, T324E, T324F, T324G>, EntityBuilder<T324H>>> entityForm =
            p => ctx.From<T324H>().Where(h => h.Id == p.Item7.Id);
        Expression<Func<Projection<T324A, T324B, T324C, T324D, T324E, T324F, T324G>, QueryCommand<T324H>>> queryForm =
            p => ctx.From<T324H>().Where(h => h.Id == p.Item1.Id).ToCommand();

        JoinedEntityBuilder<T324A, T324B, T324C, T324D, T324E, T324F, T324G, T324H> crossEntity = joined.CrossApply(entityForm);
        JoinedEntityBuilder<T324A, T324B, T324C, T324D, T324E, T324F, T324G, T324H> outerEntity = joined.OuterApply(entityForm);
        JoinedEntityBuilder<T324A, T324B, T324C, T324D, T324E, T324F, T324G, T324H> crossQuery = joined.CrossApply(queryForm);
        JoinedEntityBuilder<T324A, T324B, T324C, T324D, T324E, T324F, T324G, T324H> outerQuery = joined.OuterApply(queryForm);

        JoinedEntityBuilder<T324A, T324B, T324C, T324D, T324E, T324F, T324G, T324H>[] results =
            [crossEntity, outerEntity, crossQuery, outerQuery];

        foreach (var applied in results)
        {
            T324ApplyFixture.StaticTypeOf(applied)
                .Should().Be(typeof(JoinedEntityBuilder<T324A, T324B, T324C, T324D, T324E, T324F, T324G, T324H>));
            // Seven preceding sources are carried as six joins; the APPLY appends the seventh.
            applied.Joins.Should().HaveCount(7);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // AC01 — negative: a lambda variable typed against the inherited single-entity member must still
    // bind the flat transition and must not silently produce a nested result.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Inherited_lambda_variable_does_not_produce_a_nested_result()
    {
        using var ctx = CreateContext();
        var joined = ctx.From<T324A>().Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id);

        // The static parameter type comes from the base builder's member (Projection<T1,T2> == TEntity).
        Expression<Func<Projection<T324A, T324B>, EntityBuilder<T324C>>> inheritedLambda =
            p => ctx.From<T324C>().Where(c => c.Id == p.Item2.Id);

        var applied = joined.CrossApply(inheritedLambda);

        T324ApplyFixture.StaticTypeOf(applied).Should().Be(typeof(JoinedEntityBuilder<T324A, T324B, T324C>));
        typeof(JoinedEntityBuilder<Projection<T324A, T324B>, T324C>)
            .IsAssignableFrom(applied.GetType())
            .Should().BeFalse("the inherited member would nest the projection as Item1; the derived flat member must win");
    }

    // ---------------------------------------------------------------------------------------------
    // AC01/AC02 — boundary eight: no successful ninth-source route, refused before the source runs.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Eight_source_receiver_declares_no_correlated_apply_transition_of_its_own()
    {
        var methods = typeof(JoinedEntityBuilder<T324A, T324B, T324C, T324D, T324E, T324F, T324G, T324H>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name is "CrossApply" or "OuterApply")
            .Select(m => m.ToString())
            .ToArray();

        methods.Should().BeEmpty("a ninth source has no flat builder type, so the eight-source receiver declares no APPLY transition");
    }

    [Fact]
    public void No_nine_slot_builder_or_projection_type_exists()
    {
        var assembly = typeof(JoinedEntityBuilder<T324A, T324B>).Assembly;

        assembly.GetTypes()
            .Where(t => t.Name.StartsWith("JoinedEntityBuilder`", StringComparison.Ordinal))
            .Select(t => t.GetGenericArguments().Length)
            .Max()
            .Should().Be(8);

        assembly.GetTypes()
            .Where(t => t.Name.StartsWith("Projection`", StringComparison.Ordinal))
            .Select(t => t.GetGenericArguments().Length)
            .Max()
            .Should().Be(8);
    }

    [Fact]
    public void Ninth_positional_source_is_refused_before_the_source_is_evaluated()
    {
        using var ctx = CreateContext();
        var eight = T324ApplyFixture.BuildEight(ctx);
        // Eight flat sources are carried as seven join expressions (observed on the receiver).
        eight.Joins.Should().HaveCount(7);
        var callbacks = 0;
        T324ApplySentinel.Reset();

        Action act = () => eight.CrossApply(
            (Projection<T324A, T324B, T324C, T324D, T324E, T324F, T324G, T324H> p) =>
                ctx.From<T324I>().Where(i => i.Id == T324ApplySentinel.Probe(p.Item8.Id)),
            _ => callbacks++);

        act.Should().Throw<NotSupportedException>().WithMessage("*at most 8 total sources*");
        T324ApplySentinel.Calls.Should().Be(0, "the refused APPLY source must never be compiled or evaluated");
        callbacks.Should().Be(0, "the refusal precedes the options callback");
        eight.Joins.Should().HaveCount(7, "the rejected call must not mutate the receiver or build a ninth slot");
    }

    [Fact]
    public void Seventh_preceding_source_renders_one_flat_total_of_eight()
    {
        using var ctx = CreateContext();
        var seven = T324ApplyFixture.BuildSeven(ctx);

        var applied = seven.CrossApply(
            (Projection<T324A, T324B, T324C, T324D, T324E, T324F, T324G> p) =>
                ctx.From<T324H>().Where(h => h.Id == p.Item7.Id && h.Code == p.Item1.Code));

        var sql = T324ApplyFixture.SqlOf(ctx, applied.Select(q => new
        {
            S1 = q.Item1.Id,
            S8 = q.Item8.Id,
        }));

        sql.Should().Contain("cross apply (");
        // The applied body (t8) correlates against both ends of its own left prefix.
        sql.Should().Contain("t8.Id = t7.Id");
        sql.Should().Contain("t8.Code = t1.Code");
        sql.Should().Contain(") as [t9]", "the applied derived source is registered once, after the eight flat sources");
    }

    // ---------------------------------------------------------------------------------------------
    // AC02 — flat totals 3..8: one flat slot per source, ordered, applied slot last.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Flat_total_three_is_ordered_and_flat()
    {
        using var ctx = CreateContext();
        var applied = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .CrossApply((Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item2.Id));

        AssertFlatTotal(ctx, applied.Select(q => new { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id }), 3);
    }

    [Fact]
    public void Flat_total_four_is_ordered_and_flat()
    {
        using var ctx = CreateContext();
        var applied = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<T324C>(), (p, c) => p.Item1.Id == c.Id)
            .CrossApply((Projection<T324A, T324B, T324C> p) => ctx.From<T324D>().Where(d => d.Id == p.Item3.Id));

        AssertFlatTotal(ctx, applied.Select(q => new { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id, S4 = q.Item4.Id }), 4);
    }

    [Fact]
    public void Flat_total_five_is_ordered_and_flat()
    {
        using var ctx = CreateContext();
        var applied = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<T324C>(), (p, c) => p.Item1.Id == c.Id)
            .Join(ctx.From<T324D>(), (p, d) => p.Item2.Id == d.Id)
            .CrossApply((Projection<T324A, T324B, T324C, T324D> p) => ctx.From<T324E>().Where(e => e.Id == p.Item4.Id));

        AssertFlatTotal(ctx, applied.Select(q => new { S1 = q.Item1.Id, S2 = q.Item2.Id, S3 = q.Item3.Id, S4 = q.Item4.Id, S5 = q.Item5.Id }), 5);
    }

    [Fact]
    public void Flat_total_six_is_ordered_and_flat()
    {
        using var ctx = CreateContext();
        var applied = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<T324C>(), (p, c) => p.Item1.Id == c.Id)
            .Join(ctx.From<T324D>(), (p, d) => p.Item2.Id == d.Id)
            .Join(ctx.From<T324E>(), (p, e) => p.Item3.Id == e.Id)
            .CrossApply((Projection<T324A, T324B, T324C, T324D, T324E> p) => ctx.From<T324F>().Where(f => f.Id == p.Item5.Id));

        AssertFlatTotal(ctx, applied.Select(q => new
        {
            S1 = q.Item1.Id,
            S2 = q.Item2.Id,
            S3 = q.Item3.Id,
            S4 = q.Item4.Id,
            S5 = q.Item5.Id,
            S6 = q.Item6.Id,
        }), 6);
    }

    [Fact]
    public void Flat_total_seven_is_ordered_and_flat()
    {
        using var ctx = CreateContext();
        var applied = ctx.From<T324A>()
            .Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id)
            .Join(ctx.From<T324C>(), (p, c) => p.Item1.Id == c.Id)
            .Join(ctx.From<T324D>(), (p, d) => p.Item2.Id == d.Id)
            .Join(ctx.From<T324E>(), (p, e) => p.Item3.Id == e.Id)
            .Join(ctx.From<T324F>(), (p, f) => p.Item4.Id == f.Id)
            .CrossApply((Projection<T324A, T324B, T324C, T324D, T324E, T324F> p) => ctx.From<T324G>().Where(g => g.Id == p.Item6.Id));

        AssertFlatTotal(ctx, applied.Select(q => new
        {
            S1 = q.Item1.Id,
            S2 = q.Item2.Id,
            S3 = q.Item3.Id,
            S4 = q.Item4.Id,
            S5 = q.Item5.Id,
            S6 = q.Item6.Id,
            S7 = q.Item7.Id,
        }), 7);
    }

    [Fact]
    public void Flat_total_eight_is_ordered_and_flat()
    {
        using var ctx = CreateContext();
        var applied = T324ApplyFixture.BuildSeven(ctx)
            .CrossApply((Projection<T324A, T324B, T324C, T324D, T324E, T324F, T324G> p) => ctx.From<T324H>().Where(h => h.Id == p.Item7.Id));

        AssertFlatTotal(ctx, applied.Select(q => new
        {
            S1 = q.Item1.Id,
            S2 = q.Item2.Id,
            S3 = q.Item3.Id,
            S4 = q.Item4.Id,
            S5 = q.Item5.Id,
            S6 = q.Item6.Id,
            S7 = q.Item7.Id,
            S8 = q.Item8.Id,
        }), 8);
    }

    private static void AssertFlatTotal<T>(IDataContext ctx, QueryCommand<T> command, int total)
    {
        var sql = T324ApplyFixture.SqlOf(ctx, command);

        // Derive the aliases from the captured SQL instead of guessing an arithmetic stride: the SQL
        // Server renderer registers one alias per physical source (from/join), then the applied body's
        // inner alias, then the APPLY wrapper alias that the outer SELECT actually references.
        var physicalAliases = System.Text.RegularExpressions.Regex
            .Matches(sql, @"\b(?:from|join) \w+ as \[(t\d+)\]")
            .Select(m => m.Groups[1].Value)
            .ToArray();
        var applyWrapper = System.Text.RegularExpressions.Regex.Match(sql, @"\) as \[(t\d+)\]").Groups[1].Value;
        var selectList = sql[..sql.IndexOf(" from ", StringComparison.Ordinal)];
        var selectedAliases = System.Text.RegularExpressions.Regex
            .Matches(selectList, @"\b(t\d+)\.Id\b")
            .Select(m => m.Groups[1].Value)
            .ToArray();

        sql.Should().StartWith("select t1.Id");
        sql.Should().Contain("cross apply (", "the applied source must be a lateral APPLY");
        applyWrapper.Should().NotBeEmpty("the applied derived source must be given a wrapper alias");

        // One flat slot per physical source, and the applied source is registered exactly once.
        physicalAliases.Should().HaveCount(total, "the flat total counts one physical source per slot");
        physicalAliases.Distinct().Should().HaveCount(total, "no source occurrence may be duplicated or lost");

        // Every preceding source keeps its registration order in the SELECT list; the applied slot is last.
        selectedAliases.Should().HaveCount(total, "the SELECT must expose exactly one column per flat slot");
        selectedAliases.Take(total - 1).Should().Equal(
            physicalAliases.Take(total - 1),
            "the preceding flat slots are selected in their registration order");
        selectedAliases[total - 1].Should().Be(applyWrapper, "the applied slot is one flat slot aliased by its APPLY wrapper");
        sql.Should().Contain($") as [{applyWrapper}]", "the applied derived source is registered once, after the {total} flat sources");
    }

    // ---------------------------------------------------------------------------------------------
    // AC08 — N03 in-memory: the correlated APPLY after a join is refused at construction.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void In_memory_rejects_correlated_apply_after_a_join_before_any_sql()
    {
        using var ctx = new InMemoryDataContext();
        var joined = ctx.From<T324A>().Join(ctx.From<T324B>(), (a, b) => a.Id == b.Id);

        Action act = () => joined.CrossApply(
            (Projection<T324A, T324B> p) => ctx.From<T324C>().Where(c => c.Id == p.Item1.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*in-memory provider*");
        joined.Joins.Should().ContainSingle();
    }
}
