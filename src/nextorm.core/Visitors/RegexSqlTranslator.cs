using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace NextORM.Core;

/// <summary>
/// Translates a <see cref="Regex"/> call with a constant pattern into the provider's native
/// regular-expression SQL. Both the static surface
/// (<c>Regex.IsMatch(value, pattern[, options])</c>/<c>Regex.Replace(value, pattern, replacement[, options])</c>)
/// and the instance surface (<c>regex.IsMatch(value)</c>/<c>regex.Replace(value, replacement)</c>) are
/// recognised; the instance pattern and options are read from the receiver, which may be a constant,
/// a <c>new Regex(...)</c> expression or a captured parameter-free local.
/// <para>
/// The pattern, the replacement and the options must be compile-time constants: a pattern that
/// depends on a column cannot be compiled by the CLR into the provider's own regex dialect and is
/// rejected instead of being silently mismatched. Only <see cref="RegexOptions.IgnoreCase"/> changes
/// the emitted SQL; <see cref="RegexOptions.Compiled"/> and <see cref="RegexOptions.CultureInvariant"/>
/// are accepted as no-ops, while the options with no portable SQL form are rejected.
/// </para>
/// </summary>
internal static class RegexSqlTranslator
{
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(Regex))
            return false;

        var methodName = node.Method.Name;

        if (methodName is not (nameof(Regex.IsMatch) or nameof(Regex.Replace)))
            return Reject(node, $"Regex.{methodName} is not supported; only Regex.IsMatch and Regex.Replace are translated.");

        var args = node.Arguments;
        Expression input;
        string pattern;
        string? replacement = null;
        RegexOptions options;

        if (node.Object is null)
        {
            if (methodName == nameof(Regex.IsMatch))
            {
                if (args.Count is < 2 or > 3 || args[0].Type != typeof(string) || args[1].Type != typeof(string))
                    return Reject(node, "This Regex.IsMatch overload is not supported.");

                input = args[0];

                if (!SqlLiteral.TryGetConstantString(args[1], out pattern))
                    return Reject(node, "The Regex pattern must be a compile-time constant.");

                options = RegexOptions.None;

                if (args.Count == 3 && !TryGetOptions(args[2], out options))
                    return Reject(node, "The RegexOptions argument must be a constant.");
            }
            else
            {
                if (args.Count is < 3 or > 4
                    || args[0].Type != typeof(string)
                    || args[1].Type != typeof(string)
                    || args[2].Type != typeof(string))
                    return Reject(node, "This Regex.Replace overload is not supported; the replacement must be a string.");

                input = args[0];

                if (!SqlLiteral.TryGetConstantString(args[1], out pattern))
                    return Reject(node, "The Regex pattern must be a compile-time constant.");

                if (!SqlLiteral.TryGetConstantString(args[2], out var constantReplacement))
                    return Reject(node, "The Regex replacement must be a compile-time constant.");

                replacement = constantReplacement;
                options = RegexOptions.None;

                if (args.Count == 4 && !TryGetOptions(args[3], out options))
                    return Reject(node, "The RegexOptions argument must be a constant.");
            }
        }
        else if (methodName == nameof(Regex.IsMatch))
        {
            if (args.Count != 1 || args[0].Type != typeof(string))
                return Reject(node, "This Regex.IsMatch overload is not supported.");

            input = args[0];

            if (!TryGetRegex(node.Object, out pattern, out options))
                return Reject(node, "The Regex instance must be a constant or a new Regex(...) with a constant pattern.");
        }
        else
        {
            if (args.Count != 2 || args[0].Type != typeof(string) || args[1].Type != typeof(string))
                return Reject(node, "This Regex.Replace overload is not supported.");

            input = args[0];

            if (!SqlLiteral.TryGetConstantString(args[1], out var constantReplacement))
                return Reject(node, "The Regex replacement must be a compile-time constant.");

            replacement = constantReplacement;

            if (!TryGetRegex(node.Object, out pattern, out options))
                return Reject(node, "The Regex instance must be a constant or a new Regex(...) with a constant pattern.");
        }

        var ignoreCase = ResolveOptions(options);

        if (!visitor.Dialect.SupportsRegex)
            throw new NotSupportedException("Regular expressions are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(input);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        var value = visitor.VisitToString(input);

        if (replacement is null)
        {
            var predicate = visitor.Dialect.MakeRegexMatch(value, pattern, ignoreCase);
            visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate(predicate, visitor.IsPredicateContext));
        }
        else
        {
            visitor.Builder!.Append(visitor.Dialect.MakeRegexReplace(value, pattern, replacement, ignoreCase));
        }

        return true;
    }

    /// <summary>
    /// Resolves the supported options to the single <c>IgnoreCase</c> flag the dialects need.
    /// <c>Compiled</c> and <c>CultureInvariant</c> do not change the SQL semantics and are ignored;
    /// every other option has no portable SQL form and is rejected.
    /// </summary>
    private static bool ResolveOptions(RegexOptions options)
    {
        var unsupported = options & ~(RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        if (unsupported != 0)
            throw new NotSupportedException($"RegexOptions.{unsupported} is not supported; only IgnoreCase has a portable SQL form.");

        return (options & RegexOptions.IgnoreCase) != 0;
    }

    private static bool TryGetOptions(Expression expression, out RegexOptions options)
    {
        if (expression is ConstantExpression { Value: RegexOptions value })
        {
            options = value;
            return true;
        }

        options = RegexOptions.None;
        return false;
    }

    /// <summary>
    /// Reads the pattern and options from an instance <see cref="Regex"/> receiver: a constant
    /// instance, a <c>new Regex(...)</c> constructor call, or a captured parameter-free local that is
    /// evaluated once at translation time.
    /// </summary>
    private static bool TryGetRegex(Expression receiver, out string pattern, out RegexOptions options)
    {
        if (receiver is ConstantExpression { Value: Regex constant })
        {
            pattern = constant.ToString();
            options = constant.Options;
            return true;
        }

        if (receiver is NewExpression { Constructor: { } constructor, Arguments: var constructorArgs }
            && constructor.DeclaringType == typeof(Regex)
            && constructorArgs.Count >= 1
            && SqlLiteral.TryGetConstantString(constructorArgs[0], out var constructorPattern))
        {
            pattern = constructorPattern;
            options = RegexOptions.None;

            if (constructorArgs.Count >= 2 && !TryGetOptions(constructorArgs[1], out options))
            {
                pattern = string.Empty;
                return false;
            }

            return true;
        }

        if (!receiver.Has<ParameterExpression>()
            && Expression.Lambda<Func<object?>>(Expression.Convert(receiver, typeof(object))).Compile()() is Regex captured)
        {
            pattern = captured.ToString();
            options = captured.Options;
            return true;
        }

        pattern = string.Empty;
        options = RegexOptions.None;
        return false;
    }

    /// <summary>
    /// Fails a translation that cannot be expressed. A call with no query parameter is left to the
    /// caller's constant folding; a call that depends on a column is a clear error.
    /// </summary>
    private static bool Reject(MethodCallExpression node, string message)
    {
        if (!node.Has<ParameterExpression>())
            return false;

        throw new NotSupportedException(message);
    }
}
