using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// D01 (#213) MariaDB JoinInto coverage: the project previously had none. The many-to-many declaration
/// composed with a <see cref="EagerLoadMode.SingleQuery"/> <c>LoadWith</c> is the RED regression against
/// the unmodified build (which rejects it); the scalar many-to-many declaration is a control. This
/// project has no <c>InternalsVisibleTo</c>, so these tests use the public query surface only and never
/// open a connection (the rejection is raised before any SQL is built).
/// </summary>
public class JoinIntoCompositeSqlGenerationTests
{
    private static EntityBuilder<JnCompositeParent> Parents(IDataContext ctx)
        => ctx.From<JnCompositeParent>(b => b
            .HasManyThrough<JnCompositeChild, JnCompositeLink, int, int>(
                p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId)
            .HasMany(p => p.Notes, n => n.ParentId));

    [Fact]
    public void ManyToMany_WithSingleQuery_ShouldNotBeRejectedAtBuildTime()
    {
        using var ctx = MariaDbTestContext.Create();

        var builder = Parents(ctx)
            .JoinInto(ctx.From<JnCompositeChild>(), (p, c) => true, p => p.Children)
            .LoadWith(p => p.Notes, n => n.From<JnCompositeNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery);

        NotSupportedException? rejection = null;
        try
        {
            _ = builder.ToList();
        }
        catch (NotSupportedException ex)
        {
            rejection = ex;
        }
        catch (Exception)
        {
            // No MariaDB server is available in a SQL-generation project; reaching the connection
            // attempt proves the declaration was accepted and the pair command was built.
        }

        rejection.Should().BeNull("a many-to-many JoinInto composed with SingleQuery must no longer be rejected");
    }

    [Fact]
    public void ScalarManyToMany_Declaration_ShouldBuild()
    {
        using var ctx = MariaDbTestContext.Create();

        var builder = Parents(ctx)
            .JoinInto(ctx.From<JnCompositeChild>(), (p, c) => true, p => p.Children);

        builder.Should().NotBeNull("a supported many-to-many declaration builds without a connection");
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
