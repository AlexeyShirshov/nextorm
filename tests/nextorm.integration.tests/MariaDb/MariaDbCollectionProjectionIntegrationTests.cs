using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// #249 / T353 D4b live rows on MariaDB. MariaDB has no <see cref="CommonTestSuite"/> subclass and no
/// <c>ITestProvider</c> (its container harness is standalone), so the matrix is re-pinned here on
/// dedicated <c>t353m_*</c> tables. It cannot share the <c>eager_*</c> names: other MariaDB classes
/// drop and recreate those tables in their own constructors, so sharing them would be a race.
/// <para>
/// Covered: depth one (direct and adapter, split and single, values and statement counts), the array and
/// <c>IReadOnlyCollection</c> destinations, depth two in split and single mode with per-level
/// filter/order/page and captured rebinding, depth three in both modes, flat and nested many-to-many in
/// both modes, every named pre-SQL refusal the shared suite carries, broken/duplicate/missing reference
/// cardinality and empty-versus-null at every level. Not covered here: the composite-junction slot (its
/// harness owns <c>eager_cj_*</c> per class, the same race), recorded in <c>rv1/variants.md</c> instead of
/// being skipped silently.
/// </para>
/// </summary>
public sealed class MariaDbCollectionProjectionIntegrationTests : IDisposable
{
    private const int Rich = 9701;
    private const int Empty = 9702;
    private const int NoNotes = 9703;
    private const int NoProfile = 9704;
    private const int Profile = 9705;
    private const int BrokenProfile = 9706;
    private const int RepeatedProfile = 9707;

    private readonly IDataContext _ctx;

    public MariaDbCollectionProjectionIntegrationTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    private EntityBuilder<Mdb353Parent> Parents(IDataContext ctx) => ctx.From<Mdb353Parent>(cfg => cfg
        .HasMany(p => p.Items, i => i.ParentId)
        .HasManyThrough<Mdb353Tag, Mdb353Link, int, int>(p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.ChildId)
        .HasOneToOne(p => p.Profile, p => p.Id, pr => pr.ParentId));

    private void Register()
    {
        _ = _ctx.From<Mdb353Item>(cfg => cfg
            .HasMany(i => i.Notes, n => n.ItemId)
            .HasManyThrough<Mdb353Tag, Mdb353Link, int, int>(i => i.Tags, i => i.Id, l => l.ParentId, t => t.Id, l => l.ChildId));
        _ = _ctx.From<Mdb353Note>(cfg => cfg
            .HasMany(n => n.Tips, t => t.NoteId));
        _ = Parents(_ctx);
    }

    private JoinIntoQueryCountingInterceptor Count()
    {
        var counter = new JoinIntoQueryCountingInterceptor();
        ((DataContext)_ctx).AddInterceptor(counter);
        return counter;
    }

    [Fact]
    public void CollectionProjectionLive_Depth1_Direct_Split()
    {
        var counter = Count();

        var rows = Parents(_ctx)
            .Where(p => p.Id == Rich || p.Id == Empty)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Items = p.Items })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(Rich, Empty);
        rows[0].Items.Select(i => i.Id).Should().BeEquivalentTo([9710, 9711, 9712]);
        rows[0].Items.Select(i => i.Name).Should().BeEquivalentTo(["beta", "alpha", "skip"]);
        rows[1].Items.Should().NotBeNull().And.BeEmpty();
        counter.Executing.Should().Be(2);
    }

    [Fact]
    public void CollectionProjectionLive_Depth1_Adapter_Split()
    {
        var counter = Count();

        var rows = Parents(_ctx)
            .Where(p => p.Id == Rich || p.Id == NoNotes)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<Mdb353Item>()
                    .Where(i => i.Name != "skip")
                    .OrderByDescending(i => i.Id)
                    .Limit(1)
                    .Select(i => new { i.Id, i.Name })
                    .ToList(),
            })
            .ToList();

        rows[0].Items.Should().ContainSingle().Which.Id.Should().Be(9711);
        rows[0].Items[0].Name.Should().Be("alpha");
        rows[1].Items.Should().ContainSingle().Which.Id.Should().Be(9713);
        counter.Executing.Should().Be(2);
    }

    [Fact]
    public void CollectionProjectionLive_Depth2_Adapter_Single()
    {
        using var single = new MariaDbDataContext(
            MariaDbContainer.ConnectionString,
            new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        RegisterSingle(single);
        var counter = new JoinIntoQueryCountingInterceptor();
        ((DataContext)single).AddInterceptor(counter);

        var page = 1;
        var command = single.From<Mdb353Parent>()
            .Where(p => p.Id == Rich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<Mdb353Item>()
                    .Where(i => i.Name != "skip")
                    .OrderBy(i => i.Id)
                    .Select(i => new
                    {
                        i.Id,
                        Notes = i.Notes.AsEntityBuilder<Mdb353Note>()
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
        items.Single(i => i.Id == 9710).Notes.Should().ContainSingle().Which.Id.Should().Be(9720);
        items.Single(i => i.Id == 9711).Notes.Single().Text.Should().Be("n-alpha");
        counter.Executing.Should().Be(1, "single: the depth-two graph is one statement");

        page = 2;
        var secondRun = command.ToList();
        secondRun.Single().Items.Single(i => i.Id == 9710).Notes.Select(n => n.Id).Should().Equal([9720, 9721]);
        counter.Executing.Should().Be(2, "the captured page is re-read and never leaks");
    }

    [Fact]
    public void CollectionProjectionLive_Depth1_Direct_Single()
    {
        using var single = new MariaDbDataContext(
            MariaDbContainer.ConnectionString,
            new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        RegisterSingle(single);
        var counter = new JoinIntoQueryCountingInterceptor();
        ((DataContext)single).AddInterceptor(counter);

        var rows = single.From<Mdb353Parent>()
            .Where(p => p.Id == Rich || p.Id == Empty)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Items = p.Items })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(Rich, Empty);
        // A direct slot carries no child ordering: single mode stitches the flattened rows in the
        // order the server returned them, and MariaDB is free to change that order between runs
        // (observed once as {9712, 9710, 9711} against a green isolated run). Order is only a
        // contract through an explicit OrderBy, so the assertion is set-based, like the shared suite.
        rows[0].Items.Select(i => i.Id).Should().BeEquivalentTo([9710, 9711, 9712]);
        rows[1].Items.Should().NotBeNull("an empty collection slot is empty, never null").And.BeEmpty();
        counter.Executing.Should().Be(1, "single: the inherited mode flattens the slot in one statement");
    }

    [Fact]
    public void CollectionProjectionLive_Depth1_Adapter_Shapes()
    {
        // Two of the seven CLR shapes live: an array through ToArray() and an IReadOnlyCollection
        // destination written directly from the navigation.
        var arrays = Parents(_ctx)
            .Where(p => p.Id == Rich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<Mdb353Item>().OrderBy(i => i.Id).ToArray(),
            })
            .ToList();

        arrays.Single().Items.Should().BeOfType<Mdb353Item[]>()
            .Which.Select(i => i.Id).Should().Equal(9710, 9711, 9712);

        var readOnly = Parents(_ctx)
            .Where(p => p.Id == Rich)
            .Select(p => new Mdb353Shapes { Items = p.Items })
            .ToList();

        readOnly.Single().Items.Should().BeAssignableTo<IReadOnlyCollection<Mdb353Item>>()
            .Which.Select(i => i.Id).Should().BeEquivalentTo([9710, 9711, 9712]);
    }

    [Fact]
    public void CollectionProjectionLive_FlatManyToMany_Direct_Split()
    {
        var counter = Count();

        var rows = Parents(_ctx)
            .Where(p => p.Id == Rich || p.Id == Profile || p.Id == Empty)
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Tags = p.Tags })
            .ToList();

        rows[0].Tags.Select(t => t.Id).Should().BeEquivalentTo([9770, 9771]);
        rows.Single(r => r.Id == Empty).Tags.Should().NotBeNull().And.BeEmpty();
        rows.Single(r => r.Id == Profile).Tags.Select(t => t.Name).Should().Equal("t-c");
        counter.Executing.Should().Be(3);
    }

    [Fact]
    public void CollectionProjectionLive_NestedManyToMany_Single()
    {
        using var single = new MariaDbDataContext(
            MariaDbContainer.ConnectionString,
            new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        RegisterSingle(single);
        var counter = new JoinIntoQueryCountingInterceptor();
        ((DataContext)single).AddInterceptor(counter);

        var rows = single.From<Mdb353Parent>()
            .Where(p => p.Id == Rich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<Mdb353Item>()
                    .Where(i => i.Name != "skip")
                    .OrderBy(i => i.Id)
                    .Select(i => new
                    {
                        i.Id,
                        Tags = i.Tags.AsEntityBuilder<Mdb353Tag>().OrderBy(t => t.Id).ToList(),
                    })
                    .ToList(),
            })
            .ToList();

        var items = rows.Single().Items;
        items.Should().HaveCount(2);
        items.Single(i => i.Id == 9710).Tags.Select(t => t.Id).Should().Equal([9770, 9771],
            "the nested many-to-many single slot is never an empty success");
        items.Single(i => i.Id == 9711).Tags.Select(t => t.Id).Should().Equal(9771);
        counter.Executing.Should().Be(1);
    }

    [Fact]
    public void CollectionProjectionLive_Depth2_Direct_Split()
    {
        var counter = Count();

        var rows = Parents(_ctx)
            .Where(p => p.Id == Rich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<Mdb353Item>()
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

    [Fact]
    public void CollectionProjectionLive_NestedManyToMany_Split()
    {
        var counter = Count();

        var rows = Parents(_ctx)
            .Where(p => p.Id == Rich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<Mdb353Item>()
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

    [Fact]
    public void CollectionProjectionLive_Depth3_Split()
    {
        var counter = Count();

        var rows = Parents(_ctx)
            .Where(p => p.Id == Rich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<Mdb353Item>()
                    .Where(i => i.Name != "skip")
                    .OrderBy(i => i.Id)
                    .Select(i => new
                    {
                        i.Id,
                        Notes = i.Notes.AsEntityBuilder<Mdb353Note>()
                            .OrderBy(n => n.Id)
                            .Select(n => new
                            {
                                n.Id,
                                Tips = n.Tips.AsEntityBuilder<Mdb353Tip>()
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

    [Fact]
    public void CollectionProjectionLive_Depth3_Single()
    {
        using var single = new MariaDbDataContext(
            MariaDbContainer.ConnectionString,
            new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        RegisterSingle(single);
        var counter = new JoinIntoQueryCountingInterceptor();
        ((DataContext)single).AddInterceptor(counter);

        var rows = single.From<Mdb353Parent>()
            .Where(p => p.Id == Rich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<Mdb353Item>()
                    .Where(i => i.Name != "skip")
                    .OrderBy(i => i.Id)
                    .Select(i => new
                    {
                        i.Id,
                        Notes = i.Notes.AsEntityBuilder<Mdb353Note>()
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
        // The innermost slot is a direct navigation with no declared OrderBy, so its row order belongs
        // to the provider. Only the set is contracted here; Depth3_Split asserts the ordered adapter.
        notes.Single(n => n.Id == 9720).Tips.Select(t => t.Id).Should().BeEquivalentTo([9730, 9731]);
        notes.Single(n => n.Id == 9721).Tips.Select(t => t.Id).Should().Equal(9732);
        rows.Single().Items.Single(i => i.Id == 9711).Notes.Single().Tips.Should().NotBeNull().And.BeEmpty();
        counter.Executing.Should().Be(1, "single inherits through all three levels");
    }

    [Fact]
    public void CollectionProjectionLive_ManyToManyWithLimit()
    {
        var counter = Count();

        var rows = Parents(_ctx)
            .Where(p => p.Id == Rich || p.Id == Profile || p.Id == Empty)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Tags = p.Tags.AsEntityBuilder<Mdb353Tag>().OrderBy(t => t.Id).Limit(1).ToList(),
            })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(Rich, Empty, Profile);
        rows[0].Tags.Select(t => t.Id).Should().Equal([9770],
            "the page splits this parent's surviving occurrences and keeps the first by id");
        rows[1].Tags.Should().NotBeNull().And.BeEmpty("a parent with no surviving occurrence keeps an empty page");
        rows[2].Tags.Select(t => t.Id).Should().Equal([9772]);
        counter.Executing.Should().Be(3, "split many-to-many: parent + junction + tag statements");
    }

    [Fact]
    public void CollectionProjectionLive_Refusals_ArePreSql()
    {
        var counter = Count();
        var navigation = Parents(_ctx);
        var all = _ctx.From<Mdb353Item>().Select(i => new { i.Id });

        Action groupBy = () => navigation
            .Select(p => new { p.Id, Items = p.Items.AsEntityBuilder<Mdb353Item>().GroupBy(i => i.ParentId).ToList() });
        Action distinct = () => navigation
            .Select(p => new { p.Id, Items = p.Items.AsEntityBuilder<Mdb353Item>().Select(i => new { i.Id }).Distinct().ToList() });
        Action union = () => navigation
            .Select(p => new { p.Id, Items = p.Items.AsEntityBuilder<Mdb353Item>().Select(i => new { i.Id }).Union(all).ToList() });
        Action except = () => navigation
            .Select(p => new { p.Id, Items = p.Items.AsEntityBuilder<Mdb353Item>().Select(i => new { i.Id }).Except(all).ToList() });
        Action intersect = () => navigation
            .Select(p => new { p.Id, Items = p.Items.AsEntityBuilder<Mdb353Item>().Select(i => new { i.Id }).Intersect(all).ToList() });
        Action join = () => navigation
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<Mdb353Item>()
                    .Join(_ctx.From<Mdb353Note>(), (i, n) => i.Id == n.ItemId)
                    .ToList(),
            });
        Action grouping = () => _ctx.From<Mdb353Parent>()
            .GroupBy(p => p.Id)
            .Select(p => new { p.Id, Items = p.Items });
        Action terminal = () => navigation
            .Select(p => new { p.Id, Items = p.Items })
            .ToEnumerable();
        Action customSlot = () => navigation
            .Select(p => new Mdb353ObjectSlot { Id = p.Id, Items = p.Items });

        groupBy.Should().Throw<NotSupportedException>().WithMessage("*applies GroupBy(...) inside its adapter chain*");
        distinct.Should().Throw<NotSupportedException>().WithMessage("*applies Distinct(...) inside its adapter chain*");
        union.Should().Throw<NotSupportedException>().WithMessage("*applies Union(...) inside its adapter chain*");
        except.Should().Throw<NotSupportedException>().WithMessage("*applies Except(...) inside its adapter chain*");
        intersect.Should().Throw<NotSupportedException>().WithMessage("*applies Intersect(...) inside its adapter chain*");
        join.Should().Throw<NotSupportedException>().WithMessage("*applies Join(...) inside its adapter chain*");
        grouping.Should().Throw<NotSupportedException>().WithMessage("*cannot be combined with grouping*");
        terminal.Should().Throw<NotSupportedException>().WithMessage("*supports only ToList() and ToArray()*");
        customSlot.Should().Throw<NotSupportedException>()
            .WithMessage("*only List<T>, T[], IEnumerable<T>, ICollection<T>, IList<T>, IReadOnlyCollection<T> and IReadOnlyList<T>*");
        counter.Executing.Should().Be(0, "every rejected slot or adapter operation fails before a command exists");
    }

    [Fact]
    public void CollectionProjectionLive_CardinalityAndEmptyVersusNull()
    {
        using var single = new MariaDbDataContext(
            MariaDbContainer.ConnectionString,
            new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
        RegisterSingle(single);

        // A reference-only projection is not a carrier (the implicit-navigation path owns it), so the
        // guard's witness co-reads `Items`, exactly as the core cardinality fixture does.
        object? splitObserved = null;
        Action splitQuery = () => splitObserved = _ctx.From<Mdb353Parent>()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Items = p.Items, Profile = p.Profile })
            .ToList();
        splitQuery.Should().Throw<InvalidOperationException>()
            .WithMessage("*Mdb353Parent.Profile*")
            .WithMessage("*more than one distinct*");
        splitObserved.Should().BeNull();

        object? singleObserved = null;
        Action singleQuery = () => singleObserved = single.From<Mdb353Parent>()
            .OrderBy(p => p.Id)
            .Select(p => new { p.Id, Items = p.Items, Profile = p.Profile })
            .ToList();
        singleQuery.Should().Throw<InvalidOperationException>()
            .WithMessage("*Mdb353Parent.Profile*");
        singleObserved.Should().BeNull();

        var repeated = single.From<Mdb353Parent>()
            .Where(p => p.Id == RepeatedProfile || p.Id == NoProfile || p.Id == Empty || p.Id == NoNotes)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                Profile = p.Profile,
                Items = p.Items.AsEntityBuilder<Mdb353Item>()
                    .OrderBy(i => i.Id)
                    .Select(i => new
                    {
                        i.Id,
                        Notes = i.Notes.AsEntityBuilder<Mdb353Note>().OrderBy(n => n.Id).Select(n => new { n.Id, Tips = n.Tips }).ToList(),
                    })
                    .ToList(),
            })
            .ToList();

        repeated.Single(r => r.Id == RepeatedProfile).Profile!.Id.Should().Be(9727,
            "repeated physical rows of one identity collapse instead of throwing");
        repeated.Single(r => r.Id == RepeatedProfile).Items.Should().HaveCount(2);
        repeated.Single(r => r.Id == NoProfile).Profile.Should().BeNull();
        repeated.Single(r => r.Id == Empty).Items.Should().NotBeNull().And.BeEmpty();
        repeated.Single(r => r.Id == NoNotes).Items.Single().Notes.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void CollectionProjectionLive_CapturedFilterRebinds()
    {
        var counter = Count();
        var minimum = 9711;

        var command = Parents(_ctx)
            .Where(p => p.Id == Rich)
            .Select(p => new
            {
                p.Id,
                Items = p.Items.AsEntityBuilder<Mdb353Item>()
                    .Where(i => i.Id >= minimum)
                    .OrderBy(i => i.Id)
                    .Select(i => new { i.Id })
                    .ToList(),
            });

        command.ToList().Single().Items.Select(i => i.Id).Should().Equal([9711, 9712]);
        counter.Executing.Should().Be(2);

        minimum = 9712;
        command.ToList().Single().Items.Select(i => i.Id).Should().Equal([9712]);
        counter.Executing.Should().Be(4);
    }

    private void RegisterSingle(IDataContext ctx)
    {
        _ = ctx.From<Mdb353Item>(cfg => cfg
            .HasMany(i => i.Notes, n => n.ItemId)
            .HasManyThrough<Mdb353Tag, Mdb353Link, int, int>(i => i.Tags, i => i.Id, l => l.ParentId, t => t.Id, l => l.ChildId));
        _ = ctx.From<Mdb353Note>(cfg => cfg
            .HasMany(n => n.Tips, t => t.NoteId));
        _ = ctx.From<Mdb353Parent>(cfg => cfg
            .HasMany(p => p.Items, i => i.ParentId)
            .HasManyThrough<Mdb353Tag, Mdb353Link, int, int>(p => p.Tags, p => p.Id, l => l.ParentId, t => t.Id, l => l.ChildId)
            .HasOneToOne(p => p.Profile, p => p.Id, pr => pr.ParentId));
    }

    private void Seed()
    {
        foreach (var statement in SchemaStatements)
            Execute(statement);

        Register();

        _ctx.CreateInsertBuilder<Mdb353Parent>().Values([
            new Mdb353Parent { Id = Rich, Name = "r1" },
            new Mdb353Parent { Id = Empty, Name = "r2" },
            new Mdb353Parent { Id = NoNotes, Name = "r3" },
            new Mdb353Parent { Id = NoProfile, Name = "r4" },
            new Mdb353Parent { Id = Profile, Name = "r5" },
            new Mdb353Parent { Id = BrokenProfile, Name = "r6" },
            new Mdb353Parent { Id = RepeatedProfile, Name = "r7" },
        ]).Insert();

        _ctx.CreateInsertBuilder<Mdb353Item>().Values([
            new Mdb353Item { Id = 9710, ParentId = Rich, Name = "beta" },
            new Mdb353Item { Id = 9711, ParentId = Rich, Name = "alpha" },
            new Mdb353Item { Id = 9712, ParentId = Rich, Name = "skip" },
            new Mdb353Item { Id = 9713, ParentId = NoNotes, Name = "lonely" },
            new Mdb353Item { Id = 9717, ParentId = RepeatedProfile, Name = "r7-a" },
            new Mdb353Item { Id = 9718, ParentId = RepeatedProfile, Name = "r7-b" },
        ]).Insert();

        _ctx.CreateInsertBuilder<Mdb353Note>().Values([
            new Mdb353Note { Id = 9720, ItemId = 9710, Text = "n-beta-2" },
            new Mdb353Note { Id = 9721, ItemId = 9710, Text = "n-beta-1" },
            new Mdb353Note { Id = 9722, ItemId = 9711, Text = "n-alpha" },
            new Mdb353Note { Id = 9723, ItemId = 9712, Text = "n-skip" },
        ]).Insert();

        _ctx.CreateInsertBuilder<Mdb353Profile>().Values([
            new Mdb353Profile { Id = 9724, ParentId = Profile, Label = "profile-5" },
            new Mdb353Profile { Id = 9725, ParentId = BrokenProfile, Label = "profile-6a" },
            new Mdb353Profile { Id = 9726, ParentId = BrokenProfile, Label = "profile-6b" },
            new Mdb353Profile { Id = 9727, ParentId = RepeatedProfile, Label = "profile-7" },
        ]).Insert();

        _ctx.CreateInsertBuilder<Mdb353Tag>().Values([
            new Mdb353Tag { Id = 9770, Name = "t-a" },
            new Mdb353Tag { Id = 9771, Name = "t-b" },
            new Mdb353Tag { Id = 9772, Name = "t-c" },
        ]).Insert();

        _ctx.CreateInsertBuilder<Mdb353Tip>().Values([
            new Mdb353Tip { Id = 9730, NoteId = 9720, Value = 9780 },
            new Mdb353Tip { Id = 9731, NoteId = 9720, Value = 9781 },
            new Mdb353Tip { Id = 9732, NoteId = 9721, Value = 9782 },
        ]).Insert();

        _ctx.CreateInsertBuilder<Mdb353Link>().Values([
            new Mdb353Link { Id = 9740, ParentId = Rich, ChildId = 9770 },
            new Mdb353Link { Id = 9741, ParentId = Rich, ChildId = 9771 },
            new Mdb353Link { Id = 9747, ParentId = Profile, ChildId = 9772 },
            new Mdb353Link { Id = 9743, ParentId = 9710, ChildId = 9770 },
            new Mdb353Link { Id = 9744, ParentId = 9710, ChildId = 9771 },
            new Mdb353Link { Id = 9745, ParentId = 9711, ChildId = 9771 },
            new Mdb353Link { Id = 9746, ParentId = 9712, ChildId = 9772 },
        ]).Insert();
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }

    private static readonly string[] SchemaStatements =
    [
        "drop table if exists t353m_link",
        "drop table if exists t353m_tag",
        "drop table if exists t353m_note",
        "drop table if exists t353m_item",
        "drop table if exists t353m_parent",
        "create table t353m_parent (id int not null primary key, name varchar(100) null)",
        "create table t353m_item (id int not null primary key, parent_id int not null, name varchar(100) null)",
        "create table t353m_note (id int not null primary key, item_id int not null, text varchar(100) null)",
        "create table t353m_tag (id int not null primary key, name varchar(100) null)",
        "create table t353m_link (id int not null primary key, parent_id int not null, child_id int not null)",
    ];
}

/// <summary>The direct destination shape (<c>IReadOnlyCollection&lt;T&gt;</c>) used by the live shape witness.</summary>
public sealed class Mdb353Shapes
{
    public IReadOnlyCollection<Mdb353Item> Items { get; set; } = [];
}

/// <summary>A destination outside the seven supported shapes: <c>object</c> must be refused, never substituted.</summary>
public sealed class Mdb353ObjectSlot
{
    public int Id { get; set; }

    public object? Items { get; set; }
}

[SqlTable("t353m_parent")]
public sealed class Mdb353Parent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public List<Mdb353Item> Items { get; } = [];

    public List<Mdb353Tag> Tags { get; } = [];

    public Mdb353Profile? Profile { get; set; }
}

[SqlTable("t353m_item")]
public sealed class Mdb353Item
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public List<Mdb353Note> Notes { get; } = [];

    public List<Mdb353Tag> Tags { get; } = [];
}

[SqlTable("t353m_note")]
public sealed class Mdb353Note
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("item_id")]
    public int ItemId { get; set; }

    [Column("text")]
    public string? Text { get; set; }

    public List<Mdb353Tip> Tips { get; } = [];
}

[SqlTable("t353m_link")]
public sealed class Mdb353Tip
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int NoteId { get; set; }

    [Column("child_id")]
    public int Value { get; set; }
}

[SqlTable("t353m_note")]
public sealed class Mdb353Profile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("item_id")]
    public int ParentId { get; set; }

    [Column("text")]
    public string? Label { get; set; }
}

[SqlTable("t353m_tag")]
public sealed class Mdb353Tag
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("t353m_link")]
public sealed class Mdb353Link
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
