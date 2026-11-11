using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 4, batch 2 — <c>src/nextorm.core/DataContext/Meta/Implementation/RelationshipResolver.cs</c> and
/// <c>RelationshipMetadata.cs</c> on live SQLite through the public navigation surface.
/// <para>
/// Accepted paths execute a real join: a navigation-first <c>JoinInto</c> for a one-to-many collection, a
/// many-to-one reference, a one-to-one reference, a many-to-many through an explicit junction and a
/// composite-key collection, plus the declared-metadata <c>LoadWith</c> (Include-like) split load for the
/// collection, reference and junction shapes. Each asserts the computed rows — including the absent-parent
/// and empty-collection cases — not merely that the query ran.
/// </para>
/// <para>
/// Every refusal is a deterministic pre-SQL guard asserted by exception type plus the semantic marker the
/// resolver carries (navigation name, cardinality, key type), and the inverse-resolution facts read the
/// published <see cref="IEntityMetadata.Relationships"/> entries the same way a consumer of the metadata
/// contract would.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep4RelationshipResolverTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s4_nav_parent (id integer primary key, name text null);
        delete from s4_nav_parent;
        create table if not exists s4_nav_child (id integer primary key, parent_id int null, label text null);
        delete from s4_nav_child;
        create table if not exists s4_nav_profile (id integer primary key, parent_id int not null, bio text null);
        delete from s4_nav_profile;
        create table if not exists s4_nav_tag (id integer primary key, name text null);
        delete from s4_nav_tag;
        create table if not exists s4_nav_link (parent_id int not null, tag_id int not null);
        delete from s4_nav_link;
        create table if not exists s4_nav_comp_parent (tenant int not null, link_id int not null, name text null, primary key (tenant, link_id));
        delete from s4_nav_comp_parent;
        create table if not exists s4_nav_comp_child (id integer primary key, tenant int not null, parent_link int null, label text null);
        delete from s4_nav_comp_child;
        create table if not exists s4_nav_comp_link (tenant int not null, parent_link int not null, tag_id int not null);
        delete from s4_nav_comp_link;

        insert into s4_nav_parent (id, name) values (8001, 'p1'), (8002, 'p2'), (8003, 'p3');
        insert into s4_nav_child (id, parent_id, label) values
            (8101, 8001, 'a'), (8102, 8001, 'b'), (8103, 8002, 'c'), (8104, null, 'orphan');
        insert into s4_nav_profile (id, parent_id, bio) values (8201, 8001, 'bio1');
        insert into s4_nav_tag (id, name) values (8301, 't1'), (8302, 't2'), (8303, 't3');
        insert into s4_nav_link (parent_id, tag_id) values (8001, 8301), (8001, 8302), (8002, 8303);
        insert into s4_nav_comp_parent (tenant, link_id, name) values (1, 10, 'cp1'), (1, 20, 'cp2'), (2, 10, 'cp3');
        insert into s4_nav_comp_child (id, tenant, parent_link, label) values
            (8401, 1, 10, 'x'), (8402, 1, 10, 'y'), (8403, 1, 20, 'z'), (8404, 2, 10, 'other-tenant');
        insert into s4_nav_comp_link (tenant, parent_link, tag_id) values (1, 10, 8301), (1, 10, 8302), (2, 10, 8303);
        """;

    public SqliteStep4RelationshipResolverTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();

        // Dependents first: a principal-side collection provision resolves the related type's mapping,
        // and an auto-published mapping would shadow the configured one below.
        Context.From<S4NavChild>((EntityMetadataBuilder<S4NavChild> b) => b.HasOne(c => c.Parent, c => c.ParentId));
        Context.From<S4NavProfile>((EntityMetadataBuilder<S4NavProfile> b) => b.HasOne(p => p.Parent, p => p.ParentId));
        Context.From<S4NoInverseChild>((EntityMetadataBuilder<S4NoInverseChild> b) => b.HasOne(c => c.Parent, c => c.OtherId));
        Context.From<S4NavLink>();
        Context.From<S4NavTag>((EntityMetadataBuilder<S4NavTag> b) => b.HasManyThrough<S4NavParent, S4NavLink, int, int>(
            t => t.Parents,
            t => t.Id,
            l => l.TagId,
            p => p.Id,
            l => l.ParentId));
        Context.From<S4NavParent>((EntityMetadataBuilder<S4NavParent> b) => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasOneToOne(p => p.Profile, p => p.Id, pr => pr.ParentId)
            .HasManyThrough<S4NavTag, S4NavLink, int, int>(
                p => p.Tags,
                p => p.Id,
                l => l.ParentId,
                t => t.Id,
                l => l.TagId));

        Context.From<S4NavCompChild>((EntityMetadataBuilder<S4NavCompChild> b) => b.HasOne(
            c => c.Parent,
            c => new { c.Tenant, c.ParentLink },
            p => new { p.Tenant, p.LinkId }));
        Context.From<S4NavCompLink>();
        Context.From<S4NavCompParent>((EntityMetadataBuilder<S4NavCompParent> b) => b
            .HasMany(p => p.Children, c => new { c.Tenant, c.ParentLink }, p => new { p.Tenant, p.LinkId })
            .HasManyThrough<S4NavTag, S4NavCompLink>(
                p => p.Tags,
                p => new { p.Tenant, p.LinkId },
                l => new { l.Tenant, l.ParentLink },
                t => t.Id,
                l => l.TagId));
    }

    private IDataContext Context => _sut.DataProvider;

    // ---------------------------------------------------------------------------------------------
    // Accepted paths — real SQLite joins through the declared navigation metadata
    // ---------------------------------------------------------------------------------------------

    /// <summary>A collection navigation fills every parent, including the one with no child.</summary>
    [Fact]
    public void OneToMany_JoinInto_ShouldFillChildrenIncludingTheEmptyCollection()
    {
        var parents = Context.From<S4NavParent>()
            .JoinInto<S4NavChild>(p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(3);
        parents.Select(p => p.Name).Should().Equal("p1", "p2", "p3");
        parents[0].Children.Select(c => c.Label).Should().BeEquivalentTo(["a", "b"]);
        parents[1].Children.Select(c => c.Label).Should().Equal("c");
        parents[2].Children.Should().BeEmpty("parent 8003 owns no child, and 8104 has no parent at all");
    }

    /// <summary>The additional filter restricts children without removing the parent from the result.</summary>
    [Fact]
    public void OneToMany_JoinIntoWithFilter_ShouldKeepTheParentAndFilterTheChildren()
    {
        var parents = Context.From<S4NavParent>()
            .JoinInto<S4NavChild>(p => p.Children, (p, c) => c.Label == "b")
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(3);
        parents[0].Children.Select(c => c.Label).Should().Equal("b");
        parents[1].Children.Should().BeEmpty("the only child of p2 does not match the joined filter");
        parents[2].Children.Should().BeEmpty();
    }

    /// <summary>The declared-metadata <c>LoadWith</c> (Include-like) split load fills the collection.</summary>
    [Fact]
    public void OneToMany_LoadWith_ShouldSplitLoadTheCollection()
    {
        var parents = Context.From<S4NavParent>()
            .LoadWith<S4NavChild>(p => p.Children)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(3);
        parents[0].Children.Select(c => c.Label).Should().BeEquivalentTo(["a", "b"]);
        parents[2].Children.Should().BeEmpty();
    }

    /// <summary>A many-to-one navigation is not a one-to-one reference for the reference join route.</summary>
    [Fact]
    public void ManyToOne_JoinIntoReference_ShouldRefuseTheCardinality()
    {
        var act = () => Context.From<S4NavChild>()
            .JoinInto<S4NavParent>(c => c.Parent)
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*JoinInto with a reference navigation supports only a OneToOne relationship*S4NavChild.Parent*declares ManyToOne*");
    }

    /// <summary>The reference route of the declared-metadata eager load attaches the principal as well.</summary>
    [Fact]
    public void ManyToOne_LoadWithReference_ShouldAttachTheParent()
    {
        var children = Context.From<S4NavChild>()
            .Where(c => c.Id >= 8101)
            .LoadWith<S4NavParent>(c => c.Parent)
            .OrderBy(c => c.Id)
            .ToList();

        children.Should().HaveCount(4);
        children[0].Parent!.Name.Should().Be("p1");
        children[3].Parent.Should().BeNull();
    }

    /// <summary>A one-to-one reference attaches the single dependent row and stays null when absent.</summary>
    [Fact]
    public void OneToOne_JoinInto_ShouldAttachTheProfileOnly()
    {
        var parents = Context.From<S4NavParent>()
            .JoinInto<S4NavProfile>(p => p.Profile)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(3);
        parents[0].Profile.Should().NotBeNull();
        parents[0].Profile!.Bio.Should().Be("bio1");
        parents[1].Profile.Should().BeNull();
        parents[2].Profile.Should().BeNull();
    }

    /// <summary>A many-to-many navigation joins through the explicit junction table.</summary>
    [Fact]
    public void ManyToMany_JoinInto_ShouldFillTagsThroughTheJunction()
    {
        var parents = Context.From<S4NavParent>()
            .JoinInto<S4NavTag>(p => p.Tags)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(3);
        parents[0].Tags.Select(t => t.Name).Should().BeEquivalentTo(["t1", "t2"]);
        parents[1].Tags.Select(t => t.Name).Should().Equal("t3");
        parents[2].Tags.Should().BeEmpty();
    }

    /// <summary>The eager-load route of the same junction relationship returns the same tags.</summary>
    [Fact]
    public void ManyToMany_LoadWith_ShouldSplitLoadTheJunctionCollection()
    {
        var parents = Context.From<S4NavParent>()
            .Where(p => p.Id <= 8002)
            .LoadWith<S4NavTag>(p => p.Tags)
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(2);
        parents[0].Tags.Select(t => t.Name).Should().BeEquivalentTo(["t1", "t2"]);
        parents[1].Tags.Select(t => t.Name).Should().Equal("t3");
    }

    /// <summary>A composite principal key pairs every component positionally, never the first one only.</summary>
    [Fact]
    public void CompositeKey_JoinInto_ShouldPairTenantAndLink()
    {
        var parents = Context.From<S4NavCompParent>()
            .JoinInto<S4NavCompChild>(p => p.Children)
            .OrderBy(p => p.Tenant)
            .ThenBy(p => p.LinkId)
            .ToList();

        parents.Should().HaveCount(3);
        parents[0].Children.Select(c => c.Label).Should().BeEquivalentTo(["x", "y"]);
        parents[1].Children.Select(c => c.Label).Should().Equal("z");
        parents[2].Children.Select(c => c.Label).Should().Equal(new[] { "other-tenant" });
        // Only the (tenant 2, link 10) parent owns it: pairing by the first component alone would have
        // moved it to (1, 10).
    }

    /// <summary>A composite junction leg pairs both parent components with the link row.</summary>
    [Fact]
    public void CompositeJunction_JoinInto_ShouldFillTagsByTenant()
    {
        var parents = Context.From<S4NavCompParent>()
            .JoinInto<S4NavTag>(p => p.Tags)
            .OrderBy(p => p.Tenant)
            .ThenBy(p => p.LinkId)
            .ToList();

        parents.Should().HaveCount(3);
        parents[0].Tags.Select(t => t.Name).Should().BeEquivalentTo(["t1", "t2"]);
        parents[1].Tags.Should().BeEmpty();
        parents[2].Tags.Select(t => t.Name).Should().Equal("t3");
    }

    // ---------------------------------------------------------------------------------------------
    // Pre-SQL guards — exception type plus the semantic identifier the resolver carries
    // ---------------------------------------------------------------------------------------------

    /// <summary>A navigation without a declared relationship is rejected instead of inferred.</summary>
    [Fact]
    public void LoadWith_UndeclaredNavigation_ShouldRefuseBeforeSql()
    {
        Context.From<S4UndeclaredParent>()
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList()
            .Should().HaveCount(3, "the live table is read before the guard runs");

        var act = () => Context.From<S4UndeclaredParent>()
            .LoadWith<S4UndeclaredChild>(p => p.Children)
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*S4UndeclaredParent.Children*no relationship is declared for that navigation*");
    }

    /// <summary>
    /// A local junction configuration whose key type does not match the junction column is refused by the
    /// resolver before any SQL is rendered, naming both members and their CLR types.
    /// </summary>
    [Fact]
    public void ManyToMany_LocalOptions_WithMismatchedKeyType_ShouldRefuseBeforeSql()
    {
        Expression<Func<S4NavParent, ICollection<S4NavTag>>> navigation = p => p.Tags;

        var act = () => Context.From<S4NavParent>()
            .JoinInto(navigation, null, j => j.ManyToMany<S4NavParent, S4NavTag, S4NavLink, object, object>(
                p => p.Name!,
                l => l.ParentId,
                t => t.Name!,
                l => l.TagId))
            .ToList();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*parentKey 'Name'*does not match*junctionParentForeignKey 'ParentId'*");
    }

    /// <summary>A composite junction leg whose arity does not match its key is refused by the declaration.</summary>
    [Fact]
    public void HasManyThrough_WithMismatchedArity_ShouldRefuseBeforeSql()
    {
        var act = () => new EntityMetadataBuilder<S4ArityParent>()
            .HasManyThrough<S4ArityChild, S4ArityLink>(
                p => p.Children,
                p => new { p.Tenant, p.Id },
                l => l.ParentId,
                c => c.Id,
                l => l.ChildId)
            .Build();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*S4ArityParent*2 parentKey component(s) but 1 junctionParentForeignKey component(s)*same arity*");
    }

    // ---------------------------------------------------------------------------------------------
    // Inverse resolution — the published metadata contract read through IEntityMetadata.Relationships
    // ---------------------------------------------------------------------------------------------

    /// <summary>The inverse of a collection relationship is the declared many-to-one navigation.</summary>
    [Fact]
    public void Inverse_CollectionToReference_ShouldResolveBothSides()
    {
        Context.From<S4NavParent>().OrderBy(p => p.Id).Select(p => p.Id).ToList().Should().HaveCount(3);

        var collection = DataContextCache.Metadata[typeof(S4NavParent)].Relationships[0];
        var inverse = collection.Inverse;

        inverse.Should().NotBeNull();
        inverse!.DeclaringType.Should().Be(typeof(S4NavChild));
        inverse.RelatedType.Should().Be(typeof(S4NavParent));
        inverse.Kind.Should().Be(RelationshipKind.ManyToOne);
        inverse.ForeignKey[0].PropertyInfo.Name.Should().Be(nameof(S4NavChild.ParentId));
    }

    /// <summary>A one-to-one principal resolves the inverse many-to-one declared on the dependent.</summary>
    [Fact]
    public void Inverse_OneToOneToManyToOne_ShouldResolveAcrossCompatibleCardinality()
    {
        var oneToOne = DataContextCache.Metadata[typeof(S4NavParent)].Relationships[1];

        var inverse = oneToOne.Inverse;

        inverse.Should().NotBeNull();
        inverse!.DeclaringType.Should().Be(typeof(S4NavProfile));
        inverse.Kind.Should().Be(RelationshipKind.ManyToOne);
    }

    /// <summary>A symmetric many-to-many declaration resolves its inverse through the swapped junction legs.</summary>
    [Fact]
    public void Inverse_ManyToManySymmetric_ShouldResolveThroughTheJunction()
    {
        var manyToMany = DataContextCache.Metadata[typeof(S4NavParent)].Relationships[2];

        var inverse = manyToMany.Inverse;

        inverse.Should().NotBeNull();
        inverse!.DeclaringType.Should().Be(typeof(S4NavTag));
        inverse.Kind.Should().Be(RelationshipKind.ManyToMany);
        inverse.Junction.Should().NotBeNull();
    }

    /// <summary>When the related type was never mapped, the inverse is absent instead of guessed.</summary>
    [Fact]
    public void Inverse_UnregisteredRelatedType_ShouldBeNull()
    {
        Context.From<S4LonelyParent>((EntityMetadataBuilder<S4LonelyParent> b) => b
                .HasMany(p => p.Children, c => c.ParentId))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        var relationship = DataContextCache.Metadata[typeof(S4LonelyParent)].Relationships[0];

        relationship.Inverse.Should().BeNull("S4LonelyChild is not mapped in this process");
    }

    /// <summary>A related type whose navigation points at a different foreign key is not an inverse.</summary>
    [Fact]
    public void Inverse_WithDifferentForeignKey_ShouldBeNull()
    {
        Context.From<S4NoInverseParent>((EntityMetadataBuilder<S4NoInverseParent> b) => b
                .HasMany(p => p.Children, c => c.ParentId))
            .OrderBy(p => p.Id)
            .Select(p => p.Id)
            .ToList();

        var relationship = DataContextCache.Metadata[typeof(S4NoInverseParent)].Relationships[0];

        relationship.Inverse.Should().BeNull("the declared related navigation uses OtherId, so the key mappings differ");
    }

    /// <summary>Two navigations with the same canonical mapping make the inverse ambiguous.</summary>
    [Fact]
    public void Inverse_AmbiguousNavigations_ShouldRefuseWithBothNames()
    {
        Context.From<S4AmbiguousChild>((EntityMetadataBuilder<S4AmbiguousChild> b) => b
            .HasOne(c => c.Parent, c => c.ParentId)
            .HasOne(c => c.SecondParent, c => c.ParentId));
        Context.From<S4AmbiguousParent>((EntityMetadataBuilder<S4AmbiguousParent> b) => b
            .HasMany(p => p.Children, c => c.ParentId));

        var relationship = DataContextCache.Metadata[typeof(S4AmbiguousParent)].Relationships[0];

        var act = () => relationship.Inverse;

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*inverse of the relationship 'S4AmbiguousParent.Children' is ambiguous*S4AmbiguousChild.Parent*S4AmbiguousChild.SecondParent*");
    }

    // ---------------------------------------------------------------------------------------------
    // Marker entities
    // ---------------------------------------------------------------------------------------------

    /// <summary>Principal of the one-to-many / one-to-one / many-to-many facts.</summary>
    [SqlTable("s4_nav_parent")]
    public sealed class S4NavParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<S4NavChild> Children { get; } = new List<S4NavChild>();

        public S4NavProfile? Profile { get; set; }

        public ICollection<S4NavTag> Tags { get; } = new List<S4NavTag>();
    }

    /// <summary>Dependent of the collection and reference facts.</summary>
    [SqlTable("s4_nav_child")]
    public sealed class S4NavChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int? ParentId { get; set; }

        [Column("label")]
        public string? Label { get; set; }

        public S4NavParent? Parent { get; set; }
    }

    /// <summary>Dependent of the one-to-one fact.</summary>
    [SqlTable("s4_nav_profile")]
    public sealed class S4NavProfile
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("bio")]
        public string? Bio { get; set; }

        public S4NavParent? Parent { get; set; }
    }

    /// <summary>Related side of the many-to-many fact.</summary>
    [SqlTable("s4_nav_tag")]
    public sealed class S4NavTag
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<S4NavParent> Parents { get; } = new List<S4NavParent>();
    }

    /// <summary>Junction of the single-column many-to-many fact.</summary>
    [SqlTable("s4_nav_link")]
    public sealed class S4NavLink
    {
        [Key]
        [Column("parent_id")]
        public int ParentId { get; set; }

        [Key]
        [Column("tag_id")]
        public int TagId { get; set; }
    }

    /// <summary>Principal with a composite key.</summary>
    [SqlTable("s4_nav_comp_parent")]
    public sealed class S4NavCompParent
    {
        [Key]
        [Column("tenant")]
        public int Tenant { get; set; }

        [Key]
        [Column("link_id")]
        public int LinkId { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<S4NavCompChild> Children { get; } = new List<S4NavCompChild>();

        public ICollection<S4NavTag> Tags { get; } = new List<S4NavTag>();
    }

    /// <summary>Dependent with a composite foreign key.</summary>
    [SqlTable("s4_nav_comp_child")]
    public sealed class S4NavCompChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant")]
        public int Tenant { get; set; }

        [Column("parent_link")]
        public int? ParentLink { get; set; }

        [Column("label")]
        public string? Label { get; set; }

        public S4NavCompParent? Parent { get; set; }
    }

    /// <summary>Junction with a composite parent leg.</summary>
    [SqlTable("s4_nav_comp_link")]
    public sealed class S4NavCompLink
    {
        [Key]
        [Column("tenant")]
        public int Tenant { get; set; }

        [Key]
        [Column("parent_link")]
        public int ParentLink { get; set; }

        [Key]
        [Column("tag_id")]
        public int TagId { get; set; }
    }

    /// <summary>Principal whose related type is deliberately never mapped.</summary>
    [SqlTable("s4_nav_parent")]
    public sealed class S4LonelyParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        public ICollection<S4LonelyChild> Children { get; } = new List<S4LonelyChild>();
    }

    /// <summary>Unmapped related type of <see cref="S4LonelyParent"/>.</summary>
    public sealed class S4LonelyChild
    {
        [Key]
        public int Id { get; set; }

        public int ParentId { get; set; }
    }

    /// <summary>Principal of the mismatched-foreign-key fact.</summary>
    [SqlTable("s4_nav_parent")]
    public sealed class S4NoInverseParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        public ICollection<S4NoInverseChild> Children { get; } = new List<S4NoInverseChild>();
    }

    /// <summary>Related type whose declared navigation uses a different foreign key.</summary>
    [SqlTable("s4_nav_child")]
    public sealed class S4NoInverseChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        public int OtherId { get; set; }

        public S4NoInverseParent? Parent { get; set; }
    }

    /// <summary>Principal of the ambiguous-inverse fact.</summary>
    [SqlTable("s4_nav_parent")]
    public sealed class S4AmbiguousParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        public ICollection<S4AmbiguousChild> Children { get; } = new List<S4AmbiguousChild>();
    }

    /// <summary>Related type declaring two navigations that converge on the same canonical mapping.</summary>
    [SqlTable("s4_nav_child")]
    public sealed class S4AmbiguousChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        public S4AmbiguousParent? Parent { get; set; }

        public S4AmbiguousParent? SecondParent { get; set; }
    }

    /// <summary>Principal of the arity guard fact; never mapped to a live table.</summary>
    public sealed class S4ArityParent
    {
        [Key]
        public int Id { get; set; }

        public int Tenant { get; set; }

        public ICollection<S4ArityChild> Children { get; } = new List<S4ArityChild>();
    }

    /// <summary>Dependent of the arity guard fact.</summary>
    public sealed class S4ArityChild
    {
        [Key]
        public int Id { get; set; }

        public int ParentId { get; set; }
    }

    /// <summary>Junction with a single-component parent leg.</summary>
    public sealed class S4ArityLink
    {
        [Key]
        public int ParentId { get; set; }

        [Key]
        public int ChildId { get; set; }
    }

    /// <summary>Principal whose collection navigation is deliberately left undeclared.</summary>
    [SqlTable("s4_nav_parent")]
    public sealed class S4UndeclaredParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        public ICollection<S4UndeclaredChild> Children { get; } = new List<S4UndeclaredChild>();
    }

    /// <summary>Related type of the undeclared-navigation fact.</summary>
    [SqlTable("s4_nav_child")]
    public sealed class S4UndeclaredChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }
    }
}
