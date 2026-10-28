using System.Globalization;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the SQL Server-only T-SQL scalar functions of <see cref="SqlServerFunctions"/> (the
/// string functions <c>patindex</c>/<c>quotename</c>/<c>soundex</c>/<c>difference</c>/
/// <c>string_escape</c>/<c>unicode</c>/<c>nchar</c>/<c>format</c>, the trigonometric functions, the
/// date functions <c>datename</c>/<c>date_bucket</c>, the binary/system functions
/// <c>hashbytes</c>/<c>newsequentialid</c> and the SQL/JSON constructors, aggregates and predicates)
/// through the dialect's <see cref="ISqlDialect.SqlServerFunctions"/> renderer.
/// <para>
/// Only SQL Server exposes that renderer; every other provider rejects the members with a clear
/// message instead of emitting SQL it cannot execute. The boolean-returning JSON predicates are
/// materialised through <see cref="ISqlDialect.MakeBooleanPredicate"/> because T-SQL has no boolean
/// type.
/// </para>
/// </summary>
internal static class SqlServerScalarFunctionTranslator
{
    private static readonly HashSet<string> FunctionNames = new(StringComparer.Ordinal)
    {
        nameof(SqlServerFunctions.patindex), nameof(SqlServerFunctions.quotename),
        nameof(SqlServerFunctions.soundex), nameof(SqlServerFunctions.difference),
        nameof(SqlServerFunctions.string_escape), nameof(SqlServerFunctions.unicode),
        nameof(SqlServerFunctions.nchar), nameof(SqlServerFunctions.format),
        nameof(SqlServerFunctions.acos), nameof(SqlServerFunctions.asin),
        nameof(SqlServerFunctions.atan), nameof(SqlServerFunctions.atn2),
        nameof(SqlServerFunctions.square), nameof(SqlServerFunctions.datename),
        nameof(SqlServerFunctions.date_bucket), nameof(SqlServerFunctions.hashbytes),
        nameof(SqlServerFunctions.newsequentialid), nameof(SqlServerFunctions.json_array),
        nameof(SqlServerFunctions.json_object), nameof(SqlServerFunctions.json_arrayagg),
        nameof(SqlServerFunctions.json_objectagg), nameof(SqlServerFunctions.json_contains),
        nameof(SqlServerFunctions.json_path_exists),
        nameof(SqlServerFunctions.sysdatetime), nameof(SqlServerFunctions.sysdatetimeoffset),
        nameof(SqlServerFunctions.sysutcdatetime), nameof(SqlServerFunctions.switchoffset),
        nameof(SqlServerFunctions.todatetimeoffset), nameof(SqlServerFunctions.timefromparts),
        nameof(SqlServerFunctions.smalldatetimefromparts), nameof(SqlServerFunctions.datetimefromparts),
        nameof(SqlServerFunctions.datetime2fromparts), nameof(SqlServerFunctions.datetimeoffsetfromparts),
        nameof(SqlServerFunctions.checksum), nameof(SqlServerFunctions.binary_checksum),
        nameof(SqlServerFunctions.compress), nameof(SqlServerFunctions.decompress),
        nameof(SqlServerFunctions.rand), nameof(SqlServerFunctions.stuff),
        nameof(SqlServerFunctions.col_length), nameof(SqlServerFunctions.col_name),
        nameof(SqlServerFunctions.ident_incr), nameof(SqlServerFunctions.ident_seed),
        nameof(SqlServerFunctions.index_col), nameof(SqlServerFunctions.object_definition),
        nameof(SqlServerFunctions.object_id), nameof(SqlServerFunctions.object_name),
        nameof(SqlServerFunctions.object_schema_name), nameof(SqlServerFunctions.stats_date),
        nameof(SqlServerFunctions.db_id), nameof(SqlServerFunctions.db_name),
        nameof(SqlServerFunctions.original_db_name), nameof(SqlServerFunctions.schema_id),
        nameof(SqlServerFunctions.schema_name), nameof(SqlServerFunctions.type_id),
        nameof(SqlServerFunctions.type_name), nameof(SqlServerFunctions.filegroup_id),
        nameof(SqlServerFunctions.filegroup_name), nameof(SqlServerFunctions.file_id),
        nameof(SqlServerFunctions.file_idex), nameof(SqlServerFunctions.file_name),
        nameof(SqlServerFunctions.current_request_id),
        nameof(SqlServerFunctions.current_timezone), nameof(SqlServerFunctions.current_timezone_id),
        nameof(SqlServerFunctions.current_transaction_id),
        nameof(SqlServerFunctions.formatmessage), nameof(SqlServerFunctions.getansinull),
        nameof(SqlServerFunctions.ident_current),
        nameof(SqlServerFunctions.isdate), nameof(SqlServerFunctions.isnumeric),
        nameof(SqlServerFunctions.min_active_rowversion),
        nameof(SqlServerFunctions.parsename), nameof(SqlServerFunctions.publishingservername),
        nameof(SqlServerFunctions.parse), nameof(SqlServerFunctions.rowcount_big),
        nameof(SqlServerFunctions.scope_identity), nameof(SqlServerFunctions.str),
        nameof(SqlServerFunctions.try_parse), nameof(SqlServerFunctions.xact_state),
        nameof(SqlServerFunctions.next_value_for), nameof(SqlServerFunctions.nextval),
        nameof(SqlServerFunctions.regexp_count), nameof(SqlServerFunctions.left_shift),
        nameof(SqlServerFunctions.right_shift), nameof(SqlServerFunctions.bit_count),
        nameof(SqlServerFunctions.get_bit), nameof(SqlServerFunctions.set_bit),
        nameof(SqlServerFunctions.try_cast), nameof(SqlServerFunctions.try_convert)
    };

    /// <summary>
    /// The bit-manipulation functions whose T-SQL depends on the operand's own type: the translator
    /// passes the closed-map operand type to
    /// <see cref="ISqlServerFunctions.RenderBit"/> instead of the string-only
    /// <see cref="ISqlServerFunctions.Render"/>.
    /// </summary>
    private static readonly HashSet<string> BitFunctions = new(StringComparer.Ordinal)
    {
        nameof(SqlServerFunctions.left_shift), nameof(SqlServerFunctions.right_shift),
        nameof(SqlServerFunctions.bit_count), nameof(SqlServerFunctions.get_bit),
        nameof(SqlServerFunctions.set_bit)
    };

    /// <summary>
    /// The conversion functions rendered through the dialect's closed target map
    /// (<see cref="ISqlDialect.MakeTryCast"/>/<see cref="ISqlDialect.MakeTryConvert"/>): the target CLR
    /// type, never a string, decides the SQL type, and an unmapped target is refused before any command
    /// is prepared.
    /// </summary>
    private static readonly HashSet<string> TryConversionFunctions = new(StringComparer.Ordinal)
    {
        nameof(SqlServerFunctions.try_cast), nameof(SqlServerFunctions.try_convert)
    };

    /// <summary>The functions whose <c>params</c> array is flattened back into individual arguments.</summary>
    private static readonly HashSet<string> VariadicFunctions = new(StringComparer.Ordinal)
    {
        nameof(SqlServerFunctions.json_array), nameof(SqlServerFunctions.json_object),
        nameof(SqlServerFunctions.checksum), nameof(SqlServerFunctions.binary_checksum)
    };

    /// <summary>The maximum number of formatting arguments T-SQL's <c>FORMATMESSAGE</c> accepts.</summary>
    private const int MaxFormatMessageArgs = 20;

    /// <summary>The <c>*FROMPARTS</c>/<c>TIMEFROMPARTS</c> functions whose trailing precision must be a constant in 0..7.</summary>
    private static readonly HashSet<string> PrecisionFunctions = new(StringComparer.Ordinal)
    {
        nameof(SqlServerFunctions.timefromparts), nameof(SqlServerFunctions.datetime2fromparts),
        nameof(SqlServerFunctions.datetimeoffsetfromparts)
    };

    /// <summary>The functions whose T-SQL form returns an <c>int</c> that has to become a <c>bit</c> value.</summary>
    private static readonly HashSet<string> BooleanFunctions = new(StringComparer.Ordinal)
    {
        nameof(SqlServerFunctions.json_contains), nameof(SqlServerFunctions.json_path_exists)
    };

    /// <summary>
    /// The functions whose first argument is a date-part keyword. T-SQL's parser consumes it as
    /// grammar, not as a value, so it is emitted as a plain quoted token; the dialect then strips the
    /// surrounding quotes (<see cref="SqlServerFunctions.datename(string?, DateTime?)"/>,
    /// <see cref="SqlServerFunctions.date_bucket(string?, int, DateTime?)"/>).
    /// </summary>
    private static readonly HashSet<string> DatePartFunctions = new(StringComparer.Ordinal)
    {
        nameof(SqlServerFunctions.datename), nameof(SqlServerFunctions.date_bucket)
    };

    /// <summary>
    /// The sequence-expression functions whose single argument is a constant name: it selects the
    /// quoted identifier <c>NEXT VALUE FOR</c> consumes and must never be bound as a SQL parameter.
    /// </summary>
    private static readonly HashSet<string> SequenceFunctions = new(StringComparer.Ordinal)
    {
        nameof(SqlServerFunctions.next_value_for), nameof(SqlServerFunctions.nextval)
    };

    /// <summary>The maximum length of one SQL Server identifier component (<c>sysname</c>).</summary>
    private const int MaxIdentifierLength = 128;

    /// <summary>Translates a SQL Server-only scalar call; returns <c>false</c> when it is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(SqlServerFunctions) || !FunctionNames.Contains(node.Method.Name))
            return false;

        var name = node.Method.Name;

        if (visitor.Dialect.SqlServerFunctions is not { } functions || !functions.Supports(name))
            throw new NotSupportedException(
                visitor.Dialect.SqlServerFunctions?.GetUnsupportedReason(name)
                ?? $"The {name} function is not supported by this provider.");

        if (SequenceFunctions.Contains(name))
        {
            EmitSequence(visitor, name, node.Arguments);
            return true;
        }

        if (TryConversionFunctions.Contains(name))
        {
            EmitTryConversion(visitor, name, node);
            return true;
        }

        if (BitFunctions.Contains(name))
        {
            EmitBitFunction(visitor, functions, name, node);
            return true;
        }

        var isFormatMessage = name is nameof(SqlServerFunctions.formatmessage);
        var isVariadic = isFormatMessage || VariadicFunctions.Contains(name);
        if (isVariadic && ArgumentFlattener.IsRuntimeArray(node.Arguments, isFormatMessage ? 1 : 0))
            throw new NotSupportedException($"The {name} function does not support a runtime array argument; pass the values directly.");

        var args = isFormatMessage
            ? ArgumentFlattener.Flatten(node.Arguments, 1)
            : isVariadic
                ? ArgumentFlattener.Flatten(node.Arguments, 0)
                : node.Arguments;

        if (name is nameof(SqlServerFunctions.checksum) or nameof(SqlServerFunctions.binary_checksum)
            && args.Count == 0)
            throw new NotSupportedException($"The {name} function requires at least one argument.");

        if (name is nameof(SqlServerFunctions.formatmessage) && args.Count - 1 > MaxFormatMessageArgs)
            throw new NotSupportedException($"The {name} function accepts at most {MaxFormatMessageArgs} formatting arguments.");

        var precision = NormalizePrecision(name, args);

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
            // grammar token: a datepart/unit operand is a keyword the T-SQL parser consumes, not a
            // data value, so it keeps the historic plain quoted token the dialect unquotes (a value
            // spelling such as N'…' was left embedded and produced `datename(N'month', …)`).
            rendered[i] = i == 0 && DatePartFunctions.Contains(name)
                && TypeFacts.UnwrapConvert(args[i]) is ConstantExpression { Value: string datePart }
                    ? visitor.RenderStringLiteral(datePart, SqlStringLiteralRole.GrammarToken, SqlStringGrammar.Keyword)
                    : visitor.VisitToString(args[i]);
        }

        // A non-int integral precision (byte/short/…) would otherwise render with a boxing cast
        // (`cast(5 as int)`); T-SQL's `*FROMPARTS` wants a plain integer literal, so normalise it.
        if (precision is { } value)
            rendered[^1] = value.ToString(CultureInfo.InvariantCulture);

        var expression = functions.Render(name, rendered);

        if (BooleanFunctions.Contains(name))
            expression = visitor.Dialect.MakeBooleanPredicate($"{expression} = 1", visitor.IsPredicateContext);

        visitor.Builder!.Append(expression);
        return true;
    }

    /// <summary>
    /// Renders a closed-map conversion (<c>try_cast</c>/<c>try_convert</c>). The target CLR type is read
    /// from the method's generic argument (the string-target overloads are non-generic) and handed to the
    /// dialect hook, which owns the closed SQL type map: the string-only
    /// <see cref="ISqlServerFunctions.Render"/> can never see the target, and its open name fall-back is
    /// exactly the defect this route avoids. A supplied style is a separate overload, so an omitted style
    /// passes <see langword="null"/>; a supplied NULL style is emitted as a typed NULL and keeps the
    /// server's native NULL-style semantics.
    /// </summary>
    private static void EmitTryConversion(BaseExpressionVisitor visitor, string name, MethodCallExpression node)
    {
        var args = node.Arguments;
        var isConvert = name == nameof(SqlServerFunctions.try_convert);
        var expected = isConvert ? 2 : 1;

        if (args.Count is 0 || args.Count > expected)
            throw new NotSupportedException(
                isConvert
                    ? $"The {name} function accepts a value and an optional style."
                    : $"The {name} function accepts exactly one value.");

        var targetType = node.Method.IsGenericMethod
            ? node.Method.GetGenericArguments()[0]
            : typeof(string);

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var value = visitor.VisitToString(args[0]);

        string? style = null;
        if (isConvert && args.Count == 2)
        {
            style = visitor.VisitToString(args[1]);
            if (style == "null")
                style = "CAST(NULL AS int)";
        }

        var expression = isConvert
            ? visitor.Dialect.MakeTryConvert(value, targetType, style)
            : visitor.Dialect.MakeTryCast(value, targetType);

        visitor.Builder!.Append(expression);
    }

    /// <summary>
    /// Renders a bit-manipulation call. The operand's declared type (nullable-unwrapped) selects the
    /// closed <c>tinyint</c>/<c>smallint</c>/<c>int</c>/<c>bigint</c> rendering, because the SQL casts
    /// both the operand and the shift/offset and a NULL has to become a typed NULL of the result type
    /// (a bare <c>left_shift(null, …)</c> is Msg 8116). The renderer lives on the SQL Server functions so
    /// a provider without the family rejects it instead of emitting T-SQL.
    /// </summary>
    private static void EmitBitFunction(
        BaseExpressionVisitor visitor, ISqlServerFunctions functions, string name, MethodCallExpression node)
    {
        var args = node.Arguments;
        var operandType = BitOperandType(args[0].Type);

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var operand = visitor.VisitToString(args[0]);
        var amount = args.Count > 1 ? visitor.VisitToString(args[1]) : null;
        var value = args.Count > 2 ? visitor.VisitToString(args[2]) : null;

        visitor.Builder!.Append(functions.RenderBit(name, operand, operandType, amount, value));
    }

    /// <summary>The closed operand-type map of the bit family; any other operand type is rejected.</summary>
    private static string BitOperandType(Type type)
    {
        var operand = Nullable.GetUnderlyingType(type) ?? type;

        if (operand == typeof(byte))
            return "tinyint";
        if (operand == typeof(short))
            return "smallint";
        if (operand == typeof(int))
            return "int";
        if (operand == typeof(long))
            return "bigint";

        throw new NotSupportedException(
            $"The bit-manipulation operand type '{operand.Name}' has no SQL Server equivalent.");
    }

    /// <summary>
    /// Renders a SQL Server sequence expression (<c>NEXT VALUE FOR</c>). The single argument must be a
    /// compile-time string naming one object, written as one component (<c>[name]</c>) or two
    /// (<c>[schema].[name]</c>). Each component is bracket-quoted by the dialect (an embedded <c>]</c>
    /// is doubled) so raw name text can never escape the identifier; the constant is consumed here and
    /// is never emitted as a parameter (also in the parameter-extraction pass).
    /// </summary>
    private static void EmitSequence(BaseExpressionVisitor visitor, string name, IReadOnlyList<Expression> args)
    {
        if (args.Count != 1)
            throw new NotSupportedException($"The {name} function requires exactly one sequence name.");

        if (TypeFacts.UnwrapConvert(args[0]) is not ConstantExpression { Value: string raw })
            throw new NotSupportedException($"The {name} function requires a constant string sequence name.");

        var quoted = QuoteSequenceName(visitor, name, raw);

        if (visitor.IsParamMode)
            return;

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.SqlServerFunctions!.Render(name, [quoted]));
    }

    /// <summary>
    /// Splits a raw sequence name on <c>.</c> into one or two nonempty components and quotes each with
    /// the dialect identifier quoter, joining them again with <c>.</c>. Empty, NUL-containing,
    /// over-long (<c>&gt;128</c>) and more-than-two-component names are rejected before any SQL is
    /// emitted.
    /// </summary>
    private static string QuoteSequenceName(BaseExpressionVisitor visitor, string name, string raw)
    {
        if (raw.Length == 0)
            throw new NotSupportedException($"The {name} sequence name must not be empty.");

        if (raw.IndexOf('\0') >= 0)
            throw new NotSupportedException($"The {name} sequence name must not contain a NUL character.");

        var parts = raw.Split('.');

        var quoted = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length == 0)
                throw new NotSupportedException($"The {name} sequence name must not contain an empty component.");

            if (part.Length > MaxIdentifierLength)
                throw new NotSupportedException($"The {name} sequence name component must not exceed {MaxIdentifierLength} characters.");

            quoted[i] = visitor.Dialect.QuoteIdentifier(part);
        }

        if (parts.Length > 2)
            throw new NotSupportedException($"The {name} sequence name must have at most two components (schema.name).");

        return string.Join('.', quoted);
    }

    /// <summary>
    /// Validates the trailing precision argument of <c>timefromparts</c>/<c>datetime2fromparts</c>/
    /// <c>datetimeoffsetfromparts</c>: T-SQL requires a constant integer in 0..7, so a non-constant,
    /// null or out-of-range value is rejected before any SQL is emitted. Any integral constant type
    /// (<c>byte</c>/<c>short</c>/<c>long</c>/…) is decoded, not just <see cref="int"/>. Returns the
    /// decoded value for normalisation of the rendered literal, or <c>null</c> for other functions.
    /// </summary>
    private static long? NormalizePrecision(string name, IReadOnlyList<Expression> args)
    {
        if (!PrecisionFunctions.Contains(name))
            return null;

        if (TypeFacts.UnwrapConvert(args[^1]) is not ConstantExpression constant || !TryDecodeInt64(constant.Value, out var precision))
            throw new NotSupportedException($"The {name} precision must be a constant integer between 0 and 7.");

        if (precision is < 0 or > 7)
            throw new NotSupportedException($"The {name} precision must be between 0 and 7.");

        return precision;
    }

    /// <summary>Decodes an integral constant (of any width) to <see cref="long"/>; non-integers fail.</summary>
    private static bool TryDecodeInt64(object? value, out long result)
    {
        switch (value)
        {
            case int i: result = i; return true;
            case byte b: result = b; return true;
            case sbyte sb: result = sb; return true;
            case short s: result = s; return true;
            case ushort us: result = us; return true;
            case uint ui: result = ui; return true;
            case long l: result = l; return true;
            case ulong ul when ul <= long.MaxValue: result = (long)ul; return true;
            default: result = 0; return false;
        }
    }
}
