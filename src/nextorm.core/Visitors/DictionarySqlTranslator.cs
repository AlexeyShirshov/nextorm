using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the dictionary surface of <see cref="ClickHouseFunctions"/> (<c>dict_get</c>,
/// <c>dict_get_or_default</c>, <c>dict_has</c>). Only a dialect that opts in with
/// <see cref="ISqlDialect.SupportsDictionaries"/> (ClickHouse) may use these constructs; every other
/// provider rejects them with a clear message.
/// </summary>
internal static class DictionarySqlTranslator
{
    /// <summary>
    /// Translates a dictionary function call. Returns <c>false</c> when the call is not part of this
    /// surface.
    /// </summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        switch (node.Method.Name)
        {
            case nameof(ClickHouseFunctions.dict_get) when node.Arguments.Count == 3:
                EmitFunction(visitor, node, "dict_get");
                return true;
            case nameof(ClickHouseFunctions.dict_get_or_default) when node.Arguments.Count == 4:
                EmitFunction(visitor, node, "dict_get_or_default");
                return true;
            case nameof(ClickHouseFunctions.dict_has) when node.Arguments.Count == 2:
                EmitFunction(visitor, node, "dict_has");
                return true;
            default:
                return false;
        }
    }

    private static void EmitFunction(BaseExpressionVisitor visitor, MethodCallExpression node, string name)
    {
        if (!visitor.Dialect.SupportsDictionaries)
            throw new NotSupportedException("The dictionary functions (dictGet/dictGetOrDefault/dictHas) are not supported by this provider.");

        var args = node.Arguments;

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

        visitor.Builder!.Append(visitor.Dialect.MakeDictionaryFunction(name, rendered));
    }
}
