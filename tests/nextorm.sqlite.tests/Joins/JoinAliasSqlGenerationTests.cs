using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_sqlite_tests;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL-generation coverage for the generated join-alias projection on SQLite: every supported
/// operator must forward the matching <see cref="JoinType"/> and render the same SQL as the
/// positional join, a repeated CLR type must resolve to distinct slots, and CROSS/OUTER APPLY must
/// fail closed because SQLite has no lateral source.
/// </summary>
public class JoinAliasSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void Inner_alias_join_emits_join_and_matches_positional()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var positional = SqlOf(ctx, simple.Join(complex, (s, c) => s.Id == c.Id).Select(p => p.Item2.Id));
        var alias = SqlOf(ctx, simple.Join<IComplexEntity>(complex, (s, c) => s.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        alias.Should().Be(positional);
        alias.Should().Contain(" join complex_entity");
    }

    [Fact]
    public void Left_alias_join_emits_left_join_and_matches_positional()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var positional = SqlOf(ctx, simple.LeftJoin(complex, (s, c) => s.Id == c.Id).Select(p => p.Item2.Id));
        var alias = SqlOf(ctx, simple.LeftJoin<IComplexEntity>(complex, (s, c) => s.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        alias.Should().Be(positional);
        alias.Should().Contain(" left join complex_entity");
    }

    [Fact]
    public void Right_alias_join_emits_right_join_and_matches_positional()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var positional = SqlOf(ctx, simple.RightJoin(complex, (s, c) => s.Id == c.Id).Select(p => p.Item2.Id));
        var alias = SqlOf(ctx, simple.RightJoin<IComplexEntity>(complex, (s, c) => s.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        alias.Should().Be(positional);
        alias.Should().Contain(" right join complex_entity");
    }

    [Fact]
    public void Full_alias_join_emits_full_join_and_matches_positional()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var positional = SqlOf(ctx, simple.FullJoin(complex, (s, c) => s.Id == c.Id).Select(p => p.Item2.Id));
        var alias = SqlOf(ctx, simple.FullJoin<IComplexEntity>(complex, (s, c) => s.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        alias.Should().Be(positional);
        alias.Should().Contain(" full join complex_entity");
    }

    [Fact]
    public void Cross_alias_join_emits_cross_join_and_matches_positional()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var positional = SqlOf(ctx, simple.CrossJoin(complex).Select(p => p.Item2.Id));
        var alias = SqlOf(ctx, simple.CrossJoin(complex, Alias.Buyer).Select(p => p.Buyer.Id));

        alias.Should().Be(positional);
        alias.Should().Contain(" cross join complex_entity");
    }

    [Fact]
    public void Repeated_clr_type_resolves_to_distinct_slots()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var chained = simple
            .Join<IComplexEntity>(complex, (a, b) => a.Id == b.Id, Alias.First)
            .Join<IComplexEntity>(complex, (a, b) => a.Item1.Id == b.Id, Alias.Second);

        var firstSql = SqlOf(ctx, chained.Select(p => p.First.Id));
        var secondSql = SqlOf(ctx, chained.Select(p => p.Second.Id));

        firstSql.Should().Contain("select t2.id");
        secondSql.Should().Contain("select t3.id");
        firstSql.Should().NotBe(secondSql);
    }

    [Fact]
    public void CrossApply_alias_join_fails_closed_because_sqlite_has_no_lateral()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, simple.CrossApply(complex, Alias.Buyer).Select(p => p.Buyer.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*CrossApply*");
    }

    [Fact]
    public void OuterApply_alias_join_fails_closed_because_sqlite_has_no_lateral()
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, simple.OuterApply(complex, Alias.Buyer).Select(p => p.Buyer.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*OuterApply*");
    }

    // ---------------------------------------------------------------------------------------------
    // #160 root alias: '.WithAlias(Alias.Root)' names slot 1 for every root source and preserves the
    // source state; a later alias join becomes slot 2 (t1/t2 in the join condition).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Root_alias_with_a_join_maps_root_to_t1_and_join_to_t2()
    {
        using var ctx = SqliteTestContext.Create();
        var rooted = ctx.From<ISimpleEntity>().WithAlias(Alias.Root);
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, rooted.Join<IComplexEntity>(complex, (s, c) => s.Root.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        sql.Should().Contain("as 't1'");
        sql.Should().Contain("as 't2'");
        sql.Should().Contain("on cast(t1.id as bigint) = t2.id");
    }

    [Fact]
    public void Root_alias_on_a_fromsql_source_keeps_the_derived_source()
    {
        using var ctx = SqliteTestContext.Create();
        var rooted = ctx.FromSql("select id from simple_entity").WithAlias(Alias.Root);
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, rooted.Join<IComplexEntity>(complex, (s, c) => s.Root.GetInt64("id") == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        sql.Should().Contain("(select id from simple_entity) as 't1'");
        sql.Should().NotContain("simple_entity as 't1'");
    }

    [Fact]
    public void Root_alias_on_a_builder_source_keeps_the_derived_source()
    {
        using var ctx = SqliteTestContext.Create();
        var builder = ctx.From<ISimpleEntity>();
        var rooted = ctx.From(builder).WithAlias(Alias.Root);
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, rooted.Join<IComplexEntity>(complex, (s, c) => s.Root.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        sql.Should().Contain(") as 't1'");
        sql.Should().Contain("from simple_entity");
        sql.Should().NotContain("simple_entity as 't1'");
    }

    [Fact]
    public void Root_alias_on_a_query_command_source_keeps_the_derived_query()
    {
        using var ctx = SqliteTestContext.Create();
        var source = ctx.From<ISimpleEntity>().Where(s => s.Id == 1).ToCommand();
        var rooted = ctx.From(source).WithAlias(Alias.Root);
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, rooted.Join<IComplexEntity>(complex, (s, c) => s.Root.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        sql.Should().Contain("where id = 1");
        sql.Should().Contain(") as 't1'");
        sql.Should().NotContain("simple_entity as 't1'");
    }

    [Fact]
    public void Root_alias_is_rejected_when_the_receiver_already_has_a_join()
    {
        using var ctx = SqliteTestContext.Create();
        var joined = ctx.From<ISimpleEntity>().Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Id);

        Action act = () => joined.AliasRoot<AliasJoin_A1_Root<ISimpleEntity>, AliasProjection_A1_Root<ISimpleEntity>>(
            static dc => new AliasJoin_A1_Root<ISimpleEntity>(dc));

        act.Should().Throw<NotSupportedException>().WithMessage("*before any Join*");
    }

    // ---------------------------------------------------------------------------------------------
    // #322: the source-less alias form (no `EntityBuilder<TJoin>` argument: the implicit source is
    // created by the core seam from the receiver's context) must render byte-identical SQL to the
    // source-explicit alias form for every conditional operator, at the root and at chain arities 2-7.
    // Only the join step under test carries the operator; the two call forms differ solely in the
    // source argument, so any difference in the rendered statement is a real defect.
    // All four conditional operators support FULL here, so every operator takes the identity path.
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(JoinType.Inner)]
    [InlineData(JoinType.Left)]
    [InlineData(JoinType.Right)]
    [InlineData(JoinType.Full)]

    public void Source_less_alias_join_renders_identical_sql_to_the_source_explicit_form(JoinType joinType)
    {
        using var ctx = SqliteTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        for (var arity = 1; arity <= 7; arity++)
        {
            var sourceLess = SqlOf(ctx, Chain(joinType, arity, simple, complex, sourceLess: true));
            var sourceExplicit = SqlOf(ctx, Chain(joinType, arity, simple, complex, sourceLess: false));

            sourceLess.Should().Be(sourceExplicit,
                "the source-less {0} chain of arity {1} must render the same SQL", joinType, arity);
            // Non-vacuity: arity N must actually reach slot N+1, not silently render the same chain.
            sourceLess.Should().Contain($"t{arity + 1}.", "arity {0} must project slot {1}", arity, arity + 1);
        }
    }

    /// <summary>
    /// Builds the chain for the given operator and arity. Only the first step has two call shapes
    /// (source-less vs source-explicit); every continuation starts from the same closed receiver type,
    /// so the remainder of the chain is written once and takes the form as a flag.
    /// </summary>
    private static QueryCommand<long> Chain(
        JoinType joinType,
        int arity,
        EntityBuilder<ISimpleEntity> simple,
        EntityBuilder<IComplexEntity> complex,
        bool sourceLess)
    {
        var head = sourceLess ? Step1(joinType, simple) : Step1(joinType, simple, complex);

        return arity switch
        {
            1 => head.Select(p => p.S1.Id),
            2 => Continue2(head, complex, sourceLess),
            3 => Continue3(head, complex, sourceLess),
            4 => Continue4(head, complex, sourceLess),
            5 => Continue5(head, complex, sourceLess),
            6 => Continue6(head, complex, sourceLess),
            _ => Continue7(head, complex, sourceLess)
        };
    }

    private static AliasJoin_P1_A2_S1<ISimpleEntity, IComplexEntity> Step1(
        JoinType joinType,
        EntityBuilder<ISimpleEntity> simple) => joinType switch
    {
        JoinType.Inner => simple.Join<IComplexEntity>((a, c) => a.Id == c.Id, Alias.S1),
        JoinType.Left => simple.LeftJoin<IComplexEntity>((a, c) => a.Id == c.Id, Alias.S1),
        JoinType.Right => simple.RightJoin<IComplexEntity>((a, c) => a.Id == c.Id, Alias.S1),
        _ => simple.FullJoin<IComplexEntity>((a, c) => a.Id == c.Id, Alias.S1)
    };

    private static AliasJoin_P1_A2_S1<ISimpleEntity, IComplexEntity> Step1(
        JoinType joinType,
        EntityBuilder<ISimpleEntity> simple,
        EntityBuilder<IComplexEntity> complex) => joinType switch
    {
        JoinType.Inner => simple.Join<IComplexEntity>(complex, (a, c) => a.Id == c.Id, Alias.S1),
        JoinType.Left => simple.LeftJoin<IComplexEntity>(complex, (a, c) => a.Id == c.Id, Alias.S1),
        JoinType.Right => simple.RightJoin<IComplexEntity>(complex, (a, c) => a.Id == c.Id, Alias.S1),
        _ => simple.FullJoin<IComplexEntity>(complex, (a, c) => a.Id == c.Id, Alias.S1)
    };

    private static QueryCommand<long> Continue2(
        AliasJoin_P1_A2_S1<ISimpleEntity, IComplexEntity> head,
        EntityBuilder<IComplexEntity> complex,
        bool sourceLess)
    {
        var chain = sourceLess
            ? head.Join<IComplexEntity>((a, c) => a.Item1.Id == c.Id, Alias.S2)
            : head.Join<IComplexEntity>(complex, (a, c) => a.Item1.Id == c.Id, Alias.S2);

        return chain.Select(p => p.S2.Id);
    }

    private static QueryCommand<long> Continue3(
        AliasJoin_P1_A2_S1<ISimpleEntity, IComplexEntity> head,
        EntityBuilder<IComplexEntity> complex,
        bool sourceLess)
    {
        var chain = sourceLess
            ? head.Join<IComplexEntity>((a, c) => a.Item1.Id == c.Id, Alias.S2)
                .Join<IComplexEntity>((a, c) => a.Item2.Id == c.Id, Alias.S3)
            : head.Join<IComplexEntity>(complex, (a, c) => a.Item1.Id == c.Id, Alias.S2)
                .Join<IComplexEntity>(complex, (a, c) => a.Item2.Id == c.Id, Alias.S3);

        return chain.Select(p => p.S3.Id);
    }

    private static QueryCommand<long> Continue4(
        AliasJoin_P1_A2_S1<ISimpleEntity, IComplexEntity> head,
        EntityBuilder<IComplexEntity> complex,
        bool sourceLess)
    {
        var chain = sourceLess
            ? head.Join<IComplexEntity>((a, c) => a.Item1.Id == c.Id, Alias.S2)
                .Join<IComplexEntity>((a, c) => a.Item2.Id == c.Id, Alias.S3)
                .Join<IComplexEntity>((a, c) => a.Item3.Id == c.Id, Alias.S4)
            : head.Join<IComplexEntity>(complex, (a, c) => a.Item1.Id == c.Id, Alias.S2)
                .Join<IComplexEntity>(complex, (a, c) => a.Item2.Id == c.Id, Alias.S3)
                .Join<IComplexEntity>(complex, (a, c) => a.Item3.Id == c.Id, Alias.S4);

        return chain.Select(p => p.S4.Id);
    }

    private static QueryCommand<long> Continue5(
        AliasJoin_P1_A2_S1<ISimpleEntity, IComplexEntity> head,
        EntityBuilder<IComplexEntity> complex,
        bool sourceLess)
    {
        var chain = sourceLess
            ? head.Join<IComplexEntity>((a, c) => a.Item1.Id == c.Id, Alias.S2)
                .Join<IComplexEntity>((a, c) => a.Item2.Id == c.Id, Alias.S3)
                .Join<IComplexEntity>((a, c) => a.Item3.Id == c.Id, Alias.S4)
                .Join<IComplexEntity>((a, c) => a.Item4.Id == c.Id, Alias.S5)
            : head.Join<IComplexEntity>(complex, (a, c) => a.Item1.Id == c.Id, Alias.S2)
                .Join<IComplexEntity>(complex, (a, c) => a.Item2.Id == c.Id, Alias.S3)
                .Join<IComplexEntity>(complex, (a, c) => a.Item3.Id == c.Id, Alias.S4)
                .Join<IComplexEntity>(complex, (a, c) => a.Item4.Id == c.Id, Alias.S5);

        return chain.Select(p => p.S5.Id);
    }

    private static QueryCommand<long> Continue6(
        AliasJoin_P1_A2_S1<ISimpleEntity, IComplexEntity> head,
        EntityBuilder<IComplexEntity> complex,
        bool sourceLess)
    {
        var chain = sourceLess
            ? head.Join<IComplexEntity>((a, c) => a.Item1.Id == c.Id, Alias.S2)
                .Join<IComplexEntity>((a, c) => a.Item2.Id == c.Id, Alias.S3)
                .Join<IComplexEntity>((a, c) => a.Item3.Id == c.Id, Alias.S4)
                .Join<IComplexEntity>((a, c) => a.Item4.Id == c.Id, Alias.S5)
                .Join<IComplexEntity>((a, c) => a.Item5.Id == c.Id, Alias.S6)
            : head.Join<IComplexEntity>(complex, (a, c) => a.Item1.Id == c.Id, Alias.S2)
                .Join<IComplexEntity>(complex, (a, c) => a.Item2.Id == c.Id, Alias.S3)
                .Join<IComplexEntity>(complex, (a, c) => a.Item3.Id == c.Id, Alias.S4)
                .Join<IComplexEntity>(complex, (a, c) => a.Item4.Id == c.Id, Alias.S5)
                .Join<IComplexEntity>(complex, (a, c) => a.Item5.Id == c.Id, Alias.S6);

        return chain.Select(p => p.S6.Id);
    }

    private static QueryCommand<long> Continue7(
        AliasJoin_P1_A2_S1<ISimpleEntity, IComplexEntity> head,
        EntityBuilder<IComplexEntity> complex,
        bool sourceLess)
    {
        var chain = sourceLess
            ? head.Join<IComplexEntity>((a, c) => a.Item1.Id == c.Id, Alias.S2)
                .Join<IComplexEntity>((a, c) => a.Item2.Id == c.Id, Alias.S3)
                .Join<IComplexEntity>((a, c) => a.Item3.Id == c.Id, Alias.S4)
                .Join<IComplexEntity>((a, c) => a.Item4.Id == c.Id, Alias.S5)
                .Join<IComplexEntity>((a, c) => a.Item5.Id == c.Id, Alias.S6)
                .Join<IComplexEntity>((a, c) => a.Item6.Id == c.Id, Alias.S7)
            : head.Join<IComplexEntity>(complex, (a, c) => a.Item1.Id == c.Id, Alias.S2)
                .Join<IComplexEntity>(complex, (a, c) => a.Item2.Id == c.Id, Alias.S3)
                .Join<IComplexEntity>(complex, (a, c) => a.Item3.Id == c.Id, Alias.S4)
                .Join<IComplexEntity>(complex, (a, c) => a.Item4.Id == c.Id, Alias.S5)
                .Join<IComplexEntity>(complex, (a, c) => a.Item5.Id == c.Id, Alias.S6)
                .Join<IComplexEntity>(complex, (a, c) => a.Item6.Id == c.Id, Alias.S7);

        return chain.Select(p => p.S7.Id);
    }
}
