using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Translates the extended scalar function library of <see cref="NORM.NORM_SQL"/>: the additional
/// math functions (<c>asin</c>, <c>cbrt</c>, <c>degrees</c>, <c>pi</c>, <c>mod</c>, ...), the string
/// functions (<c>split_part</c>, <c>lpad</c>, <c>initcap</c>, ...), the POSIX regular-expression
/// functions, the date/time functions (<c>age</c>, <c>make_date</c>, <c>to_char</c>, <c>extract</c>,
/// ...) and <c>num_nulls</c>/<c>num_nonnulls</c>.
/// <para>
/// The whole surface is guarded by <see cref="ISqlDialect.SupportsExtendedScalarFunctions"/>
/// (PostgreSQL opts in today); a provider that does not support it fails with a clear message instead
/// of emitting invalid SQL.
/// </para>
/// </summary>
internal static class ExtendedScalarFunctionTranslator
{
    private static readonly HashSet<string> MathFunctions = new(StringComparer.Ordinal)
    {
        nameof(NORM.NORM_SQL.asin), nameof(NORM.NORM_SQL.acos), nameof(NORM.NORM_SQL.atan),
        nameof(NORM.NORM_SQL.atan2), nameof(NORM.NORM_SQL.cbrt), nameof(NORM.NORM_SQL.sinh),
        nameof(NORM.NORM_SQL.cosh), nameof(NORM.NORM_SQL.tanh), nameof(NORM.NORM_SQL.asinh),
        nameof(NORM.NORM_SQL.acosh), nameof(NORM.NORM_SQL.atanh), nameof(NORM.NORM_SQL.degrees),
        nameof(NORM.NORM_SQL.radians), nameof(NORM.NORM_SQL.pi), nameof(NORM.NORM_SQL.random),
        nameof(NORM.NORM_SQL.log), nameof(NORM.NORM_SQL.mod), nameof(NORM.NORM_SQL.gcd),
        nameof(NORM.NORM_SQL.lcm), nameof(NORM.NORM_SQL.factorial), nameof(NORM.NORM_SQL.width_bucket)
    };

    private static readonly HashSet<string> DirectFunctions = new(StringComparer.Ordinal)
    {
        nameof(NORM.NORM_SQL.split_part), nameof(NORM.NORM_SQL.strpos), nameof(NORM.NORM_SQL.left),
        nameof(NORM.NORM_SQL.right), nameof(NORM.NORM_SQL.lpad), nameof(NORM.NORM_SQL.rpad),
        nameof(NORM.NORM_SQL.repeat), nameof(NORM.NORM_SQL.reverse), nameof(NORM.NORM_SQL.initcap),
        nameof(NORM.NORM_SQL.translate), nameof(NORM.NORM_SQL.overlay), nameof(NORM.NORM_SQL.md5),
        nameof(NORM.NORM_SQL.regexp_replace), nameof(NORM.NORM_SQL.regexp_like),
        nameof(NORM.NORM_SQL.regexp_split_to_array), nameof(NORM.NORM_SQL.regexp_count),
        nameof(NORM.NORM_SQL.regexp_instr), nameof(NORM.NORM_SQL.age), nameof(NORM.NORM_SQL.date_bin),
        nameof(NORM.NORM_SQL.make_date), nameof(NORM.NORM_SQL.make_interval), nameof(NORM.NORM_SQL.justify_days),
        nameof(NORM.NORM_SQL.justify_hours), nameof(NORM.NORM_SQL.to_char), nameof(NORM.NORM_SQL.to_date),
        nameof(NORM.NORM_SQL.to_number), nameof(NORM.NORM_SQL.to_timestamp), nameof(NORM.NORM_SQL.timezone)
    };

    private static readonly HashSet<string> KeywordFunctions = new(StringComparer.Ordinal)
    {
        nameof(NORM.NORM_SQL.current_date), nameof(NORM.NORM_SQL.current_time),
        nameof(NORM.NORM_SQL.localtime), nameof(NORM.NORM_SQL.localtimestamp)
    };

    private static readonly HashSet<string> VariadicFunctions = new(StringComparer.Ordinal)
    {
        nameof(NORM.NORM_SQL.concat_ws), nameof(NORM.NORM_SQL.format),
        nameof(NORM.NORM_SQL.num_nulls), nameof(NORM.NORM_SQL.num_nonnulls)
    };

    private static readonly HashSet<string> ExtractFields = new(StringComparer.Ordinal)
    {
        "microseconds", "milliseconds", "second", "minute", "hour", "day", "week", "month",
        "quarter", "year", "decade", "century", "millennium", "epoch", "dow", "isodow", "doy",
        "isoyear", "timezone", "timezone_hour", "timezone_minute"
    };

    /// <summary>Translates an extended scalar call; returns <c>false</c> when it is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var name = node.Method.Name;

        if (MathFunctions.Contains(name))
        {
            RequireExtended(visitor);
            EmitMath(visitor, name, node.Arguments);
            return true;
        }

        if (KeywordFunctions.Contains(name))
        {
            RequireExtended(visitor);
            if (!visitor.IsParamMode)
            {
                visitor.NeedAliasForColumn = true;
                visitor.Builder!.Append(name);
            }

            return true;
        }

        if (VariadicFunctions.Contains(name))
        {
            RequireExtended(visitor);
            var leading = name is nameof(NORM.NORM_SQL.concat_ws) or nameof(NORM.NORM_SQL.format) ? 1 : 0;
            SqlOperandTranslator.EmitFunction(visitor, name, FlattenVariadic(node.Arguments, leading));
            return true;
        }

        if (name == nameof(NORM.NORM_SQL.extract))
        {
            RequireExtended(visitor);
            EmitExtract(visitor, node.Arguments);
            return true;
        }

        if (DirectFunctions.Contains(name))
        {
            RequireExtended(visitor);
            SqlOperandTranslator.EmitFunction(visitor, name, node.Arguments);
            return true;
        }

        return false;
    }

    private static void EmitMath(BaseExpressionVisitor visitor, string name, IReadOnlyList<Expression> args)
    {
        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var sqlArgs = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
            sqlArgs[i] = visitor.VisitToString(args[i]);

        visitor.Builder!.Append(visitor.Dialect.MakeMathFunction(name, sqlArgs));
    }

    /// <summary><c>extract(field from value)</c> with a validated constant field name.</summary>
    private static void EmitExtract(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (args.Count != 2)
            throw new NotSupportedException("extract requires a field name and a value.");

        if (!SqlLiteral.TryGetConstantString(args[0], out var field))
            throw new NotSupportedException("The extract field must be a constant string.");

        field = field.ToLowerInvariant();
        if (!ExtractFields.Contains(field))
            throw new NotSupportedException($"'{field}' is not a valid extract field.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[1]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeDatePart(field, visitor.VisitToString(args[1])));
    }

    /// <summary>
    /// Flattens a <c>params</c> argument array. The C# compiler wraps the arguments of a <c>params</c>
    /// call in a single <see cref="NewArrayExpression"/>; <paramref name="leading"/> fixed arguments
    /// are kept in front of the flattened values.
    /// </summary>
    private static IReadOnlyList<Expression> FlattenVariadic(IReadOnlyList<Expression> args, int leading)
    {
        if (leading < args.Count && args[leading] is NewArrayExpression { Expressions: var expressions })
        {
            if (leading == 0)
                return expressions;

            var items = new List<Expression>(leading + expressions.Count);
            for (var (i, cnt) = (0, leading); i < cnt; i++)
                items.Add(args[i]);

            items.AddRange(expressions);
            return items;
        }

        return args;
    }

    private static void RequireExtended(BaseExpressionVisitor visitor)
    {
        if (!visitor.Dialect.SupportsExtendedScalarFunctions)
            throw new NotSupportedException(
                "The extended scalar function library is not supported by this provider: these functions require PostgreSQL.");
    }
}
