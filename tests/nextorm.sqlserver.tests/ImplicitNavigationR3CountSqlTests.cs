using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// #148-B r3 A3′ (D-R3-2): a navigation <c>Count()</c> / property <c>Count</c> used in a predicate or
/// projection is emitted as the wide (<c>count_big</c>) correlated count with no in-database Int32
/// narrowing cast. The checked narrowing is a CLR-only materialization boundary.
/// </summary>
public class ImplicitNavigationR3CountSqlTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText).ToLowerInvariant();

    [Fact]
    public void Nav_count_predicate_and_projection_should_emit_the_wide_count_without_an_int_cast()
    {
        using var ctx = SqlServerTestContext.Create();
        ctx.From<R3CountChild>();
        ctx.From<R3CountParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

        var predicate = SqlOf(ctx, ctx.From<R3CountParent>().Where(p => p.Children.Count() > 1).ToCommand());
        var projected = SqlOf(ctx, ctx.From<R3CountParent>().Select(p => new { p.Id, C = p.Children.Count() }));
        var longCount = SqlOf(ctx, ctx.From<R3CountParent>().Select(p => new { p.Id, L = p.Children.LongCount() }));

        foreach (var sql in new[] { predicate, projected, longCount })
        {
            sql.Should().Contain("count_big(");
            sql.Should().NotContain("cast(count_big(");
            sql.Should().NotContain(" as int)");
        }
    }
}

[SqlTable("r3ss_parent")]
public sealed class R3CountParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<R3CountChild> Children { get; set; } = new List<R3CountChild>();
}

[SqlTable("r3ss_child")]
public sealed class R3CountChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}
