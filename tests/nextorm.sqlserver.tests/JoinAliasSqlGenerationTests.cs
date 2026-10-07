using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_sqlserver_tests;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// SQL-generation coverage for the generated join-alias projection on SQL Server: every supported
/// operator must forward the matching <see cref="JoinType"/> and render the same SQL as the
/// positional join, a repeated CLR type must resolve to distinct slots, and the plain-table APPLY
/// forms must be exercised because SQL Server supports them.
/// </summary>
public class JoinAliasSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void Inner_alias_join_emits_join_and_matches_positional()
    {
        using var ctx = SqlServerTestContext.Create();
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
        using var ctx = SqlServerTestContext.Create();
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
        using var ctx = SqlServerTestContext.Create();
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
        using var ctx = SqlServerTestContext.Create();
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
        using var ctx = SqlServerTestContext.Create();
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
        using var ctx = SqlServerTestContext.Create();
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
    public void CrossApply_alias_join_emits_cross_apply_because_supported()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var alias = SqlOf(ctx, simple.CrossApply(complex, Alias.Buyer).Select(p => p.Buyer.Id));

        alias.Should().Contain(" cross apply complex_entity");
    }

    [Fact]
    public void OuterApply_alias_join_emits_outer_apply_because_supported()
    {
        using var ctx = SqlServerTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var alias = SqlOf(ctx, simple.OuterApply(complex, Alias.Buyer).Select(p => p.Buyer.Id));

        alias.Should().Contain(" outer apply complex_entity");
    }

    // ---------------------------------------------------------------------------------------------
    // #159: a generated alias slot over a typed CTE descriptor. The generator must recognise a
    // Cte<T> source (named or anonymous projection) and emit a CTE-form overload that renders the
    // same SQL as the converted ctx.From(cte) alias form.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Direct_typed_cte_alias_join_infers_named_projection_and_matches_converted()
    {
        using var ctx = SqlServerTestContext.Create();
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
        using var ctx = SqlServerTestContext.Create();
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
        using var ctx = SqlServerTestContext.Create();
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
}
