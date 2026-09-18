using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Renders boolean/predicate expressions into SQL: comparison, logical and bitwise operators,
/// CASE (<c>?:</c>) and <c>switch</c>, and the value-to-predicate conversion required by dialects
/// without a boolean type. Extracted from <see cref="BaseExpressionVisitor"/>; the visitor walk and
/// the emitted SQL are unchanged.
/// </summary>
internal static class PredicateTranslator
{
    internal static Expression? VisitUnary(BaseExpressionVisitor visitor, UnaryExpression node)
    {
        // Numeric conversions are otherwise dropped, which silently changes the SQL semantics
        // (integer division, SQL Server integer avg, a projection wider than the column type).
        if (!visitor.IsParamMode
            && node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked
            && TypeFacts.TryGetNumericConversion(node.Operand.Type, node.Type, out var target))
        {
            visitor.NeedAliasForColumn = true;

            visitor.Builder!.Append("cast(");
            visitor.Visit(node.Operand);
            visitor.Builder!.Append(" as ").Append(visitor.Dialect.MakeTypeName(target)).Append(')');

            return node;
        }

        // The C# bool negation and the integral ones-complement both use ExpressionType.Not, so the
        // result type decides between the logical and the bitwise SQL operator.
        if (node.NodeType == ExpressionType.Not && !TypeFacts.IsBoolean(node.Type))
            return VisitOnesComplement(visitor, node);

        switch (node.NodeType)
        {
            case ExpressionType.Not:
                return VisitNot(visitor, node);
            case ExpressionType.OnesComplement:
                return VisitOnesComplement(visitor, node);
            case ExpressionType.Negate:
            case ExpressionType.NegateChecked:
                return VisitUnaryOperator(visitor, node, "-");
            case ExpressionType.UnaryPlus:
                return VisitUnaryOperator(visitor, node, "+");
        }

        return null;
    }

    /// <summary>
    /// Translates a logical NOT. The operand is a boolean expression, so it is rendered with
    /// predicate semantics and the whole negation is handed to the dialect: a provider without a
    /// boolean type (SQL Server) has to turn a projected predicate into a bit scalar.
    /// </summary>
    private static Expression VisitNot(BaseExpressionVisitor visitor, UnaryExpression node)
    {
        if (visitor.IsParamMode)
        {
            // Nothing is emitted, but the operand is still walked so captured constants are collected.
            visitor.Visit(node.Operand);
            return node;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate($"not ({RenderPredicate(visitor, node.Operand)})", visitor.IsPredicateContext));

        return node;
    }

    /// <summary>Translates the integer ones-complement operator (<c>~</c>).</summary>
    private static Expression VisitOnesComplement(BaseExpressionVisitor visitor, UnaryExpression node) => VisitUnaryOperator(visitor, node, "~");

    /// <summary>
    /// Translates a unary arithmetic/bitwise operator. The operand is parenthesised so that the
    /// grouping of the source expression is preserved (e.g. <c>-(a + b)</c> stays a single operand).
    /// </summary>
    private static Expression VisitUnaryOperator(BaseExpressionVisitor visitor, UnaryExpression node, string sqlOperator)
    {
        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Operand);
            return node;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(sqlOperator).Append('(');
        visitor.Visit(node.Operand);
        visitor.Builder!.Append(')');

        return node;
    }

    /// <summary>
    /// Renders a boolean expression with predicate semantics (the context of a WHERE clause) into a
    /// fresh builder, so that the test of a conditional/switch uses the dialect's predicate form
    /// (e.g. EXISTS/ANY/ALL) instead of a scalar one.
    /// </summary>
    internal static string RenderPredicate(BaseExpressionVisitor visitor, Expression expression)
    {
        // A bare boolean value (a bit column, a CASE, a method call) is not a predicate on a dialect
        // without a boolean type, so it is rendered as a value and the dialect turns it into one.
        if (!visitor.IsParamMode && TypeFacts.IsBoolean(expression.Type) && !TypeFacts.IsPredicate(expression))
        {
            using var valueVisitor = visitor.Clone();
            valueVisitor.Visit(expression);

            return visitor.Dialect.MakeBooleanValuePredicate(valueVisitor.ToString());
        }

        using var whereVisitor = new WhereExpressionVisitor(visitor.EntityType, visitor.Dialect, visitor.ColumnsProvider, visitor.Dim, visitor.AliasProvider, visitor.ParamProvider, visitor.QueryProvider, visitor.IsParamMode, visitor.Params, visitor.Logger);
        whereVisitor.Visit(expression);

        return whereVisitor.ToString();
    }

    /// <summary>
    /// Renders a boolean expression that is used as a condition (a logical operand such as the left
    /// and right side of <c>&amp;&amp;</c>/<c>||</c>). A value that is not itself a predicate is
    /// turned into one by the dialect, so a bare boolean value stays valid on providers without a
    /// boolean type.
    /// </summary>
    private static void AppendCondition(BaseExpressionVisitor visitor, Expression expression)
    {
        if (!visitor.IsParamMode && TypeFacts.IsBoolean(expression.Type) && !TypeFacts.IsPredicate(expression))
        {
            using var valueVisitor = visitor.Clone();
            valueVisitor.Visit(expression);
            visitor.Builder!.Append(visitor.Dialect.MakeBooleanValuePredicate(valueVisitor.ToString()));
            return;
        }

        visitor.Visit(expression);
    }

    /// <summary>
    /// Renders a whole condition (WHERE/HAVING/JOIN ON) into this visitor's builder. A condition
    /// that is a bare boolean value is turned into a predicate by the dialect.
    /// </summary>
    internal static void VisitCondition(BaseExpressionVisitor visitor, Expression condition)
    {
        // The root condition arrives as a lambda; the parameter is resolved by the member visitor,
        // so the body can be rendered directly (with the value-to-predicate conversion applied).
        if (condition is LambdaExpression lambda)
            condition = lambda.Body;

        AppendCondition(visitor, condition);
    }

    internal static Expression VisitConditional(BaseExpressionVisitor visitor, ConditionalExpression node)
    {
        // A CASE is a computed column and has to be aliased when it appears in a select list.
        visitor.NeedAliasForColumn = true;

        if (visitor.IsParamMode)
        {
            // The parameter-extraction pass emits no SQL, but the whole tree still has to be walked
            // so that captured constants/parameters inside the test and the branches are collected.
            visitor.Visit(node.Test);
            visitor.Visit(node.IfTrue);
            visitor.Visit(node.IfFalse);
            return node;
        }

        // The test is a condition, so it is rendered as a predicate (the branches are scalars).
        var test = RenderPredicate(visitor, node.Test);

        using var trueVisitor = visitor.Clone();
        trueVisitor.Visit(node.IfTrue);

        using var falseVisitor = visitor.Clone();
        falseVisitor.Visit(node.IfFalse);

        var caseBuilder = visitor.BuilderPool.Get();
        try
        {
            caseBuilder.Append("case when ").Append(test)
                .Append(" then ").Append(trueVisitor.ToString())
                .Append(" else ").Append(falseVisitor.ToString())
                .Append(" end");

            visitor.Builder!.Append(visitor.Dialect.MakeCase(caseBuilder.ToString(), TypeFacts.IsBoolean(node.Type), visitor.IsPredicateContext));
        }
        finally
        {
            visitor.BuilderPool.Return(caseBuilder);
        }

        return node;
    }

    internal static Expression VisitSwitch(BaseExpressionVisitor visitor, SwitchExpression node)
    {
        // A custom comparison method is not equality and cannot be translated without translating
        // the method itself; the C# compiler never produces one for a switch over constants.
        if (node.Comparison is not null)
            throw new NotSupportedException("A switch expression with a custom comparison method is not supported");

        visitor.NeedAliasForColumn = true;

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.SwitchValue);

            for (var (i, cnt) = (0, node.Cases.Count); i < cnt; i++)
            {
                var @case = node.Cases[i];
                for (var (j, tvCnt) = (0, @case.TestValues.Count); j < tvCnt; j++)
                    visitor.Visit(@case.TestValues[j]);

                visitor.Visit(@case.Body);
            }

            if (node.DefaultBody is not null)
                visitor.Visit(node.DefaultBody);

            return node;
        }

        if (node.Cases.Count == 0)
        {
            // A switch without cases is just its default arm.
            if (node.DefaultBody is null)
                visitor.Builder!.Append("null");
            else
                visitor.Visit(node.DefaultBody);

            return node;
        }

        var caseBuilder = visitor.BuilderPool.Get();
        var switchValueVisitor = visitor.Clone();
        try
        {
            switchValueVisitor.Visit(node.SwitchValue);
            var switchValue = switchValueVisitor.ToString();

            caseBuilder.Append("case");

            for (var (i, cnt) = (0, node.Cases.Count); i < cnt; i++)
            {
                var @case = node.Cases[i];

                using var bodyVisitor = visitor.Clone();
                bodyVisitor.Visit(@case.Body);
                var body = bodyVisitor.ToString();

                // Several test values share one body (case 1: case 2:), so the body is rendered once.
                for (var (j, tvCnt) = (0, @case.TestValues.Count); j < tvCnt; j++)
                {
                    using var testVisitor = visitor.Clone();
                    testVisitor.Visit(@case.TestValues[j]);

                    caseBuilder.Append(" when ").Append(switchValue)
                        .Append(" = ").Append(testVisitor.ToString())
                        .Append(" then ").Append(body);
                }
            }

            caseBuilder.Append(" else ");

            if (node.DefaultBody is null)
                caseBuilder.Append("null");
            else
            {
                using var defaultVisitor = visitor.Clone();
                defaultVisitor.Visit(node.DefaultBody);
                caseBuilder.Append(defaultVisitor.ToString());
            }

            caseBuilder.Append(" end");

            visitor.Builder!.Append(visitor.Dialect.MakeCase(caseBuilder.ToString(), TypeFacts.IsBoolean(node.Type), visitor.IsPredicateContext));
        }
        finally
        {
            switchValueVisitor.Dispose();
            visitor.BuilderPool.Return(caseBuilder);
        }

        return node;
    }

    internal static Expression VisitBinary(BaseExpressionVisitor visitor, BinaryExpression node)
    {
        visitor.NeedAliasForColumn = true;

        switch (node.NodeType)
        {
            case ExpressionType.Coalesce:
                if (!visitor.IsParamMode)
                {
                    using var leftVisitor = visitor.Clone();
                    leftVisitor.Visit(node.Left);

                    using var rightVisitor = visitor.Clone();
                    rightVisitor.Visit(node.Right);

                    var coalesceLeft = leftVisitor.ToString();
                    var coalesceRight = rightVisitor.ToString();

                    visitor.Builder!.Append(node.Type == typeof(bool)
                        ? visitor.Dialect.MakeBoolCoalesce(coalesceLeft, coalesceRight)
                        : visitor.Dialect.MakeCoalesce(coalesceLeft, coalesceRight));
                    return node;
                }
                break;
        }

        // A logical AND/OR is a condition, so each operand is rendered as a predicate. On a dialect
        // without a boolean type a bare boolean value (e.g. a bit column) has to be turned into a
        // predicate first, otherwise the emitted <c>and</c>/<c>or</c> is rejected (SQL Server).
        if (!visitor.IsParamMode && node.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            visitor.Builder!.Append('(');
            AppendCondition(visitor, node.Left);
            visitor.Builder!.Append(node.NodeType == ExpressionType.AndAlso ? " and " : " or ");
            AppendCondition(visitor, node.Right);
            visitor.Builder!.Append(')');
            return node;
        }

        // A string concatenation is delegated to the dialect: dialects where the infix operator is
        // not a concatenation (MySQL/MariaDB, ClickHouse) emit the concat function instead.
        if (!visitor.IsParamMode && node.NodeType == ExpressionType.Add && node.Type == typeof(string))
        {
            visitor.Builder!.Append('(');
            visitor.Builder!.Append(visitor.Dialect.MakeConcat([visitor.VisitToString(node.Left), visitor.VisitToString(node.Right)]));
            visitor.Builder!.Append(')');
            return node;
        }

        if (!visitor.IsParamMode)
            visitor.Builder!.Append('(');

        visitor.Visit(node.Left);

        if (!visitor.IsParamMode)
        {
            switch (node.NodeType)
            {
                case ExpressionType.Add:
                    visitor.Builder!.Append(" + ");
                    break;
                case ExpressionType.And:
                    visitor.Builder!.Append(" & "); break;
                case ExpressionType.AndAlso:
                    visitor.Builder!.Append(" and "); break;
                case ExpressionType.Decrement:
                    visitor.Builder!.Append(" -1 "); break;
                case ExpressionType.Divide:
                    visitor.Builder!.Append(" / "); break;
                case ExpressionType.GreaterThan:
                    visitor.Builder!.Append(" > "); break;
                case ExpressionType.GreaterThanOrEqual:
                    visitor.Builder!.Append(" >= "); break;
                case ExpressionType.Increment:
                    visitor.Builder!.Append(" + 1"); break;
                case ExpressionType.LeftShift:
                    visitor.Builder!.Append(" << "); break;
                case ExpressionType.LessThan:
                    visitor.Builder!.Append(" < "); break;
                case ExpressionType.LessThanOrEqual:
                    visitor.Builder!.Append(" <= "); break;
                case ExpressionType.Modulo:
                    visitor.Builder!.Append(" % "); break;
                case ExpressionType.Multiply:
                    visitor.Builder!.Append(" * "); break;
                case ExpressionType.Negate:
                    visitor.Builder!.Append(" - "); break;
                case ExpressionType.Not:
                    visitor.Builder!.Append(" ~ "); break;
                case ExpressionType.NotEqual:
                    visitor.Builder!.Append(" != "); break;
                case ExpressionType.Equal:
                    visitor.Builder!.Append(" = "); break;
                case ExpressionType.Or:
                    visitor.Builder!.Append(" | "); break;
                case ExpressionType.OrElse:
                    visitor.Builder!.Append(" or "); break;
                case ExpressionType.Power:
                    visitor.Builder!.Append(" ^ "); break;
                case ExpressionType.RightShift:
                    visitor.Builder!.Append(" >> "); break;
                case ExpressionType.Subtract:
                    visitor.Builder!.Append(" - "); break;
                default:
                    throw new NotSupportedException(node.NodeType.ToString());
            }
        }

        visitor.Visit(node.Right);

        if (!visitor.IsParamMode)
            visitor.Builder!.Append(')');

        return node;
    }

}
