using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Pins the documented <c>JoinInto</c> terminal boundary (#105): only <c>ToList</c>/<c>ToListAsync</c>
/// stitch the denormalized rows. Every other terminal forwards to the parent-only command, so the
/// collections stay unfilled and a JOIN that repeats a parent row is not stitched.
/// </summary>
public class JoinIntoTerminalBoundaryTests
{
    private static (SqliteDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-joininto-boundary-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table bound_parent (id integer primary key, name text);" +
                "create table bound_child (id integer primary key, parent_id integer, name text);" +
                "insert into bound_parent (id, name) values (1, 'p1'), (2, 'p2'), (3, 'p3');" +
                "insert into bound_child (id, parent_id, name) values (10, 1, 'c1'), (11, 1, 'c2'), (12, 2, 'c3');";
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    private static EntityBuilder<BoundaryParent> Parents(SqliteDataContext ctx) =>
        ctx.From<BoundaryParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

    private static EntityBuilder<BoundaryParent> Joined(SqliteDataContext ctx) =>
        Parents(ctx).JoinInto(ctx.From<BoundaryChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

    [Fact]
    public void ToList_ShouldStitchTheJoinedCollections()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var parents = Joined(ctx).ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);
            parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(12);
            parents.Single(p => p.Id == 3).Children.Should().BeEmpty();
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void ToArrayToHashSetAndToEnumerable_ShouldStayParentOnly()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var array = Joined(ctx).ToArray();
            array.Should().HaveCount(3, "ToArray drops the JoinInto join and returns parents only");
            array.Should().OnlyContain(p => p.Children.Count == 0);

            var set = Joined(ctx).ToHashSet();
            set.Should().HaveCount(3);
            set.Should().OnlyContain(p => p.Children.Count == 0);

            var enumerable = Joined(ctx).ToEnumerable().ToList();
            enumerable.Should().HaveCount(3);
            enumerable.Should().OnlyContain(p => p.Children.Count == 0);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public async Task ToAsyncEnumerableAndPrepare_ShouldStayParentOnly()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var streamed = new List<BoundaryParent>();
            await foreach (var parent in Joined(ctx).ToAsyncEnumerable(TestContext.Current.CancellationToken))
                streamed.Add(parent);

            streamed.Should().HaveCount(3, "ToAsyncEnumerable drops the JoinInto join and returns parents only");
            streamed.Should().OnlyContain(p => p.Children.Count == 0);

            var prepared = Joined(ctx).Prepare(cancellationToken: TestContext.Current.CancellationToken);
            ((DbPreparedQueryCommand<BoundaryParent>)prepared).DbCommand.CommandText.Should().NotContain(" join ");

            prepared.ToList(ctx).Should().HaveCount(3);
            prepared.ToList(ctx).Should().OnlyContain(p => p.Children.Count == 0);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }
}

[SqlTable("bound_parent")]
public sealed class BoundaryParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<BoundaryChild> Children { get; set; } = new List<BoundaryChild>();
}

[SqlTable("bound_child")]
public sealed class BoundaryChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
