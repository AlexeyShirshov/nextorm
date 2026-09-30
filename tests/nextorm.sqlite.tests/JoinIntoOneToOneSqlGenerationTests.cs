using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL-generation tests for the one-to-one <c>JoinInto</c> overload (#135 slice A): the reference
/// navigation renders the same <c>LEFT</c>/<c>INNER</c> join as the collection form while the user
/// projection stays the parent only.
/// </summary>
public class JoinIntoOneToOneSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static EntityBuilder<O2oParent> Parents(IDataContext ctx) =>
        ctx.From<O2oParent>(b => b.HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId));

    [Fact]
    public void OneToOneJoinInto_Left_ShouldRenderLeftJoinAndKeepTheParentProjection()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<O2oChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToCommand());

        sql.Should().Contain("select t1.id, t1.name from o2o_parent as 't1'")
            .And.Contain("left join o2o_child as 't2' on t1.id = t2.parent_id");
    }

    [Fact]
    public void OneToOneJoinInto_Inner_ShouldRenderInnerJoin()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<O2oChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary, JoinType.Inner)
            .ToCommand());

        sql.Should().Contain("select t1.id, t1.name from o2o_parent as 't1'")
            .And.Contain(" join o2o_child as 't2' on t1.id = t2.parent_id")
            .And.NotContain("left join", "an Inner one-to-one JoinInto must render an inner join");
    }

    [Fact]
    public void OneToOneJoinInto_ParentOnlyTerminal_ShouldExcludeTheStitchingJoin()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, Parents(ctx)
            .JoinInto(ctx.From<O2oChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .ToParentCommand());

        sql.Should().Be("select id, name from o2o_parent");
    }
}

[SqlTable("o2o_parent")]
public sealed class O2oParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public O2oChild? Primary { get; set; }
}

[SqlTable("o2o_child")]
public sealed class O2oChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
