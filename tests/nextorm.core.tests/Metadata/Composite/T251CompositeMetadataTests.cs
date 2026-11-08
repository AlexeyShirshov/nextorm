using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T251 structural-metadata tests: ordered key component lists, the full default principal key,
/// explicit non-key principals, positional pairing, arity/type/duplicate/unmapped validation on the
/// fluent/attribute/manual paths, manual metadata matched by mapped CLR member identity, and
/// demand-time inverse resolution over the full ordered mapping (absent, unique, ambiguous, unrelated).
/// No database is involved.
/// </summary>
public class T251CompositeMetadataTests
{
    public T251CompositeMetadataTests()
    {
        DataContextCache.Clear();
    }

    public sealed class MetaBlog
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public ICollection<MetaPost> Posts { get; set; } = new List<MetaPost>();
        public ICollection<MetaPost> SecondaryPosts { get; set; } = new List<MetaPost>();
        public MetaPost? Featured { get; set; }
        public List<MetaTag> Tags { get; set; } = new List<MetaTag>();
    }

    public sealed class MetaPost
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
        public int ParentTenantId { get; set; }
        public int ParentId { get; set; }
        public int? NullableParentTenantId { get; set; }
        public int? NullableParentId { get; set; }
        public int OwnerTenantId { get; set; }
        public int OwnerId { get; set; }
        public string ParentCode { get; set; } = "";
        public MetaBlog? Parent { get; set; }
    }

    public sealed class MetaTag
    {
        public int TenantId { get; set; }
        public int Id { get; set; }
    }

    public sealed class MetaBlogTag
    {
        public int BlogTenantId { get; set; }
        public int BlogId { get; set; }
        public int TagTenantId { get; set; }
        public int TagId { get; set; }
    }

    public sealed class PlainParent
    {
        public int Id { get; set; }
        public ICollection<PlainChild> Children { get; set; } = new List<PlainChild>();
    }

    public sealed class PlainChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public PlainParent? Parent { get; set; }
    }

    private static EntityMetadataBuilder<MetaBlog> CompositeBlogBuilder()
    {
        var builder = new EntityMetadataBuilder<MetaBlog>();
        builder.Property(b => b.TenantId).Key();
        builder.Property(b => b.Id).Key();
        return builder;
    }

    private static EntityMetadataBuilder<MetaTag> CompositeTagBuilder()
    {
        var builder = new EntityMetadataBuilder<MetaTag>();
        builder.Property(t => t.TenantId).Key();
        builder.Property(t => t.Id).Key();
        return builder;
    }

    private static string[] Names(IReadOnlyList<IPropertyMetadata> key)
        => key.Select(p => p.PropertyInfo.Name).ToArray();

    [Fact]
    public void ExplicitCompositeKeys_ShouldExposeAllComponentsInSelectorOrder()
    {
        var metadata = CompositeBlogBuilder()
            .HasMany(
                b => b.Posts,
                p => new { p.ParentTenantId, p.ParentId },
                b => new { b.Id, b.TenantId })
            .Build();

        var relationship = metadata.Relationships.Should().ContainSingle().Subject;

        Names(relationship.ForeignKey).Should().ContainInOrder(nameof(MetaPost.ParentTenantId), nameof(MetaPost.ParentId));
        Names(relationship.PrincipalKey).Should().ContainInOrder(nameof(MetaBlog.Id), nameof(MetaBlog.TenantId));
    }

    [Fact]
    public void DefaultPrincipal_ShouldUseTheFullCompositeKeyInMetadataOrder()
    {
        var metadata = CompositeBlogBuilder()
            .HasMany(b => b.Posts, p => new { p.ParentTenantId, p.ParentId })
            .Build();
        DataContextCache.Metadata[typeof(MetaBlog)] = metadata;

        var relationship = metadata.Relationships.Should().ContainSingle().Subject;

        Names(relationship.PrincipalKey).Should().ContainInOrder(nameof(MetaBlog.TenantId), nameof(MetaBlog.Id));
        relationship.PrincipalKey.Should().OnlyContain(p => p.IsKey);
    }

    [Fact]
    public void ExplicitPrincipal_MaySelectMappedNonKeyComponents()
    {
        var metadata = CompositeBlogBuilder()
            .HasMany(b => b.Posts, p => p.ParentCode, b => b.Code)
            .Build();

        var relationship = metadata.Relationships.Should().ContainSingle().Subject;

        Names(relationship.ForeignKey).Should().ContainSingle().Which.Should().Be(nameof(MetaPost.ParentCode));
        Names(relationship.PrincipalKey).Should().ContainSingle().Which.Should().Be(nameof(MetaBlog.Code));
        relationship.PrincipalKey[0].IsKey.Should().BeFalse();
    }

    [Fact]
    public void NullableComponents_ShouldPairByTheirUnwrappedTypes()
    {
        var metadata = CompositeBlogBuilder()
            .HasMany(b => b.Posts, p => new { p.NullableParentTenantId, p.NullableParentId })
            .Build();
        DataContextCache.Metadata[typeof(MetaBlog)] = metadata;

        var relationship = metadata.Relationships.Should().ContainSingle().Subject;

        Names(relationship.ForeignKey).Should().ContainInOrder(nameof(MetaPost.NullableParentTenantId), nameof(MetaPost.NullableParentId));
        Names(relationship.PrincipalKey).Should().ContainInOrder(nameof(MetaBlog.TenantId), nameof(MetaBlog.Id));
    }

    [Fact]
    public void ArityMismatch_ShouldBeRejectedWithTheSectionCounts()
    {
        var relationship = CompositeBlogBuilder()
            .HasMany(b => b.Posts, p => new { p.ParentTenantId, p.ParentId }, b => b.Id)
            .Build()
            .Relationships[0];

        Action act = () => _ = relationship.ForeignKey;

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*MetaBlog.Posts*2 foreign-key component(s) but 1 principal-key component(s)*");
    }

    [Fact]
    public void PositionalTypeMismatch_ShouldNameTheComponentAndCount()
    {
        var relationship = CompositeBlogBuilder()
            .HasMany(b => b.Posts, p => new { p.ParentTenantId, p.ParentId }, b => new { b.Code, b.Id })
            .Build()
            .Relationships[0];

        Action act = () => _ = relationship.ForeignKey;

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*MetaBlog.Posts*component 1 of 2*does not match the principal key*");
    }

    [Fact]
    public void ManualMetadata_EquivalentByMappedIdentity_ShouldValidate()
    {
        var parent = new EntityMetadataBuilder<PlainParent>()
            .HasMany(p => p.Children, c => c.ParentId)
            .Build();
        var child = new EntityMetadataBuilder<PlainChild>()
            .HasOne(c => c.Parent, c => c.ParentId)
            .Build();

        var relationship = new ManualRelationship
        {
            Kind = RelationshipKind.ManyToOne,
            DeclaringType = typeof(PlainChild),
            RelatedType = typeof(PlainParent),
            Navigation = typeof(PlainChild).GetProperty(nameof(PlainChild.Parent)),
            IsCollection = false,
            ForeignKey = new[] { new ShadowPropertyMetadata(child.Relationships[0].ForeignKey[0]) },
            PrincipalKey = new[] { new ShadowPropertyMetadata(parent.Relationships[0].PrincipalKey[0]) },
        };

        Action act = () => RelationshipResolver.ValidateRelationship(relationship);

        act.Should().NotThrow("a custom IPropertyMetadata instance is matched by mapped CLR member, not by reference");
    }

    [Fact]
    public void ManualMetadata_WithEmptyComponentList_ShouldBeRejected()
    {
        var relationship = new ManualRelationship
        {
            Kind = RelationshipKind.ManyToOne,
            DeclaringType = typeof(PlainChild),
            RelatedType = typeof(PlainParent),
            Navigation = typeof(PlainChild).GetProperty(nameof(PlainChild.Parent)),
            IsCollection = false,
            ForeignKey = Array.Empty<IPropertyMetadata>(),
            PrincipalKey = Array.Empty<IPropertyMetadata>(),
        };

        Action act = () => RelationshipResolver.ValidateRelationship(relationship);

        act.Should().Throw<NotSupportedException>().WithMessage("*PlainChild.Parent*no foreign key components*");
    }

    [Fact]
    public void ManualMetadata_WithDuplicateComponent_ShouldBeRejected()
    {
        var child = new EntityMetadataBuilder<PlainChild>().Build();
        var parent = new EntityMetadataBuilder<PlainParent>().Build();
        var relationship = new ManualRelationship
        {
            Kind = RelationshipKind.ManyToOne,
            DeclaringType = typeof(PlainChild),
            RelatedType = typeof(PlainParent),
            Navigation = typeof(PlainChild).GetProperty(nameof(PlainChild.Parent)),
            IsCollection = false,
            ForeignKey = [child.Properties.Single(p => p.PropertyInfo.Name == nameof(PlainChild.ParentId)),
                child.Properties.Single(p => p.PropertyInfo.Name == nameof(PlainChild.ParentId))],
            PrincipalKey = [parent.Properties.Single(p => p.PropertyInfo.Name == nameof(PlainParent.Id))],
        };

        Action act = () => RelationshipResolver.ValidateRelationship(relationship);

        act.Should().Throw<NotSupportedException>().WithMessage("*repeats the mapped foreign key property 'ParentId'*");
    }

    [Fact]
    public void ManualMetadata_WithUnmappedComponent_ShouldBeRejected()
    {
        var child = new EntityMetadataBuilder<PlainChild>().Build();
        var parent = new EntityMetadataBuilder<PlainParent>().Build();
        var relationship = new ManualRelationship
        {
            Kind = RelationshipKind.ManyToOne,
            DeclaringType = typeof(PlainChild),
            RelatedType = typeof(PlainParent),
            Navigation = typeof(PlainChild).GetProperty(nameof(PlainChild.Parent)),
            IsCollection = false,
            ForeignKey =
            [
                new ShadowPropertyMetadata(
                    typeof(MetaPost).GetProperty(nameof(MetaPost.ParentId))!,
                    typeof(MetaPost).GetProperty(nameof(MetaPost.ParentId))!.Name),
            ],
            PrincipalKey = [parent.Properties.Single(p => p.PropertyInfo.Name == nameof(PlainParent.Id))],
        };

        Action act = () => RelationshipResolver.ValidateRelationship(relationship);

        act.Should().Throw<NotSupportedException>().WithMessage("*ParentId*not mapped on PlainChild*");
    }

    [Fact]
    public void ManualJunctionMetadata_WithMismatchedLegArity_ShouldBeRejected()
    {
        var parent = new EntityMetadataBuilder<PlainParent>().Build();
        var child = new EntityMetadataBuilder<PlainChild>().Build();
        var compactType = typeof(MetaBlogTag);
        var relationship = new ManualRelationship
        {
            Kind = RelationshipKind.ManyToMany,
            DeclaringType = typeof(PlainParent),
            RelatedType = typeof(PlainChild),
            Navigation = typeof(PlainParent).GetProperty(nameof(PlainParent.Children)),
            IsCollection = true,
            ForeignKey = [child.Properties.Single(p => p.PropertyInfo.Name == nameof(PlainChild.Id))],
            PrincipalKey = [parent.Properties.Single(p => p.PropertyInfo.Name == nameof(PlainParent.Id))],
            Junction = new RelationshipJunctionMetadata(
                compactType,
                [parent.Properties.Single(p => p.PropertyInfo.Name == nameof(PlainParent.Id))],
                [child.Properties.Single(p => p.PropertyInfo.Name == nameof(PlainChild.Id))],
                [new ShadowPropertyMetadata(compactType.GetProperty(nameof(MetaBlogTag.BlogTenantId))!, nameof(MetaBlogTag.BlogTenantId)),
                    new ShadowPropertyMetadata(compactType.GetProperty(nameof(MetaBlogTag.BlogId))!, nameof(MetaBlogTag.BlogId))],
                [new ShadowPropertyMetadata(compactType.GetProperty(nameof(MetaBlogTag.TagId))!, nameof(MetaBlogTag.TagId))]),
        };

        Action act = () => RelationshipResolver.ValidateRelationship(relationship);

        act.Should().Throw<NotSupportedException>().WithMessage("*parent-key component(s) but 2 parent junction foreign-key component(s)*");
    }

    [Fact]
    public void Junction_ShouldExposeAllFourOrderedLists()
    {
        DataContextCache.Metadata[typeof(MetaTag)] = CompositeTagBuilder().Build();
        var metadata = CompositeBlogBuilder()
            .HasManyThrough<MetaTag, MetaBlogTag>(
                b => b.Tags,
                l => new { l.BlogTenantId, l.BlogId },
                l => new { l.TagTenantId, l.TagId })
            .Build();
        DataContextCache.Metadata[typeof(MetaBlog)] = metadata;

        var relationship = metadata.Relationships.Should().ContainSingle().Subject;
        relationship.Junction.Should().NotBeNull("the many-to-many declaration carries a junction descriptor");
        var junction = relationship.Junction!;

        Names(junction.ParentKey).Should().ContainInOrder(nameof(MetaBlog.TenantId), nameof(MetaBlog.Id));
        Names(junction.ChildKey).Should().ContainInOrder(nameof(MetaTag.TenantId), nameof(MetaTag.Id));
        Names(junction.JunctionParentForeignKey).Should().ContainInOrder(nameof(MetaBlogTag.BlogTenantId), nameof(MetaBlogTag.BlogId));
        Names(junction.JunctionChildForeignKey).Should().ContainInOrder(nameof(MetaBlogTag.TagTenantId), nameof(MetaBlogTag.TagId));
        Names(relationship.ForeignKey).Should().ContainInOrder(nameof(MetaTag.TenantId), nameof(MetaTag.Id));
        Names(relationship.PrincipalKey).Should().ContainInOrder(nameof(MetaBlog.TenantId), nameof(MetaBlog.Id));
    }

    [Fact]
    public void Junction_WithMismatchedComponentType_ShouldBeRejectedAtDeclaration()
    {
        var builder = CompositeBlogBuilder();

        Action act = () => builder.HasManyThrough<MetaTag, MetaBlogTag>(
            b => b.Tags,
            b => b.Code,
            l => l.BlogTenantId,
            t => t.Id,
            l => l.TagId);

        act.Should().Throw<NotSupportedException>().WithMessage("*does not match the junction*component 1 of 1*");
    }

    [Fact]
    public void Inverse_ShouldUseTheFullOrderedMapping_AndIgnoreAnUnrelatedMappingWithTheSameFirstComponent()
    {
        var blog = CompositeBlogBuilder()
            .HasMany(b => b.Posts, p => new { p.ParentTenantId, p.ParentId })
            .HasOneToOne(b => b.Featured, b => new { b.TenantId, b.Id }, p => new { p.OwnerTenantId, p.OwnerId })
            .Build();
        var post = new EntityMetadataBuilder<MetaPost>()
            .HasOne(p => p.Parent, p => new { p.ParentTenantId, p.ParentId })
            .Build();
        DataContextCache.Metadata[typeof(MetaBlog)] = blog;
        DataContextCache.Metadata[typeof(MetaPost)] = post;

        blog.Relationships[0].Inverse.Should().BeSameAs(post.Relationships[0]);
        blog.Relationships[1].Inverse.Should().BeNull("the unrelated one-to-one mapping shares no full canonical mapping");
        post.Relationships[0].Inverse.Should().BeSameAs(blog.Relationships[0]);
    }

    [Fact]
    public void Inverse_ShouldBeNull_WhenOnlyOneSideIsDeclared()
    {
        var blog = CompositeBlogBuilder()
            .HasMany(b => b.Posts, p => new { p.ParentTenantId, p.ParentId })
            .Build();
        var post = new EntityMetadataBuilder<MetaPost>().Build();
        DataContextCache.Metadata[typeof(MetaBlog)] = blog;
        DataContextCache.Metadata[typeof(MetaPost)] = post;

        blog.Relationships[0].Inverse.Should().BeNull();
    }

    [Fact]
    public void Inverse_ShouldThrowATypedAmbiguityErrorAtDemand_WhenTwoNavigationsShareTheCanonicalMapping()
    {
        var blogBuilder = CompositeBlogBuilder();
        blogBuilder
            .HasMany(b => b.Posts, p => new { p.ParentTenantId, p.ParentId })
            .HasMany(b => b.SecondaryPosts, p => new { p.ParentTenantId, p.ParentId });
        var blog = blogBuilder.Build();
        var post = new EntityMetadataBuilder<MetaPost>()
            .HasOne(p => p.Parent, p => new { p.ParentTenantId, p.ParentId })
            .Build();
        DataContextCache.Metadata[typeof(MetaBlog)] = blog;
        DataContextCache.Metadata[typeof(MetaPost)] = post;

        // Registration and the direct mapping stay valid; the conflict surfaces only when the inverse
        // is actually demanded.
        blog.Relationships.Should().HaveCount(2);

        Action act = () => _ = post.Relationships[0].Inverse;

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*ambiguous*MetaBlog.Posts*MetaBlog.SecondaryPosts*");
    }

    private sealed class ManualRelationship : IRelationshipMetadata
    {
        public required RelationshipKind Kind { get; init; }

        public required Type DeclaringType { get; init; }

        public required Type RelatedType { get; init; }

        public PropertyInfo? Navigation { get; init; }

        public bool IsCollection { get; init; }

        public required IReadOnlyList<IPropertyMetadata> ForeignKey { get; init; }

        public required IReadOnlyList<IPropertyMetadata> PrincipalKey { get; init; }

        public IRelationshipMetadata? Inverse => null;

        public RelationshipJunctionMetadata? Junction { get; init; }
    }

    private sealed class ShadowPropertyMetadata : IPropertyMetadata
    {
        internal ShadowPropertyMetadata(IPropertyMetadata inner)
        {
            PropertyInfo = inner.PropertyInfo;
            ColumnName = inner.ColumnName;
            IsKey = inner.IsKey;
        }

        internal ShadowPropertyMetadata(PropertyInfo propertyInfo, string columnName)
        {
            PropertyInfo = propertyInfo;
            ColumnName = columnName;
        }

        public PropertyInfo PropertyInfo { get; }

        public string ColumnName { get; }

        public bool IsKey { get; }
    }
}
