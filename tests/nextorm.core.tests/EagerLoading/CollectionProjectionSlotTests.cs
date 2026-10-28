using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests;

/// <summary>
/// #249 / T353 D2: collection-valued projection slots carried by an entity query. A projection may name a
/// collection member of a mapped navigation directly (<c>Select(p =&gt; new { p.Id, p.Children })</c>) or through
/// an adapter chain (<c>p.Children.AsEntityBuilder&lt;Child&gt;().Where(..).OrderBy(..).Limit(..).Select(..).ToList()</c>).
/// The public shape stays the projected type; the carrier materializes the entity graph with the existing split
/// or single eager-loading path and projects in memory, so the statement count is the eager-loading one (2 for
/// split, 1 for single), never per parent.
/// <para>
/// The navigation property is declared as <see cref="List{T}"/> on purpose: a slot's destination type must be
/// assignable from the navigation's static type inside the projection expression tree, so a
/// <c>List&lt;T&gt;</c>-typed navigation is the widest direct-slot witness this CLR surface permits. <c>T[]</c>
/// cannot be a navigation member at all and is covered through the adapter's <c>ToArray()</c>.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public sealed class CollectionProjectionSlotTests
{
    public CollectionProjectionSlotTests()
    {
        DataContextCache.Clear();
    }

    [SqlTable("t353_slot_parent")]
    public sealed class Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public List<Child> Children { get; } = [];

        public ICollection<Tag> Tags { get; } = new List<Tag>();
    }

    [SqlTable("t353_slot_child")]
    public sealed class Child
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
    }

    [SqlTable("t353_slot_tag")]
    public sealed class Tag
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("t353_slot_parent_tag")]
    public sealed class ParentTag
    {
        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("tag_id")]
        public int TagId { get; set; }
    }

    public sealed class ChildDto
    {
        public int Id { get; set; }
        public int Rank { get; set; }
    }

    /// <summary>One destination member per CLR shape; every one of the seven must be accepted.</summary>
    public sealed class AllShapes
    {
        public int Id { get; set; }
        public List<Child> List { get; set; } = [];
        public Child[] Array { get; set; } = [];
        public IEnumerable<Child> Enumerable { get; set; } = [];
        public ICollection<Child> Collection { get; set; } = [];
        public IList<Child> ListInterface { get; set; } = [];
        public IReadOnlyCollection<Child> ReadOnlyCollection { get; set; } = [];
        public IReadOnlyList<Child> ReadOnlyList { get; set; } = [];
    }

    /// <summary>The shapes a direct slot can declare when the navigation is a <c>List&lt;T&gt;</c>.</summary>
    public sealed class DirectShapes
    {
        public int Id { get; set; }
        public List<Child> List { get; set; } = [];
        public IEnumerable<Child> Enumerable { get; set; } = [];
        public ICollection<Child> Collection { get; set; } = [];
        public IList<Child> ListInterface { get; set; } = [];
        public IReadOnlyCollection<Child> ReadOnlyCollection { get; set; } = [];
        public IReadOnlyList<Child> ReadOnlyList { get; set; } = [];
    }

    public sealed class DirectShapeA
    {
        public int Id { get; set; }
        public IReadOnlyCollection<Child> Items { get; set; } = [];
    }

    public sealed class AdapterShapeB
    {
        public int Id { get; set; }
        public List<ChildDto> Items { get; set; } = [];
    }

    /// <summary>A destination outside the seven supported shapes: <c>object</c> must be refused, never substituted.</summary>
    public sealed class ObjectSlot
    {
        public int Id { get; set; }
        public object? Items { get; set; }
    }

    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
            => Statements.Add(command.CommandText.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Parent 1 owns children 10 (rank 2, active), 11 (rank 1, active) and 12 (rank 3, inactive); parent 2 owns
    /// none. Tag tables exist so the many-to-many refusal is reached with real metadata in place.
    /// </summary>
    private static (SqliteDataContext Ctx, string Path, SqlRecordingInterceptor Recorder) CreateDb(
        EagerLoadMode mode = EagerLoadMode.SplitQuery)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-t353-slot-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table t353_slot_parent (id integer primary key, name text null);" +
                "create table t353_slot_child (id integer primary key, parent_id int not null, rank int not null, active int not null);" +
                "create table t353_slot_tag (id integer primary key);" +
                "create table t353_slot_parent_tag (parent_id int not null, tag_id int not null);" +
                "create table t353_slot_blob (id integer primary key, payload blob null);" +
                "insert into t353_slot_parent (id, name) values (1, 'p1'), (2, 'p2');" +
                "insert into t353_slot_child (id, parent_id, rank, active) values" +
                " (10, 1, 2, 1), (11, 1, 1, 1), (12, 1, 3, 0);" +
                "insert into t353_slot_tag (id) values (100), (101);" +
                "insert into t353_slot_parent_tag (parent_id, tag_id) values (1, 100), (1, 101);" +
                "insert into t353_slot_blob (id, payload) values (1, x'0102');";
            cmd.ExecuteNonQuery();
        }

        var context = new SqliteDataContext(
            $"Data Source={path}",
            new DataContextBuilder().UseEagerLoadMode(mode));
        var recorder = new SqlRecordingInterceptor();
        context.AddInterceptor(recorder);
        return (context, path, recorder);
    }

    private static void RegisterMetadata(SqliteDataContext context)
        => _ = context.From<Parent>(cfg => cfg.HasMany(p => p.Children, c => c.ParentId));

    private static void RegisterManyToManyMetadata(SqliteDataContext context)
        => _ = context.From<Parent>(cfg => cfg
            .HasMany(p => p.Children, c => c.ParentId)
            .HasManyThrough<Tag, ParentTag, int, int>(
                p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.TagId));

    [Fact]
    public void DirectSlot_Split_ShouldProjectThePerParentCollection()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.Children })
                .ToList();

            rows.Select(r => r.Id).Should().Equal(1, 2);
            rows.Single(r => r.Id == 1).Children.Select(c => c.Id).Should().BeEquivalentTo(new[] { 10, 11, 12 });
            rows.Single(r => r.Id == 2).Children.Should().BeEmpty("a parent without children owns an empty collection, not null");
            recorder.Statements.Should().HaveCount(2, "split loading runs the parent query and one child query");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void DirectSlot_Single_ShouldProjectFromOneStatement()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SingleQuery);
        try
        {
            RegisterMetadata(context);

            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.Children })
                .ToList();

            rows.Single(r => r.Id == 1).Children.Select(c => c.Id).Should().BeEquivalentTo(new[] { 10, 11, 12 },
                "the single-query mode is inherited from the root and the graph is stitched from one result set");
            rows.Single(r => r.Id == 2).Children.Should().BeEmpty();
            recorder.Statements.Should().HaveCount(1);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void DirectSlot_ShouldAcceptEveryAssignableShape()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            var rows = context.From<Parent>()
                .Where(p => p.Id == 1)
                .Select(p => new DirectShapes
                {
                    Id = p.Id,
                    List = p.Children,
                    Enumerable = p.Children,
                    Collection = p.Children,
                    ListInterface = p.Children,
                    ReadOnlyCollection = p.Children,
                    ReadOnlyList = p.Children,
                })
                .ToList();

            var row = rows.Should().ContainSingle().Subject;
            row.Id.Should().Be(1);
            row.List.Select(c => c.Id).Should().Equal(10, 11, 12);
            row.Enumerable.Select(c => c.Id).Should().Equal(10, 11, 12);
            row.Collection.Select(c => c.Id).Should().Equal(10, 11, 12);
            row.ListInterface.Select(c => c.Id).Should().Equal(10, 11, 12);
            row.ReadOnlyCollection.Select(c => c.Id).Should().Equal(10, 11, 12);
            row.ReadOnlyList.Select(c => c.Id).Should().Equal(10, 11, 12);
            recorder.Statements.Should().HaveCount(2, "one eager-load declaration serves all six conversions");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void AdapterSlot_ShouldAcceptAllSevenShapes()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            var rows = context.From<Parent>()
                .Where(p => p.Id == 1)
                .Select(p => new AllShapes
                {
                    Id = p.Id,
                    List = p.Children.AsEntityBuilder<Child>().ToList(),
                    Array = p.Children.AsEntityBuilder<Child>().ToArray(),
                    Enumerable = p.Children.AsEntityBuilder<Child>().ToList(),
                    Collection = p.Children.AsEntityBuilder<Child>().ToList(),
                    ListInterface = p.Children.AsEntityBuilder<Child>().ToList(),
                    ReadOnlyCollection = p.Children.AsEntityBuilder<Child>().ToList(),
                    ReadOnlyList = p.Children.AsEntityBuilder<Child>().ToList(),
                })
                .ToList();

            var row = rows.Should().ContainSingle().Subject;
            row.Id.Should().Be(1);
            row.List.Select(c => c.Id).Should().Equal(10, 11, 12);
            row.Array.Should().BeOfType<Child[]>();
            row.Array.Select(c => c.Id).Should().Equal(10, 11, 12);
            row.Enumerable.Select(c => c.Id).Should().Equal(10, 11, 12);
            row.Collection.Select(c => c.Id).Should().Equal(10, 11, 12);
            row.ListInterface.Select(c => c.Id).Should().Equal(10, 11, 12);
            row.ReadOnlyCollection.Select(c => c.Id).Should().Equal(10, 11, 12);
            row.ReadOnlyList.Select(c => c.Id).Should().Equal(10, 11, 12);
            recorder.Statements.Should().HaveCount(2, "one eager-load declaration serves all seven conversions");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void AdapterSlot_Split_ShouldApplyWhereOrderAndLimitPerParent()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Active = p.Children.AsEntityBuilder<Child>()
                        .Where(c => c.Active)
                        .OrderBy(c => c.Rank)
                        .ThenBy(c => c.Id, OrderDirection.Desc)
                        .Limit(2)
                        .ToList(),
                })
                .ToList();

            rows.Single(r => r.Id == 1).Active.Select(c => c.Id).Should().Equal(new[] { 11, 10 },
                "Where/OrderBy/ThenBy/Limit apply per parent, in memory, over the materialized children");
            rows.Single(r => r.Id == 2).Active.Should().BeEmpty();
            recorder.Statements.Should().HaveCount(2);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void AdapterSlot_Single_WithProjection_ShouldProjectTheNestedShape()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SingleQuery);
        try
        {
            RegisterMetadata(context);

            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Items = p.Children.AsEntityBuilder<Child>()
                        .Where(c => c.Active)
                        .OrderBy(c => c.Id)
                        .Select(c => new ChildDto { Id = c.Id, Rank = c.Rank })
                        .ToList(),
                })
                .ToList();

            rows.Single(r => r.Id == 1).Items.Select(c => c.Id).Should().Equal(10, 11);
            rows.Single(r => r.Id == 1).Items.Select(c => c.Rank).Should().Equal(2, 1);
            rows.Single(r => r.Id == 2).Items.Should().BeEmpty();
            recorder.Statements.Should().HaveCount(1);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void CapturedValues_ShouldRebindBetweenTwoExecutionsOfOneCommand()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            var page = 1;
            var command = context.From<Parent>()
                .Where(p => p.Id == 1)
                .Select(p => new
                {
                    p.Id,
                    Page = p.Children.AsEntityBuilder<Child>().OrderBy(c => c.Id).Limit(page).ToList(),
                });

            command.ToList().Single().Page.Select(c => c.Id).Should().Equal(new[] { 10 },
                "the captured value is read at execution and never baked into the plan key or the delegate");
            recorder.Statements.Should().HaveCount(2);

            page = 2;

            command.ToList().Single().Page.Select(c => c.Id).Should().Equal(new[] { 10, 11 },
                "the same command re-executes with the new captured value");
            recorder.Statements.Should().HaveCount(4);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void TwoDifferentSlotShapes_ShouldBothBeCorrectOverOneDataContext()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            // Shape A: a direct slot whose destination is IReadOnlyCollection<Child>.
            var first = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new DirectShapeA { Id = p.Id, Items = p.Children })
                .ToList();

            first.Select(r => r.Id).Should().Equal(1, 2);
            first.Single(r => r.Id == 1).Items.Select(c => c.Id).Should().Equal(10, 11, 12);
            first.Single(r => r.Id == 2).Items.Should().BeEmpty();
            recorder.Statements.Should().HaveCount(2);

            // Shape B: an adapter slot with a different element type and a different operation set.
            recorder.Statements.Clear();
            var second = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new AdapterShapeB
                {
                    Id = p.Id,
                    Items = p.Children.AsEntityBuilder<Child>()
                        .Where(c => c.Active)
                        .OrderByDescending(c => c.Id)
                        .Select(c => new ChildDto { Id = c.Id, Rank = c.Rank })
                        .ToList(),
                })
                .ToList();

            second.Single(r => r.Id == 1).Items.Select(c => c.Id).Should().Equal(11, 10);
            second.Single(r => r.Id == 2).Items.Should().BeEmpty();
            recorder.Statements.Should().HaveCount(2);

            // Shape A again: a command prepared for one slot shape must not answer for the other.
            recorder.Statements.Clear();
            var third = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new DirectShapeA { Id = p.Id, Items = p.Children })
                .ToList();

            third.Single(r => r.Id == 1).Items.Select(c => c.Id).Should().Equal(10, 11, 12);
            third.Single(r => r.Id == 2).Items.Should().BeEmpty();
            recorder.Statements.Should().HaveCount(2);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectedShape_ShouldProduceNoSql_AndNotPoisonTheNextProjection()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            Action unsupported = () => context.From<Parent>()
                .Select(p => new { p.Id, Tags = p.Children.ToHashSet() });

            unsupported.Should().Throw<NotSupportedException>()
                .WithMessage("*not a supported collection slot*");
            recorder.Statements.Should().BeEmpty("a rejected slot shape is refused before any SQL is built");

            var rows = context.From<Parent>()
                .Where(p => p.Id == 1)
                .Select(p => new { p.Id, p.Children })
                .ToList();

            rows.Single().Children.Should().HaveCount(3, "a rejected shape never installs a plan a valid projection could hit");
            recorder.Statements.Should().HaveCount(2);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void UnsupportedTerminal_ShouldBeRefusedBeforeSql()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            var command = context.From<Parent>()
                .Select(p => new { p.Id, p.Children });

            Action unsupported = () => command.ToEnumerable();

            unsupported.Should().Throw<NotSupportedException>()
                .WithMessage("*supports only ToList() and ToArray()*");
            recorder.Statements.Should().BeEmpty();
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void GroupingComposition_ShouldBeRefusedBeforeSql()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            Action unsupported = () => context.From<Parent>()
                .GroupBy(p => p.Id)
                .Select(p => new { p.Id, p.Children });

            unsupported.Should().Throw<NotSupportedException>()
                .WithMessage("*cannot be combined with grouping*");
            recorder.Statements.Should().BeEmpty();
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void AdapterGrouping_ShouldBeRefusedBeforeSql()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            Action unsupported = () => context.From<Parent>()
                .Select(p => new { p.Id, Grouped = p.Children.AsEntityBuilder<Child>().GroupBy(c => c.ParentId).ToList() });

            unsupported.Should().Throw<NotSupportedException>()
                .WithMessage("*applies GroupBy(...) inside its adapter chain*");
            recorder.Statements.Should().BeEmpty();
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ManyToManyWithLimit_ShouldBeRefusedBeforeSql()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterManyToManyMetadata(context);

            Action unsupported = () => context.From<Parent>()
                .Select(p => new { p.Id, Page = p.Tags.AsEntityBuilder<Tag>().Limit(1).ToList() });

            unsupported.Should().Throw<NotSupportedException>()
                .WithMessage("*M:N + Limit*");
            recorder.Statements.Should().BeEmpty();
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void UnsupportedDestinationShape_Direct_ShouldBeRefusedBeforeSql()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            Action unsupported = () => context.From<Parent>()
                .Select(p => new ObjectSlot { Id = p.Id, Items = p.Children });

            unsupported.Should().Throw<NotSupportedException>()
                .WithMessage("*only List<T>, T[], IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T>*");
            recorder.Statements.Should().BeEmpty();
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void UnsupportedDestinationShape_Adapter_ShouldBeRefusedBeforeSql()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            Action unsupported = () => context.From<Parent>()
                .Select(p => new ObjectSlot { Id = p.Id, Items = p.Children.AsEntityBuilder<Child>().ToList() });

            unsupported.Should().Throw<NotSupportedException>()
                .WithMessage("*only List<T>, T[], IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T>*");
            recorder.Statements.Should().BeEmpty();
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SetOperationInsideAdapter_ShouldBeRefusedBeforeSql()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            RegisterMetadata(context);

            Action unsupported = () => context.From<Parent>()
                .Select(p => new
                {
                    p.Id,
                    Items = p.Children.AsEntityBuilder<Child>()
                        .Select(c => new ChildDto { Id = c.Id, Rank = c.Rank })
                        .Distinct()
                        .ToList(),
                });

            unsupported.Should().Throw<NotSupportedException>()
                .WithMessage("*applies Distinct(...) inside its adapter chain*");
            recorder.Statements.Should().BeEmpty();
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void InMemoryProvider_ShouldBeRefused()
    {
        using var context = new InMemoryDataContext();
        _ = context.From<Parent>(cfg => cfg.HasMany(p => p.Children, c => c.ParentId));

        Action unsupported = () => context.From<Parent>()
            .Select(p => new { p.Id, p.Children });

        unsupported.Should().Throw<NotSupportedException>()
            .WithMessage("*requires a SQL provider*");
    }

    [Fact]
    public void ScalarCollectionColumn_ShouldNotBeTakenForASlot()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            var rows = context.From<BlobRow>()
                .Select(r => new { r.Id, r.Payload })
                .ToList();

            rows.Should().ContainSingle("a byte[] column is a collection-valued CLR type but not a navigation");
            (rows[0].Payload ?? []).Should().Equal(new byte[] { 1, 2 });
            recorder.Statements.Should().HaveCount(1);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [SqlTable("t353_slot_blob")]
    public sealed class BlobRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("payload")]
        public byte[]? Payload { get; set; }
    }

    private static class OrderDirection
    {
        internal const global::NextORM.Core.OrderDirection Desc = global::NextORM.Core.OrderDirection.Desc;
        internal const global::NextORM.Core.OrderDirection Asc = global::NextORM.Core.OrderDirection.Asc;
    }
}
