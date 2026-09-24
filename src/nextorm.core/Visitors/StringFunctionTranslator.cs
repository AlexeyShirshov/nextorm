using System.Globalization;
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
                // Only the culture-less overloads are portable. ToUpperInvariant/ToLowerInvariant map to
                // the same upper/lower (they differ only for non-ASCII, which is documented); a
                // CultureInfo argument is rejected below.
                case nameof(string.ToUpper) when node.Arguments.Count == 0:
                case nameof(string.ToUpperInvariant) when node.Arguments.Count == 0:
                    return EmitStringFunction(visitor, node.Object, v => visitor.Dialect.MakeUpper(v));
                case nameof(string.ToLower) when node.Arguments.Count == 0:
                case nameof(string.ToLowerInvariant) when node.Arguments.Count == 0:
                    return EmitStringFunction(visitor, node.Object, v => visitor.Dialect.MakeLower(v));
                case nameof(string.ToUpper):
                case nameof(string.ToLower):
                    return TryTranslateCultureCase(visitor, node);
                // Trim/TrimStart/TrimEnd also have a (char)/params char[] overload that is not a
                // whitespace trim, so only the parameterless form is translated.
                case nameof(string.Trim) when node.Arguments.Count == 0:
                    return EmitStringFunction(visitor, node.Object, v => visitor.Dialect.MakeTrim(v, StringTrimKind.Both));
                case nameof(string.TrimStart) when node.Arguments.Count == 0:
                    return EmitStringFunction(visitor, node.Object, v => visitor.Dialect.MakeTrim(v, StringTrimKind.Start));
                case nameof(string.TrimEnd) when node.Arguments.Count == 0:
                    return EmitStringFunction(visitor, node.Object, v => visitor.Dialect.MakeTrim(v, StringTrimKind.End));
                case nameof(string.Trim):
                case nameof(string.TrimStart):
                case nameof(string.TrimEnd):
                    throw new NotSupportedException($"string.{methodName} with a character argument is not supported: SQL trims whitespace only.");
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
                case nameof(string.Equals):
                    return TryTranslateEquals(visitor, node);
            }

            return false;
        }

        // Static string methods.
        if (node.Object is null && methodName == nameof(string.IsNullOrEmpty))
            return TryTranslateIsNullOrEmpty(visitor, node);

        if (node.Object is null && methodName == nameof(string.Join))
            return TryTranslateJoin(visitor, node);

        if (node.Object is null && methodName == nameof(string.Equals))
            return TryTranslateEquals(visitor, node);

        if (node.Object is null && methodName is nameof(string.Compare) or nameof(string.CompareOrdinal))
            return TryTranslateCompare(visitor, node);

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

    /// <summary>
    /// Translates <c>string.ToUpper(CultureInfo)</c>/<c>string.ToLower(CultureInfo)</c>. Only
    /// <see cref="CultureInfo.InvariantCulture"/> has a portable SQL form; any other culture is
    /// rejected instead of silently using the database locale.
    /// </summary>
    private static bool TryTranslateCultureCase(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Arguments is [ConstantExpression { Value: CultureInfo culture }] && ReferenceEquals(culture, CultureInfo.InvariantCulture))
        {
            var upper = node.Method.Name == nameof(string.ToUpper);
            return EmitStringFunction(visitor, node.Object!, v => upper ? visitor.Dialect.MakeUpper(v) : visitor.Dialect.MakeLower(v));
        }

        throw new NotSupportedException($"string.{node.Method.Name}(CultureInfo) is only supported with CultureInfo.InvariantCulture; other cultures have no portable SQL form.");
    }

    /// <summary>
    /// Translates <c>string.Equals</c> (static and instance) into ordinal equality. <c>string.Equals</c>
    /// is ordinal in C#, so the operands are normalised with <see cref="ISqlDialect.MakeOrdinal"/>; the
    /// plain <c>==</c> operator keeps the provider's own collation and is documented separately.
    /// </summary>
    private static bool TryTranslateEquals(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;
        Expression left;
        Expression right;
        Expression? comparison = null;

        if (node.Object is null)
        {
            if (args.Count is < 2 or > 3 || args[0].Type != typeof(string) || args[1].Type != typeof(string))
                return false;

            left = args[0];
            right = args[1];
            if (args.Count == 3) comparison = args[2];
        }
        else
        {
            if (args.Count is < 1 or > 2)
                return false;

            right = UnwrapConvert(args[0]);
            if (right.Type != typeof(string))
                return false;

            left = node.Object;
            if (args.Count == 2) comparison = args[1];
        }

        var ignoreCase = ResolveOrdinal(comparison);
        RequireOrdinalComparison(visitor);

        if (visitor.IsParamMode)
        {
            visitor.Visit(left);
            visitor.Visit(right);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var leftSql = visitor.Dialect.MakeOrdinal(visitor.VisitToStringSuppressingColumnCollation(left), ignoreCase);
        var rightSql = visitor.Dialect.MakeOrdinal(visitor.VisitToStringSuppressingColumnCollation(right), ignoreCase);
        visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate($"({leftSql} = {rightSql})", visitor.IsPredicateContext));
        return true;
    }

    /// <summary>
    /// Translates <c>string.Compare</c>/<c>string.CompareOrdinal</c> with an ordinal
    /// <see cref="StringComparison"/> into a signed <c>CASE</c> expression, so an enclosing comparison
    /// (<c>Compare(a, b, StringComparison.Ordinal) &gt; 0</c>) keeps its SQL operator. The
    /// culture-sensitive overloads are rejected.
    /// </summary>
    private static bool TryTranslateCompare(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;
        if (args.Count is < 2 or > 3 || args[0].Type != typeof(string) || args[1].Type != typeof(string))
            return false;

        Expression? comparison = null;

        if (node.Method.Name == nameof(string.CompareOrdinal))
        {
            if (args.Count != 2)
                return false;
        }
        else if (args.Count == 2)
        {
            throw new NotSupportedException("string.Compare without a StringComparison is culture-sensitive; use string.Compare(a, b, StringComparison.Ordinal).");
        }
        else if (args[2].Type == typeof(bool))
        {
            throw new NotSupportedException("string.Compare(a, b, bool) is culture-sensitive; use string.Compare(a, b, StringComparison.Ordinal).");
        }
        else
        {
            comparison = args[2];
        }

        var ignoreCase = ResolveOrdinal(comparison);
        RequireOrdinalComparison(visitor);

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[0]);
            visitor.Visit(args[1]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var left = visitor.Dialect.MakeOrdinal(visitor.VisitToStringSuppressingColumnCollation(args[0]), ignoreCase);
        var right = visitor.Dialect.MakeOrdinal(visitor.VisitToStringSuppressingColumnCollation(args[1]), ignoreCase);
        visitor.Builder!.Append($"case when {left} < {right} then -1 when {left} > {right} then 1 else 0 end");
        return true;
    }

    /// <summary>
    /// Resolves an ordinal <see cref="StringComparison"/> constant to its case-sensitivity. The
    /// culture-sensitive and invariant-culture variants have no portable SQL form and are rejected.
    /// </summary>
    private static bool ResolveOrdinal(Expression? comparison)
    {
        if (comparison is null)
            return false;

        if (comparison is not ConstantExpression { Value: StringComparison value })
            throw new NotSupportedException("The StringComparison argument of a string method must be a constant.");

        return value switch
        {
            StringComparison.Ordinal => false,
            StringComparison.OrdinalIgnoreCase => true,
            _ => throw new NotSupportedException($"StringComparison.{value} is not supported; only Ordinal and OrdinalIgnoreCase have a portable SQL form.")
        };
    }

    private static void RequireOrdinalComparison(BaseExpressionVisitor visitor)
    {
        if (!visitor.Dialect.SupportsOrdinalComparison)
            throw new NotSupportedException("Ordinal string comparison (StringComparison.Ordinal/OrdinalIgnoreCase) is not supported by this provider.");
    }

    private static Expression UnwrapConvert(Expression expression) =>
        expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            ? unary.Operand
            : expression;

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
        if (node.Object is null || args.Count is < 1 or > 3 || args[0].Type != typeof(string))
            return false;

        // IndexOf(string[, int][, StringComparison]). The char overload has no portable SQL form.
        var startIndex = -1;
        var comparisonIndex = -1;

        for (var i = 1; i < args.Count; i++)
        {
            if (args[i].Type == typeof(int))
            {
                // IndexOf(string, int start, int count) has two int arguments; the count overload has
                // no portable SQL form and must not be mistaken for a start index.
                if (startIndex >= 0)
                    return false;

                startIndex = i;
            }
            else if (args[i].Type == typeof(StringComparison))
            {
                if (comparisonIndex >= 0)
                    return false;

                comparisonIndex = i;
            }
            else
            {
                return false;
            }
        }

        var ignoreCase = comparisonIndex >= 0 && ResolveOrdinal(args[comparisonIndex]);
        if (comparisonIndex >= 0)
            RequireOrdinalComparison(visitor);

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            visitor.Visit(args[0]);
            if (startIndex >= 0)
                visitor.Visit(args[startIndex]);

            return true;
        }

        visitor.NeedAliasForColumn = true;
        var ordinal = comparisonIndex >= 0;
        var value = ordinal ? visitor.VisitToStringSuppressingColumnCollation(node.Object) : visitor.VisitToString(node.Object);
        var substring = ordinal ? visitor.VisitToStringSuppressingColumnCollation(args[0]) : visitor.VisitToString(args[0]);

        if (ordinal)
        {
            value = visitor.Dialect.MakeOrdinal(value, ignoreCase);
            substring = visitor.Dialect.MakeOrdinal(substring, ignoreCase);
        }

        var start = startIndex >= 0 ? visitor.VisitToString(args[startIndex]) : null;
        visitor.Builder!.Append(visitor.Dialect.MakeStringIndexOf(value, substring, start));
        return true;
    }

    private static bool TryTranslateLastIndexOf(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;
        if (node.Object is null || args.Count is < 1 or > 2 || args[0].Type != typeof(string))
            return false;

        // LastIndexOf(string[, StringComparison]); the int-start overload has no primitive here.
        if (args.Count == 2 && args[1].Type != typeof(StringComparison))
            return false;

        var ignoreCase = args.Count == 2 && ResolveOrdinal(args[1]);
        if (args.Count == 2)
            RequireOrdinalComparison(visitor);

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            visitor.Visit(args[0]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var ordinal = args.Count == 2;
        var value = ordinal ? visitor.VisitToStringSuppressingColumnCollation(node.Object) : visitor.VisitToString(node.Object);
        var substring = ordinal ? visitor.VisitToStringSuppressingColumnCollation(args[0]) : visitor.VisitToString(args[0]);

        if (ordinal)
        {
            value = visitor.Dialect.MakeOrdinal(value, ignoreCase);
            substring = visitor.Dialect.MakeOrdinal(substring, ignoreCase);
        }

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

        if (visitor.Dialect.StringSplit is not { } stringSplit)
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
        visitor.Builder!.Append(stringSplit.Render(
            SqlLiteral.ToSqlStringLiteral(separator),
            visitor.VisitToString(node.Object)));
        return true;
    }

    private static bool TryTranslateStringLike(BaseExpressionVisitor visitor, MethodCallExpression node, LikePosition position)
    {
        var args = node.Arguments;
        if (node.Object is null || args.Count is < 1 or > 2 || args[0].Type != typeof(string))
            return false;

        if (args.Count == 2 && args[1].Type != typeof(StringComparison))
            return false;

        var hasComparison = args.Count == 2;
        var ignoreCase = hasComparison && ResolveOrdinal(args[1]);
        if (hasComparison)
            RequireOrdinalComparison(visitor);

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            visitor.Visit(args[0]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var value = hasComparison
            ? visitor.VisitToStringSuppressingColumnCollation(node.Object)
            : visitor.VisitToString(node.Object);
        var pattern = BuildLikePattern(visitor, args[0], position, out var escaped);

        if (hasComparison)
        {
            if (!ignoreCase && !visitor.Dialect.SupportsOrdinalLike)
                throw new NotSupportedException("A case-sensitive ordinal Contains/StartsWith/EndsWith cannot be expressed by this provider; use the collation-based overload or StringComparison.OrdinalIgnoreCase.");

            // The left operand's collation governs the LIKE comparison; for a case-insensitive ordinal
            // match both sides are case-folded instead.
            value = visitor.Dialect.MakeOrdinal(value, ignoreCase);
            if (ignoreCase)
                pattern = $"lower({pattern})";
        }

        var predicate = escaped
            ? $"{value} {visitor.Kw("like")} {pattern}{visitor.Dialect.MakeLikeEscape("\\", visitor.KeywordCase)}"
            : $"{value} {visitor.Kw("like")} {pattern}";
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
        visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate($"({value} {visitor.Kw("is null")} {visitor.Kw("or")} {value} = {visitor.Dialect.EmptyString})", visitor.IsPredicateContext));
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
