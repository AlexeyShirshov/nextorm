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
        nameof(NORM.PG.asin), nameof(NORM.PG.acos), nameof(NORM.PG.atan),
        nameof(NORM.PG.atan2), nameof(NORM.PG.cbrt), nameof(NORM.PG.sinh),
        nameof(NORM.PG.cosh), nameof(NORM.PG.tanh), nameof(NORM.PG.asinh),
        nameof(NORM.PG.acosh), nameof(NORM.PG.atanh), nameof(NORM.PG.degrees),
        nameof(NORM.PG.radians), nameof(NORM.PG.pi), nameof(NORM.PG.random),
        nameof(NORM.PG.log), nameof(NORM.PG.mod), nameof(NORM.PG.gcd),
        nameof(NORM.PG.lcm), nameof(NORM.PG.factorial), nameof(NORM.PG.width_bucket)
    };

    private static readonly HashSet<string> DirectFunctions = new(StringComparer.Ordinal)
    {
        nameof(NORM.PG.split_part), nameof(NORM.PG.strpos), nameof(NORM.PG.left),
        nameof(NORM.PG.right), nameof(NORM.PG.lpad), nameof(NORM.PG.rpad),
        nameof(NORM.PG.repeat), nameof(NORM.PG.reverse), nameof(NORM.PG.initcap),
        nameof(NORM.PG.translate), nameof(NORM.PG.overlay), nameof(NORM.PG.md5),
        nameof(NORM.PG.regexp_replace), nameof(NORM.PG.regexp_like),
        nameof(NORM.PG.regexp_split_to_array), nameof(NORM.PG.regexp_count),
        nameof(NORM.PG.regexp_instr), nameof(NORM.PG.make_interval), nameof(NORM.PG.justify_days),
        nameof(NORM.PG.justify_hours), nameof(NORM.PG.to_char), nameof(NORM.PG.to_date),
        nameof(NORM.PG.to_number), nameof(NORM.PG.to_timestamp), nameof(NORM.PG.timezone)
    };

    private static readonly HashSet<string> KeywordFunctions = new(StringComparer.Ordinal)
    {
        nameof(NORM.PG.current_date), nameof(NORM.PG.current_time),
        nameof(NORM.PG.localtime), nameof(NORM.PG.localtimestamp)
    };

    private static readonly HashSet<string> VariadicFunctions = new(StringComparer.Ordinal)
    {
        nameof(NORM.PG.concat_ws), nameof(NORM.PG.format),
        nameof(NORM.PG.num_nulls), nameof(NORM.PG.num_nonnulls)
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
            var leading = name is nameof(NORM.PG.concat_ws) or nameof(NORM.PG.format) ? 1 : 0;
            SqlOperandTranslator.EmitFunction(visitor, name, FlattenVariadic(node.Arguments, leading));
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
