using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Shared rendering of SQL operands for the provider-specific translator helpers
/// (<see cref="ArraySqlTranslator"/>, <see cref="JsonSqlTranslator"/>).
/// <para>
/// An array operand is bound as a single parameter through <see cref="InValuesEvaluator"/> (the whole
/// array, never expanded into a value list); every other operand is rendered by the visitor. For the
/// array <em>functions</em>, <see cref="AppendArrayOrColumn"/> renders an array column or other SQL
/// expression instead of parameterising it. The
/// parameter-extraction pass (<see cref="BaseExpressionVisitor.IsParamMode"/>) walks the operands in
/// the same order as the SQL pass, so the computed parameters stay aligned.
/// </para>
/// </summary>
internal static class SqlOperandTranslator
{
    /// <summary>True when the operand is an array (after unwrapping a boxing/conversion node).</summary>
    internal static bool IsArray(Expression expression) => TypeFacts.UnwrapConvert(expression).Type.IsArray;

    /// <summary>
    /// Renders one argument: an array becomes a single array parameter, anything else is emitted by
    /// the visitor (a column, a captured value or a nested SQL expression).
    /// </summary>
    internal static void AppendArgument(BaseExpressionVisitor visitor, Expression argument)
    {
        if (IsArray(argument))
        {
            // A multirange column (an array of Range<T>) is an SQL value, not an array parameter, so it
            // is rendered; only a captured/inline Range<T>[] is bound as one multirange parameter.
            if (RangeTypeFacts.IsRangeArray(argument) && !IsCapturedArray(argument))
            {
                if (visitor.IsParamMode)
                    visitor.Visit(argument);
                else
                    visitor.Builder!.Append(visitor.VisitToString(argument));

                return;
            }

            AppendArrayOperand(visitor, argument);
            return;
        }

        if (visitor.IsParamMode)
        {
            visitor.Visit(argument);
            return;
        }

        visitor.Builder!.Append(visitor.VisitToString(argument));
    }

    /// <summary>
    /// Binds an array operand as one parameter. A runtime placeholder (<see cref="SqlFunctions.Parameter{T}(int)"/>)
    /// is rendered directly; any other array (a captured local/field, a constant or an inline
    /// <c>new[]</c>) is evaluated and stored as a computed parameter.
    /// </summary>
    internal static void AppendArrayOperand(BaseExpressionVisitor visitor, Expression arrayExp)
    {
        arrayExp = TypeFacts.UnwrapConvert(arrayExp);

        if (arrayExp is MethodCallExpression { Method.DeclaringType: var declaring } paramCall
            && declaring == typeof(SqlFunctions))
        {
            visitor.Visit(paramCall);
            return;
        }

        // string.Split(...) is an array operand produced by SQL (string_to_array), not a captured
        // array, so it is rendered instead of being materialised as a single parameter.
        if (arrayExp is MethodCallExpression { Object: { } splitObject } splitCall
            && splitCall.Method.DeclaringType == typeof(string)
            && splitCall.Method.Name == nameof(string.Split)
            && TryGetSplitSeparator(splitCall, out var splitSeparator))
        {
            AppendSplitOperand(visitor, splitObject, splitSeparator);
            return;
        }

        var value = InValuesEvaluator.Evaluate(arrayExp, visitor.QueryProvider);
        var name = visitor.ParameterProvider.GetParamName();
        visitor.Params.Add(new Parameter(name, value));

        if (!visitor.IsParamMode)
            visitor.Builder!.Append(visitor.Dialect.MakeParam(name));
    }

    /// <summary>
    /// Reads the single separator of a supported <c>string.Split</c> overload. The
    /// <c>StringSplitOptions</c> argument, when present, must be the default <c>None</c>; the
    /// multi-separator and <c>RemoveEmptyEntries</c>/<c>TrimEntries</c> forms have no portable SQL.
    /// </summary>
    private static bool TryGetSplitSeparator(MethodCallExpression call, out Expression separator)
    {
        separator = null!;

        var args = call.Arguments;
        if (args.Count is < 1 or > 2)
            return false;

        if (args.Count == 2
            && args[1] is ConstantExpression { Value: StringSplitOptions options }
            && options != StringSplitOptions.None)
            return false;

        var candidate = args[0];
        if (candidate is NewArrayExpression { Expressions: [var element] })
            candidate = element;

        if (candidate.Type != typeof(char) && candidate.Type != typeof(string))
            return false;

        separator = candidate;
        return true;
    }

    /// <summary>
    /// Renders a <c>string.Split(separator)</c> array operand as <c>string_to_array(value, separator)</c>.
    /// Requires a provider with native arrays (PostgreSQL); only the single-separator overloads map.
    /// </summary>
    private static void AppendSplitOperand(BaseExpressionVisitor visitor, Expression valueExp, Expression separatorArg)
    {
        if (!visitor.Dialect.SupportsArrays)
            throw new NotSupportedException("string.Split as an array operand requires a provider with native arrays (PostgreSQL).");

        if (separatorArg.Type != typeof(char) && separatorArg.Type != typeof(string))
            throw new NotSupportedException("This string.Split overload is not supported; only a single char/string separator maps to SQL.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(valueExp);
            visitor.Visit(separatorArg);
            return;
        }

        // Render the value before the separator so the parameter-extraction order matches.
        visitor.NeedAliasForColumn = true;
        var value = visitor.VisitToString(valueExp);
        string separator;
        if (separatorArg.Type == typeof(char))
        {
            if (!SqlLiteral.TryGetConstantString(separatorArg, out var separatorChar))
                throw new NotSupportedException("The string.Split separator character must be a constant.");

            separator = SqlLiteral.ToSqlStringLiteral(separatorChar);
        }
        else
        {
            separator = visitor.VisitToString(separatorArg);
        }

        visitor.Builder!.Append("string_to_array(").Append(value).Append(", ").Append(separator).Append(')');
    }

    /// <summary>
    /// Renders an argument for an array <em>function</em>: an array column or other SQL expression is
    /// emitted as SQL, while a captured/constant/inline array is still bound as a single parameter.
    /// </summary>
    internal static void AppendArrayOrColumn(BaseExpressionVisitor visitor, Expression argument)
    {
        if (IsArray(argument) && !IsCapturedArray(argument))
        {
            if (visitor.IsParamMode)
                visitor.Visit(argument);
            else
                visitor.Builder!.Append(visitor.VisitToString(argument));

            return;
        }

        AppendArgument(visitor, argument);
    }

    /// <summary>
    /// True when an array expression is a captured/constant value (a closure field/local, an embedded
    /// constant or an inline <c>new[]</c>) that should be bound as one parameter, rather than an entity
    /// member (array column) or a SQL-producing expression that must be rendered.
    /// </summary>
    internal static bool IsCapturedArray(Expression expression)
    {
        expression = TypeFacts.UnwrapConvert(expression);

        switch (expression)
        {
            case ConstantExpression:
            case NewArrayExpression:
                return true;
            case MethodCallExpression { Method.DeclaringType: var declaring } when declaring == typeof(SqlFunctions):
                // SqlFunctions.Parameter<T>(idx) is a runtime placeholder, not a captured array.
                return false;
        }

        var root = expression;
        while (root is MemberExpression member)
            root = member.Expression!;

        // A captured local/field is a member chain rooted at the closure constant; a static field has
        // no receiver. An entity member is rooted at the query parameter, so it is a column.
        return root is null or ConstantExpression;
    }

    /// <summary>Renders <c>name(arg, ...)</c> over the given arguments.</summary>
    internal static void EmitFunction(BaseExpressionVisitor visitor, string sqlName, IReadOnlyList<Expression> args)
    {
        if (!visitor.IsParamMode)
        {
            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(sqlName).Append('(');
        }

        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            if (!visitor.IsParamMode && i > 0)
                visitor.Builder!.Append(", ");

            AppendArgument(visitor, args[i]);
        }

        if (!visitor.IsParamMode)
            visitor.Builder!.Append(')');
    }

    /// <summary>Renders the infix <paramref name="sqlOperator"/> over two operands, parenthesised.</summary>
    internal static void EmitOperator(BaseExpressionVisitor visitor, string sqlOperator, Expression left, Expression right)
    {
        if (!visitor.IsParamMode)
        {
            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append('(');
        }

        AppendArgument(visitor, left);

        if (!visitor.IsParamMode)
            visitor.Builder!.Append(' ').Append(sqlOperator).Append(' ');

        AppendArgument(visitor, right);

        if (!visitor.IsParamMode)
            visitor.Builder!.Append(')');
    }

}
