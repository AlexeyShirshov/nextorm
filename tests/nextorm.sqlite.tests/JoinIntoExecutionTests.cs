using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Executes <c>JoinInto</c> against a real SQLite database (slice B): one denormalized round trip,
/// LEFT/INNER stitching, parent deduplication/grouping and parent paging.
/// </summary>
public class JoinIntoExecutionTests
{
    private static (SqliteDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-joininto-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table exec_parent (id integer primary key, name text);" +
                "create table exec_child (id integer primary key, parent_id integer, name text);" +
                "create table exec_note (id integer primary key, parent_id integer, text text);" +
                "insert into exec_parent (id, name) values (1, 'p1'), (2, 'p2'), (3, 'p3');" +
                "insert into exec_child (id, parent_id, name) values (10, 1, 'c1'), (11, 1, 'c2'), (12, 2, 'c3');" +
                "insert into exec_note (id, parent_id, text) values (100, 1, 'n1'), (101, 1, 'n2');";
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder().AddInterceptor(new CountingInterceptor()));
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    private static EntityBuilder<ExecParent> Parents(SqliteDataContext ctx) =>
        ctx.From<ExecParent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId));

    [Fact]
    public void ListTerminal_ShouldExecuteOneRoundTrip_AndGroupChildren()
    {
        var (ctx, path) = CreateDb();
        try
        {
            CountingInterceptor.Reset();

            var parents = Parents(ctx)
                .JoinInto(ctx.From<ExecChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .ToList();

            CountingInterceptor.Executions.Should().Be(1, "JoinInto stitches from one denormalized round trip");
            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);
            parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(12);
            parents.Single(p => p.Id == 3).Children.Should().BeEmpty();
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public async Task ListTerminalAsync_ShouldExecuteOneRoundTrip_AndGroupChildren()
    {
        var (ctx, path) = CreateDb();
        try
        {
            CountingInterceptor.Reset();

            var parents = await Parents(ctx)
                .JoinInto(ctx.From<ExecChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .ToListAsync();

            CountingInterceptor.Executions.Should().Be(1, "JoinInto stitches from one denormalized round trip on the async path");

            parents.Select(p => p.Id).Should().Equal(1, 2, 3);
            parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);
            parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(12);
            parents.Single(p => p.Id == 3).Children.Should().BeEmpty();
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public async Task InnerJoinIntoAsync_ShouldExcludeChildlessParents()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var parents = await Parents(ctx)
                .JoinInto(ctx.From<ExecChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
                .ToListAsync();

            parents.Select(p => p.Id).Should().Equal(1, 2);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void InnerJoinInto_ShouldExcludeChildlessParents()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var parents = Parents(ctx)
                .JoinInto(ctx.From<ExecChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void ParentPaging_ShouldLimitParents_NotDenormalizedRows()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var parents = Parents(ctx)
                .JoinInto(ctx.From<ExecChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .Limit(1)
                .ToList();

            parents.Should().HaveCount(1);
            parents[0].Id.Should().Be(1);
            parents[0].Children.Select(c => c.Id).Should().Equal(new[] { 10, 11 }, "paging a parent must not truncate its children");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void MultipleCollections_ShouldGroupEachIndependently()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var parents = Parents(ctx)
                .JoinInto(ctx.From<ExecChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .JoinInto(ctx.From<ExecNote>(), (p, n) => p.Id == n.ParentId, p => p.Notes)
                .ToList();

            parents.Should().HaveCount(3);
            parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);
            parents.Single(p => p.Id == 1).Notes.Select(n => n.Id).Should().Equal(100, 101);
            parents.Single(p => p.Id == 2).Children.Should().HaveCount(1);
            parents.Single(p => p.Id == 2).Notes.Should().BeEmpty();
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void CountAnyFirst_ShouldBeParentOnly()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var builder = Parents(ctx)
                .JoinInto(ctx.From<ExecChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

            builder.Count().Should().Be(3);
            builder.Any().Should().BeTrue();
            builder.Where(p => p.Id == 1).First().Children.Should().BeEmpty("non-list terminals do not stitch");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    private sealed class CountingInterceptor : IQueryInterceptor
    {
        private static int _executions;

        private static string? _lastSql;

        public static int Executions => Volatile.Read(ref _executions);

        public static string? LastSql => Volatile.Read(ref _lastSql);

        public static void Reset()
        {
            Volatile.Write(ref _executions, 0);
            Volatile.Write(ref _lastSql, null);
        }

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
        {
            Interlocked.Increment(ref _executions);
            Volatile.Write(ref _lastSql, command.CommandText);
        }
    }
}

[SqlTable("exec_parent")]
public sealed class ExecParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<ExecChild> Children { get; set; } = new List<ExecChild>();

    public ICollection<ExecNote> Notes { get; set; } = new List<ExecNote>();
}

[SqlTable("exec_child")]
public sealed class ExecChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("exec_note")]
public sealed class ExecNote
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("text")]
    public string? Text { get; set; }
}
