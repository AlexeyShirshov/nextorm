using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T331 AC01/AC02: the navigation-first <c>JoinInto</c> surface. The navigation selects the declared
/// relationship, so the child type is inferred from the navigation and no key is restated.
/// <para>
/// The two new overloads are distinguished from the ten legacy ones by their first parameter: a single
/// navigation lambda (<c>Func&lt;TEntity, TChild&gt;</c>) whose second type argument <em>is</em> the child,
/// not a two-argument join predicate. The collection form must win overload resolution for an
/// <c>ICollection&lt;T&gt;</c> or concrete-collection navigation, and the reference form must be the only
/// one that constrains the child to a reference type.
/// </para>
/// <para>
/// Runs in the "Query cache controls" collection because it writes the process-wide
/// <see cref="DataContextCache"/>.
/// </para>
/// </summary>
[Collection("Query cache controls")]
[Trait("Category", "T331")]
public class T331JoinIntoApiTests
{
    public T331JoinIntoApiTests()
    {
        DataContextCache.Clear();
    }

    // ---------------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------------

    public sealed class ApiParent
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public ICollection<ApiChild> Children { get; set; } = new List<ApiChild>();

        public List<ApiChild> ListChildren { get; set; } = [];

        public ApiDetail? Detail { get; set; }

        public ApiDetail RequiredDetail { get; set; } = null!;

        public ICollection<int> Numbers { get; set; } = new List<int>();
    }

    public sealed class ApiChild
    {
        public int Id { get; set; }

        public int ParentId { get; set; }

        public string? Name { get; set; }
    }

    public sealed class ApiDetail
    {
        public int Id { get; set; }

        public int ParentId { get; set; }

        public string? Name { get; set; }
    }

    /// <summary>A value-type "child": no entity navigation can target it.</summary>
    public struct ApiValueChild
    {
        public int Id { get; set; }
    }

    public sealed class ApiStructParent
    {
        public int Id { get; set; }

        public ApiValueChild? Value { get; set; }
    }

    /// <summary>Parent whose navigations point at a genuinely unresolvable child mapping.</summary>
    public sealed class ApiUnresolvableParent
    {
        public int Id { get; set; }

        public ICollection<ApiUnresolvableChild> Children { get; set; } = new List<ApiUnresolvableChild>();

        public ApiUnresolvableChild? Detail { get; set; }
    }

    /// <summary>Two <c>[DynamicColumns]</c> stores make the metadata build throw.</summary>
    public sealed class ApiUnresolvableChild
    {
        public int Id { get; set; }

        [DynamicColumns]
        public Dictionary<string, object?> First { get; set; } = new();

        [DynamicColumns]
        public Dictionary<string, object?> Second { get; set; } = new();
    }

    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<ApiParent>(b => b
                .HasMany(p => p.Children, c => c.ParentId)
                .HasMany(p => p.ListChildren, c => c.ParentId)
                .HasOneToOne(p => p.Detail, p => p.Id, d => d.ParentId)
                .HasOneToOne(p => p.RequiredDetail, p => p.Id, d => d.ParentId))
            .WithData([
                new ApiParent { Id = 1, Name = "p1" },
                new ApiParent { Id = 2, Name = "p2" },
            ]);
        ctx.From<ApiChild>().WithData([
            new ApiChild { Id = 10, ParentId = 1, Name = "c1" },
            new ApiChild { Id = 11, ParentId = 2, Name = "c2" },
        ]);
        ctx.From<ApiDetail>().WithData([
            new ApiDetail { Id = 100, ParentId = 1, Name = "d1" },
        ]);
        return ctx;
    }

    private static Exception ObserveResolutionFailure(InMemoryDataContext ctx)
        => FluentActions.Invoking(() => { _ = ctx.From<ApiUnresolvableChild>(); })
            .Should().Throw<Exception>().Which;

    // ---------------------------------------------------------------------------------------------
    // AC01 — reference navigation: binding, inference, required and nullable references.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AC01_ReferenceNavigation_ShouldBindAndAssignWithNoExplicitTypeArgument()
    {
        using var ctx = CreateContext();

        // No <TChild> argument on either call: the child type comes from the navigation.
        var byNullable = ctx.From<ApiParent>().JoinInto(p => p.Detail).ToList();
        var byRequired = ctx.From<ApiParent>().JoinInto(p => p.RequiredDetail).ToList();

        byNullable.Should().HaveCount(2);
        byNullable.Single(p => p.Id == 1).Detail!.Id.Should().Be(100);
        byNullable.Single(p => p.Id == 2).Detail.Should().BeNull("a left join keeps the parent without a child");

        byRequired.Should().HaveCount(2);
        byRequired.Single(p => p.Id == 1).RequiredDetail!.Id.Should().Be(100);
        byRequired.Single(p => p.Id == 2).RequiredDetail.Should().BeNull("the join is a left join even for a required reference");
    }

    [Fact]
    public void AC01_ReferenceNavigation_WithKeyRestatingFilter_ShouldYieldTheDeclaredMatches()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<ApiParent>()
            .JoinInto(p => p.Detail, (p, d) => p.Id == d.ParentId)
            .ToList();

        parents.Should().HaveCount(2);
        parents.Single(p => p.Id == 1).Detail!.Id.Should().Be(100);
    }

    [Fact]
    public void AC01_NullNavigation_ShouldThrowBeforeOptionsAndChildResolution()
    {
        using var ctx = new InMemoryDataContext();
        var resolution = ObserveResolutionFailure(ctx);
        var callbackCalls = 0;
        var sentinel = new InvalidOperationException("the options callback must not run");
        Expression<Func<ApiUnresolvableParent, ApiUnresolvableChild?>> navigation = null!;

        var observed = FluentActions.Invoking(() => ctx.From<ApiUnresolvableParent>()
                .JoinInto(navigation, options: _ => { callbackCalls++; throw sentinel; }))
            .Should().Throw<ArgumentNullException>().Which;

        observed.ParamName.Should().Be("navigation");
        observed.GetType().Should().NotBe(resolution.GetType(),
            "the guard must win over child-source resolution, so nothing is resolved and no SQL can be built");
        callbackCalls.Should().Be(0, "the options callback runs only after the navigation guard");
    }

    [Fact]
    public void AC01_NonPropertyNavigation_ShouldFailThroughTheEstablishedResolverPath()
    {
        using var ctx = CreateContext();
        var callbackCalls = 0;

        var observed = FluentActions.Invoking(() => ctx.From<ApiParent>()
                .JoinInto(p => p.Id == 1 ? p.Detail : null, options: _ => callbackCalls++))
            .Should().Throw<NotSupportedException>().Which;

        observed.Message.Should().Contain("requires the navigation selector to be a property access");
        callbackCalls.Should().Be(1, "the options callback runs once, before the relationship is resolved");
    }

    // ---------------------------------------------------------------------------------------------
    // AC02 — collection navigation: ICollection<T>, concrete collection, and the negatives.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AC02_DeclaredCollection_ShouldBindAndGroupWithNoExplicitTypeArgument()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<ApiParent>().JoinInto(p => p.Children).ToList();

        parents.Should().HaveCount(2);
        parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10);
        parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(11);
    }

    [Fact]
    public void AC02_ConcreteCollection_ShouldBindAndGroupWithNoExplicitTypeArgument()
    {
        using var ctx = CreateContext();

        var parents = ctx.From<ApiParent>().JoinInto(p => p.ListChildren).ToList();

        parents.Should().HaveCount(2);
        parents.Single(p => p.Id == 1).ListChildren.Select(c => c.Id).Should().Equal(10);
        parents.Single(p => p.Id == 2).ListChildren.Select(c => c.Id).Should().Equal(11);
    }

    [Fact]
    public void AC02_CollectionNavigation_ShouldNotBindAsAReferenceChild()
    {
        using var ctx = CreateContext();

        // Forcing the reference form with the collection type as the child type is the shape a wrong
        // overload resolution would silently choose; it must keep failing through the resolver.
        var observed = FluentActions.Invoking(() => ctx.From<ApiParent>()
                .JoinInto<ICollection<ApiChild>>(p => (ICollection<ApiChild>?)p.Children))
            .Should().Throw<NotSupportedException>().Which;

        observed.Message.Should().Contain("use the collection JoinInto overload");
    }

    [Fact]
    public void AC02_NonPropertyCollection_ShouldFailClosed()
    {
        using var ctx = CreateContext();

        FluentActions.Invoking(() => ctx.From<ApiParent>()
                .JoinInto(p => (ICollection<ApiChild>)(p.Id == 1 ? p.Children : p.ListChildren)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*requires the collection selector to be a property access*");
    }

    [Fact]
    public void AC02_ValueTypeCollection_ShouldFailClosed()
    {
        using var ctx = CreateContext();

        FluentActions.Invoking(() => ctx.From<ApiParent>().JoinInto(p => p.Numbers))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*requires an explicitly declared relationship*");
    }

    [Fact]
    public void AC02_ConcreteCollection_ShouldBeRoutedByOverloadResolutionPriority()
    {
        // Both navigation-first overloads are applicable to `p => p.ListChildren`: the concrete type is
        // an exact match for the reference form's `TChild?` and a convert-to-interface match for the
        // collection form, and plain C# overload resolution prefers the reference form (the concrete type
        // is "more specific" than ICollection<TChild>). Without the priority the call would bind the
        // reference overload and throw "supports only a OneToOne relationship ... use the collection
        // JoinInto overload". The resolving rule is asserted here, not just relied on.
        var collectionForm = typeof(EntityBuilder<ApiParent>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == nameof(EntityBuilder<ApiParent>.JoinInto) && m.IsGenericMethodDefinition)
            .Where(IsNavigationFirst)
            .Single(IsCollectionForm);

        var referenceForm = typeof(EntityBuilder<ApiParent>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == nameof(EntityBuilder<ApiParent>.JoinInto) && m.IsGenericMethodDefinition)
            .Where(IsNavigationFirst)
            .Single(m => !IsCollectionForm(m));

        collectionForm.GetCustomAttribute<OverloadResolutionPriorityAttribute>().Should().NotBeNull();
        collectionForm.GetCustomAttribute<OverloadResolutionPriorityAttribute>()!.Priority.Should().BeGreaterThan(0);
        referenceForm.GetCustomAttribute<OverloadResolutionPriorityAttribute>().Should().BeNull(
            "the reference form keeps the default priority; only the collection form needs to outrank it");
    }

    [Fact]
    public void AC02_UntypedNullFilter_ShouldResolveOnBothFamiliesWithTheSameMeaning()
    {
        using var ctx = CreateContext();

        // `JoinInto(p => p.Children, null)` has no lambda in the second argument to disambiguate on: both
        // navigation-first forms take the same filter type there. The call must therefore not become an
        // ambiguity (CS0121) unless the legacy/T330 pair does too; this test pins that both families
        // accept the untyped null and give it the same meaning (no additional restriction).
        var navigationFirst = ctx.From<ApiParent>().JoinInto(p => p.Children, null).ToList();
        var sourceLessLegacy = ctx.From<ApiParent>()
            .JoinInto<ApiChild>((p, c) => p.Id == c.ParentId, p => p.Children, null)
            .ToList();

        navigationFirst.Should().HaveCount(2);
        navigationFirst.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10);
        navigationFirst.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(11);

        sourceLessLegacy.Should().HaveCount(2);
        sourceLessLegacy.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10);
        sourceLessLegacy.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(11);
    }

    // ---------------------------------------------------------------------------------------------
    // Surface shape — exactly two navigation-first overloads, no JoinType parameter, one class constraint.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AC01_AC02_NewSurface_ShouldBeExactlyTwoNavigationFirstOverloads()
    {
        var navigationFirst = typeof(EntityBuilder<ApiParent>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == nameof(EntityBuilder<ApiParent>.JoinInto) && m.IsGenericMethodDefinition)
            .Where(IsNavigationFirst)
            .ToList();

        navigationFirst.Should().HaveCount(2, "the navigation-first family is a reference form and a collection form");

        navigationFirst.Should().OnlyContain(
            m => m.GetParameters().Length == 3
                 && m.GetParameters()[0].ParameterType != null
                 && m.GetParameters()[2].ParameterType == typeof(Action<JoinOptions>),
            "each form takes the navigation, an optional filter and optional join options");

        navigationFirst.Should().NotContain(
            m => m.GetParameters().Any(p => p.ParameterType == typeof(JoinType)),
            "the navigation-first forms are implicitly LEFT joins and expose no JoinType");

        navigationFirst.Count(HasReferenceTypeConstraint).Should().Be(1,
            "only the reference form restricts the child to a reference type; the collection form matches the legacy collection family");

        navigationFirst.Should().ContainSingle(m => IsCollectionForm(m), "exactly one form takes an ICollection<TChild> navigation");
        navigationFirst.Should().ContainSingle(
            m => !IsCollectionForm(m) && HasReferenceTypeConstraint(m),
            "the other form takes a single reference navigation and constrains it to a reference type");
    }

    private static bool IsNavigationFirst(MethodInfo method)
    {
        var first = method.GetParameters()[0].ParameterType;
        if (!first.IsGenericType || first.GetGenericTypeDefinition() != typeof(Expression<>))
            return false;

        var lambda = first.GetGenericArguments()[0];
        return lambda.IsGenericType && lambda.GetGenericTypeDefinition() == typeof(Func<,>);
    }

    private static bool IsCollectionForm(MethodInfo method)
    {
        var navigation = method.GetParameters()[0].ParameterType.GetGenericArguments()[0];
        var target = navigation.GetGenericArguments()[1];
        return target.IsGenericType
               && target.GetGenericTypeDefinition() == typeof(ICollection<>)
               && target.GetGenericArguments()[0] == method.GetGenericArguments()[0];
    }

    private static bool HasReferenceTypeConstraint(MethodInfo method)
        => method.GetGenericArguments()[0].GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint);
}
