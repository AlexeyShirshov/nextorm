using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Dispatches scalar function translation into SQL: the built-in <c>string</c>, <see cref="Math"/>
/// and <c>DateTime</c> methods (delegated to <see cref="StringFunctionTranslator"/>,
/// <see cref="MathFunctionTranslator"/> and <see cref="DateTimeFunctionTranslator"/>), the
/// <c>SqlFunctions.Sql.like</c> function, methods annotated with <see cref="SqlFunctionAttribute"/>
/// and <c>string.Concat</c>. Extracted from <see cref="BaseExpressionVisitor"/>; the dispatch order
/// and the emitted SQL are unchanged.
/// </summary>
internal static class ScalarFunctionTranslator
{
    /// <summary>
    /// Translates the supported scalar functions (string, <see cref="Math"/> and <c>SqlFunctions.Sql.like</c>).
    /// Everything provider-specific is delegated to <see cref="ISqlDialect"/>; this dispatch only maps
    /// the CLR method family to its translator.
    /// </summary>
    internal static bool TryTranslateBuiltIn(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var declaringType = node.Method.DeclaringType;

        if (declaringType == typeof(string))
            return StringFunctionTranslator.TryTranslate(visitor, node);

        if (declaringType == typeof(Math))
            return MathFunctionTranslator.TryTranslate(visitor, node);

        if (declaringType == typeof(DateTime))
            return DateTimeFunctionTranslator.TryTranslate(visitor, node);

        if (declaringType == typeof(CommonFunctions) && node.Method.Name == nameof(CommonFunctions.like))
            return TryTranslateLikeFunction(visitor, node);

        return false;
    }

    /// <summary>
    /// Translates a call to a method annotated with <see cref="SqlFunctionAttribute"/> into
    /// <c>[schema.]name(arg1, arg2, ...)</c>. An instance method's target is emitted as the first
    /// argument. This is attempted only after the built-in <c>string</c>/<see cref="Math"/>/<c>SqlFunctions.Sql</c>
    /// translations, so an attribute cannot change their behaviour.
    /// </summary>
    internal static bool TryTranslateSqlFunction(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var attribute = node.Method.GetCustomAttribute<SqlFunctionAttribute>(inherit: false)
            ?? node.Method.DeclaringType?.GetCustomAttribute<SqlFunctionAttribute>(inherit: false);

        if (attribute is null)
            return false;

        if (visitor.IsParamMode)
        {
            // Nothing is emitted, but every embedded expression still has to be walked so captured
            // constants become parameters in the same order as the SQL pass.
            if (node.Object is not null)
                visitor.Visit(node.Object);

            for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
                visitor.Visit(node.Arguments[i]);

            return true;
        }

        var name = string.IsNullOrEmpty(attribute.Name) ? node.Method.Name : attribute.Name;

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeFunction(name, attribute.Schema)).Append('(');

        var first = true;

        if (node.Object is not null)
        {
            visitor.Builder!.Append(visitor.VisitToString(node.Object));
            first = false;
        }

        for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
        {
            if (!first) visitor.Builder!.Append(", ");
            visitor.Builder!.Append(visitor.VisitToString(node.Arguments[i]));
            first = false;
        }

        visitor.Builder!.Append(')');
        return true;
    }

    private static bool TryTranslateLikeFunction(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;
        if (args.Count is < 2 or > 3)
            return false;

        if (visitor.IsParamMode)
        {
            for (var i = 0; i < args.Count; i++) visitor.Visit(args[i]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var value = visitor.VisitToString(args[0]);
        var pattern = visitor.VisitToString(args[1]);

        var escapeClause = string.Empty;
        if (args.Count == 3)
        {
            if (!SqlLiteral.TryGetConstantString(args[2], out var escapeChar))
                throw new NotSupportedException("The SqlFunctions.Sql.like escape character must be a constant string or char.");

            escapeClause = visitor.Dialect.MakeLikeEscape(escapeChar);
        }

        visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate($"{value} like {pattern}{escapeClause}", visitor.IsPredicateContext));
        return true;
    }

    /// <summary>
    /// Emits <c>string.Concat</c> through the dialect concatenation hook. A concatenation is a
    /// computed column, so when it is selected it must be aliased, allowing an outer query to
    /// reference it instead of re-expanding (and losing) the inputs.
    /// </summary>
    internal static void EmitStringConcat(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        visitor.NeedAliasForColumn = true;

        var args = node.Arguments;

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        var parts = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
            parts[i] = visitor.VisitToString(args[i]);

        visitor.Builder!.Append('(').Append(visitor.Dialect.MakeConcat(parts)).Append(')');
    }
}
