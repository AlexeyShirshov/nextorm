using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Boundary tests for the <c>JoinInto</c> declaration guards (#105): the explicit-key type mismatch,
/// the undeclared relationship, the unsupported relationship kind (M2M), the composite-key
/// reject, the derived (<c>As</c>) source reject and the projection-arity cap.
/// </summary>
public class JoinIntoRejectionTests
{
    public JoinIntoRejectionTests()
    {
        DataContextCache.Clear();
    }

    public sealed class KeyParent
    {
        public int Id { get; set; }
        public ICollection<KeyChild> Children { get; set; } = new List<KeyChild>();
    }

    public sealed class KeyChild
    {
        public int Id { get; set; }
        public long ParentId { get; set; }
    }

    [Fact]
    public void ExplicitKeys_WithDifferentPropertyTypes_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<KeyParent>()
            .JoinInto<KeyChild, long>(ctx.From<KeyChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId);

        act.Should().Throw<NotSupportedException>().WithMessage("*same property type*");
    }

    public sealed class UndeclaredParent
    {
        public int Id { get; set; }
        public ICollection<UndeclaredChild> Children { get; set; } = new List<UndeclaredChild>();
    }

    public sealed class UndeclaredChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    [Fact]
    public void CollectionOverload_WithoutDeclaredRelationship_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<UndeclaredParent>()
            .JoinInto(ctx.From<UndeclaredChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        act.Should().Throw<NotSupportedException>().WithMessage("*requires an explicitly declared relationship*");
    }

    public sealed class RejectParent
    {
        public int Id { get; set; }
        public ICollection<RejectChild> Children { get; set; } = new List<RejectChild>();
    }

    public sealed class RejectChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    [Fact]
    public void ManyToManyWithoutJunction_ShouldThrow()
    {
        DataContextCache.Metadata[typeof(RejectParent)] = MetadataWithRelationship<RejectParent>(
            new RelationshipMetadata(
                RelationshipKind.ManyToMany,
                typeof(RejectParent),
                typeof(RejectChild),
                typeof(RejectParent).GetProperty(nameof(RejectParent.Children)),
                isCollection: true,
                typeof(RejectChild).GetProperty(nameof(RejectChild.ParentId))!,
                typeof(RejectParent).GetProperty(nameof(RejectParent.Id)),
                typeof(RejectChild),
                typeof(RejectParent)));

        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<RejectParent>()
            .JoinInto(ctx.From<RejectChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        act.Should().Throw<NotSupportedException>().WithMessage("*no junction metadata*");
    }

    public sealed class JunctionParent
    {
        public int Id { get; set; }
        public List<JunctionChild> Children { get; set; } = new List<JunctionChild>();
    }

    public sealed class JunctionChild
    {
        public int Id { get; set; }
    }

    public sealed class JunctionLink
    {
        public int ParentId { get; set; }
        public int ChildId { get; set; }
    }

    [Fact]
    public void ManyToManyWithJunction_ShouldResolve()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<JunctionParent>(b => b.HasManyThrough<JunctionChild, JunctionLink, int, int>(
            p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));
        ctx.From<JunctionChild>();

        Action act = () => ctx.From<JunctionParent>()
            .JoinInto(ctx.From<JunctionChild>(), (p, c) => p.Id == c.Id, p => p.Children);

        act.Should().NotThrow();
    }

    [Fact]
    public void OneToOneRelationship_OnTheCollectionOverload_ShouldThrow()
    {
        DataContextCache.Metadata[typeof(RejectParent)] = MetadataWithRelationship<RejectParent>(
            new RelationshipMetadata(
                RelationshipKind.OneToOne,
                typeof(RejectParent),
                typeof(RejectChild),
                typeof(RejectParent).GetProperty(nameof(RejectParent.Children)),
                isCollection: true,
                typeof(RejectChild).GetProperty(nameof(RejectChild.ParentId))!,
                typeof(RejectParent).GetProperty(nameof(RejectParent.Id)),
                typeof(RejectChild),
                typeof(RejectParent)));

        using var ctx = new InMemoryDataContext();

        // A one-to-one relationship is lowered by the reference-navigation overload; the collection
        // overload asks the caller to use it instead of silently producing a collection shape.
        Action act = () => ctx.From<RejectParent>()
            .JoinInto(ctx.From<RejectChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        act.Should().Throw<NotSupportedException>().WithMessage("*expects a OneToMany relationship*");
    }

    [Fact]
    public void CompositeRelationshipKey_ShouldThrow()
    {
        var parentProperties = new EntityMetadataBuilder<RejectParent>().Build().Properties;
        var childProperties = new EntityMetadataBuilder<RejectChild>().Build().Properties;
        var parentKey = parentProperties.First(p => p.PropertyInfo.Name == nameof(RejectParent.Id));
        var childKey = childProperties.First(p => p.PropertyInfo.Name == nameof(RejectChild.ParentId));

        DataContextCache.Metadata[typeof(RejectParent)] = MetadataWithRelationship<RejectParent>(
            new CompositeKeyRelationship(parentKey, childKey));

        using var ctx = new InMemoryDataContext();

        Action act = () => ctx.From<RejectParent>()
            .JoinInto(ctx.From<RejectChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        act.Should().Throw<NotSupportedException>().WithMessage("*composite key*");
    }

    [Fact]
    public void DerivedAsSource_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();

        var derived = ctx.From<RejectParent>().As(p => new { p.Id });

        Action act = () => derived
            .JoinInto<RejectChild, int>(ctx.From<RejectChild>(), (p, c) => p.Id == c.ParentId, p => new List<RejectChild>(), p => p.Id, c => c.ParentId);

        act.Should().Throw<NotSupportedException>().WithMessage("*derived (As) or joined projection source*");
    }

    public sealed class ProjectParent
    {
        public int Id { get; set; }
        public ICollection<ProjectChild> Children { get; set; } = new List<ProjectChild>();
    }

    public sealed class ProjectChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    private static EntityBuilder<ProjectParent> ProjectionParents(InMemoryDataContext ctx)
    {
        ctx.From<ProjectParent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        return ctx.From<ProjectParent>();
    }

    [Fact]
    public void SelectAfterJoinInto_ShouldThrowAtBuildTime()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ProjectionParents(ctx)
            .JoinInto(ctx.From<ProjectChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .Select(p => p.Id);

        act.Should().Throw<NotSupportedException>().WithMessage("*JoinInto cannot be combined with Select*");
    }

    [Fact]
    public void AsAfterJoinInto_ShouldThrowAtBuildTime()
    {
        using var ctx = new InMemoryDataContext();

        Action act = () => ProjectionParents(ctx)
            .JoinInto(ctx.From<ProjectChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .As(p => new { p.Id });

        act.Should().Throw<NotSupportedException>().WithMessage("*JoinInto cannot be combined with As*");
    }

    public sealed class ArityParent
    {
        public int Id { get; set; }
        public ICollection<ArityChild> C1 { get; set; } = new List<ArityChild>();
        public ICollection<ArityChild> C2 { get; set; } = new List<ArityChild>();
        public ICollection<ArityChild> C3 { get; set; } = new List<ArityChild>();
        public ICollection<ArityChild> C4 { get; set; } = new List<ArityChild>();
        public ICollection<ArityChild> C5 { get; set; } = new List<ArityChild>();
        public ICollection<ArityChild> C6 { get; set; } = new List<ArityChild>();
        public ICollection<ArityChild> C7 { get; set; } = new List<ArityChild>();
        public ICollection<ArityChild> C8 { get; set; } = new List<ArityChild>();
    }

    public sealed class ArityChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    [Fact]
    public void MoreThanSevenChildCollections_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        var parents = ctx.From<ArityParent>(b => b
            .HasMany(p => p.C1, c => c.ParentId)
            .HasMany(p => p.C2, c => c.ParentId)
            .HasMany(p => p.C3, c => c.ParentId)
            .HasMany(p => p.C4, c => c.ParentId)
            .HasMany(p => p.C5, c => c.ParentId)
            .HasMany(p => p.C6, c => c.ParentId)
            .HasMany(p => p.C7, c => c.ParentId)
            .HasMany(p => p.C8, c => c.ParentId));

        Action act = () => parents
            .JoinInto(ctx.From<ArityChild>(), (p, c) => p.Id == c.ParentId, p => p.C1)
            .JoinInto(ctx.From<ArityChild>(), (p, c) => p.Id == c.ParentId, p => p.C2)
            .JoinInto(ctx.From<ArityChild>(), (p, c) => p.Id == c.ParentId, p => p.C3)
            .JoinInto(ctx.From<ArityChild>(), (p, c) => p.Id == c.ParentId, p => p.C4)
            .JoinInto(ctx.From<ArityChild>(), (p, c) => p.Id == c.ParentId, p => p.C5)
            .JoinInto(ctx.From<ArityChild>(), (p, c) => p.Id == c.ParentId, p => p.C6)
            .JoinInto(ctx.From<ArityChild>(), (p, c) => p.Id == c.ParentId, p => p.C7)
            .JoinInto(ctx.From<ArityChild>(), (p, c) => p.Id == c.ParentId, p => p.C8)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*at most 7 child collections*");
    }

    [Fact]
    public void SemiJoinThenJoinInto_ShouldBeRejectedAtBuildTime()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<StitchParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

        Action act = () => ctx.From<StitchParent>()
            .SemiJoin(ctx.From<StitchNote>(), (p, n) => p.Id == n.ParentId)
            .JoinInto(ctx.From<StitchChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        act.Should().Throw<NotSupportedException>().WithMessage("*cannot be combined with other joins*");
    }

    [Fact]
    public void JoinIntoThenAntiJoin_ShouldBeRejectedAtBuildTime()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<StitchParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

        Action act = () => ctx.From<StitchParent>()
            .JoinInto(ctx.From<StitchChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .AntiJoin(ctx.From<StitchNote>(), (p, n) => p.Id == n.ParentId);

        act.Should().Throw<NotSupportedException>().WithMessage("*cannot be combined with other joins*");
    }

    [Fact]
    public void JoinIntoThenJoin_ShouldBeRejectedAtBuildTime()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<StitchParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

        Action act = () => ctx.From<StitchParent>()
            .JoinInto(ctx.From<StitchChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .Join(ctx.From<StitchNote>(), (p, n) => p.Id == n.ParentId);

        act.Should().Throw<NotSupportedException>().WithMessage("*cannot be combined with other joins*");
    }

    private static IEntityMetadata MetadataWithRelationship<T>(IRelationshipMetadata relationship)
    {
        // The navigation participates in the injected relationship, so build it as a declared navigation
        // (excluded from Properties) before swapping in the custom relationship list.
        var properties = new EntityMetadataBuilder<T>().Build().Properties;
        return new EntityMetadata("rejection_parent", properties, relationships: [relationship]);
    }

    private sealed class CompositeKeyRelationship : IRelationshipMetadata
    {
        public CompositeKeyRelationship(IPropertyMetadata parentKey, IPropertyMetadata childKey)
        {
            ForeignKey = [childKey, childKey];
            PrincipalKey = [parentKey, parentKey];
        }

        public RelationshipKind Kind => RelationshipKind.OneToMany;
        public Type DeclaringType => typeof(RejectParent);
        public Type RelatedType => typeof(RejectChild);
        public PropertyInfo? Navigation => typeof(RejectParent).GetProperty(nameof(RejectParent.Children));
        public bool IsCollection => true;
        public IReadOnlyList<IPropertyMetadata> ForeignKey { get; }
        public IReadOnlyList<IPropertyMetadata> PrincipalKey { get; }
        public IRelationshipMetadata? Inverse => null;
    }
}
