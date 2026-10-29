using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T249-D2 metadata binding: an additive <c>LoadWith</c> call consumes a <b>declared</b> relationship —
/// never a convention — validates its cardinality, related type and ordered key components against the
/// overload route, and only then constructs the eager-load specification. The rows here are unit-level:
/// they assert resolution and spec construction with fluent, attribute and hand-built (fake) composite
/// metadata, and that every malformed declaration fails closed before any SQL is built or a plan is
/// cached. Execution semantics belong to the D3/D4 suites and the SQL/live providers.
/// </summary>
[Collection("Query cache controls")]
public sealed class MetadataLoadWithMetadataTests
{
    public MetadataLoadWithMetadataTests() => DataContextCache.Clear();

    // ---------------------------------------------------------------------------------------------
    // Fixtures — composite keys are expressed through the T251 producer surface (fluent + attribute);
    // the fake-metadata rows inject a hand-built RelationshipMetadata so the consumer's arity-generality
    // is proved independently of what the producer can currently declare.
    // ---------------------------------------------------------------------------------------------

    public sealed class Ledger
    {
        [Key]
        public int TenantId { get; set; }

        [Key]
        public int Code { get; set; }

        public ICollection<LedgerEntry> Entries { get; set; } = new List<LedgerEntry>();
    }

    public sealed class LedgerEntry
    {
        [Key]
        public int Id { get; set; }

        public int TenantId { get; set; }

        public int OwnerCode { get; set; }
    }

    public sealed class TicketOwner
    {
        [Key]
        public int TenantId { get; set; }

        [Key]
        public int Id { get; set; }
    }

    public sealed class Ticket
    {
        [Key]
        public int Id { get; set; }

        public int OwnerTenantId { get; set; }

        public int OwnerId { get; set; }

        public TicketOwner? Owner { get; set; }
    }

    public sealed class ProfileParent
    {
        [Key]
        public int TenantId { get; set; }

        [Key]
        public int Id { get; set; }

        public Profile? Profile { get; set; }
    }

    public sealed class Profile
    {
        [Key]
        public int Id { get; set; }

        public int OwnerTenantId { get; set; }

        public int OwnerId { get; set; }
    }

    public sealed class MnParent
    {
        [Key]
        public int Id { get; set; }

        public ICollection<MnChild> Children { get; set; } = new List<MnChild>();
    }

    public sealed class MnChild
    {
        [Key]
        public int Id { get; set; }
    }

    public sealed class MnLink
    {
        [Key]
        public int Id { get; set; }

        public int ParentId { get; set; }

        public int ChildId { get; set; }
    }

    public sealed class AttrLedger
    {
        [Key]
        public int TenantId { get; set; }

        [Key]
        public int Code { get; set; }

        [Relationship(
            ForeignKeys = new[] { nameof(AttrEntry.TenantId), nameof(AttrEntry.OwnerCode) },
            PrincipalKeys = new[] { nameof(AttrLedger.TenantId), nameof(AttrLedger.Code) })]
        public ICollection<AttrEntry> Entries { get; set; } = new List<AttrEntry>();
    }

    public sealed class AttrEntry
    {
        [Key]
        public int Id { get; set; }

        public int TenantId { get; set; }

        public int OwnerCode { get; set; }

        [Relationship(
            ForeignKeys = new[] { nameof(AttrEntry.TenantId), nameof(AttrEntry.OwnerCode) },
            PrincipalKeys = new[] { nameof(AttrLedger.TenantId), nameof(AttrLedger.Code) })]
        public AttrLedger? Parent { get; set; }
    }

    public class BaseNode
    {
        [Key]
        public int Id { get; set; }

        public ICollection<Leaf> Leaves { get; set; } = new List<Leaf>();
    }

    public sealed class DerivedNode : BaseNode
    {
    }

    public sealed class Leaf
    {
        [Key]
        public int Id { get; set; }

        public int NodeId { get; set; }
    }

    public sealed class Employee
    {
        [Key]
        public int Id { get; set; }

        public int? ManagerId { get; set; }

        [Relationship(ForeignKey = nameof(ManagerId))]
        public Employee? Manager { get; set; }

        [Relationship(ForeignKey = nameof(ManagerId))]
        public ICollection<Employee> Reports { get; set; } = new List<Employee>();
    }

    public sealed class Party
    {
        [Key]
        public int Id { get; set; }
    }

    public sealed class Purchase
    {
        [Key]
        public int Id { get; set; }

        public int BuyerId { get; set; }

        public int SellerId { get; set; }

        [Relationship(ForeignKey = nameof(BuyerId))]
        public Party? Buyer { get; set; }

        [Relationship(ForeignKey = nameof(SellerId))]
        public Party? Seller { get; set; }
    }

    public sealed class PlainParent
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public ICollection<PlainChild> Children { get; set; } = new List<PlainChild>();
    }

    public sealed class PlainChild
    {
        [Key]
        public int Id { get; set; }

        public int ParentId { get; set; }
    }

    public sealed class FakeParent
    {
        [Key]
        public int TenantId { get; set; }

        [Key]
        public Guid Id { get; set; }

        public ICollection<FakeChild> Children { get; set; } = new List<FakeChild>();
    }

    public sealed class FakeChild
    {
        [Key]
        public int Id { get; set; }

        public int TenantId { get; set; }

        public Guid LegacyParentRef { get; set; }

        public string LegacyRef { get; set; } = string.Empty;
    }

    // ---------------------------------------------------------------------------------------------
    // Metadata-volume tests (fluent four kinds, attribute 1:N and N:1).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_MetadataSource_FluentOneToMany_BindsOrderedCompositeComponents()
    {
        using var context = new InMemoryDataContext();
        var builder = context
            .From<Ledger>(cfg => cfg.HasMany(
                l => l.Entries,
                e => new { e.TenantId, e.OwnerCode },
                l => new { l.TenantId, l.Code }))
            .LoadWith(l => l.Entries);

        var spec = SingleSpec<MetadataEagerLoadSpec<Ledger, LedgerEntry>>(builder);
        spec.Binding.Relationship.Kind.Should().Be(RelationshipKind.OneToMany);
        spec.Binding.Route.Should().Be(MetadataEagerLoadRoute.Collection);
        spec.Binding.Navigation.Name.Should().Be(nameof(Ledger.Entries));
        spec.Binding.KeyComponents.Select(c => c.ToString()).Should().Equal(
            "TenantId->TenantId",
            "Code->OwnerCode");
    }

    [Fact]
    public void MetadataLoadWith_MetadataSource_FluentManyToOne_MapsForeignKeyToTheParentSide()
    {
        using var context = new InMemoryDataContext();
        var builder = context
            .From<Ticket>(cfg => cfg.HasOne(
                t => t.Owner,
                t => new { t.OwnerTenantId, t.OwnerId },
                o => new { o.TenantId, o.Id }))
            .LoadWith(t => t.Owner);

        var spec = SingleSpec<MetadataEagerLoadSpec<Ticket, TicketOwner>>(builder);
        spec.Binding.Relationship.Kind.Should().Be(RelationshipKind.ManyToOne);
        spec.Binding.Route.Should().Be(MetadataEagerLoadRoute.Reference);
        spec.Binding.KeyComponents.Select(c => c.ToString()).Should().Equal(
            new[] { "OwnerTenantId->TenantId", "OwnerId->Id" },
            "the dependent (declaring) entity carries the foreign key, so it is the parent side");
    }

    [Fact]
    public void MetadataLoadWith_MetadataSource_FluentOneToOne_BindsOrderedCompositeComponents()
    {
        using var context = new InMemoryDataContext();
        var builder = context
            .From<ProfileParent>(cfg => cfg.HasOneToOne<Profile>(
                p => p.Profile,
                p => new { p.TenantId, p.Id },
                c => new { c.OwnerTenantId, c.OwnerId }))
            .LoadWith(p => p.Profile);

        var spec = SingleSpec<MetadataEagerLoadSpec<ProfileParent, Profile>>(builder);
        spec.Binding.Relationship.Kind.Should().Be(RelationshipKind.OneToOne);
        spec.Binding.Route.Should().Be(MetadataEagerLoadRoute.Reference);
        spec.Binding.KeyComponents.Select(c => c.ToString()).Should().Equal(
            "TenantId->OwnerTenantId",
            "Id->OwnerId");
    }

    [Fact]
    public void MetadataLoadWith_MetadataSource_FluentManyToMany_BindsThroughTheJunction()
    {
        using var context = new InMemoryDataContext();
        var builder = context
            .From<MnParent>(cfg => cfg.HasManyThrough<MnChild, MnLink>(
                p => p.Children,
                l => l.ParentId,
                l => l.ChildId))
            .LoadWith(p => p.Children);

        var spec = SingleSpec<MetadataEagerLoadSpec<MnParent, MnChild>>(builder);
        spec.Binding.Relationship.Kind.Should().Be(RelationshipKind.ManyToMany);
        spec.Binding.Relationship.Junction.Should().NotBeNull();
        spec.Binding.Relationship.Junction!.JunctionType.Should().Be(typeof(MnLink));
        spec.Binding.KeyComponents.Select(c => c.ToString()).Should().Equal("Id->Id");
    }

    [Fact]
    public void MetadataLoadWith_MetadataSource_AttributeOneToMany_Binds()
    {
        using var context = new InMemoryDataContext();
        var builder = context.From<AttrLedger>().LoadWith(l => l.Entries);

        var spec = SingleSpec<MetadataEagerLoadSpec<AttrLedger, AttrEntry>>(builder);
        spec.Binding.Relationship.Kind.Should().Be(RelationshipKind.OneToMany);
        spec.Binding.KeyComponents.Select(c => c.ToString()).Should().Equal(
            new[] { "TenantId->TenantId", "Code->OwnerCode" },
            "the attribute declaration order is the component order");
    }

    [Fact]
    public void MetadataLoadWith_MetadataSource_AttributeManyToOne_Binds()
    {
        using var context = new InMemoryDataContext();
        var builder = context.From<AttrEntry>().LoadWith(e => e.Parent);

        var spec = SingleSpec<MetadataEagerLoadSpec<AttrEntry, AttrLedger>>(builder);
        spec.Binding.Relationship.Kind.Should().Be(RelationshipKind.ManyToOne);
        spec.Binding.KeyComponents.Select(c => c.ToString()).Should().Equal(
            "TenantId->TenantId",
            "OwnerCode->Code");
    }

    // ---------------------------------------------------------------------------------------------
    // Fake multi-component metadata: the consumer must not depend on the producer's current arity.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_CardinalityKeys_FakeComposite_SecondComponentNameDiffersFromPrincipal_Passes()
    {
        RegisterFakeComposite(FakeRelationship(
            RelationshipKind.OneToMany,
            typeof(FakeParent),
            typeof(FakeChild),
            Navigation<FakeParent>(nameof(FakeParent.Children)),
            isCollection: true,
            foreignKeys: [Prop<FakeChild>(nameof(FakeChild.TenantId)), Prop<FakeChild>(nameof(FakeChild.LegacyParentRef))],
            principalKeys: [Prop<FakeParent>(nameof(FakeParent.TenantId)), Prop<FakeParent>(nameof(FakeParent.Id))]));

        using var context = new InMemoryDataContext();
        var builder = context.From<FakeParent>().LoadWith(p => p.Children);

        var spec = SingleSpec<MetadataEagerLoadSpec<FakeParent, FakeChild>>(builder);
        spec.Binding.KeyComponents.Should().HaveCount(2);
        spec.Binding.KeyComponents[0].Parent.Name.Should().Be(nameof(FakeParent.TenantId));
        spec.Binding.KeyComponents[0].Child.Name.Should().Be(nameof(FakeChild.TenantId));
        spec.Binding.KeyComponents[1].Parent.Name.Should().Be(nameof(FakeParent.Id));
        spec.Binding.KeyComponents[1].Child.Name.Should().Be(
            nameof(FakeChild.LegacyParentRef),
            "the child component's name differs from the principal's; only type and position pair components");
    }

    [Fact]
    public void MetadataLoadWith_CardinalityKeys_FakeComposite_ConsumesEveryComponentInOrder()
    {
        RegisterFakeComposite(FakeRelationship(
            RelationshipKind.OneToMany,
            typeof(FakeParent),
            typeof(FakeChild),
            Navigation<FakeParent>(nameof(FakeParent.Children)),
            isCollection: true,
            foreignKeys: [Prop<FakeChild>(nameof(FakeChild.TenantId)), Prop<FakeChild>(nameof(FakeChild.LegacyParentRef))],
            principalKeys: [Prop<FakeParent>(nameof(FakeParent.TenantId)), Prop<FakeParent>(nameof(FakeParent.Id))]));

        using var context = new InMemoryDataContext();
        var spec = SingleSpec<MetadataEagerLoadSpec<FakeParent, FakeChild>>(
            context.From<FakeParent>().LoadWith(p => p.Children));

        var parentKey = spec.Binding.ParentKeyExpression.Compile();
        var childKey = spec.Binding.ChildKeyExpression.Compile();
        var id = Guid.NewGuid();

        parentKey(new FakeParent { TenantId = 1, Id = id })
            .Should().Be(childKey(new FakeChild { TenantId = 1, LegacyParentRef = id }),
                "the aligned components must produce the same composite key on both sides");
        parentKey(new FakeParent { TenantId = 1, Id = id })
            .Should().NotBe(childKey(new FakeChild { TenantId = 2, LegacyParentRef = id }),
                "the first component takes part in the key");
        parentKey(new FakeParent { TenantId = 1, Id = id })
            .Should().NotBe(childKey(new FakeChild { TenantId = 1, LegacyParentRef = Guid.NewGuid() }),
                "the second component takes part in the key");
    }

    [Fact]
    public void MetadataLoadWith_CardinalityKeys_FakeComposite_SwappedComponentOrder_FailsClosed()
    {
        RegisterFakeComposite(FakeRelationship(
            RelationshipKind.OneToMany,
            typeof(FakeParent),
            typeof(FakeChild),
            Navigation<FakeParent>(nameof(FakeParent.Children)),
            isCollection: true,
            foreignKeys: [Prop<FakeChild>(nameof(FakeChild.TenantId)), Prop<FakeChild>(nameof(FakeChild.LegacyParentRef))],
            principalKeys: [Prop<FakeParent>(nameof(FakeParent.Id)), Prop<FakeParent>(nameof(FakeParent.TenantId))]));

        using var context = new InMemoryDataContext();
        var builder = context.From<FakeParent>();

        AssertFailsBeforeSql(
            () => builder.LoadWith(p => p.Children),
            "component 1 of 2");
        builder.LoadSpecs.Should().BeNull("a rejected binding registers no declaration");
        builder.Query.Should().BeNull("a rejected binding builds no command");
    }

    // ---------------------------------------------------------------------------------------------
    // Invalid metadata: every row fails closed before SQL, with no relationship guessing.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_InvalidMetadata_MissingRelationship_FailsClosed()
    {
        using var context = new InMemoryDataContext();
        var builder = context.From<PlainParent>();

        AssertFailsBeforeSql(() => builder.LoadWith(p => p.Children), "no relationship is declared");
        AssertFailsBeforeSql(() => builder.LoadWith(p => p.Children), "does not infer relationships from conventions");
    }

    [Fact]
    public void MetadataLoadWith_InvalidMetadata_WrongMember_FailsClosed()
    {
        using var context = new InMemoryDataContext();
        var builder = context.From<PlainParent>();

        AssertFailsBeforeSql(() => builder.LoadWith(p => p.Name), "not a reference navigation");
    }

    [Fact]
    public void MetadataLoadWith_InvalidMetadata_RelatedTypeMismatch_FailsClosed()
    {
        RegisterFakeComposite(FakeRelationship(
            RelationshipKind.OneToMany,
            typeof(FakeParent),
            typeof(PlainChild),
            Navigation<FakeParent>(nameof(FakeParent.Children)),
            isCollection: true,
            foreignKeys: [],
            principalKeys: []));

        using var context = new InMemoryDataContext();
        var builder = context.From<FakeParent>();

        AssertFailsBeforeSql(() => builder.LoadWith(p => p.Children), "points to 'PlainChild'");
    }

    [Fact]
    public void MetadataLoadWith_InvalidMetadata_EmptyKeyList_FailsClosed()
    {
        RegisterFakeComposite(FakeRelationship(
            RelationshipKind.OneToMany,
            typeof(FakeParent),
            typeof(FakeChild),
            Navigation<FakeParent>(nameof(FakeParent.Children)),
            isCollection: true,
            foreignKeys: [],
            principalKeys: []));

        using var context = new InMemoryDataContext();
        var builder = context.From<FakeParent>();

        AssertFailsBeforeSql(() => builder.LoadWith(p => p.Children), "declares no foreign key components");
    }

    [Fact]
    public void MetadataLoadWith_InvalidMetadata_ArityMismatch_FailsClosed()
    {
        RegisterFakeComposite(FakeRelationship(
            RelationshipKind.OneToMany,
            typeof(FakeParent),
            typeof(FakeChild),
            Navigation<FakeParent>(nameof(FakeParent.Children)),
            isCollection: true,
            foreignKeys: [Prop<FakeChild>(nameof(FakeChild.TenantId)), Prop<FakeChild>(nameof(FakeChild.LegacyParentRef))],
            principalKeys: [Prop<FakeParent>(nameof(FakeParent.Id))]));

        using var context = new InMemoryDataContext();
        var builder = context.From<FakeParent>();

        AssertFailsBeforeSql(() => builder.LoadWith(p => p.Children), "arities must match");
    }

    [Fact]
    public void MetadataLoadWith_InvalidMetadata_DuplicateComponent_FailsClosed()
    {
        RegisterFakeComposite(FakeRelationship(
            RelationshipKind.OneToMany,
            typeof(FakeParent),
            typeof(FakeChild),
            Navigation<FakeParent>(nameof(FakeParent.Children)),
            isCollection: true,
            foreignKeys: [Prop<FakeChild>(nameof(FakeChild.TenantId)), Prop<FakeChild>(nameof(FakeChild.TenantId))],
            principalKeys: [Prop<FakeParent>(nameof(FakeParent.TenantId)), Prop<FakeParent>(nameof(FakeParent.Id))]));

        using var context = new InMemoryDataContext();
        var builder = context.From<FakeParent>();

        AssertFailsBeforeSql(() => builder.LoadWith(p => p.Children), "repeats the mapped foreign key property");
    }

    [Fact]
    public void MetadataLoadWith_InvalidMetadata_IncompatibleComponentType_FailsClosed()
    {
        RegisterFakeComposite(FakeRelationship(
            RelationshipKind.OneToMany,
            typeof(FakeParent),
            typeof(FakeChild),
            Navigation<FakeParent>(nameof(FakeParent.Children)),
            isCollection: true,
            foreignKeys: [Prop<FakeChild>(nameof(FakeChild.TenantId)), Prop<FakeChild>(nameof(FakeChild.LegacyRef))],
            principalKeys: [Prop<FakeParent>(nameof(FakeParent.TenantId)), Prop<FakeParent>(nameof(FakeParent.Id))]));

        using var context = new InMemoryDataContext();
        var builder = context.From<FakeParent>();

        AssertFailsBeforeSql(() => builder.LoadWith(p => p.Children), "nullable-unwrapped types must match");
    }

    [Fact]
    public void MetadataLoadWith_InvalidMetadata_UnmappedComponent_FailsClosed()
    {
        RegisterFakeComposite(
            FakeRelationship(
                RelationshipKind.OneToMany,
                typeof(FakeParent),
                typeof(FakeChild),
                Navigation<FakeParent>(nameof(FakeParent.Children)),
                isCollection: true,
                foreignKeys: [Prop<FakeChild>(nameof(FakeChild.TenantId)), Prop<FakeChild>(nameof(FakeChild.LegacyParentRef))],
                principalKeys: [Prop<FakeParent>(nameof(FakeParent.TenantId)), Prop<FakeParent>(nameof(FakeParent.Id))]),
            excludeChildProperty: nameof(FakeChild.LegacyParentRef));

        using var context = new InMemoryDataContext();
        var builder = context.From<FakeParent>();

        AssertFailsBeforeSql(() => builder.LoadWith(p => p.Children), "is not mapped on FakeChild");
    }

    [Fact]
    public void MetadataLoadWith_InvalidMetadata_CollectionThroughReferenceRoute_FailsClosed()
    {
        using var context = new InMemoryDataContext();
        context.From<Ledger>(cfg => cfg.HasMany(
            l => l.Entries,
            e => new { e.TenantId, e.OwnerCode },
            l => new { l.TenantId, l.Code }));
        var builder = context.From<Ledger>();

        // An explicit type argument forces the reference overload for a collection-valued member.
        AssertFailsBeforeSql(
            () => builder.LoadWith<ICollection<LedgerEntry>>(l => l.Entries),
            "requires a reference navigation");
    }

    [Fact]
    public void MetadataLoadWith_InvalidMetadata_ReferenceThroughCollectionRoute_FailsClosed()
    {
        // A declared reference relationship on a collection-shaped member: the collection overload must
        // not take it, and the binding reports the route mistake before reading any key.
        RegisterFakeComposite(
            FakeRelationship(
                RelationshipKind.ManyToOne,
                typeof(FakeParent),
                typeof(FakeChild),
                Navigation<FakeParent>(nameof(FakeParent.Children)),
                isCollection: false,
                foreignKeys: [],
                principalKeys: []),
            registerChild: false);

        using var context = new InMemoryDataContext();
        var builder = context.From<FakeParent>();

        AssertFailsBeforeSql(() => builder.LoadWith(p => p.Children), "requires a collection navigation");
    }

    [Fact]
    public void MetadataLoadWith_InvalidMetadata_DuplicateDeclaration_FailsClosed()
    {
        RegisterFakeComposite(FakeRelationship(
            RelationshipKind.OneToMany,
            typeof(FakeParent),
            typeof(FakeChild),
            Navigation<FakeParent>(nameof(FakeParent.Children)),
            isCollection: true,
            foreignKeys: [Prop<FakeChild>(nameof(FakeChild.TenantId)), Prop<FakeChild>(nameof(FakeChild.LegacyParentRef))],
            principalKeys: [Prop<FakeParent>(nameof(FakeParent.TenantId)), Prop<FakeParent>(nameof(FakeParent.Id))]));

        using var context = new InMemoryDataContext();
        var builder = context.From<FakeParent>().LoadWith(p => p.Children);

        Action act = () => builder.LoadWith(p => p.Children);

        act.Should().Throw<InvalidOperationException>().WithMessage("*already has an eager-load declaration*");
    }

    // ---------------------------------------------------------------------------------------------
    // Source resolution: the bare form uses the context's related-type source; the factory form uses
    // the supplied builder and validates its source entity type before anything executes.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_MetadataSource_BareForm_UsesTheContextRelatedTypeSource()
    {
        RegisterStandardFakePair();
        using var context = new InMemoryDataContext();
        var spec = SingleSpec<MetadataEagerLoadSpec<FakeParent, FakeChild>>(
            context.From<FakeParent>().LoadWith(p => p.Children));

        var child = spec.ResolveChild(context);

        child.DataProvider.Should().BeSameAs(context);
        child.LoadSpecs.Should().BeNull();
    }

    [Fact]
    public void MetadataLoadWith_MetadataSource_FactoryForm_UsesTheSuppliedBuilder()
    {
        RegisterStandardFakePair();
        using var context = new InMemoryDataContext();
        var supplied = context.From<FakeChild>();
        var spec = SingleSpec<MetadataEagerLoadSpec<FakeParent, FakeChild>>(
            context.From<FakeParent>().LoadWith(p => p.Children, _ => supplied));

        spec.ResolveChild(context).Should().BeSameAs(supplied);
        spec.ResolveChild(context).Should().BeSameAs(supplied, "the factory is resolved once per context");
    }

    [Fact]
    public void MetadataLoadWith_InvalidMetadata_ChildSourceTypeMismatch_FailsClosed()
    {
        RegisterStandardFakePair();
        using var context = new InMemoryDataContext();
        var supplied = context.From<FakeChild>();
        supplied.SourceEntityType = typeof(LedgerEntry);
        var spec = SingleSpec<MetadataEagerLoadSpec<FakeParent, FakeChild>>(
            context.From<FakeParent>().LoadWith(p => p.Children, _ => supplied));

        FluentActions.Invoking(() => spec.ResolveChild(context))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*supplied a child builder*");
    }

    // ---------------------------------------------------------------------------------------------
    // Scoped paths: inherited navigation, self-reference and two navigations to the same related type.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_PathScope_InheritedNavigation_ResolvesTheBaseDeclaration()
    {
        using var context = new InMemoryDataContext();
        context.From<BaseNode>(cfg => cfg.HasMany(n => n.Leaves, l => l.NodeId));

        var builder = context.From<DerivedNode>().LoadWith(d => d.Leaves);

        var spec = SingleSpec<MetadataEagerLoadSpec<DerivedNode, Leaf>>(builder);
        spec.Binding.Navigation.Should().BeSameAs(typeof(BaseNode).GetProperty(nameof(BaseNode.Leaves)));
        spec.Binding.Relationship.DeclaringType.Should().Be(typeof(BaseNode));
        spec.Binding.KeyComponents.Select(c => c.ToString()).Should().Equal("Id->NodeId");
    }

    [Fact]
    public void MetadataLoadWith_PathScope_SelfReference_ResolvesReferenceAndCollectionPaths()
    {
        using var context = new InMemoryDataContext();

        var managerSpec = SingleSpec<MetadataEagerLoadSpec<Employee, Employee>>(
            context.From<Employee>().LoadWith(e => e.Manager));
        managerSpec.Binding.Relationship.Kind.Should().Be(RelationshipKind.ManyToOne);
        managerSpec.Binding.KeyComponents.Select(c => c.ToString()).Should().Equal("ManagerId->Id");

        var reportsSpec = SingleSpec<MetadataEagerLoadSpec<Employee, Employee>>(
            context.From<Employee>().LoadWith(e => e.Reports));
        reportsSpec.Binding.Relationship.Kind.Should().Be(RelationshipKind.OneToMany);
        reportsSpec.Binding.KeyComponents.Select(c => c.ToString()).Should().Equal("Id->ManagerId");
    }

    [Fact]
    public void MetadataLoadWith_PathScope_TwoNavigationsToTheSameType_ResolveByDeclaredNavigation()
    {
        using var context = new InMemoryDataContext();

        var buyerSpec = SingleSpec<MetadataEagerLoadSpec<Purchase, Party>>(
            context.From<Purchase>().LoadWith(p => p.Buyer));
        var sellerSpec = SingleSpec<MetadataEagerLoadSpec<Purchase, Party>>(
            context.From<Purchase>().LoadWith(p => p.Seller));

        buyerSpec.Binding.Navigation.Name.Should().Be(nameof(Purchase.Buyer));
        buyerSpec.Binding.KeyComponents.Single().Parent.Name.Should().Be(nameof(Purchase.BuyerId));
        sellerSpec.Binding.Navigation.Name.Should().Be(nameof(Purchase.Seller));
        sellerSpec.Binding.KeyComponents.Single().Parent.Name.Should().Be(nameof(Purchase.SellerId));
        sellerSpec.Binding.Relationship.Should().NotBeSameAs(
            buyerSpec.Binding.Relationship,
            "the two navigations to the same related type are distinct declared relationships");
    }

    // ---------------------------------------------------------------------------------------------
    // Mode and legacy compatibility.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_CardinalityKeys_ExplicitMode_ReachesTheBuilderState()
    {
        RegisterStandardFakePair();
        using var context = new InMemoryDataContext();
        var builder = context
            .From<FakeParent>()
            .LoadWith(p => p.Children, EagerLoadMode.SingleQuery);

        var spec = SingleSpec<MetadataEagerLoadSpec<FakeParent, FakeChild>>(builder);
        spec.RequestedMode.Should().Be(EagerLoadMode.SingleQuery);
        builder.Mode.Should().Be(EagerLoadMode.SingleQuery);
    }

    [Fact]
    public void MetadataLoadWith_LegacyCompatibility_FiveParameter_KeepsTheSingleKeySpec()
    {
        using var context = new InMemoryDataContext();
        context.From<Ledger>(cfg => cfg.HasMany(
            l => l.Entries,
            e => new { e.TenantId, e.OwnerCode },
            l => new { l.TenantId, l.Code }));

        var builder = context
            .From<Ledger>()
            .LoadWith(l => l.Entries, c => c.From<LedgerEntry>(), l => l.Code, e => e.OwnerCode);

        var spec = builder.LoadSpecs.Should().ContainSingle().Subject;
        spec.Should().BeOfType<EagerLoadSpec<Ledger, LedgerEntry, int>>(
            "the legacy five-parameter path keeps its single-key specification and behaviour");
        spec.CollectionMember.Should().BeSameAs(typeof(Ledger).GetProperty(nameof(Ledger.Entries)));
    }

    // ---------------------------------------------------------------------------------------------
    // Single-query join shape (D4): the join seam is internal and deterministic, so the positional
    // correlation and the left/non-collection route are asserted on the synthesized predicate itself.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_SingleQueryJoin_CompositeCollection_PairsComponentsPositionallyOnALeftJoin()
    {
        RegisterStandardFakePair();
        using var context = new InMemoryDataContext();
        var builder = context
            .From<FakeParent>()
            .LoadWith(p => p.Children, EagerLoadMode.SingleQuery);

        var spec = (IEagerLoadSpec<FakeParent>)SingleSpec<MetadataEagerLoadSpec<FakeParent, FakeChild>>(builder);
        var join = spec.ToJoinIntoSpec(context, QueryFilterScope.None, out _);

        join.JoinType.Should().Be(JoinType.Left, "childless parents must be retained");
        join.IsCollection.Should().BeTrue();
        join.ItemTypes.Should().ContainSingle().Which.Should().Be(typeof(FakeChild));
        Equalities(join.Predicate.Body).Select(e => (Parent: Member(e.Left), Child: Member(e.Right)))
            .Should().Equal(
                new[] { (Parent: "TenantId", Child: "TenantId"), (Parent: "Id", Child: "LegacyParentRef") },
                "component i pairs with component i in declaration order, never by name or only the first");
    }

    [Fact]
    public void MetadataLoadWith_SingleQueryJoin_Reference_IsALeftNonCollectionJoinOverTheFullKey()
    {
        using var context = new InMemoryDataContext();
        var builder = context
            .From<Ticket>(cfg => cfg.HasOne(
                t => t.Owner,
                t => new { t.OwnerTenantId, t.OwnerId },
                o => new { o.TenantId, o.Id }))
            .LoadWith(t => t.Owner, EagerLoadMode.SingleQuery);

        var spec = (IEagerLoadSpec<Ticket>)SingleSpec<MetadataEagerLoadSpec<Ticket, TicketOwner>>(builder);
        var join = spec.ToJoinIntoSpec(context, QueryFilterScope.None, out _);

        join.JoinType.Should().Be(JoinType.Left, "a reference with no match becomes null, not a dropped parent");
        join.IsCollection.Should().BeFalse();
        Equalities(join.Predicate.Body).Select(e => (Parent: Member(e.Left), Child: Member(e.Right)))
            .Should().Equal(
                new[] { (Parent: "OwnerTenantId", Child: "TenantId"), (Parent: "OwnerId", Child: "Id") });
    }

    private static IEnumerable<BinaryExpression> Equalities(Expression body)
    {
        if (body is BinaryExpression { NodeType: ExpressionType.AndAlso } and)
        {
            foreach (var equality in Equalities(and.Left))
                yield return equality;
            foreach (var equality in Equalities(and.Right))
                yield return equality;
            yield break;
        }

        body.Should().BeAssignableTo<BinaryExpression>();
        var comparison = (BinaryExpression)body;
        comparison.NodeType.Should().Be(ExpressionType.Equal);
        yield return comparison;
    }

    private static string Member(Expression expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            expression = unary.Operand;

        return expression.Should().BeAssignableTo<MemberExpression>().Subject.Member.Name;
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers.
    // ---------------------------------------------------------------------------------------------

    private static TSpec SingleSpec<TSpec>(IEagerLoadNodeSource builder)
        where TSpec : class
        => builder.EagerLoadNodeSpecs.Should().ContainSingle().Subject.Should().BeOfType<TSpec>().Subject;

    private static PropertyInfo Navigation<T>(string name) => typeof(T).GetProperty(name)!;

    private static PropertyInfo Prop<T>(string name) => typeof(T).GetProperty(name)!;

    private static IReadOnlyList<IPropertyMetadata> Props<T>() => new EntityMetadataBuilder<T>().Build().Properties;

    private static void RegisterStandardFakePair()
        => RegisterFakeComposite(FakeRelationship(
            RelationshipKind.OneToMany,
            typeof(FakeParent),
            typeof(FakeChild),
            Navigation<FakeParent>(nameof(FakeParent.Children)),
            isCollection: true,
            foreignKeys: [Prop<FakeChild>(nameof(FakeChild.TenantId)), Prop<FakeChild>(nameof(FakeChild.LegacyParentRef))],
            principalKeys: [Prop<FakeParent>(nameof(FakeParent.TenantId)), Prop<FakeParent>(nameof(FakeParent.Id))]));

    private static void RegisterFakeComposite(IRelationshipMetadata relationship, string? excludeChildProperty = null, bool registerChild = true)
    {
        var navigation = relationship.Navigation;
        var parentProperties = Props<FakeParent>()
            .Where(p => navigation is null || p.PropertyInfo != navigation)
            .ToList();
        DataContextCache.Metadata[typeof(FakeParent)] =
            new EntityMetadata(typeof(FakeParent).Name, parentProperties, relationships: [relationship]);

        if (!registerChild)
            return;

        var childProperties = Props<FakeChild>()
            .Where(p => excludeChildProperty is null || p.PropertyInfo.Name != excludeChildProperty)
            .ToList();
        DataContextCache.Metadata[typeof(FakeChild)] =
            new EntityMetadata(typeof(FakeChild).Name, childProperties);
    }

    /// <summary>
    /// Builds a hand-written relationship with the supplied component lists, exactly as an internal
    /// producer/test seam would publish one: the consumer must validate it at demand resolution.
    /// </summary>
    private static RelationshipMetadata FakeRelationship(
        RelationshipKind kind,
        Type declaringType,
        Type relatedType,
        PropertyInfo navigation,
        bool isCollection,
        IReadOnlyList<PropertyInfo> foreignKeys,
        IReadOnlyList<PropertyInfo> principalKeys)
    {
        var dependentType = kind == RelationshipKind.ManyToOne ? declaringType : relatedType;
        var principalType = kind == RelationshipKind.ManyToOne ? relatedType : declaringType;

        return new RelationshipMetadata(
            kind,
            declaringType,
            relatedType,
            navigation,
            isCollection,
            foreignKeys,
            principalKeys,
            dependentType,
            principalType);
    }

    /// <summary>
    /// Asserts the call is rejected with <see cref="NotSupportedException"/> before any SQL or cache work,
    /// and that repeating the attempt rethrows the same rejection (nothing was cached on the bad path).
    /// </summary>
    private static void AssertFailsBeforeSql(Action call, string messageFragment)
    {
        call.Should().Throw<NotSupportedException>().WithMessage("*" + messageFragment + "*");
        call.Should().Throw<NotSupportedException>().WithMessage("*" + messageFragment + "*");
    }
}
