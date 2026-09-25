using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Rewrites the <see cref="CommonFunctions"/> scalar functions that have a native in-memory equivalent
/// before the in-memory provider compiles an expression. It replaces each call with the matching
/// <see cref="InMemoryScalarFunctions"/> helper (and <c>collate</c> with its argument, since the
/// in-memory provider's comparisons are already ordinal). Without this the compiled expression would
/// evaluate the <c>SqlFunctions.Sql</c> marker (which is <c>null</c>) and fail with a
/// <see cref="NullReferenceException"/>.
/// </summary>
internal sealed class InMemoryScalarFunctionRewriter : ExpressionVisitor
{
    private static readonly Dictionary<string, MethodInfo> Helpers = new(StringComparer.Ordinal)
    {
        [nameof(CommonFunctions.left)] = Helper(nameof(InMemoryScalarFunctions.Left)),
        [nameof(CommonFunctions.right)] = Helper(nameof(InMemoryScalarFunctions.Right)),
        [nameof(CommonFunctions.lpad)] = Helper(nameof(InMemoryScalarFunctions.Lpad)),
        [nameof(CommonFunctions.rpad)] = Helper(nameof(InMemoryScalarFunctions.Rpad)),
        [nameof(CommonFunctions.repeat)] = Helper(nameof(InMemoryScalarFunctions.Repeat)),
        [nameof(CommonFunctions.reverse)] = Helper(nameof(InMemoryScalarFunctions.Reverse)),
        [nameof(CommonFunctions.space)] = Helper(nameof(InMemoryScalarFunctions.Space)),
        [nameof(CommonFunctions.concat_ws)] = Helper(nameof(InMemoryScalarFunctions.ConcatWs)),
        [nameof(CommonFunctions.translate)] = Helper(nameof(InMemoryScalarFunctions.Translate)),
        [nameof(CommonFunctions.ascii)] = Helper(nameof(InMemoryScalarFunctions.Ascii)),
        [nameof(CommonFunctions.@char)] = Helper(nameof(InMemoryScalarFunctions.Char))
    };

    private static readonly Dictionary<string, MethodInfo> SqliteHelpers = new(StringComparer.Ordinal)
    {
        ["hex"] = Helper(nameof(InMemoryScalarFunctions.Hex)),
        ["unhex"] = Helper(nameof(InMemoryScalarFunctions.Unhex)),
        ["octet_length"] = Helper(nameof(InMemoryScalarFunctions.OctetLength)),
        ["unicode"] = Helper(nameof(InMemoryScalarFunctions.Unicode)),
        ["char"] = Helper(nameof(InMemoryScalarFunctions.CharFromCodes)),
        ["typeof"] = Helper(nameof(InMemoryScalarFunctions.TypeOf)),
        ["acos"] = Helper(nameof(InMemoryScalarFunctions.Acos)),
        ["acosh"] = Helper(nameof(InMemoryScalarFunctions.Acosh)),
        ["asin"] = Helper(nameof(InMemoryScalarFunctions.Asin)),
        ["asinh"] = Helper(nameof(InMemoryScalarFunctions.Asinh)),
        ["atan"] = Helper(nameof(InMemoryScalarFunctions.Atan)),
        ["atan2"] = Helper(nameof(InMemoryScalarFunctions.Atan2)),
        ["atanh"] = Helper(nameof(InMemoryScalarFunctions.Atanh)),
        ["cosh"] = Helper(nameof(InMemoryScalarFunctions.Cosh)),
        ["degrees"] = Helper(nameof(InMemoryScalarFunctions.Degrees)),
        ["log10"] = Helper(nameof(InMemoryScalarFunctions.Log10)),
        ["log2"] = Helper(nameof(InMemoryScalarFunctions.Log2)),
        ["mod"] = Helper(nameof(InMemoryScalarFunctions.Mod)),
        ["pi"] = Helper(nameof(InMemoryScalarFunctions.Pi)),
        ["radians"] = Helper(nameof(InMemoryScalarFunctions.Radians)),
        ["sinh"] = Helper(nameof(InMemoryScalarFunctions.Sinh)),
        ["tanh"] = Helper(nameof(InMemoryScalarFunctions.Tanh))
    };

    private static readonly Dictionary<string, MethodInfo> PostgresRangeHelpers = new(StringComparer.Ordinal)
    {
        [nameof(PostgresFunctions.overlaps)] = Helper(nameof(InMemoryScalarFunctions.RangeOverlaps)),
        [nameof(PostgresFunctions.range_contained_by)] = Helper(nameof(InMemoryScalarFunctions.RangeContainedBy)),
        [nameof(PostgresFunctions.isempty)] = Helper(nameof(InMemoryScalarFunctions.RangeIsEmpty)),
        [nameof(PostgresFunctions.lower)] = Helper(nameof(InMemoryScalarFunctions.RangeLower)),
        [nameof(PostgresFunctions.upper)] = Helper(nameof(InMemoryScalarFunctions.RangeUpper)),
        [nameof(PostgresFunctions.lower_inc)] = Helper(nameof(InMemoryScalarFunctions.RangeLowerInc)),
        [nameof(PostgresFunctions.upper_inc)] = Helper(nameof(InMemoryScalarFunctions.RangeUpperInc)),
        [nameof(PostgresFunctions.lower_inf)] = Helper(nameof(InMemoryScalarFunctions.RangeLowerInf)),
        [nameof(PostgresFunctions.upper_inf)] = Helper(nameof(InMemoryScalarFunctions.RangeUpperInf))
    };

    private static readonly HashSet<string> UnsupportedPostgresRangeFunctions = new(StringComparer.Ordinal)
    {
        nameof(PostgresFunctions.range_union), nameof(PostgresFunctions.range_intersection),
        nameof(PostgresFunctions.range_difference), nameof(PostgresFunctions.range_adjacent),
        nameof(PostgresFunctions.range_strictly_left_of), nameof(PostgresFunctions.range_strictly_right_of),
        nameof(PostgresFunctions.range_not_extend_right_of), nameof(PostgresFunctions.range_not_extend_left_of),
        nameof(PostgresFunctions.empty_range),
        nameof(PostgresFunctions.int4range), nameof(PostgresFunctions.int8range),
        nameof(PostgresFunctions.numrange), nameof(PostgresFunctions.tsrange),
        nameof(PostgresFunctions.tstzrange), nameof(PostgresFunctions.daterange)
    };

    internal static Expression Rewrite(Expression expression) => new InMemoryScalarFunctionRewriter().Visit(expression)!;

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType is { } declaring && typeof(MySqlFunctions).IsAssignableFrom(declaring))
            throw new NotSupportedException(
                $"The {node.Method.Name} function requires a MySQL/MariaDB provider and cannot be evaluated by the in-memory provider.");

        // The SQL Server-only T-SQL functions have no in-memory equivalent; fail with a clear message
        // instead of compiling the marker call (which would invoke a method on the null SqlServer
        // surface).
        if (node.Method.DeclaringType == typeof(SqlServerFunctions))
            throw new NotSupportedException($"The {node.Method.Name} function is not supported by the in-memory provider.");

        if (node.Method.DeclaringType == typeof(CommonFunctions))
        {
            if (node.Method.Name == nameof(CommonFunctions.collate) && node.Arguments.Count == 2)
                return Visit(node.Arguments[0])!;

            if (Helpers.TryGetValue(node.Method.Name, out var helper))
                return Call(helper, node);
        }
        else if (node.Method.DeclaringType == typeof(SqliteFunctions))
        {
            if (node.Method.Name == "ifnull" && node.Arguments.Count == 2)
                return Expression.Coalesce(Visit(node.Arguments[0])!, Visit(node.Arguments[1])!);

            if (node.Method.Name == "if" && node.Arguments.Count == 3)
                return Expression.Condition(Visit(node.Arguments[0])!, Visit(node.Arguments[1])!, Visit(node.Arguments[2])!);

            if (SqliteHelpers.TryGetValue(node.Method.Name, out var sqliteHelper))
                return Call(sqliteHelper, node);
        }
        else if (node.Method.DeclaringType == typeof(PostgresFunctions))
        {
            if (node.Method.Name == nameof(PostgresFunctions.range_contains))
                return CallRangeContains(node);

            if (PostgresRangeHelpers.TryGetValue(node.Method.Name, out var rangeHelper))
                return Call(rangeHelper, node);

            if (UnsupportedPostgresRangeFunctions.Contains(node.Method.Name))
                throw new NotSupportedException(
                    $"The {node.Method.Name} range function is not supported by the in-memory provider; only overlaps, range_contains/range_contained_by and the range inspection functions are.");
        }

        return base.VisitMethodCall(node);
    }

    private Expression CallRangeContains(MethodCallExpression node)
    {
        var secondParameter = node.Method.GetParameters()[1].ParameterType;
        var isRange = secondParameter.IsGenericType && secondParameter.GetGenericTypeDefinition() == typeof(Range<>);
        var helper = Helper(isRange ? nameof(InMemoryScalarFunctions.RangeContainsRange) : nameof(InMemoryScalarFunctions.RangeContainsValue));
        return Call(helper, node);
    }

    private Expression Call(MethodInfo helper, MethodCallExpression node)
    {
        if (helper.IsGenericMethodDefinition)
            helper = helper.MakeGenericMethod(node.Method.GetGenericArguments());

        var parameters = helper.GetParameters();
        var arguments = new Expression[node.Arguments.Count];
        for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
        {
            var argument = Visit(node.Arguments[i])!;
            var target = parameters[i].ParameterType;
            if (!target.IsAssignableFrom(argument.Type))
                argument = Expression.Convert(argument, target);

            arguments[i] = argument;
        }

        return Expression.Call(helper, arguments);
    }

    private static MethodInfo Helper(string name) =>
        typeof(InMemoryScalarFunctions).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) is { } method
            ? method
            : throw new MissingMethodException(typeof(InMemoryScalarFunctions).FullName, name);
}
