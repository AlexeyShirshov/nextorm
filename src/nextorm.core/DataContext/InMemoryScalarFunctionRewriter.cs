using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<MethodInfo, ParameterInfo[]> ParameterCache = new();

    private static ParameterInfo[] Parameters(MethodInfo method) =>
        ParameterCache.GetOrAdd(method, static m => m.GetParameters());

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
        [nameof(CommonFunctions.@char)] = Helper(nameof(InMemoryScalarFunctions.Char)),
        [nameof(CommonFunctions.bit_length)] = Helper(nameof(InMemoryScalarFunctions.BitLength)),
        [nameof(CommonFunctions.octet_length)] = Helper(nameof(InMemoryScalarFunctions.OctetLength)),
        [nameof(CommonFunctions.cot)] = Helper(nameof(InMemoryScalarFunctions.Cot)),
        [nameof(CommonFunctions.degrees)] = Helper(nameof(InMemoryScalarFunctions.Degrees)),
        [nameof(CommonFunctions.radians)] = Helper(nameof(InMemoryScalarFunctions.Radians)),
        [nameof(CommonFunctions.pi)] = Helper(nameof(InMemoryScalarFunctions.Pi))
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
        ["log10"] = Helper(nameof(InMemoryScalarFunctions.Log10)),
        ["log2"] = Helper(nameof(InMemoryScalarFunctions.Log2)),
        ["mod"] = Helper(nameof(InMemoryScalarFunctions.Mod)),
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
        [nameof(PostgresFunctions.upper_inf)] = Helper(nameof(InMemoryScalarFunctions.RangeUpperInf)),
        [nameof(PostgresFunctions.range_union)] = Helper(nameof(InMemoryScalarFunctions.RangeUnion)),
        [nameof(PostgresFunctions.range_intersection)] = Helper(nameof(InMemoryScalarFunctions.RangeIntersection)),
        [nameof(PostgresFunctions.range_difference)] = Helper(nameof(InMemoryScalarFunctions.RangeDifference)),
        [nameof(PostgresFunctions.range_adjacent)] = Helper(nameof(InMemoryScalarFunctions.RangeAdjacent)),
        [nameof(PostgresFunctions.range_strictly_left_of)] = Helper(nameof(InMemoryScalarFunctions.RangeStrictlyLeftOf)),
        [nameof(PostgresFunctions.range_strictly_right_of)] = Helper(nameof(InMemoryScalarFunctions.RangeStrictlyRightOf)),
        [nameof(PostgresFunctions.range_not_extend_right_of)] = Helper(nameof(InMemoryScalarFunctions.RangeNotExtendRightOf)),
        [nameof(PostgresFunctions.range_not_extend_left_of)] = Helper(nameof(InMemoryScalarFunctions.RangeNotExtendLeftOf)),
        [nameof(PostgresFunctions.empty_range)] = Helper(nameof(InMemoryScalarFunctions.RangeEmpty)),
        [nameof(PostgresFunctions.range_merge)] = Helper(nameof(InMemoryScalarFunctions.RangeMerge)),
        [nameof(PostgresFunctions.multirange)] = Helper(nameof(InMemoryScalarFunctions.RangeMultiFromRange))
    };

    private static readonly Dictionary<string, MethodInfo> MultiRangeHelpers = new(StringComparer.Ordinal)
    {
        ["overlaps:mm"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiOverlaps)),
        ["overlaps:mr"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiOverlapsRange)),
        ["overlaps:rm"] = Helper(nameof(InMemoryScalarFunctions.RangeRangeOverlapsMulti)),
        ["range_contains:mm"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiContainsMulti)),
        ["range_contains:mr"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiContainsRange)),
        ["range_contains:rm"] = Helper(nameof(InMemoryScalarFunctions.RangeRangeContainsMulti)),
        ["range_contains:mv"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiContainsValue)),
        ["range_contained_by:mm"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiContainedByMulti)),
        ["range_contained_by:mr"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiContainedByRange)),
        ["range_contained_by:rm"] = Helper(nameof(InMemoryScalarFunctions.RangeContainedByMulti)),
        ["range_union:mm"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiUnion)),
        ["range_intersection:mm"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiIntersection)),
        ["range_difference:mm"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiDifference)),
        ["range_strictly_left_of:mm"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiStrictlyLeftOf)),
        ["range_strictly_right_of:mm"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiStrictlyRightOf)),
        ["range_not_extend_right_of:mm"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiNotExtendRightOf)),
        ["range_not_extend_left_of:mm"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiNotExtendLeftOf)),
        ["range_adjacent:mm"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiAdjacent)),
        ["range_merge:m"] = Helper(nameof(InMemoryScalarFunctions.RangeMergeMulti)),
        ["isempty:m"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiIsEmpty)),
        ["lower:m"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiLower)),
        ["upper:m"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiUpper)),
        ["lower_inc:m"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiLowerInc)),
        ["upper_inc:m"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiUpperInc)),
        ["lower_inf:m"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiLowerInf)),
        ["upper_inf:m"] = Helper(nameof(InMemoryScalarFunctions.RangeMultiUpperInf))
    };

    private static readonly HashSet<string> RangeConstructors = new(StringComparer.Ordinal)
    {
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
            if (HasRangeArrayParameter(node))
                return CallMultiRange(node);

            if (node.Method.Name == nameof(PostgresFunctions.range_contains))
                return CallRangeContains(node);

            if (RangeConstructors.Contains(node.Method.Name))
                return CallRangeConstructor(node);

            if (PostgresRangeHelpers.TryGetValue(node.Method.Name, out var rangeHelper))
                return Call(rangeHelper, node);
        }

        return base.VisitMethodCall(node);
    }

    private Expression CallRangeContains(MethodCallExpression node)
    {
        var secondParameter = Parameters(node.Method)[1].ParameterType;
        var isRange = RangeTypeFacts.IsRange(secondParameter);
        var helper = Helper(isRange ? nameof(InMemoryScalarFunctions.RangeContainsRange) : nameof(InMemoryScalarFunctions.RangeContainsValue));
        return Call(helper, node);
    }

    private static bool HasRangeArrayParameter(MethodCallExpression node)
    {
        foreach (var parameter in Parameters(node.Method))
            if (RangeTypeFacts.IsRangeArray(parameter.ParameterType))
                return true;

        return false;
    }

    private static string MultiRangeShape(MethodCallExpression node)
    {
        var parameters = Parameters(node.Method);
        if (parameters.Length == 1)
            return RangeTypeFacts.IsRangeArray(parameters[0].ParameterType) ? "m" : "r";

        if (parameters.Length != 2)
            return string.Empty;

        if (RangeTypeFacts.IsRangeArray(parameters[0].ParameterType))
        {
            if (RangeTypeFacts.IsRangeArray(parameters[1].ParameterType))
                return "mm";

            return RangeTypeFacts.IsRange(parameters[1].ParameterType) ? "mr" : "mv";
        }

        return RangeTypeFacts.IsRangeArray(parameters[1].ParameterType) ? "rm" : string.Empty;
    }

    private Expression CallMultiRange(MethodCallExpression node)
    {
        var key = node.Method.Name + ":" + MultiRangeShape(node);
        if (!MultiRangeHelpers.TryGetValue(key, out var helper))
            throw new NotSupportedException(
                $"The {node.Method.Name} multirange function is not supported by the in-memory provider.");

        return Call(helper, node);
    }

    private Expression CallRangeConstructor(MethodCallExpression node)
    {
        RangeTypeFacts.TryGetRangeBoundType(node.Method.ReturnType, out var boundType);
        var helper = Helper(nameof(InMemoryScalarFunctions.RangeCtor)).MakeGenericMethod(boundType);
        var parameters = Parameters(helper);

        var lower = ConvertArgument(node.Arguments[0], parameters[0].ParameterType);
        var upper = ConvertArgument(node.Arguments[1], parameters[1].ParameterType);
        var bounds = node.Arguments.Count > 2
            ? ConvertArgument(node.Arguments[2], parameters[2].ParameterType)
            : Expression.Constant(null, parameters[2].ParameterType);

        return Expression.Call(helper, lower, upper, bounds);
    }

    private Expression ConvertArgument(Expression argument, Type target)
    {
        var visited = Visit(argument)!;
        return target.IsAssignableFrom(visited.Type) ? visited : Expression.Convert(visited, target);
    }

    private Expression Call(MethodInfo helper, MethodCallExpression node)
    {
        if (helper.IsGenericMethodDefinition)
            helper = helper.MakeGenericMethod(node.Method.GetGenericArguments());

        var parameters = Parameters(helper);
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
