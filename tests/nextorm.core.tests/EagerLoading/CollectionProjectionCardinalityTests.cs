using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests;

/// <summary>
/// #249 / T353 D3a: the projection-scoped cardinality guard. A projection that asks for a per-parent
/// collection may also project a <em>reference</em> navigation (N:1 or 1:1), at depth one or inside the adapter's
/// child DTO. The carrier materializes such a navigation through the eager-loading reference route, which
/// resolves at most one full related identity per parent from the raw stitched rows: repeated physical rows of
/// one identity collapse to one instance, a second distinct identity throws
/// <see cref="InvalidOperationException"/> naming the navigation, and a parent with no related row stays
/// <see langword="null"/>. The guard runs while the graph materializes, so no projected list is observable when
/// it fails.
/// <para>
/// Runs against a real SQLite file (no container): the two execution paths (split and single) stitch from
/// different row shapes, and the duplicate-row witness needs a view that repeats one physical identity.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public sealed class CollectionProjectionCardinalityTests
{
    public CollectionProjectionCardinalityTests()
    {
        DataContextCache.Clear();
    }

    [SqlTable("t353g_parent")]
    public sealed class Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public List<Item> Items { get; } = [];

        public Contact? Contact { get; set; }
    }

    [SqlTable("t353g_item")]
    public sealed class Item
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("rank")]
        public int Rank { get; set; }

        public Owner? Owner { get; set; }
    }

    [SqlTable("t353g_contact")]
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

    [SqlTable("t353g_owner")]
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

    /// <summary>Which cardinality witness the fixture database has to carry.</summary>
    private enum Witness
    {
        /// <summary>One contact per parent, one owner per item: the valid graph.</summary>
        Valid,

        /// <summary>Parent 1 is matched by contacts 900 and 901: a broken 1:1 for one parent.</summary>
        BrokenContact,

        /// <summary>Parent 1 is matched by the same contact identity twice (a view repeating one row).</summary>
        RepeatedContact,

        /// <summary>Item 10 is matched by owners 500 and 501: a broken 1:1 at depth two.</summary>
        BrokenOwner,
    }

    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
            => Statements.Add(command.CommandText.Replace("\r\n", "\n"));
    }

    private static (SqliteDataContext Ctx, string Path, SqlRecordingInterceptor Recorder) CreateDb(
        EagerLoadMode mode,
        Witness witness = Witness.Valid)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-t353-guard-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();

            // The duplicate-row witness must reach the split loader as two physical rows of one identity, so the
            // contact table is a view that repeats parent 1's single row.
            var contactDdl = witness == Witness.RepeatedContact
                ? "create view t353g_contact (id, parent_id, label) as " +
                  "select 900, 1, 'c1' union all select 900, 1, 'c1';"
                : "create table t353g_contact (id integer primary key, parent_id int not null, label text null);";

            var contactRows = witness switch
            {
                Witness.RepeatedContact => string.Empty,
                Witness.BrokenContact => "insert into t353g_contact (id, parent_id, label) values (900, 1, 'c1'), (901, 1, 'c2');",
                _ => "insert into t353g_contact (id, parent_id, label) values (900, 1, 'c1');",
            };

            var ownerRows = witness == Witness.BrokenOwner
                ? "insert into t353g_owner (id, item_id, label) values (500, 10, 'o10a'), (501, 10, 'o10b'), (510, 11, 'o11');"
                : "insert into t353g_owner (id, item_id, label) values (500, 10, 'o10'), (510, 11, 'o11');";

            cmd.CommandText =
                "create table t353g_parent (id integer primary key, name text null);" +
                "create table t353g_item (id integer primary key, parent_id int not null, rank int not null);" +
                "create table t353g_owner (id integer primary key, item_id int not null, label text null);" +
                contactDdl +
                "insert into t353g_parent (id, name) values (1, 'p1'), (2, 'p2');" +
                "insert into t353g_item (id, parent_id, rank) values (10, 1, 2), (11, 1, 1), (12, 2, 3);" +
                contactRows +
                ownerRows;
            cmd.ExecuteNonQuery();
        }

        var context = new SqliteDataContext($"Data Source={path}", new DataContextBuilder().UseEagerLoadMode(mode));
        _ = context.From<Parent>(cfg => cfg
            .HasMany(p => p.Items, i => i.ParentId)
            .HasOneToOne(p => p.Contact, p => p.Id, c => c.ParentId));
        _ = context.From<Item>(cfg => cfg.HasOneToOne(i => i.Owner, i => i.Id, o => o.ItemId));

        var recorder = new SqlRecordingInterceptor();
        context.AddInterceptor(recorder);
        return (context, path, recorder);
    }

    // ================================================================================================
    // Depth one: a reference navigation projected next to a collection slot.
    // ================================================================================================

    [Fact]
    public void ReferenceSlot_Valid_Split_ShouldProjectTheLoadedEntity()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SplitQuery);
        try
        {
            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, Items = p.Items, Contact = p.Contact })
                .ToList();

            rows.Should().HaveCount(2);
            var first = rows.Single(r => r.Id == 1);
            first.Items.Select(i => i.Id).Should().Equal(10, 11);
            first.Contact.Should().NotBeNull();
            first.Contact!.Id.Should().Be(900);
            first.Contact.Label.Should().Be("c1", "the reference route materialized the row before projection");
            rows.Single(r => r.Id == 2).Contact.Should().BeNull("an absent reference keeps the existing optionality");
            recorder.Statements.Should().HaveCount(3, "parent + items + contact: no statement per parent");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReferenceSlot_Valid_Single_ShouldProjectTheLoadedEntity()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SingleQuery);
        try
        {
            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, Items = p.Items, Contact = p.Contact })
                .ToList();

            var first = rows.Single(r => r.Id == 1);
            first.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { 10, 11 });
            first.Contact!.Id.Should().Be(900);
            rows.Single(r => r.Id == 2).Contact.Should().BeNull();
            recorder.Statements.Should().HaveCount(1, "one denormalized statement carries the whole depth-one graph");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReferenceSlot_BrokenCardinality_Split_ShouldThrowBeforeAnyResultIsObservable()
    {
        var (context, path, _) = CreateDb(EagerLoadMode.SplitQuery, Witness.BrokenContact);
        try
        {
            object? observed = null;
            Action query = () => observed = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, Items = p.Items, Contact = p.Contact })
                .ToList();

            query.Should().Throw<InvalidOperationException>()
                .WithMessage("*Parent.Contact*")
                .WithMessage("*more than one distinct*");
            observed.Should().BeNull("the guard runs while the graph materializes, before any projected row escapes");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReferenceSlot_BrokenCardinality_Single_ShouldThrowBeforeAnyResultIsObservable()
    {
        var (context, path, _) = CreateDb(EagerLoadMode.SingleQuery, Witness.BrokenContact);
        try
        {
            object? observed = null;
            Action query = () => observed = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, Items = p.Items, Contact = p.Contact })
                .ToList();

            query.Should().Throw<InvalidOperationException>()
                .WithMessage("*Parent.Contact*")
                .WithMessage("*more than one distinct*");
            observed.Should().BeNull("the single-query stitcher rejects before the projector runs");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReferenceSlot_RepeatedPhysicalRows_Split_ShouldYieldOneInstanceAndNotThrow()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SplitQuery, Witness.RepeatedContact);
        try
        {
            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, Items = p.Items, Contact = p.Contact })
                .ToList();

            var first = rows.Single(r => r.Id == 1);
            first.Contact.Should().NotBeNull();
            first.Contact!.Id.Should().Be(900);
            first.Contact.Label.Should().Be("c1", "two physical rows of one identity collapse to the first instance");
            first.Items.Select(i => i.Id).Should().Equal(10, 11);
            rows.Single(r => r.Id == 2).Contact.Should().BeNull();
            recorder.Statements.Should().HaveCount(3);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReferenceSlot_RepeatedPhysicalRows_Single_ShouldYieldOneInstanceAndNotThrow()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SingleQuery, Witness.RepeatedContact);
        try
        {
            var rows = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, Items = p.Items, Contact = p.Contact })
                .ToList();

            var first = rows.Single(r => r.Id == 1);
            first.Contact!.Id.Should().Be(900);
            first.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { 10, 11 });
            rows.Single(r => r.Id == 2).Contact.Should().BeNull();
            recorder.Statements.Should().HaveCount(1);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// Control: the reference route's own contract is unchanged outside the carrier — a plain <c>LoadWith</c>
    /// reference still rejects two distinct identities and still tolerates repeated rows of one identity.
    /// </summary>
    [Fact]
    public void OrdinaryLoadWithReference_ShouldKeepItsCardinalityContract()
    {
        var (broken, brokenPath, _) = CreateDb(EagerLoadMode.SplitQuery, Witness.BrokenContact);
        try
        {
            Action load = () => broken.From<Parent>()
                .OrderBy(p => p.Id)
                .LoadWith(p => p.Contact)
                .ToList();

            load.Should().Throw<InvalidOperationException>().WithMessage("*Parent.Contact*");
        }
        finally
        {
            broken.Dispose();
            File.Delete(brokenPath);
        }

        var (repeated, repeatedPath, _) = CreateDb(EagerLoadMode.SplitQuery, Witness.RepeatedContact);
        try
        {
            var parents = repeated.From<Parent>()
                .OrderBy(p => p.Id)
                .LoadWith(p => p.Contact)
                .ToList();

            parents.Single(p => p.Id == 1).Contact!.Id.Should().Be(900);
        }
        finally
        {
            repeated.Dispose();
            File.Delete(repeatedPath);
        }
    }

    // ================================================================================================
    // Depth two: a reference edge inside the adapter's child DTO.
    // ================================================================================================

    [Fact]
    public void NestedReferenceSlot_Depth2_Split_ShouldProjectValuesAndNulls()
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
                        .OrderBy(i => i.Id)
                        .Select(i => new { i.Id, i.Owner })
                        .ToList(),
                })
                .ToList();

            var first = rows.Single(r => r.Id == 1);
            first.Items.Select(i => i.Id).Should().Equal(10, 11);
            first.Items.Single(i => i.Id == 10).Owner!.Label.Should().Be("o10");
            first.Items.Single(i => i.Id == 11).Owner!.Label.Should().Be("o11");

            var second = rows.Single(r => r.Id == 2);
            second.Items.Should().ContainSingle();
            second.Items[0].Owner.Should().BeNull("a missing reference at depth two is null, not an empty entity");

            recorder.Statements.Should().HaveCount(3, "parent + items + one owner query for the child level; never per parent");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void NestedReferenceSlot_Depth2_Single_ShouldProjectValuesAndNulls()
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
                        .OrderBy(i => i.Id)
                        .Select(i => new { i.Id, i.Owner })
                        .ToList(),
                })
                .ToList();

            var first = rows.Single(r => r.Id == 1);
            first.Items.Single(i => i.Id == 10).Owner!.Label.Should().Be("o10");
            first.Items.Single(i => i.Id == 11).Owner!.Label.Should().Be("o11");
            rows.Single(r => r.Id == 2).Items[0].Owner.Should().BeNull();
            recorder.Statements.Should().HaveCount(1, "the inherited single mode flattens the depth-two graph");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void NestedReferenceSlot_Depth2_BrokenCardinality_Split_ShouldThrowBeforeAnyOutput()
    {
        var (context, path, _) = CreateDb(EagerLoadMode.SplitQuery, Witness.BrokenOwner);
        try
        {
            object? observed = null;
            Action query = () => observed = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Select(i => new { i.Id, i.Owner })
                        .ToList(),
                })
                .ToList();

            query.Should().Throw<InvalidOperationException>()
                .WithMessage("*Item.Owner*")
                .WithMessage("*more than one distinct*");
            observed.Should().BeNull("a reference edge inside the depth-two slot graph is validated before projection");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void NestedReferenceSlot_Depth2_BrokenCardinality_Single_ShouldThrowBeforeAnyOutput()
    {
        var (context, path, _) = CreateDb(EagerLoadMode.SingleQuery, Witness.BrokenOwner);
        try
        {
            object? observed = null;
            Action query = () => observed = context.From<Parent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Items = p.Items.AsEntityBuilder<Item>()
                        .Select(i => new { i.Id, i.Owner })
                        .ToList(),
                })
                .ToList();

            query.Should().Throw<InvalidOperationException>()
                .WithMessage("*Item.Owner*")
                .WithMessage("*more than one distinct*");
            observed.Should().BeNull("the single-query stitcher rejects before the depth-two DTO is built");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }
}
