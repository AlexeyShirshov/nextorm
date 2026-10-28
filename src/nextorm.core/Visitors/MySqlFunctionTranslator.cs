using System.Globalization;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the MySQL/MariaDB-only functions of <see cref="MySqlFunctions"/> through the dialect's
/// <see cref="IMySqlFunctions"/> renderer. The family is recognized by the declaring type, so the
/// same-named PostgreSQL members (<c>md5</c>, <c>format</c>) still fall through to
/// <see cref="ExtendedScalarFunctionTranslator"/>. A provider that does not expose the family, or that
/// does not support a specific name, fails with a clear message instead of emitting invalid SQL.
/// </summary>
internal static class MySqlFunctionTranslator
{
    private static readonly Dictionary<string, int> VariadicLeads = new(StringComparer.Ordinal)
    {
        [nameof(MySqlFunctions.field)] = 1,
        [nameof(MySqlFunctions.elt)] = 1,
        [nameof(MySqlFunctions.make_set)] = 1,
        [nameof(MySqlFunctions.json_array)] = 0,
        [nameof(MySqlFunctions.json_object)] = 0,
        [nameof(MySqlFunctions.json_merge)] = 2,
        [nameof(MySqlFunctions.json_contains_path)] = 2,
        [nameof(MySqlFunctions.json_search)] = 4,
        [nameof(MySqlFunctions.grouping)] = 0,
        // T350 (#284): the MariaDB-only variadic shapes — the leading arguments are positional and the
        // remaining ones are the variadic tail.
        [nameof(MySqlFunctions.sformat)] = 1,
        [nameof(MySqlFunctions.json_array_intersect)] = 2,
        [nameof(MySqlFunctions.json_object_filter_keys)] = 1
    };

    /// <summary>
    /// The declared shape refusals: names whose surface member exists only to carry the precise message
    /// and whose translation fails before any argument visit or SQL emission.
    /// </summary>
    private static readonly Dictionary<string, string> Refused = new(StringComparer.Ordinal)
    {
        [nameof(MySqlFunctions.json_table)] = MySqlFunctions.JsonTableRefusal,
        [nameof(MySqlFunctions.uuid)] = MySqlFunctions.UuidRefusal,
        [nameof(MySqlFunctions.values)] = MySqlFunctions.ValuesRefusal,
        // T350 (#284): the MariaDB 11.4 rows the shared surface cannot express. The route is shared with
        // MySQL, which has none of these names, and every one of them fails before an argument is visited.
        [nameof(MySqlFunctions.column_get)] = MySqlFunctions.ColumnGetRefusal,
        [nameof(MySqlFunctions.trunc)] = MySqlFunctions.TruncRefusal,
        [nameof(MySqlFunctions._rowid)] = MySqlFunctions.RowIdRefusal,
        [nameof(MySqlFunctions.vec_from_text)] = MySqlFunctions.VectorRefusal,
        [nameof(MySqlFunctions.vec_to_text)] = MySqlFunctions.VectorRefusal,
        [nameof(MySqlFunctions.vec_distance_euclidean)] = MySqlFunctions.VectorRefusal,
        [nameof(MySqlFunctions.vec_distance_cosine)] = MySqlFunctions.VectorRefusal,
        [nameof(MySqlFunctions.vec_distance)] = MySqlFunctions.VectorRefusal
    };

    /// <summary>
    /// The MariaDB sequence functions whose first argument is an object identifier: the dialect's
    /// <c>SequenceName</c> unquotes the plain token into the sequence name, so the value policy's
    /// charset-introduced hex spelling must never reach it.
    /// </summary>
    private static readonly HashSet<string> SequenceNameFunctions = new(StringComparer.Ordinal)
    {
        nameof(MySqlFunctions.next_value_for), nameof(MySqlFunctions.nextval),
        nameof(MySqlFunctions.setval), nameof(MySqlFunctions.lastval),
        nameof(MySqlFunctions.previous_value_for)
    };

    /// <summary>Translates a MySQL/MariaDB-only call; returns <c>false</c> when it is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType is not { } declaring
            || !typeof(MySqlFunctions).IsAssignableFrom(declaring))
            return false;

        var name = node.Method.Name;

        if (Refused.TryGetValue(name, out var refusal))
            throw new NotSupportedException(refusal);

        if (visitor.Dialect.MySqlFunctions is not { } mySql || !mySql.Supports(name))
            throw new NotSupportedException($"The {name} function is not supported by this provider.");

        if (name is nameof(MySqlFunctions.json_arrow) or nameof(MySqlFunctions.json_arrow_text))
            return TranslateJsonArrow(visitor, node, mySql);

        if (name == nameof(MySqlFunctions.get_format))
        {
            // GET_FORMAT(DATE, 'EUR') is grammar: the first argument is a bare keyword and the second a
            // plain quoted literal, so neither may go through the generic value renderer.
            var (kind, format) = ReadGetFormat(visitor, node.Arguments);

            if (visitor.IsParamMode)
            {
                for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
                    visitor.Visit(node.Arguments[i]);

                return true;
            }

            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(mySql.Render(name, [kind, format]));
            return true;
        }

        if (name == nameof(MySqlFunctions.name_const))
            return TranslateNameConst(visitor, node, mySql);

        if (name == nameof(MySqlFunctions.column_default))
        {
            // DEFAULT(column) is grammar over a column identifier: the server resolves the column's
            // default, so a constant, parameter, cast or computed expression has no meaning there.
            if (node.Arguments.Count != 1 || node.Arguments[0] is not MemberExpression)
                throw new NotSupportedException(ColumnDefaultError);

            if (visitor.IsParamMode)
            {
                visitor.Visit(node.Arguments[0]);
                return true;
            }

            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(mySql.Render(name, [visitor.VisitToString(node.Arguments[0])]));
            return true;
        }

        var args = VariadicLeads.TryGetValue(name, out var leading)
            ? ArgumentFlattener.Flatten(node.Arguments, leading)
            : (IReadOnlyList<Expression>)node.Arguments;

        if (MinimumArgumentCount(name) is var minimum && args.Count < minimum)
            throw new NotSupportedException(name == nameof(MySqlFunctions.json_contains_path)
                ? "The json_contains_path function requires at least one path argument."
                : $"The {name} function requires at least one value argument.");

        ValidateJsonGrammarArguments(name, args);

        var hasWeightStringModifiers =
            name == nameof(MySqlFunctions.weight_string) && args.Count == 3;
        (MySqlWeightStringCast Cast, int Length) weightStringModifiers = hasWeightStringModifiers
            ? ReadWeightStringModifiers(args[1], args[2])
            : default;

        if (name == nameof(MySqlFunctions.export_set) && args.Count == 5
            && TypeFacts.UnwrapConvert(args[4]) is ConstantExpression { Value: int bitCount }
            && bitCount is < 0 or > 64)
            throw new NotSupportedException(
                $"The export_set function requires a bit count between 0 and 64 (got {bitCount}).");

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return true;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            // The weight_string modifiers are grammar tokens, not values: the cast keyword and the
            // constant length are emitted as SQL text so the enum never reaches the value policy.
            if (hasWeightStringModifiers && i == 1)
            {
                rendered[i] = weightStringModifiers.Cast == MySqlWeightStringCast.Char ? "CHAR" : "BINARY";
                continue;
            }

            if (hasWeightStringModifiers && i == 2)
            {
                rendered[i] = weightStringModifiers.Length.ToString(CultureInfo.InvariantCulture);
                continue;
            }

            // grammar token: the sequence name is an identifier the dialect unquotes, not a data
            // value, so it keeps the historic plain quoted token (the value policy emitted
            // `_utf8mb4 X'…'` for it and `next value for` received the hex literal).
            if (i == 0 && SequenceNameFunctions.Contains(name)
                && TypeFacts.UnwrapConvert(args[i]) is ConstantExpression { Value: string sequence })
            {
                rendered[i] = visitor.RenderStringLiteral(
                    sequence, SqlStringLiteralRole.GrammarToken, SqlStringGrammar.Keyword);
                continue;
            }

            // grammar token: the `->` path, `MEMBER OF` array, `one_or_all` keyword, `JSON_SEARCH`
            // escape and every JSON path must be inline literals — a parameter or the value policy's
            // `_utf8mb4 X'…'` binary expression is invalid in those grammar positions.
            if (IsGrammarArgument(name, i))
            {
                rendered[i] = TypeFacts.UnwrapConvert(args[i]) switch
                {
                    ConstantExpression { Value: string grammar } =>
                        visitor.RenderStringLiteral(
                            grammar, SqlStringLiteralRole.GrammarToken, SqlStringGrammar.Keyword),
                    ConstantExpression { Value: null } => "null",
                    _ => visitor.VisitToString(args[i])
                };
                continue;
            }

            rendered[i] = visitor.VisitToString(args[i]);

        }

        visitor.Builder!.Append(mySql.Render(name, rendered));
        return true;
    }

    private const string GetFormatError =
        "MySQL GET_FORMAT() requires a compile-time IANA format kind and format literal.";

    /// <summary>
    /// Reads the two grammar constants of <c>get_format</c>: the family keyword is emitted bare and the
    /// platform format as a plain quoted literal. Both must be compile-time constants; anything else is
    /// refused before any SQL (or argument evaluation) is emitted.
    /// </summary>
    private static (string Kind, string Format) ReadGetFormat(
        BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (args.Count != 2
            || !TryReadGetFormatKind(args[0], out var kind)
            || TypeFacts.UnwrapConvert(args[1]) is not ConstantExpression { Value: string format }
            || !IsKnownGetFormat(format))
            throw new NotSupportedException(GetFormatError);

        var keyword = kind switch
        {
            MySqlFormatKind.Date => "DATE",
            MySqlFormatKind.Time => "TIME",
            _ => "DATETIME"
        };

        return (keyword, visitor.RenderStringLiteral(
            format, SqlStringLiteralRole.GrammarToken, SqlStringGrammar.Keyword));
    }

    private static bool TryReadGetFormatKind(Expression expression, out MySqlFormatKind kind)
    {
        switch (TypeFacts.UnwrapConvert(expression))
        {
            case ConstantExpression { Value: MySqlFormatKind value } when Enum.IsDefined(value):
                kind = value;
                return true;
            case ConstantExpression { Value: int raw } when Enum.IsDefined(typeof(MySqlFormatKind), raw):
                kind = (MySqlFormatKind)raw;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    /// <summary>The platform format names MySQL 8.4 documents for every <c>GET_FORMAT</c> family.</summary>
    private static bool IsKnownGetFormat(string format) =>
        format is "USA" or "JIS" or "ISO" or "EUR" or "INTERNAL";

    private const string ColumnDefaultError =
        "MySQL DEFAULT() requires a column reference; constants, parameters, casts, and computed " +
        "expressions are not supported.";

    private const string NameConstLiteralError =
        "MySQL NAME_CONST() requires argument 0 to be a compile-time string literal.";

    private const string NameConstNullError = "MySQL NAME_CONST() requires a non-null name literal.";

    /// <summary>
    /// Renders <c>name_const('label', value)</c>: the label MySQL accepts only as a compile-time string
    /// literal is inlined as a quoted grammar token, so neither a parameter nor the value policy's
    /// <c>_utf8mb4 X'…'</c> binary expression can reach that position. A null or non-constant name is
    /// refused before any SQL is emitted.
    /// </summary>
    private static bool TranslateNameConst(
        BaseExpressionVisitor visitor, MethodCallExpression node, IMySqlFunctions mySql)
    {
        if (node.Arguments.Count != 2)
            throw new NotSupportedException(NameConstLiteralError);

        var name = TypeFacts.UnwrapConvert(node.Arguments[0]) switch
        {
            ConstantExpression { Value: string literal } => literal,
            ConstantExpression { Value: null } => throw new NotSupportedException(NameConstNullError),
            _ => throw new NotSupportedException(NameConstLiteralError)
        };

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Arguments[0]);
            visitor.Visit(node.Arguments[1]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var label = visitor.RenderStringLiteral(
            name, SqlStringLiteralRole.GrammarToken, SqlStringGrammar.Keyword);
        visitor.Builder!.Append(mySql.Render(node.Method.Name, [label, visitor.VisitToString(node.Arguments[1])]));
        return true;
    }

    private static (MySqlWeightStringCast Cast, int Length) ReadWeightStringModifiers(
        Expression castExpression, Expression lengthExpression)
    {
        if (!TryReadWeightStringCast(castExpression, out var cast))
            throw new NotSupportedException(
                "The weight_string function requires a constant cast " +
                "(MySqlWeightStringCast.Char or MySqlWeightStringCast.Binary).");

        if (TypeFacts.UnwrapConvert(lengthExpression) is not ConstantExpression { Value: int length })
            throw new NotSupportedException("The weight_string function requires a constant length.");

        if (length < 1)
            throw new NotSupportedException("The weight_string function requires a length of at least 1.");

        return (cast, length);
    }

    private static bool TryReadWeightStringCast(Expression expression, out MySqlWeightStringCast cast)
    {
        switch (TypeFacts.UnwrapConvert(expression))
        {
            case ConstantExpression { Value: MySqlWeightStringCast value }:
                cast = value;
                return true;
            case ConstantExpression { Value: int raw }
                when Enum.IsDefined(typeof(MySqlWeightStringCast), raw):
                cast = (MySqlWeightStringCast)raw;
                return true;
            default:
                cast = default;
                return false;
        }
    }

    private const string JsonObjectArgumentCountError =
        "MySQL JSON_OBJECT() requires an even number of key/value arguments.";

    private const string MemberOfArrayError =
        "MySQL MEMBER OF requires argument 1 to be a compile-time JSON-array string literal.";

    /// <summary>
    /// The minimum number of (flattened) arguments each guarded name accepts; <c>0</c> means "as the
    /// signature allows" and lets the arity stay a compile-time concern.
    /// </summary>
    private static int MinimumArgumentCount(string name) => name switch
    {
        nameof(MySqlFunctions.field) or nameof(MySqlFunctions.elt)
            or nameof(MySqlFunctions.make_set) => 2,
        nameof(MySqlFunctions.json_merge) => 2,
        nameof(MySqlFunctions.json_contains_path) or nameof(MySqlFunctions.json_search) => 3,
        nameof(MySqlFunctions.json_array) or nameof(MySqlFunctions.json_object) => 0,
        nameof(MySqlFunctions.sformat) => 1,
        nameof(MySqlFunctions.json_array_intersect) => 2,
        nameof(MySqlFunctions.json_object_filter_keys) => 2,
        _ => 0
    };

    /// <summary>
    /// True for the argument positions MySQL reads as grammar literals rather than as value expressions:
    /// <c>JSON_CONTAINS_PATH</c>'s <c>one_or_all</c> and paths, <c>JSON_SEARCH</c>'s <c>one_or_all</c>,
    /// escape and paths, and <c>MEMBER OF</c>'s JSON array. Those are inlined as quoted tokens; a
    /// parameter or the value policy's <c>_utf8mb4 X'…'</c> spelling is invalid there.
    /// </summary>
    private static bool IsGrammarArgument(string name, int index) => name switch
    {
        nameof(MySqlFunctions.json_contains_path) => index >= 1,
        nameof(MySqlFunctions.json_search) => index is 1 or 3 || index >= 4,
        nameof(MySqlFunctions.member_of) => index == 1,
        nameof(MySqlFunctions.json_key_value) => index == 1,
        _ => false
    };

    /// <summary>
    /// Validates the compile-time literals of the JSON family before any argument is visited or any SQL
    /// is emitted, so a wrong shape fails with a precise message instead of invalid SQL.
    /// </summary>
    private static void ValidateJsonGrammarArguments(string name, IReadOnlyList<Expression> args)
    {
        switch (name)
        {
            case nameof(MySqlFunctions.json_object) when args.Count % 2 != 0:
                throw new NotSupportedException(JsonObjectArgumentCountError);

            case nameof(MySqlFunctions.json_object):
                return;

            case nameof(MySqlFunctions.json_contains_path):
                RequireOneOrAll(args[1], "JSON_CONTAINS_PATH");
                RequireStringLiteralRange(args, 2, "JSON_CONTAINS_PATH", "path");
                return;

            case nameof(MySqlFunctions.json_search):
                RequireOneOrAll(args[1], "JSON_SEARCH");
                if (args.Count > 3)
                    RequireEscape(args[3]);
                RequireStringLiteralRange(args, 4, "JSON_SEARCH", "path");
                return;

            case nameof(MySqlFunctions.member_of):
                if (!IsCompileTimeString(args[1], allowNull: false))
                    throw new NotSupportedException(MemberOfArrayError);
                return;
        }
    }

    private static void RequireOneOrAll(Expression expression, string function)
    {
        var value = TypeFacts.UnwrapConvert(expression) is ConstantExpression { Value: string literal }
            ? literal
            : null;
        if (value is not ("one" or "all"))
            throw new NotSupportedException(
                $"MySQL {function}() requires one_or_all to be the compile-time literal 'one' or 'all'.");
    }

    private static void RequireEscape(Expression expression)
    {
        var unwrapped = TypeFacts.UnwrapConvert(expression);
        if (unwrapped is ConstantExpression { Value: null })
            return;
        if (unwrapped is not ConstantExpression { Value: string { Length: 1 } })
            throw new NotSupportedException(
                "MySQL JSON_SEARCH() requires escape_char to be a compile-time single-character literal.");
    }

    private static void RequireStringLiteralRange(
        IReadOnlyList<Expression> args, int start, string function, string role)
    {
        for (var i = start; i < args.Count; i++)
            if (!IsCompileTimeString(args[i], allowNull: true))
                throw new NotSupportedException(
                    $"MySQL {function}() requires every {role} argument to be a compile-time string literal.");
    }

    private static bool IsCompileTimeString(Expression expression, bool allowNull) =>
        TypeFacts.UnwrapConvert(expression) switch
        {
            ConstantExpression { Value: string } => true,
            ConstantExpression { Value: null } => allowNull,
            _ => false
        };

    /// <summary>
    /// Renders <c>column -&gt; 'path'</c> / <c>column -&gt;&gt; 'path'</c>: MySQL requires a column
    /// identifier on the left and a string literal on the right, so both are validated and the path is
    /// inlined as a grammar token before any SQL is emitted.
    /// </summary>
    private static bool TranslateJsonArrow(
        BaseExpressionVisitor visitor, MethodCallExpression node, IMySqlFunctions mySql)
    {
        var name = node.Method.Name;
        var op = name == nameof(MySqlFunctions.json_arrow) ? "->" : "->>";
        var args = node.Arguments;

        if (args.Count != 2 || TypeFacts.UnwrapConvert(args[0]) is not MemberExpression)
            throw new NotSupportedException(
                $"MySQL operator '{op}' requires argument 0 to be a column reference.");

        if (TypeFacts.UnwrapConvert(args[1]) is not ConstantExpression { Value: string path })
            throw new NotSupportedException(
                $"MySQL operator '{op}' requires argument 1 to be a compile-time string literal.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[0]);
            visitor.Visit(args[1]);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var column = visitor.VisitToString(args[0]);
        var literal = visitor.RenderStringLiteral(
            path, SqlStringLiteralRole.GrammarToken, SqlStringGrammar.Keyword);
        visitor.Builder!.Append(mySql.Render(name, [column, literal]));
        return true;
    }
}
