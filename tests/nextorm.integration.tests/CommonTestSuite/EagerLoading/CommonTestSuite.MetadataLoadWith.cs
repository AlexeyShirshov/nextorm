using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// T249-D3 split execution at the live boundary: the metadata-driven <c>LoadWith</c> is exercised against
/// real providers with command counting, so the no-N+1 property is observed, not inferred. The scenarios
/// reuse the existing composite fixtures (declared through the public fluent overloads) and the shared
/// eager tables, so no provider DDL is added. This file is authored with the D3 unit and executed by the
/// D6 DO→CHECK boundary run; it is deliberately not part of the D3 inner loop.
/// </summary>
public abstract partial class CommonTestSuite
{
    // ---------------------------------------------------------------------------------------------
    // Batching: the number of child commands is bounded by the chunk policy, never by the parent count.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Split_OneParent_TwoCommands()
    {
        var ctx = _sut.DataProvider;
        var (first, _, _) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);
        var interceptor = CountQueries(ctx);

        var parents = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == first)
            .LoadWith(p => p.Children)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Select(c => c.Id).Should().BeEquivalentTo([first + 100, first + 101]);
        // The parent query plus one child query: a single parent never triggers per-parent SQL.
        interceptor.Executing.Should().Be(2);
    }

    [Fact]
    public void MetadataLoadWith_Split_1000Parents_TwoCommands()
    {
        var (first, last) = SeedMetadataBulkParents(1000);

        var ctx = _sut.DataProvider;
        var interceptor = CountQueries(ctx);
        var parents = ctx.From<MetadataBulkParent>()
            .Where(p => p.Id >= first && p.Id <= last)
            .LoadWith(p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(1000);
        parents.Should().OnlyContain(p => p.Children.Count == 1);
        interceptor.Executing.Should().Be(2, "1000 distinct keys fit one child command");
    }

    [Fact]
    public void MetadataLoadWith_Split_1001Parents_ThreeCommands()
    {
        var (first, last) = SeedMetadataBulkParents(1001);

        var ctx = _sut.DataProvider;
        var interceptor = CountQueries(ctx);
        var parents = ctx.From<MetadataBulkParent>()
            .Where(p => p.Id >= first && p.Id <= last)
            .LoadWith(p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(1001);
        parents.Should().OnlyContain(p => p.Children.Count == 1);
        interceptor.Executing.Should().Be(3, "1001 distinct keys cross the 1000-key chunk boundary once");
    }

    [Fact]
    public void MetadataLoadWith_Split_Composite_1001Parents_ThreeCommands()
    {
        var (first, last) = SeedMetadataBulkCompositeParents(1001);

        var ctx = _sut.DataProvider;
        var interceptor = CountQueries(ctx);
        var parents = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId >= first && p.RowId <= last)
            .LoadWith(p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        parents.Should().HaveCount(1001);
        parents.Should().OnlyContain(p => p.Children.Count == 1);
        // Two components still chunk at 1000 keys (budget 2000 parameters), so 1001 parents need two
        // child commands plus the parent query.
        interceptor.Executing.Should().Be(3);
    }

    // ---------------------------------------------------------------------------------------------
    // Parent preservation and missing/dangling relationships.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_ChildFilter_KeepsParentsWithoutMatches()
    {
        var ctx = _sut.DataProvider;
        var (a, b, c) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);

        var parents = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a || p.RowId == b || p.RowId == c)
            .LoadWith(p => p.Children, child => child.From<T251DeclaredChild>().Where(x => x.SortKey == 3))
            .OrderBy(p => p.RowId)
            .ToList();

        parents.Select(p => p.RowId).Should().Equal(new[] { a, b, c }, "a child filter never filters parents");
        parents[0].Children.Should().BeEmpty();
        parents[1].Children.Select(x => x.Id).Should().Equal(a + 102);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    public void MetadataLoadWith_DanglingAndNullForeignKeys_YieldNoChildrenOrReference()
    {
        var ctx = _sut.DataProvider;
        var (parent, tenant, link, validChild) = JoinIntoCompositeKeyFixtures.SeedDeclaredNullable(ctx);

        var collections = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == parent)
            .LoadWith(p => p.NullableChildren)
            .ToList();

        collections.Single().NullableChildren.Select(ch => ch.Id).Should().Equal(validChild);

        JoinIntoCompositeKeyFixtures.RegisterDeclared(ctx);
        var baseId = NextEagerId();
        ctx.CreateInsertBuilder<T251SingleNullableChild>().Values([
            new T251SingleNullableChild { Id = baseId, Tenant = tenant, ParentId = link, SortKey = 1 },
            new T251SingleNullableChild { Id = baseId - 1, Tenant = tenant, ParentId = null, SortKey = 2 },
            new T251SingleNullableChild { Id = baseId - 2, Tenant = tenant, ParentId = link + 999, SortKey = 3 },
        ]).Insert();

        var references = ctx.From<T251SingleNullableChild>()
            .Where(ch => ch.Id == baseId || ch.Id == baseId - 1 || ch.Id == baseId - 2)
            .LoadWith(ch => ch.Parent!)
            .OrderByDescending(ch => ch.Id)
            .ToList();

        references.Should().HaveCount(3);
        references.Single(ch => ch.Id == baseId).Parent!.RowId.Should().Be(parent);
        references.Single(ch => ch.Id == baseId - 1).Parent.Should().BeNull("a null foreign key matches nothing");
        references.Single(ch => ch.Id == baseId - 2).Parent.Should().BeNull("a dangling foreign key has no principal");
    }

    // ---------------------------------------------------------------------------------------------
    // Reference cardinality: 0/1/>1 distinct full identity, on a live database.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Reference_ZerosOnesAndManyToOne_AreResolvedPerFullIdentity()
    {
        var ctx = _sut.DataProvider;
        var (withChild, otherChild, childless) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToOne(ctx);

        var parents = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == withChild || p.RowId == otherChild || p.RowId == childless)
            .LoadWith(p => p.Primary)
            .OrderBy(p => p.RowId)
            .ToList();

        parents.Select(p => p.RowId).Should().Equal(withChild, otherChild, childless);
        parents[0].Primary!.Id.Should().Be(withChild + 110);
        parents[1].Primary!.Id.Should().Be(otherChild + 110);
        parents[2].Primary.Should().BeNull("zero distinct identities resolve to null");

        var (a, _, _) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);
        var children = ctx.From<T251DeclaredChild>()
            .Where(ch => ch.Id == a + 100 || ch.Id == a + 105)
            .LoadWith(ch => ch.Parent!)
            .OrderBy(ch => ch.Id)
            .ToList();

        children.Single(ch => ch.Id == a + 100).Parent!.RowId.Should().Be(a);
        children.Single(ch => ch.Id == a + 105).Parent.Should().BeNull("a dangling composite key resolves to null");
    }

    [Fact]
    public void MetadataLoadWith_Reference_TwoDistinctIdentities_ThrowBeforeAssignment()
    {
        var ctx = _sut.DataProvider;
        var (a, _, _) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);

        // Two distinct rows share the one-to-one composite key: the full identity (Id) differs, so the
        // reference cannot be chosen deterministically and must fail before any parent is mutated.
        ctx.CreateInsertBuilder<T251DeclaredPrimary>().Values([
            new T251DeclaredPrimary { Id = a + 120, OwnerTenant = a, ParentLink = a + 10, SortKey = 1 },
            new T251DeclaredPrimary { Id = a + 121, OwnerTenant = a, ParentLink = a + 10, SortKey = 2 },
        ]).Insert();

        FluentActions.Invoking(() => ctx.From<T251DeclaredManyParent>()
                .Where(p => p.RowId == a)
                .LoadWith(p => p.Primary)
                .ToList())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*more than one distinct*");
    }

    // ---------------------------------------------------------------------------------------------
    // Many-to-many: occurrence preservation, dangling links, two independent collections.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_ManyToMany_RepeatedLinkPreservesOccurrenceAndIgnoresDangling()
    {
        var ctx = _sut.DataProvider;
        var (first, second) = JoinIntoCompositeJunctionFixtures.SeedDeclared(ctx);

        var parents = ctx.From<T251DeclaredJunctionParent>()
            .Where(p => p.RowId == first || p.RowId == second)
            .LoadWith(p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        parents.Select(p => p.RowId).Should().Equal(first, second);
        parents[0].Children.Should().HaveCount(2, "the repeated link is preserved and the dangling link contributes nothing");
        parents[0].Children.Should().OnlyContain(ch => ch.Id == first + 100);
        parents[1].Children.Should().ContainSingle().Which.Id.Should().Be(first + 101);
    }

    [Fact]
    public void MetadataLoadWith_ManyToMany_TwoCollectionsOverTheSameLinkTable_DoNotMultiply()
    {
        var ctx = _sut.DataProvider;
        var (first, _) = JoinIntoCompositeJunctionFixtures.SeedDeclared(ctx);
        MetadataTwoCollectionsParent.Register(ctx);

        var parents = ctx.From<MetadataTwoCollectionsParent>()
            .Where(p => p.RowId == first)
            .LoadWith(p => p.PrimaryChildren)
            .LoadWith(p => p.SecondaryChildren)
            .ToList();

        parents.Should().ContainSingle();
        parents[0].PrimaryChildren.Should().HaveCount(2);
        parents[0].SecondaryChildren.Should().HaveCount(2);
        parents[0].PrimaryChildren.Should().OnlyContain(ch => ch.Id == first + 100);
        parents[0].SecondaryChildren.Should().OnlyContain(ch => ch.Id == first + 100);
    }

    // ---------------------------------------------------------------------------------------------
    // Nesting: the child graph recurses through the same metadata path at every level.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Nested_CollectionToReference_RecursesThroughTheChildBuilder()
    {
        var ctx = _sut.DataProvider;
        var (a, _, _) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);

        var parents = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a)
            .LoadWith(p => p.Children, child => child
                .From<T251DeclaredChild>()
                .LoadWith(ch => ch.Parent!))
            .ToList();

        parents.Should().ContainSingle();
        parents[0].Children.Should().HaveCount(2);
        parents[0].Children.Should().OnlyContain(ch => ch.Parent != null && ch.Parent.RowId == a);
    }

    [Fact]
    public void MetadataLoadWith_Nested_CollectionToCollection_RecursesThroughTheChildBuilder()
    {
        var ctx = _sut.DataProvider;
        MetadataNestedFixture.Register(ctx);

        var baseId = NextEagerId();
        // Both child ids stay inside this test's own 1_000-wide id slot: the shared eager_child table is
        // also written by the legacy eager fixtures, whose bases are the neighbouring multiples of 1_000, so
        // a neighbouring slot would seed rows another test then sees (and collides with) as its own.
        var childA = baseId + 200;
        var childB = baseId + 100;

        ctx.CreateInsertBuilder<MetadataNestedParent>().Values([
            new MetadataNestedParent { Id = baseId, Name = "parent" },
        ]).Insert();

        ctx.CreateInsertBuilder<MetadataNestedChild>().Values([
            new MetadataNestedChild { Id = childA, ParentId = baseId, Name = "child-a" },
            new MetadataNestedChild { Id = childB, ParentId = baseId, Name = "child-b" },
        ]).Insert();

        ctx.CreateInsertBuilder<MetadataNestedGrandChild>().Values([
            new MetadataNestedGrandChild { Id = childA - 10, ParentId = childA, Name = "grand-a1" },
            new MetadataNestedGrandChild { Id = childA - 11, ParentId = childA, Name = "grand-a2" },
            new MetadataNestedGrandChild { Id = childB - 10, ParentId = childB, Name = "grand-b1" },
        ]).Insert();

        var parents = ctx.From<MetadataNestedParent>()
            .Where(p => p.Id == baseId)
            .LoadWith(p => p.Children, child => child
                .From<MetadataNestedChild>()
                .LoadWith(ch => ch.GrandChildren))
            .ToList();

        parents.Should().ContainSingle();
        var children = parents[0].Children.OrderBy(c => c.Id).ToList();
        children.Select(c => c.Id).Should().Equal(childB, childA);
        children.Single(c => c.Id == childA).GrandChildren.Select(g => g.Id).Should().BeEquivalentTo([childA - 10, childA - 11]);
        children.Single(c => c.Id == childB).GrandChildren.Select(g => g.Id).Should().Equal(childB - 10);
    }

    // ---------------------------------------------------------------------------------------------
    // Terminals: the existing list and array terminals materialize the metadata-driven graph.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Terminals_SyncListAndArray_AreStitched()
    {
        var ctx = _sut.DataProvider;
        var (a, _, _) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);

        var list = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a)
            .LoadWith(p => p.Children)
            .ToList();

        var array = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a)
            .LoadWith(p => p.Children)
            .ToArray();

        list.Should().ContainSingle();
        array.Should().ContainSingle();
        list[0].Children.Select(c => c.Id).Should().BeEquivalentTo([a + 100, a + 101]);
        array[0].Children.Select(c => c.Id).Should().BeEquivalentTo([a + 100, a + 101]);
    }

    // ---------------------------------------------------------------------------------------------
    // T249-D4 single execution at the live boundary: one command per level, split/single result
    // equivalence, left preservation, nested two-level shapes, M:N occurrence and reference
    // cardinality. Authored here and executed by the D6 DO→CHECK boundary run, not in the D4 inner loop.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_SingleQuery_Collection_IssuesOneCommandAndMatchesTheSplitGraph()
    {
        var ctx = _sut.DataProvider;
        var (a, _, _) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);
        var range = new[] { a, a + 1, a + 2 };

        var split = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId >= a && p.RowId <= a + 2)
            .LoadWith(p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        var interceptor = CountQueries(ctx);
        var single = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId >= a && p.RowId <= a + 2)
            .LoadWith(p => p.Children, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.RowId)
            .ToList();

        interceptor.Executing.Should().Be(1, "single execution issues one denormalized command");
        single.Select(p => p.RowId).Should().Equal(split.Select(p => p.RowId));
        single.Should().HaveCount(split.Count);
        for (var i = 0; i < split.Count; i++)
            single[i].Children.Select(c => c.Id).Should().BeEquivalentTo(split[i].Children.Select(c => c.Id));

        single.Sum(p => p.Children.Count).Should().Be(split.Sum(p => p.Children.Count),
            "the parent/child totals agree between the two modes");
        single.Select(p => p.RowId).Should().OnlyContain(id => range.Contains(id));
    }

    [Fact]
    public void MetadataLoadWith_SingleQuery_LeftJoin_KeepsParentsWhoseChildrenAreFilteredOut()
    {
        var ctx = _sut.DataProvider;
        var (a, b, c) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);
        var interceptor = CountQueries(ctx);

        var parents = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a || p.RowId == b || p.RowId == c)
            .LoadWith(p => p.Children, child => child.From<T251DeclaredChild>().Where(x => x.SortKey == 3), EagerLoadMode.SingleQuery)
            .OrderBy(p => p.RowId)
            .ToList();

        interceptor.Executing.Should().Be(1);
        parents.Select(p => p.RowId).Should().Equal(new[] { a, b, c }, "a child filter never filters the parents");
        parents[0].Children.Should().BeEmpty();
        parents[1].Children.Select(x => x.Id).Should().Equal(a + 102);
        parents[2].Children.Should().BeEmpty();
    }

    [Fact]
    public void MetadataLoadWith_SingleQuery_NestedCollectionToReference_StitchesBothLevelsInOneCommand()
    {
        var ctx = _sut.DataProvider;
        var (a, _, _) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);
        var interceptor = CountQueries(ctx);

        var parents = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == a)
            .LoadWith(
                p => p.Children,
                child => child.From<T251DeclaredChild>().LoadWith(ch => ch.Parent!),
                EagerLoadMode.SingleQuery)
            .ToList();

        interceptor.Executing.Should().Be(1, "both levels share one denormalized command");
        parents.Should().ContainSingle();
        parents[0].Children.Should().HaveCount(2);
        parents[0].Children.Should().OnlyContain(ch => ch.Parent != null && ch.Parent.RowId == a && ch.Parent.Tenant == a);
    }

    [Fact]
    public void MetadataLoadWith_SingleQuery_NestedCollectionToCollection_StitchesGrandChildrenPerChild()
    {
        var ctx = _sut.DataProvider;
        MetadataNestedFixture.Register(ctx);

        var baseId = NextEagerId();
        // Both child ids stay inside this test's own 1_000-wide id slot: the shared eager_child table is
        // also written by the legacy eager fixtures, whose bases are the neighbouring multiples of 1_000, so
        // a neighbouring slot would seed rows another test then sees (and collides with) as its own.
        var childA = baseId + 200;
        var childB = baseId + 100;

        ctx.CreateInsertBuilder<MetadataNestedParent>().Values([
            new MetadataNestedParent { Id = baseId, Name = "parent" },
        ]).Insert();

        ctx.CreateInsertBuilder<MetadataNestedChild>().Values([
            new MetadataNestedChild { Id = childA, ParentId = baseId, Name = "child-a" },
            new MetadataNestedChild { Id = childB, ParentId = baseId, Name = "child-b" },
        ]).Insert();

        ctx.CreateInsertBuilder<MetadataNestedGrandChild>().Values([
            new MetadataNestedGrandChild { Id = childA - 10, ParentId = childA, Name = "grand-a1" },
            new MetadataNestedGrandChild { Id = childA - 11, ParentId = childA, Name = "grand-a2" },
            new MetadataNestedGrandChild { Id = childB - 10, ParentId = childB, Name = "grand-b1" },
        ]).Insert();

        var interceptor = CountQueries(ctx);
        var parents = ctx.From<MetadataNestedParent>()
            .Where(p => p.Id == baseId)
            .LoadWith(
                p => p.Children,
                child => child.From<MetadataNestedChild>().LoadWith(ch => ch.GrandChildren),
                EagerLoadMode.SingleQuery)
            .ToList();

        interceptor.Executing.Should().Be(1);
        parents.Should().ContainSingle();
        var children = parents[0].Children.OrderBy(c => c.Id).ToList();
        children.Select(c => c.Id).Should().Equal(childB, childA);
        children.Single(c => c.Id == childA).GrandChildren.Select(g => g.Id).Should().BeEquivalentTo([childA - 10, childA - 11]);
        children.Single(c => c.Id == childB).GrandChildren.Select(g => g.Id).Should().Equal(childB - 10);
    }

    [Fact]
    public void MetadataLoadWith_SingleQuery_ManyToMany_KeepsTheRepeatedLinkInOneCommand()
    {
        var ctx = _sut.DataProvider;
        var (first, second) = JoinIntoCompositeJunctionFixtures.SeedDeclared(ctx);
        var interceptor = CountQueries(ctx);

        var parents = ctx.From<T251DeclaredJunctionParent>()
            .Where(p => p.RowId == first || p.RowId == second)
            .LoadWith(p => p.Children, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.RowId)
            .ToList();

        interceptor.Executing.Should().Be(1, "the link and child edges share one command");
        parents.Select(p => p.RowId).Should().Equal(first, second);
        parents[0].Children.Should().HaveCount(2, "the repeated link is preserved and the dangling link contributes nothing");
        parents[0].Children.Should().OnlyContain(ch => ch.Id == first + 100);
        parents[1].Children.Should().ContainSingle().Which.Id.Should().Be(first + 101);
    }

    [Fact]
    public void MetadataLoadWith_SingleQuery_Reference_ResolvesZeroOneAndManyToOnePerFullIdentity()
    {
        var ctx = _sut.DataProvider;
        var (withChild, otherChild, childless) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToOne(ctx);
        var interceptor = CountQueries(ctx);

        var parents = ctx.From<T251DeclaredManyParent>()
            .Where(p => p.RowId == withChild || p.RowId == otherChild || p.RowId == childless)
            .LoadWith(p => p.Primary, EagerLoadMode.SingleQuery)
            .OrderBy(p => p.RowId)
            .ToList();

        interceptor.Executing.Should().Be(1, "a reference join is one command");
        parents.Select(p => p.RowId).Should().Equal(withChild, otherChild, childless);
        parents[0].Primary!.Id.Should().Be(withChild + 110);
        parents[1].Primary!.Id.Should().Be(otherChild + 110);
        parents[2].Primary.Should().BeNull("zero matching identities resolves to null");

        var (a, _, _) = JoinIntoCompositeKeyFixtures.SeedDeclaredOneToMany(ctx);
        var children = ctx.From<T251DeclaredChild>()
            .Where(ch => ch.Id == a + 100 || ch.Id == a + 104)
            .LoadWith(ch => ch.Parent!, EagerLoadMode.SingleQuery)
            .OrderBy(ch => ch.Id)
            .ToList();

        children.Single(ch => ch.Id == a + 100).Parent!.RowId.Should().Be(a);
        children.Single(ch => ch.Id == a + 104).Parent.Should().BeNull("a dangling composite key resolves to null");
    }

    // ---------------------------------------------------------------------------------------------
    // D5 terminal matrix, scalar terminals and plan identity at the live boundary. Authored here and
    // executed by the DO→CHECK boundary run; never part of the D5 inner loop.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task MetadataLoadWith_Terminal_ListListAsyncArrayArrayAsync_MaterialiseTheGraph()
    {
        var ctx = _sut.DataProvider;
        MetadataBulkFixture.Register(ctx);
        var (first, _) = SeedMetadataBulkParents(1);

        EntityBuilder<MetadataBulkParent> Builder() => ctx.From<MetadataBulkParent>()
            .Where(parent => parent.Id == first)
            .LoadWith(parent => parent.Children);

        var list = Builder().ToList();
        list.Should().ContainSingle();
        list[0].Children.Should().ContainSingle("the sync list terminal loads the metadata graph");

        var listAsync = await Builder().ToListAsync();
        listAsync.Should().ContainSingle();
        listAsync[0].Children.Should().ContainSingle("the async list terminal loads the metadata graph");

        var array = Builder().ToArray();
        array.Should().ContainSingle();
        array[0].Children.Should().ContainSingle("the sync array terminal loads the metadata graph");

        var arrayAsync = await Builder().ToArrayAsync();
        arrayAsync.Should().ContainSingle();
        arrayAsync[0].Children.Should().ContainSingle("the async array terminal loads the metadata graph");
    }

    [Fact]
    public void MetadataLoadWith_Terminal_AnyCountStayScalar_NoChildCommand()
    {
        var ctx = _sut.DataProvider;
        MetadataBulkFixture.Register(ctx);
        var (first, _) = SeedMetadataBulkParents(1);

        var anyInterceptor = CountQueries(ctx);
        ctx.From<MetadataBulkParent>()
            .Where(parent => parent.Id == first)
            .LoadWith(parent => parent.Children)
            .Any()
            .Should().BeTrue();
        anyInterceptor.Executing.Should().Be(1, "Any stays scalar: the parent EXISTS command only, no child load");

        var countInterceptor = CountQueries(ctx);
        ctx.From<MetadataBulkParent>()
            .Where(parent => parent.Id == first)
            .LoadWith(parent => parent.Children)
            .Count()
            .Should().Be(1);
        countInterceptor.Executing.Should().Be(1, "Count stays scalar: one aggregate command, no child load");
    }

    [Fact]
    public void MetadataLoadWith_Cache_ColdWarmPrepared_ReobservesTheSameRows()
    {
        var ctx = _sut.DataProvider;
        MetadataBulkFixture.Register(ctx);
        var (first, _) = SeedMetadataBulkParents(1);

        EntityBuilder<MetadataBulkParent> Builder() => ctx.From<MetadataBulkParent>()
            .Where(parent => parent.Id == first)
            .LoadWith(parent => parent.Children);

        var cold = Builder().ToList();
        var warm = Builder().ToList();
        var prepared = Builder().ToList();

        cold.Select(parent => parent.Id).Should().Equal(warm.Select(parent => parent.Id));
        cold.Select(parent => parent.Id).Should().Equal(prepared.Select(parent => parent.Id));
        cold[0].Children.Select(child => child.Id).Should().Equal(warm[0].Children.Select(child => child.Id));
        cold[0].Children.Select(child => child.Id).Should().Equal(prepared[0].Children.Select(child => child.Id));

        var coldCommand = Builder().ToCommand();
        var preparedCold = ctx.GetPreparedQueryCommand(coldCommand, createEnumerator: false, storeInCache: true, CancellationToken.None);
        var preparedCommand = Builder().ToCommand();
        var preparedWarm = ctx.GetPreparedQueryCommand(preparedCommand, createEnumerator: false, storeInCache: true, CancellationToken.None);

        preparedWarm.Should().BeSameAs(preparedCold, "an equivalent metadata declaration reuses the prepared plan");
    }

    [Fact]
    public void MetadataLoadWith_Cache_ExplicitAndInferred_RunTheSameStatements()
    {
        var ctx = _sut.DataProvider;
        MetadataBulkFixture.Register(ctx);
        var (first, _) = SeedMetadataBulkParents(1);

        var inferredCommand = ctx.From<MetadataBulkParent>()
            .Where(parent => parent.Id == first)
            .LoadWith(parent => parent.Children)
            .ToCommand();
        var explicitCommand = ctx.From<MetadataBulkParent>()
            .Where(parent => parent.Id == first)
            .LoadWith(parent => parent.Children, child => child.From<MetadataBulkChild>(), parent => parent.Id, child => child.ParentId)
            .ToCommand();

        explicitCommand.EagerLoadShape.Equals(inferredCommand.EagerLoadShape)
            .Should().BeTrue("the explicit keys select exactly the declared 1:N relationship");

        var inferred = ctx.GetPreparedQueryCommand(inferredCommand, createEnumerator: false, storeInCache: true, CancellationToken.None);
        var explicitPrepared = ctx.GetPreparedQueryCommand(explicitCommand, createEnumerator: false, storeInCache: true, CancellationToken.None);
        explicitPrepared.Should().BeSameAs(inferred, "both frontends key the same plan entry");

        var recorder = new StatementRecorder();
        ((DataContext)ctx).AddInterceptor(recorder);

        ctx.From<MetadataBulkParent>()
            .Where(parent => parent.Id == first)
            .LoadWith(parent => parent.Children)
            .ToList()
            .Should().ContainSingle();
        ctx.From<MetadataBulkParent>()
            .Where(parent => parent.Id == first)
            .LoadWith(parent => parent.Children, child => child.From<MetadataBulkChild>(), parent => parent.Id, child => child.ParentId)
            .ToList()
            .Should().ContainSingle();

        recorder.Statements.Should().HaveCount(4, "each list terminal issues the parent and one child command");
        recorder.Statements[0].Should().Be(recorder.Statements[2], "the inferred parent command and the explicit parent command are the same statement");
        recorder.Statements[1].Should().Be(recorder.Statements[3], "the inferred child command and the explicit child command are the same statement");
    }

    private sealed class StatementRecorder : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
            => Statements.Add(command.CommandText);
    }

    // ---------------------------------------------------------------------------------------------
    // Seeding helpers. The bulk ids come from the shared decreasing counters, so tests never collide.
    // ---------------------------------------------------------------------------------------------

    private static CountingQueryInterceptor CountQueries(IDataContext context)
    {
        var interceptor = new CountingQueryInterceptor();
        ((DataContext)context).AddInterceptor(interceptor);
        return interceptor;
    }

    private (int First, int Last) SeedMetadataBulkParents(int count)
    {
        var ctx = _sut.DataProvider;
        MetadataBulkFixture.Register(ctx);

        var start = NextEagerBulkBase();
        var parents = new List<MetadataBulkParent>(count);
        var children = new List<MetadataBulkChild>(count);
        for (var i = 0; i < count; i++)
        {
            parents.Add(new MetadataBulkParent { Id = start - i, Name = "p" + i });
            // The child ids stay inside this call's own bulk slot (the parent ids are in another table, so
            // they cannot collide). A far offset such as -500_000 would land exactly on the slot of a
            // neighbouring bulk call (the bases are 100_000 apart) and make two tests insert the same
            // eager_child ids.
            children.Add(new MetadataBulkChild { Id = start - 1_000 - i, ParentId = start - i, Name = "c" + i });
        }

        InsertEagerRows(ctx, parents);
        InsertEagerRows(ctx, children);

        return (start - count + 1, start);
    }

    private (int First, int Last) SeedMetadataBulkCompositeParents(int count)
    {
        var ctx = _sut.DataProvider;
        JoinIntoCompositeKeyFixtures.RegisterDeclared(ctx);

        var start = NextEagerBulkBase();
        var childStart = NextEagerBulkBase();
        var tenant = start;

        var parents = new List<T251DeclaredManyParent>(count);
        var children = new List<T251DeclaredChild>(count);
        for (var i = 0; i < count; i++)
        {
            parents.Add(new T251DeclaredManyParent { RowId = start - i, Tenant = tenant, LinkId = start - i });
            children.Add(new T251DeclaredChild
            {
                Id = childStart - i,
                Tenant = tenant,
                ParentId = start - i,
                SortKey = i,
            });
        }

        InsertEagerRows(ctx, parents);
        InsertEagerRows(ctx, children);

        return (start - count + 1, start);
    }
}

/// <summary>Single-component eager fixture on the shared eager tables (metadata-driven declaration).</summary>
[SqlTable("eager_parent")]
internal sealed class MetadataBulkParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<MetadataBulkChild> Children { get; set; } = new List<MetadataBulkChild>();
}

/// <summary>Single-component eager child on the shared eager tables.</summary>
[SqlTable("eager_child")]
internal sealed class MetadataBulkChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

/// <summary>Registers the single-component eager graph through the public fluent surface.</summary>
internal static class MetadataBulkFixture
{
    internal static void Register(IDataContext ctx)
        => ctx.From<MetadataBulkParent>((EntityMetadataBuilder<MetadataBulkParent> builder) => builder
            .HasMany(parent => parent.Children, child => new { child.ParentId }, parent => new { parent.Id }));
}

/// <summary>Two-level nested eager fixture on the shared <c>eager_parent</c>/<c>eager_child</c> tables.</summary>
[SqlTable("eager_parent")]
internal sealed class MetadataNestedParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<MetadataNestedChild> Children { get; set; } = new List<MetadataNestedChild>();
}

[SqlTable("eager_child")]
internal sealed class MetadataNestedChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<MetadataNestedGrandChild> GrandChildren { get; set; } = new List<MetadataNestedGrandChild>();
}

[SqlTable("eager_child")]
internal sealed class MetadataNestedGrandChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

internal static class MetadataNestedFixture
{
    internal static void Register(IDataContext ctx)
    {
        ctx.From<MetadataNestedGrandChild>();
        ctx.From<MetadataNestedChild>((EntityMetadataBuilder<MetadataNestedChild> builder) => builder
            .HasMany(child => child.GrandChildren, grand => new { grand.ParentId }, child => new { child.Id }));
        ctx.From<MetadataNestedParent>((EntityMetadataBuilder<MetadataNestedParent> builder) => builder
            .HasMany(parent => parent.Children, child => new { child.ParentId }, parent => new { parent.Id }));
    }
}

/// <summary>Parent with two declared many-to-many collections over the same composite junction table.</summary>
[SqlTable("eager_cj_parent")]
internal sealed class MetadataTwoCollectionsParent
{
    [Key]
    [Column("row_id")]
    public int RowId { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("link_id")]
    public int LinkId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<JiCompositeJunctionChild> PrimaryChildren { get; set; } = new List<JiCompositeJunctionChild>();

    public ICollection<JiCompositeJunctionChild> SecondaryChildren { get; set; } = new List<JiCompositeJunctionChild>();

    internal static void Register(IDataContext ctx)
        => ctx.From<MetadataTwoCollectionsParent>((EntityMetadataBuilder<MetadataTwoCollectionsParent> builder) => builder
            .HasManyThrough<JiCompositeJunctionChild, JiCompositeJunctionLink>(
                parent => parent.PrimaryChildren,
                parent => new { parent.Tenant, parent.LinkId },
                link => new { link.ParentTenant, link.ParentLinkId },
                child => new { child.Id },
                link => new { link.ChildId })
            .HasManyThrough<JiCompositeJunctionChild, JiCompositeJunctionLink>(
                parent => parent.SecondaryChildren,
                parent => new { parent.Tenant, parent.LinkId },
                link => new { link.ParentTenant, link.ParentLinkId },
                child => new { child.Id },
                link => new { link.ChildId }));
}
