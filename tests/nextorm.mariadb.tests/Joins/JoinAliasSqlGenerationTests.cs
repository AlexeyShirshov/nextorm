using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_mariadb_tests;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// SQL-generation coverage for the generated join-alias projection on MariaDB: every supported
/// operator must forward the matching <see cref="JoinType"/> and render the same SQL as the
/// positional join, a repeated CLR type must resolve to distinct slots, the plain-table APPLY forms
/// must be exercised (MariaDB supports them), and FULL JOIN must fail closed because MariaDB has none.
/// </summary>
public class JoinAliasSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void Inner_alias_join_emits_join_and_matches_positional()
    {
        using var ctx = MariaDbTestContext.Create();
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
        using var ctx = MariaDbTestContext.Create();
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
        using var ctx = MariaDbTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var positional = SqlOf(ctx, simple.RightJoin(complex, (s, c) => s.Id == c.Id).Select(p => p.Item2.Id));
        var alias = SqlOf(ctx, simple.RightJoin<IComplexEntity>(complex, (s, c) => s.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        alias.Should().Be(positional);
        alias.Should().Contain(" right join complex_entity");
    }

    [Fact]
    public void Full_alias_join_fails_closed_because_mariadb_has_no_full_join()
    {
        using var ctx = MariaDbTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, simple.FullJoin<IComplexEntity>(complex, (s, c) => s.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*Full join*");
    }

    [Fact]
    public void Cross_alias_join_emits_cross_join_and_matches_positional()
    {
        using var ctx = MariaDbTestContext.Create();
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
        using var ctx = MariaDbTestContext.Create();
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
    public void CrossApply_alias_join_emits_cross_join_on_plain_table_because_supported()
    {
        using var ctx = MariaDbTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var alias = SqlOf(ctx, simple.CrossApply(complex, Alias.Buyer).Select(p => p.Buyer.Id));

        // A plain table cannot be LATERAL, so MariaDB renders a cross join.
        alias.Should().Contain(" cross join complex_entity");
    }

    [Fact]
    public void OuterApply_alias_join_emits_left_join_on_true_on_plain_table_because_supported()
    {
        using var ctx = MariaDbTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var alias = SqlOf(ctx, simple.OuterApply(complex, Alias.Buyer).Select(p => p.Buyer.Id));

        alias.Should().Contain(" left join complex_entity").And.Contain(" on true");
    }

    // ---------------------------------------------------------------------------------------------
    // #159: a generated alias slot over a typed CTE descriptor. The generator must recognise a
    // Cte<T> source (named or anonymous projection) and emit a CTE-form overload that renders the
    // same SQL as the converted ctx.From(cte) alias form.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Direct_typed_cte_alias_join_infers_named_projection_and_matches_converted()
    {
        using var ctx = MariaDbTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var named = ctx.From<IComplexEntity>().Where(c => c.Id > 1).ToCommand().AsCte("cte_buyer");

        var positional = SqlOf(ctx, simple.Join(ctx.From(named), (s, c) => s.Id == c.Id).Select(p => p.Item2.Id));
        var cteAlias = SqlOf(ctx, simple.Join<IComplexEntity>(named, (s, c) => s.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        cteAlias.Should().Be(positional);
        cteAlias.Should().Contain("cte_buyer as (").And.Contain("cte_buyer");
        cteAlias.Should().NotContain("join (select");
    }

    [Fact]
    public void Direct_typed_cte_alias_join_infers_anonymous_projection()
    {
        using var ctx = MariaDbTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var anon = ctx.From<IComplexEntity>().Where(c => c.Id > 1).Select(c => new { c.Id, c.Int }).AsCte("cte_anon");

        var positional = SqlOf(ctx, simple.Join(ctx.From(anon), (s, c) => s.Id == c.Id).Select(p => p.Item2.Id));
        var cteAlias = SqlOf(ctx, simple.Join(anon, (s, c) => s.Id == c.Id, Alias.Anon).Select(p => p.Anon.Id));

        cteAlias.Should().Be(positional);
        cteAlias.Should().Contain("cte_anon as (").And.Contain("cte_anon");
        cteAlias.Should().NotContain("join (select");
    }

    [Fact]
    public void Direct_typed_cte_alias_join_subsequent_slot_infers_projection()
    {
        using var ctx = MariaDbTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var first = ctx.From<IComplexEntity>().Where(c => c.Id > 1).ToCommand().AsCte("cte_first");
        var second = ctx.From<IComplexEntity>().Select(c => new { c.Id, c.Int }).AsCte("cte_second");

        var chained = simple
            .Join<IComplexEntity>(first, (s, c) => s.Id == c.Id, Alias.First)
            .Join(second, (a, c) => a.Item1.Id == c.Id, Alias.Second);

        var sql = SqlOf(ctx, chained.Select(p => p.Second.Id));
        sql.Should().Contain("cte_first as (").And.Contain("cte_second as (");
        sql.Should().Contain("cte_first").And.Contain("cte_second");
    }

    // ---------------------------------------------------------------------------------------------
    // #160 root alias: '.WithAlias(Alias.Root)' names slot 1 for every root source and preserves the
    // source state; a later alias join becomes slot 2 (t1/t2 in the join condition).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Root_alias_with_a_join_maps_root_to_t1_and_join_to_t2()
    {
        using var ctx = MariaDbTestContext.Create();
        var rooted = ctx.From<ISimpleEntity>().WithAlias(Alias.Root);
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, rooted.Join<IComplexEntity>(complex, (s, c) => s.Root.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        sql.Should().Contain("as `t1`");
        sql.Should().Contain("as `t2`");
        sql.Should().Contain("on cast(t1.id as signed) = t2.id");
    }

    [Fact]
    public void Root_alias_on_a_fromsql_source_keeps_the_derived_source()
    {
        using var ctx = MariaDbTestContext.Create();
        var rooted = ctx.FromSql("select id from simple_entity").WithAlias(Alias.Root);
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, rooted.Join<IComplexEntity>(complex, (s, c) => s.Root.GetInt64("id") == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        sql.Should().Contain("(select id from simple_entity) as `t1`");
        sql.Should().NotContain("simple_entity as `t1`");
    }

    [Fact]
    public void Root_alias_on_a_builder_source_keeps_the_derived_source()
    {
        using var ctx = MariaDbTestContext.Create();
        var builder = ctx.From<ISimpleEntity>();
        var rooted = ctx.From(builder).WithAlias(Alias.Root);
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, rooted.Join<IComplexEntity>(complex, (s, c) => s.Root.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        sql.Should().Contain(") as `t1`");
        sql.Should().Contain("from simple_entity");
        sql.Should().NotContain("simple_entity as `t1`");
    }

    [Fact]
    public void Root_alias_on_a_query_command_source_keeps_the_derived_query()
    {
        using var ctx = MariaDbTestContext.Create();
        var source = ctx.From<ISimpleEntity>().Where(s => s.Id == 1).ToCommand();
        var rooted = ctx.From(source).WithAlias(Alias.Root);
        var complex = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, rooted.Join<IComplexEntity>(complex, (s, c) => s.Root.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        sql.Should().Contain("where id = 1");
        sql.Should().Contain(") as `t1`");
        sql.Should().NotContain("simple_entity as `t1`");
    }

    [Fact]
    public void Root_alias_is_rejected_when_the_receiver_already_has_a_join()
    {
        using var ctx = MariaDbTestContext.Create();
        var joined = ctx.From<ISimpleEntity>().Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Id);

        Action act = () => joined.AliasRoot<AliasJoin_A1_Root<ISimpleEntity>, AliasProjection_A1_Root<ISimpleEntity>>(
            static dc => new AliasJoin_A1_Root<ISimpleEntity>(dc));

        act.Should().Throw<NotSupportedException>().WithMessage("*before any Join*");
    }
}
