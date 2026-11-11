using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T306 (#306): a single-source mapped-entity identity projection (<c>From&lt;T&gt;().Select(x =&gt; x)</c>)
/// must reuse the ordinary entity column selection, render the full explicit column list and stay
/// byte-identical to the unchanged <c>ToCommand()</c> control. No connection is opened; only the
/// generated SQL text is asserted.
/// </summary>
public class T306IdentityProjectionTests
{
    private const string ExpectedSql = "select id, name\n from entity_parent\n order by id";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void IdentityProjection_ShouldRenderTheExplicitEntityColumns()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<EntityParent>().OrderBy(x => x.Id).Select(x => x));

        sql.Should().Be(ExpectedSql);
    }

    [Fact]
    public void IdentityProjection_ShouldBeIdenticalToTheToCommandControl()
    {
        using var ctx = SqliteTestContext.Create();

        var identity = SqlOf(ctx, ctx.From<EntityParent>().OrderBy(x => x.Id).Select(x => x));
        var control = SqlOf(ctx, ctx.From<EntityParent>().OrderBy(x => x.Id).ToCommand());

        identity.Should().Be(control).And.Be(ExpectedSql);
    }

    [Fact]
    public void IdentityProjection_ShouldNotReturnTheTruncatedSelectToken()
    {
        using var ctx = SqliteTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<EntityParent>().OrderBy(x => x.Id).Select(x => x));

        sql.Should().StartWith("select ");
        sql.Should().NotContain("selec\n", "the pre-fix projection truncated 'select ' to 'selec'");
        sql.Should().NotBe("selec\n from entity_parent\n order by id");
    }
}

/// <summary>The mapped single source the identity projection reads (columns <c>id</c>, <c>name</c>).</summary>
[SqlTable("entity_parent")]
public sealed class EntityParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
