using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Provides helpers for classifying expression-tree calls as aggregate or window functions, and for
/// resolving related function metadata and projection shapes.
/// <para>
/// Aggregate classification is shared by the query engine and external operators. It applies only to
/// the supplied expression; it does not inspect expressions nested in aggregate <c>FILTER</c> or
/// <c>DISTINCT</c> arguments.
/// </para>
/// </summary>
public static class AggregateClassification
{
    // The built-in aggregate surface of CommonFunctions (and its provider-derived surfaces). The
    // *_distinct spellings are added below; names that WindowSql maps to a window function are never
    // treated as aggregates, because the same CLR name is also a window member (sum_over, ...).
    private static readonly string[] BuiltinAggregateNames =
    [
        "count",
        "count_big",
        "min",
        "max",
        "avg",
        "sum",
        "stdev",
        "stdevp",
        "var",
        "varp",
        "any_agg",
        "string_agg",
        "corr",
        "covar_pop",
        "covar_samp",
    ];

    private static readonly HashSet<string> AggregateNameSet = BuildAggregateNameSet();

    private static HashSet<string> BuildAggregateNameSet()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in BuiltinAggregateNames)
        {
            names.Add(name);
            names.Add(name + "_distinct");
        }
        return names;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="expression"/> is a method call recognized as an aggregate.
    /// </summary>
    public static bool IsAggregate(Expression? expression)
    {
        if (expression is not MethodCallExpression call)
            return false;

        // (1) An explicit annotation wins over every name-based rule.
        if (ScalarFunctionTranslator.ResolveSqlFunctionAttribute(call.Method)?.IsAggregate == true)
            return true;

        var declaringType = call.Method.DeclaringType;
        if (declaringType is null || !declaringType.IsAssignableTo(typeof(CommonFunctions)))
            return false;

        // (2) Provider-advanced families.
        if (AdvancedAggregateTranslator.IsAdvancedAggregateMethod(call))
            return true;

        // (3) Window name exclusion.
        var name = call.Method.Name;
        if (WindowSql.MapWindowFunctionName(name) is not null)
            return false;

        // (4) Built-in aggregate surface, including *_distinct.
        return AggregateNameSet.Contains(name);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="expression"/> is a method call whose name maps to a
    /// window function.
    /// </summary>
    public static bool IsWindow(Expression? expression)
    {
        if (expression is not MethodCallExpression call)
            return false;
        return WindowSql.MapWindowFunctionName(call.Method.Name) is not null;
    }

    /// <summary>
    /// Returns the <see cref="SqlFunctionAttribute"/> applied to <paramref name="method"/>, or the attribute
    /// applied to its declaring type when the method has none. Returns <see langword="null"/> when neither
    /// has an attribute.
    /// </summary>
    public static SqlFunctionAttribute? ResolveSqlFunctionAttribute(MethodInfo method)
        => ScalarFunctionTranslator.ResolveSqlFunctionAttribute(method);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="node"/> calls an aggregate in an advanced family
    /// recognized by the query engine.
    /// </summary>
    public static bool IsAdvancedAggregateMethod(MethodCallExpression node)
        => AdvancedAggregateTranslator.IsAdvancedAggregateMethod(node);

    /// <summary>
    /// Maps a method name to its window-function name, or returns <see langword="null"/> when the name is not
    /// recognized.
    /// </summary>
    public static string? MapWindowFunctionName(string methodName)
        => WindowSql.MapWindowFunctionName(methodName);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="type"/> is treated as a one-column projection.
    /// </summary>
    public static bool IsSingleColumnProjection(Type type)
        => TypeFacts.IsSingleColumnProjection(type);

    /// <summary>
    /// Removes any <see cref="ExpressionType.Convert"/> or <see cref="ExpressionType.ConvertChecked"/> wrappers
    /// from <paramref name="expression"/>.
    /// </summary>
    public static Expression UnwrapConvert(Expression expression)
        => TypeFacts.UnwrapConvert(expression);
}
