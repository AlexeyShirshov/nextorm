using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the extended scalar function library of <see cref="CommonFunctions"/>: the additional
/// math functions (<c>asin</c>, <c>cbrt</c>, <c>degrees</c>, <c>pi</c>, <c>mod</c>, ...), the string
/// functions (<c>split_part</c>, <c>lpad</c>, <c>initcap</c>, ...), the POSIX regular-expression
/// functions (<c>regexp_replace</c>/<c>regexp_like</c>/<c>regexp_substr</c>/...), the date/time
/// functions (<c>age</c>, <c>make_time</c>/<c>make_timestamp</c>, <c>date_bin</c>, <c>to_char</c>,
/// <c>extract</c>, ...), the cryptographic hashes
/// (<c>digest</c>/<c>sha224</c>/<c>sha256</c>/<c>sha384</c>/<c>sha512</c>), the runtime settings
/// (<c>current_setting</c>/<c>set_config</c>), the sequence functions
/// (<c>nextval</c>/<c>setval</c>/<c>currval</c>/<c>lastval</c>) and <c>num_nulls</c>/<c>num_nonnulls</c>.
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
        nameof(PostgresFunctions.log), nameof(PostgresFunctions.gcd),
        nameof(PostgresFunctions.lcm), nameof(PostgresFunctions.factorial), nameof(PostgresFunctions.width_bucket)
    };

    private static readonly HashSet<string> DirectFunctions = new(StringComparer.Ordinal)
    {
        nameof(PostgresFunctions.split_part), nameof(PostgresFunctions.strpos),
        nameof(PostgresFunctions.initcap),
        nameof(PostgresFunctions.overlay), nameof(PostgresFunctions.md5),
        nameof(PostgresFunctions.regexp_replace), nameof(PostgresFunctions.regexp_like),
        nameof(PostgresFunctions.regexp_split_to_array), nameof(PostgresFunctions.regexp_count),
        nameof(PostgresFunctions.regexp_instr), nameof(PostgresFunctions.regexp_substr),
        nameof(PostgresFunctions.justify_days),
        nameof(PostgresFunctions.justify_hours), nameof(PostgresFunctions.to_char), nameof(PostgresFunctions.to_date),
        nameof(PostgresFunctions.to_number), nameof(PostgresFunctions.to_timestamp), nameof(PostgresFunctions.timezone),
        nameof(PostgresFunctions.make_time), nameof(PostgresFunctions.make_timestamp),
        nameof(PostgresFunctions.age), nameof(PostgresFunctions.current_setting),
        nameof(PostgresFunctions.set_config)
    };

    private static readonly HashSet<string> KeywordFunctions = new(StringComparer.Ordinal)
    {
        nameof(PostgresFunctions.current_date), nameof(PostgresFunctions.current_time),
        nameof(PostgresFunctions.localtime), nameof(PostgresFunctions.localtimestamp)
    };

    private static readonly HashSet<string> VariadicFunctions = new(StringComparer.Ordinal)
    {
        nameof(PostgresFunctions.format),
        nameof(PostgresFunctions.num_nulls), nameof(PostgresFunctions.num_nonnulls)
    };

    private static readonly HashSet<string> TextSearchFunctions = new(StringComparer.Ordinal)
    {
        nameof(PostgresFunctions.to_tsvector), nameof(PostgresFunctions.to_tsquery),
        nameof(PostgresFunctions.plainto_tsquery), nameof(PostgresFunctions.phraseto_tsquery),
        nameof(PostgresFunctions.websearch_to_tsquery), nameof(PostgresFunctions.ts_rank),
        nameof(PostgresFunctions.ts_rank_cd), nameof(PostgresFunctions.ts_headline)
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
            SqlOperandTranslator.EmitFunction(visitor, name, ArgumentFlattener.Flatten(node.Arguments, leading));
            return true;
        }

        if (name == nameof(PostgresFunctions.make_interval))
        {
            RequireExtended(visitor);
            EmitMakeInterval(visitor, node.Arguments);
            return true;
        }

        if (name == nameof(PostgresFunctions.date_bin))
        {
            RequireExtended(visitor);
            EmitCastArgument(visitor, "date_bin", node.Arguments, 0, "interval");
            return true;
        }

        if (name is nameof(PostgresFunctions.nextval) or nameof(PostgresFunctions.setval)
            or nameof(PostgresFunctions.currval))
        {
            RequireExtended(visitor);
            EmitCastArgument(visitor, name, node.Arguments, 0, "regclass");
            return true;
        }

        if (name == nameof(PostgresFunctions.lastval))
        {
            RequireExtended(visitor);
            SqlOperandTranslator.EmitFunction(visitor, "lastval", node.Arguments);
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

        if (name is nameof(PostgresFunctions.digest) or nameof(PostgresFunctions.sha256)
            or nameof(PostgresFunctions.sha224) or nameof(PostgresFunctions.sha384)
            or nameof(PostgresFunctions.sha512))
        {
            if (!visitor.Dialect.SupportsCryptoFunctions)
                throw new NotSupportedException("The cryptographic hash functions (digest/sha256/sha224/sha384/sha512) require PostgreSQL.");

            EmitCrypto(visitor, name, node.Arguments);
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
        visitor.Builder!.Append(visitor.Kw("cast(pg_typeof(")).Append(visitor.VisitToString(args[0]))
            .Append(visitor.Kw(") as ")).Append(visitor.Dialect.MakeTypeName(typeof(string))).Append(')');
    }

    /// <summary>
    /// Renders <c>make_interval(years, months, weeks, days, hours, mins, secs)</c>. The C# surface has no
    /// <c>weeks</c> parameter, so a literal <c>0</c> is inserted in its position; otherwise the remaining
    /// arguments would shift and denote different units.
    /// </summary>
    private static void EmitMakeInterval(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        builder.Append("make_interval(");

        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            if (i == 2)
                builder.Append(", 0");

            if (i > 0)
                builder.Append(", ");

            builder.Append(visitor.VisitToString(args[i]));
        }

        builder.Append(')');
    }

    /// <summary>
    /// Renders <c>digest(data, type)</c>/<c>sha256(data)</c>. The arguments are rendered directly
    /// rather than through <see cref="SqlOperandTranslator.EmitFunction"/> because a <c>byte[]</c>
    /// argument would otherwise be taken for an array operand and materialised as a single parameter.
    /// </summary>
    private static void EmitCrypto(BaseExpressionVisitor visitor, string name, IReadOnlyList<Expression> args)
    {
        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        builder.Append(name).Append('(');

        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append(visitor.VisitToString(args[i]));
        }

        builder.Append(')');
    }

    /// <summary>
    /// Renders <c>name(arg, ...)</c> with one argument wrapped in <c>cast(arg as type)</c>. Used for
    /// the sequence functions (<c>regclass</c>) and <c>date_bin</c> (<c>interval</c>), whose text
    /// operands are not implicitly coerced by PostgreSQL.
    /// </summary>
    private static void EmitCastArgument(BaseExpressionVisitor visitor, string name, IReadOnlyList<Expression> args, int index, string castType)
    {
        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        builder.Append(name).Append('(');

        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            if (i > 0) builder.Append(", ");

            if (i == index)
            {
                builder.Append(visitor.Kw("cast("));
                SqlOperandTranslator.AppendArgument(visitor, args[i]);
                builder.Append(visitor.Kw(" as ")).Append(castType).Append(')');
            }
            else
            {
                SqlOperandTranslator.AppendArgument(visitor, args[i]);
            }
        }

        builder.Append(')');
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
