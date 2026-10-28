using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// #249 / T353 D4b: the live half of the collection-valued projection slot matrix. D2/D3 proved the
/// recognizer, binder, recursion and cardinality guard against a real SQLite file; D4a proved the SQL
/// shape on every dialect without a connection. This file projects the same slots through the shared
/// <c>eager_*</c> tables of the provider harness, so every database a provider actually runs executes
/// the matrix and returns values — split and single, depth 1/2/3, flat and nested many-to-many,
/// reference cardinality, composite junctions, captured rebinding and the named pre-SQL refusals.
/// <para>
/// The bodies are <c>internal static</c> and take the already-configured context, so ClickHouse (which
/// has no <see cref="CommonTestSuite"/> subclass) and MariaDB (which has no <c>ITestProvider</c>) can
/// re-pin the same live assertions instead of restating them. SQLite, PostgreSQL, SQL Server and MySQL
/// reach them through the thin <c>[Fact]</c> wrappers at the end of this file.
/// </para>
/// <para>
/// Row layout (ids are fixed so the assertions read as acceptance rows; every other suite uses other
/// ranges, and every query filters on these roots):
/// <c>9701</c> rich (items 9710 "beta" → notes 9720,9721 + tips; 9711 "alpha" → note 9722;
/// 9712 "skip" filtered at level two; tags 9770,9771), <c>9702</c> no items, <c>9703</c> item 9713 with
/// no notes, <c>9704</c> no profile, <c>9705</c> profile 9724 + tag 9772, <c>9706</c> two profiles
/// (broken 1:1), <c>9707</c> profile 9727 + two items (repeated physical rows of one identity), and
/// <c>9708</c> the many-to-many paging witness (tag 9770 linked twice, tag 9771 once, and a dangling link to
/// the missing tag 9799 that must consume no page slot).
/// </para>
/// </summary>
public abstract partial class CommonTestSuite
{
    internal const int LiveRich = 9701;
    internal const int LiveEmpty = 9702;
    internal const int LiveNoNotes = 9703;
    internal const int LiveNoProfile = 9704;
    internal const int LiveProfile = 9705;
    internal const int LiveBrokenProfile = 9706;
    internal const int LiveRepeatedProfile = 9707;
    internal const int LivePaged = 9708;

    private static readonly object LiveSeedGate = new();

    /// <summary>
    /// Registers the projection relationships and seeds the fixed live rows once per provider database.
    /// Relationship metadata is process-wide; the rows are not, so the guard is a real query on the
    /// target database and a second provider seeds its own copy.
    /// </summary>
    internal static void EnsureCollectionProjectionLiveSeeded(TestDataRepository sut)
    {
        var ctx = sut.DataProvider;

        RegisterCollectionProjectionLive(ctx);

        lock (LiveSeedGate)
        {
            if (ctx.From<CollectionProjectionLiveParent>().Where(p => p.Id == LiveRich).Any())
                return;

            ctx.CreateInsertBuilder<CollectionProjectionLiveParent>().Values([
                new CollectionProjectionLiveParent { Id = LiveRich, Name = "r1" },
                new CollectionProjectionLiveParent { Id = LiveEmpty, Name = "r2" },
                new CollectionProjectionLiveParent { Id = LiveNoNotes, Name = "r3" },
                new CollectionProjectionLiveParent { Id = LiveNoProfile, Name = "r4" },
                new CollectionProjectionLiveParent { Id = LiveProfile, Name = "r5" },
                new CollectionProjectionLiveParent { Id = LiveBrokenProfile, Name = "r6" },
                new CollectionProjectionLiveParent { Id = LiveRepeatedProfile, Name = "r7" },
                new CollectionProjectionLiveParent { Id = LivePaged, Name = "r8" },
            ]).Insert();

            ctx.CreateInsertBuilder<CollectionProjectionLiveItem>().Values([
                new CollectionProjectionLiveItem { Id = 9710, ParentId = LiveRich, Name = "beta" },
                new CollectionProjectionLiveItem { Id = 9711, ParentId = LiveRich, Name = "alpha" },
                new CollectionProjectionLiveItem { Id = 9712, ParentId = LiveRich, Name = "skip" },
                new CollectionProjectionLiveItem { Id = 9713, ParentId = LiveNoNotes, Name = "lonely" },
                new CollectionProjectionLiveItem { Id = 9717, ParentId = LiveRepeatedProfile, Name = "r7-a" },
                new CollectionProjectionLiveItem { Id = 9718, ParentId = LiveRepeatedProfile, Name = "r7-b" },
            ]).Insert();

            ctx.CreateInsertBuilder<CollectionProjectionLiveNote>().Values([
                new CollectionProjectionLiveNote { Id = 9720, ItemId = 9710, Text = "n-beta-2" },
                new CollectionProjectionLiveNote { Id = 9721, ItemId = 9710, Text = "n-beta-1" },
                new CollectionProjectionLiveNote { Id = 9722, ItemId = 9711, Text = "n-alpha" },
                new CollectionProjectionLiveNote { Id = 9723, ItemId = 9712, Text = "n-skip" },
            ]).Insert();

            ctx.CreateInsertBuilder<CollectionProjectionLiveProfile>().Values([
                new CollectionProjectionLiveProfile { Id = 9724, OwnerKey = LiveProfile, Label = "profile-5" },
                new CollectionProjectionLiveProfile { Id = 9725, OwnerKey = LiveBrokenProfile, Label = "profile-6a" },
                new CollectionProjectionLiveProfile { Id = 9726, OwnerKey = LiveBrokenProfile, Label = "profile-6b" },
                new CollectionProjectionLiveProfile { Id = 9727, OwnerKey = LiveRepeatedProfile, Label = "profile-7" },
            ]).Insert();

            ctx.CreateInsertBuilder<CollectionProjectionLiveTag>().Values([
                new CollectionProjectionLiveTag { Id = 9770, Name = "t-a" },
                new CollectionProjectionLiveTag { Id = 9771, Name = "t-b" },
                new CollectionProjectionLiveTag { Id = 9772, Name = "t-c" },
            ]).Insert();

            ctx.CreateInsertBuilder<CollectionProjectionLiveTip>().Values([
                new CollectionProjectionLiveTip { Id = 9730, NoteId = 9720, Value = 9780 },
                new CollectionProjectionLiveTip { Id = 9731, NoteId = 9720, Value = 9781 },
                new CollectionProjectionLiveTip { Id = 9732, NoteId = 9721, Value = 9782 },
            ]).Insert();

            ctx.CreateInsertBuilder<CollectionProjectionLiveLink>().Values([
                // Flat many-to-many: the rich root owns both tags; the profiled root owns the third.
                new CollectionProjectionLiveLink { Id = 9740, ParentId = LiveRich, ChildId = 9770 },
                new CollectionProjectionLiveLink { Id = 9741, ParentId = LiveRich, ChildId = 9771 },
                new CollectionProjectionLiveLink { Id = 9747, ParentId = LiveProfile, ChildId = 9772 },
                // Nested many-to-many: the junction's parent side is the item id, not the root id.
                new CollectionProjectionLiveLink { Id = 9743, ParentId = 9710, ChildId = 9770 },
                new CollectionProjectionLiveLink { Id = 9744, ParentId = 9710, ChildId = 9771 },
                new CollectionProjectionLiveLink { Id = 9745, ParentId = 9711, ChildId = 9771 },
                new CollectionProjectionLiveLink { Id = 9746, ParentId = 9712, ChildId = 9772 },
                // Paging witness: a repeated occurrence of 9770, one more tag, and a dangling link whose
                // child row (9799) does not exist — the last must never occupy a page slot.
                new CollectionProjectionLiveLink { Id = 9750, ParentId = LivePaged, ChildId = 9770 },
                new CollectionProjectionLiveLink { Id = 9751, ParentId = LivePaged, ChildId = 9770 },
                new CollectionProjectionLiveLink { Id = 9752, ParentId = LivePaged, ChildId = 9771 },
                new CollectionProjectionLiveLink { Id = 9753, ParentId = LivePaged, ChildId = 9799 },
            ]).Insert();
        }
    }

    /// <summary>Declares the projection graph on one context. Registration is process-wide and idempotent.</summary>
    internal static void RegisterCollectionProjectionLive(IDataContext ctx)
    {
        _ = ctx.From<CollectionProjectionLiveItem>(cfg => cfg
            .HasMany(i => i.Notes, n => n.ItemId)
            .HasManyThrough<CollectionProjectionLiveTag, CollectionProjectionLiveLink, int, int>(
                i => i.Tags, i => i.Id, l => l.ParentId, t => t.Id, l => l.ChildId));
        _ = ctx.From<CollectionProjectionLiveNote>(cfg => cfg
            .HasMany(n => n.Tips, t => t.NoteId));
        _ = ctx.From<CollectionProjectionLiveParent>(cfg => cfg
            .HasMany(p => p.Items, i => i.ParentId)
            .HasManyThrough<CollectionProjectionLiveTag, CollectionProjectionLiveLink, int, int>(
                p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.ChildId)
            .HasOneToOne(p => p.Profile, p => p.Id, pr => pr.OwnerKey));
    }

    private static JoinIntoQueryCountingInterceptor CollectionProjectionLiveCounter(IDataContext ctx)
    {
        var counter = new JoinIntoQueryCountingInterceptor();
        ((DataContext)ctx).AddInterceptor(counter);
        return counter;
    }

    private static EntityBuilder<CollectionProjectionLiveParent> LiveParents(IDataContext ctx)
        => ctx.From<CollectionProjectionLiveParent>();

    // ================================================================================================
    // Depth 1 — direct and adapter slots, split and single, counted statements.
    // ================================================================================================

    internal static void Depth1DirectSplit(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveRich || p.Id == LiveEmpty)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Items = p.Items })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(LiveRich, LiveEmpty);
        rows[0].Items.Should().NotBeNull().And.HaveCount(3);
        rows[0].Items.Select(i => i.Id).Should().BeEquivalentTo([9710, 9711, 9712]);
        rows[0].Items.Select(i => i.Name).Should().BeEquivalentTo(["beta", "alpha", "skip"]);
        rows[1].Items.Should().NotBeNull().And.BeEmpty("an empty collection slot is empty, never null");
        counter.Executing.Should().Be(2, "split: one parent statement plus one child statement, never per parent");
    }

    internal static void Depth1DirectSingle(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveRich || p.Id == LiveEmpty)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Items = p.Items })
            .ToList();

        rows[0].Items.Select(i => i.Id).Should().BeEquivalentTo([9710, 9711, 9712]);
        rows[1].Items.Should().NotBeNull().And.BeEmpty();
        counter.Executing.Should().Be(1, "single: the inherited mode flattens the slot in one statement");
    }

    internal static void Depth1AdapterSplit(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveRich || p.Id == LiveNoNotes)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>()
                    .Where(i => i.Name != "skip")
                    .OrderByDescending(i => i.Id)
                    .Limit(1)
                    .Select(i => new { i.Id, i.Name })
                    .ToList(),
            })
            .ToList();

        rows[0].Items.Should().HaveCount(1, "the page is per parent, applied in memory over the materialized children");
        rows[0].Items[0].Id.Should().Be(9711);
        rows[0].Items[0].Name.Should().Be("alpha", "'skip' is filtered out at the item level");
        rows[1].Items.Should().ContainSingle().Which.Id.Should().Be(9713);
        counter.Executing.Should().Be(2);
    }

    internal static void Depth1AdapterShapes(IDataContext ctx)
    {
        // Live witness for two of the seven CLR shapes: an array through ToArray() and an
        // IReadOnlyCollection destination written directly from the navigation.
        var arrays = LiveParents(ctx)
            .Where(p => p.Id == LiveRich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>().OrderBy(i => i.Id).ToArray(),
            })
            .ToList();

        arrays.Single().Items.Should().BeOfType<CollectionProjectionLiveItem[]>()
            .Which.Select(i => i.Id).Should().Equal(9710, 9711, 9712);

        var readOnly = LiveParents(ctx)
            .Where(p => p.Id == LiveRich)
            .Select(p => new CollectionProjectionLiveShapes { Items = p.Items })
            .ToList();

        readOnly.Single().Items.Should().BeAssignableTo<IReadOnlyCollection<CollectionProjectionLiveItem>>()
            .Which.Select(i => i.Id).Should().BeEquivalentTo([9710, 9711, 9712]);
    }

    // ================================================================================================
    // Depth 2 — direct nested slot and adapter nested slot with per-level filter/order/page.
    // ================================================================================================

    internal static void Depth2DirectSplit(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveRich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>()
                    .Where(i => i.Name != "skip")
                    .OrderBy(i => i.Name)
                    .Select(i => new { i.Id, i.Name, Notes = i.Notes })
                    .ToList(),
            })
            .ToList();

        var items = rows.Single().Items;
        items.Select(i => i.Name).Should().Equal(["alpha", "beta"],
            "the level-two Where dropped 'skip' and the order is per parent");
        items.Single(i => i.Id == 9710).Notes.Select(n => n.Id).Should().BeEquivalentTo([9720, 9721]);
        items.Single(i => i.Id == 9711).Notes.Select(n => n.Id).Should().Equal(9722);
        counter.Executing.Should().Be(3, "split: parent + items + notes, one statement per level");
    }

    internal static void Depth2AdapterSingle(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var page = 1;
        var command = LiveParents(ctx)
            .Where(p => p.Id == LiveRich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>()
                    .Where(i => i.Name != "skip")
                    .OrderBy(i => i.Id)
                    .Select(i => new
                    {
                        i.Id,
                        Notes = i.Notes.AsEntityBuilder<CollectionProjectionLiveNote>()
                            .OrderBy(n => n.Id)
                            .Limit(page)
                            .Select(n => new { n.Id, n.Text })
                            .ToList(),
                    })
                    .ToList(),
            });

        var firstRun = command.ToList();
        var items = firstRun.Single().Items;
        items.Select(i => i.Id).Should().Equal(9710, 9711);
        items.Single(i => i.Id == 9710).Notes.Should().ContainSingle("Limit(1) pages per item");
        items.Single(i => i.Id == 9710).Notes[0].Id.Should().Be(9720);
        items.Single(i => i.Id == 9710).Notes[0].Text.Should().Be("n-beta-2");
        items.Single(i => i.Id == 9711).Notes[0].Id.Should().Be(9722);
        counter.Executing.Should().Be(1, "single: the whole depth-two graph is one statement");

        page = 2;

        var secondRun = command.ToList();
        secondRun.Single().Items.Single(i => i.Id == 9710).Notes.Select(n => n.Id).Should().Equal([9720, 9721],
            "the captured page is re-read on every execution and never leaks");
        counter.Executing.Should().Be(2);
    }

    internal static void Depth3Split(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveRich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>()
                    .Where(i => i.Name != "skip")
                    .OrderBy(i => i.Id)
                    .Select(i => new
                    {
                        i.Id,
                        Notes = i.Notes.AsEntityBuilder<CollectionProjectionLiveNote>()
                            .OrderBy(n => n.Id)
                            .Select(n => new
                            {
                                n.Id,
                                Tips = n.Tips.AsEntityBuilder<CollectionProjectionLiveTip>()
                                    .OrderBy(t => t.Id)
                                    .Select(t => new { t.Id, t.Value })
                                    .ToList(),
                            })
                            .ToList(),
                    })
                    .ToList(),
            })
            .ToList();

        var items = rows.Single().Items;
        items.Single(i => i.Id == 9710).Notes.Select(n => n.Id).Should().Equal(9720, 9721);
        var notes = items.Single(i => i.Id == 9710).Notes;
        notes.Single(n => n.Id == 9720).Tips.Select(t => t.Id).Should().Equal(9730, 9731);
        notes.Single(n => n.Id == 9720).Tips.Select(t => t.Value).Should().Equal(9780, 9781);
        notes.Single(n => n.Id == 9721).Tips.Select(t => t.Id).Should().Equal(9732);
        items.Single(i => i.Id == 9711).Notes.Single().Tips.Should().NotBeNull().And.BeEmpty();
        counter.Executing.Should().Be(4, "split: one statement per level of the depth-three graph");
    }

    internal static void Depth3Single(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveRich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>()
                    .Where(i => i.Name != "skip")
                    .OrderBy(i => i.Id)
                    .Select(i => new
                    {
                        i.Id,
                        Notes = i.Notes.AsEntityBuilder<CollectionProjectionLiveNote>()
                            .OrderBy(n => n.Id)
                            .Select(n => new
                            {
                                n.Id,
                                Tips = n.Tips,
                            })
                            .ToList(),
                    })
                    .ToList(),
            })
            .ToList();

        var notes = rows.Single().Items.Single(i => i.Id == 9710).Notes;
        // The innermost slot is a direct navigation with no declared OrderBy, so its row order belongs to
        // the provider (MySQL returns 9731, 9730; SQLite/PostgreSQL happen to return 9730, 9731). Only the
        // set is contracted here — Depth3Split asserts the ordered adapter variant.
        notes.Single(n => n.Id == 9720).Tips.Select(t => t.Id).Should().BeEquivalentTo([9730, 9731]);
        notes.Single(n => n.Id == 9721).Tips.Select(t => t.Id).Should().Equal(9732);
        rows.Single().Items.Single(i => i.Id == 9711).Notes.Single().Tips.Should().NotBeNull().And.BeEmpty();
        counter.Executing.Should().Be(1, "single inherits through all three levels");
    }

    // ================================================================================================
    // Many-to-many — flat, nested and the C8 nested-single witness.
    // ================================================================================================

    internal static void FlatManyToManyDirectSplit(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveRich || p.Id == LiveProfile || p.Id == LiveEmpty)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Tags = p.Tags })
            .ToList();

        rows[0].Tags.Select(t => t.Id).Should().BeEquivalentTo([9770, 9771], "both junction rows belong to the rich root");
        rows[0].Tags.Select(t => t.Name).Should().BeEquivalentTo(["t-a", "t-b"]);
        rows.Single(r => r.Id == LiveEmpty).Tags.Should().NotBeNull().And.BeEmpty();
        rows.Single(r => r.Id == LiveProfile).Tags.Select(t => t.Id).Should().Equal(9772);
        counter.Executing.Should().Be(3, "split many-to-many: parent + junction + tag statements");
    }

    internal static void FlatManyToManyAdapterSingle(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveRich)
            .Select(p => new
            {
                p.Id,
                Tags = p.Tags.AsEntityBuilder<CollectionProjectionLiveTag>()
                    .OrderByDescending(t => t.Id)
                    .Select(t => new { t.Id, t.Name })
                    .ToList(),
            })
            .ToList();

        rows.Single().Tags.Select(t => t.Id).Should().Equal([9771, 9770]);
        rows.Single().Tags.Select(t => t.Name).Should().Equal(["t-b", "t-a"]);
        counter.Executing.Should().Be(1, "single many-to-many: one statement for the whole graph");
    }

    internal static void NestedManyToManySplit(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveRich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>()
                    .Where(i => i.Name != "skip")
                    .OrderBy(i => i.Id)
                    .Select(i => new { i.Id, Tags = i.Tags })
                    .ToList(),
            })
            .ToList();

        var items = rows.Single().Items;
        items.Single(i => i.Id == 9710).Tags.Select(t => t.Id).Should().BeEquivalentTo([9770, 9771]);
        items.Single(i => i.Id == 9711).Tags.Select(t => t.Id).Should().Equal(9771);
        counter.Executing.Should().Be(4, "split nested many-to-many: parent + items + junction + tags");
    }

    /// <summary>
    /// C8's live witness: the depth-two many-to-many slot in single-query mode must return the real rows
    /// in one statement. The pre-D1 defect was an empty success — no exception, no rows.
    /// </summary>
    internal static void NestedManyToManySingle(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveRich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>()
                    .Where(i => i.Name != "skip")
                    .OrderBy(i => i.Id)
                    .Select(i => new
                    {
                        i.Id,
                        Tags = i.Tags.AsEntityBuilder<CollectionProjectionLiveTag>().OrderBy(t => t.Id).ToList(),
                    })
                    .ToList(),
            })
            .ToList();

        var items = rows.Single().Items;
        items.Should().HaveCount(2);
        items.Single(i => i.Id == 9710).Tags.Select(t => t.Id).Should().Equal([9770, 9771],
            "the nested many-to-many single slot is never an empty success");
        items.Single(i => i.Id == 9711).Tags.Select(t => t.Id).Should().Equal(9771);
        counter.Executing.Should().Be(1, "the stride fix keeps the whole nested graph in one statement");
    }

    // ================================================================================================
    // Many-to-many per-parent paging — the page counts surviving junction occurrences.
    // ================================================================================================

    internal static void ManyToManyWithLimit_Split(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveEmpty || p.Id == LiveProfile || p.Id == LivePaged)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Tags = p.Tags.AsEntityBuilder<CollectionProjectionLiveTag>().OrderBy(t => t.Id).Limit(2).ToList(),
            })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(LiveEmpty, LiveProfile, LivePaged);
        rows[0].Tags.Should().NotBeNull().And.BeEmpty("a parent with no surviving occurrence keeps an empty page");
        rows[1].Tags.Select(t => t.Id).Should().Equal([9772], "a parent with fewer occurrences than the page keeps them all");
        rows[2].Tags.Should().NotBeNull();
        rows[2].Tags.Should().HaveCount(2, "the page splits the parent's surviving occurrences");
        rows[2].Tags.Select(t => t.Id).Should().Equal([9770, 9770],
            "a repeated junction row occupies its own slot while the dangling link consumes none");
        counter.Executing.Should().Be(3, "split many-to-many: parent + junction + tag statements");
    }

    internal static void ManyToManyWithLimit_Single(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveEmpty || p.Id == LiveProfile || p.Id == LivePaged)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Tags = p.Tags.AsEntityBuilder<CollectionProjectionLiveTag>().OrderBy(t => t.Id).Limit(2).ToList(),
            })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(LiveEmpty, LiveProfile, LivePaged);
        rows[0].Tags.Should().NotBeNull().And.BeEmpty();
        rows[1].Tags.Select(t => t.Id).Should().Equal([9772]);
        rows[2].Tags.Should().NotBeNull();
        rows[2].Tags.Should().HaveCount(2, "single mode pages the same surviving occurrences");
        rows[2].Tags.Select(t => t.Id).Should().Equal([9770, 9770]);
        counter.Executing.Should().Be(1, "single many-to-many: one statement for the whole graph");
    }

    internal static void LoadWithManyToManyPage_Split(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var parents = LiveParents(ctx)
            .Where(p => p.Id == LiveEmpty || p.Id == LiveProfile || p.Id == LivePaged)
            .LoadWith(p => p.Tags, t => t.From<CollectionProjectionLiveTag>().OrderBy(x => x.Id).Offset(1).Limit(2))
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(LiveEmpty, LiveProfile, LivePaged);
        parents[0].Tags.Should().BeEmpty();
        parents[1].Tags.Should().BeEmpty("the only surviving occurrence of this parent is skipped by the offset");
        parents[2].Tags.Select(t => t.Id).Should().Equal([9770, 9771],
            "the page crosses the occurrence boundary: one duplicate is skipped and the next tag is kept");
        counter.Executing.Should().Be(3, "split many-to-many: parent + junction + tag statements");
    }

    internal static void LoadWithManyToManyPage_Single(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var parents = LiveParents(ctx)
            .Where(p => p.Id == LiveEmpty || p.Id == LiveProfile || p.Id == LivePaged)
            .LoadWith(p => p.Tags, t => t.From<CollectionProjectionLiveTag>().OrderBy(x => x.Id).Offset(1).Limit(2))
            .OrderBy(p => p.Id)
            .ToList();

        parents.Select(p => p.Id).Should().Equal(LiveEmpty, LiveProfile, LivePaged);
        parents[0].Tags.Should().BeEmpty();
        parents[1].Tags.Should().BeEmpty();
        parents[2].Tags.Select(t => t.Id).Should().Equal([9770, 9771]);
        counter.Executing.Should().Be(1, "single many-to-many: one statement for the whole graph");
    }

    internal static void NestedManyToManyWithLimit_Split(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveRich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>()
                    .Where(i => i.Name != "skip")
                    .OrderBy(i => i.Id)
                    .Select(i => new
                    {
                        i.Id,
                        Tags = i.Tags.AsEntityBuilder<CollectionProjectionLiveTag>()
                            .OrderBy(t => t.Id)
                            .Limit(1)
                            .ToList(),
                    })
                    .ToList(),
            })
            .ToList();

        var items = rows.Single().Items;
        items.Should().HaveCount(2);
        items.Single(i => i.Id == 9710).Tags.Select(t => t.Id).Should().Equal([9770],
            "the nested many-to-many page is never an empty success");
        items.Single(i => i.Id == 9711).Tags.Select(t => t.Id).Should().Equal([9771]);
        counter.Executing.Should().Be(4, "split nested many-to-many: parent + items + junction + tags");
    }

    // ================================================================================================
    // Refusals — all raised while the projection is bound, before any SQL exists.
    // ================================================================================================

    internal static void AdapterRefusals(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);
        var all = ctx.From<CollectionProjectionLiveItem>().Select(i => new { i.Id });

        Action groupBy = () => LiveParents(ctx)
            .Select(p => new { p.Id, Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>().GroupBy(i => i.ParentId).ToList() });
        Action distinct = () => LiveParents(ctx)
            .Select(p => new { p.Id, Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>().Select(i => new { i.Id }).Distinct().ToList() });
        Action union = () => LiveParents(ctx)
            .Select(p => new { p.Id, Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>().Select(i => new { i.Id }).Union(all).ToList() });
        Action except = () => LiveParents(ctx)
            .Select(p => new { p.Id, Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>().Select(i => new { i.Id }).Except(all).ToList() });
        Action intersect = () => LiveParents(ctx)
            .Select(p => new { p.Id, Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>().Select(i => new { i.Id }).Intersect(all).ToList() });
        Action join = () => LiveParents(ctx)
            .Select(p => new { p.Id, Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>().Join(ctx.From<CollectionProjectionLiveNote>(), (i, n) => i.Id == n.ItemId).ToList() });

        groupBy.Should().Throw<NotSupportedException>().WithMessage("*applies GroupBy(...) inside its adapter chain*");
        distinct.Should().Throw<NotSupportedException>().WithMessage("*applies Distinct(...) inside its adapter chain*");
        union.Should().Throw<NotSupportedException>().WithMessage("*applies Union(...) inside its adapter chain*");
        except.Should().Throw<NotSupportedException>().WithMessage("*applies Except(...) inside its adapter chain*");
        intersect.Should().Throw<NotSupportedException>().WithMessage("*applies Intersect(...) inside its adapter chain*");
        join.Should().Throw<NotSupportedException>().WithMessage("*applies Join(...) inside its adapter chain*");
        counter.Executing.Should().Be(0, "every rejected adapter operation fails before a command exists");
    }

    internal static void CompositionAndShapeRefusals(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);

        Action grouping = () => LiveParents(ctx)
            .GroupBy(p => p.Id)
            .Select(p => new { p.Id, Items = p.Items });
        Action terminal = () => LiveParents(ctx)
            .Select(p => new { p.Id, Items = p.Items })
            .ToEnumerable();
        Action customSlot = () => LiveParents(ctx)
            .Select(p => new CollectionProjectionLiveObjectSlot { Id = p.Id, Items = p.Items });

        grouping.Should().Throw<NotSupportedException>().WithMessage("*cannot be combined with grouping*");
        terminal.Should().Throw<NotSupportedException>().WithMessage("*supports only ToList() and ToArray()*");
        customSlot.Should().Throw<NotSupportedException>()
            .WithMessage("*only List<T>, T[], IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T>*");
        counter.Executing.Should().Be(0, "composition and destination-shape refusals are pre-SQL");
    }

    // ================================================================================================
    // Reference cardinality, empty vs null and captured rebinding.
    // ================================================================================================

    internal static void BrokenReferenceCardinalityThrows(IDataContext split, IDataContext single)
    {
        // The guard is projection-scoped to a projection that carries a per-parent collection; a
        // reference-only projection stays on the ordinary implicit-navigation path and is not a carrier.
        // Both projections therefore co-read `Items`, exactly as the core witness does.
        object? splitObserved = null;
        Action splitQuery = () => splitObserved = split.From<CollectionProjectionLiveParent>()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Items = p.Items, Profile = p.Profile })
            .ToList();

        splitQuery.Should().Throw<InvalidOperationException>()
            .WithMessage("*CollectionProjectionLiveParent.Profile*")
            .WithMessage("*more than one distinct*");
        splitObserved.Should().BeNull("the guard completes before any projected row escapes");

        object? singleObserved = null;
        Action singleQuery = () => singleObserved = single.From<CollectionProjectionLiveParent>()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Items = p.Items, Profile = p.Profile })
            .ToList();

        singleQuery.Should().Throw<InvalidOperationException>()
            .WithMessage("*CollectionProjectionLiveParent.Profile*")
            .WithMessage("*more than one distinct*");
        singleObserved.Should().BeNull("the single-query stitcher rejects before the projector runs");
    }

    internal static void RepeatedIdentityAndMissingReference(IDataContext split, IDataContext single)
    {
        // 9707 owns one profile and two items: the single-query join repeats the profile's physical row,
        // and it must collapse to one instance without throwing.
        var splitRows = split.From<CollectionProjectionLiveParent>()
            .Where(p => p.Id == LiveRepeatedProfile || p.Id == LiveNoProfile)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Profile = p.Profile, Items = p.Items })
            .ToList();

        splitRows.Single(r => r.Id == LiveRepeatedProfile).Profile!.Id.Should().Be(9727);
        splitRows.Single(r => r.Id == LiveRepeatedProfile).Profile!.Label.Should().Be("profile-7");
        splitRows.Single(r => r.Id == LiveRepeatedProfile).Items.Should().HaveCount(2);
        splitRows.Single(r => r.Id == LiveNoProfile).Profile.Should().BeNull("a missing reference stays null");

        var counter = CollectionProjectionLiveCounter(single);
        var singleRows = single.From<CollectionProjectionLiveParent>()
            .Where(p => p.Id == LiveRepeatedProfile || p.Id == LiveNoProfile)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Profile = p.Profile, Items = p.Items })
            .ToList();

        singleRows.Single(r => r.Id == LiveRepeatedProfile).Profile!.Id.Should().Be(9727,
            "repeated physical rows of one identity collapse instead of throwing");
        singleRows.Single(r => r.Id == LiveRepeatedProfile).Items.Select(i => i.Id).Should().BeEquivalentTo([9717, 9718]);
        singleRows.Single(r => r.Id == LiveNoProfile).Profile.Should().BeNull();
        counter.Executing.Should().Be(1);
    }

    internal static void EmptyAtEveryLevel(IDataContext ctx)
    {
        var rows = LiveParents(ctx)
            .Where(p => p.Id == LiveEmpty || p.Id == LiveNoNotes)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>()
                    .OrderBy(i => i.Id)
                    .Select(i => new
                    {
                        i.Id,
                        Notes = i.Notes.AsEntityBuilder<CollectionProjectionLiveNote>()
                            .OrderBy(n => n.Id)
                            .Select(n => new { n.Id, Tips = n.Tips })
                            .ToList(),
                    })
                    .ToList(),
            })
            .ToList();

        rows.Single(r => r.Id == LiveEmpty).Items.Should().NotBeNull("level one: no rows is an empty collection").And.BeEmpty();

        var item = rows.Single(r => r.Id == LiveNoNotes).Items.Should().ContainSingle().Which;
        item.Notes.Should().NotBeNull("level two: no rows is an empty collection").And.BeEmpty();

        var richNote = LiveParents(ctx)
            .Where(p => p.Id == LiveRich)
            .Select(p => new
            {
                Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>()
                    .Where(i => i.Id == 9711)
                    .Select(i => new
                    {
                        i.Id,
                        Notes = i.Notes.AsEntityBuilder<CollectionProjectionLiveNote>()
                            .Select(n => new { n.Id, Tips = n.Tips })
                            .ToList(),
                    })
                    .ToList(),
            })
            .ToList()
            .Single()
            .Items.Single()
            .Notes.Single();

        richNote.Tips.Should().NotBeNull("level three: no rows is an empty collection").And.BeEmpty();
    }

    internal static void CapturedFilterRebinds(IDataContext ctx)
    {
        var counter = CollectionProjectionLiveCounter(ctx);
        var minimum = 9711;

        var command = LiveParents(ctx)
            .Where(p => p.Id == LiveRich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<CollectionProjectionLiveItem>()
                    .Where(i => i.Id >= minimum)
                    .OrderBy(i => i.Id)
                    .Select(i => new { i.Id })
                    .ToList(),
            });

        var first = command.ToList();
        first.Single().Items.Select(i => i.Id).Should().Equal([9711, 9712]);
        counter.Executing.Should().Be(2);

        minimum = 9712;

        var second = command.ToList();
        second.Single().Items.Select(i => i.Id).Should().Equal([9712],
            "the captured filter is rebound on every execution and never leaks between requests");
        counter.Executing.Should().Be(4);
    }

    internal static void CompositeJunctionSlotAndPageRefusal(IDataContext ctx)
    {
        var (first, second) = JoinIntoCompositeJunctionFixtures.Seed(ctx);
        var counter = CollectionProjectionLiveCounter(ctx);

        var rows = ctx.From<JiCompositeJunctionParent>()
            .Where(p => p.RowId == first || p.RowId == second)
            .OrderBy(p => p.RowId)
            .Select(p => new { p.RowId, Children = p.Children })
            .ToList();

        rows.Select(r => r.RowId).Should().Equal(first, second);
        rows[0].Children.Should().HaveCount(2, "two junction occurrences of one pair remain distinct elements");
        rows[0].Children.Select(c => c.Id).Should().OnlyContain(id => id == first + 100,
            "the junction's last parent component isolates the first parent");
        rows[1].Children.Select(c => c.Id).Should().Equal([first + 101],
            "the shared first component never cross-links the second parent");
        counter.Executing.Should().Be(3, "a composite junction slot is the ordinary split many-to-many");

        var pagedRows = ctx.From<JiCompositeJunctionParent>()
            .Where(p => p.RowId == first)
            .Select(p => new
            {
                p.RowId,
                Children = p.Children.AsEntityBuilder<JiCompositeJunctionChild>()
                    .OrderBy(c => c.Id)
                    .Limit(1)
                    .ToList(),
            })
            .ToList();

        pagedRows.Should().ContainSingle();
        pagedRows[0].Children.Should().ContainSingle(
            "the page keeps one of the two surviving junction occurrences of the composite pair");
        pagedRows[0].Children.Select(c => c.Id).Should().Equal([first + 100]);
    }

    // ================================================================================================
    // Shared facts — SQLite, PostgreSQL, SQL Server and MySQL (one abstraction instance per provider).
    // ================================================================================================

    [Fact]
    public void CollectionProjectionLive_Depth1_Direct_Split()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        Depth1DirectSplit(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_Depth1_Direct_Single()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        using var ctx = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        RegisterCollectionProjectionLive(ctx);
        Depth1DirectSingle(ctx);
    }

    [Fact]
    public void CollectionProjectionLive_Depth1_Adapter_Split()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        Depth1AdapterSplit(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_Depth1_Adapter_Shapes()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        Depth1AdapterShapes(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_Depth2_Direct_Split()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        Depth2DirectSplit(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_Depth2_Adapter_Single()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        using var ctx = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        RegisterCollectionProjectionLive(ctx);
        Depth2AdapterSingle(ctx);
    }

    [Fact]
    public void CollectionProjectionLive_Depth3_Split()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        Depth3Split(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_Depth3_Single()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        using var ctx = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        RegisterCollectionProjectionLive(ctx);
        Depth3Single(ctx);
    }

    [Fact]
    public void CollectionProjectionLive_FlatManyToMany_Direct_Split()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        FlatManyToManyDirectSplit(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_FlatManyToMany_Adapter_Single()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        using var ctx = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        RegisterCollectionProjectionLive(ctx);
        FlatManyToManyAdapterSingle(ctx);
    }

    [Fact]
    public void CollectionProjectionLive_NestedManyToMany_Split()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        NestedManyToManySplit(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_NestedManyToMany_Single()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        using var ctx = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        RegisterCollectionProjectionLive(ctx);
        NestedManyToManySingle(ctx);
    }

    [Fact]
    public void CollectionProjectionLive_ManyToManyWithLimit_Split()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        ManyToManyWithLimit_Split(_sut.DataProvider);
        NestedManyToManyWithLimit_Split(_sut.DataProvider);
        LoadWithManyToManyPage_Split(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_ManyToManyWithLimit_Single()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        using var ctx = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        RegisterCollectionProjectionLive(ctx);
        ManyToManyWithLimit_Single(ctx);
        LoadWithManyToManyPage_Single(ctx);
    }

    [Fact]
    public void CollectionProjectionLive_AdapterRefusals()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        AdapterRefusals(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_CompositionAndShapeRefusals()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        CompositionAndShapeRefusals(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_BrokenReferenceCardinality()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        using var single = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        RegisterCollectionProjectionLive(single);
        BrokenReferenceCardinalityThrows(_sut.DataProvider, single);
    }

    [Fact]
    public void CollectionProjectionLive_RepeatedIdentityAndMissingReference()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        using var single = Provider.CreateContext(new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        RegisterCollectionProjectionLive(single);
        RepeatedIdentityAndMissingReference(_sut.DataProvider, single);
    }

    [Fact]
    public void CollectionProjectionLive_EmptyAtEveryLevel()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        EmptyAtEveryLevel(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_CapturedFilterRebinds()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        CapturedFilterRebinds(_sut.DataProvider);
    }

    [Fact]
    public void CollectionProjectionLive_CompositeJunctionSlotAndPageRefusal()
    {
        EnsureCollectionProjectionLiveSeeded(_sut);
        CompositeJunctionSlotAndPageRefusal(_sut.DataProvider);
    }
}

/// <summary>The direct destination shape (<c>IReadOnlyCollection&lt;T&gt;</c>) used by the live shape witness.</summary>
public sealed class CollectionProjectionLiveShapes
{
    public IReadOnlyCollection<CollectionProjectionLiveItem> Items { get; set; } = [];
}

/// <summary>A destination outside the seven supported shapes: <c>object</c> must be refused, never substituted.</summary>
public sealed class CollectionProjectionLiveObjectSlot
{
    public int Id { get; set; }

    public object? Items { get; set; }
}

[SqlTable("eager_parent")]
public sealed class CollectionProjectionLiveParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public List<CollectionProjectionLiveItem> Items { get; } = [];

    public List<CollectionProjectionLiveTag> Tags { get; } = [];

    public CollectionProjectionLiveProfile? Profile { get; set; }
}

[SqlTable("eager_child")]
public sealed class CollectionProjectionLiveItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public List<CollectionProjectionLiveNote> Notes { get; } = [];

    public List<CollectionProjectionLiveTag> Tags { get; } = [];
}

[SqlTable("eager_note")]
public sealed class CollectionProjectionLiveNote
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ItemId { get; set; }

    [Column("text")]
    public string? Text { get; set; }

    public List<CollectionProjectionLiveTip> Tips { get; } = [];
}

[SqlTable("eager_link")]
public sealed class CollectionProjectionLiveTip
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int NoteId { get; set; }

    [Column("child_id")]
    public int Value { get; set; }
}

[SqlTable("eager_note")]
public sealed class CollectionProjectionLiveProfile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    // Deliberately not named ParentId: the child entity in the same projection already exposes a
    // ParentId, and ClickHouse rejects two select-list aliases with one name in a single statement
    // (MULTIPLE_EXPRESSIONS_FOR_ALIAS). The physical column stays parent_id; the property name is a
    // fixture choice recorded as a ClickHouse limitation in rv1/counts.md.
    [Column("parent_id")]
    public int OwnerKey { get; set; }

    [Column("text")]
    public string? Label { get; set; }
}

[SqlTable("eager_tag")]
public sealed class CollectionProjectionLiveTag
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("eager_link")]
public sealed class CollectionProjectionLiveLink
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
