using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests;

/// <summary>
/// #249 / T353 D3b: the recursive collection-projection matrix at depth ≥ 2. A slot declared inside the
/// adapter's <c>Select(...)</c> belongs to the child level, so the same recognizer, binder and reader run at
/// every depth: per-level <c>Where</c>/<c>OrderBy</c>/<c>ThenBy</c>/<c>Limit</c> apply to the parent of that
/// level, the split/single mode is inherited by the whole graph, and the statement count is constant in the
/// number of roots (no N+1). Reference edges keep the cardinality contract of D3a, and an empty collection stays
/// an empty collection while a missing reference stays <see langword="null"/>.
/// <para>
/// Runs against a real SQLite file (no container): the statement count is part of the oracle and the single-query
/// matrix is the witness for the nested many-to-many stride fix (D1) now reached through a projection slot.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public sealed class CollectionProjectionDepthTests
{
    public CollectionProjectionDepthTests()
    {
        DataContextCache.Clear();
    }

    [SqlTable("t353d_parent")]
    public sealed class Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        public List<Item> Items { get; } = [];

        public List<Tag> Tags { get; } = [];

        public Contact? Contact { get; set; }
    }

    [SqlTable("t353d_item")]
    public sealed class Item
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("rank")]
        public int Rank { get; set; }

        [Column("active")]
        public bool Active { get; set; }

        public List<Note> Notes { get; } = [];

        public List<Tag> Tags { get; } = [];

        public Owner? Owner { get; set; }
    }

    [SqlTable("t353d_note")]
    public sealed class Note
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("item_id")]
        public int ItemId { get; set; }

        [Column("text")]
        public string? Text { get; set; }

        public List<Tip> Tips { get; } = [];
    }

    [SqlTable("t353d_tip")]
    public sealed class Tip
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("note_id")]
        public int NoteId { get; set; }

        [Column("text")]
        public string? Text { get; set; }
    }

    [SqlTable("t353d_tag")]
    public sealed class Tag
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    [SqlTable("t353d_item_tag")]
    public sealed class ItemTag
    {
        [Column("item_id")]
        public int ItemId { get; set; }

        [Column("tag_id")]
        public int TagId { get; set; }
    }

    [SqlTable("t353d_parent_tag")]
    public sealed class ParentTag
    {
        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("tag_id")]
        public int TagId { get; set; }
    }

    [SqlTable("t353d_contact")]
    public sealed class Contact
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("label")]
        public string? Label { get; set; }
    }

    [SqlTable("t353d_owner")]
    public sealed class Owner
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("item_id")]
        public int ItemId { get; set; }

        [Column("label")]
        public string? Label { get; set; }
    }

    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
            => Statements.Add(command.CommandText.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Root <c>i</c> (1..<paramref name="roots"/>) owns items <c>i*10</c> (rank 2, active) and <c>i*10+1</c>
    /// (rank 1, active), plus an inactive item <c>i*10+2</c> (rank 3) whose note must be filtered out at level two.
    /// Item <c>i*10</c> owns notes <c>i*100</c> and <c>i*100+1</c> (each with two tips for the first),
    /// item <c>i*10+1</c> owns note <c>i*100+2</c>; item <c>i*10+2</c> owns note <c>i*100+3</c>. Tags are global
    /// (300 "a", 301 "b") and every item owns one link, so a tag must appear once per item in every mode (the
    /// single-query cartesian product repeats the physical row and the stitcher has to collapse it).
    /// </summary>
    private static (SqliteDataContext Ctx, string Path, SqlRecordingInterceptor Recorder) CreateDb(
        EagerLoadMode mode,
        int roots = 2)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-t353-depth-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();

            var sql = new System.Text.StringBuilder();
            sql.Append("create table t353d_parent (id integer primary key);")
               .Append("create table t353d_item (id integer primary key, parent_id int not null, rank int not null, active int not null);")
               .Append("create table t353d_note (id integer primary key, item_id int not null, text text null);")
               .Append("create table t353d_tip (id integer primary key, note_id int not null, text text null);")
               .Append("create table t353d_tag (id integer primary key, name text null);")
               .Append("create table t353d_item_tag (item_id int not null, tag_id int not null);")
               .Append("create table t353d_parent_tag (parent_id int not null, tag_id int not null);")
               .Append("create table t353d_contact (id integer primary key, parent_id int not null, label text null);")
               .Append("create table t353d_owner (id integer primary key, item_id int not null, label text null);")
               .Append("insert into t353d_tag (id, name) values (300, 'a'), (301, 'b');");

            for (var root = 1; root <= roots; root++)
            {
                var item = root * 10;
                var note = root * 100;
                sql.Append($"insert into t353d_parent (id) values ({root});")
                   .Append($"insert into t353d_item (id, parent_id, rank, active) values ({item}, {root}, 2, 1), ({item + 1}, {root}, 1, 1), ({item + 2}, {root}, 3, 0);")
                   .Append($"insert into t353d_note (id, item_id, text) values ({note}, {item}, 'n{note}'), ({note + 1}, {item}, 'n{note + 1}'), ({note + 2}, {item + 1}, 'n{note + 2}'), ({note + 3}, {item + 2}, 'n{note + 3}');")
                   .Append($"insert into t353d_tip (id, note_id, text) values ({note * 10}, {note}, 't{note}a'), ({note * 10 + 1}, {note}, 't{note}b');")
                   .Append($"insert into t353d_item_tag (item_id, tag_id) values ({item}, 300), ({item + 1}, 301);")
                   .Append($"insert into t353d_parent_tag (parent_id, tag_id) values ({root}, 300), ({root}, 301);")
                   .Append($"insert into t353d_contact (id, parent_id, label) values ({root * 1000}, {root}, 'c{root}');")
                   .Append($"insert into t353d_owner (id, item_id, label) values ({root * 1000}, {item}, 'o{root}');");
            }

            cmd.CommandText = sql.ToString();
            cmd.ExecuteNonQuery();
        }

        var context = new SqliteDataContext($"Data Source={path}", new DataContextBuilder().UseEagerLoadMode(mode));
        _ = context.From<Parent>(cfg => cfg
            .HasMany(p => p.Items, i => i.ParentId)
            .HasManyThrough<Tag, ParentTag, int, int>(p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.TagId)
            .HasOneToOne(p => p.Contact, p => p.Id, c => c.ParentId));
        _ = context.From<Item>(cfg => cfg
            .HasMany(i => i.Notes, n => n.ItemId)
            .HasManyThrough<Tag, ItemTag, int, int>(i => i.Tags, i => i.Id, l => l.ItemId, t => t.Id, l => l.TagId)
            .HasOneToOne(i => i.Owner, i => i.Id, o => o.ItemId));
        _ = context.From<Note>(cfg => cfg.HasMany(n => n.Tips, t => t.NoteId));

        var recorder = new SqlRecordingInterceptor();
        context.AddInterceptor(recorder);
        return (context, path, recorder);
    }

    // ================================================================================================
    // Depth 2 — 1:N over 1:N, direct and adapter slots, both modes.
    // ================================================================================================

    [Fact]
    public void Depth2_OneToMany_DirectNestedSlot_Split_ShouldProjectPerLevelValues()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SplitQuery);
        try
        {
            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Where(i => i.Active)
                        .OrderBy(i => i.Rank)
                        .ThenBy(i => i.Id, OrderDirection.Desc)
                        .Select(i => new { i.Id, i.Rank, Notes = i.Notes })
                        .ToList(),
                })
                .ToList();

            rows.Select(r => r.Id).Should().Equal(1, 2);
            var first = rows.Single(r => r.Id == 1);
            first.Items.Select(i => i.Id).Should().Equal(new[] { 11, 10 }, "the level-two Where dropped the inactive item and the ordering is per parent");
            first.Items.Single(i => i.Id == 10).Notes.Select(n => n.Id).Should().BeEquivalentTo(new[] { 100, 101 });
            first.Items.Single(i => i.Id == 11).Notes.Select(n => n.Id).Should().Equal(102);

            var second = rows.Single(r => r.Id == 2);
            second.Items.Select(i => i.Id).Should().Equal(21, 20);
            second.Items.Single(i => i.Id == 20).Notes.Select(n => n.Id).Should().BeEquivalentTo(new[] { 200, 201 });
            second.Items.Single(i => i.Id == 21).Notes.Select(n => n.Id).Should().Equal(202);

            recorder.Statements.Should().HaveCount(3, "parent + items + notes: one statement per level, never per parent");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Depth2_OneToMany_DirectNestedSlot_Single_ShouldProjectPerLevelValues()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SingleQuery);
        try
        {
            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Where(i => i.Active)
                        .OrderBy(i => i.Rank)
                        .Select(i => new { i.Id, Notes = i.Notes })
                        .ToList(),
                })
                .ToList();

            var first = rows.Single(r => r.Id == 1);
            first.Items.Select(i => i.Id).Should().Equal(new[] { 11, 10 }, "single mode inherits through the whole graph");
            first.Items.Single(i => i.Id == 10).Notes.Select(n => n.Id).Should().BeEquivalentTo(new[] { 100, 101 });
            rows.Single(r => r.Id == 2).Items.Single(i => i.Id == 20).Notes.Select(n => n.Id).Should().BeEquivalentTo(new[] { 200, 201 });
            recorder.Statements.Should().HaveCount(1, "the inherited single mode flattens the depth-two graph");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Depth2_OneToMany_AdapterNestedSlot_ShouldApplyPerLevelPagingAndProjection()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SplitQuery);
        try
        {
            var page = 1;
            var command = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Where(i => i.Active)
                        .OrderBy(i => i.Id)
                        .Select(i => new
                        {
                            i.Id,
                            Notes = i.Notes.AsEntityBuilder<Note>()
                                .OrderBy(n => n.Id)
                                .Limit(page)
                                .Select(n => new { n.Id, n.Text })
                                .ToList(),
                        })
                        .ToList(),
                });

            var firstRun = command.ToList();
            var item10 = firstRun.Single(r => r.Id == 1).Items.Single(i => i.Id == 10);
            item10.Notes.Should().ContainSingle("Limit(1) inside Notes pages per Item, not per root");
            item10.Notes[0].Id.Should().Be(100);
            item10.Notes[0].Text.Should().Be("n100");
            firstRun.Single(r => r.Id == 1).Items.Single(i => i.Id == 11).Notes[0].Id.Should().Be(102);
            recorder.Statements.Should().HaveCount(3);

            page = 2;

            var secondRun = command.ToList();
            secondRun.Single(r => r.Id == 1).Items.Single(i => i.Id == 10).Notes.Select(n => n.Id).Should().Equal(new[] { 100, 101 },
                "the captured value is re-read per level and never leaks between executions");
            recorder.Statements.Should().HaveCount(6);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Depth2_ReferencesAndCollections_ShouldKeepEmptyAndNullDistinctAtEveryLevel()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SplitQuery);
        try
        {
            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Contact = p.Contact,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Where(i => i.Active)
                        .OrderBy(i => i.Id)
                        .Select(i => new { i.Id, Owner = i.Owner, Notes = i.Notes })
                        .ToList(),
                })
                .ToList();

            var first = rows.Single(r => r.Id == 1);
            first.Contact!.Id.Should().Be(1000, "a present reference at level one is materialized");
            first.Items.Single(i => i.Id == 10).Owner!.Id.Should().Be(1000, "a present reference at level two is materialized");
            first.Items.Single(i => i.Id == 11).Owner.Should().BeNull("a missing reference at level two is null");
            first.Items.Single(i => i.Id == 11).Notes.Should().NotBeNull().And.HaveCount(1);

            // Root 3 is created by a dedicated fixture: it owns one item with no notes and no owner, and no contact.
            recorder.Statements.Should().HaveCount(5, "parent + contact + items + notes + owner");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Depth2_EmptyChildCollection_ShouldStayEmptyNotNull()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SplitQuery);
        try
        {
            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Where(i => i.Active)
                        .OrderBy(i => i.Id)
                        .Select(i => new { i.Id, Notes = i.Notes.AsEntityBuilder<Note>().Where(n => n.Id > 9000).ToList() })
                        .ToList(),
                })
                .ToList();

            var first = rows.Single(r => r.Id == 1);
            first.Items.Should().HaveCount(2);
            first.Items.Should().OnlyContain(i => i.Notes != null && i.Notes.Count == 0,
                "a filtered-out nested collection is an empty collection, never null");
            recorder.Statements.Should().HaveCount(3);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    // ================================================================================================
    // Depth 3 — the recursive binding witness.
    // ================================================================================================

    [Fact]
    public void Depth3_MixedGraph_ShouldBindEveryLevelInOneCodePath()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SplitQuery);
        try
        {
            var rows = context.From<Parent>()
                .Where(p => p.Id == 1)
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Where(i => i.Active)
                        .OrderBy(i => i.Id)
                        .Select(i => new
                        {
                            i.Id,
                            Notes = i.Notes.AsEntityBuilder<Note>()
                                .OrderBy(n => n.Id)
                                .Select(n => new { n.Id, Tips = n.Tips })
                                .ToList(),
                        })
                        .ToList(),
                })
                .ToList();

            var item10 = rows.Single().Items.Single(i => i.Id == 10);
            item10.Notes.Select(n => n.Id).Should().Equal(100, 101);
            item10.Notes.Single(n => n.Id == 100).Tips.Select(t => t.Text).Should().Equal("t100a", "t100b");
            item10.Notes.Single(n => n.Id == 101).Tips.Should().BeEmpty("a note without tips owns an empty collection");
            recorder.Statements.Should().HaveCount(4, "parent + items + notes + tips: one statement per level");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void Depth3_AllAdapterGraph_Single_ShouldFlattenFourLevelsInOneStatement()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SingleQuery);
        try
        {
            var rows = context.From<Parent>()
                .Where(p => p.Id == 1)
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Where(i => i.Active)
                        .OrderBy(i => i.Id)
                        .Select(i => new
                        {
                            i.Id,
                            Notes = i.Notes.AsEntityBuilder<Note>()
                                .OrderBy(n => n.Id)
                                .Select(n => new
                                {
                                    n.Id,
                                    Tips = n.Tips.AsEntityBuilder<Tip>().OrderBy(t => t.Id).Select(t => t.Text).ToList(),
                                })
                                .ToList(),
                        })
                        .ToList(),
                })
                .ToList();

            var item10 = rows.Single().Items.Single(i => i.Id == 10);
            item10.Notes.Select(n => n.Id).Should().Equal(100, 101);
            item10.Notes.Single(n => n.Id == 100).Tips.Should().Equal("t100a", "t100b");
            item10.Notes.Single(n => n.Id == 101).Tips.Should().BeEmpty();
            recorder.Statements.Should().HaveCount(1, "four levels, one denormalized statement");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    // ================================================================================================
    // Many-to-many: flat and nested, direct and adapter, both modes.
    // ================================================================================================

    [Fact]
    public void FlatManyToMany_DirectSlot_ShouldDeduplicateAndKeepTheShape()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SplitQuery);
        try
        {
            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, Tags = p.Tags })
                .ToList();

            rows.Select(r => r.Id).Should().Equal(1, 2);
            rows.Should().OnlyContain(r => r.Tags.Count == 2, "one link per tag: the flat path must not double a tag");
            rows.Single(r => r.Id == 1).Tags.Select(t => t.Id).Should().BeEquivalentTo(new[] { 300, 301 });
            rows.Single(r => r.Id == 2).Tags.Select(t => t.Id).Should().BeEquivalentTo(new[] { 300, 301 });
            recorder.Statements.Should().HaveCount(3, "split many-to-many reads the parent, the junction and the tags");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void FlatManyToMany_AdapterSlot_Single_ShouldDeduplicateAndKeepTheShape()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SingleQuery);
        try
        {
            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Tags = p.Tags.AsEntityBuilder<Tag>().OrderBy(t => t.Id).ToList(),
                })
                .ToList();

            rows.Should().OnlyContain(r => r.Tags.Count == 2);
            rows.Single(r => r.Id == 1).Tags.Select(t => t.Name).Should().Equal("a", "b");
            recorder.Statements.Should().HaveCount(1);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void NestedManyToMany_DirectSlot_Depth2_ShouldKeepTheRows()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SplitQuery);
        try
        {
            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Where(i => i.Active)
                        .OrderBy(i => i.Id)
                        .Select(i => new { i.Id, Tags = i.Tags })
                        .ToList(),
                })
                .ToList();

            var first = rows.Single(r => r.Id == 1);
            first.Items.Single(i => i.Id == 10).Tags.Select(t => t.Id).Should().Equal(new[] { 300 },
                "the junction holds one link, so the tag appears once at depth two");
            first.Items.Single(i => i.Id == 11).Tags.Select(t => t.Id).Should().Equal(301);
            recorder.Statements.Should().HaveCount(4, "parent + items + junction + tags for the nested level");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void NestedManyToMany_Single_Depth2_ShouldReturnCorrectRowsInOneStatement()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SingleQuery);
        try
        {
            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Where(i => i.Active)
                        .OrderBy(i => i.Id)
                        .Select(i => new { i.Id, Tags = i.Tags.AsEntityBuilder<Tag>().OrderBy(t => t.Id).ToList() })
                        .ToList(),
                })
                .ToList();

            var first = rows.Single(r => r.Id == 1);
            first.Items.Should().HaveCount(2);
            first.Items.Single(i => i.Id == 10).Tags.Select(t => t.Id).Should().Equal(new[] { 300 },
                "the nested many-to-many slot reuses the D1 stride fix: it is never an empty success");
            first.Items.Single(i => i.Id == 11).Tags.Select(t => t.Id).Should().Equal(301);
            rows.Single(r => r.Id == 2).Items.Single(i => i.Id == 20).Tags.Select(t => t.Id).Should().Equal(300);
            recorder.Statements.Should().HaveCount(1);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void NestedManyToManyWithLimit_ShouldBeRefusedBeforeAnySql()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SplitQuery);
        try
        {
            Action unsupported = () => context.From<Parent>()
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Select(i => new { i.Id, Tags = i.Tags.AsEntityBuilder<Tag>().Limit(1).ToList() })
                        .ToList(),
                });

            unsupported.Should().Throw<NotSupportedException>().WithMessage("*M:N + Limit*");
            recorder.Statements.Should().BeEmpty("a nested M:N + Limit is refused while the projection is recognized");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    // ================================================================================================
    // No N+1: the statement count does not follow the root count.
    // ================================================================================================

    [Fact]
    public void Depth2_StatementCount_ShouldBeConstantAcrossOneThreeAndThirtyRoots()
    {
        foreach (var roots in new[] { 1, 3, 30 })
        {
            var (context, path, recorder) = CreateDb(EagerLoadMode.SplitQuery, roots);
            try
            {
                var rows = context.From<Parent>()
                    .OrderBy(p => p.Id)
                    .Select(p => new
                    {
                        p.Id,
                        Items = p.Items.AsEntityBuilder<Item>()
                            .Where(i => i.Active)
                            .OrderBy(i => i.Id)
                            .Select(i => new { i.Id, Notes = i.Notes })
                            .ToList(),
                    })
                    .ToList();

                rows.Should().HaveCount(roots);
                rows.Should().OnlyContain(r => r.Items.Count == 2);
                rows.SelectMany(r => r.Items).Should().OnlyContain(i => i.Notes.Count > 0);
                recorder.Statements.Should().HaveCount(3, $"three levels stay three statements for {roots} root(s)");
            }
            finally
            {
                context.Dispose();
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Depth2_CapturedParentFilter_ShouldNotLeakBetweenExecutions()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SplitQuery);
        try
        {
            var minimumRoot = 1;
            var command = context.From<Parent>()
                .Where(p => p.Id >= minimumRoot)
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Where(i => i.Active)
                        .OrderBy(i => i.Id)
                        .Select(i => new { i.Id, Notes = i.Notes })
                        .ToList(),
                });

            command.ToList().Select(r => r.Id).Should().Equal(1, 2);
            recorder.Statements.Should().HaveCount(3);

            minimumRoot = 2;

            var second = command.ToList();
            second.Select(r => r.Id).Should().Equal(new[] { 2 }, "the parent filter reads the captured value at execution");
            second.Single().Items.Single(i => i.Id == 20).Notes.Select(n => n.Id).Should().BeEquivalentTo(new[] { 200, 201 });
            recorder.Statements.Should().HaveCount(6);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// A forbidden adapter operator declared at depth two (inside the outer adapter's <c>Select</c>) must be
    /// refused by the same validation as one at depth one: the nested projection is visited during recognition,
    /// so no nested node can evade the whitelist and no SQL is ever built.
    /// </summary>
    [Fact]
    public void Depth2_ForbiddenAdapterOperators_ShouldBeRefusedBeforeAnySql()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SplitQuery);
        try
        {
            var allNotes = context.From<Note>().Select(n => new { n.Id });

            Action nestedDistinct = () => context.From<Parent>()
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Select(i => new { i.Id, Notes = i.Notes.AsEntityBuilder<Note>().Distinct().ToList() })
                        .ToList(),
                });
            Action nestedGroupBy = () => context.From<Parent>()
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Select(i => new { i.Id, Notes = i.Notes.AsEntityBuilder<Note>().GroupBy(n => n.ItemId).ToList() })
                        .ToList(),
                });
            Action nestedUnion = () => context.From<Parent>()
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Select(i => new
                        {
                            i.Id,
                            Notes = i.Notes.AsEntityBuilder<Note>().Select(n => new { n.Id }).Union(allNotes).ToList(),
                        })
                        .ToList(),
                });
            Action nestedJoin = () => context.From<Parent>()
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Select(i => new
                        {
                            i.Id,
                            Notes = i.Notes.AsEntityBuilder<Note>()
                                .Join(context.From<Note>(), (n, other) => n.Id == other.Id)
                                .ToList(),
                        })
                        .ToList(),
                });

            nestedDistinct.Should().Throw<NotSupportedException>()
                .WithMessage("*applies Distinct(...) inside its adapter chain*");
            nestedGroupBy.Should().Throw<NotSupportedException>()
                .WithMessage("*applies GroupBy(...) inside its adapter chain*");
            nestedUnion.Should().Throw<NotSupportedException>()
                .WithMessage("*applies Union(...) inside its adapter chain*");
            nestedJoin.Should().Throw<NotSupportedException>()
                .WithMessage("*applies Join(...) inside its adapter chain*");
            recorder.Statements.Should().BeEmpty("every depth-two forbidden operator is refused before any SQL");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    private static class OrderDirection
    {
        internal const global::NextORM.Core.OrderDirection Desc = global::NextORM.Core.OrderDirection.Desc;
    }
}
