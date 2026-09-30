using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// SQL-generation tests for the many-to-many <c>JoinInto</c> (#135): the junction is projected as a
/// derived <c>row_number()</c> link subquery between the parent and two flat joins, LEFT/LEFT for a
/// LEFT declaration and INNER/INNER for an INNER one, with any user predicate appended to the child ON.
/// These tests never open a connection.
/// </summary>
public class JoinIntoManyToManySqlGenerationTests
{
    private static string Canon(string sql)
    {
        var s = sql
            .Replace("\r\n", "\n")
            .Replace("\"", string.Empty)
            .Replace("'", string.Empty)
            .Replace("`", string.Empty)
            .Replace("[", string.Empty)
            .Replace("]", string.Empty)
            .Replace("_", string.Empty);
        return Regex.Replace(s, @"\s+", " ").Trim().ToLowerInvariant();
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Canon(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static EntityBuilder<JoinIntoManyParent> Parents(IDataContext ctx)
    {
        return ctx.From<JoinIntoManyParent>(b => b.HasManyThrough<JoinIntoManyChild, JoinIntoManyLink, int, int>(
            p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));
    }

    [Fact]
    public void ManyToMany_Left_ShouldRenderDerivedLinkSubqueryWithTwoLeftJoins()
    {
        using var ctx = ClickHouseTestContext.Create();

        string sql = SqlOf(ctx, (dynamic)Parents(ctx)
            .JoinInto(ctx.From<JoinIntoManyChild>(), (p, c) => c.Id > 0, p => p.Children)
            .CreateJoinIntoPairCommand());

        sql.Should().MatchRegex(@"left join \(select .*?from joinmnlink\)");
        sql.Should().MatchRegex(@"rownumber\(\)\s*over\s*\(\s*partition by\s+[^)]+order by\s+[^)]+\)\s*as occurrence");
        sql.Should().Contain("from joinmnparent");
        sql.Should().Contain("parentkey").And.Contain("childkey");
        sql.Should().MatchRegex(@"on \w+\.id = \w+\.parentkey");
        sql.Should().MatchRegex(@"on \(?\w+\.id = \w+\.childkey");
        Regex.Matches(sql, @"left join").Count.Should().Be(2);
        sql.Should().NotMatchRegex(@"\(\s*\(");
    }

    [Fact]
    public void ManyToMany_Inner_ShouldRenderTwoInnerJoinsAndNoLeftJoin()
    {
        using var ctx = ClickHouseTestContext.Create();

        string sql = SqlOf(ctx, (dynamic)Parents(ctx)
            .JoinInto(ctx.From<JoinIntoManyChild>(), (p, c) => c.Id > 0, p => p.Children, JoinType.Inner)
            .CreateJoinIntoPairCommand());

        Regex.Matches(sql, @"left join").Count.Should().Be(0);
        Regex.Matches(sql, @"\bjoin\b").Count.Should().Be(2);
        sql.Should().Contain("from joinmnlink");
    }

    [Fact]
    public void ManyToMany_ChildOn_ShouldAndTheUserPredicateIntoTheChildJoin()
    {
        using var ctx = ClickHouseTestContext.Create();

        string sql = SqlOf(ctx, (dynamic)Parents(ctx)
            .JoinInto(ctx.From<JoinIntoManyChild>(), (p, c) => c.Name == p.Name, p => p.Children)
            .CreateJoinIntoPairCommand());

        sql.Should().MatchRegex(@"on \(?\w+\.id = \w+\.childkey and \w+\.name = \w+\.name");
    }

    [Fact]
    public void ManyToMany_ShouldRenderFlatJoinsWithoutNestedParentheses()
    {
        using var ctx = ClickHouseTestContext.Create();

        string sql = SqlOf(ctx, (dynamic)Parents(ctx)
            .JoinInto(ctx.From<JoinIntoManyChild>(), (p, c) => c.Id > 0, p => p.Children)
            .CreateJoinIntoPairCommand());

        sql.Should().NotMatchRegex(@"\(\s*\(");
    }
}

[SqlTable("join_mn_parent")]
public sealed class JoinIntoManyParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<JoinIntoManyChild> Children { get; set; } = new List<JoinIntoManyChild>();
}

[SqlTable("join_mn_child")]
public sealed class JoinIntoManyChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("join_mn_link")]
public sealed class JoinIntoManyLink
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
