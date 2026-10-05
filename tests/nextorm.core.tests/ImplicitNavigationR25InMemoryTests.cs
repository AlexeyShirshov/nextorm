using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #148-B R2.5 acceptance closure (in-memory): A5 null-compensation for <c>!=</c>, negation, nullable
/// operands and OR; A10 metadata/registered datasets authoritative (the registered dataset wins over a
/// populated CLR graph) and a missing registered source is diagnosed instead of silently yielding an
/// empty result; A1 reject matrix on the in-memory provider.
/// </summary>
[Collection("Query cache controls")]
public class ImplicitNavigationR25InMemoryTests
{
    public ImplicitNavigationR25InMemoryTests()
    {
        DataContextCache.Clear();
    }

    private static InMemoryDataContext Create()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<R25MParent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        ctx.From<R25MChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        ctx.From<R25MParent>().WithData([new R25MParent { Id = 1, Age = 42, Name = "p1", Score = null }]);
        ctx.From<R25MChild>().WithData([
            new R25MChild { Id = 10, ParentId = 1, Active = true },
            new R25MChild { Id = 11, ParentId = 1, Active = false },
            new R25MChild { Id = 12, ParentId = 999, Active = true },
            new R25MChild { Id = 13, ParentId = 999, Active = false },
        ]);
        return ctx;
    }

    // ---- A5: null compensation in predicates and projections ------------------------------------

    [Fact]
    public void Null_compensation_should_hold_for_or_not_equal_nullable_and_negation()
    {
        using var ctx = Create();

        var or = ctx.From<R25MChild>()
            .Where(c => c.Active || c.Parent!.Name == "p1")
            .Select(c => c.Id)
            .ToList();
        or.Should().BeEquivalentTo(new[] { 10, 11, 12 }, "an active row whose principal is absent must survive the OR");

        var notEqual = ctx.From<R25MChild>()
            .Where(c => c.Parent!.Name != "p1")
            .Select(c => c.Id)
            .ToList();
        notEqual.Should().BeEquivalentTo(new[] { 12, 13 }, "an absent principal satisfies != because its value is NULL");

        var negation = ctx.From<R25MChild>()
            .Where(c => !(c.Parent!.Name == "p1"))
            .Select(c => c.Id)
            .ToList();
        negation.Should().BeEquivalentTo(new[] { 12, 13 }, "a negated navigation equality must include the absent principal");

        var nullable = ctx.From<R25MChild>()
            .Where(c => c.Parent!.Score == null)
            .Select(c => c.Id)
            .ToList();
        nullable.Should().BeEquivalentTo(new[] { 10, 11, 12, 13 }, "an absent principal OR a NULL column satisfies == null");

        // A nullable/negative comparison must not silently include the absent principal.
        var greater = ctx.From<R25MChild>()
            .Where(c => (int?)c.Parent!.Age > 5)
            .Select(c => c.Id)
            .ToList();
        greater.Should().BeEquivalentTo(new[] { 10, 11 }, "an absent principal cannot satisfy a lifted relational comparison");
    }

    [Fact]
    public void Null_compensation_should_hold_in_a_bool_value_projection()
    {
        using var ctx = Create();

        var rows = ctx.From<R25MChild>()
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, Different = c.Parent!.Name != "p1" })
            .ToList();

        rows.Single(r => r.Id == 10).Different.Should().BeFalse();
        rows.Single(r => r.Id == 11).Different.Should().BeFalse();
        rows.Single(r => r.Id == 12).Different.Should().BeTrue("the absent principal is NULL, which is different from 'p1'");
        rows.Single(r => r.Id == 13).Different.Should().BeTrue();
    }

    // ---- A10: metadata and registered datasets are authoritative ---------------------------------

    [Fact]
    public void Collection_terminals_should_use_the_registered_dataset_not_the_clr_graph()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<R25MParent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        ctx.From<R25MChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));

        // The CLR graph claims five children; the registered dataset has two.
        ctx.From<R25MParent>().WithData([
            new R25MParent
            {
                Id = 1,
                Children =
                [
                    new R25MChild { Id = 901 }, new R25MChild { Id = 902 }, new R25MChild { Id = 903 },
                    new R25MChild { Id = 904 }, new R25MChild { Id = 905 },
                ],
            },
        ]);
        ctx.From<R25MChild>().WithData([
            new R25MChild { Id = 10, ParentId = 1, Active = true },
            new R25MChild { Id = 11, ParentId = 1, Active = false },
        ]);

        var rows = ctx.From<R25MParent>()
            .Select(p => new { p.Id, Has = p.Children.Any(), C = p.Children.Count() })
            .ToList();

        rows.Single().Has.Should().BeTrue();
        rows.Single().C.Should().Be(2, "the registered dataset is authoritative, never the populated CLR collection");
    }

    [Fact]
    public void Missing_registered_source_for_a_collection_should_be_diagnosed()
    {
        using var ctx = new InMemoryDataContext();
        // The declared relationship points at R25MChild, but the dataset/mapping is never registered.
        ctx.From<R25MParent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        ctx.From<R25MParent>().WithData([new R25MParent { Id = 1 }]);

        Action act = () => ctx.From<R25MParent>().Select(p => p.Children.Any()).ToList();

        act.Should().Throw<BuildSqlCommandException>()
            .WithMessage("*Table name is not registered*R25MChild*");
    }

    [Fact]
    public void Missing_registered_source_for_a_reference_should_be_diagnosed()
    {
        using var ctx = new InMemoryDataContext();
        // The declared reference points at R25MParent, but the dataset/mapping is never registered.
        ctx.From<R25MChild>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        ctx.From<R25MChild>().WithData([new R25MChild { Id = 1, ParentId = 99 }]);

        Action act = () => ctx.From<R25MChild>().Select(c => c.Parent!.Name).ToList();

        act.Should().Throw<BuildSqlCommandException>()
            .WithMessage("*Table name is not registered*R25MParent*");
    }

    // ---- A1: reject matrix on the in-memory provider ---------------------------------------------

    // A composite relationship key is unreachable through the fluent single-key API, so the metadata is
    // built directly here to pin the resolver's fail-closed composite-key guard end to end.
    [Fact]
    public void Composite_relationship_key_should_fail_closed()
    {
        var parentProperties = new EntityMetadataBuilder<R25CompositeParent>().Build().Properties;
        var childProperties = new EntityMetadataBuilder<R25CompositeChild>().Build().Properties;
        var parentKey = parentProperties.First(p => p.PropertyInfo.Name == nameof(R25CompositeParent.Id));
        var childKey = childProperties.First(p => p.PropertyInfo.Name == nameof(R25CompositeChild.ParentId));

        DataContextCache.Metadata[typeof(R25CompositeParent)] = new EntityMetadata(
            "r25m_composite_parent",
            parentProperties,
            relationships: [new CompositeKeyRelationship(parentKey, childKey)]);

        using var ctx = new InMemoryDataContext();
        Action act = () => ctx.From<R25CompositeParent>().Select(p => p.Children.Any()).ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*composite key*");
    }

    [Fact]
    public void Every_unsupported_collection_operator_should_fail_closed()
    {
        using var ctx = Create();

        Action[] rejects =
        [
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.OrderBy(c => c.Id) }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.OrderByDescending(c => c.Id) }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.Skip(1) }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.Take(1) }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.Distinct() }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.GroupBy(c => c.Id) }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.Join(p.Children, a => a.Id, b => b.Id, (a, b) => a) }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.Union(p.Children) }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.Except(p.Children) }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.Intersect(p.Children) }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.SelectMany(c => p.Children) }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.Single() }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.Last() }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.Average(c => c.Id) }).ToList(),
            () => ctx.From<R25MParent>().Select(p => new { p.Id, X = p.Children.Contains(new R25MChild { Id = 1 }) }).ToList(),
        ];

        for (var i = 0; i < rejects.Length; i++)
            rejects[i].Should().Throw<NotSupportedException>($"reject form #{i} must fail closed");
    }
}

[SqlTable("r25m_parent")]
public sealed class R25MParent
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("age")]
    public int Age { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("name")]
    public string? Name { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("score")]
    public int? Score { get; set; }

    public ICollection<R25MChild> Children { get; set; } = new List<R25MChild>();
}

[SqlTable("r25m_child")]
public sealed class R25MChild
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("parent_id")]
    public int ParentId { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("active")]
    public bool Active { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("name")]
    public string? Name { get; set; }

    public R25MParent? Parent { get; set; }
}

[SqlTable("r25m_composite_parent")]
public sealed class R25CompositeParent
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    public ICollection<R25CompositeChild> Children { get; set; } = new List<R25CompositeChild>();
}

[SqlTable("r25m_composite_child")]
public sealed class R25CompositeChild
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("id")]
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("parent_id")]
    public int ParentId { get; set; }
}

file sealed class CompositeKeyRelationship : IRelationshipMetadata
{
    public CompositeKeyRelationship(IPropertyMetadata parentKey, IPropertyMetadata childKey)
    {
        ForeignKey = [childKey, childKey];
        PrincipalKey = [parentKey, parentKey];
    }

    public RelationshipKind Kind => RelationshipKind.OneToMany;
    public Type DeclaringType => typeof(R25CompositeParent);
    public Type RelatedType => typeof(R25CompositeChild);
    public System.Reflection.PropertyInfo? Navigation => typeof(R25CompositeParent).GetProperty(nameof(R25CompositeParent.Children));
    public bool IsCollection => true;
    public IReadOnlyList<IPropertyMetadata> ForeignKey { get; }
    public IReadOnlyList<IPropertyMetadata> PrincipalKey { get; }
    public IRelationshipMetadata? Inverse => null;
}
