using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the cross-provider scalar functions of <see cref="CommonFunctions"/> (the members that
/// render natively on several providers) through the dialect's <see cref="ISqlDialect.ScalarFunctions"/>.
/// Split out of <see cref="BuiltinFunctionTranslator"/> for cohesion; the supported set and emitted SQL
/// are unchanged. Named on <see cref="CommonFunctions"/> only, so a same-named provider-only member
/// still falls through to <see cref="ExtendedScalarFunctionTranslator"/>.
/// </summary>
internal static class CrossProviderScalarTranslator
{
    private static readonly HashSet<string> ScalarFunctionNames = new(StringComparer.Ordinal)
    {
        nameof(CommonFunctions.left), nameof(CommonFunctions.right), nameof(CommonFunctions.lpad),
        nameof(CommonFunctions.rpad), nameof(CommonFunctions.repeat), nameof(CommonFunctions.reverse),
        nameof(CommonFunctions.space), nameof(CommonFunctions.concat_ws), nameof(CommonFunctions.translate),
        nameof(CommonFunctions.ascii), nameof(CommonFunctions.@char),
        nameof(CommonFunctions.bit_length), nameof(CommonFunctions.octet_length),
        nameof(CommonFunctions.cot), nameof(CommonFunctions.degrees), nameof(CommonFunctions.radians),
        nameof(CommonFunctions.pi)
    };

    /// <summary>Translates a cross-provider scalar call; returns <c>false</c> when it is not one.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(CommonFunctions) || !ScalarFunctionNames.Contains(node.Method.Name))
            return false;

        EmitScalarFunction(visitor, node);
        return true;
    }

    /// <summary>
    /// Renders a cross-provider scalar function through the dialect's <see cref="ISqlDialect.ScalarFunctions"/>
    /// renderer, failing with a clear message on a provider that does not support the name.
    /// </summary>
    private static void EmitScalarFunction(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var name = node.Method.Name;

        if (visitor.Dialect.ScalarFunctions is not { } scalars || !scalars.Supports(name))
            throw new NotSupportedException($"The {name} function is not supported by this provider.");

        var args = name == nameof(CommonFunctions.concat_ws)
            ? ArgumentFlattener.Flatten(node.Arguments, 1)
            : node.Arguments;

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
            rendered[i] = visitor.VisitToString(args[i]);

        visitor.Builder!.Append(scalars.Render(name, rendered));
    }
}
