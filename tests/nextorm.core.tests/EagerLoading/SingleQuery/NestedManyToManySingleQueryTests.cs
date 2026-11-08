using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests;

/// <summary>
/// #249 (T353 D1): the single-query flattened row layout must give every node its own physical arity. A
/// many-to-many declaration occupies <b>two</b> projection items (the link item followed by the child), while
/// <see cref="IEagerLoadNodeSpec.SubtreeItemCount"/> used to count one per node. A many-to-many node nested
/// under a child therefore had its slice start handed to the recursive stitcher as the <b>child</b> column, so
/// the link item was read as the child, skipped by the <c>is not TChild</c> guard, and the collection came back
/// <b>empty</b> in one statement with no exception. A sibling declared after a many-to-many node additionally
/// started one column early.
/// <para>
/// Runs against a real SQLite file (no container) because the defect is only observable on the single-query
/// path that flattens the graph into one statement, and the statement count is part of the oracle.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public sealed class NestedManyToManySingleQueryTests
{
    public NestedManyToManySingleQueryTests()
    {
        DataContextCache.Clear();
    }

    [SqlTable("t353_mn_root")]
    public sealed class Root
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        public ICollection<Mid> Mids { get; } = new List<Mid>();

        public ICollection<Tag> Tags { get; } = new List<Tag>();
    }

    [SqlTable("t353_mn_mid")]
    public sealed class Mid
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("root_id")]
        public int RootId { get; set; }

        public ICollection<Tag> Tags { get; } = new List<Tag>();

        public ICollection<Note> Notes { get; } = new List<Note>();
    }

    [SqlTable("t353_mn_tag")]
    public sealed class Tag
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    [SqlTable("t353_mn_note")]
    public sealed class Note
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("mid_id")]
        public int MidId { get; set; }

        [Column("text")]
        public string? Text { get; set; }
    }

    [SqlTable("t353_mn_mid_tag")]
    public sealed class MidTag
    {
        [Column("mid_id")]
        public int MidId { get; set; }

        [Column("tag_id")]
        public int TagId { get; set; }
    }

    [SqlTable("t353_mn_root_tag")]
    public sealed class RootTag
    {
        [Column("root_id")]
        public int RootId { get; set; }

        [Column("tag_id")]
        public int TagId { get; set; }
    }

    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
            => Statements.Add(command.CommandText.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Root 1 -> mids 10, 11; root 2 -> mid 12. Mid 10 owns tags 100/"a" and 101/"b", mid 11 owns tag 102/"c",
    /// mid 12 owns none. The junction also carries a dangling link (mid 10 -> tag 999) whose tag row does not
    /// exist, so an outer join must not invent a tag.
    /// </summary>
    private static (SqliteDataContext Ctx, string Path, SqlRecordingInterceptor Recorder) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-t353-mn-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table t353_mn_root (id integer primary key);" +
                "create table t353_mn_mid (id integer primary key, root_id int not null);" +
                "create table t353_mn_tag (id integer primary key, name text null);" +
                "create table t353_mn_note (id integer primary key, mid_id int not null, text text null);" +
                "create table t353_mn_mid_tag (mid_id int not null, tag_id int not null);" +
                "create table t353_mn_root_tag (root_id int not null, tag_id int not null);" +
                "insert into t353_mn_root (id) values (1), (2);" +
                "insert into t353_mn_mid (id, root_id) values (10, 1), (11, 1), (12, 2);" +
                "insert into t353_mn_tag (id, name) values (100, 'a'), (101, 'b'), (102, 'c');" +
                "insert into t353_mn_note (id, mid_id, text) values (200, 10, 'n10'), (201, 11, 'n11'), (202, 12, 'n12');" +
                "insert into t353_mn_mid_tag (mid_id, tag_id) values (10, 100), (10, 101), (11, 102), (10, 999);" +
                "insert into t353_mn_root_tag (root_id, tag_id) values (1, 100), (1, 102), (2, 101);";
            cmd.ExecuteNonQuery();
        }

        var context = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        var recorder = new SqlRecordingInterceptor();
        context.AddInterceptor(recorder);
        return (context, path, recorder);
    }

    /// <summary>Registers the many-to-many metadata the nested declarations bind through.</summary>
    private static void RegisterMetadata(SqliteDataContext context)
    {
        _ = context.From<Root>(cfg => cfg
            .HasManyThrough<Tag, RootTag, int, int>(r => r.Tags, r => r.Id, l => l.RootId, t => t.Id, l => l.TagId));
        _ = context.From<Mid>(cfg => cfg
            .HasManyThrough<Tag, MidTag, int, int>(m => m.Tags, m => m.Id, l => l.MidId, t => t.Id, l => l.TagId));
    }

    [Fact]
    public void SingleQuery_Depth2_ManyToMany_ShouldReturnThePerParentTags()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            var roots = context.From<Root>()
                .LoadWith(
                    r => r.Mids,
                    c => c.From<Mid>().LoadWith(m => m.Tags),
                    r => r.Id,
                    m => m.RootId,
                    EagerLoadMode.SingleQuery)
                .OrderBy(r => r.Id)
                .ToList();

            roots.Select(r => r.Id).Should().Equal(1, 2);
            roots.Single(r => r.Id == 1).Mids.Single(m => m.Id == 10).Tags.Select(t => t.Id).Should().Equal(100, 101);
            roots.Single(r => r.Id == 1).Mids.Single(m => m.Id == 11).Tags.Select(t => t.Id).Should().Equal(102);
            roots.Single(r => r.Id == 2).Mids.Single(m => m.Id == 12).Tags.Should().BeEmpty(
                "a mid with no surviving junction occurrence owns an empty collection, not null");
            roots.SelectMany(r => r.Mids).SelectMany(m => m.Tags).Select(t => t.Id).Should().NotContain(999,
                "a dangling junction row has no child row and must not produce one");

            recorder.Statements.Should().HaveCount(1, "the whole depth-2 graph is one denormalized statement");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_Depth2_ManyToManyWithSiblingOneToMany_ShouldStitchBothLevelTwoCollections()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            // The many-to-many declaration comes first on purpose: it occupies two projection items, so a
            // node that strides by one leaves the sibling one-to-many reading the link column.
            var roots = context.From<Root>()
                .LoadWith(
                    r => r.Mids,
                    c => c.From<Mid>()
                        .LoadWith(m => m.Tags)
                        .LoadWith(m => m.Notes, n => n.From<Note>(), m => m.Id, n => n.MidId),
                    r => r.Id,
                    m => m.RootId,
                    EagerLoadMode.SingleQuery)
                .OrderBy(r => r.Id)
                .ToList();

            var mid10 = roots.Single(r => r.Id == 1).Mids.Single(m => m.Id == 10);
            mid10.Tags.Select(t => t.Id).Should().Equal(100, 101);
            mid10.Notes.Select(n => n.Id).Should().Equal(200);

            var mid12 = roots.Single(r => r.Id == 2).Mids.Single(m => m.Id == 12);
            mid12.Tags.Should().BeEmpty();
            mid12.Notes.Select(n => n.Id).Should().Equal(202);

            recorder.Statements.Should().HaveCount(1);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_FlatManyToMany_ShouldKeepItsRows()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            var roots = context.From<Root>()
                .LoadWith(r => r.Tags, EagerLoadMode.SingleQuery)
                .OrderBy(r => r.Id)
                .ToList();

            roots.Single(r => r.Id == 1).Tags.Select(t => t.Id).Should().Equal(100, 102);
            roots.Single(r => r.Id == 2).Tags.Select(t => t.Id).Should().Equal(101);

            recorder.Statements.Should().HaveCount(1);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SingleQuery_Depth2_OneToMany_ShouldKeepItsRows()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            var roots = context.From<Root>()
                .LoadWith(
                    r => r.Mids,
                    c => c.From<Mid>().LoadWith(m => m.Notes, n => n.From<Note>(), m => m.Id, n => n.MidId),
                    r => r.Id,
                    m => m.RootId,
                    EagerLoadMode.SingleQuery)
                .OrderBy(r => r.Id)
                .ToList();

            roots.Single(r => r.Id == 1).Mids.Select(m => m.Id).Should().Equal(10, 11);
            roots.Single(r => r.Id == 1).Mids.Single(m => m.Id == 10).Notes.Select(n => n.Id).Should().Equal(200);
            roots.Single(r => r.Id == 2).Mids.Single(m => m.Id == 12).Notes.Select(n => n.Id).Should().Equal(202);

            recorder.Statements.Should().HaveCount(1);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }
}
