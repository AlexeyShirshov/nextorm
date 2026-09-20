using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the extended scalar function library of <see cref="CommonFunctions"/>: the additional
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
        nameof(PostgresFunctions.asin), nameof(PostgresFunctions.acos), nameof(PostgresFunctions.atan),
        nameof(PostgresFunctions.atan2), nameof(PostgresFunctions.cbrt), nameof(PostgresFunctions.sinh),
        nameof(PostgresFunctions.cosh), nameof(PostgresFunctions.tanh), nameof(PostgresFunctions.asinh),
        nameof(PostgresFunctions.acosh), nameof(PostgresFunctions.atanh), nameof(PostgresFunctions.degrees),
        nameof(PostgresFunctions.radians), nameof(PostgresFunctions.pi), nameof(PostgresFunctions.random),
        nameof(PostgresFunctions.log), nameof(PostgresFunctions.mod), nameof(PostgresFunctions.gcd),
        nameof(PostgresFunctions.lcm), nameof(PostgresFunctions.factorial), nameof(PostgresFunctions.width_bucket)
    };

    private static readonly HashSet<string> DirectFunctions = new(StringComparer.Ordinal)
    {
        nameof(PostgresFunctions.split_part), nameof(PostgresFunctions.strpos), nameof(PostgresFunctions.left),
        nameof(PostgresFunctions.right), nameof(PostgresFunctions.lpad), nameof(PostgresFunctions.rpad),
        nameof(PostgresFunctions.repeat), nameof(PostgresFunctions.reverse), nameof(PostgresFunctions.initcap),
        nameof(PostgresFunctions.translate), nameof(PostgresFunctions.overlay), nameof(PostgresFunctions.md5),
        nameof(PostgresFunctions.regexp_replace), nameof(PostgresFunctions.regexp_like),
        nameof(PostgresFunctions.regexp_split_to_array), nameof(PostgresFunctions.regexp_count),
        nameof(PostgresFunctions.regexp_instr), nameof(PostgresFunctions.make_interval), nameof(PostgresFunctions.justify_days),
        nameof(PostgresFunctions.justify_hours), nameof(PostgresFunctions.to_char), nameof(PostgresFunctions.to_date),
        nameof(PostgresFunctions.to_number), nameof(PostgresFunctions.to_timestamp), nameof(PostgresFunctions.timezone)
    };

    private static readonly HashSet<string> KeywordFunctions = new(StringComparer.Ordinal)
    {
        nameof(PostgresFunctions.current_date), nameof(PostgresFunctions.current_time),
        nameof(PostgresFunctions.localtime), nameof(PostgresFunctions.localtimestamp)
    };

    private static readonly HashSet<string> VariadicFunctions = new(StringComparer.Ordinal)
    {
        nameof(PostgresFunctions.concat_ws), nameof(PostgresFunctions.format),
        nameof(PostgresFunctions.num_nulls), nameof(PostgresFunctions.num_nonnulls)
    };

    private static readonly HashSet<string> TextSearchFunctions = new(StringComparer.Ordinal)
    {
        nameof(PostgresFunctions.to_tsvector), nameof(PostgresFunctions.to_tsquery),
        nameof(PostgresFunctions.plainto_tsquery), nameof(PostgresFunctions.phraseto_tsquery),
        nameof(PostgresFunctions.websearch_to_tsquery), nameof(PostgresFunctions.ts_rank),
        nameof(PostgresFunctions.ts_headline)
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

        if (name == nameof(PostgresFunctions.pg_typeof))
        {
            RequireExtended(visitor);
            EmitPgTypeOf(visitor, node.Arguments);
            return true;
        }

        if (VariadicFunctions.Contains(name))
        {
            RequireExtended(visitor);
            var leading = name is nameof(PostgresFunctions.concat_ws) or nameof(PostgresFunctions.format) ? 1 : 0;
            SqlOperandTranslator.EmitFunction(visitor, name, FlattenVariadic(node.Arguments, leading));
            return true;
        }

        if (DirectFunctions.Contains(name))
        {
            RequireExtended(visitor);
            SqlOperandTranslator.EmitFunction(visitor, name, node.Arguments);
            return true;
        }

        if (TextSearchFunctions.Contains(name))
        {
            RequireTextSearch(visitor);
            SqlOperandTranslator.EmitFunction(visitor, name, node.Arguments);
            return true;
        }

        if (name == nameof(PostgresFunctions.ts_match))
        {
            RequireTextSearch(visitor);
            SqlOperandTranslator.EmitOperator(visitor, "@@", node.Arguments[0], node.Arguments[1]);
            return true;
        }

        if (name == nameof(PostgresFunctions.setseed))
        {
            if (!visitor.Dialect.SupportsRandomSeed)
                throw new NotSupportedException("The random seed function (setseed) requires PostgreSQL.");

            SqlOperandTranslator.EmitFunction(visitor, "setseed", node.Arguments);
            return true;
        }

        return false;
    }

    /// <summary>
    /// <c>pg_typeof(value)</c> returns the <c>regtype</c> OID type, which the driver cannot read as a
    /// string, so it is cast to the dialect's text type.
    /// </summary>
    private static void EmitPgTypeOf(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append("cast(pg_typeof(").Append(visitor.VisitToString(args[0]))
            .Append(") as ").Append(visitor.Dialect.MakeTypeName(typeof(string))).Append(')');
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

    private static void RequireTextSearch(BaseExpressionVisitor visitor)
    {
        if (!visitor.Dialect.SupportsTextSearchFunctions)
            throw new NotSupportedException(
                "The native text-search functions (to_tsvector/to_tsquery/ts_rank/ts_headline/@@) require PostgreSQL.");
    }
}
