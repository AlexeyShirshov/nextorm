using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Slice-A unit tests for declared relationship metadata (#105): the fluent <c>HasMany</c>/<c>HasOne</c>
/// declarations, the <see cref="RelationshipAttribute"/> parity, the navigation-vs-column mapping rule,
/// inverse resolution, first-registration-wins and the <see cref="NotSupportedException"/> rejections.
/// No database is involved: the tests exercise <see cref="EntityMetadataBuilder{T}"/> directly and read
/// the metadata model (and the configured <see cref="DataContextCache.Metadata"/> seam) without a query.
/// </summary>
public class RelationshipMetadataTests
{
    public RelationshipMetadataTests()
    {
        DataContextCache.Clear();
    }

    public sealed class ParentA
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public ICollection<ChildA> Children { get; set; } = new List<ChildA>();
    }

    public sealed class ChildA
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public ParentA? Parent { get; set; }
    }

    public sealed class AttrParent
    {
        public int Id { get; set; }

        [Relationship(ForeignKey = nameof(AttrChild.ParentId))]
        public ICollection<AttrChild> Children { get; set; } = new List<AttrChild>();
    }

    public sealed class AttrChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class ClashParent
    {
        public int Id { get; set; }

        [Relationship(ForeignKey = nameof(ClashChild.OtherParentId))]
        public ICollection<ClashChild> Children { get; set; } = new List<ClashChild>();
    }

    public sealed class ClashChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public int OtherParentId { get; set; }
    }

    public sealed class InvParent
    {
        public int Id { get; set; }
        public ICollection<InvChild> Children { get; set; } = new List<InvChild>();
    }

    public sealed class InvChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public InvParent? Parent { get; set; }
    }

    public sealed class OneSidedParent
    {
        public int Id { get; set; }
        public ICollection<LoneChild> Children { get; set; } = new List<LoneChild>();
    }

    public sealed class LoneChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class FirstWinsParent
    {
        public int Id { get; set; }
        public ICollection<FirstWinsChild> Children { get; set; } = new List<FirstWinsChild>();
    }

    public sealed class FirstWinsChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public int OtherParentId { get; set; }
    }

    public sealed class MixedParent
    {
        public int Id { get; set; }
        public ICollection<MixedChild> Children { get; set; } = new List<MixedChild>();
        public MixedChild? Unrelated { get; set; }
    }

    public sealed class MixedChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class ClashMapParent
    {
        public int Id { get; set; }
        public ICollection<ClashMapChild> Children { get; set; } = new List<ClashMapChild>();
    }

    public sealed class ClashMapChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class FallbackParent
    {
        public int Id { get; set; }
    }

    public sealed class FallbackChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class NarrowParent
    {
        public int Id { get; set; }
        public ICollection<NarrowChild> Children { get; set; } = new List<NarrowChild>();
    }

    public sealed class NarrowChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class KeylessParent
    {
        public string Name { get; set; } = "";
        public ICollection<KeylessChild> Children { get; set; } = new List<KeylessChild>();
    }

    public sealed class KeylessChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class MismatchParent
    {
        public Guid Id { get; set; }
        public ICollection<MismatchChild> Children { get; set; } = new List<MismatchChild>();
    }

    public sealed class MismatchChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class ProfileOwnerA
    {
        public int Id { get; set; }
        public ProfileA? Profile { get; set; }
    }

    public sealed class ProfileA
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
    }

    public sealed class SharedFkParent
    {
        public int Id { get; set; }
        public ICollection<SharedFkChild> Children { get; set; } = new List<SharedFkChild>();
        public SharedFkChild? Primary { get; set; }
    }

    public sealed class SharedFkChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class DualRefParent
    {
        public int Id { get; set; }
        public SharedFkChild? Primary { get; set; }
        public SharedFkChild? Secondary { get; set; }
    }

    public sealed class NonUniqueFkParent
    {
        public int Id { get; set; }
        public NonUniqueFkChild? Primary { get; set; }
    }

    public sealed class NonUniqueFkChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class M2MParent
    {
        public int Id { get; set; }
        public List<M2MChild> Children { get; set; } = new List<M2MChild>();
    }

    public sealed class M2MChild
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class M2MLink
    {
        public int ParentId { get; set; }
        public int ChildId { get; set; }
    }

    public sealed class JuncMismatchParent
    {
        public int Id { get; set; }
        public List<JuncMismatchChild> Children { get; set; } = new List<JuncMismatchChild>();
    }

    public sealed class JuncMismatchChild
    {
        public int Id { get; set; }
    }

    public sealed class JuncParentMismatchLink
    {
        public Guid ParentId { get; set; }
        public int ChildId { get; set; }
    }

    public sealed class JuncChildMismatchLink
    {
        public int ParentId { get; set; }
        public long ChildId { get; set; }
    }

    [Fact]
    public void HasMany_ShouldRegisterOneToManyRelationshipOnPrincipalSide()
    {
        var metadata = new EntityMetadataBuilder<ParentA>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build();

        metadata.Relationships.Should().ContainSingle();
        var relationship = metadata.Relationships[0];

        relationship.Kind.Should().Be(RelationshipKind.OneToMany);
        relationship.DeclaringType.Should().Be(typeof(ParentA));
        relationship.RelatedType.Should().Be(typeof(ChildA));
        relationship.Navigation!.Name.Should().Be(nameof(ParentA.Children));
        relationship.Navigation!.DeclaringType.Should().Be(typeof(ParentA));
        relationship.IsCollection.Should().BeTrue();

        relationship.ForeignKey.Should().ContainSingle();
        relationship.ForeignKey[0].PropertyInfo.Name.Should().Be(nameof(ChildA.ParentId));
        relationship.ForeignKey[0].PropertyInfo.DeclaringType.Should().Be(typeof(ChildA));

        relationship.PrincipalKey.Should().ContainSingle();
        relationship.PrincipalKey[0].PropertyInfo.Name.Should().Be(nameof(ParentA.Id));
        relationship.PrincipalKey[0].IsKey.Should().BeTrue();
    }

    [Fact]
    public void HasOneToOne_ShouldRegisterOneToOneRelationshipOnPrincipalSide()
    {
        var metadata = new EntityMetadataBuilder<ProfileOwnerA>()
            .HasOneToOne(o => o.Profile, o => o.Id, p => p.OwnerId)
            .Build();

        metadata.Relationships.Should().ContainSingle();
        var relationship = metadata.Relationships[0];

        relationship.Kind.Should().Be(RelationshipKind.OneToOne);
        relationship.DeclaringType.Should().Be(typeof(ProfileOwnerA));
        relationship.RelatedType.Should().Be(typeof(ProfileA));
        relationship.Navigation!.Name.Should().Be(nameof(ProfileOwnerA.Profile));
        relationship.Navigation!.DeclaringType.Should().Be(typeof(ProfileOwnerA));
        relationship.IsCollection.Should().BeFalse();

        // The foreign key lives on the related (dependent) type; the principal key on this entity.
        relationship.ForeignKey.Should().ContainSingle();
        relationship.ForeignKey[0].PropertyInfo.Name.Should().Be(nameof(ProfileA.OwnerId));
        relationship.ForeignKey[0].PropertyInfo.DeclaringType.Should().Be(typeof(ProfileA));

        relationship.PrincipalKey.Should().ContainSingle();
        relationship.PrincipalKey[0].PropertyInfo.Name.Should().Be(nameof(ProfileOwnerA.Id));
        relationship.PrincipalKey[0].IsKey.Should().BeTrue();

        metadata.Properties.Should().NotContain(p => p.PropertyInfo.Name == nameof(ProfileOwnerA.Profile));
    }

    [Fact]
    public void HasOneToOne_WithSharedForeignKeyAndOneToMany_ShouldPreserveBothDeclarationsIndependently()
    {
        // R170-02 / SH1: the same mapped FK backs both a OneToMany collection navigation and a OneToOne
        // reference navigation. Declaring the one-to-one must not merge the declarations, subsume the
        // collection, or force uniqueness onto the shared FK.
        var metadata = new EntityMetadataBuilder<SharedFkParent>()
            .HasMany(p => p.Children, c => c.ParentId)
            .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId)
            .Build();

        metadata.Relationships.Should().HaveCount(2);

        var oneToMany = metadata.Relationships.Single(r => r.Kind == RelationshipKind.OneToMany);
        var oneToOne = metadata.Relationships.Single(r => r.Kind == RelationshipKind.OneToOne);
        oneToMany.Should().NotBeSameAs(oneToOne, "the two declarations are independent, not merged");

        // Both declarations survive with their declared cardinality and participants.
        oneToMany.Navigation!.Name.Should().Be(nameof(SharedFkParent.Children));
        oneToMany.IsCollection.Should().BeTrue("the non-one-to-one side keeps its collection multiplicity");
        oneToOne.Navigation!.Name.Should().Be(nameof(SharedFkParent.Primary));
        oneToOne.IsCollection.Should().BeFalse();

        oneToMany.DeclaringType.Should().Be(typeof(SharedFkParent));
        oneToMany.RelatedType.Should().Be(typeof(SharedFkChild));
        oneToOne.DeclaringType.Should().Be(typeof(SharedFkParent));
        oneToOne.RelatedType.Should().Be(typeof(SharedFkChild));

        // Both resolve to the one shared foreign-key property, each with its own principal key.
        oneToMany.ForeignKey[0].PropertyInfo.Should().BeSameAs(oneToOne.ForeignKey[0].PropertyInfo,
            "both declarations share the one foreign-key property without merging");
        oneToMany.ForeignKey[0].PropertyInfo.Name.Should().Be(nameof(SharedFkChild.ParentId));
        oneToOne.ForeignKey[0].PropertyInfo.Name.Should().Be(nameof(SharedFkChild.ParentId));
        oneToMany.PrincipalKey[0].PropertyInfo.Name.Should().Be(nameof(SharedFkParent.Id));
        oneToOne.PrincipalKey[0].PropertyInfo.Name.Should().Be(nameof(SharedFkParent.Id));

        // Negative control: a OneToOne declaration must not force a uniqueness flag/constraint onto the
        // shared FK. The core model exposes no uniqueness member, so the observable contract is that the
        // FK stays a plain non-key dependent column; an implementation that forced uniqueness fails here.
        oneToOne.ForeignKey[0].IsKey.Should().BeFalse(
            "a OneToOne declaration does not force uniqueness onto the shared foreign key");
        oneToMany.ForeignKey[0].IsKey.Should().BeFalse(
            "the OneToMany declaration does not mark the shared foreign key unique");
    }

    [Fact]
    public void TwoHasOneToOne_NavigationsSharingOneForeignKey_ShouldBothRegisterWithoutRejection()
    {
        // R170-02 / SH2: two independently declared OneToOne navigations over the same mapped FK are not a
        // conflict; both register, neither forces a uniqueness constraint, and neither is mapped as a column.
        var metadata = new EntityMetadataBuilder<DualRefParent>()
            .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId)
            .HasOneToOne(p => p.Secondary, p => p.Id, c => c.ParentId)
            .Build();

        metadata.Relationships.Should().HaveCount(2);
        metadata.Relationships.Should().OnlyContain(r => r.Kind == RelationshipKind.OneToOne);
        metadata.Relationships.Select(r => r.Navigation!.Name).Should()
            .BeEquivalentTo(new[] { nameof(DualRefParent.Primary), nameof(DualRefParent.Secondary) });
        metadata.Relationships.Should().OnlyContain(r => !r.ForeignKey[0].IsKey,
            "a OneToOne declaration adds no uniqueness flag to the shared foreign key");

        metadata.Properties.Should().NotContain(p => p.PropertyInfo.Name == nameof(DualRefParent.Primary));
        metadata.Properties.Should().NotContain(p => p.PropertyInfo.Name == nameof(DualRefParent.Secondary));
    }

    [Fact]
    public void HasOneToOne_OnUniqueForeignKey_ShouldAcceptDeclarationAndResolveKeys()
    {
        // case UQ / R170-01: a OneToOne declared over a genuinely unique FK is accepted at metadata build
        // time; the declaration resolves its keys and excludes the navigation from mapped columns. This
        // asserts declaration acceptance only — metadata does not inspect schema/index uniqueness.
        var metadata = new EntityMetadataBuilder<ProfileOwnerA>()
            .HasOneToOne(o => o.Profile, o => o.Id, p => p.OwnerId)
            .Build();

        metadata.Relationships.Should().ContainSingle();
        var relationship = metadata.Relationships[0];
        relationship.Kind.Should().Be(RelationshipKind.OneToOne);
        relationship.ForeignKey[0].PropertyInfo.Name.Should().Be(nameof(ProfileA.OwnerId));
        relationship.PrincipalKey[0].PropertyInfo.Name.Should().Be(nameof(ProfileOwnerA.Id));
        relationship.ForeignKey[0].IsKey.Should().BeFalse("declaring OneToOne does not mark the FK unique");
        metadata.Properties.Should().NotContain(p => p.PropertyInfo.Name == nameof(ProfileOwnerA.Profile));
    }

    [Fact]
    public void HasOneToOne_OnNonUniqueForeignKey_ShouldBuildWithoutMetadataRejection()
    {
        // case NQ / R170-01: a OneToOne declared over an FK that is not unique in the data is still
        // accepted at metadata build time; no uniqueness validation runs and no metadata exception is
        // raised. The non-uniqueness only surfaces at runtime (REQ170-RUNTIME). Declaration acceptance only.
        var metadata = new EntityMetadataBuilder<NonUniqueFkParent>()
            .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId)
            .Build();

        var relationship = metadata.Relationships.Should().ContainSingle().Subject;
        relationship.Kind.Should().Be(RelationshipKind.OneToOne);
        relationship.ForeignKey[0].PropertyInfo.Name.Should().Be(nameof(NonUniqueFkChild.ParentId));
        relationship.ForeignKey[0].IsKey.Should().BeFalse(
            "the OneToOne declaration must not force a uniqueness flag onto a non-unique FK");
    }

    [Fact]
    public void HasOne_ShouldRegisterManyToOneRelationshipOnDependentSide()
    {
        var metadata = new EntityMetadataBuilder<ChildA>()
            .HasOne(c => c.Parent, c => c.ParentId)
            .Build();

        metadata.Relationships.Should().ContainSingle();
        var relationship = metadata.Relationships[0];

        relationship.Kind.Should().Be(RelationshipKind.ManyToOne);
        relationship.DeclaringType.Should().Be(typeof(ChildA));
        relationship.RelatedType.Should().Be(typeof(ParentA));
        relationship.Navigation!.Name.Should().Be(nameof(ChildA.Parent));
        relationship.Navigation!.DeclaringType.Should().Be(typeof(ChildA));
        relationship.IsCollection.Should().BeFalse();

        // The foreign key lives on the declaring (dependent) type, not on the related one.
        relationship.ForeignKey.Should().ContainSingle();
        relationship.ForeignKey[0].PropertyInfo.Name.Should().Be(nameof(ChildA.ParentId));
        relationship.ForeignKey[0].PropertyInfo.DeclaringType.Should().Be(typeof(ChildA));

        relationship.PrincipalKey[0].PropertyInfo.Name.Should().Be(nameof(ParentA.Id));
        relationship.PrincipalKey[0].IsKey.Should().BeTrue();
    }

    [Fact]
    public void FluentAndAttribute_ShouldProduceTheSameModel()
    {
        var attributed = new EntityMetadataBuilder<AttrParent>().Build();
        var fluent = new EntityMetadataBuilder<AttrParent>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build();

        attributed.Relationships.Should().ContainSingle();
        fluent.Relationships.Should().ContainSingle();

        var fromAttribute = attributed.Relationships[0];
        var fromFluent = fluent.Relationships[0];

        fromAttribute.Kind.Should().Be(fromFluent.Kind);
        fromAttribute.DeclaringType.Should().Be(fromFluent.DeclaringType);
        fromAttribute.RelatedType.Should().Be(fromFluent.RelatedType);
        fromAttribute.Navigation!.Name.Should().Be(fromFluent.Navigation!.Name);
        fromAttribute.Navigation!.DeclaringType.Should().Be(fromFluent.Navigation!.DeclaringType);
        fromAttribute.IsCollection.Should().Be(fromFluent.IsCollection);
        fromAttribute.ForeignKey[0].PropertyInfo.Name.Should().Be(fromFluent.ForeignKey[0].PropertyInfo.Name);
        fromAttribute.PrincipalKey[0].PropertyInfo.Name.Should().Be(fromFluent.PrincipalKey[0].PropertyInfo.Name);
    }

    [Fact]
    public void Fluent_ShouldWinOverAttribute_OnTheSameNavigation()
    {
        var metadata = new EntityMetadataBuilder<ClashParent>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build();

        metadata.Relationships.Should().ContainSingle();
        metadata.Relationships[0].ForeignKey[0].PropertyInfo.Name
            .Should().Be(nameof(ClashChild.ParentId), "the fluent declaration wins and the attribute is ignored");
    }

    [Fact]
    public void Inverse_ShouldBeNull_WhenOnlyOneSideIsDeclared()
    {
        var parentMetadata = new EntityMetadataBuilder<OneSidedParent>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build();
        var childMetadata = new EntityMetadataBuilder<LoneChild>().Build();

        DataContextCache.Metadata[typeof(OneSidedParent)] = parentMetadata;
        DataContextCache.Metadata[typeof(LoneChild)] = childMetadata;

        parentMetadata.Relationships[0].Inverse.Should().BeNull("the inverse is never inferred from convention");
    }

    [Fact]
    public void Inverse_ShouldPointAtTheOtherSide_WhenBothSidesConverge()
    {
        var parentMetadata = new EntityMetadataBuilder<InvParent>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build();
        var childMetadata = new EntityMetadataBuilder<InvChild>()
            .HasOne(c => c.Parent, c => c.ParentId)
            .Build();

        DataContextCache.Metadata[typeof(InvParent)] = parentMetadata;
        DataContextCache.Metadata[typeof(InvChild)] = childMetadata;

        parentMetadata.Relationships[0].Inverse.Should().BeSameAs(childMetadata.Relationships[0]);
        childMetadata.Relationships[0].Inverse.Should().BeSameAs(parentMetadata.Relationships[0]);
    }

    [Fact]
    public void SecondRegistration_ShouldBeIgnored_FirstRegistrationWins()
    {
        using var context = new InMemoryDataContext();
        context.From<FirstWinsParent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        context.From<FirstWinsParent>(b => b.HasMany(p => p.Children, c => c.OtherParentId));

        var metadata = DataContextCache.Metadata[typeof(FirstWinsParent)];

        metadata.Relationships.Should().ContainSingle();
        metadata.Relationships[0].ForeignKey[0].PropertyInfo.Name.Should().Be(nameof(FirstWinsChild.ParentId));
    }

    [Fact]
    public void DeclaredNavigation_ShouldBeExcludedFromProperties_WhileUnrelatedMemberStaysMapped()
    {
        var metadata = new EntityMetadataBuilder<MixedParent>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build();

        metadata.Properties.Should().Contain(p => p.PropertyInfo.Name == nameof(MixedParent.Id));
        metadata.Properties.Should().Contain(p => p.PropertyInfo.Name == nameof(MixedParent.Unrelated));
        metadata.Properties.Should().NotContain(p => p.PropertyInfo.Name == nameof(MixedParent.Children));
    }

    [Fact]
    public void DeclaredNavigation_MappedAsColumn_ShouldThrow()
    {
        var builder = new EntityMetadataBuilder<ClashMapParent>()
            .HasMany(p => p.Children, c => c.ParentId);
        _ = builder.Property(p => p.Children).HasColumnName("children");

        Action act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*participates in a declared relationship*");
    }

    [Fact]
    public void FallbackOverloads_ShouldDeclareRelationshipsWithoutNavigation()
    {
        var oneToMany = new EntityMetadataBuilder<FallbackParent>()
            .HasMany<FallbackChild, int>(c => c.ParentId, p => p.Id)
            .Build();
        var manyToOne = new EntityMetadataBuilder<FallbackChild>()
            .HasOne<FallbackParent, int>(c => c.ParentId, p => p.Id)
            .Build();

        oneToMany.Relationships.Should().ContainSingle();
        oneToMany.Relationships[0].Kind.Should().Be(RelationshipKind.OneToMany);
        oneToMany.Relationships[0].Navigation.Should().BeNull();
        oneToMany.Relationships[0].IsCollection.Should().BeFalse();

        manyToOne.Relationships.Should().ContainSingle();
        manyToOne.Relationships[0].Kind.Should().Be(RelationshipKind.ManyToOne);
        manyToOne.Relationships[0].Navigation.Should().BeNull();
    }

    [Fact]
    public void ForeignKeyNotMappedOnDependentType_ShouldThrow()
    {
        var narrowChildBuilder = new EntityMetadataBuilder<NarrowChild>();
        _ = narrowChildBuilder.Property(c => c.Id);
        DataContextCache.Metadata[typeof(NarrowChild)] = narrowChildBuilder.Build();

        var relationship = new EntityMetadataBuilder<NarrowParent>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build()
            .Relationships[0];

        Action act = () => _ = relationship.ForeignKey;

        act.Should().Throw<NotSupportedException>().WithMessage("*ParentId*not mapped on NarrowChild*");
    }

    [Fact]
    public void PrincipalTypeWithoutKey_ShouldThrow()
    {
        var relationship = new EntityMetadataBuilder<KeylessParent>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build()
            .Relationships[0];

        Action act = () => _ = relationship.PrincipalKey;

        act.Should().Throw<NotSupportedException>().WithMessage("*no principal key*");
    }

    [Fact]
    public void ForeignKeyPrincipalKeyTypeMismatch_ShouldThrow()
    {
        var relationship = new EntityMetadataBuilder<MismatchParent>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build()
            .Relationships[0];

        Action act = () => _ = relationship.ForeignKey;

        act.Should().Throw<NotSupportedException>().WithMessage("*does not match the principal key*");
    }

    [Fact]
    public void Junction_ShouldBeNull_ForNonManyToManyRelationship()
    {
        var relationship = new EntityMetadataBuilder<ParentA>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build()
            .Relationships[0];

        relationship.Junction.Should().BeNull();
    }

    [Fact]
    public void HasManyThrough_ShouldRegisterManyToManyWithJunction()
    {
        var metadata = new EntityMetadataBuilder<M2MParent>()
            .HasManyThrough<M2MChild, M2MLink, int, int>(
                p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId)
            .Build();

        metadata.Relationships.Should().ContainSingle();
        var relationship = metadata.Relationships[0];

        relationship.Kind.Should().Be(RelationshipKind.ManyToMany);
        relationship.DeclaringType.Should().Be(typeof(M2MParent));
        relationship.RelatedType.Should().Be(typeof(M2MChild));
        relationship.IsCollection.Should().BeTrue();
        relationship.Navigation!.Name.Should().Be(nameof(M2MParent.Children));

        relationship.PrincipalKey.Should().ContainSingle();
        relationship.PrincipalKey[0].PropertyInfo.Name.Should().Be(nameof(M2MParent.Id));
        relationship.ForeignKey.Should().ContainSingle();
        relationship.ForeignKey[0].PropertyInfo.Name.Should().Be(nameof(M2MChild.Id));

        relationship.Junction.Should().NotBeNull();
        relationship.Junction!.JunctionType.Should().Be(typeof(M2MLink));
        relationship.Junction.ParentKey[0].PropertyInfo.Name.Should().Be(nameof(M2MParent.Id));
        relationship.Junction.ChildKey[0].PropertyInfo.Name.Should().Be(nameof(M2MChild.Id));
        relationship.Junction.JunctionParentForeignKey[0].PropertyInfo.Name.Should().Be(nameof(M2MLink.ParentId));
        relationship.Junction.JunctionChildForeignKey[0].PropertyInfo.Name.Should().Be(nameof(M2MLink.ChildId));
    }

    [Fact]
    public void HasManyThrough_ShouldExcludeNavigationFromProperties()
    {
        var metadata = new EntityMetadataBuilder<M2MParent>()
            .HasManyThrough<M2MChild, M2MLink, int, int>(
                p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId)
            .Build();

        metadata.Properties.Should().NotContain(p => p.PropertyInfo.Name == nameof(M2MParent.Children));
    }

    [Fact]
    public void HasManyThrough_ParentKeyTypeMismatch_ShouldThrow()
    {
        var builder = new EntityMetadataBuilder<JuncMismatchParent>();

        Action act = () => builder.HasManyThrough<JuncMismatchChild, JuncParentMismatchLink, object, int>(
            p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId);

        act.Should().Throw<NotSupportedException>().WithMessage("*does not match the junction*");
    }

    [Fact]
    public void HasManyThrough_ChildKeyTypeMismatch_ShouldThrow()
    {
        var builder = new EntityMetadataBuilder<JuncMismatchParent>();

        Action act = () => builder.HasManyThrough<JuncMismatchChild, JuncChildMismatchLink, int, object>(
            p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId);

        act.Should().Throw<NotSupportedException>().WithMessage("*does not match the junction*");
    }

    [Fact]
    public void AllRelationshipKinds_ShouldBeRepresentable()
    {
        var kinds = Enum.GetValues<RelationshipKind>();
        kinds.Should().HaveCount(4);
        kinds.Should().Contain(RelationshipKind.OneToMany);
        kinds.Should().Contain(RelationshipKind.ManyToOne);
        kinds.Should().Contain(RelationshipKind.OneToOne);
        kinds.Should().Contain(RelationshipKind.ManyToMany);

        // Fluent overloads exist for O2M/M2O/O2O; the model can carry M2M as well.
        var oneToOne = new RelationshipMetadata(
            RelationshipKind.OneToOne,
            typeof(InvChild),
            typeof(InvParent),
            typeof(InvChild).GetProperty(nameof(InvChild.Parent)),
            false,
            typeof(InvChild).GetProperty(nameof(InvChild.ParentId))!,
            typeof(InvParent).GetProperty(nameof(InvParent.Id)),
            typeof(InvChild),
            typeof(InvParent));

        var manyToMany = new RelationshipMetadata(
            RelationshipKind.ManyToMany,
            typeof(InvParent),
            typeof(InvChild),
            typeof(InvParent).GetProperty(nameof(InvParent.Children)),
            true,
            typeof(InvChild).GetProperty(nameof(InvChild.ParentId))!,
            typeof(InvParent).GetProperty(nameof(InvParent.Id)),
            typeof(InvChild),
            typeof(InvParent));

        oneToOne.Kind.Should().Be(RelationshipKind.OneToOne);
        oneToOne.IsCollection.Should().BeFalse();
        manyToMany.Kind.Should().Be(RelationshipKind.ManyToMany);
        manyToMany.IsCollection.Should().BeTrue();
    }
}
