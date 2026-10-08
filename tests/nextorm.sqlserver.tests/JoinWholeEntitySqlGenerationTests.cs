using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// SQL-generation coverage for a direct whole-entity projection of a joined item
/// (<c>Select(p =&gt; p.ItemN)</c>): the statement must contain exactly the selected entity's mapped
/// columns and no wildcard, so the provider renders the agreed explicit-column form of <c>tN.*</c>.
/// These tests never open a connection, so a placeholder connection string is enough.
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

    [Fact]
    public void DirectChild_InnerJoin_ShouldSelectOnlyChildColumns()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<JwParent>()
            .Join(ctx.From<JwChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => p.Item2));

        sql.Should().Contain("child_value");
        sql.Should().Contain("parent_id");
        sql.Should().NotContain("parent_label", "only the selected child entity may be projected");
        sql.Should().NotContain("*", "explicit mapped columns are the agreed form of the projection");
    }

    [Fact]
    public void DirectParent_LeftJoin_ShouldSelectOnlyParentColumns()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<JwParent>()
            .LeftJoin(ctx.From<JwChild>(), (p, c) => p.Id == c.ParentId)
            .Select(p => p.Item1));

        sql.Should().Contain("parent_label");
        sql.Should().NotContain("child_value", "only the selected parent entity may be projected");
        sql.Should().NotContain("*", "explicit mapped columns are the agreed form of the projection");
    }
}
