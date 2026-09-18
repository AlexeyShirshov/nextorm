using System.Linq.Expressions;
using System.Reflection;

namespace nextorm.core;

/// <summary>
/// Translates scalar functions into SQL: the built-in <c>string</c>, <see cref="Math"/> and
/// <c>NORM.SQL.like</c> methods, <c>string.Concat</c>, and methods annotated with
/// <see cref="SqlFunctionAttribute"/>. Extracted from <see cref="BaseExpressionVisitor"/>; the
/// dispatch order and the emitted SQL are unchanged.
/// </summary>
internal static class ScalarFunctionTranslator
{
    /// <summary>Which part of a string a LIKE-style method (<c>Contains</c>/<c>StartsWith</c>/<c>EndsWith</c>) matches.</summary>
    private enum LikePosition
    {
        Contains,
        StartsWith,
        EndsWith
    }

    /// <summary>
    /// Translates the supported scalar functions (string, <see cref="Math"/> and <c>NORM.SQL.like</c>).
    /// Everything provider-specific is delegated to <see cref="ISqlDialect"/>; this dispatch only maps
    /// the CLR method to a function name and walks the arguments.
    /// </summary>
    internal static bool TryTranslateBuiltIn(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var declaringType = node.Method.DeclaringType;

        if (declaringType == typeof(string))
            return TryTranslateStringMethod(visitor, node);

        if (declaringType == typeof(Math))
            return TryTranslateMathMethod(visitor, node);

        if (declaringType == typeof(DateTime))
            return TryTranslateDateTimeMethod(visitor, node);

        if (declaringType == typeof(NORM.NORM_SQL) && node.Method.Name == nameof(NORM.NORM_SQL.like))
            return TryTranslateLikeFunction(visitor, node);

        return false;
    }

    /// <summary>
    /// Translates the <see cref="DateTime.AddYears"/>/<see cref="DateTime.AddMonths"/>/... methods to
    /// the dialect's date-addition rendering (<c>date_add</c>). The method name selects the date part
    /// and the single argument is the amount; the target is the date/time value. Requires a provider
    /// that opts into <see cref="ISqlDialect.SupportsDateArithmetic"/>.
    /// </summary>
    private static bool TryTranslateDateTimeMethod(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Object is null || node.Arguments.Count != 1)
            return false;

        var field = node.Method.Name switch
        {
            nameof(DateTime.AddYears) => "year",
            nameof(DateTime.AddMonths) => "month",
            nameof(DateTime.AddDays) => "day",
            nameof(DateTime.AddHours) => "hour",
            nameof(DateTime.AddMinutes) => "minute",
            nameof(DateTime.AddSeconds) => "second",
            nameof(DateTime.AddMilliseconds) => "milliseconds",
            _ => null
        };

        if (field is null)
            return false;

        if (!visitor.Dialect.SupportsDateArithmetic)
            throw new NotSupportedException("Date arithmetic (DateTime.Add*) is not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            visitor.Visit(node.Arguments[0]);
            return true;
        }

        // A date arithmetic expression is a computed column and has to be aliased when selected.
        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeDateAdd(
            field,
            visitor.VisitToString(node.Arguments[0]),
            visitor.VisitToString(node.Object)));
        return true;
    }

    /// <summary>
    /// Translates a call to a method annotated with <see cref="SqlFunctionAttribute"/> into
    /// <c>[schema.]name(arg1, arg2, ...)</c>. An instance method's target is emitted as the first
    /// argument. This is attempted only after the built-in <c>string</c>/<see cref="Math"/>/<c>NORM.SQL</c>
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

    private static bool TryTranslateStringMethod(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var methodName = node.Method.Name;

        // Instance string methods.
        if (node.Object is not null && node.Object.Type == typeof(string))
        {
            switch (methodName)
            {
                // Only the culture-less overloads are portable; the CultureInfo overloads are not.
                case nameof(string.ToUpper) when node.Arguments.Count == 0:
                    return EmitStringFunction(visitor, node.Object, v => visitor.Dialect.MakeUpper(v));
                case nameof(string.ToLower) when node.Arguments.Count == 0:
                    return EmitStringFunction(visitor, node.Object, v => visitor.Dialect.MakeLower(v));
                // Trim/TrimStart/TrimEnd also have a (char)/params char[] overload that is not a
                // whitespace trim, so only the parameterless form is translated.
                case nameof(string.Trim) when node.Arguments.Count == 0:
                    return EmitStringFunction(visitor, node.Object, v => visitor.Dialect.MakeTrim(v, StringTrimKind.Both));
                case nameof(string.TrimStart) when node.Arguments.Count == 0:
                    return EmitStringFunction(visitor, node.Object, v => visitor.Dialect.MakeTrim(v, StringTrimKind.Start));
                case nameof(string.TrimEnd) when node.Arguments.Count == 0:
                    return EmitStringFunction(visitor, node.Object, v => visitor.Dialect.MakeTrim(v, StringTrimKind.End));
                case nameof(string.Substring):
                    return TryTranslateSubstring(visitor, node);
                case nameof(string.Replace):
                    return TryTranslateReplace(visitor, node);
                case nameof(string.Contains):
                    return TryTranslateStringLike(visitor, node, LikePosition.Contains);
                case nameof(string.StartsWith):
                    return TryTranslateStringLike(visitor, node, LikePosition.StartsWith);
                case nameof(string.EndsWith):
                    return TryTranslateStringLike(visitor, node, LikePosition.EndsWith);
            }

            return false;
        }

        // Static string methods.
        if (node.Object is null && methodName == nameof(string.IsNullOrEmpty))
            return TryTranslateIsNullOrEmpty(visitor, node);

        return false;
    }

    private static bool EmitStringFunction(BaseExpressionVisitor visitor, Expression operand, Func<string, string> render)
    {
        if (visitor.IsParamMode)
        {
            visitor.Visit(operand);
            return true;
        }

        // A function call is a computed column and has to be aliased when selected.
        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(render(visitor.VisitToString(operand)));
        return true;
    }

    private static bool TryTranslateSubstring(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;
        if (node.Object is null || args.Count is < 1 or > 2)
            return false;

        // Only the (int) and (int, int) overloads have a SQL equivalent; Substring(Range) does not.
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].Type != typeof(int))
                return false;
        }

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            for (var i = 0; i < args.Count; i++) visitor.Visit(args[i]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var value = visitor.VisitToString(node.Object);
        var start = visitor.VisitToString(args[0]);
        var length = args.Count == 2 ? visitor.VisitToString(args[1]) : null;
        visitor.Builder!.Append(visitor.Dialect.MakeSubstring(value, start, length));
        return true;
    }

    private static bool TryTranslateReplace(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;
        if (node.Object is null || args.Count != 2)
            return false;

        // Only the (string, string) overload maps to SQL replace; (char, char) has no char-typed form.
        if (args[0].Type != typeof(string) || args[1].Type != typeof(string))
            return false;

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            visitor.Visit(args[0]);
            visitor.Visit(args[1]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeReplace(visitor.VisitToString(node.Object), visitor.VisitToString(args[0]), visitor.VisitToString(args[1])));
        return true;
    }

    private static bool TryTranslateStringLike(BaseExpressionVisitor visitor, MethodCallExpression node, LikePosition position)
    {
        var args = node.Arguments;
        if (node.Object is null || args.Count != 1 || args[0].Type != typeof(string))
            return false;

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            visitor.Visit(args[0]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var value = visitor.VisitToString(node.Object);
        var pattern = BuildLikePattern(visitor, args[0], position, out var escaped);
        var predicate = escaped
            ? $"{value} like {pattern} escape '\\'"
            : $"{value} like {pattern}";
        visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate(predicate, visitor.IsPredicateContext));
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
                throw new NotSupportedException("The NORM.SQL.like escape character must be a constant string or char.");

            escapeClause = $" escape {SqlLiteral.ToSqlStringLiteral(escapeChar)}";
        }

        visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate($"{value} like {pattern}{escapeClause}", visitor.IsPredicateContext));
        return true;
    }

    private static bool TryTranslateIsNullOrEmpty(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;
        if (args.Count != 1)
            return false;

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[0]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var value = visitor.VisitToString(args[0]);
        visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate($"({value} is null or {value} = {visitor.Dialect.EmptyString})", visitor.IsPredicateContext));
        return true;
    }

    private static bool TryTranslateMathMethod(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var name = node.Method.Name switch
        {
            nameof(Math.Abs) => "abs",
            nameof(Math.Ceiling) => "ceiling",
            nameof(Math.Floor) => "floor",
            nameof(Math.Round) => "round",
            nameof(Math.Sqrt) => "sqrt",
            nameof(Math.Pow) => "pow",
            nameof(Math.Exp) => "exp",
            nameof(Math.Log) => "log",
            nameof(Math.Sin) => "sin",
            nameof(Math.Cos) => "cos",
            nameof(Math.Tan) => "tan",
            nameof(Math.Sign) => "sign",
            nameof(Math.Truncate) => "trunc",
            _ => null
        };

        if (name is null)
            return false;

        var args = node.Arguments;

        // Math.Round's MidpointRounding overloads and the two-argument Math.Log are not portable and
        // are deliberately left unsupported rather than emitting SQL with different semantics.
        if (node.Method.Name == nameof(Math.Round) && args.Count > 2)
            return false;
        if (node.Method.Name == nameof(Math.Log) && args.Count != 1)
            return false;

        if (visitor.IsParamMode)
        {
            for (var i = 0; i < args.Count; i++) visitor.Visit(args[i]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var sqlArgs = new string[args.Count];
        for (var i = 0; i < args.Count; i++)
            sqlArgs[i] = visitor.VisitToString(args[i]);

        visitor.Builder!.Append(visitor.Dialect.MakeMathFunction(name, sqlArgs));
        return true;
    }

    /// <summary>
    /// Builds the right-hand side of a LIKE predicate for a <c>Contains</c>/<c>StartsWith</c>/<c>EndsWith</c>
    /// call. A constant is escaped and inlined; a runtime value is wrapped in <c>%</c> using the dialect
    /// concatenation operator and left unescaped (the wildcards cannot be escaped at translation time).
    /// </summary>
    private static string BuildLikePattern(BaseExpressionVisitor visitor, Expression argument, LikePosition position, out bool escaped)
    {
        var prefix = position is LikePosition.Contains or LikePosition.EndsWith ? "%" : string.Empty;
        var suffix = position is LikePosition.Contains or LikePosition.StartsWith ? "%" : string.Empty;

        if (argument is ConstantExpression { Value: string constant })
        {
            var escapedValue = SqlLiteral.EscapeLikeWildcards(constant);
            escaped = escapedValue != constant;
            return SqlLiteral.ToSqlStringLiteral(prefix + escapedValue + suffix);
        }

        escaped = false;

        var parts = new List<string>(3);

        if (prefix.Length > 0)
            parts.Add(SqlLiteral.ToSqlStringLiteral(prefix));

        parts.Add(visitor.VisitToString(argument));

        if (suffix.Length > 0)
            parts.Add(SqlLiteral.ToSqlStringLiteral(suffix));

        return visitor.Dialect.MakeConcat(parts);
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
