using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T292 (#292) AC08 arm: the declared result type of the four variability terminals and of the eight
/// SQL facade forms over a NON-NULLABLE value-type selector. The unconstrained <c>TResult?</c>
/// annotation did not produce a nullable CLR type — closed over <c>double</c> the method returned
/// plain <see cref="double"/>, which is why the row mapper selected the unguarded getter and the
/// scalar terminal collapsed NULL to zero.
/// </summary>
/// <remarks>
/// Closed over a non-nullable <c>double</c> selector, every one of <c>Stdev</c>/<c>Stdevp</c>/
/// <c>Var</c>/<c>Varp</c> must return <see cref="Nullable{T}"/> of <c>double</c>, in each of its four
/// parameter shapes and for both selector nullabilities. The facade forms must expose the same
/// structurally nullable pairing.
/// </remarks>
public class T292VariabilityApiTests
{
    private static readonly string[] Families = ["Stdev", "Stdevp", "Var", "Varp"];

    private static readonly string[] Facades =
    [
        "stdev", "stdev_distinct", "stdevp", "stdevp_distinct",
        "var", "var_distinct", "varp", "varp_distinct",
    ];

    public static TheoryData<string> FamilyData => new(Families);

    public static TheoryData<string> FacadeData => new(Facades);

    [Theory]
    [MemberData(nameof(FamilyData))]
    public void NonNullableDoubleSelector_ShouldReturnNullableDouble(string family)
    {
        var overloads = SelectorOnlyOverloads(family)
            .Where(method => IsNonNullableSelector(method.GetParameters()[1].ParameterType))
            .ToList();

        overloads.Should().ContainSingle(
            "'{0}' must declare exactly one selector-only overload taking Expression<Func<TEntity, TResult>> "
            + "without a Nullable<TResult> selector", family);

        var closed = overloads[0].MakeGenericMethod(typeof(T292ApiEntity), typeof(double));

        closed.ReturnType.Should().Be(
            typeof(double?),
            "AC08: '{0}' closed over a non-nullable double selector must return System.Nullable<System.Double>; "
            + "the unconstrained TResult? annotation is plain System.Double", family);
    }

    [Theory]
    [MemberData(nameof(FamilyData))]
    public void AllEightShapes_ShouldReturnNullableDouble(string family)
    {
        var shapes = TerminalOverloads(family);

        shapes.Should().HaveCount(8, "'{0}' must declare four parameter shapes for both selector nullabilities", family);

        foreach (var shape in shapes)
        {
            var closed = shape.MakeGenericMethod(typeof(T292ApiEntity), typeof(double));

            closed.ReturnType.Should().Be(
                IsAsync(shape) ? typeof(Task<double?>) : typeof(double?),
                "AC08: every '{0}' shape closed over double must carry a structurally nullable double result", family);
        }

        shapes.Count(method => IsNonNullableSelector(method.GetParameters()[1].ParameterType))
            .Should().Be(4, "'{0}' must keep the four non-nullable-selector shapes", family);
        shapes.Count(method => !IsNonNullableSelector(method.GetParameters()[1].ParameterType))
            .Should().Be(4, "'{0}' must add the four nullable-selector shapes (AC06)", family);

        shapes.Should().Contain(method => IsAsync(method) && HasCancellationToken(method), "'{0}Async' with a token", family);
        shapes.Should().Contain(method => IsAsync(method) && !HasCancellationToken(method), "'{0}Async' that defaults the token", family);
        shapes.Should().Contain(method => !IsAsync(method) && HasParamsSpan(method), "'{0}' with span parameters", family);
    }

    [Theory]
    [MemberData(nameof(FacadeData))]
    public void FacadeForms_ShouldReturnNullableDouble(string facade)
    {
        var declarations = FacadeOverloads(facade);

        declarations.Should().HaveCount(2, "'{0}' must declare the value-type and the Nullable<T> input forms", facade);

        foreach (var declaration in declarations)
        {
            var closed = declaration.MakeGenericMethod(typeof(double));

            closed.ReturnType.Should().Be(
                typeof(double?),
                "AC08: facade '{0}' closed over double must return System.Nullable<System.Double>", facade);
        }

        declarations.Should().Contain(
            declaration => declaration.GetParameters()[0].ParameterType.IsGenericParameter,
            "'{0}' must keep a non-nullable value-type input form", facade);
        declarations.Should().Contain(
            declaration => Nullable.GetUnderlyingType(declaration.GetParameters()[0].ParameterType) != null,
            "'{0}' must keep a Nullable<T> input form", facade);
    }

    /// <summary>Public static generic definitions of <paramref name="family"/> taking (builder, selector).</summary>
    private static IEnumerable<MethodInfo> SelectorOnlyOverloads(string family)
        => typeof(EntityBuilderExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == family)
            .Where(method => method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 2)
            .Where(method => method.GetParameters().Length == 2);

    /// <summary>Every public static generic definition of <paramref name="family"/> the terminal declares.</summary>
    private static List<MethodInfo> TerminalOverloads(string family)
        => typeof(EntityBuilderExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name is var name && (name == family || name == family + "Async"))
            .Where(method => method.IsGenericMethodDefinition && method.GetGenericArguments().Length == 2)
            .ToList();

    /// <summary>Single-value declarations of a SQL facade.</summary>
    private static List<MethodInfo> FacadeOverloads(string facade)
        => typeof(CommonFunctions)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == facade && method.GetParameters().Length == 1)
            .ToList();

    private static bool IsAsync(MethodInfo method) => method.Name.EndsWith("Async", StringComparison.Ordinal);

    private static bool HasCancellationToken(MethodInfo method)
        => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(CancellationToken));

    private static bool HasParamsSpan(MethodInfo method)
        => method.GetParameters().Any(parameter => parameter.ParameterType.IsGenericType
            && parameter.ParameterType.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>));

    /// <summary>
    /// True when the selector parameter is <c>Expression&lt;Func&lt;TEntity, TResult&gt;&gt;</c> — the
    /// non-nullable value-type form — as opposed to <c>Expression&lt;Func&lt;TEntity, TResult?&gt;&gt;</c>,
    /// whose inner delegate returns <c>Nullable&lt;TResult&gt;</c>.
    /// </summary>
    private static bool IsNonNullableSelector(Type parameterType)
    {
        if (!parameterType.IsGenericType || parameterType.GetGenericTypeDefinition() != typeof(Expression<>))
            return false;

        var delegateType = parameterType.GetGenericArguments()[0];
        if (!delegateType.IsGenericType || delegateType.GetGenericTypeDefinition() != typeof(Func<,>))
            return false;

        return !delegateType.GetGenericArguments()[1].IsGenericType;
    }

    /// <summary>Entity type used only to close the generic declarations.</summary>
    public sealed class T292ApiEntity
    {
        public int Id { get; set; }
    }
}
