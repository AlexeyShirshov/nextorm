using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// #148-B D2: locks the public adapter markers, the native <c>LongCount</c> terminal surface and the
/// wide-count narrowing helper added before the navigation rewrite consumes them.
/// </summary>
public class ImplicitNavigationD2Tests
{
    [Fact]
    public void AsEntityBuilder_collection_marker_outside_expression_throws()
    {
        var source = new List<SimpleEntity> { new() { Id = 1 } };

        Action act = () => source.AsEntityBuilder();

        act.Should().Throw<NotSupportedException>(
            "AsEntityBuilder is an expression-only marker, never a materializable adapter");
    }

    [Fact]
    public void AsEntityBuilder_reference_marker_outside_expression_throws()
    {
        object? navigation = new SimpleEntity { Id = 1 };

        Action act = () => navigation.AsEntityBuilder<SimpleEntity>();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void AsEntityBuilder_reference_marker_outside_expression_throws_on_null()
    {
        object? navigation = null;

        Action act = () => navigation.AsEntityBuilder<SimpleEntity>();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void LongCount_exposes_a_native_64bit_terminal()
    {
        var countMethods = typeof(EntityBuilderExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(EntityBuilderExtensions.LongCount))
            .ToArray();

        countMethods.Should().NotBeEmpty();
        countMethods.Should().OnlyContain(m => m.ReturnType == typeof(long));
        countMethods.Should().OnlyContain(m =>
            m.GetParameters()[0].ParameterType.IsGenericType
            && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(EntityBuilder<>));

        var asyncMethod = typeof(EntityBuilderExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(EntityBuilderExtensions.LongCountAsync)
                && m.GetParameters()[1].ParameterType == typeof(CancellationToken));

        asyncMethod.ReturnType.Should().Be(typeof(Task<long>));
    }

    [Fact]
    public void WideCountNarrowing_emits_a_checked_conversion_for_the_count_form()
    {
        var scalar = Expression.Constant(3L);

        var narrowed = WideCountNarrowing.ForCount(scalar);

        narrowed.NodeType.Should().Be(ExpressionType.ConvertChecked);
        narrowed.Type.Should().Be(typeof(int));
        ((UnaryExpression)narrowed).Operand.Should().BeSameAs(scalar);
    }

    [Fact]
    public void WideCountNarrowing_passes_the_long_count_through()
    {
        var scalar = Expression.Constant(3L);

        WideCountNarrowing.ForLongCount(scalar).Should().BeSameAs(scalar);
    }
}
