using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>Which part of a string a LIKE-style method (<c>Contains</c>/<c>StartsWith</c>/<c>EndsWith</c>) matches.</summary>
internal enum LikePosition
{
    Contains,
    StartsWith,
    EndsWith
}

/// <summary>
/// Translates the built-in <see cref="string"/> instance and static methods into SQL. Split out of
/// <see cref="ScalarFunctionTranslator"/> so the string surface (the largest group) stays below the
/// god-class threshold; the dispatch order and the emitted SQL are unchanged.
/// </summary>
internal static class StringFunctionTranslator
{
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
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
                case nameof(string.Remove):
                    return TryTranslateRemove(visitor, node);
                case nameof(string.Insert):
                    return TryTranslateInsert(visitor, node);
                case nameof(string.IndexOf):
                    return TryTranslateIndexOf(visitor, node);
                case nameof(string.LastIndexOf):
                    return TryTranslateLastIndexOf(visitor, node);
                case nameof(string.PadLeft):
                    return TryTranslatePad(visitor, node, left: true);
                case nameof(string.PadRight):
                    return TryTranslatePad(visitor, node, left: false);
                case nameof(string.Contains):
                    return TryTranslateStringLike(visitor, node, LikePosition.Contains);
                case nameof(string.StartsWith):
                    return TryTranslateStringLike(visitor, node, LikePosition.StartsWith);
                case nameof(string.EndsWith):
                    return TryTranslateStringLike(visitor, node, LikePosition.EndsWith);
                case nameof(string.Split):
                    return TryTranslateSplit(visitor, node);
            }

            return false;
        }

        // Static string methods.
        if (node.Object is null && methodName == nameof(string.IsNullOrEmpty))
            return TryTranslateIsNullOrEmpty(visitor, node);

        if (node.Object is null && methodName == nameof(string.Join))
            return TryTranslateJoin(visitor, node);

        return false;
    }

    /// <summary>
    /// Translates <c>string.Join(separator, string[])</c> to the provider's array-to-string
    /// function (<c>array_to_string</c>) on a provider with native arrays (PostgreSQL). A call whose
    /// array is entirely constant is left to the constant-folding path instead.
    /// </summary>
    private static bool TryTranslateJoin(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;
        if (args.Count != 2 || args[0].Type != typeof(string))
            return false;

        var valuesType = args[1].Type;
        if (!valuesType.IsArray || valuesType.GetElementType() != typeof(string))
            return false;

        // A constant-only join is folded to a parameter, exactly as before this translation existed.
        if (!node.Has<ParameterExpression>())
            return false;

        if (!visitor.Dialect.SupportsArrays)
            throw new NotSupportedException("string.Join over an array requires a provider with native arrays (PostgreSQL).");

        // PostgreSQL orders the arguments as array_to_string(array, separator).
        SqlOperandTranslator.EmitFunction(visitor, "array_to_string", [args[1], args[0]]);
        return true;
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

    private static bool TryTranslateRemove(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;
        if (node.Object is null || args.Count is < 1 or > 2)
            return false;

        // Only the (int) and (int, int) overloads map to a splice; there is no Range form on Remove.
        if (args[0].Type != typeof(int) || (args.Count == 2 && args[1].Type != typeof(int)))
            return false;

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            for (var i = 0; i < args.Count; i++) visitor.Visit(args[i]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var value = visitor.VisitToString(node.Object);
        var start = visitor.VisitToString(args[0]);
        var count = args.Count == 2 ? visitor.VisitToString(args[1]) : null;
        visitor.Builder!.Append(visitor.Dialect.MakeStuff(value, start, count, visitor.Dialect.EmptyString));
        return true;
    }

    private static bool TryTranslateInsert(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;
        if (node.Object is null || args.Count != 2)
            return false;

        if (args[0].Type != typeof(int) || args[1].Type != typeof(string))
            return false;

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            visitor.Visit(args[0]);
            visitor.Visit(args[1]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var value = visitor.VisitToString(node.Object);
        var start = visitor.VisitToString(args[0]);
        var text = visitor.VisitToString(args[1]);
        // A zero replacement length inserts the text at start instead of removing anything.
        visitor.Builder!.Append(visitor.Dialect.MakeStuff(value, start, count: "0", text));
        return true;
    }

    private static bool TryTranslateIndexOf(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;
        if (node.Object is null || args.Count is < 1 or > 2)
            return false;

        // IndexOf(string) and IndexOf(string, int); the char and StringComparison overloads have no
        // portable SQL form.
        if (args[0].Type != typeof(string) || (args.Count == 2 && args[1].Type != typeof(int)))
            return false;

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            visitor.Visit(args[0]);
            if (args.Count == 2) visitor.Visit(args[1]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var value = visitor.VisitToString(node.Object);
        var substring = visitor.VisitToString(args[0]);
        var start = args.Count == 2 ? visitor.VisitToString(args[1]) : null;
        visitor.Builder!.Append(visitor.Dialect.MakeStringIndexOf(value, substring, start));
        return true;
    }

    private static bool TryTranslateLastIndexOf(BaseExpressionVisitor visitor, MethodCallExpression node)
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
        var substring = visitor.VisitToString(args[0]);
        visitor.Builder!.Append(visitor.Dialect.MakeStringLastIndexOf(value, substring));
        return true;
    }

    private static bool TryTranslatePad(BaseExpressionVisitor visitor, MethodCallExpression node, bool left)
    {
        var args = node.Arguments;
        if (node.Object is null || args.Count is < 1 or > 2)
            return false;

        if (args[0].Type != typeof(int))
            return false;

        // SQL has no char type, so the padding character has to be a compile-time constant that can be
        // rendered as a one-character string literal.
        string pad;
        if (args.Count == 2)
        {
            if (!SqlLiteral.TryGetConstantString(args[1], out var padValue))
                throw new NotSupportedException("The string.PadLeft/PadRight padding character must be a constant.");

            pad = SqlLiteral.ToSqlStringLiteral(padValue);
        }
        else
        {
            pad = SqlLiteral.ToSqlStringLiteral(" ");
        }

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            for (var i = 0; i < args.Count; i++) visitor.Visit(args[i]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakePad(
            visitor.VisitToString(node.Object),
            visitor.VisitToString(args[0]),
            pad,
            left));
        return true;
    }

    /// <summary>
    /// Translates a <c>string.Split</c> call whose separator is a single character or a
    /// one-character constant string into the provider's scalar array-split form
    /// (<c>splitByChar(separator, value)</c> on ClickHouse). The result is a <c>string[]</c>, so it can
    /// only be used as the operand of another array function; the count overload, multiple
    /// separators, a multi-character separator and <c>StringSplitOptions</c> other than
    /// <c>None</c> are rejected.
    /// </summary>
    private static bool TryTranslateSplit(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;
        if (node.Object is null || args.Count is < 1 or > 2)
            throw new NotSupportedException("This string.Split overload is not supported; only a single-separator overload maps to SQL.");

        if (args.Count == 2
            && args[1] is ConstantExpression { Value: StringSplitOptions options }
            && options != StringSplitOptions.None)
            throw new NotSupportedException("string.Split with StringSplitOptions other than None is not supported.");

        if (args.Count == 2 && args[1] is not ConstantExpression { Value: StringSplitOptions })
            throw new NotSupportedException("This string.Split overload is not supported; only a single-separator overload maps to SQL.");

        var candidate = args[0];
        if (candidate is NewArrayExpression { Expressions: [var single] })
            candidate = single;
        else if (candidate is NewArrayExpression)
            throw new NotSupportedException("string.Split with multiple separators is not supported.");

        if (candidate.Type != typeof(char) && candidate.Type != typeof(string))
            throw new NotSupportedException("This string.Split overload is not supported; only a single char/string separator maps to SQL.");

        if (!visitor.Dialect.SupportsStringSplit)
            throw new NotSupportedException("string.Split is not supported by this provider.");

        if (!SqlLiteral.TryGetConstantString(candidate, out var separator))
            throw new NotSupportedException("The string.Split separator must be a constant character or single-character string.");

        if (separator.Length != 1)
            throw new NotSupportedException("string.Split requires a one-character separator; multi-character separators are not supported.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeStringSplit(
            SqlLiteral.ToSqlStringLiteral(separator),
            visitor.VisitToString(node.Object)));
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
            ? $"{value} like {pattern}{visitor.Dialect.MakeLikeEscape("\\")}"
            : $"{value} like {pattern}";
        visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate(predicate, visitor.IsPredicateContext));
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
}
