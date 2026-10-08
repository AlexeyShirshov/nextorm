using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// SQL-generation coverage for a direct whole-entity projection of a joined item
/// (<c>Select(p =&gt; p.ItemN)</c>): the statement must contain exactly the selected entity's mapped
/// columns and no wildcard, so the provider renders the agreed explicit-column form of <c>tN.*</c>.
/// The assertions are anchored to the SELECT clause (everything before the first <c>from</c>) so a
/// column named in a JOIN predicate cannot satisfy them. These tests never open a connection.
/// </summary>
public class JoinWholeEntitySqlGenerationTests
{
    [SqlTable("jw_parent")]
    public sealed class JwParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_label")]
        public string? Label { get; set; }
    }

    [SqlTable("jw_child")]
    public sealed class JwChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("child_value")]
        public string? Value { get; set; }
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    private static string SelectClause(string sql)
    {
        var from = sql.IndexOf(" from ", StringComparison.OrdinalIgnoreCase);
        return from < 0 ? sql : sql[..from];
    }

    [Fact]
    public void DirectChild_InnerJoin_ShouldSelectOnlyChildColumns()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<JwParent>()
            .Join(ctx.From<JwChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => p.Item2));

        var selectClause = SelectClause(sql);
        selectClause.Should().StartWith("select", "the statement must open with the SELECT clause");
        selectClause.Should().Contain("child_value");
        selectClause.Should().Contain("parent_id");
        selectClause.Should().NotContain("parent_label", "only the selected child entity may be projected");
        selectClause.Should().NotContain("*", "explicit mapped columns are the agreed form of the projection");
    }

    [Fact]
    public void DirectParent_LeftJoin_ShouldSelectOnlyParentColumns()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<JwParent>()
            .LeftJoin(ctx.From<JwChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => p.Item1));

        var selectClause = SelectClause(sql);
        selectClause.Should().StartWith("select");
        selectClause.Should().Contain("parent_label");
        selectClause.Should().NotContain("child_value", "only the selected parent entity may be projected");
        selectClause.Should().NotContain("*", "explicit mapped columns are the agreed form of the projection");
    }

    [Fact]
    public void DirectThirdItem_Arity3_ShouldSelectOnlyThirdEntityColumns()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<JwParent>()
            .Join(ctx.From<JwChild>(), (p, c) => p.Id == c.ParentId)
            .Join(ctx.From<JwChild>(), (p, c) => p.Item2.Id == c.ParentId)
            .Select(p => p.Item3));

        var selectClause = SelectClause(sql);
        selectClause.Should().Contain("child_value");
        selectClause.Should().NotContain("parent_label");
        selectClause.Should().NotContain("*");
    }

    [Fact]
    public void DirectEighthItem_Arity8_ShouldSelectOnlyEighthEntityColumns()
    {
        using var ctx = ClickHouseTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<JwParent>()
            .Join(ctx.From<JwChild>(), (p, c) => p.Id == c.ParentId)
            .Join(ctx.From<JwChild>(), (p, c) => p.Item2.Id == c.ParentId)
            .Join(ctx.From<JwChild>(), (p, c) => p.Item3.Id == c.ParentId)
            .Join(ctx.From<JwChild>(), (p, c) => p.Item4.Id == c.ParentId)
            .Join(ctx.From<JwChild>(), (p, c) => p.Item5.Id == c.ParentId)
            .Join(ctx.From<JwChild>(), (p, c) => p.Item6.Id == c.ParentId)
            .Join(ctx.From<JwChild>(), (p, c) => p.Item7.Id == c.ParentId)
            .Select(p => p.Item8));

        var selectClause = SelectClause(sql);
        selectClause.Should().Contain("child_value");
        selectClause.Should().NotContain("parent_label");
        selectClause.Should().NotContain("*");
    }
}
