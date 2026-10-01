using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_mysql_tests;

namespace NextORM.MySql.Tests;

/// <summary>
/// SQL-generation coverage for the generated join-alias projection on MySQL: every supported
/// operator must forward the matching <see cref="JoinType"/> and render the same SQL as the
/// positional join, a repeated CLR type must resolve to distinct slots, the plain-table APPLY forms
/// must be exercised (MySQL supports them), and FULL JOIN must fail closed because MySQL has none.
/// </summary>
public class JoinAliasSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void Inner_alias_join_emits_join_and_matches_positional()
    {
        using var ctx = MySqlTestContext.Create();
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
        using var ctx = MySqlTestContext.Create();
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
        using var ctx = MySqlTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var positional = SqlOf(ctx, simple.RightJoin(complex, (s, c) => s.Id == c.Id).Select(p => p.Item2.Id));
        var alias = SqlOf(ctx, simple.RightJoin<IComplexEntity>(complex, (s, c) => s.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        alias.Should().Be(positional);
        alias.Should().Contain(" right join complex_entity");
    }

    [Fact]
    public void Full_alias_join_fails_closed_because_mysql_has_no_full_join()
    {
        using var ctx = MySqlTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, simple.FullJoin<IComplexEntity>(complex, (s, c) => s.Id == c.Id, Alias.Buyer).Select(p => p.Buyer.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*Full join*");
    }

    [Fact]
    public void Cross_alias_join_emits_cross_join_and_matches_positional()
    {
        using var ctx = MySqlTestContext.Create();
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
        using var ctx = MySqlTestContext.Create();
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
        using var ctx = MySqlTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var alias = SqlOf(ctx, simple.CrossApply(complex, Alias.Buyer).Select(p => p.Buyer.Id));

        // A plain table cannot be LATERAL, so MySQL renders a cross join.
        alias.Should().Contain(" cross join complex_entity");
    }

    [Fact]
    public void OuterApply_alias_join_emits_left_join_on_true_on_plain_table_because_supported()
    {
        using var ctx = MySqlTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var alias = SqlOf(ctx, simple.OuterApply(complex, Alias.Buyer).Select(p => p.Buyer.Id));

        alias.Should().Contain(" left join complex_entity").And.Contain(" on true");
    }
}
