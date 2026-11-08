using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T249-D1 step-0 overload probe: every normative and matrix <c>LoadWith</c> form is compiled as a real
/// call on <see cref="EntityBuilder{T}"/> and resolved at runtime by the fail-closed metadata binding
/// each additive overload performs, so the assertion pins the exact overload the compiler chose. The
/// fixtures declare no relationship metadata, so every additive call must be rejected with a
/// <see cref="NotSupportedException"/> naming the overload form (<c>LoadWith(collection)</c>,
/// <c>LoadWith(reference, childQuery)</c>, ...); the null-factory cases prove the factory overload by its
/// argument guard. The signatures probed here are the frozen D2 contract.
/// </summary>
[Trait("Feature", "T249-D1")]
public sealed class MetadataLoadWithBindingTests
{
    private const string CollectionShort = "LoadWith(collection)";
    private const string CollectionFactory = "LoadWith(collection, childQuery)";
    private const string ReferenceShort = "LoadWith(reference)";
    private const string ReferenceFactory = "LoadWith(reference, childQuery)";

    public sealed class ProbeOrder
    {
        public int Id { get; set; }
        public ICollection<ProbeItem> Items { get; set; } = new List<ProbeItem>();
        public List<ProbeItem> ItemList { get; set; } = [];
        public ProbeCustomer? Customer { get; set; }
        public ProbeCustomer RequiredCustomer { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
    }

    public sealed class ProbeItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public bool Active { get; set; }
        public ProbeProduct? Product { get; set; }
    }

    public sealed class ProbeProduct
    {
        public int Id { get; set; }
    }

    public sealed class ProbeCustomer
    {
        public int Id { get; set; }
        public bool Active { get; set; }
    }

    private static InMemoryDataContext CreateContext() => new();

    private static void AssertBindsTo(string expectedForm, Action call)
    {
        call.Should().Throw<NotSupportedException>(
                "the fail-closed binding of the selected overload identifies the overload the compiler chose")
            .WithMessage("*" + expectedForm + "*");
    }

    // ---------------------------------------------------------------------------------------------
    // Reference navigation: nullable and non-nullable annotated property x short/factory x mode forms.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Binding_ReferenceShort_NullableNavigation()
    {
        using var context = CreateContext();

        AssertBindsTo(ReferenceShort, () => context.From<ProbeOrder>().LoadWith(o => o.Customer));
    }

    [Fact]
    public void MetadataLoadWith_Binding_ReferenceShort_NonNullableNavigation()
    {
        using var context = CreateContext();

        AssertBindsTo(ReferenceShort, () => context.From<ProbeOrder>().LoadWith(o => o.RequiredCustomer));
    }

    [Fact]
    public void MetadataLoadWith_Binding_ReferenceFactory_NullableNavigation()
    {
        using var context = CreateContext();

        AssertBindsTo(ReferenceFactory, () => context.From<ProbeOrder>().LoadWith(
            o => o.Customer,
            c => c.From<ProbeCustomer>()));
    }

    [Fact]
    public void MetadataLoadWith_Binding_ReferenceFactory_NonNullableNavigation()
    {
        using var context = CreateContext();

        AssertBindsTo(ReferenceFactory, () => context.From<ProbeOrder>().LoadWith(
            o => o.RequiredCustomer,
            c => c.From<ProbeCustomer>()));
    }

    [Fact]
    public void MetadataLoadWith_Binding_ReferenceShort_SplitQuery()
    {
        using var context = CreateContext();

        AssertBindsTo(ReferenceShort, () => context.From<ProbeOrder>().LoadWith(
            o => o.Customer,
            EagerLoadMode.SplitQuery));
    }

    [Fact]
    public void MetadataLoadWith_Binding_ReferenceShort_SingleQuery()
    {
        using var context = CreateContext();

        AssertBindsTo(ReferenceShort, () => context.From<ProbeOrder>().LoadWith(
            o => o.Customer,
            EagerLoadMode.SingleQuery));
    }

    [Fact]
    public void MetadataLoadWith_Binding_ReferenceFactory_SplitQuery()
    {
        using var context = CreateContext();

        AssertBindsTo(ReferenceFactory, () => context.From<ProbeOrder>().LoadWith(
            o => o.Customer,
            c => c.From<ProbeCustomer>(),
            EagerLoadMode.SplitQuery));
    }

    [Fact]
    public void MetadataLoadWith_Binding_ReferenceFactory_SingleQuery()
    {
        using var context = CreateContext();

        AssertBindsTo(ReferenceFactory, () => context.From<ProbeOrder>().LoadWith(
            o => o.RequiredCustomer,
            c => c.From<ProbeCustomer>(),
            EagerLoadMode.SingleQuery));
    }

    [Fact]
    public void MetadataLoadWith_Binding_ReferenceFactory_NullFactoryCast()
    {
        using var context = CreateContext();

        Action act = () => context.From<ProbeOrder>().LoadWith(
            o => o.Customer,
            (Func<IDataContext, EntityBuilder<ProbeCustomer>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("childQuery", "only the factory overload reads a supplied child query");
    }

    [Fact]
    public void MetadataLoadWith_Binding_ReferenceFactory_NormativeNestedExample()
    {
        using var context = CreateContext();

        AssertBindsTo(ReferenceFactory, () => context.From<ProbeOrder>().LoadWith(
            o => o.Customer,
            c => c.From<ProbeCustomer>().Where(x => x.Active)));
    }

    // ---------------------------------------------------------------------------------------------
    // Collection navigation: declared ICollection<T> and List<T> must both bind the collection route.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Binding_CollectionShort_ICollection()
    {
        using var context = CreateContext();

        AssertBindsTo(CollectionShort, () => context.From<ProbeOrder>().LoadWith(o => o.Items));
    }

    [Fact]
    public void MetadataLoadWith_Binding_CollectionShort_List()
    {
        using var context = CreateContext();

        AssertBindsTo(CollectionShort, () => context.From<ProbeOrder>().LoadWith(o => o.ItemList));
    }

    [Fact]
    public void MetadataLoadWith_Binding_CollectionFactory_ICollection()
    {
        using var context = CreateContext();

        AssertBindsTo(CollectionFactory, () => context.From<ProbeOrder>().LoadWith(
            o => o.Items,
            c => c.From<ProbeItem>()));
    }

    [Fact]
    public void MetadataLoadWith_Binding_CollectionFactory_List()
    {
        using var context = CreateContext();

        AssertBindsTo(CollectionFactory, () => context.From<ProbeOrder>().LoadWith(
            o => o.ItemList,
            c => c.From<ProbeItem>()));
    }

    [Fact]
    public void MetadataLoadWith_Binding_CollectionShort_SplitQuery()
    {
        using var context = CreateContext();

        AssertBindsTo(CollectionShort, () => context.From<ProbeOrder>().LoadWith(
            o => o.Items,
            EagerLoadMode.SplitQuery));
    }

    [Fact]
    public void MetadataLoadWith_Binding_CollectionShort_SingleQuery_List()
    {
        using var context = CreateContext();

        AssertBindsTo(CollectionShort, () => context.From<ProbeOrder>().LoadWith(
            o => o.ItemList,
            EagerLoadMode.SingleQuery));
    }

    [Fact]
    public void MetadataLoadWith_Binding_CollectionFactory_SingleQuery_List()
    {
        using var context = CreateContext();

        AssertBindsTo(CollectionFactory, () => context.From<ProbeOrder>().LoadWith(
            o => o.ItemList,
            c => c.From<ProbeItem>(),
            EagerLoadMode.SingleQuery));
    }

    [Fact]
    public void MetadataLoadWith_Binding_CollectionFactory_NullFactoryCast()
    {
        using var context = CreateContext();

        Action act = () => context.From<ProbeOrder>().LoadWith(
            o => o.Items,
            (Func<IDataContext, EntityBuilder<ProbeItem>>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("childQuery", "only the factory overload reads a supplied child query");
    }

    [Fact]
    public void MetadataLoadWith_Binding_CollectionFactory_NormativeNestedExample()
    {
        using var context = CreateContext();

        AssertBindsTo(CollectionFactory, () => context.From<ProbeOrder>().LoadWith(
            o => o.Items,
            c => c.From<ProbeItem>().Where(i => i.Active).LoadWith(i => i.Product)));
    }

    // ---------------------------------------------------------------------------------------------
    // default literal and default(EagerLoadMode) in the mode position.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_DefaultLiteral_ReferenceShort()
    {
        using var context = CreateContext();

        AssertBindsTo(ReferenceShort, () => context.From<ProbeOrder>().LoadWith(o => o.Customer, default));
    }

    [Fact]
    public void MetadataLoadWith_DefaultLiteral_ReferenceFactory()
    {
        using var context = CreateContext();

        AssertBindsTo(ReferenceFactory, () => context.From<ProbeOrder>().LoadWith(
            o => o.Customer,
            c => c.From<ProbeCustomer>(),
            default));
    }

    [Fact]
    public void MetadataLoadWith_DefaultLiteral_ReferenceShort_ExplicitModeType()
    {
        using var context = CreateContext();

        AssertBindsTo(ReferenceShort, () => context.From<ProbeOrder>().LoadWith(
            o => o.RequiredCustomer,
            default(EagerLoadMode)));
    }

    [Fact]
    public void MetadataLoadWith_DefaultLiteral_ReferenceFactory_ExplicitModeType()
    {
        using var context = CreateContext();

        AssertBindsTo(ReferenceFactory, () => context.From<ProbeOrder>().LoadWith(
            o => o.RequiredCustomer,
            c => c.From<ProbeCustomer>(),
            default(EagerLoadMode)));
    }

    [Fact]
    public void MetadataLoadWith_DefaultLiteral_CollectionShort()
    {
        using var context = CreateContext();

        AssertBindsTo(CollectionShort, () => context.From<ProbeOrder>().LoadWith(o => o.Items, default));
    }

    [Fact]
    public void MetadataLoadWith_DefaultLiteral_CollectionFactory()
    {
        using var context = CreateContext();

        AssertBindsTo(CollectionFactory, () => context.From<ProbeOrder>().LoadWith(
            o => o.Items,
            c => c.From<ProbeItem>(),
            default));
    }

    [Fact]
    public void MetadataLoadWith_DefaultLiteral_CollectionShort_ExplicitModeType()
    {
        using var context = CreateContext();

        AssertBindsTo(CollectionShort, () => context.From<ProbeOrder>().LoadWith(
            o => o.ItemList,
            default(EagerLoadMode)));
    }

    [Fact]
    public void MetadataLoadWith_DefaultLiteral_CollectionFactory_ExplicitModeType()
    {
        using var context = CreateContext();

        AssertBindsTo(CollectionFactory, () => context.From<ProbeOrder>().LoadWith(
            o => o.ItemList,
            c => c.From<ProbeItem>(),
            default(EagerLoadMode)));
    }

    // ---------------------------------------------------------------------------------------------
    // The unchanged legacy 5-parameter overload still binds for its 4- and 5-argument call shapes.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_LegacyBinding_FourArguments()
    {
        using var context = CreateContext();

        var builder = context.From<ProbeOrder>()
            .LoadWith(o => o.Items, c => c.From<ProbeItem>(), o => o.Id, i => i.OrderId);

        builder.LoadSpecs.Should().HaveCount(1, "the legacy body registered the declaration");
        builder.LoadSpecs![0].CollectionMember.Should().NotBeNull();
    }

    [Fact]
    public void MetadataLoadWith_LegacyBinding_FiveArgumentsWithMode()
    {
        using var context = CreateContext();

        var builder = context.From<ProbeOrder>()
            .LoadWith(o => o.Items, c => c.From<ProbeItem>(), o => o.Id, i => i.OrderId, EagerLoadMode.SingleQuery);

        builder.LoadSpecs.Should().HaveCount(1, "the legacy body registered the declaration");
        builder.SingleQuery.Should().BeTrue("the explicit mode reached the legacy state machine");
    }

    // ---------------------------------------------------------------------------------------------
    // Surface: five overloads exist; the legacy one is untouched; the additive ones keep the optional
    // EagerLoadMode parameter whose default value is Default.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Binding_OverloadSurface()
    {
        var overloads = typeof(EntityBuilder<ProbeOrder>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == nameof(EntityBuilder<ProbeOrder>.LoadWith))
            .ToArray();

        overloads.Should().HaveCount(5);

        var legacy = overloads.Single(m => m.GetGenericArguments().Length == 2);
        legacy.GetParameters().Select(p => p.ParameterType.Name).Should().Equal(
            "Expression`1", "Func`2", "Expression`1", "Expression`1", "EagerLoadMode");
        legacy.GetParameters()[4].IsOptional.Should().BeTrue();
        legacy.GetParameters()[4].DefaultValue.Should().Be(EagerLoadMode.Default);

        var additive = overloads.Where(m => m.GetGenericArguments().Length == 1).ToArray();
        additive.Should().HaveCount(4);
        additive.Select(m => m.GetParameters().Length).Should().BeEquivalentTo([2, 3, 2, 3]);
        foreach (var method in additive)
        {
            var last = method.GetParameters()[^1];
            last.ParameterType.Should().Be(typeof(EagerLoadMode));
            last.IsOptional.Should().BeTrue();
            last.DefaultValue.Should().Be(EagerLoadMode.Default);
        }
    }

    [Fact]
    public void MetadataLoadWith_Binding_ReferenceOverloadsCarryLowerPriority()
    {
        var overloads = typeof(EntityBuilder<ProbeOrder>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == nameof(EntityBuilder<ProbeOrder>.LoadWith))
            .ToArray();

        static bool IsReferenceRoute(MethodInfo m)
        {
            var first = m.GetParameters()[0].ParameterType;
            return first.IsGenericType
                   && first.GetGenericArguments()[0].GetGenericArguments()[1].IsGenericParameter;
        }

        foreach (var method in overloads)
        {
            var priority = method.GetCustomAttribute<OverloadResolutionPriorityAttribute>();
            if (IsReferenceRoute(method))
            {
                priority.Should().NotBeNull("the reference route only wins when the collection route does not apply");
                priority!.Priority.Should().Be(-1);
            }
            else
            {
                priority.Should().BeNull("the collection and legacy routes stay at the default priority 0");
            }
        }
    }
}
