using System.Data.Common;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T331 AC03-AC07 + AC10 on the core in-memory path: the navigation-first forms derive their <c>ON</c>
/// from the declared relationship (ordered, positional, componentwise) and treat the optional predicate as
/// an <b>additional</b> conjunct.
/// <para>
/// The oracles are deliberately adversarial: the AC05 fixture points every <c>OtherId</c> at the opposite
/// parent (so a filter that replaced the declared-key equality would still match and the test would fail),
/// and the AC03 composite fixture is built so that positional pairing, first-key-only pairing and
/// name/natural pairing produce three <b>different</b> parent-to-children maps.
/// </para>
/// <para>
/// Runs in the "Query cache controls" collection because it writes the process-wide
/// <see cref="DataContextCache"/>.
/// </para>
/// </summary>
[Collection("Query cache controls")]
[Trait("Category", "T331")]
public class T331JoinIntoRelationshipTests
{
    public T331JoinIntoRelationshipTests()
    {
        DataContextCache.Clear();
    }

    // ---------------------------------------------------------------------------------------------
    // Fixtures — one-to-many / one-to-one (single key)
    // ---------------------------------------------------------------------------------------------

    public sealed class RelParent
    {
        public int Id { get; set; }

        public int TenantId { get; set; }

        public ICollection<RelChild> Children { get; set; } = new List<RelChild>();

        public RelDetail? Detail { get; set; }
    }

    public sealed class RelChild
    {
        public int Id { get; set; }

        public int ParentId { get; set; }

        /// <summary>A second, unrelated child column: the AC05 filter target.</summary>
        public int OtherId { get; set; }
    }

    public sealed class RelDetail
    {
        public int Id { get; set; }

        public int ParentId { get; set; }
    }

    /// <summary>Parent with a navigation but no declared relationship.</summary>
    public sealed class UnregisteredParent
    {
        public int Id { get; set; }

        public ICollection<RelChild> Children { get; set; } = new List<RelChild>();

        public RelDetail? Detail { get; set; }
    }

    // ---------------------------------------------------------------------------------------------
    // Fixtures — nullable foreign keys (AC10)
    // ---------------------------------------------------------------------------------------------

    public sealed class NullParent
    {
        public int Id { get; set; }

        public ICollection<NullChild> Children { get; set; } = new List<NullChild>();

        public NullDetail? Detail { get; set; }
    }

    public sealed class NullChild
    {
        public int Id { get; set; }

        public int? ParentId { get; set; }
    }

    public sealed class NullDetail
    {
        public int Id { get; set; }

        public int? ParentId { get; set; }
    }

    // ---------------------------------------------------------------------------------------------
    // Fixtures — composite keys (AC03) and junction (AC07)
    // ---------------------------------------------------------------------------------------------

    public sealed class Pair
    {
        public int TenantId { get; set; }

        public int Id { get; set; }

        public ICollection<PairChild> Children { get; set; } = new List<PairChild>();
    }

    /// <summary>Composite-key parent whose junction navigation is declared, not a mapped column.</summary>
    public sealed class CjParent
    {
        public int TenantId { get; set; }

        public int Id { get; set; }

        public ICollection<JTag> Tags { get; set; } = new List<JTag>();
    }

    public sealed class PairChild
    {
        public int Id { get; set; }

        public int TenantId { get; set; }

        public int ParentId { get; set; }
    }

    // ---------------------------------------------------------------------------------------------
    // Fixtures — many-to-many junction (AC07)
    // ---------------------------------------------------------------------------------------------

    public sealed class JParent
    {
        public int Id { get; set; }

        public ICollection<JTag> Tags { get; set; } = new List<JTag>();
    }

    public sealed class JTag
    {
        public int Id { get; set; }
    }

    public sealed class JLink
    {
        public int ParentId { get; set; }

        public int TagId { get; set; }
    }

    public sealed class CjLink
    {
        public int TenantId { get; set; }

        public int ParentId { get; set; }

        public int TagId { get; set; }
    }

    // ---------------------------------------------------------------------------------------------
    // Context factories
    // ---------------------------------------------------------------------------------------------

    /// <summary>AC04/AC05/AC06 fixture: 1-&gt;[10,12], 2-&gt;[11], 3-&gt;[] with <c>OtherId == ParentId</c>.</summary>
    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<RelParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasOneToOne(p => p.Detail, p => p.Id, d => d.ParentId))
            .WithData([
                new RelParent { Id = 1 },
                new RelParent { Id = 2 },
                new RelParent { Id = 3 },
            ]);
        ctx.From<RelChild>().WithData([
            new RelChild { Id = 10, ParentId = 1, OtherId = 1 },
            new RelChild { Id = 11, ParentId = 2, OtherId = 2 },
            new RelChild { Id = 12, ParentId = 1, OtherId = 1 },
        ]);
        ctx.From<RelDetail>().WithData([
            new RelDetail { Id = 100, ParentId = 1 },
            new RelDetail { Id = 101, ParentId = 2 },
        ]);
        return ctx;
    }

    private static InMemoryDataContext CreateNullableContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<NullParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId, p => (int?)p.Id)
                .HasOneToOne(p => p.Detail, p => (int?)p.Id, d => d.ParentId))
            .WithData([
                new NullParent { Id = 1 },
                new NullParent { Id = 2 },
            ]);
        ctx.From<NullChild>().WithData([
            new NullChild { Id = 10, ParentId = 1 },
            // Two null foreign keys: a null component must never manufacture a relationship, not even a
            // "null == null" one between the two children.
            new NullChild { Id = 11, ParentId = null },
            new NullChild { Id = 12, ParentId = null },
        ]);
        ctx.From<NullDetail>().WithData([
            new NullDetail { Id = 50, ParentId = 1 },
            new NullDetail { Id = 60, ParentId = 2 },
            new NullDetail { Id = 51, ParentId = null },
        ]);
        return ctx;
    }

    private static void RegisterComposite()
    {
        var pair = new EntityMetadataBuilder<Pair>();
        pair.HasMany(p => p.Children, c => new { c.TenantId, c.ParentId }, p => new { p.Id, p.TenantId });
        DataContextCache.Metadata[typeof(Pair)] = pair.Build();
        DataContextCache.Metadata[typeof(PairChild)] = new EntityMetadataBuilder<PairChild>().Build();
    }

    private static InMemoryDataContext CreateCompositeContext()
    {
        RegisterComposite();
        var ctx = new InMemoryDataContext();
        ctx.From<Pair>().WithData([
            new Pair { TenantId = 1, Id = 20 },
            new Pair { TenantId = 2, Id = 10 },
        ]);
        ctx.From<PairChild>().WithData([
            // c1 matches ONLY under positional pairing (see the AC03 table below).
            new PairChild { Id = 1, TenantId = 20, ParentId = 1 },
            // c2 matches ONLY under name/natural pairing.
            new PairChild { Id = 2, TenantId = 2, ParentId = 10 },
            // c3 matches ONLY under name/natural pairing.
            new PairChild { Id = 3, TenantId = 1, ParentId = 20 },
            // c4 matches ONLY under first-key-only pairing: it shares the first component (a parent id)
            // and is rejected only when the second component is also compared.
            new PairChild { Id = 4, TenantId = 10, ParentId = 99 },
        ]);
        return ctx;
    }

    private static InMemoryDataContext CreateJunctionContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<JParent>(b => b.HasManyThrough<JTag, JLink>(
                p => p.Tags,
                p => p.Id,
                l => l.ParentId,
                t => t.Id,
                l => l.TagId))
            .WithData([
                new JParent { Id = 1 },
                new JParent { Id = 2 },
            ]);
        ctx.From<JTag>().WithData([
            new JTag { Id = 10 },
            new JTag { Id = 20 },
        ]);
        ctx.From<JLink>().WithData([
            new JLink { ParentId = 1, TagId = 10 },
            new JLink { ParentId = 1, TagId = 20 },
        ]);
        return ctx;
    }

    private static List<string> Shape(IEnumerable<RelParent> parents)
        => parents.OrderBy(p => p.Id)
            .Select(p => $"{p.Id}:{string.Join(",", p.Children.OrderBy(c => c.Id).Select(c => c.Id))}")
            .ToList();

    // ---------------------------------------------------------------------------------------------
    // AC04 — declared-key ON, with and without a key-restating filter.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AC04_NavigationOnly_ShouldProduceTheDeclaredMatchesWithoutDuplicates()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<RelParent>().JoinInto(p => p.Children).ToList();

        Shape(parents).Should().Equal(new[] { "1:10,12", "2:11", "3:" },
            "1->[10,12] and 2->[11], each child exactly once, and the childless parent retained");
        parents.SelectMany(p => p.Children).Should().HaveCount(3);
    }

    [Fact]
    public void AC04_KeyRestatingFilter_ShouldProduceTheSameOrderedResultAsNoFilter()
    {
        using var ctx = CreateContext();

        var filtered = ctx.From<RelParent>()
            .JoinInto(p => p.Children, (p, c) => p.Id == c.ParentId)
            .ToList();

        Shape(filtered).Should().Equal(new[] { "1:10,12", "2:11", "3:" },
            "a filter that restates the declared keys adds no restriction and removes no child");
        filtered.SelectMany(p => p.Children).Should().HaveCount(3, "no child may be attached twice");
    }

    // ---------------------------------------------------------------------------------------------
    // AC05 — the filter is an extra conjunct, never a replacement for the declared-key ON.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AC05_EquivalentOtherIdFilter_ShouldMatchTheDeclaredResult()
    {
        // OtherId == ParentId for every child, so an extra `p.Id == c.OtherId` conjunct is satisfied by
        // exactly the declared matches: the result must equal AC04.
        using var ctx = CreateContext();

        var parents = ctx.From<RelParent>()
            .JoinInto(p => p.Children, (p, c) => p.Id == c.OtherId)
            .ToList();

        Shape(parents).Should().Equal("1:10,12", "2:11", "3:");
    }

    [Fact]
    public void AC05_ConflictingOtherIdFilter_ShouldYieldOnlyEmptyCollectionsAndKeepEveryParent()
    {
        // Every OtherId points at the OPPOSITE parent: the children still satisfy the declared-key ON, so
        // a filter that replaced it would keep 1->[10,12] and 2->[11]. As an extra conjunct it matches
        // nothing, and both parents must survive with empty collections.
        using var ctx = new InMemoryDataContext();
        ctx.From<RelParent>(b => b.HasMany(p => p.Children, c => c.ParentId))
            .WithData([new RelParent { Id = 1 }, new RelParent { Id = 2 }]);
        ctx.From<RelChild>().WithData([
            new RelChild { Id = 10, ParentId = 1, OtherId = 2 },
            new RelChild { Id = 11, ParentId = 2, OtherId = 1 },
            new RelChild { Id = 12, ParentId = 1, OtherId = 2 },
        ]);

        var parents = ctx.From<RelParent>()
            .JoinInto(p => p.Children, (p, c) => p.Id == c.OtherId)
            .ToList();

        Shape(parents).Should().Equal(new[] { "1:", "2:" },
            "the declared-key ON stays in place and the conflicting conjunct matches nothing");
    }

    // ---------------------------------------------------------------------------------------------
    // AC06 — a child-only filter narrows matches without dropping parents.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AC06_ChildOnlyFilter_ShouldKeepParentOneEmptyAndParentTwoWithEleven()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<RelParent>()
            .JoinInto(p => p.Children, (p, c) => c.Id == 11)
            .ToList();

        Shape(parents).Should().Equal(new[] { "1:", "2:11", "3:" },
            "parent 1 keeps no child but must not disappear; parent 2 must not duplicate child 11");
        parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(11);
        parents.Single(p => p.Id == 1).Children.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------------------------
    // AC03 — composite keys: every ordered component, positionally paired.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AC03_CompositeKeys_ShouldPairPositionallyAndRejectEveryCrossPair()
    {
        // Declaration: HasMany(Children, c => new { c.TenantId, c.ParentId }, p => new { p.Id, p.TenantId })
        // => PrincipalKey = [Id, TenantId], ForeignKey = [TenantId, ParentId], paired by position:
        // ON = p.Id == c.TenantId AND p.TenantId == c.ParentId.
        //
        // Parents: p1 = (Tenant 1, Id 20), p2 = (Tenant 2, Id 10).
        //
        // | child | (TenantId, ParentId) | positional: Id=TenantId AND Tenant=ParentId | first-key-only: Id=TenantId | name/natural: Tenant=TenantId AND Id=ParentId |
        // |-------|----------------------|---------------------------------------------|----------------------------------|--------------------------------------------|
        // | c1    | (20, 1)              | p1                                          | p1                               | none                                       |
        // | c2    | (2, 10)              | none                                        | none                             | p2                                         |
        // | c3    | (1, 20)              | none                                        | none                             | p1                                         |
        // | c4    | (10, 99)             | none (shares only the first component)      | p2                               | none                                       |
        //
        // positional => p1:[c1], p2:[]   natural => p1:[c3], p2:[c2]   first-key-only => p1:[c1], p2:[c4].
        // The three are pairwise different; the asserted result is the POSITIONAL one, i.e. c2, c3 and c4
        // are all rejected, so neither first-component-only nor name-based pairing can satisfy this test.
        using var ctx = CreateCompositeContext();

        var parents = ctx.From<Pair>().JoinInto(p => p.Children).ToList();

        parents.Should().HaveCount(2);
        parents.Single(p => p.TenantId == 1 && p.Id == 20).Children.Select(c => c.Id).Should().Equal(1);
        parents.Single(p => p.TenantId == 2 && p.Id == 10).Children.Should().BeEmpty(
            "c2 would be grouped by a name/natural pairing and is rejected by positional pairing");
    }

    [Fact]
    public void AC03_CompositeKeys_ShouldRenderOneEqualityPerComponentInDeclarationOrder()
    {
        RegisterComposite();
        using var ctx = new RootProjectionTests.TestContext();

        var sql = ((DbPreparedQueryCommand<Pair>)ctx.GetPreparedQueryCommand(
                ctx.From<Pair>().JoinInto(p => p.Children).ToCommand(),
                createEnumerator: false,
                storeInCache: false,
                TestContext.Current.CancellationToken))
            .DbCommand.CommandText;

        // PrincipalKey = [Id, TenantId], ForeignKey = [TenantId, ParentId]: the first pair is
        // id -> tenant id, the second is tenant -> parent id, in selector order.
        sql.Should().Contain("t1.Id = t2.TenantId", "the first declared component pair is id -> tenant id");
        sql.Should().Contain("t1.TenantId = t2.ParentId", "the second declared component pair is tenant -> parent id");
        sql.IndexOf("t1.Id = t2.TenantId", StringComparison.Ordinal)
            .Should().BeLessThan(
                sql.IndexOf("t1.TenantId = t2.ParentId", StringComparison.Ordinal),
                "the equalities keep the declaration order");
        sql.Should().Contain("left join", "the navigation-first forms are always left joins");
    }

    // ---------------------------------------------------------------------------------------------
    // AC10 — left-join preservation, filtered references, null key components.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AC10_FilteredOutReference_ShouldKeepTheParentWithANullReference()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<RelParent>()
            .JoinInto(p => p.Detail, (p, d) => d.Id != 101)
            .ToList();

        parents.Should().HaveCount(3, "the filtered-out reference must not remove its parent");
        parents.Single(p => p.Id == 1).Detail!.Id.Should().Be(100);
        parents.Single(p => p.Id == 2).Detail.Should().BeNull("its only child is filtered out by the extra conjunct");
        parents.Single(p => p.Id == 3).Detail.Should().BeNull();
    }

    [Fact]
    public void AC10_MissingChildAndNullKeys_ShouldPreserveTheParentWithoutPhantomChildren()
    {
        using var ctx = CreateNullableContext();

        var parents = ctx.From<NullParent>()
            .JoinInto(p => p.Children)
            .JoinInto(p => p.Detail)
            .ToList();

        parents.Should().HaveCount(2);
        parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10);
        parents.Single(p => p.Id == 1).Detail!.Id.Should().Be(50);
        parents.Single(p => p.Id == 2).Children.Should().BeEmpty(
            "the null foreign keys of children 11 and 12 must not manufacture a match");
        parents.Single(p => p.Id == 2).Detail!.Id.Should().Be(60);
    }

    [Fact]
    public void AC10_FilteredOutNullableReference_ShouldBeNullAndKeepTheParent()
    {
        using var ctx = CreateNullableContext();

        var parents = ctx.From<NullParent>()
            .JoinInto(p => p.Detail, (p, d) => d.Id == 50)
            .ToList();

        parents.Should().HaveCount(2);
        parents.Single(p => p.Id == 1).Detail!.Id.Should().Be(50);
        parents.Single(p => p.Id == 2).Detail.Should().BeNull("a filtered-out left edge leaves no phantom default child");
    }

    // ---------------------------------------------------------------------------------------------
    // AC07 — cardinality / junction handling and fail-closed metadata.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AC07_WrongCardinality_ShouldFailClosedInsteadOfBecomingADirectRelationship()
    {
        using var ctx = CreateContext();

        // A one-to-many navigation forced into the reference form.
        FluentActions.Invoking(() => ctx.From<RelParent>().JoinInto<ICollection<RelChild>>(p => p.Children))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*use the collection JoinInto overload*");

        // A one-to-one relationship offered to the collection form through a local configuration.
        FluentActions.Invoking(() => ctx.From<UnregisteredParent>()
                .JoinInto(
                    p => p.Children,
                    (p, c) => true,
                    j => j.OneToOne<UnregisteredParent, RelChild, int>(p => p.Id, c => c.ParentId)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*must be OneToMany or ManyToMany*");
    }

    [Fact]
    public void AC07_AbsentOrMalformedMetadata_ShouldNotFallBackToAConventionalJoin()
    {
        using var absent = new InMemoryDataContext();
        absent.From<UnregisteredParent>().WithData([new UnregisteredParent { Id = 1 }]);
        absent.From<RelChild>().WithData([new RelChild { Id = 10, ParentId = 1 }]);
        absent.From<RelDetail>().WithData([new RelDetail { Id = 100, ParentId = 1 }]);

        FluentActions.Invoking(() => absent.From<UnregisteredParent>().JoinInto(p => p.Children))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*requires an explicitly declared relationship*");

        FluentActions.Invoking(() => absent.From<UnregisteredParent>().JoinInto(p => p.Detail))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*requires an explicitly declared one-to-one relationship*");

        // Malformed: a two-component principal and a one-component foreign key. The declaration must be
        // rejected by the shared validator instead of joining on whatever components happen to be readable.
        var parentProperties = new EntityMetadataBuilder<RelParent>().Build().Properties;
        var childProperties = new EntityMetadataBuilder<RelChild>().Build().Properties;
        var navigation = typeof(RelParent).GetProperty(nameof(RelParent.Children))!;

        DataContextCache.Metadata[typeof(RelParent)] = new EntityMetadata(
            typeof(RelParent).Name,
            parentProperties.Where(p => p.PropertyInfo != navigation).ToList(),
            relationships:
            [
                new MalformedArityRelationship(
                    declaringType: typeof(RelParent),
                    relatedType: typeof(RelChild),
                    navigation: navigation,
                    principalKey: [.. parentProperties.Where(p => p.PropertyInfo.Name is nameof(RelParent.Id) or nameof(RelParent.TenantId))],
                    foreignKey: [.. childProperties.Where(p => p.PropertyInfo.Name is nameof(RelChild.ParentId))]),
            ]);

        using var malformed = new InMemoryDataContext();
        malformed.From<RelChild>().WithData([new RelChild { Id = 10, ParentId = 1 }]);

        FluentActions.Invoking(() => malformed.From<RelParent>().JoinInto(p => p.Children))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*arities must match*");
    }

    [Fact]
    public void AC07_JunctionFilter_ShouldStayAnAdditionalConjunctOnTheChildEdge()
    {
        using var ctx = CreateJunctionContext();

        var unfiltered = ctx.From<JParent>().JoinInto(p => p.Tags).ToList();
        unfiltered.OrderBy(p => p.Id)
            .Select(p => $"{p.Id}:{string.Join(",", p.Tags.OrderBy(t => t.Id).Select(t => t.Id))}")
            .Should().Equal(new[] { "1:10,20", "2:" },
                "the junction synthesis supplies both key edges; a parent without links is kept");

        var filtered = ctx.From<JParent>()
            .JoinInto(p => p.Tags, (p, t) => t.Id != 20)
            .ToList();
        filtered.OrderBy(p => p.Id)
            .Select(p => $"{p.Id}:{string.Join(",", p.Tags.OrderBy(t => t.Id).Select(t => t.Id))}")
            .Should().Equal(new[] { "1:10", "2:" },
                "the filter narrows the child set without replacing the declared junction edges");
    }

    [Fact]
    public void AC07_CompositeJunction_ShouldRouteBothLegsByEveryComponent()
    {
        var parent = new EntityMetadataBuilder<CjParent>();
        parent.Property(p => p.TenantId).Key();
        parent.Property(p => p.Id).Key();
        parent.HasManyThrough<JTag, CjLink>(
            p => p.Tags,
            b => new { b.TenantId, b.Id },
            l => new { l.TenantId, l.ParentId },
            t => t.Id,
            l => l.TagId);
        DataContextCache.Metadata[typeof(CjParent)] = parent.Build();
        DataContextCache.Metadata[typeof(JTag)] = new EntityMetadataBuilder<JTag>().Build();
        DataContextCache.Metadata[typeof(CjLink)] = new EntityMetadataBuilder<CjLink>().Build();

        using var ctx = new InMemoryDataContext();
        ctx.From<CjParent>().WithData([
            new CjParent { TenantId = 1, Id = 20 },
            new CjParent { TenantId = 2, Id = 10 },
        ]);
        ctx.From<JTag>().WithData([new JTag { Id = 10 }, new JTag { Id = 20 }]);
        ctx.From<CjLink>().WithData([
            new CjLink { TenantId = 1, ParentId = 20, TagId = 10 },
            // Shares the tenant with (1,20) but not the parent component: only componentwise pairing
            // rejects it (a first-component-only routing would attach tag 20 to the same parent).
            new CjLink { TenantId = 1, ParentId = 10, TagId = 20 },
        ]);

        var parents = ctx.From<CjParent>().JoinInto(p => p.Tags).ToList();

        parents.Should().HaveCount(2);
        parents.Single(p => p.TenantId == 1 && p.Id == 20).Tags.Select(t => t.Id).Should().Equal(10);
        parents.Single(p => p.TenantId == 2 && p.Id == 10).Tags.Should().BeEmpty();
    }

    /// <summary>A hand-written relationship whose principal/foreign arities disagree.</summary>
    private sealed class MalformedArityRelationship(
        Type declaringType,
        Type relatedType,
        PropertyInfo navigation,
        IReadOnlyList<IPropertyMetadata> principalKey,
        IReadOnlyList<IPropertyMetadata> foreignKey) : IRelationshipMetadata
    {
        public RelationshipKind Kind => RelationshipKind.OneToMany;

        public Type DeclaringType => declaringType;

        public Type RelatedType => relatedType;

        public PropertyInfo? Navigation => navigation;

        public bool IsCollection => true;

        public IReadOnlyList<IPropertyMetadata> ForeignKey => foreignKey;

        public IReadOnlyList<IPropertyMetadata> PrincipalKey => principalKey;

        public IRelationshipMetadata? Inverse => null;
    }
}
