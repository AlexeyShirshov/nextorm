using FluentAssertions;
using NextORM.Core;
using Xunit;

namespace nextorm.core.tests.Reflection;

public sealed class PropertyIdentityTests
{
    [Fact]
    public void SameProperty_InheritedPropertyReflectedOnDerivedType_IsSameDeclaration()
    {
        var baseProperty = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!;
        var reflectedProperty = typeof(DerivedEntity).GetProperty(nameof(BaseEntity.Id))!;

        reflectedProperty.ReflectedType.Should().NotBe(reflectedProperty.DeclaringType);
        MemberInfoExtensions.SameProperty(baseProperty, reflectedProperty).Should().BeTrue();
    }

    [Fact]
    public void SameProperty_HiddenPropertyWithNewDeclaration_IsDifferent()
    {
        var baseProperty = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!;
        var hiddenProperty = typeof(HiddenEntity).GetProperty(nameof(BaseEntity.Id))!;

        MemberInfoExtensions.SameProperty(baseProperty, hiddenProperty).Should().BeFalse();
    }

    [Fact]
    public void SameProperty_InterfaceProperty_IsDistinctFromImplementationProperty()
    {
        var interfaceProperty = typeof(IEntity).GetProperty(nameof(IEntity.Id))!;
        var implementationProperty = typeof(InterfaceEntity).GetProperty(nameof(IEntity.Id))!;

        MemberInfoExtensions.SameProperty(interfaceProperty, interfaceProperty).Should().BeTrue();
        MemberInfoExtensions.SameProperty(interfaceProperty, implementationProperty).Should().BeFalse();
    }

    [Fact]
    public void SameProperty_ReflectedTypeDoesNotChangeInheritedIdentity()
    {
        var reflectedFromBase = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!;
        var reflectedFromDerived = typeof(DerivedEntity).GetProperty(nameof(BaseEntity.Id))!;

        reflectedFromDerived.ReflectedType.Should().NotBe(reflectedFromDerived.DeclaringType);
        MemberInfoExtensions.SameProperty(reflectedFromBase, reflectedFromDerived).Should().BeTrue();
    }

    [Fact]
    public void EntityMetadata_FindProperty_ResolvesInheritedAndReflectedMembers()
    {
        var metadata = (EntityMetadata)new EntityMetadataBuilder<DerivedEntity>().Build();
        var baseProperty = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!;
        var derivedReflected = typeof(DerivedEntity).GetProperty(nameof(BaseEntity.Id))!;

        metadata.FindProperty(baseProperty).Should().NotBeNull();
        metadata.FindProperty(derivedReflected).Should().BeSameAs(metadata.Properties[0]);
    }

    [Fact]
    public void EntityMetadata_FindProperty_DoesNotMergeHiddenNewProperty()
    {
        var metadata = (EntityMetadata)new EntityMetadataBuilder<HiddenEntity>().Build();
        var baseProperty = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!;
        var hiddenProperty = typeof(HiddenEntity).GetProperty(nameof(BaseEntity.Id))!;

        metadata.FindProperty(hiddenProperty).Should().NotBeNull();
        metadata.FindProperty(baseProperty).Should().BeNull();
    }

    [Fact]
    public void EntityMetadata_FindProperty_DoesNotMergeInterfaceAndImplementationProperties()
    {
        var metadata = (EntityMetadata)new EntityMetadataBuilder<InterfaceEntity>().Build();
        var interfaceProperty = typeof(IEntity).GetProperty(nameof(IEntity.Id))!;
        var implementationProperty = typeof(InterfaceEntity).GetProperty(nameof(IEntity.Id))!;

        metadata.FindProperty(implementationProperty).Should().NotBeNull();
        metadata.FindProperty(interfaceProperty).Should().BeNull();
    }

    private class BaseEntity
    {
        public int Id { get; set; }
    }

    private sealed class DerivedEntity : BaseEntity
    {
    }

    private sealed class HiddenEntity : BaseEntity
    {
        public new int Id { get; set; }
    }

    private interface IEntity
    {
        int Id { get; set; }
    }

    private sealed class InterfaceEntity : IEntity
    {
        public int Id { get; set; }
    }
}
