using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Shared rendering of SQL operands for the provider-specific translator helpers
/// (<see cref="ArraySqlTranslator"/>, <see cref="JsonSqlTranslator"/>).
/// <para>
/// An array operand is bound as a single parameter through <see cref="InValuesEvaluator"/> (the whole
/// array, never expanded into a value list); every other operand is rendered by the visitor. The
/// parameter-extraction pass (<see cref="BaseExpressionVisitor.IsParamMode"/>) walks the operands in
/// the same order as the SQL pass, so the computed parameters stay aligned.
/// </para>
/// </summary>
internal static class SqlOperandTranslator
{
    /// <summary>True when the operand is an array (after unwrapping a boxing/conversion node).</summary>
    internal static bool IsArray(Expression expression) => UnwrapConvert(expression).Type.IsArray;

    /// <summary>
    /// Renders one argument: an array becomes a single array parameter, anything else is emitted by
    /// the visitor (a column, a captured value or a nested SQL expression).
    /// </summary>
    internal static void AppendArgument(BaseExpressionVisitor visitor, Expression argument)
    {
        if (IsArray(argument))
        {
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
    /// Binds an array operand as one parameter. A runtime placeholder (<see cref="NORM.Param{T}(int)"/>)
    /// is rendered directly; any other array (a captured local/field, a constant or an inline
    /// <c>new[]</c>) is evaluated and stored as a computed parameter.
    /// </summary>
    internal static void AppendArrayOperand(BaseExpressionVisitor visitor, Expression arrayExp)
    {
        arrayExp = UnwrapConvert(arrayExp);

        if (arrayExp is MethodCallExpression { Method.DeclaringType: var declaring } paramCall
            && declaring == typeof(NORM))
        {
            visitor.Visit(paramCall);
            return;
        }

        var value = InValuesEvaluator.Evaluate(arrayExp, visitor.QueryProvider);
        var name = visitor.ParamProvider.GetParamName();
        visitor.Params.Add(new Param(name, value));

        if (!visitor.IsParamMode)
            visitor.Builder!.Append(visitor.Dialect.MakeParam(name));
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

    private static Expression UnwrapConvert(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            ? unary.Operand
            : expression;
}
