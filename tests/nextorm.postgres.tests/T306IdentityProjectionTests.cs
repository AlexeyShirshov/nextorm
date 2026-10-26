using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// T306 (#306): the PostgreSQL dialect renders the single-source mapped-entity identity projection
/// exactly like the unchanged <c>ToCommand()</c> control - the full explicit mapped column list, not a
/// truncated <c>selec</c> head. No connection is opened; only the generated SQL text is asserted.
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
        using var ctx = PostgresTestContext.Create();

        var sql = SqlOf(ctx, ctx.From<EntityParent>().OrderBy(x => x.Id).Select(x => x));

        sql.Should().Be(ExpectedSql);
    }

    [Fact]
    public void IdentityProjection_ShouldBeIdenticalToTheToCommandControl()
    {
        using var ctx = PostgresTestContext.Create();

        var identity = SqlOf(ctx, ctx.From<EntityParent>().OrderBy(x => x.Id).Select(x => x));
        var control = SqlOf(ctx, ctx.From<EntityParent>().OrderBy(x => x.Id).ToCommand());

        identity.Should().Be(control).And.Be(ExpectedSql);
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
