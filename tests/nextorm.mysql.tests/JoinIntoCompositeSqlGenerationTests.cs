using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// D01 (#213) SQL-generation red regression for composing a many-to-many <c>JoinInto</c> declaration
/// with a <see cref="EagerLoadMode.SingleQuery"/> <c>LoadWith</c>: the unmodified build rejects any
/// many-to-many declaration in the single-query dispatcher, so this renders the combined pair command
/// once the restriction is lifted. These tests never open a connection.
/// </summary>
[Collection("Query plan cache")]
public class JoinIntoCompositeSqlGenerationTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    private static string SingleQuerySql(IDataContext ctx, EntityBuilder<JnCompositeParent> builder)
    {
        var (specs, joins) = builder.BuildSingleQueryJoins();
        return SqlOf(ctx, (dynamic)builder.CreatePairCommand(specs, joins));
    }

    private static EntityBuilder<JnCompositeParent> Parents(IDataContext ctx)
        => ctx.From<JnCompositeParent>(b => b
            .HasManyThrough<JnCompositeChild, JnCompositeLink, int, int>(
                p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId)
            .HasMany(p => p.Notes, n => n.ParentId));

    [Fact]
    public void ManyToMany_WithSingleQueryLoadWith_ShouldRenderBothCollections()
    {
        using var ctx = MySqlTestContext.Create();

        var builder = Parents(ctx)
            .JoinInto(ctx.From<JnCompositeChild>(), (p, c) => true, p => p.Children)
            .LoadWith(p => p.Notes, n => n.From<JnCompositeNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery);

        var sql = SingleQuerySql(ctx, builder);

        sql.Should().Contain("jn_comp_link", "the derived junction link source is rendered");
        sql.Should().Contain("jn_comp_child", "the many-to-many child is joined");
        sql.Should().Contain("jn_comp_note", "the single-query LoadWith child is joined");
    }
}

[SqlTable("jn_comp_parent")]
public sealed class JnCompositeParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<JnCompositeChild> Children { get; set; } = new List<JnCompositeChild>();

    public ICollection<JnCompositeNote> Notes { get; set; } = new List<JnCompositeNote>();
}

[SqlTable("jn_comp_child")]
public sealed class JnCompositeChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("jn_comp_link")]
public sealed class JnCompositeLink
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}

[SqlTable("jn_comp_note")]
public sealed class JnCompositeNote
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("text")]
    public string? Text { get; set; }
}
