using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Regression tests for the SQL execution of <c>JoinInto</c> stitching (#105): the child dedup of a
/// cartesian join must use the entity's composite key, and parent deduplication must use the parent's
/// own identity rather than the first declaration's key.
/// </summary>
public class JoinIntoCartesianDedupTests
{
    private static (SqliteDataContext Ctx, string Path) CreateDb(string schema)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-joininto-dedup-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = schema;
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    [Fact]
    public void CartesianJoin_WithCompositeChildKey_ShouldNotDuplicateChildren()
    {
        var (ctx, path) = CreateDb(
            "create table dedup_parent (id integer primary key, name text);" +
            "create table dedup_child (a integer, b integer, parent_id integer, name text, primary key (a, b));" +
            "create table dedup_note (id integer primary key, parent_id integer, text text);" +
            "insert into dedup_parent (id, name) values (1, 'p1');" +
            "insert into dedup_child (a, b, parent_id, name) values (1, 1, 1, 'c1'), (2, 1, 1, 'c2');" +
            "insert into dedup_note (id, parent_id, text) values (100, 1, 'n1'), (101, 1, 'n2');");
        try
        {
            var parents = ctx.From<DedupParent>(b => b
                    .HasMany(p => p.Children, c => c.ParentId)
                    .HasMany(p => p.Notes, n => n.ParentId))
                .JoinInto(ctx.From<DedupChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .JoinInto(ctx.From<DedupNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
                .ToList();

            parents.Should().HaveCount(1);
            parents[0].Children.Select(c => c.A).Should().Equal(new[] { 1, 2 }, "the cartesian join must not repeat composite-keyed children");
            parents[0].Notes.Select(n => n.Id).Should().Equal(100, 101);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void ExplicitKeyJoin_WithNonUniqueSpecKey_ShouldKeepDistinctParents()
    {
        var (ctx, path) = CreateDb(
            "create table parent_dup (id integer primary key, name text);" +
            "create table child_dup (id integer primary key, parent_id integer, name text);" +
            "insert into parent_dup (id, name) values (1, 'dup'), (2, 'dup');" +
            "insert into child_dup (id, parent_id, name) values (10, 1, 'dup');");
        try
        {
            var parents = ctx.From<ParentDup>(b => b.HasMany(p => p.Children, c => c.ParentId))
                .JoinInto(ctx.From<ChildDup>(), (p, c) => p.Name == c.Name, p => p.Children, p => p.Name, c => c.Name)
                .ToList();

            parents.Should().HaveCount(2, "parents are identified by their own key, not by the first declaration's non-unique key");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }
}

[SqlTable("dedup_parent")]
public sealed class DedupParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<DedupChild> Children { get; set; } = new List<DedupChild>();

    public ICollection<DedupNote> Notes { get; set; } = new List<DedupNote>();
}

[SqlTable("dedup_child")]
public sealed class DedupChild
{
    [Key]
    [Column("a")]
    public int A { get; set; }

    [Key]
    [Column("b")]
    public int B { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("dedup_note")]
public sealed class DedupNote
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("text")]
    public string? Text { get; set; }
}

[SqlTable("parent_dup")]
public sealed class ParentDup
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    public ICollection<ChildDup> Children { get; set; } = new List<ChildDup>();
}

[SqlTable("child_dup")]
public sealed class ChildDup
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;
}
